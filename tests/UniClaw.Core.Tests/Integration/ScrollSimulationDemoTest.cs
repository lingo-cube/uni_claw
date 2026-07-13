using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.Traversal;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Core.Tests.Integration;

/// <summary>
/// 滚动仿真演示测试 — 直接演示滚动效果的工作原理。
///
/// 这个测试直接操作滚动组件，用于验证滚动逻辑的正确性。
/// </summary>
public class ScrollSimulationDemoTest
{
    private readonly ITestOutputHelper _output;

    public ScrollSimulationDemoTest(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 演示：滚动如何改变元素可见性
    /// </summary>
    [Fact]
    public void Demo_ScrollChangesElementVisibility()
    {
        // Arrange
        var (fixture, scrollData) = CreateProductListFixture();
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var action = new ScrollableMockActionExecutor(vision);

        _output.WriteLine("=== Initial State ===");
        var initialAnalysis = vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
        _output.WriteLine($"Initial visible items: {initialAnalysis?.Items.Length ?? 0}");
        foreach (var item in initialAnalysis?.Items ?? [])
        {
            _output.WriteLine($"  - {item.Name}");
        }

        // Act: 执行多次滚动，观察元素变化
        _output.WriteLine("\n=== Simulating Scrolls ===");

        int totalUniqueItems = 0;
        var seenItems = new HashSet<string>();

        for (int i = 0; i < 12; i++)
        {
            // 获取当前页面分析
            var pageAnalysis = vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
            var currentItems = pageAnalysis?.Items ?? [];

            // 统计新元素
            var newItems = currentItems.Where(item => seenItems.Add(item.Name ?? ""));
            totalUniqueItems += newItems.Count();

            _output.WriteLine($"\nStep {i + 1}: Progress = {vision.GetScrollProgress("product_list"):F2}");
            _output.WriteLine($"  Visible items: {currentItems.Length}");
            _output.WriteLine($"  New items: {newItems.Count()}");
            _output.WriteLine($"  Total unique: {totalUniqueItems}");

            // 执行滚动（如果未到底部）
            if (!vision.IsEndOfList)
            {
                var beforeProgress = vision.GetScrollProgress("product_list");
                action.ScrollDown(0.15); // 15% 步长
                var afterProgress = vision.GetScrollProgress("product_list");

                _output.WriteLine($"  📜 SCROLLED: {beforeProgress:F2} → {afterProgress:F2}");
            }
            else
            {
                _output.WriteLine($"  ✓ End of list reached");
                break;
            }
        }

        // Assert
        _output.WriteLine($"\n=== Final Results ===");
        _output.WriteLine($"Total unique items seen: {totalUniqueItems}");
        _output.WriteLine($"Total scroll operations: {action.GetScrollCount("")}");
        _output.WriteLine($"Final progress: {vision.GetScrollProgress("product_list"):F2}");

        // 验证看到了大部分元素
        Assert.True(totalUniqueItems >= 45, $"Should see at least 45 unique items, saw {totalUniqueItems}");

        // 验证执行了滚动（根据数据结构，约6次滚动）
        Assert.True(action.GetScrollCount("") >= 5, $"Should scroll at least 5 times, scrolled {action.GetScrollCount("")}");

        // 验证到达或接近底部
        var finalProgress = vision.GetScrollProgress("product_list");
        Assert.True(finalProgress >= 0.85, $"Should reach near bottom, got {finalProgress:F2}");
    }

    /// <summary>
    /// 演示：滚动统计的记录
    /// </summary>
    [Fact]
    public void Demo_ScrollStatisticsAreTracked()
    {
        // Arrange
        var (fixture, scrollData) = CreateProductListFixture();
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var action = new ScrollableMockActionExecutor(vision);

        // Act: 手动执行多次滚动
        int maxScrolls = 10;
        for (int i = 0; i < maxScrolls; i++)
        {
            if (vision.IsEndOfList)
                break;

            var step = 0.15; // 15% 步长
            action.ScrollDown(step);
        }

        // Assert
        _output.WriteLine("=== Scroll Statistics ===");
        _output.WriteLine($"Total scrolls: {action.ScrollHistory.Length}");

        foreach (var scroll in action.ScrollHistory)
        {
            _output.WriteLine($"  {scroll.Action}: {scroll.BeforeProgress:F2} → {scroll.AfterProgress:F2} (Δ{(scroll.AfterProgress - scroll.BeforeProgress):F2})");
        }

        _output.WriteLine($"\nScroll up count: {action.GetScrollUpCount()}");
        _output.WriteLine($"Total scroll count: {action.GetScrollCount("")}");

        Assert.InRange(action.ScrollHistory.Length, 5, maxScrolls);
        Assert.Equal(0, action.GetScrollUpCount()); // 没有向上滚动
    }

    /// <summary>
    /// 演示：滚动到顶部和底部的边界检测
    /// </summary>
    [Fact]
    public void Demo_ScrollBoundaries()
    {
        // Arrange
        var (fixture, scrollData) = CreateProductListFixture();
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var action = new ScrollableMockActionExecutor(vision);

        _output.WriteLine("=== Boundary Testing ===");

        // 测试：从顶部开始
        var initialProgress = vision.GetScrollProgress("product_list");
        _output.WriteLine($"Initial progress: {initialProgress:F2}");
        Assert.Equal(0.0, initialProgress);

        // 测试：滚动到超过底部
        _output.WriteLine("\nScrolling beyond bottom...");
        while (!vision.IsEndOfList)
        {
            action.ScrollDown(0.2);
            var progress = vision.GetScrollProgress("product_list");
            _output.WriteLine($"  Progress: {progress:F2}, IsEndOfList: {vision.IsEndOfList}");

            if (progress >= 1.0)
                break;
        }

        // 验证：不会超过 1.0 太多
        var finalProgress = vision.GetScrollProgress("product_list");
        _output.WriteLine($"\nFinal progress: {finalProgress:F2}");
        Assert.InRange(finalProgress, 0.0, 1.05);
        Assert.True(vision.IsEndOfList);
    }

    /// <summary>
    /// 演示：滚动感知组件的基本功能
    /// </summary>
    [Fact]
    public void Demo_ScrollAwareComponentsBasicFunctionality()
    {
        // Arrange
        var (fixture, scrollData) = CreateProductListFixture();
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var action = new ScrollableMockActionExecutor(vision);

        _output.WriteLine("=== Scroll-Aware Components Demo ===");

        // Act: 模拟遍历过程中的滚动
        int iterations = 0;
        int maxIterations = 20;
        var seenItems = new HashSet<string>();

        while (iterations < maxIterations)
        {
            iterations++;

            var progress = vision.GetScrollProgress("product_list");
            var pageAnalysis = vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
            var currentItems = pageAnalysis?.Items ?? [];

            // 统计新元素
            var newItems = currentItems.Where(item => seenItems.Add(item.Name ?? "")).Count();

            _output.WriteLine($"Iter {iterations}: Progress={progress:F2}, Visible={currentItems.Length}, New={newItems}, Scrolled={action.ScrollHistory.Length}");

            // 检查是否可以继续滚动
            if (vision.IsEndOfList)
            {
                _output.WriteLine($"✓ Reached end of list");
                break;
            }

            // 执行滚动
            action.ScrollDown(0.15);
        }

        // Assert
        _output.WriteLine($"\n=== Results ===");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Total scrolls: {action.GetScrollCount("")}");
        _output.WriteLine($"Final progress: {vision.GetScrollProgress("product_list"):F2}");

        Assert.True(action.GetScrollCount("") >= 5, $"Should scroll at least 5 times");
        Assert.InRange(vision.GetScrollProgress("product_list"), 0.85, 1.05);
    }

    // ══════════════════════════════════════════════════════════════════════

    private static (StateFixture fixture, ScrollDataStore scrollData) CreateProductListFixture()
    {
        var fixture = new StateFixtureBuilder()
            .Page("product_list", p => p.Name("Products"))
            .Build();

        var scrollData = CreateProductScrollData();
        return (fixture, scrollData);
    }

    private static ScrollDataStore CreateProductScrollData()
    {
        var segments = new List<(double threshold, ImmutableArray<MenuItem> items)>();
        var productIndex = 0;

        // 10 屏数据
        for (int screen = 0; screen < 10; screen++)
        {
            var threshold = screen / 10.0;
            var itemsInScreen = 5 + (screen % 3);
            var items = new List<MenuItem>();

            // 重叠元素（除了第一屏）
            if (screen > 0 && productIndex > 0)
            {
                items.Add(new MenuItem(
                    $"Product{productIndex - 1}",
                    new Coordinate(0.5, 0.05),
                    MenuItemType.Button));
            }

            // 当前屏元素
            for (int i = 0; i < itemsInScreen && productIndex < 50; i++)
            {
                items.Add(new MenuItem(
                    $"Product{productIndex}",
                    new Coordinate(0.1 + (i % 5) * 0.15, 0.1 + (i / 5) * 0.2),
                    MenuItemType.Button));
                productIndex++;
            }

            segments.Add((threshold, items.ToImmutableArray()));
        }

        var builder = ScrollDataStore.CreateBuilder();
        foreach (var (threshold, items) in segments)
        {
            builder.Add("product_list", new ScrollSegment(threshold, items));
        }

        return builder.Build();
    }
}
