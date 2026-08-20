---
name: architecture-change-safety-check
description: Check authority, lifecycle, behavior, and rollback risk before modifying documentation, runtime, tests, or OpenSpec artifacts.
metadata:
  type: Safety Checklist
  authority: NONE
---

# Architecture Change Safety Check

Before a change:

1. Classify its scope: Documentation, Runtime, Test, or OpenSpec.
2. Check authority impact, lifecycle impact, behavior impact, and rollback availability.
3. If authority is unclear, sources conflict, lifecycle conflicts, or rollback is unavailable, output `ARCHITECTURE_DECISION_REQUIRED` and stop.

Do not automatically resolve conflicts.
