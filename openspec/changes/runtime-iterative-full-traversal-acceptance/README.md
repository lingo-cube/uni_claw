# runtime-iterative-full-traversal-acceptance

> OpenSpec change — Phase 2.6 Runtime Iterative Full Traversal Acceptance (validation-only).

## Status

- **Phase**: PROPOSAL — artifacts created and strict-validated; implementation pending a separate Human Gate (Phase26Implementation: NOT_AUTHORIZED).
- **Classification**: Large Change (new acceptance surface + multi-campaign Real Emulator validation), validation-only.
- **Direction**: APPROVED (2026-08-26); Real Emulator campaign AUTHORIZED_IN_PRINCIPLE, waiting on the implementation gate.
- **Out of scope**: Runtime modification, new wire/API, Strategy Contract change, Memory service, dynamic depth, mid-run replanning, physical device (DEFERRED), Phase 3 apply (DEFERRED_UNTIL_PHASE26_COMPLETE).

## What this validates

Two capabilities, kept strictly separate:

- **A. `RUNTIME_SINGLE_RUN_AUTONOMY`** — graduated by Phase 2.5; re-asserted every run (one start, zero mid-run intervention, fresh observe/ground/authorize).
- **B. `UPPER_AGENT_CROSS_RUN_PLAN_ADAPTATION`** — new: the upper agent forms provenance-bearing ScenarioKnowledgeFixture records from runtime evidence, revises plans (evidenced, contract-legal PlanDelta), and issues safer/more effective independent strategies across runs.

Combination principle: **`Upper Agent learns; Runtime executes fresh.`**

Stage structure: 2.6A (iterative planning acceptance: online adaptation + persisted fixture reuse) → entry gate → 2.6B (real Android Settings full traversal on Real Emulator with a mature strategy; target claim `RUNTIME_AGENT_CAN_AUTONOMOUSLY_EXHAUST_A_REAL_BOUNDED_UI_TREE`, Real-Emulator tier, no physical claim).

## Key frozen invariants

`HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH` · `HISTORICAL_RESULT != RUNTIME_ACTION_AUTHORITY` · `RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS` · `AUTONOMOUS_EXCEPTION_DISPOSITION != UNIVERSAL_RECOVERY` · `TEST_KNOWLEDGE != RUNTIME_TRUTH / ACTION_AUTHORITY / FORMAL_MEMORY`.

## Safety posture

`UNPROVEN_SAFE → RECORD_ONLY / FAIL_CLOSED`. Dangerous classes are learned only from observational/typed/boundary evidence, never by execution; knowledge shapes the next strategy only through existing contract levers (`prohibitedEffects`, dispatch policy). Acceptance asserts an empty dangerous-dispatch intersection across all runs.

## Artifacts

- `proposal.md` — buyer claims, scope, non-claims.
- `design.md` — decisions D1–D7 (loop, knowledge fixture, safety, PlanDelta, binding, reuse), authority proof, risks.
- `specs/runtime-iterative-full-traversal-acceptance/spec.md` — normative requirements.
- `tasks.md` — serial stages A–N with the implementation gate first.

## Validation

```bash
openspec validate runtime-iterative-full-traversal-acceptance --strict
```
