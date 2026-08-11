using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    /// <summary>
    /// Runs the semantic closed loop for a structured desired outcome.
    ///
    /// Flow: READ belief → DECIDE → ACT (if needed) → OBSERVE → UPDATE → RE-EVALUATE
    /// Terminates on: SATISFIED, STATE_EVIDENCE_REQUIRED, BINDING_UNRESOLVED,
    /// SEMANTIC_CONTRADICTION, BUDGET_EXHAUSTED, or EXECUTION_FAILED.
    ///
    /// Agent is the sole semantic decision authority (I-3).
    /// Container owns all local belief state (I-2).
    /// Traversal lowers; Environment dispatches.
    /// </summary>
    /// <param name="goal">Desired semantic outcome.</param>
    /// <param name="objects">Available SemanticObject definitions.</param>
    /// <param name="capabilities">Available Capability definitions.</param>
    /// <param name="runId">Deterministic Run identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="maxIterations">Maximum loop iterations (default 5).</param>
    public async Task<SemanticRunResult> RunSemanticGoalAsync(
        SemanticGoalInput goal,
        ImmutableArray<SemanticObject> objects,
        ImmutableArray<Capability> capabilities,
        string runId,
        CancellationToken cancellationToken = default,
        int maxIterations = 5)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (_state != RunState.Idle)
            throw new InvalidOperationException("Agent has already executed a Run.");
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Idle });
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Initializing });
        _state = RunState.Initializing;
        if (_pageAnalysisCriteria is null || _elementBindingCriteria is null)
            return FailSemantic(runId, new SemanticRunResult.BindingUnresolved("Semantic recognition criteria were not configured on Agent."));

        var obj = ResolveSemanticObject(objects, goal);
        if (obj is null)
            return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                $"Unknown object '{goal.ObjectIdentity}'."));
        if (!obj.StateDimensions.Contains(goal.StateDimension))
            return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                $"Object '{goal.ObjectIdentity}' does not declare state dimension '{goal.StateDimension}'."));

        var startupResult = await _startup.StartAsync(cancellationToken);
        if (startupResult is StartupResult.NotReady notReady)
            return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(notReady.Reason));
        var ready = (StartupResult.Ready)startupResult;
        _recoveryAnchor = ready.Anchor;
        _state = RunState.Running;
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Running });

        var observation = await _observeInitial(cancellationToken);
        _belief = Reconcile.FromObservation(observation, _resolveSemanticPage);
        if (!string.Equals(_belief.SemanticPage, ready.Anchor.ExpectedSemanticEntry, StringComparison.Ordinal))
            return FailSemantic(runId, new SemanticRunResult.SemanticContradiction(
                $"Initial semantic observation does not reconcile to '{ready.Anchor.ExpectedSemanticEntry}'."));
        _activeContainer = CreateContainer(ready.Anchor.ExpectedSemanticEntry);
        _activeContainer.Bind(observation);
        RefreshContainerEvidence(_activeContainer, observation);
        _trace.Add(new TraceEvent(runId) { ContainerId = _activeContainer.SemanticPageName });

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ── 1. READ current belief ─────────────────────────────────
            var stateKey = $"{goal.ObjectIdentity}.{goal.StateDimension}";
            var container = _activeContainer ?? throw new InvalidOperationException("Semantic run has no active Container.");

            // ── 2. DECIDE ──────────────────────────────────────────────
            // Check page belief — contradictory page belief blocks action
            if (container.LocalPageBeliefState == SemanticBeliefState.Contradicted)
                return FailSemantic(runId, new SemanticRunResult.SemanticContradiction(
                    "Container page belief is CONTRADICTED — refusing to act on local binding."));

            var currentBelief = container.ObjectStateBeliefs.GetValueOrDefault(stateKey);
            if (currentBelief == goal.DesiredValue)
            {
                var evidence = new GoalEvidence(true, $"'{stateKey}' is {goal.DesiredValue}.", observation.SequenceNumber);
                return CompleteSemantic(runId, evidence);
            }

            // Missing grounding is distinct from an identified surface whose
            // world state is unknown. Check it before reporting state evidence.
            if (container.ObjectBindings is not { Length: > 0 } currentBindings
                || currentBindings.All(b => b.ObjectIdentity != goal.ObjectIdentity))
                return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                    $"No binding for '{goal.ObjectIdentity}'."));

            if (currentBelief is null)
                return FailSemantic(runId, new SemanticRunResult.StateEvidenceRequired(
                    $"'{stateKey}' is UNKNOWN — cannot safely dispatch."));

            // ── 3. SELECT capability ──────────────────────────────────
            var matches = SelectCapability(capabilities, obj, goal);
            if (matches.Length != 1)
                return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                    $"Capability selection for category '{obj.Category}' dimension '{goal.StateDimension}' is {(matches.Length == 0 ? "unresolved" : "ambiguous")}"));
            var capability = matches[0];
            _trace.Add(new TraceEvent(runId) { Reason = $"semantic capability selected: {capability.Name}" });

            // ── 4. AUTHORIZE semantic action ──────────────────────────
            var action = new SemanticAction(
                goal.ObjectIdentity, capability.Name, goal.StateDimension, goal.DesiredValue);
            var authResult = AuthorizeAction(action, obj, capability);
            if (authResult is not null)
                return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(
                    $"Authorization failed: {((SemanticActionResult.Invalid)authResult).Reason}"));

            // ── 5. CHECK binding ──────────────────────────────────────
            var binding = currentBindings.FirstOrDefault(b => b.ObjectIdentity == goal.ObjectIdentity);
            if (binding is null)
                return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                    $"No binding for '{goal.ObjectIdentity}'."));

            // ── 6. LOWER to ExecutionAction ───────────────────────────
            var lowerResult = RuntimeTraversal.LowerAction(action, binding, observation);
            switch (lowerResult)
            {
                case SemanticActionResult.Dispatched dispatched:
                    var step = _traversal.ExecuteLoweredAction(dispatched.Action, observation);
                    var journal = _traversal.Journal[^1];
                    if (step is TraversalStepResult.Failed failed || journal.PostActionObservation is null)
                        return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(
                            step is TraversalStepResult.Failed failure ? failure.Reason : "Semantic action did not yield fresh observation."));
                    var freshObs = journal.PostActionObservation;
                    var freshBelief = Reconcile.FromObservation(freshObs, _resolveSemanticPage);
                    if (!container.TryVerifyLocalContinuity(
                            freshObs,
                            freshBelief.SemanticPage,
                            ready.Anchor.ApplicationIdentity))
                        return FailSemantic(runId, new SemanticRunResult.SemanticContradiction("Post-action observation cannot prove same Container continuity."));
                    observation = freshObs;
                    _belief = freshBelief;
                    RefreshContainerEvidence(container, freshObs);
                    RecordDispatchedStep(runId, container, journal);

                    // ── 10. RE-EVALUATE (loop continues) ───────────────
                    break;

                case SemanticActionResult.NoOp:
                    return FailSemantic(runId, new SemanticRunResult.ExecutionFailed("Lowering reported no-op before fresh GoalEvidence evaluation."));

                case SemanticActionResult.StateUnknown unknown:
                    return FailSemantic(runId, new SemanticRunResult.StateEvidenceRequired(unknown.Reason));

                case SemanticActionResult.Unresolved unresolved:
                    return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(unresolved.Reason));

                case SemanticActionResult.Invalid invalid:
                    return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(invalid.Reason));

                default:
                    return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(
                        $"Unexpected lowering result: {lowerResult.GetType().Name}"));
            }
        }

        return FailSemantic(runId, new SemanticRunResult.BudgetExhausted(
            $"Semantic loop did not converge within {maxIterations} iterations."));
    }

    private static SemanticObject? ResolveSemanticObject(
        ImmutableArray<SemanticObject> objects,
        SemanticGoalInput goal)
        => objects.FirstOrDefault(obj => obj.Identity == goal.ObjectIdentity);

    private static ImmutableArray<Capability> SelectCapability(
        ImmutableArray<Capability> capabilities,
        SemanticObject obj,
        SemanticGoalInput goal)
        => [.. capabilities.Where(capability =>
            capability.ApplicableToCategory == obj.Category
            && capability.StateDimension == goal.StateDimension)];

    private void RefreshContainerEvidence(RuntimeContainer container, Observation observation)
    {
        var bindingEvidence = BindingAnalysis.Analyze(observation, _elementBindingCriteria!);
        var bindings = BindingReconciler.Reconcile(bindingEvidence, _elementBindingCriteria!.KnownObjects);
        var pageEvidence = PageAnalysis.Analyze(observation, _pageAnalysisCriteria!);
        container.RefreshSemanticSnapshot(observation, bindings, pageEvidence);
    }

    private SemanticRunResult.Satisfied CompleteSemantic(string runId, GoalEvidence evidence)
    {
        _ = Complete(runId, evidence);
        return new SemanticRunResult.Satisfied(evidence);
    }

    private T FailSemantic<T>(string runId, T result) where T : SemanticRunResult
    {
        var reason = result switch
        {
            SemanticRunResult.StateEvidenceRequired state => state.Reason,
            SemanticRunResult.BindingUnresolved binding => binding.Reason,
            SemanticRunResult.SemanticContradiction contradiction => contradiction.Reason,
            SemanticRunResult.BudgetExhausted budget => budget.Reason,
            SemanticRunResult.ExecutionFailed execution => execution.Reason,
            _ => "Semantic run failed.",
        };
        _ = Fail(runId, reason);
        return result;
    }
}
