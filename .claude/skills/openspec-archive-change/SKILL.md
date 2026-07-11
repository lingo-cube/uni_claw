---
name: openspec-archive-change
description: Archive a completed change in the experimental workflow. Use when the user wants to finalize and archive a change after implementation is complete.
license: MIT
compatibility: Requires openspec CLI.
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.3.1"
---

Archive a completed change in the experimental workflow.

**Input**: Optionally specify a change name. If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **If no change name provided, prompt for selection**

   Run `openspec list --json` to get available changes. Use the **AskUserQuestion tool** to let the user select.

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
   - Use **AskUserQuestion tool** to confirm user wants to proceed
   - Proceed if user confirms

3. **Check task completion status**

   Read the tasks file (typically `tasks.md`) to check for incomplete tasks.

   Count tasks marked with `- [ ]` (incomplete) vs `- [x]` (complete).

   **If incomplete tasks found:**
   - Display warning showing count of incomplete tasks
   - Use **AskUserQuestion tool** to confirm user wants to proceed
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

   If user chooses sync, use Task tool (subagent_type: "general-purpose", prompt: "Use Skill tool to invoke openspec-sync-specs for change '<name>'. Delta spec analysis: <include the analyzed delta spec summary>"). Proceed to archive regardless of choice.

5. **Decisions Extract (Tier 4 — mandatory)**

   This step extracts architectural decisions from the change's design document and appends
   them to `docs/system/decisions/log.md`. It MUST be completed before the archive proceeds.

   **Extract decisions from design.md:**

   1. Read `openspec/changes/<name>/design.md` and identify all architectural decisions
      (marked as D1, D2, etc. or described as choice/rationale pairs).
   2. Read `docs/system/decisions/log.md` to find the last D-{id} number.
   3. For each design decision, format it as a D-{next_id} entry following the log.md format:

      ```
      ### D-{id} | {date} | {title}

      Decision: {one-line conclusion — what AI must follow}
      Rationale: {why — 1-2 sentences}
      Source: openspec:{change-name}
      Ref: {path to affected source files}
      Guard: {GuardTestName} | 无 (convention-level)
      Commit: pending
      Status: Locked | Fixed
      ```

   4. **Present the proposed decisions to the user** before appending. Format:

      ```
      ## Proposed Decisions Extract

      | ID | Title | Status | Guard |
      |----|-------|--------|-------|
      | D-{next} | ... | Locked | XxxGuard |
      | D-{next+1} | ... | Fixed | 无 |

      Append to docs/system/decisions/log.md? [Yes / Review individually / Skip]
      ```

   5. If user approves, append the decisions to `docs/system/decisions/log.md`.
   6. If user skips, note this in the archive summary as a warning.

6. **Four-Layer Documentation Confirmation (Tier 1/2/3 — verification gate)**

   This step **verifies** that the four-layer documentation was already updated during the
   apply phase (Step 7). It does NOT re-do the updates — it checks that they were done.

   1. Read `docs/system/charter-specification.md` §5.6 for the sync responsibility mapping.
   2. For each code change made in the change, check whether the affected Tier 1/2/3 documents
      have been updated since the change was applied (compare file modification dates or
      content).

   3. **Present the verification result**:

      ```
      ## Documentation Sync Verification

      | Tier | Document | Status | Detail |
      |------|----------|--------|--------|
      | T1 | constitution/locked-enums.md | ✅ Updated | ErrorSeverity + ITraceRecorder 7-method lock added |
      | T2 | patterns/dispatch-table.md | ✅ Updated | TraceCoordinator instance + method mapping table added |
      | T3 | layers/observability.md | ✅ Created | New layer doc for Observability |
      | T3 | layers/traversal.md | ❌ NOT updated | TraceCoordinator section still says "15/16 empty lambdas" |
      ```

   4. **If any Tier 1/2/3 document is NOT updated**: WARN the user and offer two options:
      - "Update now (recommended)" — pause archive, update the missing docs, then continue
      - "Archive anyway (with warning)" — proceed but flag the gap in the archive summary

   5. If user chooses "Update now", update the missing documents before proceeding to Step 7.
   6. Tier 4 (decisions/log.md) is handled in Step 5 above — do NOT double-check it here.

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
   - **Decisions extracted** (D-{id} range appended to log.md, or "skipped")
   - **Four-layer doc verification status** (✅ all updated / ❌ gaps flagged)
   - Note about any warnings (incomplete artifacts/tasks/docs)

**Output On Success**

```
## Archive Complete

**Change:** <change-name>
**Schema:** <schema-name>
**Archived to:** openspec/changes/archive/YYYY-MM-DD-<name>/
**Specs:** ✓ Synced to main specs (or "No delta specs" or "Sync skipped")
**Decisions:** D-{id1}~D-{idN} appended to decisions/log.md (or "skipped")
**Docs:** ✅ All Tier 1/2/3 documents verified (or "❌ gaps flagged, see warnings")

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
- Decisions Extract (Step 5) MUST be offered before archive proceeds — do not skip
- Four-Layer Documentation Confirmation (Step 6) MUST be checked — flag any gaps in summary
