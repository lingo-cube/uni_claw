using System.Text.Json;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

// External XML/window/Agent/delivery are synthetic; all Product Runtime owners are real.
public sealed partial class FiniteMenuGraphSimulationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string Scroll = "finite:id/scroll";
    private const string Back = "Navigate up";
    private const string RootRoute = "android.settings|rk1:Settings|src=homepage_title|up=0";
    private sealed record Target(string From, string Descriptor, string? To, int Viewport, string Kind);
    private sealed record ReturnEdge(string From, string To);
    private sealed record MenuGraph(string Scope, Target[] Targets, ReturnEdge[] Returns);
    private sealed record TargetResult(string From, string Descriptor, string? ExpectedTo, string Status);
    private sealed record ReturnResult(string From, string ExpectedTo, string Status);
    private sealed record ObservedStep(string? TargetDescriptor, string EffectClass, string? RouteBefore,
        string? RouteAfter, string? ReceiptId, bool Verified);
    private sealed record GraphAudit(TargetResult[] Targets, ReturnResult[] Returns, string[] WrongReturns,
        string ProductCoverageStatus, bool AllTraversableReached, bool AllTargetsAccounted);

    [Theory]
    [InlineData("complete", 5, 1, 1, 0, 0, 5, 12)]
    [InlineData("missing-root-scroll", 3, 1, 1, 2, 0, 3, 7)]
    [InlineData("missing-child-scroll", 4, 1, 1, 1, 0, 4, 9)]
    [InlineData("wrong-return", 2, 1, 0, 3, 1, 0, 3)]
    public void KnownGraphDenominatorSurvivesDiscoveryGapsAndWrongReturns(string mode,
        int reached, int skipped, int blocked, int undiscovered, int pending, int returns, int deliveries)
    {
        var graph = JsonSerializer.Deserialize<MenuGraph>(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "tests/UniClaw.Host.Tests/Fixtures/FiniteMenuGraph/menu.json")), Json)!;
        var sourceGraph = JsonSerializer.Deserialize<MenuGraph>(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "tests/UniClaw.Host.Tests/Fixtures/FiniteMenuGraph/world.json")), Json)!;
        var world = new MenuWorld(sourceGraph, mode);
        var original = SettingsCoverageConfig.Load(Path.Combine(RepositoryRoot(),
            SettingsCoverageConfig.DefaultConfigRelativePath));
        var config = original with
        {
            Bounds = new(16, 8, 2, 2, 1),
            Coverage = new(true, "configured", 1, 2, true, 0),
            RootRoute = RootRoute, ScrollContainerDescriptor = Scroll, BackDescriptor = Back,
            TargetPages = ["Network", "Apps"],
            DynamicRootEntries = new(true, "all-visible"),
            DynamicNestedEntries = new(true, 2, 4),
            NavigationScope = null, LanguageInspection = null,
            ObservationRecovery = new(15000, 16),
        };
        var consulted = 0;
        AgentDecision Consult(AgentDecisionContext context)
        {
            if (++consulted > 1)
                return new AgentDecision.Blocked(context.DecisionId, "finite scripted plan stopped after current-step rejection");
            return new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(Plan(mode),
                "synthetic finite navigation strategy; controls are observed only", "finite-menu", MaxLocalOperations: 32));
        }
        var temp = Path.Combine(Path.GetTempPath(), "sim026-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = SettingsCoverageRunner.Run(temp, new("synthetic-finite-device",
                new("synthetic-finite-device", "unused", "/unused", "/unused/python"), config, Consult,
                ViewportWidth: 1000, ViewportHeight: 1000, FeedNext: world.Next,
                CurrentCaptureId: () => world.CaptureId, EffectDriver: world, UseCoverageDirector: false));
            var audit = Audit(graph, result);
            Export(mode, world, result, audit);
            Assert.Equal(7, audit.Targets.Length); // independent fixed denominator, including unseen pages
            Assert.Equal(5, audit.Returns.Length);
            Assert.Equal(reached, audit.Targets.Count(item => item.Status == "reached"));
            Assert.Equal(skipped, audit.Targets.Count(item => item.Status == "structural-skipped"));
            Assert.Equal(blocked, audit.Targets.Count(item => item.Status == "blocked"));
            Assert.Equal(undiscovered, audit.Targets.Count(item => item.Status == "undiscovered"));
            Assert.Equal(pending, audit.Targets.Count(item => item.Status == "observed-pending"));
            var expectedUnseen = mode switch
            {
                "missing-root-scroll" => new[] { "Apps", "Permissions" },
                "missing-child-scroll" => new[] { "Advanced" },
                "wrong-return" => new[] { "Advanced", "Apps", "Permissions" },
                _ => Array.Empty<string>(),
            };
            Assert.Equal(expectedUnseen.OrderBy(item => item, StringComparer.Ordinal),
                audit.Targets.Where(item => item.Status == "undiscovered")
                    .Select(item => item.Descriptor).OrderBy(item => item, StringComparer.Ordinal));
            if (mode == "wrong-return")
                Assert.Equal("Factory reset", Assert.Single(audit.Targets.Where(item => item.Status == "observed-pending")).Descriptor);
            Assert.Equal(returns, audit.Returns.Count(item => item.Status == "verified-destination"));
            Assert.Equal(deliveries, world.Delivered.Count);
            Assert.Equal(deliveries, world.DispatchAttempts);
            Assert.Equal(deliveries, result.Steps.Count);
            Assert.Equal(deliveries, result.Steps.Select(step => step.ReceiptId).Distinct().Count());
            Assert.DoesNotContain(world.Delivered, item => item.Descriptor is "Airplane mode" or "Factory reset");
            Assert.Equal(mode == "complete", audit.AllTraversableReached);
            Assert.Equal(mode == "complete", audit.AllTargetsAccounted);
            if (mode == "wrong-return")
            {
                Assert.Single(audit.WrongReturns);
                Assert.Contains(result.Steps, step => step.TargetDescriptor == Back
                    && step.RouteBefore == Route("Details") && step.RouteAfter == Route("Detour"));
            }
            else Assert.Empty(audit.WrongReturns);
            if (mode == "complete")
            {
                Assert.All(result.Steps, step => Assert.True(step.Verified));
                Assert.Contains(result.Steps, step => step.EffectClass == "swipe-up" && step.RouteBefore == Route("Network"));
                Assert.Contains(result.Steps, step => step.EffectClass == "swipe-up" && step.RouteBefore == RootRoute);
            }
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("deep-wrong-return")]
    [InlineData("missing-deep-scroll")]
    [InlineData("supplement-budget-exhausted")]
    public void ThreeLevelGraphKeepsEachParentReturnAndSameNamedTargetDistinct(string mode)
    {
        var fixture = Path.Combine(RepositoryRoot(), "tests/UniClaw.Host.Tests/Fixtures/ThreeLevelMenuGraph");
        var graph = JsonSerializer.Deserialize<MenuGraph>(File.ReadAllText(Path.Combine(fixture, "menu.json")), Json)!;
        var source = JsonSerializer.Deserialize<MenuGraph>(File.ReadAllText(Path.Combine(fixture, "world.json")), Json)!;
        using var expectedDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "oracle.json")));
        var expected = expectedDocument.RootElement.GetProperty("cases").GetProperty(mode);
        var world = new MenuWorld(source, mode, supplyIdentityInCapture: mode != "supplement-budget-exhausted");
        var original = SettingsCoverageConfig.Load(Path.Combine(RepositoryRoot(), SettingsCoverageConfig.DefaultConfigRelativePath));
        var config = original with
        {
            Bounds = new(24, 8, 4, 2, 1), Coverage = new(true, "configured", 1, 2, true, 0),
            RootRoute = RootRoute, ScrollContainerDescriptor = Scroll, BackDescriptor = Back,
            TargetPages = ["Network", "Apps"], DynamicRootEntries = new(true, "all-visible"),
            DynamicNestedEntries = new(true, 3, 4), NavigationScope = null, LanguageInspection = null,
            ObservationRecovery = new(15000, 16),
        };
        var consulted = 0;
        AgentDecision Consult(AgentDecisionContext context) => ++consulted == 1
            ? new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(ThreeLevelPlan(),
                "synthetic three-level navigation script; not autonomous discovery", "three-level-menu", MaxLocalOperations: 48))
            : new AgentDecision.Blocked(context.DecisionId, "finite script stopped after a rejected step");
        var temp = Path.Combine(Path.GetTempPath(), "sim031-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = SettingsCoverageRunner.Run(temp, new("synthetic-finite-device",
                new("synthetic-finite-device", "unused", "/unused", "/unused/python"), config, Consult,
                ViewportWidth: 1000, ViewportHeight: 1000, FeedNext: world.Next,
                CurrentCaptureId: () => world.CaptureId, EffectDriver: world, UseCoverageDirector: false));
            var audit = Audit(graph, result);
            Export("three-level-" + mode, world, result, audit);
            foreach (var (status, key) in new[] { ("reached", "reached"), ("structural-skipped", "skipped"),
                         ("blocked", "blocked"), ("undiscovered", "undiscovered"), ("observed-pending", "pending") })
                Assert.Equal(expected.GetProperty(key).GetInt32(), audit.Targets.Count(item => item.Status == status));
            foreach (var (status, key) in new[] { ("observed-pending", "observedPendingTargets"), ("undiscovered", "undiscoveredTargets") })
                if (expected.TryGetProperty(key, out var targetList))
                    Assert.Equal(targetList.EnumerateArray().Select(row => row[0].GetString() + " > " + row[1].GetString()).Order(),
                        audit.Targets.Where(item => item.Status == status).Select(item => item.From + " > " + item.Descriptor).Order());
            Assert.Equal(expected.GetProperty("returns").GetInt32(), audit.Returns.Count(item => item.Status == "verified-destination"));
            Assert.Equal(expected.GetProperty("wrongReturns").GetInt32(), audit.WrongReturns.Length);
            Assert.Equal(expected.GetProperty("allTraversableReached").GetBoolean(), audit.AllTraversableReached);
            Assert.Equal(expected.GetProperty("allTargetsAccounted").GetBoolean(), audit.AllTargetsAccounted);
            var sequence = expected.GetProperty("sequence").EnumerateArray()
                .Select(row => row.EnumerateArray().Select(value => value.GetString()!).ToArray()).ToArray();
            Assert.Equal(sequence.Select(row => (Route(row[0]), row[1], Route(row[2]))),
                result.Steps.Select(step => (step.RouteBefore, step.TargetDescriptor, step.RouteAfter)));
            var deliveries = expected.GetProperty("deliveries").GetInt32();
            Assert.Equal(deliveries, world.DispatchAttempts);
            Assert.Equal(deliveries, world.Delivered.Count);
            Assert.Equal(deliveries, result.Steps.Count);
            Assert.Equal(deliveries, result.Steps.Select(step => step.ReceiptId).Distinct().Count());
            Assert.DoesNotContain(world.Delivered, item => item.Descriptor is "Factory reset" or "Airplane mode");
            Assert.InRange(consulted, 1, 2);
            if (mode != "complete")
            {
                Assert.NotEqual(RunDriveStatus.Completed, result.Status);
                Assert.NotEqual("CoverageComplete", result.Report.Status);
            }
            if (mode == "complete")
            {
                Assert.All(result.Steps, step => Assert.True(step.Verified, step.FailureReason));
                Assert.Equal(3, result.Steps.Count(step => step.EffectClass == "swipe-up"));
                Assert.Equal(RootRoute, result.Steps[^1].RouteAfter);
                Assert.Equal(new[] { Route("DetailsSummary"), Route("PermissionSummary") },
                    result.Steps.Where(step => step.TargetDescriptor == "Summary").Select(step => step.RouteAfter));
            }
            else if (mode == "supplement-budget-exhausted")
            {
                Assert.Equal(RunDriveStatus.VerificationFailed, result.Status);
                Assert.Equal("page-identity-observation-exhausted", result.Reason);
                Assert.False(result.Steps[^1].Verified);
                Assert.All(result.Steps.Take(15), step => Assert.True(step.Verified));
            }
            else Assert.DoesNotContain(result.Steps, step => step.TargetDescriptor == "Summary");
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    private static PlanItem[] ThreeLevelPlan()
    {
        PlanItem Tap(string descriptor) => new PlanItem.ActItem("ui.element", descriptor, "tap", null);
        PlanItem Swipe() => new PlanItem.ActItem("scrollable", Scroll, "swipe-up", null);
        PlanItem Return(int count) => new PlanItem.RepeatItem([Tap(Back)], count);
        return [Tap("Network"), Tap("Details"), Swipe(), Tap("Diagnostics"), Tap(Back),
            Tap("Summary"), Return(2), Swipe(), Tap("Advanced"), Return(2),
            Swipe(), Tap("Apps"), Tap("Permissions"), Tap("Summary"), Return(3),
            Tap("Factory reset")];
    }

    private static PlanItem[] Plan(string mode)
    {
        PlanItem Tap(string descriptor) => new PlanItem.ActItem("ui.element", descriptor, "tap", null);
        PlanItem Swipe() => new PlanItem.ActItem("scrollable", Scroll, "swipe-up", null);
        var items = new List<PlanItem> { Tap("Network"), Tap("Details"), Tap(Back) };
        if (mode != "missing-child-scroll") items.AddRange([Swipe(), Tap("Advanced"), Tap(Back)]);
        items.Add(Tap(Back));
        if (mode != "missing-root-scroll") items.AddRange([Swipe(), Tap("Apps"), Tap("Permissions"), Tap(Back), Tap(Back)]);
        // A real current-step policy guard must reject this observed forbidden target before dispatch.
        items.Add(Tap("Factory reset"));
        return items.ToArray();
    }

    private static GraphAudit Audit(MenuGraph graph, SettingsCoverageRunner.RunResult result)
    {
        using var observed = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "coverage-observed-elements.json")));
        using var guards = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "current-step-guards.json")));
        var steps = JsonSerializer.Deserialize<ObservedStep[]>(File.ReadAllText(
            Path.Combine(result.RunDir, "coverage-steps.json")), Json)!;
        Assert.Equal(result.Steps.Select(step => (step.TargetDescriptor, step.EffectClass, step.RouteBefore,
                step.RouteAfter, step.ReceiptId, step.Verified)),
            steps.Select(step => (step.TargetDescriptor, step.EffectClass, step.RouteBefore,
                step.RouteAfter, step.ReceiptId, step.Verified)));
        var rows = observed.RootElement.EnumerateArray().ToArray();
        var constraints = guards.RootElement.EnumerateArray().ToArray();
        bool Seen(Target target) => rows.Any(row => row.GetProperty("route").GetString() == Route(target.From)
            && row.GetProperty("descriptor").GetString() == target.Descriptor);
        bool GuardBlocked(Target target) => constraints.Any(row =>
            row.GetProperty("step").GetProperty("targetDescriptor").GetString() == target.Descriptor
            && row.GetProperty("constraint").GetProperty("reason").GetString() == "target matches forbidden pattern: Factory reset"
            && row.GetProperty("constraint").GetProperty("evidenceRefs").GetArrayLength() > 0);
        var targets = graph.Targets.Select(target => new TargetResult(target.From, target.Descriptor, target.To,
            target.Kind == "navigate" && steps.Any(step => step.Verified
                && step.TargetDescriptor == target.Descriptor && step.RouteBefore == Route(target.From)
                && step.RouteAfter == Route(target.To!)) ? "reached"
            : target.Kind == "readOnly" && Seen(target)
                && (result.Report.StructuralSkippedTargets ?? []).Contains(target.Descriptor) ? "structural-skipped"
            : Seen(target) && GuardBlocked(target) ? "blocked"
            : Seen(target) ? "observed-pending" : "undiscovered")).ToArray();
        var returns = graph.Returns.Select(edge => new ReturnResult(edge.From, edge.To,
            steps.Any(step => step.Verified && step.TargetDescriptor == Back
                && step.RouteBefore == Route(edge.From) && step.RouteAfter == Route(edge.To))
                ? "verified-destination" : "unproven")).ToArray();
        var wrong = steps.Where(step => step.TargetDescriptor == Back && graph.Returns.Any(edge =>
            step.RouteBefore == Route(edge.From) && step.RouteAfter != Route(edge.To)))
            .Select(step => $"{step.RouteBefore} -> {step.RouteAfter}").ToArray();
        return new(targets, returns, wrong, result.Report.Status,
            targets.Count(target => target.Status == "reached") == graph.Targets.Count(target => target.Kind == "navigate")
                && returns.All(edge => edge.Status == "verified-destination") && wrong.Length == 0,
            targets.All(target => target.Status is "reached" or "structural-skipped" or "blocked") && wrong.Length == 0);
    }

    private sealed record Delivered(string From, string Descriptor, string To, string Effect);
    private sealed record Row(string Descriptor, string Kind, string? To, int Top);
    private sealed class MenuWorld(MenuGraph graph, string mode, bool supplyIdentityInCapture = false) : IEffectDriver
    {
        private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        private readonly Dictionary<string, int> _offsets = [];
        private PageCaptureObservation? _capture;
        private int _captures;
        private string _page = "Settings";
        public string? CaptureId => _capture?.CaptureId;
        public List<Delivered> Delivered { get; } = [];
        public int DispatchAttempts { get; private set; }
        public List<object> Observations { get; } = [];
        private Row[] Rows() => graph.Targets.Where(target => target.From == _page
                && (target.Viewport == _offsets.GetValueOrDefault(_page) || target.Viewport == -1))
            .Select((target, index) => new Row(target.Descriptor, target.Kind, target.To, 100 + index * 100))
            .Concat(_page == "Settings" ? [] : [new Row(Back, "back", null, 800)]).ToArray();
        public DispatchResult Deliver(DispatchRequest request)
        {
            DispatchAttempts++; // include a rejected/throwing delivery attempt in the safety oracle
            var from = _page;
            string descriptor;
            if (request.EffectClass == "swipe-up")
            {
                descriptor = Scroll;
                Assert.Contains(graph.Targets, target => target.From == _page && target.Viewport > 0);
                _offsets[_page] = mode == "missing-deep-scroll" && _page == "Details" ? 0 : 1;
            }
            else
            {
                var locator = request.Target.Spatial!;
                var y = locator.CenterY * 1000;
                var hit = Assert.Single(Rows().Where(row => y >= row.Top && y < row.Top + 80));
                Assert.DoesNotContain(hit.Kind, new[] { "blocked", "readOnly" });
                descriptor = hit.Descriptor;
                _page = hit.Kind == "back" ? graph.Returns.Single(edge => edge.From == from).To : hit.To!;
                if (mode == "wrong-return" && from == "Details" && hit.Kind == "back") _page = "Detour";
                if (mode == "deep-wrong-return" && from == "Diagnostics" && hit.Kind == "back") _page = "Detour";
            }
            Delivered.Add(new(from, descriptor, _page, request.EffectClass));
            return new(DispatchOutcome.DeliveryCompleted, "synthetic finite-menu spatial delivery", _capture!.CaptureTime.AddMilliseconds(100));
        }
        public RunDriverInput? Next(ObservationDirective directive)
        {
            if (directive.Need is { } need)
            {
                var identity = PageIdentityRecovery.Identity(new(need.Capture, need.CaptureEvidenceId, null),
                    _page, "finite-page:" + _page, directive.Context)!;
                return new RunDriverInput.Observation([identity]);
            }
            if (++_captures > 32) return new RunDriverInput.Cancel("finite-menu-capture-bound", Start.AddMinutes(2));
            var xml = "<hierarchy>" + Node(_page, "finite:id/title", "android.widget.TextView", 0, false, true)
                + string.Concat(Rows().Select(row => Node(row.Descriptor, "finite:id/" + row.Descriptor.Replace(" ", "_"),
                    row.Kind == "readOnly" ? "android.widget.Switch" : "android.widget.Button", row.Top, true, true)))
                + (graph.Targets.Any(target => target.From == _page && target.Viewport > 0) ? "<node resource-id='finite:id/scroll' class='android.widget.ScrollView' package='com.android.settings' scrollable='true' enabled='true' bounds='[0,60][1000,950]'/>" : "") + "</hierarchy>";
            var id = "finite-" + _captures;
            _capture = PageIdentityRecovery.Capture(id, "cycle:" + id, "finite-session", Start.AddSeconds(_captures),
                1000, 1000, xml, null) with { DeviceId = "synthetic-finite-device" };
            var typed = UiAutomatorDump.ParseHierarchyObservation(xml, new(id, _capture.CaptureTime,
                "synthetic-finite-device", "finite-session", 35, ObservationCycleId: _capture.ObservationCycleId,
                Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities, Space: _capture.Space));
            var proposals = new List<ObservationProposal>
            {
                Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(_capture), directive.Context),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, Route(_page), directive.Context),
            };
            if (supplyIdentityInCapture)
            {
                // Fixture input encoding uses the public admission seam to obtain the
                // content-addressed source reference. The Runner admits it independently.
                var inputEncodingLedger = new EvidenceLedger();
                var captureEvidenceId = inputEncodingLedger.Admit(proposals[0]).Record!.EvidenceId;
                proposals.Add(PageIdentityRecovery.Identity(new(_capture, captureEvidenceId, null),
                    _page, "finite-page:" + _page, directive.Context)!);
            }
            proposals.AddRange(TypedHierarchyProposalProjector.Project(typed.Observation!, directive.Context, "synthetic-finite-menu"));
            Observations.Add(new { id, page = _page, viewport = _offsets.GetValueOrDefault(_page), xml, synthetic = true });
            return new RunDriverInput.Observation(proposals);
        }
        private ObservationProposal Proposal(string subject, string value, ObservationContext context) => new(new(subject, value),
            IngressKind.Observation, context, new("synthetic-finite-menu", _capture!.CaptureTime,
                _capture.CaptureId + ":" + subject, ["synthetic", "offline", "finite-menu"]));
        private static string Node(string text, string rid, string type, int top, bool clickable, bool enabled) =>
            $"<node text='{text}' resource-id='{rid}' class='{type}' package='com.android.settings' clickable='{clickable.ToString().ToLowerInvariant()}' enabled='{enabled.ToString().ToLowerInvariant()}' checkable='{(type.EndsWith("Switch") ? "true" : "false")}' bounds='[0,{top}][1000,{top + 80}]'/>";
    }

    private static string Route(string page) => page == "Settings" ? RootRoute : "android.settings|route:" + page;
    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "UniClaw.Kernel.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("UniClaw repository root not found");
    }
    private static void Export(string mode, MenuWorld world, SettingsCoverageRunner.RunResult result, GraphAudit audit)
    {
        var output = Environment.GetEnvironmentVariable("SIM026_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        var dir = Path.Combine(output, mode); Directory.CreateDirectory(dir);
        foreach (var file in Directory.GetFiles(result.RunDir)) File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), true);
        File.WriteAllText(Path.Combine(dir, "independent-graph-audit.json"), JsonSerializer.Serialize(audit, Json));
        File.WriteAllText(Path.Combine(dir, "synthetic-source.json"), JsonSerializer.Serialize(world.Observations, Json));
        File.WriteAllText(Path.Combine(dir, "synthetic-delivery.json"), JsonSerializer.Serialize(world.Delivered, Json));
    }
}
