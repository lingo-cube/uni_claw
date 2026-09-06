using System.Collections.ObjectModel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Effects;

/// <summary>
/// Effect Boundary — sole Canonical Binding Authority 与 sole Effect
/// Delivery Authority（Target §16，不变量 23）。唯一拥有 candidate→
/// canonical binding 认定、Effect Gate（只执法）与 Dispatch。不拥有
/// strategy、replan、recovery、admissibility judgment、Effect
/// Verification 或 Outcome Proof。OUT-003：terminal 后 external effect
/// delivery 由本边界关闭（不变量 42，delivery latch）。
/// </summary>
public sealed class EffectBoundary
{
    private readonly IEffectDriver _driver;
    private readonly List<BindingDecision> _bindings = new();
    private readonly List<EffectReceipt> _receipts = new();
    private readonly ReadOnlyCollection<BindingDecision> _bindingLogView;
    private readonly ReadOnlyCollection<EffectReceipt> _receiptLogView;

    private bool _deliveryClosed;

    public EffectBoundary(IEffectDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _bindingLogView = _bindings.AsReadOnly();
        _receiptLogView = _receipts.AsReadOnly();
    }

    /// <summary>每次 binding 判定的留痕（append-only）。</summary>
    public IReadOnlyList<BindingDecision> BindingLog => _bindingLogView;

    /// <summary>每次 dispatch 的 receipt 留痕（append-only attempt evidence）。</summary>
    public IReadOnlyList<EffectReceipt> ReceiptLog => _receiptLogView;

    /// <summary>terminal 后是否已关闭 external effect delivery（不变量 42）。</summary>
    public bool IsDeliveryClosed => _deliveryClosed;

    /// <summary>
    /// 关闭 external effect delivery（Uni Kernel 在 terminal Outcome State
    /// 成立后调用；§3.5/§17/不变量 42）。幂等。关闭后任何 dispatch 请求
    /// fail-closed（gate reason: delivery-closed），不重判、不扩权。
    /// </summary>
    public void CloseDelivery() => _deliveryClosed = true;

    /// <summary>
    /// Binding path（Target §19：selected intent + candidate binding +
    /// WorldBelief Slice/current）。四态拒绝：no-candidate（CBA-005 D2）/
    /// stale-revision / ambiguous / unknown-target（D8）；canonical 必绑定
    /// current WorldBelief revision（不变量 24，验收 3）。
    /// </summary>
    public BindingDecision Bind(ControlIntent intent, CandidateBinding? candidate, WorldBeliefRevision current)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(current);

        if (candidate is null)
        {
            // CBA-005 D2：candidate 缺失 = 第四态拒绝（显式 decision，非异常）
            var missing = new BindingDecision(null, BindingRejectionReason.NoCandidate);
            _bindings.Add(missing);
            return missing;
        }

        BindingDecision decision;
        if (string.IsNullOrWhiteSpace(intent.EffectClass))
            throw new ArgumentException("act-intent 缺少 effect class，无法认定 binding", nameof(intent));
        if (candidate.SourceRevisionId != current.RevisionId)
            decision = new BindingDecision(null, BindingRejectionReason.StaleRevision);
        else if (candidate.IsAmbiguous)
            decision = new BindingDecision(null, BindingRejectionReason.Ambiguous);
        else if (!current.WorldState.ContainsKey(candidate.TargetSubject))
            decision = new BindingDecision(null, BindingRejectionReason.UnknownTarget);
        else
            decision = new BindingDecision(
                new CanonicalBinding(
                    $"bind-{intent.IntentId}-{candidate.TargetSubject}",
                    intent.IntentId, intent.EffectClass,
                    candidate.TargetSubject, candidate.TargetValue,
                    current.RevisionId, current.RevisionNumber),
                RejectionReason: null);

        _bindings.Add(decision);
        return decision;
    }

    /// <summary>
    /// Canonical binding 有效性 = 派生判定（无 event，D8/§17）：revision 仍为
    /// current，且该 binding 未被 dispatch 消费（两者都可从 append-only
    /// ReceiptLog / current revision 推出）。freshness 不参与本判定
    /// （FRS-007 D4 / ADR-0010：freshness 拒绝的是授权，≠ binding
    /// invalidation）。
    /// </summary>
    public bool IsBindingValid(CanonicalBinding binding, WorldBeliefRevision current)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(current);
        return binding.RevisionId == current.RevisionId
            && !_receipts.Any(r => r.BindingId == binding.BindingId);
    }

    /// <summary>
    /// Effect Gate + Dispatch：只执法既有 judgment（authorization +
    /// intent 匹配 + binding 派生有效性），fail-closed；通过才做机械
    /// 投递并产出 Effect Receipt（验收 4）。不重判、不改 target、不扩权。
    /// 同一 binding 只可投递一次（§17 dispatch 失效）。OUT-003：terminal
    /// 后 delivery 关闭，任何 dispatch 请求拒绝（不变量 42）。
    /// </summary>
    public (GateDecision Gate, EffectReceipt? Receipt) Dispatch(
        CanonicalBinding binding,
        AssuranceJudgment judgment,
        WorldBeliefRevision current)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(judgment);
        ArgumentNullException.ThrowIfNull(current);

        GateDecision gate;
        if (_deliveryClosed)
            gate = new GateDecision(false, "delivery-closed");
        else if (!judgment.IsAdmissible)
            gate = new GateDecision(false, "not-authorized");
        else if (judgment.IntentId != binding.IntentId)
            gate = new GateDecision(false, "judgment-binding-mismatch");
        else if (judgment.BindingId != binding.BindingId)
            gate = new GateDecision(false, "judgment-binding-id-mismatch");
        else if (judgment.RevisionId != binding.RevisionId)
            gate = new GateDecision(false, "judgment-revision-mismatch");
        else if (binding.RevisionId != current.RevisionId)
            gate = new GateDecision(false, "binding-stale");
        else if (_receipts.Any(r => r.BindingId == binding.BindingId))
            gate = new GateDecision(false, "binding-already-dispatched");
        else
            gate = new GateDecision(true, null);

        if (!gate.Allowed)
            return (gate, null);

        var result = _driver.Deliver(binding);
        var receipt = new EffectReceipt(
            $"receipt-{binding.BindingId}-{_receipts.Count + 1}",
            binding.IntentId, binding.BindingId, binding.TargetSubject,
            binding.RevisionId, binding.RevisionNumber,
            result.Outcome, result.Report, result.CompletedAt);
        _receipts.Add(receipt);
        return (gate, receipt);
    }

    /// <summary>
    /// Receipt → AttemptReport 表达（ING-006：kind 与 context 显式声明；
    /// `attempt.*` subject / producer 命名空间保留为描述性 provenance，
    /// 不再承担 kind 或 relevance 判定职责）。
    /// </summary>
    public ObservationProposal ExportAttemptEvidence(EffectReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new ObservationProposal(
            new ObservationClaim($"attempt.{receipt.TargetSubject}", receipt.Outcome.ToString().ToLowerInvariant()),
            IngressKind.AttemptReport,
            ObservationContext.PostActionEffectFlow,
            new Provenance(
                Producer: "effect.boundary",
                CaptureTime: receipt.DispatchedAt,
                Scope: $"scope:attempt.{receipt.TargetSubject}",
                TransformationLineage: new[] { $"dispatch:{receipt.ReceiptId}" }));
    }
}
