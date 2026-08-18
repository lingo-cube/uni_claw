# Phase 1 Deterministic Runtime — Graduation Decision

> Status: GRADUATED (independent review) | Decision: `GRADUATE_PHASE1_DETERMINISTIC_RUNTIME` | Date: 2026-08-16
> Gate: `PROJECT_LEADER_PHASE1_DETERMINISTIC_RUNTIME_GRADUATION_REVIEW`
> MODE: `INDEPENDENT_DETERMINISTIC_RUNTIME_GRADUATION_REVIEW` (no production/test/spec mutation during review)
> Maturity: `PHASE1_DETERMINISTIC_RUNTIME_BASELINE_GRADUATED`
> Change: `openspec/changes/phase1-deterministic-runtime/`

## 0. Scope Discipline (§25)

This graduation proves ONLY the Phase 1 deterministic Runtime baseline (Normal WiFi
Scenario on a Fake Environment). It does NOT claim: Phase 2 Trap/Recovery, Phase 3
popup/bounded-exploration, open-world inventory, physical-device reality, DSH
cognition, or outer intelligence — those are separate capabilities with their own
lifecycle records.

## 1. Original Buyer

| Field | Value |
|---|---|
| Change ID | `phase1-deterministic-runtime` |
| Buyer | Deterministic Runtime baseline (宪章 §60 第一项工作: A Architecture Proposal + B Minimum Contracts + C Fake Environment + D Normal WiFi Scenario) |
| Root | `greenfield-agent-runtime` (Phase 0) |
| Scope | Vertical Slice — Agent / Container / Traversal / Startup / World / IEnvironment / Model |

## 2. Normative Requirements (specs/*, 4 top-level groups, FULL coverage)

| Requirement (spec) | SHALL count | Implementation | Test | Coverage |
|---|---|---|---|---|
| Run Lifecycle, Startup & World Belief (`run-lifecycle`) | 13 | `RunState` enum; `StartupResult.Ready/NotReady`; `WorldBelief` (SemanticPage/Confidence/Evidence/SourceObservationSequence, no scenario fields — 裁决 2); `RecoveryAnchor`; `GoalEvidence`; `Agent.RunAsync` transitions | B10/B11/B12 (24 tests) | **FULL** |
| Environment 观察与动作边界 (`environment`) | 9 | `IEnvironment` (ObserveAsync/ExecuteAsync + CT); `Observation` (Elements/ForegroundApplication/SequenceNumber); `ObservedElement` (Text/SwitchState?/Index); `DeviceAction` (LaunchApp/Tap/SetSwitch + TargetElementIndex); `ActionResult` (Dispatched/TimedOut/Rejected); `ScriptedEnvironment` | B10/B12/B14 | **FULL** |
| Container 与 Traversal (`container-traversal`) | 11 | `Container` (local state sole owner, still-mine, local-complete, injected step executor); `Traversal` (Select→Check→Execute→Observe→Verify→Branch, journal, `TraversalStepResult`); grounding Text+SwitchState? state-bearing-first | B13/B14 + Guards | **FULL** |
| Normal WiFi Scenario SC-P1-001 (`normal-wifi-scenario`) | 8 | §34 full lifecycle; Goal evaluator injected (no hardcoded strings — 裁决 3/11); SetSwitch semantics (no Toggle — 裁决 1); Trace causal chain | B10 (8 tests, assertions 1-7) | **FULL** |

## 3. SC-P1-001..005 Results (fresh targeted validation, current repository)

| Scenario | Purpose | Key invariant | Result |
|---|---|---|---|
| SC-P1-001 Normal WiFi Happy Path | Deterministic happy path: goal → startup → observe → belief → container/traversal → grounded action → dispatch → verification → GoalEvidence → Completed | §34 lifecycle order; dispatch ≠ completion; Trace causal chain; deterministic replay | **PASS** (8 tests) |
| SC-P1-002 Startup Foreground Verification Failure | Never enter Running when trusted runtime cannot establish | NotReady(explicit reason); RecoveryAnchor not established; no recovery actions; action history = [LaunchApp] only | **PASS** (6 tests) |
| SC-P1-003 Goal Evidence Completion | I-10/§43: Plan exhausted ≠ Completed, Dispatch ≠ Completed; only satisfied GoalEvidence completes | GoalEvidence.SourceObservationSequence == post-action Observation seq; negative switch-stuck variant → Failed not Completed | **PASS** (10 tests) |
| SC-P1-004 Escalation Without Stealing Authority | I-8 escalate half: low-level returns structured result, Agent is final failure authority | TraversalStepResult.Failed(reason) → Container read-only handoff → Agent Failed; no recovery actions; no Trap model in Phase 1 | **PASS** (4 tests) |
| SC-P1-005 Same-text Element Disambiguation | Grounding selects state-bearing element without coordinate/hierarchy | TargetElementIndex == switch Index ≠ title Index; SetSwitch on non-switch → Rejected; no coordinate/hierarchy model | **PASS** (3 tests) |

**Semantic conclusions**: `LOW_LEVEL_ESCALATION_DOES_NOT_STEAL_AUTHORITY` (SC-P1-004);
`GROUNDING_IS_EVIDENCE_NOT_AUTHORITY` (SC-P1-005).

## 4. Authority Ownership (I-2/I-3, single owner per state, single authority per decision)

| State / decision | Owner / authority | Charter |
|---|---|---|
| RunState (global lifecycle) | Agent | §5/§18 |
| WorldBelief | Agent (World/ reconciles, never adjudicates) | §5/§10 |
| Active Container Stack | Agent | §5/§6 |
| Container local state (observation/candidates/visited/progress/completion) | Container | §6 |
| Traversal step state (selected/journal) | Traversal | §7 |
| Simulated world state | ScriptedEnvironment (fake, tests/) | §8/§33 |
| TraceEvent list | Agent | §28 |
| Run termination / completion | Agent (sole authority; GoalEvidence consumer) | §5/I-3/I-10 |
| Element grounding / disambiguation | Traversal.Select (Text+SwitchState? only) | §7/裁决 3 |
| Physical effect application | Environment (by element identity) | §8 |

No God Object: state is dispersed across Agent/Container/Traversal/Environment per
the ownership table (design.md §2). No duplicate authority: each decisive semantic
responsibility has exactly one owner. State ownership is fully explainable for every
meaningful Phase 1 state.

## 5. Core Deterministic Boundaries

- **Runtime State ≠ World Truth (§12)**: every post-action step re-observes
  (`_environment.ObserveAsync`) and reconciles (`Reconcile.FromObservation`);
  Observation/evidence is the bridge; no cached state becomes reality by assertion
  (charter §3: Internal Runtime State ≠ External World State).
- **Plan is not Reality (§13)**: Plan steps are execution hypotheses (注入数据, 裁决 3/11);
  belief updates come only from fresh Observation; Plan exhaustion alone never completes
  (I-10/§43, SC-P1-003 negative variant proves Failed).
- **Grounding boundary (§20)**: `ObservedElement.Index` is observation-local stable
  ordinal + Text/SwitchState? evidence; no coordinate/hierarchy model (Guard 6); index
  is not long-lived SemanticObject identity.
- **Escalation not authority (§8/§19)**: low level detects failure and returns
  `TraversalStepResult.Failed(reason)`; Container hands off read-only; only Agent
  terminates the Run (SC-P1-004; `RunState` sole owner).
- **Environment boundary (§16)**: `IEnvironment` is the sole external-world port;
  Runtime requests actions, Environment reports outcomes; `ActionResult` (Dispatched/
  TimedOut/Rejected) never equals world success or completion (裁决 10); fresh
  observation required where fresh evidence is required.
- **GoalEvidence semantics (§18)**: `GoalEvidence(Satisfied, Reason, SourceObservationSequence)`
  is produced by the injected evaluator from Observation evidence; the evaluator only
  reports evidence — the Completed/Failed decision authority is kernel Runtime side
  (Agent). No caller fabrication; no planned outcome becomes completion.
- **No unnecessary FSM (§14)**: Phase 1 protocol expressed with plain methods
  (design.md §3: "无 FSM (I-7)"); no second decision authority.
- **Container/Traversal boundary (§19)**: Container holds semantic local state and
  answers still-mine/local-complete; Traversal executes step mechanics only — it never
  becomes a semantic decision authority.

## 6. Fake Environment Acceptance (§17)

Core deterministic acceptance runs entirely on `ScriptedEnvironment` (tests/): Screen
configuration driven (Screen A + Click X → Screen B), deterministic and replayable.
- CoreAcceptanceRequiresPhysicalDevice = NO
- CoreAcceptanceRequiresLLM = NO
- CoreAcceptanceRequiresVLM = NO

This does not diminish later physical-reality graduations (separate records exist).

## 7. Historical Validation Provenance (§22, durable and attributable)

- `docs/decisions/phase2-human-gate-decision.md`: "Phase 1 (Deterministic Runtime)
  完成并通过独立验收（104/104 tests, 6/6 guards, 5/5 scenarios, validator PASS）" —
  independent acceptance recorded at the Phase 2 human gate.
- `docs/decisions/phase2-freeze.md`: "Phase 1 invariants frozen: I-1..I-14 全部不变"
  + Phase 2 precondition 158/158 tests, 8/8 guards.
- `docs/decisions/phase2-architecture-receipt.md`: Frozen Ownership/Authority tables
  (Phase 1 rows) — architecture review product.
- `openspec/changes/phase1-deterministic-runtime/scenarios/catalog.md`: Phase acceptance
  evidence (SC-P1-001..005 authoritative contracts).

## 8. Fresh Targeted Validation (§24, current repository truth)

- Targeted SC-P1-001..005 tests + ArchitectureGuardTests: **47/47 PASS** (2026-08-16,
  current build; includes 8/8 architecture guards).
- `openspec validate phase1-deterministic-runtime --strict`: **PASS**.
- `scripts/check-consistency.sh`: **ALL PASS** (C1–C10).
- Later-phase contradiction: **NONE** — Phase 2/3 extended the Runtime compatibly
  (EXTENDED_COMPATIBLY); phase2 gate explicitly preserved SC-P1-001..004 semantics;
  no later record invalidates the Phase 1 baseline.

## 9. Task-Count Correction History (explicit, not hidden)

```text
reported 20/22   (prior reconciliation prose — denominator was a counting error)
→ actual 19/21   (independent recount: 21 task lines; A4 + B15 + C2)
→ reconciled 20/21 (this review: C1 §60-F checklist satisfied by existing durable
                     evidence; C2 remains unchecked)
```

**C2 remained unchecked before graduation** because it is the post-graduation archive
task (`评审通过后归档本 change`). 20/21 is not a graduation blocker: correct lifecycle
order is implementation → validation → independent graduation review → GRADUATED →
archive → C2 fulfilled.

## 10. OpenSpec Truthfulness (§26)

- tasks = 20/21; C2 unchecked (POST_GRADUATION_ARCHIVE_PENDING)
- implementation complete; normative requirements FULLY covered
- no stale unsupported authority claim; no self-graduation (this record is the
  independent graduation authority)
- no spec/production/test mutation during this review

## 11. Compliance

- FORBIDDEN list respected: zero production change, zero test change, zero spec
  mutation, zero repair during review, no later-capability overclaim, no 21/21
  requirement.
- This record is the independent graduation evidence; no separate closeout was
  fabricated.

## State

```text
PHASE1_DETERMINISTIC_RUNTIME_BASELINE_GRADUATED
```

Next lifecycle action: archive `phase1-deterministic-runtime` (C2 fulfilled by the
archive operation) → `PROJECT_LEADER_PHASE2_TRAP_RECOVERY_GRADUATION_REVIEW`.
