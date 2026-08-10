# Scenario Trigger / Resume Contract — H4-2

> Status: Purchased and Materialized | Version: 1.1 | Date: 2026-08-10
> Scope: repository-driven lifecycle resolution only. This contract produces one Leader Decision and then stops.
> Depends on: `.ai/leader-decision-contract.md` (H4-1/H4-1.1).
> Evidence: the real `RUN_SCENARIO SC-P3-003` pilot correctly resolved candidate-only repository state to `SEMANTIC_GATE` without skipping lifecycle gates or dispatching implementation.

## Purpose

H4-2 standardizes exactly one boundary:

```text
Scenario identity
+ requested action
+ repository truth
→ one LEADER_DECISION
```

H4-2 discovers which H4-1 Leader Decision applies. It does not define a second Leader Decision schema and does not execute the decision it returns.

## Two-Lane Scenario Policy

Scenarios serve different purposes in the two development lanes:

- In `SEMANTIC_DISCOVERY`, scenarios may discover reality distinctions and
  purchase new semantics, Reality Models, or capability candidates through the
  required gates.
- In `CAPABILITY_DELIVERY_FAST`, scenarios primarily falsify already accepted
  semantics. They must not silently purchase new semantics during implementation.

The default delivery unit is:

```text
ONE PRESSURE
→ ONE MINIMUM FALSIFYING SCENARIO
→ ONE MINIMUM CAPABILITY DELTA
```

Do not require a large Scenario portfolio before implementation unless evidence
shows that the minimum Scenario is insufficient. Purchase additional Scenarios
only when a newly observed failure mode requires them. If the minimum Scenario
reveals a new Reality Pressure or semantic ambiguity, exit the Fast Lane and
return the exact pressure to Semantic Discovery.

H4-2 still resolves only one Leader Decision. When the selected Scenario or
pressure enters `SEMANTIC_DISCOVERY`, the separately defined
`SEMANTIC_DISCOVERY_AUTOPILOT` host in `.ai/auto-continue-contract.md` may
continue routine research → admission → capability-gap → candidate →
Architecture Fit stages without Human prompt relay. That host cannot change
H4-2 decision vocabulary or select an unrelated Scenario.

Scenario evidence participates in the Test Asset Evolution Feedback Loop. The
preferred regression form is the smallest `L2_SHORT_CHAIN_INTEGRATION` asset
that crosses the production boundaries responsible for the pressure; use L3/L4
when recorded/live reality is required. H4-2 does not itself promote an asset or
commit corpus priority.

## Canonical Input

```yaml
SCENARIO_TRIGGER:
  scenario: <explicit Scenario ID>
  action: START | CONTINUE | VALIDATE
```

The user-facing pilot form:

```text
RUN_SCENARIO <Scenario ID>
```

normalizes to `SCENARIO_TRIGGER` with `action: START`. This alias does not add another action mode.

`REPLAY` is not purchased. An explicit Scenario ID is required; H4-2 does not select a Scenario from a catalog.

### Caller-forbidden fields

The caller must not supply:

- Role, Tier, Model, Worker, or provider;
- Task ID;
- Decision Type;
- architecture authority;
- semantic authority.

Those values must be derived from repository truth and the existing H4-1 contract. Caller-supplied values cannot override repository state.

## Canonical Output

Exactly one output is permitted:

```text
LEADER_DECISION
```

The output must conform to `.ai/leader-decision-contract.md`. In particular:

- `decision_type` must use the H4-1 vocabulary;
- `next_role` must be a portable repository role;
- `next_task_id` may name only an existing approved task and is otherwise `NONE`;
- model or provider selection is forbidden;
- the output is a decision, not an execution trigger.

H4-2 stops immediately after producing that one Leader Decision.

## Repository Truth

Repository truth is authoritative; chat history is not lifecycle truth. Before resolving a trigger, inspect the Scenario's current repository-backed state, including as applicable:

- candidate or queued Scenario evidence;
- approved Semantic Gate and formal Scenario receipt;
- active OpenSpec proposal, design, specs, Scenario, and task state;
- approved task IDs and dependency completion;
- formal proof and independent-validation state;
- capability closeout/frozen receipt;
- semantic, ownership, authority, invariant, and frozen-decision conflicts.

Never assume `START` means start from zero. Never assume `CONTINUE` means dispatch a coder. Never invent a missing Task ID, Scenario state, or validation readiness.

If repository evidence conflicts or cannot determine one unique transition, return `STOP` unless an existing H4-1 gate rule uniquely requires `SEMANTIC_GATE`, `ARCHITECTURE_REVIEW`, or `HUMAN_GATE`.

## Resolution Order

Apply the following checks to repository truth and return the first uniquely applicable H4-1 decision:

1. Ownership, authority, invariant, or frozen-architecture conflict → `ARCHITECTURE_REVIEW` or `HUMAN_GATE`, according to H4-1 and frozen-decision policy.
2. Semantic conflict, or candidate evidence without an approved Semantic Gate → `SEMANTIC_GATE`.
3. Scenario already has a frozen capability closeout → `STOP` with reason `Scenario already frozen`.
4. Approved Scenario and passed Semantic Gate, but normative OpenSpec is absent, stale, or insufficient → `RECONCILE_SPEC`.
5. OpenSpec is sufficient, but implementation tasks are absent → `GENERATE_TASKS`.
6. An approved incomplete task exists and its dependencies are satisfied → `DISPATCH_TASK` with the existing repository Task ID.
7. Implementation and formal-proof tasks are complete, but independent validation remains → `REQUEST_VALIDATION`.
8. Independent validation passed, but capability closeout is absent → `SCENARIO_SLICE_COMPLETE`.
9. No unique transition → `STOP`.

H4-2 discovers when these decisions apply; H4-1 remains the authority for their meaning and fields.

## Action Semantics

### START

`START` means the caller intends to begin or activate work on the named Scenario. It does not reset repository progress, recreate artifacts, restart completed tasks, or ignore an existing Semantic Gate. A partially progressed Scenario resumes from repository truth.

### CONTINUE

`CONTINUE` means resume the named Scenario from its current repository lifecycle state. It returns exactly one next Leader Decision and performs no lifecycle action itself.

### VALIDATE

`VALIDATE` requests validation only when repository truth proves validation is currently permitted. It cannot bypass Semantic Gate, OpenSpec reconciliation, task generation, incomplete implementation tasks, or formal Scenario proof. If validation is premature, return the uniquely required prerequisite decision; otherwise return `STOP`. Never fabricate validation readiness.

## Lifecycle Cases

| Repository condition | Applicable trigger | Leader Decision |
|---|---|---|
| Candidate/queued evidence exists; Semantic Gate not approved | `START` or `CONTINUE` | `SEMANTIC_GATE` |
| Scenario and Semantic Gate approved; OpenSpec absent/stale/insufficient | any action whose prerequisite resolution reaches this state | `RECONCILE_SPEC` |
| OpenSpec sufficient; tasks absent | `START`, `CONTINUE`, or premature `VALIDATE` | `GENERATE_TASKS` |
| Approved incomplete task exists; dependencies satisfied | `START`, `CONTINUE`, or premature `VALIDATE` | `DISPATCH_TASK` using repository Task ID |
| Implementation/formal tasks complete; independent validation pending | `CONTINUE` or `VALIDATE` | `REQUEST_VALIDATION` |
| Validation passed; closeout absent | `START`, `CONTINUE`, or `VALIDATE` | `SCENARIO_SLICE_COMPLETE` |
| Scenario frozen | `START` or `CONTINUE` | `STOP` — already frozen |
| Semantic conflict | any | `SEMANTIC_GATE` |
| Ownership/authority/frozen-architecture conflict | any | `ARCHITECTURE_REVIEW` or `HUMAN_GATE` |
| No unique transition | any | `STOP` |

## Real Behavioral Pilot

Input:

```text
RUN_SCENARIO SC-P3-003
```

Repository state at invocation:

- SC-P3-003 had only deferred/candidate evidence;
- no approved Semantic Gate;
- no OpenSpec artifacts;
- no implementation tasks.

Observed resolution:

```text
decision_type: SEMANTIC_GATE
next_role: scenario-architect
next_task_id: NONE
```

Observed safety:

- no Task ID invented;
- no OpenSpec stage skipped;
- no runtime-coder dispatched;
- no production files modified;
- Fingerprint was not assumed;
- Scenario and Phase boundaries remained intact.

Pilot result: **PASS**.

This pilot was behavioral purchase evidence. H4-2 became authoritative only when this contract artifact was materialized.

## Current SC-P3-003 Acceptance State

At H4-2 materialization, the approved gate state is:

- Formal Scenario: approved;
- Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`;
- minimum semantic purchase: one immutable bounded-forward-viewport `DeviceAction` variant;
- OpenSpec: absent;
- implementation tasks: absent;
- Runtime implementation: not started.

Therefore:

```yaml
SCENARIO_TRIGGER:
  scenario: SC-P3-003
  action: CONTINUE
```

must resolve to one H4-1 decision:

```yaml
decision_type: RECONCILE_SPEC
next_role: openspec-coder
next_task_id: NONE
```

H4-2 does not execute that reconciliation.

## Static Acceptance Cases

### Case 1 — Candidate plus START

- Repository: candidate evidence exists; Semantic Gate is not approved.
- Input: `START`.
- Expected: `SEMANTIC_GATE`.

### Case 2 — Approved gate plus absent OpenSpec

- Repository: formal Scenario and Semantic Gate approved; OpenSpec absent.
- Input: `CONTINUE`.
- Expected: `RECONCILE_SPEC`, `next_task_id: NONE`.

### Case 3 — OpenSpec ready plus tasks absent

- Repository: normative OpenSpec sufficient; tasks absent.
- Input: `CONTINUE`.
- Expected: `GENERATE_TASKS`.

### Case 4 — Next dependency-safe task exists

- Repository: Task 1.1 `DONE`; approved Task 2.1 exists and its dependency is satisfied.
- Input: `CONTINUE`.
- Expected: `DISPATCH_TASK`, `next_task_id: 2.1`.

### Case 5 — Independent validation remains

- Repository: implementation and formal-proof tasks complete; independent validation pending.
- Input: `CONTINUE` or `VALIDATE`.
- Expected: `REQUEST_VALIDATION`.

### Case 6 — Validation passed plus closeout absent

- Repository: independent validation `PASS`; capability closeout absent.
- Input: `CONTINUE`.
- Expected: `SCENARIO_SLICE_COMPLETE`.

### Case 7 — Frozen Scenario

- Repository: named Scenario has a frozen capability closeout.
- Input: `CONTINUE`.
- Expected: `STOP`, reason `Scenario already frozen`.

### Case 8 — Ownership or authority conflict

- Repository: proposed next action conflicts with ownership, authority, or a frozen architecture decision.
- Input: any action.
- Expected: `ARCHITECTURE_REVIEW` or `HUMAN_GATE`, according to H4-1 policy.
- Forbidden: `DISPATCH_TASK`.

### Case 9 — No unique transition

- Repository: available evidence cannot determine exactly one next transition.
- Input: any action.
- Expected: `STOP`.

### Case 10 — Partially progressed Scenario plus START

- Repository: Scenario lifecycle has already advanced beyond candidate state.
- Input: `START`.
- Expected: resolve from current repository truth.
- Forbidden: reset, recreate, or restart completed lifecycle work.

## H4-1/H4-1.1 Compatibility

All H4-1/H4-1.1 decision rules and static cases remain authoritative. In particular:

```text
Semantic Gate passed
+ Scenario approved
+ OpenSpec absent / stale / incomplete
→ RECONCILE_SPEC
```

H4-2 only discovers that this transition applies. It neither changes `RECONCILE_SPEC` semantics nor relaxes its boundary.

## Platform Neutrality

The shared lifecycle layer is:

```text
SCENARIO_TRIGGER
→ repository truth
→ LEADER_DECISION
```

The contract assumes no Codex worker API, Codex inline-role behavior, Claude native subagent, recursive Claude call, provider, model, or tier. Claude and Codex may adapt the returned portable role differently, but that execution detail cannot alter lifecycle semantics.

## Explicitly Deferred

H4-2 does not implement:

- decision execution;
- recursive continuation or an automatic lifecycle loop;
- worker dispatch;
- model, provider, or tier selection;
- Task ID generation;
- Semantic Gate execution;
- OpenSpec reconciliation;
- implementation-task generation;
- Runtime modification;
- `REPLAY`;
- `RUN_NEXT_SCENARIO`;
- automatic Catalog or roadmap-priority commitment (evidence-pulled recommendations are allowed);
- H4-3 Auto-Continue Until Gate.

## Stop Rule

After emitting one conforming `LEADER_DECISION`, stop. A caller or separately authorized task must execute that decision.

The separately purchased H4-3 host loop is defined in `.ai/auto-continue-contract.md`. H4-3 consumes this one-decision contract without changing H4-2 input, resolution, or output semantics.
