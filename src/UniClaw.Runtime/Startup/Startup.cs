using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.Startup;

/// <summary>
/// §19 Startup 程序（一次生命周期阶段；Initializing 阶段由 Agent 调用 — design.md §5）。
/// 按 §19 顺序执行：Attach（no-op）→ Launch → Observe → Verify ForegroundApplication →
/// Resolve Initial Semantic World → Establish Initial Container → Establish RecoveryAnchor → Ready；
/// 产出 StartupResult：Ready(RecoveryAnchor) 或 NotReady(显式原因)（SC-P1-002）——失败不抛异常（§45）。
/// 本组件只拥有 Startup 执行状态（全部为局部变量）；不拥有 WorldBelief / RunState / Container（I-2）；
/// 不硬编码场景字符串（裁决 11）：目标应用与语义解析规则全部由调用侧注入。
/// </summary>
public sealed class Startup
{
    private readonly IEnvironment _environment;
    private readonly string _targetApplicationIdentity;
    private readonly Func<Observation, string?> _resolveSemanticPage;

    /// <summary>构造 Startup 程序。</summary>
    /// <param name="environment">IEnvironment 端口（B2）——观察与动作能力边界。</param>
    /// <param name="targetApplicationIdentity">目标应用标识：LaunchApp 的 ApplicationId 与 ForegroundApplication 验证的期望值。</param>
    /// <param name="resolveSemanticPage">语义解析规则：Observation → 语义页面名（Resolve Initial Semantic World 与
    /// RecoveryAnchor.ExpectedSemanticEntry 的数据来源）；返回 null = 无法解析。</param>
    /// <exception cref="ArgumentNullException">environment 或 resolveSemanticPage 为 null。</exception>
    /// <exception cref="ArgumentException">targetApplicationIdentity 为空或空白。</exception>
    public Startup(IEnvironment environment, string targetApplicationIdentity, Func<Observation, string?> resolveSemanticPage)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetApplicationIdentity);
        ArgumentNullException.ThrowIfNull(resolveSemanticPage);
        _environment = environment;
        _targetApplicationIdentity = targetApplicationIdentity;
        _resolveSemanticPage = resolveSemanticPage;
    }

    /// <summary>
    /// 执行 §19 启动序列并报告 StartupResult。Verify ForegroundApplication 失败或语义页面无法解析时
    /// 返回 NotReady(显式原因)，不做任何进一步动作（无恢复动作 — SC-P1-002）。
    /// </summary>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>StartupResult：Ready(RecoveryAnchor) 或 NotReady(显式原因)。</returns>
    public async Task<StartupResult> StartAsync(CancellationToken cancellationToken)
    {
        // §19 step 1 — Attach（Phase 1 Fake 环境无需真实 attach — §33；真实 attach 由 Phase 4 Adapter 接入 — I-12）
        await AttachAsync(cancellationToken);

        // §19 step 2 — Launch（dispatch 结果不构成启动成功证据 — 裁决 10；门控是 step 4 的 ForegroundApplication 验证）
        _ = await _environment.ExecuteAsync(new DeviceAction.LaunchApp(_targetApplicationIdentity), cancellationToken);

        // §19 step 3 — Observe（动作后必须重新观察 — §3）
        var observation = await _environment.ObserveAsync(cancellationToken);

        // §19 step 4 — Verify ForegroundApplication（裁决 7 的消费者：确认已进入目标应用，作为解析语义入口的依据）
        if (!string.Equals(observation.ForegroundApplication, _targetApplicationIdentity, StringComparison.Ordinal))
        {
            return new StartupResult.NotReady(
                "ForegroundApplication 验证失败：观测到"
                + (observation.ForegroundApplication is null ? "<null>" : $"「{observation.ForegroundApplication}」")
                + $"，期望「{_targetApplicationIdentity}」（观测 seq={observation.SequenceNumber}）。");
        }

        // §19 step 5 — Resolve Initial Semantic World（World/Reconcile 纯函数；belief 实例由 Agent 持有 — B7）
        var belief = Reconcile.FromObservation(observation, _resolveSemanticPage);
        if (belief.SemanticPage is null)
        {
            return new StartupResult.NotReady(
                $"初始语义页面解析失败：观测（seq={observation.SequenceNumber}）无法解析出语义页面。");
        }

        // §19 step 6 — Establish Initial Container（Phase 1：语义页面名即初始容器的期望语义身份；
        //              此处仅把期望语义入口记录进 RecoveryAnchor，不创建 Container 实例 — B5 / I-12）
        // §19 step 7 — Establish RecoveryAnchor（§20：ApplicationIdentity / ExpectedSemanticEntry /
        //              VerificationCriteria — 裁决 8：无 EntryStrategy / RestoreRecipe）
        var anchor = new RecoveryAnchor(
            _targetApplicationIdentity,
            belief.SemanticPage,
            $"ForegroundApplication == {_targetApplicationIdentity}");

        // §19 step 8 — Ready
        return new StartupResult.Ready(anchor);
    }

    /// <summary>§19 step 1 — Attach：Phase 1 Fake 环境的 no-op（§33）；真实 attach 语义由 Phase 4 引入。</summary>
    private static Task AttachAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
