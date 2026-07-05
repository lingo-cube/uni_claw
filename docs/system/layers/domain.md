# Layers — Domain

> **Tier 3 · Layers**: Domain 层规格书。改 Domain 类型/接口/校验/序列化时更新此文档。
> 状态: Phase 1 完成 (24+2 类型, 229 测试全绿)
> 源码: `src/UniClaw.Core/Domain/`

---

## 1. Type Inventory (26 types)

### Vision (8 types) — "看起来像什么"

| Type | Kind | Key behavior |
|------|------|-------------|
| `BoundingBox` | sealed record | 构造期 fail-fast (X,Y,Width,Height ≥ 0) |
| `Region` | sealed record | RegionRole enum (5值), id ?? string.Empty (P3: 需非空校验) |
| `RegionRole` | enum | 5值 (Header/Nav/Main/Footer/Dialog), 有 `[JsonPropertyName]` |
| `TypeHint` | enum | **8值锁定** (→ constitution C-2), FromString + AliasMap (18 entries) |
| `TypeHintExtensions` | static class | AliasMap, IsInteractive, IsVisualOnly, FromString, IsValid |
| `SelectionState` | enum | **3值锁定** (→ constitution C-8), FromString + alias fallback |
| `SelectionStateExtensions` | static class | SelectedAliases, DisabledAliases, FromString, IsValid |
| `FlattenedElement` | sealed record | TypeHint + BoundingBox + SelectionState, IsInteractive computed |
| `FlattenedScreen` | sealed record | 元素列表 + ScreenHints, Phase 2 AI 输入 |
| `ScreenHints` | sealed record | topBarText/bottomBarText/isScrollable/hasKeyboard (P3: snake→camel) |

### Content (10 types) — "能做什么 / 有什么内容"

| Type | Kind | Key behavior |
|------|------|-------------|
| `Coordinate` | sealed record | 构造期 fail-fast (X,Y ≥ 0) |
| `Direction` | enum | 4值 (Up/Down/Left/Right), `[JsonPropertyName]`, FromValue fail-fast |
| `MenuItemType` | enum | 11值 (Hilly级), `[JsonPropertyName]`, FromValue fail-fast |
| `ExpectedAction` | enum | 4值 (Click/Type/Scroll/Select), `[JsonPropertyName]` |
| `MenuInfo` | sealed record | Name coerces null → empty (P3: Python rejects null) |
| `MenuItem` | sealed record | MenuItemType + ExpectedAction + Coordinate + Target |
| `PopupInfo` | sealed record | popup text + dismiss targets |
| `PageAnalysis` | sealed record | **12 fields** — 遍历引擎的核心输出 |
| `VisitFingerprint` | sealed record | screen hash + visited items |
| `ContentNode` | sealed record | hierarchical content tree (P3: ToMarkdown 待实现) |

### Common (5 types) — "操作是什么"

| Type | Kind | Key behavior |
|------|------|-------------|
| `OperationType` | enum | 5值 (Click/Type/Scroll/Wait/Navigate) |
| `Operation` | sealed record | 构造期 fail-fast: Action 非空 |
| `TargetType` | enum | 4值 (Id/Xpath/Text/Coordinate) |
| `Target` | sealed record | 构造期 fail-fast: By 非空 |
| `RestoreAction` | sealed record | 构造期 fail-fast: Action 非空 |

### Mappings (2 types) — 唯一跨域桥

| Type | Kind | Key behavior |
|------|------|-------------|
| `ElementTypeMapper` | static class | MapAndroidClass → 中间字符串 (14值), NOT TypeHint (P0 fix). ToTypeHint optional convenience |
| `AndroidWidgetClass` | enum | 孤立enum, 0 references |

### Cross-cutting (2 types)

| Type | Kind | Key behavior |
|------|------|-------------|
| `DomainValidationException` | class | FieldName + IllegalValue, 12 reference points across all sub-domains |
| `DomainJsonOptions` | static class | Default: CamelCase + JsonStringEnumConverter + null-skip |

---

## 2. Dependency Topology

**三岛零互 import 规则 (→ constitution C-3)**:

```
Domain.Vision ←────×────→ Domain.Content ←────×────→ Domain.Common
                        │
                   唯一桥: Domain.Mappings (ElementTypeMapper)

Cross-cutting: DomainValidationException, DomainJsonOptions (all sub-domains reference these)
```

**Hub types**:
- `BoundingBox` (Vision): 3 direct dependencies (Region, FlattenedElement, ScreenHints)
- `Coordinate` (Content): 5 direct dependencies (MenuItem, Region, FlattenedScreen, Operation, ContentNode)

**Phase 2+ cross-domain references**:
- DynamicMatcher uses `MenuItemType`, `ExpectedAction` from Content (→ layers/graph.md)
- TraversalNode uses `Operation` from Common (→ layers/graph.md)

---

## 3. Stability Classification (→ constitution locked-enums.md)

| Level | Types | Extension rule |
|-------|-------|---------------|
| **火山** | TypeHint (8), SelectionState (3) | **禁止扩展** — cascade 4/2 域 |
| **丘陵** | MenuItemType (11), ExpectedAction (4), OperationType (5), Direction (4), RegionRole (5) | 先更新 mapping table 再加值 |
| **平原** | BoundingBox, Coordinate, FlattenedScreen, MenuInfo, PopupInfo, PageAnalysis 等 | 自由演进 (1-2 direct deps) |
| **独立** | Target, RestoreAction, AndroidWidgetClass | 自由演进 (0 cascade) |
| **跨切面** | DomainValidationException, DomainJsonOptions | 单向影响，扩展需全局审查 |

---

## 4. Validation Strategy

**Fail-fast (构造期)** — DomainValidationException, 无 fallback:
- BoundingBox (X,Y,Width,Height ≥ 0), Coordinate (X,Y ≥ 0)
- Operation (Action 非空), Target (By 非空), RestoreAction (Action 非空)
- Direction.FromValue, MenuItemType.FromValue, ExpectedAction.FromValue (exact match only)

**Graceful (解析期)** — fallback + IsValid:
- TypeHint.FromString: AliasMap fallback → Text (IsValid 区分精确 vs 别名, P3: 补 IsCanonical)
- SelectionState.FromString: SelectedAliases/DisabledAliases fallback → Normal
- MapAndroidClass: null → DVE, unrecognized → default string

---

## 5. Serialization Conventions

**DomainJsonOptions.Default**:
- `JsonNamingPolicy.CamelCase` — 键名 camelCase (PRD §6: 本期不背 Python snake_case)
- `JsonStringEnumConverter` — enum 值序列化为字符串
- `JsonIgnoreCondition.WhenWritingNull` — null 字段跳过

**已知问题 (P3)**:
- TypeHint 缺 `[JsonPropertyName]` — compound 值如 `ClickableText` 序列化为 `clickableText` 而非 Python 的 `clickable_text`
- 其他 3 个 Domain enum (MenuItemType, ExpectedAction, Direction) 都有 `[JsonPropertyName]` 标注

**键名不匹配表** (camelCase vs Python snake_case):
- BoundingBox: `width/height` vs Python `w/h`
- FlattenedElement: `typeHint/boundingBox` vs Python `type_hint/bbox`
- ScreenHints: `topBarText` vs Python `top_bar_text`
- PageAnalysis: 11 fields snake→camel mismatch

---

## 6. Semantic Contracts Summary (→ constitution C-2)

**Three semantic layers**:

| Layer | Types | Answers | MUST NOT |
|-------|-------|---------|----------|
| **Visual Appearance** | TypeHint, BoundingBox, FlattenedElement, SelectionState, ScreenHints, RegionRole | "看起来像什么" | 不含行为性值 (toggle/menu_item/input) |
| **Behavioral Semantics** | MenuItemType, ExpectedAction, OperationType, Operation, Target | "能做什么" | 不含视觉描述 |
| **Spatial Position** | Coordinate, Direction, Region | "在什么位置" | 不含行为推断 |

**唯一桥**: ElementTypeMapper maps visual string → behavioral enum (中间字符串, NOT TypeHint)

---

## 7. P3 Outstanding Items

| # | Item | Priority |
|---|------|----------|
| 1 | ContentNode.ToMarkdown() | P3 |
| 2 | Region.Id 非空校验 (Python: `if not self.id: raise ValueError`) | P3 |
| 3 | TypeHint 加 `[JsonPropertyName]` | P3 |
| 4 | TypeHint.Values 改为 `IReadOnlyList<string>` | P3 |
| 5 | 补 `IsCanonical(string)` 区分精确值 vs 别名 | P3 |

---

## 8. Python Alignment (Archived)

Python↔C# 全量对比已归档至 `docs/refactor/04-phase1-python-csharp-comparison.md`，不再在此文档维护。
模型关系图 (P0 fix 前版本): `docs/refactor/05-model-relationship-map.md` (部分过时)
