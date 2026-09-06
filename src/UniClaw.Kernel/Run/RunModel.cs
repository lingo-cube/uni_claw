using System.Collections.ObjectModel;

namespace UniClaw.Kernel.Run;

/// <summary>
/// Run Model — sole Canonical Run State Recording Authority（Target §13）。
/// 唯一拥有 Execution Contract View、Objective State、Proof Obligation
/// State、Progress State。不产生 Control Intent、不判断 Effect / Outcome
/// Proof、不保存 action-local assurance state。
/// </summary>
public sealed class RunModel
{
    private readonly List<RunState> _history = new();
    private readonly ReadOnlyCollection<RunState> _historyView;

    public RunModel() => _historyView = _history.AsReadOnly();

    /// <summary>当前 Run 的 immutable Contract View（contract 被接受前为 null）。</summary>
    public ExecutionContractView? View { get; private set; }

    /// <summary>Current canonical Run State（typed transitions 的最新结果）。</summary>
    public RunState? State => _history.Count == 0 ? null : _history[^1];

    /// <summary>Run State typed transition 历史（append-only）。</summary>
    public IReadOnlyList<RunState> History => _historyView;

    /// <summary>
    /// Contract admission path：非法/不完整 contract → 拒绝 + 零 Run State
    /// 副作用（验收 1）；同 version 重复 admit → 幂等复用同一 View（验收 9）；
    /// 不同 version → fail-closed 拒绝（显式取代语义不在本片）。
    /// </summary>
    public ContractAdmission AdmitContract(ExecutionContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var checks = new List<ContractCheck>
        {
            new("version-present", !string.IsNullOrWhiteSpace(contract.Version)),
            new("objective-present", !string.IsNullOrWhiteSpace(contract.Objective)),
            new("scope-nonempty", contract.Scope is { Count: > 0 }),
            new("allowed-effects-nonempty", contract.AllowedEffects is { Count: > 0 }),
            new("forbidden-effects-declared", contract.ForbiddenEffects is not null),
            new("proof-criteria-declared", contract.ProofCriteria is { Count: > 0 }),
        };

        if (!checks.All(c => c.Passed))
            return new ContractAdmission(false, checks, checks.First(c => !c.Passed).Name);

        if (View is null)
        {
            View = new ExecutionContractView(
                contract.Version, contract.Objective,
                contract.Scope!, contract.AllowedEffects!, contract.ForbiddenEffects!,
                contract.ProofCriteria!);
            _history.Add(new RunState(
                View,
                new ObjectiveState(contract.Objective, "pursuing"),
                new ProofObligationState(contract.ProofCriteria!),
                new ProgressState(0, 0)));
            return new ContractAdmission(true, checks, RejectionReason: null);
        }

        if (View.Version == contract.Version)
            return new ContractAdmission(true, checks, RejectionReason: null);

        return new ContractAdmission(false, checks, "version-conflict");
    }

    /// <summary>Typed transition：一个 control cycle 完成（验收 6）。</summary>
    public RunState RecordCycle() => Transition(p => p with { Cycles = p.Cycles + 1 });

    /// <summary>Typed transition：一次 dispatched act 完成（成功或失败）。</summary>
    public RunState RecordAction() => Transition(p => p with { Acts = p.Acts + 1 });

    private RunState Transition(Func<ProgressState, ProgressState> progress)
    {
        var current = State ?? throw new InvalidOperationException("Run State 尚未建立（无已接受 contract）");
        var next = current with { Progress = progress(current.Progress) };
        _history.Add(next);
        return next;
    }
}
