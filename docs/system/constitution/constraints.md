# Constitution — Hard Constraints

> **Tier 1**: 跨 Phase 不变，CI 强制执行。每条规则有对应 Guard test。
> 详细规格: `docs/system/charter-specification.md` §2
> 更新触发: 新增 locked enum / 发现新 constraint / 架构重构

---

## C-1: TraversalState 值锁定 = 8 [火山级]

**违反后果**: FSM 迁移矩阵失效, DynamicMatch 混入已修复 (H-1)
**影响范围**: TraversalFSM, StepOrchestrator, all handler tests
**Guard**: `EnumValueGuardTests.TraversalState_Has8Values`
**决策记录**: → decisions/log.md D-5

**8 个值**: NodeSelect, PreconditionCheck, Execute, ResultVerify, Branch, FrameComplete, ErrorHandling, PopupHandling

**为什么锁定**: TraversalState 是 FSM 状态空间的基础。加一个值意味着所有迁迁矩阵、StepOrchestrator 的 14-step 逻辑、handler 的状态感知都要变更。H-1 事故证明：DynamicMatch 作为 ChildrenStrategyType 值被错误放入 TraversalState，导致 FSM 矩阵中有不可达状态。

---

## C-2: TypeHint 值锁定 = 8 [火山级]

**违反后果**: 4 域 cascade — TypeHintExtensions.AliasMap, IsInteractive, IsVisualOnly, ElementTypeMapper.TypeStringToTypeHintMap
**影响范围**: Domain.Vision + Domain.Content + Domain.Mappings + DynamicMatcher (Phase 2)
**Guard**: `EnumValueGuardTests.TypeHint_Has8Values`
**决策记录**: → decisions/log.md D-1

**8 个值**: Text, Image, Button, Link, InputField, Checkbox, Dropdown, ClickableText

**为什么锁定**: TypeHint 是「视觉外观」层的核心标识符。它只回答"看起来像什么"。任何新值都会 cascade 到：AliasMap (别名映射)、IsInteractive (交互性判断)、IsVisualOnly (纯视觉判断)、TypeStringToTypeHintMap (字符串映射)。这些全在跨域桥上，修改一个值需要同步更新 4 个地方。

**扩展替代方案**: 如果需要行为分类，用中间字符串 (ElementTypeMapper) → MenuItemType / ExpectedAction。不在 TypeHint 里加行为性值如 "toggle"/"menu_item"/"input"。

---

## C-3: Domain 三岛零互 import [架构级]

**规则**: Domain.Vision ↔ Domain.Content ↔ Domain.Common 零直接 import
**唯一桥**: Mappings (ElementTypeMapper)
**违反后果**: 跨域语义泄漏, 两级映射分离原则被破坏
**Guard**: 待新增 (ArchUnitNET namespace isolation test — Phase 2.2)
**决策记录**: → decisions/log.md D-2

**为什么重要**: Domain 层的三个子域 (Vision/Content/Common) 是语义独立的三岛。Vision 回答"看起来像什么"，Content 回答"能做什么 / 有什么内容"，Common 回答"操作是什么"。如果 Vision 直接 import Content 的 MenuItemType，就把行为语义泄漏到了视觉层——这正是 P0 fix 修复的问题 (MapAndroidClass 返回 TypeHint → 视觉行为混淆)。

---

## C-4: FSM 独立性原则 [架构级]

**规则**: TraversalFSM 和 GlobalFSM 不得共享 state/transition/callback
**协调方式**: 仅通过 `ITraversalContext.GlobalState`
**已知偏差**: M-14 — GlobalState setter 在 ITraversalContext 上，创造类型级跨 FSM 依赖
**偏差处理**: Phase 3 待修，当前不修 (D-7)
**Guard**: 待新增 (FSM type dependency check — Phase 2.2)
**决策记录**: → decisions/log.md D-7

**为什么重要**: 两个 FSM 有不同的状态空间和迁移规则。TraversalFSM 是微观 (节点选择 → 执行 → 验证)，GlobalFSM 是宏观 (初始化 → 遍历 → 完成)。如果它们共享 state 类型，修改一个 FSM 的状态会影响另一个的迁移逻辑，导致调试困难。

---

## C-5: Graph→StateMachine 单向依赖 [架构级]

**规则**: Graph → StateMachine (using), 禁止反向
**已修复**: H-5 (ITraversalNode 已从 StateMachine 移到 Graph.Models)
**违反后果**: 双向依赖导致循环，无法独立演进和测试
**Guard**: `DependencyDirectionGuardTests` (3 tests)
**决策记录**: → decisions/log.md D-6

**3 个 Guard tests**:
- `TraversalNode_DoesNotReferenceStateMachineNamespace`
- `ITraversalNode_ResidesInGraphModelsNamespace`
- `TraversalState_DoesNotContainITraversalNodeOrIStackFrame`

**为什么重要**: Graph 层定义节点类型 (TraversalNode, MatchCondition 等)，StateMachine 层定义运行时状态 (TraversalFSM, Context 等)。如果 StateMachine import Graph 的 ITraversalNode，Graph 修改接口会影响 FSM 运行逻辑。单向依赖确保 Graph 可以独立修改节点 schema 而不影响 FSM 实现。

---

## C-6: ReadOnlySetWrapper cast-back 阻断 [安全级]

**规则**: VisitedChildren 不得通过 cast-back 修改引擎内部数据
**实现**: `ReadOnlySetWrapper` private sealed class — `(HashSet<string>)wrapper` throws InvalidCastException
**违反后果**: AI advisor 或外部 consumer 可篡改引擎遍历状态
**Guard**: `VisitedChildrenIsolationTests.VisitedChildren_CastBackToHashSet_ThrowsInvalidCastException`
**决策记录**: → decisions/log.md D-9

**为什么重要**: 遍历引擎的 visited 集合是核心状态。如果 AI advisor 通过 ITraversalContext 获取 VisitedChildren 后 cast-back 到 HashSet 并调用 Add/Remove，引擎的遍历逻辑会静默失效——不会报错，但遍历行为会不一致。ReadOnlySetWrapper 从运行时层面杜绝了这个可能性。

---

## C-7: GlobalState 值锁定 = 8 [火山级]

**违反后果**: GlobalFSM 迁移矩阵失效
**影响范围**: GlobalFSM, StepOrchestrator, TraversalFSM (通过 Context)
**Guard**: `EnumValueGuardTests.GlobalState_Has8Values`
**决策记录**: → decisions/log.md D-3

**8 个值**: Idle, Initializing, Traversing, Paused, Error, Recovering, Completed, Terminated
**Terminal 状态**: Completed, Terminated — 不可迁出

---

## C-8: SelectionState 值锁定 = 3 [火山级]

**违反后果**: 2 域 cascade — SelectionStateExtensions (SelectedAliases, DisabledAliases), FlattenedElement.IsInteractive
**影响范围**: Domain.Vision + Domain.Content (通过 FlattenedElement)
**Guard**: `EnumValueGuardTests.SelectionState_Has3Values`
**决策记录**: → decisions/log.md D-4

**3 个值**: Normal, Selected, Disabled

---

## C-9: sealed record class 约定 [规范级]

**规则**: 所有 Domain 和 Phase 2+ record 必须用 `sealed record class`
**违反后果**: 非预期继承，不可变设计被破坏
**Guard**: 待新增 (grep test 或 Roslyn Analyzer — Phase 2.2 / Phase 3)
**例外**: TraversalRuntimeContext 是 `sealed class` (非 record，26 mutable fields 不适合 record)

---

## C-10: DomainValidationException 统一校验 [规范级]

**规则**: Domain 层校验异常统一使用 `DomainValidationException` (FieldName + IllegalValue)
**禁止**: `ValueError`, `InvalidOperationException`, `ArgumentException` for domain validation
**违反后果**: 校验异常类型不一致，上层无法统一捕获处理
**Guard**: 待新增 (grep test — Phase 2.2)
