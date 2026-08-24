## Context

See `proposal.md` for motivation. Hierarchical observability currently has two valid but disconnected materializations: DriverHost retains finalized immutable `TraceRun` values for registered runs and projects them into Runtime events, while the Harness capture store can publish an optional `observability-trace.json`. Neither side offers a bounded read contract for the raw trace/span model. Architecture v1 assigns facts, persistence, replay, and analytics to the Data Plane and forbids reverse dependency into Agent Core; Protocol v1 freezes the current wire method set.

## Goals / Non-Goals

**Goals:**

- Make finalized trace metadata and spans queryable in process for one known registered run.
- Load and validate one known published capture, including its optional TraceRun, through a Harness-owned read boundary.
- Provide deterministic pagination, typed exact filters, explicit absence/error semantics, and authority-firewall tests.
- Preserve the existing immutable TraceRun model, append-only capture publication, and Runtime failure isolation.

**Non-Goals:**

- New DriverHost, DSH, CLI, HTTP, JSON-RPC, or UI operations.
- Query by Goal, Scenario, natural language, diagnostic text, latest run, or filesystem scanning.
- Database/index/search/retention services, remote telemetry, live streaming, push delivery, or mutable caches.
- Event-search expansion, recorder schema changes, new instrumentation boundaries, or repair of persisted captures.
- Any change to Agent lifecycle, FSM, Traversal, Recovery, action authorization, WorldBelief, GoalEvidence, Runtime Result, or Goal Evaluation.

## Decisions

### D1 — Keep live-run and persisted-capture read sides distinct

DriverHost's read side locates an already registered finalized trace by explicit `runId`. Harness's read side locates an already published capture by explicit `CaptureSessionId`. They share immutable trace values and pure validation rules where appropriate, but they do not share identity authority: no `runId`-to-directory guess, implicit latest selection, background index, or Scenario lookup is introduced.

Alternative considered: one generic trace repository keyed by any available identifier. Rejected because it obscures whether data is live or persisted, encourages inferred correlation, and risks turning the Harness store into a system-of-record authority it does not own.

### D2 — Expose an in-process summary plus paged span projection

The DriverHost `IReadOnlyObservability` / `DriverHostObservability` boundary gains two semantic operations:

- get a trace summary for one registered run;
- get a page of span envelopes after an optional cursor using an optional bounded filter.

The summary contains supported schema version, TraceRun/trace/run correlation exactly as recorded, span count, and diagnostics. `TraceRun` has no run-level observability outcome, so the read model does not derive one from child spans. A span envelope contains a read-model sequence plus the immutable recorded span. The query boundary does not expose a mutable Runtime object.

No wire method is added in this change. `IUniClawControlSurface` remains a separately guarded closed method set and is not extended. Queries are consumed directly through the read-only observability boundary until a future protocol/control exposure gate is explicitly authorized. A future external operation requires a concrete DSH/CLI consumer and an additive Protocol v1 gate that defines sanitized DTOs and transport compatibility.

Alternative considered: add `trace.get` / `trace.spans.query` directly to JSON-RPC. Rejected for this phase because Protocol v1 and the production-server contract currently freeze the method set; implementation convenience is not authorization to amend it.

### D3 — Derive stable query sequence without changing TraceSpan identity

For each finalized `TraceRun`, projection sorts spans by `(StartOffsetNs, SpanId)` and assigns one-based sequence numbers. `SpanId` breaks equal-offset ties deterministically. The cursor binds `runId`, `TraceRunId`, and last sequence. Pages return only higher sequences; query filters affect inclusion but never renumber the frozen trace projection.

A continuation request must use the same run, TraceRun identity, and filter fingerprint. Mismatch fails closed instead of restarting. This makes duplicate polling harmless and prevents cross-trace cursor reuse without adding sequence fields to the persisted TraceSpan schema.

Alternative considered: page by collection index. Rejected because callback/materialization order is not a stable contract. Alternative considered: mutate TraceSpan to persist a sequence. Rejected because query pagination is a read-model concern, not trace identity.

### D4 — Allow only typed exact-match filters

The first version permits optional exact filters over stable `Name`, `Layer`, `Component`, `Outcome`, and `ParentSpanId`. All supplied filters are conjunctive. There is no regex, substring, arbitrary attribute predicate, query language, prompt, reason parsing, or CLR-name matching. Unsupported values produce typed validation issues.

Alternative considered: return the entire TraceRun and let each caller search it. Rejected because it creates unbounded transfer pressure and duplicates pagination/filter semantics. Alternative considered: general search expressions. Rejected because no buyer justifies a query language or its authority/security surface.

### D5 — Introduce a separate Harness read contract

The Harness gains a narrow reader separate from the append-only save contract. Its physical lookup key is always `CaptureSessionId`; an optional required `TraceRunId` is an exact post-validation match, never a path key. The result is discriminated rather than a boolean plus free-form error, distinguishing at least found, capture-not-found, trace-absent, compatibility-rejected, identity-mismatch, and validation-rejected outcomes with typed issues.

The reader reconstructs artifact content from files because artifact bytes are intentionally omitted from the JSON manifest. It validates safe paths, published identity/state, supported schemas, record ordering, declared artifacts, byte counts, content hashes, checksum entry coverage, and TraceRun hierarchy/timing. Existing checksum coverage proves artifact integrity only; JSON structural validation must not be described as cryptographic tamper proof.

Alternative considered: add `LoadAsync` to the write store. Rejected because append-only publication and fail-closed reconstruction have different result models and responsibilities. A concrete filesystem type may implement both interfaces through shared private validation.

### D6 — Extract pure validation, preserve publication semantics

Current write/staging validation logic may be extracted into Harness-internal pure helpers so save and read enforce the same schema, identity, record, artifact, and TraceRun constraints. Extraction must not weaken atomic staging, overwrite refusal, or the public save result. The reader never accepts staging directories and never repairs or republishes data.

### D7 — Mechanically guard the authority firewall

Tests compare Runtime state, dispatched actions, recovery state, GoalEvidence, capture files, and existing wire method behavior before and after repeated queries and failed reads. Production dependencies remain one-way: DriverHost/Harness may read immutable trace data; Runtime, Agent, Container, Traversal, Recovery, and Environment cannot reference the new read model or reader.

### D8 — Apply-time v1 vocabulary is closed

The DriverHost read model uses these semantic records: `TraceRunSummary`, `TraceRunSummaryResult`, `TraceSpanFilter`, `TraceSpanCursor`, `TraceSpanEnvelope`, and `TraceSpanPage`. Query status is closed to `Found`, `Unavailable`, `InvalidRequest`, and `CursorMismatch`. The default page size is 100 and the maximum is 256. A cursor binds the explicit registered `runId`, finalized `TraceRunId`, last read-model sequence, and a deterministic fingerprint of the exact filter fields. Filter fields are limited to `Name`, `Layer`, `Component`, `Outcome`, and `ParentSpanId`.

The Harness read side uses a separate `ITraceCaptureReader` and `TraceCaptureReadResult`. Read status is closed to `Found`, `CaptureNotFound`, `TraceAbsent`, `UnsupportedSchema`, `IdentityMismatch`, and `ValidationFailed`; detailed failures use typed issue codes rather than free-form control decisions. `TraceAbsent` may carry the otherwise valid immutable capture bundle, while unsupported, mismatched, or invalid reads return no bundle. The optional required `TraceRunId` is compared only after the capture has passed validation.

These names describe in-process contracts only. They are not wire DTOs, Runtime Result values, capture lifecycle transitions, or Goal Evaluation dispositions.

## Risks / Trade-offs

- [A process restart loses registered in-memory traces] → State this honestly; persisted capture reading is the post-process path, and no database/index is purchased.
- [Concurrent registration could expose a partial trace] → Publish/query only immutable finalized TraceRun snapshots and return typed unavailable before finalization.
- [Equal span start offsets destabilize pagination] → Use SpanId as the deterministic secondary key and bind cursors to the finalized TraceRun identity.
- [Filters could become a hidden semantic engine] → Limit v1 to typed exact stable fields and forbid diagnostic/prompt/reason interpretation.
- [Filesystem paths or symlinks escape the capture root] → Validate one safe path segment, canonical containment, publication state, and indirection before reading content.
- [Existing checksums do not cover every JSON file] → Claim only structural/schema validation for JSON and cryptographic integrity for declared artifacts; a full publication-signing format requires a separate migration.
- [A new external consumer pressures an immediate wire method] → Require a separate Protocol v1 additive gate rather than leaking transport DTO decisions into this core change.

## Migration Plan

1. Add query contracts and deterministic span projection beside the existing DriverHost observability read side.
2. Add the Harness reader result model and safe explicit capture lookup.
3. Extract/reuse pure validation and prove old trace-absent captures plus current trace-attached captures remain readable.
4. Add authority guards and targeted tests, then run full Runtime/DriverHost/Harness regression, consistency, and strict OpenSpec validation.
5. Leave composition and transport unchanged. Rollback removes only additive read contracts/projections/reader; existing TraceRun production and capture publications remain valid.

## Open Questions

None. External transport, full-publication checksums, run-to-capture indexing, and event-search expansion each require a separate buyer and gate.
