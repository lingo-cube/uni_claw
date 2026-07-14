using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.Scroll;
using Xunit;

namespace UniClaw.Core.Tests.Simulation.Scroll;

/// <summary>
/// PagedItemGenerator / IScrollContentSource / ScrollBehaviorProfile / SimulatedScreen + 适配器联动单测
/// (任务 4.5 + 2.6)。验证动态分页内容源、行为 profile、以及 SwipeAsync→Analyze 联动一致。
/// </summary>
public class PagedContentAndScreenTests
{
    // ── PagedItemGenerator (4.5) ───────────────────────────────────────────

    [Fact(DisplayName = "PagedItemGenerator: GetPage 确定性 (纯函数, 重复调用相等)")]
    public void GetPage_IsDeterministic()
    {
        var gen = new PagedItemGenerator(totalCount: 30, pageSize: 8);
        var a = gen.GetPage(1);
        var b = gen.GetPage(1);
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.Equal(a[i].Name, b[i].Name);
            Assert.Equal(a[i].X, b[i].X);
            Assert.Equal(a[i].Y, b[i].Y);
        }
    }

    [Fact(DisplayName = "PagedItemGenerator: 末页不足 PageSize, 无填充")]
    public void GetPage_LastPagePartial_NoPadding()
    {
        var gen = new PagedItemGenerator(totalCount: 10, pageSize: 8, fillRatio: 1.0);
        Assert.Equal(8, gen.GetPage(0).Length);
        Assert.Equal(2, gen.GetPage(1).Length);  // 末页仅 2 项
        Assert.Empty(gen.GetPage(2));            // 超出末页 → 空
    }

    [Fact(DisplayName = "PagedItemGenerator: fillRatio 稀疏 vs 密集 (同页稀疏更少)")]
    public void GetPage_SparseVersusDense()
    {
        var dense = new PagedItemGenerator(totalCount: 100, pageSize: 10, fillRatio: 1.0);
        var sparse = new PagedItemGenerator(totalCount: 100, pageSize: 10, fillRatio: 0.5);
        Assert.True(dense.GetPage(0).Length > sparse.GetPage(0).Length);
        Assert.Equal(10, dense.GetPage(0).Length);
        Assert.Equal(5, sparse.GetPage(0).Length); // 50% 填充
    }

    [Fact(DisplayName = "PagedItemGenerator: TotalCount=null 无限流, 任意页满页")]
    public void GetPage_InfiniteTotal_AlwaysFullPage()
    {
        var gen = new PagedItemGenerator(totalCount: null, pageSize: 6);
        Assert.Null(gen.TotalCount);
        Assert.Equal(6, gen.GetPage(0).Length);
        Assert.Equal(6, gen.GetPage(1000).Length); // 流不结束
    }

    [Fact(DisplayName = "PagedItemGenerator: 非法参数 fail-fast")]
    public void PagedItemGenerator_InvalidArgs_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new PagedItemGenerator(10, pageSize: 0));
        Assert.Throws<DomainValidationException>(() => new PagedItemGenerator(10, 8, fillRatio: 1.5));
        Assert.Throws<DomainValidationException>(() => new PagedItemGenerator(totalCount: -1, 8));
    }

    // ── ScrollBehaviorProfile / ScrollJump (4.5) ───────────────────────────

    [Fact(DisplayName = "ScrollBehaviorProfile: 工厂语义 (Paged/WithCumulative/PagedWithJump)")]
    public void Profile_Factories()
    {
        Assert.False(ScrollBehaviorProfile.Paged.Cumulative);
        Assert.True(ScrollBehaviorProfile.WithCumulative().Cumulative);
        var jump = ScrollBehaviorProfile.PagedWithJump(ScrollJump.Overshoot(2.0));
        Assert.False(jump.Cumulative);
        Assert.Equal(2.0, jump.Jump.OvershootFactor);
    }

    [Fact(DisplayName = "ScrollJump: OvershootFactor<1 或 SkipPages<0 fail-fast")]
    public void ScrollJump_Invalid_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new ScrollJump(OvershootFactor: 0.5));
        Assert.Throws<DomainValidationException>(() => new ScrollJump(SkipPages: -1));
        Assert.Equal(1.0, ScrollJump.None.OvershootFactor);
    }

    // ── SimulatedScreen 可见性模型 (4.5) ───────────────────────────────────

    [Fact(DisplayName = "SimulatedScreen: Cumulative vs Windowed 产出不同 PageAnalysis")]
    public void Visibility_CumulativeVersusWindowed()
    {
        var fixture = PageShellFixture("list");
        var source = new PagedItemGenerator(totalCount: 24, pageSize: 8, fillRatio: 1.0, namePrefix: "N_");

        var cumulative = new SimulatedScreen(fixture, ScrollBehaviorProfile.WithCumulative())
            .WithScrollablePage("list", source);
        var windowed = new SimulatedScreen(fixture, ScrollBehaviorProfile.Paged)
            .WithScrollablePage("list", source);

        // 推进视口两页 (向下 swipe 两次)
        Advance(cumulative, 2);
        Advance(windowed, 2);

        int cumCount = Names(cumulative.GetPageAnalysis()).Count;
        int winCount = Names(windowed.GetPageAnalysis()).Count;

        Assert.True(cumCount > winCount);                // 累积可见 > 仅当前页
        Assert.Equal(8, winCount);                       // windowed 仅当前页 8 项
    }

    [Fact(DisplayName = "SimulatedScreen: Windowed+Jump 过冲跳页, 部分元素永不出现")]
    public void Visibility_WindowedWithJump_SkipsPages()
    {
        var fixture = PageShellFixture("list");
        var source = new PagedItemGenerator(totalCount: 40, pageSize: 8, fillRatio: 1.0, namePrefix: "N_");
        var jump = new SimulatedScreen(fixture, ScrollBehaviorProfile.PagedWithJump(ScrollJump.Overshoot(2.0)))
            .WithScrollablePage("list", source);

        // 单次过冲 swipe 应前进 > 1 页
        jump.ApplySwipe(0.5, 0.7, 0.5, 0.3);
        var after = Names(jump.GetPageAnalysis());
        // 过冲因子 2.0 → 前进 2 页, 视口到 page 2; page 1 的元素在 windowed 下不可见
        Assert.All(after, n => Assert.StartsWith("N_", n));
        Assert.True(after.Count == 8);                   // 仍单页 8 项, 但视口已跳到 page 2
        // page 1 首元素 N_8 不在当前窗口
        Assert.DoesNotContain("N_8", after);
    }

    // ── 适配器联动 (2.6) ───────────────────────────────────────────────────

    [Fact(DisplayName = "Adapter: SwipeAsync 后 AnalyzeCurrentPageAsync 反映新视口")]
    public void Adapter_SwipeThenAnalyze_ReflectsNewViewport()
    {
        var fixture = PageShellFixture("list");
        var source = new PagedItemGenerator(totalCount: 24, pageSize: 8, fillRatio: 1.0, namePrefix: "N_");
        var screen = new SimulatedScreen(fixture).WithScrollablePage("list", source);
        var vision = new ScrollableMockVisionService(screen);
        var action = new ScrollableMockActionExecutor(screen);

        var before = Names(vision.AnalyzeCurrentPageAsync().Result);
        Assert.Contains("N_0", before);

        action.SwipeAsync(0.5, 0.7, 0.5, 0.3, 300).Wait();   // 向下滚动一页

        var after = Names(vision.AnalyzeCurrentPageAsync().Result);
        Assert.NotEqual(before, after);
        Assert.Contains("N_8", after);                       // 第二页元素已可见 (累积模式? 否则 windowed page1)
    }

    [Fact(DisplayName = "Adapter: ScrollableMockActionExecutor 无 ScrollableMockVisionService 字段")]
    public void Adapter_NoVisionFieldOnActionExecutor()
    {
        var screen = new SimulatedScreen(PageShellFixture("list"))
            .WithScrollablePage("list", new PagedItemGenerator(8, 8));
        var action = new ScrollableMockActionExecutor(screen);

        var visionFieldTypes = action.GetType()
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(f => f.FieldType.Name)
            .ToArray();

        Assert.DoesNotContain("ScrollableMockVisionService", visionFieldTypes);
        Assert.Contains("SimulatedScreen", visionFieldTypes); // 仅持有共享 SimulatedScreen
    }

    [Fact(DisplayName = "Adapter: SwipeAsync 记录方向与进度差的 ActionRecord")]
    public void Adapter_SwipeRecordsDirectionAndProgress()
    {
        var fixture = PageShellFixture("list");
        var screen = new SimulatedScreen(fixture)
            .WithScrollablePage("list", new PagedItemGenerator(24, 8));
        var action = new ScrollableMockActionExecutor(screen);

        action.SwipeAsync(0.5, 0.7, 0.5, 0.3, 300).Wait();  // down

        var swipe = action.GetHistory().Single(r => r.Action == "swipe");
        Assert.Equal("down", swipe.Parameters["direction"]);
        Assert.True((double)swipe.Parameters["after_progress"] >= (double)swipe.Parameters["before_progress"]);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static StateFixture PageShellFixture(string pageId) => new StateFixtureBuilder()
        .Page(pageId, p => p.Name(pageId))
        .Build();

    private static void Advance(SimulatedScreen screen, int swipes)
    {
        for (int i = 0; i < swipes; i++)
            screen.ApplySwipe(0.5, 0.7, 0.5, 0.3);
    }

    private static IReadOnlyCollection<string> Names(PageAnalysis? analysis)
    {
        if (analysis == null) return Array.Empty<string>();
        return analysis.Items.Select(i => i.Name).ToList();
    }
}
