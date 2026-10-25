using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.Collections.Immutable;
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
public sealed partial class RunModel
{
    private readonly List<RunState> _history = new();
    private readonly ReadOnlyCollection<RunState> _historyView;

    public RunModel() : this(null) { }

    public RunModel(IRunStateJournal? journal)
    {
        if (journal is not null && (!journal.CanInitialize || journal.ReadRecords().Count != 0))
            throw new InvalidOperationException("existing-run-history-requires-read-only-restore");
        _stateJournal = journal;
        _historyView = _history.AsReadOnly();
    }

    /// <summary>
    /// Primary Run identity（本片单 Run cardinality）。RUN-001：由
    /// first-accepted Contract View 内容确定性派生（"run-" + SHA-256 完整
    /// 64 位 hex，长度前缀帧式 canonical——同构 EvidenceId 内容寻址
    /// 先例），首次 admission 铸造后 immutable；同 version 幂等
    /// re-admit 不重铸。
    /// </summary>
    public string RunId { get; private set; } = string.Empty;

    /// <summary>当前 Run 的 immutable Contract View（contract 被接受前为 null）。</summary>
    public ExecutionContractView? View { get; private set; }

    /// <summary>Canonical latest traversal progress, recorded only by this Owner.</summary>
    public UniClaw.Kernel.Runtime.TraversalExecutionView? TraversalExecution { get; private set; }

    private readonly List<UniClaw.Kernel.Runtime.TraversalExecutionView> _traversalHistory = new();
    private readonly List<UniClaw.Kernel.Runtime.PlanHandlerRecord> _traversalHandlers = new();
    private readonly List<UniClaw.Kernel.Runtime.TraversalGap> _traversalGaps = new();
    private readonly List<UniClaw.Kernel.Runtime.TraversalOperationRecord> _traversalOperations = new();
    private readonly List<string> _traversalCompletedWork = new();
    public IReadOnlyList<UniClaw.Kernel.Runtime.TraversalExecutionView> TraversalHistory => _traversalHistory.AsReadOnly();

    internal void BeginTraversalRecords() => BeginTraversalRecordsAsync().AsTask().GetAwaiter().GetResult();

    internal void RecordTraversalHandler(UniClaw.Kernel.Runtime.PlanHandlerRecord record) =>
        RecordTraversalHandlerAsync(record).AsTask().GetAwaiter().GetResult();

    internal void RecordTraversalOperation(UniClaw.Kernel.Runtime.TraversalOperationRecord record) =>
        RecordTraversalOperationAsync(record).AsTask().GetAwaiter().GetResult();

    internal void RecordTraversalProgress(UniClaw.Kernel.Runtime.TraversalProgressEvent progress) =>
        RecordTraversalProgressAsync(progress).AsTask().GetAwaiter().GetResult();

    internal void RecordTraversal(UniClaw.Kernel.Runtime.TraversalExecutionView controlState) =>
        RecordTraversalAsync(controlState).AsTask().GetAwaiter().GetResult();

    internal ValueTask BeginTraversalRecordsAsync() => RecordTraversalEventAsync(new(TraversalSchema, RunTraversalTransition.Reset));
    internal ValueTask RecordTraversalHandlerAsync(UniClaw.Kernel.Runtime.PlanHandlerRecord record) =>
        RecordTraversalEventAsync(new(TraversalSchema, RunTraversalTransition.Handler, Handler: record));
    internal ValueTask RecordTraversalOperationAsync(UniClaw.Kernel.Runtime.TraversalOperationRecord record) =>
        RecordTraversalEventAsync(new(TraversalSchema, RunTraversalTransition.Operation, Operation: record));
    internal ValueTask RecordTraversalProgressAsync(UniClaw.Kernel.Runtime.TraversalProgressEvent progress) => progress switch
    {
        UniClaw.Kernel.Runtime.TraversalProgressEvent.Gap gap => RecordTraversalEventAsync(new(TraversalSchema, RunTraversalTransition.Gap, Gap: gap.Value)),
        UniClaw.Kernel.Runtime.TraversalProgressEvent.Completed completed => RecordTraversalEventAsync(new(TraversalSchema, RunTraversalTransition.Completed,
            OperationId: completed.OperationId, Work: completed.Work)),
        _ => ValueTask.CompletedTask,
    };
    internal ValueTask RecordTraversalAsync(UniClaw.Kernel.Runtime.TraversalExecutionView controlState) =>
        RecordTraversalEventAsync(new(TraversalSchema, RunTraversalTransition.View, Control: controlState));

    /// <summary>Current canonical Run State（typed transitions 的最新结果）。</summary>
    public RunState? State => _history.Count == 0 ? null : _history[^1];

    /// <summary>Run State typed transition 历史（append-only）。</summary>
    public IReadOnlyList<RunState> History => _historyView;

    /// <summary>是否已 terminal（Terminal Outcome State 已记录；§17 不可恢复 active）。</summary>
    public bool IsTerminal => State?.Outcome is not null;

    /// <summary>RUN-004 D8（F5）：预算合同默认值（null→默认；须与
    /// ExecutionContractView 尾部默认一致——同价 null≡默认 的幂等语义）。</summary>
    private const int DefaultMaxConsultations = 16;
    private const int DefaultMaxTotalSteps = 256;

    /// <summary>
    /// Contract admission path：非法/不完整 contract → 拒绝 + 零 Run State
    /// 副作用（验收 1）；同 version 且合同签名（Version + 两预算解析值）相等
    /// 重复 admit → 幂等复用同一 View（验收 9）；同 version 异预算 → 拒绝
    /// "contract-signature-conflict"（RUN-004 D8 fail-closed，评审 #3 F5）；
    /// 不同 version → fail-closed 拒绝（显式取代语义不在本片）。outcome 语义
    /// 不变；Obligations 可选中携带 run-level 证明义务（D2）。
    /// </summary>
    /// <param name="runIdOverride">Optional identity supplied by the Runtime
    /// authority at a Host launch boundary. When absent, the Kernel retains its
    /// content-derived RUN-001 identity.</param>
    public ContractAdmission AdmitContract(ExecutionContract contract, string? runIdOverride = null) =>
        AdmitContractAsync(contract, runIdOverride).AsTask().GetAwaiter().GetResult();

    public ValueTask<ContractAdmission> AdmitContractAsync(ExecutionContract contract, string? runIdOverride = null,
        CancellationToken cancellationToken = default) =>
        RecordOperationAsync(() => AdmitContractCoreAsync(contract, runIdOverride), cancellationToken);

    private async ValueTask<ContractAdmission> AdmitContractCoreAsync(ExecutionContract contract, string? runIdOverride)
    {
        ArgumentNullException.ThrowIfNull(contract);
        EnsureStateAvailable();

        var checks = CheckContract(contract);

        if (!checks.All(c => c.Passed))
            return new ContractAdmission(false, checks, checks.First(c => !c.Passed).Name);

        if (View is null)
        {
            // RUN-004 D8（F5）：null→默认解析后值进 View（合同预算声明通道）。
            // PROTOCOL-FIX-003（ADR-0050 Q1:B）：预算来源留痕——显式声明 =
            // configured（初始额度承诺），缺省 = default。契约预算只是初始
            // 额度，终止测度由 policy HardCap 结构保证（续授见 P25 通道）。
            var consultations = contract.MaxConsultations ?? DefaultMaxConsultations;
            var totalSteps = contract.MaxTotalSteps ?? DefaultMaxTotalSteps;
            var consultationsOrigin = contract.MaxConsultations.HasValue
                ? BudgetValueOrigin.Configured
                : BudgetValueOrigin.Default;
            var totalStepsOrigin = contract.MaxTotalSteps.HasValue
                ? BudgetValueOrigin.Configured
                : BudgetValueOrigin.Default;
            // 保留旧聚合读取面，但在部分缺省时明确标记 Mixed，避免把
            // 一个显式维度误报成两个维度都 configured。
            var valueOrigin = consultationsOrigin == totalStepsOrigin
                ? consultationsOrigin
                : BudgetValueOrigin.Mixed;
            var view = new ExecutionContractView(
                contract.Version, contract.Objective,
                SnapshotSet(contract.Scope!), SnapshotSet(contract.AllowedEffects!), SnapshotSet(contract.ForbiddenEffects!),
                Array.AsReadOnly(contract.ProofCriteria!.ToArray()), consultations, totalSteps, valueOrigin,
                contract.TaskInstanceId, contract.ProductSessionId, contract.ContractRef,
                contract.AcceptedContractViewRef, contract.ContractGeneration)
            {
                MaxConsultationsOrigin = consultationsOrigin,
                MaxTotalStepsOrigin = totalStepsOrigin,
            };
            var runtimeAssigned = !string.IsNullOrWhiteSpace(runIdOverride);
            var runId = string.IsNullOrWhiteSpace(runIdOverride)
                ? MintRunId(contract, consultations, totalSteps)
                : runIdOverride.Trim();
            await CommitStateAsync(new RunState(
                view,
                new ObjectiveState(contract.Objective, "pursuing"),
                new ProofObligationState(Array.AsReadOnly(ResolveObligations(contract).ToArray())),
                new ProgressState(0, 0)), RunHistoryTransition.Admitted, CaptureIdentity(view, runId, runtimeAssigned)).ConfigureAwait(false);
            View = view;
            RunId = runId;
            _identityRuntimeAssigned = runtimeAssigned;
            return new ContractAdmission(true, checks, RejectionReason: null);
        }

        // RUN-004 D8（F5 评审 #3 修正）：幂等比较升级为合同签名比较——
        // Version + 两预算解析值全等才算同一合同；同 version 异预算
        // = 不同合同 → fail-closed（原实现按 Version 相等即幂等复用，
        // 预算变更被静默丢弃）。
        var sameSignature = View.Version == contract.Version
            && View.MaxConsultations == (contract.MaxConsultations ?? DefaultMaxConsultations)
            && View.MaxTotalSteps == (contract.MaxTotalSteps ?? DefaultMaxTotalSteps);
        if (sameSignature)
            return new ContractAdmission(true, checks, RejectionReason: null);
        if (View.Version == contract.Version)
            return new ContractAdmission(false, checks, "contract-signature-conflict");

        return new ContractAdmission(false, checks, "version-conflict");
    }

    private static IReadOnlySet<string> SnapshotSet(IReadOnlySet<string> values)
    {
        if (values is FrozenSet<string> or IImmutableSet<string>) return values;
        if (values is SortedSet<string> sorted)
            return sorted.ToImmutableSortedSet(sorted.Comparer);

        var comparer = values is HashSet<string> hashSet
            ? hashSet.Comparer
            : StringComparer.Ordinal;
        return values.ToFrozenSet(comparer);
    }

    /// <summary>
    /// RunId 内容派生（RUN-001；评审二轮 Standards 1 / Spec P1 硬化）：
    /// canonical 为自描述结构——每字段独立编码 field-tag + 长度帧内容，
    /// 集合字段加长度帧成员数（"tag + count-frame + item-frames*"）。
    /// 字段边界、集合边界、成员数全部显式：跨字段 / 跨集合重分组均不可
    /// 构造相同 canonical。set 字段按 ordinal 排序参与（消除插入序影响）；
    /// Obligations 不参与（admission 等价以 View 为准）。
    /// "run-" + SHA-256 完整 hex（64 位；人工裁决 2026-09-09 二轮）。
    /// 编码拼法 = realization。
    /// </summary>
    private static string MintRunId(ExecutionContract contract, int consultations, int totalSteps)
    {
        var canonical = string.Concat(
            Scalar("V", contract.Version),
            Scalar("O", contract.Objective),
            Collection("S", contract.Scope!.OrderBy(s => s, StringComparer.Ordinal)),
            Collection("E", contract.AllowedEffects!.OrderBy(s => s, StringComparer.Ordinal)),
            Collection("F", contract.ForbiddenEffects!.OrderBy(s => s, StringComparer.Ordinal)),
            Collection("C", contract.ProofCriteria!),
            // RUN-004 D8（F5）：两预算解析后值参与 canonical（null≡默认，幂等语义成立）
            Scalar("B1", consultations.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Scalar("B2", totalSteps.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "run-" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>自描述标量字段：tag + 长度帧值。</summary>
    private static string Scalar(string tag, string value) => tag + Frame(value);

    /// <summary>自描述集合字段：tag + 长度帧成员数 + 逐成员长度帧（不得
    /// 先展开成统一序列——会丢失字段/集合边界，评审二轮碰撞根源）。</summary>
    private static string Collection(string tag, IEnumerable<string> values)
    {
        var items = values as IReadOnlyList<string> ?? values.ToList();
        return tag + Frame(items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))
             + string.Concat(items.Select(Frame));
    }

    /// <summary>长度前缀帧：解析器由十进制长度确定字段边界，字段内容可为
    /// 任意字符（含分隔字符），帧串接无歧义。</summary>
    private static string Frame(string value) =>
        value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;

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
    public RunState RecordCycle() => RecordCycleAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask<RunState> RecordCycleAsync(CancellationToken cancellationToken = default) =>
        RecordOperationAsync(() => TransitionAsync(p => p with { Cycles = p.Cycles + 1 }), cancellationToken);

    /// <summary>Typed transition：一次 dispatched act 完成（成功或失败）。terminal 后禁止。</summary>
    public RunState RecordAction() => RecordActionAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask<RunState> RecordActionAsync(CancellationToken cancellationToken = default) =>
        RecordOperationAsync(() => TransitionAsync(p => p with { Acts = p.Acts + 1 }), cancellationToken);

    /// <summary>
    /// Typed terminal transition：基于 Outcome Proof result 进入 Terminal
    /// Outcome State（任务 六）。exact-prior / 引用等同并发守卫：只有
    /// expectedPrior 仍是 current 且尚未 terminal 时成功——一个 Primary Run
    /// 至多成功进入一次 terminal（任务 六.4）。记录 judgment 快照，不重判。
    /// </summary>
    public OutcomeTransition TransitionToTerminal(Assurance.OutcomeProof proof, RunState expectedPrior) =>
        TransitionToTerminalAsync(proof, expectedPrior).AsTask().GetAwaiter().GetResult();

    public ValueTask<OutcomeTransition> TransitionToTerminalAsync(Assurance.OutcomeProof proof, RunState expectedPrior,
        CancellationToken cancellationToken = default) =>
        RecordOperationAsync(() => TransitionToTerminalCoreAsync(proof, expectedPrior), cancellationToken);

    private async ValueTask<OutcomeTransition> TransitionToTerminalCoreAsync(Assurance.OutcomeProof proof, RunState expectedPrior)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(expectedPrior);

        EnsureStateAvailable();
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
                Array.AsReadOnly(proof.Obligations.ToArray()),
                SnapshotSet(proof.BasisEvidenceIds),
                SnapshotSet(proof.EffectEvidenceIds),
                SnapshotSet(proof.SituationEvidenceIds),
                proof.UnresolvedUncertainty,
                proof.Reason),
        };
        await CommitStateAsync(terminal, RunHistoryTransition.Terminal).ConfigureAwait(false);
        return new OutcomeTransition(true, null, terminal);
    }

    private void EnsureActive()
    {
        EnsureStateAvailable();
        if (IsTerminal)
            throw new InvalidOperationException("Run 已 terminal，不得恢复 active（§17）");
    }

    private async ValueTask<RunState> TransitionAsync(Func<ProgressState, ProgressState> progress)
    {
        EnsureActive();
        var current = State ?? throw new InvalidOperationException("Run State 尚未建立（无已接受 contract）");
        var next = current with { Progress = progress(current.Progress) };
        await CommitStateAsync(next, next.Progress.Cycles != current.Progress.Cycles ? RunHistoryTransition.Cycle : RunHistoryTransition.Action).ConfigureAwait(false);
        return next;
    }
}
