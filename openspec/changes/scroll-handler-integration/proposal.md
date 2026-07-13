# Proposal: ScrollHandler Integration

## Why

当前 TraversalEngine 缺少滚动感知能力，导致高级基线测试无法工作。DynamicMatch 策略只能看到 `threshold=0.0` 时的可见元素（初始视口），无法触发滚动访问后续 scroll segment 的内容，导致 AllChildrenVisited 过早触发。

这阻塞了：
- **HierarchyBaselineTests**: 4 层级导航测试（4/4 场景失败，仅访问 3 页）
- **LongListBaselineTests**: 长列表滚动测试（3/3 场景失败，元素覆盖率 4-13%）

虽然 ScrollHandler 组件已存在，但与 TraversalEngine 完全独立，没有集成到主遍历循环中。

## What Changes

### 核心变更

- **IVisionProvider 扩展**: 添加 `HasScroll()`, `GetScrollProgress()`, `IsEndOfList()` 滚动感知接口
- **TraversalRuntimeContext 扩展**: 添加滚动状态字段和 `UpdateScrollProgress()` 方法
- **TraversalFSM 状态扩展**: 新增 `ScrollCheck` 状态用于滚动决策点
- **StepOrchestrator 集成**: 添加滚动检查逻辑，委托 ScrollHandler 决策和执行
- **ExitCondition 扩展**: 新增 `AllChildrenVisitedOrScrollEnd` 退出条件类型

### 测试适配

- 更新 HierarchyBaselineTests 以验证 4 层级完整遍历
- 更新 LongListBaselineTests 以验证 20-30 项列表完整遍历
- 新增 ScrollFSM 集成测试验证状态转换
- 确保现有 8 个基线场景继续通过

### 文档更新

- `docs/system/layers/state-machine.md`: 添加 ScrollCheck 状态和状态转换表
- `docs/system/layers/traversal.md`: 更新 StepOrchestrator 滚动决策逻辑
- `docs/system/decisions/log.md`: 提取 ScrollHandler 集成决策

## Capabilities

### New Capabilities

- **scroll-aware-traversal**: TraversalEngine 滚动感知遍历能力
  - 支持 DynamicMatch 在子节点耗尽时触发滚动
  - 支持 FSM 状态流转包含滚动检查点
  - 支持滚动进度跟踪和末尾检测

### Modified Capabilities

- **traversal-engine**: 扩展 TraversalEngine 以支持滚动感知遍历
  - REQUIREMENTS 变更：集成 ScrollHandler 到主遍历循环
  - 新增滚动状态管理和决策触发点

- **dynamic-child-generation**: DynamicMatch 扩展以感知可滚动内容
  - REQUIREMENTS 变更：子节点生成考虑滚动内容的分段特性

## Impact

### 代码变更

**受影响组件**:
- `src/UniClaw.Core/Traversal/TraversalEngine.cs` - 扩展初始化和上下文管理
- `src/UniClaw.Core/Traversal/StepOrchestrator.cs` - 添加滚动决策逻辑
- `src/UniClaw.Core/StateMachine/TraversalFSM.cs` - 新增 ScrollCheck 状态
- `src/UniClaw.Core/Traversal/IGraphTraversalEngine.cs` - 接口保持不变
- `src/UniClaw.Core/Domain/Traversal/IVisionProvider.cs` - 扩展接口
- `src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs` - 实现新接口
- `src/UniClaw.Core/Simulation/StatefulMockVisionService.cs` - 默认实现

**新增依赖**:
- TraversalFSM → ScrollHandler (新增集成点)
- StepOrchestrator → ScrollHandler (新增委托)

**测试影响**:
- `tests/UniClaw.Core.Tests/Baseline/HierarchyBaselineTests.cs` - 需要更新以通过
- `tests/UniClaw.Core.Tests/Baseline/LongListBaselineTests.cs` - 需要更新以通过
- `tests/UniClaw.Core.Tests/StateMachine/ScrollFSMIntegrationTests.cs` - 新增集成测试

### API 影响

**新增接口**:
```csharp
public interface IVisionProvider
{
    // 现有方法...
    
    // 新增滚动感知接口
    bool HasScroll();
    double GetScrollProgress();
    bool IsEndOfList();
}
```

**向后兼容**:
- IVisionProvider 扩展默认返回 false/0.0/true，现有实现兼容
- 现有 8 个基线场景无行为变化
- ScrollableBaselineTests 无影响

### 依赖影响

- **无外部依赖变更**
- **无 breaking changes**（仅扩展接口）
- Phase 3 前置依赖：完成此变更后，advanced-simulation-baseline 可恢复

### CI 影响

- 新增 ScrollFSM 集成测试（CI-blocking）
- 高级基线测试 7 个场景从 blocked → active（CI-blocking）
- 预计测试执行时间增加 < 500ms

### 向后兼容

- ✅ 完全向后兼容
- 现有测试无影响
- 现有遍历行为不变（除非使用新的滚动感知配置）
