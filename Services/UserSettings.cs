namespace MutsumiPet.Services;

/// <summary>
/// 用户可通过设置窗口修改的持久化配置。
/// API Key 以 DPAPI 加密存储，不会以明文写入磁盘。
/// </summary>
public sealed class UserSettings
{
    // ────────────── 模型服务 ──────────────

    /// <summary>
    /// OpenAI-compatible API 基础地址。
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// LLM 模型名称。
    /// </summary>
    public string? ApiModel { get; set; }

    /// <summary>
    /// LLM 请求超时秒数。
    /// </summary>
    public int? ApiTimeoutSeconds { get; set; }

    /// <summary>
    /// DPAPI 加密后的 API Key（Base64 编码）。
    /// 写入磁盘时设置，读取后由 SettingsService 解密填充到 AppSettings。
    /// </summary>
    public string? ApiKeyEncrypted { get; set; }

    // ────────────── 隐私与监控 ──────────────

    /// <summary>
    /// 是否记录本地使用时间线。
    /// </summary>
    public bool EnableTracking { get; set; } = true;

    /// <summary>
    /// 是否允许调用 LLM。
    /// </summary>
    public bool EnableLlm { get; set; } = true;

    /// <summary>
    /// 是否允许 QQ/微信消息提醒。
    /// </summary>
    public bool EnableMessageReminders { get; set; } = true;

    /// <summary>
    /// 是否允许把窗口标题发送给 LLM。
    /// </summary>
    public bool SendWindowTitleToLlm { get; set; } = true;

    /// <summary>
    /// 是否把窗口标题保存到本地数据库。
    /// </summary>
    public bool StoreWindowTitles { get; set; }

    /// <summary>
    /// 是否启用使用摘要。
    /// </summary>
    public bool EnableUsageSummary { get; set; } = true;

    /// <summary>
    /// 是否启用输入计数（当前版本保留开关但不采集键鼠内容）。
    /// </summary>
    public bool EnableInputCounter { get; set; }

    // ────────────── 显示 ──────────────

    /// <summary>
    /// 桌宠缩放百分比。合法值：50, 75, 100, 125, 150, 175。默认 150。
    /// </summary>
    public double PetScalePercent { get; set; } = 150.0;

    /// <summary>
    /// 气泡文字字号（pt）。合法值：9, 10.5, 12, 14, 16, 18, 20, 24, 28, 32。默认 16。
    /// </summary>
    public double SpeechFontSizePt { get; set; } = 16.0;

    // ────────────── 人格 ──────────────

    /// <summary>
    /// 当前激活的人格 ID，默认 "mutsumi"。
    /// </summary>
    public string ActivePersonaId { get; set; } = "mutsumi";

    // ────────────── 交互行为 ──────────────

    /// <summary>
    /// 进入空闲状态的秒数阈值（默认 180）。
    /// </summary>
    public int IdleThresholdSeconds { get; set; } = 180;

    /// <summary>
    /// 连续使用提醒的分钟阈值（默认 45）。
    /// </summary>
    public int ContinuousUseMinutes { get; set; } = 45;

    // ────────────── 启动设置 ──────────────

    /// <summary>
    /// 是否在 Windows 登录后自动启动。
    /// </summary>
    public bool EnableAutoStart { get; set; }

    // ────────────── 方法 ──────────────

    /// <summary>
    /// 复制并规范化设置值，避免非法配置影响运行。
    /// </summary>
    public UserSettings CloneNormalized()
    {
        return new UserSettings
        {
            ApiBaseUrl = ApiBaseUrl,
            ApiModel = ApiModel,
            ApiTimeoutSeconds = ApiTimeoutSeconds,
            ApiKeyEncrypted = ApiKeyEncrypted,
            EnableTracking = EnableTracking,
            EnableLlm = EnableLlm,
            EnableMessageReminders = EnableMessageReminders,
            SendWindowTitleToLlm = SendWindowTitleToLlm,
            StoreWindowTitles = StoreWindowTitles,
            EnableUsageSummary = EnableUsageSummary,
            EnableInputCounter = EnableInputCounter,
            EnableAutoStart = EnableAutoStart,
            ActivePersonaId = string.IsNullOrWhiteSpace(ActivePersonaId) ? "mutsumi" : ActivePersonaId,
            PetScalePercent = ClampToValidOption(PetScalePercent, [50.0, 75.0, 100.0, 125.0, 150.0, 175.0], 150.0),
            SpeechFontSizePt = ClampToValidOption(SpeechFontSizePt, [9.0, 10.5, 12.0, 14.0, 16.0, 18.0, 20.0, 24.0, 28.0, 32.0], 16.0),
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

    /// <summary>
    /// 将浮点数限制在合法选项列表中，无匹配时返回默认值。
    /// </summary>
    private static double ClampToValidOption(double value, double[] options, double defaultValue)
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
}
