using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.Fusion;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

public sealed class SlowContractsTests
{
    [Fact]
    public void RequiredClaim_IsOnlySubjectAndField()
    {
        var claim = new RequiredClaim("ui.dialog", "visible");
        Assert.True(claim.IsValid);
        Assert.Equal("ui.dialog", claim.Subject);
        Assert.Equal("visible", claim.Field);
    }

    [Fact]
    public void AttemptLedger_RejectsDuplicateAndTerminalRetry()
    {
        var key = Key("cycle-1");
        var ledger = new EphemeralAttemptLedger();
        Assert.True(ledger.TryStart(key));
        Assert.False(ledger.TryStart(key));
        Assert.True(ledger.MarkTerminal(key));
        Assert.True(ledger.IsTerminal(key));
        Assert.False(ledger.CanSemanticRetry(key));
    }

    [Fact]
    public void NewCycle_ProducesDifferentAttemptKey()
    {
        var claim = new RequiredClaim("ui.dialog", "visible");
        Assert.NotEqual(
            new SlowAttemptKey("dialog", "cycle-1", claim, LogicalProfileId.Text),
            new SlowAttemptKey("dialog", "cycle-2", claim, LogicalProfileId.Text));
    }

    [Fact]
    public void ContextBuilder_PreservesConflictAtomically()
    {
        var claim = new RequiredClaim("ui.dialog", "visible");
        var basis = new ConflictBasisBundle(new[] { "e-established", "e-challenging" }, "two values");
        var insufficient = EvidenceContextBuilder.Build(claim, conflict: basis, budget: 1);
        Assert.True(insufficient.IsContextInsufficient);
        Assert.Empty(insufficient.EvidenceIds);

        var context = EvidenceContextBuilder.Build(claim, conflict: basis, budget: 2);
        Assert.True(context.IsValid);
        Assert.Equal(2, context.EvidenceIds.Count);
    }

    [Fact]
    public void VisualRequest_RequiresRawArtifactReference()
    {
        var claim = new RequiredClaim("ui.dialog", "visible");
        var capture = new FusionCapture("cap-1", "run-1", "cycle-1", DateTimeOffset.UnixEpoch);
        var context = EvidenceContextBuilder.Build(claim);
        var request = new SlowPerceptionRequest("req-1",
            new SlowAttemptKey("dialog", "cycle-1", claim, LogicalProfileId.Visual), claim,
            LogicalProfileId.Visual, "control", "required claim", capture, context);
        Assert.False(request.IsValid);
    }

    private static SlowAttemptKey Key(string cycle) =>
        new("dialog", cycle, new RequiredClaim("ui.dialog", "visible"), LogicalProfileId.Text);
}
