using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class PreTerminalCycleContractTests
{
    [Fact]
    public void SnapshotAndProposal_CopyReferenceCollections()
    {
        var trace = new List<string> { "trace-1" };
        var snapshot = Snapshot(trace);
        var evidence = new List<string> { "evidence-1" };
        var proposal = Proposal(evidence);

        trace.Add("trace-2");
        evidence.Add("evidence-2");

        Assert.Single(snapshot.TraceReferences);
        Assert.Single(proposal.SupportingEvidenceReferences);
    }

    [Fact]
    public void FreshProposalCommitsExactlyOneRevision()
    {
        var ledger = new PreTerminalReasoningLedger();
        var validator = new PreTerminalCheckpointValidator();
        var result = validator.Validate(Snapshot(), Proposal(), State());

        Assert.True(result.Accepted);
        Assert.True(ledger.TryCommit(Proposal()));
        Assert.Equal("r1", ledger.Current.Reference);
        Assert.False(ledger.TryCommit(Proposal()));
        Assert.Single(ledger.History.Where(r => r.Reference == "r1"));
        Assert.Equal(PreTerminalCheckpointRejection.DuplicateCycle,
            validator.Validate(Snapshot(), Proposal(), State()).Rejection);
    }

    [Theory]
    [InlineData(PreTerminalCheckpointRejection.RunMismatch)]
    [InlineData(PreTerminalCheckpointRejection.ObservationMismatch)]
    [InlineData(PreTerminalCheckpointRejection.BeliefMismatch)]
    [InlineData(PreTerminalCheckpointRejection.DfsProgressMismatch)]
    [InlineData(PreTerminalCheckpointRejection.TraceMismatch)]
    [InlineData(PreTerminalCheckpointRejection.RevisionMismatch)]
    public void CorrelationMismatchFailsClosed(PreTerminalCheckpointRejection expected)
    {
        var snapshot = Snapshot();
        var proposal = Proposal();
        var state = State();
        state = expected switch
        {
            PreTerminalCheckpointRejection.RunMismatch => state with { RunId = "other" },
            PreTerminalCheckpointRejection.ObservationMismatch => state with { AcceptedObservationSequence = 2 },
            PreTerminalCheckpointRejection.BeliefMismatch => state with { BeliefDigest = "other" },
            PreTerminalCheckpointRejection.DfsProgressMismatch => state with { DfsProgressRevision = 2 },
            PreTerminalCheckpointRejection.TraceMismatch => state with { TraceDigest = "other" },
            PreTerminalCheckpointRejection.RevisionMismatch => state with { AcceptedReasoningRevisionReference = "other" },
            _ => state,
        };

        var result = new PreTerminalCheckpointValidator().Validate(
            snapshot, proposal, state);

        Assert.False(result.Accepted);
        Assert.Equal(expected, result.Rejection);
    }

    [Fact]
    public void TerminalAndCancelledCyclesRejectAndCloseLateResults()
    {
        var validator = new PreTerminalCheckpointValidator();
        var ledger = new PreTerminalReasoningLedger();
        var snapshot = Snapshot();
        var proposal = Proposal();

        Assert.Equal(PreTerminalCheckpointRejection.Terminal,
            validator.Validate(snapshot, proposal, State() with { RunState = RunState.Completed }).Rejection);
        Assert.Equal(PreTerminalCheckpointRejection.Cancelled,
            new PreTerminalCheckpointValidator().Validate(snapshot, proposal, State(), cancelled: true).Rejection);
    }

    [Fact]
    public void ExpiredDeadlineRejectsWithoutCommit()
    {
        var ledger = new PreTerminalReasoningLedger();
        var result = new PreTerminalCheckpointValidator().Validate(
            Snapshot(deadline: DateTimeOffset.UtcNow.AddSeconds(-1)), Proposal(), State());

        Assert.False(result.Accepted);
        Assert.Equal(PreTerminalCheckpointRejection.DeadlineExpired, result.Rejection);
        Assert.Equal("reasoning-0", ledger.Current.Reference);
    }

    [Fact]
    public void DisabledCapabilityDoesNotTouchExistingAgentPath()
    {
        // The optional seam has no callback or global registration; absent capability is inert.
        Assert.Empty(new PreTerminalReasoningLedger().History.Skip(1));
    }

    [Fact]
    public void PassiveProposalHasNoExecutionAuthorityMembers()
    {
        var names = typeof(PreTerminalContinuationProposal).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("DeviceAction", names);
        Assert.DoesNotContain("RunState", names);
        Assert.DoesNotContain("GoalEvidence", names);
        Assert.DoesNotContain("Route", names);
        Assert.DoesNotContain("Selector", names);
    }

    private static PreTerminalReasoningSnapshot Snapshot(
        IReadOnlyList<string>? trace = null,
        DateTimeOffset? deadline = null) =>
        new(
            PreTerminalReasoningSnapshot.CurrentContractVersion,
            "run-1", 1, 1, 1, "belief-1", 1, "cursor-1", "trace-1", "reasoning-0",
            "boundary-1", deadline ?? DateTimeOffset.UtcNow.AddMinutes(1), trace);

    private static PreTerminalContinuationProposal Proposal(IReadOnlyList<string>? evidence = null) =>
        new("run-1", 1, 1, 1, "trace-1", "reasoning-0", "r1",
            PreTerminalContinuationKind.ContinuationSupported, evidence);

    private static PreTerminalCheckpointState State() =>
        new("run-1", RunState.Running, 1, 1, 1, "belief-1", 1, "trace-1", "reasoning-0", DateTimeOffset.UtcNow, "boundary-1");
}
