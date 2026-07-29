# Layers — Graph

> **Tier 3 · Layers**: Graph 层规格书。改 Graph 类型/MatchCondition/PlanCompiler 时更新。
> 状态: Phase 2 实现中 (D-28: 服务/模型三目录分离已完成)
> 源码: `src/UniClaw.Core/Graph/`
>   `Graph/Models/`      — 纯数据模型 (records + enums + data interfaces)
>   `Graph/Abstractions/`— 服务接口 (D-28: 4 interfaces, guard 锁定)
>   `Graph/Services/`    — 服务实现 (D-28: 3 服务类 + 2 静态工具)
> 约束: → constitution C-5 (Graph→StateMachine 单向依赖)

---

## 1. Type Inventory

### 三目录结构 (D-28)

```
src/UniClaw.Core/Graph/
├── Abstractions/          ← 服务接口 (namespace .Graph.Abstractions)
│   ├── IDynamicMatcher.cs
│   ├── IPlanCompiler.cs
│   ├── ITemplateInstantiator.cs
│   └── ITemplateRegistry.cs
├── Models/                ← 纯数据 (namespace .Graph.Models)
│   ├── EntryConfig.cs
│   ├── ITraversalNode.cs  (+ IStackFrame)
│   ├── MatchableItem.cs
│   ├── MatchResult.cs
│   ├── NodeData.cs
│   ├── Template.cs        (仅 Template record)
│   ├── TraversalNode.cs
│   └── TraversalPlan.cs
└── Services/              ← 实现 (namespace .Graph.Services)
    ├── DynamicMatcher.cs
    ├── PlaceholderResolver.cs
    ├── PlanCompiler.cs
    ├── TemplateInstantiator.cs
    └── TemplateValidator.cs
```

**耦合约束**: Models MUST NOT → Abstractions/Services; Abstractions → Models + Domain; Services → Abstractions + Models + Domain。

### Enums (13)

| Enum | 值数 | 级别 | 用途 |
|------|------|------|------|
| `NodeType` | 8 | 火山 | TraversalNode 分类 (Screen/Container/Menu/Leaf 等) |
| `ChildrenStrategyType` | 3 | — | Static/DynamicMatch/None |
| `MatchAction` | ? | — | 匹配后动作 |
| `ErrorPolicyType` | 5 | — | Retry/Skip/Abort/Fallback/Backtrack |
| ~~`ExitConditionType`~~ | ~~3~~ | — | **REMOVED** (D-88): ContainerHandler supersedes, nav-subframe via Meta flag |
| `FallbackAction` | 4 | 丘陵 | Back/AutoEscape/Skip/Abort — dispatch key for ContainerActionExecutor |
| `EntryStrategy` | 3 | — | WaitForLoad/BindCurrentScreen/CheckPrecondition |
| `CompletionPolicyType` | ? | — | 完成策略 |
| `MatchMode` | ? | — | 匹配模式 |
| `TargetFoundAction` | ? | — | 目标找到后动作 |
| `TraversalMode` | ? | — | 遍历模式 |
| `WaitMode` | ? | — | 等待模式 |
| `TextMatchMode` | 2 | 平原 | Exact/Contains (→ decisions/log D-8) |

### Records (16)

| Record | Key fields | 用途 |
|--------|-----------|------|
| `TraversalNode` | NodeId, Name, NodeType, StaticChildren, ChildrenStrategy | ITraversalNode 实现, IsContainer/IsLeaf computed |
| `ChildrenStrategy` | Type (Static/DynamicMatch/None), DynamicRules | 子节点发现策略 |
| `DynamicRule` | RuleId, MatchCondition, MatchAction | 动态匹配规则 |
| `MatchCondition` | Type (MenuItemType), ExpectedAction, TextPattern, TextMatchMode, MinIndex, MaxIndex, Custom | 5维 conjunctive matching |
| `MatchableItem` | Text, MenuItemType, ExpectedAction, Index, Metadata | DynamicMatcher 输入 (D-28: 独立文件) |
| `MatchResult` | Matched, MatchRuleId, MatchedItem, Action | DynamicMatcher 输出 (D-28: 独立文件) |
| `ErrorPolicy` | Type, MaxRetries, BackoffMs | 错误策略 |
| ~~`ExitCondition`~~ | ~~Type, FallbackAction~~ | **REMOVED** (D-88): superseded by ContainerHandler |
| `Precondition` | PageName, Path, UiCondition, TimeoutSeconds | 前置条件 |
| `EntryPolicy` | Strategy, Precondition, WaitAfterEntryMs | 入口策略 |
| `CompletionPolicy` | Type (Exhaustive/TargetFound/Timeout/MaxSteps), TargetName, MatchMode, ActionOnFound, TimeoutSeconds, MaxSteps | 完成策略 |
| `IntentSlots` | TargetApp, Scope (`{full, target_only}`), Target, Depth (int?), ElementHandling, Navigation, Restore, Completion, Entry | Intent→Plan 输入 (9 正交维度) |
| `TraversalPlan` | 12 fields (root node, entry, completion, mode 等) | 遍历蓝图 |
| `NodeData` | Nodes dictionary | node registry |
| `Template` | MatchCondition, Children, Action, Name | 模板定义 (D-28: Template.cs 仅含此 record) |
| `EntryConfig` | Strategy, WaitMs, Preconditions | 入口配置 |

**构造期 fail-fast 校验 (fail-fast-validation-baseline 变更)**: Graph 模型 record 从无校验 primary constructor 改为手动构造函数 + `DomainValidationException`:
- 数值范围: `Precondition/EntryPolicy.TimeoutSeconds` (0,300], `CompletionPolicy.TimeoutSeconds` (0,86400] / `MaxSteps` [1,1e6] / `TargetName`(TargetFound 时非空), `EntryConfig` 加安全上界 (WaitTimeoutSeconds≤300 等)
- `ChildrenStrategy.MaxChildren` [0,10000], `ErrorPolicy.MaxRetries` [0,100]
- 非空: `DynamicRule.RuleId`/`ChildTemplate`, `TraversalNode.NodeId`/`Name`
- `TraversalPlan` 根节点校验: 显式提供时须 Screen/Container + NoAction; **null 保留合法** (引擎 BuildDefaultRoot 兜底, D-83)

### Interfaces (6)

| Interface | 所在文件 | 用途 |
|-----------|---------|------|
| `ITraversalNode` | `Graph/Models/ITraversalNode.cs` | 节点接口 (NodeId, Name, NodeType, StaticChildren, ChildrenStrategy, **ErrorPolicy**) — C-3 加 ErrorPolicy (D-84) |
| `IStackFrame` | `Graph/Models/ITraversalNode.cs` | DFS stack frame (NodeId, Node, Children) |
| `ITemplateRegistry` | `Graph/Abstractions/ITemplateRegistry.cs` | 模板注册接口 (D-28: 从 Models/Template.cs 拆出) |
| `IPlanCompiler` | `Graph/Abstractions/IPlanCompiler.cs` | 意图→计划编译 (D-28 新增) |
| `IDynamicMatcher` | `Graph/Abstractions/IDynamicMatcher.cs` | 5维 conjunctive matching (D-28 新增) |
| `ITemplateInstantiator` | `Graph/Abstractions/ITemplateInstantiator.cs` | 模板→节点实例化 (D-28 新增) |

**Abstractions/ 锁定为 4 接口** — Guard: `GraphAbstractions_Has4Interfaces` (CI-blocking)。新增接口须同步更新 guard + 本表。

### Classes (5, Services/)

| Class | 实现接口 | 用途 |
|-------|---------|------|
| `DynamicMatcher` | `IDynamicMatcher` | 5维 conjunctive matching, MatchAll (first-rule-wins), MatchableItem→MatchResult |
| `PlanCompiler` | `IPlanCompiler` | 4 TEMPLATE_SETS (keyed by ElementHandling), 5-step compile (IntentSlots→TraversalPlan), scope ∈ `{full, target_only}`, Completion override covers Type (→ decisions D-89, D-90) |
| `TemplateInstantiator` | `ITemplateInstantiator` | 7-step instantiate flow, path concatenation |
| `PlaceholderResolver` | — (static utility) | 占位符解析 (从 Models/Template.cs 拆出) |
| `TemplateValidator` | — (static utility) | 模板验证 (从 Models/Template.cs 拆出) |

---

## 2. Core Schema — TraversalPlan

TraversalPlan 是遍历引擎的蓝图，由 PlanCompiler 从 IntentSlots 编译生成。12 个核心字段定义遍历范围、入口条件、完成策略和节点树。

---

## 3. PlanCompiler

**4 TEMPLATE_SETS** (keyed by **ElementHandling**, not Scope):
- `full_interaction` → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"]
- `menu_only` → ["menu_container"]
- `safe_mode` → ["menu_container", "switch_leaf", "slider_leaf", "leaf_action"]
- `read_only` → ["leaf_info"]

**5 MatchConditions per template** (unchanged):
- menu_container → MenuItemType=menu_item
- switch_leaf → TypeHint=switch
- slider_leaf → TypeHint=slider
- leaf_action → TypeHint=button
- leaf_info → empty (matches everything)

**5-step compile flow (legacy 6-step → 5)**:
1. `ValidateSlots` — TargetApp 非空, Scope ∈ `{full, target_only}` (拒 legacy full_interaction/target_path → DomainValidationException), ElementHandling ∈ TEMPLATE_SETS keys, target_only ⇒ Target 非空, Depth ≥ 0, Completion ∈ `{max_steps, timeout}` (unknown → fail-fast)
2. `BuildEntryPolicy` — 默认 ColdLaunch / fallback=null (非 DirectDeeplink)
3. `BuildRootNode` — ChildrenStrategy.DYNAMIC_MATCH, DynamicRules 来自 `slots.ElementHandling ?? "full_interaction"` (NOT Scope), RootNode 反映 `slots.Entry ?? slots.TargetApp`
4. `BuildCompletionPolicy` — `full → Type=Exhaustive`, `target_only → Type=TargetFound(TargetName, Contains, MarkAndStop)`, Completion override **covers** Type (`max_steps → MaxSteps`, `timeout → Timeout`)
5. Assemble `TraversalPlan` with all required fields

**Removed**: `BuildStaticNodes` (step 6) + `target_path` scope branch（target_path 零场景，静态节点构造退役）。

**CompletionPolicyType.Exhaustive** (formerly `None`) — exhaustive intent semantic, renamed in Change B (container-handler-canonicalization).

---

## 4. DynamicMatcher

**5 conjunctive dimensions**:
1. MenuItemType — 精确匹配
2. ExpectedAction — 精确匹配
3. TextPattern + TextMatchMode — Exact 或 Contains (→ decisions D-8)
4. MinIndex/MaxIndex — 范围匹配
5. Custom dictionary — 键值对匹配

**空 condition = leaf_info semantics**: 所有维度为 null/empty 时匹配一切。

**MatchAll**: first-matching-rule-wins across all items。

**MatchableItem**: wraps page analysis items — MenuItemType, ExpectedAction from Domain.Content。

**MatchResult**: Matched, MatchRuleId, MatchedItem, Action。

---

## 5. Dependency

```
Graph.Models → Domain.Content | Domain.Common | Domain
Graph.Services → Graph.Abstractions (实现接口)
Graph.Services → Graph.Models (服务消费数据模型)
Graph.Services → Domain.Content (DynamicMatcher 输入)
Graph.Services → Domain.Common (TemplateInstantiator 输出)
Graph.Abstractions → Graph.Models (接口参数类型)

Graph.Models → StateMachine (ITraversalNode 使用 NodeType)
  BUT: ITraversalNode 定义在 Graph.Models namespace (→ constitution C-5)
  TraversalNode.cs 不 using StateMachine (Guard test verified)

Traversal → Graph.Abstractions (D-28: TraversalEngine 字段为 IDynamicMatcher/ITemplateInstantiator, 默认实现仍 new 具体类)
```

---

## 6. Locked Enums (→ constitution locked-enums.md)

| Enum | 值数 | Guard test |
|------|------|-----------|
| NodeType | 8 | `NodeType_Has8Values` |
| FallbackAction | 4 | `FallbackAction_Has4Values` |
| TextMatchMode | 2 | convention-level (无 Guard test) |

---

## 7. Host Scenario Compilation

Android Settings 场景由 Host 的 `ScenarioPlanCompiler` 复用既有
`PlanCompiler` 编译，不在 Graph 层新增场景专用模型：

- `locate_one_item` → `IntentSlots.Scope = "target_only"`
- Settings 菜单只读导航 → `ElementHandling = "menu_only"`
- 场景预算、scenario/policy hash 写入 `TraversalPlan.Meta`
- Host 在设备执行前持久化编译后的 `plan.json`

长期 `TraversalPlan` 表达入口、目标和边界；每一屏的单动作
`ScenarioStepPlan` 属于 Host runner，不进入 Graph canonical schema。这样既
复用 Graph 的确定性编译，又避免把设备页面指纹和 ADB 坐标泄漏进 Core。
