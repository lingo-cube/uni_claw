using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;

namespace UniClaw.DeepSeekProvider;

/// <summary>
/// DeepSeekModelProvider — IModelProvider 实现, 使用 DeepSeek chat/completions HTTP API。
/// 纯传输层: 构造请求体 → POST → 解析响应 → ModelResponse。
/// 失败 graceful (返回 Success=false), 不抛 (除用户 CancellationToken 取消)。
/// </summary>
public sealed class DeepSeekModelProvider : IModelProvider
{
    private readonly HttpClient _http;
    private readonly DeepSeekProviderConfig _config;

    /// <summary>
    /// 构造 DeepSeekModelProvider。
    /// </summary>
    /// <param name="http">HttpClient (由调用方管理生命周期, e.g. IHttpClientFactory)</param>
    /// <param name="config">传输层配置</param>
    public DeepSeekModelProvider(HttpClient http, DeepSeekProviderConfig config)
    {
        _http = http ?? throw new DomainValidationException(nameof(http), null);
        _config = config ?? throw new DomainValidationException(nameof(config), null);
    }

    /// <inheritdoc />
    public string ProviderId => "deepseek";

    /// <inheritdoc />
    public async Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
    {
        if (request is null)
            throw new DomainValidationException(nameof(request), null);

        // ── 构造 OpenAI-compatible chat/completions 请求体 ──
        var body = new JsonObject
        {
            ["model"] = _config.Model,
            ["max_tokens"] = request.MaxTokens,
        };

        var messages = new JsonArray();
        if (request.SystemPrompt is not null)
        {
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });
        }
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.Prompt });
        body["messages"] = messages;

        if (request.Schema is not null)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }

        // ── 构造 HttpRequestMessage ──
        using var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/chat/completions");
        reqMsg.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_config.ApiKey}");
        reqMsg.Content = new StringContent(body.ToJsonString(), Encoding.UTF8);
        reqMsg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // ── 链接用户 ct + 超时取消 ──
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_config.RequestTimeoutSeconds));

        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await _http.SendAsync(reqMsg, cts.Token).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return GracefulError($"DeepSeek HTTP {(int)resp.StatusCode} {resp.StatusCode}: {errBody}");
            }

            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonNode.Parse(raw);
            if (doc is null)
                return GracefulError("DeepSeek returned unparseable (null) JSON body.");

            var content = doc["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
            var inputTok = (int?)doc["usage"]?["prompt_tokens"] ?? 0;
            var outputTok = (int?)doc["usage"]?["completion_tokens"] ?? 0;
            sw.Stop();
            return new ModelResponse(content, "deepseek", "text", inputTok, outputTok, sw.Elapsed.TotalMilliseconds, _config.Model);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            // 用户主动取消 → 重抛 (不是 graceful 错误)
            if (ct.IsCancellationRequested)
                throw;

            return GracefulError($"DeepSeek transport error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek vision/multimodal not implemented in vertical slice.");

    /// <inheritdoc />
    public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException("DeepSeek vision/multimodal not implemented in vertical slice.");

    private ModelResponse GracefulError(string message)
        => new ModelResponse("", "deepseek", "text", 0, 0, 0) with { Success = false, ErrorMessage = message };
}
