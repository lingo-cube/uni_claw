namespace UniClaw.Runtime.ValidationHarness.Classification;

/// <summary>
/// Phase 2.6 Fast-only blocker taxonomy (WI-CRV2-P26-B): EXACTLY thirteen
/// blocker categories. Unlike <see cref="FailureOwner"/> (a fixed eight-owner
/// protocol-failure taxonomy used by <see cref="ProtocolFailureClassifier"/>),
/// this is the richer Phase-2.6 campaign vocabulary that separates perception /
/// capture / semantic / resolution concerns the protocol taxonomy deliberately
/// folds together. It is a closed classification set: a campaign blocker is
/// always labelled with one AND only one of these categories — never a bare
/// "failed → perception issue" generalisation (blocker_record block).
///
/// The set is fixed at exactly 13; no value may be added or removed by a
/// consumer. The <see cref="Phase26BlockerCategory.PERCEPTION"/> ... set is
/// defined here, once, and shared by
/// <see cref="Phase26FastOnlyRunMetrics.BlockerCategoryCounts"/> and
/// <see cref="Phase26BlockerRecord"/> attribution.
/// NEW_SYMBOL_JUSTIFICATION: no existing classification type carries this
/// closed 13-value Phase-2.6 blocker vocabulary — <see cref="FailureOwner"/> is
/// a coarser 8-value protocol-failure taxonomy and must not be conflated with
/// it. A dedicated closed enum is required so aggregation and comparison can
/// rely on an exactly-13 shape without inventing a second owner system.
/// </summary>
public enum Phase26BlockerCategory
{
    /// <summary>Sensing variance: a screen element was not reliably perceived
    /// (dual-channel miss, OCR short-read) so its semantic status is Unknown.</summary>
    PERCEPTION = 0,

    /// <summary>Collection / cadence variance: a split-frame or sparse window
    /// was accepted into the epoch and could not be normalized monotonically.</summary>
    CAPTURE = 1,

    /// <summary>Semantic interpretation divergence beyond raw sensing.</summary>
    SEMANTIC = 2,

    /// <summary>The Fast resolution path produced a working interpretation with
    /// no authority, but the campaign result diverged around Fast trust.</summary>
    FAST_RESOLUTION = 3,

    /// <summary>Container identity (semantic identity candidate) divergence.</summary>
    CONTAINER_IDENTITY = 4,

    /// <summary>Transition classification / occurrence divergence (same/child/
    /// return/off-path) at the boundary level.</summary>
    TRANSITION = 5,

    /// <summary>Entry / return-context divergence (path-relative entry, verified
    /// return to active parent).</summary>
    ENTRY_RETURN = 6,

    /// <summary>Local container model divergence (viewport / local progress).</summary>
    LOCAL_MODEL = 7,

    /// <summary>Coverage / local-inventory lifecycle divergence
    /// (COVERAGE_COMPLETE != SEMANTIC_RESOLVED; coverage exhaustion).</summary>
    COVERAGE = 8,

    /// <summary>Agent execution-obligation divergence (what the runtime was
    /// obligated to complete vs what physically advanced).</summary>
    AGENT_OBLIGATION = 9,

    /// <summary>Action-grounding divergence (intended action vs fresh observed
    /// world reality).</summary>
    ACTION_GROUNDING = 10,

    /// <summary>Device-environment divergence (popup, external boundary,
    /// unexpected navigation, empty run).</summary>
    ENVIRONMENT = 11,

    /// <summary>Genuinely unattributed divergence — no category claim is
    /// supportable by existing evidence (fail-open for attribution, fail-closed
    /// for honesty: never forced into a category).</summary>
    UNKNOWN = 12,
}

/// <summary>
/// Pure advisory mapping from the 13-value Phase-2.6 blocker category onto the
/// existing eight-owner protocol vocabulary (<see cref="FailureOwner"/>) —
/// REUSE, never a second owner system. The map is a best-fit for attribution
/// bookkeeping; it never replaces the independently supplied
/// <see cref="Phase26BlockerRecord.Owner"/>.
///
/// <see cref="Phase26BlockerCategory.UNKNOWN"/> maps to null because no fixed
/// protocol owner can honestly claim a genuinely unattributed blocker — forcing
/// it into an owner would be exactly the generalised attribution the taxonomy
/// forbids.
/// NEW_SYMBOL_JUSTIFICATION: required to satisfy the "Owner reuses/maps the
/// existing FailureOwner vocabulary" obligation without inventing a parallel
/// owner enum.
/// </summary>
public static class Phase26BlockerOwnerMapping
{
    /// <summary>Best-fit existing protocol owner for each non-UNKNOWN category;
    /// null for <see cref="Phase26BlockerCategory.UNKNOWN"/> (not attributable).</summary>
    public static FailureOwner? ToFailureOwner(this Phase26BlockerCategory category)
    {
        return category switch
        {
            Phase26BlockerCategory.PERCEPTION => FailureOwner.Grounding,
            Phase26BlockerCategory.CAPTURE => FailureOwner.Environment,
            Phase26BlockerCategory.SEMANTIC => FailureOwner.Execution,
            Phase26BlockerCategory.FAST_RESOLUTION => FailureOwner.Grounding,
            Phase26BlockerCategory.CONTAINER_IDENTITY => FailureOwner.Grounding,
            Phase26BlockerCategory.TRANSITION => FailureOwner.Execution,
            Phase26BlockerCategory.ENTRY_RETURN => FailureOwner.Execution,
            Phase26BlockerCategory.LOCAL_MODEL => FailureOwner.Execution,
            Phase26BlockerCategory.COVERAGE => FailureOwner.Discovery,
            Phase26BlockerCategory.AGENT_OBLIGATION => FailureOwner.Execution,
            Phase26BlockerCategory.ACTION_GROUNDING => FailureOwner.Grounding,
            Phase26BlockerCategory.ENVIRONMENT => FailureOwner.Environment,
            Phase26BlockerCategory.UNKNOWN => null,
            _ => null,
        };
    }
}

/// <summary>
/// One Phase-2.6 blocker record with EXACTLY six mandatory attribution fields:
/// <c>LastGood</c>, <c>FirstDivergence</c>, <c>ExpectedReality</c>,
/// <c>ObservedReality</c>, <c>Owner</c>, <c>EvidenceRef</c>. The six-field
/// shape is the anti-generalisation guarantee (WI-CRV2-P26-B): a bare
/// "run failed → perception issue" conclusion cannot be constructed, because a
/// record with a blank reality pair, no divergence, no evidence ref, or no
/// valid owner is rejected at construction (fail-closed).
///
/// <see cref="Owner"/> reuses the existing <see cref="FailureOwner"/> vocabulary
/// (never a second owner system); <see cref="EvidenceRef"/> is the evidence
/// locator that supports the attribution.
/// NEW_SYMBOL_JUSTIFICATION: required to record campaign blockers with the
/// mandatory six-field anti-generalisation shape; no existing record carries
/// this exact closure contract.
/// </summary>
public sealed record Phase26BlockerRecord
{
    /// <summary>Gets the last point identified as correct (evidence ref or
    /// readable description of the last-good boundary).</summary>
    public string LastGood { get; }

    /// <summary>Gets the earliest evidence-derived divergence point (never the
    /// final symptom alone).</summary>
    public string FirstDivergence { get; }

    /// <summary>Gets what the runtime expected / intended at the divergence.</summary>
    public string ExpectedReality { get; }

    /// <summary>Gets what actually occurred / was observed at the divergence.</summary>
    public string ObservedReality { get; }

    /// <summary>Gets the protocol owner reusing the existing FailureOwner
    /// vocabulary (validated, never default/undefined).</summary>
    public FailureOwner Owner { get; }

    /// <summary>Gets the evidence reference supporting the attribution.</summary>
    public string EvidenceRef { get; }

    /// <summary>
    /// Create a blocker record. Fails closed on any blank field or an undefined
    /// owner: a record missing any of the six mandatory fields cannot exist.
    /// </summary>
    public Phase26BlockerRecord(
        string lastGood,
        string firstDivergence,
        string expectedReality,
        string observedReality,
        FailureOwner owner,
        string evidenceRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastGood);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstDivergence);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedReality);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedReality);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceRef);
        if (!Enum.IsDefined(owner))
        {
            throw new ArgumentOutOfRangeException(
                nameof(owner), owner,
                "a Phase-2.6 blocker record requires one of the fixed FailureOwner values (WI-CRV2-P26-B).");
        }

        LastGood = lastGood;
        FirstDivergence = firstDivergence;
        ExpectedReality = expectedReality;
        ObservedReality = observedReality;
        Owner = owner;
        EvidenceRef = evidenceRef;
    }
}
