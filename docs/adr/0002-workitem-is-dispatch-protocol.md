---
status: accepted
date: 2026-09-05
---

# WorkItem is a Leader→SubAgent dispatch protocol, not a resident task system

A WorkItem exists only when the Leader delegates work to a fresh subagent
context (isolation, parallelism, or context offload). Small changes are
executed directly in the current context and never produce a WorkItem; durable
work intent lives in `plans/` and git history, so the repo carries no standing
ticket system.

## Consequences

- UniFlow Small flow is Explore → direct execution → TDD/Verify → Complete.
- `workitems/` holds dispatched payloads only; the setup-matt issue-tracker
  config records "no tracker" instead of treating WorkItems as tickets.
- The WorkItem schema is unchanged — it defines the dispatch payload's shape
  (portable, harness-neutral), which is exactly its protocol role.
