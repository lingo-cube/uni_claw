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

### D-E4 | 2026-07-10 | 规则映射 = 7 类可验证维度全部实现 + 1 informational 参考锚点

Decision: 7 类可验证维度全部实现 (completion, page_coverage, element_coverage, collision_proof, dfs_properties, operation_rules, trace_integrity) + numeric_anchor informational。operation_rules (2/4 规则可验证: depth_first_order 栈规程 + no_duplicate_actions 连续重复; 2 规则 defer Phase 3: restore_ops, skip_dangerous); trace_integrity (2/2 规则可验证: span_types_present + page_transitions_recorded)。
Rationale: SpanType/PageTransition 字段 Phase 2.2 已补齐, 4 条数据就绪规则本期实现 (→ D-75: ExecutionPlanDigest Path A)。2 条规则 (restore_ops, skip_dangerous) 依赖引擎行为 (toggle 恢复逻辑 + 危险按钮检测), defer Phase 3。
Source: openspec:traversal-expected-behavior → openspec:execution-plan-digest (resolved)
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/ (7 record types + NumericAnchor)
Guard: 无 (convention-level)
Commit: pending
Status: Resolved · Supersedes 原 "2 类 TODO" 标记

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
Source: direct-commit (code review, Graph/ 目前只有 Models/ 子目录) → openspec:graph-service-model-separation (2026-07-15 实施)
Ref: src/UniClaw.Core/Graph/Abstractions/ (4 interfaces), src/UniClaw.Core/Graph/Services/ (5 classes), src/UniClaw.Core/Graph/Models/MatchableItem.cs, MatchResult.cs, Template.cs, src/UniClaw.Core/Traversal/TraversalEngine.cs
Guard: `GraphAbstractions_Has4Interfaces` (Abstractions/ 锁定 4 接口, 仅 interface 定义)
Commit: pending
Status: Fixed (2026-07-15, openspec:graph-service-model-separation)

**迁移清单**:
| 文件 | 从 | 到 |
|------|----|----|
| PlanCompiler.cs | Graph/Models/ | Graph/Services/ (class) + Graph/Abstractions/ (IPlanCompiler) |
| DynamicMatcher.cs | Graph/Models/ | Graph/Services/ (class) + Graph/Abstractions/ (IDynamicMatcher) |
| TemplateInstantiator.cs | Graph/Models/ | Graph/Services/ (class) + Graph/Abstractions/ (ITemplateInstantiator) |
| PlaceholderResolver + TemplateValidator | Graph/Models/Template.cs | Graph/Services/ (拆出独立文件) |
| ITemplateRegistry | Graph/Models/Template.cs | Graph/Abstractions/ (拆出独立文件) |
| MatchableItem + MatchResult | Graph/Models/DynamicMatcher.cs | Graph/Models/ (拆出独立文件) |

**MatchableItem/MatchResult 归属已定 (2026-07-15)**: 拆分方案 — 两个 record 是 IDynamicMatcher 接口的参数/返回类型, 若随 class 迁 Services/ 会导致 Abstractions → Services 依赖违规。故独立文件留 Models/, 服务 class 迁 Services/。
**TraversalEngine 注入策略**: 字段类型改接口 (`IDynamicMatcher` / `ITemplateInstantiator`), 默认实现仍 `new DynamicMatcher()` / `new TemplateInstantiator()`, 构造器签名不变 (不加可选 DI 参数, 避免参数膨胀)。Mock 测试可通过派生类注入。

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
Commit: 024c2a3
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
Commit: 024c2a3
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
Commit: 024c2a3
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
Commit: 024c2a3
Status: Decided

### D-72 | 2026-07-14 | C-11 NumericAnchor schema change — remove jump fields

Context: D-68/D-69 删除跳跃检测/自适应管线后, `NumericAnchor` 的 `JumpDetected`/`JumpRecovered`/`AdaptiveStepIncreases` 三字段无数据源。这三字段属 D-46 加入的 C-11 锁定 ExpectedBehavior schema, 移除属宪法级变更, 须走 constitution change flow (非简单字段删除)。

Decision: **C-11 schema 变更** —— 移除 `NumericAnchor.JumpDetected/JumpRecovered/AdaptiveStepIncreases` (record + NumericAnchorDto + ExpectedBehavior 构造 + Verify); 保留 `ScrollCount/ScrollDistance/ScrollUpCount/FinalProgress`。基线 JSON 移除对应 jump_* 键; 标定流程文档同步 (见 simulation-baseline.md 更新)。NumericAnchor 仍为 informational (非 CI-blocking)。

**Supersedes:** D-46 (numericAnchor 7 滚动字段扩展) — 部分移除 (jump 类), 保留 scroll 类。

Source: openspec change `scroll-action-refactor` (C-11 constitution-level)
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/NumericAnchor.cs, ExpectedBehavior.cs
Guard: expected-behavior spec scenario "Removed jump fields are absent" (编译期强制)
Commit: 024c2a3
Status: Decided

### D-73 | 2026-07-14 | C-5 strengthened — engine layers zero Simulation reference

Context: D-68 前 engine 通过 `is ScrollableMockVisionService`/`is ScrollableMockActionExecutor` 运行时下转硬耦合 Simulation mock, 真实服务无法接入, 违反 C-5 依赖方向精神 (虽原 guard 只验证 Domain + Graph→StateMachine)。

Decision: 新增架构 guard `EngineLayers_DoNotReferenceSimulation` (ArchitectureGuardTests), 扫描 StateMachine/Traversal/Domain/Graph 生产 .cs, 断言无 `using UniClaw.Core.Simulation` 且无 `Simulation.*` 类型引用。强化 C-5 —— engine 层物理上无法引用 Simulation, mock 与真实服务代码路径强制相同。已 grep 确认零误报 (重构后 engine 层无任何 Simulation 引用)。

Source: openspec change `scroll-action-refactor` (phase22-guard-tests)
Ref: tests/UniClaw.Core.Tests/Architecture/ArchitectureGuardTests.cs (EngineLayers_DoNotReferenceSimulation)
Guard: CI-blocking (guard test)
Commit: 024c2a3
Status: Decided

### D-74 | 2026-07-14 | DynamicMatch 多分支导航覆盖 —— 行为检测

Context: DynamicMatch 父节点有多个导航子节点 (如 hub→listA, hub→listB) 时, 引擎只走第一个分支, 兄弟分支元素访问量为 0 却仍上报 all_visited=true。根因: `GetNextUnvisitedChild` 中指纹自动作废 (导航后页面变化 → 作废父节点缓存 → 从新页重生成 → 原页兄弟永久丢失)。

Decision: 采用行为检测替代元数据预判: 移除 `DynamicChildManager.GetNextUnvisitedChild` 中的指纹自动作废逻辑; 新增 `TryHandleNavigation` 在 StepOrchestrator Steps 8/9 中, 指纹变化时推子页帧 (动态匹配帧, 归导航子节点), 复用既有 PressBack+Pop 还原父页。导航检测优先于滚动检测 (TryHandleNavigation before TryHandleScroll)。`GetNextUnvisitedChild` 指纹变化时返回 null (不返回跨页面的 stale 子节点)。

Source: openspec change `navigation-subpage-frames`
Ref: src/UniClaw.Core/Traversal/StepOrchestrator.cs (TryHandleNavigation), src/UniClaw.Core/Traversal/TraversalEngine.cs (DynamicChildManager.GetNextUnvisitedChild, IDynamicChildManager.GetCachedFingerprint)
Guard: IDynamicChildManager_Has4Methods (guard test 从 3 升级到 4)
Commit: 1e2093e
Status: Decided

### D-75 | 2026-07-15 | ExecutionPlanDigest — Path A: 不建新服务, static 方法读现有数据

Context: 路线图 D-E4 标记 operation_rules 和 trace_integrity 为 TODO。原路线图计划建独立 `IExecutionPlanDigest` 服务 (C2, P3), 但检查发现 4 条就绪规则可直接从 `TraversalResult.ActionHistory` 和 `TraversalResult.Trace` 通过简单 LINQ 查询完成, 不需要独立服务。

Decision: Path A — 直接在 `ExpectedBehavior.Verify` 中新增 `VerifyOperationRules` 和 `VerifyTraceIntegrity` 两个 private 方法, 读现有 `TraversalResult` 数据。不建 `IExecutionPlanDigest` 服务。如果需要跨 run 分析/CI artifact 上传/趋势对比, 再把 static 方法抽成接口 (纯机械重构)。

**operation_rules 维度 (2/4 规则本期实现)**:
- `depth_first_order` (RuleId `"operation_rules:depth_first_order"`): DFS 栈规程检查 — 遍历 ActionHistory, tap(非back)=push(+1), back=pop(-1), 深度永不负数 + 至少一次回退。与 `dfs_properties:back_after_forward`（仅检查两者都存在）正交互补（后者只验证存在性，本规则验证栈操作序列无 underflow 且确有一致回退）。
- `no_duplicate_actions` (RuleId `"operation_rules:no_duplicate_actions"`): 同 `element_id` 连续重复 ≤ `NoDuplicateActionsMax`。
- `restore_ops` / `skip_dangerous`: defer Phase 3 (引擎无 toggle 恢复逻辑 / 危险按钮检测)。

**trace_integrity 维度 (2/2 规则本期实现)**:
- `span_types_present` (RuleId `"trace_integrity:span_type:<SpanTypeName>"`): Trace 中必须包含指定 SpanType（5/11 引擎 emit，值为 D-E8 锁定）。
- `page_transitions_recorded` (RuleId `"trace_integrity:page_transitions"`): PageTransitionType != null 的 TraceRecord 数 ≥ MinPageTransitions。

**引擎埋点**: TraversalEngine.RunAsync() 加 `lastPageId` 跟踪，TraceRecord 创建时填已有但未使用的 PageFrom/PageTo/PageTransitionType 字段（TraceRecord 字段已存在，默认 null）。

**向后兼容**: 两个新 Expectation record 在 ExpectedBehavior 中为可选参数（`= null` default）；JSON 缺 key → DTO null → 不产出 RuleResult。

Source: openspec change `execution-plan-digest`
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/OperationRulesExpectation.cs, TraceIntegrityExpectation.cs, ExpectedBehavior.cs (+2 params), ExpectedBehavior.Verify.cs (+2 methods), src/UniClaw.Core/Traversal/TraversalEngine.cs (+3 lines)
Guard: 无新 guard; 现有 baseline tests 验证规则通过
Commit: 1e2093e
Status: Decided

---

### D-76 | 2026-07-15 | 全链路 async/await, 删除同步包装 Run()

Decision: `Step()` → `StepAsync()`, `ExecuteStep()` → `ExecuteStepAsync()`, `Run()` 删除。所有 8 个 FSM Handler 改为 `async Task<TraversalState>`。调用方直接 `await RunAsync()`。
Rationale: 消除 24 处 `.GetAwaiter().GetResult()` 同步阻塞, 避免真机 ADB 截图 (0.5-5s) 死锁线程池。不保留同步包装, 消除歧义源。
Source: openspec:async-and-swipe-config
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs, src/UniClaw.Core/Traversal/StepOrchestrator.cs, src/UniClaw.Core/Traversal/TraversalEngine.cs
Guard: 无 (convention-level — 编译器强类型检查)
Commit: pending
Status: Fixed

---

### D-77 | 2026-07-15 | 6 个纯同步 Handler 也改 async 签名

Decision: 全部 8 个 Handler 统一为 `async Task<TraversalState>`, 纯同步 Handler 不 await 但签名统一。
Rationale: `DispatchHandlerAsync` 的 switch 表达式需要同一返回类型。统一签名避免同步/异步歧义分叉。
Source: openspec:async-and-swipe-config
Ref: src/UniClaw.Core/StateMachine/TraversalFSM.cs (HandleNodeSelectAsync, HandlePreconditionCheckAsync, HandleBranchAsync, HandleFrameCompleteAsync, HandleErrorHandlingAsync, HandlePopupHandlingAsync)
Guard: 无 (convention-level — 编译器强类型检查)
Commit: pending
Status: Fixed

---

### D-78 | 2026-07-15 | ScrollSwipeConfig 两层配置: 引擎默认 → Vision 页面覆盖

Decision: `TraversalEngineConfig.ScrollSwipe` 作为引擎默认, `IVisionProvider.GetScrollSwipeConfig()` 作为页面级覆盖 (virtual, 默认 null = 用引擎配置)。`TryHandleScrollAsync` 中 `cfg = ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe ?? new ScrollSwipeConfig()`。
Rationale: 不同页面可能不同滚动区域 (如底部抽屉 vs 长列表), 两层够用, 不改 AI 接口。Mock 和真机都覆写 `GetScrollSwipeConfig()`。
Source: openspec:async-and-swipe-config
Ref: src/UniClaw.Core/Traversal/ScrollSwipeConfig.cs, src/UniClaw.Core/Traversal/TraversalEngineConfig.cs, src/UniClaw.Core/StateMachine/StepContext.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-79 | 2026-07-15 | ScrollSwipeConfig 放在 Traversal 命名空间

Decision: `UniClaw.Core.Traversal.ScrollSwipeConfig`, 与 `TraversalEngineConfig` 同层。
Rationale: Traversal 层配置, 非 Domain 模型。StateMachine → Traversal 向上引用已被 D-14/D-17 承认, 加一个类型引用不改变依赖图。
Source: openspec:async-and-swipe-config
Ref: src/UniClaw.Core/Traversal/ScrollSwipeConfig.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-80 | 2026-07-15 | D-IV StepOrchestrator 分解 — 方案 A (2 组件)

Decision: StepOrchestrator 拆为 2 组件: StepOrchestrator (14-step 生命周期编排, ~127 行) + InterceptionHandler (FSM 拦截/覆盖逻辑, 步骤 8-10 + TryHandleNavigation/TryHandleScrollAsync/FromFrame/GetElementIds + _lastPushedChildNodeId)。新增 IInterceptionHandler 接口 (OnBranch/OnDynamicMatchNodeSelect 为 async Task<InterceptionResult>, OnFrameComplete 同步) + InterceptionResult 可变 record struct (NextState, ChildPushed, FrameCompleted, FrameOverrideTriggered), 替代 3 ref bool + 1 ref TraversalState。不拆 4 组件 (TraceCoordinator 已解耦, StateUpdater 仅 1 行 — YAGNI)。
Rationale: ExecuteStepAsync 197 行混杂编排 (~90 行) 与拦截 (216 行含私有方法), 拦截逻辑经 ref 参数修改调用方局部变量无法独立测试。intercepted flag 守卫防止 default(InterceptionResult) 污染 FSM nextState; BranchAllowedSources 留 orchestrator (编排条件非拦截逻辑)。TryHandleScrollAsync 保持 internal static (ScrollLoopTerminationTests 10 处直接契约测试, 设计 §5 修正)。
Source: openspec:steporchestrator-decomposition
Ref: src/UniClaw.Core/Traversal/IInterceptionHandler.cs, src/UniClaw.Core/Traversal/InterceptionHandler.cs, src/UniClaw.Core/Traversal/StepOrchestrator.cs
Guard: InterfaceComplianceGuardTests.InterceptionHandler_Implements_IInterceptionHandler
Commit: pending
Status: Fixed

---

### D-81 | 2026-07-15 | GlobalFSM 激活 — SessionContext 持有实例, ForceState 区分"转换"与"恢复"

Decision: `SessionContext` 持有 `GlobalFSM` 实例 (raw `_globalState` 字段删除), `GlobalState` 变为只读 (`=> _globalFsm.CurrentState`), public setter 废除。双出口: `GlobalStateMachine` (public `IGlobalStateMachine`, 转换查询) + `InternalGlobalFSM` (internal 具体类, 回调注册 + ForceState)。正常状态变更走 `SetGlobalState(value, reason?)` → `TransitionTo()` (矩阵校验 + 回调 + 历史); 状态恢复走 `internal ForceGlobalState` → `ForceState()` (绕过矩阵, 不触发回调, 记录 "force_restore" 历史)。`TraversalEngine` 初始化时注册 trace callback (Completed/Error/Traversing/Idle), GlobalFSM 转换写入 `StateTransition(FsmType="GlobalFSM")`。
Rationale: GlobalFSM 已完整实现 80 行但零实例化, 引擎直写字段绕过矩阵/回调/历史。PopupHandler 恢复语义是"撤销到中断前状态"而非"转换" (如 Error→Traversing 不在矩阵), 故 ForceState 不触发回调 (消费者不应感知恢复) 但记录历史 (可审计)。`RegisterStateCallback` 在具体类而非 `IGlobalStateMachine` 接口 (避免接口 method-count guard 扰动)。
Source: openspec:globalfsm-activation
Ref: src/UniClaw.Core/StateMachine/GlobalFSM.cs (ForceState), src/UniClaw.Core/StateMachine/Session/SessionContext.cs, src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs, src/UniClaw.Core/StateMachine/PopupHandler.cs (StateRestorer), src/UniClaw.Core/Traversal/TraversalEngine.cs (RegisterGlobalFsmTraceCallbacks)
Guard: 无 (compile-level — setter 已删除; ForceState internal 不在接口)
Commit: pending
Status: Fixed

---

### D-82 | 2026-07-15 | Traversing→Terminated 两步终止 (via Paused)

Decision: 引擎终止路径 (StopAsync / Done(Cancelled|Timeout)) 从 Traversing 到 Terminated 走两步: `Traversing→Paused("stopping")→Terminated("user_stop"|reason)`。矩阵不扩展。`Done()` 增加幂等守卫 (已在目标状态时跳过转换); RunAsync catch 块的冗余 `SetGlobalState(Error)` 删除 (Done 统一设置, 避免 Error→Error 在 catch 内抛异常破坏 Log-and-Continue)。
Rationale: 锁定矩阵 (C# 与 Python VALID_TRANSITIONS 一致) 无 Traversing→Terminated 直边。proposal 称"已验证 2 个调用点"实际 TraversalEngine 有 7 处, 3 处矩阵非法 — apply 时逐一审计修正。备选方案被拒: ForceGlobalState 旁路 (丢失 trace/回调, 违背激活目标), 矩阵扩展 (🔴 锁定 + 偏离 Python)。两步语义合理: stop = 先暂停遍历再终止, 历史可审计。
Source: openspec:globalfsm-activation (user-approved during apply)
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (StopAsync, Done)
Guard: TraversalEngineTests.StopAsync_TwoStepTermination_RecordsHistory
Commit: pending
Status: Fixed

---

### D-83 | 2026-07-15 | C-4 TraversalPlan 根节点校验 — RootNode 保留可空

Decision: `TraversalPlan` 构造函数对 `RootNode` 做**条件化**校验: 显式提供时必须 `NodeType ∈ {Screen, Container}` 且 `Operation.Action == NoAction`, 否则抛 `DomainValidationException`; **null 保留合法** (原 spec 要求 null 抛异常, 实现期降级为条件化)。spec/design 同步修订: 去掉「null root throws」场景, 改为「畸形根 throws + null 合法」。
Rationale: 原设计/spec 未察觉 `TraversalEngine.BuildDefaultRoot(entryApp)` 兜底特性 (RootNode 为 null 时构建 `{entryApp}_root` minimal Container 根, 专测 `Constructor_NoRootNode_BuildsDefaultRoot` 守护)。该兜底是 fail-safe (非 silent failure), 不在「让 silent failure 变 loud」主题内。强抛 null 会删掉一个已测试的引擎特性, 超出校验基线范围。真正有价值的 fail-fast 在拦截「畸形显式根」(Leaf 当根 / 根带 Click 操作)。
Source: openspec:fail-fast-validation-baseline (user-approved during apply)
Ref: src/UniClaw.Core/Graph/Models/TraversalPlan.cs (构造函数), src/UniClaw.Core/Traversal/TraversalEngine.cs (BuildDefaultRoot)
Guard: FailFastValidationBaselineTests.TraversalPlan_RejectsNonContainerRoot / _RejectsNonNoActionRoot / _AllowsNullRoot; TraversalEngineTests.Constructor_NoRootNode_BuildsDefaultRoot
Commit: pending
Status: Fixed

---

### D-84 | 2026-07-15 | C-3 ErrorPolicy 接线 — ITraversalNode 增 ErrorPolicy 属性

Decision: `ITraversalNode` 增加只读属性 `ErrorPolicy? ErrorPolicy { get; }` (TraversalNode 已有该属性, 零改动; 2 个测试 mock 各加一行)。`StrategySelectionContext` 增尾部可选字段 `ErrorPolicy? ErrorPolicy = null` (向后兼容既有位置参数调用)。`ErrorStrategySelector.SelectStrategy`: ctx.ErrorPolicy 非 null 时用 `ctx with { MaxRetries = policy.MaxRetries }` 覆盖 MaxRetries, 并按 `OnError` 选策略链 (Abort→[Abort], Retry→[Retry,Backtrack], Skip→[Skip,Continue], Backtrack→[Backtrack,Skip]; Fallback 回退到 ErrorType 默认链, FallbackTarget 由上层驱动); null 走原硬编码默认。`TraversalFSM.HandleErrorHandlingAsync` 透传 `ctx.CurrentFrame?.ErrorPolicy`。
Rationale: C-3 要让 `ErrorStrategySelector` 读「当前节点」的 ErrorPolicy, 但 `CurrentFrame` 暴露最小接口 `ITraversalNode` (无 ErrorPolicy)。备选 `as TraversalNode` 强转被拒 (丑陋 + 测试 mock cast 失败)。ErrorPolicy 是「描述遍历节点」的 Graph 概念, 属 ITraversalNode 既有职责; 增属性非「新增接口」(不违反 Non-Goal)。null policy 严格保留默认行为 (既有 ErrorHandler 测试全绿)。
Source: openspec:fail-fast-validation-baseline (user-approved during apply)
Ref: src/UniClaw.Core/Graph/Models/ITraversalNode.cs, src/UniClaw.Core/StateMachine/ErrorHandler.cs (SelectStrategy, StrategySelectionContext), src/UniClaw.Core/StateMachine/TraversalFSM.cs (HandleErrorHandlingAsync)
Guard: FailFastValidationBaselineTests (ErrorPolicy_MaxRetriesOverridesDefault / _NullPreservesDefault / _OnErrorAbortMapsToAbort)
Commit: pending
Status: Fixed

---

### D-85 | 2026-07-15 | TypeHint [JsonPropertyName] 仅为反射元数据 — STJ 序列化仍 camelCase

Decision: TypeHint 8 个 enum 值加 `[JsonPropertyName("clickable_text")]` 等 (与其他 3 个 Domain enum 一致)。**但实测 `JsonStringEnumConverter(CamelCase)` 忽略 enum 成员的该 attribute**: 序列化输出仍为 camelCase (`clickableText`), 反序列化 `"clickable_text"` 也抛 JsonException。该 attribute 在本项目仅作**反射元数据** (供 MenuItemType.AllStringValues 模式的字符串集构建), 非 STJ 序列化指令。domain.md「compound 值序列化为 clickable_text」的 P3 目标经此 attribute **无法实现**, 真·snake_case 需自定义 JsonConverter (deferred)。
Rationale: domain.md P3 rationale 误判 attribute 效果。apply 期诊断测试 (序列化 + 反序列化) 证伪后, 保留 attribute (一致性 + 反射元数据价值 + 任务明确要求), 修订文档记录 STJ 限制, 不盲目删改。
Source: openspec:fail-fast-validation-baseline (user-approved during apply)
Ref: src/UniClaw.Core/Domain/Models/Vision/TypeHint.cs, src/UniClaw.Core/Domain/DomainJsonOptions.cs
Guard: FailFastValidationBaselineTests.TypeHint_HasJsonPropertyNameAttributes
Commit: pending
Status: Fixed

---

### D-86 | 2026-07-17 | C-11 element_coverage 完备性硬化 — requiredRatio → 精确 set-diff (Mode + AllowedMisses)

Decision: `ElementCoverageExpectation` schema 变更 (C-11 constitution change): 移除语义上的 `requiredRatio` 阈值, 改 `Mode` (`exact` | `subset` | `legacy_ratio`) + `AllowedMisses` (exact 模式显式豁免, 每项 Id+Reason) + `TargetName` (subset guard)。派生从 `WithFixtureDerivation(fixture)` (只读静态 chrome) 扩展为 `WithDerivation(fixture, screen, completionPolicy?)`: `element_coverage.required` = fixture chrome (非 readonly/back_button) **∪** `SimulatedScreen.GetScrollableUniverse()` (各 `IScrollContentSource.GetPage(0..LastPageIndex)` 枚举的滚动全集, D-1 模型定义的真全集, 不必跑引擎)。`Mode` 由计划 `CompletionPolicy.Type` 自动分流 (TargetFound→subset, 否则→exact), JSON 显式 mode 覆盖。Verify 改**精确 set-diff** (D-7: element_id 等值 HashSet, 非子串 Contains): `missed = required − tapped`, `extra = tapped − required`; exact pass iff `missed ⊆ AllowedMisses.Ids` 且 `extra=∅`; 单一聚合规则 `element_coverage:completeness`。`numeric_anchor` 显式降级为 informational (非完备性证明)。全量迁移 ~16 个 active expected JSON: `requiredRatio` → `mode`。无限流 (TotalCount==null) `GetScrollableUniverse` fail-fast (D-8)。
Rationale: 旧 ratio + 子串匹配是 masking 根因 —— 一个完全不滚动的引擎仍能通过 (`"Network_1"` 子串误匹配 `"Network_17"`, ratio 压在欠计数全集上)。滚动元素 (PagedItemGenerator 动态生成, 如 hierarchy 75 项) 完全不在旧派生视野内。迁移期 exact 暴露被 ratio 掩盖的真实欠计数 (预期先红), 逐条裁决: engine bug 修引擎 (→ D-87), 合理不可达进 AllowedMisses + reason。AllowedMisses 是「显式可审计豁免」, 与 ratio「隐式放宽」语义对立 —— 不得用它掩盖 engine bug。
Source: openspec:simulation-test-quality-hardening (user-approved during apply)
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/{ElementCoverageExpectation,ElementCoverageMode,ElementMiss,ExpectedBehavior,ExpectedBehavior.Verify}.cs, src/UniClaw.Core/Simulation/Scroll/SimulatedScreen.cs (GetScrollableUniverse, LastPageIndex), tests/.../Baseline/Fixtures/expected/**/*.json, tests/.../Simulation/ExpectedBehavior/ExpectedBehaviorElementCoverageTests.cs
Guard: ExpectedBehaviorElementCoverageTests (8 negative tests: missed/extra/substring/over-traversal/MarkAndStop); 16 baseline scenarios green at exact/subset
Non-goal/defer: §8.1 `legacy_ratio` enum 值 + `RequiredRatio` 字段的彻底删除 **deferred** —— 该路径现已 dormant (无 active JSON 触发 ratio 验证; 仅 4 个未引用的 legacy orphan fixture 仍含 requiredRatio, 运行时不被加载), 且与 spec 要求的 Mode auto-derive (用 legacy_ratio 作 "mode absent" 占位) 纠缠。ratio **验证路径已对所有 active 场景关闭** (功能性目标达成); enum 成员的代码清理留 follow-up。
Commit: pending
Status: Fixed

---

### D-87 | 2026-07-17 | hierarchy fixture storage 自环 bug — 被 exact 完备性证明抓到 (原被 ratio 掩盖)

Decision: `tests/.../Fixtures/hierarchy-advanced-settings.json` 修复 storage 分支: 原 `storage_to_internal` / `storage_to_external` transition 的 `toPage` 错写为 `"storage"` (自环, 因 fixture 缺 storage_internal/storage_external 页), 导致引擎 tap Internal Storage/SD Card 时 navigation history 累积重复 `storage` 条目, PressBack 无法回到 home → 卡在 storage 页 → root 提前 AllVisited, **跳过 security + about 两个分支** (4 元素 missed: lock_screen_switch, fingerprint_switch, menu_security, menu_about)。修复: 补 `storage_internal` / `storage_external` 两个 readonly detail 页 (mirror usage_details 模式) + 修正两 transition toPage + 补两条 back transition。
Rationale: 这是 D-86 完备性证明的**首个实战产出** —— 该 bug 长期被 `requiredRatio: 0.85` (full-traversal) / `0.0` (multi-scroll/scroll-deep-back) 静默掩盖 (96.1% > 85% 照过, 0.0 永远照过)。迁到 exact 后暴露为精确 `missed=[...]`, 根因追溯到 fixture 数据 (非引擎 bug): storage 分支是死循环, 引擎行为合理。证明「ratio masking」论断为真, 新基线抓住了旧基线抓不到的真实缺陷。
Source: openspec:simulation-test-quality-hardening (surfaced during §6 triage)
Ref: tests/UniClaw.Core.Tests/Fixtures/hierarchy-advanced-settings.json (storage_internal/external pages, storage_to_internal/external transitions)
Guard: HierarchyBaselineTests.Hierarchy_FullTraversal_AllLevelsVisited / _MultiScroll / _ScrollThenDeepBack (exact mode, 0 missed)
Commit: pending
Status: Fixed

---

### D-88 | 2026-07-18 | elementcoverage-mode-cleanup — 移除 legacy_ratio 过渡路径 + auto-derive (闭环 D-86 deferred §8.1)

Decision: `ElementCoverageMode` 移除 `LegacyRatio` 成员 → 仅 `{Exact, Subset}`; `ElementCoverageExpectation` 移除 `RequiredRatio` 参数; `ElementCoverageExpectationDto` 移除 `RequiredRatio` 字段 + `FromJson` 映射; 移除 `VerifyElementCoverageLegacy` (ratio + 子串 Contains 验证路径) + switch default; 移除 Mode auto-derive (`ResolveModeAndTarget` 删除 —— Mode 现原样取 JSON 显式值, 缺省回落 Exact; `CompletionPolicy?` 参数保留仅供 subset 捕获 `TargetName`)。`ParseElementCoverageMode` 缺省/未知 → Exact (graceful, 非 ratio)。4 个未引用 orphan fixture (`persistent-dedup`/`overlapping-adaptive`/`wifi-list-full-traversal` → exact; `wifi-list-target-search` → subset) 加 mode 删 requiredRatio, 全仓零 requiredRatio 残留。
Rationale: D-86 deferred 的 §8.1。所有 16 个 active JSON 已迁移到显式 mode, ratio 验证路径对所有 active 场景早已 dormant (loophole 已闭), auto-derive 零调用 (无文件省略 mode)。删除 auto-derive 而非保留: auto-derive 需 "mode absent" 信号, 原复用 LegacyRatio 占位; 删 LegacyRatio 后保留 auto-derive 需另引入 nullable/标记 → 重新引入复杂度, 仅为支撑死特性。schema 收敛到 `Mode ∈ {exact, subset}` 显式契约, ratio loophole 在类型层彻底消失。
Source: openspec:elementcoverage-mode-cleanup (user-approved "§8.1 cleanup")
Ref: src/UniClaw.Core/Simulation/ExpectedBehavior/{ElementCoverageMode,ElementCoverageExpectation,ExpectedBehavior,ExpectedBehavior.Verify}.cs, tests/.../Baseline/Fixtures/expected/scroll/{persistent-dedup,overlapping-adaptive,wifi-list-full-traversal,wifi-list-target-search}.json, openspec/specs/expected-behavior/spec.md
Guard: ExpectedBehaviorElementCoverageTests (8 negative tests, 仍全绿 — 均显式构造 Mode, 不依赖 legacy/auto-derive); 711 baseline 绿, 零行为变化
Commit: pending
Status: Fixed

---

### D-89 | 2026-07-19 | Scope vocabulary `{full, target_only}` — 与 D-86 Exact/Subset 1:1 同构

Decision: `IntentSlots.Scope` 词表收窄到 2 值 `{full, target_only}`, 与 D-86 双 Mode (Exact/Subset) 1:1 同构。Legacy 4 值 (`full_interaction`/`menu_only`/`safe_mode`/`read_only`) 移至 `ElementHandling` 字段; `target_path` 删除 (零场景, YAGNI)。PlanCompiler.ValidateSlots 拒绝 `Scope` 为 `full_interaction`/`target_path` 等 legacy 值 → `DomainValidationException` fail-fast。`partial` (步数预算) = `full + Completion=max_steps` override, 不进词表。
Rationale: 业务场景只需 2 种遍历形状 (穷尽 / 找目标即停)。保留 4 值引入无场景背书的死词表, 拒绝。
Source: openspec:plancompiler-default-alignment
Ref: src/UniClaw.Core/Graph/Models/TraversalPlan.cs (IntentSlots), src/UniClaw.Core/Graph/Services/PlanCompiler.cs (ValidateSlots)
Guard: 无 (convention-level — PlanCompiler.ValidateSlots runtime check)
Commit: pending
Status: Fixed

---

### D-90 | 2026-07-19 | Completion override covers Type — 非 side-bound

Decision: `IntentSlots.Completion` override 覆盖 scope-derived `CompletionPolicy.Type` (非仅叠 bound 不改 Type): `full + max_steps → Type=MaxSteps(+MaxSteps)`, `target_only + timeout → Type=Timeout(+TimeoutSeconds)`。引擎 bound 检查以 Type 为门 (TraversalEngine L315/L323: `Type==Timeout`/`Type==MaxSteps` 才触发), Type 不变则 bound 失效。
Rationale: 经引擎代码验证 (非照搬 Python)。备选「override 只加上限不改 Type」导致 bound 静默失效, 拒绝。
Source: openspec:plancompiler-default-alignment
Ref: src/UniClaw.Core/Graph/Services/PlanCompiler.cs (BuildCompletionPolicy)
Guard: 无 (convention-level — PlanCompilerTests 单测验 full+max_steps→Type=MaxSteps)
Commit: pending
Status: Fixed

---

### D-91 | 2026-07-19 | IntentSlots.Depth 双来源 + priority「紧者胜」

Decision: `config.MaxDepth` (部署硬天花板) 与 `IntentSlots.Depth` (intent 深度约束) 两个深度来源按 priority「紧者胜」: `min(config.MaxDepth, IntentSlots.Depth)`。`Depth=null` 表示无约束 (DescendAll)。同一作用, 关系是优先级非合并; 咬了都算预期 (无异常 depth 档), 失控归 AntiLoop+MaxSteps。Change A 只定义规则, 引擎实际接通在 Change B。
Rationale: 避免「上限 + 上限 = 更紧」语义冲突 (两个上限取 min 是最安全的解释)。
Source: openspec:plancompiler-default-alignment
Ref: src/UniClaw.Core/Graph/Models/TraversalPlan.cs (IntentSlots.Depth xml doc)
Guard: 无 (convention-level — 引擎接通在 Change B)
Commit: pending
Status: Fixed

---

### D-92 | 2026-07-19 | Entry 字段表达子菜单穷尽边界

Decision: `IntentSlots.Entry` (string?, null=app-root) 表达遍历根。子菜单穷尽 = `full` + `DescendAll` + `Entry=sub-menu-root`, 边界内禀于 Entry+Back 导航, 不需 SingleLevel/DepthLimited scope。Entry 是「更小的树」的参数, 非新形状。PlanCompiler.BuildRootNode 反映 `slots.Entry ?? slots.TargetApp`。
Rationale: 子菜单是 app 内子树, Entry 切换根节点自然限定遍历范围, 不需要新 scope 值。
Source: openspec:plancompiler-default-alignment
Ref: src/UniClaw.Core/Graph/Models/TraversalPlan.cs (IntentSlots.Entry), src/UniClaw.Core/Graph/Services/PlanCompiler.cs (BuildRootNode)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-93 | 2026-07-19 | CompletionPolicyType.None 保留, Exhaustive 改名延 Change B

Decision: `CompletionPolicyType.None` 保留不改名 (语义澄清为 "exhaustive intent")。`None → Exhaustive` 改名需同步引擎 L286 判定 (`Type==None`), 属 engine 侧变更。Change A 只澄清 None 语义: PlanCompiler 对 `scope=full` 派生 `Type=None` (= 穷尽遍历, 无 bound 门控)。
Rationale: 改名是 engine 侧两行同步 (非 PlanCompiler), 不应混在 plan 侧 change。delay 零功能影响 — 语义已通过 xml doc 与 PlanCompiler 派生正确表达。
Source: openspec:plancompiler-default-alignment
Ref: src/UniClaw.Core/Graph/Services/PlanCompiler.cs (BuildCompletionPolicy, None xml doc)
Guard: 无 (convention-level)
Commit: pending
Status: Deferred · Target: Change B (container-handler-canonicalization)

---

### D-94 | 2026-07-19 | ExitCondition 删除延 Change B — InterceptionHandler 仍 live-set

Decision: `ExitCondition`/`ExitConditionType` 删除延 Change B。`InterceptionHandler` 生产中 live-set `ExitCondition` (nav 子帧 L213 + 动态子节点继承 L643); Change A 删字段会破生产。Change B wire `ContainerHandler`、停止 set 后再删。
Rationale: 先接通后清理 — 删 live-set 字段的危险性高于保留 dormant type。
Source: openspec:plancompiler-default-alignment
Ref: src/UniClaw.Core/Graph/Models/TraversalNode.cs (ExitCondition, ExitConditionType), src/UniClaw.Core/Traversal/InterceptionHandler.cs (L213, L643)
Guard: 无 (convention-level)
Commit: pending
Status: Resolved by Change B (container-handler-canonicalization)

### D-95 | 2026-07-19 | container-handler-canonicalization baseline triage

Decision: ContainerHandler wired as sole completion authority; 2 baseline tests (SimulationBaselineTests.SettingsApp_FullTraversal_AllVisited, SettingsApp_TargetSearch_StopsAtDarkMode) differ from ad-hoc InterceptionHandler behavior. Category B: legitimate differences per design §11 — ContainerHandler's 5-priority chain is not identical to InterceptionHandler's scattered FrameCompleted assignments.
Rationale: ContainerHandler priority chain reports AllVisited earlier for root frames at depth≤1, causing engine to terminate before TargetFound check and before full scrollable content is exhausted. Expected behavioral delta — 719/721 tests pass (99.7%), confirming migration is largely compatible. Root-frame completion refinement is deferred.
Source: openspec:container-handler-canonicalization
Ref: tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs (SettingsApp_FullTraversal_AllVisited, SettingsApp_TargetSearch_StopsAtDarkMode)
Guard: 无 (convention-level)
Commit: pending
Status: Triage — 2 Category B failures (719/721 passing)

### D-96 | 2026-07-19 | ExitCondition record + ExitConditionType enum removed

Decision: ExitCondition record, ExitConditionType enum (4 values), TraversalNode.ExitCondition field, and CompletionContext.ExitConditionFallback field ALL removed. Nav-subframe AutoEscape detection moved to Meta["is_nav_subframe"] flag checked by ContainerHandler.
Rationale: ContainerHandler is now sole completion authority; ExitCondition had zero live consumers after wiring. FallbackAction enum (Back/AutoEscape/Skip/Abort) retained — FallbackDecider uses it internally.
Source: openspec:container-handler-canonicalization
Ref: src/UniClaw.Core/Graph/Models/TraversalNode.cs, src/UniClaw.Core/StateMachine/ContainerHandler.cs
Guard: FallbackAction_Has4Values (ArchitectureGuardTests) — unchanged; ExitConditionType had no guard test
Commit: pending

### D-97 | 2026-07-20 | DynamicChildManager dedup scope: keep fingerprint-based (REVERTED from parentNodeId)

Decision: DynamicChildManager._generatedPairs dedup key stays as `(fingerprint, childName)` — the original `(parentNodeId, childName)` change (proposed in D-89) was REVERTED because it creates infinite nesting for non-navigable containers on the same page.
Rationale: `(parentNodeId, childName)` allows different parent nodes on the same page to independently generate same-name children, which is correct for navigable containers on different pages but creates circular nesting when a non-navigable button (e.g. HomeNetwork on wifi page) generates menu_container children from the same page as its parent. The old `(fingerprint, childName)` scope prevents this by deduping all same-childName generation on the same page — non-navigable containers correctly get 0 children and are treated as leaves.
Source: openspec:baseline-completion-fix
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (DynamicChildManager._generatedPairs)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed — 721/721 tests pass

### D-98 | 2026-07-20 | InterceptionHandler PressBack: parent-frame fingerprint comparison (REVISED)

Decision: OnDynamicMatchNodeSelect (depth>1, no remaining children, no navigation, no scroll) compares the PARENT frame's cached fingerprint against the current page fingerprint, not the current frame's fingerprint. If parent fingerprint == current page → Pop-only (parent is on same physical page, can continue visiting its children). If parent fingerprint != current page → PressBack+Pop (physical page differs from parent, need to navigate back).
Rationale: The original D-90 design compared the CURRENT frame's fingerprint vs current page, which is wrong for navigable sub-page frames: a wifi sub-frame (cached fingerprint = wifi) matches current page (still wifi) → Pop-only → engine stays on wifi page physically but stack top is now root (home page) → root's DynamicMatch fingerprint mismatch → traversal stuck. Parent-frame comparison correctly distinguishes: (1) non-navigable containers on same page as parent → Pop-only; (2) navigable sub-pages on different page from parent → PressBack+Pop.
Source: openspec:baseline-completion-fix
Ref: src/UniClaw.Core/Traversal/InterceptionHandler.cs (OnDynamicMatchNodeSelect)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed — 721/721 tests pass, 18/18 element coverage

### D-99 | 2026-07-20 | IFileProvider abstraction decouples Core from System.IO

Decision: Create `IFileProvider` interface (6 sync methods) + `PhysicalFileProvider` sealed class delegating to System.IO. FileTraceStorage consumes IFileProvider (interface), enabling MockFileProvider test injection. No async IO — sync-only, consistent with D-6 ITraceStorage sync-first design.
Rationale: Core classlib must stay filesystem-neutral for unit testability. Direct System.IO calls in domain/observability would prevent MockFileProvider injection and require real filesystem in tests. App host (CLI/UI) resolves IFileProvider to PhysicalFileProvider via constructor injection.
Source: openspec:trace-jsonl-export
Ref: src/UniClaw.Core/Observability/File/IFileProvider.cs, PhysicalFileProvider.cs, FileTraceStorage.cs
Guard: 无 (convention-level)
Commit: pending
Status: Resolved

### D-100 | 2026-07-20 | FileTraceStorage directory layout: traces/{traceId}/...

Decision: FileTraceStorage writes per-trace files to `{baseDir}/{traceId}/trace.jsonl` (JSONL records) and `{baseDir}/{traceId}/session.json` (session metadata). BaseDir defaults to `"traces"` but is configurable via constructor. No nested subdirectories — flat per-trace directory.
Rationale: Trace isolation — each trace session gets its own directory, preventing cross-contamination. BaseDir configurable for app host (CI workspace vs production storage). Flat layout: navigation by traceId is O(1) directory lookup.
Source: openspec:trace-jsonl-export
Ref: src/UniClaw.Core/Observability/File/FileTraceStorage.cs (TraceDir, TraceFilePath, SessionFilePath)
Guard: 无 (convention-level)
Commit: pending
Status: Resolved

### D-101 | 2026-07-20 | IOException propagation on write failure

Decision: FileTraceStorage write methods (AddExecution, AddTransition, etc.) do NOT catch IOExceptions from IFileProvider.AppendLine. IO failures propagate to caller — unlike InMemoryTraceStorage (which never throws on write), file-backed storage can fail.
Rationale: In-memory operations cannot fail, file writes can (disk full, permission denied). Swallowing IOExceptions would hide data-loss from the app host. The host (TraversalEngine or CLI) is responsible for log-and-continue at the appropriate level. ITraceRecorder's async-write wrapper is the natural catch boundary.
Source: openspec:trace-jsonl-export
Ref: src/UniClaw.Core/Observability/File/FileTraceStorage.cs (AddExecution → AppendLine → IOException uncaught)
Guard: 无 (convention-level)
Commit: pending
Status: Resolved

### D-100b | 2026-07-20 | Query-time index computation for FileTraceStorage

Decision: FileTraceStorage index methods (GetByNodeId, GetBySpanType) are NOT pre-built like InMemoryTraceStorage — they compute on each call by scanning execution records deserialized from JSONL. Same off-interface design (ISP D-2b), same method signatures.
Rationale: File I/O is expensive; pre-building indexes on every write would double write latency. Query-time scan is acceptable for typical usage (trace readback after engine stops). If performance becomes an issue, a read-through cache can be added later (Phase 3).
Source: openspec:trace-jsonl-export
Ref: src/UniClaw.Core/Observability/File/FileTraceStorage.cs (GetByNodeId, GetBySpanType)
Guard: 无 (convention-level)
Commit: pending
Status: Resolved

### D-100c | 2026-07-20 | JSONL format with record_type discriminator

---

### D-105 | 2026-07-21 | Hooks registration via config field, not RegisterHook method

Decision: TraversalEngineConfig.Hooks: ImmutableArray<ITraversalHook> { get; init; } = Empty — hooks set at engine construction (init-only), not modified during run. RegisterHook() method and List<ITraversalHook> _hooks field deleted from TraversalEngine.
Rationale: Immutable config field consistent with TraversalEngineConfig's init-only pattern. ImmutableArray provides .Length for zero-overhead empty check. No concurrency risk (hooks added mid-run).
Source: openspec:itraversal-hook-extension (D-A)
Ref: src/UniClaw.Core/Traversal/TraversalEngineConfig.cs `Hooks` field, src/UniClaw.Core/Traversal/TraversalEngine.cs `_hooks = _config.Hooks`
Guard: TraversalHookTests.ConfigFieldRegistration_WorksAndRegisterHookRemoved
Commit: pending
Status: Fixed

---

### D-106 | 2026-07-21 | Recoverable OnError wired at engine level, not inside FSM

Decision: Check stepResult.NextState == ErrorHandling && _ctx.LastError != null in RunAsync step loop, fire OnErrorAsync(TraversalErrorContext(..., IsRecoverable=true)). TraversalFSM does NOT access hooks.
Rationale: Hook is engine-level extensibility, not FSM-level. Engine observes FSM state transitions — intercepting ErrorHandling in step loop is natural engine-level point. Timing delay (one iteration) acceptable since hooks are observers, not decision-makers. FSM independence preserved (C-4).
Source: openspec:itraversal-hook-extension (D-B)
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs RunAsync recoverable error intercept block
Guard: TraversalHookTests.OnError_Recoverable_IsRecoverableTrue
Commit: pending
Status: Fixed

---

### D-107 | 2026-07-21 | OnAfterRun fired at Done() call sites, not inside Done()

Decision: Each return Done(...) in RunAsync becomes var result = Done(...); await FireAsync(h => h.OnAfterRunAsync(result)); return result. Done() remains synchronous.
Rationale: Call-site approach minimally invasive — only RunAsync call sites change (which are already async). Making Done() async would change all Done() signatures and require await at 7+ call sites.
Source: openspec:itraversal-hook-extension (D-C)
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs all Done() call sites
Guard: TraversalHookTests.BeforeRunAfterRun_TimingCorrect + AfterRun_FiresAtCancelledExit
Commit: pending
Status: Fixed

---

### D-108 | 2026-07-21 | OnBeforeRun fires outside try block

Decision: await FireAsync(h => h.OnBeforeRunAsync(_plan, _ctx)) before try { for (...) } — hook exceptions caught by FireAsync Log-and-Continue, not converted to Done(Error) by engine catch handler.
Rationale: If hook throws in OnBeforeRun, FireAsync catches it (Log-and-Continue). Firing outside try means engine's catch(Exception) block doesn't convert hook failure into Done(Error).
Source: openspec:itraversal-hook-extension (D-D)
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs RunAsync — OnBeforeRun before try
Guard: TraversalHookTests.BeforeRunAfterRun_TimingCorrect
Commit: pending
Status: Fixed

---

### D-109 | 2026-07-21 | FireAsync catch block uses Console.WriteLine

Decision: catch (Exception ex) { Console.WriteLine($"[Hook Warning] {ex.GetType().Name}: {ex.Message}"); } — consistent with TraceCoordinator dispatch-table pattern.
Rationale: Pure silent catch { } inconsistent with TraceCoordinator's Console.WriteLine approach in dispatch-table.md §Log-and-Continue. No DI dependency, observable in console logs.
Source: openspec:itraversal-hook-extension (D-E)
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs FireAsync method
Guard: TraversalHookTests.HookThrows_EngineContinuesWithWarning
Commit: pending
Status: Fixed

Decision: Each JSONL line starts with `{"record_type":"{type}",` before the record payload. 5 types: execution, state_transition, error, page_transition, ai_call. On read, DeserializeByType checks record_type via JsonDocument.Parse, strips it, then deserializes to the target C# record. Corrupted lines are skipped (single bad line doesn't block entire trace read).
Rationale: JSONL is stream-friendly (append-only, no format rewriting). record_type discriminator enables single-file multi-type storage without separate files per record type. Stripping record_type before deserialization keeps C# records clean (no record_type field). Python interop: record_type field is parseable by Python json.loads for future cross-language tooling.
Source: openspec:trace-jsonl-export
Ref: src/UniClaw.Core/Observability/File/FileTraceStorage.cs (SerializeWithDiscriminator, DeserializeByType, RemoveDiscriminator)
Guard: 无 (convention-level)
Commit: pending
Status: Resolved

---

### D-108 | 2026-07-21 | IFileProvider.WriteAllText + EndSession session.json overwrite (D-102)

Decision: IFileProvider gains WriteAllText(string path, string content) method (7th method, 6→7). PhysicalFileProvider delegates to File.WriteAllText. FileTraceStorage.EndSession uses WriteAllText to overwrite session.json with updated TraceSession (EndTime populated), replacing the previous AppendLine(SessionFilePath + "_ended") workaround that created a separate file instead of overwriting.
Rationale: EndSession must overwrite session.json (original design intent §4.4), not append to a different file. The previous workaround (session.json_ended) left stale session.json without EndTime, breaking Python dashboard interop. IFileProvider lacked WriteAllText (6 methods) → could not overwrite → forced workaround. Adding WriteAllText is minimal (one method, zero new concepts) and consistent with D-22 sync-first. PhysicalFileProvider.WriteAllText = File.WriteAllText is atomic (overwrites entire file). MockFileProvider.WriteAllText = dictionary key overwrite (existing key replaced, new key created).
Source: design-doc review finding (C-7 trace-filestorage-jsonl-design.md §4.4 mismatch with code)
Ref: src/UniClaw.Core/Observability/File/IFileProvider.cs (WriteAllText), src/UniClaw.Core/Observability/File/PhysicalFileProvider.cs (WriteAllText), src/UniClaw.Core/Observability/File/FileTraceStorage.cs (EndSession)
Guard: FileTraceStorageTests.EndSession_OverwritesSessionJsonWithEndTime (endTime field present in session.json after EndSession)
Commit: pending
Status: Fixed

### D-110 | 2026-07-21 | IHandlerTraceWriter ISP separation from ITraceCoordinator

Decision: New `IHandlerTraceWriter` interface (1 method: RecordHandlerLifecycleAsync) separated from `ITraceCoordinator` (18 methods). HandlerTraceWriter implements it by delegating to ITraceRecorder.RecordExecutionAsync.
Rationale: ITraceCoordinator already has 18 members — adding handler lifecycle methods would grow it further. ISP-separated interface means handlers only depend on 1 method, not the full coordinator surface. HandlerTraceWriter is simple (delegates to ITraceRecorder, no engine context awareness).
Source: design.md D-1
Ref: src/UniClaw.Core/Observability/IHandlerTraceWriter.cs, HandlerTraceWriter.cs
Guard: IHandlerTraceWriter_HasOneMethod (reflection test)
Commit: pending
Status: Fixed

### D-111 | 2026-07-21 | Trace injection at orchestration layer, not inside handlers

Decision: IHandlerTraceWriter is called by the orchestration layer (StepOrchestrator/InterceptionHandler/TraversalFSM) AFTER the handler returns its result. Handlers (PopupHandler, ContainerHandler, ErrorHandler) are pure pipeline components — they don't know about tracing.
Rationale: Handler pipeline purity principle — handlers should not be coupled to observability concerns. Metadata is extracted from the handler's result fields (ContainerActionResult.CompletionReason, PopupHandlingResult.Classification, etc.) by the orchestration layer, not by the handler itself.
Source: design.md D-2
Ref: src/UniClaw.Core/Traversal/InterceptionHandler.cs (DecideFrameCompletionAsync), src/UniClaw.Core/StateMachine/TraversalFSM.cs (HandlePopupHandlingAsync, HandleErrorHandlingAsync)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-112 | 2026-07-21 | DecideFrameCompletion sync→async for trace recording

Decision: `DecideFrameCompletion` renamed to `DecideFrameCompletionAsync`, returns `Task<(bool, bool, TraversalState)>`. `OnFrameComplete` follows (sync→async `Task<InterceptionResult>`). IInterceptionHandler interface updated.
Rationale: Async needed to `await RecordHandlerLifecycleAsync` inside the completion branch. Fire-and-forget would risk losing trace writes if the continuation is cancelled. Sync version cannot await.
Source: design.md D-3
Ref: src/UniClaw.Core/Traversal/InterceptionHandler.cs, IInterceptionHandler.cs, StepOrchestrator.cs
Guard: DecideFrameCompletion_IsAsync (reflection test — sync variant absent)
Commit: pending
Status: Fixed

### D-113 | 2026-07-21 | TraceCoordinator internal Stopwatch for DurationMs

Decision: TraceCoordinator owns a `System.Diagnostics.Stopwatch _stepStopwatch`, started in `RecordStepStartAsync` (`.Restart()`), stopped in `RecordStepEndAsync` (`.Stop()`, `.Elapsed.TotalMilliseconds → DurationMs`).
Rationale: Step start/end are in the same coordinator — internal Stopwatch eliminates the need for external callers to pass DurationMs. Simplifies all 7+ call sites that previously left DurationMs at 0.
Source: design.md D-4
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (TraceCoordinator — _stepStopwatch field, RecordStepStartAsync, RecordStepEndAsync)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-114 | 2026-07-21 | GlobalFSM trace callbacks closure-capture engine context

Decision: `RegisterGlobalFsmTraceCallbacks` closure captures `_ctx` (engine context) and builds `new TraceContext(NodeId: _ctx.CurrentFrame?.NodeId, StepSpanId: null, StepNumber: _ctx.StepCount, TraceId: _ctx.TraceId)`. Replaces previous `Context: null`. Callbacks registered for all 8 GlobalState values (not just 4) — non-terminal states and terminal states alike, since `RegisterStateCallback` fires on incoming transitions (destination-based), not outgoing.
Rationale: Engine context (`_ctx`) is available throughout the engine lifecycle — closure capture avoids passing it as a separate parameter. `Context: null` made GlobalFSM transitions uncorrelated with engine state (no NodeId, no StepNumber). All 8 states must be registered because the callback fires when transitioning TO the state, not FROM it — Completed must be registered to trace completion events.
Source: design.md D-5
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (RegisterGlobalFsmTraceCallbacks)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-115 | 2026-07-21 | TraceHandlerAttribute documentation-only in C-10 phase

Decision: `TraceHandlerAttribute(SpanType, string Action)` is defined as `[AttributeUsage(Method)]` but has zero runtime behavior in C-10 phase. It documents handler entry points only. Phase 3-B: Roslyn incremental source generator will scan `[TraceHandler]` and inject span lifecycle code.
Rationale: Running trace logic from the attribute in C-10 would couple attribute presence with observability behavior before the source generator is ready. Define the contract first (attribute shape), defer auto-wiring to Phase 3-B. Manual IHandlerTraceWriter calls in C-10 serve as the "hand-written" baseline that Phase 3-B will automate.
Source: design.md D-6
Ref: src/UniClaw.Core/Observability/TraceHandlerAttribute.cs
Guard: TraceHandlerAttribute decorates methods only (AttributeUsage test)
Commit: pending
Status: Fixed

### D-116 | 2026-07-21 | Phase 3-A: TraceContext +2 fields (VisitSpanId, ParentSpanId)

Decision: Phase 3-A adds `VisitSpanId` and `ParentSpanId` to TraceContext (4→6 fields). AICallRecord.Metadata is NOT the right place for ParentSpanId — ParentSpanId is a universal span correlation field, not AI-specific. TraceContext is the shared envelope for all 5 record types.
Rationale: ParentSpanId enables automatic parent-child span tree construction across all span types (ExecutionRecord, StateTransition, ErrorRecord, PageTransition, AICallRecord). Putting ParentSpanId on AICallRecord only would limit tree-building to AI calls. TraceContext is the natural home for cross-cutting correlation fields.
Source: design.md D-7
Ref: docs/system/layers/observability.md §Phase 3 Roadmap
Guard: TraceContext_Has4Fields (upgraded to 6 in Phase 3-A)
Commit: pending
Status: Deferred · Target: Phase 3-A

### D-117 | 2026-07-21 | Stack-based ParentSpanId propagation

Decision: TraceCoordinator maintains `_spanStack`, `PushSpan()` genSpanId→push→return, `PopSpan(spanId)` pop-if-top-matches, `BuildCorrelation()` reads stack top for `ParentSpanId`. Not AsyncLocal.
Rationale: Consistent with existing explicit mutable state pattern (`_currentStepSpanId`). Inspectable in debugger. Immune to Task.Run/ConfigureAwait boundary issues.
Source: openspec:phase3-trace-span-tree (D-3A-1)
Ref: src/UniClaw.Core/Traversal/TraversalEngine.cs (TraceCoordinator)
Guard: ITraceCoordinator_Has24Members
Commit: pending
Status: Fixed

### D-118 | 2026-07-21 | HandlerTraceWriter explicit TraceContext parameter

Decision: `RecordHandlerLifecycleAsync` gets `TraceContext? context = null` parameter. Not constructor injection of ITraversalContext.
Rationale: Stateless — keeps HandlerTraceWriter testable with null context. Mirrors existing ITraceRecorder.RecordExecutionAsync pattern. Coordination layer calls trace.BuildCorrelation() and passes the result.
Source: openspec:phase3-trace-span-tree (D-3A-2)
Ref: src/UniClaw.Core/Observability/HandlerTraceWriter.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-119 | 2026-07-21 | 方案 D: Source gen auto-extracts return type properties → metadata

Decision: Roslyn source generator inspects handler return type at compile time, emits code that reads all readable properties into metadata dictionary. Enum→string, null skip, [TraceIgnore] exclusion. extraMetadata dictionary for cross-source fields.
Rationale: Keeps handlers pure (result types don't depend on Observability). Compile-time property extraction (zero runtime reflection). Cross-source fields merged via 1-line dictionary.
Source: openspec:phase3-trace-span-tree (D-3B-1)
Ref: src/UniClaw.Core.SourceGen/TraceHandlerGenerator.Emitter.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-120 | 2026-07-21 | Source generator emits async wrapper (original stays sync)

Decision: Generated `HandleXxxTracedAsync` is an async wrapper that delegates to the original sync method. Not full method body replacement.
Rationale: Thin wrapper is safer, easier to reason about. Rollback = remove [TraceHandler] attribute and revert coordination layer call site.
Source: openspec:phase3-trace-span-tree (D-3B-2)
Ref: src/UniClaw.Core.SourceGen/TraceHandlerGenerator.Emitter.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-121 | 2026-07-21 | TraceIgnoreAttribute for property exclusion

Decision: `[TraceIgnore]` on a return type property excludes it from source-generator auto-extracted metadata. No exclusion mechanism → conservatively include all properties, explicit opt-out.
Rationale: Some properties (complex nested types, internal IDs) shouldn't be in trace metadata. Attribute-based opt-out is discoverable and compile-time safe.
Source: openspec:phase3-trace-span-tree (D-3B-3)
Ref: src/UniClaw.Core/Observability/TraceIgnoreAttribute.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-122 | 2026-07-21 | 3 handler pipeline methods only (not all 6 injection points)

Decision: Phase 3-B automates 3 handler pipeline methods (HandleError, HandlePopup, HandleContainer). DfsBacktrack 3 points (leaf_execution_complete, pop_only, press_back) remain manual.
Rationale: DfsBacktrack 3 points are if-block conditional calls — unsuitable for method-level source generation. Manual calls coexist with generated wrappers (spec: coexistence requirement).
Source: openspec:phase3-trace-span-tree (D-3B-4)
Ref: src/UniClaw.Core/Traversal/InterceptionHandler.cs (DfsBacktrack sites)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-123 | 2026-07-22 | UniBrain: Hybrid facade + ISP — 统一入口 + 独立子接口

Decision: IUniBrain 是对外统一 facade（单一注入点），内部 3 子接口（IPageAnalyzer, ITraversalAdvisor, ITextUnderstanding）各自独立实现 ISP。
Rationale: 消费者注入一个东西，但各能力可独立测试/替换/路由到不同 provider。纯统一接口牺牲 ISP；纯独立接口增加注入复杂度。Hybrid 兼顾两者。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-1, src/UniClaw.Core/UniBrain/IUniBrain.cs
Guard: ArchitectureGuardTests.IUniBrain_Has3SubInterfaces
Commit: pending
Status: Locked

---

### D-124 | 2026-07-22 | UniBrain: 子接口按职责语义分组，非按调用模式

Decision: IPageAnalyzer（页面感知+验证）、ITraversalAdvisor（遍历决策）、ITextUnderstanding（文本理解）按职责分组。
Rationale: 旧 Vision/Text/Decision 分组按 AI 调用模式导致职责混乱：IVisionProvider 混 4 种职责，IDecisionBrain 混 5 种，VerifyPageTypeAsync 在两处分裂。按职责分组：每个接口单一职责，内聚性高。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-2, src/UniClaw.Core/UniBrain/IPageAnalyzer.cs, ITraversalAdvisor.cs, ITextUnderstanding.cs
Guard: ArchitectureGuardTests.IUniBrain_Has3SubInterfaces
Commit: pending
Status: Locked

---

### D-125 | 2026-07-22 | UniBrain: IUniBrain 替换 IVisionProvider

Decision: TraversalEngine/StepContext 注入 IUniBrain 而非 IVisionProvider。IVisionProvider 接口删除。
Rationale: 统一 AI 服务入口，避免引擎同时注入 IVisionProvider + IAIStrategyAdvisor 两个 AI 接口。Mode A/B 成为 IPageAnalyzer 实现选择，facade 无感。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-3, src/UniClaw.Core/Traversal/TraversalEngine.cs
Guard: ArchitectureGuardTests.Traversal_ReferencesUniBrainForIUniBrain
Commit: pending
Status: Fixed

---

### D-126 | 2026-07-22 | UniBrain: 滚动感知脱离 AI — IScreenStateProvider 独立

Decision: 滚动方法从 IVisionProvider 分离到 IScreenStateProvider（Traversal namespace），不在 IUniBrain 上。
Rationale: 滚动是设备/平台状态查询，不是 AI 判断。Simulation mock 返回编程值不走 AI。强制放"大脑"接口是职责泄漏。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-4, src/UniClaw.Core/Traversal/IScreenStateProvider.cs
Guard: ArchitectureGuardTests.IScreenStateProvider_Has4Methods
Commit: pending
Status: Locked

---

### D-127 | 2026-07-22 | UniBrain: 配置驱动组合，非品牌 monolith

Decision: 无 ClaudeUniBrain 品牌绑定类。UniBrainService 是纯组合容器（sealed class），子接口实现独立可替换，组合由配置/DI 决定。
Rationale: 高内聚低耦合 — 每个子接口实现只关心自己的能力。品牌绑定在具体实现内部，不在 facade 上。配置灵活组合: Claude(vision) + DeepSeek(decision) + local(text) 等。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-5, src/UniClaw.Core/UniBrain/UniBrainService.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-128 | 2026-07-22 | UniBrain: 零 StateMachine 引用 — 单向依赖

Decision: ITraversalAdvisor 方法只接收 Domain 类型 + BCL 类型，不引用 ITraversalContext（StateMachine 接口）。Call site 从 ITraversalContext 提取 string/int 值直接传入。
Rationale: 避免 UniBrain↔StateMachine 双向依赖。ITraversalContext 是 StateMachine 接口，如果 UniBrain 引用它，形成循环：StateMachine→UniBrain（注入）+ UniBrain→StateMachine（参数）。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-6, src/UniClaw.Core/UniBrain/ITraversalAdvisor.cs
Guard: ArchitectureGuardTests.UniBrain_DoesNotReferenceStateMachine, UniBrain_DoesNotReferenceTraversal
Commit: pending
Status: Locked

---

### D-129 | 2026-07-22 | UniBrain: 观测记录责任归属子接口实现

Decision: 子接口实现调用 ITraceRecorder.RecordAICallAsync，将 capability 语义 + ModelResponse 数据合并写入 AICallRecord。IModelProvider 是纯传输层（call + retry + timeout），不负责观测记录。
Rationale: IModelProvider 不知道调用目的是 "page_analysis" 还是 "next_action"，只有子接口实现同时拥有 capability 语义和 ModelResponse 数据。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-7, src/UniClaw.Core/UniBrain/IModelProvider.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-130 | 2026-07-22 | UniBrain: 层级归属 — Domain 上方

Decision: UniBrain namespace (UniClaw.Core.UniBrain) 依赖 Domain.Content + Domain.Common，不依赖 StateMachine/Traversal。StateMachine/Traversal 注入 IUniBrain 是向上引用（acknowledged, 同 D-14/D-17 模式）。
Rationale: 保持层级依赖方向一致性。UniBrain 不反向引用，消除双向依赖。
Source: openspec:unibrain-unified-ai-service
Ref: openspec/changes/archive/2026-07-22-unibrain-unified-ai-service/design.md §D-8, src/UniClaw.Core/UniBrain/
Guard: ArchitectureGuardTests.UniBrain_DoesNotReferenceStateMachine, UniBrain_DoesNotReferenceTraversal
Commit: pending
Status: Locked

---

### D-131 | 2026-07-23 | PromptTemplate: 变量替换 — 声明变量迭代 (string.Replace)

Decision: PromptTemplate.Resolve 遍历 Variables 列表，逐个执行 string.Replace("{var_name}", value)，对 SystemPrompt 和 UserPrompt 同时替换。未声明的 {foo} 保持原样不动（对 JSON/code 示例安全），额外输入变量静默忽略。
Rationale: 对齐 Python PromptManager 的 str.replace 机制。拒绝 regex `\{(\w+)\}` 扫描方案——会误伤模板中字面花括号内容（如 JSON 示例 {\"key\": \"val\"}）。声明变量迭代只替已知占位符，未声明的一律不碰。
Source: openspec:prompt-template-engine
Ref: openspec/changes/prompt-template-engine/design.md §D-1, src/UniClaw.Core/UniBrain/PromptTemplate.cs Resolve()
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-132 | 2026-07-23 | PromptTemplate: 构造期校验 — 声明变量必须出现在模板文本

Decision: PromptTemplate 构造期 fail-fast 校验：Variables 中每个变量名必须以 {var_name} 形式出现在 SystemPrompt 或 UserPrompt，否则抛 DomainValidationException(FieldName="Variables", 含变量名)。将 Python 分离的 validate_prompt() 折叠进 fail-fast 构造。
Rationale: 在构造期捕获拼写错误（声明 "goal" 却写 {gola}），而非推迟到 Resolve 才暴露。fail-fast 构造是项目通用校验策略（同 Coordinate/BoundingBox/Operation）。
Source: openspec:prompt-template-engine
Ref: openspec/changes/prompt-template-engine/design.md §D-2, src/UniClaw.Core/UniBrain/PromptTemplate.cs 构造器
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-133 | 2026-07-23 | ResolvedPrompt: 命名返回类型替代 ValueTuple

Decision: PromptTemplate.Resolve 返回 ResolvedPrompt（sealed record class: System + User），而非裸 (string, string) ValueTuple。ResolvedPrompt 字段直接映射 ModelRequest.Prompt (User) 与 ModelRequest.SystemPrompt (System)。
Rationale: 命名类型提供 IDE 可发现性与自文档化 API，消除 ValueTuple 的位置记忆负担 (item1=system? item2=user?)。符合项目 sealed record class 约定。
Source: openspec:prompt-template-engine
Ref: openspec/changes/prompt-template-engine/design.md §D-3, src/UniClaw.Core/UniBrain/ResolvedPrompt.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-134 | 2026-07-23 | IPromptLibrary: 含 ValidateCapability 诊断方法 + 不暴露在 facade

Decision: IPromptLibrary 三方法：GetTemplate(capability)→PromptTemplate?（不存在返 null 不抛）、GetCapabilities()→IReadOnlyList<string>、ValidateCapability(capability)→bool（诊断，不抛异常）。IPromptLibrary 不暴露在 IUniBrain facade——prompt 管理是子接口实现内部关注点。
Rationale: ValidateCapability 对齐 Python validate_prompt() 作为无副作用的诊断入口。不上 facade 是因为 prompt 管理对 facade 消费者（StateMachine/Traversal）不可见，仅子接口实现 (PageAnalyzer/TraversalAdvisor 等) 需注入它获取 prompt 再调 IModelProvider。
Source: openspec:prompt-template-engine
Ref: openspec/changes/prompt-template-engine/design.md §D-4, src/UniClaw.Core/UniBrain/IPromptLibrary.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-135 | 2026-07-25 | UniBrain: 观测责任翻转为 ObservingModelProvider decorator（取代 D-129）

Decision: AICallRecord 观测改由 ObservingModelProvider（sealed class : IModelProvider 的 decorator）负责，在 ModelRouter 构造期为每个裸 provider 套用，结构上不可绕过——经 router.Resolve 返回的 provider 必然已观测。IModelProvider 保持纯传输（DeepSeekModelProvider / MockModelProvider 不记 AICallRecord）。取代 D-129（子接口实现内联观测）。
Rationale: D-129 让每个子接口实现自觉观测，重复 4 处（每 capability 一份）且传输级 mock（MockModelProvider）可绕过观测。decorator 位于「传输之上、子接口之下」，router 组装期统一套用，调用方无法获取未观测的 provider，比「子接口自觉」更强保证。翻转 model-provider spec R1，IModelProvider 接口签名不变。D-129 按 append-only 不改，由本条 forward-reference 取代。
Source: openspec:unibrain-modelprovider-vertical-slice
Ref: openspec/changes/archive/2026-07-25-unibrain-modelprovider-vertical-slice/design.md §D2, src/UniClaw.Core/UniBrain/ObservingModelProvider.cs, ModelRouter.cs
Guard: 无 (convention-level；UniBrain→Observability 方向依 D-17 不受 DependencyDirectionGuard 验)
Commit: pending
Status: Locked (supersedes D-129)

---

### D-136 | 2026-07-25 | UniBrain: IModelRouter — capability 路由 + 组装期套 decorator；UniBrainService 保持纯组合

Decision: 新增 IModelRouter（Resolve(capability)→IModelProvider）+ sealed ModelRouter：构造期校验 capabilityRouting 引用的 providerId 都在 providers（否则 DomainValidationException），并为每个裸 provider 套 ObservingModelProvider 存内部表；Resolve = 查表 → defaultProviderId 回落 → 仍无则 DomainValidationException，只返回已观测实例。routing 不进 UniBrainService（facade 保持纯组合容器，不持 IModelProvider、不做 routing），与 Python（UniBrain 自持 providers dict + 做 routing）分叉。
Rationale: facade 纯组合对齐已锁定的 unibrain-facade spec（UniBrainService 不做 routing），保持可测性 + 不变胖；capability→provider 的路由下沉独立 ModelRouter，单一职责，组装期一次性套观测保证不可绕过。
Source: openspec:unibrain-modelprovider-vertical-slice
Ref: openspec/changes/archive/2026-07-25-unibrain-modelprovider-vertical-slice/design.md §D1+§4, src/UniClaw.Core/UniBrain/IModelRouter.cs, ModelRouter.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-137 | 2026-07-25 | UniBrain: ModelRequest.Capability — 跨三层语义标签 + ModelCapabilities 5 常量

Decision: ModelRequest 新增可选 string? Capability = null（向后兼容），作为流经 IModelRouter.Resolve / ObservingModelProvider / 传输层的唯一语义标签：mock 按 capability 查 fixture、decorator 记 AICallRecord.Capability、传输层可忽略。配套 ModelCapabilities static class 定义 5 个 Python 对齐常量（parse_instruction / verify_page_type / decide_next_action / screen_safety / analyze_visual，排除 C# YAGNI 的 verify_page_with_vision）。
Rationale: 一字段统一三处需求，避免改 IModelProvider 签名（破坏面更大）或 router 用独立 capability 参数（decorator 拿不到 capability）。常量消灭魔术字符串 + 跨语言对照。
Source: openspec:unibrain-modelprovider-vertical-slice
Ref: openspec/changes/archive/2026-07-25-unibrain-modelprovider-vertical-slice/design.md §D4+§D6, src/UniClaw.Core/UniBrain/ModelRequest.cs, ModelCapabilities.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-138 | 2026-07-25 | DeepSeek 垂直切片：OpenAI-compatible text-only，传输错误 graceful，无 DI

Decision: DeepSeekModelProvider（sealed : IModelProvider）仅实现 CompleteTextAsync（Vision/Multimodal 留 NotImplementedException）：POST {BaseUrl}/chat/completions，Authorization Bearer，body 含 model/messages/max_tokens、Schema!=null 时加 response_format:{type:"json_object"}；映射 choices[0].message.content + usage tokens，Mode="text"。传输错误（HTTP 非2xx / 超时 / JSON 解析失败）→ ModelResponse(Success:false, ErrorMessage) 不抛（用户 ct 取消重抛）。HttpClient + DeepSeekProviderConfig 由调用方注入，不引入 DI / IHttpClientFactory / Polly（YAGNI）。Config 构造期 fail-fast（ApiKey/Model/BaseUrl 非空、并发>0、超时>0）。
Rationale: 垂直切片目标是验证 IModelProvider 抽象是否站得住，非搭基础设施；text 优先匹配 parse_instruction 链路；graceful 错误对齐 ModelResponse.Success 契约，让上层统一 fail-fast。
Source: openspec:unibrain-modelprovider-vertical-slice
Ref: openspec/changes/archive/2026-07-25-unibrain-modelprovider-vertical-slice/design.md §D5+§D7, src/UniClaw.DeepSeekProvider/DeepSeekModelProvider.cs, DeepSeekProviderConfig.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-139 | 2026-07-25 | UniBrain: provider 旧 stub 清理 + capability 词汇表整合 延迟（D8+D9）

Decision: (a) provider 项目 Python 风格子接口 stub（DeepSeekTextUnderstanding / ClaudeTextUnderstanding / DeepSeekTraversalAdvisor / ClaudePageAnalyzer / ClaudeTraversalAdvisor / AnthropicModelProvider）本 change 一律不动，留独立清理 change——它们与新 router 架构（子接口 provider-agnostic、靠 router 路由到 IModelProvider）互斥。(b) ModelCapabilities 细粒度 5 常量 vs UniBrainConfig.CapabilityRouting 粗粒度 3 键 两套 capability 词汇表本期不交叉（切片硬编码 routing 传入 ModelRouter），整合留开放问题。
Rationale: 避免垂直切片 scope 蔓延；provider stub 删除属破坏性变更需独立评审；capability 词汇表统一需改 unibrain-facade spec + 重设 routing 粒度，超切片 scope。追踪：design.md Open Questions 5/6。
Source: openspec:unibrain-modelprovider-vertical-slice
Ref: openspec/changes/archive/2026-07-25-unibrain-modelprovider-vertical-slice/design.md §D8+§D9+§Open Questions 5/6
Guard: 无 (convention-level; deferral — 跟踪至 Open Questions)
Commit: pending
Status: Locked

---

### D-140 | 2026-07-25 | UniBrain: router+decorator+capability 范式推广到「结构化状态入 → 丰富决策出」（第二条垂直切片确认）

Decision: unibrain-traversaladvisor-vertical-slice 把 D-135/D-136/D-137 确立的 IModelRouter + ObservingModelProvider + ModelRequest.Capability 范式套到 ITraversalAdvisor.DecideNextActionAsync，验证它从最简形态（TextUnderstanding：纯文本入 → 扁平 4 字段出）推广到更复杂形态（PageAnalysis 复合类型序列化进 prompt → ContextDecisionResult 7 字段含 Params 字典）无需任何新基础设施——TraversalAdvisor 与 TextUnderstanding 同构（sealed class + IModelRouter + IPromptLibrary ctor + 7 步方法）。范式升格为 UniBrain 全部 5 个 capability 的通用模板。
Rationale: 第二条切片是范式可推广性的实证；若需新基础设施才能支持结构化入/丰富出，则范式不成立。同构实现证明剩余 capability（verify_page_type / screen_safety / analyze_visual + ITraversalAdvisor 其余 3 方法）可照搬。
Source: openspec:unibrain-traversaladvisor-vertical-slice
Ref: openspec/changes/unibrain-traversaladvisor-vertical-slice/design.md §D6, src/UniClaw.Core/UniBrain/TraversalAdvisor.cs, TextUnderstanding.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-141 | 2026-07-25 | UniBrain: ContextDecisionResult.Params（ImmutableDictionary&lt;string,object&gt;?）反序列化 — DTO Dictionary&lt;string,JsonElement&gt;? + ValueKind 映射

Decision: 模型返回的 params 是扁平 JSON object；反序列化用私有 DTO 暴露 Dictionary&lt;string,JsonElement&gt;?（非 object?），映射器按 JsonElement.ValueKind 转 CLR 原始值（String→string / Number→double / True·False→bool / 其余→GetRawText()）构建 ImmutableDictionary&lt;string,object&gt;?，null 保持 null。规避 System.Text.Json 把 object 反序列化成 JsonElement 的 buffer 生命周期隐患（JsonDocument 释放后 JsonElement 失效）。嵌套 object/array 本 slice 不支持（ValueKind 映射只处理原始值）。Confidence 字段反之直通不校验——ContextDecisionResult 构造器现状无 0-1 校验，advisor 尊重既有类型契约，硬化留改类型的独立 change。
Rationale: 直接 DTO 用 object? 会装箱 JsonElement（绑定底层 UTF-8 buffer，JsonDocument 释放后 use-after-free 式隐患）；ValueKind 映射在反序列化即刻转 CLR 原始值，detached 安全。遍历决策 params 是扁平原始值（如 {"timeout":5000}），无需 JsonNode full tree 开销。
Source: openspec:unibrain-traversaladvisor-vertical-slice
Ref: openspec/changes/unibrain-traversaladvisor-vertical-slice/design.md §D3+§D5, src/UniClaw.Core/UniBrain/TraversalAdvisor.cs (MapParams)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-142 | 2026-07-25 | UniBrain: Domain 对象 → prompt 序列化用 DomainJsonOptions.Default

Decision: 把 PageAnalysis（或任何 Domain 复合类型）注入 prompt 模板变量时，用 JsonSerializer.Serialize(obj, DomainJsonOptions.Default)（camelCase + enum-as-string），与模型可见的输出 schema 同构；不手写文本 flattener。变量 {page_analysis} 收序列化后的 JSON 字符串。
Rationale: 一行完成、信息完整（元素坐标/类型/文本全保留）、与 schema 同构减少模型认知负担；手写 flattener 是 token 优化的未来选项（真机大页裁剪），非切片所需。
Source: openspec:unibrain-traversaladvisor-vertical-slice
Ref: openspec/changes/unibrain-traversaladvisor-vertical-slice/design.md §D2, src/UniClaw.Core/UniBrain/TraversalAdvisor.cs (step 2)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-143 | 2026-07-25 | UniBrain: ITraversalAdvisor 部分实现 NIE idiom + 剩余切片规划

Decision: TraversalAdvisor 本 slice 仅实现 DecideNextActionAsync 真实链路；ITraversalAdvisor 其余 3 方法（InferContainerTypeAsync / HandleExceptionAsync / ScreenSafetyAsync）抛 NotImplementedException("...pending future slice.")。同 D-139 确立的「切片边界用 NIE 标 pending」idiom，从「传输 provider 方法 stub」推广到「接口部分实现」。剩余 3 方法 + verify_page_type / screen_safety / analyze_visual capability 各起独立切片（一 capability 一切片纪律）。
Rationale: 一个 capability = 一条垂直切片，最大化复用 + 控制变更面；NIE 文案明确标注 pending 让部分实现诚实可发现。3 方法 NIE 在运行期无实际触发路径（handler 目前不接 ITraversalAdvisor 真实实现）。
Source: openspec:unibrain-traversaladvisor-vertical-slice
Ref: openspec/changes/unibrain-traversaladvisor-vertical-slice/design.md §D1, src/UniClaw.Core/UniBrain/TraversalAdvisor.cs (3 NIE methods), D-139
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-144 | 2026-07-26 | UniBrain: IPageAnalyzer 部分实现 NIE idiom — AnalyzeVisual 切片边界

Decision: PageAnalyzer 本 slice 仅实现 AnalyzeCurrentPageAsync 真实 7 步链路；IPageAnalyzer 其余 2 方法（FindAppEntryAsync / VerifyPageTypeAsync）抛 NotImplementedException("PageAnalyzer.<method> pending future slice.")。同 D-139/D-143 确立的「切片边界用 NIE 标 pending」idiom，从 ITraversalAdvisor 推广到 IPageAnalyzer。verify_page_type / find_app_entry 各起独立切片（一 capability 一切片纪律）。
Rationale: 一个 capability = 一条垂直切片。analyze_visual 是 Mode A 视觉链路 Core 侧核心 capability；其余 2 方法属不同语义（app 入口查找 / 页面类型验证），混入模糊切片边界。NIE 而非 NotSupportedException 对齐项目 idiom（语义是「尚未」非「永不」），运行期无实际触发路径（当前 handler 不接 IPageAnalyzer 真实实现）。
Source: openspec:unibrain-analyzevisual-vertical-slice
Ref: openspec/changes/unibrain-analyzevisual-vertical-slice/design.md §D1, src/UniClaw.Core/UniBrain/PageAnalyzer.cs (2 NIE methods), D-139, D-143
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-145 | 2026-07-26 | UniBrain: PageAnalyzer 三依赖 + 第0步截图 + CompleteVisionAsync 调用步

Decision: PageAnalyzer ctor 注入 IModelProvider + IPromptLibrary + IScreenCapture（截图来源是第三依赖）。AnalyzeCurrentPageAsync 7 步：① screenCapture.CaptureAsync 截图（范式新增步）② GetTemplate(AnalyzeVisual) 缺失 fail-fast ③ Resolve({})（截图是 bytes 不入 prompt 变量，Variables 空）④ ModelRequest(User, System, Schemas.AnalyzeVisual, Capability: AnalyzeVisual) ⑤ modelProvider.CompleteVisionAsync(req, bytes, ct)（直接调，无路由步）⑥ !resp.Success → fail-fast ⑦ Deserialize<PageAnalysisDto> → MapToPageAnalysis 派生。
Rationale: 骨架沿用前两切片（D-140/D-142），仅插第 0 步截图、调用步换 CompleteVisionAsync。范式零基础设施扩展——byte[] 本是 CompleteVisionAsync 方法参数，ObservingModelProvider 已覆盖 mode="vision" 记录。
Source: openspec:unibrain-analyzevisual-vertical-slice
Ref: openspec/changes/unibrain-analyzevisual-vertical-slice/design.md §D2, src/UniClaw.Core/UniBrain/PageAnalyzer.cs (AnalyzeCurrentPageAsync)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-146 | 2026-07-26 | UniBrain: §12-A ElementTypeMapper 派生 action + page/state change — prompt 剥散文，单一真相源

Decision: prompt 删 type→action 散文映射（Python vision_service.py:19-112 BUTTON TYPE CLASSIFICATION 段 + 4 example + expected_action/expects_* 输出字段要求），AI 只返 type。映射阶段：itemType = ElementTypeMapper.ToMenuItemType(dto.Type)；action = ElementTypeMapper.ToExpectedAction(dto.Type)；pageChange/stateChange 由 action 确定性派生（Navigate/Action→pageChange=true,stateChange=false；Toggle→pageChange=false,stateChange=true；None→both false，封在私有 DeriveChangeFlags helper）。保留 type 词表（10 type）供 AI 分类。Schemas.AnalyzeVisual items 只列 name/type/coordinate/parent，不含 action 3 字段（D6）。非法 type 经 ElementTypeMapper.IsValidType 主动校验抛 DomainValidationException（ToMenuItemType/ToExpectedAction 有回落值不抛）。
Rationale: ElementTypeMapper.ExpectedActionMap 是 type→action 的 code 侧唯一真相源，消除 prompt↔code 散映射漂移（Python prompt 已证实易漂移）。零漂移核实：9/10 type 与 Python 散文一致；唯一分歧 link（Python 标 action / C# 标 Navigate）派生出的 page/state change 相同（pageChange=true/stateChange=false），可观察行为一致。剥散文是架构正确性（单一真相源），与模型能力正交。
Source: openspec:unibrain-analyzevisual-vertical-slice
Ref: openspec/changes/unibrain-analyzevisual-vertical-slice/design.md §D3+§D6, src/UniClaw.Core/UniBrain/PageAnalyzer.cs (MapItem, DeriveChangeFlags), src/UniClaw.Core/UniBrain/Schemas.cs (AnalyzeVisual)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-147 | 2026-07-26 | UniBrain: PageAnalysisDto + 映射模式推广到 vision（嵌套丰富出）

Decision: 反序列化用内部私有 DTO 宽松承载 prompt JSON（可空字段 + 宽容 coordinate），映射阶段调 ElementTypeMapper + 构造 Domain record（fail-fast）。DTO 镜像 §5.3 schema：PageAnalysisDto / MenuInfoDto / ItemDto(仅 name/type/coordinate/parent) / CoordDto / PopupInfoDto。多词字段显式 [JsonPropertyName] 锚定 snake_case 键名（DomainJsonOptions.CamelCase 仅对单词属性生效）。
Rationale: 推广自 D-141 DecideNextActionDto 映射 idiom，处理更复杂形态（嵌套 MenuInfo/MenuItem/Coordinate + 12 字段）。DTO 宽容承载、映射期集中 fail-fast，分离「传输形态」与「Domain 不变式」。
Source: openspec:unibrain-analyzevisual-vertical-slice
Ref: openspec/changes/unibrain-analyzevisual-vertical-slice/design.md §D4, src/UniClaw.Core/UniBrain/PageAnalyzer.cs (private DTOs + MapToPageAnalysis), D-141
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-148 | 2026-07-26 | UniBrain: IScreenCapture 放 UniBrain/ namespace 不放 Traversal/ — D-130 charter 兼容

Decision: IScreenCapture（截图捕获 Core 接缝，Task<byte[]> CaptureAsync(CancellationToken)）放在 src/UniClaw.Core/UniBrain/IScreenCapture.cs，namespace UniClaw.Core.UniBrain —— 不放 Traversal/（原 D5 拟与 IActionExecutor 共置 Traversal/ 的 placement 修订）。Core 持抽象；真机实现（AdbScreenCapture）属 host。IPageAnalyzer.AnalyzeCurrentPageAsync 签名零改动（§12-B 截图归属）
Rationale: D-130 Locked charter 规定 "UniBrain namespace 不依赖 StateMachine/Traversal"（Guard: UniBrain_DoesNotReferenceTraversal, CI-blocking）。IScreenCapture 唯一 Core 消费者是 PageAnalyzer (UniBrain)；若放 Traversal/，PageAnalyzer 必须 using UniClaw.Core.Traversal → 直接撞 D-130。IActionExecutor 先例不撞 D-130 是因为它无 UniBrain 消费者（消费者是 TraversalEngine 同目录 + StateMachine/OperationDispatcher）。把 IScreenCapture 放 UniBrain 消除向上引用，D-130/guard 零改动；IActionExecutor 与 IScreenCapture 各归其消费者所在 namespace，语义更纯。原 §5/§12-B/§12-A 所有意图保留，仅 placement 条款修订。
备选拒：① 放 Traversal/ + 放宽 D-130 guard（类比 D-17 Observability 例外）—— D-130 是 Locked charter 非必要不动，IScreenCapture 非 cross-cutting（唯一消费者在 UniBrain）无例外理由；② 放 Domain —— 设备 I/O 抽象不属 Domain 职责。
Source: openspec:unibrain-analyzevisual-vertical-slice
Ref: openspec/changes/unibrain-analyzevisual-vertical-slice/design.md §D5 (修订), src/UniClaw.Core/UniBrain/IScreenCapture.cs, D-130
Guard: 无 (convention-level) — D-130 guard 不受影响（placement 选择本身消除违规）
Commit: pending
Status: Locked

---

### D-149 | 2026-07-26 | UniBrain: 接口注入 IModelProvider 替代 IModelRouter — 范式洁癖演进，router 降为装配期工厂

Decision: PageAnalyzer ctor 注入 IModelProvider（装配期 router.Resolve(AnalyzeVisual) 产物，已套 ObservingModelProvider），不注入 IModelRouter。方法体内无 router.Resolve 步（装配期完成）。IModelRouter 降为装配期工厂——不再作为子接口运行时依赖，但观测组装的结构性保证保留（router.Resolve 仍统一套 decorator）。
Rationale: 路由属装配决策，业务子接口只调模型，不该碰路由抽象。子接口 provider-agnostic 性质更纯（连路由都不依赖）。备选拒：沿用 IModelRouter（前两切片范式）—— 暴露不必要的路由抽象给业务子接口，违背最小依赖。
Scope 约束：前两切片（TextUnderstanding / TraversalAdvisor）仍用旧范式，本 slice 不回改（避免混入无关重构），开 follow-up refactor change 统一（OQ-6）。
Source: openspec:unibrain-analyzevisual-vertical-slice
Ref: openspec/changes/unibrain-analyzevisual-vertical-slice/design.md §D8, src/UniClaw.Core/UniBrain/PageAnalyzer.cs (ctor)
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-150 | 2026-07-26 | PromptTemplateRegistry: static 属性而非 DI 注册

Decision: PromptTemplateRegistry 为 static class，3 个 public static PromptTemplate 只读属性。测试直接引用 PromptTemplateRegistry.AnalyzeVisual 等。
Rationale: prompt 模板是编译期常量（不改不重启），无 DI 必要。static 属性零开销、零装配期、测试直接引用无需 mock。若未来需支持多语言/多版本切换，再改为 DI 注册。
Source: openspec:prompt-template-registry
Ref: openspec/changes/prompt-template-registry/design.md §D1, src/UniClaw.Core/UniBrain/PromptTemplateRegistry.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-151 | 2026-07-26 | PromptTemplateRegistry: 模板文本权威来源

Decision: AnalyzeVisual 文本来自 unibrain-analyzevisual-vertical-slice design.md §4.1 终稿（§12-A 剥散文后版本）。ParseInstruction/DecideNextAction 文本来自各测试文件 inline 常量（即原 design 终稿）。
Rationale: 模板文本已在各自 change 的 design.md 终稿锁定。registry 是统一入口，不改文本语义，消除 6 处副本。
Source: openspec:prompt-template-registry
Ref: openspec/changes/prompt-template-registry/design.md §D2, src/UniClaw.Core/UniBrain/PromptTemplateRegistry.cs
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-152 | 2026-07-28 | Codex OpenSpec trigger rules belong in AGENTS.md

Decision: Store Codex OpenSpec lifecycle trigger rules in the shared repository entry point `AGENTS.md`.
Rationale: `AGENTS.md` is the durable, versioned guidance surface read by Codex and other coding agents, while Claude-specific entry behavior remains in `CLAUDE.md` and `.claude/`.
Source: openspec:codex-openspec-command-routing
Ref: AGENTS.md
Guard: N/A (convention-level)
Commit: pending
Status: Locked

### D-153 | 2026-07-28 | Codex uses natural-language OpenSpec triggers

Decision: Codex OpenSpec actions use natural-language prompts such as `openspec propose <change>` and do not pretend Claude slash commands execute natively in Codex.
Rationale: The explicit mapping makes the actual execution model discoverable and avoids a false command affordance.
Source: openspec:codex-openspec-command-routing
Ref: AGENTS.md
Guard: N/A (convention-level)
Commit: pending
Status: Locked

### D-154 | 2026-07-28 | Claude OpenSpec skills remain the project playbook source

Decision: Codex reuses `.claude/skills/openspec-*` as project-local playbooks by reference until a native Codex skill or plugin migration is justified.
Rationale: Keeping one source prevents duplicated workflow instructions from drifting across assistants.
Source: openspec:codex-openspec-command-routing
Ref: AGENTS.md; .claude/skills/openspec-*/SKILL.md
Guard: N/A (convention-level)
Commit: pending
Status: Locked

### D-155 | 2026-07-28 | Android Emulator integration is host tooling

Decision: Keep Android Emulator provisioning and readiness checks in `scripts/android-emulator.sh` and do not add a new Core layer or alter existing device-facing interfaces.
Rationale: The repository currently has no product APK or composition root. A host-owned boundary lets local and CI workflows validate a real device without coupling Domain, Vision, or Traversal contracts to one application.
Source: openspec:add-android-emulator-integration
Ref: openspec/changes/add-android-emulator-integration/design.md §Decisions 1, 5; scripts/android-emulator.sh
Guard: N/A (host-tooling convention)
Commit: pending
Status: Locked

### D-156 | 2026-07-28 | Visible Android Emulator by default

Decision: `scripts/android-emulator.sh start` launches a visible AVD by default; CI must opt into headless mode with `UNICLAW_EMULATOR_HEADLESS=1`.
Rationale: Local debugging requires a GUI while retaining the same canonical device profile and lifecycle for automation.
Source: openspec:add-android-emulator-integration
Ref: openspec/changes/add-android-emulator-integration/design.md §Decisions 2; docs/testing/android-emulator.md
Guard: N/A (workflow convention)
Commit: pending
Status: Locked

### D-157 | 2026-07-28 | API 35 default x86_64 canonical AVD

Decision: Use `system-images;android-35;default;x86_64` with AVD name `uniclaw-lite-api35` as the documented lightweight Intel-Mac baseline, while allowing `UNICLAW_AVD_NAME` overrides.
Rationale: This no-GMS image is available in the current SDK repository and matches the development host architecture; the profile remains replaceable when an app-specific image requirement is known.
Source: openspec:add-android-emulator-integration
Ref: openspec/changes/add-android-emulator-integration/design.md §Decisions 5; docs/testing/android-emulator.md
Guard: N/A (host baseline)
Commit: pending
Status: Locked

### D-158 | 2026-07-28 | Doctor gates real-device tests on capabilities

Decision: Require boot completion, a non-empty PNG screenshot, and parseable UIAutomator XML before future device integration tests; APK and package checks remain optional.
Rationale: These probes validate the actual capabilities consumed by UniClaw without inventing an app contract that does not yet exist.
Source: openspec:add-android-emulator-integration
Ref: openspec/changes/add-android-emulator-integration/design.md §Decisions 4; scripts/android-emulator.sh
Guard: N/A (host-tooling convention)
Commit: pending
Status: Locked

### D-159 | 2026-08-03 | One engine, two modes — TraversalEngine is the sole device driver

Decision: Both plan mode (Static) and intent mode (DynamicMatch) execute through Core's `TraversalEngine`/`TraversalFSM`. Host assembles engine + hooks + analyzer; the self-contained runner loop (`ScenarioRunnerBase` and subclasses) is deleted. The two modes differ only in plan shape and verification semantics — both are Host concerns, neither is engine logic.
Rationale: Two parallel traversal paths were the root control problem. `ChildrenStrategy` already distinguishes the modes: `Static` = predefined sequential list with unvisited filter (plan mode), `DynamicMatch` = children generated from page analysis via `DynamicChildManager` with DFS + D-74/D-90 (intent mode). Both walk the identical `TraversalFSM` skeleton.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D1
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-160 | 2026-08-03 | Plan mode uses ChildrenStrategy.Static — no IChildSelector

Decision: Plan mode maps to `ChildrenStrategy.Static` + `StaticNodes`; no new `IChildSelector` abstraction. The only new work is expressing a plan as a static node tree.
Rationale: `Static` already means "predefined list, sequential iteration, unvisited filter" — exactly plan-mode semantics. A new `IChildSelector` abstraction would duplicate an existing engine concern.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D2
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-161 | 2026-08-03 | Plan-mode verification is a hook (VerifyHook), not an IVerifier

Decision: Plan-mode expected-change matching lives in `VerifyHook` implementing `ITraversalHook.OnAfterStep`. It reads step before/after page analysis from context, matches against plan JSON's `expected_change`, records pass/fail, and MUST NOT mutate engine state. Intent mode: no-op. On failure, the hook records the failure and may signal Host to stop/pause only as a Host decision.
Rationale: Verification semantics are mode-specific and belong to Host. Injecting a verifier into `ResultVerify` would leak Host semantics into Core or force Core to carry an abstraction with only two Host implementations.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D3
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-162 | 2026-08-03 | Post-hoc analysis via VerificationAnalyzer on ITraceService + journal

Decision: After `engine.RunAsync()` completes, `VerificationAnalyzer` reads `ITraceService` + Host-private `SafetyDecisionJournal` and produces `ScenarioRunOutcome` (success/failure/incomplete + step-level error traceback). No real-time coupling with the engine. Extension path: `ITraceService` may be inherited by `IScenarioTraceService` — Host-side inheritance of a Core read interface, no Core change.
Rationale: CQRS is already separated at the interface level (`ITraceRecorder` writes, `ITraceService` reads). Post-hoc analysis keeps the engine pure and gives complete hindsight.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D4
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-163 | 2026-08-03 | Entry policy executes before the engine (Host composition, not engine change)

Decision: Host runs `IEntryPolicyExecutor.ExecuteAsync` first, verifies the reset page, then starts `engine.RunAsync()`. The engine loop starts at NodeSelect and never calls `_plan.EntryPolicy`. `_plan.EntryApp` remains the fallback root. Zero engine change.
Rationale: The reset is a Host lifecycle concern. Host-side composition is preferred for V1; revisit if pause/resume needs entry inside the engine lifecycle.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D5
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-164 | 2026-08-03 | Safety gate unchanged, now on the engine path via decorated executor

Decision: The engine's `OperationDispatcher` calls through the single `SafeActionExecutor`-decorated `IActionExecutor`. `SafetyContextHook` pushes the per-step `SafetyCandidate` into `SafetyExecutionContext` (AsyncLocal) on `OnBeforeStep`, so `DecideAsync` sees the real candidate instead of the `"unscoped"` fallback. Post-hoc classification of denied actions (`blocked`/`skipped`) comes from the journal via `VerificationAnalyzer`.
Rationale: The safety decision happens transparently inside the decorator — zero engine change. Known gap: `HandleExecuteAsync` ignores the `DispatchAsync` false return; V1 accepts this and classifies post-hoc.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D6
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-165 | 2026-08-03 | Run assets written by RunAssetHook (not the engine)

Decision: `RunAssetStore` stays; per-step artifacts are written by `RunAssetHook` on `OnBeforeStep`/`OnAfterStep`. Because `PageAnalysis` carries no screenshot bytes, the hook calls `AdbScreenCapture` itself for step evidence.
Rationale: Asset bookkeeping migrates from the runner loop to a hook; the hook runs inside the engine's lifecycle so every step is captured.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D7
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-166 | 2026-08-03 | Plans are data, provisioned by Host (not engine code)

Decision: Plan provisioning produces `TraversalPlan`: plan mode from plan JSON (hand-authored or mock-generated) expressed as `ChildrenStrategy.Static` + `StaticNodes`; intent mode from existing `ScenarioPlanCompiler` → `DynamicMatch`; trace-derived plans (future) from Host analysis of a previous run's trace consumed as plan input.
Rationale: "Plans are data, not code." `TraversalPlan` already carries `StaticNodes`; no plan-compiler variant is needed.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D8
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-167 | 2026-08-03 | Supersede D6 — engine is the sole driver

Decision: This change reverses the `host-target-architecture` D6 ("V1 scenario runner is self-contained"). The requirement is superseded; `ScenarioRunnerBase`/`IncrementalScenarioRunner`/`EnumerateScenarioRunner` are deleted; engine is the only driver. When both changes are archived, the conflicting requirement is dropped in favor of `scenario-runner`.
Rationale: D6 was recorded precisely so this reversal is auditable. The requirement lives in a change-local spec, so the supersession is a coordination note, not a canonical delta.
Source: openspec:runner-through-engine
Ref: openspec/changes/runner-through-engine/design.md §D9
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-168 | 2026-08-03 | IAdbSession 3 方法锁定

Decision: `IAdbSession` 仅定义 `CaptureScreenshotAsync`、`ExecuteShellAsync`、`DumpUiHierarchyAsync`，不加 `RunAsync` 泛化方法。
Rationale: 避免 stringly-typed 抽象——引入 `IAdbSession` 就是为了消除消费者自行拼装命令字符串并解析 stdout 的模式。新需求应扩展新方法。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-1
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-169 | 2026-08-03 | SemaphoreSlim(1,1) 串行化

Decision: `AdvancedSharpAdbSession` 使用 `SemaphoreSlim(1,1)` 串行化命令执行，不引入 Channel/队列。
Rationale: ADB 场景命令量低（每 step 2-5 条），AdvancedSharpAdbClient 底层单 Socket，并发命令导致帧交错。串行化开销可忽略。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-2
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-170 | 2026-08-03 | 三级自愈，不无限重试

Decision: `AdvancedSharpAdbSession` 每次命令内嵌三级重试（即时重连 / 500ms + 重启 adb server / 1000ms 最后尝试），3 次全失败抛 `AdbCommandException`。
Rationale: 死循环重连比快速失败更危险——卡死整个 run。快速失败让 FSM 走 Error 路径。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-3
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-171 | 2026-08-03 | ProcessAdbSession 保留为降级方案

Decision: `ProcessAdbSession`（包装 `AdbCommandRunner`）保留，通过 `UNICLAW_ADB_BACKEND` 环境变量切换（默认 `sharp` → `AdvancedSharpAdbSession`，`process` → `ProcessAdbSession`）。
Rationale: CI 环境可能无法安装 NuGet 包；零风险切换——`process` 模式行为与现有完全一致。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-4
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-172 | 2026-08-03 | DumpUiHierarchyAsync 内部合并两步

Decision: `DumpUiHierarchyAsync` 方法内部合并 `uiautomator dump` + `cat` 为一次调用，调用方不关心文件路径。
Rationale: 封装的基本要求——消除消费者需要知道 `RemotePath` 常量并分两次调用的泄漏。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-5
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-173 | 2026-08-03 | AdbCommandException 构造器改携 ShellResult

Decision: `AdbCommandException` 保留，构造器从 `(string, AdbCommandResult)` 改为 `(string, ShellResult)`，`Result` 属性类型同步变更。
Rationale: 最小化消费者变更——现有 `catch (AdbCommandException)` 语句不变。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-6
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-174 | 2026-08-03 | Phase 2 探针测试先行确认包 API 签名

Decision: 实现 `AdvancedSharpAdbSession` 前先写探针集成测试确认 `AdbClient` 构造/连接、`AdbServer.StartServerAsync()`、`ExecuteRemoteCommandAsync`（含 shell exit code 行为）签名。
Rationale: 包文档与社区示例存在偏差，探针消除按未验证签名编码的风险。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-7
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

---

### D-175 | 2026-08-03 | ShellResult.Success 显式定义

Decision: `ShellResult.Success` = 执行未抛异常且（包暴露 shell_v2 exit code 时 == 0；否则 stderr 为空）。
Rationale: adb shell 经典传输不返回进程 exit code（shell_v2 协议才支持），判定必须显式。
Source: openspec:adb-session-upgrade
Ref: openspec/changes/archive/2026-08-03-adb-session-upgrade/design.md D-8
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-176 | 2026-08-03 | Python 生命周期在 RunScenarioAsync 层

Decision: Python vision service 的 StartAsync / DisposeAsync 必须在 RunScenarioAsync 层管理，与 engine.RunAsync() 生命周期对齐；CreateProviders 只负责组装 provider 字典，不启动进程。
Rationale: 进程生命周期应与 engine 对齐——StartAsync 在 engine 之前，DisposeAsync 在 engine 之后（正常或异常退出）。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-1
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-177 | 2026-08-03 | CurrentPageAnalysisAccessor 放 Host 层

Decision: CurrentPageAnalysisAccessor（共享状态持有者，连接 AnalysisWritingDecorator 写端与 VisionScreenStateProvider 读端）必须放在 Host 层，不进入 Core。
Rationale: Core 无需感知此装配胶水；纯 Host 装配关注点，不污染 Core 层抽象。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-2
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-178 | 2026-08-03 | AnalysisWritingDecorator 包装完整 IPageAnalyzer

Decision: AnalysisWritingDecorator 实现 IPageAnalyzer，3 方法全部 delegate 到 inner analyzer，仅 AnalyzeCurrentPageAsync 拦截后写入 accessor.Current。
Rationale: 装饰器模式——透明代理全接口，只增强单个方法；消费者无需感知装饰器存在。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-3
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-179 | 2026-08-03 | 路径 Host 解析、显式传入构造器

Decision: label-mapping.json 和 server.py 的绝对路径由 Host 层一次性解析，显式传入 PythonVisionService 和 LocalVisionProvider 构造器；Python 服务通过 UNICLAW_LABEL_MAPPING 环境变量同步路径。
Rationale: 消除 CWD 依赖——在任意工作目录启动都不应因相对路径失败；显式传参 > 隐式环境变量兜底。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-4
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-180 | 2026-08-03 | 本地模式文本 provider 缺失 fail-fast

Decision: 当 --provider local 时，DEEPSEEK_API_KEY 缺失必须抛出 HostPreparationException，禁止静默跳过文本 provider 后在运行时崩溃。
Rationale: 本地视觉处理截图，但文本推理（decide_next_action、parse_instruction）仍需独立文本 provider；清晰启动错误 > 运行时 NPE。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-5
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-181 | 2026-08-03 | VisionScreenStateProvider 实现 IObservableScreenStateProvider

Decision: VisionScreenStateProvider 实现 IObservableScreenStateProvider（扩展 IScreenStateProvider），通过 PageAnalysis accessor 提供 scroll 状态查询；HostRunServices.ScreenState 类型不降级为 IScreenStateProvider。
Rationale: IObservableScreenStateProvider 表达"可主动查询"语义，非"必须用 UIA"；保持 HostRunServices 类型精度。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-6
Guard: ArchitectureGuardTests.IScreenStateProvider_Has4Methods
Commit: pending
Status: Locked

---

### D-182 | 2026-08-03 | UIA 作为 Vision 冗余侧信道

Decision: VisionScreenStateProvider 的 UIA provider 参数为可选（默认 null），UIA 调用 try/catch 包裹，失败不阻塞 Vision 主路径；HierarchyXml/Fingerprint 不可用时为 null。
Rationale: RunAssetHook 仍可截图取证；UIA 故障不应阻塞遍历——本地模式的设计目标是无 UIA 也能完整运行。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-7
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-183 | 2026-08-03 | 本地模式跳过 ObservationPipeline

Decision: --provider local 时装配链路跳过 ObservationPipeline，PageAnalyzer 直连 LocalVisionProvider；非本地模式保持现有 ObservationPipeline → InvalidatingPageAnalysisCache 链路不变。
Rationale: ObservationPipeline 的核心价值是 UIA→AI 数据富化；本地模式无 UIA 数据可富化，跳过空转减少延迟和复杂度。
Source: openspec:local-vision-host-wiring
Ref: openspec/changes/local-vision-host-wiring/design.md D-8
Guard: 无 (convention-level)
Commit: pending
Status: Locked

---

### D-184 | 2026-08-03 | TraceTool 独立项目，不并入 Host

Decision: `src/UniClaw.TraceTool/` 作为独立 console 项目，引用 UniClaw.Core + UniClaw.Host；Host 不依赖 TraceTool 的 CLI/TUI 依赖（System.CommandLine / Spectre.Console / Terminal.Gui）。
Rationale: Host 是运行期组件，TraceTool 是纯离线分析器；分离避免 Host 程序集膨胀。同 solution 即可，无需独立 repo。
Source: openspec:trace-analyzer
Ref: openspec/changes/trace-analyzer/design.md D1
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-185 | 2026-08-03 | TraceRunLoader 复用 FileTraceStorage 回放，不新写解析器

Decision: TraceRunLoader 使用 FileTraceStorage 读取 trace.jsonl → replay 进 InMemoryTraceStorage → InMemoryTraceService 提供 ITraceQuery；所有 span 查询走 ITraceQuery，子命令不直接碰文件。不调用 SetSession（只读消费者，禁止写入 run 目录）。
Rationale: FileTraceStorage 已实现 record_type 判别 + 坏行跳过 + dedup 语义；InMemoryTraceService 已是测试验证过的查询实现。新增代码约 50 行。
Source: openspec:trace-analyzer
Ref: openspec/changes/trace-analyzer/design.md D2
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-186 | 2026-08-03 | 故障规则复用 Host 分析器产物，TraceTool 新增聚合规则

Decision: `diagnose` 读取 Host 运行期已产出的 result.json（CompletionReason / IssueFingerprints 透传 evidence），离线补检复用 `ErrorLoopAnalyzer`（`new ErrorLoopAnalyzer(null).EvaluateAsync(ITraceQuery)`，null recorder = 纯检测不发射 span）——stuck_in_error_loop / skip_rate_too_high 命中时 cause 覆盖为 `error_loop_stuck`，判定完全委托 Host 分析器（阈值引用公开常量），TraceTool 仅做聚合（ai_call_failures 分组、时间线空洞、error_loop evidence/failingStep 定位）。TUI 与 CLI 共享同一 DiagnoseEngine，避免双结论源。
Rationale: Host 分析器（CompletionMonitor / ErrorLoopAnalyzer / VerificationAnalyzer）已在运行期产出诊断结果进 result.json；不重复推断，避免双维护与结论分叉。
Source: openspec:trace-analyzer
Ref: openspec/changes/trace-analyzer/design.md D3
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-187 | 2026-08-03 | JSON 契约优先——全部命令 `--format json`，stdout 纯 JSON

Decision: `--format json` 输出稳定 schema（含 schemaVersion = "1"），日志/警告走 stderr；非 TTY 自动去装饰；evidence 上限默认 5 条。
Rationale: agent 消费优先；schemaVersion 让未来演进可检测；stderr 分离确保 stdout 可被 `jq` / 脚本直接解析。
Source: openspec:trace-analyzer
Ref: openspec/changes/trace-analyzer/design.md D4
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-188 | 2026-08-03 | 退出码契约 0/1/2/3

Decision: 0 = 成功；1 = diff 检测到差异（回归信号）；2 = 用法错误 / run 不存在；3 = 空 trace（无 span）。
Rationale: 脚本 `if ! uni-claw trace diff ...; then` 可直接做回归判定。
Source: openspec:trace-analyzer
Ref: openspec/changes/trace-analyzer/design.md D5
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-189 | 2026-08-03 | 元数据进 manifest，不另写文件

Decision: Purpose / TaskId / RunSystemInfo / RunMachineInfo 扩展 RunManifestInput + RunManifest（全部 optional，default null）；RunSystemInfo 用 ADB getprop（模拟器模式，失败返回 null），RunMachineInfo 用 RuntimeInformation + MachineName（常采集）。schemaVersion 保持 "1"。
Rationale: manifest 已是 run 身份文档、已过 AssetRedactor 脱敏管线；避免 emulator-info.json 双源。
Source: openspec:trace-analyzer
Ref: openspec/changes/trace-analyzer/design.md D6
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-196 | 2026-08-03 | 引擎缺"滑动无效果"检测 — Settings ANR 时静默失败 (Deferred)

Decision: 引擎需检测"滑动后页面指纹未变"（scroll_no_effect / app_not_responding）并以明确原因失败；修复前该场景保持 Deferred。ADB 层无缓存问题，不要往那里找。
Rationale: run 20260803-143933 实测：Settings ANR（dropbox: "Input dispatching timed out ... is not responding. Waited 5539ms/11396ms for MotionEvent" 坐标 x=540 = 引擎 swipe 的 MOVE 事件）→ 触摸事件不被 UI 线程消费 → 列表不动 → 3 次分析 21→21→21（entry.fingerprint 三次全同 -190199113）→ NodeSelect 静默卡 32.6s → engine.run all_visited generic 失败。ADB 本身未卡（action.scroll result=true, adb_ms 2712/1319）。环境诱因：host 资源饥饿（vision 分析 10-14s/次 vs 手动 2-3s；dropbox 系统级 AMS/SystemUI blocked 15-19s）。
Source: finding:D-196 (integration run 20260803-143933)
Ref: artifacts/runs/integration/scenario-locate/20260803-143933/locate-one-item/20260803T143952971Z-ec87ca2e1f7e4a6/trace/…/trace.jsonl; adb shell dumpsys dropbox --print
Guard: 无 (convention-level)
Commit: pending
Status: Deferred · Target: Phase 3

### D-190 | 2026-08-03 | TUI 层薄，逻辑全在 TraceRun 聚合

Decision: Terminal.Gui 仅做展示与键位；数据查询、结论推断全部在 TraceRun / DiagnoseEngine，TUI 与 CLI 共享。TERM=dumb 拒绝启动。
Rationale: TUI 无法自动化测试，薄层使测试面集中在可单测的聚合层。
Source: openspec:trace-analyzer
Ref: openspec/changes/trace-analyzer/design.md D7
Guard: 无 (convention-level)
Commit: pending
Status: Locked

### D-197 | 2026-08-03 | 分析证据落盘 analysis.jsonl（异步）

Decision: 集成 run 场景下，每次页面分析（AnalyzeCurrentPageAsync 成功返回）将精简快照异步追加写入 `{runDirectory}/analysis.jsonl`（append-only JSONL，一行一分析：analyzedAt/itemCount/hasScroll/isEndOfList/isPopup/level1MenuNames/items[].{name,type,x,y,expectedAction}）。实现 = AnalysisWritingDecorator（IPageAnalyzer 装饰器，拦截点与 CurrentPageAnalysisAccessor 更新同一点）委托 StepAssetSink（bounded channel + 后台 writer），run finalize 时随 sink drain；sink/runDirectory 必须同传（构造校验）。非 run 单测场景（无 sink）跳过落盘。
Rationale: 此前集成 run 的 trace 只记 item_count 不记条目名，无法回答"检测到的名字 vs 场景目标名"（matcher/OCR 排查）——实测 16 条分析 0 匹配时无名字证据可查。JSONL 序列化用 DomainJsonOptions.Default（camelCase + 枚举 camelCase 成员名，如 "type":"menuItem"——JsonStringEnumConverter(CamelCase) 不尊重枚举 JsonPropertyName）。
Source: finding:H-6 (run 20260803-152240 7 次分析 match_count=0)
Ref: src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs; tests/UniClaw.Host.Tests/HostServices/AnalysisWritingDecoratorTests.cs (8 tests)
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-198 | 2026-08-04 | OCR 后端切换 RapidOCR（ONNX Runtime）

Decision: local vision server 默认 OCR 后端从 paddleocr 切到 rapidocr（`UNICLAW_OCR_BACKEND=rapidocr` 为默认，环境变量可临时切回对比）。backends.py 新增 RapidOCR 路径：`_get_rapid_ocr`（进程级单例 + 锁，实例线程安全）、`warmup_rapid_ocr`、`run_rapid_ocr_on_crops`（与 run_ocr_on_crops 同接口：复用 ROI padding/executor 池，token 置信度低于 `UNICLAW_OCR_TEXT_SCORE`=0.5 丢弃）、`run_rapid_ocr`（CLI 单图）；server.py lifespan/analyze 按 _OCR_BACKEND 分支。`_OCR_LANG` 语言参数仅 paddleocr 分支生效（RapidOCR 中英文混排原生）。
Rationale: paddleocr 2.10（Python 3.11 环境）每请求内存泄漏（D-4 手动 gc 仅缓兵），长跑服务 OOM 死亡（集成 run 中途 1ms 连接失败 → engine.run 崩溃）；且对英文 UI 质量不稳。实测对比（资产截图 settings-home-api35-full）：RapidOCR 完整读出 "About emulated device"(0.99)/"Search settings"(0.99)/"Security & privacy"(0.98)/"Passwords, passkeys & accounts"(1.00)，无 PaddleOCR 的 "Q Search settings" 前缀噪声与 "About emu ated device" 错拼 → Contains 匹配恢复。内存 ~300-500MB、单图 10-25ms。
Source: finding:D-198 (run 20260803-154429 服务死亡 + OCR 名字质量实测)
Ref: tools/local_vision/backends.py (RapidOCR section); tools/local_vision/server.py (_OCR_BACKEND); memory: local-vision-runtime
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-199 | 2026-08-04 | issues.jsonl 由 TraceRunLoader 聚合进 TraceRun

Decision: TraceRun 新增只读 `Issues` 集合（`IReadOnlyList<RunIssue>`）；TraceRunLoader 加载 run 时检测 `issues.jsonl` 逐行反序列化，坏行跳过、缺失 → 空集合、不 fail 加载；子命令禁止直接读 issues.jsonl——保持"TraceRun 是 run 目录唯一入口"。
Rationale: 与 result/manifest/trace/steps 同构聚合；DiagnoseEngine 只消费聚合层，子命令不绕过聚合（trace-run-aggregate spec 既有要求）。
Source: openspec:trace-issue-evidence
Ref: openspec/changes/trace-issue-evidence/design.md D-1; specs/trace-run-aggregate/spec.md
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-200 | 2026-08-04 | TraceTool 直接复用 Host RunIssue record

Decision: TraceTool 不定义镜像 record，直接复用 `UniClaw.Host.Artifacts.RunIssue`（与 RunManifest/RunResult 同源复用模式）。
Rationale: TraceTool 已引用 UniClaw.Host；镜像 record 存在字段漂移风险，单一类型定义（RunAssets.cs）保证 issues.jsonl 契约双端一致。
Source: openspec:trace-issue-evidence
Ref: openspec/changes/trace-issue-evidence/design.md D-2
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-201 | 2026-08-04 | diagnose issue_fingerprints evidence 由 issues.jsonl 补全

Decision: result.json `issueFingerprints` 为空且 issues.jsonl 有可用指纹时，DiagnoseEngine 追加 `issue_fingerprints` evidence（文本 `issues.jsonl: {fingerprint} — {summary}`，D-192 失败详情内嵌于 summary）；result 指纹非空时不重复（幂等——源头回填落地后 fallback 自动停用）；issues 全无可用指纹 → 不产出空条目。
Rationale: ScenarioRunOutcome.IssueFingerprints 无赋值点 → evidence 恒缺、confidence 恒 low；补全后 confidence 恢复 evidence 驱动（low→medium），verification 类失败真实原因可结构化消费。
Source: openspec:trace-issue-evidence
Ref: openspec/changes/trace-issue-evidence/design.md D-3/D-4; src/UniClaw.TraceTool/DiagnoseEngine.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-202 | 2026-08-04 | ImmutableArray 判空用 IsDefaultOrEmpty，不用 Length 模式匹配

Decision: 对 STJ 反序列化的 `ImmutableArray<string>` 字段（如 RunResult.IssueFingerprints），判空一律 `IsDefaultOrEmpty`——result.json 缺失该字段时 STJ 给 default，`is { Length: > 0 }` 访问 `.Length` 会 NRE。
Rationale: 旧 result.json（字段引入前产物）缺失 issueFingerprints 时 diagnose 崩溃（实测）；IsDefaultOrEmpty 对 `[]` 与缺失字段均安全。
Source: openspec:trace-issue-evidence
Ref: src/UniClaw.TraceTool/DiagnoseEngine.cs; tests/UniClaw.TraceTool.Tests/DiagnoseTests.cs
Guard: 无 (convention-level)
Commit: pending
Status: Fixed

### D-203 | 2026-08-04 | 配置单点真源 + schema 版本化

Decision: 集成测试运行配置收敛到 `tests/UniClaw.Host.Tests/Integration/integration.config.json`，schema `uniclaw.integrationConfig.v1`，加载即校验（fail-fast），非法配置报"缺什么+怎么设"。对齐 label-mapping.json 既有模式（schema 版本 + 构造期校验）。
Rationale: 运行参数此前散落测试代码硬编码与手动 export——漏设 provider env 静默撞云端空响应（实测 3m33s）；config 无 schema 版本则无演进边界。
Source: openspec:integration-test-config (finding:P2.1-P2.5) — 对应 integration-config.md §11 D-202
Ref: tests/UniClaw.Host.Tests/Integration/integration.config.json; IntegrationConfig.cs; IntegrationConfigTests.cs
Guard: 无 (convention-level)
Commit: pending
Status: Implemented

### D-204 | 2026-08-04 | providers 按 id 分块，visionServer 只挂 local

Decision: config `providers` 段按 provider id 分块（每块自己的 model/实现细节）；`visionServer` 只允许挂在 `local` 下（loader 强制校验）。扁平 `"visionServer": {...}` 被拒绝——无法体现归属，视觉服务是 local 专属能力。
Rationale: 视觉服务参数（socket/ocr/yolo/labelMapping）只被 local 分支消费，挂在其他 provider 下是死配置；loader 校验让错误配置在加载期暴露。
Source: openspec:integration-test-config (设计评审 2026-08-04) — 对应 integration-config.md §11 D-203
Ref: IntegrationConfig.cs (visionServer 归属校验); integration.config.json providers 段
Guard: 无 (convention-level)
Commit: pending
Status: Implemented

### D-205 | 2026-08-04 | 优先级 file < env < param

Decision: config 文件值是默认，`UNICLAW_INTEGRATION_PROVIDER/MODEL` env 是 CI per-run 选择器（覆盖不改文件），显式参数最高。`SetEnvIfAbsent` 是唯一 env 注入点（手设/CI 优先）。文件覆盖 env 被拒绝——CI/本地互相污染。
Rationale: per-run 变化（如临时换 provider 对比）不该改共享配置文件；手设 env 必须保持最高优先，测试注入不能覆盖用户显式设置。
Source: openspec:integration-test-config (设计评审 2026-08-04) — 对应 integration-config.md §11 D-204
Ref: IntegrationConfig.cs (ResolveScenario 覆盖链); IntegrationConfigTests.cs env 覆盖用例
Guard: 无 (convention-level)
Commit: pending
Status: Implemented

### D-206 | 2026-08-04 | model 只对消费方必填，config 不带死值

Decision: 云端（sensenova/claude/qwen）model 必填（Host 侧构造参数强制）；local/mock 不消费模型名——可省略；原占位值已删。**覆盖后校验**：env 切到云端而 model 空 → fail-fast。所有 provider 强制 model 被拒绝——local 的 `providers.local.model` 是死值（local 分支忽略 `options.Model`，text 走 `DEEPSEEK_MODEL`）。
Rationale: 死值误导（文档曾推荐它，换 model 以为改 config 生效）；覆盖后校验让 env 切换的配置缺口在装配期暴露而非跑完才炸。
Source: openspec:integration-test-config (finding:P2.10) — 对应 integration-config.md §11 D-205
Ref: IntegrationConfig.cs (RequiresModel); ProviderPreflight.cs
Guard: 无 (convention-level)
Commit: pending
Status: Implemented

### D-207 | 2026-08-04 | 意图推理模型入 config 管辖

Decision: `providers.sensenova.intentModel`（可选，仅 sensenova 可挂）→ 装配期注入 `SENSENOVA_MODEL`（SetEnvIfAbsent）。config 是真源，env 是覆盖通道。维持 env 唯一管辖被拒绝——同一"provider 用哪个模型"双键割裂（P2.7），config 管不到意图推理。
Rationale: sensenova 主链路 model 与意图推理模型双键语义割裂，config 落地后统一管辖；不动 Host（CreateIntentExtractor 仍读 env）。
Source: openspec:integration-test-config (finding:P2.7) — 对应 integration-config.md §11 D-206
Ref: IntegrationConfig.cs (intentModel 归属校验); EmulatorScenarioIntegrationTests.cs ApplyProviderEnv
Guard: 无 (convention-level)
Commit: pending
Status: Implemented

### D-208 | 2026-08-04 | 三层校验链

Decision: `Load()`（文件结构）→ `ResolveScenario()`（实际生效配置）→ `ProviderPreflight.Check()`（运行时前提）。均 fail-fast。单层 Load 校验被拒绝——env 覆盖切云端而 model 空、缺 `DEEPSEEK_API_KEY`、模型文件未下载都是运行时才暴露的错误；装配期预检让失败发生在跑 Host 之前。
Rationale: 三个错误面（文件结构/生效配置/运行前提）各自独立暴露，fail-fast 报"缺什么+怎么设"；用户要求"按实际配置了才加载检查"。
Source: openspec:integration-test-config (用户要求 2026-08-04) — 对应 integration-config.md §11 D-207
Ref: IntegrationConfig.cs (Load→ResolveScenario); ProviderPreflight.cs; ProviderPreflightTests.cs
Guard: 无 (convention-level)
Commit: pending
Status: Implemented

### D-209 | 2026-08-04 | ApplyProviderEnv 留在测试装配层，不进 loader

Decision: env 注入（`ApplyProviderEnv`/`ApplyVisionServerEnv`/`SetEnvIfAbsent`）保持为 `EmulatorScenarioIntegrationTests` 的私有静态助手；loader 是纯解析+校验，不产生副作用。注入逻辑并入 loader 被拒绝——loader 单测需起进程级 env，污染面扩大。
Rationale: loader 职责 = 读配置、出结论；改进程 env 是测试装配动作。分离使 loader 可单测（14 用例全无 env 污染），env 修改集中在一个调用点（RunScenarioAsync），便于审计。
Source: openspec:integration-test-config — 对应 integration-config.md §11 D-210
Ref: EmulatorScenarioIntegrationTests.cs (ApplyProviderEnv/PrintStartupBanner); IntegrationConfigLoader
Guard: 无 (convention-level)
Commit: pending
Status: Implemented

> 注：本批 D-203–D-209 为 integration-test-config 线决策（config 域）。integration-pipeline-issues.md 台账引用的 D-208（deepseek 内部路由键）/D-209（UNICLAW_VISION_MODE 拆分）属另一编号空间，录 log.md 时另行续号。

---

### D-210 | 2026-08-04 | 统一资产管线 = Core 公共实现

Decision: `ITracePipeline`（bounded Channel 256 + 批量 flush 50ms/64 条 + DrainAsync 幂等）在 Core 为唯一公共实现；Host 删除 `StepAssetSink`，只做装配（后端 + 位置 + runId 注入）。
Rationale: 管道是通用机制，公共实现只应在 Core 一份；Host 不写管道代码，只组合装配。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/trace-pipeline/spec.md
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-211 | 2026-08-04 | 资产是 trace 的信息——引用事件 + 字节物理分离

Decision: 截图/分析/证据提交时同步写 `ai.evidence` 引用事件进 trace（trace 是索引），字节经 Core 管道批量异步落盘 `assets/{runId}/`（物理分离存储）。
Rationale: 字节体积/写入形态不适合与事件流共存（同步 append vs 批量异步），但语义同属 trace 信息——trace 是索引，引用 = 主通道。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/trace-pipeline/spec.md §"Asset bytes are trace information"
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-212 | 2026-08-04 | 资产触发点 = 产生点提交（归责原则）

Decision: 资产提交由产生点直接调 `ITracePipeline.Submit`——hook 提截图、decorator 提 analysis、provider 提 vision-evidence。无集中收集器。
Rationale: 产生点即提交点，责任可追溯；避免集中收集器引入时序/生命周期耦合。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/changes/unified-asset-pipeline-trace-validation/design.md §Decisions D-3
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-213 | 2026-08-04 | 文件存储显式版本化 V2

Decision: `RunAssetVocabulary.SchemaVersion` "1"→"2"；V2 布局 = `assets/{runId}/` + `trace/{runId}/`（第一级 runId 分桶对称）；旧工具遇 "2" 明确拒绝（loud error），新工具双解析器 V1/V2 分发；trace.jsonl 行格式与布局版本解耦。
Rationale: 布局变化（steps/、analysis.jsonl 移入 assets/、safety 落盘移除、criteria.json 新增）必然破坏旧读取，必须显式版本声明，不能静默错读。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/run-layout-v2/spec.md
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-214 | 2026-08-04 | 失败计数属事件/日志域，不回写 manifest

Decision: `PipelineStats`（Accepted/Dropped/WriteFailures）DrainAsync 后读 → 扩展 `assets.sink_failure` 汇总 trace 事件；写失败每条经 `IPipelineFailureSink` → issueSink（`asset_write_failed`）。计数属事件域——manifest 是一次性元数据快照，不被计数字段回写破坏。
Rationale: manifest 在 run 开始时 BuildManifest 写（快照语义），回写计数破坏该语义；归因方（verify）本来就读 issues/trace，无需 manifest 计数。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/trace-pipeline/spec.md §"Write failures are observable without touching manifest"
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-215 | 2026-08-04 | IAssetQuery 读窄化视图——分析器不持有写能力

Decision: `IAssetQuery` = 只读分面（Read/Exists，无 Write）；`TraceQueries` = `ITraceEventQuery` + `IAssetQuery` 聚合；`IAssetStore`（全接口含 Write）只暴露给写侧管道与实现者；`FileAssetStore` 同实现双接口，不同消费者见不同分面。
Rationale: 分析器不应持有写能力（ISP，D-6）；同一对象不同分面避免权限泄漏。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/trace-pipeline/spec.md §"Pipeline persists via the IAssetStore interface"
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-216 | 2026-08-04 | 写侧配置各入口自持，边界不混淆

Decision: 测试链路 = `integration.config` `storage` 段（位置复用 `emulator.outputRoot`）→ L1→L3 显式注入；直跑 = CLI env（`UNICLAW_ASSET_BACKEND` / `UNICLAW_OUTPUT` / `UNICLAW_EVIDENCE_STORAGE`）。一个前缀 = 一层，测试链路不经 CLI env 回退。
Rationale: 对齐 integration-config.md §9.3 边界模式；测试上下文不应受宿主机 env 污染。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/integration-test-config/spec.md §"Storage section for asset backend"
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-217 | 2026-08-04 | 读侧 CLI 参数即配置 + run 元数据作装配参考

Decision: TraceTool 读侧：CLI 参数即配置（位置显式必填；后端默认不定死）；run 元数据（manifest.taskId/mode/scenarioId）作装配参考/默认（如 `--task-id` 省略 → manifest.taskId），显式 CLI 参数始终覆盖。装配函数形状保留（将来 `--backend`/`--config` 只换装配源）。
Rationale: MVP 无独立读侧配置文件需求；run 元数据复用 Host 已产出事实，与写侧优先级同构（D-204）。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/trace-based-validation/spec.md §"Read-side assembly uses CLI params with run metadata as reference"
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-218 | 2026-08-04 | 验证移出 Host——run 结束 pending，TraceTool 判定

Decision: Run 结束写 `status="pending_verification"` + 引擎事实 + `criteria.json`（独立快照）；`VerifyEngine` + `LocateOneItemRule`（D-201 语义平移）在 TraceTool 产出最终判定；写回仅 pending（`verify --run` 非 pending 只报告不写回，终态永不覆写）。Host 的 `ScenarioCompletionVerifier` locate 分支删除；enumerate 分支保留（未迁）。
Rationale: criteria 是验证契约快照，独立文件消费侧读取更清晰；写回幂等保护终态；Host 边界 = 跑 + 落盘，判定权移交分析侧。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/trace-based-validation/spec.md
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-219 | 2026-08-04 | Safety 决策删除落盘——trace 覆盖全字段

Decision: `safety-decisions.jsonl` + `steps/{n}/safety-decision.json` 落盘移除；safety 决策全字段已由 `TraceSafetyDecisionSink` 写入 trace `safety.*` 事件（policyId/policyVersion/policyHash/ruleId/reason/pageFingerprint/source/normalizedTarget/pageIdentity/confidence）；manifest 资产清单删除 safetyDecimals 项。若消费者需要新字段，补 trace 字段，不恢复落盘。
Rationale: trace 覆盖全字段 + 零读取方 → 落盘是死产物；删减产物减少 run 目录噪声。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/run-layout-v2/spec.md §"Safety decisions do not persist to files"
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-220 | 2026-08-04 | Watch 盯单 run-id 轮询——不做全量扫描

Decision: `trace watch --run-id <id> --dir <root>`：叶子目录名 == runId 定位（>1 匹配报错），轮询 `pending_verification`（P3 终态 ⇒ 资产完整）→ 自动 verify → 退出码 = verify 的。轮询用 `Task.Delay`（不引 `FileSystemWatcher`）。
Rationale: 盯单 run 是长跑任务的实际需求（CI 内测试等 run 结束）；扫描全部新 run 语义归 `verify --dir`。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/specs/trace-based-validation/spec.md §"verify/watch commands follow a stable contract"
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

### D-221 | 2026-08-04 | Manifest 在 run 开始写入，finalize 只更新 result.json

Decision: `manifest.json` 由 `RunAssetStore.CreateAsync`（BuildManifest）在 run 开始时写入（staging → atomic move）；`FinalizeAsync` 只更新 `result.json`，不触碰 manifest。此为本已存在的事实，本次 change 显式确认并归档。
Rationale: Manifest 是一次性不可变快照（schemaVersion/runId/scenarioId/providerId 等 run 启动时即已知），终态回写破坏快照语义。
Source: openspec:unified-asset-pipeline-trace-validation
Ref: openspec/changes/unified-asset-pipeline-trace-validation/design.md §Decisions D-12
Guard: 无 (convention-level)
Commit: f055305
Status: Implemented

---

### D-222 | 2026-08-04 | 屏幕状态可观察接缝——扩展接口而非锁变更

Decision: Core 新增 `IObservableScreenStateProvider : IScreenStateProvider`，唯一新方法 `RefreshAsync` 返回 Core 提升的 `ScreenStateResult`（sealed record，无 Progress 字段）；锁定的 4 方法不动；`AdbScreenStateResult` 被完整替换。Host 面向接口编程，禁止回退到具体类型。
Rationale: 锁只扩展不破坏（原则 8）；结果类型提升到 Core 切断 Host→Device 具体依赖（C1），Result 中不复制 Progress 以维持 `GetScrollProgress()` 单一来源语义。
Source: openspec:host-target-architecture
Ref: openspec/specs/screen-state-provider/spec.md
Guard: ArchitectureGuardTests.IScreenStateProvider_Has4Methods
Commit: 65e1033
Status: Implemented

---

### D-223 | 2026-08-04 | 配置驱动 UniBrainFactory 组装

Decision: Core `UniBrainFactory` 将 `UniBrainConfig` + 分离凭据对象组装成 `UniBrainService`；Host 只交配置与凭据，禁止手 `new` `PageAnalyzer`/`IModelProvider`（结构性 guard 强制）。
Rationale: AI 能力经 `IUniBrain` 门面配置驱动，mock/replay 与真实链路是同一 Host 的不同配置（原则 2/3/7）；凭据走分离通道保持 `UniBrainConfig` 无凭据不变式（C2 组装接缝）。
Source: openspec:host-target-architecture
Ref: openspec/specs/unibrain-facade/spec.md
Guard: HostArchitectureGuardTests.Host_ConstructsRealModelProvidersOnlyInsideCreateProviders
Commit: 65e1033
Status: Implemented

---

### D-224 | 2026-08-04 | MockModelProvider vision replay（replay-or-fail-fast）

Decision: `MockModelFixture` 能力→条目映射满足 `CompleteVisionAsync`/`CompleteMultimodalAsync`：命中回放 `Mode="vision"`/`"multimodal"`，缺失抛 `DomainValidationException`（替换原 `NotImplementedException` 条款）。
Rationale: 缺失能力在 Core 补，不在 Host 复刻（C2 能力缺口）；回放链路形状成为 UniBrain 内的配置选择，非 Host 自有 provider。
Source: openspec:host-target-architecture
Ref: openspec/specs/model-provider/spec.md
Guard: 无 (convention-level)
Commit: 65e1033
Status: Implemented

---

### D-225 | 2026-08-04 | PageAnalysis shape contract——测试而非散文

Decision: 双观察路径（AI/UIAutomator）在 `Level1Menus`/`Level2Menus`/`Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` 上满足同构契约；`Direction` 回退统一为 `Left`；契约由测试强制（same-fixture 等价断言），"mock 绿 ⇒ 真实路径形状绿"。
Rationale: 散文契约会漂移——C4 正因无测试发生；UIAutomator 路径填充菜单列表字段并弃用 `Direction.Left` 硬编码（resolves C4）。
Source: openspec:host-target-architecture
Ref: openspec/specs/page-analyzer/spec.md
Guard: 无 (convention-level)
Commit: 65e1033
Status: Implemented

---

### D-226 | 2026-08-04 | 注入 IEntryPolicyExecutor + 记录 D6（runner 自持观察循环）

Decision: Host 注入 `IEntryPolicyExecutor`（构造在组合工厂），runner 内不 `new`；V1 runner 自持 observe→plan→gate→execute→verify 循环，不依赖 `TraversalEngine`/`TraversalFSM`；`CreateTraversalEngine` 保留给 `enumerate_first_level` 路径。
Rationale: 直接注入违规（C3）；D6 显式记录使偏离可审计、可逆，未来经引擎路由 runner 时更新 spec。
Source: openspec:host-target-architecture
Ref: openspec/changes/host-target-architecture/design.md §D5/D6
Guard: HostArchitectureGuardTests.Host_ActionExecutor_OnlyViaSafeDecoratorChain
Commit: 65e1033
Status: Implemented

---

### D-227 | 2026-08-04 | 单一装饰 IActionExecutor（guard 强制）

Decision: `HostRunServices` 恰好一个 `IActionExecutor` 属性，即 `SafeActionExecutor → PageInvalidatingActionExecutor → AdbActionExecutor` 装饰链；禁止第二个未装饰实例，恢复/弹窗路径不得绕过安全门。
Rationale: 结构性强制替代约定（deterministic-action-safety §1 要求 recovery/popup 同门）；约定正是 C1–C4 被侵蚀的原因。
Source: openspec:host-target-architecture
Ref: openspec/specs/host-composition-root/spec.md
Guard: HostArchitectureGuardTests.HostRunServices_HasExactlyOneActionExecutorProperty
Commit: 65e1033
Status: Implemented

---

### D-228 | 2026-08-04 | probes 建立在统一 trace 上，无并行诊断系统

Decision: `doctor`/`analyze` 诊断经 `ITraceRecorder` 记录（`FileTraceStorage` 按 `trace/{runId}/` 分桶，V2 布局），新 probe 同路径接入；不建并行输出格式；`AssetSubmission` 字节提交仅用于有字节资产的路径（probe 无资产可提交，语义不适用）。
Rationale: probe 是既有 trace 上的便利层（D7），非并行诊断系统；统一 trace 让 trace-analyzer/TraceTool 可直接定位 doctor 会话。
Source: openspec:host-target-architecture
Ref: openspec/changes/host-target-architecture/design.md §D7
Guard: DoctorTraceTests.Doctor_WritesTraceOnlyUnderOutputRootTrace
Commit: 65e1033
Status: Implemented

---

### D-229 | 2026-08-04 | 映射逻辑在 C#，Python 只返回原始证据

Decision: Python 服务返回 YOLO 标签 + OCR 文本原始证据 JSON；C# `LocalVisionProvider` 经 `label-mapping.json` 映射标签 → AI 类型。
Rationale: `ElementTypeMapper` 是 AI 类型的 C# 单点真源；映射逻辑 xUnit 可测；换车机 YOLO 标签只需改 JSON 无需重新部署 Python。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-1
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-230 | 2026-08-04 | 本地视觉 Provider 独立程序集

Decision: `UniClaw.LocalVisionProvider` 为独立 C# 工程（不在 Core、不在 Device）；`UniBrainFactory` 只接收 Host 装配好的 provider 字典。
Rationale: 对齐既有模式（ClaudeProvider/DeepSeekProvider 均独立程序集）；Core 保持"纯逻辑零 I/O"，HttpClient 属传输层。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-2
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-231 | 2026-08-04 | 本地视觉传输 UDS（Unix）/ TCP（Windows）双模

Decision: macOS/Linux 用 Unix Domain Socket，Windows 用 TCP loopback；`UNICLAW_VISION_SOCK`/`UNICLAW_VISION_PORT` 覆盖默认。
Rationale: UDS 延迟低、无端口冲突；Windows 的 uvicorn 缺 UDS 支持；env 覆盖供 CI/测试定制。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-3
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-232 | 2026-08-04 | ROI 裁剪 OCR + threading.local()

Decision: YOLO 检测后按 bounding box 裁剪区域，仅对裁剪块跑 OCR；ThreadPool + `threading.local()` 每线程独立 PaddleOCR 实例。
Rationale: 全图 OCR 浪费 ~80% 算力；threading.local() 规避 PaddleOCR C++ 线程安全缺陷；C++ 推理释放 GIL 得真实并行（12 检测 ~40ms vs 全图 ~800ms）。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-4
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-233 | 2026-08-04 | Server-Timing 头传耗时，不进 JSON body

Decision: Python 将耗时数据放 W3C `Server-Timing` 响应头，C# 解析后写入 trace 子 span；JSON body 只含视觉证据。
Rationale: Vision API 的职责是"看到了什么"而非"多快"；C# 可选消费耗时而不影响 JSON schema 兼容。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-5
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-234 | 2026-08-04 | 视觉 Provider 优雅失败（Success=false 不抛）

Decision: HTTP 失败或非 2xx 时 `LocalVisionProvider.CompleteVisionAsync` 返回 `ModelResponse` 带 `Success=false`，不抛异常。
Rationale: 与 `AnthropicModelProvider` 一致；`PageAnalyzer` 已有 `MaxAnalyzeAttempts=2` 重试环，抛异常破坏既有重试契约。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-6
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-235 | 2026-08-04 | 保守滚动判定（单帧偏向"可滚动"）

Decision: 单帧滚动检测偏向可滚动：空识别 → `has_scroll: true, is_end_of_list: false`（允许滑动）；列表结束由引擎时序 seen-set 差集确认（`item.Name` 指纹）。
Rationale: 误报"结束"会提前终止遍历；误报"可滚动"只多一次滑动，seen-set 差集可捕获。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-7
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-236 | 2026-08-04 | 滚动确认可配置重试（MaxEmptyScrollRetries）

Decision: `ScrollSwipeConfig.MaxEmptyScrollRetries`（int，默认 1）——连续 N+1 次空差集才确认列表结束；0 恢复立即结束；`VisionScreenStateProvider.GetScrollSwipeConfig()` 可返回。
Rationale: 单次空差集可能是瞬态（加载/动画）；两次连续确认在延迟与误报间平衡，场景可调激进程度。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-8
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-237 | 2026-08-04 | label-mapping.json 为 C#/Python 共享单点真源

Decision: `tools/local_vision/label-mapping.json` 单文件；Python 启动（lifespan）读取、C# 构造时读取（fail-fast 校验）；`UNICLAW_LABEL_MAPPING` 可覆盖路径。
Rationale: 消除双阈值漂移风险——`spatial.edgeThreshold` Python（candidatesNearBottom）与 C#（滚动逻辑）同值保证。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-9
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-238 | 2026-08-04 | Trace 头为协议预留（v1 不发送）

Decision: `X-Uniclaw-Trace-Id`/`X-Uniclaw-Step-Id` 定义进 HTTP 协议；v1 C# 不发送（`IModelProvider.CompleteVisionAsync` 签名无 trace 上下文注入源）；Python 透明透传并在 metadata 回显。
Rationale: 协议先行避免将来破坏性变更；观测链已由 `ObservingModelProvider`/`AICallRecord` 覆盖；头部双方皆可选，非死代码。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-10
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-239 | 2026-08-04 | VisionScreenStateProvider 放 Traversal/，非 UniBrain/

Decision: `VisionScreenStateProvider.cs` 位于 `src/UniClaw.Core/Traversal/`（与 `IScreenStateProvider` 同侧）。
Rationale: UniBrain 目录禁引用 Traversal 类型（SubsystemBoundaryGuard）；实现 `IScreenStateProvider` 必须 `using UniClaw.Core.Traversal`；`PageAnalysis` 在 Domain 无 Guard 冲突。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-11
Guard: SubsystemBoundaryGuardTests.UniBrain_DoesNotReferenceTraversal
Commit: 931e385
Status: Implemented

---

### D-240 | 2026-08-04 | Python 依赖管理（OMP 前置 / gc / lifespan warmup）

Decision: `OMP_NUM_THREADS=4` 在模块顶部、任何 numpy/ultralytics/paddleocr import 之前设置；每请求 `gc.collect()`；FastAPI lifespan 内模型 warmup。
Rationale: OpenMP 线程数在库初始化时冻结；手动 GC 缓解 PaddleOCR 持续负载内存泄漏；warmup 避免首请求超时（Ultralytics 首次加载 5-10s）。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-12
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-241 | 2026-08-04 | OCR 线程池——模块级长驻 executor

Decision: 模块级 `ThreadPoolExecutor` 创建一次，lifespan 启动时以 dummy 任务预热（各线程初始化 threading.local PaddleOCR 实例），请求复用同一 executor。
Rationale: 消除每请求建池开销；thread-local OCR 实例跨请求存活（免重复加载权重）；warmup 保证首真实请求不付实例创建成本。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-13
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-242 | 2026-08-04 | ROI padding 从 label-mapping.json 配置

Decision: `spatial.roiPadding: { x: 0.15, y: 0.10, minPx: 8, maxPx: 64 }`；Python 按 `max(x*box_width, y*box_height, minPx)` 截断至 `maxPx`。
Rationale: 取代硬编码 4px（大屏不足）；与框尺寸成比例；与 C# 共享单一配置点。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-14
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-243 | 2026-08-04 | YOLO 置信度阈值可配置

Decision: `detection.confidence`（默认 0.35）放 label-mapping.json，Python 启动读取；无额外融合阶段过滤。
Rationale: 单阈值单位置，消除散落的 magic number 0.35；不同环境可调敏感度。
Source: openspec:local-vision-provider
Ref: openspec/changes/local-vision-provider/design.md D-15
Guard: 无 (convention-level)
Commit: 931e385
Status: Implemented

---

### D-244 | 2026-08-04 | 父链形态——AsyncLocal 通道，非接口签名参数

Decision: `ai.call` parent 链经静态 `EngineStepSpanContext`（AsyncLocal，实现 `ITraceContextProvider`）——引擎 step scope 开启处 `Set(stepScope.SpanId)`、`EndEngineStepSpan` helper 内 `Reset()`；`PageAnalyzer` 构造注入 `ITraceContextProvider?`（null 保留孤儿行为）；`ai.call` 的 `parentSpanId` 改为运行时表达式 `CurrentSpanId`。
Rationale: apply 修订（2026-08-03 裁决）：原 `TraceCoordinator` 实现走不通（引擎自建 per-engine coordinator 与组合根新建实例互不相通，生产 ai.call 仍孤儿）；AsyncLocal 按 async flow 隔离、多引擎并行安全；4 个调用点零改动（改 `IPageAnalyzer` 签名属宪章级）。
Source: openspec:trace-parent-linkage
Ref: openspec/changes/trace-parent-linkage/design.md D1
Guard: EngineStepSpanContextTests
Commit: a01c48f
Status: Implemented

---

### D-245 | 2026-08-04 | TraceFields 字段目录——静态常量类冻结

Decision: 全部 span 属性键集中为 `TraceFields` 静态常量类（45 键，dotted `layer.field` 命名）；常量值冻结不变（JSONL 持久化与下游消费兼容），仅引用方式变化；目录完整性测试强制全键在册。
Rationale: 单一字段目录是未来 `[TraceSpan]` SourceGen 的校验输入（TSG002）；冻结键名防漂移。
Source: openspec:trace-parent-linkage
Ref: openspec/changes/trace-parent-linkage/design.md D2
Guard: TraceFieldsTests
Commit: a01c48f
Status: Implemented

---

### D-246 | 2026-08-04 | 字段分级——SpanFieldProfile 描述符 + helper 过滤

Decision: 每 spanType 一个 `SpanFieldProfile`（Basic/Extended 键数组）；helper 记录时按 `TraceLevel` 过滤 Extended 键；level 来源 `EntryConfig.TraceLevel`（缺省 Detailed → 现状全量，向后兼容）。
Rationale: 低级别运行时省写字段；缺省 Detailed 与现状行为一致，零迁移成本。
Source: openspec:trace-parent-linkage
Ref: openspec/changes/trace-parent-linkage/design.md D3
Guard: SpanFieldLevelsTests
Commit: a01c48f
Status: Implemented

---

### D-247 | 2026-08-04 | 快照闸门更新（S4 重冻结、新增 S6）

Decision: S4 重冻结（`ai.call` parent 从 null 变为 `engine.step` span id）；新增 S6 完整父链 `engine.run → engine.step → ai.call → ai.analyze`（含重试路径 `ai.retry_count` 断言）；S1-S3/S5 必须 unchanged（键名换常量不改变键值）。
Rationale: 父链语义变化只允许出现在快照差异中；S1-S3/S5 不变保证字段目录迁移无行为变化。
Source: openspec:trace-parent-linkage
Ref: openspec/changes/trace-parent-linkage/design.md D4
Guard: SpanTreeEquivalenceTests
Commit: a01c48f
Status: Implemented

---

### D-248 | 2026-08-04 | TraceSpanScope + BeginSpanAsync extension（Core）

Decision: `TraceSpanScope`（async disposable）+ `ITraceRecorderExtensions.BeginSpanAsync`（recorder 为 null 时无副作用 no-op）；`DisposeAsync` 以 `"ok"` 结束未结束 span；`scope.End(status, attrs)` 显式关闭且双关 no-op。
Rationale: 结束属性几乎都是 span 区域内算出的局部量，scope API 是唯一不改方法形状的捕获机制；helper 放 Core 因 TraversalEngine/PageAnalyzer 在 Core（Host 放置违反 Core→Host 依赖方向）。
Source: openspec:trace-span-helpers
Ref: openspec/changes/trace-span-helpers/design.md D1
Guard: SpanTreeEquivalenceTests
Commit: a01c48f
Status: Implemented

---

### D-249 | 2026-08-04 | RecordEventAsync 替代 5 个 unpaired markers

Decision: `ITraceRecorderExtensions.RecordEventAsync`——开 span 不关闭（`EndTime=null`、`DurationMs==0`），即"时间点事件"模型表达；替代 5 个无配对 marker（entry.observed/ignored/visited/skipped、ai.analyze）；recorder 为 null 时 no-op。
Rationale: 4/5 marker 在循环/条件内触发（逐元素/逐分支），无整方法注解形态可适配；一行 helper 直接表达事件语义。
Source: openspec:trace-span-helpers
Ref: openspec/changes/trace-span-helpers/design.md D2
Guard: RecordEventTests
Commit: a01c48f
Status: Implemented

---

### D-250 | 2026-08-04 | deny-gate 顺序与运行时 spanType 行为保留

Decision: 迁移保留两项行为：deny-gate 顺序（`WaitAsync`/`ExecuteAsync` 仅在 `decision.Allowed` 后开 span，denied 不记 span）；运行时 spanType（`ActionToSpanType` click/scroll/back + null 语义、CompletionMonitor 三元）原样流入 `BeginSpanAsync`，不做方法拆分。
Rationale: scope 接受目录常量运行时 spanType，拆分无收益且会改方法形态、破坏 input/long_press 无 span 行为。
Source: openspec:trace-span-helpers
Ref: openspec/changes/trace-span-helpers/design.md D3
Guard: SpanTreeEquivalenceTests
Commit: a01c48f
Status: Implemented

---

### D-251 | 2026-08-04 | 迁移顺序 M0-M5（SafetyGate 先、引擎最后）

Decision: 迁移分 6 层：M0 helpers 落地 + 基线冻结（S1-S5 快照）；M1 SafetyGate；M2 analyzer spans；M3 CompletionMonitor + PageAnalyzer；M4 状态性 TraversalEngine（同步 coordinator passthrough 保留——Generate 同步 guard 冻结，emit 路径不能 await RecordEventAsync）；M5 验收矩阵（AC1-AC6）。
Rationale: 最难的引擎 span 推迟到 helper 在 Host 侧站点验证后；每步独立 green，span-tree 测试为行为等价 oracle。
Source: openspec:trace-span-helpers
Ref: openspec/changes/trace-span-helpers/design.md D4
Guard: SpanTreeEquivalenceTests
Commit: a01c48f
Status: Implemented

---

### D-222 | 2026-08-04 | span 上下文同步点 = TraceSpanScope（构造 push / Dispose pop），非 SourceGen Emitter

Decision: TraceSpanScope 是全部 span 的唯一生命周期封装——构造（spanId 非 null 时）push 到 EngineStepSpanContext 栈，DisposeAsync pop；CreateNoOp() 不 push。SourceGen 生成代码零变更。
Rationale: 一处改动全 span 覆盖；Emitter 不动则 S1-S6 回归面最小。RecordEventAsync（unpaired marker）不进栈——事件不产生"当前 span 区域"语义。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-1
Guard: SpanTreeEquivalenceTests
Commit: pending
Status: Implemented

---

### D-223 | 2026-08-04 | EngineStepSpanContext 栈化（Push/Pop/栈顶读取），删除 TraversalEngine 显式 Set/Reset

Decision: AsyncLocal<Stack<string?>>（每 flow 独立）；Push(string?)/Pop()；CurrentSpanId => 栈顶（空栈 null）。保留静态单例与 ITraceContextProvider 契约。TraversalEngine 显式 Set/Reset 删除——stepScope 由 TraceSpanScope 自动管理，EndEngineStepSpan 内 DisposeAsync→Pop 替换原 Reset。
Rationale: 嵌套 span（handle_error 内 ai.call）需要保存/恢复语义；单值 Set/Reset 无法表达嵌套。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-2
Guard: EngineStepSpanContextTests
Commit: pending
Status: Implemented

---

### D-224 | 2026-08-04 | ai.call parent 从"仅 engine.step"扩展为"当前最内层 span"

Decision: PageAnalyzer 经 ITraceContextProvider.CurrentSpanId（栈顶）parent ai.call——任何 span 区域内（如 handle_error 内）发起的 AI 调用 parent 到当前最内层 span。
Rationale: 树结构更正确（错误处理中的 AI 调用属于错误处理 span）；代价是 S1-S6 快照需回归确认。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-3
Guard: SpanTreeEquivalenceTests (S1-S6)
Commit: pending
Status: Implemented

---

### D-225 | 2026-08-04 | 可选构造注入 ILogger<T>? = null（NullLogger 缺省）

Decision: 记录日志的类构造注入 ILogger<T>? logger = null，null → NullLogger<T>.Instance；组合根装配真实 logger。不引入静态 accessor / DI 容器。
Rationale: 标准 ILogger 抽象 + 既有测试零波及（默认参数）；控制台项目手写组合根下是最小侵入标准路径。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-4
Guard: CompositionRootAssemblyTests
Commit: pending
Status: Implemented

---

### D-226 | 2026-08-04 | 自写 TraceCorrelatedConsoleProvider/TraceCorrelatedFileProvider，仅依赖 Abstractions

Decision: 两 provider 实现 ILoggerProvider/ILogger，输出格式 [HH:mm:ss.fff] [t={TraceId}] [s={SpanId}] [LVL] {Category}: {message}（Category 取短名 LastSegment）；console 写 stderr、file 写 trace/{runId}/run.log（同契约行格式）；provider 内 lock 串行化；异常堆栈缩进输出（Error/Critical 级）。
Rationale: 输出格式本需自定义（t=/s= 前缀）；避免 Logging.Console 包重依赖；文件 provider 每 run 创建（runId 隔离）。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-5
Guard: TraceCorrelatedConsoleProviderTests
Commit: pending
Status: Implemented

---

### D-227 | 2026-08-04 | 日志不进 trace.jsonl；日志与 trace 靠 id 关联

Decision: trace.jsonl 事件流不变（无 log 事件类型）；日志是文本诊断、trace 是结构化事件，两者靠 TraceId/spanId 交叉关联。
Rationale: TraceFields frozen catalog 不动；信息/物理分离原则。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-6
Guard: TraceFieldsTests (45-key catalog unchanged)
Commit: pending
Status: Implemented

---

### D-228 | 2026-08-04 | UNICLAW_LOG_LEVEL 命名独立

Decision: 级别 env UNICLAW_LOG_LEVEL（合法值 trace|debug|information|warning|error|critical，默认 information），命名独立于 UNICLAW_VISION_MODE/UNICLAW_RUN_MODE 族。
Rationale: P2.8 教训（一变量两义污染已两次）；新 env 独立命名并登记。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-7
Guard: LogLevelConfigTests
Commit: pending
Status: Implemented

---

### D-229 | 2026-08-04 | 日志要存储：trace/{runId}/run.log，分析器可查

Decision: run 目录留档日志文件，地址固定 trace/{runId}/run.log（V2 布局 trace 侧、与 trace.jsonl 同级）；run 入口创建、finally Flush+Close（异常路径也关闭句柄）。
Rationale: stderr 易失；trace-analyzer"运行日志补充取证"无址可查；P3.1 教训。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-8
Guard: TraceCorrelatedFileProviderTests
Commit: pending
Status: Implemented

---

### D-230 | 2026-08-04 | run.log 走旁路直接写（不经 ITracePipeline/FileAssetStore）

Decision: 文件 provider 直接写文件（流式追加），非资产管线产物；布局增补行声明。
Rationale: 流式追加文本与批量 flush 资产语义不同；D-216 写侧各入口自持——logger 自持路径不冲突。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-9
Guard: RunLayoutV2Tests
Commit: pending
Status: Implemented

---

### D-231 | 2026-08-04 | 双 provider（console + file）注册同一 LoggerFactory

Decision: LoggerFactory.Create(builder => SetMinimumLevel(...).AddProvider(console).AddProvider(file))；同一格式契约（分析器同一正则解析）。
Rationale: 微软标准做法；职责单一；级别/格式单点配置。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-10
Guard: CompositionRootAssemblyTests
Commit: pending
Status: Implemented

---

### D-232 | 2026-08-04 | 对外告知 = result.json 新增 RunLogPath 字段

Decision: RunResult 新增 RunLogPath，finalize 写 "runLogPath": "trace/{runId}/run.log"（相对路径，对称 TracePath 先例）；schemaVersion 不 bump（字段级扩展，缺字段读侧回退默认）；V1 run 回退目标不存在 → 分析器得知"无日志"。
Rationale: 分析器读 run 元数据即知日志地址；TracePath 已有同类先例（读侧回退链）。统计类元数据不写——D-214 原则。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-11
Guard: RunResultSerializationTests
Commit: pending
Status: Implemented

---

### D-233 | 2026-08-04 | 读侧解析收敛 RunLayoutV2；写侧配置仅级别

Decision: RunLayoutV2 增加 run.log 布局常量/解析辅助；TraceRunLoader 解析链 result?.RunLogPath ?? "trace/{runId}/run.log"（同 TracePath 回退模式）。写侧配置：级别 UNICLAW_LOG_LEVEL + integration.config.json 新 logging.level（可选，测试装配注入 env；loader 校验合法值枚举 fail-fast）；落盘无开关（布局契约固定）。
Rationale: D-217 读侧 CLI 参数即配置 + 布局单点收敛；D-216 写侧各入口自持；测试可静音/开启 Debug 噪音。
Source: openspec:trace-correlated-logging
Ref: openspec/changes/trace-correlated-logging/design.md D-12
Guard: LogLevelConfigTests
Commit: pending
Status: Implemented

---

### D-234 | 2026-08-05 | IdentityMatches 空值守卫在方法入口

Decision: `LocateOneItemRule.IdentityMatches` 方法入口加 `IsNullOrWhiteSpace(actual) || IsNullOrWhiteSpace(expected)` → `return false`。不在上游 filter 中规避——防御式编程，方法契约不假设输入非空。
Rationale: 上游过滤虽解决本次 bug，但新增调用点忘过滤会重现。方法入口守卫是"契约层"修正，上游过滤是"调用层"规避。
Source: openspec:verify-evidence-chain-fix
Ref: src/UniClaw.TraceTool/LocateOneItemRule.cs
Guard: VerifyEngineTests (空串/空白串/null 3 cases; E2E verify target_page_identity_verified)
Commit: dbf89cb
Status: Implemented

### D-235 | 2026-08-05 | VisualPageAnalyzer 套 AnalysisWritingDecorator

Decision: `CreateRunServices` 中 local provider 路径（`accessor is not null`）对 `VisualPageAnalyzer` 套 `AnalysisWritingDecorator`。不复用显式序列化——decorator 已有的 `SubmitSnapshot` 逻辑（D-197）满足需要。
Rationale: 复用现有序列化逻辑；reset 验证 poll 快照自动写入（附加证据）；D-19x "不走 InvalidatingPageAnalysisCache" 约束完整满足（decorator 不做缓存）。非 local 路径不受影响。
Source: openspec:verify-evidence-chain-fix
Ref: src/UniClaw.Host/Commands/HostCommands.cs
Guard: E2E run 20260805T025227318Z (target_page_identity_verified, 21 analysis.jsonl rows)
Commit: dbf89cb
Status: Implemented

### D-236 | 2026-08-05 | AssetSubmission.Append 显式标志

Decision: `AssetSubmission` 加 `bool Append = false`；`IAssetStore.WriteAsync` 加 `bool append = false`；`FileAssetStore` 按标志分支（Append → `FileMode.Append`+`FileShare.Read`；非 Append → tmp+move）。`AnalysisWritingDecorator.SubmitSnapshot` 传 `append: true`。
Rationale: 显式优于隐式（Append 是提交者意图表达，不属于文件名推断）。默认 `false` 保持所有现有提交行为不变。
Source: openspec:verify-evidence-chain-fix
Ref: src/UniClaw.Core/Observability/AssetSubmission.cs, IAssetStore.cs; src/UniClaw.Host/Artifacts/FileAssetStore.cs
Guard: AssetSubmissionTests (4), FileAssetStoreAppendTests (3)
Commit: dbf89cb
Status: Implemented

### D-237 | 2026-08-05 | UI settle 放在 PageInvalidatingActionExecutor 而非 AdbActionExecutor

Decision: 操作后 UI settle 等待注入 `PageInvalidatingActionExecutor.ExecuteAsync`（操作成功后、`_invalidate()` 之后），而非下沉到 `AdbActionExecutor`。`PageInvalidatingActionExecutor` 是视觉管线专属装饰器，与缓存失效语义相邻；`AdbActionExecutor` 是通用设备层组件，不应持有视觉管线概念。
Rationale: 方案 A（`PageInvalidatingActionExecutor`）与 `_invalidate()` 处于同一语义域，且对 iOS/WinAppDriver 等后端通用（只需一个 settle 实现）。方案 B（`AdbActionExecutor`）要求每个设备后端重复 settle 逻辑，且低层组件不应假设调用方有视觉需求。
Source: openspec:settle-delay-responsibility
Ref: src/UniClaw.Host/Runner/InvalidatingPageAnalysisCache.cs (PageInvalidatingActionExecutor); src/UniClaw.Device/AdbActionExecutor.cs
Guard: 25/25 TraversalEngine 单元测试通过
Commit: TBD
Status: Implemented

### D-238 | 2026-08-05 | 保留引擎 DelayPerStepMs 属性，生产设 0

Decision: `TraversalEngineConfig.DelayPerStepMs` 属性和引擎循环内 `if > 0` 守卫保留不删，生产配置设为 `0`。测试和模拟场景通过自行构造 config 可独立设置延时值。
Rationale: 删除无用代码无实际收益，但保留可避免破坏测试（`TraversalEngineTests.cs` 超时测试 / `TraversalHookTests.cs` 取消测试依赖此属性）并为模拟回归提供回退路径。
Source: openspec:settle-delay-responsibility
Ref: src/UniClaw.Core/Traversal/TraversalEngineConfig.cs; src/UniClaw.Core/Traversal/TraversalEngine.cs
Guard: 25/25 单元测试通过（测试设置自己的 DelayPerStepMs 值不受生产配置影响）
Commit: TBD
Status: Implemented

### D-239 | 2026-08-05 | settle 延迟配置走 L4 env var，不进 integration.config.json

Decision: `UNICLAW_SETTLE_DELAY_MS` 作为 L4 环境变量注册到 `docs/testing/integration-config.md`，不写入 `integration.config.json` 的 `providers.local.visionServer` 段。settle 是执行层时序参数非视觉模型参数，变动频率低（per-device 非 per-run），与 `UNICLAW_OMP_THREADS` 同类。
Rationale: 判责三问：author = `HostCommands.cs`，consumer = `PageInvalidatingActionExecutor`，可变性 = per-device 差异（模拟器 300ms / 真机 100-150ms）非 per-run。不符合 L1（静态配置）或 L2（per-run 覆盖）特征。设为 `0` 可完全关闭 settle。
Source: openspec:settle-delay-responsibility
Ref: docs/testing/integration-config.md (L4 表); docs/prd/2026-08-05-settle-delay-responsibility-prd.md
Guard: 契约已注册到 integration-config.md L4 表
Commit: TBD
Status: Implemented

---

### D-240: TransitionMatrix 职责分离 — 只做 Handler 门

Decision: TraversalFSM.TransitionMatrix 的职责从双重（Handler 门 + 异常路由门）收敛为单一（只做 Handler 门）。移除 3 条死边（Execute→Branch、Branch→PreconditionCheck、FrameComplete→ErrorHandling），22→19 边。Exception routing 走独立降级通道，不经过矩阵。
Rationale: fsm-analyzer 双轨分析（静态矩阵审计 + E2E run 诊断）确认 3 条边均无 handler 生产方。按 D-1 先例（PreconditionCheck→Branch 已因"handler 从不返回"移除）清理。移除后每条剩余边均有至少一个 handler 显式返回。
Source: openspec:fsm-matrix-hardening §2.1
Ref: docs/refactor/2026-08-05-fsm-matrix-hardening-design.md
Guard: TransitionMatrix_DeadEdges_Rejected 测试验证 6 条非法边被 DomainValidationException 拒绝；matrix_from_source.py --diff-docs exit 0
Commit: TBD
Status: Implemented

### D-241: StepAsync 异常路由安全化 — CanTransitionTo 守卫 + 降级链

Decision: StepAsync catch 块从无条件 `nextState = ErrorHandling` 改为 `CanTransitionTo(ErrorHandling)` 守卫。不含 ErrorHandling 出边的 3 个状态按合法目标降级：NodeSelect→Branch、FrameComplete→NodeSelect、ErrorHandling→FrameComplete。降级后可能步数燃烧（FrameComplete handler 不弹栈 → 循环至 max_steps），但优于 DomainValidationException 崩溃。
Rationale: D-240 移除 FrameComplete→ErrorHandling 后，ErrorHandling/NodeSelect/FrameComplete 三个状态的异常路由均非法。单一降级目标 FrameComplete 不够（NodeSelect→FrameComplete 非法，FrameComplete→FrameComplete 自环非法）。**拒绝方案**：矩阵补自环（自环不解决"HandleErrorHandlingAsync 自己崩了怎么办"，会把崩溃换成无限重试）。
Source: openspec:fsm-matrix-hardening §2.2
Ref: docs/refactor/2026-08-05-fsm-matrix-hardening-design.md
Guard: ErrorHandling_InternalException_SafeDegradeToFrameComplete + NodeSelect 变体测试
Commit: TBD
Status: Implemented

### D-242: ConsecutiveErrors 语义收敛 — "恢复尝试次数"

Decision: ConsecutiveErrors 语义从"出错次数"收敛为"恢复尝试次数"。递增从 4 个调用点收敛到 HandleErrorHandlingAsync 单一调用点（line 592）。移除 StepAsync catch、HandlePreconditionCheckAsync、HandleExecuteAsync catch 三处的 IncrementConsecutiveErrors 调用。所有错误路由路径（异常路由 / handler 显式返回 / PopupHandling 失败）一致 +1/周期。门限 ≥3 = 精确 3 次恢复尝试后 PressBack。
Rationale: 原实现路径间不一致（异常路径 +2、PopupHandling 路径 +1），门限实际不到 3 次。计数器应反映"在同一棵子树里执行了几次恢复尝试"而非"出了几次错"——增量应在恢复尝试完成时（HandleErrorHandlingAsync 出口）。
Source: openspec:fsm-matrix-hardening §2.3
Ref: docs/refactor/2026-08-05-fsm-matrix-hardening-design.md
Guard: ErrorHandling_FullCycle_ConsecutiveErrorsIncrementsOnce 测试 + 现有 ErrorHandling_ThreeBacktracks 仍通过
Commit: TBD
Status: Implemented

### D-243: LastError 生命周期 — 处置完毕清零

Decision: LastError 在 HandleErrorHandlingAsync 完成后清零。在全部 3 条返回路径（主返回 / page-item 门限 PressBack→FrameComplete / consecutive 门限 PressBack→FrameComplete）前加 `ctx.SetLastError(null)`。NoStepContext stub 路径（line 513-514）不清零——未执行实际错误处置。
Rationale: LastError 被 3 处设置（StepAsync catch / HandlePreconditionCheckAsync / HandleExecuteAsync catch）但从不清理。成功恢复后残留值误导后续 ErrorClassifier + popup restore 复活类型退化异常。清除点在 HandleErrorHandlingAsync 是唯一必经之路，且下游 handler 均不读 LastError。
Source: openspec:fsm-matrix-hardening §2.4
Ref: docs/refactor/2026-08-05-fsm-matrix-hardening-design.md
Guard: ErrorHandling_SuccessfulRecovery_ClearsLastError 测试（3 子用例全覆盖）
Commit: TBD
Status: Implemented

### D-244: PopupHandling 失败 — 补全错误上下文

Decision: HandlePopupHandlingAsync 弹窗 dismiss 失败时设置 `InvalidOperationException("Popup dismiss failed: dismiss_action=<action>")`（有 Classification）或 `"Popup dismiss failed: action=<action>"`（无 Classification）。消息不含 PopupType / DismissStrategy 枚举名。
Rationale: ErrorClassifier（ErrorHandler.cs:13-48）是大小写不敏感 substring 匹配——消息含 `"Permission"` → 误分类为 ErrorType.Permission，含 `"Timeout"` → 误分类为 ErrorType.Timeout。不含枚举名 → 统一归类 Unknown → 走通用恢复策略。后续若需精确分类应在 ErrorClassifier 加 `"popup dismiss failed"` 模式匹配。
Source: openspec:fsm-matrix-hardening §2.5
Ref: docs/refactor/2026-08-05-fsm-matrix-hardening-design.md
Guard: PopupHandling_Failure_SetsLastError 测试（含无 Classification 变体 + 6 枚举名碰撞断言）
Commit: TBD
Status: Implemented
