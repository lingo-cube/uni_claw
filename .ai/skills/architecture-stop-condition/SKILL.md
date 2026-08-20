---
name: architecture-stop-condition
description: Stop safely and request an architecture decision when authority, evidence, ownership, decisions, or gate status are unresolved.
metadata:
  type: Governance Safety
  authority: NONE
---

# Architecture Stop Condition

Stop and output `ARCHITECTURE_DECISION_REQUIRED` when any of these applies:

- Authority conflict
- Decision conflict
- Gate-status conflict
- Missing evidence
- Unknown ownership
- An architecture judgment is required but the current role lacks permission

Do not guess.
