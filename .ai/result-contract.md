# Result Contract

> Platform: Codex + Claude | Source: .ai/development-protocol.md
> This is the shared output interface for all AI Coding Agent task results.
> Claude agents return equivalent structured output; Codex returns this format inline.

---

## Successful Result (DONE)

```markdown
TASK_RESULT

Task:            <Task ID>
Role:            <portable role>
Status:          DONE

Scenario:        <SC-Px-xxx + Evidence Required fulfilled>

Changes:
  - <file:line — summary of change>
  - ...

Production Delta:
  - <new or modified production artifact + Scenario Receipt reference>
  - NONE (if test-only)

Scenario Receipt:
  - <existing receipt reference or N/A>

Tests:
  - <test file + count of new tests>
  - <dotnet test result: N/N PASS>

Build:
  - <dotnet build result: 0 warnings, 0 errors>

Guards:
  - <Architecture Guard result: N/N PASS>

Consistency:
  - <scripts/check-consistency.sh result: ALL PASS>

Deferred Boundary:
  - <confirmed: no deferred capability leaked>
  - <or: deferred items remain absent>

Unexpected Findings:
  - NONE
  - <or: specific finding + resolution>
```

---

## Blocked Result

```markdown
TASK_RESULT

Task:            <Task ID>
Role:            <portable role>
Status:          BLOCKED_FOR_SPEC
               | BLOCKED_FOR_SEMANTIC_REVIEW
               | BLOCKED_FOR_ARCHITECTURE_REVIEW
               | BLOCKED_FOR_HUMAN
               | ROUTING_UNAVAILABLE

Reason:          <concrete reason the task cannot proceed>

Repository Evidence:
  - <file:line — evidence supporting the block>
  - ...

Required Decision:
  - <what must be decided before retry>
  - <who should decide>

Production Changes:
  NONE

Do not propose speculative implementation unless explicitly useful for reviewer context.
```

---

## Validator Result

```markdown
VALIDATION_RESULT

Phase / Slice:   <phase or slice identifier>
Role:            runtime-validator
Verdict:         PASS | CONDITIONAL_PASS | FAIL

Scenario Verification:
  - <SC-Px-xxx: SATISFIED / FAILED — evidence>
  - ...

Semantic Verification:
  - <Required Semantic / Goal Evidence / Observation boundary check results>

Architecture Verification:
  - <invariant / ownership / authority / dependency direction / leakage check results>

Spec Consistency:
  - <OpenSpec / design / tasks / implementation alignment>

Phase Boundary Audit:
  - <boundary not breached; deferred remains deferred>

Verification Evidence:
  - <actual commands run + output>
  - <files read>

Violations:
  - <per violation: what / where / classification>

Failure Classification:
  IMPLEMENTATION | TEST_HARNESS | SPEC | SEMANTIC | ARCHITECTURE

Required Follow-up:
  - <next actions for the scheduler>
```

---

## Phase Controller Result

```markdown
PHASE_CONTROLLER_RESULT

Phase:           <phase identifier>
Status:          SLICE_DONE | PHASE_DONE | HUMAN_GATE | ROUTING_UNAVAILABLE

Completed Tasks:
  - <task ID + status>
  - ...

Active Scenarios:
  - <SC-Px-xxx: SATISFIED / PENDING>
  - ...

Next Action:
  Type: DISPATCH | REROUTE | HUMAN_GATE | SLICE_DONE | PHASE_DONE
  Detail: <complete Task Contract if DISPATCH; route/decision if other>

Verification Summary:
  - build: <result>
  - tests: <N/N PASS>
  - guards: <N/N PASS>
  - consistency: <result>
```

---

## Consistency Rules

1. `Status` must be exactly one of the defined values. No custom status strings.
2. `DONE` must be accompanied by verification evidence (build/tests/guards/consistency).
3. `BLOCKED_FOR_*` must include concrete Reason + Repository Evidence. Never return blocked without proof.
4. `TASK_RESULT` with `Production Changes: NONE` + `Status: DONE` is valid for test-only or documentation tasks.
5. `VALIDATION_RESULT` must include independent verification evidence — not just acceptance of a prior coder report.
6. Contracts are shared: Claude coder output and Codex coder output use the same format.
