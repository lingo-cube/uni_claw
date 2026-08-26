using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.Emulator;

/// <summary>
/// Harness-local canonical payload freeze and digest (task 3.2). The driver
/// transports a directive's canonical JSON shape — mirroring the frozen
/// <c>run.strategy.start</c> wire parse (camelCase enum names, fixed field
/// order, ordinal-sorted set members) — and logs the SHA-256 digest of the
/// exact canonical params object (strategy + device) it transported. Two
/// logically identical directives therefore always share one digest regardless
/// of hash-set member ordering. No new wire contract is introduced.
/// </summary>
public static class StrategyPayloadJson
{
    /// <summary>Freeze one typed directive into its canonical wire-shaped
    /// <c>strategy</c> JSON object (deterministic serialization).</summary>
    public static JsonObject Freeze(StrategyDirective directive)
    {
        ArgumentNullException.ThrowIfNull(directive);

        var objective = new JsonObject { ["kind"] = Camel(directive.Objective.Kind) };
        if (directive.Objective.Criterion is { } criterion)
        {
            objective["criterion"] = new JsonObject
            {
                ["capabilityId"] = criterion.CapabilityId,
                ["criterionId"] = criterion.CriterionId,
                ["version"] = criterion.Version,
            };
        }

        return new JsonObject
        {
            ["strategyId"] = directive.StrategyId,
            ["contractVersion"] = directive.ContractVersion,
            ["objective"] = objective,
            ["scope"] = new JsonObject
            {
                ["applicationIdentity"] = directive.Scope.ApplicationIdentity,
                ["semanticRoot"] = directive.Scope.SemanticRoot,
                ["maximumDepth"] = directive.Scope.MaximumDepth,
            },
            ["exploration"] = Camel(directive.Exploration),
            ["constraints"] = new JsonObject
            {
                ["allowedInteractionCategories"] = SortedStringArray(directive.Constraints.AllowedInteractionCategories.Select(Camel)),
                ["prohibitedEffects"] = SortedStringArray(directive.Constraints.ProhibitedEffects.Select(Camel)),
            },
            ["completion"] = new JsonObject { ["kind"] = Camel(directive.Completion.Kind) },
            ["adaptation"] = new JsonObject
            {
                ["allowedAdaptations"] = SortedStringArray(directive.Adaptation.AllowedAdaptations.Select(Camel)),
            },
        };
    }

    /// <summary>Build the canonical <c>run.strategy.start</c> params object
    /// (strategy + explicit device selection) — exactly what the transport
    /// sends and what the digest is computed over. The strategy is deep-cloned
    /// because a <c>JsonNode</c> can have only one parent: the same frozen
    /// payload may be parameterized (and digested) more than once.</summary>
    public static JsonObject BuildParameters(JsonObject strategy, string device)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        return new JsonObject { ["strategy"] = strategy.DeepClone(), ["device"] = device };
    }

    /// <summary>SHA-256 hex digest of the canonical params JSON.</summary>
    public static string CanonicalDigest(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(parameters.ToJsonString())));
    }

    private static string Camel<T>(T value)
        where T : struct, Enum
        => JsonNamingPolicy.CamelCase.ConvertName(value.ToString()!);

    private static JsonArray SortedStringArray(IEnumerable<string> values)
    {
        var ordered = new JsonArray();
        foreach (var value in values.Order(StringComparer.Ordinal))
            ordered.Add(value);
        return ordered;
    }
}