# SC-P3-CAND-005 Capability Closeout

> Status: Frozen Capability | Date: 2026-08-08
> Scope: SC-P3-CAND-005 only — this is not a Phase 3 freeze.
> Authority: acceptance receipt for `openspec/changes/phase3-recovery-progress-resume/`; it does not replace the approved Scenario, Spec, Architecture Contract, frozen Phase 2 decisions, or SC-P3-CAND-004 closeout.

## Capability

**Evidence-Validated Progress Resume After Agent Recovery**

Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`

## Proven Behavior

```text
A historically complete in bounded parent P
→ Agent-scope external drift
→ verified Recovery directly restores P
→ Agent evaluates A criterion against fresh recovered-world Observation
→ true: refresh A evidence and continue B without replaying A
→ false: exclude A and fail without fabricated completion or replay
→ null/absent: retain history only and fail explicitly without replay
→ only satisfied GoalEvidence may complete the Run
```

The accepted slice proves:

- The branch criterion is an Agent-owned Plan hypothesis, not proof; it is evaluated only against fresh Observation evidence after verified Recovery.
- A historical completion sequence at or before `Trap.Observed` cannot independently contribute after Recovery.
- `true` refreshes A completion evidence beyond the drift boundary and permits bounded continuation with independently proven B.
- `false` excludes A from current completion while preserving historical Trace and journal provenance.
- `null` or an absent criterion leaves A unresolved, contributes no current completion, and produces an explicit Agent-level non-completion outcome.
- Correct parent identity, Container continuity, and `RecoveryResult.Verified` alone do not validate A.
- A's external-effect action appears exactly once; the A-entry/work/return prefix is not blindly replayed.
- Revalidated A and proven B can support bounded subtree evidence, but only Agent consumption of satisfied `GoalEvidence` sets `RunState.Completed`.
- Equal RunId, world input, Plan criteria, disturbance schedule, and action sequence replay to equal criterion outcome, progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState.

## Production Delta

- Model types: +0.
- Fields: exactly +1 optional immutable `PlanStep.BranchEffectEvidenceEvaluator: Func<Observation, bool?>?` field.
- Enums: +0.
- Interfaces: +0.
- Components: +0.
- New mutable-state owners: +0.
- Behavior: existing Agent Recovery/resume control-flow adjustment only.

## Ownership and Authority

- Ownership delta: **NONE**.
- Authority delta: **NONE**.
- Agent remains the sole owner of Plan criteria, `BranchProgressEvidence`, recovered-world validity interpretation, resume/escalation decisions, GoalEvidence evaluation, and final RunState.
- Recovery remains the owner of restoration actions, fresh observation, and injected position verification only; it does not reference Container or Traversal and does not mutate branch progress.
- Container remains page-local and Traversal remains the deterministic local execution/journal owner.
- Environment reports external Observation and dispatch outcomes only.

## Frozen Boundary

| Criterion outcome | Frozen meaning |
|---|---|
| `true` on strict-fresh recovered-world evidence | Refresh the bounded retained completion claim and continue remaining approved sibling work without replaying the completed prefix. |
| `false` | Exclude the contradicted completion from current progress; preserve historical provenance; no fabricated completion or blind repair. |
| `null` or absent | Treat current validity as unresolved; retained history contributes nothing; explicit Agent-level non-completion; no blind redispatch. |

## Explicitly Not Purchased

- validity, evidence, or Recovery state enum;
- Recovery epoch, freshness field, second criterion field, or persistent validity state;
- generic evidence/predicate framework, EffectRegistry, or action-idempotence taxonomy;
- checkpoint, ResumeToken, snapshot/progress manager, Recovery planner, graph, stack, FSM, or workflow engine;
- Recovery ownership of progress or Recovery dependencies on Container/Traversal;
- more than one bounded parent or Recovery cycle;
- autonomous discovered-candidate safety, Capstone implementation, Harness change, Runtime refactor, or Phase completion.

## Structural Pressure

Agent Recovery/resume control flow remains under observable structural pressure because it now coordinates bounded position restoration and progress revalidation. The accepted slice preserves clear ownership and stays within the approved one-field budget, so this pressure does not authorize a refactor.

## Acceptance Receipt

- OpenSpec: strict validation passed; artifacts complete.
- Tasks: 4/4 complete.
- Independent validation: PASS.
- Build: 0 warnings, 0 errors.
- Tests: 250/250 passed.
- Formal SC-P3-CAND-005 Scenario tests: 8/8 passed.
- Targeted fixture/behavior/formal tests: 19/19 passed.
- Architecture Guards: 8/8 passed.
- Consistency checks: C1–C9 ALL PASS.
- Production delta: exactly one approved optional immutable PlanStep field and existing Agent control-flow adjustment.
- Ownership delta: NONE.
- Authority delta: NONE.
- Semantic drift: NONE.

## State

```text
SC_P3_CAND_005_FROZEN_CAPABILITY
```

This state does **not** mean `PHASE_3_FROZEN`, `S0_GRADUATED`, or `PHASE_COMPLETE`. Capstone, autonomous-safety research, OpenSpec archive, and other Scenario work require separate authority.
