## Purpose

Defines the Phase 2.6 validation-only acceptance for (a) upper-agent cross-run plan adaptation from runtime evidence and (b) autonomous real-tree full traversal on the Real Emulator — re-asserting graduated single-run autonomy every run. This change is validation tooling: it introduces no runtime capability, no formal Memory, no Planner, and no UniAgent implementation. `Upper Agent learns; Runtime executes fresh.`

## ADDED Requirements

### Requirement: Validation tooling, never runtime or planning capability

The harness SHALL consume only the frozen strategy surface (`run.strategy.start`) and frozen read-only wire, reusing the graduated Phase 2.5 per-run composition. The change MUST NOT modify Runtime production source, add a wire method or Runtime API, alter the Strategy Contract, GoalEvidence/FSM/Traversal authority, or SourceIdentity semantics, implement Runtime Memory or learning, introduce dynamic depth or mid-run replanning, or implement UniAgent/Planner. Any discovered pressure to do so SHALL stop the change with `STOPPED_AT_RUNTIME_OR_CONTRACT_GAP` and First Divergence Point evidence.

#### Scenario: Harness stays outside Runtime

- **WHEN** the change's sources are compared with the frozen runtime/contract baseline
- **THEN** runtime production and frozen contract files are byte-identical and all new code lives in validation tooling and tests

### Requirement: Frozen iterative loop with independent runs

The harness SHALL drive a campaign loop: User Goal → initial conservative plan → StrategyDirective → independent Runtime Run → Result/Evidence → upper-agent interpretation → ScenarioKnowledgeFixture update → PlanDelta → next StrategyDirective (new StrategyId, independent RunId). Each run SHALL be start-time-immutable with fresh Observation, fresh Grounding, and fresh Authorization, exactly one `run.strategy.start`, and zero emulator mid-run control calls. The loop SHALL terminate on bounded scope exhaustion, an explicitly unsafe remaining frontier, or an evidenced Runtime/Contract gap.

#### Scenario: Every run is autonomous and independent

- **WHEN** any run in the campaign executes
- **THEN** the emulator call log for that run contains exactly the single accepted start, the terminal is reached through the existing Agent-owned path, and the strategy for that run was fixed before start

### Requirement: Four frozen invariants

Every campaign artifact SHALL preserve: `HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH`; `HISTORICAL_RESULT != RUNTIME_ACTION_AUTHORITY`; `RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS`; `AUTONOMOUS_EXCEPTION_DISPOSITION != UNIVERSAL_RECOVERY`. These SHALL be asserted per run, not assumed.

#### Scenario: Knowledge never substitutes for fresh evidence

- **WHEN** loaded fixture knowledge conflicts with a run's fresh runtime evidence
- **THEN** the fresh evidence wins, and the contradicting knowledge is downgraded to `CONTRADICTED`/`STALE`, superseded, or invalidated — never force-applied over current reality

### Requirement: ScenarioKnowledgeFixture as a validation test asset

Knowledge SHALL be maintained as a scenario-scoped, versioned, human-readable, diffable, deterministic, provenance-bearing fixture asset (recommended name `ScenarioKnowledgeFixture`). Each record SHALL carry `{KnowledgeType, SemanticAnchor, SourceRunId, EvidenceRefs, ObservedRole, Scope, Disposition, Confidence, ValidityAssumption, Version, Status, Supersedes/SupersededBy}` with Status ∈ {ACTIVE, STALE, CONTRADICTED, SUPERSEDED, INVALIDATED}. KnowledgeType SHALL be restricted to graduated vocabulary: KnownContainer, KnownRecordOnly, KnownLocalControl, KnownExternalBoundary, KnownNonInteractive, KnownUnresolved, KnownPotentiallyStateMutating — no new runtime semantics. The fixture SHALL carry explicit scope (scenario id, app/package, semantic capability version, Android/emulator assumptions, locale, created-from run set); implicit global knowledge and automatic cross-app/cross-version/cross-scenario reuse are forbidden. The fixture is a validation asset, not a production Memory component: `TEST_KNOWLEDGE != RUNTIME_TRUTH`, `TEST_KNOWLEDGE != ACTION_AUTHORITY`, `TEST_KNOWLEDGE != FORMAL_MEMORY`.

#### Scenario: Provenance-gated admission

- **WHEN** a knowledge record is admitted to the fixture
- **THEN** it traces to a SourceRunId and EvidenceRefs of an observed result, and records lacking provenance are rejected

#### Scenario: Forbidden knowledge sources are rejected

- **WHEN** a candidate record originates from guesswork, hardcoded UI text as truth, coordinates, fixed page paths, selector scripts, probing-by-execution, or assumptions about runtime internals
- **THEN** it is not admitted

#### Scenario: Human-readable persisted asset

- **WHEN** a fixture is frozen after a campaign
- **THEN** it is persisted as human-readable, diffable, deterministic, versioned content with explicit scope — never an opaque blob as the sole knowledge representation

### Requirement: Knowledge persistence and cross-campaign reuse

The harness SHALL support freezing a fixture after a campaign and loading it in a later validation session (clean emulator) as advisory input to the initial plan, reducing repeated exploration and dangerous attempts. Reuse MUST NOT replace fresh runtime evidence in any run.

#### Scenario: Persisted fixture improves a fresh campaign's initial plan

- **WHEN** campaign B starts from a clean emulator with fixture v1 loaded
- **THEN** its initial plan reflects v1's knowledge (e.g. state-mutating classes planned as record-only/prohibited from the start) while the runtime still fully re-observes, re-grounds, and re-authorizes every node

### Requirement: Safety learning without dangerous trial-and-error

The default posture SHALL be `UNPROVEN_SAFE → RECORD_ONLY / FAIL_CLOSED`. Dangerous classes (factory reset, data deletion, account mutation, security configuration, developer/system dangerous controls, install/uninstall, payment/authentication, critical network mutation, destructive or state-mutating effects) SHALL be identified only through observational evidence, typed semantic capability output, or boundary disposition — never by exploratory execution. Once identified as `KnownPotentiallyStateMutating`, subsequent plans SHALL exclude the class via `StrategyConstraintSet.prohibitedEffects` or graduated dispatch-policy.

#### Scenario: Dangerous dispatch intersection is empty

- **WHEN** the campaign's full action-dispatch history is intersected with the set of nodes known or suspected to be state-mutating/external-boundary
- **THEN** the intersection is empty, and no run ever learns danger by executing it

### Requirement: PlanDelta contract

Each planning round SHALL produce `{PreviousPlan, ObservedResult, LoadedKnowledge, NewKnowledge, RemainingUnknowns, PlanDelta, NextStrategy}`. PlanDelta SHALL cite EvidenceRefs/KnowledgeRefs, explain the change, and land strictly within existing directive freedom (depth, constraints, prohibited effects, dispatch policy, objective, typed criterion, scope, completion). A round without a real PlanDelta SHALL be marked `NO_OP_WITH_REASON` or terminate the loop. PlanDelta SHALL NOT specify UI action sequences, coordinates, selector paths, fixed navigation paths, or mid-run instructions.

#### Scenario: Deltas are evidenced and contract-legal

- **WHEN** a PlanDelta is recorded
- **THEN** its knowledge/evidence citations resolve, the next directive differs only within the frozen freedom set, and the directive passes closed-vocabulary validation

### Requirement: Phase 2.6A — iterative planning acceptance

2.6A SHALL validate `UPPER_AGENT_CROSS_RUN_PLAN_ADAPTATION` via (A) at least three genuine online Result→Plan adaptations within one campaign, and (B) at least one persisted-fixture reuse across campaigns. Acceptance SHALL prove with evidence links: every PlanDelta has provenance; KnownRecordOnly nodes cease to be exploratory dispatch targets; KnownExternalBoundary classes are no longer planned as recursive children; KnownLocalControl classes are no longer navigation targets; KnownPotentiallyStateMutating nodes are never executed; the unresolved set shrinks or every non-shrink is explained by new evidence; repeated-exploration cost improvements are traceable to Knowledge/PlanDelta; the persisted fixture improves a fresh campaign's initial plan; stale/contradicted knowledge never remains active advisory; every run has zero emulator mid-run intervention; historical knowledge never substitutes for fresh runtime evidence. Click-count reduction alone SHALL NOT constitute acceptance.

#### Scenario: Online adaptation is real

- **WHEN** the campaign's rounds are reviewed
- **THEN** at least three PlanDeltas each trace to observed-result evidence and produce behaviorally visible strategy differences (dispatch-surface exclusions, boundary exclusions, depth/scope changes)

### Requirement: Phase 2.6B entry gate

Phase 2.6B (Simulator Full Traversal) SHALL be entered only when online adaptation PASSes, persisted-knowledge reuse PASSes, provenance is complete, safety assertions PASS (dangerous-dispatch intersection empty), stale/contradiction semantics PASS, remaining unknowns are honest, no Runtime gap exists, and regression is green.

#### Scenario: Gate is enforced

- **WHEN** any 2.6A criterion fails
- **THEN** Stage D is not entered and the failure is recorded with evidence

### Requirement: Phase 2.6B — simulator full traversal acceptance

Using a mature fixture-informed plan and a single (or minimal set of) independent run(s) on the Real Emulator's real Android Settings, the harness SHALL validate `RUNTIME_AGENT_CAN_AUTONOMOUSLY_EXHAUST_A_REAL_BOUNDED_UI_TREE`: autonomous inventory discovery, recursive descent, scroll exhaustion, logical-identity correctness (no double-count, no false merge), verified parent return with sibling frontier continuation, revisit correctness, honest unknown/unresolved accounting, external boundary disposition without crossing, prohibited-effect safety, bounded tree exhaustion, GoalEvidence-backed FSM terminal, and an independent external Scenario Acceptance.

#### Scenario: Full traversal is evidenced end-to-end

- **WHEN** the traversal run completes
- **THEN** the ledger/evidence/events reconcile with independent scenario acceptance, terminal completion is GoalEvidence+FSM-owned, and `RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS` remains enforced

### Requirement: SettingsStrategyBinding adapts without inventing

A harness-local SettingsStrategyBinding SHALL present the existing production `SettingsSemanticCapability` to the graduated strategy execution surface. It MUST NOT inject fixture knowledge into the runtime, add semantic meanings, hardcode UI text as runtime truth, or introduce fixed page paths, click sequences, selectors, or coordinates.

#### Scenario: Binding is a pure adapter

- **WHEN** the binding's sources are inspected
- **THEN** they only adapt production capability output to the binding interface and contain no knowledge injection, truth injection, or navigation scripting

### Requirement: Advisory Knowledge Package and physical-device deferral

Physical-device validation is OUT_OF_SCOPE/DEFERRED. Only after both 2.6A and 2.6B pass may a Simulator-derived Advisory Knowledge Package be derived from a mature fixture. The package SHALL be usable only as UniAgent pre-Run planning advisory for a future, separately gated physical campaign, and MUST NOT enter runtime belief, observation, grounding, authorization, or GoalEvidence.

#### Scenario: Deferral is explicit

- **WHEN** the change completes
- **THEN** no physical-device claim is made and the advisory package (if produced) is marked advisory-only with provenance and scope assumptions

### Requirement: Phase 3 Memory learning inputs

The campaign SHALL record which knowledge types were actually created, reused, caused PlanDeltas, and were contradicted/superseded/invalidated — as empirical input for the later `uniagent-local-exploration-memory` draft check. Fixture requirements SHALL be derived from observed behavior, not redesigned from the Memory draft, and this recording SHALL NOT constitute Memory implementation or Phase 3 authorization.

#### Scenario: Learning inputs are captured

- **WHEN** the campaign ends
- **THEN** a record of knowledge-type lifecycle statistics with provenance exists for the Phase 3 compatibility check

## Explicit Non-Claims

This change does not include: UniAgent implementation; formal Planner; formal Memory (service/database/API); Runtime learning; Runtime cross-run intelligence; dynamic depth; universal Android traversal; universal recovery; physical-device validation; arbitrary-app semantic understanding.
