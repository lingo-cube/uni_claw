## Context

UniClaw's trace subsystem is more mature than the design doc's "flat event list" framing suggests, and this change must coexist with — not replace — what already lands. Current state (symbols resolved via MCP, branch `feature/refactor`):

- **`SpanType` enum** (`src/UniClaw.Core/Observability/ITraceRecorder.cs:10`) — 11 values, **constitution-locked** by `EnumValueGuardTests.SpanType_Has11Values` (C-11 change flow required to add a value). `ExecutionRecord` already carries `SpanType? SpanType`, `SpanId`, `ChildNodeId`, `ParentNodeId`, `Depth`.
- **`ITraceRecorder`** (`ITraceRecorder.cs:213`) — 7 async write methods (`StartSessionAsync` … `RecordAICallAsync`); **no `StartSpan`/`EndSpan`**. `TraversalEngine` already injects `ITraceRecorder? _traceRecorder` (`TraversalEngine.cs:30`, ctor `:61/75`).
- **`ITraceService`** (`ITraceService.cs:10`) — 1 property + 12 methods, **already including tree-shaped queries**: `ReconstructTree()`, `GetNodeSpans(nodeId)`, `GetNodeVisitTimeline(nodeId)`, `GetStepTimeline(step)`, `GetBySpanType(SpanType)`, `GetStepSpanGroup(stepSpanId)`. But these operate on the flat `ExecutionRecord` list — no start/end timing, no attribute bags, no true parent-span-id tree.
- **`ITraceCoordinator`** (embedded in `TraversalEngine.cs:955`, impl `TraceCoordinator :988`) — the engine-internal correlation surface: `PushSpan()`/`PopSpan()`/`ClearVisitSpan()`/`BuildCorrelation()`/`GetStepSnapshot()`, backed by `_spanCounter`, `_currentStepSpanId`, `_currentVisitSpanId`, `Stack<string?> _spanStack`, `_stepStopwatch`. A span stack **already exists in the engine**.
- **`TraceContext`** (`TraceContext.cs:18`) — already carries `NodeId`, `StepSpanId`, `StepNumber`, `TraceId`, `VisitSpanId`, `ParentSpanId`.
- **Storage** — `InMemoryTraceStorage` (`InMemory/InMemoryTraceStorage.cs:16-24`) holds 5 lists (`_executions`/`_transitions`/`_errors`/`_pageTransitions`/`_aiCalls`) + 2 indexes; `InMemoryTraceService` delegates to it. `FileTraceStorage` (`File/FileTraceStorage.cs:14`) writes JSONL at `{baseDir}/{traceId}/trace.jsonl` with `record_type` as first field (`:37-41` private consts: `execution`/`state_transition`/`error`/`page_transition`/`ai_call`); `MirroringTraceStorage` (`Host/Observability/MirroringTraceStorage.cs:9`) decorates it.
- **Existing post-hoc analyzer** — `VerificationAnalyzer` (`Host/Verification/VerificationAnalyzer.cs:16`) reads `ITraceService.GetExecutions()`/`GetErrors()` + `SafetyDecisionJournal` strictly after `RunAsync()`. This is the precedent for Host-side, post-hoc, trace-reading analysis.
- **Latent wiring gap** — `DynamicChildManager` (`TraversalEngine.cs:665`) has an `ITraceCoordinator? _trace` field and calls `_trace.RecordDynamicLifecycleAsync(...)` at `:859`, but `TraversalEngine.Initialize()` constructs it at `:115` as `new DynamicChildManager(registry)` — **`_trace` is null**, so that call never fires. This change fixes the wiring as part of `entry.*` instrumentation.
- **None of** `TraceSpan`, `ITraceQuery`, `ICompletionAnalyzer`, `CompletionVerdict`, `CompletionMonitor`, `BaselineBuilder` exist yet (MCP `find_symbol` empty for all).
- **`artifacts/baselines/`** does not exist; `artifacts/` has `benchmarks/`, `runs/`, `videos/`. No baseline JSONL anywhere. `scenarioId` comes from `scenarios/android-settings/enumerate-settings-safely.v1.json`, loaded by `ScenarioCatalog`.

Stakeholders: this change is the foundation for the three sibling design docs — `ai-trace-analysis-design` (replaces the rule-based analyzers here with an AI agent over `ITraceQuery` tools), `baseline-simulation-design` (consumes the `baseline.jsonl` produced here as offline simulation input), and `screen-recording-design` (independent). Sequencing them in this order is the user's intent.

## Goals / Non-Goals

**Goals:**
- A true parent-child span tree with timing (`TraceSpan`: `ParentSpanId`, `StartTime`/`EndTime`, `DurationMs`, `Attributes`) coexisting with the existing `ExecutionRecord`/`SpanType` system.
- A general-purpose span write/read API (`ITraceRecorder.StartSpan/EndSpan`, `ITraceQuery`) the analyzers and instrumentation call.
- Phase 1 manual instrumentation of the engine/entry/action/ai span types.
- Real-time termination: a Host `CompletionMonitor` that polls `ITraceQuery`, writes `analyze.completion` spans, and cancels the engine on Halt/Terminate.
- Offline `BaselineBuilder` producing `artifacts/baselines/<scenarioId>.jsonl` and p50/p95 thresholds that drive the analyzer from data once ≥ 10 records exist.

**Non-Goals:**
- Phase 2 `[TraceSpan]` source generator (deferred to a future `trace-span-source-generator` change).
- Replacing or migrating the existing `ExecutionRecord`/`SpanType`/`ITraceCoordinator` system — it stays as-is.
- AI-agent-based completion analysis (sibling `ai-trace-analysis-design`).
- Offline baseline-driven **simulation** (sibling `baseline-simulation-design` — consumes this change's `baseline.jsonl` but is a separate change).
- macOS screen recording (sibling `screen-recording-design`).
- Adding values to the `SpanType` enum (constitution-locked — intentionally avoided; see D1).

## Decisions

### D1 — Parallel `TraceSpan` system, not an extension of `ExecutionRecord`/`SpanType`
**Choice:** Add a new `TraceSpan` record (string `spanType`) + a new `_spans` list + a new `"span"` JSONL `record_type`, all parallel to the existing `ExecutionRecord`/`SpanType`-enum system. Existing consumers (`ITraceService` 12 methods, `VerificationAnalyzer`) are untouched.
**Rationale:** The design needs an OpenTelemetry-style span tree with start/end timing and attribute bags. `ExecutionRecord` is a flat record with `SpanType`/`Depth`/`ParentNodeId` but no timing or attributes, and reshaping it would risk the 930+ green tests. The `SpanType` enum is constitution-locked at 11 values (C-11 flow to add a value); the design's ~20 dotted spanTypes (`engine.run`, `entry.observed`, `action.click`, `ai.analyze`, `analyze.completion`, …) cannot reasonably be expressed as enum values without churning the constitution and bloating the locked guard.
**Alternative considered:** Extend `ExecutionRecord` with `StartTime`/`EndTime`/`Attributes` + add the new spanTypes to the `SpanType` enum (C-11). **Rejected** — flat-list reshape across `InMemoryTraceStorage`/`FileTraceStorage`/all consumers is high-regression, and the constitution churn per value is disproportionate. The string `spanType` is open-ended and matches OTel convention.
**Trade-off:** Two span systems coexist until Phase 2; loses compile-time exhaustiveness on `spanType`. Mitigated by a `spanType` string catalog + a test that asserts every emitted `spanType` is in the catalog.

### D2 — `StartSpan`/`EndSpan` on `ITraceRecorder`, coexisting with `ITraceCoordinator.PushSpan`/`PopSpan`
**Choice:** The new span lifecycle lives on `ITraceRecorder` (the general write surface). The existing `ITraceCoordinator.PushSpan`/`PopSpan`/`BuildCorrelation` engine-internal stack is **unchanged**. The two are not merged.
**Rationale:** `ITraceCoordinator` is the engine's correlation machinery (`StepSpanId` lifecycle, step snapshots, `BuildCorrelation` for `TraceContext`); it is engine-internal and not a Host-injectable general-purpose span API. The new analyzers are Host-side and must not depend on `ITraceCoordinator`. `StartSpan`/`EndSpan` is the minimal general API both instrumentation and analyzers call.
**Alternative:** Route `StartSpan` through `ITraceCoordinator`. **Rejected** — couples general spans to engine step state and forces Host analyzers to take an engine-internal dependency.

### D3 — `ITraceQuery` inherits `ITraceService`
**Choice:** `ITraceQuery : ITraceService`; `InMemoryTraceService` implements `ITraceQuery` (one additional interface on the existing class).
**Rationale:** `InMemoryTraceService` already implements `ITraceService` over the same `InMemoryTraceStorage`; the new span queries (`GetRootSpan`/`GetSpansByType(string)`/`GetChildSpans`/`GetSpan`/`GetAllSpans`) read the same storage. Inheritance gives read-only consumers a superset with one injection.
**Alternative:** Composition (`ITraceQuery` holds an `ITraceService`). **Rejected** as the default — adds indirection and a second injectable. **Trade-off / open escape hatch:** if `ITraceQuery` grows fat (design doc §11 risk), switch to composition without breaking `ITraceService` consumers.

### D4 — Analyzers are Host-side, read `ITraceQuery`, write `analyze.*` spans, cancel via CTS
**Choice:** The contracts `ICompletionAnalyzer` + `CompletionVerdict` live in Core (`src/UniClaw.Core/Observability/`, alongside `ITraceQuery`, since the analyzer contract consumes `ITraceQuery` and Core must not depend on Host). The implementations `EnumerateCompletionAnalyzer`/`ErrorLoopAnalyzer`/`CompletionMonitor`/`BaselineBuilder` live in `src/UniClaw.Host/Analysis/`. They read `ITraceQuery` (Core), write `analyze.completion`/`analyze.error_loop` spans back via `ITraceRecorder`, and the `CompletionMonitor` cancels the engine through a linked `CancellationTokenSource` — **no engine change**.
**Rationale:** Mirrors the layering precedent — `ITraceService`/`ITraceQuery` contracts in Core, `VerificationAnalyzer` implementation in Host. Keeps "what does completion mean" out of the engine — the engine only records what happened. The `CompletionMonitor` is a Host composition concern around `engine.RunAsync(cts.Token)`.
**Alternative:** Put the contract and `CompletionMonitor` inside Core/`TraversalEngine`. **Rejected** — re-introduces engine-coupled policy (the exact anti-pattern `runner-through-engine` removed) and violates "engine records, Host decides".

### D5 — `spanType` is a dotted string namespace, not the `SpanType` enum
**Choice:** `TraceSpan.SpanType` is a `string` (`"<layer>.<event>"`, e.g. `engine.run`, `entry.observed`, `analyze.completion`). The constitution-locked `SpanType` enum is **not** extended.
**Rationale:** See D1 — avoids C-11 churn; open-ended; OTel-conventional; expresses the layer hierarchy the design's span tree depends on.
**Trade-off:** No compile-time exhaustiveness. Mitigated by a `SpanTypes` static catalog (constant strings) + a test asserting emitted spanTypes are catalog members.

### D6 — `BaselineBuilder` is offline, append-only JSONL, p50/p95 after ≥ 10 records
**Choice:** Each run appends one JSON line to `artifacts/baselines/<scenarioId>.jsonl`. p50/p95 computed only once ≥ 10 records exist; below that, `EnumerateCompletionAnalyzer` runs in cold-start mode (only Halt + Warn fire).
**Rationale:** Matches the existing `FileTraceStorage` JSONL pattern (inspectable, append-only, per-id file). The ≥ 10 threshold avoids unreliable early thresholds. One file per `scenarioId` isolates scenarios.
**Alternative:** A binary/DB baseline store. **Rejected** — JSONL is inspectable, diff-friendly, and consistent with the existing trace store; volume is tiny (one line per run).

### D7 — Fix the `DynamicChildManager` trace wiring as part of instrumentation
**Choice:** `TraversalEngine.Initialize()` (`:115`) constructs `DynamicChildManager` with its `ITraceCoordinator` trace so the existing `RecordDynamicLifecycleAsync` call fires, then adds `entry.generate`/`entry.observed`/`entry.ignored` spans.
**Rationale:** The `_trace` field is currently null — the existing `:859` call is dead code. Wiring it is a latent-bug fix and the prerequisite for `entry.*` instrumentation. Additive (more trace data); `ITraceService` consumers are unaffected.

## Risks / Trade-offs

- **[Two span systems → drift/confusion]** → `spanType` string catalog + catalog-membership test; Phase 2 source generator will eventually unify manual `StartSpan`/`EndSpan`. Cross-ref this design from the existing `trace-record`/`trace-storage` specs via the Impact section.
- **[`ITraceQuery` inheritance may bloat]** (design doc §11) → escape hatch: switch to composition if it grows; `ITraceService` consumers unaffected either way.
- **[Span volume — N spans per entry]** → query by `spanType` (`GetSpansByType`), lazy load; `_spans` is a plain `List<TraceSpan>` in `InMemoryTraceStorage`.
- **[`CompletionMonitor` 500 ms poll races the engine]** → linked `CancellationTokenSource`; on Halt/Terminate the monitor calls `cts.Cancel()` → engine throws `OperationCanceledException` → normal exit (design doc §7.4.5). A monitor crash does not affect the engine — it simply stops canceling, and the engine runs to `MaxSteps`/Exhaustive.
- **[Wiring `DynamicChildManager._trace` changes existing trace output]** (the `:859` call starts firing) → additive trace data only; `ITraceService` consumers read the unchanged 12-method surface; verify the 930+ existing tests stay green (acceptance criterion).
- **[Cold-start termination is conservative]** → by design: below 10 baseline records only `Halt` (`pending==0 && endOfList`) and `Warn` fire; `Terminate`/`Recommend` are suppressed so a new scenario does not under-stop.

## Migration Plan

Additive only — no migration, no rollback complexity. `_spans` is a parallel storage list; nothing depends on it until the new analyzers are wired. Rollback = revert the change; existing trace behavior is unchanged. Deployment order: (1) Core span model + storage + `ITraceQuery` + `StartSpan`/`EndSpan`; (2) Phase 1 instrumentation (incl. `DynamicChildManager` wiring fix); (3) Host `BaselineBuilder` + analyzers + `CompletionMonitor` composition. Each step is independently verifiable (see tasks acceptance criteria).

## Open Questions

- **Phase 2 timing:** when to start the `[TraceSpan]` source generator (deferred). Decide after Phase 1 lands and the manual spanType catalog stabilizes. Tracked as a deferred task, not a blocker.
- **`ITraceQuery` composition vs inheritance** (D3 escape hatch) — revisit if > 5 new methods accrete. No action now.
