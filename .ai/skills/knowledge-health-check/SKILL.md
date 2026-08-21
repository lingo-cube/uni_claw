---
name: knowledge-health-check
description: Perform a read-only audit of a project's decision registry, process skills, current-state projections, and context-loading routes after knowledge-structure changes, around freeze or adoption, during periodic validation, or when drift is suspected; do not use for ordinary development, editing, or architecture adjudication.
metadata:
  type: Knowledge Health Check Skill
  authority: NONE
---

# Knowledge Health Check

Use this skill to detect knowledge-maintenance risks and report them. It is read-only: do not repair findings, establish authority, or decide unresolved facts.

## When to Use

Use this skill when at least one of these applies:

- A decision registry, skill definition, projection, snapshot, or context-routing document changed.
- A knowledge system is being frozen, adopted, or independently revalidated.
- A periodic knowledge-health audit is requested.
- Broken references, context drift, duplicated skill responsibility, or project facts in a skill are suspected.

## Do Not Use

Do not trigger this skill for ordinary feature or Runtime work, routine tests, a single content edit with no knowledge-structure impact, architecture correctness review, or implementation and repair work. A health finding does not authorize a modification.

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

## Audit Loop

1. State the trigger and select the smallest relevant input scope.
2. Capture a read-only baseline for that scope.
3. Run the applicable checks without editing any input.
4. Classify every finding using only the failure classes below.
5. Emit the required report with evidence and a bounded next action.
6. Close the audit according to its status:
   - `PASS`: report `RequiredAction: NONE`; the audit is complete.
   - `WARN`: report the metadata or maintenance action that would require separate authorization and name the exact scope to recheck.
   - `FAIL`: report the blocking defect and the separately authorized repair and recheck scope; do not perform the repair.
   - Stop condition: output `ARCHITECTURE_DECISION_REQUIRED`; do not select a repair or continue the audit by guessing.

After an independently authorized correction is completed, rerun only the stated recheck scope. End when it passes or when a remaining finding requires a new authorization or architecture decision; do not broaden or repeat the loop automatically.

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

`RequiredAction` must be `NONE` for `PASS`. For `WARN` or `FAIL`, it must identify the separate authorization needed and the exact recheck scope, without applying the change.

## Boundaries and Stop Conditions

- Do not auto-fix any finding.
- Do not infer a missing status, category, or Current Reference.
- If sources conflict, authority or lifecycle is unclear, or a correction would require factual inference, output `ARCHITECTURE_DECISION_REQUIRED` and stop.
