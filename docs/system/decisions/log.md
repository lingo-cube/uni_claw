# Decisions — AI-Coding 索引

> **Tier 4**: append-only 决策记录。每条是对后续编码有约束力的决策摘要。
> 详细规格: `docs/system/charter-specification.md` §5
> 更新触发: OpenSpec archive / 审计报告完成 / 直接 commit 小 fix
> 关键属性: 只追加不改旧 (唯一例外: Guard 字段升级)

---

## 条目格式

```
### D-{id} | {date} | {title}

Decision: {一句话结论 — AI 需遵守什么}
Rationale: {为什么 — 1-2 句}
Source: openspec:{change-id} | finding:{H/M/D-id} | direct-commit
Ref: {指向原文的路径}
Guard: {ConstitutionGuardTests test 名} | 无 (convention-level)
Commit: {hash} | pending
Status: Fixed | Locked | Deferred · Target: Phase {n}
```

---

### D-1 | 2026-07-02 | TypeHint 8 值锁定

Decision: TypeHint enum 8 值封顶, 不新增。如需行为分类用中间字符串/MenuItemType。
Rationale: 视觉外观层火山级, 任何新值 cascade 4 域 (AliasMap, IsInteractive, IsVisualOnly, TypeStringToTypeHintMap)。
Source: finding:design-decision (Phase 1 PRD)
Ref: docs/refactor/03-phase1-prd.md
Guard: EnumValueGuardTests.TypeHint_Has8Values
Commit: ce124b5
Status: Locked

---

### D-2 | 2026-07-02 | Domain 三岛零互 import

Decision: Domain.Vision ↔ Domain.Content ↔ Domain.Common 零直接 import, 唯一桥 Mappings。
Rationale: 防止跨域语义泄漏, 保持两级映射分离 (P0 fix)。
Source: finding:design-decision (Phase 1 PRD)
Ref: docs/refactor/03-phase1-prd.md
Guard: 待新增 NamespaceIsolationGuardTests
Commit: ce124b5
Status: Locked

---

### D-3 | 2026-07-02 | GlobalState 8 值锁定

Decision: GlobalState enum 8 值封顶 (Idle/Initializing/Traversing/Paused/Error/Recovering/Completed/Terminated), Completed 和 Terminated 为 terminal 状态。
Rationale: 对齐 Python GlobalFSM macro 状态, terminal 状态不可迁出保证遍历终态确定性。
Source: openspec:traversal-fsm
Ref: openspec/specs/traversal-fsm/spec.md
Guard: EnumValueGuardTests.GlobalState_Has8Values
Commit: ce124b5
Status: Locked

---

### D-4 | 2026-07-02 | SelectionState 3 值锁定

Decision: SelectionState enum 3 值封顶 (Normal/Selected/Disabled)。
Rationale: 视觉外观层火山级, cascade SelectedAliases + DisabledAliases + FlattenedElement.IsInteractive。
Source: finding:design-decision (Phase 1 PRD)
Ref: docs/refactor/03-phase1-prd.md
Guard: EnumValueGuardTests.SelectionState_Has3Values
Commit: ce124b5
Status: Locked

---

### D-5 | 2026-07-04 | DynamicMatch 从 TraversalState 移除

Decision: DynamicMatch 是 ChildrenStrategyType 值, 不是 FSM state。
Rationale: H-1 违规 — 在 FSM 迁移矩阵中不可达, 与 ChildrenStrategyType 值域重叠, 造成 9 值而非规格要求的 8 值。
Source: finding:H-1 (docs/refactor/09-phase2-review-report.md §Hard Constraints)
Ref: docs/refactor/10-phase2.1-fix-design.md §Phase2.1a
Guard: EnumValueGuardTests.TraversalState_Has8Values
Commit: pending (Phase 2.1a 实施中)
Status: Fixed

---

### D-6 | 2026-07-04 | ITraversalNode 移到 Graph 层

Decision: ITraversalNode 和 IStackFrame 定义在 Graph.Models namespace, 不在 StateMachine。
Rationale: H-5 — 消除 Graph↔StateMachine 双向依赖。StateMachine 不应定义 Graph 层接口。
Source: finding:H-5 (docs/refactor/09-phase2-review-report.md §Hard Constraints)
Ref: docs/refactor/10-phase2.1-fix-design.md §Phase2.1b
Guard: DependencyDirectionGuardTests.ITraversalNode_ResidesInGraphModelsNamespace
       DependencyDirectionGuardTests.TraversalState_DoesNotContainITraversalNodeOrIStackFrame
Commit: pending
Status: Fixed

---

### D-7 | 2026-07-04 | GlobalState 暂留 ITraversalContext

Decision: Phase 3 再修, 当前不 breaking change。
Rationale: 影响 6 consumer (TraversalFSM, StepOrchestrator, ContainerHandler, ErrorHandler, PopupHandler, TraversalRuntimeContext), 无 runtime defect, 优先级低于 H-1/H-2/H-5 hard constraint。
Source: finding:M-14 (docs/refactor/09-phase2-review-report.md §Medium)
Ref: docs/refactor/11-m14-globalstate-evaluation.md
Guard: ConstitutionGuardTests.C4 (waived — 当前不验证)
Commit: pending
Status: Deferred · Target: Phase 3

---

### D-8 | 2026-07-04 | TextMatchMode 默认 Contains

Decision: DynamicMatcher text_pattern 匹配默认 Contains, Exact 为显式选项。
Rationale: 向后兼容已有 DynamicMatcher 逻辑, M-9 (text_pattern 缺 Exact mode)。
Source: finding:M-9 (docs/refactor/09-phase2-review-report.md §Medium)
Ref: openspec/specs/text-match-mode/spec.md
Guard: 无 (convention-level, 不需机器验证)
Commit: pending
Status: Locked

---

### D-9 | 2026-07-04 | ReadOnlySetWrapper cast-back 阻断

Decision: VisitedChildren 用 ReadOnlySetWrapper private sealed class, cast-back → InvalidCastException。
Rationale: H-2 — VisitedChildren 泄漏 HashSet 引用, 可 cast-back 修改引擎内部, 设计要求 cast-back-level 安全但代码只达 interface-level。
Source: finding:H-2 (docs/refactor/09-phase2-review-report.md §Hard Constraints)
Ref: openspec/specs/readonly-set-wrapper/spec.md
Guard: VisitedChildrenIsolationTests.VisitedChildren_CastBackToHashSet_ThrowsInvalidCastException
Commit: pending
Status: Fixed

---

### D-10 | 2026-07-05 | DismissStrategy 改为条件逻辑对齐 Python

Decision: PopupClassifier.DismissStrategyMap 删除，改为 `DetermineDismissStrategy(PopupType, string? dismissTarget)` 条件逻辑。有 dismiss target → 统一 AutoClose；无 target → 按 PopupType fallback (Permission→WaitTimeout, Error→AutoCloseOrBack, Ad→Back, Dialog/Unknown→Back)。PopupActionExecutor Default 方法同步改为相同条件逻辑。
Rationale: 5/5 静态映射值不对应 Python。Python 先检查 dismiss target 再按 type fallback，C# 不区分有无 target，导致 Permission 无 target 时 AutoClose（应为 WaitTimeout）操作失败，Ad 有 target 时 WaitTimeout（应为 AutoClose）等待太久。
Source: finding:python-alignment (docs/refactor/12-python-csharp-design-gaps.md §正确性问题1)
Ref: src/UniClaw.Core/StateMachine/PopupHandler.cs PopupClassifier.DetermineDismissStrategy vs Python src/state_machine/popup_handler.py `_determine_dismiss_strategy`
Guard: 无 (逻辑行为验证, 非值数锁定)
Commit: pending
Status: Fixed

**修复前偏差对比**:

| PopupType | Python 有target | Python 无target | C# DismissStrategyMap (旧) |
|-----------|----------------|----------------|----------------------|
| Permission | auto_close | wait_timeout | AutoClose (永远) |
| Error | auto_close | auto_close_or_back | Back (永远) |
| Ad | auto_close | back | WaitTimeout (永远) |
| Dialog | auto_close | back | AutoCloseOrBack (永远) |
| Unknown | auto_close | back | Back (永远) |

**修复后**: 有 target → AutoClose (所有 type); 无 target → Permission=WaitTimeout, Error=AutoCloseOrBack, Ad=Back, Dialog/Unknown=Back。5/5 对齐 Python。

---

### D-11 | 2026-07-05 | UrgencyLevel 移除 Critical，3 值对齐 Python

Decision: 移除 UrgencyLevel.Critical enum 值，值数从 4→3 对齐 Python (LOW/MEDIUM/HIGH)。Guard test 更新为 Has3Values。
Rationale: Critical 是死值 — DetermineUrgency 从不赋值，全代码库零引用。Python UrgencyLevel 只有 3 值无 Critical。移除死值与 fail-fast 原则一致，消除"声明但不可达"的设计噪音。
Source: finding:python-alignment (docs/refactor/12-python-csharp-design-gaps.md §正确性问题2)
Ref: src/UniClaw.Core/StateMachine/PopupHandler.cs `enum UrgencyLevel` + `DetermineUrgency()` vs Python src/state_machine/popup_handler.py `enum UrgencyLevel`
Guard: EnumValueGuardTests.UrgencyLevel_Has3Values (从 Has4Values 更新)
Commit: pending
Status: Fixed

---

### D-12 | 2026-07-05 | CompletionReason 保持 4 值 — 不加死值 Error

Decision: CompletionReason 保持 4 值 (Timeout/MaxDepth/AllVisited/Incomplete)，不加 Error。当 ErrorHandler 实现直接 completion 路径 (bypass CompletionDetector) 时再加 Error + 赋值路径，确保新值永远可达。
Rationale: Python CompletionStatus.ERROR 也是死值 — CompletionDetector.detect_completion() 从不赋 ERROR，只返回 TIMEOUT/MAX_DEPTH/ALL_VISITED/INCOMPLETE。加死值 Error 与 D-11 移除死值 Critical 自相矛盾。原则: 移除死值 (D-11) 和不加新死值 (D-12) 是同一原则的两个实例。
Source: finding:python-alignment (docs/refactor/12-python-csharp-design-gaps.md §正确性问题3) + 验证 Python src/state_machine/container_handler.py CompletionDetector
Ref: src/UniClaw.Core/StateMachine/ContainerHandler.cs `enum CompletionReason` vs Python src/state_machine/container_handler.py `enum CompletionStatus`
Guard: 无 (CompletionReason 不在 Guard test 中; 4 值对齐 Python 实际使用)
Commit: pending
Status: Fixed · Deferred Error value until ErrorHandler has direct completion path

---

### D-13 | 2026-07-05 | PreconditionCheck→Branch 保持移除 — Python handler 不使用此路径

Decision: 保持 D-1 修正移除 PreconditionCheck→Branch。Python `_handle_precondition_check()` 从不返回 BRANCH (只返回 EXECUTE 或 ERROR_HANDLING)，此路径在 Python VALID_TRANSITIONS 中声明但 handler 不使用，等同于死路径。C# 移除是正确收紧。
Rationale: 验证结论: Python handler `_handle_precondition_check` 只走 EXECUTE 或 ERROR_HANDLING，有便利方法 `precondition_failed()` 可 transition_to(BRANCH) 但 handler 不调用。矩阵声明了不使用的路径，C# 移除是收紧而非行为 bug。
Source: finding:python-alignment (docs/refactor/12-python-csharp-design-gaps.md §正确性问题4) + 验证 Python src/state_machine/traversal_fsm.py `_handle_precondition_check`
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs TransitionMatrix vs Python src/state_machine/traversal_fsm.py VALID_TRANSITIONS + `_handle_precondition_check`
Guard: StateMachineTests.TransitionMatrix_PreconditionCheckToBranch_Rejected
Commit: pending
Status: Fixed

---

### D-14 | 2026-07-05 | IGraphTraversalEngine 双定义 stub 清理

Decision: **Resolved** — 空 stub 已删除 (TraversalState.cs:152-155)。StateMachine→Traversal 向上引用显式承认 (与 D-17 Observability 向上引用一致)。HasUnvisitedChildren 参数类型改为 UniClaw.Core.Traversal.IGraphTraversalEngine。ArchitectureGuardTests 新增 2 个 acknowledged upward reference tests。
Rationale: 空 stub 是死代码 — HasUnvisitedChildren 永远传 null。隐藏依赖不如显式承认。Traversal 是被 StateMachine 消费的层 (FSM 需要 visited-children 判断)，这与 Observability 被两层消费 (D-17) 是同类依赖。
Source: finding:docs-vs-code → Phase 2.3 TraversalEngine unified entry point design
Ref: src/UniClaw.Core/StateMachine/TraversalState.cs (stub deleted), src/UniClaw.Core/StateMachine/TraversalFSM.cs (using UniClaw.Core.Traversal added), tests/ArchitectureGuardTests.cs (2 new acknowledged-ref tests)
Guard: DependencyDirectionGuardTests (StateMachine_ReferencesTraversalForIGraphTraversalEngine + StateMachine_ReferencesObservabilityForCrossCuttingUtility)
Commit: feature/refactor
Status: Resolved · Date: 2026-07-06

---

### D-15 | 2026-07-05 | 5 subsystem 名称 canonical 定义

Decision: **待定** — TraversalRuntimeContext 5 subsystem 分类名称 (DFS 遍历/进度控制/错误追踪/宏观状态/缓存与配置) 目前是推导性分类, 未在任何设计文档正式定义。部分字段归属有歧义 (_visitedLevel1Menus 属 DFS 还是 Cache? _completionPolicy 属 Macro 还是 Progress?)。
Rationale: Phase 3 拆分 Context 时需要明确的子系统边界和 canonical 字段归属。
Source: finding:docs-vs-code (docs/system/patterns/system-orchestration.md §3.4 correctness audit)
Ref: docs/system/layers/state-machine.md (says "5 subsystems" but never enumerates)
Guard: 无 (convention-level)
Commit: pending
Status: Deferred · Target: Phase 3 (Context decomposition prerequisite)

---

### D-16 | 2026-07-05 | Container/ErrorHandler 统一编排 wrapper

Decision: **待定** — Container (CompletionDetector/FallbackDecider/ContainerActionExecutor) 和 Error (ErrorClassifier/ErrorStrategySelector/RecoveryExecutor) 均为 3 独立子组件, 无统一 Handler wrapper 类。当前靠 TraversalEngine 手动按序调用。
Rationale: handler-pipeline.md 定义了统一 pipeline 模式, 但 Container 和 Error 的实现不遵循此模式。加 wrapper 类可使 pipeline 模式一致, 也简化 TraversalEngine 调用。
Source: finding:docs-vs-code (docs/system/patterns/system-orchestration.md §4 correctness audit)
Ref: src/UniClaw.Core/StateMachine/ContainerHandler.cs, src/UniClaw.Core/StateMachine/ErrorHandler.cs
Guard: 无 (convention-level)
Commit: pending
Status: Deferred · Target: Phase 2.3

---

### D-17 | 2026-07-05 | Observability 层定位 — cross-cutting utility vs 传统顶层

Decision: Observability 是 cross-cutting utility, 被 StateMachine + Traversal 共同消费, 不是文档原先声称的严格顶层。依赖方向图已更新为实际状态。
Rationale: ITraceRecorder 定义完全自包含 (零外部引用), 天然横切。StateMachine 和 Traversal 均向上引用 Observability — 这是架构现实, 不是设计缺陷。严格分层会迫使类型不合理下移。
Source: finding:docs-vs-code (docs/system/patterns/system-orchestration.md §1 dependency direction audit)
Ref: src/UniClaw.Core/Observability/ITraceRecorder.cs, src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs, src/UniClaw.Core/Traversal/TraversalEngine.cs
Guard: DependencyDirectionGuardTests.C5_GraphDoesNotReferenceStateMachine (only checks one layer boundary, not full graph)
Commit: pending
Status: Fixed (文档已修正) · 是否需要 Guard 扩展: 待定
