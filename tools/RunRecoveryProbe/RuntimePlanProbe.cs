using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

internal static class RuntimePlanProbe
{
    internal static Task Run(string mode, string directory, string argument)
    {
        if (mode == "read-plan")
        {
            var read = new FileRunRecoveryArchive(directory).ReadRuntimePlanHistory(argument);
            File.WriteAllText(Path.Combine(directory, "plan-reader.json"), JsonSerializer.Serialize(new { ProcessId = Environment.ProcessId, Read = read }));
            return Task.CompletedTask;
        }
        Directory.CreateDirectory(directory);
        using var runSource = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        using var planSource = new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"));
        using var control = new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var run = new RunModel(runSource); var policy = new AgentPlanPolicy(); var scope = new HashSet<string> { "live.frame" };
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(scope, new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy()),
            DisabledRunTrace.Instance, run, new ControlLoop(policy, control), new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery(), execution));
        KernelRunDriver? driver = null; var observations = 0;
        driver = new(kernel, policy, new RunDriverInputs
        {
            RuntimePlanJournal = new HeldAcknowledgement(planSource, directory, run, () => driver!),
            ResourceJournal = resources, ConsultationJournal = p25, RecoveryArchive = new FileRunRecoveryArchive(directory),
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "offline current page" },
            ProjectPlanCandidates = _ => new("scope", "revision", []),
            ProjectTraversal = _ => throw new InvalidOperationException("Unacknowledged Plan must not start traversal"),
            EvaluatePlanCondition = (_, _) => new(PolicyTruth.Satisfied, "scope", "revision", ["owner:condition"]),
            NextInput = directive => ++observations == 1 ? new RunDriverInput.Observation([new(new("live.frame", "synthetic-seed"),
                IngressKind.Observation, directive.Context, new("plan-probe", DateTimeOffset.UtcNow, "original:seed", ["synthetic", "offline"]))]) : null,
            ConsultAgent = context => new AgentDecision.Plan(context.DecisionId, new([
                new PlanItem.RepeatItem([new PlanItem.ForEachItem("current", [new PlanItem.IfItem("ready",
                    [new PlanItem.ObserveItem(null), new PlanItem.CheckpointItem("original checkpoint")],
                    [new PlanItem.ActItem("$candidate", "$candidate", "tap", "on")])], 1)], 2),
                new PlanItem.TraverseItem("current", [new("rule", "always", new("read", TraversalOperationKind.ReadOnly, null, [], []))],
                    1, new("button", "Back", "tap", null)),
                new PlanItem.ControlItem(PlanControlKind.Stop, "original stop")], "original complete graph", "original-plan", 1, "original stage", 64)),
        });
        if (!kernel.AdmitContract(new("plan-probe-v1", "original Plan history", scope, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            MaxConsultations: 4, MaxTotalSteps: 8, TaskInstanceId: "plan-task", ProductSessionId: "plan-session", ContractRef: "plan-contract")).Accepted
            || !driver.Activate().Accepted) throw new InvalidOperationException("original Plan admission/activation failed");
        var result = driver.Drive();
        throw new InvalidOperationException($"Plan carrier did not reach adoption: {result.Status}:{result.Reason}");
    }

    private sealed class HeldAcknowledgement(IRuntimePlanJournal inner, string directory, RunModel run,
        Func<KernelRunDriver> driver) : IRuntimePlanJournal
    {
        public bool CanInitialize => inner.CanInitialize;
        public string RecordingId => inner.RecordingId;
        public IReadOnlyList<RuntimePlanHistoryRecord> ReadRecords() => inner.ReadRecords();
        public void Append(RuntimePlanHistoryRecord row)
        {
            inner.Append(row);
            if (row.Operation != "adopt") return;
            File.WriteAllText(Path.Combine(directory, "plan-expected.json"), JsonSerializer.Serialize(new
            { SourceRows = inner.ReadRecords(), Published = driver().RuntimePlanHistory }));
            File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, SourceSequence = row.Sequence,
                PublishedSequence = driver().RuntimePlanHistory!.Position.Sequence, Plan = driver().PlanExecution,
                RunOperations = run.TraversalOperations, Deliveries = 0,
            }));
            File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
            // Persisted original commit, deliberately before Runtime receives acknowledgement.
            Thread.Sleep(Timeout.Infinite);
        }
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline synthetic"); }
    private sealed class NoDelivery : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("No delivery in Plan history probe"); }
}
