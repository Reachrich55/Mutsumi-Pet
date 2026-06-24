using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MutsumiPet.Models;
using MutsumiPet.Services;

namespace MutsumiPet;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _speechTimer;
    private readonly DispatcherTimer _emotionTimer;
    private readonly AppSettings _appSettings;
    private readonly SettingsService _settingsService;
    private readonly WindowsUsageMonitor _usageMonitor;
    private readonly ChatAppMessageMonitor _chatMessageMonitor;
    private readonly LlmClient _llmClient;
    private readonly PetInteractionService _interactionService;
    private readonly ChatConversationService _chatConversationService;
    private readonly PersonaManager _personaManager;
    private readonly PersonaConversationMemoryService _personaMemoryService;
    private readonly SpeechQueueService _speechQueueService = new();
    private ChatWindow? _chatWindow;
    private SettingsWindow? _settingsWindow;
    private bool _isPaused;
    private bool _isBusy;
    private bool _isChatBusy;

    // 显示缩放常量
    private const double BaseWindowSize = 280.0;   // 100% 基准窗口尺寸
    private const double CanvasSize = 600.0;        // 内部 Canvas 固定尺寸
    private static readonly double[] ValidScaleOptions = [50, 75, 100, 125, 150, 175];
    private static readonly double[] ValidFontSizeOptions = [9.0, 10.5, 12.0, 14.0, 16.0, 18.0, 20.0, 24.0, 28.0, 32.0];

    // 拖动与双击检测
    private Point _dragStartPoint;
    private bool _isDragging;
    private DateTime _lastClickTime = DateTime.MinValue;
    private static readonly TimeSpan DoubleClickWindow = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// 初始化桌宠窗口、服务对象和轮询计时器。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _appSettings = AppSettings.Load();
        var appClassifier = new AppClassifierService();
        _settingsService = new SettingsService();
        var usageSessionStore = new UsageSessionStore(Path.Combine(SettingsService.AppDataDirectory, "mutsumi.db"));
        var usageSessionTracker = new UsageSessionTracker(usageSessionStore, appClassifier);
        var focusSessionService = new FocusSessionService();
        var usageSummaryService = new UsageSummaryService(usageSessionStore);
        _usageMonitor = new WindowsUsageMonitor(appClassifier);
        _chatMessageMonitor = new ChatAppMessageMonitor();
        _llmClient = new LlmClient(_appSettings);
        _personaManager = new PersonaManager(
            _settingsService.Current.ActivePersonaId);
        _personaMemoryService = new PersonaConversationMemoryService();
        _interactionService = new PetInteractionService(
            _usageMonitor,
            _llmClient,
            _settingsService,
            usageSessionTracker,
            focusSessionService,
            usageSummaryService,
            _personaManager);
        _chatConversationService = new ChatConversationService(
            new ChatCommandService(),
            _interactionService,
            _personaManager,
            _personaMemoryService);
        _chatMessageMonitor.MessageReceived += ChatMessageMonitor_MessageReceived;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        _personaManager.CurrentPersonaChanged += PersonaManager_CurrentPersonaChanged;

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _pollTimer.Tick += PollTimer_Tick;

        _speechTimer = new DispatcherTimer();
        _speechTimer.Tick += SpeechTimer_Tick;

        _emotionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(45)
        };
        _emotionTimer.Tick += EmotionTimer_Tick;

        LoadPetImage();
        ApplyDisplaySettings(_settingsService.Current);
    }

    /// <summary>
    /// 窗口加载完成后定位到桌面右下角并触发首次问候。
    /// 首次启动时如果没有 API Key，自动打开设置窗口。
    /// </summary>
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionNearBottomRight();
        _pollTimer.Start();
        _emotionTimer.Start();
        ApplyMessageMonitorState();

        // 首次启动：如果没有有效 API 配置，自动打开设置
        if (!_appSettings.IsLlmEnabled)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                OpenSettingsWindow(apiTab: 0);
            }));
        }

        await RunInteractionCheckAsync(force: true);
    }

    /// <summary>
    /// 窗口关闭时停止轮询并释放后台服务。
    /// </summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _pollTimer.Stop();
        _speechTimer.Stop();
        _emotionTimer.Stop();
        _shutdownTokenSource.Cancel();
        _interactionService.FlushTracking();
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        _personaManager.CurrentPersonaChanged -= PersonaManager_CurrentPersonaChanged;
        _chatMessageMonitor.MessageReceived -= ChatMessageMonitor_MessageReceived;
        _chatWindow?.Close();
        _settingsWindow?.Close();
        _chatMessageMonitor.Dispose();
        _usageMonitor.Dispose();
        _llmClient.Dispose();
        _shutdownTokenSource.Dispose();
    }

    // ────────────── 拖动与双击 ──────────────

    /// <summary>
    /// 记录鼠标按下位置，检测双击打开聊天窗口。
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        if (now - _lastClickTime < DoubleClickWindow)
        {
            // 双击 → 打开聊天窗口
            _lastClickTime = DateTime.MinValue;
            _isDragging = false;
            OpenChatWindow();
            e.Handled = true;
            return;
        }

        _lastClickTime = now;
        _dragStartPoint = e.GetPosition(this);
        _isDragging = false;
    }

    /// <summary>
    /// 鼠标移动超过阈值时启动窗口拖动。
    /// </summary>
    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging)
        {
            return;
        }

        var delta = e.GetPosition(this) - _dragStartPoint;
        if (Math.Abs(delta.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(delta.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isDragging = true;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 拖动过程中鼠标状态变化时 WPF 可能抛出异常，忽略即可。
            }
        }
    }

    /// <summary>
    /// 鼠标抬起时重置拖动状态。
    /// </summary>
    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
    }

    /// <summary>
    /// Tab 键循环切换人格。
    /// 仅在桌宠主窗口激活且无子窗口/输入控件焦点时响应。
    /// </summary>
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.Handled)
        {
            return;
        }

        // 仅当主窗口处于激活状态时响应
        if (!IsActive)
        {
            return;
        }

        // 右键菜单打开时不响应
        if (ContextMenu?.IsOpen == true)
        {
            return;
        }

        // 聊天窗口或设置窗口可见时不响应
        if (_chatWindow?.IsVisible == true || _settingsWindow?.IsVisible == true)
        {
            return;
        }

        // TextBox 获得焦点时保留 Tab 原生导航
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
        {
            return;
        }

        e.Handled = true;
        CycleToNextPersona();
    }

    // ────────────── 右键菜单 ──────────────

    /// <summary>
    /// 右键菜单 - 打开聊天。
    /// </summary>
    private void OpenChatMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenChatWindow();
    }

    /// <summary>
    /// 右键菜单 - 刷新对话。
    /// </summary>
    private async void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _speechQueueService.Clear();
        _speechTimer.Stop();
        await RunInteractionCheckAsync(force: true);
    }

    /// <summary>
    /// 右键菜单 - 打开设置（单例窗口）。
    /// </summary>
    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    /// <summary>
    /// 右键菜单 - 暂停互动。
    /// </summary>
    private async void PauseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = PauseMenuItem.IsChecked;
        if (_isPaused)
        {
            _speechTimer.Stop();
            _speechQueueService.Clear();
            SpeechText.Text = "我先安静一会儿。";
        }
        else
        {
            QueueSpeechText("我回来啦。", replaceExisting: true);
        }

        if (!_isPaused)
        {
            await RunInteractionCheckAsync(force: true);
        }
    }

    /// <summary>
    /// 右键菜单 - 切换人格（子项共享此处理器）。
    /// </summary>
    private void PersonaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string personaId)
        {
            return;
        }

        var oldPersona = _personaManager.Current;
        if (!_personaManager.SetCurrent(personaId))
        {
            return;
        }

        var newPersona = _personaManager.Current;
        ApplyPersonaSwitch(oldPersona, newPersona);
    }

    /// <summary>
    /// 右键菜单 - 退出。
    /// </summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 按确定性顺序循环切换到下一个人格。
    /// </summary>
    private void CycleToNextPersona()
    {
        var oldPersona = _personaManager.Current;
        if (!_personaManager.TryCycleNext(out var newPersona))
        {
            return;
        }

        ApplyPersonaSwitch(oldPersona, newPersona);
    }

    /// <summary>
    /// 执行人格切换的完整流程：取消旧请求、保存设置、清理气泡、显示切换台词、更新菜单和窗口。
    /// </summary>
    private void ApplyPersonaSwitch(PersonaProfile oldPersona, PersonaProfile newPersona)
    {
        // 1. 取消旧人格尚未完成的 LLM 请求（主动互动 + 聊天独立取消路径）
        _interactionService.CancelInFlightRequests();

        // 2. 清空尚未显示的气泡队列
        _speechTimer.Stop();
        _speechQueueService.Clear();

        // 3. 保存新人格 ID 到持久化设置
        var settings = _settingsService.Current.CloneNormalized();
        settings.ActivePersonaId = newPersona.Id;
        _settingsService.Save(settings);

        // 4. 显示切换台词（切出 + 切入）
        if (!string.IsNullOrWhiteSpace(oldPersona.SwitchOutText))
        {
            _speechQueueService.EnqueueText(oldPersona.SwitchOutText, replaceExisting: true);
        }
        if (!string.IsNullOrWhiteSpace(newPersona.SwitchInText))
        {
            _speechQueueService.EnqueueText(newPersona.SwitchInText, replaceExisting: !string.IsNullOrWhiteSpace(oldPersona.SwitchOutText));
        }
        else
        {
            ShowNextSpeechSegment();
        }

        // 如有切换台词则开始展示
        if (!string.IsNullOrWhiteSpace(oldPersona.SwitchOutText) || !string.IsNullOrWhiteSpace(newPersona.SwitchInText))
        {
            ShowNextSpeechSegment();
        }

        // 5. 更新右键菜单勾选状态
        MutsumiPersonaMenuItem.IsChecked = newPersona.Id == "mutsumi";
        MortisPersonaMenuItem.IsChecked = newPersona.Id == "mortis";

        // 6. 更新聊天窗口标题和占位文字
        _chatWindow?.UpdatePersona(newPersona.DisplayName);

        // 7. 暂停状态下更新静止文本
        if (_isPaused)
        {
            SpeechText.Text = "我先安静一会儿。";
        }
    }

    /// <summary>
    /// PersonaManager 人格切换事件处理。
    /// </summary>
    private void PersonaManager_CurrentPersonaChanged(object? sender, PersonaProfile persona)
    {
        // UI 更新由 ApplyPersonaSwitch 统一处理。
        // 此事件监听用于未来扩展（如设置窗口中切换人格）。
    }

    // ────────────── 对话框按钮 ──────────────

    /// <summary>
    /// 输入按钮打开单例浮动输入面板。
    /// </summary>
    private void MessageButton_Click(object sender, RoutedEventArgs e)
    {
        OpenChatWindow();
    }

    /// <summary>
    /// 打开聊天窗口（单例）。
    /// </summary>
    private void OpenChatWindow()
    {
        if (_chatWindow is not null)
        {
            _chatWindow.Activate();
            return;
        }

        _chatWindow = new ChatWindow(
            _chatConversationService,
            _shutdownTokenSource.Token)
        {
            Owner = this
        };
        _chatWindow.InputSubmitted += ChatWindow_InputSubmitted;
        _chatWindow.Closed += ChatWindow_Closed;
        _chatWindow.Show();
    }

    /// <summary>
    /// 对话窗口关闭后清理单例引用。
    /// </summary>
    private void ChatWindow_Closed(object? sender, EventArgs e)
    {
        if (_chatWindow is not null)
        {
            _chatWindow.Closed -= ChatWindow_Closed;
            _chatWindow.InputSubmitted -= ChatWindow_InputSubmitted;
            _chatWindow = null;
        }
    }

    /// <summary>
    /// 处理输入面板提交的聊天文本，并将结果回流到原始气泡。
    /// </summary>
    private async void ChatWindow_InputSubmitted(object? sender, string input)
    {
        if (_isChatBusy)
        {
            QueueSpeechText("我还在处理上一条内容。", replaceExisting: true);
            return;
        }

        _isChatBusy = true;
        ShowThinkingBubble();
        try
        {
            var result = await _chatConversationService.SubmitAsync(input, _shutdownTokenSource.Token);
            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                QueueSpeechText(result.Text, replaceExisting: true);
            }

            if (result.OpenSettings)
            {
                OpenSettingsWindow();
            }
        }
        catch (OperationCanceledException)
        {
            QueueSpeechText("应用正在关闭，这次消息就先停在这里。", replaceExisting: true);
        }
        finally
        {
            _isChatBusy = false;
        }
    }

    // ────────────── 设置窗口（单例）────────────────

    /// <summary>
    /// 打开隐私与设置窗口（单例）。
    /// </summary>
    private void OpenSettingsWindow(int apiTab = -1)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            if (apiTab >= 0)
            {
                _settingsWindow.SelectTab(apiTab);
            }
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsService.Current, _appSettings, _settingsService)
        {
            Owner = this
        };

        if (apiTab >= 0)
        {
            _settingsWindow.SelectTab(apiTab);
        }

        _settingsWindow.Closed += SettingsWindow_Closed;
        _settingsWindow.ShowDialog();
    }

    /// <summary>
    /// 设置窗口关闭后保存设置并重新加载 LLM 配置。
    /// </summary>
    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        if (_settingsWindow is null) return;

        _settingsWindow.Closed -= SettingsWindow_Closed;

        if (_settingsWindow.DialogResult == true && _settingsWindow.SavedSettings is not null)
        {
            _settingsService.Save(_settingsWindow.SavedSettings, _settingsWindow.PlaintextApiKey);
            _appSettings.Reload();
        }

        _settingsWindow = null;
    }

    // ────────────── 定时器与互动 ──────────────

    /// <summary>
    /// 定时更新所有人格的情绪状态。
    /// </summary>
    private void EmotionTimer_Tick(object? sender, EventArgs e)
    {
        // 通过 WindowsUsageMonitor 判断用户是否活跃（非空闲）
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        var isUserActive = !snapshot.IsIdle;

        _interactionService.UpdateAllEmotions(isUserActive);
    }

    /// <summary>
    /// 定时检查用户状态并在需要时更新对话气泡。
    /// </summary>
    private async void PollTimer_Tick(object? sender, EventArgs e)
    {
        await RunInteractionCheckAsync(force: false);
    }

    /// <summary>
    /// 自动翻页计时器触发时展示下一段气泡文本。
    /// </summary>
    private void SpeechTimer_Tick(object? sender, EventArgs e)
    {
        ShowNextSpeechSegment();
    }

    /// <summary>
    /// 用户设置保存后更新监听状态、显示设置并刷新 LLM 配置。
    /// </summary>
    private void SettingsService_SettingsChanged(object? sender, UserSettings e)
    {
        ApplyMessageMonitorState();
        ApplyDisplaySettings(e);
    }

    /// <summary>
    /// QQ/微信窗口监听到新消息信号时触发优先提醒。
    /// </summary>
    private async void ChatMessageMonitor_MessageReceived(object? sender, Models.MessageNotification notification)
    {
        if (_isPaused || !_settingsService.Current.EnableMessageReminders)
        {
            return;
        }

        var interactionTask = await Dispatcher.InvokeAsync(() => RunMessageInteractionAsync(notification));
        await interactionTask;
    }

    // ────────────── 图片与位置 ──────────────

    /// <summary>
    /// 加载角色图片并在运行时去除图片外部白底。
    /// </summary>
    private void LoadPetImage()
    {
        try
        {
            var imagePath = AssetPathResolver.FindRequired("mutsumi.png");
            PetImage.Source = ImageTransparencyService.CreateTransparentImage(imagePath);
        }
        catch (Exception ex)
        {
            SpeechText.Text = $"找不到形象图：{ex.Message}";
        }
    }

    /// <summary>
    /// 将桌宠窗口初始放置在工作区右下角。
    /// </summary>
    private void PositionNearBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 24);
        Top = Math.Max(workArea.Top, workArea.Bottom - Height - 24);
    }

    // ────────────── 显示设置应用 ──────────────

    /// <summary>
    /// 统一应用桌宠缩放和气泡字号设置。
    /// </summary>
    private void ApplyDisplaySettings(UserSettings settings)
    {
        // 规范化为合法值
        var petScale = ClampToValidOptions(settings.PetScalePercent, ValidScaleOptions, 150.0);
        var fontSizePt = ClampToValidOptions(settings.SpeechFontSizePt, ValidFontSizeOptions, 16.0);

        // 保存旧位置用于缩放后重新定位
        var oldBottom = Top + Height;
        var oldRight = Left + Width;
        var oldCenterX = Left + Width / 2;
        var oldCenterY = Top + Height / 2;

        // 计算新窗口尺寸
        var newSize = BaseWindowSize * petScale / 100.0;

        // 应用窗口大小
        Width = newSize;
        Height = newSize;

        // 应用气泡字号（pt → DIP 转换）
        SpeechText.FontSize = PointToDip(fontSizePt);

        // 更新气泡布局
        UpdateBubbleLayout();

        // 重新定位窗口：保持底部/右下角锚定
        RepositionAfterResize(oldBottom, oldRight, oldCenterX, oldCenterY);
    }

    /// <summary>
    /// 根据当前窗口尺寸计算气泡覆盖层的位置和宽度。
    /// Canvas 坐标为 600×600 基准，通过当前窗口比例映射。
    /// </summary>
    private void UpdateBubbleLayout()
    {
        var scale = Width / CanvasSize;

        // Canvas 坐标 (58, 80)，宽度 238 → 窗口坐标
        var bubbleLeft = 58.0 * scale;
        var bubbleTop = 80.0 * scale;
        var bubbleWidth = 238.0 * scale;
        var bubbleMaxHeight = 360.0 * scale;

        SpeechBubble.Margin = new Thickness(bubbleLeft, bubbleTop, 0, 0);
        SpeechBubble.Width = bubbleWidth;
        SpeechBubble.MaxHeight = bubbleMaxHeight;
        SpeechBubble.MinHeight = 40.0 * scale;
    }

    /// <summary>
    /// 缩放后重新定位窗口，优先保持右下角锚定，确保不超出屏幕。
    /// </summary>
    private void RepositionAfterResize(double oldBottom, double oldRight,
        double oldCenterX, double oldCenterY)
    {
        var workArea = SystemParameters.WorkArea;

        // 如果之前靠近右下角，保持右下角锚定
        var wasNearBottomRight = oldBottom >= workArea.Bottom - 48 &&
                                 oldRight >= workArea.Right - 48;

        if (wasNearBottomRight)
        {
            Left = Math.Max(workArea.Left, workArea.Right - Width - 24);
            Top = Math.Max(workArea.Top, workArea.Bottom - Height - 24);
        }
        else
        {
            // 保持底部边缘稳定
            Left = Math.Max(workArea.Left,
                Math.Min(oldRight - Width, workArea.Right - Width));
            Top = Math.Max(workArea.Top,
                Math.Min(oldBottom - Height, workArea.Bottom - Height));
        }

        // 最终边界修正
        EnforceScreenBounds();
    }

    /// <summary>
    /// 确保窗口不超出屏幕边界。
    /// </summary>
    private void EnforceScreenBounds()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, workArea.Left - Width + 48, workArea.Right - 48);
        Top = Math.Clamp(Top, workArea.Top, workArea.Bottom - 48);
    }

    /// <summary>
    /// 将磅值 (pt) 转换为 WPF 设备无关像素 (DIP)。
    /// 1 pt = 1/72 inch, 1 DIP = 1/96 inch → DIP = pt × 96/72。
    /// </summary>
    private static double PointToDip(double pointSize)
    {
        return pointSize * 96.0 / 72.0;
    }

    /// <summary>
    /// 将数值限制在合法选项列表中，无匹配时返回默认值。
    /// </summary>
    private static double ClampToValidOptions(double value, double[] options, double defaultValue)
    {
        foreach (var option in options)
        {
            if (Math.Abs(value - option) < 0.01)
            {
                return option;
            }
        }

        return defaultValue;
    }

    // ────────────── 互动编排 ──────────────

    /// <summary>
    /// 根据当前状态决定是否请求互动文本并更新 UI。
    /// </summary>
    private async Task RunInteractionCheckAsync(bool force)
    {
        if (_isBusy)
        {
            return;
        }

        if (_isPaused && !force)
        {
            _interactionService.ProcessTrackingOnly();
            return;
        }

        _isBusy = true;
        try
        {
            if (force)
            {
                SpeechText.Text = "我来啦...";
            }

            var line = await _interactionService.GetNextLineAsync(force, _shutdownTokenSource.Token);
            if (!string.IsNullOrWhiteSpace(line))
            {
                QueueSpeechText(line, replaceExisting: force);
            }
        }
        catch (OperationCanceledException)
        {
            // 应用关闭时取消请求，不需要提示用户。
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// 根据收到的聊天消息信号生成并展示桌宠提醒。
    /// </summary>
    private async Task RunMessageInteractionAsync(Models.MessageNotification notification)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var line = await _interactionService.GetMessageLineAsync(notification, _shutdownTokenSource.Token);
            if (!string.IsNullOrWhiteSpace(line))
            {
                QueueSpeechText(line, replaceExisting: true);
            }
        }
        catch (OperationCanceledException)
        {
            // 应用关闭时取消请求，不需要提示用户。
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ────────────── 气泡展示 ──────────────

    /// <summary>
    /// 将文本加入气泡展示队列并启动自动翻页。
    /// </summary>
    private void QueueSpeechText(string text, bool replaceExisting)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _speechQueueService.EnqueueText(text, replaceExisting);
        if (replaceExisting || !_speechTimer.IsEnabled)
        {
            ShowNextSpeechSegment();
        }
    }

    /// <summary>
    /// 清空当前气泡队列并显示思考中的占位文本。
    /// </summary>
    private void ShowThinkingBubble()
    {
        _speechTimer.Stop();
        _speechQueueService.Clear();
        SpeechText.Text = _personaManager.Current.ThinkingText;
    }

    /// <summary>
    /// 展示队列中的下一段文本并安排下一次翻页。
    /// </summary>
    private void ShowNextSpeechSegment()
    {
        if (!_speechQueueService.TryDequeue(out var segment))
        {
            _speechTimer.Stop();
            return;
        }

        SpeechText.Text = segment.Text;
        _speechTimer.Interval = segment.DisplayDuration;
        _speechTimer.Start();
    }

    /// <summary>
    /// 根据设置启停聊天软件消息监听器。
    /// </summary>
    private void ApplyMessageMonitorState()
    {
        if (_settingsService.Current.EnableMessageReminders)
        {
            _chatMessageMonitor.Start(this);
        }
        else
        {
            _chatMessageMonitor.Stop();
        }
    }
}
