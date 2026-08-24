---
name: evidence-driven-debugging
description: Evidence-driven AI coding workflow — build a human-readable reality model (Expected → Observed → Gap → First Divergence) before entering evidence levels (E0-E4), classify failures, respect ownership, apply the minimal change, and validate with capability tests. Use for Runtime/Agent/FSM/Traversal/Recovery/Async/Real-device/flaky failures.
metadata:
  type: Debugging Method
  authority: NONE
---

# Evidence-Driven Debugging (AI Coding Workflow)

> The UniClaw Runtime engineering experience codified as a mandatory workflow
> for AI coding agents: **Evidence → Diagnosis → Ownership → Minimal Change →
> Validation**. This skill is methodology; `runtime-behavior-debugging` is its
> runtime-specific application (evidence levels E0-E4, failure classes,
> evidence-first rules for the Agent loop / FSM / Traversal / real device).
> Authority: **NONE** — it authorizes no code change by itself.

## Core Principle

Never guess Runtime behavior. Never modify code directly from a failure
phenomenon. Never weaken a test to bypass a real problem.

### Forbidden

- **Deriving code owner from the symptom alone.**
  Wrong: "Button not found → Semantic Capability problem."
  Right: "The button was not discovered; confirm whether Observation,
  Normalization, or Grounding failed first."

- **Using a code error message as the reality description.**
  Wrong: "`TryHandleExternalBoundaryAsync` failed."
  Right: "The page changed after the click, but the Runtime did not confirm
  the external page appeared."

- **Modifying a verification condition to pass a test.**
  Examples: relaxing fail-closed, adding fixed waits, deleting a failing
  assertion, or masking a perception gap with auxiliary data.

## Human-Readable Debugging Model

> A failure report describes the *final symptom*. The actual divergence from
> reality happened earlier. Before collecting evidence or touching code, build
> a human-readable model of what the real world did, what the system believed,
> and where they first parted.

### Analysis Flow

```
Expected Reality
        ↓
Observed Reality
        ↓
Reality Gap
        ↓
Evidence Reference
        ↓
First Divergence Point
        ↓
Owner
        ↓
Minimal Change
```

### Reality Analysis Template

For complex failures (E2–E4), output the following before any code proposal:

#### Expected Reality

Describe what the system *should* have happened, in human language that a
user could understand. No code function names, no internal type names.

Wrong: `ResolveCurrentVisibleElement` should return a valid occurrence.
Right: The system should confirm that the target button on screen is still
the same button it discovered earlier.

#### Observed Reality

Describe what actually happened. Facts only — no explanations, no root causes.

Example: The system reached the permission page, but the Runtime still
believed it had not entered an external page.

#### Reality Gap

The difference between expected and observed reality.

Example: The real device state changed, but the Runtime state judgment did
not synchronise.

#### Evidence Reference

Provide the location of existing evidence. Prefer cited analysis over fresh
guessing.

Evidence:
- Trace: path/to/file
- Observation: path/to/file
- Frame dump: path/to/file
- Environment state: path/to/file
- Test result: path/to/file
- Decision document: path/to/file

Rules:
1. When an existing analysis report is available, cite it — do not re-guess.
2. When no evidence exists, state exactly what is missing:
   Evidence missing — need to collect: trace, observation, state snapshot,
   action history.
3. Never write "Report says X, so fix X directly." The evidence must prove
   where the first divergence occurred.

#### First Divergence Point

Identify the step where the system's understanding of reality first
diverged from actual reality.

Forbidden: naming the final failure location.
Wrong: `ExternalBoundary` failed.
Right: The external page appeared, but `foreground` detection still returned
the old application state.

#### Owner

Determine responsibility — and state why:

- Agent
- Traversal
- Environment
- Device Adapter
- Semantic Capability
- Test Harness
- Vision Capability

#### Debugging Gate

For Runtime / FSM / Agent / Recovery problems, BEFORE confirming the First
Divergence Point, the following are FORBIDDEN:

- retry workaround（盲目重试绕过）
- timeout adjustment（调超时掩盖）
- fallback injection（注入回退）
- validation weakening（放宽校验）
- test modification（改测试隐藏）

Reason: prevent patching over the real problem — prove the divergence point
first, then make the minimal change.

## 1. Evidence Level (choose by task risk — NOT every task needs E4)

| level | risk | evidence required |
|-------|------|-------------------|
| E0 | compile / format / trivial edit | compiler error / message |
| E1 | unit test / local component | stack trace, assertion, input |
| E2 | stateful component / async flow | state snapshot, execution history, action/result sequence |
| E3 | Runtime / Agent / FSM / Traversal / Lifecycle | trace + state transition + observation + decision record |
| E4 | real device / integration / nondeterministic failure | trace timeline + observation frames + environment state + action history + reproduction context |

## 2. Scope

**Must be evidence-first**: Agent loop, FSM, Traversal, Runtime behavior,
Recovery, Async workflow, Real device, Flaky integration.

**Not required**: documentation, formatting, simple renames, mechanical DTO
changes.

## 3. Worker Execution Flow (complex tasks)

1. **Reality Understanding** — build the human-readable model:
   Describe Expected Reality → Observed Reality → Reality Gap →
   Evidence Reference → First Divergence Point → Owner (see
   Human-Readable Debugging Model above).
2. **Evidence Collection** — gather existing traces, observations, action
   history, ledgers, and environment state that prove or disprove the
   divergence point.
3. **Failure Classification** — classify the failure type; **prove it, do not
   assume**:
   Discovery / Grounding / Authorization / Execution / Recovery / Environment.
   Forbidden shortcuts: "Child missing ⇒ DFS bug"; "Element missing ⇒
   Semantic bug"; "Test fail ⇒ Production bug".
4. **Owner Judgment** — determine which seam owns the decision/state where
   the first divergence occurred (Agent / Container / Traversal / Environment
   / Semantic Capability / Test Harness / Vision Capability).
5. **Minimal Change** — propose the minimal change inside the owned seam.
6. **Invariant Validation** — verify invariants: authority, DFS ownership,
   GoalEvidence, no scenario knowledge.
7. **Regression** — run regression, including architecture guards and
   consistency checks.

## 4. Runtime Change Check

For any Runtime-core modification, output:

```
AuthorityDelta: NONE | CHANGED
ArchitectureDelta: NONE | ADDITIVE | BREAKING
```

And state whether the change affects: Agent authority, FSM, Traversal,
GoalEvidence, or introduces scenario knowledge.

## 5. Test Design

Tests verify CAPABILITY, not scripts. Forbidden: fixed click counts, fixed
ActionHistory, fixed page paths, fixed coordinates, fixed UI copy.

Recommended model:

```
EvidenceFixture + ExpectedSpecification
        ↓
  Runtime Execution
        ↓
  Evidence Evaluation
```

Verify: coverage, authorization, consistency, fail-closed, evidence
sufficiency.

## 6. Canonical Examples

### Case 1: Wrong Tap

- **Expected:** Clicking the target button navigates to the target page.
- **Observed:** The click navigated to a different page.
- **Reality Gap:** The action was dispatched, but the target element was not
  the intended one.
- **Evidence needed:** action trace, observation frame, bounds freshness.
- **First Divergence:** Confirm whether the `Observation` used for the click
  still represented the current page.
- **Owner:** Observation / Grounding.

### Case 2: External Page Not Detected

- **Expected:** Clicking the permission entry navigates to the external
  permission page.
- **Observed:** The permission page appeared, but the Runtime judged it had
  not.
- **Reality Gap:** The real device state and the Runtime state disagreed.
- **Evidence needed:** external transition trace, foreground timeline,
  observation frames.
- **First Divergence:** `Foreground` state detection did not reflect the real
  external state.
- **Owner:** Environment detection.

### Case 3: OCR Failure After Scroll

- **Expected:** After scrolling, the system continues to recognise list
  content.
- **Observed:** After scrolling, the list could not be normalised.
- **Reality Gap:** The `Observation` produced by the action did not meet the
  requirements for subsequent understanding.
- **Evidence needed:** scroll trace, frame timeline, OCR result.
- **First Divergence:** Determine whether the problem originated from scroll
  motion, observation timing, or vision capability.
- **Owner:** Determined by evidence (scroll / Observation timing / Vision).

## 7. Review Checklist

Before finishing, run `.ai/reviews/change-review.md`:
Authority / Evidence / Boundary / Testing (Runtime / Test / Architecture
change types; the unified rules live in `.ai/development-protocol.md` §17).

## STOP

Stop immediately if the fix would require modifying Runtime architecture,
Authority, or Agent / FSM / Traversal ownership.
