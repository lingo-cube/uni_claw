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
using UniClaw.Runtime.Tests.Scenario.Fakes;
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

        /// <summary>Simulate a REAL transition delay: after tapping the external
        /// target the foreground stays on the OWNED app for a couple of frames
        /// before the external activity appears (and then stays stable).</summary>
        public bool DelayExternalTransition { get; init; }

        /// <summary>Simulate an external target that NEVER opens: the foreground
        /// stays on the owned app forever (the bounded settle must fail closed).</summary>
        public bool ExternalNeverAppears { get; init; }
        public int SystemBackCount { get; private set; }

        private long _externalVisibleFrom;
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
                    if (Screen == "External")
                    {
                        if (ExternalNeverAppears)
                            Screen = "Location"; // the external page never opens
                        else if (DelayExternalTransition)
                            _externalVisibleFrom = _seq + 2; // external appears 2 frames later
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "tap"));
                case DeviceAction.SystemBack:
                    SystemBackCount++;
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
            // While the delayed transition is in flight the foreground is STILL
            // the owned app (the external activity has not appeared yet).
            "External" when DelayExternalTransition && seq < _externalVisibleFrom => LocationObservation(seq),
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
                rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(3 + i), "text"))
                    .Append(new ObservedElement(ParentReturnActionRoleLabel, null, rows.Length, UpControlBounds, "image_button"))
                    .ToImmutableArray(), App, seq)
            {
                StructuredElements = rows.Select((r, i) => Row(r, 3 + i))
                    .Append(UpControl()).Append(TitleRole("Location")).ToImmutableArray(),
            };
        }

        private Observation ServicesObservation(long seq)
        {
            var rows = new[] { "Wi-Fi scanning", "Bluetooth scanning" };
            return new Observation(
                rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(3 + i), "text"))
                    .Append(new ObservedElement(ParentReturnActionRoleLabel, null, rows.Length, UpControlBounds, "image_button"))
                    .ToImmutableArray(), App, seq)
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

        private static readonly ElementBounds UpControlBounds = new(0f, 0f, 0.13f, 0.1f);

        internal static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true, RowBounds(ordinal), RawText: title);

        internal static StructuredElementEvidence UpControl()
            => new("android.widget.ImageButton", null, true, false, false, true, true,
                new ElementBounds(0f, 0f, 0.13f, 0.1f), ContentDescription: ParentReturnActionRoleLabel);

        internal static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", "com.android.settings:id/search_action_bar", true, false, false, true, false,
                new ElementBounds(0f, 0.1f, 1f, 0.3f), RawText: "Search settings");

        internal static StructuredElementEvidence TitleRole(string pageTitle)
            => new("android.widget.FrameLayout", TitleRoleRid, null, null, null, true, null,
                new ElementBounds(0f, 0f, 1f, 0.28f), ContentDescription: pageTitle);

        internal static StructuredElementEvidence TitleRoleExt(string pageTitle)
            => new("android.widget.FrameLayout", ExternalTitleRoleRid, null, null, null, true, null,
                new ElementBounds(0f, 0f, 1f, 0.28f), ContentDescription: pageTitle);
    }

    private sealed record EbdOutcome(RunState State, string? Reason, BoundaryWorld Environment, RuntimeAgent Agent);

    private static async Task<EbdOutcome> RunEbdAsync(BoundaryWorld world, string runId, Func<Observation, GoalEvidence>? goalEvidence = null)
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(world);
        var traversal = new UniClaw.Runtime.Traversal.Traversal(environment);
        var startup = new RuntimeStartup(environment, App, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup, traversal, ct => environment.ObserveAsync(ct),
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
    public async Task EBD19_ExternalTransitionDelayed_SettlesAndRecognized()
    {
        // The external activity appears a couple of frames AFTER the tap (the
        // foreground stays on the OWNED app meanwhile — the real-device
        // transition delay). The bounded transition settle must NOT fail on the
        // first frame; once the external foreground appears and stabilizes, the
        // boundary is observed and the flow completes.
        var run = await RunEbdAsync(
            new BoundaryWorld { BackToScreen = "Location", DelayExternalTransition = true }, "ebd-19");

        Assert.Contains(run.Agent.Trace, t => t.Reason?.Contains("EXTERNAL_BOUNDARY_OBSERVED", StringComparison.Ordinal) is true);
        var ledger = LocationLedger(run.Agent);
        Assert.NotNull(ledger);
        Assert.Single(ledger.RequiredBoundaryObligations);
        Assert.Equal(ExternalApp, ledger.RequiredBoundaryObligations[0].Relation.ExternalForeground);
        Assert.Equal(1, run.Environment.SystemBackCount);
        Assert.Equal(1, run.Agent.Trace.Count(t => t.Reason?.Contains("EXTERNAL_BOUNDARY_RETURNED_TO_PARENT", StringComparison.Ordinal) is true));
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("did not settle into an external foreground", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task EBD20_ExternalTransitionNeverAppears_FailsClosed()
    {
        // The external page NEVER opens: the bounded transition settle exhausts
        // its budget and fails closed — no SystemBack, no obligation, never an
        // assumed success.
        var run = await RunEbdAsync(
            new BoundaryWorld { ExternalNeverAppears = true }, "ebd-20");

        Assert.Contains(run.Agent.Trace, t => t.Reason?.Contains("did not settle into an external foreground", StringComparison.Ordinal) is true);
        Assert.Equal(0, run.Environment.SystemBackCount);
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("EXTERNAL_BOUNDARY_OBSERVED", StringComparison.Ordinal) is true);
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

[Collection("RealDevice")]
public partial class ExternalBoundaryRealDeviceTests
{
    private const string RealApp = "com.android.settings";
    private const string RealPerctl = "com.android.permissioncontroller";
    private static string RealAdbPath => RealDeviceTestConfiguration.AdbPath;
    private static string RealSerial => RealDeviceTestConfiguration.SettingsSerial;
    private const string RealVisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RealRunId = "settings-external-boundary-ebd";
    private const string RealBoundarySource = "App location permissions";

    /// <summary>Canonical OCR text: lowercased, whitespace/punctuation stripped —
    /// the real detector emits the SAME row with unstable spelling across frames
    /// ("Notification history, conversations" vs "Notification history,conversations",
    /// "38%used-9.96GBfree" vs "38% used - 9.96 GB free"). The harness canonicalizes
    /// so the ordered-overlap normalizer sees ONE stable signature per row; all
    /// test-side consumers compare canonical labels.</summary>
    internal static string CanonicalText(string text)
        => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    // Foreground package detection uses UiAutomatorXml.ForegroundPackage
    // (attribute-order independent parser, extracted for direct unit testing —
    // see ForegroundDetectionTests).

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

        /// <summary>Test-side root-screen marker derived from the uiautomator XML
        /// (the Settings root carries the search_action_bar resource). Never
        /// injected into the Runtime observation — Vision-first.</summary>
        public bool IsSettingsRootFrame;

        public async Task<Observation> ObserveAsync(CancellationToken ct)
        {
            var obs = await _inner.ObserveAsync(ct);
            // uiautomator = AUXILIARY ANALYSIS ONLY (never a flow component):
            // the XML is parsed solely for test-side device-state collection
            // (live foreground, root-screen marker, external-foreground
            // assertion). It is NEVER injected into the Runtime observation —
            // the observation carries the primary OCR channel and nothing else.
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
            var fg = UiAutomatorXml.ForegroundPackage(xml) ?? obs.ForegroundApplication;
            LastForeground = fg;
            IsSettingsRootFrame = structured.Any(se =>
                string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal));
            var structuredObs = obs with { ForegroundApplication = fg, StructuredElements = structured };
            if (!structured.IsDefaultOrEmpty)
                AllStructured.Add(structuredObs);
            // VISION-FIRST EVIDENCE NORMALIZATION (test harness; mirrors a real
            // semantic capability assembling stable screen-ordered detections):
            // the primary OCR channel is the navigation evidence, but the raw
            // real-device detections violate the ordered-overlap contract the
            // normalizer correctly enforces (duplicate detections of one row,
            // icon-typed text overlays, unstable perception types AND unstable
            // OCR text for the same row — whitespace/punctuation variants and
            // short fragments like the location-pin "LoO"/"Lo"/"Lou"). Normalize
            // to: one occurrence per distinct CANONICAL text (lowercased,
            // whitespace/punctuation stripped), stable "row" type, screen order
            // from the OCR bounds, fragments (< 4 canonical chars) dropped.
            // The search-bar/title anchor is excluded from the NAV sequence (the
            // fixture classifier would mark it NonInteractive anyway).
            // TOP-EDGE drop: rows entering at the TOP edge are the least reliable
            // detections (the detector may miss them in the previous frame,
            // breaking the ordered suffix/prefix overlap); excluding the
            // top-most nav row per frame keeps the evidence in the trusted
            // region. NOTE: OCR bounds are used as-is — the imprecise bounding
            // boxes on the dense Settings list are a REPORTED perception defect
            // (see external-boundary-evidence-analysis.md), not compensated here.
            var normalized = obs.Elements
                .Where(e => !string.IsNullOrWhiteSpace(e.Text)
                    && !string.Equals(e.PerceptionType, "icon", StringComparison.OrdinalIgnoreCase))
                .Select(e => new { Raw = e, Canon = CanonicalText(e.Text!) })
                .Where(x => x.Canon.Length >= 4
                    && !x.Canon.EndsWith("searchsettings", StringComparison.Ordinal)
                    && !string.Equals(x.Canon, "settings", StringComparison.Ordinal))
                .GroupBy(x => x.Canon, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(x => x.Raw.Bounds is { IsValid: true } b ? b.Y1 : 0f)
                .ThenBy(x => x.Raw.Bounds is { IsValid: true } b ? b.X1 : 0f)
                .Skip(1)
                .Select(x => x.Raw with { Text = x.Canon, PerceptionType = "row" })
                .ToImmutableArray();
            return obs with { ForegroundApplication = fg, Elements = normalized };
        }

        public async Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken ct)
        {
            if (action is DeviceAction.SystemBack) SystemBackCount++;
            return await _inner.ExecuteAsync(action, ct);
        }
    }

    private static CandidateAuthorizationEvidence AuthorizeEbdReal(
        StructuredBoundaryEnvironment env, Observation observation, ObservedElement candidate)
    {
        if (env.IsSettingsRootFrame)
        {
            if (string.Equals(candidate.Text, "location", StringComparison.Ordinal))
                return new(true, "EBD-real: authorize Location child.", AuthorizationKind.AuthorizedChild);
            return new(false, "EBD-real audit: root source not Location.");
        }
        if (string.Equals(candidate.Text, CanonicalText(RealBoundarySource), StringComparison.Ordinal))
            return new(true, "EBD-real: authorized external-boundary crossing.", AuthorizationKind.AuthorizedBoundary);
        if (string.Equals(candidate.Text, "locationservices", StringComparison.Ordinal))
            return new(true, "EBD-real: recursive child.", AuthorizationKind.AuthorizedChild);
        if (string.Equals(candidate.Text, "settingsroot", StringComparison.Ordinal)
            || string.Equals(candidate.Text, "navigateup", StringComparison.Ordinal))
            return new(true, "EBD-real: labelled parent-return control authorized.");
        return new(false, "EBD-real audit.");
    }

    /// <summary>
    /// EBD-real VISION-FIRST page resolution: the Runtime consumes ONLY the
    /// primary OCR channel. The root/sub-page CLASS uses the test-side
    /// auxiliary root marker (uiautomator-derived, auxiliary analysis only —
    /// never part of the observation); the sub-page IDENTITY comes from the OCR
    /// title text. Caller-supplied scenario knowledge, like every resolver.
    /// </summary>
    private static string? EbdResolveSemanticPage(
        StructuredBoundaryEnvironment env, Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, RealApp, StringComparison.Ordinal))
            return null;
        if (env.IsSettingsRootFrame)
            return "SettingsRoot";
        var title = observation.Elements
            .Select(e => e.Text)
            .FirstOrDefault(t => t is "uselocation" or "locationservices" or "applocationpermissions");
        return title is null ? null : "Location:" + title;
    }

    /// <summary>
    /// EBD-real viewport exploration: continue while new navigation sources
    /// appear, EXCEPT stop (exhausted) once the target "Location" entry is
    /// visible — the Settings list is long and keeps showing new rows at the
    /// bottom, so a generic "new source" criterion would scroll forever past
    /// the EBD target. This is scenario-specific exploration BOUNDING for the
    /// real-device test (the Runtime's exploration authority is unchanged; the
    /// evaluator is the caller-supplied criterion).
    /// </summary>
    private static ViewportExplorationEvidence EbdViewportExploration(ImmutableArray<Observation> observations)
    {
        if (observations.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(true, "explore");
        var latest = observations[^1];
        if (latest.Elements.Any(e => string.Equals(e.Text, "location", StringComparison.Ordinal)))
            return new ViewportExplorationEvidence(false, "EBD target Location visible; exploration exhausted.");
        static System.Collections.Generic.HashSet<string> Sigs(Observation o) =>
            SourceEquivalenceNormalizer.OccurrencesOf(o).Select(x => x.StructuredSignature).ToHashSet(StringComparer.Ordinal);
        var latestSigs = Sigs(latest);
        var prior = observations.Take(observations.Length - 1)
            .SelectMany(o => Sigs(o)).ToHashSet(StringComparer.Ordinal);
        var hasNew = latestSigs.Any(s => !prior.Contains(s));
        return new ViewportExplorationEvidence(
            hasNew,
            hasNew ? "new source appeared; scroll more" : "no new source; exhausted");
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
        var structuredEnv = new StructuredBoundaryEnvironment(raw);
        // Fixture semantic capability (test-side): primary OCR elements get an
        // admitted role so viewport exploration can start — without navigation
        // candidates ExploreWhileNew sees "no new source" and never scrolls.
        // Root-page text rows are navigation candidates; sub-page text is
        // non-interactive except the labelled parent-return control.
        var env = new SemanticCapabilityTestEnvironment(structuredEnv, (observation, element, index) =>
        {
            var text = element.Text;
            if (string.IsNullOrWhiteSpace(text))
                return FixtureSemanticRole.NonInteractive;
            // Settings screen TITLE / search bar (canonical OCR form of
            // "Settings"/"Search settings" — the detector may prefix the search
            // hint with the Q icon glyph, e.g. "Q Search settings") is constant
            // across scroll frames — never a navigation source (it would pollute
            // the normalization signature sequence with a stable-but-inert
            // anchor and mask real row transitions).
            if (text.EndsWith("searchsettings", StringComparison.Ordinal)
                || string.Equals(text, "settings", StringComparison.Ordinal))
            {
                return FixtureSemanticRole.NonInteractive;
            }
            // Root page: text rows are navigation candidates (authorization is
            // decided by AuthorizeEbdReal, which accepts only Location etc.).
            if (string.Equals(text, "SettingsRoot", StringComparison.Ordinal)
                || string.Equals(text, "Navigate up", StringComparison.Ordinal)
                || string.Equals(text, "Location services", StringComparison.Ordinal)
                || string.Equals(text, "App location permissions", StringComparison.Ordinal))
            {
                return FixtureSemanticRole.ParentReturnControl;
            }
            return FixtureSemanticRole.NavigationCandidate;
        });
        var traversal = new UniClaw.Runtime.Traversal.Traversal(env);
        string? Page(Observation o) => EbdResolveSemanticPage(structuredEnv, o);
        var startup = new RuntimeStartup(env, RealApp, Page,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, ct => env.ObserveAsync(ct),
            Page,
            page => new RuntimeContainer(page, o => string.Equals(Page(o), page, StringComparison.Ordinal), traversal.ExecuteStep),
            recovery);
        var goal = new Goal(
            obs => new GoalEvidence(
                string.Equals(obs.ForegroundApplication, RealApp, StringComparison.Ordinal)
                    && structuredEnv.IsSettingsRootFrame,
                "EBD-real: fresh final Root observation confirms completion.", obs.SequenceNumber),
            CandidateAuthorizationEvaluator: (obs, candidate) => AuthorizeEbdReal(structuredEnv, obs, candidate),
            ViewportExplorationEvaluator: EbdViewportExploration,
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
        for (int ri = 0; ri < structuredEnv.RawXmls.Count; ri++)
            System.IO.File.WriteAllText($"/tmp/ebd_obs_{ri}.xml", structuredEnv.RawXmls[ri]);
        evidence.AppendLine($"STATE={state}");
        evidence.AppendLine($"EXTERNAL_FOREGROUND_SEEN={structuredEnv.AllStructured.Any(o => o.ForegroundApplication == RealPerctl)}");
        evidence.AppendLine($"LAST_FOREGROUND={structuredEnv.LastForeground}");
        evidence.AppendLine($"SYSTEMBACK_COUNT={structuredEnv.SystemBackCount}");
        evidence.AppendLine($"CONTAINERS=[{string.Join(",", agent.Trace.Select(t => t.ContainerId).Where(id => id is not null).Distinct())}]");
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE |{entry.ContainerId}| {entry.Reason}");
        evidence.AppendLine("FRAME_TIMELINE (AllStructured seq -> fg):");
        foreach (var o in structuredEnv.AllStructured)
            evidence.AppendLine($"  seq={o.SequenceNumber} fg={o.ForegroundApplication}");
        evidence.AppendLine($"XML_COUNT={structuredEnv.RawXmls.Count}");
        File.WriteAllText("/tmp/ebd_real_evidence.txt", evidence.ToString());

        Assert.True(structuredEnv.AllStructured.Any(o => o.ForegroundApplication == RealPerctl),
            "External foreground (com.android.permissioncontroller) not observed.");
        Assert.Equal(1, structuredEnv.SystemBackCount); // exactly one SystemBack
        Assert.DoesNotContain(RealPerctl, agent.Trace.Select(t => t.ContainerId)); // external never a container
    }

}
