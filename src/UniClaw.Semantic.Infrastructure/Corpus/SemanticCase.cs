using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Infrastructure.Corpus;

/// <summary>Source category of a Semantic case.</summary>
public enum SemanticCaseSource
{
    /// <summary>Case captured from a real Runtime trace.</summary>
    RealTrace = 1,

    /// <summary>Manually authored case.</summary>
    Manual = 2,

    /// <summary>Constructed synthetic case.</summary>
    Synthetic = 3,

    /// <summary>Case used to prevent regression of existing behavior.</summary>
    Regression = 4,

    /// <summary>Legacy alias for real-world cases.</summary>
    RealWorld = 1,
}

/// <summary>Viewport state of a Container Identity case.</summary>
public enum SemanticViewportState
{
    /// <summary>Viewport state not specified.</summary>
    Unknown = 0,

    /// <summary>The container title is visible.</summary>
    TitleVisible = 1,

    /// <summary>The container title has left the viewport.</summary>
    TitleOffscreen = 2,

    /// <summary>Only a partial set of container elements is visible.</summary>
    Partial = 3,

    /// <summary>The observation belongs to a different/wrong page.</summary>
    WrongPage = 4,
}

/// <summary>Visible anchor state of a Container Identity case.</summary>
public enum SemanticVisibleAnchorState
{
    /// <summary>Anchor state not specified.</summary>
    Unknown = 0,

    /// <summary>The primary identity anchor is visible.</summary>
    AnchorVisible = 1,

    /// <summary>The primary identity anchor is missing from the viewport.</summary>
    AnchorMissing = 2,
}

/// <summary>Difficulty category of a Semantic case.</summary>
public enum SemanticCaseDifficulty
{
    /// <summary>Easy case.</summary>
    Easy = 1,

    /// <summary>Medium case.</summary>
    Medium = 2,

    /// <summary>Hard case.</summary>
    Hard = 3,
}

/// <summary>
/// A single Semantic Corpus case. Phase 1 supports Container Identity cases only.
/// Element Meaning and Relation cases are not part of this baseline.
/// </summary>
public sealed record SemanticCase
{
    /// <summary>Unique case id.</summary>
    public string CaseId { get; }

    /// <summary>Input observation/feature snapshot.</summary>
    public Observation InputObservation { get; }

    /// <summary>Expected identity candidate produced by the provider.</summary>
    public string ExpectedCandidate { get; }

    /// <summary>Expected runtime container identity (may equal ExpectedCandidate).</summary>
    public string? ExpectedIdentity { get; }

    /// <summary>Case source.</summary>
    public SemanticCaseSource Source { get; }

    /// <summary>Case difficulty.</summary>
    public SemanticCaseDifficulty Difficulty { get; }

    /// <summary>Optional previous verified identity used in ObservationContext.</summary>
    public string? PreviousVerifiedIdentity { get; init; }

    /// <summary>Viewport state metadata for dataset management / benchmark analysis.</summary>
    public SemanticViewportState ViewportState { get; init; } = SemanticViewportState.Unknown;

    /// <summary>Scroll position metadata (0 = top, increasing downward).</summary>
    public double ScrollPosition { get; init; }

    /// <summary>Visible anchor state metadata.</summary>
    public SemanticVisibleAnchorState VisibleAnchorState { get; init; } = SemanticVisibleAnchorState.Unknown;

    /// <summary>Noise level metadata (0 = clean, higher = noisier).</summary>
    public int NoiseLevel { get; init; }

    /// <summary>Ambiguity level metadata (0 = unambiguous, higher = more ambiguous).</summary>
    public int AmbiguityLevel { get; init; }

    /// <summary>Creates a Semantic corpus case.</summary>
    public SemanticCase(
        string caseId,
        Observation inputObservation,
        string expectedCandidate,
        string? expectedIdentity,
        SemanticCaseSource source,
        SemanticCaseDifficulty difficulty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentNullException.ThrowIfNull(inputObservation);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCandidate);
        CaseId = caseId;
        InputObservation = inputObservation;
        ExpectedCandidate = expectedCandidate;
        ExpectedIdentity = expectedIdentity;
        Source = source;
        Difficulty = difficulty;
    }
}