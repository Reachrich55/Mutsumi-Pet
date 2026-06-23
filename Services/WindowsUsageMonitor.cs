using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class WindowsUsageMonitor : IDisposable
{
    private const uint IdleThresholdSeconds = 180;
    private const uint IdleReturnSeconds = 30;
    private static readonly TimeSpan AppDwellThreshold = TimeSpan.FromSeconds(60);
    private readonly AppClassifierService _appClassifier;
    private readonly object _sessionEventLock = new();
    private IntPtr _lastWindowHandle = IntPtr.Zero;
    private DateTimeOffset _activeWindowStartedAt = DateTimeOffset.Now;
    private bool _hasCaptured;
    private bool _wasIdle;
    private bool _isSessionLocked;
    private bool _hasReportedActiveWindowDwell;
    private UsageEventKind? _pendingSessionEvent;

    /// <summary>
    /// 初始化 Windows 使用状态监控并订阅会话切换事件。
    /// </summary>
    public WindowsUsageMonitor()
        : this(new AppClassifierService())
    {
    }

    /// <summary>
    /// 初始化 Windows 使用状态监控并订阅会话切换事件。
    /// </summary>
    public WindowsUsageMonitor(AppClassifierService appClassifier)
    {
        _appClassifier = appClassifier;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>
    /// 采集当前前台窗口、空闲时长和最近使用事件。
    /// </summary>
    public UsageSnapshot CaptureSnapshot()
    {
        return CaptureSnapshot(new UserSettings());
    }

    /// <summary>
    /// 按用户设置采集当前前台窗口、空闲时长和最近使用事件。
    /// </summary>
    public UsageSnapshot CaptureSnapshot(UserSettings settings)
    {
        var now = DateTimeOffset.Now;
        var windowHandle = NativeMethods.GetForegroundWindow();
        var idleSeconds = GetIdleSeconds();
        var idleThresholdSeconds = (uint)Math.Max(30, settings.IdleThresholdSeconds);
        var isIdle = idleSeconds >= idleThresholdSeconds;
        var firstCapture = !_hasCaptured;
        var windowChanged = windowHandle != _lastWindowHandle;

        if (windowChanged)
        {
            _lastWindowHandle = windowHandle;
            _activeWindowStartedAt = now;
            _hasReportedActiveWindowDwell = false;
        }

        _hasCaptured = true;
        var activeWindowDuration = windowHandle == IntPtr.Zero ? TimeSpan.Zero : now - _activeWindowStartedAt;
        var eventKind = ResolveEventKind(
            firstCapture,
            windowChanged,
            idleSeconds,
            idleThresholdSeconds,
            TimeSpan.FromMinutes(settings.ContinuousUseMinutes),
            activeWindowDuration);
        var recentEvent = DescribeEvent(eventKind);
        _wasIdle = isIdle;
        var processName = GetProcessName(windowHandle);

        return new UsageSnapshot
        {
            CapturedAt = now,
            ProcessName = processName,
            WindowTitle = GetWindowTitle(windowHandle),
            AppCategory = _appClassifier.ClassifyProcess(processName),
            IdleSeconds = idleSeconds,
            IsIdle = isIdle,
            ActiveWindowDuration = activeWindowDuration,
            EventKind = eventKind,
            RecentEvent = recentEvent,
            IsSessionLocked = _isSessionLocked
        };
    }

    /// <summary>
    /// 取消会话事件订阅。
    /// </summary>
    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 根据 Windows 会话切换事件记录锁屏或解锁状态。
    /// </summary>
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        lock (_sessionEventLock)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                _isSessionLocked = true;
                _pendingSessionEvent = UsageEventKind.SessionLocked;
                return;
            }

            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _isSessionLocked = false;
                _pendingSessionEvent = UsageEventKind.SessionUnlocked;
            }
        }
    }

    /// <summary>
    /// 按采集状态推导最近一次电脑使用事件。
    /// </summary>
    private UsageEventKind ResolveEventKind(
        bool firstCapture,
        bool windowChanged,
        uint idleSeconds,
        uint idleThresholdSeconds,
        TimeSpan continuousUseThreshold,
        TimeSpan activeWindowDuration)
    {
        var pendingSessionEvent = ConsumePendingSessionEvent();
        if (pendingSessionEvent.HasValue)
        {
            return pendingSessionEvent.Value;
        }

        if (firstCapture)
        {
            return UsageEventKind.Startup;
        }

        var idleReturnSeconds = Math.Min(IdleReturnSeconds, Math.Max(5, idleThresholdSeconds / 2));
        if (_wasIdle && idleSeconds <= idleReturnSeconds)
        {
            return UsageEventKind.IdleReturned;
        }

        if (!_wasIdle && idleSeconds >= idleThresholdSeconds)
        {
            return UsageEventKind.IdleStarted;
        }

        if (windowChanged)
        {
            return UsageEventKind.AppSwitch;
        }

        if (!_hasReportedActiveWindowDwell && activeWindowDuration >= AppDwellThreshold)
        {
            _hasReportedActiveWindowDwell = true;
            return UsageEventKind.AppDwell;
        }

        return activeWindowDuration >= continuousUseThreshold
            ? UsageEventKind.ContinuousUse
            : UsageEventKind.Routine;
    }

    /// <summary>
    /// 读取并清除最近一次待处理的会话事件。
    /// </summary>
    private UsageEventKind? ConsumePendingSessionEvent()
    {
        lock (_sessionEventLock)
        {
            var pending = _pendingSessionEvent;
            _pendingSessionEvent = null;
            return pending;
        }
    }

    /// <summary>
    /// 将事件类型转换成适合提示词使用的中文描述。
    /// </summary>
    private static string DescribeEvent(UsageEventKind eventKind)
    {
        return eventKind switch
        {
            UsageEventKind.Startup => "启动观察",
            UsageEventKind.AppSwitch => "前台应用切换",
            UsageEventKind.AppDwell => "前台应用停留",
            UsageEventKind.IdleStarted => "进入空闲",
            UsageEventKind.IdleReturned => "空闲后返回",
            UsageEventKind.ContinuousUse => "连续使用",
            UsageEventKind.SessionLocked => "会话锁定",
            UsageEventKind.SessionUnlocked => "会话解锁",
            _ => "常规观察"
        };
    }

    /// <summary>
    /// 通过 Win API 读取距离最后一次用户输入的秒数。
    /// </summary>
    private static uint GetIdleSeconds()
    {
        var info = new NativeMethods.LastInputInfo
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LastInputInfo>()
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return 0;
        }

        var currentTick = unchecked((uint)Environment.TickCount);
        var idleMilliseconds = currentTick - info.dwTime;
        return idleMilliseconds / 1000;
    }

    /// <summary>
    /// 获取前台窗口标题，失败时返回占位文本。
    /// </summary>
    private static string GetWindowTitle(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return "无前台窗口";
        }

        var builder = new StringBuilder(512);
        var length = NativeMethods.GetWindowTextW(windowHandle, builder, builder.Capacity);
        return length > 0 ? builder.ToString() : "无标题窗口";
    }

    /// <summary>
    /// 获取前台窗口所属进程名，失败时返回 Unknown。
    /// </summary>
    private static string GetProcessName(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return "Unknown";
        }

        NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
        {
            return "Unknown";
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "Unknown";
        }
        catch (InvalidOperationException)
        {
            return "Unknown";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "Unknown";
        }
    }
}
