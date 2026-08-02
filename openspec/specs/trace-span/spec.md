# trace-span Specification

## Purpose
TBD - created by archiving change trace-span-observability. Update Purpose after archive.
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

### Requirement: spanType 字符串目录

Every emitted `spanType` SHALL be a member of a static string catalog (`SpanTypes`), using the dotted namespace: `engine.run`/`engine.step`/`entry.generate`/`entry.observed`/`entry.ignored`/`entry.visited`/`entry.skipped`/`entry.action`/`action.click`/`action.scroll`/`action.back`/`action.launch`/`action.wait`/`ai.call`/`ai.analyze`/`analyze.completion`/`analyze.error_loop`/`analyze.tree`. The `SpanType` enum SHALL NOT be extended (constitution-locked C-11). The catalog SHALL expose each spanType as a constant string used by both instrumentation and queries.

#### Scenario: 运行中发出的每个 spanType 都在目录内

- **WHEN** a full mock run (engine + entry + action + analyze spans) is executed and every recorded span's `SpanType` is checked against the `SpanTypes` catalog
- **THEN** each emitted `SpanType` SHALL be present in the catalog, and no span SHALL carry an out-of-catalog `SpanType`

#### Scenario: 目录常量即查询参数

- **WHEN** `GetSpansByType` is called with the catalog constant `SpanTypes.EntryObserved`
- **THEN** the query SHALL return the same result as calling it with the literal string `"entry.observed"`, and the `SpanType` enum SHALL still have exactly 11 values

