using System.Diagnostics;
using UniClaw.Kernel.Perception;

namespace UniClaw.Kernel.Effects;

/// <summary>
/// AdbLiveEffectDriver — ADB 真机执行 driver（ADB-001；当务之急方向：
/// ADB 优先，ego-browser 已完成协议扩展性验证使命）。与 AdbEffectDriver
/// （dry-run）共享 TryBuildTap（支持集与投影单一事实源）；本 driver 把
/// 构造出的命令交给真实 adb 进程执行，并执行**三态真实物理映射**——
/// legacy 三态实证（uni-agent:AdbDispatchTarget）× DSE-001 HD-1 裁决词汇：
///
/// ```text
/// 进程结果                → DispatchOutcome（认知状态）        reason
/// TimedOut（kill client）  → UnknownOutcome                    timeout-killed
///                            （设备侧效果可能已发生也可能未发生——
///                             唯一合法后继 re-observe，never blind redispatch）
/// !Started / ExitCode≠0    → DeliveryFailed                    transport
///                            （确定性失败：命令未送达设备执行）
/// ExitCode==0              → DeliveryCompleted（≠ world effect） —
/// ```
/// diagnostic（adb stderr / 失败原因）保留在 Reason，不进共享协议词汇。
///
/// 已知债（显式记录，非隐藏等待）：Deliver 同步阻塞等待进程（≤DispatchTimeout）
/// ——当前单线程语义环内行为正确（Control 环本就串行）；异步签名切换 =
/// HD-1 第二阶段 buyer（机械手/pending 状态机），无 buyer 不预建。
/// </summary>
public sealed class AdbLiveEffectDriver : IEffectDriver
{
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(10);

    private readonly string _serial;
    private readonly string _adbExecutable;
    private readonly IAdbProcessRunner _runner;
    private readonly DeviceViewportResolver _viewport;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// 生产构造（真实进程 runner）。viewport 参数（CSC-001 Slice B 起）为
    /// **可选显式已验证配置**（优先级最低：live device query > config）；
    /// null = 不配置——dispatch 前经 wm size 实测，实测不到即 fail-closed
    /// （zero effect + diagnostic），绝不静默落到 1080×2400 类魔数默认。
    /// </summary>
    public AdbLiveEffectDriver(
        string serial,
        int? viewportWidth = null,
        int? viewportHeight = null,
        string adbExecutable = "adb",
        Func<DateTimeOffset>? clock = null)
        : this(serial, viewportWidth, viewportHeight, null, adbExecutable, clock)
    {
    }

    /// <summary>测试构造（fake runner 注入；IAdbProcessRunner 是 internal seam）。</summary>
    internal AdbLiveEffectDriver(
        string serial,
        int? viewportWidth,
        int? viewportHeight,
        IAdbProcessRunner? runner,
        string adbExecutable = "adb",
        Func<DateTimeOffset>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException("Resolved device serial is required.", nameof(serial));
        if ((viewportWidth is null) != (viewportHeight is null))
            throw new ArgumentException("viewport 配置必须成对（CSC-001：不完整的配置 = 无效）");
        _serial = serial;
        _runner = runner ?? new AdbProcessRunner();
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable) ? "adb" : adbExecutable;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        // CSC-002：唯一 resolver（live > validated config > unresolved；
        // session-scoped cache + evidence-driven invalidation）。driver 只消费，
        // 不复制 resolution 规则。
        _viewport = new DeviceViewportResolver(
            serial,
            explicitConfig: viewportWidth is { } w && viewportHeight is { } h
                ? CoordinateSpace.DeviceViewport(w, h)
                : null,
            liveQuery: QueryLiveViewport);
    }

    /// <summary>live device 实测（wm size，Override 优先；CSC-002 起为 resolver 的 liveQuery）。</summary>
    private UniClaw.Kernel.Perception.CoordinateSpace? QueryLiveViewport()
    {
        var captured = _runner
            .RunCaptureAsync(_adbExecutable, new[] { "-s", _serial, "shell", "wm", "size" },
                TimeSpan.FromSeconds(5), CancellationToken.None)
            .GetAwaiter().GetResult();
        return captured is { Started: true, TimedOut: false, ExitCode: 0 } query
            && TryParseWmSize(System.Text.Encoding.UTF8.GetString(query.StandardOutput), out var width, out var height)
            ? CoordinateSpace.DeviceViewport(width, height)
            : null;
    }

    public DispatchResult Deliver(DispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // CSC-001 Slice C：先 driver 支持集（frozen 拒绝语义），后坐标空间链。
        if (!AdbEffectDriver.ValidateSupport(request, out var supportReason, out var supportDetail))
            return Fail(request, supportReason!, supportDetail!);

        // CSC-001 Slice C：grounded space（binding 携带，来自 capture 实测）
        // 必须与当前设备空间机械 Matches 才允许投影。
        //  - grounded space 缺席 = legacy/unknown → 不可验证 → fail-closed；
        //  - mismatch（rotation / viewport 变化 / 过期 capture）→ RE-GROUND /
        //    RE-OBSERVE，不 dispatch（禁止旧截图坐标 × 当前设备尺寸直接 tap）。
        var requestSpace = request.Target.Space;
        if (requestSpace is null)
        {
            return Fail(request, "coordinate-space-unknown-on-target",
                "grounding 未绑定 CoordinateSpace（claim 缺实测 w/h？）→ RE-OBSERVE，不 dispatch");
        }

        // CSC-002 Slice E：唯一 resolution 路径（resolver 内含 session cache
        // 与 evidence-driven invalidation；driver 零复制）。
        if (_viewport.Resolve(requestSpace) is not { } resolved)
        {
            return Fail(request, "coordinate-space-unresolved",
                $"无法确定设备 viewport（live 查询失败且无可用的显式已验证配置）；"
                + "拒绝投影归一化坐标（CSC-001：不静默使用默认值）");
        }

        if (!requestSpace.Matches(resolved.Space))
        {
            return Fail(request, "coordinate-space-mismatch",
                $"grounded space {requestSpace.CoordinateSpaceId} ≠ device {resolved.Space.CoordinateSpaceId}"
                + $"（source={resolved.Source}；rotation/viewport 变化或过期 capture）"
                + "→ RE-GROUND/RE-OBSERVE，不 dispatch");
        }

        if (!AdbEffectDriver.TryBuildTap(request, resolved.Space.PixelWidth, resolved.Space.PixelHeight,
                out var x, out var y, out var deviceArgs, out var reason, out var detail))
            return Fail(request, reason!, detail!);

        var args = new List<string> { "-s", _serial };
        args.AddRange(deviceArgs);
        var command = $"{_adbExecutable} {string.Join(' ', args)}";

        // 同步等待（≤10s；债见类注释——异步化 = HD-1 第二阶段 buyer）
        var result = _runner
            .RunAsync(_adbExecutable, args, DispatchTimeout, CancellationToken.None)
            .GetAwaiter().GetResult();

        // CSC-002 Slice B：transport 失败形态 → 保守失效 session cache
        // （假失效无害：下一 dispatch 多一次 live 查询）
        if (result is { Started: false } or { TimedOut: true } or { ExitCode: not 0 })
        {
            _viewport.ObserveTransportFailure();
        }

        if (result.TimedOut)
            return new DispatchResult(
                DispatchOutcome.UnknownOutcome,
                command,
                _clock(),
                Reason: $"timeout-killed: adb client 已被终止（{DispatchTimeout.TotalSeconds:0}s），设备侧效果未知——re-observe，永不盲补发");
        if (!result.Started || result.ExitCode != 0)
            return new DispatchResult(
                DispatchOutcome.DeliveryFailed,
                command,
                _clock(),
                Reason: $"transport: {(result.FailureReason ?? result.StandardError ?? "adb 进程失败").Trim()}");
        return new DispatchResult(
            DispatchOutcome.DeliveryCompleted,
            command,
            _clock(),
            Reason: null);   // 命令已送达执行 ≠ world effect（不变量 33/34）
    }

    internal static bool TryParseWmSize(string output, out int width, out int height)
    {
        width = height = 0;
        var overrideMatch = System.Text.RegularExpressions.Regex.Match(
            output, @"Override size:\s*(\d+)x(\d+)");
        if (overrideMatch.Success
            && int.TryParse(overrideMatch.Groups[1].Value, out width)
            && int.TryParse(overrideMatch.Groups[2].Value, out height)
            && width > 0 && height > 0)
        {
            return true;
        }

        var physicalMatch = System.Text.RegularExpressions.Regex.Match(
            output, @"Physical size:\s*(\d+)x(\d+)");
        return physicalMatch.Success
            && int.TryParse(physicalMatch.Groups[1].Value, out width)
            && int.TryParse(physicalMatch.Groups[2].Value, out height)
            && width > 0 && height > 0;
    }

    private DispatchResult Fail(DispatchRequest request, string reason, string detail) =>
        new(DispatchOutcome.DeliveryFailed,
            $"rejected: {request.EffectClass} → {request.Target.OccurrenceReference}",
            _clock(),
            Reason: $"{reason}: {detail}");
}
