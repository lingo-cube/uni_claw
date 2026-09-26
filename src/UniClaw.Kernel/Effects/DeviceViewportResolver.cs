namespace UniClaw.Kernel.Effects;

/// <summary>viewport 解析来源（CSC-002 Slice A）。</summary>
internal enum ViewportSource
{
    /// <summary>live device 实测（wm size）。</summary>
    LiveDevice,

    /// <summary>显式已验证配置（transient fallback，不缓存）。</summary>
    ExplicitValidatedConfig,
}

/// <summary>
/// 解析结果：空间 + 来源 + 会话身份。会话身份 = serial + resolver 实例 id
/// （实例生命周期 = device session 生命周期；新实例 = 新 session，缓存与
/// config 均不跨实例存活）。
/// </summary>
internal sealed record ResolvedViewport(
    UniClaw.Kernel.Perception.CoordinateSpace Space,
    ViewportSource Source,
    string DeviceSessionIdentity);

/// <summary>
/// CSC-002 Slice A/B/C/D：**唯一** viewport resolver——全仓 viewport
/// resolution policy 只存在于此（live device &gt; validated explicit config
/// &gt; unresolved），Host/Capture/EffectDriver 不得各自判断。
/// cache 语义（session-scoped，无 TTL/lease）：
/// - live 查询成功 → 缓存（Source=LiveDevice），同 session 多 dispatch
///   复用（V1/V2：零重复查询）；
/// - **失效条件（全部机械）**：① 新实例（serial/session 变——per-run
///   构造）② ObserveTransportFailure（adb 进程失败形态 → 保守失效；假
///   失效无害 = 多一次查询）③ capture evidence 与 cached space 不
///   Matches（Slice D：capture 只触发失效，**不写缓存**——capture ≠
///   device authority）④ config 变 = 新实例（ctor 固定；文档化）。
/// - 禁 TTL、禁永久缓存、禁 background polling。
/// config 边界（Slice C）：live available → live wins（V6）；live
/// transient 失败 + config 存在 → fallback（**不缓存**——下次仍先试
/// live，V5）；capture evidence 与 config 冲突 → resolver 照常返回
/// config，由调用侧 Matches 检查落 zero effect（config 不压倒证据，
/// V7）。
/// internal test seam（ControlBeliefView 先例）。
/// </summary>
internal sealed class DeviceViewportResolver
{
    private readonly string _serial;
    private readonly UniClaw.Kernel.Perception.CoordinateSpace? _explicitConfig;
    private readonly Func<UniClaw.Kernel.Perception.CoordinateSpace?> _liveQuery;
    private readonly string _sessionIdentity;
    private ResolvedViewport? _cached;

    internal DeviceViewportResolver(
        string serial,
        UniClaw.Kernel.Perception.CoordinateSpace? explicitConfig,
        Func<UniClaw.Kernel.Perception.CoordinateSpace?> liveQuery)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            throw new ArgumentException("device serial required", nameof(serial));
        }

        _serial = serial;
        _explicitConfig = explicitConfig;   // 已由 CoordinateSpace 构造执法（dims>0/成对由调用侧保证）
        _liveQuery = liveQuery ?? throw new ArgumentNullException(nameof(liveQuery));
        _sessionIdentity = $"{serial}#session-{Guid.NewGuid():N}";
    }

    /// <summary>测试观察位：live 查询次数（V1/V3 断言输入）。</summary>
    internal int LiveQueryCount { get; private set; }

    /// <summary>当前会话身份（serial + 实例 id）。</summary>
    internal string SessionIdentity => _sessionIdentity;

    /// <summary>
    /// 解析当前 viewport。captureEvidence（grounding 携带的 capture 空间）
    /// 只用作失效信号（Slice D）；与 cached 冲突 → invalidate → fresh live。
    /// </summary>
    internal ResolvedViewport? Resolve(UniClaw.Kernel.Perception.CoordinateSpace? captureEvidence)
    {
        if (_cached is { } cached
            && captureEvidence is { } evidence
            && !cached.Space.Matches(evidence))
        {
            Invalidate(); // capture 尺寸/rotation 冲突 → 设备可能已变 → 重新实测
        }

        if (_cached is { } valid)
        {
            return valid;
        }

        LiveQueryCount++;
        if (_liveQuery() is { } live)
        {
            return _cached = new ResolvedViewport(live, ViewportSource.LiveDevice, _sessionIdentity);
        }

        return _explicitConfig is { } config
            ? new ResolvedViewport(config, ViewportSource.ExplicitValidatedConfig, _sessionIdentity)
            : null;
    }

    /// <summary>显式失效（下一 Resolve 重新实测）。</summary>
    internal void Invalidate() => _cached = null;

    /// <summary>
    /// transport 失败形态的保守失效（Started=false / 超时 / ExitCode≠0）。
    /// 保守方向：假失效只多一次 live 查询，无正确性代价。
    /// </summary>
    internal void ObserveTransportFailure() => _cached = null;
}
