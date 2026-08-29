namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// Validation-asset lifecycle status (spec requirement "ScenarioKnowledgeFixture
/// as a validation test asset" — "Status ∈ {ACTIVE, STALE, CONTRADICTED,
/// SUPERSEDED, INVALIDATED}"; design D3). CLOSED: exactly these five.
/// A record is <see cref="Active"/> only while current fresh evidence
/// supports it; every other state is a DOWNGRADE produced exclusively by
/// <see cref="ScenarioKnowledgeFixture.ApplyFreshEvidence"/> — there is NO
/// re-activation path (CURRENT FRESH EVIDENCE FIRST). Status is a
/// validation-asset lifecycle only, never a Runtime or Memory contract.
/// </summary>
public enum KnowledgeStatus
{
    /// <summary>Currently supported by fresh evidence; returned by active
    /// advisory queries.</summary>
    Active,

    /// <summary>Downgraded: still believed, but weakened/aged by newer
    /// evidence — not a force-apply candidate.</summary>
    Stale,

    /// <summary>Downgraded: contradicted by fresh evidence — never applied
    /// over current reality.</summary>
    Contradicted,

    /// <summary>Downgraded: replaced by a newer record of the same semantic
    /// anchor (the Supersedes/SupersededBy pair stays traceable).</summary>
    Superseded,

    /// <summary>Downgraded: invalidated by fresh evidence — no longer usable
    /// advisory input at all.</summary>
    Invalidated,
}