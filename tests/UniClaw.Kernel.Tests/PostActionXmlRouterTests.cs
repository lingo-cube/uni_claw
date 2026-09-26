using UniClaw.Kernel.Control;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// PER-009 S7：事后验证 XML 路由四门（D9 收紧版）回归。
/// </summary>
public sealed class PostActionXmlRouterTests
{
    private static readonly DateTimeOffset Dispatch = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterDispatch = Dispatch.AddMilliseconds(500);

    private static TargetSpec StateTarget(CheckedState desired = CheckedState.Checked) =>
        new("switch", "primary", "toggle", desired);

    private static TargetSpec ClickTarget() =>
        new("button", null, "tap", null); // Click 型：无 DesiredState

    private static ConflictResolver.XmlAuthoritySnapshot Snapshot(
        bool identityUnique = true,
        DateTimeOffset? dumpTime = null,
        bool checkable = true,
        string checkedValue = "true") =>
        new("wifi_switch", identityUnique, dumpTime ?? AfterDispatch, checkable, checkedValue,
            Enabled: true, Selected: false, Focused: false);

    // ---- 四门全过 → XML 验证 ----

    [Fact]
    public void AllGatesPass_RoutesToXml()
    {
        var r = PostActionXmlRouter.Route(StateTarget(), Snapshot(), Dispatch, AfterDispatch);
        Assert.True(r.UseXml);
        Assert.Equal("on", r.ResolvedState);        // checked=true → on
        Assert.Equal("wifi_switch", r.XmlLocalId);
    }

    [Fact]
    public void TriStatePartial_RoutesToXml()
    {
        var r = PostActionXmlRouter.Route(StateTarget(), Snapshot(checkedValue: "partial"), Dispatch, AfterDispatch);
        Assert.True(r.UseXml);
        Assert.Equal("partial", r.ResolvedState);
    }

    // ---- 门④：时序（事后新鲜度 = 时序约束，非窗口）----

    [Fact]
    public void DumpBeforeDispatch_FallsBack()
    {
        var before = Dispatch.AddMilliseconds(-100);
        var r = PostActionXmlRouter.Route(StateTarget(), Snapshot(dumpTime: before), Dispatch, before);
        Assert.False(r.UseXml);
        Assert.Contains("时序门", r.Basis);
    }

    // ---- 门①：字段权威域 ----

    [Fact]
    public void ClickTarget_NoDesiredState_FallsBack()
    {
        var r = PostActionXmlRouter.Route(ClickTarget(), Snapshot(), Dispatch, AfterDispatch);
        Assert.False(r.UseXml);
        Assert.Contains("字段门", r.Basis);
    }

    // ---- 门②：IdentityMatched（防同名错配）----

    [Fact]
    public void NonUniqueIdentity_FallsBack()
    {
        // 双同名 Switch 场景：身份不唯一 → 假验证防线
        var r = PostActionXmlRouter.Route(StateTarget(), Snapshot(identityUnique: false), Dispatch, AfterDispatch);
        Assert.False(r.UseXml);
        Assert.Contains("身份门", r.Basis);
        Assert.Contains("错配", r.Basis);
    }

    // ---- 门③：PropertyValid ----

    [Fact]
    public void CheckableFalse_FallsBack()
    {
        var r = PostActionXmlRouter.Route(StateTarget(), Snapshot(checkable: false), Dispatch, AfterDispatch);
        Assert.False(r.UseXml);
        Assert.Contains("属性门", r.Basis);
        Assert.Contains("checkable", r.Basis);
    }

    // ---- 缺席 ----

    [Fact]
    public void NoSnapshot_FallsBack()
    {
        var r = PostActionXmlRouter.Route(StateTarget(), null, Dispatch, AfterDispatch);
        Assert.False(r.UseXml);
        Assert.Contains("缺席", r.Basis);
    }

    // ---- confidence 盲：路由不看视觉（结构性保证，同 D13）----

    [Fact]
    public void RouteResult_HasNoConfidenceField()
    {
        var type = typeof(PostActionXmlRouter.RouteResult);
        Assert.Empty(type.GetProperties().Where(p =>
            p.Name.Contains("confidence", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("score", StringComparison.OrdinalIgnoreCase)));
    }
}
