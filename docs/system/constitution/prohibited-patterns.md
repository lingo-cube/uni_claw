# Constitution — Prohibited Patterns

> **Tier 1**: 禁止的编码模式、原因、替代方案。
> 详细规格: `docs/system/charter-specification.md` §2.3
> 检查方式: grep test / 代码审查 / ArchUnitNET / Roslyn Analyzer (按难度分层)

---

## P-1: 禁止 ToDictionary / FromDictionary

| 属性 | 内容 |
|------|------|
| **规则** | Domain 和 Graph 层不得定义 `ToDictionary()` 或 `FromDictionary()` 方法 |
| **来源** | PRD §4.4 明确禁止 |
| **原因** | 字典化导致语义压缩——TypeHint 作为 string key 丢失 enum 类型安全；反序列化时无法验证 |
| **替代** | JSON 序列化 (`DomainJsonOptions.Default`) — 保留类型信息 + camelCase + enum-as-string |
| **检查** | grep test (Phase 2.2) → Roslyn Analyzer (Phase 3) |

---

## P-2: 禁止视觉外观 + 行为语义混在一个类型

| 属性 | 内容 |
|------|------|
| **规则** | TypeHint 只回答"看起来像什么"，不得含行为性值 |
| **来源** | P0 fix — 两级映射分离 |
| **原因** | 视觉外观 (TypeHint) 和行为语义 (MenuItemType/ExpectedAction) 是两个独立语义层。混在一起导致 TypeHint "toggle" 既表示视觉又表示行为，破坏语义一致性 |
| **替代** | 行为分类用 ElementTypeMapper 中间字符串 → MenuItemType (11 值) / ExpectedAction (4 值) |
| **检查** | 代码审查 (无简单自动化检查) |

**具体禁止**: TypeHint 不得新增 `"toggle"` / `"menu_item"` / `"input"` / `"loading"` 等行为性值。

---

## P-3: 禁止 ITraversalContext 上暴露 mutation 方法

| 属性 | 内容 |
|------|------|
| **规则** | `ITraversalContext` 接口只暴露 readonly view 和 3 个 allowed setter (CurrentFrame, GlobalState, LastError) |
| **来源** | Phase 2 设计 — 只读接口原则 (D-4) |
| **原因** | AI advisor 通过 ITraversalContext 读取状态，不应能修改引擎内部。mutation 方法在 `TraversalRuntimeContext` class 上 |
| **替代** | mutation 通过 `TraversalRuntimeContext` class 的 engine-only 方法: AppendPath, PopPath, MarkVisited, MarkNodeVisited, IncrementStepCount 等 |
| **检查** | 反射测试 (已实现: `ITraversalContextInterfaceTests.MutationMethods_NotAccessibleViaITraversalContext`) |

---

## P-4: 禁止 HashSet 直接暴露为 IReadOnlySet (嵌套集合场景)

| 属性 | 内容 |
|------|------|
| **规则** | 套集合 (VisitedChildren 内的 `IReadOnlySet<string>`) 不得直接暴露 HashSet 引用 |
| **来源** | H-2 fix — ReadOnlySetWrapper |
| **原因** | `(HashSet<string>)visitedChildren["key"]` 可 cast-back 并调用 Add/Remove，篡改引擎状态 |
| **替代** | `ReadOnlySetWrapper` private sealed class — cast-back throws InvalidCastException |
| **检查** | 运行时测试 (已实现: `VisitedChildrenIsolationTests`) |

**安全等级区分**:
- Level 3 (最强): ReadOnlySetWrapper — 嵌套集合 (VisitedChildren)
- Level 2 (中): IReadOnlySet/IReadOnlyList — 顶层集合 (VisitedPages, CurrentPath)
- Level 1 (弱): 直接 HashSet — 仅限引擎内部自用 (注释标注安全等级)

---

## P-5: 禁止 non-sealed record class

| 属性 | 内容 |
|------|------|
| **规则** | 所有 Domain 和 Phase 2+ record 必须用 `sealed record class` |
| **来源** | 不可变设计约定 |
| **原因** | 非预期继承破坏 sealed-based dispatch 和不可变保证 |
| **例外** | `TraversalRuntimeContext` 是 `sealed class` (非 record, 26 mutable fields) |
| **替代** | 无 — 直接加 `sealed` |
| **检查** | grep test (Phase 2.2) → Roslyn Analyzer (Phase 3) |

---

## P-6: 禁止 DomainValidationException 外的校验异常

| 属性 | 内容 |
|------|------|
| **规则** | Domain 层校验异常统一使用 `DomainValidationException` |
| **来源** | Phase 1 PRD |
| **原因** | 统一异常类型让上层可以 `catch (DomainValidationException)` 一站式处理，FieldName + IllegalValue 提供诊断信息 |
| **替代** | 无 — 直接用 `DomainValidationException` |
| **禁止**: `ValueError`, `InvalidOperationException`, `ArgumentException` for domain value validation |
| **允许**: `InvalidCastException` for runtime isolation (ReadOnlySetWrapper), `NotSupportedException` for FSM invalid transition |
| **检查** | grep test (Phase 2.2) |

---

## P-7: 禁止 TraversalFSM 引用 GlobalFSM 的 state/transition/callback

| 属性 | 内容 |
|------|------|
| **规则** | TraversalFSM 和 GlobalFSM 不得共享 state enum / transition method / callback registration |
| **来源** | FSM 独立性原则 (C-4) |
| **原因** | 两个 FSM 有不同状态空间和迁移规则，交叉引用导致修改一个 FSM 影响另一个 |
| **协调方式**: 仅通过 `ITraversalContext.GlobalState` setter/read |
| **已知偏差**: M-14 — GlobalState setter 在 ITraversalContext 上 (D-7, Phase 3 待修) |
| **检查** | ArchUnitNET type dependency test (Phase 2.2) |
