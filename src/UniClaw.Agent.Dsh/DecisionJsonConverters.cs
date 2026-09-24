using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

/// <summary>Explicit wire discriminator for the closed AgentDecision union. The
/// Kernel records remain the authority; this converter only defines their transport
/// projection.</summary>
public sealed class AgentDecisionJsonConverter : JsonConverter<AgentDecision>
{
    public override AgentDecision Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        EnsureObject(root);
        var kind = RequiredString(root, "kind");
        return kind switch
        {
            "act" => ReadAct(root, options),
            "policy" => ReadPolicy(root, options),
            "noAction" => ReadNoAction(root, options),
            "defer" => ReadDefer(root, options),
            _ => throw new JsonException($"unknown AgentDecision kind '{kind}'"),
        };
    }

    private static AgentDecision ReadAct(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "proposal");
        var decisionId = RequiredString(root, "decisionId");
        ValidateActionProposal(root.GetProperty("proposal"));
        var proposal = DeserializeRequired<AgentActionProposal>(root, "proposal", options);
        if (!string.Equals(decisionId, proposal.DecisionId, StringComparison.Ordinal))
            throw new JsonException("AgentDecision act decisionId does not match proposal");
        return new AgentDecision.Act(proposal);
    }

    private static AgentDecision ReadPolicy(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "proposal");
        ValidatePolicyProposal(root.GetProperty("proposal"));
        return new AgentDecision.Policy(RequiredString(root, "decisionId"),
            DeserializeRequired<PolicyProposal>(root, "proposal", options));
    }

    private static AgentDecision ReadNoAction(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "proposal");
        var decisionId = RequiredString(root, "decisionId");
        ValidateNoActionProposal(root.GetProperty("proposal"));
        var proposal = DeserializeRequired<AgentNoActionProposal>(root, "proposal", options);
        if (!string.Equals(decisionId, proposal.DecisionId, StringComparison.Ordinal))
            throw new JsonException("AgentDecision noAction decisionId does not match proposal");
        return new AgentDecision.NoAction(proposal);
    }

    private static AgentDecision ReadDefer(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "spec");
        ValidateObserveSpec(root.GetProperty("spec"));
        return new AgentDecision.Defer(RequiredString(root, "decisionId"),
            DeserializeRequired<ObserveSpec>(root, "spec", options));
    }

    public override void Write(Utf8JsonWriter writer, AgentDecision value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case AgentDecision.Act act:
                writer.WriteString("kind", "act");
                writer.WritePropertyName("decisionId");
                writer.WriteStringValue(act.Proposal.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, act.Proposal, options);
                break;
            case AgentDecision.Policy policy:
                writer.WriteString("kind", "policy");
                writer.WriteString("decisionId", policy.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, policy.Proposal, options);
                break;
            case AgentDecision.NoAction noAction:
                writer.WriteString("kind", "noAction");
                writer.WriteString("decisionId", noAction.Proposal.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, noAction.Proposal, options);
                break;
            case AgentDecision.Defer defer:
                EnsureNonEmpty(defer.DecisionId, "decisionId");
                writer.WriteString("kind", "defer");
                writer.WriteString("decisionId", defer.DecisionId);
                writer.WritePropertyName("spec");
                JsonSerializer.Serialize(writer, defer.Spec, options);
                break;
            default:
                throw new JsonException($"unsupported AgentDecision type {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }

    private static T DeserializeRequired<T>(JsonElement root, string property,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(property, out var element))
            throw new JsonException($"AgentDecision requires '{property}'");
        return element.Deserialize<T>(options)
            ?? throw new JsonException($"AgentDecision '{property}' is null");
    }

    internal static string RequiredString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
            throw new JsonException($"AgentDecision requires non-empty string '{property}'");
        return element.GetString()!;
    }

    internal static void EnsureObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("protocol value must be an object");
    }

    internal static void EnsureOnly(JsonElement root, params string[] allowed)
    {
        EnsureObject(root);
        var permitted = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!permitted.Contains(property.Name))
                throw new JsonException($"unexpected protocol property '{property.Name}'");
    }

    internal static void EnsureNonEmpty(string? value, string property)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException($"protocol property '{property}' must be non-empty");
    }

    private static void ValidateActionProposal(JsonElement proposal)
    {
        EnsureOnly(proposal, "decisionId", "steps", "justification");
        RequiredString(proposal, "decisionId");
        var steps = RequiredArray(proposal, "steps");
        foreach (var step in steps.EnumerateArray())
        {
            EnsureOnly(step, "targetRole", "targetDescriptor", "effectClass", "desiredState");
            RequiredString(step, "targetRole");
            RequiredString(step, "effectClass");
        }
    }

    private static void ValidateNoActionProposal(JsonElement proposal)
    {
        EnsureOnly(proposal, "decisionId", "justification", "completion");
        RequiredString(proposal, "decisionId");
        RequiredString(proposal, "justification");
        if (proposal.TryGetProperty("completion", out var completion)
            && completion.ValueKind != JsonValueKind.Null)
        {
            EnsureOnly(completion, "basis", "checklist");
            RequiredString(completion, "basis");
            RequiredArray(completion, "checklist");
        }
    }

    private static void ValidateObserveSpec(JsonElement spec)
    {
        EnsureOnly(spec, "subject", "maxRounds");
        var rounds = RequiredArrayOrNumber(spec, "maxRounds");
        if (rounds.ValueKind != JsonValueKind.Number || !rounds.TryGetInt32(out var maxRounds)
            || maxRounds < 1)
            throw new JsonException("observe spec maxRounds must be a positive integer");
    }

    private static void ValidatePolicyProposal(JsonElement proposal)
    {
        EnsureOnly(proposal, "policyId", "match", "actionTemplate", "termination", "guards",
            "maxApplications", "justification");
        RequiredString(proposal, "policyId");
        RequiredArray(proposal, "match");
        var action = RequiredObject(proposal, "actionTemplate");
        EnsureOnly(action, "targetRole", "targetDescriptor", "effectClass", "desiredState");
        RequiredString(action, "targetRole");
        RequiredString(action, "effectClass");
        RequiredArray(proposal, "termination");
        RequiredArray(proposal, "guards");
        var maxApplications = RequiredArrayOrNumber(proposal, "maxApplications");
        if (maxApplications.ValueKind != JsonValueKind.Number
            || !maxApplications.TryGetInt32(out var max) || max < 1)
            throw new JsonException("policy maxApplications must be a positive integer");
    }

    private static JsonElement RequiredObject(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new JsonException($"protocol property '{property}' must be an object");
        return value;
    }

    private static JsonElement RequiredArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new JsonException($"protocol property '{property}' must be an array");
        return value;
    }

    private static JsonElement RequiredArrayOrNumber(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value))
            throw new JsonException($"protocol property '{property}' is required");
        return value;
    }
}

public sealed class PolicyPredicateJsonConverter : JsonConverter<PolicyPredicate>
{
    public override PolicyPredicate Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = AgentDecisionJsonConverter.RequiredString(root, "kind");
        return kind switch
        {
            "claimEquals" => ReadClaimEquals(root),
            "claimInSet" => ReadClaimInSet(root, options),
            _ => throw new JsonException($"unknown PolicyPredicate kind '{kind}'"),
        };
    }

    private static PolicyPredicate ReadClaimEquals(JsonElement root)
    {
        AgentDecisionJsonConverter.EnsureOnly(root, "kind", "subject", "value");
        return new PolicyPredicate.ClaimEquals(
            AgentDecisionJsonConverter.RequiredString(root, "subject"),
            AgentDecisionJsonConverter.RequiredString(root, "value"));
    }

    private static PolicyPredicate ReadClaimInSet(JsonElement root, JsonSerializerOptions options)
    {
        AgentDecisionJsonConverter.EnsureOnly(root, "kind", "subject", "values");
        return new PolicyPredicate.ClaimInSet(
            AgentDecisionJsonConverter.RequiredString(root, "subject"),
            RequiredStringList(root, "values", options));
    }

    private static IReadOnlyList<string> RequiredStringList(JsonElement root, string property,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new JsonException($"policy predicate requires array '{property}'");
        var items = value.Deserialize<IReadOnlyList<string>>(options)
            ?? throw new JsonException($"policy predicate '{property}' is null");
        if (items.Any(string.IsNullOrWhiteSpace))
            throw new JsonException($"policy predicate '{property}' contains an empty value");
        return items;
    }

    public override void Write(Utf8JsonWriter writer, PolicyPredicate value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case PolicyPredicate.ClaimEquals equals:
                writer.WriteString("kind", "claimEquals");
                writer.WriteString("subject", equals.Subject);
                writer.WriteString("value", equals.Value);
                break;
            case PolicyPredicate.ClaimInSet inSet:
                writer.WriteString("kind", "claimInSet");
                writer.WriteString("subject", inSet.Subject);
                writer.WritePropertyName("values");
                JsonSerializer.Serialize(writer, inSet.Values, options);
                break;
            default:
                throw new JsonException($"unsupported PolicyPredicate type {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }
}

public sealed class PolicyGuardJsonConverter : JsonConverter<PolicyGuard>
{
    public override PolicyGuard Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        AgentDecisionJsonConverter.EnsureOnly(root, "kind", "subject", "afterRounds");
        var kind = AgentDecisionJsonConverter.RequiredString(root, "kind");
        return kind switch
        {
            "observationUnchanged" => new PolicyGuard.ObservationUnchanged(
                AgentDecisionJsonConverter.RequiredString(root, "subject"),
                RequiredPositiveInt(root, "afterRounds")),
            _ => throw new JsonException($"unknown PolicyGuard kind '{kind}'"),
        };
    }

    private static int RequiredPositiveInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number)
            || number < 1)
            throw new JsonException($"policy guard requires positive integer '{property}'");
        return number;
    }

    public override void Write(Utf8JsonWriter writer, PolicyGuard value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case PolicyGuard.ObservationUnchanged unchanged:
                writer.WriteString("kind", "observationUnchanged");
                writer.WriteString("subject", unchanged.Subject);
                writer.WriteNumber("afterRounds", unchanged.AfterRounds);
                break;
            default:
                throw new JsonException($"unsupported PolicyGuard type {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }
}

public static class AgentDecisionCorrelation
{
    public static string? TryGetDecisionId(AgentDecision decision) => decision switch
    {
        AgentDecision.Act act => act.Proposal.DecisionId,
        AgentDecision.Policy policy => policy.DecisionId,
        AgentDecision.NoAction noAction => noAction.Proposal.DecisionId,
        AgentDecision.Defer defer => defer.DecisionId,
        _ => null,
    };
}
