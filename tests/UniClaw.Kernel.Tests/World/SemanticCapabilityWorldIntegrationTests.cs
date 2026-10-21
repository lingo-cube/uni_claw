using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class SemanticCapabilityWorldIntegrationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

    private static SemanticPerceptionRequest Request(string basis) => new("semantic-request",
        new CapabilityCorrelation("session:test", captureId: "capture:a", observationCycleId: "cycle:a"),
        new("capture:a", "cycle:a", "session:test", T0,
            CoordinateSpace.DeviceViewport(1000, 1000, captureId: "capture:a"),
            [new("title", "text", "Details", new UiBounds(0, 0, 100, 50))], [basis]),
        [new("details", "Details", "Observed details page"), new("other", "Other", "Another page")],
        Intent: new("other", "Caller intended another page"));

    private static (EvidenceLedger Ledger, WorldModel World, UniKernel Kernel) Compose()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.semantic.page" },
            new ProductAssociationStrategy());
        return (ledger, world, new UniKernel(ledger, world, DisabledRunTrace.Instance));
    }

    private static KernelResult Page(UniKernel kernel, string signature, string capture, int second) =>
        kernel.Process(new(new("ui.screen", signature), IngressKind.Observation, ObservationContext.External,
            new("page-observer", T0.AddSeconds(second), "scope:" + capture, ["capture:" + capture])));

    [Theory]
    [InlineData("supplier-a")]
    [InlineData("supplier-b")]
    public async Task RegistryReplacement_UsesOneL1ToP2ToWorldChain_IntentMismatchStaysSeparate(string supplier)
    {
        var (ledger, world, kernel) = Compose();
        var page = Page(kernel, "entity:details", "capture:a", 0);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        var request = Request(page.Admission.EvidenceId!);
        var capability = new UniPerceptionCapability().WithSemanticExecutor((input, _) =>
            ValueTask.FromResult(SemanticPerceptionResult.Observed(input, "details", "Details",
                expectedMatch: SemanticExpectedMatch.Mismatch, source: supplier)));
        var registry = new CapabilityRegistry(TrustDomain.Product);
        registry.Register(capability, "semantic-world-test");
        var consumer = Assert.IsAssignableFrom<ISemanticPerception>(registry.Resolve("uni.perception"));

        var result = await consumer.ObserveSemanticsAsync(request);
        var proposal = Assert.Single(SemanticObservationProjector.CreateProposals(request, result));
        var accepted = kernel.Process(proposal);

        Assert.Equal(SemanticExpectedMatch.Mismatch, result.ExpectedMatch);
        Assert.Equal(AdmissionDecision.Accepted, accepted.Admission.Decision);
        Assert.Equal(supplier, result.Assessment.SourcePath);
        var meaning = world.DeriveSlice(container).ScopedClaims[container + ".semantic.page"];
        Assert.Contains("details", meaning);
        Assert.DoesNotContain("Caller intended", meaning);
        Assert.Equal(container, world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value);
        Assert.Equal("entity:details", world.Current.WorldState[WorldModel.SignatureSubjectPrefix + container].Value);
        Assert.Single(world.Current.Containers!);
        Assert.Empty(world.Current.Relations!);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Contains(accepted.Admission.EvidenceId!, ledger.CanonicalRecords.Keys);
    }

    [Fact]
    public async Task ProjectedLateResult_IsAcceptedButCannotAnnotateTheNewPage()
    {
        var (_, world, kernel) = Compose();
        var first = Page(kernel, "entity:a", "capture:a", 0);
        var request = Request(first.Admission.EvidenceId!);
        ISemanticPerception capability = new UniPerceptionCapability().WithSemanticExecutor((input, _) =>
            ValueTask.FromResult(SemanticPerceptionResult.Observed(input, "details", "Details")));
        var result = await capability.ObserveSemanticsAsync(request);
        Page(kernel, "entity:b", "capture:b", 2);
        var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;

        var accepted = kernel.Process(Assert.Single(SemanticObservationProjector.CreateProposals(request, result)));

        Assert.Equal(AdmissionDecision.Accepted, accepted.Admission.Decision);
        Assert.Contains(accepted.Admission.EvidenceId!, world.Current!.EvidenceBasis);
        Assert.Equal(container, world.Current.WorldState[WorldModel.CurrentContainerSubject].Value);
        Assert.Empty(world.DeriveSlice(container).ScopedClaims);
        Assert.False(world.Current.WorldState.ContainsKey("ui.semantic.page"));
        Assert.Empty(world.Current.Relations!);
        Assert.Empty(kernel.EffectReceipts);
    }
}
