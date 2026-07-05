# Phase 1 代码审计：Python ↔ C# Domain 模型对比

> **版本**: 1.0
> **日期**: 2026-07-02
> **分支**: `feature/refactor`
> **状态**: 记录 — 待实施
> **关联**: [03-phase1-prd.md](03-phase1-prd.md)

---

## 1. 审计方法

逐模块读取 Python 源码（main 分支）与 C# 实现（feature/refactor 分支），对比：

- **功能准确性**：字段/枚举/校验/默认值/别名映射是否一致
- **设计正确性**：C# 设计取舍是否符合 PRD 规格和 C# 语言惯例

---

## 2. 系统性问题

### 2.1 JSON 键名 snake_case ↔ camelCase 不兼容

Python pydantic 默认 snake_case，C# 全局 CamelCase 策略。25+ 个复合字段键名不同。

| 模块 | 受影响字段数 | 典型差异 |
|------|-------------|----------|
| BoundingBox | 2 | `w`/`h` → `width`/`height`（缩写→全称+大小写） |
| TypeHint enum | 2 | `clickable_text`/`input_field` → `clickableText`/`inputField` |
| FlattenedElement | 4 | `type_hint`/`bbox`/`selection_state`/`visual_state` → camelCase + 重命名 |
| ScreenHints | 2 | `top_bar_text`/`layout_type` → camelCase |
| FlattenedScreen | 1 | `screen_hints` → `screenHints` |
| MenuItem | 3 | `expected_action`/`expects_page_change`/`expects_state_change` |
| PopupInfo | 1 | `close_button` |
| PageAnalysis | 11 | 所有复合字段 |
| VisitFingerprint | 1 | `item_name` |
| ContentNode | 2 | `parent_id`/`node_type` |

**PRD 定位**：§6 明确「本期 camelCase，不背 Python snake_case」。Phase 2 跨语言交互时需加 `[JsonPropertyName]` 或切换策略。**已知限制，不阻塞。**

### 2.2 默认值 null vs 非-null 偏差

| 类型 | Python 默认 | C# 默认 | 语义差异 |
|------|------------|---------|----------|
| FlattenedElement.bbox | `BoundingBox(0,0,0.001,0.001)`（自动创建） | `null` | Python center() 总有值；C# 返回 nullable |
| FlattenedElement.visual_state | `{}` | `null` | Python 总有 dict；C# 需 null check |
| ScreenHints.top_bar_text | `""` | `null` | "空但存在" vs "缺失" |
| ScreenHints.layout_type | `"unknown"` | `null` | Python 有 sentinel；C# 丢失语义 |
| ScreenHints.extra | `{}` | `null` | 同 visual_state |
| string 字段（MenuInfo.Name 等） | 必填（pydantic 拒 null） | `null→string.Empty` 隐式 coerces | 验证软化 |

**C# 设计立场**：nullable 语义更清晰（"缺失" vs "空值"）。跨语言交互时需注意映射。**已知限制，Phase 2 视需求再定。**

---

## 3. TypeHint 与 ElementTypeMapper 架构问题

### 3.1 Python 的两套类型系统

Python 有**两套独立的类型系统**，TypeHint 只是其中一套：

**系统 1 — TypeHint（8 值，视觉分类）**

```python
class TypeHint(str, Enum):
    """Coarse-grained visual type hint for element classification.
    These types represent visual features observable in screenshots,
    without behavioral inference. They are output by the multimodal
    model and later mapped to precise MenuItemType by the text model.
    """
    CLICKABLE_TEXT = "clickable_text"
    SWITCH = "switch"
    SLIDER = "slider"
    BUTTON = "button"
    ICON = "icon"
    INPUT_FIELD = "input_field"
    TEXT = "text"
    IMAGE = "image"
```

用途：`FlattenedElement.type_hint`、`GetElementsByType`、`IsInteractive`——**纯视觉外观分类**。

**系统 2 — 中间类型字符串（14 值，Android→行为桥接）**

```python
ANDROID_CLASS_MAP = {
    "Switch": "switch", "CheckBox": "switch", "RadioButton": "switch",
    "ToggleButton": "toggle",           # ← 注意：独立于 TypeHint.SWITCH
    "Button": "button", "ImageButton": "button",
    "TextView": "menu_item",            # ← 注意：不在 TypeHint enum 里
    "EditText": "input",                # ← 注意：不在 TypeHint enum 里
    "LinearLayout": "menu_item", ...
    "SeekBar": "slider", "RatingBar": "slider",
}

TYPE_TO_MENU_ITEM = {
    "toggle": MenuItemType.TOGGLE,      # ← 有独立的行为映射
    "menu_item": MenuItemType.MENU_ITEM, # ← 有独立的行为映射
    ...
}
```

用途：Android 控件类 → 中间字符串 → MenuItemType / ExpectedAction——**行为语义桥接**。

**两套系统不重叠**：TypeHint 8 值里没有 `"toggle"`、`"menu_item"`、`"input"`。`from_android_class` 返回字符串，不经过 TypeHint enum。

### 3.2 C# 的合二为一问题

C# `MapAndroidClass` 返回 **TypeHint enum**，把两套系统合二为一：

```
C# 当前：
  "ToggleButton" → MapAndroidClass → TypeHint.Switch ← 错误
  因为 Python 的 "toggle" 中间字符串不在 TypeHint enum 里

C# 也丢失了：
  "menu_item" → TypeHint.ClickableText ← 视觉分类勉强可以
  "input" → TypeHint.InputField ← 同上
```

合二为一的结果：TypeHint 既要当**视觉分类**（8 值够用），又要当**Android→行为桥接**（需要 14 个中间类型），8 值不够覆盖。

### 3.3 设计决策：方案 B — 分离两套系统

**理由**（不是"对齐 Python"，而是 C# 自己的设计正确性）：

1. TypeHint docstring 明确说 **visual features observable in screenshots, without behavioral inference**。Toggle 和 Switch 在屏幕上**看起来一样**——视觉上不该分两个值。
2. `"menu_item"` 不是视觉外观——它是一个行为分类（"这东西在菜单里"）。塞进 TypeHint 违反自身定义。
3. 行为推断属于 ElementTypeMapper 的下游映射（MenuItemType / ExpectedAction），不属于 TypeHint。

**具体改动**：

| 改动项 | 当前 | 改为 |
|--------|------|------|
| `MapAndroidClass` 返回类型 | `TypeHint` | `string`（中间类型字符串） |
| `AndroidClassToTypeHintMap` 类型 | `Dictionary<string, TypeHint>` | `Dictionary<string, string>`，更名为 `AndroidClassMap` |
| 字典值 | TypeHint enum 值 | Python ANDROID_CLASS_MAP 逐行搬运（含 `"toggle"`、`"menu_item"`、`"input"`） |
| 新增便利方法 | 无 | `ToTypeHint(string)`：中间字符串→视觉分类（`"toggle"→Switch`，`"menu_item"→ClickableText`，`"input"→InputField`，回落→Text） |
| TypeHint enum | 8 值 | **8 值不变** |

**调用方完整链路**：

```csharp
string type = ElementTypeMapper.MapAndroidClass("ToggleButton");  // "toggle"
MenuItemType menu = ElementTypeMapper.ToMenuItemType(type);       // Toggle
ExpectedAction action = ElementTypeMapper.ToExpectedAction(type); // Toggle
TypeHint visual = ElementTypeMapper.ToTypeHint(type);             // Switch（可选）
```

`ToMenuItemType` 和 `ToExpectedAction` **完全不动**（它们本来就是 string key）。

---

## 4. FromString 算法对齐

### 4.1 Python 设计意图

`TypeHint.from_string` 的**两级算法**：

```python
def from_string(cls, value: str) -> 'TypeHint':
    # 第一级：精确匹配枚举值
    try:
        return cls(value_lower)
    except ValueError:
        pass

    # 第二级：已知别名精确映射
    mapping = {
        'clickable': cls.CLICKABLE_TEXT,
        'click': cls.CLICKABLE_TEXT,
        'toggle': cls.SWITCH,
        'checkbox': cls.SWITCH,
        'check': cls.SWITCH,
        'btn': cls.BUTTON,
        'input': cls.INPUT_FIELD,
        'field': cls.INPUT_FIELD,
        'img': cls.IMAGE,
        'picture': cls.IMAGE,
    }
    return mapping.get(value_lower, cls.TEXT)  # 回落 TEXT
```

**回落语义**：AI 输出不在枚举也不在别名集的字符串 → 回落 Text → 下游**感知到异常输入**。

C# 当前用 `Contains` 子串匹配，当 AI 输出 `"scrollable"`（不在任何别名集）时，Contains 碰巧命中 `"scroll"` → Slider，下游**不知道 AI 输出了意外值**。

### 4.2 修复方案

改为 Python 同构的**精确别名字典 + 回落**：

```csharp
private static readonly Dictionary<string, TypeHint> AliasMap = new()
{
    // 精确枚举值
    ["clickable_text"] = ClickableText,
    ["switch"] = Switch,
    ["slider"] = Slider,
    ["button"] = Button,
    ["icon"] = Icon,
    ["input_field"] = InputField,
    ["text"] = Text,
    ["image"] = Image,
    // 已知别名（来源：Python TypeHint.from_string mapping + C# 补充）
    ["clickable"] = ClickableText,
    ["click"] = ClickableText,
    ["toggle"] = Switch,
    ["checkbox"] = Switch,
    ["check"] = Switch,
    ["btn"] = Button,
    ["input"] = InputField,
    ["field"] = InputField,
    ["img"] = Image,
    ["picture"] = Image,
    // C# 扩展别名（不在 Python 中，标注）
    ["seekbar"] = Slider,       // Android SeekBar 常见拼写
    ["edit"] = InputField,      // Android EditText 常见引用
    ["textbox"] = InputField,   // 变体
};

public static TypeHint FromString(string value)
{
    if (AliasMap.TryGetValue(value.ToLowerInvariant().Trim(), out var result))
        return result;
    return TypeHint.Text;  // 回落：未知值→Text，下游可感知异常
}
```

同理改 `SelectionState.FromString`（Python 用 `selected_aliases` / `disabled_aliases` 两个集合）。

### 4.3 补 `IsValid(string)` 方法

当前 C# `IsValid` 只验证 enum 值范围，不验证 string。补 string 版：

```csharp
public static bool IsValid(string value) =>
    AliasMap.ContainsKey(value.ToLowerInvariant().Trim());
```

同理补 `SelectionState.IsValid(string)`。

---

## 5. 按模块的具体问题

### 5.1 Vision（7 类型）

| # | 类型 | 问题 | 分类 | 严重度 |
|---|------|------|------|--------|
| V1 | BoundingBox | JSON 键 `w`/`h` → `width`/`height` | 已知限制（wire 不兼容） | P1-defer |
| V2 | TypeHint enum | `clickable_text`/`input_field` 序列化 camelCase | 已知限制 | P1-defer |
| V3 | TypeHint.FromString | Contains 子串匹配 vs PRD 规定「精确→别名→回落」 | 功能偏差 | **P2-修** |
| V4 | TypeHint | 缺 `IsValid(string)` string 版 | 缺方法 | P3-修 |
| V5 | TypeHint | `Values` 返回 enum 列表而非 string 列表 | 签名差异 | P3-defer |
| V6 | SelectionState.FromString | 同 V3 | 功能偏差 | P2-修 |
| V7 | SelectionState | 缺 `values()` / `IsValid(string)` | 缺方法 | P3-修 |
| V8 | Region | 不校验 `Id` 非空（Python 校验） | 验证软化 | P3-defer |
| V9 | FlattenedElement | bbox null vs Python 自动创建默认 bbox | 默认值差异 | P2-defer |
| V10 | FlattenedElement | `visual_state` null vs `{}` | 默认值差异 | P3-defer |
| V11 | FlattenedElement | JSON 键 `bbox` → `boundingBox` | 已知限制 | P1-defer |
| V12 | FlattenedScreen | `screen_hints` 存为 `ScreenHints?` vs Python `Dict` | 正确升级 | ✅ |
| V13 | FlattenedScreen | 排序 `BoundingBox?.Y ?? 0.0` vs Python `e.bbox.y`（永远非 null） | null 排序差异 | P3-defer |
| V14 | FlattenedScreen | 缺 `set_screen_hints`（C# 用 `with` 替代） | 不可变替代 | ✅ |
| V15 | FlattenedScreen | `GetElementsByType` 接收 TypeHint vs Python 接收 string | 更类型安全 | ✅ |

### 5.2 Content（10 类型）

| # | 类型 | 问题 | 分类 | 严重度 |
|---|------|------|------|--------|
| C1 | Direction | `Values` 硬编码 vs MenuItemType/ExpectedAction 用反射 | 设计不一致 | P2-修 |
| C2 | MenuInfo | `Name ?? string.Empty` 验证软化 | 验证软化 | P3-defer |
| C3 | MenuItem | 3 个 snake_case JSON 键不兼容 | 已知限制 | P1-defer |
| C4 | PopupInfo | `close_button` → `closeButton` | 已知限制 | P1-defer |
| C5 | PageAnalysis | 11 个 snake_case JSON 键不兼容 | 已知限制 | P1-defer |
| C6 | VisitFingerprint | `FromString`/`ToString` 逻辑一致 | ✅ 正确 | — |
| C7 | ContentNode | 缺 `ToMarkdown()` | 缺方法 | P3-修 |
| C8 | ContentNode | `parent_id`/`node_type` 键不兼容 | 已知限制 | P1-defer |
| C9 | ContentTree | 整体缺失 | PRD 明确 defer Phase 2 | ✅ |
| C10 | SimulationState | 整体缺失 | PRD 明确 defer | ✅ |

### 5.3 Common（3 类型）

| # | 类型 | 问题 | 分类 | 严重度 |
|---|------|------|------|--------|
| O1 | Operation | 字段/枚举完全匹配 Python | ✅ 正确 | — |
| O2 | Operation | Wait/LongPress Python 本身也没有 | ✅ 正确删除 | — |
| O3 | Target | ResourceId/ElementType Python 本身也没有 | ✅ 正确删除 | — |
| O4 | Target | `Value ?? string.Empty` vs Python 无 coerces | 验证软化 | P3-defer |
| O5 | RestoreAction | 完全匹配 Python | ✅ 正确 | — |

### 5.4 Mappings（2 类型）

| # | 类型 | 问题 | 分类 | 严重度 |
|---|------|------|------|--------|
| M1 | AndroidWidgetClass | 14 值完全匹配 Python | ✅ 正确 | — |
| M2 | AndroidWidgetClass | int enum 丢失完整 Android 类路径字符串 | 设计取舍 | P3-defer |
| M3 | ElementTypeMapper | ToggleButton 映射偏差（两套系统合二为一） | **架构问题** | **P0-修** |
| M4 | ElementTypeMapper | `MapAndroidClass(null)` 无类型检查 | 缺防御 | P3-修 |
| M5 | ElementTypeMapper | 缺 `ValidateAndConvert` | 功能偏差 | P3-defer |

---

## 6. 修复清单

### P0 — ElementTypeMapper 两套系统分离

| 文件 | 改动 |
|------|------|
| `ElementTypeMapper.cs` | `MapAndroidClass` 返回 `string`；字典改为 `Dictionary<string, string>` 逐行搬运 Python `ANDROID_CLASS_MAP`；新增 `ToTypeHint(string)` 便利方法 |
| `AndroidWidgetClass.cs` | 不动 |
| ElementTypeMapper 测试 | 更新：全表扫描改用 string 断言；加 ToggleButton→"toggle"→MenuItemType.Toggle 全链路测试 |
| TypeHint.cs | **8 值不变** |

### P2 — FromString 改精确别名+回落

| 文件 | 改动 |
|------|------|
| `TypeHint.cs` (Extensions) | `Contains` switch → `Dictionary<string, TypeHint>` 别名字典 + 回落 Text |
| `SelectionState.cs` (Extensions) | `Contains` switch → `selected_aliases` / `disabled_aliases` 集合 + 回落 Normal |
| 测试 | 更新 FromString 测试：验证精确匹配、每个别名、回落路径；验证 `"scrollable"` 等未知值回落而非误命中 |

### P2 — Direction 加 `[JsonPropertyName]` + 反射统一

| 文件 | 改动 |
|------|------|
| `Direction` enum | 加 `[JsonPropertyName("left")]` 等 4 个属性 |
| `DirectionExtensions` | `Values` 从硬编码改为反射读取 `[JsonPropertyName]`，与 MenuItemType/ExpectedAction 一致 |

### P3 — 补缺失方法

| 文件 | 改动 |
|------|------|
| `TypeHintExtensions` | 加 `IsValid(string value)` string 版 |
| `SelectionStateExtensions` | 加 `IsValid(string value)` string 版 |
| `ElementTypeMapper` | 加 `MapAndroidClass` null/非 string 输入防御（throw `DomainValidationException`） |
| `ContentNode` | 加 `ToMarkdown()` 移植 Python `to_markdown()` |

### 不修复（已知限制/合理取舍）

| 项 | 理由 |
|------|------|
| JSON 键名 snake_case ↔ camelCase | PRD §6 明确本期 camelCase；Phase 2 加 `[JsonPropertyName]` |
| null vs 空串/sentinel 默认值 | C# nullable 语义更清晰；Phase 2 视交互需求再定 |
| `to_dict`/`from_dict` 缺失 | PRD 明确禁止 |
| `set_screen_hints()` 缺失 | C# 不可变设计用 `with` 替代 |
| Region `Id` 不校验非空 | 低风险；Phase 2 可加 |
| `Target.Value ?? string.Empty` | 低影响 |

---

## 7. 修复顺序

```
1. P0: ElementTypeMapper 两套系统分离                    ← 架构问题，必须先做
2. P2: FromString 改精确别名+回落 (TypeHint + SelectionState)
3. P2: Direction 加 [JsonPropertyName] + 反射统一
4. P3: IsValid(string) + MapAndroidClass 防御 + ContentNode.ToMarkdown()
```

每步做完 `dotnet test`，确保 185 测试基础上增量通过。

---

## 8. 上层代码对照（Phase 1 不涉及，记录备查）

### 8.1 Graph/Node → C# Graph/Models/

Python `node.py` (742 行) 定义 21 个类型，C# 基本对齐：

| Python 类型 | C# 对应 | 状态 |
|-------------|---------|------|
| NodeType (8值) | NodeType (8值) | ✅ 值一致 |
| ExitConditionType | ExitConditionType | ✅ |
| FallbackAction | FallbackAction | ✅ |
| CompletionPolicyType | CompletionPolicyType | ✅ |
| TargetFoundAction | TargetFoundAction | ✅ |
| MatchMode | MatchMode | ✅ |
| EntryStrategy | EntryStrategy | ✅ |
| TraversalMode | TraversalMode | ✅ |
| ChildrenStrategyType | ChildrenStrategyType | ✅ |
| Target | Target (Domain/Common) | ✅ |
| RestoreAction | RestoreAction (Domain/Common) | ✅ |
| Operation | Operation (Domain/Common) | ✅ |
| Precondition | Precondition | ✅ |
| DynamicRule | DynamicRule | ✅ — Python action 是 string，C# 用 MatchAction enum（类型安全升级） |
| ChildrenStrategy | ChildrenStrategy | ✅ — Python static_children 是 List，C# 是 List\<string\>? |
| ErrorPolicy | ErrorPolicy | ✅ — Python on_error 是 string，C# 用 ErrorPolicyType enum |
| ExitCondition | ExitCondition | ✅ |
| CompletionPolicy | CompletionPolicy | ✅ — Python match_mode 默认 CONTAINS，C# 默认 Exact（⚠️ 默认值偏差） |
| EntryPolicy | EntryPolicy | ✅ |
| IntentSlots | IntentSlots | ✅ — Python 全部 Optional，C# 有 required TargetApp/Scope（⚠️ 必填偏差） |
| TraversalNode | TraversalNode | ✅ |
| **EntryConfig** | **❌ 缺失** | Python 有（wait_mode/wait_timeout/wait_interval/action_delay_ms/trace_level 含构造校验），C# 无 |

**注意**：CompletionPolicy 默认值偏差（Python `MatchMode.CONTAINS` vs C# `MatchMode.Exact`）和 IntentSlots 必填偏差（Python 全部 Optional vs C# TargetApp/Scope required）需在 Phase 2 审查。

### 8.2 AI Layer — PageAnalysis/PopupInfo 双版本冲突

C# 同时存在两个 `PageAnalysis` 和两个 `PopupInfo`：

1. **Domain 版**（Content 层）：完整版 — 12 字段 PageAnalysis + 3 字段 PopupInfo
2. **AI 层简化版**：3 字段 PageAnalysis（FlattenedScreen, Path, PopupInfo?）+ 3 字段 PopupInfo（Detected, CloseButton?, Message?）

PRD §5.2 明确：AI 层简化版后续阶段替换（单一源原则）。当前两版共存是 **已知冲突**，Phase 2 应删除 AI 层简化版。

AI 层其他类型（DecisionResult, NodeData, ContainerInference, SafetyScreeningResult 等）属 Phase 3+。

### 8.3 TraversalContext 对比

Python `TraversalContext` (frozen dataclass, 11 字段) vs C# `ITraversalContext` (interface, 8 属性)：

| Python 字段 | C# ITraversalContext | 差异 |
|-------------|----------------------|------|
| node_stack: List[str] | NodeStack: INodeStack | ⚠️ C# 用接口 |
| current_path: List[str] | CurrentPath: List\<string\> | ✅ |
| visited_pages: Set[str] | VisitedPages: Dict\<string,object\> | ⚠️ 类型不同 |
| **failed_nodes** | **❌ 无** | 缺失 |
| **action_history** | **❌ 无** | 缺失 |
| **inference_history** | **❌ 无** | 缺失 |
| **goal_attempts** | **❌ 无** | 缺失 |
| **page_cache** | **❌ 无** | 缺失 |
| visited_nodes: Set[str] | ❌ 无 | 缺失 |
| step_count: int | StepCount: int | ✅ |
| global_state: GlobalState | GlobalState: GlobalState | ✅ |

C# 更精简，缺少 5 个运行时状态字段。Phase 2 决策是否扩展。

### 8.4 GlobalState 对比

| Python (6值) | C# (8值) | 备注 |
|---------------|----------|------|
| IDLE | Idle | ✅ |
| — | **Initializing** | C# 新增 |
| TRAVERSING | Traversing | ✅ |
| PAUSED | Paused | ✅ |
| ERROR | Error | ✅ |
| — | **Recovering** | C# 新增 |
| COMPLETED | Completed | ✅ |
| TERMINATED | Terminated | ✅ |

Initializing 和 Recovering 是合理的 C# 扩展（Python 用其他机制处理这些状态）。

### 8.5 ActionRecord 对比

| Python | C# | 差异 |
|--------|-----|------|
| action_type: str | Action: string | ✅ 字段名不同 |
| target: Optional[str] | ❌ 无 target | 缺失 |
| timestamp: datetime | Timestamp: DateTimeOffset | ✅ 类型升级 |
| result: Optional[str] | ❌ 无 result | 缺失 |
| — | **Parameters: Dict\<string,object\>** | C# 新增 |
| — | **Success: bool** | C# 新增 |

字段语义不完全对齐。Phase 2 需统一。

### 8.6 Trace/Observability 对比

Python `trace/models.py` 有分布式追踪模型（TraceNode, SessionNode, StepNode, SpanNode 等，含 ULID 生成）。C# `Observability/ITraceRecorder.cs` 有简化版 record 类型（TraceSession, StateTransition 等）和接口。

两者不是 1:1 对应——C# 是重新设计而非移植。Phase 2 需确认是否沿用 C# 简化设计还是补齐 Python 追踪能力。
