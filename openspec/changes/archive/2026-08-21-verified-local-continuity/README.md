# verified-local-continuity

**Status**: IMPLEMENTED (APPLY gate 2026-08-18). Real-device corpus re-run:
FALSE_SEMANTIC_CONTRADICTION ELIMINATED (6/24 → 0/24); residual ASU state-change
runs end in truthful BindingUnresolved; already-satisfied fast path unchanged;
WiFi multilevel navigation intact (14/14). Pending graduation review (no archive).

## One-line

When the absolute page resolver returns null after a same-Container action on a
scrollable page (title scrolled out of view), preserve the previous semantic page
ONLY when fresh continuity evidence independently verifies same-Container
continuity (Source = VERIFIED_LOCAL_CONTINUITY) — never resolver==null→previousPage.

## Owner

- **Agent**: `IsVerifiedLocalContinuity` predicate (semantic reconciliation; fresh
  identity conclusion from previous verified identity + action context + fresh
  world evidence).
- **Container**: `TryAcceptVerifiedContinuity` (mechanical same-Container
  acceptance) + `EvaluatePageBeliefVerifiedContinuity` / `RefreshSemanticSnapshot`
  (LOCAL_IDENTITY Supports when verified — no false Contradicted fusion).
- Traversal / Environment unchanged. Binding/state freshly resolved from the new
  Observation.

## Falsifiers

F1 resolver-null→previousPage · F2 stale carry-forward · F3 other-page override ·
F4 foreground change accepted · F5 insufficient evidence accepted · F6 navigation
suppressed · F7 L1/L2 coupling · F8 binding/state freeze violated.
