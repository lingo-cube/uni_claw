using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Evidence;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

public sealed class SemanticPerceptionCapabilityTests
{
    [Fact]
    public async Task RegistryResolutionExecutesTheInjectedSemanticImplementation()
    {
        var request = Request();
        var calls = 0;
        var capability = new UniPerceptionCapability().WithSemanticExecutor((input, token) =>
        {
            calls++;
            return ValueTask.FromResult(SemanticPerceptionResult.Observed(input, "notifications", "App notifications"));
        });
        var registry = new CapabilityRegistry(TrustDomain.Product);
        registry.Register(capability, "semantic-test-composition");
        var resolved = Assert.IsAssignableFrom<ISemanticPerception>(registry.Resolve("uni.perception"));
        var result = await resolved.ObserveSemanticsAsync(request);
        Assert.Equal(1, calls);
        Assert.Equal(CapabilityStatus.Completed, result.Status);
        Assert.Equal("notifications", result.Observation!.PageKey);
        Assert.Equal(request.Current.CaptureId, result.CaptureId);
        Assert.Equal(SemanticExpectedMatch.Unknown, result.ExpectedMatch);
    }

    [Fact]
    public async Task ObservationProjectionKeepsSourceLineageAndDoesNotProjectIntent()
    {
        var ledger = new EvidenceLedger();
        var request = Request();
        var source = ledger.Admit(new ObservationProposal(new("fast.layout", "actual captured notification layout"),
            IngressKind.Observation, ObservationContext.External,
            new("perception.live.vision", request.Current.CaptureTime, "capture-a", new[] { "raw-layout" }))).Record!;
        var current = request.Current;
        request = request with
        {
            Current = new(current.CaptureId, current.ObservationCycleId, current.SessionCorrelation,
                current.CaptureTime, current.Space, current.Items, new[] { source.EvidenceId }),
            Intent = new("unused", "this is a caller expectation, not observed content"),
        };
        var cap = new UniPerceptionCapability().WithSemanticExecutor((input, _) =>
            ValueTask.FromResult(SemanticPerceptionResult.Observed(input, "notifications", "App notifications")));
        var result = await cap.ObserveSemanticsAsync(request);
        var proposal = Assert.Single(SemanticObservationProjector.CreateProposals(request, result));
        Assert.Equal("ui.semantic.page", proposal.Claim.Subject);
        Assert.DoesNotContain("caller expectation", proposal.Claim.Value);
        Assert.DoesNotContain("unused", proposal.Claim.Value);
        Assert.Equal(new[] { source.EvidenceId }, proposal.DerivedFromEvidenceIds);
        var admitted = ledger.Admit(proposal);
        Assert.Equal(AdmissionDecision.Accepted, admitted.Admission.Decision);
        Assert.Contains(source.EvidenceId, admitted.Record!.TransitiveEvidenceBasis);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("correlation")]
    [InlineData("space")]
    [InlineData("current-page-hint")]
    [InlineData("oversized-text")]
    public async Task InvalidFastBasisDoesNotInvokeSupplier(string variant)
    {
        var request = Request();
        request = variant switch
        {
            "stale" => request with { Current = request.Current with { IsFresh = false } },
            "correlation" => request with { Correlation = new("other-run", captureId: "capture-a", observationCycleId: "cycle-a") },
            "current-page-hint" => request with { Current = request.Current with { PageName = "Unused apps", PagePurpose = "预期伪装成观察", PageKey = "unused" } },
            "oversized-text" => request with { Intent = new("notifications", new string('a', 140000)) },
            _ => request with { Current = request.Current with { Space = CoordinateSpace.DeviceViewport(1080, 1920, captureId: "other-capture") } },
        };
        var calls = 0;
        var capability = new UniPerceptionCapability().WithSemanticExecutor((input, token) =>
        { calls++; return ValueTask.FromResult(SemanticPerceptionResult.Observed(input, "notifications")); });
        var result = await capability.ObserveSemanticsAsync(request);
        Assert.Equal(0, calls);
        Assert.Null(result.Observation);
        Assert.Equal(PerceptionDisposition.Unaligned, result.Assessment.Disposition);
    }

    [Fact]
    public async Task BoundedWaitTerminatesUncooperativeSupplierAndAttemptCannotBeRetried()
    {
        var calls = 0;
        var never = new TaskCompletionSource<SemanticPerceptionResult>();
        var cap = new UniPerceptionCapability().WithSemanticExecutor((input, token) =>
        { calls++; return new ValueTask<SemanticPerceptionResult>(never.Task); });
        var request = Request() with { Budget = TimeSpan.FromMilliseconds(20) };
        var first = await cap.ObserveSemanticsAsync(request);
        var second = await cap.ObserveSemanticsAsync(request with { RequestId = "different-id-same-attempt" });
        Assert.Equal(CapabilityStatus.TimedOut, first.Status);
        Assert.Equal(CapabilityStatus.Duplicate, second.Status);
        Assert.Null(first.Observation);
        Assert.Equal(1, calls);
        never.SetResult(SemanticPerceptionResult.Observed(request, "notifications"));
    }

    [Fact]
    public async Task ContradictoryIntentAssessmentDoesNotEraseSupportedPageObservation()
    {
        var request = Request() with { Intent = new("unused", "查看闲置应用页") };
        var cap = new UniPerceptionCapability().WithSemanticExecutor((input, token) => ValueTask.FromResult(
            SemanticPerceptionResult.Observed(input, "notifications", "App notifications", expectedMatch: SemanticExpectedMatch.Match)));
        var result = await cap.ObserveSemanticsAsync(request);
        Assert.Equal("notifications", result.Observation!.PageKey);
        Assert.Equal(SemanticExpectedMatch.Conflicted, result.ExpectedMatch);
        Assert.Equal(PerceptionDisposition.Partial, result.Assessment.Disposition);
    }

    [Fact]
    public async Task ConflictCannotBypassTheMissingPreviousObservationRule()
    {
        var request = Request() with { Intent = new("unused", "查看闲置应用列表") };
        var cap = new UniPerceptionCapability().WithSemanticExecutor((input, _) => ValueTask.FromResult(
            SemanticPerceptionResult.Observed(input, "notifications", relation: SemanticPageRelation.SamePage,
                expectedMatch: SemanticExpectedMatch.Match)));
        var result = await cap.ObserveSemanticsAsync(request);
        Assert.Equal(SemanticExpectedMatch.Conflicted, result.ExpectedMatch);
        Assert.Equal(SemanticPageRelation.Unknown, result.Relation);
    }

    [Fact]
    public async Task CancelledAndUnsupportedCallsProduceNoObservation()
    {
        var request = Request();
        ISemanticPerception marker = new MarkerOnly();
        var unsupported = await marker.ObserveSemanticsAsync(request);
        Assert.Equal(PerceptionDisposition.Unsupported, unsupported.Assessment.Disposition);
        Assert.Null(unsupported.Observation);
        var calls = 0;
        var cap = new UniPerceptionCapability().WithSemanticExecutor((input, _) =>
        { calls++; return ValueTask.FromResult(SemanticPerceptionResult.Observed(input, "notifications")); });
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Equal(CapabilityStatus.Cancelled, (await cap.ObserveSemanticsAsync(request, cancelled.Token)).Status);
        Assert.Equal(0, calls);
        Assert.NotNull((await cap.ObserveSemanticsAsync(request)).Observation);
    }

    [Fact]
    public async Task ObservationCannotHideConsultedPreviousEvidenceOrInventLeafLineage()
    {
        var request = Request() with { Previous = Request("previous", "previous-cycle").Current };
        // Distinct immediate parents, with an already-derived current source.
        var c = request.Current;
        var p = request.Previous!;
        request = request with
        {
            Current = new(c.CaptureId, c.ObservationCycleId, c.SessionCorrelation, c.CaptureTime, c.Space,
                c.Items, ["derived-current"], SourceLeafEvidenceIds: new HashSet<string> { "current-leaf" }),
            Previous = new(p.CaptureId, p.ObservationCycleId, p.SessionCorrelation, p.CaptureTime, p.Space,
                p.Items, ["previous-leaf"]),
        };
        var cap = new UniPerceptionCapability().WithSemanticExecutor((input, _) =>
        {
            var full = SemanticPerceptionResult.Observed(input, "notifications");
            return ValueTask.FromResult(full with { Observation = new(full.Observation!.PageKey,
                full.Observation.PageName, full.Observation.PagePurpose, null, ["derived-current"], full.Assessment) });
        });
        Assert.Null((await cap.ObserveSemanticsAsync(request)).Observation);
        var valid = SemanticPerceptionResult.Observed(request, "notifications");
        var proposal = Assert.Single(SemanticObservationProjector.CreateProposals(request, valid));
        Assert.True(proposal.TransitiveEvidenceBasis.SetEquals(new[] { "current-leaf", "previous-leaf" }));
        Assert.DoesNotContain("derived-current", proposal.TransitiveEvidenceBasis);
    }

    private sealed class MarkerOnly : ISemanticPerception
    {
        public CapabilityDescription Description => UniPerceptionCapability.CanonicalDescription;
    }

    [Fact]
    public async Task InventedTitleAndWrongResultCorrelationCannotBecomeObservations()
    {
        foreach (var variant in new[] { "title", "correlation" })
        {
            var cap = new UniPerceptionCapability().WithSemanticExecutor((input, token) =>
            {
                var result = SemanticPerceptionResult.Observed(input, "notifications",
                    variant == "title" ? "invented title" : "App notifications");
                return ValueTask.FromResult(variant == "correlation" ? result with { CaptureId = "another-capture" } : result);
            });
            var result = await cap.ObserveSemanticsAsync(Request());
            Assert.Null(result.Observation);
            Assert.Empty(SemanticObservationProjector.CreateProposals(Request(), result));
        }
    }

    internal static SemanticPerceptionRequest Request(string capture = "capture-a", string cycle = "cycle-a") =>
        new("request-" + capture, new CapabilityCorrelation("run-a", captureId: capture, observationCycleId: cycle),
            new SemanticObservationContext(capture, cycle, "run-a", DateTimeOffset.Parse("2026-10-21T00:00:00Z"),
                CoordinateSpace.DeviceViewport(1080, 1920, captureId: capture),
                new[] { new SemanticLayoutItem("title", "text", "App notifications", new UiBounds(60, 418, 776, 519)) },
                new[] { "ev-source" }),
            new[] { new SemanticPageCandidate("notifications", "App notifications", "按应用查看通知列表"),
                    new SemanticPageCandidate("unused", "Unused apps", "未使用应用列表") });
}
