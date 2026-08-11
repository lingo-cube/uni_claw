using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    /// <summary>
    /// U2 opt-in open-world traversal. The Planning seam passes only already-authoritative
    /// primitive/model boundaries; this Agent neither references Planning nor manufactures a
    /// Plan, route, inventory, or completion receipt. Parent continuation is method-local.
    /// </summary>
    internal async Task<RunState> RunOpenWorldAsync(
        Goal goal,
        string applicationIdentity,
        string expectedSemanticEntry,
        int maximumDepth,
        string runId,
        TypeLevelDispatchPolicy? dispatchPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSemanticEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (maximumDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        if (goal.BranchInventoryEvaluator is null || goal.CandidateAuthorizationEvaluator is null)
            throw new ArgumentException("Open-world traversal requires inventory and candidate-authorization criteria.", nameof(goal));
        if (_state != RunState.Idle)
            throw new InvalidOperationException("Agent 已执行过 Run（一个实例恰好对应一次 Run；请新建实例）。");

        _trace.Add(new TraceEvent(runId) { RunState = RunState.Idle });
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Initializing });
        _state = RunState.Initializing;
        var startupResult = await _startup.StartAsync(cancellationToken);
        if (startupResult is StartupResult.NotReady notReady)
            return Fail(runId, notReady.Reason);

        var ready = (StartupResult.Ready)startupResult;
        _recoveryAnchor = ready.Anchor;
        if (!string.Equals(ready.Anchor.ApplicationIdentity, applicationIdentity, StringComparison.Ordinal)
            || !string.Equals(ready.Anchor.ExpectedSemanticEntry, expectedSemanticEntry, StringComparison.Ordinal))
        {
            return Fail(runId, "Open-world specification entry does not match the verified Startup boundary.");
        }

        _trace.Add(new TraceEvent(runId) { RunState = RunState.Running });
        _state = RunState.Running;
        var initial = await _observeInitial(cancellationToken);
        _belief = Reconcile.FromObservation(initial, _resolveSemanticPage);
        if (!string.Equals(_belief.SemanticPage, expectedSemanticEntry, StringComparison.Ordinal))
            return Fail(runId, "Open-world initial Observation does not reconcile to the declared semantic entry.");
        _activeContainer = CreateContainer(expectedSemanticEntry);
        _activeContainer.Bind(initial);
        _trace.Add(new TraceEvent(runId) { ContainerId = expectedSemanticEntry });

        // Execution-local association only: no frame type, field, persistent route, or state owner.
        var parents = new Stack<(RuntimeContainer Parent, string ChildIdentity)>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var container = _activeContainer
                ?? throw new InvalidOperationException("open-world traversal 缺少 active Container。");
            var current = container.CurrentObservation
                ?? throw new InvalidOperationException("open-world traversal Container 缺少当前 Observation。");
            var semanticDepth = parents.Count;
            var inventory = goal.BranchInventoryEvaluator(container.ViewportExplorationObservations, semanticDepth)
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 返回 null：必须返回 BranchInventoryEvidence。");
            var outcome = inventory.RequiredBranchEvidence is null ? "unresolved"
                : inventory.RequiredBranchEvidence.Count == 0 ? "bounded-leaf" : "complete";
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = $"open-world branch inventory {outcome}: depth={semanticDepth}, source-seq={current.SequenceNumber}; {inventory.Reason}",
            });
            if (!TryAcceptBranchInventory(container, current, inventory, out var progress, out var inventoryFailure))
                return Fail(runId, inventoryFailure!);

            var requiredBranches = inventory.RequiredBranchEvidence
                ?? throw new InvalidOperationException("Accepted inventory must contain required-branch evidence.");
            var pending = progress!.ApprovedSiblingEvidence
                .Where(item => !progress.CompletedSiblingEvidence.ContainsKey(item.Key))
                .OrderBy(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .ToArray();
            var subtreeTerminal = requiredBranches.Count == 0 || pending.Length == 0;
            if (subtreeTerminal)
            {
                if (parents.Count == 0)
                {
                    if (requiredBranches.Count == 0)
                        return Fail(runId, "Root bounded inventory is empty; no verified required traversal work supports this U2 execution path.");

                    // VerifiedBoundedTraversalCompletion is derived only at the root, before the existing evaluator.
                    var finalEvidence = goal.EvidenceEvaluator(current);
                    if (finalEvidence.Satisfied)
                        return Complete(runId, finalEvidence);
                    return Fail(runId, $"Verified bounded traversal completion but fresh GoalEvidence remains unsatisfied：{finalEvidence.Reason}");
                }

                var (parent, childIdentity) = parents.Peek();
                var returnCandidates = current.Elements
                    .Where(element => string.Equals(element.Text, parent.SemanticPageName, StringComparison.Ordinal))
                    .ToArray();
                if (returnCandidates.Length != 1)
                    return Fail(runId, $"Parent return is not uniquely grounded for '{parent.SemanticPageName}'；零 return dispatch。");
                var returnAuthorization = goal.CandidateAuthorizationEvaluator(current, returnCandidates[0])
                    ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
                if (returnAuthorization.Authorized is not true)
                    return Fail(runId, $"Parent return is not authorized for '{parent.SemanticPageName}'；零 return dispatch。");

                var returnResult = container.ExecuteStep(new PlanStep(parent.SemanticPageName, "Tap"));
                var returnEntry = LastJournalEntry();
                if (returnResult is TraversalStepResult.Failed failed)
                    return Fail(runId, failed.Reason, returnEntry.StepId);
                RecordDispatchedStep(runId, container, returnEntry);
                var returned = returnEntry.PostActionObservation
                    ?? throw new InvalidOperationException("parent return Succeeded 但缺少 fresh Observation。");
                _belief = Reconcile.FromObservation(returned, _resolveSemanticPage);
                if (returned.SequenceNumber <= current.SequenceNumber
                    || !string.Equals(_belief.SemanticPage, parent.SemanticPageName, StringComparison.Ordinal)
                    || !parent.TryVerifyViewportContinuity(returned, _belief.SemanticPage, applicationIdentity))
                {
                    return Fail(runId, $"Parent return did not prove fresh exact reconciliation to '{parent.SemanticPageName}'；no child completion.", returnEntry.StepId);
                }

                if (!_branchProgress.TryGetValue(parent.SemanticPageName, out var parentProgress))
                    return Fail(runId, "Verified parent return lacks accepted parent progress evidence.", returnEntry.StepId);
                _branchProgress = _branchProgress.SetItem(
                    parent.SemanticPageName,
                    parentProgress.WithCompletedSibling(childIdentity, current.SequenceNumber));
                parents.Pop();
                _activeContainer = parent;
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = parent.SemanticPageName,
                    StepId = returnEntry.StepId,
                    Reason = $"verified parent return; child '{childIdentity}' progress retained (seq={current.SequenceNumber})",
                });
                continue;
            }

            if (semanticDepth >= maximumDepth)
            {
                return Fail(runId,
                    $"In-scope inventory requires traversal beyond declared depth={maximumDepth}; bounded cutoff is not exhaustion.");
            }

            var (branchIdentity, sourceSequence) = pending[0];
            var source = container.ViewportExplorationObservations
                .First(observation => observation.SequenceNumber == sourceSequence);
            var sourceCandidate = source.Elements.First(element => string.Equals(element.Text, branchIdentity, StringComparison.Ordinal));
            var authorization = goal.CandidateAuthorizationEvaluator(source, sourceCandidate)
                ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
            if (authorization.Authorized is not true)
                return Fail(runId, $"Required branch '{branchIdentity}' is not authorized；zero discovered-branch dispatch。");
            var selected = current.Elements.Where(element => string.Equals(element.Text, branchIdentity, StringComparison.Ordinal)).ToArray();
            if (selected.Length != 1)
                return Fail(runId, $"Required branch '{branchIdentity}' is not uniquely present in current fresh evidence；zero dispatch。");

            // ── CP-12 type-directed dispatch: classify element → resolve handling → dispatch ──
            var category = goal.CategoryClassifier?.Invoke(sourceCandidate);
            var handling = category is not null && dispatchPolicy is not null
                ? dispatchPolicy.Resolve(category.Value)
                : null;
            if (handling == TypeLevelHandling.Forbidden)
                return Fail(runId, $"Required branch '{branchIdentity}' category {category} is forbidden by the dispatch policy；zero dispatch。");
            if (category is not null && handling is null)
                return Fail(runId, $"Required branch '{branchIdentity}' category {category} has no authorized handling in the dispatch policy；zero dispatch。");

            PlanStep step;
            switch (handling)
            {
                case TypeLevelHandling.SetDesiredState:
                    step = new PlanStep(branchIdentity, "SetSwitch true"); break;
                case TypeLevelHandling.Inspect:
                    step = new PlanStep(branchIdentity, "Tap"); break; // Inspect = Tap without child container creation
                default:
                    step = new PlanStep(branchIdentity, "Tap"); break; // EnterAndTraverse or null → Tap
            }

            var result = container.ExecuteStep(step);
            var entry = LastJournalEntry();
            if (result is TraversalStepResult.Failed failedStep)
                return Fail(runId, failedStep.Reason, entry.StepId);
            RecordDispatchedStep(runId, container, entry);

            // Inspect / SetDesiredState: leaf interaction — stay on current container, mark branch completed.
            if (handling is TypeLevelHandling.Inspect or TypeLevelHandling.SetDesiredState)
            {
                if (!_branchProgress.TryGetValue(container.SemanticPageName, out var leafProgress))
                    return Fail(runId, "Leaf dispatch lacks accepted progress evidence for the current container.", entry.StepId);
                _branchProgress = _branchProgress.SetItem(
                    container.SemanticPageName,
                    leafProgress.WithCompletedSibling(branchIdentity, entry.PostActionObservation?.SequenceNumber ?? current.SequenceNumber));
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = container.SemanticPageName,
                    StepId = entry.StepId,
                    Reason = $"leaf {handling} dispatched for '{branchIdentity}' (seq={entry.PostActionObservation?.SequenceNumber})",
                });
                continue;
            }

            // EnterAndTraverse (or null/Tap): enter child container for subtree traversal.
            var childObs = entry.PostActionObservation
                ?? throw new InvalidOperationException("bounded branch Tap Succeeded 但缺少 fresh Observation。");
            _belief = Reconcile.FromObservation(childObs, _resolveSemanticPage);
            var childPage = _belief.SemanticPage;
            if (childPage is null
                || string.Equals(childPage, container.SemanticPageName, StringComparison.Ordinal)
                || container.IsStillMine(childObs))
            {
                return Fail(runId, $"Required branch '{branchIdentity}' dispatch did not prove a fresh child Container transition；不 blind redispatch。", entry.StepId);
            }

            parents.Push((container, branchIdentity));
            _activeContainer = CreateContainer(childPage);
            _activeContainer.Bind(childObs);
            _trace.Add(new TraceEvent(runId) { ContainerId = childPage });
        }
    }

    private RunState RunBoundedCrossPageDiscovery(Goal goal, Plan plan, string runId)
    {
        var semanticDepth = 0;
        var nextViewportStep = 0;
        var viewportSteps = plan.Steps
            .Where(step => IsScrollForwardAction(step.ActionDescription))
            .ToImmutableArray();

        while (true)
        {
            var container = _activeContainer
                ?? throw new InvalidOperationException("bounded discovery 缺少 active Container。");
            var current = container.CurrentObservation
                ?? throw new InvalidOperationException("bounded discovery Container 缺少当前 Observation。");
            var accepted = container.ViewportExplorationObservations;
            var evaluator = goal.BranchInventoryEvaluator
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 缺失：调用方必须先检查 optional criterion。");
            var inventory = evaluator(accepted, semanticDepth)
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 返回 null：必须返回 BranchInventoryEvidence。");

            var inventoryOutcome = inventory.RequiredBranchEvidence switch
            {
                null => "unresolved",
                { Count: 0 } => "leaf",
                _ => "complete",
            };
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = $"branch inventory {inventoryOutcome}: depth={semanticDepth}, "
                    + $"source-seq={current.SequenceNumber}; {inventory.Reason}",
            });

            if (inventory.RequiredBranchEvidence is null)
            {
                if (nextViewportStep >= viewportSteps.Length
                    || goal.ViewportExplorationEvaluator is null)
                {
                    return Fail(
                        runId,
                        $"Required branch inventory unresolved at depth={semanticDepth}：{inventory.Reason}；零 discovered-branch dispatch。");
                }

                var viewportDecision = EvaluateViewportExploration(goal, container, runId, stepId: null);
                if (viewportDecision.ContinueExploration is not true)
                {
                    var outcome = viewportDecision.ContinueExploration is false ? "exhausted" : "unresolved";
                    return Fail(
                        runId,
                        $"Branch inventory unresolved and viewport exploration {outcome}：{viewportDecision.Reason}；不 dispatch discovered branch。");
                }

                var viewportStep = viewportSteps[nextViewportStep++];
                var viewportResult = container.ExecuteStep(viewportStep);
                var viewportEntry = LastJournalEntry();
                if (viewportResult is TraversalStepResult.Failed viewportFailed)
                    return Fail(runId, viewportFailed.Reason, viewportEntry.StepId);

                RecordDispatchedStep(runId, container, viewportEntry);
                var viewportObservation = viewportEntry.PostActionObservation
                    ?? throw new InvalidOperationException("viewport step Succeeded 但缺少 fresh Observation。");
                _belief = Reconcile.FromObservation(viewportObservation, _resolveSemanticPage);
                if (!container.TryVerifyViewportContinuity(
                        viewportObservation,
                        _belief.SemanticPage,
                        _recoveryAnchor!.ApplicationIdentity))
                {
                    EmitViewportEscalation(
                        runId,
                        viewportEntry,
                        container,
                        viewportObservation,
                        "Bounded discovery viewport evidence cannot prove same-Container continuity.");
                    return Fail(
                        runId,
                        $"Viewport movement 后无法证明同一 Container continuity（seq={viewportObservation.SequenceNumber}）。",
                        viewportEntry.StepId);
                }

                // Same-Container accepted evidence extends the criterion input but does not change
                // semanticDepth. Re-evaluate inventory from the refreshed evidence next iteration.
                continue;
            }

            if (!TryAcceptBranchInventory(container, current, inventory, out var progress, out var invalidReason))
                return Fail(runId, invalidReason!);

            if (inventory.RequiredBranchEvidence.Count == 0)
            {
                var leafGoalEvidence = goal.EvidenceEvaluator(current);
                if (leafGoalEvidence.Satisfied)
                    return Complete(runId, leafGoalEvidence);
                return Fail(
                    runId,
                    $"Bounded leaf positively proven but GoalEvidence remains unsatisfied：{leafGoalEvidence.Reason}");
            }

            var pendingBranches = progress!.ApprovedSiblingEvidence
                .Where(entry => !progress.CompletedSiblingEvidence.ContainsKey(entry.Key))
                .OrderBy(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .ToArray();
            if (pendingBranches.Length == 0)
            {
                var exhaustedGoalEvidence = goal.EvidenceEvaluator(current);
                if (exhaustedGoalEvidence.Satisfied)
                    return Complete(runId, exhaustedGoalEvidence);
                return Fail(
                    runId,
                    "Required branch inventory contains no unresolved work, but independent GoalEvidence remains unsatisfied；不 redispatch proven branch。");
            }

            var authorizationEvaluator = goal.CandidateAuthorizationEvaluator;
            if (authorizationEvaluator is null)
            {
                return Fail(
                    runId,
                    "Required branch inventory exists but bounded candidate authorization is unresolved because no criterion was supplied；零 dispatch。");
            }

            ObservedElement? selected = null;
            foreach (var (branchIdentity, sourceSequence) in pendingBranches)
            {
                var sourceObservation = accepted.First(observation => observation.SequenceNumber == sourceSequence);
                var sourceCandidate = sourceObservation.Elements.First(element =>
                    string.Equals(element.Text, branchIdentity, StringComparison.Ordinal));
                var authorization = authorizationEvaluator(sourceObservation, sourceCandidate)
                    ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
                var outcome = authorization.Authorized switch
                {
                    true => "authorized",
                    false => "rejected",
                    null => "unresolved",
                };
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = container.SemanticPageName,
                    Reason = $"required branch authorization {outcome}: text={branchIdentity}, "
                        + $"source-seq={sourceSequence}; {authorization.Reason}",
                });
                if (authorization.Authorized is true)
                {
                    selected = current.Elements.FirstOrDefault(element =>
                        string.Equals(element.Text, branchIdentity, StringComparison.Ordinal));
                    if (selected is null)
                    {
                        return Fail(
                            runId,
                            $"Required authorized branch '{branchIdentity}' is absent from the current fresh Observation；零 dispatch。");
                    }
                    break;
                }
            }

            if (selected is null)
            {
                return Fail(
                    runId,
                    $"No required branch is independently authorized at depth={semanticDepth}；零 discovered-branch dispatch。");
            }

            var selectedStep = new PlanStep(selected.Text, "Tap");
            var parentPage = container.SemanticPageName;
            var result = container.ExecuteStep(selectedStep);
            var entry = LastJournalEntry();
            if (result is TraversalStepResult.Failed failed)
                return Fail(runId, failed.Reason, entry.StepId);

            RecordDispatchedStep(runId, container, entry);
            var postObservation = entry.PostActionObservation
                ?? throw new InvalidOperationException("bounded branch Tap Succeeded 但缺少 fresh Observation。");
            _belief = Reconcile.FromObservation(postObservation, _resolveSemanticPage);
            var childPage = _belief.SemanticPage;
            if (childPage is null
                || string.Equals(childPage, parentPage, StringComparison.Ordinal)
                || container.IsStillMine(postObservation))
            {
                return Fail(
                    runId,
                    $"Required branch '{selected.Text}' dispatch did not prove a fresh child Container transition；不 blind redispatch。",
                    entry.StepId);
            }

            _activeContainer = CreateContainer(childPage);
            _activeContainer.Bind(postObservation);
            semanticDepth = checked(semanticDepth + 1);
            _trace.Add(new TraceEvent(runId) { ContainerId = childPage });
        }
    }

    private bool TryAcceptBranchInventory(
        RuntimeContainer container,
        Observation current,
        BranchInventoryEvidence inventory,
        out BranchProgressEvidence? progress,
        out string? failure)
    {
        progress = null;
        failure = null;
        var required = inventory.RequiredBranchEvidence;
        if (required is null)
        {
            failure = "Unresolved inventory cannot be accepted.";
            return false;
        }

        var accepted = container.ViewportExplorationObservations;
        if (accepted.IsDefaultOrEmpty
            || accepted[^1].SequenceNumber != current.SequenceNumber
            || !ReferenceEquals(current, container.CurrentObservation)
            || !string.Equals(_belief?.SemanticPage, container.SemanticPageName, StringComparison.Ordinal))
        {
            failure = "Inventory source is not the current accepted semantic Container evidence.";
            return false;
        }

        foreach (var (identity, sequence) in required)
        {
            var source = accepted.FirstOrDefault(observation => observation.SequenceNumber == sequence);
            if (source is null
                || !source.Elements.Any(element => string.Equals(element.Text, identity, StringComparison.Ordinal)))
            {
                failure = $"Inventory branch '{identity}' does not reference accepted source evidence seq={sequence}.";
                return false;
            }
        }

        var completed = ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal);
        if (_branchProgress.TryGetValue(container.SemanticPageName, out var prior))
        {
            completed = prior.CompletedSiblingEvidence
                .Where(entry => required.ContainsKey(entry.Key))
                .ToImmutableDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }

        progress = new BranchProgressEvidence(container.SemanticPageName, required, completed);
        _branchProgress = _branchProgress.SetItem(container.SemanticPageName, progress);
        return true;
    }
}
