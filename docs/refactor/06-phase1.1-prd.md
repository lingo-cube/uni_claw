# Phase 1.1 PRD：Domain 层修正 — ElementTypeMapper 分离 + FromString 对齐

> **版本**: 1.0
> **日期**: 2026-07-02
> **分支**: `feature/refactor`
> **前置**: Phase 1（已完成，185 测试全绿，87.8% 覆盖率）
> **关联**: [03-phase1-prd.md](03-phase1-prd.md)、[04-phase1-python-csharp-comparison.md](04-phase1-python-csharp-comparison.md)、[05-model-relationship-map.md](05-model-relationship-map.md)

---

## 1. 背景

Phase 1 完成后进行了 Python↔C# 全量对比审查（04 文档）和模型关系梳理（05 文档），发现 4 个必修问题：

1. **ElementTypeMapper 两套系统合二为一** — 视觉分类（TypeHint）和行为桥接（中间字符串）被压缩成一步映射，ToggleButton 映射链断裂
2. **FromString 用 Contains 子串匹配** — 违反 PRD §5.1 规定的「精确→别名→回落」算法，未知输入可能误命中而非回落
3. **DirectionExtensions.Values 硬编码** — 与同文件的 MenuItemType/ExpectedAction 反射模式不一致，新增值会遗漏
4. **IsValid(string) 缺失** — 上层无法区分合法 Text 和回落 Text

本期（Phase 1.1）修正这 4 项 + 2 项顺手防御（MapAndroidClass null 防御、IsValid(string) 自然附带）。不涉及新功能、不涉及 JSON 键名兼容。

---

## 2. 设计原则

| 原则 | 说明 |
|------|------|
| **只修不扩** | 不新增类型、不新增字段、不新增功能；只修正现有实现的正确性 |
| **Python 对齐** | 映射链和别名集以 Python 源码为基准，C# 合理扩展标注来源 |
| **测试增量** | 在 185 测试基础上增量，不删不改旧测试的正确断言 |
| **自顶向下** | 先修 ElementTypeMapper（架构桥），再修叶子类型（FromString/反射） |
| **不可变保持** | 所有 record 保持 sealed record class + ImmutableArray，不引入可变 |

---

## 3. 修正项

### 3.1 ElementTypeMapper：两套系统分离

#### 业务作用

ElementTypeMapper 是 **Android 控件→遍历决策** 的核心桥梁。当遍历引擎遇到一个 Android UI 控件，需要回答三个独立问题：

1. **这是什么控件？** → 视觉分类（TypeHint：告诉 AI "这是开关样式的控件"）
2. **该怎么操作它？** → 行为语义（ExpectedAction：Toggle——点击后切换状态）
3. **归类到哪种菜单项？** → 遍历策略（MenuItemType：Toggle——按 Toggle 节点模板处理）

当前 C# 把三个回答压缩成一步（直接映射到 TypeHint enum），丢失了中间层。后果：

- **ToggleButton 被归类为 Switch**（`TypeHint.Switch`）— 遍历引擎按 Switch 模板处理，但 ToggleButton 的行为语义是 Toggle。MenuItemType.Toggle 和 MenuItemType.Switch 对应不同的节点模板和状态恢复逻辑。
- **"menu_item"/"input" 被强行映射到 ClickableText/InputField** — 视觉上勉强对，但语义上不同。一个 "menu_item" 是导航型元素（ExpectedAction.NAVIGATE），不是简单可点击文字。

Python 的正确架构是**两级映射**：

```
Android控件 → ANDROID_CLASS_MAP → 中间字符串 → TYPE_TO_MENU_ITEM → MenuItemType
                                       ↓
                                   "toggle" → TOGGLE（独立行为映射）
                                   "switch" → SWITCH（独立行为映射）
                                   "menu_item" → MENU_ITEM（独立行为映射）
```

中间字符串层有 14 个值（含 `"toggle"`, `"menu_item"`, `"input"`），TypeHint enum 只有 8 个值。两者**不重叠**——TypeHint 是纯视觉外观分类，中间字符串是行为语义桥接。

#### 论证

**Python 源码验证**（`src/models/element_type_mapper.py`）：

```python
ANDROID_CLASS_MAP: Dict[str, str] = {
    "ToggleButton": "toggle",     # ← 独立于 TypeHint.SWITCH
    "TextView": "menu_item",      # ← 不在 TypeHint enum
    "EditText": "input",          # ← 不在 TypeHint enum
    ...
}

TYPE_TO_MENU_ITEM: Dict[str, MenuItemType] = {
    "toggle": MenuItemType.TOGGLE,     # ← 有独立下游映射
    "menu_item": MenuItemType.MENU_ITEM,
    ...
}
```

`from_android_class` 返回 `str`（中间字符串），不返回 TypeHint enum。`to_menu_item_type` 和 `to_expected_action` 用中间字符串做 key。

C# 当前 `MapAndroidClass` 返回 `TypeHint` enum，是唯一把两套系统合二为一的地方。`ToMenuItemType` 和 `ToExpectedAction` 已经用 string key（和 Python 一致），不受影响。

#### 改动

| 项目 | 当前 | 改为 |
|------|------|------|
| `MapAndroidClass` 返回类型 | `TypeHint` | `string` |
| 私有字典名 | `AndroidClassToTypeHintMap` | `AndroidClassMap` |
| 私有字典类型 | `Dictionary<string, TypeHint>` | `Dictionary<string, string>` |
| 公开属性 | `IReadOnlyDictionary<string, TypeHint> AndroidClassMap` | `IReadOnlyDictionary<string, string> AndroidClassMap` |
| 字典值 | TypeHint.Switch, TypeHint.Button, TypeHint.ClickableText, TypeHint.InputField, TypeHint.Slider | 逐行搬 Python ANDROID_CLASS_MAP 14 行：Switch→"switch", CheckBox→"switch", RadioButton→"switch", **ToggleButton→"toggle"**, Button→"button", ImageButton→"button", **TextView→"menu_item"**, **EditText→"input"**, LinearLayout→"menu_item", RelativeLayout→"menu_item", FrameLayout→"menu_item", ConstraintLayout→"menu_item", SeekBar→"slider", RatingBar→"slider" |
| 新增方法 | 无 | `public static TypeHint ToTypeHint(string typeString)` — 中间字符串→视觉分类 |
| 3 级匹配逻辑 | 精确→子串→fallback Button（保留） | 精确→子串→fallback `"button"`（逻辑不变，fallback 值从 TypeHint.Button 改为字符串 `"button"`） |
| null 防御 | 无（NullReferenceException） | throw `DomainValidationException(nameof(className), className)` |

`ToTypeHint(string)` 映射逻辑：

```csharp
"switch"    → TypeHint.Switch       // 视觉上 Switch 和 ToggleButton 都看起来像开关
"toggle"    → TypeHint.Switch       // ToggleButton 视觉外观 = Switch
"menu_item" → TypeHint.ClickableText // 菜单项看起来像可点击文字
"input"     → TypeHint.InputField   // 输入框
"slider"    → TypeHint.Slider       // 滑块
"button"    → TypeHint.Button       // 按钮
// 其他 → TypeHint.Text（回落）
```

这是**视觉外观→行为语义的反向映射**，供需要视觉分类的调用方使用。不影响核心映射链。

**不动的方法**：`ToMenuItemType`, `ToExpectedAction`, `IsValidType`, `IsValidAndroidClass` — 全部用 string key，不受返回类型变更影响。

---

### 3.2 TypeHint.FromString：精确别名+回落

#### 业务作用

`FromString` 解析 AI 视觉分析的输出。AI 分析手机屏幕后输出 JSON，每个元素有 `type_hint` 字段（如 `"button"`、`"switch"`）。FromString 把字符串转换为 TypeHint enum。

**核心问题**：AI 输出了不在已知枚举也不在已知别名集的字符串时（如 `"scrollable"`、`"dropdown"`），系统应该怎么处理？

- **当前 Contains 匹配**：`"scrollable"` 含 `"scroll"` → 误归类为 Slider → 遍历引擎按 Slider 模板处理 → **错误策略**，且不知道 AI 给出了意外值
- **精确别名+回落**：`"scrollable"` 不在别名字典 → 回落 Text → 遍历引擎知道这不是标准交互元素 → **正确策略**，能感知异常

**业务影响**：遍历引擎的节点模板选择依赖 TypeHint 正确分类。误分类 → 选错模板 → 执行错误操作 → 遍历失败或遗漏目标。

#### 论证

**Python 源码验证**（`src/models/vision/type_hint.py`）：

```python
@classmethod
def from_string(cls, value: str) -> 'TypeHint':
    value_lower = value.lower().strip()
    # 第一级：精确匹配枚举值
    try:
        return cls(value_lower)
    except ValueError:
        pass
    # 第二级：已知别名精确映射
    mapping = {
        'text': cls.TEXT, 'clickable': cls.CLICKABLE_TEXT,
        'click': cls.CLICKABLE_TEXT, 'toggle': cls.SWITCH,
        'checkbox': cls.SWITCH, 'check': cls.SWITCH,
        'btn': cls.BUTTON, 'input': cls.INPUT_FIELD,
        'field': cls.INPUT_FIELD, 'img': cls.IMAGE,
        'picture': cls.IMAGE,
    }
    return mapping.get(value_lower, cls.TEXT)  # 第三级：回落 TEXT
```

Python 的 `from_string` 是**两级字典查找 + 回落**，不是子串匹配。别名集共 7 条（不含精确枚举值的 8 条）。

C# 当前用 `Contains` 子串匹配，有两类偏差：
- **缺别名**：`"click"` → Python ClickableText，C# 落 Text；`"check"` → Python Switch，C# 落 Text
- **过度匹配**：`"scrollable"` → C# Slider（含 `"scroll"`），Python 回落 Text

#### 改动

替换 `Contains` switch expression 为 `Dictionary<string, TypeHint>` 别名字典：

```csharp
private static readonly Dictionary<string, TypeHint> AliasMap = new()
{
    // 精确枚举值（保证 ContainsKey 对所有合法输入返回 true）
    ["clickable_text"] = ClickableText,
    ["switch"]         = Switch,
    ["slider"]         = Slider,
    ["button"]         = Button,
    ["icon"]           = Icon,
    ["input_field"]    = InputField,
    ["text"]           = Text,
    ["image"]          = Image,

    // Python 别名（来源：TypeHint.from_string mapping dict）
    ["clickable"] = ClickableText,
    ["click"]     = ClickableText,     // Python 有，C# 当前缺 ← 补回
    ["toggle"]    = Switch,
    ["checkbox"]  = Switch,
    ["check"]     = Switch,            // Python 有，C# 当前缺 ← 补回
    ["btn"]       = Button,
    ["input"]     = InputField,
    ["field"]     = InputField,
    ["img"]       = Image,
    ["picture"]   = Image,

    // C# 扩展别名（不在 Python mapping dict 中，但来自实际 AI/Android 输入场景）
    ["seekbar"]   = Slider,            // Android SeekBar 的常见引用方式（AI 或 accessibility 可能输出）
    ["edit"]      = InputField,        // Android EditText 的常见简称
    ["textbox"]   = InputField,        // AI 可能输出的变体
    // 扩展别名判定标准：Python 不覆盖但 C# 遍历场景中实际遇到的输入变体
};

public static TypeHint FromString(string value)
{
    var key = value.ToLowerInvariant().Trim();
    if (AliasMap.TryGetValue(key, out var result))
        return result;
    return TypeHint.Text;  // 回落：未知值→Text
}
```

**语义保证**：已知别名精确命中（确定性），未知值回落 Text（可感知异常）。不含运气成分。

---

### 3.3 SelectionState.FromString：精确别名+回落

#### 业务作用

同 TypeHint — 解析 AI 输出的元素状态字符串（`"selected"`、`"active"`、`"gray"` 等）。误命中 vs 回落的业务影响相同：遍历引擎依赖正确状态判断元素是否可交互。

#### 论证

**Python 源码验证**（`src/models/vision/selection_state.py`）：

```python
selected_aliases = {'active', 'highlighted', 'highlight', 'checked'}  # 4 值
disabled_aliases = {'gray', 'grayed', 'dimmed', 'inactive', 'hidden'}  # 5 值
# 其他 → NORMAL（回落）
```

C# 当前 Contains 缺 **`"highlighted"`** 和 **`"highlight"`**（Python 有，C# 当前缺）。

#### 改动

替换 Contains switch expression 为别名集合 + 回落：

```csharp
private static readonly HashSet<string> SelectedAliases = new()
{
    "selected", "active", "checked", "highlight", "highlighted"
};

private static readonly HashSet<string> DisabledAliases = new()
{
    "disabled", "inactive", "hidden", "gray", "grayed", "dimmed"
};

public static SelectionState FromString(string value)
{
    var key = value.ToLowerInvariant().Trim();
    if (DisabledAliases.Contains(key))   return SelectionState.Disabled;   // 先查 Disabled（避免 "inactive" 被 "active" 匹配）
    if (SelectedAliases.Contains(key))    return SelectionState.Selected;
    return SelectionState.Normal;  // 回落
}
```

HashSet 比 Dictionary 更贴合 Python 的集合语义（两个别名集，不是键值映射）。

---

### 3.4 DirectionExtensions.Values 统一反射

#### 业务作用

Direction 描述菜单展开方向（Left/Right/Top/Bottom），用于 PageAnalysis 的 `Level1Dir`/`Level2Dir`。如果未来 AI 分析出新的布局方向，硬编码数组不会自动包含，导致 `FromValue` 无法解析新值。

#### 论证

Python `Direction.values()` 是动态的（`[e.value for e in cls]`）。C# MenuItemType/ExpectedAction 用反射动态构建，Direction 用硬编码。不一致意味着维护风险。

#### 改动

DirectionExtensions.Values 从 `new[] { "left", "right", "top", "bottom" }` 改为反射读取 `[JsonPropertyName]` 属性（Direction 已有 4 个 `[JsonPropertyName]`），与 MenuItemTypeExtensions/ExpectedActionExtensions 一致。

---

### 3.5 IsValid(string) 补 string 版

#### 业务作用

上层代码需要先验证字符串是否可解析，再决定是否调用 FromString。当前 `IsValid(TypeHint enum)` 只验证 enum 值范围，无法验证 string。

**示例**：上层收到 `"scrollable"` → 调用 FromString → 得到 Text（回落值）→ 上层不知道这是回落还是合法 Text。有了 `IsValid(string)` 后：`IsValid("scrollable")` → false → 上层明确走异常路径。

#### 论证

Python `TypeHint.is_valid(str)` 只检查枚举值字符串（`value.lower() in cls.values()`），不包括别名。C# 版检查别名字典（`AliasMap.ContainsKey`），语义更广但更实用。

**语义标注**：C# `IsValid(string)` 回答"这个 string 能否被 FromString 成功解析"（含别名），Python `is_valid(str)` 回答"这是否是标准枚举值字符串"（不含别名）。C# 版更实用——上层关心的是"能不能解析"，不是"是不是标准值"。

#### 改动

```csharp
// TypeHintExtensions
public static bool IsValid(string value) =>
    AliasMap.ContainsKey(value.ToLowerInvariant().Trim());

// SelectionStateExtensions
public static bool IsValid(string value)
{
    var key = value.ToLowerInvariant().Trim();
    return SelectedAliases.Contains(key) || DisabledAliases.Contains(key) || key == "normal";
}
```

---

### 3.6 MapAndroidClass null 防御

#### 业务作用

Python `from_android_class` 有 `TypeError` 检查（`not isinstance(class_name, str)`）。C# 当前无防御，null 输入触发 NullReferenceException——不结构化，调试困难。

#### 改动

`MapAndroidClass` 入口加：

```csharp
if (className is null)
    throw new DomainValidationException(nameof(className), className);
```

---

## 4. 修复顺序

自顶向下——先修架构桥，再修叶子类型：

```
1. ElementTypeMapper：MapAndroidClass 返回 string + 字典重建 + ToTypeHint + null 防御
2. TypeHint.FromString：Contains → 别名字典 + 回落
3. SelectionState.FromString：Contains → 别名集合 + 回落
4. DirectionExtensions.Values：硬编码 → 反射
5. IsValid(string)：两个 string 版
6. 测试更新
```

每步做完 `dotnet test`，在 185 测试基础上增量通过。

---

## 5. 成功标准

| 标准 | 验证方式 |
|------|----------|
| `dotnet build` 0 错误 0 警告 | CI |
| `dotnet test` 全绿（≥185） | CI |
| ToggleButton 映射链完整 | 测试：`MapAndroidClass("ToggleButton")` → `"toggle"` → `ToMenuItemType("toggle")` → `MenuItemType.Toggle` → `ToExpectedAction("toggle")` → `ExpectedAction.Toggle` |
| FromString 未知值回落不误命中 | 测试：`TypeHint.FromString("scrollable")` → Text（不是 Slider）；`SelectionState.FromString("activated")` → Normal（不是 Selected） |
| FromString 缺失别名补回 | 测试：`TypeHint.FromString("click")` → ClickableText；`TypeHint.FromString("check")` → Switch |
| SelectionState 缺失别名补回 | 测试：`SelectionState.FromString("highlighted")` → Selected |
| IsValid(string) 区分合法/回落 | 测试：`TypeHint.IsValid("button")` → true；`TypeHint.IsValid("scrollable")` → false |
| DirectionExtensions.Values 反射 | 测试：`DirectionExtensions.Values` 包含 4 值且与 `[JsonPropertyName]` 一致 |
| MapAndroidClass null 防御 | 测试：`MapAndroidClass(null)` throws DomainValidationException |
| TypeHint 8 值不变 | grep 确认 enum 无新增值 |

---

## 6. 不修项（已知限制，本期不阻塞）

| 项 | 原因 | 文档 |
|------|------|------|
| JSON 25+ 字段 snake_case↔camelCase | PRD §6 明确本期 camelCase | 04 §2.1 |
| null vs 空串/sentinel 默认值 | C# nullable 语义更清晰 | 04 §2.2 |
| Region.Id 不校验非空 | 低风险 | 04 §5.1 V8 |
| ContentNode.ToMarkdown() | 非关键功能，独立工作 | 04 §5.2 C7 |
| AI 层 PageAnalysis/PopupInfo 双版本 | Phase 2 删除简化版 | 05 §8.2 |
| EntryConfig 缺失 | Phase 2 | 05 §8.1 |
