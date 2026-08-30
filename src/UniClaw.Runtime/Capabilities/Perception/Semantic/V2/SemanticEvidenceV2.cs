using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable CS1591

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

/// <summary>Closed, versioned semantic evidence protocol.</summary>
public static class SemanticEvidenceV2Protocol
{
    public const string Version = "semantic-evidence-v2";
}

public enum SemanticSourceTier
{
    Primary = 1,
    Auxiliary = 2,
}

public enum SemanticEvidenceKind
{
    ContainerIdentity = 1,
    ElementAffordance = 2,
    ContainerRelation = 3,
}

public enum ElementAffordanceKind
{
    NonInteractive = 1,
    NavigationCandidate = 2,
    LocalControl = 3,
    ParentReturnControl = 4,
}

public enum ContainerRelationKind
{
    Parent = 1,
    Child = 2,
    ReturnToParent = 3,
}

public enum SemanticEvidenceScope
{
    Observation = 1,
    Container = 2,
    BoundedHistory = 3,
}

public sealed record SemanticObservationReference
{
    public string ObservationId { get; }
    public long Sequence { get; }
    public string FrameId { get; }

    public SemanticObservationReference(string observationId, long sequence, string frameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ObservationId = observationId;
        Sequence = sequence;
        FrameId = frameId;
    }
}

public sealed record SemanticScopeReference
{
    public string ScopeId { get; }
    public SemanticEvidenceScope Kind { get; }

    public SemanticScopeReference(string scopeId, SemanticEvidenceScope kind = SemanticEvidenceScope.Observation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ScopeId = scopeId;
        Kind = kind;
    }
}

public sealed record SemanticProvenance
{
    public string SourceId { get; }
    public SemanticSourceTier Tier { get; }
    public string CaptureId { get; }
    public DateTimeOffset CapturedAt { get; }
    public string FrameId { get; }

    public SemanticProvenance(
        string sourceId,
        SemanticSourceTier tier,
        string captureId,
        DateTimeOffset capturedAt,
        string frameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(captureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        if (!Enum.IsDefined(tier)) throw new ArgumentOutOfRangeException(nameof(tier));
        SourceId = sourceId;
        Tier = tier;
        CaptureId = captureId;
        CapturedAt = capturedAt;
        FrameId = frameId;
    }
}

public sealed record SemanticSourceMetadata
{
    public string SourceId { get; }
    public SemanticSourceTier Tier { get; }
    public bool Available { get; }
    public string FrameId { get; }

    public SemanticSourceMetadata(string sourceId, SemanticSourceTier tier, bool available, string frameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        SourceId = sourceId;
        Tier = tier;
        Available = available;
        FrameId = frameId;
    }
}

public enum SemanticObservationFactKind
{
    Text = 1,
    ClassName = 2,
    ResourceName = 3,
    ContentDescription = 4,
    BooleanState = 5,
    Geometry = 6,
}

public sealed record SemanticNormalizedBounds
{
    /// <summary>Accepted surplus for the normalized-frame fit check.
    /// ElementBounds is float32; a valid full-width element (X2 == 1.0f) can
    /// reconstruct to left+width == 1.0000000063 in double after float→double
    /// widening (the widen-first fix in SemanticObservationFactProjector covers
    /// the projection path; this tolerance covers every other constructor
    /// caller). A surplus at float32-reconstruction scale is NOT an
    /// out-of-frame violation — only a surplus ABOVE this tolerance is
    /// rejected fail-closed. 1e-6 ≈ 10× the float32 ulp of 1.0 (1.19e-7) and
    /// far below any genuine out-of-frame amount observed (e.g. 0.05).</summary>
    public const double FitTolerance = 1e-6;

    public double Left { get; }
    public double Top { get; }
    public double Width { get; }
    public double Height { get; }

    public SemanticNormalizedBounds(double left, double top, double width, double height)
    {
        if (left is < 0 or > 1 || top is < 0 or > 1 || width is <= 0 or > 1 || height is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(left), "Bounds must be normalized and positive.");
        if (left + width > 1 + FitTolerance || top + height > 1 + FitTolerance)
            throw new ArgumentOutOfRangeException(nameof(width), "Bounds must fit the normalized frame.");
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }
}

public sealed record SemanticObservationFact
{
    public string OccurrenceId { get; }
    public SemanticObservationFactKind Kind { get; }
    public string SourceId { get; }
    public SemanticSourceTier SourceTier { get; }
    public string ProvenanceId { get; }
    public long ObservationSequence { get; }
    public string FrameId { get; }
    public string? ParentOccurrenceId { get; }
    public string? RawText { get; }
    public string? RawClassName { get; }
    public string? RawResourceName { get; }
    public string? RawContentDescription { get; }
    public string? RawProviderType { get; }
    public bool? Clickable { get; }
    public bool? Checkable { get; }
    public bool? Enabled { get; }
    public bool? Focusable { get; }
    public bool? PrimitiveState { get; }
    public SemanticNormalizedBounds? Bounds { get; }

    public SemanticObservationFact(
        string occurrenceId,
        SemanticObservationFactKind kind,
        string sourceId,
        SemanticSourceTier sourceTier,
        string provenanceId,
        long observationSequence,
        string frameId,
        string? rawText = null,
        string? rawClassName = null,
        string? rawResourceName = null,
        string? rawContentDescription = null,
        bool? primitiveState = null,
        SemanticNormalizedBounds? bounds = null,
        string? rawProviderType = null,
        bool? clickable = null,
        bool? checkable = null,
        bool? enabled = null,
        bool? focusable = null,
        string? parentOccurrenceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        if (parentOccurrenceId is not null && string.IsNullOrWhiteSpace(parentOccurrenceId))
            throw new ArgumentException("Parent occurrence must be nonblank when provided.", nameof(parentOccurrenceId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(sourceTier)) throw new ArgumentOutOfRangeException(nameof(sourceTier));
        ArgumentOutOfRangeException.ThrowIfNegative(observationSequence);
        if (kind == SemanticObservationFactKind.Geometry && bounds is null)
            throw new ArgumentException("Geometry facts require normalized bounds.", nameof(bounds));
        OccurrenceId = occurrenceId;
        Kind = kind;
        SourceId = sourceId;
        SourceTier = sourceTier;
        ProvenanceId = provenanceId;
        ObservationSequence = observationSequence;
        FrameId = frameId;
        ParentOccurrenceId = parentOccurrenceId;
        RawText = rawText;
        RawClassName = rawClassName;
        RawResourceName = rawResourceName;
        RawContentDescription = rawContentDescription;
        RawProviderType = rawProviderType;
        Clickable = clickable;
        Checkable = checkable;
        Enabled = enabled;
        Focusable = focusable;
        PrimitiveState = primitiveState;
        Bounds = bounds;
    }
}

public sealed record SemanticVerifiedHistoryReference
{
    public string ReferenceId { get; }
    public long Revision { get; }

    public SemanticVerifiedHistoryReference(string referenceId, long revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        ReferenceId = referenceId;
        Revision = revision;
    }
}

public sealed record ExternalSemanticCapabilityContext
{
    public SemanticObservationReference Observation { get; }
    public ImmutableArray<SemanticSourceMetadata> Sources { get; }
    public ImmutableArray<SemanticVerifiedHistoryReference> VerifiedHistory { get; }
    public ImmutableArray<SemanticObservationFact> Facts { get; }

    public ExternalSemanticCapabilityContext(
        SemanticObservationReference observation,
        IEnumerable<SemanticSourceMetadata> sources,
        IEnumerable<SemanticVerifiedHistoryReference>? verifiedHistory = null,
        IEnumerable<SemanticObservationFact>? facts = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(sources);
        Observation = observation;
        Sources = sources.ToImmutableArray();
        VerifiedHistory = (verifiedHistory ?? Array.Empty<SemanticVerifiedHistoryReference>()).ToImmutableArray();
        Facts = (facts ?? Array.Empty<SemanticObservationFact>()).ToImmutableArray();
        foreach (var fact in Facts)
        {
            var source = Sources.FirstOrDefault(item =>
                string.Equals(item.SourceId, fact.SourceId, StringComparison.Ordinal));
            if (source is null || !source.Available || source.Tier != fact.SourceTier ||
                !string.Equals(source.FrameId, fact.FrameId, StringComparison.Ordinal) ||
                fact.ObservationSequence != Observation.Sequence)
                throw new ArgumentException("Observation fact is not correlated with the current observation.", nameof(facts));
        }
    }
}

public sealed record SemanticSymbolReference
{
    public string ManifestId { get; }
    public string ManifestVersion { get; }
    public string SymbolId { get; }

    public SemanticSymbolReference(string manifestId, string manifestVersion, string symbolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolId);
        ManifestId = manifestId;
        ManifestVersion = manifestVersion;
        SymbolId = symbolId;
    }
}

public sealed record SemanticCapabilityManifest
{
    public string ManifestId { get; }
    public string Version { get; }
    public ImmutableHashSet<string> Symbols { get; }

    public SemanticCapabilityManifest(string manifestId, string version, IEnumerable<string> symbols)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(symbols);
        var values = symbols.ToImmutableHashSet(StringComparer.Ordinal);
        if (values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Manifest symbols must be nonblank.", nameof(symbols));
        ManifestId = manifestId;
        Version = version;
        Symbols = values;
    }

    public bool Contains(SemanticSymbolReference symbol) =>
        string.Equals(ManifestId, symbol.ManifestId, StringComparison.Ordinal) &&
        string.Equals(Version, symbol.ManifestVersion, StringComparison.Ordinal) &&
        Symbols.Contains(symbol.SymbolId);
}

public abstract record SemanticCandidateEvidence
{
    public abstract SemanticEvidenceKind EvidenceKind { get; }
    public virtual string? OccurrenceId => null;
    public SemanticSymbolReference Meaning { get; }
    public SemanticObservationReference Observation { get; }
    public SemanticScopeReference Scope { get; }
    public SemanticProvenance Provenance { get; }
    public double Confidence { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ValidUntil { get; }

    protected SemanticCandidateEvidence(
        SemanticSymbolReference meaning,
        SemanticObservationReference observation,
        SemanticScopeReference scope,
        SemanticProvenance provenance,
        double confidence,
        DateTimeOffset createdAt,
        DateTimeOffset validUntil)
    {
        ArgumentNullException.ThrowIfNull(meaning);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(provenance);
        if (confidence is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(confidence));
        if (validUntil < createdAt) throw new ArgumentException("Expiry precedes creation.", nameof(validUntil));
        if (!string.Equals(observation.FrameId, provenance.FrameId, StringComparison.Ordinal))
            throw new ArgumentException("Observation and provenance frames must match.", nameof(provenance));
        Meaning = meaning;
        Observation = observation;
        Scope = scope;
        Provenance = provenance;
        Confidence = confidence;
        CreatedAt = createdAt;
        ValidUntil = validUntil;
    }
}

public sealed record ContainerIdentityCandidateEvidence : SemanticCandidateEvidence
{
    public override SemanticEvidenceKind EvidenceKind => SemanticEvidenceKind.ContainerIdentity;
    public override string? OccurrenceId { get; }
    public ContainerIdentityCandidateEvidence(
        SemanticSymbolReference meaning, SemanticObservationReference observation,
        SemanticScopeReference scope, SemanticProvenance provenance, double confidence,
        DateTimeOffset createdAt, DateTimeOffset validUntil, string? occurrenceId = null)
        : base(meaning, observation, scope, provenance, confidence, createdAt, validUntil)
    {
        if (occurrenceId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        OccurrenceId = occurrenceId;
    }
}

public sealed record ElementAffordanceCandidateEvidence : SemanticCandidateEvidence
{
    public override SemanticEvidenceKind EvidenceKind => SemanticEvidenceKind.ElementAffordance;
    public override string OccurrenceId { get; }
    public ElementAffordanceKind AffordanceKind { get; }

    public ElementAffordanceCandidateEvidence(
        string occurrenceId, ElementAffordanceKind affordanceKind, SemanticSymbolReference meaning, SemanticObservationReference observation,
        SemanticScopeReference scope, SemanticProvenance provenance, double confidence,
        DateTimeOffset createdAt, DateTimeOffset validUntil)
        : base(meaning, observation, scope, provenance, confidence, createdAt, validUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        if (!Enum.IsDefined(affordanceKind)) throw new ArgumentOutOfRangeException(nameof(affordanceKind));
        OccurrenceId = occurrenceId;
        AffordanceKind = affordanceKind;
    }

    public ElementAffordanceCandidateEvidence(
        string occurrenceId, SemanticSymbolReference meaning, SemanticObservationReference observation,
        SemanticScopeReference scope, SemanticProvenance provenance, double confidence,
        DateTimeOffset createdAt, DateTimeOffset validUntil)
        : this(occurrenceId, ElementAffordanceKind.NavigationCandidate, meaning, observation, scope, provenance,
            confidence, createdAt, validUntil) { }
}

public sealed record ContainerRelationCandidateEvidence : SemanticCandidateEvidence
{
    public override SemanticEvidenceKind EvidenceKind => SemanticEvidenceKind.ContainerRelation;
    public override string OccurrenceId => RelatedOccurrenceId;
    public string RelatedOccurrenceId { get; }
    public ContainerRelationKind RelationKind { get; }
    public SemanticSymbolReference RelatedContainer { get; }

    public ContainerRelationCandidateEvidence(
        string relatedOccurrenceId, ContainerRelationKind relationKind, SemanticSymbolReference relatedContainer,
        SemanticSymbolReference meaning, SemanticObservationReference observation,
        SemanticScopeReference scope, SemanticProvenance provenance, double confidence,
        DateTimeOffset createdAt, DateTimeOffset validUntil)
        : base(meaning, observation, scope, provenance, confidence, createdAt, validUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relatedOccurrenceId);
        ArgumentNullException.ThrowIfNull(relatedContainer);
        if (!Enum.IsDefined(relationKind)) throw new ArgumentOutOfRangeException(nameof(relationKind));
        RelatedOccurrenceId = relatedOccurrenceId;
        RelationKind = relationKind;
        RelatedContainer = relatedContainer;
    }

    public ContainerRelationCandidateEvidence(
        string relatedOccurrenceId, SemanticSymbolReference meaning, SemanticObservationReference observation,
        SemanticScopeReference scope, SemanticProvenance provenance, double confidence,
        DateTimeOffset createdAt, DateTimeOffset validUntil)
        : this(relatedOccurrenceId, ContainerRelationKind.Child, meaning, meaning, observation, scope, provenance,
            confidence, createdAt, validUntil) { }
}

/// <summary>Requirement input only; it is not observation evidence or completion.</summary>
public sealed record CoverageRequirementEvidence
{
    public string RequirementId { get; }
    public SemanticSymbolReference Criterion { get; }
    public SemanticScopeReference Scope { get; }
    public ImmutableArray<SemanticEvidenceKind> RequiredEvidenceKinds { get; }
    public string ProtocolVersion => SemanticEvidenceV2Protocol.Version;
    public double Confidence { get; }
    public SemanticObservationReference Observation { get; }
    public SemanticProvenance Provenance { get; }

    public CoverageRequirementEvidence(
        string requirementId, SemanticSymbolReference criterion,
        SemanticScopeReference scope, IEnumerable<SemanticEvidenceKind> requiredEvidenceKinds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requirementId);
        ArgumentNullException.ThrowIfNull(criterion);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requiredEvidenceKinds);
        var kinds = requiredEvidenceKinds.ToImmutableArray();
        if (kinds.IsDefaultOrEmpty || kinds.Any(kind => !Enum.IsDefined(kind)))
            throw new ArgumentException("At least one evidence kind is required.", nameof(requiredEvidenceKinds));
        RequirementId = requirementId;
        Criterion = criterion;
        Scope = scope;
        RequiredEvidenceKinds = kinds;
        Confidence = 1d;
        Observation = new SemanticObservationReference("requirement", 0, "requirement-frame");
        Provenance = new SemanticProvenance("requirement", SemanticSourceTier.Primary, "requirement", DateTimeOffset.UnixEpoch, "requirement-frame");
    }
}

public sealed record SemanticEvidenceV2Envelope
{
    public string EvidenceId { get; }
    public string ProtocolVersion { get; init; }
    public SemanticSymbolReference Meaning { get; init; }
    public SemanticObservationReference Observation { get; }
    public SemanticScopeReference Scope { get; }
    public SemanticProvenance Provenance { get; }
    public SemanticCandidateEvidence Candidate { get; }
    public SemanticEvidenceKind EvidenceKind => Candidate.EvidenceKind;

    public SemanticEvidenceV2Envelope(string evidenceId, SemanticCandidateEvidence candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        ArgumentNullException.ThrowIfNull(candidate);
        EvidenceId = evidenceId;
        ProtocolVersion = SemanticEvidenceV2Protocol.Version;
        Meaning = candidate.Meaning;
        Observation = candidate.Observation;
        Scope = candidate.Scope;
        Provenance = candidate.Provenance;
        Candidate = candidate;
    }
}

public enum SemanticEvidenceAdmissionFailure
{
    UnsupportedProtocol,
    ManifestMismatch,
    UnregisteredSymbol,
    InvalidSourceTier,
    StaleObservation,
    FrameMismatch,
    ScopeMismatch,
    InvalidProvenance,
    InvalidCandidate,
}

public sealed record SemanticEvidenceAdmissionResult(
    bool Accepted,
    SemanticEvidenceAdmissionFailure? Failure,
    SemanticEvidenceV2Envelope? Evidence)
{
    public static SemanticEvidenceAdmissionResult Reject(SemanticEvidenceAdmissionFailure failure) => new(false, failure, null);
    public static SemanticEvidenceAdmissionResult Admit(SemanticEvidenceV2Envelope evidence) => new(true, null, evidence);
}

public sealed record SemanticEvidenceAdmissionContext
{
    public SemanticCapabilityManifest Manifest { get; }
    public SemanticObservationReference CurrentObservation { get; }
    public DateTimeOffset Now { get; }
    public SemanticSourceTier MaximumPermittedTier { get; }
    public ImmutableArray<SemanticSourceMetadata> Sources { get; }
    public ImmutableArray<SemanticObservationFact> Facts { get; }

    public SemanticEvidenceAdmissionContext(
        SemanticCapabilityManifest manifest,
        SemanticObservationReference currentObservation,
        DateTimeOffset now,
        SemanticSourceTier maximumPermittedTier,
        IEnumerable<SemanticSourceMetadata>? sources = null,
        IEnumerable<SemanticObservationFact>? facts = null)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        CurrentObservation = currentObservation ?? throw new ArgumentNullException(nameof(currentObservation));
        Now = now;
        MaximumPermittedTier = maximumPermittedTier;
        Sources = (sources ?? Array.Empty<SemanticSourceMetadata>()).ToImmutableArray();
        Facts = (facts ?? Array.Empty<SemanticObservationFact>()).ToImmutableArray();
    }
}

public interface IExternalSemanticCapability
{
    SemanticCapabilityManifest Manifest { get; }
    ValueTask<ImmutableArray<SemanticEvidenceV2Envelope>> InterpretAsync(
        ExternalSemanticCapabilityContext context, CancellationToken cancellationToken = default);
}

public static class SemanticEvidenceV2Admission
{
    public static SemanticEvidenceAdmissionResult Admit(
        SemanticEvidenceV2Envelope evidence, SemanticEvidenceAdmissionContext context)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(evidence.ProtocolVersion, SemanticEvidenceV2Protocol.Version, StringComparison.Ordinal))
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.UnsupportedProtocol);
        if (!context.Manifest.Contains(evidence.Meaning))
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.ManifestMismatch);
        if (evidence.Candidate.Meaning != evidence.Meaning)
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.UnregisteredSymbol);
        if (!Enum.IsDefined(evidence.EvidenceKind) ||
            (evidence.EvidenceKind == SemanticEvidenceKind.ContainerIdentity && evidence.Candidate is not ContainerIdentityCandidateEvidence) ||
            (evidence.EvidenceKind == SemanticEvidenceKind.ElementAffordance && evidence.Candidate is not ElementAffordanceCandidateEvidence) ||
            (evidence.EvidenceKind == SemanticEvidenceKind.ContainerRelation && evidence.Candidate is not ContainerRelationCandidateEvidence))
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.InvalidCandidate);
        if (evidence.Observation != context.CurrentObservation)
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.StaleObservation);
        if (!string.Equals(evidence.Observation.FrameId, evidence.Provenance.FrameId, StringComparison.Ordinal))
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.FrameMismatch);
        if (!Enum.IsDefined(evidence.Candidate.Provenance.Tier) ||
            !Enum.IsDefined(evidence.Candidate.Scope.Kind))
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.InvalidCandidate);
        if (evidence.Provenance.Tier > context.MaximumPermittedTier)
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.InvalidSourceTier);
        if (!context.Facts.IsDefaultOrEmpty)
        {
            var requiresOccurrence = evidence.Candidate is ElementAffordanceCandidateEvidence or ContainerRelationCandidateEvidence;
            if (requiresOccurrence && string.IsNullOrWhiteSpace(evidence.Candidate.OccurrenceId))
                return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.InvalidCandidate);
            if (!requiresOccurrence)
                goto SkipOccurrenceCorrelation;
            var fact = context.Facts.FirstOrDefault(item =>
                string.Equals(item.OccurrenceId, evidence.Candidate.OccurrenceId, StringComparison.Ordinal));
            if (fact is null || !string.Equals(fact.SourceId, evidence.Provenance.SourceId, StringComparison.Ordinal) ||
                fact.SourceTier != evidence.Provenance.Tier || fact.ObservationSequence != evidence.Observation.Sequence ||
                !string.Equals(fact.FrameId, evidence.Provenance.FrameId, StringComparison.Ordinal) ||
                !context.Sources.Any(source => source.Available && source.SourceId == fact.SourceId && source.Tier == fact.SourceTier))
                return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.InvalidProvenance);
        }
        SkipOccurrenceCorrelation:
        if (string.IsNullOrWhiteSpace(evidence.Provenance.SourceId) ||
            string.IsNullOrWhiteSpace(evidence.Provenance.CaptureId) ||
            string.IsNullOrWhiteSpace(evidence.Provenance.FrameId))
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.InvalidProvenance);
        var sourceMetadata = context.Sources.FirstOrDefault(
            candidate => string.Equals(candidate.SourceId, evidence.Provenance.SourceId, StringComparison.Ordinal));
        if (sourceMetadata is null || !sourceMetadata.Available ||
            sourceMetadata.Tier != evidence.Provenance.Tier ||
            !string.Equals(sourceMetadata.FrameId, evidence.Provenance.FrameId, StringComparison.Ordinal))
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.InvalidProvenance);
        if (evidence.Candidate.ValidUntil < context.Now || evidence.Candidate.CreatedAt > context.Now)
            return SemanticEvidenceAdmissionResult.Reject(SemanticEvidenceAdmissionFailure.StaleObservation);
        return SemanticEvidenceAdmissionResult.Admit(evidence);
    }
}

#pragma warning restore CS1591
