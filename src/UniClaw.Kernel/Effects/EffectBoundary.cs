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
    /// WorldBelief 消费面）。EXP-008 / ADR-0011：WorldBelief 消费面 =
    /// BindingView（Owner 派生的 ephemeral view，非 WorldBeliefRevision
    /// 聚合）。四态拒绝：no-candidate（CBA-005 D2）/ stale-revision /
    /// ambiguous / unknown-target（D8，从 HasTargetSubjectClaim fact
    /// 推出——判定权在本边界）；canonical 必绑定 current WorldBelief
    /// revision（不变量 24，验收 3，锚 view.RevisionId/RevisionNumber）。
    /// </summary>
    public BindingDecision Bind(ControlIntent intent, CandidateBinding? candidate, BindingView view)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(view);

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
        if (candidate.UiTarget is { } uiTarget)
        {
            // UIW-004 UI 通道：恒绑已解析 occurrence 引用，无字符串 fallback。
            // stale 校验以 UiTarget.SourceRevisionId 为准（candidate.
            // SourceRevisionId 与之不一致时以此为准——UiTarget 是 UI candidate
            // 的权威 revision 锚）。
            if (uiTarget.SourceRevisionId != view.RevisionId)
                decision = new BindingDecision(null, BindingRejectionReason.StaleRevision);
            else if (candidate.IsAmbiguous)
                decision = new BindingDecision(null, BindingRejectionReason.Ambiguous);
            else if (!view.HasTargetOccurrence)
                decision = new BindingDecision(null, BindingRejectionReason.UnknownTarget);
            else
                decision = new BindingDecision(
                    new CanonicalBinding(
                        // TargetSubject 沿载 occurrence id 字符串：receipt / attempt
                        // 留痕约定（ExportAttemptEvidence subject）自然工作
                        $"bind-{intent.IntentId}-{uiTarget.OccurrenceId}",
                        intent.IntentId, intent.EffectClass,
                        uiTarget.OccurrenceId, candidate.TargetValue,
                        view.RevisionId, view.RevisionNumber,
                        TargetOccurrenceId: uiTarget.OccurrenceId,
                        // OwningContainerId 尽力而为：BindingView 不携带 container
                        // fact（owner-side 溯源），无据不取 → null
                        OwningContainerId: null,
                        LogicalItemId: uiTarget.LogicalItemId,
                        // DSE-002：executable target anchor（owner fact 从 view
                        // 携带；occurrence 无 locator → null，规则 B 判定归 driver）
                        TargetLocator: view.TargetOccurrenceLocator),
                    RejectionReason: null);
        }
        else if (candidate.SourceRevisionId != view.RevisionId)
            decision = new BindingDecision(null, BindingRejectionReason.StaleRevision);
        else if (candidate.IsAmbiguous)
            decision = new BindingDecision(null, BindingRejectionReason.Ambiguous);
        else if (!view.HasTargetSubjectClaim)
            decision = new BindingDecision(null, BindingRejectionReason.UnknownTarget);
        else
            decision = new BindingDecision(
                new CanonicalBinding(
                    $"bind-{intent.IntentId}-{candidate.TargetSubject}",
                    intent.IntentId, intent.EffectClass,
                    candidate.TargetSubject, candidate.TargetValue,
                    view.RevisionId, view.RevisionNumber),
                RejectionReason: null);

        _bindings.Add(decision);
        return decision;
    }

    /// <summary>
    /// Canonical binding 有效性 = 派生判定（无 event，D8/§17）：revision 仍为
    /// current（EXP-008：经 BindingView.RevisionId correlation anchor），
    /// 且该 binding 未被 dispatch 消费（两者都可从 append-only
    /// ReceiptLog / view revision 推出）。freshness 不参与本判定
    /// （FRS-007 D4 / ADR-0010：freshness 拒绝的是授权，≠ binding
    /// invalidation）。
    /// </summary>
    public bool IsBindingValid(CanonicalBinding binding, BindingView view)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(view);
        return binding.RevisionId == view.RevisionId
            && !_receipts.Any(r => r.BindingId == binding.BindingId);
    }

    /// <summary>
    /// Effect Gate + Dispatch：只执法既有 judgment（authorization +
    /// intent 匹配 + binding 派生有效性），fail-closed；通过才做机械
    /// 投递并产出 Effect Receipt（验收 4）。不重判、不改 target、不扩权。
    /// 同一 binding 只可投递一次（§17 dispatch 失效）。OUT-003：terminal
    /// 后 delivery 关闭，任何 dispatch 请求拒绝（不变量 42）。EXP-008：
    /// WorldBelief 消费面 = BindingView（binding-stale 经 view.RevisionId
    /// correlation anchor 判定）。
    /// </summary>
    public (GateDecision Gate, EffectReceipt? Receipt) Dispatch(
        CanonicalBinding binding,
        AssuranceJudgment judgment,
        BindingView view)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(judgment);
        ArgumentNullException.ThrowIfNull(view);

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
        else if (binding.RevisionId != view.RevisionId)
            gate = new GateDecision(false, "binding-stale");
        else if (_receipts.Any(r => r.BindingId == binding.BindingId))
            gate = new GateDecision(false, "binding-already-dispatched");
        else
            gate = new GateDecision(true, null);

        if (!gate.Allowed)
            return (gate, null);

        var result = _driver.Deliver(ToDispatchRequest(binding));
        var receipt = new EffectReceipt(
            $"receipt-{binding.BindingId}-{_receipts.Count + 1}",
            binding.IntentId, binding.BindingId, binding.TargetSubject,
            binding.RevisionId, binding.RevisionNumber,
            result.Outcome, result.Report, result.CompletedAt,
            Reason: result.Reason);
        _receipts.Add(receipt);
        return (gate, receipt);
    }

    /// <summary>
    /// EB lowering seam（DSE-001 J-b + DSE-002 正式化）：Runtime-authorized
    /// target → driver-executable DeliveryTarget 的唯一转换点（Human 裁决：
    /// WorldModel 认目标，Effect Boundary 出地址，Driver 只送货）。driver
    /// 不重新 grounding、不见 binding identity / authorization correlation
    /// （规则 C 结构保证）；locator 失效 = DeliveryFailed/Unknown → 上游
    /// re-observe → re-ground → 新 binding → 新 DispatchRequest——driver
    /// 永不 fallback 猜测。UI 通道 = occurrence ref + locator；字符串通道
    /// （Deferred ⑮）locatorless 包装（executable 判定归 driver 规则 B）。
    /// </summary>
    private static DispatchRequest ToDispatchRequest(CanonicalBinding binding) => new(
        Target: new DeliveryTarget(
            OccurrenceReference: binding.TargetOccurrenceId ?? binding.TargetSubject,
            Spatial: binding.TargetLocator),
        EffectClass: binding.EffectClass,
        Parameters: binding.TargetValue,
        RevisionId: binding.RevisionId);

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

    /// <summary>
    /// P22 producer 导出缝（PER-003 / ADR-0012 / UWM-009 §12.1）：actual
    /// runtime attempt 已发生的 non-evidentiary 上下文。transitionKind =
    /// effect class（实际发生的 runtime 语义）；correlation = ReceiptId；
    /// epistemic strength = Attempt 腿。不是 EvidenceRecord、不是 World
    /// claim、不 establish Matched/New——零副作用纯派生，不触碰 ledger /
    /// belief；单次 association 消费作用域（ephemeral）。
    /// </summary>
    public TransitionContext ExportTransitionContext(string effectClass, EffectReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(effectClass))
            throw new ArgumentException("effect class 不能为空——transition kind 必须是实际发生的 runtime 语义", nameof(effectClass));
        ArgumentNullException.ThrowIfNull(receipt);
        return new TransitionContext(
            TransitionKind: effectClass,
            AttemptCorrelation: receipt.ReceiptId,
            Strength: TransitionStrength.Attempt);
    }
}
