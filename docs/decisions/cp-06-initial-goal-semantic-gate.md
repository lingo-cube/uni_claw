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

## Human Decision (2026-08-09)

**`HUMAN_AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL` — APPROVED.**

Fresh admissible initial post-Startup GoalEvidence may establish completion before Plan execution regardless of Plan length. Plan existence does not create an obligation to act. The empty-plan-only behavior is recognized as a bounded implementation artifact, not a semantic principle.

- Approved classification: `SEMANTIC_CORRECTION_WITHIN_EXISTING_CP06`.
- Approved bounded repair scope: generalize initial GoalEvidence evaluation across empty and non-empty Plans; remove empty-plan-only special casing where redundant; repair Goal fixtures that misuse GoalEvidence as probe/recovery sentinel; update mechanical evidence/index/count expectations caused by valid earlier evaluation; add non-empty initially-satisfied positive proof with zero Plan-step dispatch; add non-empty initially-unsatisfied negative control; preserve all existing architecture ownership/authority; full validation required.
- Explicitly NOT authorized: new Goal model; new completion authority; new Planner; new mutable state; ownership change; authority change; architecture change; Intent → Goal synthesis.

## Repository Reconciliation (2026-08-09)

Working tree CLEAN. HEAD = `791cdef` "feat: CP-06 initial goal semantic gate — empty-plan satisfied completion" (commits the full evidence chain, the empty-plan production change `Agent.cs +10`, Assertion6/7, and this gate record). No `REALITY_MODEL_ADMISSION_CONTRACT` artifacts exist in this repository at reconciliation time; if that task runs in another session, the repair waits for its completion per the Human Gate.

## Approved Execution Order (recommended — NOT executed)

Precondition: the currently running `REALITY_MODEL_ADMISSION_CONTRACT` task completes; repository reconciled clean.

1. **Production generalization (1 file)** — `src/UniClaw.Runtime/Agent/Agent.cs`: remove the `Steps.Length == 0` condition; evaluate `goal.EvidenceEvaluator(initialObservation)` for all Plans; `Complete(runId, evidence)` when Satisfied. Empty-plan + unsatisfied falls through to the existing exhaustion-Failed path (Assertion7 preserved).
2. **Goal fixture repairs (2 tests)** — `tests/UniClaw.Runtime.Tests/Unit/AgentRecoveryTests.cs` #1 and #6: honest probe Goal ("ProbeTarget absent", final template `Template(BaselineApplication)`). Opportunistic tightening of the always-true probe Goal at `Unit/TrapEmissionTests.cs:144`.
3. **Mechanical assertion updates (~15 files)** — evidence counts, array indices, sequence expectations, trace lengths (+1 initial evaluation) across the mechanical-debt families.
4. **New proofs** — `GoalEvidenceCompletionTests` Assertion8 (non-empty plan + initially-satisfied → Completed, 0 Plan-step dispatches, evidence seq=2) and Assertion9 (non-empty plan + initially-unsatisfied → normal execution, 3 dispatches, Completed from post-action evidence).
5. **Full validation** — full test suite, architecture guards 8/8, build 0 warnings 0 errors, deterministic replay conventions.

No step proceeds while the previous step leaves failures unclassified; no ownership/authority change at any step.

---

## Repository Changes

`docs/decisions/cp-06-initial-goal-semantic-gate.md` — created (only file changed; no Runtime or test modifications).

---

## CP-06 Execution Result (2026-08-09)

**CP_06_FULLY_CLOSED.**

Canonical pressure: Goal satisfaction without unnecessary execution (`GoalSatisfied != PlanExecutionRequired`).

Plan-length-independent initial GoalEvidence: **PROVEN** — both branches:

- **A.** empty Plan + initially satisfied Goal → honest zero-dispatch completion (Assertion6/Assertion7).
- **B.** non-empty Plan + initially satisfied Goal → honest zero-Plan-step-dispatch completion (Assertion8).

Negative controls (initially unsatisfied + non-empty Plan → normal execution; unsatisfied + empty Plan → Failed) confirmed (Assertion7/Assertion9).

Empty-plan special case: **REMOVED / NO LONGER SEMANTICALLY SPECIAL.** The unconditional initial GoalEvidence evaluation in `Agent.cs` treats all Plans identically; Plan existence does not create an obligation to act.

Production change: 1 file (`Agent.cs`).
Goal fixture repairs: 3 tests (AgentRecoveryTests #1/#6, TrapEmissionTests:144).
Mechanical assertion reconciliation: ~17 files, 37→0 failures, 0 regressions.
New canonical proofs: Assertion8 (non-empty+initially-satisfied, 0 dispatch), Assertion9 (negative control).
Full suite: 415 pass, 0 fail. Architecture guards: 8/8. Build: 0 warnings, 0 errors. Deterministic replay: 22/22.
Delta audit: semantic = SEMANTIC_CORRECTION_WITHIN_EXISTING_CP06; all others NONE.
