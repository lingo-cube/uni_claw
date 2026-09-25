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

/// <summary>Version and generated-schema identity exchanged at startup.</summary>
public sealed record ProtocolStamp(
    string ProtocolVersion,
    string SchemaVersion,
    string SchemaHash);

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
    string? FailureReason = null);

public sealed record DshDiagnostic(
    string Code,
    string Message,
    string? RequestId = null,
    long? Generation = null,
    string? DecisionId = null);

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
