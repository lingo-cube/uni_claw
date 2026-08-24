using System;
using System.Collections.Immutable;
using System.Linq;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-2: external boundary observed → RuntimeDecision Revise. A bounded exploration
/// directive expects a recursive child transition; the Fake world crosses an external
/// boundary (trace records EXTERNAL_BOUNDARY_OBSERVED); the reconciler classifies
/// Revise. The RuntimeAgent does not decide the boundary action — the existing Agent
/// authority (its boundary handling) remains responsible, and the decision only
/// records the contradiction.
/// </summary>
public sealed class ReconciliationScenario2ReviseTests
{
    private const string App = "com.android.settings";
    private const string ExternalApp = "com.android.permissioncontroller";
    private const string TitleRoleRid = "com.android.settings:id/collapsing_toolbar";
    private const string ExternalTitleRoleRid = "com.android.permissioncontroller:id/collapsing_toolbar";
    private const string ParentReturnActionRoleLabel = "Navigate up";
    private const string RootPage = "SettingsRoot";
    private const string BoundarySource = "App location permissions";
    private const string ChildSource = "Location services";

    /// <summary>Deterministic external-world Fake with a genuine boundary crossing.</summary>
    private sealed class BoundaryScenarioWorld : IEnvironment
    {
        public string Screen { get; private set; } = "Root";
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
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
                    Screen = "Location";
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
            => new(new[]
            {
                new ObservedElement("Location", null, 0, RowBounds(3), "menu_item"),
                new ObservedElement("Search", null, 1, RowBounds(1), "button"),
            }.ToImmutableArray(), App, seq)
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
                rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(3 + i), "menu_item"))
                    .Append(new ObservedElement(ParentReturnActionRoleLabel, null, 2, RowBounds(0), "button"))
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
                rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(3 + i), "menu_item"))
                    .Append(new ObservedElement(ParentReturnActionRoleLabel, null, 2, RowBounds(0), "button"))
                    .ToImmutableArray(), App, seq)
            {
                StructuredElements = rows.Select((r, i) => Row(r, 3 + i))
                    .Append(UpControl()).Append(TitleRole("Location services")).ToImmutableArray(),
            };
        }

        private Observation ExternalObservation(long seq)
            => new(new[]
            {
                new ObservedElement("Allowed all the time", null, 0, RowBounds(3), "menu_item"),
                new ObservedElement(ParentReturnActionRoleLabel, null, 1, RowBounds(0), "button"),
            }.ToImmutableArray(), ExternalApp, seq)
            {
                StructuredElements = new[] { UpControl(), TitleRoleExt("Location") }.ToImmutableArray(),
            };

        private static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", null, false, false, false, true, true, RowBounds(ordinal), RawText: title);

        private static StructuredElementEvidence UpControl()
            => new("android.widget.ImageButton", null, false, false, false, true, true,
                new ElementBounds(0f, 0f, 0.13f, 0.1f), ContentDescription: ParentReturnActionRoleLabel);

        private static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", "com.android.settings:id/search_action_bar", false, false, false, true, false,
                new ElementBounds(0f, 0.1f, 1f, 0.3f), RawText: "Search settings");

        private static StructuredElementEvidence TitleRole(string pageTitle)
            => new("android.widget.FrameLayout", TitleRoleRid, null, null, null, true, null,
                new ElementBounds(0f, 0f, 1f, 0.28f), ContentDescription: pageTitle);

        private static StructuredElementEvidence TitleRoleExt(string pageTitle)
            => new("android.widget.FrameLayout", ExternalTitleRoleRid, null, null, null, true, null,
                new ElementBounds(0f, 0f, 1f, 0.28f), ContentDescription: pageTitle);
    }

    [Fact]
    public async Task ExternalBoundaryObserved_ProducesReviseDecision_WhileAgentHandlesTheBoundary()
    {
        var world = new BoundaryScenarioWorld();
        var environment = new SettingsSemanticCapabilityTestEnvironment(world);
        var traversal = new RuntimeTraversal(environment);
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

        var directive = new Directive(
            new TypeLevelTaskScope(App, RootPage),
            new TypeLevelEntryBoundary(App, RootPage),
            maximumDepth: 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new DirectiveStrategyRules(
                observation => new GoalEvidence(
                    string.Equals(observation.ForegroundApplication, App, StringComparison.Ordinal)
                        && observation.StructuredElements.Any(se => string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal)),
                    "Fresh final Root observation confirms bounded exploration completion.",
                    observation.SequenceNumber),
                CandidateAuthorizationEvaluator: ExternalBoundaryTests.AuthorizeEbd,
                BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory,
                ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew));

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        var ledger = new ExecutionHypothesisLedger(resolved, "recon-s2-run");
        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "recon-s2-run", CancellationToken.None, ledger);

        // The DFS truly encountered + handled the external boundary (auditable trace).
        Assert.True(agent.Trace.Any(t => t.Reason?.Contains("EXTERNAL_BOUNDARY_OBSERVED", StringComparison.Ordinal) is true),
            $"state={state}; reason={agent.Reason}; trace={string.Join(" || ", agent.Trace.Select(t => t.Reason ?? "(no reason)"))}");
        Assert.Contains(agent.Trace, t => t.Reason?.Contains("EXTERNAL_BOUNDARY_RETURNED_TO_PARENT", StringComparison.Ordinal) is true);

        // The reconciler classifies Revise: the hypothesis was contradicted by the
        // external boundary observation even though the Agent handled it.
        var decision = ledger.LatestDecision;
        Assert.NotNull(decision);
        Assert.Equal(RuntimeDecisionState.Revise, decision!.State);
        Assert.Contains("boundary", decision.DecisionReason, StringComparison.OrdinalIgnoreCase);

        // The RuntimeAgent does not decide the boundary action: the RunState is the
        // Agent's own DFS result (whatever its outcome — the boundary was handled and
        // the run continued under the Agent's authority), and the decision records the
        // contradiction only. The state is never produced by the reconciler.
        Assert.Equal(agent.State, state);
        Assert.NotEqual(RuntimeDecisionState.Continue, decision.State);

        // The hypothesis was revised with the boundary reason during the run.
        var revised = ledger.History.FirstOrDefault(h => h.Status == ExecutionHypothesisStatus.Revised);
        Assert.NotNull(revised);
        Assert.Contains("EXTERNAL_BOUNDARY_OBSERVED", revised!.RevisionReason, StringComparison.Ordinal);
    }
}
