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

namespace UniClaw.Kernel.Tests.Perception.UiHierarchy;

/// <summary>Reviewed synthetic claims enter the public Kernel ingestion and
/// verification seams. No internal resolver calls or expected-value adapter.</summary>
public sealed class CheckedGuardPublicTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("missing", false)]
    [InlineData("malformed", false)]
    [InlineData("other-node", false)]
    [InlineData("old-capture", false)]
    [InlineData("mismatched-metadata", false)]
    public void Verification_RequiresTargetNodeCheckableObservation(string guard, bool expected)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.node.*" },
            new ProductAssociationStrategy(), new ReviewedOccurrence());
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(new AgentPlanPolicy()),
            new RuntimeAssurance(new ProductFreshnessEvaluator(() => T0.AddSeconds(3), TimeSpan.FromMinutes(1))),
            new EffectBoundary(new Delivery()));

        if (guard == "old-capture")
        {
            kernel.Process(Proposal("old", 0, "resource_id", "fixture:target", T0.AddSeconds(-1)));
            kernel.Process(Proposal("old", 0, "checked", "unchecked", T0.AddSeconds(-1)));
            kernel.Process(Proposal("old", 0, "checkable", "true", T0.AddSeconds(-1)));
        }
        kernel.Process(Proposal("post", 0, "resource_id", "fixture:target", T0.AddSeconds(2)));
        kernel.Process(Proposal("post", 0, "checked", "unchecked", T0.AddSeconds(2)));
        if (guard is "true" or "false" or "malformed")
            kernel.Process(Proposal("post", 0, "checkable", guard == "malformed" ? "not-a-boolean" : guard, T0.AddSeconds(2)));
        if (guard == "other-node")
            kernel.Process(Proposal("post", 1, "checkable", "true", T0.AddSeconds(2)));
        if (guard == "mismatched-metadata")
        {
            var proposal = Proposal("post", 0, "checkable", "true", T0.AddSeconds(2));
            kernel.Process(proposal with { Provenance = proposal.Provenance with
                { Hierarchy = proposal.Provenance.Hierarchy! with { NodeLocalIndex = 1 } } });
        }

        var verification = kernel.VerifyPostActionEffect(
            new TargetSpec("switch", "Reviewed target", "tap", CheckedState.Unchecked),
            Array.Empty<KernelResult>(), dispatchTime: T0.AddSeconds(1));
        Assert.All(ledger.AdmissionLog, a => Assert.Equal(AdmissionDecision.Accepted, a.Decision));
        Assert.Equal("unchecked", kernel.CurrentBelief!.WorldState["ui.node.post#0.checked"].Value);
        Assert.Contains(ledger.CanonicalRecords.Values, r => r.Claim.Subject == "ui.node.post#0.checked"
            && r.Claim.Value == "unchecked");
        Assert.Equal(expected, verification.IsVerified);
        if (!expected)
            Assert.Contains("checkable-not-observed-true", verification.RejectionReason ?? "");
    }

    private static ObservationProposal Proposal(string capture, int index, string field, string value, DateTimeOffset time) =>
        new(new($"ui.node.{capture}#{index}.{field}", value), IngressKind.Observation,
            ObservationContext.PostActionEffectFlow,
            new(TypedHierarchyProposalProjector.Producer, time, $"scope:{capture}:{index}:{field}",
                ["synthetic:checked-guard-001"], Hierarchy: new(
                    capture, 36, UiHierarchyAcquirerKind.AccessibilityNodeInfo, "reviewed-synthetic/1",
                    UiHierarchyFormat.AccessibilityTree, "synthetic-device", "checked-guard-001", null,
                    time, null, HierarchyCapability.CheckedTriState,
                    CoverageCompleteness.CompleteWithinDeclaredSurface, null, index, null, field)));

    private sealed class ReviewedOccurrence : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            [new(null, "switch", "Reviewed target", "off", Native: new("android.resource-id", "fixture:target"))];
    }

    private sealed class Delivery : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => new(DispatchOutcome.DeliveryCompleted, "unused", T0);
    }
}
