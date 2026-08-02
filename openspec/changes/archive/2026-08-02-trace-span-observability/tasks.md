# Tasks — trace-span-observability

> Phased P1→P7 deliver the change; P8 (Phase 2 source generator) is **DEFERRED**. Anchor lines are current as of branch `feature/refactor`; re-resolve symbols via MCP first per `.claude/MCP-QUERY.md` if lines shift. Every checkbox maps to a code-anchored acceptance criterion in §8.

## 1. P1 — Core: `TraceSpan` record + `ITraceRecorder.StartSpan/EndSpan` + storage + `ITraceQuery`

- [x] 1.1 Add `TraceSpan` sealed record (`src/UniClaw.Core/Observability/TraceSpan.cs`): `SpanId`, `ParentSpanId?`, `SpanType` (string), `SpanName`, `StartTime` (DateTimeOffset), `EndTime?`, `Status` (string: "ok"|"error"|"deny"|"skip"), `TraceContext?`, `Attributes?` (Dictionary<string,object>); computed `DurationMs` (0 when EndTime null). JSON-serializes camelCase via `DomainJsonOptions`.
- [x] 1.2 Extend `ITraceRecorder` (`ITraceRecorder.cs:213`) with `StartSpanAsync(string spanType, string spanName, string? parentSpanId = null, Dictionary<string,object>? attributes = null, CancellationToken ct = default)` (returns spanId, records StartTime) and `EndSpanAsync(string spanId, string status = "ok", Dictionary<string,object>? attributes = null, CancellationToken ct = default)` (writes EndTime + merges final attributes). Closing an already-closed/unknown spanId is a no-op, never a crash. The 7 existing async methods are unchanged. (Async to match the ITraceRecorder write-contract surface — architecture guard updated to 9 methods.)
- [x] 1.3 Add `_spans` (`List<TraceSpan>`) to `InMemoryTraceStorage` (`InMemoryTraceStorage.cs:16-24`) alongside the existing 5 lists; implement span CRUD (add on StartSpan/EndSpan, lookup by id/parent/type); update `ITraceStorage` with the span read/write members. Existing `_executions`/`_transitions`/`_errors`/`_pageTransitions`/`_aiCalls` consumers stay unchanged.
- [x] 1.4 Add `"span"` `record_type` constant to `FileTraceStorage` (`FileTraceStorage.cs:37-41`); serialize `TraceSpan` through the existing `SerializeWithDiscriminator`/`DeserializeByType` path (`:175-244`); corrupt-line skip (D-93) applies. (Append-only JSONL: close appends a second line; reads deduplicate by spanId keeping the last.)
- [x] 1.5 Define `ITraceQuery : ITraceService` (`src/UniClaw.Core/Observability/ITraceQuery.cs`): `TraceSpan? GetRootSpan()`, `IReadOnlyList<TraceSpan> GetSpansByType(string spanType)`, `IReadOnlyList<TraceSpan> GetChildSpans(string parentSpanId)`, `TraceSpan? GetSpan(string spanId)`, `IReadOnlyList<TraceSpan> GetAllSpans()`. `InMemoryTraceService` implements `ITraceQuery` delegating to `InMemoryTraceStorage`.
- [x] 1.6 Add a `SpanTypes` static string catalog (`src/UniClaw.Core/Observability/SpanTypes.cs`) listing every emitted dotted spanType (engine.run, engine.step, entry.generate, entry.observed, entry.ignored, entry.visited, entry.skipped, entry.action, action.click, action.scroll, action.back, action.launch, action.wait, ai.call, ai.analyze, analyze.completion, analyze.error_loop, analyze.tree).

## 2. P2 — Core: Phase 1 engine + entry instrumentation (incl. `DynamicChildManager` wiring fix)

- [x] 2.1 In `TraversalEngine.RunAsync` (`:236`): `StartSpan("engine.run")` before the `:249` MaxSteps loop; `StartSpan("engine.step", runSpanId)` per iteration; `EndSpan(stepSpanId)` at loop end; `EndSpan(runSpanId)` after the loop.
- [x] 2.2 **Wiring fix:** at `TraversalEngine.Initialize()` (`:115`), construct `DynamicChildManager` with its `ITraceCoordinator` trace so the existing `:859` `RecordDynamicLifecycleAsync` call fires (currently dead — `_trace` is null).
- [x] 2.3 In `DynamicChildManager.Generate` (`:753`): `StartSpan("entry.generate", currentStepSpanId, {entry.parent_node, entry.fingerprint})`; per matchResult emit `entry.observed` (new item) or `entry.ignored` (dedup hit, attributes: entry.name, entry.reason); `EndSpan(genSpanId, "ok", {entry.match_count, entry.ignored_count})`.
- [x] 2.4 In `InterceptionHandler.OnBranch` (`:42`): after the push-child at `:50-58`, `StartSpan("entry.visited", currentStepSpanId, {entry.name, entry.node_id, entry.step, entry.depth})` via `StepContext.Trace`'s recorder surface (extend `StepContext`/`ITraceCoordinator` only if a span-write passthrough is missing — prefer adding `StartSpan`/`EndSpan` passthrough on `ITraceCoordinator` rather than a new dependency).
- [x] 2.5 Verify: existing 930+ Core+Host tests stay green; `EnumValueGuardTests.SpanType_Has11Values` still passes (enum untouched).

## 3. P3 — Host: `entry.skipped` + `action.*` + `ai.*` spans

- [x] 3.1 In `SafeActionExecutor` (`SafetyGate.cs:278`): on the deny branch (`ExecuteAsync :344-351` / `WaitAsync :333-340`), emit `entry.skipped` (parent = the step's `entry.visited` span; attributes: entry.name, entry.rule_id, entry.reason) before/alongside the existing `_sink.RecordAsync(decision)` (`:377`). The journal write is retained. **Implemented**: `SafeActionExecutor` gained optional `ITraceQuery? traceQuery` + `ITraceRecorder? traceRecorder`; deny → `RecordSkippedAsync` parents to the latest `entry.visited` (Host 侧无 Core TraceCoordinator，跨步骤回查 `GetSpansByType(EntryVisited)` 最近一条；composition root `HostCommands` 注入 `traceService` + `traceRecorder`).
- [x] 3.2 Emit `action.click`/`action.scroll`/`action.back`/`action.launch`/`action.wait` spans at the ADB action sites (attributes: action.type, action.result, action.*_ms) — scope to the action executor path the engine drives; if the action executor is the `SafeActionExecutor`-decorated `IActionExecutor`, emit there. **Implemented**: `ExecuteAsync`/`WaitAsync` allowed 分支按 `ActionToSpanType` 映射发出 click/scroll/back/wait（input/long_press 不在 §3.4 矩阵 → 不埋），Stopwatch 计 `action.adb_ms`。parent = 最新 `entry.visited`（design 的 back/wait parent=engine.step 在 Host 不可达，回落 entry.visited）。`action.launch` 由 `SafeEntryActionDriver` 驱动不在本 executor 路径，未埋（P4/P7 视需要）。
- [x] 3.3 Emit `ai.call` (HTTP round-trip: ai.capability, ai.provider_id, ai.latency_ms, ai.tokens, ai.success, ai.model, ai.mode) and `ai.analyze` (parent=ai.call: ai.page_fingerprint, ai.item_count, ai.retry_count) at the vision/model call sites — `OpenAiCompatibleVisionProvider` / `PageAnalyzer.AnalyzeCurrentPageAsync`. **Implemented**: `PageAnalyzer` 增加可选 `ITraceRecorder?`；`AnalyzeOnceAsync` 包裹 `CompleteVisionAsync` 发 `ai.call`，解析后发 `ai.analyze`（parent=ai.call，attempt→ai.retry_count）。`ai.page_fingerprint` 在 PageAnalyzer 无指纹源，省略（§3.6 属性可选）。**偏差**: ai.call parent 留空 —— PageAnalyzer 无法触达 Core TraceCoordinator 的 engine.step spanId（跨层无通道），P7 树重建容忍孤儿 ai.*。recorder 经 `UniBrainFactory` 注入。

## 4. P4 — Host: `BaselineBuilder` + `artifacts/baselines/`

- [x] 4.1 Add `src/UniClaw.Host/Analysis/BaselineBuilder.cs`: after a run, read `ITraceQuery.GetAllSpans()`, compute `itemsObserved`/`itemsVisited`/`itemsSkipped`/`stepsUsed`/`scrollCount`/`endOfListDetected`/`success`/`aiLatencyP50`/`aiLatencyP95`, append one JSON line to `artifacts/baselines/<scenarioId>.jsonl`.
- [x] 4.2 Add `artifacts/baselines/` dir (gitkeep); one file per `scenarioId` (loaded via `ScenarioCatalog` scenarioId).
- [x] 4.3 p50/p95 computation: when the file has ≥ 10 records, compute p50/p95 of itemsVisited, stepsUsed, aiLatency; expose a `BaselineProfile.Load(scenarioId)` that returns null / "not enough data" below 10.

## 5. P5 — Host: `ICompletionAnalyzer` + analyzers

- [x] 5.1 Add `src/UniClaw.Core/Observability/ICompletionAnalyzer.cs` (interface in Core — analyzers are Host but the contract is Core so `CompletionMonitor` composition can vary): `Task<CompletionVerdict?> EvaluateAsync(ITraceQuery trace, CancellationToken ct)` + `CompletionVerdict` record (`ShouldTerminate`, `Reason`, `Confidence` 0.0-1.0).
- [x] 5.2 Add `Host/Analysis/EnumerateCompletionAnalyzer.cs`: count observed/visited/skipped from `GetSpansByType`; `pending = observed - visited - skipped`; load `BaselineProfile`; apply rules Halt/Terminate/Recommend/Warn/Observe per design §7.4.2; write `analyze.completion` span regardless of verdict.
- [x] 5.3 Add `Host/Analysis/ErrorLoopAnalyzer.cs`: detect consecutive ≥5 steps all-skipped-no-visited (`stuck_in_error_loop`, 0.9) and per-page skipped>visited×4 (`skip_rate_too_high`, 0.7).
- [x] 5.4 Cold-start: baseline < 10 → only Halt + Warn fire; Terminate/Recommend suppressed; all verdicts still written as Observe spans.

## 6. P6 — Host: `CompletionMonitor` scheduler + composition

- [x] 6.1 Add `Host/Analysis/CompletionMonitor.cs` (`IDisposable`): configurable poll interval (default 500ms); per tick run each `ICompletionAnalyzer.EvaluateAsync`; write `analyze.completion`/`analyze.error_loop` span; on `Confidence >= 0.9` cancel the linked CTS; on `0.7 <= Confidence < 0.9` invoke the `Func<CompletionVerdict, Task<bool?>>` Recommend callback (true→cancel, false→continue, null→downgrade to Observe); `< 0.7` continue.
- [x] 6.2 Wire `CompletionMonitor` in the Host composition root (`HostCommands.RunScenarioAsync` / `HostRunServices`): linked CTS with the run's cancellation token; `_ = monitor.StartAsync()` background; `await engine.RunAsync(cts.Token)`; `monitor.Stop()` after. A monitor crash must not affect the engine (it simply stops canceling).
- [x] 6.3 Edge cases: missing/corrupt baseline → Observe-only + log warn; repeated Recommend in one run → second downgrade to Terminate; `observed > p95×2` → flag abnormal, do NOT Terminate; per-scenarioId isolated baseline files.

## 7. P7 — Integration: span tree reconstruction + closed loop

- [x] 7.1 Add a Core unit test: a mock run's `GetAllSpans()` reconstructs the entry tree — `GetRootSpan().SpanType == "engine.run"`, `GetChildSpans(runSpanId)` returns the `engine.step` children, `entry.visited` is parent of `entry.skipped`/`action.*`.
- [x] 7.2 Add a Host unit test: `EnumerateCompletionAnalyzer` on a mock span tree with `pending==0 && end_reached` returns Halt (conf 1.0); on baseline ≥10 + `visited>=p95` returns Terminate (0.9); cold-start (baseline <10) suppresses Terminate.
- [x] 7.3 Add a Host unit test: `CompletionMonitor` with an analyzer returning conf 0.95 cancels the linked CTS within one poll; conf 0.5 does not cancel.
- [x] 7.4 Add a Host unit test: `BaselineBuilder` appends one correct JSON line per run; `BaselineProfile.Load` returns "not enough data" at 9 records and p50/p95 at 11.

## 8. P8 — Phase 2 `[TraceSpan]` source generator (DEFERRED)

> **DEFERRED** by design (design.md D1 / proposal: "Phase 2 deferred to a future `trace-span-source-generator` change"). Not executed this pass. Land only after Phase 1 spanType catalog stabilizes.

- [ ] 8.1 `[TraceSpan]` attribute + Roslyn source generator emitting the `StartSpan`/`EndSpan` calls now written by hand in P2/P3.
- [ ] 8.2 Migrate P2/P3 manual spans to attributes; delete the hand-written calls; behavior equivalent (per design §6.4).

## 9. Acceptance criteria (explicit, code-anchored, per phase)

> Each criterion is an **objective verdict** — a grep/test/compile check that passes or fails, not prose. Re-resolve by symbol (MCP first) if lines shift.

### P1 — Span model + recorder + storage + query

- [x] 9.1 `TraceSpan` exists and round-trips through JSON with `record_type:"span"` — `TraceSpanTests.TraceSpan_Json_RoundTripsThroughDomainJsonOptions` + `TraceSpan_DurationMs_ZeroWhenEndTimeNull` + `TraceSpan_InvalidStatus_ThrowsDomainValidationException` pass
- [x] 9.2 `ITraceRecorder` has `StartSpanAsync`/`EndSpanAsync` — architecture guard updated to `ITraceRecorder_Has9Methods` (9 methods, green); the original 7 async methods unchanged
- [x] 9.3 `_spans` (`List<TraceSpan>`) declared in `InMemoryTraceStorage` — `InMemoryStorage_SpanWrites_LeaveExistingListsUntouched` asserts the 5 existing lists are unaffected by span writes
- [x] 9.4 `"span"` record_type const in `FileTraceStorage` — `FileTraceStorage_OpenCloseSpan_ReadsBackDeduplicated` round-trips a span through the file store (append-only, dedup by spanId)
- [x] 9.5 `InMemoryTraceService` implements `ITraceQuery` — `ITraceQuery_GetRootSpan_ReturnsParentNullSpan` + `ITraceQuery_ThreeLevelTree_Queries` pass; 12 `ITraceService` methods unchanged; `dotnet test` Core 1028 pass (was 1013, +15 new)

### P2 — Engine + entry instrumentation

- [x] 9.6 `grep -n 'StartSpan(SpanTypes.EngineRun\|StartSpan(SpanTypes.EngineStep' src/UniClaw.Core/Traversal/TraversalEngine.cs` returns hits in `RunAsync`; `EndSpan` pairs with each via `EndEngineStepSpan`/`EndEngineRunSpan` helpers (instrumentation uses catalog constants per spec §100, not string literals)
- [x] 9.7 `DynamicChildManager` is constructed with a non-null trace — `grep -n 'new DynamicChildManager' src/UniClaw.Core/Traversal/TraversalEngine.cs` shows the trace argument at `:115`; a test asserts `Generate` on a node emits `entry.generate` + `entry.observed`/`entry.ignored` (the previously-dead `:859` `RecordDynamicLifecycleAsync` now fires)
- [x] 9.8 `grep -n 'StartSpan("entry.visited"' src/UniClaw.Core/Traversal/InterceptionHandler.cs` returns a hit after the `:50-58` push-child (implemented via `RecordEntryVisited` helper calling `ctx.Trace.StartSpan`)
- [x] 9.9 All 930+ existing tests pass (Core 1034; Host 112) — `dotnet test src/UniClaw.Core.sln` green; `EnumValueGuardTests.SpanType_Has11Values` green (enum untouched — `git diff src/UniClaw.Core/Observability/ITraceRecorder.cs` shows no change to the `SpanType` enum block `:10`)

### P3 — `entry.skipped` + `action.*` + `ai.*`

- [x] 9.10 `grep -n 'SpanTypes.EntrySkipped' src/UniClaw.Host/Safety/SafetyGate.cs` returns a hit on the deny path (`RecordSkippedAsync`, `:452`); Host test `Deny_EmitsEntrySkippedUnderLatestEntryVisited_AndStillJournals` asserts a denied action emits `entry.skipped` whose parent is the latest `entry.visited` and the `InMemorySafetyDecisionSink` journal still records the decision (asserted `sink.Decisions` count)
- [x] 9.11 `action.*` and `ai.call`/`ai.analyze` spanTypes appear in the emitted span set — Host test `AllowedAction_EmitsActionClickUnderLatestEntryVisited` asserts `GetSpansByType("action.click")` non-empty with `action.type`/`action.adb_ms`; Core test `PageAnalyzer_EmitsAiCall_WithAiAnalyzeChild` asserts `GetSpansByType("ai.call")` non-empty after a vision call and `ai.analyze` parents to the `ai.call` span (grep uses catalog constants per spec §100, not string literals)

### P4 — BaselineBuilder

- [x] 9.12 `test -d artifacts/baselines` passes; `grep -rn 'BaselineBuilder' src/UniClaw.Host/Analysis/` returns the class; a Host test asserts one run appends exactly one JSON line with all 9 aggregate fields to `artifacts/baselines/<scenarioId>.jsonl`
- [x] 9.13 `BaselineProfile.Load` returns "not enough data" with 9 records and yields p50/p95 with 11 — a test covers both branches

### P5 — Analyzers

- [x] 9.14 `EnumerateCompletionAnalyzer` test: `pending==0 && end_reached` → Halt conf 1.0; baseline ≥10 + `visited>=p95` → Terminate 0.9; cold-start (<10) suppresses Terminate
- [x] 9.15 `ErrorLoopAnalyzer` test: 5 consecutive all-skipped steps → `stuck_in_error_loop` conf 0.9; per-page skipped>visited×4 → `skip_rate_too_high` 0.7

### P6 — CompletionMonitor + composition

- [x] 9.16 `CompletionMonitor` test: analyzer conf 0.95 cancels the linked CTS within one poll tick; conf 0.5 does not cancel
- [x] 9.17 `grep -n 'CompletionMonitor' src/UniClaw.Host/Commands/HostCommands.cs` (or `HostRunServices`) returns the composition site; the monitor is `StartAsync()`-ed before `engine.RunAsync` and `Stop()`-ped after
- [x] 9.18 Edge-case test: missing baseline file → only Halt can terminate + a warning is logged; a second Recommend in one run downgrades to Terminate

### P7 — Integration

- [x] 9.19 Integration test: a full mock run's `GetAllSpans()` reconstructs the entry tree — root `engine.run`, child `engine.step` spans, `entry.visited` is parent of its `entry.skipped`/`action.*`
- [x] 9.20 `dotnet test src/UniClaw.Core.sln` green end-to-end with all new tests added

### P8 — DEFERRED

- [ ] 9.21 (Not executed this pass.) `[TraceSpan]` attribute + source generator land in a separate `trace-span-source-generator` change; manual P2/P3 spans migrate to attributes with equivalent behavior.
