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
| `ExitConditionType` | 3 | — | Timeout/MaxDepth/AllVisited |
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
| `ExitCondition` | Type, FallbackAction | 退出条件 + 回退行为 |
| `Precondition` | PageName, Path, UiCondition, TimeoutSeconds | 前置条件 |
| `EntryPolicy` | Strategy, Precondition, WaitAfterEntryMs | 入口策略 |
| `CompletionPolicy` | ExitConditions (list), MaxContainers | 完成策略 |
| `IntentSlots` | Scope, Target, TargetPath | Intent→Plan 输入 |
| `TraversalPlan` | 12 fields (root node, entry, completion, mode 等) | 遍历蓝图 |
| `NodeData` | Nodes dictionary | node registry |
| `Template` | MatchCondition, Children, Action, Name | 模板定义 (D-28: Template.cs 仅含此 record) |
| `EntryConfig` | Strategy, WaitMs, Preconditions | 入口配置 |

### Interfaces (6)

| Interface | 所在文件 | 用途 |
|-----------|---------|------|
| `ITraversalNode` | `Graph/Models/ITraversalNode.cs` | 节点最小接口 (NodeId, Name, NodeType, StaticChildren, ChildrenStrategy) |
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
| `PlanCompiler` | `IPlanCompiler` | 4 TEMPLATE_SETS, 6-step compile (IntentSlots→TraversalPlan), scope validation (→ decisions D-8) |
| `TemplateInstantiator` | `ITemplateInstantiator` | 7-step instantiate flow, path concatenation |
| `PlaceholderResolver` | — (static utility) | 占位符解析 (从 Models/Template.cs 拆出) |
| `TemplateValidator` | — (static utility) | 模板验证 (从 Models/Template.cs 拆出) |

---

## 2. Core Schema — TraversalPlan

TraversalPlan 是遍历引擎的蓝图，由 PlanCompiler 从 IntentSlots 编译生成。12 个核心字段定义遍历范围、入口条件、完成策略和节点树。

---

## 3. PlanCompiler

**4 TEMPLATE_SETS**:
- `full_interaction` (4 templates: menu_container, switch_leaf, slider_leaf, leaf_action + leaf_info)
- `menu_only` (1 template)
- `safe_mode` (4 templates)
- `read_only` (1 leaf_info template)

**5 MatchConditions per template**:
- menu_container → MenuItemType=menu_item
- switch_leaf → TypeHint=switch (通过 TextPattern)
- slider_leaf → TypeHint=slider
- leaf_action → TextPattern=button
- leaf_info → empty (matches everything)

**6-step compile flow**:
1. validate_slots — scope legality, depth checks (→ decisions D-8)
2. build_entry_policy
3. build_root_node
4. build_completion_policy
5. assemble TraversalPlan
6. build_static_nodes (target_path scope only)

**Scope legality** (H-4 fix): `validate_slots` 检查 IntentSlots.Scope 的合法取值范围，非法 scope → DomainValidationException。

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
