# Python 架构地图（C# 重构参考）

> **日期**: 2026-07-01
> **分支**: `feature/refactor`（C# 重写）
> **状态**: 步骤 1 产出 — Python 现状梳理（模块级，非字段级）
> **数据来源**: `main` 分支（`git ls-tree` / `git grep` 导入分析）

---

## 1. 目的

为 C# 重写提供事实依据与核验标尺。本次目标为 **B：C# 重写、Python 仅作参考**
（允许 C# 惯用法重设计，不要求 JSON 互操作）。本文不展开到字段级，
只梳理 Python 的**模块职责**与**依赖方向**，作为后续 C# 分层设计的输入。

## 2. 顶层包清单（共 12 个）

| 包 | 职责 | 关键文件 | 内部依赖 |
|---|---|---|---|
| `models` | 领域数据：视觉模型、内容模型(PageAnalysis/PopupInfo/MenuItem)、element_type_mapper、traversal_context | `vision/*`, `content_models`, `element_type_mapper`, `traversal_context` | leaf（基本纯数据） |
| `graph` | 声明式遍历图：节点/计划/模板/动态匹配/编译 | `node`, `plan`, `template`, `matcher`, `compiler` | leaf |
| `trace` | 分布式追踪：span 树录制、运行时上下文、存储、指标、恢复 | `recorder`, `models`, `context`, `storage`, `metrics`, `recovery`, `analyzer` | leaf |
| `config` | 配置 | `settings` | leaf |
| `safety` | 安全筛选 | `filter` | leaf |
| `exception` | 异常链：处理/上下文/历史/初始化 | `chain`, `handlers`, `context`, `history`, `exceptions`, `initialization` | `models`, `adb` |
| `adb` | 设备控制 | `adb_client` | leaf |
| `state_machine` | 状态机：全局FSM/遍历FSM/节点栈/容器·错误·弹窗处理器 | `global_fsm`, `traversal_fsm`, `node_stack`, `container_handler`, `error_handler`, `popup_handler` | `graph`, `trace` |
| `ai` | AI 决策：顾问/多LLM provider/prompt/视觉服务 | `advisor`, `ai_types`, `providers/*`, `prompts/*`, `vision_service` | `models`, `graph` |
| `traversal` | 遍历引擎+子系统：图引擎/步骤编排/页面缓存·快照/计划校验/trace协调/动态子节点/入口策略 | `graph_engine`, `step_orchestrator`, `page_cache_manager`, `page_snapshot_manager`, `plan_validator`, `trace_coordinator`, `dynamic_child_manager`, `entry_policy_executor` | `graph`, `state_machine`, `trace`, `exception`, `models` |
| `simulation` | 离线测试台：mock动作/视觉、runner、操作执行器、滚动、状态夹具、行为校验 | `runner`, `mock_action`, `mock_vision`, `operation_executor`, `scroll/*`, `state_fixture`, `behavior_validator` | `ai`, `graph`, `models`, `trace`, `traversal`（顶层集成） |
| `analysis` | 事后分析：trace分析/指标/结果/树/服务 | `trace_analyzer`, `metrics`, `results`, `tree`, `server`, `structured_logging` | `trace` |

> `leaf` = 无内部包依赖（仅 stdlib/三方库）。

## 3. 依赖分层

```
L3  集成层     simulation（测试台）         analysis（事后分析）
                  ↑ 依赖几乎所有                ↑ 依赖 trace
L2  服务/引擎层  ai（AI决策）              traversal（遍历引擎+7子系统）
                  ↑ 依赖 models/graph         ↑ 依赖 graph/state_machine/trace/exception/models
L1  状态控制层   state_machine（全局FSM/遍历FSM/节点栈/3处理器）
                  ↑ 依赖 graph, trace
L0  基础层       models   graph   trace   config   safety   exception   adb
                  （纯数据/声明式图/追踪/配置/安全/异常/设备 — 基本无内部依赖）
```

**依赖方向总体**：L3 → L2 → L1 → L0（上层依赖下层）。`graph` 与 `trace` 是真·底层
（无内部依赖、被多处引用），适合做 C# 分层最底座。

## 4. 分层异味（对 C# 分层的关键输入）

1. **运行时上下文位置混乱**：`models/traversal_context.py` 与 `trace/context.py`
   （`TraversalRuntimeContext`）**两套**运行时上下文并存。若 `traversal_context`
   引用了 `graph`/`state_machine` 的类型，则底层 `models` 反向依赖上层
   ——Python 无分层强制所致。C# 里"运行时上下文"应明确归到 `state_machine`
   或 `traversal` 层，不放底层 Domain。
2. **`state_machine → trace`**：状态机为记 span 依赖 trace。C# 要决定：
   trace 作底层（可被 state_machine 依赖），还是 state_machine 通过
   观察者/`ITraceRecorder` 接口反向解耦。
3. **`exception → adb`**：异常处理反向依赖设备层 adb，方向可疑。
   C# 里 exception 应是基础层，不依赖 adb。
4. **死代码**：`ai/vision_service.py` 的 `from ..state.content_tree`
   （`src/state/` 不存在）——Python 端遗留，C# 不照搬。
5. **`graph`/`trace` 是真·底层**：两者无内部依赖、被多处引用，
   适合做 C# 分层最底座。

## 5. 后续

本文为四步流程的步骤 1。后续：
- **步骤 2**：基于本地图提出 C# 分层设计（命名空间/文件夹，PascalCase，C# 规范）。
- **步骤 3**：拿 C# 分层对照本地图核验覆盖面与职责切分，探讨合理性。
- **步骤 4**：针对细节出分阶段重构计划（每阶段 spec→plan→实现）。

---

## 6. 文件级梳理 — 阶段 1：核心领域模型（`models` 包）

> 为分阶段实施计划提供文件级参考。从 `models` 包（L0 纯领域数据）开始。
> 每个文件列出：main 上的代码路径、职责、关键类型、C# 现状（已移植位置 / 缺口）。
> C# 现状引用自 2026-06-30 的逐文件审计。

### 6.1 `vision/` 视觉模型

**`src/models/vision/bounding_box.py`** — 归一化边界框（`@dataclass(frozen=True)`，含范围/非零校验 + `to_dict`/`from_dict`）
- 关键类型：`BoundingBox`
- C# 现状：✅ `Domain/Models/Vision/BoundingBox.cs`；缺校验、`to_dict`/`from_dict`，多了 `BoundingBoxPixel`/`ToPixel`（Python 无）

**`src/models/vision/type_hint.py`** — 视觉元素类型枚举 + 模糊匹配（精确匹配→别名集合，未识别落 `TEXT`）
- 关键类型：`TypeHint(str, Enum)`；方法 `from_string`/`is_valid`/`values`
- C# 现状：✅ `Domain/Models/Vision/TypeHint.cs`；多了 `Unknown`，`FromString` 语义偏离（子串 vs 精确+别名），缺 `values`/`is_valid`

**`src/models/vision/selection_state.py`** — 选中/禁用状态枚举 + 模糊匹配
- 关键类型：`SelectionState(str, Enum)`；方法 `from_string`/`is_valid`/`values`
- C# 现状：✅ `Domain/Models/Vision/SelectionState.cs`；`FromString` 语义偏离，缺 `values`/`is_valid`

**`src/models/vision/region.py`** — 屏幕区域（`RegionRole` 为 `Literal` 字符串，含 `contains_point`）
- 关键类型：`Region`(`@dataclass(frozen=True)`)
- C# 现状：✅ `Domain/Models/Vision/Region.cs`；`to_dict` 键名 `width/height` 应为 `w/h`（往返断裂）

**`src/models/vision/flattened_element.py`** — 单个 UI 元素（`bbox` 可空且自动填 `0.001`，`confidence∈[0,1]` 校验）
- 关键类型：`FlattenedElement`(`@dataclass`)
- C# 现状：✅ `Domain/Models/Vision/FlattenedElement.cs`；`bbox` 默认/校验缺、`type_hint` 串化缺下划线

**`src/models/vision/screen_hints.py`** — 屏幕级提示（`extra` 为独立字段）
- 关键类型：`ScreenHints`(`@dataclass`)
- C# 现状：✅ `Domain/Models/Vision/ScreenHints.cs`；`extra` 被摊平到顶层（Python 嵌套在 `extra`）

**`src/models/vision/flattened_screen.py`** — 扁平化屏幕（构造时按 `y,x` 排序 elements；`screen_hints` 存原始 dict 懒解析）
- 关键类型：`FlattenedScreen`(`@dataclass`)
- C# 现状：✅ `Domain/Models/Vision/FlattenedScreen.cs`；缺自动排序、`screen_hints` 改强类型对象、`get_elements_by_type` 取 enum 而非字符串

### 6.2 内容/菜单结构模型（pydantic，**C# 完全未移植 — 重大缺口**）

**`src/models/content_models.py`** — 内容树/菜单/页面分析模型（pydantic `BaseModel`，自带校验）
- 关键类型：`Coordinate`、`Direction(str,Enum)`、`MenuInfo`、`MenuItemType(str,Enum)`、`ExpectedAction(str,Enum)`、`MenuItem`、`PopupInfo`、`PageAnalysis`、`VisitFingerprint`、`ContentNode`、`ContentTree`、`SimulationState`
- C# 现状：❌ 完全未移植。`PopupInfo`/`PageAnalysis` 在 C# `AI/IAIStrategyAdvisor.cs` 里有**简化版**（字段大量缺失）；其余类型无对应
- 注：pydantic 的声明式校验在 C# 需用 record + 构造校验或 `System.Text.Json` + 校验层替代

### 6.3 类型映射（**C# 完全未移植 — 重大缺口**）

**`src/models/element_type_mapper.py`** — android 控件类 → TypeHint / MenuItemType / ExpectedAction 的映射中枢
- 关键类型：`AndroidWidgetClass(str,Enum)`、`ElementTypeMapper`；函数 `map_android_class`、`to_menu_item_type`、`to_expected_action`
- C# 现状：❌ 完全未移植。这是"android 控件类→UI 类型"的核心真相源，C# 缺失导致类型映射无处安放

### 6.4 运行时上下文 + 杂项记录（**分层异味，C# 应拆分不照搬**）

**`src/models/traversal_context.py`** — 运行时上下文 + 一堆杂项记录类型混在一个文件
- 关键类型：`GlobalState(Enum)`、`PageCacheInfo`、`ErrorRecord`、`ActionRecord`、`TraversalContext`(`@dataclass(frozen=True)`)
- C# 现状：⚠️ 部分散落移植：`GlobalState`→`StateMachine/GlobalState.cs`（C# 已正确归类，是改进）、`TraversalContext`→`ITraversalContext`（StateMachine 层）、`ActionRecord`→`Traversal/IGraphTraversalEngine.cs`；`PageCacheInfo`/`ErrorRecord` 未明确
- **分层异味**：`GlobalState`（状态机枚举）、`PageCacheInfo`（页面缓存）、`ErrorRecord`/`ActionRecord`（trace/运行时记录）、`TraversalContext`（运行时上下文）**不该同处一个 models 文件**。Python 因无分层强制而堆一起；C# 必须按职责拆分到各自层（`GlobalState`→StateMachine、记录类→Observability/Trace、`TraversalContext`→StateMachine 或 Traversal 层）。

### 6.5 阶段 1 小结

- **已移植但有缺口**：`vision/*` 7 个文件（校验/序列化/语义偏离）
- **完全未移植**：`content_models.py`（12 类型）、`element_type_mapper.py`（映射中枢）
- **需拆分重定位**：`traversal_context.py`（杂货袋，按职责分散到各层）
- **对 C# 分层启示**：`models` 包并非"全部归 Domain 层"——其中 `GlobalState`/`PageCacheInfo`/`ErrorRecord`/`ActionRecord`/`TraversalContext` 应上移到 `StateMachine`/`Observability`/`Traversal` 层。C# 的 Domain 层只留纯领域数据（vision + content_models + element_type_mapper）。

> 下一阶段（阶段 2）将梳理 `graph` 包（声明式遍历图：node/plan/template/matcher/compiler）的文件级清单。
