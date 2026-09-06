# Flow tool destinations: changes/ + workitems/ (to-spec + to-tickets)

This repository does NOT use Matt's issue-tracker workflow (GitHub Issues,
Linear, Jira, or `.scratch/` tickets).

## Two durable surfaces, two tools

| Tool | Maps to | Output goes to |
|---|---|---|
| `/to-spec` | PERSIST state | `changes/<id>/state.md` (Change State format) |
| `/to-tickets` | PLAN state | `workitems/WI-<ID>.json` (WorkItem schema, one per ticket) |

## When `/to-spec` runs

Synthesizes the conversation into a **Change State** file. Section mapping:

| to-spec section | Change State equivalent |
|---|---|
| Problem Statement | Intent (WHAT/WHY) |
| Solution | Intent (approach summary) |
| User Stories | Acceptance criteria |
| Implementation Decisions | Decisions |
| Testing Decisions | Verification declaration (level + quad) |
| Out of Scope | Scope / Out of Scope |

Write directly into `changes/<change-id>/state.md` following the templates
in `changes/README.md`. Do NOT create a separate spec file.

## When `/to-tickets` runs

Breaks the approved Change State (or conversation) into **tracer-bullet
vertical-slice WorkItems**. Write each ticket as a WorkItem JSON file under
`workitems/` using `schemas/work-item.schema.json`.

Ticket-to-WorkItem field mapping:

| to-tickets concept | WorkItem field |
|---|---|
| Title | `objective` |
| "What to build" (end-to-end behaviour) | `objective` + `semantic_brief` |
| "Blocked by" (ticket numbers) | `dependencies` (WorkItem IDs) |
| Acceptance criteria | `acceptance` array |
| Vertical slice scoping | `scope.write` (paths this ticket touches) |
| Prefactoring constraint | `forbidden` (paths/behaviors excluded) |
| "Status: ready-for-agent" | `status: "pending"` (set at creation) |

Rules specific to this repository:

- One JSON file per ticket (`WI-<PARENT-ID>-<NN>.json`), numbered in
  dependency order (blockers first).
- Each ticket must be independently executable by a fresh SubAgent
  (self-contained context, no Leader conversation backfill needed).
- Wide refactors: follow to-tickets' expand–contract pattern; each batch
  is its own WorkItem with `dependencies` pointing at the expand ticket.
- Do NOT close or modify the parent Change State — WorkItems are transient
  delegation contracts, not the durable record.

## When a skill says "publish to the issue tracker"

to-spec → `changes/`; to-tickets → `workitems/`. See above.

## When a skill says "fetch the relevant ticket"

Read the WorkItem JSON at the referenced path, or the Change State file.

## Note

`triage` / `wayfinder` / `ask-matt` / `implement` are not installed.
No second task surface exists beyond `changes/` (durable) + `workitems/`
(transient delegation). Status transitions are governed by the Development
Flow (`.agents/skills/uniflow/SKILL.md`).
