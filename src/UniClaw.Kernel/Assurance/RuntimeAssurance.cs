using System.Collections.ObjectModel;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

/// <summary>
/// Runtime Assurance — sole Runtime Assurance Judgment Authority（Target
/// §15，不变量 22），包括 Outcome Proof Authority。本切片含：
/// action-local 判定（Judge）+ Evidence 支撑的 obligation 满足判定
/// （EvaluateObligations）+ terminal Outcome Proof（JudgeOutcome，四分类）。
/// 不拥有 Tactical Hypothesis、Run State、WorldBelief、canonical binding
/// 或 mechanical dispatch。
/// </summary>
public sealed class RuntimeAssurance
{
    private readonly IFreshnessEvaluator _freshnessEvaluator;
    private readonly Dictionary<string, int> _failedAtRevisionNumber = new();
    private readonly List<AssuranceJudgment> _judgments = new();
    private readonly List<OutcomeProof> _outcomeProofs = new();
    private readonly ReadOnlyCollection<AssuranceJudgment> _judgmentLogView;
    private readonly ReadOnlyCollection<OutcomeProof> _outcomeProofLogView;

    /// <summary>evaluator 未配置 = composition/configuration error（fail-fast），
    /// 不是 runtime Unknown（FRS-007 D3：两类 absence 不混）。</summary>
    public RuntimeAssurance(IFreshnessEvaluator freshnessEvaluator)
    {
        _freshnessEvaluator = freshnessEvaluator ?? throw new ArgumentNullException(nameof(freshnessEvaluator));
        _judgmentLogView = _judgments.AsReadOnly();
        _outcomeProofLogView = _outcomeProofs.AsReadOnly();
    }

    /// <summary>每次 action-local judgment 的留痕（append-only，immutable 产物）。</summary>
    public IReadOnlyList<AssuranceJudgment> JudgmentLog => _judgmentLogView;

    /// <summary>每次 Outcome Proof 判断的留痕（append-only；唯一证明 Authority）。</summary>
    public IReadOnlyList<OutcomeProof> OutcomeProofLog => _outcomeProofLogView;

    /// <summary>
    /// Action-local judgment（Target §19 输入；ADR-0009 目标序
    /// Bind→Judge→Gate：Judge 审的对象是 CanonicalBinding，不再是
    /// candidate）。三元组 (IntentId, BindingId, RevisionId) 是 correlation
    /// key——RevisionId 记 Binding.RevisionId（"审的是这个 binding"，D4），
    /// 与 current 相符由 binding-revision-currentness 检查验证。null
    /// binding = API contract violation（CBA-005 D3），不产生 judgment。
    /// FRS-007：freshness sufficiency 的唯一执法点（D4）——消费时经注入
    /// evaluator 判定，Insufficient / Unknown 都 fail-closed。
    /// 任一检查失败 → 拒绝（RejectionReason = 首个失败项），fail-closed。
    /// </summary>
    public AssuranceJudgment Judge(
        ControlIntent intent,
        CanonicalBinding binding,
        ExecutionContractView view,
        WorldBeliefRevision current)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(current);

        var target = intent.TargetSubject;
        // FRS-007（ADR-0010）：freshness = Freshness Basis × Consumption
        // Requirement 的消费相对判断——World Model 表达 basis，此处消费时
        // 判定 sufficiency；结果随 judgment 携带，只对该次消费有效
        var freshness = _freshnessEvaluator.Evaluate(new FreshnessEvaluationInput(
            current.FreshnessBasis,
            current.RevisionId,
            new ConsumptionRequirement(intent.TargetSubject, intent.EffectClass)));
        var checks = new List<AssuranceCheck>
        {
            new("intent-is-act", intent.Kind == ControlIntentKind.Act),
            new("effect-class-allowed",
                intent.EffectClass is not null && view.AllowedEffects.Contains(intent.EffectClass)),
            new("effect-class-not-forbidden",
                intent.EffectClass is null || !view.ForbiddenEffects.Contains(intent.EffectClass)),
            new("target-declared", !string.IsNullOrWhiteSpace(target)),
            new("binding-intent-correlation", binding.IntentId == intent.IntentId),
            new("binding-revision-currentness", binding.RevisionId == current.RevisionId),
            new("no-unresolved-conflict",
                target is null || !current.Conflicts.Any(c => c.Subject == target)),
            new("intent-basis-currentness", intent.BasisRevisionId == current.RevisionId),
            // FRS-007 D4（唯一执法点）：freshness sufficiency 与 currentness
            // 是两个独立维度——revision 仍 current 也可 Insufficient/Unknown
            // （Scenario 14）；Passed = Sufficient，Insufficient 与 Unknown
            // 都 fail-closed，三态区分在 FreshnessJudgment 自身
            new("freshness-sufficiency", freshness.Sufficiency == FreshnessSufficiency.Sufficient),
            new("no-blind-retry",
                target is null
                || !_failedAtRevisionNumber.TryGetValue(target, out var failedAt)
                || current.RevisionNumber > failedAt),
        };

        var judgment = checks.All(c => c.Passed)
            ? new AssuranceJudgment(
                intent.IntentId, binding.BindingId, binding.RevisionId,
                IsAdmissible: true, checks, RejectionReason: null, Freshness: freshness)
            : new AssuranceJudgment(
                intent.IntentId, binding.BindingId, binding.RevisionId,
                IsAdmissible: false, checks, checks.First(c => !c.Passed).Name, freshness);
        _judgments.Add(judgment);
        return judgment;
    }

    /// <summary>
    /// Dispatch 结果通知：失败时更新 action-local retry 记忆（P4）。
    /// 该状态只在 judgment lifecycle 内有效，不进入 canonical Run State。
    /// </summary>
    public void NoteOutcome(EffectReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Outcome != DispatchOutcome.Failed)
            return;

        _failedAtRevisionNumber.TryGetValue(receipt.TargetSubject, out var existing);
        _failedAtRevisionNumber[receipt.TargetSubject] = Math.Max(existing, receipt.RevisionNumber);
    }

    /// <summary>
    /// 逐条 obligation 的满足判定（Assurance 权威；供观察与 JudgeOutcome 内部
    /// 使用）。满足 = current revision 内存在 accepted Evidence 支持的
    /// subject=value claim（WorldState 或 Conflicts 携带该值，backing
    /// EvidenceId ∈ basis）。MaterialEffect 判定门（ING-006 D7）：accepted
    /// Observation ∧ ObservationContext=PostActionEffectFlow（kind 门先于
    /// context 门，AttemptReport 永不满足）。
    /// </summary>
    public IReadOnlyList<ObligationStatus> EvaluateObligations(
        ProofObligationState obligations,
        WorldBeliefRevision current,
        IReadOnlyDictionary<string, EvidenceRecord> canonical)
    {
        ArgumentNullException.ThrowIfNull(obligations);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(canonical);

        return obligations.Obligations
            .Select(o =>
            {
                var backing = ResolveBackingEvidence(o, current, canonical);
                return new ObligationStatus(
                    o.ObligationId, o.Kind, o.Mandatory,
                    Satisfied: backing is not null, backing);
            })
            .ToList();
    }

    /// <summary>
    /// Outcome Proof judgment（Target §15.2，不变量 22/37）：判断 Run-level
    /// Proof Obligation State 是否具备足够 accepted Evidence 支持具体
    /// terminal claim。分类优先级（D5）：mandatory Failure → SafeStop →
    /// Escalation → 全部 mandatory satisfied → Completion；均不成立 → 返回
    /// null（证据不足，不猜测分类，保持 non-terminal）。判断产物 append-only
    /// 留痕；immutable；写入 Outcome State 后冻结引用（§17）。
    /// </summary>
    public OutcomeProof? JudgeOutcome(
        ExecutionContractView view,
        ProofObligationState obligations,
        WorldBeliefRevision current,
        IReadOnlyDictionary<string, EvidenceRecord> canonical)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(obligations);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(canonical);

        var statuses = EvaluateObligations(obligations, current, canonical);
        var mandatory = statuses.Where(s => s.Mandatory).ToList();

        var effectEvidenceIds = statuses
            .Where(s => s.Satisfied && s.Kind == RunObligationKind.MaterialEffect)
            .Select(s => s.BackingEvidenceId!)
            .ToHashSet();
        var situationEvidenceIds = statuses
            .Where(s => s.Satisfied && s.Kind is RunObligationKind.Failure or RunObligationKind.SafeStop or RunObligationKind.Escalation)
            .Select(s => s.BackingEvidenceId!)
            .ToHashSet();

        OutcomeProof ProofOf(TerminalClassification classification, string reason)
        {
            var proof = new OutcomeProof(
                $"proof-{current.RevisionId}-{_outcomeProofs.Count + 1}",
                classification, statuses,
                BasisEvidenceIds: current.EvidenceBasis,
                effectEvidenceIds, situationEvidenceIds,
                UnresolvedUncertainty: current.Uncertainty.ConflictingClaimCount,
                reason);
            _outcomeProofs.Add(proof);
            return proof;
        }

        // 分类优先级（D5）：非成功终局由 situation obligation 的
        // evidence-backed 满足触发；成功终局要求全部 mandatory 满足。
        if (mandatory.Any(s => s.Kind == RunObligationKind.Failure && s.Satisfied))
            return ProofOf(TerminalClassification.Failure, "mandatory-failure-evidence");
        if (mandatory.Any(s => s.Kind == RunObligationKind.SafeStop && s.Satisfied))
            return ProofOf(TerminalClassification.SafeStop, "mandatory-safe-stop-evidence");
        if (mandatory.Any(s => s.Kind == RunObligationKind.Escalation && s.Satisfied))
            return ProofOf(TerminalClassification.Escalation, "mandatory-escalation-evidence");
        // 成功终局要求「存在 mandatory」且全部满足——空 mandatory 集不得
        // 因 vacuous truth 伪装成 completion（Review F2，任务 四.4）
        if (mandatory.Count > 0 && mandatory.All(s => s.Satisfied))
            return ProofOf(TerminalClassification.Completion,
                $"all-mandatory-run-obligations-satisfied:objective:{view.Objective}");

        // 证据不足：既不能证明 success 也不能证明 failure → 不猜测（任务 九.E）
        return null;
    }

    /// <summary>
    /// 解析支撑 obligation 声称的 subject=value claim 的 accepted EvidenceId；
    /// 找不到返回 null。匹配来源：current revision 的 WorldState 或 Conflicts
    /// （E2B conflict 词汇：Challenging/Established 双方值都算 evidence-backed
    /// claim，backing id 必须 ∈ basis）。MaterialEffect 判定门（ING-006
    /// D3/D7）：kind=Observation ∧ ObservationContext=PostActionEffectFlow
    /// ——kind 门先于 context 门，AttemptReport 无论 context 永不满足
    /// （⑤a）；判定门是语义归类门，不是真实性门（⑤b，Deferred ⑦）。
    /// </summary>
    private static string? ResolveBackingEvidence(
        RunObligation obligation,
        WorldBeliefRevision current,
        IReadOnlyDictionary<string, EvidenceRecord> canonical)
    {
        if (current.WorldState.TryGetValue(obligation.Subject, out var claim)
            && claim.Value == obligation.RequiredValue
            && current.EvidenceBasis.Contains(claim.EvidenceId)
            && ContextMatches(claim.EvidenceId))
            return claim.EvidenceId;

        foreach (var conflict in current.Conflicts)
        {
            if (conflict.Subject != obligation.Subject)
                continue;
            if (conflict.ChallengingValue == obligation.RequiredValue
                && current.EvidenceBasis.Contains(conflict.ChallengingEvidenceId)
                && ContextMatches(conflict.ChallengingEvidenceId))
                return conflict.ChallengingEvidenceId;
            if (conflict.EstablishedValue == obligation.RequiredValue
                && current.EvidenceBasis.Contains(conflict.EstablishedEvidenceId)
                && ContextMatches(conflict.EstablishedEvidenceId))
                return conflict.EstablishedEvidenceId;
        }

        return null;

        // ING-006：MaterialEffect fulfillment policy 从 producer-prefix 判断
        // 迁移为 accepted Observation + explicit PostActionEffectFlow context
        // （D7；原以 producer 前缀粗略收窄的语义职责改由 context 字段承担，
        // ProducerIdentity 不参与判定；真实性核验属 Deferred ⑦）
        bool ContextMatches(string evidenceId) =>
            obligation.Kind != RunObligationKind.MaterialEffect
            || (canonical.TryGetValue(evidenceId, out var record)
                && record.Kind == IngressKind.Observation
                && record.Context == ObservationContext.PostActionEffectFlow);
    }
}
