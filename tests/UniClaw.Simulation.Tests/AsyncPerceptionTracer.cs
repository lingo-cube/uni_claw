using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;

namespace UniClaw.Simulation.Tests;

// ============================================================================
// UAP-001 — Phase 4 统一异步 Perception 最小垂直 tracer。
//
// 证明目标（roadmap §10 Phase 4）：Fast-only / Fast→Slow / Slow-only /
// one-shot 四种 realization 对 Runtime 顶层同构——一切观察以
// ObservationProposal 经 P2 admission → P3 accepted Evidence → World Model
// relevance/reconciliation 进入 belief；迟到/重复/乱序/partial/failure/
// timeout/cancel 受控且 fail closed；Perception 不直接改 WorldBelief。
//
// H9（OPEN_GATE）：capture/operation correlation 只在本 tracer 内实现
// （test assembly），不升格产品 Interface/模型——本文件全部类型 internal
// 且位于测试程序集即其结构性执行。
//
// 真实 buyer：Kernel internal driver 的观察控制（RunDriverInputs.NextInput
// 既有 seam，RFS-001）。Perception realization 选择（Fast→Slow 升级等）
// 是 feed 内部决策；Runtime 只消费完成后的合并 ObservationProposal 批次。
// ============================================================================

/// <summary>可控虚拟时间（无 canonical clock；录制输入的确定性时间轴）。</summary>
internal sealed class VirtualClock
{
    internal DateTimeOffset Now { get; private set; }

    internal VirtualClock(DateTimeOffset start) => Now = start;

    /// <summary>前进到 t（回退 fail closed）。</summary>
    internal void AdvanceTo(DateTimeOffset t)
    {
        if (t < Now)
            throw new InvalidOperationException($"虚拟时间不可回退: {Now:O} → {t:O}");
        Now = t;
    }

    internal void Advance(TimeSpan duration) => AdvanceTo(Now + duration);
}

/// <summary>
/// 观察操作的 typed completion（场景 ⑥ 四态严格区分的执行面）。
/// Pending = 进行中；Complete = 声明覆盖完成且有结果（结果可为显式空帧
/// ——对已覆盖 scope 的合法负观察，≠ failure）；PartialAtDeadline = 预算
/// 到限时部分覆盖（未覆盖 region 零声明）；Failed = producer 失败记录
/// （零投递，绝不产生空帧/absence 声明）；TimedOut = 预算内无可用结果；
/// Superseded = 页面/采集推进后被隔离；Cancelled = 等待期取消后隔离。
/// </summary>
internal enum ObservationOperationStatus
{
    Pending,
    Complete,
    PartialAtDeadline,
    Failed,
    TimedOut,
    Superseded,
    Cancelled,
}

/// <summary>
/// 一条录制观察结果（确定性输入）。ResultId = 去重身份（重复投递 = 同 id
/// 再次入列）；Stage ∈ fast/slow/oneshot/targeted-slow/review 是
/// Perception 内部 realization 细节——只进 provenance，不进 Runtime 面
/// producer。FailureReason 非 null = 失败记录（零 proposal，≠ OK_EMPTY）。
/// </summary>
internal sealed record ScheduledResult(
    string ResultId,
    DateTimeOffset ArrivalTime,
    string Stage,
    bool IsFinal,
    IReadOnlyList<string> CoveredRegions,
    string? FailureReason,
    IReadOnlyList<ObservationProposal> Proposals);

/// <summary>
/// 一次观察操作脚本（外部输入的时间轴声明）。OperationKey 在场景内唯一，
/// 参与 operation id 派生；OpenedAt = 虚拟打开时刻（预算/deadline 与
/// provenance CaptureTime 的确定性基准）。
/// </summary>
internal sealed record ScriptedOperation(
    ObservationContext Context,
    string OperationKey,
    string Purpose,
    IReadOnlyList<string> ScopeRegions,
    TimeSpan Budget,
    DateTimeOffset OpenedAt,
    string CaptureArtifactId,
    IReadOnlyList<ScheduledResult> Schedule);

/// <summary>operation 确定性 id（H9：tracer 内 correlation 身份）。</summary>
internal static class ObservationOperationId
{
    internal static string For(ScriptedOperation operation)
    {
        var canonical = string.Join('\x1F',
            operation.Purpose, operation.CaptureArtifactId,
            string.Join("\x1E", operation.ScopeRegions.OrderBy(r => r, StringComparer.Ordinal)),
            operation.OperationKey);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "op-" + Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }
}

/// <summary>operation 诊断快照（只读观察面；测试断言 target）。</summary>
internal sealed record OperationSnapshot(
    string OperationId,
    string Purpose,
    string CaptureArtifactId,
    ObservationContext Context,
    ObservationOperationStatus Status,
    IReadOnlyList<string> RequestedRegions,
    IReadOnlyList<string> CoveredRegions,
    IReadOnlyList<string> ArrivedResultIds,
    IReadOnlyList<string> DeliveredResultIds,
    IReadOnlyList<string> DuplicateResultIds,
    IReadOnlyList<string> StaleQuarantinedResultIds,
    IReadOnlyList<string> CancelledQuarantinedResultIds,
    DateTimeOffset OpenedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? DeliveredAt,
    TimeSpan? VirtualDeliveryLatency,
    string? FailureReason,
    int DeliveredProposalCount);

/// <summary>
/// 受控异步投递 feed：driver NextInput seam 的 Perception 侧 realization。
/// hold-until-complete（UAP-001 D3）：结果按录制虚拟时间到达、按 operation
/// correlation 去重合并；operation 完成（Complete/PartialAtDeadline）才投递
/// 一个 canonical-ordered 批次；Pending 期间 driver 合法等待。Failed/
/// TimedOut/Superseded/Cancelled 零投递（D6/D7 fail closed）。
/// </summary>
internal sealed class AsyncObservationFeed
{
    private readonly VirtualClock _clock;
    private readonly Queue<ScriptedOperation> _script;
    private readonly Dictionary<ObservationContext, Operation?> _current = new();
    private readonly List<Operation> _operations = new();
    private readonly Queue<(string Reason, DateTimeOffset VirtualTime)> _cancels = new();

    internal AsyncObservationFeed(VirtualClock clock, IReadOnlyList<ScriptedOperation> operations)
    {
        _clock = clock;
        _script = new Queue<ScriptedOperation>(operations);
    }

    /// <summary>全部 operation 诊断（append-only 顺序）。</summary>
    internal IReadOnlyList<OperationSnapshot> Operations =>
        _operations.Select(o => o.Snapshot()).ToList();

    /// <summary>宿主侧页面/采集推进：pending operation 全部隔离为 Superseded。</summary>
    internal void SupersedePending()
    {
        foreach (var operation in _operations)
            if (operation.Status == ObservationOperationStatus.Pending)
                operation.Status = ObservationOperationStatus.Superseded;
    }

    /// <summary>排队宿主/用户 cancel（下一次 Next 优先返回；pending ops → Cancelled）。</summary>
    internal void QueueCancel(string reason, DateTimeOffset virtualTime) =>
        _cancels.Enqueue((reason, virtualTime));

    /// <summary>
    /// driver NextInput seam 实现（UAP-001 D3/D6/D7）。
    ///
    /// 每次调用：① 泵送全部 operation 的到期结果（到达/去重/隔离/完成转
    /// 移）；② cancel 优先（pending → Cancelled，返回 Cancel 输入）；③ 对
    /// 期望 context 的当前 operation：Complete/PartialAtDeadline 且未投递
    /// → 投递 canonical-ordered 合并批次（恰一次）；Pending → null（合法
    /// 等待，hold-until-complete）；终态非投递（Failed/TimedOut/Empty 语义
    /// 见 D7/Superseded/Cancelled）或已投递 → 前进到下一脚本 operation。
    /// </summary>
    internal RunDriverInput? Next(ObservationContext expected)
    {
        PumpAll();
        if (_cancels.Count > 0)
        {
            var (reason, virtualTime) = _cancels.Dequeue();
            foreach (var operation in _operations)
                if (operation.Status == ObservationOperationStatus.Pending)
                    operation.Status = ObservationOperationStatus.Cancelled;
            return new RunDriverInput.Cancel(reason, virtualTime);
        }

        while (true)
        {
            if (!_current.TryGetValue(expected, out var operation) || operation is null)
            {
                operation = OpenNext(expected);
                if (operation is null)
                    return null; // 无脚本输入 → 合法等待
                _current[expected] = operation;
                PumpAll(); // 开启即可能有已到期结果
            }

            switch (operation.Status)
            {
                case ObservationOperationStatus.Complete:
                case ObservationOperationStatus.PartialAtDeadline:
                    if (!operation.Delivered)
                    {
                        var batch = operation.MergedBatch();
                        operation.Delivered = true;
                        operation.DeliveredAt = _clock.Now;
                        _current[expected] = null; // 本 operation 已消费
                        if (batch.Count == 0)
                            break; // 零可用 proposal 的完成：不投递空批（driver 拒绝空批），继续等待
                        return new RunDriverInput.Observation(batch);
                    }
                    break;

                case ObservationOperationStatus.Pending:
                    return null; // hold-until-complete：driver 合法等待
            }

            // 终态非投递（Failed/TimedOut/Superseded/Cancelled）或已投递 →
            // 前进到下一脚本 operation
            _current[expected] = null;
        }
    }

    /// <summary>
    /// 诊断面泵送（terminal 后 driver 不再 pull；宿主侧推进到达/隔离记录，
    /// 不产生任何 admission——隔离结果零投递）。
    /// </summary>
    internal void PumpNow() => PumpAll();

    /// <summary>打开下一匹配 context 的脚本 operation（保持脚本顺序；无匹配 → null）。</summary>
    private Operation? OpenNext(ObservationContext context)
    {
        if (_script.Count == 0 || _script.Peek().Context != context)
            return null;
        var script = _script.Dequeue();
        var operation = new Operation
        {
            Script = script,
            OperationId = ObservationOperationId.For(script),
        };
        _operations.Add(operation);
        return operation;
    }

    /// <summary>
    /// 泵送（UAP-001 D13 事件时间语义，Human 复审要求 2026-09-14）：
    /// 到期事件（结果到达 + deadline）按**事件时间**排序逐项处理，状态
    /// 转移在每个事件点及时发生——完成判定不得滞后到拉取时刻，否则拉取
    /// 节奏会改变交付批次（S5b RED 证伪）。推论：完成后才到的结果一律
    /// 隔离（新 ResultId → stale；已交付 ResultId 的重投 → Duplicates），
    /// 不得翻转已完成 op（「完成后才到的 failure」反例）。deadline 同为
    /// 事件：预算耗尽在其事件时间生效，晚于它的到达即迟到。同刻事件按
    /// （时间, 到达先于 deadline, 脚本序）稳定排序。
    /// </summary>
    private void PumpAll()
    {
        var now = _clock.Now;
        foreach (var operation in _operations)
        {
            const int arrivalKind = 0;
            const int deadlineKind = 1;
            var events = new List<(DateTimeOffset Time, int Kind, int Index)>();
            foreach (var (index, result) in operation.Script.Schedule.Select((r, i) => (i, r)))
                if (result.ArrivalTime <= now && !operation.ProcessedArrivals.Contains(index))
                    events.Add((result.ArrivalTime, arrivalKind, index));
            if (!operation.DeadlineFired && operation.Deadline <= now)
                events.Add((operation.Deadline, deadlineKind, Index: -1));

            foreach (var evt in events.OrderBy(e => e.Time).ThenBy(e => e.Kind))
            {
                if (evt.Kind == deadlineKind)
                {
                    operation.DeadlineFired = true;
                    if (operation.Status != ObservationOperationStatus.Pending)
                        continue;
                    var proposalsAtDeadline = operation.Arrived.Sum(r => r.Proposals.Count);
                    operation.Status = proposalsAtDeadline > 0
                        ? ObservationOperationStatus.PartialAtDeadline // 已覆盖 region 的真实 claims
                        : ObservationOperationStatus.TimedOut; // 预算内无可用结果（零投递）
                    operation.CompletedAt = evt.Time;
                    continue;
                }

                var result = operation.Script.Schedule[evt.Index];
                operation.ProcessedArrivals.Add(evt.Index);
                switch (operation.Status)
                {
                    case ObservationOperationStatus.Pending:
                        _ = operation.TryArrive(result); // 同 ResultId 重投 → Duplicates
                        // 到达即评估完成（事件时间语义）
                        if (operation.Arrived.Any(r => r.FailureReason is not null))
                        {
                            operation.Status = ObservationOperationStatus.Failed; // 零投递（≠ OK_EMPTY）
                            operation.CompletedAt = evt.Time;
                            break;
                        }
                        var coverage = operation.CoveredRegions();
                        if (operation.Arrived.Any(r => r.IsFinal)
                            && operation.Script.ScopeRegions
                                .All(region => coverage.Contains(region, StringComparer.Ordinal)))
                        {
                            operation.Status = ObservationOperationStatus.Complete; // 空帧 = 完成的显式负观察
                            operation.CompletedAt = evt.Time;
                        }
                        break;

                    case ObservationOperationStatus.Cancelled:
                        // 完成后到达：已交付 ResultId 的重投 → Duplicates；新结果 → 隔离
                        if (operation.Arrived.Any(r => r.ResultId == result.ResultId))
                            operation.Duplicates.Add(result.ResultId);
                        else
                            operation.CancelledQuarantined.Add(result.ResultId);
                        break;

                    default:
                        // Superseded/Failed/TimedOut/Complete/PartialAtDeadline：
                        // 完成后到达——重投去重，新结果隔离（不翻转已完成状态）
                        if (operation.Arrived.Any(r => r.ResultId == result.ResultId))
                            operation.Duplicates.Add(result.ResultId);
                        else
                            operation.StaleQuarantined.Add(result.ResultId);
                        break;
                }
            }
        }
    }

    // ---- operation 状态（feed 私有；快照只读导出）----

    private sealed class Operation
    {
        internal required ScriptedOperation Script { get; init; }
        internal required string OperationId { get; init; }
        internal ObservationOperationStatus Status { get; set; } = ObservationOperationStatus.Pending;
        internal DateTimeOffset? CompletedAt { get; set; }
        internal bool Delivered { get; set; }
        internal DateTimeOffset? DeliveredAt { get; set; }
        internal readonly List<ScheduledResult> Arrived = new();
        internal readonly List<string> Duplicates = new();
        internal readonly List<string> StaleQuarantined = new();
        internal readonly List<string> CancelledQuarantined = new();
        internal readonly HashSet<int> ProcessedArrivals = new();
        internal bool DeadlineFired;
        private readonly HashSet<string> _arrivedIds = new(StringComparer.Ordinal);

        internal DateTimeOffset OpenedAt => Script.OpenedAt;

        internal DateTimeOffset Deadline => Script.OpenedAt + Script.Budget;

        internal bool TryArrive(ScheduledResult result)
        {
            if (_arrivedIds.Contains(result.ResultId))
            {
                Duplicates.Add(result.ResultId);
                return false;
            }
            _arrivedIds.Add(result.ResultId);
            Arrived.Add(result);
            return true;
        }

        /// <summary>canonical merge：结果按（到达时间, ResultId）稳定排序。</summary>
        internal IReadOnlyList<ObservationProposal> MergedBatch() =>
            Arrived
                .OrderBy(r => r.ArrivalTime)
                .ThenBy(r => r.ResultId, StringComparer.Ordinal)
                .SelectMany(r => r.Proposals)
                .ToList();

        internal IReadOnlyList<string> CoveredRegions() =>
            Arrived.SelectMany(r => r.CoveredRegions)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToList();

        internal OperationSnapshot Snapshot() => new(
            OperationId,
            Script.Purpose,
            Script.CaptureArtifactId,
            Script.Context,
            Status,
            Script.ScopeRegions.OrderBy(r => r, StringComparer.Ordinal).ToList(),
            CoveredRegions(),
            Arrived.Select(r => r.ResultId).ToList(),
            Delivered
                ? Arrived.OrderBy(r => r.ArrivalTime)
                    .ThenBy(r => r.ResultId, StringComparer.Ordinal)
                    .Select(r => r.ResultId).ToList()
                : new List<string>(),
            Duplicates.ToList(),
            StaleQuarantined.ToList(),
            CancelledQuarantined.ToList(),
            OpenedAt,
            CompletedAt,
            DeliveredAt,
            DeliveredAt is { } at ? at - OpenedAt : null,
            Arrived.FirstOrDefault(r => r.FailureReason is not null)?.FailureReason,
            Delivered ? MergedBatch().Count : 0);
    }
}

/// <summary>
/// UAP-001 occurrence 派生 double（IUiObservationStrategy）。UAP-001 D9
/// （tracer 发现）：整帧替换语义与 partial 交付不相容——partial frame 会
/// 把未覆盖 region 的既有 occurrence 抹掉（伪装 absence）。本 double 采用
/// descriptor 键合并：新帧条目替换同 descriptor 的既有 occurrence；新帧未
/// 覆盖的既有条目 carry-over。这是 tracer 级假设（H9 邻接），非产品语义。
/// </summary>
internal sealed class MenuFrameObservationStrategy : IUiObservationStrategy
{
    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Claim.Subject != "live.frame"
            || !record.Claim.Value.StartsWith("{\"detects\"", StringComparison.Ordinal))
            return CarryOver(previous);

        using var document = JsonDocument.Parse(record.Claim.Value);
        var entries = new List<ProposedOccurrence>();
        foreach (var entry in document.RootElement.GetProperty("detects").EnumerateArray())
        {
            var role = entry.GetProperty("role").GetString()
                ?? throw new InvalidOperationException("live.frame detects 条目缺 role（fail closed）");
            var descriptor = entry.TryGetProperty("text", out var textProperty)
                && textProperty.GetString() is { Length: > 0 } text
                ? text
                : null;
            var state = entry.TryGetProperty("st", out var stateProperty)
                && stateProperty.GetString() is { Length: > 0 } stateValue
                ? stateValue
                : null;
            var bounds = entry.GetProperty("b").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            entries.Add(new ProposedOccurrence(
                OwningContainerId: null,
                Role: role,
                SemanticDescriptor: descriptor,
                State: state,
                Locator: new SpatialLocator(bounds[0], bounds[1], bounds[2], bounds[3],
                    AdbEffectDriver.SupportedFrame),
                Native: null));
        }

        // D9 合并：新帧覆盖的 descriptor 替换；未覆盖的 carry-over（partial
        // 不得伪装 absence）。descriptor 为 null 的条目不参与 carry-over 匹配。
        var coveredDescriptors = entries
            .Select(e => e.SemanticDescriptor)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var owner = previous?.Containers is { Count: 1 } containers
            ? containers[0].Identity.ContainerId
            : null;
        var carried = (previous?.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Where(o => o.SemanticDescriptor is null
                || !coveredDescriptors.Contains(o.SemanticDescriptor))
            .Select(o => new ProposedOccurrence(
                o.OwningContainerId ?? owner, o.Role, o.SemanticDescriptor,
                o.State, o.Locator, o.Native));
        return entries.Concat(carried).ToList();
    }

    private static IReadOnlyList<ProposedOccurrence> CarryOver(WorldBeliefRevision? previous)
    {
        if (previous?.Occurrences is not { Count: > 0 } carried)
            return Array.Empty<ProposedOccurrence>();
        var owner = previous.Containers is { Count: 1 } containers ? containers[0].Identity.ContainerId : null;
        return carried
            .Select(o => new ProposedOccurrence(
                o.OwningContainerId ?? owner, o.Role, o.SemanticDescriptor,
                o.State, o.Locator, o.Native))
            .ToList();
    }
}

/// <summary>UAP-001 场景声明（脚本 + 契约 + agent 脚本）。</summary>
internal sealed record AsyncScenario(
    string ScenarioId,
    DateTimeOffset T0,
    ExecutionContract Contract,
    AgentScriptStep AgentScript,
    IReadOnlyList<ScriptedOperation> Operations);

/// <summary>
/// UAP-001 组合根：全部 L2 product modules 真实（UniKernel / KernelRunDriver /
/// EvidenceLedger / WorldModel / ControlLoop / RuntimeAssurance / EffectBoundary
/// / FastPerception + LiveVisionStrategy），外部缝确定性（AsyncObservationFeed /
/// ScriptedUniAgent / DeterministicEffectDriver / SeedingAssociationStrategy /
/// MenuFrameObservationStrategy）。Trace = DisabledRunTrace：Phase 4 tracer
/// 的 trace 半边不属本 Change；Disabled 臂结构性保证 Trace 零耦合（Trace 仅
/// 异步诊断原则不受本 tracer 影响的负证据）。
/// </summary>
internal sealed class AsyncPerceptionHost
{
    internal UniKernel KernelCore { get; }
    internal KernelRunDriver Driver { get; }
    internal AgentPlanPolicy Plan { get; }
    internal ScriptedUniAgent ScriptedAgent { get; }
    internal DeterministicEffectDriver EffectDriver { get; }
    internal AsyncObservationFeed Feed { get; }
    internal VirtualClock Clock { get; }
    internal RuntimeStageMetrics Metrics { get; }
    internal RunModel RunModelCore { get; }
    internal EffectBoundary EffectBoundaryCore { get; }
    internal RuntimeAssurance AssuranceCore { get; }
    internal WorldModel WorldCore { get; }
    internal EvidenceLedger LedgerCore { get; }

    internal string ScenarioId { get; }

    private readonly List<RunDriveResult> _driveResults = new();

    internal IReadOnlyList<RunDriveResult> DriveResults => _driveResults;

    internal AsyncPerceptionHost(AsyncScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ScenarioId = scenario.ScenarioId;
        Clock = new VirtualClock(scenario.T0);
        var world = new WorldModel(
            scenario.Contract.Scope!,
            new SeedingAssociationStrategy(),
            new MenuFrameObservationStrategy());
        var assurance = new RuntimeAssurance(new SatisfyingFreshness());
        EffectDriver = new DeterministicEffectDriver(scenario.T0);
        EffectBoundaryCore = new EffectBoundary(EffectDriver);
        Metrics = new RuntimeStageMetrics();
        Plan = new AgentPlanPolicy();
        LedgerCore = new EvidenceLedger();
        Feed = new AsyncObservationFeed(Clock, scenario.Operations);
        ScriptedAgent = new ScriptedUniAgent(scenario.AgentScript);
        RunModelCore = new RunModel();
        KernelCore = new UniKernel(
            LedgerCore, world, DisabledRunTrace.Instance,
            RunModelCore, new ControlLoop(Plan), assurance, EffectBoundaryCore, Metrics);
        WorldCore = world;
        AssuranceCore = assurance;
        Driver = new KernelRunDriver(KernelCore, Plan, new RunDriverInputs
        {
            NextInput = Feed.Next,
            ConsultAgent = ScriptedAgent.Consult,
        });
    }

    /// <summary>包裹一次 Driver.Drive()（terminal 后标记 ScriptedAgent）。</summary>
    internal RunDriveResult DriveOnce()
    {
        var result = Driver.Drive();
        _driveResults.Add(result);
        if (KernelCore.IsRunTerminal)
            ScriptedAgent.MarkTerminal();
        return result;
    }

    /// <summary>确定性 freshness double（VerificationMode 声明面见 report）。</summary>
    private sealed class SatisfyingFreshness : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "uap001:sufficient");
    }
}

// ============================================================================
// realization 计划构建（四 realization + 场景脚本）。
// D8：fast/oneshot stage 经真实 FastPerception + LiveVisionStrategy 消费合成
// provider response（live one-shot/fast 策略代码路径）；slow/review stage =
// recorded executable double（truth 对齐或错误臂）。
// ============================================================================

/// <summary>一条元素在某个 stage 的判定（typing + 可选 state + 是否被覆盖）。</summary>
internal sealed record TypedElement(
    AsyncPerceptionTruth.ElementSpec Element,
    string AssumedType,
    string? State = null);

/// <summary>realization 计划构建器。</summary>
internal static class AsyncRealizations
{
    internal const string ProducerRuntime = "perception.runtime";
    internal const string ProducerReview = "review.manifest";

    /// <summary>capture identity（= RawArtifact.Capture 的内容寻址 id 派生）。</summary>
    internal static string CaptureIdOf(byte[] providerResponse)
    {
        var hash = Convert.ToHexString(SHA256.HashData(providerResponse)).ToLowerInvariant();
        return "art-" + hash[..16];
    }

    /// <summary>
    /// fast/oneshot stage 结果：合成 provider response（元素 × 判定 typing）→
    /// 真实 FastPerception + LiveVisionStrategy 解析 → join 为 live.frame +
    /// typing claims。isFinal=false 表达「该 stage 非最终覆盖」（Fast→Slow 中
    /// 的 fast partial）。
    /// </summary>
    internal static ScheduledResult ModelStageResult(
        ScriptedOperation script,
        string resultId,
        string stage,
        DateTimeOffset arrival,
        bool IsFinal,
        IReadOnlyList<TypedElement> typedElements,
        IReadOnlyList<(string Subject, string Value)>? extraStateClaims = null,
        string producer = ProducerRuntime,
        IReadOnlyList<string>? coveredRegionsOverride = null)
    {
        var response = AsyncPerceptionTruth.BuildProviderResponse(
            typedElements.Select(t => (t.Element, t.AssumedType)));
        var artifact = RawArtifact.Capture(response, new ArtifactMetadata(
            1080, 1920, "artifact", script.OpenedAt, CaptureScope: null));
        var perception = new FastPerception(producer, new LiveVisionStrategy());
        var observations = perception.Observe(artifact, script.Context);

        var elements = typedElements.Select(t => t.Element).ToList();
        var states = typedElements.ToDictionary(t => t.Element.ElementId, t => t.State);
        var (frameValue, typings) = AsyncPerceptionTruth.JoinDetections(observations, elements, states);

        var operationId = ObservationOperationId.For(script);
        var lineage = new List<string>
        {
            $"capture:{artifact.ArtifactId}",
            $"op:{operationId}",
            $"stage:{stage}",
            $"result:{resultId}",
        };
        var scope = $"obs:{operationId}:{stage}";
        var proposals = new List<ObservationProposal>
        {
            new(
                new ObservationClaim("live.frame", frameValue),
                IngressKind.Observation,
                script.Context,
                new Provenance(producer, script.OpenedAt, scope, lineage)),
        };
        foreach (var (subject, value) in typings)
            proposals.Add(new ObservationProposal(
                new ObservationClaim(subject, value),
                IngressKind.Observation,
                script.Context,
                new Provenance(producer, script.OpenedAt, scope, lineage)));
        if (extraStateClaims is not null)
            foreach (var (subject, value) in extraStateClaims)
                proposals.Add(new ObservationProposal(
                    new ObservationClaim(subject, value),
                    IngressKind.Observation,
                    script.Context,
                    new Provenance(producer, script.OpenedAt, scope, lineage)));

        return new ScheduledResult(
            resultId,
            arrival,
            stage,
            IsFinal,
            coveredRegionsOverride ?? typedElements.Select(t => RegionFor(t.Element)).ToList(),
            FailureReason: null,
            proposals);
    }

    /// <summary>
    /// slow/review double 结果：不经模型（recorded executable double）——
    /// 直接由（元素 × 判定 typing）构造 live.frame + typing claims；lineage
    /// 关联同一 capture（§5.1：Slow 对同一 capture 的识别仍关联该 capture）。
    /// </summary>
    internal static ScheduledResult RecordedStageResult(
        ScriptedOperation script,
        string resultId,
        string stage,
        DateTimeOffset arrival,
        bool IsFinal,
        IReadOnlyList<TypedElement> typedElements,
        IReadOnlyList<(string Subject, string Value)>? extraStateClaims = null,
        string producer = ProducerRuntime)
    {
        var operationId = ObservationOperationId.For(script);
        var captureTime = script.OpenedAt;
        var lineage = new List<string>
        {
            $"capture:{script.CaptureArtifactId}",
            $"op:{operationId}",
            $"stage:{stage}",
            $"result:{resultId}",
        };
        var scope = $"obs:{operationId}:{stage}";
        var entries = new List<string>();
        var proposals = new List<ObservationProposal>();
        foreach (var typed in typedElements)
        {
            var bounds = AsyncPerceptionTruth.NormalizedBoundsFor(typed.Element.Cy);
            var stateSegment = typed.State is null ? "" : $",\"st\":\"{typed.State}\"";
            entries.Add($"{{\"id\":\"{typed.Element.ElementId}\",\"cls\":\"{typed.AssumedType}\","
                + $"\"role\":\"{AsyncPerceptionTruth.RoleForType(typed.AssumedType)}\","
                + $"\"text\":\"{AsyncPerceptionTruth.JsonEscape(typed.Element.Text)}\"{stateSegment},"
                + $"\"b\":[{bounds[0]},{bounds[1]},{bounds[2]},{bounds[3]}]}}");
            proposals.Add(new ObservationProposal(
                new ObservationClaim($"ui.typing.{typed.Element.ElementId}", typed.AssumedType),
                IngressKind.Observation,
                script.Context,
                new Provenance(producer, captureTime, scope, lineage)));
        }
        proposals.Insert(0, new ObservationProposal(
            new ObservationClaim("live.frame", $"{{\"detects\":[{string.Join(",", entries)}]}}"),
            IngressKind.Observation,
            script.Context,
            new Provenance(producer, captureTime, scope, lineage)));
        if (extraStateClaims is not null)
            foreach (var (subject, value) in extraStateClaims)
                proposals.Add(new ObservationProposal(
                    new ObservationClaim(subject, value),
                    IngressKind.Observation,
                    script.Context,
                    new Provenance(producer, captureTime, scope, lineage)));

        return new ScheduledResult(
            resultId,
            arrival,
            stage,
            IsFinal,
            typedElements.Select(t => RegionFor(t.Element)).ToList(),
            FailureReason: null,
            proposals);
    }

    /// <summary>失败记录（零 proposal；≠ OK_EMPTY——绝不产生空帧声明）。</summary>
    internal static ScheduledResult FailureResult(
        ScriptedOperation script, string resultId, string stage, DateTimeOffset arrival, string reason) =>
        new(resultId, arrival, stage, IsFinal: true,
            Array.Empty<string>(), reason, Array.Empty<ObservationProposal>());

    internal static string RegionFor(AsyncPerceptionTruth.ElementSpec element) => element.ElementId;

    /// <summary>契约 scope 构建：live.frame + typing subjects + 场景状态 subjects。</summary>
    internal static HashSet<string> ScopeOf(
        IEnumerable<AsyncPerceptionTruth.ElementSpec> elements,
        params string[] extraSubjects)
    {
        var scope = new HashSet<string>(StringComparer.Ordinal) { "live.frame" };
        foreach (var element in elements)
            scope.Add($"ui.typing.{element.ElementId}");
        foreach (var subject in extraSubjects)
            scope.Add(subject);
        return scope;
    }
}
