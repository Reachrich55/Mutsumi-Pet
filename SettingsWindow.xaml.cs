using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MutsumiPet.Services;

namespace MutsumiPet;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _original;
    private readonly AppSettings _appSettings;
    private readonly SettingsService _settingsService;
    private bool _isTesting;

    /// <summary>
    /// 初始化设置窗口并填充当前设置。
    /// </summary>
    public SettingsWindow(UserSettings settings, AppSettings appSettings, SettingsService settingsService)
    {
        InitializeComponent();
        _original = settings;
        _appSettings = appSettings;
        _settingsService = settingsService;
        LoadSettings(settings);
        LoadAutoStartState();
    }

    /// <summary>
    /// 获取保存后的设置（包含加密后的 API Key）。
    /// </summary>
    public UserSettings? SavedSettings { get; private set; }

    /// <summary>
    /// 获取用户可能修改的明文 API Key（用于保存时加密）。
    /// </summary>
    public string? PlaintextApiKey { get; private set; }

    /// <summary>
    /// 程序化切换到指定 Tab。
    /// </summary>
    public void SelectTab(int index)
    {
        if (index >= 0 && index < SettingsTabControl.Items.Count)
        {
            SettingsTabControl.SelectedIndex = index;
        }
    }

    /// <summary>
    /// 将设置值写入窗口控件。
    /// </summary>
    private void LoadSettings(UserSettings settings)
    {
        var normalized = settings.CloneNormalized();

        // 模型服务
        ApiBaseUrlTextBox.Text = normalized.ApiBaseUrl ?? string.Empty;
        ApiKeyTextBox.Text = string.Empty; // 不预填明文 Key
        ModelTextBox.Text = normalized.ApiModel ?? string.Empty;
        TimeoutTextBox.Text = (normalized.ApiTimeoutSeconds ?? 60).ToString();

        // 如果有已保存的加密 Key，显示占位提示
        var hasSavedKey = !string.IsNullOrWhiteSpace(normalized.ApiKeyEncrypted);
        ShowApiKeyCheckBox.IsChecked = false;
        UpdateApiKeyVisibility(showPlaintext: false, hasEncrypted: hasSavedKey);

        // 隐私与监控
        EnableTrackingCheckBox.IsChecked = normalized.EnableTracking;
        EnableLlmCheckBox.IsChecked = normalized.EnableLlm;
        EnableMessageRemindersCheckBox.IsChecked = normalized.EnableMessageReminders;
        SendWindowTitleToLlmCheckBox.IsChecked = normalized.SendWindowTitleToLlm;
        StoreWindowTitlesCheckBox.IsChecked = normalized.StoreWindowTitles;
        EnableUsageSummaryCheckBox.IsChecked = normalized.EnableUsageSummary;

        // 交互行为
        IdleThresholdTextBox.Text = normalized.IdleThresholdSeconds.ToString();
        ContinuousUseTextBox.Text = normalized.ContinuousUseMinutes.ToString();

        // 显示
        SelectComboBoxByTag(PetScaleComboBox, normalized.PetScalePercent);
        SelectComboBoxByTag(SpeechFontSizeComboBox, normalized.SpeechFontSizePt);

        // 启动设置
        EnableAutoStartCheckBox.IsChecked = normalized.EnableAutoStart;
    }

    /// <summary>
    /// 根据 Tag 值选中 ComboBox 对应项。
    /// </summary>
    private static void SelectComboBoxByTag(ComboBox comboBox, double tagValue)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (item.Tag is string tagStr && double.TryParse(tagStr, out var val) &&
                Math.Abs(val - tagValue) < 0.01)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    /// <summary>
    /// 从 ComboBox 选中项的 Tag 中读取数值。
    /// </summary>
    private static double ReadComboBoxTag(ComboBox comboBox, double defaultValue)
    {
        if (comboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr &&
            double.TryParse(tagStr, out var val))
        {
            return val;
        }

        return defaultValue;
    }

    /// <summary>
    /// 读取注册表获取开机自启真实状态。
    /// </summary>
    private void LoadAutoStartState()
    {
        var isEnabled = StartupService.IsEnabled();
        EnableAutoStartCheckBox.IsChecked = isEnabled;

        if (!StartupService.IsPublishedExecutable)
        {
            AutoStartHint.Text = "仅发布版 exe 支持开机自启。当前为开发模式，此选项不可用。";
            AutoStartHint.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 80));
            EnableAutoStartCheckBox.IsEnabled = false;
        }
        else
        {
            AutoStartHint.Text = isEnabled
                ? "已启用开机自启。"
                : "勾选后将在 Windows 登录时自动启动。";
            AutoStartHint.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            EnableAutoStartCheckBox.IsEnabled = true;
        }
    }

    /// <summary>
    /// 切换 API Key 明文显示/隐藏。
    /// </summary>
    private void ShowApiKeyCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var hasEncrypted = !string.IsNullOrWhiteSpace(_original.ApiKeyEncrypted);
        UpdateApiKeyVisibility(ShowApiKeyCheckBox.IsChecked == true, hasEncrypted);
    }

    /// <summary>
    /// 更新 API Key 输入区域的显示状态。
    /// </summary>
    private void UpdateApiKeyVisibility(bool showPlaintext, bool hasEncrypted)
    {
        if (showPlaintext)
        {
            ApiKeyTextBox.Visibility = Visibility.Visible;
            ApiKeyPlaceholder.Visibility = Visibility.Collapsed;
            // 如果此前未手动输入过，尝试从 AppSettings 获取当前解密值
            if (string.IsNullOrWhiteSpace(ApiKeyTextBox.Text) && !string.IsNullOrWhiteSpace(_appSettings.ApiKey))
            {
                ApiKeyTextBox.Text = _appSettings.ApiKey;
            }
        }
        else
        {
            ApiKeyTextBox.Visibility = Visibility.Collapsed;
            ApiKeyPlaceholder.Visibility = hasEncrypted ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 测试当前输入框中的 API 连接。
    /// </summary>
    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isTesting) return;

        var baseUrl = ApiBaseUrlTextBox.Text.Trim();
        var apiKey = GetCurrentApiKeyInput();
        var model = ModelTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            TestResultText.Text = "请先填写 API Base URL。";
            TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 80));
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            TestResultText.Text = "请先填写 API Key。";
            TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 80));
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            TestResultText.Text = "请先填写模型名称。";
            TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 80));
            return;
        }

        _isTesting = true;
        TestConnectionButton.IsEnabled = false;
        TestResultText.Text = "正在测试连接…";
        TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));

        try
        {
            using var tester = new ApiConnectionTester();
            var result = await tester.TestAsync(baseUrl, apiKey, model);

            TestResultText.Text = ApiConnectionTester.GetUserMessage(result);
            TestResultText.Foreground = result == ApiTestResult.Success
                ? new SolidColorBrush(Color.FromRgb(40, 140, 60))
                : new SolidColorBrush(Color.FromRgb(200, 80, 80));
        }
        catch (Exception)
        {
            TestResultText.Text = "测试过程发生意外错误，请稍后重试。";
            TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 80));
        }
        finally
        {
            _isTesting = false;
            TestConnectionButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// 获取当前 API Key 输入（明文）。
    /// 如果用户已输入新 Key 则返回新 Key；
    /// 如果显示占位符（未修改）则从 AppSettings 取当前解密值。
    /// </summary>
    private string? GetCurrentApiKeyInput()
    {
        if (ApiKeyTextBox.Visibility == Visibility.Visible &&
            !string.IsNullOrWhiteSpace(ApiKeyTextBox.Text))
        {
            return ApiKeyTextBox.Text;
        }

        // 如果用户正在查看明文且清空了输入框，视为想清空 Key
        if (ApiKeyTextBox.Visibility == Visibility.Visible)
        {
            return string.Empty;
        }

        // 占位符模式：返回当前运行时 Key（可能来自 .env 或之前的加密存储）
        return !string.IsNullOrWhiteSpace(_appSettings.ApiKey)
            ? _appSettings.ApiKey
            : null;
    }

    /// <summary>
    /// 取消设置修改并关闭窗口。
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 校验并保存设置。
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadPositiveInt(IdleThresholdTextBox.Text, out var idleThresholdSeconds) ||
            !TryReadPositiveInt(ContinuousUseTextBox.Text, out var continuousUseMinutes))
        {
            MessageBox.Show(this, "请填写有效的正整数。", "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var timeoutStr = TimeoutTextBox.Text.Trim();
        int? apiTimeoutSeconds = null;
        if (!string.IsNullOrWhiteSpace(timeoutStr))
        {
            if (int.TryParse(timeoutStr, out var t) && t > 0)
            {
                apiTimeoutSeconds = t;
            }
            else
            {
                MessageBox.Show(this, "请求超时请填写有效的正整数。", "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var plaintextKey = GetCurrentApiKeyInput();
        // 如果用户清空了 Key 输入框，视为要清空 Key
        if (ApiKeyTextBox.Visibility == Visibility.Visible &&
            string.IsNullOrWhiteSpace(ApiKeyTextBox.Text) &&
            string.IsNullOrWhiteSpace(plaintextKey))
        {
            plaintextKey = null;
        }

        SavedSettings = new UserSettings
        {
            ApiBaseUrl = ApiBaseUrlTextBox.Text.Trim(),
            ApiModel = ModelTextBox.Text.Trim(),
            ApiTimeoutSeconds = apiTimeoutSeconds,
            EnableTracking = EnableTrackingCheckBox.IsChecked == true,
            EnableLlm = EnableLlmCheckBox.IsChecked == true,
            EnableMessageReminders = EnableMessageRemindersCheckBox.IsChecked == true,
            SendWindowTitleToLlm = SendWindowTitleToLlmCheckBox.IsChecked == true,
            StoreWindowTitles = StoreWindowTitlesCheckBox.IsChecked == true,
            EnableUsageSummary = EnableUsageSummaryCheckBox.IsChecked == true,
            IdleThresholdSeconds = idleThresholdSeconds,
            ContinuousUseMinutes = continuousUseMinutes,
            EnableAutoStart = EnableAutoStartCheckBox.IsChecked == true,
            // 从 SettingsService 实时读取 ActivePersonaId，避免用 _original 快照覆盖 Tab 切换
            ActivePersonaId = _settingsService.Current.ActivePersonaId,
            // 显示设置
            PetScalePercent = ReadComboBoxTag(PetScaleComboBox, 150.0),
            SpeechFontSizePt = ReadComboBoxTag(SpeechFontSizeComboBox, 16.0)
        }.CloneNormalized();

        PlaintextApiKey = plaintextKey;

        // 处理开机自启注册表
        ApplyAutoStartSetting();

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 根据复选框状态启用或禁用开机自启。
    /// </summary>
    private void ApplyAutoStartSetting()
    {
        if (!StartupService.IsPublishedExecutable)
        {
            return;
        }

        if (EnableAutoStartCheckBox.IsChecked == true)
        {
            StartupService.Enable();
        }
        else
        {
            StartupService.Disable();
        }
    }

    /// <summary>
    /// 读取正整数输入。
    /// </summary>
    private static bool TryReadPositiveInt(string value, out int result)
    {
        return int.TryParse(value.Trim(), out result) && result > 0;
    }
}
