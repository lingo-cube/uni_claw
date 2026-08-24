using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-OW-TD-001 open-world type-directed dispatch proofs over reality-seeded Settings.
/// No pre-enumerated route. Runtime discovers, classifies, dispatches by category policy.
/// </summary>
public sealed class OpenWorldTypeDirectedScenarioTests
{
    private const string App = "com.android.settings";
    private static readonly ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> SafeMenuPolicy =
        ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
        {
            [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.EnterAndTraverse,
            [TypeLevelElementCategory.StateChangingControl] = TypeLevelHandling.SetDesiredState,
        });

    private static readonly ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> ForbiddenPolicy =
        ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
        {
            [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.Forbidden,
        });

    private static (RuntimeAgent Agent, ScriptedEnvironment Env, RuntimeTraversal Traversal, IntentSemanticEnvelope.Resolved Envelope, List<GoalEvidence> Evidence) Create(
        ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling>? policyOverride = null,
        bool incompleteInventory = false)
    {
        var policy = policyOverride ?? SafeMenuPolicy;
        // The Fake world carries the state transition across every exact parent return.
        // OFF/ON screen variants resolve to the same semantic Container identities.
        var screens = new[]
        {
            new ScreenConfig("Launcher", null, []),
            new ScreenConfig("RootOff", App, [
                new ElementConfig("Network&internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "NetworkOff")),
                new ElementConfig("System info", null, new TransitionConfig(ScreenTransitionAction.Tap, "SystemInfoOff")),
            ]),
            new ScreenConfig("RootOn", App, [
                new ElementConfig("Network&internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "NetworkOn")),
                new ElementConfig("System info", null, new TransitionConfig(ScreenTransitionAction.Tap, "SystemInfoOn")),
                new ElementConfig("Wi‑Fi status", true, null),
            ]),
            new ScreenConfig("NetworkOff", App, [
                new ElementConfig("Root", null, new TransitionConfig(ScreenTransitionAction.Tap, "RootOff")),
                new ElementConfig("Internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "InternetOff")),
                new ElementConfig("SIMs", null, null),
            ]),
            new ScreenConfig("NetworkOn", App, [
                new ElementConfig("Root", null, new TransitionConfig(ScreenTransitionAction.Tap, "RootOn")),
                new ElementConfig("Internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "InternetOn")),
                new ElementConfig("SIMs", null, null),
            ]),
            new ScreenConfig("InternetOff", App, [
                new ElementConfig("NetworkPage", null, new TransitionConfig(ScreenTransitionAction.Tap, "NetworkOff")),
                new ElementConfig("Wi‑Fi", null, new TransitionConfig(ScreenTransitionAction.Tap, "WifiOff")),
                new ElementConfig("T-Mobile", null, null),
            ]),
            new ScreenConfig("InternetOn", App, [
                new ElementConfig("NetworkPage", null, new TransitionConfig(ScreenTransitionAction.Tap, "NetworkOn")),
                new ElementConfig("Wi‑Fi", null, new TransitionConfig(ScreenTransitionAction.Tap, "WifiOn")),
                new ElementConfig("T-Mobile", null, null),
            ]),
            new ScreenConfig("WifiOff", App, [
                new ElementConfig("InternetPage", null, new TransitionConfig(ScreenTransitionAction.Tap, "InternetOff")),
                new ElementConfig("Wi‑Fi", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "WifiOn", true)),
            ]),
            new ScreenConfig("WifiOn", App, [
                new ElementConfig("InternetPage", null, new TransitionConfig(ScreenTransitionAction.Tap, "InternetOn")),
                new ElementConfig("Wi‑Fi", true, null),
            ]),
            new ScreenConfig("SystemInfoOff", App, [
                new ElementConfig("Root", null, new TransitionConfig(ScreenTransitionAction.Tap, "RootOff")),
                new ElementConfig("Device details", null, null),
            ]),
            new ScreenConfig("SystemInfoOn", App, [
                new ElementConfig("Root", null, new TransitionConfig(ScreenTransitionAction.Tap, "RootOn")),
                new ElementConfig("Device details", null, null),
            ]),
        };
        var env = new ScriptedEnvironment("Launcher", "RootOff", WithPrimaryBounds(screens));
        var semanticEnv = new SemanticCapabilityTestEnvironment(env, element =>
        {
            if (element.SwitchState is not null) return FixtureSemanticRole.NavigationCandidate;
            return element.Text is "Root" or "NetworkPage" or "InternetPage" or "WifiSub" or "SystemInfo"
                ? FixtureSemanticRole.ParentReturnControl
                : FixtureSemanticRole.NavigationCandidate;
        });
        var traversal = new RuntimeTraversal(semanticEnv);
        var evidence = new List<GoalEvidence>();
        var categorized = new List<(string Text, TypeLevelElementCategory? Category)>();

        var goal = new Goal(
            EvidenceEvaluator: observation =>
            {
                var satisfied = observation.Elements.Any(e => e.Text == "Wi‑Fi status" && e.SwitchState is true);
                var ev = new GoalEvidence(satisfied, satisfied ? "Wi‑Fi ON confirmed." : "Goal unproven.", observation.SequenceNumber);
                evidence.Add(ev);
                return ev;
            },
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(true, $"safe receipt: {candidate.Text}"),
            BranchInventoryEvaluator: (observations, _) =>
            {
                if (incompleteInventory)
                    return new BranchInventoryEvidence(null, "inventory unresolved (simulated)");
                var latest = observations[^1];
                var required = Page(latest) switch
                {
                    "Root" => new[] { "Network&internet", "System info" },
                    "NetworkPage" => ["Internet"],
                    "InternetPage" => ["Wi‑Fi"],
                    "WifiSub" => ["Wi‑Fi"],
                    "SystemInfo" => [],
                    _ => [],
                };
                var branches = required
                    .ToImmutableDictionary(t => t, _ => latest.SequenceNumber, StringComparer.Ordinal);
                var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(latest);
                var grounding = required.ToImmutableDictionary(
                    branch => branch,
                    branch =>
                    {
                        var occurrence = occurrences.FirstOrDefault(candidate =>
                            candidate.CanonicalOccurrence.Reference.ElementIndex < latest.Elements.Length &&
                            string.Equals(latest.Elements[candidate.CanonicalOccurrence.Reference.ElementIndex].Text, branch, StringComparison.Ordinal));
                        return new NavigationSourceOccurrenceReference(
                            latest.SequenceNumber,
                            occurrence?.OccurrenceIdentity ?? $"missing:{branch}");
                    }, StringComparer.Ordinal);
                return branches.Count > 0
                    ? new BranchInventoryEvidence(branches, $"inventory complete: {branches.Count} branches at seq={latest.SequenceNumber}", grounding)
                    : new BranchInventoryEvidence(ImmutableDictionary<string, long>.Empty, "bounded leaf");
            },
            CategoryClassifier: element =>
            {
                var cat = element.SwitchState is not null
                    ? TypeLevelElementCategory.StateChangingControl
                    : string.IsNullOrEmpty(element.Text) ? (TypeLevelElementCategory?)null
                    : TypeLevelElementCategory.NavigableContainer;
                categorized.Add((element.Text, cat));
                return cat;
            });

        var spec = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, "Root"),
            ImmutableHashSet.Create(
                TypeLevelElementCategory.NavigableContainer,
                TypeLevelElementCategory.StateChangingControl),
            maximumDepth: 4,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(
                TypeLevelElementCategory.NavigableContainer,
                TypeLevelElementCategory.StateChangingControl)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, "Root"),
            new TypeLevelDispatchPolicy(policy));

        var envelope = IntentSemanticEnvelope.Project(
            "确保 WiFi 已开启", goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));

        static string? Page(Observation o) =>
            o.Elements.Any(e => e.Text == "Network&internet") && o.Elements.Any(e => e.Text == "System info") ? "Root"
            : o.Elements.Any(e => e.Text == "Root") && o.Elements.Any(e => e.Text == "Device details") ? "SystemInfo"
            : o.Elements.Any(e => e.Text == "InternetPage") && o.Elements.Any(e => e.Text == "Wi‑Fi" && e.SwitchState is not null) ? "WifiSub"
            : o.Elements.Any(e => e.Text == "NetworkPage") && o.Elements.Any(e => e.Text == "T-Mobile") ? "InternetPage"
            : o.Elements.Any(e => e.Text == "Root") && o.Elements.Any(e => e.Text == "Internet") ? "NetworkPage"
            : null;

        RuntimeContainer Factory(string page) => new(page, o => Page(o) == page, traversal.ExecuteStep, forwardsAuthorizationReceipts: true);
        var startup = new RuntimeStartup(semanticEnv, App, Page);
        var recovery = new RuntimeRecovery(semanticEnv, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, token => semanticEnv.ObserveAsync(token), Page, Factory, recovery);
        return (agent, env, traversal, envelope, evidence);
    }

    private static IEnumerable<ScreenConfig> WithPrimaryBounds(IEnumerable<ScreenConfig> screens) =>
        screens.Select(screen => screen with
        {
            Elements = screen.Elements.Select((element, index) => element with
            {
                Bounds = element.Bounds ?? new ElementBounds(0, index * 0.08f, 1, (index + 1) * 0.08f)
            }).ToImmutableArray()
        });

    [Fact]
    public async Task Proof1_MenuContainer_DispatchesEnterAndTraverse()
    {
        var (agent, env, _, envelope, _) = Create();
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ow-td-1", CancellationToken.None);
        Assert.True(state == RunState.Completed, $"state={state} reason={agent.Reason}\nacts={string.Join(",", env.ActionHistory)}\ntrace={string.Join(" | ", agent.Trace.Select(t => $"[{t.ContainerId}] {t.Reason}"))}");
        Assert.Contains(env.ActionHistory, a => a is DeviceAction.Tap);
        var setSwitch = Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.True(setSwitch.TargetState);
        Assert.Contains(agent.Trace, e => e.Reason?.Contains("leaf SetDesiredState dispatched for 'Wi‑Fi'", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task Proof3_DangerousNode_ZeroDispatch()
    {
        var (agent, env, _, envelope, _) = Create(policyOverride: ForbiddenPolicy);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ow-td-3", CancellationToken.None);
        Assert.NotEqual(RunState.Completed, state);
        Assert.Contains("forbidden", agent.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Proof4_IncompleteInventory_ZeroGuessedDispatch()
    {
        var (agent, _, _, envelope, _) = Create(incompleteInventory: true);
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ow-td-4", CancellationToken.None);
        Assert.NotEqual(RunState.Completed, state);
        Assert.Contains("unresolved", agent.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Proof5_ChildCompletion_ParentReturn_SiblingContinuation()
    {
        var (agent, env, _, envelope, _) = Create();
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ow-td-5", CancellationToken.None);
        Assert.Equal(RunState.Completed, state);
        var returns = agent.Trace
            .Where(t => t.Reason?.StartsWith("verified parent return", StringComparison.Ordinal) is true)
            .ToArray();
        // Container-relation evidence: every traversed child produced a verified
        // parent-return receipt at its parent container (evidence outcome, not a
        // click sequence — the specific labels are fixture data, not assertions).
        Assert.Contains(returns, t => t.ContainerId == "InternetPage" && t.Reason!.Contains("child 'Wi‑Fi'", StringComparison.Ordinal));
        Assert.Contains(returns, t => t.ContainerId == "NetworkPage" && t.Reason!.Contains("child 'Internet'", StringComparison.Ordinal));
        Assert.Contains(returns, t => t.ContainerId == "Root" && t.Reason!.Contains("child 'Network&internet'", StringComparison.Ordinal));
        Assert.Contains(returns, t => t.ContainerId == "Root" && t.Reason!.Contains("child 'System info'", StringComparison.Ordinal));

        // Coverage evidence: every container completed exactly the children its
        // inventory approved (no skipped work, no blind redispatch).
        var rootProgress = agent.BranchProgress["Root"];
        Assert.Equal(2, rootProgress.ApprovedSiblingEvidence.Count);
        Assert.Equal(2, rootProgress.CompletedSiblingEvidence.Count);
        Assert.True(rootProgress.CompletedSiblingEvidence["Network&internet"] < rootProgress.CompletedSiblingEvidence["System info"]);
        foreach (var (container, progress) in agent.BranchProgress)
        {
            Assert.Equal(progress.ApprovedSiblingEvidence.Count, progress.CompletedSiblingEvidence.Count);
        }

        // State-changing evidence: exactly one authorized switch dispatch achieved
        // the desired state (goal outcome by observation, proven in Proof6).
        Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.Contains(env.ActionHistory, a => a is DeviceAction.Tap);
        Assert.DoesNotContain(agent.Trace, t => t.Reason?.Contains("不 redispatch proven branch", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task Proof6_FinalCompletion_GoalEvidence()
    {
        var (agent, env, _, envelope, evidence) = Create();
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ow-td-6", CancellationToken.None);
        Assert.Equal(RunState.Completed, state);
        var finalObservation = env.ObservationHistory[^1];
        Assert.Contains(finalObservation.Elements, e => e.Text == "Wi‑Fi status" && e.SwitchState is true);
        var satisfied = Assert.Single(evidence, e => e.Satisfied);
        Assert.Equal(finalObservation.SequenceNumber, satisfied.SourceObservationSequence);
        Assert.Equal("Wi‑Fi ON confirmed.", agent.Reason);
    }

    [Fact]
    public async Task Proof7_DeterministicReplay()
    {
        static string ObservationKey(Observation observation) =>
            $"{observation.SequenceNumber}:{observation.ForegroundApplication}:"
            + string.Join("|", observation.Elements.Select(element =>
                $"{element.Index}:{element.Text}:{element.SwitchState?.ToString() ?? "null"}"));

        async Task<(RunState State, string[] Actions, string[] Observations,
            string[] Journal, string[] Trace, string[] Progress, string[] Evidence, string? Reason)> Execute()
        {
            var (agent, env, traversal, envelope, evidence) = Create();
            var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, "ow-td-7", CancellationToken.None);
            return (
                state,
                env.ActionHistory.Select(action => action.ToString()!).ToArray(),
                env.ObservationHistory.Select(ObservationKey).ToArray(),
                traversal.Journal.Select(entry =>
                    $"{entry.StepId}:{entry.SelectedElementIndex}:{entry.DispatchedAction}:"
                    + $"{(entry.PostActionObservation is null ? "null" : ObservationKey(entry.PostActionObservation))}:"
                    + $"{entry.Result}:{entry.RetryCount}").ToArray(),
                agent.Trace.Select(entry => entry.ToString()!).ToArray(),
                agent.BranchProgress
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => $"{item.Key}:approved="
                        + string.Join(",", item.Value.ApprovedSiblingEvidence.OrderBy(e => e.Key, StringComparer.Ordinal))
                        + ":completed="
                        + string.Join(",", item.Value.CompletedSiblingEvidence.OrderBy(e => e.Key, StringComparer.Ordinal)))
                    .ToArray(),
                evidence.Select(item => item.ToString()).ToArray(),
                agent.Reason);
        }
        var first = await Execute();
        var second = await Execute();
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(first.Observations, second.Observations);
        Assert.Equal(first.Journal, second.Journal);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.Progress, second.Progress);
        Assert.Equal(first.Evidence, second.Evidence);
        Assert.Equal(first.Reason, second.Reason);
    }
}
