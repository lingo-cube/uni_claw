# PRD: C# 重构 Phase 1 — Domain 核心模型建立

> **版本**: 1.3 (Draft)
> **日期**: 2026-07-01
> **分支**: `feature/refactor`
> **状态**: 草案（待评审 → 细化任务清单 → writing-plans）
> **关联**: [01-python-architecture-map.md](01-python-architecture-map.md)、[02-phase1-domain-refactor.md](02-phase1-domain-refactor.md)
> **本期重心**：**核心模型的建立**（类型/字段/不变量/不可变/领域解析/归属）。序列化降为最小可用，**不引入 DTO**，复杂持久化/往返 defer 到后续有需求时再议。

---

## 1. 目标运行时与依赖

- **运行时**：.NET 8 (LTS)。
- **依赖**：仅 BCL（`System.Text.Json` 等系统库）；不依赖任何上层包、不依赖三方领域库。
- **Domain 层定位**：纯领域数据 + 领域不变量 + 领域解析；**无 I/O**、**不依赖上层**。

## 2. Domain 内部分层与依赖方向

```
Domain/
├─ Models/
│  ├─ Vision/      （视觉模型）
│  ├─ Content/     （内容/菜单结构模型）
│  └─ Common/      （Operations: Operation/Target/RestoreAction）
└─ Mappings/       （ElementTypeMapper 等纯领域映射）
```

**依赖方向**：`Mappings → Models.{Content,Vision}`；`Models` 内部不跨子域反向；**Domain 整体不依赖任何上层**（StateMachine/Graph/Trace/...），同层单向、无循环。

> 注：`Common` 与 `Mappings` 的命名/位置可在实现期微调（如改 `Operations/`、`Services/`），不阻塞本期。

## 3. 设计原则

| 原则 | 说明 |
|------|------|
| **接口优先** | 核心能力定义为接口；Domain 之上各层依赖接口契约（Domain 本身是 record/enum，无"实现"） |
| **类型安全** | C# 强类型 + record；领域数据优先具体类型，`object?` 仅限真正不透明处 |
| **不可变性** | `readonly record struct` / `sealed record class`；集合用 `ImmutableArray<T>` |
| **依赖注入** | 构造函数注入依赖；不用静态单例/服务定位 |
| **高内聚低耦合** | 模块化、组件化、接口化；按职责切分，单向依赖，无循环 |

## 4. 反原则（明确不做）

1. 不在 Domain 做 I/O（文件/DB/网络/ADB/像素转换）。
2. 不让 Domain 反向依赖上层（只依赖 BCL）。
3. 不静默构造非法对象（校验即抛 fail-fast）。
4. 不写手写 JSON 序列化（不写 `ToDictionary`/`FromDictionary`）；**本期不引入 DTO**。
5. 不引入无业务基础的冗余概念（YAGNI）；但**鼓励**用 C# 语言特性（nullable/`init`+`required`/`[Flags]`/泛型约束）提升类型安全，超出 Python 的扩展加注释标记意图。
6. 不跨命名空间重复定义同名类型（一类型一位置）。
7. 不照搬 Python 分层混乱（杂货袋文件按职责拆分重定位，不 1:1 镜像）。
8. 不用可变集合/可变状态做 Domain（`ImmutableArray<T>`，不暴露 `List`/`IList`）。
9. 不依赖具体类（跨层，约束上层各阶段）。
10. **不给领域数据 record 抽接口**（接口仅用于行为/服务；数据类型是唯一源，上层直接引用、不复制简化版）。

## 5. 核心模型清单与归属

> 逐类型详细字段/动作见 [02 §5](02-phase1-domain-refactor.md)。此处列模型清单与关键不变量。

### 5.1 `Models/Vision/`（修正现有 7 类型）
- `BoundingBox`：归一化 `[0,1]`，`w/h>0`；删 `BoundingBoxPixel`/`ToPixel`。
- `TypeHint`(enum)：`FromString` 精确匹配→别名集合→未识别落 `Text`；删 `Unknown`；补 `Values`/`IsValid`。
- `SelectionState`(enum)：`FromString` 精确+别名（`checked`/`highlight`→Selected；`inactive`/`hidden`→Disabled）。
- `Region`：`id`/`bounds`/`role`；`role` 受限集合。
- `FlattenedElement`：`bbox` 可空默认 `0.001`；`confidence∈[0,1]`。
- `ScreenHints`：`extra` 独立嵌套字段。
- `FlattenedScreen`：`elements` 构造即按 `(y,x)` 排序；集合 `ImmutableArray`。

### 5.2 `Models/Content/`（新移植 12 类型，来自 `content_models.py`）
`Coordinate`/`Direction`/`MenuInfo`/`MenuItemType`/`ExpectedAction`/`MenuItem`/`PopupInfo`/`PageAnalysis`/`VisitFingerprint`/`ContentNode`/`ContentTree`/`SimulationState`。
- pydantic `BaseModel` → C# `record` + 构造校验。
- `PageAnalysis`/`PopupInfo` 在 `AI/IAIStrategyAdvisor.cs` 有简化版（R-4）；本期 Content 层建完整版，AI 层简化版后续阶段替换（**单一源原则**，不并行存在）。

### 5.3 `Models/Common/`（修正 3 类型）
- `Operation`：`action`∈{click,swipe,back,input_text,no_action}（删 `Wait`/`LongPress`）；`params` 默认空。
- `Target`：`by`∈{text,coordinate,ui_index}（删 `ResourceId`/`ElementType`）。
- `RestoreAction`：同 Operation 校验。

### 5.4 `Mappings/`（新移植，来自 `element_type_mapper.py`）
`AndroidWidgetClass`(enum)、`ElementTypeMapper`(类)、`map_android_class`/`to_menu_item_type`/`to_expected_action`(→C# 静态方法)。
- 依赖 `Models.Content`（`MenuItemType`/`ExpectedAction`）与 `Models.Vision`（`TypeHint`）。

## 6. 模型不变量与跨切面

- **校验**：record 构造期校验，非法抛 `DomainValidationException`（带字段名+非法值），fail-fast；不静默构造非法对象。
- **不可变**：`readonly record struct` / `sealed record class`；集合字段 `ImmutableArray<T>`，`with` 表达式产生独立副本。
- **领域解析**：`FromString`/别名映射等保留为**静态方法**（领域语义，非序列化），单独测试。
- **序列化（最小·单向）**：本期序列化**仅保证对象可输出为 JSON（单向）**，不保证从 JSON 安全重建对象。record 主构造校验会使反序列化在缺字段/非法值时直接抛异常——此为**已知限制**，待有持久化需求时统一解决（届时引入 DTO/工厂或放宽校验）。删手写 `to_dict`/`from_dict`；**不引入 DTO**。
- **JSON 键名**：camelCase（全局 `PropertyNameCasePolicy=CamelCase`），`[JsonPropertyName]` 仅覆盖；不背 Python snake_case。

## 7. 测试契约（模型正确性）

1. **构造校验**：每类型列出所有非法输入组合（零/负宽高、`confidence` 越界、空枚举、非法 `action`/`by`/`role` 等）→ 断言抛 `DomainValidationException` 且含字段名。
2. **领域解析**：所有 `FromString` 覆盖精确匹配、别名、未识别回落路径。
3. **不可变性**：含集合的 record 执行 `with`，验证集合副本独立（`ImmutableArray`）。
4. **映射表**：`element_type_mapper` 的 android 控件类→类型映射**全表扫描**测试。
5. **序列化（单向）**：仅测对象→JSON 可输出；**不测 JSON→对象往返**（已知限制，defer）。

## 8. Spike（模型模式验证，关 R-3/R-5）

Phase 1 启动先做 3 个代表类型全链路（**不含 DTO / 反序列化往返**）：
- `BoundingBox`：构造期校验抛 `DomainValidationException`。
- `FlattenedScreen`：`ImmutableArray` + 构造排序 + `with` 副本独立。
- 一个 enum（如 `TypeHint`）：`FromString` 精确/别名/回落。
验：构造期抛行为（.NET 8）、`ImmutableArray` 不可变性、`FromString` 路径正确。

## 9. 风险

- **R-1**：`content_models` 12 类型工作量大。
- **R-2**：`element_type_mapper` 映射表数据量大，需逐一核对。
- **R-3**：.NET 8 构造期抛异常行为需 Spike 验证。
- **R-5**：`ImmutableArray` 在 `with`/序列化的行为需 Spike 验证。
- **R-4**：`PageAnalysis`/`PopupInfo` 与 AI 层简化版短期并存，后续替换。
- **R-6**：本期序列化仅单向（对象→JSON）；后续阶段若有持久化/配置加载需求，需统一解决 JSON→对象重建（届时引入 DTO/工厂或放宽构造校验）。

## 10. 成功标准 / 出口

- `dotnet build` 0 错误 0 警告。
- `dotnet test` 全绿，Domain 层覆盖率 >80%。
- 校验：非法值构造即抛 `DomainValidationException`（不静默）。
- 不可变：集合为 `ImmutableArray`，`with` 副本独立。
- 无 `BoundingBoxPixel`、无多出枚举值（`Wait`/`LongPress`/`ResourceId`/`ElementType`）、无 `TypeHint.Unknown`。
- `FlattenedScreen` 构造即排序；`ScreenHints.Extra` 嵌套。
- 序列化：对象→JSON 可输出（**单向**）；JSON→对象重建不保证（已知限制，defer）。

## 11. 范围与依赖

- **依赖**：无（Domain 是底层）。
- **被依赖**：阶段 2 Graph 层引用 `Common`（Operation/Target/RestoreAction）；`Mappings` 依赖 `Content`/`Vision`（均在期内）。

## 12. 后续

1. 评审本 PRD。
2. 细化 Phase 1 任务清单（按文件/类型）→ 移交 `writing-plans`。
3. 阶段 2：`graph` 包文件级梳理 + Graph 层 PRD。
