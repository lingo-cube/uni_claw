# Domain 验证边界分析

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix 后）

---

## 1. 校验策略

Domain 层有两种校验策略：

| 策略 | 机制 | 适用场景 | 代表 |
|------|------|----------|------|
| **Fail-fast** | 构造器 throw DomainValidationException | 值域校验（范围、枚举越界） | Coordinate, BoundingBox, Operation |
| **Graceful 回落** | FromString/MapAndroidClass 回落默认值 + IsValid 通知 | 外部输入解析（AI 输出、字符串匹配） | TypeHint.FromString, SelectionState.FromString, MapAndroidClass |

**原则**：
- **构造期校验 = fail-fast**：不构造非法对象，宁可 throw
- **解析期校验 = graceful**：AI 输出不可控，回落比 throw 更实用；IsValid 让上层感知异常

---

## 2. 校验矩阵

| 类型 | 校验字段 | 未校验字段 | 校验机制 | 风险评级 |
|------|----------|-----------|----------|----------|
| **BoundingBox** | X,Y,Width,Height (4/4) | 无 | DVE fail-fast: 范围+正数 | ✅ 全覆盖 |
| **Coordinate** | X,Y (2/2) | 无 | DVE fail-fast: 范围 | ✅ 全覆盖 |
| **FlattenedElement** | Confidence (1/8) | Id, Text, TypeHint, BoundingBox?, Region?, SelectionState, VisualState? | DVE fail-fast: 范围 | ⚠️ Id 应>0？Text 应非null？ |
| **FlattenedScreen** | Elements.IsDefault (1/2) | ScreenHints? | DVE fail-fast: 非default数组 | ✅ 合理 |
| **Region** | 0/3 | id, bounds, role | **零校验** | ⚠️ id 应非空 |
| **ScreenHints** | 0/6 | 所有字段 | **零校验** | ✅ 全 optional，合理 |
| **MenuInfo** | 0/3 | Name, Coordinate, Active | **零校验** | ⚠️ Name coerces null→empty |
| **MenuItem** | 0/8 | 所有字段 | **零校验** | ✅ 全有合理默认值 |
| **PopupInfo** | 0/3 | 所有字段 | **零校验** | ✅ 全 optional |
| **PageAnalysis** | 0/13 | 所有字段 | **零校验** | ✅ 全有合理默认值 |
| **VisitFingerprint** | FromString 格式 (1/3) | Level1, Level2, ItemName | DVE fail-fast: 格式 | ✅ 合理 |
| **ContentNode** | 0/9 | 所有字段 | **零校验** | ⚠️ Id coerces null→empty |
| **Operation** | Action (1/4) | Target?, Params, Restore? | DVE fail-fast: enum范围 | ✅ 合理 |
| **Target** | By (1/3) | Value (coerces null→empty), Meta | DVE fail-fast: enum范围 | ⚠️ Value 无语义校验 |
| **RestoreAction** | Action (1/3) | Target?, Params | DVE fail-fast: enum范围 | ✅ 合理 |
| **Direction** | FromValue (1/1) | — | DVE fail-fast: 非法值 | ✅ 全覆盖 |
| **MenuItemType** | FromValue (1/1) | — | DVE fail-fast: 非法值 | ✅ 全覆盖 |
| **ExpectedAction** | FromValue (1/1) | — | DVE fail-fast: 非法值 | ✅ 全覆盖 |

---

## 3. 风险项详析

### ⚠️ R1: Region.id 不校验非空

**Python 行为**: `__post_init__` 检查 `if not self.id: raise ValueError("Region id cannot be empty")`
**C# 行为**: 无校验，`id ?? string.Empty` 隐式 coerces null

**风险**: Region.id="" 创建了一个合法但无意义的 Region——下游查找 `GetElementsInRegion("")` 可能意外匹配所有未指定 region 的元素。

**建议**: 加 `if (Id is null || Id.Length == 0) throw new DomainValidationException(nameof(Id), Id);`
**优先级**: P3（04 V8 标记 defer，但逻辑清晰）

### ⚠️ R2: FlattenedElement.Id 不校验

**Python 行为**: id 是 required int，无范围校验（Python 不检查 id>0）
**C# 行为**: 同 Python，无校验

**风险**: id=0 或 id=-1 创建了一个合法但可能混淆的元素。FlattenedScreen 按 id 识别元素，负值 id 可能与查询逻辑冲突。

**建议**: 保持现状（Python 也不校验）。如果后续发现混淆问题，加 `if (Id < 0) throw DVE`。
**优先级**: P3-defer

### ⚠️ R3: Target.Value 无语义校验

**Python 行为**: `Value` 是 required object，无语义校验
**C# 行为**: `Value ?? string.Empty` coerces null

**风险**: Target(By=Text, Value="") 创建了一个"按空文本定位"的 Target——行为定义模糊。Target(By=Coordinate, Value="not a coordinate") 无类型校验。

**建议**: 保持现状（PRD §4.2 明确 Value 是 opaque object）。Phase 2 上层引擎可以加使用期校验。
**优先级**: P3-defer

### ⚠️ R4: MenuInfo.Name coerces null→empty

**Python 行为**: `name` 是 required str（Pydantic 拒 null）
**C# 行为**: `Name ?? string.Empty` 隐式 coerces null→empty

**风险**: MenuInfo(Name=null→"") 创建了一个合法但无意义的菜单项。下游 `GetFingerprint("a", "b")` 返回 `"a|b|"`,与 `GetFingerprint("a", "b", "WiFi")` 不同——不会混淆，但空名菜单项意义模糊。

**建议**: 保持现状。与 Python 的 required-vs-nullable 差异是 04 §2.2 的已知限制。
**优先级**: P3-defer

---

## 4. 校验密度分布图

```
覆盖率 = 校验字段数 / 总字段数

BoundingBox  ████████████████████ 100% (4/4)
Coordinate   ████████████████████ 100% (2/2)
Direction    ████████████████████ 100% (1/1 via FromValue)
MenuItemType ████████████████████ 100% (1/1 via FromValue)
ExpectedAction ████████████████████ 100% (1/1 via FromValue)
VisitFingerprint ████████████████ 50% (1/2 via FromString)
Operation    ████████             25% (1/4)
RestoreAction ████████             33% (1/3)
Target       ██████               33% (1/3)
FlattenedElement ███                12.5% (1/8)
FlattenedScreen ██                  50% (1/2, IsDefault check)
Region       ░░░░░░░░░░░░░░░░░░░░  0% (0/3) ← ⚠️
ScreenHints  ░░░░░░░░░░░░░░░░░░░░  0% (0/6) ← ✅ all optional
MenuInfo     ░░░░░░░░░░░░░░░░░░░░  0% (0/3)
MenuItem     ░░░░░░░░░░░░░░░░░░░░  0% (0/8) ← ✅ defaults
PopupInfo    ░░░░░░░░░░░░░░░░░░░░  0% (0/3)
PageAnalysis ░░░░░░░░░░░░░░░░░░░░  0% (0/13) ← ✅ defaults
ContentNode  ░░░░░░░░░░░░░░░░░░░░  0% (0/9)
```

**规律**：
- **几何/数值类型** → 高校验覆盖率（范围校验有意义）
- **聚合/配置类型** → 0% 校验（字段全 optional 或有合理默认值）
- **混合类型** → 低覆盖率（核心字段校验，其余 optional）

**真正的漏洞**：只有 **Region.id** 的 0% 覆盖率是不合理的——id 是 required 字段而非 optional，空 id 无业务意义。

---

## 5. Phase 2 校验建议

| 建议 | 类型 | 优先级 |
|------|------|--------|
| 加 Region.id 非空校验 | Region | P3 |
| 加 Region.role enum 范围校验 | Region | P3 |
| 评估 FlattenedElement.Id ≥ 0 | FlattenedElement | P3-defer |
| 评估 Target.Value 语义校验（按 By 类型分支） | Target | P3-defer |
| TypeHint.IsValid(string) 前置校验链路文档化 | TypeHint | P3（已有方法，需文档化使用模式） |
| SelectionState.IsValid(string) 前置校验链路文档化 | SelectionState | P3 |
