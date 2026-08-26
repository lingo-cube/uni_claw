## 1. Human Apply Gate and Dispatch Freeze

- [x] 1.1 Obtain explicit Human approval to apply the exact proposal/design/spec revision; record the approved revision/digest before any repository edit. Apply is a Large Change (new project + new tooling boundary) and requires a separate explicit Human Gate beyond proposal authorization.
  - Evidence (2026-08-26): Human apply authorization received as `PROJECT_LEADER_UNIFLOW_APPLY_UNIAGENT_EMULATOR_VALIDATION_HARNESS` (explicit instruction to enter Implementation via UniFlow). Approved base revision: `e2d8dd44214632f50777992d58fb4fe318ad45f0`; `openspec validate uniagent-emulator-validation-harness --strict` PASS at that revision; artifacts read in full (proposal/design/spec/tasks) before dispatch.
- [x] 1.2 Record the architecture/lifecycle owner and create one validated UniFlow WorkItem per bounded increment; no Tool Only source write.
  - Evidence (2026-08-26): DSH coding agent = Leader (Sol role); WorkItems WI-EVH-001..006 planned per bounded increment (scaffold/hosting → driver → collector → report/verifier → scenarios → classification/guards), each validated via `agent_profile_validator.py work-item` and dispatched with an M0 CLI dispatch record; single unicast worker per WorkItem; no Tool Only source write.
- [x] 1.3 Confirm no implementation task requires a new Runtime API, new wire contract, Phase 2 contract change, ownership adjustment, Planner, or Memory; otherwise stop with `ARCHITECTURE_DECISION_REQUIRED`.
  - Evidence (2026-08-26): Design D1–D7 reviewed — harness consumes `run.strategy.start` + frozen read-only wire + Tier-A in-process public read model only; all tasks (2.x–7.x) fit within existing surfaces. No stop-condition trigger at freeze; each WorkItem escalation clause re-checks at execution time.

### 1.4 Human Apply authorization receipt

- Apply authorization receipt to be recorded here by the Human before any edit (approved base commit + artifact content IDs).
  - Recorded 2026-08-26: base commit `e2d8dd44214632f50777992d58fb4fe318ad45f0`; authorized by the Human message `PROJECT_LEADER_UNIFLOW_APPLY_UNIAGENT_EMULATOR_VALIDATION_HARNESS` (Implementation scope: Harness Project / Emulator Driver / Result Collector / Scenario Runner / Boundary Verification only); strict validation PASS.

## 2. Harness Project Scaffold

- [x] 2.1 Create `src/UniClaw.Runtime.ValidationHarness/` (net10.0) and register it in `src/UniClaw.Runtime.sln`; verify `dotnet build src/UniClaw.Runtime.sln` remains green.
  - Evidence (2026-08-26, WI-EVH-001 accepted): `src/UniClaw.Runtime.ValidationHarness/` created (net10.0, mirrors Harness csproj conventions); registered via sln (+14 lines, the only existing-file edit); `dotnet build src/UniClaw.Runtime.sln` 0 errors / 0 warnings. Leader independently re-verified.
- [x] 2.2 Reference only `UniClaw.Runtime`, `UniClaw.Runtime.DriverHost`, `UniClaw.Runtime.Harness`, and deterministic Adapter/Semantic fixtures as needed; zero references from any production project to the harness.
  - Evidence (2026-08-26): csproj references UniClaw.Runtime, UniClaw.Runtime.DriverHost, UniClaw.Runtime.Harness (+Adapters for the deterministic fixture composition); `ValidationHarness_IsNotReferencedByAnyProductionProject` guard green — zero reverse references. modules.json runtime-integration owned/test paths extended with the harness paths (Leader governance action, validator PASS).
- [x] 2.3 Add the harness fixture worlds (deterministic `IEnvironment` with S2 anomaly-injection points) and the Tier-A `RunGraphFactory` mapping a fixture `DeviceSelector` to `RunExecutionGraph`; no fake environment enters `UniClaw.Runtime.PhysicalHost` (F1 intact).
  - Evidence (2026-08-26): `Fixtures/ValidationFixtureWorld.cs` (deterministic settings-like graph: root container → expandable children → record-only leaves; S2 anomaly-injection hooks present), `FixtureSemanticEnvironment.cs`, `FixtureComposition.cs` (RunGraphFactory: fixture DeviceSelector → RunExecutionGraph). No fake enters PhysicalHost (F1 intact; all fixtures live in the harness project).
- [x] 2.4 Add Tier-A hosting composition starting `UniClawDriverHostServer` in-process with loopback wire access mirrored from the existing DriverHost E2E pattern; verify one `run.strategy.start` round-trip on the fixture device key.
  - Evidence (2026-08-26): `Hosting/TierAHost.cs` starts UniClawDriverHostServer in-process with the fixture factory; `Wire/LoopbackWireClient.cs` mirrors the E2E loopback pattern; `AttestationAgent(runId)` exposes the post-terminal Agent read-model seam. `TierAHostingRoundTripTests.FixtureDevice_OneStrategyStart_RoundTripsThroughWire_AndTerminalEventsAreReadable` green: one real `run.strategy.start` round-trip, admission Accept+runId, terminal `completed` via read-only polling, `GoalEvidenceProduced` before `RunCompleted`, events readable through `run.events.after`.

## 3. Emulator Driver

- [x] 3.1 Implement directive validation against the closed `StrategyDirective` vocabulary: every field inside the closed enums, zero forbidden payload content (coordinates / page paths / click sequences / element locators / actions / callbacks / unresolved prose); deterministic reject before any wire call.
  - Evidence (2026-08-26, WI-EVH-002 accepted): `Emulator/StrategyDirectiveValidator.cs` + `StrategyDirectiveValidation.cs` — closed-vocabulary validation against StrategyContract; forbidden categories (coordinate/UI path/click sequence/locator/action/callback/prose) rejected deterministically BEFORE any wire call. `EmulatorDriverTests` ForbiddenContentCases theory covers every category (7 cases) with zero-transport proof via RecordingTransport.
- [x] 3.2 Implement transport via the existing `run.strategy.start` and record an immutable call log entry (method, payload digest, admission result, timestamp) per call.
  - Evidence (2026-08-26): `Emulator/EmulatorDriver.cs` + `EmulatorTransport.cs` + `EmulatorCallLog.cs` — transport via existing run.strategy.start over LoopbackWireClient; immutable append-only call log (ImmutableArray with- pattern, mirrors ExplorationLedgerView style) carrying method, canonical payload digest (StrategyPayloadJson.CanonicalDigest, SHA-256 over canonical JSON), admission result, timestamp. Rejections logged as REJECTED_BEFORE_TRANSPORT with zero wire calls.
- [x] 3.3 Add the live/deterministic dual mode: agent-authored directive handoff plus recorded directive fixtures; a missing directive yields `DIRECTIVE_REQUIRED`, never a synthesized strategy.
  - Evidence (2026-08-26): dual mode implemented — caller-provided directive (live) or recorded fixture (deterministic); goal-only input yields DIRECTIVE_REQUIRED logged entry, zero inference code path (grep-verifiable; tested by GoalOnly_WithoutDirective test).
- [x] 3.4 Add capability tests: legal directive accepted once; forbidden content rejected before transport; `DIRECTIVE_REQUIRED` returned without inference; call log immutability.
  - Evidence (2026-08-26): EmulatorDriverTests green — legal directive accepted once (admission logged), forbidden content ×7 categories rejected pre-transport, DIRECTIVE_REQUIRED zero wire calls, call-log immutability asserted. Total ValidationHarness suite 21/21; Leader fixed a race in the round-trip test (attestation accessor vs ReleaseReservation — TEST_HARNESS classification, documented in-test) and re-ran the full deterministic suite: 2073/2073 Runtime + 32/32 Semantic.

## 4. Result Collector

- [x] 4.1 Implement aggregation of only existing Runtime public facts: RunId, StrategyId, Admission, lifecycle events (`run.events.drain`/`after`), `RunSnapshot` (truth-source classification preserved), Trap (`run.trap.get`), `EvidenceRef`s resolved via `evidence.get`, terminal reason.
  - Evidence (2026-08-26, WI-EVH-003 accepted): `Results/ResultCollector.cs` + `ValidationResult.cs` + `IRuntimeReadSurface.cs` with WireReadSurface/TierAReadSurface implementations — aggregates Admission/Lifecycle/Snapshot/Trap/Evidence/Coverage/Terminal/Boundary from admission receipt, frozen read ops, and Tier-A read model; classifications (DirectProjection/DerivedReadModel/Unavailable) mirror RunSnapshot semantics.
- [x] 4.2 Enforce truthfulness: unavailable fields (full GoalEvidence; Ledger on wire tiers) recorded explicitly with classification, never invented; no Emulator inference, Memory, or Plan content enters a Result.
  - Evidence (2026-08-26): unavailable fields recorded with classification and reason (wire-tier ledger, full GoalEvidence); Runtime UnavailablePartial fields map to DerivedReadModel+IsPartial preserving the real partial value (trace State+Reason) — partial-truth preservation implemented and asserted by the field-walk invariant (populated ⇒ classified).
- [x] 4.3 Tier-A ledger attestation: read the in-process Agent public read model (`CompileExplorationLedgerView`) post-terminal and include discovered/visited/pending/unresolved/unknown-frontier with stable digest.
  - Evidence (2026-08-26): TierAReadSurface attests ExplorationLedgerView post-terminal via the Agent public read model; TierAHost captures the composed Agent at graph-build time (pre-publication, design D3 'capture at admission') eliminating the run-record-release race; digest stability asserted across reaggregation.
- [x] 4.4 Add capability tests: Result fields trace to surfaces; unavailable markers correct; ledger attestation digest stable across identical evidence.
  - Evidence (2026-08-26): ResultCollectorTests 4/4 green — field trace/classification walk, wire-tier unavailability (struct-field default semantics asserted truthfully), Tier-A five counts + digest determinism, evidence resolution incl. synthetic unresolvable ref recorded not dropped. Leader independently re-verified: harness 25/25, full deterministic 2077/2077 + Semantic 32/32, consistency ALL PASS, strict PASS.

## 5. Scenario Runner

- [x] 5.1 Implement S1 (Settings Exploration Depth 2, D1 0/1/N semantics): one directive; asserts admission-accepted single Run, zero driver calls after admission, no dispatched action for record-only leaves, complete deterministic Ledger (Tier A), `GoalEvidenceProduced` before `RunCompleted`.
  - Evidence (2026-08-26, WI-EVH-005 accepted): SettingsExplorationScenario green — exactly one start (1-entry accepted call-log slice); zero driver calls after admission; record-only leaves never dispatched (fixture dispatch record==0, A/B event stream); Tier-A ledger complete (all scopes pending=unresolved=frontier=0, digest present); GoalEvidenceProduced precedes RunCompleted; G1–G4 pass; JSON+MD report returned.
- [x] 5.2 Implement S2 (Runtime Autonomous Exception Disposition — revised 2026-08-26 by Human decision REVISE_SPEC_WITHOUT_RUNTIME_CHANGE): exactly one `run.strategy.start` call; anomaly injection (unclassifiable node / popup / external boundary / unexpected navigation); asserts zero Emulator intervention from admission to terminal and a disposition outcome under the revised contract — `PASS_RECOVERED` (real recovery evidence + continued execution) or `PASS_BOUNDED_FAIL_CLOSED` (Runtime-originated terminal failure, explicit FailureReason backed by EvidenceRef/lifecycle events, no unbounded retry, no hidden fallback). Bounded fail-closed is never labeled recovery success; absent recovery evidence is never fabricated; terminal assertions are not weakened. The result records `STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN / NOT_PURCHASED_BY_PHASE_2_5` (capability gap preserved for a separate future buyer).
  - Evidence (2026-08-26, semantic remediation applied per Human decision REVISE_SPEC_WITHOUT_RUNTIME_CHANGE): `Scenarios/ExceptionDispositionScenario.cs` + `ExceptionDispositionScenarioTests` (2/2 green). Mid-run unexpected-navigation anomaly (fixture scheduling at observation +4, verified to land after initial grounding — probe showed the +1 offset surfaced as a startup foreground mismatch, corrected); exactly one accepted start; zero Emulator intervention (run-slice call log = single start only); outcome **PASS_BOUNDED_FAIL_CLOSED** — Runtime-originated terminal Failed with explicit reason 'post-action transition did not settle within 3 fresh observations；fail closed（composition policy；zero redispatch）' (zero redispatch = no retry storm, honestly evidenced), lifecycle events [ActionDispatched, RunFailed] + snapshot diagnostics present, no hidden fallback; recovery evidence honestly absent (RECOVERY=False, never fabricated); STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN / NOT_PURCHASED_BY_PHASE_2_5 recorded verbatim in every result. Runtime source untouched (zero Trap/Recovery/wire/API added). Tier A revalidation: harness 51/51, full deterministic 2103/2103 + Semantic 32/32, architecture guards 61/61, consistency ALL PASS, strict PASS, diff-check PASS.
  - Superseded evidence note: the prior S2 (Runtime Autonomous Recovery) was BLOCKED_FOR_SPEC (STOPPED_AT_S2_RECOVERY_EVIDENCE_UNOBTAINABLE_ON_STRATEGY_RUN_SURFACES, 2026-08-26) — that block is resolved by this semantic revision, not by Runtime changes: the trap/recovery vocabulary remains absent from the strategy path by design and the harness now proves the disposition contract truthfully on the existing surfaces.
- [x] 5.3 Implement S3 (Cross-Run Adaptation Simulation): Run 1 → harness-local analysis → Run 2 with new `StrategyId`; asserts two distinct one-Directive-one-Run executions, Result 1 influencing only Run 2 strategy (never Runtime state/evidence), insertion point outside Runtime.
  - Evidence (2026-08-26, WI-EVH-005 accepted): CrossRunAdaptationScenario green — two distinct one-Directive-one-Run executions (distinct runIds/strategyIds, shared log exactly 2 accepted starts, distinct payload digests); Result-1 coverage → harness-local pure analysis → NEW StrategyId embedding derived digest; payload diff = strategyId ONLY (influence confined to Run 2 directive); insertion point 'Historical Result → Strategy' outside Runtime operationally proven (Run-1 snapshot/events/trap re-read unchanged after Run 2); G1–G4 pass on both runs.
- [x] 5.4 Add scenario capability tests following EvidenceFixture → Runtime Execution → Evidence Evaluation; no fixed click counts, coordinates, page text, UI paths, or action histories.
  - Evidence (2026-08-26): ScenarioRunnerTests S1+S3 capability tests + shared G1–G4/report-section helper; EvidenceFixture → Runtime Execution → Evidence Evaluation throughout; zero fixed click counts/coordinates/page text/UI paths. Leader re-verified: harness 35/35, build 0 errors, consistency ALL PASS, strict PASS, diff-check PASS.

## 6. Evidence Report and Boundary Verifier

- [x] 6.1 Implement tier-scoped JSON/Markdown report rendering Admission / Lifecycle / Snapshot / Trap / Evidence / Coverage / Terminal / Boundary sections; wire tiers mark ledger-level coverage unavailable.
  - Evidence (2026-08-26, WI-EVH-004 accepted): `Reporting/ValidationReport.cs` — JSON + Markdown renderers; all 8 sections + G1–G4; every field renders value+classification+truth-source; Unavailable→'unavailable'+reason; IsPartial→'partial'; wire-tier ledger coverage renders unavailable (asserted).
- [x] 6.2 Implement the Boundary Verifier from derived evidence only: call-log scan (zero mutating calls, exact start counts), payload scans, A/B event-source classification, and `EvidenceRef` resolution through `evidence.get`; no Runtime instrumentation.
  - Evidence (2026-08-26): `Reporting/BoundaryVerifier.cs` — pure derived proof from (a) immutable call log (zero foreign methods + exact start count), (b) payload re-scans via StrategyDirectiveValidator over actually-transported payloads, (c) A/B event-kind classification (C-class absent), (d) evidence.get resolution outcomes; fail-closed on incomplete proof; zero Runtime instrumentation.
- [x] 6.3 Wire the four gates as explicit report fields: G1 directive legal, G2 end-to-end autonomy, G3 Result Evidence-backed, G4 boundary clean.
  - Evidence (2026-08-26): `Reporting/ValidationGates.cs` — G1 directive-legal / G2 end-to-end autonomy / G3 field-walk classification invariant / G4 boundary clean as explicit GateOutcome(pass, evidence, offending) report fields.
- [x] 6.4 Add boundary-verifier tests: injected-action payload flagged, simulated mutating call flagged, unresolvable evidence ref flagged, clean run yields positive bound evidence for all four prohibitions.
  - Evidence (2026-08-26): BoundaryVerifierTests 8/8 green — injected payload flagged w/ offending record, simulated mutating call flagged, unresolvable ref flagged (G4-only fail, G1-G3 pass), clean run 4 positive bounds + gates pass, JSON+MD sections, wire-tier unavailable, forced gate failure rendered not masked. Leader re-verified: harness 33/33, full deterministic 2085/2085 + Semantic 32/32, consistency ALL PASS, strict PASS, diff-check PASS. Recorded deviations (non-blocking): BoundarySection placeholder consumed by ValidationReport.Boundary; clean-run no-fabrication bound vacuous-positive explicitly stated.

## 7. Failure Classification and Guardrails

- [x] 7.1 Implement protocol failure classification (Strategy Compilation / Discovery / Grounding / Authorization / Execution / Recovery / Environment / Test Harness) with First Divergence Point recording; forbid bare "Runtime failed" conclusions.
  - Evidence (2026-08-26, WI-EVH-006 accepted): `Classification/FailureOwner.cs` + `ProtocolFailureClassifier.cs` — fixed eight-owner taxonomy; owner+FDP type-level construction guard makes bare 'Runtime failed' unrepresentable; classification derives from existing evidence only; BLOCKED_FOR_SPEC marker → Recovery owner, IsFailure=false metadata (never alters gates). 9 classifier capability tests green.
- [x] 7.2 Add source-shape/dependency guards asserting the harness declares no Planner inference, no mutation surface, no FSM surface, and no scenario knowledge tokens beyond its own fixtures; frozen wire/DTO source remains byte-identical (reuse existing wire-guard pattern).
  - Evidence (2026-08-26): `HarnessSourceShapeGuardTests.cs` 5 guards green — planner-inference token ban (DIRECTIVE_REQUIRED carve-out only), mutation/FSM/DeviceAction surface ban, scenario-token neutrality (minimal enumerated whitelist: 5 Fixtures files + EmulatorDriverTests), frozen-wire SHA-256 byte-identity over 7 files (baseline post-WI-EVH-004, harness edits zero), zero-reverse-reference incl. PhysicalHost + allowed forward edges only.
- [x] 7.3 Confirm no harness test depends on fixed click counts, coordinates, page text, or UI paths; tests validate capabilities.
  - Evidence (2026-08-26): reviewed all ValidationHarness test assertions — capability-shaped only (admission legality, autonomy, classified fields, ledger accounting, boundary cleanliness, classification correctness, source shape, frozen bytes); zero fixed click counts/coordinates/page text/UI paths in assertions. Leader re-verified independently.

## 8. Deterministic and Regression Verification

- [x] 8.1 Run the harness capability/scenario suite plus the full deterministic Runtime test suite and Semantic suite; verify architecture guards and `scripts/check-consistency.sh` still pass.
  - Evidence (2026-08-26, Leader-executed final battery): harness capability/scenario suite 49/49; full deterministic Runtime suite 2101/2101 + Semantic 32/32 (excluding RealDevice/RealEmulator/RealityBaseline); architecture guards green (included in the 2101); scripts/check-consistency.sh ALL PASS.
- [x] 8.2 Run `dotnet build src/UniClaw.Runtime.sln` (0 warnings / 0 errors), `openspec validate uniagent-emulator-validation-harness --strict`, and `git diff --check`.
  - Evidence (2026-08-26): dotnet build src/UniClaw.Runtime.sln 0 errors; openspec validate uniagent-emulator-validation-harness --strict PASS; git diff --check PASS.
- [x] 8.3 Confirm `git diff` over frozen Strategy/wire/DTO/protocol source files is empty; record real-device tier limitations honestly (Tier B/C execution requires Human-approved device access and is not required for this change's capability tests).
  - Evidence (2026-08-26): git diff over frozen Strategy/wire/DTO/protocol sources shows only the pre-existing in-flight Phase-2 tracked diff (7 files, byte-identical to session start; harness SHA-256 guard now enforces byte-identity going forward with baseline post-WI-EVH-004). Real-device tier: 7 RealDevice/RealEmulator tests fail-closed on absent ADB device (environmental precondition; Human-approved device access required for Tier B/C, not required for this change's capability tests per design).

## 9. Validation Report and Graduation Readiness

- [x] 9.1 Produce the Phase 2.5 validation report from the harness outputs (the change's output artifact); the report contains the three scenarios, four gates, boundary evidence, and failure classifications.
  - Evidence (2026-08-26): Phase 2.5 validation report produced as PROJECT_LEADER_UNIAGENT_EMULATOR_VALIDATION_HARNESS_IMPLEMENTATION_RESULT (session output artifact): three scenarios (S1 pass, S2 BLOCKED_FOR_SPEC with static+empirical evidence and three adjudication options, S3 pass), four gates evaluated per scenario, boundary evidence via derived-only verifier, failure classification taxonomy wired. Report content derives exclusively from harness outputs.
- [x] 9.2 Have an independent reviewer rebuild the Spec → symbol → test → executed-evidence map without trusting task checkboxes; reopen overstated tasks and stop on any protocol/authority/wire/lifecycle pressure.
  - Evidence (2026-08-26): Leader independent verification throughout — each increment's acceptance re-ran build/tests/consistency independently (never trusting worker self-report); Spec → symbol → test → executed-evidence map reconstructed in the implementation result; the one overstated-risk area (S2) was caught by worker escalation AND re-verified by direct source inspection (Agent.OpenWorld.cs zero trap machinery on strategy path) rather than accepted from the report.
- [x] 9.3 Only after all gates pass may the Human record lifecycle conclusions (Phase 2.5 outcome, Phase 3 Memory resume disposition); this change itself does not graduate, archive, or resume anything.
  - Not executed by design (2026-08-26): lifecycle conclusions (Phase 2.5 outcome, Phase 3 Memory resume disposition, S2 adjudication) remain Human-owned; this change does not graduate, archive, or resume anything. The implementation result presents the S2 Human Gate options without selecting one.
- [x] 9.4 Do not archive, commit, merge, clean, or reset as part of this change without explicit Human instruction.
  - Evidence (2026-08-26): no archive, no commit, no merge, no clean, no reset performed; working tree retains all increments as untracked harness files + the pre-existing Phase-2 tracked diff.


### Checkbox-state repair (2026-08-26, graduation review)

独立毕业评审发现：实现期间的 evidence 行均已如实记录，但多数 checkbox 未同步勾选
（Python 回填脚本的替换缺陷；3/30 → 30/30）。本次按「每个任务下存在对应 Evidence 行」
逐项核对后修复勾选状态；任务内容与 Evidence 未改动。Task 5.2 含修订后语义的实现
证据；9.3/9.4 为按设计不执行类任务，其 Evidence 行即结论。

### 9.5 Tier B execution + Tier C waiver (2026-08-26, post-implementation)

- Tier B (Real Emulator, Human-authorized): S1 **PASS @ real 8/8** (`docs/work/active/tierb-s1-8of8-PASS.json`, 51 events, zero Emulator intervention, Runtime source untouched); S2 **PASS_BOUNDED_FAIL_CLOSED** (real force-stop anomaly, autonomous zero-redispatch terminal); S3 **PASS** (distinct runs, adaptation confined to Run-2 strategyId). Remediations along the way were all Validation-Harness-owned (goal-evaluation alignment to the fixture 8/8 contract; scenario-acceptance layer RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS; viewport-union inventory + scroll-until-exhausted; page-context/truncated classifier alignment to the graduated capstone semantics; vision-only composition realignment). Full regression after each step.
- Tier C (Physical Device): **WAIVED_BY_HUMAN** (path B, 2026-08-26) — execution was blocked by absent physical hardware (USB enumeration + adb devices evidence), and the Human ruled Tier B fidelity sufficient for the Phase 2.5 conclusion. Result: `docs/work/active/uniagent-emulator-tierc-physical-device-validation-result.md`.
- Phase 2.5 recommendation recorded: **READY_FOR_GRADUATION_REVIEW** (graduation itself remains Human-owned; this change does not graduate or archive itself).

## Design Docs

> Auto-generated from proposal Impact section and refined for this module.
> Implementation agents: read these before starting.

| Module / concern | Design Doc |
|---|---|
| Harness scope and buyer | `openspec/changes/uniagent-emulator-validation-harness/proposal.md` |
| Harness architecture (D1–D7) | `openspec/changes/uniagent-emulator-validation-harness/design.md` |
| Normative harness behavior | `openspec/changes/uniagent-emulator-validation-harness/specs/uniagent-emulator-validation-harness/spec.md` |
| Approved Phase 2.5 protocol | `docs/decisions/runtime-exploration-roadmap-phase2-consistency-analysis.md` (roadmap context) + Phase 2.5 protocol approval record (Human) |
| Strategy Contract authority | `openspec/changes/uniagent-runtimeagent-strategy-contract/specs/uniagent-runtimeagent-strategy-contract/spec.md` |
| Ledger/depth authority | `openspec/changes/runtime-exploration-ledger-and-depth-control/specs/runtime-exploration-ledger-and-depth-control/spec.md` |
| Runtime authority | `docs/system/constitution/runtime-architecture-contract.md` |
| Runtime module map | `src/UniClaw.Runtime/AGENTS.md` |
| Test module map | `tests/UniClaw.Runtime.Tests/AGENTS.md` |