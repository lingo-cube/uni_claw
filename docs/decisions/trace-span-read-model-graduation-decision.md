# Trace Span Read Model — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_TRACE_SPAN_READ_MODEL` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-trace-span-read-model/`
> Authority: Runtime Architecture Contract I-1..I-14, Architecture v1, and Protocol v1 remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** read-only consumers of finalized hierarchical trace data and persisted captures who need a bounded in-process read model for one explicitly identified registered run and a validated Harness-owned reader for one explicitly identified published capture (per proposal.md's Why section).

This receipt claims only that:

1. an in-process, read-only trace summary and cursor-paged span query exist for one explicitly identified registered run, exposed only through the `IReadOnlyObservability` / `DriverHostObservability` boundary, with no Runtime object or wire DTO changed;
2. a Harness-owned, fail-closed reader exists for one explicitly identified persisted `CaptureSessionId` (`ITraceCaptureReader` / `TraceCaptureReadResult`), validating schema, path, manifest, record, artifact, and TraceRun structure before returning one immutable bundle, and performing no scan, repair, replay, catalog, Runtime, or persistence mutation;
3. span queries support only typed exact-match filters (`Name`, `Layer`, `Component`, `Outcome`, `ParentSpanId`) over a stable read-model sequence assigned in deterministic `(StartOffsetNs, SpanId)` order, and never parse prompts, reasons, messages, or diagnostics into query authority;
4. absence and failure are explicit: found/not-found, trace-absent, unsupported-schema, invalid-capture, and cursor-mismatch outcomes are typed, and no empty success or partially trusted bundle is ever fabricated;
5. Runtime activity emission, Agent lifecycle, FSM, Traversal, Recovery, GoalEvidence, capture publication, and the existing Protocol v1 wire method set (`IUniClawControlSurface`) are unchanged;
6. two new capability specifications (`trace-span-read-model`, `persisted-trace-capture-read`) are added to `openspec/specs/` with no existing main specification weakened or redefined.

No claim is made for: any new DriverHost, DSH, CLI, HTTP, JSON-RPC, or UI operation; queries by Goal, Scenario, natural language, diagnostic text, latest run, or filesystem scanning; database/index/search/retention services, remote telemetry, live streaming, push delivery, or mutable caches; event-search expansion, recorder schema changes, new instrumentation boundaries, or repair of persisted captures; or any change to Agent lifecycle, FSM, Traversal, Recovery, action authorization, WorldBelief, GoalEvidence, Runtime Result, or Goal Evaluation (per proposal.md What Changes and design.md Non-Goals).

## 2. Validation evidence

- `dotnet build src/UniClaw.Runtime.sln --no-restore`: **PASS, 0 warnings, 0 errors** — recorded in `tasks.md` (Apply Receipt, 2026-08-22).
- Targeted capability tests: read model + persisted reader + architecture guards — **PASS, 80/80**; persisted reader alone — **PASS, 58/58** — recorded in `tasks.md` (Apply Receipt).
- Existing support regressions: observability conformance + trace-capture foundation + DriverHost wire/server + plugin/control guards — **PASS, 55/55** — recorded in `tasks.md` (Apply Receipt).
- Deterministic non-device regressions: SETTINGS/OpenWorld/RuntimeAgent Phase 1–4 selector — **PASS, 241/241** — recorded in `tasks.md` (Apply Receipt).
- Repository checks: `scripts/check-consistency.sh`, `openspec validate trace-span-read-model --strict`, `openspec validate --changes --strict` (15 active changes), and `git diff --check` — **PASS** — recorded in `tasks.md` (Apply Receipt).
- Full-suite classification: **1818 passed / 9 failed** — seven failures require unavailable `emulator-5554` / `emulator-5556`; one concurrent unrelated `RunExecutionCoordinatorTests.SameDeviceExclusivity_SecondConcurrentRejected_ReleasedAfterTerminal` failure; one concurrent unrelated architecture guard flagging `DeveloperOptions` in `SemanticEvidence.cs`; none references this change's read model or reader — recorded in `tasks.md` (Apply Receipt).
- Sol Final Verification: PASS for implementation completion, with apply only; graduation and archive explicitly deferred to a separate decision — recorded in `tasks.md` (§§1–5, task 5.2, and Apply Receipt).

The change's files record no separate `evidence/` directory; the build/test records above are the change's own validation record in `tasks.md` (Apply Receipt, 2026-08-22).

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (no `evidence/` directory; design.md's Risks/Trade-offs are design-time mitigations, not recorded falsifier results); rejection/negative requirements are defined in the delta specs:

- The read model MUST NOT choose a run from a Goal, Scenario, prompt, diagnostic string, latest-run heuristic, or other inferred correlation; when the requested run is unknown or its finalized trace is unavailable it SHALL return a typed unavailable result and SHALL NOT fabricate an empty successful trace (`specs/trace-span-read-model/spec.md`, Explicit registered-run lookup).
- A cursor bound to another run or finalized trace identity SHALL fail closed with a typed cursor-mismatch result and SHALL NOT silently restart or cross trace boundaries (`specs/trace-span-read-model/spec.md`, Stable cursor-paged span projection).
- Unsupported filter fields, free-form expressions, semantic reason parsing, and implementation-name matching MUST be rejected rather than interpreted; supplied prompt/expression/diagnostic-text/private-name text SHALL NOT be converted into query or Runtime authority (`specs/trace-span-read-model/spec.md`, Bounded typed span filters).
- Structural observability outcome MUST remain diagnostic and MUST NOT be represented as Runtime Result, action success, Goal completion, satisfaction, or recovery success; repeated queries SHALL NOT dispatch actions, transition the FSM, start or continue a Run, invoke recovery, update WorldBelief or GoalEvidence, select a Scenario, or orchestrate another Run, and this capability adds no DriverHost wire operation (`specs/trace-span-read-model/spec.md`, Hierarchy and observability semantics remain honest; Query is harmless and transport-independent).
- The capture reader MUST NOT scan for a run, infer a capture from `RunId`, `TraceRunId`, Goal, Scenario, prompt, or latest-directory order, or follow a path outside the configured root; unsafe identifiers or filesystem indirection SHALL be rejected before returning capture content (`specs/persisted-trace-capture-read/spec.md`, Capture lookup uses explicit safe identity).
- Any validation failure MUST reject the whole read and MUST NOT return a partially trusted bundle; the reader MUST NOT repair, rewrite, overwrite, quarantine, catalog, replay, dispatch, retry, recover, or derive GoalEvidence from a capture, and trace absence SHALL be reported without synthesizing spans or manufacturing a TraceRun from records, results, or diagnostic strings (`specs/persisted-trace-capture-read/spec.md`, Published capture is reconstructed and validated as one unit; Compatibility and absence are explicit; Read failures cannot mutate persistence or Runtime).

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- Any new DriverHost, DSH, CLI, HTTP, JSON-RPC, or UI wire operation — a future external consumer requires a concrete DSH/CLI consumer and an additive Protocol v1 gate that defines sanitized DTOs and transport compatibility (`proposal.md` What Changes; `design.md` D2).
- Query by Goal, Scenario, natural language, diagnostic text, latest run, or filesystem scanning (`design.md` Non-Goals).
- Database/index/search/retention services, remote telemetry, live streaming, push delivery, and mutable caches (`design.md` Non-Goals).
- Event-search expansion, recorder schema changes, new instrumentation boundaries, and repair of persisted captures (`design.md` Non-Goals).
- Any change to Agent lifecycle, FSM, Traversal, Recovery, action authorization, WorldBelief, GoalEvidence, Runtime Result, or Goal Evaluation (`design.md` Non-Goals).
- External transport, full-publication checksum/signing format, run-to-capture indexing, and event-search expansion each require a separate buyer and gate (`design.md` Open Questions; `design.md` Risks).

## 5. Final conclusion

**GRADUATED.** The bounded in-process read model for finalized traces of one explicitly identified registered run and the Harness-owned fail-closed reader for one explicitly identified published capture are human-authorized and supported by the change's recorded build, targeted-test, regression, consistency, and strict OpenSpec validation evidence; archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.