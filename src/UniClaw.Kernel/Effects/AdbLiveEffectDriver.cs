using System.Diagnostics;

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
    private readonly int _viewportWidth;
    private readonly int _viewportHeight;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>生产构造（真实进程 runner）。</summary>
    public AdbLiveEffectDriver(
        string serial,
        int viewportWidth,
        int viewportHeight,
        string adbExecutable = "adb",
        Func<DateTimeOffset>? clock = null)
        : this(serial, viewportWidth, viewportHeight, null, adbExecutable, clock)
    {
    }

    /// <summary>测试构造（fake runner 注入；IAdbProcessRunner 是 internal seam）。</summary>
    internal AdbLiveEffectDriver(
        string serial,
        int viewportWidth,
        int viewportHeight,
        IAdbProcessRunner? runner,
        string adbExecutable = "adb",
        Func<DateTimeOffset>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException("Resolved device serial is required.", nameof(serial));
        if (viewportWidth <= 0 || viewportHeight <= 0)
            throw new ArgumentException("viewport 尺寸必须为正");
        _serial = serial;
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _runner = runner ?? new AdbProcessRunner();
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable) ? "adb" : adbExecutable;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public DispatchResult Deliver(DispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!AdbEffectDriver.TryBuildTap(request, _viewportWidth, _viewportHeight,
                out var x, out var y, out var deviceArgs, out var reason, out var detail))
            return Fail(request, reason!, detail!);

        var args = new List<string> { "-s", _serial };
        args.AddRange(deviceArgs);
        var command = $"{_adbExecutable} {string.Join(' ', args)}";

        // 同步等待（≤10s；债见类注释——异步化 = HD-1 第二阶段 buyer）
        var result = _runner
            .RunAsync(_adbExecutable, args, DispatchTimeout, CancellationToken.None)
            .GetAwaiter().GetResult();

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

    private DispatchResult Fail(DispatchRequest request, string reason, string detail) =>
        new(DispatchOutcome.DeliveryFailed,
            $"rejected: {request.EffectClass} → {request.Target.OccurrenceReference}",
            _clock(),
            Reason: $"{reason}: {detail}");
}
