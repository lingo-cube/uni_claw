## Requirements

### Requirement: Unified logging exit with trace/span correlation

All run-path diagnostics SHALL flow through a standard `ILogger` abstraction with a unified line format carrying the run trace id and current span id. Every log line SHALL match `[HH:mm:ss.fff] [t={TraceId}] [s={SpanId}] [LVL] {Category}: {message}` where `TraceId` is the run id (`RunTraceContext`), `SpanId` is the innermost open span (`EngineStepSpanContext` stack top), and both default to `-` when no context exists. Log level gating SHALL follow the `LogLevel` hierarchy with minimum level from `UNICLAW_LOG_LEVEL` (default `information`).

#### Scenario: Engine step log carries run and step ids
- **WHEN** a log call is made inside an engine step span during a run
- **THEN** the emitted line contains `[t={runId}]` matching the run and `[s={spanId}]` matching that step's span id

#### Scenario: Log outside any run context uses placeholders
- **WHEN** a log call is made with no run or span context (CLI path, unit test)
- **THEN** the line contains `[t=-] [s=-]`

#### Scenario: Level below minimum is suppressed
- **WHEN** `UNICLAW_LOG_LEVEL=information` and a Debug-level call is made
- **THEN** the line is not emitted

### Requirement: Every span region exposes its span id to logging (full span coverage)

The current span id SHALL be available to logging anywhere inside any open span region — engine steps, `handle_error`, AI calls, and all other `SpanType` spans — without per-site plumbing. The span context synchronization point SHALL be the span lifecycle scope: opening a span pushes its id onto the async-flow stack, closing pops it, and nesting restores the outer span. Point-in-time event records SHALL NOT enter the stack. The source generator emitting `[TraceHandler]` wrappers SHALL remain unchanged.

#### Scenario: Error handling span is visible to inner log calls
- **WHEN** a log call is made inside an `ErrorHandler.HandleError` invocation (a `handle_error` span)
- **THEN** the line's `[s=...]` equals that error-handling span's id

#### Scenario: Nested spans restore the outer span
- **WHEN** a nested span closes inside an outer span
- **THEN** subsequent log calls inside the outer span again show the outer span's id

### Requirement: State machine and engine exceptions are logged

State machine exception paths SHALL emit log records in addition to trace events: the FSM step dispatch SHALL log routed exceptions (type + message) at Warning/Error; `ErrorHandler.HandleError` SHALL log its classification outcome (error type, strategy, retry count) at Information and its pipeline-level fallback (unhandled exception during error handling) with the full exception at Error. The traversal engine SHALL log step open/close at Debug level. Host SHALL log run start (runId, mode, provider), run end (runId, status, duration), asset submission failure (relative path + exception, at the same site as the `asset_write_failed` issue), and the final run state at Information/Error.

#### Scenario: FSM exception is visible on stderr with ids
- **WHEN** a step throws and the FSM routes to ErrorHandling
- **THEN** stderr shows the exception type/message with `[t={runId}] [s={step span id}]`

#### Scenario: Error handler fallback surfaces the full exception
- **WHEN** error classification itself throws inside `HandleError`
- **THEN** an Error record is emitted with the complete exception and the `handle_error` span id

### Requirement: Logs persist to the run directory and are discoverable via config

Logs SHALL persist to `trace/{runId}/run.log` in the V2 layout (event-stream side, same directory as `trace.jsonl`; stream-append text diagnostics, NOT pipeline assets — written directly by the file logger, not through `ITracePipeline`/`FileAssetStore`). The file SHALL be created at run start, flushed and closed in a `finally` block (exception paths included), and isolated per run id. The log location SHALL be discoverable through configuration: `RunResult` SHALL gain a `RunLogPath` field written to `result.json` as a relative path (`"runLogPath": "trace/{runId}/run.log"`, mirroring the `TracePath` precedent), the layout model SHALL expose a run.log path resolution helper, and readers SHALL fall back to the default path when the field is absent (a V1 run resolves to "no log"). Schema version SHALL NOT bump for this field-level extension.

#### Scenario: Analyzer finds the log from run metadata
- **WHEN** an analyzer reads a V2 run's `result.json`
- **THEN** it finds `runLogPath: "trace/{runId}/run.log"` and resolves the full path via the layout helper without composing path strings

#### Scenario: Old run without the field resolves to the default
- **WHEN** a run's `result.json` lacks `runLogPath`
- **THEN** the reader falls back to `trace/{runId}/run.log` and reports "no log" when that file does not exist

#### Scenario: Log file survives abrupt termination
- **WHEN** a run is interrupted mid-way
- **THEN** the run.log file handle is closed and already-written lines remain readable

### Requirement: Logging level is configurable per assembly surface

The minimum log level SHALL be configurable: `UNICLAW_LOG_LEVEL` for the Host run (named independently of the vision/run-mode variable family), and an optional `logging.level` section in `integration.config.json` for test runs (injected into the environment at test assembly time, same pattern as the vision-server env injection; unknown level values fail fast in the loader). File persistence SHALL have no switch — it is a fixed layout contract.

#### Scenario: Test config raises the level
- **WHEN** `integration.config.json` sets `logging.level: "warning"`
- **THEN** test runs emit Warning+ only and step-level Debug lines are suppressed
