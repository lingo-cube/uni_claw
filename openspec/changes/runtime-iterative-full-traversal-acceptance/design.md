## Context

Authority baselines: Runtime Architecture Contract I-1..I-14; Architecture v1; the graduated
Phase 2 change set (`runtime-exploration-ledger-and-depth-control` +
`runtime-exploration-semantic-admission-remediation`, archived 2026-08-26); the graduated
Phase 2.5 `uniagent-emulator-validation-harness` (archived; capability
`PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED`); the graduated real-device Settings
traversal capstone (`SETTINGS_TREE_CAPSTONE_PROVEN`, legacy entry, depth 3); the two
analysis/design records `docs/decisions/runtime-full-traversal-acceptance-analysis.md` and
`docs/decisions/runtime-simulator-iterative-planning-gate-design.md` (2026-08-26).

Current execution reality (verified at source):

- The strategy wire reaches the full recursive open-world machine:
  `StrategyExecution.RunAsync → RunStrategyOpenWorldAsync(semantics) → Agent.RunOpenWorldAsync(maximumDepth …)`
  — the same recursive descent (parents stack, verified parent return ×4 call sites),
  scroll exhaustion (`ExploreCurrentContainerViewportsAsync` + `SourceEquivalenceNormalizer`
  overlap union), depth semantics (RESAR `ExplorationExecutionSemantics`, admission-derived,
  `MaximumSupportedDepth=64`), identity-exact ledger accounting, and EBD boundary machinery
  used by the graduated legacy-entry real-device depth-3 capstone.
- The strategy directive carries exactly the plan-revision levers an upper agent needs:
  `MaximumDepth`, `StrategyConstraintSet` (allowed categories + `StrategyProhibitedEffect`:
  StateMutation / ExternalBoundaryCrossing), per-strategy `CreateDispatchPolicy`,
  `StrategyObjectiveKind` + typed criterion, `StrategyScope`, `StrategyCompletionKind`.
- The production `SettingsSemanticCapability` (UniClaw.Semantic.Settings) already types real
  Settings semantics (container / preference-row / search-role / navigate-up /
  parent-container, en-US/GB).
- Phase 2.5's harness (EmulatorDriver → ResultCollector → BoundaryVerifier → Gates →
  Scenario Acceptance, wire-closed surface) is graduated and reusable as the per-run
  foundation.

Gap (validation-only, per the analysis): the recursive machine has never executed on the
strategy wire against a real unknown tree (Phase 2.5's reality binding expressed a
single-layer tree), and no cross-run evidence-informed plan adaptation has ever been
validated.

## Goals / Non-Goals

**Goals:**

- Phase 2.6A: validate `UPPER_AGENT_CROSS_RUN_PLAN_ADAPTATION` — ≥3 online
  evidence→knowledge→PlanDelta→next-strategy adaptations within one campaign, plus persisted
  ScenarioKnowledgeFixture reuse across campaigns (clean emulator) improving initial plans —
  while every run re-asserts graduated single-run autonomy and the four invariants.
- Phase 2.6B: validate `RUNTIME_AGENT_CAN_AUTONOMOUSLY_EXHAUST_A_REAL_BOUNDED_UI_TREE` on
  the Real Emulator's real Android Settings with a mature knowledge-informed strategy.
- Safety learning as a first-class requirement: UNPROVEN_SAFE defaults to record-only /
  fail-closed; dangerous classes never learned by execution; knowledge shapes the next
  strategy only through existing contract levers.
- Record empirical Memory learning inputs for the later Phase 3 draft check.

**Non-Goals:**

- No Runtime modification, no new Runtime API/wire, no Strategy Contract or SourceIdentity
  change, no GoalEvidence/FSM/Traversal authority change.
- No UniAgent implementation, no formal Planner, no formal Memory (no Memory service /
  database / API), no Runtime learning or cross-run runtime intelligence, no dynamic depth,
  no mid-run replanning.
- No physical-device campaign (deferred; separately gated; preceded by the Advisory
  Knowledge Package).
- No arbitrary-app semantic understanding; scenario scope is real Android Settings on the
  Real Emulator.

## Decisions

### D1 — One change, two acceptance stages (2.6A + 2.6B)

Phase 2.6A (iterative planning acceptance) and Phase 2.6B (simulator full-traversal
acceptance) share the harness composition, knowledge model, safety rules, and invariants;
2.6B's entry gate is 2.6A's output. Splitting would duplicate boundary definitions and
create cross-change knowledge-model coupling. Lifecycle: serial stages inside one change.

### D2 — ScenarioKnowledgeFixture is a test asset, not ephemeral-only, not Memory

Knowledge lives as a scenario-scoped, versioned, diffable, human-readable fixture asset
(recommended implementation name `ScenarioKnowledgeFixture`; concrete layout decided at
implementation under a validation-side asset directory, e.g. logical
`validation/knowledge/settings/<scenario>/<version>`). It may be incrementally generated,
frozen after a campaign, loaded by a later clean-emulator campaign as planning advisory,
and must carry scope metadata (scenario id, app/package, semantic capability version,
Android/emulator image assumptions, locale, created-from run set). Frozen invariants:
`TEST_KNOWLEDGE != RUNTIME_TRUTH`, `TEST_KNOWLEDGE != ACTION_AUTHORITY`,
`TEST_KNOWLEDGE != FORMAL_MEMORY`. No implicit global knowledge; no automatic cross-app /
cross-Android-version / cross-scenario reuse.

### D3 — Knowledge records reuse graduated vocabulary with a validation-side lifecycle

Each record: `{KnowledgeType, SemanticAnchor, SourceRunId, EvidenceRefs, ObservedRole,
Scope, Disposition, Confidence, ValidityAssumption, Version, Status, Supersedes/SupersededBy}`.
KnowledgeType is restricted to the seven graduated-vocabulary types. Status
(`ACTIVE/STALE/CONTRADICTED/SUPERSEDED/INVALIDATED`) is a validation-asset lifecycle only —
it must not become a formal Memory contract. Admission requires provenance
(record → SourceRunId → EvidenceRefs → observed result); prohibited sources: guesswork,
hardcoded UI text as truth, coordinates, fixed paths, selector scripts, probing-by-click,
assumptions about runtime internals. Conflicts resolve CURRENT FRESH EVIDENCE FIRST — the
fixture never overrides live reality; stale/contradicted records must be downgraded,
superseded, or invalidated, never force-applied.

### D4 — Safety learning without dangerous trial-and-error

`UNPROVEN_SAFE → RECORD_ONLY / FAIL_CLOSED` is the default posture. The dangerous classes
(factory reset, data deletion, account mutation, security configuration, developer/system
dangerous controls, install/uninstall, payment/authentication, critical network mutation,
and destructive/state-mutating effects generally) are identified ONLY through observational
evidence / typed semantics / boundary evidence. `KnownPotentiallyStateMutating` knowledge
must shape the next strategy via existing levers (`StrategyConstraintSet.prohibitedEffects`,
graduated dispatch-policy) — never via exploratory execution. Acceptance asserts the
dangerous-dispatch intersection is empty across all runs.

### D5 — PlanDelta is a mandatory, evidenced, contract-legal artifact

Every round produces `{PreviousPlan, ObservedResult, LoadedKnowledge, NewKnowledge,
RemainingUnknowns, PlanDelta, NextStrategy}`. PlanDelta cites EvidenceRefs/KnowledgeRefs,
explains the change, and lands strictly inside existing directive freedom (depth,
constraints, prohibited effects, dispatch policy, objective, typed criterion, scope,
completion). No PlanDelta ⇒ `NO_OP_WITH_REASON` or loop termination. Forbidden deltas:
UI action sequences, coordinates, selector paths, fixed navigation paths, mid-run
instructions.

### D6 — SettingsStrategyBinding adapts, never invents

A harness-local binding presents the production `SettingsSemanticCapability` to the
graduated `IStrategySemanticCapabilityBinding` surface. It injects no fixture knowledge into
the runtime, adds no semantic meanings, no fixed page paths, no click sequences, no
selectors, no coordinates, no test-truth. The runtime stays fresh-execution every run; the
fixture only shapes what the upper agent writes into the next directive.

### D7 — Reuse the Phase 2.5 harness unchanged

Per-run composition is the graduated Phase 2.5 chain (Driver → Collector → Verifier →
Gates → Scenario Acceptance, wire-closed surface). New tooling adds only: iterative
campaign runner, knowledge fixture store/versioning, PlanDelta recorder, and the settings
binding. Boundary proofs stay derived-only.

## Authority proof

| Forbidden edge | Why impossible | Guard/proof |
|---|---|---|
| Harness → Runtime mutation / wire addition | Consumes frozen five-method surface only; zero runtime edits | Phase 2.5 source-shape guards + frozen-file SHA checks, extended to new files |
| Fixture → Runtime truth / action authority | Fixture feeds only upper-agent planning; runtime receives only StrategyDirective; fresh observation/grounding/authorization per run | Invariant assertions per run + boundary verifier + knowledge-injection source scan |
| Dangerous learning by execution | UNPROVEN_SAFE default + observational-only admission | Empty dangerous-dispatch-intersection assertion across all runs |
| Fixture as formal Memory | Validation asset lifecycle only; no service/db/API | Spec freezes the non-claim; guard on no Memory-service surface in harness |
| Planner/UniAgent implementation | Upper agent only interprets evidence and authors directives within frozen freedom | PlanDelta contract-legal assertion; directive validator reuse |
| Old knowledge overriding fresh evidence | Current-fresh-evidence-first conflict rule | Contradiction/stale/supersession acceptance tests |

## Stop-condition evaluation

Design requires no Runtime API, wire, contract, authority, Memory, dynamic depth, or
mid-run change. Proceed to OpenSpec creation (authorized 2026-08-26). Any stage hitting a
Runtime-owner FDP stops with `STOPPED_AT_RUNTIME_OR_CONTRACT_GAP` + FDP evidence and
returns to Human Gate. Implementation is NOT authorized by this artifact set.

## Risks / Trade-offs

- [Fixture drifts toward de-facto Memory] → spec freezes non-claims; validation-asset
  lifecycle only; Phase 3 requirements are derived from observed behavior, not the draft.
- [Knowledge stale across emulator images/versions] → mandatory scope metadata + explicit
  non-reuse boundaries + stale semantics.
- [PlanDelta theater (deltas without causal effect)] → acceptance requires evidence-linked
  behavioral deltas (record-only exclusions, boundary exclusions, dispatch-surface changes),
  not click-count reductions.
- [Real Settings nondeterminism] → scenario acceptance stays external + independent; runtime
  failure paths are bounded fail-closed by the graduated contract.
- [Campaign cost] → staged serial execution with per-stage acceptance; 2.6B only after the
  2.6A gate.

## Design Docs

| Concern | Doc |
|---|---|
| Gap analysis (validation-only conclusion, FDP) | `docs/decisions/runtime-full-traversal-acceptance-analysis.md` |
| Iterative-planning gate design (loop/knowledge/safety) | `docs/decisions/runtime-simulator-iterative-planning-gate-design.md` |
| Normative behavior | `specs/runtime-iterative-full-traversal-acceptance/spec.md` |
| Implementation steps | `tasks.md` |
| Prior harness authority | archived `2026-08-26-uniagent-emulator-validation-harness/` |
| Strategy Contract authority | archived `2026-08-26-uniagent-runtimeagent-strategy-contract` predecessors + main specs |
