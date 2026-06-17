using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MutsuPet.Services;

namespace MutsuPet;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _speechTimer;
    private readonly WindowsUsageMonitor _usageMonitor;
    private readonly ChatAppMessageMonitor _chatMessageMonitor;
    private readonly LlmClient _llmClient;
    private readonly PetInteractionService _interactionService;
    private readonly SpeechQueueService _speechQueueService = new();
    private bool _isPaused;
    private bool _isBusy;

    /// <summary>
    /// 初始化桌宠窗口、服务对象和轮询计时器。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var settings = AppSettings.Load();
        _usageMonitor = new WindowsUsageMonitor();
        _chatMessageMonitor = new ChatAppMessageMonitor();
        _llmClient = new LlmClient(settings);
        _interactionService = new PetInteractionService(_usageMonitor, _llmClient);
        _chatMessageMonitor.MessageReceived += ChatMessageMonitor_MessageReceived;

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
        _chatMessageMonitor.Start(this);
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
        _chatMessageMonitor.MessageReceived -= ChatMessageMonitor_MessageReceived;
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
    /// 右键菜单退出项关闭应用。
    /// </summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
    /// QQ/微信窗口监听到新消息信号时触发优先提醒。
    /// </summary>
    private async void ChatMessageMonitor_MessageReceived(object? sender, Models.MessageNotification notification)
    {
        if (_isPaused)
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
            var imagePath = AssetPathResolver.FindRequired("mutsu.png");
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
        if (_isBusy || (_isPaused && !force))
        {
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
}
