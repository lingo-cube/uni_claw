# Auto-Continue Until Gate Contract — H4-3

> Status: Purchased and Materialized | Version: 1.1 | Date: 2026-08-10
> Scope: one selected Scenario or semantic pressure, one repository-driven decision per iteration, one execution owner, stop at a material Gate or terminal state.
> Depends on: `.ai/scenario-trigger-contract.md` (H4-2), `.ai/leader-decision-contract.md` (H4-1/H4-1.1), `.ai/result-contract.md`, and `.ai/model-routing.yaml`.
> Evidence: SC-P3-001 and SC-P3-002 repeatedly required manual result-to-decision-to-execution relay after the next transition was already uniquely determined.

## Purchase

The original H4-3 purchase remains exactly:

```text
AUTO_CONTINUE_UNTIL_GATE
```

`DEVELOPMENT_FLOW_OPTIMIZATION` adds
`SEMANTIC_DISCOVERY_AUTOPILOT` as a selected-pressure host mode. It does not add
an H4-1 decision type, change H4-2 resolution, or authorize unrelated Scenario
selection.

Given an already selected Scenario, a control-loop host may repeatedly:

1. read repository truth;
2. derive exactly one H4-1 `LEADER_DECISION` through H4-2;
3. execute exactly that authorized decision;
4. require and validate the declared Result Contract;
5. re-read repository truth;
6. resolve the next decision;
7. stop at a mandatory Gate or terminal Scenario state.

Capability-delivery autonomy is allowed only inside the already-approved
semantic envelope. Semantic-discovery autonomy is allowed only inside one
explicitly selected pressure boundary and the governance stages defined by
`.ai/development-protocol.md`; it does not pre-authorize a production purchase.
A stronger executor may satisfy a task's minimum tier, but it never receives
stronger authority.

## Default Continuation Policy — Capability Delivery Fast Lane

The H4-3 loop is the default execution host for an accepted capability delivery
task. The canonical policy is:

```text
DEFAULT_CONTINUATION_POLICY:

if current task is in CAPABILITY_DELIVERY_FAST
and no Hard Gate is triggered
and work remains within the accepted semantic envelope:
    Project Leader MUST continue automatically
```

The Project Leader must not stop solely because a worker finished, a test
failed, a repair is needed, another validation pass is required, documentation
needs reconciliation, or a local implementation choice is pending.

The following bounded failures continue through `Diagnose → Repair → Validate`:
`MECHANICAL_FAILURE`, `TEST_FIXTURE_FAILURE`, `LOCAL_BEHAVIOR_GAP`,
`LOCAL_COMPOSITION_GAP`, purchased-semantic `ASSERTION_MISMATCH`,
`DOC_RECONCILIATION`, local `BUILD_FAILURE`, repairable implementation
regressions, style/lint/static failures, and bounded deterministic test failures.

This default does not override the mandatory stop conditions below. An
independent validator finding is first classified. Mechanical, fixture,
documentation, local-composition, or already-purchased semantic failures are
repaired and revalidated automatically. Semantic or architecture pressure,
scope expansion, routing failure, or repository-state conflict remains a stop
unless an existing contract provides a unique safe resolution.

## Semantic Discovery Autopilot Host

For one explicitly selected pressure/capability boundary, the same host may run
`SEMANTIC_DISCOVERY_AUTOPILOT`:

```text
Evidence research → Reality Model extraction → independent validation
→ condition repair → admission → capability-gap analysis
→ candidate generation → Architecture Fit
```

Each iteration emits or updates one repository artifact/result, validates its
contract, re-reads repository truth, and derives only the next stage. Routine
provenance normalization, evidence-grade/label repair, deduplication,
conditional-pass repair, and admission mechanics auto-continue. Workers may
perform bounded research, minimization, fixture construction, validation, and
repair; the Project Leader alone commits semantic/admission/architecture/corpus
and priority decisions.

The host does not choose an unrelated next Scenario or capability. It stops for
Human input only at the seven material boundaries in
`.ai/development-protocol.md` and presents the five-field compressed decision
packet defined there.

## Canonical Input

```yaml
AUTO_CONTINUE:
  scenario: <explicit Scenario ID>
  originating_action: START | CONTINUE | VALIDATE  # optional
```

The Scenario ID is mandatory and must already be selected by the caller. `originating_action` preserves invocation intent but cannot override repository lifecycle state.

Semantic Discovery Autopilot uses a separate explicit selected-pressure input:

```yaml
SEMANTIC_DISCOVERY_AUTOPILOT:
  pressure: <repository evidence cluster / CP candidate / named capability gap>
  owner_architecture_prior: <explicit prior | NONE>
```

The pressure is mandatory. It cannot mean `RUN_NEXT_SCENARIO`, automatic
roadmap selection, or permission to mine and commit unrelated capabilities.

The caller must not provide:

- Task ID;
- Role or Tier;
- Model, provider, or Worker;
- Leader Decision type;
- semantic authority;
- architecture authority.

Those values derive from repository truth and the contracts listed above.

## Canonical Scenario Lifecycle Loop

```text
while true:
    read repository truth

    derive exactly one LEADER_DECISION using H4-2 and H4-1

    if the decision or repository state requires a mandatory stop:
        return AUTO_CONTINUE_RESULT

    execute exactly that authorized decision through a platform adapter
    require the decision's expected Result Contract
    validate the returned contract shape and status

    re-read repository truth
    continue
```

The host must never derive multiple future decisions in advance, cache a next Task ID across execution, or assume that an earlier repository snapshot remains current. Every iteration starts with a fresh repository read.

The Semantic Discovery Autopilot follows the same one-stage/fresh-read rule but
derives the next governance stage from its selected pressure rather than using
H4-2 to invent a Scenario lifecycle decision.

## Loop Result

When the loop stops it returns one summary without inventing lifecycle state:

```yaml
AUTO_CONTINUE_RESULT:
  scenario: <Scenario ID>
  status: GATE_REACHED | FROZEN | REJECTED | BLOCKED | STOPPED
  iterations_completed: <non-negative integer>
  last_result_type: <repository-approved Result Contract type | NONE>
  stop_decision: <H4-1 decision type | NONE>
  reason: <repository-backed stop reason>
  evidence:
    - <repository artifact or validated result>
```

`AUTO_CONTINUE_RESULT` summarizes why the host stopped. It cannot rewrite a blocked, failed, conditional, rejected, or frozen state.

`SEMANTIC_DISCOVERY_AUTOPILOT` returns the separate result contract defined in
`.ai/result-contract.md`; it does not overload Scenario lifecycle status.

## Authorized Auto-Continue Decisions

### SEMANTIC_GATE

May execute only when:

- the selected Scenario has candidate/queued evidence;
- no approved Semantic Gate exists;
- repository truth does not require a Human Gate;
- the next semantic role is uniquely determined.

If the gate returns an explicit approved semantic purchase, including a non-zero model delta, the loop may continue inside that exact purchase. A new or ambiguous purchase outside the gate result stops the loop.

### RECONCILE_SPEC

May execute only when:

- the Scenario and Semantic Gate are approved;
- the approved semantic purchase is explicit;
- OpenSpec is absent, stale, or incomplete;
- no unresolved semantic, ownership, authority, or architecture conflict exists.

The OpenSpec role may align normative artifacts only; it cannot implement production or generate Task IDs during reconciliation.

### GENERATE_TASKS

May execute only when:

- the approved Scenario exists;
- normative OpenSpec is sufficient;
- the task artifact is absent or incomplete;
- no additional Gate is required.

### DISPATCH_TASK

May execute only when:

- the Task ID exists and is approved in repository truth;
- all dependencies are satisfied;
- the required previous result is `DONE` or `PASS`;
- the task remains inside the approved production purchase and deferred boundary;
- exactly one execution owner is assigned.

The host cannot create, rename, combine, or speculate about Task IDs.

### REQUEST_VALIDATION

May execute only when:

- every required implementation and formal-proof task before validation is complete;
- independent validation is repository-authorized;
- the validator role and expected contract are uniquely determined.

### SCENARIO_SLICE_COMPLETE and capability closeout

Capability closeout may execute automatically only when:

- independent validation returned `PASS`;
- the approved capability and its Scenario boundary are fully known;
- actual Production, Ownership, and Authority deltas match the approved budgets;
- no unresolved semantic, architecture, or validator finding remains;
- closeout is the unique next transition.

The closeout freezes only the selected Scenario. It must not infer `PHASE_COMPLETE`. After the Scenario becomes frozen, the loop stops.

## Mandatory Stop Conditions

H4-3 stops immediately when any of the following is true:

1. A Human Gate is required by one of the seven material boundaries in `.ai/development-protocol.md`.
2. Architecture review is required by repository truth or an `ARCHITECTURE_REVIEW_REQUIRED` result.
3. A semantic conflict is not already covered by an explicit approved Semantic Gate purchase.
4. Mutable-state ownership is ambiguous.
5. Decision authority is ambiguous.
6. A frozen architecture or Human Gate decision would need to change.
7. Scenario and normative OpenSpec contradict each other.
8. Actual or proposed production purchase exceeds the approved budget.
9. A task requires an unapproved type, field, enum, interface, component, or mutable state.
10. A validator returns `FAIL` whose cause cannot be repaired mechanically or within the already-approved semantic/scope boundary.
11. A validator returns `CONDITIONAL_PASS` whose remaining condition requires semantic expansion, architecture/ownership/authority/safety change, material public API purchase, or other non-mechanical judgment; mechanically resolvable conditions continue through repair.
12. A task result returns `BLOCKED_FOR_SPEC`, `BLOCKED_FOR_SEMANTIC_REVIEW`, `BLOCKED_FOR_ARCHITECTURE_REVIEW`, `BLOCKED_FOR_HUMAN`, or `ROUTING_UNAVAILABLE`, unless the selected Semantic Discovery Autopilot boundary or another existing contract specifies one unique safe next stage/fallback.
13. Repository truth cannot determine exactly one next transition.
14. The expected Result Contract is missing, malformed, or inconsistent with the Leader Decision.
15. Repository truth changes unexpectedly during execution or conflicts with the consumed result.
16. The Scenario becomes `FROZEN`, `REJECTED`, or `BLOCKED`, or Phase completion would have to be inferred rather than explicitly proven.

Stopping preserves the actual result and repository state. It is not failure recovery and does not authorize repair.

## Unexpected Finding Classification

### NON_BLOCKING_OBSERVATION

Examples include an optional semantic service being unavailable with a documented fallback, or a generic documentation hook being absent while repository-native checks pass. Record the evidence and continue only if the active contract remains satisfied.

### STRUCTURAL_PRESSURE

Examples include duplicated mechanical pipelines, temporal coupling, or increasing class size. Record the evidence for a later Architecture Shape Audit. Continue only if the current task remains inside its approved semantic, ownership, authority, and production budgets. Do not refactor automatically.

### SEMANTIC_OR_ARCHITECTURE_PRESSURE

Examples include a required unapproved semantic distinction, non-unique authority, ownership transfer, or frozen-boundary change. Stop immediately at the appropriate Gate.

Unexpected findings never expand task authority.

## Test Asset Evolution During Continuation

Each meaningful run or failure is triaged using the classifications in
`.ai/development-protocol.md`. When promotion criteria are met, the loop should
produce the smallest feasible replayable asset, preferring
`L2_SHORT_CHAIN_INTEGRATION` and then `L3_RECORDED_REALITY_REPLAY`. The host must
not mark a meaningful production failure fully closed without such an asset
where feasible, and must not mock away the layer that caused the pressure.

Asset clusters and evidence-maturity gaps may recommend next capability
pressure. That recommendation is not automatic top-level selection; the Project
Leader commits any corpus promotion and next-capability priority.

## Result Handling

Each executed role must return the result declared by the Leader Decision and repository task convention, such as:

- `TASK_RESULT`;
- `VALIDATION_RESULT`;
- `OPENSPEC_RECONCILIATION_RESULT`;
- `TASK_GENERATION_RESULT`;
- an approved Semantic Gate result;
- a capability closeout result.

The host validates result type, required fields, status vocabulary, Scenario/Task identity, and production delta before continuing. Build success, test success, or changed files alone never imply `DONE`.

A result and the freshly reloaded repository state must agree. If they do not, stop for repository-state conflict.

## Routing and Execution Ownership

Routing remains governed by `.ai/model-routing.yaml`.

- Preserve one active task → one logical role → one execution owner.
- The logical roles `PROJECT_LEADER_MODEL` and `EXECUTION_WORKER_MODEL` are provider-neutral. Provider-specific model identifiers resolve from `.ai/model-routing.yaml`.
- Changing provider must not change auto-continue semantics, stop conditions, or Hard Gate behavior.
- No parallel Scenario or Runtime coding is authorized.
- A preferred executor may use an existing same-role stronger-tier fallback.
- Fallback cannot reduce the logical role, minimum reasoning tier, task boundary, or authority requirement.
- If no allowed executor exists, stop with `ROUTING_UNAVAILABLE`.
- A stronger model cannot expand semantics, scope, ownership, or authority.

H4-3 does not modify routing rules and does not require a specific worker implementation.

## Platform Adapter Boundary

The shared control protocol is:

```text
SCENARIO_TRIGGER
→ LEADER_DECISION
→ ROLE EXECUTION
→ RESULT
→ repository refresh
→ next LEADER_DECISION or stop
```

Codex may execute the portable role inline or through an available approved worker. Claude may execute through its main session and an appropriate `.claude/agent`. Claude custom agents do not need recursive dispatch capability; the main session remains the loop host and consumes every result.

Platform adapters may differ in invocation mechanics only. Lifecycle semantics, authority, stop conditions, task identity, and result validation must remain identical.

## No Prompt Relay Requirement

After `AUTO_CONTINUE` begins, the user need not manually relay a successful Task 1.1 result into the Task 2.1 prompt, Task 2.1 into Task 3.1, or formal proof into validator dispatch when repository truth uniquely authorizes the next transition.

Likewise, after `SEMANTIC_DISCOVERY_AUTOPILOT` begins, the user need not relay
routine evidence research into extraction, validation, condition repair,
admission, capability-gap analysis, candidate generation, or Architecture Fit.

This removal of relay friction does not permit the host to cross a mandatory stop condition.

## Fast Lane Boundary Escalation

When a Hard Gate is encountered:

1. Preserve executable evidence.
2. Record the exact failed assumption.
3. Exit the Fast Lane.
4. Enter Semantic Discovery only for that pressure.
5. Resolve the pressure through the required gate.
6. Return to the same Fast Lane without restarting unrelated lifecycle work.

## Current SC-P3-003 Dry Run

Current approved state at H4-3 materialization:

- Semantic Gate: complete;
- decision: `SEMANTIC_PURCHASE_REQUIRED`;
- approved minimum purchase: one immutable `DeviceAction` variant representing one bounded forward viewport movement;
- OpenSpec: absent;
- Runtime implementation: not started.

Fresh H4-2 resolution for `SC-P3-003` therefore yields:

```yaml
decision_type: RECONCILE_SPEC
next_role: openspec-coder
next_task_id: NONE
```

Dry-run result: **PASS**. The decision was not executed.

## Static Acceptance Cases

### Case 1 — Full behavior-only happy path

- Initial: candidate Scenario.
- Flow: `SEMANTIC_GATE → BEHAVIOR_PURCHASE_ONLY → RECONCILE_SPEC → GENERATE_TASKS → DISPATCH_TASK 1.1 → DISPATCH_TASK 2.1 → DISPATCH_TASK 3.1 → REQUEST_VALIDATION → PASS → closeout → FROZEN`.
- Expected: reach the Scenario-specific frozen state without human prompt relay.

### Case 2 — Approved semantic purchase

- Initial: candidate Scenario.
- Gate result: `SEMANTIC_PURCHASE_REQUIRED` with an explicit approved minimum production delta.
- Expected: continue to `RECONCILE_SPEC`; do not stop merely because the approved model delta is non-zero.

### Case 3 — Unapproved semantic growth

- Result: implementation requires a new production field while the approved budget is zero.
- Expected: stop at the semantic or architecture Gate.

### Case 4 — Ownership conflict

- Task pressure: Container would have to invoke Agent Recovery directly.
- Expected: stop at `ARCHITECTURE_REVIEW` or `HUMAN_GATE`.

### Case 5 — Mechanically repairable validator failure

- Result: `VALIDATION_RESULT` with `FAIL` caused by fixture, documentation,
  local composition, or implementation error inside the approved purchase.
- Expected: classify → repair → revalidate; do not create a Human Gate.

### Case 6 — Conditional validation

- Result A: `CONDITIONAL_PASS` with a mechanically predetermined
  provenance/label/dedup/fixture repair.
- Expected A: repair and continue.
- Result B: remaining condition requires semantic expansion, architecture,
  ownership/authority, safety, material public API, or scope judgment.
- Expected B: stop at the exact canonical Gate.

### Case 7 — Allowed routing fallback

- State: preferred standard executor unavailable; routing contract permits a stronger-tier same-role fallback.
- Expected: continue under the same role and Task ID with unchanged authority.

### Case 8 — Routing unavailable

- State: no approved executor or fallback exists.
- Expected: stop with `ROUTING_UNAVAILABLE`.

### Case 9 — Frozen Scenario

- Input: `AUTO_CONTINUE` for SC-P3-001.
- Expected: stop immediately without executing work.

### Case 10 — Missing tasks

- State: OpenSpec sufficient; task artifact absent.
- Expected: `GENERATE_TASKS`, not an invented `DISPATCH_TASK`.

### Case 11 — Missing OpenSpec

- State: Semantic Gate and Scenario approved; OpenSpec absent.
- Expected: `RECONCILE_SPEC`.

### Case 12 — Architecture pressure only

- Result: task succeeds with temporal-coupling or duplication evidence, but no semantic, ownership, authority, or budget conflict.
- Expected: record `STRUCTURAL_PRESSURE`, continue the approved Scenario, and do not refactor automatically.

### Case 13 — Scenario-specific closeout

- State: validation passed while the Phase still has queued Scenarios.
- Expected: freeze only the selected Scenario; never emit `PHASE_COMPLETE`.

### Case 14 — Non-unique next action

- State: repository evidence supports two unresolved authority choices.
- Expected: stop; never guess.

### Case 15 — Repository refresh

- State: task completion updates `tasks.md`.
- Expected: re-read repository truth before deriving the next Task; do not use a cached Task ID.

### Case 16 — Semantic Discovery routine governance

- Input: one explicitly selected pressure with evidence needing provenance
  normalization, independent validation, condition repair, and admission.
- Expected: auto-continue through those stages; no Human prompt relay.

### Case 17 — Owner architecture prior

- Input A: `OWNER_ARCHITECTURE_PRIOR` with no material repository falsifier.
- Expected A: Project Leader adopts it as working direction without restarting
  full discovery.
- Input B: executable repository evidence materially contradicts the prior.
- Expected B: stop with the exact evidence and compressed Human decision packet.

### Case 18 — Human compression

- State: one of the seven Human boundaries is reached.
- Expected: user-facing output contains only Goal, discovery/change,
  architecture impact, material trade-off, and exact decision; detailed
  governance evidence remains in repository artifacts.

### Case 19 — Meaningful production failure asset

- State: a reproducible production failure crosses two responsible production
  boundaries and can be minimized.
- Expected: preserve the smallest L2 short-chain integration regression asset
  with an explicit oracle; do not mock away the failing boundary.

### Case 20 — Evidence-pulled roadmap recommendation

- State: regression/coverage/evidence clusters identify a likely next pressure.
- Expected: Project Leader may recommend and rank it from evidence; the host
  does not automatically start that unrelated capability.

## H4 Regression Boundary

H4-3 consumes but does not replace:

- H4-1/H4-1.1 Leader Decision vocabulary and cases;
- H4-2 Scenario Trigger resolution and cases.

All prior acceptance cases remain authoritative. H4-3 may execute only the one decision those contracts derive.

## Deferred Automation

H4-3 does not purchase:

- `RUN_NEXT_SCENARIO`;
- automatic Catalog or roadmap priority commitment;
- unbounded mining or commitment outside the explicitly selected pressure;
- parallel Scenario loops;
- multi-worktree orchestration;
- background daemon or service;
- external ChatGPT ↔ Codex transport;
- webhook orchestration;
- automatic Phase selection;
- autonomous architecture-invariant/ownership/authority changes or Human Gate decisions.

## Harness Baseline State

Successful validation of this contract establishes:

```text
HARNESS_CONTROL_LOOP_BASELINE_READY
```

This means the repository defines the minimum platform-neutral lifecycle:

```text
SCENARIO_TRIGGER
→ LEADER_DECISION
→ ROLE EXECUTION
→ RESULT
→ AUTO-CONTINUE
→ GATE / FROZEN
```

It does not mean fully autonomous development or automatic Scenario selection.
