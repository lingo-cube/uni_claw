using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;

namespace UniClaw.Host;

/// <summary>PER-019 — UniPerception fetch 结果（Host 侧形状；组合根从 adapter 桥适配）。
/// SemanticDisposition 非权威，只进诊断面。</summary>
public sealed record UniPerceptionFetchResult(
    Kernel.Perception.SlowConsultationStatus Status,
    bool Admitted,
    IReadOnlyList<(string Subject, string Value)> Proposals,
    string? SemanticDisposition = null,
    string? Diagnostic = null);

/// <summary>
/// PER-019 — UniPerception 异步流水（组件默认异步）：
/// Dispatch 非阻塞发射（Fast+XML 已先行入世界模型）；Poll 在驱动线程结算——
/// 晚到结果 IsLate 投影（producer uni.perception，lineage 带 basis 关联）、
/// 路由已变诚实丢弃（Unaligned）、超时零投影。预算在发射时计。
/// fetch 自带界（桥 CTS）；kernel 投影只发生在 Poll（单线程）。
/// </summary>
public sealed class UniPerceptionPipeline
{
    /// <summary>fetch 缝：门控+transport+解析，零 kernel 副作用（后台执行）。</summary>
    public delegate Task<UniPerceptionFetchResult> Fetch(
        SlowConsultationRequest request, string sessionCorrelation, CancellationToken cancellationToken);

    /// <summary>Poll 结算的投影回调（驱动线程；feed 提供，经 kernel.Process 走 P2）。
    /// 返回 Landed trace 令牌。</summary>
    public delegate string LandProjector(
        SlowConsultationRequest request, UniPerceptionFetchResult result);

    private sealed record Pending(
        SlowConsultationRequest Request,
        string RouteAtDispatch,
        Task<UniPerceptionFetchResult> Task,
        string TriggerToken,
        CancellationTokenSource CancellationTokenSource,
        // AGT-019 M3：发射时刻（ms 延迟分量基准）与 fetch 界（周期剩余预算基准）。
        long DispatchedAtTimestamp,
        TimeSpan FetchBound,
        // AGT-019 M2③/B5（Q3 裁决）：judgment 级来源标记（截图/视觉模型
        // 读出）——闸一：载荷强制标记，发射时声明，结算时执法。
        bool JudgmentLevel = false);

    private readonly Fetch _fetch;
    private readonly int _maxRequestsPerRun;
    private readonly List<Pending> _pending = new();
    private int _dispatched;

    public UniPerceptionPipeline(Fetch fetch, int maxRequestsPerRun)
    {
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _maxRequestsPerRun = maxRequestsPerRun;
    }

    public int Dispatched => _dispatched;
    public int InFlight => _pending.Count;
    public bool HasBudget => _dispatched < _maxRequestsPerRun;

    /// <summary>非阻塞发射。返回 Dispatched 令牌；预算尽/无效输入返回 Skipped 令牌。</summary>
    public string Dispatch(
        SlowConsultationRequest request, string sessionCorrelation,
        string routeKey, string triggerToken, TimeSpan? fetchBound = null,
        bool judgmentLevel = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HasBudget)
            return $"{triggerToken}|Skipped|budget-exhausted";
        if (_pending.Count >= Math.Max(1, _maxRequestsPerRun))
            return $"{triggerToken}|Skipped|in-flight-cap";
        _dispatched++;
        var bound = fetchBound ?? TimeSpan.FromSeconds(15);
        // fetch 本身异步（桥内部 CTS 界）；直接启动，不加 Task.Run——发射即返回，
        // 完成即刻可结算（无需跨线程拍）。外层再挂一道界，防 fetch 忽略 token。
        var cts = new CancellationTokenSource(bound);
        var task = _fetch(request, sessionCorrelation, cts.Token);
        _pending.Add(new Pending(request, routeKey, task, triggerToken, cts,
            System.Diagnostics.Stopwatch.GetTimestamp(), bound, judgmentLevel));
        // AGT-019 M3：令牌带 ms 延迟分量（发射即 0）。
        return $"{triggerToken}|Dispatched|in-flight={_pending.Count}|ms=0";
    }

    /// <summary>AGT-019 M3 — 材料不足的有界同步等待：在当前驱动线程等待本流水
    /// 全部 in-flight 至完成或预算耗尽，随后正常 Poll 结算。等待量 = 预算
    /// 期望（调用方传 BoundedWaitMs 同一常量），并被周期剩余预算封顶（最早
    /// fetch 界的剩余量）；提前完成即刻返回。未完成的 pending 原样保留
    /// （跨周期晚到语义不变）；wrong-screen（unaligned-route）门控在 Poll
    /// 内保持全额执法——同步等待不豁免。</summary>
    public IReadOnlyList<string> WaitAndPoll(LandProjector land, string currentRouteKey, TimeSpan budget)
    {
        ArgumentNullException.ThrowIfNull(land);
        if (_pending.Count > 0)
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var remainingCycleBudget = _pending
                .Select(p => p.FetchBound - System.Diagnostics.Stopwatch.GetElapsedTime(
                    p.DispatchedAtTimestamp, now))
                .Min();
            var wait = budget < remainingCycleBudget ? budget : remainingCycleBudget;
            if (wait > TimeSpan.Zero)
                Task.WaitAll(_pending.Select(p => (Task)p.Task).ToArray(), wait);
        }
        return Poll(land, currentRouteKey);
    }

    /// <summary>驱动线程结算：落成投影（LandProjector）/ 路由变更丢弃 / 超时-错误零投影。
    /// 返回本周期产生的全部 trace 令牌。</summary>
    public IReadOnlyList<string> Poll(LandProjector land, string currentRouteKey)
    {
        ArgumentNullException.ThrowIfNull(land);
        var tokens = new List<string>();
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var p = _pending[i];
            if (!p.Task.IsCompleted)
                continue;
            _pending.RemoveAt(i);
            p.CancellationTokenSource.Dispose();
            // AGT-019 M3：结算令牌统一带 ms 延迟分量（发射→结算实际耗时）。
            var latencyMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(
                p.DispatchedAtTimestamp).TotalMilliseconds;
            UniPerceptionFetchResult result;
            try
            {
                result = p.Task.Result;
            }
            catch (Exception error)
            {
                tokens.Add($"{p.TriggerToken}|Rejected|slow-pipeline-fault:{error.Message}|ms={latencyMs}");
                continue;
            }
            if (result.Status == Kernel.Perception.SlowConsultationStatus.TimedOut)
            {
                tokens.Add($"{p.TriggerToken}|TimedOut|zero-projection|ms={latencyMs}");
                continue;
            }
            if (result.Status is not (Kernel.Perception.SlowConsultationStatus.Succeeded
                or Kernel.Perception.SlowConsultationStatus.Partial))
            {
                tokens.Add($"{p.TriggerToken}|{result.Status}|{result.Diagnostic}|ms={latencyMs}");
                continue;
            }
            // AGT-019 M2③/B5（Q3 裁决）：闸二——入账拒收。judgment 级来源
            // 永不投影入账（与错屏丢弃门同构执法），且**先于**路由对齐判定
            //（对齐也不入账：判断级/事实级分界红线）；drop 令牌即闸三——
            // trace 只记"看过"，不记采信。
            if (p.JudgmentLevel)
            {
                tokens.Add($"{p.TriggerToken}|Dropped|judgment-source-never-ledgered|ms={latencyMs}");
                continue;
            }
            if (!string.Equals(p.RouteAtDispatch, currentRouteKey, StringComparison.Ordinal))
            {
                tokens.Add($"{p.TriggerToken}|Dropped|unaligned-route:{p.RouteAtDispatch}->{currentRouteKey}|ms={latencyMs}");
                continue;
            }
            tokens.Add(land(p.Request, result) + $"|ms={latencyMs}");
        }
        return tokens;
    }
}
