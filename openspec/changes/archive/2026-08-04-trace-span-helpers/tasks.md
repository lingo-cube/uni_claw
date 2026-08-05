## 1. M0 — Core helpers: `TraceSpanScope` + `RecordEventAsync`

- [x] 1.1 Add `TraceSpanScope` async-disposable type in `src/UniClaw.Core/Observability/` — holds `spanId` + recorder reference; `End(status, attributes)` sets status/attrs; `DisposeAsync` ends `"ok"` when not already ended; double-end is a no-op
- [x] 1.2 Add `ITraceRecorderExtensions.BeginSpanAsync(spanType, spanName?, parentSpanId?, attributes?, ct)` declared on `ITraceRecorder?` returning `TraceSpanScope`; null recorder yields a side-effect-free no-op scope (no span, no exception)
- [x] 1.3 Add `ITraceRecorderExtensions.RecordEventAsync(spanType, parentSpanId?, attributes?, ct)` recording a point-in-time event span with `EndTime` left null (`DurationMs == 0` per TraceSpan.cs L35-36); no-op when recorder is null
- [x] 1.4 Add tests (`TraceSpanScopeTests` + `RecordEventTests`): scope auto-end `"ok"` on dispose; explicit `scope.End("error", attrs)`; double-end no-op; null-recorder zero-side-effect; event span has `EndTime == null` and `DurationMs == 0`; runtime spanType + runtime parent (method-call) accepted
- [x] 1.5 Record the migration baseline: `dotnet test tests/UniClaw.Core.Tests` + `dotnet test tests/UniClaw.Host.Tests` → record exact pass counts (AC5 anchor); `git stash`-clean oracle file list (AC2 anchor)
- [x] 1.6 Add `SpanTreeEquivalenceTests` (Host.Tests, `RunnerTestHarness` + `InMemoryTraceService`): `SpanTreeSnapshot` canonical dump (spanType | spanName | status | parent | sorted attrs | sibling order, timestamps/durations stripped) frozen from **current pre-migration behavior** for S1 success-run, S2 denied-action, S3 error-loop, S4 AI-failure, S5 parent-chain — this suite is the hard gate for every M1–M4 tier (AC1)
- [x] 1.7 Verify: architecture guard (9 `ITraceRecorder` methods + 2 additive extensions) green; Core + Host baseline green (1176 pass / 9 skip per AC5 recorded counts)

## 2. M1 — `SafetyGate` (SafeActionExecutor)

- [x] 2.1 `WaitAsync`: replace hand-written pair (L352-373) with a scope opened **after** the `decision.Allowed` deny-gate (L345-346) — start attrs `action.type=wait`, end attrs `action.wait_ms=milliseconds` + `action.result=true`; denied runs record no span
- [x] 2.2 `ExecuteAsync`: keep runtime `ActionToSpanType` dispatch (input/long_press → spanType null → no span preserved, L387-388); replace try/finally pair (L391-414) with a scope; `scope.End(success ? "ok" : "error", { action.result=success, action.adb_ms=stopwatch.Elapsed })` in the finally-position
- [x] 2.3 `RecordSkippedAsync`: replace hand-written `StartSpanAsync` (L458-468) with `RecordEventAsync(SpanTypes.EntrySkipped, LatestEntryVisitedSpanId(), { entry.name/entry.rule_id/entry.reason })` — parent stays a runtime method-call expression
- [x] 2.4 Verify (AC1+AC2 gate): `dotnet test tests/UniClaw.Host.Tests --filter "FullyQualifiedName~SafetyGateTests|FullyQualifiedName~SpanTreeEquivalenceTests"` → all green; S2 (denied action, no `action.*` span) snapshot unchanged

## 3. M2 — Analyzer spans

- [x] 3.1 `EnumerateCompletionAnalyzer.EvaluateAsync`: replace the `_traceRecorder is not null` block (L135-159) with a scope (`await using var scope = await _traceRecorder.BeginSpanAsync(SpanTypes.AnalyzeCompletion, "enumerate completion check", null, attributes, ct)`), start attrs = the existing locals-built dictionary, then `scope.End("ok")`; no method extraction, no param plumbing
- [x] 3.2 `ErrorLoopAnalyzer.EmitErrorLoopSpanAsync`: replace the pair (L134-141) with a scope — dynamic spanName `$"error loop: {verdict.Reason}"` and the whole-dictionary `attributes` param pass through directly; null-guard collapses into the no-op scope
- [x] 3.3 Verify (AC1+AC2 gate): `dotnet test tests/UniClaw.Host.Tests --filter "FullyQualifiedName~AnalyzerTests|FullyQualifiedName~SpanTreeEquivalenceTests"` → all green; S3 (error-loop spanName/attrs) snapshot unchanged

## 4. M3 — `CompletionMonitor` + `PageAnalyzer`

- [x] 4.1 `CompletionMonitor.PollOnceAsync`: replace the pair (L157-177) with a scope — dynamic spanType ternary (L152-155) and `"completion poll"` name preserved; end attrs = `finalAttributes` from `DecideActionAsync` (locals — captured by the scope); `scope.End("ok", finalAttributes)` after the `_linkedCts.Cancel()` call
- [x] 4.2 `PageAnalyzer` ai.call: replace the hand-written dual-end pair with a scope (inline — no method extraction per design Q1); catch-path `scope.End("error", { ai.success=false, ... })` and success-path end attrs (provider/model/tokens) preserved
- [x] 4.3 `PageAnalyzer` ai.analyze marker → `RecordEventAsync` with `ai.item_count`/`ai.retry_count` attrs; delete hand-written open
- [x] 4.4 Verify (AC1+AC2 gate): `dotnet test tests/UniClaw.Core.Tests --filter "FullyQualifiedName~PageAnalyzerTests"` + `dotnet test tests/UniClaw.Host.Tests --filter "FullyQualifiedName~CompletionMonitorTests|FullyQualifiedName~SpanTreeEquivalenceTests"` → all green; S4 (`ai.call` error status, `ai.analyze` unclosed) snapshot unchanged

## 5. M4 — `TraversalEngine` + `InterceptionHandler` (stateful spans)

- [x] 5.1 engine.run: 8 conditional close sites → `scope.End(status)` at each terminal branch (same statuses); parentage unchanged; delete hand-written pairs
- [x] 5.2 engine.step: 6 conditional close sites → same mechanism; `_currentEngineStepSpanId` tracking preserved (passthrough stays the seam for entry.generate/entry.visited parent attribution)
- [x] 5.3 entry.generate: 2 close sites → scope or retained passthrough; `entry.match_count`/`entry.ignored_count` end attrs preserved
- [x] 5.4 entry.observed + entry.ignored — **retained on the sync TraceCoordinator passthrough** (deviation from original plan, see design.md M4 line: `IDynamicChildManager.Generate` is sync-guard-frozen so the async `RecordEventAsync` extension cannot be awaited on the emit path; parent = gen span and `entry.name`/`entry.reason`/`entry.node_id` attrs unchanged; span-tree output identical — verified by S1/S5 snapshots; AC3 whitelist exempts TraversalEngine passthrough lines)
- [x] 5.5 `InterceptionHandler` entry.visited → `RecordEventAsync` (parent = `CurrentEngineStepSpanId`, keep `entry.name`/`entry.node_id`/`entry.step`/`entry.depth` attrs)
- [x] 5.6 Verify (AC1+AC2 gate): `dotnet test tests/UniClaw.Core.Tests --filter "FullyQualifiedName~Traversal"` + `SpanTreeEquivalenceTests` → all green; S1 (full success-run tree) and S5 (parent chain: observed/ignored→generate, visited→step, step→run) snapshots unchanged

## 6. M5 — Acceptance verification (final matrix)

- [x] 6.1 AC1 differential snapshots: `SpanTreeEquivalenceTests` all green (S1–S5) — the migration-wide behavior-equivalence gate
- [x] 6.2 AC2 oracle zero-change: `git diff --stat` empty for `TraceSpanTests`/`TraceSpanTreeTests`/`HandlerTraceWriterTests`/`InMemoryTraceRecorderTests`/`ArchitectureGuardTests`/`PageAnalyzerTests`/Traversal 7 文件/`SafetyGateTests`/`ErrorLoopAnalyzerTests`/`EnumerateCompletionAnalyzerTests`/`CompletionMonitorTests`/`BaselineTests`, and all green
- [x] 6.3 AC3 scaffolding zero: `grep -rn 'StartSpanAsync\|EndSpanAsync' src/` hits only in `ITraceRecorderExtensions`, `TraversalEngine` passthrough lines, and `ITraceRecorder`/`InMemoryTraceRecorder` implementations — `SafetyGate`/`PageAnalyzer`/analyzers/`CompletionMonitor`/`InterceptionHandler`/TraversalEngine non-passthrough lines have zero hits
- [x] 6.4 AC4 catalog membership: catalog-membership test green (every recorded spanType a `SpanTypes` member); `SpanType` enum still 11 values (`EnumValueGuardTests`); no new spanTypes introduced
- [x] 6.5 AC5 baseline counts: full `dotnet test tests/UniClaw.Core.Tests` + `dotnet test tests/UniClaw.Host.Tests` pass counts equal to the M0-recorded baseline (no new failures, new tests only added); `UNICLAW_INTEGRATION_SCOPES`-gated emulator tests remain in original skip state
- [x] 6.6 AC6 null-recorder zero-side-effect: `TraceSpanScopeTests`/`RecordEventTests` null-recorder scenarios green; a mock run on a composition without `ITraceRecorder` executes identically with zero spans
- [x] 6.7 Archive-ready: update `openspec/specs/trace-span/spec.md` (helpers + spanType catalog), verify the change's spec deltas match the landing code; all tasks `[x]` → `/opsx:archive`
