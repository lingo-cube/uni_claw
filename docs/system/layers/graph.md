# Layers — Graph

> **Tier 3 · Layers**: Graph 层规格书。改 Graph 类型/MatchCondition/PlanCompiler 时更新。
> 状态: Phase 2 实现中 (D-28: 服务/模型分离规划中 → 见 roadmap P3)
> 源码: `src/UniClaw.Core/Graph/`
>   `Graph/Models/`      — 纯数据模型 (records + enums + data interfaces)
>   `Graph/Services/`    — 服务实现 (D-28 规划中, 当前随 Models/)
>   `Graph/Abstractions/`— 服务接口 (D-28 规划中)
> 约束: → constitution C-5 (Graph→StateMachine 单向依赖)

---

## 1. Type Inventory

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

### Records (14)

| Record | Key fields | 用途 |
|--------|-----------|------|
| `TraversalNode` | NodeId, Name, NodeType, StaticChildren, ChildrenStrategy | ITraversalNode 实现, IsContainer/IsLeaf computed |
| `ChildrenStrategy` | Type (Static/DynamicMatch/None), DynamicRules | 子节点发现策略 |
| `DynamicRule` | RuleId, MatchCondition, MatchAction | 动态匹配规则 |
| `MatchCondition` | Type (MenuItemType), ExpectedAction, TextPattern, TextMatchMode, MinIndex, MaxIndex, Custom | 5维 conjunctive matching |
| `ErrorPolicy` | Type, MaxRetries, BackoffMs | 错误策略 |
| `ExitCondition` | Type, FallbackAction | 退出条件 + 回退行为 |
| `Precondition` | PageName, Path, UiCondition, TimeoutSeconds | 前置条件 |
| `EntryPolicy` | Strategy, Precondition, WaitAfterEntryMs | 入口策略 |
| `CompletionPolicy` | ExitConditions (list), MaxContainers | 完成策略 |
| `IntentSlots` | Scope, Target, TargetPath | Intent→Plan 输入 |
| `TraversalPlan` | 12 fields (root node, entry, completion, mode 等) | 遍历蓝图 |
| `NodeData` | Nodes dictionary | node registry |
| `Template` | MatchCondition, Children, Action, Name | 模板定义 |
| `EntryConfig` | Strategy, WaitMs, Preconditions | 入口配置 |

### Interfaces (3 → 6 planned, D-28)

| Interface | 所在文件 | 用途 |
|-----------|---------|------|
| `ITraversalNode` | `Graph/Models/ITraversalNode.cs` | 节点最小接口 (NodeId, Name, NodeType, StaticChildren, ChildrenStrategy) |
| `IStackFrame` | `Graph/Models/ITraversalNode.cs` | DFS stack frame (NodeId, Node, Children) |
| `ITemplateRegistry` | `Graph/Abstractions/` (D-28: 从 Models/Template.cs 拆出) | 模板注册接口 |
| `IPlanCompiler` | `Graph/Abstractions/` (D-28 新增) | 意图→计划编译 |
| `IDynamicMatcher` | `Graph/Abstractions/` (D-28 新增) | 5维 conjunctive matching |
| `ITemplateInstantiator` | `Graph/Abstractions/` (D-28 新增) | 模板→节点实例化 |

### Classes (4 → 移入 Services/, D-28)

| Class | 目标位置 | 用途 |
|-------|---------|------|
| `DynamicMatcher` | `Graph/Services/` | 5维 conjunctive matching, MatchAll (first-rule-wins), MatchableItem→MatchResult |
| `PlanCompiler` | `Graph/Services/` | 4 TEMPLATE_SETS, 6-step compile (IntentSlots→TraversalPlan), scope validation (→ decisions D-8) |
| `TemplateInstantiator` | `Graph/Services/` | 7-step instantiate flow, path concatenation |
| `PlaceholderResolver` / `TemplateValidator` | `Graph/Services/` | static utility methods (从 Models/Template.cs 拆出) |

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
Graph.Services → Graph.Models (服务消费数据模型)
Graph.Services → Domain.Content (DynamicMatcher 输入)
Graph.Services → Domain.Common (TemplateInstantiator 输出)
Graph.Abstractions (D-28) → Graph.Models (接口参数类型)

Graph.Models → StateMachine (ITraversalNode 使用 NodeType)
  BUT: ITraversalNode 定义在 Graph.Models namespace (→ constitution C-5)
  TraversalNode.cs 不 using StateMachine (Guard test verified)

Traversal → Graph.Abstractions (D-28 后 TraversalEngine 注入接口，不再 new 具体类)
```

---

## 6. Locked Enums (→ constitution locked-enums.md)

| Enum | 值数 | Guard test |
|------|------|-----------|
| NodeType | 8 | `NodeType_Has8Values` |
| FallbackAction | 4 | `FallbackAction_Has4Values` |
| TextMatchMode | 2 | convention-level (无 Guard test) |
