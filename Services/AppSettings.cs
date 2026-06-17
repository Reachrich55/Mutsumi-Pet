namespace MutsuPet.Services;

public sealed class AppSettings
{
    private const string DefaultBaseUrl = "https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1";
    private const string DefaultModel = "qwen3.7-plus";
    private const int DefaultTimeoutSeconds = 60;

    /// <summary>
    /// 初始化应用使用的 LLM 配置。
    /// </summary>
    public AppSettings(string apiKey, string baseUrl, string model, int timeoutSeconds)
    {
        ApiKey = apiKey;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;
        Model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        TimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds;
    }

    /// <summary>
    /// 获取 LLM API Key。
    /// </summary>
    public string ApiKey { get; }

    /// <summary>
    /// 获取 OpenAI-compatible API 基础地址。
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// 获取 LLM 模型名称。
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// 获取 LLM 请求超时时间秒数。
    /// </summary>
    public int TimeoutSeconds { get; }

    /// <summary>
    /// 获取当前配置是否足以启用 LLM 请求。
    /// </summary>
    public bool IsLlmEnabled => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>
    /// 从 .env 文件和环境变量读取应用配置。
    /// </summary>
    public static AppSettings Load()
    {
        var values = DotEnv.LoadNearest();
        var apiKey = ReadSetting(values, "LLM_API_KEY", string.Empty);
        var baseUrl = ReadSetting(values, "LLM_BASE_URL", DefaultBaseUrl);
        var model = ReadSetting(values, "LLM_MODEL", DefaultModel);
        var timeoutSeconds = ReadIntSetting(values, "LLM_TIMEOUT_SECONDS", DefaultTimeoutSeconds);
        return new AppSettings(apiKey, baseUrl, model, timeoutSeconds);
    }

    /// <summary>
    /// 按环境变量优先、.env 次之、默认值最后的顺序读取配置项。
    /// </summary>
    private static string ReadSetting(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        var environmentValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        return values.TryGetValue(key, out var fileValue) && !string.IsNullOrWhiteSpace(fileValue)
            ? fileValue
            : defaultValue;
    }

    /// <summary>
    /// 按环境变量优先、.env 次之、默认值最后的顺序读取整数配置项。
    /// </summary>
    private static int ReadIntSetting(IReadOnlyDictionary<string, string> values, string key, int defaultValue)
    {
        var rawValue = ReadSetting(values, key, defaultValue.ToString());
        return int.TryParse(rawValue, out var parsedValue) && parsedValue > 0
            ? parsedValue
            : defaultValue;
    }
}
