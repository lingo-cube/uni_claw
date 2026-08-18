# Phase 2 Trap & Recovery — Graduation Decision

> Status: GRADUATED (independent review) | Decision: `GRADUATE_PHASE2_TRAP_RECOVERY` | Date: 2026-08-16
> Gate: `PROJECT_LEADER_PHASE2_TRAP_RECOVERY_GRADUATION_REVIEW`
> MODE: `INDEPENDENT_TRAP_RECOVERY_GRADUATION_REVIEW` (no production/test/spec mutation during review)
> Maturity: `PHASE2_DETERMINISTIC_TRAP_RECOVERY_BASELINE_GRADUATED`
> Change: `openspec/changes/phase2-trap-recovery/`

## 0. Scope Discipline (§24)

This graduation proves ONLY the Phase 2 baseline: deterministic Trap model + Agent-scope
Recovery + Step-scope retry + Recovery verification gate, on the Fake Environment.
It does NOT prove Phase 3 capabilities (popup-local recovery, uncertain-action
refinement, bounded viewport exploration, sibling branch progress, recovery-progress
resume, cross-page discovery) — those are later capabilities with their own lifecycle
records (Phase 3 aggregate archive, closed 2026-08-16). S0 aggregate graduation does
NOT cover Phase 2 and is not used as evidence here.

## 1. Original Phase2 Buyer

| Field | Value |
|---|---|
| Change ID | `phase2-trap-recovery` |
| Buyer | Charter §60-E Recovery WiFi Scenario; Charter §39 Phase 2 (Trap & Recovery) |
| Root | `phase1-deterministic-runtime` (GRADUATED + archived 2026-08-16) |
| Pressure | I-8 escalate-only half; I-9 full loop missing; RecoveryAnchor data without consumer; no drift detection; no step retry |
| Scope | Trap model + Agent-scope Recovery + Step-scope retry + Recovery verification (3 scenarios) |

## 2. Task Truth (§3)

Actual repository count: **32/32** — all tasks checked (A1–A7, B1–B6, C1–C8, D1–D3, E1–E8 = 32).
The gate header reported 33/33; the repository truth is 32 task lines, all complete
(same class of reported-count discrepancy as Phase1's 22 vs 21; repository is canonical,
gate header was not). TaskTruth = PASS; no unchecked task exists.

## 3. Normative Requirement Matrix (§4, all FULL)

| Requirement (spec) | ImplementationEvidence | TestEvidence | DecisionEvidence | AuthorityOwner | Coverage |
|---|---|---|---|---|---|
| Trap Detection Boundary | `Model/Trap.cs` (7 fields: Kind/Scope/Source/Expected/Observed/Evidence/LastAction?), `TrapKind.cs`, `TrapScope.cs`; `Agent.EmitDriftTrap` | AgentRecoveryLauncherDriftTests | phase2-human-gate HG-1/HG-2/HG-3 | Agent (sole emitter) | **FULL** |
| Agent-scope Recovery | `Agent/Agent.Recovery.cs` (RecoverFromDriftAsync: Begin → recipe actions → Observe → Verify → Reconcile/Rebind → position-restore → Resume); `Recovery/Recovery.cs` | AgentRecoveryLauncherDriftTests | HG-4 Option B; phase2-freeze Recovery Ownership | Agent (decision) / Recovery (mechanism) | **FULL** |
| Step-scope Retry | `Traversal/Traversal.cs` (Select retry: re-observe + re-resolve, maxRetries, zero dispatch) | StepRetryScenarioTests | HG-5 minimal scope; phase2-freeze Traversal Retry Boundary | Traversal (step scope, bounded) | **FULL** |
| Recovery Verification | `Recovery.Verify` (injected VerificationCriteria → Verified | Failed(Reason: 期望 vs 实际)); `Model/RecoveryResult.cs` (Verified | Failed) | RecoveryVerificationFailureTests | Agent consumes result; component checks criteria | **FULL** |

## 4. Trap Contract & Ownership (§6-8)

- **TrapContract**: `Trap(TrapKind, TrapScope, long? Expected, long? Observed, string Source, string Evidence, DeviceAction? LastAction)` — exactly 7 fields (HG-2 frozen; NO Recoverability/Confidence/Severity/Timestamp/HistoricalMemoryFields). Pure immutable value; no behavior methods, no RunState, no recovery logic. `Expected`/`Observed` are observation-sequence references (`long?`), NOT Observation snapshots (I-13 God Context prevention).
- **TrapIsEvidence = YES**: describes what went wrong (Kind/Source/Evidence), what was expected (Expected seq), what was observed (Observed seq).
- **TrapDecisionAuthority = NONE**: Trap does not choose a semantic action, change the goal, authorize recovery completion, or fabricate world truth.
- **Trap vs exception (§8)**: failure semantics relevant to recovery are structured fields (Kind/Scope/Expected/Observed/Evidence), not an opaque exception wrapper. `TraversalStepResult.Failed` remains parallel; not every exception becomes a Trap.
- **TrapOwnership (§7)**: Agent emits (sole emitter, Agent-scope only in Phase 2; Step/Container enum members reserved, unused), Agent stores the active Trap (`_lastTrap`), Agent consumes it (drift decision), Agent clears it (next emission overwrites). Trap flows as immutable value across owner boundaries (I-2). Explainable: YES.

## 5. Recovery Component & Boundaries (§9-11, §32)

- **RecoveryOwner = INDEPENDENT_RECOVERY_COMPONENT** (HG-4 Option B): `Recovery/Recovery.cs` owns mechanism state (recipe action list `_recipeActions`, dispatch cursor `_recipeIndex`); Agent owns all decision authority (when to recover, where to restore, verify pass/fail consumption, resume, terminate).
- **RecoveryReverseAuthorityDependency = NONE**: Guard 7 (mechanical) forbids `UniClaw.Runtime.Container` / `UniClaw.Runtime.Traversal` namespaces in Recovery/; Recovery/ references only IEnvironment + Model + BCL. Dependency direction: Agent → Recovery → Environment (I-1).
- **RecoveryRequestBoundary**: no RecoveryRequest/Planner/Runtime types exist (HG-5 minimal scope); `RecoveryRequest` banned repo-wide by Guard 5b. RecoveryResult = exactly 2 variants (Verified | Failed(Reason)) — carries no future Agent decisions, no semantic Plan, no GoalEvidence, no caller-forced success. RecoveryRequestAuthority = NONE (type absent).
- **RecoveryStateMachineSemanticAuthority = NONE**: no FSM introduced (I-7 — protocol expressed as ordinary methods); mechanism state is bounded internal state, never a competing semantic authority.
- **Zero cognition (§33)**: Recovery/ + Trap models contain zero LLM/VLM/DSH/HttpClient references. LlmCalls=0, VlmCalls=0, DshCalls=0.

## 6. RecoveryAnchor Semantics (§12)

`RecoveryAnchor` (ApplicationIdentity / ExpectedSemanticEntry / VerificationCriteria, + RestoreRecipe / EntryStrategy consumed in Phase 2 — 裁决 8 released) is a **reference** to a previously accepted recoverable position — NOT a claim that the world is currently restored, the goal is satisfied, or future observations are guaranteed. RecoveryAnchorIsReferenceNotReality = YES.

## 7. Recovery Flow (§13-17)

Actual flow (implementation names):

```text
IsAgentScopeDrift(postObservation, container, belief)   [Agent, pure function — HG-3 no DriftStatus field]
  → EmitDriftTrap (TrapKind=UnexpectedPage, TrapScope=Agent, Expected/Observed seq refs)
  → RecoverFromDriftAsync [Agent]:
      _recovery.Begin(_recoveryAnchor)             [component: parse RestoreRecipe → action list]
      while HasRemainingActions: ExecuteNextAsync   [component → IEnvironment; dispatch ≠ success — 裁决 10]
      ObserveAsync                                  [component → IEnvironment; fresh post-recovery observation]
      _recovery.Verify(recoveryObs, VerificationCriteria)   [component checks criteria]
        ├─ Failed(Reason) → Agent Fail(runId, reason)      [Run Failed; NO Resume]
        └─ Verified → Agent: Reconcile.FromObservation → Rebind container → position-restore
                        → Resume plan from suspended index → continue with per-step evidence evaluation
```

- **RestoreDispatchEqualsRecoverySuccess = NO**: recipe action dispatch is not recovery success; fresh observation + VerificationCriteria gate is mandatory (I-9; 裁决 10 extends to recovery).
- **RecoveryFreshVerification = PASS**: verification consumes the post-recovery fresh observation only; no stale pre-recovery state, no expected-state substitution. Failure reason explicitly carries 期望 (VerificationCriteria text) vs 实际 (observed Foreground/page/seq).
- **RecoveryVerificationStealsDecisionAuthority = NO**: `Recovery.Verify` only reports Verified | Failed(Reason); it does not decide resume/retry/terminate/target change.
- **AgentSolePostRecoveryDecisionAuthority = YES**: after Verified, all control flow (Reconcile, rebind, position-restore, resume, completion) executes inside Agent; no direct Recovery → Traversal.Execute / Recovery → DeviceAction (unmediated) / Recovery → Completed path.

## 8. Container / WorldTruth / GoalEvidence (§18-20)

- **RecoveryFabricatesContainerTruth = NO**: Container remains sole owner of its local state; recovery rebinds via `Container.Bind(observation)` called by Agent; Recovery never writes Container state/ObjectBinding/StateBelief/page truth.
- **RecoveryState ≠ WorldTruth**: `_belief = Reconcile.FromObservation(recoveryObs, ...)` — belief rebuilt from fresh evidence; Recovery component holds no world truth.
- **RecoveryResult ≠ GoalEvidence**: Completed is reached only via `goal.EvidenceEvaluator(postObservation)` producing satisfied GoalEvidence (I-10), exactly as Phase 1; RecoveryResult.Verified never writes satisfaction evidence, marks goal complete, or reinterprets the goal. GoalEvidenceAuthorityRegression = NONE.

## 9. Failure Classification & Boundedness (§21-23)

- **FailureClassificationTruthful = PASS**: Phase 2 distinguishes — step failure (`TraversalStepResult.Failed` → Phase 1 path, no Trap), step uncertainty (deferred to Phase 3 Uncertain Action), recovery-needed (Agent-scope drift → Trap), goal failed (evidence unsatisfied → Failed). Not collapsed into one generic retry loop.
- **RecoveryBounded = YES**: single recovery attempt (no retry, no recovery strategy — HG-2 boundary); post-recovery drift emits a new Trap and fails explicitly ("恢复后再次 Agent-scope drift…") — no recursive recovery, no infinite fail→recover loop.
- **Recovery progress state (§23)**: mechanism state (recipe list/cursor) owned by Recovery component with observable transition reason (Trace RecoveryId + Reason) and bounded lifecycle (one session per recovery); suspended Plan index/Container owned by Agent.

## 10. Phase1 Invariant Preservation (§5, §30)

Phase2 regression subset: SC-P1-001..005 all PASS (35/35 combined run incl. guards).
- Runtime belief ≠ World truth: preserved (fresh observation bridge).
- Plan ≠ Reality: preserved (recovery does not treat recipe as truth; resume re-evaluates evidence).
- Grounding ≠ semantic identity authority: preserved (no coordinate/hierarchy; Guard 6 unchanged).
- Escalation ≠ decision authority: preserved (Trap is evidence; Agent decides).
- Traversal ≠ Agent: preserved (Traversal owns step mechanics incl. bounded retry; Agent owns decision).
- Environment remains external-world boundary: preserved (recovery actions + observations via IEnvironment).
- Normal path: Phase 1 path does not acquire recovery behavior (Phase2Recovery activates only under drift; NormalWifiHappyPath etc. pass unmodified). NormalPathRecoveryInterference = NONE.
- Phase1InvariantRegression = NONE.

## 11. Human Gate / Freeze / Receipt (§25-27)

- phase2-human-gate-decision.md (Approved 2026-08-08): HG-1 Guard 5 narrowed to Trap only in Model/+Recovery/; HG-2 Trap = 7 fields; HG-3 no DriftStatus field; HG-4 Option B independent Recovery component; HG-5 minimal Recovery scope, no Request/Planner/Runtime. **Implementation matches all five (HumanGateImplementationAlignment = PASS)**.
- phase2-freeze.md (Frozen): Trap model, Recovery ownership (HG-4), Recovery boundary (HG-1/HG-5), Agent authority, Traversal retry boundary, Environment contract, deferred items confirmed absent. Used as historical implementation evidence; FROZEN ≠ GRADUATED.
- phase2-architecture-receipt.md: Frozen Ownership/Authority tables still true in current code (verified against `Recovery/Recovery.cs` and `Agent/Agent.Recovery.cs`). ArchitectureReceiptStillTrue = YES.
- Independent graduation record: NONE existed before this review — this document IS the independent graduation decision.

## 12. Scenario Acceptance (§28-29)

| ScenarioId | Purpose | Failure pressure | Expected Trap | Recovery behavior | Agent decision boundary | Result |
|---|---|---|---|---|---|---|
| SC-P2-001 Agent Recovery: Launcher Drift | Drift → Trap → restore → verify → resume | Foreground leaves baseline + container not still-mine + page unresolvable | Trap(UnexpectedPage, Agent, expected/observed seq refs) | Begin → recipe actions → Observe → Verify → Reconcile → Rebind → position-restore → Resume | Agent decides recovery initiation, resume, completion | **PASS** (3 tests) |
| SC-P2-002 Step-scope Retry | Select failure → bounded re-observe/re-resolve → continue | Flickering target during Step-2 | NONE (retry never produces Trap) | NONE (step scope; exhaustion → Phase 1 Failed path) | Traversal bounded retry; exhaustion escalates to Agent | **PASS** (2 tests) |
| SC-P2-003 Recovery Verification Failure | Verify gate negative | Unrecoverable world (restore cannot establish truth) | Trap emitted at drift | Recipe executed, Verify → Failed(Reason 期望 vs 实际) | Agent fails Run explicitly; no Resume, no fabricated success | **PASS** (2 tests) |

NegativeRecoveryCoverage = PASS (SC-P2-003 + post-recovery drift explicit failure in code).

## 13. Trace Authority (§31)

TraceEvent carries TrapKind?/TrapScope?/RecoveryId? (A4) as observational records — what Runtime did/observed (action payloads, seq refs, reasons) — never assertions about what the external world must be. TraceAuthority = OBSERVATIONAL.

## 14. Later Extensions (§35)

Phase3 CAND-005/009 extended `Agent.Recovery` with opt-in retained-branch revalidation after verified recovery; the code explicitly preserves the frozen SC-P2 position-restore path ("Existing SC-P2 path" branch). No later record contradicts Phase2 baseline. LaterExtensions = EXTENDED_COMPATIBLY; LaterContradiction = NONE (closed).

## 15. Fresh Targeted Validation (§36)

- Phase2 scenario tests (SC-P2-001/002/003) + Phase1 regression subset (SC-P1-001, SC-P1-004) + ArchitectureGuardTests: **35/35 PASS** (2026-08-16 current build; includes Guard 7 recovery-dependency boundary and Guard 5b RecoveryRequest ban).
- `openspec validate phase2-trap-recovery --strict`: PASS.
- `scripts/check-consistency.sh`: ALL PASS (C1–C10).
- CorePhase2AcceptanceRequiresPhysicalDevice = NO (all Fake/ScriptedEnvironment: launcher-drift / flicker-target / unrecoverable variants).

## 16. OpenSpec Truthfulness (§37)

32/32 tasks (reported 33/33 is a count discrepancy, repository canonical); normative spec fully implemented; no unsupported graduation claim (S0 does not cover Phase2); no stale authority statement; not yet archived at review time. OpenSpecTruthfulness = PASS.

## 17. Compliance

- FORBIDDEN list respected: zero production change, zero test change, zero spec mutation, zero repair during review; S0 not used as Phase2 evidence; no archive before independent graduation; no later-capability overclaim.
- Historical human gate / freeze / receipt were evidence; THIS review is the independent graduation decision.

## State

```text
PHASE2_DETERMINISTIC_TRAP_RECOVERY_BASELINE_GRADUATED
```

Next lifecycle action: archive `phase2-trap-recovery` → `PROJECT_LEADER_U2_OPEN_WORLD_SETTINGS_GRADUATION_REVIEW`.
