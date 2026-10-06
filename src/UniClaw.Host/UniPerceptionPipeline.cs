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
        CancellationTokenSource CancellationTokenSource);

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
        string routeKey, string triggerToken, TimeSpan? fetchBound = null)
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
        _pending.Add(new Pending(request, routeKey, task, triggerToken, cts));
        return $"{triggerToken}|Dispatched|in-flight={_pending.Count}";
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
            UniPerceptionFetchResult result;
            try
            {
                result = p.Task.Result;
            }
            catch (Exception error)
            {
                tokens.Add($"{p.TriggerToken}|Rejected|slow-pipeline-fault:{error.Message}");
                continue;
            }
            if (result.Status == Kernel.Perception.SlowConsultationStatus.TimedOut)
            {
                tokens.Add($"{p.TriggerToken}|TimedOut|zero-projection");
                continue;
            }
            if (result.Status is not (Kernel.Perception.SlowConsultationStatus.Succeeded
                or Kernel.Perception.SlowConsultationStatus.Partial))
            {
                tokens.Add($"{p.TriggerToken}|{result.Status}|{result.Diagnostic}");
                continue;
            }
            if (!string.Equals(p.RouteAtDispatch, currentRouteKey, StringComparison.Ordinal))
            {
                tokens.Add($"{p.TriggerToken}|Dropped|unaligned-route:{p.RouteAtDispatch}->{currentRouteKey}");
                continue;
            }
            tokens.Add(land(p.Request, result));
        }
        return tokens;
    }
}
