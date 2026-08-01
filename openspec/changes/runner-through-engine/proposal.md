## Why

The Host scenario runner (`ScenarioRunnerBase` + `IncrementalScenarioRunner` + `EnumerateScenarioRunner`, ~1200 lines) owns a self-contained observe→plan→gate→execute→verify loop that bypasses Core's `TraversalEngine`/`TraversalFSM`. This creates two parallel traversal paths: the engine — correctly designed and fully tested (11 test files) — never executes on the device path, so FSM changes have zero effect on device driving (the implementation map §6.6 calls this "the single biggest reason the layer feels out of control"). D6 in `host-target-architecture` recorded the self-contained runner as a V1 scoping decision; this change is that deferred re-architecture: route the runner through the engine so plan mode (scripted) and intent mode (dynamic) share one Core FSM, with Host supplying plan-as-data, verification hooks, the safety decorator, and post-run trace analysis.

## What Changes

- **Engine becomes the sole driver.** Host assembles a `TraversalEngine` from the landed seams (`UniBrainFactory`-built `IUniBrain`, `IObservableScreenStateProvider`, the single `SafeActionExecutor`-decorated `IActionExecutor`, `ITraceRecorder`, and an `ITraversalHook[]` array) and `RunAsync()` drives scenario execution. Entry policy runs **before** `RunAsync` (Host composition, not an engine change — the engine loop starts at NodeSelect and never calls `_plan.EntryPolicy`).
- **New `ITraversalHook[]` Host implementations** (zero engine change; the interface already exists):
  - `VerifyHook` — plan-mode expected-change matching on `OnAfterStep` (observes and records; never mutates engine state; intent mode is a no-op because the engine's structural `ResultVerify` + D-74/D-90 already cover it).
  - `RunAssetHook` — per-step artifacts (screenshot via `AdbScreenCapture`, before/after page data, plan, verification) on `OnBeforeStep`/`OnAfterStep`.
  - `SafetyContextHook` — pushes the per-step `SafetyCandidate` into `SafetyExecutionContext` (AsyncLocal) so `SafeActionExecutor.DecideAsync` sees the real candidate instead of the `"unscoped"` fallback (which denies by default).
  - `BoundaryHook` — package/page-prefix boundary checks recorded, replacing the runner's `ValidateBoundary`.
- **New `VerificationAnalyzer`** reads `ITraceService` + `SafetyDecisionJournal` after `RunAsync` and produces `ScenarioRunOutcome` (success/failure/incomplete + step-level error traceback). Level-3 traceback is already supported by `InMemoryTraceService`'s 12 queries (`GetStepTimeline`, `GetBySpanType`, `GetPageTransitions`, `ReconstructTree`, …). No real-time coupling between analyzer and engine.
- **Plans are data, not code.** Plan mode = plan JSON → `TraversalPlan` with `ChildrenStrategy.Static` + `StaticNodes` (each node carries `Meta["expected_change"]`); intent mode = existing `PlanCompiler` `DynamicMatch` plan. Plans may be hand-authored, mock-generated for emulator tests, or derived from a previous run's trace (Host analysis output consumed as plan input).
- **BREAKING (Host-only):** Delete `ScenarioRunnerBase` (956 lines), `IncrementalScenarioRunner` (75), `EnumerateScenarioRunner` (252) — net **~-1200 lines** replaced by hooks (~200) + analyzer (~200) + plan provisioning (~100).
- **Supersedes** D6 in `host-target-architecture` design ("V1 runner self-contained, no `TraversalEngine` dependency") — recorded as a sequencing note for the change-local `host-composition-root` spec, whose "V1 scenario runner is self-contained" requirement is reversed by this change.

## Capabilities

### New Capabilities

- `scenario-runner`: Host scenario execution driven through Core's `TraversalEngine` — dual-mode (plan `Static` / intent `DynamicMatch`) on the shared FSM, entry-before-engine composition, hook array (Verify/RunAsset/SafetyContext/Boundary), post-hoc `VerificationAnalyzer` over `ITraceService` + safety journal, plan-as-data provisioning, and deletion of the self-contained runner loop. Engine itself is unchanged.

### Modified Capabilities

- *(none — the engine, the hook interface, and `ITraceService` are all unchanged; this is a Host-side restructuring.)*

## Impact

- **Host** (`src/UniClaw.Host/`): `HostCommands.CreateRunServices`/`RunScenarioAsync` rewired to assemble engine + hooks + analyzer; `Runner/` (`ScenarioRunnerBase`, `IncrementalScenarioRunner`, `EnumerateScenarioRunner`) deleted; new `Hooks/`, `Verification/`, plan-provisioning code. `ScenarioObservation`, `RunAssetStore`, `SafetyGate` retained as-is.
- **Core** (`src/UniClaw.Core/`): zero change — the engine already accepts `TraversalPlan` + hooks; `RunAsync`'s hook firing points (`OnBeforeRun`/`OnBeforeStep`/`OnAfterStep`/`OnError`) already exist. Read-only confirmation only.
- **Device** (`src/UniClaw.Device/`): unchanged (`AdbScreenStateProvider` already implements `IObservableScreenStateProvider`).
- **Dependencies:** requires the landed `host-target-architecture` seams — `IObservableScreenStateProvider` + `ScreenStateResult` (M1), `UniBrainFactory` (M2/M3), `IEntryPolicyExecutor` injection (M5). Supersedes D6's runner decision.
- **Tests:** existing 930+ stay green; the 11 `TraversalEngine` test files protect the engine; new tests cover the hooks + analyzer + plan provisioning. Each migration step E1–E8 is independently verifiable on the emulator.
- **Relationship to active change:** `deliver-safe-android-settings-test-loop` tasks 8/9 (`enumerate_first_level`, stability gates) consume the engine-driven runner; this change is their structural prerequisite.
