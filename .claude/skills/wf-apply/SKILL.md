---
name: wf-apply
description: Workflow-driven task execution - combines openspec with intelligent Haiku/Opus routing
license: MIT
compatibility: Requires openspec CLI
metadata:
  author: uni-claw
  version: "2.0"
  workflow: self-driven-task-execution-final
---

Workflow-driven task execution with intelligent model routing.

**Input**: Change name (optional, will infer from context)

**Key Features**
- **Smart Routing**: Learns which model (Haiku/Opus) works best for each task type
- **Cost Optimization**: Uses Haiku for simple tasks, Opus for complex ones
- **Fast Verification**: 2×Haiku + 1×Sonnet for verification
- **Mixed Battle**: 1×Haiku + 1×Sonnet for adversarial validation
- **Issue Tracking**: Auto-generates ISSUES documents for failed tasks

**Steps**

1. **Select the change**

   If no name provided:
   - Infer from conversation context
   - Auto-select if only one active change
   - If ambiguous, run `openspec list --json` and use **AskUserQuestion** to select

   Announce: "Using change: <name>"

2. **Pre-check change status**

   ```bash
   openspec instructions apply --change "<name>" --json
   ```

   Handle states:
   - `state: "blocked"` → Show message, suggest openspec-continue-change, exit
   - `state: "all_done"` → Congratulate, suggest archive, exit
   - Otherwise → Proceed to workflow

3. **Invoke Self-Driven Workflow**

   Call the optimized workflow (it will handle all tasks internally):
   ```
   /Workflow self-driven-task-execution-final <change-name>
   ```

   The workflow will:
   - Fetch all tasks via openspec
   - Loop through each task with smart routing (Haiku vs Opus)
   - Execute with multi-agent verification
   - Generate issues if failures occur
   - Mark complete tasks
   - Return summary and routing stats

4. **Post-execution sync**

   When workflow completes, check if design docs need updates:
   ```bash
   python openspec/hooks/doc_sync_hook.py
   ```

   If issues found, mention them in the summary.

5. **Show summary**

   Display:
   - Tasks completed this session
   - Overall progress
   - Issues generated (if any)
   - Routing memory statistics
   - Next steps (review issues, archive, etc.)

**Output Example**

```
## WF-Apply: feature-a

📋 Using change: feature-a
✓ Change status: OK (5 pending tasks)

[Workflow output...]

📊 Results:
- Tasks completed: 5
- Tasks remaining: 0
- Issues generated: docs/issues/FEATURE_A_ISSUES_2025-01-15.md (2 issues)

🧠 Routing Memory:
- 测试: → Haiku (成功率 100%)
- 实现: → Opus (成功率 80%)

Next steps:
- Review the generated issues
- Run /opsx:archive feature-a when ready
```

**Why This Skill**

Combines workflow intelligence with openspec task management:

| Feature | opsx:apply | wf-apply |
|---------|-----------|----------|
| Task fetching | ✅ | ✅ (via workflow) |
| Smart routing | ❌ | ✅ |
| Multi-agent verify | ❌ | ✅ |
| Issue tracking | ❌ | ✅ |
| Cost optimization | ❌ | ✅ |
| Doc sync | ✅ | ✅ |

**Usage**

```bash
# Use with specific change
/wf-apply <change-name>

# Auto-select from context
/wf-apply
```

**How It Works**

This skill is a thin wrapper - it:
1. Validates the change is ready to apply
2. Delegates ALL task execution to the workflow
3. Shows results and handles doc sync

The workflow handles the entire task loop internally.

**Guardrails**
- Don't proceed if change is blocked
- Let workflow handle implementation
- Monitor for critical issues in workflow output
- Always run doc sync before claiming completion
- Suggest manual intervention if workflow fails repeatedly
