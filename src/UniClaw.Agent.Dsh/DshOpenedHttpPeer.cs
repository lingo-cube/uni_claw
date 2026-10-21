using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using UniClaw.Agent.Profile;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// The formal authenticated local-web peer (AGT-002 E2E slice): the single
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
    private const int MaxCapacityRetries = 2;
    private static readonly TimeSpan CapacityRetryDelay = TimeSpan.FromSeconds(2);
    private const string HandshakePath = "/api/uniclaw-agent/handshake";
    private const string ConsultPath = "/api/uniclaw-agent/consult";
    private const string SlowPath = "/api/uniclaw-agent/slow";
    private const string AbortPath = "/api/uniclaw-agent/abort";
    private const string DetachPath = "/api/uniclaw-agent/detach";

    private readonly HttpClient _http;
    private readonly DshWebCredential _credential;
    private readonly bool _ownsHttp;
    private readonly JsonSerializerOptions _json = ProductProtocolJson.CreateOptions();
    private readonly ModelConfiguration? _model;
    private readonly ArtifactFingerprint _expectedArtifacts;
    private string? _dshSessionId;
    private bool _disposed;

    public DshOpenedHttpPeer(
        DshServiceEndpoint endpoint,
        DshWebCredential? credential = null,
        HttpMessageHandler? handler = null,
        ModelConfiguration? model = null,
        TimeSpan? requestTimeout = null,
        UniAgentProfile? expectedProfile = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        endpoint.Validate();
        _credential = credential ?? DshWebCredential.Mint(endpoint);
        _model = model;
        var profile = expectedProfile ?? UniAgentProfileYaml.LoadDefault();
        _expectedArtifacts = CreateArtifactFingerprint(profile);
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
            expectedArtifacts = _expectedArtifacts,
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
        var response = new HandshakeResponse(
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
                : null,
            ReadArtifacts(root));
        if (response.Accepted && !ArtifactsMatch(_expectedArtifacts, response.ReportedArtifacts))
            response = response with
            {
                Accepted = false,
                DshSessionId = null,
                FailureReason = "artifact-fingerprint-mismatch",
            };
        _dshSessionId = response.Accepted ? response.DshSessionId : null;
        return response;
    }

    private static ArtifactFingerprint CreateArtifactFingerprint(UniAgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var promptHash = profile.Assembly?.PromptManifest?.Hash;
        var skills = profile.AllowedSkillRefs ?? Array.Empty<SkillReference>();
        if (string.IsNullOrWhiteSpace(promptHash) || promptHash.Length != 64
            || !promptHash.All(Uri.IsHexDigit))
            throw new InvalidOperationException("profile-prompt-artifact-pin-missing-or-invalid");
        if (skills.Count != 1)
            throw new InvalidOperationException("profile-primary-skill-pin-count-must-be-one");
        var skill = skills[0];
        if (string.IsNullOrWhiteSpace(skill.Name) || skill.Revision < 1
            || skill.Sha256.Length != 64 || !skill.Sha256.All(Uri.IsHexDigit))
            throw new InvalidOperationException("profile-primary-skill-pin-invalid");
        return new ArtifactFingerprint(
            promptHash.ToLowerInvariant(),
            new SkillArtifactFingerprint(skill.Name, skill.Revision, skill.Sha256.ToLowerInvariant()));
    }

    private static ArtifactFingerprint? ReadArtifacts(JsonElement root)
    {
        if (!root.TryGetProperty("artifacts", out var artifacts)
            || artifacts.ValueKind != JsonValueKind.Object
            || !artifacts.TryGetProperty("promptHash", out var promptHash)
            || promptHash.ValueKind != JsonValueKind.String
            || !artifacts.TryGetProperty("skill", out var skill)
            || skill.ValueKind != JsonValueKind.Object
            || !skill.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String
            || !skill.TryGetProperty("revision", out var revision)
            || revision.ValueKind != JsonValueKind.Number
            || !revision.TryGetInt32(out var revisionValue)
            || !skill.TryGetProperty("sha256", out var sha256)
            || sha256.ValueKind != JsonValueKind.String)
            return null;
        return new ArtifactFingerprint(promptHash.GetString()!,
            new SkillArtifactFingerprint(name.GetString()!, revisionValue, sha256.GetString()!));
    }

    private static bool ArtifactsMatch(ArtifactFingerprint expected, ArtifactFingerprint? reported) =>
        reported is not null
        && string.Equals(expected.PromptHash, reported.PromptHash, StringComparison.OrdinalIgnoreCase)
        && string.Equals(expected.Skill.Name, reported.Skill.Name, StringComparison.Ordinal)
        && expected.Skill.Revision == reported.Skill.Revision
        && string.Equals(expected.Skill.Sha256, reported.Skill.Sha256, StringComparison.OrdinalIgnoreCase);

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
            ["turnTimeoutMs"] = (int)TimeSpan.FromSeconds(180).TotalMilliseconds, // PER-019: reasoning models need longer consult turns
        };
        if (_dshSessionId is { Length: > 0 })
            body["dshSessionId"] = _dshSessionId;
        if (request.ImagePng is { Length: > 0 })
            body["image"] = new
            {
                mediaType = "image/png",
                data = Convert.ToBase64String(request.ImagePng),
                name = request.Context.Screen?.ContainerId is { Length: > 0 } id ? id + ".png" : "capture.png",
            };
        if (_model is { } model)
            body["model"] = new { provider = model.Provider, model = model.Name };
        for (var retry = 0; ; retry++)
        {
            var document = await PostJsonAsync(ConsultPath, body, cancellationToken).ConfigureAwait(false);
            var response = DecodeDecisionResponse(document, request);
            // [DEBUG-drop] TRAJECTORY-001 E4 取证：C# 侧偶发丢结果定位。
            if (response.Decision is null)
            {
                var rawJson = document.RootElement.GetRawText();
                Console.Error.WriteLine(
                    $"[DEBUG-drop] requestId={request.RequestId} decision=null "
                    + $"error={response.Error ?? "none"} "
                    + $"rawLength={rawJson.Length} raw={rawJson[..Math.Min(500, rawJson.Length)]}");
            }
            if (retry >= MaxCapacityRetries || !IsCapacityFailure(response))
                return response;
            await Task.Delay(CapacityRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private DecisionChannelResponse DecodeDecisionResponse(
        JsonDocument document, DecisionRequest request)
    {
        try
        {
            var root = document.RootElement;
            if (root.TryGetProperty("ok", out var ok)
                && ok.ValueKind == JsonValueKind.False)
            {
                return new DecisionChannelResponse(
                    request.RequestId, request.Generation, null, ReadErrorMessage(root));
            }
            AgentDecision? decision = null;
            AgentTaskInitialization? taskInitialization = null;
            if (root.TryGetProperty("task", out var taskElement)
                && taskElement.ValueKind == JsonValueKind.Object)
            {
                if (!taskElement.TryGetProperty("initialization", out var initializationElement)
                    || initializationElement.ValueKind != JsonValueKind.Object
                    || !taskElement.TryGetProperty("payload", out var payloadElement)
                    || payloadElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.String))
                    throw new JsonException("task requires initialization and payload");
                taskInitialization = initializationElement.Deserialize<AgentTaskInitialization>(_json)
                    ?? throw new JsonException("task initialization is null");
                decision = payloadElement.ValueKind == JsonValueKind.String
                    ? JsonSerializer.Deserialize<AgentDecision>(payloadElement.GetString()!, _json)
                    : JsonSerializer.Deserialize<AgentDecision>(payloadElement.GetRawText(), _json);
            }
            if (root.TryGetProperty("capabilitySelection", out _))
                throw new JsonException("top-level capabilitySelection is no longer accepted; use task.initialization");
            var errorMessage = ReadErrorMessage(root);
            if (root.TryGetProperty("diagnostics", out var diagnostics)
                && diagnostics.ValueKind == JsonValueKind.Object
                && diagnostics.TryGetProperty("message", out var diagnosticMessage)
                && diagnosticMessage.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(diagnosticMessage.GetString()))
            {
                errorMessage = string.IsNullOrWhiteSpace(errorMessage)
                    ? diagnosticMessage.GetString()
                    : $"{errorMessage}:{diagnosticMessage.GetString()}";
            }
            return new DecisionChannelResponse(
                root.GetProperty("requestId").GetString() ?? request.RequestId,
                root.TryGetProperty("generation", out var generation) && generation.TryGetInt64(out var value)
                    ? value : request.Generation,
                decision, errorMessage, taskInitialization);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            var payload = document.RootElement.GetRawText();
            return new DecisionChannelResponse(
                request.RequestId, request.Generation, null,
                $"decision-decode-error:{ex.Message}:payload={payload[..Math.Min(2000, payload.Length)]}");
        }
    }

    private static bool IsCapacityFailure(DecisionChannelResponse response)
    {
        var text = response.Error ?? string.Empty;
        return text.Contains("429", StringComparison.OrdinalIgnoreCase)
            || text.Contains("529", StringComparison.OrdinalIgnoreCase)
            || text.Contains("capacity", StringComparison.OrdinalIgnoreCase)
            || text.Contains("too many", StringComparison.OrdinalIgnoreCase)
            || text.Contains("selected model is at capacity", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Runs a DSH-backed Slow prompt and returns only the captured
    /// assistant text. Product-side schema validation remains the caller's
    /// responsibility; no assistant prose is promoted here.</summary>
    public async Task<DshSlowResponse> ExecuteSlowAsync(
        string requestId,
        string prompt,
        ModelConfiguration model,
        byte[]? imagePng = null,
        CancellationToken cancellationToken = default,
        string? reasoningEffort = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(model);
        model.Validate();
        var body = new Dictionary<string, object?>
        {
            ["requestId"] = requestId,
            ["prompt"] = prompt,
            ["turnTimeoutMs"] = (int)TimeSpan.FromSeconds(110).TotalMilliseconds,
            ["model"] = new { provider = model.Provider, model = model.Name },
        };
        if (reasoningEffort is not null)
        {
            if (reasoningEffort != "high") throw new ArgumentException("explicit slow effort must be high", nameof(reasoningEffort));
            body["reasoningEffort"] = reasoningEffort;
        }
        if (imagePng is { Length: > 0 })
            body["image"] = new { mediaType = "image/png", data = Convert.ToBase64String(imagePng), name = "capture.png" };
        for (var retry = 0; ; retry++)
        {
            JsonDocument document;
            try { document = await PostJsonAsync(SlowPath, body, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation stops only this Slow request. A stale cleanup
                // cannot abort a later request or the main decision session.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await PostJsonAsync(AbortPath, new { slowRequestId = requestId }, cleanup.Token).ConfigureAwait(false); }
                catch (Exception) { /* Caller remains cancelled; server deadline/archival is still authoritative. */ }
                throw;
            }
            var root = document.RootElement;
            // AGT-019/RUN-006 修复：插件 fail() 封包 error 为 {code,message}
            // 对象（consult 路径同形状），slow 解析此前只认字符串——真实
            // 错误被吞成 slow-endpoint-empty-text。归一两种形状。
            string? errorText = null;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    errorText = error.GetString();
                else if (error.ValueKind == JsonValueKind.Object)
                {
                    var code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                    var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                    errorText = string.IsNullOrWhiteSpace(code) ? message
                        : string.IsNullOrWhiteSpace(message) ? code
                        : $"{code}:{message}";
                }
            }
            var result = new DshSlowResponse(
                root.TryGetProperty("requestId", out var id) ? id.GetString() ?? requestId : requestId,
                root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null,
                errorText,
                root.TryGetProperty("diagnostics", out var diagnostics) ? diagnostics.GetRawText() : null);
            if (retry >= MaxCapacityRetries || !IsCapacityFailure(result)) return result;
            await Task.Delay(CapacityRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsCapacityFailure(DshSlowResponse response)
    {
        var text = (response.Error ?? "") + " " + (response.Diagnostic ?? "");
        return text.Contains("429", StringComparison.OrdinalIgnoreCase)
            || text.Contains("529", StringComparison.OrdinalIgnoreCase)
            || text.Contains("capacity", StringComparison.OrdinalIgnoreCase)
            || text.Contains("too many", StringComparison.OrdinalIgnoreCase);
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
        _dshSessionId = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _dshSessionId = null;
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

    private static string? ReadErrorMessage(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error)) return null;
        if (error.ValueKind == JsonValueKind.String) return error.GetString();
        if (error.ValueKind != JsonValueKind.Object) return error.GetRawText();

        var code = error.TryGetProperty("code", out var codeElement)
            && codeElement.ValueKind == JsonValueKind.String
            ? codeElement.GetString()
            : null;
        var message = error.TryGetProperty("message", out var messageElement)
            && messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString()
            : null;
        return string.IsNullOrWhiteSpace(message)
            ? code
            : string.IsNullOrWhiteSpace(code) ? message : $"{code}:{message}";
    }

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
