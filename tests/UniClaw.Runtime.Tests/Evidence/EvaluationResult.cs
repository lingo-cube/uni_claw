using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// Result of comparing an <see cref="ExpectedSpecification"/> against actual
/// Runtime output. The evaluator inspects only evidence surfaces:
///   - container identity evidence (semantic pages visited)
///   - coverage evidence (branch inventory / completed children)
///   - belief evidence (state beliefs observed)
///   - trace correctness (terminal state, reasons)
///   - goal evidence satisfaction
///
/// It NEVER inspects click order, navigation paths, element names, or
/// scenario-specific action sequences.
/// </summary>
public sealed record EvaluationResult(
    bool Passed,
    RunState TerminalState,
    ImmutableHashSet<string> DiscoveredContainers,
    ImmutableHashSet<string> CoveredContainers,
    bool GoalEvidenceSatisfied,
    ImmutableArray<string> Failures,
    string? TerminalReason)
{
    public string Summary => Passed
        ? "PASS: evidence satisfies the expected specification."
        : $"FAIL: {string.Join("; ", Failures)}";

    public static EvaluationResult Failed(RunState state, string reason, params string[] failures) =>
        new(false, state, ImmutableHashSet<string>.Empty, ImmutableHashSet<string>.Empty, false,
            failures.ToImmutableArray(), reason);
}
