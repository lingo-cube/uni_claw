# UniClaw.Host — Runner Through Engine (计划/意图双模式共享 Core FSM)

> Date: 2026-07-30
> Scope: route the Host scenario runner through Core's `TraversalEngine`/`TraversalFSM`; unify plan-mode (scripted) and intent-mode (dynamic) execution on one engine skeleton.
> Status: design draft (docs-first, per user). Not yet an OpenSpec change — to be promoted after review.
> Supersedes: D6 decision in `2026-07-30-host-target-architecture-design.md` §3 ("V1 runner self-contained, no TraversalEngine dependency").
> Companion docs:
> - `2026-07-30-host-target-architecture-design.md` — the seam-fix predecessor (M1-M7); this design is its runner-level downstream.
> - `2026-07-30-host-implementation-map.md` — current Host control gaps (§6.1-6.6); §6.6 is the TraversalEngine relationship this design resolves.
> - `2026-07-29-current-internal-gaps.md` — gap inventory; G1 (UniBrain capabilities), G2 (screen-state bridge) intersect.

## 1. Problem Statement

The current Host scenario runner owns an observe→plan→gate→execute→verify loop **outside** Core's `TraversalEngine`/`TraversalFSM`. This creates two parallel traversal paths:

- `TraversalEngine` (Core) — a full DFS engine with `TraversalFSM`, `StepOrchestrator`, `OperationDispatcher`, `DynamicChildManager`, and trace. It is **correctly designed and fully tested** (11 test files) but **never executes on the device path**.
- `ScenarioRunnerBase` (Host) + `IncrementalScenarioRunner` + `EnumerateScenarioRunner` — a self-contained loop that re-implements observe→plan→verify against `IPageAnalyzer` + `IActionExecutor` directly, **bypassing the engine**.

The implementation map (§6.6) calls this "the single biggest reason the layer feels out of control": the engine exists but is not on the critical path. Changes to the FSM have zero effect on the device-driving path.

The prior design (`host-target-architecture-design.md`) recorded D6 — "V1 runner is self-contained, no TraversalEngine dependency" — as a scoping decision: fix the assembly seams first, defer runner re-architecture. **This design is that deferred re-architecture.**

## 2. Key Insight: Plan Mode ≠ Intent Mode, But Both Use the FSM

Two distinct execution paradigms must coexist. They are not the same thing, and neither is a subset of the other:

```
┌─ Plan Mode (scripted) ──────────────────────────────────┐
│  A plan exists up front. Execute steps in order.        │
│  Each step has an expected change ("click Wi-Fi →       │
│  navigate to Wi-Fi page"). Verify each step.            │
│  Node selection: deterministic, from the plan.          │
└────────────────────────────────────────────────────────┘
┌─ Intent Mode (dynamic) ─────────────────────────────────┐
│  No fixed plan. Each step analyzes the page and picks   │
│  an action by intent/rule. Goal-directed exploration.   │
│  Node selection: dynamic, from page analysis.           │
└────────────────────────────────────────────────────────┘
```

**Both share the same skeleton**: `TraversalFSM` state transitions (NodeSelect→PreconditionCheck→Execute→ResultVerify→Branch→…), `StepOrchestrator` orchestration, `OperationDispatcher` action execution, and trace. The difference is only **which nodes are selected** and **what counts as success** — both are Host concerns, neither is engine logic.

### 2.1 Why `IChildSelector` is NOT needed

The engine already distinguishes selection strategies via `ChildrenStrategy.Type`:

| Strategy | Selection | Mode |
|----------|-----------|------|
| `ChildrenStrategyType.Static` | iterate pre-defined `staticChildren` in order, skip visited | **Plan mode** |
| `ChildrenStrategyType.DynamicMatch` | generate children from `PageAnalysis` via `DynamicChildManager`, DFS + navigation detection + D-90 PressBack/Pop | **Intent mode** |

`ChildrenStrategy.Static` already means "predefined list, sequential iteration, unvisited filter" — exactly the semantics plan mode needs. **A new `IChildSelector` abstraction is over-engineering**: plan mode maps to `Static`, intent mode maps to `DynamicMatch`, both walk the same FSM. The only new work is expressing a plan as a static node tree (or feeding it directly as `TraversalPlan.StaticNodes`).

## 3. Design Principles

1. **One engine, two modes.** Plan and intent are both driven by `TraversalEngine`. Host supplies the plan (data), the verification semantics (hook), the safety gate (decorator), and the post-run analysis (trace reader). The engine does not change between modes.
2. **Plans are data, not code.** A plan is a configuration artifact: JSON describing the static node tree / step list. It may be hand-authored, `mock`-ed for emulator tests, or **derived from a previous run's trace** (Host analysis, not engine logic). No plan-compiler variant is needed — `TraversalPlan` already carries `StaticNodes`.
3. **Immediate verification and post-hoc analysis are different layers.** Immediate verification (per-step, inside the engine) decides "did this step meet its expected change" so the loop can continue or stop correctly. Post-hoc analysis (after the run, outside the engine) reads the full trace to reconstruct *why* a run failed. They are complementary, not alternatives.
4. **Responsibility separation: engine walks, Host judges.**
   - Engine owns: state transitions, action execution, trace writing, `ResultVerify` structural check (fingerprint change / popup / retry).
   - Host owns: what counts as success (expected change matching, end-of-list, stability gates), the safety decision (decorator + journal), run artifacts (screenshots/evidence), and post-run trace analysis.
   - Coupling is one-directional: Host → Core (feeds plan + injects hooks + reads trace). Core knows nothing of Host.
5. **Trace is the read channel for analysis.** `ITraceRecorder` writes; `ITraceService` reads/queries (CQRS at the interface level, already separated). The analysis layer consumes `ITraceService`; it may be extended by inheriting the interface for scenario-specific queries. No real-time coupling between analyzer and engine.

## 4. Target Architecture

```
[Config] plan JSON (static nodes / step list) + scenario JSON + UniBrainConfig + device serial
   │
   ├─[Host] load plan → TraversalPlan (Static: ChildrenStrategy.Static + StaticNodes)
   │                        (Intent: ChildrenStrategy.DynamicMatch via existing PlanCompiler)
   ├─[Host] assemble IUniBrain  (config-driven via UniBrainFactory)
   │
   ├─[Host] assemble non-AI capabilities
   │      ├─ IActionExecutor = SafeActionExecutor(AdbActionExecutor)   ← one decorated executor
   │      ├─ IObservableScreenStateProvider = AdbScreenStateProvider  ← new Core seam (already landed)
   │      ├─ IEntryPolicyExecutor = EntryPolicyExecutor(SafeEntryActionDriver)  ← injected
   │      └─ RunAssets / ITraceRecorder / ITraceService
   │
   ├─[Host] assemble ITraversalHook[]:
   │      ├─ SafetyContextHook   (per-step: push SafetyCandidate to AsyncLocal)
   │      ├─ RunAssetHook        (per-step: screenshot + write step artifacts)
   │      ├─ BoundaryHook        (package / page-prefix boundary check)
   │      └─ VerifyHook          (per-step: expected-change match for plan mode)
   │
   ├─[Host] entry policy executed BEFORE engine (composition, not engine change)
   │
   ├─ new TraversalEngine(plan, brain, screenState, safeActions, config, recorder)
   │      └─ RunAsync() → TraversalResult  (engine walks; no scenario semantics inside)
   │
   └─[Host] post-run: VerificationAnalyzer reads ITraceService + SafetyJournal
            → ScenarioRunOutcome (success/failure + step-level error traceback)
```

**Plan mode** = `TraversalPlan` with `ChildrenStrategy.Static` + `StaticNodes` describing each step. Engine's STATIC path iterates them in order; `VerifyHook` checks each step's expected change.

**Intent mode** = `TraversalPlan` with `ChildrenStrategy.DynamicMatch` (existing `PlanCompiler` output). Engine's DynamicMatch path generates children from page analysis, navigates, and D-90 PressBack/Pop returns home.

**Both modes** share the identical engine skeleton; only the plan shape and the `VerifyHook` semantics differ.

## 5. Component Design

### 5.1 `TraversalEngine` as the single driver (Core — minimal/no change)
- `RunAsync()` already accepts `TraversalPlan` + `IUniBrain` + `IScreenStateProvider` + `IActionExecutor` + hooks. Host injects the decorated executor, the observable screen-state provider, and the hook array.
- **Entry policy is executed by Host before the engine** (composition, not an engine change): `RunAsync`'s loop starts at NodeSelect and never calls `_plan.EntryPolicy`. Host runs `IEntryPolicyExecutor.ExecuteAsync` first, verifies the reset page, then starts the engine.
- **Gap to confirm (open question):** the plan's entry/reset must complete before `RunAsync`. If the reset needs to be inside the engine's lifecycle (for pause/resume correctness), a new hook (`OnBeforeRun` already exists) could host it. Prefer Host-side composition for V1.

### 5.2 Immediate verification — `VerifyHook` (Host, on `ITraversalHook.OnAfterStep`)
- Fires after each `ExecuteStepAsync`. Reads the step's before/after page analysis from `StepContext` (via `ITraversalContext.CurrentPageAnalysis`).
- Plan mode: matches `after` page identity / fingerprint against the step's expected change (from the plan JSON, carried in `TraversalNode.Meta`).
- Intent mode: no-op (engine's `ResultVerify` fingerprint check already covers the navigation case; enumerate's enter→sample→back is already structural via D-74/D-90).
- **Does NOT alter engine state** — it observes and records. If the verification fails, it records the failure and (if needed) signals Host to stop the engine via `StopAsync`/`PauseAsync` — but this is a Host decision, not an engine control.
- **Why not a `IVerifier` interface replacing `ResultVerify`**: verification semantics are mode-specific and belong to Host. Injecting a verifier into `ResultVerify` would either (a) leak Host semantics into Core, or (b) force Core to carry an abstraction with only two Host implementations. The hook is the lower-coupling seam: Core's `ResultVerify` keeps its structural check; Host's hook adds the semantic check.

### 5.3 Post-hoc analysis — `VerificationAnalyzer` (Host, reads `ITraceService`)
- Runs after `engine.RunAsync()` completes. Reads `ITraceService`:
  - `GetStepTimeline(stepNumber)` — per-step before/after page, action, decision → locate/enumerate step verification.
  - `GetBySpanType(SpanType.SkipDangerous)` — which entries were safety-skipped → enumerate skipped accounting.
  - `GetPageTransitions()` — navigation sequence → enter→sample→back verification.
  - `ReconstructTree()` — DFS tree → "all first-level entries visited" check.
- Reads `SafetyDecisionJournal` (Host-private) for denied decisions.
- Produces `ScenarioRunOutcome`: success/failure/incomplete + step-level error traceback (which step failed, why — verification mismatch / safety denial / execution failure).
- **Level-3 error traceback is already available**: `InMemoryTraceService` implements all 12 `ITraceService` queries (`ReconstructTree`, `GetStepTimeline`, `GetBySpanType`, `GetPageTransitions`, `GetAICalls`, …). The analysis layer only needs to consume it.
- **Extension path (later):** `ITraceService` may be inherited by an `IScenarioTraceService` adding scenario-specific queries (e.g., "all entries processed?", "target alias seen?"). This is Host-side inheritance of a Core read interface — no Core change.

### 5.4 Safety gate — unchanged, but now on the engine path (Host)
- `SafeActionExecutor` already decorates `IActionExecutor`; the engine's `OperationDispatcher` calls `TapAsync`/`SwipeAsync`/`PressBackAsync` through it. **Zero engine change** — the safety decision happens transparently inside the decorator.
- `SafetyContextHook` (per-step `OnBeforeStep`) pushes the `SafetyCandidate` into `SafetyExecutionContext` (AsyncLocal), so `SafeActionExecutor.DecideAsync` sees the real candidate instead of the `"unscoped"` fallback (which denies by default and would block the entire run).
- **Gap to confirm (open question):** `HandleExecuteAsync` ignores the `DispatchAsync` return value (`TraversalFSM.cs:174-224`), so a safety-denied action returns `false` → the FSM proceeds as if verified-failed, and the denial is only visible in the journal/trace. For V1, the post-hoc analyzer reads the journal to classify `blocked`/`skipped`. A future change may surface the denial as an engine signal (e.g., `VerifyResult.Denied`), but that touches `HandleExecuteAsync` and is deferred.

### 5.5 Run assets — via `RunAssetHook` (Host)
- `RunAssetStore` stays; per-step artifacts (screenshot, before/after XML, plan, verification) are written by `RunAssetHook` on `OnBeforeStep`/`OnAfterStep`.
- **Gap to confirm (open question):** `PageAnalysis` carries no screenshot bytes (`PageAnalysisRecords.cs:35`), so the hook must call `AdbScreenCapture` itself for step evidence — mirroring the current runner's `WriteBeforeAsync`/`WriteAfterAsync`. A future trace extension could carry screenshot references in `PageTransition.Metadata`.

### 5.6 Plan provisioning (Host)
- Plan mode: plan JSON → `TraversalPlan` with `ChildrenStrategy.Static` + `StaticNodes`. Each node: `Operation` (click/swipe/back), `Target`, `Meta["expected_change"]`. Hand-authored or mock-generated.
- Intent mode: existing `ScenarioPlanCompiler` → `TraversalPlan` with `DynamicMatch` (no change).
- Trace-derived plans (future): a Host analyzer turns a previous run's trace into a static plan for a repeat run. This is Host analysis output consumed as plan input — no engine involvement.

## 6. What Is Deleted

- `ScenarioRunnerBase` (956 lines) — the self-contained loop, template-method base, `LoopControl`, all `Verify*`/`On*` hooks. Its responsibilities migrate: observe→plan→verify becomes engine + `VerifyHook` + post-hoc analyzer; asset bookkeeping becomes `RunAssetHook`; boundary checks become `BoundaryHook`.
- `IncrementalScenarioRunner` (75 lines) — the locate subclass; becomes plan data + `VerifyHook` semantics.
- `EnumerateScenarioRunner` (252 lines) — the enumerate subclass; becomes intent-mode plan + post-hoc analyzer semantics.

Net effect: **~-1200 lines of Host runner code**, replaced by hooks (~200 lines) + analyzer (~200 lines) + plan provisioning (~100 lines).

## 7. Migration Steps

Order respects dependencies: engine hooks exist before Host uses them; Host-side analyzer lands after the engine path is verified on the emulator.

| Step | What | Verify |
|------|------|--------|
| **E1** | Host: assemble `TraversalEngine` with existing seams (decorated executor, observable screen-state, hooks array); run entry policy before engine; wire `RunAssetHook` | emulator mock run produces a `TraversalResult` + step artifacts |
| **E2** | Host: add `SafetyContextHook` (per-step candidate push) | journal shows real candidates, no `unscoped` fallback |
| **E3** | Host: add `BoundaryHook` (package/page-prefix) | boundary violations recorded, not silently ignored |
| **E4** | Host: add `VerificationAnalyzer` reading `ITraceService` + journal → `ScenarioRunOutcome` | level-2 step traceback on a mock failing run |
| **E5** | Host: plan provisioning for intent mode (existing `ScenarioPlanCompiler`); verify enumerate on emulator | enumerate completes: all entries sampled/skipped + end-of-list |
| **E6** | Host: plan provisioning for plan mode (plan JSON → static node tree); verify locate on emulator | locate finds target; each step's expected change verified |
| **E7** | Host: delete `ScenarioRunnerBase`/`IncrementalScenarioRunner`/`EnumerateScenarioRunner` | no runner loop remains; engine is the only driver |
| **E8** | Host: `VerificationAnalyzer` extended for level-3 traceback (per-step error reconstruction) | level-3 traceback on a failed run |

Each step keeps the existing 930+ tests green. The 11 `TraversalEngine` test files already protect the engine's behavior; new tests protect the hooks + analyzer.

## 8. Open Questions

1. **Entry policy placement** — Host-side composition (before `RunAsync`) vs a new `OnBeforeRun` hook invocation. Prefer Host-side for V1; revisit if pause/resume needs entry inside the engine lifecycle.
2. **Safety-denial surfacing** — V1 relies on post-hoc journal reading for `blocked`/`skipped` classification. A future `VerifyResult.Denied` engine signal (touching `HandleExecuteAsync`) is deferred. Is the post-hoc classification sufficient for the stability gates (task 9)?
3. **Screenshot evidence** — `PageAnalysis` carries no bytes; `RunAssetHook` must call `AdbScreenCapture` itself. Should a future trace extension carry screenshot references in `PageTransition.Metadata`?
4. **Plan-mode validation depth** — plan mode's `VerifyHook` matches page identity/fingerprint against expected change. Is that sufficient, or does plan mode need the AI `verify_page_type` (G1 slice, not yet implemented) before the stability gates are honest?

## 9. Relationship to Prior Design

- **Supersedes** D6 in `2026-07-30-host-target-architecture-design.md` §3 ("V1 runner self-contained, no TraversalEngine dependency"). The engine path is now the intended one.
- **Depends on** that design's seams: `IObservableScreenStateProvider` + `ScreenStateResult` (M1, landed), `UniBrainFactory` (M2/M3, landed), `PageAnalysis` shape contract (M4, in progress), `IEntryPolicyExecutor` injection (M5, landed), single decorated executor guard (M7, pending).
- **Does not require** the `IVerifier`/`IChildSelector` abstractions considered and rejected in this design's discussion — `ChildrenStrategy.Static/DynamicMatch` + `ITraversalHook` + `ITraceService` are the entire seam surface.

## Verification Note

Static design based on reading the current code (`TraversalEngine.cs`, `TraversalFSM.cs`, `InterceptionHandler.cs`, `ScenarioRunnerBase.cs`, `EnumerateScenarioRunner.cs`, `ITraceService.cs`, `PageAnalysisRecords.cs`). No `dotnet` execution. The "already landed" seam claims (M1/M3/M5) are verified against `git log` and the presence of `IObservableScreenStateProvider.cs` / `UniBrainFactory.cs` in `src/UniClaw.Core/`.
