using System.Collections.ObjectModel;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Control;

/// <summary>
/// Control Loop — sole Control Intent Authority（Target §14，不变量 21）。
/// 唯一拥有 Control State、Tactical Hypothesis（内部）与 intent 签发。
/// 不拥有 WorldBelief、Run progress truth、Effect Verification、canonical
/// binding 或 external effect delivery。Recovery 必须重新进入正常闭环
/// （不变量 30/31，D10）。
/// </summary>
public sealed class ControlLoop
{
    private readonly IControlPolicy _policy;
    private readonly List<ControlIntent> _intents = new();
    private readonly List<TacticalHypothesis> _hypotheses = new();
    private readonly ReadOnlyCollection<ControlIntent> _intentLogView;
    private readonly ReadOnlyCollection<TacticalHypothesis> _hypothesisLogView;

    private int _cycleIndex;
    private (string TargetSubject, string FailedAtRevisionId)? _pendingRecovery;

    public ControlLoop(IControlPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _intentLogView = _intents.AsReadOnly();
        _hypothesisLogView = _hypotheses.AsReadOnly();
    }

    /// <summary>已签发 intent 留痕（append-only；sole Control Intent Authority）。</summary>
    public IReadOnlyList<ControlIntent> IntentLog => _intentLogView;

    /// <summary>Tactical Hypothesis 内部留痕（append-only，不改写历史；
    /// disposable：同 target 后来者取代先者；不外溢，验收 7）。</summary>
    public IReadOnlyList<TacticalHypothesis> HypothesisLog => _hypothesisLogView;

    /// <summary>
    /// 签发一个 control intent（EXP-008 / ADR-0011：Run State 不再进入
    /// Control 输入——P5 no-current-buyer / deferred，Control 对 run 侧
    /// 信息零消费）。dispatch 失败后、且尚未出现更新的 WorldBelief
    /// revision 时，强制签发 Recovery intent（re-observe，D10）；否则
    /// 由注入策略决定（D9）。act-intent 同时产生内部 Tactical
    /// Hypothesis（P1 零附着）。
    /// </summary>
    public ControlIntent SelectIntent(ExecutionContractView view, Slice slice)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(slice);

        _cycleIndex++;
        ControlIntent intent;
        if (_pendingRecovery is { } pr && slice.SourceRevisionId == pr.FailedAtRevisionId)
        {
            intent = new ControlIntent(
                $"intent-{_cycleIndex}", ControlIntentKind.Recovery,
                EffectClass: null, TargetSubject: pr.TargetSubject,
                BasisRevisionId: slice.SourceRevisionId);
        }
        else
        {
            var decision = _policy.Decide(new ControlInputs(view, slice));
            intent = new ControlIntent(
                $"intent-{_cycleIndex}", decision.Kind,
                decision.EffectClass, decision.TargetSubject,
                BasisRevisionId: slice.SourceRevisionId);

            if (decision.Kind == ControlIntentKind.Act && decision.TargetSubject is not null)
                RecordHypothesis(decision.TargetSubject, view.Objective, slice.SourceRevisionId);
        }

        _intents.Add(intent);
        return intent;
    }

    /// <summary>dispatch 结果通知（Control State 最小更新；recovery 边输入）。</summary>
    public void NoteDispatchOutcome(EffectReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Outcome == DispatchOutcome.Failed)
            _pendingRecovery = (receipt.TargetSubject, receipt.RevisionId);
    }

    /// <summary>intent 是否由本 Control Loop 签发（引用同一性；挡 forged intent）。</summary>
    public bool IsIssued(ControlIntent intent) =>
        intent is not null && _intents.Any(i => ReferenceEquals(i, intent));

    private void RecordHypothesis(string targetSubject, string objective, string basisRevisionId) =>
        _hypotheses.Add(new TacticalHypothesis(
            $"hyp-{_hypotheses.Count + 1}", targetSubject,
            $"acting on {targetSubject} advances {objective}",
            basisRevisionId));
}
