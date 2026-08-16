using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;
using UniClaw.Runtime.World;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    /// <summary>
    /// 真实 UI 转场存在动画窗口：动作后立即观测可能捕获滑动中的中间帧（现场证据：multilevel
    /// live run 的 post-action 帧捕获到移动中的 Wi‑Fi 开关）。D5 验证失败 ≠ 世界未变 —
    /// 导航相位以有界 settle + 仅重观测（零重发、零 journal 条目）再取证；耗尽仍未证明 → 原原因 fail closed。
    /// 机制级时序常量（Phase 4 真实 IO seam），非语义知识；Fake 环境瞬时完成，happy path 不触发。
    /// 软件渲染 emulator（swiftshader_indirect）的 Settings 转场实测需 ~1.5-2s，窗口取 4×500ms。
    /// </summary>
    private static readonly TimeSpan NavigationTransitionSettle = TimeSpan.FromMilliseconds(500);

    /// <summary>导航转场重观测上限（有界；耗尽即 fail closed，绝不无限重试）。</summary>
    private const int NavigationReobserveAttempts = 4;

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
    /// <param name="viewportExplorationEvaluator">SC-P3-CAND-007 optional bounded same-Container
    /// exploration criterion (runtime exploration knowledge, NOT part of <see cref="SemanticGoalInput"/>).
    /// Absent (null) preserves the existing navigation-only behavior. The evaluator only interprets
    /// the Container's accumulated viewport-exploration observations; the Agent keeps sole decision
    /// authority.</param>
    public async Task<SemanticRunResult> RunSemanticGoalAsync(
        SemanticGoalInput goal,
        ImmutableArray<SemanticObject> objects,
        ImmutableArray<Capability> capabilities,
        string runId,
        CancellationToken cancellationToken = default,
        int maxIterations = 5,
        Func<ImmutableArray<Observation>, ViewportExplorationEvidence>? viewportExplorationEvaluator = null,
        bool enableDeferredReconciliation = false)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (_state != RunState.Idle)
            throw new InvalidOperationException("Agent has already executed a Run.");

        using var span = RuntimeObservability.StartSpan(
            "RunSemanticGoal", ObservabilityLayer.Agent, ObservabilityComponent.AgentExecution);
        RuntimeObservability.SetTag(span, "goal", $"{goal.ObjectIdentity}.{goal.StateDimension}={goal.DesiredValue}");
        RuntimeObservability.SetTag(span, "runId", runId);

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

            // ── 1.5 LOCAL OBSTRUCTION (SC-P3-002) ────────────────────────
            // If the current Observation looks like an unexpected blocking surface
            // (same foreground, page unknown/not-mine), handle it as a bounded
            // local obstruction before any semantic commitment.
            if (container.IsLocalObstructionHypothesis(
                    observation,
                    _belief?.SemanticPage,
                    ready.Anchor.ApplicationIdentity))
            {
                var handled = await TryHandleLocalObstructionAsync(
                    container, observation, ready, runId, cancellationToken);
                if (handled)
                    continue; // fresh Observation + reconciled Container, re-evaluate SAME Goal
                // If not handled, fall through to existing failure path below.
            }

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
            {
                // ── 2.5a CURRENT-CONTAINER VIEWPORT EXPLORATION (SC-P3-003 / SC-P3-CAND-007) ──
                // Target-agnostic and evidence-driven: only when a caller-injected evaluator
                // positively justifies ONE further movement on the CURRENT container do we
                // scroll. Scroll is NOT a Container transition; the SAME goal is re-evaluated
                // after same-Container continuity is proven. Evaluator absent → this phase is
                // skipped entirely and the existing navigation path is preserved (zero regression).
                if (viewportExplorationEvaluator is not null)
                {
                    var exploration = EvaluateViewportExploration(viewportExplorationEvaluator, container, runId, null);
                    if (exploration.ContinueExploration == true)
                    {
                        _trace.Add(new TraceEvent(runId)
                        {
                            ContainerId = container.SemanticPageName,
                            Reason = "viewport exploration decision: ScrollForward (current Container)",
                        });
                        var scrollStep = await _traversal.ExecuteLoweredActionAsync(
                            new DeviceAction.ScrollForward(), observation);
                        var scrollJournal = _traversal.Journal[^1];
                        if (scrollStep is TraversalStepResult.Failed scrollFailed
                            || scrollJournal.PostActionObservation is null)
                            return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(
                                scrollStep is TraversalStepResult.Failed failure
                                    ? failure.Reason
                                    : "Scroll action did not yield a fresh observation."));

                        // Fresh observation is MANDATORY after every ScrollForward.
                        // Old viewport-local grounding becomes stale immediately.
                        var scrollObs = scrollJournal.PostActionObservation;
                        var scrollBelief = Reconcile.FromObservation(scrollObs, _resolveSemanticPage);

                        // ── DEFERRED_BOUNDED EXPLORATION ──
                        // Full semantic reconciliation may be deferred for latency reasons.
                        // While deferred, only exploration-safe actions (ScrollForward) are
                        // allowed; semantic commitments (SetSwitch, Tap, completion) are forbidden.
                        // Reconciliation is MANDATORY before any semantic action.
                        if (enableDeferredReconciliation && (_postScrollContinuityUnverified || _deferredScrollCount > 0))
                        {
                            // Already in deferred mode: perform cheap drift check on the
                            // already-obtained fresh Observation. No additional screenshots,
                            // no LLM, no repeated perception.
                            var drift = PerformCheapDriftCheck(scrollObs, scrollBelief, container, ready.Anchor.ApplicationIdentity);
                            if (drift.IsDrift)
                            {
                                _trace.Add(new TraceEvent(runId)
                                {
                                    ContainerId = container.SemanticPageName,
                                    Reason = $"deferred scroll drift detected: {drift.Reason}; performing mandatory checkpoint reconciliation.",
                                });
                                // Drift detected: abort deferred window, perform full reconciliation.
                                var checkpointResult = PerformSemanticCheckpoint(
                                    goal, scrollObs, scrollBelief, container, ready, runId);
                                if (checkpointResult is not null)
                                    return checkpointResult; // failure
                                continue; // re-evaluate SAME goal
                            }

                            // Increment deferred count and check budget
                            _deferredScrollCount++;
                            if (_deferredScrollCount > MaxDeferredScrolls)
                            {
                                _trace.Add(new TraceEvent(runId)
                                {
                                    ContainerId = container.SemanticPageName,
                                    Reason = $"deferred scroll budget exhausted ({_deferredScrollCount} > {MaxDeferredScrolls}); performing mandatory checkpoint reconciliation.",
                                });
                                var checkpointResult = PerformSemanticCheckpoint(
                                    goal, scrollObs, scrollBelief, container, ready, runId);
                                if (checkpointResult is not null)
                                    return checkpointResult; // failure
                                continue; // re-evaluate SAME goal
                            }

                            // Deferred: accept the fresh observation as exploration-only evidence.
                            // Do NOT perform full TryVerifyViewportContinuity (which would require
                            // page resolution). Do NOT claim continuity is verified.
                            // The observation is appended to viewport exploration evidence for
                            // the evaluator, but semantic continuity remains UNVERIFIED.
                            container.TryVerifyViewportContinuity(
                                scrollObs,
                                scrollBelief.SemanticPage,
                                ready.Anchor.ApplicationIdentity);
                            observation = scrollObs;
                            _belief = scrollBelief;
                            // Invalidates old grounding: refresh evidence from fresh observation.
                            RefreshContainerEvidence(container, scrollObs);
                            RecordDispatchedStep(runId, container, scrollJournal);
                            _trace.Add(new TraceEvent(runId)
                            {
                                ContainerId = container.SemanticPageName,
                                Reason = $"deferred scroll #{_deferredScrollCount} accepted; continuity UNVERIFIED, target still absent.",
                            });
                            continue; // re-evaluate the SAME goal on the SAME container
                        }

                        // ── STRICT RECONCILIATION (default) ──
                        // Full semantic reconciliation after every ScrollForward.
                        // Same-Container continuity: fresh sequence advanced (Traversal-enforced),
                        // foreground compatible, IsStillMine, same reconciled semantic page.
                        if (!container.TryVerifyViewportContinuity(
                                scrollObs,
                                scrollBelief.SemanticPage,
                                ready.Anchor.ApplicationIdentity))
                        {
                            // F5: External world wins. If the fresh observation resolves to a
                            // DIFFERENT KNOWN semantic page, use existing multi-level reconciliation.
                            // If unknown or same page but continuity failed, fail closed.
                            var reconcileResult = ReconcilePostScrollContinuityFailure(
                                scrollObs, scrollBelief, container, ready, runId);
                            if (reconcileResult is not null)
                                return reconcileResult; // failure or transition
                            continue; // re-evaluate SAME goal on new container
                        }

                        // Full reconciliation succeeded: same Container, continuity verified.
                        // Fresh binding: refresh the semantic snapshot from the fresh observation.
                        observation = scrollObs;
                        _belief = scrollBelief;
                        RefreshContainerEvidence(container, scrollObs);
                        RecordDispatchedStep(runId, container, scrollJournal);
                        continue; // re-evaluate the SAME goal on the SAME container // re-evaluate the SAME goal on the SAME container
                    }

                    // Start deferred mode if not already in it and evaluator says continue
                    if (exploration.ContinueExploration == true)
                    {
                        // First scroll: enter deferred mode
                        _postScrollContinuityUnverified = true;
                        _deferredScrollCount = 1;
                        continue;
                    }

                    if (exploration.ContinueExploration is null)
                        return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                            $"Viewport exploration for '{goal.ObjectIdentity}' is unresolved on page '{container.SemanticPageName}'; refusing to guess."));
                    // exploration.ContinueExploration == false (exhausted): do NOT scroll;
                    // fall through to the existing navigation path (which fails closed if no
                    // navigation candidate exists — bounded stop, no fabricated progress).
                }

                // ── 2.5 NAVIGATION (D1): goal object is not bound on this Container ──
                // The Agent evaluates the CURRENT fresh observation + declared page
                // recognition knowledge (PageAnalysisCriteria — knowledge, never an
                // encoded route) to select the unique known, non-current page whose
                // positive anchors are present and whose negative anchors are absent.
                // Uniqueness of the page AND of the anchor element IS the authorization
                // condition (D3): 0 candidates or multiple candidates → fail closed (F1).
                var nextPage = ResolveNavigationPage(observation, container.SemanticPageName);
                if (nextPage is null)
                    return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                        $"No binding for '{goal.ObjectIdentity}' and no unique navigation target from page '{container.SemanticPageName}'."));

                // Anchor element: the unique row carrying the target page's anchor text.
                // Perception may split one row into title + summary boxes carrying the
                // same text; same-row-band duplicates count as ONE anchor. Distinct
                // rows sharing the text = genuine ambiguity -> fail closed (no guessing).
                var anchor = ResolveNavigationAnchor(observation, nextPage);
                if (anchor is null)
                    return FailSemantic(runId, new SemanticRunResult.BindingUnresolved(
                        $"Navigation target '{nextPage}' has no unique anchor element on page '{container.SemanticPageName}'."));

                _trace.Add(new TraceEvent(runId) { Reason = $"navigation decision: {nextPage} (anchor '{anchor.Text}')" });
                var navigationStep = await _traversal.ExecuteLoweredActionAsync(
                    new DeviceAction.Tap(anchor.Index, anchor.Bounds), observation);
                var navigationJournal = _traversal.Journal[^1];
                if (navigationStep is TraversalStepResult.Failed navigationFailed
                    || navigationJournal.PostActionObservation is null)
                    return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(
                        navigationStep is TraversalStepResult.Failed failure
                            ? failure.Reason
                            : "Navigation action did not yield a fresh observation."));

                // D5 per-hop verification: fresh observation sequence advanced (enforced
                // by Traversal), fresh page identity == expected next page, page CHANGED,
                // and the old Container no longer claims the observation. Dispatch
                // receipt alone is NOT progress (F2).
                var navigationObs = navigationJournal.PostActionObservation;
                var navigationBelief = Reconcile.FromObservation(navigationObs, _resolveSemanticPage);
                if (!ProvesNavigationTransition(navigationBelief, navigationObs, nextPage, container))
                {
                    // 真实 UI 转场动画窗口：立即观测可能捕获两页共存的中间帧，页面身份无法唯一
                    // 融合（现场证据：live run post-action 帧 switch 处于移动位置）。这与「世界未变」
                    // 语义不同 — 故以有界 settle + 仅重观测（零重发、零新 journal 条目、零动作）重新取证；
                    // 每次重观测都重新 reconcile，页面身份证明转场才接受。耗尽仍未证明 → 原原因 fail
                    // closed（F2 语义保持：页面真正未变 → 重观测后仍不证明 → 零推进、拒盲重发）。
                    var proved = false;
                    for (int attempt = 1; attempt <= NavigationReobserveAttempts && !proved; attempt++)
                    {
                        await Task.Delay(NavigationTransitionSettle, cancellationToken);
                        var settledObs = await _observeInitial(cancellationToken);
                        var settledBelief = Reconcile.FromObservation(settledObs, _resolveSemanticPage);
                        _trace.Add(new TraceEvent(runId) { Reason =
                            $"navigation transition re-observe #{attempt}: seq={settledObs.SequenceNumber} page={settledBelief.SemanticPage ?? "(unknown)"}" });
                        if (ProvesNavigationTransition(settledBelief, settledObs, nextPage, container))
                        {
                            navigationObs = settledObs;
                            navigationBelief = settledBelief;
                            proved = true;
                        }
                    }
                    if (!proved)
                        return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(
                            $"Navigation dispatch did not prove a fresh Container transition to '{nextPage}'; refusing blind redispatch."));
                }

                // Container transition (D2): the new Container derives ONLY from the
                // fresh observation — old bindings / state beliefs / element indexes do
                // not survive (Bind resets, RefreshSemanticSnapshot replaces).
                observation = navigationObs;
                _belief = navigationBelief;
                _navigationEvidence.Add(navigationObs); // 宿主独立佐证：Agent 实际接受的 fresh 观测
                _activeContainer = CreateContainer(nextPage);
                _activeContainer.Bind(navigationObs);
                RefreshContainerEvidence(_activeContainer, navigationObs);
                RecordDispatchedStep(runId, container, navigationJournal);
                _trace.Add(new TraceEvent(runId) { ContainerId = _activeContainer.SemanticPageName });
                // Reset deferred state since we performed a navigation transition
                _postScrollContinuityUnverified = false;
                _deferredScrollCount = 0;
                // Navigation transitions always reset deferred state
                continue; // re-evaluate the SAME goal on the new Container
            }

            if (currentBelief is null)
                return FailSemantic(runId, new SemanticRunResult.StateEvidenceRequired(
                    $"'{stateKey}' is UNKNOWN — cannot safely dispatch."));

            // ── 2.5b CHECKPOINT before semantic action ──
            // If we are in deferred mode (post-scroll continuity unverified),
            // perform mandatory checkpoint reconciliation before any semantic commitment.
            if (enableDeferredReconciliation && _postScrollContinuityUnverified)
            {
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = container.SemanticPageName,
                    Reason = "checkpoint: performing mandatory reconciliation before semantic action.",
                });
                var checkpointResult = PerformSemanticCheckpoint(
                    goal, observation, _belief, container, ready, runId);
                if (checkpointResult is not null)
                    return checkpointResult; // failure
                continue; // re-evaluate SAME goal after checkpoint
            }

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
                    var step = await _traversal.ExecuteLoweredActionAsync(dispatched.Action, observation);
                    var journal = _traversal.Journal[^1];
                    if (step is TraversalStepResult.Failed failed || journal.PostActionObservation is null)
                        return FailSemantic(runId, new SemanticRunResult.ExecutionFailed(
                            step is TraversalStepResult.Failed failure ? failure.Reason : "Semantic action did not yield fresh observation."));
                    var freshObs = journal.PostActionObservation;
                    var freshBelief = Reconcile.FromObservation(freshObs, _resolveSemanticPage);
                    if (container.TryVerifyLocalContinuity(
                            freshObs,
                            freshBelief.SemanticPage,
                            ready.Anchor.ApplicationIdentity) == false)
                    {
                        var reconcileResult = ReconcileKnownPageTransition(
                            freshObs, freshBelief, container, ready, runId,
                            "Post-action unexpected navigation");
                        if (reconcileResult is not null)
                            return reconcileResult;
                        continue; // re-evaluate SAME Goal on new Container
                    }
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

    /// <summary>
    /// D1 step 2: resolve the unique known, non-current navigation candidate page.
    /// A candidate page Q (Q ≠ current) is recognized when the current observation
    /// yields Supports evidence for "page is Q" (TEXT_ANCHOR / SWITCH_DISTRIBUTION)
    /// and no Contradicts evidence for it (TEXT_ANCHOR_NEGATIVE). Exactly one such
    /// page → return it; zero or multiple → null (fail closed — F1).
    /// Pure recognition: consumes only observation + caller-injected criteria.
    /// </summary>
    private string? ResolveNavigationPage(Observation observation, string currentPage)
    {
        var evidence = PageAnalysis.Analyze(observation, _pageAnalysisCriteria!);
        var candidates = new List<string>();
        var contradicted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in evidence)
        {
            if (!item.Claim.StartsWith("page is ", StringComparison.Ordinal))
                continue;
            var pageName = item.Claim["page is ".Length..];
            if (item.Source == "TEXT_ANCHOR_NEGATIVE" && item.Stance == SemanticEvidenceStance.Contradicts)
            {
                contradicted.Add(pageName);
                continue;
            }
            if ((item.Source == "TEXT_ANCHOR" || item.Source == "SWITCH_DISTRIBUTION")
                && item.Stance == SemanticEvidenceStance.Supports
                && !string.Equals(pageName, currentPage, StringComparison.Ordinal)
                && !candidates.Contains(pageName, StringComparer.Ordinal))
                candidates.Add(pageName);
        }
        var valid = candidates.Where(p => !contradicted.Contains(p)).ToArray();
        return valid.Length == 1 ? valid[0] : null;
    }

    /// <summary>
    /// D1 step 3: locate the anchor element for the navigation target page.
    /// Anchor texts = target page's positive anchors (TEXT + SwitchState anchor sets).
    /// Multiple matching elements are tolerated ONLY when they belong to the SAME row
    /// band (vertical overlap or small adjacency gap — the calibrated title/summary
    /// duplicate artifact of one UI row); the row's largest element is the tap target.
    /// Matches spanning DISTINCT row bands → null (ambiguous; fail closed, no guessing).
    /// </summary>
    private ObservedElement? ResolveNavigationAnchor(Observation observation, string page)
    {
        var anchors = new List<string>();
        if (_pageAnalysisCriteria!.PageAnchors.TryGetValue(page, out var textAnchors))
            anchors.AddRange(textAnchors);
        if (_pageAnalysisCriteria.PageSwitchStateAnchors is { } switchAnchors
            && switchAnchors.TryGetValue(page, out var switchTexts))
            anchors.AddRange(switchTexts);

        var matches = observation.Elements
            .Where(e => anchors.Contains(e.Text, StringComparer.Ordinal))
            .ToArray();
        if (matches.Length == 0)
            return null;

        var bands = new List<List<ObservedElement>>();
        foreach (var match in matches.OrderBy(e => e.Bounds?.Y1 ?? float.MaxValue))
        {
            var band = bands.FirstOrDefault(b => SameRowBand(b[0].Bounds, match.Bounds));
            if (band is null)
                bands.Add([match]);
            else
                band.Add(match);
        }
        if (bands.Count != 1)
            return null;
        return bands[0].OrderByDescending(e => BoundsArea(e.Bounds)).First();
    }

    /// <summary>Same-row-band predicate: vertical overlap OR small adjacency gap
    /// (calibrated for the title + summary lines of one Settings row).</summary>
    private static bool SameRowBand(ElementBounds? a, ElementBounds? b)
    {
        if (a is null || b is null)
            return false;
        const float rowBandTolerance = 0.02f; // 归一化容差（≈ 半行高；现场校准值）
        return a.Y1 <= b.Y2 + rowBandTolerance && b.Y1 <= a.Y2 + rowBandTolerance;
    }

    private static double BoundsArea(ElementBounds? bounds)
        => bounds is null ? 0d : (bounds.X2 - bounds.X1) * (bounds.Y2 - bounds.Y1);

    /// <summary>
    /// D5 per-hop transition proof: fresh page identity == expected next page, page CHANGED
    /// (not the current container's page), and the old Container no longer claims the
    /// observation. Dispatch receipt alone is not progress (F2); this is the semantic proof.
    /// </summary>
    private static bool ProvesNavigationTransition(
        WorldBelief belief, Observation observation, string nextPage, RuntimeContainer container)
        => belief.SemanticPage is not null
            && string.Equals(belief.SemanticPage, nextPage, StringComparison.Ordinal)
            && !string.Equals(belief.SemanticPage, container.SemanticPageName, StringComparison.Ordinal)
            && !container.IsStillMine(observation);

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

    /// <summary>
    /// Result of a cheap drift check performed during deferred scrolling.
    /// Only uses the already-obtained fresh Observation.
    /// </summary>
    private sealed record DriftCheckResult(bool IsDrift, string Reason);

    /// <summary>
    /// Perform cheap deterministic drift checks using the SAME fresh Observation
    /// already obtained after ScrollForward. No additional screenshots, no LLM,
    /// no repeated perception.
    /// </summary>
    private DriftCheckResult PerformCheapDriftCheck(
        Observation observation,
        WorldBelief belief,
        RuntimeContainer container,
        string expectedForeground)
    {
        // Check 1: foreground application changed
        if (!string.Equals(observation.ForegroundApplication, expectedForeground, StringComparison.Ordinal))
        {
            return new DriftCheckResult(true,
                $"Foreground application changed from '{expectedForeground}' to '{observation.ForegroundApplication}'.");
        }

        // Check 2: obvious popup/system window appeared (foreground compatible but page unknown)
        if (belief.SemanticPage is null
            && string.Equals(observation.ForegroundApplication, expectedForeground, StringComparison.Ordinal))
        {
            return new DriftCheckResult(true,
                "Foreground unchanged but semantic page is unknown — possible popup/system window.");
        }

        // Check 3: strong contradiction to current Container identity
        if (!container.IsStillMine(observation) && belief.SemanticPage is not null)
        {
            return new DriftCheckResult(true,
                $"Observation contradicts current Container '{container.SemanticPageName}' (resolved to '{belief.SemanticPage}').");
        }

        // No drift detected
        return new DriftCheckResult(false, string.Empty);
    }

    /// <summary>
    /// Perform mandatory semantic checkpoint reconciliation.
    /// Called when deferred scroll budget exhausted, drift detected, or target becomes visible.
    /// Returns null if reconciliation succeeded (goal continues), or a SemanticRunResult if the run should terminate.
    /// </summary>
    private SemanticRunResult? PerformSemanticCheckpoint(
        SemanticGoalInput goal,
        Observation scrollObs,
        WorldBelief scrollBelief,
        RuntimeContainer container,
        StartupResult.Ready ready,
        string runId)
    {
        // CASE A: Same Container confirmed
        if (container.TryVerifyViewportContinuity(
                scrollObs,
                scrollBelief.SemanticPage,
                ready.Anchor.ApplicationIdentity))
        {
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = $"checkpoint: same Container '{container.SemanticPageName}' confirmed.",
            });
            _postScrollContinuityUnverified = false;
            _deferredScrollCount = 0;
            return null;
        }

        // CASE B: Different known page
        if (scrollBelief.SemanticPage is not null
            && !string.Equals(scrollBelief.SemanticPage, container.SemanticPageName, StringComparison.Ordinal))
        {
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = $"checkpoint: external world transition from '{container.SemanticPageName}' to '{scrollBelief.SemanticPage}'.",
            });
            _navigationEvidence.Add(scrollObs);
            _activeContainer = CreateContainer(scrollBelief.SemanticPage);
            _activeContainer.Bind(scrollObs);
            RefreshContainerEvidence(_activeContainer, scrollObs);
            _postScrollContinuityUnverified = false;
            _deferredScrollCount = 0;
            return null;
        }

        // CASE C: Unknown page
        if (scrollBelief.SemanticPage is null)
        {
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = "checkpoint: semantic page unresolved (unknown).",
            });
            _postScrollContinuityUnverified = false;
            _deferredScrollCount = 0;
            return FailSemantic(runId, new SemanticRunResult.SemanticContradiction(
                "Checkpoint reconciliation: semantic page unresolved after scroll."));
        }

        // CASE D: Same page claimed but continuity cannot be proven
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = container.SemanticPageName,
            Reason = "checkpoint: same Container claimed but continuity could not be proven.",
        });
        _postScrollContinuityUnverified = false;
        _deferredScrollCount = 0;
        return FailSemantic(runId, new SemanticRunResult.SemanticContradiction(
            $"Checkpoint reconciliation: Container continuity '{container.SemanticPageName}' could not be proven."));
    }

    /// <summary>
    /// Shared known-page reconciliation for continuity mismatches.
    /// If fresh Observation resolves to a DIFFERENT KNOWN page, create/reconcile
    /// a new Container and continue SAME Goal. Otherwise fail closed.
    /// </summary>
    private SemanticRunResult? ReconcileKnownPageTransition(
        Observation freshObs,
        WorldBelief freshBelief,
        RuntimeContainer oldContainer,
        StartupResult.Ready ready,
        string runId,
        string context)
    {
        // CASE B: Different known page — external world wins
        if (freshBelief.SemanticPage is not null
            && string.Equals(freshBelief.SemanticPage, oldContainer.SemanticPageName, StringComparison.Ordinal) == false)
        {
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = oldContainer.SemanticPageName,
                Reason = $"{context}: external world transition from '{oldContainer.SemanticPageName}' to '{freshBelief.SemanticPage}'.",
            });
            _navigationEvidence.Add(freshObs);
            _activeContainer = CreateContainer(freshBelief.SemanticPage);
            _activeContainer.Bind(freshObs);
            RefreshContainerEvidence(_activeContainer, freshObs);
            _postScrollContinuityUnverified = false;
            _deferredScrollCount = 0;
            return null;
        }

        // CASE C: Unknown page — fail closed
        if (freshBelief.SemanticPage is null)
        {
            _postScrollContinuityUnverified = false;
            _deferredScrollCount = 0;
            return FailSemantic(runId, new SemanticRunResult.SemanticContradiction(
                $"{context}: semantic page unresolved."));
        }

        // CASE D: Same page claimed but continuity cannot be proven — fail closed
        _postScrollContinuityUnverified = false;
        _deferredScrollCount = 0;
        return FailSemantic(runId, new SemanticRunResult.SemanticContradiction(
            $"{context}: Container '{oldContainer.SemanticPageName}' continuity could not be proven."));
    }

    /// <summary>
    /// Handle post-scroll continuity failure (F5).
    /// Uses the shared known-page reconciliation mechanism.
    /// </summary>
    private SemanticRunResult? ReconcilePostScrollContinuityFailure(
        Observation scrollObs,
        WorldBelief scrollBelief,
        RuntimeContainer container,
        StartupResult.Ready ready,
        string runId)
        => ReconcileKnownPageTransition(
            scrollObs,
            scrollBelief,
            container,
            ready,
            runId,
            "Post-scroll continuity failure");

    /// <summary>
    /// Attempt bounded handling of a local obstruction (popup/dialog/overlay).
    /// Uses existing Container local-obstruction semantics.
    /// </summary>
    private async Task<bool> TryHandleLocalObstructionAsync(
        RuntimeContainer container,
        Observation observation,
        StartupResult.Ready ready,
        string runId,
        CancellationToken cancellationToken)
    {
        var dismiss = observation.Elements
            .FirstOrDefault(e =>
                string.Equals(e.Text, "Dismiss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Text, "OK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Text, "Back", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Text, "Cancel", StringComparison.OrdinalIgnoreCase));
        if (dismiss is null)
        {
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = "local obstruction detected but no dismiss/back element found; falling through to existing semantics.",
            });
            return false;
        }

        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = container.SemanticPageName,
            Reason = "local obstruction handling: dismissing '" + dismiss.Text + "'.",
        });

        var step = await _traversal.ExecuteLoweredActionAsync(
            new DeviceAction.Tap(dismiss.Index, dismiss.Bounds), observation);
        var journal = _traversal.Journal[^1];
        if (step is TraversalStepResult.Failed || journal.PostActionObservation is null)
        {
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = "local obstruction dismiss dispatch failed.",
            });
            return false;
        }

        var freshObs = journal.PostActionObservation;
        var freshBelief = Reconcile.FromObservation(freshObs, _resolveSemanticPage);

        var cleared = container.TryVerifyLocalContinuity(
            freshObs,
            freshBelief.SemanticPage,
            ready.Anchor.ApplicationIdentity);
        if (cleared == false)
        {
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = "local obstruction dismiss verification failed: page=" + (freshBelief.SemanticPage ?? "Unknown") + ", seq=" + freshObs.SequenceNumber + ".",
            });
            return false;
        }

        observation = freshObs;
        _belief = freshBelief;
        RefreshContainerEvidence(container, freshObs);
        RecordDispatchedStep(runId, container, journal);

        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = container.SemanticPageName,
            Reason = "local obstruction cleared (seq=" + freshObs.SequenceNumber + "); SAME Goal continues.",
        });
        return true;
    }

}
