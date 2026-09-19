using Xunit;
using UniClaw.Core;

namespace UniClaw.Core.Tests;

public sealed class PhoneScrollTracerTests
{
    [Fact]
    public void Scroll_creates_overlapping_slices_without_rewriting_history_and_binding_stays_fixed()
    {
        var page = new CoreId("page:1");
        var elementA = new CoreId("element:a");
        var elementB = new CoreId("element:b");
        var elementC = new CoreId("element:c");
        var firstObservedAt = DateTimeOffset.Parse("2026-09-19T10:00:00Z");
        var secondObservedAt = firstObservedAt.AddSeconds(2);

        var firstEvidence = new EvidenceRecord(
            new CoreId("evidence:scroll-1"), page, firstObservedAt, "phone-camera", "viewport-1", []);
        var secondEvidence = new EvidenceRecord(
            new CoreId("evidence:scroll-2"), page, secondObservedAt, "phone-camera", "viewport-2", []);
        var firstSlice = new Slice(
            new CoreId("slice:1"), page, [firstEvidence.Id], firstObservedAt, "viewport-1",
            [elementA, elementB], "partial");
        var secondSlice = new Slice(
            new CoreId("slice:2"), page, [secondEvidence.Id], secondObservedAt, "viewport-2",
            [elementB, elementC], "partial-overlap");

        var effect = new Effect(new CoreId("effect:click-b"), "click", elementB);
        var binding = new TargetBinding(
            new CoreId("binding:click-b"), page, firstSlice.Id,
            "phone-accessibility", "node:b@viewport-1", BindingDisposition.Canonical);
        var attempt = new Attempt(
            new CoreId("attempt:click-b"), effect.Id, binding.Id,
            secondObservedAt.AddSeconds(1), DeliveryOutcome.Pending, null);

        Assert.Equal([elementA, elementB], firstSlice.ObservedRecordIds);
        Assert.Equal([elementB, elementC], secondSlice.ObservedRecordIds);
        Assert.Equal(firstSlice.Id, binding.BasisSliceId);
        Assert.True(CoreInvariants.IsHistoricalBasisStable(binding, firstSlice.Id));
        Assert.False(CoreInvariants.IsHistoricalBasisStable(binding, secondSlice.Id));
        Assert.True(attempt.StartedAt > secondSlice.ObservedAt);
    }

    [Fact]
    public void Unknown_delivery_does_not_become_world_result_or_dispatch_permission()
    {
        var attempt = new Attempt(
            new CoreId("attempt:unknown"), new CoreId("effect:1"), new CoreId("binding:unknown"),
            DateTimeOffset.Parse("2026-09-19T10:00:00Z"), DeliveryOutcome.Unknown, null);
        var binding = new TargetBinding(
            new CoreId("binding:unknown"), new CoreId("page:1"), new CoreId("slice:1"),
            "phone-accessibility", "node:x", BindingDisposition.Unverified);

        Assert.False(CoreInvariants.CanDispatch(binding));
        Assert.False(CoreInvariants.IsTerminalDelivery(attempt));
        Assert.Equal(DeliveryOutcome.Unknown, attempt.Delivery);
    }

    [Fact]
    public void Binding_dispositions_remain_distinct_and_fail_closed()
    {
        var dispositions = Enum.GetValues<BindingDisposition>();

        Assert.Equal(6, dispositions.Length);
        Assert.Contains(BindingDisposition.Stale, dispositions);
        Assert.Contains(BindingDisposition.Ambiguous, dispositions);
        Assert.Contains(BindingDisposition.Unauthorized, dispositions);
        Assert.Contains(BindingDisposition.Unverified, dispositions);
        Assert.DoesNotContain(BindingDisposition.Stale, new[] { BindingDisposition.Canonical });

        foreach (var disposition in dispositions.Where(d => d != BindingDisposition.Canonical))
        {
            var binding = new TargetBinding(
                new CoreId($"binding:{disposition}"),
                new CoreId("page:1"), new CoreId("slice:1"),
                "domain-locator", "target-1", disposition);

            Assert.False(CoreInvariants.CanDispatch(binding));
        }
    }

    [Fact]
    public void Permission_is_explicit_clause_and_is_not_derived_from_observation()
    {
        var observed = new EvidenceRecord(
            new CoreId("evidence:button"),
            new CoreId("page:1"),
            DateTimeOffset.Parse("2026-09-19T10:00:00Z"),
            "phone-camera",
            "viewport-1",
            []);
        var claim = new Claim(
            new CoreId("claim:button-visible"),
            observed.SubjectId,
            "visible",
            "button:confirm",
            ClaimDisposition.Accepted,
            [observed.Id]);

        var permission = new Clause(
            new CoreId("clause:tap-confirm"),
            ClauseKind.Permission,
            "tap button:confirm",
            observed.ObservedAt);

        Assert.Equal(ClaimDisposition.Accepted, claim.Disposition);
        Assert.Contains(observed.Id, claim.EvidenceBasis);
        Assert.Equal(ClauseKind.Permission, permission.Kind);
        Assert.NotEqual(claim.Id, permission.Id);
    }

    [Fact]
    public void Event_semantics_can_be_composed_from_evidence_and_claim_without_new_authority()
    {
        var occurrenceEvidence = new EvidenceRecord(
            new CoreId("evidence:tap-1"),
            new CoreId("button:confirm"),
            DateTimeOffset.Parse("2026-09-19T10:01:00Z"),
            "phone-effect-receipt",
            "run:1",
            ["dispatch", "receipt"]);
        var occurred = new Claim(
            new CoreId("claim:tap-1-occurred"),
            occurrenceEvidence.SubjectId,
            "occurred",
            "tap",
            ClaimDisposition.Accepted,
            [occurrenceEvidence.Id]);
        var caused = new Claim(
            new CoreId("claim:tap-1-caused"),
            occurrenceEvidence.SubjectId,
            "caused",
            "screen:detail",
            ClaimDisposition.Candidate,
            [occurrenceEvidence.Id]);

        Assert.Equal("occurred", occurred.Predicate);
        Assert.Equal("caused", caused.Predicate);
        Assert.Equal(DateTimeOffset.Parse("2026-09-19T10:01:00Z"), occurrenceEvidence.ObservedAt);
        Assert.Equal([occurrenceEvidence.Id], occurred.EvidenceBasis);
        Assert.Equal(ClaimDisposition.Candidate, caused.Disposition);
    }

    [Fact]
    public void Binding_basis_slice_is_optional_when_a_fixed_non_slice_basis_is_used()
    {
        var basis = new BasisReference(new CoreId("resource:account"), "resource-version", "snapshot:rev-7");
        var binding = new TargetBinding(
            new CoreId("binding:fixed-snapshot"),
            new CoreId("segment:account"),
            BasisSliceId: null,
            "snapshot",
            "snapshot:rev-7",
            BindingDisposition.Canonical,
            [basis]);

        Assert.Null(binding.BasisSliceId);
        Assert.True(CoreInvariants.HasFixedBasis(binding));
        Assert.True(CoreInvariants.IsHistoricalBasisStable(binding, basis));
        Assert.True(CoreInvariants.CanDispatch(binding));
    }

    [Fact]
    public void Attempt_keeps_request_executor_authorization_and_three_execution_dimensions_separate()
    {
        var state = new AttemptExecutionState(
            DispatchProgress.Accepted,
            ExternalExecutionStatus.Unknown,
            CoordinationStatus.Open);
        var attempt = new Attempt(
            new CoreId("attempt:late"),
            new CoreId("effect:tap"),
            new CoreId("binding:tap"),
            DateTimeOffset.Parse("2026-09-19T10:02:00Z"),
            DeliveryOutcome.Unknown,
            DeliveryEvidenceId: null,
            RequestSnapshotId: new CoreId("request:rev-4"),
            ExecutorId: new CoreId("executor:phone-driver"),
            AuthorizationBasis: [new CoreId("clause:tap-confirm")],
            ExecutionState: state);

        Assert.Equal(new CoreId("request:rev-4"), attempt.RequestSnapshotId);
        Assert.Equal(new CoreId("executor:phone-driver"), attempt.ExecutorId);
        Assert.Equal([new CoreId("clause:tap-confirm")], attempt.AuthorizationBasis);
        Assert.Equal(DispatchProgress.Accepted, attempt.ExecutionState!.Dispatch);
        Assert.Equal(ExternalExecutionStatus.Unknown, attempt.ExecutionState.ExternalExecution);
        Assert.Equal(CoordinationStatus.Open, attempt.ExecutionState.Coordination);
        Assert.False(CoreInvariants.IsTerminalDelivery(attempt));
    }

    [Fact]
    public void Canonical_binding_without_fixed_basis_fails_closed()
    {
        var binding = new TargetBinding(
            new CoreId("binding:unfixed"),
            new CoreId("segment:account"),
            BasisSliceId: null,
            "snapshot",
            "snapshot:unfixed",
            BindingDisposition.Canonical);

        Assert.False(CoreInvariants.HasFixedBasis(binding));
        Assert.False(CoreInvariants.CanDispatch(binding));
    }
}
