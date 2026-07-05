# Domain 序列化契约

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix 后）

---

## 1. 序列化策略总览

Domain 层使用 `System.Text.Json` + `DomainJsonOptions` 统一序列化：

```csharp
DomainJsonOptions.Default = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,    // camelCase 键名
    Converters = { new JsonStringEnumConverter() },       // enum → string 值
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // null 字段跳过
};
```

**关键决策**：
- **camelCase** 键名（PRD §6: "本期 camelCase，不背 Python snake_case"）
- **enum-as-string**（JsonStringEnumConverter，而非整数）
- **null 跳过**（WhenWritingNull，而非写 null）

---

## 2. 各类型序列化行为表

### Enum 类型

| Enum | 序列化方式 | 示例输出 | Python 对应键名 | 键名对齐? |
|------|-----------|----------|----------------|----------|
| **TypeHint** | camelCase enum 名（全局策略） | `"clickableText"` | `"clickable_text"` | ❌ snake_case vs camelCase |
| **SelectionState** | camelCase enum 名 | `"selected"` | `"selected"` | ✅ 单词无下划线 |
| **Direction** | `[JsonPropertyName]` 显式 | `"left"` | `"left"` | ✅ |
| **MenuItemType** | `[JsonPropertyName]` 显式 | `"menu_item"` | `"menu_item"` | ✅ |
| **ExpectedAction** | `[JsonPropertyName]` 显式 | `"navigate"` | `"navigate"` | ✅ |
| **OperationType** | camelCase enum 名 | `"click"` | `"click"` | ✅ 单词无下划线 |
| **TargetType** | camelCase enum 名 | `"text"` | `"text"` | ✅ |
| **RegionRole** | camelCase enum 名 | `"menu"` | `"menu"` | ✅ |

**不一致**：TypeHint 是唯一用全局 camelCase 策略而非 `[JsonPropertyName]` 显式属性的复合枚举。Direction/MenuItemType/ExpectedAction 都有显式 `[JsonPropertyName]`，而 TypeHint 没有。

**结果**：TypeHint 的 `"clickable_text"` 序列化为 `"clickableText"`，与 Python 的 `"clickable_text"` 不兼容。其他有 `[JsonPropertyName]` 的 enum 都与 Python 兼容。

---

### Record 类型

| Record | 序列化键名 | null 处理 | Python 对应键名 | 键名对齐? |
|--------|-----------|----------|----------------|----------|
| **BoundingBox** | `{x,y,width,height}` (camelCase) | 无 null 字段 | `{x,y,w,h}` | ❌ w→width, h→height |
| **Coordinate** | `{x,y}` | 无 null 字段 | `{x,y}` | ✅ |
| **Region** | `{id,bounds,role}` | 无 null 字段 | `{id,bounds,role}` | ✅ |
| **ScreenHints** | `{topBarText,layoutType,regions,...}` | null 字段跳过 | `{top_bar_text,layout_type,...}` | ❌ snake→camel |
| **FlattenedElement** | `{id,text,typeHint,boundingBox,...}` | null 字段跳过 | `{id,text,type_hint,bbox,...}` | ❌ type_hint→typeHint, bbox→boundingBox |
| **FlattenedScreen** | `{elements,screenHints}` | null 字段跳过 | `{elements,screen_hints}` | ❌ screen_hints→screenHints |
| **MenuInfo** | `{name,coordinate,active}` | 无 null 字段 | `{name,coordinate,active}` | ✅ |
| **MenuItem** | `{name,type,coordinate,...}` | null 字段跳过 | `{name,type,coordinate,...}` | ❌ expected_action→expectedAction 等 3 字段 |
| **PopupInfo** | `{title,content,closeButton}` | null 字段跳过 | `{title,content,close_button}` | ❌ close_button→closeButton |
| **PageAnalysis** | `{level1Dir,...}` | null 字段跳过 | `{level1_dir,...}` | ❌ 11 字段 snake→camel |
| **VisitFingerprint** | `{level1,level2,itemName}` | 无 null 字段 | `{level1,level2,item_name}` | ❌ item_name→itemName |
| **ContentNode** | `{id,title,...}` | null 字段跳过 | `{id,title,...}` | ❌ parent_id→parentId, node_type→nodeType |
| **Operation** | `{action,target,params,restore}` | null 字段跳过 | Python 无 Domain Operation | N/A |
| **Target** | `{by,value,meta}` | null 字段跳过 | Python 无 Domain Target | N/A |
| **RestoreAction** | `{action,target,params}` | null 字段跳过 | Python 无 Domain RestoreAction | N/A |

---

## 3. JSON 兼容差距汇总

| 类别 | 受影响类型数 | 典型差异 | Phase 2 对策 |
|------|-------------|----------|-------------|
| **复合键名 snake→camel** | 7 | `top_bar_text`→`topBarText` | 加 `[JsonPropertyName]` 双序列化或切换策略 |
| **字段重命名** | 3 | `w`→`width`, `bbox`→`boundingBox`, `type_hint`→`typeHint` | 加 `[JsonPropertyName]` 保持 Python 键名 |
| **TypeHint enum 值名** | 1 | `clickable_text`→`clickableText` | 加 `[JsonPropertyName("clickable_text")]` |
| **null 跳过 vs Python 非空默认** | 6 | Python 写 `"top_bar_text": ""`，C# 跳过 null | Phase 2 视需求调整 DefaultIgnoreCondition |

**PRD 定位**：§6 明确"本期 camelCase，Phase 2 加 `[JsonPropertyName]` 或切换策略"。**当前不修**。

---

## 4. 反序列化行为

| 类型 | 反序列化方式 | Python 对应 | 差异 |
|------|-------------|-------------|------|
| **所有 record** | `JsonSerializer.Deserialize<T>(json, DomainJsonOptions.Default)` | `cls.from_dict(data)` | C# 用 JSON 反序列化替代 Python 的 from_dict。构造器校验在反序列化后执行。 |
| **TypeHint/SelectionState** | `FromString(string)` 从 JSON 字段解析 | `cls.from_string(value)` | ✅ 对齐（P0 fix 后精确别名匹配） |
| **Direction/MenuItemType/ExpectedAction** | `FromValue(string)` 从 JSON 字段解析 | `cls.from_value(value)` | ✅ 对齐 |
| **VisitFingerprint** | `FromString(string)` | `cls.from_string(value)` | ✅ 对齐 |
| **BoundingBox** | 仅构造器（无 from_dict） | `cls.from_dict(data)` | C# 用 JSON 反序列化替代。默认值不同（Python w=0.001, C# 强制 >0） |

---

## 5. 序列化测试覆盖

| 类型 | 有序列化测试? | 测试内容 |
|------|-------------|----------|
| **PageAnalysis** | ✅ | PageAnalysis_Serialization_CamelCase: 验证 camelCase 键名 + Direction 序列化 |
| **Direction** | ✅（间接） | 在 PageAnalysis 测试中验证 `"level1Dir": "left"` |
| **其他类型** | ❌ | 无独立序列化测试 |

**建议**：Phase 2 为每个跨 JSON 边界的类型加序列化 roundtrip 测试（serialize → deserialize → assert equal）。

---

## 6. TypeHint `[JsonPropertyName]` 属性缺失

**当前**：TypeHint 8 值无 `[JsonPropertyName]`，依赖全局 camelCase 策略。
**后果**：
- `"clickable_text"` 序列化为 `"clickableText"`（与 Python `"clickable_text"` 不兼容）
- `"input_field"` 序列化为 `"inputField"`（与 Python `"input_field"` 不兼容）
- Values 反射模式无法使用（因为没有 `[JsonPropertyName]` 属性可读）

**建议**：加 `[JsonPropertyName]` 属性，使 TypeHint 序列化行为与 Python 对齐，同时使 Values 反射模式可行：

```csharp
public enum TypeHint
{
    [JsonPropertyName("clickable_text")] ClickableText,
    [JsonPropertyName("switch")] Switch,
    [JsonPropertyName("slider")] Slider,
    [JsonPropertyName("button")] Button,
    [JsonPropertyName("icon")] Icon,
    [JsonPropertyName("input_field")] InputField,
    [JsonPropertyName("text")] Text,
    [JsonPropertyName("image")] Image
}
```

**优先级**: P3（当前 DomainJsonOptions camelCase 策略已能工作，但与 Python 不兼容且与其他 enum 不一致）
