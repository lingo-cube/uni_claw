using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

internal static class ControlTraversalProbe
{
    internal static Task Run(string mode, string directory, string operation)
    {
        if (mode == "read-control")
        {
            var read = new FileRunRecoveryArchive(directory).ReadControlTraversalHistory(operation);
            File.WriteAllText(Path.Combine(directory, "control-reader.json"), JsonSerializer.Serialize(new { ProcessId = Environment.ProcessId, Read = read }));
            return Task.CompletedTask;
        }
        Directory.CreateDirectory(directory);
        using var state = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        using var original = new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var run = new RunModel(state); var feed = new NavigationFeed(); var policy = new AgentPlanPolicy(); ControlLoop? control = null;
        var held = new HeldAcknowledgement(original, operation, directory, run, feed, () => control!.TraversalHistory!);
        control = new(policy, held);
        var scope = new HashSet<string> { "live.frame", "ui.*" };
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(scope, new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy()),
            DisabledRunTrace.Instance, run, control, new RuntimeAssurance(new Fresh()), new EffectBoundary(feed, execution));
        var consultations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = resources, ConsultationJournal = p25, RecoveryArchive = new FileRunRecoveryArchive(directory),
            NextInput = feed.Next, AvailablePlanSources = new Dictionary<string, string> { ["current"] = "offline native current page" },
            ProjectTraversal = _ =>
            {
                var graph = kernel.ProjectWorldGraph($"{kernel.RunId}:control-probe-v1")!;
                var belief = kernel.CurrentBelief!;
                var candidates = belief.Occurrences!.Where(o => o.OwningContainerId == graph.CurrentContainerId && o.SemanticDescriptor == "Open child")
                    .Select(o => new TraversalCandidate(new("entry", o.Role, o.SemanticDescriptor!, PlanCandidateDisposition.Ready, o.EvidenceBasis),
                        new Dictionary<string, PolicyTruth> { ["always"] = PolicyTruth.Satisfied })).ToArray();
                return new(graph, candidates, true, belief.EvidenceBasis.ToArray());
            },
            ConsultAgent = context => ++consultations == 1 ? new AgentDecision.Plan(context.DecisionId,
                new([new PlanItem.TraverseItem("current", [new("navigation-rule", "always", new("open", TraversalOperationKind.Navigate,
                    new("ui.element", "Open child", "tap", null), [], []))], 2, new("ui.element", "Back", "tap", null))],
                    "original offline control path", "control-plan", MaxLocalOperations: 16))
                : new AgentDecision.NoAction(new(context.DecisionId, "no further work")),
        });
        if (!kernel.AdmitContract(new("control-probe-v1", "original Control path", scope, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            MaxConsultations: 4, MaxTotalSteps: 8, TaskInstanceId: "control-task", ProductSessionId: "control-session", ContractRef: "control-contract")).Accepted
            || !driver.Activate().Accepted) throw new InvalidOperationException("original Control admission/activation failed");
        var result = driver.Drive();
        throw new InvalidOperationException($"Control carrier did not reach {operation}: {result.Status}:{result.Reason}; {JsonSerializer.Serialize(driver.TraversalExecution)}");
    }

    private sealed class HeldAcknowledgement(IControlTraversalJournal inner, string operation, string directory,
        RunModel run, NavigationFeed feed, Func<ControlTraversalHistoryRead> published) : IControlTraversalJournal
    {
        public bool CanInitialize => inner.CanInitialize;
        public string RecordingId => inner.RecordingId;
        public IReadOnlyList<ControlTraversalHistoryRecord> ReadRecords() => inner.ReadRecords();
        public void Append(ControlTraversalHistoryRecord row)
        {
            inner.Append(row);
            if (row.Operation != operation) return;
            var acknowledged = published();
            File.WriteAllText(Path.Combine(directory, "control-expected.json"), JsonSerializer.Serialize(new
            { Row = row, Published = acknowledged, SourceRows = inner.ReadRecords() }));
            File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, Operation = operation,
                SourceSequence = row.Sequence, PublishedSequence = acknowledged.Position.Sequence, feed.Deliveries,
                RunOperations = run.TraversalOperations, RunProgress = run.State!.Progress,
            }));
            File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
            // Original journal has committed. The Owner has not received acknowledgement or published.
            Thread.Sleep(Timeout.Infinite);
        }
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline synthetic"); }

    private sealed class NavigationFeed : IEffectDriver
    {
        private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        private int _cycles; private bool _child; private PageCaptureObservation? _capture;
        public int Deliveries { get; private set; }
        public DispatchResult Deliver(DispatchRequest request)
        {
            Deliveries++; _child = true;
            return new(DispatchOutcome.DeliveryCompleted, "offline verified navigation", _capture!.CaptureTime.AddMilliseconds(100));
        }
        public RunDriverInput Next(ObservationDirective directive)
        {
            if (++_cycles > 16) return new RunDriverInput.Cancel("offline fixture bound", Time.AddMinutes(1));
            var page = _child ? "Child" : "Settings";
            if (directive.Need is { } need)
            {
                var capture = need.Capture;
                var identity = new ObservationProposal(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
                    capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime, "synthetic-page:" + page, page))),
                    IngressKind.Observation, directive.Context, new("offline-control-identity", capture.CaptureTime, "identity:" + capture.CaptureId, ["synthetic", "offline"]))
                { DerivedFromEvidenceIds = [need.CaptureEvidenceId], TransitiveEvidenceBasis = new HashSet<string> { need.CaptureEvidenceId },
                    FusionRule = "offline.observed-layout-title", FusionVersion = "1" };
                return directive.CompleteObservation([identity]);
            }
            var id = "control-capture-" + _cycles;
            var xml = $"<hierarchy><node text='{page}' resource-id='test:id/title' class='android.widget.TextView' package='com.android.settings' enabled='true' bounds='[0,0][100,20]'/><node text='Open child' resource-id='test:id/open' class='android.widget.Button' package='com.android.settings' clickable='true' enabled='true' bounds='[0,30][100,80]'/></hierarchy>";
            _capture = new(id, "cycle:" + id, "synthetic-session", Time.AddSeconds(_cycles), CoordinateSpace.DeviceViewport(100, 100, captureId: id),
                [new("xml-0", "text", page, new(0, 0, 100, 20)), new("xml-1", "ui.element", "Open child", new(0, 30, 100, 80))])
                { DeviceId = "synthetic-device" };
            var typed = UiAutomatorDump.ParseHierarchyObservation(xml, new(id, _capture.CaptureTime, "synthetic-device", "synthetic-session", 35,
                ObservationCycleId: _capture.ObservationCycleId, Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities, Space: _capture.Space));
            ObservationProposal Proposal(string subject, string value) => new(new(subject, value), IngressKind.Observation, directive.Context,
                new("offline-control-navigation", _capture.CaptureTime, id + ":" + subject, ["synthetic", "offline"]));
            var proposals = new List<ObservationProposal> { Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(_capture)),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, _child ? "synthetic:child" : "synthetic:resources") };
            proposals.AddRange(TypedHierarchyProposalProjector.Project(typed.Observation!, directive.Context, "offline-control-navigation"));
            return directive.CompleteObservation(proposals);
        }
    }
}
