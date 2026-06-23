using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MutsumiPet.Services;

namespace MutsumiPet;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _speechTimer;
    private readonly SettingsService _settingsService;
    private readonly WindowsUsageMonitor _usageMonitor;
    private readonly ChatAppMessageMonitor _chatMessageMonitor;
    private readonly LlmClient _llmClient;
    private readonly PetInteractionService _interactionService;
    private readonly ChatConversationService _chatConversationService;
    private readonly SpeechQueueService _speechQueueService = new();
    private ChatWindow? _chatWindow;
    private bool _isPaused;
    private bool _isBusy;
    private bool _isChatBusy;

    /// <summary>
    /// 初始化桌宠窗口、服务对象和轮询计时器。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var appSettings = AppSettings.Load();
        var appClassifier = new AppClassifierService();
        _settingsService = new SettingsService();
        var usageSessionStore = new UsageSessionStore(Path.Combine(SettingsService.AppDataDirectory, "mutsumi.db"));
        var usageSessionTracker = new UsageSessionTracker(usageSessionStore, appClassifier);
        var focusSessionService = new FocusSessionService();
        var usageSummaryService = new UsageSummaryService(usageSessionStore);
        _usageMonitor = new WindowsUsageMonitor(appClassifier);
        _chatMessageMonitor = new ChatAppMessageMonitor();
        _llmClient = new LlmClient(appSettings);
        _interactionService = new PetInteractionService(
            _usageMonitor,
            _llmClient,
            _settingsService,
            usageSessionTracker,
            focusSessionService,
            usageSummaryService);
        _chatConversationService = new ChatConversationService(
            new ChatCommandService(),
            _interactionService,
            new ConversationMemoryService());
        _chatMessageMonitor.MessageReceived += ChatMessageMonitor_MessageReceived;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _pollTimer.Tick += PollTimer_Tick;

        _speechTimer = new DispatcherTimer();
        _speechTimer.Tick += SpeechTimer_Tick;

        LoadPetImage();
    }

    /// <summary>
    /// 窗口加载完成后定位到桌面右下角并触发首次问候。
    /// </summary>
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionNearBottomRight();
        _pollTimer.Start();
        ApplyMessageMonitorState();
        await RunInteractionCheckAsync(force: true);
    }

    /// <summary>
    /// 窗口关闭时停止轮询并释放后台服务。
    /// </summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _pollTimer.Stop();
        _speechTimer.Stop();
        _shutdownTokenSource.Cancel();
        _interactionService.FlushTracking();
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        _chatMessageMonitor.MessageReceived -= ChatMessageMonitor_MessageReceived;
        _chatWindow?.Close();
        _chatMessageMonitor.Dispose();
        _usageMonitor.Dispose();
        _llmClient.Dispose();
        _shutdownTokenSource.Dispose();
    }

    /// <summary>
    /// 左键拖动时移动整个桌宠窗口。
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 拖动过程中鼠标状态变化时 WPF 可能抛出异常，忽略即可。
        }
    }

    /// <summary>
    /// 右键菜单刷新项触发一次强制互动。
    /// </summary>
    private async void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _speechQueueService.Clear();
        _speechTimer.Stop();
        await RunInteractionCheckAsync(force: true);
    }

    /// <summary>
    /// 右键菜单暂停项切换自动互动状态。
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
    /// 输入按钮打开单例浮动输入面板。
    /// </summary>
    private void MessageButton_Click(object sender, RoutedEventArgs e)
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

    /// <summary>
    /// 右键菜单退出项关闭应用。
    /// </summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 打开隐私与设置窗口。
    /// </summary>
    private void OpenSettingsWindow()
    {
        var window = new SettingsWindow(_settingsService.Current)
        {
            Owner = this
        };

        if (window.ShowDialog() == true && window.SavedSettings is not null)
        {
            _settingsService.Save(window.SavedSettings);
        }
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
    /// 用户设置保存后更新运行中的监听状态。
    /// </summary>
    private void SettingsService_SettingsChanged(object? sender, UserSettings e)
    {
        ApplyMessageMonitorState();
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
        SpeechText.Text = "小睦正在思考...";
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
