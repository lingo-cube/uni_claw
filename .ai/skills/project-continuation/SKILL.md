---
name: project-continuation
description: Resume a long-running project from current projections and evidence while keeping historical material on demand.
metadata:
  type: Project Continuation Skill
  authority: NONE
---

# Project Continuation

Use this skill to restore the minimum working context for an existing project. It does not establish project state or choose an architectural direction.

## Startup Order

1. Latest snapshot
2. Current architecture state
3. Current gates
4. Active work
5. Required evidence

## Output

- Current State
- Current Goal
- Current Blocker
- Next Action
- Required Context

## Boundaries

- Do not scan all historical material.
- Do not restore current state from an old Decision.
- Do not decide the next architectural direction automatically.
- When a proposed Next Action lacks current source support, report it as unresolved rather than selecting it.
