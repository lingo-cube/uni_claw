using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SCN-002 D3：调度器 = 条件式接口。固定时机 / 条件触发 / 对抗者 =
/// 同一接口的三种实现，架构不变。Phase 1 只实现 FixedTimingScheduler
/// （Builder 的 afterStep(n) 语法糖）；Conditional / Adversarial 见
/// Out of Scope（等买家）。
/// </summary>
internal interface IStimulusScheduler
{
    /// <summary>条件谓词：给定调度状态，stimulus 是否到期。</summary>
    bool IsDue(StimulusSchedulingState state);
}

/// <summary>调度状态（条件输入；随实现演进扩展）。</summary>
internal sealed record StimulusSchedulingState(int CompletedPostActionObservations);

/// <summary>Phase 1：固定时机——第 n 个 effect 的 post-action 观察完成后到期。</summary>
internal sealed class FixedTimingScheduler(int afterStep) : IStimulusScheduler
{
    public int AfterStep { get; } = afterStep >= 0 ? afterStep
        : throw new ArgumentOutOfRangeException(nameof(afterStep), "afterStep 不得为负");

    public bool IsDue(StimulusSchedulingState state) =>
        state.CompletedPostActionObservations >= AfterStep;
}

/// <summary>
/// SCN-002 生成式场景 Builder（Fluent API，D1）。从 GoldenBundle 模板
/// 出发（D2：模板 = 录制资产再利用的预填底座），参数化 = C# 函数变换
/// （D5：零基础设施，不做 DSL）；Build() 产出与录制同格式的
/// MinimalScenarioBundle（D4：ScenarioRunner / SimulationHost / 覆盖率
/// 工具零改动）。
///
/// 确定性三层防线（D6）：
/// 层1 本 API 的类型契约——不接受 Random / 真实时钟参数（时间全部经
///     stimulus 的 VirtualTime 显式声明）；
/// 层2 运行时注入——VirtualClock / 种子化 Random 由 Simulation Host 注入
///     （既有机制，不在 Builder 内）；
/// 层3 Digest 验证——两次运行同 digest（GeneratedScenarioTests 执法）。
///
/// Inject(stimulus, afterStep: n)（D3 语法糖 ≡ Scope 里的 AfterEffect(n)）：
/// 构建期把 stimulus 插入第 n 个 post-action 观察帧之后——同步 feed 是
/// FIFO + 队首 context 匹配，位置即时机。
/// </summary>
internal sealed class ScenarioBuilder
{
    private readonly Func<MinimalScenarioBundle> _template;
    private string? _bundleId;
    private string? _scenarioId;
    private string? _scenarioVersion;
    private Func<IReadOnlyList<ScenarioStimulus>, IReadOnlyList<ScenarioStimulus>>? _screen;
    private Func<PhaseAwareAgentScript, PhaseAwareAgentScript>? _decision;
    private Func<ScenarioExpectation, ScenarioExpectation>? _expectation;
    private readonly List<(ScenarioStimulus Stimulus, IStimulusScheduler Scheduler)> _injections = new();

    private ScenarioBuilder(Func<MinimalScenarioBundle> template) =>
        _template = template ?? throw new ArgumentNullException(nameof(template));

    /// <summary>D2：从 GoldenBundle 模板构造（模板提供 TargetUiSystem /
    /// Assets / ProducerIdentities / Contract / Goal 底座）。</summary>
    public static ScenarioBuilder FromTemplate(Func<MinimalScenarioBundle> template) => new(template);

    /// <summary>生成场景身份（缺省 = 模板 id + "-generated"）。</summary>
    public ScenarioBuilder WithScenarioId(string bundleId, string scenarioId, string scenarioVersion = "v1")
    {
        _bundleId = bundleId;
        _scenarioId = scenarioId;
        _scenarioVersion = scenarioVersion;
        return this;
    }

    /// <summary>观察面参数化：变换模板的 stimulus 序列（C# 函数模式，D5）。</summary>
    public ScenarioBuilder Screen(Func<IReadOnlyList<ScenarioStimulus>, IReadOnlyList<ScenarioStimulus>> configure)
    {
        _screen = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <summary>
    /// 决策面参数化：变换模板的相位感知 turn 序列（SIM-004：变换
    /// scripted consultation sequence，不重新发明 Product decision taxonomy——
    /// turn 载荷即 Product 类型）。
    /// </summary>
    public ScenarioBuilder AgentDecides(Func<PhaseAwareAgentScript, PhaseAwareAgentScript> configure)
    {
        _decision = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <summary>期望参数化：变换模板期望值快照。</summary>
    public ScenarioBuilder Expect(Func<ScenarioExpectation, ScenarioExpectation> configure)
    {
        _expectation = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <summary>注入 stimulus：第 n 个 effect 的 post-action 观察后到达
    /// （AfterEffect(n) 语法糖；D3 FixedTimingScheduler）。</summary>
    public ScenarioBuilder Inject(ScenarioStimulus stimulus, int afterStep) =>
        Inject(stimulus, new FixedTimingScheduler(afterStep));

    /// <summary>注入 stimulus：经条件式调度器决定到达时机（接口面，
    /// Phase 2/3 演进点）。</summary>
    public ScenarioBuilder Inject(ScenarioStimulus stimulus, IStimulusScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(stimulus);
        ArgumentNullException.ThrowIfNull(scheduler);
        _injections.Add((stimulus, scheduler));
        return this;
    }

    /// <summary>构建 bundle（重铸 RuntimeArtifact 为当前构建；重封 digest）。</summary>
    public MinimalScenarioBundle Build()
    {
        var template = _template();
        var stimuli = _screen is { } screen ? screen(template.Stimuli) : template.Stimuli;
        foreach (var (stimulus, scheduler) in _injections)
            stimuli = InsertAfterDue(stimuli, stimulus, scheduler);
        return ScenarioBundleDigest.Sealed(template with
        {
            BundleId = _bundleId ?? template.BundleId + "-generated",
            ScenarioId = _scenarioId ?? template.ScenarioId + "-generated",
            ScenarioVersion = _scenarioVersion ?? template.ScenarioVersion,
            RuntimeArtifact = RuntimeArtifactIdentity.CaptureCurrent(),
            Stimuli = stimuli,
            PhaseScript = _decision is { } decision ? decision(template.PhaseScript) : template.PhaseScript,
            Expected = _expectation is { } expectation ? expectation(template.Expected) : template.Expected,
            BundleDigest = "",
        });
    }

    /// <summary>调度器解析插入位（层1 契约内的确定性变换）：
    /// FixedTiming(n) = 第 n 个 post-action 观察帧之后；条件不满足
    /// （序列不足 n 个 post 帧）→ 追加到序列尾（迟到语义，保持可观察）。</summary>
    private static IReadOnlyList<ScenarioStimulus> InsertAfterDue(
        IReadOnlyList<ScenarioStimulus> stimuli, ScenarioStimulus stimulus, IStimulusScheduler scheduler)
    {
        var completed = 0;
        var index = stimuli.Count;
        for (var i = 0; i < stimuli.Count; i++)
        {
            if (stimuli[i] is ScenarioStimulus.ObservationFrame
                {
                    Context: ObservationContext.PostActionEffectFlow,
                })
            {
                completed++;
                if (scheduler.IsDue(new StimulusSchedulingState(completed)))
                {
                    index = i + 1;
                    break;
                }
            }
        }
        var result = new List<ScenarioStimulus>(stimuli);
        result.Insert(index, stimulus);
        return result;
    }
}
