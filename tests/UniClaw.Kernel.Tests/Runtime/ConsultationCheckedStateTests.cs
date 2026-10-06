using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class ConsultationCheckedStateTests
{
    private static readonly DateTimeOffset Captured = new(2026, 10, 6, 3, 0, 0, TimeSpan.Zero);
    private const string Frame = "screen.frame";
    private const string Node = "ui.node.cap-consult#0";

    [Theory]
    [InlineData("checked", "off", "checked")]
    [InlineData("unchecked", "on", "unchecked")]
    [InlineData("partial", "off", "partial")]
    [InlineData(null, "on", null)]
    public void AgentSeesTypedCheckedEvidence_WithoutFallingBackToPresentationState(
        string? typed, string presentation, string? expected)
    {
        var scope = new HashSet<string> { Frame, Node + ".resource_id", Node + ".checked" };
        var ledger = new EvidenceLedger();
        var effects = new EffectBoundary(new NeverDeliver());
        var plan = new AgentPlanPolicy();
        var kernel = new UniKernel(ledger,
            new WorldModel(scope, new SeedContainerAssociationStrategy(), new SwitchOccurrence(presentation)),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()), effects);
        var observations = new List<ObservationProposal>
        {
            new(new ObservationClaim(Frame, "settings"), IngressKind.Observation,
                ObservationContext.External, new Provenance("host.live", Captured, "scope:screen.frame", new[] { "test" })),
            Typed("resource_id", "switch-wifi"),
        };
        if (typed is not null) observations.Add(Typed("checked", typed));
        AgentDecisionContext? capturedContext = null;
        var inputs = new RunDriverInputs
        {
            NextInput = _ => new RunDriverInput.Observation(observations),
            ConsultAgent = context =>
            {
                capturedContext = context;
                return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "inspect-only"));
            },
        };
        Assert.True(kernel.AdmitContract(new ExecutionContract("v0", "inspect Wi-Fi", scope,
            new HashSet<string> { "tap" }, new HashSet<string>(), new[] { "switch-state" },
            new[] { new RunObligation("wifi", RunObligationKind.Objective,
                "ui.role.switch.checked", "checked", true, EntityScope: new TargetDescriptor("switch", "Wi-Fi")) })).Accepted);
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        driver.Drive();

        Assert.NotNull(capturedContext);
        var element = Assert.Single(capturedContext!.Elements!);
        Assert.Equal("switch", element.Role);
        Assert.Equal("Wi-Fi", element.Text);
        Assert.Equal(expected, element.State);
        Assert.DoesNotContain(capturedContext.CurrentWorldClaims.Keys, k => k.StartsWith("ui.node."));
        Assert.Empty(effects.ReceiptLog);
    }

    private static ObservationProposal Typed(string field, string value) => new(
        new ObservationClaim(Node + "." + field, value), IngressKind.Observation, ObservationContext.External,
        new Provenance(TypedHierarchyProposalProjector.Producer, Captured, "scope:" + Node,
            new[] { TypedHierarchyProposalProjector.LineageMarker },
            Hierarchy: new HierarchyCaptureDescriptor("cap-consult", 35,
                UiHierarchyAcquirerKind.LegacyUiAutomatorXml, "1", UiHierarchyFormat.UiAutomatorXml,
                "device", "session", null, Captured, null, HierarchyCapability.CheckedTriState,
                CoverageCompleteness.CompleteWithinDeclaredSurface, null, 0, null, field)));

    private sealed class SwitchOccurrence(string presentation) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[] { new ProposedOccurrence(null, "switch", "Wi-Fi", presentation,
                Native: new NativeLocator("android.resource-id", "switch-wifi")) };
    }

    private sealed class NeverDeliver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("inspection must not dispatch");
    }

    private sealed class AlwaysFresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "test");
    }
}
