using System.Collections.Immutable;
using System.Threading.Channels;

namespace UniClaw.Kernel.Trace;

/// <summary>
/// TRW-001 异步 writer 注入选项：持久化目录由 Host 注入（D5）；
/// ChannelCapacity / BatchSize 为 bounded sink 与批量消费形态；
/// BatchDelay 注入消费侧减速（验证 capture 不阻塞，D6）；
/// FaultAfterRecords 按已消费计数注入消费侧崩溃（CaptureFailed 触发路径，
/// D3）。review 修法 4 确定性测试钩子：BeforeBatchFlushGate 在第 n 批
/// flush 前同步调用（可阻塞 consumer 线程——测试以 ManualResetEventSlim
/// 驱动 stall/release，禁 Task.Delay 竞态）；SealPersistFault 注入 seal
/// 持久化失败（Deterministic）。internal realization，非公共契约（TRW-001 D1）。
/// </summary>
internal sealed record AsyncTraceWriterOptions(
    string PersistenceDirectory,
    int ChannelCapacity = 1024,
    int BatchSize = 16,
    TimeSpan? DrainTimeout = null,
    Func<int, Task>? BatchDelay = null,
    Func<int, Exception>? FaultAfterRecords = null,
    Action<int>? BeforeBatchFlushGate = null,
    Func<string, Exception>? SealPersistFault = null);

/// <summary>
/// 真实异步 Trace writer（TRW-001 D1–D3）：capture path = O(1) 有界
/// enqueue（TryWrite；队列满 → 显式丢弃计数 + TraceDiagnostic，绝不
/// 阻塞、绝不抛错——与 UniKernel StartTraced/TryRecord/TryComplete 的
/// fail-safe 吸收面互补）；后台单 consumer 批量消费并持久化到注入目录；
/// Finalize = bounded drain → 按 InMemoryRunTrace 同一语义组装
/// RunTraceArtifact → seal（canonical rendering SHA-256 随 artifact 携带）。
/// 词表执法 / span 组装 / RecorderTerminal 规则逐条镜像 InMemoryRunTrace
/// （校验在 capture 调用路径同步执行，与 InMemory 的选择一致）；差异点：
/// emission 观测经队列传递（marker record），drain 后才对组装可见——
/// 队列满导致 marker 被丢弃时诚实地降级为 Quarantined。writer 故障 →
/// CaptureFailed（现有预留终态的 FIRST 真实触发路径，D3）。
/// internal realization，非公共契约（TRW-001 D1）。
/// </summary>
internal sealed class AsyncFileTraceWriter : IRunTrace, IRunTraceSink
{
    private const string SchemaVersion = "trc/0.1";

    /// <summary>
    /// review 修法 3：artifact 内 RecorderDiagnostics 的总条数上限。
    /// 样本列表只保留 DiagnosticSampleCap 条（为 finalize 关键诊断
    /// [dropped-records / drain-timeout / unflushed / emission / capped
    /// 汇总] 预留席位），超出部分只累计计数。
    /// </summary>
    private const int DiagnosticCap = 32;
    private const int SampleCap = DiagnosticCap - 5;

    private readonly string _runId;
    private readonly string _traceId;
    private readonly AsyncTraceWriterOptions _options;
    private readonly Channel<TraceRecord> _channel;
    private readonly CancellationTokenSource _consumerCts = new();
    private readonly Task _consumerTask;

    // 锁序（无环）：_captureLock → _sync → _diagnosticsLock。
    // consumer 线程只取 _sync / _diagnosticsLock，绝不取 _captureLock。
    private readonly object _captureLock = new();
    private readonly object _sync = new();
    private readonly object _diagnosticsLock = new();

    private readonly Dictionary<string, CaptureSpanState> _openSpans = new();
    private readonly List<TraceDiagnostic> _diagnostics = new();
    private readonly List<TraceDiagnostic> _finalizeDiagnostics = new();
    private readonly List<TraceRecord> _journal = new();

    private bool _consumerExited;
    private bool _faulted;
    private long _droppedCount;
    private long _consumedRecords;
    private int _pendingUnflushed;
    private int _cappedDiagnostics;
    private int _sequence;
    private RunTraceArtifact? _finalized;

    public AsyncFileTraceWriter(RunCorrelation correlation, AsyncTraceWriterOptions options)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _runId = correlation.RunId;
        _traceId = "trc-" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(correlation.RunId))).ToLowerInvariant()[..12];
        _channel = Channel.CreateBounded<TraceRecord>(new BoundedChannelOptions(
            Math.Max(1, _options.ChannelCapacity))
        {
            SingleReader = true,
            // FullMode 仅在 Wait 写路径有意义；本 writer 只用 TryWrite，
            // 满时显式丢弃（D2），任何情况不得阻塞 Runtime 线程。
            FullMode = BoundedChannelFullMode.Wait,
        });
        _consumerTask = Task.Run(() => ConsumeAsync(_consumerCts.Token));
    }

    // ---- 测试观察面（drain/seal 之后读取；D2/D3 可诊断性）----

    /// <summary>capture 队列满显式丢弃计数。</summary>
    internal long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>诊断快照（词表执法 / 丢弃 / writer 故障 / emission 缺失）。</summary>
    internal IReadOnlyList<TraceDiagnostic> Diagnostics
    {
        get { lock (_diagnosticsLock) return _diagnostics.ToList(); }
    }

    /// <summary>后台 consumer 是否已故障（→ CaptureFailed 终态）。</summary>
    internal bool IsFaulted
    {
        get { lock (_sync) return _faulted; }
    }

    /// <summary>consumer 已消费（并持久化）的 record 数。</summary>
    internal long ConsumedRecords => Interlocked.Read(ref _consumedRecords);

    /// <summary>累计诊断总数（含被上限截断的样本；bounded diagnostics 项）。</summary>
    internal long CumulativeDiagnosticCount
    {
        get { lock (_diagnosticsLock) return _diagnostics.Count + _cappedDiagnostics; }
    }

    // ---- capture path（Runtime 线程；全路径 no-throw，镜像 InMemoryRunTrace）----

    public ITraceOperationScope StartOperation(
        SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references)
    {
        try
        {
            lock (_captureLock)
            {
                if (_finalized is not null)
                {
                    Diagnose("start-after-finalize");
                    return NoOpOperationScope.Instance;
                }
                if (definition is null)
                {
                    Diagnose("definition-null");
                    return NoOpOperationScope.Instance;
                }
                if (definition.IsProvisional)
                {
                    Diagnose($"operation-provisional:{definition.OperationId}");
                    return NoOpOperationScope.Instance;
                }

                var sequence = _sequence++;
                var spanId = $"sp-{sequence:D4}";
                var state = new CaptureSpanState(definition, spanId);
                _openSpans.Add(spanId, state);
                Enqueue(new SpanStartRecord(
                    spanId, parent?.SpanId, definition.OperationId, sequence + 1,
                    FilterReferences(definition, references)));
                return new AsyncOperationScope(this, state, _traceId, spanId);
            }
        }
        catch (Exception e)
        {
            Diagnose($"recorder-fault:{e.GetType().Name}");
            return NoOpOperationScope.Instance;
        }
    }

    public void MarkOutcomeEmitted()
    {
        lock (_captureLock)
        {
            if (_finalized is not null)
            {
                Diagnose("mark-after-finalize");
                return;
            }
            Enqueue(new EmissionMarkerRecord());
        }
    }

    private void Record(
        CaptureSpanState span, TraceEventDefinition eventDefinition,
        IReadOnlyList<TraceReference> references, string? reasonCode)
    {
        lock (_captureLock)
        {
            if (_finalized is not null)
            {
                Diagnose("record-after-finalize");
                return;
            }
            if (span.Completed)
            {
                Diagnose("record-after-complete");
                return;
            }
            if (eventDefinition is null || !span.Definition.AllowedEvents.Contains(eventDefinition))
            {
                Diagnose($"event-not-allowed:{eventDefinition?.EventId ?? "null"}:{span.Definition.OperationId}");
                return;
            }
            if (eventDefinition.ReasonCodeRequired && string.IsNullOrEmpty(reasonCode))
            {
                Diagnose($"reason-code-required:{eventDefinition.EventId}");
                return;
            }
            if (reasonCode is not null && !eventDefinition.AllowedReasonCodes.Contains(reasonCode))
            {
                Diagnose($"reason-code-not-allowed:{reasonCode}:{eventDefinition.EventId}");
                return;
            }
            Enqueue(new SpanEventRecord(
                span.SpanId, eventDefinition.EventId,
                FilterReferences(span.Definition, references), reasonCode));
        }
    }

    private void Complete(CaptureSpanState span, StructuralOutcome outcome)
    {
        lock (_captureLock)
        {
            if (_finalized is not null)
            {
                Diagnose("complete-after-finalize");
                return;
            }
            if (span.Completed)
            {
                Diagnose("double-complete");
                return;
            }
            span.Completed = true;
            Enqueue(new SpanCompleteRecord(span.SpanId, outcome));
        }
    }

    /// <summary>词表执法（镜像 InMemoryRunTrace.FilterReferences）：非法 ref kind 丢弃 + diagnostic。</summary>
    private TraceReference[] FilterReferences(
        SpanDefinition definition, IReadOnlyList<TraceReference> references)
    {
        var kept = new List<TraceReference>();
        foreach (var reference in references ?? Array.Empty<TraceReference>())
        {
            if (definition.AllowedReferenceKinds.Contains(reference.Kind))
                kept.Add(reference);
            else
                Diagnose($"reference-kind-not-allowed:{reference.Kind}:{definition.OperationId}");
        }
        return kept.ToArray();
    }

    /// <summary>有界 enqueue（D2）：满 → 显式丢弃计数 + diagnostic，绝不阻塞、绝不抛错。</summary>
    private void Enqueue(TraceRecord record)
    {
        if (!_channel.Writer.TryWrite(record))
        {
            Interlocked.Increment(ref _droppedCount);
            Diagnose("capture-dropped");
        }
    }

    /// <summary>
    /// 常规诊断（review 修法 3：有界样本）——超上限只累计计数，不无限堆积。
    /// </summary>
    private void Diagnose(string reason)
    {
        lock (_diagnosticsLock)
        {
            if (_diagnostics.Count >= SampleCap)
                _cappedDiagnostics++;
            else
                _diagnostics.Add(new TraceDiagnostic(reason));
        }
    }

    /// <summary>
    /// finalize 关键诊断（drain-timeout / unflushed accounting / 丢弃计数 /
    /// seal 持久化失败 / capped 汇总）：不占样本预算、不被截断——终态裁决
    /// 依据必须在产物中可见。
    /// </summary>
    private void DiagnoseCritical(string reason)
    {
        lock (_diagnosticsLock) _finalizeDiagnostics.Add(new TraceDiagnostic(reason));
    }

    // ---- 后台 consumer（单 reader；批量消费 + 批间可注入延迟/崩溃）----

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        System.IO.StreamWriter? stream = null;
        var persistedCount = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var batch = new List<TraceRecord>(_options.BatchSize);
                lock (_sync)
                {
                    while (batch.Count < _options.BatchSize && _channel.Reader.TryRead(out var record))
                        batch.Add(record);
                    // review 修法 1 的 accounting 面：已读出但尚未 flush 的条数
                    _pendingUnflushed = batch.Count;
                }
                if (batch.Count == 0)
                {
                    if (!await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        break;
                    continue;
                }

                // BatchDelay 在两次 flush 之间注入消费侧减速（D6 验收手段）
                if (_options.BatchDelay is not null)
                    await _options.BatchDelay(persistedCount).ConfigureAwait(false);

                // review 修法 4：确定性测试闸门（同步调用，可阻塞 consumer 线程）
                if (_options.BeforeBatchFlushGate is not null)
                    _options.BeforeBatchFlushGate(persistedCount);

                stream ??= CreateStream();
                foreach (var record in batch)
                {
                    if (_options.FaultAfterRecords?.Invoke(persistedCount) is { } injected)
                        throw injected;
                    stream.WriteLine(RenderLine(record));
                    persistedCount++;
                }
                stream.Flush();
                // review 修法 2：journal 只在整批 Flush 成功后入账——journal
                // 恒等于已刷盘记录，drain 超时快照不能把未刷盘记录算作已录制。
                lock (_sync)
                {
                    _journal.AddRange(batch);
                    _pendingUnflushed = 0;
                }
                Interlocked.Add(ref _consumedRecords, batch.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 正常关停：入口关闭后的等待退出（Completed 路径不 cancel，consumer
            // 自然排空退出），或 drain 超时的恢复性 cancel——都不是 writer 故障。
        }
        catch (Exception e)
        {
            // 消费侧崩溃（注入或真实 IO 故障）→ Faulted：停止消费，队列中
            // 剩余 record 即为丢失；Finalize 将产出 RecorderTerminal=CaptureFailed
            // （该预留终态的首次真实触发路径，D3）。
            lock (_sync)
            {
                _faulted = true;
                Monitor.Pulse(_sync);
            }
            Diagnose($"writer-fault:{e.GetType().Name}");
        }
        finally
        {
            try
            {
                stream?.Dispose();
            }
            catch (Exception)
            {
                // 关停路径自身 no-throw（诊断已记录故障原因）
            }
            lock (_sync)
            {
                _consumerExited = true;
                Monitor.Pulse(_sync);
            }
        }
    }

    private System.IO.StreamWriter CreateStream()
    {
        // 目录惰性创建（capture path 永不做 IO；IO 故障 → Faulted 隔离）
        System.IO.Directory.CreateDirectory(_options.PersistenceDirectory);
        var path = System.IO.Path.Combine(
            _options.PersistenceDirectory, SealedTraceStore.Sanitize(_runId) + ".trace.log");
        var fileStream = new System.IO.FileStream(
            path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
        return new System.IO.StreamWriter(fileStream, leaveOpen: false);
    }

    private static string RenderLine(TraceRecord record) => record switch
    {
        SpanStartRecord s => string.Join('\t',
            "start", s.SpanId, s.ParentSpanId ?? "-", s.OperationId,
            s.CaptureSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderReferences(s.References)),
        SpanEventRecord e => string.Join('\t',
            "event", e.SpanId, e.EventId, e.ReasonCode ?? "-", RenderReferences(e.References)),
        SpanCompleteRecord c => string.Join('\t', "complete", c.SpanId, c.Outcome.ToString()),
        EmissionMarkerRecord => "emission",
        _ => "unknown",
    };

    private static string RenderReferences(TraceReference[] references) =>
        references.Length == 0
            ? "-"
            : string.Join(",", references.Select(r => r.Kind + ":" + r.Value));

    // ---- drain / seal / persist（IRunTraceSink.Finalize；幂等）----

    /// <summary>drain 显式结果（review 修法 1）：Completed / Timeout / Faulted。</summary>
    private enum DrainResult
    {
        Completed,
        Timeout,
        Faulted,
    }

    public RunTraceArtifact Finalize()
    {
        lock (_captureLock)
        {
            if (_finalized is not null)
                return _finalized;

            // review 修法 1：先关入口、再有界等待 consumer 排空并退出；
            // drain 结果参与终态裁决（Timeout ⇒ Quarantined）。
            var drainResult = Drain();
            var artifact = Assemble(drainResult);

            // review 修法 3：PersistSealed 失败必须被吸收——返回内存
            // CaptureFailed 诊断产物，磁盘不留可导入 seal 对，绝不抛出。
            try
            {
                PersistSealed(artifact);
            }
            catch (Exception e)
            {
                DiagnoseCritical($"seal-persist-failed:{e.GetType().Name}");
                var rebuilt = artifact with
                {
                    RecorderTerminal = RecorderTerminal.CaptureFailed,
                    RecorderDiagnostics = artifact.RecorderDiagnostics
                        .Append(new TraceDiagnostic($"seal-persist-failed:{e.GetType().Name}"))
                        .ToImmutableArray(),
                };
                artifact = rebuilt with { IntegritySha256 = SealedTraceStore.CanonicalRendering(rebuilt) };
            }

            _finalized = artifact;
            return artifact;
        }
    }

    /// <summary>
    /// bounded drain（review 修法 1）：先 <c>TryComplete</c> 关闭队列入口，
    /// 再有界等待 consumer 完成全部 flush 并退出（成功路径不 cancel——
    /// consumer 自然排空退出；Faulted 立即返回）。仅超时才作恢复性 cancel，
    /// 并记录 drain-timeout 诊断与已读出未刷盘 record 的 accounting 诊断。
    /// 测试不得 sleep-race（D7）。
    /// </summary>
    private DrainResult Drain()
    {
        _channel.Writer.TryComplete();
        var timeout = _options.DrainTimeout ?? TimeSpan.FromSeconds(10);
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        lock (_sync)
        {
            while (!_consumerExited && !_faulted)
            {
                var remaining = deadline - Environment.TickCount64;
                if (remaining <= 0)
                {
                    // 恢复路径：超时才 cancel；被测试闸门卡住的线程不受
                    // 影响，闸门释放后自行收尾（不产生故障）。
                    _consumerCts.Cancel();
                    DiagnoseCritical("drain-timeout");
                    DiagnoseCritical($"drain-timeout-unflushed:{_pendingUnflushed}");
                    return DrainResult.Timeout;
                }
                Monitor.Wait(_sync, (int)Math.Min(remaining, 250));
            }
            return _faulted ? DrainResult.Faulted : DrainResult.Completed;
        }
    }

    /// <summary>从 journal 组装 artifact：逐条镜像 InMemoryRunTrace.Finalize 的 span/event 形状与排序。</summary>
    private RunTraceArtifact Assemble(DrainResult drainResult)
    {
        List<TraceRecord> journal;
        bool faulted;
        lock (_sync)
        {
            journal = _journal.ToList();
            faulted = _faulted;
        }

        var emissionObserved = journal.Any(r => r is EmissionMarkerRecord);
        var builders = new Dictionary<string, SpanBuilder>(StringComparer.Ordinal);
        var ordered = new List<SpanBuilder>();
        foreach (var record in journal)
        {
            switch (record)
            {
                case SpanStartRecord start:
                    var builder = new SpanBuilder(
                        start.SpanId, start.ParentSpanId, start.OperationId, start.CaptureSequence);
                    builder.References.AddRange(start.References);
                    builders[start.SpanId] = builder;
                    ordered.Add(builder);
                    break;
                case SpanEventRecord journalEvent:
                    if (builders.TryGetValue(journalEvent.SpanId, out var target))
                        target.Events.Add(new TraceEvent(
                            journalEvent.EventId,
                            journalEvent.References.ToImmutableArray(),
                            journalEvent.ReasonCode));
                    else
                        Diagnose($"journal-orphan-event:{journalEvent.SpanId}");
                    break;
                case SpanCompleteRecord complete:
                    if (builders.TryGetValue(complete.SpanId, out var completing))
                    {
                        completing.Completed = true;
                        completing.Outcome = complete.Outcome;
                    }
                    else
                        Diagnose($"journal-orphan-complete:{complete.SpanId}");
                    break;
            }
        }

        // RecorderTerminal（review 修法 1 收紧）：
        // Finalized ⟺ drain 完成 ∧ 无故障 ∧ DroppedCount==0 ∧ 已观测 emission；
        // writer/persistence 故障 → CaptureFailed；drain 超时 / 丢弃 / 无
        // emission → Quarantined（各带具名诊断）。
        if (!emissionObserved)
            DiagnoseCritical("runtime-outcome-emission-not-observed");
        var dropped = Interlocked.Read(ref _droppedCount);
        if (dropped > 0)
            DiagnoseCritical($"dropped-records:{dropped}");
        var terminal = faulted || drainResult == DrainResult.Faulted
            ? RecorderTerminal.CaptureFailed
            : drainResult == DrainResult.Timeout || dropped > 0 || !emissionObserved
                ? RecorderTerminal.Quarantined
                : RecorderTerminal.Finalized;

        var spans = ordered.Select(b => new TraceSpan(
            b.SpanId,
            b.ParentSpanId,
            b.OperationId,
            b.Completed ? b.Outcome : StructuralOutcome.Incomplete,
            b.References.ToImmutableArray(),
            b.Events.ToImmutableArray(),
            b.CaptureSequence)).ToImmutableArray();

        // review 修法 3：诊断有界——样本 ≤ SampleCap；发生截断时以汇总
        // 条目（diagnostics-capped:total=N）+ finalize 关键诊断组成终集，
        // 总条数 ≤ DiagnosticCap。
        TraceDiagnostic[] diagnostics;
        lock (_diagnosticsLock)
        {
            diagnostics = _cappedDiagnostics > 0
                ? _diagnostics.Take(SampleCap)
                    .Append(new TraceDiagnostic(
                        $"diagnostics-capped:total={_diagnostics.Count + _cappedDiagnostics}"))
                    .Concat(_finalizeDiagnostics)
                    .ToArray()
                : _diagnostics.Concat(_finalizeDiagnostics).ToArray();
        }

        var artifact = new RunTraceArtifact(
            SchemaVersion, _runId, _traceId,
            RootSpanId: ordered.Count > 0 ? ordered[0].SpanId : null,
            terminal,
            spans,
            diagnostics.ToImmutableArray());
        return artifact with
        {
            IntegritySha256 = SealedTraceStore.CanonicalRendering(artifact),
        };
    }

    /// <summary>
    /// seal 持久化（review 修法 3）：两文件先写 TEMP 名，再以 File.Move
    /// 原子发布为 {name}.trace.json + {name}.trace.sha256；任一步失败
    /// 删除 temps 与任何已部分发布的文件并重抛（Finalize 吸收为
    /// CaptureFailed）——失败后磁盘不得残留可导入 seal 对。
    /// </summary>
    private void PersistSealed(RunTraceArtifact artifact)
    {
        // seal 时完整性成立（D4）：canonical rendering SHA-256 随 artifact 持久化。
        System.IO.Directory.CreateDirectory(_options.PersistenceDirectory);
        var name = SealedTraceStore.Sanitize(_runId);
        var jsonPath = System.IO.Path.Combine(_options.PersistenceDirectory, name + ".trace.json");
        var hashPath = System.IO.Path.Combine(_options.PersistenceDirectory, name + ".trace.sha256");
        var tmpJson = jsonPath + ".tmp";
        var tmpHash = hashPath + ".tmp";
        try
        {
            System.IO.File.WriteAllText(tmpJson, System.Text.Json.JsonSerializer.Serialize(artifact));
            System.IO.File.WriteAllText(tmpHash, artifact.IntegritySha256);
            if (_options.SealPersistFault?.Invoke(name) is { } injected)
                throw injected;
            System.IO.File.Move(tmpJson, jsonPath, overwrite: true);
            System.IO.File.Move(tmpHash, hashPath, overwrite: true);
        }
        catch
        {
            TryDelete(tmpJson);
            TryDelete(tmpHash);
            TryDelete(jsonPath);
            TryDelete(hashPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch (Exception)
        {
            // 清理路径自身 no-throw（失败已由调用方诊断吸收）
        }
    }

    private sealed class CaptureSpanState
    {
        public CaptureSpanState(SpanDefinition definition, string spanId)
        {
            Definition = definition;
            SpanId = spanId;
        }

        public SpanDefinition Definition { get; }
        public string SpanId { get; }
        public bool Completed { get; set; }
    }

    private sealed class SpanBuilder
    {
        public SpanBuilder(string spanId, string? parentSpanId, string operationId, int captureSequence)
        {
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            OperationId = operationId;
            CaptureSequence = captureSequence;
        }

        public string SpanId { get; }
        public string? ParentSpanId { get; }
        public string OperationId { get; }
        public int CaptureSequence { get; }
        public List<TraceReference> References { get; } = new();
        public List<TraceEvent> Events { get; } = new();
        public bool Completed { get; set; }
        public StructuralOutcome Outcome { get; set; } = StructuralOutcome.Incomplete;
    }

    /// <summary>scope 语义镜像 InMemoryRunTrace.OperationScope：校验同步、落点改为 enqueue。</summary>
    private sealed class AsyncOperationScope : ITraceOperationScope
    {
        private readonly AsyncFileTraceWriter _owner;
        private readonly CaptureSpanState _state;

        public AsyncOperationScope(
            AsyncFileTraceWriter owner, CaptureSpanState state, string traceId, string spanId)
        {
            _owner = owner;
            _state = state;
            Context = new TraceContext(traceId, spanId);
        }

        public TraceContext Context { get; }

        public void Record(
            TraceEventDefinition eventDefinition, IReadOnlyList<TraceReference> references, string? reasonCode = null) =>
            _owner.Record(_state, eventDefinition, references, reasonCode);

        public void Complete(StructuralOutcome outcome) => _owner.Complete(_state, outcome);

        public void Dispose()
        {
            // 未显式 Complete 的 span 在 Finalize 时如实标 Incomplete（与
            // InMemoryRunTrace 相同；此处无额外动作）。
        }
    }

    private abstract record TraceRecord;

    private sealed record SpanStartRecord(
        string SpanId, string? ParentSpanId, string OperationId,
        int CaptureSequence, TraceReference[] References) : TraceRecord;

    private sealed record SpanEventRecord(
        string SpanId, string EventId, TraceReference[] References, string? ReasonCode) : TraceRecord;

    private sealed record SpanCompleteRecord(string SpanId, StructuralOutcome Outcome) : TraceRecord;

    private sealed record EmissionMarkerRecord : TraceRecord;
}

/// <summary>
/// TRW-001 组合入口：caller 侧显式选择异步 writer（与 RunTraceFactory 并列；
/// 公共 RunTraceFactory 零改动）。internal realization，非公共契约（TRW-001 D1）。
/// </summary>
internal static class AsyncTraceFactory
{
    internal static RunTraceScope BeginAsyncRun(RunCorrelation correlation, AsyncTraceWriterOptions options) =>
        new(new AsyncFileTraceWriter(correlation, options));
}
