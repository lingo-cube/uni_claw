using UniClaw.Kernel.Effects;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 deterministic IEffectDriver double：每次 Deliver 确定性成功
/// （DeliveryCompleted），CompletedAt 由注入基线 + 递增计数派生——
/// 零 wall-clock、零随机（replay 语义边界前提）。
/// </summary>
public sealed class DeterministicEffectDriver : IEffectDriver
{
    private readonly DateTimeOffset _base;

    public DeterministicEffectDriver(
        DateTimeOffset? baseTime = null) =>
        _base = baseTime ?? new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);

    /// <summary>累计投递次数（runner 观察面）。</summary>
    public int DeliveryCount { get; private set; }

    /// <summary>PROTOCOL-FIX-004 S4：捕获的 DispatchRequest（观察 DeliveryTarget——
    /// locator 唯一载体；本 double 不做任何语义改写）。</summary>
    public IReadOnlyList<DispatchRequest> Requests { get; } = new List<DispatchRequest>();

    public DispatchResult Deliver(DispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        DeliveryCount++;
        ((List<DispatchRequest>)Requests).Add(request);
        return new DispatchResult(
            DispatchOutcome.DeliveryCompleted,
            "sim:delivered",
            _base.AddSeconds(DeliveryCount));
    }
}
