using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;

namespace UniClaw.ClaudeProvider;

/// <summary>
/// AnthropicModelProvider — IModelProvider 实现，使用 Anthropic Claude Messages API。
/// 纯传输层: 构造请求体 → POST /v1/messages → 解析响应 → ModelResponse。
/// 支持 text + vision (base64 图片) 两种模式。
/// 失败 graceful (返回 Success=false), 不抛 (除用户 CancellationToken 取消)。
/// </summary>
public sealed class AnthropicModelProvider : IModelProvider
{
    private readonly HttpClient _http;
    private readonly AnthropicProviderConfig _config;

    /// <summary>
    /// 构造 AnthropicModelProvider。
    /// </summary>
    /// <param name="http">HttpClient (由调用方管理生命周期)</param>
    /// <param name="config">传输层配置</param>
    public AnthropicModelProvider(HttpClient http, AnthropicProviderConfig config)
    {
        _http = http ?? throw new DomainValidationException(nameof(http), null);
        _config = config ?? throw new DomainValidationException(nameof(config), null);
    }

    /// <inheritdoc />
    public string ProviderId => "claude";

    /// <inheritdoc />
    public async Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
    {
        if (request is null)
            throw new DomainValidationException(nameof(request), null);

        var body = BuildMessagesBody(request, imageData: null);
        return await SendAsync(body, request.Schema is not null, ct);
    }

    /// <inheritdoc />
    public async Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
    {
        if (request is null)
            throw new DomainValidationException(nameof(request), null);
        if (imageData is null || imageData.Length == 0)
            throw new DomainValidationException(nameof(imageData), null);

        var body = BuildMessagesBody(request, imageData);
        return await SendAsync(body, request.Schema is not null, ct);
    }

    /// <inheritdoc />
    public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => CompleteVisionAsync(request, imageData, ct);

    // ── 内部 ────────────────────────────────────────────────────────

    /// <summary>构造 Claude Messages API 请求体</summary>
    private JsonObject BuildMessagesBody(ModelRequest request, byte[]? imageData)
    {
        var body = new JsonObject
        {
            ["model"] = _config.Model,
            ["max_tokens"] = request.MaxTokens,
        };

        // system prompt（Claude 专用顶级参数，不在 messages 数组内）
        if (request.SystemPrompt is not null)
        {
            body["system"] = request.SystemPrompt;
        }

        // messages 数组
        var messages = new JsonArray();

        if (imageData is not null)
        {
            // 视觉模式: content 为 content block 数组
            var content = new JsonArray();

            // 文本部分
            content.Add(new JsonObject
            {
                ["type"] = "text",
                ["text"] = request.Prompt,
            });

            // 图片部分 (base64)
            var base64 = Convert.ToBase64String(imageData);
            var mediaType = DetectImageMimeType(imageData);
            content.Add(new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mediaType,
                    ["data"] = base64,
                },
            });

            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = content,
            });
        }
        else
        {
            // 纯文本模式
            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = request.Prompt,
            });
        }

        body["messages"] = messages;
        return body;
    }

    /// <summary>发送请求到 Claude Messages API 并解析响应</summary>
    private async Task<ModelResponse> SendAsync(JsonObject body, bool useJsonMode, CancellationToken ct)
    {
        using var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/v1/messages");
        reqMsg.Headers.TryAddWithoutValidation("x-api-key", _config.ApiKey);
        reqMsg.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        // JSON 模式: Claude 通过 beta header 启用
        if (useJsonMode)
        {
            reqMsg.Headers.TryAddWithoutValidation("anthropic-beta", "response-json-2025-04-15");
        }

        reqMsg.Content = new StringContent(body.ToJsonString(), Encoding.UTF8);
        reqMsg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_config.RequestTimeoutSeconds));

        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await _http.SendAsync(reqMsg, cts.Token).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return GracefulError($"Claude HTTP {(int)resp.StatusCode}: {errBody}");
            }

            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonNode.Parse(raw);
            if (doc is null)
                return GracefulError("Claude returned unparseable (null) JSON body.");

            // 解析响应: content[0].text + usage
            var content = doc["content"]?[0]?["text"]?.ToString() ?? "";
            var inputTok = (int?)doc["usage"]?["input_tokens"] ?? 0;
            var outputTok = (int?)doc["usage"]?["output_tokens"] ?? 0;
            sw.Stop();

            return new ModelResponse(
                content, "claude", "text", inputTok, outputTok,
                sw.Elapsed.TotalMilliseconds, _config.Model);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
                throw;

            return GracefulError($"Claude transport error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>根据文件头检测图片 MIME type</summary>
    private static string DetectImageMimeType(byte[] data)
    {
        if (data.Length < 4) return "image/png";
        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        // WEBP: 52 49 46 46 ... 57 45 42 50
        if (data.Length > 12 && data[0] == 0x52 && data[1] == 0x49 && data[8] == 0x57)
            return "image/webp";
        return "image/png";
    }

    private ModelResponse GracefulError(string message)
        => new ModelResponse("", "claude", "text", 0, 0, 0) with
        {
            Success = false,
            ErrorMessage = message,
        };
}
