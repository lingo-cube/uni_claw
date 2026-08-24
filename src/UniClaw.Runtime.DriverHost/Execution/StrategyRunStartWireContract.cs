using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.DriverHost;

/// <summary>Strict closed wire mapping for the additive run.strategy.start method.</summary>
public static class UniClawStrategyRunStartWire
{
    /// <summary>Parse a closed strategy request. Unknown fields fail as bad_request.</summary>
    public static StrategyRunStartRequest Parse(JsonObject? parameters)
    {
        var root = parameters ?? throw new ArgumentException("missing 'params' object");
        RequireOnly(root, "strategy", "device");
        if (root["strategy"] is not JsonObject strategy)
            throw new ArgumentException("missing 'strategy' object");

        RequireOnly(
            strategy,
            "strategyId",
            "contractVersion",
            "objective",
            "scope",
            "exploration",
            "constraints",
            "completion",
            "adaptation");

        var directive = new StrategyDirective(
            RequireString(strategy, "strategyId"),
            RequireInt(strategy, "contractVersion"),
            ParseObjective(RequireObject(strategy, "objective")),
            ParseScope(RequireObject(strategy, "scope")),
            RequireEnum<ExplorationIntent>(strategy, "exploration"),
            ParseConstraints(RequireObject(strategy, "constraints")),
            ParseCompletion(RequireObject(strategy, "completion")),
            ParseAdaptation(RequireObject(strategy, "adaptation")));

        var deviceText = RequireString(root, "device");
        if (!DeviceSelector.TryParse(deviceText, out var device))
            throw new ArgumentException($"invalid device selector '{deviceText}'");

        return new StrategyRunStartRequest(directive, device);
    }

    /// <summary>Map an admission receipt to an immutable wire copy.</summary>
    public static UniClawStrategyRunAdmissionDto ToDto(StrategyRunAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        return new UniClawStrategyRunAdmissionDto(
            admission.Accepted,
            admission.RunId,
            admission.RunState?.ToString(),
            admission.RejectionCode is null ? null : Camel(admission.RejectionCode.Value.ToString()),
            admission.RejectionReason);
    }

    private static StrategyObjective ParseObjective(JsonObject obj)
    {
        RequireOnly(obj, "kind", "criterion");
        SemanticCriterionRef? criterion = null;
        if (obj["criterion"] is JsonObject criterionObject)
        {
            RequireOnly(criterionObject, "capabilityId", "criterionId", "version");
            criterion = new SemanticCriterionRef(
                RequireString(criterionObject, "capabilityId"),
                RequireString(criterionObject, "criterionId"),
                RequireInt(criterionObject, "version"));
        }
        else if (obj["criterion"] is not null)
        {
            throw new ArgumentException("'criterion' must be an object when present");
        }

        return new StrategyObjective(RequireEnum<StrategyObjectiveKind>(obj, "kind"), criterion);
    }

    private static StrategyScope ParseScope(JsonObject obj)
    {
        RequireOnly(obj, "applicationIdentity", "semanticRoot", "maximumDepth");
        return new StrategyScope(
            RequireString(obj, "applicationIdentity"),
            RequireString(obj, "semanticRoot"),
            RequireInt(obj, "maximumDepth"));
    }

    private static StrategyConstraintSet ParseConstraints(JsonObject obj)
    {
        RequireOnly(obj, "allowedInteractionCategories", "prohibitedEffects");
        return new StrategyConstraintSet(
            ParseEnumSet<TypeLevelElementCategory>(obj, "allowedInteractionCategories", requireNonEmpty: true),
            ParseEnumSet<StrategyProhibitedEffect>(obj, "prohibitedEffects", requireNonEmpty: false));
    }

    private static StrategyCompletionCriteria ParseCompletion(JsonObject obj)
    {
        RequireOnly(obj, "kind");
        return new StrategyCompletionCriteria(RequireEnum<StrategyCompletionKind>(obj, "kind"));
    }

    private static StrategyAdaptationBoundary ParseAdaptation(JsonObject obj)
    {
        RequireOnly(obj, "allowedAdaptations");
        return new StrategyAdaptationBoundary(
            ParseEnumSet<StrategyAdaptationKind>(obj, "allowedAdaptations", requireNonEmpty: false));
    }

    private static JsonObject RequireObject(JsonObject obj, string key)
        => obj[key] as JsonObject ?? throw new ArgumentException($"missing '{key}' object");

    private static string RequireString(JsonObject obj, string key)
    {
        if (obj[key] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
            throw new ArgumentException($"missing or non-string '{key}'");
        var text = value.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException($"'{key}' must not be empty");
        return text;
    }

    private static int RequireInt(JsonObject obj, string key)
    {
        if (obj[key] is not JsonValue value || value.GetValueKind() != JsonValueKind.Number
            || !value.TryGetValue<int>(out var result))
        {
            throw new ArgumentException($"missing or non-integer '{key}'");
        }
        return result;
    }

    private static T RequireEnum<T>(JsonObject obj, string key) where T : struct, Enum
    {
        var text = RequireString(obj, key);
        if (!Enum.TryParse<T>(text, ignoreCase: true, out var value) || !Enum.IsDefined(value))
            throw new ArgumentException($"unsupported '{key}' value '{text}'");
        return value;
    }

    private static ImmutableHashSet<T> ParseEnumSet<T>(JsonObject obj, string key, bool requireNonEmpty)
        where T : struct, Enum
    {
        if (obj[key] is not JsonArray array)
            throw new ArgumentException($"missing '{key}' array");
        var builder = ImmutableHashSet.CreateBuilder<T>();
        foreach (var item in array)
        {
            if (item is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
                throw new ArgumentException($"each '{key}' entry must be a string");
            var text = value.GetValue<string>();
            if (!Enum.TryParse<T>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ArgumentException($"unsupported '{key}' value '{text}'");
            builder.Add(parsed);
        }
        if (requireNonEmpty && builder.Count == 0)
            throw new ArgumentException($"'{key}' must not be empty");
        return builder.ToImmutable();
    }

    private static void RequireOnly(JsonObject obj, params string[] allowed)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var unknown = obj.Select(property => property.Key).FirstOrDefault(key => !allowedSet.Contains(key));
        if (unknown is not null)
            throw new ArgumentException($"unsupported strategy field '{unknown}'");
    }

    private static string Camel(string value) => JsonNamingPolicy.CamelCase.ConvertName(value);
}

/// <summary>Wire receipt for one strategy admission.</summary>
public sealed record UniClawStrategyRunAdmissionDto(
    bool Accepted,
    string? RunId,
    string? RunState,
    string? RejectionCode,
    string? RejectionReason);
