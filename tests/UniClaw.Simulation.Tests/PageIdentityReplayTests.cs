using System.Collections.Frozen;
using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Control;
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

public sealed class PageIdentityReplayTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record ReplayInput(string CaptureId, string Title, string Target, string WrongIdentityCaptureId, ReplayExpected Expected);
    private sealed record ReplayExpected(bool PausedWithoutIdentity, bool WrongIdentityKeepsPaused, int Effects,
        int PostActionVerificationsBeforeRecovery, int DepthBeforeRecovery, int DepthAfterRecovery, int RecoveryDispatches);
    private sealed record Checkpoint(string Stage, string Status, string? Reason, bool NeedsIdentity, int Effects, int Verifications, int Depth);

    [Fact, Trait("Scenario", "SCN-REPLAY-001")]
    public void MissingThenWrongIdentity_ReplaysFailure_AndMatchingEvidenceResumesOriginalTraversal()
    {
        var input = JsonSerializer.Deserialize<ReplayInput>(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(),
            "tests/UniClaw.Simulation.Tests/Fixtures/Replay/page-identity.json")), Json)!;
        var first = Replay(input);
        Assert.Equal(JsonSerializer.Serialize(first, Json), JsonSerializer.Serialize(Replay(input), Json));
        if (Environment.GetEnvironmentVariable("UNICLAW_REGRESSION_REPLAY_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "page-identity-replay.json"), JsonSerializer.Serialize(new
            { source = "hand-authored regression input; existing product recovery, no product code fix", input, actual = first }, Json));
        }
    }

    private static object Replay(ReplayInput input)
    {
        var ledger = new EvidenceLedger(); var policy = new AgentPlanPolicy(); var delivery = new Delivery();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject,
            PageObservationCodec.IdentitySubject, ProductAssociationStrategy.ScreenRouteSubject }, new ProductAssociationStrategy());
        var effects = new EffectBoundary(delivery);
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), effects);
        var initial = false; var post = false; var sentWrong = false; var stage = 0; var consultations = 0;
        var runtime = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Need is { } need)
                {
                    var isChild = need.Capture.CaptureId == input.CaptureId;
                    if (isChild && stage == 0 || isChild && stage == 1 && sentWrong) return null;
                    var wrong = isChild && stage == 1;
                    if (wrong) sentWrong = true;
                    var c = need.Capture;
                    var identity = new PageIdentityObservation(wrong ? input.WrongIdentityCaptureId : c.CaptureId,
                        c.ObservationCycleId, c.SessionCorrelation, c.CaptureTime, "observed:" + c.Items[0].Text, c.Items[0].Text);
                    return new RunDriverInput.Observation([new ObservationProposal(
                        new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)), IngressKind.Observation,
                        directive.Context, new("replay.identity", c.CaptureTime, "identity:" + (wrong ? "wrong" : c.CaptureId), ["synthetic-input"]))
                    { DerivedFromEvidenceIds = [need.CaptureEvidenceId], TransitiveEvidenceBasis = new[] { need.CaptureEvidenceId }.ToFrozenSet(), FusionRule = "replay", FusionVersion = "1" }]);
                }
                if (!initial)
                { initial = true; return Observation("root", "Settings", "Open", 0, directive.Context); }
                if (directive.Context == ObservationContext.PostActionEffectFlow && !post)
                { post = true; return Observation(input.CaptureId, input.Title, input.Target, 2, directive.Context); }
                return null;
            },
            ConsultAgent = context => ++consultations == 1
                ? new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal([
                    new PlanItem.TraverseItem("current", [new("navigate", "go", new("open", TraversalOperationKind.Navigate,
                        new("ui.element", "Open", "tap", null), [], []))], 1, new("ui.element", "Back", "tap", null))],
                    "replay identity recovery", "identity-replay", MaxLocalOperations: 8))
                : new AgentDecision.NoAction(new(context.DecisionId, "bounded recovery replay complete")),
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "replay" },
            ProjectTraversal = _ =>
            {
                var belief = world.Current!;
                if (belief.ObservationNeed is not null) return null;
                var current = belief.WorldState[WorldModel.CurrentContainerSubject].Value;
                var candidates = belief.CurrentPageObservation!.Capture.CaptureId == "root"
                    ? belief.Occurrences!.Where(o => o.SemanticDescriptor == "Open").Select(o => new TraversalCandidate(
                        new("open", o.Role, o.SemanticDescriptor!, PlanCandidateDisposition.Ready, o.EvidenceBasis),
                        new Dictionary<string, PolicyTruth> { ["go"] = PolicyTruth.Satisfied })).ToArray() : [];
                return new(new(belief.RevisionId, kernel.NavigationScope, current, belief.Containers!, belief.Relations!, []),
                    candidates, false, belief.EvidenceBasis.ToArray());
            },
            MaxObservationRecoveryRequests = 8,
            ObservationRecoveryTimeout = TimeSpan.FromSeconds(5),
        });
        Assert.True(kernel.AdmitContract(new("replay", "identity recovery", new HashSet<string> {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject, ProductAssociationStrategy.ScreenRouteSubject },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["replay"], TaskInstanceId: "identity-task",
            ProductSessionId: "identity-session", ContractRef: "identity-contract")).Accepted);
        Assert.True(runtime.Activate().Accepted);
        var checkpoints = new List<Checkpoint>();
        void Record(string name, RunDriveResult result) => checkpoints.Add(new(name, result.Status.ToString(), result.Reason,
            world.Current!.ObservationNeed is not null, effects.ReceiptLog.Count, kernel.PostActionVerifications.Count, runtime.TraversalExecution!.Depth));
        var waiting = runtime.Drive(); Record("missing-identity", waiting);
        Assert.Equal(input.Expected.PausedWithoutIdentity, waiting.Status == RunDriveStatus.WaitingForInput);
        Assert.NotNull(world.Current!.ObservationNeed);
        Assert.Equal(input.Expected.Effects, effects.ReceiptLog.Count);
        Assert.Equal(input.Expected.DepthBeforeRecovery, runtime.TraversalExecution!.Depth);
        Assert.Equal(input.Expected.PostActionVerificationsBeforeRecovery, kernel.PostActionVerifications.Count);
        Assert.All(world.Current.Occurrences!, o => Assert.Null(o.OwningContainerId));
        var before = world.Current; var frozen = JsonSerializer.Serialize(before, Json);

        stage = 1;
        var rejected = runtime.Drive(); Record("wrong-capture", rejected);
        Assert.Equal(input.Expected.WrongIdentityKeepsPaused, rejected.Status == RunDriveStatus.WaitingForInput);
        Assert.True(sentWrong);
        Assert.NotNull(world.Current!.ObservationNeed);
        Assert.Equal(input.Expected.Effects, effects.ReceiptLog.Count);
        Assert.Empty(kernel.PostActionVerifications);
        Assert.Equal(input.Expected.DepthBeforeRecovery, runtime.TraversalExecution.Depth);
        // Ledger admission checks integrity, not identity truth: a structurally
        // valid but mismatched identity must still not authorize traversal.
        Assert.Contains(ledger.CanonicalRecords.Values, r => r.Claim.Subject == PageObservationCodec.IdentitySubject
            && r.Claim.Value.Contains(input.WrongIdentityCaptureId, StringComparison.Ordinal));

        stage = 2; var dispatchedBefore = effects.ReceiptLog.Count;
        var resumed = runtime.Drive(); Record("matching-evidence", resumed);
        Assert.Null(world.Current!.ObservationNeed);
        Assert.True(Assert.Single(kernel.PostActionVerifications).IsVerified);
        Assert.Equal(input.Expected.DepthAfterRecovery, runtime.TraversalExecution.Depth);
        Assert.Equal(input.Expected.RecoveryDispatches, effects.ReceiptLog.Count - dispatchedBefore);
        Assert.Equal(input.Expected.Effects, effects.ReceiptLog.Count);
        Assert.Equal(frozen, JsonSerializer.Serialize(before, Json));
        Assert.All(world.Current.Occurrences!, o => Assert.Equal(world.Current.CurrentPageObservation!.ContainerId, o.OwningContainerId));
        using var scenario = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenPaths.RepoRoot(), "scenarios/SCN-REPLAY-001.json")));
        var summary = scenario.RootElement.GetProperty("expectations");
        Assert.Equal(summary.GetProperty("effects").GetInt32(), effects.ReceiptLog.Count);
        Assert.Equal(summary.GetProperty("agentConsultations").GetInt32(), consultations);
        Assert.Equal(summary.GetProperty("status").GetString(), resumed.Status.ToString());
        return new { checkpoints, admissions = ledger.AdmissionLog, receipts = effects.ReceiptLog,
            verifications = kernel.PostActionVerifications, graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}") };
    }

    private static RunDriverInput Observation(string id, string title, string target, int second, ObservationContext context)
    {
        var time = T0.AddSeconds(second);
        var capture = new PageCaptureObservation(id, "cycle:" + id, "identity-replay", time,
            CoordinateSpace.DeviceViewport(1000, 1000, captureId: id), [new("title", "text", title, new(100, 50, 900, 100)), new("row", "ui.element", target, new(100, 200, 900, 300))]);
        return new RunDriverInput.Observation([
            new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
                context, new("replay.capture", time, "capture:" + id, ["synthetic-input"])),
            new(new(ProductAssociationStrategy.ScreenRouteSubject, id), IngressKind.Observation,
                context, new("replay.route", time, "route:" + id, ["synthetic-input"]))]);
    }
    private sealed class Delivery : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => new(DispatchOutcome.DeliveryCompleted, "replay", T0.AddSeconds(1));
    }
    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "fixed-time replay");
    }
}
