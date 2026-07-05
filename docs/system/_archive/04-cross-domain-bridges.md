# Domain 跨域桥分析

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix 后）

---

## 1. Python 架构的桥设计

Python 的 3 个独立岛之间只有 **1 座隐式桥**：

```
ISLAND 1 (Vision)    ISLAND 2 (Content)    ISLAND 3 (Graph+Trace)
  TypeHint              MenuItemType          TraversalNode
  FlattenedElement      ExpectedAction        Operation
  ...                   ...                   ...

                      ElementTypeMapper ← 隐式桥
                      Vision type strings → ElementTypeMapper dicts
                      （零 import，共享字符串词汇表）
```

**隐式桥机制**：Vision 和 Content 之间 **零 import 依赖**，但ElementTypeMapper 的 `TYPE_TO_MENU_ITEM` 字典 key 使用与 TypeHint enum 值相同的字符串（"switch"、"button"），这是**共享词汇表**而非类型依赖。

---

## 2. C# 架构的桥设计（P0 fix 后）

C# 有 **4 座桥**：2 核心 + 1 可选 + 1 跨切面：

```
VISION               CONTENT               COMMON
  TypeHint              MenuItemType          OperationType
  ...                   ExpectedAction        ...

                      Mappings ← 显式桥
                      ──→ MenuItemType(Content)     ← 核心
                      ──→ ExpectedAction(Content)   ← 核心
                      ──→ TypeHint(Vision)          ← 可选（ToTypeHint）
                      ──→ DomainValidationException ← 跨切面

          DomainValidationException ← 跨切面桥（所有子域→root）
          DomainJsonOptions ← 跨切面桥（所有子域→root）
```

---

## 3. 桥清单与必要性论证

### 3.1 核心：Mappings → MenuItemType / ExpectedAction

| 属性 | 说明 |
|------|------|
| **桥方向** | Mappings → Content |
| **依赖机制** | `TypeToMenuItemTypeMap` 和 `TypeToExpectedActionMap` 字典值类型引用 |
| **必要性** | ✅ **不可消除** — ElementTypeMapper 的核心职责是将中间字符串映射为行为语义分类（MenuItemType + ExpectedAction）。没有这两个字典，映射器无法完成任何有意义的转换。 |
| **Python 对应** | Python 同样有这两座桥——`TYPE_TO_MENU_ITEM` 和 `TYPE_TO_EXPECTED_ACTION` 引用 `MenuItemType` 和 `ExpectedAction`。只是 Python 用隐式 import（同文件 content_models.py），C# 用显式 using。 |
| **风险** | 如果 MenuItemType 或 ExpectedAction 的值发生变化，ElementTypeMapper 的字典必须同步更新。这是 **可管理的耦合**——只有映射表需要同步。 |

### 3.2 可选：Mappings → TypeHint (ToTypeHint)

| 属性 | 说明 |
|------|------|
| **桥方向** | Mappings → Vision |
| **依赖机制** | `ToTypeHint(string)` 便利方法引用 TypeHint enum |
| **必要性** | ⚠️ **可消除** — 删除 ToTypeHint 方法即可完全切断此桥。调用方可直接用中间字符串，不需要 TypeHint。 |
| **保留理由** | 便利——调用方可能需要"这个中间字符串对应什么视觉外观"的快速查询。如果不加 ToTypeHint，调用方需要自己做映射（toggle→Switch, menu_item→ClickableText），映射规则非直觉且容易不一致。 |
| **Python 对应** | Python **没有此桥**——Python 没有中间字符串→TypeHint 的反向映射。TypeHint 和中间字符串是两套独立系统。 |
| **风险** | ToTypeHint 是 C# **新增的桥**（Python 不存在）。如果 TypeHint enum 增值，ToTypeHint 映射也必须同步。但 ToTypeHint 不是核心映射链的一部分——删除不影响任何链路。 |

### 3.3 跨切面：所有子域 → DomainValidationException

| 属性 | 说明 |
|------|------|
| **桥方向** | 单向：Vision/Content/Common/Mappings → root |
| **依赖机制** | 构造器中的校验 throw |
| **必要性** | ✅ **不可消除** — C# 不像 Python 用 ValueError（BCL）做校验。DomainValidationException 是 C# 的设计选择：更结构化（FieldName + IllegalValue），便于上层捕获和日志。 |
| **Python 对应** | Python 无此桥——Python 各模块各自抛 ValueError/TypeError（BCL），没有统一领域异常类。 |
| **风险** | 无反向依赖（root 不 import 任何子域），不引入循环。如果 DVE 的接口变更（加字段），所有子域的调用代码需要同步，但这是纯机械性变更。 |

### 3.4 跨切面：所有子域 → DomainJsonOptions

| 属性 | 说明 |
|------|------|
| **桥方向** | 单向：子域 → root |
| **依赖机制** | JSON 序列化时传入 options |
| **必要性** | ✅ **不可消除** — DomainJsonOptions 定义 camelCase + enum-as-string 策略，所有需要 JSON 的类型都依赖它。 |
| **Python 对应** | Python 无此桥——Pydantic BaseModel 自带序列化。 |
| **风险** | 同 DVE，无反向依赖，纯配置依赖。 |

---

## 4. 桥强度对比：Python vs C# (fix 后)

| 桥 | Python | C# (fix后) | 对齐度 |
|----|--------|------------|--------|
| ElementTypeMapper → MenuItemType | ✅ 隐式（同文件 import） | ✅ 显式（跨 namespace using） | **高** — 功能等价 |
| ElementTypeMapper → ExpectedAction | ✅ 隐式 | ✅ 显式 | **高** — 功能等价 |
| ElementTypeMapper → TypeHint | ❌ **不存在** | ⚠️ 可选（ToTypeHint） | **新增桥** — Python 无此连接 |
| 全域 → DomainValidationException | ❌ **不存在** | ✅ 跨切面 | **新增桥** — C# 设计选择 |
| 全域 → DomainJsonOptions | ❌ **不存在** | ✅ 跨切面 | **新增桥** — C# 技术需求 |
| Vision ↔ Content 隐式字符串耦合 | ✅ 共享词汇表 | ✅ 同样存在 | **高** — "switch"/"button"/"toggle" 等字符串是隐式共享词汇 |

**结论**：fix 后 C# 有 2 座**必要的**显式桥（同 Python）+ 1 座**可选的**新增桥（ToTypeHint）+ 2 座**技术性**跨切面桥（DVE + JsonOptions）。核心业务桥与 Python 对齐度 **高**，新增桥都有明确理由且可消除/可控制。

---

## 5. Phase 2 新增桥预测

| 新桥 | 方向 | 产生原因 | 预期必要性 |
|------|------|----------|-----------|
| FlattenedScreen → AI 层 | AI ← Vision | AI 策略需要视觉分析结果 | ✅ 不可消除 |
| Operation → TraversalNode | Graph ← Common | Graph 节点定义操作 | ✅ 不可消除 |
| MatchCondition.Type → MenuItemType | Graph ← Content | 匹配条件引用交互类型 | ⚠️ 当前是 string→enum 隐式桥，应显式化 |
| PageAnalysis → 上层遍历 | 上层 ← Content | 遍历逻辑消费页面结构 | ✅ 不可消除 |
| Template dict → Domain record | Graph ← Domain | 模板实例化需要类型转换 | ✅ Phase 2 必须实现 |

**Phase 2 桥设计建议**：每个新桥都应该先论证必要性，再决定是隐式（共享词汇表）还是显式（import/using）。优先选隐式桥——比显式桥耦合更松。
