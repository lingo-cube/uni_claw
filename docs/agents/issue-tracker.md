# Issue tracker: Change State in `changes/` (to-spec destination)

This repository does NOT use Matt's issue-tracker workflow (GitHub Issues,
Linear, Jira, or `.scratch/` tickets).

**UniFlow Change State** (`changes/<id>/state.md`) is the sole durable
WHAT/WHY/ACCEPTANCE record for every change. See `changes/README.md`
for the full state machine, tier templates, and resume protocol.

## When a skill says "publish to the issue tracker"

Write the output as a Change State file under `changes/<change-id>/state.md`
following the templates in `changes/README.md`.

**Do not publish or mirror WorkItems into GitHub Issues or `.scratch/`.**

## When `to-spec` runs

`/to-spec` synthesizes the current conversation into a structured document.
In this repository, its "spec" output maps to a **STANDARD or DECISION-HEAVY
Change State** file. The spec template's sections map as follows:

| to-spec template section | Change State equivalent |
|---|---|
| Problem Statement | Intent (WHAT/WHY) |
| Solution | Intent (approach summary) |
| User Stories | Acceptance criteria |
| Implementation Decisions | Decisions |
| Testing Decisions | Verification declaration (level + quad) |
| Out of Scope | Scope / Out of Scope |

`to-spec` should NOT create a separate spec file — it writes directly
into the Change State format.

## When a skill says "fetch the relevant ticket"

Read the Change State file at the referenced path, or ask for the change ID.
There is no ticket queue to scan.

## Note

`to-tickets` / `triage` / `wayfinder` are not installed; no second task
surface exists. Status transitions of dispatched WorkItems are governed by
the Development Flow (`.agents/skills/uniflow/SKILL.md`).
