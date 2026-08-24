using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// Generic evidence evaluator: compares an <see cref="ExpectedSpecification"/>
/// against actual Runtime evidence output (trace / branch progress / goal
/// evidence / terminal state). It is scenario-neutral — it never references a
/// specific UI label, page name, click order, or navigation route. Container
/// identities come from the fixture, and only structural evidence surfaces are
/// inspected.
/// </summary>
public static class EvidenceEvaluator
{
    /// <summary>Evaluate the actual Runtime output against the expected specification.</summary>
    public static EvaluationResult Evaluate(
        ExpectedSpecification spec,
        RuntimeAgent agent,
        IReadOnlyList<GoalEvidence> evidenceReceipts)
    {
        var failures = ImmutableArray.CreateBuilder<string>();

        // 1. Terminal state correctness.
        if (agent.State != RunState.Completed && spec.RequireGoalEvidenceSatisfied)
        {
            return EvaluationResult.Failed(
                agent.State,
                agent.Reason ?? "(no reason)",
                $"terminal state {agent.State} != Completed; reason={agent.Reason}");
        }

        // 2. Evidence sufficiency: goal evidence must be satisfied by observation evidence.
        var satisfied = evidenceReceipts.LastOrDefault(e => e.Satisfied);
        if (spec.RequireGoalEvidenceSatisfied && satisfied is null)
        {
            failures.Add("no satisfied GoalEvidence was produced (evidence insufficiency)");
        }

        // 3. Coverage completion: every required container must appear in the
        //    agent's container trace with completed-child evidence.
        var tracedContainers = agent.Trace
            .Where(t => t.ContainerId is not null)
            .Select(t => t.ContainerId!)
            .ToHashSet(StringComparer.Ordinal);
        var covered = agent.BranchProgress.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var required in spec.RequiredCoverage)
        {
            if (!tracedContainers.Contains(required))
                failures.Add($"required container '{required}' was never traced (coverage missing)");
            if (!covered.Contains(required))
                failures.Add($"required container '{required}' has no completed-child evidence");
        }

        // 4. Belief consistency: a completed run must end with a belief whose
        //    semantic page is the entry root (the run never drifted outside scope).
        if (agent.Belief?.SemanticPage is { } page
            && !string.Equals(page, spec.RootContainerIdentity, StringComparison.Ordinal)
            && agent.State == RunState.Completed)
        {
            failures.Add($"final belief semantic page '{page}' != root '{spec.RootContainerIdentity}'");
        }

        return new EvaluationResult(
            failures.Count == 0,
            agent.State,
            tracedContainers.ToImmutableHashSet(StringComparer.Ordinal),
            covered.ToImmutableHashSet(StringComparer.Ordinal),
            satisfied is not null,
            failures.ToImmutable(),
            agent.Reason);
    }
}
