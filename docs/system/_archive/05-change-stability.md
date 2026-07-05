# Domain 变更稳定性分析

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix 后）

---

## 1. 稳定性分级标准

| 级别 | 含义 | 变更策略 | 标记 |
|------|------|----------|------|
| 🔴 **火山** | 加/删/改一个值，跨域波及 ≥4 | 锁定：值数不变，值名不变 | 🔴 |
| 🟡 **丘陵** | 加/删/改一个值，域内波及 2-3 | 谨慎扩展：可加值但需同步更新映射表 | 🟡 |
| 🟢 **平原** | 加/删/改一个字段，仅本类型+1-2 直依赖 | 自由演进：可加字段、改默认值 | 🟢 |
| ⚪ **独立** | 变更波及仅自身 | 自由演进：可改任意内容 | ⚪ |
| 🔵 **跨切面** | 变更波及全域但方向单一（子域→root） | 接口稳定：字段/方法可加但不可删改 | 🔵 |

---

## 2. 各类型稳定性评级

### 🔴 火山级 — 锁定，不可变

| 类型 | 值数 | 波及面 | 锁定原因 |
|------|------|--------|----------|
| **TypeHint** | 8 | 4 跨域 | 加值 → AliasMap 需同步、ToTypeHint 需同步、FlattenedElement.IsInteractive 需同步、TypeHintTests 需更新。删值 → 同上反向。PRD §5.1 明确"无 Unknown"且 8 值完整覆盖视觉外观。 |
| **SelectionState** | 3 | 2 域 | 加值 → SelectedAliases/DisabledAliases 需同步、IsValid 需同步。3 值完整覆盖状态空间（Selected/Normal/Disabled）。 |

### 🟡 丘陵级 — 谨慎扩展

| 类型 | 值数 | 波及面 | 扩展条件 |
|------|------|--------|----------|
| **MenuItemType** | 11 | 3 跨域 | 加值 → TYPE_TO_MENU_ITEM 需新增行、MenuItemTypeExtensions.Values 反射自动扩展（✅）。删值 → 字典需删行、PageAnalysis.Items 的默认 fallback 需检查。 |
| **ExpectedAction** | 4 | 3 跨域 | 加值 → TYPE_TO_EXPECTED_ACTION 需新增行。4 值可能不够（Python 同 4 值，当前对齐）。 |
| **OperationType** | 5 | 2 域内 | 加值 → Operation 和 RestoreAction 构造器的 Enum.IsDefined 自动兼容（✅）。Python 同 5 值。 |
| **Direction** | 4 | 1 域内 | 加值 → DirectionExtensions.Values 反射自动扩展（✅）。4 值完整覆盖方向空间。 |
| **RegionRole** | 5 | 1 域内 | 加值 → Region 构造器无校验（无 Enum.IsDefined），需补校验。5 值可能不够。 |

### 🟢 平原级 — 自由演进

| 类型 | 波及面 | 变更自由度 |
|------|--------|-----------|
| **BoundingBox** | 3 域内 | 加字段 → 仅 Region/FlattenedElement/FlattenedScreen 需同步。改校验 → 影响构造器。字段名变更 → JSON 键名兼容风险。 |
| **Coordinate** | 5 域内 | 加字段 → 5 个 Content record 需同步。改校验 → 构造器。 |
| **FlattenedElement** | 2 域内 | 加字段 → FlattenedScreen 需同步。改默认值 → 测试需更新。 |
| **FlattenedScreen** | 1 域内 | 加方法 → 无波及。改排序逻辑 → 测试需更新。 |
| **ScreenHints** | 1 域内 | 加字段 → FlattenedScreen 需同步。改默认值 → null vs sentinel 需决策。 |
| **MenuInfo / MenuItem / PopupInfo / PageAnalysis** | 1-2 域内 | 加字段 → 仅 PageAnalysis 可能需同步。 |
| **VisitFingerprint / ContentNode** | 1 域内 | 完全自由。 |

### ⚪ 独立级

| 类型 | 波及面 | 说明 |
|------|--------|------|
| **Target** | 1 (Operation/RestoreAction nullable 引用) | 加字段 → 仅 Operation/RestoreAction 的 nullable Target 需检查。 |
| **RestoreAction** | 2 (Operation.Restore nullable 引用) | 结构与 Operation 几乎相同。 |
| **AndroidWidgetClass** | 0（孤立） | 变更无波及——没有任何代码引用它。 |

### 🔵 跨切面级

| 类型 | 波及面 | 稳定策略 |
|------|--------|----------|
| **DomainValidationException** | 12 全域 | 接口稳定——FieldName + IllegalValue 是不可变的契约。可加构造器参数但不改已有签名。 |
| **DomainJsonOptions** | 全域 | 序列化策略一旦确定不应变更——camelCase + enum-as-string 是 Domain 与外部世界的契约。 |

---

## 3. 波及面详图

### TypeHint 加值波及（假设加 TypeHint.Loading）

```
TypeHint.Loading (新增)
  │
  ├─→ TypeHintExtensions.AliasMap     ← 需加 "loading"→Loading 精确值
  ├─→ TypeHintExtensions.IsInteractive ← 需判断 Loading 是否交互 → 修改 switch
  ├─→ TypeHintExtensions.IsVisualOnly  ← 需判断 Loading 是否纯视觉 → 修改 switch 或显式集
  ├─→ TypeHintExtensions.IsValid(string) ← AliasMap 自动覆盖 ✅
  ├─→ ElementTypeMapper.TypeStringToTypeHintMap ← 需加 "loading"→Loading? 或让回落 Text 覆盖?
  ├─→ FlattenedElement.IsInteractive    ← 调用 TypeHint.IsInteractive()，需同步
  ├─→ FlattenedScreen.GetElementsByType ← 自动兼容（enum 参数）✅
  ├─→ 测试文件                          ← 需新增 Loading 相关断言
  │
  └── Python 对齐？ ← Python TypeHint 无 Loading 值，需论证是否为 C# 扩展
```

**波及点**: 8 处（4 必须改 + 2 需决策 + 2 自动兼容）

### MenuItemType 加值波及（假设加 MenuItemType.Dropdown）

```
MenuItemType.Dropdown (新增)
  │
  ├─→ TYPE_TO_MENU_ITEM 字典           ← 需加 "dropdown"→Dropdown 行
  ├─→ TYPE_TO_EXPECTED_ACTION 字典     ← 需加 "dropdown"→ExpectedAction.? 行（行为需决策）
  ├─→ MenuItemTypeExtensions.Values    ← 反射自动扩展 ✅
  ├─→ MenuItemTypeExtensions.FromValue ← 反射自动扩展 ✅
  ├─→ 测试文件                          ← 需新增断言
  │
  └── Python 对齐？ ← Python MenuItemType 无 Dropdown，需论证
```

**波及点**: 4 处（2 必须改 + 2 自动兼容）

---

## 4. 变更策略矩阵

| 变更类型 | 🔴火山 | 🟡丘陵 | 🟢平原 | ⚪独立 | 🔵跨切面 |
|----------|--------|--------|--------|--------|----------|
| 加 enum 值 | ❌ 禁止 | ⚠️ 需映射表同步 | ✅ 自由 | ✅ 自由 | ❌ 禁止 |
| 删 enum 值 | ❌ 禁止 | ⚠️ 需映射表+fallback同步 | ✅ 自由 | ✅ 自由 | ❌ 禁止 |
| 改 enum 值名 | ❌ 禁止 | ⚠️ 需映射表+JSON同步 | ⚠️ JSON兼容 | ✅ 自由 | ❌ 禁止 |
| 加 record 字段 | ❌ 禁止（enum 无字段） | ✅ 自由 | ✅ 自由 | ✅ 自由 | ✅ 可加 |
| 改 record 默认值 | N/A | ✅ 自由 | ✅ 自由 | ✅ 自由 | ❌ 禁止 |
| 加 AliasMap 行 | ❌ 禁止（enum值不变） | ✅ 可加别名 | ✅ 自由 | ✅ 自由 | N/A |
| 加映射字典行 | ❌ 禁止（enum值不变） | ✅ 需同步 | ✅ 自由 | ✅ 自由 | N/A |
| 改校验规则 | ❌ 禁止 | ✅ 自由 | ✅ 自由 | ✅ 自由 | ❌ 禁止（接口稳定） |

---

## 5. Phase 2 建议演进顺序

Phase 2 的演进应**从稳定向不稳定**推进：

1. **先锁定 🔴火山类型**（TypeHint 8 值、SelectionState 3 值）→ 确认值数不再变化
2. **再扩展 🟡丘陵类型**（MenuItemType/ExpectedAction/OperationType）→ 按需加值，同步映射表
3. **再演进 🟢平原类型**（加字段、改默认值）→ 按业务需求
4. **最后做 🔵跨切面演进**（如有必要）→ 接口不变，内部可优化

**关键原则**：任何 🟡丘陵级扩展都必须先更新映射表，再做 enum 加值。映射表是变更的"缓冲区"——它可以在 enum 加值之前先准备好映射行。
