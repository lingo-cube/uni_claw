# Proposal: Open-World Traversal Identity Safety

| Attribute | Value |
|-----------|-------|
| Change ID | `open-world-traversal-identity-safety` |
| Status | Proposed |
| Type | Mechanism extension |
| Date | 2026-08-16 |
| Buyer | SETTINGS_FULL_TREE_EXPLORATION |
| Gap | OPEN_WORLD_EXPLORATION_IDENTITY_SAFETY |

## Why

The Runtime already performs bounded open-world Settings traversal with fresh evidence, branch inventory, parent return, and evidence-backed completion. However, it does not yet have run-local cycle detection or duplicate semantic page identity handling. For full Settings tree exploration, A → B → A cycles and same-page-different-branch duplicates must be rejected or explicitly merged instead of silently re-traversed until a depth cutoff.

## What

- Add Agent-owned run-local traversal identity evidence for the open-world path.
- Track:
  - current ancestry page identities
  - visited page identities
- Before child Container entry:
  - if child identity is already in current ancestry → reject as cycle
  - if child identity is already visited from a different branch and no explicit merge rule exists → fail closed on ambiguity
- Preserve Container ownership, Traversal authority, CandidateAuthorization boundary, and GoalEvidence authority.

## Non-Goals

- No global graph database
- No LLM / VLM
- No new planner
- No Runtime semantic model rewrite
- No DSH change
- No change to closed-world PlanRun behavior
- No new Container/Traversal/Recovery ownership or authority
