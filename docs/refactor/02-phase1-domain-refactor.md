# Phase 1 重构：Domain 层（纯领域数据）

> **日期**: 2026-07-01
> **分支**: `feature/refactor`
> **状态**: 范围确立（待跨切面契约确认后细化任务清单）
> **依据**: [01-python-architecture-map.md](01-python-architecture-map.md) §6 阶段 1 文件梳理；目标 B（C# 重写、Python 仅参考）；策略 3（单项目 + 命名空间分层 + 自底向上）

---

## 1. 阶段目标

建立 C# **Domain 层**：纯领域数据、无内部依赖、无 I/O。作为后续
`StateMachine` / `Graph` / `Trace` / `Traversal` / `AI` 各层的基础。

具体：
- 修正现有 `vision/*` 的缺口（校验、序列化键名、`FromString` 语义）。
- 补齐 Python 有而 C# 缺的 `content_models`（12 类型）、`element_type_mapper`（映射中枢）。
- 确立 **序列化 + 校验** 跨切面契约，在 Domain 层统一应用。
- 清理推测性添加（多出枚举值、`BoundingBoxPixel`）。

## 2. 范围 IN

| Python 来源 | C# 目标 | 动作 |
|---|---|---|
| `models/vision/*`（7 类型） | `Domain/Models/Vision/` | 修正：补校验、序列化键名（`w/h`）、`FromString` 语义（精确+别名） |
| `models/content_models.py`（12 类型） | `Domain/Models/Content/` | 新移植：`Coordinate`/`Direction`/`MenuInfo`/`MenuItemType`/`ExpectedAction`/`MenuItem`/`PopupInfo`/`PageAnalysis`/`VisitFingerprint`/`ContentNode`/`ContentTree`/`SimulationState` |
| `models/element_type_mapper.py` | `Domain/Mappings/` | 新移植：`AndroidWidgetClass`/`ElementTypeMapper`/`map_android_class`/`to_menu_item_type`/`to_expected_action` |
| `graph/node.py` 中 `Operation`/`Target`/`RestoreAction` | `Domain/Models/Common/` | 修正：删多出枚举值、补校验 |
| —（跨切面） | Domain 层 | 确立序列化 + 校验契约并统一应用 |
| —（测试） | `tests/.../Domain/` | 单元测试：每类型 + 校验 + 序列化往返 |

## 3. 范围 OUT（推迟到后续阶段）

- `TraversalNode` / `TraversalPlan` / `Template` / `DynamicMatcher` / `EntryConfig` 等 Graph 复合类型 → **阶段 2 Graph 层**
- `GlobalState` / `TraversalContext` / `PageCacheInfo` / `ErrorRecord` / `ActionRecord`（运行时/状态/记录）→ 各自归属层（`StateMachine`/`Observability`/`Traversal`）
- `trace/` / `state_machine/` / `traversal/` / `ai/` 的实现 → 后续阶段

## 4. 边界决策（已定）

1. **`Operation`/`Target`/`RestoreAction` 归 `Domain/Models/Common/`**
   理由：纯数据（`action`/`target`/`params`/`restore`），无 graph 逻辑；Python 把它们塞 `graph/node.py` 仅因 `TraversalNode` 引用。C# 放 Domain 更惯用。

2. **删多出枚举值**：`OperationType.Wait`/`LongPress`、`TargetType.ResourceId`/`ElementType`
   理由：均无 Python 基础（Python `Operation.action`∈{click,swipe,back,input_text,no_action}；`Target.by`∈{text,coordinate,ui_index}）。"等待"由 `EntryConfig` 实现，非 Operation；`long_press` 全 src 无；`resource_id` 仅作 `popup_handler` 匹配启发式，非定位模式。对齐 Python 删除；将来引擎真需要时再加并标记意图。

3. **删 `BoundingBoxPixel`/`ToPixel`**
   理由：Python 全程归一化 `[0,1]`，仅在 ADB/动作边界转像素——像素转换是 I/O 关注点，不进 Domain。Domain 只留归一化 `BoundingBox`；像素转换留到 adb/action 层阶段。

## 5. 文件级工作清单

### 5.1 修正现有 `Domain/Models/Vision/`
- `BoundingBox.cs`：补范围/非零校验、`to_dict`/`from_dict`（键 `x,y,w,h`，`from_dict` 默认 `w/h=0.001`）；删 `BoundingBoxPixel`/`ToPixel`。
- `TypeHint.cs`：`FromString` 改"精确匹配→别名集合，未识别落 `Text`"；删 `Unknown`；补 `values`/`is_valid`。
- `SelectionState.cs`：`FromString` 改精确+别名集合（`checked`/`highlight`→Selected，`inactive`/`hidden`→Disabled）；补 `values`/`is_valid`。
- `Region.cs`：`to_dict` bounds 键改 `w/h`；`from_dict` 容错对齐（总返回对象）；`role` 用 `Literal` 等价（C# enum 或 string + 校验）。
- `FlattenedElement.cs`：`bbox` 可空且默认 `0.001`；`confidence∈[0,1]` 校验；`type_hint` 串化带下划线（`clickable_text`）。
- `ScreenHints.cs`：`extra` 作独立字段嵌套（不摊平）；`top_bar_text`/`layout_type` 默认 `""`/`"unknown"`。
- `FlattenedScreen.cs`：构造按 `y,x` 排序 elements；`screen_hints` 类型对齐 Python（原始 dict 懒解析 or 强类型——见 §6 决策）；`get_elements_by_type` 取字符串再 `from_string`。

### 5.2 新移植 `Domain/Models/Content/`（12 类型，来自 `content_models.py`）
- pydantic `BaseModel` → C# `record` + 构造校验（见 §6）。
- `PageAnalysis`/`PopupInfo` 注意：当前 C# `AI/IAIStrategyAdvisor.cs` 里有**简化版**，Phase 1 在 Content 层建完整版，AI 层后续引用（AI 层的简化版届时删除/替换）。

### 5.3 新移植 `Domain/Mappings/`（来自 `element_type_mapper.py`）
- `AndroidWidgetClass`(enum)、`ElementTypeMapper`(类)、`map_android_class`/`to_menu_item_type`/`to_expected_action`(函数 → C# 静态方法)。
- 依赖 `Content/MenuItemType`、`Content/ExpectedAction`、`Vision/TypeHint`（均在 Phase 1 内）。

### 5.4 修正 `Domain/Models/Common/`
- `Operation.cs`：`OperationType` 删 `Wait`/`LongPress`，对齐 5 值；补 `action` 集合校验；`params` 默认空 dict。
- `Target.cs`：`TargetType` 删 `ResourceId`/`ElementType`，对齐 3 值；补 `by` 集合校验。
- `RestoreAction.cs`：同 Operation 校验。

### 5.5 删除
- `BoundingBoxPixel`/`ToPixel`（在 `BoundingBox.cs` 内）。

## 6. 跨切面契约（Phase 1 内确立 — 已确认 A+A）

Phase 1 确立以下两个契约并统一应用于所有 Domain 类型（后续阶段沿用）：

- **序列化**：选 **A — `System.Text.Json` + 属性**（`[JsonPropertyName]` 等；枚举用 `JsonStringEnumConverter`，C# 自定串化形式）。
  - 理由：目标 B 不要求 Python 互操作，用 .NET 标准库即可；手写 `ToDictionary`/`FromDictionary`（B）是重造轮子。
- **校验**：选 **A — record 构造期校验，非法抛 `ArgumentException`**。
  - 理由：匹配 Python `__post_init__`（构造即校验）；`IValidatableObject`（B）是 opt-in、易漏，不适合领域不变量。
- **配套**：System.Text.Json 经 record 主构造反序列化 → **反序列化即校验**（非法 JSON 直接抛，不构造非法对象）。Python `from_dict` 的"缺字段默认值"用主构造参数默认值复现。
- **R8**：删所有手写 `ToDictionary`/`FromDictionary`，由 System.Text.Json 接管。

> 已确认。详见 [03-phase1-prd.md](03-phase1-prd.md) 的「契约需求」与「设计决策」节。

## 7. 命名空间/文件夹（C# 规范，PascalCase）

```
src/UniClaw.Core/Domain/
├─ Models/
│  ├─ Vision/      → UniClaw.Core.Domain.Models.Vision
│  ├─ Content/     → UniClaw.Core.Domain.Models.Content
│  └─ Common/      → UniClaw.Core.Domain.Models.Common
└─ Mappings/       → UniClaw.Core.Domain.Mappings
```

## 8. 出口标准

- `dotnet build` 0 错误 0 警告。
- `dotnet test` 全绿，Domain 层覆盖率 >80%。
- 序列化往返：每个 Domain 类型 `to_X → from_X` 一致。
- 校验：非法值抛异常（不静默构造非法对象）。
- 无 `BoundingBoxPixel`、无多出枚举值、无 `Unknown` TypeHint。
- `FlattenedScreen` 构造即排序；`ScreenHints.extra` 嵌套。

## 9. 后续

1. 确认 §6 跨切面契约（序列化/校验选型）。
2. 据此细化 Phase 1 任务清单 → 移交 `writing-plans` 出实现计划。
3. 阶段 2：`graph` 包文件级梳理 + Graph 层范围。
