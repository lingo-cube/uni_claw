using System.Collections.Frozen;
using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>Dedicated, non-golden replay carrier. Inputs never read Expected;
/// only product-owned graph transforms are used for content coordinates.</summary>
public sealed class OperationReplayRegressionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly DateTimeOffset T0 = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private sealed record Item(string Id, string Kind, string Text, int X1, int Y1, int X2, int Y2);
    private sealed record Frame(string Page, Item[] Items);
    private sealed record ActionInput(string Role, string Descriptor, string Effect);
    private sealed record NavigationPath(string SourceTitle, string TargetTitle);
    private sealed record Expected(int Receipts, int NavigationEdges, NavigationPath[] NavigationPaths, string[] TransitionStatuses,
        double?[] OffsetsY, Dictionary<string, double[]> ContentY, int PostActionVerifications);
    private sealed record ReplayCase(string Id, Frame[] Frames, ActionInput[] Actions, Expected Expected);
    private sealed record Corpus(string Source, int Width, int Height, int IdentityRecoveryRequests, ReplayCase[] Cases);
    private sealed record ContentPoint(string Label, double Y, string RevisionId, string ReceiptId);
    private sealed record Checkpoint(string CaptureId, string RevisionId, bool NeedsIdentity, int Receipts, int Verifications);
    private sealed record Result(WorldGraphProjection Graph, EffectReceipt[] Receipts,
        WorldBeliefRevision[] Revisions, PostActionEffectVerification[] Verifications,
        AdmissionRecord[] Admissions, ContentPoint[] Content, Checkpoint[] Checkpoints,
        string Status, string? Reason, int Consultations, int UnconsumedFrames);
    private sealed record Divergence(string Path, string Expected, string Actual);

    private static Corpus Load() => JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(
        GoldenPaths.RepoRoot(), "tests/UniClaw.Simulation.Tests/Fixtures/Replay/operations.json")), Json)!;

    [Fact, Trait("Scenario", "SCN-REPLAY-002")]
    public void OperationReplay_ConsumesOwnerGraph_AndMatchesIndependentRoundTrip()
    {
        var corpus = Load(); var input = corpus.Cases.Single(c => c.Id == "translation");
        var actual = Execute(corpus, input);
        Export(corpus, input, actual);
        Assert.True(Compare(input.Expected, actual) is null, JsonSerializer.Serialize(new { divergence = Compare(input.Expected, actual), actual.Status, actual.Reason }, Json));
        Assert.Equal(JsonSerializer.Serialize(actual, Json), JsonSerializer.Serialize(Execute(corpus, input), Json));
        AssertScenarioSummary("SCN-REPLAY-002", actual);
    }

    [Fact, Trait("Scenario", "SCN-REPLAY-003")]
    public void OperationReplay_SpatialFailuresRemainUnknown()
    {
        var corpus = Load();
        foreach (var input in corpus.Cases.Where(c => c.Id != "translation"))
        {
        var actual = Execute(corpus, input);
        Export(corpus, input, actual);
        Assert.True(Compare(input.Expected, actual) is null, JsonSerializer.Serialize(new { divergence = Compare(input.Expected, actual), actual.Status, actual.Reason }, Json));
        Assert.Empty(actual.Content);
        Assert.All(actual.Graph.ViewportTransitions, t => { Assert.Null(t.OffsetX); Assert.Null(t.OffsetY); });
        AssertScenarioSummary("SCN-REPLAY-003", actual);
        }
    }

    [Fact, Trait("Scenario", "SCN-REPLAY-002")]
    public void ReplayFixture_InsufficientRecoveryBudget_ReproducesFailure_BeforeBudgetCorrection()
    {
        var corpus = Load(); var input = corpus.Cases[0];
        var broken = Execute(corpus with { IdentityRecoveryRequests = 4 }, input);
        Assert.Equal("VerificationFailed", broken.Status);
        Assert.Equal("page-identity-observation-exhausted", broken.Reason);
        Assert.Equal(4, broken.Receipts.Length);
        Assert.Single(broken.Graph.Edges);
        Assert.Equal("navigationEdges", Compare(input.Expected, broken)!.Path);
        Export(corpus with { IdentityRecoveryRequests = 4 }, input with { Id = "translation-budget4" }, broken);
        // Same frames/actions/expectations; fix only the fixture recovery budget.
        var corrected = Execute(corpus, input);
        Assert.Null(Compare(input.Expected, corrected));
    }

    [Theory, InlineData("offset"), InlineData("relation"), InlineData("content"), InlineData("verification"),
        InlineData("navigation-target"), InlineData("navigation-kind"), InlineData("navigation-direction")]
    public void IndependentOracle_DetectsWrongReplayResult_AtFirstDivergence(string mutation)
    {
        var corpus = Load(); var input = corpus.Cases[0]; var good = Execute(corpus, input);
        var wrong = mutation switch
        {
            "offset" => good with { Graph = good.Graph with { ViewportTransitions = good.Graph.ViewportTransitions
                .Select((t, i) => i == 0 ? t with { OffsetY = .3 } : t).ToArray() } },
            "relation" => good with { Graph = good.Graph with { Edges = [] } },
            "navigation-target" => good with { Graph = good.Graph with { Edges = good.Graph.Edges.Select((e, i) =>
                i == 0 ? e with { TargetContainerId = e.SourceContainerId } : e).ToArray() } },
            "navigation-kind" => good with { Graph = good.Graph with { Edges = good.Graph.Edges.Select((e, i) =>
                i == 0 ? e with { Kind = ContainerRelationKind.Contains } : e).ToArray() } },
            "navigation-direction" => good with { Graph = good.Graph with { Edges = good.Graph.Edges.Select((e, i) =>
                i == 0 ? e with { SourceContainerId = e.TargetContainerId, TargetContainerId = e.SourceContainerId,
                    SourceRevisionId = e.TargetRevisionId, TargetRevisionId = e.SourceRevisionId } : e).ToArray() } },
            "content" => good with { Content = good.Content.Select((c, i) => i == 0 ? c with { Y = .9 } : c).ToArray() },
            _ => good with { Verifications = [] },
        };
        var divergence = Compare(input.Expected, wrong);
        Assert.NotNull(divergence);
        Assert.Equal(mutation switch { "offset" => "transitions[0].offsetY", "relation" => "navigationEdges",
            "content" => "content.Alpha", "navigation-target" or "navigation-kind" or "navigation-direction" => "navigation[0]", _ => "postActionVerifications" }, divergence!.Path);
        Assert.Null(Compare(input.Expected, good));
        if (Environment.GetEnvironmentVariable("UNICLAW_REGRESSION_REPLAY_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "oracle-" + mutation + ".json"), JsonSerializer.Serialize(new
            { kind = "test-oracle-mutation; not a product defect or product fix", mutation, firstDivergence = divergence, correctedMatches = Compare(input.Expected, good) is null }, Json));
        }
    }

    private static Divergence? Compare(Expected expected, Result actual)
    {
        Divergence? Count(string path, int e, int a) => e == a ? null : new(path, e.ToString(), a.ToString());
        var mismatch = Count("receipts", expected.Receipts, actual.Receipts.Length)
            ?? Count("navigationEdges", expected.NavigationEdges, actual.Graph.Edges.Count)
            ?? Count("postActionVerifications", expected.PostActionVerifications, actual.Verifications.Length)
            ?? Count("transitions", expected.TransitionStatuses.Length, actual.Graph.ViewportTransitions.Count);
        if (mismatch is not null) return mismatch;
        if (expected.NavigationPaths.Length != expected.NavigationEdges)
            throw new InvalidDataException("Expected navigation paths must describe every expected edge");
        for (var i = 0; i < expected.NavigationPaths.Length; i++)
        {
            var edge = actual.Graph.Edges[i]; var path = expected.NavigationPaths[i];
            var source = actual.Revisions.SingleOrDefault(r => r.RevisionId == edge.SourceRevisionId);
            var target = actual.Revisions.SingleOrDefault(r => r.RevisionId == edge.TargetRevisionId);
            if (edge.Kind != ContainerRelationKind.NavigatesTo
                || source?.CurrentPageObservation?.ContainerId != edge.SourceContainerId
                || target?.CurrentPageObservation?.ContainerId != edge.TargetContainerId
                || source?.CurrentPageObservation?.Capture.Items[0].Text != path.SourceTitle
                || target?.CurrentPageObservation?.Capture.Items[0].Text != path.TargetTitle)
                return new($"navigation[{i}]", JsonSerializer.Serialize(path), JsonSerializer.Serialize(edge));
        }
        for (var i = 0; i < expected.TransitionStatuses.Length; i++)
        {
            var transition = actual.Graph.ViewportTransitions[i];
            if (transition.AlignmentStatus != expected.TransitionStatuses[i])
                return new($"transitions[{i}].status", expected.TransitionStatuses[i], transition.AlignmentStatus);
            var e = expected.OffsetsY[i]; var a = transition.OffsetY;
            if (e.HasValue != a.HasValue || e.HasValue && Math.Abs(e.Value - a!.Value) > .000001)
                return new($"transitions[{i}].offsetY", JsonSerializer.Serialize(e), JsonSerializer.Serialize(a));
        }
        foreach (var (label, values) in expected.ContentY.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var actualValues = actual.Content.Where(p => p.Label == label).Select(p => p.Y).ToArray();
            if (values.Length != actualValues.Length || values.Where((v, i) => Math.Abs(v - actualValues[i]) > .000001).Any())
                return new("content." + label, JsonSerializer.Serialize(values), JsonSerializer.Serialize(actualValues));
        }
        if (actual.Content.Any(p => !expected.ContentY.ContainsKey(p.Label)))
            return new("content.unexpected", "[]", JsonSerializer.Serialize(actual.Content.Select(p => p.Label)));
        if (actual.Verifications.Any(v => !v.IsVerified)) return new("verification", "verified", "rejected");
        return null;
    }

    private static Result Execute(Corpus corpus, ReplayCase input)
    {
        var scope = new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            ProductAssociationStrategy.ScreenRouteSubject };
        var world = new WorldModel(scope, new ProductAssociationStrategy());
        var ledger = new EvidenceLedger(); var driver = new ReplayDriver(); var effects = new EffectBoundary(driver);
        var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), effects);
        var consumed = 0; var consultations = 0; var checkpoints = new List<Checkpoint>();
        var runtime = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Need is { } need)
                {
                    var identityCapture = need.Capture;
                    var identity = new PageIdentityObservation(identityCapture.CaptureId, identityCapture.ObservationCycleId,
                        identityCapture.SessionCorrelation, identityCapture.CaptureTime, "observed:" + identityCapture.Items[0].Text, identityCapture.Items[0].Text);
                    var proposal = new ObservationProposal(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)),
                        IngressKind.Observation, directive.Context, new("replay.identity", identityCapture.CaptureTime, "identity:" + identityCapture.CaptureId, ["synthetic-replay"]))
                    { DerivedFromEvidenceIds = [need.CaptureEvidenceId], TransitiveEvidenceBasis = new[] { need.CaptureEvidenceId }.ToFrozenSet(), FusionRule = "replay", FusionVersion = "1" };
                    return new RunDriverInput.Observation([proposal]);
                }
                if (consumed >= input.Frames.Length) return null;
                if (world.Current is { } previous) checkpoints.Add(new("before:" + consumed, previous.RevisionId,
                    previous.ObservationNeed is not null, effects.ReceiptLog.Count, kernel.PostActionVerifications.Count));
                var index = consumed++; var f = input.Frames[index]; var time = T0.AddSeconds(index * 10);
                var captureId = "replay:" + index;
                var capture = new PageCaptureObservation(captureId, "cycle:" + index, "sim009", time,
                    CoordinateSpace.DeviceViewport(corpus.Width, corpus.Height, captureId: captureId),
                    f.Items.Select(i => new SemanticLayoutItem(i.Id, i.Kind, i.Text, new(i.X1, i.Y1, i.X2, i.Y2))).ToArray())
                { DeviceId = "controlled-sim009-device" };
                return new RunDriverInput.Observation([
                    new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
                        directive.Context, new("replay.capture", time, "capture:" + index, ["synthetic-replay"])),
                    new(new(ProductAssociationStrategy.ScreenRouteSubject, f.Page), IngressKind.Observation,
                        directive.Context, new("replay.route", time, "route:" + index, ["synthetic-replay"]))]);
            },
            MaxObservationRecoveryRequests = corpus.IdentityRecoveryRequests,
            ConsultAgent = context =>
            {
                consultations++;
                if (effects.ReceiptLog.Count >= input.Actions.Length)
                    return new AgentDecision.NoAction(new(context.DecisionId, "bounded replay complete"));
                var action = input.Actions[effects.ReceiptLog.Count];
                return new AgentDecision.Act(new(context.DecisionId,
                    [new(action.Role, action.Descriptor, action.Effect, null)], "synthetic replay action"));
            },
        });
        Assert.True(kernel.AdmitContract(new("sim009", "bounded replay", scope, new HashSet<string> { "tap", "swipe-up", "swipe-down" },
            new HashSet<string>(), ["record operations"], TaskInstanceId: "sim009-task", ProductSessionId: "sim009-session", ContractRef: "sim009-contract")).Accepted);
        Assert.True(runtime.Activate().Accepted);
        var result = runtime.Drive();
        var graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}")!;
        var content = new List<ContentPoint>(); double offsetY = 0;
        foreach (var t in graph.ViewportTransitions)
        {
            var receipt = Assert.Single(effects.ReceiptLog, r => r.ReceiptId == t.OperationReceiptId);
            Assert.Equal(receipt.BindingId, t.OperationBindingId);
            Assert.Equal(receipt.TargetSubject, t.SourceTargetOccurrenceId);
            Assert.Contains(t.OperationReceiptId, t.VerificationSourceRef);
            Assert.NotEmpty(t.SourceEvidenceIds); Assert.NotEmpty(t.TargetEvidenceIds);
            if (t.OffsetY is not { } dy || t.OffsetX is null) break;
            offsetY += dy;
            var target = world.RevisionHistory.Single(r => r.RevisionId == t.TargetRevisionId);
            foreach (var pair in t.Correspondences.Where(p => !p.Stationary))
            {
                var occurrence = target.Occurrences!.Single(o => o.OccurrenceId == pair.TargetOccurrenceId);
                content.Add(new(occurrence.SemanticDescriptor!, occurrence.Locator!.Y1 + offsetY, target.RevisionId, t.OperationReceiptId));
            }
        }
        Assert.All(graph.Edges, e => { Assert.Contains(world.RevisionHistory, r => r.RevisionId == e.SourceRevisionId);
            Assert.Contains(world.RevisionHistory, r => r.RevisionId == e.TargetRevisionId); });
        return new(graph, effects.ReceiptLog.ToArray(), world.RevisionHistory.ToArray(), kernel.PostActionVerifications.ToArray(),
            ledger.AdmissionLog.ToArray(), content.ToArray(), checkpoints.ToArray(), result.Status.ToString(), result.Reason, consultations, input.Frames.Length - consumed);
    }

    private static void AssertScenarioSummary(string scenarioId, Result actual)
    {
        using var scenario = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(), "scenarios", scenarioId + ".json")));
        var expected = scenario.RootElement.GetProperty("expectations");
        Assert.Equal(expected.GetProperty("status").GetString(), actual.Status);
        Assert.Equal(expected.GetProperty("effects").GetInt32(), actual.Receipts.Length);
        Assert.Equal(expected.GetProperty("agentConsultations").GetInt32(), actual.Consultations);
        Assert.Equal(expected.GetProperty("unconsumedStimuli").GetInt32(), actual.UnconsumedFrames);
    }

    private static void Export(Corpus corpus, ReplayCase input, Result actual)
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_REGRESSION_REPLAY_OUTPUT") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "operations-" + input.Id + ".json"), JsonSerializer.Serialize(new
        { corpus.Source, input = input.Id, expected = input.Expected, actual, firstDivergence = Compare(input.Expected, actual),
            limitation = "synthetic semantic captures and scripted decisions; product owner graph; no live/model quality claim" }, Json));
    }
    private sealed class ReplayDriver : IEffectDriver
    {
        private int count;
        public DispatchResult Deliver(DispatchRequest request) => new(DispatchOutcome.DeliveryCompleted, "replay:" + request.EffectClass, T0.AddSeconds(++count * 10 - 5));
    }
    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "synthetic fixed-time replay");
    }
}
