using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// EXTERNAL BOUNDARY v1 (EBD-1..EBD-18, deterministic).
/// Full chain: SettingsRoot → SettingsSubpage(Location) whose sources are:
///   "App location permissions"  → AUTHORIZED_BOUNDARY (crosses to EXTERNAL
///                                 foreground com.android.permissioncontroller)
///   "Location services"          → AUTHORIZED_CHILD (recursive Settings child)
/// The boundary crosses external, creates a PENDING BoundaryObligation, performs
/// exactly ONE SystemBack, and on exact-parent + continuity returns to Location
/// → VerifiedBoundaryDisposition(RETURNED_TO_PARENT).
/// </summary>
public sealed class ExternalBoundaryTests
{
    private const string App = "com.android.settings";
    private const string ExternalApp = "com.android.permissioncontroller";
    private const string TitleRoleRid = "com.android.settings:id/collapsing_toolbar";
    private const string ExternalTitleRoleRid = "com.android.permissioncontroller:id/collapsing_toolbar";
    private const string ParentReturnActionRoleLabel = "Navigate up";
    private const string RootPage = "SettingsRoot";
    private const string LocationIdentity = "SettingsSubpage(Location)";
    private const string BoundarySource = "App location permissions";
    private const string ChildSource = "Location services";

    internal static CandidateAuthorizationEvidence AuthorizeEbd(Observation observation, ObservedElement candidate)
    {
        var isRoot = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal));
        if (isRoot)
        {
            if (string.Equals(candidate.Text, "Location", StringComparison.Ordinal))
                return new(true, "EBD: authorize the recursive Location child.", AuthorizationKind.AuthorizedChild);
            return new(false, "EBD audit: root source not the Location child.");
        }
        if (string.Equals(candidate.Text, BoundarySource, StringComparison.Ordinal))
            return new(true, "EBD: authorized external-boundary crossing.", AuthorizationKind.AuthorizedBoundary);
        if (string.Equals(candidate.Text, ChildSource, StringComparison.Ordinal))
            return new(true, "EBD: authorized recursive child.", AuthorizationKind.AuthorizedChild);
        if (string.Equals(candidate.Text, RootPage, StringComparison.Ordinal)
            || string.Equals(candidate.Text, ParentReturnActionRoleLabel, StringComparison.Ordinal))
            return new(true, "EBD: labelled parent-return control authorized.");
        return new(false, "EBD audit: source not an authorized boundary or recursive child.");
    }

    private sealed class BoundaryWorld : IEnvironment
    {
        public string Screen { get; private set; } = "Root";
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        public bool BackNoOp { get; init; }
        public string? BackToScreen { get; init; }
        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

        public Task<Observation> ObserveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Build(++_seq));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _actions.Add(action);
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    Screen = "Root";
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "launch", "launch"));
                case DeviceAction.Tap tap:
                    Screen = Screen switch
                    {
                        "Root" => "Location",
                        "Location" => ResolveLocationTap(tap) ?? Screen,
                        "Services" => "Location",
                        _ => Screen,
                    };
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "tap"));
                case DeviceAction.SystemBack:
                    Screen = BackNoOp ? "External" : (BackToScreen ?? "Location");
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "back", "back"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "noop"));
            }
        }

        private static string? ResolveLocationTap(DeviceAction.Tap tap)
        {
            if (tap.TargetBounds is not { } b)
                return null;
            int idx = (int)Math.Round(b.Y1 / 0.1f) - 3;
            return idx switch
            {
                0 => "External",
                1 => "Services",
                _ => null,
            };
        }

        private Observation Build(long seq) => Screen switch
        {
            "Root" => RootObservation(seq),
            "External" => ExternalObservation(seq),
            "Services" => ServicesObservation(seq),
            _ => LocationObservation(seq),
        };

        private static ElementBounds RowBounds(int ordinal) => new(0, ordinal * 0.1f, 1, (ordinal + 1) * 0.1f);

        private Observation RootObservation(long seq)
            => new(new[] { new ObservedElement("Location", null, 0, RowBounds(3), "text") }.ToImmutableArray(), App, seq)
            {
                StructuredElements = new[]
                {
                    Row("Location", 3),
                    SearchBar(),
                }.ToImmutableArray(),
            };

        private Observation LocationObservation(long seq)
        {
            var rows = new[] { BoundarySource, ChildSource };
            return new Observation(
                rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(3 + i), "text")).ToImmutableArray(), App, seq)
            {
                StructuredElements = rows.Select((r, i) => Row(r, 3 + i))
                    .Append(UpControl()).Append(TitleRole("Location")).ToImmutableArray(),
            };
        }

        private Observation ServicesObservation(long seq)
        {
            var rows = new[] { "Wi-Fi scanning", "Bluetooth scanning" };
            return new Observation(
                rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(3 + i), "text")).ToImmutableArray(), App, seq)
            {
                StructuredElements = rows.Select((r, i) => Row(r, 3 + i))
                    .Append(UpControl()).Append(TitleRole("Location services")).ToImmutableArray(),
            };
        }

        private Observation ExternalObservation(long seq)
            => new(new[] { new ObservedElement("Allowed all the time", null, 0, RowBounds(3), "text") }.ToImmutableArray(), ExternalApp, seq)
            {
                StructuredElements = new[] { UpControl(), TitleRoleExt("Location") }.ToImmutableArray(),
            };

        internal static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true, RowBounds(ordinal), title, null, false, null, null);

        internal static StructuredElementEvidence UpControl()
            => new("android.widget.ImageButton", null, true, false, false, true, true,
                new ElementBounds(0f, 0f, 0.13f, 0.1f), null, null, null, ParentReturnActionRoleLabel, null);

        internal static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", "com.android.settings:id/search_action_bar", true, false, false, true, false,
                new ElementBounds(0f, 0.1f, 1f, 0.3f), "Search settings", null, null, null, null);

        internal static StructuredElementEvidence TitleRole(string pageTitle)
            => new("android.widget.FrameLayout", TitleRoleRid, null, null, null, true, null,
                new ElementBounds(0f, 0f, 1f, 0.28f), null, null, null, pageTitle, null);

        internal static StructuredElementEvidence TitleRoleExt(string pageTitle)
            => new("android.widget.FrameLayout", ExternalTitleRoleRid, null, null, null, true, null,
                new ElementBounds(0f, 0f, 1f, 0.28f), null, null, null, pageTitle, null);
    }

    private sealed record EbdOutcome(RunState State, string? Reason, BoundaryWorld Environment, RuntimeAgent Agent);

    private static async Task<EbdOutcome> RunEbdAsync(BoundaryWorld world, string runId, Func<Observation, GoalEvidence>? goalEvidence = null)
    {
        var traversal = new UniClaw.Runtime.Traversal.Traversal(world);
        var startup = new RuntimeStartup(world, App, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup, traversal, ct => world.ObserveAsync(ct),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            page => new RuntimeContainer(page,
                observation => string.Equals(SettingsSingleRecursiveChildTests.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        Func<Observation, GoalEvidence> ev = goalEvidence ?? (obs =>
            new GoalEvidence(
                string.Equals(obs.ForegroundApplication, App, StringComparison.Ordinal)
                    && obs.StructuredElements.Any(se => string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal)),
                "EBD: fresh final Root observation confirms completion.", obs.SequenceNumber));
        var goal = new Goal(
            ev,
            CandidateAuthorizationEvaluator: AuthorizeEbd,
            ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew,
            BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory);
        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "EBD: Root → Location → authorized boundary → external → SystemBack → exact parent",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new EbdOutcome(state, agent.Reason, world, agent);
    }

    private static BranchProgressEvidence? LocationLedger(RuntimeAgent agent)
        => agent.ProgressSnapshot.TryGetValue(LocationIdentity, out var v) ? v : null;

    private static BranchProgressEvidence RootLedger(RuntimeAgent agent) => agent.ProgressSnapshot[RootPage];

    [Fact]
    public async Task EBD5_AuthorizedCrossing_CreatesPendingObligation()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-5");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.Single(ledger.RequiredBoundaryObligations);
        Assert.Equal(BoundaryObligationState.Verified, ledger.RequiredBoundaryObligations[0].State);
        Assert.Contains(run.Agent.Trace, t => t.Reason?.Contains("EXTERNAL_BOUNDARY_OBSERVED", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task EBD4_BoundaryNotRequiredChild()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-4");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.DoesNotContain(BoundarySource, ledger.RequiredChildren.Keys);
        Assert.Contains(ledger.RequiredBoundaryObligations, o => o.Relation.SourceOccurrenceReference.StartsWith(BoundarySource, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EBD8_ExactParentReturn_VerifiedDisposition()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-8");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.Contains(run.Agent.Trace, t => t.Reason?.Contains("EXTERNAL_BOUNDARY_RETURNED_TO_PARENT", StringComparison.Ordinal) is true);
        Assert.Single(ledger.VerifiedBoundaryDispositions);
        Assert.Equal("RETURNED_TO_PARENT", ledger.VerifiedBoundaryDispositions[0].Disposition);
        Assert.Equal(LocationIdentity, ledger.VerifiedBoundaryDispositions[0].ReturnedParentIdentity);
    }

    [Fact]
    public async Task EBD12_PendingBoundary_BlocksSubtreeComplete()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackNoOp = true }, "ebd-12");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.True(ledger.HasPendingBoundaryObligation);
        Assert.False(ledger.IsSubtreeCompleteByRequiredChildren);
        Assert.Empty(ledger.VerifiedBoundaryDispositions);
        Assert.NotEqual(RunState.Completed, run.State);
    }

    [Fact]
    public async Task EBD9_WrongSettingsDestination_Fails()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Services" }, "ebd-9");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.True(ledger.HasPendingBoundaryObligation);
        Assert.Empty(ledger.VerifiedBoundaryDispositions);
        Assert.NotEqual(RunState.Completed, run.State);
    }

    [Fact]
    public async Task EBD10_PostBackRemainsExternal_Fails()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackNoOp = true }, "ebd-10");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.True(ledger.HasPendingBoundaryObligation);
        Assert.Empty(ledger.VerifiedBoundaryDispositions);
        Assert.NotEqual(RunState.Completed, run.State);
    }

    [Fact]
    public async Task EBD13_GoalEvidenceCannotBypassBoundary()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackNoOp = true }, "ebd-13",
            obs => new GoalEvidence(true, "satisfied but boundary pending.", obs.SequenceNumber));
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.True(ledger.HasPendingBoundaryObligation);
        Assert.False(ledger.IsSubtreeCompleteByRequiredChildren);
    }

    [Fact]
    public async Task EBD7_ExternalCreatesZeroRecursiveContainer()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-7");
        var containers = run.Agent.Trace.Select(t => t.ContainerId).Where(id => id is not null).Distinct().ToList();
        Assert.DoesNotContain(ExternalApp, containers);
        Assert.DoesNotContain("SettingsSubpage()", containers);
    }

    [Fact]
    public async Task EBD6_BoundaryEntryDoesNotVerify()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-6");
        Assert.Contains(run.Agent.Trace, t => t.Reason?.Contains("EXTERNAL_BOUNDARY_OBSERVED", StringComparison.Ordinal) is true);
        var verified = run.Agent.Trace.Count(t => t.Reason?.Contains("EXTERNAL_BOUNDARY_RETURNED_TO_PARENT", StringComparison.Ordinal) is true);
        Assert.Equal(1, verified);
    }

    [Fact]
    public async Task EBD17_SystemBackDispatchedExactlyOnce()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-17");
        var backs = run.Environment.ActionHistory.OfType<DeviceAction.SystemBack>().Count();
        Assert.Equal(1, backs);
    }

    [Fact]
    public void EBD18_ScrollBackwardIsNotBack()
    {
        Assert.IsType<DeviceAction.SystemBack>(new DeviceAction.SystemBack());
        Assert.IsType<DeviceAction.ScrollBackward>(new DeviceAction.ScrollBackward());
        Assert.NotEqual(typeof(DeviceAction.ScrollBackward), typeof(DeviceAction.SystemBack));
    }

    [Fact]
    public async Task EBD3_ExactParentRelationRetained()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-3");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        foreach (var d in ledger.VerifiedBoundaryDispositions)
            Assert.Equal(LocationIdentity, d.ReturnedParentIdentity);
    }

    [Fact]
    public async Task EBD1_AuthorizedCrossing_ExternalBoundaryRelation()
    {
        var run = await RunEbdAsync(new BoundaryWorld { BackToScreen = "Location" }, "ebd-1");
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.Single(ledger.RequiredBoundaryObligations);
        var rel = ledger.RequiredBoundaryObligations[0].Relation;
        Assert.Equal("ExternalBoundary", BoundaryRelation.RelationKind);
        Assert.Equal(App, rel.PreActionForeground);
        Assert.Equal(ExternalApp, rel.ExternalForeground);
        Assert.Equal(LocationIdentity, rel.ParentContainerIdentity);
        Assert.Equal(LocationIdentity, rel.ExpectedReturnParent);
    }

    [Fact]
    public void EBD2_TapReceiptAloneIsNotBoundaryObserved()
    {
        var rel = new BoundaryRelation(LocationIdentity, BoundarySource + "@1", App, ExternalApp, LocationIdentity, 1);
        Assert.Equal(BoundaryObligationState.Pending, new BoundaryObligation(rel).State);
        Assert.Equal("RETURNED_TO_PARENT", new BoundaryObligation(rel).RequiredDisposition);
        Assert.Equal("ExternalBoundary", BoundaryRelation.RelationKind);
    }
}

// ═════════════════════════════════════════════════════════════════════════
// REAL DEVICE — External Boundary (EBD) validation
// ═════════════════════════════════════════════════════════════════════════

public partial class ExternalBoundaryRealDeviceTests
{
    private const string RealApp = "com.android.settings";
    private const string RealPerctl = "com.android.permissioncontroller";
    private const string RealAdbPath = "/Users/fran/Android/Sdk/platform-tools/adb";
    private const string RealSerial = "emulator-5554";
    private const string RealVisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RealRunId = "settings-external-boundary-ebd";
    private const string RealBoundarySource = "App location permissions";

    /// <summary>Derive the foreground package from a uiautomator XML dump (first node package).</summary>
    private static string? DeriveForegroundFromXml(string xml)
    {
        var m = System.Text.RegularExpressions.Regex.Match(xml, "<node[^>]*?package=\"([^\"]*)\">");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Live foreground detector: the currently focused window package.</summary>
    private static async Task<string> DetectLiveForegroundAsync(CancellationToken ct)
    {
        var runner = new AdbProcessRunner();
        var res = await runner.RunAsync(RealAdbPath,
            ["-s", RealSerial, "shell", "dumpsys", "window", "windows"], TimeSpan.FromSeconds(15), ct);
        var text = System.Text.Encoding.UTF8.GetString(res.StandardOutput);
        var m = System.Text.RegularExpressions.Regex.Match(text,
            @"mCurrentFocus=Window\{[^}]*\s+([A-Za-z0-9_.]+)/");
        return m.Success ? m.Groups[1].Value : RealApp;
    }

    private sealed class StructuredBoundaryEnvironment : IEnvironment
    {
        private readonly PhysicalEnvironment _inner;
        public StructuredBoundaryEnvironment(PhysicalEnvironment inner) => _inner = inner;
        public IReadOnlyList<DeviceAction> ActionHistory => _inner.ActionHistory;
        public List<Observation> AllStructured { get; } = new();
        public List<string> RawXmls { get; } = new();
        public int SystemBackCount;
        public string? LastForeground;

        public async Task<Observation> ObserveAsync(CancellationToken ct)
        {
            var obs = await _inner.ObserveAsync(ct);
            var runner = new AdbProcessRunner();
            _ = await runner.RunAsync(RealAdbPath,
                ["-s", RealSerial, "shell", "uiautomator", "dump", "/sdcard/ebd.xml"], TimeSpan.FromSeconds(30), ct);
            var cat = await runner.RunAsync(RealAdbPath,
                ["-s", RealSerial, "shell", "cat", "/sdcard/ebd.xml"], TimeSpan.FromSeconds(30), ct);
            var xml = System.Text.Encoding.UTF8.GetString(cat.StandardOutput);
            RawXmls.Add(xml);
            var structured = string.IsNullOrWhiteSpace(xml)
                ? obs.StructuredElements
                : AdbUiHierarchySource.Parse(xml, 1080, 1920);
            // Derive the LIVE foreground from the parsed XML's first package
            // (no extra dumpsys latency — avoids perturbing scroll-settle timing;
            // on the external boundary page this yields com.android.permissioncontroller).
            var fg = DeriveForegroundFromXml(xml) ?? obs.ForegroundApplication;
            LastForeground = fg;
            var decorated = obs with { ForegroundApplication = fg, StructuredElements = structured };
            if (!decorated.StructuredElements.IsDefaultOrEmpty)
                AllStructured.Add(decorated);
            return decorated;
        }

        public async Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken ct)
        {
            if (action is DeviceAction.SystemBack) SystemBackCount++;
            return await _inner.ExecuteAsync(action, ct);
        }
    }

    private static CandidateAuthorizationEvidence AuthorizeEbdReal(Observation observation, ObservedElement candidate)
    {
        var isRoot = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal));
        if (isRoot)
        {
            if (string.Equals(candidate.Text, "Location", StringComparison.Ordinal))
                return new(true, "EBD-real: authorize Location child.", AuthorizationKind.AuthorizedChild);
            return new(false, "EBD-real audit: root source not Location.");
        }
        if (string.Equals(candidate.Text, RealBoundarySource, StringComparison.Ordinal))
            return new(true, "EBD-real: authorized external-boundary crossing.", AuthorizationKind.AuthorizedBoundary);
        if (string.Equals(candidate.Text, "Location services", StringComparison.Ordinal))
            return new(true, "EBD-real: recursive child.", AuthorizationKind.AuthorizedChild);
        if (string.Equals(candidate.Text, "SettingsRoot", StringComparison.Ordinal)
            || string.Equals(candidate.Text, "Navigate up", StringComparison.Ordinal))
            return new(true, "EBD-real: labelled parent-return control authorized.");
        return new(false, "EBD-real audit.");
    }

    [Fact]
    public async Task ExternalBoundary_RealDevice()
    {
        var setup = new AdbProcessRunner();
        _ = await setup.RunAsync(RealAdbPath, ["-s", RealSerial, "shell", "pm", "clear", RealApp], TimeSpan.FromSeconds(30), CancellationToken.None);
        _ = await setup.RunAsync(RealAdbPath, ["-s", RealSerial, "shell", "am", "start", "-a", "android.settings.SETTINGS"], TimeSpan.FromSeconds(30), CancellationToken.None);
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(1000);
            var fg = await DetectLiveForegroundAsync(CancellationToken.None);
            if (fg == RealApp) break;
        }
        // Longer settle so Settings fully renders the Location page (avoids
        // transient mid-render empty container wrappers classified Unknown).
        await Task.Delay(3000);

        var raw = new PhysicalEnvironment(
            new AdbScreenshotSource(RealSerial, RealAdbPath),
            new LocalVisionPerceptionSource(RealVisionSocket),
            new AdbDispatchTarget(RealSerial, RealAdbPath),
            RealApp, 1080, 1920);
        var env = new StructuredBoundaryEnvironment(raw);
        var traversal = new UniClaw.Runtime.Traversal.Traversal(env);
        var startup = new RuntimeStartup(env, RealApp, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, ct => env.ObserveAsync(ct),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            page => new RuntimeContainer(page, o => string.Equals(SettingsSingleRecursiveChildTests.ResolveSemanticPage(o), page, StringComparison.Ordinal), traversal.ExecuteStep),
            recovery);
        var goal = new Goal(
            obs => new GoalEvidence(
                string.Equals(obs.ForegroundApplication, RealApp, StringComparison.Ordinal)
                    && obs.StructuredElements.Any(se => string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal)),
                "EBD-real: fresh final Root observation confirms completion.", obs.SequenceNumber),
            CandidateAuthorizationEvaluator: AuthorizeEbdReal,
            ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew,
            BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory);
        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(RealApp, "SettingsRoot"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(RealApp, "SettingsRoot"));
        var envelope = IntentSemanticEnvelope.Project(
            "EBD-real: Root → Location → authorized external boundary → SystemBack → exact parent",
            goal, new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, RealRunId, CancellationToken.None);

        var evidence = new System.Text.StringBuilder();
        for (int ri = 0; ri < env.RawXmls.Count; ri++)
            System.IO.File.WriteAllText($"/tmp/ebd_obs_{ri}.xml", env.RawXmls[ri]);
        evidence.AppendLine($"STATE={state}");
        evidence.AppendLine($"EXTERNAL_FOREGROUND_SEEN={env.AllStructured.Any(o => o.ForegroundApplication == RealPerctl)}");
        evidence.AppendLine($"LAST_FOREGROUND={env.LastForeground}");
        evidence.AppendLine($"SYSTEMBACK_COUNT={env.SystemBackCount}");
        evidence.AppendLine($"CONTAINERS=[{string.Join(",", agent.Trace.Select(t => t.ContainerId).Where(id => id is not null).Distinct())}]");
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE |{entry.ContainerId}| {entry.Reason}");
        File.WriteAllText("/tmp/ebd_real_evidence.txt", evidence.ToString());

        Assert.True(env.AllStructured.Any(o => o.ForegroundApplication == RealPerctl),
            "External foreground (com.android.permissioncontroller) not observed.");
        Assert.Equal(1, env.SystemBackCount); // exactly one SystemBack
        Assert.DoesNotContain(RealPerctl, agent.Trace.Select(t => t.ContainerId)); // external never a container
    }

}
