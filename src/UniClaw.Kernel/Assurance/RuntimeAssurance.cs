using System.Collections.ObjectModel;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

/// <summary>
/// Runtime Assurance — sole Runtime Assurance Judgment Authority（Target
/// §15，不变量 22）。本切片只做 action-local 判定：Action Admissibility /
/// Preconditions-Freshness / Safety-Contract Guard（fail-closed）。
/// 不拥有 Tactical Hypothesis、Run State、WorldBelief、canonical binding
/// 或 mechanical dispatch。blind-retry 记忆是 action-local 状态，只在
/// judgment lifecycle 内有效（P4，不变量 30/31）。
/// </summary>
public sealed class RuntimeAssurance
{
    private readonly Dictionary<string, int> _failedAtRevisionNumber = new();
    private readonly List<AssuranceJudgment> _judgments = new();
    private readonly ReadOnlyCollection<AssuranceJudgment> _judgmentLogView;

    public RuntimeAssurance() => _judgmentLogView = _judgments.AsReadOnly();

    /// <summary>每次 judgment 的留痕（append-only，immutable 产物）。</summary>
    public IReadOnlyList<AssuranceJudgment> JudgmentLog => _judgmentLogView;

    /// <summary>
    /// Action-local judgment（Target §19 输入：intent、binding 候选、
    /// Contract View、accepted Evidence 聚合即 current WorldBelief）。
    /// 任一检查失败 → 拒绝（RejectionReason = 首个失败项），fail-closed。
    /// </summary>
    public AssuranceJudgment Judge(
        ControlIntent intent,
        CandidateBinding? candidate,
        ExecutionContractView view,
        WorldBeliefRevision current)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(current);

        var target = intent.TargetSubject;
        var checks = new List<AssuranceCheck>
        {
            new("intent-is-act", intent.Kind == ControlIntentKind.Act),
            new("effect-class-allowed",
                intent.EffectClass is not null && view.AllowedEffects.Contains(intent.EffectClass)),
            new("effect-class-not-forbidden",
                intent.EffectClass is null || !view.ForbiddenEffects.Contains(intent.EffectClass)),
            new("target-declared", !string.IsNullOrWhiteSpace(target)),
            new("binding-sufficient", candidate is not null),
            new("no-unresolved-conflict",
                target is null || !current.Conflicts.Any(c => c.Subject == target)),
            new("freshness", intent.BasisRevisionId == current.RevisionId),
            new("no-blind-retry",
                target is null
                || !_failedAtRevisionNumber.TryGetValue(target, out var failedAt)
                || current.RevisionNumber > failedAt),
        };

        var judgment = checks.All(c => c.Passed)
            ? new AssuranceJudgment(intent.IntentId, IsAdmissible: true, checks, RejectionReason: null)
            : new AssuranceJudgment(intent.IntentId, IsAdmissible: false, checks,
                checks.First(c => !c.Passed).Name);
        _judgments.Add(judgment);
        return judgment;
    }

    /// <summary>
    /// Dispatch 结果通知：失败时更新 action-local retry 记忆（P4）。
    /// 该状态只在 judgment lifecycle 内有效，不进入 canonical Run State（验收 6）。
    /// </summary>
    public void NoteOutcome(EffectReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Outcome != DispatchOutcome.Failed)
            return;

        _failedAtRevisionNumber.TryGetValue(receipt.TargetSubject, out var existing);
        _failedAtRevisionNumber[receipt.TargetSubject] = Math.Max(existing, receipt.RevisionNumber);
    }
}
