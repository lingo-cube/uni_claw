using System.Diagnostics;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel;

/// <summary>
/// Uni Kernel 组合缝（Target §3.5）：组合 Evidence Ledger、World Model、
/// Run Model、Control Loop、Assurance、Effect Boundary 六个 L2，不成为任何
/// Owner 的兜底（不变量 3）。pipeline 次序固定且不可合并：observation 侧
/// admission → relevance → reconciliation（E2B）；action 侧（CBA-005 /
/// ADR-0009 目标序）intent → canonical binding → assurance judgment →
/// gate/dispatch → receipt → attempt evidence 回流（C2E）；终局侧
/// Assurance judgment → Run Model terminal record → Runtime Outcome
/// emission（OUT-003）。
/// </summary>
public sealed record KernelResult(
    AdmissionRecord Admission,
    RelevanceJudgment? Relevance,
    WorldBeliefRevision? ResultingRevision);

/// <summary>
/// 一次 act pipeline 的组合结果（C2E-002；CBA-005 目标序）。四类产出各自
/// 在 Owner 的 append-only log 留痕；本聚合只持引用，不是平行记录（验收 2）。
/// Bind 四态拒绝 → Judgment/Gate/Receipt/Reflux/Transition 全 null（短路，D1）。
/// PER-003（P22 producer）：dispatch 成功时携带 ExportTransitionContext 产物
/// （non-evidentiary prior，ADR-0012）；receipt null → null。
/// </summary>
public sealed record ActResult(
    AssuranceJudgment? Judgment,
    BindingDecision? Binding,
    GateDecision? Gate,
    EffectReceipt? Receipt,
    KernelResult? AttemptEvidenceReflux,
    TransitionContext? Transition = null);

/// <summary>
/// 接地编排结果（CTL-001 / D3）：View 恒非 null（四态原样上抛，判定权在
/// 调用侧）；Act 非 null 仅当 View.Result == UniqueCandidate 且 Act pipeline
/// 全程成功产出 receipt 语义（Bind 拒绝时 Act 非 null 但 Receipt null，
/// 语义同 ActResult）。
/// </summary>
public sealed record GroundedActResult(CurrentGroundingView View, ActResult? Act);

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
    private readonly IRunTrace _trace;
    private readonly RunModel? _run;
    private readonly ControlLoop? _control;
    private readonly RuntimeAssurance? _assurance;
    private readonly EffectBoundary? _effects;
    private readonly RuntimeStageMetrics? _metrics;
    private readonly object _activationSync = new();
    private object? _activationOwner;

    /// <summary>
    /// 注入被组合的 L2 authority 与 trace 观察面；Kernel 不持有任何平行
    /// 状态。IRunTrace 为必选显式参数（TRC-001：禁 nullable / 隐式
    /// fallback；禁用传 DisabledRunTrace.Instance）。LAT-001：可选
    /// RuntimeStageMetrics 量化观察面（null = 禁用，零开销；非权威，
    /// 不参与任何决策）。
    /// </summary>
    public UniKernel(
        EvidenceLedger ledger,
        WorldModel world,
        IRunTrace trace,
        RunModel? run = null,
        ControlLoop? control = null,
        RuntimeAssurance? assurance = null,
        EffectBoundary? effects = null,
        RuntimeStageMetrics? metrics = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _run = run;
        _control = control;
        _assurance = assurance;
        _effects = effects;
        _metrics = metrics;
        _world.AttachPerformanceMetrics(metrics);
    }

    /// <summary>Current WorldBelief 透传（Kernel 不持有平行 belief）。</summary>
    public WorldBeliefRevision? CurrentBelief => _world.Current;

    /// <summary>Container Association decision log 透传（owner-internal；R-UW-02/03）。</summary>
    public IReadOnlyList<AssociationDecision> AssociationLog => _world.AssociationLog;

    // ---- RFS-001：internal run driver 组合观察面（只读透传；不新增 authority）----

    /// <summary>
    /// PER-009 S6b：driver 组合接线——Control 侧悬案 subjects 透传
    /// （internal；无 current revision = 空 = 无悬案可聚焦）。
    /// </summary>
    internal IReadOnlyList<string> CurrentConflictedSubjects =>
        _world.DeriveControlBeliefViewOrNull()?.ConflictedSubjects ?? Array.Empty<string>();

    /// <summary>
    /// PER-009 C-1（评审修复）：权威域冲突裁决 + owner 销案。对每个悬案
    /// subject 构造 ConflictCase（双方 producer/value/captureTime 来自
    /// CanonicalRecords）+ XmlAuthoritySnapshot（自 XML 侧 claim 的
    /// provenance lineage：xml-map:{localId} / xml-checkable / 构造唯一性），
    /// 经冻结 ConflictResolver 裁决；Tier 0 → WorldModel.ResolveConflict
    /// 销案（D13：confidence 盲、不升档）。返回销案数。internal。
    /// </summary>
    internal int ResolveAuthorityConflicts(TimeSpan freshnessWindow)
    {
        var view = _world.DeriveControlBeliefViewOrNull();
        if (view is null || view.ConflictedSubjects.Count == 0)
            return 0;

        var resolved = 0;
        foreach (var subject in view.ConflictedSubjects.ToArray())
        {
            var conflict = _world.Current?.Conflicts.FirstOrDefault(c => c.Subject == subject);
            if (conflict is null)
                continue;

            var claims = new List<World.ConflictResolver.ConflictingClaim>();
            Evidence.EvidenceRecord? xmlRecord = null;
            string? xmlValue = null;
            foreach (var pair in new[]
                     {
                         (EvidenceId: conflict.EstablishedEvidenceId, Value: conflict.EstablishedValue),
                         (EvidenceId: conflict.ChallengingEvidenceId, Value: conflict.ChallengingValue),
                     })
            {
                if (!_ledger.CanonicalRecords.TryGetValue(pair.EvidenceId, out var record))
                    continue;
                claims.Add(new World.ConflictResolver.ConflictingClaim(
                    record.Provenance.Producer, pair.Value, record.Provenance.CaptureTime));
                if (record.Provenance.Producer == World.ProducerTrust.XmlProducer)
                {
                    xmlRecord = record;
                    xmlValue = pair.Value;
                }
            }

            var disposition = World.ConflictResolver.Resolve(
                new World.ConflictResolver.ConflictCase(subject, claims),
                SnapshotFromLineage(xmlRecord, xmlValue),
                freshnessWindow);

            if (disposition.Tier == World.ConflictResolver.Tier.CategoryAuthority
                && disposition.ResolvedValue is { } value)
            {
                _world.ResolveConflict(
                    subject, value, "category-authority",
                    disposition.OverruledProducer, disposition.Basis,
                    DateTimeOffset.UtcNow);
                resolved++;
            }
        }
        return resolved;
    }

    /// <summary>
    /// XML 侧快照（C-1）：共享层映射 claim（Host MapTargetStateClaim）的
    /// lineage 携带节点身份与 guard——xml-map:{localId} / xml-checkable:{b} /
    /// xml-unique:true（映射只在唯一最佳匹配时发射，构造保证 IdentityMatched）。
    /// dump 时序 = claim 的 CaptureTime（D14）。无 XML 侧 → null（缺席即数据）。
    /// </summary>
    private static World.ConflictResolver.XmlAuthoritySnapshot? SnapshotFromLineage(
        Evidence.EvidenceRecord? xmlRecord, string? xmlValue)
    {
        if (xmlRecord is null || xmlValue is null)
            return null;
        var localId = xmlRecord.Provenance.TransformationLineage
            .FirstOrDefault(l => l.StartsWith("xml-map:", StringComparison.Ordinal))?["xml-map:".Length..];
        var checkable = xmlRecord.Provenance.TransformationLineage
            .Any(l => l == "xml-checkable:true");
        var unique = xmlRecord.Provenance.TransformationLineage
            .Any(l => l == "xml-unique:true");
        var checkedRaw = xmlValue switch { "on" => "true", "off" => "false", var v => v };
        return new World.ConflictResolver.XmlAuthoritySnapshot(
            localId ?? "(unknown)", unique, xmlRecord.Provenance.CaptureTime,
            checkable, checkedRaw, Enabled: null, Selected: null, Focused: null);
    }

    // ---- RFS-001 D20：Kernel 级 activation latch（composition/lifecycle 协调态）----

    /// <summary>
    /// Run 是否已 legal activation（RFS-001 D20 / 评审 S4）。本 latch 是
    /// UniKernel 的 INTERNAL 组合/生命周期协调状态——协议通则 0.1.10 允许
    /// Kernel 持有非 canonical 的 lifecycle 编排态；RunModel 仍是唯一 Run
    /// Authority，本 latch 不是 canonical Run State（baseline §24.1 不变量 44
    /// 的 Kernel 侧执行面，非 Run 真相）。
    /// </summary>
    internal bool IsActivated
    {
        get
        {
            lock (_activationSync)
                return _activationOwner is not null;
        }
    }

    /// <summary>
    /// Legal activation gate（P24）：一次性幂等 lifecycle command。RunView
    /// 为 null（无 accepted contract）→ fail closed (false, false)；首次调用
    /// 置位 IsActivated 并返回 (true, false)；后续调用返回 (true, true)
    /// （零副作用：不创建第二 Run、不重放 Effect）。同一 UniKernel 上的多个
    /// internal driver 实例共享本 gate（RFS-001 D20）。
    /// </summary>
    internal (bool Accepted, bool AlreadyActivated) ActivateGate(object driverIdentity)
    {
        ArgumentNullException.ThrowIfNull(driverIdentity);
        lock (_activationSync)
        {
            if (RunView is null)
                return (false, false);
            if (_activationOwner is not null)
                return (true, true);
            _activationOwner = driverIdentity;
            return (true, false);
        }
    }

    /// <summary>
    /// 只有完成首次 legal activation 的 driver 才拥有该 Run 的 execution
    /// lease。重复 activation 只返回同一 Run 关联，不转移 lease，也不能让第二
    /// driver 从自身 phase 重新开始执行。
    /// </summary>
    internal bool IsActivationOwner(object driverIdentity)
    {
        ArgumentNullException.ThrowIfNull(driverIdentity);
        lock (_activationSync)
            return ReferenceEquals(_activationOwner, driverIdentity);
    }

    /// <summary>当前 accepted Contract View（未接受 contract 时为 null）。</summary>
    public ExecutionContractView? RunView => _run?.View;

    /// <summary>RunId（admission 铸造后非空；否则空串）。</summary>
    public string RunId => _run?.RunId ?? string.Empty;

    /// <summary>Run 是否已 terminal。</summary>
    public bool IsRunTerminal => _run?.IsTerminal ?? false;

    /// <summary>Current canonical Run State（只读透传）。</summary>
    public RunState? RunState => _run?.State;

    /// <summary>Effect Receipt log（只读透传；Effect Boundary 仍是唯一 owner）。</summary>
    public IReadOnlyList<EffectReceipt> EffectReceipts =>
        _effects?.ReceiptLog ?? Array.Empty<EffectReceipt>();

    /// <summary>
    /// 处理一条观察输入：
    /// rejected → 短路（无 relevance、无 reconciliation、零 belief 变化）；
    /// accepted + irrelevant → 保留 canonical record，不产 revision；
    /// accepted + relevant → Reconciliation（幂等）。
    /// UIW-001（P22 / ADR-0012）：可选 TransitionContext 仅作为 Container
    /// Association 的 non-evidentiary prior 随 relevant evidence 进入；
    /// 无 relevant evidence 时无任何作用路径。
    /// terminal 后仍可 admission / reconciliation（historical evidence 可追加），
    /// 但不得恢复 terminal Run（任务 九.H，E2B 路径不变）。
    /// </summary>
    public KernelResult Process(ObservationProposal observation, TransitionContext? transitionContext = null)
    {
        var runRefs = RunRefs();

        // evidence.admit（TRC-001 binding；trace 故障全吸收，不改变行为）
        var admitSpan = StartTraced(TraceCatalog.EvidenceAdmit, parent: null, runRefs);
        AdmissionRecord admission;
        EvidenceRecord? record;
        var admitStart = Stopwatch.GetTimestamp();
        try
        {
            (admission, record) = _ledger.Admit(observation);
        }
        catch (Exception)
        {
            TryComplete(admitSpan, StructuralOutcome.Faulted);
            throw;
        }
        // LAT-001：量化观察（成功路径；异常由 catch 上抛不记录）
        _metrics?.Record(RuntimeStage.EvidenceAdmission, Stopwatch.GetTimestamp() - admitStart,
            inputSize: 1, outputSize: record is null ? 0 : 1);
        _metrics?.CountAdmission(admission.Decision == AdmissionDecision.Accepted);

        // fail-closed 短路（验收 4）：rejected 不得触达 Relevance / Reconciliation
        if (admission.Decision != AdmissionDecision.Accepted || record is null)
        {
            TryRecord(admitSpan, TraceCatalog.AdmissionRejected, Array.Empty<TraceReference>(), admission.RejectionReason);
            TryComplete(admitSpan, StructuralOutcome.Completed); // domain 拒绝 ≠ trace failure
            return new KernelResult(admission, Relevance: null, ResultingRevision: null);
        }

        TryRecord(admitSpan, TraceCatalog.Admitted,
            new[] { new TraceReference(TraceReferenceKind.Evidence, record.EvidenceId) }, reasonCode: null);
        TryComplete(admitSpan, StructuralOutcome.Completed);

        // Belief Relevance 判定（验收 1）：与 Admission 是两个独立产出
        // （world.judge-relevance 为 provisional 词表项，本 bullet 不埋点）
        var relevance = _world.JudgeRelevance(record);

        // irrelevant（验收 3）：canonical record 保留，不要求 revision
        if (!relevance.IsRelevant)
            return new KernelResult(admission, relevance, ResultingRevision: null);

        var before = _world.Current;
        var reconcileSpan = StartTraced(TraceCatalog.WorldReconcile, parent: admitSpan.Context, runRefs);
        WorldBeliefRevision revision;
        var reconcileStart = Stopwatch.GetTimestamp();
        try
        {
            revision = _world.Reconcile(record, relevance, transitionContext);
        }
        catch (Exception)
        {
            TryComplete(reconcileSpan, StructuralOutcome.Faulted);
            throw;
        }
        var reconcileTicks = Stopwatch.GetTimestamp() - reconcileStart;

        // 幂等（验收 8）：reconcile 未产生新 revision 时如实报告
        var resulting = ReferenceEquals(revision, before) ? null : revision;
        // LAT-001：量化观察（input = parent world-state entries scanned；
        // output = 是否产生新 revision）
        _metrics?.Record(RuntimeStage.WorldReconciliation, reconcileTicks,
            inputSize: before?.WorldState.Count ?? 0, outputSize: resulting is not null ? 1 : 0);
        _metrics?.CountReconciliation(resulting is not null);
        TryRecord(reconcileSpan,
            resulting is null ? TraceCatalog.ReconcileIdempotent : TraceCatalog.Reconciled,
            resulting is null
                ? new[] { new TraceReference(TraceReferenceKind.Evidence, record.EvidenceId) }
                : new[]
                {
                    new TraceReference(TraceReferenceKind.Evidence, record.EvidenceId),
                    new TraceReference(TraceReferenceKind.WorldRevision, resulting.RevisionId),
                },
            reasonCode: null);
        TryComplete(reconcileSpan, StructuralOutcome.Completed);
        return new KernelResult(admission, relevance, resulting);
    }

    /// <summary>trace 观察面 fail-safe：StartOperation 抛错 → no-op scope（acceptance 2）。</summary>
    private ITraceOperationScope StartTraced(
        SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references)
    {
        try
        {
            return _trace.StartOperation(definition, parent, references);
        }
        catch (Exception)
        {
            return NoOpOperationScope.Instance;
        }
    }

    private static void TryRecord(
        ITraceOperationScope span, TraceEventDefinition eventDefinition, IReadOnlyList<TraceReference> references, string? reasonCode)
    {
        try
        {
            span.Record(eventDefinition, references, reasonCode);
        }
        catch (Exception)
        {
            // acceptance 2：trace 故障不得改变 Runtime 行为
        }
    }

    private static void TryComplete(ITraceOperationScope span, StructuralOutcome outcome)
    {
        try
        {
            span.Complete(outcome);
        }
        catch (Exception)
        {
            // acceptance 2：trace 故障不得改变 Runtime 行为
        }
    }

    /// <summary>Slice 派生透传（UIW-004：container-anchored 新形状）。
    /// LAT-001：组合缝观察——world.derive-slice span + 规模计数
    /// （input = occurrences scanned；secondary = world-state entries
    /// scanned；output = slice occurrences）。</summary>
    public Slice DeriveSlice(string rootContainerId, IReadOnlyList<string>? inScopeContainerIds = null)
    {
        var current = _world.Current;
        var span = StartTraced(TraceCatalog.WorldDeriveSlice, parent: null, RevisionRefs(current));
        var start = Stopwatch.GetTimestamp();
        try
        {
            var slice = _world.DeriveSlice(rootContainerId, inScopeContainerIds);
            var ticks = Stopwatch.GetTimestamp() - start;
            TryComplete(span, StructuralOutcome.Completed);
            _metrics?.Record(RuntimeStage.SliceDerivation, ticks,
                inputSize: current?.Occurrences?.Count ?? 0,
                secondarySize: current?.WorldState.Count ?? 0,
                outputSize: slice.Occurrences.Count);
            return slice;
        }
        catch (Exception)
        {
            TryComplete(span, StructuralOutcome.Faulted);
            throw;
        }
    }

    /// <summary>Run 引用（组合缝 span references 公共构造）。</summary>
    private IReadOnlyList<TraceReference> RunRefs() =>
        _run is { RunId.Length: > 0 } runModel
            ? new[] { new TraceReference(TraceReferenceKind.Run, runModel.RunId) }
            : Array.Empty<TraceReference>();

    private IReadOnlyList<TraceReference> RevisionRefs(WorldBeliefRevision? current) =>
        current is null
            ? RunRefs()
            : RunRefs().Append(new TraceReference(TraceReferenceKind.WorldRevision, current.RevisionId)).ToArray();

    /// <summary>Slice 有效性透传。</summary>
    public bool IsSliceValid(Slice slice) => _world.IsSliceValid(slice);

    /// <summary>
    /// 不变量 43 的组合缝：Kernel 只派生当前 scoped Slice 并把本轮 P2/P3 结果
    /// 交给 Assurance；是否足以放行下一次现实 Effect 由 Assurance 判断。
    /// </summary>
    internal PostActionEffectVerification VerifyPostActionEffect(
        TargetSpec target,
        IReadOnlyList<KernelResult> processedObservations,
        DateTimeOffset? dispatchTime = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(processedObservations);

        // PER-014 R2：四门等价的 typed 验证路由——语义 checked 权威域
        // （DesiredState ≠ null）∧ post belief 可经 R1 缝唯一解析目标
        // occurrence 的 typed checked claim ∧ capture 时序在 dispatch 之后
        // → typed 定案。不过门 / Unknown / Unsupported → 既有 occurrence
        // 视觉验证路径（typed 失去本次资格 ≠ 视觉必胜；fail-closed）。
        if (dispatchTime is { } dispatched && TryRouteTypedVerification(
                target, dispatched) is { } routed)
            return routed;

        var root = _world.Current?.Containers is { Count: 1 } containers
            ? containers[0].Identity.ContainerId
            : throw new InvalidOperationException("post-action verification requires one root container");
        var slice = DeriveSlice(root);
        return Assurance.VerifyPostActionEffect(
            new PostActionEffectVerificationInput(target, slice, processedObservations));
    }

    /// <summary>
    /// typed 验证路由执行（PER-014 R2）：经 <see cref="SemanticCheckedResolver"/>
    /// （R1 只读缝）对 post-action belief 解析目标 role 的 typed checked 值。
    /// 四门等价重述：
    /// ① 权威域 = DesiredState ≠ null（Click 型非状态权威域 → 视觉路径）；
    /// ② 身份唯一 = occurrence 解析恰一（缝内零/多候选 → Unknown → 回视觉）；
    /// ③ 属性有效 = typed claim 仅在 parser 能力/ExactProof 执法通过时发射
    ///    （TypedHierarchyProposalProjector fail-closed），Observed 即证明；
    ///    capability 缺席 → Unsupported → fail-closed 回视觉；
    /// ④ 时序 = capture timestamp 严格晚于 dispatchTime。
    /// 值判定：CheckedState 与 typed DesiredState 相等才 verified；Partial
    /// 不通过且不回视觉（explicit insufficient-evidence）；Unchecked/Checked
    /// 不等 = 如实未验证（mismatch 留档）。任一 Unknown/Unsupported → 返回
    /// null 回 occurrence 视觉路径（fail-closed，不猜值）。
    /// </summary>
    private PostActionEffectVerification? TryRouteTypedVerification(
        TargetSpec target,
        DateTimeOffset dispatchTime)
    {
        // 门①：字段权威域（无期望终态 = Click 型，非语义 checked 权威域）。
        if (target.DesiredState is null)
            return null;

        var current = _world.Current;
        if (current is null)
            return null;

        var resolution = SemanticCheckedResolver.ResolveDetailed(
            current,
            new World.UiRealization.TargetDescriptor(target.Role, target.SemanticDescriptor),
            _ledger.CanonicalRecords);

        // 门②/③：occurrence 唯一解析 + typed claim 在案。Unknown（无 claim /
        // 歧义 / capture 不可判）与 Unsupported（capability 缺席）一律
        // fail-closed 回视觉路径（不猜测、不降级）。
        if (!resolution.IsObserved)
            return null;

        // 门④：时序——capture 必须严格晚于 dispatch（事后新鲜度）。
        if (resolution.CaptureTimestamp is not { } captureTime || captureTime <= dispatchTime)
            return null;

        var resolved = resolution.Value.Value!;
        if (resolved == CheckedState.Partial)
        {
            // Partial：tri-state 语义，不得折叠为 off/true——显式证据不足。
            return new PostActionEffectVerification(
                current.RevisionId, target, IsVerified: false,
                new[]
                {
                    new AssuranceCheck("typed-route-four-gates", true),
                    new AssuranceCheck("typed-checked-matches-desired", false),
                },
                $"typed-route: insufficient-evidence (partial at {resolution.CaptureId})");
        }

        var verified = resolved == target.DesiredState;
        return new PostActionEffectVerification(
            current.RevisionId, target, verified,
            new[]
            {
                new AssuranceCheck("typed-route-four-gates", true),
                new AssuranceCheck("typed-checked-matches-desired", verified),
            },
            verified ? null : $"typed-route: resolved={resolved} desired={target.DesiredState}");
    }

    private RunModel Run => _run ?? throw new InvalidOperationException("Uni Kernel 未组合 Run Model");

    private ControlLoop Control => _control ?? throw new InvalidOperationException("Uni Kernel 未组合 Control Loop");

    private RuntimeAssurance Assurance => _assurance ?? throw new InvalidOperationException("Uni Kernel 未组合 Assurance");

    private EffectBoundary Effects => _effects ?? throw new InvalidOperationException("Uni Kernel 未组合 Effect Boundary");

    /// <summary>
    /// Execution Contract admission（Run Model 唯一写入路径）+ ESO-001 组合缝
    /// 扩展：admission accepted 后，对 canonical Run State 中每个带 EntityScope
    /// 的 obligation 登记 descriptor-scoped standing ContinuityDemand（P23
    /// producer ②；无 anchor，D3）。DemandId 由 obligation identity 派生
    /// （D2/D4：同 contract 重复 admit 复用同 demand——幂等由
    /// RegisterContinuityDemand 同 DemandId 复用保证）；rejected → 零登记。
    /// Run Model 不取得对 WorldModel 的直连边（L0 边界不破）。
    /// </summary>
    public ContractAdmission AdmitContract(ExecutionContract contract)
    {
        var admission = Run.AdmitContract(contract);
        if (admission.Accepted)
        {
            foreach (var scope in Run.State!.ProofObligations.Obligations
                         .Where(o => o.EntityScope is not null)
                         .Select(o => (ObligationId: o.ObligationId, Descriptor: o.EntityScope!)))
            {
                _world.RegisterContinuityDemand(new ContinuityDemand(
                    DemandId: $"demand-obl-{scope.ObligationId}",
                    SourceKind: ContinuityDemandSourceKind.EntityScopedObligation,
                    OwningContainerId: scope.Descriptor.OwningContainerId,
                    Role: scope.Descriptor.Role,
                    SemanticDescriptor: scope.Descriptor.SemanticDescriptor,
                    AnchorOccurrenceId: null, AnchorRevisionId: null, LogicalItemId: null));
            }
        }
        return admission;
    }

    /// <summary>
    /// 一个 control cycle（Target §19 输入）：Contract View + Slice →
    /// Control Loop 签发 intent；Run Model 记录 cycle 推进（typed
    /// transition）。terminal 后 fail-closed（intent 签发即冻结，不得
    /// 重新打开 Run；§17）。EXP-008 / ADR-0011：Run State 不再进入
    /// Control 输入（P5 no-current-buyer / deferred）。
    /// </summary>
    public ControlIntent SelectIntent(Slice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        if (Run.IsTerminal)
            throw new InvalidOperationException("Run 已 terminal，不可再签发 Control Intent（不得重新打开 Run）");
        var view = Run.View ?? throw new InvalidOperationException("尚无已接受的 Execution Contract");
        var intent = Control.SelectIntent(view, slice);
        Run.RecordCycle();
        return intent;
    }

    /// <summary>
    /// Act pipeline（CBA-005 目标序，ADR-0009）：已签发 act-intent →
    /// Effect Boundary canonical binding（四态拒绝即短路：无 judgment、
    /// 无 gate、零 effect 副作用）→ Assurance 针对该 canonical binding 的
    /// action-local judgment（三元组 correlation）→ Gate 执法（judgment
    /// 拒绝也执行 enforcement 并拒绝——binding validity 不因此消失）+
    /// 机械投递 → Effect Receipt → attempt evidence 经既有 admission
    /// 路径回流（P3）+ action-local 结果通知 + Run Model 记录 act。
    /// terminal 后 fail-closed：新 Control Intent 即使存在也不能变成
    /// effect（不变量 42，任务 八）。
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
        _ = _world.Current ?? throw new InvalidOperationException("尚无 WorldBelief revision");

        // EXP-008 / ADR-0011：WorldBelief 消费面 = Owner 即时派生的
        // consumer view（ephemeral，单次 act 内用毕即弃），不再整传
        // WorldBeliefRevision 聚合。UIW-004：UI candidate 的 owner fact =
        // UiTarget.OccurrenceId ∈ 当前 occurrence 投影（HasTargetOccurrence）
        var bindingView = _world.DeriveBindingView(
            candidate?.TargetSubject, candidate?.UiTarget?.OccurrenceId);

        // 1) Canonical binding（Effect Boundary 唯一认定；四态拒绝即短路）
        var binding = Effects.Bind(intent, candidate, bindingView);
        if (binding.Canonical is not { } canonical)
            return new ActResult(Judgment: null, binding, Gate: null, Receipt: null,
                AttemptEvidenceReflux: null, Transition: null);

        // 2) Assurance judgment（针对 canonical binding；ADR-0009）
        var judgment = Assurance.Judge(intent, canonical, view, _world.DeriveActionAssuranceView(intent.TargetSubject));

        // 3) Gate 执法 + Dispatch（judgment 拒绝也执法并拒绝——D6；
        //    拒绝即零 effect 副作用，decision 留痕非副作用）
        var (gate, receipt) = Effects.Dispatch(canonical, judgment, bindingView);

        // 4) Receipt 已在 Owner log 留痕；回流走既有 E2B 路径（P3，验收 8）。
        //    AttemptReport reflux 不携带 TC：attempt evidence 是 kind-gated
        //    irrelevant 路径（P22 无作用面，ADR-0012）
        KernelResult? reflux = null;
        TransitionContext? transition = null;
        if (receipt is not null)
        {
            Assurance.NoteOutcome(receipt);
            Control.NoteDispatchOutcome(receipt);
            reflux = Process(Effects.ExportAttemptEvidence(receipt));
            // P22 producer（PER-003 / D1）：dispatch 已发生 → 导出 non-evidentiary
            // 上下文供 association prior 消费（不 establish Matched/New）
            transition = Effects.ExportTransitionContext(canonical.EffectClass, receipt);
            Run.RecordAction();
        }

        return new ActResult(judgment, binding, gate, receipt, reflux, transition);
    }

    /// <summary>
    /// 最小接地编排缝（CTL-001 / 协议 deferred ① 的最小 realization，不改
    /// 驱动权语义，D3）：ResolveCurrent(descriptor) → UniqueCandidate 才构造
    /// UiTarget candidate（SourceRevisionId 对齐 view）→ Act(intent, candidate)。
    /// 非 Unique（NoCandidate / MultipleCandidates / ScopeProjectionUnavailable）
    /// → Act=null + View 原样上抛，不强制选择（P-UW-35：Identity never
    /// creates information）。intent 校验与 Act 内既有 fail-closed 全适用
    /// （经 Act(intent, candidate) 复用）。
    /// </summary>
    public GroundedActResult ActViaCurrentGrounding(ControlIntent intent, TargetDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(descriptor);

        // LAT-001：world.resolve-current 组合缝观察（input = occurrences
        // scanned；output = matched candidates）
        var current = _world.Current;
        var span = StartTraced(TraceCatalog.WorldResolveCurrent, parent: null, RevisionRefs(current));
        CurrentGroundingView view;
        var start = Stopwatch.GetTimestamp();
        try
        {
            view = _world.ResolveCurrent(descriptor);
        }
        catch (Exception)
        {
            TryComplete(span, StructuralOutcome.Faulted);
            throw;
        }
        var ticks = Stopwatch.GetTimestamp() - start;
        TryComplete(span, StructuralOutcome.Completed);
        _metrics?.Record(RuntimeStage.CurrentGrounding, ticks,
            inputSize: current?.Occurrences?.Count ?? 0,
            outputSize: view.Candidates.Count);

        if (view.Result != CurrentCandidateSetResultKind.UniqueCandidate)
            return new GroundedActResult(view, Act: null);

        var fact = view.Candidates.Single();
        // UI 通道 candidate（UIW-004 恒绑 occurrence 引用）：经 ForUiTarget 工厂
        // 构造——字符串通道字段对 UI 无作用（Bind 只读 UiTarget），null 压制
        // 收拢在工厂内（RVR-001 F3）。
        var candidate = CandidateBinding.ForUiTarget(
            new UiTargetReference(fact.OccurrenceId, view.SourceRevisionId));
        return new GroundedActResult(view, Act(intent, candidate));
    }

    /// <summary>
    /// Terminal 编排（OUT-003；终局链的 Uni Kernel 段）：Assurance 形成
    /// Outcome Proof → Run Model 记录 terminal Outcome State（exact-prior
    /// CAS）→ 接受后关闭 Effect Boundary delivery → 从 canonical Outcome
    /// State 投影 immutable Runtime Outcome（exactly once）。证据不足时
    /// 返回无 proof（保持 non-terminal，不猜测分类）；已 terminal / 竞争
    /// <summary>
    /// RUN-004 裁决⑧ G2（终局收口）：折抵目标 = <b>未世界满足</b>的 mandatory
    /// 义务。与层1 判定同源（RuntimeAssurance.EvaluateObligations 权威，同一
    /// beliefView 派生）——已满足的义务不重复入证（避免对已满足义务写
    /// subject claim 污染 belief）。义务是否满足始终由 Kernel authority
    /// 判定，completion anchor 只是进入折抵/裁决的门槛。
    /// </summary>
    internal IReadOnlyList<RunObligation> UnsatisfiedMandatoryObligations()
    {
        var state = Run.State ?? throw new InvalidOperationException("Run State 尚未建立（无已接受 contract）");
        var view = Run.View ?? throw new InvalidOperationException("尚无已接受的 Execution Contract");
        _ = _world.Current ?? throw new InvalidOperationException("尚无 WorldBelief revision");
        var beliefView = _world.DeriveOutcomeAssuranceView(
            state.ProofObligations.Obligations.Select(o => o.Subject),
            state.ProofObligations.Obligations
                .Where(o => o.EntityScope is not null)
                .Select(o => (o.ObligationId, o.EntityScope!, o.RequiredValue)),
            _ledger.CanonicalRecords);
        return Assurance.EvaluateObligations(state.ProofObligations, beliefView, _ledger.CanonicalRecords)
            .Where(s => s.Mandatory && !s.Satisfied)
            .Select(s => state.ProofObligations.Obligations.Single(o => o.ObligationId == s.ObligationId))
            .ToList();
    }

    /// <summary>
    /// Terminal 编排公开面：Kernel 独占 terminal 证明判断（不变量 22），
    /// 但 Run 状态记录与 effect 门控仍由 Run Model / Effect Boundary 拥有。
    /// 失败时不产生新的 Runtime Outcome。
    /// </summary>
    public TerminalEvaluation EvaluateTerminal()
    {
        var state = Run.State ?? throw new InvalidOperationException("Run State 尚未建立（无已接受 contract）");
        if (Run.IsTerminal)
            return new TerminalEvaluation(null, new OutcomeTransition(false, "already-terminal", null), null);

        var view = Run.View ?? throw new InvalidOperationException("尚无已接受的 Execution Contract");
        _ = _world.Current ?? throw new InvalidOperationException("尚无 WorldBelief revision");

        // 1) Assurance 独占 Outcome Proof judgment（不变量 22；Kernel 不重判）
        //    EXP-008：belief 消费面 = OutcomeAssuranceView（claims/conflicts
        //    按 obligation subjects scope，Owner 即时派生）
        var beliefView = _world.DeriveOutcomeAssuranceView(
            state.ProofObligations.Obligations.Select(o => o.Subject),
            // ESO-002：entity-scoped obligation (id, EntityScope, RequiredValue)
            // 传入 view 派生（PER-014 R3：typed checked 缝，canonical 供时序）
            state.ProofObligations.Obligations
                .Where(o => o.EntityScope is not null)
                .Select(o => (o.ObligationId, o.EntityScope!, o.RequiredValue)),
            _ledger.CanonicalRecords);
        var proof = Assurance.JudgeOutcome(view, state.ProofObligations, beliefView, _ledger.CanonicalRecords);
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

        // TRC-001 A8（评审二轮 Standards 2）：emission 锚点——真实 envelope
        // 构造完成（exactly-once 点）经 internal sink 标记；与 span 埋点
        // 同一信任锚，公共 IRunTrace 面无标记能力
        MarkTraceOutcomeEmitted();

        return new TerminalEvaluation(proof, transition, outcome);
    }

    /// <summary>emission 观测标记只经 internal sink（组合缝信任锚）；
    /// 非 sink 的 trace 替身（如测试 throwing double）静默跳过。</summary>
    private void MarkTraceOutcomeEmitted()
    {
        try
        {
            (_trace as IRunTraceSink)?.MarkOutcomeEmitted();
        }
        catch (Exception)
        {
            // acceptance 2：trace 故障不得改变 Runtime 行为
        }
    }
}
