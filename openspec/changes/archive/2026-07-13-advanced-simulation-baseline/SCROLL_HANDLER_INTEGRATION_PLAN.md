# ScrollHandler 集成计划

> **创建时间**: 2026-07-13
> **状态**: Phase 3 前置依赖
> **影响**: Advanced Simulation Baseline 变更 blocked

## 问题概述

### 现状

当前 TraversalEngine 与 ScrollHandler **完全独立**，没有集成：

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

### 导致的问题

**高级基线测试无法工作**：

| 测试类 | 预期行为 | 实际表现 | 原因 |
|--------|----------|----------|------|
| HierarchyBaselineTests | 访问12页，75+元素 | 仅3页，1个动作 | 无滚动触发，FSM卡住 |
| LongListBaselineTests | 访问30/25/20项 | 仅4/3/1项 (4-13%) | DynamicMatch只看threshold=0.0 |
| ScrollableBaselineTests | 24项，7屏 | ✅ 通过 | 第一屏有控制元素，设计规避了问题 |

### 根本原因

1. **DynamicMatch 的盲区**：
   - 只从 `PageAnalysis.Items` 生成子节点
   - `PageAnalysis` 只包含 `threshold <= current_progress` 的元素
   - `current_progress` 初始为 0.0，只有第一段可见

2. **没有滚动触发机制**：
   - TraversalFSM 没有滚动状态
   - StepOrchestrator 不调用 ScrollHandler
   - AllChildrenVisited 过早触发

3. **状态隔离**：
   - ScrollableMockVisionService 维护独立的 `ScrollState`
   - TraversalRuntimeContext 不知道滚动状态

## 集成方案设计

### 方案 A: FSM 扩展方案（推荐）

**核心思路**: 将滚动状态集成到 TraversalFSM，在适当的状态点触发滚动决策。

#### 架构变更

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

#### 关键组件

**1. ScrollFSM 集成点**

```csharp
// TraversalRuntimeContext 扩展
public class TraversalRuntimeContext : ITraversalContext
{
    // 现有字段...
    
    // 🆕 滚动相关状态
    public double CurrentScrollProgress { get; private set; }
    public bool HasScrollableContent => _scrollHandler != null;
    public bool IsAtScrollEnd => /* ... */;
    
    public void UpdateScrollProgress(double progress) { /* ... */}
}
```

**2. StepOrchestrator 扩展**

```csharp
public class StepOrchestrator
{
    private ScrollHandler? _scrollHandler; // 🆕 滚动处理器
    
    public StepResult ExecuteStep(StepContext ctx)
    {
        // 现有逻辑...
        
        // 🆕 滚动检查点
        if (ShouldCheckForScroll(ctx))
        {
            return HandleScrollDecision(ctx);
        }
        
        return _existingLogic;
    }
    
    private StepResult HandleScrollDecision(StepContext ctx)
    {
        var hasScroll = ctx.Context.VisionProvider.HasScroll(); // IVisionProvider 扩展
        var isEnd = ctx.Context.IsAtScrollEnd;
        
        if (!hasScroll || isEnd)
        {
            return StepResult.Completed(ctx.CurrentFrame); // 没有更多内容
        }
        
        // 委托 ScrollHandler 决策
        var action = _scrollHandler.DecideScroll(ctx.Context);
        return ExecuteScrollAction(ctx, action);
    }
}
```

**3. IVisionProvider 扩展**

```csharp
public interface IVisionProvider
{
    // 现有方法...
    
    // 🆕 滚动感知接口
    bool HasScroll();
    double GetScrollProgress();
    bool IsEndOfList();
}
```

**4. ExitCondition 修改**

```csharp
// 现有: AllChildrenVisited 在所有可见子节点访问后触发
// 问题: 不考虑滚动内容

// 🆕 新增退出条件类型
public enum ExitConditionType
{
    // 现有...
    AllChildrenVisited,
    
    // 🆕 新增
    AllChildrenVisitedOrScrollEnd  // 子节点访问完 OR 到达滚动末尾
}
```

#### 实现步骤

1. **IVisionProvider 扩展** (1天)
   - 添加 `HasScroll()`, `GetScrollProgress()`, `IsEndOfList()`
   - ScrollableMockVisionService 实现
   - StatefulMockVisionService 默认实现 (返回 false/0.0/true)

2. **TraversalRuntimeContext 扩展** (1天)
   - 添加滚动状态字段
   - 添加 `UpdateScrollProgress()` 方法
   - 集成 ScrollHandler 初始化

3. **ScrollFSM 状态扩展** (2天)
   - 添加 `ScrollCheck` 状态
   - 实现状态转换逻辑
   - 更新状态转换表

4. **StepOrchestrator 集成** (2天)
   - 添加 `ShouldCheckForScroll()` 判断
   - 添加 `HandleScrollDecision()` 方法
   - 添加 `ExecuteScrollAction()` 执行

5. **ExitCondition 修改** (1天)
   - 添加 `AllChildrenVisitedOrScrollEnd` 类型
   - 修改退出条件评估逻辑

6. **测试适配** (2天)
   - 更新 HierarchyBaselineTests
   - 更新 LongListBaselineTests
   - 验证所有15个基线场景通过

**总计**: ~9 工作日

### 方案 B: Container 扩展方案（备选）

**核心思路**: 将滚动逻辑封装在 Container Handler 内部，对上层透明。

```csharp
public sealed class ContainerHandler
{
    // 现有逻辑...
    
    // 🆕 滚动感知的容器处理
    private HandlerResult HandleWithScrollSupport(StepContext ctx)
    {
        // 1. 处理当前可见子节点
        var result = HandleChildren(ctx);
        
        // 2. 检查是否有滚动内容
        if (ctx.Context.HasScrollableContent && !ctx.Context.IsAtScrollEnd)
        {
            // 3. 触发滚动，重新处理
            ExecuteScroll(ctx);
            return HandleChildren(ctx); // 递归处理新可见的子节点
        }
        
        return result;
    }
}
```

**优点**: 
- 改动范围较小
- 对现有 FSM 影响小

**缺点**:
- 滚动逻辑与容器处理耦合
- 难以支持复杂的滚动策略（跳跃恢复、自适应步长）
- 测试场景有限

**推荐**: 方案 A（FSM 扩展）更符合架构设计原则

## 设计文档更新

### 需要更新的文档

1. **docs/system/layers/state-machine.md**
   - §3.2: 添加 ScrollCheck 状态
   - §3.3: 更新状态转换表
   - §4: 添加 ScrollHandler 集成说明

2. **docs/system/layers/traversal.md**
   - §2.2: StepOrchestrator 添加滚动决策逻辑
   - §3: ExitCondition 添加新类型

3. **docs/system/charter-specification.md**
   - §5.6: 添加 ScrollHandler 集成决策点

### 需要添加的决策

**docs/system/decisions/log.md**
- **D-19**: ScrollHandler Integration into TraversalFSM
  - 背景: 高级基线测试需要滚动感知
  - 决策: 采用 FSM 扩展方案
  - 影响: TraversalFSM, StepOrchestrator, IVisionProvider

## 相关变更

### Blocked 变更

- **advanced-simulation-baseline**: 完全依赖 ScrollHandler 集成

### 相关组件

- `src/UniClaw.Core/StateMachine/Scroll/ScrollHandler.cs` (已存在)
- `src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs` (需要扩展)
- `src/UniClaw.Core/Traversal/TraversalEngine.cs` (需要扩展)
- `src/UniClaw.Core/Traversal/StepOrchestrator.cs` (需要扩展)

## 测试策略

### 集成测试

```csharp
// ScrollFSM 集成测试
public class ScrollFSMIntegrationTests
{
    [Fact]
    public void ScrollCheck_State_Transition_Correct()
    {
        // 验证 ScrollCheck 状态转换
    }
    
    [Fact]
    public void Scroll_Decision_Executes_On_Exhausted_Visible_Children()
    {
        // 验证子节点耗尽时触发滚动决策
    }
}

// 端到端测试
public class ScrollAwareTraversalTests
{
    [Fact]
    public void LongList_Complete_Traversal_Visits_All_Items()
    {
        // 验证30项列表完整遍历
    }
    
    [Fact]
    public void Hierarchy_Four_Level_Traversal_Visits_All_Pages()
    {
        // 验证4层级完整遍历
    }
}
```

### 回归测试

- 现有 8 个基线场景必须继续通过
- ScrollableBaselineTests (6场景) 不受影响
- SimulationBaselineTests (2场景) 不受影响

## 里程碑

| 里程碑 | 交付物 | 状态 |
|--------|--------|------|
| M1: IVisionProvider 扩展 | HasScroll(), GetScrollProgress(), IsEndOfList() | ⏳ Pending |
| M2: TraversalRuntimeContext 扩展 | 滚动状态字段, UpdateScrollProgress() | ⏳ Pending |
| M3: ScrollFSM 状态扩展 | ScrollCheck 状态, 状态转换表 | ⏳ Pending |
| M4: StepOrchestrator 集成 | ShouldCheckForScroll(), HandleScrollDecision() | ⏳ Pending |
| M5: ExitCondition 修改 | AllChildrenVisitedOrScrollEnd | ⏳ Pending |
| M6: 测试适配 | HierarchyBaselineTests, LongListBaselineTests 通过 | ⏳ Pending |
| M7: 文档更新 | state-machine.md, traversal.md, decisions/log.md | ⏳ Pending |

## 参考

- 设计文档: `docs/system/layers/state-machine.md`
- 现有 ScrollHandler: `src/UniClaw.Core/StateMachine/Scroll/ScrollHandler.cs`
- Blocked 变更: `openspec/changes/advanced-simulation-baseline/`
- 基线测试: `tests/UniClaw.Core.Tests/Baseline/`
