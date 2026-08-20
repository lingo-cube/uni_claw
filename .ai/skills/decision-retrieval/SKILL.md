---
name: decision-retrieval
description: Retrieve a specific historical decision through an evidence-led, scoped search without treating history as current architecture.
metadata:
  type: Knowledge Retrieval Skill
  authority: NONE
---

# Decision Retrieval

Use this skill when a task needs a historical decision as traceability evidence. Retrieve only the decision path needed by the question.

## Retrieval Flow

Question

↓

Domain

↓

Capability

↓

Gate / Scenario

↓

Evidence

↓

Decision

## Output

- Decision ID
- Title
- Status
- Current Reference
- Loading Reason

## Boundaries

- Do not use a historical Decision in place of Current Architecture.
- Do not infer status from a filename.
- Do not load all historical Decisions.
- Report only status explicitly stated by the source; when it is absent, keep it undeclared.
