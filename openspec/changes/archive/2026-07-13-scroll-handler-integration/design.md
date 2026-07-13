# Design: ScrollHandler Integration

## Context

### Current State

**TraversalEngine 当前架构**:
```
TraversalEngine
├── TraversalFSM (遍历状态机)
├── StepOrchestrator (步骤编排)
├── DynamicChildManager (动态子节点生成)
└── ❌ 没有滚动感知

ScrollHandler (独立组件)
├── ScrollabilityDetector (可滚动性检测)
├── ScrollClassifier (滚动分类)
├── ScrollDecider (滚动决策)
└── ScrollActionExecutor (滚动执行)
```

**问题表现**:
- HierarchyBaselineTests: 4/4 场景失败，ActionHistoryCount=1，仅访问 3 页
- LongListBaselineTests: 3/3 场景失败，元素覆盖率 4-13%
- ScrollableBaselineTests: 6/6 通过（设计规避了问题）

**根本原因**:
1. DynamicMatch 只从 `PageAnalysis.Items` 生成子节点
2. `PageAnalysis` 只包含 `threshold <= current_progress` 的元素
3. `current_progress` 初始为 0.0，只有第一段可见
4. TraversalEngine 没有滚动触发机制
5. AllChildrenVisited 过早触发

### Constraints

**架构约束** (constitution):
- C-4: FSM 独立性原则
- C-5: TraversalFSM 与 GlobalFSM 不共享状态
- C-11: 基线 E2E 回归门槛（必须通过，CI-blocking）

**现有组件**:
- ScrollHandler 已存在且功能完整（7步流程）
- ScrollableMockVisionService 支持滚动状态管理
- ScrollableMockActionExecutor 支持滚动执行

**向后兼容要求**:
- 现有 8 个基线场景必须继续通过
- IVisionProvider 接口扩展必须有默认实现
- 不破坏现有遍历行为

### Stakeholders

- 基线测试使用者：需要可靠的回归检测
- CI 系统：需要快速、稳定的测试执行
- Phase 3 开发：高级基线测试依赖此集成

## Goals / Non-Goals

**Goals:**
1. 集成 ScrollHandler 到 TraversalEngine 主遍历循环
2. 支持 DynamicMatch 感知可滚动内容
3. 支持 FSM 状态流转包含滚动检查点
4. 支持滚动进度跟踪和末尾检测
5. 保持向后兼容，现有测试无影响

**Non-Goals:**
- 修改 ScrollHandler 核心逻辑（7步流程保持不变）
- 修改现有 ScrollableBaselineTests（应继续通过）
- 新增滚动策略（使用现有 ScrollDecider）
- 性能优化（滚动决策同步执行即可）

## Decisions

### Decision 1: 集成方案选择 — FSM 扩展方案

**选择**: 将滚动状态集成到 TraversalFSM，在适当的状态点触发滚动决策。

**理由**:
- 符合现有 FSM 架构模式（PopupHandler、ErrorHandler 都有对应状态）
- 滚动是遍历流程中的自然步骤
- 便于状态转换可视化（添加 ScrollCheck 状态）

**考虑过的替代方案**:
- **Container 扩展方案**: 将滚动逻辑封装在 ContainerHandler 内部
  - ❌ 拒绝：滚动逻辑与容器处理耦合，难以支持复杂滚动策略

**架构变更**:
```
TraversalFSM 状态扩展:
├── Container (容器处理)
├── NodeSelect (节点选择)
├── ActionExecute (动作执行)
├── ResultVerify (结果验证)
└── 🆕 ScrollCheck (滚动检查) ← 新增状态

状态流转:
Container → NodeSelect → ActionExecute → ResultVerify
                              ↓
                         ScrollCheck ← 新增分支点
                              ↓
                    [有更多内容?] → Yes → ActionExecute (scroll)
                              ↓
                         No → Container (完成当前节点)
```

### Decision 2: IVisionProvider 扩展设计

**选择**: 添加三个滚动感知接口方法，使用默认实现保持兼容。

**接口定义**:
```csharp
public interface IVisionProvider
{
    // 现有方法...
    
    // 滚动感知接口
    bool HasScroll();              // 当前页面是否有滚动数据
    double GetScrollProgress();    // 当前滚动进度 (0.0-1.0)
    bool IsEndOfList();            // 是否到达列表末尾
}
```

**默认实现**:
```csharp
// StatefulMockVisionService 等非滚动实现
bool HasScroll() => false;
double GetScrollProgress() => 0.0;
bool IsEndOfList() => true;
```

**理由**:
- 接口扩展而非新建，保持兼容性
- 默认实现确保现有代码无需修改
- 方法语义清晰，易于理解

### Decision 3: TraversalRuntimeContext 扩展

**选择**: 添加滚动状态字段和更新方法，不引入新的 Context 类型。

**新增字段**:
```csharp
public class TraversalRuntimeContext : ITraversalContext
{
    // 现有字段...
    
    // 滚动相关状态
    public double CurrentScrollProgress { get; private set; }
    public bool HasScrollableContent => _scrollHandler != null;
    public bool IsAtScrollEnd => /* 通过 IVisionProvider.IsEndOfList() */;
    
    public void UpdateScrollProgress(double progress)
    {
        CurrentScrollProgress = progress;
        // 触发滚动状态变更事件（如需要）
    }
}
```

**理由**:
- 避免引入新的 Context 类型（减少复杂性）
- 滚动状态是遍历状态的一部分，不是独立的 Context
- 与现有 GlobalState 管理（Traversing/Paused 等）保持一致

### Decision 4: StepOrchestrator 滚动决策集成

**选择**: 在 StepOrchestrator 中添加滚动检查点，委托 ScrollHandler 决策。

**集成逻辑**:
```csharp
public class StepOrchestrator
{
    private ScrollHandler? _scrollHandler; // 可选注入
    
    public StepResult ExecuteStep(StepContext ctx)
    {
        // 现有逻辑...
        
        // 滚动检查点：子节点耗尽且有滚动内容
        if (ShouldCheckForScroll(ctx))
        {
            return HandleScrollDecision(ctx);
        }
        
        return _existingLogic;
    }
    
    private bool ShouldCheckForScroll(StepContext ctx)
    {
        // 检查条件：
        // 1. 当前节点使用 DynamicMatch
        // 2. 所有可见子节点已访问
        // 3. IVisionProvider.HasScroll() == true
        // 4. IVisionProvider.IsEndOfList() == false
        return /* 上述条件 */;
    }
    
    private StepResult HandleScrollDecision(StepContext ctx)
    {
        // 委托 ScrollHandler 决策
        var scrollAction = _scrollHandler.DecideScroll(
            hasScroll: ctx.Context.VisionProvider.HasScroll(),
            isEnd: ctx.Context.VisionProvider.IsEndOfList(),
            progress: ctx.Context.CurrentScrollProgress
        );
        
        // 执行滚动
        return ExecuteScrollAction(ctx, scrollAction);
    }
}
```

**理由**:
- StepOrchestrator 是步骤编排的天然位置
- 滚动决策与现有决策流程并行（不干扰）
- ScrollHandler 保持独立（通过委托模式）

### Decision 5: ExitCondition 扩展

**选择**: 新增 `AllChildrenVisitedOrScrollEnd` 退出条件类型。

**新增类型**:
```csharp
public enum ExitConditionType
{
    // 现有类型...
    AllChildrenVisited,
    
    // 新增
    AllChildrenVisitedOrScrollEnd  // 子节点访问完 OR 到达滚动末尾
}
```

**实现逻辑**:
```csharp
// ExitCondition 评估
public bool IsSatisfied(ITraversalContext context)
{
    if (Type == ExitConditionType.AllChildrenVisitedOrScrollEnd)
    {
        // 条件 1: 所有子节点已访问
        var allChildrenVisited = /* 现有逻辑 */;
        
        // 条件 2: 到达滚动末尾（如果有滚动内容）
        var atScrollEnd = context.VisionProvider.IsEndOfList();
        
        return allChildrenVisited || (context.HasScrollableContent && atScrollEnd);
    }
    
    // 现有逻辑...
}
```

**理由**:
- 明确语义：子节点耗尽或到达末尾都算完成
- 不破坏现有 AllChildrenVisited 行为
- 向后兼容（新增类型，不影响现有使用）

## Risks / Trade-offs

### Risk 1: FSM 状态爆炸

**风险**: 每添加一个新功能都需要新增 FSM 状态，导致状态管理复杂。

**缓解**:
- ScrollCheck 状态设计为轻量级决策点（不长期停留）
- 状态转换表保持简洁（只增加必要转换）
- 文档化状态转换逻辑（state-machine.md §3.2）

### Risk 2: 滚动性能影响

**风险**: 每个步骤都检查滚动，增加遍历延迟。

**缓解**:
- 滚动检查只在特定条件下触发（ShouldCheckForScroll）
- 大多数页面无滚动内容，HasScroll() 快速返回
- 滚动决策同步执行（无异步开销）

**Trade-off**: 
- 同步决策简化设计，但可能阻塞主循环
- 预期影响：每场景 < 100ms（可接受）

### Risk 3: 向后兼容破坏

**风险**: 接口扩展导致现有 IVisionProvider 实现编译失败。

**缓解**:
- IVisionProvider 扩展方法使用默认接口实现（C# 8.0+）
- 现有实现自动继承默认行为
- 测试：现有 8 个基线场景必须继续通过

### Risk 4: 滚动状态不一致

**风险**: TraversalRuntimeContext 的滚动进度与 ScrollableMockVisionService 不同步。

**缓解**:
- 单一数据源：滚动进度始终从 IVisionProvider 获取
- TraversalRuntimeContext.UpdateScrollProgress() 在动作执行后立即调用
- 添加验证测试确保状态一致性

## Migration Plan

### 部署步骤

**Phase 1: 接口扩展** (1天)
1. 扩展 IVisionProvider 接口
2. ScrollableMockVisionService 实现新接口
3. StatefulMockVisionService 继承默认实现
4. 单元测试验证接口实现

**Phase 2: Context 扩展** (1天)
1. TraversalRuntimeContext 添加滚动状态字段
2. 实现 UpdateScrollProgress() 方法
3. 集成到 TraversalEngine.Initialize()
4. 单元测试验证状态管理

**Phase 3: FSM 状态扩展** (2天)
1. 添加 ScrollCheck 状态到 TraversalState enum
2. 实现状态转换逻辑
3. 更新状态转换表
4. ScrollFSM 集成测试

**Phase 4: StepOrchestrator 集成** (2天)
1. 添加 ShouldCheckForScroll() 判断
2. 添加 HandleScrollDecision() 方法
3. 添加 ExecuteScrollAction() 执行
4. 集成测试验证决策触发

**Phase 5: ExitCondition 修改** (1天)
1. 添加 AllChildrenVisitedOrScrollEnd 类型
2. 修改退出条件评估逻辑
3. 单元测试验证新行为

**Phase 6: 测试适配** (2天)
1. 更新 HierarchyBaselineTests
2. 更新 LongListBaselineTests
3. 新增 ScrollFSM 集成测试
4. 验证所有 15 个基线场景通过

**Phase 7: 文档更新** (1天)
1. 更新 state-machine.md
2. 更新 traversal.md
3. 更新 decisions/log.md

**总计**: ~10 工作日

### 回滚策略

- 每个 Phase 独立提交，便于回滚
- 接口扩展使用默认实现，回滚时删除新方法即可
- FSM 状态变更可独立回滚（移除 ScrollCheck 状态）
- 测试失败时使用 git revert 恢复到上一个 Phase

### 无迁移影响

- 现有生产代码无影响（纯测试基础设施变更）
- 现有测试无影响（向后兼容设计）
- 无需数据迁移或配置变更

## Open Questions

**无。** 所有设计决策已明确，集成路径清晰。如有未知问题在实现阶段通过 `/opsx:explore` 探讨。
