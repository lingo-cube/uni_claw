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
            "plan" => ReadPlan(root, options),
            "continuePlan" => ReadContinuation(root, options),
            "blocked" => ReadBlocked(root, options),
            "noAction" => ReadNoAction(root, options),
            "defer" => ReadDefer(root, options),
            "skip" => ReadSkip(root, options),
            _ => throw new JsonException($"unknown AgentDecision kind '{kind}'"),
        };
    }

    private static AgentDecision ReadAct(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "proposal", "reviewResponse");
        var decisionId = RequiredString(root, "decisionId");
        ValidateActionProposal(root.GetProperty("proposal"));
        ValidateReviewResponse(root);
        RejectLegacyRenewalWithReview(root, root.GetProperty("proposal"));
        var proposal = DeserializeRequired<AgentActionProposal>(root, "proposal", options);
        if (!string.Equals(decisionId, proposal.DecisionId, StringComparison.Ordinal))
            throw new JsonException("AgentDecision act decisionId does not match proposal");
        return new AgentDecision.Act(proposal, ReadReviewResponse(root, options));
    }

    private static AgentDecision ReadPolicy(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "proposal", "reviewResponse");
        ValidatePolicyProposal(root.GetProperty("proposal"));
        ValidateReviewResponse(root);
        return new AgentDecision.Policy(RequiredString(root, "decisionId"),
            DeserializeRequired<PolicyProposal>(root, "proposal", options), ReadReviewResponse(root, options));
    }

    private static AgentDecision ReadPlan(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "proposal", "reviewResponse");
        var proposal = root.GetProperty("proposal");
        EnsureOnly(proposal, "items", "justification", "planId", "version", "stageExpectation", "maxLocalOperations");
        RequiredString(proposal, "planId");
        PositiveInt(proposal, "version");
        PositiveInt(proposal, "maxLocalOperations");
        var items = RequiredArray(proposal, "items");
        if (items.GetArrayLength() is < 1 or > 16) throw new JsonException("plan items must contain 1..16 nodes");
        ValidateReviewResponse(root);
        return new AgentDecision.Plan(RequiredString(root, "decisionId"),
            DeserializeRequired<AgentPlanProposal>(root, "proposal", options), ReadReviewResponse(root, options));
    }

    private static AgentDecision ReadContinuation(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "position", "reviewResponse");
        var position = root.GetProperty("position");
        EnsureOnly(position, "planId", "version", "itemIndex", "iteration", "bodyIndex", "candidateKey", "workUsed", "controlPath");
        RequiredString(position, "planId");
        PositiveInt(position, "version");
        foreach (var name in new[] { "itemIndex", "iteration", "bodyIndex", "workUsed" })
            if (!position.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < 0)
                throw new JsonException($"plan position requires non-negative integer '{name}'");
        ValidateReviewResponse(root);
        return new AgentDecision.ContinuePlan(RequiredString(root, "decisionId"),
            DeserializeRequired<PlanExecutionPosition>(root, "position", options), ReadReviewResponse(root, options));
    }

    private static AgentDecision ReadBlocked(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "reason", "reviewResponse");
        ValidateReviewResponse(root);
        return new AgentDecision.Blocked(RequiredString(root, "decisionId"), RequiredString(root, "reason"), ReadReviewResponse(root, options));
    }

    internal static int PositiveInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number) || number < 1)
            throw new JsonException($"protocol requires positive integer '{property}'");
        return number;
    }

    private static AgentDecision ReadNoAction(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "proposal", "reviewResponse");
        var decisionId = RequiredString(root, "decisionId");
        ValidateNoActionProposal(root.GetProperty("proposal"));
        ValidateReviewResponse(root);
        RejectLegacyRenewalWithReview(root, root.GetProperty("proposal"));
        var proposal = DeserializeRequired<AgentNoActionProposal>(root, "proposal", options);
        if (!string.Equals(decisionId, proposal.DecisionId, StringComparison.Ordinal))
            throw new JsonException("AgentDecision noAction decisionId does not match proposal");
        return new AgentDecision.NoAction(proposal, ReadReviewResponse(root, options));
    }

    private static AgentDecision ReadDefer(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "spec", "renewal", "reviewResponse");
        ValidateObserveSpec(root.GetProperty("spec"));
        ValidateReviewResponse(root);
        RejectLegacyRenewalWithReview(root);
        return new AgentDecision.Defer(RequiredString(root, "decisionId"),
            DeserializeRequired<ObserveSpec>(root, "spec", options),
            ReadRenewal(root, options), ReadReviewResponse(root, options));
    }

    private static AgentDecision ReadSkip(JsonElement root, JsonSerializerOptions options)
    {
        EnsureOnly(root, "kind", "decisionId", "target", "reason", "evidenceRefs", "reviewResponse");
        ValidateReviewResponse(root);
        var decisionId = RequiredString(root, "decisionId");
        var target = RequiredString(root, "target");
        var reason = RequiredString(root, "reason");
        var evidenceRefs = RequiredArray(root, "evidenceRefs")
            .EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .ToArray();
        if (evidenceRefs.Length == 0 || evidenceRefs.Any(string.IsNullOrWhiteSpace))
            throw new JsonException("skip evidenceRefs must contain at least one non-empty string");
        return new AgentDecision.Skip(decisionId, target, reason, evidenceRefs!, ReadReviewResponse(root, options));
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
                WriteReviewResponse(writer, act.ReviewResponse, options);
                break;
            case AgentDecision.Policy policy:
                writer.WriteString("kind", "policy");
                writer.WriteString("decisionId", policy.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, policy.Proposal, options);
                WriteReviewResponse(writer, policy.ReviewResponse, options);
                break;
            case AgentDecision.Plan plan:
                EnsureNonEmpty(plan.Proposal.PlanId, "planId");
                writer.WriteString("kind", "plan");
                writer.WriteString("decisionId", plan.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, plan.Proposal, options);
                WriteReviewResponse(writer, plan.ReviewResponse, options);
                break;
            case AgentDecision.ContinuePlan resume:
                writer.WriteString("kind", "continuePlan");
                writer.WriteString("decisionId", resume.DecisionId);
                writer.WritePropertyName("position");
                JsonSerializer.Serialize(writer, resume.Position, options);
                WriteReviewResponse(writer, resume.ReviewResponse, options);
                break;
            case AgentDecision.Blocked blocked:
                writer.WriteString("kind", "blocked");
                writer.WriteString("decisionId", blocked.DecisionId);
                writer.WriteString("reason", blocked.Reason);
                WriteReviewResponse(writer, blocked.ReviewResponse, options);
                break;
            case AgentDecision.NoAction noAction:
                writer.WriteString("kind", "noAction");
                writer.WriteString("decisionId", noAction.Proposal.DecisionId);
                writer.WritePropertyName("proposal");
                JsonSerializer.Serialize(writer, noAction.Proposal, options);
                WriteReviewResponse(writer, noAction.ReviewResponse, options);
                break;
            case AgentDecision.Defer defer:
                EnsureNonEmpty(defer.DecisionId, "decisionId");
                writer.WriteString("kind", "defer");
                writer.WriteString("decisionId", defer.DecisionId);
                writer.WritePropertyName("spec");
                JsonSerializer.Serialize(writer, defer.Spec, options);
                if (defer.Renewal is not null)
                {
                    writer.WritePropertyName("renewal");
                    JsonSerializer.Serialize(writer, defer.Renewal, options);
                }
                WriteReviewResponse(writer, defer.ReviewResponse, options);
                break;
            case AgentDecision.Skip skip:
                EnsureNonEmpty(skip.DecisionId, "decisionId");
                EnsureNonEmpty(skip.Target, "target");
                EnsureNonEmpty(skip.Reason, "reason");
                if (skip.EvidenceRefs is null || skip.EvidenceRefs.Count == 0
                    || skip.EvidenceRefs.Any(string.IsNullOrWhiteSpace))
                    throw new JsonException("skip evidenceRefs must contain at least one non-empty string");
                writer.WriteString("kind", "skip");
                writer.WriteString("decisionId", skip.DecisionId);
                writer.WriteString("target", skip.Target);
                writer.WriteString("reason", skip.Reason);
                writer.WritePropertyName("evidenceRefs");
                JsonSerializer.Serialize(writer, skip.EvidenceRefs, options);
                WriteReviewResponse(writer, skip.ReviewResponse, options);
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
        EnsureOnly(proposal, "decisionId", "steps", "justification", "renewal");
        ValidateRenewal(proposal);
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
        EnsureOnly(proposal, "decisionId", "justification", "completion", "renewal");
        ValidateRenewal(proposal);
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

    private static void ValidateRenewal(JsonElement owner)
    {
        if (owner.TryGetProperty("renewal", out var renewal) && renewal.ValueKind != JsonValueKind.Null)
        {
            EnsureOnly(renewal, "additionalSteps", "additionalRounds", "justification");
            foreach (var name in new[] { "additionalSteps", "additionalRounds" })
                if (!renewal.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number
                    || !value.TryGetInt32(out _))
                    throw new JsonException($"renewal requires integer '{name}'");
        }
    }

    private static BudgetRenewalRequest? ReadRenewal(JsonElement owner, JsonSerializerOptions options)
    {
        ValidateRenewal(owner);
        return owner.TryGetProperty("renewal", out var renewal) && renewal.ValueKind != JsonValueKind.Null
            ? renewal.Deserialize<BudgetRenewalRequest>(options)
            : null;
    }

    private static void ValidateReviewResponse(JsonElement owner)
    {
        if (!owner.TryGetProperty("reviewResponse", out var response) || response.ValueKind == JsonValueKind.Null)
            return;
        EnsureOnly(response, "reviewId", "outcome", "assessment", "renewal");
        RequiredString(response, "reviewId");
        var outcome = RequiredString(response, "outcome");
        if (outcome is not ("ContinueWithoutRenewal" or "RequestRenewal" or "Replan" or "SubmitCompletion" or "CollectEvidence"))
            throw new JsonException($"reviewResponse outcome '{outcome}' is not supported");
        if (!response.TryGetProperty("assessment", out var assessment) || assessment.ValueKind == JsonValueKind.Null)
            throw new JsonException("reviewResponse requires assessment for every explicit Runtime review");
        var hasRenewal = response.TryGetProperty("renewal", out var renewal) && renewal.ValueKind != JsonValueKind.Null;
        if (outcome == "RequestRenewal" && !hasRenewal)
            throw new JsonException("reviewResponse RequestRenewal requires renewal");
        if (outcome != "RequestRenewal" && hasRenewal)
            throw new JsonException("reviewResponse renewal is only valid with RequestRenewal");
        if (hasRenewal)
            ValidateRenewalValue(renewal);
        {
            EnsureOnly(assessment, "overviewId", "judgment", "nextDirection", "nextSegmentExpectation",
                "expectedObservableEvidence", "evidenceRefs", "declinedDimensions", "retriggerCondition",
                "deviation", "rationale", "nextItemKey");
            RequiredString(assessment, "overviewId");
            RequiredString(assessment, "judgment");
            RequiredArray(assessment, "expectedObservableEvidence");
            RequiredArray(assessment, "evidenceRefs");
            foreach (var field in new[] { "expectedObservableEvidence", "evidenceRefs", "declinedDimensions" })
                if (assessment.TryGetProperty(field, out var list) && list.ValueKind != JsonValueKind.Null)
                {
                    if (list.ValueKind != JsonValueKind.Array
                        || list.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                        throw new JsonException($"reviewResponse assessment '{field}' must be an array of strings");
                }
            foreach (var field in new[] { "nextDirection", "nextSegmentExpectation", "retriggerCondition", "deviation", "rationale", "nextItemKey" })
                if (assessment.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null
                    && value.ValueKind != JsonValueKind.String)
                    throw new JsonException($"reviewResponse assessment '{field}' must be a string or null");
        }
    }

    private static void RejectLegacyRenewalWithReview(JsonElement decision, JsonElement? proposal = null)
    {
        var hasReview = decision.TryGetProperty("reviewResponse", out var review)
            && review.ValueKind != JsonValueKind.Null;
        var hasProposalRenewal = proposal is { } value
            && value.TryGetProperty("renewal", out var proposalRenewal)
            && proposalRenewal.ValueKind != JsonValueKind.Null;
        var hasTopLevelRenewal = decision.TryGetProperty("renewal", out var renewal)
            && renewal.ValueKind != JsonValueKind.Null;
        if (hasReview && (hasProposalRenewal || hasTopLevelRenewal))
            throw new JsonException("reviewResponse cannot be combined with a legacy renewal field");
    }

    private static void ValidateRenewalValue(JsonElement renewal)
    {
        EnsureOnly(renewal, "additionalSteps", "additionalRounds", "justification");
        foreach (var name in new[] { "additionalSteps", "additionalRounds" })
            if (!renewal.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number
                || !value.TryGetInt32(out _))
                throw new JsonException($"renewal requires integer '{name}'");
        if (renewal.TryGetProperty("justification", out var justification)
            && justification.ValueKind != JsonValueKind.Null && justification.ValueKind != JsonValueKind.String)
            throw new JsonException("renewal justification must be a string or null");
    }

    private static BudgetReviewResponse? ReadReviewResponse(JsonElement owner, JsonSerializerOptions options) =>
        owner.TryGetProperty("reviewResponse", out var response) && response.ValueKind != JsonValueKind.Null
            ? response.Deserialize<BudgetReviewResponse>(options)
            : null;

    private static void WriteReviewResponse(Utf8JsonWriter writer, BudgetReviewResponse? response,
        JsonSerializerOptions options)
    {
        if (response is null) return;
        writer.WritePropertyName("reviewResponse");
        JsonSerializer.Serialize(writer, response, options);
    }

    private static void ValidateObserveSpec(JsonElement spec)
    {
        // AGT-019 B2：参数化观察（深度档/目的）入严格词表——与生成 schema
        // 同源（schemas/agt-002 单源生成，codec 不得落后于 schema）。
        EnsureOnly(spec, "subject", "maxRounds", "depth", "purpose");
        var rounds = RequiredArrayOrNumber(spec, "maxRounds");
        if (rounds.ValueKind != JsonValueKind.Number || !rounds.TryGetInt32(out var maxRounds)
            || maxRounds < 1)
            throw new JsonException("observe spec maxRounds must be a positive integer");
        if (spec.TryGetProperty("depth", out var depth) && depth.ValueKind != JsonValueKind.Null)
        {
            if (depth.ValueKind != JsonValueKind.String
                || depth.GetString() is not ("Normal" or "Focused" or "Deep"))
                throw new JsonException("observe spec depth must be Normal, Focused, or Deep");
        }
        if (spec.TryGetProperty("purpose", out var purpose) && purpose.ValueKind != JsonValueKind.Null)
        {
            var text = purpose.ValueKind == JsonValueKind.String ? purpose.GetString() : null;
            if (string.IsNullOrWhiteSpace(text) || text!.Length > 256)
                throw new JsonException(
                    "observe spec purpose must be a non-empty string of at most 256 characters");
        }
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
        AgentDecision.Plan plan => plan.DecisionId,
        AgentDecision.ContinuePlan resume => resume.DecisionId,
        AgentDecision.Blocked blocked => blocked.DecisionId,
        AgentDecision.NoAction noAction => noAction.Proposal.DecisionId,
        AgentDecision.Defer defer => defer.DecisionId,
        AgentDecision.Skip skip => skip.DecisionId,
        _ => null,
    };
}

/// <summary>Closed wire projection of the Kernel-owned PlanItem vocabulary.</summary>
internal sealed class PlanItemJsonConverter : JsonConverter<PlanItem>
{
    public override PlanItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ReadItem(document.RootElement, options, depth: 0);
    }

    private static PlanItem ReadItem(JsonElement root, JsonSerializerOptions options, int depth)
    {
        if (depth > 8) throw new JsonException("plan block depth exceeds 8");
        AgentDecisionJsonConverter.EnsureObject(root);
        var kind = AgentDecisionJsonConverter.RequiredString(root, "kind");
        switch (kind)
        {
            case "act":
                AgentDecisionJsonConverter.EnsureOnly(root, "kind", "targetRole", "targetDescriptor", "effectClass", "desiredState");
                return new PlanItem.ActItem(AgentDecisionJsonConverter.RequiredString(root, "targetRole"),
                    OptionalString(root, "targetDescriptor"), AgentDecisionJsonConverter.RequiredString(root, "effectClass"),
                    OptionalString(root, "desiredState"));
            case "observe":
                AgentDecisionJsonConverter.EnsureOnly(root, "kind", "subject");
                return new PlanItem.ObserveItem(OptionalString(root, "subject"));
            case "control":
                AgentDecisionJsonConverter.EnsureOnly(root, "kind", "control", "reason");
                var name = AgentDecisionJsonConverter.RequiredString(root, "control");
                if (!Enum.TryParse<PlanControlKind>(name, out var control) || !Enum.GetNames<PlanControlKind>().Contains(name, StringComparer.Ordinal))
                    throw new JsonException("unknown plan control");
                return new PlanItem.ControlItem(control, OptionalString(root, "reason"));
            case "checkpoint":
                AgentDecisionJsonConverter.EnsureOnly(root, "kind", "reason");
                return new PlanItem.CheckpointItem(OptionalString(root, "reason"));
            case "forEach":
                AgentDecisionJsonConverter.EnsureOnly(root, "kind", "source", "body", "maxIterations", "continueOnBlocked");
                var source = AgentDecisionJsonConverter.RequiredString(root, "source");
                var max = AgentDecisionJsonConverter.PositiveInt(root, "maxIterations");
                if (!root.TryGetProperty("body", out var items) || items.ValueKind != JsonValueKind.Array
                    || items.GetArrayLength() is < 1 or > 16)
                    throw new JsonException("forEach body must contain 1..16 nodes");
                var proceed = false;
                if (root.TryGetProperty("continueOnBlocked", out var value))
                {
                    if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new JsonException("continueOnBlocked must be boolean");
                    proceed = value.GetBoolean();
                }
                return new PlanItem.ForEachItem(source, items.EnumerateArray().Select(item => ReadItem(item, options, depth + 1)).ToArray(), max, proceed);
            case "if":
                AgentDecisionJsonConverter.EnsureOnly(root, "kind", "condition", "then", "else");
                return new PlanItem.IfItem(AgentDecisionJsonConverter.RequiredString(root, "condition"),
                    ReadBlock(root, "then", options, depth, allowEmpty: true),
                    ReadBlock(root, "else", options, depth, allowEmpty: true));
            case "repeat":
                AgentDecisionJsonConverter.EnsureOnly(root, "kind", "body", "maxIterations");
                return new PlanItem.RepeatItem(ReadBlock(root, "body", options, depth, allowEmpty: false),
                    AgentDecisionJsonConverter.PositiveInt(root, "maxIterations"));
            case "traverse":
                return ReadTraverse(root, options);
            default: throw new JsonException($"unsupported plan node '{kind}' at this position");
        }
    }

    private static PlanItem.TraverseItem ReadTraverse(JsonElement root, JsonSerializerOptions options)
    {
        AgentDecisionJsonConverter.EnsureOnly(root, "kind", "source", "rules", "maxDepth", "returnStep");
        var source = AgentDecisionJsonConverter.RequiredString(root, "source");
        if (!root.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array
            || rules.GetArrayLength() is < 1 or > 16)
            throw new JsonException("traverse rules must contain 1..16 entries");
        var maxDepth = ReadBoundedInt(root, "maxDepth", 0, 16);
        var returnStep = ReadStep(root, "returnStep", options);
        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        var parsedRules = new List<TraversalRule>();
        foreach (var rule in rules.EnumerateArray())
        {
            AgentDecisionJsonConverter.EnsureOnly(rule, "ruleId", "condition", "operation");
            var ruleId = AgentDecisionJsonConverter.RequiredString(rule, "ruleId");
            var condition = AgentDecisionJsonConverter.RequiredString(rule, "condition");
            if (!ruleIds.Add(ruleId)) throw new JsonException("traverse ruleId values must be unique");
            if (!rule.TryGetProperty("operation", out var operation) || operation.ValueKind != JsonValueKind.Object)
                throw new JsonException("traverse rule requires an operation object");
            var parsed = ReadOperation(operation, options);
            if (!operationIds.Add(parsed.OperationId)) throw new JsonException("traverse operationId values must be unique");
            parsedRules.Add(new TraversalRule(ruleId, condition, parsed));
        }
        var traverse = new PlanItem.TraverseItem(source, parsedRules, maxDepth, returnStep);
        ValidateTraverse(traverse);
        return traverse;
    }

    internal static TraversalOperation ReadOperation(JsonElement root, JsonSerializerOptions options)
    {
        AgentDecisionJsonConverter.EnsureOnly(root, "operationId", "kind", "step", "before", "afterVerified");
        var operationId = AgentDecisionJsonConverter.RequiredString(root, "operationId");
        if (string.Equals(operationId, "$return", StringComparison.Ordinal))
            throw new JsonException("traverse operationId '$return' is reserved for the Runtime return operation");
        var kindName = AgentDecisionJsonConverter.RequiredString(root, "kind");
        if (!Enum.TryParse<TraversalOperationKind>(kindName, ignoreCase: false, out var kind)
            || !Enum.GetNames<TraversalOperationKind>().Contains(kindName, StringComparer.Ordinal))
            throw new JsonException("traverse operation kind must be ReadOnly or Navigate");

        AgentActionStep? step = null;
        if (kind == TraversalOperationKind.ReadOnly)
        {
            if (!root.TryGetProperty("step", out var stepValue) || stepValue.ValueKind != JsonValueKind.Null)
                throw new JsonException("ReadOnly operation step must be explicitly null");
        }
        else
        {
            step = ReadStep(root, "step", options);
        }

        var before = ReadHandlers(root, "before", options);
        var after = ReadHandlers(root, "afterVerified", options);
        return new TraversalOperation(operationId, kind, step, before, after);
    }

    private static IReadOnlyList<PlanLanguageHandler> ReadHandlers(JsonElement root, string property,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(property, out var handlers) || handlers.ValueKind != JsonValueKind.Array
            || handlers.GetArrayLength() > 8)
            throw new JsonException($"traverse operation requires '{property}' array with at most 8 entries");
        var result = new List<PlanLanguageHandler>();
        foreach (var handler in handlers.EnumerateArray())
        {
            AgentDecisionJsonConverter.EnsureOnly(handler, "handlerId", "capabilityId", "protocolVersion",
                "expectedLanguage", "required", "requirePass");
            var handlerId = AgentDecisionJsonConverter.RequiredString(handler, "handlerId");
            var capabilityId = AgentDecisionJsonConverter.RequiredString(handler, "capabilityId");
            var protocolVersion = AgentDecisionJsonConverter.RequiredString(handler, "protocolVersion");
            var language = AgentDecisionJsonConverter.RequiredString(handler, "expectedLanguage");
            if (!string.Equals(capabilityId, "runtime.language-inspector", StringComparison.Ordinal)
                || !string.Equals(protocolVersion, "1.0", StringComparison.Ordinal))
                throw new JsonException("traverse handler binding must use runtime.language-inspector / Language Inspection@1.0");
            if (!handler.TryGetProperty("required", out var required) || required.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !handler.TryGetProperty("requirePass", out var requirePass) || requirePass.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new JsonException("traverse handler required and requirePass must be booleans");
            result.Add(new PlanLanguageHandler(handlerId, capabilityId, protocolVersion, language,
                required.GetBoolean(), requirePass.GetBoolean()));
        }
        return result;
    }

    private static AgentActionStep ReadStep(JsonElement root, string property, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(property, out var step) || step.ValueKind != JsonValueKind.Object)
            throw new JsonException($"traverse requires '{property}' step object");
        AgentDecisionJsonConverter.EnsureOnly(step, "targetRole", "targetDescriptor", "effectClass", "desiredState");
        AgentDecisionJsonConverter.RequiredString(step, "targetRole");
        AgentDecisionJsonConverter.RequiredString(step, "effectClass");
        return JsonSerializer.Deserialize<AgentActionStep>(step.GetRawText(), options)
            ?? throw new JsonException($"traverse '{property}' step is invalid");
    }

    private static int ReadBoundedInt(JsonElement root, string property, int minimum, int maximum)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number) || number < minimum || number > maximum)
            throw new JsonException($"traverse '{property}' must be an integer in {minimum}..{maximum}");
        return number;
    }

    private static IReadOnlyList<PlanItem> ReadBlock(JsonElement root, string name, JsonSerializerOptions options, int depth, bool allowEmpty)
    {
        if (!root.TryGetProperty(name, out var items) || items.ValueKind != JsonValueKind.Array
            || items.GetArrayLength() > 16 || (!allowEmpty && items.GetArrayLength() == 0))
            throw new JsonException($"invalid plan block '{name}'");
        return items.EnumerateArray().Select(item => ReadItem(item, options, depth + 1)).ToArray();
    }

    private static string? OptionalString(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new JsonException($"'{name}' must be string or null");
        return value.GetString();
    }

    public override void Write(Utf8JsonWriter writer, PlanItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case PlanItem.ActItem act:
                writer.WriteString("kind", "act"); writer.WriteString("targetRole", act.TargetRole);
                if (act.TargetDescriptor is not null) writer.WriteString("targetDescriptor", act.TargetDescriptor);
                writer.WriteString("effectClass", act.EffectClass);
                if (act.DesiredState is not null) writer.WriteString("desiredState", act.DesiredState);
                break;
            case PlanItem.ObserveItem observe:
                writer.WriteString("kind", "observe");
                if (observe.Subject is not null) writer.WriteString("subject", observe.Subject);
                break;
            case PlanItem.ControlItem control:
                writer.WriteString("kind", "control"); writer.WriteString("control", control.Kind.ToString());
                if (control.Reason is not null) writer.WriteString("reason", control.Reason);
                break;
            case PlanItem.CheckpointItem checkpoint:
                writer.WriteString("kind", "checkpoint");
                if (checkpoint.Reason is not null) writer.WriteString("reason", checkpoint.Reason);
                break;
            case PlanItem.ForEachItem loop:
                writer.WriteString("kind", "forEach"); writer.WriteString("source", loop.Source);
                writer.WritePropertyName("body"); JsonSerializer.Serialize(writer, loop.Body, options);
                writer.WriteNumber("maxIterations", loop.MaxIterations); writer.WriteBoolean("continueOnBlocked", loop.ContinueOnBlocked);
                break;
            case PlanItem.IfItem branch:
                writer.WriteString("kind", "if"); writer.WriteString("condition", branch.Condition);
                writer.WritePropertyName("then"); JsonSerializer.Serialize(writer, branch.Then, options);
                writer.WritePropertyName("else"); JsonSerializer.Serialize(writer, branch.Else, options);
                break;
            case PlanItem.RepeatItem repeat:
                writer.WriteString("kind", "repeat"); writer.WriteNumber("maxIterations", repeat.MaxIterations);
                writer.WritePropertyName("body"); JsonSerializer.Serialize(writer, repeat.Body, options);
                break;
            case PlanItem.TraverseItem traverse:
                ValidateTraverse(traverse);
                writer.WriteString("kind", "traverse");
                writer.WriteString("source", traverse.Source);
                writer.WritePropertyName("rules"); JsonSerializer.Serialize(writer, traverse.Rules, options);
                writer.WriteNumber("maxDepth", traverse.MaxDepth);
                writer.WritePropertyName("returnStep"); JsonSerializer.Serialize(writer, traverse.ReturnStep, options);
                break;
            default: throw new JsonException("unsupported PlanItem type");
        }
        writer.WriteEndObject();
    }

    private static void ValidateTraverse(PlanItem.TraverseItem traverse)
    {
        AgentDecisionJsonConverter.EnsureNonEmpty(traverse.Source, "source");
        if (traverse.Rules is null || traverse.Rules.Count is < 1 or > 16)
            throw new JsonException("traverse rules must contain 1..16 entries");
        if (traverse.MaxDepth is < 0 or > 16) throw new JsonException("traverse maxDepth must be in 0..16");
        if (traverse.ReturnStep is null) throw new JsonException("traverse returnStep is required");
        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        var handlerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in traverse.Rules)
        {
            if (rule is null || rule.Operation is null) throw new JsonException("traverse rule and operation are required");
            AgentDecisionJsonConverter.EnsureNonEmpty(rule.RuleId, "ruleId");
            AgentDecisionJsonConverter.EnsureNonEmpty(rule.Condition, "condition");
            if (!ruleIds.Add(rule.RuleId)) throw new JsonException("traverse ruleId values must be unique");
            AgentDecisionJsonConverter.EnsureNonEmpty(rule.Operation.OperationId, "operationId");
            if (!operationIds.Add(rule.Operation.OperationId)) throw new JsonException("traverse operationId values must be unique");
            if (string.Equals(rule.Operation.OperationId, "$return", StringComparison.Ordinal))
                throw new JsonException("traverse operationId '$return' is reserved for the Runtime return operation");
            if (rule.Operation.Kind == TraversalOperationKind.ReadOnly)
            {
                if (rule.Operation.Step is not null) throw new JsonException("ReadOnly operation step must be null");
            }
            else if (rule.Operation.Kind == TraversalOperationKind.Navigate)
            {
                if (rule.Operation.Step is null) throw new JsonException("Navigate operation step is required");
                ValidateStep(rule.Operation.Step, "step");
            }
            else throw new JsonException("traverse operation kind must be ReadOnly or Navigate");
            ValidateHandlers(rule.Operation.Before, handlerIds);
            ValidateHandlers(rule.Operation.AfterVerified, handlerIds);
        }
        ValidateStep(traverse.ReturnStep, "returnStep");
    }

    private static void ValidateHandlers(IReadOnlyList<PlanLanguageHandler>? handlers,
        HashSet<string> handlerIds)
    {
        if (handlers is null || handlers.Count > 8) throw new JsonException("traverse handler list must contain at most 8 entries");
        foreach (var handler in handlers)
        {
            if (handler is null) throw new JsonException("traverse handler binding is required");
            AgentDecisionJsonConverter.EnsureNonEmpty(handler.HandlerId, "handlerId");
            if (!handlerIds.Add(handler.HandlerId)) throw new JsonException("traverse handlerId values must be unique across the traversal");
            AgentDecisionJsonConverter.EnsureNonEmpty(handler.ExpectedLanguage, "expectedLanguage");
            if (!string.Equals(handler.CapabilityId, "runtime.language-inspector", StringComparison.Ordinal)
                || !string.Equals(handler.ProtocolVersion, "1.0", StringComparison.Ordinal))
                throw new JsonException("traverse handler binding must use runtime.language-inspector / Language Inspection@1.0");
        }
    }

    private static void ValidateStep(AgentActionStep step, string name)
    {
        AgentDecisionJsonConverter.EnsureNonEmpty(step.TargetRole, $"{name}.targetRole");
        AgentDecisionJsonConverter.EnsureNonEmpty(step.EffectClass, $"{name}.effectClass");
    }
}

/// <summary>Preserves the explicit null step required by ReadOnly on the wire.</summary>
internal sealed class TraversalOperationJsonConverter : JsonConverter<TraversalOperation>
{
    public override TraversalOperation Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return PlanItemJsonConverter.ReadOperation(document.RootElement, options);
    }

    public override void Write(Utf8JsonWriter writer, TraversalOperation value,
        JsonSerializerOptions options)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.OperationId))
            throw new JsonException("traverse operationId is required");
        if (string.Equals(value.OperationId, "$return", StringComparison.Ordinal))
            throw new JsonException("traverse operationId '$return' is reserved for the Runtime return operation");
        if (value.Kind == TraversalOperationKind.ReadOnly && value.Step is not null)
            throw new JsonException("ReadOnly operation step must be null");
        if (value.Kind == TraversalOperationKind.Navigate && value.Step is null)
            throw new JsonException("Navigate operation step is required");
        if (value.Kind is not (TraversalOperationKind.ReadOnly or TraversalOperationKind.Navigate))
            throw new JsonException("traverse operation kind must be ReadOnly or Navigate");
        writer.WriteStartObject();
        writer.WriteString("operationId", value.OperationId);
        writer.WriteString("kind", value.Kind.ToString());
        writer.WritePropertyName("step");
        if (value.Step is null) writer.WriteNullValue();
        else JsonSerializer.Serialize(writer, value.Step, options);
        writer.WritePropertyName("before"); JsonSerializer.Serialize(writer, value.Before, options);
        writer.WritePropertyName("afterVerified"); JsonSerializer.Serialize(writer, value.AfterVerified, options);
        writer.WriteEndObject();
    }
}
