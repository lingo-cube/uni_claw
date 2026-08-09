# LEGACY_SOURCE_INVENTORY_RESULT — Step 1

> Generated: 2026-08-09
> Inspection Mode: READ_ONLY_GIT_OBJECTS
> Source Branch: `feature/refactor`

---

## Legacy Branch

`feature/refactor`

Inspection Mode:
READ_ONLY_GIT_OBJECTS

---

## Taxonomy Documents

### PRIMARY — Canonical classification documents

#### 1. `docs/testing/test-tiers.md`
- **Relevant Sections:**全文 — "Test tiers: Legacy Harness, Simulation, and Explicit Integration"
- **Defines:** LEGACY_HARNESS / SIMULATION / INTEGRATION
- **Referenced Sources:**
  - Legacy Harness: `tests/UniClaw.Host.Tests/Runner/RunnerTestHarness.cs` + `FakeObservationSource`, `FakeActionExecutor`, `FakeEntryDriver`, `FakeAdbRunner`, `FakeScreenState`, `UnusedPageAnalyzer`, `UnusedBrain`
  - Simulation: `tests/UniClaw.Core.Tests/Simulation/` — `SimulationE2ETests.cs`, `StatefulMockVisionTests.cs`, `StatefulMockActionTests.cs`, `StateFixtureTests.cs`, `Scroll/PagedContentAndScreenTests.cs`, `ExpectedBehavior/...`
  - Integration: `*IntegrationTests` + `docs/testing/integration-tests.md`
- **Notes:** Canonical anchor. Explicit boundary rules: "Do not try to inject 'back landed on the wrong page' into the stateful Simulation... and do not try to verify engine traversal via the stateless Harness."

#### 2. `docs/testing/integration-tests.md`
- **Relevant Sections:**全文 — 8-level progressive scope ladder (Chinese)
- **Defines:** INTEGRATION
- **Referenced Sources:**
  - Vision: `tests/UniClaw.Core.Tests/UniBrain/RealVisionIntegrationTests.cs`, `VisionGoldenIntegrationTests.cs`
  - ADB: `tests/UniClaw.Host.Tests/Device/AdbRealDeviceIntegrationTests.cs`, `AdbSessionIntegrationTests.cs`
  - Vision+ADB: `tests/UniClaw.Host.Tests/Integration/AdbVisionActionIntegrationTests.cs`
  - Scenario: `tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs`
  - Fixtures: `tests/UniClaw.Core.Tests/Fixtures/Screenshots/`, `*.expected.json` goldens
  - Emulator: `scripts/android-emulator.sh`, `UNICLAW_ADB_SERIAL=emulator-5554`
  - Outputs: `artifacts/runs/integration/adb-*/`, `scenario-locate/`, `scenario-enumerate/`
- **Notes:** Default baseline filter: `--filter "Category!=Integration"`; scope-gated via `UNICLAW_INTEGRATION_SCOPES` env.

#### 3. `docs/superpowers/specs/2026-08-02-baseline-simulation-design.md`
- **Relevant Sections:**全文 — "离线基线驱动仿真测试设计" (Offline baseline-driven simulation test)
- **Defines:** SIMULATION_REPLAY (baseline-driven simulation)
- **Referenced Sources:**
  - `tests/UniClaw.Core.Tests/Simulation/BaselineRecord.cs`, `BaselineSimulationProfile.cs`, `BaselineRegressionTests.cs`
  - `src/UniClaw.Host/Analysis/BaselineBuilder.cs`
  - `artifacts/baselines/<scenarioId>.jsonl` (min 10 records, only successful runs)
- **Notes:** Data loop: 线上 run → trace spans → BaselineBuilder → baseline.jsonl → BaselineSimulationProfile → FsmSimulationHarness → CI 回归测试. Thresholds: visited ≥ 80% p50, steps ≤ 120% p95.

#### 4. `docs/system/layers/simulation-baseline.md`
- **Relevant Sections:** "三类测试的区分" table, §1–§4
- **Defines:** SIMULATION (Architecture/ + Baseline/ + Simulation/ sub-tiers)
- **Referenced Sources:**
  - `SimulationBaselineTests.cs` (2 scenarios + inline 7-page Settings fixture)
  - `ScrollableBaselineTests.cs` (6 scroll scenarios)
  - `ArchitectureGuardTests.cs`
  - `SimulationE2ETests.cs`
  - Expected JSON: `tests/.../Baseline/Fixtures/expected/*.json`, `scroll/*.json`
  - C-11 CI-blocking constitution rule
- **Notes:** Python↔C# asset mapping: `expected_behavior.yaml` ↔ `simulation-baseline.md`; `test_settings_simulation.py` ↔ full-traversal; `test_target_search.py` ↔ target-search; `settings_page.json` ↔ `SettingsAppFixture7Pages()`.

#### 5. `docs/system/layers/simulation.md`
- **Relevant Sections:** Type inventory, test coverage table
- **Defines:** SIMULATION
- **Referenced Sources:**
  - `StateFixture`/`PageState`/`PageElement`/`PageTransition`
  - `StatefulMockVisionService` (`IVisionProvider`)
  - `StatefulMockActionExecutor` (`IActionExecutor`)
  - Tests: `StateFixtureTests` (6), `StatefulMockVisionTests` (11), `StatefulMockActionTests` (5), `SimulationE2ETests` (2)
- **Notes:** `SimulationRunner`/`SimulationConfig`/`SimulationResult` deleted (logic migrated to `TraversalEngine`).

### SECONDARY — Supporting taxonomy / design documents

#### 6. `docs/refactor/13-phase2-sim-design.md`
- §8.1 "三层测试金字塔" (three-tier simulation test pyramid): E2E 遍历 (2-4) / 联动测试 (3-5) / 单元测试 (8-10)
- §7.4 dependency table: production code + mocks

#### 7. `.claude/skills/trace-to-simulation/SKILL.md`
- **Defines:** SIMULATION_REPLAY workflow
- Inputs: `result.json`, `plan.json`, `criteria.json`, `trace/{runId}/trace.jsonl`, `run.log`, `assets/{runId}/analysis.jsonl`
- Outputs: `tests/UniClaw.Core.Tests/Simulation/TraceReplay/{runIdShort}_{scenarioSlug}.cs`

#### 8. `.claude/agents/shadow-fsm-analyzer.md`
- S2 test inference map: StateMachine tests (`FSMIntegrationTests.cs` — 全周期集成测试, `FsmSimulationRegressionTests.cs`), Simulation tests (`TraceReplayHarness.cs` — 仿真回放机制, `FixVerificationTests.cs`), Host integration tests (`EmulatorScenarioIntegrationTests.cs` — 端到端场景)

#### 9. `.claude/skills/host-test-runner/SKILL.md`
- **Defines:** INTEGRATION (execution orchestration)
- Phase 0-6: emulator boot → build → config → execute → monitor → post-analysis → visualization

#### 10. `docs/refactor/2026-07-16-simulation-test-quality-hardening-design.md`
- SIMULATION. "实际 / 应该 / 证明" triplet hardening. References `HierarchyBaselineTests.cs`, `ExpectedBehavior.cs`, `PagedItemGenerator.cs`, `SimulatedScreen.cs`.

#### 11. `docs/refactor/scrollable-baseline-test-design.md`
- SIMULATION. `ScrollableBaselineTests.cs` (6 scenarios), WiFi list 7 screens / 25 elements.

#### 12. `docs/refactor/17-simulation-baseline-tests.md`
- SIMULATION. `SimulationBaselineTests.cs` HOW guide. Python anchor: 118 steps/19 nodes.

#### 13. `docs/refactor/18-expected-behavior-design.md`
- SIMULATION. Three-tier verification chain (Python data → expected → compare; C# `ExpectedBehavior` + JSON).

#### 14. `docs/prd/2026-07-29-local-implementation-gap-prd.md`
- FR-8 three-layer verification + CI tiering. SIMULATION baseline as Core regression main gate.

#### 15. `docs/testing/integration-config.md`
- INTEGRATION. `integration.config.json` schema, 7 config layers (L0-L6), three-layer validation (D-207).

#### 16. `docs/conventions/observation-conventions.md`
- §"集成测试" references `RealVisionIntegrationTests.cs` (default Skip).

#### 17. `docs/testing/integration-pipeline-issues.md`
- INTEGRATION. Pipeline issue list. Links `integration-tests.md` and `test-tiers.md`.

#### 18. `docs/prd/2026-07-13-advanced-simulation-baseline.md`
- SIMULATION. `HierarchyBaselineTests.cs` (4 scenarios) + `LongListBaselineTests.cs` (3 scenarios).

#### 19. `docs/prd/2026-07-12-baseline-test-reporting.md`
- SIMULATION. `BaselineReportCollector`/`BaselineReportWriter`.

#### 20. `docs/validation/unit_test_status.md`
- Validation reports. Baseline accounting (1240/1240, 14 skipped emulator-gated + 既有 skip).

#### 21. `AGENTS.md` (root)
- Doc-routing table: Simulation 层 → constitution + layers/simulation.md + layers/simulation-baseline.md; Guard Tests = `ArchitectureGuardTests.cs`.

---

## Integration Test Inventory

**Count:** 11 items (6 externally-gated + 5 in-process with integration naming)

### Externally-gated (require emulator / provider / device)

| Source ID | Path | Test/Class Name | Short Purpose | Executable | Fixture | External Evidence | Failure Coverage |
|---|---|---|---|---|---|---|---|
| IT-01 | `tests/UniClaw.Core.Tests/UniBrain/RealVisionIntegrationTests.cs` | `RealVisionIntegrationTests.AnalyzeScreenshot_WithSensenovaVision_ReturnsPageAnalysis` | Real AI vision smoke via sensenova | YES (scope `vision-smoke`) | `FileScreenCapture`, `VisionTestSecrets`, fixture screenshot | YES — writes `<screenshot>.analysis.json` | NO |
| IT-02 | `tests/UniClaw.Core.Tests/UniBrain/VisionGoldenIntegrationTests.cs` | `VisionGoldenIntegrationTests.AnalyzeSingleImage_MatchesExpectedGolden` | Vision golden comparison against human-reviewed `.expected.json` | YES (scope `vision-golden`) | `VisionGoldenComparer`, `VisionTestSecrets`, `FileScreenCapture` | YES — writes `<image>.actual.json` | YES — diff failures, missing expected |
| IT-03 | `tests/UniClaw.Host.Tests/Device/AdbRealDeviceIntegrationTests.cs` | `AdbRealDeviceIntegrationTests` (3 tests: `Devices_ResolvesOnlineSerial`, `Screencap_ReturnsDecodablePng`, `ScreencapRaw_ReturnsValidRgbaBuffer`) | Minimal ADB device boundary | YES (scopes `adb-connectivity`, `adb-read`) | `AdbTestContext`, `ProcessAdbSession`, `AdbScreenCapture` | YES — `serial.txt`, `screenshot.png`, `screenshot-raw.jpg` | PARTIAL — PNG magic-byte asserts |
| IT-04 | `tests/UniClaw.Host.Tests/Device/AdbSessionIntegrationTests.cs` | `AdbSessionIntegrationTests` (4 tests incl. `SelfHealing_AfterKillServer_AutoRecovers`) | ADB session behavioral equivalence + self-healing | YES (scope `adb-session`) | `AdbTestContext`, raw `adb` CLI | NO artifacts written | YES — deliberately kills adb server mid-test |
| IT-05 | `tests/UniClaw.Host.Tests/Integration/AdbVisionActionIntegrationTests.cs` | `AdbVisionActionIntegrationTests.VisionLocatesSafeSettingsRow_AdbNavigatesAndRestores` | Vision+ADB minimal closed loop | YES (scope `adb-vision-action`) | `AdbTestContext`, `HostCompositionFactory` | PARTIAL — launches Settings, no artifact files | PARTIAL — safe navigation whitelist |
| IT-06 | `tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs` | `EmulatorScenarioIntegrationTests` (2 tests: `LocateOneItem_ThroughCoreEngine_Completes`, `EnumerateSettings_ThroughCoreEngine_Completes`) | Full-stack Android Settings via production `HostCompositionFactory.RunScenarioAsync` | YES (scopes `scenario-locate`, `scenario-enumerate`) | `AdbTestContext`, `IntegrationConfigLoader`, `ProviderPreflight`, scenario JSONs | YES — full run output under `artifacts/runs/integration/` incl. `result.json` | YES — status, steps>0, actions>0, VerifyEngine verdict |

### In-process (integration-named, default baseline, no external world)

| Source ID | Path | Test/Class Name | Short Purpose | Notes |
|---|---|---|---|---|
| IT-07 | `tests/UniClaw.Core.Tests/StateMachine/FSMIntegrationTests.cs` | `FSMIntegrationTests` (5 tests) | In-memory full TraversalFSM cycle with mocked vision/snapshot/trace | Named "Integration" for cross-component FSM coverage, not external |
| IT-08 | `tests/UniClaw.Core.Tests/Observability/TraceSpanScopeIntegrationTests.cs` | `TraceSpanScopeIntegrationTests` (3 tests) | TraceSpanScope + EngineStepSpanContext push/pop | In-memory recorder |
| IT-09 | `tests/UniClaw.Host.Tests/Logging/LoggingIntegrationTests.cs` | `LoggingIntegrationTests` (4 tests) | Host logging assembly (dual-sink, run-context, exception-path flush) | Doc: "无设备集成测试" (device-free) |
| IT-10 | `tests/UniClaw.Host.Tests/Integration/IntegrationConfigTests.cs` | `IntegrationConfigTests` (17 tests) | IntegrationConfigLoader unit tests + fail-fast validation | **Config/test mismatch found**: `DefaultConfig_LoggingSection_ParsesToWarning` expects `"warning"` but committed `integration.config.json` has `"information"` |
| IT-11 | `tests/UniClaw.Host.Tests/Integration/ProviderPreflightTests.cs` | `ProviderPreflightTests` (6 tests) | ProviderPreflight runtime-prerequisite checks | Primary purpose is negative fail-fast coverage |

### Supporting infrastructure (integration)

- `IntegrationFactAttribute` + `IntegrationTestScopes` — dual implementations in Core and Host test projects
- `AdbTestContext` — serial resolution, ADB session/screencap/action executor assembly
- `IntegrationConfigLoader`/`IntegrationConfig` + `integration.config.json` — L0-L6 config layers
- `ProviderPreflight` — per-provider runtime precondition checks
- Vision fixtures: `VisionGoldenComparer`, `VisionTestSecrets`, `FileScreenCapture`
- Asset fixtures: `tests/UniClaw.Core.Tests/Fixtures/Screenshots/` (1 real-device screenshot + `.expected.json` + `.local-vision.*`)
- Scenario assets: `scenarios/android-settings/locate-one-item.v1.json`, `enumerate-settings-safely.v1.json`
- Emulator tooling: `scripts/android-emulator.sh` (AVD `uniclaw-lite-api35`)

### Notable observations

1. **Stale scope `adb-action`**: Defined in `IntegrationTestScopes` but zero consumers on the branch.
2. **Config/test mismatch**: IT-10 `DefaultConfig_LoggingSection_ParsesToWarning` likely fails against committed `integration.config.json`.
3. **EmulatorScenarioIntegrationTests** is the deepest external-world test — full production composition + TraceTool post-hoc verification.
4. Python `tools/local_vision/tests/` tests are in-process (FastAPI `TestClient`), not external integration.

---

## Simulation Test Inventory

**Count:** 12 suites, ~90+ individual tests

### Core simulation suites

| Source ID | Path | Simulation/Test Name | Simulated World / Environment | Input Fixture | Disturbance | Failure Cases | Deterministic |
|---|---|---|---|---|---|---|---|
| SIM-01 | `tests/UniClaw.Core.Tests/Simulation/SimulationE2ETests.cs` | `SimulationE2ETests` (7 tests) | `StateFixture` (2-page / 4-page Settings) + `StatefulMockVisionService` + `StatefulMockActionExecutor` + real `TraversalEngine`/`TraversalFSM` | `StateFixtureBuilder` code-built + hand-built `TraversalNode` trees | NO (only `MaxSteps` variation) | YES (`Runner_MaxStepsExceeded`, `EmptyAreaTap_ReturnsResultVerify`) | YES |
| SIM-02 | `tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs` | `SimulationBaselineTests` (2 tests: `SettingsApp_FullTraversal_AllVisited`, `SettingsApp_TargetSearch_StopsAtDarkMode`) | 7-page Settings `StateFixture` + DynamicMatch root | Code-built fixture + JSON `ExpectedBehavior` contracts | NO | YES (indirect — contract assertions: forbidden pages, numeric anchors) | YES |
| SIM-03 | `tests/UniClaw.Core.Tests/Simulation/StateFixtureTests.cs` | `StateFixtureTests` (6 tests) | Pure `StateFixture` model (no engine) | Inline JSON + builder | NO | YES (miss/not-found) | YES |
| SIM-04 | `tests/UniClaw.Core.Tests/Simulation/StatefulMockActionTests.cs` | `StatefulMockActionTests` (5 tests) | 2-page fixture via `StatefulMockVisionService`/`StatefulMockActionExecutor` | `StateFixture` | NO | YES (empty-area tap, empty back stack) | YES |
| SIM-05 | `tests/UniClaw.Core.Tests/Simulation/StatefulMockVisionTests.cs` | `StatefulMockVisionTests` (12 tests) | 2-page fixture; vision-side observation model | `StateFixture` | NO | YES (unknown element, empty stack, tolerance miss) | YES |
| SIM-06 | `tests/UniClaw.Core.Tests/Simulation/MockModelProviderTests.cs` | `MockModelProviderTests` (9 tests) | Simulated AI transport (`MockModelProvider`) | `MockModelFixture` code + `Fixtures/parse_instruction.mock.json` | NO | YES (missing preset → `DomainValidationException`) | YES |
| SIM-07 | `tests/UniClaw.Core.Tests/Simulation/Scroll/PagedContentAndScreenTests.cs` | `PagedContentAndScreenTests` (12 tests) | `SimulatedScreen` + `PagedItemGenerator` + `ScrollableMockVisionService`/`ScrollableMockActionExecutor` | Code-built page shell + generator configs | YES (sparse fill, overshoot jumps) | YES (invalid-arg fail-fast, skip-page) | YES |
| SIM-08 | `tests/UniClaw.Core.Tests/Simulation/ExpectedBehavior/ExpectedBehaviorElementCoverageTests.cs` | `ExpectedBehaviorElementCoverageTests` (8 tests) | None — synthetic `ExpectedBehavior` + `TraversalResult` | In-code expectation + action history | NO | YES (6/8 are FAIL-path tests) | YES |

### FSM simulation / regression suites

| Source ID | Path | Simulation/Test Name | Simulated World / Environment | Input Fixture | Disturbance | Failure Cases | Deterministic |
|---|---|---|---|---|---|---|---|
| SIM-09 | `tests/UniClaw.Core.Tests/StateMachine/FsmSimulationRegressionTests.cs` | `FsmSimulationRegressionTests` (7 tests) | No world fixture — direct FSM simulation via `FsmSimulationHarness` | Synthetic runtime context + `PageAnalysis` helpers | YES — fault injection (forced error strategies, failing precondition checkers) | YES (error-handling gate, consecutive-error accumulation, popup retry, AI empty response) | YES |
| SIM-10 | `tests/UniClaw.Core.Tests/UniBrain/AIIntentSimulationTests.cs` | `AIIntentSimulationTests` (9 tests) | 6-page Settings `StateFixture` + `StubHttpHandler` canned DeepSeek JSON | Natural-language intent → stub AI → real `PlanCompiler` → real `TraversalEngine` | NO (except live Sensenova opt-in tests) | YES (early-stop/target-found, deep nav limits) | YES (stub); UNKNOWN (`LiveSensenova_*`) |

### Baseline scroll suites

| Source ID | Path | Simulation/Test Name | Simulated World | Disturbance | Failure Cases | Deterministic |
|---|---|---|---|---|---|---|
| SIM-11 | `tests/UniClaw.Core.Tests/Baseline/HierarchyBaselineTests.cs` | `HierarchyBaselineTests` (4 tests) | `StateFixture` from JSON + `SimulatedScreen` (3 scrollable pages: 25/30/20 items) + `ScrollableMock*` | YES (partial — scroll profiles) | YES | YES |
| SIM-12 | `tests/UniClaw.Core.Tests/Baseline/LongListBaselineTests.cs` | `LongListBaselineTests` (4 tests) | Single-page shells + `PagedItemGenerator` (30/25/20 items, fillRatio 1.0/0.5) | YES (sparse/dense/jump) | YES | YES |
| SIM-13 | `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs` | `ScrollableBaselineTests` (7 tests) | `WiFiListFixture7Screens` + generator content (24 networks/pageSize 4, sparse 8/2, overlapping 17/5) | YES (sparse/dense/overlap) | YES | YES |
| SIM-14 | `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs` | `MultiBranchNavigationTests` | Hub→listA/listB multi-branch (16-item scrollable branches) | YES (missing-back-button bug) | YES (documents unfixed BUG: "0/16 but AllVisited") | YES |

### Simulation ownership model

- **External world state**: `StateFixture`/`StateFixtureBuilder`, `SimulatedScreen` + `PagedItemGenerator`, `MockModelFixture`
- **Observations**: `StatefulMockVisionService` / `ScrollableMockVisionService` (IPageAnalyzer), `MockScreenStateProvider`
- **Actions**: `StatefulMockActionExecutor` / `ScrollableMockActionExecutor` (IActionExecutor)
- **Expected semantic conclusions**: `ExpectedBehavior` (7 rule classes: `CompletionExpectation`, `PageCoverageExpectation`, `ElementCoverageExpectation`, `CollisionProof`, `DfsPropertiesExpectation`, `OperationRulesExpectation`, `TraceIntegrityExpectation`, `NumericAnchor`)
- **Replay**: `TraceReplayHarness` + `TraceReplayVisionService`/`TraceReplayActionExecutor`

### Key findings

- No Python/TS/JS simulation tests exist — entirely C#
- Disturbance support: NO dedicated framework; disturbances exist as (a) FSM harness error-strategy forcing, (b) adversarial fixtures from real bug traces, (c) scroll content profiles
- Determinism: fixture-driven tests are deterministic; only UNKNOWN areas are artifact-dependent replay tests and opt-in live-API tests
- Failure cases: present in every suite except pure model tests

---

## Simulation Replay Inventory

**Count:** 4 replay families

### R1 — TraceReplay Harness (record-then-replay simulation)

- **Path:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/` (6 files)
- **Replay Tests/Classes:**
  - `TraceReplayFromRunTests` (4 tests): `Step1_AutoDiscoverAndReplay`, `ExportReplayForViewer`, `Step2_Diagnose_DepthRunaway`, `Step3_FixVerify_RestoreConstrainsDepth`
  - `FixVerificationTests` (9 tests): L1 replay regression, L2-L8 fixture-based FSM tests
  - `TraceReplay_20260805T052309367Z_Enumerate` (3 tests): DFS revisit-loop, search-box type=input skip, search-box misclassified → stuck
  - `SettingsEnumerateRegression` (1 test): Depth constraint on API 35 Settings
  - Harness: `TraceReplayHarness`, `TraceReplayVisionService : IPageAnalyzer`, `TraceReplayActionExecutor : IActionExecutor`
- **Replay Fixture Path:** Auto-discovery of `artifacts/runs/{scenario}/{runId}/` (NOT committed): `plan.json` + `result.json` + `assets/{runId}/analysis.jsonl` + `trace/{runId}/run.log`. Committed export: `artifacts/sim-replay/trace-replay-export.json`
- **Trace/Data Format:** JSONL `PageAnalysis` frames; `plan.json` (serialized `TraversalPlan`); `result.json` (runId, completionReason, actionsAttempted); `run.log` text parsed by regex
- **Recorded Inputs:** traversal plan (compiled or from plan.json); recorded vision frames
- **Recorded Observations:** `PageAnalysis` frames (frame index → page elements)
- **Recorded Actions:** real action sequence from `run.log` regex, or engine decisions from no-I/O `TraceReplayActionExecutor`
- **Recorded Outcomes:** `result.json` completionReason/actionsAttempted; FSM `TraceRecord` steps
- **Failure Replay Present:** YES — run `20260805T052309367Z` (max_steps loop) replayed; references `20260806T072558649Z` (subtitle double-click, Flash misclick, PressBack-exits-desktop)
- **Source Appears:** Mixed — harness replays real artifacts; run-derived `StateFixture` factories are hand-reconstructed synthetic pages
- **Notes:** Record-then-replay: integration test fails → artifacts in `artifacts/runs/` → replay <1s no emulator → fix → re-replay → emulator E2E

### R2 — TraceTool Run Replay (trace.jsonl → InMemory service)

- **Path:** `src/UniClaw.TraceTool/TraceRunLoader.cs`; tests in `tests/UniClaw.TraceTool.Tests/`
- **Replay Tests/Classes:** `TraceRunTests` (via `TraceRunFixture`), `DiagnoseTests`, `CliTests`, `JsonContractTests`
- **Replay Fixture Path:** COMMITTED — `tests/UniClaw.TraceTool.Tests/Fixtures/success/` and `Fixtures/failure/` (each: `manifest.json`, `result.json`, `trace/{runId}/trace.jsonl`)
- **Trace/Data Format:** JSONL trace: `record_type` = execution/state_transition/span/ai_call/page_transition/error; span tree with spanId/spanType
- **Recorded Inputs:** manifest.json, result.json, step assets dir
- **Recorded Observations:** span records incl. `ai.call` with `ai.capability=analyze_visual`, `ai.mode=vision`, tokens, latency
- **Recorded Actions:** `execution` records (`safety.launch`, `safety.wait`, `step_start`, …)
- **Recorded Outcomes:** `result.json` status, completion reason; `issues.jsonl`
- **Failure Replay Present:** YES — `Fixtures/failure` snapshot of `20260803T131333575Z`
- **Source Appears:** Real — committed snapshots of actual runs
- **Notes:** Read-only offline analysis replay, not engine re-execution. Consumers: `DiagnoseEngine`, `RunDiffer`, `RunEvidenceLoader`.

### R3 — Simulation Replay Viewer (visualization tooling)

- **Path:** `scripts/sim-replay-viewer.py`; `artifacts/sim-replay/trace-replay-export.json`
- **Trace/Data Format:** JSON `schemaVersion:1`, `sourceMode: fixture|trace`, optional `fixture`, `actionHistory[]`, `visitedPages[]`, `trace[]`, `analysisFrameCount`
- **Source Appears:** Real (export of real run artifacts)
- **Notes:** HTML viewer (phone frame + screenshot base + click flash + timeline). Wired in `.claude/settings.json` for interactive debugging.

### R4 — MockModelProvider Fixture-Driven Replay (UniBrain model-level)

- **Path:** `src/UniClaw.Core/Simulation/MockModelProvider.cs`; tests `MockModelProviderTests.cs`, `UniBrainFactoryTests.cs`
- **Data:** `MockModelFixture` preset table (capability → `MockModelEntry`)
- **Failure Replay:** NO
- **Source Appears:** Synthetic (mock presets)
- **Notes:** Mock/replay at model layer, not trace replay.

### SIMULATION_REPLAY vs SIMULATION_TEST boundary

- **SIMULATION_REPLAY** (record-then-replay of real runs): R1 TraceReplay tests, R2 TraceTool tests, R3 viewer data
- **SIMULATION_TEST** (synthetic fixture-driven, no recorded data): All SIM-01 through SIM-14 suites, plus `FixVerificationTests` L2-L8, `SettingsEnumerateRegression`

---

## Failure / Regression Supporting Evidence

**Count:** 23 discrete items across tests, production code, and documentation

### Test-based regression evidence

| Ref | Path | Class/Test | Relates to |
|---|---|---|---|
| F-01 | `tests/UniClaw.Core.Tests/StateMachine/FsmSimulationRegressionTests.cs` | 7 tests: 5-failure gate → PressBack, consecutive-error accumulation, popup retry, no-change → Branch, Execute success, precondition fail, AI empty response | Regression / Recovery |
| F-02 | `tests/UniClaw.Core.Tests/Simulation/TraceReplay/SettingsEnumerateRegression.cs` | `Enumerate_StopsAtDepth2` — DynamicMatch depth runaway bug regression | Regression / False completion |
| F-03 | `tests/UniClaw.Core.Tests/Simulation/TraceReplay/FixVerificationTests.cs` | L1-L8: replay regression, depth guard, FSM invariant, click circuit breaker, empty-text skip, OCR variants, depth semantics, subtitle downgrade | Regression / Recovery |
| F-04 | `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs` | Documents **unfixed BUG**: hub→listA/listB, only first branch walked, listB 0/16 but `AllVisited` ("谎言") | Regression / False completion |
| F-05 | `tests/UniClaw.Core.Tests/Traversal/ContainerGatewayTests.cs` | F3 + non-root-frame tests: dead-end completion only on scroll-failure → infinite scroll; non-root `FrameCompleted` unconsumed → stuck | Regression / False completion |
| F-06 | `tests/UniClaw.Core.Tests/Traversal/TraversalEngineTests.cs` | `Run_MaxSteps_ReturnsMaxStepsReason`, `Timeout_ExceedsPolicySeconds`, entry policy no fake success | Failure / False completion |
| F-07 | `tests/UniClaw.Core.Tests/StateMachine/HandleErrorHandlingTests.cs` | Bug #2: Execute throws → catch → ErrorHandling without incrementing | Recovery |
| F-08 | `tests/UniClaw.Core.Tests/Traversal/TextTargetResolutionTests.cs` | "BUG: currently matches 'Flash notifications' via Contains" — over-broad matching | Failure |
| F-09 | `tests/UniClaw.TraceTool.Tests/VerifyEngineTests.cs` | "Regression: Bug 1 — empty item name should NOT match" via `Contains("")` | Regression |
| F-10 | `tests/UniClaw.Host.Tests/Hooks/VerifyHookTests.cs`, `Commands/HostCommandTests.cs`, `Scenarios/ScenarioCatalogTests.cs`, `CliTests.cs` | Host/CLI failure-path tests (expected page change fail, trace close on analysis fail, unsupported schema, duplicate scenario ids, missing run) | Failure |

### Production-code failure/recovery infrastructure

| Ref | Path | Relates to | Description |
|---|---|---|---|
| F-11 | `src/UniClaw.Core/StateMachine/ErrorHandler.cs`, `ErrorContext.cs`, `IErrorContext.cs` | Recovery | Error classification + recovery strategy (Retry/Backtrack/PressBack/Pop-only), consecutive-error gate |
| F-12 | `src/UniClaw.Core/StateMachine/PopupHandler.cs` | Failure | Popup dismiss handling |
| F-13 | `src/UniClaw.Core.SourceGen/TraceHandlerGenerator.Emitter.cs` | Recovery | Generates `RecoveryOutcome` enum, `ErrorStrategy` |
| F-14 | `src/UniClaw.Core/Observability/File/FileTraceStorage.cs` | Failure tolerance | D-93: corrupted trace lines skipped — single bad line must not block reads |
| F-15 | `src/UniClaw.Core/Traversal/InterceptionHandler.cs:469` | Failure / False positive | Content guard: unchanged items after scroll → ROI difference treated as false positive |

### Documentation-based failure references

| Ref | Path | Description |
|---|---|---|
| F-16 | `.test_fix_log.md` | Decision log: full suite 677/677; S5 snapshot re-freeze (MaxEmptyScrollRetries=1 drifted 53→70 lines) |
| F-17 | `.claude/agents/fsm-analyzer-memory/knowledge.md` | D-244 popup dismiss failure; misclassification risk: ErrorClassifier substring matching |
| F-18 | `docs/refactor/2026-07-30-deliver-safe-settings-spec-defect-analysis.md` | Spec defect: completion accounting contract never specified; MUST NOT report exhaustive enumeration when end-of-list unproven |
| F-19 | `docs/refactor/2026-07-30-current-internal-gaps-calibrated.md:49` | Coverage gap: UIAutomator-vs-AI observation source equivalence unbridged |
| F-20 | `docs/system/decisions/log.md:1094` | Stale DynamicMatch caches → `max_steps` (1000) exhaustion + error-retry loops; fingerprint-aware caching fix |
| F-21 | `docs/prd/2026-07-29-local-implementation-gap-prd.md` | GAP-P0-02: EntryPolicy is fake success; ADB scroll failure folds into "reached end"; 429/5xx/timeout bounded retry |
| F-22 | `docs/refactor/12-python-csharp-design-gaps.md` | F-1: Permission popup C# forces auto_close vs Python waits timeout; `CompletionReason`/`CompletionStatus` enum drift (4 vs 5 values) |
| F-23 | `docs/testing/android-emulator.md:141` | device/ADB failures are not no-scroll or end-of-list |

---

## Intent / Goal / Plan Pointers

**Count:** 17 pointers (production code + tests + documentation)

### Production code — Planning/Compilation Pipeline

| Ref | Path | Class | Relates to | Description |
|---|---|---|---|---|
| IP-01 | `src/UniClaw.Core/Graph/Services/PlanCompiler.cs` | `PlanCompiler : IPlanCompiler` | Plan | **Deterministic IntentSlots → TraversalPlan compiler, no AI.** 5-step `Compile()`: ValidateSlots → BuildEntryPolicy → BuildRootNode → BuildCompletionPolicy → assemble. `TemplateSets` keyed by ElementHandling. "Single source of truth"; dormant in baseline |
| IP-02 | `src/UniClaw.Core/Graph/Abstractions/IPlanCompiler.cs` | `IPlanCompiler` | Plan | `TraversalPlan Compile(IntentSlots slots)` interface |
| IP-03 | `src/UniClaw.Core/Graph/Models/TraversalPlan.cs` | `TraversalPlan`, `EntryPolicy`, `CompletionPolicy`, `IntentSlots` | Plan / Intent | `CompletionPolicyType { None, TargetFound, MaxSteps, Timeout }`; fail-fast validation; `IntentSlots(TargetApp, Scope, Depth, Entry, ElementHandling, Navigation, Restore, Completion)` |
| IP-04 | `src/UniClaw.Core/UniBrain/IntentExtractor.cs` | `IIntentExtractor`, `IntentExtractor`, `ExtractedIntentSlots` | Intent | **AI intent extraction from NL scenario description** via `IModelProvider` (DeepSeek flash); infers Scope/ElementHandling/Navigation/Restore/Completion |
| IP-05 | `src/UniClaw.Host/Runner/ScenarioPlanning.cs` | `ScenarioPlanCompiler` | Plan / Scenario | Host orchestration: `Compile(ScenarioSnapshot)` → `ResolveIntentSlots` (via `IIntentExtractor`) → `PlanCompiler.Compile` → `ApplyTargetNarrowing` → `ApplyExcludePatterns` |
| IP-06 | `src/UniClaw.Host/Runner/ScenarioPlanLoader.cs` | `ScenarioPlanLoader.Load(planJson)` | Plan | **Plan mode**: hand-authored plan JSON → executable `TraversalPlan` (Static + StaticNodes) |
| IP-07 | `src/UniClaw.Core/Traversal/TraversalEngine.cs` | `TraversalEngine.CompilePlan` | Plan | Plan → node-tree compilation; consumes EntryPolicy/CompletionPolicy/IntentSlots.Depth |
| IP-08 | `src/UniClaw.Core/UniBrain/ITraversalAdvisor.cs`, `TraversalAdvisor.cs` | `DecideNextActionAsync(string goal, ...)` | Goal / Dynamic action | **Dynamic action generation**: goal-directed single-next-action decision ("Given a goal... decide the single next action"). No standalone `Goal` class — "goal" is a prompt variable only |

### Test code — intent/goal/plan

| Ref | Path | Relates to |
|---|---|---|
| IP-09 | `tests/UniClaw.Core.Tests/UniBrain/IntentExtractorTests.cs` (14 tests) | Intent |
| IP-10 | `tests/UniClaw.Core.Tests/UniBrain/AIIntentSimulationTests.cs` (9 tests) | Intent / Plan |
| IP-11 | `tests/UniClaw.Core.Tests/Graph/GraphTests.cs` | Plan (only production instantiator of `PlanCompiler`) |
| IP-12 | `tests/UniClaw.Core.Tests/Graph/FailFastValidationBaselineTests.cs` | Plan |
| IP-13 | `tests/UniClaw.Core.Tests/Traversal/TraversalEngineTests.cs` — `CompletionPolicyTests` class | Plan / Goal |

### Documentation — intent/goal/plan

| Ref | Path | Description |
|---|---|---|
| IP-14 | `docs/design/ai-plan-optimization-hints.md` | Canonical pipeline: 场景描述 → AI IntentExtractor → IntentSlots → PlanCompiler → TraversalPlan → 仿真/执行 |
| IP-15 | `docs/prd/2026-07-30-runner-through-engine-design.md` | "Plan mode ≠ Intent mode, But Both Use the FSM": plan mode = Static/StaticNodes (data); intent mode = DynamicMatch |
| IP-16 | `docs/refactor/2026-07-19-plancompiler-default-alignment-design.md` | Change A (plan-side): CompletionPolicy semantics + PlanCompiler derivation; **dormant preventive correctness fix** |
| IP-17 | `docs/refactor/2026-07-15-python-csharp-gap-triage.md` | C-5: Python `task_parser.py` (NL task → IntentSlots) has **no C# equivalent** — `UnderstandTextAsync` has only test callers |

### Notable negative finding

No class named `Goal`, `Task`, `TaskCompiler`, or `TaskManager` exists in `feature/refactor` C# source. "Goal" appears only as the `ITraversalAdvisor` prompt variable; "Task" as `System.Threading.Tasks.Task`. The Python `task_parser.py` NL→IntentSlots link is documented as unimplemented in C#.

---

## Unclassified Legacy Evidence

The following items in `feature/refactor` appear high-value but do not fit cleanly into the four documented test families:

1. **`tests/UniClaw.Core.Tests/Architecture/ArchitectureGuardTests.cs`** — CI-blocking architecture guards (EnumValueGuardTests 12 + DependencyDirectionGuardTests 4). Documented in `AGENTS.md` as "Guard Tests" and in `simulation-baseline.md` as `Architecture/` sub-tier. These are constraint tests, not simulation tests per se — they validate code structure (enums exhaustive, dependency direction) rather than runtime behavior.

2. **`tests/UniClaw.TraceTool.Tests/` entire project** — TraceTool tests (`TraceRunTests`, `DiagnoseTests`, `CliTests`, `JsonContractTests`, `VerifyEngineTests`) are offline analysis/verification tests operating on committed trace snapshots. They don't fit the Integration/Simulation/Replay trichotomy cleanly — they're a fourth category: **offline trace analysis**.

3. **`tests/UniClaw.Host.Tests/Runner/RunnerTestHarness.cs`** — Documented in `test-tiers.md` as "Legacy Harness (migration-only)". Preserves old-runner fault cases (stale plan, wrong-page back, dangerous-skip, scroll-stuck). Classified as its own tier, not part of the three primary families.

---

## Missing / Broken References

1. **`adb-action` scope has zero consumers** — `IntegrationTestScopes.AdbAction` is defined and documented (layer 5 in the 8-level ladder), but no test on `feature/refactor` uses it. The click-layer test appears to have been removed (possibly with `delete-uia`).

2. **Config/test mismatch in `IntegrationConfigTests`** — `DefaultConfig_LoggingSection_ParsesToWarning` asserts `config.Logging.Level == "warning"` but the committed `integration.config.json` has `"level": "information"`. Likely a failing baseline test or stale config.

3. **Python `task_parser.py` has no C# equivalent** — Documented in `docs/refactor/2026-07-15-python-csharp-gap-triage.md` (C-5) and `docs/prd/2026-07-29-local-implementation-gap-prd.md:246`. The NL→IntentSlots link (`UnderstandTextAsync`) has only test callers, no production consumers.

4. **`SimulationRunner`/`SimulationConfig`/`SimulationResult` deleted** — Documented in `docs/system/layers/simulation.md` as having logic migrated to `TraversalEngine`, but any documentation still referencing these types is stale.

5. **Unfixed `MultiBranchNavigationTests` BUG** — Hub with two nav buttons, engine only walks first branch; listB 0/16 visited but reports `AllVisited`. Comments at lines 151, 202, 249 state "当前行为 (BUG)". References archived `openspec/changes/navigation-subpage-frames` but `docs/refactor/2026-07-29-current-internal-gaps.md:160` notes these comments still describe TDD failures + BUG behavior while the related change is archived as complete.

---

## Inventory Completeness

**LEGACY_PRIMARY_CORPUS_INVENTORIED**

All four documented test families (INTEGRATION / SIMULATION / SIMULATION_REPLAY / LEGACY_HARNESS) have been inventoried from `feature/refactor` documentation → concrete tests → fixtures/traces/replay data → failure/regression evidence. Intent/Goal/Plan pointers have been flagged for later extraction. Three unclassified families (Architecture guards, TraceTool offline analysis, Legacy Harness) and five missing/broken references have been documented.

---

## Repository Changes

NONE
