using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// Deterministic human-readable rendering of a <see cref="PlanningRound"/> for
/// campaign evidence artifacts (spec "PlanDelta contract": every delta needs
/// evidence links; "Human-readable persisted asset" determinism). JSON is built
/// via explicit <see cref="JsonNode"/> construction with a fixed property order
/// and ordinal-sorted arrays — never reflection over delegate-bearing types;
/// the StrategyDirective is rendered as its plain frozen fields (objective
/// kind/criterion, scope, exploration, constraints, completion, adaptation).
/// Same round ⇒ byte-identical rendering. Validation artifact; no field ever
/// enters the wire.
/// </summary>
public static class PlanningRoundRecord
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Deterministic indented JSON of one planning round.</summary>
    public static string ToJson(PlanningRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var root = new JsonObject
        {
            ["roundIndex"] = round.RoundIndex,
            ["previousPlan"] = DirectiveNode(round.PreviousPlan),
            ["observedResult"] = new JsonObject
            {
                ["runId"] = round.ObservedResult.RunId,
                ["strategyId"] = round.ObservedResult.StrategyId,
                ["terminalState"] = round.ObservedResult.TerminalState,
                ["eventKinds"] = SortedStrings(round.ObservedResult.EventKinds),
                ["evidenceRefs"] = SortedStrings(round.ObservedResult.EvidenceRefs),
            },
            ["loadedKnowledge"] = SortedStrings(round.LoadedKnowledge),
            ["newKnowledge"] = SortedStrings(round.NewKnowledge),
            ["remainingUnknowns"] = SortedStrings(round.RemainingUnknowns),
            ["planDelta"] = DeltaNode(round.PlanDelta),
            ["nextStrategy"] = DirectiveNode(round.NextStrategy),
        };

        var dispatchNode = new JsonObject();
        if (round.PreviousDispatchPolicySummary is { } previousSummary)
            dispatchNode["previous"] = DispatchPolicyNode(previousSummary);
        if (round.NextDispatchPolicySummary is { } nextSummary)
            dispatchNode["next"] = DispatchPolicyNode(nextSummary);
        if (dispatchNode.Count > 0)
            root["dispatchPolicy"] = dispatchNode;

        return root.ToJsonString(Indented);
    }

    /// <summary>Deterministic Markdown of one planning round (evidence artifact).</summary>
    public static string ToMarkdown(PlanningRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var writer = new StringBuilder();
        writer.AppendLine($"# Planning Round {round.RoundIndex}");
        writer.AppendLine();
        writer.AppendLine("## Previous Plan");
        writer.AppendLine(DirectiveLine(round.PreviousPlan));
        writer.AppendLine();
        writer.AppendLine("## Observed Result");
        writer.AppendLine($"- runId: {round.ObservedResult.RunId}");
        writer.AppendLine($"- strategyId: {round.ObservedResult.StrategyId}");
        writer.AppendLine($"- terminalState: {round.ObservedResult.TerminalState}");
        writer.AppendLine($"- eventKinds: {JoinSorted(round.ObservedResult.EventKinds)}");
        writer.AppendLine($"- evidenceRefs: {JoinSorted(round.ObservedResult.EvidenceRefs)}");
        writer.AppendLine();
        writer.AppendLine("## Knowledge");
        writer.AppendLine($"- loaded: {JoinSorted(round.LoadedKnowledge)}");
        writer.AppendLine($"- new: {JoinSorted(round.NewKnowledge)}");
        writer.AppendLine($"- remainingUnknowns: {JoinSorted(round.RemainingUnknowns)}");
        writer.AppendLine();
        writer.AppendLine("## Plan Delta");
        if (round.PlanDelta.IsNoOp)
        {
            writer.AppendLine($"- NO_OP_WITH_REASON: {round.PlanDelta.NoOpReason}");
        }
        else
        {
            foreach (var change in round.PlanDelta.Changes)
            {
                writer.AppendLine($"- {change.Freedom}: {change.Description}");
                writer.AppendLine($"  - knowledgeRefs: {JoinSorted(change.KnowledgeRefs)}");
                writer.AppendLine($"  - evidenceRefs: {JoinSorted(change.EvidenceRefs)}");
            }
        }

        writer.AppendLine();
        writer.AppendLine("## Next Strategy");
        writer.AppendLine(DirectiveLine(round.NextStrategy));
        if (round.PreviousDispatchPolicySummary is not null || round.NextDispatchPolicySummary is not null)
        {
            writer.AppendLine();
            writer.AppendLine("## Dispatch Policy");
            writer.AppendLine($"- previous: {DispatchLine(round.PreviousDispatchPolicySummary)}");
            writer.AppendLine($"- next: {DispatchLine(round.NextDispatchPolicySummary)}");
        }

        return writer.ToString();
    }

    // ── JsonNode construction (fixed order; never reflection-based) ──────────

    private static JsonObject DirectiveNode(StrategyDirective directive) => new()
    {
        ["strategyId"] = directive.StrategyId,
        ["contractVersion"] = directive.ContractVersion,
        ["objective"] = ObjectiveNode(directive.Objective),
        ["scope"] = new JsonObject
        {
            ["applicationIdentity"] = directive.Scope.ApplicationIdentity,
            ["semanticRoot"] = directive.Scope.SemanticRoot,
            ["maximumDepth"] = directive.Scope.MaximumDepth,
        },
        ["exploration"] = directive.Exploration.ToString(),
        ["constraints"] = new JsonObject
        {
            ["allowedInteractionCategories"] = SortedStrings(
                directive.Constraints.AllowedInteractionCategories.Select(category => category.ToString())),
            ["prohibitedEffects"] = SortedStrings(
                directive.Constraints.ProhibitedEffects.Select(effect => effect.ToString())),
        },
        ["completion"] = new JsonObject { ["kind"] = directive.Completion.Kind.ToString() },
        ["adaptation"] = new JsonObject
        {
            ["allowedAdaptations"] = SortedStrings(
                directive.Adaptation.AllowedAdaptations.Select(adaptation => adaptation.ToString())),
        },
    };

    private static JsonObject ObjectiveNode(StrategyObjective objective)
    {
        var node = new JsonObject { ["kind"] = objective.Kind.ToString() };
        if (objective.Criterion is { } criterion)
        {
            node["criterion"] = new JsonObject
            {
                ["capabilityId"] = criterion.CapabilityId,
                ["criterionId"] = criterion.CriterionId,
                ["version"] = criterion.Version,
            };
        }

        return node;
    }

    private static JsonObject DeltaNode(PlanDelta delta)
    {
        var node = new JsonObject { ["isNoOp"] = delta.IsNoOp };
        if (delta.IsNoOp)
        {
            node["noOpReason"] = delta.NoOpReason ?? string.Empty;
            return node;
        }

        var changes = new JsonArray();
        foreach (var change in delta.Changes)
        {
            changes.Add(new JsonObject
            {
                ["freedom"] = change.Freedom.ToString(),
                ["description"] = change.Description,
                ["knowledgeRefs"] = SortedStrings(change.KnowledgeRefs),
                ["evidenceRefs"] = SortedStrings(change.EvidenceRefs),
            });
        }

        node["changes"] = changes;
        return node;
    }

    private static JsonObject DispatchPolicyNode(DispatchPolicySummary summary)
    {
        var node = new JsonObject();
        foreach (var pair in summary.CategoryHandling.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            node[pair.Key.ToString()] = pair.Value.ToString();
        return node;
    }

    private static JsonArray SortedStrings(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values.Order(StringComparer.Ordinal))
            array.Add(value);
        return array;
    }

    private static string JoinSorted(IEnumerable<string> values)
        => string.Join(", ", values.Order(StringComparer.Ordinal));

    private static string DirectiveLine(StrategyDirective directive)
        => $"strategy {directive.StrategyId} v{directive.ContractVersion}: objective={directive.Objective.Kind}"
           + $" criterion={directive.Objective.Criterion?.CriterionId ?? "-"}"
           + $" scope={directive.Scope.ApplicationIdentity}/{directive.Scope.SemanticRoot}"
           + $" depth={directive.Scope.MaximumDepth}"
           + $" exploration={directive.Exploration}"
           + $" allowed={JoinSorted(directive.Constraints.AllowedInteractionCategories.Select(c => c.ToString()))}"
           + $" prohibited={JoinSorted(directive.Constraints.ProhibitedEffects.Select(e => e.ToString()))}"
           + $" completion={directive.Completion.Kind}"
           + $" adaptations={JoinSorted(directive.Adaptation.AllowedAdaptations.Select(a => a.ToString()))}";

    private static string DispatchLine(DispatchPolicySummary? summary)
        => summary is null
            ? "(none)"
            : string.Join(", ", summary.CategoryHandling
                .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));
}