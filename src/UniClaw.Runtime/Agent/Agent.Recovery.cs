using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;
using UniClaw.Runtime.World;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    /// <summary>
    /// 发射 Agent-scope drift Trap（B2 — HG-1 恰好 7 字段不变；I-13：只携带观测序号引用，不携带世界快照）
    /// 并记录独立 Trap 事件（与生命周期事件分离：无 RunState / Reason / RecoveryId）；
    /// 载荷保存至 _lastTrap（LastTrap 属性 — C4 观察面）。
    /// </summary>
    /// <param name="runId">Run 标识。</param>
    /// <param name="entry">drift 步骤的 journal 条目（StepId / LastAction 数据源）。</param>
    /// <param name="postObservation">drift post-action Observation（Observed 序号引用）。</param>
    /// <param name="container">drift 时的活动容器（Expected 序号引用与 ContainerId 数据源；调用侧保证非 null）。</param>
    private void EmitDriftTrap(string runId, TraversalJournalEntry entry, Observation postObservation, RuntimeContainer container)
    {
        _lastTrap = new Trap(
            TrapKind.UnexpectedPage,
            TrapScope.Agent,
            container.CurrentObservation?.SequenceNumber,
            postObservation.SequenceNumber,
            "Agent.DetectDrift",
            $"Agent-scope drift: foreground={postObservation.ForegroundApplication} != {_recoveryAnchor?.ApplicationIdentity}, page unresolvable, seq expected={container.CurrentObservation?.SequenceNumber} observed={postObservation.SequenceNumber}",
            entry.DispatchedAction);
        _trace.Add(new DecisionRecord(runId)
        {
            StepId = entry.StepId,
            ContainerId = container.SemanticPageName,
            TrapKind = _lastTrap.Kind,
            TrapScope = _lastTrap.Scope,
        });
        RuntimeObservability.AddEvent(System.Diagnostics.Activity.Current,
            "decision.trap",
            ("decision.reason", $"trap: {_lastTrap.Kind} ({_lastTrap.Scope})"));
    }

    /// <summary>
    /// Agent-scope drift 恢复流程（B3 — HG-4 Option B：机制在 Recovery 组件，决策在 Agent）：
    /// 1) Begin（消费 RecoveryAnchor.RestoreRecipe → 配方动作列表，解析在组件）→
    /// 2) 依次分发配方动作（经组件 → IEnvironment；dispatch 结果不构成恢复成功证据 — 裁决 10）→
    /// 3) 恢复后重新观测（§3）→ 4) 按 VerificationCriteria 验证（判据检查在组件；未通过 = 显式 Failed）→
    /// 5) 通过则 Reconcile + 重绑入口容器（ExpectedSemanticEntry — §20）→
    /// 6) 位置恢复：重放 Plan[0..suspendedIndex)（动作解析 + 分发经组件，不重走 Traversal 协议）；
    ///    语义页面回到挂起容器页面即重绑挂起容器并停止重放（挂起容器是 drift 时局部状态 owner — I-2）→
    /// 7) 续跑：从挂起索引重放剩余步骤（含挂起步骤自身——其动作虽已分发，恢复后必须重新执行；
    ///    与主循环相同步逻辑含证据评估；恢复后再次 drift → 发射 Trap + 显式失败，不递归恢复）。
    /// 单次恢复尝试（B3 边界）：无重试 / 无恢复策略（HG-2）。
    /// </summary>
    /// <param name="runId">Run 标识。</param>
    /// <param name="goal">Goal（续跑步骤的证据评估器数据源 — 裁决 3）。</param>
    /// <param name="plan">执行计划（挂起索引的剩余步骤数据源 — 裁决 11）。</param>
    /// <param name="suspendedIndex">挂起步骤索引（drift 步骤在 Plan 中的位置）。</param>
    /// <param name="suspendedContainer">drift 时的活动容器（挂起容器）。</param>
    /// <param name="driftObservation">drift 观测（载荷已固化于 Trap.Evidence — I-13 序号引用；本流程不再消费）。</param>
    /// <param name="suspendedStepId">挂起步骤的 StepId（验证失败显式原因关联）。</param>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>最终 RunState（Completed | Failed）。</returns>
    private async Task<RunState> RecoverFromDriftAsync(
        string runId,
        Goal goal,
        Plan plan,
        int suspendedIndex,
        RuntimeContainer suspendedContainer,
        Observation driftObservation,
        string? suspendedStepId,
        CancellationToken cancellationToken)
    {
        _suspendedStepIndex = suspendedIndex;
        _suspendedContainer = suspendedContainer;

        // 1. Begin recovery：消费 RecoveryAnchor.RestoreRecipe（配方解析在组件 — HG-4）
        _recovery.Begin(_recoveryAnchor!);

        // 2. Execute recipe actions（Relaunch 等；RestoreRecipe 由调用侧注入 — 裁决 8/11；
        //    B3 Startup 尚未填充配方 → 惰性执行）
        while (_recovery.HasRemainingActions)
        {
            var action = await _recovery.ExecuteNextAsync(cancellationToken);
            _trace.Add(new DecisionRecord(runId)
            {
                RecoveryId = $"Recovery-{++_recoveryCounter}",
                Action = action,
                ContainerId = ActiveExecutionContainerOrThrow?.SemanticPageName,
            });
        }

        // 3. Post-recovery observe（§3：动作后必须重新观察；恢复结果只能经观测确认）
        var recoveryObs = await _recovery.ObserveAsync(cancellationToken);
        _trace.Add(new DecisionRecord(runId)
        {
            RecoveryId = $"Recovery-{_recoveryCounter}",
            Reason = $"recovery observe (seq={recoveryObs.SequenceNumber})",
        });

        // 4. Verify（判据在 RecoveryAnchor.VerificationCriteria；检查机制在组件 — HG-4）
        var result = _recovery.Verify(recoveryObs, _recoveryAnchor!.VerificationCriteria);
        _trace.Add(new DecisionRecord(runId)
        {
            RecoveryId = $"Recovery-{_recoveryCounter}",
            Reason = $"recovery verify: {(result is RecoveryResult.Verified ? "VERIFIED" : ((RecoveryResult.Failed)result).Reason)}",
        });
        if (result is RecoveryResult.Failed failed)
        {
            // B5：原因由组件构建（SC-P2-003：RecoveryResult.Failed(Reason: "恢复验证失败：期望 X，实际 Y（seq=N）")）→ 原样转交
            return Fail(runId, failed.Reason, suspendedStepId);
        }

        // 5. VERIFIED：Reconcile。SC-P3-CAND-005 先由 Agent 使用 fresh recovered-world evidence
        //    解释 retained branch progress；RecoveryResult.Verified / parent identity 本身不证明 branch effect。
        var recoveredWorldBelief = Reconcile.FromObservation(recoveryObs, _resolveSemanticPage);
        var resumeFromRecoveredParent = TryRevalidateRecoveredBranchProgress(
            goal.DiscoveredBranchEffectCriterion,
            plan,
            suspendedContainer,
            recoveryObs,
            recoveredWorldBelief,
            out var progressValidityFailure);
        if (progressValidityFailure is not null)
        {
            return Fail(runId, progressValidityFailure, suspendedStepId);
        }

        // Recovery freshness/continuity is the existing owner of the failure
        // classification.  Only after it accepts the evidence may V2 replace
        // physical current; stale recovery therefore cannot steal the reason
        // or mutate any V2/local/progress state.
        if (!TryCommitFreshObservedLocation(runId, recoveryObs, recoveredWorldBelief, false, out var recoveredV2Failure))
            return Fail(runId, $"Recovered observation could not be committed to V2: {recoveredV2Failure}", suspendedStepId);

        if (resumeFromRecoveredParent)
        {
            // Verified Recovery 已直接回到挂起 parent，且 retained completion 已由 fresh criterion
            // revalidate；重绑同一 owner 后直接续跑，不 replay 已完成 A prefix。
            ReplaceActiveExecutionContainer(suspendedContainer);
            var recoveredParent = ActiveExecutionContainerOrThrow!;
            recoveredParent.Bind(recoveryObs);
            _trace.Add(new DecisionRecord(runId)
            {
                RecoveryId = $"Recovery-{_recoveryCounter}",
                ContainerId = recoveredParent.SemanticPageName,
                Reason = $"recovered parent branch progress revalidated (seq={recoveryObs.SequenceNumber})",
            });
        }
        else
        {
            // Existing SC-P2 path：没有 applicable retained branch progress 时保持原 position-restore 行为。
            ReplaceActiveExecutionContainer(CreateContainer(_recoveryAnchor!.ExpectedSemanticEntry));
            var recoveredEntry = ActiveExecutionContainerOrThrow!;
            recoveredEntry.Bind(recoveryObs);
            _trace.Add(new DecisionRecord(runId)
            {
                RecoveryId = $"Recovery-{_recoveryCounter}",
                ContainerId = recoveredEntry.SemanticPageName,
            });
        }

        // 6. Position-restore：重放 Plan[0..suspendedIndex)（动作解析 + 分发经组件，不重走 Traversal 协议）
        for (int j = 0; !resumeFromRecoveredParent && j < suspendedIndex; j++)
        {
            var step = plan.Steps[j];
            var action = _recovery.ResolveRecoveryAction(step, ActiveExecutionContainerOrThrow!.CurrentObservation!);
            if (action is null)
            {
                return Fail(runId, $"位置恢复: 无法解析 Step-{j + 1} 的动作", suspendedStepId);
            }
            await _recovery.ExecuteActionAsync(action, cancellationToken);
            _trace.Add(new DecisionRecord(runId) { RecoveryId = $"Recovery-{_recoveryCounter}", Action = action });
            var obs = await _recovery.ObserveAsync(cancellationToken);
            var replayBelief = Reconcile.FromObservation(obs, _resolveSemanticPage);
            if (!TryCommitFreshObservedLocation(runId, obs, replayBelief, false, out var replayV2Failure))
                return Fail(runId, $"Recovery replay observation could not be committed to V2: {replayV2Failure}", suspendedStepId);

            // 页面回到挂起容器页面 → 重绑挂起容器，停止重放
            var recoveredBelief = Belief;
            if (recoveredBelief?.SemanticPage == suspendedContainer.SemanticPageName)
            {
                ReplaceActiveExecutionContainer(suspendedContainer);
                ActiveExecutionContainerOrThrow!.Bind(obs);
                _trace.Add(new DecisionRecord(runId) { RecoveryId = $"Recovery-{_recoveryCounter}", ContainerId = ActiveExecutionContainerOrThrow!.SemanticPageName });
                break;
            }
            if (recoveredBelief?.SemanticPage is not null)
            {
                ReplaceActiveExecutionContainer(CreateContainer(recoveredBelief.SemanticPage));
                ActiveExecutionContainerOrThrow!.Bind(obs);
                _trace.Add(new DecisionRecord(runId) { RecoveryId = $"Recovery-{_recoveryCounter}", ContainerId = ActiveExecutionContainerOrThrow!.SemanticPageName });
            }
        }

        // 7. Resume：续跑剩余步骤（含挂起步骤自身——其动作虽已分发，恢复后必须重新执行）
        _trace.Add(new DecisionRecord(runId)
        {
            RecoveryId = $"Recovery-{_recoveryCounter}",
            Reason = $"recovery resume: plan index={suspendedIndex}",
        });
        for (int i = suspendedIndex; i < plan.Steps.Length; i++)
        {
            var step = plan.Steps[i];
            var wasLocallyCompleteBeforeStep = ActiveExecutionContainerOrThrow!.IsLocalComplete;
            var stepResult = ActiveExecutionContainerOrThrow!.ExecuteStep(step);
            var entry = LastJournalEntry();
            if (stepResult is TraversalStepResult.Failed stepFailed)
            {
                return Fail(runId, stepFailed.Reason, entry.StepId);
            }
            _trace.Add(new DecisionRecord(runId)
            {
                ContainerId = ActiveExecutionContainerOrThrow!.SemanticPageName,
                StepId = entry.StepId,
                ActionId = $"Action-{++_actionCounter}",
                Action = entry.DispatchedAction,
            });
            var postObservation = entry.PostActionObservation
                ?? throw new InvalidOperationException("step executor 返回 Succeeded 但未提供 post-action Observation（协议违约 — §3）。");
            var postRecoveryBelief = Reconcile.FromObservation(postObservation, _resolveSemanticPage);
            if (!TryCommitFreshObservedLocation(runId, postObservation, postRecoveryBelief, false, out var postRecoveryV2Failure))
                return Fail(runId, $"Recovery post-action observation could not be committed to V2: {postRecoveryV2Failure}", suspendedStepId);

            // PAGEANALYSIS INTEGRATION: 恢复后同样派生多源证据并更新 Container 局部信念。
            if (_pageAnalysisCriteria is not null)
            {
                var pageEvidence = PageAnalysis.Analyze(postObservation, _pageAnalysisCriteria);
                ActiveExecutionContainerOrThrow!.EvaluatePageBelief(postObservation, pageEvidence.ToArray());
            }

            // ELEMENTANALYSIS INTEGRATION: 恢复后同样刷新对象绑定。
            if (_elementBindingCriteria is not null)
            {
                var bindingEvidence = BindingAnalysis.Analyze(postObservation, _elementBindingCriteria);
                var bindings = BindingReconciler.Reconcile(
                    bindingEvidence, _elementBindingCriteria.KnownObjects);
                ActiveExecutionContainerOrThrow!.UpdateBindings(bindings);
            }

            // 恢复后再次 drift：单次恢复尝试（不递归）— 发射 Trap + 显式失败（挂起上下文写入原因 — HG-4 决策记录）
            if (Belief is { } currentBelief
                && IsAgentScopeDrift(postObservation, ActiveExecutionContainerOrThrow, currentBelief))
            {
                EmitDriftTrap(runId, entry, postObservation, ActiveExecutionContainerOrThrow);
                return Fail(runId, $"恢复后再次 Agent-scope drift（挂起于 plan index={_suspendedStepIndex}，容器={_suspendedContainer?.SemanticPageName}）", entry.StepId);
            }

            RecordBranchCompletionBeforeReturn(
                plan,
                i,
                ActiveExecutionContainerOrThrow,
                wasLocallyCompleteBeforeStep,
                postObservation,
                Belief?.SemanticPage);

            var evidence = goal.EvidenceEvaluator(postObservation);
            if (evidence.Satisfied)
            {
                _trace.Add(new DecisionRecord(runId) { RunState = RunState.Completed, Reason = evidence.Reason });
                _state = RunState.Completed;
                _reason = evidence.Reason;
                return RunState.Completed;
            }
            if (!ActiveExecutionContainerOrThrow!.IsStillMine(postObservation))
            {
                var newPage = Belief?.SemanticPage;
                if (newPage is null)
                {
                    return Fail(runId, $"Navigate 无法继续：观测（seq={postObservation.SequenceNumber}）无法解析新语义页面（Unknown — §10）。");
                }
                ReplaceActiveExecutionContainer(CreateContainer(newPage));
                ActiveExecutionContainerOrThrow!.Bind(postObservation);
                _trace.Add(new DecisionRecord(runId) { ContainerId = ActiveExecutionContainerOrThrow!.SemanticPageName });
            }
        }

        return Fail(runId, $"Plan 步数耗尽但 Goal 证据未满足：最后一次证据评估（seq={Belief?.SourceObservationSequence}）Satisfied=false。");
    }

    /// <summary>
    /// SC-P3-CAND-005 bounded recovered-parent protocol extended by SC-P3-CAND-009. Existing
    /// completion sequences at/before Trap.Observed remain historical until their matching branch
    /// effect criterion evaluates the strict-fresh post-verified-Recovery Observation. A PlanStep
    /// carrying a BranchEffectEvidenceEvaluator keeps frozen CAND-005 precedence; a discovered
    /// non-Plan branch may use the Goal-held singular carrier only when the carrier identity is
    /// exactly the completed branch identity present in the approved inventory under the same
    /// suspended parent. The three-way outcome is consumed by Agent control flow and is never
    /// stored as a validity state.
    /// </summary>
    /// <returns>true when retained branch progress made this bounded protocol applicable; false keeps the frozen SC-P2 path.</returns>
    private bool TryRevalidateRecoveredBranchProgress(
        BranchEffectCriterion? discoveredBranchEffectCriterion,
        Plan plan,
        RuntimeContainer suspendedContainer,
        Observation recoveryObservation,
        WorldBelief recoveredBelief,
        out string? failure)
    {
        failure = null;
        var driftBoundary = _lastTrap?.Observed;
        if (driftBoundary is null
            || !_branchProgress.TryGetValue(suspendedContainer.SemanticPageName, out var progress))
        {
            return false;
        }

        var retainedCompletions = progress.CompletedSiblingEvidence
            .Where(item => item.Value <= driftBoundary.Value)
            .ToArray();
        if (retainedCompletions.Length == 0)
            return false;

        if (recoveryObservation.SequenceNumber <= driftBoundary.Value
            || !string.Equals(
                recoveredBelief.SemanticPage,
                suspendedContainer.SemanticPageName,
                StringComparison.Ordinal)
            || !suspendedContainer.IsStillMine(recoveryObservation))
        {
            failure =
                $"Recovery 后 retained branch progress 无法验证：fresh recovered parent continuity 未获证明"
                + $"（boundary={driftBoundary.Value}, observed={recoveryObservation.SequenceNumber}, "
                + $"expectedParent={suspendedContainer.SemanticPageName}, actualParent={recoveredBelief.SemanticPage ?? "Unknown"}）。";
            return true;
        }

        var completed = progress.CompletedSiblingEvidence;
        foreach (var (branchIdentity, _) in retainedCompletions)
        {
            // SC-P3-CAND-005 precedence: a PlanStep carrying a durable effect criterion evaluates the
            // fresh Observation unchanged. Transient CAND-006 Tap steps carry no criterion and are
            // not criterion carriers. SC-P3-CAND-009 fallback: a discovered non-Plan branch uses the
            // Goal-held singular carrier only on exact identity match within the approved inventory
            // of this same suspended parent; missing/mismatched identity stays unresolved.
            bool? outcome = null;
            var branchStep = plan.Steps.FirstOrDefault(step =>
                string.Equals(step.TargetDescription, branchIdentity, StringComparison.Ordinal)
                && step.BranchEffectEvidenceEvaluator is not null);
            if (branchStep is not null)
            {
                outcome = branchStep.BranchEffectEvidenceEvaluator?.Invoke(recoveryObservation);
            }
            else if (discoveredBranchEffectCriterion is { } carrier
                && string.Equals(carrier.BranchIdentity, branchIdentity, StringComparison.Ordinal)
                && progress.ApprovedSiblingEvidence.ContainsKey(carrier.BranchIdentity))
            {
                outcome = carrier.Evaluator(recoveryObservation);
            }

            if (outcome is true)
            {
                completed = completed.SetItem(branchIdentity, recoveryObservation.SequenceNumber);
                continue;
            }

            if (outcome is false)
            {
                completed = completed.Remove(branchIdentity);
                failure ??=
                    $"Recovery 后 branch effect contradicted：{branchIdentity} 的 fresh evidence 明确不满足"
                    + $"（seq={recoveryObservation.SequenceNumber}）；不贡献 completion，且不 blind redispatch。";
                continue;
            }

            failure ??=
                $"Recovery 后 branch effect unresolved：{branchIdentity} 缺少可判定的 fresh evidence"
                + $"（seq={recoveryObservation.SequenceNumber}）；retained history 不贡献 completion，且不 blind redispatch。";
        }

        _branchProgress = _branchProgress.SetItem(
            progress.ParentSemanticPage,
            new BranchProgressEvidence(
                progress.ParentSemanticPage,
                progress.ApprovedSiblingEvidence,
                completed));
        return true;
    }
    /// <summary>
    /// Agent-scope drift 判定（B1 — HG-3：不新增 DriftStatus 字段；纯函数，不新增状态）。
    /// 三个条件同时成立才判定：
    /// (1) 前台应用 ≠ RecoveryAnchor.ApplicationIdentity（恢复入口基线），
    /// (2) 容器判定当前观测不再属于本语义页面（!IsStillMine — I-3），
    /// (3) 语义页面 Unknown（SemanticPage == null — §10）。
    /// 不误伤正常导航：导航前提是语义页面可解析（条件 3 不成立）；
    /// 仅前台切换而页面可解析、或仍属本页，均不算 drift。
    /// </summary>
    /// <param name="observation">post-action Observation（§3）。</param>
    /// <param name="container">当前活动容器。</param>
    /// <param name="belief">本次 Reconcile 后的世界信念。</param>
    /// <returns>true = Agent-scope 世界信念丢失（由 Agent 判定显式失败）。</returns>
    private bool IsAgentScopeDrift(Observation observation, RuntimeContainer container, WorldBelief belief)
    {
        var baseline = _recoveryAnchor?.ApplicationIdentity;
        return !string.Equals(observation.ForegroundApplication, baseline, StringComparison.Ordinal)
            && !container.IsStillMine(observation)
            && belief.SemanticPage is null;
    }
}
