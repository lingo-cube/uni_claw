using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;
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
    private readonly string? _launchIntentAction;
    private readonly Func<Observation, string?> _resolveSemanticPage;
    private readonly string? _restoreRecipe;
    private readonly string? _entryStrategy;
    private readonly Func<CancellationToken, Task<string?>>? _attach;

    /// <summary>构造 Startup 程序。</summary>
    /// <param name="environment">IEnvironment 端口（B2）——观察与动作能力边界。</param>
    /// <param name="targetApplicationIdentity">目标应用标识：LaunchApp 的 ApplicationId 与 ForegroundApplication 验证的期望值。</param>
    /// <param name="resolveSemanticPage">语义解析规则：Observation → 语义页面名（Resolve Initial Semantic World 与
    /// RecoveryAnchor.ExpectedSemanticEntry 的数据来源）；返回 null = 无法解析。</param>
    /// <param name="launchIntentAction">可选机制级启动意图，随 LaunchApp 传递给
    /// Environment（Provider 翻译为物理启动命令）；null = Phase 1 默认启动方式。机制提示，非场景状态注入、非语义。</param>
    /// <param name="restoreRecipe">恢复动作描述（C4 — SC-P2-001：注入 RecoveryAnchor.RestoreRecipe；默认 null = Phase 1 行为）。</param>
    /// <param name="entryStrategy">入口策略描述（C4 — SC-P2-001：注入 RecoveryAnchor.EntryStrategy；默认 null = Phase 1 行为）。</param>
    /// <param name="attach">§19 step 1 Attach 的物理就绪检查委托（Phase 4 真实 IO 接入点 — I-12）：
    /// 返回 null = attach 成功；返回非 null 字符串 = 显式失败原因（Startup 以 NotReady(原因) 终止，零动作分发 — SC-P1-002）。
    /// 默认 null = Phase 1 Fake 环境 no-op attach（§33 行为保持不变）。组合根负责注入真实设备预检。</param>
    /// <exception cref="ArgumentNullException">environment 或 resolveSemanticPage 为 null。</exception>
    /// <exception cref="ArgumentException">targetApplicationIdentity 为空或空白。</exception>
    public Startup(
        IEnvironment environment,
        string targetApplicationIdentity,
        Func<Observation, string?> resolveSemanticPage,
        string? launchIntentAction = null,
        string? restoreRecipe = null,
        string? entryStrategy = null,
        Func<CancellationToken, Task<string?>>? attach = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetApplicationIdentity);
        ArgumentNullException.ThrowIfNull(resolveSemanticPage);
        _environment = environment;
        _targetApplicationIdentity = targetApplicationIdentity;
        _launchIntentAction = launchIntentAction;
        _resolveSemanticPage = resolveSemanticPage;
        _restoreRecipe = restoreRecipe;
        _entryStrategy = entryStrategy;
        _attach = attach;
    }

    /// <summary>
    /// 执行 §19 启动序列并报告 StartupResult。Verify ForegroundApplication 失败或语义页面无法解析时
    /// 返回 NotReady(显式原因)，不做任何进一步动作（无恢复动作 — SC-P1-002）。
    /// </summary>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>StartupResult：Ready(RecoveryAnchor) 或 NotReady(显式原因)。</returns>
    public async Task<StartupResult> StartAsync(CancellationToken cancellationToken)
    {
        // Startup bootstrap timing (STARTUP layer; structural outcome only —
        // NotReady results are completed BOOTSTRAP, not exceptions).
        using var span = RuntimeObservability.StartSpan(
            "StartupBootstrap", ObservabilityLayer.Startup, ObservabilityComponent.StartupBootstrap);
        try
        {
            var result = await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            RuntimeObservability.Complete(span, ObservabilityOutcome.Succeeded);
            return result;
        }
        catch (OperationCanceledException)
        {
            RuntimeObservability.Complete(span, ObservabilityOutcome.Cancelled);
            throw;
        }
        catch (Exception)
        {
            RuntimeObservability.Complete(span, ObservabilityOutcome.Failed);
            throw;
        }
    }

    private async Task<StartupResult> StartCoreAsync(CancellationToken cancellationToken)
    {
        // §19 step 1 — Attach（Phase 1 Fake 环境无需真实 attach — §33；真实 attach 由组合根注入的
        // 物理就绪检查委托完成 — I-12）。attach 失败 = NotReady(显式原因)，零动作分发（SC-P1-002）。
        var attachFailure = await AttachAsync(cancellationToken);
        if (attachFailure is not null)
        {
            return new StartupResult.NotReady($"设备预检失败（Attach）：{attachFailure}");
        }

        // §19 step 2 — Launch（dispatch 结果不构成启动成功证据 — 裁决 10；门控是 step 4 的 ForegroundApplication 验证）
        _ = await _environment.ExecuteAsync(
            new DeviceAction.LaunchApp(_targetApplicationIdentity, _launchIntentAction), cancellationToken);

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
        //              VerificationCriteria + C4 注入的 RestoreRecipe / EntryStrategy — 裁决 8 解除，
        //              默认 null = Phase 1 向后兼容）
        var anchor = new RecoveryAnchor(
            _targetApplicationIdentity,
            belief.SemanticPage,
            $"ForegroundApplication == {_targetApplicationIdentity}",
            _restoreRecipe,
            _entryStrategy);

        // §19 step 8 — Ready
        return new StartupResult.Ready(anchor);
    }

    /// <summary>
    /// §19 step 1 — Attach：执行注入的物理就绪检查（Phase 4 真实 IO — I-12）。
    /// 返回 null = attach 成功；返回非 null = 显式失败原因（NotReady 终止，零动作分发）。
    /// 未注入 attach（Phase 1 Fake 环境）时保持 no-op 行为 — §33 向后兼容。
    /// </summary>
    private async Task<string?> AttachAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_attach is null)
            return null;
        return await _attach(cancellationToken);
    }
}
