# Phase 1 执行清单：核心定义 2.0

> **目标**: 从当前 refactor 分支代码 → 方案定义的 ~49 文件结构
> **原则**: 接口与实现分离、一文件一类型、序列化外置、补全遗漏

---

## 当前状态 → 目标状态 总览

| 维度 | 当前 | 目标 |
|------|------|------|
| 源文件数 | 18 | ~49 |
| 多类型文件 | 16/18（全部违规） | 0 |
| 接口位置 | 与实现混合 | 全部在 Domain/ |
| 序列化 | 嵌入模型 | Infrastructure/Serialization/ |
| 枚举命名 | C# PascalCase（无映射） | 同左，桥接层独立处理 Python 兼容 |
| Domain/Models/AI/ | 不存在 | ~13 文件 |
| Domain/Models/Tracing/ | 不存在 | ~6 文件 |
| Domain/Models/Exception/ | 不存在 | ~2 文件 |
| 遗漏类型 | MenuItem, SimulationState 等 | 全部补全 |

---

## 操作清单

每个操作标记为: **新建** / **拆分** / **移入** / **删除**

---

### 第一批：Domain/Models/Vision/（7 文件，基本达标）

当前状态：7 个文件已存在，内容正确。需微小调整。

| # | 操作 | 文件 | 说明 |
|---|------|------|------|
| V1 | **保留** | `Domain/Models/Vision/TypeHint.cs` | `TypeHint` 枚举 + `TypeHintExtensions` 扩展类（枚举+扩展放同一文件可接受） |
| V2 | **拆分** | `Domain/Models/Vision/BoundingBox.cs` | 拆出 `BoundingBoxPixel` → 新文件 `BoundingBoxPixel.cs` |
| V3 | **保留** | `Domain/Models/Vision/FlattenedElement.cs` | 移除 `ToDictionary()`/`FromDictionary()` → 移入序列化层 |
| V4 | **保留** | `Domain/Models/Vision/FlattenedScreen.cs` | 同上，移除序列化方法；集合字段改为 `IReadOnlyList<T>` |
| V5 | **拆分** | `Domain/Models/Vision/Region.cs` | 拆出 `RegionRole` 枚举 → `RegionRole.cs`；移除序列化方法 |
| V6 | **保留** | `Domain/Models/Vision/ScreenHints.cs` | 移除序列化方法 |
| V7 | **拆分** | `Domain/Models/Vision/SelectionState.cs` | 拆出 `SelectionStateExtensions` → `SelectionStateExtensions.cs` |

---

### 第二批：Domain/Models/Common/（6 文件，需补充 3 个 + 重构 3 个）

当前状态：3 个文件存在（Operation.cs、Target.cs、RestoreAction.cs），每个含枚举+模型+序列化。缺 3 个模型。

| # | 操作 | 文件 | 说明 |
|---|------|------|------|
| C1 | **拆分** | `Domain/Models/Common/Operation.cs` | 拆出 `OperationType` 枚举 → `OperationType.cs`；拆出 `Operation` record → `Operation.cs`；移除序列化方法 |
| C2 | **拆分** | `Domain/Models/Common/Target.cs` | 拆出 `TargetType` 枚举 → `TargetType.cs`；拆出 `Target` record → `Target.cs`；移除序列化方法 |
| C3 | **拆分** | `Domain/Models/Common/RestoreAction.cs` | 移除序列化方法 |
| C4 | **新建** | `Domain/Models/Common/MenuItem.cs` | `MenuItem` record，含 `type`、`expected_action`、`expects_page_change`、`expects_state_change` 及默认值 |
| C5 | **新建** | `Domain/Models/Common/Coordinate.cs` | `Coordinate` readonly record struct（归一化坐标 0-1） |
| C6 | **新建** | `Domain/Models/Common/SimulationState.cs` | `SimulationState` record，字段默认值用 `[]` / `{}` 初始化 |

---

### 第三批：Domain/Models/Traversal/（7 文件，从 3 个大文件拆分）

当前状态：3 个大文件（TraversalNode.cs 含 12 类型、TraversalPlan.cs 含 9 类型、Template.cs 含 4 类型）。

| # | 操作 | 文件 | 内容 |
|---|------|------|------|
| T1 | **拆分** | `Domain/Models/Traversal/NodeType.cs` | `NodeType` 枚举（从 TraversalState.cs 移入） |
| T2 | **拆分** | `Domain/Models/Traversal/TraversalNode.cs` | 仅 `TraversalNode` record |
| T3 | **拆分** | `Domain/Models/Traversal/ChildrenStrategyType.cs` | `ChildrenStrategyType` 枚举 |
| T4 | **拆分** | `Domain/Models/Traversal/ChildrenStrategy.cs` | `ChildrenStrategy` record |
| T5 | **拆分** | `Domain/Models/Traversal/DynamicRule.cs` | `DynamicRule` record + `MatchCondition` record + `MatchAction` 枚举 |
| T6 | **拆分** | `Domain/Models/Traversal/ErrorPolicyType.cs` | `ErrorPolicyType` 枚举 |
| T7 | **拆分** | `Domain/Models/Traversal/ErrorPolicy.cs` | `ErrorPolicy` record |
| T8 | **拆分** | `Domain/Models/Traversal/ExitConditionType.cs` | `ExitConditionType` 枚举 |
| T9 | **拆分** | `Domain/Models/Traversal/FallbackAction.cs` | `FallbackAction` 枚举 |
| T10 | **拆分** | `Domain/Models/Traversal/ExitCondition.cs` | `ExitCondition` record |
| T11 | **拆分** | `Domain/Models/Traversal/Precondition.cs` | `Precondition` record |
| T12 | **拆分** | `Domain/Models/Traversal/TargetFoundAction.cs` | `TargetFoundAction` 枚举（从 TraversalPlan.cs 拆出） |
| T13 | **拆分** | `Domain/Models/Traversal/MatchMode.cs` | `MatchMode` 枚举 |
| T14 | **拆分** | `Domain/Models/Traversal/EntryStrategy.cs` | `EntryStrategy` 枚举 |
| T15 | **拆分** | `Domain/Models/Traversal/EntryPolicy.cs` | `EntryPolicy` record |
| T16 | **拆分** | `Domain/Models/Traversal/CompletionPolicyType.cs` | `CompletionPolicyType` 枚举 |
| T17 | **拆分** | `Domain/Models/Traversal/CompletionPolicy.cs` | `CompletionPolicy` record |
| T18 | **拆分** | `Domain/Models/Traversal/TraversalMode.cs` | `TraversalMode` 枚举 |
| T19 | **拆分** | `Domain/Models/Traversal/IntentSlots.cs` | `IntentSlots` record |
| T20 | **拆分** | `Domain/Models/Traversal/TraversalPlan.cs` | `TraversalPlan` record |
| T21 | **拆分** | `Domain/Models/Traversal/Template.cs` | `Template` record（从 Graph/Models/Template.cs 移入） |
| T22 | **拆分** | `Domain/Models/Traversal/EntryConfig.cs` | `EntryConfig` record |

> **注意**: T1-T22 完成后，原 Graph/Models/TraversalNode.cs、TraversalPlan.cs 删除。原 Template.cs 只保留 ITemplateRegistry（移入 Domain/Graph/）和 PlaceholderResolver/TemplateValidator（保留在 Graph/）。

---

### 第四批：Domain/Models/StateMachine/（2 文件）

从 StateMachine/GlobalState.cs、TraversalState.cs 中拆出纯数据部分。

| # | 操作 | 文件 | 内容 |
|---|------|------|------|
| S1 | **拆分** | `Domain/Models/StateMachine/GlobalState.cs` | `GlobalState` 枚举 + `StateTransitionResult` readonly record struct + `StateTransitionEventArgs` record |
| S2 | **拆分** | `Domain/Models/StateMachine/TraversalState.cs` | `TraversalState` 枚举 + `PageRelation` 枚举 |

---

### 第五批：Domain/Models/AI/（~10 文件，全部新建）

当前状态：所有 AI 类型嵌入在 `AI/IAIStrategyAdvisor.cs`（14 个类型），`Domain/Models/AI/` 目录不存在。

| # | 操作 | 文件 | 内容 |
|---|------|------|------|
| A1 | **新建** | `Domain/Models/AI/DecisionResult.cs` | `DecisionResult` 枚举 |
| A2 | **新建** | `Domain/Models/AI/ContainerInference.cs` | `ContainerInference` record |
| A3 | **新建** | `Domain/Models/AI/SafetyTag.cs` | `SafetyTag` 枚举 |
| A4 | **新建** | `Domain/Models/AI/SafetyEvaluation.cs` | `SafetyEvaluation` record |
| A5 | **新建** | `Domain/Models/AI/SafetyScreeningResult.cs` | `SafetyScreeningResult` record |
| A6 | **新建** | `Domain/Models/AI/PageTypeVerification.cs` | `PageTypeVerification` record |
| A7 | **新建** | `Domain/Models/AI/PageLevelGuidance.cs` | `PageLevelGuidance` record |
| A8 | **新建** | `Domain/Models/AI/ContextDecisionResult.cs` | `ContextDecisionResult` record |
| A9 | **新建** | `Domain/Models/AI/MismatchDetails.cs` | `MismatchDetails` record |
| A10 | **新建** | `Domain/Models/AI/Suggestion.cs` | `Suggestion` record |
| A11 | **新建** | `Domain/Models/AI/NodeData.cs` | `NodeData` record（从 AI/ 移入） |
| A12 | **新建** | `Domain/Models/AI/PageAnalysis.cs` | `PageAnalysis` record（从 AI/ 移入） |
| A13 | **新建** | `Domain/Models/AI/PopupInfo.cs` | `PopupInfo` record（从 AI/ 移入） |

---

### 第六批：Domain/Models/Tracing/（~6 文件，全部新建）

当前状态：追踪类型嵌入在 `Observability/ITraceRecorder.cs`，`Domain/Models/Tracing/` 目录不存在。

| # | 操作 | 文件 | 内容 |
|---|------|------|------|
| R1 | **新建** | `Domain/Models/Tracing/TraceSession.cs` | `TraceSession` record |
| R2 | **新建** | `Domain/Models/Tracing/StateTransition.cs` | `StateTransition` record |
| R3 | **新建** | `Domain/Models/Tracing/AICallRecord.cs` | `AICallRecord` record |
| R4 | **新建** | `Domain/Models/Tracing/ExecutionRecord.cs` | `ExecutionRecord` record |
| R5 | **新建** | `Domain/Models/Tracing/ErrorRecord.cs` | `ErrorRecord` record |
| R6 | **新建** | `Domain/Models/Tracing/ErrorSeverity.cs` | `ErrorSeverity` 枚举 |

---

### 第七批：Domain/Models/Exception/（~2 文件，全部新建）

| # | 操作 | 文件 | 内容 |
|---|------|------|------|
| E1 | **新建** | `Domain/Models/Exception/ExceptionSeverity.cs` | `ExceptionSeverity` 枚举 |
| E2 | **新建** | `Domain/Models/Exception/TraversalException.cs` | `TraversalException` 异常基类层级 |

---

### 第八批：Domain 接口（从实现文件中移出）

当前状态：接口与实现混在同一文件。

| # | 操作 | 文件 | 来源 |
|---|------|------|------|
| I1 | **移入** | `Domain/StateMachine/IGlobalStateMachine.cs` | 从 StateMachine/GlobalState.cs 移出 |
| I2 | **移入** | `Domain/StateMachine/ITraversalStateMachine.cs` | 从 StateMachine/TraversalState.cs 移出 |
| I3 | **移入** | `Domain/StateMachine/ITraversalContext.cs` | 从 StateMachine/TraversalState.cs 移出 |
| I4 | **移入** | `Domain/StateMachine/INodeStack.cs` | 从 StateMachine/TraversalState.cs 移出 |
| I5 | **移入** | `Domain/StateMachine/IStackFrame.cs` | 从 StateMachine/TraversalState.cs 移出 |
| I6 | **移入** | `Domain/StateMachine/ITraversalNode.cs` | 从 StateMachine/TraversalState.cs 移出 |
| I7 | **移入** | `Domain/Graph/IGraphTraversalEngine.cs` | 从 Traversal/IGraphTraversalEngine.cs 移入（删除 StateMachine/TraversalState.cs 中的重复空存根） |
| I8 | **移入** | `Domain/Graph/IActionExecutor.cs` | 从 Traversal/IGraphTraversalEngine.cs 移出 |
| I9 | **移入** | `Domain/Graph/ITemplateRegistry.cs` | 从 Graph/Models/Template.cs 移出 |
| I10 | **移入** | `Domain/AI/IAIStrategyAdvisor.cs` | 从 AI/IAIStrategyAdvisor.cs 移入（接口与数据分离） |
| I11 | **移入** | `Domain/Observability/ITraceRecorder.cs` | 从 Observability/ITraceRecorder.cs 移出 |
| I12 | **移入** | `Domain/Observability/IMetricsCollector.cs` | 从 Observability/ITraceRecorder.cs 移出 |

---

### 第九批：实现文件（清理后的保留文件）

| # | 操作 | 文件 | 说明 |
|---|------|------|------|
| M1 | **保留** | `StateMachine/GlobalStateMachine.cs` | 原 GlobalState.cs 中只保留 IGlobalStateMachine 的具体实现（Phase 2 实现） |
| M2 | **保留** | `StateMachine/TraversalStateMachine.cs` | 原 TraversalState.cs 中只保留 ITraversalStateMachine 的具体实现（Phase 2 实现） |
| M3 | **保留** | `StateMachine/NodeStack.cs` | 保留，移除内部 StackFrame → 独立文件或留在同一文件（它是私有辅助类） |
| M4 | **保留** | `Graph/PlaceholderResolver.cs` | 从 Template.cs 拆出的静态工具类 |
| M5 | **保留** | `Graph/TemplateValidator.cs` | 从 Template.cs 拆出的静态工具类 |
| M6 | **保留** | `Graph/ActionExecutor.cs` | Phase 2 Stub 实现 |
| M7 | **保留** | `Graph/GraphTraversalEngine.cs` | Phase 2 实现 |
| M8 | **保留** | `Graph/TemplateRegistry.cs` | Phase 2 实现 |
| M9 | **保留** | `Traversal/TraversalResult.cs` | 从 IGraphTraversalEngine.cs 拆出 |

---

### 第十批：序列化层（新建）

> **枚举策略**：C# 内部始终 PascalCase，不注册全局 `SnakeCaseLower`。
> `PythonBridgeSerializer` 是唯一知道 Python 命名规则的地方——仅在 Phase 3 桥接时使用。

| # | 操作 | 文件 | 说明 |
|---|------|------|------|
| Z1 | **新建** | `Infrastructure/Serialization/DictionaryModelSerializer.cs` | 静态扩展类，包含所有模型的 `ToDictionary()`/`FromDictionary()` |
| Z2 | **新建** | `Infrastructure/Serialization/SerializationExtensions.cs` | 通用 JSON 序列化辅助方法（C# 默认行为，PascalCase） |
| Z3 | **新建** | `Infrastructure/Serialization/PythonBridgeSerializer.cs` | Python 通信专用序列化器（配置 `SnakeCaseLower`，Phase 3 启用） |

---

### 第十一批：需删除的文件

| # | 文件 | 原因 |
|---|------|------|
| D1 | `Graph/Models/TraversalNode.cs` | 已拆分到 Domain/Models/Traversal/ 下 12 个独立文件 |
| D2 | `Graph/Models/TraversalPlan.cs` | 已拆分到 Domain/Models/Traversal/ 下 8 个独立文件 |
| D3 | `Graph/Models/Template.cs` | ITemplateRegistry → Domain/Graph/；Template → Domain/Models/Traversal/；工具 → Graph/ |
| D4 | `Graph/Models/` 目录 | 清空后删除 |
| D5 | `AI/IAIStrategyAdvisor.cs` | 接口 → Domain/AI/；模型 → Domain/Models/AI/ |
| D6 | `AI/` 目录 | 清空后删除（Phase 3 重新创建实现文件） |
| D7 | `Observability/ITraceRecorder.cs` | 接口 → Domain/Observability/；模型 → Domain/Models/Tracing/ |
| D8 | `Observability/` 目录 | 清空后删除（Phase 3 重新创建实现文件） |
| D9 | `Traversal/IGraphTraversalEngine.cs` | 接口移入 Domain/Graph/；TraversalResult/ActionRecord 移入 Traversal/ 或 Domain/Models/ |
| D10 | `Traversal/` 目录 | 清空后删除（Phase 2 重新创建 Graph/ 实现） |
| D11 | `StateMachine/GlobalState.cs` | 拆分为 Domain 接口 + Domain 模型 + StateMachine 实现 |
| D12 | `StateMachine/TraversalState.cs` | 同上 |

---

## 执行顺序

```
第 1 步: 新建目录结构（所有缺失目录）
第 2 步: 新建 Domain/Models/ 补全文件（第五、六、七批 — AI、Tracing、Exception）
第 3 步: 拆分 Vision 和 Common（第一、二批 — 最小改动）
第 4 步: 拆分 Traversal 大文件（第三批 — 最大的拆分工作）
第 5 步: 拆分 StateMachine 模型（第四批 — 枚举移入 Domain/Models/）
第 6 步: 移出接口（第八批 — I1→I12）
第 7 步: 清理实现文件和删除旧文件（第九、十一批）
第 8 步: 新建序列化层（第十批 — 从模型中提取序列化方法）
第 9 步: 编译验证（dotnet build）
第 10 步: 运行现有测试（dotnet test）
```

---

## 文件数量变化

| 阶段 | 操作 | 计数 |
|------|------|:---:|
| 初始 | 当前源文件 | 18 |
| | 新建 Domain/Models/AI/ | +13 |
| | 新建 Domain/Models/Tracing/ | +6 |
| | 新建 Domain/Models/Exception/ | +2 |
| | 新建 Domain/Models/Common/ 补充 | +3 |
| | 新建 Domain/Models/Traversal/（拆分增值） | +22 |
| | 新建 Domain/StateMachine/ 接口 | +6 |
| | 新建 Domain/Graph/ 接口 | +3 |
| | 新建 Domain/AI/ 接口 | +1 |
| | 新建 Domain/Observability/ 接口 | +2 |
| | 新建 Infrastructure/Serialization/ | +3 |
| | 新建 StateMachine/ 接口实现占位 | +2 |
| | 新建 Graph/ 工具类 | +2 |
| | 删除旧文件 | -12 |
| **最终** | | **~70**（含实现占位和序列化层，纯模型 ~49） |
