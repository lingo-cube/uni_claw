using System.Collections.Immutable;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class ActiveContainerContextStageBTests
{
    [Fact]
    public void ActivePathIsAnOrderedExecutionChainAndNotAVisitedHistory()
    {
        var root = new UniClaw.Runtime.Container.Container(
            "SettingsRoot", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());
        var child = new UniClaw.Runtime.Container.Container(
            "Display", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());
        var grandchild = new UniClaw.Runtime.Container.Container(
            "Advanced", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());

        var context = ActiveContainerContext.Create(root)
            .EnterChild(child, "display-obligation")
            .EnterChild(grandchild, "advanced-obligation");

        Assert.Equal(
            ["SettingsRoot", "Display"],
            context.ActiveAncestorPath.Select(path => path.ParentExecutionContainer.SemanticPageName));
        Assert.Equal("advanced-obligation", context.ActiveAncestorPath[^1].EnteredChildObligationIdentity);
        Assert.True(context.ContainsSemanticIdentity("SettingsRoot"));
        Assert.True(context.ContainsSemanticIdentity("Display"));
        Assert.True(context.ContainsSemanticIdentity("Advanced"));
        Assert.False(context.ContainsSemanticIdentity("Sibling"));
    }

    [Fact]
    public async Task OpenWorldProjectionTracksNestedReturnAndAuthorizedSibling()
    {
        var run = CreateGraphRun(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Root"] = ["Child", "Sibling"],
                ["Child"] = ["Grandchild"],
                ["Grandchild"] = [],
                ["Sibling"] = [],
            },
            [
                Page("Root", Nav("Child", "Child"), Nav("Sibling", "Sibling")),
                Page("Child", Nav("Grandchild", "Grandchild"), ReturnTo("Root", "Root")),
                Page("Grandchild", ReturnTo("Child", "Child")),
                Page("Sibling", ReturnTo("Root", "Root")),
            ]);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, "stage-b-context-positive", CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(
            ["Root", "Child", "Grandchild", "Child", "Root", "Sibling", "Root"],
            run.InventorySamples.Select(sample => sample.Page));
        AssertContext(run.InventorySamples[0], "Root");
        AssertContext(run.InventorySamples[1], "Child", "Root");
        AssertContext(run.InventorySamples[2], "Grandchild", "Root", "Child");
        AssertContext(run.InventorySamples[3], "Child", "Root");
        AssertContext(run.InventorySamples[4], "Root");
        AssertContext(run.InventorySamples[5], "Sibling", "Root");
        AssertContext(run.InventorySamples[6], "Root");

        var childReturn = run.AuthorizationSamples.FindIndex(sample =>
            sample.Candidate == "Root" && sample.Execution == "Child");
        Assert.True(childReturn >= 0, "The child parent-return control was not authorized.");
        var siblingAuthorization = run.AuthorizationSamples.FindIndex(childReturn + 1, sample =>
            sample.Candidate == "Sibling" && sample.Execution == "Root");
        Assert.True(siblingAuthorization > childReturn, "Sibling authorization must follow the verified return.");
        AssertContext(run.InventorySamples[5], "Sibling", "Root");
    }

    [Fact]
    public async Task OpenWorldCycleRejectionPreservesChildPathAndDispatchesNoCycleChild()
    {
        var run = CreateGraphRun(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["A"] = ["B"],
                ["B"] = ["A"],
            },
            [
                Page("A", Nav("B", "B")),
                Page("B", Nav("A", "A")),
            ],
            root: "A");

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, "stage-b-context-cycle", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Contains("cycle", run.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Equal("B", run.Agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal(["A"], run.Agent.ContainerContext.ActiveAncestorPath);
        Assert.Contains(run.InventorySamples, sample =>
            sample.Page == "B" && sample.Execution == "B" && sample.Path.SequenceEqual(["A"]));
    }

    [Fact]
    public async Task OpenWorldWrongExactParentFailureKeepsChildPathWithoutHalfPop()
    {
        var run = CreateGraphRun(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Root"] = ["Child"],
                ["Child"] = [],
                ["WrongRoot"] = [],
            },
            [
                Page("Root", Nav("Child", "Child")),
                Page("Child", ReturnTo("Root", "WrongRoot")),
                Page("WrongRoot"),
            ]);

        var state = await IntentExecution.RunOpenWorldAsync(
            run.Agent, run.Envelope, "stage-b-context-wrong-parent", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Equal("Child", run.Agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal(["Root"], run.Agent.ContainerContext.ActiveAncestorPath);
        Assert.DoesNotContain(run.Agent.Trace, entry =>
            entry.ContainerTransition?.Kind == ContainerTransitionKind.VERIFIED_RETURN_TO_ACTIVE_PARENT);
    }

    [Fact]
    public async Task OpenWorldCancellationAfterChildCommitPreservesChildPath()
    {
        using var cancellation = new CancellationTokenSource();
        var run = CreateGraphRun(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Root"] = ["Child"],
                ["Child"] = [],
            },
            [
                Page("Root", Nav("Child", "Child")),
                Page("Child", ReturnTo("Root", "Root")),
            ],
            onInventorySample: sample =>
            {
                if (sample.Page == "Child")
                    cancellation.Cancel();
            });

        await Assert.ThrowsAsync<OperationCanceledException>(() => IntentExecution.RunOpenWorldAsync(
            run.Agent,
            run.Envelope,
            "stage-b-context-cancelled-child",
            cancellation.Token));

        Assert.Equal("Child", run.Agent.ContainerContext.ActiveExecutionContainer);
        Assert.Equal(["Root"], run.Agent.ContainerContext.ActiveAncestorPath);
        Assert.Contains(run.InventorySamples, sample =>
            sample.Page == "Child" && sample.Execution == "Child" && sample.Path.SequenceEqual(["Root"]));
    }

    [Fact]
    public async Task ExistingPlanRecoveryPathReadsKnownEmptyRootProjection()
    {
        var environment = new RecoveryScriptEnvironment(
            [
                Observation("Settings", "ProbeTarget", "WiFi"),
                Observation("Settings", "ProbeTarget", "WiFi"),
                Observation("Settings", "ProbeTarget", "WiFi"),
                Observation("Launcher"),
                Observation("Settings", "ProbeTarget", "WiFi"),
                Observation("Settings", "ProbeTarget", "WiFi"),
                Observation("Settings", "WiFi"),
            ]);
        var startup = new RuntimeStartup(
            environment,
            "Settings",
            ResolveProbePage,
            restoreRecipe: "Relaunch(Settings)",
            entryStrategy: "Resolve(ProbeEntry)");
        var traversal = new RuntimeTraversal(environment);
        RuntimeAgent? agent = null;
        ContextSample? recoveryContext = null;
        var recovery = new RuntimeRecovery(
            environment,
            recipe => string.Equals(recipe, "Relaunch(Settings)", StringComparison.Ordinal)
                ? [new DeviceAction.LaunchApp("Settings")]
                : [],
            (step, observation) =>
            {
                if (agent is not null)
                {
                    var context = agent.ContainerContext;
                    recoveryContext = new ContextSample(
                        step.TargetDescription,
                        context.ActiveExecutionContainer,
                        context.ActiveAncestorPath);
                }

                var target = observation.Elements.FirstOrDefault(element =>
                    string.Equals(element.Text, step.TargetDescription, StringComparison.Ordinal));
                return target is null ? null : new DeviceAction.Tap(target.Index);
            },
            (observation, criteria) =>
                string.Equals(criteria, "ForegroundApplication == Settings", StringComparison.Ordinal)
                && string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal));
        var goal = new Goal(observation => new GoalEvidence(
            string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal)
            && !observation.Elements.Any(element => element.Text == "ProbeTarget"),
            "probe recovery complete",
            observation.SequenceNumber));
        agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolveProbePage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(ResolveProbePage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);

        var state = await agent.RunAsync(
            goal,
            new Plan([new PlanStep("ProbeTarget", "Tap"), new PlanStep("WiFi", "Tap")]),
            "stage-b-context-plan-recovery",
            CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        Assert.NotNull(recoveryContext);
        Assert.Equal("ProbeEntry", recoveryContext!.Execution);
        Assert.Empty(recoveryContext.Path);
        Assert.Equal("ProbeEntry", agent.ContainerContext.ActiveExecutionContainer);
        Assert.Empty(agent.ContainerContext.ActiveAncestorPath);
    }

    [Fact]
    public async Task ExistingPlanRecoveryVerificationFailurePreservesKnownEmptyRootProjection()
    {
        var environment = new RecoveryScriptEnvironment(
            [
                Observation("Settings", "ProbeTarget"),
                Observation("Settings", "ProbeTarget"),
                Observation("Launcher"),
                Observation("Launcher"),
            ]);
        var startup = new RuntimeStartup(
            environment,
            "Settings",
            ResolveProbePage,
            restoreRecipe: "Relaunch(Settings)",
            entryStrategy: "Resolve(ProbeEntry)");
        var traversal = new RuntimeTraversal(environment);
        var recovery = new RuntimeRecovery(
            environment,
            recipe => string.Equals(recipe, "Relaunch(Settings)", StringComparison.Ordinal)
                ? [new DeviceAction.LaunchApp("Settings")]
                : [],
            (_, _) => null,
            (observation, criteria) =>
                string.Equals(criteria, "ForegroundApplication == Settings", StringComparison.Ordinal)
                && string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal));
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolveProbePage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(ResolveProbePage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);

        var state = await agent.RunAsync(
            new Goal(_ => new GoalEvidence(false, "probe recovery must fail", null)),
            new Plan([new PlanStep("ProbeTarget", "Tap")]),
            "stage-b-context-recovery-failure",
            CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Equal("ProbeEntry", agent.ContainerContext.ActiveExecutionContainer);
        Assert.False(agent.ContainerContext.ActiveAncestorPath.IsDefault);
        Assert.Empty(agent.ContainerContext.ActiveAncestorPath);
        Assert.DoesNotContain(agent.Trace, entry =>
            entry.Reason?.Contains("recovery resume", StringComparison.Ordinal) is true);
    }

    private static GraphRun CreateGraphRun(
        IReadOnlyDictionary<string, string[]> inventory,
        IEnumerable<ScreenConfig> screens,
        string root = "Root",
        Action<ContextSample>? onInventorySample = null)
    {
        var raw = new ScriptedEnvironment(
            root,
            root,
            WithPrimaryBounds(screens));
        var parents = inventory.Keys
            .Where(page => !string.Equals(page, root, StringComparison.Ordinal))
            .ToDictionary(page => page, _ => string.Empty, StringComparer.Ordinal);
        foreach (var (page, branches) in inventory)
        {
            foreach (var branch in branches)
            {
                if (parents.ContainsKey(branch) && string.IsNullOrEmpty(parents[branch]))
                    parents[branch] = page;
            }
        }

        var semanticEnvironment = new SemanticCapabilityTestEnvironment(
            raw,
            (observation, element, _) =>
            {
                var page = Resolve(observation);
                if (page is not null
                    && parents.TryGetValue(page, out var parent)
                    && string.Equals(element.Text, parent, StringComparison.Ordinal))
                    return FixtureSemanticRole.ParentReturnControl;
                return element.Text is { } text && !text.StartsWith("@", StringComparison.Ordinal)
                    ? FixtureSemanticRole.NavigationCandidate
                    : null;
            });
        var traversal = new RuntimeTraversal(semanticEnvironment);
        var startup = new RuntimeStartup(semanticEnvironment, "settings", Resolve);
        var recovery = new RuntimeRecovery(semanticEnvironment, _ => [], (_, _) => null, (_, _) => true);
        RuntimeAgent? agent = null;
        var inventorySamples = new List<ContextSample>();
        var authorizationSamples = new List<AuthorizationSample>();
        var goal = new Goal(
            EvidenceEvaluator: observation => new GoalEvidence(
                string.Equals(Resolve(observation), root, StringComparison.Ordinal),
                "root traversal complete",
                observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (_, candidate) =>
            {
                if (agent is not null)
                {
                    var context = agent.ContainerContext;
                    authorizationSamples.Add(new AuthorizationSample(
                        candidate.Text,
                        context.ActiveExecutionContainer,
                        context.ActiveAncestorPath));
                }
                return new CandidateAuthorizationEvidence(true, "authorized fixture navigation");
            },
            BranchInventoryEvaluator: (observations, _) =>
            {
                if (agent is null)
                    throw new InvalidOperationException("Agent must be assigned before Run.");
                var context = agent.ContainerContext;
                var latest = observations[^1];
                var page = Resolve(latest)
                    ?? throw new InvalidOperationException("Fixture page was not resolved.");
                inventorySamples.Add(new ContextSample(
                    page,
                    context.ActiveExecutionContainer,
                    context.ActiveAncestorPath));
                onInventorySample?.Invoke(inventorySamples[^1]);
                var branches = inventory[page];
                var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(latest);
                var grounding = branches.ToImmutableDictionary(
                    branch => branch,
                    branch =>
                    {
                        var occurrence = occurrences.FirstOrDefault(candidate =>
                            candidate.CanonicalOccurrence.Reference.ElementIndex < latest.Elements.Length
                            && string.Equals(
                                latest.Elements[candidate.CanonicalOccurrence.Reference.ElementIndex].Text,
                                branch,
                                StringComparison.Ordinal));
                        return new NavigationSourceOccurrenceReference(
                            latest.SequenceNumber,
                            occurrence?.OccurrenceIdentity ?? $"missing:{branch}");
                    },
                    StringComparer.Ordinal);
                return new BranchInventoryEvidence(
                    branches.ToImmutableDictionary(branch => branch, _ => latest.SequenceNumber, StringComparer.Ordinal),
                    $"fixture inventory for {page}",
                    grounding);
            });
        var policy = ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
        {
            [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.EnterAndTraverse,
        });
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope("settings", root),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 4,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("settings", root),
            new TypeLevelDispatchPolicy(policy));
        var envelope = IntentSemanticEnvelope.Project(
            "stage-b active context fixture",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => semanticEnvironment.ObserveAsync(cancellationToken),
            Resolve,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(Resolve(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep,
                forwardsAuthorizationReceipts: true),
            recovery);
        return new GraphRun(agent, semanticEnvironment, envelope, inventorySamples, authorizationSamples);
    }

    private static ScreenConfig Page(string page, params ElementConfig[] elements)
        => new(page, "settings", [new ElementConfig($"@{page}", null, null, null, "text"), .. elements]);

    private static ElementConfig Nav(string text, string next)
        => new(text, null, new TransitionConfig(ScreenTransitionAction.Tap, next), null, "menuItem");

    private static ElementConfig ReturnTo(string text, string next)
        => new(text, null, new TransitionConfig(ScreenTransitionAction.Tap, next), null, "menuItem");

    private static IEnumerable<ScreenConfig> WithPrimaryBounds(IEnumerable<ScreenConfig> screens)
        => screens.Select(screen => screen with
        {
            Elements = screen.Elements.Select((element, index) => element with
            {
                Bounds = element.Bounds ?? new ElementBounds(0, index * 0.1f, 1, (index + 1) * 0.1f),
            }).ToImmutableArray(),
        });

    private static string? Resolve(Observation observation)
        => observation.Elements
            .Select(element => element.Text)
            .FirstOrDefault(text => text.StartsWith("@", StringComparison.Ordinal))?[1..];

    private static string? ResolveProbePage(Observation observation)
        => string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal)
            ? "ProbeEntry"
            : null;

    private static Observation Observation(string foreground, params string[] elements)
        => new(
            elements.Select((text, index) => new ObservedElement(text, null, index)).ToImmutableArray(),
            foreground,
            0);

    private static void AssertContext(ContextSample sample, string execution, params string[] path)
    {
        Assert.Equal(execution, sample.Execution);
        Assert.Equal(path, sample.Path);
    }

    private sealed record GraphRun(
        RuntimeAgent Agent,
        SemanticCapabilityTestEnvironment Environment,
        IntentSemanticEnvelope.Resolved Envelope,
        List<ContextSample> InventorySamples,
        List<AuthorizationSample> AuthorizationSamples);

    private sealed record ContextSample(
        string Page,
        string? Execution,
        ImmutableArray<string> Path);

    private sealed record AuthorizationSample(
        string Candidate,
        string? Execution,
        ImmutableArray<string> Path);

    private sealed class RecoveryScriptEnvironment(IReadOnlyList<Observation> script) : IEnvironment
    {
        private readonly IReadOnlyList<Observation> _script = script;
        private readonly List<DeviceAction> _actions = [];
        private int _index;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var template = _script[Math.Min(_index++, _script.Count - 1)];
            return Task.FromResult(template with { SequenceNumber = _index });
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _actions.Add(action);
            return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, action.ToString(), "recovery fixture dispatched"));
        }
    }
}
