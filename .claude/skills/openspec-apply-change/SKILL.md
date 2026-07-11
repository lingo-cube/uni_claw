---
name: openspec-apply-change
description: Implement tasks from an OpenSpec change. Use when the user wants to start implementing, continue implementation, or work through tasks.
license: MIT
compatibility: Requires openspec CLI.
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.3.1"
---

Implement tasks from an OpenSpec change.

**Input**: Optionally specify a change name. If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **Select the change**

   If a name is provided, use it. Otherwise:
   - Infer from conversation context if the user mentioned a change
   - Auto-select if only one active change exists
   - If ambiguous, run `openspec list --json` to get available changes and use the **AskUserQuestion tool** to let the user select

   Always announce: "Using change: <name>" and how to override (e.g., `/opsx:apply <other>`).

2. **Check status to understand the schema**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to understand:
   - `schemaName`: The workflow being used (e.g., "spec-driven")
   - Which artifact contains the tasks (typically "tasks" for spec-driven, check status for others)

3. **Get apply instructions**

   ```bash
   openspec instructions apply --change "<name>" --json
   ```

   This returns:
   - `contextFiles`: artifact ID -> array of concrete file paths (varies by schema - could be proposal/specs/design/tasks or spec/tests/implementation/docs)
   - Progress (total, complete, remaining)
   - Task list with status
   - Dynamic instruction based on current state

   **Handle states:**
   - If `state: "blocked"` (missing artifacts): show message, suggest using openspec-continue-change
   - If `state: "all_done"`: congratulate, suggest archive
   - Otherwise: proceed to implementation

4. **Read context files**

   Read every file path listed under `contextFiles` from the apply instructions output.
   The files depend on the schema being used:
   - **spec-driven**: proposal, specs, design, tasks
   - Other schemas: follow the contextFiles from CLI output

5. **Show current progress**

   Display:
   - Schema being used
   - Progress: "N/M tasks complete"
   - Remaining tasks overview
   - Dynamic instruction from CLI

6. **Implement tasks (loop until done or blocked)**

   For each pending task:
   - Show which task is being worked on
   - Make the code changes required
   - Keep changes minimal and focused
   - Mark task complete in the tasks file: `- [ ]` → `- [x]`
   - Continue to next task

   **Pause if:**
   - Task is unclear → ask for clarification
   - Implementation reveals a design issue → suggest updating artifacts
   - Error or blocker encountered → report and wait for guidance
   - User interrupts

7. **Four-Layer Documentation Sync** (mandatory checkpoint — NOT optional)

   This step is a **hard checkpoint**: it MUST be completed before proceeding to Step 8.
   It cannot be skipped or deferred. If documentation is not synced, the change is NOT complete.

   Read `docs/system/charter-specification.md` §5.6 for the four-layer documentation sync
   responsibility mapping table. For each code change made in this session, systematically
   check the mapping table and produce a **sync checklist** listing every affected document.

   **Produce the checklist first** (do NOT start editing docs before listing what needs updating):

   For each code change in this session, determine affected tiers:

   - **Tier 1 (Constitution)**: Did any locked enum value count change? → Update
     `docs/system/constitution/locked-enums.md` AND `docs/system/charter-specification.md`
     §2.2 + §6.1. Did any interface method count lock change? → Same update.
   - **Tier 2 (Patterns)**: Did any Handler/dispatch/FSM behavior change? → Update
     corresponding `docs/system/patterns/*.md`. Did TraceCoordinator method mapping change?
     → Update `docs/system/patterns/dispatch-table.md`.
   - **Tier 3 (Layers)**: Did any type list, field list, or dependency change in a layer? →
     Update `docs/system/layers/<affected-layer>.md`. New layer created? → Create new layer doc.
   - **Tier 4 (Decisions)**: Do NOT update `docs/system/decisions/log.md` here — this is
     handled at **archive time** from Decisions Extract (Step 4b in archive skill).
   - **OpenSpec main specs**: Do NOT update `openspec/specs/` here — this is handled at
     archive time via delta spec sync.

   **Present the checklist to the user** before making any doc edits. Format:

   ```
   ## Documentation Sync Checklist

   | Tier | Document | Action Required | Reason |
   |------|----------|----------------|--------|
   | T1 | constitution/locked-enums.md | Update: add X enum | New enum Y added with Z values |
   | T2 | patterns/dispatch-table.md | Update: add instance | TraceCoordinator method mapping changed |
   | T3 | layers/X.md | Update: type inventory | 3 new types added to layer X |
   | T3 | layers/Y.md | Create | New layer created — no doc exists yet |
   | T4 | decisions/log.md | Skip (archive time) | D-{next} extracted at archive |
   ```

   For each row marked "Update" or "Create", make the documented changes now.
   For each row marked "No change needed", briefly note why (e.g., "enum count unchanged").
   Rows marked "Skip (archive time)" are deferred — do NOT attempt them here.

   **Completion gate**: All Tier 1/2/3 rows must have either "Updated" or "No change needed"
   status before proceeding. If any row is still "Update" or "Create" and not yet done,
   STOP and complete it. Do NOT mark the change as "all tasks complete" until this gate passes.

   Also run the general doc sync hook:
   ```bash
   python openspec/hooks/doc_sync_hook.py
   ```

   If doc sync issues are found, add tasks to update the relevant design docs before marking complete.

8. **On completion or pause, show status**

   Display:
   - Tasks completed this session
   - Overall progress: "N/M tasks complete"
   - If all done: suggest archive
   - If paused: explain why and wait for guidance

**Output During Implementation**

```
## Implementing: <change-name> (schema: <schema-name>)

Working on task 3/7: <task description>
[...implementation happening...]
✓ Task complete

Working on task 4/7: <task description>
[...implementation happening...]
✓ Task complete
```

**Output On Completion**

```
## Implementation Complete

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 7/7 tasks complete ✓

### Completed This Session
- [x] Task 1
- [x] Task 2
...

All tasks complete! Ready to archive this change.
```

**Output On Pause (Issue Encountered)**

```
## Implementation Paused

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 4/7 tasks complete

### Issue Encountered
<description of the issue>

**Options:**
1. <option 1>
2. <option 2>
3. Other approach

What would you like to do?
```

**Guardrails**
- Keep going through tasks until done or blocked
- Always read context files before starting (from the apply instructions output)
- If task is ambiguous, pause and ask before implementing
- If implementation reveals issues, pause and suggest artifact updates
- Keep code changes minimal and scoped to each task
- Update task checkbox immediately after completing each task
- Pause on errors, blockers, or unclear requirements - don't guess
- Use contextFiles from CLI output, don't assume specific file names

**Fluid Workflow Integration**

This skill supports the "actions on a change" model:

- **Can be invoked anytime**: Before all artifacts are done (if tasks exist), after partial implementation, interleaved with other actions
- **Allows artifact updates**: If implementation reveals design issues, suggest updating artifacts - not phase-locked, work fluidly
