using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Container;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// No-authority-escalation proof for the decision-driven hypothesis adaptation. The
/// HypothesisAdaptation is a passive record and the HypothesisAdapter is a stateless pure
/// function: neither exposes a method that authorizes, decides, completes, executes,
/// dispatches, creates a container, or initiates a sub-run. Replace does NOT execute
/// SystemBack or any DeviceAction; Escalate does NOT recover or retry. The RunState is
/// produced by the Agent's existing DFS engine, the GoalEvidence is evaluated by the
/// existing injected evaluator, and the Agent authorization path never references the
/// adaptation.
/// </summary>
public sealed class HypothesisAdaptationAuthorityTests
{
    [Fact]
    public void AdaptationAndAdapter_ExposeNoAuthorizingOrDecidingMethod()
    {
        var forbiddenNames = new[]
        {
            "Authorize", "Decide", "Complete", "Execute", "Dispatch", "Evaluate",
            "CreateContainer", "SubRun", "StartRun", "Apply", "Mutate", "Recover", "Retry",
        };

        AssertNoForbiddenPublicInstanceMethods(typeof(HypothesisAdaptation), forbiddenNames);
        AssertNoForbiddenPublicStaticMethods(typeof(HypothesisAdapter), forbiddenNames);
    }

    [Fact]
    public void Adapter_ExposesOnlyThePureAdaptEntry()
    {
        var methods = typeof(HypothesisAdapter)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(new[] { "Adapt" }, methods);
    }

    [Fact]
    public void AdaptationAndAdapter_ExposeNoAuthorizationOrCompletionEvidence()
    {
        Assert.DoesNotContain(typeof(HypothesisAdaptation).GetProperties(),
            property => typeof(CandidateAuthorizationEvidence).IsAssignableFrom(property.PropertyType)
                || typeof(GoalEvidence).IsAssignableFrom(property.PropertyType)
                || typeof(DeviceAction).IsAssignableFrom(property.PropertyType)
                || typeof(ObservedElement).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public void Replace_DoesNotExecuteSystemBackOrAnyDeviceAction()
    {
        var decision = new RuntimeDecision(
            "run-1",
            RuntimeDecisionState.Revise,
            "run-1",
            "contradicting or unknown world evidence",
            "External boundary observation contradicts the in-scope hypothesis expectation.");
        var hypothesis = new ExecutionHypothesis(
            runId: "run-1",
            directiveReference: "Application/Root",
            objective: "Explore declared scope within bounded depth",
            expectedTransition: "Discover -> Authorize -> Expand",
            expectedOutcome: "Exhaustive coverage within declared scope",
            confidence: 0.8f,
            revisionReason: null,
            createdAtObservation: null,
            status: ExecutionHypothesisStatus.Active);

        var adaptation = HypothesisAdapter.Adapt(decision, hypothesis);

        // The adaptation carries a hypothesis update only — no SystemBack / DeviceAction /
        // Tap anywhere in the record or its adapted hypothesis.
        Assert.Equal(HypothesisAdaptationType.Replace, adaptation.AdaptationType);
        Assert.DoesNotContain(typeof(HypothesisAdaptation).GetProperties(),
            property => typeof(DeviceAction).IsAssignableFrom(property.PropertyType));

        var text = string.Join(" ", new[]
        {
            adaptation.AdaptationReason,
            adaptation.AdaptedHypothesis.Objective,
            adaptation.AdaptedHypothesis.ExpectedTransition,
            adaptation.AdaptedHypothesis.ExpectedOutcome,
        });
        Assert.DoesNotContain("SystemBack", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceAction", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tap", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Navigate", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Escalate_DoesNotRecoverOrRetry()
    {
        var decision = new RuntimeDecision(
            "run-1",
            RuntimeDecisionState.Escalate,
            "run-1",
            "terminal authority-boundary failure",
            "Authority boundary exceeded: the run failed at an authority-boundary indicator.");
        var hypothesis = new ExecutionHypothesis(
            runId: "run-1",
            directiveReference: "Application/Root",
            objective: "Explore declared scope within bounded depth",
            expectedTransition: "Discover -> Authorize -> Expand",
            expectedOutcome: "Exhaustive coverage within declared scope",
            confidence: 0.8f,
            revisionReason: null,
            createdAtObservation: null,
            status: ExecutionHypothesisStatus.Active);

        var adaptation = HypothesisAdapter.Adapt(decision, hypothesis);

        // Records inability only: Revised + escalation reason. No recovery, retry, or
        // dispatch anywhere in the record or its adapted hypothesis.
        Assert.Equal(HypothesisAdaptationType.Escalate, adaptation.AdaptationType);
        Assert.Equal(ExecutionHypothesisStatus.Revised, adaptation.AdaptedHypothesis.Status);
        Assert.Contains("Escalation", adaptation.AdaptedHypothesis.RevisionReason, StringComparison.Ordinal);

        var text = string.Join(" ", new[]
        {
            adaptation.AdaptationReason,
            adaptation.AdaptedHypothesis.RevisionReason ?? string.Empty,
        });
        Assert.DoesNotContain("Recovery", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Retry", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatch", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemBack", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AdaptationAndAdapter_ExposeNoDispatchContainerOrSubRunMethod()
    {
        AssertNoForbiddenPublicInstanceMethods(typeof(HypothesisAdaptation), new[]
        {
            "Dispatch", "CreateContainer", "EnterContainer", "SubRun", "RunChild",
            "StartTraversal", "Navigate", "ExecuteStep",
        });
        AssertNoForbiddenPublicStaticMethods(typeof(HypothesisAdapter), new[]
        {
            "Dispatch", "CreateContainer", "EnterContainer", "SubRun", "RunChild",
            "StartTraversal", "Navigate", "ExecuteStep",
        });
    }

    [Fact]
    public async Task RunState_IsProducedByTheDfsEngine_NotByTheAdaptation()
    {
        var (agent, resolved, ledger, fixture) = await BuildRunAsync();

        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "ha-axis-run", CancellationToken.None, ledger);

        // The RunState is exactly the Agent's own DFS result; the adaptation only records.
        Assert.Equal(RunState.Completed, state);
        Assert.Equal(RunState.Completed, agent.State);

        // The integration ran the adaptation after Reconcile: a bounded Keep record that
        // reflects the Completed outcome but did not determine it.
        var adaptation = ledger.LatestAdaptation;
        Assert.NotNull(adaptation);
        Assert.Equal(HypothesisAdaptationType.Keep, adaptation!.AdaptationType);
        Assert.Equal(fixture.RunId, adaptation.RunId);
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, ledger.Current.Status);
    }

    [Fact]
    public async Task GoalEvidence_IsEvaluatedByTheExistingEvaluator_NotByTheAdaptation()
    {
        var goalSequences = new List<long>();
        var (agent, resolved, ledger, fixture) = await BuildRunAsync(goalSequences);

        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "ha-evidence-run", CancellationToken.None, ledger);

        // The injected evidence evaluator ran, so GoalEvidence is the existing
        // evaluator's result, never the adaptation's.
        Assert.NotEmpty(goalSequences);
        Assert.Equal(RunState.Completed, state);
        Assert.NotNull(ledger.LatestAdaptation);
        Assert.Equal(HypothesisAdaptationType.Keep, ledger.LatestAdaptation!.AdaptationType);
        Assert.False(string.IsNullOrWhiteSpace(fixture.RunId));
    }

    [Fact]
    public void AgentAuthorizationPath_DoesNotReferenceTheAdaptation()
    {
        // No Agent field may reference the adaptation or the adapter.
        var agentFields = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.DoesNotContain(agentFields,
            type => type == typeof(HypothesisAdaptation)
                || type == typeof(ExecutionHypothesisLedger));

        // No Agent source file may mention the adaptation or adapter: the Agent stays the
        // sole run-level semantic authority and never consults them.
        var agentSourceDir = TestRepositoryPaths.RepoPath("src", "UniClaw.Runtime", "Agent");
        var agentSources = Directory.GetFiles(agentSourceDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(agentSources);
        foreach (var path in agentSources)
        {
            var content = File.ReadAllText(path);
            Assert.DoesNotContain("HypothesisAdaptation", content, StringComparison.Ordinal);
            Assert.DoesNotContain("HypothesisAdapter", content, StringComparison.Ordinal);
        }
    }

    private static void AssertNoForbiddenPublicInstanceMethods(Type type, string[] forbiddenNames)
    {
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        foreach (var forbidden in forbiddenNames)
        {
            Assert.DoesNotContain(publicMethods,
                name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    private static void AssertNoForbiddenPublicStaticMethods(Type type, string[] forbiddenNames)
    {
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        foreach (var forbidden in forbiddenNames)
        {
            Assert.DoesNotContain(publicMethods,
                name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Builds a Fake U2 world agent + resolved decomposition + ledger, reusing the
    /// U2OpenWorldSettingsFixture. When <paramref name="goalSequences"/> is supplied, the
    /// evidence evaluator records the observation sequence it saw (proving the existing
    /// evaluator ran).
    /// </summary>
    private static async Task<(RuntimeAgent Agent, DirectiveDecompositionResult.Resolved, ExecutionHypothesisLedger, U2OpenWorldSettingsFixture)> BuildRunAsync(
        List<long>? goalSequences = null)
    {
        var fixture = U2OpenWorldSettingsFixture.Positive();
        var environment = fixture.Environment;
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", U2OpenWorldSettingsFixture.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            U2OpenWorldSettingsFixture.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);

        var directive = new Directive(
            new TypeLevelTaskScope("Settings", U2OpenWorldSettingsFixture.RootPage),
            new TypeLevelEntryBoundary("Settings", U2OpenWorldSettingsFixture.RootPage),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new DirectiveStrategyRules(
                observation =>
                {
                    goalSequences?.Add(observation.SequenceNumber);
                    return new GoalEvidence(
                        string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), U2OpenWorldSettingsFixture.RootPage, StringComparison.Ordinal),
                        "Fresh root GoalEvidence is satisfied only after Agent derives bounded traversal completion.",
                        observation.SequenceNumber);
                },
                U2OpenWorldSettingsFixture.EvaluateAuthorization,
                BranchInventoryEvaluator: U2OpenWorldSettingsFixture.EvaluateInventory));

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        var ledger = new ExecutionHypothesisLedger(resolved, fixture.RunId);
        return (agent, resolved, ledger, fixture);
    }
}