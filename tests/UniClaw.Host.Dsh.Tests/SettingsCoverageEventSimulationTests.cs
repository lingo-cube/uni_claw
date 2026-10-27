using System.Net;
using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

// Real Host/Kernel/codec; only acquisition, Agent answers and delivery are synthetic.
public sealed class SettingsCoverageEventSimulationTests
{
    private const string Route = "synthetic:settings";
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
    private static readonly JsonSerializerOptions Json = ProductProtocolJson.CreateOptions();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task HostStagnationReachesWireWithOneCutoffAndNewDecision(bool retry, bool cooldown)
    {
        var feed = new SyntheticFeed(false);
        using var handler = new AgentHandler(retry, stopAtFirst: false, cooldown);
        await using var peer = Peer(handler);
        var contexts = new List<AgentDecisionContext>();
        var root = Path.Combine(Path.GetTempPath(), "sim024-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await Task.Run(() => Run(root, feed, context =>
            {
                contexts.Add(context);
                try { return Consult(peer, context, "synthetic-request-" + contexts.Count); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(root, "exception.txt"), ex.ToString()); throw; }
            }));
            Export(cooldown ? "cooldown" : retry ? "retry" : "normal", result.RunDir, handler.Bodies);
            // Frozen specification: unchanged relevant evidence must not rearm after cooldown alone.
            if (!cooldown) Assert.True(contexts.Count == 2, $"consults={contexts.Count}, status={result.Status}, reason={result.Reason}");
            Assert.Null(contexts[0].Heartbeat);
            Assert.NotEqual(contexts[0].DecisionId, contexts[1].DecisionId);
            Assert.NotEqual(contexts[0].DecisionBoundaryId, contexts[1].DecisionBoundaryId);
            Assert.Equal(0, feed.Deliveries);
            Assert.Empty(result.Steps);
            if (!cooldown) Assert.Contains("synthetic-stagnation-stop", result.Reason);
            var eventRequests = handler.Bodies.Where(b => b.GetProperty("context")
                .TryGetProperty("heartbeat", out var h) && h.ValueKind == JsonValueKind.Object).ToArray();
            Assert.Equal(retry ? 2 : 1, eventRequests.Length);
            if (retry) Assert.Equal(eventRequests[0].GetRawText(), eventRequests[1].GetRawText());
            var wire = eventRequests[^1].GetProperty("context");
            Assert.Equal(contexts[1].DecisionId, wire.GetProperty("decisionId").GetString());
            var heartbeat = wire.GetProperty("heartbeat");
            var packet = wire.GetProperty("casePacket");
            Assert.Equal("coverage-stagnant", packet.GetProperty("caseKind").GetString());
            Assert.Equal(heartbeat.GetRawText(), packet.GetProperty("heartbeatSnapshot").GetRawText());
            Assert.Equal(heartbeat.GetProperty("eventDelta").GetRawText(), packet.GetProperty("eventDelta").GetRawText());
            Assert.Equal(wire.GetProperty("contextReferenceIndex").GetRawText(), packet.GetProperty("relevantRefs").GetRawText());
            var cutoff = heartbeat.GetProperty("toEventSeq").GetInt64();
            var from = heartbeat.GetProperty("fromEventSeq").GetInt64();
            Assert.True(cutoff > 0);
            Assert.Equal(Route, heartbeat.GetProperty("worldModel").GetProperty("currentRoute").GetString());
            Assert.True(heartbeat.GetProperty("kernel").GetProperty("nextEffectBlocked").GetBoolean());
            Assert.True(heartbeat.GetProperty("traversal").GetProperty("frontier").GetArrayLength() > 0);
            var events = JsonDocument.Parse("[" + File.ReadAllText(Path.Combine(result.RunDir, "coverage-events.jsonl"))
                .Trim().Replace("}\n{", "},{") + "]").RootElement.EnumerateArray().ToArray();
            Assert.NotEmpty(heartbeat.GetProperty("eventDelta").EnumerateArray());
            foreach (var item in heartbeat.GetProperty("eventDelta").EnumerateArray())
            {
                var seq = item.GetProperty("eventSeq").GetInt64();
                Assert.InRange(seq, from, cutoff);
                Assert.Contains(events, e => e.GetProperty("sequence").GetInt64() == seq);
            }
            foreach (var reference in wire.GetProperty("contextReferenceIndex").EnumerateArray())
            {
                var seq = reference.GetProperty("eventSeq").GetInt64();
                Assert.InRange(seq, from, cutoff);
                Assert.Contains(events, e => e.GetProperty("sequence").GetInt64() == seq);
                Assert.False(string.IsNullOrWhiteSpace(reference.GetProperty("semanticLabel").GetString()));
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task NewAcceptedPopupEvidenceRearmsAfterCooldownWithNewDecision()
    {
        var feed = new SyntheticFeed(false);
        using var handler = new AgentHandler(false, stopAtFirst: false, cooldown: true);
        await using var peer = Peer(handler);
        var contexts = new List<AgentDecisionContext>();
        var root = Path.Combine(Path.GetTempPath(), "sim024-rearm-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await Task.Run(() => Run(root, feed, context =>
            {
                contexts.Add(context);
                if (context.Heartbeat is not null) feed.ChangePopup = true;
                return Consult(peer, context, "synthetic-rearm-" + contexts.Count);
            }));
            Export("changed-popup", result.RunDir, handler.Bodies);
            var events = contexts.Where(c => c.Heartbeat is not null).ToArray();
            Assert.Equal(2, events.Length);
            Assert.Null(events[0].Heartbeat!.WorldModel.Overlay);
            Assert.Equal("popup", events[1].Heartbeat!.WorldModel.Overlay);
            Assert.NotEqual(events[0].DecisionId, events[1].DecisionId);
            Assert.True(events[1].Heartbeat!.ToEventSeq > events[0].Heartbeat!.ToEventSeq);
            Assert.Equal(0, feed.Deliveries);
            Assert.Contains("synthetic-stagnation-stop", result.Reason);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task NeedSupplementationReachesSourceWithoutFalseEventConsultation()
    {
        var feed = new SyntheticFeed(true);
        using var handler = new AgentHandler(false, stopAtFirst: true);
        await using var peer = Peer(handler);
        var root = Path.Combine(Path.GetTempPath(), "sim024-need-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await Task.Run(() => Run(root, feed, context => Consult(peer, context, "synthetic-need")));
            Assert.Equal(4, feed.RecoveryPulls);
            var wire = Assert.Single(handler.Bodies).GetProperty("context");
            Assert.True(!wire.TryGetProperty("heartbeat", out var h) || h.ValueKind == JsonValueKind.Null);
            Assert.True(!wire.TryGetProperty("casePacket", out var c) || c.ValueKind == JsonValueKind.Null);
            Assert.Equal(JsonValueKind.Object, wire.GetProperty("screen").ValueKind);
            Assert.NotEmpty(wire.GetProperty("elements").EnumerateArray());
            Assert.Equal(0, feed.Deliveries);
            Export("need", result.RunDir, handler.Bodies);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static SettingsCoverageRunner.RunResult Run(string root, SyntheticFeed feed,
        Func<AgentDecisionContext, AgentDecision?> consult) => SettingsCoverageRunner.Run(root,
        new("synthetic-device", new("synthetic-device", "unused", "/unused", "/unused/python"),
            new("1", new("synthetic event chain", "offline", false, false),
                new(4, 6, 0, 3, 1), new(true, "configured", 0, 0, false, 0), ["Unreached row"],
                new(true, true, true, true), Route, "unused", null, ObservationRecovery: new(15000, 4)),
            consult, ViewportWidth: 100, ViewportHeight: 100, FeedNext: feed.Next,
            CurrentCaptureId: () => feed.CaptureId, EffectDriver: feed, UseCoverageDirector: false));

    // Runner's public consult seam is synchronous. The whole run executes on a pool thread.
    private static AgentDecision? Consult(DshOpenedHttpPeer peer, AgentDecisionContext context, string requestId) =>
        peer.ReceiveDecisionRequestAsync(new(requestId, 1, "synthetic-session", context.RunId, context),
            CancellationToken.None).GetAwaiter().GetResult().Decision;

    private static DshOpenedHttpPeer Peer(HttpMessageHandler handler) => new(new(new Uri("http://127.0.0.1:3081/")),
        credential: new("dsh-auth-synthetic", "synthetic-token", DateTimeOffset.MaxValue), handler: handler);

    private sealed class AgentHandler(bool retry, bool stopAtFirst, bool cooldown = false) : HttpMessageHandler
    {
        public List<JsonElement> Bodies { get; } = [];
        private bool _retried;
        private int _events;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("/api/uniclaw-agent/consult", request.RequestUri!.AbsolutePath);
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement.Clone();
            Bodies.Add(body);
            var context = body.GetProperty("context");
            var id = context.GetProperty("decisionId").GetString()!;
            var hasEvent = context.TryGetProperty("heartbeat", out var h) && h.ValueKind == JsonValueKind.Object;
            if (hasEvent && retry && !_retried)
            {
                _retried = true;
                return Response(new { requestId = body.GetProperty("requestId").GetString(), generation = 1,
                    error = "Selected model is at capacity. Please try a different model." });
            }
            if (hasEvent) _events++;
            AgentDecision decision = (hasEvent && (!cooldown || _events == 2)) || stopAtFirst
                ? new AgentDecision.Blocked(id, "synthetic-stagnation-stop")
                : new AgentDecision.Plan(id, new(Enumerable.Range(0, 8)
                    .Select(_ => (PlanItem)new PlanItem.ObserveItem(null)).ToArray(), "synthetic read-only repeated observation", PlanId: "synthetic-observe-plan-" + id));
            return Response(new { requestId = body.GetProperty("requestId").GetString(), generation = 1,
                task = new { initialization = new { }, payload = decision } });
        }
        private static HttpResponseMessage Response(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value, Json), System.Text.Encoding.UTF8, "application/json") };
    }

    private sealed class SyntheticFeed(bool incompleteIdentity) : IEffectDriver
    {
        public string? CaptureId { get; private set; }
        public int RecoveryPulls { get; private set; }
        public int NormalPulls { get; private set; }
        public bool ChangePopup { get; set; }
        public int Deliveries { get; private set; }
        private int _cycles;
        private bool _popupCaptureStarted;
        private int _captureVersion;
        public DispatchResult Deliver(DispatchRequest request)
        { Deliveries++; throw new InvalidOperationException("synthetic read-only scenario must never deliver"); }
        public RunDriverInput? Next(ObservationDirective directive)
        {
            if (++_cycles > 20) return new RunDriverInput.Cancel("synthetic source bound", Time.AddSeconds(_cycles));
            var need = directive.Need;
            if (need is not null) RecoveryPulls++; else NormalPulls++;
            var newCapture = need is null && (CaptureId is null || ChangePopup && !_popupCaptureStarted);
            if (newCapture)
            {
                _captureVersion++;
                if (ChangePopup) _popupCaptureStarted = true;
                CaptureId = "synthetic-capture-" + _captureVersion;
            }
            var id = CaptureId = need?.Capture.CaptureId ?? CaptureId!;
            var cycle = need?.Capture.ObservationCycleId ?? "synthetic-cycle-" + _captureVersion;
            var time = need?.Capture.CaptureTime ?? Time.AddSeconds(_captureVersion);
            ObservationProposal Proposal(string subject, string value) => new(new(subject, value), IngressKind.Observation,
                directive.Context, new("synthetic-event-source", time, id + ":" + subject + ":" + _cycles, ["synthetic", "offline"]));
            var overlay = Proposal(OverlayObservationCodec.Subject, OverlayObservationCodec.Encode(new(id, cycle,
                "synthetic-session", time, ChangePopup ? "present" : "absent", "synthetic-window-source")));
            if (need is not null && incompleteIdentity && RecoveryPulls < 4) return new RunDriverInput.Observation([overlay]);
            var identity = Proposal(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(
                new PageIdentityObservation(id, cycle, "synthetic-session", time, "Settings", "Settings")));
            if (need is not null) return new RunDriverInput.Observation([identity with
            {
                DerivedFromEvidenceIds = [need.CaptureEvidenceId],
                TransitiveEvidenceBasis = new HashSet<string> { need.CaptureEvidenceId },
                FusionRule = "synthetic-identity", FusionVersion = "1",
            }]);
            // Reobserve the frozen source after identity completion: no new capture or recovery allowance.
            if (!incompleteIdentity && !newCapture && _cycles > 2) return new RunDriverInput.Observation([
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, Route), overlay]);
            var capture = new PageCaptureObservation(id, cycle, "synthetic-session", time,
                CoordinateSpace.DeviceViewport(100, 100, captureId: id),
                [new("title", "text", "Settings", new(0, 0, 90, 20)),
                 new("row", "ui.element", "Unreached row", new(0, 30, 90, 60))])
                { DeviceId = "synthetic-device", RequiresOverlayEvidence = true };
            var proposals = new List<ObservationProposal>
            {
                Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, Route), overlay,
            };
            // Identity is supplied on the Need pull with its accepted capture evidence lineage.
            return new RunDriverInput.Observation(proposals);
        }
    }

    private static void Export(string mode, string runDir, IReadOnlyList<JsonElement> bodies)
    {
        var destination = Environment.GetEnvironmentVariable("SIM024_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(destination)) return;
        var dir = Path.Combine(destination, mode); Directory.CreateDirectory(dir);
        var exception = Path.Combine(Directory.GetParent(runDir)!.FullName, "exception.txt");
        if (File.Exists(exception)) File.Copy(exception, Path.Combine(dir, "exception.txt"), true);
        File.WriteAllText(Path.Combine(dir, "wire.json"), JsonSerializer.Serialize(bodies, Json));
        foreach (var name in new[] { "coverage-events.jsonl", "consultations.journal.jsonl", "consult-bridge.json", "facts.json" })
            File.Copy(Path.Combine(runDir, name), Path.Combine(dir, name), true);
    }
}
