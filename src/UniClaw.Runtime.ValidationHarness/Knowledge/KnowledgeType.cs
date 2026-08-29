namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// Graduated knowledge vocabulary (spec requirement "ScenarioKnowledgeFixture
/// as a validation test asset" — "KnowledgeType SHALL be restricted to
/// graduated vocabulary"; design D3). CLOSED: exactly these seven
/// observation-level disposition classes, and nothing else — no eighth
/// vocabulary word, no new runtime semantics. Each value classifies what was
/// OBSERVED in a run's evidence/result, never what should be executed.
/// TEST_KNOWLEDGE != RUNTIME_TRUTH; TEST_KNOWLEDGE != ACTION_AUTHORITY.
/// </summary>
public enum KnowledgeType
{
    /// <summary>A discovered container (e.g. a Settings section/sub-page
    /// shell holding further rows). Observed, not assumed.</summary>
    KnownContainer,

    /// <summary>Observed as record-only content — informative but NOT an
    /// exploration / dispatch target; plans exclude it from traversal.</summary>
    KnownRecordOnly,

    /// <summary>A local control (toggle/switch) whose effect is confined to
    /// local state. Never a navigation target.</summary>
    KnownLocalControl,

    /// <summary>An external boundary — recursing past it would cross the
    /// bounded scenario; plans exclude it from recursive children.</summary>
    KnownExternalBoundary,

    /// <summary>Non-interactive content (plain text/status rows).</summary>
    KnownNonInteractive,

    /// <summary>Observed but unresolved; accounted honestly as unknown rather
    /// than guessed.</summary>
    KnownUnresolved,

    /// <summary>Potentially state-mutating (destructive/dangerous classes):
    /// RECORD_ONLY / FAIL_CLOSED default, never learned by execution
    /// (spec "Safety learning without dangerous trial-and-error"; design D4).</summary>
    KnownPotentiallyStateMutating,
}