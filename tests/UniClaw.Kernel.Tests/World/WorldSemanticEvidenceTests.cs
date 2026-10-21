using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldSemanticEvidenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static EvidenceRecord Accept(EvidenceLedger ledger, WorldModel world, ObservationProposal proposal)
    {
        var (admission, record) = ledger.Admit(proposal);
        Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
        world.Reconcile(record!, world.JudgeRelevance(record!));
        return record!;
    }

    private static EvidenceRecord Page(EvidenceLedger ledger, WorldModel world, string page, string capture, int second) =>
        Accept(ledger, world, new(new("ui.screen", page), IngressKind.Observation, ObservationContext.External,
            new("page-observer", T0.AddSeconds(second), "scope:" + capture, ["capture:" + capture])));

    private static ObservationProposal Semantic(EvidenceRecord source, string capture, string pageKey = "details") =>
        new(new("ui.semantic.page", JsonSerializer.Serialize(
                new SemanticPageClaim(pageKey, "Details", "Observed details page", "Details", capture,
                    "cycle:" + capture, "session:test"), Json)),
            IngressKind.Observation, ObservationContext.External,
            new("semantic-observer", source.Provenance.CaptureTime, "semantic:" + capture, ["semantic-projection"]))
        {
            DerivedFromEvidenceIds = [source.EvidenceId],
            TransitiveEvidenceBasis = new HashSet<string> { source.EvidenceId },
            FusionRule = "semantic-page-observation", FusionVersion = "1",
        };

    private static WorldModel World() => new(new HashSet<string> { "ui.screen", "ui.semantic.page" },
        new ProductAssociationStrategy());

    [Fact]
    public void AcceptedCurrentSemanticEvidence_EnrichesSliceWithoutReplacingIdentitySignature()
    {
        var ledger = new EvidenceLedger();
        var world = World();
        var page = Page(ledger, world, "specific-context", "current", 1);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;

        var semantic = Accept(ledger, world, Semantic(page, "current"));

        var slice = world.DeriveSlice(container);
        Assert.Equal(semantic.Claim.Value, slice.ScopedClaims[container + ".semantic.page"]);
        Assert.Equal("specific-context", world.Current!.WorldState[WorldModel.SignatureSubjectPrefix + container].Value);
        Assert.Equal(container, world.Current.WorldState[WorldModel.CurrentContainerSubject].Value);
        Assert.Single(world.Current.Containers!);
        Assert.Empty(world.Current.Relations!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstSemanticResultForRetiredCapture_DoesNotAnnotateCurrentPage(bool samePage)
    {
        var ledger = new EvidenceLedger();
        var world = World();
        var old = Page(ledger, world, "A", "old", 1);
        Page(ledger, world, samePage ? "A" : "B", "current", 3);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;

        var late = Accept(ledger, world, Semantic(old, "old"));

        Assert.Contains(late.EvidenceId, world.Current!.EvidenceBasis);
        Assert.False(world.Current.WorldState.ContainsKey("ui.semantic.page"));
        Assert.Empty(world.DeriveSlice(container).ScopedClaims);
        Assert.Equal(container, world.Current.WorldState[WorldModel.CurrentContainerSubject].Value);
        Assert.Empty(world.Current.Relations!);
    }

    [Fact]
    public void SameSemanticCategoryOnDifferentObservedEntities_DoesNotMergeContainers()
    {
        var ledger = new EvidenceLedger();
        var world = World();
        var first = Page(ledger, world, "parent-a/details", "a", 1);
        var a = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        var meaningA = Accept(ledger, world, Semantic(first, "a"));
        var second = Page(ledger, world, "parent-b/details", "b", 3);
        var b = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        var meaningB = Accept(ledger, world, Semantic(second, "b"));

        Assert.NotEqual(a, b);
        Assert.Equal(2, world.Current!.Containers!.Count);
        Assert.Equal(meaningA.Claim.Value, world.DeriveSlice(a).ScopedClaims[a + ".semantic.page"]);
        Assert.Equal(meaningB.Claim.Value, world.DeriveSlice(b).ScopedClaims[b + ".semantic.page"]);
        Assert.Empty(world.Current.Relations!);
    }

    [Fact]
    public void ConflictingSemanticEvidence_PreservesMeaningAndCannotChangeIdentity()
    {
        var ledger = new EvidenceLedger();
        var world = World();
        var page = Page(ledger, world, "specific-context", "current", 1);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        var established = Accept(ledger, world, Semantic(page, "current", "details"));

        var contradicting = Accept(ledger, world, Semantic(page, "current", "other"));

        Assert.Equal(established.Claim.Value, world.DeriveSlice(container).ScopedClaims[container + ".semantic.page"]);
        Assert.Contains(world.Current!.Conflicts, conflict => conflict.Subject == "ui.semantic.page"
            && conflict.EstablishedEvidenceId == established.EvidenceId
            && conflict.ChallengingEvidenceId == contradicting.EvidenceId);
        Assert.Equal(container, world.Current.WorldState[WorldModel.CurrentContainerSubject].Value);
        Assert.Single(world.Current.Containers!);
        Assert.Empty(world.Current.Relations!);
    }

    [Fact]
    public void CaptureLabelWithoutMatchingIdentityBasis_CannotAttachSemanticMeaning()
    {
        var ledger = new EvidenceLedger();
        var world = World();
        var source = Page(ledger, world, "A", "source", 1);
        Page(ledger, world, "B", "current", 1);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;

        Accept(ledger, world, Semantic(source, "current"));

        Assert.False(world.Current!.WorldState.ContainsKey("ui.semantic.page"));
        Assert.Empty(world.DeriveSlice(container).ScopedClaims);
    }

    [Fact]
    public void SemanticCategoryCannotMintContainerEvenWhenAssociationPluginProposesNew()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.semantic.page" }, new AlwaysNewStrategy());
        var source = Page(ledger, world, "specific-context", "current", 1);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;

        Accept(ledger, world, Semantic(source, "current"));

        Assert.Single(world.Current!.Containers!);
        Assert.Equal(container, world.Current.WorldState[WorldModel.CurrentContainerSubject].Value);
        Assert.Equal("specific-context", world.Current.WorldState[WorldModel.SignatureSubjectPrefix + container].Value);
        Assert.Equal(AssociationDispositionKind.Insufficient, world.AssociationLog[^1].EffectiveKind);
        Assert.Empty(world.Current.Relations!);
    }

    [Theory]
    [InlineData("aligned", true)]
    [InlineData("capture", false)]
    [InlineData("cycle", false)]
    [InlineData("session", false)]
    [InlineData("time", false)]
    public void TypedCaptureCorrelation_MustAgreeBeforeSemanticAttachment(string variant, bool attached)
    {
        var ledger = new EvidenceLedger();
        var world = World();
        var capture = new HierarchyCaptureDescriptor(
            variant == "capture" ? "other" : "current", 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
            "fixture-v1", UiHierarchyFormat.UiAutomatorXml, "device:fixture",
            variant == "session" ? "session:other" : "session:test",
            variant == "cycle" ? "cycle:other" : "cycle:current",
            variant == "time" ? T0 : T0.AddSeconds(1), null,
            HierarchyCapability.SemanticText, CoverageCompleteness.CompleteWithinDeclaredSurface,
            null, 0, null, "identity");
        var page = Accept(ledger, world, new(new("ui.screen", "specific-context"), IngressKind.Observation,
            ObservationContext.External, new("page-observer", T0.AddSeconds(1), "scope:current", ["fixture"], capture)));
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;

        var semantic = Accept(ledger, world, Semantic(page, "current"));

        Assert.Equal(attached, world.DeriveSlice(container).ScopedClaims.ContainsKey(container + ".semantic.page"));
        Assert.Contains(semantic.EvidenceId, world.Current!.EvidenceBasis);
        Assert.Single(world.Current.Containers!);
        Assert.Empty(world.Current.Relations!);
    }

    [Fact]
    public void UndeclaredIntentFieldInRawSemanticPayload_DoesNotBecomeScopedWorldFact()
    {
        var ledger = new EvidenceLedger();
        var world = World();
        var page = Page(ledger, world, "specific-context", "current", 1);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        var proposal = Semantic(page, "current");
        var raw = JsonNode.Parse(proposal.Claim.Value)!.AsObject();
        raw["expectedPageKey"] = "caller-intent";

        var record = Accept(ledger, world, proposal with
        {
            Claim = new("ui.semantic.page", raw.ToJsonString()),
        });

        Assert.Contains(record.EvidenceId, world.Current!.EvidenceBasis);
        Assert.False(world.Current.WorldState.ContainsKey("ui.semantic.page"));
        Assert.Empty(world.DeriveSlice(container).ScopedClaims);
        Assert.Empty(world.Current.Relations!);
    }

    private sealed class AlwaysNewStrategy : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(AssociationDispositionKind.New,
            null, [new("(new)", [input.Current.EvidenceId], [])], [], "unconditional-new");
    }
}
