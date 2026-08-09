# LEGACY_HIGH_VALUE_EVIDENCE_SET_RESULT — Step 2

> Generated: 2026-08-09
> Primary input: `docs/decisions/legacy-source-inventory-step1.md`
> Legacy truth source: `feature/refactor` (read-only Git objects)

---

## Selection Summary

**Inventory Sources Reviewed:** 65+ items across 5 families
**Primary Evidence Selected:** 18
**Supporting Evidence Selected:** 28
**Deferred:** 11
**Rejected Low-Value:** 11

---

## Scoring Methodology

Each candidate scored 0–2 on five axes (max 10):

| Axis | Name | 0 | 1 | 2 |
|---|---|---|---|---|
| R | Reality Strength | Implementation-only / synthetic helper | Deterministic simulation or executable regression | Real integration / recorded run / emulator / production failure |
| F | Failure Information | Happy path only | Boundary / negative behavior | Concrete historical failure or regression |
| C | Composition Depth | Single isolated operation | Multi-step local behavior | Cross-page / recovery / replay / multi-component |
| U | Uniqueness | Clearly redundant with stronger evidence | Partially distinctive | Distinct pressure worth later analysis |
| T | Traceability | Weak / document claim only | Executable test or fixture | Executable + fixture/trace/history linking to evidence |

Only used for selection priority — NOT a semantic novelty score.

---

## Evidence Coverage Check

| Required Category | Covered By | Status |
|---|---|---|
| Real / integration behavior | IT-06, IT-04 | YES |
| Deterministic simulation | SIM-02, SIM-09, SIM-13 | YES |
| Historical replay | R1-TRR, R1-FV, R1-TRZ | YES |
| Known failure / regression | SIM-14 (F-04), F-02, F-05, F-21 | YES |
| Recovery | SIM-09, F-07 | YES |
| Multi-branch / navigation | SIM-14, SIM-02, F-05 | YES |
| Completion | SIM-14, F-05, F-18, F-21 | YES |
| Scroll / exploration | SIM-13, SIM-12 | YES |
| Negative / error | SIM-09, F-21, SIM-08 | YES |
| Intent / Goal / Plan transformation | IP-01, IP-04, IP-06, IP-08, IP-17 | YES |

---

## Primary Evidence Set

### E-01 — EmulatorScenarioIntegrationTests (Full-stack scenario integration)

- **Evidence ID:** E-01
- **Title:** EmulatorScenarioIntegrationTests — full production composition through Core engine on Android emulator
- **Source:** `tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs`
- **Family:** INTEGRATION
- **Priority:** P0
- **EvidenceValue:** 10 (R=2, F=2, C=2, U=2, T=2)
- **Why Selected:** Deepest external-world test in the legacy corpus. Drives the entire production composition (`HostCompositionFactory.RunScenarioAsync` → Core `TraversalEngine`/`TraversalFSM`) against a real Android emulator with real AI provider. Two scenarios: locate (targeted stop) and enumerate (exhaustive traversal). Produces full `result.json` with status, stepsConsumed, successCriteriaSatisfied, successEvidence, discovered/visited/skipped entries. Post-hoc TraceTool `VerifyEngine` verdict for locate mode. Provider preflight fail-fast. This is the only evidence showing the full chain: scenario description → AI vision → ADB action → FSM step → trace → verification.
- **Executable:** YES (scope-gated: `scenario-locate`, `scenario-enumerate`)
- **Fixture / Trace:** `AdbTestContext`, `IntegrationConfigLoader` + `integration.config.json`, `ProviderPreflight`, scenario JSONs `scenarios/android-settings/locate-one-item.v1.json` + `enumerate-settings-safely.v1.json`, run artifacts under `artifacts/runs/integration/{scope}/{scenarioId}/{runTimestamp}/` including `result.json`
- **Historical Failure Link:** No specific historical failure recorded, but verify step (`VerifyEngine`) implies prior false-success risk
- **Supporting Evidence:** IT-05 (AdbVisionActionIntegrationTests — vision+ADB closed loop), IT-02 (VisionGoldenIntegrationTests — observation quality), `docs/testing/integration-tests.md` (scope ladder), `docs/testing/integration-config.md` (config layers)
- **Evidence Available For Later Extraction:**
  - task intent: YES (scenario JSON → IntentSlots)
  - initial state: YES (emulator state)
  - observation: YES (real AI vision PageAnalysis)
  - action: YES (real ADB tap/navigate/back)
  - disturbance: PARTIAL (provider preflight failures)
  - outcome: YES (result.json with completion reason, steps, success evidence)
  - failure: YES (status assertions, VerifyEngine verdict)
  - expected behavior: YES (scenario success criteria)

---

### E-02 — AdbSessionIntegrationTests (ADB self-healing with killed server)

- **Evidence ID:** E-02
- **Title:** AdbSessionIntegrationTests — ADB session self-healing after deliberate server kill
- **Source:** `tests/UniClaw.Host.Tests/Device/AdbSessionIntegrationTests.cs`
- **Family:** INTEGRATION
- **Priority:** P1
- **EvidenceValue:** 8 (R=2, F=2, C=1, U=2, T=1)
- **Why Selected:** Only test in the corpus that actively kills an external service mid-test and asserts recovery. Proves 3-tier ADB self-healing. Also verifies `AdvancedSharpAdbSession` vs `ProcessAdbSession` behavioral equivalence. Unique evidence of external-infrastructure failure injection at the device boundary — a class of disturbance absent from simulation.
- **Executable:** YES (scope-gated: `adb-session`)
- **Fixture / Trace:** `AdbTestContext.ResolveSerialAsync`, raw `adb` CLI process helper, no artifact files written
- **Historical Failure Link:** No specific historical failure; test was designed to prevent regression
- **Supporting Evidence:** IT-03 (AdbRealDeviceIntegrationTests — serial/screencap boundary), `scripts/android-emulator.sh`
- **Evidence Available For Later Extraction:**
  - task intent: NO
  - initial state: YES (adb server state)
  - observation: NO
  - action: YES (kill adb server, re-execute shell)
  - disturbance: YES (external service kill)
  - outcome: YES (recovery success/failure)
  - failure: YES (external infrastructure failure)
  - expected behavior: YES (self-healing within 3 tiers)

---

### E-03 — SimulationBaselineTests (7-page Settings full traversal + target search)

- **Evidence ID:** E-03
- **Title:** SimulationBaselineTests — deterministic 7-page Settings app full traversal and target-search stop
- **Source:** `tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs`
- **Family:** SIMULATION
- **Priority:** P2
- **EvidenceValue:** 7 (R=1, F=1, C=2, U=1, T=2)
- **Why Selected:** Strongest representative of the deterministic simulation baseline tier. 7-page Settings fixture with DynamicMatch root, code-built via `StateFixtureBuilder`, driven through real `TraversalEngine`/`TraversalFSM` with `StatefulMockVisionService`/`StatefulMockActionExecutor`. Two scenarios: exhaustive traversal (`AllVisited`) and target-search early stop (`StopsAtDarkMode`). JSON `ExpectedBehavior` contracts with numeric anchors (e.g., totalSteps=66), forbidden-page assertions, and CI-blocking constitution C-11. Proves the legacy engine can traverse a non-trivial page graph and stop at a target.
- **Executable:** YES (plain `[Fact]`, always runs)
- **Fixture / Trace:** Code-built 7-page `StateFixture` + `StatefulMockVisionService`/`StatefulMockActionExecutor`, JSON ExpectedBehavior contracts at `Baseline/Fixtures/expected/settings-full-traversal.json` + `settings-target-search.json`
- **Historical Failure Link:** No specific historical failure; Python baseline anchor at 118 steps/19 nodes for reference
- **Supporting Evidence:** SIM-01 (SimulationE2ETests — MaxSteps failure, empty-area tap failure), `docs/system/layers/simulation-baseline.md` (contract specification), `docs/refactor/17-simulation-baseline-tests.md` (HOW guide)
- **Evidence Available For Later Extraction:**
  - task intent: YES (full traversal vs target-search intent)
  - initial state: YES (Settings app home page)
  - observation: YES (mock vision producing PageAnalysis from fixture)
  - action: YES (mock action executing tap/back)
  - disturbance: NO
  - outcome: YES (AllVisited vs TargetFound, visited pages, step count)
  - failure: PARTIAL (contract assertions check forbidden pages)
  - expected behavior: YES (JSON contracts with CompletionExpectation, PageCoverageExpectation, NumericAnchor)

---

### E-04 — FsmSimulationRegressionTests (FSM fault injection + error-handling regression)

- **Evidence ID:** E-04
- **Title:** FsmSimulationRegressionTests — deterministic FSM error-handling regression with fault injection
- **Source:** `tests/UniClaw.Core.Tests/StateMachine/FsmSimulationRegressionTests.cs`
- **Family:** SIMULATION
- **Priority:** P2
- **EvidenceValue:** 8 (R=1, F=2, C=2, U=2, T=1)
- **Why Selected:** Only evidence in the corpus of systematic fault injection into the FSM. Uses `FsmSimulationHarness` to drive `TraversalFSM` to target states with configurable `FakeActionExecutor` returns, `StrategyForcingHandler`, failing precondition checkers, and `CallbackPageAnalyzer`. Covers: 5-failure gate → PressBack, consecutive-error accumulation across backtracks (Bug #2: Backtrack must NOT reset consecutive-error count), popup single-retry detection, no-change → Branch, Execute success → ResultVerify, precondition-checker gate → ErrorHandling, AI empty response not transient. <1ms each, no emulator, no AI.
- **Executable:** YES (plain `[Fact]`, always runs)
- **Fixture / Trace:** `FsmSimulationHarness` (drives FSM to target states), `FakeActionExecutor` (configurable returns), `FakeBrain` (null-advisor), `StrategyForcingHandler`, `CallbackPageAnalyzer`, `InMemoryTraceStorage`
- **Historical Failure Link:** F-07 (Bug #2: Execute throws → catch → ErrorHandling without incrementing consecutive-error count)
- **Supporting Evidence:** F-07 (HandleErrorHandlingTests Bug #2), F-11 (ErrorHandler/ErrorContext production implementation), F-12 (PopupHandler production implementation), F-17 (fsm-analyzer-memory misclassification risk)
- **Evidence Available For Later Extraction:**
  - task intent: NO
  - initial state: YES (synthetic FSM runtime context)
  - observation: YES (callback PageAnalysis)
  - action: YES (configurable FakeActionExecutor)
  - disturbance: YES (fault injection: forced error strategies, failing precondition checkers, empty AI responses)
  - outcome: YES (FSM state transitions, error recovery outcomes)
  - failure: YES (5-failure gate, consecutive-error accumulation, popup retry failure, AI empty response)
  - expected behavior: YES (asserted FSM state transitions and error strategy selections)

---

### E-05 — AIIntentSimulationTests (NL intent → PlanCompiler → Engine end-to-end)

- **Evidence ID:** E-05
- **Title:** AIIntentSimulationTests — natural-language intent through stub AI to PlanCompiler through TraversalEngine
- **Source:** `tests/UniClaw.Core.Tests/UniBrain/AIIntentSimulationTests.cs`
- **Family:** SIMULATION
- **Priority:** P2
- **EvidenceValue:** 8 (R=1, F=1, C=2, U=2, T=2)
- **Why Selected:** Only evidence of the full Intent → Plan → Execution chain in a deterministic simulation. Uses 6-page Settings `StateFixture`, `StubHttpHandler` returning canned DeepSeek chat/completions JSON through `OpenAiCompatibleVisionProvider`, real `PlanCompiler`, real `TraversalEngine`. Covers locate and enumerate scenarios, deep navigation (3 levels), and live Sensenova opt-in variants. Proves: NL scenario description → AI intent extraction → IntentSlots → PlanCompiler → TraversalPlan → TraversalEngine → completion. The stub-driven tests are deterministic; live tests are opt-in gated.
- **Executable:** YES (stub-driven: plain `[Fact]`; `LiveSensenova_*`: opt-in gated)
- **Fixture / Trace:** 6-page Settings `StateFixture`, `StubHttpHandler` with canned DeepSeek JSON responses, `StatefulMockVisionService`/`StatefulMockActionExecutor`, real `PlanCompiler` + `TraversalEngine`
- **Historical Failure Link:** No specific failure; IP-16 documents dormant preventive correctness fix in PlanCompiler
- **Supporting Evidence:** IP-09 (IntentExtractorTests — 14 tests), IP-14 (ai-plan-optimization-hints.md — pipeline diagram), IP-16 (plancompiler-default-alignment-design.md — dormant fix)
- **Evidence Available For Later Extraction:**
  - task intent: YES (natural-language scenario description → ExtractedIntentSlots)
  - initial state: YES (Settings fixture home page)
  - observation: YES (mock vision)
  - action: YES (mock action)
  - disturbance: NO (stub tests); PARTIAL (live tests)
  - outcome: YES (completion reason, visited pages)
  - failure: YES (early-stop/target-found, deep navigation limits)
  - expected behavior: YES (asserted completion at target / exhaustive coverage)

---

### E-06 — ScrollableBaselineTests (WiFi list scroll + content profiles)

- **Evidence ID:** E-06
- **Title:** ScrollableBaselineTests — 7-scenario WiFi list scroll baseline with varied content profiles
- **Source:** `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs`
- **Family:** SIMULATION
- **Priority:** P2
- **EvidenceValue:** 7 (R=1, F=1, C=2, U=1, T=2)
- **Why Selected:** Strongest scroll evidence in the corpus. 7 scenarios covering: scroll-through-all-screens, scroll-back-to-top, element deduplication with overlapping content, boundary conditions (top/bottom), sparse-list jump recovery, overlapping-list adaptive step, and target-search with scroll. Uses `WiFiListFixture7Screens` (24 networks, pageSize 4) plus sparse (8 items, fillRatio 0.5) and overlapping (17 items, fillRatio 0.8) profiles. JSON `ExpectedBehavior` contracts with numeric anchors. CI-blocking (constitution C-11).
- **Executable:** YES (plain `[Fact]`, always runs)
- **Fixture / Trace:** `WiFiListFixture7Screens` + `PagedItemGenerator` content (24/8/17 items), `ScrollableMockVisionService`/`ScrollableMockActionExecutor`, ExpectedBehavior JSON contracts at `Baseline/Fixtures/expected/scroll/*.json` (6 files)
- **Historical Failure Link:** No specific failure; `docs/refactor/scrollable-baseline-test-design.md` documents design
- **Supporting Evidence:** SIM-12 (LongListBaselineTests — sparse/dense/jump scroll profiles), SIM-11 (HierarchyBaselineTests — 4-level nav with scroll), `docs/refactor/scrollable-baseline-test-design.md`
- **Evidence Available For Later Extraction:**
  - task intent: YES (full scroll vs target-search)
  - initial state: YES (WiFi list top)
  - observation: YES (scroll-aware mock vision)
  - action: YES (scroll-aware mock action with progress tracking)
  - disturbance: YES (sparse fill → jump recovery, overlapping → dedup, adaptive step)
  - outcome: YES (all networks visited, progress reverted, dedup count, boundary correctness)
  - failure: YES (contract assertions: forbidden pages, termination conditions)
  - expected behavior: YES (JSON contracts with scroll-specific expectations)

---

### E-07 — MultiBranchNavigationTests (UNFIXED BUG: false AllVisited with unvisited branch)

- **Evidence ID:** E-07
- **Title:** MultiBranchNavigationTests — unfixed production bug: hub with two nav buttons, only first branch traversed, reports AllVisited while second branch remains 0/16
- **Source:** `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs`
- **Family:** SIMULATION
- **Priority:** P0
- **EvidenceValue:** 9 (R=1, F=2, C=2, U=2, T=2)
- **Why Selected:** **MANDATORY INCLUSION — Known unresolved production/regression bug.** Hub page with two navigation buttons (to_A→listA with 16 scrollable items, to_B→listB with 16 scrollable items). Engine only walks the first branch. listB reports 0/16 visited. Yet `CompletionReason` = `AllVisited`. Comments at lines 151, 202, 249 explicitly state "当前行为 (BUG)". References archived `openspec/changes/navigation-subpage-frames` but the bug persists. `docs/refactor/2026-07-29-current-internal-gaps.md:160` confirms these comments still describe TDD failures + BUG behavior while the related change is archived as complete. This is the strongest false-completion evidence in the corpus.
- **Executable:** YES (plain `[Fact]`, always runs — documents bug, may not assert failure)
- **Fixture / Trace:** Hub→listA/listB `StateFixture` (16-item scrollable branches), `ScrollableMockVisionService`/`ScrollableMockActionExecutor`
- **Historical Failure Link:** Archived OpenSpec change `navigation-subpage-frames` (marked complete but bug persists); `docs/refactor/2026-07-29-current-internal-gaps.md:160`
- **Supporting Evidence:** F-18 (spec-defect-analysis.md — completion accounting contract never specified), `docs/refactor/2026-07-30-deliver-safe-settings-spec-defect-analysis.md`, archived `openspec/changes/navigation-subpage-frames`
- **Evidence Available For Later Extraction:**
  - task intent: YES (exhaustive enumeration of multi-branch hub)
  - initial state: YES (hub page with two nav buttons)
  - observation: YES (mock vision showing both buttons)
  - action: YES (mock action executing tap on first button only)
  - disturbance: NO (the bug IS the disturbance — second branch silently skipped)
  - outcome: YES (AllVisited despite 0/16 on listB — false completion)
  - failure: YES (silent branch loss, false AllVisited)
  - expected behavior: YES (both branches should be visited; 32/32 items)

---

### E-08 — TraceReplayFromRunTests (Record → replay → diagnose → fix cycle)

- **Evidence ID:** E-08
- **Title:** TraceReplayFromRunTests — auto-discover real run artifacts, replay, diagnose depth runaway, verify fix
- **Source:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/TraceReplayFromRunTests.cs`
- **Family:** SIMULATION_REPLAY
- **Priority:** P0
- **EvidenceValue:** 10 (R=2, F=2, C=2, U=2, T=2)
- **Why Selected:** **MANDATORY INCLUSION — Recorded replay of historical failures.** Establishes the record-then-replay pattern against real run artifacts. `Step1_AutoDiscoverAndReplay` auto-discovers `artifacts/runs/` directories and replays `analysis.jsonl` frames through `TraceReplayHarness`. `Step2_Diagnose_DepthRunaway` reproduces a pre-fix bug where subframe depth reached 4 (should be ≤2). `Step3_FixVerify_RestoreConstrainsDepth` verifies the fix. `ExportReplayForViewer` writes `artifacts/sim-replay/trace-replay-export.json`. Proves: real run failures can be reproduced deterministically at <1s per iteration without an emulator.
- **Executable:** YES (self-skip when `artifacts/runs/` absent; plain `[Fact]` when artifacts present)
- **Fixture / Trace:** Auto-discovered `artifacts/runs/{scenario}/{runId}/`: `plan.json` + `result.json` + `assets/{runId}/analysis.jsonl` + `trace/{runId}/run.log`. `TraceReplayHarness` with `TraceReplayVisionService` and `TraceReplayActionExecutor`.
- **Historical Failure Link:** Depth=4 subframe bug (pre-fix); run `20260805T052309367Z` (max_steps loop); run `20260806T072558649Z` (subtitle double-click, Flash misclick, PressBack-exits-desktop)
- **Supporting Evidence:** R2 (TraceTool committed failure snapshot `Fixtures/failure/`), R3 (sim-replay-viewer.py visualization), `artifacts/sim-replay/trace-replay-export.json` (committed export), `.claude/skills/trace-to-simulation/SKILL.md` (workflow)
- **Evidence Available For Later Extraction:**
  - task intent: YES (replayed plan.json)
  - initial state: YES (first PageAnalysis frame)
  - observation: YES (recorded PageAnalysis frames replayed in sequence)
  - action: YES (recorded action sequence from run.log or engine decisions)
  - disturbance: YES (the bugs being diagnosed: depth runaway, loop stuck, misclicks)
  - outcome: YES (result.json completionReason/actionsAttempted)
  - failure: YES (depth=4, max_steps, stuck states)
  - expected behavior: YES (fix verification: depth ≤ maxDepth after fix)

---

### E-09 — FixVerificationTests (L1–L8 layered regression verification)

- **Evidence ID:** E-09
- **Title:** FixVerificationTests — 8-layer fix verification from replay regression to subtitle degradation
- **Source:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/FixVerificationTests.cs`
- **Family:** SIMULATION_REPLAY
- **Priority:** P0
- **EvidenceValue:** 10 (R=2, F=2, C=2, U=2, T=2)
- **Why Selected:** **MANDATORY INCLUSION — Recorded replay of historical failures.** 3-layer verification architecture: L1 replays a real trace regression (post-fix verification). L2–L8 are fixture-based FSM tests encoding specific historical bug patterns: L2 `DepthConstraint_StopsAtLevel2` (depth guard), L3 `FsmInvariant_SubframeDepthNeverExceedsMaxDepth` (FSM invariant), L4 `StaleClick_NodeSkippedAfterLimit` (circuit breaker — 3x unchanged → skip, no infinite retry), L5 `EmptyTextItem_SkippedInGenerate` (empty OCR text skip without exception), L6 `NormalizeItemText_OcrVariants` (OCR variant normalization), L7 depth-semantics theory (`Depth ≥ MaxDepth+1` → menu_container degrades to leaf_info), L8 `SubtitleDegraded_NoDoubleClick_SamePage` (subtitle V2 missed downgrade → same page double-click regression). Each layer encodes a distinct historical bug class.
- **Executable:** YES (plain `[Fact]`, always runs)
- **Fixture / Trace:** L1: real trace replay via `TraceReplayHarness`; L2–L8: `StateFixture`-based with `StatefulMockVisionService`/`StatefulMockActionExecutor`
- **Historical Failure Link:** Real run `20260806T072558649Z` (subtitle double-click, Flash misclick, PressBack-exits-desktop); depth runaway; stale click infinite retry; OCR empty text; subtitle V2 missed downgrade
- **Supporting Evidence:** R1-TRZ (TraceReplay_20260805T052309367Z — DFS revisit-loop), F-02 (SettingsEnumerateRegression — depth constraint), F-20 (decisions/log.md — stale DynamicMatch cache exhaustion)
- **Evidence Available For Later Extraction:**
  - task intent: YES (enumerate Settings safely)
  - initial state: YES (Settings pages from fixture or trace)
  - observation: YES (mock vision or recorded frames)
  - action: YES (mock action or recorded actions)
  - disturbance: YES (each layer encodes a specific disturbance: depth violation, stale click, empty OCR, subtitle degradation, OCR variants)
  - outcome: YES (asserted correct behavior post-fix)
  - failure: YES (each layer reproduces a specific historical failure pattern)
  - expected behavior: YES (L2–L8 each assert a specific invariant)

---

### E-10 — TraceReplay_20260805T052309367Z (Real-run DFS revisit-loop reproduction)

- **Evidence ID:** E-10
- **Title:** TraceReplay_20260805T052309367Z — real run enumerate-settings-safely failure reproduction: DFS revisit loop + search-box stuck
- **Source:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/20260805T052309367Z_TraceReplayTests.cs`
- **Family:** SIMULATION_REPLAY
- **Priority:** P0
- **EvidenceValue:** 10 (R=2, F=2, C=2, U=2, T=2)
- **Why Selected:** **MANDATORY INCLUSION — Recorded replay of historical failures.** Concrete replay of a specific real integration run that failed with `max_steps (120), settings_home_not_restored`. Three tests: DFS revisit-loop reproduction (asserts MaxSteps OR AllVisited — the "stuck" end state), search-box type=input skip, search-box misclassified as menu_item → stuck. Fixture factory (`20260805T052309367Z_EnumerateFixtures.cs`) extracted from real `analysis.jsonl` rows + `run.log` actions — data provenance documented inline. Proves: real-world OCR misclassification and loop behavior can be captured, replayed, and used as permanent regression guards.
- **Executable:** YES (plain `[Fact]`, always runs — fixtures are committed)
- **Fixture / Trace:** `20260805T052309367Z_EnumerateFixtures.cs` (hand-reconstructed from real `analysis.jsonl` + `run.log`), `plan.json` from run, `TraceReplayHarness`
- **Historical Failure Link:** Real integration run `20260805T052309367Z-1bc7a25ea6384e3` (local provider, safe_mode, enumerate-settings-safely) — outcome `max_steps (120), settings_home_not_restored`
- **Supporting Evidence:** E-08 (TraceReplayFromRunTests), E-09 (FixVerificationTests), R2 (TraceTool failure snapshot)
- **Evidence Available For Later Extraction:**
  - task intent: YES (enumerate Settings safely — safe_mode plan template)
  - initial state: YES (Settings home from real analysis.jsonl)
  - observation: YES (reconstructed PageAnalysis from real vision frames)
  - action: YES (action sequence from real run.log)
  - disturbance: YES (search-box misclassified as menu_item, DFS revisit loop)
  - outcome: YES (MaxSteps exhausted, settings_home_not_restored)
  - failure: YES (two distinct failure modes: revisit loop + search-box stuck)
  - expected behavior: YES (should complete enumeration without getting stuck)

---

### E-11 — SettingsEnumerateRegression (Depth constraint: DynamicMatch ignored maxDepth)

- **Evidence ID:** E-11
- **Title:** SettingsEnumerateRegression — permanent regression: DynamicMatch sub-frame generation ignored maxDepth → depth runaway
- **Source:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/SettingsEnumerateRegression.cs`
- **Family:** FAILURE / REGRESSION
- **Priority:** P2
- **EvidenceValue:** 7 (R=1, F=2, C=1, U=2, T=1)
- **Why Selected:** Encodes a specific production bug as a permanent deterministic regression: "DFS DynamicMatch 子帧生成不受 maxDepth 约束 → 深度失控". Uses real API-35 Settings page structure (4-level nesting). Verifies engine stops at depth=2, never enters Wi-Fi (depth=3). Uses `CompletionPolicyType.Exhaustive` + `IntentSlots(..., Depth: 2)`. Proves: depth constraints can be violated by DynamicMatch sub-frame generation, and the fix must propagate depth constraints into sub-frame expansion.
- **Executable:** YES (plain `[Fact]`, always runs)
- **Fixture / Trace:** API-35 Settings `StateFixture` (4-level nesting), `StatefulMockVisionService`/`StatefulMockActionExecutor`
- **Historical Failure Link:** Real bug: DynamicMatch depth ignored → real runs hit depth=3+ pages that should have been excluded
- **Supporting Evidence:** E-09 L2 (DepthConstraint_StopsAtLevel2), E-09 L3 (FsmInvariant_SubframeDepthNeverExceedsMaxDepth), E-09 L7 (depth-semantics theory), F-20 (stale DynamicMatch cache exhaustion)
- **Evidence Available For Later Extraction:**
  - task intent: YES (Depth: 2 constraint in IntentSlots)
  - initial state: YES (API-35 Settings home)
  - observation: YES (mock vision)
  - action: YES (mock action)
  - disturbance: NO (the bug was the disturbance — depth constraint not propagated)
  - outcome: YES (stops at depth=2, never enters Wi-Fi at depth=3)
  - failure: YES (pre-fix: would enter depth=3; post-fix: stopped at depth=2)
  - expected behavior: YES (depth constraint honored)

---

### E-12 — ContainerGatewayTests (False completion: scroll-only dead-end, unconsumed FrameCompleted)

- **Evidence ID:** E-12
- **Title:** ContainerGatewayTests — false completion patterns: dead-end only on scroll-failure branch, non-root FrameCompleted unconsumed → permanently stuck
- **Source:** `tests/UniClaw.Core.Tests/Traversal/ContainerGatewayTests.cs`
- **Family:** FAILURE / REGRESSION
- **Priority:** P2
- **EvidenceValue:** 8 (R=1, F=2, C=2, U=2, T=1)
- **Why Selected:** Encodes two distinct false-completion patterns not covered by E-07 (MultiBranchNavigation). Pattern 1: old behavior — dead-end completion only triggered on the scroll-failure branch, causing infinite scroll until `MaxSteps` exhausted (now AllVisited via content stability, K=3). Pattern 2: old behavior — non-root `FrameCompleted` unconsumed → child frame permanently stuck → MaxSteps. These are container/navigation-level false completion bugs distinct from the branch-loss bug in E-07.
- **Executable:** YES (plain `[Fact]`, always runs)
- **Fixture / Trace:** Container navigation `StateFixture`, `StatefulMockVisionService`/`StatefulMockActionExecutor`, `TraversalEngine`
- **Historical Failure Link:** Real bugs: infinite scroll from scroll-only dead-end detection; stuck child frames from unconsumed FrameCompleted
- **Supporting Evidence:** F-18 (completion accounting contract never specified), F-05 (ContainerGatewayTests full description), `docs/refactor/2026-07-30-deliver-safe-settings-spec-defect-analysis.md`
- **Evidence Available For Later Extraction:**
  - task intent: PARTIAL (container traversal)
  - initial state: YES (page with scrollable container + child frames)
  - observation: YES (mock vision)
  - action: YES (mock action)
  - disturbance: YES (scroll-failure branch, unconsumed FrameCompleted)
  - outcome: YES (false completion vs correct AllVisited/MaxSteps)
  - failure: YES (infinite scroll, permanently stuck child frame)
  - expected behavior: YES (content stability K=3 for scroll termination; FrameCompleted must be consumed)

---

### E-13 — GAP-P0-02 (EntryPolicy fake success + ADB scroll failure misclassified as "reached end")

- **Evidence ID:** E-13
- **Title:** GAP-P0-02 — documented behavioral gaps: EntryPolicy is fake success; ADB scroll failure folds into "reached end"
- **Source:** `docs/prd/2026-07-29-local-implementation-gap-prd.md`
- **Family:** FAILURE / REGRESSION
- **Priority:** P1
- **EvidenceValue:** 6 (R=1, F=2, C=1, U=2, T=0)
- **Why Selected:** **MANDATORY INCLUSION — Cases where documentation and executable behavior disagree.** Documents two real behavioral gaps that affect correctness: (1) EntryPolicy never executes actual device operations — it returns fake success (`docs/prd/2026-07-29-local-implementation-gap-prd.md` GAP-P0-02), meaning cold-start / app-launch failures are invisible to the engine. (2) ADB scroll failure at `AdbScreenStateProvider.cs:38` folds into "reached end" status — the engine cannot distinguish "nothing more to scroll" from "scroll command failed." Both gaps mean the engine can report success (AllVisited, TargetFound) when real device state is unknown. Also documents 429/5xx/timeout bounded retry for AI provider calls. This is documentation evidence, not an executable test — traceability is weak but the behavioral gap is real and severe.
- **Executable:** NO (documentation claim; references production code at `AdbScreenStateProvider.cs:38`)
- **Fixture / Trace:** None directly; references `src/UniClaw.Host/Device/AdbScreenStateProvider.cs:38`
- **Historical Failure Link:** Documented as GAP-P0-02 in PRD
- **Supporting Evidence:** F-21 (same document, additional gaps), F-23 (android-emulator.md — device/ADB failures are not no-scroll or end-of-list), IT-06 (EmulatorScenarioIntegrationTests — where these gaps would manifest), `src/UniClaw.Core/Traversal/InterceptionHandler.cs:469` (content guard false positive)
- **Evidence Available For Later Extraction:**
  - task intent: YES (entry policy, scroll-to-end)
  - initial state: YES (device app state)
  - observation: PARTIAL (scroll failure indistinguishable from end-of-list)
  - action: YES (EntryPolicy fake success, ADB scroll)
  - disturbance: YES (ADB scroll failure, cold-start failure)
  - outcome: YES (false success, false "reached end")
  - failure: YES (undetected infrastructure failures)
  - expected behavior: YES (EntryPolicy should execute real device ops; scroll failure ≠ end-of-list)

---

### E-14 — PlanCompiler (Deterministic IntentSlots → TraversalPlan, no AI)

- **Evidence ID:** E-14
- **Title:** PlanCompiler — deterministic 5-step IntentSlots → TraversalPlan compiler
- **Source:** `src/UniClaw.Core/Graph/Services/PlanCompiler.cs`
- **Family:** INTENT / GOAL / PLAN
- **Priority:** P2
- **EvidenceValue:** 7 (R=1, F=1, C=2, U=2, T=1)
- **Why Selected:** **MANDATORY INCLUSION — Intent / Goal / Plan construction evidence.** The legacy system's plan construction is split into two modes: (1) AI-driven IntentExtractor → IntentSlots → PlanCompiler (intent mode), and (2) hand-authored plan JSON → ScenarioPlanLoader (plan mode). PlanCompiler is the central deterministic compiler: 5-step `Compile()` = ValidateSlots → BuildEntryPolicy → BuildRootNode → BuildCompletionPolicy → assemble. `TemplateSets` keyed by `ElementHandling` (full_interaction/menu_only/safe_mode/read_only). Defaults: `DefaultCompletionTimeoutSeconds=300`, `DefaultCompletionMaxSteps=500`, `EntryTimeoutSeconds=10`. "Single source of truth" for plan semantics. Dormant in baseline (Change A preventive fix not yet applied — `docs/refactor/2026-07-19-plancompiler-default-alignment-design.md`). Proves: the legacy system had a deterministic plan construction path, but it was dormant and had known alignment issues.
- **Executable:** YES (production code; test coverage via IP-11 GraphTests, IP-12 FailFastValidationBaselineTests)
- **Fixture / Trace:** `IntentSlots` input → `TraversalPlan` output; `TemplateSets` by ElementHandling; `MatchConditions`
- **Historical Failure Link:** IP-16 documents dormant preventive correctness fix (field misreads, vocabulary misalignment, default drift: timeout 60↔300s, DirectDeeplink↔ColdLaunch, target_path scope NONE↔TargetFound)
- **Supporting Evidence:** IP-02 (IPlanCompiler interface), IP-03 (TraversalPlan/IntentSlots data model), IP-05 (ScenarioPlanCompiler — Host orchestration), IP-07 (TraversalEngine.CompilePlan — plan→node tree), IP-11 (GraphTests — only production instantiator), IP-12 (FailFastValidationBaselineTests — validation), IP-14 (pipeline diagram), IP-16 (dormant fix)
- **Evidence Available For Later Extraction:**
  - task intent: YES (IntentSlots → TraversalPlan transformation)
  - initial state: N/A (pure data transformation)
  - observation: NO
  - action: NO
  - disturbance: NO
  - outcome: YES (complete TraversalPlan with EntryPolicy, root node, CompletionPolicy)
  - failure: YES (fail-fast validation: zero timeout, excessive entry timeout, invalid scope)
  - expected behavior: YES (TemplateSets define expected behavior by ElementHandling mode)

---

### E-15 — IntentExtractor (AI-driven NL → IntentSlots extraction)

- **Evidence ID:** E-15
- **Title:** IntentExtractor — AI-driven natural-language scenario description → IntentSlots extraction
- **Source:** `src/UniClaw.Core/UniBrain/IntentExtractor.cs`
- **Family:** INTENT / GOAL / PLAN
- **Priority:** P2
- **EvidenceValue:** 7 (R=1, F=1, C=1, U=2, T=2)
- **Why Selected:** **MANDATORY INCLUSION — Intent / Goal / Plan construction evidence.** The AI-driven path for Intent construction. Uses `IModelProvider` (DeepSeek flash) to extract `ExtractedIntentSlots` from natural-language scenario descriptions. Infers: Scope, ElementHandling, Navigation, Restore, Completion. Factual fields (TargetApp, Target, Depth, Entry) come from the caller (ScenarioPlanCompiler), not from AI. JSON schema aligned with `PromptTemplateRegistry.ExtractIntent`. Proves: NL → structured intent extraction exists but is probabilistic (AI-dependent); factual constraints are caller-supplied, not AI-inferred.
- **Executable:** YES (production code; test coverage via IP-09 IntentExtractorTests — 14 tests)
- **Fixture / Trace:** `IModelProvider` (DeepSeek flash), `PromptTemplateRegistry.ExtractIntent` template, `ExtractedIntentSlots` output schema
- **Historical Failure Link:** No specific failure; the probabilistic nature of AI extraction is itself a source of uncertainty
- **Supporting Evidence:** IP-04 (full class description), IP-09 (IntentExtractorTests — 14 tests), IP-05 (ScenarioPlanCompiler — caller), IP-14 (pipeline diagram showing position in chain)
- **Evidence Available For Later Extraction:**
  - task intent: YES (NL scenario description → ExtractedIntentSlots)
  - initial state: N/A (pure NL → structured data)
  - observation: NO
  - action: NO
  - disturbance: NO (AI uncertainty is implicit)
  - outcome: YES (Scope, ElementHandling, Navigation, Restore, Completion)
  - failure: PARTIAL (AI can return wrong/missing fields)
  - expected behavior: YES (JSON schema defines valid output shape)

---

### E-16 — ScenarioPlanLoader (Plan mode: hand-authored Static plan JSON)

- **Evidence ID:** E-16
- **Title:** ScenarioPlanLoader — plan mode: hand-authored plan JSON → executable TraversalPlan with Static nodes
- **Source:** `src/UniClaw.Host/Runner/ScenarioPlanLoader.cs`
- **Family:** INTENT / GOAL / PLAN
- **Priority:** P2
- **EvidenceValue:** 6 (R=1, F=0, C=1, U=2, T=1)
- **Why Selected:** **MANDATORY INCLUSION — Intent / Goal / Plan construction evidence.** Proves the legacy system had TWO distinct plan construction modes, as documented in `docs/prd/2026-07-30-runner-through-engine-design.md`: "Plan mode ≠ Intent mode, But Both Use the FSM." Plan mode = Static + StaticNodes (data, not code); Intent mode = DynamicMatch (PlanCompiler output). `ScenarioPlanLoader.Load(planJson)` materializes hand-authored JSON into executable `TraversalPlan` with `JsonElement` coordinates resolved to `Coordinate` for `OperationDispatcher`. Proves: plan mode bypasses IntentExtractor and PlanCompiler entirely — it's a direct data→execution path.
- **Executable:** YES (production code; exercised by IT-06 EmulatorScenarioIntegrationTests in locate mode)
- **Fixture / Trace:** Hand-authored plan JSON (e.g., `scenarios/android-settings/locate-one-item.v1.json`), `Static` + `StaticNodes` TraversalNode type
- **Historical Failure Link:** No specific failure
- **Supporting Evidence:** IP-06 (full class description), IP-15 (runner-through-engine-design.md — "Plan mode ≠ Intent mode"), IT-06 (locate scenario uses plan JSON)
- **Evidence Available For Later Extraction:**
  - task intent: YES (hand-authored plan JSON with explicit target/actions)
  - initial state: YES (device app state)
  - observation: NO (plan mode assumes specific page structure)
  - action: YES (explicit action sequence from JSON)
  - disturbance: NO
  - outcome: YES (target found or enumeration complete)
  - failure: PARTIAL (if JSON coordinates don't match real screen)
  - expected behavior: YES (plan JSON defines exact expectations)

---

### E-17 — ITraversalAdvisor (Goal-directed dynamic next-action generation)

- **Evidence ID:** E-17
- **Title:** ITraversalAdvisor — "Given a goal, decide the single next action": goal-directed dynamic action generation
- **Source:** `src/UniClaw.Core/UniBrain/ITraversalAdvisor.cs` + `TraversalAdvisor.cs`
- **Family:** INTENT / GOAL / PLAN
- **Priority:** P2
- **EvidenceValue:** 6 (R=1, F=1, C=1, U=2, T=1)
- **Why Selected:** **MANDATORY INCLUSION — Intent / Goal / Plan construction evidence.** The only "Goal" concept in the legacy C# codebase. `DecideNextActionAsync(string goal, ...)` takes a goal string, current page analysis, current node ID, and depth, and returns the single next action that "best advances the goal." Uses `PromptTemplateRegistry.decide_next_action` template. No standalone `Goal` class exists — "goal" is purely a prompt variable. This is the dynamic/online decision-making counterpart to the offline PlanCompiler. Proves: the legacy system had a goal-directed action selection mechanism that was entirely AI-driven and stateless (no goal persistence across steps).
- **Executable:** YES (production code; test coverage via `TraversalAdvisorTests.cs`)
- **Fixture / Trace:** `IModelProvider`, `PromptTemplateRegistry.decide_next_action` template with variables: goal, page_analysis, current_node_id, depth
- **Historical Failure Link:** No specific failure; the stateless nature of goal-as-prompt-variable is itself a design pressure
- **Supporting Evidence:** IP-08 (full class description), `TraversalAdvisorTests.cs` (DecideNextActionAsync_ModelFailure_ThrowsWithError), `MockTraversalAdvisor.cs` (test double)
- **Evidence Available For Later Extraction:**
  - task intent: YES (goal string → single next action)
  - initial state: YES (current page analysis, current node ID, depth)
  - observation: YES (page_analysis prompt variable)
  - action: YES (single next action output)
  - disturbance: YES (model failure throws, AI uncertainty)
  - outcome: YES (next action + rationale)
  - failure: YES (model failure → exception)
  - expected behavior: PARTIAL (goal-directed but stateless — no persistent goal tracking)

---

### E-18 — Python task_parser.py (NL task → IntentSlots, no C# equivalent)

- **Evidence ID:** E-18
- **Title:** Python task_parser.py — NL task → IntentSlots parsing with no C# equivalent
- **Source:** `docs/refactor/2026-07-15-python-csharp-gap-triage.md` (C-5) + `docs/prd/2026-07-29-local-implementation-gap-prd.md:246`
- **Family:** INTENT / GOAL / PLAN
- **Priority:** P1
- **EvidenceValue:** 5 (R=0, F=1, C=1, U=2, T=1)
- **Why Selected:** **MANDATORY INCLUSION — Python task_parser.py NL→IntentSlots evidence.** Documents a capability gap: the Python reference implementation has `src/ai/task_parser.py` (NL task → IntentSlots) that has no C# equivalent. The C# method `UnderstandTextAsync` exists but has only test callers — no production consumer. This is evidence of an unimplemented link in the intent chain: Python could parse NL tasks into structured intent slots, but the C# port never completed that link. The existing C# IntentExtractor is a different mechanism (AI-driven extraction via model provider, not a deterministic parser).
- **Executable:** NO (Python code exists but is not in the C# codebase; C# `UnderstandTextAsync` has only test callers)
- **Fixture / Trace:** Python `src/ai/task_parser.py` (on feature/refactor? — verify existence); C# `UnderstandTextAsync` method
- **Historical Failure Link:** Documented as C-5 gap in `docs/refactor/2026-07-15-python-csharp-gap-triage.md`
- **Supporting Evidence:** IP-17 (full gap description), IP-15 (runner-through-engine-design.md), `docs/refactor/12-python-csharp-design-gaps.md`
- **Evidence Available For Later Extraction:**
  - task intent: YES (NL task → structured IntentSlots)
  - initial state: N/A (text transformation)
  - observation: NO
  - action: NO
  - disturbance: NO
  - outcome: YES (structured IntentSlots)
  - failure: YES (unimplemented in C# — capability gap)
  - expected behavior: YES (Python reference defines expected input/output behavior)

---

## Supporting Evidence

### Integration Supporting

| Evidence ID | Source | Supports | Reason |
|---|---|---|---|
| S-01 | IT-05 `AdbVisionActionIntegrationTests.cs` | E-01 | Vision+ADB closed loop; cross-component behavior supplementing full-stack scenario |
| S-02 | IT-02 `VisionGoldenIntegrationTests.cs` | E-01 | Observation quality evidence: human-reviewed golden comparison |
| S-03 | IT-10 `IntegrationConfigTests.cs` | Evidence-quality | **MANDATORY INCLUSION #10** — config/test mismatch: expects `"warning"`, config has `"information"`. Evidence-quality / consistency evidence. |

### Simulation Supporting

| Evidence ID | Source | Supports | Reason |
|---|---|---|---|
| S-04 | SIM-01 `SimulationE2ETests.cs` | E-03 | MaxSteps failure + empty-area tap failure cases supplement full traversal evidence |
| S-05 | SIM-08 `ExpectedBehaviorElementCoverageTests.cs` | E-03, E-07 | 6/8 FAIL-path verification semantics; supplements completion evidence |
| S-06 | SIM-12 `LongListBaselineTests.cs` | E-06 | Sparse/dense/jump scroll profiles supplement WiFi list scroll evidence |
| S-07 | SIM-11 `HierarchyBaselineTests.cs` | E-06, E-03 | 4-level nav with 3 scrollable pages; bridges nav + scroll evidence |

### Simulation Replay Supporting

| Evidence ID | Source | Supports | Reason |
|---|---|---|---|
| S-08 | R2 TraceTool `Fixtures/success/` + `Fixtures/failure/` | E-08, E-09, E-10 | Committed real-run trace snapshots; offline analysis evidence for failure patterns |
| S-09 | R1 `SettingsEnumerateRegression.cs` | E-11 | Depth constraint regression; supplements F-02 depth evidence |

### Failure / Regression Supporting

| Evidence ID | Source | Supports | Reason |
|---|---|---|---|
| S-10 | F-06 `TraversalEngineTests.cs` (MaxSteps/Timeout/EntryPolicy) | E-03, E-04 | Termination path evidence: MaxSteps, Timeout, no fake success |
| S-11 | F-07 `HandleErrorHandlingTests.cs` (Bug #2) | E-04 | Execute throws → ErrorHandling without incrementing consecutive-error count |
| S-12 | F-11 `ErrorHandler.cs` + `ErrorContext.cs` (production) | E-04 | Error classification + recovery strategy implementation reference |
| S-13 | F-18 `spec-defect-analysis.md` | E-07, E-12 | Completion accounting contract never specified — design-level evidence |
| S-14 | F-20 `decisions/log.md:1094` | E-09, E-11 | Stale DynamicMatch cache → max_steps exhaustion; fingerprint-aware caching fix |
| S-15 | F-22 `python-csharp-design-gaps.md` (F-1) | E-13 | Permission popup C# auto_close vs Python timeout; enum drift — cross-implementation behavioral difference |
| S-16 | F-23 `android-emulator.md:141` | E-13 | Device/ADB failures are not no-scroll or end-of-list — failure classification evidence |
| S-17 | F-17 `fsm-analyzer-memory/knowledge.md` (D-244) | E-04 | Popup dismiss failure; ErrorClassifier substring matching misclassification risk |

### Intent / Goal / Plan Supporting

| Evidence ID | Source | Supports | Reason |
|---|---|---|---|
| S-18 | IP-02 `IPlanCompiler.cs` | E-14 | Interface definition for PlanCompiler |
| S-19 | IP-03 `TraversalPlan.cs` / `IntentSlots` | E-14 | Data model: CompletionPolicyType, IntentSlots fields with fail-fast validation |
| S-20 | IP-05 `ScenarioPlanCompiler.cs` | E-14, E-15 | Host orchestration: resolves IntentSlots → PlanCompiler → apply narrowing/exclude |
| S-21 | IP-07 `TraversalEngine.CompilePlan` | E-14 | Plan → node-tree compilation; EntryPolicy/CompletionPolicy/IntentSlots.Depth consumption |
| S-22 | IP-09 `IntentExtractorTests.cs` (14 tests) | E-15 | Test evidence for AI intent extraction |
| S-23 | IP-11 `GraphTests.cs` | E-14 | Only production instantiator of PlanCompiler |
| S-24 | IP-12 `FailFastValidationBaselineTests.cs` | E-14 | Plan validation: zero timeout, excessive entry timeout, scope validation |
| S-25 | IP-14 `ai-plan-optimization-hints.md` | E-14, E-15 | Canonical pipeline diagram: 场景描述 → AI IntentExtractor → IntentSlots → PlanCompiler → TraversalPlan → 仿真/执行 |
| S-26 | IP-15 `runner-through-engine-design.md` | E-16 | "Plan mode ≠ Intent mode, But Both Use the FSM" |
| S-27 | IP-16 `plancompiler-default-alignment-design.md` | E-14 | Dormant preventive correctness fix: field misreads, vocabulary misalignment, default drift |
| S-28 | F-19 `current-internal-gaps-calibrated.md:49` | E-14, E-15 | Coverage gap: UIAutomator-vs-AI observation source equivalence unbridged |

---

## Deferred Evidence

| Evidence ID | Source | Reason |
|---|---|---|
| D-01 | IT-01 `RealVisionIntegrationTests.cs` | Single happy-path smoke test; observation evidence covered by S-02 (IT-02 VisionGolden) |
| D-02 | IT-03 `AdbRealDeviceIntegrationTests.cs` | Narrow device boundary (serial/screencap); device evidence covered by E-01, E-02 |
| D-03 | IT-07 `FSMIntegrationTests.cs` | In-process FSM cycle; redundant with E-04 (FsmSimulationRegressionTests) which adds fault injection |
| D-04 | SIM-11 `HierarchyBaselineTests.cs` | 4-level nav + scroll; nav evidence covered by E-03, scroll by E-06 |
| D-05 | F-08 `TextTargetResolutionTests.cs` | Implementation-level matching issue (Contains matching); not behavioral pressure |
| D-06 | F-09 `VerifyEngineTests.cs` (empty item match) | Offline verification tooling issue; not behavioral pressure |
| D-07 | F-12 `PopupHandler.cs` (production) | Implementation detail; popup handling covered by E-04 popup retry test |
| D-08 | F-14 `FileTraceStorage.cs` (corrupted line tolerance) | Observability infrastructure; not behavioral evidence |
| D-09 | F-15 `InterceptionHandler.cs:469` (content guard) | Implementation detail for false positive handling |
| D-10 | F-16 `.test_fix_log.md` | Historical decision record; not primary behavioral evidence |
| D-11 | R3 `sim-replay-viewer.py` | Visualization tooling only; not behavioral evidence |

---

## Rejected Low-Value

| Evidence ID | Source | Reason |
|---|---|---|
| R-01 | IT-08 `TraceSpanScopeIntegrationTests.cs` | Implementation detail (span push/pop); P5 pure implementation test |
| R-02 | IT-09 `LoggingIntegrationTests.cs` | Infrastructure (logging assembly); device-free, no behavioral evidence |
| R-03 | IT-11 `ProviderPreflightTests.cs` | Infrastructure (credential checks); no behavioral evidence |
| R-04 | SIM-03 `StateFixtureTests.cs` | Pure model unit tests; P5 implementation test |
| R-05 | SIM-04 `StatefulMockActionTests.cs` | Mock unit tests; P5 implementation test |
| R-06 | SIM-05 `StatefulMockVisionTests.cs` | Mock unit tests; P5 implementation test |
| R-07 | SIM-06 `MockModelProviderTests.cs` | AI transport mock unit tests; P5 implementation test |
| R-08 | SIM-07 `PagedContentAndScreenTests.cs` | Scroll model unit tests; behavior covered by E-06, S-06 |
| R-09 | F-10 Host/CLI failure-path tests | Tool/infrastructure failure tests; no behavioral evidence |
| R-10 | F-13 `TraceHandlerGenerator.Emitter.cs` | Codegen infrastructure; no behavioral evidence |
| R-11 | R4 `MockModelProvider` replay | Synthetic presets, no real evidence; TEST_HELPER only |

---

## Integration Evidence Detail

| Item | Disposition | Priority | Score | Key Reason |
|---|---|---|---|---|
| IT-01 RealVisionIntegrationTests | DEFER (D-01) | P4 | 4 | Single happy-path smoke; covered by IT-02 golden |
| IT-02 VisionGoldenIntegrationTests | SUPPORTING (S-02) | P3 | 6 | Observation quality evidence; human-reviewed golden |
| IT-03 AdbRealDeviceIntegrationTests | DEFER (D-02) | P3 | 5 | Narrow device boundary; covered by E-01, E-02 |
| IT-04 AdbSessionIntegrationTests | **PRIMARY (E-02)** | P1 | 8 | Only test that kills external service; self-healing evidence |
| IT-05 AdbVisionActionIntegrationTests | SUPPORTING (S-01) | P2 | 6 | Vision+ADB closed loop; supplements E-01 |
| IT-06 EmulatorScenarioIntegrationTests | **PRIMARY (E-01)** | P0 | 10 | Deepest external-world test; full production composition |
| IT-07 FSMIntegrationTests | DEFER (D-03) | P4 | 5 | Redundant with E-04 fault injection suite |
| IT-08 TraceSpanScopeIntegrationTests | REJECT (R-01) | P5 | 1 | Implementation detail |
| IT-09 LoggingIntegrationTests | REJECT (R-02) | P5 | 3 | Infrastructure |
| IT-10 IntegrationConfigTests | SUPPORTING (S-03) | — | — | Mandatory inclusion #10: config/test mismatch |
| IT-11 ProviderPreflightTests | REJECT (R-03) | P5 | 3 | Infrastructure |

---

## Simulation Evidence Detail

| Item | Disposition | Priority | Score | Key Reason |
|---|---|---|---|---|
| SIM-01 SimulationE2ETests | SUPPORTING (S-04) | P3 | 5 | MaxSteps + empty-area tap failure cases |
| SIM-02 SimulationBaselineTests | **PRIMARY (E-03)** | P2 | 7 | Strongest representative: 7-page Settings + ExpectedBehavior |
| SIM-03 StateFixtureTests | REJECT (R-04) | P5 | — | Pure model unit tests |
| SIM-04 StatefulMockActionTests | REJECT (R-05) | P5 | — | Mock unit tests |
| SIM-05 StatefulMockVisionTests | REJECT (R-06) | P5 | — | Mock unit tests |
| SIM-06 MockModelProviderTests | REJECT (R-07) | P5 | — | AI transport mock unit tests |
| SIM-07 PagedContentAndScreenTests | REJECT (R-08) | P5 | 2 | Scroll model unit tests; covered by E-06 |
| SIM-08 ExpectedBehaviorElementCoverageTests | SUPPORTING (S-05) | P2 | 6 | 6/8 FAIL-path verification semantics |
| SIM-09 FsmSimulationRegressionTests | **PRIMARY (E-04)** | P2 | 8 | Only systematic fault injection evidence |
| SIM-10 AIIntentSimulationTests | **PRIMARY (E-05)** | P2 | 8 | Only full Intent→Plan→Execution chain evidence |
| SIM-11 HierarchyBaselineTests | DEFER (D-04) | P3 | 7 | Nav+scroll overlap; covered by E-03 + E-06 |
| SIM-12 LongListBaselineTests | SUPPORTING (S-06) | P3 | 6 | Supplements E-06 scroll evidence |
| SIM-13 ScrollableBaselineTests | **PRIMARY (E-06)** | P2 | 7 | Strongest scroll evidence: 7 scenarios + content profiles |
| SIM-14 MultiBranchNavigationTests | **PRIMARY (E-07)** | P0 | 9 | UNFIXED BUG: false AllVisited |

---

## Simulation Replay Evidence Detail

| Item | Disposition | Priority | Score | Key Reason |
|---|---|---|---|---|
| R1 TraceReplayFromRunTests | **PRIMARY (E-08)** | P0 | 10 | Record→replay→diagnose→fix cycle with real artifacts |
| R1 FixVerificationTests | **PRIMARY (E-09)** | P0 | 10 | 8-layer regression verification; each layer = distinct bug class |
| R1 TraceReplay_20260805T052309367Z | **PRIMARY (E-10)** | P0 | 10 | Concrete real-run failure reproduction |
| R1 SettingsEnumerateRegression | SUPPORTING (S-09) | P2 | 6 | Supplements E-11 depth evidence |
| R2 TraceTool snapshots | SUPPORTING (S-08) | P1 | 7 | Committed real-run trace evidence; offline analysis |
| R3 sim-replay-viewer.py | DEFER (D-11) | — | — | Visualization tooling; not behavioral evidence |
| R4 MockModelProvider replay | REJECT (R-11) | P5 | — | Synthetic presets; TEST_HELPER only |

---

## Failure / Regression Evidence Detail

| Item | Disposition | Priority | Score | Key Reason |
|---|---|---|---|---|
| F-01 FsmSimulationRegressionTests | → E-04 PRIMARY | — | — | Same item as SIM-09 |
| F-02 SettingsEnumerateRegression | **PRIMARY (E-11)** | P2 | 7 | Depth constraint violation; permanent deterministic regression |
| F-03 FixVerificationTests | → E-09 PRIMARY | — | — | Same item as R1 FixVerificationTests |
| F-04 MultiBranchNavigationTests | → E-07 PRIMARY | — | — | Same item as SIM-14 |
| F-05 ContainerGatewayTests | **PRIMARY (E-12)** | P2 | 8 | Two distinct false-completion patterns |
| F-06 TraversalEngineTests (MaxSteps/Timeout) | SUPPORTING (S-10) | P3 | 5 | Termination path evidence |
| F-07 HandleErrorHandlingTests (Bug #2) | SUPPORTING (S-11) | P3 | 6 | Error counting bug; supplements E-04 |
| F-08 TextTargetResolutionTests | DEFER (D-05) | P5 | 3 | Implementation-level matching issue |
| F-09 VerifyEngineTests (empty match) | DEFER (D-06) | P5 | 4 | Offline tooling issue |
| F-10 Host/CLI failure-path tests | REJECT (R-09) | P5 | 2 | Tool/infrastructure tests |
| F-11 ErrorHandler/ErrorContext (prod) | SUPPORTING (S-12) | — | — | Implementation reference for E-04 |
| F-12 PopupHandler (prod) | DEFER (D-07) | — | — | Implementation detail |
| F-13 TraceHandlerGenerator (prod) | REJECT (R-10) | — | — | Codegen infrastructure |
| F-14 FileTraceStorage (prod) | DEFER (D-08) | — | — | Observability infrastructure |
| F-15 InterceptionHandler (prod) | DEFER (D-09) | — | — | Implementation detail |
| F-16 .test_fix_log.md | DEFER (D-10) | — | — | Historical record |
| F-17 fsm-analyzer-memory/knowledge.md | SUPPORTING (S-17) | — | — | Misclassification risk supplements E-04 |
| F-18 spec-defect-analysis.md | SUPPORTING (S-13) | — | — | Design-level false-completion evidence |
| F-19 current-internal-gaps-calibrated.md | SUPPORTING (S-28) | — | — | Observation gap; supplements intent evidence |
| F-20 decisions/log.md | SUPPORTING (S-14) | — | — | Stale cache exhaustion; supplements E-09, E-11 |
| F-21 local-implementation-gap-prd.md | **PRIMARY (E-13)** | P1 | 6 | GAP-P0-02: fake success + misclassified failure |
| F-22 python-csharp-design-gaps.md | SUPPORTING (S-15) | — | — | Cross-implementation behavioral difference |
| F-23 android-emulator.md | SUPPORTING (S-16) | — | — | Device failure classification |

---

## Intent / Goal / Plan Evidence Detail

| Item | Disposition | Type | Key Reason |
|---|---|---|---|
| IP-01 PlanCompiler | **PRIMARY (E-14)** | PRODUCTION_IMPLEMENTATION | Central deterministic IntentSlots → TraversalPlan compiler |
| IP-02 IPlanCompiler | SUPPORTING (S-18) | PRODUCTION_IMPLEMENTATION | Interface for E-14 |
| IP-03 TraversalPlan/IntentSlots | SUPPORTING (S-19) | PRODUCTION_IMPLEMENTATION | Data model for E-14 |
| IP-04 IntentExtractor | **PRIMARY (E-15)** | PRODUCTION_IMPLEMENTATION | AI-driven NL → IntentSlots |
| IP-05 ScenarioPlanCompiler | SUPPORTING (S-20) | PRODUCTION_IMPLEMENTATION | Host orchestration for E-14/E-15 |
| IP-06 ScenarioPlanLoader | **PRIMARY (E-16)** | PRODUCTION_IMPLEMENTATION | Plan mode: Static → TraversalPlan |
| IP-07 TraversalEngine.CompilePlan | SUPPORTING (S-21) | PRODUCTION_IMPLEMENTATION | Plan → node-tree for E-14 |
| IP-08 ITraversalAdvisor | **PRIMARY (E-17)** | PRODUCTION_IMPLEMENTATION | Goal-directed dynamic action generation |
| IP-09 IntentExtractorTests | SUPPORTING (S-22) | TEST | Test evidence for E-15 |
| IP-10 AIIntentSimulationTests | → E-05 PRIMARY | TEST | Same item as SIM-10 |
| IP-11 GraphTests | SUPPORTING (S-23) | TEST | PlanCompiler instantiation for E-14 |
| IP-12 FailFastValidationBaselineTests | SUPPORTING (S-24) | TEST | Plan validation for E-14 |
| IP-13 CompletionPolicyTests | → S-10 | TEST | Covered by F-06 supporting |
| IP-14 ai-plan-optimization-hints.md | SUPPORTING (S-25) | DESIGN_DOCUMENT | Pipeline diagram for E-14/E-15 |
| IP-15 runner-through-engine-design.md | SUPPORTING (S-26) | DESIGN_DOCUMENT | Plan mode ≠ Intent mode for E-16 |
| IP-16 plancompiler-default-alignment-design.md | SUPPORTING (S-27) | DESIGN_DOCUMENT | Dormant fix for E-14 |
| IP-17 python-csharp-gap-triage.md (C-5) | **PRIMARY (E-18)** | DESIGN_DOCUMENT | Python task_parser.py has no C# equivalent |

---

## Evidence Gaps

The following categories are **not directly represented** in the selected primary evidence:

1. **Resume / pause behavior** — The inventory notes `TraversalEnginePauseResumeTests.cs` under "other test files consuming the Simulation namespace" but it was not included in the 14 simulation suites. If this contains unique behavioral evidence for resume-after-disturbance, it should be evaluated in a follow-up.

2. **Python reference implementation executable behavior** — E-18 is a documentation pointer to a gap, not an executable artifact. The actual Python `task_parser.py` input/output behavior may need direct inspection if the gap becomes relevant to Runtime design.

3. **Legacy Harness fault cases** — `RunnerTestHarness.cs` (stale plan, wrong-page back, dangerous-skip, scroll-stuck) was deferred as migration-only. If any of these fault cases encode unique behavioral pressure not covered by the selected simulation/regression evidence, they may need later inclusion.

4. **ArchitectureGuardTests** — Deferred as code-structure constraints, not behavioral evidence. If any guard encodes a semantic invariant (e.g., "engine must not depend on Host"), it may become relevant for architecture boundary extraction.

---

## Readiness

**EVIDENCE_SET_READY_FOR_NORMALIZATION**

18 primary items selected across all 5 families. All 10 mandatory inclusion categories covered. Supporting evidence linked. Deferred and rejected items documented with rationale.

---

## Repository Changes

NONE
