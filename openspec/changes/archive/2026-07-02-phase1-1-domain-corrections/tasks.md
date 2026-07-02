## 1. ElementTypeMapper：两级映射分离 + null 防御

- [x] 1.1 将 `AndroidClassToTypeHintMap` 字典类型从 `Dictionary<string, TypeHint>` 改为 `Dictionary<string, string>`，字典名改为 `AndroidClassMap`，逐行替换值：ToggleButton→`"toggle"`、TextView→`"menu_item"`、EditText→`"input"`、各 Layout→`"menu_item"`，其余保持原映射字符串
- [x] 1.2 将 `MapAndroidClass` 返回类型从 `TypeHint` 改为 `string`，3 级匹配逻辑保持（精确→子串→fallback），fallback 值从 `TypeHint.Button` 改为 `"button"`
- [x] 1.3 将 `AndroidClassMap` 公开属性类型从 `IReadOnlyDictionary<string, TypeHint>` 改为 `IReadOnlyDictionary<string, string>`
- [x] 1.4 新增 `public static TypeHint ToTypeHint(string typeString)` 方法：已知映射 `"switch"→Switch, "toggle"→Switch, "menu_item"→ClickableText, "input"→InputField, "slider"→Slider, "button"→Button`，未知值回落 `TypeHint.Text`
- [x] 1.5 在 `MapAndroidClass` 入口加 null 防御：`if (className is null) throw new DomainValidationException(nameof(className), className);`
- [x] 1.6 `dotnet build` 验证编译通过；确认 `ToMenuItemType`/`ToExpectedAction` 无需改动（它们已用 string key）；确认现有 `ToMenuItemType("toggle")`→`MenuItemType.Toggle` 和 `ToExpectedAction("toggle")`→`ExpectedAction.Toggle` 测试仍通过
- [x] 1.7 更新 ElementTypeMapper 测试：全行 14 行扫描断言改为 string 返回值；ToggleButton 链测试 `"toggle"` → `MenuItemType.Toggle` → `ExpectedAction.Toggle`；null 输入测试 DomainValidationException

## 2. TypeHint.FromString：精确别名+回落 + IsValid(string)

- [x] 2.1 将 `FromString` 的 Contains switch expression 替换为 `Dictionary<string, TypeHint> AliasMap`：8 精确枚举值 + 7 Python 别名（`"click"`→ClickableText, `"check"`→Switch, `"toggle"`→Switch, `"checkbox"`→Switch, `"btn"`→Button, `"input"`→InputField, `"field"`→InputField, `"img"`→Image, `"picture"`→Image, `"clickable"`→ClickableText） + 3 C# 扩展别名（`"seekbar"`→Slider, `"edit"`→InputField, `"textbox"`→InputField）
- [x] 2.2 重写 `FromString` 方法体：`value.ToLowerInvariant().Trim()` → `AliasMap.TryGetValue(key, out result)` → 未命中回落 `TypeHint.Text`
- [x] 2.3 新增 `public static bool IsValid(string value)` 方法：`AliasMap.ContainsKey(value.ToLowerInvariant().Trim())`
- [x] 2.4 更新 TypeHintTests：新增 `"scrollable"` → Text（非 Slider）；`"click"` → ClickableText；`"check"` → Switch；`"dropdown"` → Text；`"seekbar"` → Slider（C# 扩展）；`"BUTTON"` → Button（大小写容错）；`" button "` → Button（空格容错）；`IsValid("button")` → true；`IsValid("btn")` → true（别名）；`IsValid("scrollable")` → false；`IsValid("")` → false

## 3. SelectionState.FromString：精确别名+回落 + IsValid(string)

- [x] 3.1 将 `FromString` 的 Contains switch expression 替换为两个 `HashSet<string>`：SelectedAliases（`"selected"`, `"active"`, `"checked"`, `"highlight"`, `"highlighted"`）+ DisabledAliases（`"disabled"`, `"inactive"`, `"hidden"`, `"gray"`, `"grayed"`, `"dimmed"`）
- [x] 3.2 重写 `FromString` 方法体：先查 DisabledAliases（避免 `"inactive"` 误落入 Selected），再查 SelectedAliases，未命中回落 `SelectionState.Normal`
- [x] 3.3 新增 `public static bool IsValid(string value)` 方法：`SelectedAliases.Contains(key) || DisabledAliases.Contains(key) || key == "normal"`
- [x] 3.4 更新 SelectionStateTests：新增 `"highlighted"` → Selected（之前缺失）；`"highlight"` → Selected；`"activated"` → Normal（非 Selected）；`"inactive"` → Disabled（非 Selected）；`"grayed"` → Disabled；`"SELECTED"` → Selected（大小写容错）；`IsValid("selected")` → true；`IsValid("checked")` → true（别名）；`IsValid("normal")` → true；`IsValid("activated")` → false

## 4. DirectionExtensions.Values：硬编码→反射

- [x] 4.1 将 `DirectionExtensions.Values` 从 `new[] { "left", "right", "top", "bottom" }` 改为反射读取 `[JsonPropertyName]` 属性：复用 MenuItemTypeExtensions 的 `GetStringValue` 模式，添加 `private static string GetStringValue(Direction d)` 方法
- [x] 4.2 验证 `FromValue` 和 `IsValid` 逻辑无需改动（它们已用 string 比较）
- [x] 4.3 更新 Direction 测试：验证 `Values` 包含 4 值且与 `[JsonPropertyName]` 一致；可新增反射构建验证（非硬编码）

## 5. 全量验证

- [x] 5.1 `dotnet build` — 0 错误 0 警告
- [x] 5.2 `dotnet test` — 全绿（≥185 测试基础上增量通过）
- [x] 5.3 grep 确认 TypeHint enum 无新增值（8 值不变）
- [x] 5.4 ToggleButton 链端到端验证：`MapAndroidClass("ToggleButton")` → `"toggle"` → `ToMenuItemType("toggle")` → `MenuItemType.Toggle` → `ToExpectedAction("toggle")` → `ExpectedAction.Toggle`
- [x] 5.5 FromString 未知值回落验证：`TypeHint.FromString("scrollable")` → Text（非 Slider）；`SelectionState.FromString("activated")` → Normal（非 Selected）
