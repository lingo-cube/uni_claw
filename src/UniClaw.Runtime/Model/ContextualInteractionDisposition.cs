namespace UniClaw.Runtime.Model;

/// <summary>
/// Kind of an Agent-produced contextual interaction disposition.
/// </summary>
public enum ContextualInteractionDispositionKind
{
    /// <summary>The Agent contextually resolved this occurrence as the
    /// parent-return control (a RESOLVED_NON_INVENTORY_CONTROL).</summary>
    ParentReturnControl,
}

/// <summary>
/// Agent-produced contextual disposition for ONE interactive occurrence in a
/// fresh post-completeness Observation (the post-completeness consistency
/// contract). The Agent — the sole contextual semantic authority — explicitly
/// resolved this occurrence as a
/// <see cref="ContextualInteractionDispositionKind.ParentReturnControl"/> in
/// the CURRENT observation.
///
/// OCCURRENCE-SCOPED: the reference is (ObservationSequence,
/// Canonical OccurrenceId) — valid ONLY for that observation's accepted
/// channel. It is never a global element identity, never a cross-viewport
/// source identity, never a destination identity, and is never cached or
/// reused across Observations (a disposition for a different sequence never
/// applies). The Validator consumes ONLY this explicit disposition; it never
/// performs its own parent-return semantic interpretation.
/// </summary>
public sealed record ContextualInteractionDisposition(
    long ObservationSequence,
    string OccurrenceId,
    ContextualInteractionDispositionKind Kind);
