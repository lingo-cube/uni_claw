using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Simulation.Tests.ComponentReplay;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>The SUT is the real L2 capability. Two L3 supply realizations enter
/// its public executor seam; this does not claim two arbitrary L2s are equivalent.</summary>
public sealed class ComponentSemanticReplayTests
{
    public static IEnumerable<object[]> RecordedCases() => ComponentReplayCases.Rows("semanticSuppliers");

    private sealed class Supply(RecordedPagePair input, string kind)
    {
        internal int Calls { get; private set; }
        internal Func<SemanticPerceptionResult, SemanticPerceptionResult>? Alter { get; init; }
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Hold { get; init; }

        internal async ValueTask<SemanticPerceptionResult> Execute(SemanticPerceptionRequest request, CancellationToken token)
        {
            Calls++;
            var response = kind switch
            {
                "scripted" => SemanticPerceptionResult.Observed(request, input.Page.Key, input.Page.Name,
                    SemanticPageRelation.DifferentPage, SemanticExpectedMatch.Match, source: "controlled-semantic-supply"),
                "frozen-response" => JsonSerializer.Deserialize<SemanticPerceptionResult>(File.ReadAllText(
                    ComponentReplayCases.Fixture("responses/" + input.Id + ".json")), RecordedComponentInputs.Json)!,
                _ => throw new ArgumentException("unknown semantic supply", nameof(kind)),
            };
            Started.TrySetResult();
            // Deliberately uncooperative supplier for the cancellation test:
            // the real L2 must bound waiting and ignore a subsequently released reply.
            if (Hold) await Release.Task.ConfigureAwait(false);
            var result = Alter is null ? response : Alter(response);
            Finished.TrySetResult();
            return result;
        }
    }

    private static ISemanticPerception Compose(Supply supply)
    {
        var product = new UniPerceptionCapability().WithSemanticExecutor(supply.Execute);
        var registry = new CapabilityRegistry(TrustDomain.Product);
        registry.Register(product, "sim014-component-composition");
        var consumer = Assert.IsAssignableFrom<ISemanticPerception>(registry.Resolve("uni.perception"));
        Assert.Same(product, consumer);
        Assert.Contains(consumer.Description.Protocols, p => p.Name == PerceptionProtocol.Semantic && p.Version == "1.0");
        return consumer;
    }

    [Theory, MemberData(nameof(RecordedCases)), Trait("Scenario", "SCN-REPLAY-008")]
    public async Task SharedCaptureFaultIsRejectedBeforeSupply_ThenSamePublicContractRecovers(string id, string supplier)
    {
        var guard = ComponentReplayCases.Guard();
        var setup = guard.Create(ComponentReplayCases.Root, () =>
        {
            var input = RecordedComponentInputs.Load(id, "frozen-capture");
            var ledger = new EvidenceLedger();
            var request = RecordedComponentInputs.SemanticRequest(input, ledger);
            var supply = new Supply(input, supplier);
            return (Input: input, Ledger: ledger, Request: request, Supply: supply, Consumer: Compose(supply));
        });
        var oracle = ComponentReplayCases.Oracle(); var expected = oracle.Cases.Single(c => c.Id == id);
        var frozenInput = JsonSerializer.Serialize(setup.Request, RecordedComponentInputs.Json);
        var fault = new CaptureMismatchFault();
        var wrong = setup.Request with { Correlation = new(setup.Request.Correlation.RunId,
            captureId: fault.Deliver(setup.Request.Current.CaptureId, "semantic-request.correlation.captureId"),
            observationCycleId: setup.Request.Current.ObservationCycleId) };
        var rejected = await setup.Consumer.ObserveSemanticsAsync(wrong);
        Assert.Equal(oracle.Semantic.RejectionStatus, rejected.Status.ToString());
        Assert.Equal(oracle.Semantic.RejectionDisposition, rejected.Assessment.Disposition.ToString());
        Assert.Null(rejected.Observation); Assert.Empty(SemanticObservationProjector.CreateProposals(wrong, rejected));
        Assert.Equal(0, setup.Supply.Calls);

        var corrected = setup.Request with { Correlation = new(setup.Request.Correlation.RunId,
            captureId: fault.Deliver(setup.Request.Current.CaptureId, "semantic-request.correlation.captureId"),
            observationCycleId: setup.Request.Current.ObservationCycleId) };
        var actual = await setup.Consumer.ObserveSemanticsAsync(corrected);
        Assert.True(oracle.Semantic.SuccessStatus == actual.Status.ToString(),
            "semantic replay product result: " + JsonSerializer.Serialize(actual, RecordedComponentInputs.Json));
        Assert.True(oracle.Semantic.SuccessDisposition == actual.Assessment.Disposition.ToString(),
            "semantic replay product result: " + JsonSerializer.Serialize(actual, RecordedComponentInputs.Json));
        Assert.Equal(oracle.Semantic.SuccessRelation, actual.Relation.ToString());
        Assert.Equal(oracle.Semantic.SuccessExpectedMatch, actual.ExpectedMatch.ToString());
        Assert.Equal(expected.PageKey, actual.Observation!.PageKey); Assert.Equal(expected.Title, actual.Observation.ObservedTitle);
        Assert.All(actual.Observation.BasisEvidenceIds, basis => Assert.Contains(basis, setup.Ledger.CanonicalRecords.Keys));
        Assert.Equal(1, setup.Supply.Calls);
        var projected = Assert.Single(SemanticObservationProjector.CreateProposals(corrected, actual));
        Assert.DoesNotContain("expected", projected.Claim.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Controlled caller intent", projected.Claim.Value, StringComparison.Ordinal);
        Assert.Equal(AdmissionDecision.Accepted, setup.Ledger.Admit(projected).Admission.Decision);

        var duplicate = await setup.Consumer.ObserveSemanticsAsync(corrected);
        Assert.Equal(oracle.Semantic.DuplicateStatus, duplicate.Status.ToString());
        Assert.Null(duplicate.Observation); Assert.Equal(1, setup.Supply.Calls);
        var receipt = Assert.Single(fault.Receipts);
        Assert.Equal(setup.Request.Current.CaptureId, receipt.Original); Assert.NotEqual(receipt.Original, receipt.Delivered);
        Assert.Equal(frozenInput, JsonSerializer.Serialize(setup.Request, RecordedComponentInputs.Json));
        ComponentReplayCases.Export("component-semantic-" + id + "-" + supplier, guard,
            new { supplier, setup.Input.SourceHashes, rejected, recovered = actual, duplicate, fault = fault.Receipts, supplierCalls = setup.Supply.Calls });
    }

    [Theory, Trait("Scenario", "SCN-REPLAY-008")]
    [InlineData("scripted", "unavailable"), InlineData("frozen-response", "unavailable")]
    [InlineData("scripted", "conflicted-intent"), InlineData("frozen-response", "conflicted-intent")]
    [InlineData("scripted", "misbound-response"), InlineData("frozen-response", "misbound-response")]
    public async Task SameConsumerClosurePreservesFailureAndObservationDimensions(string supplier, string variant)
    {
        var guard = ComponentReplayCases.Guard();
        var setup = guard.Create(ComponentReplayCases.Root, () =>
        {
            var input = RecordedComponentInputs.Load("sims", "frozen-capture");
            var ledger = new EvidenceLedger(); var request = RecordedComponentInputs.SemanticRequest(input, ledger);
            var supply = new Supply(input, supplier)
            {
                Alter = result => variant switch
                {
                    "unavailable" => SemanticPerceptionResult.Empty(request, CapabilityStatus.Unavailable,
                        PerceptionDisposition.Unavailable, "controlled supplier unavailable"),
                    "misbound-response" => result with { CaptureId = "foreign-response" },
                    _ => result,
                },
            };
            if (variant == "conflicted-intent") request = request with { Intent = new(input.PreviousPage.Key, "Caller intended the previous page") };
            return (Request: request, Supply: supply, Consumer: Compose(supply));
        });
        var oracle = ComponentReplayCases.Oracle();
        var actual = await setup.Consumer.ObserveSemanticsAsync(setup.Request);
        if (variant == "conflicted-intent")
        {
            Assert.True(oracle.Semantic.ConflictedStatus == actual.Status.ToString(),
                "conflicted-intent product result: " + JsonSerializer.Serialize(actual, RecordedComponentInputs.Json));
            Assert.True(oracle.Semantic.ConflictedDisposition == actual.Assessment.Disposition.ToString(),
                "conflicted-intent product result: " + JsonSerializer.Serialize(actual, RecordedComponentInputs.Json));
            Assert.Equal(oracle.Semantic.ConflictedMatch, actual.ExpectedMatch.ToString());
            Assert.Equal("sims", actual.Observation!.PageKey);
            var claim = Assert.Single(SemanticObservationProjector.CreateProposals(setup.Request, actual));
            Assert.Contains("sims", claim.Claim.Value); Assert.DoesNotContain("Caller intended", claim.Claim.Value);
        }
        else
        {
            Assert.Equal(variant == "unavailable" ? oracle.Semantic.UnavailableStatus : oracle.Semantic.MisboundStatus, actual.Status.ToString());
            Assert.Equal(variant == "unavailable" ? oracle.Semantic.UnavailableDisposition : oracle.Semantic.MisboundDisposition,
                actual.Assessment.Disposition.ToString());
            Assert.Null(actual.Observation); Assert.Empty(SemanticObservationProjector.CreateProposals(setup.Request, actual));
        }
        Assert.Equal(1, setup.Supply.Calls);
        var duplicate = await setup.Consumer.ObserveSemanticsAsync(setup.Request);
        Assert.Equal(oracle.Semantic.DuplicateStatus, duplicate.Status.ToString()); Assert.Equal(1, setup.Supply.Calls);
        ComponentReplayCases.Export("component-semantic-" + supplier + "-" + variant, guard, new { actual, duplicate });
    }

    [Theory, InlineData("scripted"), InlineData("frozen-response"), Trait("Scenario", "SCN-REPLAY-008")]
    public async Task CancellationTerminatesAttempt_LateSupplierReplyCannotReplaceTheReturnedResult(string supplier)
    {
        var guard = ComponentReplayCases.Guard();
        var setup = guard.Create(ComponentReplayCases.Root, () =>
        {
            var input = RecordedComponentInputs.Load("sims", "frozen-capture");
            var request = RecordedComponentInputs.SemanticRequest(input, new EvidenceLedger());
            var supply = new Supply(input, supplier) { Hold = true };
            return (Request: request, Supply: supply, Consumer: Compose(supply));
        });
        var oracle = ComponentReplayCases.Oracle(); using var cancellation = new CancellationTokenSource();
        var pending = setup.Consumer.ObserveSemanticsAsync(setup.Request, cancellation.Token).AsTask();
        await setup.Supply.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancellation.Cancel();
        var actual = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(oracle.Semantic.CancelStatus, actual.Status.ToString()); Assert.Null(actual.Observation);
        var frozen = JsonSerializer.Serialize(actual, RecordedComponentInputs.Json);
        setup.Supply.Release.TrySetResult();
        await setup.Supply.Finished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var duplicate = await setup.Consumer.ObserveSemanticsAsync(setup.Request);
        Assert.Equal(oracle.Semantic.DuplicateStatus, duplicate.Status.ToString()); Assert.Equal(1, setup.Supply.Calls);
        Assert.Equal(frozen, JsonSerializer.Serialize(actual, RecordedComponentInputs.Json));
        Assert.Empty(SemanticObservationProjector.CreateProposals(setup.Request, actual));
        ComponentReplayCases.Export("component-semantic-cancel-" + supplier, guard, new { actual, duplicate, lateSupplierCompleted = setup.Supply.Finished.Task.IsCompletedSuccessfully, supplierCalls = setup.Supply.Calls });
    }
}
