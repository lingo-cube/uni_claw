using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Agent.Profile;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

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

/// <summary>
/// 任务初始化里的能力选择。它是 transport envelope 的装配字段，不是
/// AgentDecision 的动作内容；当前最小买方是 language inspection。
/// </summary>
public sealed record CapabilitySelectionPayload(
    string CapabilityId,
    string? ExpectedLanguage = null,
    IReadOnlyList<string>? IgnoreRoutes = null);

/// <summary>
/// Agent→Host task initialization shared by every task payload kind. It is
/// metadata for the task envelope, not an action-tool field.
/// </summary>
public sealed record AgentTaskInitialization(
    CapabilitySelectionPayload? CapabilitySelection = null);

/// <summary>Common Agent task envelope. The payload remains the Kernel-owned
/// AgentDecision union; the envelope owns task initialization.</summary>
public sealed record AgentTaskEnvelope(
    AgentTaskInitialization Initialization,
    AgentDecision Payload);

/// <summary>DSH decision-channel service endpoint (PRF-002: lives in the DSH
/// binding file, never in the product profile).</summary>
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
/// and service endpoint come from the DSH realization binding
/// (<see cref="UniagentDshBindingsYaml"/>; PRF-002/ADR-0041).</summary>
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
/// <see cref="UniagentDshBindings.Choices"/> 中解析（loader fail-closed 校验）。
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
    /// <summary>AGT-019 B2：ObserveDepth 字符串载荷转换器（strict——
    /// 只收字符串名，与生成 schema 的 enum 声明一致）。</summary>
    private sealed class ObserveDepthJsonConverter
        : System.Text.Json.Serialization.JsonConverter<ObserveDepth>
    {
        public override ObserveDepth Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String
            && System.Enum.TryParse<ObserveDepth>(reader.GetString(), out var depth)
                ? depth
                : throw new JsonException("observe spec depth must be Normal, Focused, or Deep");

        public override void Write(
            Utf8JsonWriter writer, ObserveDepth value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new AgentDecisionJsonConverter());
        options.Converters.Add(new PolicyPredicateJsonConverter());
        options.Converters.Add(new PolicyGuardJsonConverter());
        // AGT-019 B2：ObserveDepth 以字符串进出载荷（与生成 schema 的
        // enum ["Deep","Focused","Normal"] 同源；定向转换器，不泛化到
        // 其他枚举——ElementEpistemic 等既有数字载荷不受影响）。
        options.Converters.Add(new ObserveDepthJsonConverter());
        return options;
    }
}
