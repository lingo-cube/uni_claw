# Scroll Simulation Enhancement — 完整设计文档

> **Version**: 2.0  
> **Date**: 2026-07-12  
> **Status**: Design Complete  
> **Python 对齐**: PRD_V7_0_SimScroll.md

---

## 目录

1. [概述](#概述)
2. [背景与问题](#背景与问题)
3. [目标与范围](#目标与范围)
4. [核心概念](#核心概念)
5. [数据模型](#数据模型)
6. [ScrollHandler 7-step Pipeline](#scrollhandler-7-step-pipeline)
7. [自适应步长策略](#自适应步长策略)
8. [跳跃检测与恢复](#跳跃检测与恢复)
9. [滚动模拟场景](#滚动模拟场景)
10. [架构设计](#架构设计)
11. [实施计划](#实施计划)
12. [测试策略](#测试策略)

---

## 概述

### 变更简介

本次变更将为 C# UniClaw.Core 项目添加完整的滚动模拟能力，包括：

- ✅ 滚动数据模型（ScrollSegment、ScrollState、ScrollDataStore）
- ✅ 累积模式元素可见性
- ✅ 元素去重机制
- ✅ ScrollHandler 7-step pipeline（含跳跃检测和恢复）
- ✅ 可配置滚动步长策略
- ✅ 自适应步长算法
- ✅ 完整的测试场景覆盖

### 设计原则

1. 使用 C# 风格的 StateFixtureBuilder 扩展（不使用 JSON 格式）
2. 滚动场景单独测试，按类别存放（tests/Simulation/Scroll/）
3. 累积模式：threshold <= progress 的元素都可见
4. 跳跃检测作为核心链路，而非测试验证
5. 所有步长参数可配置，支持自适应调整

---

## 背景与问题

### 当前状态

C# 仿真基础设施当前存在以下限制：

| 限制 | 描述 | 影响 |
|------|------|------|
| **无滚动支持** | `StatefulMockVisionService` 返回固定元素集合 | 无法测试滚动列表场景 |
| **状态静态化** | `IsEndOfList` 来自 `PageState.IsComplete`（静态值） | 无法动态检测列表到底 |
| **无滚动状态跟踪** | 没有滚动进度、次数、历史记录 | 无法验证滚动逻辑 |
| **缺少滚动决策机制** | 无法集成到遍历流程 | 遍历引擎不支持滚动 |

### 问题案例

**场景**：测试 WiFi 列表遍历

现有 C# 实现：
```csharp
// StatefulMockVisionService.BuildPageAnalysis
return new PageAnalysis(
    IsEndOfList: page.IsComplete  // 静态值，无法随滚动变化
);
```

期望行为：
```csharp
// ScrollableMockVisionService 应该支持：
// 1. 根据 scroll_progress 返回不同元素集合
// 2. 动态计算 IsEndOfList（基于当前进度 vs 最大阈值）
// 3. 动态计算 HasScroll（是否还有未到达的片段）
```

### Python 对齐目标

- Python V7.0 `src/simulation/scroll/` 模块
- ScrollSegment/ScrollState 数据模型一致
- 累积模式元素可见性逻辑一致
- **增强**：跳跃检测（Python 无，C# 新增）
- **增强**：自适应步长（Python 无，C# 新增）
- **增强**：可配置策略（Python 有限，C# 完整）

---

## 目标与范围

### 核心目标

1. **支持滚动列表模拟** - 根据滚动进度返回不同元素集合
2. **滚动状态管理** - 跟踪每个页面的进度、次数、历史
3. **动态状态计算** - 自动计算 `HasScroll` 和 `IsEndOfList`
4. **元素去重机制** - 确保同一 ID 元素只返回一个
5. **跳跃检测与恢复** - 核心链路防止遗漏元素
6. **自适应步长** - 根据重复元素比例优化滚动效率
7. **向后兼容** - 不影响现有非滚动测试

### 范围界定

| 包含 (Phase 1) | 不包含 (Phase 2) |
|----------------|------------------|
| ✅ 垂直滚动支持 | ❌ 水平滚动 |
| ✅ 单容器滚动 | ❌ 嵌套滚动 |
| ✅ 累积模式元素可见性 | ❌ 故障注入（延迟、无响应） |
| ✅ 元素去重 | ❌ 步长自适应（已包含） |
| ✅ HasScroll/IsEndOfList 计算 | ❌ 向上滚动 |
| ✅ ScrollHandler (7-step) | ❌ 滚动决策在 TraversalEngine 主流程 |

### 成功标准

- ✅ 仿真测试能完整遍历 3 屏列表（9个元素）
- ✅ 滚动到底检测正确（`IsEndOfList`）
- ✅ `HasScroll` 计算正确（是否还有更多内容）
- ✅ 元素去重生效（同一 ID 只返回一个）
- ✅ 现有测试无需修改即可运行
- ✅ 跳跃检测正确识别并恢复
- ✅ 自适应步长在重复元素过多时增大步长

---

## 核心概念

### Scroll Progress（滚动进度）

归一化的 0.0-1.0 值，表示当前滚动位置：
- `0.0` = 列表顶部
- `1.0` = 列表底部
- 进度通过滚动操作累积增加/减少

### Scroll Segment（滚动片段）

按阈值分段的元素集合：

```csharp
public sealed record ScrollSegment(
    double Threshold,           // 激活阈值 (0.0-1.0)
    ImmutableArray<PageElement> Elements  // 该片段的元素
);
```

### Accumulation Mode（累积模式）

**核心规则**：所有 `Threshold <= CurrentProgress` 的片段元素都可见。

```
CurrentProgress = 0.5:
  Segment0 (Threshold=0.0) → 可见 (0.0 <= 0.5) ✓
  Segment1 (Threshold=0.5) → 可见 (0.5 <= 0.5) ✓
  Segment2 (Threshold=1.0) → 不可见 (1.0 > 0.5) ✗
```

### Element Deduplication（元素去重）

当同一元素 ID 在多个片段中出现时，只返回一个（低 threshold 优先）：

```
Segment0 (threshold=0.0): wifi_switch
Segment1 (threshold=0.5): wifi_switch (重复)
结果: 只返回 Segment0 的 wifi_switch
```

---

## 数据模型

### 核心数据结构

```csharp
namespace UniClaw.Core.Simulation.Scroll;

/// <summary>滚动片段：按阈值分段的元素集合</summary>
public sealed record ScrollSegment(
    double Threshold,
    ImmutableArray<PageElement> Elements
);

/// <summary>滚动状态：单个页面的滚动进度和操作历史</summary>
public sealed record ScrollState(
    double CurrentProgress,        // 0.0-1.0
    int ScrollCount,              // 操作次数
    ImmutableArray<double> ScrollHistory  // 历史记录
);

/// <summary>滚动动作记录</summary>
public sealed record ScrollAction(
    string Action,                // "SCROLL_DOWN" / "SCROLL_UP"
    double StepPercent,           // 步长（如 0.3 = 30%）
    double BeforeProgress,        // 滚动前进度
    double AfterProgress,         // 滚动后进度
    DateTimeOffset Timestamp
);

/// <summary>滚动数据存储：管理 ScrollSegment 数据</summary>
public sealed class ScrollDataStore
{
    private readonly Dictionary<string, ImmutableArray<ScrollSegment>> _segments = new();

    public void AddPage(string pageId, ImmutableArray<ScrollSegment> segments)
    {
        _segments[pageId] = segments;
    }

    public ImmutableArray<ScrollSegment> GetScrollSegments(string pageId)
    {
        return _segments.TryGetValue(pageId, out var segments)
            ? segments
            : ImmutableArray<ScrollSegment>.Empty;
    }

    public bool HasScrollData(string pageId) => _segments.ContainsKey(pageId);
}
```

### 滚动验证相关类型（v2.0 新增）

```csharp
/// <summary>滚动前后元素重叠状态</summary>
public enum OverlapStatus
{
    HasOverlap,                    // 有重叠，安全
    NoOverlap_BothHaveElements,   // 无重叠但都有元素 → 发生跳跃
    NoOverlap_BeforeEmpty,        // 滚动前无元素（初始状态）
    NoOverlap_AfterEmpty,         // 滚动后无元素（可能到底）
    BothEmpty                     // 都无元素（空列表）
}

/// <summary>滚动验证结果</summary>
public sealed record ScrollVerifyResult(
    OverlapStatus OverlapStatus,
    ImmutableArray<string> BeforeElementIds,
    ImmutableArray<string> AfterElementIds,
    int OverlapCount,              // 重叠元素数量
    int NewElementCount,           // 新出现元素数量
    int DuplicateElementCount);    // 重复元素数量（用于自适应步长）

/// <summary>跳跃恢复结果</summary>
public sealed record JumpRecoveryResult(
    bool Success,              // 恢复是否成功
    int RetryCount,            // 实际重试次数
    double FinalStep,          // 最终使用的步长
    double FinalProgress,      // 恢复后的进度
    string Reason);            // 原因说明
```

### 配置类型（v2.0 新增）

```csharp
/// <summary>滚动处理器配置 - 所有参数可配置</summary>
public sealed record ScrollHandlerConfig(
    // ===== 基础步长配置 =====
    double DefaultScrollStep = 0.3,      // 默认滚动步长 30%
    double MinScrollStep = 0.01,         // 最小滚动步长 1%
    double MaxScrollStep = 0.5,          // 最大滚动步长 50%

    // ===== 跳跃恢复配置 =====
    int MaxJumpRetryCount = 3,           // 最大跳跃重试次数
    double JumpRecoveryFactor = 0.5,     // 跳跃恢复步长缩减因子

    // ===== 自适应步长配置 =====
    bool EnableAdaptiveStep = true,      // 是否启用自适应步长
    double DuplicateRatioThreshold = 0.7, // 重复元素比例阈值（70%）
    double AdaptiveStepIncrease = 1.5,   // 自适应步长增长因子（50%）
    int MinSampleSize = 3,               // 自适应最小样本数量

    // ===== 边界配置 =====
    double ProgressEpsilon = 0.001);     // 进度比较精度
```

---

## ScrollHandler 7-step Pipeline

### Pipeline 概览

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ScrollHandler 7-step Pipeline                              │
└─────────────────────────────────────────────────────────────────────────────┘

  detect → classify → decide → execute → verify → recover → statistics
    ↓         ↓         ↓        ↓        ↓         ↓         ↓
  可滚动?  什么类型?  滚动?    执行    跳跃?   恢复?    统计
```

### Step 1: Detect（可滚动性检测）

```csharp
/// <summary>页面可滚动性检测结果</summary>
public enum Scrollability
{
    NotScrollable,    // 非滚动页面（无 ScrollSegment 数据）
    CanScrollDown,    // 可滚动且未到底（HasScroll && !IsEndOfList）
    AtBottom,         // 可滚动但已到底（HasScroll && IsEndOfList）
    CanScrollUp       // 可滚动且在顶部（可向上滚动）
}

/// <summary>可滚动性检测器 - 纯函数，无副作用</summary>
public sealed class ScrollabilityDetector
{
    public Scrollability Detect(
        string pageId,
        bool hasScroll,
        bool isEndOfList,
        double currentProgress,
        ScrollDataStore scrollDataStore)
    {
        // 优先级 1: 无滚动数据
        if (!scrollDataStore.HasScrollData(pageId))
            return Scrollability.NotScrollable;

        // 优先级 2: 已到底
        if (isEndOfList)
            return Scrollability.AtBottom;

        // 优先级 3: 有内容可向下滚动
        if (hasScroll)
            return Scrollability.CanScrollDown;

        // 优先级 4: 在顶部可向上滚动
        if (currentProgress > 0.0)
            return Scrollability.CanScrollUp;

        return Scrollability.NotScrollable;
    }
}
```

### Step 2: Classify（滚动分类）

```csharp
/// <summary>滚动决策结果 - 4 字段分类</summary>
public sealed record ScrollDecision(
    Scrollability Scrollability,        // 检测结果
    double CurrentProgress,              // 当前进度
    double MaxProgress,                  // 最大进度（最大 threshold）
    double RecommendedStep);             // 推荐步长

/// <summary>滚动分类器 - 4-submethod 顺序执行</summary>
public sealed class ScrollClassifier
{
    public ScrollDecision Classify(
        Scrollability scrollability,
        PageAnalysis analysis,
        double currentProgress,
        ImmutableArray<ScrollSegment> segments)
    {
        double progress = currentProgress;
        double maxProgress = segments.IsEmpty
            ? 1.0
            : segments.Max(s => s.Threshold);

        double recommendedStep = DetermineRecommendedStep(
            scrollability, progress, maxProgress);

        return new ScrollDecision(
            scrollability, progress, maxProgress, recommendedStep);
    }

    private static double DetermineRecommendedStep(
        Scrollability scrollability,
        double currentProgress,
        double maxProgress)
    {
        double defaultStep = _scrollHandlerConfig.DefaultScrollStep;

        return scrollability switch
        {
            Scrollability.CanScrollDown => Math.Min(
                defaultStep,
                maxProgress - currentProgress),
            Scrollability.CanScrollUp => Math.Min(
                defaultStep,
                currentProgress),
            _ => 0.0
        };
    }
}
```

### Step 3: Decide（滚动决策）

```csharp
/// <summary>滚动动作类型</summary>
public enum ScrollActionType
{
    None,       // 不执行滚动
    ScrollDown, // 向下滚动
    ScrollUp    // 向上滚动
}

/// <summary>滚动决策器 - 将 ScrollDecision 映射到 ScrollActionType</summary>
public sealed class ScrollDecider
{
    public ScrollActionType Decide(ScrollDecision decision)
    {
        return decision.Scrollability switch
        {
            Scrollability.CanScrollDown => ScrollActionType.ScrollDown,
            Scrollability.CanScrollUp => ScrollActionType.ScrollUp,
            Scrollability.AtBottom => ScrollActionType.None,
            Scrollability.NotScrollable => ScrollActionType.None,
            _ => ScrollActionType.None
        };
    }
}
```

### Step 4: Execute（滚动执行）

```csharp
/// <summary>滚动执行上下文</summary>
public sealed record ScrollContext(
    ScrollDecision Decision,
    ScrollActionType ActionType,
    double StepPercent,
    ITraversalContext TraversalContext);

/// <summary>滚动执行结果</summary>
public sealed record ScrollActionResult(
    ScrollActionType Action,
    bool Success,
    double NewProgress,
    string Description);

/// <summary>滚动动作执行器 - Hook Dispatch 表 + 异常兜底</summary>
public sealed class ScrollActionExecutor
{
    private readonly Dictionary<ScrollActionType, Func<ScrollContext, ScrollActionResult>> _dispatchTable;

    public ScrollActionResult Execute(ScrollActionType actionType, ScrollContext ctx)
    {
        try
        {
            if (_dispatchTable.TryGetValue(actionType, out var hook))
                return hook(ctx);
            return DefaultNone(ctx);
        }
        catch (Exception ex)
        {
            return new ScrollActionResult(
                actionType, false, ctx.Decision.CurrentProgress,
                $"Exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ScrollActionResult DefaultScrollDown(ScrollContext ctx)
    {
        var executor = ctx.TraversalContext.ActionExecutor as ScrollableMockActionExecutor;
        if (executor == null)
            return new ScrollActionResult(
                ScrollActionType.ScrollDown, false, ctx.Decision.CurrentProgress,
                "ActionExecutor is not ScrollableMockActionExecutor");

        double before = ctx.Decision.CurrentProgress;
        bool success = executor.ScrollDown(ctx.StepPercent);

        var vision = ctx.TraversalContext.VisionProvider as ScrollableMockVisionService;
        double after = vision?.GetScrollProgress(vision.CurrentPageId) ?? before;

        return new ScrollActionResult(ScrollActionType.ScrollDown, success, after,
            $"Scrolled from {before:F2} to {after:F2}");
    }
}
```

### Step 5: Verify（跳跃检测）

```csharp
/// <summary>跳跃检测器</summary>
public sealed class JumpDetector
{
    public ScrollVerifyResult Verify(
        ImmutableArray<string> beforeElements,
        ImmutableArray<string> afterElements)
    {
        var overlapStatus = DetectOverlapStatus(beforeElements, afterElements);
        
        var overlapSet = beforeElements.Intersect(afterElements).ToImmutableArray();
        var newElements = afterElements.Except(beforeElements).ToImmutableArray();
        int duplicateCount = afterElements.Count(id => beforeElements.Contains(id));

        return new ScrollVerifyResult(
            overlapStatus,
            beforeElements,
            afterElements,
            overlapSet.Length,
            newElements.Length,
            duplicateCount);
    }

    private static OverlapStatus DetectOverlapStatus(
        ImmutableArray<string> before,
        ImmutableArray<string> after)
    {
        if (before.IsEmpty && after.IsEmpty)
            return OverlapStatus.BothEmpty;
        
        if (before.IsEmpty)
            return OverlapStatus.NoOverlap_BeforeEmpty;
        
        if (after.IsEmpty)
            return OverlapStatus.NoOverlap_AfterEmpty;
        
        return before.Any(id => after.Contains(id))
            ? OverlapStatus.HasOverlap
            : OverlapStatus.NoOverlap_BothHaveElements;
    }
}
```

### Step 6: Recover（跳跃恢复）

```csharp
/// <summary>跳跃恢复器 - 回滚 + 减半步长重试</summary>
public sealed class JumpRecoveryHandler
{
    private readonly ScrollHandlerConfig _config;
    private readonly ScrollActionExecutor _executor;

    public JumpRecoveryResult RecoverFromJump(
        ScrollDecision decision,
        ScrollActionType actionType,
        ScrollContext scrollContext,
        ImmutableArray<string> beforeElements,
        double originalProgress)
    {
        double currentProgress = originalProgress;
        double currentStep = decision.RecommendedStep;
        int retryCount = 0;

        while (retryCount <= _config.MaxJumpRetryCount)
        {
            // 回滚到滚动前位置
            RollbackScroll(scrollContext, currentProgress);

            // 减半步长（但不小于最小步长）
            currentStep = Math.Max(currentStep / 2, _config.MinScrollStep);

            // 执行滚动
            var executeResult = _executor.Execute(actionType, updatedContext);

            if (!executeResult.Success)
                return new JumpRecoveryResult(false, retryCount, currentStep, currentProgress, "Execution failed");

            // 验证是否仍有跳跃
            var afterElements = GetVisibleElementIds(scrollContext);
            var verifyResult = new JumpDetector().Verify(beforeElements, afterElements);

            if (verifyResult.OverlapStatus == OverlapStatus.HasOverlap ||
                verifyResult.OverlapStatus == OverlapStatus.NoOverlap_BeforeEmpty ||
                verifyResult.OverlapStatus == OverlapStatus.BothEmpty)
            {
                return new JumpRecoveryResult(
                    true, retryCount + 1, currentStep, executeResult.NewProgress,
                    "Recovery successful");
            }

            retryCount++;

            if (retryCount > _config.MaxJumpRetryCount)
            {
                RollbackScroll(scrollContext, originalProgress);
                return new JumpRecoveryResult(
                    false, retryCount - 1, currentStep, originalProgress,
                    "Max retry count exceeded");
            }

            currentProgress = executeResult.NewProgress;
        }

        return new JumpRecoveryResult(
            false, retryCount, currentStep, originalProgress,
            "Unexpected exit from recovery loop");
    }
}
```

### Step 7: Statistics（统计）

```csharp
/// <summary>滚动处理统计</summary>
public sealed record ScrollHandlerStatistics(
    int ScrolledCount,         // 执行滚动次数
    int SkippedCount,          // 跳过次数（AtBottom/NotScrollable）
    int JumpDetectedCount,     // 检测到跳跃次数
    int JumpRecoveredCount,    // 成功恢复跳跃次数
    double TotalDistance,      // 总滚动距离
    double AverageStep);       // 平均步长
```

---

## 自适应步长策略

### 配置结构

```csharp
public sealed record ScrollHandlerConfig(
    // ===== 自适应步长配置 =====
    bool EnableAdaptiveStep = true,      // 是否启用自适应步长
    double DuplicateRatioThreshold = 0.7, // 重复元素比例阈值（70%）
    double AdaptiveStepIncrease = 1.5,   // 自适应步长增长因子（50%）
    int MinSampleSize = 3,               // 自适应最小样本数量
    ...
);
```

### 自适应步长算法

```csharp
/// <summary>自适应步长计算器</summary>
public sealed class AdaptiveStepCalculator
{
    /// <summary>根据滚动验证结果计算下一个步长</summary>
    public double CalculateNextStep(
        double currentStep,
        ScrollVerifyResult verifyResult)
    {
        if (!_config.EnableAdaptiveStep)
            return currentStep;

        // 计算重复元素比例
        double duplicateRatio = verifyResult.AfterElementIds.IsEmpty
            ? 0.0
            : (double)verifyResult.DuplicateElementCount / verifyResult.AfterElementIds.Length;

        // 重复元素太多 → 增大步长
        if (duplicateRatio >= _config.DuplicateRatioThreshold &&
            verifyResult.NewElementCount >= _config.MinSampleSize)
        {
            double newStep = Math.Min(
                currentStep * _config.AdaptiveStepIncrease,
                _config.MaxScrollStep);
            return newStep;
        }

        return currentStep;
    }
}
```

### 自适应步长决策图

```
┌─────────────────────────────────────────────────────────────┐
│                    自适应步长决策                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  输入:                                                      │
│    - currentStep: 当前步长                                  │
│    - verifyResult: 滚动验证结果                             │
│                                                             │
│  决策:                                                      │
│    IF (重复元素比例 >= 70%) AND (新元素数量 >= 3) THEN      │
│        nextStep = Min(currentStep * 1.5, MaxScrollStep)     │
│    ELSE                                                     │
│        nextStep = currentStep                               │
│                                                             │
│  示例:                                                      │
│    滚动前: [A, B, C, D, E]                                  │
│    滚动后: [A, B, C, D, E, F] (重复 5/6 = 83%, 新增 1)     │
│    → 新元素数量 < 3，步长不变                               │
│                                                             │
│    滚动前: [A, B, C, D, E, F, G]                            │
│    滚动后: [A, B, C, D, E, F, G, H] (重复 7/8 = 87.5%)    │
│    → 新元素数量 >= 3，重复比例 >= 70%                       │
│    → 步长从 0.3 增加到 0.45                                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 跳跃检测与恢复

### 防遗漏四重机制

```
┌─────────────────────────────────────────────────────────────┐
│                  防遗漏四重机制                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. Overlap Detection (跳跃检测)                            │
│     └─ 检测滚动前后元素重叠，防止跳跃遗漏                    │
│                                                             │
│  2. Progress Clamp (进度钳制)                               │
│     └─ SafeStep = Min(Preferred, MaxThreshold - Current)    │
│                                                             │
│  3. Element Deduplication (元素去重)                        │
│     └─ 按 ID 去重，低 threshold 优先                          │
│                                                             │
│  4. Visited Tracking (访问追踪)                             │
│     └─ VisitedChildren 集合追踪已访问元素                    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 防遗漏检查点

**Checkpoint 1: 滚动前**
- □ HasScroll = true？
- □ IsEndOfList = false？
- □ CurrentProgress < MaxThreshold？

**Checkpoint 2: 滚动中**
- □ StepPercent <= (MaxThreshold - CurrentProgress)？
- □ StepPercent >= MinScrollStep？

**Checkpoint 3: 滚动后**
- □ NewProgress = Clamp(OldProgress + Step, 0.0, Max)？
- □ 滚动后元素与滚动前有重叠？
- □ 新元素中排除已访问的 ID？

**Checkpoint 4: FrameComplete 前最终检查**
- □ IsEndOfList = true？
- □ HasScroll = false？（或 CurrentProgress >= MaxThreshold）

---

## 滚动模拟场景

### 场景分类

| 类别 | 场景数 | 覆盖内容 |
|------|--------|---------|
| 基础场景 | 4 | 单屏、双屏、多屏、空列表 |
| 边界场景 | 4 | 顶部、底部、接近、精确到底 |
| 元素场景 | 3 | 去重、重复、动态变化 |
| 步长场景 | 4 | 小步长、默认、大步长、自适应 |
| 跳跃场景 | 4 | 正常、检测、恢复、失败 |
| **总计** | **19** | **全覆盖** |

### 关键场景示例

#### 场景 1: 单屏列表

```csharp
// 场景: 单屏列表，不需要滚动
ScrollSegments(
    (0.0, s => s
        .Element("net1", "button", "Network 1")
        .Element("net2", "button", "Network 2"))
)

Expected:
  - HasScroll = false (max_threshold = 0.0)
  - IsEndOfList = true (progress >= max_threshold)
  - ScrollCount = 0
```

#### 场景 2: 双屏列表

```csharp
// 场景: 双屏列表，需要一次滚动
ScrollSegments(
    (0.0, s => s.Element("net1").Element("net2")),
    (0.5, s => s.Element("net3").Element("net4"))
)

Expected:
  - progress=0.0: [net1, net2], HasScroll=true, IsEndOfList=false
  - progress=0.5: [net1, net2, net3, net4], HasScroll=false, IsEndOfList=true
  - ScrollCount = 1
```

#### 场景 3: 接近底部

```csharp
// 场景: 接近底部时的滚动
CurrentProgress = 0.9
MaxThreshold = 1.0
PreferredStep = 0.3

Expected:
  - SafeStep = Min(0.3, 1.0 - 0.9) = 0.1
  - NewProgress = 1.0 (精确到底)
  - IsEndOfList = true
```

#### 场景 4: 元素去重

```csharp
// 场景: 元素在多个片段中重复
ScrollSegments(
    (0.0, s => s.Element("wifi_switch", "switch", "WiFi")),
    (0.5, s => s.Element("wifi_switch", "switch", "WiFi")),  // 重复
    (1.0, s => s.Element("ethernet", "switch", "Ethernet"))
)

progress=1.0 时的 Expected:
  - 返回: [wifi_switch, ethernet]
  - wifi_switch 只出现一次（来自 threshold=0.0）
```

#### 场景 5: 跳跃检测和恢复

```csharp
// 场景: 大步长导致跳跃，需要恢复
ScrollSegments(
    (0.0, s => s.Element("A").Element("B")),
    (0.5, s => s.Element("C").Element("D")),
    (1.0, s => s.Element("E"))
)

InitialProgress = 0.0
InitialStep = 1.0  // 太大！

Expected:
  - 第 1 次尝试: 0.0 → 1.0
    - Before: [A, B]
    - After: [A, B, C, D, E]
    - 检测: NoOverlap_BothHaveElements (跳跃!)
  - 恢复: 回滚到 0.0，步长减半到 0.5
  - 第 2 次尝试: 0.0 → 0.5
    - Before: [A, B]
    - After: [A, B, C, D]
    - 检测: HasOverlap ✓
  - 成功: FinalProgress = 0.5
```

#### 场景 6: 自适应步长增长

```csharp
// 场景: 重复元素太多，自动增大步长
Before: [A, B, C, D, E, F, G]
After:  [A, B, C, D, E, F, G, H]
  - 重复: 7/8 = 87.5%
  - 新增: 1

Expected (默认配置):
  - DuplicateRatio = 87.5% > 70% (阈值)
  - NewElementCount = 1 < 3 (最小样本)
  - → 步长不变（样本不足）

Expected (宽松配置 MinSampleSize = 1):
  - DuplicateRatio = 87.5% > 70%
  - NewElementCount = 1 >= 1
  - → NextStep = 0.3 * 1.5 = 0.45
```

---

## 架构设计

### 组件关系图

```
┌─────────────────────────────────────────────────────────────┐
│                      测试层                                  │
│  SimulationE2ETests / ScrollScenarioTests                   │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                    Mock 服务层                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  ScrollableMockVisionService                         │  │
│  │  - _scrollStates: Dictionary<string, ScrollState>    │  │
│  │  - _dataStore: ScrollDataStore                       │  │
│  │  - AnalyzeCurrentPageAsync(): 累积模式元素收集        │  │
│  │  - SimulateScroll(): 进度更新 + 状态记录              │  │
│  └──────────────────────────────────────────────────────┘  │
│                           │                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  ScrollableMockActionExecutor                        │  │
│  │  - ScrollDown(): 调用 SimulateScroll                 │  │
│  │  - ScrollUp(): 调用 SimulateScroll(-delta)           │  │
│  └──────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                    StateMachine 层                           │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  ScrollHandler (7-step pipeline)                    │  │
│  │  - ScrollabilityDetector                             │  │
│  │  - ScrollClassifier                                 │  │
│  │  - ScrollDecider                                     │  │
│  │  - ScrollActionExecutor                              │  │
│  │  - JumpDetector                                      │  │
│  │  - JumpRecoveryHandler                               │  │
│  │  - AdaptiveStepCalculator                            │  │
│  └──────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                    TraversalFSM 集成点                        │
│  HandleBranch():                                            │
│    - 所有子节点已访问时检查是否需要滚动                       │
│    - 调用 ScrollHandler.HandleScroll()                     │
│    - 滚动成功 → 重置 VisitedChildren → NodeSelect            │
│    - 滚动失败 → FrameComplete                                │
└─────────────────────────────────────────────────────────────┘
```

### FSM 集成代码

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
        var analysis = context.VisionProvider?.AnalyzeCurrentPageAsync();
        if (analysis?.Result != null)
        {
            var scrollHandler = new ScrollHandler(
                config: context.ScrollHandlerConfig ?? new ScrollHandlerConfig());
            
            var scrollResult = scrollHandler.HandleScroll(
                analysis.Result,
                GetScrollProgress(context),
                context);
            
            if (scrollResult.Success)
            {
                // 滚动成功，清除已访问标记
                context.ResetVisitedChildren(currentFrame.Node.NodeId);
                
                // 返回 NodeSelect 访问新元素
                return TraversalState.NodeSelect;
            }
        }
        
        // 进入 FrameComplete
        return TraversalState.FrameComplete;
    }
    
    return TraversalState.NodeSelect;
}
```

---

## 实施计划

### Phase 1: 数据模型（8 tasks）

```
src/UniClaw.Core/Simulation/Scroll/
  ├── ScrollSegment.cs
  ├── ScrollState.cs
  ├── ScrollAction.cs
  ├── ScrollDataStore.cs
  ├── OverlapStatus.cs (enum)
  ├── ScrollVerifyResult.cs
  ├── JumpRecoveryResult.cs
  ├── ScrollHandlerConfig.cs
  ├── ScrollActionResult.cs
  └── ScrollContext.cs
```

### Phase 2: Builder 扩展（6 tasks）

- ScrollSegmentBuilder.cs
- PageStateBuilder.ScrollSegments() 扩展
- 向后兼容验证

### Phase 3: Vision Service（13 tasks）

- ScrollableMockVisionService 实现
- 累积模式元素收集
- IsEndOfList/HasScroll 计算
- 滚动进度管理

### Phase 4: ScrollHandler 组件（12 tasks）

```
src/UniClaw.Core/StateMachine/Scroll/
  ├── ScrollabilityDetector.cs
  ├── ScrollClassifier.cs
  ├── ScrollDecider.cs
  ├── ScrollActionExecutor.cs
  ├── JumpDetector.cs
  ├── JumpRecoveryHandler.cs
  ├── AdaptiveStepCalculator.cs
  ├── ScrollStatisticsCollector.cs
  └── ScrollHandler.cs
```

### Phase 5: ActionExecutor（9 tasks）

- ScrollableMockActionExecutor 实现
- ScrollDown/ScrollUp 方法
- 滚动动作记录

### Phase 6: 测试（19+ tests）

- 数据模型测试
- Service 测试
- ScrollHandler 组件测试
- 场景测试（19 场景）
- 端到端集成测试

### Phase 7: 文档（4 tasks）

- 更新 simulation-baseline.md
- 更新 state-machine.md
- 添加代码注释
- 统一本文档

### Phase 8: 验证与归档（2 tasks）

- 最终验证（CI 全绿）
- 归档变更

---

## 测试策略

### 测试金字塔

```
                    /\
                   /  \
                  / E2E\
                 /______\
                /        \
               /  集成    \
              /__________  \
             /             \
            /    单元测试    \
           /_________________\
```

### 场景测试矩阵

| 场景类别 | 测试数量 | 覆盖点 |
|---------|---------|--------|
| 基础场景 | 4 | 单屏、双屏、多屏、空列表 |
| 边界场景 | 4 | 顶部、底部、接近、精确 |
| 元素场景 | 3 | 去重、重复、动态 |
| 步长场景 | 4 | 小步长、默认、大步长、自适应 |
| 跳跃场景 | 4 | 正常、检测、恢复、失败 |
| **总计** | **19** | **全覆盖** |

### 关键测试用例

```csharp
// 累积模式不遗漏
[Fact] public void Scroll_AccumulationMode_NoElementsMissed()

// 步长钳制不越界
[Fact] public void Scroll_SafeStep_NoOverflow()

// 去重不重复访问
[Fact] public void Scroll_Deduplication_NoDuplicateVisits()

// 跳跃检测和恢复
[Fact] public void Scroll_JumpDetection_RecoveryWithHalfStep()

// 自适应步长增长
[Fact] public void Scroll_AdaptiveStep_IncreaseOnHighDuplicateRatio()

// 精确到底
[Fact] public void Scroll_PreciseEndOfList()
```

---

## 附录

### 配置示例

```csharp
// 默认配置
var defaultConfig = new ScrollHandlerConfig();

// 保守配置（小步长，多次重试）
var conservativeConfig = new ScrollHandlerConfig(
    DefaultScrollStep: 0.1,
    MinScrollStep: 0.01,
    MaxJumpRetryCount: 5);

// 激进配置（大步长，快速滚动）
var aggressiveConfig = new ScrollHandlerConfig(
    DefaultScrollStep: 0.5,
    MinScrollStep: 0.05,
    MaxJumpRetryCount: 2,
    EnableAdaptiveStep: true,
    DuplicateRatioThreshold: 0.8);

// 测试配置（固定步长，无自适应）
var testConfig = new ScrollHandlerConfig(
    DefaultScrollStep: 0.3,
    EnableAdaptiveStep: false,
    MaxJumpRetryCount: 0);
```

### 术语对照

| Python | C# | 说明 |
|--------|-----|------|
| `ScrollSegment` | `ScrollSegment` | 滚动片段 |
| `ScrollState` | `ScrollState` | 滚动状态 |
| `ScrollAction` | `ScrollAction` | 滚动动作记录 |
| `ScrollDataStore` | `ScrollDataStore` | 数据存储 |
| `path_key` | `pageId` | 页面标识符 |
| `virtual_pages` | `StateFixture.Pages` | 页面数据 |

---

**文档所有者**: UniClaw.Core C# 迁移项目  
**状态**: 设计完成，待实施  
**最后更新**: 2026-07-12  
**版本**: 2.0
