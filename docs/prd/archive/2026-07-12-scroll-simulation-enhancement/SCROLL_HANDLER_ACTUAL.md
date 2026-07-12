# ScrollHandler 实际落地设计（基于 Python 遍历架构）

> **版本**: 1.0  
> **日期**: 2026-07-12  
> **参考**: Python Phase 2 架构 (docs/refactor/07-phase2-python-architecture-reference.md)

---

## 1. Python 遍历架构回顾

### TraversalFSM 8 状态流程

```
NodeSelect → PreconditionCheck → Execute → ResultVerify → Branch
    ↑                                                      ↓
    └────────────────────────────── FrameComplete ←───┘
```

### 关键观察

**滚动决策发生在 `BRANCH` 状态**：

- `Branch` 负责决定下一步：选择子节点、完成帧、错误处理
- 当所有子节点已访问完成时，需要判断是否继续滚动
- 如果需要滚动 → 回到 `NodeSelect` 访问新元素
- 如果不需要 → 进入 `FrameComplete` 返回父节点

### Python 的 _handle_branch 逻辑

```python
def _handle_branch(self):
    node = context.current_frame.node
    visited = context.visited_children.get(node.node_id, set())
    
    # 检查是否所有子节点已访问
    all_visited = len(visited) >= len(node.children or [])
    
    if all_visited:
        # ===== 这里是滚动的切入点 =====
        # 当前 Python 无滚动逻辑，直接 FRAME_COMPLETE
        return FRAME_COMPLETE
    
    # 还有未访问子节点
    return NODE_SELECT
```

---

## 2. ScrollHandler 的实际职责

### 单一职责原则

ScrollHandler 只回答一个问题：

> 在所有子节点已访问完成后，是否应该继续滚动而不是结束当前帧？

### 不属于 ScrollHandler 的职责

| 职责 | 谁负责 | 原因 |
|------|--------|------|
| 决定何时停止遍历 | CompletionPolicy | 引擎级策略 |
| 选择下一个访问的元素 | NodeSelect | 节点选择逻辑 |
| 记录已访问的元素 | VisitedChildren | FSM 状态管理 |
| 检测和处理弹窗 | PopupHandler | 专门的弹窗处理 |
| 滚动步长自适应 | Phase 2 | 优化功能，Phase 1 不需要 |

---

## 3. ScrollHandler 实际接口

### 输入（精简）

```csharp
public sealed record ScrollContext(
    bool HasScroll,              // PageAnalysis.HasScroll
    bool IsEndOfList,            // PageAnalysis.IsEndOfList
    double CurrentProgress,       // 当前滚动进度
    double MaxThreshold,          // 最大阈值（最大 segment threshold）
    string PageId);              // 当前页面 ID
```

### 输出（精简）

```csharp
public enum ScrollRecommendation
{
    /// <summary>不滚动，结束当前帧</summary>
    NoScroll,
    /// <summary>向下滚动，然后回到 NodeSelect</summary>
    ScrollDown,
    /// <summary>向上滚动（Phase 2）</summary>
    ScrollUp
}

public sealed record ScrollDecision(
    ScrollRecommendation Recommendation,
    double SuggestedStep);       // 建议的滚动步长
```

### 核心方法

```csharp
public sealed class ScrollHandler
{
    /// <summary>
    /// 判断在所有子节点已访问完成后，是否应该继续滚动
    /// </summary>
    public ScrollDecision ShouldScrollAfterChildrenVisited(ScrollContext ctx)
    {
        // 条件 1: HasScroll = true（有滚动数据）
        if (!ctx.HasScroll)
            return NoScroll();

        // 条件 2: IsEndOfList = false（未到底）
        if (ctx.IsEndOfList)
            return NoScroll();

        // 条件 3: CurrentProgress < MaxThreshold（还有距离）
        if (ctx.CurrentProgress >= ctx.MaxThreshold)
            return NoScroll();

        // 满足所有条件 → 建议向下滚动
        double safeStep = CalculateSafeStep(
            ctx.CurrentProgress, 
            ctx.MaxThreshold);

        return new ScrollDecision(
            ScrollRecommendation.ScrollDown, 
            safeStep);
    }

    private static ScrollDecision NoScroll()
        => new ScrollDecision(ScrollRecommendation.NoScroll, 0.0);

    private static double CalculateSafeStep(
        double currentProgress, 
        double maxThreshold,
        double preferredStep = 0.3)
    {
        double remaining = maxThreshold - currentProgress;
        // 步长不超过剩余距离，最小 5%
        return Math.Clamp(preferredStep, 0.05, remaining);
    }
}
```

---

## 4. FSM 集成点（实际代码位置）

### 在 TraversalFSM.HandleBranch() 中

```csharp
private TraversalState HandleBranch()
{
    var node = Context.CurrentFrame?.Node;
    if (node == null) 
        return TraversalState.FrameComplete;

    // 获取已访问的子节点
    var visitedChildren = Context.VisitedChildren.TryGetValue(node.NodeId, out var set) 
        ? set 
        : ImmutableHashSet<string>.Empty;

    bool allChildrenVisited = visitedChildren.Count >= TotalChildrenCount(node);

    if (allChildrenVisited)
    {
        // ===== 滚动检查点 =====
        
        // 获取当前页面分析
        var analysis = Context.VisionProvider?.AnalyzeCurrentPageAsync();
        if (analysis == null || analysis.Result == null)
            return TraversalState.FrameComplete;

        // 构建滚动上下文
        var scrollContext = new ScrollContext(
            HasScroll: analysis.Result.HasScroll,
            IsEndOfList: analysis.Result.IsEndOfList,
            CurrentProgress: GetScrollProgress(Context),
            MaxThreshold: GetMaxThreshold(Context),
            PageId: Context.CurrentPageId);

        // 调用 ScrollHandler
        var scrollHandler = new ScrollHandler();
        var decision = scrollHandler.ShouldScrollAfterChildrenVisited(scrollContext);

        // 根据建议决定下一步
        switch (decision.Recommendation)
        {
            case ScrollRecommendation.ScrollDown:
                // 执行滚动
                var executor = Context.ActionExecutor as ScrollableMockActionExecutor;
                executor?.ScrollDown(decision.SuggestedStep);

                // 滚动后清除"已访问"标记，因为会有新元素出现
                Context.ResetVisitedChildren(node.NodeId);

                // 返回 NodeSelect 访问新元素
                return TraversalState.NodeSelect;

            case ScrollRecommendation.NoScroll:
            default:
                // 不滚动，结束当前帧
                return TraversalState.FrameComplete;
        }
    }

    // 还有子节点未访问，继续选择
    return TraversalState.NodeSelect;
}
```

---

## 5. 防遗漏机制（简化版）

### 机制 1: 累积模式自动防遗漏

**核心思想**: 累积模式（threshold <= progress）天然防止遗漏

```
progress=0.0: Segment0 可见  → 访问 A, B
progress=0.5: Segment0+1 可见 → 访问 C, D（A,B 已访问，跳过）
progress=1.0: Segment0+1+2 可见 → 访问 E（A,B,C,D 已访问，跳过）
```

**保证**: 任何元素的 threshold <= progress 时，都会出现在可见列表中。

### 机制 2: VisitedChildren 追踪

**核心思想**: 记录每个节点的已访问子节点 ID

```csharp
// 在 ITraversalContext 中
private readonly Dictionary<string, ImmutableHashSet<string>> _visitedChildren = new();

// 访问子节点后记录
void MarkChildVisited(string parentNodeId, string childNodeId)
{
    if (!_visitedChildren.ContainsKey(parentNodeId))
        _visitedChildren[parentNodeId] = ImmutableHashSet<string>.Empty;

    _visitedChildren[parentNodeId] = _visitedChildren[parentNodeId].Add(childNodeId);
}

// 滚动后重置
void ResetVisitedChildren(string parentNodeId)
{
    _visitedChildren[parentNodeId] = ImmutableHashSet<string>.Empty;
}
```

**保证**: 滚动后重新开始元素访问，不会跳过新元素。

### 机制 3: 进度 Clamp 防越界

**核心思想**: 滚动步长永远不超过剩余距离

```csharp
double safeStep = Math.Min(preferredStep, maxThreshold - currentProgress);
```

**保证**: 不会"滚过头"而跳过最后的元素。

---

## 6. 边界情况处理

### 情况 1: 接近底部

```
CurrentProgress = 0.9
MaxThreshold = 1.0
PreferredStep = 0.3

SafeStep = Min(0.3, 0.1) = 0.1  // 自动缩小，确保能到达 1.0
```

### 情况 2: 已到底

```
IsEndOfList = true
→ Recommendation = NoScroll
→ 直接进入 FrameComplete
```

### 情况 3: 无滚动数据

```
HasScroll = false（或无 ScrollSegment）
→ Recommendation = NoScroll
→ 直接进入 FrameComplete
```

### 情况 4: 空列表

```
MaxThreshold = 0.0（只有 threshold=0.0 的空片段）
CurrentProgress >= MaxThreshold 始终成立
→ 不进入滚动逻辑
```

---

## 7. 与 PopupHandler 的对比

| Aspect | PopupHandler | ScrollHandler |
|--------|-------------|---------------|
| **触发时机** | ResultVerify 检测到弹窗 | Branch 所有子节点已访问 |
| **中断遍历** | 是（先处理弹窗） | 否（滚动是继续遍历的一部分） |
| **状态保存/恢复** | 需要（StateRestorer） | 不需要（滚动不改变遍历状态） |
| **返回状态** | ResultVerify | NodeSelect |
| **Pipeline 步骤** | 6 步（含 preserve/restore） | 1 步（直接判断） |

**关键差异**: ScrollHandler 比 PopupHandler 简单得多，因为它不中断遍历流程，只是在"已完成当前子节点"和"结束当前帧"之间插入一个判断。

---

## 8. 实施步骤（简化）

### Phase 1: 核心判断（P0）

```csharp
// src/UniClaw.Core/StateMachine/ScrollHandler.cs
public enum ScrollRecommendation { NoScroll, ScrollDown, ScrollUp }
public sealed record ScrollContext(...)
public sealed record ScrollDecision(...)
public sealed class ScrollHandler
{
    public ScrollDecision ShouldScrollAfterChildrenVisited(ScrollContext ctx)
}
```

### Phase 2: FSM 集成（P0）

```csharp
// src/UniClaw.Core/StateMachine/TraversalFSM.cs
// 在 HandleBranch() 中添加滚动检查
```

### Phase 3: 测试验证（P0）

```csharp
// tests/UniClaw.Core.Tests/StateMachine/ScrollHandlerTests.cs
[Fact] public void ShouldScroll_WhenHasScrollAndNotEnd()
[Fact] public void NoScroll_WhenNoScrollData()
[Fact] public void NoScroll_WhenEndOfList()
[Fact] public void NoScroll_WhenProgressAtMax()
[Fact] public void SafeStep_ClampedToRemaining()
```

### Phase 4: 端到端场景（P1）

```csharp
// tests/UniClaw.Core.Tests/Simulation/Scroll/ScrollScenarioTests.cs
[Fact] public void WiFiList_ScrollThroughAllSegments()
[Fact] public void Scroll_ClampAtBottom()
```

---

## 9. 防遗漏验证

### 验证点 1: 累积模式覆盖

**测试**: 滚动遍历 3 段列表，验证所有元素都被访问

```csharp
// Given: 3 segments with [A,B], [C,D], [E]
// When: Scroll from 0.0 → 0.5 → 1.0
// Then: All 5 elements visited
Assert.Equal(5, visitedElementIds.Count);
```

### 验证点 2: 边界不遗漏

**测试**: 接近底部时滚动，不跳过最后元素

```csharp
// Given: CurrentProgress = 0.9, MaxThreshold = 1.0
// When: Scroll with preferred step 0.3
// Then: Actual step = 0.1 (clamped to remaining)
Assert.Equal(1.0, newProgress);  // 精确到达底部
```

### 验证点 3: VisitedChildren 重置

**测试**: 滚动后，新元素可以被访问

```csharp
// Given: Node X has children [A, B] visited
// When: Scroll reveals [C, D]
// Then: C and D can be visited (not skipped)
Assert.Contains(visitedElementIds, "C");
Assert.Contains(visitedElementIds, "D");
```

---

## 10. 关键设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| **职责范围** | 只判断"是否滚动" | 单一职责，避免与 FSM/PopupHandler 职责重叠 |
| **集成点** | HandleBranch（所有子节点已访问后） | 这是唯一的"可能需要滚动"时刻 |
| **状态保存** | 不需要 | 滚动不中断遍历，不像弹窗需要恢复状态 |
| **步长策略** | 简单 clamp | Phase 1 不需要自适应，clamp 足够 |
| **向上滚动** | Phase 2 | 简化 Phase 1，只支持向下滚动 |

---

## 总结

**ScrollHandler 的实际逻辑**：

1. **触发点**: 在 `TraversalFSM.HandleBranch()` 中，当所有子节点已访问完成时
2. **判断逻辑**: `HasScroll && !IsEndOfList && CurrentProgress < MaxThreshold`
3. **执行动作**: 滚动 + 重置 VisitedChildren + 返回 NodeSelect
4. **防遗漏**: 累积模式 + VisitedChildren 追踪 + 进度 Clamp

**比之前的设计简化了**：

- ❌ 移除了 5-step pipeline（太复杂）
- ❌ 移除了 ScrollabilityDetector（直接用 HasScroll/IsEndOfList）
- ❌ 移除了 ScrollClassifier/ScrollDecider（不需要细分）
- ✅ 保留核心判断方法
- ✅ 保留安全步长计算
- ✅ 明确 FSM 集成点

这个设计更符合 Python 的实际架构模式，也更容易实施。
