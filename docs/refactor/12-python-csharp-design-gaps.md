# Python↔C# 正确性偏差报告

> 基于 main 分支 Python 源码与 feature/refactor 分支 C# 代码的**仅正确性**对比。
> 范围: C# 已实现的逻辑与 Python **行为矛盾**或不一致的地方。
> 不含: 缺失子系统、placeholder (未完成)、C# 正确增强。
> 所有条目已记录在 → decisions/log.md D-10~D-13
> 日期: 2026-07-05

---

## F-1: DismissStrategyMap 静态映射 vs Python 条件逻辑 (🔴 高)

**C# 代码**: `PopupClassifier.DismissStrategyMap` — 按 PopupType 的静态字典映射，不检查有无 dismiss target。

**Python 代码**: `_determine_dismiss_strategy(popup_type, ui_elements)` — 两步条件逻辑:
1. 如果找到 dismiss target → `"auto_close"`
2. 否则按 PopupType fallback

**具体值偏差**:

| PopupType | Python 有target | Python 无target | C# DismissStrategyMap | 偏差 |
|-----------|----------------|----------------|----------------------|------|
| Permission | auto_close | **wait_timeout** | AutoClose | ❌ Python 等timeout, C# 强制auto_close |
| Error | auto_close | **auto_close_or_back** | **Back** | ❌ Python 先尝试再回退, C# 直接回退 |
| Ad | auto_close | **back** | **WaitTimeout** | ❌ Python 立即回退, C# 等timeout |
| Dialog | auto_close | back | AutoCloseOrBack | ❌ Python 无target→back, C#→auto_close_or_back |
| Unknown | auto_close | back | Back | ✅ 对齐 |

**影响**: 当页面没有 dismiss 按钮时，Permission/Error/Ad 三种 popup 的处理策略在 C# 和 Python 中**完全不同**。这是运行时行为偏差，不是命名偏差。

**决策记录**: → decisions/log.md D-10 (Status: Open)

---

## F-2: UrgencyLevel 有死值 Critical (🟡 中)

**C# enum**: `UrgencyLevel { Low, Medium, High, Critical }` — 4 值

**Python enum**: `UrgencyLevel { LOW, MEDIUM, HIGH }` — 3 值

**偏差**: C# 的 `Critical` 在 `PopupClassifier.DetermineUrgency()` 中不可达——没有任何 PopupType 会赋值 Critical。Python 不需要此值因为 popup 语义里没有"比 High 更紧急"的级别。

**影响**: Guard test `UrgencyLevel_Has4Values` 硬编码 4，如果改为 3 需同步更新。当前 Critical 是死值不产生运行时错误，但违反「每个 enum 值都有可达路径」原则。

**决策记录**: → decisions/log.md D-11 (Status: Open)

---

## F-3: CompletionReason 缺少 Error 值 (🟡 中)

**C# enum**: `CompletionReason { Timeout, MaxDepth, AllVisited, Incomplete }` — 4 值

**Python enum**: `CompletionStatus { ALL_VISITED, MAX_DEPTH, TIMEOUT, INCOMPLETE, ERROR }` — 5 值

**偏差**: Python 有 `ERROR` 作为完成原因 (container 处理中发生错误导致完成), C# 没有。

**影响**: 当 ErrorHandler 触发 backtrack → container 完成, C# 无法记录这是 error-driven completion，只能记为 `Incomplete` — 丢失语义信息。CompletionDetector 没有返回 `CompletionReason.Error` 的路径。

**决策记录**: → decisions/log.md D-12 (Status: Open)

---

## F-4: PreconditionCheck→Branch 迁移路径被 D-1 移除 (🟡 中)

**Python VALID_TRANSITIONS**: `PRECONDITION_CHECK → { EXECUTE, BRANCH, ERROR_HANDLING }`

**C# TransitionMatrix**: `PreconditionCheck → { Execute, ErrorHandling }` — **Branch 被移除**

**D-1 修正论据**: "Python V6.7 handler 从不返回 Branch from PreconditionCheck"

**偏差**: Python 的 VALID_TRANSITIONS **明确包含** PreconditionCheck→Branch，意味着 FSM 层允许此迁移。Python handler 可能从不走此路径，但 FSM 不拒绝它。C# FSM 在 TransitionTo(Branch) from PreconditionCheck 时会抛 DomainValidationException — 这在 Python 中是合法的。

**需要验证**: Python `_handle_precondition_check` 的 3-round retry + correction 逻辑是否在某些 edge case 返回 Branch? 如果是, C# 的移除就是正确性偏差。如果确实从不返回, 移除是合理的简化。

**决策记录**: → decisions/log.md D-13 (Status: Open)

---

## 已确认对齐 (无偏差)

以下 enum 值经逐一对比，确认 Python↔C# 语义完全对齐 (仅命名风格差异):

| Enum | Python 值 | C# 值 | 对齐? |
|------|---------|-------|------|
| `GlobalState` | IDLE/INITIALIZING/TRAVERSING/PAUSED/ERROR/RECOVERING/COMPLETED/TERMINATED | Idle/Initializing/Traversing/Paused/Error/Recovering/Completed/Terminated | ✅ 8值完全对齐 |
| `PopupType` | PERMISSION/ERROR/AD/DIALOG/UNKNOWN | Permission/Error/Ad/Dialog/Unknown | ✅ 5值完全对齐 |
| `BlockingType` | MODAL/NON_MODAL/TOAST | Modal/NonModal/Toast | ✅ 3值完全对齐 |
| `DismissStrategy` | auto_close/back/wait_timeout/auto_close_or_back | AutoClose/Back/WaitTimeout/AutoCloseOrBack | ✅ 4值名对齐, 但**映射逻辑不同** (见 F-1) |
| `ErrorType` | NETWORK/UI_ELEMENT/TIMEOUT/PERMISSION/APP_CRASH/UNKNOWN | Network/UiElement/Timeout/Permission/Crash/Unknown | ✅ 6值语义对齐 (值名风格不同) |
| `ErrorStrategy` | SKIP/RETRY/BACKTRACK/CONTINUE/ABORT | Skip/Retry/Backtrack/Continue/Abort | ✅ 5值完全对齐 |
| `NodeType` | CONTAINER/LEAF_SWITCH/LEAF_SLIDER/LEAF_ACTION/LEAF_INFO/SCREEN/ACTION/TARGET | Container/LeafSwitch/LeafSlider/LeafAction/LeafInfo/Screen/Action/Target | ✅ 8值完全对齐 |
| `FallbackAction` | BACK/AUTO_ESCAPE/SKIP/ABORT | Back/AutoEscape/Skip/Abort | ✅ 4值完全对齐 |
| `ExitConditionType` | ALL_CHILDREN_VISITED/DEPTH_LIMITED/SINGLE_LEVEL | AllChildrenVisited/DepthLimited/SingleLevel | ✅ 3值完全对齐 |
| `ChildrenStrategyType` | STATIC/DYNAMIC_MATCH/NONE | Static/DynamicMatch/None | ✅ 3值完全对齐 |

---

## 对 constitution 和 guard tests 的影响

| 条目 | 当前状态 | 需要变更 |
|------|---------|---------|
| `UrgencyLevel_Has4Values` guard | 硬编码 4 | D-11 决定后: 可能改为 Has3Values 或保留 |
| `CompletionReason` 无 guard | 不在 10 enum guard 中 | D-12 决定后: 如果新增 Error 值需评估是否加 guard |
| `DismissStrategy_Has4Values` guard | 硬编码 4 | 值数正确 (4=4), 但映射逻辑需修正 (F-1) |
| `TraversalState_Has8Values` guard | 硬编码 8 | D-13 决定后: 值数不变 (8=8), 但 TransitionMatrix 可能需要新增 1 条迁移路径 |
