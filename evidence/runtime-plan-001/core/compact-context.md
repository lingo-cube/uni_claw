# Agent context PlanAttempt evidence projection

The public Driver audit view retains the complete `PlanAttempt.EvidenceRefs` list. Only `AgentDecisionContext.PlanExecution.Attempts[*].EvidenceRefs` is projected to the single stable reference `plan-attempt:<full AttemptId>`. `BlockedEvidence` is passed through unchanged, preserving its Owner/source evidence. The completion verifier was not modified.

## Verification

- Public Driver regression: 24 candidate evidence refs and 24 guard evidence refs remain present in the verified Driver attempt, while the subsequent Agent context contains only the matching `plan-attempt:` reference. That reference is used by the existing completion path and the run completes. `compact-green.log` passed 1/1; `compact-red.log` records the expected pre-fix failure.
- Plan-driver focused suite: `KernelRunDriverPlanTests` passed 38/38 in `compact-plan-focused.log`.
- No Kernel full-suite run was performed for this bounded task, as requested.
