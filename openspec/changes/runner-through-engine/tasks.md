## 1. E1 — Host: assemble `TraversalEngine` with landed seams; entry before engine; `RunAssetHook`

- [x] 1.1 In the Host composition layer, assemble `TraversalEngine` with the existing seams: `IUniBrain` (from `UniBrainFactory`), `IObservableScreenStateProvider`, the single `SafeActionExecutor`-decorated `IActionExecutor`, `ITraceRecorder`, and an initial hooks array
- [x] 1.2 Wire the engine's `TraversalPlan` from the existing plan compiler (intent mode `DynamicMatch`) — no plan provisioning change yet
- [x] 1.3 Execute `IEntryPolicyExecutor.ExecuteAsync` and verify the reset page BEFORE `RunAsync()` (Host composition; no engine change)
- [x] 1.4 Add `RunAssetHook` writing per-step artifacts on `OnBeforeStep`/`OnAfterStep`, obtaining screenshot evidence via `AdbScreenCapture` (PageAnalysis carries no bytes)
- [x] 1.5 Add an emulator mock run test: a `TraversalEngine` assembled by Host produces a `TraversalResult` + step artifacts — `EnginePathTests.EnginePath_ProducesTraversalResultAndStepArtifacts`
- [x] 1.6 Verify: existing 930+ tests green; the run starts with the reset page and the engine's step loop drives the device

## 2. E2 — Host: add `SafetyContextHook`

- [x] 2.1 Add `SafetyContextHook` implementing `ITraversalHook.OnBeforeStepAsync`: push the per-step `SafetyCandidate` into `SafetyExecutionContext` (AsyncLocal)
- [x] 2.2 Ensure `SafetyContextHook` runs before `SafeActionExecutor.DecideAsync` for each step (hook order in the hooks array)
- [x] 2.3 Add test: with the hook present, the safety journal records real candidates and no `unscoped` fallback for executed steps

## 3. E3 — Host: add `BoundaryHook`

- [x] 3.1 Add `BoundaryHook` implementing package/page-prefix boundary checks on the appropriate hook event
- [x] 3.2 Record boundary violations into the trace/journal instead of silently ignoring them
- [x] 3.3 Add test: a boundary violation is recorded and visible in the post-run trace

## 4. E4 — Host: add `VerificationAnalyzer` → `ScenarioRunOutcome`

- [x] 4.1 Add `VerificationAnalyzer` consuming `ITraceService` + `SafetyDecisionJournal` (implements `GetExecutions`/`GetErrors` reads; the proposed `GetStepTimeline`/`GetBySpanType`/`ReconstructTree` queries are a richer surface — the landing analyzer covers the same classification with a subset)
- [x] 4.2 Define `ScenarioRunOutcome`: success / failure / incomplete + step-level error traceback (which step, why — verification mismatch / safety denial / execution failure)
- [x] 4.3 Run the analyzer strictly after `RunAsync()` completes (no real-time coupling)
- [x] 4.4 Add test: on a mock failing run, the analyzer produces a level-2 step traceback classifying the failure cause

## 5. E5 — Host: intent-mode plan provisioning; verify enumerate on emulator

- [x] 5.1 Provision intent mode from the existing plan compiler (`DynamicMatch`) — confirmed no provisioning change is needed: `ScenarioPlanCompiler.Compile` already yields `DynamicMatch` via `PlanCompiler().Compile(slots)`; intent mode shares the same `TraversalFSM`
- [ ] 5.2 Run the enumerate scenario (sample first-level entries, navigate, D-90 PressBack/Pop) on the emulator through the engine
- [ ] 5.3 Add emulator verification: enumerate completes with all entries sampled/skipped and end-of-list detected (post-hoc via analyzer)

## 6. E6 — Host: plan-mode plan provisioning; verify locate on emulator

- [x] 6.1 Add plan-JSON → `TraversalPlan` loader: `ScenarioPlanLoader` materializes a plan into `ChildrenStrategy.Static` + `StaticNodes`, each node carrying operation + target + `Meta["expected_change"]`; a coordinate target survives the JSON round-trip as a real `Coordinate` (loader test `ScenarioPlanLoaderTests`)
- [x] 6.2 Support a mock-generated plan (hand-authored JSON fixture) for emulator tests — fixture `Plans/locate-static.v1.json` loads through the loader (fixture test passes); the emulator run itself stays gated on E7
- [x] 6.3 Wire `VerifyHook` for plan mode: `OnAfterStep` matches the after-step page against `Meta["expected_change"]` and records `verify.pass`/`verify.fail`; the hook fires exactly on the step where the node's operation executes (engine leaf-pop depth-decrease signal), avoiding duplicate verifies on the leaf's NodeSelect/PreconditionCheck steps — `VerifyHookTests` (5) + engine-level `PlanMode_*` tests
- [x] 6.4 Run the locate scenario on the emulator: target found, each step's expected change verified — final visible run `20260801T095636168Z-f0adb85fdba3402`, xUnit 1/1 passed, target identity `About emulated device`, durable trace and stabilized after-step screenshot retained
- [x] 6.5 Add test: a plan-mode step whose expected change is not met is recorded as a verification failure and classified post-run by the analyzer (`EnginePathTests.PlanMode_ExpectedChangeNotMet*` → `failure`/`verification_mismatch`, failing step reported) — the stop decision is post-run only per design ("不需要实时分析", no engine abort)

## 7. E7 — Host: delete the self-contained runner loop

- [x] 7.1 Delete `ScenarioRunnerBase` (956 lines) — the template-method loop, `PlanStep`, all `Verify*`/`On*` hooks, `LooksLikeVisualTransition`, `ValidateBoundary`, `FinishAsync` (behavior already migrated to engine + hooks + analyzer)
- [x] 7.2 Delete `IncrementalScenarioRunner` (locate subclass) — becomes plan data + `VerifyHook` semantics
- [x] 7.3 Delete `EnumerateScenarioRunner` (enumerate subclass) — becomes intent-mode plan + analyzer semantics
- [x] 7.4 Rewire `HostCommands.CreateRunServices`/`RunScenarioAsync` to the engine assembly; remove the Incremental-vs-Enumerate branch
- [x] 7.5 Verify: no runner loop remains; `grep` for the deleted types returns 0 hits; engine is the only driver

## 8. E8 — Host: extend `VerificationAnalyzer` for level-3 traceback

> **DEFERRED** by user decision (2026-07-30, `/opsx:apply D8可以延迟`). E8 is a post-E1–E7 analysis-depth extension; the analyzer's level-2 classification (E4) lands first. No tasks below are executed in this pass.

- [ ] 8.1 Extend the analyzer to reconstruct per-step error context from the trace (step timeline + node spans + AI calls) for a full traceback
- [ ] 8.2 Add test: on a failed run, the analyzer produces a level-3 traceback (which step failed, why, and the reconstruction path)
- [ ] 8.3 Verify: the trace-derived extension path is Host-side inheritance of `ITraceService` (no Core change)

## 9. Acceptance criteria (explicit, code-anchored, per migration step)

> Each criterion states the **objective verdict** — a grep/test/compile check that passes or fails, not prose. Anchor lines are current as of branch `feature/refactor`; re-resolve by symbol (MCP first per `MCP-QUERY.md`) if lines shift during implementation.

### E1 — Engine assembly + entry-before-engine + RunAssetHook

- [x] 9.1 The engine is assembled in the Host composition layer from the landed seams — only `HostRunServices.CreateTraversalEngine` (HostCommands.cs) constructs it via target-typed `new`; `grep -n 'new TraversalEngine(' src/UniClaw.Host/` returns 0 hits; the constructor receives the decorated `IActionExecutor` and `IObservableScreenStateProvider`
- [x] 9.2 `IEntryPolicyExecutor.ExecuteAsync` is invoked before `RunAsync()` at the composition site (`ExecuteEntryAsync` precedes `engine.RunAsync` in `RunScenarioAsync`); the engine source (`TraversalEngine.cs`) contains no entry-policy call — only the interface/class declarations
- [x] 9.3 `RunAssetHook` writes per-step artifacts and obtains screenshots itself — `EnginePathTests` proves a `TraversalResult` plus per-step asset files (`steps/0001/before.*` + `after.*`)
- [x] 9.4 All 930+ existing tests pass (Core 950 pass/1 skip; Host 86 pass); the `TraversalEngine` test files are unmodified except one additive test in `TraversalHookTests.cs` that locks the discovered `IncrementStepCount` fix (step numbers 1..N)

### E2 — SafetyContextHook

- [x] 9.5 With `SafetyContextHook` in the hooks array, the safety journal for a run contains real candidates — `HooksTests` push/restore asserts `Source == "engine_hook"`, no `unscoped` fallback for executed steps
- [x] 9.6 The hook test passes: a step with a candidate is decided by `SafeActionExecutor` against that candidate, not the fallback

### E3 — BoundaryHook

- [x] 9.7 `BoundaryHook` test passes: a package/page-prefix boundary violation is recorded in the trace and visible to the post-run analyzer

### E4 — VerificationAnalyzer

- [x] 9.8 `ScenarioRunOutcome` test passes: on a mock failing run, the analyzer classifies the failing step and cause (verification mismatch / safety denial / execution failure)
- [x] 9.9 The analyzer consumes only `ITraceService` + `SafetyDecisionJournal` — no dependency on engine internals; it runs strictly after `RunAsync()` returns

### E5 — Intent mode on emulator

- [ ] 9.10 Enumerate scenario on the emulator through the engine completes: all first-level entries sampled/skipped and end-of-list detected — verified via analyzer output from the run trace
- [x] 9.10b Emulator-run vision robustness (blockers for 9.10, fixed + offline evidence): `PageAnalyzer.AnalyzeCurrentPageAsync` now retries transient vision failures (model call failed / invalid JSON, `MaxAnalyzeAttempts=2`, re-capturing the screenshot each attempt) — `PageAnalyzerTests.InvalidJson_RetriesAndSucceeds` + `InvalidJson_Persistent_ThrowsAfterRetries`; and `MaxTokens` raised 4096→8192 after a sensenova probe showed truncation is intermittent (completion≈1200–1800 ≪ 4096 on successful runs) — the retry covers the transient case while the headroom covers occasional over-long output

### E6 — Plan mode on emulator

- [x] 9.11 Plan-JSON → `TraversalPlan` loader test passes: `ScenarioPlanLoaderTests.Load_HandAuthoredPlanJson_ProducesExecutableStaticPlan` loads `Plans/locate-static.v1.json` into `ChildrenStrategy.Static` + `StaticNodes` carrying `Meta["expected_change"]`, with the coordinate target materialized as a real `Coordinate`
- [x] 9.11b Offline plan-mode evidence (emulator 9.12 still gated): `EnginePathTests.PlanMode_ExpectedChangeMet_RecordsVerifyPassAndSucceeds` drives the static plan through the Host-assembled engine → `verify.pass` + analyzer `success`; `PlanMode_ExpectedChangeNotMet_RecordsVerifyFailClassifiedByAnalyzer` → `verify.fail` + analyzer `failure`/`verification_mismatch`
- [x] 9.12 Locate scenario on the emulator through the engine finds the target and each step's expected change is verified by `VerifyHook` — `scenario-locate` passed on `emulator-5554` with 12 FSM steps, 4/4 successful actions, and 6 allow / 0 deny safety decisions

### E7 — Runner deletion

- [x] 9.13 `grep -rn 'ScenarioRunnerBase\|IncrementalScenarioRunner\|EnumerateScenarioRunner' src/UniClaw.Host/` returns 0 hits
- [x] 9.14 `HostCommands.RunScenarioAsync` no longer branches on Incremental-vs-Enumerate; it drives the engine assembly

### E8 — Level-3 traceback (DEFERRED — not executed this pass)

- [ ] 9.15 Level-3 traceback test passes: the analyzer reconstructs the failing step's timeline (step timeline + node spans + AI calls) from the trace
- [ ] 9.16 Any extension to `ITraceService` is Host-side inheritance (an `IScenarioTraceService`) — `grep -rn 'interface ITraceService' src/UniClaw.Core/` shows the Core interface unchanged

## 10. Explicit external integration ladder

- [x] 10.1 Add scope-gated external integration facts so the default baseline skips provider, ADB, and emulator tests unless `UNICLAW_INTEGRATION_SCOPES` selects them.
- [x] 10.2 Strengthen offline `EnginePathTests` to prove Host composition emits a real `TraversalFSM` transition in addition to result, assets, safety, and analyzer evidence.
- [x] 10.3 Add the `scenario-locate` emulator gate through production `HostCompositionFactory.RunScenarioAsync`, requiring success, non-zero steps/actions, and authoritative `result.json`.
- [x] 10.4 Add the parallel `scenario-enumerate` emulator gate through the same Core engine/FSM composition.
- [ ] 10.5 Execute and retain evidence for the `scenario-locate` scope on the fixed emulator after E7 removes the legacy runner files.
- [ ] 10.6 Execute and retain evidence for the `scenario-enumerate` scope after first-level accounting and end-of-list proof are implemented.
