using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MutsuPet.Models;

namespace MutsuPet.Services;

public sealed class ChatAppMessageMonitor : IDisposable
{
    private static readonly HashSet<string> QqProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "QQ",
        "QQNT",
        "TIM"
    };

    private static readonly HashSet<string> WeChatProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "WeChat",
        "Weixin",
        "WeChatAppEx"
    };

    private static readonly TimeSpan SourceCooldown = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SignalRetention = TimeSpan.FromMinutes(5);
    private readonly object _stateLock = new();
    private readonly Dictionary<NotificationSourceKind, DateTimeOffset> _lastPublishedAt = new();
    private readonly Dictionary<string, DateTimeOffset> _recentSignals = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _sweepTimer;
    private readonly NativeMethods.WinEventProc _eventProc;
    private IntPtr _eventHook = IntPtr.Zero;
    private HwndSource? _shellHookSource;
    private IntPtr _shellHookWindowHandle = IntPtr.Zero;
    private uint _shellHookMessage;
    private uint _nextMessageId;
    private bool _isDisposed;

    /// <summary>
    /// 初始化 QQ/微信窗口消息监听器。
    /// </summary>
    public ChatAppMessageMonitor()
    {
        _eventProc = HandleWinEvent;
        _sweepTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _sweepTimer.Tick += SweepTimer_Tick;
    }

    /// <summary>
    /// 当识别到 QQ 或微信可能有新消息时触发。
    /// </summary>
    public event EventHandler<MessageNotification>? MessageReceived;

    /// <summary>
    /// 获取监听器是否已经启动。
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// 启动窗口事件钩子和周期补扫。
    /// </summary>
    public void Start(Window shellHookWindow)
    {
        if (IsEnabled)
        {
            return;
        }

        RegisterShellHook(shellHookWindow);
        _eventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventObjectCreate,
            NativeMethods.EventObjectNameChange,
            IntPtr.Zero,
            _eventProc,
            0,
            0,
            NativeMethods.WinEventOutOfContext | NativeMethods.WinEventSkipOwnProcess);

        IsEnabled = true;
        _sweepTimer.Start();
        SweepTargetWindows();
    }

    /// <summary>
    /// 停止监听并释放 Win32 事件钩子。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _sweepTimer.Stop();
        if (_shellHookSource is not null)
        {
            _shellHookSource.RemoveHook(HandleShellMessage);
            _shellHookSource = null;
        }

        if (_shellHookWindowHandle != IntPtr.Zero)
        {
            NativeMethods.DeregisterShellHookWindow(_shellHookWindowHandle);
            _shellHookWindowHandle = IntPtr.Zero;
        }

        if (_eventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_eventHook);
            _eventHook = IntPtr.Zero;
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 注册主窗口接收任务栏闪烁等 Shell 通知。
    /// </summary>
    private void RegisterShellHook(Window shellHookWindow)
    {
        _shellHookMessage = NativeMethods.RegisterWindowMessageW("SHELLHOOK");
        _shellHookWindowHandle = new WindowInteropHelper(shellHookWindow).Handle;
        if (_shellHookMessage == 0 || _shellHookWindowHandle == IntPtr.Zero)
        {
            return;
        }

        _shellHookSource = HwndSource.FromHwnd(_shellHookWindowHandle);
        if (_shellHookSource is null)
        {
            return;
        }

        _shellHookSource.AddHook(HandleShellMessage);
        NativeMethods.RegisterShellHookWindow(_shellHookWindowHandle);
    }

    /// <summary>
    /// 处理任务栏按钮闪烁消息并转换为聊天消息信号。
    /// </summary>
    private IntPtr HandleShellMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_isDisposed || _shellHookMessage == 0 || msg != _shellHookMessage)
        {
            return IntPtr.Zero;
        }

        if (wParam.ToInt32() == NativeMethods.HShellFlash && lParam != IntPtr.Zero)
        {
            EvaluateWindow(lParam, MessageSignalOrigin.ShellFlash);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 周期补扫目标聊天软件窗口，弥补事件钩子遗漏。
    /// </summary>
    private void SweepTimer_Tick(object? sender, EventArgs e)
    {
        SweepTargetWindows();
    }

    /// <summary>
    /// 处理系统窗口事件并尝试识别新消息信号。
    /// </summary>
    private void HandleWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hWnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (_isDisposed ||
            hWnd == IntPtr.Zero ||
            idObject != NativeMethods.ObjectIdWindow ||
            idChild != NativeMethods.ChildIdSelf ||
            !IsTrackedEvent(eventType))
        {
            return;
        }

        EvaluateWindow(hWnd, MessageSignalOrigin.WinEvent);
    }

    /// <summary>
    /// 枚举顶层窗口并检查是否存在消息提示窗口或未读状态变化。
    /// </summary>
    private void SweepTargetWindows()
    {
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            EvaluateWindow(hWnd, MessageSignalOrigin.Sweep);
            return true;
        }, IntPtr.Zero);
    }

    /// <summary>
    /// 判断指定窗口是否代表 QQ/微信新消息信号。
    /// </summary>
    private void EvaluateWindow(IntPtr hWnd, MessageSignalOrigin origin)
    {
        if (origin != MessageSignalOrigin.ShellFlash && !NativeMethods.IsWindowVisible(hWnd))
        {
            return;
        }

        var processName = GetProcessName(hWnd);
        var sourceKind = ClassifyProcess(processName);
        if (sourceKind == NotificationSourceKind.Unknown)
        {
            return;
        }

        var title = GetWindowTitle(hWnd);
        var className = GetWindowClassName(hWnd);
        if (!IsLikelyMessageSignal(sourceKind, title, className, hWnd, origin))
        {
            return;
        }

        PublishMessage(sourceKind, BuildSignalKey(sourceKind, title, className));
    }

    /// <summary>
    /// 发布去重且经过冷却的新消息事件。
    /// </summary>
    private void PublishMessage(NotificationSourceKind sourceKind, string signalKey)
    {
        var now = DateTimeOffset.Now;
        MessageNotification notification;

        lock (_stateLock)
        {
            PruneSignals(now);
            if (_lastPublishedAt.TryGetValue(sourceKind, out var lastPublishedAt) &&
                now - lastPublishedAt < SourceCooldown)
            {
                return;
            }

            if (_recentSignals.TryGetValue(signalKey, out var lastSignalAt) &&
                now - lastSignalAt < SignalRetention)
            {
                return;
            }

            _recentSignals[signalKey] = now;
            _lastPublishedAt[sourceKind] = now;
            _nextMessageId++;
            notification = new MessageNotification
            {
                NotificationId = _nextMessageId,
                SourceKind = sourceKind,
                AppName = GetDisplayName(sourceKind),
                Title = "新消息",
                Body = string.Empty,
                CreatedAt = now
            };
        }

        MessageReceived?.Invoke(this, notification);
    }

    /// <summary>
    /// 移除过期信号，避免去重集合无限增长。
    /// </summary>
    private void PruneSignals(DateTimeOffset now)
    {
        var expiredKeys = _recentSignals
            .Where(pair => now - pair.Value >= SignalRetention)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _recentSignals.Remove(key);
        }
    }

    /// <summary>
    /// 判断窗口事件是否属于需要关注的类型。
    /// </summary>
    private static bool IsTrackedEvent(uint eventType)
    {
        return eventType is NativeMethods.EventObjectCreate
            or NativeMethods.EventObjectShow
            or NativeMethods.EventObjectStateChange
            or NativeMethods.EventObjectNameChange;
    }

    /// <summary>
    /// 判断进程名是否属于 QQ 或微信桌面客户端。
    /// </summary>
    private static NotificationSourceKind ClassifyProcess(string processName)
    {
        if (QqProcessNames.Contains(processName))
        {
            return NotificationSourceKind.Qq;
        }

        if (WeChatProcessNames.Contains(processName))
        {
            return NotificationSourceKind.WeChat;
        }

        return NotificationSourceKind.Unknown;
    }

    /// <summary>
    /// 根据窗口标题、类名和尺寸判断是否可能是新消息提示。
    /// </summary>
    private static bool IsLikelyMessageSignal(
        NotificationSourceKind sourceKind,
        string title,
        string className,
        IntPtr hWnd,
        MessageSignalOrigin origin)
    {
        if (origin == MessageSignalOrigin.ShellFlash)
        {
            return true;
        }

        var combined = $"{title} {className}";
        if (ContainsAny(combined, "新消息", "条消息", "未读", "有人@我", "收到消息"))
        {
            return true;
        }

        if (origin != MessageSignalOrigin.WinEvent ||
            string.IsNullOrWhiteSpace(title) ||
            IsKnownNonMessageTitle(sourceKind, title) ||
            !IsCompactWindow(hWnd))
        {
            return false;
        }

        return sourceKind switch
        {
            NotificationSourceKind.Qq => ContainsAny(className, "TXGuiFoundation", "QQ", "Chrome_WidgetWin"),
            NotificationSourceKind.WeChat => ContainsAny(className, "WeChat", "Weixin", "Chrome_WidgetWin"),
            _ => false
        };
    }

    /// <summary>
    /// 判断标题是否属于主窗口、设置、登录等非消息场景。
    /// </summary>
    private static bool IsKnownNonMessageTitle(NotificationSourceKind sourceKind, string title)
    {
        var normalized = title.Trim();
        if (sourceKind == NotificationSourceKind.Qq &&
            (normalized.Equals("QQ", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("TIM", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (sourceKind == NotificationSourceKind.WeChat &&
            (normalized.Equals("微信", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("WeChat", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("Weixin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return ContainsAny(normalized, "登录", "设置", "安全", "验证", "文件传输助手");
    }

    /// <summary>
    /// 判断窗口尺寸是否更像轻量提示窗口而非主界面。
    /// </summary>
    private static bool IsCompactWindow(IntPtr hWnd)
    {
        if (!NativeMethods.GetWindowRect(hWnd, out var rect))
        {
            return false;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        return width is >= 80 and <= 760 && height is >= 40 and <= 460;
    }

    /// <summary>
    /// 获取窗口所属进程名，失败时返回空字符串。
    /// </summary>
    private static string GetProcessName(IntPtr hWnd)
    {
        NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 读取窗口标题，失败时返回空字符串。
    /// </summary>
    private static string GetWindowTitle(IntPtr hWnd)
    {
        var builder = new StringBuilder(256);
        var length = NativeMethods.GetWindowTextW(hWnd, builder, builder.Capacity);
        return length > 0 ? builder.ToString() : string.Empty;
    }

    /// <summary>
    /// 读取窗口类名，失败时返回空字符串。
    /// </summary>
    private static string GetWindowClassName(IntPtr hWnd)
    {
        var builder = new StringBuilder(256);
        var length = NativeMethods.GetClassNameW(hWnd, builder, builder.Capacity);
        return length > 0 ? builder.ToString() : string.Empty;
    }

    /// <summary>
    /// 构造用于短期去重的消息信号键。
    /// </summary>
    private static string BuildSignalKey(NotificationSourceKind sourceKind, string title, string className)
    {
        return $"{sourceKind}:{title.Trim()}:{className.Trim()}";
    }

    /// <summary>
    /// 获取来源类型对应的显示名称。
    /// </summary>
    private static string GetDisplayName(NotificationSourceKind sourceKind)
    {
        return sourceKind switch
        {
            NotificationSourceKind.Qq => "QQ",
            NotificationSourceKind.WeChat => "微信",
            _ => "聊天软件"
        };
    }

    /// <summary>
    /// 判断文本中是否包含任意关键词。
    /// </summary>
    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private enum MessageSignalOrigin
    {
        Sweep,
        WinEvent,
        ShellFlash
    }
}
