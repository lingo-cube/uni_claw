using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Perception;

/// <summary>
/// Production perception source — calls the local Python vision service
/// (tools/local_vision/server.py) via Unix Domain Socket.
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

        // Encode screenshot as JPEG for transport
        using var jpegStream = new MemoryStream();
        screenshot.Encode(jpegStream, SKEncodedImageFormat.Jpeg, quality: 92);
        jpegStream.Position = 0;

        using var content = new ByteArrayContent(jpegStream.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _http.PostAsync(
            "/v1/analyze", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            return []; // perception failure → empty result, truthful

        var json = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var evidence = JsonSerializer.Deserialize<VisionEvidence>(json, JsonOptions);
        if (evidence?.Candidates is null)
            return [];

        var candidates = ImmutableArray.CreateBuilder<PerceptionCandidate>();
        foreach (var c in evidence.Candidates)
        {
            ElementBounds? bounds = null;
            if (c.Bounds is { } b)
            {
                bounds = new ElementBounds(
                    (float)b.X1, (float)b.Y1, (float)b.X2, (float)b.Y2);
                if (!bounds.IsValid)
                    bounds = null;
            }

            candidates.Add(new PerceptionCandidate(
                c.Text ?? "",
                c.Type ?? "",
                bounds));
        }

        return candidates.ToImmutable();
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

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
