using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    /// <summary>
    /// 执行一次 Run（Phase 1：一个 Agent 实例恰好对应一次 Run — I-2）：
    /// Idle → Initializing（Startup）→ Ready? → Running（bind / traverse / navigate 循环）→ Completed | Failed。
    /// 每次 post-action Observation 后：Reconcile → evidence evaluator 评估（SC-P1-003）；
    /// Satisfied → Completed（I-10——Plan 耗尽、dispatch 结果均不构成完成判定）；
    /// Plan 耗尽且无 Satisfied → Failed（显式原因，不是 Completed）；
    /// Traversal 步骤 Failed → Running → Failed（StepId + 显式原因，无恢复动作 — SC-P1-004）；
    /// Agent-scope drift（世界信念丢失，B1/B2）→ Trap 发射 + RecoveryAnchor 驱动恢复（B3）；
    /// 恢复验证失败 / 恢复后再次 drift → Failed（显式原因；单次恢复尝试 — HG-2）。
    /// 生命周期转移全部以 DecisionRecord 记录（RunState? / Reason? / Action? / ActionId / StepId / ContainerId）。
    /// </summary>
    /// <param name="goal">Goal（evidence evaluator 由调用侧注入 — 裁决 3）。</param>
    /// <param name="plan">执行计划（步数据由调用侧注入 — 裁决 11）。</param>
    /// <param name="runId">Run 标识（DecisionRecord.RunId；确定性重放 — SC-P1-001 断言 7）。</param>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>最终 RunState（Completed | Failed）。</returns>
    /// <exception cref="ArgumentNullException">goal 或 plan 为 null。</exception>
    /// <exception cref="ArgumentException">runId 为空或空白。</exception>
    /// <exception cref="InvalidOperationException">实例已执行过 Run；或 executor / containerFactory 违反协议。</exception>
    public async Task<RunState> RunAsync(Goal goal, Plan plan, string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (_state != RunState.Idle)
            throw new InvalidOperationException("Agent 已执行过 Run（Phase 1：一个实例恰好对应一次 Run；请新建实例）。");

        // ── Idle → Initializing（生命周期转移记录 — SC-P1-001 断言 1）───────────────────────────────────
        _trace.Add(new DecisionRecord(runId) { RunState = RunState.Idle });
        _trace.Add(new DecisionRecord(runId) { RunState = RunState.Initializing });
        _state = RunState.Initializing;

        // ── Initializing：Startup（§19；Ready 之前不得进入 Running — SC-P1-001 / SC-P1-002）──────────────
        var startupResult = await _startup.StartAsync(cancellationToken);
        if (startupResult is StartupResult.NotReady notReady)
        {
            return Fail(runId, notReady.Reason);
        }
        var ready = (StartupResult.Ready)startupResult;
        _recoveryAnchor = ready.Anchor;

        // ── Running：observeInitial → Reconcile → 建立初始容器（bind — §5）──────────────────────────────
        _trace.Add(new DecisionRecord(runId) { RunState = RunState.Running });
        _state = RunState.Running;
        var initialObservation = await _observeInitial(cancellationToken);
        if (!TryInitializeV2Belief(Reconcile.FromObservation(initialObservation, _resolveSemanticPage)))
            return Fail(runId, "Initial V2 Container state could not be prepared; refusing to start the Run.");
        StartRunActiveExecutionContext(CreateContainer(ready.Anchor.ExpectedSemanticEntry));
        ActiveExecutionContainerOrThrow.Bind(initialObservation);
        _trace.Add(new DecisionRecord(runId) { ContainerId = ActiveExecutionContainerOrThrow.SemanticPageName });
        InitializeBranchProgress(initialObservation, plan);

        // SC-P3-CAND-008 is an opt-in bounded protocol. It repeatedly consumes one complete
        // inventory receipt from the current Container evidence, independently authorizes at most
        // one required branch, executes one existing Tap, and reconciles a fresh child Container
        // before another decision. All depth/protocol bookkeeping is Run-local derived data; no
        // route/frontier/depth field or second semantic authority is introduced.
        if (goal.BranchInventoryEvaluator is not null)
        {
            // SC-P3-CAND-009 opt-in: the singular discovered-branch effect carrier scopes this Run to
            // the bounded main-loop revalidation flow instead of the CAND-008 discovery loop. The
            // required P inventory is still accepted only through the frozen CAND-008 acceptance gate
            // (bounded current same-Container evidence + validated source Observation sequences); the
            // carrier itself establishes no membership. Carrier absent keeps the frozen CAND-008 route.
            if (goal.DiscoveredBranchEffectCriterion is null)
            {
                return RunBoundedCrossPageDiscovery(
                    goal,
                    plan,
                    runId);
            }

            var currentContainer = ActiveExecutionContainerOrThrow
                ?? throw new InvalidOperationException("bounded revalidation 缺少 active Container。");
            var current = currentContainer.CurrentObservation
                ?? throw new InvalidOperationException("bounded revalidation Container 缺少当前 Observation。");
            var accepted = currentContainer.ViewportExplorationObservations;
            var inventory = goal.BranchInventoryEvaluator(accepted, 0)
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 返回 null：必须返回 BranchInventoryEvidence。");
            var inventoryOutcome = inventory.RequiredBranchEvidence switch
            {
                null => "unresolved",
                { Count: 0 } => "leaf",
                _ => "complete",
            };
            _trace.Add(new DecisionRecord(runId)
            {
                ContainerId = currentContainer.SemanticPageName,
                Reason = $"branch inventory {inventoryOutcome}: depth=0, source-seq={current.SequenceNumber}; {inventory.Reason}",
            });
            if (!TryAcceptBranchInventory(currentContainer, current, inventory, frozenNormalization: null, out _, out var inventoryFailure))
            {
                return Fail(runId, inventoryFailure!);
            }
        }

        // SC-P3-CAND-006：仅当 Goal 显式提供 bounded criterion 时，Agent 对同一 fresh
        // initial Observation 的 candidates 做一次稳定顺序分类。false/null 只留下无 Action 的
        // Trace evidence；first true 才可作为一个 transient Tap step 进入既有 Container/Traversal。
        // 该步骤不修改 immutable Plan，也不把 authorization 解释为 required work 或 completion。
        // A fixed CP12 grounding step is already a Plan hypothesis. It must not activate the frozen
        // CAND-006 discovered-candidate transient insertion path.
        var executionPlan = plan;
        var hasGroundedFixedStep = plan.Steps.Any(step => step.TargetGroundingCriterion is not null);
        if (!hasGroundedFixedStep
            && !TryBuildBoundedCandidateExecutionPlan(goal, initialObservation, plan, runId, out executionPlan))
        {
            return Fail(
                runId,
                $"Bounded candidate authorization 未产生可执行候选（seq={initialObservation.SequenceNumber}）；零 candidate dispatch。");
        }

        // ── CP-06：fresh post-Startup 初始 Observation 已满足 Goal 时，无需 dispatch 任何 Plan step 即可完成 ──
        //    语义与 Plan 长度无关（HUMAN_AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL）：
        //    Plan 存在不产生行动义务；empty / non-empty 一视同仁。
        var initialGoalEvidence = goal.EvidenceEvaluator(initialObservation);
        if (initialGoalEvidence.Satisfied)
        {
            return Complete(runId, initialGoalEvidence);
        }

        ViewportExplorationEvidence? viewportExplorationDecision = null;
        RuntimeContainer? viewportExplorationContainer = null;
        long? viewportExplorationSourceSequence = null;

        // ── Running 循环：bind / traverse / navigate（§5；B3 — 索引循环：drift 恢复需挂起 Plan 索引）──────
        for (int i = 0; i < executionPlan.Steps.Length; i++)
        {
            var step = executionPlan.Steps[i];
            var stepObservation = ActiveExecutionContainerOrThrow.CurrentObservation
                ?? throw new InvalidOperationException("active Container 缺少当前观测（协议违约：Bind 必须先于执行）。");
            var isViewportPlanStep = IsScrollForwardAction(step.ActionDescription);
            if (isViewportPlanStep && goal.ViewportExplorationEvaluator is not null)
            {
                var retainedEvidence = ActiveExecutionContainerOrThrow.ViewportExplorationObservations;
                var currentEvidenceSequence = retainedEvidence.IsDefaultOrEmpty
                    ? (long?)null
                    : retainedEvidence[^1].SequenceNumber;
                if (!ReferenceEquals(viewportExplorationContainer, ActiveExecutionContainerOrThrow)
                    || viewportExplorationSourceSequence != currentEvidenceSequence)
                {
                    viewportExplorationDecision = EvaluateViewportExploration(
                        goal,
                        ActiveExecutionContainerOrThrow,
                        runId,
                        stepId: null);
                    viewportExplorationContainer = ActiveExecutionContainerOrThrow;
                    viewportExplorationSourceSequence = currentEvidenceSequence;
                }

                if (viewportExplorationDecision!.ContinueExploration is null)
                {
                    return Fail(
                        runId,
                        $"Viewport exploration unresolved：{viewportExplorationDecision.Reason}；不 dispatch 下一次 viewport action。");
                }
                if (viewportExplorationDecision.ContinueExploration is false)
                {
                    var initialExhaustionGoalEvidence = goal.EvidenceEvaluator(stepObservation);
                    if (initialExhaustionGoalEvidence.Satisfied)
                        return Complete(runId, initialExhaustionGoalEvidence);
                    return Fail(
                        runId,
                        $"Viewport exploration positively exhausted，但 GoalEvidence 未满足：{viewportExplorationDecision.Reason}");
                }
            }
            var isLocalHandlingStep = ActiveExecutionContainerOrThrow.CanHandleLocalObstruction(
                stepObservation,
                Belief?.SemanticPage,
                _recoveryAnchor.ApplicationIdentity,
                step);
            var wasLocallyCompleteBeforeStep = ActiveExecutionContainerOrThrow.IsLocalComplete;
            var result = step.TargetGroundingCriterion is null
                ? ActiveExecutionContainerOrThrow.ExecuteStep(step)
                : ActiveExecutionContainerOrThrow.ExecuteStep(step, PrepareCandidateAuthorizationReceipts(goal, stepObservation, runId));
            var entry = LastJournalEntry();
            var isViewportStep = entry.DispatchedAction is DeviceAction.ScrollForward;
            if (result is TraversalStepResult.Failed failed)
            {
                if (isLocalHandlingStep)
                {
                    EmitContainerEscalation(
                        runId,
                        entry,
                        ActiveExecutionContainerOrThrow,
                        entry.PostActionObservation,
                        $"Container-scope local handling 未能完成连续性证明：{failed.Reason}");
                }
                if (isViewportStep)
                {
                    EmitViewportEscalation(
                        runId,
                        entry,
                        ActiveExecutionContainerOrThrow,
                        entry.PostActionObservation,
                        $"Viewport action 未能取得可接受的 fresh continuity evidence：{failed.Reason}");
                }
                // SC-P1-004：Agent 是最终 failure authority——StepId + 显式原因，无恢复动作
                return Fail(runId, failed.Reason, entry.StepId);
            }

            // Succeeded：读 Journal[^1]（StepId / 动作载荷 / post-action Observation — B6 组合模式）。
            // Action trace is appended after reconciliation so an unexpected
            // transition can remain correlated on this single causal Step
            // event without mutating an already-appended record.
            var postObservation = entry.PostActionObservation
                ?? throw new InvalidOperationException("step executor 返回 Succeeded 但未提供 post-action Observation（协议违约 — §3）。");

            // 每次 post-action Observation 后：Reconcile → prepare → commit（SC-P1-003）。
            // The successful same-Container paths all use the same atomic seam;
            // only unexpected foreign/Unknown observations intentionally omit a
            // Container acceptance while preserving execution/path.
            var currentContext = _activeContainerContext
                ?? throw new InvalidOperationException("post-action reconciliation requires an active execution context.");
            var previousBelief = Belief;
            var postBelief = Reconcile.FromObservation(postObservation, _resolveSemanticPage);
            var activeContainer = currentContext.ActiveExecutionContainer;
            ContainerTransition? postTransition = null;
            var sameContainer = string.Equals(
                postBelief.SemanticPage,
                activeContainer.SemanticPageName,
                StringComparison.Ordinal)
                && activeContainer.IsStillMine(postObservation);
            var unexpectedBuyer = postBelief.SemanticPage is null
                || (!sameContainer
                    && !string.Equals(
                        postBelief.SemanticPage,
                        activeContainer.SemanticPageName,
                        StringComparison.Ordinal));
            var viewportContinuityFailed = false;
            var localContinuityFailed = isLocalHandlingStep && !sameContainer;

            var sameContainerInput = new ContainerTransitionClassificationInput
            {
                RunId = runId,
                FromObservedLocation = previousBelief?.SemanticPage
                    ?? activeContainer.SemanticPageName,
                ToObservedLocation = postBelief.SemanticPage,
                ActiveExecutionContainer = activeContainer.SemanticPageName,
                ActiveParentAtObservation = currentContext.ActiveAncestorPath.IsDefaultOrEmpty
                    ? null
                    : currentContext.ActiveAncestorPath[^1].ParentExecutionContainer.SemanticPageName,
                FreshObservationRef = $"observation:{postObservation.SequenceNumber}",
                CompletenessRef = $"container:{activeContainer.SemanticPageName}:local-completeness",
                EvidenceRef = $"observation:{postObservation.SequenceNumber}",
            };

            if (isViewportStep || sameContainer)
            {
                if (!TryPrepareContainerReconciliation(
                        runId,
                        postObservation,
                        postBelief,
                        currentContext,
                        sameContainerInput,
                        observationContainer: activeContainer,
                        recordViewportObservation: isViewportStep,
                        candidateContext: currentContext,
                        candidateProgress: null,
                        expectedEnteredChildObligationIdentity: null,
                        progressReplacementIntent: ContainerProgressReplacementIntent.None,
                        out var sameContainerPreparation,
                        out var sameContainerFailure))
                {
                    if (isViewportStep)
                    {
                        viewportContinuityFailed = true;
                    }
                    else
                    {
                        _trace.Add(new DecisionRecord(runId)
                        {
                            ContainerId = activeContainer.SemanticPageName,
                            StepId = entry.StepId,
                            ActionId = $"Action-{++_actionCounter}",
                            Action = entry.DispatchedAction,
                        });
                        return Fail(
                            runId,
                            $"Post-action reconciliation preparation rejected: {sameContainerFailure}",
                            entry.StepId);
                    }
                }
                else
                {
                    postTransition = sameContainerPreparation!.Transition;
                    CommitContainerReconciliation(
                        sameContainerPreparation,
                        appendStandaloneTrace: false);
                }
            }
            else if (unexpectedBuyer)
            {
                var unexpectedInput = new ContainerTransitionClassificationInput
                {
                    RunId = runId,
                    FromObservedLocation = previousBelief?.SemanticPage
                        ?? activeContainer.SemanticPageName,
                    ToObservedLocation = postBelief.SemanticPage,
                    ActiveExecutionContainer = activeContainer.SemanticPageName,
                    ActiveParentAtObservation = currentContext.ActiveAncestorPath.IsDefaultOrEmpty
                        ? null
                        : currentContext.ActiveAncestorPath[^1].ParentExecutionContainer.SemanticPageName,
                    FreshObservationRef = $"observation:{postObservation.SequenceNumber}",
                    CompletenessRef = currentContext.ActiveAncestorPath.IsDefaultOrEmpty
                        ? null
                        : $"container:{activeContainer.SemanticPageName}:local-completeness",
                    EvidenceRef = $"observation:{postObservation.SequenceNumber}",
                };
                if (!TryPrepareContainerReconciliation(
                        runId,
                        postObservation,
                        postBelief,
                        currentContext,
                        unexpectedInput,
                        observationContainer: null,
                        recordViewportObservation: false,
                        candidateContext: currentContext,
                        candidateProgress: null,
                        expectedEnteredChildObligationIdentity: null,
                        progressReplacementIntent: ContainerProgressReplacementIntent.None,
                        out var unexpectedPreparation,
                        out var unexpectedFailure))
                {
                    _trace.Add(new DecisionRecord(runId)
                    {
                        ContainerId = activeContainer.SemanticPageName,
                        StepId = entry.StepId,
                        ActionId = $"Action-{++_actionCounter}",
                        Action = entry.DispatchedAction,
                    });
                    return Fail(runId,
                        $"Post-action reconciliation preparation rejected: {unexpectedFailure}",
                        entry.StepId);
                }
                postTransition = unexpectedPreparation!.Transition;
                CommitContainerReconciliation(unexpectedPreparation, appendStandaloneTrace: false);
            }
            else
            {
                // Foreign/identity-conflict observation: accept only
                // WorldBelief and typed evidence; leave the old Container
                // untouched so the established higher-scope rebind policy
                // remains in control.  This covers both a known non-parent
                // page and a same-name identity conflict.
                if (!TryPrepareContainerReconciliation(
                        runId,
                        postObservation,
                        postBelief,
                        currentContext,
                        sameContainerInput,
                        observationContainer: null,
                        recordViewportObservation: false,
                        candidateContext: currentContext,
                        candidateProgress: null,
                        expectedEnteredChildObligationIdentity: null,
                        progressReplacementIntent: ContainerProgressReplacementIntent.None,
                        out var foreignPreparation,
                        out var foreignFailure))
                {
                    _trace.Add(new DecisionRecord(runId)
                    {
                        ContainerId = activeContainer.SemanticPageName,
                        StepId = entry.StepId,
                        ActionId = $"Action-{++_actionCounter}",
                        Action = entry.DispatchedAction,
                    });
                    return Fail(
                        runId,
                        $"Post-action reconciliation preparation rejected: {foreignFailure}",
                        entry.StepId);
                }
                postTransition = foreignPreparation!.Transition;
                CommitContainerReconciliation(foreignPreparation, appendStandaloneTrace: false);
            }

            _trace.Add(new DecisionRecord(runId)
            {
                ContainerId = ActiveExecutionContainerOrThrow.SemanticPageName,
                StepId = entry.StepId,
                ActionId = $"Action-{++_actionCounter}",
                Action = entry.DispatchedAction,
                ContainerTransition = postTransition,
            });

            // PAGEANALYSIS INTEGRATION: 当 PageAnalysisCriteria 已注入时，
            // 从 Fresh Observation 派生多源 SemanticEvidence 并融合进 Container 局部信念。
            // Container 仍是局部语义状态的唯一 owner（I-2）；Agent 仅消费结果。
            if (_pageAnalysisCriteria is not null)
            {
                var pageEvidence = PageAnalysis.Analyze(postObservation, _pageAnalysisCriteria);
                ActiveExecutionContainerOrThrow.EvaluatePageBelief(postObservation, pageEvidence.ToArray());
            }

            // BINDINGANALYSIS INTEGRATION: 当 ElementBindingCriteria 已注入时，
            // 从 Fresh Observation 派生对象绑定证据并更新 Container 局部绑定状态。
            if (_elementBindingCriteria is not null)
            {
                var bindingEvidence = BindingAnalysis.Analyze(postObservation, _elementBindingCriteria);
                var bindings = BindingReconciler.Reconcile(
                    bindingEvidence, _elementBindingCriteria.KnownObjects);
                ActiveExecutionContainerOrThrow.UpdateBindings(bindings);
            }

            // SC-P3-003：viewport snapshot 变化与 dispatch outcome 都不证明 semantic navigation/continuity。
            // Container 只在 fresh + compatible foreground + IsStillMine + same reconciled page 时推进其
            // CurrentObservation；不 Bind，因而保留 local progress。失败先产生 Container-scope evidence，
            // 再由 Agent 独占 higher-scope response authority。
            if (isViewportStep && viewportContinuityFailed)
            {
                EmitViewportEscalation(
                    runId,
                    entry,
                    ActiveExecutionContainerOrThrow,
                    postObservation,
                    $"Viewport continuity 未获证明：foreground={postObservation.ForegroundApplication ?? "<null>"}, "
                    + $"semanticPage={postBelief.SemanticPage ?? "Unknown"}, seq={postObservation.SequenceNumber}。");
            }

            ViewportExplorationEvidence? postViewportExplorationDecision = null;
            if (isViewportStep
                && !viewportContinuityFailed
                && goal.ViewportExplorationEvaluator is not null)
            {
                postViewportExplorationDecision = EvaluateViewportExploration(
                    goal,
                    ActiveExecutionContainerOrThrow,
                    runId,
                    entry.StepId);
                viewportExplorationDecision = postViewportExplorationDecision;
                viewportExplorationContainer = ActiveExecutionContainerOrThrow;
                viewportExplorationSourceSequence = ActiveExecutionContainerOrThrow.ViewportExplorationObservations[^1].SequenceNumber;
                if (postViewportExplorationDecision.ContinueExploration is null)
                {
                    return Fail(
                        runId,
                        $"Viewport exploration unresolved：{postViewportExplorationDecision.Reason}；不 dispatch 下一次 viewport action。",
                        entry.StepId);
                }
            }

            // B1 — Agent-scope drift（HG-3：无 DriftStatus 字段；仅用既有表面，纯函数）：
            // 前台离开恢复入口基线 + 容器不再属于本页 + 语义页面 Unknown → 世界信念丢失
            // B2 — 结构化 Trap(Scope=Agent) 发射（A1 模型首个消费者）+ 独立 Trap 事件记录
            //      （I-13：Trap 只携带观测序号引用；Expected = 容器绑定观测 / Observed = drift 观测）
            // B3 — 发射 Trap 后进入 RecoveryAnchor 驱动的恢复流程（HG-4 Option B：机制在 Recovery 组件，
            //      决策在 Agent — 挂起索引 / 恢复验证 / 位置恢复 / 续跑）；不再以裸 Fail 终止
            var currentWorldBelief = Belief;
            if (!isViewportStep
                && currentWorldBelief is not null
                && IsAgentScopeDrift(postObservation, ActiveExecutionContainerOrThrow, currentWorldBelief))
            {
                EmitDriftTrap(runId, entry, postObservation, ActiveExecutionContainerOrThrow);
                return await RecoverFromDriftAsync(runId, goal, executionPlan, i, ActiveExecutionContainerOrThrow, postObservation, entry.StepId, cancellationToken);
            }

            // SC-P3-002：批准的 bounded local handling 之后，dispatch / Succeeded 都不证明连续性。
            // Container 仅在 fresh sequence + compatible foreground + existing identity rule + reconciled page
            // 共同成立时接受同一 Container；不调用 Bind，因此已有 local progress 保留。
            if (localContinuityFailed)
            {
                EmitContainerEscalation(
                    runId,
                    entry,
                    ActiveExecutionContainerOrThrow,
                    postObservation,
                    $"Container continuity 未获证明：foreground={postObservation.ForegroundApplication ?? "<null>"}, "
                    + $"semanticPage={postBelief.SemanticPage ?? "Unknown"}, seq={postObservation.SequenceNumber}。");
            }

            if (!isViewportStep && !isLocalHandlingStep)
            {
                RecordBranchCompletionBeforeReturn(
                    executionPlan,
                    i,
                    ActiveExecutionContainerOrThrow,
                    wasLocallyCompleteBeforeStep,
                    postObservation,
                    Belief?.SemanticPage);
            }

            var evidence = goal.EvidenceEvaluator(postObservation);
            if (evidence.Satisfied)
            {
                // I-10：仅 Satisfied 的 GoalEvidence 触发 Completed（dispatch 结果不构成完成判定 — 裁决 10）
                _trace.Add(new DecisionRecord(runId) { RunState = RunState.Completed, Reason = evidence.Reason });
                _state = RunState.Completed;
                _reason = evidence.Reason;
                return RunState.Completed;
            }

            if (postViewportExplorationDecision?.ContinueExploration is false)
            {
                return Fail(
                    runId,
                    $"Viewport exploration positively exhausted，但 GoalEvidence 未满足：{postViewportExplorationDecision.Reason}",
                    entry.StepId);
            }
            if (postViewportExplorationDecision?.ContinueExploration is true
                && !HasRemainingViewportStep(executionPlan, i))
            {
                return Fail(
                    runId,
                    $"Viewport exploration bound reached while fresh evidence still requires continuation：{postViewportExplorationDecision.Reason}；semantic exhaustion 未获证明。",
                    entry.StepId);
            }

            // viewport local proof 失败后不重新 dispatch，也不让 snapshot difference 隐式成为 navigation。
            // 若 semantic page 可解析，Agent 可显式 rebind；Unknown 则 Agent 以结构化 Container evidence
            // 为依据结束本 Run。原 Container 的 progress 保持不变。
            if (viewportContinuityFailed)
            {
                // Preserve the established higher-scope response: a failed
                // same-container proof may rebind to the fresh known page,
                // but only after the failed prepare has left Container state
                // untouched.
                if (!TryCommitFreshObservedLocation(
                        runId,
                        postObservation,
                        postBelief,
                        sameContainerContinuity: false,
                        out var v2Failure))
                    return Fail(
                        runId,
                        $"Viewport replacement observation could not be committed to V2: {v2Failure}",
                        entry.StepId);
                var higherScopePage = Belief?.SemanticPage;
                if (higherScopePage is null)
                {
                    return Fail(
                        runId,
                        $"Viewport movement 后无法证明 Container 连续性：观测（seq={postObservation.SequenceNumber}）语义页面 Unknown。",
                        entry.StepId);
                }
                ReplaceActiveExecutionContainer(CreateContainer(higherScopePage));
                ActiveExecutionContainerOrThrow.Bind(postObservation);
                _trace.Add(new DecisionRecord(runId) { ContainerId = ActiveExecutionContainerOrThrow.SemanticPageName });
                continue;
            }

            // local proof 已失败时不再次把同一证据分类为可处理 obstruction，避免 blind repeat。
            // Agent 仅使用既有 higher-scope outcome：已解析页面则 rebind；Unknown 则显式失败。
            if (localContinuityFailed)
            {
                var higherScopePage = Belief?.SemanticPage;
                if (higherScopePage is null)
                {
                    return Fail(
                        runId,
                        $"局部 obstruction 处理后无法证明 Container 连续性：观测（seq={postObservation.SequenceNumber}）语义页面 Unknown。",
                        entry.StepId);
                }
                ReplaceActiveExecutionContainer(CreateContainer(higherScopePage));
                ActiveExecutionContainerOrThrow.Bind(postObservation);
                _trace.Add(new DecisionRecord(runId) { ContainerId = ActiveExecutionContainerOrThrow.SemanticPageName });
                continue;
            }

            // 未满足：IsStillMine? → 是 → 下一步；否 → Navigate（容器切换判定 authority 在 Agent — I-3）
            if (!ActiveExecutionContainerOrThrow.IsStillMine(postObservation))
            {
                // 同一前台 + Unknown + identity 不接受，只构成 Container-scope obstruction hypothesis。
                // 仅当计划中的下一步可由当前候选 grounding 时，Container 接受 fresh obstruction Observation
                // 供一次 bounded handling；不 Bind、不清空 progress、不调用 Recovery。
                var currentSemanticPage = Belief?.SemanticPage;
                if (ActiveExecutionContainerOrThrow.IsLocalObstructionHypothesis(
                        postObservation,
                        currentSemanticPage,
                        _recoveryAnchor.ApplicationIdentity))
                {
                    if (i + 1 < executionPlan.Steps.Length
                        && ActiveExecutionContainerOrThrow.TryAcceptLocalObstruction(
                            postObservation,
                            currentSemanticPage,
                            _recoveryAnchor.ApplicationIdentity,
                            executionPlan.Steps[i + 1]))
                    {
                        continue;
                    }
                }

                var newPage = currentSemanticPage;
                if (newPage is null)
                {
                    return Fail(runId, $"Navigate 无法继续：观测（seq={postObservation.SequenceNumber}）无法解析新语义页面（Unknown — §10）。");
                }
                ReplaceActiveExecutionContainer(CreateContainer(newPage));
                ActiveExecutionContainerOrThrow.Bind(postObservation);
                _trace.Add(new DecisionRecord(runId) { ContainerId = ActiveExecutionContainerOrThrow.SemanticPageName });
            }
        }

        // ── Plan 耗尽且无 Satisfied 证据：Failed（显式原因；不是 Completed — SC-P1-003 负向）─────────────
        return Fail(runId, $"Plan 步数耗尽但 Goal 证据未满足：最后一次证据评估（seq={Belief?.SourceObservationSequence}）Satisfied=false。");
    }
    /// <summary>
    /// SC-P3-CAND-006 one-Observation bounded classification. Agent alone consumes the Goal criterion;
    /// lower scopes receive only the first authorized candidate as an existing Tap protocol step.
    /// Rejected/unresolved candidates remain pre-dispatch Trace evidence and never enter Traversal.
    /// </summary>
    private bool TryBuildBoundedCandidateExecutionPlan(
        Goal goal,
        Observation observation,
        Plan plan,
        string runId,
        out Plan executionPlan)
    {
        executionPlan = plan;
        var evaluator = goal.CandidateAuthorizationEvaluator;
        if (evaluator is null)
            return true;

        ObservedElement? firstAuthorized = null;
        foreach (var candidate in observation.Elements)
        {
            var authorization = evaluator(observation, candidate)
                ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence：必须返回三值 Authorized 与非空 Reason。");
            if (authorization.Authorized is true)
            {
                firstAuthorized ??= candidate;
                continue;
            }

            var outcome = authorization.Authorized is false ? "rejected" : "unresolved";
            _trace.Add(new DecisionRecord(runId)
            {
                ContainerId = ActiveExecutionContainerOrThrow?.SemanticPageName,
                Reason = $"bounded candidate {outcome}: text={candidate.Text}, index={candidate.Index}, "
                    + $"source-seq={observation.SequenceNumber}; {authorization.Reason}",
            });
        }

        if (firstAuthorized is null)
            return false;

        executionPlan = new Plan(plan.Steps.Insert(
            0,
            new PlanStep(firstAuthorized.Text, "Tap")));
        return true;
    }

    /// <summary>
    /// CP12 preparation only: Agent evaluates its existing safety criterion over the immutable current
    /// candidate snapshot. It does not select a target, construct an action, or verify local effects.
    /// </summary>
    private ImmutableDictionary<int, CandidateAuthorizationEvidence> PrepareCandidateAuthorizationReceipts(
        Goal goal,
        Observation observation,
        string runId)
    {
        var evaluator = goal.CandidateAuthorizationEvaluator;
        if (evaluator is null)
            return ImmutableDictionary<int, CandidateAuthorizationEvidence>.Empty;
        var receipts = ImmutableDictionary.CreateBuilder<int, CandidateAuthorizationEvidence>();
        foreach (var candidate in observation.Elements)
        {
            var receipt = evaluator(observation, candidate)
                ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence：必须返回 immutable safety receipt。");
            receipts.Add(candidate.Index, receipt);
            if (receipt.Authorized is not true)
            {
                var outcome = receipt.Authorized is false ? "rejected" : "unresolved";
                _trace.Add(new DecisionRecord(runId)
                {
                    ContainerId = ActiveExecutionContainerOrThrow?.SemanticPageName,
                    Reason = $"bounded candidate {outcome}: text={candidate.Text}, index={candidate.Index}, "
                        + $"source-seq={observation.SequenceNumber}; {receipt.Reason}",
                });
            }
        }
        return receipts.ToImmutable();
    }
    private static bool HasRemainingViewportStep(Plan plan, int completedStepIndex)
        => plan.Steps
            .Skip(completedStepIndex + 1)
            .Any(step => IsScrollForwardAction(step.ActionDescription));

    /// <summary>
    /// 记录 Container 构造的现有 Trap vocabulary evidence；Agent 仅拥有观察面与 higher-scope response，
    /// 不把 Container-scope proof failure 解释为 Goal success 或自动 Recovery。
    /// </summary>
    private void EmitContainerEscalation(
        string runId,
        TraversalJournalEntry entry,
        RuntimeContainer container,
        Observation? observed,
        string evidence)
    {
        _lastTrap = container.CreateLocalObstructionEscalation(observed, entry.DispatchedAction, evidence);
        _trace.Add(new DecisionRecord(runId)
        {
            StepId = entry.StepId,
            ContainerId = container.SemanticPageName,
            TrapKind = _lastTrap.Kind,
            TrapScope = _lastTrap.Scope,
        });
    }
    /// <summary>
    /// SC-P3-CAND-004 bounded inventory: the initial parent Observation must freshly expose at least
    /// two distinct approved Tap targets. Plan limits the approved boundary but never proves presence.
    /// </summary>
    private void InitializeBranchProgress(Observation parentObservation, Plan plan)
    {
        var parentPage = Belief?.SemanticPage;
        if (parentPage is null)
            return;
        var approvedTargets = parentObservation.Elements
            .Select(element => element.Text)
            .Where(text => plan.Steps.Any(step =>
                string.Equals(step.TargetDescription, text, StringComparison.Ordinal)
                && string.Equals(step.ActionDescription, "Tap", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (approvedTargets.Length < 2)
            return;

        var inventory = approvedTargets.ToImmutableDictionary(
            identity => identity,
            _ => parentObservation.SequenceNumber,
            StringComparer.Ordinal);
        _branchProgress = _branchProgress.SetItem(
            parentPage,
            new BranchProgressEvidence(
                parentPage,
                inventory,
                ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal)));
    }
}
