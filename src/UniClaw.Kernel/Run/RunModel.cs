using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Kernel.Run;

/// <summary>
/// Run Model — sole Canonical Run State Recording Authority（Target §13）。
/// 唯一拥有 Execution Contract View、Objective State、Proof Obligation
/// State、Progress State、Outcome State。不产生 Control Intent、不判断
/// Effect / Outcome Proof（只记录 Assurance 的 proof 快照）、不保存
/// action-local assurance state。
/// </summary>
public sealed class RunModel
{
    private readonly List<RunState> _history = new();
    private readonly ReadOnlyCollection<RunState> _historyView;

    public RunModel() => _historyView = _history.AsReadOnly();

    /// <summary>
    /// Primary Run identity（本片单 Run cardinality）。RUN-001：由
    /// first-accepted Contract View 内容确定性派生（"run-" + SHA-256 hex
    /// 前 12 位，同构 EvidenceId 内容寻址先例），首次 admission 铸造后
    /// immutable；同 version 幂等 re-admit 不重铸。
    /// </summary>
    public string RunId { get; private set; } = string.Empty;

    /// <summary>当前 Run 的 immutable Contract View（contract 被接受前为 null）。</summary>
    public ExecutionContractView? View { get; private set; }

    /// <summary>Current canonical Run State（typed transitions 的最新结果）。</summary>
    public RunState? State => _history.Count == 0 ? null : _history[^1];

    /// <summary>Run State typed transition 历史（append-only）。</summary>
    public IReadOnlyList<RunState> History => _historyView;

    /// <summary>是否已 terminal（Terminal Outcome State 已记录；§17 不可恢复 active）。</summary>
    public bool IsTerminal => State?.Outcome is not null;

    /// <summary>
    /// Contract admission path：非法/不完整 contract → 拒绝 + 零 Run State
    /// 副作用（验收 1）；同 version 重复 admit → 幂等复用同一 View（验收 9）；
    /// 不同 version → fail-closed 拒绝（显式取代语义不在本片）。outcome 语义
    /// 不变；Obligations 可选中携带 run-level 证明义务（D2）。
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
            new("obligations-wellformed", contract.Obligations is null
                || (contract.Obligations.Count > 0
                    && contract.Obligations.All(o => !string.IsNullOrWhiteSpace(o.ObligationId)
                        && !string.IsNullOrWhiteSpace(o.Subject)
                        && !string.IsNullOrWhiteSpace(o.RequiredValue)))),
        };

        if (!checks.All(c => c.Passed))
            return new ContractAdmission(false, checks, checks.First(c => !c.Passed).Name);

        if (View is null)
        {
            View = new ExecutionContractView(
                contract.Version, contract.Objective,
                contract.Scope!, contract.AllowedEffects!, contract.ForbiddenEffects!,
                contract.ProofCriteria!);
            RunId = MintRunId(contract);
            _history.Add(new RunState(
                View,
                new ObjectiveState(contract.Objective, "pursuing"),
                new ProofObligationState(ResolveObligations(contract)),
                new ProgressState(0, 0)));
            return new ContractAdmission(true, checks, RejectionReason: null);
        }

        if (View.Version == contract.Version)
            return new ContractAdmission(true, checks, RejectionReason: null);

        return new ContractAdmission(false, checks, "version-conflict");
    }

    /// <summary>
    /// RunId 内容派生（RUN-001）：canonical = Contract View 六字段；set
    /// 字段按 ordinal 排序后参与（消除插入序影响）；Obligations 不参与
    /// （admission 等价以 View 为准，View 不含 Obligations）。
    /// "run-" + SHA-256 hex 前 12 位；拼法与前缀长度 = realization。
    /// </summary>
    private static string MintRunId(ExecutionContract contract)
    {
        var canonical = string.Join('\x1F',
            contract.Version,
            contract.Objective,
            string.Join('\x1E', contract.Scope!.OrderBy(s => s, StringComparer.Ordinal)),
            string.Join('\x1E', contract.AllowedEffects!.OrderBy(s => s, StringComparer.Ordinal)),
            string.Join('\x1E', contract.ForbiddenEffects!.OrderBy(s => s, StringComparer.Ordinal)),
            string.Join('\x1E', contract.ProofCriteria!));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "run-" + Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }

    /// <summary>
    /// Run-level obligations 解析（D2）：contract 显式提供 → 原样；否则从
    /// ProofCriteria 派生不可判定的占位 obligation（无 subject/value 的
    /// 结构化规格，Assurance 永不判其满足——C2E 既有 contract 行为诚实）。
    /// </summary>
    private static IReadOnlyList<RunObligation> ResolveObligations(ExecutionContract contract) =>
        contract.Obligations is { Count: > 0 }
            ? contract.Obligations
            : contract.ProofCriteria!
                .Select(c => new RunObligation(
                    c, RunObligationKind.Objective, Subject: "", RequiredValue: "", Mandatory: true))
                .ToList();

    /// <summary>Typed transition：一个 control cycle 完成（验收 6）。terminal 后禁止（§17）。</summary>
    public RunState RecordCycle()
    {
        EnsureActive();
        return Transition(p => p with { Cycles = p.Cycles + 1 });
    }

    /// <summary>Typed transition：一次 dispatched act 完成（成功或失败）。terminal 后禁止。</summary>
    public RunState RecordAction()
    {
        EnsureActive();
        return Transition(p => p with { Acts = p.Acts + 1 });
    }

    /// <summary>
    /// Typed terminal transition：基于 Outcome Proof result 进入 Terminal
    /// Outcome State（任务 六）。exact-prior / 引用等同并发守卫：只有
    /// expectedPrior 仍是 current 且尚未 terminal 时成功——一个 Primary Run
    /// 至多成功进入一次 terminal（任务 六.4）。记录 judgment 快照，不重判。
    /// </summary>
    public OutcomeTransition TransitionToTerminal(Assurance.OutcomeProof proof, RunState expectedPrior)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(expectedPrior);

        var current = State ?? throw new InvalidOperationException("Run State 尚未建立（无已接受 contract）");
        // exact-prior guard 优先：任一 proposal 必须基于当前 canonical state
        // （引用等同 CAS；single-winner，任务 六.4）
        if (!ReferenceEquals(current, expectedPrior))
            return new OutcomeTransition(false, "concurrent-terminal-proposal", null);
        if (current.Outcome is not null)
            return new OutcomeTransition(false, "already-terminal", null);

        var terminal = current with
        {
            Outcome = new OutcomeState(
                proof.ProofId,
                proof.Classification,
                proof.Obligations,
                proof.BasisEvidenceIds,
                proof.EffectEvidenceIds,
                proof.SituationEvidenceIds,
                proof.UnresolvedUncertainty,
                proof.Reason),
        };
        _history.Add(terminal);
        return new OutcomeTransition(true, null, terminal);
    }

    private void EnsureActive()
    {
        if (IsTerminal)
            throw new InvalidOperationException("Run 已 terminal，不得恢复 active（§17）");
    }

    private RunState Transition(Func<ProgressState, ProgressState> progress)
    {
        var current = State ?? throw new InvalidOperationException("Run State 尚未建立（无已接受 contract）");
        var next = current with { Progress = progress(current.Progress) };
        _history.Add(next);
        return next;
    }
}
