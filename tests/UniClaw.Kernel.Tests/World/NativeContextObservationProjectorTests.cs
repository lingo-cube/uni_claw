using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using System.Text.Json;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class NativeContextObservationProjectorTests
{
    private static readonly DateTimeOffset CapturedAt = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
    private const string Device = "fixture-device";
    private const string Package = "com.example.settings";

    [Fact]
    public void Project_MarksOnlyConfiguredContextResourceIdsAndPreservesTypedSource()
    {
        var input = Observation();
        var profile = Profile();
        var ordinary = TypedHierarchyProposalProjector.Project(input, ObservationContext.External);
        var projected = NativeContextObservationProjector.Project(input, profile, ObservationContext.External);
        var originals = ordinary.ToDictionary(p => p.Claim.Subject);
        var resourceFields = projected.Where(p => p.Claim.Subject.EndsWith(".resource_id", StringComparison.Ordinal)).ToArray();

        Assert.Equal(3, resourceFields.Length);
        Assert.Equal(2, resourceFields.Count(p => p.Provenance.TransformationLineage.Contains(PageInterpretationCodec.NativeContextProfile)));
        Assert.All(resourceFields, p =>
        {
            var original = originals[p.Claim.Subject];
            Assert.Equal(original.Claim, p.Claim);
            Assert.Equal(original.Provenance.CaptureTime, p.Provenance.CaptureTime);
            Assert.Equal(original.Provenance.Hierarchy, p.Provenance.Hierarchy);
            if (p.Claim.Value != "com.example.settings:id/save")
            {
                Assert.Contains("native-profile-ref:settings-page:v1", p.Provenance.TransformationLineage);
                Assert.Contains(p.Provenance.TransformationLineage, value => value.StartsWith("native-profile-sha256:", StringComparison.Ordinal));
            }
        });
        Assert.DoesNotContain(resourceFields.Single(p => p.Claim.Value == "com.example.settings:id/save")
            .Provenance.TransformationLineage, value => value.StartsWith("native-profile-ref:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("device")]
    [InlineData("package")]
    [InlineData("acquirer")]
    [InlineData("version")]
    [InlineData("no-profile")]
    public void Project_LeavesOrdinaryTypedFieldsUnmarkedWhenProfileScopeDoesNotMatch(string mismatch)
    {
        var input = Observation(mismatch == "package" ? "com.example.other" : Package,
            mismatch == "device" ? "other-device" : Device,
            mismatch == "acquirer" ? UiHierarchyAcquirerKind.AccessibilityNodeInfo : UiHierarchyAcquirerKind.AndroidXUiAutomator,
            mismatch == "version" ? "other-version" : "androidx-uiautomator/1");
        var profile = mismatch == "no-profile" ? null : Profile();
        var ordinary = TypedHierarchyProposalProjector.Project(input, ObservationContext.External);
        var projected = NativeContextObservationProjector.Project(input, profile, ObservationContext.External);

        Assert.Equal(ordinary.Count, projected.Count);
        Assert.All(projected, proposal => Assert.DoesNotContain(PageInterpretationCodec.NativeContextProfile,
            proposal.Provenance.TransformationLineage));
        Assert.Equal(ordinary.Select(p => p.Claim), projected.Select(p => p.Claim));
    }

    [Fact]
    public void Profile_DefensivelyCopiesAndRejectsEmptyOrOversizedAllowlist()
    {
        var input = new List<string> { "com.example.settings:id/home" };
        var profile = Profile(input);
        input.Clear();
        Assert.Contains("com.example.settings:id/home", profile.ContextResourceIds);
        Assert.Throws<ArgumentException>(() => Profile(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => Profile(Enumerable.Range(0, 129).Select(i => "id:" + i)));
    }

    [Fact]
    public void Profile_DefensivelyCopiesMeaningMapAndFingerprintCommitsToIt()
    {
        var meanings = new Dictionary<string, SemanticPageCandidate>
        {
            ["com.example.settings:id/home"] = new("settings-home", "Settings", "Settings home"),
        };
        var profile = Profile(meanings: meanings);
        var same = Profile(meanings: new Dictionary<string, SemanticPageCandidate>
        {
            ["com.example.settings:id/home"] = new("settings-home", "Settings", "Settings home"),
        });
        meanings["com.example.settings:id/home"] = new("other", "Other", "Other page");

        Assert.Equal(same.Fingerprint, profile.Fingerprint);
        Assert.NotSame(meanings, profile.StablePageMeanings);
        Assert.IsNotType<Dictionary<string, SemanticPageCandidate>>(profile.StablePageMeanings);
        Assert.Equal("settings-home", profile.StablePageMeanings["com.example.settings:id/home"].Key);
        Assert.NotEqual(profile.Fingerprint, Profile(meanings: new Dictionary<string, SemanticPageCandidate>
        {
            ["com.example.settings:id/home"] = new("settings-home", "Settings", "Different purpose"),
        }).Fingerprint);
        Assert.Throws<ArgumentException>(() => Profile(meanings: new Dictionary<string, SemanticPageCandidate>
        {
            ["com.example.settings:id/unknown"] = new("settings-home", "Settings", "Settings home"),
        }));
        Assert.Throws<ArgumentException>(() => Profile(meanings: new Dictionary<string, SemanticPageCandidate>
        {
            ["com.example.settings:id/home"] = new("same", "Settings", "Settings home"),
            ["com.example.settings:id/dialog"] = new("same", "Dialog", "Settings dialog"),
        }));
    }

    [Fact]
    public void VerifySemantic_RequiresExactProfileMappingAndReturnsFourSourceBoundProposal()
    {
        var profile = Profile(meanings: MeaningMap("settings-home", "Settings", "Settings home"));
        var (semantic, capture, identity, native) = VerificationSources(profile);

        var proposal = NativeContextObservationProjector.VerifySemantic(semantic, capture, identity, native, profile);

        Assert.NotNull(proposal);
        Assert.Equal(SemanticVerificationCodec.Subject, proposal!.Claim.Subject);
        Assert.True(SemanticVerificationCodec.TryRead(proposal.Claim.Value, out var verification));
        Assert.Equal(semantic.EvidenceId, verification!.SemanticEvidenceId);
        Assert.Equal(capture.EvidenceId, verification.SourceCaptureEvidenceId);
        Assert.Equal(identity.EvidenceId, verification.SourceIdentityEvidenceId);
        Assert.Equal(native.EvidenceId, verification.NativeContextEvidenceId);
        Assert.Equal(profile.Fingerprint, verification.ProfileFingerprint);
        Assert.Equal(new[] { semantic.EvidenceId, capture.EvidenceId, identity.EvidenceId, native.EvidenceId },
            proposal.DerivedFromEvidenceIds);
        Assert.Equal(new[] { capture.EvidenceId, native.EvidenceId }.Order(StringComparer.Ordinal),
            proposal.TransitiveEvidenceBasis.Order(StringComparer.Ordinal));
        Assert.Equal(CapturedAt, proposal.Provenance!.CaptureTime);
        Assert.Equal("native-context-verifier", proposal.Provenance.Producer);
        Assert.Equal("1", proposal.FusionVersion);
    }

    [Theory]
    [InlineData("wrong-meaning")]
    [InlineData("wrong-device")]
    [InlineData("wrong-capture")]
    [InlineData("wrong-package")]
    public void VerifySemantic_RejectsMismatchedMeaningOrSourceCorrelation(string mismatch)
    {
        var profile = Profile(meanings: MeaningMap("settings-home", "Settings", "Settings home"));
        var sources = VerificationSources(profile, mismatch);

        Assert.Null(NativeContextObservationProjector.VerifySemantic(
            sources.Semantic, sources.Capture, sources.Identity, sources.Native, profile));
    }

    private static NativeContextSignatureProfile Profile(IEnumerable<string>? ids = null,
        IReadOnlyDictionary<string, SemanticPageCandidate>? meanings = null) =>
        new("settings-page:v1", Device, Package, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "androidx-uiautomator/1", ids ?? ["com.example.settings:id/home", "com.example.settings:id/dialog"], meanings);

    private static IReadOnlyDictionary<string, SemanticPageCandidate> MeaningMap(string key, string name, string purpose) =>
        new Dictionary<string, SemanticPageCandidate>
        { ["com.example.settings:id/home"] = new(key, name, purpose) };

    private static (EvidenceRecord Semantic, EvidenceRecord Capture, EvidenceRecord Identity, EvidenceRecord Native)
        VerificationSources(NativeContextSignatureProfile profile, string mismatch = "")
    {
        var capturePayload = new PageCaptureObservation("capture-1", "cycle-1", "session-1", CapturedAt,
            CoordinateSpace.DeviceViewport(100, 100, captureId: "capture-1"),
            [new("title", "text", "Settings", new(0, 0, 20, 10)), new("body", "text", "Home", new(0, 10, 90, 90))])
        { DeviceId = mismatch == "wrong-device" ? "other-device" : Device };
        var captureId = "capture-evidence";
        var capture = Source(captureId, PageObservationCodec.CaptureSubject,
            PageObservationCodec.Encode(capturePayload), new("host.capture", CapturedAt, "capture:capture-1", []));
        var identityPayload = new PageIdentityObservation("capture-1", "cycle-1", "session-1", CapturedAt,
            "com.example.settings:id/home", "Settings");
        if (mismatch == "wrong-capture") identityPayload = identityPayload with { CaptureId = "other-capture" };
        var identity = Source("identity-evidence", PageObservationCodec.IdentitySubject,
            PageObservationCodec.Encode(identityPayload), new("page-identity", CapturedAt, "capture:capture-1", []))
        with { DerivedFromEvidenceIds = [captureId], TransitiveEvidenceBasis = new HashSet<string>([captureId], StringComparer.Ordinal) };

        var meaning = mismatch == "wrong-meaning"
            ? new SemanticPageClaim("other-key", "Other", "Other page", "Settings", "capture-1", "cycle-1", "session-1")
            : new SemanticPageClaim("settings-home", "Settings", "Settings home", "Settings", "capture-1", "cycle-1", "session-1");
        var semantic = Source("semantic-evidence", "ui.semantic.page", JsonSerializer.Serialize(meaning,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)), new("semantic", CapturedAt, "capture:capture-1", []))
        with { DerivedFromEvidenceIds = [captureId], TransitiveEvidenceBasis = new HashSet<string>([captureId], StringComparer.Ordinal) };

        var hierarchyObservation = Observation(mismatch == "wrong-package" ? "com.example.other" : Package);
        var proposal = NativeContextObservationProjector.Project(hierarchyObservation, profile, ObservationContext.External)
            .Single(p => p.Claim.Subject == "ui.node.capture-1#0.resource_id");
        var native = new EvidenceRecord("native-evidence", proposal.Claim, proposal.Kind, proposal.Context,
            proposal.Provenance!);
        return (semantic, capture, identity, native);
    }

    private static EvidenceRecord Source(string id, string subject, string value, Provenance provenance) =>
        new(id, new(subject, value), IngressKind.Observation, ObservationContext.External, provenance);

    private static UiHierarchyObservation Observation(string package = Package, string device = Device,
        UiHierarchyAcquirerKind acquirer = UiHierarchyAcquirerKind.AndroidXUiAutomator, string version = "androidx-uiautomator/1")
    {
        var metadata = new CaptureMetadata("capture-1", 35, acquirer, version, UiHierarchyFormat.AccessibilityTree,
            CapturedAt, TimeSpan.FromMilliseconds(20), device, "session-1", "cycle-1",
            new HierarchyCapabilities(HierarchyCapability.SemanticText),
            new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-only"),
            UniClaw.Kernel.Perception.CoordinateSpace.DeviceViewport(100, 100, captureId: "capture-1"));
        var nodes = new[]
        {
            Node(0, "com.example.settings:id/home", package),
            Node(1, "com.example.settings:id/dialog", package, parent: 0),
            Node(2, "com.example.settings:id/save", package, parent: 0),
        };
        return new(metadata, Array.Empty<UiWindowOccurrence>(), nodes);
    }

    private static UiNodeObservation Node(int index, string resourceId, string package, int? parent = null) => new(
        new OccurrenceRef("capture-1", index), parent is { } parentIndex ? new OccurrenceRef("capture-1", parentIndex) : null,
        null, index, null,
        ObservedValue<string>.Observed("android.view.View"), ObservedValue<string>.Observed(resourceId),
        ObservedValue<string>.Observed(package), ObservedValue<string>.Observed("Settings"),
        ObservedValue<string>.Unknown("missing"), ObservedValue<string>.Unknown("missing"),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<CheckedState>.Unknown("missing"),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<bool>.Unknown("missing"), ObservedValue<bool>.Observed(false),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Observed(true),
        ObservedValue<bool>.Observed(true), ObservedValue<bool>.Unknown("missing"),
        ObservedValue<UiBounds>.Observed(new(0, 0, 50, 50)));
}
