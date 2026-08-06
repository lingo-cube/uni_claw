# Constitution — Locked Enums

> **Tier 1**: 每个 enum 值数不准变，CI Guard test 强制执行。
> 详细规格: `docs/system/charter-specification.md` §2.2
> 更新触发: 新增 enum 值 → 必须先更新 mapping table (Hilly 级), 再加值

---

## Phase 2.2 锁定 (2 enum + 1 interface method lock) — ArchitectureGuardTests.cs

| Enum | Namespace | 值数 | 级别 | Cascade 影响 | Guard Test |
|------|-----------|------|------|-------------|-----------|
| `SpanType` | Observability | **11** | 火山 | operation_rules + trace_integrity verification dimensions | `SpanType_Has11Values` |
| `ErrorSeverity` | Observability | **5** | 丘陵 | ErrorRecord severity 分类, 日志级别映射 | 无 (Phase 3 Guard 候选) |

### SpanType (11 值 — 火山级)

```
DfsForward · DfsBacktrack · RestoreOp · SkipDangerous · PopupHandling
ContainerHandling · ErrorHandling · PageAnalysis · CacheOp · AICall · StateDecision

覆盖 operation_rules (RestoreOp, SkipDangerous) 和 trace_integrity (DfsForward, DfsBacktrack, PageAnalysis, StateDecision) 验证维度。
```

### ErrorSeverity (5 值 — 丘陵级)

```
Debug · Info · Warning · Error · Fatal

ErrorRecord severity 5 级分类。扩展需审查日志级别映射和 ErrorClassifier 输出。
```

### Interface Method Lock

| Interface | Namespace | 方法数 | 角色 | Guard Test |
|-----------|-----------|-------|------|-----------|
| `ITraceRecorder` | Observability | **7** | 纯写契约 (2 session lifecycle + 5 span recording) | `ITraceRecorder_Has7Methods` |

**ITraceRecorder 7 method lock** (→ D-19): ITraceRecorder SHALL NOT 包含查询方法 (GetXxxAsync)、CurrentSession getter、或 ExportTraceAsync。13→7 方法精简已完成，Guard 阻止方法数回归。

### Cross-Layer Reference — ExecutionRecord.TargetType

ExecutionRecord.TargetType 引用 `Domain.Common.TargetType` enum (Text/Coordinate/UiIndex, 3 值)。
此 Observability→Domain.Common 引用允许 per D-17 (Observability 是 cross-cutting utility) + D-21 (类型安全优于 object? Target)。
TargetType 本身不在 Guard 锁定中 (Domain Hilly 级, 值数=3, 见下方 Domain Hilly 级表)。

---

## Phase 2.1 锁定 (10 enum) — ArchitectureGuardTests.cs

| Enum | Namespace | 值数 | 级别 | Cascade 影响 | Guard Test |
|------|-----------|------|------|-------------|-----------|
| `TraversalState` | StateMachine | **8** | 火山 | FSM matrix, StepOrchestrator, handlers | `TraversalState_Has8Values` |
| `GlobalState` | StateMachine | **8** | 火山 | GlobalFSM matrix, ITraversalContext, terminal 状态 | `GlobalState_Has8Values` |
| `NodeType` | Graph.Models | **8** | 火山 | DynamicMatcher, PlanCompiler, TraversalNode | `NodeType_Has8Values` |
| `ErrorType` | StateMachine | **6** | 丘陵 | ErrorClassifier 7-priority chain, ErrorStrategySelector | `ErrorType_Has6Values` |
| `ErrorStrategy` | StateMachine | **5** | 丘陵 | RecoveryExecutor 5 hooks, backoff calculation | `ErrorStrategy_Has5Values` |
| `PopupType` | StateMachine | **6** | 丘陵 | PopupDetector regex, PopupClassifier, 6 dispatch hooks (incl. Anr) | `PopupType_Has6Values` |
| `DismissStrategy` | StateMachine | **4** | 丘陵 | PopupClassifier conditional logic (D-10: target-based), PopupActionExecutor dispatch | `DismissStrategy_Has4Values` |
| `UrgencyLevel` | StateMachine | **3** | 平原 | PopupClassifier urgency determination (1 dependency) | `UrgencyLevel_Has3Values` |
| `BlockingType` | StateMachine | **3** | 平原 | PopupClassifier blocking determination (1 dependency) | `BlockingType_Has3Values` |
| `FallbackAction` | Graph.Models | **4** | 丘陵 | ExitCondition, ContainerActionExecutor dispatch | `FallbackAction_Has4Values` |

⚠️ **Python↔C# 值数偏差 (→ decisions/log D-10, D-11, D-12, D-13)**:

| Enum | C# 值数 | Python 值数 | 偏差 | 决策状态 |
|------|---------|-----------|------|---------|
| `UrgencyLevel` | 3 (LOW/MEDIUM/HIGH) | 3 (LOW/MEDIUM/HIGH) | ✅ 对齐 | D-11 Fixed — 移除不可达 Critical |
| `CompletionReason` | 4 (Timeout/MaxDepth/AllVisited/Incomplete) | 5 (含 ERROR 死值) | C# 对齐 Python **实际使用** 4 值 | D-12 Fixed — 不加死值 Error |
| `DismissStrategy` | 4 (AutoClose/Back/WaitTimeout/AutoCloseOrBack) | 4 (同值) | ✅ 值数对齐, **映射逻辑对齐** — D-10 改为条件逻辑 | D-10 Fixed |
| `CompletionStatus` (Python) | C# 无此 enum | 5 | C# 用 CompletionReason 替代, 对齐 Python 实际使用 | D-12 Fixed |

### TraversalState (8 值 — 火山级)

```
NodeSelect → PreconditionCheck → Execute → ResultVerify
                                   ↓           ↓
                               Branch ← ErrorHandling
                                   ↓           ↓
                              FrameComplete  PopupHandling
```

**H-1 事故**: DynamicMatch 曾被错误放入此 enum (值=9)，导致 FSM 矩阵不可达状态。DynamicMatch 实际是 `ChildrenStrategyType` 值。

### GlobalState (8 值 — 火山级)

```
Idle → Initializing → Traversing → Completed (terminal)
                  ↓       ↓         ↓
                Error   Paused   Terminated (terminal)
                  ↓       ↓
              Recovering → Initializing (recovery path)
```

---

## Phase 1 Domain 锁定 (2 enum) — ArchitectureGuardTests.cs

| Enum | Namespace | 值数 | 级别 | Cascade 影响 | Guard Test |
|------|-----------|------|------|-------------|-----------|
| `TypeHint` | Domain.Vision | **8** | 火山 | AliasMap, IsInteractive, IsVisualOnly, TypeStringToTypeHintMap | `TypeHint_Has8Values` |
| `SelectionState` | Domain.Vision | **3** | 火山 | SelectedAliases, DisabledAliases, FlattenedElement.IsInteractive | `SelectionState_Has3Values` |

### TypeHint (8 值 — 火山级)

```
Text · Image · Button · Link · InputField · Checkbox · Dropdown · ClickableText

8 值只回答"看起来像什么"。
不含行为性值: 没有 "toggle" / "menu_item" / "input" / "loading"。
行为分类 → ElementTypeMapper → MenuItemType (11) / ExpectedAction (4)
```

**Cascade 扩展影响图** (假设加 TypeHint.Loading):

```
TypeHint.Loading
  → TypeHintExtensions.AliasMap: 需加 "loading"/"spinner" 别名
  → TypeHintExtensions.IsInteractive: Loading 应返回 false (纯视觉)
  → TypeHintExtensions.IsVisualOnly: Loading 应返回 true
  → ElementTypeMapper.TypeStringToTypeHintMap: 需决定 "loading" 是否映射
  → FlattenedElement.IsInteractive: 如果 type=Loading, IsInteractive=false
  → DynamicMatcher: 如果 MatchCondition.Type=Loading, 需匹配逻辑

共 6 个 cascade 点 (4 域 + 2 Phase 2 域)
```

### SelectionState (3 值 — 火山级)

```
Normal · Selected · Disabled

不含: "hover" / "focused" / "loading" / "error"
```

---

## Domain Hilly 级 Enum (非 Guard 锁定, 但扩展需谨慎)

| Enum | 值数 | 扩展规则 |
|------|------|---------|
| `MenuItemType` | 11 | **先更新 TYPE_TO_MENU_ITEM mapping，再加 enum 值** |
| `ExpectedAction` | 4 | **先更新 TYPE_TO_EXPECTED_ACTION mapping，再加 enum 值** |
| `OperationType` | 5 | 审查所有 Operation.Action 校验再加 |
| `Direction` | 4 | 审查所有导航逻辑再加 |
| `RegionRole` | 5 | 审查 Region 验证再加 |

**扩展铁律**: 任何 Hilly 级 enum 扩展值时，**必须先更新 mapping table，再加 enum 值**。反过来做会导致新值无 mapping → 运行时 fallback → 静默降级。
