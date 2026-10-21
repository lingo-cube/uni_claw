using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class PageIdentityRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    private const string Xml = """
        <hierarchy><node text="SIMs" class="android.widget.TextView" clickable="false" enabled="true" bounds="[10,10][100,40]"/>
        <node text="Mobile network" class="android.widget.Button" clickable="true" enabled="true" bounds="[10,60][180,100]"/></hierarchy>
        """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task XmlOrVisionTitleFlowsThroughRegisteredSemanticCapabilityAndP2BeforeWorldConfirmation(bool xml)
    {
        var fast = new FastTextBasis("c", "session", "cycle", [],
            ["""{"text":"SIMs","boundsPx":[10,10,100,40]}""", """{"text":"Mobile network","boundsPx":[10,60,180,100]}"""], Now);
        var capture = PageIdentityRecovery.Capture("c", "cycle", "session", Now, 200, 200, xml ? Xml : null, fast);
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            "ui.semantic.page" }, new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy());
        var proposal = new ObservationProposal(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.PostActionEffectFlow, new("capture", Now, "capture:c", [xml ? "XML-fixture" : "vision-fixture"]));
        var (_, record) = ledger.Admit(proposal);
        world.Reconcile(record!, world.JudgeRelevance(record!));
        var unknown = world.Current!;
        var providerCalls = 0;
        var capability = new UniPerceptionCapability().WithSemanticExecutor((request, _) =>
        {
            providerCalls++;
            Assert.Null(request.Intent);
            Assert.Null(request.Current.PageKey);
            return ValueTask.FromResult(SemanticPerceptionResult.Observed(request, "sims", "SIMs"));
        });
        var registry = PerceptionCapabilityComposition.RegisterProductPerception(uniPerception: capability);
        var result = await PageIdentityRecovery.RecognizeAsync((ISemanticPerception)registry.Resolve("uni.perception")!,
            [new("sims", "SIMs", "Manage mobile subscriptions")], unknown.ObservationNeed!, TimeSpan.FromSeconds(2),
            ObservationContext.PostActionEffectFlow, CancellationToken.None);
        Assert.Equal("SIMs", result.Title);
        Assert.NotNull(world.Current.ObservationNeed); // model completion alone grants nothing
        var identity = PageIdentityRecovery.Identity(unknown.CurrentPageObservation!, result.Title,
            "context:actual-entry:observed-SIMs", ObservationContext.PostActionEffectFlow)!;
        var accepted = ledger.Admit(identity);
        Assert.Equal(AdmissionDecision.Accepted, accepted.Admission.Decision);
        world.Reconcile(accepted.Record!, world.JudgeRelevance(accepted.Record!));
        Assert.Null(world.Current!.ObservationNeed);
        Assert.NotNull(world.Current.CurrentPageObservation!.ContainerId);
        Assert.NotNull(unknown.ObservationNeed);
        Assert.Empty(world.Current.Relations!); // still no Assurance proof of arrival
        Assert.Equal(1, providerCalls);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-capture")]
    [InlineData("timeout")]
    [InlineData("cancel")]
    public async Task FailedOrUnalignedSemanticResultCannotCreateIdentity(string failure)
    {
        var capture = PageIdentityRecovery.Capture("c", "cycle", "session", Now, 200, 200, Xml, null);
        var need = new ObservationNeed("rev:1", "ev:source", capture);
        var capability = new UniPerceptionCapability().WithSemanticExecutor(async (request, token) =>
        {
            if (failure is "timeout" or "cancel") await Task.Delay(1000, token);
            return failure == "unknown"
                ? SemanticPerceptionResult.Empty(request, CapabilityStatus.Completed, PerceptionDisposition.Unknown, "fixture")
                : SemanticPerceptionResult.Observed(request, "sims", "SIMs") with { CaptureId = "wrong" };
        });
        using var cts = new CancellationTokenSource();
        if (failure == "cancel") cts.Cancel();
        var result = await PageIdentityRecovery.RecognizeAsync(capability, [new("sims", "SIMs", "Subscriptions")],
            need, TimeSpan.FromMilliseconds(20), ObservationContext.External, cts.Token);
        Assert.Null(result.Title);
        Assert.Empty(result.Meaning);
    }
}
