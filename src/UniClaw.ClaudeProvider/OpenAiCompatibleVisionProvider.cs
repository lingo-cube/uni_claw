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

    public OpenAiCompatibleProviderConfig(
        string ApiKey,
        string Model,
        string BaseUrl,
        double RequestTimeoutSeconds = 120.0)
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
        try
        {
            using var response = await _http.SendAsync(message, timeout.Token)
                .ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure(
                    $"Sensenova HTTP {(int)response.StatusCode}: {raw}");
            }

            var document = JsonNode.Parse(raw);
            var content = document?["choices"]?[0]?["message"]?["content"]
                ?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content) && attempt < 2)
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
                _config.Model);
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
                ["content"] = imageData is null ? content[0] : content,
            },
        };
    }

    private ModelResponse Failure(string message) =>
        new("", ProviderId, "vision", 0, 0, 0, _config.Model)
        {
            Success = false,
            ErrorMessage = message,
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
