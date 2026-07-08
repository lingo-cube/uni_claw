## Why

TraversalEngine.RunAsync 有 5 条硬终止路径 (AllVisited, AntiLoop, MaxSteps, Cancelled, Error)，但缺失用户意图层面的终止维度。`CompletionPolicy` 类型已完整定义 (TargetFound/Timeout/MaxSteps + MatchMode + TargetFoundAction)，`TraversalRuntimeContext._completionPolicy` 能存储它，但 RunAsync 循环从不读取和检查它 — 这意味着用户配置的目标搜索、超时终止、软步数上限全部无效。

## What Changes

- 新增 RunAsync 循环内 CompletionPolicy 检查块 (3 个 if 分支: TargetFound, Timeout, MaxSteps)，位于 AntiLoop 检查之后、引擎硬上限之前
- **⚠️ 关键设计决策**: TargetFound 匹配用 `Operation.Target.Value` (元素文本, 如 "Dark mode")，不用 `Name` (Name = template ID, 如 "switch_leaf")
- 新增 `TraversalResult.Reasons.TargetFound` 和 `TraversalResult.Reasons.Timeout` 两个常量
- 修改 Done() GlobalState 映射: TargetFound → Completed, Timeout → Terminated
- Phase A 对 TargetFoundAction.ExecuteThenStop 等价 MarkAndStop 处理 (Phase 3 完整实现)

## Capabilities

### New Capabilities
- `completion-policy-check`: RunAsync 循环内 CompletionPolicy 检查逻辑 (TargetFound/Timeout/MaxSteps 三个终止维度)

### Modified Capabilities
- `traversal-engine`: 新增 CompletionPolicy 检查插入点 + Reasons 新增 TargetFound/Timeout + Done() GlobalState 映射变更

## Impact

- **代码**: TraversalEngine.cs (RunAsync + Done), TraversalResult.cs (Reasons 常量)
- **测试**: 5 个新增 TraversalEngine 单元测试
- **依赖**: CompletionPolicy/MatchMode/TargetFoundAction 类型定义已完成; TraversalRuntimeContext._completionPolicy 已存在
- **下游**: Phase B (simulation-baseline-tests) 依赖 TargetFound 终止能力
