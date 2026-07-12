using System.Collections.Immutable;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.StateMachine.Scroll;
using UniClaw.Core.Domain.Models.Content;
using Coordinate = UniClaw.Core.Domain.Models.Content.Coordinate;
using Xunit;

namespace UniClaw.Core.Tests.Simulation.Scroll;

/// <summary>
/// 滚动场景 E2E 测试
/// 覆盖基本、边界、元素、步长和跳跃场景
/// </summary>
public class ScrollScenarioTests
{
    #region Basic Scenarios

    /// <summary>
    /// 单屏场景：所有元素在初始可见，无需滚动
    /// </summary>
    [Fact]
    public async Task Scenario_SingleScreen_AllElementsVisibleInitially()
    {
        // Arrange: 创建单屏 fixture（所有元素在 threshold 0.0）
        var fixture = new StateFixtureBuilder()
            .Page("wifi_list", page => page
                .Name("WiFi List")
                .Button("wifi_switch", "WiFi", 0.1, 0.1)
                .Button("wifi_1", "Network1", 0.5, 0.3)
                .Button("wifi_2", "Network2", 0.5, 0.5))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("wifi_list",
                new ScrollSegment(0.0, ImmutableArray.Create(
                    new MenuItem("WiFi", new Coordinate(0.1, 0.1)),
                    new MenuItem("Network1", new Coordinate(0.5, 0.3)),
                    new MenuItem("Network2", new Coordinate(0.5, 0.5)))))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // Act: 分析当前页面
        var analysis = await vision.AnalyzeCurrentPageAsync();

        // Assert: 所有元素可见，无需滚动
        Assert.NotNull(analysis);
        Assert.True(analysis.HasScroll);
        Assert.Equal(3, analysis.Items.Length);
        // 当只有一个分段且 threshold=0.0 时，maxThreshold=0.0，等于当前进度，所以 IsEndOfList=true
        Assert.True(analysis.IsEndOfList);
    }

    /// <summary>
    /// 双屏场景：需要一次滚动才能看到所有元素
    /// </summary>
    [Fact]
    public async Task Scenario_DualScreen_ScrollRevealsMoreElements()
    {
        // Arrange: 创建双屏 fixture
        var fixture = new StateFixtureBuilder()
            .Page("wifi_list", page => page
                .Name("WiFi List"))
            .Build();

        // 第一屏元素 (0.0)
        var screen1 = ImmutableArray.Create(
            new MenuItem("WiFi", new Coordinate(0.1, 0.1)),
            new MenuItem("Network1", new Coordinate(0.5, 0.3)),
            new MenuItem("Network2", new Coordinate(0.5, 0.5)));

        // 第二屏元素 (0.5)
        var screen2 = ImmutableArray.Create(
            new MenuItem("Network3", new Coordinate(0.5, 0.7)),
            new MenuItem("Network4", new Coordinate(0.5, 0.9)));

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("wifi_list",
                new ScrollSegment(0.0, screen1),
                new ScrollSegment(0.5, screen2))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // Act & Assert: 初始状态 - 只有第一屏
        var analysis1 = await vision.AnalyzeCurrentPageAsync();
        Assert.Equal(3, analysis1.Items.Length);
        Assert.Equal(0.0, vision.GetScrollProgress("wifi_list"));
        Assert.False(vision.IsEndOfList);

        // Act: 滚动 50%
        executor.ScrollDown(0.5);

        // Assert: 看到两屏元素（累积模式）
        var analysis2 = await vision.AnalyzeCurrentPageAsync();
        Assert.Equal(5, analysis2.Items.Length); // 3 + 2
        Assert.Equal(0.5, vision.GetScrollProgress("wifi_list"));
    }

    /// <summary>
    /// 多屏场景：多次滚动才能到达底部
    /// </summary>
    [Fact]
    public async Task Scenario_MultiScreen_MultipleScrollsToBottom()
    {
        // Arrange: 创建 4 屏场景
        var fixture = new StateFixtureBuilder()
            .Page("long_list", page => page.Name("Long List"))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("long_list",
                new ScrollSegment(0.0, CreateMenuItems("Item", 1, 3)),
                new ScrollSegment(0.25, CreateMenuItems("Item", 4, 6)),
                new ScrollSegment(0.5, CreateMenuItems("Item", 7, 9)),
                new ScrollSegment(0.75, CreateMenuItems("Item", 10, 12)),
                new ScrollSegment(1.0, CreateMenuItems("Item", 13, 13))) // 最后一个元素
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // Act: 逐屏滚动
        var progressHistory = new List<double>();

        for (int i = 0; i < 4; i++)
        {
            var analysis = await vision.AnalyzeCurrentPageAsync();
            progressHistory.Add(vision.GetScrollProgress("long_list"));
            Assert.False(vision.IsEndOfList, $"Should not be at bottom at scroll {i + 1}");
            executor.ScrollDown(0.25);
        }

        // Assert: 最后一次滚动后到达底部
        var finalAnalysis = await vision.AnalyzeCurrentPageAsync();
        Assert.True(vision.IsEndOfList);
        Assert.Equal(13, finalAnalysis.Items.Length); // 所有元素

        // 验证进度历史
        Assert.Equal(4, progressHistory.Count);
        Assert.Equal(0.0, progressHistory[0]);
        Assert.Equal(0.25, progressHistory[1]);
        Assert.Equal(0.5, progressHistory[2]);
        Assert.Equal(0.75, progressHistory[3]);
    }

    /// <summary>
    /// 空列表场景：没有元素
    /// </summary>
    [Fact]
    public async Task Scenario_EmptyList_NoElementsVisible()
    {
        // Arrange: 创建空列表
        var fixture = new StateFixtureBuilder()
            .Page("empty_list", page => page.Name("Empty List"))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("empty_list", ScrollSegment.Empty(0.0))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);

        // Act: 分析页面
        var analysis = await vision.AnalyzeCurrentPageAsync();

        // Assert: 无可见元素
        Assert.NotNull(analysis);
        Assert.Empty(analysis.Items);
        Assert.True(vision.HasScroll);
    }

    #endregion

    #region Boundary Scenarios

    /// <summary>
    /// 顶部边界：进度为 0，但还有更多内容
    /// </summary>
    [Fact]
    public async Task Scenario_BoundaryTop_ProgressIsZero()
    {
        var fixture = new StateFixtureBuilder()
            .Page("top_page", page => page.Name("Top"))
            .Build();

        // 需要至少两个分段，这样 max threshold > 0
        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("top_page",
                new ScrollSegment(0.0, ImmutableArray.Create(
                    new MenuItem("Item1", new Coordinate(0.5, 0.5)))),
                new ScrollSegment(1.0, ImmutableArray.Create(
                    new MenuItem("Item2", new Coordinate(0.5, 0.7)))))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);

        Assert.Equal(0.0, vision.GetScrollProgress("top_page"));
        Assert.False(vision.IsEndOfList); // max threshold 是 1.0，不是末尾
    }

    /// <summary>
    /// 底部边界：进度等于最大阈值
    /// </summary>
    [Fact]
    public async Task Scenario_BoundaryBottom_ProgressEqualsMaxThreshold()
    {
        var fixture = new StateFixtureBuilder()
            .Page("bottom_page", page => page.Name("Bottom"))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("bottom_page", new ScrollSegment(1.0, ImmutableArray.Create(
                new MenuItem("LastItem", new Coordinate(0.5, 0.5)))))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // 滚动到最大阈值
        executor.ScrollDown(1.0);

        Assert.Equal(1.0, vision.GetScrollProgress("bottom_page"));
        Assert.True(vision.IsEndOfList);
    }

    /// <summary>
    /// 接近底部：进度在 epsilon 范围内
    /// </summary>
    [Fact]
    public async Task Scenario_BoundaryNearBottom_WithinEpsilon()
    {
        var fixture = new StateFixtureBuilder()
            .Page("near_bottom", page => page.Name("Near Bottom"))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("near_bottom",
                new ScrollSegment(0.0, ImmutableArray.Create(new MenuItem("Item1", new Coordinate(0.5, 0.5)))),
                new ScrollSegment(1.0, ImmutableArray.Create(new MenuItem("Item2", new Coordinate(0.5, 0.7)))))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // 滚动到 0.9995（接近 1.0，在 epsilon 0.001 范围内）
        executor.ScrollDown(0.9995);

        // 在 epsilon 范围内应被视为到达底部
        // maxThreshold - progress = 1.0 - 0.9995 = 0.0005 < 0.001 (epsilon)
        var progress = vision.GetScrollProgress("near_bottom");
        Assert.InRange(progress, 0.999, 1.0); // Verify we're near the bottom

        // Check the actual epsilon comparison
        var config = ScrollHandlerConfig.Default();
        var maxThreshold = scrollData.GetMaxThreshold("near_bottom");
        var diff = maxThreshold - progress;
        Assert.True(diff <= config.ProgressEpsilon, $"Difference {diff} should be <= epsilon {config.ProgressEpsilon}");
    }

    /// <summary>
    /// 精确末尾：最后一屏只有少量元素
    /// </summary>
    [Fact]
    public async Task Scenario_BoundaryPreciseEnd_LastScreenHasFewElements()
    {
        var fixture = new StateFixtureBuilder()
            .Page("precise_end", page => page.Name("Precise End"))
            .Build();

        // 前面每屏 3 个元素，最后一屏只有 1 个
        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("precise_end",
                new ScrollSegment(0.0, CreateMenuItems("Item", 1, 3)),
                new ScrollSegment(0.5, CreateMenuItems("Item", 4, 6)),
                new ScrollSegment(1.0, CreateMenuItems("Item", 7, 7))) // 只有 1 个
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // 滚动到底部
        executor.ScrollDown(1.0);

        var analysis = await vision.AnalyzeCurrentPageAsync();
        Assert.Equal(7, analysis.Items.Length);
        Assert.True(vision.IsEndOfList);
    }

    #endregion

    #region Element Scenarios

    /// <summary>
    /// 元素去重：相同 ID 在多个分段只出现一次
    /// </summary>
    [Fact]
    public async Task Scenario_ElementDeduplication_SameIdAppearsOnce()
    {
        var fixture = new StateFixtureBuilder()
            .Page("dedup", page => page.Name("Dedup"))
            .Build();

        // WiFi 开关在所有分段都出现
        var wifiSwitch = new MenuItem("WiFi", new Coordinate(0.1, 0.1));

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("dedup",
                new ScrollSegment(0.0, ImmutableArray.Create(
                    wifiSwitch,
                    new MenuItem("Item1", new Coordinate(0.5, 0.3)))),
                new ScrollSegment(0.5, ImmutableArray.Create(
                    wifiSwitch, // 重复
                    new MenuItem("Item2", new Coordinate(0.5, 0.5)))),
                new ScrollSegment(1.0, ImmutableArray.Create(
                    wifiSwitch, // 重复
                    new MenuItem("Item3", new Coordinate(0.5, 0.7)))))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // 滚动到底部
        executor.ScrollDown(1.0);

        var analysis = await vision.AnalyzeCurrentPageAsync();

        // WiFi 开关只出现一次
        var wifiCount = analysis.Items.Count(item => item.Name == "WiFi");
        Assert.Equal(1, wifiCount);

        // 总共 4 个元素（WiFi + Item1 + Item2 + Item3）
        Assert.Equal(4, analysis.Items.Length);
    }

    /// <summary>
    /// 元素重复：相同 ID 优先使用最低阈值的实例
    /// </summary>
    [Fact]
    public async Task Scenario_ElementRepeat_LowestThresholdWins()
    {
        var fixture = new StateFixtureBuilder()
            .Page("repeat", page => page.Name("Repeat"))
            .Build();

        // 相同 ID，不同坐标
        var itemAt0 = new MenuItem("Header", new Coordinate(0.5, 0.1));
        var itemAt5 = new MenuItem("Header", new Coordinate(0.5, 0.6));

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("repeat",
                new ScrollSegment(0.0, ImmutableArray.Create(itemAt0)),
                new ScrollSegment(0.5, ImmutableArray.Create(itemAt5)))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        executor.ScrollDown(1.0);
        var analysis = await vision.AnalyzeCurrentPageAsync();

        // 只有一个 Header，使用 threshold 0.0 的坐标
        Assert.Equal(1, analysis.Items.Count(item => item.Name == "Header"));
        var header = analysis.Items.First(item => item.Name == "Header");
        Assert.Equal(0.1, header.Coordinate.Y); // 最低阈值的 Y 坐标
    }

    /// <summary>
    /// 动态变化：元素在不同分段显示不同内容
    /// </summary>
    [Fact]
    public async Task Scenario_ElementDynamicChange_ContentChangesAcrossSegments()
    {
        var fixture = new StateFixtureBuilder()
            .Page("dynamic", page => page.Name("Dynamic"))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("dynamic",
                new ScrollSegment(0.0, ImmutableArray.Create(
                    new MenuItem("Status: Loading", new Coordinate(0.5, 0.5)))),
                new ScrollSegment(0.5, ImmutableArray.Create(
                    new MenuItem("Status: Loaded", new Coordinate(0.5, 0.5)))))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // 初始状态
        var analysis1 = await vision.AnalyzeCurrentPageAsync();
        Assert.Contains(analysis1.Items, item => item.Name == "Status: Loading");

        // 滚动后
        executor.ScrollDown(0.5);
        var analysis2 = await vision.AnalyzeCurrentPageAsync();

        // 累积模式下两个都可见（没有去重，因为 ID 不同）
        Assert.Equal(2, analysis2.Items.Length);
    }

    #endregion

    #region Step Size Scenarios

    /// <summary>
    /// 小步长：需要多次滚动
    /// </summary>
    [Fact]
    public async Task Scenario_StepSizeSmall_MultipleScrollsRequired()
    {
        var fixture = new StateFixtureBuilder()
            .Page("small_step", page => page.Name("Small Step"))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("small_step",
                new ScrollSegment(0.0, CreateMenuItems("Item", 1, 3)),
                new ScrollSegment(1.0, CreateMenuItems("Item", 4, 6)))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // 使用小步长 0.1
        for (int i = 0; i < 10; i++)
        {
            executor.ScrollDown(0.1);
        }

        Assert.True(vision.IsEndOfList);
        Assert.Equal(10, executor.ScrollHistory.Length);
    }

    /// <summary>
    /// 默认步长：30%
    /// </summary>
    [Fact]
    public async Task Scenario_StepSizeDefault_ThirtyPercent()
    {
        var config = ScrollHandlerConfig.Default();
        Assert.Equal(0.3, config.DefaultScrollStep);
    }

    /// <summary>
    /// 大步长：一次滚动到底
    /// </summary>
    [Fact]
    public async Task Scenario_StepSizeLarge_SingleScrollToBottom()
    {
        var fixture = new StateFixtureBuilder()
            .Page("large_step", page => page.Name("Large Step"))
            .Build();

        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("large_step",
                new ScrollSegment(0.0, CreateMenuItems("Item", 1, 5)),
                new ScrollSegment(1.0, CreateMenuItems("Item", 6, 10)))
            .Build();

        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var executor = new ScrollableMockActionExecutor(vision);

        // 一次滚动到底
        executor.ScrollDown(1.0);

        Assert.True(vision.IsEndOfList);
        Assert.Single(executor.ScrollHistory);
    }

    /// <summary>
    /// 自适应步长：高重复率时增加步长
    /// </summary>
    [Fact]
    public void Scenario_StepSizeAdaptive_IncreasesOnHighDuplicateRatio()
    {
        var config = new ScrollHandlerConfig(
            DefaultScrollStep: 0.3,
            AdaptiveStepIncreaseThreshold: 0.7,
            MinSampleSize: 3);

        // 高重复率场景
        var verifyResult = new ScrollVerifyResult(
            Status: OverlapStatus.HasOverlap,
            BeforeElementIds: ImmutableArray.Create("A", "B", "C", "D"),
            AfterElementIds: ImmutableArray.Create("A", "B", "C", "D", "E"),
            OverlapCount: 4,
            NewElementCount: 5,
            DuplicateElementCount: 4,
            DuplicateRatio: 0.8); // 80% 重复

        var nextStep = AdaptiveStepCalculator.CalculateNextStep(0.3, verifyResult, config);

        // 步长应该增加到 0.3 * 1.5 = 0.45
        Assert.InRange(nextStep, 0.44, 0.46);
    }

    #endregion

    #region Jump Scenarios

    /// <summary>
    /// 正常滚动：有重叠元素
    /// </summary>
    [Fact]
    public void Scenario_JumpNormal_HasOverlap()
    {
        var before = ImmutableArray.Create("A", "B", "C");
        var after = ImmutableArray.Create("C", "D", "E");

        var result = JumpDetector.Detect(before, after);

        Assert.Equal(OverlapStatus.HasOverlap, result.Status);
        Assert.False(result.IsJumpDetected);
    }

    /// <summary>
    /// 跳跃检测：无重叠元素
    /// </summary>
    [Fact]
    public void Scenario_JumpDetection_NoOverlapBothHaveElements()
    {
        var before = ImmutableArray.Create("A", "B");
        var after = ImmutableArray.Create("C", "D");

        var result = JumpDetector.Detect(before, after);

        Assert.Equal(OverlapStatus.NoOverlap_BothHaveElements, result.Status);
        Assert.True(result.IsJumpDetected);
    }

    /// <summary>
    /// 跳跃恢复：回滚并重试
    /// </summary>
    [Fact]
    public void Scenario_JumpRecovery_RollbackAndRetry()
    {
        var config = new ScrollHandlerConfig(MaxJumpRetryCount: 3, JumpRecoveryFactor: 0.5);
        var recovery = new JumpRecoveryHandler(config);

        var executedSteps = new List<double>();
        int verifyCallCount = 0;

        ScrollActionResult ExecuteFunc(double step)
        {
            executedSteps.Add(step);
            return ScrollActionResult.Succeeded(ScrollActionType.ScrollDown, 0.0, "Success");
        }

        ScrollVerifyResult VerifyFunc()
        {
            verifyCallCount++;
            // 第一次调用返回跳跃（无重叠），后续返回有重叠（恢复成功）
            if (verifyCallCount == 1)
            {
                return new ScrollVerifyResult(
                    Status: OverlapStatus.NoOverlap_BothHaveElements,
                    BeforeElementIds: ImmutableArray.Create("A"),
                    AfterElementIds: ImmutableArray.Create("B"));
            }
            else
            {
                return new ScrollVerifyResult(
                    Status: OverlapStatus.HasOverlap,
                    BeforeElementIds: ImmutableArray.Create("A"),
                    AfterElementIds: ImmutableArray.Create("A", "B"));
            }
        }

        var result = recovery.Recover(0.0, 0.3, ExecuteFunc, VerifyFunc);

        Assert.True(result.Success);
        Assert.Equal(1, result.RetryCount); // 第一次重试成功
        Assert.InRange(result.FinalStep, 0.14, 0.16); // 0.3 * 0.5
        Assert.Single(executedSteps); // 只有一次重试执行（初始跳跃在 recovery 之外）
    }

    /// <summary>
    /// 跳跃恢复失败：超过最大重试次数
    /// </summary>
    [Fact]
    public void Scenario_JumpRecoveryFailure_ExceedsMaxRetries()
    {
        var config = new ScrollHandlerConfig(MaxJumpRetryCount: 2, JumpRecoveryFactor: 0.5);
        var recovery = new JumpRecoveryHandler(config);

        int attemptCount = 0;
        ScrollActionResult ExecuteFunc(double step)
        {
            attemptCount++;
            return ScrollActionResult.Succeeded(ScrollActionType.ScrollDown, 0.0, "Success");
        }

        // 始终检测到跳跃
        ScrollVerifyResult VerifyFunc() => new ScrollVerifyResult(
            Status: OverlapStatus.NoOverlap_BothHaveElements,
            BeforeElementIds: ImmutableArray.Create("A"),
            AfterElementIds: ImmutableArray.Create("B"));

        var result = recovery.Recover(0.0, 0.3, ExecuteFunc, VerifyFunc);

        Assert.False(result.Success);
        Assert.Equal(2, result.RetryCount);
    }

    #endregion

    #region Helper Methods

    private static ImmutableArray<MenuItem> CreateMenuItems(string prefix, int start, int end)
    {
        var builder = ImmutableArray.CreateBuilder<MenuItem>();
        for (int i = start; i <= end; i++)
        {
            // 使用 0.05 作为增量，确保坐标在 [0, 1] 范围内
            var y = 0.05 + (0.06 * (i - start));
            builder.Add(new MenuItem($"{prefix}{i}", new Coordinate(0.5, Math.Min(y, 0.95))));
        }
        return builder.ToImmutable();
    }

    #endregion
}
