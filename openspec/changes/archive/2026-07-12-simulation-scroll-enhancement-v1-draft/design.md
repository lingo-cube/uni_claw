# Scroll Simulation Enhancement — Unified Design Document

> **Version**: 2.0
> **Date**: 2026-07-12
> **Status**: Design Complete (含跳跃检测 + 自适应步长)
> **Python 对齐**: PRD_V7_0_SimScroll.md

---

## Table of Contents

1. [Context](#context)
2. [Goals / Non-Goals](#goals--non-goals)
3. [Core Concepts](#core-concepts)
4. [Data Models](#data-models)
5. [ScrollHandler Pipeline (7-step)](#scrollhandler-pipeline-7-step)
6. [Adaptive Step Strategy](#adaptive-step-strategy)
7. [Jump Detection & Recovery](#jump-detection--recovery)
8. [Scroll Simulation Scenarios](#scroll-simulation-scenarios)
9. [Architecture](#architecture)
10. [Decisions](#decisions)
11. [Implementation Tasks](#implementation-tasks)
12. [Testing Strategy](#testing-strategy)

---

## Context

**Current State**:
- C# 仿真基础设施有 StateFixture + StatefulMockVisionService
- 支持页面跳转模拟，但不支持滚动模拟
- PageAnalysis.IsEndOfList 当前来自 PageState.IsComplete（静态值）

**Problem Statement**:
现有 C# 实现无法测试滚动列表场景：
- 无滚动支持 → 无法测试 WiFi 列表等多屏内容
- 状态静态化 → IsEndOfList 不随滚动变化
- 无滚动状态跟踪 → 无法验证滚动逻辑
- 缺少滚动决策机制 → 无法集成到遍历流程

**Python 对齐目标**:
- Python V7.0 `src/simulation/scroll/` 模块
- ScrollSegment + ScrollState 数据模型
- ScrollableMockVisionService 累积模式实现

**Constraints**:
- 使用 C# 风格的 StateFixtureBuilder 扩展（不用 JSON）
- 滚动场景分类单独测试
- 保持现有测试不受影响（向后兼容）

---

## Goals / Non-Goals

**Goals**:
- ✅ 实现滚动模拟基础设施（ScrollSegment + ScrollState + ScrollDataStore）
- ✅ ScrollableMockVisionService 支持累积模式元素可见性
- ✅ StateFixtureBuilder 扩展支持滚动段定义
- ✅ is_end_of_list 自动计算（基于滚动进度和段阈值）
- ✅ ScrollHandler 7-step pipeline（含跳跃检测和恢复）
- ✅ 可配置滚动步长策略（默认 + 自适应）
- ✅ 滚动场景测试覆盖

**Non-Goals**:
- ❌ 不修改 PageAnalysis 结构（复用现有 HasScroll/IsEndOfList）
- ❌ 不影响现有 Simulation/Baseline 测试
- ❌ 不实现 Python 的故障注入（延迟/失败注入）— 预留 Phase 2
- ❌ 不实现向上滚动（ScrollUp）— 预留 Phase 2

---

## Core Concepts

### Scroll Progress (滚动进度)

归一化的 0.0-1.0 值，表示当前滚动位置：
- `0.0` = 列表顶部
- `1.0` = 列表底部
- 进度通过滚动操作累积增加/减少

### Scroll Segment (滚动片段)

按阈值分段的元素集合：
```csharp
public sealed record ScrollSegment(
    double Threshold,           // 激活阈值 (0.0-1.0)
    ImmutableArray<PageElement> Elements  // 该片段的元素
);
```

### Accumulation Mode (累积模式)

**核心规则**: 所有 `Threshold <= CurrentProgress` 的片段元素都可见。

```
CurrentProgress = 0.5:
  Segment0 (Threshold=0.0) → 可见 (0.0 <= 0.5) ✓
  Segment1 (Threshold=0.5) → 可见 (0.5 <= 0.5) ✓
  Segment2 (Threshold=1.0) → 不可见 (1.0 > 0.5) ✗
```

### Element Deduplication (元素去重)

当同一元素 ID 在多个片段中出现时，只返回一个（低 threshold 优先）：

```csharp
// Segment0 (threshold=0.0): wifi_switch
// Segment1 (threshold=0.5): wifi_switch (重复)
// 结果: 只返回 Segment0 的 wifi_switch
```

---

## Data Models

### Core Models

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

### PageState Extension

```csharp
// 扩展 PageState 支持 ScrollSegment 存储
public sealed record PageState(
    string PageName,
    ImmutableArray<PageElement> Elements,
    bool IsComplete = false,
    ImmutableArray<ScrollSegment> ScrollSegments = default
)
{
    public ImmutableArray<ScrollSegment> ScrollSegments { get; init; } =
        ScrollSegments.IsDefaultOrEmpty
            ? ImmutableArray<ScrollSegment>.Empty
            : ScrollSegments;
}
```

---

## ScrollHandler Pipeline (7-step)

### Pipeline Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ScrollHandler 7-step Pipeline                              │
└─────────────────────────────────────────────────────────────────────────────┘

  detect → classify → decide → execute → verify → recover → statistics
    ↓         ↓         ↓        ↓        ↓         ↓         ↓
  可滚动?  什么类型?  滚动?    执行    跳跃?   恢复?    统计
```

### Step 1: Detect (可滚动性检测)

```csharp
/// <summary>页面可滚动性检测结果</summary>
public enum Scrollability
{
    /// <summary>非滚动页面（无 ScrollSegment 数据）</summary>
    NotScrollable,
    /// <summary>可滚动且未到底（HasScroll && !IsEndOfList）</summary>
    CanScrollDown,
    /// <summary>可滚动但已到底（HasScroll && IsEndOfList）</summary>
    AtBottom,
    /// <summary>可滚动且在顶部（可向上滚动）</summary>
    CanScrollUp
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

### Step 2: Classify (滚动分类)

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
        // Sub-method 1: 确认可滚动性（已由 detector 完成）
        
        // Sub-method 2: 获取当前进度
        double progress = currentProgress;

        // Sub-method 3: 计算最大进度
        double maxProgress = segments.IsEmpty
            ? 1.0
            : segments.Max(s => s.Threshold);

        // Sub-method 4: 确定推荐步长
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
        // 使用配置的默认步长
        double defaultStep = _scrollHandlerConfig.DefaultScrollStep;

        return scrollability switch
        {
            Scrollability.CanScrollDown => Math.Min(
                defaultStep,
                maxProgress - currentProgress),  // 不超过剩余距离
            Scrollability.CanScrollUp => Math.Min(
                defaultStep,
                currentProgress),  // 不超过当前距离
            _ => 0.0
        };
    }
}
```

### Step 3: Decide (滚动决策)

```csharp
/// <summary>滚动动作类型</summary>
public enum ScrollActionType
{
    /// <summary>不执行滚动</summary>
    None,
    /// <summary>向下滚动</summary>
    ScrollDown,
    /// <summary>向上滚动</summary>
    ScrollUp
}

/// <summary>滚动决策器 - 将 ScrollDecision 映射到 ScrollActionType</summary>
public sealed class ScrollDecider
{
    public ScrollActionType Decide(ScrollDecision decision)
    {
        return decision.Scrollability switch
        {
            // 优先级 1: 可向下滚动 → ScrollDown
            Scrollability.CanScrollDown => ScrollActionType.ScrollDown,

            // 优先级 2: 可向上滚动 → ScrollUp
            Scrollability.CanScrollUp => ScrollActionType.ScrollUp,

            // 优先级 3: 已到底或不滚动 → None
            Scrollability.AtBottom => ScrollActionType.None,
            Scrollability.NotScrollable => ScrollActionType.None,

            _ => ScrollActionType.None
        };
    }
}
```

### Step 4: Execute (滚动执行)

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

    public ScrollActionExecutor(
        Func<ScrollContext, ScrollActionResult>? scrollDownHook = null,
        Func<ScrollContext, ScrollActionResult>? scrollUpHook = null,
        Func<ScrollContext, ScrollActionResult>? noneHook = null)
    {
        _dispatchTable = new Dictionary<ScrollActionType, Func<ScrollContext, ScrollActionResult>>
        {
            [ScrollActionType.ScrollDown] = scrollDownHook ?? DefaultScrollDown,
            [ScrollActionType.ScrollUp] = scrollUpHook ?? DefaultScrollUp,
            [ScrollActionType.None] = noneHook ?? DefaultNone
        };
    }

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

    // ... DefaultScrollUp, DefaultNone 类似实现
}
```

### Step 5: Verify (跳跃检测)

```csharp
/// <summary>滚动前后元素重叠状态</summary>
public enum OverlapStatus
{
    /// <summary>有重叠，安全</summary>
    HasOverlap,
    /// <summary>无重叠但前后都有元素 → 发生跳跃</summary>
    NoOverlap_BothHaveElements,
    /// <summary>滚动前无元素（初始状态）</summary>
    NoOverlap_BeforeEmpty,
    /// <summary>滚动后无元素（可能到底）</summary>
    NoOverlap_AfterEmpty,
    /// <summary>都无元素（空列表）</summary>
    BothEmpty
}

/// <summary>滚动验证结果</summary>
public sealed record ScrollVerifyResult(
    OverlapStatus OverlapStatus,
    ImmutableArray<string> BeforeElementIds,
    ImmutableArray<string> AfterElementIds,
    int OverlapCount,              // 重叠元素数量
    int NewElementCount,           // 新出现元素数量
    int DuplicateElementCount);    // 重复元素数量（用于自适应步长）

/// <summary>跳跃检测器</summary>
public sealed class JumpDetector
{
    public ScrollVerifyResult Verify(
        ImmutableArray<string> beforeElements,
        ImmutableArray<string> afterElements)
    {
        // 检测重叠状态
        var overlapStatus = DetectOverlapStatus(beforeElements, afterElements);
        
        // 计算重叠、新元素、重复元素数量
        var overlapSet = beforeElements.Intersect(afterElements).ToImmutableArray();
        var newElements = afterElements.Except(beforeElements).ToImmutableArray();
        
        // 重复元素 = afterElements 中也在 beforeElements 中的元素
        // 但排除新增的元素（这个逻辑可能需要根据实际场景调整）
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

### Step 6: Recover (跳跃恢复)

```csharp
/// <summary>跳跃恢复结果</summary>
public sealed record JumpRecoveryResult(
    bool Success,              // 恢复是否成功
    int RetryCount,            // 实际重试次数
    double FinalStep,          // 最终使用的步长
    double FinalProgress,      // 恢复后的进度
    string Reason);            // 原因说明

/// <summary>跳跃恢复器 - 回滚 + 减半步长重试</summary>
public sealed class JumpRecoveryHandler
{
    private readonly ScrollHandlerConfig _config;
    private readonly ScrollActionExecutor _executor;

    public JumpRecoveryHandler(
        ScrollHandlerConfig config,
        ScrollActionExecutor executor)
    {
        _config = config;
        _executor = executor;
    }

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

            // 更新决策的步长
            var updatedDecision = decision with { RecommendedStep = currentStep };
            var updatedContext = scrollContext with 
            { 
                Decision = updatedDecision,
                StepPercent = currentStep
            };

            // 执行滚动
            var executeResult = _executor.Execute(actionType, updatedContext);

            if (!executeResult.Success)
            {
                return new JumpRecoveryResult(
                    false, retryCount, currentStep, currentProgress,
                    "Execution failed during recovery");
            }

            // 验证是否仍有跳跃
            var afterElements = GetVisibleElementIds(scrollContext);
            var verifyResult = new JumpDetector().Verify(beforeElements, afterElements);

            if (verifyResult.OverlapStatus == OverlapStatus.HasOverlap ||
                verifyResult.OverlapStatus == OverlapStatus.NoOverlap_BeforeEmpty ||
                verifyResult.OverlapStatus == OverlapStatus.BothEmpty)
            {
                // 恢复成功
                return new JumpRecoveryResult(
                    true, retryCount + 1, currentStep, executeResult.NewProgress,
                    "Recovery successful");
            }

            retryCount++;

            if (retryCount > _config.MaxJumpRetryCount)
            {
                // 超过最大重试次数，回滚到原始位置
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

    private void RollbackScroll(ScrollContext context, double targetProgress)
    {
        var vision = context.TraversalContext.VisionProvider as ScrollableMockVisionService;
        vision?.SetScrollProgress(context.TraversalContext.CurrentPageId, targetProgress);
    }

    private ImmutableArray<string> GetVisibleElementIds(ScrollContext context)
    {
        var analysis = context.TraversalContext.VisionProvider?.AnalyzeCurrentPageAsync();
        return analysis?.Result != null
            ? analysis.Result.Items.Select(item => item.Id).ToImmutableArray()
            : ImmutableArray<string>.Empty;
    }
}
```

### Step 7: Statistics (统计)

```csharp
/// <summary>滚动处理统计</summary>
public sealed record ScrollHandlerStatistics(
    int ScrolledCount,         // 执行滚动次数
    int SkippedCount,          // 跳过次数（AtBottom/NotScrollable）
    int JumpDetectedCount,     // 检测到跳跃次数
    int JumpRecoveredCount,    // 成功恢复跳跃次数
    double TotalDistance,      // 总滚动距离
    double AverageStep);       // 平均步长

/// <summary>统计收集器</summary>
public sealed class ScrollStatisticsCollector
{
    private int _scrolledCount;
    private int _skippedCount;
    private int _jumpDetectedCount;
    private int _jumpRecoveredCount;
    private double _totalDistance;
    private double _totalStep;

    public void RecordScroll(double step, double distance)
    {
        _scrolledCount++;
        _totalDistance += distance;
        _totalStep += step;
    }

    public void RecordSkip()
    {
        _skippedCount++;
    }

    public void RecordJumpDetected()
    {
        _jumpDetectedCount++;
    }

    public void RecordJumpRecovered()
    {
        _jumpRecoveredCount++;
    }

    public ScrollHandlerStatistics GetStatistics()
    {
        return new ScrollHandlerStatistics(
            _scrolledCount,
            _skippedCount,
            _jumpDetectedCount,
            _jumpRecoveredCount,
            _totalDistance,
            _scrolledCount > 0 ? _totalStep / _scrolledCount : 0.0);
    }
}
```

---

## Adaptive Step Strategy

### 配置结构

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

### 自适应步长算法

```csharp
/// <summary>自适应步长计算器</summary>
public sealed class AdaptiveStepCalculator
{
    private readonly ScrollHandlerConfig _config;

    public AdaptiveStepCalculator(ScrollHandlerConfig config)
    {
        _config = config;
    }

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

        // 使用当前步长
        return currentStep;
    }

    /// <summary>计算推荐初始步长</summary>
    public double CalculateInitialStep(
        double currentProgress,
        double maxProgress)
    {
        double remainingDistance = maxProgress - currentProgress;

        // 剩余距离很小时，使用较小的步长
        if (remainingDistance < _config.DefaultScrollStep)
        {
            return Math.Max(remainingDistance / 2, _config.MinScrollStep);
        }

        return _config.DefaultScrollStep;
    }
}
```

### 自适应步长逻辑

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
│    滚动前: [A, B, C]                                        │
│    滚动后: [A, B, C, D] (重复 3/4 = 75%, 新增 1)           │
│    → 重复太多，步长从 0.3 增加到 0.45                       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Jump Detection & Recovery

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

### 检查点清单

```
┌─────────────────────────────────────────────────────────────┐
│                  防遗漏检查点                                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Checkpoint 1: 滚动前                                       │
│    □ HasScroll = true？                                      │
│    □ IsEndOfList = false？                                   │
│    □ CurrentProgress < MaxThreshold？                         │
│                                                             │
│  Checkpoint 2: 滚动中                                       │
│    □ StepPercent <= (MaxThreshold - CurrentProgress)？        │
│    □ StepPercent >= MinScrollStep？                          │
│                                                             │
│  Checkpoint 3: 滚动后                                       │
│    □ NewProgress = Clamp(OldProgress + Step, 0.0, Max)？     │
│    □ 滚动后元素与滚动前有重叠？                               │
│    □ 新元素中排除已访问的 ID？                                │
│                                                             │
│  Checkpoint 4: FrameComplete 前最终检查                      │
│    □ IsEndOfList = true？                                    │
│    □ HasScroll = false？（或 CurrentProgress >= MaxThreshold）│
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Scroll Simulation Scenarios

### 场景分类

```
┌─────────────────────────────────────────────────────────────┐
│                  滚动模拟场景分类                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  基础场景 (Basic)                                           │
│    ├── 单屏列表                                             │
│    ├── 双屏列表                                             │
│    ├── 多屏列表                                             │
│    └── 空列表                                               │
│                                                             │
│  边界场景 (Boundary)                                        │
│    ├── 顶部边界                                             │
│    ├── 底部边界                                             │
│    ├── 接近底部                                             │
│    └── 精确到底                                             │
│                                                             │
│  元素场景 (Element)                                         │
│    ├── 无重复元素                                           │
│    ├── 有重复元素                                           │
│    ├── 元素去重                                             │
│    └── 动态元素变化                                         │
│                                                             │
│  步长场景 (Step Size)                                       │
│    ├── 小步长滚动 (5%)                                      │
│    ├── 默认步长滚动 (30%)                                   │
│    ├── 大步长滚动 (50%)                                     │
│    └── 自适应步长                                           │
│                                                             │
│  跳跃场景 (Jump)                                            │
│    ├── 正常滚动（有重叠）                                   │
│    ├── 跳跃检测（无重叠）                                   │
│    ├── 跳跃恢复（减半步长）                                 │
│    └── 跳跃失败（超过重试次数）                             │
│                                                             │
│  故障场景 (Fault) - Phase 2                                │
│    ├── 滚动延迟                                             │
│    ├── 滚动失败                                             │
│    └── 网络超时                                             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 详细场景规格

#### 场景 1: 单屏列表

```csharp
// 场景: 单屏列表，不需要滚动
// Given: 只有 threshold=0.0 的片段
// When: 访问所有元素
// Then: 不执行滚动操作，scroll_count=0

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
// Given: 两个片段，threshold=0.0 和 0.5
// When: 从顶部滚动到底部
// Then: 执行 1 次滚动，所有元素都被访问

ScrollSegments(
    (0.0, s => s
        .Element("net1")
        .Element("net2")),
    (0.5, s => s
        .Element("net3")
        .Element("net4"))
)

Expected:
  - progress=0.0: [net1, net2], HasScroll=true, IsEndOfList=false
  - progress=0.5: [net1, net2, net3, net4], HasScroll=false, IsEndOfList=true
  - ScrollCount = 1
```

#### 场景 3: 多屏列表

```csharp
// 场景: 5 屏列表，需要多次滚动
// Given: 5 个片段
// When: 从顶部滚动到底部
// Then: 执行多次滚动，所有元素都被访问

ScrollSegments(
    (0.0, s => s.Element("A").Element("B")),
    (0.25, s => s.Element("C").Element("D")),
    (0.5, s => s.Element("E").Element("F")),
    (0.75, s => s.Element("G").Element("H")),
    (1.0, s => s.Element("I"))
)

Expected:
  - 使用默认步长 0.3
  - 需要约 4 次滚动到达底部
  - 所有 9 个元素都被访问
```

#### 场景 4: 接近底部

```csharp
// 场景: 接近底部时的滚动
// Given: 当前进度 0.9，最大阈值 1.0
// When: 尝试滚动 0.3（默认步长）
// Then: 自动钳制到剩余距离 0.1

CurrentProgress = 0.9
MaxThreshold = 1.0
PreferredStep = 0.3

Expected:
  - SafeStep = Min(0.3, 1.0 - 0.9) = 0.1
  - NewProgress = 1.0 (精确到底)
  - IsEndOfList = true
```

#### 场景 5: 元素去重

```csharp
// 场景: 元素在多个片段中重复
// Given: wifi_switch 出现在多个片段中
// When: 收集可见元素
// Then: wifi_switch 只返回一次（来自最低 threshold）

ScrollSegments(
    (0.0, s => s.Element("wifi_switch", "switch", "WiFi")),
    (0.5, s => s.Element("wifi_switch", "switch", "WiFi")),  // 重复
    (1.0, s => s.Element("ethernet", "switch", "Ethernet"))
)

progress=1.0 时的 Expected:
  - 返回: [wifi_switch, ethernet]
  - wifi_switch 只出现一次（来自 threshold=0.0）
```

#### 场景 6: 跳跃检测和恢复

```csharp
// 场景: 大步长导致跳跃，需要恢复
// Given: 3 个片段，尝试直接从 0.0 跳到 1.0
// When: 执行滚动
// Then: 检测到跳跃，回滚并减小步长

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

#### 场景 7: 自适应步长增长

```csharp
// 场景: 重复元素太多，自动增大步长
// Given: 滚动后大部分元素是重复的
// When: 计算下一步长
// Then: 自动增大步长

Before: [A, B, C, D, E]
After:  [A, B, C, D, E, F]
  - 重复: 5/6 = 83%
  - 新增: 1

Expected (默认配置):
  - DuplicateRatio = 83% > 70% (阈值)
  - NewElementCount = 1 < 3 (最小样本)
  - → 步长不变（样本不足）

Before: [A, B, C, D, E, F, G]
After:  [A, B, C, D, E, F, G, H]
  - 重复: 7/8 = 87.5%
  - 新增: 1

Expected (更宽松配置):
  - MinSampleSize = 1
  - DuplicateRatio = 87.5% > 70%
  - → NextStep = 0.3 * 1.5 = 0.45
```

#### 场景 8: 空列表

```csharp
// 场景: 空列表，无元素
// Given: 没有片段或空片段
// When: 尝试滚动
// Then: 快速退出，不进入死循环

ScrollSegments(
    (0.0, s => { })  // 空片段
)

Expected:
  - HasScroll = false (无有效片段)
  - IsEndOfList = false
  - 不执行滚动
```

#### 场景 9: 精确到底

```csharp
// 场景: 精确滚动到底部
// Given: 进度精确等于最大阈值
// When: 检查是否到底
// Then: IsEndOfList = true

ScrollSegments(
    (0.0, s => s.Element("A")),
    (0.5, s => s.Element("B")),
    (1.0, s => s.Element("C"))
)

CurrentProgress = 1.0
MaxThreshold = 1.0

Expected:
  - IsEndOfList = true (1.0 >= 1.0)
  - HasScroll = false
  - 不执行滚动
```

#### 场景 10: 顶部边界

```csharp
// 场景: 在顶部尝试向上滚动
// Given: 进度为 0（顶部）
// When: 尝试向上滚动
// Then: 进度保持为 0

CurrentProgress = 0.0
ScrollUp(step = 0.1)

Expected:
  - NewProgress = Max(0.0 - 0.1, 0.0) = 0.0
  - 滚动无效，进度钳制到边界
```

---

## Architecture

### Component Relationship

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
│  │  - GetScrollProgress(): 查询当前进度                  │  │
│  │  - SetScrollProgress(): 设置进度（用于回滚）          │  │
│  └──────────────────────────────────────────────────────┘  │
│                           │                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  ScrollableMockActionExecutor                        │  │
│  │  - ScrollDown(): 调用 SimulateScroll                 │  │
│  │  - ScrollUp(): 调用 SimulateScroll(-delta)           │  │
│  │  - GetScrollCount(): 查询滚动次数                     │  │
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
│  │  - ScrollStatisticsCollector                         │  │
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

### FSM 集成点

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
            
            // 滚动失败，记录但不中断
            context.RecordScrollFailure(scrollResult.Description);
        }
        
        // 进入 FrameComplete
        return TraversalState.FrameComplete;
    }
    
    return TraversalState.NodeSelect;
}
```

---

## Decisions

### Decision 1: 7-step Pipeline with Jump Recovery

**选择**: ScrollHandler 使用 7-step pipeline，包含跳跃检测和恢复

**理由**:
- 跳跃检测作为核心链路，而非测试验证
- 提供完整的防遗漏机制
- 支持回滚和减半步长重试

**实现**: detect → classify → decide → execute → verify → recover → statistics

### Decision 2: Configurable Step Strategy

**选择**: 所有步长参数可配置

**理由**:
- 不同场景可能需要不同步长
- 自适应算法参数需要可调整
- 便于性能优化

**配置项**:
- DefaultScrollStep: 默认 30%
- MinScrollStep: 最小 1%
- MaxScrollStep: 最大 50%
- MaxJumpRetryCount: 最大重试次数（默认 3）

### Decision 3: Adaptive Step Based on Duplicate Ratio

**选择**: 当重复元素比例过高时，自动增大步长

**理由**:
- 重复太多说明步长太小，效率低
- 避免过多微小滚动
- 提高遍历效率

**算法**:
```
IF (重复元素比例 >= 70%) AND (新元素数量 >= 3) THEN
    nextStep = Min(currentStep * 1.5, MaxScrollStep)
```

### Decision 4: Accumulation Mode

**选择**: `threshold <= progress` 的所有段元素都可见

**理由**:
- 符合实际滚动行为
- Python 对齐
- 天然提供重叠保护

### Decision 5: Element Deduplication

**选择**: 同一 ID 元素只返回一个（低 threshold 优先）

**理由**:
- 避免重复访问
- 符合 UI 行为

---

## Implementation Tasks

### Phase 1: Data Models (Task 1.1-1.7)

```
src/UniClaw.Core/Simulation/Scroll/
  ├── ScrollSegment.cs          # sealed record (Threshold + Elements)
  ├── ScrollState.cs            # sealed record (CurrentProgress + ScrollCount + History)
  ├── ScrollAction.cs           # sealed record (Action + StepPercent + Before/After + Timestamp)
  ├── ScrollDataStore.cs        # 存储和查询 ScrollSegment 数据
  ├── OverlapStatus.cs          # enum (跳跃检测状态)
  ├── ScrollVerifyResult.cs     # sealed record (验证结果)
  ├── JumpRecoveryResult.cs     # sealed record (恢复结果)
  └── ScrollHandlerConfig.cs    # sealed record (配置)

tests/UniClaw.Core.Tests/Simulation/Scroll/
  ├── ScrollSegmentTests.cs
  ├── ScrollStateTests.cs
  └── ScrollActionTests.cs
```

### Phase 2: StateFixtureBuilder Extension (Task 2.1-2.6)

```csharp
// 新增 ScrollSegmentBuilder
public sealed class ScrollSegmentBuilder
{
    private readonly double _threshold;
    private readonly List<PageElement> _elements = new();

    internal ScrollSegmentBuilder(double threshold) => _threshold = threshold;

    public ScrollSegmentBuilder Element(string id, string type = "button", 
        string text = "", double x = 0.5, double y = 0.5)
    {
        _elements.Add(new PageElement(id, type, text, x, y));
        return this;
    }

    public ScrollSegment Build() => new(_threshold, _elements.ToImmutableArray());
}

// PageStateBuilder 扩展
public PageStateBuilder ScrollSegments(
    params (double threshold, Action<ScrollSegmentBuilder> configure)[] segments)
```

### Phase 3: ScrollableMockVisionService (Task 3.1-3.9)

```csharp
public sealed class ScrollableMockVisionService : StatefulMockVisionService
{
    private readonly Dictionary<string, ScrollState> _scrollStates;
    private readonly ScrollDataStore _dataStore;

    // 1. 滚动状态管理
    public ScrollState GetOrCreateScrollState(string pageId) { }
    
    // 2. 累积模式元素收集
    private ImmutableArray<PageElement> GetVisibleElements(...) { }
    
    // 3. IsEndOfList 计算
    private bool CalculateIsEndOfList(...) { }
    
    // 4. HasScroll 计算
    private bool CalculateHasScroll(...) { }
    
    // 5. 模拟滚动
    public double SimulateScroll(double delta) { }
    
    // 6. 查询滚动进度
    public double GetScrollProgress(string pageId) { }
    
    // 7. 设置滚动进度（用于回滚）
    public void SetScrollProgress(string pageId, double progress) { }
    
    // 8. 重写 AnalyzeCurrentPageAsync
    public override Task<PageAnalysis?> AnalyzeCurrentPageAsync(...) { }
    
    // 9. 获取数据存储
    public ScrollDataStore GetDataStore() => _dataStore;
}
```

### Phase 4: ScrollHandler Components (Task 4.1-4.9)

```
src/UniClaw.Core/StateMachine/Scroll/
  ├── ScrollabilityDetector.cs      # Step 1: Detect
  ├── ScrollClassifier.cs           # Step 2: Classify
  ├── ScrollDecider.cs              # Step 3: Decide
  ├── ScrollActionExecutor.cs       # Step 4: Execute
  ├── JumpDetector.cs               # Step 5: Verify
  ├── JumpRecoveryHandler.cs        # Step 6: Recover
  ├── ScrollStatisticsCollector.cs  # Step 7: Statistics
  ├── AdaptiveStepCalculator.cs     # 自适应步长
  └── ScrollHandler.cs              # 7-step pipeline 编排
```

### Phase 5: ScrollableMockActionExecutor (Task 5.1-5.8)

```csharp
public sealed class ScrollableMockActionExecutor : StatefulMockActionExecutor
{
    private readonly ScrollableMockVisionService _scrollableVision;
    private readonly List<ScrollAction> _scrollActions = new();

    public bool ScrollDown(double stepPercent = 0.3) { }
    public bool ScrollUp(double stepPercent = 0.1) { }
    public int GetScrollCount() => _scrollActions.Count;
    private void RecordScrollAction(...) { }
}
```

### Phase 6: Tests (Task 6.1-6.12)

```
tests/UniClaw.Core.Tests/
  ├── Simulation/Scroll/
  │   ├── ScrollSegmentTests.cs           # 数据模型测试
  │   ├── ScrollStateTests.cs
  │   ├── ScrollActionTests.cs
  │   ├── ScrollDataStoreTests.cs
  │   ├── ScrollableMockVisionServiceTests.cs
  │   ├── ScrollableMockActionExecutorTests.cs
  │   └── ScrollScenarioTests.cs         # 端到端场景测试
  │
  └── StateMachine/Scroll/
      ├── ScrollabilityDetectorTests.cs
      ├── ScrollClassifierTests.cs
      ├── ScrollDeciderTests.cs
      ├── ScrollActionExecutorTests.cs
      ├── JumpDetectorTests.cs
      ├── JumpRecoveryHandlerTests.cs
      ├── AdaptiveStepCalculatorTests.cs
      └── ScrollHandlerTests.cs          # 集成测试
```

---

## Testing Strategy

### 测试金字塔

```
┌─────────────────────────────────────────────────────────────┐
│                    测试金字塔                                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│                        /|\                                  │
│                       / | \                                 │
│                      /  E  \                                │
│                     /   2   \                               │
│                    /   E   E\                              │
│                   /    2    T\                             │
│                  /  (场景测试)  \                            │
│                 /_______________\                           │
│                /                 \                          │
│               /                   \                         │
│              /        /\           \                        │
│             /        /  \           \                       │
│            /    I1   /  I   \        \                      │
│           /        /    1    \        \                     │
│          /       /  (集成测试) \        \                    │
│         /      /_______________\        \                   │
│        /      /                   \      \                  │
│       /      /        /\            \      \                 │
│      /      /        /  \            \      \                │
│     /   U1 /        /  U \            \  U2 \               │
│    /      /        /     \            \      \              │
│   /      /   (单元测试)    \            \      \             │
│  /      /____________________\            \     \            │
│ /                                            \    \           │
│/______________________________________________\____\          │
│                                                             │
│  U1 = Unit Tests (数据模型、单个组件)                        │
│  I1 = Integration Tests (组件集成)                           │
│  E2 = End-to-End Tests (完整场景)                            │
│  U2 = Unit Tests for Handlers (各 Handler 独立测试)           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
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
// 测试: 累积模式不遗漏
[Fact]
public void Scroll_AccumulationMode_NoElementsMissed()
{
    // 3 个片段，5 个元素
    // 滚动 3 次覆盖所有片段
    // 验证: 所有 5 个元素都被访问
}

// 测试: 步长钳制不越界
[Fact]
public void Scroll_SafeStep_NoOverflow()
{
    // CurrentProgress = 0.9, MaxThreshold = 1.0
    // 验证: SafeStep <= (1.0 - 0.9) = 0.1
}

// 测试: 去重不重复访问
[Fact]
public void Scroll_Deduplication_NoDuplicateVisits()
{
    // wifi_switch 在多个片段重复
    // 验证: wifi_switch 只被访问一次
}

// 测试: 跳跃检测和恢复
[Fact]
public void Scroll_JumpDetection_RecoveryWithHalfStep()
{
    // 大步长导致跳跃
    // 验证: 检测到跳跃并减半步长重试
}

// 测试: 自适应步长增长
[Fact]
public void Scroll_AdaptiveStep_IncreaseOnHighDuplicateRatio()
{
    // 重复元素比例 > 70%
    // 验证: 步长自动增大
}

// 测试: 精确到底
[Fact]
public void Scroll_PreciseEndOfList()
{
    // 进度精确等于最大阈值
    // 验证: IsEndOfList = true
}
```

---

## Appendix

### A. 关键设计决策对比

| 决策 | C# 选择 | Python 对应 | 说明 |
|------|---------|------------|------|
| 数据模型 | sealed record class | @dataclass | C# 不可变设计 |
| 集合类型 | ImmutableArray | List | C# 不可变集合 |
| Builder 模式 | StateFixtureBuilder 扩展 | YAML/JSON fixture | C# Fluent API |
| 累积模式 | threshold <= progress | 相同 | 一致 |
| 元素去重 | 低 threshold 优先 | 相同 | 一致 |
| 跳跃检测 | 核心链路 | 无 | C# 增强功能 |
| 自适应步长 | 支持 | 无 | C# 增强功能 |

### B. 术语对照

| Python | C# | 说明 |
|--------|-----|------|
| `ScrollSegment` | `ScrollSegment` | 滚动片段 |
| `ScrollState` | `ScrollState` | 滚动状态 |
| `ScrollAction` | `ScrollAction` | 滚动动作记录 |
| `ScrollDataStore` | `ScrollDataStore` | 数据存储 |
| `path_key` | `pageId` | 页面标识符 |
| `virtual_pages` | `StateFixture.Pages` | 页面数据 |
| `has_scroll` | `HasScroll` | 是否可滚动 |
| `is_end_of_list` | `IsEndOfList` | 是否到底 |

### C. 配置示例

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

---

**文档所有者**: UniClaw.Core C# 迁移项目
**状态**: 设计完成，待实施
**最后更新**: 2026-07-12
**版本**: 2.0
