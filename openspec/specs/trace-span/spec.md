# trace-span Specification

## Purpose
Span tree recording for engine, entry, action, and analysis observability. Defines the `TraceSpan` record model and JSONL persistence, the `ITraceRecorder`/`ITraceQuery` interfaces, the Phase 1 engine instrumentation points and `SpanTypes` catalog, and (from change trace-span-helpers) the reusable recording helpers — `TraceSpanScope` region scopes and `RecordEventAsync` event markers — that replace hand-written span scaffolding in business code. From change trace-parent-linkage: `ai.call`/`ai.analyze` parentage to the current `engine.step` through an injected `ITraceContextProvider`, the `TraceFields` attribute-key catalog (frozen dotted `layer.field` names), and `TraceLevel`-gated field granularity via per-spanType `SpanFieldProfile` descriptors.
## Requirements
### Requirement: TraceSpan record 与 span JSONL record_type

`TraceSpan` SHALL be an immutable record carrying `SpanId` (string), `ParentSpanId` (string?, null for root spans), `SpanType` (string, dotted `layer.event` namespace), `SpanName` (string), `StartTime` (DateTimeOffset/UtcTicks), `EndTime` (DateTimeOffset?), `Status` (string, one of `"ok"`/`"error"`/`"deny"`/`"skip"`), `TraceContext?`, and `Attributes?` (`Dictionary<string, object>`). `DurationMs` SHALL be computed as `EndTime - StartTime`, and SHALL be 0 when `EndTime` is null. Each span SHALL be persisted to the trace JSONL as a `"span"` `record_type` record, parallel to the existing `execution`/`state_transition`/`page_transition`/`ai_call` record types. The existing `SpanType` enum SHALL NOT be extended; `TraceSpan.SpanType` is a string distinct from the enum.

#### Scenario: 序列化/反序列化 round-trip

- **WHEN** a `TraceSpan` with a populated `SpanId`, `ParentSpanId`, `SpanType`, `SpanName`, `StartTime`, `EndTime`, `Status`, and `Attributes` is serialized to JSONL as `record_type:"span"` and then deserialized back
- **THEN** every field SHALL round-trip losslessly, including nested `Attributes` dictionary values and the `"span"` record_type discriminator

#### Scenario: DurationMs 在 EndTime 为 null 时为 0

- **WHEN** a `TraceSpan` is created with `EndTime` null (still open) and its `DurationMs` is read
- **THEN** `DurationMs` SHALL be 0, and no exception SHALL be thrown

#### Scenario: Status 取值受限

- **WHEN** a `TraceSpan` is constructed or updated with a `Status` value other than `"ok"`/`"error"`/`"deny"`/`"skip"`
- **THEN** construction or validation SHALL reject the value with a `DomainValidationException`

### Requirement: ITraceRecorder.StartSpan/EndSpan

`ITraceRecorder` SHALL add `StartSpan(spanType, spanName, parentSpanId?, attributes?, ct)` returning a `string spanId` and opening a span (recording `StartTime`), and `EndSpan(spanId, status = "ok", attributes?, ct)` SHALL close that span (writing `EndTime` and merging the final attributes into the span). Both SHALL be additive to the existing `ITraceRecorder` surface; the existing `ITraceCoordinator.PushSpan`/`PopSpan` engine-internal stack SHALL remain unchanged and SHALL NOT be merged with `StartSpan`/`EndSpan`. Closing an already-closed or unknown spanId SHALL be a no-op or a recorded error, never a crash of the calling engine.

#### Scenario: StartSpan 返回非空 id 且开一个 span

- **WHEN** `StartSpan("engine.step", "step 1", ct)` is called and immediately followed by a query of all spans
- **THEN** the returned `spanId` SHALL be non-empty, and a span with that `spanId`, `SpanType == "engine.step"`, non-null `StartTime`, and null `EndTime` SHALL be readable via `ITraceQuery`

#### Scenario: EndSpan 后 span 可读且 EndTime 非空

- **WHEN** a span opened by `StartSpan` is closed via `EndSpan(spanId, status: "ok", attributes: {...})` and then read via `ITraceQuery.GetSpan(spanId)`
- **THEN** the span SHALL have non-null `EndTime`, `Status == "ok"`, and the merged final `Attributes` including the attributes passed at `EndSpan`

#### Scenario: 重复 EndSpan 不崩溃

- **WHEN** `EndSpan` is called twice on the same `spanId` (or on a spanId that was never started)
- **THEN** the second call SHALL not throw and SHALL not alter the already-recorded span

### Requirement: ITraceQuery 读接口

`ITraceQuery` SHALL inherit `ITraceService` and SHALL add `GetRootSpan()` (returns the span whose `ParentSpanId` is null, or null when none), `GetSpansByType(string spanType)`, `GetChildSpans(parentSpanId)`, `GetSpan(spanId)`, and `GetAllSpans()`. `InMemoryTraceService` SHALL implement `ITraceQuery` over the same `InMemoryTraceStorage`. The existing 12 `ITraceService` methods SHALL be unchanged. A span whose `ParentSpanId` references a non-existent span SHALL still be returned by `GetAllSpans`/`GetSpansByType`, only parent-child queries SHALL be affected by the missing parent.

#### Scenario: 写入父子 span 后 GetChildSpans 返回子

- **WHEN** a root span and a child span referencing the root's `SpanId` are recorded, and `GetChildSpans(root.SpanId)` is called
- **THEN** the result SHALL contain exactly the child span and nothing else

#### Scenario: GetRootSpan 返回 parentSpanId 为 null 的根

- **WHEN** two spans are recorded — one with null `ParentSpanId` and one with a non-null `ParentSpanId` — and `GetRootSpan()` is called
- **THEN** the result SHALL be the span with null `ParentSpanId`

#### Scenario: 现有多层 span 树可查询

- **WHEN** a three-level span tree (`engine.run` → `engine.step` → `entry.visited`) is recorded and queried via `GetAllSpans` and `GetSpansByType("entry.visited")`
- **THEN** `GetAllSpans` SHALL return all three spans, and `GetSpansByType` SHALL return only the `entry.visited` span

### Requirement: 并行存储共存

`InMemoryTraceStorage` SHALL add a `_spans` list (`List<TraceSpan>`) parallel to the existing `_executions`/`_transitions`/`_pageTransitions`/`_aiCalls`/`_errors` lists, with span CRUD writing only into `_spans`. Existing consumers of the other lists SHALL be unaffected by span writes. `FileTraceStorage` SHALL write and read the `"span"` `record_type` in the trace JSONL alongside the existing record types.

#### Scenario: 写 span 后现有 GetExecutions 返回值不变

- **WHEN** an existing run has recorded executions and then a new span is written via `StartSpan`/`EndSpan`
- **THEN** `GetExecutions()` SHALL return exactly the same executions as before the span write, with no span data leaking into the execution records

#### Scenario: GetAllSpans 返回新写入的 span

- **WHEN** a span is recorded and then `GetAllSpans()` is called
- **THEN** the returned collection SHALL include the newly recorded span, and SHALL NOT include any `ExecutionRecord`/execution data

#### Scenario: FileTraceStorage 读写 span record_type

- **WHEN** a trace containing both executions and spans is written by `FileTraceStorage` and then re-read
- **THEN** the written file SHALL contain `"span"`-typed lines for each span, and the re-read spans SHALL equal the originally recorded spans

### Requirement: Phase 1 引擎埋点

Phase 1 instrumentation SHALL emit spans at the following points: `TraversalEngine.RunAsync` SHALL write `engine.run` (root, one per run) and one `engine.step` per step with `parentSpanId == engine.run` span id; `DynamicChildManager.Generate` SHALL (once its `_trace` wiring is fixed) write `entry.generate` and one `entry.observed` per matched result and one `entry.ignored` per dedup hit; `InterceptionHandler.OnBranch` SHALL write `entry.visited` (parent = the current `engine.step`); `SafeActionExecutor` deny branch SHALL write `entry.skipped` (parent = the corresponding `entry.visited`). These SHALL be written via `ITraceRecorder.StartSpan`/`EndSpan`, coexisting with any existing `ExecutionRecord` output.

#### Scenario: mock run 后 observed 数量等于匹配条目数

- **WHEN** a mock run with a known set of matched entries is executed and `GetSpansByType("entry.observed")` is queried afterwards
- **THEN** the count SHALL equal the number of matched entries in the run, and each `entry.observed` span SHALL have `parentSpanId` referencing an `entry.generate` span

#### Scenario: entry.visited 是 entry.skipped 的父

- **WHEN** a step whose branch action is denied is recorded, emitting both `entry.visited` and `entry.skipped`
- **THEN** `GetSpan(entrySkipped.ParentSpanId)` SHALL be the `entry.visited` span, and `GetChildSpans(entryVisited.SpanId)` SHALL include the `entry.skipped` span

#### Scenario: engine.run 是 engine.step 的根父

- **WHEN** a run with multiple steps completes and the span tree is queried
- **THEN** every `engine.step` span SHALL have `parentSpanId == engine.run.SpanId`, and `GetRootSpan()` SHALL return the `engine.run` span

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

### Requirement: spanType 字符串目录

Every emitted `spanType` SHALL be a member of a static string catalog (`SpanTypes`), using the dotted namespace: `engine.run`/`engine.step`/`entry.generate`/`entry.observed`/`entry.ignored`/`entry.visited`/`entry.skipped`/`entry.action`/`action.click`/`action.scroll`/`action.back`/`action.launch`/`action.wait`/`ai.call`/`ai.analyze`/`analyze.completion`/`analyze.error_loop`/`analyze.tree`. The `SpanType` enum SHALL NOT be extended (constitution-locked C-11). The catalog SHALL expose each spanType as a constant string used by both instrumentation and queries.

#### Scenario: 运行中发出的每个 spanType 都在目录内

- **WHEN** a full mock run (engine + entry + action + analyze spans) is executed and every recorded span's `SpanType` is checked against the `SpanTypes` catalog
- **THEN** each emitted `SpanType` SHALL be present in the catalog, and no span SHALL carry an out-of-catalog `SpanType`

#### Scenario: 目录常量即查询参数

- **WHEN** `GetSpansByType` is called with the catalog constant `SpanTypes.EntryObserved`
- **THEN** the query SHALL return the same result as calling it with the literal string `"entry.observed"`, and the `SpanType` enum SHALL still have exactly 11 values

### Requirement: ai.call/ai.analyze 父链归属

`ai.call` SHALL record `parentSpanId` as the current innermost `engine.step` span id when the engine step context is available to the PageAnalyzer. The parent SHALL be resolved at runtime through an injected trace-context provider (`ITraceContextProvider.CurrentSpanId`), so the 4 existing `AnalyzeCurrentPageAsync` call sites need no signature change. When no engine step context is available (non-engine entry points, or no provider injected), `ai.call` SHALL be recorded as a root span — orphan spans SHALL be preserved, not suppressed. `ai.analyze` SHALL keep `parentSpanId == ai.call` as specified by the event-marker requirement. The parentage SHALL NOT change the recorded spanType, attributes, status, or timing of `ai.call`/`ai.analyze`.

#### Scenario: 引擎入口 ai.call 挂在 engine.step 下

- **WHEN** a mock run executes an engine step that calls `AnalyzeCurrentPageAsync` with the trace-context provider wired, and the recorded spans are queried
- **THEN** `ai.call` SHALL have `parentSpanId` equal to the current `engine.step` span id, `ai.analyze` SHALL have `parentSpanId` equal to the `ai.call` span id, and the chain `engine.run → engine.step → ai.call → ai.analyze` SHALL be queryable via `GetChildSpans`

#### Scenario: 非引擎入口保留孤儿根

- **WHEN** `AnalyzeCurrentPageAsync` is called outside any engine step context (no provider, or `CurrentSpanId` is null)
- **THEN** `ai.call` SHALL still be recorded with `parentSpanId` null (root), and `ai.analyze` SHALL still have `parentSpanId == ai.call`

### Requirement: span 属性字段目录（TraceFields）

All span attribute keys SHALL be members of a static string catalog (`TraceFields`), using the existing dotted `layer.field` naming (`ai.provider_id`, `action.adb_ms`, `entry.name`, `analyze.observed`, `error.reason`, etc.). The catalog SHALL contain every key emitted by the Phase 1 spans, and the constant values SHALL be frozen — they are the persisted JSONL attribute names and SHALL NOT change. Business code SHALL reference the catalog constants instead of string literals. The catalog SHALL be verified by a completeness test (every emitted key present, keys non-empty, `layer.` namespaced). This catalog is the future validation input for the deferred `[TraceSpan]` source generator.

#### Scenario: 目录含全部发射键且值冻结

- **WHEN** every span recorded by a full mock run (engine + entry + action + ai + analyze spans) has its attribute keys checked against the `TraceFields` catalog
- **THEN** every key SHALL be present in the catalog, no key SHALL be an out-of-catalog literal, and the catalog values SHALL equal the keys recorded in the span JSONL

### Requirement: span 字段按 TraceLevel 分级

Each spanType SHALL have a field-granularity profile (`SpanFieldProfile`) splitting its attribute keys into core fields (recorded at every `TraceLevel` at or above Basic) and extended fields (recorded only at Detailed/Full — latency, token counts, timings, node ids, and similar detail). Recording SHALL filter extended fields by the active `TraceLevel` (`None`/`Basic`/`Detailed`/`Full`, unchanged enum). The default level SHALL produce the same full attribute set as before this requirement (backward-compatible — profiles SHALL be applied additively by the recording helpers, without adding scaffolding to business code). Core-vs-extended membership per key SHALL be fixed in the profiles and verified by tests.

#### Scenario: 缺省级别与全量记录一致

- **WHEN** a mock run records spans at the default level
- **THEN** every recorded span SHALL carry the same full attribute set as before this requirement (no key dropped)

#### Scenario: Basic 级别裁剪扩展字段

- **WHEN** the same mock run is recorded with `TraceLevel.Basic`
- **THEN** each span SHALL carry its core fields and SHALL NOT carry extended fields (e.g. `ai.tokens`/`ai.latency_ms`/`action.adb_ms`/`analyze.p50`), while core fields (e.g. `ai.success`, `action.result`, `entry.name`, `analyze.observed`) SHALL remain present

