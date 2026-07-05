## Why

Phase 1/1.1 完成了 Domain 层（24 类型，185 测试全绿），但 C# 非 Domain 层只有 30+ 类型骨架和零引擎逻辑。Python `main` 分支有 11 个非 Domain 模块，核心引擎（Graph + StateMachine + Traversal）需要搬运到 C# 才能启动端到端遍历。同时，现有 C# 骨架有 3 个架构问题需要修正（F-1 PageAnalysis 双定义、NodeType 层级错误、ITraversalContext 类型语义不匹配）。Python 源码验证发现 5 处设计偏差需在搬运时修正（见 docs/refactor/08-phase2-core-engine-design-v2.md §9 决策日志）。

## What Changes

- **BREAKING**: 删除 AI 层简化版 `PageAnalysis`（3 字段）和 `PopupInfo`（3 字段），所有引用改为 Domain 版（12 字段）
- **BREAKING**: `NodeType` enum 从 StateMachine 层移入 Domain.Models.Content 层，6 处引用更新
- **BREAKING**: `ITraversalContext` 接口集合类型修正：`Dictionary<string, object>` → `IReadOnlySet<string>`，`Dictionary<string, List<string>>` → `IReadOnlyDictionary<string, IReadOnlySet<string>>`，`List<string>` → `IReadOnlyList<string>`
- `TraversalRuntimeContext` 从隐含 record 改为 `sealed class` + mutable 内部字段 + `ITraversalContext` 只读接口
- 新增 `TraversalContextSnapshot`（sealed record class，AI advisor 只读快照）
- 新增 Trace 基础层：TraceNode 层级（4 类型）+ ITraceRecorder 补全 + TraversalRuntimeContext（26 字段）
- 新增 Graph 基础层：TraversalPlan 补全（6 字段）+ EntryConfig + PlanCompiler + DynamicMatcher + TemplateInstantiator
- 新增 TraversalFSM（8 状态 × 修正转换矩阵）+ ContainerHandler（三阶段管道，无缓存）+ ErrorHandler（三阶段管道）+ PopupHandler（五步流程 + StateRestorer）
- 新增 StepOrchestrator（14 步拦截层）+ DynamicChildManager + TraceCoordinator + EntryPolicyExecutor + PageCacheManager + PageSnapshotManager
- TraversalFSM 转换矩阵移除 `PRECONDITION_CHECK → BRANCH` 死路径（Python V6.7 handler 从不走此路径）
- ContainerHandler CompletionDetector/FallbackDecider 不移植 Python 的无效缓存设计

## Capabilities

### New Capabilities

- `trace-foundation`: TraceNode 层级（4 类型）+ ITraceRecorder + TraversalRuntimeContext（sealed class + ITraversalContext 只读接口）+ TraversalContextSnapshot + ULID 生成 + "Log and Continue" 模式
- `graph-foundation`: TraversalPlan 补全 + EntryConfig + PlanCompiler + DynamicMatcher + TemplateInstantiator
- `traversal-fsm`: TraversalFSM（8 状态 × 修正转换矩阵）+ GlobalFSM 回调机制
- `container-handler`: CompletionDetector（5 条优先级链，无缓存）+ FallbackDecider（纯计算）+ ContainerActionExecutor（Hook Dispatch 表，4 hook）
- `error-handler`: ErrorClassifier（6 值优先链）+ ErrorStrategySelector（6 类型 × 策略优先链）+ RecoveryExecutor（5 hook）
- `popup-handler`: PopupDetector + PopupClassifier + PopupActionExecutor + StateRestorer（保存/恢复/验证）
- `step-orchestrator`: StepOrchestrator（14 步拦截层）+ StepContext + DynamicChildManager + TraceCoordinator + EntryPolicyExecutor + PageCacheManager + PageSnapshotManager

### Modified Capabilities

- `domain-type-mappings`: ITraversalContext 接口类型修正（强类型只读集合）
- `domain-vision-models`: 无需求变更（仅引用方改动）

## Impact

- **代码**：AI 层（IAIStrategyAdvisor.cs 删除简化版类型）、StateMachine 层（NodeType 移出、ITraversalContext 类型修正）、Graph 层（TraversalPlan 补全）、新增 Traversal/Trace/Observability 层实现
- **API**：ITraversalContext 接口签名变更（Breaking）、IAIStrategyAdvisor 方法签名变更（Breaking）
- **依赖**：新增 Domain.Models.Content 对 NodeType 的依赖、StateMachine 层不再引用 NodeType
- **测试**：现有 Domain 测试不受影响、新增 ~80 单元测试覆盖 7 个新子系统
- **架构修正参考**：docs/refactor/08-phase2-core-engine-design-v2.md
