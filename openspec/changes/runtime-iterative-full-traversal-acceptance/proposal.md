## Why

Phase 2.5 graduated `PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED`: the vertical execution chain (abstract Strategy → autonomous single Run → evidence-backed terminal) holds on a Real Emulator. But the 8/8 capstone exercised a single-layer tree (root → record-only children): the recursive descent machinery (parent push/pop, verified return re-entry, sibling frontier continuation) has never executed on the strategy wire against a real unknown tree, and nothing has yet validated an upper agent improving its plans across runs from runtime evidence.

This change answers both remaining buyer questions in one validation-only campaign on the Real Emulator:

1. Can an upper agent, given only each run's frozen read-only evidence, form provenance-bearing knowledge, revise its plan, and issue a safer/more effective next independent StrategyDirective — approaching a bounded real-tree exhaustion across runs (`Upper Agent learns; Runtime executes fresh`)?
2. Can the RuntimeAgent, handed one mature strategy, autonomously exhaust a real bounded UI tree (recursive descent, scroll exhaustion, identity correctness, verified returns, boundary dispositions, honest unresolved accounting, GoalEvidence+FSM completion)?

Analysis (`docs/decisions/runtime-full-traversal-acceptance-analysis.md` + `runtime-simulator-iterative-planning-gate-design.md`) established the gap is **validation-only**: every required runtime mechanism is already graduated in frozen code (recursive traversal at real-device depth 3 via the legacy entry; scroll exhaustion, depth semantics, identity-exact ledger, boundary dispositions, popup handling on the strategy path or graduated capability changes). No runtime modification is planned or authorized.

## What Changes

- Add validation tooling only (succeeding the graduated Phase 2.5 harness pattern):
  - **Iterative campaign runner** — drives N independent `run.strategy.start` runs on the Real Emulator's real Android Settings, each run: fresh observation, fresh grounding, fresh authorization, zero emulator mid-run intervention, start-time-immutable strategy.
  - **ScenarioKnowledgeFixture** — a scenario-scoped, provenance-bearing, versioned, human-readable, diffable validation asset that records knowledge records typed ONLY in graduated semantic vocabulary (`KnownContainer`, `KnownRecordOnly`, `KnownLocalControl`, `KnownExternalBoundary`, `KnownNonInteractive`, `KnownUnresolved`, `KnownPotentiallyStateMutating`). It is a test asset, never runtime state, belief, action authority, or a production Memory component. Fixtures are incrementally generated, persisted between campaigns, loaded as planning advisory for later initial plans, and support status lifecycle (`ACTIVE`/`STALE`/`CONTRADICTED`/`SUPERSEDED`/`INVALIDATED`) with fresh-evidence-wins conflict resolution.
  - **PlanDelta recorder** — each planning round must produce `{PreviousPlan, ObservedResult, LoadedKnowledge, NewKnowledge, RemainingUnknowns, PlanDelta, NextStrategy}` where PlanDelta cites EvidenceRefs/KnowledgeRefs and lands as a legal StrategyDirective difference within existing contract freedom (depth, constraints, prohibited effects, dispatch policy, objective, typed criterion, scope, completion). No-delta rounds are `NO_OP_WITH_REASON` or terminate the loop.
  - **SettingsStrategyBinding** — harness-local adapter presenting the existing production `SettingsSemanticCapability` to the graduated strategy execution surface. Injects no fixture knowledge, no UI text truth, no new meanings, no fixed paths/selectors/coordinates.
  - **Safety semantics** — `UNPROVEN_SAFE → RECORD_ONLY/FAIL_CLOSED` default; dangerous classes (factory reset, deletion, account/security mutation, developer controls, install/uninstall, payment/auth, critical network) are identified only via observational/typed/boundary evidence, never exploratory execution; `KnownPotentiallyStateMutating` knowledge must shape the NEXT strategy through `StrategyConstraintSet.prohibitedEffects` / graduated dispatch-policy.
- Freeze as spec behavior: the iterative loop contract, knowledge record/admission/persistence/reuse contracts, safety-learning rules, plan-revision contract, the four inequality invariants, Phase 2.6A acceptance (≥3 online adaptations + persisted-knowledge reuse across campaigns), the 2.6A→2.6B entry gate, and Phase 2.6B full-traversal acceptance criteria.
- Produce, after 2.6B, a **Simulator-derived Advisory Knowledge Package** (derived from a mature fixture) reserved for a future, separately gated physical-device campaign as UniAgent pre-Run planning advisory only.
- Record empirical Memory learning inputs (which knowledge types were created/reused/contradicted/superseded) to later check the `uniagent-local-exploration-memory` draft — without redesigning the fixture from the draft.

## Capabilities

### New Capabilities

- `runtime-iterative-full-traversal-acceptance`: validation tooling that (a) re-asserts graduated `RUNTIME_SINGLE_RUN_AUTONOMY` every run, (b) validates the new `UPPER_AGENT_CROSS_RUN_PLAN_ADAPTATION` capability — an upper agent forming provenance-bearing scenario knowledge from runtime evidence and revising plans across independent runs — and (c) validates `RUNTIME_AGENT_CAN_AUTONOMOUSLY_EXHAUST_A_REAL_BOUNDED_UI_TREE` on the Real Emulator via a mature, knowledge-informed strategy. Validation-only; the ScenarioKnowledgeFixture is a test asset, not a production Memory component.

### Modified Capabilities

- None. Runtime production source, wire/DTOs, Strategy Contract, GoalEvidence/FSM/Traversal authority, SourceIdentity semantics are untouched; graduated Phase 2 / 2.5 / earlier capabilities are consumed as-is.

## Impact

- Production scope: NONE (`src/UniClaw.Runtime`, DriverHost, Harness byte-identical; zero new Runtime API/wire; frozen surfaces guarded).
- New tooling scope: validation harness extensions (iterative runner, knowledge fixture store + versioned asset directory, PlanDelta recorder, SettingsStrategyBinding) plus capability tests under the ValidationHarness test area; knowledge fixture assets live under a validation-side asset directory whose concrete layout is decided at implementation per repo conventions (spec mandates properties: human-readable, diffable, deterministic, provenance-bearing, versioned, scope-explicit — never opaque blobs).
- Classification: **Large Change** (new acceptance surface + multi-campaign Real Emulator validation). OpenSpec creation authorized by Human (2026-08-26); implementation requires a separate explicit Human Gate.
- Stop-condition guard: any pressure to modify Runtime/Contract, add wire/API, authorize dangerous exploratory execution, or treat fixture knowledge as runtime truth stops immediately (`STOPPED_AT_RUNTIME_OR_CONTRACT_GAP`) with FDP evidence; a Physical Device campaign and formal Phase 3 Memory remain out of scope and separately gated.
