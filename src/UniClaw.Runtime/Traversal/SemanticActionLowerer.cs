using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Traversal;

/// <summary>
/// Stateless pure function: authorized SemanticAction → lowered result.
///
/// SemanticActionLowerer owns the lowering mechanics — locating the bound
/// interaction surface, checking SwitchState for safety, and producing
/// a DeviceAction proposal. It does NOT dispatch, mutate Container state,
/// select capabilities, authorize actions, or make business decisions.
///
/// Traversal remains protocol owner: lower → dispatch → observe → verify → journal.
///
/// Safety rules (CRITICAL):
///   1. Binding must match the action's ObjectIdentity.
///   2. Unknown SwitchState → NO DISPATCH (StateUnknown).
///   3. Already satisfied → NO DISPATCH (NoOp).
///   4. Ambiguous interaction surface → NO DISPATCH (Unresolved).
///   5. SetEnabled is IDEMPOTENT desired-world semantics — NOT a physical toggle.
/// </summary>
public static class SemanticActionLowerer
{
    /// <summary>
    /// Lowers an authorized SemanticAction to an ExecutionAction using
    /// the Container's current object binding and fresh Observation.
    /// </summary>
    /// <param name="action">Authorized SemanticAction.</param>
    /// <param name="binding">Container's current object binding.</param>
    /// <param name="observation">Fresh observation (binding must match these indices).</param>
    /// <returns>Lowered ExecutionAction or safe no-dispatch outcome.</returns>
    public static SemanticActionResult Lower(
        SemanticAction action,
        ObjectBinding binding,
        Observation observation)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(observation);

        // Verify binding targets the correct object
        if (!string.Equals(binding.ObjectIdentity, action.ObjectIdentity, StringComparison.Ordinal))
            return new SemanticActionResult.Invalid(
                $"Binding is for '{binding.ObjectIdentity}', not '{action.ObjectIdentity}'.");

        // Grounding is permitted only from fresh, admitted primary semantic evidence.
        // Raw provider labels (including PerceptionType) and manifest symbols are never
        // interpreted here.
        var toggleCandidates = observation.AdmittedSemanticEvidence.EligibleForAuthorizationInput
            .Select(e => e.Candidate)
            .OfType<ElementAffordanceCandidateEvidence>()
            .Where(e => e.AffordanceKind == ElementAffordanceKind.LocalControl
                && e.Observation.Sequence == observation.SequenceNumber
                && string.Equals(e.Observation.FrameId, e.Provenance.FrameId, StringComparison.Ordinal)
                && SemanticObservationFactProjector.TryResolveVisualIndex(observation, e.OccurrenceId, out _))
            .Select(e => (Evidence: e, Index: ResolveVisualIndex(observation, e.OccurrenceId)))
            .Where(x => x.Index is not null && binding.ElementIndices.Contains(x.Index.Value))
            .ToImmutableArray();

        if (toggleCandidates.Length == 0)
            return new SemanticActionResult.Unresolved(
                $"No toggle-type element found in binding for '{action.ObjectIdentity}'.");

        if (toggleCandidates.Length > 1)
            return new SemanticActionResult.Unresolved(
                $"Ambiguous: {toggleCandidates.Length} toggle candidates in binding for '{action.ObjectIdentity}'.");

        var toggle = observation.Elements.First(e => e.Index == toggleCandidates[0].Index);

        // SAFETY: unknown state → NO DISPATCH
        if (toggle.SwitchState is null)
            return new SemanticActionResult.StateUnknown(
                $"Toggle for '{action.ObjectIdentity}' exists but SwitchState is unknown. "
                + "State evidence required before dispatch.");

        // SAFETY: already satisfied → NO DISPATCH
        if (toggle.SwitchState.Value == action.DesiredValue)
            return new SemanticActionResult.NoOp(
                $"'{action.ObjectIdentity}.{action.StateDimension}' is already {action.DesiredValue}.");

        // SAFETY: SetEnabled is idempotent, not a blind toggle
        // SwitchState=false, DesiredValue=true → dispatch SetSwitch(true)
        var deviceAction = new DeviceAction.SetSwitch(
            toggle.Index,
            action.DesiredValue,
            toggle.Bounds);

        return new SemanticActionResult.Dispatched(deviceAction);
    }

    private static int? ResolveVisualIndex(Observation observation, string occurrenceId) =>
        SemanticObservationFactProjector.TryResolveVisualIndex(observation, occurrenceId, out var index) ? index : null;
}
