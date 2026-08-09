# Leader Decision Contract — H4-1

> Status: Purchased | Version: 1.1 | Date: 2026-08-08
> Scope: stable decision interface only. This contract does not execute, route, or advance work.
> Evidence: the SC-P3-001 chain repeatedly required a human relay between Task/Validation Results and the Project Leader's next prompt; the first SC-P3-002 run proved that a passed Semantic Gate with absent OpenSpec artifacts requires an explicit reconciliation decision before task generation.

## Purpose

H4-1 standardizes one boundary:

```text
Result Contract
→ Leader Decision
→ Next Action
```

It makes the Project Leader's decision explicit and machine-readable while preserving human, semantic, architecture, phase, and routing authority.

## Canonical Shape

```yaml
LEADER_DECISION:
  input:
    previous_result_type: TASK_RESULT | VALIDATION_RESULT | PHASE_CONTROLLER_RESULT | NONE
    repository_state: <repository-backed state summary>
    active_scenario: <SC-Px-xxx | NONE>
    active_phase: <phase identifier>
  decision:
    decision_type: DISPATCH_TASK | RECONCILE_SPEC | GENERATE_TASKS | REQUEST_VALIDATION | SEMANTIC_GATE | ARCHITECTURE_REVIEW | HUMAN_GATE | SCENARIO_SLICE_COMPLETE | PHASE_COMPLETE | STOP
    reason: <why this decision follows from repository truth>
    evidence:
      - <repository path, task state, validation result, or explicit gate evidence>
    next_role: <portable role from .ai/model-routing.yaml | NONE>
    next_task_id: <existing approved task ID | NONE>
    requires_human: true | false
    boundary_constraints:
      - <scope and deferred-boundary constraints>
    expected_result_contract: TASK_RESULT | VALIDATION_RESULT | PHASE_CONTROLLER_RESULT | NONE
```

## Field Rules

| Field | Rule |
|---|---|
| `previous_result_type` | Names the result being interpreted; it does not change that result's status. |
| `repository_state` | Must be freshly loaded from repository truth, not inferred only from conversation. |
| `active_scenario` / `active_phase` | Scope the decision. A slice decision must not be silently promoted to a phase decision. |
| `decision_type` | Must be exactly one value from the minimum set above. |
| `reason` | Must explain the decision without inventing missing repository facts. |
| `evidence` | Must cite repository state or an explicit prior result/gate. |
| `next_role` | Uses a portable role; model/provider selection is forbidden here. |
| `next_task_id` | Required only for `DISPATCH_TASK`; otherwise `NONE` unless a repository convention explicitly requires it. |
| `requires_human` | `true` for a Human Gate and whenever repository policy reserves the decision for a human. |
| `boundary_constraints` | Carries the active Task/Scenario/Phase limits into the next action. |
| `expected_result_contract` | Declares the return interface, not an automatic continuation trigger. |

## Decision Rules

1. A Leader Decision cannot create a Task ID.
2. `DISPATCH_TASK` may reference only a task that exists and is approved in repository truth.
3. A coder cannot bypass `SEMANTIC_GATE`, `ARCHITECTURE_REVIEW`, or `HUMAN_GATE`.
4. `SCENARIO_SLICE_COMPLETE` is not `PHASE_COMPLETE`.
5. Model and provider selection are not decision authority and must not appear as leader choices.
6. Execution routing remains governed by `.ai/model-routing.yaml`.
7. A blocked result remains blocked until the named gate resolves it; the Leader cannot rewrite it as DONE.
8. `PHASE_COMPLETE` requires repository evidence that all approved Phase scope and required independent validation are complete. It is never inferred from one completed slice.
9. If no approved next action exists after applying the `RECONCILE_SPEC` gate below, choose `STOP`; do not invent work.
10. This contract describes a decision only. A human or existing task mechanism still performs any dispatch.
11. `RECONCILE_SPEC` is valid only when a Scenario is approved, its Semantic Gate has passed, and the corresponding normative OpenSpec artifacts are absent, stale, or incomplete.
12. `RECONCILE_SPEC` authorizes only the repository-approved OpenSpec role to align proposal, design, specs, and Scenario artifacts with already-approved semantics. It cannot modify Scenario semantics, create production implementation, generate implementation Task IDs, change ownership or authority, bypass a Human Gate, or dispatch a coder.
13. `RECONCILE_SPEC` always uses `next_task_id: NONE`; it does not require a human unless an independent Human Gate condition exists.

## Required Transitions

```text
SEMANTIC_GATE passed
+ Scenario approved
+ Spec absent / stale / incomplete
→ RECONCILE_SPEC

RECONCILE_SPEC completed
+ Spec sufficient
→ GENERATE_TASKS

GENERATE_TASKS completed
+ approved Task ID exists
→ DISPATCH_TASK
```

## Result Expectations

| Decision type | Expected result contract |
|---|---|
| `DISPATCH_TASK` | `TASK_RESULT` |
| `RECONCILE_SPEC` | `TASK_RESULT` for the normative artifact reconciliation only; not a production implementation result |
| `GENERATE_TASKS` | `TASK_RESULT` |
| `REQUEST_VALIDATION` | `VALIDATION_RESULT` |
| `SEMANTIC_GATE` | `NONE` until an approved semantic-gate result contract exists |
| `ARCHITECTURE_REVIEW` | `NONE` until an approved architecture-review result contract exists |
| `HUMAN_GATE` | `NONE` |
| `SCENARIO_SLICE_COMPLETE` | `PHASE_CONTROLLER_RESULT` or `NONE`, according to the caller |
| `PHASE_COMPLETE` | `PHASE_CONTROLLER_RESULT` or `NONE`, according to the caller |
| `STOP` | `NONE` |

## Static Acceptance Cases

### Case 1 — Dispatch the next approved task

- Input: `TASK_RESULT`, Task 1.1 `DONE`.
- Repository: Task 2.1 exists and is approved.
- Expected: `DISPATCH_TASK`, `next_task_id: 2.1`, `next_role: runtime-coder`, `expected_result_contract: TASK_RESULT`.
- Forbidden: inventing a different task or advancing the slice.

### Case 2 — Preserve a semantic block

- Input: `TASK_RESULT`, status `BLOCKED_FOR_SEMANTIC_REVIEW`.
- Expected: `SEMANTIC_GATE`.
- Forbidden: `DISPATCH_TASK` to a coder or rewriting the result as DONE.

### Case 3 — Complete a slice, not the phase

- Input: `VALIDATION_RESULT`, SC-P3-001 `PASS`.
- Repository: tasks 4/4; Phase 3 still has queued candidate scenarios.
- Expected: `SCENARIO_SLICE_COMPLETE`.
- Forbidden: `PHASE_COMPLETE`.

### Case 4 — Generate the missing task artifact

- Input: approved Scenario and sufficient Spec.
- Repository: `tasks.md` is missing.
- Expected: `GENERATE_TASKS`, `next_role: phase-evolution-controller` or the repository-approved OpenSpec task role.
- Forbidden: production dispatch before task generation.

### Case 5 — Protect frozen ownership

- Input: a proposed implementation changes frozen ownership.
- Expected: `ARCHITECTURE_REVIEW` or `HUMAN_GATE`, according to the frozen-decision policy.
- Forbidden: coder implementation under the existing task authority.

### Case 6 — Stop when no approved task exists

- Input: the previous result is complete.
- Repository: no approved next task exists.
- Expected: `STOP`, `next_role: NONE`, `next_task_id: NONE`.
- Forbidden: inventing a task.

### Case 7 — Reconcile OpenSpec after a passed Semantic Gate

- Input: SC-P3-002 Semantic Gate `BEHAVIOR_PURCHASE_ONLY`; Scenario `APPROVED`.
- Repository: SC-P3-002 OpenSpec artifacts are `ABSENT`.
- Expected: `RECONCILE_SPEC`, `next_role: openspec-coder` or the repository-approved OpenSpec owner, `next_task_id: NONE`, `requires_human: false`.
- Forbidden: `STOP`, `GENERATE_TASKS`, or `DISPATCH_TASK`; modifying the approved Scenario semantics; generating implementation Task IDs; production implementation.

## Explicitly Out of Scope

H4-1 does not implement:

- a ChatGPT/Codex transport bridge;
- an automatic router or worker pool;
- a task engine;
- automatic model switching;
- an automatic Semantic Gate;
- automatic OpenSpec reconciliation;
- automatic Phase advancement;
- a background daemon;
- an external orchestration service.

Any future transport or orchestration capability requires repeated workflow evidence and a separate purchase decision.
