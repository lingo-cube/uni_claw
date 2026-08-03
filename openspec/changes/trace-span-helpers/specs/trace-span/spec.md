## ADDED Requirements

### Requirement: Span 记录方式为可复用助手（TraceSpanScope / RecordEventAsync）

Spans SHALL be recorded through the reusable helpers provided by the `trace-span-helpers` capability: the `TraceSpanScope` async-disposable region scope (via the `ITraceRecorder.BeginSpanAsync` extension) for spans whose attributes are computed inside the region, whose spanType is selected at runtime from the catalog, or whose termination is conditional; and the `RecordEventAsync` event-marker helper for point-in-time markers (unpaired spans). The Phase 1 hand-written `StartSpanAsync`/`EndSpanAsync` scaffolding in business code SHALL be replaced by these helpers. This requirement supplements the existing `Phase 1 引擎埋点` requirement — the emission points and span parentage SHALL remain exactly as specified there; only the recording mechanism changes.

#### Scenario: 引擎埋点经助手完成

- **WHEN** a full mock run is executed after migration and every span from the `Phase 1 引擎埋点` requirement (`engine.run`/`engine.step`/`entry.generate`/`entry.observed`/`entry.ignored`/`entry.visited`/`entry.skipped`) is inspected
- **THEN** each span SHALL be emitted via a `TraceSpanScope` or a `RecordEventAsync` call, and SHALL carry the same spanType, parent linkage, attributes, and timing as before migration

#### Scenario: 目录成员资格仍成立

- **WHEN** a full mock run is executed and every recorded span's `SpanType` is checked against the `SpanTypes` catalog
- **THEN** each emitted `SpanType` SHALL be present in the catalog, and no span SHALL carry an out-of-catalog `SpanType`

### Requirement: TraceSpanScope 可复用 span 作用域

`ITraceRecorder` SHALL gain an additive extension `BeginSpanAsync(spanType, spanName?, parentSpanId?, attributes?, ct)` returning an async-disposable `TraceSpanScope`, declared on a nullable receiver so a missing recorder yields a side-effect-free no-op scope (no exception, no span). Disposing the scope SHALL end the span with status `"ok"` (or an explicit `scope.End(status, attributes)` call when the business code must set a custom status or merge final attributes). The scope SHALL be the recording mechanism for spans that cannot be expressed as whole-method annotations (regions whose end-attributes are computed from method-local variables, spans crossing awaited helper calls, multi-branch terminal closes, runtime-selected spanTypes). Closing via the scope SHALL have the same no-op-on-unknown-spanId semantics as `EndSpanAsync`.

#### Scenario: 作用域包住业务代码自动结束

- **WHEN** business code opens a scope with `await using var scope = await recorder.BeginSpanAsync(spanType, ...)` and the code inside completes
- **THEN** the span SHALL have non-null `EndTime` and status `"ok"` without an explicit end call

#### Scenario: 显式 status 与最终属性

- **WHEN** business code calls `scope.End("error", new Dictionary<string, object> { ["reason"] = "..." })`
- **THEN** the span SHALL record status `"error"` and the merged final attributes, and a second end (dispose after explicit end) SHALL be a no-op

#### Scenario: 运行时 spanType 与 deny-gate 顺序保持

- **WHEN** a scope is opened with a spanType selected at runtime from the `SpanTypes` catalog (e.g. `ActionToSpanType(action)` or a `verdict.Reason` ternary), or when a denied action passes the safety gate before any scope opens
- **THEN** the recorded span SHALL carry the runtime-selected catalog spanType, and a denied run SHALL record no span for that action

#### Scenario: 无 recorder 时零副作用

- **WHEN** a scope is opened on a composition where no `ITraceRecorder` is injected
- **THEN** the scope SHALL be a no-op: the surrounded code executes identically and no span SHALL be recorded

### Requirement: RecordEventAsync 原子事件标记

`ITraceRecorder` SHALL gain an additive extension `RecordEventAsync(spanType, parentSpanId?, attributes?, ct)` that records a point-in-time event span with `EndTime` left null (`DurationMs == 0`). It SHALL be the recording mechanism for the Phase 1 unpaired marker spans (`entry.observed`/`entry.ignored`/`entry.visited`/`entry.skipped`/`ai.analyze`), replacing hand-written fire-and-forget `StartSpanAsync` calls. `parentSpanId` SHALL accept any runtime expression (including method-call lookups). The helper SHALL be a no-op when no recorder is attached, and the emitted spanType SHALL be a `SpanTypes` catalog member.

#### Scenario: 事件 span 无 EndTime

- **WHEN** `RecordEventAsync("entry.visited", parentSpanId, attributes)` is called and the span is read via `ITraceQuery.GetSpan`
- **THEN** the span SHALL have non-null `StartTime`, null `EndTime`, `DurationMs == 0`, and the recorded attributes

#### Scenario: 无 recorder 时零副作用

- **WHEN** `RecordEventAsync` is called on a composition without an `ITraceRecorder`
- **THEN** no exception SHALL be thrown and no span SHALL be recorded

### Requirement: 业务代码无手动 span 脚手架

After migration, hand-written `StartSpanAsync`/`EndSpanAsync` scaffolding SHALL NOT remain in business code paths that are covered by an equivalent `TraceSpanScope` or `RecordEventAsync` call. Allowed residual call sites: the two extension helpers themselves (`ITraceRecorderExtensions`), the `TraversalEngine` sync passthroughs (the engine's recording seam), and `ITraceRecorder`/`InMemoryTraceRecorder` implementations themselves. Existing span-tree, handler-trace-writer, safety-gate, page-analyzer, and engine tests SHALL pass unchanged after migration.

#### Scenario: 迁移后脚手架清零

- **WHEN** production `src/` is searched for direct `StartSpanAsync`/`EndSpanAsync` invocations
- **THEN** every hit SHALL be inside the extension helpers, the `TraversalEngine` passthroughs, or the recorder implementation — and no migrated method SHALL contain hand-written pairs alongside its scope or event call

#### Scenario: 既有 span 测试保持通过

- **WHEN** the full test suite runs after migration
- **THEN** `TraceSpanTests`, `TraceSpanTreeTests`, `HandlerTraceWriterTests`, `SafetyGateTests`, `PageAnalyzerTests`, and the `TraversalEngine` test files SHALL pass without modification
