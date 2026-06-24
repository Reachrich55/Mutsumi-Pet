using System.IO;

namespace MutsumiPet.Services;

/// <summary>
/// 运行时 LLM 配置，支持从多个来源加载，并在运行中更新。
/// 配置优先级：设置窗口保存的 settings.json → 环境变量 → .env → 代码默认值。
/// </summary>
public sealed class AppSettings
{
    private const string DefaultBaseUrl = "https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1";
    private const string DefaultModel = "qwen3.7-plus";
    private const int DefaultTimeoutSeconds = 60;

    /// <summary>
    /// LLM API Key（明文）。
    /// </summary>
    public string ApiKey { get; private set; } = string.Empty;

    /// <summary>
    /// OpenAI-compatible API 基础地址。
    /// </summary>
    public string BaseUrl { get; private set; } = DefaultBaseUrl;

    /// <summary>
    /// LLM 模型名称。
    /// </summary>
    public string Model { get; private set; } = DefaultModel;

    /// <summary>
    /// LLM 请求超时秒数。
    /// </summary>
    public int TimeoutSeconds { get; private set; } = DefaultTimeoutSeconds;

    /// <summary>
    /// 当前配置是否足以发起 LLM 请求。
    /// </summary>
    public bool IsLlmEnabled => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>
    /// 使用仅含默认值的配置。（供测试或离线使用）
    /// </summary>
    public AppSettings()
    {
    }

    /// <summary>
    /// 用指定参数创建配置。
    /// </summary>
    public AppSettings(string apiKey, string baseUrl, string model, int timeoutSeconds)
    {
        ApiKey = apiKey;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;
        Model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        TimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds;
    }

    /// <summary>
    /// 从多层来源加载配置。
    /// 优先级：settings.json（含加密 Key）→ 环境变量 → .env → 默认值。
    /// </summary>
    public static AppSettings Load()
    {
        var settings = new AppSettings();
        settings.Reload();
        return settings;
    }

    /// <summary>
    /// 重新从文件和环境变量加载所有配置（API Key 除外，不会被覆盖为空）。
    /// </summary>
    public void Reload()
    {
        var dotEnvValues = DotEnv.LoadNearest();
        UserSettings? fileSettings = null;

        // 1. 尝试加载 settings.json 中的配置
        try
        {
            var settingsJsonPath = SettingsService.SettingsPath;
            if (File.Exists(settingsJsonPath))
            {
                var json = File.ReadAllText(settingsJsonPath);
                fileSettings = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch (Exception)
        {
            fileSettings = null;
        }

        // 2. 按优先级解析各项配置

        // API Key：settings.json(解密) → 环境变量 → .env → 保留当前值(不清空)
        var newApiKey = ResolveApiKey(fileSettings, dotEnvValues);
        if (!string.IsNullOrWhiteSpace(newApiKey))
        {
            ApiKey = newApiKey;
        }

        // Base URL：settings.json → 环境变量 → .env → 默认值
        BaseUrl = ResolveValue(
            fileSettings?.ApiBaseUrl,
            "LLM_BASE_URL",
            dotEnvValues,
            DefaultBaseUrl);

        // Model：settings.json → 环境变量 → .env → 默认值
        Model = ResolveValue(
            fileSettings?.ApiModel,
            "LLM_MODEL",
            dotEnvValues,
            DefaultModel);

        // Timeout：settings.json → 环境变量 → .env → 默认值
        var timeoutStr = ResolveValue(
            fileSettings?.ApiTimeoutSeconds?.ToString(),
            "LLM_TIMEOUT_SECONDS",
            dotEnvValues,
            DefaultTimeoutSeconds.ToString());
        TimeoutSeconds = int.TryParse(timeoutStr, out var parsed) && parsed > 0
            ? parsed
            : DefaultTimeoutSeconds;
    }

    /// <summary>
    /// 从 settings.json(加密存储) 或环境变量/.env 获取 API Key。
    /// settings.json 优先级最高。
    /// </summary>
    private static string? ResolveApiKey(UserSettings? fileSettings, IReadOnlyDictionary<string, string> dotEnvValues)
    {
        // 1. settings.json 中的加密 Key（最高优先级）
        if (fileSettings is not null && !string.IsNullOrWhiteSpace(fileSettings.ApiKeyEncrypted))
        {
            var decrypted = ApiKeyProtection.Unprotect(fileSettings.ApiKeyEncrypted);
            if (!string.IsNullOrWhiteSpace(decrypted))
            {
                return decrypted;
            }
        }

        // 2. 环境变量
        var envValue = Environment.GetEnvironmentVariable("LLM_API_KEY");
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        // 3. .env 文件
        if (dotEnvValues.TryGetValue("LLM_API_KEY", out var dotEnvValue) &&
            !string.IsNullOrWhiteSpace(dotEnvValue))
        {
            return dotEnvValue;
        }

        // 4. 无可用 Key
        return null;
    }

    /// <summary>
    /// 按优先级解决单个字符串配置值。
    /// </summary>
    private static string ResolveValue(
        string? fileValue,
        string envKey,
        IReadOnlyDictionary<string, string> dotEnvValues,
        string defaultValue)
    {
        // 1. settings.json 值
        if (!string.IsNullOrWhiteSpace(fileValue))
        {
            return fileValue;
        }

        // 2. 环境变量
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        // 3. .env 文件
        if (dotEnvValues.TryGetValue(envKey, out var dotEnvValue) &&
            !string.IsNullOrWhiteSpace(dotEnvValue))
        {
            return dotEnvValue;
        }

        // 4. 默认值
        return defaultValue;
    }
}
