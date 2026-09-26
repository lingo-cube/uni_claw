using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Effects;

/// <summary>
/// CSC-002 V1–V9：唯一 resolver 的 session cache / 失效条件 / config 边界。
/// liveQuery delegate 注入 + LiveQueryCount 观察——零 adb。
/// </summary>
public class DeviceViewportResolverTests
{
    private static CoordinateSpace Space(int w, int h) =>
        CoordinateSpace.DeviceViewport(w, h);

    private sealed class Live
    {
        public int Calls;
        private readonly Func<CoordinateSpace?> _impl;

        public Live(Func<CoordinateSpace?> impl) => _impl = impl;

        public CoordinateSpace? Invoke()
        {
            Calls++;
            return _impl();
        }
    }

    private static DeviceViewportResolver Resolver(
        Live live, CoordinateSpace? config = null, string serial = "emulator-5554") =>
        new(serial, config, live.Invoke);

    // V1 session start → 一次查询 → 多次 resolve 零重复
    [Fact]
    public void V1_SessionStart_QueriesOnce_MultipleResolvesReuseCache()
    {
        var live = new Live(() => Space(1080, 1920));
        var resolver = Resolver(live);

        var first = resolver.Resolve(Space(1080, 1920));
        for (var i = 0; i < 10; i++)
        {
            var again = resolver.Resolve(Space(1080, 1920));
            Assert.Same(first, again);
        }

        Assert.Equal(1, live.Calls);
        Assert.Equal(ViewportSource.LiveDevice, first!.Source);
    }

    // V2 同 session + matching captures → cache 保持
    [Fact]
    public void V2_MatchingCaptures_CacheStaysValid_NoExtraQuery()
    {
        var live = new Live(() => Space(1080, 1920));
        var resolver = Resolver(live);
        resolver.Resolve(Space(1080, 1920));
        resolver.Resolve(CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-2"));
        resolver.Resolve(null);

        Assert.Equal(1, live.Calls);
    }

    // V3 新 capture 尺寸不同 → 缓存失效 → 重新实测
    [Fact]
    public void V3_ConflictingCapture_InvalidatesCache_FreshQuery()
    {
        var current = Space(1080, 1920);
        var live = new Live(() => current);
        var resolver = Resolver(live);

        resolver.Resolve(Space(1080, 1920));          // attach：query #1
        current = Space(1080, 2400);                  // 设备真实变化
        var after = resolver.Resolve(Space(1080, 2400)); // capture 冲突 → query #2

        Assert.Equal(2, live.Calls);
        Assert.Equal("device-viewport:1080x2400@0", after!.Space.CoordinateSpaceId);
        Assert.Equal(ViewportSource.LiveDevice, after.Source);
    }

    // V4 新 session（新实例）→ 旧缓存不可继承
    [Fact]
    public void V4_NewSessionInstance_NoInheritedCache()
    {
        var live = new Live(() => Space(1080, 1920));
        var sessionA = Resolver(live);
        sessionA.Resolve(null);
        Assert.Equal(1, live.Calls);

        var sessionB = Resolver(live);                 // 新实例 = 新 session
        sessionB.Resolve(null);

        Assert.Equal(2, live.Calls);                   // B 独立查询
        Assert.NotEqual(sessionA.SessionIdentity, sessionB.SessionIdentity);
    }

    // V5 live 瞬时失败 + config + 同 session + 证据不冲突 → 合法 fallback（不缓存）
    [Fact]
    public void V5_TransientLiveFailure_ConfigFallback_NotCached()
    {
        var available = false;
        var live = new Live(() => available ? Space(1080, 1920) : null);
        var resolver = Resolver(live, config: Space(1080, 1920));

        var fallback = resolver.Resolve(Space(1080, 1920));
        Assert.Equal(ViewportSource.ExplicitValidatedConfig, fallback!.Source);

        available = true;                              // live 恢复
        var recovered = resolver.Resolve(Space(1080, 1920));
        Assert.Equal(ViewportSource.LiveDevice, recovered!.Source); // fallback 未被缓存
        Assert.Equal(2, live.Calls);
    }

    // V6 live 与 config 冲突 → live wins
    [Fact]
    public void V6_LiveWinsOverConfig()
    {
        var live = new Live(() => Space(1080, 1920));
        var resolver = Resolver(live, config: Space(1080, 2400));

        var resolved = resolver.Resolve(null);

        Assert.Equal("device-viewport:1080x1920@0", resolved!.Space.CoordinateSpaceId);
        Assert.Equal(ViewportSource.LiveDevice, resolved.Source);
    }

    // V7 config 与新 capture 冲突（live 不可用）→ 返回 config，由调用侧
    // Matches 落 zero effect（resolver 不压倒证据，也不偷偷改判）
    [Fact]
    public void V7_ConfigConflictsWithCapture_ReturnsConfig_CallerRejects()
    {
        var live = new Live(() => null);
        var resolver = Resolver(live, config: Space(1080, 2400));

        var resolved = resolver.Resolve(Space(1080, 1920)); // capture 证据 1920

        Assert.Equal("device-viewport:1080x2400@0", resolved!.Space.CoordinateSpaceId);
        Assert.False(Space(1080, 1920).Matches(resolved.Space)); // 调用侧据此 zero effect
    }

    // V8 前一设备的 config/cache 不能跨 session 存活（构造期绑定）
    [Fact]
    public void V8_PreviousDeviceState_CannotSurviveNewSession()
    {
        var live = new Live(() => Space(1080, 1920));
        var deviceA = Resolver(live, config: Space(1080, 2400), serial: "device-A");
        deviceA.Resolve(null); // A 缓存 + A config

        var deviceB = new DeviceViewportResolver("device-B", explicitConfig: null, liveQuery: live.Invoke);
        var resolved = deviceB.Resolve(null);

        Assert.Equal(ViewportSource.LiveDevice, resolved!.Source); // B 不见 A 的 config fallback
        Assert.Contains("device-B", deviceB.SessionIdentity);
    }

    // V9 无 live + 无 config → unresolved（fail-closed 输入）
    [Fact]
    public void V9_NoLiveNoConfig_Unresolved()
    {
        var live = new Live(() => null);
        var resolver = Resolver(live);

        Assert.Null(resolver.Resolve(null));
    }

    // transport 失败 → 保守失效 → 下次重新实测
    [Fact]
    public void TransportFailure_Invalidates_NextResolveRequeries()
    {
        var live = new Live(() => Space(1080, 1920));
        var resolver = Resolver(live);
        resolver.Resolve(null);
        resolver.ObserveTransportFailure();

        resolver.Resolve(null);

        Assert.Equal(2, live.Calls);
    }

    // 显式 Invalidate 同理
    [Fact]
    public void ExplicitInvalidate_FreshQueryNext()
    {
        var live = new Live(() => Space(1080, 1920));
        var resolver = Resolver(live);
        resolver.Resolve(null);
        resolver.Invalidate();
        resolver.Resolve(null);
        Assert.Equal(2, live.Calls);
    }
}
