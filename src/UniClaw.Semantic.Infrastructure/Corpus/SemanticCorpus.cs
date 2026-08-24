using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Corpus;

/// <summary>Corpus category used for dataset management and benchmark filtering.</summary>
public enum SemanticCorpusCategory
{
    /// <summary>Verified correct samples.</summary>
    Golden = 1,

    /// <summary>Historical failure / repair regression samples.</summary>
    Regression = 2,

    /// <summary>Samples likely to cause false recovery.</summary>
    Adversarial = 3,

    /// <summary>Exploration samples.</summary>
    Experimental = 4,
}

/// <summary>
/// A named collection of <see cref="SemanticCase"/> values. Phase 1 supports
/// Container Identity cases only.
/// </summary>
public sealed record SemanticCorpus
{
    /// <summary>Corpus id, e.g. "DeveloperOptions-v1".</summary>
    public string CorpusId { get; }

    /// <summary>Cases in this corpus.</summary>
    public ImmutableArray<SemanticCase> Cases { get; }

    /// <summary>Corpus category. Defaults to Experimental.</summary>
    public SemanticCorpusCategory Category { get; init; } = SemanticCorpusCategory.Experimental;

    /// <summary>Creates a Semantic corpus.</summary>
    public SemanticCorpus(string corpusId, ImmutableArray<SemanticCase> cases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusId);
        Cases = cases.IsDefault ? ImmutableArray<SemanticCase>.Empty : cases;
        CorpusId = corpusId;
    }
}