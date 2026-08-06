# AI Coding Charter — UniClaw.Core 可执行宪章体系规格书

> **版本**: v1.0
> **日期**: 2026-07-04
> **分支**: `feature/refactor` (Phase 2.1 修复阶段)
> **状态**: Specification — 待实施

---

## 0. 动机与问题诊断

### 传统横切文档的致命缺陷（在本项目中已显现）

当前 `docs/system/` 7 篇文档按「分析维度」横切——每篇覆盖所有类型从一个角度：
- 01 拓扑 × 24 类型、02 数据流 × 24 类型、03 语义 × 24 类型 …… 07 序列化 × 24 类型

这种组织在 AI 编码时代暴露三个结构性缺陷：

| 缺陷 | 表现 | 本项目实例 |
|------|------|----------|
| **上下文污染** | AI 必须消化数十页才能找到一条规则 | TypeHint 火山级锁定散在 03/04/05 三篇里，AI 要读 3 篇才能拼出全图 |
| **规则无强制力** | 文档只"建议"，AI 或开发者可能因疏忽违规 | H-1 DynamicMatch 混入 TraversalState 就是因为无机器验证 |
| **无自然更新触发** | 改 1 个类型要更新 7 篇，映射不明确 | 当前 7 篇全部滞留在 Phase 1，Phase 2+ ~80 类型零覆盖 |

### 解决方案：从横切文档到纵切可执行宪章

**横切 → 纵切的跃迁**：按稳定性层级纵向分层，而不是按分析角度横向铺开。

```
横切（当前）：  所有类型 × 7 角度 = 7×N 矩阵 → 改 1 类型更新 7 文件
纵切（目标）：  4 稳定性层 × 各层关注点 → 改 Domain 代码只改 1 文件
```

**文档 → 规范的跃迁**：将 constitution 规则代码化为 CI 强制测试，文档从「规则声明」升级为「规则的解释性文档」。

**本项目的已有基础**：`ArchitectureGuardTests.cs` 已包含 10 enum guard + 1 dependency direction guard，说明宪章代码化的基础设施已经存在。

---

## 1. 宪章架构：四层纵切 + 闭环执行

### 1.1 四层结构

```
┌──────────────────────────────────────────────────────────────────┐
│  Tier 1 · Constitution (宪法)                                   │
│  角色: 不可变的 hard constraints — AI 的 System Prompt           │
│  代码化出口: ConstitutionGuardTests.cs (阻断性 CI)                │
│  文件: constitution/constraints.md / locked-enums.md             │
│        constitution/prohibited-patterns.md                        │
│  更新触发: 新增 locked enum / 发现新 constraint / 架构重构       │
│  更新频率: ≈ 每个 Phase 1 次                                     │
├──────────────────────────────────────────────────────────────────┤
│  Tier 2 · Patterns (模式手册)                                   │
│  角色: 经提炼的可复用设计模式 — AI 的代码骨架                     │
│  代码化出口: 代码中显式引用模式名称 + fsm-design 可用矩阵测试验证 │
│  文件: patterns/fsm-design.md / patterns/handler-pipeline.md     │
│        patterns/readonly-isolation.md / patterns/dispatch-table.md│
│  更新触发: 新增 handler 类型 / FSM 迁移规则变更 / 发现新 pattern │
│  更新频率: ≈ 每个 Phase 1-2 次                                   │
├──────────────────────────────────────────────────────────────────┤
│  Tier 3 · Layers (层规格书)                                     │
│  角色: 具体层的类型清单与依赖关系 — AI 的当前工作区蓝图           │
│  代码化出口: DocSyncTests.cs (提醒性 CI, 不阻断)                  │
│  文件: layers/domain.md / layers/graph.md                        │
│        layers/state-machine.md / layers/traversal.md              │
│  更新触发: 该层新增类型 / 修改接口 / 改校验规则 / 改序列化行为   │
│  更新频率: ≈ 每周 (随代码改动)                                   │
├──────────────────────────────────────────────────────────────────┤
│  Tier 4 · Decisions (决策日志)                                  │
│  角色: append-only 决策记录 — AI 的长期记忆                      │
│  代码化出口: Guard 字段链接到实际测试用例                         │
│  文件: decisions/log.md                                          │
│  更新触发: OpenSpec archive / 审计报告完成 / 直接 commit 小 fix  │
│  更新频率: ≈ 每次 design decision 后                             │
│  关键属性: 只追加不改旧 (append-only)                            │
└──────────────────────────────────────────────────────────────────┘
```

### 1.2 单向引用链

层间引用严格单向，不循环：

```
Constitution ←── Patterns ←── Layers ←── Decisions
(被引用)        (被引用)      (被引用)    (不被引用)

例:
  layers/state-machine.md 写:
    "PopupHandler 采用 handler-pipeline 模式 (→ patterns/handler-pipeline.md)"
    "服从 C-4 FSM 独立性原则 (→ constitution/constraints.md)"
    "H-8 修复记录 (→ decisions/log.md D-8)"
```

### 1.3 闭环执行系统

四层结构与代码化验证、AI 路由、OpenSpec 流程形成闭环：

```
                    ┌─────────────────────────────┐
                    │   Constitution (Tier 1)     │
                    │   解释性文档: 为什么          │
                    └──────────┬──────────────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
              ▼                ▼                ▼
    ConstitutionGuard    DocSyncTests      CLAUDE.md Routing
    (阻断性 CI)          (提醒性 CI)      (AI 入口路由)
    "规则被遵守了吗"      "文档滞后了吗"    "AI 该读哪些文档"
              │                │                │
    ┌─────────┴────────────────┴────────────────┴──────────┐
    │                    代码修改                             │
    │  AI → 读 routing → 读 constitution → 写代码 → CI 验证  │
    └──────────────────────────────────────────────┬────────┘
                                                   │
                                                   ▼
                                    ┌──────────────────────┐
                                    │  Decisions (Tier 4)  │
                                    │  累积: 决策索引       │
                                    │  来源: OpenSpec /     │
                                    │  Finding / direct     │
                                    └──────────────────────┘
```

---

## 2. Tier 1: Constitution — 具体规则与代码化映射

### 2.1 constraints.md — 全项目 hard constraint 清单

按「违反后果严重度」排序，每条标注对应的 Guard test：

```markdown
## C-1: TraversalState 值锁定 = 8 [火山级]
违反后果: FSM 迁移矩阵失效, DynamicMatch 混入已修复 (H-1)
影响范围: TraversalFSM, StepOrchestrator, all handler tests
Guard: EnumValueGuardTests.TraversalState_Has8Values
决策记录: → decisions/log.md D-5

## C-2: TypeHint 值锁定 = 8 [火山级]
违反后果: 4 域 cascade (AliasMap, IsInteractive, IsVisualOnly, TypeStringToTypeHintMap)
影响范围: Domain.Vision + Domain.Content + Domain.Mappings + DynamicMatcher
Guard: 待新增 (当前仅在 Domain 测试中覆盖)
决策记录: → decisions/log.md D-1

## C-3: Domain 三岛零互 import [架构级]
规则: Domain.Vision ↔ Domain.Content ↔ Domain.Common 零直接 import
唯一桥: Mappings (ElementTypeMapper)
违反后果: 跨域语义泄漏, 两级映射分离原则被破坏
Guard: 待新增 (ArchUnitNET / 反射测试)
决策记录: → decisions/log.md D-2

## C-4: FSM 独立性原则 [架构级]
规则: TraversalFSM 和 GlobalFSM 不得共享 state/transition/callback
协调仅通过 ITraversalContext.GlobalState
已知偏差: M-14 (GlobalState 在 ITraversalContext 上, Phase 3 待修)
违反后果: FSM 状态空间交叉, 调试困难
Guard: 待新增 (类型依赖检查)
决策记录: → decisions/log.md D-7

## C-5: Graph→StateMachine 单向依赖 [架构级]
规则: Graph → StateMachine (using), 禁止反向
已修复: H-5 (ITraversalNode 已移到 Graph 层)
违反后果: 双向依赖导致循环, 无法独立演进
Guard: DependencyDirectionGuardTests.TraversalNode_DoesNotReferenceStateMachineNamespace
       DependencyDirectionGuardTests.ITraversalNode_ResidesInGraphModelsNamespace
       DependencyDirectionGuardTests.TraversalState_DoesNotContainITraversalNodeOrIStackFrame
决策记录: → decisions/log.md D-6

## C-6: ReadOnlySetWrapper cast-back 阻断 [安全级]
规则: VisitedChildren 不得通过 cast-back 修改引擎内部数据
违反后果: AI advisor 或外部 consumer 可篡改引擎状态
Guard: VisitedChildrenIsolationTests.VisitedChildren_CastBackToHashSet_ThrowsInvalidCastException
决策记录: → decisions/log.md D-9

## C-7: GlobalState 值锁定 = 8 [火山级]
违反后果: GlobalFSM 迁移矩阵失效
影响范围: GlobalFSM, StepOrchestrator, TraversalFSM (通过 Context)
Guard: EnumValueGuardTests.GlobalState_Has8Values
决策记录: → decisions/log.md D-3

## C-8: SelectionState 值锁定 = 3 [火山级]
违反后果: 2 域 cascade (SelectionStateExtensions, FlattenedElement.IsInteractive)
影响范围: Domain.Vision + Domain.Content (通过 FlattenedElement)
Guard: 待新增 (当前在 Domain 测试中覆盖)
决策记录: → decisions/log.md D-4
```

### 2.2 locked-enums.md — 10 enum 值锁定 + cascade 影响图

所有 10 个 Phase 2.1 锁定 enum，标注值数、cascade 影响路径和 guard test：

| Enum | Namespace | 值数 | 级别 | Cascade 影响 | Guard Test |
|------|-----------|------|------|-------------|-----------|
| `TraversalState` | StateMachine | 8 | 火山 | FSM matrix, StepOrchestrator, handlers | `TraversalState_Has8Values` |
| `GlobalState` | StateMachine | 8 | 火山 | GlobalFSM matrix, ITraversalContext | `GlobalState_Has8Values` |
| `NodeType` | Graph.Models | 8 | 火山 | DynamicMatcher, PlanCompiler | `NodeType_Has8Values` |
| `ErrorType` | StateMachine | 6 | 丘陵 | ErrorClassifier, ErrorStrategySelector | `ErrorType_Has6Values` |
| `ErrorStrategy` | StateMachine | 5 | 丘陵 | RecoveryExecutor, backoff calculation | `ErrorStrategy_Has5Values` |
| `PopupType` | StateMachine | 6 | 丘陵 | PopupDetector, PopupClassifier, dispatch (incl. Anr) | `PopupType_Has6Values` |
| `DismissStrategy` | StateMachine | 4 | 丘陵 | PopupClassifier, PopupActionExecutor | `DismissStrategy_Has4Values` |
| `UrgencyLevel` | StateMachine | 3 | 平原 | PopupClassifier urgency (1 dependency) | `UrgencyLevel_Has3Values` |
| `BlockingType` | StateMachine | 3 | 平原 | PopupClassifier (1 dependency) | `BlockingType_Has3Values` |
| `FallbackAction` | Graph.Models | 4 | 丘陵 | ExitCondition, ContainerActionExecutor | `FallbackAction_Has4Values` |

**Phase 1 Domain enum (不在 Phase 2.1 guard 中，但有 Domain 级约束)**：

| Enum | Namespace | 值数 | 级别 | Cascade 影响 |
|------|-----------|------|------|-------------|
| `TypeHint` | Domain.Vision | 8 | 火山 | AliasMap, IsInteractive, IsVisualOnly, TypeStringToTypeHintMap |
| `SelectionState` | Domain.Vision | 3 | 火山 | SelectedAliases, DisabledAliases, IsInteractive |
| `MenuItemType` | Domain.Content | 11 | 丘陵 | TYPE_TO_MENU_ITEM, ExpectedAction mapping |
| `ExpectedAction` | Domain.Content | 4 | 丘陵 | TYPE_TO_EXPECTED_ACTION |
| `Direction` | Domain.Content | 4 | 丘陵 | Navigation logic |
| `RegionRole` | Domain.Vision | 5 | 丘陵 | Region 验证 |

**扩展规则**：任何 Hilly 级 enum 扩展值时，**必须先更新 mapping table，再加 enum 值**。

### 2.3 prohibited-patterns.md — 禁止模式清单

| 禁止模式 | 原因 | 替代方案 | 检查方式 |
|---------|------|---------|---------|
| `ToDictionary()` / `FromDictionary()` | PRD §4.4 明确禁止，语义压缩丢失 | JSON 序列化 (`DomainJsonOptions`) | grep test (Phase 2.2+) |
| 视觉外观 + 行为语义混在一个类型 | 两级映射分离原则 (P0 fix) | TypeHint 只回答"看起来像什么"; 行为用 MenuItemType/ExpectedAction | 代码审查 |
| `ITraversalContext` 上暴露 mutation 方法 | 只读接口原则 (D-4) | mutation 方法在 `TraversalRuntimeContext` class 上 | 反射测试 (已实现) |
| `HashSet<string>` 直接暴露为 `IReadOnlySet<string>` | cast-back 安全 (H-2) | `ReadOnlySetWrapper` private sealed class | 运行时测试 (已实现) |
| non-sealed record class | 不可变设计约定 | `sealed record class` | grep test (Phase 2.2+) / Roslyn Analyzer (Phase 3) |
| `ValueError` / `InvalidOperationException` for domain validation | 统一校验异常约定 | `DomainValidationException` | grep test |
| TraversalFSM 引用 GlobalFSM 的 state/transition/callback | FSM 独立性 (C-4) | 协调仅通过 `ITraversalContext.GlobalState` | ArchUnitNET (Phase 2.2+) |

---

## 3. Tier 2: Patterns — 设计模式手册

### 3.1 fsm-design.md — 双 FSM 架构

**TraversalFSM (微观 — 8 状态)**：

| 状态 | 可迁至 | 条件 |
|------|--------|------|
| NodeSelect | PreconditionCheck | 有 stack + 有 unvisited children |
| NodeSelect | Branch | 空 stack 或无 unvisited children |
| PreconditionCheck | Execute | precondition pass |
| PreconditionCheck | Branch | precondition fail (D-1 已移除到 Branch) |
| Execute | ResultVerify | action executed |
| ResultVerify | FrameComplete | result verified |
| ResultVerify | ErrorHandling | verification fail |
| Branch | NodeSelect | new node selected |
| FrameComplete | NodeSelect | next frame |
| ErrorHandling | NodeSelect | recovery success |
| ErrorHandling | PopupHandling | popup detected during error |
| PopupHandling | NodeSelect | popup dismissed |

**关键规则**：
- 无自环 (self-loop)
- D-1 修正: PreconditionCheck→Branch 已移除（precondition fail → 回到 Branch）
- DynamicMatch 不属于此矩阵（它是 ChildrenStrategyType 值）

**GlobalFSM (宏观 — 8 状态)**：

| 状态 | 可迁至 | 终态? |
|------|--------|-------|
| Idle | Initializing | No |
| Initializing | Traversing, Error | No |
| Traversing | Paused, Error, Completed | No |
| Paused | Traversing, Terminated | No |
| Error | Recovering | No |
| Recovering | Initializing | No |
| Completed | — | **Yes** |
| Terminated | — | **Yes** |

**独立性原则 (→ constitution C-4)**：
- TraversalFSM 和 GlobalFSM 不得共享 state/transition/callback
- 协调仅通过 `ITraversalContext.GlobalState`
- 已知偏差: M-14 (GlobalState setter 在 ITraversalContext 上)

### 3.2 handler-pipeline.md — 通用 Handler 管道

**通用管道**: detect → classify → decide → execute → statistics

**三个 Handler 的差异**：

| Handler | 输入 | 分类器 | Dispatch hooks | Fallback |
|---------|------|--------|---------------|---------|
| PopupHandler | screen text | PopupDetector (5 type regex) → PopupClassifier (6 sub-methods) | 6 PopupType hooks | back (H-8 top-level try-catch) |
| ContainerHandler | CompletionResult | CompletionDetector (5-priority chain) → FallbackDecider (priority chain) | 4 FallbackAction hooks | BACK (exception fallback) |
| ErrorHandler | exception | ErrorClassifier (7-priority chain) → ErrorStrategySelector (applicability-based) | 5 RecoveryHook | abort (exception fallback) |

**每个 Handler 都遵循**：
- Dispatch Table 模式 (→ patterns/dispatch-table.md)
- Log-and-Continue 模式 (异常不阻断主遍历流)
- Statistics tracking

### 3.3 readonly-isolation.md — 集合安全隔离

**三级集合暴露安全**：

| 安全等级 | 模式 | 适用 | cast-back 阻断? |
|---------|------|------|-----------------|
| **Level 3 (最强)** | `ReadOnlySetWrapper` private sealed | VisitedChildren (嵌套集合) | ✅ `(HashSet<string>)wrapper` → InvalidCast |
| **Level 2 (中)** | `IReadOnlySet<string>` / `IReadOnlyList<string>` | VisitedPages, VisitedNodes, CurrentPath | ⚠️ cast-back 技术可行但接口不暴露 mutation |
| **Level 1 (弱)** | 直接 HashSet 暴露 | 内部引擎自用 (注释标注安全等级) | ❌ cast-back 可修改 |

**TraversalContextSnapshot (AI advisor 专用)**: 8 immutable fields, `ImmutableHashSet`, 完全隔离于 source context。

### 3.4 dispatch-table.md — Hook Dispatch + Fallback Chain

**模式**:
```
1. 查找 dispatch table: key → hook function
2. 执行 hook
3. hook 抛异常 → fallback chain 终端行为 (back / abort / continue)
4. hook 返回 → 正常结果
```

**本项目中的实例**：
- PopupActionExecutor: 6 PopupType hooks → exception fallback to back
- ContainerActionExecutor: 4 FallbackAction hooks → exception fallback to BACK
- RecoveryExecutor: 5 ErrorStrategy hooks → exception fallback to abort
- GlobalFSM callback: state → callback → exception not propagated (Log-and-Continue)

---

## 4. Tier 3: Layers — 层规格书

### 4.1 layers/domain.md — Domain 层 (Phase 1 完成)

**类型清单** (24 + 2 跨切面):
- Vision (8): BoundingBox, Region, RegionRole, TypeHint+Ext, SelectionState+Ext, FlattenedElement, FlattenedScreen, ScreenHints
- Content (10): Coordinate, Direction+Ext, MenuItemType+Ext, ExpectedAction+Ext, MenuInfo, MenuItem, PopupInfo, PageAnalysis, VisitFingerprint, ContentNode
- Common (5): OperationType, Operation, TargetType, Target, RestoreAction
- Mappings (2): ElementTypeMapper, AndroidWidgetClass
- Cross-cutting (2): DomainValidationException, DomainJsonOptions

**依赖拓扑** (→ constitution C-3): 三岛零互 import, Mappings 是唯一桥

**稳定性评级** (→ constitution locked-enums):
- 火山: TypeHint (8), SelectionState (3)
- 丘陵: MenuItemType (11), ExpectedAction (4), OperationType (5), Direction (4), RegionRole (5)
- 平原: BoundingBox, Coordinate, FlattenedScreen, MenuInfo, PopupInfo, PageAnalysis 等
- 独立: Target, RestoreAction, AndroidWidgetClass (0 references)

**校验策略**: fail-fast (构造期 DVE) vs graceful (FromString/MapAndroidClass fallback + IsValid)

**序列化约定**: DomainJsonOptions (camelCase + enum-as-string + null-skip)
已知问题: TypeHint 缺 `[JsonPropertyName]` (P3)

**P3 补齐清单**:
1. ContentNode.ToMarkdown()
2. Region.Id 非空校验
3. TypeHint 加 [JsonPropertyName]
4. TypeHint Values → IReadOnlyList<string>
5. 补 IsCanonical(string)

### 4.2 layers/graph.md — Graph 层 (Phase 2)

**核心类型**:
- TraversalPlan (12 fields), TraversalNode (ITraversalNode impl)
- MatchCondition (5 维 conjunctive + TextMatchMode), ChildrenStrategy (3 types)
- PlanCompiler (4 TEMPLATE_SETS, 6-step compile)
- DynamicMatcher (5 维匹配, MatchableItem→MatchResult)
- TemplateInstantiator (7-step instantiate)
- EntryConfig, NodeData, Template

**枚举** (13): TextMatchMode, ChildrenStrategyType, MatchAction, ErrorPolicyType, ExitConditionType, FallbackAction, EntryStrategy, CompletionPolicyType, MatchMode, TargetFoundAction, TraversalMode, WaitMode, TraceLevel

### 4.3 layers/state-machine.md — StateMachine 层 (Phase 2)

**核心类型**:
- GlobalFSM + TraversalFSM (→ patterns/fsm-design.md)
- PopupHandler (→ patterns/handler-pipeline.md): PopupDetector, PopupClassifier, PopupActionExecutor, StateRestorer
- ContainerHandler: CompletionDetector, FallbackDecider, ContainerActionExecutor
- ErrorHandler: ErrorClassifier, ErrorStrategySelector, RecoveryExecutor
- NodeStack (DFS traversal stack, DefaultMaxDepth=10)
- TraversalRuntimeContext (26 mutable fields, → patterns/readonly-isolation.md)

**枚举** (7): TraversalState (8), GlobalState (8), PopupType (6), UrgencyLevel (3), BlockingType (3), DismissStrategy (4), ErrorType (6), ErrorStrategy (5), CompletionReason (4), RecoveryOutcome (3)

**接口** (4): IGlobalStateMachine, ITraversalStateMachine, ITraversalContext, INodeStack

### 4.4 layers/traversal.md — Traversal 层 (Phase 2)

**核心类型**:
- StepOrchestrator (14-step interception layer)
- DynamicChildManager (9-step generate pipeline + dedup)
- TraceCoordinator (16+ span methods, Log-and-Continue, active gate)
- EntryPolicyExecutor (3 strategies + BIND_CURRENT_SCREEN fallback)
- PageCacheManager, PageSnapshotManager (deterministic fingerprint)
- NodeStackAdapter (wraps NodeStack + INodeRegistry)

---

## 5. Tier 4: Decisions — 与 OpenSpec 的集成

### 5.1 定位：决策的 AI-Coding 索引，不是原始记录

```
decisions/log.md 的角色:

  不是 ── 决策的原始记录（那是 OpenSpec change 的职责）
  不是 ── 独立平行系统（那样会和 OpenSpec 漂移）

  而是 ── 决策的 AI-Coding 索引
         从 OpenSpec change / 审计报告 / 直接 commit 中
         提取对后续编码有约束力的决策摘要
         每条指向原文入口，AI 需要深入时可追溯
```

### 5.2 条目格式

```markdown
### D-{id} | {date} | {title}

Decision: {一句话结论 — AI 需遵守什么}
Rationale: {为什么 — 1-2 句}
Source: openspec:{change-id} | finding:{H/M/D-id} | direct-commit
Ref: {指向原文的路径 — design.md / spec.md / review report}
Guard: {ConstitutionGuardTests test 名} | 无 (convention-level)
Commit: {hash} | pending
Status: Fixed | Locked | Deferred · Target: Phase {n}
```

### 5.3 Source 三路分类

| Source 类型 | 含义 | 生成时机 | 原文入口 |
|------------|------|---------|---------|
| `openspec:{change-id}` | 来自正式 OpenSpec 变更流程 | OpenSpec archive 时 | change 目录下的 proposal.md / design.md |
| `finding:{id}` | 来自内部审计发现 (H/M/D 编号) | 审计报告完成时 | docs/refactor/09-phase2-review-report.md |
| `direct-commit` | 来自直接修 bug，无 OpenSpec 流程 | commit 后手动 append | commit diff |

### 5.4 更新触发映射

| 触发事件 | decisions/log.md 操作 |
|---------|---------------------|
| OpenSpec archive 完成 | 从 change 的 Decisions Extract 聚合条目 · append |
| Phase review report 完成 | 从 Finding ID 生成条目 · append |
| 直接 commit 小 fix | 手动 append (Source: direct-commit) |
| OpenSpec change 决策变更 | 不修改旧条目 · append 新条目标注 superseded-by |
| Constitution 新增 guard | 更新相关条目的 Guard 字段（唯一允许改旧条目的例外） |

### 5.5 OpenSpec change 中新增 Decisions Extract 段

在 proposal.md 或 design.md 末尾增加结构化决策提取段：

```markdown
## Decisions Extract

| ID | Decision | Rationale | Status |
|----|----------|-----------|--------|
| D-5 | DynamicMatch 从 TraversalState 移除 | 是 ChildrenStrategyType 值不是 FSM state | Fixed |
| D-7 | GlobalState 暂留 ITraversalContext | breaking change 范围大 | Deferred·Phase3 |
| D-8 | TextMatchMode 默认 Contains | 向后兼容 | Locked |
```

OpenSpec archive 时，从 Decisions Extract 生成 decisions/log.md 索引条目。

### 5.6 Apply 阶段的四层文档同步责任

OpenSpec `/opsx:apply` 的 Step 7 (Documentation Sync Check) 检查"文档是否需要更新"，
但当前定义过于模糊 — 只说 "check if updates needed"，没有指定哪些文件、什么触发。

**明确规则**：代码改动完成后，按以下映射表强制检查并更新受影响的四层文档：

| 改了什么代码 | Tier 1 (Constitution) | Tier 2 (Patterns) | Tier 3 (Layers) | Tier 4 (Decisions) |
|-------------|----------------------|-------------------|----------------|-------------------|
| Phase 2.1 locked enum 值数变更 | `locked-enums.md` 值数列 + cascade 列; `charter` §2.2 表 + §6.1 guard 表 | 如果改了 Handler 子组件逻辑 → `handler-pipeline.md` 对应行 | `state-machine.md` enum 值数列 | archive 时从 Decisions Extract 生成 |
| Handler 子组件逻辑变更 (如 DismissStrategy 决策方式) | 不改 (enum 值数不变) | `handler-pipeline.md` Decision class/priority 行 | 不改 (enum 值数不变) | archive 时从 Decisions Extract 生成 |
| FSM 迁移矩阵变更 (加/删路径) | 不改 (enum 值数不变) | `fsm-design.md` 迁移表 | `state-machine.md` 状态表 | archive 时从 Decisions Extract 生成 |
| 新增 / 删除 enum 值 (非 locked) | 不改 (非 locked 不进 constitution) | 如果改了 Handler dispatch → `dispatch-table.md` | 对应 `layers/<layer>.md` enum 表 | archive 时从 Decisions Extract 生成 |
| 新增 Guard test (C-级别约束) | `constraints.md` 新增约束条目; `locked-enums.md` 如果锁定新 enum | 不改 | 不改 | archive 时从 Decisions Extract 生成; 旧条目 Guard 字段更新 (§5.4 唯一允许改旧条目的例外) |
| Domain 类型新增/修改 | 不改 (除非新增 locked enum) | 不改 (除非改了集合暴露模式) | `domain.md` 类型清单 | archive 时从 Decisions Extract 生成 |
| 纯决策记录 (无代码改动) | 不改 | 不改 | 不改 | finding/direct-commit → 手动 append |

**执行时机**：
- Tier 1/2/3: 在 **Apply 阶段** (代码改动完成后) 立即更新，作为 Documentation Sync Check 的具体步骤
- Tier 4: 在 **Archive 阶段** 从 Decisions Extract 提取 (不手动写)
- OpenSpec main specs: 在 **Archive 阶段** delta → main sync

**原则**：
- Constitution 先改 → 确保后续 AI 读取正确的约束值
- Patterns/Layers 紧跟代码改 → 确保文档描述当前实现状态
- Decisions 延迟到 archive → 确保条目从正式源头生成，不漂移
- 不在 Propose 阶段改任何四层文档 → Propose 只创建 change-local artifacts

---

## 6. 代码化执行：ConstitutionGuardTests + DocSyncGuardTests 设计

### 6.1 当前状态

`ArchitectureGuardTests.cs` 已包含：

**EnumValueGuardTests** (10 tests — 阻断性):
- TraversalState=8, GlobalState=8, NodeType=8, ErrorType=6, ErrorStrategy=5
- PopupType=6, DismissStrategy=4, UrgencyLevel=3, BlockingType=3, FallbackAction=4

**DependencyDirectionGuardTests** (3 tests — 阻断性):
- C-5a: TraversalNode 不引用 StateMachine namespace
- C-5b: ITraversalNode + IStackFrame 在 Graph.Models namespace
- C-5c: TraversalState.cs 不包含 ITraversalNode/IStackFrame 定义

**DocSyncGuardTests** (4 tests — 阻断性, §5.6 流程强制执行):
- P1a: locked-enums.md 值数列 ↔ Enum.GetValues<T>().Length (12 enum 全量交叉验证)
- P1b: charter §6.1 enum 值数表 ↔ Enum.GetValues<T>().Length (10 enum 交叉验证)
- P1c: locked-enums.md Guard test 名 ↔ ArchitectureGuardTests.cs 实际方法名
- P1d: Guard test Assert.Equal 期望值 ↔ locked-enums.md 值数 ↔ test 名隐含值数 三路一致

### 6.2 需新增的 Guard Tests

| 约束 | Test 类 | 难度 | 优先级 |
|------|---------|------|--------|
| C-2 TypeHint=8 | `EnumValueGuardTests.TypeHint_Has8Values` | 低 | P1 |
| C-8 SelectionState=3 | `EnumValueGuardTests.SelectionState_Has3Values` | 低 | P1 |
| C-3 Domain 三岛零互 import | `NamespaceIsolationGuardTests` (ArchUnitNET 或反射) | 中 | P2 |
| C-4 FSM 独立性 | `FsmIndependenceGuardTests` (类型依赖检查) | 中 | P2 |
| 禁止 ToDictionary | `ProhibitedPatternGuardTests` (grep test) | 低 | P2 |
| Domain 类型数=26 | `DocSyncTests.DomainTypeCount_MatchesDoc` | 低 | P3 |

### 6.3 三层防御体系

```
第一层 (立即): xUnit 反射断言 — enum 值数 + grep 检查
  成本: 极低, 已有基础设施
  拦截: CI build fail

第二层 (Phase 2.2): ArchUnitNET 架构测试 — namespace 依赖 + 类型隔离
  成本: 中 (1 NuGet 包 + 1 测试文件)
  拦截: CI build fail

第三层 (Phase 3): Roslyn Analyzer — IDE 实时红线 (ToDictionary, non-sealed record)
  成本: 高 (自定义 Analyzer 开发)
  拦截: IDE 即时反馈, 优于 CI
  务实策略: 先用 grep test, 模式稳定后再投入 Analyzer
```

### 6.4 DocSyncTests (提醒性, 不阻断)

```csharp
// DocSyncTests.cs — 文档-代码一致性守卫 (warning level, 不阻断构建)

[Fact]
void Domain_Type_Count_Matches_Doc()
{
    // 反射统计 Domain 层导出的 public type 数量
    // layers/domain.md 声明 24 类型 + 2 跨切面 = 26
    // 如果实际数量不匹配 → Assert.Equal 会 fail
    // 但这是提醒性 fail, 不是宪法级 fail
    // 语义: "你的文档落后了, 请更新"
}
```

**与 ConstitutionGuardTests 的设计区分**：
- ConstitutionGuardTests: **阻断性** — 规则违反 → build fail → 必须修代码
- DocSyncTests: **提醒性** — 文档滞后 → test fail → 修文档 (不改代码)

---

## 7. AI Context Routing — CLAUDE.md 路由表

在 CLAUDE.md 中新增以下段，让 AI 按任务类型自动组装最小文档集：

```markdown
## AI Context Routing

修改代码前，按任务影响层级组装最小文档集:

| 任务类型 | 必读 | 按需读 |
|---------|------|-------|
| Domain 类型修改 | constitution/* + layers/domain.md | patterns/readonly-isolation (改集合暴露) |
| Graph 层修改 | constitution/* + layers/graph.md | patterns/fsm-design (改节点策略) |
| StateMachine 层修改 | constitution/* + patterns/fsm-design + layers/state-machine.md | patterns/handler-pipeline (改 handler) |
| Traversal 层修改 | constitution/* + patterns/dispatch-table + layers/traversal.md | patterns/fsm-design (改 step 流程) |
| 新增 enum | constitution/locked-enums.md + layers/<affected-layer>.md | decisions/log.md (查同类决策) |
| 修 bug | decisions/log.md + layers/<affected-layer>.md | constitution/constraints.md (检查是否违反约束) |
| 新增 Handler | constitution/* + patterns/handler-pipeline + patterns/dispatch-table | layers/state-machine.md |
| Phase 规划 | constitution/* + all patterns + decisions/log.md | all layers |

规则: 先读 constitution, 再读 patterns, 再读当前 layer。不读不相关的 layer。
```

---

## 8. 文档腐化防线

### 8.1 CI 检查

| 检查类型 | 位置 | 阻断性 | 语义 |
|---------|------|--------|------|
| ConstitutionGuardTests | `Architecture/ArchitectureGuardTests.cs` | **阻断** | 规则违反 → 必须修代码 |
| DocSyncGuardTests | `Architecture/DocSyncGuardTests.cs` | **阻断** | 文档-代码不一致 → 必须同步 (§5.6 流程强制) |
| DocSyncTests (P3) | `Architecture/DocSyncTests.cs` (待新增) | **提醒** | Tier 2/3 文档滞后 → 修文档 |
| `dotnet test` 全量 | CI pipeline | **阻断** | 任何测试 fail → build fail |

### 8.2 PR Checklist

```markdown
## Checklist

- [ ] Constitution: 改动是否涉及 constitution 中的约束?
      如果是, 是否更新了 constitution 文档?
- [ ] Layer doc: 改动涉及哪一层? layers/<layer>.md 是否需要更新?
- [ ] Decision log: 是否做了新的设计决策?
      如果是, 是否 append 到 decisions/log.md?
- [ ] Guard test: 是否新增了可静态检查的约束?
      如果是, 是否在 ConstitutionGuardTests.cs 加了对应测试?
- [ ] Doc-code sync: locked-enums.md 值数是否与 enum 定义一致?
      DocSyncGuardTests 是否全绿?
```

### 8.3 变更流程保证 — 三层防线

§5.6 定义了"改代码后必须同步更新哪些 Tier 文档"，但写入 charter 只是 Tier 4 记录（decision）。
要让流程生效，必须从 decision 升级为 constraint — 有代码化验证手段。

**三层防线**:

| 层级 | 机制 | 阻断性 | 捕获场景 |
|------|------|--------|---------|
| **第一层: 提醒** | `.claude/hooks/` 编辑提醒 + `/opsx:apply` Step 7 §5.6 checklist | 无阻断 | AI 编辑代码时自动提醒需要同步哪些 Tier 文档 |
| **第二层: 引导** | OpenSpec skill 定义 (brainstorming → propose → apply → verify → archive) | 无阻断 | 按正确步骤走流程; 跳过步骤不阻断但偏离最佳实践 |
| **第三层: 强制** | DocSyncGuardTests CI-blocking | **阻断** | 改了 enum 但没改 locked-enums.md → test fail → 必须补 |

**第三层测试覆盖矩阵** (DocSyncGuardTests.cs):

| Test | 验证 | 捕获 |
|------|------|------|
| `LockedEnums_ValueCounts_MatchCodeReality` | locked-enums.md 每行值数 = Enum.GetValues<T>().Length | 改了 enum 但没改 locked-enums |
| `Charter_GuardTable_ValueCounts_MatchCodeReality` | charter §6.1 每行值数 = Enum.GetValues<T>().Length | 改了 enum 但没改 charter |
| `LockedEnums_GuardTestNames_ExistInCode` | locked-enums.md Guard test 名 = ArchitectureGuardTests.cs 方法名 | 改了 Guard test 名但没改文档 |
| `GuardTest_AssertValues_MatchLockedEnums` | Guard test Assert.Equal(N) = locked-enums.md 值数 = test 名隐含数 | 改了 Assert 值但没改文档或 test 名 |

**设计原则**:
- Tier 1 ↔ 代码不一致 → **阻断** (constitution 值错了 = hard constraint 违反)
- Tier 2/3 ↔ 代码不一致 → 提醒 (patterns/layers 描述滞后不致命，P3 补 DocSyncTests)
- Tier 4 ↔ OpenSpec → 提醒 (decisions 是索引，手动写不致命但违反流程)

---

## 9. 迁移映射：当前 7 篇 → 新结构

| 当前文档 | 原内容去向 | 归档 |
|---------|-----------|------|
| 01 依赖拓扑 | Domain DAG → `layers/domain.md` §拓扑; 全层约束 → `constitution/constraints.md` C-3, C-5 | 01 保留为历史参考 |
| 02 数据流 | AI 模式 → `layers/domain.md` §数据流; 遍历流 → `layers/traversal.md` §执行流 | 02 保留为历史参考 |
| 03 语义合约 | Domain 职责 → `layers/domain.md` §语义; 语义层规则 → `constitution/constraints.md` C-2 | 03 保留为历史参考 |
| 04 跨域桥 | Domain 桥 → `layers/domain.md` §桥; 跨层桥 → `constitution/constraints.md` C-5 | 04 保留为历史参考 |
| 05 变更稳定性 | Domain 评级 → `layers/domain.md` §稳定性; enum lock → `constitution/locked-enums.md` | 05 保留为历史参考 |
| 06 校验边界 | 策略模式 → `layers/domain.md` §校验; enum guard → `constitution/locked-enums.md` | 06 保留为历史参考 |
| 07 序列化合约 | 约定 + 已知问题 → `layers/domain.md` §序列化; 逐类型行为表 → **归档** | 07 归档 (逐类型细节不再维护) |
| README.md | 重写为新结构索引 | 旧 README 保留为历史参考 |

**迁移策略**：不删除旧文档，保留在原位置作为历史参考。新结构在 `docs/system/` 下新建子目录，逐步填充内容后替换 README 索引。

---

## 10. 实施路线图

按「拦截价值 / 实现成本」排序，分三批实施：

### Batch 1: 立即可做 (单次会话)

| 步骤 | 动作 | 产出文件 | 成本 | 拦截价值 |
|------|------|---------|------|---------|
| 1 | 把 Domain enum guard (TypeHint=8, SelectionState=3) 加到 ArchitectureGuardTests | `ArchitectureGuardTests.cs` 扩展 | 极低 | 🟥 |
| 2 | CLAUDE.md 加 AI Context Routing 段 | `CLAUDE.md` 更新 | 极低 | 🟧 |
| 3 | 创建 docs/system/ 四层目录结构 + 填充 constitution 核心内容 | `constitution/constraints.md`, `constitution/locked-enums.md`, `constitution/prohibited-patterns.md` | 低 | 🟧 |
| 4 | 创建 decisions/log.md 初始条目 (从 Phase 2.1 已有决策提取) | `decisions/log.md` | 低 | 🟨 |

### Batch 2: Phase 2.2 期间

| 步骤 | 动作 | 产出文件 | 成本 | 拦截价值 |
|------|------|---------|------|---------|
| 5 | 引入 ArchUnitNET, 写 C-3 (三岛隔离) + C-4 (FSM 独立性) 测试 | `ArchitectureGuardTests.cs` 扩展 | 中 | 🟥 |
| 6 | DocSyncTests: Domain 类型数量一致性 | `Architecture/DocSyncTests.cs` 新增 | 低 | 🟨 |
| 7 | 填充 patterns 四篇内容 (从当前代码 + openspec specs 提取) | `patterns/fsm-design.md` 等 4 篇 | 中 | 🟧 |
| 8 | 填充 layers 四篇内容 (从当前代码 + 旧 7 篇提取) | `layers/domain.md` 等 4 篇 | 中 | 🟧 |

### Batch 3: Phase 3

| 步骤 | 动作 | 产出文件 | 成本 | 拦截价值 |
|------|------|---------|------|---------|
| 9 | Roslyn Analyzer: ToDictionary / non-sealed record | Analyzer project 新增 | 高 | 🟨 |
| 10 | M-14 修复: GlobalState 从 ITraversalContext 移除 | `TraversalState.cs` 修改 | 中 | 🟥 |
| 11 | D-I 修复: TraversalRuntimeContext 解构 God Object | 大范围重构 | 高 | 🟧 |

---

## 11. 初始 decisions/log.md 条目

从 Phase 1-2.1 已有决策提取的初始条目：

```markdown
### D-1 | 2026-07-02 | TypeHint 8 值锁定
Decision: TypeHint enum 8 值封顶, 不新增
Rationale: 视觉外观层火山级, 任何新值 cascade 4 域
Source: finding:design-decision (Phase 1 PRD)
Ref: docs/refactor/03-phase1-prd.md
Guard: 待新增 EnumValueGuardTests.TypeHint_Has8Values
Commit: ce124b5 (Phase 1 baseline)
Status: Locked

### D-2 | 2026-07-02 | Domain 三岛零互 import
Decision: Vision/Content/Common 零直接 import, 唯一桥 Mappings
Rationale: 防止跨域语义泄漏, 保持两级映射分离
Source: finding:design-decision (Phase 1 PRD)
Ref: docs/refactor/03-phase1-prd.md
Guard: 待新增 NamespaceIsolationGuardTests
Commit: ce124b5
Status: Locked

### D-3 | 2026-07-02 | GlobalState 8 值锁定
Decision: GlobalState enum 8 值封顶 (Idle/Initializing/Traversing/Paused/Error/Recovering/Completed/Terminated)
Rationale: 对齐 Python GlobalFSM macro 状态, Terminal 状态不可迁出
Source: openspec:traversal-fsm
Ref: openspec/specs/traversal-fsm/spec.md
Guard: EnumValueGuardTests.GlobalState_Has8Values
Commit: ce124b5
Status: Locked

### D-4 | 2026-07-02 | SelectionState 3 值锁定
Decision: SelectionState enum 3 值封顶 (Normal/Selected/Disabled)
Rationale: 视觉外观层火山级, cascade AliasMap + FlattenedElement.IsInteractive
Source: finding:design-decision (Phase 1 PRD)
Ref: docs/refactor/03-phase1-prd.md
Guard: 待新增 EnumValueGuardTests.SelectionState_Has3Values
Commit: ce124b5
Status: Locked

### D-5 | 2026-07-04 | DynamicMatch 从 TraversalState 移除
Decision: DynamicMatch 是 ChildrenStrategyType 值, 不是 FSM state
Rationale: H-1 违规 — 在 FSM 迁移矩阵中不可达, 与 ChildrenStrategyType 值域重叠
Source: finding:H-1 (docs/refactor/09-phase2-review-report.md §Hard Constraints)
Ref: docs/refactor/10-phase2.1-fix-design.md §Phase2.1a
Guard: EnumValueGuardTests.TraversalState_Has8Values
Commit: pending (Phase 2.1a 实施中)
Status: Fixed

### D-6 | 2026-07-04 | ITraversalNode 移到 Graph 层
Decision: ITraversalNode 和 IStackFrame 定义在 Graph.Models namespace, 不在 StateMachine
Rationale: H-5 — 消除 Graph↔StateMachine 双向依赖
Source: finding:H-5 (docs/refactor/09-phase2-review-report.md §Hard Constraints)
Ref: docs/refactor/10-phase2.1-fix-design.md §Phase2.1b
Guard: DependencyDirectionGuardTests.ITraversalNode_ResidesInGraphModelsNamespace
       DependencyDirectionGuardTests.TraversalState_DoesNotContainITraversalNodeOrIStackFrame
Commit: pending
Status: Fixed

### D-7 | 2026-07-04 | GlobalState 暂留 ITraversalContext
Decision: Phase 3 再修, 当前不 breaking change
Rationale: 影响 6 consumer (TraversalFSM, StepOrchestrator, ContainerHandler, ErrorHandler, PopupHandler, TraversalRuntimeContext), 无 runtime defect
Source: finding:M-14 (docs/refactor/09-phase2-review-report.md §Medium)
Ref: docs/refactor/11-m14-globalstate-evaluation.md
Guard: ConstitutionGuardTests.C4 (waived — 当前不验证)
Commit: pending
Status: Deferred · Target: Phase 3

### D-8 | 2026-07-04 | TextMatchMode 默认 Contains
Decision: DynamicMatcher text_pattern 匹配默认 Contains, Exact 为显式选项
Rationale: 向后兼容已有 DynamicMatcher 逻辑, M-9 (text_pattern 缺 Exact mode)
Source: finding:M-9 (docs/refactor/09-phase2-review-report.md §Medium)
Ref: openspec/specs/text-match-mode/spec.md
Guard: 无 (convention-level, 不需机器验证)
Commit: pending
Status: Locked

### D-9 | 2026-07-04 | ReadOnlySetWrapper cast-back 阻断
Decision: VisitedChildren 用 ReadOnlySetWrapper private sealed class, cast-back → InvalidCastException
Rationale: H-2 — VisitedChildren 泄漏 HashSet 引用, 可 cast-back 修改引擎内部
Source: finding:H-2 (docs/refactor/09-phase2-review-report.md §Hard Constraints)
Ref: openspec/specs/readonly-set-wrapper/spec.md
Guard: VisitedChildrenIsolationTests.VisitedChildren_CastBackToHashSet_ThrowsInvalidCastException
Commit: pending
Status: Fixed
```

---

## 12. 最终文件结构

```
docs/system/
  constitution/
    constraints.md          ← 解释性: 每条规则 + rationale + 影响范围 + Guard 链接
    locked-enums.md         ← 解释性: 每个 enum + 值数 + cascade 图 + Guard 链接
    prohibited-patterns.md  ← 解释性: 禁止模式 + 原因 + 替代方案 + 检查方式

  patterns/
    fsm-design.md           ← 双 FSM 架构 + 8×8 迁移矩阵 + 独立性原则
    handler-pipeline.md     ← 通用管道 + 3 Handler 差异对比 + dispatch + fallback
    readonly-isolation.md   ← 三级集合安全 + ReadOnlySetWrapper + Snapshot 隔离
    dispatch-table.md       ← Hook dispatch + fallback chain + Log-and-Continue

  layers/
    domain.md               ← 24+2 类型 + 三岛拓扑 + 桥 + 稳定性 + 校验 + 序列化
    graph.md                ← TraversalPlan + PlanCompiler + DynamicMatcher + 13 enum
    state-machine.md        ← 双 FSM + 3 Handler + NodeStack + Context 26 字段
    traversal.md            ← StepOrchestrator + 6 子组件 + 14-step 编排

  decisions/
    log.md                  ← AI-Coding 索引: Decision + Source + Ref + Guard + Commit + Status

  01-07 原文档              ← 保留为历史参考, 不删除
  README.md                 ← 重写为新结构索引

tests/UniClaw.Core.Tests/
  Architecture/
    ArchitectureGuardTests.cs   ← 阻断性: enum guard + dependency direction + namespace isolation (扩展中)
    DocSyncTests.cs             ← 提醒性: 文档-代码数量一致性 (新增)

CLAUDE.md                          ← 新增段: AI Context Routing 表
```

---

## 13. 核心保证

| 属性 | 保证机制 |
|------|---------|
| **改动影响 1-2 文件** | Domain 改动 → `layers/domain.md`; 新 enum → `constitution/locked-enums.md`; FSM 改动 → `patterns/fsm-design.md` |
| **AI 摄入 13 页动手** | constitution ≈ 4 页 + 1 pattern ≈ 2 页 + 1 layer ≈ 3 页 ≈ 9 页最小集 |
| **规则不可违反** | ConstitutionGuardTests CI 阻断 + (Phase 3) Roslyn Analyzer IDE 红线 |
| **决策不可遗忘** | decisions/log.md append-only, AI 读到就知道已定结论 |
| **文档滞后可检测** | DocSyncTests 提醒性 + PR checklist 勾选 |
| **新层扩展自然** | 新增层 → 新增 `layers/<layer>.md`, 其他文件不动 |
