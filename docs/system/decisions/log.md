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

Decision: **Fixed** — ITraversalContext 改为纯只读接口（移除 CurrentFrame/GlobalState/LastError setters），mutation 通过 TraversalRuntimeContext.SetXxx() 方法。TraversalFSM 添加 RuntimeContext 属性（concrete 类型）用于内部 mutation。
Rationale: 影响 6 consumer (TraversalFSM, StepOrchestrator, ContainerHandler, ErrorHandler, PopupHandler, TraversalRuntimeContext), 无 runtime defect, 优先级低于 H-1/H-2/H-5 hard constraint。Phase 2.3 通过 itraversalcontext-reform 修复。
Source: finding:M-14 (docs/refactor/09-phase2-review-report.md §Medium) → openspec:itraversalcontext-reform
Ref: docs/refactor/11-m14-globalstate-evaluation.md, openspec/changes/itraversalcontext-reform/design.md
Guard: ConstitutionGuardTests.C4 (waived — 当前不验证)
Commit: 9762b08
Status: Fixed · Note: ITraversalContext 现在是纯只读接口，符合 D-I/D-V 模式

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

### D-15 | 2026-07-11 | TraversalRuntimeContext 5 subsystem canonical 定义 + 10 ambiguity resolutions

Decision: TraversalRuntimeContext 30 mutable states 正式分类为 5 canonical subsystems: NavigationContext (DFS traversal, 12 fields), ErrorContext (error tracking, 5 fields), SessionContext (macro state, 4 fields), ProgressContext (progress control, 5 fields), CacheContext (cache & config, 2 core + 2 Phase 3 reserved fields). 所有 10 原有歧义字段归属已判定并附 rationale。
Rationale: Phase 3 拆分 Context (D-I) 需明确子系统边界和 canonical 字段归属。无此定义, 拆分方向无法确定。
Source: openspec:subsystem-canonical-naming
Ref: docs/system/layers/state-machine.md §5 (canonical field ownership table), src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs (D-15 annotation comments)
Guard: SubsystemBoundaryGuardTests.TraversalRuntimeContext_FieldCountsPerSubsystem (CI-blocking)
Commit: pending
Status: Locked

**5 subsystem canonical names (D-15-1)**:
| # | Name | Responsibility |
|---|------|----------------|
| 1 | NavigationContext | DFS traversal — node selection, visited tracking, page identity, stack management |
| 2 | ErrorContext | Error tracking — error recording, retry counting, failure tracking, recovery state |
| 3 | SessionContext | Macro state — global FSM state, trace identity, device/AI configuration |
| 4 | ProgressContext | Progress control — step counting, completion policy, action audit, timing config |
| 5 | CacheContext | Cache & config — page cache, cache validity, screen snapshots (Phase 3 reserved) |

**Field counts per subsystem (D-15-2)**:
NavigationContext=12, ErrorContext=5, SessionContext=4, ProgressContext=5, CacheContext=2 (core, +2 Phase 3 reserved)

**10 ambiguity resolutions (D-15-3)**:
| Field | Decision | Rationale |
|-------|----------|-----------|
| _visitedLevel1Menus | NavigationContext | Primary consumer: DynamicChildManager for DFS traversal decisions. Dedup is side-effect |
| _visitedLevel2Menus | NavigationContext | Same pattern as L1 — DFS traversal decision, not cache dedup |
| _completionPolicy | ProgressContext | Answers "when should traversal end?" — progress/termination question |
| _currentFingerprint | NavigationContext | VisitFingerprint is page identity marker for DFS revisit detection. Cache invalidation is downstream side-effect |
| _globalState | SessionContext | Macro session lifecycle managed by GlobalFSM. D-7 addresses ITraversalContext exposure, not internal attribution |
| _deviceExperience | SessionContext | Set once per session, never changes — session-level metadata |
| _aiProvider | SessionContext | Set once, session-level configuration — same reasoning as deviceExperience |
| _pageTree | NavigationContext | DynamicChildManager uses for child enumeration — DFS navigation data structure |
| _actionHistory | ProgressContext | Audit trail of recent actions. Navigation decisions don't query it |
| _cacheValid | CacheContext | Cache validity flag controlling _pageCache reuse lifecycle — cache semantics, not progress semantics |

---

### D-16 | 2026-07-05 | Container/ErrorHandler 统一编排 wrapper

Decision: **Fixed** — ContainerHandler.HandleContainer() 和 ErrorHandler.HandleError() 作为统一 3-step pipeline entry points，pipeline-level try/catch fallback。Sub-component methods stored as Func delegates for testability (sealed classes can't be subclassed)。ErrorRecoveryResult extended with `string? Description = null` for pipeline fallback diagnostic info。
Rationale: handler-pipeline.md 定义了统一 pipeline 模式, 但 Container 和 Error 的实现不遵循此模式。加 wrapper 类可使 pipeline 模式一致, 也简化 TraversalEngine 调用。
Source: openspec:phase22-refactoring
Ref: src/UniClaw.Core/StateMachine/ContainerHandler.cs, src/UniClaw.Core/StateMachine/ErrorHandler.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-17 | 2026-07-05 | Observability 层定位 — cross-cutting utility vs 传统顶层

Decision: Observability 是 cross-cutting utility, 被 StateMachine + Traversal 共同消费, 不是文档原先声称的严格顶层。依赖方向图已更新为实际状态。
Rationale: ITraceRecorder 定义完全自包含 (零外部引用), 天然横切。StateMachine 和 Traversal 均向上引用 Observability — 这是架构现实, 不是设计缺陷。严格分层会迫使类型不合理下移。
Source: finding:docs-vs-code (docs/system/patterns/system-orchestration.md §1 dependency direction audit)
Ref: src/UniClaw.Core/Observability/ITraceRecorder.cs, src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs, src/UniClaw.Core/Traversal/TraversalEngine.cs
Guard: DependencyDirectionGuardTests.C5_GraphDoesNotReferenceStateMachine (only checks one layer boundary, not full graph)
Commit: pending
Status: Fixed (文档已修正) · 是否需要 Guard 扩展: 待定

---

### D-E1 | 2026-07-10 | ExpectedBehavior 载体形态 = sealed record class + JSON 文件

Decision: C# sealed record class 定义 schema + JSON 文件存放具体场景数值实例。ExpectedBehavior record 结构变更走 C-11 constitution change flow (同 enum 值锁定级)。
Rationale: record 编译期保障字段名/类型/必填 (和 StateFixture / TraversalResult 设计模式一致)。JSON 提供可读性和可修改性 (改数值不改代码、不改测试)。两者结合: record 是 schema 契约, JSON 是数据实例。
Source: openspec:traversal-expected-behavior
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/ (9 record classes), tests/.../Baseline/Fixtures/expected/ (2 JSON files)
Guard: 无 (convention-level, C-11 constitution entry 补充)
Commit: pending
Status: Locked

### D-E2 | 2026-07-10 | ExpectedBehavior.Verify 返回 VerificationReport

Decision: `ExpectedBehavior.Verify(TraversalResult)` 返回 `VerificationReport`, 测试代码 `Assert.True(report.AllPassed, report.Summary)`。
Rationale: 失败时能看到具体哪条规则 fail + 实际值 (不是只看到异常消息或 bool)。验证逻辑和测试框架解耦 (report 是纯数据 record, 不依赖 Xunit)。
Source: openspec:traversal-expected-behavior
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-E3 | 2026-07-10 | 预期值来源 = 结构性推导 + 数值锚定

Decision: 结构性预期从 fixture 推导 (`auto_derive` sentinel), 数值性预期由运行时锚定 (JSON 手写)。
Rationale: fixture 变了 (加页面/加元素), 结构性预期自动跟着变, 不用手动同步 JSON。步数/节点数无法从 fixture 推导 (依赖引擎行为), 必须运行一次后锚定。
Source: openspec:traversal-expected-behavior
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.cs (WithFixtureDerivation method)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-E4 | 2026-07-10 | 规则映射 = 5 类可验证先行 + 2 类 TODO

Decision: 当前只定义 5 类可验证维度 (completion, page_coverage, element_coverage, collision_proof, dfs_properties) + numeric_anchor informational。2 类 (operation_rules, trace_integrity) 标记 TODO, 待 Trace 补齐后扩展。
Rationale: 当前 Trace 不包含 SpanType/PageTransition 专用字段, operation_rules 的 restore_ops/skip_dangerous 无法验证。先行定义 5 类可覆盖 Python 7 类中的核心验证维度。
Source: openspec:traversal-expected-behavior
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/ (5 record types + NumericAnchor)
Guard: 无 (convention-level)
Commit: pending
Status: Locked · Note: operation_rules 和 trace_integrity 字段 (SpanType, PageTransition) 已在 Phase 2.2 添加，验证逻辑为未来变更

### D-E5 | 2026-07-10 | 标识体系 = 语义标识, 不用 NodeId

Decision: 预期定义用 fixture 页面名/元素名 (如 "Wi-Fi", "bluetooth_switch"), Verify 内部做语义→NodeId 映射 (Contains semantics)。
Rationale: NodeId 是实现细节 (`dyn_menu_container_Wi-Fi_root`), 预期定义应面向人能读懂的语义。改 NodeId 公式不影响预期文件。
Source: openspec:traversal-expected-behavior
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs (Contains semantics matching)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-E6 | 2026-07-10 | JSON 预期定义存放位置 = tests/Baseline/Fixtures/expected/

Decision: JSON 预期定义文件放在 `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/`。
Rationale: 和 fixture 输入数据 (StateFixtureBuilder 内联构建) 分开, 预期输出有独立目录。baseline 测试入口 SimulationBaselineTests.cs 直接消费这些 JSON 文件。
Source: openspec:traversal-expected-behavior
Ref: tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-E7 | 2026-07-10 | ExpectedBehavior 代码层 = Simulation 命名空间, 独立文件

Decision: ExpectedBehavior.cs + 子 records + VerificationReport.cs + ExpectedBehavior.Verify.cs 放在 `src/UniClaw.Core/Simulation/ExpectedBehavior/`, 不放 Traversal 命名空间。
Rationale: ExpectedBehavior 是测试基础设施 (验证预期 vs 实际), 不是引擎核心逻辑。Simulation 命名空间已有 StateFixture/StatefulMockVisionService 等测试构建基础设施, ExpectedBehavior 是同一性质的扩展。Traversal 命名空间保持纯引擎逻辑。
Source: openspec:traversal-expected-behavior
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-E8 | 2026-07-10 | SpanType 11 值锁定

Decision: SpanType enum 11 值封顶 (DfsForward, DfsBacktrack, RestoreOp, SkipDangerous, PopupHandling, ContainerHandling, ErrorHandling, PageAnalysis, CacheOp, AICall, StateDecision)。如需新增值走 constitution change flow (C-11 style)。
Rationale: SpanType 覆盖 operation_rules 和 trace_integrity 验证维度。每个值映射到遍历生命周期中的可追踪语义事件类型。值数锁定防止验证框架因随意加值而失效。
Source: openspec:phase22-refactoring
Ref: src/UniClaw.Core/Observability/ITraceRecorder.cs (SpanType enum)
Guard: EnumValueGuardTests.SpanType_Has11Values
Commit: pending
Status: Locked

---

### D-18 | 2026-07-11 | TraceContext 4-field boundary — 关联信封封装

Decision: TraceContext sealed record class 封装 5 种 ITraceRecorder record 类型共享的 4 个关联字段 (NodeId, StepSpanId, StepNumber, TraceId)。TraceContext **ONLY** 含 ALL 5 类型共享字段; 类型专属字段 (FsmType, SpanId, ChildNodeId, ParentNodeId, PageId, TargetType/TargetValue, Depth, DurationMs, Tokens) 留在各 record type 上。Guard test `TraceContext_Has4Fields` 阻止意外添加类型专属字段。Phase 3 扩展 VisitSpanId+ParentSpanId 加入 TraceContext, 无需改任何 record type。
Rationale: TraceContext 回答"when/where/how was this event recorded" — 关联信封, 不是核心域。4x5=20 参数精简为 1x5=5, TraceCoordinator.BuildCorrelation() 一次性填充。拒绝: (A) 每类型显式 4 参数 (混合 domain+trace), (B) Metadata 字典 (丢类型安全), (C) record 继承 (C# sealed record 不可继承)。
Source: openspec:trace-pipeline-three-layer
Ref: src/UniClaw.Core/Observability/TraceContext.cs, src/UniClaw.Core/Observability/ITraceRecorder.cs (5 record types with Context? field)
Guard: TraceContext_Has4Fields
Commit: pending
Status: Locked

---

### D-19 | 2026-07-11 | 三层 CQRS + ISP 非对称注入

Decision: Observability 层 CQRS at interface level — ITraceRecorder (7 async, pure write) + ITraceStorage (14 sync, shared backend) + ITraceService (13, pure read+query)。非对称依赖注入: InMemoryTraceRecorder 注入 ITraceStorage (接口), InMemoryTraceService 注入 InMemoryTraceStorage (具体类, 需要 index 方法)。ITraceRecorder SHALL NOT 含查询方法、CurrentSession getter、或 ExportTraceAsync。Guard test `ITraceRecorder_Has7Methods` 阻止方法数回归 (13→7 精简已完成)。Index 方法 (GetByNodeId, GetBySpanType) 是 InMemoryTraceStorage 专属, 不在 ITraceStorage 接口 (ISP: 不所有实现都有内存 indexes)。
Rationale: TraceCoordinator 只写 (injects ITraceRecorder), 分析只读 (injects ITraceService), 共享 ITraceStorage 后端解耦两者 — 替换 storage 不影响任一消费端。拒绝: (A) 单 13 方法单体 (混合写+读, 无 CQRS), (B) ITraceRecorderWriter+Reader (Recorder 实现 still stores+reads, 不干净分离)。ISP: DB storage 用 SQL 查询, file storage 用 scan, 不都需内存 indexes。
Source: openspec:trace-pipeline-three-layer
Ref: src/UniClaw.Core/Observability/ITraceRecorder.cs, ITraceStorage.cs, ITraceService.cs, InMemoryTraceRecorder.cs, InMemoryTraceStorage.cs, InMemoryTraceService.cs
Guard: ITraceRecorder_Has7Methods
Commit: pending
Status: Locked

---

### D-20 | 2026-07-11 | StepSpanId per-step 语义 + StepSpanId=StepStart 的 SpanId

Decision: StepSpanId 语义 = per-engine-step grouping key, 不是 VisitSpanId (per-node-visit)。StepSpanId 在 RecordStepStart 时赋值 (= 该 StepStart 的 SpanId), RecordStepEnd 时释放。命名匹配实现 (TraceCoordinator 实际实现 StepStart→StepEnd 生命周期)。Phase 3 将添加 VisitSpanId 作为 TraceContext 独立字段, StepSpanId 作为独立概念保留。
Rationale: StepSpanId=StepStart's SpanId 使得 SpanId==StepSpanId 直接查找 StepStart record, 避免两个独立计数器表达同一概念事件 (step start)。拒绝: VisitSpanId per-node-visit (TraceCoordinator 需检测 NodeId 跨步变化, 复杂节点生命周期追踪)。
Source: openspec:trace-pipeline-three-layer
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (TraceCoordinator: _currentStepSpanId, NextSpanId, BuildCorrelation)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-21 | 2026-07-11 | TargetType+TargetValue 类型安全替换 object? Target

Decision: ExecutionRecord 用 `TargetType?` (Domain.Common enum: Text/Coordinate/UiIndex) + `string? TargetValue` 替换 `object? Target`。TargetType/TargetValue 是 ExecutionRecord 类型专属字段 (NOT in TraceContext)。Back/NoAction 有 TargetType=null, TargetValue=null。Observability→Domain.Common 引用允许 per D-17 (cross-cutting utility)。
Rationale: TargetType enum 编译期类型安全, TargetValue string 可查询/可缓存/可序列化。拒绝: (A) object? Target (无类型, 不可查询/过滤/缓存), (C) Domain.Common.Target record (Target.Value 是 object?, 同样问题)。SerializeTarget: Coordinate→"{X},{Y}", string→string, int→ToString()。
Source: openspec:trace-pipeline-three-layer
Ref: src/UniClaw.Core/Observability/ITraceRecorder.cs (ExecutionRecord.TargetType, TargetValue), src/UniClaw.Core/Domain/Models/Common/Target.cs (TargetType enum)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-22 | 2026-07-11 | ITraceStorage sync-first + ErrorRecord.ParentNodeId 移除

Decision: ITraceStorage 写方法是同步 (void return)。async 层在 ITraceRecorder (消费侧契约)。ErrorRecord.ParentNodeId 移除 — Context.NodeId 提供 "error occurred at this node" 语义。ExecutionRecord.ParentNodeId 保留 — 它的语义是 "DFS tree parent for tree reconstruction", 不同于 Context.NodeId "event-at-node"。
Rationale: 内存操作总是同步, 不需要 async wrapper 在 storage 层。ErrorRecord.ParentNodeId 原语义是 "error at this node", 与 Context.NodeId 精确重叠。ExecutionRecord.ParentNodeId 语义是 DFS 父 (tree reconstruction), 与 Context.NodeId 不同概念。
Source: openspec:trace-pipeline-three-layer
Ref: src/UniClaw.Core/Observability/ITraceStorage.cs (sync methods), src/UniClaw.Core/Observability/ITraceRecorder.cs (ErrorRecord no ParentNodeId, ExecutionRecord ParentNodeId stays)
Guard: 无 (convention-level)
Commit: pending

---

### D-23 | 2026-07-11 | HandlePreconditionCheck assume pass + explicit trace

Decision: HandlePreconditionCheck 无条件返回 Execute (assume pass)，加 TraceCoordinator.RecordDecision("precondition_assume_pass") 使 stub→实装过渡可观测。ITraversalNode 不暴露 Precondition 属性，真正 precondition 逻辑等 Phase 3。
Rationale: Phase 2.3 优先级是 FSM 闭环，不是接口扩展。assume pass 与 Python V6.7 _handle_precondition_check 一致。
Source: openspec:handler-implementation
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs HandlePreconditionCheck
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-24 | 2026-07-11 | HandleResultVerify 3-round retry + IsPopup popup 检测

Decision: HandleResultVerify 检查 PageSnapshotManager.HasChanged，最多 3 round retry + IVisionProvider re-call。弹窗检测用 PageAnalysis.IsPopup (vision/AI 权威判定)，不用 PopupDetector substring scan (false positive — "ad" in "Headphones Pro")。
Rationale: Python 实现是 3 round retry。PopupDetector 设计用于已知弹窗的分类，不适合做"当前页面是否有弹窗"的初始扫描。IsPopup 是 vision 层权威判定。
Source: openspec:handler-implementation
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs HandleResultVerify, docs/system/decisions/log.md D-6 in design
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-25 | 2026-07-11 | HandleErrorHandling 5-strategy RecoveryExecutor delegation

Decision: HandleErrorHandling 委托 ErrorHandler pipeline (classify → select → execute)。ErrorStrategy→FSM transition 映射: Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete。Consecutive error tracking: increment on Retry, reset on non-Retry。
Rationale: RecoveryExecutor 已有 dispatch-table pattern + fallback chain。ErrorHandling 只做 FSM transition 映射，不自己实现 recovery。
Source: openspec:handler-implementation
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs HandleErrorHandling
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-26 | 2026-07-11 | HandlePopupHandling PopupHandler pipeline delegation

Decision: HandlePopupHandling 委托 PopupHandler.HandlePopup() (6-step pipeline)。Success=true → ResultVerify, Success=false → ErrorHandling。PopupClassifier 在已知弹窗时做分类，不做初始检测。
Rationale: PopupHandler 已有完整 pipeline (detect → classify → preserve → dispatch → restore → validate)。PopupHandling 只做 FSM transition 映射。
Source: openspec:handler-implementation
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs HandlePopupHandling
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-27 | 2026-07-11 | HandleFrameComplete minimal — stack pop in StepOrchestrator

Decision: HandleFrameComplete 只决定 FSM transition (→ NodeSelect)，不操作 stack。Stack pop + frame teardown 由 StepOrchestrator Step 10 负责。
Rationale: FSM handler 职责边界是决定 transition，不操作 stack/cache/context。Stack pop 已在 StepOrchestrator 实现。
Source: openspec:handler-implementation
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs HandleFrameComplete, src/UniClaw.Core/Traversal/StepOrchestrator.cs Step 10
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-28 | 2026-07-11 | Graph 层服务/模型分离 — Services/ 子目录 + 服务接口

Decision: Graph/Models/ 只保留纯数据 record/enum/interface。PlanCompiler/DynamicMatcher/TemplateInstantiator 移入 Graph/Services/；PlaceholderResolver/TemplateValidator 移入 Graph/Services/ (static utilities)。提取 IPlanCompiler/IDynamicMatcher/ITemplateInstantiator 接口，TraversalEngine 改用接口类型注入 (替换 `new DynamicMatcher()` / `new TemplateInstantiator()`)。
Rationale: PlanCompiler/DynamicMatcher/TemplateInstantiator 是有行为逻辑的服务 class, 不是纯数据模型。与纯 records/enums 混放违反分层原则。缺少接口导致 TraversalEngine 直接依赖具体类 (不可 mock 测试)。PlaceholderResolver/TemplateValidator 虽为 static utility, 但作为服务组件的一部分也应统一归入 Services/。
Source: direct-commit (code review, Graph/ 目前只有 Models/ 子目录)
Ref: src/UniClaw.Core/Graph/Models/PlanCompiler.cs, DynamicMatcher.cs, TemplateInstantiator.cs, Template.cs (PlaceholderResolver + TemplateValidator)
Guard: 无 (convention-level)
Commit: pending
Status: Deferred · Target: Phase 2.3 (P3 in roadmap)

**迁移清单**:
| 文件 | 从 | 到 |
|------|----|----|
| PlanCompiler.cs | Graph/Models/ | Graph/Services/ (class) + Graph/Abstractions/ (IPlanCompiler) |
| DynamicMatcher.cs | Graph/Models/ | Graph/Services/ (class + MatchableItem/MatchResult 保持模型但迁 Models/?) |
| TemplateInstantiator.cs | Graph/Models/ | Graph/Services/ (class) + Graph/Abstractions/ (ITemplateInstantiator) |
| PlaceholderResolver + TemplateValidator | Graph/Models/Template.cs | Graph/Services/ (拆出独立文件) |
| ITemplateRegistry | Graph/Models/Template.cs | Graph/Abstractions/ (拆出独立文件) |

**⚠️ MatchableItem/MatchResult 归属待定**: DynamicMatcher.cs 当前混放 class + model records。MatchableItem 和 MatchResult 是数据模型, 可留 Models/ 或随 DynamicMatcher 迁 Services/。建议拆分: 模型记录迁 Models/, 服务 class 迁 Services/, 但增加文件数。最小改动方案: 整文件迁 Services/。

**⚠️ ITemplateRegistry 当前位置**: 定义在 Template.cs 中 (interface + model class 同文件)。建议拆出到 Graph/Abstractions/。

---

### D-V-1 | 2026-07-11 | Interface 定义位置 — 同文件嵌套

Decision: 6 新 interface 定义在 TraversalEngine.cs 内，与现有 INodeRegistry 同位置 (nested public interface)。
Rationale: 保持一致性 — INodeRegistry 已在 TraversalEngine.cs 内; 避免过度拆分文件 (6 interface 各 1 文件但内容极简); 现阶段 interface 是 sealed class 的镜像提取，与 class 同文件最直观。
Source: openspec:interface-extraction
Ref: docs/system/layers/traversal.md §1 Interfaces table, design.md D-V-1
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-V-2 | 2026-07-11 | Interface 方法签名 — 精确镜像 public API

Decision: 每个 interface 的方法签名精确镜像对应 sealed class 的 public 方法，不改参数类型或返回类型。唯一例外: ITraversalContext 替换 TraversalRuntimeContext (D-V-4), ITraceCoordinator? 替换 TraceCoordinator? (D-V-5)。
Rationale: 最小改动原则 — interface 是 sealed class 的 API 提取，不是 redesign。参数类型调整仅限于实现依赖倒置所必需的场景。
Source: openspec:interface-extraction
Ref: openspec/changes/interface-extraction/design.md D-V-2
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-V-3 | 2026-07-11 | PageSnapshotManager static → instance 转换

Decision: PageSnapshotManager 2 个 static 方法 (Fingerprint, HasChanged) 改为 instance 方法 (去掉 static 修饰符)。Sealed class 内部逻辑不变。
Rationale: C# interface 不能包含 static 方法 (C# 8 default impl 增加复杂度)。PageSnapshotManager 无 instance state — instance 方法后仍是纯计算。StepContext 字段已是 instance 调用形式。
Source: openspec:interface-extraction
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs PageSnapshotManager, design.md D-V-3
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-V-4 | 2026-07-11 | PageCacheManager / NodeStackAdapter 参数类型 → ITraversalContext

Decision: IPageCacheManager 和 INodeStackAdapter 方法签名用 ITraversalContext 替换 TraversalRuntimeContext。Sealed class 实现通过 cast 桥接。
Rationale: ITraversalContext 是已有 read-only interface，消费者不持有 TraversalRuntimeContext。D-I (Phase 3) 拆分后 TraversalRuntimeContext 不再是单 class，interface 需面向 ITraversalContext。
Source: openspec:interface-extraction
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (IPageCacheManager, INodeStackAdapter), design.md D-V-4
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-V-5 | 2026-07-11 | DynamicChildManager 构造器 → ITraceCoordinator

Decision: DynamicChildManager 构造器参数 TraceCoordinator? 改为 ITraceCoordinator?。
Rationale: 最小改动 — DynamicChildManager 只调用 TraceCoordinator 的 Record 方法，这些方法在 interface 上完全定义。
Source: openspec:interface-extraction
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs DynamicChildManager constructor, design.md D-V-5
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-V-6 | 2026-07-11 | StepContext 参数类型同步

Decision: StepContext 4 个字段从 concrete → interface: ChildMgr DynamicChildManager→IDynamicChildManager, Trace TraceCoordinator→ITraceCoordinator, SnapshotMgr PageSnapshotManager→IPageSnapshotManager, Stack NodeStackAdapter→INodeStackAdapter。
Rationale: D-V 的必要连带变更 — 如果 StepContext 保持 concrete 类型，mock 测试仍然不可能。
Source: openspec:interface-extraction
Ref: src/UniClaw.Core/StateMachine/StepContext.cs, design.md D-V-6
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-V-7 | 2026-07-11 | TraversalEngine 构造器保持向后兼容

Decision: TraversalEngine 构造器不改签名 — 保持 IVisionProvider + IActionExecutor + TraversalPlan + TraversalEngineConfig + ITraceRecorder?。子组件通过 Initialize() 中 new 创建，类型声明改为 interface。
Rationale: 构造器已在 Initialize() 中 new 所有子组件 — 子组件依赖 engine 内部创建的 context/registry。暴露子组件参数会爆炸参数列表 (5+6=11+参数)。
Source: openspec:interface-extraction
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs Initialize(), design.md D-V-7
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-V | 2026-07-12 | Interface Extraction 完成 — 6 接口 + StepContext interface 类型

Decision: **完成** — 6 接口提取 (IDynamicChildManager, ITraceCoordinator, IPageSnapshotManager, INodeStackAdapter, IEntryPolicyExecutor, IPageCacheManager) + StepContext interface 类型参数。所有接口和实现在 TraversalEngine.cs 内嵌定义 (同文件 nested public interface)。
Rationale: P1 优先级 — 为 P2 Context Decomposition (D-I) 铺路，使 StateMachine/Traversal 组件可 mock 测试，消除测试覆盖率天花板。当前 StepContext 已全 interface 类型，TraversalEngine.Initialize() 已使用 interface 声明 locals。
Source: openspec:interface-extraction
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (lines 418-1257), src/UniClaw.Core/StateMachine/StepContext.cs, docs/refactor/20-b-refactoring-roadmap-design.md §5
Guard: 无 (convention-level)
Commit: pending
Status: Fixed · Note: 所有 D-V 子决策 (D-V-1 至 D-V-7) 已锁定

**6 接口总结**:
| Interface | 行为职责 | 实现类 |
|-----------|---------|--------|
| IDynamicChildManager | 动态子节点生成 + 缓存失效 | DynamicChildManager |
| ITraceCoordinator | Trace 记录生命周期 (18 方法) | TraceCoordinator |
| IPageSnapshotManager | 页面指纹计算 + 变更检测 | PageSnapshotManager |
| INodeStackAdapter | NodeStack 操作封装 | NodeStackAdapter |
| IEntryPolicyExecutor | 入口策略执行 (3 strategies) | EntryPolicyExecutor |
| IPageCacheManager | 页面缓存管理 | PageCacheManager |

**StepContext 参数类型 (D-V-6)**:
- ChildMgr: IDynamicChildManager ✅
- NodeRegistry: INodeRegistry ✅ (已存在)
- Trace: ITraceCoordinator ✅
- SnapshotMgr: IPageSnapshotManager ✅
- Stack: INodeStackAdapter ✅

---

### D-I | 2026-07-12 | Context Decomposition 完成 — 5 Sub-Contexts + Container Pattern

Decision: **完成** — TraversalRuntimeContext God Object (30 fields) → 5 sub-contexts (Navigation/Error/Session/Progress/Cache) per Container pattern。所有 sub-contexts 在 StateMachine/ 子目录，持有只读接口 + mutable sealed class。
Rationale: P1 优先级 — 解决 D-15 (subsystem canonical attribution) + D-V (interface extraction) 后的遗留 God Object 问题。TraversRuntimeContext 现为纯 Container，5 sub-contexts immutable 引用，617 CI tests通过。
Source: openspec:context-decomposition
Ref: src/UniClaw.Core/StateMachine/Navigation/, src/UniClaw.Core/StateMachine/Error/, src/UniClaw.Core/StateMachine/Session/, src/UniClaw.Core/StateMachine/Progress/, src/UniClaw.Core/StateMachine/Cache/, docs/refactor/2026-07-12-context-decomposition-design.md
Guard: SubsystemBoundaryGuardTests.TraversalRuntimeContext_FieldCountsPerSubsystem (verifies 0 fields remain in TraversalRuntimeContext after extraction)
Commit: pending
Status: Fixed · Note: 所有 5 phases (Navigation → Error → Session → Progress → Cache) 已完成

**5 Sub-Contexts 总结**:
| Sub-Context | 字段数 | 职责 | Interface |
|-------------|--------|------|-----------|
| NavigationContext | 12 | DFS traversal state (NodeStack, CurrentPath, VisitedPages/Nodes/Children, PageTree, CurrentFrame) | INavigationContext |
| ErrorContext | 5 | Error tracking (FailedNodes, ConsecutiveErrors, RetryCount, LastError, ExceptionChain) | IErrorContext |
| SessionContext | 4 | Macro session (TraceId, GlobalState, DeviceExperience, AIProvider) | ISessionContext |
| ProgressContext | 5 | Progress control (StepCount, MaxDepth, CompletionPolicy, ActionHistory, WaitAfterActionMs) | IProgressContext |
| CacheContext | 2+2 | Cache (PageCache, CacheValid) + Phase 3 reserved (ScrollHandler, CurrentSnapshot) | ICacheContext |

**Container Pattern**:
- TraversalRuntimeContext 持有 5 个 readonly sub-context 引用 (构造时创建，永不替换)
- 所有 ITraversalContext 属性委托到对应 sub-context
- Engine 通过 concrete sub-context 访问 mutation 方法 (IncrementStepCount, MarkVisited, etc.)
- CreateReadOnlySnapshot() 仍正常工作，从各 sub-context 提取不可变快照

---

### D-29 | 2026-07-12 | ITraversalContext 保持所有属性，只移除 setters

Decision: ITraversalContext 保留所有 9 个属性的 getters，只移除 3 个 setters (CurrentFrame, GlobalState, LastError)。
Rationale: 外部通过 ITraversalStateMachine.Context 读取 GlobalState/LastError 是合理的（了解当前状态），问题在于 **setter**，不在于 getter。最小改动原则。
Source: openspec:itraversalcontext-reform
Ref: openspec/changes/itraversalcontext-reform/design.md Decision 1
Guard: 无 (convention-level)
Commit: 9762b08
Status: Fixed

---

### D-30 | 2026-07-12 | Mutation 通过 concrete class 方法

Decision: ITraversalContext 的 mutation 通过 TraversalRuntimeContext 的 SetXxx() 方法 (SetCurrentFrame, SetGlobalState, SetLastError)。
Rationale: 符合 D-I/D-V 模式 — 接口只读，concrete 可变。方法调用比属性赋值更明确这是 mutation 操作。
Source: openspec:itraversalcontext-reform
Ref: openspec/changes/itraversalcontext-reform/design.md Decision 2
Guard: 无 (convention-level)
Commit: 9762b08
Status: Fixed

---

### D-31 | 2026-07-12 | TraversalFSM RuntimeContext 属性暴露可写视图

Decision: TraversalFSM 添加 RuntimeContext 属性（TraversalRuntimeContext concrete 类型），FSM 内部用于 mutation，Context 保持 ITraversalContext 只读视图。
Rationale: ITraversalStateMachine.Context 保持 ITraversalContext（只读视图）— 不破坏现有接口。RuntimeContext 提供可写视图 — FSM 内部使用。符合"接口隔离"原则。
Source: openspec:itraversalcontext-reform
Ref: openspec/changes/itraversalcontext-reform/design.md Decision 3
Guard: 无 (convention-level)
Commit: 9762b08
Status: Fixed

---

### D-32 | 2026-07-12 | Scroll Accumulation Mode — Threshold-based Visibility

Decision: ScrollSegment 使用 accumulation mode 语义：所有 `Threshold <= CurrentProgress` 的 segments 均贡献可见元素。
Rationale: 对齐 Python V7.0 行为，语义自然（"向下滚动更多内容出现"），实现简单可验证，支持精确控制元素出现时机。
Source: openspec:scroll-simulation-enhancement
Ref: openspec/changes/scroll-simulation-enhancement/design.md Decision 1, src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-33 | 2026-07-12 | Scroll Element Deduplication — Lowest Threshold Wins

Decision: 相同元素 ID 出现在多个 segments 时，只返回最低 threshold 的实例。
Rationale: 防止遍历中重复访问同一元素，匹配用户预期（同一元素 = 同一身份），支持可靠的"已访问子节点"跟踪。
Source: openspec:scroll-simulation-enhancement
Ref: openspec/changes/scroll-simulation-enhancement/design.md Decision 2, src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-34 | 2026-07-12 | Jump Detection as Core Chain Logic

Decision: 跳跃检测（元素不连续）是 ScrollHandler pipeline 的一部分，不是测试验证关注点。
Rationale: 跳跃可在真实场景发生（激进 step sizing、稀疏 segments），恢复需要 rollback + retry（操作逻辑），使滚动行为健壮而非脆弱，统计跟踪帮助诊断滚动效率。
Source: openspec:scroll-simulation-enhancement
Ref: openspec/changes/scroll-simulation-enhancement/design.md Decision 3, src/UniClaw.Core/StateMachine/Scroll/JumpDetector.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

**Recovery Strategy**: 检测 (BeforeElements ∩ AfterElements = ∅，both non-empty) → Rollback (恢复进度) → Retry (减少 step size，默认 0.5x) → 重复 (直到 overlap 或 max retries)

---

### D-35 | 2026-07-12 | Adaptive Step Calculation — Duplicate Ratio Triggered

Decision: 当重复元素比例超过阈值（默认 70%）且新元素数量 >= MinSampleSize（默认 3）时，增加滚动 step。
Rationale: 高重复比例 = 小有效移动 = 低效滚动。自适应 sizing 减少冗余滚动操作。可配置允许 per-scenario 调优。
Source: openspec:scroll-simulation-enhancement
Ref: openspec/changes/scroll-simulation-enhancement/design.md Decision 4, src/UniClaw.Core/StateMachine/Scroll/AdaptiveStepCalculator.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

**Formula**: IF (DuplicateRatio >= Threshold) AND (NewElementCount >= MinSampleSize) THEN NextStep = Min(CurrentStep * IncreaseFactor, MaxScrollStep)

---

### D-36 | 2026-07-12 | ScrollHandler 7-step Pipeline Architecture

Decision: ScrollHandler 采用 pipeline 架构：Detect → Classify → Decide → Execute → Verify → Recover → Statistics。
Rationale: 每步是纯函数或副作用隔离，易于单独测试组件，职责清晰分离，符合 handler-pipeline.md 模式。
Source: openspec:scroll-simulation-enhancement
Ref: openspec/changes/scroll-simulation-enhancement/design.md Decision 5, docs/system/patterns/handler-pipeline.md
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

**Step Responsibilities**:
1. **Detect**: 判断 scrollability (NotScrollable, CanScrollDown, AtBottom, CanScrollUp)
2. **Classify**: 计算 progress, max threshold, recommended step
3. **Decide**: 映射 scrollability 到 action type (None, ScrollDown, ScrollUp)
4. **Execute**: 通过 Hook Dispatch table 执行滚动
5. **Verify**: 通过元素 overlap 检测跳跃
6. **Recover**: 处理跳跃（rollback + retry）
7. **Statistics**: 跟踪滚动指标

---

### D-37 | 2026-07-12 | ScrollableMock Services as Extensions — Backward Compatible

Decision: ScrollableMockVisionService 和 ScrollableMockActionExecutor 是独立类，不是 StatefulMockVisionService/StatefulMockActionExecutor 的替换。
Rationale: 向后兼容 — 现有测试使用 StatefulMock* 不变。Opt-in — 滚动场景显式使用 scrollable services。清晰意图 — 类型名指示滚动能力。无破坏性 API 变更。
Source: openspec:scroll-simulation-enhancement
Ref: openspec/changes/scroll-simulation-enhancement/design.md Decision 6, src/UniClaw.Core/Simulation/Scroll/
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

**Usage Pattern**:
```csharp
// Non-scroll test (unchanged)
var vision = new StatefulMockVisionService(fixture);

// Scroll test (new)
var vision = new ScrollableMockVisionService(fixture);
```

---

### D-38 | 2026-07-12 | Scroll Progress-Based Loop Prevention

Decision: TryHandleScroll 在重置 VisitedChildren 前必须验证滚动实际前进进度。progressDelta <= Config.ProgressEpsilon 时返回 FrameComplete, 不重置。
Rationale: 滚动未前进 = 无新内容, 重置 VisitedChildren 会导致无限循环。
Source: openspec:fsm-scroll-loop-fix
Ref: openspec/changes/archive/2026-07-12-fsm-scroll-loop-fix/design.md §D1
Guard: 无 (convention-level: FSM internal logic)
Commit: pending
Status: Fixed

---

### D-39 | 2026-07-12 | Scroll Element Count-Based Loop Prevention

Decision: TryHandleScroll 必须验证滚动揭示了新去重元素。uniqueAfter <= uniqueBefore 时返回 FrameComplete, 不重置。
Rationale: 即使进度前进, 内容可能相同 (稀疏分段)。需要检查实际新元素可见性。
Source: openspec:fsm-scroll-loop-fix
Ref: openspec/changes/archive/2026-07-12-fsm-scroll-loop-fix/design.md §D2
Guard: 无 (convention-level: FSM internal logic)
Commit: pending
Status: Fixed

---

### D-40 | 2026-07-12 | DynamicMatch Scroll Trigger Support

Decision: HandleBranch 必须为 DynamicMatch 策略检查滚动。无未访问子节点时调用 TryHandleScroll。
Rationale: 原代码跳过 DynamicMatch 滚动检查, 导致滚动增强无法用于动态匹配场景。
Source: openspec:fsm-scroll-loop-fix
Ref: openspec/changes/archive/2026-07-12-fsm-scroll-loop-fix/design.md §D3
Guard: 无 (convention-level: FSM internal logic)
Commit: pending
Status: Fixed

---

### D-41 | 2026-07-12 | Selective VisitedChildren Reset (Simplified)

Decision: 滚动后重置 VisitedChildren 使用完全重置 (非选择性), 依赖 D1/D2 循环检测。选择性重置需访问 TraversalEngine.StaticNodes 进行名称到 ID 映射。
Rationale: 元素名称 (PageAnalysis) 与节点 ID (VisitedChildren) 不匹配, 精确映射需额外架构支持。
Source: openspec:fsm-scroll-loop-fix
Ref: openspec/changes/archive/2026-07-12-fsm-scroll-loop-fix/design.md §D4
Guard: 无 (architectural limitation, documented in tasks.md)
Commit: pending
Status: Deferred · Target: Future architecture redesign

---

### D-42 | 2026-07-12 | IsEndOfList Early Exit Check

Decision: TryHandleScroll 必须在创建 ScrollHandler 之前检查 IsEndOfList。已到达列表末尾时返回 FrameComplete。
Rationale: 早期退出避免不必要的 ScrollHandler 创建, 控制流更明确。
Source: openspec:fsm-scroll-loop-fix
Ref: openspec/changes/archive/2026-07-12-fsm-scroll-loop-fix/design.md §D5
Guard: 无 (convention-level: FSM internal logic)
Commit: pending
Status: Fixed

---

### D-43 | 2026-07-12 | Baseline Reporting 架构

Decision: 轻量级 ReportWriter (方案 A) — 在现有 Verify → Assert 链上追加 BaselineReportCollector + BaselineReportWriter, 输出 JSON 每场景报告 + Markdown index.md 汇总。每次运行全量覆盖, 只保留最新。Scroll 数值由测试层传入, 不修改 TraversalResult。
Rationale: 最小侵入, 零新依赖, 自然延伸 Verify 链。测试只需加 1 行 Collector.Add(), 不强制继承基类。
Source: brainstorming:baseline-test-reporting
Ref: docs/prd/2026-07-12-baseline-test-reporting.md, openspec/changes/baseline-test-reporting/
Guard: 无 (test infrastructure convention-level)
Commit: pending
Status: Fixed

---

### D-44 | 2026-07-12 | Scroll Baseline Test — Independent Test Class

Decision: ScrollableBaselineTests.cs 作为独立测试类，不扩展 SimulationBaselineTests.cs。滚动场景使用 DynamicMatch + ScrollableMockVisionService + ScrollDataStore。
Rationale: 关注点分离 — 滚动 vs 非滚动场景能力不同。Fixture 隔离 — ScrollDataStore vs StateFixture。清晰可发现性 — 独立类名立即表明滚动能力。
Source: openspec:scrollable-baseline-test
Ref: openspec/changes/scrollable-baseline-test/design.md §D1
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-45 | 2026-07-12 | Scroll Baseline Fixture Strategy — Hybrid

Decision: 1 个主 WiFi 列表 fixture (7 段, 24 唯一元素, 3 重叠) 共享 4 个场景 + 2 个特殊 fixture (sparse jump, overlapping adaptive) 用于聚焦验证。所有 fixture 通过 ScrollDataStore 数据驱动。
Rationale: 主 fixture 覆盖多种场景 (无需重复定义)，特殊 fixture 针对特定行为 (跳跃检测, 自适应步长)。数据驱动维护 —— ScrollDataStore API 易于调整。
Source: openspec:scrollable-baseline-test
Ref: openspec/changes/scrollable-baseline-test/design.md §D2
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-46 | 2026-07-12 | ExpectedBehavior numericAnchor — Scroll Metrics Extension

Decision: numericAnchor 新增 7 个滚动特有字段 (scrollCount, scrollDistance, scrollUpCount, jumpDetected, jumpRecovered, finalProgress, adaptiveStepIncreases)。保留现有字段 (totalSteps, visitedPagesCount 等) 不变。
Rationale: 向后兼容 — numericAnchor 是松散 map，未知键被忽略，不破坏现有 JSON。滚动特定验证 — 新字段提供滚动行为专属指标。所有 numericAnchor 为 INFO 级别 (non-CI-blocking)。
Source: openspec:scrollable-baseline-test
Ref: openspec/changes/scrollable-baseline-test/design.md §D3, src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.cs NumericAnchorDto
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-47 | 2026-07-12 | ScrollableMockVisionService.FindElementAt — Dual Search

Decision: FindElementAt 先搜索 fixture 元素，再后备搜索 ScrollDataStore 可见元素（累积模式 + 去重）。新增 GetVisibleElementsFromScrollData 私有方法提供后备搜索。
Rationale: DynamicMatch 从滚动数据元素解析坐标 → TapAsync 需在 ScrollableMockVisionService 中找到这些坐标对应的元素。原实现仅搜索 fixture 元素 → 滚动数据坐标无匹配 → TapAsync 返回 false。双搜索确保 fixture 和滚动数据元素均可被点击。
Source: openspec:scrollable-baseline-test
Ref: openspec/changes/scrollable-baseline-test/design.md §Implementation Findings, src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-48 | 2026-07-13 | 从 ScrollHistory 计算指标而非引入新结构

Decision: 直接从 ScrollableMockActionExecutor.ScrollHistory 计算滚动指标，不创建 ScrollStatistics 类型。
Rationale: 滚动是遍历过程的一部分，不是独立目标；遍历关注"所有元素被访问"，滚动只是手段；滚动信息已在 ActionHistory 中体现。
Source: openspec:baseline-scroll-metrics-fix
Ref: openspec/changes/archive/2026-07-13-baseline-scroll-metrics-fix/design.md Decision 1
Guard: 无 (convention-level)
Commit: 6231286
Status: Fixed

**Rejected Alternative**: 引入 ScrollStatistics record 并扩展 TraversalResult — 拒绝因为需要破坏性变更和版本管理

---

### D-49 | 2026-07-13 | ScrollDistance 计算方式

Decision: ScrollDistance = lastScroll.AfterProgress - firstScroll.BeforeProgress
Rationale: 反映实际滚动的总距离，对于到底的场景应该接近 1.0
Source: openspec:baseline-scroll-metrics-fix
Ref: openspec/changes/archive/2026-07-13-baseline-scroll-metrics-fix/design.md Decision 2
Guard: 无 (convention-level)
Commit: 6231286
Status: Fixed

**Rejected Alternative**: 使用 finalProgress (0.0 - 1.0) — 拒绝因为无法反映中间滚动

---

### D-50 | 2026-07-13 | 高级指标暂时保持 0

Decision: JumpDetected, JumpRecovered, AdaptiveStepIncreases 硬编码为 0，标记为 Phase 3 Future Work。
Rationale: 这些指标需要 ScrollHandler.Statistics 数据，当前测试场景不使用 ScrollHandler，避免过度设计。
Source: openspec:baseline-scroll-metrics-fix
Ref: openspec/changes/archive/2026-07-13-baseline-scroll-metrics-fix/design.md Decision 3
Guard: 无 (convention-level)
Commit: 6231286
Status: Fixed · Deferred · Target: Phase 3

**Rejected Alternative**: 集成 ScrollHandler 到测试场景 — 拒绝因为范围超出了"最小改动"

---

### D-51 | 2026-07-13 | 滚动指标验证规则不 CI-blocking

Decision: 滚动指标在 BaselineReport 中展示对比，但不作为 VerificationReport.AllPassed 阻塞条件。
Rationale: 滚动指标是 informational 参考锚点，类似 TotalSteps, VisitedPagesCount。
Source: openspec:baseline-scroll-metrics-fix
Ref: openspec/changes/archive/2026-07-13-baseline-scroll-metrics-fix/design.md Trade-off
Guard: 无 (convention-level)
Commit: 6231286
Status: Fixed

---

### D-52 | 2026-07-13 | 高级基线测试类组织：按 Complexity Dimension 分离

Decision: 创建两个独立测试类 HierarchyBaselineTests + LongListBaselineTests，不合并到现有类。
Rationale: 遵循现有模式（SimulationBaselineTests vs ScrollableBaselineTests），清晰关注点分离：深层导航 vs 长列表滚动。
Source: openspec:advanced-simulation-baseline
Ref: openspec/changes/archive/2026-07-13-advanced-simulation-baseline/design.md §Decisions.1
Guard: 无
Commit: pending
Status: Deferred · Target: Phase 3 (ScrollHandler Integration)

---

### D-53 | 2026-07-13 | 高级基线层级深度：4 层级

Decision: 高级基线测试采用 4 层级深度（12 页应用），Level 3 包含 3 个可滚动页面。
Rationale: 比现有 2-3 层更深，能暴露深层返回导航问题；3 个可滚动页面验证多页面滚动状态管理。
Source: openspec:advanced-simulation-baseline
Ref: openspec/changes/archive/2026-07-13-advanced-simulation-baseline/design.md §Decisions.2
Guard: 无
Commit: pending
Status: Deferred · Target: Phase 3 (ScrollHandler Integration)

---

### D-54 | 2026-07-13 | 多页面滚动 ExpectedBehavior 限制处理

Decision: 层级场景（多页面滚动）设置 finalProgress=0.0，添加 _note 说明不适用。
Rationale: NumericAnchor 只有一个 FinalProgress 字段无法表示多页面滚动；通过约定解决，避免 schema 变更。
Source: openspec:advanced-simulation-baseline
Ref: openspec/changes/archive/2026-07-13-advanced-simulation-baseline/design.md §Decisions.3
Guard: 无
Commit: pending
Status: Deferred · Target: Phase 3 (ScrollHandler Integration)

---

### D-55 | 2026-07-13 | 滚动列表 ElementCoverage 手动列举

Decision: 滚动列表元素在 ElementCoverage.Required 中手动列出所有项，不使用 auto_derive。
Rationale: WithFixtureDerivation 只能从 StateFixture 推导，滚动元素在 ScrollDataStore 中；扩展推导逻辑会增加复杂度。
Source: openspec:advanced-simulation-baseline
Ref: openspec/changes/archive/2026-07-13-advanced-simulation-baseline/design.md §Decisions.4
Guard: 无
Commit: pending
Status: Deferred · Target: Phase 3 (ScrollHandler Integration)

---

### D-56 | 2026-07-13 | ScrollHandler 集成阻塞高级基线测试

Decision: 高级基线测试（HierarchyBaselineTests, LongListBaselineTests）推迟到 ScrollHandler 集成完成。
Rationale: TraversalEngine 缺少滚动感知，DynamicMatch 只能看到 threshold=0.0 的元素，无法触发滚动访问后续内容。
Source: openspec:advanced-simulation-baseline
Ref: openspec/changes/archive/2026-07-13-advanced-simulation-baseline/SCROLL_HANDLER_INTEGRATION_PLAN.md
Guard: 无
Commit: pending
Status: Active · Target: Phase 3 (ScrollHandler Integration)

---

### D-57 | 2026-07-13 | ScrollHandler Integration — Inline Approach (No New FSM State)

Decision: ScrollHandler integration 采用内联方式 (TryHandleScroll in HandleBranch + StepOrchestrator Step 9)，不新增 TraversalState.ScrollCheck 状态。
Rationale: C-1 锁定 TraversalState 为 8 值，新增 ScrollCheck 违反宪法约束。现有内联滚动处理已覆盖 ScrollableBaselineTests（6/6 通过），无需新 FSM 状态。TryHandleScroll + Step 9 实现等效的滚动决策点，内联方式更简单。
Source: openspec:scroll-handler-integration
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs (TryHandleScroll, lines 415-540), src/UniClaw.Core/Traversal/StepOrchestrator.cs (Step 9, lines 89-159)
Guard: EnumValueGuardTests.TraversalState_Has8Values (remains at 8)
Commit: pending
Status: Fixed

**Rejected Alternative**: 新增 ScrollCheck FSM 状态 (design.md Decision 1) — 违反 C-1。等效行为已通过内联实现。

---

### D-58 | 2026-07-13 | ExitConditionType — AllChildrenVisitedOrScrollEnd 语义标记

Decision: 新增 `AllChildrenVisitedOrScrollEnd` 到 ExitConditionType enum（4 值），作为语义标记。CompletionDetector 不直接使用 ExitConditionType（使用 FallbackAction），实际滚动检测在 TryHandleScroll 中处理。
Rationale: 标记"子节点访问完或到达滚动末尾"的退出意图。滚动感知行为已在 TryHandleScroll + Step 9 实现，不需要 CompletionDetector 修改。枚举值无宪法约束（ExitConditionType 不在 locked enums 中）。
Source: openspec:scroll-handler-integration
Ref: src/UniClaw.Core/Graph/Models/TraversalNode.cs (ExitConditionType enum, line 136)
Guard: 无 (ExitConditionType 不在 ArchitectureGuardTests 中)
Commit: pending
Status: Fixed

---

### D-59 | 2026-07-13 | IVisionProvider 滚动接口 — virtual 默认实现

Decision: IVisionProvider 的 HasScroll()/GetScrollProgress()/IsEndOfList() 使用 virtual 默认实现（返回 false/0.0/true），确保向后兼容。
Rationale: 现有实现（StatefulMockVisionService）自动继承默认行为，无需修改代码。ScrollableMockVisionService 通过显式接口实现覆盖。
Source: openspec:scroll-handler-integration
Ref: src/UniClaw.Core/StateMachine/StepContext.cs (IVisionProvider, lines 24-32)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-60 | 2026-07-13 | Baseline Reporting — Collector + Writer Separation

Decision: Separate `BaselineReportCollector` (collection + xUnit lifecycle) from `BaselineReportWriter` (serialization + I/O) with static methods.
Rationale: Single Responsibility — Collector owns xUnit fixture lifecycle and data aggregation, Writer owns JSON/Markdown serialization. Enables independent testing of Writer with mock data.
Source: openspec:baseline-test-reporting
Ref: tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs, tests/UniClaw.Core.Tests/Baseline/BaselineReportWriter.cs
Guard: 无 (test infrastructure only)
Commit: d6195ab
Status: Fixed

---

### D-61 | 2026-07-13 | Baseline Reporting — xUnit Collection Fixture Lifecycle

Decision: Use `ICollectionFixture<BaselineTestsFixture>` with `[Collection("Baseline Tests")]` and `DisableParallelization = true` for baseline test report collection.
Rationale: Ensures all baseline tests run sequentially through the same Collector instance. xUnit guarantees `Dispose()` runs after all tests complete, triggering `WriteAll()`. No race conditions on shared collection state.
Source: openspec:baseline-test-reporting
Ref: tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs (BaselineTestsFixture)
Guard: 无 (test infrastructure only)
Commit: d6195ab
Status: Fixed

---

### D-62 | 2026-07-13 | Baseline Reporting — actualNumeric Construction in Collector

Decision: Collector accepts optional `executor?` and `vision?` parameters, constructs `actualNumeric` internally by merging `TraversalResult` data with mock service scroll metrics.
Rationale: Centralizes data extraction logic (one place to maintain). Keeps test code simple (1-2 lines vs 8 lines of manual extraction). TraversalResult already provides 70% of needed data.
Source: openspec:baseline-test-reporting
Ref: tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs (BuildActualNumeric)
Guard: 无 (test infrastructure only)
Commit: d6195ab
Status: Fixed

---

### D-63 | 2026-07-13 | Baseline Reporting — Scroll Metrics from Existing Data

Decision: Extract scroll metrics (`ScrollCount`, `ScrollDistance`, `ScrollUpCount`) from existing `ScrollHistory` and `ScrollState` rather than adding new state tracking.
Rationale: YAGNI — don't add state until needed for verification logic. `ScrollHistory` already records each scroll operation. Jump/Recovery/Adaptive metrics return 0 for now (Phase 3 adds real detection).
Source: openspec:baseline-test-reporting
Ref: tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs (BuildActualNumeric), src/UniClaw.Core/Simulation/Scroll/ScrollAction.cs
Guard: 无 (test infrastructure only)
Commit: d6195ab
Status: Fixed

---

### D-64 | 2026-07-13 | Baseline Reporting — Error Handling: Silent Fail with Console Logging

Decision: Wrap all report I/O in try-catch, log errors to `Console.WriteLine`, never fail tests. Individual file failures don't prevent other files from writing.
Rationale: Report generation is informational — baseline quality is enforced by Assert. Console output visible during local dev, doesn't break CI.
Source: openspec:baseline-test-reporting
Ref: tests/UniClaw.Core.Tests/Baseline/BaselineReportWriter.cs (WriteJson, WriteIndex, WriteAll)
Guard: 无 (test infrastructure only)
Commit: d6195ab
Status: Fixed

---

### D-65 | 2026-07-13 | BaselineReport — Minimal Fields Only

Decision: `BaselineReport` contains only `Scenario`, `Timestamp`, `AllPassed`, `Details`, `ExpectedNumeric`, `ActualNumeric`. Aggregate stats computed during index generation, not stored per-report.
Rationale: Removed `Description` (no data source), `TotalScenarios/PassedScenarios` (aggregate-level). Simpler record with clear purpose — each report represents exactly one test scenario.
Source: openspec:baseline-test-reporting
Ref: tests/UniClaw.Core.Tests/Baseline/BaselineReport.cs
Guard: 无 (data model, test infrastructure only)
Commit: d6195ab
Status: Fixed

---

### D-60 | 2026-07-13 | DynamicChildManager Fingerprint-Aware Cache Auto-Invalidation

Decision: DynamicChildManager cache entries store page fingerprint alongside children. GetNextUnvisitedChild auto-detects fingerprint mismatch → invalidates and regenerates from current page analysis.
Rationale: Multi-page hierarchy traversal hit max_steps (1000) due to stale DynamicMatch caches. After navigation, root's cached children had Text targets from the old page. OperationDispatcher threw InvalidOperationException for non-Coordinate targets → error-retry loops. Fingerprint-aware caching ensures children always match current page. Additional explicit invalidation in StepOrchestrator Step 9 and TraversalFSM.TryHandleScroll handles scroll-triggered page analysis changes.
Source: direct-commit (advanced-simulation-baseline engine fix)
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (DynamicChildManager, lines 449-517)
Guard: 无 (verified by 15/15 baseline + 721/721 full suite)
Commit: pending
Status: Fixed

---

### D-66 | 2026-07-14 | StepOrchestrator: Scroll Logic in Branch State (Step 8 Fix)

Context: LongListBaselineTests (3 scenarios) passed trivially — `scrollCount=0`, `finalProgress=0`, only first scroll segment's items visited. Engine terminated with `all_visited` after exhausting first-segment children because scrolling never triggered.

Root cause: FSM uses **Branch** state (Step 8) for DynamicMatch node child selection, not **NodeSelect** (Step 9). Step 8's `else` branch (no more children → `frameCompleted=true`) had no scroll check. A DynamicMatch page with scrollable content would exhaust segment-0 children and terminate immediately.

Fix:
1. Added scroll logic to Step 8 (Branch) else branch when `currentFrame.ChildrenStrategy.Type == DynamicMatch` and `nextChild == null`
2. Extracted shared `TryHandleScroll()` helper used by both Step 8 and Step 9
3. `TryHandleScroll` routes through `ScrollableMockActionExecutor.ScrollDown()` for metrics capture (was calling `vision.SimulateScroll` directly → `scrollCount` always 0)
4. Fallback: direct `SimulateScroll` when executor doesn't support scroll

Result:
- long-list (30 items, 8 segments): scrollCount 0→4, finalProgress 0.0→1.0, totalSteps 20→124
- sparse-list (25 items, 6 segments): scrollCount 0→3, finalProgress 0.0→1.0, totalSteps 16→105
- dense-list (20 items, 10 segments): scrollCount 0→4, finalProgress 0.0→1.0, totalSteps 8→44
- All 3 LongListBaselineTests now meaningfully verify full scroll traversal

Source: direct-commit (long-list calibration fix)
Ref: src/UniClaw.Core/Traversal/StepOrchestrator.cs (TryHandleScroll + Step 8/9 scroll logic)
Guard: 721/721 full suite pass
Commit: pending
Status: Fixed

### D-67 | 2026-07-14 | Baseline JSON Calibration Procedure

Context: LongList 3 scenarios had numericAnchor all-zero values (`_note: "运行测试后更新实际值"`), meaning the tests verified completion/reason but skipped all numeric validation. This is the correct initial state for new baseline tests, but a documented procedure was missing.

Decision: Established calibration procedure in `docs/system/layers/simulation-baseline.md` §4.1:
1. **Initial state**: all `numericAnchor` values = 0 (skip mode), `_note` describes expected behavior
2. **Calibration**: run tests → capture actual values → set `numericAnchor` with ±5% tolerance
3. **elapsedSecondsMax**: set 5-10× actual to avoid CI flakiness
4. **Meaningful thresholds**: `requiredRatio` = 0.95 (full traversal) or 0.60 (target search); `finalProgress` = 1.0 (end-of-list)
5. **Phase 3 fields**: `jumpDetected`, `jumpRecovered`, `adaptiveStepIncreases` stay at 0 until detectors are implemented
6. **Verify**: full suite must pass after calibration

LongList calibration values (2026-07-14):
- long-list: totalSteps=124, scrollCount=4, finalProgress=1.0
- sparse-list: totalSteps=105, scrollCount=3, finalProgress=1.0
- dense-list: totalSteps=44, scrollCount=4, finalProgress=1.0

Source: direct-commit (long-list calibration)
Ref: docs/system/layers/simulation-baseline.md §4.1
Guard: 无 (process rule, not code constraint)
Commit: pending
Status: Rule Established

---

### D-68 | 2026-07-14 | Scroll = Action + Judgment (seen-set diff termination)

Context: engine 滚动集成被绕过 —— 两处 `TryHandleScroll` 都注释"不使用 ScrollHandler(简化逻辑)"、硬编码 stepPercent、运行时下转 Simulation mock; 9 类 ScrollHandler 管线是冷钝代码。根因: 把滚动当成需要 progress/threshold/jump-detect/verify/recover 的特殊领域概念。

Decision: 滚动回归本质 —— **一次操作 (SwipeAsync) + 对新截图的判断 (AnalyzeCurrentPageAsync)**, 与 engine 处理任何操作后重新分析页面同一套机制。终止 = per-frame 累积 seen 元素 id 集合差分 (滚动后无未见元素 = 到底), 经验式, 对真实服务鲁棒 (IsEndOfList 不可靠时仍成立)。统一单站点 `StepOrchestrator.TryHandleScroll` (Step 8/9 共用); FSM 不再持有滚动职责 (`HandleBranch` 对耗尽 DynamicMatch 返回 NodeSelect)。零新 enum/接口方法。

**Supersedes:**
- D-34 (跳跃检测核心链路) — 跳跃检测不再作为 engine 概念
- D-35 (自适应步长) — 固定/可配步长, 无自适应管线
- D-36 (ScrollHandler 7 步管线) — 管线整体删除, seen-set 差分取代
- D-38 (progress-based 循环防护) — seen 集合差分取代
- D-39 (元素计数循环防护) — seen 集合差分取代
- D-40 (DynamicMatch 滚动触发) — 保留触发点, 统一到 orchestrator
- D-41 (选择性 VisitedChildren 重置) — seen-set 差分 + Invalidate 取代
- D-42 (IsEndOfList 早退) — 保留为辅助, 主终止信号是 seen-set

Source: openspec change `scroll-action-refactor`
Ref: src/UniClaw.Core/Traversal/StepOrchestrator.cs (TryHandleScroll), src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs (RecordSeenElementIds)
Guard: ScrollLoopTerminationTests (8 tests), EngineLayers_DoNotReferenceSimulation
Commit: pending
Status: Decided

### D-69 | 2026-07-14 | Delete cold Scroll pipeline + dead code

Context: D-68 把滚动改为操作+判断后, `StateMachine/Scroll/` 9 类管线 (ScrollHandler/ScrollabilityDetector/ScrollClassifier/ScrollDecider/ScrollActionExecutor/JumpDetector/JumpRecoveryHandler/AdaptiveStepCalculator/ScrollStatisticsCollector) + 依附类型 (ScrollActionResult/ScrollVerifyResult/JumpRecoveryResult/ScrollContext/ScrollAction/ScrollActionType/OverlapStatus) 全部成为冷钝代码。`Traversal/ScrollAwareNodeSelector.cs` 是死代码 (唯一消费者 ScrollHandler 未接入, GetCurrentPageAnalysis 永返 null)。两处 `TryHandleScroll` 逻辑分叉。

Decision: 删除整目录 `StateMachine/Scroll/` + 依附类型; 删除 `ScrollAwareNodeSelector.cs`; 删除 `TraversalFSM.TryHandleScroll` + `_visitedScrollRanges`; 收敛为 orchestrator 单站点。`ScrollActionType` enum 删除 (非 Guard 锁定, 仅 log 记录)。

**Supersedes:**
- D-36 (ScrollHandler 管线架构) — 删除
- D-37 (ScrollableMock 作为独立扩展类) — 改为 SimulatedScreen + 两个薄适配器 (见 D-70)
- D-47 (FindElementAt 双搜索) — 迁入 SimulatedScreen (chrome + 内容统一搜索)

Source: openspec change `scroll-action-refactor`
Ref: (deleted) src/UniClaw.Core/StateMachine/Scroll/, src/UniClaw.Core/Traversal/ScrollAwareNodeSelector.cs
Guard: dotnet build 0 errors; EngineLayers_DoNotReferenceSimulation
Commit: pending
Status: Decided

### D-70 | 2026-07-14 | SimulatedScreen + dynamic paged content source (mock-only coordination)

Context: swipe (变异) 与 analyze (观察) 是 engine 两次独立接口调用, mock 侧须作用在同一屏幕状态。旧 mock 用静态 ScrollDataStore/ScrollSegment (每场景预构段数据), 复用度低; 两适配器具体互引 (ScrollableMockActionExecutor → ScrollableMockVisionService)。

Decision: 抽出共享可变 `SimulatedScreen` (mock-only), 拥有 currentPageId/导航历史/视口 pageIndex/`IScrollContentSource`/`ScrollBehaviorProfile`; 两适配器构造时注入同一实例, 变为无状态薄包装, 不再互引具体类型。内容改为动态分页 `IScrollContentSource.GetPage(i)` (纯函数, 确定性) + `PagedItemGenerator(totalCount,pageSize,fillRatio,namePrefix)` 配置驱动复用密集/稀疏/跳跃场景, 取代每场景静态 fixture。`ScrollBehaviorProfile` (sealed record, 无新 enum: Cumulative/PagesPerSwipe/ScrollJump/ProgressEpsilon + 工厂 Paged/PagedWithJump/WithCumulative) 控制滚动效果, ProgressEpsilon 从 ScrollHandlerConfig 迁入。

**Supersedes:**
- D-32 (累积模式 threshold 可见性) — PagedItemGenerator 按页生成 + profile Cumulative/Windowed 可见性
- D-33 (元素去重 lowest-threshold-wins) — 累积模式 seen-id 去重 (最低页优先)
- D-44 (滚动基线独立测试类) — 场景改为 PagedItemGenerator 配置, 共享 SimulatedScreen
- D-45 (ScrollDataStore fixture 策略) — 改为生成器配置, ScrollDataStore/Segment/SegmentBuilder 删除

Source: openspec change `scroll-action-refactor`
Ref: src/UniClaw.Core/Simulation/Scroll/SimulatedScreen.cs, IScrollContentSource.cs, PagedItemGenerator.cs, ScrollBehaviorProfile.cs
Guard: PagedContentAndScreenTests (12 tests)
Commit: pending
Status: Decided

### D-71 | 2026-07-14 | Scroll metrics → ActionHistory (interface, not concrete mock)

Context: 滚动指标 (ScrollCount/ScrollUpCount/ScrollDistance/FinalProgress) 旧从 `ScrollableMockActionExecutor.ScrollHistory` (具体类型) 取, collector 依赖 Simulation 具体类型, 真实服务无法接入。

Decision: 指标改从 `IActionExecutor.GetHistory()` (ActionHistory 接口) 的 swipe ActionRecord 按方向统计 (swipe Parameters 含 direction/before_progress/after_progress); FinalProgress 取自 `IVisionProvider.GetScrollProgress()` 接口。`BaselineReportCollector.Add` 签名改为 `IActionExecutor?/IVisionProvider?`, executor 或 vision 为 null 时全 0。删除 `ScrollableMockActionExecutor.ScrollDown/Up/History/GetScrollCount/GetScrollUpCount` (滚动走 SwipeAsync)。

**Supersedes:**
- D-43 (baseline reporting 架构) — collector 改用 ActionHistory 接口
- D-48 (从 ScrollHistory 取指标) — 改从 ActionHistory 取

Source: openspec change `scroll-action-refactor`
Ref: tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs (BuildActualNumeric)
Guard: baseline suite allPassed=true, scroll metrics 非零
Commit: pending
Status: Decided

### D-72 | 2026-07-14 | C-11 NumericAnchor schema change — remove jump fields

Context: D-68/D-69 删除跳跃检测/自适应管线后, `NumericAnchor` 的 `JumpDetected`/`JumpRecovered`/`AdaptiveStepIncreases` 三字段无数据源。这三字段属 D-46 加入的 C-11 锁定 ExpectedBehavior schema, 移除属宪法级变更, 须走 constitution change flow (非简单字段删除)。

Decision: **C-11 schema 变更** —— 移除 `NumericAnchor.JumpDetected/JumpRecovered/AdaptiveStepIncreases` (record + NumericAnchorDto + ExpectedBehavior 构造 + Verify); 保留 `ScrollCount/ScrollDistance/ScrollUpCount/FinalProgress`。基线 JSON 移除对应 jump_* 键; 标定流程文档同步 (见 simulation-baseline.md 更新)。NumericAnchor 仍为 informational (非 CI-blocking)。

**Supersedes:** D-46 (numericAnchor 7 滚动字段扩展) — 部分移除 (jump 类), 保留 scroll 类。

Source: openspec change `scroll-action-refactor` (C-11 constitution-level)
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/NumericAnchor.cs, ExpectedBehavior.cs
Guard: expected-behavior spec scenario "Removed jump fields are absent" (编译期强制)
Commit: pending
Status: Decided

### D-73 | 2026-07-14 | C-5 strengthened — engine layers zero Simulation reference

Context: D-68 前 engine 通过 `is ScrollableMockVisionService`/`is ScrollableMockActionExecutor` 运行时下转硬耦合 Simulation mock, 真实服务无法接入, 违反 C-5 依赖方向精神 (虽原 guard 只验证 Domain + Graph→StateMachine)。

Decision: 新增架构 guard `EngineLayers_DoNotReferenceSimulation` (ArchitectureGuardTests), 扫描 StateMachine/Traversal/Domain/Graph 生产 .cs, 断言无 `using UniClaw.Core.Simulation` 且无 `Simulation.*` 类型引用。强化 C-5 —— engine 层物理上无法引用 Simulation, mock 与真实服务代码路径强制相同。已 grep 确认零误报 (重构后 engine 层无任何 Simulation 引用)。

Source: openspec change `scroll-action-refactor` (phase22-guard-tests)
Ref: tests/UniClaw.Core.Tests/Architecture/ArchitectureGuardTests.cs (EngineLayers_DoNotReferenceSimulation)
Guard: CI-blocking (guard test)
Commit: pending
Status: Decided
