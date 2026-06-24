using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MutsumiPet.Services;

/// <summary>
/// 表示 API 连接测试的结果。
/// </summary>
public enum ApiTestResult
{
    /// <summary>连接成功，返回格式兼容。</summary>
    Success,
    /// <summary>API Key 无效或无权限（HTTP 401/403）。</summary>
    InvalidKey,
    /// <summary>API 地址无法访问（DNS 解析失败或连接被拒）。</summary>
    Unreachable,
    /// <summary>模型不存在或无权访问（HTTP 404）。</summary>
    ModelNotFound,
    /// <summary>请求超时。</summary>
    Timeout,
    /// <summary>返回内容格式不兼容（无法解析为 OpenAI 格式）。</summary>
    FormatIncompatible,
    /// <summary>发生了未知错误。</summary>
    UnknownError
}

/// <summary>
/// 使用临时 HTTP 客户端测试 LLM API 连接。
/// 不会修改任何已保存的配置，只返回测试结论。
/// </summary>
public sealed class ApiConnectionTester : IDisposable
{
    private readonly HttpClient _httpClient;

    public ApiConnectionTester()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    /// <summary>
    /// 使用给定的参数发送最小测试请求，返回分类测试结果。
    /// </summary>
    public async Task<ApiTestResult> TestAsync(
        string baseUrl,
        string apiKey,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return ApiTestResult.Unreachable;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ApiTestResult.InvalidKey;
        }

        var completionsUrl = BuildCompletionsUrl(baseUrl);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, completionsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = "ping" }
                },
                max_tokens = 1,
                temperature = 0,
                stream = false
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await ValidateResponseFormatAsync(response, cancellationToken);
            }

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => ApiTestResult.InvalidKey,
                System.Net.HttpStatusCode.Forbidden => ApiTestResult.InvalidKey,
                System.Net.HttpStatusCode.NotFound => ApiTestResult.ModelNotFound,
                _ => ApiTestResult.UnknownError
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiTestResult.Timeout;
        }
        catch (HttpRequestException)
        {
            return ApiTestResult.Unreachable;
        }
        catch (Exception)
        {
            return ApiTestResult.UnknownError;
        }
    }

    /// <summary>
    /// 获取测试结果对应的用户可读中文说明。
    /// </summary>
    public static string GetUserMessage(ApiTestResult result)
    {
        return result switch
        {
            ApiTestResult.Success => "连接成功！API 响应格式兼容。",
            ApiTestResult.InvalidKey => "API Key 无效或无权限，请检查后重试。",
            ApiTestResult.Unreachable => "无法访问 API 地址，请检查地址是否正确、网络是否通畅。",
            ApiTestResult.ModelNotFound => "模型不存在或无权访问，请检查模型名称。",
            ApiTestResult.Timeout => "请求超时，请检查网络或增大超时时间后重试。",
            ApiTestResult.FormatIncompatible => "API 返回了内容，但格式与 OpenAI 接口不兼容。",
            _ => "发生了未知错误，请检查配置后重试。"
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// 验证响应是否可以按 OpenAI chat-completions 格式解析。
    /// </summary>
    private static async Task<ApiTestResult> ValidateResponseFormatAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = System.Text.Json.JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != System.Text.Json.JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return ApiTestResult.FormatIncompatible;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) ||
                message.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return ApiTestResult.FormatIncompatible;
            }

            return message.TryGetProperty("content", out _)
                ? ApiTestResult.Success
                : ApiTestResult.FormatIncompatible;
        }
        catch (System.Text.Json.JsonException)
        {
            return ApiTestResult.FormatIncompatible;
        }
    }

    /// <summary>
    /// 构建 chat/completions 端点 URL。
    /// </summary>
    private static Uri BuildCompletionsUrl(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(normalized), "chat/completions");
    }
}
