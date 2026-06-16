using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MutsuPet.Models;

namespace MutsuPet.Services;

public sealed class LlmClient : IDisposable
{
    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 初始化 OpenAI-compatible LLM 客户端。
    /// </summary>
    public LlmClient(AppSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    /// <summary>
    /// 获取当前客户端是否具备发送 LLM 请求的配置。
    /// </summary>
    public bool IsEnabled => _settings.IsLlmEnabled;

    /// <summary>
    /// 请求 LLM 根据电脑使用状态生成一句桌宠台词。
    /// </summary>
    public async Task<string?> GenerateLineAsync(
        UsageSnapshot snapshot,
        InteractionTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildCompletionsEndpoint());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = JsonContent.Create(CreateRequestBody(snapshot, trigger));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return ExtractAssistantText(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 释放内部 HTTP 客户端。
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// 拼接 OpenAI-compatible chat completions 接口地址。
    /// </summary>
    private Uri BuildCompletionsEndpoint()
    {
        var normalizedBaseUrl = _settings.BaseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(normalizedBaseUrl), "chat/completions");
    }

    /// <summary>
    /// 构造发送给 LLM 的聊天补全请求体。
    /// </summary>
    private object CreateRequestBody(UsageSnapshot snapshot, InteractionTrigger trigger)
    {
        return new
        {
            model = _settings.Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "你是名叫 Mutsu 的 Windows 桌面宠物。你会根据用户当前电脑使用状态，用中文给出一句轻量、温柔、不打扰的互动短句。不要输出推理过程、Markdown、编号或解释。"
                },
                new
                {
                    role = "user",
                    content = BuildUserPrompt(snapshot, trigger)
                }
            },
            temperature = 0.8,
            max_tokens = 90,
            stream = false
        };
    }

    /// <summary>
    /// 将 Win API 采集到的详细状态整理为提示词。
    /// </summary>
    private static string BuildUserPrompt(UsageSnapshot snapshot, InteractionTrigger trigger)
    {
        return string.Join(
            Environment.NewLine,
            "请基于以下 Windows 使用上下文，输出一句适合放进桌宠气泡的中文短句。",
            "要求：20 到 42 个中文字符；自然陪伴；可以提到当前应用或状态；不要显得监控感太强；不要包含换行。",
            $"当前时间：{snapshot.CapturedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"触发类型：{trigger}",
            $"最近事件：{snapshot.RecentEvent}",
            $"前台进程：{snapshot.ProcessName}",
            $"窗口标题：{snapshot.WindowTitle}",
            $"空闲秒数：{snapshot.IdleSeconds}",
            $"当前窗口连续使用分钟：{Math.Round(snapshot.ActiveWindowDuration.TotalMinutes, 1)}",
            $"会话锁定：{snapshot.IsSessionLocked}");
    }

    /// <summary>
    /// 从 OpenAI-compatible 响应中提取 assistant 文本。
    /// </summary>
    private static string? ExtractAssistantText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message))
        {
            return null;
        }

        return message.ValueKind == JsonValueKind.Object &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : null;
    }
}
