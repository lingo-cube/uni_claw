# Issue tracker: none

This repository does not use Matt's issue-tracker workflow.

UniFlow WorkItems are transient Leader-to-SubAgent delegation contracts,
not an issue tracker or backlog.

**Do not publish or mirror WorkItems into GitHub Issues or `.scratch/`.**

## Where work intent actually lives

- Durable work intent: `plans/` (Plan artifacts) and git history.
- Architecture decisions: `docs/adr/`.
- Dispatched delegation payloads: `workitems/` (only when the Leader actually
  delegates to a fresh subagent context; transient by design — see
  `docs/adr/0002-workitem-is-dispatch-protocol.md`).

## When a skill says "publish to the issue tracker"

Record the work as a Plan artifact under `plans/` (if it outlives the session)
or express it in the commit itself. Do not create a WorkItem unless the work
is being delegated to a subagent.

## When a skill says "fetch the relevant ticket"

Ask the user for the plan/document reference or the WorkItem id. There is no
ticket queue to scan.

## Note

`to-tickets` / `triage` / `wayfinder` are not installed; no second task
surface exists. Status transitions of dispatched WorkItems are governed by
UniFlow (`.agents/skills/uniflow/SKILL.md`).
