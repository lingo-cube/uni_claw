---
status: accepted
date: 2026-09-06
---

# Freeze the Pre-UniFlow / UniFlow boundary and Plan persistence rules

Explore (feature grilling or bug diagnosis) is Pre-UniFlow: its job is to make
the problem actionable. UniFlow starts only at EXPLORE_RESOLVED (problem,
current state, desired state, constraints all clear; unknowns non-blocking; no
open human decision) and owns Plan / Route / Execute / Delegate / Review /
Verify / Complete. grill-with-docs completion is not permission to implement —
it hands control back. Plan is the Leader's execution intent, not a mandatory
artifact: it persists (plans/) only across context boundaries or long-running
work, and transient WorkItems are compiled from it when delegating.

## Consequences

- Bug fixes enter UniFlow only after FDP / Owner / Root Cause; no root cause,
  no implementation delegation.
- Leader may execute directly in a continued healthy context; only delegation
  defaults to fresh disposable subagent contexts.
- Completion is judged solely by UniFlow from evidence — subagent self-reports
  and code-review outputs never decide Complete.
- Directives on issue tracking reinforced: WorkItems are never published or
  mirrored to GitHub Issues or `.scratch/`.
