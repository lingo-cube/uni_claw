# PROJECT_LEADER_U3_F1_VARIATION_SLICE_GATE_RESULT

> Date: 2026-08-10
> Role: Project Leader
> Parent: `PROJECT_LEADER_U3_TASK_FAMILY_SCOPING_RESULT`
> Task family: `U3-F1 — Single-Target Desired-State Assurance`
> Gate mode: Scenario selection + lane classification only
> Runtime changes: NONE

## Decision

`ENTER_CAPABILITY_DELIVERY_FAST`

The minimum U3-F1 slice is a test-only production-shaped variation proof. It
reuses the accepted U1 desired-state Goal, CP-06 zero-unnecessary-mutation
semantics, CP-12/RM-10 target grounding, CP-14/RM-11 closed-world projection,
existing safety receipts, Traversal execution/fresh verification, and Agent
GoalEvidence completion authority.

No new world-truth, completion, safety, Recovery, ownership, authority, or
architecture semantic is required. Candidate ordering is Observation evidence,
not target identity or action authority.

## Minimum Falsifying Scenario Contract

```text
Scenario: SC-U3-F1-001
Title: Wi-Fi Desired-State Assurance Under Reordered Similar Candidates
Evidence level: deterministic production-shaped Scenario (current S0/E1 lane)
New disturbance class: UI structural variation only
```

### Authoritative Input

All branches use the same already-authoritative structured meaning:

- Intent: `确保 WiFi 已开启`;
- Goal: fresh external evidence must show the `Wi-Fi` setting ON;
- execution representation: existing `CLOSED_WORLD_CONCRETE` with the exact
  caller-supplied Plan;
- safety authority and grounding criterion: supplied immutable existing inputs.

The Scenario must pass through the production-shaped upstream projection and
existing Runtime path. It must not parse natural language, select a hidden
method, invent a route, or change the supplied Plan.

### Branch A — Already Satisfied Under Layout Variation

Given a fresh Settings Observation where Wi-Fi is already ON and unrelated or
similar rows have changed order, then:

1. existing fresh GoalEvidence satisfies the Goal;
2. zero Tap and zero SetSwitch are dispatched;
3. the non-empty supplied Plan creates no mandatory work;
4. completion comes only from Agent consumption of the fresh GoalEvidence.

### Branch B — Reordered Similar Candidates

Given Wi-Fi is OFF and the candidate list order differs from the U1 baseline,
with `Wi-Fi Calling` appearing before the state-bearing `Wi-Fi` row, then:

1. every current matching candidate is evaluated deterministically in current
   Observation order;
2. the selected target is supported by current semantic evidence, not by the
   baseline index or text match alone;
3. exactly one Tap is dispatched to the current `Wi-Fi` candidate index;
4. exactly one minimum `SetSwitch(true)` follows only after fresh expected
   destination evidence;
5. a later fresh Observation shows Wi-Fi ON;
6. only satisfied fresh GoalEvidence completes the Run.

This branch falsifies an implementation that memorizes index 0, treats the
first text match as identity, or binds task meaning to one fixture layout.

### Branch C — Reordered but Ambiguous

Given the reordered current candidates do not provide sufficient evidence to
uniquely distinguish the intended Wi-Fi target, then:

1. ambiguity remains explicit;
2. zero Tap and zero SetSwitch are dispatched;
3. no action success or Goal completion is fabricated;
4. no default index, route, desired state, or authority is invented.

### Deterministic Replay

Equal Intent, Goal, Plan, RunId, world variant, and scripted dispatch outcomes
must replay equal candidate/safety evaluation order, actions, Observations,
Traversal journal, Trace, GoalEvidence, reason, and final RunState.

## Explicitly Reused Regression Evidence

The new Scenario does not duplicate these frozen cases; they remain targeted
regressions around it:

- U1 already-ON, OFF-to-ON, ambiguity, wrong destination, and timeout branches;
- CP-12 positive, contradicted, unconfirmed, and unsafe grounding branches;
- SC-P3-001 timeout uncertainty with no blind redispatch;
- SC-P3-002 Popup continuity and SC-P2 Recovery ownership boundaries.

Popup, timeout, external drift, observation failure, and alternate-route
selection are not combined into `SC-U3-F1-001`. Each is a later independent
disturbance slice so failure attribution remains falsifiable.

## Architecture Fit Check

| Boundary | Result | Reason |
|---|---|---|
| Intent authority | `UNCHANGED` | Caller supplies the already-resolved Intent, Goal, and closed-world representation. |
| Goal authority | `UNCHANGED` | Agent completes only from existing fresh GoalEvidence. |
| Grounding authority | `UNCHANGED` | Existing immutable criterion evaluates current candidate evidence; no fixed index becomes authority. |
| Safety authority | `UNCHANGED` | Existing independent authorization receipts remain required. |
| Local execution | `UNCHANGED` | Traversal retains selection, dispatch, first fresh Observation, and expected-effect verification. |
| World authority | `UNCHANGED` | Fake Environment varies only visible state, order, transitions, and dispatch outcome. |
| Mutable-state ownership | `UNCHANGED` | No production state is added. |
| Dependency direction | `UNCHANGED` | Existing upstream projection and Agent → Container → Traversal → Environment spine remain intact. |
| Architecture invariants | `UNCHANGED` | I-1 through I-14 remain applicable without amendment. |

`ARCHITECTURE_FIT_CONFIRMED`

## Exact Fast-Lane Purchase

```text
Production delta: 0
Model/API delta: 0
Ownership delta: NONE
Authority delta: NONE
Dependency delta: NONE
Safety-semantic delta: NONE
Evidence-maturity advance: NONE
OpenSpec change: NONE — test-only composition of accepted frozen semantics
```

Allowed next-step files:

- one deterministic test fixture under
  `tests/UniClaw.Runtime.Tests/Scenario/Fakes/`;
- one formal Scenario test under
  `tests/UniClaw.Runtime.Tests/Scenario/`;
- one validation/completion receipt under `docs/decisions/`;
- the minimum roadmap status reconciliation after validation.

Forbidden:

- any `src/UniClaw.Runtime/` modification;
- Goal, Plan, Intent envelope, grounding, safety, Traversal, Recovery, or Agent
  semantic change;
- new enum, type, interface, component, engine, mutable field, route model,
  Planner, Graph, FSM, LLM/VLM/provider integration, or clarification UX;
- Popup/timeout/drift composition inside this Scenario;
- S1/S2/S3 or live-emulator maturity claims;
- `U3-F2`, `U3-F3-CANDIDATE`, or another U3 Scenario.

## Required Verification for the Fast Loop

- targeted `SC-U3-F1-001` positive, ambiguity, and replay tests;
- frozen U1 and CP-12 regressions;
- relevant uncertain-action, Popup, and Recovery guards unchanged;
- `dotnet build src/UniClaw.Runtime.sln`;
- full `dotnet test src/UniClaw.Runtime.sln`;
- Architecture Guards;
- consistency checks, OpenSpec strict validation, and diff hygiene;
- independent scope audit proving production delta remains zero.

## Gate Result

```text
Lane: CAPABILITY_DELIVERY_FAST
Scenario: SC-U3-F1-001
Architecture: ARCHITECTURE_FIT_CONFIRMED
Semantic Gate: NOT_REQUIRED
Architecture Gate: NOT_REQUIRED
Safety Gate: NOT_REQUIRED
Human Gate: NOT_REQUIRED
Implementation authorization: TEST_ONLY_ZERO_PRODUCTION_DELTA
Runtime implementation: NOT_STARTED
```

## Recommended Next Task

`U3_F1_VARIATION_MINIMUM_VERTICAL_SLICE_FAST_LOOP`

Execute only `SC-U3-F1-001` inside the exact test-only boundary above and stop
at `VALIDATED` or a canonical Hard Gate.

