---
name: openspec-archive-change
description: Archive a completed change in the experimental workflow. Use when the user wants to finalize and archive a change after implementation is complete.
license: MIT
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.3.1"
  authority: NONE
  compatibility: Requires openspec CLI.
---

Archive a completed change in the experimental workflow.

**Input**: Optionally specify a change name. If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **If no change name provided, prompt for selection**

   Run `openspec list --json` to get available changes. Request an explicit user selection through the Host's available interaction mechanism.

   Show only active changes (not already archived).
   Include the schema used for each change if available.

   **IMPORTANT**: Do NOT guess or auto-select a change. Always let the user choose.

2. **Check artifact completion status**

   Run `openspec status --change "<name>" --json` to check artifact completion.

   Parse the JSON to understand:
   - `schemaName`: The workflow being used
   - `artifacts`: List of artifacts with their status (`done` or other)

   **If any artifacts are not `done`:**
   - Display warning listing incomplete artifacts
   - Request explicit user confirmation through the available interaction mechanism
   - Proceed if user confirms

3. **Check task completion status**

   Read the tasks file (typically `tasks.md`) to check for incomplete tasks.

   Count tasks marked with `- [ ]` (incomplete) vs `- [x]` (complete).

   **If incomplete tasks found:**
   - Display warning showing count of incomplete tasks
   - Request explicit user confirmation through the available interaction mechanism
   - Proceed if user confirms

   **If no tasks file exists:** Proceed without task-related warning.

4. **Assess delta spec sync state**

   Check for delta specs at `openspec/changes/<name>/specs/`. If none exist, proceed without sync prompt.

   **If delta specs exist:**
   - Compare each delta spec with its corresponding main spec at `openspec/specs/<capability>/spec.md`
   - Determine what changes would be applied (adds, modifications, removals, renames)
   - Show a combined summary before prompting

   **Prompt options:**
   - If changes needed: "Sync now (recommended)", "Archive without syncing"
   - If already synced: "Archive now", "Sync anyway", "Cancel"

   If the user chooses sync, invoke the repository's available spec-sync workflow if it exists and is authorized, passing the change name and analyzed delta summary. Do not invent a missing workflow. Proceed according to the user's choice and repository lifecycle rules.

5. **Graduation Decision and Registry (mandatory)**

   The repository uses Knowledge System v1. Durable lifecycle conclusions live in
   `docs/decisions/` and are routed through `docs/decisions/index.md`; the removed legacy
   `docs/system/decisions/log.md` MUST NOT be recreated.

   1. Read `docs/knowledge-maintenance-policy.md`, `docs/knowledge-map.md`, the change's
      `design.md` / `tasks.md`, and any existing graduation receipt.
   2. Require an independent graduation conclusion before archive. A fully checked task list
      is implementation evidence, not self-graduation.
   3. If no receipt exists, propose `docs/decisions/<change>-graduation-decision.md` containing
      the buyer, exact claim boundary, validation evidence, falsifier result, deferred scope,
      and final lifecycle conclusion.
   4. **Present the proposed receipt and registry entry to the user before writing it.**
      Offer: "Write receipt and register (recommended)", "Review individually", or
      "Archive without receipt (warning)".
   5. If approved, write the receipt and add one source-linked entry to
      `docs/decisions/index.md`. Do not invent a numeric decision-log ID.
   6. If skipped, record the missing receipt as an archive warning.

6. **Knowledge and Documentation Confirmation (verification gate)**

   Verify documentation against the current knowledge system; do not restore removed
   four-layer/charter paths.

   1. Read `docs/knowledge-maintenance-policy.md`, `docs/knowledge-map.md`, the canonical
      architecture index, current snapshot, current gates, and task-relevant layer/pattern docs.
   2. For every behavior or contract changed by the implementation, verify that its canonical
      source or task-relevant documentation is current. Projections may only restate
      source-linked current state and MUST retain `Authority: NONE`.
   3. Present each relevant document as `Updated`, `No change needed`, or `Missing/stale`.
   4. If a required source or projection is missing/stale, offer "Update now (recommended)"
      or "Archive anyway (with warning)". Do not infer an architecture or lifecycle decision
      as ordinary documentation maintenance.
   5. After archive, update `docs/work/active/current-gates.md` and the latest snapshot only
      from the approved graduation receipt and actual archive inventory.

7. **Perform the archive**

   Create the archive directory if it doesn't exist:
   ```bash
   mkdir -p openspec/changes/archive
   ```

   Generate target name using current date: `YYYY-MM-DD-<change-name>`

   **Check if target already exists:**
   - If yes: Fail with error, suggest renaming existing archive or using different date
   - If no: Move the change directory to archive

   ```bash
   mv openspec/changes/<name> openspec/changes/archive/YYYY-MM-DD-<name>
   ```

8. **Display summary**

   Show archive completion summary including:
   - Change name
   - Schema that was used
   - Archive location
   - Whether specs were synced (if applicable)
   - **Graduation receipt** (decision path + registry status, or "skipped")
   - **Knowledge/doc verification status** (✅ current / ❌ gaps flagged)
   - Note about any warnings (incomplete artifacts/tasks/docs)

**Output On Success**

```
## Archive Complete

**Change:** <change-name>
**Schema:** <schema-name>
**Archived to:** openspec/changes/archive/YYYY-MM-DD-<name>/
**Specs:** ✓ Synced to main specs (or "No delta specs" or "Sync skipped")
**Decision:** docs/decisions/<change>-graduation-decision.md registered (or "skipped")
**Docs:** ✅ Knowledge/current-state sources verified (or "❌ gaps flagged, see warnings")

All artifacts complete. All tasks complete.
```

**Guardrails**
- Always prompt for change selection if not provided
- Use artifact graph (openspec status --json) for completion checking
- Don't block archive on warnings - just inform and confirm
- Preserve .openspec.yaml when moving to archive (it moves with the directory)
- Show clear summary of what happened
- If sync is requested, use openspec-sync-specs approach (agent-driven)
- If delta specs exist, always run the sync assessment and show the combined summary before prompting
- Graduation receipt/registry confirmation (Step 5) MUST be offered before archive proceeds
- Knowledge/documentation confirmation (Step 6) MUST be checked — flag any gaps in summary
