using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.LocalVision;

/// <summary>
/// VisionScreenStateProvider 单测 — 滚动状态委托给 PageAnalysis + 反射断言
/// 实现 IObservableScreenStateProvider (V8/V9) + RefreshAsync UIA 冗余场景 + null analysis 安全默认值。
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

    // ── 13.3 (V9): 反射断言 — 实现 IObservableScreenStateProvider ──

    [Fact(DisplayName = "V9: VisionScreenStateProvider implements IObservableScreenStateProvider")]
    public void Implements_IObservableScreenStateProvider()
    {
        Assert.True(typeof(IObservableScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider)));
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

    // ── RefreshAsync: Vision 主路径 + UIA 冗余 ──

    [Fact(DisplayName = "RefreshAsync returns Vision-derived scroll state")]
    public async Task RefreshAsync_VisionScrollState()
    {
        var provider = new VisionScreenStateProvider(
            () => new PageAnalysis(Direction.Left, Direction.Left, HasScroll: true, IsEndOfList: false));
        var result = await provider.RefreshAsync();
        Assert.True(result.Succeeded);
        Assert.True(result.HasScroll);
        Assert.False(result.IsEndOfList);
        Assert.Null(result.HierarchyXml);
    }

    [Fact(DisplayName = "RefreshAsync with UIA available includes hierarchy")]
    public async Task RefreshAsync_WithUia_IncludesHierarchy()
    {
        var uiaMock = new FakeObservableProvider(
            new ScreenStateResult(true, "uia", "<hierarchy/>", "fp1", false, true, null));
        var provider = new VisionScreenStateProvider(
            () => new PageAnalysis(Direction.Left, Direction.Left, HasScroll: true),
            uia: uiaMock);
        var result = await provider.RefreshAsync();
        Assert.True(result.Succeeded);
        Assert.True(result.HasScroll);
        Assert.Equal("<hierarchy/>", result.HierarchyXml);
        Assert.Equal("fp1", result.HierarchyFingerprint);
    }

    [Fact(DisplayName = "RefreshAsync with UIA failure still succeeds via Vision")]
    public async Task RefreshAsync_UiaFailure_VisionStillSucceeds()
    {
        var uiaMock = new FakeObservableProvider(throwOnRefresh: true);
        var provider = new VisionScreenStateProvider(
            () => new PageAnalysis(Direction.Left, Direction.Left, HasScroll: true),
            uia: uiaMock);
        var result = await provider.RefreshAsync();
        Assert.True(result.Succeeded);
        Assert.True(result.HasScroll);
        Assert.Null(result.HierarchyXml);
    }

    private sealed class FakeObservableProvider : IObservableScreenStateProvider
    {
        private readonly ScreenStateResult? _result;
        private readonly bool _throwOnRefresh;

        public FakeObservableProvider(ScreenStateResult? result = null, bool throwOnRefresh = false)
        {
            _result = result;
            _throwOnRefresh = throwOnRefresh;
        }

        public Task<ScreenStateResult> RefreshAsync(string? previousHierarchyXml = null,
            bool afterScroll = false, CancellationToken cancellationToken = default)
        {
            if (_throwOnRefresh) throw new InvalidOperationException("UIA failure");
            return Task.FromResult(_result!);
        }

        public bool HasScroll() => false;
        public double GetScrollProgress() => 0.0;
        public bool IsEndOfList() => false;
        public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
    }
}
