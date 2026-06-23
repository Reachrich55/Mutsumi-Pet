namespace MutsumiPet.Services;

public sealed class UserSettings
{
    /// <summary>
    /// 获取或设置是否记录本地使用时间线。
    /// </summary>
    public bool EnableTracking { get; set; } = true;

    /// <summary>
    /// 获取或设置是否允许调用 LLM。
    /// </summary>
    public bool EnableLlm { get; set; } = true;

    /// <summary>
    /// 获取或设置是否允许 QQ/微信消息提醒。
    /// </summary>
    public bool EnableMessageReminders { get; set; } = true;

    /// <summary>
    /// 获取或设置是否允许把窗口标题发送给 LLM。
    /// </summary>
    public bool SendWindowTitleToLlm { get; set; } = true;

    /// <summary>
    /// 获取或设置是否把窗口标题保存到本地数据库。
    /// </summary>
    public bool StoreWindowTitles { get; set; }

    /// <summary>
    /// 获取或设置是否启用使用摘要。
    /// </summary>
    public bool EnableUsageSummary { get; set; } = true;

    /// <summary>
    /// 获取或设置是否启用输入计数；当前版本保留开关但不采集键鼠内容。
    /// </summary>
    public bool EnableInputCounter { get; set; }

    /// <summary>
    /// 获取或设置进入空闲状态的秒数阈值。
    /// </summary>
    public int IdleThresholdSeconds { get; set; } = 180;

    /// <summary>
    /// 获取或设置连续使用提醒的分钟阈值。
    /// </summary>
    public int ContinuousUseMinutes { get; set; } = 45;

    /// <summary>
    /// 复制并规范化设置值，避免非法配置影响运行。
    /// </summary>
    public UserSettings CloneNormalized()
    {
        return new UserSettings
        {
            EnableTracking = EnableTracking,
            EnableLlm = EnableLlm,
            EnableMessageReminders = EnableMessageReminders,
            SendWindowTitleToLlm = SendWindowTitleToLlm,
            StoreWindowTitles = StoreWindowTitles,
            EnableUsageSummary = EnableUsageSummary,
            EnableInputCounter = EnableInputCounter,
            IdleThresholdSeconds = Clamp(IdleThresholdSeconds, 30, 3600, 180),
            ContinuousUseMinutes = Clamp(ContinuousUseMinutes, 5, 240, 45)
        };
    }

    /// <summary>
    /// 将整数限制在允许范围内，非法值回落到默认值。
    /// </summary>
    private static int Clamp(int value, int min, int max, int defaultValue)
    {
        return value < min || value > max ? defaultValue : value;
    }
}
