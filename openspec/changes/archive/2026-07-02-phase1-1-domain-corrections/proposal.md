## Why

Phase 1 交付了 Domain 层的首次完整实现（185 测试全绿，87.8% 覆盖率），但经 Python↔C# 全量对比审查（[04-phase1-python-csharp-comparison.md](../../../docs/refactor/04-phase1-python-csharp-comparison.md)）和模型关系梳理（[05-model-relationship-map.md](../../../docs/refactor/05-model-relationship-map.md)），发现 4 个必修正确性问题：

1. **ElementTypeMapper 两套系统合二为一** — `MapAndroidClass` 直接返回 `TypeHint` enum，把视觉分类和行为语义桥接压缩成一步映射。ToggleButton 被错误归类为 `TypeHint.Switch`，丧失了 `MenuItemType.Toggle` 与 `MenuItemType.Switch` 的独立下游映射。Python 正确架构是两级映射（Android 控件 → 中间字符串 → MenuItemType/ExpectedAction），中间字符串层有 14 个值，TypeHint enum 只有 8 个值，两者不重叠。
2. **FromString 用 Contains 子串匹配** — TypeHint 和 SelectionState 的 `FromString` 使用 `Contains` 子串匹配，违反 PRD §5.1 的「精确→别名→回落」算法。`"scrollable"` 被误归为 Slider（含 `"scroll"`），而非回落 Text；缺失 Python 别名 `"click"`→ClickableText、`"check"`→Switch、`"highlighted"`→Selected。
3. **DirectionExtensions.Values 硬编码** — 与同文件 MenuItemType/ExpectedAction 的反射模式不一致，新增值会遗漏。
4. **IsValid(string) 缺失** — 上层无法区分合法 TypeHint 和回落 Text，无法感知 AI 输出了意外值。

这些错误直接影响遍历引擎的节点模板选择和操作策略，必须在上层消费 Domain API 前修正。

## What Changes

- **ElementTypeMapper 两级映射分离** — `MapAndroidClass` 返回类型从 `TypeHint` 改为 `string`（中间字符串）；私有字典从 `Dictionary<string, TypeHint>` 改为 `Dictionary<string, string>`，逐行搬 Python `ANDROID_CLASS_MAP` 14 行（ToggleButton→`"toggle"` 不再压缩到 Switch）；新增 `ToTypeHint(string)` 方法提供中间字符串→视觉分类反向映射；fallback 值从 `TypeHint.Button` 改为 `"button"`。**BREAKING**：`MapAndroidClass` 返回类型变更。
- **TypeHint.FromString 精确别名+回落** — 替换 Contains switch 为 `Dictionary<string, TypeHint>` 别名字典（8 精确枚举值 + 7 Python 别名 + 3 C# 扩展别名），未知值回落 Text。补回缺失别名 `"click"`→ClickableText、`"check"`→Switch。消除 `"scrollable"` 误命中 Slider。
- **SelectionState.FromString 精确别名+回落** — 替换 Contains switch 为 `HashSet<string>` 别名集合（SelectedAliases 5 值 + DisabledAliases 6 值），补回缺失别名 `"highlight"`、`"highlighted"`。未知值回落 Normal。
- **DirectionExtensions.Values 统一反射** — 从硬编码数组改为反射读取 `[JsonPropertyName]` 属性，与 MenuItemType/ExpectedAction 一致。
- **IsValid(string) 补 string 版** — TypeHint 和 SelectionState 各增 `IsValid(string)` 方法，上层可区分合法解析与回落。
- **MapAndroidClass null 防御** — 入口加 `DomainValidationException` 检查，取代 NullReferenceException。

## Capabilities

### New Capabilities

None — 本期只修正现有实现的正确性，不新增功能或类型。

### Modified Capabilities

- `domain-type-mappings`: `MapAndroidClass` 返回类型从 `TypeHint` 改为 `string`（中间字符串）；字典重建为 14 行 Python 精确映射；新增 `ToTypeHint(string)` 视觉分类反向映射；null 防御。spec 行为变更：`map_android_class` 的返回类型和映射值变更（**BREAKING**），mapper 表全行重建对齐 Python。
- `domain-vision-models`: `TypeHint.FromString` 从 Contains 子串匹配改为精确别名字典+回落；`SelectionState.FromString` 从 Contains 改为 HashSet 别名+回落；两者各增 `IsValid(string)` 方法。spec 行为变更：`FromString` 解析算法变更（确定性取代子串匹配），`IsValid` 新增 string 版。

## Impact

- **Affected Code**:
  - `src/UniClaw.Core/Domain/Mappings/ElementTypeMapper.cs` — 返回类型、字典类型、字典内容、新增方法、null 防御。
  - `src/UniClaw.Core/Domain/Models/Vision/TypeHint.cs` — `FromString` 重写、新增 `IsValid(string)`、别名字典。
  - `src/UniClaw.Core/Domain/Models/Vision/SelectionState.cs` — `FromString` 重写、新增 `IsValid(string)`、别名集合。
  - `src/UniClaw.Core/Domain/Models/Content/EnumsAndCoordinate.cs` — `DirectionExtensions.Values` 从硬编码改为反射。
  - `tests/UniClaw.Core.Tests/Domain/Mappings/` — 测试更新（MapAndroidClass 返回 string、全行扫描对齐 Python）。
  - `tests/UniClaw.Core.Tests/Domain/Vision/TypeHintTests.cs` — 测试更新（别名命中、回落、IsValid）。
  - `tests/UniClaw.Core.Tests/Domain/Vision/SelectionStateTests.cs` — 测试更新（别名命中、回落、IsValid）。
  - `tests/UniClaw.Core.Tests/Domain/Content/` — 测试更新（Direction Values 反射验证）。

- **API Changes**:
  - **BREAKING**: `ElementTypeMapper.MapAndroidClass` 返回 `string`（原 `TypeHint`）。调用方需通过 `ElementTypeMapper.ToTypeHint(result)` 获取视觉分类。
  - `TypeHint.FromString` 语义变更：确定性精确匹配取代子串匹配，部分输入结果变更（`"scrollable"` → Text 而非 Slider）。
  - `SelectionState.FromString` 语义变更：`"highlighted"` → Selected（原回落 Normal）。
  - 新增：`TypeHint.IsValid(string)`、`SelectionState.IsValid(string)`、`ElementTypeMapper.ToTypeHint(string)`。

- **Dependencies**: 无新增依赖。`DomainValidationException` 已在 Phase 1 实现。

- **Systems**: Domain 层内部修正，无 I/O、无上层耦合影响。下游（Phase 2 Graph 层）尚未消费此 API，BREAKING 变更无传播风险。
