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
