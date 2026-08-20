---
name: task-classification
description: Classify a request as architecture, protocol, implementation, investigation, documentation, or research, then identify the minimum appropriate context without making decisions or changes.
metadata:
  type: Task Routing Skill
  authority: NONE
---

# Task Classification

Use this skill to identify the request type before loading context or taking action. Classification routes work; it does not judge architecture, create decisions, choose an implementation, or authorize file changes.

## Inputs

- User request
- Stated task scope
- Known domain, if provided
- Requested change type, if provided

## Classify the Task

### Architecture Task

Use for architecture design, authority, ownership, or component-boundary questions.

- Required context: Architecture Context and Current Architecture State.
- Next step: establish the relevant decision process before any implementation.
- Forbidden: entering code modification directly.

### Protocol Task

Use for protocol work or domain-contract work.

- Required context: Protocol and Domain Contract; load Architecture Context first when it is needed to determine the applicable boundary.
- Next step: confirm the approved scope before changing protocol-related material.
- Forbidden: elevating protocol work into Architecture Authority or bypassing the approved scope.

### Implementation Task

Use for feature implementation, an OpenSpec change, or Runtime modification.

- Required context: Architecture Context, Domain Contract, Runtime Contract, and Relevant Active OpenSpec.
- Next step: confirm the approved implementation scope before editing.
- Forbidden: treating an incomplete request as an approved design.

### Bug Investigation Task

Use for a failure, regression, or unexpected behavior.

- Required context: Evidence, Failure Records, and Relevant Historical Evidence.
- Flow: Evidence first → Root cause isolation → Minimal repair.
- Next step: report the isolated cause and required authorization; diagnosis does not automatically authorize modification.
- Forbidden: assuming a repair from symptoms alone.

### Documentation Task

Use for document organization, indexing, or knowledge maintenance.

- Required context: Documentation Safety and Current Projection.
- Next step: confirm the requested document operation and its evidence source.
- Forbidden: modifying fact sources.

### Research Task

Use for external research, technical investigation, or comparison analysis.

- Required context: Research Context and Required Evidence.
- Next step: define the question, evidence standard, and comparison criteria.
- Forbidden: presenting research as an architecture decision.

## Required Output

```text
TaskType:
RequiredContext:
ForbiddenContext:
NextStep:
```

## Boundaries

- Do not judge whether an architecture is correct.
- Do not create an Architecture Decision.
- Do not modify lifecycle state.
- Do not automatically select an implementation approach.
- Do not automatically modify files.
- Do not load all historical material by default.
- Do not use file quantity as a signal of importance.
- Do not determine authority automatically.
- When authority or source-precedence conflicts, output `ARCHITECTURE_DECISION_REQUIRED` and STOP.
