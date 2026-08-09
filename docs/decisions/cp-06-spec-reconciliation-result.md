# CP_06_SPEC_RECONCILIATION_RESULT

> Generated: 2026-08-09
> Canonical pressure: CP-06 — Goal satisfaction without execution
> Source: `docs/decisions/unified-legacy-scenario-pressure-portfolio.md`

---

## Status: PASS

---

## Normative Gap

The Runtime's GoalEvidence model could express zero-dispatch goal satisfaction (GoalEvidence.Satisfied from any observation), but:

1. No normative SHALL required the system to evaluate GoalEvidence against the initial post-Startup observation before entering the main step loop.
2. No executable test exercised the zero-dispatch completion path.
3. An empty Plan with an already-satisfied Goal would fall through the empty step loop and report `Failed("Plan 步数耗尽...")` instead of `Completed`.

---

## Normative Repair

**Agent.cs** (line ~215, before the main step loop):

```csharp
// ── CP-06：空 Plan 时初始 Observation 可能已满足 Goal；此时无需 dispatch 任何 action 即可完成 ──
if (executionPlan.Steps.Length == 0)
{
    var initialGoalEvidence = goal.EvidenceEvaluator(initialObservation);
    if (initialGoalEvidence.Satisfied)
    {
        return Complete(runId, initialGoalEvidence);
    }
}
```

The repair is conditional on `executionPlan.Steps.Length == 0` because:
- For non-empty plans, the evaluator is always called after each step, and the existing completion path already handles GoalEvidence.Satisfied correctly.
- The SPECIFICATION_GAP was specifically about the empty-plan case where the evaluator was never called.
- This is the minimum change needed to close the gap without disrupting existing behavior.

The semantic model already supports this: `GoalEvidence.Satisfied` can be returned from any observation, and `Complete(runId, evidence)` already handles the completion path correctly (trace event, state transition, reason recording).

---

## Executable Proof

### Positive test (Assertion6):
- **Variant:** `initial-goal-satisfied` — LaunchApp transitions to `WiFiSettingsOn` (Wi‑Fi switch already ON)
- **Plan:** `ScenarioPlans.Empty()` (zero steps)
- **Goal:** `ScenarioGoals.EnableWifi(evidence)` — evaluates Wi‑Fi switch state from observation
- **Assertions:**
  - `RunState.Completed` (not Failed)
  - `GoalEvidence.Satisfied = true`
  - `GoalEvidence.SourceObservationSequence = 2` (initial post-Startup observation)
  - Exactly one `Completed` Trace event with non-empty Reason
  - ActionHistory contains only `LaunchApp` (no Plan-step actions dispatched)

### Negative test (Assertion7):
- **Variant:** `happy` (SettingsMain — no Wi‑Fi switch visible)
- **Plan:** `ScenarioPlans.Empty()` (zero steps)
- **Goal:** `ScenarioGoals.EnableWifi(evidence)` — no switch element → `Satisfied = false`
- **Assertions:**
  - `RunState.Failed` (not Completed)
  - `GoalEvidence.Satisfied = false`
  - No `Completed` Trace event
  - Failed event Reason contains `"Plan 步数耗尽"`

---

## Production Semantic Delta: 0

No new model types. No new fields. No new enums. No new authority. The `goal.EvidenceEvaluator(initialObservation)` call uses the existing evaluator injection point. The `Complete(runId, evidence)` method already existed at line 1338.

---

## Ownership Delta: NONE

Agent already owns the completion decision (I-10). The repair adds one evaluation call before the main loop — same evaluator, same owner.

---

## Authority Delta: NONE

GoalEvidence authority remains with the caller-injected evaluator. Completion authority remains with Agent (I-10).

---

## Validation

| Check | Result |
|---|---|
| Targeted CP-06 tests (Assertion6 + Assertion7) | PASS |
| Full test suite (413/413) | PASS |
| Architecture guards (8/8) | PASS |
| Build (0 warnings, 0 errors) | PASS |

---

## Files Changed

| File | Change |
|---|---|
| `src/UniClaw.Runtime/Agent/Agent.cs` | +8 lines: conditional pre-loop GoalEvidence evaluation |
| `tests/.../GoalEvidenceCompletionTests.cs` | +2 `using` statements + 2 test methods (Assertion6, Assertion7) |
| `tests/.../ScriptedEnvironmentVariants.cs` | +1 variant: `InitialGoalSatisfied()` |
| `tests/.../ScenarioHarness.cs` | +1 variant entry: `"initial-goal-satisfied"` |

---

## Next

**CP_12_TARGET_GROUNDING_CHALLENGE** — the one genuinely new canonical pressure from the unified portfolio requires assessment against current Runtime semantics.

---

## Repository Changes

As listed above. No other modifications.
