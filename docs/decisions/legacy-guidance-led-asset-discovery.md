# LEGACY_GUIDANCE_LED_ASSET_DISCOVERY_RESULT

> Generated: 2026-08-09
> Role: Legacy Knowledge / Asset Cartographer — mode READ_ONLY / DISCOVERY_ONLY
> Roadmap position: `PHASE_B_REALITY_MODEL_FOUNDATION` / B1.5 (pre-B2)
> Legacy source: branch `feature/refactor` (read only through Git objects; not checked out, not modified)
> Purpose: reconstruct the legacy repository's own knowledge map so that `LEGACY_REALITY_MODEL_EXTRACTION` (B2) navigates by legacy-native paths instead of guessed keywords

---

## Pass 1 — Guidance Roots

### Guidance Root Records

| ID | Path | Type | Scope |
|---|---|---|---|
| GR-01 | `AGENTS.md` (root) | agent guide | Shared project guide (architecture, doc system, conventions, OpenSpec flow, MCP-first rule) |
| GR-02 | `CLAUDE.md` (root) | agent adapter | Claude entry; points to AGENTS.md and `.claude/` extensions |
| GR-03 | `.claude/WORKFLOWS_AND_SKILLS.md` | skill/workflow index | 18 skills + 9 workflows catalog; per-skill authority is the skill's own frontmatter |
| GR-04 | `.claude/MCP-QUERY.md` | tooling guide | C# symbol query doctrine (MCP first, grep forbidden) |
| GR-05 | `docs/testing/test-tiers.md` | testing doctrine | Three-tier test doctrine: Legacy Harness (stateless) / Simulation (stateful virtual device) / Explicit Integration |
| GR-06 | `docs/testing/integration-tests.md` | integration guide | 8-level explicit integration scope ladder (vision-smoke → scenario-enumerate) |
| GR-07 | `docs/testing/integration-config.md` | run config guide | L0–L6 config layers, `integration.config.json` as single source of truth |
| GR-08 | `docs/testing/integration-pipeline-issues.md` | pipeline issue register | P1.1–P5 integration pipeline problems and fix status |
| GR-09 | `docs/testing/android-emulator.md` + `scripts/android-emulator.sh` + `scripts/dev-doctor.sh` | device/emulator guide | AVD `uniclaw-lite-api35`, doctor probes, run smoke sequence |
| GR-10 | `scripts/sim-replay-viewer.py` | replay tool guide | Replay JSON or run dir → self-contained HTML visualization |
| GR-11 | `scripts/log-analyzer.py` | log tool guide | run.log → table / timeline / mermaid / metrics / compare |
| GR-12 | `.claude/skills/trace-analysis/SKILL.md` + `.claude/agents/trace-analyzer.md` | trace analysis guidance | TraceTool CLI contract, run-dir layout, diagnosis methodology |
| GR-13 | `.claude/skills/trace-to-simulation/SKILL.md` | replay mechanism guide | Real run artifacts → reproducible FSM simulation test (5 phases, output to `Simulation/TraceReplay/`) |
| GR-14 | `.claude/skills/host-test-runner/SKILL.md` | e2e run guide | Host integration test lifecycle (env → execute → monitor → report), evidence-gap checklist |
| GR-15 | `.claude/skills/e2e-diagnose/SKILL.md` | diagnosis orchestration guide | Execute → metrics → trace-analyzer (mandatory) → fsm-analyzer/local-vision-analyzer (conditional) → fix report |
| GR-16 | `.claude/agents/fsm-analyzer.md` + memory | FSM source-first analysis | TraversalFSM/GlobalFSM matrices, handler pipeline, run.log grep recipes, script library |
| GR-17 | `.claude/agents/shadow-fsm-analyzer.md` + memory | requirements-first analysis | Independent FSM re-derivation (blind to source), battle-log, `fsm-design.md` |
| GR-18 | `.claude/agents/local-vision-analyzer.md` + memory | vision pipeline analysis | YOLO→OCR pipeline, quality dimensions, analysis.jsonl forensics, scripts |
| GR-19 | `.claude/skills/module-test/SKILL.md` + contracts/ | module test guidance | 5-level failure handling, contract JSON output (stale: Python-era paths) |
| GR-20 | `.claude/skills/validation-documentation/SKILL.md` | validation reporting | Fixed-name reports in `docs/validation/`, freshness checks |
| GR-21 | `docs/system/charter-specification.md` + four-tier docs | architecture charter | Tier 1 constitution / Tier 2 patterns / Tier 3 layers / Tier 4 decisions |
| GR-22 | `docs/refactor/` (57 files incl. `v2/2026-08-06-agent-runtime-pre`) | historical design narrative | Phase 1→2→2.2→Host-era designs; each dated design maps 1:1 to an archived OpenSpec change |
| GR-23 | `docs/prd/` (26 files) | PRD history | Trace/vision/evidence-chain/real-device fix PRDs (Chinese by convention since 2026-08-04) |
| GR-24 | `docs/conventions/` (5 files) | conventions | Doc-location routing, namespace isolation, observation/trace conventions, LiteLLMBar maintenance, RD workflow |
| GR-25 | `openspec/changes/` + `archive/` | change history | 10 active + 76 archived changes — the project's own history vocabulary |
| GR-26 | `.claude/agents/*-memory/{knowledge,lessons}.md` (4 dirs) | accumulated knowledge | Distilled knowledge + case lessons with named failure modes and run IDs |
| GR-27 | `docs/fix/` (2 reports) + `.test_fix_log.md` | fix verification records | Canonical diagnose→fix→regression loops with root-cause chains and exact file:line anchors |

**What the guidance layer teaches as a whole:** the legacy project was operated through a strict evidence chain — real run dirs (`artifacts/runs/…`) are the recorded reality; `trace.jsonl`/`analysis.jsonl`/`issues.jsonl`/`run.log`/`result.json` are its structured records; TraceTool CLI is the only sanctioned reader; trace-to-simulation turns recorded runs into executable reproductions; fix reports and agent memories are the accumulated interpretation; verification verdicts are deterministic C# rules, never model judgment.

---

## Pass 2 — Legacy Knowledge Map

How a competent legacy Agent/developer (as taught by feature/refactor's own guidance) answers the thirteen questions:

1. **Understand the architecture:** read `AGENTS.md` → `docs/system/charter-specification.md` (§5 four-tier slicing, §6 Guard Tests) → `docs/system/constitution/` (Tier 1) → `docs/system/patterns/` (Tier 2: `fsm-design.md` matrix, `handler-pipeline.md`, `dispatch-table.md`, `system-orchestration.md`) → `docs/system/layers/{device,domain,graph,host,observability,simulation,simulation-baseline,state-machine,traversal,vision}.md` (Tier 3) → `docs/system/decisions/log.md` (Tier 4, D-* decision log). Historical depth: `docs/refactor/v2/2026-08-06-agent-runtime-pre` + `docs/refactor/` dated designs.
2. **Run the system:** `scripts/dev-doctor.sh` (env checks) → `dotnet build src/UniClaw.Core.sln` → `dotnet test src/UniClaw.Core.sln` (840-test baseline, non-integration filter `Category!=Integration`).
3. **Run real-device/emulator tests:** `scripts/android-emulator.sh doctor|start|stop` (AVD `uniclaw-lite-api35`, AOSP Settings, no APK needed) → set `UNICLAW_INTEGRATION_SCOPES` (8-tier ladder: vision-smoke → vision-golden → adb-connectivity → adb-read → adb-action → adb-vision-action → scenario-locate → scenario-enumerate) → `dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=…"` or Host CLI `run --scenario … --provider local`. All integration tests skipped by default unless scope-enabled.
4. **Run simulation tests:** Core `tests/UniClaw.Core.Tests/Simulation/` — `SimulationE2ETests` (real TraversalEngine + `StateFixtureBuilder` virtual device + `StatefulMockVisionService`/`StatefulMockActionExecutor`), scroll profiles (`ScrollableMock*`), ExpectedBehavior snapshot gates (S1–S6 byte-pinned `expected/*.json`), `TraceReplay/` reproductions.
5. **Replay historical behavior:** `trace-to-simulation` skill — read `{runDir}/result.json`, `plan.json`, `criteria.json`, `trace/{runId}/trace.jsonl`, `run.log`, `assets/{runId}/analysis.jsonl` → extract FSM sequence and page snapshots → classify scenario (`search-box-stuck`, `dfs-revisit-loop`, `home-not-restored`, `scroll-no-progress`, `swipe-misnavigation`) → build `StateFixture` from analysis.jsonl items → write xUnit test `{runIdShort}_{scenarioSlug}.cs` into `tests/UniClaw.Core.Tests/Simulation/TraceReplay/`. `TraceReplayHarness.ExportReplayJson()` → `artifacts/sim-replay/trace-replay-export.json` → `scripts/sim-replay-viewer.py`.
6. **Inspect failures:** TraceTool CLI (`list|timeline|diagnose|diff|report|interactive|watch|verify`, exit codes 0/1/2/3) → `scripts/log-analyzer.py` subcommands → trace-analyzer agent (deep attribution) → fsm-analyzer (source-anchored, own script library) → fix report to `docs/fix/`.
7. **Inspect visual/perception behavior:** `analysis.jsonl` per-page snapshots (matcher/OCR forensics), screenshots `steps/{N}/before|after.png`, vision golden corpus (`Fixtures/Screenshots/*.jpg` + `.expected.json` + `.local-vision.expected.json` + `.local-vision.evidence.json`), `tools/local_vision/` pipeline (`server.py`, `fusion.py`, `label-mapping.json`, `backends.py`), local-vision-analyzer quality dimensions.
8. **Where scenarios/test worlds are defined:** `scenarios/android-settings/{locate-one-item.v1.json, enumerate-settings-safely.v1.json}` + `policies/settings-read-only.v1.json`; simulation worlds: `StateFixtureBuilder` DSL in Core Simulation; `tests/UniClaw.Host.Tests/Plans/locate-static.v1.json` (static graph plan).
9. **Where runtime artifacts are produced:** `artifacts/runs/{scope}/{scenarioId}/{runId}/` — `manifest.json`, `scenario.snapshot.json`, `plan.json`, `result.json`, `trace/{runId}/{trace.jsonl,run.log,session.json}`, `assets/{runId}/analysis.jsonl` + `steps/{N}/` asset pairs, `issues.jsonl`, `criteria.json`; retention `keepRuns=5`; not committed (gitignored).
10. **Where historical regressions/fixes are documented:** `docs/fix/*.md` (E2E root-cause chains), `docs/prd/*.md` (fix PRDs), `.test_fix_log.md` (test-fix decisions), `openspec/changes/` archive (76 changes), agent-memory `lessons.md`, `docs/system/decisions/log.md`.
11. **Diagnostic tools/scripts:** `src/UniClaw.TraceTool` CLI (8 subcommands), `scripts/log-analyzer.py`, `scripts/sim-replay-viewer.py`, `scripts/dev-doctor.sh`, `scripts/android-emulator.sh`, fsm-analyzer script library (`matrix_from_source.py`, `fsm_error_alignment.py`, `fsm_run_metrics.py`, `fsm_transition_path.py`, `fsm_cycle_detector.py`), local-vision-analyzer scripts (`coord_validation.py`, `item_quality_check.py`), shadow `test_contract_extractor.py`.
12. **Explicitly legacy/deprecated/migration-only areas:** the Legacy Harness (`tests/UniClaw.Host.Tests/Runner/RunnerTestHarness.cs`) is migration-only while OpenSpec `runner-through-engine` deletes the old runner loop; `docs/system/_archive/` holds the 7 superseded cross-cutting docs; `docs/superpowers/specs/` is a staging area flagged misaligned (migrate out, delete); Mode B vision path is never-wired dead code; Python-era skills (module-test, test-extraction, design-doc-sync, trace-collection/visualization/state-machine-integration, workflow-trace-collection) reference paths absent from feature/refactor (`tests/ai/`, `docs/architecture/modules/`, `trace_tree_visualizer.py`, `test_asset_collection_demo.py`) — those assets live on the Python `main` branch; `deepseek` is not a user-selectable provider (D-208).
13. **Authoritative vs stale/supporting docs:** authoritative = `docs/system/charter-specification.md`, four-tier system docs, `docs/testing/test-tiers.md` + `integration-*.md`, `integration.config.json`, `docs/fix/` reports, `docs/refactor/v2/2026-08-06-agent-runtime-pre`, `openspec/changes/` (active = truth source). Stale/supporting = `docs/superpowers/` (pre-cursor drafts), `docs/system/_archive/` (historical), `docs/testing/integration-pipeline-issues.md` (status register, partially fixed), Python-era skill references, `docs/validation/*.md` (point-in-time reports; freshness must be checked).

---

## Pass 3 — Guidance → Asset Discovery Chains

Only references intentionally exposed by the guidance layer were followed:

1. AGENTS.md → docs/system charter → `docs/system/layers/` + `patterns/fsm-design.md` + `decisions/log.md` (D-*) — architecture/decision layer.
2. docs/testing/test-tiers.md → Legacy Harness fakes (`RunnerTestHarness.cs`) → Simulation (`StateFixtureBuilder`) → Integration ladder (`integration-tests.md` → `integration.config.json` → scenario scopes).
3. integration-tests.md → run output layout → `artifacts/runs/…` → TraceTool `verify` → `criteria.json` + `VerifyEngine` + `LocateOneItemRule`.
4. host-test-runner → Phase 4 evidence-gap checklist (result.json, trace.jsonl, analysis.jsonl, manifest.json, run.log, criteria.json, scenario.snapshot.json, steps/ screenshots) — the canonical evidence set per run.
5. trace-to-simulation → `analysis.jsonl` → `StateFixture` → `tests/UniClaw.Core.Tests/Simulation/TraceReplay/*.cs` (committed reproductions) + `TraceReplayHarness.ExportReplayJson()` → `artifacts/sim-replay/trace-replay-export.json` → `sim-replay-viewer.py`.
6. trace-analysis → TraceTool CLI → committed TraceTool fixtures `tests/UniClaw.TraceTool.Tests/Fixtures/{failure,success}/`.
7. e2e-diagnose → docs/fix reports → run IDs → agent memories (fsm-analyzer/trace-analyzer/local-vision-analyzer lessons) → `docs/prd/` fix PRDs.
8. local-vision-analyzer → `tools/local_vision/` (server.py, fusion.py, backends.py, label-mapping.json, benchmark_raw.py) → vision golden corpus (`Fixtures/Screenshots/`).
9. android-emulator.md → doctor probes → `UniClaw.Device` seams (AdbScreenCapture, AdbScreenStateProvider, AdbActionExecutor) → scenario JSONs + safety policy.
10. integration-pipeline-issues.md → P1.1/P1.2 → TraceTool VerifyEngine relocation (D-218) → `pending_verification` + `criteria.json` semantics.

**Dead ends encountered (deliberately not followed):** `tests/ai/`, `trace_tree_visualizer.py`, `test_asset_collection_demo.py`, `docs/architecture/modules/`, `TEST_EXTRACTION_METHODOLOGY.md` — referenced by Python-era skills but absent from feature/refactor.

---

## Pass 4 — Legacy-Native Asset Map

Asset families named with the legacy project's own vocabulary. Strength grades use the Reality Model Admission Contract taxonomy (E4 recorded external world / E3 recorded-reality-derived executable reproduction / E2 executable integration / E1 deterministic simulation / E0 document-only).

| Family ID | Legacy Name | Location | Discovered Through | Purpose | Produced By | Consumed By | Historical/Current/Legacy-only | Reality Evidence Value | Strength | Existing Evidence IDs / CPs | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AF-01 | **run dir** (`{runId}/` with manifest, plan, result, trace, assets, issues) | `artifacts/runs/{scope}/{scenarioId}/{runId}/` (on disk; gitignored) | GR-06/14/13 (run layout, evidence checklist, replay chain) | Complete recorded-reality record of one real run | Host CLI / integration scope runs | TraceTool, trace-analyzer, trace-to-simulation, fix reports, sim-replay-viewer | Current (produced until project end; only 2 artifacts committed) | Highest — the recorded external world itself (frames, ADB results, decisions, verdicts) | E4 (when artifacts preserved) | E-01/E-08/E-10/E-13 sources; CP-01..CP-14 | Retention `keepRuns=5`; historical runs cited in docs (e.g. `20260806T072534Z`) may no longer exist on disk — provenance must be re-checked at B2 |
| AF-02 | **trace.jsonl** (span records) | `{runDir}/trace/{runId}/trace.jsonl` (+ `session.json`, `run.log`) | GR-13/16/12 | Structured event log (spans, transitions, AI calls) | Trace pipeline (TraceCorrelatedFileProvider) | TraceTool CLI, log-analyzer.py, trace-to-simulation, fsm-analyzer | Current | Core raw evidence of system actions vs world transitions | E4 (in run dir) | E-01..E-18 evidence backbone; CP-02/03/08/10 | `record_type=="span"` discriminator; bad lines skipped (D-93) |
| AF-03 | **analysis.jsonl** (per-page analysis snapshots) | `{runDir}/assets/{runId}/analysis.jsonl` | GR-13/18 (Phase 1 timing extraction; vision forensics) | Per-page perception output (elements, types, coords, scroll state) | Host analysis writing | trace-to-simulation (fixture extraction), local-vision-analyzer, trace-analyzer | Current | The perception-side record — element inventory per observed page | E4 (in run dir) | E-08/TE-08 (real-run elements 16/21/14); CP-07/11/12 | Fixtures MUST be extracted from this file, never fabricated (skill hard rule) |
| AF-04 | **result.json / criteria.json / manifest.json / scenario.snapshot.json** (run verdict + expectation set) | `{runDir}/` | GR-14 (evidence checklist), GR-13 | Completion status, verification criteria, run identity, scenario snapshot | Host | TraceTool `verify`, trace-analyzer, report tooling | Current | Verdict + declared expectation records — the honest-completion layer | E4 (in run dir) | completionReason semantics; CP-03/04/05/06 | `pending_verification` = verdict deferred to TraceTool VerifyEngine (D-218) |
| AF-05 | **steps/N/ asset pairs** (before/after/analysis/step-plan/safety-decision/verification) | `{runDir}/assets/{runId}/steps/N/` | GR-14, GR-08 (triage order) | Per-step world evidence (screenshots + decisions) | Host | sim-replay-viewer, host-test-runner Phase 4/5, diagnose | Current | Per-action world-change evidence — the ActionExecution≠ActionEffect record | E4 (in run dir) | CP-01/02 | PNG dedup by MD5 in viewer; size-change threshold ≥20% is a transition signal (legacy heuristic) |
| AF-06 | **issues.jsonl** (issue fingerprints) | `{runDir}/issues.jsonl` | GR-08, GR-16 | Issue evidence with fingerprints | Host | TraceTool diagnose (evidence fallback), fsm-analyzer | Current | Failure-class fingerprints (e.g. e32ad8b9) | E4 (in run dir) | F-01..F-23 mapping source; CP-05/08/10 | diagnose fills `issue_fingerprints` from here (confidence low→medium) |
| AF-07 | **TraceReplay fixtures + tests** (`{runIdShort}_{scenarioSlug}.cs`) | `tests/UniClaw.Core.Tests/Simulation/TraceReplay/` (6 files: `20260805T052309367Z_EnumerateFixtures.cs`, `TraceReplayTests.cs`, `FixVerificationTests.cs`, `SettingsEnumerateRegression.cs`, `TraceReplayFromRunTests.cs`, `TraceReplayHarness.cs`) | GR-13 (skill output contract) | Executable reproductions of recorded runs replayed through the real engine | trace-to-simulation skill | xUnit, fix verification, regressions | Current (committed) | The EXECUTABLE_REPRODUCTION layer — recorded reality re-run headlessly | E3 | E-08/E-10/E-11/E-12 sources; TE-01..TE-10; CP-02/04/05/07/09/13 | Run 20260805T052309367Z (DFS revisit + search-box misclassification) reconstructed from analysis.jsonl |
| AF-08 | **TraceTool run fixtures** (failure + success) | `tests/UniClaw.TraceTool.Tests/Fixtures/{failure,success}/` (runs `20260803T131333575Z`, `20260801T124355012Z`) | GR-12 (CLI contract), GR-08 | Committed minimal run dirs for diagnose/verify contract tests | Test authoring (from real runs) | TraceTool tests, contract validation | Current (committed) | Committed recorded-reality samples — verifiable without local disk | E4 (committed trace.jsonl) | R1-R4 family; CP-08/10 | The ONLY committed run-dir-class evidence |
| AF-09 | **sim-replay export** | `artifacts/sim-replay/trace-replay-export.json` (run `20260805T083146853Z`, 19 steps, all_visited) | GR-10 (viewer input), replay chain | TraceReplayHarness.ExportReplayJson output — fixture-ized replay of a launcher run | TraceReplayHarness | sim-replay-viewer.py | Current (committed) | Recorded-reality-derived fixture (menuitem/text elements) | E3 | E-08 sibling; CP-13 | A distinct run from the E-10 run; not previously catalogued |
| AF-10 | **scenario JSONs + safety policy** | `scenarios/android-settings/{locate-one-item.v1.json, enumerate-settings-safely.v1.json, policies/settings-read-only.v1.json}`; `tests/UniClaw.Host.Tests/Plans/locate-static.v1.json` | GR-06/09 (scenario scopes), GR-05 | The two agreed task classes (closed-world locate, open-world bounded enumerate) + the safety constraint vocabulary | Scenario authoring | Host runner, integration gates, plan loader | Current (committed) | The declared constraint/expectation layer (depth bounds, allowed actions, dangerousSemantics, confidenceThresholds) — ER-side source | E1 (declared specs) | TE-02 (static plan), TE-05 (depth bound); CP-04/07/14 | `excludePatterns: ["search"]`, `dangerousSemantics` list, `resetProcedure` — normative world constraints |
| AF-11 | **vision golden corpus** | `tests/UniClaw.Core.Tests/Fixtures/Screenshots/` — `Screenshot_2026-07-26-17-47-23-33_fc704e6b….{jpg, expected.json, local-vision.expected.json, local-vision.evidence.json}` | GR-06 (vision-golden scope, human-reviewed expected) | Human-reviewed perception ground truth | Vision-golden scope runs + human review | vision tests, provider regressions | Current (committed; only 1 screenshot set) | Production-shaped perception reference (real YOLO/OCR output vs human review) | E2 | VE-01..VE-10 context; CP-11/12 | `.actual.json` diagnostic-only; regen via `UNICLAW_VISION_UPDATE_EXPECTED=1` |
| AF-12 | **ExpectedBehavior snapshots (S1–S6)** | `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/{hierarchy,long-list,scroll,settings}/*.json` (20 committed files) + `src/UniClaw.Core/Simulation/ExpectedBehavior/*` (VerificationReport, NumericAnchor, ElementCoverage, TraceIntegrity, CompletionExpectation, DfsPropertiesExpectation…) | GR-05 (simulation tier), GR-21 (charter Guard Tests) | Byte-pinned expected behavior for full/multi-scroll/deep-back/target-search traversals | Simulation baseline runs | baseline tests, snapshot gates | Current (committed) | Deterministic behavioral norms for traversal classes (scroll, hierarchy, dedup, target search) | E1 | SIM-01..SIM-14; CP-05/07/09 | Includes element-coverage mode (anti "skip-scroll-and-pass") |
| AF-13 | **Simulation virtual device** | `src/UniClaw.Core/Simulation/` — StateFixture/Builder, StatefulMockVisionService/ActionExecutor, Scroll/{IScrollContentSource, ScrollableMock*}, MockModelProvider/PageAnalyzer | GR-05 (simulation tier), GR-13 (fixture build classes) | Deterministic world models (pages, elements, transitions, scroll profiles) | Simulation design (13/14-phase2-sim-*) | SimulationE2ETests, TraceReplay tests | Current (committed) | The deterministic world vocabulary — what "world" meant in simulation | E1 | SIM-01..14; CP-01..14 test worlds | Stateful mock's action flips fixture page — the simulated ActionEffect rule |
| AF-14 | **Legacy Harness fakes** | `tests/UniClaw.Host.Tests/Runner/RunnerTestHarness.cs` (FakeObservationSource, FakeActionExecutor, FakeAdbRunner, SafeActionExecutor…) | GR-05 (test-tiers doctrine) | Stateless fault-injection orchestration tests (stale plan, back-on-wrong-page, dangerous-skip, scroll-stuck) | Harness test authoring | Runner tests | Migration-only (doomed by runner-through-engine) | World-behavior pressure tests that stateful simulators cannot express | E1 | SP-07/08-related; CP-10 | Real safety gate not faked; FakeAdbRunner throws if used |
| AF-15 | **integration scope ladder tests** | `tests/UniClaw.Host.Tests/Integration/` (EmulatorScenarioIntegrationTests, AdbVisionActionIntegrationTests, AdbSessionIntegrationTests, IntegrationConfig, ProviderPreflight, integration.config.json) | GR-06/07 (ladder + config) | Gated real-device verification from vision-smoke to scenario-enumerate | Integration scope runs | CI/gated test runs | Current (committed; skipped unless scoped) | The maturity ladder from perception to full scenario — maps to S2/S3 | E2 (when scoped-enabled) | IT-01..IT-11; CP-01/02/07/11/12 | Default provider sensenova / `sensenova-6.7-flash-lite` |
| AF-16 | **VerifyEngine + LocateOneItemRule + criteria.json** | `src/UniClaw.TraceTool/` + run-dir criteria | GR-12/08 (D-201, D-218) | Deterministic C# verdict rules (verified/not_verified, cause, confidence, artifactPaths) | TraceTool | verify/watch commands, reports | Current (committed) | The expectation machinery — deterministic rules over recorded evidence | E2 (rules) / E4 (verdicts on runs) | CP-03/06 | Verdict ≠ explanation doctrine: agents never override C# verdicts |
| AF-17 | **fix reports + fix PRDs** | `docs/fix/{2026-08-06-container-gateway-f3-report, 2026-08-06-enumerate-settings-safely-e2e-report}.md`; `docs/prd/2026-08-0x-*` (settle-delay, verify-evidence-chain, depth-popup, e2e-enumerate-fix, roi-scroll, raw-rgba) | GR-27 (diagnose→fix loop), GR-23 | Root-cause chains with exact file:line anchors (F1/F2/F3, V2/V7-V9) + fix rationale | e2e-diagnose flow | Fix verification, regressions, future maintenance | Current (committed) | The deepest failure interpretation layer — names the world-behavior failures precisely | E0 (interpretation) over E4 (cited runs) | F-01..F-23 origin; E-09/E-13; CP-01/02/04/05/08/09/10/11 | Canonical run cited: `20260806T072534Z` (753 spans, 176 analysis lines, 120 step pairs) |
| AF-18 | **agent memory knowledge/lessons** | `.claude/agents/{fsm,trace,local-vision,shadow-fsm}-analyzer-memory/{knowledge,lessons}.md` | GR-16/17/18 (refresh-checked memory) | Accumulated failure knowledge: named failure modes, D-* decisions, run IDs, scripts | Agent analysis sessions | fsm-battle distillation, future analysis | Current (committed) | Legacy-interpretation layer with high pointer value (container re-execution loop, double-crop coord bug, OCR-variant dedup bypass, 91.9% threshold miss, child_control false-positive, search-box type fluctuation) | E0 (interpretation) with E4 pointers | VE-05/06/07 support; E-13 context; CP-02/05/07/08/09/11/12 | Run IDs inside memories: 33-run comparison, `20260804T170915736Z` (verify-evidence-chain evidence) |
| AF-19 | **OpenSpec change archive** | `openspec/changes/` (10 active) + `openspec/changes/archive/` (76 archived) | GR-25 (AGENTS.md: change progress truth source) | The project's own history vocabulary: every change maps to designs/PRDs/evidence eras | OpenSpec lifecycle | Archive/decisions, future work | Historical (committed) | The index key into the codebase: `fix-dfs-depth-runaway`, `depth-popup-fix`, `e2e-dedup-vision-quality`, `roi-scroll-detection`, `verify-evidence-chain-fix`, `settle-delay-responsibility`, `raw-rgba-screenshot-pipeline`… | E0 (pointers) | E-04..E-18 cross-reference; CP-02/04/05/07/08/09/11/12 | Chronological eras: Phase 1 domain → Phase 2 sim → trace → UniBrain → Host/adb → observability/evidence → failure-fix batch |
| AF-20 | **decision log + FSM matrix pattern** | `docs/system/decisions/log.md` (D-*), `docs/system/patterns/fsm-design.md` | GR-21 (charter tiers) | Append-only decisions and the authoritative FSM transition matrix (8 states, 19 edges, D-240 hardened) | Design process | fsm-analyzer, architecture guards | Historical (committed) | The normative/expected-behavior layer (state semantics, edge conditions) — ER-side source | E0 (normative) | RD-01..RD-11 context; CP-10 | 8 handlers, 2-round ResultVerify, MaxDepth=10, stale-click fuse 5/3 |
| AF-21 | **Python-era trace/mock assets** | `tests/ai/`, `trace_tree_visualizer.py`, `test_asset_collection_demo.py`, `traces/{latest,baseline,verification}/`, `docs/architecture/modules/` | GR-19/20 stale skill references (trace-collection/visualization/state-machine-integration, module-test, test-extraction, design-doc-sync) | Python-era mock trace generation and module design docs | Python-era tooling | Python-era tests | Legacy-only — ABSENT from feature/refactor (live on `main`) | Only via `main` branch; mock assets (synthetic), not real-world | E0/E1 (on main) | — | Any prior extraction citing these paths must be re-anchored to `main` or downgraded |
| AF-22 | **local vision tooling** | `tools/local_vision/` (server.py, backends.py, fusion.py, analyze.py, benchmark_raw.py, label-mapping.json, schema.py, tests/) | GR-18 (vision analyzer L1) | YOLO (Deki-Yolo, 21 labels) + RapidOCR/PaddleOCR pipeline producing evidence JSON | Python pipeline | LocalVisionProvider (C#), vision tests | Current (committed) | The perception machinery whose OUTPUTS are the observation records (and whose `_apply_chevron_heuristic` is the phantom-subtitle source) | E2 (pipeline) / outputs E4 | VE-01..VE-10; CP-11/12 | `fusion.py:292-343` chevron heuristic — the subtitle phantom root cause |
| AF-23 | **scripts (diagnosis tools)** | `scripts/{log-analyzer.py, sim-replay-viewer.py, dev-doctor.sh, android-emulator.sh}` | GR-10/11/09 | Log parsing/visualization/env doctor/emulator control | Tooling | All analysis flows | Current (committed) | Tools for B2 inspection — not evidence themselves | n/a (tools) | — | log-analyzer extracts FSM transitions, safety denies, page analyses from run.log |

---

## Pass 5 — Guidance vs Evidence Classification

| Resource | Classification |
|---|---|
| AGENTS.md / CLAUDE.md / WORKFLOWS_AND_SKILLS.md / MCP-QUERY.md | GUIDANCE_ONLY / POINTER_TO_EVIDENCE |
| docs/testing/*, docs/conventions/* | GUIDANCE_ONLY (with POINTER_TO_EVIDENCE for run layout and scopes) |
| docs/system charter + constitution + patterns + layers | GUIDANCE_ONLY / POINTER_TO_EVIDENCE (normative expectation layer — ER-side input, not world evidence) |
| docs/refactor/, docs/prd/ (excluding fix PRDs) | LEGACY_INTERPRETATION (historical narrative + design intent) |
| docs/fix/ reports, .test_fix_log.md, agent memories | LEGACY_INTERPRETATION (with E4 pointers via cited run IDs) |
| scripts/* | TOOLS (diagnosis/visualization; IMPLEMENTATION_ONLY for dev-doctor/android-emulator) |
| `artifacts/runs/…` (on disk) | RAW_EVIDENCE (E4) — the recorded external world |
| `trace.jsonl` / `analysis.jsonl` / `issues.jsonl` / `result.json` / `criteria.json` / step assets (in run dirs) | RAW_EVIDENCE (E4) |
| TraceTool committed fixtures (AF-08) | RAW_EVIDENCE (E4, committed) |
| TraceReplay fixtures (AF-07), sim-replay export (AF-09) | EXECUTABLE_REPRODUCTION (E3) |
| Baseline ExpectedBehavior snapshots (AF-12), simulation virtual device (AF-13), Harness fakes (AF-14) | EXECUTABLE_REPRODUCTION / EVIDENCE_PRODUCER (E1) |
| Vision golden corpus (AF-11) | DERIVED_EVIDENCE (E2, human-reviewed) — perception ground truth |
| Integration ladder tests (AF-15), VerifyEngine (AF-16) | EVIDENCE_PRODUCER (E2) / EXPECTATION machinery |
| Scenario JSONs + safety policy (AF-10) | GUIDANCE_ONLY for B2 reality facts; POINTER_TO_EVIDENCE for constraint/ER extraction |
| Python-era trace/mock assets (AF-21) | UNKNOWN on feature/refactor (absent — re-anchor to `main`) |

---

## Pass 6 — Comparison Against Existing Extraction

Compared with `legacy-source-inventory-step1.md`, `legacy-high-value-evidence-set-step2.md`, `legacy-normalized-evidence-step3.md`, `legacy-visual-perception-pressure-supplement.md`, `legacy-traversal-plan-abstraction-supplement.md`.

**Asset families already captured:** simulation families (SIM-01..14 → AF-12/13), integration families (IT-01..11 → AF-15), replay families (R1-R4 → AF-07/08), failure evidence (F-01..23 → AF-06/17/18), intent/goal/plan pointers (IP-01..17 → AF-19/20), primary evidence E-01..E-18 and atomic TE/VE cases (→ AF-01..AF-11). The step1–3 pipeline and both supplements correctly mined the test-code layer; the evidence IDs map cleanly onto the guidance-led families above.

**Previously missed / underrepresented:**

1. **The run-dir class itself (AF-01..AF-06) is underrepresented as an asset family.** Existing extraction cites individual runs (20260805T052309367Z, 20260806T072534Z) through fixtures/fix reports, but never catalogued the run-dir layout as a recorded-reality corpus with retention semantics (`keepRuns=5`, gitignored). B2 must treat on-disk run dirs as the primary recorded-reality source and re-verify existence of cited runs.
2. **Committed TraceTool fixtures (AF-08)** — two committed run dirs (failure `20260803T131333575Z`, success `20260801T124355012Z`) not catalogued in the previous extraction.
3. **The sim-replay export (AF-09)** — run `20260805T083146853Z`, a committed replay fixture distinct from the E-10 run, not previously catalogued.
4. **Vision golden corpus (AF-11)** — one committed human-reviewed screenshot set (2026-07-26) with four companion files; the visual supplement derived VE-01..VE-10 from narrative/memory rather than the golden corpus.
5. **ExpectedBehavior snapshot corpus (AF-12)** — 20 byte-pinned behavioral norms (hierarchy/long-list/scroll/settings classes) supporting CP-05/07/09 — referenced as "S1–S6 snapshot gates" in validation reports but never mined as behavioral evidence.
6. **Scenario JSONs + safety policy (AF-10)** — the declared constraint vocabulary (`dangerousSemantics`, `excludePatterns: ["search"]`, `confidenceThresholds`, `resetProcedure`) — the ER-side normative source, largely absent from extraction (which focused on test behavior).
7. **Verify rules (AF-16)** — LocateOneItemRule/criteria.json/VerifyEngine — the deterministic expectation machinery; relevant to the contract's EXPECTED REQUIREMENT and independent-validation concepts, not mined before.
8. **Safety policy and deny-journal semantics** — `blocked` = safety-gate-denied and the safety decision records per step — evidence of constraint enforcement (CP-07/CP-10), underrepresented.
9. **Agent memory lessons (AF-18)** as a navigational index with run IDs (33-run comparison, `20260804T170915736Z`) — pointer value only, but high precision.
10. **OpenSpec change names (AF-19)** as the history vocabulary — not used as an index in previous extraction.

**Provenance corrections suggested (no artifact modified):**

- **"RECORDED_REALITY_DERIVED" claims must be re-checked against actual committed artifacts.** The reconstructed fixtures (AF-07) are committed; the cited raw runs behind fix reports (e.g., `20260806T072534Z`, `20260806T072558649Z`) are gitignored and may no longer exist on disk → their E4 provenance is doc-anchored only, and E3 fixtures derived from them inherit that risk.
- **"INTEGRATION" provenance (IT-*) must be scope-qualified:** integration tests are skipped by default; evidence claims that depend on scoped runs (vision-golden, adb-action, scenario-enumerate) should state whether the scope actually ran.
- **Any evidence anchored to Python-era trace assets (`tests/ai/assets/traces/`, `trace_tree_visualizer.py` outputs) is currently unverifiable on feature/refactor** — re-anchor to `main` or downgrade (this affects skill-referenced mock-trace generation; prior extraction did not cite these paths, but B2 should not follow them).
- **The `all_visited` completionReason in the sim-replay export (AF-09) is a legacy verdict, not a world fact** — consistent with the admission contract's no-answers-embedded rule; it documents the launcher-run world (19 steps) that produced it.
- **TraceReplay fixture naming (`{runIdShort}_{scenarioSlug}`)** reveals the derivation chain: FixVerificationTests (L1–L8) and SettingsEnumerateRegression derive from real runs (20260805T052309367Z family) — the E-08/E-11 evidence provenance chains should cite the fixture files as E3 artifacts.

**Guidance paths that explain how old assets relate:** `docs/testing/test-tiers.md` (three tiers and their boundaries), `integration-tests.md` (ladder + run output), `trace-to-simulation` skill (recorded run → fixture → test), `host-test-runner` Phase 4 evidence checklist (the canonical per-run evidence set), `e2e-diagnose` (dispatch to analyzers), agent memories (D-* decisions and failure modes linking docs/PRDs/fix reports to code).

---

## Pass 7 — B2 Authoritative Entrypoints

| Entrypoint ID | Path | How Discovered | Asset Family | Why Reality-Relevant | Expected Strength | Relevant CPs | B2 Usage |
|---|---|---|---|---|---|---|---|
| EP-01 | `artifacts/runs/` (on-disk run dirs) | GR-06/14 run layout + retention | AF-01..AF-06 | The recorded external world (per-run evidence chain) | E4 | all | **PRIMARY** — extract WF/OB/RI from preserved runs; verify each cited run exists before trusting it |
| EP-02 | `tests/UniClaw.Core.Tests/Simulation/TraceReplay/` | GR-13 skill output contract | AF-07 | Executable reproductions of recorded reality | E3 | CP-02/04/05/07/09/13 | **PRIMARY** — the reproducible world-fact corpus (transitions, elements, scroll, revisit) |
| EP-03 | `tests/UniClaw.TraceTool.Tests/Fixtures/{failure,success}/` | GR-12 CLI contract + GR-08 | AF-08 | Committed recorded runs (only committed run-dir evidence) | E4 | CP-08/10 | **PRIMARY** — low-risk starting point: committed, self-contained run evidence |
| EP-04 | `artifacts/sim-replay/trace-replay-export.json` | GR-10 replay chain | AF-09 | Committed replay fixture (launcher world) | E3 | CP-13 | **PRIMARY** — a committed fixture-ized run; inspect as OB/RI source |
| EP-05 | `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/` | GR-05 simulation tier + validation reports (S1–S6) | AF-12 | Byte-pinned behavioral norms for traversal classes | E1 | CP-05/07/09 | **SUPPORTING** — ER-side norms (what traversal classes must exhibit) |
| EP-06 | `tests/UniClaw.Core.Tests/Fixtures/Screenshots/` | GR-06 vision-golden scope | AF-11 | Human-reviewed perception ground truth | E2 | CP-11/12 | **SUPPORTING** — perception ground truth + declared-vs-actual type conflicts |
| EP-07 | `scenarios/android-settings/` + `tests/UniClaw.Host.Tests/Plans/locate-static.v1.json` | GR-06/09 scenario scopes + GR-05 | AF-10 | The two task classes + constraint vocabulary | E1 (declared) | CP-04/07/14 | **SUPPORTING** — ER/constraint extraction (depth, allowed actions, dangerous semantics) |
| EP-08 | `docs/fix/*.md` + `docs/prd/2026-08-0x-*` fix PRDs | GR-27 diagnose→fix loop | AF-17 | Deepest failure interpretation with file:line anchors and run IDs | E0 (E4 pointers) | CP-01/02/04/05/08/09/10/11 | **POINTER_ONLY** — resolve cited run IDs to EP-01/EP-03 before claiming E4 |
| EP-09 | `.claude/agents/*-memory/{knowledge,lessons}.md` | GR-16/17/18 memory system | AF-18 | Named failure modes + run IDs + D-* decisions | E0 (E4 pointers) | CP-02/05/07/08/09/11/12 | **RESEARCH_ONLY** — legacy interpretation; use to locate runs and frame RIs, never as facts |
| EP-10 | `openspec/changes/` + `archive/` | GR-25 change-history truth source | AF-19 | History vocabulary mapping changes to evidence eras | E0 (pointers) | CP-02/04/05/07/08/09/11/12 | **POINTER_ONLY** — index into designs/PRDs/tests by change name |
| EP-11 | `docs/system/decisions/log.md` + `patterns/fsm-design.md` | GR-21 charter tiers | AF-20 | Normative FSM semantics and decisions | E0 (normative) | CP-10 | **RESEARCH_ONLY** — ER-side state semantics; never world facts |
| EP-12 | `scripts/{log-analyzer.py, sim-replay-viewer.py}` | GR-10/11 | AF-23 | Tools to read run evidence (not evidence) | n/a | — | **SUPPORTING** — inspection tooling for EP-01/EP-03 evidence |

---

## Anti-Keyword-Bias Findings

**Assets that keyword-only discovery (trace / fixture / screenshot / integration / action / replay / vision / simulation) would likely have missed:**

1. **The expectation layer** — `*.expected.json` (byte-pinned), `ExpectedBehavior` subsystem (`NumericAnchor`, `ElementCoverage`, `TraceIntegrityExpectation`, `VerificationReport`), `criteria.json`, `LocateOneItemRule`, `VerifyEngine`, `expectedPageIdentities`, `successCriteria` — the project's normative expectations about world behavior. These are the primary source for the admission contract's EXPECTED REQUIREMENT layer and would be invisible to "trace/screenshot/action" keyword scans.
2. **The safety vocabulary** — `safetyPolicy`, `settings-read-only.v1.json`, `dangerousSemantics`, `dangerousText`, `confidenceThresholds`, `excludePatterns: ["search"]`, `resetProcedure`, deny-journal (`safety.decision` records, `blocked` outcomes) — the constraint-enforcement layer (CP-07/CP-10).
3. **The "doctor" probe definitions** — `sys.boot_completed==1`, PNG-magic screenshot check, uiautomator XML parseability — observable world-boundary definitions embedded in `android-emulator.md`/`dev-doctor.sh`.
4. **The "golden" concept** (`vision-golden` scope, `local-vision.expected.json` = human-reviewed ground truth) — under the generic word "screenshot" its review-status semantics are lost.
5. **The trace STRUCTURE vocabulary** — `span`, `spanId`, `parent-linkage`, `TraceContext` envelope, `run.log` format `[t=<runId>] [s=<spanId>]`, `record_type=="span"`, `evidence_path` relative resolution — the correlation machinery that makes run artifacts usable.
6. **Run identity conventions** — `runId` UTC naming (`yyyyMMddTHHmmssZ`), `runIdShort` (first 17 chars) used in replay fixture names, `{runIdShort}_{scenarioSlug}` — the temporal identity scheme that ties recorded reality together.
7. **Verdict semantics** — `completionReason`, `pending_verification`, `incomplete:duration_budget_exhausted`, `blocked`, verify verdicts (`not_verified`/`verified`) — the honesty machinery; "result" alone would not surface it.
8. **The FSM matrix and decision log** (D-*) — normative semantics and the hardening history (D-240~D-244) — the ER-side behavioral contract.
9. **The `steps/N/{before,after,analysis,safety-decision,verification}` naming** — per-step paired evidence with decision records — the ActionExecution≠ActionEffect record structure.
10. **The OpenSpec change-name vocabulary** (`fix-dfs-depth-runaway`, `depth-popup-fix`, `e2e-dedup-vision-quality`, `verify-evidence-chain-fix`, `roi-scroll-detection`, `settle-delay-responsibility`) — the project's own failure-history language.

**Legacy terminology to preserve for B2 navigation:** run dir / runId / trace.jsonl / analysis.jsonl / issues.jsonl / result.json / criteria.json / manifest.json / scenario.snapshot.json / steps/N/ before|after / span / spanId / evidence_path / expected.json / local-vision.expected.json / golden / ExpectedBehavior / NumericAnchor / ElementCoverage / snapshot (S1–S6) / completionReason / pending_verification / verify verdict / deny / safetyPolicy / dangerousSemantics / isEndOfList / hasScroll / FSM matrix / frame / stack depth / AllVisited / MaxSteps / EnumerateFixtures / TraceReplay / StateFixture / chevron heuristic / double-crop / phantom subtitle / search-box misclassification / dfs-revisit-loop / home-not-restored / scroll-no-progress.

---

## Completeness Assessment

- **Pass 1 (guidance roots):** complete — AGENTS.md, CLAUDE.md, WORKFLOWS_AND_SKILLS.md, MCP-QUERY.md, 5 testing docs, 5 validation docs, 2 fix reports, .test_fix_log.md, 4 scripts, 13 skill docs, 9 workflow metas, 7 agent definitions, 4 memory dirs (knowledge/lessons), charter + system-doc tier map, refactor index + key narratives (incl. v2 final survey), PRD index + key fix PRDs, 5 conventions, superpowers index, openspec change index (10 active + 76 archived), scenario JSONs + safety policy, committed artifact files.
- **Pass 3 (meaningful references):** followed only guidance-exposed chains; dead Python-era references identified and NOT followed.
- **Pass 4–7:** asset map (23 families), classification, comparison, and 12 B2 entrypoints produced.
- **Bounded one-pass discipline maintained:** no open-ended mining, no Reality Models formulated, no artifact modified.

**Gaps (recorded, not blocking):** (a) full on-disk run-dir inventory requires local disk access (gitignored) — B2 must reconcile with disk at extraction time; (b) `docs/refactor/` remaining 20+ dated designs not read in full (POINTER_ONLY status is sufficient for B2); (c) Python-era asset contents on `main` not inventoried here (out of scope; re-anchor only if B2 needs them).

## Readiness

**LEGACY_GUIDANCE_MAP_READY_FOR_REALITY_MODEL_EXTRACTION**

## Next Task

**LEGACY_REALITY_MODEL_EXTRACTION** (B2) — use EP-01..EP-12 as the authoritative entrypoint set; extract from the already-filtered evidence corpus under the frozen rules of `docs/system/reality-model-admission-contract.md`.

## Repository Changes

`docs/decisions/legacy-guidance-led-asset-discovery.md` ONLY. No other files modified; `feature/refactor` untouched (read-only via Git objects).
