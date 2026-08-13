using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;

namespace UniClaw.Runtime.Adapters.Perception;

/// <summary>
/// Production perception source — calls the local Python vision service
/// (<c>uniclaw_perception.server:app</c> under <c>platforms/perception</c>) via
/// Unix Domain Socket.
///
/// Transports a screenshot to POST /v1/analyze, parses the YOLO+OCR+fusion
/// evidence JSON, and returns structured PerceptionCandidates.
///
/// Owns transport mechanics only. Owns NO semantic belief, Agent authority,
/// state truth, or goal completion.
/// </summary>
public sealed class LocalVisionPerceptionSource : IPerceptionSource
{
    private readonly HttpClient _http;

    internal LocalVisionPerceptionSource(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Creates a perception source connected to the local vision service.
    /// </summary>
    /// <param name="socketPath">Unix domain socket path (e.g., /tmp/uniclaw-vision.sock).</param>
    /// <param name="timeout">Request timeout. Default 30s.</param>
    public LocalVisionPerceptionSource(string socketPath, TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, ct) =>
            {
                var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.Unix,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Unspecified);
                await socket.ConnectAsync(
                    new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath), ct);
                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            },
        };

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
    }

    /// <inheritdoc />
    public async Task<ImmutableArray<PerceptionCandidate>> AnalyzeAsync(
        SKBitmap screenshot,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(screenshot);

        try
        {
            // Encode screenshot as JPEG for transport.
            using var jpegStream = new MemoryStream();
            screenshot.Encode(jpegStream, SKEncodedImageFormat.Jpeg, quality: 92);
            jpegStream.Position = 0;

            using var content = new ByteArrayContent(jpegStream.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await _http.PostAsync(
                "/v1/analyze", content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return EmptyWithDiagnostic("INFRASTRUCTURE_FAILURE");

            var json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            VisionEvidence? evidence;
            try { evidence = JsonSerializer.Deserialize<VisionEvidence>(json, JsonOptions); }
            catch (JsonException) { return EmptyWithDiagnostic("MALFORMED_RESPONSE"); }

            if (evidence?.Candidates is null)
                return EmptyWithDiagnostic("SCHEMA_FAILURE");

            var candidates = ImmutableArray.CreateBuilder<PerceptionCandidate>();
            var invalidGeometry = evidence.Diagnostics?.Any(d =>
                string.Equals(d.Code, "INVALID_GEOMETRY", StringComparison.Ordinal)) == true;
            foreach (var c in evidence.Candidates)
            {
                ElementBounds? bounds = null;
                if (c.Bounds is { } b)
                {
                    bounds = new ElementBounds(
                        (float)b.X1, (float)b.Y1, (float)b.X2, (float)b.Y2);
                    if (!bounds.IsValid)
                    {
                        // A schema-valid response containing unusable spatial evidence
                        // is still fail-closed; do not turn it into a tappable candidate.
                        invalidGeometry = true;
                        continue;
                    }
                }

                candidates.Add(new PerceptionCandidate(c.Text ?? "", c.Type ?? "", bounds));
            }

            var result = candidates.ToImmutable();
            if (invalidGeometry)
                EmitDiagnostic("INVALID_GEOMETRY");
            else if (result.IsEmpty)
                EmitDiagnostic("OK_EMPTY");
            else
                EmitDiagnostic("OK");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is control flow, never an empty-evidence diagnostic.
            throw;
        }
        catch (TaskCanceledException)
        {
            return EmptyWithDiagnostic("TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return EmptyWithDiagnostic("INFRASTRUCTURE_FAILURE");
        }
        catch (Exception)
        {
            return EmptyWithDiagnostic("INFRASTRUCTURE_FAILURE");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private static ImmutableArray<PerceptionCandidate> EmptyWithDiagnostic(string failureClass)
    {
        EmitDiagnostic(failureClass);
        return [];
    }

    // This augments the active Environment observation span when one exists.
    // It deliberately never becomes a Runtime semantic result or decision input.
    private static void EmitDiagnostic(string outcome)
    {
        var activity = System.Diagnostics.Activity.Current;
        RuntimeObservability.SetTag(activity, "perception.outcome", outcome);
        RuntimeObservability.SetTag(activity, "perception.failure_class",
            outcome is "OK" or "OK_EMPTY" ? null : outcome);
        RuntimeObservability.AddEvent(activity, $"perception.{outcome}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

// ── Response DTOs matching Python server schema ──────────────────────────

file sealed class VisionEvidence
{
    [JsonPropertyName("candidates")]
    public List<VisionCandidate>? Candidates { get; init; }

    // Additive Phase 3 response field. Older service responses omit it.
    [JsonPropertyName("diagnostics")]
    public List<VisionDiagnostic>? Diagnostics { get; init; }
}

file sealed class VisionDiagnostic
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

file sealed class VisionCandidate
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("bounds")]
    public VisionBounds? Bounds { get; init; }
}

file sealed class VisionBounds
{
    [JsonPropertyName("x1")]
    public double X1 { get; init; }

    [JsonPropertyName("y1")]
    public double Y1 { get; init; }

    [JsonPropertyName("x2")]
    public double X2 { get; init; }

    [JsonPropertyName("y2")]
    public double Y2 { get; init; }
}
