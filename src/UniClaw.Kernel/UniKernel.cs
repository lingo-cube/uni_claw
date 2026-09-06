using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel;

/// <summary>
/// Uni Kernel 组合缝（Target §3.5）：组合 Evidence Ledger、World Model、
/// Run Model、Control Loop、Assurance、Effect Boundary 六个 L2，不成为任何
/// Owner 的兜底（不变量 3）。pipeline 次序固定且不可合并：observation 侧
/// admission → relevance → reconciliation（E2B）；action 侧 intent →
/// assurance judgment → canonical binding → gate/dispatch → receipt →
/// attempt evidence 回流（C2E）；终局侧 Assurance judgment → Run Model
/// terminal record → Runtime Outcome emission（OUT-003）。
/// </summary>
public sealed record KernelResult(
    AdmissionRecord Admission,
    RelevanceJudgment? Relevance,
    WorldBeliefRevision? ResultingRevision);

/// <summary>
/// 一次 act pipeline 的组合结果（C2E-002）。四类产出各自在 Owner 的
/// append-only log 留痕；本聚合只持引用，不是平行记录（验收 2）。
/// </summary>
public sealed record ActResult(
    AssuranceJudgment Judgment,
    BindingDecision? Binding,
    GateDecision? Gate,
    EffectReceipt? Receipt,
    KernelResult? AttemptEvidenceReflux);

/// <summary>
/// 一次 terminal 编排的组合结果（OUT-003）。Proof 为 null = 证据不足
/// （不做 terminal，不猜测分类）；Transition.Accepted=false = 竞争失败 /
/// 已 terminal；Outcome 非 null 仅当本调用完成 exactly-once emission。
/// </summary>
public sealed record TerminalEvaluation(
    OutcomeProof? Proof,
    OutcomeTransition Transition,
    RuntimeOutcome? Outcome);

/// <summary>
/// Uni Kernel 边界：六 L2 组合缝。后四个 L2 可选注入——未注入时
/// 对应组合 API fail-closed；E2B 两 L2 用法（Process/Slice 面）零改动。
/// OUT-003 新增：terminal Runtime Outcome emission boundary（不变量 39）——
/// 编排 Assurance judgment → Run Model terminal record → Effect Boundary
/// delivery closure → immutable envelope（exactly once）；Kernel 不重判完成、
/// 不解析 Evidence、不覆盖 Outcome State、不改 classification。
/// </summary>
public sealed class UniKernel
{
    private readonly EvidenceLedger _ledger;
    private readonly WorldModel _world;
    private readonly RunModel? _run;
    private readonly ControlLoop? _control;
    private readonly RuntimeAssurance? _assurance;
    private readonly EffectBoundary? _effects;

    /// <summary>注入被组合的 L2 authority；Kernel 不持有任何平行状态。</summary>
    public UniKernel(
        EvidenceLedger ledger,
        WorldModel world,
        RunModel? run = null,
        ControlLoop? control = null,
        RuntimeAssurance? assurance = null,
        EffectBoundary? effects = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _run = run;
        _control = control;
        _assurance = assurance;
        _effects = effects;
    }

    /// <summary>Current WorldBelief 透传（Kernel 不持有平行 belief）。</summary>
    public WorldBeliefRevision? CurrentBelief => _world.Current;

    /// <summary>
    /// 处理一条观察输入：
    /// rejected → 短路（无 relevance、无 reconciliation、零 belief 变化）；
    /// accepted + irrelevant → 保留 canonical record，不产 revision；
    /// accepted + relevant → Reconciliation（幂等）。
    /// terminal 后仍可 admission / reconciliation（historical evidence 可追加），
    /// 但不得恢复 terminal Run（任务 九.H，E2B 路径不变）。
    /// </summary>
    public KernelResult Process(ObservationRecord observation)
    {
        var (admission, record) = _ledger.Admit(observation);

        // fail-closed 短路（验收 4）：rejected 不得触达 Relevance / Reconciliation
        if (admission.Decision != AdmissionDecision.Accepted || record is null)
            return new KernelResult(admission, Relevance: null, ResultingRevision: null);

        // Belief Relevance 判定（验收 1）：与 Admission 是两个独立产出
        var relevance = _world.JudgeRelevance(record);

        // irrelevant（验收 3）：canonical record 保留，不要求 revision
        if (!relevance.IsRelevant)
            return new KernelResult(admission, relevance, ResultingRevision: null);

        var before = _world.Current;
        var revision = _world.Reconcile(record, relevance);

        // 幂等（验收 8）：reconcile 未产生新 revision 时如实报告
        var resulting = ReferenceEquals(revision, before) ? null : revision;
        return new KernelResult(admission, relevance, resulting);
    }

    /// <summary>Slice 派生透传。</summary>
    public Slice DeriveSlice(string scope) => _world.DeriveSlice(scope);

    /// <summary>Slice 有效性透传。</summary>
    public bool IsSliceValid(Slice slice) => _world.IsSliceValid(slice);

    private RunModel Run => _run ?? throw new InvalidOperationException("Uni Kernel 未组合 Run Model");

    private ControlLoop Control => _control ?? throw new InvalidOperationException("Uni Kernel 未组合 Control Loop");

    private RuntimeAssurance Assurance => _assurance ?? throw new InvalidOperationException("Uni Kernel 未组合 Assurance");

    private EffectBoundary Effects => _effects ?? throw new InvalidOperationException("Uni Kernel 未组合 Effect Boundary");

    /// <summary>Execution Contract admission 透传（Run Model 唯一写入路径）。</summary>
    public ContractAdmission AdmitContract(ExecutionContract contract) => Run.AdmitContract(contract);

    /// <summary>
    /// 一个 control cycle（Target §19 输入三元组）：Contract View + Slice +
    /// Run State → Control Loop 签发 intent；Run Model 记录 cycle 推进
    /// （typed transition）。terminal 后 fail-closed（intent 签发即冻结，
    /// 不得重新打开 Run；§17）。
    /// </summary>
    public ControlIntent SelectIntent(Slice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        if (Run.IsTerminal)
            throw new InvalidOperationException("Run 已 terminal，不可再签发 Control Intent（不得重新打开 Run）");
        var view = Run.View ?? throw new InvalidOperationException("尚无已接受的 Execution Contract");
        var runState = Run.State ?? throw new InvalidOperationException("Run State 尚未建立");
        var intent = Control.SelectIntent(view, slice, runState);
        Run.RecordCycle();
        return intent;
    }

    /// <summary>
    /// Act pipeline（次序固定且不可合并，验收 2）：已签发 act-intent →
    /// Assurance action-local judgment → Effect Boundary canonical binding →
    /// Gate 执法 + 机械投递 → Effect Receipt → attempt evidence 经既有
    /// admission 路径回流（P3）+ action-local 结果通知 + Run Model 记录 act。
    /// 任一环节拒绝即短路，后续环节零副作用。terminal 后 fail-closed：
    /// 新 Control Intent 即使存在也不能变成 effect（不变量 42，任务 八）。
    /// </summary>
    public ActResult Act(ControlIntent intent, CandidateBinding? candidate)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (Run.IsTerminal)
            throw new InvalidOperationException("Run 已 terminal，external effect 通道已关闭（不得 dispatch）");
        if (intent.Kind != ControlIntentKind.Act)
            throw new ArgumentException("Act pipeline 只接受 act-intent", nameof(intent));
        if (!Control.IsIssued(intent))
            throw new InvalidOperationException("intent 未经 Control Loop 签发（sole Control Intent Authority）");

        var view = Run.View ?? throw new InvalidOperationException("尚无已接受的 Execution Contract");
        var current = _world.Current ?? throw new InvalidOperationException("尚无 WorldBelief revision");

        // 1) Assurance judgment（action-local；拒绝即短路，验收 4 前半）
        var judgment = Assurance.Judge(intent, candidate, view, current);

        BindingDecision? binding = null;
        GateDecision? gate = null;
        EffectReceipt? receipt = null;
        KernelResult? reflux = null;

        // 2) Canonical binding（Effect Boundary 唯一认定路径；拒绝即短路，验收 3）
        if (judgment.IsAdmissible && candidate is not null)
            binding = Effects.Bind(intent, candidate, current);

        // 3) Gate 执法 + Dispatch（拒绝即短路，验收 4 后半）
        if (binding?.Canonical is { } canonical)
        {
            var (gateDecision, dispatched) = Effects.Dispatch(canonical, judgment, current);
            gate = gateDecision;
            receipt = dispatched;
        }

        // 4) Receipt 已在 Owner log 留痕；回流走既有 E2B 路径（P3，验收 8）
        if (receipt is not null)
        {
            Assurance.NoteOutcome(receipt);
            Control.NoteDispatchOutcome(receipt);
            reflux = Process(Effects.ExportAttemptEvidence(receipt));
            Run.RecordAction();
        }

        return new ActResult(judgment, binding, gate, receipt, reflux);
    }

    /// <summary>
    /// Terminal 编排（OUT-003；终局链的 Uni Kernel 段）：Assurance 形成
    /// Outcome Proof → Run Model 记录 terminal Outcome State（exact-prior
    /// CAS）→ 接受后关闭 Effect Boundary delivery → 从 canonical Outcome
    /// State 投影 immutable Runtime Outcome（exactly once）。证据不足时
    /// 返回无 proof（保持 non-terminal，不猜测分类）；已 terminal / 竞争
    /// 失败时不产生新的 Runtime Outcome。
    /// </summary>
    public TerminalEvaluation EvaluateTerminal()
    {
        var state = Run.State ?? throw new InvalidOperationException("Run State 尚未建立（无已接受 contract）");
        if (Run.IsTerminal)
            return new TerminalEvaluation(null, new OutcomeTransition(false, "already-terminal", null), null);

        var view = Run.View ?? throw new InvalidOperationException("尚无已接受的 Execution Contract");
        var current = _world.Current ?? throw new InvalidOperationException("尚无 WorldBelief revision");

        // 1) Assurance 独占 Outcome Proof judgment（不变量 22；Kernel 不重判）
        var proof = Assurance.JudgeOutcome(view, state.ProofObligations, current, _ledger.CanonicalRecords);
        if (proof is null)
            return new TerminalEvaluation(null, new OutcomeTransition(false, "evidence-insufficient", null), null);

        // 2) Run Model 记录 terminal Outcome State（exact-prior / single-winner）
        var transition = Run.TransitionToTerminal(proof, state);
        if (!transition.Accepted)
            return new TerminalEvaluation(proof, transition, null);

        // 3) terminal 后 external effect delivery 关闭（不变量 42）
        Effects.CloseDelivery();

        // 4) 只从 canonical Outcome State 投影 envelope（§7；不覆盖、不重判）
        var outcomeState = Run.State!.Outcome!;
        var outcome = new RuntimeOutcome(
            Run.RunId,
            outcomeState.OutcomeProofId,
            outcomeState.Classification,
            outcomeState.Obligations,
            outcomeState.BasisEvidenceIds,
            outcomeState.EffectEvidenceIds,
            outcomeState.SituationEvidenceIds,
            outcomeState.UnresolvedUncertainty,
            outcomeState.Reason);
        return new TerminalEvaluation(proof, transition, outcome);
    }
}
