---
status: accepted
date: 2026-09-06
---

# Four Semantic Gates drive UniFlow V2

The development flow keeps exactly four generic gates — Explore Resolution
(10 checks → EXPLORE_RESOLVED / STAY_IN_EXPLORE), Execution Readiness
(8 checks → EXECUTION_READY), Verification (8 checks → VERIFIED /
VERIFICATION_FAILED), Completion (7 checks → COMPLETE). Old-flow control
semantics (intent clarity, scope, known/unknown/assumption, owner/authority
clarity, acceptance, human decision, evidence, fail-closed) migrate into
these checks; historical governance structure (two-lane state machine, H4
contracts, OpenSpec lifecycle, product runtime gates) does not. Gates are
semantic contracts, not forms. UniFlow may identify/reference/validate/detect
conflicts in project authority (declared by CONTEXT.md/ADR) but never invent
or override it. Review and worker self-reports never decide COMPLETE.

## Consequences

- Flow may be light, gates may not be softened: unresolved explore blocks
  UniFlow; insufficient evidence blocks VERIFIED; unresolved human decisions
  block COMPLETE.
- Bug fixes require Root Cause before entering UniFlow (STAY_IN_DIAGNOSIS).
- Token vocabulary is now normative: EXPLORE_RESOLVED, STAY_IN_EXPLORE,
  STAY_IN_DIAGNOSIS, EXECUTION_READY, VERIFIED, VERIFICATION_FAILED, COMPLETE.
