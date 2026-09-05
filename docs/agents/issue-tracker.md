# Issue tracker: UniFlow WorkItems

Issues and specs for this repo live as UniFlow WorkItems in `workitems/`, one JSON
file per item (`workitems/WI-<ID>.json`), validated by
`schemas/work-item.schema.json`.

## Conventions

- Publishing a ticket = creating a WorkItem file with a unique `WI-*` id; status
  lives inside the file (`pending | in_progress | done | blocked | rejected`).
- Fetching a ticket = reading the WorkItem at the referenced path or id.
- Dependencies are explicit `dependencies` ids; a WorkItem is unblocked when all
  of them are `done`.
- Acceptance criteria are part of the WorkItem (`acceptance`); completion
  evidence goes to `evidence/<WI-ID>/`.

## When a skill says "publish to the issue tracker"

Create or update a WorkItem JSON file under `workitems/` (see `workitems/README.md`).

## When a skill says "fetch the relevant ticket"

Read the WorkItem file at the referenced path or id.

## Note

The triage-label vocabulary does not apply (the `triage` skill is not
installed). Status transitions are governed by UniFlow (`uniflow.md`), not by
label strings.
