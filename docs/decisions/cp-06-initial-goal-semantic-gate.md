# CP_06_INITIAL_GOAL_SEMANTIC_GATE

> Generated: 2026-08-09
> Role: Project Leader / Semantic Gate Coordinator — HUMAN_GATE_PREPARATION
> Canonical pressure: CP-06 — Goal satisfaction without unnecessary execution (`GoalSatisfied != PlanExecutionRequired`)
> Prior records: `docs/decisions/cp-06-spec-reconciliation-result.md`, `docs/decisions/cp-06-nonempty-initial-goal-repair-result.md`
> Mode: semantic decision only — no implementation performed; no tests preserved merely because they pass.

---

## Canonical Semantic Question

**Should fresh initial post-Startup GoalEvidence be allowed to establish completion before Plan execution regardless of Plan length?**

The semantic question must NOT be reduced to `EmptyPlan != NonEmptyPlan` unless reality evidence justifies Plan length as semantically relevant.

---

## Experimental Evidence (Recap)

1. Empty Plan: initial GoalEvidence correctly establishes completion (frozen, proven by Assertion6/Assertion7).
2. Non-empty Plan: generalized pre-loop GoalEvidence evaluation was implemented experimentally; the completion decisions it produced for the intended CP-06 scenario were **semantically correct**.
3. The generalization caused 20+ existing test failures, in exactly two categories (below).
4. The generalized production change was reverted; current production retains an empty-plan-only special case (`Agent.cs:212-220`).

---

## Failure Classification

All failing families from the reverted experiment were re-inspected on the current tree. Every Goal fixture across the scenario suites was verified against its initial observation:

| Failing family | Files | Classification |
|---|---|---|
| Probe Goals trivially satisfied at initial observation | `Unit/AgentRecoveryTests.cs` #1 (`EndToEnd_Drift_Trap_Recovery_Resume_Completed`), #6 (`Deterministic_SameInputs_SameTraceAndTrap`) | **GOAL_FIXTURE_SEMANTIC_DEBT** |
| Evidence-count / index / sequence / trace-length shifts | `NormalWifiHappyPathTests`, `CapstoneSettingsFormalProofTests`, `CapstoneSettingsIntegrationRunTests`, `StepRetryScenarioTests`, `SameTextElementDisambiguationTests`, `AgentRecoveryLauncherDriftTests` (formal SC-P2-001), `ViewportExplorationScenarioTests`, `ViewportIdentityContinuityTests`, `RecoveryProgressResumeScenarioTests`, `BoundedCandidateSafetyScenarioTests`, `BoundedCrossPageDiscoveryScenarioTests`, `SiblingBranchProgressScenarioTests`, `DiscoveredBranchEffectRevalidationScenarioTests`, `PopupObstructionRecoveryTests`, `UncertainActionVerificationTests` and other `harness.Evidence` / `ProgressSnapshots` / trace-length consumers | **MECHANICAL_EXPECTATION_DEBT** |
| — | — | **REAL_BEHAVIOR_REGRESSION: NONE** |
| — | — | **UNKNOWN: NONE** |

### GOAL_FIXTURE_SEMANTIC_DEBT — detail

The two `Unit/AgentRecoveryTests` probe runs pass Goal evaluator `obs.ForegroundApplication == BaselineApplication` with reason **`"probe: recovered"`** ([AgentRecoveryTests.cs:47-48](tests/UniClaw.Runtime.Tests/Unit/AgentRecoveryTests.cs#L47-L48), :244-245). The initial post-Startup observation (seq=2) already satisfies it.

- **Actual intended Goal of the scenario:** the probe scenario's semantics is the *recovery control-flow exercise* — drift → Trap → recovery (observe/verify/rebind) → resume → completed. The intended completion condition is "the probe step produced its effect after recovery", not "we are on the baseline app".
- **GoalEvidence role:** **recovery sentinel / test convenience**, NOT a task completion condition. The reason string itself (`"probe: recovered"`) names the non-goal role.
- **Fix direction:** the fixture Goal must express the scenario's actual completion (e.g., "ProbeTarget element absent" with the final script template `Template(BaselineApplication)`), so it is genuinely unsatisfied at seq=2 and the drift/recovery path is exercised. This is fixture repair, not production semantics.

Latent note: `Unit/TrapEmissionTests.cs:144` uses an always-true probe Goal (`"probe: satisfied"`). It does not fail under Option A (its assertions tolerate early completion), but it is the same fixture-debt pattern and should be tightened opportunistically in the repair.

### MECHANICAL_EXPECTATION_DEBT — verification

Every non-probe Goal fixture was checked against its initial observation:

- `ScenarioGoals.EvaluateWifiSwitchEvidence` (happy / switch-stuck / same-text / missing-target / launcher-drift / flicker-target / unrecoverable / initial-goal-satisfied / Capstone): initial observation (SettingsMain) contains **no switch element** → `Satisfied=false` → no behavior change; only evidence arrays shift (+1).
- `ScenarioGoals.ReachNetworkSettings` (uncertain-action-effect-applied / absent): initial observation (`UncertainSettingsMain`) contains only `"Network & Internet"` → **no "WiFi" text** → `Satisfied=false` → no behavior change.
- Sequence-anchored evaluators (ViewportIdentityContinuity seq=4/5, ViewportExploration "End of list", PopupObstruction seq=4, SiblingBranchProgress `IsSubtreeComplete`, Capstone harness): all anchored to late-sequence evidence → unsatisfied at seq=2 → mechanical only.

The mechanical shift is precisely: the Goal evaluator is now also invoked on the initial post-Startup observation (seq=2), so evaluator-side capture arrays (`harness.Evidence`, `GoalEvidence`, `ProgressSnapshots`, trace lengths, `Captured[i]` indices, sequence expectations `{3,4,5}→{2,3,4,5}`) shift by one. No assertion encodes a semantic truth that changes.

---

## Plan-Length Authority Analysis

**Does Plan length have legitimate authority to suppress otherwise admissible GoalEvidence?**

**NO.**

Reasoning against frozen principles:

| Principle | Implication |
|---|---|
| External world authoritative | The initial observation is fresh evidence of the world. The world says WiFi is ON. Plan length does not alter what the world says. |
| Plan != reality | A Plan is an execution hypothesis. Its step count describes the hypothesis, not the world. A hypothesis cannot suppress evidence about the state it hypothesizes. |
| Action dispatch != world result | Executing Plan steps is not required to establish completion; only GoalEvidence is. An already-satisfied Goal makes dispatch unnecessary by definition. |
| Completion requires Goal Evidence | The requirement is met: fresh admissible GoalEvidence exists (post-Startup, seq=2). |
| Agent owns final completion | The Agent decides from admissible evidence; the decision is the same regardless of how many steps the Plan happens to hold. |
| Goal expresses desired world condition | The Goal condition is a world predicate. It is evaluable from the initial observation. Its truth does not depend on the number of steps of any Plan. |

CP-06's fail oracle is plan-length-independent: "System navigates to Wi‑Fi page, toggles switch (turning it OFF), reports 'goal achieved' because prescribed steps were executed. World now contradicts goal." Under an empty-plan-only rule, a non-empty valid WiFi plan against an already-ON world reproduces exactly this fail oracle — the plan-length qualifier does not prevent it, it *causes* it.

The empty-plan special case was the minimum bounded repair for the Step-6 SPECIFICATION_GAP; the plan-length qualifier was a bounded-repair artifact, never a semantic principle. No reality evidence justifies treating step count as semantically relevant to GoalEvidence authority.

---

## Scenario Analysis

| # | Goal | Initial world | Plan | Expected completion |
|---|---|---|---|---|
| A | WiFi == ON | WiFi ON | empty | **Completed from initial evidence, 0 dispatches.** Proven (Assertion6). Unchanged under Option A. |
| B | WiFi == ON | WiFi ON | non-empty valid WiFi execution plan | **Option A: Completed from initial evidence, 0 dispatches** (zero unnecessary mutation; CP-06 pass oracle). Option B: ≥1 step dispatched — toggling OFF then ON risks the CP-06 fail oracle. Plan length has no world-truth bearing. |
| C | WiFi == ON | WiFi OFF | non-empty | **Normal execution begins.** Initial evaluation returns `Satisfied=false` → the run proceeds through the Plan identically to today. No behavior change under Option A. |
| D | `ForegroundApplication == BaselineApplication` (recovery probe) | already satisfies it at seq=2 | non-empty probe plan | **The fixture Goal is NOT the genuine task Goal** — it is a recovery sentinel (`"probe: recovered"`). Classified as GOAL_FIXTURE_SEMANTIC_DEBT, not evidence against initial Goal evaluation. The scenario's intended semantics (drift → trap → recovery → resume) must be expressed by an honest Goal. |

---

## Semantic Delta Classification

**SEMANTIC_CORRECTION_WITHIN_EXISTING_CP06**

- The canonical pressure (portfolio) states the requirement without plan-length qualifier: "When the external world already satisfies a goal, the system must recognize satisfaction without executing unnecessary actions."
- Generalization evaluates the **same evaluator** on the **same fresh observation** the Runtime already holds (the initial post-Startup observation), using the **same mechanism** as existing post-action evaluation (`Agent.cs:401`).
- No new types, fields, enums, or authority. Agent still owns completion; world still authoritative; GoalEvidence remains the sole completion source; all 14 invariants untouched.
- Not NEW_SEMANTIC_CAPABILITY (mechanism exists; only the scope of admissible observations widens). Not ARCHITECTURE_CHANGE (no ownership/authority change). Classification does not follow from test-failure count.

---

## Recommendation

**AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL** (Option A)

Fresh admissible initial post-Startup world evidence may establish Goal completion before execution regardless of whether the Plan has zero or N steps. Plan existence does not itself create an obligation to act.

This is a recommendation for HUMAN decision. **Not self-authorized.**

---

## Proposed Bounded Repair Scope (NOT executed — for approval)

1. **Generalize** the pre-loop GoalEvidence evaluation in `Agent.cs`: evaluate `goal.EvidenceEvaluator(initialObservation)` for ALL plans (drop the `Steps.Length == 0` condition); if Satisfied → `Complete(runId, evidence)` with zero dispatches. The empty-plan special case becomes redundant and is removed; the empty-plan + unsatisfied path falls through to the existing exhaustion-Failed path (Assertion7 preserved).
2. **Repair Goal fixtures that misuse completion evidence**: `Unit/AgentRecoveryTests.cs` #1/#6 — express the probe scenario's actual completion (e.g., "ProbeTarget absent", final script template `Template(BaselineApplication)`), so the Goal is genuinely unsatisfied at seq=2 and drift/trap/recovery/resume is exercised. Opportunistically tighten the always-true probe Goal in `Unit/TrapEmissionTests.cs:144`.
3. **Update mechanical assertions** caused by the valid earlier completion: adjust evidence counts, array indices, sequence-number expectations, and trace lengths (+1 initial evaluation) across the mechanical-debt families listed above.
4. **Preserve** scenarios whose actual Goals are not initially satisfied (verified: all WiFi-switch, ReachNetworkSettings, sequence-anchored, and Capstone evaluators).
5. **Add positive non-empty-plan proof**: "initial-goal-satisfied" variant + non-empty `WifiEnableSequence` plan → Completed with 0 Plan-step dispatches, evidence from seq=2.
6. **Add negative control**: happy variant (world OFF, non-empty plan) → initial evidence unsatisfied → full normal execution (3 dispatches, Completed from post-action evidence). Assertion7 (empty + unsatisfied → Failed) remains.
7. **Full validation**: full test suite, architecture guards 8/8, build 0 warnings 0 errors, deterministic replay conventions.
8. **No ownership/authority changes.**

---

## Human Gate

**HUMAN_DECISION_REQUIRED**

Decision options: `AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL` | `RETAIN_EMPTY_PLAN_ONLY_SEMANTICS` | `INSUFFICIENT_EVIDENCE`.

Note for the human: `RETAIN_EMPTY_PLAN_ONLY_SEMANTICS` would require explicit reality/semantic justification for why Plan length changes GoalEvidence authority — none exists in the evidence corpus; the CP-06 fail oracle is plan-length-independent.

---

## Repository Changes

`docs/decisions/cp-06-initial-goal-semantic-gate.md` — created (only file changed; no Runtime or test modifications).
