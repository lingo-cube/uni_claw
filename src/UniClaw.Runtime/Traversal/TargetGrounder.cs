using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Traversal;

/// <summary>
/// Stateless target-resolution algorithm. Owns grounding logic; does NOT own retry policy.
///
/// TargetGrounder resolves which element index should receive an action for a given
/// PlanStep, Observation, and optional grounding criterion. It returns a grounded
/// index or indicates unresolved/ambiguous. It does NOT dispatch, retry, authorize
/// business actions, or mutate state.
///
/// Traversal owns WHEN to invoke/reinvoke the Grounder — including retry decisions.
/// The current criterion path is fail-closed before legacy retry; a criterion failure
/// therefore yields NO DISPATCH. Legacy retry asks the same legacy Ground method again.
/// Never: criterion failure → weaker legacy grounding → dispatch.
/// </summary>
public static class TargetGrounder
{
    /// <summary>
    /// Legacy text-match grounding: exact ordinal match on element text.
    /// For SetSwitch actions with multiple matches, state-bearing candidates
    /// (SwitchState != null) are preferred deterministically.
    /// </summary>
    /// <param name="targetDescription">Text to match against element text.</param>
    /// <param name="actionDescription">Action token — used to trigger state-bearing priority.</param>
    /// <param name="candidates">Current observation elements.</param>
    /// <returns>Grounded element index, or null if no match.</returns>
    public static int? Ground(
        string targetDescription,
        string actionDescription,
        ImmutableArray<ObservedElement> candidates)
    {
        var matches = candidates
            .Select((element, index) => (Element: element, Index: index))
            .Where(x => string.Equals(x.Element.Text, targetDescription, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
            return null;

        if (IsSetSwitchAction(actionDescription) && matches.Count > 1)
        {
            var stateBearing = matches.Where(x => x.Element.SwitchState is not null).ToList();
            if (stateBearing.Count > 0)
                return stateBearing[0].Index;
        }

        return matches[0].Index; // single candidate / Tap / no state-bearing: first match (deterministic)
    }

    /// <summary>
    /// Criterion-grounded target resolution. Each candidate is evaluated by the
    /// caller-provided criterion, and the result is checked against authorization
    /// receipts. Exactly one supported + authorized candidate must exist.
    /// </summary>
    /// <param name="targetDescription">Human-readable target description (for error messages only).</param>
    /// <param name="observation">Fresh observation for criterion evaluation.</param>
    /// <param name="candidates">Current observation elements.</param>
    /// <param name="criterion">Caller-provided two-phase grounding criterion.</param>
    /// <param name="authorizationReceipts">Pre-dispatch authorization evidence per element index.</param>
    /// <param name="failure">If resolution fails, a human-readable reason.</param>
    /// <returns>Grounded element index, or null if unresolved/ambiguous/unauthorized.</returns>
    public static int? GroundCriterion(
        string targetDescription,
        Observation observation,
        ImmutableArray<ObservedElement> candidates,
        TargetGroundingCriterion criterion,
        ImmutableDictionary<int, CandidateAuthorizationEvidence> authorizationReceipts,
        out string? failure)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(criterion);
        ArgumentNullException.ThrowIfNull(authorizationReceipts);

        var supported = new List<(ObservedElement Element, TargetGroundingEvidence Evidence)>();
        foreach (var candidate in candidates)
        {
            var evidence = criterion.CandidateEvaluator(observation, candidate)
                ?? throw new InvalidOperationException("TargetGroundingCriterion.CandidateEvaluator 返回 null evidence。");
            if (evidence.Supported is true)
                supported.Add((candidate, evidence));
        }

        if (supported.Count != 1)
        {
            failure = supported.Count == 0
                ? $"Target grounding insufficient: no current candidate is sufficiently supported for '{targetDescription}'."
                : $"Target grounding ambiguous: {supported.Count} current candidates are sufficiently supported for '{targetDescription}'.";
            return null;
        }

        var selected = supported[0].Element;
        if (!authorizationReceipts.TryGetValue(selected.Index, out var authorization)
            || authorization.Authorized is not true)
        {
            failure = $"Target grounding safety authorization is absent or not authorized for index={selected.Index}.";
            return null;
        }

        failure = null;
        return selected.Index;
    }

    private static bool IsSetSwitchAction(string actionDescription)
        => actionDescription.StartsWith("SetSwitch", StringComparison.Ordinal);
}
