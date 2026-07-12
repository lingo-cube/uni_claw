# C# 滚动列表模拟测试 PRD

> **版本**: 1.1
> **日期**: 2026-07-12
> **状态**: 设计阶段
> **Python 对齐**: PRD_V7_0_SimScroll.md
> **OpenSpec Change**: simulation-scroll-enhancement

> **版本历史**:
> - v1.1 (2026-07-12): 新增 ScrollHandler 设计（5-step pipeline 遵循 Handler Pipeline 模式）
> - v1.0 (2026-07-12): 初始版本，包含滚动数据模型、Mock 服务和 ActionExecutor 设计

---

## 文档信息

- **所有者**: UniClaw.Core C# 迁移项目
- **Python 参考**: docs/prd/PRD_V7_0_SimScroll.md
- **OpenSpec**: openspec/changes/simulation-scroll-enhancement/
- **设计文档**: openspec/changes/simulation-scroll-enhancement/design.md

---

## 目录

1. [背景与问题](#背景与问题)
2. [目标与范围](#目标与范围)
3. [核心概念](#核心概念)
4. [滚动处理逻辑](#滚动处理逻辑)
5. [数据模型](#数据模型)
6. [架构设计](#架构设计)
7. [接口设计](#接口设计)
8. [ScrollHandler 设计](#scrollhandler-设计)
9. [测试场景](#测试场景)
10. [实施计划](#实施计划)
11. [Python 对齐说明](#python-对齐说明)

---

## 背景与问题

### 现状

C# UniClaw.Core 仿真基础设施当前存在以下限制：

| 限制 | 描述 | 影响 |
|------|------|------|
| **无滚动支持** | `StatefulMockVisionService` 返回固定元素集合 | 无法测试滚动列表场景 |
| **状态静态化** | `IsEndOfList` 来自 `PageState.IsComplete`（静态值） | 无法动态检测列表到底 |
| **无滚动状态跟踪** | 没有滚动进度、次数、历史记录 | 无法验证滚动逻辑 |
| **故障场景缺失** | 无法模拟滚动卡顿、无响应等边界情况 | 测试覆盖不完整 |

### 问题案例

**场景**：测试 WiFi 列表遍历

现有 C# 实现：
```csharp
// StatefulMockVisionService.BuildPageAnalysis
return new PageAnalysis(
    // ... 其他字段
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

### 根本原因

缺少**滚动列表模拟能力**。现有的 Mock 视觉服务基于离散的页面状态（PageState），无法模拟：
1. 连续的滚动进度（0.0-1.0）
2. 动态元素可见性（随滚动变化）
3. 滚动状态管理（进度、次数、历史）

---

## 目标与范围

### 核心目标

1. **支持滚动列表模拟** - 根据滚动进度返回不同元素集合
2. **滚动状态管理** - 跟踪每个页面的进度、次数、历史
3. **动态状态计算** - 自动计算 `HasScroll` 和 `IsEndOfList`
4. **元素去重机制** - 确保同一 ID 元素只返回一个
5. **向后兼容** - 不影响现有非滚动测试

### 范围界定

| 包含 (Phase 1) | 不包含 (Phase 2) |
|----------------|------------------|
| ✅ 垂直滚动支持 | ❌ 水平滚动 |
| ✅ 单容器滚动 | ❌ 嵌套滚动 |
| ✅ 累积模式元素可见性 | ❌ 故障注入（延迟、无响应） |
| ✅ 元素去重 | ❌ 步长自适应 |
| ✅ HasScroll/IsEndOfList 计算 | ❌ 滚动跳跃检测与回滚 |
| ✅ ScrollHandler (5-step pipeline) | ❌ 滚动决策在 TraversalEngine 主流程 |

**Note**: ScrollHandler 提供 5-step pipeline 决策能力，但 Phase 1 中仅在 Simulation 测试场景中使用，不集成到 TraversalEngine 主流程中。Phase 2 将考虑将滚动决策集成到 TraversalFSM.HandleBranch 中。

### 成功标准

- ✅ 仿真测试能完整遍历 3 屏列表（9个元素）
- ✅ 滚动到底检测正确（`IsEndOfList`）
- ✅ `HasScroll` 计算正确（是否还有更多内容）
- ✅ 元素去重生效（同一 ID 只返回一个）
- ✅ 现有测试无需修改即可运行

---

## 核心概念

### 滚动进度 (Scroll Progress)

归一化的 0.0-1.0 值，表示当前滚动位置：
- `0.0` = 列表顶部
- `1.0` = 列表底部
- 进度通过滚动操作累积增加/减少

### 滚动片段 (Scroll Segment)

按阈值分段的元素集合：
```csharp
public sealed record ScrollSegment(
    double Threshold,           // 激活阈值 (0.0-1.0)
    ImmutableArray<PageElement> Elements  // 该片段的元素
);
```

### 累积模式 (Accumulation Mode)

**核心规则**: 所有 `Threshold <= CurrentProgress` 的片段元素都可见。

```
CurrentProgress = 0.5:
  Segment0 (Threshold=0.0) → 可见 (0.0 <= 0.5) ✓
  Segment1 (Threshold=0.5) → 可见 (0.5 <= 0.5) ✓
  Segment2 (Threshold=1.0) → 不可见 (1.0 > 0.5) ✗
```

### 元素去重 (Element Deduplication)

当同一元素 ID 在多个片段中出现时，只返回一个（低 threshold 优先）：

```csharp
// Segment0 (threshold=0.0): wifi_switch
// Segment1 (threshold=0.5): wifi_switch (重复)
// 结果: 只返回 Segment0 的 wifi_switch
```

---

## 滚动处理逻辑

### 1. 滚动状态管理

#### ScrollState 结构

```csharp
public sealed record ScrollState(
    double CurrentProgress,      // 当前滚动进度 0.0-1.0
    int ScrollCount,             // 滚动操作次数
    ImmutableArray<double> ScrollHistory  // 历史进度值
);
```

#### 状态初始化

```csharp
// 每个页面独立的滚动状态
private readonly Dictionary<string, ScrollState> _scrollStates = new();

ScrollState GetOrCreateScrollState(string pageId)
{
    if (!_scrollStates.ContainsKey(pageId))
    {
        _scrollStates[pageId] = new ScrollState(
            CurrentProgress: 0.0,
            ScrollCount: 0,
            ScrollHistory: ImmutableArray<double>.Empty
        );
    }
    return _scrollStates[pageId];
}
```

### 2. 滚动进度更新

#### SimulateScroll 实现

```csharp
double SimulateScroll(string pageId, double delta)
{
    var state = GetOrCreateScrollState(pageId);

    // 计算新进度（clamped to [0.0, 1.0]）
    double oldProgress = state.CurrentProgress;
    double newProgress = Math.Clamp(oldProgress + delta, 0.0, 1.0);

    // 更新状态
    _scrollStates[pageId] = state with
    {
        CurrentProgress = newProgress,
        ScrollCount = state.ScrollCount + 1,
        ScrollHistory = state.ScrollHistory.Add(newProgress)
    };

    return newProgress;
}
```

#### 进度边界处理

| 场景 | 输入 | 输出 | 说明 |
|------|------|------|------|
| 正常滚动 | progress=0.0, delta=0.3 | 0.3 | 正常累加 |
| 超出上界 | progress=0.9, delta=0.2 | 1.0 | Clamp 到 1.0 |
| 超出下界 | progress=0.1, delta=-0.2 | 0.0 | Clamp 到 0.0 |
| 向上滚动 | progress=0.5, delta=-0.1 | 0.4 | 减少 |

### 3. 可见元素收集

#### 累积模式核心逻辑

```csharp
ImmutableArray<PageElement> GetVisibleElements(
    ImmutableArray<ScrollSegment> segments,
    double progress)
{
    // 使用 Dictionary 去重（低 threshold 优先）
    var elementMap = new Dictionary<string, PageElement>();

    // 按 threshold 升序遍历
    foreach (var segment in segments.OrderBy(s => s.Threshold))
    {
        // 累积模式：threshold <= progress 时元素可见
        if (segment.Threshold <= progress)
        {
            foreach (var element in segment.Elements)
            {
                // 去重：同 ID 只保留第一个（低 threshold）
                if (!elementMap.ContainsKey(element.Id))
                {
                    elementMap[element.Id] = element;
                }
            }
        }
    }

    return elementMap.Values.ToImmutableArray();
}
```

#### 可见性示例

```
Segments:
  Segment0 (threshold=0.0): [A, B]
  Segment1 (threshold=0.5): [C, D]
  Segment2 (threshold=1.0): [E]

progress=0.0 → [A, B]
progress=0.5 → [A, B, C, D]  // 累积：Segment0 + Segment1
progress=1.0 → [A, B, C, D, E]  // 累积：所有片段
```

### 4. HasScroll 计算

#### 逻辑定义

```csharp
bool CalculateHasScroll(ImmutableArray<ScrollSegment> segments, double progress)
{
    // 有片段且存在未到达的 threshold
    if (segments.IsEmpty) return false;
    return segments.Any(s => s.Threshold > progress);
}
```

#### 计算示例

| segments | progress | HasScroll | 原因 |
|----------|----------|-----------|------|
| [0.0, 0.5, 1.0] | 0.0 | ✅ true | 0.5, 1.0 未到达 |
| [0.0, 0.5, 1.0] | 0.5 | ✅ true | 1.0 未到达 |
| [0.0, 0.5, 1.0] | 1.0 | ❌ false | 全部到达 |
| [] | 任意 | ❌ false | 无片段 |

### 5. IsEndOfList 计算

#### 逻辑定义

```csharp
bool CalculateIsEndOfList(ImmutableArray<ScrollSegment> segments, double progress)
{
    // 到达或超过最大 threshold
    if (segments.IsEmpty) return false;
    double maxThreshold = segments.Max(s => s.Threshold);
    return progress >= maxThreshold;
}
```

#### 计算示例

| segments | progress | IsEndOfList | 原因 |
|----------|----------|-------------|------|
| [0.0, 0.5, 1.0] | 0.5 | ❌ false | 0.5 < 1.0 |
| [0.0, 0.5, 1.0] | 1.0 | ✅ true | 1.0 >= 1.0 |
| [] | 任意 | ❌ false | 无片段 |

### 6. 滚动决策逻辑

#### 遍历引擎中的判断

```csharp
// 访问完所有可见元素后
var analysis = await _vision.AnalyzeCurrentPageAsync();

if (analysis.HasScroll && !analysis.IsEndOfList)
{
    // 还有内容未显示，执行滚动
    await _action.ScrollDown(stepPercent: 0.3);
    // 重新分析页面，继续访问
}
else
{
    // 列表已到底或不可滚动，页面完成
    // 进入下一步
}
```

#### 决策表

| HasScroll | IsEndOfList | 动作 |
|-----------|-------------|------|
| true | false | 继续滚动 |
| false | true | 停止（到底） |
| false | false | 停止（不可滚动） |
| true | true | ❌ 不应出现（矛盾状态） |

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

### PageState 扩展

```csharp
// 扩展 PageState 支持 ScrollSegment 存储
public sealed record PageState(
    string PageName,
    ImmutableArray<PageElement> Elements,
    bool IsComplete = false,
    ImmutableArray<ScrollSegment> ScrollSegments = default  // 新增
)
{
    public ImmutableArray<ScrollSegment> ScrollSegments { get; init; } =
        ScrollSegments.IsDefaultOrEmpty
            ? ImmutableArray<ScrollSegment>.Empty
            : ScrollSegments;
}
```

---

## 架构设计

### 组件关系

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
│  │  - GetScrollCount(): 查询滚动次数                     │  │
│  └──────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                    基类层 (已有)                              │
│  StatefulMockVisionService / StatefulMockActionExecutor     │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                    数据层                                     │
│  ScrollDataStore + ScrollSegment + ScrollState               │
└─────────────────────────────────────────────────────────────┘
```

### 继承关系

```mermaid
classDiagram
    StatefulMockVisionService <|-- ScrollableMockVisionService
    StatefulMockActionExecutor <|-- ScrollableMockActionExecutor

    StatefulMockVisionService: +_current_page_id: string
    StatefulMockVisionService: +AnalyzeCurrentPageAsync()
    StatefulMockVisionService: +SimulateAction()

    ScrollableMockVisionService: +_scrollStates: Dictionary
    ScrollableMockVisionService: +_dataStore: ScrollDataStore
    ScrollableMockVisionService: +SimulateScroll(delta)
    ScrollableMockVisionService: +GetVisibleElements()
    ScrollableMockVisionService: +CalculateIsEndOfList()
    ScrollableMockVisionService: +CalculateHasScroll()

    StatefulMockActionExecutor: +execute(context)
    StateableMockActionExecutor: +history: List

    ScrollableMockActionExecutor: +_scrollActions: List
    ScrollableMockActionExecutor: +ScrollDown(step)
    ScrollableMockActionExecutor: +ScrollUp(step)
    ScrollableMockActionExecutor: +GetScrollCount()
```

---

## 接口设计

### ScrollableMockVisionService

```csharp
public sealed class ScrollableMockVisionService : StatefulMockVisionService
{
    private readonly Dictionary<string, ScrollState> _scrollStates = new();
    private readonly ScrollDataStore _dataStore;

    public ScrollableMockVisionService(StateFixture fixture)
        : base(fixture)
    {
        _dataStore = new ScrollDataStore();
        // 从 fixture 提取 ScrollSegment 数据
        InitializeScrollSegments(fixture);
    }

    // 核心 API
    public override Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        string pageId = CurrentPageId;
        ScrollState state = GetOrCreateScrollState(pageId);

        // 获取滚动片段
        var segments = _dataStore.GetScrollSegments(pageId);

        // 累积模式收集可见元素
        var visibleElements = GetVisibleElements(segments, state.CurrentProgress);

        // 计算状态
        bool hasScroll = CalculateHasScroll(segments, state.CurrentProgress);
        bool isEndOfList = CalculateIsEndOfList(segments, state.CurrentProgress);

        // 构建 PageAnalysis
        return Task.FromResult<PageAnalysis?>(BuildPageAnalysis(
            pageId, visibleElements, hasScroll, isEndOfList));
    }

    public double SimulateScroll(double delta)
    {
        string pageId = CurrentPageId;
        return UpdateScrollProgress(pageId, delta);
    }

    public double GetScrollProgress(string pageId)
    {
        return _scrollStates.TryGetValue(pageId, out var state)
            ? state.CurrentProgress
            : 0.0;
    }

    // 内部方法
    private ScrollState GetOrCreateScrollState(string pageId) { }
    private ImmutableArray<PageElement> GetVisibleElements(
        ImmutableArray<ScrollSegment> segments, double progress) { }
    private bool CalculateHasScroll(
        ImmutableArray<ScrollSegment> segments, double progress) { }
    private bool CalculateIsEndOfList(
        ImmutableArray<ScrollSegment> segments, double progress) { }
    private double UpdateScrollProgress(string pageId, double delta) { }
}
```

### ScrollableMockActionExecutor

```csharp
public sealed class ScrollableMockActionExecutor : StatefulMockActionExecutor
{
    private readonly ScrollableMockVisionService _scrollableVision;
    private readonly List<ScrollAction> _scrollActions = new();

    public ScrollableMockActionExecutor(ScrollableMockVisionService vision)
        : base(vision)
    {
        _scrollableVision = vision;
    }

    public bool ScrollDown(double stepPercent = 0.3)
    {
        double before = _scrollableVision.GetScrollProgress(_scrollableVision.CurrentPageId);
        double after = _scrollableVision.SimulateScroll(stepPercent);

        RecordScrollAction("SCROLL_DOWN", stepPercent, before, after);
        return true;
    }

    public bool ScrollUp(double stepPercent = 0.1)
    {
        double before = _scrollableVision.GetScrollProgress(_scrollableVision.CurrentPageId);
        double after = _scrollableVision.SimulateScroll(-stepPercent);

        RecordScrollAction("SCROLL_UP", stepPercent, before, after);
        return true;
    }

    public int GetScrollCount() => _scrollActions.Count;

    private void RecordScrollAction(
        string action, double step, double before, double after)
    {
        _scrollActions.Add(new ScrollAction(
            action, step, before, after, DateTimeOffset.UtcNow));
    }
}
```

---

## ScrollHandler 设计

### 概述

**ScrollHandler** 遵循项目的 Handler Pipeline 模式（→ `patterns/handler-pipeline.md`），提供 5-step 滚动决策流程：

```
detect → classify → decide → execute → statistics
```

与其他 Handler 的对比：

| Aspect | PopupHandler | ContainerHandler | **ScrollHandler** |
|--------|-------------|------------------|-------------------|
| **Pipeline steps** | 6 (with preserve/restore/validate) | 3 | 5 |
| **触发时机** | ResultVerify 检测到弹窗 | Branch 容器完成检测 | Branch 访问完所有子节点后 |
| **输入类型** | `string popupText` | `CompletionContext` | `ScrollContext` |
| **Detector 输出** | `PopupType` (5 values) | `CompletionResult` | `Scrollability` (3 values) |
| **Classifier 输出** | `PopupClassification` (5 fields) | (无独立分类器) | `ScrollDecision` (4 fields) |
| **Decider 输出** | `DismissStrategy` | `FallbackAction` | `ScrollAction` (3 values) |
| **Dispatch key enum** | `PopupType` | `FallbackAction` | `ScrollActionType` |
| **Statistics** | Detected/Handled/Rate | (无) | ScrollCount/TotalDistance |

### 1. Scrollability 检测 (Detect)

#### Scrollability 枚举

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
```

#### ScrollabilityDetector

```csharp
/// <summary>检测页面是否可滚动 - 纯函数，无副作用</summary>
public sealed class ScrollabilityDetector
{
    /// <summary>检测页面可滚动性</summary>
    /// <param name="pageId">当前页面 ID</param>
    /// <param name="hasScroll">PageAnalysis.HasScroll</param>
    /// <param name="isEndOfList">PageAnalysis.IsEndOfList</param>
    /// <param name="currentProgress">当前滚动进度</param>
    /// <param name="scrollDataStore">ScrollDataStore 实例</param>
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

#### 检测优先级链

| 优先级 | 条件 | 输出 | 说明 |
|--------|------|------|------|
| 1 | `!HasScrollData(pageId)` | `NotScrollable` | 无滚动片段数据 |
| 2 | `IsEndOfList` | `AtBottom` | 已到达列表底部 |
| 3 | `HasScroll` | `CanScrollDown` | 有更多内容可显示 |
| 4 | `CurrentProgress > 0.0` | `CanScrollUp` | 可向上滚动 |
| 5 | (其他) | `NotScrollable` | 默认不可滚动 |

### 2. ScrollDecision 分类 (Classify)

#### ScrollDecision record

```csharp
/// <summary>滚动决策结果 - 4 字段分类</summary>
public sealed record ScrollDecision(
    Scrollability Scrollability,        // 检测结果
    double CurrentProgress,              // 当前进度
    double MaxProgress,                  // 最大进度（最大 threshold）
    double RecommendedStep);             // 推荐步长（默认 0.3）
```

#### ScrollClassifier

```csharp
/// <summary>滚动分类器 - 4-submethod 顺序执行</summary>
public sealed class ScrollClassifier
{
    /// <summary>分类滚动状态 - 4 sub-methods</summary>
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
        // 默认步长 30%
        const double defaultStep = 0.3;

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

### 3. ScrollAction 决策 (Decide)

#### ScrollActionType 枚举

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
```

#### ScrollDecider

```csharp
/// <summary>滚动决策器 - 将 ScrollDecision 映射到 ScrollActionType</summary>
public sealed class ScrollDecider
{
    /// <summary>决定滚动动作 - 优先级链</summary>
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

#### 决策表

| Scrollability | Decision | StepPercent | 说明 |
|---------------|----------|-------------|------|
| `CanScrollDown` | `ScrollDown` | `Min(0.3, MaxProgress - CurrentProgress)` | 不超过剩余距离 |
| `CanScrollUp` | `ScrollUp` | `Min(0.3, CurrentProgress)` | 不超过当前距离 |
| `AtBottom` | `None` | 0.0 | 已到底，不滚动 |
| `NotScrollable` | `None` | 0.0 | 非滚动页面 |

### 4. ScrollAction 执行 (Execute)

#### ScrollContext

```csharp
/// <summary>滚动执行上下文</summary>
public sealed record ScrollContext(
    ScrollDecision Decision,
    ScrollActionType ActionType,
    double StepPercent,
    ITraversalContext TraversalContext);
```

#### ScrollActionResult

```csharp
/// <summary>滚动执行结果</summary>
public sealed record ScrollActionResult(
    ScrollActionType Action,
    bool Success,
    double NewProgress,
    string Description);
```

#### ScrollActionExecutor

```csharp
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

    /// <summary>执行滚动动作 - Hook Dispatch + 异常兜底</summary>
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
            // 异常兜底：返回失败结果
            return new ScrollActionResult(
                actionType, false, ctx.Decision.CurrentProgress,
                $"Exception during scroll execution: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ScrollActionResult DefaultScrollDown(ScrollContext ctx)
    {
        // 从 TraversalContext 获取 ScrollableMockActionExecutor
        var executor = ctx.TraversalContext.ActionExecutor as ScrollableMockActionExecutor;
        if (executor == null)
        {
            return new ScrollActionResult(
                ScrollActionType.ScrollDown, false, ctx.Decision.CurrentProgress,
                "ActionExecutor is not ScrollableMockActionExecutor");
        }

        double before = ctx.Decision.CurrentProgress;
        bool success = executor.ScrollDown(ctx.StepPercent);

        // 获取滚动后进度（需要从 vision service 查询）
        var vision = ctx.TraversalContext.VisionProvider as ScrollableMockVisionService;
        double after = vision?.GetScrollProgress(vision.CurrentPageId) ?? before;

        return new ScrollActionResult(ScrollActionType.ScrollDown, success, after,
            $"Scrolled down from {before:F2} to {after:F2}");
    }

    private static ScrollActionResult DefaultScrollUp(ScrollContext ctx)
    {
        var executor = ctx.TraversalContext.ActionExecutor as ScrollableMockActionExecutor;
        if (executor == null)
        {
            return new ScrollActionResult(
                ScrollActionType.ScrollUp, false, ctx.Decision.CurrentProgress,
                "ActionExecutor is not ScrollableMockActionExecutor");
        }

        double before = ctx.Decision.CurrentProgress;
        bool success = executor.ScrollUp(ctx.StepPercent);

        var vision = ctx.TraversalContext.VisionProvider as ScrollableMockVisionService;
        double after = vision?.GetScrollProgress(vision.CurrentPageId) ?? before;

        return new ScrollActionResult(ScrollActionType.ScrollUp, success, after,
            $"Scrolled up from {before:F2} to {after:F2}");
    }

    private static ScrollActionResult DefaultNone(ScrollContext ctx)
        => new ScrollActionResult(ScrollActionType.None, true, ctx.Decision.CurrentProgress,
            "No scroll action needed");
}
```

### 5. ScrollHandler 管道编排

#### ScrollHandlerStatistics

```csharp
/// <summary>滚动处理统计</summary>
public sealed record class ScrollHandlerStatistics(
    int ScrolledCount,        // 执行滚动次数
    int SkippedCount,         // 跳过次数（AtBottom/NotScrollable）
    double TotalDistance);    // 总滚动距离（向下为正，向上为负）
```

#### ScrollHandler

```csharp
/// <summary>
/// ScrollHandler — 5-step handle_scroll() 流程:
/// detect → classify → decide → execute → statistics
/// </summary>
public sealed class ScrollHandler
{
    private readonly ScrollabilityDetector _detector = new();
    private readonly ScrollClassifier _classifier = new();
    private readonly ScrollDecider _decider = new();
    private readonly ScrollActionExecutor _executor;

    private int _scrolledCount;
    private int _skippedCount;
    private double _totalDistance;

    public ScrollHandler(ScrollActionExecutor? executor = null)
    {
        _executor = executor ?? new ScrollActionExecutor();
    }

    /// <summary>
    /// 5-step handle_scroll() 流程 — 严格顺序执行
    /// </summary>
    public ScrollActionResult HandleScroll(
        PageAnalysis analysis,
        double currentProgress,
        ITraversalContext traversalContext)
    {
        // Step 1: detect
        var scrollability = _detector.Detect(
            traversalContext.CurrentPageId,
            analysis.HasScroll,
            analysis.IsEndOfList,
            currentProgress,
            GetScrollDataStore(traversalContext));

        if (scrollability == Scrollability.NotScrollable)
        {
            _skippedCount++;
            return new ScrollActionResult(
                ScrollActionType.None, true, currentProgress,
                "Page is not scrollable");
        }

        // Step 2: classify
        var segments = GetScrollSegments(traversalContext);
        var decision = _classifier.Classify(
            scrollability, analysis, currentProgress, segments);

        // Step 3: decide
        var actionType = _decider.Decide(decision);

        if (actionType == ScrollActionType.None)
        {
            _skippedCount++;
            return new ScrollActionResult(
                ScrollActionType.None, true, currentProgress,
                $"No scroll needed: {scrollability}");
        }

        // Step 4: execute
        var scrollContext = new ScrollContext(
            decision, actionType, decision.RecommendedStep, traversalContext);
        var result = _executor.Execute(actionType, scrollContext);

        // Step 5: statistics
        if (result.Success)
        {
            _scrolledCount++;
            _totalDistance += actionType == ScrollActionType.ScrollDown
                ? result.NewProgress - currentProgress
                : currentProgress - result.NewProgress;
        }

        return result;
    }

    /// <summary>获取统计信息</summary>
    public ScrollHandlerStatistics GetStatistics()
        => new ScrollHandlerStatistics(_scrolledCount, _skippedCount, _totalDistance);

    // 辅助方法
    private ScrollDataStore GetScrollDataStore(ITraversalContext context)
    {
        // 从 TraversalContext 获取 ScrollDataStore
        // 具体实现取决于 TraversalContext 的设计
        var vision = context.VisionProvider as ScrollableMockVisionService;
        return vision?.GetDataStore() ?? new ScrollDataStore();
    }

    private ImmutableArray<ScrollSegment> GetScrollSegments(ITraversalContext context)
    {
        var dataStore = GetScrollDataStore(context);
        return dataStore.GetScrollSegments(context.CurrentPageId);
    }
}
```

### 6. FSM 集成

#### 在 TraversalFSM 中的调用位置

```csharp
// TraversalFSM.HandleBranch() 中添加滚动检查
private TraversalState HandleBranch()
{
    var context = Context;
    var node = context.CurrentFrame?.Node;
    
    // ... 现有的分支逻辑 ...
    
    // 访问完所有子节点后，检查是否需要滚动
    if (allChildrenVisited && IsScrollablePage(context))
    {
        var analysis = context.VisionProvider.AnalyzeCurrentPageAsync();
        if (analysis.Result.HasScroll && !analysis.Result.IsEndOfList)
        {
            // 调用 ScrollHandler
            var scrollHandler = new ScrollHandler();
            var scrollResult = scrollHandler.HandleScroll(
                analysis.Result,
                GetScrollProgress(context),
                context);
            
            if (scrollResult.Success)
            {
                // 滚动成功，返回 NodeSelect 继续访问新出现的元素
                return TraversalState.NodeSelect;
            }
        }
    }
    
    // 原有的 FrameComplete 逻辑
    return TraversalState.FrameComplete;
}
```

### 7. 完整流程图

```
┌─────────────────────────────────────────────────────────────┐
│                    TraversalFSM.HandleBranch                 │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
                ┌───────────────────────┐
                │ 所有子节点已访问?      │
                └───────────────────────┘
                     │              │
                    Yes             No
                     │              │
                     ▼              ▼
        ┌───────────────────────┐   继续分支选择
        │ 是可滚动页面?          │
        └───────────────────────┘
             │           │
            Yes          No
             │           │
             ▼           ▼
    ┌────────────────────────┐  直接进入 FrameComplete
    │   ScrollHandler        │
    │   (5-step pipeline)    │
    └────────────────────────┘
             │
     ┌───────┴────────┐
     │                │
  ScrollDown         None
     │                │
     ▼                ▼
┌─────────┐    ┌─────────────┐
│NodeSelect│    │FrameComplete│
└─────────┘    └─────────────┘
```

---

## 测试场景

### 场景 1: 正常多屏滚动

**输入**: 3 个片段，5 个元素
```csharp
ScrollSegments(
    (0.0, s => s.Element("net1").Element("net2")),
    (0.5, s => s.Element("net3").Element("net4")),
    (1.0, s => s.Element("net5"))
)
```

**预期**:
- progress=0.0: [net1, net2]
- progress=0.5: [net1, net2, net3, net4]
- progress=1.0: [net1, net2, net3, net4, net5]
- 所有 5 个元素被访问
- IsEndOfList 在 progress=1.0 时为 true

### 场景 2: 滚动到底检测

**输入**: 2 个片段
```csharp
ScrollSegments(
    (0.0, s => s.Element("net1")),
    (1.0, s => s.Element("net2"))
)
```

**预期**:
- progress=0.0: HasScroll=true, IsEndOfList=false
- progress=1.0: HasScroll=false, IsEndOfList=true

### 场景 3: 元素去重

**输入**: 重复 ID
```csharp
ScrollSegments(
    (0.0, s => s.Element("wifi_switch")),
    (0.5, s => s.Element("wifi_switch"))  // 重复
)
```

**预期**: wifi_switch 只被访问一次（来自 threshold=0.0）

### 场景 4: 空列表

**输入**: 空片段
```csharp
ScrollSegments(
    (0.0, s => { })  // 无元素
)
```

**预期**: 快速退出，不进入死循环

### 场景 5: 单屏列表

**输入**: 单片段
```csharp
ScrollSegments(
    (0.0, s => s.Element("net1").Element("net2"))
)
```

**预期**: 不执行滚动操作，scroll_count=0

---

## 实施计划

### Phase 1: 数据模型 (Task 1.1-1.7)

```
src/UniClaw.Core/Simulation/Scroll/
  ├── ScrollSegment.cs          # sealed record (Threshold + Elements)
  ├── ScrollState.cs            # sealed record (CurrentProgress + ScrollCount + History)
  ├── ScrollAction.cs           # sealed record (Action + StepPercent + Before/After + Timestamp)
  └── ScrollDataStore.cs        # 存储和查询 ScrollSegment 数据

tests/UniClaw.Core.Tests/Simulation/Scroll/
  ├── ScrollSegmentTests.cs
  ├── ScrollStateTests.cs
  └── ScrollActionTests.cs
```

### Phase 2: StateFixtureBuilder 扩展 (Task 2.1-2.6)

```csharp
// 新增 ScrollSegmentBuilder
public sealed class ScrollSegmentBuilder
{
    private readonly double _threshold;
    private readonly List<PageElement> _elements = new();

    internal ScrollSegmentBuilder(double threshold) => _threshold = threshold;

    public ScrollSegmentBuilder Element(string id, string type = "button", string text = "", double x = 0.5, double y = 0.5)
    {
        _elements.Add(new PageElement(id, type, text, x, y));
        return this;
    }

    public ScrollSegment Build() => new(_threshold, _elements.ToImmutableArray());
}

// PageStateBuilder 扩展
public PageStateBuilder ScrollSegments(
    params (double threshold, Action<ScrollSegmentBuilder> configure)[] segments)
{
    var builtSegments = new List<ScrollSegment>();
    foreach ((var threshold, var configure) in segments)
    {
        var builder = new ScrollSegmentBuilder(threshold);
        configure(builder);
        builtSegments.Add(builder.Build());
    }
    _scrollSegments = builtSegments.ToImmutableArray();
    return this;
}
```

### Phase 3: ScrollableMockVisionService (Task 3.1-3.9)

```csharp
public sealed class ScrollableMockVisionService : StatefulMockVisionService
{
    // 1. 滚动状态管理
    private readonly Dictionary<string, ScrollState> _scrollStates;

    // 2. 累积模式元素收集
    private ImmutableArray<PageElement> GetVisibleElements(...)

    // 3. IsEndOfList 计算
    private bool CalculateIsEndOfList(...)

    // 4. HasScroll 计算
    private bool CalculateHasScroll(...)

    // 5. 模拟滚动
    public double SimulateScroll(double delta)

    // 6. 重写 AnalyzeCurrentPageAsync
    public override Task<PageAnalysis?> AnalyzeCurrentPageAsync(...)
}
```

### Phase 4: 测试 (Task 4.1-4.7)

- 累积模式测试
- 元素去重测试
- IsEndOfList 计算测试
- HasScroll 计算测试
- 进度 clamping 测试

### Phase 5: ScrollableMockActionExecutor (Task 5.1-5.8)

```csharp
// src/UniClaw.Core/Simulation/ScrollableMockActionExecutor.cs
public sealed class ScrollableMockActionExecutor : StatefulMockActionExecutor
{
    private readonly ScrollableMockVisionService _scrollableVision;
    private readonly List<ScrollAction> _scrollActions = new();

    public ScrollableMockActionExecutor(ScrollableMockVisionService vision)
        : base(vision)
    {
        _scrollableVision = vision;
    }

    public bool ScrollDown(double stepPercent = 0.3) { }
    public bool ScrollUp(double stepPercent = 0.1) { }
    public int GetScrollCount() => _scrollActions.Count;
    private void RecordScrollAction(...) { }
}
```

### Phase 6: ScrollHandler 实现 (新增)

```csharp
// src/UniClaw.Core/StateMachine/ScrollabilityDetector.cs
// src/UniClaw.Core/StateMachine/ScrollClassifier.cs
// src/UniClaw.Core/StateMachine/ScrollDecider.cs
// src/UniClaw.Core/StateMachine/ScrollActionExecutor.cs
// src/UniClaw.Core/StateMachine/ScrollHandler.cs

public enum Scrollability { NotScrollable, CanScrollDown, AtBottom, CanScrollUp }
public enum ScrollActionType { None, ScrollDown, ScrollUp }

public sealed record ScrollDecision(
    Scrollability Scrollability,
    double CurrentProgress,
    double MaxProgress,
    double RecommendedStep);

public sealed record ScrollContext(
    ScrollDecision Decision,
    ScrollActionType ActionType,
    double StepPercent,
    ITraversalContext TraversalContext);

public sealed record ScrollActionResult(
    ScrollActionType Action,
    bool Success,
    double NewProgress,
    string Description);

public sealed class ScrollHandler
{
    public ScrollActionResult HandleScroll(
        PageAnalysis analysis,
        double currentProgress,
        ITraversalContext traversalContext) { }
    
    public ScrollHandlerStatistics GetStatistics() { }
}
```

**测试文件**:
```
tests/UniClaw.Core.Tests/StateMachine/
  ├── ScrollabilityDetectorTests.cs
  ├── ScrollClassifierTests.cs
  ├── ScrollDeciderTests.cs
  ├── ScrollActionExecutorTests.cs
  └── ScrollHandlerTests.cs
```

### Phase 7: 场景测试 (Task 6.1-6.6)

### Phase 8: 文档更新 (原 Task 7.1-7.3)

### Phase 9: 验证与归档 (原 Task 8.1-8.2)

---

## Python 对齐说明

### 完全对齐部分

| Python | C# | 说明 |
|--------|-----|------|
| `ScrollSegment` | `ScrollSegment` | Threshold + Elements |
| `ScrollState` | `ScrollState` | CurrentProgress + ScrollCount + History |
| 累积模式 | 累积模式 | threshold <= progress |
| 元素去重 | 元素去重 | 低 threshold 优先 |
| `IsEndOfList` | `IsEndOfList` | progress >= max_threshold |
| `HasScroll` | `HasScroll` | any(threshold > progress) |

### C# 特有设计

| C# 特有 | Python 对应 | 原因 |
|---------|------------|------|
| `sealed record class` | `@dataclass` | C# 不可变设计模式 |
| `ImmutableArray<T>` | `List[T]` | C# 不可变集合 |
| `StateFixtureBuilder` 扩展 | YAML/JSON fixture | C# Fluent Builder 模式 |
| `DomainValidationException` | ValueError | C# fail-fast 校验模式 |

### Phase 2 延迟内容

以下功能在 Python 中有实现，C# 延迟到 Phase 2：
- 故障注入（延迟、无响应模拟）
- 步长自适应
- 跳跃检测与回滚

---

## 验收标准

### 功能验收

- ✅ 所有基础场景测试通过（5个场景）
- ✅ 累积模式元素可见性正确
- ✅ 元素去重生效
- ✅ IsEndOfList 计算正确
- ✅ HasScroll 计算正确
- ✅ 进度 clamping 正确（0.0-1.0 边界）

### 代码质量

- ✅ 通过 `dotnet build` 零错误
- ✅ 通过 `dotnet test` 所有测试通过
- ✅ 使用 `sealed record class` + `ImmutableArray`
- ✅ 遵循 C# 命名约定

### 兼容性

- ✅ 现有 `StatefulMockVisionService` 测试通过
- ✅ 现有 `StatefulMockActionExecutor` 测试通过
- ✅ 现有仿真测试通过

---

## 附录

### A. 关键设计决策

| 决策 | C# 选择 | Python 对应 | 说明 |
|------|---------|------------|------|
| 数据模型 | sealed record class | @dataclass | C# 不可变设计 |
| 集合类型 | ImmutableArray | List | C# 不可变集合 |
| Builder 模式 | StateFixtureBuilder 扩展 | YAML/JSON fixture | C# Fluent API |
| 校验方式 | DomainValidationException | ValueError | C# fail-fast |
| 进度范围 | [0.0, 1.0] double | [0.0, 1.0] float | 一致 |

### B. 术语对照

| Python | C# | 说明 |
|--------|-----|------|
| `ScrollSegment` | `ScrollSegment` | 滚动片段 |
| `ScrollState` | `ScrollState` | 滚动状态 |
| `ScrollAction` | `ScrollAction` | 滚动动作记录 |
| `ScrollDataStore` | `ScrollDataStore` | 数据存储 |
| `path_key` | `pageId` | 页面标识符 |
| `virtual_pages` | `StateFixture.Pages` | 页面数据 |

### C. 参考文档

- Python PRD: docs/prd/PRD_V7_0_SimScroll.md
- Python 设计: docs/prd/DESIGN_V7_0_SimScroll.md
- OpenSpec Change: openspec/changes/simulation-scroll-enhancement/
- C# Simulation 层: docs/system/layers/simulation.md
- C# 基线测试: docs/system/layers/simulation-baseline.md

---

**文档所有者**: UniClaw.Core C# 迁移项目
**状态**: 设计阶段
**最后更新**: 2026-07-12
**版本**: 1.1
