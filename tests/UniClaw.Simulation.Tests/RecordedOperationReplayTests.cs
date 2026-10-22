using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>Reviewed typed observations imported from historical XML enter the product chain.
/// No recorded setup/tap coordinates are executed or treated as grounding authority.</summary>
public sealed class RecordedOperationReplayTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly DateTimeOffset T0 = new(2026, 9, 12, 10, 12, 44, TimeSpan.Zero);
    private sealed record Asset(string Path, string Sha256, string PageKey);
    private sealed record ActionInput(string Role, string Descriptor, string EffectClass, string? DesiredState);
    private sealed record Expected(string Status, int Effects, int VerifiedEffects, int NavigationEdges, bool CheckState,
        string? BeforeState, string? AfterState, string? Phase = null, string? RejectionReasonContains = null);
    private sealed record ImportedCapture(UiHierarchyObservation Observation, ProposedOccurrence[] Candidates, string TriggerSubject);
    private sealed record ImportedAsset(string Path, string Sha256);
    private sealed record Imported(ImportedAsset Initial, ImportedAsset Post, ImportedAsset Unchanged);
    private sealed record ReplayCase(string Id, string SequencePath, string SequenceSha256, Asset Before, Asset After, ActionInput Action, Expected ExpectedBehavior, Expected DesiredOutcome, Imported Imported);
    private sealed record Corpus(string Source, int Width, int Height, ReplayCase[] Cases);
    private sealed record Ingress(string Path, string Sha256, string CaptureId, string Context);
    private sealed record Checkpoint(string Stage, string Status, string? Reason, int Effects, int Verifications, string? RevisionId);
    private sealed record Consultation(string Phase, string? FailureReason);
    private sealed record Result(string Status, string? Reason, string RunId, int Consultations,
        EffectReceipt[] Receipts, PostActionEffectVerification[] Verifications, AdmissionRecord[] Admissions,
        WorldGraphProjection Graph, Slice Before, Slice After, Ingress[] Ingress, Checkpoint[] Checkpoints, Consultation[] Decisions);
    private sealed record Divergence(string Path, string Expected, string Actual);

    [Fact, Trait("Scenario", "SCN-REPLAY-004")]
    public void RecordedNavigation_MissingPostResumes_UnchangedPostMissesIndependentNavigationExpectation() => RunCase("network", "SCN-REPLAY-004");

    [Fact, Trait("Scenario", "SCN-REPLAY-005")]
    public void RecordedCheckedState_MissingPostResumes_LegacyFalseRemainsUnknownAndRejected() => RunCase("stay-awake", "SCN-REPLAY-005");

    private static Corpus Load() => JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(),
        "tests/UniClaw.Simulation.Tests/Fixtures/Replay/recorded-operations.json")), Json)!;

    private static void RunCase(string id, string scenarioId)
    {
        var corpus = Load(); var input = corpus.Cases.Single(c => c.Id == id);
        ValidateSequence(input);
        var unchanged = Replay(corpus, input, samePost: true, delayPost: false);
        Export(corpus, input, "unchanged-diagnostic", unchanged, Compare(input, unchanged));
        Assert.Equal("TerminalNotProven", unchanged.Status);
        Assert.Single(unchanged.Receipts);
        var unchangedVerification = Assert.Single(unchanged.Verifications);
        if (id == "network")
        {
            // Current generic tap verifies presence. Independent navigation intent still fails.
            Assert.True(unchangedVerification.IsVerified);
            Assert.Equal("navigationEdges", Compare(input, unchanged, input.DesiredOutcome)!.Path);
        }
        else
        {
            Assert.False(unchangedVerification.IsVerified);
            Assert.Equal("post-action-desired-state-not-satisfied", unchangedVerification.RejectionReason);
            Assert.Equal("VerificationFailed", unchanged.Decisions[^1].Phase);
        }
        Assert.Empty(unchanged.Graph.Edges);
        var failure = Compare(input, unchanged, input.DesiredOutcome);
        Assert.NotNull(failure);
        Export(corpus, input, "unchanged-post", unchanged, failure);

        // The same Run waits without another dispatch, then consumes the imported
        // original post. Unknown checked state remains a refusal after resumption.
        var recovered = Replay(corpus, input, samePost: false, delayPost: true);
        Assert.True(Compare(input, recovered) is null, JsonSerializer.Serialize(Compare(input, recovered), Json));
        Assert.Equal(new[] { "waiting", "still-waiting", "resumed" }, recovered.Checkpoints.Select(c => c.Stage));
        Assert.All(recovered.Checkpoints.Take(2), c =>
        {
            Assert.Equal("WaitingForInput", c.Status);
            Assert.Equal(1, c.Effects);
            Assert.Equal(0, c.Verifications);
        });
        Assert.Equal(1, recovered.Checkpoints[^1].Effects);
        Export(corpus, input, "recovered", recovered, Compare(input, recovered));
        var direct = Replay(corpus, input, samePost: false, delayPost: false);
        Assert.Null(Compare(input, direct));
        Export(corpus, input, "direct", direct, Compare(input, direct));
        var desiredGap = Compare(input, direct, input.DesiredOutcome);
        if (id == "network") Assert.Null(desiredGap);
        else Assert.Equal("verifiedEffects", desiredGap!.Path);
        using var scenario = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(), "scenarios", scenarioId + ".json")));
        var expected = scenario.RootElement.GetProperty("expectations");
        Assert.Equal(expected.GetProperty("status").GetString(), recovered.Status);
        Assert.Equal(expected.GetProperty("effects").GetInt32(), recovered.Receipts.Length);
        Assert.Equal(expected.GetProperty("agentConsultations").GetInt32(), recovered.Consultations);
        // All source frames were used; there are no queued post observations.
        Assert.Equal(2, recovered.Ingress.Length);
        Assert.Equal(expected.GetProperty("unconsumedStimuli").GetInt32(), 2 - recovered.Ingress.Length);
    }

    [Theory, InlineData("network"), InlineData("stay-awake")]
    public void RecordedInputs_RejectChangedBytesBeforeReplay(string id)
    {
        var input = Load().Cases.Single(c => c.Id == id);
        Assert.Throws<InvalidDataException>(() => ReadAsset(input.Before.Path, new string('0', 64)));
        Assert.Throws<InvalidDataException>(() => ReadAsset(input.Imported.Initial.Path, new string('0', 64)));
    }

    [Theory, InlineData("network"), InlineData("stay-awake")]
    public void IndependentExpectedOutcome_DetectsWrongVerificationEvenWhenDeliverySucceeded(string id)
    {
        var corpus = Load(); var input = corpus.Cases.Single(c => c.Id == id);
        var actual = Replay(corpus, input, samePost: false, delayPost: false);
        Export(corpus, input, "direct-diagnostic", actual, Compare(input, actual));
        Assert.Null(Compare(input, actual));
        var corrupted = actual with { Verifications = actual.Verifications.Select(v => v with { IsVerified = !v.IsVerified }).ToArray() };
        Assert.Equal("verifiedEffects", Compare(input, corrupted)!.Path);
        Assert.Single(corrupted.Receipts); // DeliveryCompleted cannot satisfy the independent expected outcome.
    }

    private static Result Replay(Corpus corpus, ReplayCase input, bool samePost, bool delayPost)
    {
        var ledger = new EvidenceLedger();
        var observationStrategy = new ImportedObservationStrategy();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.node.*" },
            new ProductAssociationStrategy(), observationStrategy);
        var effects = new EffectBoundary(new Delivery()); var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new ProductFreshnessEvaluator(() => T0.AddSeconds(3), TimeSpan.FromMinutes(1))), effects);
        var initial = false; var post = false; var released = !delayPost; var consultations = 0;
        var decisions = new List<Consultation>();
        Slice? before = null; CoordinateSpace? targetSpace = null; var ingress = new List<Ingress>();
        RunDriverInput Observe(Asset asset, ObservationDirective directive, int second)
        {
            ReadAsset(asset.Path, asset.Sha256);
            var captureId = input.Id + ":" + second; var time = T0.AddSeconds(second);
            var imported = second == 0 ? input.Imported.Initial : samePost ? input.Imported.Unchanged : input.Imported.Post;
            var capture = JsonSerializer.Deserialize<ImportedCapture>(ReadAsset(imported.Path, imported.Sha256), Json)!;
            observationStrategy.Use(capture);
            var observation = capture.Observation;
            Assert.True(observation.IsValid);
            Assert.Equal(captureId, observation.Metadata.CaptureId);
            Assert.Equal(time, observation.Metadata.CaptureTimestamp);
            var proposals = new List<ObservationProposal>();
            foreach (var subject in new[] { "ui.screen", "ui.screen.route" })
                proposals.Add(new(new(subject, asset.PageKey), IngressKind.Observation, directive.Context,
                    new("replay.context", time, subject + ":" + captureId, ["recorded-xml:" + asset.Path, "sha256:" + asset.Sha256])));
            proposals.AddRange(TypedHierarchyProposalProjector.Project(observation, directive.Context));
            ingress.Add(new(asset.Path, asset.Sha256, captureId, directive.Context.ToString()));
            return new RunDriverInput.Observation(proposals);
        }
        var runtime = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (!initial) { initial = true; return Observe(input.Before, directive, 0); }
                if (directive.Context == ObservationContext.PostActionEffectFlow && released && !post)
                { post = true; return Observe(samePost ? input.Before : input.After, directive, 2); }
                return null;
            },
            ConsultAgent = context =>
            {
                consultations++;
                decisions.Add(new(context.Phase.ToString(), context.FailureReason));
                if (effects.ReceiptLog.Count > 0) return new AgentDecision.NoAction(new(context.DecisionId, "bounded recorded replay complete"));
                var container = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
                before = world.DeriveSlice(container);
                var sourceTarget = Assert.Single(world.Current.Occurrences!, o => o.Role == input.Action.Role && o.SemanticDescriptor == input.Action.Descriptor);
                targetSpace = sourceTarget.Space;
                return new AgentDecision.Act(new(context.DecisionId,
                    [new(input.Action.Role, input.Action.Descriptor, input.Action.EffectClass, input.Action.DesiredState)], "recorded action expression; fresh product grounding"));
            },
        });
        Assert.True(kernel.AdmitContract(new("sim010", "bounded recorded operation replay", new HashSet<string> { "ui.screen", "ui.screen.route", "ui.node.*" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["recorded regression"], TaskInstanceId: "sim010:" + input.Id,
            ProductSessionId: "sim010", ContractRef: "sim010-contract:" + input.Id)).Accepted);
        Assert.True(runtime.Activate().Accepted);
        var result = runtime.Drive(); var checkpoints = new List<Checkpoint>();
        void Record(string stage) => checkpoints.Add(new(stage, result.Status.ToString(), result.Reason, effects.ReceiptLog.Count,
            kernel.PostActionVerifications.Count, world.Current?.RevisionId));
        if (delayPost)
        {
            Record("waiting"); var originalRun = kernel.RunId;
            result = runtime.Drive(); Record("still-waiting");
            released = true;
            result = runtime.Drive(); Record("resumed");
            Assert.Equal(originalRun, kernel.RunId);
        }
        else Record("direct");
        var current = world.Current!; var root = current.WorldState[WorldModel.CurrentContainerSubject].Value;
        var graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}")!;
        Assert.All(ledger.AdmissionLog, a => Assert.Equal(AdmissionDecision.Accepted, a.Decision));
        Assert.All(current.Occurrences!, o => Assert.All(o.EvidenceBasis, id => Assert.Contains(id, ledger.CanonicalRecords.Keys)));
        Assert.Single(effects.ReceiptLog);
        var target = Assert.Single(before!.Occurrences, o => o.Role == input.Action.Role && o.SemanticDescriptor == input.Action.Descriptor);
        Assert.Equal(target.OccurrenceId, effects.ReceiptLog[0].TargetSubject);
        Assert.Equal(corpus.Width, targetSpace!.PixelWidth); Assert.Equal(corpus.Height, targetSpace.PixelHeight);
        return new(result.Status.ToString(), result.Reason, kernel.RunId!, consultations, effects.ReceiptLog.ToArray(),
            kernel.PostActionVerifications.ToArray(), ledger.AdmissionLog.ToArray(), graph, before, world.DeriveSlice(root), ingress.ToArray(), checkpoints.ToArray(), decisions.ToArray());
    }

    private static Divergence? Compare(ReplayCase input, Result actual, Expected? expected = null)
    {
        var e = expected ?? input.ExpectedBehavior;
        if (actual.Receipts.Length != e.Effects) return new("effects", e.Effects.ToString(), actual.Receipts.Length.ToString());
        var verified = actual.Verifications.Count(v => v.IsVerified);
        if (verified != e.VerifiedEffects) return new("verifiedEffects", e.VerifiedEffects.ToString(), verified.ToString());
        if (actual.Status != e.Status) return new("status", e.Status, actual.Status);
        if (actual.Graph.Edges.Count != e.NavigationEdges) return new("navigationEdges", e.NavigationEdges.ToString(), actual.Graph.Edges.Count.ToString());
        foreach (var edge in actual.Graph.Edges)
            if (edge.Kind != ContainerRelationKind.NavigatesTo || edge.SourceContainerId != actual.Before.RootContainerId
                || edge.TargetContainerId != actual.After.RootContainerId || !edge.VerificationSourceRef.Contains(actual.Receipts[0].ReceiptId, StringComparison.Ordinal))
                return new("navigation", "verified before→after", JsonSerializer.Serialize(edge, Json));
        if (e.Phase is not null && actual.Decisions[^1].Phase != e.Phase)
            return new("consultation.phase", e.Phase, actual.Decisions[^1].Phase);
        if (e.RejectionReasonContains is not null && !actual.Verifications.Any(v => v.RejectionReason?.Contains(e.RejectionReasonContains, StringComparison.Ordinal) == true))
            return new("verification.rejectionReason", e.RejectionReasonContains, JsonSerializer.Serialize(actual.Verifications, Json));
        if (e.CheckState)
        {
            var beforeState = actual.Before.Occurrences.Single(o => o.Role == input.Action.Role && o.SemanticDescriptor == input.Action.Descriptor).State;
            var afterState = actual.After.Occurrences.SingleOrDefault(o => o.Role == input.Action.Role && o.SemanticDescriptor == input.Action.Descriptor)?.State;
            if (beforeState != e.BeforeState) return new("beforeState", e.BeforeState ?? "null", beforeState ?? "null");
            if (afterState != e.AfterState) return new("afterState", e.AfterState ?? "null", afterState ?? "null");
        }
        return null;
    }

    private static void ValidateSequence(ReplayCase input)
    {
        using var sequence = JsonDocument.Parse(ReadAsset(input.SequencePath, input.SequenceSha256));
        var frames = sequence.RootElement.GetProperty("frames").EnumerateArray().Select(f => f.GetString()).ToArray();
        Assert.Equal(new[] { Path.GetFileNameWithoutExtension(input.Before.Path), Path.GetFileNameWithoutExtension(input.After.Path) }, frames);
    }
    private static string ReadAsset(string relativePath, string expectedHash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(GoldenPaths.RepoRoot(), relativePath));
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (actual != expectedHash) throw new InvalidDataException(relativePath + ": replay input hash mismatch");
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
    private static void Export(Corpus corpus, ReplayCase input, string mode, Result actual, Divergence? divergence)
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_REGRESSION_REPLAY_OUTPUT") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "recorded-" + input.Id + "-" + mode + ".json"), JsonSerializer.Serialize(new
        { corpus.Source, mode, input, actual, firstDivergence = divergence,
            desiredOutcomeFirstDivergence = Compare(input, actual, input.DesiredOutcome),
            limitation = "Host-validated immutable import of recorded XML; virtual capture clock and deterministic page context/Agent/delivery; no live/model quality or product code fix claim" }, Json));
    }
    // Test-side adapter consumes reviewed proposals, not canonical World state.
    // World mints identities and binds the newly admitted evidence. Candidates
    // are released only after all fields of the imported capture were admitted.
    private sealed class ImportedObservationStrategy : IUiObservationStrategy
    {
        private ImportedCapture? _capture;
        private readonly List<string> _basis = new();
        public void Use(ImportedCapture capture) { _capture = capture; _basis.Clear(); }
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (_capture is not null && record.Provenance.Hierarchy?.CaptureId == _capture.Observation.Metadata.CaptureId)
            {
                _basis.Add(record.EvidenceId);
                if (record.Claim.Subject == _capture.TriggerSubject)
                    return _capture.Candidates.Select(c => c with
                    { OwningContainerId = previous?.WorldState.GetValueOrDefault(WorldModel.CurrentContainerSubject)?.Value, SupportingEvidenceIds = _basis.ToArray() }).ToArray();
            }
            return previous?.Occurrences?.Select(o => new ProposedOccurrence(o.OwningContainerId, o.Role,
                o.SemanticDescriptor, o.State, o.Locator, o.Native, o.Space) { SupportingEvidenceIds = o.EvidenceBasis }).ToArray()
                ?? Array.Empty<ProposedOccurrence>();
        }
    }
    private sealed class Delivery : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => new(DispatchOutcome.DeliveryCompleted, "recorded fixture delivery", T0.AddSeconds(1));
    }
}
