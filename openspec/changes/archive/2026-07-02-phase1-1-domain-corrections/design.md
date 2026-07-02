## Context

Phase 1 交付了完整的 Domain 层实现（185 测试全绿，87.8% 覆盖率），但经 Python↔C# 全量对比审查发现 4 个正确性问题需要修正。当前 C# 代码状态：

- **ElementTypeMapper** (`Domain/Mappings/ElementTypeMapper.cs`): `MapAndroidClass` 返回 `TypeHint` enum，把 Python 的两级映射（Android 控件 → 中间字符串 → MenuItemType/ExpectedAction）压缩成一步。`AndroidClassToTypeHintMap` 是 `Dictionary<string, TypeHint>`，14 行映射中 `ToggleButton`→`TypeHint.Switch`（错误，应为独立 `"toggle"`）。
- **TypeHint.FromString** (`Domain/Models/Vision/TypeHint.cs`): 使用 `Contains` 子串匹配 switch expression，缺失 Python 别名 `"click"` 和 `"check"`，`"scrollable"` 误命中 Slider。
- **SelectionState.FromString** (`Domain/Models/Vision/SelectionState.cs`): 同样使用 `Contains`，缺失 `"highlighted"` 和 `"highlight"`。
- **DirectionExtensions.Values** (`Domain/Models/Content/EnumsAndCoordinate.cs:49`): 硬编码 `new[] { "left", "right", "top", "bottom" }`，与同文件 MenuItemType/ExpectedAction 的反射模式不一致。
- **IsValid(string)**: TypeHint 和 SelectionState 均无 string 版，上层无法区分合法解析与回落。

**约束**：不新增类型、不新增字段、不新增功能；只修正现有实现的正确性。所有 record 保持 sealed record class + ImmutableArray。Python 源码为映射链和别名集的基准。

**Python 基准来源**（`main` 分支）：
- `src/models/element_type_mapper.py` — `ANDROID_CLASS_MAP` (14 行)、`TYPE_TO_MENU_ITEM` (13 行)、`TYPE_TO_EXPECTED_ACTION` (11 行)
- `src/models/vision/type_hint.py` — `from_string` 别名映射 (7 条 + 8 精确枚举值)
- `src/models/vision/selection_state.py` — `from_string` 别名集合 (selected_aliases 4 值 + disabled_aliases 5 值)
- `src/models/content_models.py` — Direction/MenuItemType/ExpectedAction 枚举定义

## Goals / Non-Goals

**Goals**:
- 修正 ElementTypeMapper 为两级映射：`MapAndroidClass` 返回中间字符串，ToggleButton 映射链完整（`"toggle"` → MenuItemType.Toggle → ExpectedAction.Toggle）。
- 修正 TypeHint.FromString 和 SelectionState.FromString 为确定性精确别名匹配+回落，消除子串误命中。
- 修正 DirectionExtensions.Values 从硬编码改为反射，与 MenuItemType/ExpectedAction 一致。
- 补 TypeHint.IsValid(string) 和 SelectionState.IsValid(string)，上层可区分合法解析与回落。
- 加 MapAndroidClass null 防御（DomainValidationException）。
- 在 185 测试基础上增量通过，不删不改旧测试的正确断言。

**Non-Goals**:
- 新增类型、字段或功能（本期只修不扩）。
- JSON 键名 snake_case↔camelCase 兼容（PRD §6 明确本期 camelCase）。
- null vs 空串/sentinel 默认值语义变更（04 §2.2）。
- Region.Id 非空校验、ContentNode.ToMarkdown()、AI 层双版本简化（均独立工作，本期不阻塞）。
- MenuItemTypeExtensions/ExpectedActionExtensions 的 FromValue 反射效率优化（不影响正确性，可独立处理）。

## Decisions

### 1. MapAndroidClass 返回中间字符串而非 TypeHint

**决策**: `MapAndroidClass` 返回类型从 `TypeHint` 改为 `string`。字典从 `Dictionary<string, TypeHint>` 改为 `Dictionary<string, string>`，逐行搬 Python `ANDROID_CLASS_MAP` 14 行。

**理由**: Python 的正确架构是两级映射。中间字符串层有 14 个值（含 `"toggle"`, `"menu_item"`, `"input"`），TypeHint enum 只有 8 个值。两者不重叠——TypeHint 是纯视觉外观分类，中间字符串是行为语义桥接。压缩成一步导致 ToggleButton 丧失独立下游映射。

**影响**: `MapAndroidClass` 是 **BREAKING** API 变更。调用方需通过新增的 `ToTypeHint(string)` 获取视觉分类。但下游（Phase 2 Graph 层）尚未消费此 API，无传播风险。

**替代方案**:
- 在原 `Dictionary<string, TypeHint>` 中给 ToggleButton 新增 `TypeHint.Toggle` enum 值 — **拒绝**：违反「不新增类型」原则，且 `TypeHint.Toggle` 在视觉分类上与 `TypeHint.Switch` 完全相同（ToggleButton 的视觉外观就是开关），不应引入只有行为语义差异的冗余 enum 值。
- 保留 `MapAndroidClass` 返回 `TypeHint` 但内部先走中间字符串 — **拒绝**：API 签名返回 TypeHint 意味着调用方无法获取中间字符串，丧失了 `ToMenuItemType`/`ToExpectedAction` 的独立入口点（它们接收中间字符串作为 key）。

### 2. ToTypeHint(string) 作为中间字符串→视觉分类的反向映射

**决策**: 新增 `public static TypeHint ToTypeHint(string typeString)` 方法，提供中间字符串→TypeHint 的视觉分类映射。

映射逻辑：
```
"switch"    → TypeHint.Switch       // 视觉上 Switch 和 ToggleButton 都看起来像开关
"toggle"    → TypeHint.Switch       // ToggleButton 视觉外观 = Switch
"menu_item" → TypeHint.ClickableText // 菜单项看起来像可点击文字
"input"     → TypeHint.InputField   // 输入框
"slider"    → TypeHint.Slider       // 滑块
"button"    → TypeHint.Button       // 按钮
// 其他 → TypeHint.Text（回落）
```

**理由**: 调用方可能需要视觉分类（如 AI 层构建 FlattenedElement 的 type_hint 字段），此方法保持两套系统的独立入口点，同时提供视觉分类的便捷方法。

**替代方案**:
- 不提供 ToTypeHint，要求调用方自己做中间字符串→TypeHint 映射 — **拒绝**：映射逻辑有非直觉规则（如 `"toggle"`→Switch 而非独立值），硬编码在调用方会导致不一致。

### 3. FromString 精确别名字典取代 Contains 子串匹配

**决策**: TypeHint.FromString 使用 `Dictionary<string, TypeHint>` 别名字典（8 精确枚举值 + 7 Python 别名 + 3 C# 扩展别名），`TryGetValue` 精确匹配，未命中回落 `Text`。SelectionState.FromString 使用两个 `HashSet<string>`（SelectedAliases 5 值 + DisabledAliases 6 值），`Contains` 精确匹配，先查 Disabled（避免 `"inactive"` 落入 Selected 的 `"active"`），未命中回落 `Normal`。

**理由**: Contains 子串匹配是概率性而非确定性——`"scrollable"` 含 `"scroll"` 误命中 Slider，`"inactive"` 含 `"active"` 误命中 Selected（需顺序 workaround）。精确字典/集合查找是确定性，不含运气成分。Python 源码验证：`from_string` 用 `dict.get(key, fallback)` 精确查找，不是子串匹配。

**补充别名**（Python 有但 C# 当前缺）:
- TypeHint: `"click"`→ClickableText, `"check"`→Switch
- SelectionState: `"highlighted"`→Selected, `"highlight"`→Selected（Python 有，C# 当前缺）

**C# 扩展别名**（不在 Python mapping dict 中，来自实际 AI/Android 输入场景）:
- TypeHint: `"seekbar"`→Slider (Android SeekBar), `"edit"`→InputField (EditText), `"textbox"`→InputField (AI 变体)
- SelectionState: `"disabled"`→Disabled (语义明确，Python disabled_aliases 不含但 C# Contains 逻辑原本有)

**替代方案**:
- 用 `switch` expression 精确匹配代替字典 — **拒绝**：18 条别名用 switch 太长且无结构性，字典/集合天然适合「键→值」和「属于」语义，代码更短、可扩展性更好。
- 不加 C# 扩展别名 — **拒绝**：`"seekbar"` 和 `"edit"` 是 Android 遍历场景的实际输入，不加意味着合法输入回落 Text/Normal，丧失业务语义。

### 4. IsValid(string) 别名字典覆盖

**决策**: `TypeHint.IsValid(string)` 检查 `AliasMap.ContainsKey(key)`（含别名），`SelectionState.IsValid(string)` 检查 `SelectedAliases.Contains(key) || DisabledAliases.Contains(key) || key == "normal"`。

**理由**: Python `TypeHint.is_valid(str)` 只检查枚举值字符串（不含别名），但 C# 版回答"这个 string 能否被 FromString 成功解析"更实用——上层关心的是"能不能解析"，不是"是不是标准值"。`IsValid("btn")` → true 比更严格版本更符合调用方需求。

**替代方案**:
- 只检查精确枚举值不含别名 — **拒绝**：调用方使用 `IsValid` 前置验证，`FromString("btn")` 返回 Button 但 `IsValid("btn")` 返回 false 是逻辑矛盾。

### 5. DirectionExtensions.Values 统一反射

**决策**: 从硬编码 `new[] { "left", "right", "top", "bottom" }` 改为反射读取 `[JsonPropertyName]` 属性，复用 MenuItemTypeExtensions 的 `GetStringValue` 模式。

**理由**: Direction 已有 `[JsonPropertyName]` 属性（4 个），反射动态构建与 MenuItemType/ExpectedAction 一致。新增值不会遗漏。

**替代方案**:
- 用 `Enum.GetValues<Direction>().Select(d => d.ToString().ToLowerInvariant())` — **拒绝**：Direction 的 `[JsonPropertyName]` 值（`"left"` 等）就是枚举名的 lowercase，但其他 enum（MenuItemType）的 `[JsonPropertyName]` 值与枚举名不同（如 `MenuItemType.MenuItem` → `"menu_item"`）。统一用 `[JsonPropertyName]` 反射保持一致性，即使 Direction 当前恰好 lowercase==enum name。

### 6. MapAndroidClass null 防御用 DomainValidationException

**决策**: 入口加 `if (className is null) throw new DomainValidationException(nameof(className), className);`

**理由**: Python `from_android_class` 有 `TypeError` 检查。当前 C# 无防御，null 输入触发 `NullReferenceException`——不结构化，调试困难。DomainValidationException 已在 Phase 1 实现，携带 `FieldName` + `IllegalValue`。

## Risks / Trade-offs

- **R-1 (BREAKING API)**: `MapAndroidClass` 返回类型变更。**缓解**: 下游（Phase 2）尚未消费此 API；TypeHintTests 当前测试 `FromString` 不涉及 MapAndroidClass 返回值；全行扫描测试需要更新断言（从 TypeHint 改为 string）。
- **R-2 (测试更新量)**: 需要更新 Mappings、TypeHint、SelectionState、Direction 的测试。**缓解**: 增量而非删改；不改旧测试的正确断言，只修正因 API 变更导致的断言类型变化。
- **R-3 (FromValue 反射效率)**: MenuItemTypeExtensions/ExpectedActionExtensions 的 `FromValue` 内部循环做反射而非利用已缓存的 `_values` 字典。**本期不修**——不影响正确性，属于性能优化范畴，可独立处理。
- **R-4 (C# 扩展别名判定标准)**: `"seekbar"`、`"edit"`、`"textbox"` 不在 Python mapping dict 中。**缓解**: 判定标准是「Python 不覆盖但 C# 遍历场景实际遇到的输入变体」；在别名字典中注释标注来源；如果后续发现有误可独立增删。
- **R-5 (SelectionState Disabled aliases 包含 "disabled")**: Python `disabled_aliases` 是 `{'gray', 'grayed', 'dimmed', 'inactive', 'hidden'}`，不含 `"disabled"`。C# 当前 Contains 逻辑有 `"disabled"`，改 HashSet 后保留（语义明确：disabled→Disabled 是直觉映射）。**缓解**: 在代码注释中标注 C# 扩展。

## Acceptance Criteria

所有验收标准均有对应测试断言，可通过 `dotnet test` 自动验证：

| # | 标准 | 验证方式 | 对应 spec |
|---|------|----------|-----------|
| AC-1 | `dotnet build` 0 错误 0 警告 | CI 构建 | — |
| AC-2 | `dotnet test` 全绿（≥185，增量通过） | CI 测试 | — |
| AC-3 | ToggleButton 映射链完整 | 测试断言：`MapAndroidClass("ToggleButton")` → `"toggle"` → `ToMenuItemType("toggle")` → `MenuItemType.Toggle` → `ToExpectedAction("toggle")` → `ExpectedAction.Toggle` | domain-type-mappings: "ToggleButton mapping chain is complete" |
| AC-4 | FromString 未知值回落不误命中 | 测试断言：`TypeHint.FromString("scrollable")` → Text（非 Slider）；`SelectionState.FromString("activated")` → Normal（非 Selected） | domain-vision-models: "FromString unknown value falls back to Text/Normal" |
| AC-5 | FromString 缺失别名补回 | 测试断言：`TypeHint.FromString("click")` → ClickableText；`TypeHint.FromString("check")` → Switch | domain-vision-models: "FromString Python alias match / check maps to Switch" |
| AC-6 | SelectionState 缺失别名补回 | 测试断言：`SelectionState.FromString("highlighted")` → Selected；`SelectionState.FromString("highlight")` → Selected | domain-vision-models: "FromString Python alias highlight/highlighted maps to Selected" |
| AC-7 | IsValid(string) 区分合法/回落 | 测试断言：`TypeHint.IsValid("button")` → true；`TypeHint.IsValid("scrollable")` → false；`SelectionState.IsValid("selected")` → true；`SelectionState.IsValid("activated")` → false | domain-vision-models: "IsValid returns true/false" |
| AC-8 | DirectionExtensions.Values 反射 | 测试断言：`DirectionExtensions.Values` 包含 4 值且与 `[JsonPropertyName]` 一致；实现代码非硬编码 | domain-type-mappings: "DirectionExtensions.Values uses reflection" |
| AC-9 | MapAndroidClass null 防御 | 测试断言：`MapAndroidClass(null)` throws `DomainValidationException` with `FieldName="className"` | domain-type-mappings: "MapAndroidClass rejects null input" |
| AC-10 | TypeHint 8 值不变 | grep 确认 enum 无新增值 | — |
