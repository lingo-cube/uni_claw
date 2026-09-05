# Issue tracker: none (plans + git); WorkItems are dispatch payloads, not tickets

This repo has no standing issue tracker or ticket system.

- Durable work intent lives in `plans/` (Plan artifacts) and git history.
- A UniFlow WorkItem is a **Leader→SubAgent dispatch protocol payload**
  (`schemas/work-item.schema.json`), materialized under `workitems/` only
  when work is actually delegated to a fresh subagent context. It is not a
  task tracker: direct work never creates one.

## When a skill says "publish to the issue tracker"

Record the work as a Plan artifact under `plans/` (if it outlives the session)
or express it in the commit itself. Do not create a WorkItem unless the work
is being delegated to a subagent.

## When a skill says "fetch the relevant ticket"

Ask the user for the plan/document reference or the WorkItem id. There is no
ticket queue to scan.

## Note

The triage-label vocabulary does not apply (the `triage` skill is not
installed). Status transitions of dispatched WorkItems are governed by UniFlow
(`uniflow.md`).
