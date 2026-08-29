## A. Implementation Human Gate

- [x] A.1 Obtain explicit Human approval to implement this change (Phase 2.6 Implementation Gate); record the approved artifact revision/digest before any edit. Creation of this OpenSpec does NOT authorize implementation.
- [x] A.2 Record implementation/review owners and confirm the Real Emulator campaign scope (AVD/image, real Android Settings app, no physical device); confirm no task requires Runtime modification, new wire/API, Strategy Contract change, Memory service, dynamic depth, or mid-run replanning — otherwise stop with `STOPPED_AT_RUNTIME_OR_CONTRACT_GAP`.

## B. Validation Harness Foundation

- [x] B.1 Extend the ValidationHarness (Phase 2.5 pattern) with the iterative campaign runner: N independent runs, per-run exactly one `run.strategy.start`, per-run autonomy assertion, loop termination on exhaustion / unsafe frontier / evidenced gap.
- [x] B.2 Add capability tests: loop independence (distinct StrategyId/RunId per run), zero mid-run intervention per run, termination conditions, per-run re-assertion of all four invariants.

## C. ScenarioKnowledgeFixture Contract

- [x] C.1 Implement the knowledge record contract (all fields incl. Status lifecycle and Supersedes/SupersededBy) with the seven graduated KnowledgeTypes only; provenance-gated admission (SourceRunId + EvidenceRefs mandatory); rejected-source enforcement (guesswork, hardcoded text-as-truth, coordinates, fixed paths, selectors, probe-by-click, runtime-internal guesses).
- [x] C.2 Enforce scope metadata on every fixture (scenario id, app/package, semantic capability version, Android/emulator assumptions, locale, created-from run set); no implicit global knowledge; no automatic cross-app/version/scenario reuse.
- [x] C.3 Implement conflict resolution: current fresh evidence first; downgrade to STALE/CONTRADICTED, supersede, or invalidate — never force-apply old knowledge.
- [x] C.4 Capability tests: admission/provenance gates, forbidden sources, lifecycle transitions, conflict resolution, scope isolation.

## D. Knowledge Persistence / Versioning / Provenance

- [x] D.1 Implement fixture persistence: freeze after campaign, load into a fresh validation session; human-readable, diffable, deterministic, versioned artifacts under a validation-side asset directory (concrete layout per repo conventions; never opaque-blob-only).
- [x] D.2 Capability tests: freeze/load round-trip fidelity, version supersession across freezes, no cross-scope leakage on load.

## E. PlanDelta Recorder

- [x] E.1 Implement the per-round record `{PreviousPlan, ObservedResult, LoadedKnowledge, NewKnowledge, RemainingUnknowns, PlanDelta, NextStrategy}` with mandatory EvidenceRefs/KnowledgeRefs citations and `NO_OP_WITH_REASON` semantics.
- [x] E.2 Enforce contract-legal deltas only (depth/constraints/prohibited effects/dispatch policy/objective/typed criterion/scope/completion); reject action-sequence/coordinate/selector/path deltas via the closed-vocabulary validator.
- [x] E.3 Capability tests: evidenced-delta acceptance, illegal-delta rejection, no-op recording.

## F. SettingsStrategyBinding

- [x] F.1 Implement the harness-local binding adapting the production `SettingsSemanticCapability` to `IStrategySemanticCapabilityBinding` (goal evaluators, inventory, viewport exploration, dispatch policy) with zero knowledge/truth injection and zero navigation scripting.
- [x] F.2 Capability tests: binding purity (no fixture reads, no fixed paths/selectors/coordinates, no new meanings) and admission acceptance on the real Settings scope.

## G. Stage A — Conservative Initial Exploration (Real Emulator)

- [x] G.1 Run the first conservative campaign: UNPROVEN_SAFE → RecordOnly/FailClosed posture; collect Result/Evidence; form the first fixture increment. *(Real-emulator runs with truthful fail-closed terminals; fixture increment = KnownUnresolved records with provenance. See `evidence/G-stage-a/` + STOP report: every run fail-closes at the IR-G0 perception×normalization composition gap — honest PARTIAL, traversal did not occur.)*
- [x] G.2 Record per-run autonomy, invariants, and safety assertions (dangerous-dispatch intersection empty). *(Per-round autonomy + four invariants + gates asserted and PASSED on every real run — `stageAB-adaptive-campaign.json`; dangerous-dispatch intersection empty: navigate-only constraints, no state-mutating category ever authorized.)*

## H. Stage B — Online Evidence-Informed Replanning

- [ ] H.1 Execute ≥3 genuine online adaptations: Result → knowledge → PlanDelta → next independent strategy; each delta behaviorally visible (record-only/boundary/local-control exclusions, depth/scope changes). *(BLOCKED by IR-G0: every reachable scope fails identically, so behaviorally-visible adaptation deltas are impossible; the planner machinery is built + unit-proven 20/20; the real campaign produced provenance-gated knowledge + honest NO_OP_WITH_REASON rounds.)*
- [ ] H.2 Verify acceptance items: provenance per delta; KnownRecordOnly no longer exploratory dispatch targets; KnownExternalBoundary not recursive children; KnownLocalControl not navigation targets; unresolved shrink or explained non-shrink; cost improvement traceable to Knowledge/PlanDelta (not bare click counts). *(BLOCKED — same condition.)*

## I. Stage C — Persisted Knowledge Reuse

- [ ] I.1 Freeze fixture v1; start a clean-emulator campaign loading v1; verify the initial plan reflects v1 (e.g. state-mutating classes prohibited from the start) while every run still fully re-observes/re-grounds/re-authorizes. *(PARTIAL: fixture v1 FROZEN from the real campaign — `validation/knowledge/settings/settings-bounded-traversal/v1/` (3 KnownUnresolved records, provenance run-1..4, byte-deterministic re-freeze verified, admission 3/3 via the real gate). The clean-emulator reuse campaign remains BLOCKED by IR-G0.)*
- [ ] I.2 Verify stale/contradicted knowledge never remains active advisory; verify fresh-evidence-wins in at least one engineered conflict case. *(BLOCKED at campaign level; conflict semantics verified at capability-test level. NOTE: frozen v1's KnownUnresolved root records will naturally become the real conflict case at re-entry — post-perception-fix, fresh root-normalization evidence CONTRADICTS them, exercising fresh-evidence-wins on real data.)*

## J. Phase 2.6A Independent Acceptance

- [ ] J.1 Independent reviewer re-verifies 2.6A acceptance (A online adaptation + B persisted reuse) from artifacts without trusting checkboxes; all safety assertions (esp. empty dangerous-dispatch intersection) re-checked from raw evidence. *(NOT ENTERED: 2.6A criteria cannot PASS — H/I blocked by IR-G0. Determination: PHASE26A_ACCEPTANCE_RESULT = BLOCKED, see STOP report.)*

## K. Stage D — Simulator Full Traversal (2.6B)

- [ ] K.1 Enter only through the 2.6B entry gate (all 2.6A criteria PASS, provenance complete, safety PASS, honest unknowns, no Runtime gap, regression green). *(NOT ENTERED — gate enforced: 2.6A did not PASS.)*
- [ ] K.2 Execute the mature-strategy full traversal of real Android Settings on the Real Emulator; prove autonomous inventory, recursive descent, scroll exhaustion, identity correctness, verified returns + sibling continuation, revisit correctness, honest unresolved accounting, boundary dispositions, prohibited-effect safety, bounded exhaustion, GoalEvidence+FSM terminal. *(BLOCKED by IR-G0 — no reachable real Settings scope normalizes.)*
- [ ] K.3 Independent external Scenario Acceptance reconciliation (RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS enforced); ledger/evidence/events cross-accounting. *(NOT REACHED.)*

## L. Advisory Knowledge Package

- [ ] L.1 After 2.6A+2.6B pass: derive the Simulator-derived Advisory Knowledge Package from a mature fixture (advisory-only, provenance + scope assumptions) reserved for a future separately gated physical-device campaign. *(NOT PRODUCED — 2.6A+2.6B did not pass; no physical-device claim is made.)*

## M. Full Regression

- [x] M.1 Run harness targeted suite, Runtime deterministic full suite, Semantic suite, architecture guards, `scripts/check-consistency.sh`, `git diff --check`, and strict OpenSpec validation; confirm runtime production byte-identity and that archived Phase 2/2.5 bundles and the Phase 3 draft are untouched. *(See `evidence/M-full-regression.md`: build 0 err; 2213/2215 (2 pre-existing environmental RealDevice tests); Semantic 32/32; new capability tests 97/97; consistency ALL PASS; git diff --check clean; Runtime byte-identity 0/216 deviations; OpenSpec strict 18/18.)*

## N. Independent Graduation Readiness

- [ ] N.1 Rebuild Spec → symbol → test → executed-evidence map independently; record Phase 3 Memory learning inputs (knowledge-type lifecycle statistics with provenance). *(PARTIAL via leader's final report; independent reviewer pass not reached.)*
- [ ] N.2 Produce the graduation readiness report; lifecycle conclusions (graduation/archive/physical gate/Phase 3 disposition) remain Human-owned. *(Leader produced the implementation result; lifecycle conclusions reserved to Human Gate.)*

## Design Docs

> Implementation agents: read these before starting.

| Concern | Doc |
|---|---|
| Buyer claims / scope / non-claims | `proposal.md` |
| Loop / knowledge / safety / binding decisions (D1–D7) | `design.md` |
| Normative behavior | `specs/runtime-iterative-full-traversal-acceptance/spec.md` |
| Gap analysis + FDP | `docs/decisions/runtime-full-traversal-acceptance-analysis.md` |
| Iterative-planning gate design | `docs/decisions/runtime-simulator-iterative-planning-gate-design.md` |
| Prior harness authority | `openspec/changes/archive/2026-08-26-uniagent-emulator-validation-harness/` |
