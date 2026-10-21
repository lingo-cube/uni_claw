# WORKBENCH-INTEGRATION-001
lifecycle_state: closed
disposition: none
depth: standard

## Intent
Integrate the approved layout/cards from the attached worktree into the shared checkout, preserving unrelated dirty work and validating the real DSH client loading path.

## Scope / constraints
Only the inventoried Workspace UI source/tests, generated workbench client, README, and own Change/evidence records. Product protocols, Runtime, source projection and launch semantics unchanged. No commit/push requested.
Only update the installed workbench client, with a recoverable backup. Do not restart or kill the owner 3080 or another task's active 3081. If reload is needed, prefer an owned isolated verification instance.

## Acceptance
- A1: scoped merge preserves P0 and unrelated dirty work; generated client matches shared source.
- A2: full Workspace/DSH tests pass; scoped syntax/whitespace checks pass.
- A3: real DSH loads the new client and displays actual session conversations, Trace and record details; browser asset hash matches the generated client.
- A4: profile client update is backed up; active product work is not interrupted.

## Plan
Inventory/snapshot -> exact-baseline merge -> tests/review -> installed copy and real browser loading -> evidence and state synchronization.

## Verification
level: ENVIRONMENT
method: exact-baseline merge, generated bundle, full tests, real DSH product profile on owned port 3082, persisted-session browser interactions and visual review.
expected: A1-A4 pass without interrupting active product work.
actual: 167/167; actual DSH renders conversation/tool cards, Trace and selected records, Evidence reads ready, launch dialog opens without submission, 390px has no horizontal overflow. Served batch contains exact generated client. Active 3081 was not restarted.
evidence: evidence/workbench-integration-20261022/verification.json and review.md.
limit: Existing persisted real-session data was queried; no new model/device run. Active 3081's current client revision is not claimed updated; its installed client is ready for next reload. Preview remains on 3082.

## Status
- 2026-10-22: user authorized continuing integration; shared target files match prior entry snapshots, no new overlapping edits found. Snapshot recorded before mutation.

- 2026-10-22: planned -> implemented: inventoried source/test/UI files integrated; generated 8-module client; installed product client backed up and atomically replaced.
- 2026-10-22: implemented -> reviewed: scoped diff, P0/authority, installed Host/source equality and active-process ownership reviewed.
- 2026-10-22: reviewed -> verified: shared tests 167/167; real DSH profile and read-only browser flows passed, served-client equality recorded.
- 2026-10-22: verified -> closed: requested integration complete; owned preview kept on 3082; active 3081 and owner 3080 preserved. Next optional presentation work is readable labels for old structured steps.
