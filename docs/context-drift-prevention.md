# Context Drift Prevention

DocumentType: CONTEXT_DRIFT_PREVENTION_GUIDE  
Authority: NONE  
Scope: Knowledge-loading and metadata maintenance only. This guide does not
replace Architecture, Decisions, Lifecycle, or any source authority.

## Default Context

Load Current State by following the minimum loading order in the Context
Loading Guide. Do not expand the default context into the complete historical
record.

## Historical Retrieval

Retrieve historical material only when traceability, evidence, a predecessor,
or a failure record is required for the task.

## New Decision Metadata

Each new Decision record must explicitly declare:

- `Purpose`
- `Category`
- `Current Reference`

If a current reference has not been confirmed, record `UNDECLARED`. Do not
guess a successor, status, or lifecycle state.

## New Skill Checks

Each new Skill must demonstrate:

- `Reusable`
- `Authority: NONE`
- `No project facts`

Before creating it, check whether its stable process already overlaps an
existing Skill.

## New Projection and Snapshot Checks

A new Projection must be source linked, declare `Authority: NONE`, and contain
no independent `SHALL` statement or lifecycle judgment.

A new Snapshot must contain only source-referenced current state and must not
carry historical narrative or create new authority.

## Read-only Health Check

Health checks discover and report issues; they do not automatically repair
them.

Check the following:

- Decision Registry: ID, Category, Current Reference, and source-path validity.
- Skill: YAML frontmatter, `Authority: NONE`, no project facts, and no
  responsibility duplication.
- Projection: `Authority: NONE`, source links, no new `SHALL` statement, and
  no independent lifecycle judgment.
- Context: the default context excludes the full historical record and no
  isolated entry claims to be the default context.

Do not infer a missing legacy Category or an unconfirmed Current Reference.
Report these as `METADATA_GAP` or `UNDECLARED`.

Use only these failure classes:

- `METADATA_GAP`
- `BROKEN_REFERENCE`
- `AUTHORITY_RISK`
- `LIFECYCLE_RISK`
- `CONTEXT_DRIFT`
- `DUPLICATE_SKILL`
- `PROJECT_FACT_IN_SKILL`

Use this report format:

- `Status`: `PASS`, `WARN`, or `FAIL`
- `Counts`
- `Findings`: `Class`, `Location`, `Evidence`
- `RequiredAction`

If sources conflict, authority or lifecycle is unclear, or a repair would
require inferred facts, stop and output `ARCHITECTURE_DECISION_REQUIRED`. Do
not auto-fix.

## Stop Condition

If authority or lifecycle is unclear, or sources conflict, stop and output
`ARCHITECTURE_DECISION_REQUIRED`.
