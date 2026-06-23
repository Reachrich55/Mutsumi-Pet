using System.Windows;
using MutsumiPet.Services;

namespace MutsumiPet;

public partial class SettingsWindow : Window
{
    /// <summary>
    /// 初始化设置窗口并填充当前设置。
    /// </summary>
    public SettingsWindow(UserSettings settings)
    {
        InitializeComponent();
        LoadSettings(settings);
    }

    /// <summary>
    /// 获取保存后的设置。
    /// </summary>
    public UserSettings? SavedSettings { get; private set; }

    /// <summary>
    /// 将设置值写入窗口控件。
    /// </summary>
    private void LoadSettings(UserSettings settings)
    {
        var normalized = settings.CloneNormalized();
        EnableTrackingCheckBox.IsChecked = normalized.EnableTracking;
        EnableLlmCheckBox.IsChecked = normalized.EnableLlm;
        EnableMessageRemindersCheckBox.IsChecked = normalized.EnableMessageReminders;
        SendWindowTitleToLlmCheckBox.IsChecked = normalized.SendWindowTitleToLlm;
        StoreWindowTitlesCheckBox.IsChecked = normalized.StoreWindowTitles;
        EnableUsageSummaryCheckBox.IsChecked = normalized.EnableUsageSummary;
        IdleThresholdTextBox.Text = normalized.IdleThresholdSeconds.ToString();
        ContinuousUseTextBox.Text = normalized.ContinuousUseMinutes.ToString();
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

        SavedSettings = new UserSettings
        {
            EnableTracking = EnableTrackingCheckBox.IsChecked == true,
            EnableLlm = EnableLlmCheckBox.IsChecked == true,
            EnableMessageReminders = EnableMessageRemindersCheckBox.IsChecked == true,
            SendWindowTitleToLlm = SendWindowTitleToLlmCheckBox.IsChecked == true,
            StoreWindowTitles = StoreWindowTitlesCheckBox.IsChecked == true,
            EnableUsageSummary = EnableUsageSummaryCheckBox.IsChecked == true,
            IdleThresholdSeconds = idleThresholdSeconds,
            ContinuousUseMinutes = continuousUseMinutes
        }.CloneNormalized();

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 读取正整数输入。
    /// </summary>
    private static bool TryReadPositiveInt(string value, out int result)
    {
        return int.TryParse(value.Trim(), out result) && result > 0;
    }
}
