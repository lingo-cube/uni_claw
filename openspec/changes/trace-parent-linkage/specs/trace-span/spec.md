## ADDED Requirements

### Requirement: ai.call/ai.analyze 父链归属

`ai.call` SHALL record `parentSpanId` as the current innermost `engine.step` span id when the engine step context is available to the PageAnalyzer. The parent SHALL be resolved at runtime through an injected trace-context provider (`ITraceContextProvider.CurrentSpanId`), so the 4 existing `AnalyzeCurrentPageAsync` call sites need no signature change. When no engine step context is available (non-engine entry points, or no provider injected), `ai.call` SHALL be recorded as a root span — orphan spans SHALL be preserved, not suppressed. `ai.analyze` SHALL keep `parentSpanId == ai.call` as specified by the existing event-marker requirement. The parentage SHALL NOT change the recorded spanType, attributes, status, or timing of `ai.call`/`ai.analyze`.

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
