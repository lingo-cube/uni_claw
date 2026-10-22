using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Capability;
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

/// <summary>Original recorded layouts through public runtime seams, with explicit
/// controlled identity/agent/delivery substitutes. This is not a replay of Host
/// title recognition, the original model decisions, or the complete recorded Run.</summary>
public sealed class RecentRecordingReplayTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record Asset(string Path, string Sha256);
    private sealed record SourceAsset(string Path, string Sha256, string CaptureId, DateTimeOffset ReplayTime);
    private sealed record Imported(UiHierarchyObservation Observation, ProposedOccurrence[] Candidates);
    private sealed record Pair(Asset Before, Asset After);
    private sealed record Identity(string Signature, string Title);
    private sealed record Identities(Identity Before, Identity After);
    private sealed record ActionInput(string Role, string Descriptor, string EffectClass);
    private sealed record Expected(int Effects, int VerificationsBeforeRecovery, int DepthBeforeRecovery,
        int DepthAfterRecovery, int RecoveryDispatches, string Status, string[] AfterControls);
    private sealed record Case(string Id, SourceAsset Before, SourceAsset After, string RouteBefore, string RouteAfter,
        Pair Imported, Identities IdentityInput, ActionInput Action, Expected Expected);
    private sealed record Corpus(int Width, int Height, Case[] Cases);
    private sealed record Checkpoint(string Stage, string Status, string? Reason, int Effects,
        int Verifications, int Depth, string? ContainerId, string?[] Owners);

    [Theory, InlineData("sims"), InlineData("all-apps"), InlineData("default-apps"), Trait("Scenario", "SCN-REPLAY-007")]
    public void RecentRecordedPage_MissingAndWrongIdentityPause_MatchingEvidenceResumesWithoutRedispatch(string id)
    {
        var corpus = JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(),
            "tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json")), Json)!;
        var c = corpus.Cases.Single(c => c.Id == id);
        var result = Replay(corpus, c);
        Assert.Equal(JsonSerializer.Serialize(result, Json), JsonSerializer.Serialize(Replay(corpus, c), Json));
        if (Environment.GetEnvironmentVariable("UNICLAW_REGRESSION_REPLAY_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "recent-" + id + ".json"), JsonSerializer.Serialize(new
            { source = "recorded XML layouts; controlled perception/agent/effect delivery; fixed replay clock", input = c, actual = result }, Json));
        }
    }

    private static string Read(Asset asset)
    {
        var bytes = File.ReadAllBytes(Path.Combine(GoldenPaths.RepoRoot(), asset.Path));
        Assert.Equal(asset.Sha256, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    // Test adapter only: generic typed text/content-description plus publicly
    // imported actionable candidates become a PageCapture layout. No canonical
    // IDs/owners, title heuristics, checked authority, or expected values enter.
    private static PageCaptureObservation LoadCapture(Corpus corpus, SourceAsset source, Asset asset)
    {
        Read(new(source.Path, source.Sha256));
        var imported = JsonSerializer.Deserialize<Imported>(Read(asset), Json)!;
        var m = imported.Observation.Metadata;
        Assert.Equal(source.CaptureId, m.CaptureId); Assert.Equal(source.ReplayTime, m.CaptureTimestamp);
        var items = new List<SemanticLayoutItem>();
        foreach (var n in imported.Observation.Nodes)
        {
            if (!n.Bounds.TryGetObserved(out var b) || !b.IsValid || b.X1 < 0 || b.Y1 < 0
                || b.X2 > corpus.Width || b.Y2 > corpus.Height) continue;
            var text = n.Text.TryGetObserved(out var t) && !string.IsNullOrWhiteSpace(t) ? t
                : n.ContentDescription.TryGetObserved(out var d) ? d : null;
            if (!string.IsNullOrWhiteSpace(text)) items.Add(new("text:" + n.OccurrenceRef.LocalIndex, "text", text, b));
        }
        foreach (var (o, i) in imported.Candidates.Select((o, i) => (o, i)))
        {
            Assert.Null(o.OwningContainerId); Assert.True(o.SupportingEvidenceIds is null or { Count: 0 });
            if (o.Locator is not { } b || string.IsNullOrWhiteSpace(o.SemanticDescriptor)) continue;
            items.Add(new("candidate:" + i, o.Role, o.SemanticDescriptor, new(
                (int)Math.Round(b.X1 * corpus.Width), (int)Math.Round(b.Y1 * corpus.Height),
                (int)Math.Round(b.X2 * corpus.Width), (int)Math.Round(b.Y2 * corpus.Height))));
        }
        return new(m.CaptureId, m.ObservationCycleId!, m.SessionCorrelation, m.CaptureTimestamp, m.Space!, items);
    }

    private static object Replay(Corpus corpus, Case c)
    {
        var beforeCapture = LoadCapture(corpus, c.Before, c.Imported.Before);
        var afterCapture = LoadCapture(corpus, c.After, c.Imported.After);
        var ledger = new EvidenceLedger(); var policy = new AgentPlanPolicy();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, ProductAssociationStrategy.ScreenRouteSubject }, new ProductAssociationStrategy());
        var effects = new EffectBoundary(new Delivery(c.Before.ReplayTime.AddSeconds(1)));
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), effects);
        var initial = false; var post = false; var sentWrong = false; var stage = 0; var consultations = 0;
        WorldBeliefRevision? parent = null; string? parentFrozen = null; string? parentId = null;
        RunDriverInput Observation(PageCaptureObservation capture, string route, ObservationContext context) =>
            new RunDriverInput.Observation([
                new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
                    context, new("recorded-layout-adapter", capture.CaptureTime, "capture:" + capture.CaptureId, ["recorded-xml", "replay-clock"])),
                new(new(ProductAssociationStrategy.ScreenRouteSubject, route), IngressKind.Observation,
                    context, new("recorded-route", capture.CaptureTime, "route:" + capture.CaptureId, ["recorded-sequence", "replay-clock"]))]);
        var runtime = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Need is { } need)
                {
                    var isPost = need.Capture.CaptureId == c.After.CaptureId;
                    if (isPost && (stage == 0 || stage == 1 && sentWrong)) return null;
                    var wrong = isPost && stage == 1;
                    if (wrong) sentWrong = true;
                    var identityInput = isPost ? c.IdentityInput.After : c.IdentityInput.Before;
                    var p = need.Capture;
                    var identity = new PageIdentityObservation(wrong ? c.Before.CaptureId : p.CaptureId,
                        p.ObservationCycleId, p.SessionCorrelation, p.CaptureTime, identityInput.Signature, identityInput.Title);
                    return new RunDriverInput.Observation([new ObservationProposal(
                        new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)), IngressKind.Observation,
                        directive.Context, new("controlled-identity-substitute", p.CaptureTime,
                            "identity:" + (wrong ? "wrong:" : "matching:") + p.CaptureId, ["controlled-perception-substitute"]))
                    { DerivedFromEvidenceIds = [need.CaptureEvidenceId], TransitiveEvidenceBasis = new[] { need.CaptureEvidenceId }.ToFrozenSet(),
                        FusionRule = "explicit-replay-identity", FusionVersion = "1" }]);
                }
                if (!initial) { initial = true; return Observation(beforeCapture, c.RouteBefore, directive.Context); }
                if (directive.Context == ObservationContext.PostActionEffectFlow && !post)
                { post = true; return Observation(afterCapture, c.RouteAfter, directive.Context); }
                return null;
            },
            ConsultAgent = context => ++consultations == 1
                ? new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal([
                    new PlanItem.TraverseItem("current", [new("navigate", "go", new("recorded-target", TraversalOperationKind.Navigate,
                        new(c.Action.Role, c.Action.Descriptor, c.Action.EffectClass, null), [], []))], 1,
                        new("ui.element", "Navigate up", "tap", null))], "one forward hop of recorded layout recovery", "recent-recording", MaxLocalOperations: 1))
                : new AgentDecision.NoAction(new(context.DecisionId, "bounded recorded layout replay complete")),
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "replay" },
            ProjectTraversal = _ =>
            {
                var belief = world.Current!;
                if (belief.ObservationNeed is not null) return null;
                var current = belief.WorldState[WorldModel.CurrentContainerSubject].Value;
                var isBefore = belief.CurrentPageObservation!.Capture.CaptureId == c.Before.CaptureId;
                if (isBefore && parent is null)
                { parent = belief; parentFrozen = JsonSerializer.Serialize(parent, Json); parentId = current; }
                var candidates = isBefore ? belief.Occurrences!.Where(o => o.Role == c.Action.Role && o.SemanticDescriptor == c.Action.Descriptor)
                    .Select(o => new TraversalCandidate(new("recorded-target", o.Role, o.SemanticDescriptor!, PlanCandidateDisposition.Ready, o.EvidenceBasis),
                        new Dictionary<string, PolicyTruth> { ["go"] = PolicyTruth.Satisfied })).ToArray() : [];
                return new(new(belief.RevisionId, kernel.NavigationScope, current, belief.Containers!, belief.Relations!, []),
                    candidates, false, belief.EvidenceBasis.ToArray());
            },
            MaxObservationRecoveryRequests = 8, ObservationRecoveryTimeout = TimeSpan.FromSeconds(5),
        });
        Assert.True(kernel.AdmitContract(new("recent-" + c.Id, "recorded layout recovery", new HashSet<string> {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject, ProductAssociationStrategy.ScreenRouteSubject },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["replay"], TaskInstanceId: "recent-" + c.Id,
            ProductSessionId: "sim012", ContractRef: "recent-recorded-contract")).Accepted);
        Assert.True(runtime.Activate().Accepted);
        var checkpoints = new List<Checkpoint>();
        void Record(string name, RunDriveResult result) => checkpoints.Add(new(name, result.Status.ToString(), result.Reason,
            effects.ReceiptLog.Count, kernel.PostActionVerifications.Count, runtime.TraversalExecution!.Depth,
            world.Current!.CurrentPageObservation!.ContainerId, world.Current.Occurrences!.Select(o => o.OwningContainerId).Distinct().ToArray()));
        void AssertPaused(RunDriveResult result)
        {
            Assert.Equal(RunDriveStatus.WaitingForInput, result.Status); Assert.NotNull(world.Current!.ObservationNeed);
            Assert.Null(world.Current.CurrentPageObservation!.ContainerId);
            Assert.Equal(c.Expected.Effects, effects.ReceiptLog.Count);
            Assert.Equal(c.Expected.VerificationsBeforeRecovery, kernel.PostActionVerifications.Count);
            Assert.Equal(c.Expected.DepthBeforeRecovery, runtime.TraversalExecution!.Depth);
            Assert.NotEmpty(world.Current.Occurrences!);
            Assert.All(world.Current.Occurrences!, o => Assert.Null(o.OwningContainerId));
            foreach (var control in c.Expected.AfterControls) Assert.Contains(world.Current.Occurrences!, o => o.SemanticDescriptor == control);
        }
        var waiting = runtime.Drive(); Record("missing-identity", waiting); AssertPaused(waiting);
        var unresolved = world.Current; var unresolvedFrozen = JsonSerializer.Serialize(unresolved, Json);
        stage = 1; var rejected = runtime.Drive(); Record("wrong-capture", rejected); AssertPaused(rejected); Assert.True(sentWrong);
        Assert.Contains(ledger.CanonicalRecords.Values, r => r.Claim.Subject == PageObservationCodec.IdentitySubject
            && PageObservationCodec.TryReadIdentity(r.Claim.Value, out var i) && i!.CaptureId == c.Before.CaptureId
            && i.CaptureTime == c.After.ReplayTime);
        stage = 2; var dispatched = effects.ReceiptLog.Count;
        var resumed = runtime.Drive(); Record("matching-evidence", resumed);
        Assert.Null(world.Current!.ObservationNeed); Assert.NotNull(parentId);
        var childId = world.Current.CurrentPageObservation!.ContainerId;
        Assert.NotNull(childId); Assert.NotEqual(parentId, childId);
        Assert.Equal(c.Expected.Status, resumed.Status.ToString());
        Assert.True(Assert.Single(kernel.PostActionVerifications).IsVerified);
        Assert.Equal(c.Expected.DepthAfterRecovery, runtime.TraversalExecution!.Depth);
        Assert.Equal(c.Expected.Effects, effects.ReceiptLog.Count);
        Assert.Equal(c.Expected.RecoveryDispatches, effects.ReceiptLog.Count - dispatched);
        Assert.Equal(parentFrozen, JsonSerializer.Serialize(parent, Json));
        Assert.Equal(unresolvedFrozen, JsonSerializer.Serialize(unresolved, Json));
        Assert.All(world.Current.Occurrences!, o => Assert.Equal(childId, o.OwningContainerId));
        foreach (var control in c.Expected.AfterControls) Assert.Contains(world.Current.Occurrences!, o => o.SemanticDescriptor == control && o.OwningContainerId == childId);
        using var scenario = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(), "scenarios/SCN-REPLAY-007.json")));
        Assert.Equal(scenario.RootElement.GetProperty("expectations").GetProperty("effects").GetInt32(), effects.ReceiptLog.Count);
        Assert.Equal(scenario.RootElement.GetProperty("expectations").GetProperty("agentConsultations").GetInt32(), consultations);
        return new { checkpoints, admissions = ledger.AdmissionLog, receipts = effects.ReceiptLog,
            verifications = kernel.PostActionVerifications, graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}") };
    }

    private sealed class Delivery(DateTimeOffset time) : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => new(DispatchOutcome.DeliveryCompleted, "recorded-layout-no-device", time); }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "fixed replay clock; no live freshness claim"); }
}
