## Context

Two parallel traversal paths exist today. Core's `TraversalEngine`/`TraversalFSM` is a full DFS engine — `StepOrchestrator`, `OperationDispatcher`, `DynamicChildManager`, D-74 navigation detection (fingerprint change → sub-frame push), D-90 PressBack/Pop return-home, `ITraceRecorder` writing, and 11 test files — but **never executes on the device path**. Host's `ScenarioRunnerBase` (956 lines) + `IncrementalScenarioRunner` (75) + `EnumerateScenarioRunner` (252) re-implement observe→plan→gate→execute→verify directly against `IPageAnalyzer` + `IActionExecutor`, bypassing the engine. The implementation map (§6.6) names this "the single biggest reason the layer feels out of control": FSM changes have zero effect on device driving.

The `host-target-architecture` change fixed the assembly seams first (C1–C4): `IObservableScreenStateProvider` + `ScreenStateResult` (M1), `UniBrainFactory` (M2/M3), `IEntryPolicyExecutor` injection (M5), single decorated `IActionExecutor` (M7 guard). It recorded **D6** as a scoping decision — "V1 runner self-contained, no `TraversalEngine` dependency." This design is that deferred runner re-architecture: **the engine becomes the sole driver, and Host supplies plan-as-data, verification semantics, the safety decorator, and post-run analysis.**

Constraints:
- **Zero engine change.** The engine, `ITraversalHook`, `ITraceService`, `ITraceRecorder`, `ChildrenStrategy` all stay as-is. The entire seam surface is existing: `TraversalPlan` (carries `StaticNodes` + `ChildrenStrategy`), `ITraversalHook[]` (7 methods already fired by `RunAsync`), `ITraceService` (12 read queries, all implemented in `InMemoryTraceService`).
- The Host 4-layer dependency rule holds: Host → Core one-way; Core knows nothing of Host.
- Existing 930+ tests stay green at every migration step; each step E1–E8 is independently verifiable on the emulator.
- Supersedes D6. The D6 requirement text ("V1 scenario runner is self-contained") lives in the change-local `host-composition-root` spec of `host-target-architecture`; when that change and this one are archived, the requirement is reversed by `scenario-runner`.

Stakeholders: `deliver-safe-android-settings-test-loop` tasks 8/9 (`enumerate_first_level`, stability gates) consume the engine-driven runner; `host-target-architecture` provided the seams this design plugs into.

## Goals / Non-Goals

**Goals:**
- Make `TraversalEngine` the single driver of device traversal; delete the self-contained runner loop (~-1200 lines).
- Support both execution paradigms — plan mode (scripted, `ChildrenStrategy.Static`) and intent mode (dynamic, `ChildrenStrategy.DynamicMatch`) — on the **same** engine skeleton.
- Immediate per-step verification (plan mode) as a hook (`VerifyHook` on `OnAfterStep`) that observes and records without mutating engine state.
- Post-hoc failure analysis via `VerificationAnalyzer` reading `ITraceService` + `SafetyDecisionJournal` → `ScenarioRunOutcome` with step-level error traceback.
- Plans are data: JSON/config → `TraversalPlan`; hand-authored, mock, or trace-derived.
- Keep the safety gate and run-asset pipeline unchanged in behavior, now on the engine path.

**Non-Goals:**
- Engine changes of any kind (no `IVerifier`, no `IChildSelector`, no `VerifyResult.Denied` signal, no entry-policy invocation inside the engine).
- Real-time / streaming verification coupling between analyzer and engine — analysis is strictly post-run.
- AI `verify_page_type` (G1 slice) — plan-mode verification matches page identity/fingerprint, not LLM judgment (see Open Questions Q4).
- `enumerate_first_level`'s specific accounting (skipped entries, end-of-list) as engine logic — it stays a Host post-hoc concern.
- Trace-storage changes (screenshot references in `PageTransition.Metadata`, etc.) — deferred, see Open Questions Q3.

## Decisions

### D1 — One engine, two modes (the core reversal of D6)
**Decision:** Both plan mode and intent mode execute through `TraversalEngine`. Host assembles engine + hooks + analyzer; the runner loop is deleted. The two modes differ only in plan shape and verification semantics — both are Host concerns, neither is engine logic.
**Rationale:** The engine is correctly designed and tested; two traversal paths are the root control problem. `ChildrenStrategy` already distinguishes the modes: `Static` = predefined sequential list with unvisited filter (plan mode); `DynamicMatch` = children generated from `PageAnalysis` via `DynamicChildManager` with DFS + D-74/D-90 (intent mode). Both walk the identical `TraversalFSM` skeleton.
**Alternatives considered:**
- *Keep the self-contained runner (D6 status quo)* — rejected: this is the exact control gap §6.6 names; the active change's tasks 8/9 cannot be honest on a parallel path.

### D2 — No `IChildSelector`; `ChildrenStrategy.Static` covers plan mode
**Decision:** Plan mode maps to `ChildrenStrategy.Static` + `StaticNodes`; no new selector abstraction. The only new work is expressing a plan as a static node tree (or feeding `TraversalPlan.StaticNodes` directly).
**Rationale:** `Static` already means "predefined list, sequential iteration, unvisited filter" — exactly plan-mode semantics. A new `IChildSelector` abstraction would duplicate an existing engine concern.
**Alternatives considered:**
- *Introduce `IChildSelector` so Host can plug plan selection into the engine* — rejected: over-engineering; the engine already distinguishes selection via `ChildrenStrategy.Type`.

### D3 — Immediate verification is a hook, not a verifier
**Decision:** Plan-mode expected-change matching lives in `VerifyHook` on `ITraversalHook.OnAfterStep`. It reads the step's before/after page analysis from the context, matches against the plan JSON's expected change (carried in `TraversalNode.Meta["expected_change"]`), records pass/fail. It **never mutates engine state**; on failure it records the failure and signals Host (via `StopAsync`/`PauseAsync`) only as a Host decision. Intent mode: no-op (engine `ResultVerify` structural fingerprint check + D-74/D-90 already cover navigation/enumerate).
**Rationale:** Verification semantics are mode-specific and belong to Host. Injecting a verifier into `ResultVerify` would either (a) leak Host semantics into Core, or (b) force Core to carry an abstraction with only two Host implementations. The hook is the lower-coupling seam: Core keeps its structural check; Host adds the semantic check.
**Alternatives considered:**
- *`IVerifier` interface replacing `ResultVerify`* — rejected: leaks Host semantics into Core and re-opens the seam surface this design closes.
- *Verify after `RunAsync` only (no hook)* — rejected: immediate verification decides "can the loop continue or must it stop" for plan mode; that decision is needed inside the run, post-hoc alone cannot drive the loop.

### D4 — Post-hoc analysis via `VerificationAnalyzer` on `ITraceService` + journal
**Decision:** After `engine.RunAsync()` completes, `VerificationAnalyzer` reads `ITraceService` (`GetStepTimeline`, `GetBySpanType(SpanType.SkipDangerous)`, `GetPageTransitions`, `ReconstructTree`) and the Host-private `SafetyDecisionJournal`, and produces `ScenarioRunOutcome` (success/failure/incomplete + step-level error traceback). No real-time coupling with the engine. Extension path: `ITraceService` may be inherited by an `IScenarioTraceService` adding scenario-specific queries — Host-side inheritance of a Core read interface, no Core change.
**Rationale:** CQRS is already separated at the interface level (`ITraceRecorder` writes, `ITraceService` reads). Level-3 traceback is already available — `InMemoryTraceService` implements all 12 queries. Post-hoc analysis keeps the engine pure and gives complete hindsight; the user requirement is "追溯错误就够了" (traceback suffices), not real-time.
**Alternatives considered:**
- *Analyzer as a hook running mid-run* — rejected: real-time coupling; the user chose traceable-after-the-fact over streaming.

### D5 — Entry policy before the engine (composition, not engine change)
**Decision:** Host runs `IEntryPolicyExecutor.ExecuteAsync` first, verifies the reset page, then starts `engine.RunAsync()`. The engine loop starts at NodeSelect and never calls `_plan.EntryPolicy` — that stays true. `_plan.EntryApp` remains the fallback root.
**Rationale:** Zero engine change; the reset is a Host lifecycle concern. Prefer Host-side composition for V1; revisit if pause/resume needs entry inside the engine lifecycle (see Open Questions Q1).
**Alternatives considered:**
- *Invoke entry policy from an `OnBeforeRun` hook* — deferred: possible if pause/resume demands it, but V1 keeps the policy outside the engine.

### D6 — Safety gate unchanged, now on the engine path
**Decision:** The engine's `OperationDispatcher` calls `TapAsync`/`SwipeAsync`/`PressBackAsync` through the single `SafeActionExecutor`-decorated `IActionExecutor`. `SafetyContextHook` pushes the per-step `SafetyCandidate` into `SafetyExecutionContext` (AsyncLocal) on `OnBeforeStep`, so `SafeActionExecutor.DecideAsync` sees the real candidate instead of the `"unscoped"` fallback (which denies by default and would block the whole run). Post-hoc classification of denied actions (`blocked`/`skipped`) comes from the journal via `VerificationAnalyzer`.
**Rationale:** The safety decision happens transparently inside the decorator — zero engine change. Known gap: `TraversalFSM.HandleExecuteAsync` ignores the `DispatchAsync` false return (denial surfaces as verify-failure in the FSM and is visible only in journal/trace); V1 accepts this and classifies post-hoc (Open Questions Q2).
**Alternatives considered:**
- *Surface denial as an engine signal (`VerifyResult.Denied`)* — rejected for now: touches `HandleExecuteAsync`, deferred.

### D7 — Run assets via `RunAssetHook`
**Decision:** `RunAssetStore` stays; per-step artifacts are written by `RunAssetHook` on `OnBeforeStep`/`OnAfterStep`. Because `PageAnalysis` carries no screenshot bytes, the hook calls `AdbScreenCapture` itself for step evidence (mirroring the current runner's `WriteBeforeAsync`/`WriteAfterAsync`).
**Rationale:** Asset bookkeeping migrates from the runner loop to a hook; the hook runs inside the engine's lifecycle so every step is captured. The screenshot gap is a known limitation, tracked in Open Questions Q3.

### D8 — Plans are data, provisioned by Host
**Decision:** Plan provisioning produces `TraversalPlan`:
- Plan mode: plan JSON → `TraversalPlan` with `ChildrenStrategy.Static` + `StaticNodes`; each node = `Operation` (click/swipe/back) + `Target` + `Meta["expected_change"]`. Hand-authored or mock-generated.
- Intent mode: existing `ScenarioPlanCompiler` → `TraversalPlan` with `DynamicMatch` (no change).
- Trace-derived (future): a Host analyzer turns a previous run's trace into a static plan — Host analysis output consumed as plan input, no engine involvement.
**Rationale:** "Plans are data, not code" (PRD §3). `TraversalPlan` already carries `StaticNodes`; no plan-compiler variant is needed.

### D9 — Supersede D6 (sequencing note for change-local specs)
**Decision:** This change reverses D6. The `host-composition-root` spec requirement "V1 scenario runner is self-contained" is superseded; the deletion of `ScenarioRunnerBase`/`IncrementalScenarioRunner`/`EnumerateScenarioRunner` lands in this change. When `host-target-architecture` and this change are both archived, the conflicting requirement is dropped in favor of `scenario-runner`.
**Rationale:** D6 was recorded (not silently decided) precisely so this reversal is auditable. The requirement lives in a change-local spec (not canonical), so the supersession is a coordination note, not a canonical delta.

## Risks / Trade-offs

- **[Risk] The engine path has never executed on a real device** — `DynamicMatch` rules on real Settings pages are untested in emulator runs. → Mitigation: E1 assembles the engine and runs a mock emulator run *before* any verification/analysis is added; intent-mode validation (E5) lands only after plan-mode is proven. The 11 engine test files protect engine behavior; the risk is integration, not engine correctness.
- **[Risk] `HandleExecuteAsync` ignores the safety-denial return** — a denied action looks like a verify-failure in the FSM; only the journal/trace reveals the denial. → Mitigation: V1 classifies `blocked`/`skipped` post-hoc in `VerificationAnalyzer`; a future `VerifyResult.Denied` signal is deferred and documented (Open Questions Q2). Stability gates (task 9 of the active change) must be written against the post-hoc classification.
- **[Risk] `RunAssetHook` must call `AdbScreenCapture` itself** — screenshot evidence depends on a second observation call per step. → Mitigation: mirrors the current runner's behavior (no regression); the trace may later carry screenshot references (Open Questions Q3).
- **[Risk] Deleting the runner loop removes the only working device-driving path** — if the engine assembly has a defect, the device path has no fallback. → Mitigation: E1–E6 keep every behavior verifiable on the emulator before E7 deletes the runners; each step is a discrete commit with independent rollback.
- **[Risk] The D6 supersession could be lost in archive sequencing** — if `host-target-architecture` archives before `scenario-runner`'s requirements land, the canonical specs briefly conflict. → Mitigation: this change's spec carries the reversal as a first-class requirement ("engine is the sole driver"); the design records the coordination note explicitly.

## Migration Plan

Order respects dependencies (engine path verified before Host semantics attach; analyzer lands after the engine path is proven). Each step keeps the 930+ tests green and is independently verifiable on the emulator.

| Step | What | Verify |
|------|------|--------|
| **E1** | Host: assemble `TraversalEngine` with the landed seams (decorated executor, `IObservableScreenStateProvider`, hooks array); run entry policy before `RunAsync`; wire `RunAssetHook` | emulator mock run produces a `TraversalResult` + step artifacts; journal shows `unscoped` fallback (SafetyContextHook not yet present) |
| **E2** | Host: add `SafetyContextHook` (per-step candidate push on `OnBeforeStep`) | journal shows real candidates, no `unscoped` fallback |
| **E3** | Host: add `BoundaryHook` (package/page-prefix) | boundary violations recorded, not silently ignored |
| **E4** | Host: add `VerificationAnalyzer` reading `ITraceService` + journal → `ScenarioRunOutcome` | level-2 step traceback on a mock failing run |
| **E5** | Host: plan provisioning for intent mode (existing `ScenarioPlanCompiler`); verify enumerate on emulator | enumerate completes: all entries sampled/skipped + end-of-list |
| **E6** | Host: plan provisioning for plan mode (plan JSON → static node tree); verify locate on emulator | locate finds target; each step's expected change verified |
| **E7** | Host: delete `ScenarioRunnerBase`/`IncrementalScenarioRunner`/`EnumerateScenarioRunner` | no runner loop remains; engine is the only driver |
| **E8** | Host: extend `VerificationAnalyzer` for level-3 traceback (per-step error reconstruction) | level-3 traceback on a failed run |

Dependencies: E1 first (engine path proven). E2/E3 attach to E1's hook array. E4 after E2 (denial classification needs the journal populated with real candidates). E5/E6 after E4 (plan provisioning consumes the same assembly). E7 after E5/E6 prove both modes on the emulator. E8 last (analysis depth).

Rollback: each step is a discrete commit; the runner deletion (E7) is the only irreversible step and lands only after both modes are proven on the emulator, so a defect in any earlier step rolls back without losing the device path.

### Explicit integration ladder

External verification is scope-gated and remains outside the default baseline.
The order is fixed: real-vision screenshot smoke/golden → ADB connectivity/read
→ bounded ADB navigation → vision-selected single navigation →
`scenario-locate` → `scenario-enumerate`. The two
scenario scopes call production `HostCompositionFactory.RunScenarioAsync` and
require a successful `result.json` with non-zero engine/FSM steps. Offline
`EnginePathTests` separately assert that Host composition produces step assets,
safety decisions, analyzer output, and `TraversalFSM` transitions before a real
device is required. Commands and fixture rules live in
`docs/testing/integration-tests.md`.

## Open Questions

- **Q1 — Entry policy placement.** Host-side composition (before `RunAsync`) vs a new `OnBeforeRun` hook invocation. V1 prefers Host-side; revisit if pause/resume needs entry inside the engine lifecycle.
- **Q2 — Safety-denial surfacing.** V1 classifies `blocked`/`skipped` post-hoc from the journal. Is that sufficient for the stability gates (`deliver-safe-android-settings-test-loop` task 9)? A future `VerifyResult.Denied` engine signal touches `HandleExecuteAsync` and is deferred.
- **Q3 — Screenshot evidence.** `PageAnalysis` carries no bytes; `RunAssetHook` calls `AdbScreenCapture` itself. Should a future trace extension carry screenshot references in `PageTransition.Metadata`?
- **Q4 — Plan-mode verification depth.** `VerifyHook` matches page identity/fingerprint against `expected_change`. Is that sufficient, or does plan mode need the AI `verify_page_type` (G1 slice, not yet implemented) before the stability gates are honest?
