# Phase 1 模型关系梳理：Python ↔ C# 依赖图对比

> **日期**: 2026-07-02
> **分支**: `feature/refactor`
> **范围**: 全模型关系（Domain + 上层）

---

## 1. Python 依赖岛架构

Python 的模型形成 **3 个独立依赖岛**，仅 1 座桥连接：

```
ISLAND 1: Vision (7 types)
  BoundingBox ──→ Region ──→ ScreenHints
  TypeHint ──→ FlattenedElement ──→ FlattenedScreen
  SelectionState ──→ FlattenedElement ──→ FlattenedScreen

ISLAND 2: Content (12 types)
  Coordinate ──→ MenuInfo / MenuItem / PopupInfo / PageAnalysis / ContentNode
  MenuItemType ──→ MenuItem / ElementTypeMapper
  ExpectedAction ──→ MenuItem / ElementTypeMapper
  VisitFingerprint ──→ SimulationState

  BRIDGE: ElementTypeMapper ──→ MenuItemType + ExpectedAction (Island 2)
           同时引用 Island 1 的 TypeHint 字符串值（隐式耦合）

ISLAND 3: Graph + Trace + TraversalContext
  Target ──→ Operation ──→ TraversalNode
  RestoreAction ──→ Operation
  9 enums ──→ TraversalNode / ExitCondition / CompletionPolicy 等
  Template ──→ TraversalNode (工厂)
  TraceNode hierarchy ──→ SessionNode / StepNode / SpanNode (独立)
  TraversalContext ──→ GlobalState / ErrorRecord / ActionRecord (独立)
```

**关键特征**：Island 1 和 Island 2 **零 import 依赖**，仅通过 ElementTypeMapper 隐式耦合。Island 3 完全独立。

---

## 2. C# 依赖图架构

C# 的依赖图结构相似，但有关键差异：

```
Domain (root)
  DomainValidationException ←── 所有校验 record (11 个类型)
  DomainJsonOptions ←── (无依赖，仅 BCL)

Domain.Vision (8 types)
  BoundingBox ──→ Region ──→ ScreenHints
  TypeHint ──→ FlattenedElement ──→ FlattenedScreen
  SelectionState ──→ FlattenedElement ──→ FlattenedScreen

Domain.Content (10 types)
  Coordinate ──→ MenuInfo / MenuItem / PopupInfo / PageAnalysis / ContentNode
  MenuItemType ──→ MenuItem / ElementTypeMapper
  ExpectedAction ──→ MenuItem / ElementTypeMapper

Domain.Common (5 types)
  OperationType ──→ Operation / RestoreAction
  Target ──→ Operation / RestoreAction

Domain.Mappings (2 types)
  ElementTypeMapper ──→ TypeHint(Vision) + MenuItemType(Content) + ExpectedAction(Content)
                       ← 这是 C# 中唯一跨 Vision↔Content 的显式依赖
  AndroidWidgetClass ──→ (孤立，未被 ElementTypeMapper 直接引用)

上层 → Domain 桥接：
  AI.PageAnalysis ──→ FlattenedScreen(Vision)        ← 唯一 Vision→AI 桥
  TraversalNode ──→ Operation(Common)                  ← 唯一 Common→Graph 桥
  IAIStrategyAdvisor ──→ ITraversalContext(StateMachine)

StateMachine ──→ (完全独立，零 Domain 依赖)
Observability ──→ (完全独立，零 Domain 依赖)
```

---

## 3. 关键差异对比

### 3.1 ElementTypeMapper 桥的性质不同

| 维度 | Python | C# | 影响 |
|------|--------|-----|------|
| **桥方向** | Content → ElementTypeMapper（import MenuItemType/ExpectedAction） | Vision + Content → ElementTypeMapper（import TypeHint + MenuItemType + ExpectedAction） | C# 桥更宽，同时连接两个岛 |
| **桥类型** | 隐式语义耦合（Island 1→Island 2 通过共享字符串词汇） | 显式类型依赖（import TypeHint enum） | C# 合二为一了 Python 的两套系统 |
| **中间层** | `ANDROID_CLASS_MAP` → 中间字符串 → `TYPE_TO_MENU_ITEM` | `AndroidClassToTypeHintMap` → TypeHint enum → (无第二层) | C# 缺少中间字符串层，直接映射到 TypeHint |

**这是 P0 问题的根源**——C# ElementTypeMapper 同时是 Vision↔Content 的显式桥和 Android→行为的映射器，承担了两个本应独立的角色。

### 3.2 DomainValidationException 是 C# 独有的跨切面

Python 各模块各自抛 `ValueError`（BCL），没有统一的领域异常类。

C# 的 `DomainValidationException`（带 `FieldName` + `IllegalValue`）是 **所有校验 record 的共同依赖**——11 个类型都依赖它。这比 Python 更集中、更结构化，是正确的 C# 设计选择。

### 3.3 上层→Domain 桥接点

| 桥 | Python | C# | 一致性 |
|----|--------|-----|--------|
| Operation→TraversalNode | graph/node.py 内联定义 Operation | Domain.Common.Operation → Graph.Models.TraversalNode | ✅ C# 更正确——Operation 在 Domain 层 |
| FlattenedScreen→AI | 无独立 AI 接口文件（main 分支无 ai_strategy_advisor.py） | AI.PageAnalysis 直接引用 FlattenedScreen | ⚠️ C# 有 AI 层简化版 PageAnalysis/PopupInfo 与 Domain 版冲突 |
| GlobalState→TraversalContext | traversal_context.py 内联定义 | StateMachine.GlobalState → StateMachine.ITraversalContext | ✅ C# 放在 StateMachine 更正确 |
| TraversalContext→AI | forward ref ContainerInference | ITraversalContext 在 IAIStrategyAdvisor 方法参数 | ✅ |

### 3.4 独立性差异

| 模块 | Python | C# | 差异 |
|------|--------|-----|------|
| Content | 单文件 content_models.py，无外部 import | 4 文件（EnumsAndCoordinate + MenuRecords + PageAnalysisRecords + TreeAndFingerprint），无 Vision import | ✅ 一致 |
| Trace | 完全独立（stdlib only） | Observability 完全独立（无 Domain import） | ✅ 一致 |
| StateMachine | 无（Python 用 TraversalContext 内联的 GlobalState） | 完全独立（GlobalState/TraversalState/NodeStack 自命名空间） | ⚠️ C# 重构为独立层，更清晰 |
| AndroidWidgetClass | element_type_mapper.py 内联定义 | Domain.Mappings 独立 enum | ✅ C# 更模块化 |

---

## 4. Hub 类型对比

### Python Hub

| Hub | 依赖者数 | 跨岛? |
|-----|---------|-------|
| BoundingBox | 3 直 + 2 间接 | 仅 Island 1 |
| Coordinate | 5 直 + 2 间接 | 仅 Island 2 |
| TypeHint | 2 直 + 1 间接 | 仅 Island 1 |
| Target | 2 直 + 2 间接 | 仅 Island 3 |
| Operation | 1 直 + 1 间接 | 仅 Island 3 |

### C# Hub

| Hub | 依赖者数 | 跨域? |
|-----|---------|-------|
| **DomainValidationException** | **11** | **跨全部 3 个 Domain 子域** ← Python 无此角色 |
| Coordinate | 5 | 仅 Content |
| BoundingBox | 3 | 仅 Vision |
| TypeHint | 4 (含 ElementTypeMapper) | **跨 Vision↔Mappings** ← Python 无此跨岛 |
| MenuItemType | 3 | **跨 Content↔Mappings** |
| ExpectedAction | 3 | **跨 Content↔Mappings** |
| Operation | 2 (Domain 内) + 1 (Graph) | **跨 Common↔Graph** |

C# 的 Hub 更多跨域连接——因为 C# 把 Python 的隐式语义耦合变成了显式类型依赖。这是设计取舍：更类型安全，但也更耦合。

---

## 5. 隐式语义耦合（Python 有，C# 需对齐）

Python 3 个岛之间没有 import 依赖，但有 **隐式数据流耦合**：

1. **Vision → Content**：FlattenedElement.type_hint 的字符串值（"switch", "button"）和 ElementTypeMapper 的 TYPE_TO_MENU_ITEM 字典 key 完全一致。这是**共享词汇表**。

2. **Content → Graph**：DynamicRule.match_condition 的 type 字段使用与 ElementTypeMapper 相同的字符串词汇（`{"type": "switch"}`）。MenuItem 对象从 PageAnalysis 流入 DynamicMatcher。

3. **Graph → Trace**：TraversalNode.node_id、Operation.action、Target.value 被记录到 StepNode 和 SpanNode 字段中。

C# 当前通过 ElementTypeMapper 的显式 import 处理了耦合 #1，但耦合 #2 和 #3 依赖上层实现（Phase 2+）。

---

## 6. 依赖方向图对比

### Python

```
Island 1 (Vision)       Island 2 (Content)       Island 3 (Graph+Trace)
BoundingBox ←─ Region    Coordinate ←─ MenuInfo    Target ←─ Operation ←─ TraversalNode
TypeHint ←─ FlattenedEl  MenuItemType ←─ MenuItem  RestoreAction ←─ Operation
SelectionState ←─ FlatEl ExpectedAction ←─ MenuItem 9 enums ←─ TraversalNode/Exit/Completion
                          PageAnalysis ←─ (5 types)  Template ←─ TraversalNode (factory)
                                                          TraceNode ←─ (独立层级)

                          ElementTypeMapper ←─ MenuItemType + ExpectedAction
                          隐式：Vision type strings → ElementTypeMapper dicts
```

**无反向依赖。无循环依赖。3 岛仅 1 桥（隐式）。**

### C#

```
Domain (root)
  DomainValidationException ←── 11 types (跨 Vision/Content/Common)

Domain.Vision            Domain.Content            Domain.Common
BoundingBox ←─ Region    Coordinate ←─ MenuInfo    OperationType ←─ Operation/RestoreAction
TypeHint ←─ FlattenedEl  MenuItemType ←─ MenuItem  Target ←─ Operation/RestoreAction
SelectionState ←─ FlatEl ExpectedAction ←─ MenuItem

Domain.Mappings ←── TypeHint(Vision) + MenuItemType(Content) + ExpectedAction(Content)
                   ← 这是显式跨域桥（Python 无此显式桥）

上层桥：
  AI ←── FlattenedScreen(Vision)
  Graph ←── Operation(Common)
  StateMachine ←── (独立)
  Observability ←── (独立)
```

**无反向依赖。无循环依赖。但显式跨域桥比 Python 更多。**

---

## 7. P0 修复对依赖图的影响

当前（ ElementTypeMapper 返回 TypeHint）：
```
ElementTypeMapper ──→ TypeHint(Vision)    ← 显式跨域依赖
                    ──→ MenuItemType(Content)
                    ──→ ExpectedAction(Content)
```

修复后（ElementTypeMapper 返回 string）：
```
ElementTypeMapper ──→ MenuItemType(Content)    ← 保留
                    ──→ ExpectedAction(Content) ← 保留
                    ──→ TypeHint(Vision)         ← 仅通过 ToTypeHint(string) 便利方法（可选依赖）
```

修复后 ElementTypeMapper **不再以 TypeHint 作为核心输出类型**，桥从"显式 TypeHint 依赖"变为"中间字符串自含"——与 Python 的隐式语义耦合更对齐。ToTypeHint(string) 是可选便利方法，不影响核心映射链。

---

## 8. Phase 2 需关注的跨域桥

| 桥 | 当前状态 | Phase 2 行动 |
|----|----------|-------------|
| AI.PageAnalysis ↔ Content.PageAnalysis | 双版本共存 | 删除 AI 简化版，统一用 Domain 版 |
| Operation → TraversalNode | 已建立 | 确认 TraversalNode 是否应留在 Graph 或移入 Domain |
| ITraversalContext → IAIStrategyAdvisor | 已建立 | TraversalContext 需扩展（缺 5 个 Python 字段） |
| DynamicRule.match_condition → MenuItemType | 隐式（string "switch"） | Phase 2 实现匹配逻辑时需对齐词汇 |
| TraversalNode → Trace | 未实现 | Phase 2 实现 TraceRecorder 时需记录 node_id/action/target |
