---
name: knowledge-health-check
description: Perform a read-only health audit of a project's decision registry, process skills, current-state projections, and context-loading routes; use for periodic knowledge-system validation, not editing or architecture adjudication.
metadata:
  type: Knowledge Health Check Skill
  authority: NONE
---

# Knowledge Health Check

Use this skill to detect knowledge-maintenance risks and report them. It is read-only: do not repair findings, establish authority, or decide unresolved facts.

## Inputs

- A decision registry.
- Skill directories.
- Current-state projections and snapshots.
- Context-routing and context-loading documentation.

## Checks

### Decision Registry

- Confirm every record has a present, unique ID.
- Confirm Category is present, or explicitly undeclared for a legacy record.
- Confirm Current Reference is resolved or explicitly `UNDECLARED`.
- Confirm each declared source path exists.

### Skills

- Confirm YAML frontmatter is valid.
- Confirm `Authority: NONE`.
- Detect project facts.
- Detect substantial responsibility duplication with another skill.

### Projections

- Confirm `Authority: NONE`.
- Confirm sources are linked.
- Detect independent SHALL statements.
- Detect independent lifecycle conclusions.

### Context

- Confirm default loading excludes the complete historical record.
- Detect isolated documents that claim a competing default entry point.

## Failure Classes

Use only these classes:

- `METADATA_GAP`
- `BROKEN_REFERENCE`
- `AUTHORITY_RISK`
- `LIFECYCLE_RISK`
- `CONTEXT_DRIFT`
- `DUPLICATE_SKILL`
- `PROJECT_FACT_IN_SKILL`

## Required Output

```text
Status: PASS | WARN | FAIL
Counts:
Findings:
  - Class:
    Location:
    Evidence:
RequiredAction:
```

## Boundaries and Stop Conditions

- Do not auto-fix any finding.
- Do not infer a missing status, category, or Current Reference.
- If sources conflict, authority or lifecycle is unclear, or a correction would require factual inference, output `ARCHITECTURE_DECISION_REQUIRED` and stop.
