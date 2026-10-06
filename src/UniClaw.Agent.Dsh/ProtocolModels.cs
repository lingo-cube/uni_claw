using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

/// <summary>AGT-002 product protocol constants. These values are handshake data, not
/// DSH session state.</summary>
public static class ProductProtocolVersions
{
    public const string ProtocolVersion = "uniclaw.agent.protocol.v1";
    public const string SchemaVersion = "uniclaw.agent.schema.v1";
}

/// <summary>Version and generated-schema identity exchanged at startup.
/// F1: all six fields are mandatory protocol fields — there are no defaults.
/// A peer that omits any field produces a null/empty stamp member, which the
/// handshake validator rejects as a malformed handshake (fail closed).</summary>
public sealed record ProtocolStamp(
    string ProtocolVersion,
    string SchemaVersion,
    string SchemaHash,
    string ProfileId,
    string ProfileVersion,
    string CapabilityManifestHash);

/// <summary>Capabilities exposed by a Product DSH profile. The Product profile is
/// intentionally a small positive allowlist.</summary>
public sealed record CapabilityManifest(IReadOnlyList<string> Capabilities)
{
    public static CapabilityManifest ProductHeadless { get; } =
        new(new[] { ProductCapabilities.SubmitDecision });

    public IReadOnlyList<string> NormalizedCapabilities => (Capabilities ?? Array.Empty<string>())
        .Where(static c => !string.IsNullOrWhiteSpace(c))
        .Select(static c => c.Trim())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public bool HasDuplicates => Capabilities is null
        || Capabilities.Count != NormalizedCapabilities.Count;

    public string ManifestHash => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", NormalizedCapabilities)))).ToLowerInvariant();
}

public static class ProductCapabilities
{
    public const string SubmitDecision = "submit_decision";

    // Kept as named constants so a forbidden capability cannot be smuggled in as
    // an unreviewed string in the adapter.
    public static readonly IReadOnlySet<string> ForbiddenProductCapabilities =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "webserver", "interactive-ui", "interactive_ui", "shell", "filesystem",
            "browser", "device", "device-effect", "adb", "click", "swipe", "workflow",
            "subagent", "todo", "task-manager", "task_manager", "ask-user", "ask_user",
            "steer", "inject", "approval", "approval-ui", "approval_ui",
        };
}

public sealed record HandshakeRequest(
    ProtocolStamp Protocol,
    CapabilityManifest ExpectedCapabilities,
    string ProductSessionId,
    string ProductRunId);

public sealed record HandshakeResponse(
    bool Accepted,
    ProtocolStamp Protocol,
    CapabilityManifest ReportedCapabilities,
    string? DshSessionId,
    string? FailureReason = null,
    IReadOnlyList<string>? RuntimeCapabilities = null,
    string? RuntimePreset = null);

public sealed record DshDiagnostic(
    string Code,
    string Message,
    string? RequestId = null,
    long? Generation = null,
    string? DecisionId = null);

/// <summary>Product realization profile. A profile is the allowlisted capability
/// surface; it deliberately does not contain provider or model selection.</summary>
public static class UniagentProdProfile
{
    public const string ProfileId = "uniagent-prod";
    public const string ProfileVersion = "1";

    public static ProductProfile Current => new(
        ProfileId,
        ProfileVersion,
        CapabilityManifest.ProductHeadless,
        ProductProtocolVersions.ProtocolVersion,
        ProductProtocolVersions.SchemaVersion);
}

public sealed record ProductProfile(
    string ProfileId,
    string ProfileVersion,
    CapabilityManifest Capabilities,
    string ProtocolVersion,
    string SchemaVersion);

public sealed record DshServiceEndpoint(Uri BaseUri)
{
    public void Validate()
    {
        if (!BaseUri.IsAbsoluteUri)
            throw new ArgumentException("DSH service URI must be absolute", nameof(BaseUri));
        if (BaseUri.Scheme != "http" && BaseUri.Scheme != "https")
            throw new ArgumentException("DSH service URI must be http(s)", nameof(BaseUri));
    }
}

/// <summary>Provider/model selection is configuration only. Replacing this
/// value cannot change the Product decision protocol or Kernel semantics.
/// F2: there is no hardcoded model catalog in Product code — provider, name,
/// and service endpoint come from the single runtime config source
/// (<see cref="UniagentProdYaml"/>).</summary>
public sealed record ModelConfiguration(string Provider, string Name)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider)) throw new ArgumentException("provider is required", nameof(Provider));
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("model name is required", nameof(Name));
    }
}

/// <summary>
/// CAP-007 — per-profile 模型选择：一个产品 logical profile 到有序 choice keys
/// 的映射（单个 key = 单选；多个 = 偏好序，首选在前）。choice key 必须能在
/// <see cref="UniagentProdConfiguration.Choices"/> 中解析（loader fail-closed 校验）。
/// </summary>
public sealed record ModelProfileSelection(
    string Profile,
    IReadOnlyList<string> ChoiceKeys)
{
    /// <summary>profile 非空且至少一个非空 choice key。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Profile)
        && ChoiceKeys is { Count: > 0 }
        && ChoiceKeys.All(key => !string.IsNullOrWhiteSpace(key));
}

public sealed record UniagentProdConfiguration(
    ProductProfile Profile,
    ModelConfiguration Model,
    DshServiceEndpoint Service,
    string? SelectedModelKey = null,
    IReadOnlyDictionary<string, ModelConfiguration>? Choices = null,
    IReadOnlyList<ModelProfileSelection>? ProfileSelections = null)
{
    /// <summary>Explicit construction for tests and fixtures. The composed
    /// runtime loads its configuration from the single source
    /// (<see cref="UniagentProdYaml.LoadDefault"/>), never from code constants.</summary>
    public static UniagentProdConfiguration Create(
        ModelConfiguration model,
        DshServiceEndpoint service,
        string? selectedModelKey = null,
        IReadOnlyDictionary<string, ModelConfiguration>? choices = null,
        IReadOnlyList<ModelProfileSelection>? profileSelections = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(service);
        model.Validate();
        service.Validate();
        return new(UniagentProdProfile.Current, model, service, selectedModelKey, choices, profileSelections);
    }
}

/// <summary>JSON-RPC envelope used by the local stdio transport. The DSH protocol
/// implementation may carry arbitrary JSON in Result; the adapter only accepts the
/// product response shape.</summary>
public sealed record JsonRpcRequest(
    int Id,
    string Method,
    JsonElement Params);

public sealed record JsonRpcError(int Code, string Message, JsonElement? Data = null);

public sealed record JsonRpcResponse(
    int Id,
    JsonElement? Result,
    JsonRpcError? Error = null);

public static class ProductProtocolJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new AgentDecisionJsonConverter());
        options.Converters.Add(new PolicyPredicateJsonConverter());
        options.Converters.Add(new PolicyGuardJsonConverter());
        return options;
    }
}
