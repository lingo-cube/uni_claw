## Why

Phase 2.0–2.3 完成了核心引擎搬运（63 任务，9423 行新增代码），但 spec-vs-code 对齐审查发现了 3 条硬约束违反和 16 条中等偏差。核心引擎地基存在结构性缺陷：枚举值数溢出、集合引用泄露、接口归属错误、多项行为缺失。这些问题如果不修正，Phase 3 集成时会导致状态机误转、数据隔离失效、依赖循环等不可恢复的缺陷。

## What Changes

- **BREAKING**: 从 `TraversalState` enum 移除 `DynamicMatch` 成员（9→8 值）。DynamicMatch 是 ChildrenStrategy 值而非 FSM 状态
- **BREAKING**: 将 `ITraversalNode` 和 `IStackFrame` 从 StateMachine 层移入 Graph.Models 层，消除 Graph→StateMachine 双向依赖
- `VisitedChildren` 嵌套集合用 `ReadOnlySetWrapper` 包装，阻断 `HashSet<string>` cast-back 引用泄露
- `PlanCompiler._validate_slots` 补充 scope 合法性校验，非法 scope 抛 DomainValidationException
- `StateRestorer.preserve_state` 改为保存完整 NodeStack（非仅 depth int），`restore_state` 恢复全部 5 字段
- `PopupHandler.HandlePopup` 加顶层 try-catch 兜底到 back 导航
- `PageSnapshotManager.Fingerprint` 改用逐字符确定性哈希（替换 `string.GetHashCode()`）
- `DynamicMatcher` text_pattern 补充 Exact 模式（新增 `TextMatchMode` enum）
- `TraceCoordinator` Log-and-Continue catch block 补充 `Console.WriteLine` 日志输出
- 对全部 10 个"值数锁定"enum 加 `Enum.GetValues<X>().Length == N` 防御性断言测试
- 评估 GlobalState 在 ITraversalContext 的跨 FSM 依赖（仅评估不改）

## Capabilities

### New Capabilities
- `enum-value-guards`: 防御性值数守卫断言测试，对 10 个 enum 加 `Length == N` 测试
- `readonly-set-wrapper`: ReadOnlySetWrapper 不可泄露集合包装，阻断 HashSet cast-back
- `text-match-mode`: TextMatchMode enum（Exact/Contains），DynamicMatcher 双模式匹配

### Modified Capabilities
- `traversal-fsm`: TraversalState 移除 DynamicMatch（9→8 值），转换矩阵不变但 enum 收窄
- `domain-type-mappings`: ITraversalNode/IStackFrame 移入 Graph.Models（接口归属修正）
- `popup-handler`: StateRestorer 保存完整 stack + 恢复全部字段 + HandlePopup 顶层兜底
- `graph-foundation`: PlanCompiler scope 合法性校验补充
- `step-orchestrator`: PageSnapshotManager 确定性哈希 + TraceCoordinator 日志补充

## Impact

- **API breaking**: TraversalState enum 成员减少，任何引用 `TraversalState.DynamicMatch` 的代码需改为 `ChildrenStrategy.DynamicMatch`
- **API breaking**: ITraversalNode namespace 从 `UniClaw.Core.StateMachine` 改为 `UniClaw.Core.Graph.Models`，所有 using 需更新
- **Internal behavior**: VisitedChildren 返回类型不再是 HashSet 引用（ReadOnlySetWrapper），但 `IReadOnlySet<string>` 接面不变
- **Internal behavior**: PreservedState 结构从 `int NodeStackDepth` 改为完整 stack 内容
- **Dependencies**: TraversalNode.cs 不再需要 `using StateMachine`；NodeStack.cs 需新增 `using Graph.Models`
- **Tests**: 新增约 20 个测试（值数守卫 10 + cast-back 2 + scope 校验 1 + Exact 模式 2 + 确定性哈希 1 + StateRestorer 2 + PopupHandler 2）
