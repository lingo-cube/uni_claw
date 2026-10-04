using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Perception;

/// <summary>OpenCode realization 的可替换 HTTP 配置。Product 只传入冻结 binding。</summary>
internal sealed record OpenCodeSlowRealizationOptions(
    Uri ChatCompletionsEndpoint,
    Uri ModelsEndpoint,
    TimeSpan RequestTimeout,
    TimeSpan CapacityRetryDelay,
    int MaxCapacityRetries = 2,
    string? ApiKey = null)
{
    public static OpenCodeSlowRealizationOptions Local(Uri? baseUri = null) {
        var root = baseUri ?? new Uri("http://127.0.0.1:4096/");
        return new(
            new Uri(root, "v1/chat/completions"),
            new Uri(root, "v1/models"),
            TimeSpan.FromSeconds(90),
            TimeSpan.FromSeconds(2));
    }

    public bool IsValid => ChatCompletionsEndpoint.IsAbsoluteUri
        && ModelsEndpoint.IsAbsoluteUri
        && RequestTimeout > TimeSpan.Zero
        && CapacityRetryDelay >= TimeSpan.Zero
        && MaxCapacityRetries >= 0;
}

internal sealed record SlowProviderInvocation(
    string RequestId,
    LogicalProfileId LogicalProfile,
    string ProviderId,
    string ModelId,
    SlowExecutionStatus Status,
    int CapacityRetries,
    string? Diagnostic);

internal sealed record SlowBindingAudit(
    LogicalProfileId LogicalProfile,
    string ProviderId,
    string ModelId,
    bool Available,
    string? Diagnostic = null)
{
    public ModelBindingSnapshot ToSnapshot(bool experimental = true) => new(
        LogicalProfile, ProviderId, ModelId,
        Available: Available, Experimental: experimental,
        Health: Available);
}

/// <summary>
/// Real Slow adapter for the OpenCode OpenAI-compatible endpoint. It owns only
/// serialization, transport, response validation and SlowResult construction.
/// P2, Evidence Ledger, WorldModel and Effect Boundary remain outside this type.
/// </summary>
internal sealed class OpenCodeSlowRealization : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly SlowModelManagement _models;
    private readonly OpenCodeSlowRealizationOptions _options;
    private readonly Func<string, CancellationToken, ValueTask<byte[]?>>? _rawArtifactResolver;
    private readonly ConcurrentQueue<SlowProviderInvocation> _invocations = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private bool _disposed;

    public OpenCodeSlowRealization(
        SlowModelManagement models,
        OpenCodeSlowRealizationOptions options,
        HttpClient? http = null,
        Func<string, CancellationToken, ValueTask<byte[]?>>? rawArtifactResolver = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsValid) throw new ArgumentException("invalid OpenCode options", nameof(options));
        _models = models;
        _options = options;
        _rawArtifactResolver = rawArtifactResolver;
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = options.RequestTimeout };
    }

    public IReadOnlyList<SlowProviderInvocation> Invocations => _invocations.ToArray();

    /// <summary>
    /// Availability is evidence from the provider's model catalogue. A string
    /// in a config file is never sufficient to mark a binding available.
    /// </summary>
    public async Task<IReadOnlyList<SlowBindingAudit>> AuditBindingsAsync(
        IReadOnlyList<(LogicalProfileId Profile, string Provider, string Model)> requested,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.ModelsEndpoint);
            AddApiKey(request);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return requested.Select(x => new SlowBindingAudit(x.Profile, x.Provider, x.Model, false,
                    $"model catalogue HTTP {(int)response.StatusCode}")).ToArray();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var ids = ReadModelIds(document.RootElement);
            return requested.Select(x => ids.Contains(x.Model, StringComparer.Ordinal)
                ? new SlowBindingAudit(x.Profile, x.Provider, x.Model, true)
                : new SlowBindingAudit(x.Profile, x.Provider, x.Model, false, "model id not advertised"))
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return requested.Select(x => new SlowBindingAudit(x.Profile, x.Provider, x.Model, false, "audit cancelled")).ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return requested.Select(x => new SlowBindingAudit(x.Profile, x.Provider, x.Model, false,
                $"model catalogue unavailable: {exception.GetType().Name}")).ToArray();
        }
    }

    public async Task<SlowResult> ExecuteAsync(
        SlowPerceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var binding = ResolveBinding(request);
        var invalid = ValidateRequest(request, binding);
        if (invalid is not null)
            return Record(request, binding, invalid.Value.Status, invalid.Value.Diagnostic, 0);

        byte[]? image = null;
        if (request.LogicalProfile == LogicalProfileId.Visual)
        {
            if (_rawArtifactResolver is null)
                return Record(request, binding, SlowExecutionStatus.InvalidInput,
                    "visual RawArtifact resolver is not configured", 0);
            try
            {
                image = await _rawArtifactResolver(request.RawArtifact!.ArtifactId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Record(request, binding, SlowExecutionStatus.Cancelled,
                    "RawArtifact resolution cancelled", 0);
            }
            catch (Exception exception)
            {
                return Record(request, binding, SlowExecutionStatus.InfrastructureFailure,
                    $"RawArtifact resolution failed: {exception.Message}", 0);
            }
            if (image is null || image.Length == 0)
                return Record(request, binding, SlowExecutionStatus.InvalidInput,
                    "same-capture RawArtifact PNG is unavailable", 0);
        }

        var body = BuildRequestBody(request, binding!, image);
        var capacityRetries = 0;
        while (true)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, _options.ChatCompletionsEndpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
                };
                AddApiKey(message);
                using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
                if (IsCapacityResponse(response.StatusCode) && capacityRetries < _options.MaxCapacityRetries)
                {
                    capacityRetries++;
                    await Task.Delay(_options.CapacityRetryDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (IsCapacityResponse(response.StatusCode))
                    return Record(request, binding, SlowExecutionStatus.InfrastructureFailure,
                        "provider capacity exhausted after delayed retries", capacityRetries);
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable)
                    return Record(request, binding, SlowExecutionStatus.ModelUnavailable,
                        $"provider/model unavailable HTTP {(int)response.StatusCode}", capacityRetries);
                if (!response.IsSuccessStatusCode)
                    return Record(request, binding, SlowExecutionStatus.InfrastructureFailure,
                        $"provider HTTP {(int)response.StatusCode}", capacityRetries);

                var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var result = ParseProviderResponse(request, binding!, raw);
                _invocations.Enqueue(new(request.RequestId, request.LogicalProfile, binding!.ProviderId,
                    binding.ModelId, result.Status, capacityRetries, result.Diagnostic));
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Record(request, binding, SlowExecutionStatus.Cancelled, "provider call cancelled", capacityRetries);
            }
            catch (OperationCanceledException)
            {
                return Record(request, binding, SlowExecutionStatus.Timeout, "provider call timed out", capacityRetries);
            }
            catch (HttpRequestException exception)
            {
                return Record(request, binding, SlowExecutionStatus.InfrastructureFailure, exception.Message, capacityRetries);
            }
            catch (JsonException exception)
            {
                return Record(request, binding, SlowExecutionStatus.MalformedResponse, exception.Message, capacityRetries);
            }
        }
    }

    private ModelBindingSnapshot? ResolveBinding(SlowPerceptionRequest request)
    {
        var resolved = _models.Resolve(request.LogicalProfile);
        if (!resolved.IsResolved) return null;
        if (request.Binding is { IsValid: true, Available: true, Health: true }
            && request.Binding.LogicalProfile == request.LogicalProfile)
            return request.Binding;
        return resolved.Binding;
    }

    private static (SlowExecutionStatus Status, string Diagnostic)? ValidateRequest(
        SlowPerceptionRequest request, ModelBindingSnapshot? binding)
    {
        if (request.Context is null || request.Capture is null || !request.IsValid)
            return (SlowExecutionStatus.InvalidInput, "request context/capture/correlation is invalid");
        if (request.Context.IsContextInsufficient)
            return (SlowExecutionStatus.ContextInsufficient, "bounded evidence context is insufficient");
        if (binding is null || !binding.IsValid || !binding.Available || !binding.Health)
            return (SlowExecutionStatus.ModelUnavailable, "ROUTING_UNAVAILABLE");
        if (binding.LogicalProfile != request.LogicalProfile)
            return (SlowExecutionStatus.ModelUnavailable, "binding identity does not match logical profile");
        return null;
    }

    private object BuildRequestBody(SlowPerceptionRequest request, ModelBindingSnapshot binding, byte[]? image)
    {
        var userContent = new List<object> { new { type = "text", text = SerializeContext(request) } };
        if (image is not null)
            userContent.Add(new { type = "image_url", image_url = new {
                url = "data:image/png;base64," + Convert.ToBase64String(image)
            }});
        return new {
            model = binding.ModelId,
            messages = new object[] {
                new { role = "system", content = "Return only the SlowResult JSON object. Free prose is invalid." },
                new { role = "user", content = userContent },
            },
            response_format = new { type = "json_object" },
            temperature = 0,
        };
    }

    internal string SerializeContext(SlowPerceptionRequest request)
    {
        var context = new {
            requestId = request.RequestId,
            logicalProfile = request.LogicalProfile.Value,
            requiredClaim = new { subject = request.RequiredClaim.Subject, field = request.RequiredClaim.Field },
            reason = request.Reason,
            buyerRef = request.BuyerRef,
            captureId = request.Capture.CaptureId,
            observationCycleId = request.Capture.ObservationCycleId,
            sessionCorrelation = request.Capture.SessionCorrelation,
            evidenceIds = request.Context.EvidenceIds.Order(StringComparer.Ordinal).ToArray(),
            elementLayout = request.Context.ElementLayout,
            semanticReasoning = request.Context.SemanticReasoning,
            conflictBasis = request.Context.ConflictBasis,
            exclusions = request.Context.Exclusions,
            fastBasis = request.FastBasis is null ? null : new {
                captureId = request.FastBasis.CaptureId,
                sessionCorrelation = request.FastBasis.SessionCorrelation,
                observationCycleId = request.FastBasis.ObservationCycleId,
                captureTimestamp = request.FastBasis.CaptureTimestamp,
                yoloDetections = request.FastBasis.YoloDetections,
                ocrTokens = request.FastBasis.OcrTokens,
                providerAvailable = request.FastBasis.ProviderAvailable,
                isFresh = request.FastBasis.IsFresh,
            },
            rawArtifactId = request.RawArtifact?.ArtifactId,
        };
        return JsonSerializer.Serialize(context, _json);
    }

    private SlowResult ParseProviderResponse(SlowPerceptionRequest request, ModelBindingSnapshot binding, string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var payload = root;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var message = choices[0].GetProperty("message");
            var content = message.GetProperty("content");
            if (content.ValueKind != JsonValueKind.String)
                return Empty(request, binding, SlowExecutionStatus.SchemaFailure, "message.content is not JSON text");
            var contentText = content.GetString();
            if (string.IsNullOrWhiteSpace(contentText))
                return Empty(request, binding, SlowExecutionStatus.MalformedResponse, "empty provider content");
            using var nested = JsonDocument.Parse(contentText);
            payload = nested.RootElement.Clone();
        }
        if (payload.ValueKind != JsonValueKind.Object)
            return Empty(request, binding, SlowExecutionStatus.SchemaFailure, "SlowResult must be a JSON object");

        var status = ReadStatus(payload);
        if (status is null)
            return Empty(request, binding, SlowExecutionStatus.SchemaFailure, "status is missing or invalid");
        if (status is not (SlowExecutionStatus.Succeeded or SlowExecutionStatus.Partial))
            return Empty(request, binding, status.Value, "provider returned terminal failure");

        var proposals = new List<ObservationProposal>();
        if (payload.TryGetProperty("proposals", out var rawProposals))
        {
            if (rawProposals.ValueKind != JsonValueKind.Array)
                return Empty(request, binding, SlowExecutionStatus.SchemaFailure, "proposals must be an array");
            foreach (var candidate in rawProposals.EnumerateArray())
            {
                if (!candidate.TryGetProperty("subject", out var subject)
                    || !candidate.TryGetProperty("value", out var value)
                    || subject.ValueKind != JsonValueKind.String || value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(subject.GetString()) || string.IsNullOrWhiteSpace(value.GetString()))
                {
                    if (status == SlowExecutionStatus.Succeeded)
                        return Empty(request, binding, SlowExecutionStatus.SchemaFailure, "malformed proposal");
                    continue;
                }
                proposals.Add(new ObservationProposal(
                    new ObservationClaim(subject.GetString()!, value.GetString()!),
                    IngressKind.Observation,
                    ObservationContext.External,
                    new Provenance(
                        $"slow.{binding.ProviderId}", request.Capture.CaptureTimestamp,
                        $"slow:{request.Capture.CaptureId}",
                        new[] { $"slow:{request.LogicalProfile.Value}", $"provider:{binding.ProviderId}",
                            $"model:{binding.ModelId}", $"capture:{request.Capture.CaptureId}" })));
            }
        }
        return new SlowResult(request.RequestId, request.AttemptKey, status.Value, proposals, request.Capture,
            binding, ReadDisposition(payload), ReadDiagnostic(payload));
    }

    private static SlowExecutionStatus? ReadStatus(JsonElement root) =>
        root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
            && Enum.TryParse<SlowExecutionStatus>(status.GetString(), true, out var parsed) ? parsed : null;

    private static SlowSemanticDisposition ReadDisposition(JsonElement root) =>
        root.TryGetProperty("semanticDisposition", out var value) && value.ValueKind == JsonValueKind.String
            && Enum.TryParse<SlowSemanticDisposition>(value.GetString(), true, out var parsed)
            ? parsed : SlowSemanticDisposition.Unknown;

    private static string? ReadDiagnostic(JsonElement root) =>
        root.TryGetProperty("diagnostic", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private SlowResult Record(SlowPerceptionRequest request, ModelBindingSnapshot? binding,
        SlowExecutionStatus status, string diagnostic, int retries) {
        var result = Empty(request, binding, status, diagnostic);
        if (binding is not null)
            _invocations.Enqueue(new(request.RequestId, request.LogicalProfile, binding.ProviderId,
                binding.ModelId, status, retries, diagnostic));
        return result;
    }

    private static SlowResult Empty(SlowPerceptionRequest request, ModelBindingSnapshot? binding,
        SlowExecutionStatus status, string diagnostic) => new(
        request.RequestId, request.AttemptKey, status, Array.Empty<ObservationProposal>(), request.Capture,
        binding, SlowSemanticDisposition.Unknown, diagnostic);

    private void AddApiKey(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    private static bool IsCapacityResponse(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status == 529;

    private static IReadOnlySet<string> ReadModelIds(JsonElement root)
    {
        var values = root.ValueKind == JsonValueKind.Array ? root : root.TryGetProperty("data", out var data) ? data : default;
        if (values.ValueKind != JsonValueKind.Array) return new HashSet<string>(StringComparer.Ordinal);
        return values.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String)
            .Select(x => x.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsHttp) _http.Dispose();
    }
}
