using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.LocalVision;

/// <summary>
/// VisionScreenStateProvider 单测 — 滚动状态委托给 PageAnalysis + 反射断言
/// 未实现 IObservableScreenStateProvider (V8/V9) + null analysis 安全默认值。
/// </summary>
public class VisionScreenStateProviderTests
{
    // ── 13.1 (V8): HasScroll() 委托给 PageAnalysis.HasScroll ──

    [Fact(DisplayName = "V8: HasScroll() 委托给 PageAnalysis.HasScroll")]
    public void HasScroll_DelegatesToPageAnalysis()
    {
        var provider = new VisionScreenStateProvider(
            () => new PageAnalysis(Direction.Left, Direction.Left, HasScroll: true));

        Assert.True(provider.HasScroll());
    }

    // ── 13.2: IsEndOfList() 委托给 PageAnalysis.IsEndOfList ──

    [Fact(DisplayName = "13.2: IsEndOfList() 委托给 PageAnalysis.IsEndOfList")]
    public void IsEndOfList_DelegatesToPageAnalysis()
    {
        var provider = new VisionScreenStateProvider(
            () => new PageAnalysis(Direction.Left, Direction.Left, IsEndOfList: false));

        Assert.False(provider.IsEndOfList());
    }

    // ── 13.3 (V9): 反射断言 — 未实现 IObservableScreenStateProvider ──

    [Fact(DisplayName = "V9: 反射断言 — VisionScreenStateProvider 未实现 IObservableScreenStateProvider")]
    public void DoesNotImplement_IObservableScreenStateProvider()
    {
        // InterceptionHandler 依赖此断言自动落入 AI seen-set 差分安全路径
        Assert.False(
            typeof(IObservableScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider)));
    }

    // ── null analysis 安全默认值 ──

    [Fact(DisplayName = "Null analysis → HasScroll=false, IsEndOfList=true, progress=0.0, config=null")]
    public void NullAnalysis_UsesSafeDefaults()
    {
        var provider = new VisionScreenStateProvider(() => null);

        Assert.False(provider.HasScroll());
        Assert.True(provider.IsEndOfList());
        Assert.Equal(0.0, provider.GetScrollProgress());
        Assert.Null(provider.GetScrollSwipeConfig());
    }
}
