using System.Collections.Generic;
using System.Diagnostics;

namespace UniClaw.Kernel.Diagnostics;

/// <summary>
/// Product Runtime 派生路径的观察阶段（LAT-001 词表，非权威）。每阶段的
/// 规模语义在成员注释中固定；调用次数 = 该阶段 aggregate 的 Invocations。
/// </summary>
public enum RuntimeStage
{
    /// <summary>RawArtifact.Capture（v0.1 为 host-owned seam；input=payload bytes；output=1）。</summary>
    CaptureArtifact,

    /// <summary>IFastPerceptionStrategy.Observe（input=payload bytes；output=observations 数）。</summary>
    FastPerceptionStrategy,

    /// <summary>ArtifactObservation → ObservationProposal 包装（input=observations 数；output=proposals 数）。</summary>
    ProposalEmission,

    /// <summary>EvidenceLedger.Admit 经 UniKernel.Process（input=1 proposal；output=accepted?1:0）。</summary>
    EvidenceAdmission,

    /// <summary>WorldModel.Reconcile 经 UniKernel.Process（input=parent world-state entries；output=新 revision?1:0）。</summary>
    WorldReconciliation,

    /// <summary>IUiObservationStrategy.Derive（input=parent occurrences 数；output=proposed occurrences 数）。</summary>
    OccurrenceDerivation,

    /// <summary>WorldModel.DeriveSlice（input=occurrences scanned；secondary=world-state entries scanned；output=slice occurrences 数）。</summary>
    SliceDerivation,

    /// <summary>WorldModel.ResolveCurrent（input=occurrences scanned；output=matched candidates 数）。</summary>
    CurrentGrounding,
}

/// <summary>
/// 单阶段聚合（immutable 快照单元）：invocations、wall-clock elapsed
/// ticks 总量与极值（无分布直方图——事实基线阶段不设门槛，D1）、
/// 输入 / 次级输入 / 输出规模总量与极值。
/// </summary>
public sealed record RuntimeStageAggregate(
    long Invocations,
    long TotalTicks,
    long MinTicks,
    long MaxTicks,
    long TotalInputSize,
    long MaxInputSize,
    long TotalSecondarySize,
    long MaxSecondarySize,
    long TotalOutputSize,
    long MaxOutputSize);

/// <summary>
/// WMP-001 owner-internal operation vocabulary. This is deliberately not a
/// public telemetry contract: it exists only to produce reproducible change
/// evidence without expanding the Product Runtime interface.
/// </summary>
internal enum WorldModelOperation
{
    RevisionIndexBuild,
    Reconcile,
    DeriveSlice,
    ResolveCurrent,
    ResolveContinuity,
    ConsumerViewDerivation,
    DemandLookup,
}

/// <summary>WMP-001 deterministic work/allocation snapshot.</summary>
internal sealed record WorldModelPerformanceAggregate(
    long Invocations,
    long ScannedEntries,
    long CopiedEntries,
    long OutputEntries,
    long AllocatedBytes,
    long TotalTicks);

/// <summary>
/// RuntimeStageMetrics — Product Runtime 派生路径的低开销观察收集器
/// （LAT-001；非权威观察面，与 IRunTrace 同族但职责不同：trace = 结构
/// 因果，本类型 = 量化计数）。契约：
/// <list type="bullet">
/// <item>非权威：一切数据仅供观察/基线，不进入任何 Owner 决策、不是
/// canonical state、不参与 replay 判定。</item>
/// <item>elapsed = Stopwatch.GetTimestamp 差值（wall-clock 观测元数据；
/// 系统无 canonical clock——协议 deferred ⑪ 不动）。</item>
/// <item>禁用 = 不注入（null）；启用方（组合根 / benchmark）持有实例，
/// 调用侧每次仅一次 null check，无其他开销。</item>
/// <item>并发面（FCR-001 后）：product 派生管线（admission/reconcile）
/// 维持单线程纪律；perception 缝经 single-flight 契约可并发调用——
/// Record / CountArtifactPresentation 内部锁串行化，缓存计数
/// Interlocked 原子；计数与聚合语义不变。</item>
/// <item>只增不改：聚合单调累加，无重置 / 删除面。</item>
/// </list>
/// LAT-001 D9（OTel 可接性约束）：本类型接口面保持窄且稳定——未来若经
/// BCL Meter / ActivitySource 桥接标准信号生态，只增发射路径、不改本面。
/// </summary>
public sealed class RuntimeStageMetrics
{
    private readonly Dictionary<RuntimeStage, Aggregate> _stages = new();
    private readonly Dictionary<WorldModelOperation, WorldModelAggregate> _worldModel = new();
    private readonly HashSet<string> _distinctArtifacts = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>artifact 进入 perception 的次数（presentation）。</summary>
    public long ArtifactsPresented { get; private set; }

    /// <summary>出现过的不同 ArtifactId 数（内容寻址去重）。</summary>
    public long DistinctArtifacts => _distinctArtifacts.Count;

    /// <summary>evidence admission 结果计数：accepted。</summary>
    public long AdmissionsAccepted { get; private set; }

    /// <summary>evidence admission 结果计数：rejected。</summary>
    public long AdmissionsRejected { get; private set; }

    /// <summary>reconciliation 结果计数：产生新 revision。</summary>
    public long ReconciliationsNew { get; private set; }

    /// <summary>reconciliation 结果计数：幂等（零新 revision）。</summary>
    public long ReconciliationsIdempotent { get; private set; }

    // ---- FCR-001 additive 缓存观察计数（不改既有 stage / 计数定义，D8）----
    // 本组计数经 Interlocked 原子累加：缓存缝是 product runtime 中唯一的
    // 并发观察调用方（single-flight 等待者与 elected 并行上报）；其余
    // 计数 / stage 聚合维持 LAT-001 单线程语义不变。

    private long _cacheLookups;
    private long _cacheHits;
    private long _cacheMisses;
    private long _cacheEvictions;
    private long _cacheSingleFlightWaits;

    /// <summary>缓存查找次数（cache 启用时的每次 Observe）。</summary>
    public long CacheLookups => Interlocked.Read(ref _cacheLookups);

    /// <summary>缓存命中次数。</summary>
    public long CacheHits => Interlocked.Read(ref _cacheHits);

    /// <summary>缓存未命中次数（含等待者与被淘汰后的重访）。</summary>
    public long CacheMisses => Interlocked.Read(ref _cacheMisses);

    /// <summary>容量满载插入时的 LRU 淘汰次数。</summary>
    public long CacheEvictions => Interlocked.Read(ref _cacheEvictions);

    /// <summary>single-flight 等待者次数（同键并发等待已有计算）。</summary>
    public long CacheSingleFlightWaits => Interlocked.Read(ref _cacheSingleFlightWaits);

    /// <summary>缓存查找（FCR-001）。</summary>
    public void CountCacheLookup() => Interlocked.Increment(ref _cacheLookups);

    /// <summary>缓存命中（FCR-001）。</summary>
    public void CountCacheHit() => Interlocked.Increment(ref _cacheHits);

    /// <summary>缓存未命中（FCR-001）。</summary>
    public void CountCacheMiss() => Interlocked.Increment(ref _cacheMisses);

    /// <summary>LRU 淘汰（FCR-001）。</summary>
    public void CountCacheEviction() => Interlocked.Increment(ref _cacheEvictions);

    /// <summary>single-flight 等待（FCR-001）。</summary>
    public void CountCacheSingleFlightWait() => Interlocked.Increment(ref _cacheSingleFlightWaits);

    /// <summary>记录一次阶段完成（异常路径不记录——只记成功完成的阶段）。
    /// FCR-001 评审修复：perception 缝在 single-flight 契约下可被并发调用，
    /// 本方法与 CountArtifactPresentation 经内部锁串行化（无争用锁开销
    /// ~ns 级；计数 / 聚合语义不变）。</summary>
    public void Record(
        RuntimeStage stage, long elapsedTicks, long inputSize = 0, long secondarySize = 0, long outputSize = 0)
    {
        lock (_gate)
        {
            if (!_stages.TryGetValue(stage, out var aggregate))
            {
                aggregate = new Aggregate();
                _stages[stage] = aggregate;
            }
            aggregate.Invocations++;
            aggregate.TotalTicks += elapsedTicks;
            aggregate.MinTicks = aggregate.Invocations == 1 ? elapsedTicks : Math.Min(aggregate.MinTicks, elapsedTicks);
            aggregate.MaxTicks = Math.Max(aggregate.MaxTicks, elapsedTicks);
            aggregate.TotalInputSize += inputSize;
            aggregate.MaxInputSize = Math.Max(aggregate.MaxInputSize, inputSize);
            aggregate.TotalSecondarySize += secondarySize;
            aggregate.MaxSecondarySize = Math.Max(aggregate.MaxSecondarySize, secondarySize);
            aggregate.TotalOutputSize += outputSize;
            aggregate.MaxOutputSize = Math.Max(aggregate.MaxOutputSize, outputSize);
        }
    }

    /// <summary>artifact 进入 perception 侧的 intake 计数（perception 真实缝；
    /// FCR-001 评审修复：内部锁串行化，见 Record 注释）。</summary>
    public void CountArtifactPresentation(string artifactId)
    {
        lock (_gate)
        {
            ArtifactsPresented++;
            if (!string.IsNullOrEmpty(artifactId))
                _distinctArtifacts.Add(artifactId);
        }
    }

    /// <summary>admission 结果计数（UniKernel.Process 缝）。</summary>
    public void CountAdmission(bool accepted)
    {
        if (accepted) AdmissionsAccepted++;
        else AdmissionsRejected++;
    }

    /// <summary>reconciliation 结果计数（UniKernel.Process 缝）。</summary>
    public void CountReconciliation(bool newRevision)
    {
        if (newRevision) ReconciliationsNew++;
        else ReconciliationsIdempotent++;
    }

    /// <summary>全部阶段的 immutable 聚合快照。</summary>
    public IReadOnlyDictionary<RuntimeStage, RuntimeStageAggregate> Stages =>
        _stages.ToDictionary(
            kv => kv.Key,
            kv => new RuntimeStageAggregate(
                kv.Value.Invocations, kv.Value.TotalTicks, kv.Value.MinTicks, kv.Value.MaxTicks,
                kv.Value.TotalInputSize, kv.Value.MaxInputSize,
                kv.Value.TotalSecondarySize, kv.Value.MaxSecondarySize,
                kv.Value.TotalOutputSize, kv.Value.MaxOutputSize));

    /// <summary>WMP-001 internal evidence surface; never exposed by Product Runtime.</summary>
    internal IReadOnlyDictionary<WorldModelOperation, WorldModelPerformanceAggregate> WorldModelPerformance
    {
        get
        {
            lock (_gate)
            {
                return _worldModel.ToDictionary(
                    kv => kv.Key,
                    kv => new WorldModelPerformanceAggregate(
                        kv.Value.Invocations,
                        kv.Value.ScannedEntries,
                        kv.Value.CopiedEntries,
                        kv.Value.OutputEntries,
                        kv.Value.AllocatedBytes,
                        kv.Value.TotalTicks));
            }
        }
    }

    /// <summary>Record exact World Model implementation work for WMP-001 evidence.</summary>
    internal void RecordWorldModel(
        WorldModelOperation operation,
        long scannedEntries,
        long copiedEntries,
        long outputEntries,
        long allocatedBytes,
        long elapsedTicks)
    {
        lock (_gate)
        {
            if (!_worldModel.TryGetValue(operation, out var aggregate))
            {
                aggregate = new WorldModelAggregate();
                _worldModel[operation] = aggregate;
            }

            aggregate.Invocations++;
            aggregate.ScannedEntries += scannedEntries;
            aggregate.CopiedEntries += copiedEntries;
            aggregate.OutputEntries += outputEntries;
            aggregate.AllocatedBytes += allocatedBytes;
            aggregate.TotalTicks += elapsedTicks;
        }
    }

    /// <summary>ticks → 毫秒（观测换算；消费侧渲染用）。</summary>
    public static double TicksToMilliseconds(long ticks) =>
        ticks * 1000.0 / Stopwatch.Frequency;

    private sealed class Aggregate
    {
        public long Invocations;
        public long TotalTicks;
        public long MinTicks;
        public long MaxTicks;
        public long TotalInputSize;
        public long MaxInputSize;
        public long TotalSecondarySize;
        public long MaxSecondarySize;
        public long TotalOutputSize;
        public long MaxOutputSize;
    }

    private sealed class WorldModelAggregate
    {
        public long Invocations;
        public long ScannedEntries;
        public long CopiedEntries;
        public long OutputEntries;
        public long AllocatedBytes;
        public long TotalTicks;
    }
}
