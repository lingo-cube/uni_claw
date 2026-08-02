using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;

namespace UniClaw.ClaudeProvider;

public sealed record class OpenAiCompatibleProviderConfig
{
    public string ApiKey { get; init; }
    public string Model { get; init; }
    public string BaseUrl { get; init; }
    public double RequestTimeoutSeconds { get; init; }
    /// <summary>Model temperature (default 0.2 — calibrated for Sensenova vision accuracy).</summary>
    public double Temperature { get; init; } = 0.2;
    /// <summary>Top-p sampling (default 0.30).</summary>
    public double TopP { get; init; } = 0.30;

    public OpenAiCompatibleProviderConfig(
        string ApiKey,
        string Model,
        string BaseUrl,
        double RequestTimeoutSeconds = 300.0,
        double Temperature = 0.2,
        double TopP = 0.30)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new DomainValidationException(nameof(ApiKey), ApiKey);
        if (string.IsNullOrWhiteSpace(Model))
            throw new DomainValidationException(nameof(Model), Model);
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new DomainValidationException(nameof(BaseUrl), BaseUrl);
        if (RequestTimeoutSeconds <= 0)
            throw new DomainValidationException(
                nameof(RequestTimeoutSeconds),
                RequestTimeoutSeconds);

        this.ApiKey = ApiKey;
        this.Model = Model;
        this.BaseUrl = BaseUrl.TrimEnd('/');
        this.RequestTimeoutSeconds = RequestTimeoutSeconds;
        this.Temperature = Temperature;
        this.TopP = TopP;
    }
}

/// <summary>
/// OpenAI-compatible vision transport used by the Sensenova (日日新) endpoint.
/// </summary>
public sealed class OpenAiCompatibleVisionProvider : IModelProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiCompatibleProviderConfig _config;

    public OpenAiCompatibleVisionProvider(
        HttpClient http,
        OpenAiCompatibleProviderConfig config)
    {
        _http = http ?? throw new DomainValidationException(nameof(http), null);
        _config = config
                  ?? throw new DomainValidationException(nameof(config), null);
    }

    public string ProviderId => "sensenova";

    public Task<ModelResponse> CompleteTextAsync(
        ModelRequest request,
        CancellationToken ct = default) =>
        SendAsync(request, null, ct);

    public Task<ModelResponse> CompleteVisionAsync(
        ModelRequest request,
        byte[] imageData,
        CancellationToken ct = default)
    {
        if (imageData is null || imageData.Length == 0)
            throw new DomainValidationException(nameof(imageData), null);
        return SendAsync(request, imageData, ct);
    }

    public Task<ModelResponse> CompleteMultimodalAsync(
        ModelRequest request,
        byte[] imageData,
        CancellationToken ct = default) =>
        CompleteVisionAsync(request, imageData, ct);

    private async Task<ModelResponse> SendAsync(
        ModelRequest request,
        byte[]? imageData,
        CancellationToken ct,
        int attempt = 0,
        bool useJsonMode = true)
    {
        if (request is null)
            throw new DomainValidationException(nameof(request), null);

        var body = new JsonObject
        {
            ["model"] = _config.Model,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = _config.Temperature,
            ["top_p"] = _config.TopP,
            ["stream"] = false,
            ["messages"] = BuildMessages(request, imageData),
        };
        if (request.Schema is not null && useJsonMode)
        {
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_object",
            };
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_config.BaseUrl}/v1/chat/completions");
        message.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        message.Content = new StringContent(
            body.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_config.RequestTimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();
        var headersStopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token)
                .ConfigureAwait(false);
            headersStopwatch.Stop();
            var bodyStopwatch = Stopwatch.StartNew();
            var raw = await response.Content.ReadAsStringAsync(timeout.Token)
                .ConfigureAwait(false);
            bodyStopwatch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                return Failure(
                    $"Sensenova HTTP {(int)response.StatusCode}: {raw}");
            }

            var document = JsonNode.Parse(raw);
            var content = document?["choices"]?[0]?["message"]?["content"]
                ?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content) && attempt < 1)
            {
                return await SendAsync(
                        request,
                        imageData,
                        ct,
                        attempt + 1,
                        useJsonMode: false)
                    .ConfigureAwait(false);
            }
            if (string.IsNullOrWhiteSpace(content))
                return Failure("Sensenova returned an empty model response.");
            var usage = document?["usage"];
            var inputTokens = (int?)(usage?["prompt_tokens"]) ?? 0;
            var outputTokens = (int?)(usage?["completion_tokens"]) ?? 0;
            stopwatch.Stop();
            return new ModelResponse(
                content,
                ProviderId,
                imageData is null ? "text" : "vision",
                inputTokens,
                outputTokens,
                stopwatch.Elapsed.TotalMilliseconds,
                _config.Model)
            {
                Diagnostics = BuildDiagnostics(
                    attempt,
                    useJsonMode,
                    imageData?.Length ?? 0,
                    headersStopwatch.Elapsed.TotalMilliseconds,
                    bodyStopwatch.Elapsed.TotalMilliseconds),
            };
        }
        catch (Exception ex) when (
            ex is HttpRequestException
            or JsonException
            or OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
                throw;
            return Failure(
                $"Sensenova transport error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static JsonArray BuildMessages(
        ModelRequest request,
        byte[]? imageData)
    {
        var content = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = request.Prompt,
            },
        };
        if (imageData is not null)
        {
            content.Add(new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject
                {
                    ["url"] =
                        $"data:{DetectMime(imageData)};base64,{Convert.ToBase64String(imageData)}",
                },
            });
        }

        return new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = request.SystemPrompt ?? string.Empty,
            },
            new JsonObject
            {
                ["role"] = "user",
                // JsonNode single-parent rule: content[0] is already a child of `content`.
                // For text-only, create a fresh array wrapping the prompt; for vision, pass `content` whole.
                ["content"] = imageData is null
                    ? new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = request.Prompt,
                        },
                    }
                    : content,
            },
        };
    }

    private ModelResponse Failure(string message) =>
        new("", ProviderId, "vision", 0, 0, 0, _config.Model)
        {
            Success = false,
            ErrorMessage = message,
        };

    private static IReadOnlyDictionary<string, object> BuildDiagnostics(
        int attempt,
        bool useJsonMode,
        int imageBytes,
        double headersMs,
        double bodyMs) =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["attempt"] = attempt + 1,
            ["jsonMode"] = useJsonMode,
            ["imageBytes"] = imageBytes,
            ["headersMs"] = Math.Round(headersMs, 1),
            ["bodyMs"] = Math.Round(bodyMs, 1),
        };

    private static string DetectMime(byte[] data) =>
        data.Length >= 4
        && data[0] == 0x89
        && data[1] == 0x50
        && data[2] == 0x4E
        && data[3] == 0x47
            ? "image/png"
            : data.Length >= 3
              && data[0] == 0xFF
              && data[1] == 0xD8
              && data[2] == 0xFF
                ? "image/jpeg"
                : "image/png";
}
