using UniClaw.Host.SettingsCoverage;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunResourceAccountingTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-24T00:00:00Z");

    [Fact]
    public void PublicRunnerPersistsConfirmedSemanticConsumption()
    {
        var root = Path.Combine(Path.GetTempPath(), "resource-runner-" + Guid.NewGuid().ToString("N"));
        var feed = new Source();
        string? originalRunId = null;
        try
        {
            var result = SettingsCoverageRunner.Run(root, new("synthetic-device",
                new("synthetic-device", "unused", "/unused", "/unused/python"),
                new("1", new("resource recording", "offline", false, false),
                    new(4, 6, 0, 3, 1), new(true, "configured", 0, 0, false, 0), ["Unreached row"],
                    new(true, true, true, true), "synthetic:resources", "unused", null),
                context =>
                {
                    originalRunId = context.RunId;
                    return new AgentDecision.Blocked(context.DecisionId, "synthetic resource stop");
                },
                ViewportWidth: 100, ViewportHeight: 100, FeedNext: feed.Next,
                CurrentCaptureId: () => feed.CaptureId, EffectDriver: feed, UseCoverageDirector: false));
            Assert.Equal(0, feed.Deliveries);
            Assert.Contains("synthetic resource stop", result.Reason);
            Assert.True(File.Exists(Path.Combine(result.RunDir, "resource.journal.jsonl")),
                "The production Runner must persist confirmed resource accounting, not just a P25 response.");
            var read = FileRunResourceJournal.Inspect(Path.Combine(result.RunDir, "resource.journal.jsonl"),
                result.ResourceBinding, Path.Combine(result.RunDir, "consultations.journal.jsonl"), Path.Combine(result.RunDir, "exec.journal"));
            Assert.NotNull(result.ResourceBinding);
            Assert.Equal(originalRunId, read.Identity!.RunId);
            Assert.True(read.AccountingComplete, read.Reason);
            Assert.Equal(0, read.Confirmed!.StepsConsumed);
            Assert.Equal(1, read.Confirmed.RoundsConsumed);
            Assert.Equal(4, read.StepsRemaining);
            Assert.Equal(5, read.RoundsRemaining);
            Assert.False(read.ContinuationSupported);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void VerifiedDeliveryIsChargedAndReadBackWithoutStalePendingFlag()
    {
        var root = Path.Combine(Path.GetTempPath(), "resource-effect-" + Guid.NewGuid().ToString("N"));
        var feed = new Source(navigation: true);
        string? originalRunId = null;
        try
        {
            var result = SettingsCoverageRunner.Run(root, new("synthetic-device",
                new("synthetic-device", "unused", "/unused", "/unused/python"),
                new("1", new("resource receipt recording", "offline", false, false),
                    new(4, 6, 0, 3, 1), new(true, "configured", 0, 0, false, 0), ["Child"],
                    new(true, true, true, true), "synthetic:resources", "unused", null),
                context =>
                {
                    originalRunId ??= context.RunId;
                    return new AgentDecision.Act(new(context.DecisionId,
                        [new("ui.element", "Open child", "tap", null)], "synthetic navigation"));
                },
                ViewportWidth: 100, ViewportHeight: 100, FeedNext: feed.Next,
                CurrentCaptureId: () => feed.CaptureId, EffectDriver: feed, UseCoverageDirector: false,
                PollCancellation: () => feed.PostIdentitySupplied
                    ? new RunDriverInput.Cancel("synthetic stop after verified delivery", Time.AddMinutes(1)) : null));
            Assert.Equal(1, feed.Deliveries);
            Assert.True(Assert.Single(result.Steps).Verified);
            var read = FileRunResourceJournal.Inspect(Path.Combine(result.RunDir, "resource.journal.jsonl"),
                result.ResourceBinding, Path.Combine(result.RunDir, "consultations.journal.jsonl"), Path.Combine(result.RunDir, "exec.journal"));
            Assert.NotNull(result.ResourceBinding);
            Assert.Equal(originalRunId, read.Identity!.RunId);
            Assert.True(read.AccountingComplete, read.Reason);
            Assert.Equal(1, read.Confirmed!.StepsConsumed);
            Assert.Single(read.Confirmed.ReceiptIds);
            Assert.Equal(3, read.StepsRemaining);
            Assert.False(read.ContinuationSupported);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    internal sealed class Source(bool navigation = false, DispatchOutcome outcome = DispatchOutcome.DeliveryCompleted) : IEffectDriver
    {
        public string? CaptureId { get; private set; }
        public int Deliveries { get; private set; }
        public bool PostIdentitySupplied { get; private set; }
        private int _cycles;
        private bool _child;
        private PageCaptureObservation? _navigationCapture;
        public DispatchResult Deliver(DispatchRequest request)
        {
            Deliveries++;
            if (!navigation) throw new InvalidOperationException("read-only source must never deliver");
            _child = true;
            return new(outcome, "synthetic navigation delivery",
                _navigationCapture!.CaptureTime.AddMilliseconds(100));
        }
        public RunDriverInput Next(ObservationDirective directive)
        {
            if (navigation) return Navigation(directive);
            if (++_cycles > 10) return new RunDriverInput.Cancel("synthetic source bound", Time);
            var need = directive.Need;
            var id = CaptureId = need?.Capture.CaptureId ?? CaptureId ?? "synthetic-resource-capture";
            var cycle = need?.Capture.ObservationCycleId ?? "synthetic-resource-cycle";
            var time = need?.Capture.CaptureTime ?? Time;
            ObservationProposal Proposal(string subject, string value) => new(new(subject, value), IngressKind.Observation,
                directive.Context, new("synthetic-resource-source", time, id + ":" + subject + ":" + _cycles, ["synthetic", "offline"]));
            if (need is not null) return directive.CompleteObservation([Proposal(PageObservationCodec.IdentitySubject,
                PageObservationCodec.Encode(new PageIdentityObservation(id, cycle, "synthetic-session", time, "Settings", "Settings"))) with
            {
                DerivedFromEvidenceIds = [need.CaptureEvidenceId],
                TransitiveEvidenceBasis = new HashSet<string> { need.CaptureEvidenceId },
                FusionRule = "synthetic-identity", FusionVersion = "1",
            }]);
            var capture = new PageCaptureObservation(id, cycle, "synthetic-session", time,
                CoordinateSpace.DeviceViewport(100, 100, captureId: id),
                [new("title", "text", "Settings", new(0, 0, 90, 20)),
                 new("row", "ui.element", "Unreached row", new(0, 30, 90, 60))])
                { DeviceId = "synthetic-device", RequiresOverlayEvidence = true };
            return directive.CompleteObservation([Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, "synthetic:resources"),
                Proposal(OverlayObservationCodec.Subject, OverlayObservationCodec.Encode(new(id, cycle,
                    "synthetic-session", time, "absent", "synthetic-window-source")))]);
        }

        private RunDriverInput Navigation(ObservationDirective directive)
        {
            var page = _child ? "Child" : "Settings";
            if (directive.Need is { } need)
            {
                if (directive.Context == ObservationContext.PostActionEffectFlow) PostIdentitySupplied = true;
                return directive.CompleteObservation([
                    PageIdentityRecovery.Identity(new(need.Capture, need.CaptureEvidenceId, null),
                        page, "synthetic-page:" + page, directive.Context)!]);
            }
            var id = CaptureId = "synthetic-resource-" + ++_cycles;
            var xml = $"<hierarchy><node text='{page}' resource-id='test:id/title' class='android.widget.TextView' package='com.android.settings' enabled='true' bounds='[0,0][100,20]'/><node text='Open child' resource-id='test:id/open' class='android.widget.Button' package='com.android.settings' clickable='true' enabled='true' bounds='[0,30][100,80]'/></hierarchy>";
            _navigationCapture = PageIdentityRecovery.Capture(id, "cycle:" + id, "synthetic-session",
                Time.AddSeconds(_cycles), 100, 100, xml, null) with { DeviceId = "synthetic-device" };
            var typed = UiAutomatorDump.ParseHierarchyObservation(xml, new(id, _navigationCapture.CaptureTime,
                "synthetic-device", "synthetic-session", 35, ObservationCycleId: _navigationCapture.ObservationCycleId,
                Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities, Space: _navigationCapture.Space));
            ObservationProposal Proposal(string subject, string value) => new(new(subject, value), IngressKind.Observation,
                directive.Context, new("synthetic-resource-navigation", _navigationCapture.CaptureTime,
                    id + ":" + subject, ["synthetic", "offline"]));
            var proposals = new List<ObservationProposal>
            {
                Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(_navigationCapture)),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, _child ? "synthetic:child" : "synthetic:resources"),
            };
            proposals.AddRange(TypedHierarchyProposalProjector.Project(typed.Observation!, directive.Context, "synthetic-resource-navigation"));
            return directive.CompleteObservation(proposals);
        }
    }
}
