using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Frozen;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>Observer-owned allowlist for page-context resource identifiers.</summary>
public sealed class NativeContextSignatureProfile
{
    public string ProfileRef { get; }
    public string DeviceId { get; }
    public string PackageName { get; }
    public UiHierarchyAcquirerKind AcquirerKind { get; }
    public string AcquirerVersion { get; }
    public IReadOnlySet<string> ContextResourceIds { get; }
    public IReadOnlyDictionary<string, SemanticPageCandidate> StablePageMeanings { get; }
    public string Fingerprint { get; }

    public NativeContextSignatureProfile(string profileRef, string deviceId, string packageName,
        UiHierarchyAcquirerKind acquirerKind, string acquirerVersion, IEnumerable<string> contextResourceIds,
        IReadOnlyDictionary<string, SemanticPageCandidate>? stablePageMeanings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(acquirerVersion);
        ArgumentNullException.ThrowIfNull(contextResourceIds);
        var ids = contextResourceIds.ToArray();
        if (ids.Length is 0 or > 128 || ids.Any(string.IsNullOrWhiteSpace)
            || ids.Any(id => id.Length > 512) || !Enum.IsDefined(acquirerKind))
            throw new ArgumentException("native context profile must contain 1..128 bounded resource IDs", nameof(contextResourceIds));
        var meanings = (stablePageMeanings ?? new Dictionary<string, SemanticPageCandidate>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (meanings.Count > ids.Length || meanings.Any(pair => !ids.Contains(pair.Key, StringComparer.Ordinal)
            || pair.Value is null || InvalidMeaning(pair.Value))
            || meanings.Values.GroupBy(value => value.Key, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new ArgumentException("stable page meanings must be valid, uniquely keyed mappings for configured context resources", nameof(stablePageMeanings));
        ProfileRef = profileRef;
        DeviceId = deviceId;
        PackageName = packageName;
        AcquirerKind = acquirerKind;
        AcquirerVersion = acquirerVersion;
        ContextResourceIds = ids.ToFrozenSet(StringComparer.Ordinal);
        StablePageMeanings = meanings.ToFrozenDictionary(StringComparer.Ordinal);
        Fingerprint = ComputeFingerprint(this);
    }

    private static bool InvalidMeaning(SemanticPageCandidate value) =>
        InvalidField(value.Key) || InvalidField(value.Name) || InvalidField(value.Purpose);

    private static bool InvalidField(string? value) => string.IsNullOrWhiteSpace(value)
        || string.Equals(value.Trim(), "unknown", StringComparison.OrdinalIgnoreCase);

    private static string ComputeFingerprint(NativeContextSignatureProfile profile) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile.ProfileRef, profile.DeviceId, profile.PackageName,
            AcquirerKind = profile.AcquirerKind.ToString(), profile.AcquirerVersion,
            ContextResourceIds = profile.ContextResourceIds.Order(StringComparer.Ordinal),
            StablePageMeanings = profile.StablePageMeanings.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new { ResourceId = pair.Key, pair.Value.Key, pair.Value.Name, pair.Value.Purpose }),
        }))).ToLowerInvariant();
}

/// <summary>Projects ordinary typed evidence and marks only explicitly profiled native context anchors.</summary>
public static class NativeContextObservationProjector
{
    private const string SemanticPageSubject = "ui.semantic.page";
    private const string ProfileRefPrefix = "native-profile-ref:";
    private const string ProfileHashPrefix = "native-profile-sha256:";

    public static IReadOnlyList<ObservationProposal> Project(UiHierarchyObservation observation,
        NativeContextSignatureProfile? profile, ObservationContext context)
    {
        var proposals = TypedHierarchyProposalProjector.Project(observation, context);
        if (profile is null || !ScopeMatches(observation.Metadata, profile)) return proposals;

        var eligibleNodeIds = observation.Nodes.Where(node =>
                node.Package.TryGetObserved(out var package) && package == profile.PackageName
                && node.ResourceId.TryGetObserved(out var resourceId)
                && profile.ContextResourceIds.Contains(resourceId))
            .Select(node => node.OccurrenceRef.LocalIndex).ToFrozenSet();
        if (eligibleNodeIds.Count == 0) return proposals;
        var profileHash = profile.Fingerprint;
        return proposals.Select(proposal =>
        {
            if (!TryResourceNodeIndex(proposal.Claim.Subject, observation.Metadata.CaptureId, out var index)
                || !eligibleNodeIds.Contains(index)) return proposal;
            var provenance = proposal.Provenance with
            {
                TransformationLineage = proposal.Provenance.TransformationLineage
                    .Concat([PageInterpretationCodec.NativeContextProfile, ProfileRefPrefix + profile.ProfileRef,
                        ProfileHashPrefix + profileHash]).ToArray(),
            };
            return proposal with { Provenance = provenance };
        }).ToArray();
    }

    private static bool ScopeMatches(CaptureMetadata metadata, NativeContextSignatureProfile profile) =>
        metadata.DeviceId == profile.DeviceId && metadata.AcquirerKind == profile.AcquirerKind
        && metadata.AcquirerVersion == profile.AcquirerVersion;

    /// <summary>Creates a verification proposal only when four accepted source records agree exactly.</summary>
    public static ObservationProposal? VerifySemantic(EvidenceRecord semantic, EvidenceRecord capture,
        EvidenceRecord identity, EvidenceRecord nativeContext, NativeContextSignatureProfile profile)
    {
        ArgumentNullException.ThrowIfNull(semantic);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(nativeContext);
        ArgumentNullException.ThrowIfNull(profile);

        if (new[] { semantic, capture, identity, nativeContext }.Select(record => record.EvidenceId)
                .Distinct(StringComparer.Ordinal).Count() != 4
            || new[] { semantic, capture, identity, nativeContext }.Any(record =>
                record.Kind != IngressKind.Observation || record.Context != ObservationContext.External
                || record.Origin != ObservationOrigin.External)) return null;

        if (capture.Claim.Subject != PageObservationCodec.CaptureSubject
            || !PageObservationCodec.TryReadCapture(capture.Claim.Value, out var pageCapture) || pageCapture is null
            || identity.Claim.Subject != PageObservationCodec.IdentitySubject
            || !PageObservationCodec.TryReadIdentity(identity.Claim.Value, out var pageIdentity) || pageIdentity is null
            || semantic.Claim.Subject != SemanticPageSubject
            || !TryReadSemantic(semantic.Claim.Value, out var meaning) || meaning is null) return null;

        var captureTime = pageCapture.CaptureTime;
        if (pageCapture.DeviceId != profile.DeviceId || pageCapture.CaptureId != pageIdentity.CaptureId
            || pageCapture.ObservationCycleId != pageIdentity.ObservationCycleId
            || pageCapture.SessionCorrelation != pageIdentity.SessionCorrelation
            || captureTime != pageIdentity.CaptureTime || pageCapture.Items.Count < 2
            || string.IsNullOrWhiteSpace(pageIdentity.ObservedTitle)
            || !pageCapture.Items.Any(item => string.Equals(item.Text?.Trim(), pageIdentity.ObservedTitle?.Trim(), StringComparison.Ordinal))
            || capture.Provenance.CaptureTime != captureTime || identity.Provenance.CaptureTime != captureTime
            || semantic.Provenance.CaptureTime != captureTime
            || meaning.CaptureId != pageCapture.CaptureId
            || meaning.ObservationCycleId != pageCapture.ObservationCycleId
            || meaning.SessionCorrelation != pageCapture.SessionCorrelation
            || !HasBasis(semantic, capture.EvidenceId) || !HasBasis(identity, capture.EvidenceId)) return null;

        var descriptor = nativeContext.Provenance.Hierarchy;
        if (descriptor is not { Field: "resource_id" }
            || nativeContext.Provenance.Producer != TypedHierarchyProposalProjector.Producer
            || nativeContext.DerivedFromEvidenceIds.Count != 0 || nativeContext.TransitiveEvidenceBasis.Count != 0
            || !nativeContext.Provenance.TransformationLineage.Contains(PageInterpretationCodec.NativeContextProfile)
            || !nativeContext.Provenance.TransformationLineage.Contains(ProfileRefPrefix + profile.ProfileRef)
            || !nativeContext.Provenance.TransformationLineage.Contains(ProfileHashPrefix + profile.Fingerprint)
            || descriptor.CaptureId != pageCapture.CaptureId
            || descriptor.DeviceId != pageCapture.DeviceId || descriptor.SessionCorrelation != pageCapture.SessionCorrelation
            || descriptor.ObservationCycleId != pageCapture.ObservationCycleId
            || descriptor.CaptureTimestamp != captureTime || descriptor.Space != pageCapture.Space
            || descriptor.AcquirerKind != profile.AcquirerKind || descriptor.AcquirerVersion != profile.AcquirerVersion
            || nativeContext.Provenance.CaptureTime != captureTime
            || nativeContext.Claim.Subject != $"ui.node.{descriptor.CaptureId}#{descriptor.NodeLocalIndex}.resource_id"
            || pageIdentity.Signature != nativeContext.Claim.Value
            || !profile.ContextResourceIds.Contains(nativeContext.Claim.Value)
            || !profile.StablePageMeanings.TryGetValue(nativeContext.Claim.Value, out var expected)
            || meaning.PageKey != expected.Key || meaning.PageName != expected.Name
            || meaning.PagePurpose != expected.Purpose) return null;

        var sourceIds = new[] { semantic.EvidenceId, capture.EvidenceId, identity.EvidenceId, nativeContext.EvidenceId };
        static IEnumerable<string> Leaves(EvidenceRecord record) => record.TransitiveEvidenceBasis.Count == 0
            ? Enumerable.Repeat(record.EvidenceId, 1) : record.TransitiveEvidenceBasis;
        var leafBasis = new[] { semantic, capture, identity, nativeContext }
            .SelectMany(Leaves)
            .ToFrozenSet(StringComparer.Ordinal);
        var value = SemanticVerificationCodec.Encode(new(semantic.EvidenceId, capture.EvidenceId,
            identity.EvidenceId, nativeContext.EvidenceId, profile.Fingerprint));
        return new(new(SemanticVerificationCodec.Subject, value), IngressKind.Observation,
            ObservationContext.External,
            new("native-context-verifier", captureTime, $"semantic-verification:{pageCapture.CaptureId}",
                ["semantic-native-profile-verification:v1", ProfileRefPrefix + profile.ProfileRef,
                    ProfileHashPrefix + profile.Fingerprint]),
            Origin: ObservationOrigin.External)
        {
            DerivedFromEvidenceIds = sourceIds,
            TransitiveEvidenceBasis = leafBasis,
            FusionRule = "semantic.native-profile-verification",
            FusionVersion = "1",
        };
    }

    private static bool TryReadSemantic(string value, out SemanticPageClaim? claim)
    {
        try
        {
            claim = JsonSerializer.Deserialize<SemanticPageClaim>(value,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
            return claim is not null;
        }
        catch (JsonException) { claim = null; return false; }
    }

    private static bool HasBasis(EvidenceRecord record, string evidenceId) =>
        record.DerivedFromEvidenceIds.Contains(evidenceId) || record.TransitiveEvidenceBasis.Contains(evidenceId);

    private static bool TryResourceNodeIndex(string subject, string captureId, out int index)
    {
        var prefix = $"ui.node.{captureId}#";
        index = -1;
        if (!subject.StartsWith(prefix, StringComparison.Ordinal) || !subject.EndsWith(".resource_id", StringComparison.Ordinal)) return false;
        var raw = subject[prefix.Length..^".resource_id".Length];
        return int.TryParse(raw, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out index);
    }

}
