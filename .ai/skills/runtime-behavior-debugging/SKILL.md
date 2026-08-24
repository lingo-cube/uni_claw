---
name: runtime-behavior-debugging
description: Debug Agent runtime / FSM / traversal / asynchronous / real-device / flaky / nondeterministic failures evidence-first — classify the failure, check evidence availability (E0-E4), and only then touch code.
metadata:
  type: Debugging Method
  authority: NONE
---

# Runtime Behavior Debugging

> Evidence-first debugging for **Runtime behavior** failures (Agent loop, FSM,
> traversal, asynchronous workflows, real devices, flaky / nondeterministic
> tests). Complementary to `architecture-evidence-first-debugging` — this skill
> is specific to the UniClaw Agent Runtime's observable behavior.
> Authority: **NONE** — it is an execution method; it creates no decision or
> gate and authorizes no code change by itself.
>
> **Relationship (kept separate by design)**: this skill is the
> RUNTIME-SPECIFIC application; `evidence-driven-debugging` is the GENERAL
> methodology (E0-E4 evidence levels, task risk classification L0-L4, worker
> flow, test design, review integration). Unified rules live in
> `.ai/development-protocol.md` §17; the review gate is
> `.ai/reviews/change-review.md`.

## When to Use

A task involves any of:

- Agent runtime (open-world DFS / semantic loop / plan run / recovery seam)
- FSM / state-machine / lifecycle transitions
- Traversal / action execution / settle loops
- Asynchronous workflows (background jobs, cancellation, races)
- Real device / emulator / ADB / vision socket behavior
- Flaky tests or nondeterministic outcomes

## Workflow

### 1. Failure Classification (before any code read)

Classify the failure into one of:

| class | meaning |
|-------|---------|
| A | discovery failure (sources not found / inventory wrong) |
| B | grounding failure (occurrence / logical-source resolution failed) |
| C | authorization failure (candidate denied) |
| D | execution failure (action / settle / return failed) |
| E | recovery / revisit failure (bounded recovery did not cover) |
| F | environment failure (device / emulator / vision / fixture) |

Also record: which lifecycle stage, the last correct state, and which
invariant / guard / fail-closed gate was triggered.

### 2. Evidence Availability Check

| level | evidence available |
|-------|--------------------|
| E0 | error message only |
| E1 | logs |
| E2 | action / history records |
| E3 | trace / state timeline |
| E4 | trace + observation timeline + fact/evidence ledger |

**Rule: at E0-E1 code analysis is permitted; at E2-E4 the evidence MUST be
analyzed first and the classification grounded in it before any code
modification.** Never attribute to "OCR / device / emulator" without frame or
trace evidence.

### 3. Evidence-Driven Root Cause (no premature attribution)

Ground the classification in the collected evidence:

- runtime trace (revisit steps, coverage ledger, grounding/authorization
  rejections, terminal reason)
- observation timeline (per-frame OCR/structured content)
- branch coverage evidence (discovered / resolved / unresolved)
- action history
- failure terminal reason

Categories (with evidence, not guesses):

- Runtime logic defect
- Test fixture / environment mismatch
- Insufficient bounded policy (budget / step / termination quantity)
- Observation / evidence missing

### 4. Architecture Ownership Check

Confirm the owner (Agent / Traversal / Environment / Semantic Capability /
Container) and state explicitly what is NOT touched:

- DFS ownership
- GoalEvidence authority
- Semantic authority
- scenario knowledge (Settings / child index / list-size / coordinate memory)

Stop (per PROJECT_LEADER stop conditions) if the fix would require modifying
any of those.

### 5. Fix Only After Evidence

Apply the minimal fix inside the owned seam; then:

- deterministic tests (EvidenceFixture + ExpectedSpecification — never fixed
  click counts / fixed action sequences / fixed page paths)
- real-device re-run with the same evidence dump
- full regression

## Exit Criteria

The debugging result records: evidence summary, root cause, ownership decision,
production change list, architecture impact, regression result, remaining
uncertainty. No self-graduation — an independent review validates the result
against the stop conditions.
