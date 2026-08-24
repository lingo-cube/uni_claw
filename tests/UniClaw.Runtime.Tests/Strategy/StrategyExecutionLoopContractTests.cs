using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Strategy;

public sealed class StrategyExecutionLoopContractTests
{
    private const string RunId = "run-strategy-loop";

    [Fact]
    public void AcceptedIntentCreatesExactlyOneSessionWithH0AndN0()
    {
        var intent = AcceptedIntent();
        var session = new StrategyExecutionReasoningSession(intent, RunId);

        Assert.Equal(RunId, session.RunId);
        Assert.Single(session.AcceptedHistory);
        Assert.Equal("reasoning-0", session.AcceptedReasoningRevisionReference);
        Assert.Equal(ExecutionHypothesisStatus.Created, session.CurrentHypothesis.Status);
    }

    [Fact]
    public async Task SupportedEvidenceProducesPassiveProposalAndCommitsOnlyAfterAcceptance()
    {
        var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
        var proposal = await session.EvaluateAsync(Snapshot(session, View()), CancellationToken.None);

        Assert.Equal(PreTerminalContinuationKind.ContinuationSupported, proposal.Kind);
        Assert.Single(session.AcceptedHistory);
        Assert.True(session.TryCommit(proposal));
        Assert.Equal("reasoning-1", session.AcceptedReasoningRevisionReference);
        Assert.Equal(2, session.AcceptedHistory.Count);
    }

    [Fact]
    public async Task PermittedRevisionRemainsTentativeUntilCommit()
    {
        var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
        var proposal = await session.EvaluateAsync(Snapshot(session, View(contradiction: true)), CancellationToken.None);

        Assert.Equal(PreTerminalContinuationKind.ContinuationSupportedAfterRevision, proposal.Kind);
        Assert.Single(session.AcceptedHistory);
        Assert.True(session.TryCommit(proposal));
        Assert.Equal(2, session.AcceptedHistory.Count);
        Assert.NotNull(session.AcceptedHistory[1].Adaptation);
    }

    [Fact]
    public async Task OutOfBoundaryRevisionIsPassiveNotSupported()
    {
        var strategy = StrategyTestSupport.Explore(adaptations:
            System.Collections.Immutable.ImmutableHashSet.Create(StrategyAdaptationKind.ReconcileBelief));
        var result = new StrategyContractCompiler([new StrategyTestSupport.TestBinding(ExplorationIntent.ExhaustiveWithinScope, true)])
            .Compile(strategy);
        var intent = Assert.IsType<StrategyCompilationResult.Accepted>(result).Intent;
        var session = new StrategyExecutionReasoningSession(intent, RunId);
        var proposal = await session.EvaluateAsync(Snapshot(session, View(contradiction: true)), CancellationToken.None);

        Assert.Equal(PreTerminalContinuationKind.ContinuationNotSupported, proposal.Kind);
        Assert.Single(session.AcceptedHistory);
    }

    [Fact]
    public async Task CorrelationMismatchAndDuplicateCommitDoNotMutateHistory()
    {
        var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
        var snapshot = Snapshot(session, View());
        var proposal = await session.EvaluateAsync(snapshot, CancellationToken.None);
        var wrongRun = new PreTerminalContinuationProposal(
            "other-run", proposal.CycleSequence, proposal.AcceptedObservationSequence, proposal.BeliefRevision,
            proposal.TraceDigest, proposal.BaseReasoningRevisionReference, proposal.ProposedReasoningRevisionReference,
            proposal.Kind, proposal.SupportingEvidenceReferences, proposal.EvaluatedAt,
            proposal.StrategyExecutionId, proposal.RuntimeExecutionIntentReference, proposal.EvidenceViewDigest);
        Assert.False(session.TryCommit(wrongRun));
        Assert.Single(session.AcceptedHistory);
        Assert.True(session.TryCommit(proposal));
        Assert.False(session.TryCommit(proposal));
        Assert.Equal(2, session.AcceptedHistory.Count);
    }

    [Fact]
    public void EvidenceDigestIsDeterministicAndContentSensitive()
    {
        var a = View();
        var b = View();
        var c = new StrategyExecutionEvidenceView(RunId, "strategy-explore-1", 1, 1, "belief", 1,
            [new StrategyStructuralProgressFact(StrategyStructuralProgressKind.ChildObligationDiscovered, 1, "evidence-1")],
            ["coverage-1"], [], ["trace-1"], "trace");
        Assert.Equal(a.EvidenceViewDigest, b.EvidenceViewDigest);
        Assert.NotEqual(a.EvidenceViewDigest, c.EvidenceViewDigest);
    }

    [Fact]
    public async Task SessionRejectsEveryEvidenceCorrelationMismatch()
    {
        var cases = new (string Name, Func<StrategyExecutionEvidenceView, StrategyExecutionEvidenceView> Mutate)[]
        {
            ("contract", v => new StrategyExecutionEvidenceView(v.RunId, v.RuntimeExecutionIntentReference, v.AcceptedObservationSequence, v.BeliefRevision, v.BeliefDigest, v.StructuralProgressRevision, v.StructuralProgressFacts, v.CoverageEvidenceReferences, v.ContradictionEvidenceReferences, v.TraceReferences, v.TraceDigest, "other.contract")),
            ("run", v => new StrategyExecutionEvidenceView("other-run", v.RuntimeExecutionIntentReference, v.AcceptedObservationSequence, v.BeliefRevision, v.BeliefDigest, v.StructuralProgressRevision, v.StructuralProgressFacts, v.CoverageEvidenceReferences, v.ContradictionEvidenceReferences, v.TraceReferences, v.TraceDigest)),
            ("observation", v => new StrategyExecutionEvidenceView(v.RunId, v.RuntimeExecutionIntentReference, 2, v.BeliefRevision, v.BeliefDigest, v.StructuralProgressRevision, v.StructuralProgressFacts, v.CoverageEvidenceReferences, v.ContradictionEvidenceReferences, v.TraceReferences, v.TraceDigest)),
            ("belief", v => new StrategyExecutionEvidenceView(v.RunId, v.RuntimeExecutionIntentReference, v.AcceptedObservationSequence, 2, "other-belief", v.StructuralProgressRevision, v.StructuralProgressFacts, v.CoverageEvidenceReferences, v.ContradictionEvidenceReferences, v.TraceReferences, v.TraceDigest)),
            ("dfs", v => new StrategyExecutionEvidenceView(v.RunId, v.RuntimeExecutionIntentReference, v.AcceptedObservationSequence, v.BeliefRevision, v.BeliefDigest, 2, v.StructuralProgressFacts, v.CoverageEvidenceReferences, v.ContradictionEvidenceReferences, v.TraceReferences, v.TraceDigest)),
            ("trace", v => new StrategyExecutionEvidenceView(v.RunId, v.RuntimeExecutionIntentReference, v.AcceptedObservationSequence, v.BeliefRevision, v.BeliefDigest, v.StructuralProgressRevision, v.StructuralProgressFacts, v.CoverageEvidenceReferences, v.ContradictionEvidenceReferences, v.TraceReferences, "other-trace")),
        };
        foreach (var (_, mutate) in cases)
        {
            var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.EvaluateAsync(Snapshot(session, mutate(View())), CancellationToken.None));
            Assert.Single(session.AcceptedHistory);
        }
    }

    [Fact]
    public async Task SessionRejectsSnapshotStrategyAndIntentCorrelationMismatch()
    {
        foreach (var snapshotMutation in new Func<StrategyExecutionReasoningSession, PreTerminalReasoningSnapshot>[]
        {
            s => SnapshotWithIds(s, "other-session", s.RuntimeExecutionIntentReference),
        })
        {
            var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.EvaluateAsync(snapshotMutation(session), CancellationToken.None));
            Assert.Single(session.AcceptedHistory);
        }
    }

    [Fact]
    public async Task ValidatorRejectsStrategyCorrelationFields()
    {
        var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
        var snapshot = Snapshot(session, View());
        var proposal = await session.EvaluateAsync(snapshot, CancellationToken.None);
        var validator = new UniClaw.Runtime.Agent.PreTerminalCheckpointValidator();
        var baseState = new UniClaw.Runtime.Agent.PreTerminalCheckpointState(RunId, RunState.Running, 0, 1, 1, "belief", 1, "trace", session.AcceptedReasoningRevisionReference, DateTimeOffset.UtcNow, "boundary", session.StrategyExecutionId, session.RuntimeExecutionIntentReference, snapshot.EvidenceViewDigest);
        Assert.False(validator.Validate(snapshot, proposal, baseState with { StrategyExecutionId = "other" }).Accepted);
        Assert.False(validator.Validate(snapshot, proposal, baseState with { RuntimeExecutionIntentReference = "other" }).Accepted);
        Assert.False(validator.Validate(snapshot, proposal, baseState with { EvidenceViewDigest = "other" }).Accepted);
        var wrongIntentSnapshot = SnapshotWithIds(session, session.StrategyExecutionId, "other-intent");
        Assert.False(validator.Validate(wrongIntentSnapshot, proposal, baseState).Accepted);
        Assert.Single(session.AcceptedHistory);
    }

    [Fact]
    public async Task CommitRejectsBaseObservationBeliefTraceAndKindMismatches()
    {
        foreach (var mutate in new Func<PreTerminalContinuationProposal, PreTerminalContinuationProposal>[]
        {
            p => new PreTerminalContinuationProposal(p.RunId, p.CycleSequence, p.AcceptedObservationSequence, p.BeliefRevision, p.TraceDigest, "other-base", p.ProposedReasoningRevisionReference, p.Kind, p.SupportingEvidenceReferences, p.EvaluatedAt, p.StrategyExecutionId, p.RuntimeExecutionIntentReference, p.EvidenceViewDigest),
            p => new PreTerminalContinuationProposal(p.RunId, p.CycleSequence, p.AcceptedObservationSequence + 1, p.BeliefRevision, p.TraceDigest, p.BaseReasoningRevisionReference, p.ProposedReasoningRevisionReference, p.Kind, p.SupportingEvidenceReferences, p.EvaluatedAt, p.StrategyExecutionId, p.RuntimeExecutionIntentReference, p.EvidenceViewDigest),
            p => new PreTerminalContinuationProposal(p.RunId, p.CycleSequence, p.AcceptedObservationSequence, p.BeliefRevision + 1, p.TraceDigest, p.BaseReasoningRevisionReference, p.ProposedReasoningRevisionReference, p.Kind, p.SupportingEvidenceReferences, p.EvaluatedAt, p.StrategyExecutionId, p.RuntimeExecutionIntentReference, p.EvidenceViewDigest),
            p => new PreTerminalContinuationProposal(p.RunId, p.CycleSequence, p.AcceptedObservationSequence, p.BeliefRevision, "other-trace", p.BaseReasoningRevisionReference, p.ProposedReasoningRevisionReference, p.Kind, p.SupportingEvidenceReferences, p.EvaluatedAt, p.StrategyExecutionId, p.RuntimeExecutionIntentReference, p.EvidenceViewDigest),
            p => new PreTerminalContinuationProposal(p.RunId, p.CycleSequence, p.AcceptedObservationSequence, p.BeliefRevision, p.TraceDigest, p.BaseReasoningRevisionReference, p.ProposedReasoningRevisionReference, PreTerminalContinuationKind.ContinuationNotSupported, p.SupportingEvidenceReferences, p.EvaluatedAt, p.StrategyExecutionId, p.RuntimeExecutionIntentReference, p.EvidenceViewDigest),
        })
        {
            var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
            var proposal = await session.EvaluateAsync(Snapshot(session, View()), CancellationToken.None);
            Assert.False(session.TryCommit(mutate(proposal)));
            Assert.Single(session.AcceptedHistory);
        }
    }

    [Fact]
    public async Task TerminalAndCancellationAreRejectedByAgentValidator()
    {
        var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
        var snapshot = Snapshot(session, View());
        var proposal = await session.EvaluateAsync(snapshot, CancellationToken.None);
        var validator = new UniClaw.Runtime.Agent.PreTerminalCheckpointValidator();
        var state = new UniClaw.Runtime.Agent.PreTerminalCheckpointState(
            RunId, RunState.Completed, 0, 1, 1, "belief", 1, "trace", session.AcceptedReasoningRevisionReference,
            DateTimeOffset.UtcNow, "boundary", session.StrategyExecutionId, session.RuntimeExecutionIntentReference,
            snapshot.EvidenceViewDigest);
        Assert.False(validator.Validate(snapshot, proposal, state).Accepted);
        Assert.False(validator.Validate(snapshot, proposal, state, cancelled: true).Accepted);
        Assert.Single(session.AcceptedHistory);
    }

    [Fact]
    public void EvidenceViewIsImmutableAndHasNoAuthorityBearingTypes()
    {
        var type = typeof(StrategyExecutionEvidenceView);
        Assert.True(type.IsSealed);
        foreach (var property in type.GetProperties())
            Assert.False(property.SetMethod is not null && property.SetMethod.IsPublic);
        var forbidden = new[] { "DeviceAction", "RunState", "GoalEvidence", "Traversal", "StateMachine", "Recovery", "Agent" };
        Assert.DoesNotContain(type.GetMembers(BindingFlags.Public | BindingFlags.Instance), member =>
            forbidden.Any(token => member.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SessionHasNoExecutionOrLifecycleDependencies()
    {
        var forbidden = new[] { "DeviceAction", "RunState", "GoalEvidence", "Traversal", "StateMachine", "Recovery", "Agent", "MultiRun" };
        var session = typeof(StrategyExecutionReasoningSession);
        foreach (var member in session.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            Assert.DoesNotContain(forbidden, token => member.Name.Contains(token, StringComparison.OrdinalIgnoreCase));
        foreach (var field in session.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            Assert.DoesNotContain(forbidden, token => field.FieldType.FullName?.Contains(token, StringComparison.OrdinalIgnoreCase) is true);
    }

    [Fact]
    public void StrategyAndEvidenceCollectionsAreDefensivelyImmutable()
    {
        var intent = AcceptedIntent();
        Assert.True(typeof(StrategyDirective).GetProperties().All(p => p.SetMethod is null || !p.SetMethod.IsPublic));
        var view = View();
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<string>>(view.CoverageEvidenceReferences);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<StrategyStructuralProgressFact>>(view.StructuralProgressFacts);
        Assert.NotEmpty(intent.Strategy.Adaptation.AllowedAdaptations);
    }

    [Fact]
    public void StrategyExecutionSourceHasNoRunCreationOrSemanticEnrichmentCalls()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Planning/StrategyContract.cs"));
        foreach (var token in new[] { "new DeviceAction", "StartRun", "MultiRun", "InvokeTraversal", "SemanticCapabilityBinding.Enrich" })
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionHypothesisLedger", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviseFromEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Reconcile(agent", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Adapt()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RunState.Completed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryEvaluatorPathDoesNotProjectStrategyEvidence()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.PreTerminalCycle.cs"));
        Assert.Contains("strategyEvaluator is null ? null : new StrategyExecutionEvidenceView", source, StringComparison.Ordinal);
        Assert.Contains("strategyEvaluator?.StrategyExecutionId", source, StringComparison.Ordinal);
        Assert.Contains("evaluator is null || _state != RunState.Running", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RelatedProductionSurfacesHaveNoForbiddenAuthorityTypesOrScenarioTokens()
    {
        var files = new[]
        {
            "src/UniClaw.Runtime/Planning/StrategyExecutionReasoningSession.cs",
            "src/UniClaw.Runtime/Model/StrategyExecutionEvidenceView.cs",
            "src/UniClaw.Runtime/Model/PreTerminalReasoningSnapshot.cs",
            "src/UniClaw.Runtime/Model/PreTerminalContinuationProposal.cs",
            "src/UniClaw.Runtime/Agent/Agent.PreTerminalCycle.cs",
            "src/UniClaw.Runtime/Planning/StrategyContract.cs",
        };
        var forbidden = new[] { "DeviceAction", "WorldBelief", "GoalEvidence", "RunState", "Traversal", "StateMachine", "Recovery", "Android", "Settings", "Adb" };
        foreach (var file in files)
        {
            var source = File.ReadAllText(RepoPath(file));
            if (file.EndsWith("StrategyContract.cs", StringComparison.Ordinal)
                || file.EndsWith("Agent.PreTerminalCycle.cs", StringComparison.Ordinal)) continue; // Agent owns lifecycle/state at this seam
            foreach (var token in forbidden)
                Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task SealedSessionRejectsLaterEvaluationAndCommit()
    {
        var session = new StrategyExecutionReasoningSession(AcceptedIntent(), RunId);
        var proposal = await session.EvaluateAsync(Snapshot(session, View()), CancellationToken.None);
        session.Seal();
        Assert.False(session.TryCommit(proposal));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.EvaluateAsync(Snapshot(session, View()), CancellationToken.None));
    }

    [Fact]
    public void SourceContainsNoScenarioOrExternalEnrichmentDependency()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Planning/StrategyExecutionReasoningSession.cs"));
        foreach (var token in new[] { "Android", "Settings", "Adb", "SemanticCapability", "DeviceAction", "RunOpenWorldAsync", "Traversal" })
            Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"\bVision\b", source);
    }

    private static RuntimeExecutionIntent AcceptedIntent()
    {
        var result = StrategyTestSupport.ExploreCompiler().Compile(StrategyTestSupport.Explore());
        return Assert.IsType<StrategyCompilationResult.Accepted>(result).Intent;
    }

    private static StrategyExecutionEvidenceView View(bool contradiction = false)
        => new(RunId, "strategy-explore-1", 1, 1, "belief", 1,
            [new StrategyStructuralProgressFact(contradiction ? StrategyStructuralProgressKind.ContradictionObserved : StrategyStructuralProgressKind.BoundedScopeEntered, 1, "evidence-1")],
            ["coverage-1"], contradiction ? ["contradiction-1"] : [], ["trace-1"], "trace");

    private static PreTerminalReasoningSnapshot Snapshot(StrategyExecutionReasoningSession session, StrategyExecutionEvidenceView view)
        => new(PreTerminalReasoningSnapshot.CurrentContractVersion, RunId, 0, 1, 1, "belief", 1,
            "cursor", "trace", session.AcceptedReasoningRevisionReference, "boundary", DateTimeOffset.UtcNow.AddMinutes(1),
            ["trace-1"], view, session.StrategyExecutionId, session.RuntimeExecutionIntentReference);

    private static PreTerminalReasoningSnapshot SnapshotWithIds(StrategyExecutionReasoningSession session, string strategyId, string intentId)
        => new(PreTerminalReasoningSnapshot.CurrentContractVersion, RunId, 0, 1, 1, "belief", 1,
            "cursor", "trace", session.AcceptedReasoningRevisionReference, "boundary", DateTimeOffset.UtcNow.AddMinutes(1),
            ["trace-1"], View(), strategyId, intentId);

    private static string RepoPath(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        // 仓库根 = 同时含 AGENTS.md 与 src/UniClaw.Runtime.sln（子级区域地图只满足 AGENTS.md）。
        while (directory is not null
            && !(File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "src", "UniClaw.Runtime.sln"))))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException(), relative);
    }
}
