# ScrollHandler 实际逻辑与防遗漏机制

> **版本**: 1.0
> **日期**: 2026-07-12
> **用途**: ScrollHandler 实际落地设计

---

## 1. 实际遍历流程中的滚动决策

### 当前 C# TraversalFSM 流程

```
NodeSelect → PreconditionCheck → Execute → ResultVerify → Branch
     ↑                                                      ↓
     └─────────────────────────── FrameComplete ←──────────┘
```

### 滚动应该在何时发生？

**关键观察**: 滚动是发生在 **Branch 状态**，当所有子节点已访问完成但页面还有更多内容时。

```
Branch 状态:
  ├─ STATIC 子节点全部访问 → FrameComplete (可能需要滚动)
  ├─ DYNAMIC_MATCH 子节点全部访问 → FrameComplete (可能需要滚动)
  └─ 其他情况 → 继续

在 FrameComplete 之前检查:
  IF (当前页面可滚动) AND (HasScroll) AND (!IsEndOfList) THEN
    执行滚动 → NodeSelect (访问新出现的元素)
  ELSE
    FrameComplete → 返回父节点
```

---

## 2. ScrollHandler 的实际职责

### 核心职责（单一职责原则）

ScrollHandler **只负责一个判断**：

```
是否应该继续滚动？
```

它不负责：
- ❌ 决定何时停止遍历（这是 FSM 的事）
- ❌ 决定如何访问元素（这是 NodeSelect 的事）
- ❌ 记录哪些元素被访问过（这是 VisitedChildren 的事）

### 输入输出

```
输入:
  - PageAnalysis (包含 HasScroll, IsEndOfList, Items)
  - 当前已访问的元素 ID 集合
  - 当前滚动进度

输出:
  - ScrollDecision (ScrollDown/ScrollUp/None)
  - NewProgress (滚动后的进度)
  - NewElements (新出现的元素)
```

---

## 3. 防遗漏的核心机制

### 问题：什么是"遗漏"？

**遗漏**的定义：
> 滚动后，某个元素应该在可见范围内，但没有被遍历引擎访问到。

### 遗漏的三种场景

| 场景 | 描述 | 防止机制 |
|------|------|---------|
| **跳跃遗漏** | 滚动步长过大，跳过了中间片段 | Overlap Detection |
| **重复遗漏** | 元素在多片段重复，导致 ID 冲突 | Deduplication by ID |
| **边界遗漏** | 接近底部时滚动，没注意到最后元素 | End-of-List Clamp |

### 机制 1: Overlap Detection（重叠检测）

**原理**: 滚动前后必须有至少一个共同元素，确保没有跳跃。

```csharp
/// <summary>检测滚动前后元素重叠情况</summary>
public enum OverlapStatus
{
    /// <summary>有重叠，安全</summary>
    HasOverlap,
    /// <summary>无重叠但都有元素 → 可能跳跃</summary>
    NoOverlap_BothHaveElements,
    /// <summary>滚动前无元素（初始状态）</summary>
    NoOverlap_BeforeEmpty,
    /// <summary>滚动后无元素（可能到底）</summary>
    NoOverlap_AfterEmpty,
    /// <summary>都无元素（空列表）</summary>
    BothEmpty
}

public OverlapStatus DetectOverlap(
    ImmutableArray<string> beforeElements,
    ImmutableArray<string> afterElements)
{
    if (beforeElements.IsEmpty && afterElements.IsEmpty)
        return OverlapStatus.BothEmpty;

    if (beforeElements.IsEmpty)
        return OverlapStatus.NoOverlap_BeforeEmpty;

    if (afterElements.IsEmpty)
        return OverlapStatus.NoOverlap_AfterEmpty;

    bool hasOverlap = beforeElements.Any(id => afterElements.Contains(id));
    return hasOverlap
        ? OverlapStatus.HasOverlap
        : OverlapStatus.NoOverlap_BothHaveElements;
}
```

### 机制 2: Progress Clamp（进度钳制）

**原理**: 确保滚动进度不超过最大阈值，防止"越界"遗漏。

```csharp
/// <summary>计算安全的滚动步长（不超过剩余距离）</summary>
public double CalculateSafeStep(
    double currentProgress,
    double maxThreshold,
    double preferredStep = 0.3)
{
    double remainingDistance = maxThreshold - currentProgress;

    if (remainingDistance <= 0)
        return 0.0;  // 已到底

    // 步长不超过剩余距离
    return Math.Min(preferredStep, remainingDistance);
}
```

### 机制 3: Element Deduplication（元素去重）

**原理**: 累积模式中，低 threshold 的元素优先，避免重复访问。

```csharp
/// <summary>收集可见元素，自动去重（低 threshold 优先）</summary>
public ImmutableArray<PageElement> CollectVisibleElementsDeduplicated(
    ImmutableArray<ScrollSegment> segments,
    double progress)
{
    var elementMap = new Dictionary<string, (PageElement element, double threshold)>();

    foreach (var segment in segments.OrderBy(s => s.Threshold))
    {
        if (segment.Threshold <= progress)
        {
            foreach (var element in segment.Elements)
            {
                // 去重：只保留第一次出现的（低 threshold）
                if (!elementMap.ContainsKey(element.Id))
                {
                    elementMap[element.Id] = (element, segment.Threshold);
                }
            }
        }
    }

    return elementMap.Values.Select kvp => kvp.element).ToImmutableArray();
}
```

### 机制 4: Visited Tracking（访问追踪）

**原理**: 在 Branch 状态中，记录已访问的元素，避免重复访问。

```csharp
// 在 TraversalFSM.Branch 中维护
private readonly HashSet<string> _visitedElementIds = new();

// 访问元素后记录
_visitedElementIds.Add(element.Id);

// 滚动后，过滤掉已访问的元素
var newElements = afterElements.Where(e => !_visitedElementIds.Contains(e.Id));
```

---

## 4. ScrollHandler 实际逻辑

### 简化后的职责

ScrollHandler 实际只需要回答两个问题：

1. **Q1**: 当前页面是否可滚动？
   - 检查：`HasScrollData && !IsEndOfList`

2. **Q2**: 如果可滚动，应该滚多少？
   - 计算：`SafeStep = Min(PreferredStep, MaxProgress - CurrentProgress)`

### 核心方法

```csharp
/// <summary>
/// 判断是否应该继续滚动
/// </summary>
public bool ShouldContinueScroll(
    bool hasScroll,
    bool isEndOfList,
    double currentProgress,
    double maxThreshold)
{
    // 条件 1: HasScroll = true（有更多内容）
    if (!hasScroll)
        return false;

    // 条件 2: IsEndOfList = false（未到底）
    if (isEndOfList)
        return false;

    // 条件 3: CurrentProgress < MaxThreshold（还有距离）
    return currentProgress < maxThreshold;
}

/// <summary>
/// 计算安全的滚动步长
/// </summary>
public double CalculateSafeScrollStep(
    double currentProgress,
    double maxThreshold,
    double preferredStep = 0.3)
{
    double remaining = maxThreshold - currentProgress;

    // 不超过剩余距离，最小 5%
    return Math.Clamp(preferredStep, 0.05, remaining);
}
```

### 滚动决策输出

```csharp
public sealed record ScrollDecision(
    bool ShouldScroll,         // 是否应该滚动
    ScrollDirection Direction, // 滚动方向（Down/Up）
    double StepPercent,        // 滚动步长
    double ExpectedProgress);  // 预期滚动后的进度

public enum ScrollDirection
{
    Down,  // 向下滚动（增加进度）
    Up     // 向上滚动（减少进度）
}
```

---

## 5. FSM 集成点

### 在 TraversalFSM 中的调用

```csharp
// TraversalFSM.HandleBranch() 中
private TraversalState HandleBranch()
{
    var context = Context;
    var currentFrame = context.CurrentFrame;
    
    // 检查是否所有子节点已访问
    if (AllChildrenVisited(currentFrame))
    {
        // ===== 滚动检查点 =====
        var analysis = context.VisionProvider.AnalyzeCurrentPageAsync();
        var scrollHandler = new ScrollHandler();
        
        if (scrollHandler.ShouldContinueScroll(
                analysis.Result.HasScroll,
                analysis.Result.IsEndOfList,
                GetScrollProgress(context),
                GetMaxThreshold(context)))
        {
            // 执行滚动
            var step = scrollHandler.CalculateSafeScrollStep(...);
            context.ActionExecutor.ScrollDown(step);
            
            // 清除"所有子节点已访问"标记，因为会有新元素出现
            currentFrame.ResetVisitedChildren();
            
            // 返回 NodeSelect 访问新元素
            return TraversalState.NodeSelect;
        }
        
        // ===== 滚动检查结束 =====
        
        // 不可滚动或已到底，进入 FrameComplete
        return TraversalState.FrameComplete;
    }
    
    // 还有子节点未访问，继续
    return TraversalState.NodeSelect;
}
```

---

## 6. 防遗漏检查清单

### Checkpoint 1: 滚动前

- [ ] `HasScroll` = true？（有滚动数据）
- [ ] `IsEndOfList` = false？（未到底）
- [ ] `CurrentProgress < MaxThreshold`？（还有距离）

### Checkpoint 2: 滚动中

- [ ] `StepPercent <= (MaxThreshold - CurrentProgress)`？（步长不超过剩余）
- [ ] `StepPercent >= 0.05`？（最小步长 5%）

### Checkpoint 3: 滚动后

- [ ] `NewProgress = Clamp(OldProgress + Step, 0.0, MaxThreshold)`？
- [ ] 滚动后元素与滚动前有重叠？（跳跃检测）
- [ ] 新元素中排除已访问的 ID？（去重）

### Checkpoint 4: FrameComplete 前最终检查

- [ ] `IsEndOfList` = true？（确认到底）
- [ ] `HasScroll` = false？（或 `CurrentProgress >= MaxThreshold`）

---

## 7. 边界情况处理

### 情况 1: 接近底部时的滚动

```
CurrentProgress = 0.9
MaxThreshold = 1.0
PreferredStep = 0.3

SafeStep = Min(0.3, 1.0 - 0.9) = 0.1  // 自动缩小步长
```

### 情况 2: 空列表

```
HasScrollData = false
→ HasScroll = false, 不进入滚动逻辑
```

### 情况 3: 单屏列表

```
MaxThreshold = 0.0（只有一个片段）
→ CurrentProgress >= MaxThreshold 始终成立
→ IsEndOfList = true，不滚动
```

### 情况 4: 重复元素

```
Segment0: [A, B]
Segment1: [A, C]  // A 重复

累积模式去重后: [A, B, C]
A 只访问一次（来自 Segment0）
```

---

## 8. 与 Python 对齐

### Python 的滚动决策逻辑

```python
# Python V7.0 伪代码
def should_scroll(page_analysis, scroll_state):
    # HasScroll 且未到底
    if page_analysis.has_scroll and not page_analysis.is_end_of_list:
        # 计算安全步长
        remaining = 1.0 - scroll_state.current_progress
        step = min(0.3, remaining)
        return ScrollDecision(should=True, step=step)
    return ScrollDecision(should=False)
```

### C# 的对齐实现

完全一致，只是类型系统和命名约定的差异：

| Python | C# |
|--------|-----|
| `has_scroll` | `HasScroll` (bool) |
| `is_end_of_list` | `IsEndOfList` (bool) |
| `current_progress` | `CurrentProgress` (double) |
| `min(step, remaining)` | `Math.Min(step, remaining)` |

---

## 9. 测试验证点

### 验证 1: 累积模式不遗漏

```csharp
[Fact]
public void Scroll_AccumulationMode_NoElementsMissed()
{
    // 3 个片段，每片段 2 个元素
    var segments = new[] {
        new ScrollSegment(0.0, new[] { A, B }),
        new ScrollSegment(0.5, new[] { C, D }),
        new ScrollSegment(1.0, new[] { E })
    };
    
    // 滚动 3 次覆盖所有片段
    var visited = new HashSet<string>();
    foreach (var progress in new[] { 0.0, 0.5, 1.0 })
    {
        var elements = GetVisibleElements(segments, progress);
        foreach (var e in elements) visited.Add(e.Id);
    }
    
    // 验证：所有 5 个元素都被访问
    Assert.Equal(5, visited.Count);
    Assert.Contains("A", visited);
    Assert.Contains("B", visited);
    Assert.Contains("C", visited);
    Assert.Contains("D", visited);
    Assert.Contains("E", visited);
}
```

### 验证 2: 步长钳制不越界

```csharp
[Fact]
public void Scroll_SafeStep_NoOverflow()
{
    double current = 0.9;
    double max = 1.0;
    
    double step = CalculateSafeScrollStep(current, max, 0.3);
    
    // 验证：步长不超过剩余距离
    Assert.True(step <= (max - current));
    Assert.Equal(0.1, step);
}
```

### 验证 3: 去重不重复访问

```csharp
[Fact]
public void Scroll_Deduplication_NoDuplicateVisits()
{
    var segments = new[] {
        new ScrollSegment(0.0, new[] { wifi_switch }),
        new ScrollSegment(0.5, new[] { wifi_switch })  // 重复
    };
    
    var elements = GetVisibleElementsDeduplicated(segments, 0.5);
    
    // 验证：wifi_switch 只出现一次
    Assert.Single(elements);
    Assert.Equal("wifi_switch", elements[0].Id);
}
```

---

## 10. 实施优先级

### P0 (必须)

1. **ShouldContinueScroll** - 核心判断逻辑
2. **CalculateSafeScrollStep** - 步长钳制
3. **CollectVisibleElementsDeduplicated** - 累积模式 + 去重

### P1 (重要)

4. **DetectOverlap** - 跳跃检测（用于测试验证）
5. **FSM 集成** - 在 HandleBranch 中调用

### P2 (可选)

6. **ScrollUp 支持** - 向上滚动（Phase 2）
7. **自适应步长** - 根据元素变化调整（Phase 2）

---

**总结**: ScrollHandler 的核心是**单一判断**（是否继续滚动）和**安全步长**（不越界）。防遗漏通过 4 重机制保证：重叠检测、进度钳制、元素去重、访问追踪。
