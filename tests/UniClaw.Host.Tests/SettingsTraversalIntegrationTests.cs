using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using UniClaw.Agent.Dsh;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

// Synthetic acquisition, scripted Agent and spatial delivery; all Runtime owners are production.
public sealed class SettingsTraversalIntegrationTests
{
    private const string Root = "android.settings|rk1:Settings|src=homepage_title|up=0";
    private const string Network = "android.settings|route:Network";
    private const string Apps = "android.settings|route:Apps";
    private const string Scroll = "integration:id/scroll";
    private const string Back = "Navigate up";
    private const string Popup = "SIM name & color";
    private static readonly JsonSerializerOptions Json = ProductProtocolJson.CreateOptions();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TraversalConnectsArrivalClearanceReturnDiscoveryAndAgentInterruption(bool interrupt, bool routeChange)
    {
        var source = new MenuSource(routeChange);
        using var handler = new ScriptedAgent(interrupt);
        await using var peer = new DshOpenedHttpPeer(new(new Uri("http://127.0.0.1:3081/")),
            credential: new("synthetic-auth", "synthetic-token", DateTimeOffset.MaxValue), handler: handler);
        var root = Path.Combine(Path.GetTempPath(), "sim029-" + Guid.NewGuid().ToString("N"));
        var original = SettingsCoverageConfig.LoadDefault();
        var config = original with
        {
            Bounds = new(12, 16, 2, 3, 0, interrupt ? 3 : 100),
            Coverage = new(true, "configured", 1, 0, true, 0, ["Apps"]),
            TargetPages = ["Network"], RootRoute = Root,
            ScrollContainerDescriptor = Scroll, BackDescriptor = Back,
            DynamicRootEntries = new(), DynamicNestedEntries = new(),
            DeepTraversal = new(true, 1, 1, ["Network > SIM name & color"]),
            LanguageInspection = null, NavigationScope = null,
            ObservationRecovery = new(15000, 16),
        };
        var consultations = 0;
        try
        {
            var result = await Task.Run(() => SettingsCoverageRunner.Run(root, new("synthetic-integration",
                new("synthetic-integration", "unused", "/unused", "/unused/python"), config, context =>
                {
                    Assert.True(++consultations <= 16, "bounded scripted consultation");
                    return peer.ReceiveDecisionRequestAsync(new("sim029-" + consultations, 1,
                        "synthetic-integration-session", context.RunId, context), CancellationToken.None)
                        .GetAwaiter().GetResult().Decision;
                }, ViewportWidth: 1000, ViewportHeight: 1000, FeedNext: source.Next,
                CurrentCaptureId: () => source.CaptureId, EffectDriver: source)));
            Export(interrupt, routeChange, source, handler, result);

            // Independent fixed Oracle: CANCEL/scroll/return are not additional visited targets.
            Assert.Equal(new[] { "Network", Popup, "CANCEL", Back, Scroll, "Apps", Back },
                result.Steps.Select(step => step.TargetDescriptor));
            Assert.Equal(new[] { (Root, Network), (Network, routeChange ? "android.settings" : Network), (routeChange ? "android.settings" : Network, Network),
                (Network, Root), (Root, Root), (Root, Apps), (Apps, Root) },
                result.Steps.Select(step => (step.RouteBefore!, step.RouteAfter!)));
            Assert.Equal(7, source.Delivered.Count);
            Assert.All(result.Steps.Where(step => step.TargetDescriptor != Popup),
                step => Assert.True(step.Verified, step.FailureReason));
            Assert.Equal(7, result.Steps.Select(step => step.ReceiptId).Distinct().Count());
            var entry = Assert.Single(result.Steps, step => step.TargetDescriptor == Popup);
            Assert.Equal(routeChange, entry.Verified);
            Assert.Equal(routeChange ? null : "post-action-target-unique", entry.FailureReason);
            var cancel = result.Steps[2];
            Assert.Contains(cancel.Checks, check => check.Key == "post-action-overlay-dismissed" && check.Value);
            Assert.Single(result.Consults.Where(consult => consult.DirectiveKind == "arrival-detected"));
            Assert.Contains(Popup, result.Report.ReachedTargets ?? []);
            Assert.DoesNotContain("CANCEL", result.Report.ReachedTargets ?? []);
            Assert.Empty(result.Report.UncoveredItems);
            Assert.Equal(RunDriveStatus.Completed, result.Status);
            var events = handler.Bodies.Select(body => body.GetProperty("context"))
                .Where(context => context.TryGetProperty("heartbeat", out var h) && h.ValueKind == JsonValueKind.Object).ToArray();
            if (interrupt) Assert.NotEmpty(events);
            else Assert.Empty(events);
            var semanticSignals = events.Select(context =>
            {
                var heartbeat = context.GetProperty("heartbeat");
                var traversal = heartbeat.GetProperty("traversal");
                return (heartbeat.GetProperty("worldModel").GetProperty("currentRoute").GetString(),
                    traversal.GetProperty("currentTarget").GetString(), traversal.GetProperty("frontier").GetRawText());
            }).ToArray();
            Assert.Equal(semanticSignals.Length, semanticSignals.Distinct().Count());
            foreach (var context in events)
            {
                var heartbeat = context.GetProperty("heartbeat");
                var packet = context.GetProperty("casePacket");
                Assert.Equal(heartbeat.GetRawText(), packet.GetProperty("heartbeatSnapshot").GetRawText());
                Assert.Equal(heartbeat.GetProperty("eventDelta").GetRawText(), packet.GetProperty("eventDelta").GetRawText());
                Assert.True(heartbeat.GetProperty("kernel").GetProperty("nextEffectBlocked").GetBoolean());
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(TargetCoverageMarkKind.ReachedOverlay, Network, true)]
    [InlineData(TargetCoverageMarkKind.RouteEntered, Network, true)]
    [InlineData(TargetCoverageMarkKind.ReachedOverlay, Apps, false)]
    [InlineData(TargetCoverageMarkKind.RouteEntered, Apps, false)]
    [InlineData(TargetCoverageMarkKind.ReachedOverlay, null, false)]
    [InlineData(TargetCoverageMarkKind.RouteEntered, null, false)]
    public void AcceptedNestedArrivalRemovesPendingWithoutInventingVerifiedClicks(
        TargetCoverageMarkKind kind, string? route, bool reached)
    {
        var config = new SettingsCoverageConfig("1", new("nested arrival", "synthetic", false, false),
            new(4, 6, 2, 3, 0), new(true, "configured", 0, 0, false, 0), [],
            new(true, true, true, true), Root, Scroll, Back,
            DeepTraversal: new(true, 1, 1, ["Network > SIM name & color"]));
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, [("ui.element", "Network")]);
        ledger.RecordObservation(Network, [("ui.element", Popup), ("ui.element", Back)]);
        ledger.RecordPopupState("absent");
        Assert.Equal("nested-enter", ledger.Snapshot(config).NextDirectiveKind);
        Assert.True(ledger.TryMarkTargetReached(Popup, kind, "arrival", route));
        Assert.Equal(!reached, ledger.Snapshot(config).NextDirectiveKind == "nested-enter");
        Assert.Equal(reached, Assert.Single(ledger.Report(config).Items, item => item.Requirement == "nested-pages").Covered);
        Assert.Empty(ledger.EnteredRootDescriptors(config));
        Assert.Equal(0, ledger.Report(config).StepsVerified);
    }

    [Theory]
    [InlineData(Network, true)]
    [InlineData(Apps, false)]
    public void FallbackArrivalUsesRecordedEntryParent(string entryParent, bool reached)
    {
        var config = new SettingsCoverageConfig("1", new("nested fallback", "synthetic", false, false),
            new(4, 6, 2, 3, 0), new(true, "configured", 0, 0, false, 0), [],
            new(true, true, true, true), Root, Scroll, Back,
            DeepTraversal: new(true, 1, 1, ["Network > SIM name & color"]));
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, [("ui.element", "Network")]);
        ledger.RecordObservation(Network, [("ui.element", Popup), ("ui.element", Back)]);
        ledger.RecordPopupState("absent");
        // Replay the public Ledger's producer input, including typed directive attribution.
        ledger.RecordStep(new(0, "entry", "synthetic", "ui.element", Popup, "tap", null, true,
            "receipt-entry", "DeliveryCompleted", null, entryParent, "android.settings", "popup-capture",
            false, "post-action-target-unique", null)
            { SelectedDirective = new(CoverageDirectiveKind.NestedEnter, Popup, "synthetic nested entry") });
        Assert.True(ledger.TryMarkTargetReached(Popup, TargetCoverageMarkKind.ReachedOverlay, "arrival", "android.settings"));
        Assert.Equal(!reached, ledger.Snapshot(config).NextDirectiveKind == "nested-enter");
        Assert.Equal(reached, Assert.Single(ledger.Report(config).Items, item => item.Requirement == "nested-pages").Covered);
        Assert.Equal(0, ledger.Report(config).StepsVerified);
    }

    private sealed class ScriptedAgent(bool interrupt) : HttpMessageHandler
    {
        public List<JsonElement> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("/api/uniclaw-agent/consult", request.RequestUri!.AbsolutePath);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var body = document.RootElement.Clone(); Bodies.Add(body);
            var context = body.GetProperty("context");
            var id = context.GetProperty("decisionId").GetString()!;
            var objective = context.GetProperty("objective").GetString()!;
            var directive = objective.Split("COVERAGE DIRECTIVE (authoritative for this turn): ")[^1];
            var match = Regex.Match(directive, "<([^>]+)>");
            Assert.True(match.Success, objective);
            var descriptor = match.Groups[1].Value;
            Assert.Contains(descriptor, new[] { "Network", Popup, "CANCEL", Back, Scroll, "Apps" });
            var scroll = descriptor == Scroll;
            AgentDecision decision = interrupt && scroll
                ? new AgentDecision.Plan(id, new([new PlanItem.ObserveItem(null), new PlanItem.ObserveItem(null),
                    new PlanItem.ObserveItem(null), new PlanItem.ActItem("scrollable", Scroll, "swipe-up", null)],
                    "synthetic deliberate observation pause before discovery", "integration-pause-" + id))
                : new AgentDecision.Act(new(id, [new(scroll ? "scrollable" : "ui.element", descriptor, scroll ? "swipe-up" : "tap", null)], "synthetic finite traversal"));
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    requestId = body.GetProperty("requestId").GetString(), generation = 1,
                    task = new { initialization = new { }, payload = decision },
                }, Json), System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed record Delivery(string Descriptor, string Before, string After, bool PopupBefore, bool PopupAfter);
    private sealed record Row(string Text, string Rid, int Top, bool Clickable = true);
    private sealed class MenuSource(bool routeChange) : IEffectDriver
    {
        private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        private string _route = Root;
        private bool _popup;
        private bool _scrolled;
        private int _captures;
        private PageCaptureObservation? _capture;
        public string? CaptureId => _capture?.CaptureId;
        public List<Delivery> Delivered { get; } = [];
        public List<object> Observations { get; } = [];
        private Row[] Rows() => _popup ? [new("CANCEL", "android:id/button2", 700)]
            : _route == Root ? [new(_scrolled ? "Apps" : "Network", "integration:id/entry", 200)]
            : _route == Network ? [new(Popup, "integration:id/sim", 200), new(Back, "integration:id/back", 850)]
            : [new(Back, "integration:id/back", 850)];
        public DispatchResult Deliver(DispatchRequest request)
        {
            var before = _route; var popupBefore = _popup; string descriptor;
            if (request.EffectClass == "swipe-up")
            {
                Assert.Equal(Root, _route); Assert.False(_popup);
                descriptor = Scroll; _scrolled = true;
            }
            else
            {
                Assert.Equal("tap", request.EffectClass);
                var y = request.Target.Spatial!.CenterY * 1000;
                descriptor = Assert.Single(Rows().Where(row => y >= row.Top && y < row.Top + 80)).Text;
                switch (descriptor)
                {
                    case "Network": _route = Network; break;
                    case Popup: _popup = true; break;
                    case "CANCEL": _popup = false; break;
                    case Back: _route = Root; break;
                    case "Apps": _route = Apps; break;
                    default: throw new InvalidOperationException(descriptor);
                }
            }
            Delivered.Add(new(descriptor, before, _route, popupBefore, _popup));
            return new(DispatchOutcome.DeliveryCompleted, "synthetic spatial navigation only", _capture!.CaptureTime.AddMilliseconds(100));
        }
        public RunDriverInput? Next(ObservationDirective directive)
        {
            if (directive.Need is { } need)
                return new RunDriverInput.Observation([PageIdentityRecovery.Identity(new(need.Capture,
                    need.CaptureEvidenceId, null), _route == Root ? "Settings" : _route == Network ? "Network" : "Apps",
                    "integration-page:" + _route, directive.Context)!]);
            if (++_captures > 40) return new RunDriverInput.Cancel("synthetic integration capture bound", Start.AddMinutes(2));
            var title = _route == Root ? "Settings" : _route == Network ? "Network" : "Apps";
            var xml = "<hierarchy>" + Node(new(title, "integration:id/title", 0, false))
                + (_popup ? "<node resource-id='android:id/parentPanel' class='android.widget.LinearLayout' package='com.android.settings' bounds='[100,100][900,800]'>"
                    + Node(new(Popup, "android:id/alertTitle", 120, false)) + string.Concat(Rows().Select(Node)) + "</node>"
                    : string.Concat(Rows().Select(Node)))
                + (_route == Root ? "<node resource-id='integration:id/scroll' class='android.widget.ScrollView' package='com.android.settings' scrollable='true' enabled='true' bounds='[0,100][1000,950]'/>" : "")
                + "</hierarchy>";
            var id = "integration-" + _captures;
            _capture = PageIdentityRecovery.Capture(id, "cycle:" + id, "integration-session", Start.AddSeconds(_captures),
                1000, 1000, xml, null) with { DeviceId = "synthetic-integration", RequiresOverlayEvidence = true };
            var classified = SettingsPopupClassifier.ClassifyWithWindows(xml, Windows(_popup), PopupClearanceConfig.DefaultPopupResourceIds);
            Assert.Equal(_popup ? "present" : "absent", classified.State);
            var typed = UiAutomatorDump.ParseHierarchyObservation(xml, new(id, _capture.CaptureTime,
                "synthetic-integration", _capture.SessionCorrelation, 35, ObservationCycleId: _capture.ObservationCycleId,
                Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities, Space: _capture.Space));
            ObservationProposal Proposal(string subject, string value) => new(new(subject, value), IngressKind.Observation,
                directive.Context, new("synthetic-integration", _capture.CaptureTime, id + ":" + subject, ["synthetic", "offline"]));
            var proposals = new List<ObservationProposal>
            {
                Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(_capture)),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, _popup && routeChange ? "android.settings" : _route),
                Proposal(OverlayObservationCodec.Subject, OverlayObservationCodec.Encode(new(id, _capture.ObservationCycleId,
                    _capture.SessionCorrelation, _capture.CaptureTime, classified.State, "synthetic complete window acquisition")
                    { WindowEvidence = classified.WindowEvidence })),
            };
            proposals.AddRange(TypedHierarchyProposalProjector.Project(typed.Observation!, directive.Context, "synthetic-integration"));
            Observations.Add(new { id, _route, _popup, _scrolled, xml, classified, context = directive.Context.ToString() });
            return new RunDriverInput.Observation(proposals);
        }
        private static string Node(Row row) => $"<node text='{System.Security.SecurityElement.Escape(row.Text)}' resource-id='{row.Rid}' class='android.widget.Button' package='com.android.settings' clickable='{row.Clickable.ToString().ToLowerInvariant()}' enabled='true' bounds='[100,{row.Top}][900,{row.Top + 80}]'/>";
        private SettingsWindowEvidence Windows(bool popup)
        {
            var baseId = "base-" + (_route == Root ? "root" : _route == Network ? "network" : "apps");
            static string Block(int index, string id, string type, string bounds) => $"  Window #{index} Window{{{id}}}:\n    package=com.android.settings ty={type} mHasSurface=true mToken=ActivityRecord{{synthetic-settings}}\n    isVisible=true\n    isOnScreen=true\n    Frames: frame={bounds}\n\n";
            var windows = "WINDOW MANAGER WINDOWS\n" + Block(0, baseId, "BASE_APPLICATION", "[0,0][1000,1000]")
                + (popup ? Block(1, "overlay-sim", "APPLICATION", "[100,100][900,800]") : "")
                + "  Animation settings:\n  mSystemBooted=true mDisplayEnabled=true\n";
            var display = "WINDOW MANAGER DISPLAY CONTENTS\n  Display: mDisplayId=0\n  cur=1000x1000\n  mCurrentFocus=Window{" + (popup ? "overlay-sim" : baseId) + "}\n";
            return new(windows, display, windows, display, 35);
        }
    }

    private static void Export(bool interrupt, bool routeChange, MenuSource source, ScriptedAgent handler, SettingsCoverageRunner.RunResult result)
    {
        var output = Environment.GetEnvironmentVariable("SIM029_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(output)) return;
        var dir = Path.Combine(output, (interrupt ? "interrupted" : "normal") + (routeChange ? "-route-change" : "")); Directory.CreateDirectory(dir);
        foreach (var file in Directory.GetFiles(result.RunDir)) File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), true);
        File.WriteAllText(Path.Combine(dir, "wire.json"), JsonSerializer.Serialize(handler.Bodies, Json));
        File.WriteAllText(Path.Combine(dir, "synthetic-delivery.json"), JsonSerializer.Serialize(source.Delivered, Json));
        File.WriteAllText(Path.Combine(dir, "synthetic-source.json"), JsonSerializer.Serialize(source.Observations, Json));
        File.WriteAllText(Path.Combine(dir, "result.json"), JsonSerializer.Serialize(result, Json));
    }
}
