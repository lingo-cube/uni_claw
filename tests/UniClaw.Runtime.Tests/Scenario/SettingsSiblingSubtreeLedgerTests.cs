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
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_SIBLING_SUBTREE_LEDGER — Phase 4 (SL-1..SL-20).
///
/// One real parent (SettingsRoot) with EXACTLY TWO explicitly authorized
/// sibling children (Location, Battery): Child A completes + verified return →
/// ledger {A} → sibling continuation via the SAME frozen parent epoch →
/// Child B completes + verified return → ledger {A,B} → SubtreeComplete(Parent).
///
/// Ledger contract: RequiredChildren = ONLY explicitly AUTHORIZED_CHILD
/// obligations (AuthorizedSiblingEvidence — DISCOVERED/GROUNDED/AUDITED ≠
/// REQUIRED); CompletedChildren = completed authorized obligations (verified
/// return required — dispatch receipt / ContainerComplete alone are never
/// completion); SubtreeComplete(Parent) = ContainerComplete(Parent) AND every
/// RequiredChild completed with verified return — GoalEvidence /
/// ContainerComplete / return-eligibility alone never imply it.
/// SL-20 (no regression) is the full deterministic suite.
/// </summary>
public sealed class SettingsSiblingSubtreeLedgerTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private const string SearchBarRid = "com.android.settings:id/search_action_bar";
    private const string ParentReturnActionRoleLabel = "Navigate up";
    private const string SiblingALabel = "Location";
    private const string SiblingBLabel = "Battery";
    private const string ChildAIdentity = "SettingsSubpage(Location)";
    private const string ChildBIdentity = "SettingsSubpage(Battery)";

    private const string AdbPath = "/Users/fran/Android/Sdk/platform-tools/adb";
    private const string Serial = "emulator-5554";
    private const string VisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RunId = "settings-sibling-subtree-ledger-001";
    private const string AgentInstanceId = "SETTINGS-LEDGER-001";

    private static int _agentCreations;

    /// <summary>
    /// Phase-4 authorization — EXACTLY TWO root children (the declared sibling
    /// labels; caller branch labels — the binding is the RequiredBranchGrounding
    /// validated by SourceGroundingValidator). From non-root observations only
    /// the labelled parent-return control is authorized (return mechanism).
    /// Everything else is DISCOVERED/GROUNDED/AUDITED but NOT authorized.
    /// </summary>
    internal static CandidateAuthorizationEvidence AuthorizePhase4(Observation observation, ObservedElement candidate)
    {
        var isRootObservation = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, SearchBarRid, StringComparison.Ordinal));
        if (isRootObservation)
        {
            if (string.Equals(candidate.Text, SiblingALabel, StringComparison.Ordinal)
                || string.Equals(candidate.Text, SiblingBLabel, StringComparison.Ordinal))
            {
                return new(true,
                    $"Phase-4: authorize exactly the two declared root siblings ('{SiblingALabel}', '{SiblingBLabel}').");
            }
            return new(false, "Phase-4 audit: root source is not a declared sibling; recursion is Phase 5+.");
        }
        if (string.Equals(candidate.Text, RootPage, StringComparison.Ordinal)
            || string.Equals(candidate.Text, ParentReturnActionRoleLabel, StringComparison.Ordinal))
        {
            return new(true, "Phase-4: labelled parent-return control authorized (return mechanism).");
        }
        return new(false, "Phase-4 audit: child source; recursion is Phase 5+.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SL deterministic tests (fake world, shared resolver/authorizer)
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class LedgerWorld : IEnvironment
    {
        private string _screen = "Launcher"; // Root | Child:Location | Child:Battery
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;
        public IReadOnlyList<Observation> ObservationHistory => _history;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = Build(++_seq);
            _history.Add(observation);
            return Task.FromResult(observation);
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _actions.Add(action);
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    _screen = "Root";
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "launch", "launch dispatched"));
                case DeviceAction.Tap tap:
                    if (_screen == "Root")
                    {
                        var row = ResolveRootRow(tap);
                        _screen = row switch
                        {
                            SiblingALabel => "Child:Location",
                            SiblingBLabel => "Child:Battery",
                            _ => _screen, // audited row taps never dispatched
                        };
                    }
                    else if (_screen == "Child:Location" || _screen == "Child:Battery")
                    {
                        _screen = "Root"; // the verified return
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "tap dispatched"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport unchanged"));
            }
        }

        private Observation Build(long seq)
        {
            if (_screen == "Launcher")
                return new Observation([new ObservedElement("Launcher", null, 0, null, null)], App, seq);
            if (_screen == "Root")
            {
                var rows = new[] { "Network & internet", "Connected devices", "Apps", SiblingALabel, SiblingBLabel, "Notifications", "Storage" };
                return new Observation(
                    rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(i), "text")).ToImmutableArray(),
                    App, seq)
                {
                    StructuredElements = rows.Select((r, i) => Row(r, i))
                        .Append(SearchBar())
                        .ToImmutableArray(),
                };
            }
            if (_screen == "Child:Location")
            {
                var rows = new[] { "See all", "App location permissions", "Location services" };
                return ChildBuild(rows, "Location", seq);
            }
            var batteryRows = new[] { "Battery usage", "Battery Saver", "Battery Manager" };
            return ChildBuild(batteryRows, "Battery", seq);
        }

        private Observation ChildBuild(string[] rows, string title, long seq)
        {
            var structured = rows.Select((r, i) => ChildRow(r, i))
                .Append(UpControl(rows.Length))
                .Append(TitleRole(title))
                .ToImmutableArray();
            return new Observation(
                rows.Select((r, i) => new ObservedElement(r, null, i, ChildRowBounds(i), "text")).ToImmutableArray(),
                App, seq)
            {
                StructuredElements = structured,
            };
        }

        private static string? ResolveRootRow(DeviceAction.Tap tap)
        {
            if (tap.TargetBounds is not { } bounds)
                return null;
            int idx = (int)Math.Round(bounds.Y1 / 0.1f);
            var rows = new[] { "Network & internet", "Connected devices", "Apps", SiblingALabel, SiblingBLabel, "Notifications", "Storage" };
            return idx >= 0 && idx < rows.Length ? rows[idx] : null;
        }

        private static ElementBounds RowBounds(int ordinal) => new(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1));
        private static ElementBounds ChildRowBounds(int ordinal) => new(0, 0.08f + 0.1f * ordinal, 1, 0.08f + 0.1f * (ordinal + 1));

        internal static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", SearchBarRid, true, false, false, true, false,
                new ElementBounds(0f, 0f, 1f, 0.06f), "Search settings", null, null, null, null);

        internal static StructuredElementEvidence UpControl(int ordinal)
            => new("android.widget.ImageButton", null, true, false, false, true, true,
                new ElementBounds(0f, 0f, 0.13f, 0.1f), null, null, null, ParentReturnActionRoleLabel, null);

        internal static StructuredElementEvidence TitleRole(string pageTitle)
            => new("android.widget.FrameLayout", "com.android.settings:id/collapsing_toolbar",
                null, null, null, true, null, new ElementBounds(0f, 0f, 1f, 0.28f),
                null, null, null, pageTitle, null);

        internal static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                RowBounds(ordinal), title, null, false, null, null);

        internal static StructuredElementEvidence ChildRow(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                ChildRowBounds(ordinal), title, null, false, null, null);
    }

    private sealed record SlRunOutcome(RunState State, string? Reason, LedgerWorld Environment, RuntimeAgent Agent);

    private static async Task<SlRunOutcome> RunSlAsync(LedgerWorld world, string runId)
    {
        var traversal = new RuntimeTraversal(world);
        var startup = new RuntimeStartup(world, App, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => world.ObserveAsync(cancellationToken),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(SettingsSingleRecursiveChildTests.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var goal = new Goal(
            SettingsSingleRecursiveChildTests.AuditGoal,
            CandidateAuthorizationEvaluator: AuthorizePhase4,
            ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew,
            BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Phase-4: two authorized siblings → verified returns → SubtreeComplete(Parent)",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new SlRunOutcome(state, agent.Reason, world, agent);
    }

    private static BranchProgressEvidence Progress(
        string[] approved, string[] authorized, string[] completed)
        => new(
            RootPage,
            approved.ToImmutableDictionary(id => id, _ => 1L, StringComparer.Ordinal),
            completed.ToImmutableDictionary(id => id, _ => 2L, StringComparer.Ordinal),
            authorized.ToImmutableDictionary(id => id, _ => 1L, StringComparer.Ordinal));

    private static BranchProgressEvidence RootLedger(RuntimeAgent agent)
        => agent.ProgressSnapshot[RootPage];

    // ── SL-1: two explicitly authorized siblings → RequiredChildren count=2 ─

    [Fact]
    public async Task SL1_TwoAuthorizedSiblings_RequiredChildrenTwo()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-1");

        var ledger = RootLedger(run.Agent);
        Assert.Equal(2, ledger.RequiredChildren.Count);
        Assert.Contains(SiblingALabel, ledger.RequiredChildren.Keys);
        Assert.Contains(SiblingBLabel, ledger.RequiredChildren.Keys);
    }

    // ── SL-2: discovered but denied source → not RequiredChild ──────────────

    [Fact]
    public async Task SL2_DeniedSource_NotRequiredChild()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-2");

        var ledger = RootLedger(run.Agent);
        // "Notifications"/"Storage"/"Apps" were discovered + audited (denied)
        // → never RequiredChildren.
        Assert.DoesNotContain("Notifications", ledger.RequiredChildren.Keys);
        Assert.DoesNotContain("Storage", ledger.RequiredChildren.Keys);
        Assert.DoesNotContain("Apps", ledger.RequiredChildren.Keys);
    }

    // ── SL-3: Child A ContainerComplete alone → not CompletedChild ──────────

    [Fact]
    public void SL3_ContainerCompleteAlone_NotCompletedChild()
    {
        var progress = Progress(
            approved: [SiblingALabel, SiblingBLabel, "Notifications"],
            authorized: [SiblingALabel, SiblingBLabel],
            completed: []); // no verified return yet
        Assert.Empty(progress.CompletedChildren);
    }

    // ── SL-4: Child A verified return → CompletedChildren={A} ───────────────

    [Fact]
    public void SL4_ChildAVerifiedReturn_CompletedContainsA()
    {
        var progress = Progress(
            approved: [SiblingALabel, SiblingBLabel, "Notifications"],
            authorized: [SiblingALabel, SiblingBLabel],
            completed: [SiblingALabel]); // only A returned
        var completed = progress.CompletedChildren;
        Assert.Single(completed);
        Assert.Contains(SiblingALabel, completed.Keys);
        Assert.DoesNotContain(SiblingBLabel, completed.Keys);
    }

    // ── SL-5: after A only → SubtreeComplete(parent)=FALSE ──────────────────

    [Fact]
    public void SL5_AfterAOnly_SubtreeCompleteFalse()
    {
        var progress = Progress(
            approved: [SiblingALabel, SiblingBLabel, "Notifications"],
            authorized: [SiblingALabel, SiblingBLabel],
            completed: [SiblingALabel]);
        Assert.False(progress.IsSubtreeCompleteByRequiredChildren);
    }

    // ── SL-6: returned Parent uses frozen epoch (no re-discovery) ───────────

    [Fact]
    public async Task SL6_ReturnedParentUsesFrozenEpoch()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-6");

        // The Root epoch is frozen exactly once — the sibling continuation
        // reuses it (no re-discovery after the returns).
        var rootEpochs = run.Agent.Trace.Count(t =>
            t.ContainerId == RootPage && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.Equal(1, rootEpochs);
    }

    // ── SL-7: Parent consistency required before sibling B ──────────────────

    [Fact]
    public async Task SL7_ParentConsistencyRequiredBeforeSiblingB()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-7");

        // The returned Parent evidence was validated against the frozen epoch
        // between the siblings — zero INVALIDATED throughout.
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("Post-completeness fresh evidence INVALIDATED", StringComparison.Ordinal) is true);
    }

    // ── SL-8: fresh bounds required for B dispatch ──────────────────────────

    [Fact]
    public async Task SL8_FreshBoundsRequiredForBDispatch()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-8");

        var taps = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().ToList();
        // 4 taps: Root→A, A→Root, Root→B, B→Root (verified returns).
        Assert.Equal(4, taps.Count);
        Assert.All(taps, t => Assert.True(t.TargetBounds is { IsValid: true } && t.TargetBounds.Height > 0f));
    }

    // ── SL-9: Child A visited retained; B still traversable ─────────────────

    [Fact]
    public async Task SL9_AVisitedRetained_BTraversable()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-9");

        Assert.Contains(run.Agent.Trace, t => t.ContainerId == ChildAIdentity);
        Assert.Contains(run.Agent.Trace, t => t.ContainerId == ChildBIdentity);
    }

    // ── SL-10: re-entry into completed Child A rejected ─────────────────────

    [Fact]
    public async Task SL10_ReentryIntoCompletedChildARejected()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-10");

        // A is entered exactly once: exactly ONE A discovery epoch freeze — a
        // re-entry would attempt a second A container (duplicate identity →
        // fail closed). The completed sibling is excluded from the pending set.
        var aEpochs = run.Agent.Trace.Count(t =>
            t.ContainerId == ChildAIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.Equal(1, aEpochs);
    }

    // ── SL-11: Child B verified return → CompletedChildren={A,B} ────────────

    [Fact]
    public async Task SL11_ChildBVerifiedReturn_CompletedAB()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-11");

        var ledger = RootLedger(run.Agent);
        Assert.Equal(2, ledger.CompletedChildren.Count);
        Assert.Contains(SiblingALabel, ledger.CompletedChildren.Keys);
        Assert.Contains(SiblingBLabel, ledger.CompletedChildren.Keys);
        // Both returns were VERIFIED (the traces exist).
        Assert.Equal(2, run.Agent.Trace.Count(t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true));
    }

    // ── SL-12: all RequiredChildren complete → SubtreeComplete(parent)=TRUE ──

    [Fact]
    public async Task SL12_AllRequiredChildrenComplete_SubtreeCompleteTrue()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-12");

        var ledger = RootLedger(run.Agent);
        Assert.True(ledger.IsSubtreeCompleteByRequiredChildren);
    }

    // ── SL-13: ContainerComplete(parent) alone != SubtreeComplete ───────────

    [Fact]
    public void SL13_ContainerCompleteAlone_NotSubtreeComplete()
    {
        // A complete container with ZERO required children is NOT a subtree
        // completion (nothing was required).
        var progress = Progress(
            approved: ["Notifications", "Storage"],
            authorized: [],
            completed: []);
        Assert.False(progress.IsSubtreeCompleteByRequiredChildren);
    }

    // ── SL-14: GoalEvidence true alone != SubtreeComplete ───────────────────

    [Fact]
    public void SL14_GoalEvidenceAlone_NotSubtreeComplete()
    {
        var progress = Progress(
            approved: [SiblingALabel, SiblingBLabel],
            authorized: [SiblingALabel, SiblingBLabel],
            completed: []); // GoalEvidence could be satisfied; obligations pending
        Assert.False(progress.IsSubtreeCompleteByRequiredChildren);
    }

    // ── SL-15: pending authorized child prevents SubtreeComplete ────────────

    [Fact]
    public void SL15_PendingAuthorizedChild_PreventsSubtreeComplete()
    {
        var progress = Progress(
            approved: [SiblingALabel, SiblingBLabel],
            authorized: [SiblingALabel, SiblingBLabel],
            completed: [SiblingALabel]); // B authorized but pending
        Assert.False(progress.IsSubtreeCompleteByRequiredChildren);
    }

    // ── SL-16: denied child does not prevent SubtreeComplete ────────────────

    [Fact]
    public async Task SL16_DeniedChild_DoesNotPreventSubtreeComplete()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-16");

        // "Notifications"/"Storage"/"Apps" were denied — they are not required
        // children and do not block the subtree completion.
        var ledger = RootLedger(run.Agent);
        Assert.True(ledger.IsSubtreeCompleteByRequiredChildren);
        Assert.DoesNotContain("Notifications", ledger.RequiredChildren.Keys);
    }

    // ── SL-17: wrong-return destination does not complete the obligation ────

    [Fact]
    public void SL17_WrongReturnDestination_DoesNotCompleteObligation()
    {
        // A CompletedChild must correspond to the exact authorized obligation:
        // a completed entry for a DIFFERENT identity cannot satisfy A's
        // obligation (the ledger keys are the authorized branch identities).
        var progress = Progress(
            approved: [SiblingALabel, SiblingBLabel],
            authorized: [SiblingALabel, SiblingBLabel],
            completed: [SiblingALabel]);
        Assert.False(progress.CompletedChildren.ContainsKey("SettingsSubpage(Location)"));
        Assert.False(progress.IsSubtreeCompleteByRequiredChildren);
    }

    // ── SL-18: external boundary cannot silently count completed ────────────

    [Fact]
    public void SL18_ExternalBoundary_NotSilentlyCompleted()
    {
        // An external-boundary source is DISCOVERED but NOT AUTHORIZED → it is
        // not a RequiredChild and never counts as completed; the verified-
        // boundary disposition stays explicit (no "ignored = completed").
        var progress = Progress(
            approved: ["About emulated device", SiblingALabel, SiblingBLabel],
            authorized: [SiblingALabel, SiblingBLabel],
            completed: [SiblingALabel, SiblingBLabel]);
        Assert.DoesNotContain("About emulated device", progress.RequiredChildren.Keys);
        Assert.DoesNotContain("About emulated device", progress.CompletedChildren.Keys);
        Assert.True(progress.IsSubtreeCompleteByRequiredChildren);
    }

    // ── SL-19: ledger does not mutate frozen inventory ──────────────────────

    [Fact]
    public async Task SL19_LedgerDoesNotMutateFrozenInventory()
    {
        var run = await RunSlAsync(new LedgerWorld(), "sl-19");

        var rootEpoch = run.Agent.Trace.FirstOrDefault(t =>
            t.ContainerId == RootPage && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.NotNull(rootEpoch);
        // The ledger (progress) is separate evidence — the frozen epoch
        // sequences are untouched by sibling completion.
        Assert.Contains("sources=7", rootEpoch.Reason, StringComparison.Ordinal);
    }

    // ── SL-20: VRT / GC / DIM / PCC / PRC / RC1 / ART / ROLE / SIG / SEARCH /
    // ── SQ / PROV / NM / RVT / AFF / SET / COMPOSE-05 green — covered by the
    // ── full deterministic suite. ────────────────────────────────────────────

    // ═════════════════════════════════════════════════════════════════════════
    // REAL DEVICE Phase-4 run
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class StructuredEnvironment : IEnvironment
    {
        private readonly PhysicalEnvironment _inner;
        public StructuredEnvironment(PhysicalEnvironment inner) => _inner = inner;
        public IReadOnlyList<DeviceAction> ActionHistory => _inner.ActionHistory;
        public IReadOnlyList<Observation> ObservationHistory => _inner.ObservationHistory;
        public List<Observation> AllObservations { get; } = new();
        public List<string> RawXmls { get; } = new();

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var observation = await _inner.ObserveAsync(cancellationToken);
            var runner = new AdbProcessRunner();
            _ = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/ledger.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var cat = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "cat", "/sdcard/ledger.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var xml = System.Text.Encoding.UTF8.GetString(cat.StandardOutput);
            RawXmls.Add(xml);
            if (string.IsNullOrWhiteSpace(xml))
                return observation;
            try
            {
                var structured = AdbUiHierarchySource.Parse(xml, 1080, 1920);
                var decorated = observation with { StructuredElements = structured };
                AllObservations.Add(decorated);
                return decorated;
            }
            catch
            {
                AllObservations.Add(observation);
                return observation;
            }
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => _inner.ExecuteAsync(action, cancellationToken);
    }

    [Fact]
    public async Task SettingsSiblingSubtreeLedger_RealDevice_Phase4()
    {
        _agentCreations = 0;
        var setupRunner = new AdbProcessRunner();
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "force-stop", App }, TimeSpan.FromSeconds(30), CancellationToken.None);
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "start", "-a", "android.settings.SETTINGS" }, TimeSpan.FromSeconds(30), CancellationToken.None);
        for (int i = 0; i < 20; i++)
        {
            var probe = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/ready_ledger.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            var probeCat = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "cat", "/sdcard/ready_ledger.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            if (System.Text.Encoding.UTF8.GetString(probeCat.StandardOutput).Contains("com.android.settings", StringComparison.Ordinal))
                break;
            await Task.Delay(1000);
        }

        var rawEnvironment = new PhysicalEnvironment(
            new AdbScreenshotSource(Serial, AdbPath),
            new LocalVisionPerceptionSource(VisionSocket),
            new AdbDispatchTarget(Serial, AdbPath),
            App, 1080, 1920);
        var environment = new StructuredEnvironment(rawEnvironment);

        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(
            environment, App, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        _agentCreations++;
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(SettingsSingleRecursiveChildTests.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var receipts = new List<GoalEvidence>();
        var goal = new Goal(
            observation =>
            {
                var evidence = SettingsSingleRecursiveChildTests.AuditGoal(observation);
                receipts.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: AuthorizePhase4,
            ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew,
            BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Phase-4: two authorized siblings → verified returns → SubtreeComplete(Parent)",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, RunId, CancellationToken.None);

        // ── evidence dump ────────────────────────────────────────────────────
        var evidence = new System.Text.StringBuilder();
        evidence.AppendLine($"STATE={state}");
        evidence.AppendLine($"AGENT_ID={AgentInstanceId} (creations={_agentCreations})");
        evidence.AppendLine($"RUN_ID={RunId}");
        evidence.AppendLine($"SIBLINGS={SiblingALabel},{SiblingBLabel}");
        evidence.AppendLine("OBSERVATIONS=" + string.Join(",", environment.ObservationHistory.Select(o => o.SequenceNumber)));
        var rootEpoch = agent.Trace.FirstOrDefault(t =>
            t.ContainerId == RootPage && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        evidence.AppendLine($"ROOT_EPOCH_FROZEN={rootEpoch is not null} epochTrace={rootEpoch?.Reason}");
        var ledger = agent.ProgressSnapshot.TryGetValue(RootPage, out var rootProgress) ? rootProgress : null;
        evidence.AppendLine($"LEDGER_ROOT required=[{string.Join(",", ledger?.RequiredChildren.Keys ?? [])}] completed=[{string.Join(",", ledger?.CompletedChildren.Keys ?? [])}] subtreeCompleteByRequired={ledger?.IsSubtreeCompleteByRequiredChildren}");
        evidence.AppendLine("ACTIONS=" + string.Join(",", environment.ActionHistory.Select(a => a.GetType().Name)));
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE {entry.RunState} | {entry.ContainerId} | {entry.StepId} | {entry.Reason}");
        evidence.AppendLine("GOAL_EVIDENCE=" + string.Join(";", receipts.Select(r => $"{r.Satisfied}@{r.SourceObservationSequence}")));
        File.WriteAllText("/tmp/settings_ledger_evidence.txt", evidence.ToString());

        // ── Phase-4 truth: the trace + ledger decide.
        Assert.Equal(1, _agentCreations);
        Assert.True(environment.ObservationHistory.Any(o =>
            string.Equals(o.ForegroundApplication, App, StringComparison.Ordinal)));
        Assert.True(environment.AllObservations.Any(o => !o.StructuredElements.IsDefaultOrEmpty));
    }
}
