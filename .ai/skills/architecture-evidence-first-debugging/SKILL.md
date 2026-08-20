---
name: architecture-evidence-first-debugging
description: Diagnose complex-system failures from evidence before assumptions, separating fixture, test, runtime, and architecture causes.
metadata:
  type: Debugging Method
  authority: NONE
---

# Architecture Evidence-First Debugging

Follow this sequence:

1. Collect evidence.
2. Identify the first failure.
3. Separate fixture, test, runtime, and architecture problems.
4. Find the root cause.
5. Apply the minimal repair.

Evidence comes before assumption. Do not change architecture from symptoms, add fixture-specific exceptions, or weaken invariants to hide a problem. A diagnostic request does not authorize a repair; apply one only when modification is authorized.
