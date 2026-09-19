using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// KernelFacts 是优选的 owner 状态只读投影，不是编译级隔离：本 test
/// assembly 内部仍可触及 host 的 internal 组合面。per-cycle 零接触依赖
/// 源码纪律测试；真正的编译级隔离须另立 assembly 边界。
/// </summary>
internal sealed record KernelFacts(
    string RunId,
    bool IsRunTerminal,
    IReadOnlyList<EffectReceipt> EffectReceipts,
    IReadOnlyList<RunState> RunHistory,
    IReadOnlyList<WorldBeliefRevision> Revisions,
    IReadOnlyList<AssuranceJudgment> Judgments,
    IReadOnlyList<PostActionEffectVerification> PostActionVerifications,
    IReadOnlyList<OutcomeProof> OutcomeProofs,
    IReadOnlyDictionary<string, string> CurrentWorldClaims);

/// <summary>
/// RFS-001 Simulation Host composition root：全部 L2 product modules 真实
/// （EvidenceLedger / WorldModel / RunModel / ControlLoop / RuntimeAssurance /
/// EffectBoundary / UniKernel / KernelRunDriver），只有外部缝是确定性
/// double（SeedingAssociationStrategy / ReplayFrameObservationStrategy /
/// SatisfyingFreshness / DeterministicEffectDriver / ScriptedUniAgent /
/// ScenarioStimulusFeed）。Trace 三臂（Enabled / Disabled / FailingRecorder）
/// 由 RunOptions 选择。D21：phased 场景经 DriveOnce/SubmitStimulus 驱动，
/// 每次 Drive 结果记录在 RecordedDriveResults。
/// </summary>
internal sealed class SimulationHost
{
    // ---- 内部组合面（runner / digest 专用；测试读 owner 数据走 Facts）----

    internal UniKernel KernelCore { get; }
    internal KernelRunDriver Driver { get; }
    internal AgentPlanPolicy Plan { get; }
    internal ScriptedUniAgent ScriptedAgent { get; }
    internal DeterministicEffectDriver EffectDriver { get; }
    internal ScenarioStimulusFeed Feed { get; }
    internal RuntimeStageMetrics Metrics { get; }
    internal RunTraceScope? TraceScope { get; }
    internal RunModel RunModelCore { get; }
    internal EffectBoundary EffectBoundaryCore { get; }
    internal RuntimeAssurance AssuranceCore { get; }
    internal WorldModel WorldCore { get; }
    internal TraceArm BundleArm { get; }

    /// <summary>TRW-001：AsyncFile 臂的 writer 实例（其余臂为 null）。</summary>
    internal AsyncFileTraceWriter? TraceWriter { get; }

    /// <summary>TRW-001：AsyncFile 臂实际使用的 writer 选项（含持久化目录）。</summary>
    internal AsyncTraceWriterOptions? AsyncTraceOptions { get; }

    /// <summary>
    /// CORE-011：本 host 组合时注入的可靠执行源 fixture（仿真契约 §3：
    /// SimulationHost 负责把同一 fixture 交给 Host A 与 Host B）。null =
    /// 未注入（非执行源场景不受影响）。fixture 独立于 Trace 与 host 内存
    /// 对象，是 crash 后唯一跨 Host 存活的测试侧资产。
    /// </summary>
    internal ReliableExecutionSourceFixture? ExecutionSource { get; }

    /// <summary>D21：测试每次 Host.DriveOnce 的结果（append-only）。</summary>
    public IReadOnlyList<RunDriveResult> RecordedDriveResults => _recordedDriveResults;
    private readonly List<RunDriveResult> _recordedDriveResults = new();

    /// <summary>runner 记录的激活面（FinalizePhased 复用）。</summary>
    internal ActivationResult FirstActivation { get; set; } = new(false, null, false, "unset");
    internal ActivationResult? SecondActivation { get; set; }

    /// <summary>
    /// kernel/world/assurance/run/effect owner 状态的只读事实面（D23）：
    /// 每次读取即时派生（无平行状态）。
    /// </summary>
    public KernelFacts Facts => new(
        RunId: KernelCore.RunId,
        IsRunTerminal: KernelCore.IsRunTerminal,
        EffectReceipts: KernelCore.EffectReceipts.ToList(),
        RunHistory: RunModelCore.History.ToList(),
        Revisions: WorldCore.RevisionHistory.ToList(),
        Judgments: AssuranceCore.JudgmentLog.ToList(),
        PostActionVerifications: AssuranceCore.PostActionVerificationLog.ToList(),
        OutcomeProofs: AssuranceCore.OutcomeProofLog.ToList(),
        CurrentWorldClaims: KernelCore.CurrentBelief?.WorldState.ToDictionary(
            kv => kv.Key, kv => kv.Value.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal));

    private SimulationHost(
        UniKernel kernel, KernelRunDriver driver, AgentPlanPolicy plan,
        ScriptedUniAgent scriptedAgent, DeterministicEffectDriver effectDriver,
        ScenarioStimulusFeed feed, RuntimeStageMetrics metrics,
        RunTraceScope? traceScope, RunModel runModel, EffectBoundary effectBoundary,
        RuntimeAssurance assurance, WorldModel world, TraceArm bundleArm,
        AsyncFileTraceWriter? asyncWriter, AsyncTraceWriterOptions? asyncOptions,
        ReliableExecutionSourceFixture? executionSource)
    {
        KernelCore = kernel;
        Driver = driver;
        Plan = plan;
        ScriptedAgent = scriptedAgent;
        EffectDriver = effectDriver;
        Feed = feed;
        Metrics = metrics;
        TraceScope = traceScope;
        RunModelCore = runModel;
        EffectBoundaryCore = effectBoundary;
        AssuranceCore = assurance;
        WorldCore = world;
        BundleArm = bundleArm;
        TraceWriter = asyncWriter;
        AsyncTraceOptions = asyncOptions;
        ExecutionSource = executionSource;
    }

    /// <summary>
    /// 组合一个场景 bundle（本方法 Verify 并取得该 host 独占资产注册表）。
    /// D18：per-host ScenarioPerceptionAdapter 注入本 host 的 trace 与 metrics
    /// （FastPerception 以 TraceReferenceKind.Artifact 引用每帧 RawArtifact，
    /// 使 sealed trace artifact 可作 derivation 输入源）；并登记
    /// stimulus→artifact 映射（host 侧登记面）。
    /// CORE-013：reliableExecutionSource 注入产品执行源（EffectBoundary
    /// 构造 seam）；null = 既有行为。与 CORE-011 的测试侧 fixture 参数
    /// （executionSource）是两层：前者是产品 IReliableExecutionSource，
    /// 后者是仿真验收 fixture。
    /// </summary>
    public static SimulationHost Compose(
        MinimalScenarioBundle bundle, RunOptions? options = null,
        ReliableExecutionSourceFixture? executionSource = null,
        UniClaw.Kernel.Effects.ExecutionSource.IReliableExecutionSource? reliableExecutionSource = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        options ??= new RunOptions();
        var assets = bundle.Verify();

        IRunTrace trace;
        RunTraceScope? scope;
        AsyncFileTraceWriter? asyncWriter = null;
        AsyncTraceWriterOptions? asyncOptions = null;
        switch (options.TraceArm)
        {
            case TraceArm.Enabled:
                scope = RunTraceFactory.BeginRun(new RunCorrelation("sim:" + bundle.ScenarioId));
                trace = scope.Trace;
                break;
            case TraceArm.Disabled:
                scope = RunTraceFactory.BeginDisabled(new RunCorrelation("sim:" + bundle.ScenarioId));
                trace = scope.Trace;
                break;
            case TraceArm.FailingRecorder:
                scope = null;
                trace = new ThrowingRunTrace();
                break;
            case TraceArm.AsyncFile:
                // TRW-001：真实异步 writer 臂（D5：目录由 Host 注入；默认独占临时目录）
                asyncOptions = options.AsyncTrace ?? new AsyncTraceWriterOptions(
                    Path.Combine(Path.GetTempPath(), "uniclaw-async-trace-" + Guid.NewGuid().ToString("N")));
                asyncWriter = new AsyncFileTraceWriter(
                    new RunCorrelation("sim:" + bundle.ScenarioId), asyncOptions);
                scope = new RunTraceScope(asyncWriter);
                trace = asyncWriter;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.TraceArm, "未知 Trace 臂");
        }

        var world = new WorldModel(
            bundle.Contract.Scope!,
            new SeedingAssociationStrategy(),
            new ReplayFrameObservationStrategy());
        var assurance = new RuntimeAssurance(new SatisfyingFreshness());
        var effectDriver = new DeterministicEffectDriver();
        var effectBoundary = new EffectBoundary(effectDriver, reliableExecutionSource);
        var metrics = new RuntimeStageMetrics();
        var planPolicy = new AgentPlanPolicy();
        var perception = new ScenarioPerceptionAdapter(assets, trace, metrics);
        var feed = new ScenarioStimulusFeed(bundle.Stimuli, perception);
        var scriptedAgent = new ScriptedUniAgent(bundle.AgentScript);
        var runModel = new RunModel();
        var kernel = new UniKernel(
            new EvidenceLedger(), world, trace,
            runModel, new ControlLoop(planPolicy), assurance, effectBoundary, metrics);
        var driver = new KernelRunDriver(kernel, planPolicy, new RunDriverInputs
        {
            NextInput = feed.Next,
            ConsultAgent = scriptedAgent.Consult,
        });

        var host = new SimulationHost(
            kernel, driver, planPolicy, scriptedAgent, effectDriver, feed, metrics,
            scope, runModel, effectBoundary, assurance, world, options.TraceArm,
            asyncWriter, asyncOptions, executionSource);

        // D18：per-host adapter（trace/metrics 注入）+ stimulus→artifact 登记
        foreach (var stimulus in bundle.Stimuli.OfType<ScenarioStimulus.ObservationFrame>())
            feed.RegisterStimulusArtifact(stimulus.StimulusId, stimulus.PerceptionArtifactId);
        return host;
    }

    /// <summary>
    /// D21 phased 驱动面：包裹一次 Driver.Drive()，记录结果并在 run terminal
    /// 后标记 ScriptedAgent（late-call 建模）。测试不得直接调用 Driver.Drive。
    /// </summary>
    public RunDriveResult DriveOnce()
    {
        var result = Driver.Drive();
        _recordedDriveResults.Add(result);
        if (KernelCore.IsRunTerminal)
            ScriptedAgent.MarkTerminal();
        return result;
    }

    /// <summary>D21：运行期补充 stimulus（包裹 Feed.Submit；重复 id → false）。</summary>
    public bool SubmitStimulus(ScenarioStimulus stimulus)
    {
        var accepted = Feed.Submit(stimulus);
        if (accepted && stimulus is ScenarioStimulus.ObservationFrame frame)
            Feed.RegisterStimulusArtifact(frame.StimulusId, frame.PerceptionArtifactId);
        return accepted;
    }

    /// <summary>
    /// 确定性 freshness double：恒 Sufficient（scripted:sufficient）——
    /// freshness 纪律的验证模式已在 report VerificationMode 显式声明。
    /// </summary>
    private sealed class SatisfyingFreshness : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "scripted:sufficient");
    }

    /// <summary>
    /// FailingRecorder 臂：StartOperation 恒抛 recorder-failure——
    /// 验证 trace 故障不改变 Runtime 行为（UniKernel fail-safe 吸收）。
    /// </summary>
    private sealed class ThrowingRunTrace : IRunTrace
    {
        public ITraceOperationScope StartOperation(
            SpanDefinition definition, TraceContext? parent,
            IReadOnlyList<TraceReference> references) =>
            throw new InvalidOperationException("recorder-failure");
    }
}
