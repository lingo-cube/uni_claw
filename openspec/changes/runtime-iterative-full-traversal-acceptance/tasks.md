## A. Implementation Human Gate

- [ ] A.1 Obtain explicit Human approval to implement this change (Phase 2.6 Implementation Gate); record the approved artifact revision/digest before any edit. Creation of this OpenSpec does NOT authorize implementation.
- [ ] A.2 Record implementation/review owners and confirm the Real Emulator campaign scope (AVD/image, real Android Settings app, no physical device); confirm no task requires Runtime modification, new wire/API, Strategy Contract change, Memory service, dynamic depth, or mid-run replanning — otherwise stop with `STOPPED_AT_RUNTIME_OR_CONTRACT_GAP`.

## B. Validation Harness Foundation

- [ ] B.1 Extend the ValidationHarness (Phase 2.5 pattern) with the iterative campaign runner: N independent runs, per-run exactly one `run.strategy.start`, per-run autonomy assertion, loop termination on exhaustion / unsafe frontier / evidenced gap.
- [ ] B.2 Add capability tests: loop independence (distinct StrategyId/RunId per run), zero mid-run intervention per run, termination conditions, per-run re-assertion of all four invariants.

## C. ScenarioKnowledgeFixture Contract

- [ ] C.1 Implement the knowledge record contract (all fields incl. Status lifecycle and Supersedes/SupersededBy) with the seven graduated KnowledgeTypes only; provenance-gated admission (SourceRunId + EvidenceRefs mandatory); rejected-source enforcement (guesswork, hardcoded text-as-truth, coordinates, fixed paths, selectors, probe-by-click, runtime-internal guesses).
- [ ] C.2 Enforce scope metadata on every fixture (scenario id, app/package, semantic capability version, Android/emulator assumptions, locale, created-from run set); no implicit global knowledge; no automatic cross-app/version/scenario reuse.
- [ ] C.3 Implement conflict resolution: current fresh evidence first; downgrade to STALE/CONTRADICTED, supersede, or invalidate — never force-apply old knowledge.
- [ ] C.4 Capability tests: admission/provenance gates, forbidden sources, lifecycle transitions, conflict resolution, scope isolation.

## D. Knowledge Persistence / Versioning / Provenance

- [ ] D.1 Implement fixture persistence: freeze after campaign, load into a fresh validation session; human-readable, diffable, deterministic, versioned artifacts under a validation-side asset directory (concrete layout per repo conventions; never opaque-blob-only).
- [ ] D.2 Capability tests: freeze/load round-trip fidelity, version supersession across freezes, no cross-scope leakage on load.

## E. PlanDelta Recorder

- [ ] E.1 Implement the per-round record `{PreviousPlan, ObservedResult, LoadedKnowledge, NewKnowledge, RemainingUnknowns, PlanDelta, NextStrategy}` with mandatory EvidenceRefs/KnowledgeRefs citations and `NO_OP_WITH_REASON` semantics.
- [ ] E.2 Enforce contract-legal deltas only (depth/constraints/prohibited effects/dispatch policy/objective/typed criterion/scope/completion); reject action-sequence/coordinate/selector/path deltas via the closed-vocabulary validator.
- [ ] E.3 Capability tests: evidenced-delta acceptance, illegal-delta rejection, no-op recording.

## F. SettingsStrategyBinding

- [ ] F.1 Implement the harness-local binding adapting the production `SettingsSemanticCapability` to `IStrategySemanticCapabilityBinding` (goal evaluators, inventory, viewport exploration, dispatch policy) with zero knowledge/truth injection and zero navigation scripting.
- [ ] F.2 Capability tests: binding purity (no fixture reads, no fixed paths/selectors/coordinates, no new meanings) and admission acceptance on the real Settings scope.

## G. Stage A — Conservative Initial Exploration (Real Emulator)

- [ ] G.1 Run the first conservative campaign: UNPROVEN_SAFE → RecordOnly/FailClosed posture; collect Result/Evidence; form the first fixture increment.
- [ ] G.2 Record per-run autonomy, invariants, and safety assertions (dangerous-dispatch intersection empty).

## H. Stage B — Online Evidence-Informed Replanning

- [ ] H.1 Execute ≥3 genuine online adaptations: Result → knowledge → PlanDelta → next independent strategy; each delta behaviorally visible (record-only/boundary/local-control exclusions, depth/scope changes).
- [ ] H.2 Verify acceptance items: provenance per delta; KnownRecordOnly no longer exploratory dispatch targets; KnownExternalBoundary not recursive children; KnownLocalControl not navigation targets; unresolved shrink or explained non-shrink; cost improvement traceable to Knowledge/PlanDelta (not bare click counts).

## I. Stage C — Persisted Knowledge Reuse

- [ ] I.1 Freeze fixture v1; start a clean-emulator campaign loading v1; verify the initial plan reflects v1 (e.g. state-mutating classes prohibited from the start) while every run still fully re-observes/re-grounds/re-authorizes.
- [ ] I.2 Verify stale/contradicted knowledge never remains active advisory; verify fresh-evidence-wins in at least one engineered conflict case.

## J. Phase 2.6A Independent Acceptance

- [ ] J.1 Independent reviewer re-verifies 2.6A acceptance (A online adaptation + B persisted reuse) from artifacts without trusting checkboxes; all safety assertions (esp. empty dangerous-dispatch intersection) re-checked from raw evidence.

## K. Stage D — Simulator Full Traversal (2.6B)

- [ ] K.1 Enter only through the 2.6B entry gate (all 2.6A criteria PASS, provenance complete, safety PASS, honest unknowns, no Runtime gap, regression green).
- [ ] K.2 Execute the mature-strategy full traversal of real Android Settings on the Real Emulator; prove autonomous inventory, recursive descent, scroll exhaustion, identity correctness, verified returns + sibling continuation, revisit correctness, honest unresolved accounting, boundary dispositions, prohibited-effect safety, bounded exhaustion, GoalEvidence+FSM terminal.
- [ ] K.3 Independent external Scenario Acceptance reconciliation (RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS enforced); ledger/evidence/events cross-accounting.

## L. Advisory Knowledge Package

- [ ] L.1 After 2.6A+2.6B pass: derive the Simulator-derived Advisory Knowledge Package from a mature fixture (advisory-only, provenance + scope assumptions) reserved for a future separately gated physical-device campaign.

## M. Full Regression

- [ ] M.1 Run harness targeted suite, Runtime deterministic full suite, Semantic suite, architecture guards, `scripts/check-consistency.sh`, `git diff --check`, and strict OpenSpec validation; confirm runtime production byte-identity and that archived Phase 2/2.5 bundles and the Phase 3 draft are untouched.

## N. Independent Graduation Readiness

- [ ] N.1 Rebuild Spec → symbol → test → executed-evidence map independently; record Phase 3 Memory learning inputs (knowledge-type lifecycle statistics with provenance).
- [ ] N.2 Produce the graduation readiness report; lifecycle conclusions (graduation/archive/physical gate/Phase 3 disposition) remain Human-owned.

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
