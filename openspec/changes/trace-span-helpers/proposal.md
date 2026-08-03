## Why

Phase 1 (`trace-span-observability`, archived 2026-08-02) landed the `TraceSpan` span tree plus 34 hand-written `StartSpanAsync`/`EndSpanAsync` call sites across 7 production files. The scaffolding is invasive: `TraversalEngine` carries 14 conditional close sites, `SafetyGate` a 22-line try/finally executor, and every site re-implements the same start→try→end ceremony with per-site recorder null-guards. The goal is to keep business code clean with small, reusable recording helpers.

A prior proposal built this on a `[TraceSpan]` attribute + Roslyn source generator (`trace-span-source-generator`). An adversarial review of that design against the actual code (2026-08-03) found the attribute's expressive limits — compile-time-constant arguments, and `"key:expr"` expressions resolvable only against method parameters and containing-class fields (a wrapper cannot see method-local variables) — match **only ~1 of the 34 sites**. The remaining spans record attributes computed inside the span region from locals, use runtime-selected spanTypes, or open after a deny-gate. The generator is therefore **deferred to an independent future change** (see design.md "Deferred" section); this change delivers the two helpers that actually cover all 34 sites — `TraceSpanScope` (async-disposable region scope) and `RecordEventAsync` (point-in-time event marker) — then migrates every site.

## What Changes

### Modified Capabilities

#### trace-span

- **ADDED** requirement: `Span 记录方式为可复用助手` — spans are recorded via `TraceSpanScope` (`ITraceRecorder.BeginSpanAsync` extension) or `RecordEventAsync`; Phase-1 hand-written scaffolding in business code is replaced. Emission points, span parentage, and recorded behavior (spanType/attributes/status/timing) remain exactly as specified by the existing `Phase 1 引擎埋点` requirement — only the recording mechanism changes.

## Impact

- **`ITraceRecorder`**: two additive extension methods (`BeginSpanAsync`, `RecordEventAsync`) — the 9-method interface contract is untouched; the architecture guard stays green (9 methods + 2 extensions).
- **`UniClaw.Core`**: new `TraceSpanScope` type + `ITraceRecorderExtensions` (one new file).
- **Migrated files (7 production files, 34 call sites)**: `SafetyGate.cs` (wait/execute scopes + `entry.skipped` event), `ErrorLoopAnalyzer.cs` + `EnumerateCompletionAnalyzer.cs` (analyzer span scopes), `CompletionMonitor.cs` (poll span scope), `PageAnalyzer.cs` (`ai.call` scope + `ai.analyze` event), `TraversalEngine.cs` (engine.run/step/generate scopes + `entry.observed`/`entry.ignored` events), `InterceptionHandler.cs` (`entry.visited` event).
- **Tests**: new `TraceSpanScopeTests` + `RecordEventTests`; existing `TraceSpanTests`/`TraceSpanTreeTests`/`HandlerTraceWriterTests`/`SafetyGateTests`/`PageAnalyzerTests`/analyzer tests/11 `TraversalEngine` test files pass unchanged (behavior-equivalence oracle).
- **Deferred**: `[TraceSpan]` attribute + `TraceSpanGenerator` + `SourceGen.Tests` → independent future change (see design.md "Deferred" section for the verified constraints).
