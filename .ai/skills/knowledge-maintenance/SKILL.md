---
name: knowledge-maintenance
description: Maintain long-lived project knowledge by separating current state, reusable process guidance, and historical evidence without changing facts or governance.
metadata:
  type: Knowledge Maintenance Skill
  authority: NONE
---

# Knowledge Maintenance

Use this skill when accumulated knowledge makes current work harder to understand or retrieve. Apply the principle: Current State > Historical Process. This is a maintenance method, not an authority source or a substitute for architecture documentation.

## Trigger Conditions

Use when one or more of the following occurs:

- Decision volume continues to grow.
- Default context becomes large.
- Historical process affects current judgment.
- The same issue is analyzed repeatedly.
- Current State is unclear.

## Maintenance Process

### Step 1: Audit Current State

Confirm:

- the current-state entry point;
- the current work state; and
- the current context structure.

### Step 2: Identify Repeated Knowledge

Look for high-frequency repeated processes, stable methods, and general rules. Evaluate whether each candidate is suitable for a reusable skill.

### Step 3: Evaluate Knowledge Placement

Classify knowledge by purpose:

- Skill: general processes, operating methods, and retrieval rules.
- Decision: architecture judgments, design choices, and authority conclusions.
- Documentation: current state, indexes, and projections.

### Step 4: Update Knowledge Structure

Allowed operations may include creating an index, projection, or skill when authorized. Do not delete history, modify facts, or change authority.

### Step 5: Validate Safety

Check:

- Authority: does the result create a new authority?
- Traceability: are sources preserved?
- Context: does it reduce default loading?
- Rollback: can the change be safely reversed?

## Required Output

```text
MaintenanceNeed:
AffectedKnowledge:
RecommendedAction:
Risk:
```

## Boundaries

- Do not delete Decisions.
- Do not merge historical records.
- Do not modify Architecture Authority.
- Do not automatically change Lifecycle.
- Do not automatically close a Gate.
