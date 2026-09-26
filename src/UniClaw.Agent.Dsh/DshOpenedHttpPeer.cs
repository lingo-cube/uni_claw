using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// The formal authenticated 3080 peer (AGT-002 E2E slice): the single
/// sanctioned concrete <see cref="IDshOpenedChannelPeer"/>. It speaks the
/// uniagent-prod decision-channel routes that the
/// <c>@uniclaw/dsh-decision-channel</c> DSH profile plugin mounts on the
/// authenticated <c>/api</c> lane of the local DSH web service:
///
///   POST /api/uniclaw-agent/handshake
///   POST /api/uniclaw-agent/consult
///   POST /api/uniclaw-agent/abort
///
/// Transport authentication is the DSH web cookie minted from the harness
/// home's secure credential store (<see cref="DshWebCredential"/> — the same
/// programmatic pattern DSH's own host tests use; no browser, no stdio, no
/// fallback). 401/403 and missing credentials fail closed. All Product
/// semantics stay behind <see cref="DshOpenedDecisionChannel"/>, which
/// validates the handshake against the frozen stamp.
/// </summary>
public sealed class DshOpenedHttpPeer : IDshOpenedChannelPeer
{
    private const string HandshakePath = "/api/uniclaw-agent/handshake";
    private const string ConsultPath = "/api/uniclaw-agent/consult";
    private const string AbortPath = "/api/uniclaw-agent/abort";
    private const string DetachPath = "/api/uniclaw-agent/detach";

    private readonly HttpClient _http;
    private readonly DshWebCredential _credential;
    private readonly bool _ownsHttp;
    private readonly JsonSerializerOptions _json = ProductProtocolJson.CreateOptions();
    private readonly ModelConfiguration? _model;
    private bool _disposed;

    public DshOpenedHttpPeer(
        DshServiceEndpoint endpoint,
        DshWebCredential? credential = null,
        HttpMessageHandler? handler = null,
        ModelConfiguration? model = null,
        TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        endpoint.Validate();
        _credential = credential ?? DshWebCredential.Mint(endpoint);
        _model = model;
        _ownsHttp = handler is null;
        _http = handler is null
            ? new HttpClient { Timeout = requestTimeout ?? TimeSpan.FromSeconds(90) }
            : new HttpClient(handler, disposeHandler: false) { Timeout = requestTimeout ?? TimeSpan.FromSeconds(90) };
        _http.BaseAddress = endpoint.BaseUri;
        // The trust fence requires the loopback Host header; a non-browser
        // client sends no Origin.
        _http.DefaultRequestHeaders.Host = endpoint.BaseUri.Authority;
    }

    public async Task<HandshakeResponse> HandshakeAsync(
        HandshakeRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        var body = new
        {
            protocol = new
            {
                protocolVersion = request.Protocol.ProtocolVersion,
                schemaVersion = request.Protocol.SchemaVersion,
                schemaHash = request.Protocol.SchemaHash,
                profileId = request.Protocol.ProfileId,
                profileVersion = request.Protocol.ProfileVersion,
                capabilityManifestHash = request.Protocol.CapabilityManifestHash,
            },
            expectedCapabilities = new { capabilities = request.ExpectedCapabilities.NormalizedCapabilities },
            productSessionId = request.ProductSessionId,
            productRunId = request.ProductRunId,
        };
        var document = await PostJsonAsync(HandshakePath, body, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        // The channel reports failures as {ok:false, error:{code,message}};
        // map them to a rejected HandshakeResponse instead of throwing.
        if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False
            && root.TryGetProperty("error", out var channelError))
        {
            var message = channelError.TryGetProperty("message", out var m) ? m.GetString() : null;
            var code = channelError.TryGetProperty("code", out var c) ? c.GetString() : null;
            return new HandshakeResponse(
                false,
                new ProtocolStamp("", "", "", "", "", ""),
                new CapabilityManifest(Array.Empty<string>()),
                null,
                $"{code ?? "handshake-rejected"}:{message ?? ""}");
        }
        return new HandshakeResponse(
            root.GetProperty("accepted").GetBoolean(),
            ReadStamp(document.RootElement.GetProperty("protocol")),
            ReadManifest(document.RootElement.GetProperty("reportedCapabilities")),
            document.RootElement.TryGetProperty("dshSessionId", out var sessionId)
                ? sessionId.GetString()
                : null,
            document.RootElement.TryGetProperty("error", out var error)
                ? error.GetProperty("message").GetString()
                : null,
            ReadOptionalStrings(document.RootElement, "runtimeCapabilities"),
            document.RootElement.TryGetProperty("runtimePreset", out var preset)
                ? preset.GetString()
                : null);
    }

    public async Task<DecisionChannelResponse> ReceiveDecisionRequestAsync(
        DecisionRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        var contextJson = JsonSerializer.Serialize(request.Context, _json);
        var body = new Dictionary<string, object?>
        {
            ["requestId"] = request.RequestId,
            ["generation"] = request.Generation,
            ["productSessionId"] = request.ProductSessionId,
            ["productRunId"] = request.ProductRunId,
            ["context"] = JsonDocument.Parse(contextJson).RootElement.Clone(),
            ["turnTimeoutMs"] = (int)TimeSpan.FromSeconds(75).TotalMilliseconds,
        };
        if (_model is { } model)
            body["model"] = new { provider = model.Provider, model = model.Name };
        var document = await PostJsonAsync(ConsultPath, body, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        AgentDecision? decision = null;
        if (root.TryGetProperty("decision", out var decisionElement)
            && decisionElement.ValueKind is JsonValueKind.Object or JsonValueKind.String)
        {
            decision = decisionElement.ValueKind == JsonValueKind.String
                ? JsonSerializer.Deserialize<AgentDecision>(decisionElement.GetString()!, _json)
                : JsonSerializer.Deserialize<AgentDecision>(decisionElement.GetRawText(), _json);
        }
        return new DecisionChannelResponse(
            root.GetProperty("requestId").GetString() ?? request.RequestId,
            root.TryGetProperty("generation", out var generation) && generation.TryGetInt64(out var value)
                ? value
                : request.Generation,
            decision,
            root.TryGetProperty("error", out var consultError)
                ? consultError.GetString()
                : null);
    }

    public async Task AbortCurrentTurnAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await PostJsonAsync(AbortPath, new { }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>S1: realization-private detach (physical cleanup only).</summary>
    public async Task DetachAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await PostJsonAsync(DetachPath, new { }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsHttp)
            _http.Dispose();
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    private async Task<JsonDocument> PostJsonAsync(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Add("Cookie", _credential.CookieHeader);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new HttpRequestException(
                $"dsh-web-auth-failed:{(int)response.StatusCode}:{path}", null, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            throw new HttpRequestException($"dsh-channel-malformed:{path}:{raw[..Math.Min(200, raw.Length)]}");
        }
    }

    private static IReadOnlyList<string>? ReadOptionalStrings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Select(item => item.GetString() ?? "").ToArray()
            : null;

    private static ProtocolStamp ReadStamp(JsonElement element) => new(
        element.GetProperty("protocolVersion").GetString() ?? "",
        element.GetProperty("schemaVersion").GetString() ?? "",
        element.GetProperty("schemaHash").GetString() ?? "",
        element.GetProperty("profileId").GetString() ?? "",
        element.GetProperty("profileVersion").GetString() ?? "",
        element.GetProperty("capabilityManifestHash").GetString() ?? "");

    private static CapabilityManifest ReadManifest(JsonElement element) =>
        new((element.GetProperty("capabilities").EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .ToArray()));
}
