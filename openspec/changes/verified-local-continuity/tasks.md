# Tasks: verified-local-continuity

> System of record. APPLY gate executed 2026-08-18.

## Implementation plan (EXECUTED)

- [x] A1 — Container: `TryAcceptVerifiedContinuity(observation, expectedForeground,
      recordViewportObservation)` — mechanical same-Container acceptance (strict
      sequence advance + compatible foreground; no absolute-identity requirement),
      viewport-history recording for scroll acceptances.
- [x] A2 — Container: `EvaluatePageBeliefVerifiedContinuity` — LOCAL_IDENTITY takes
      Supports when the Agent independently verified continuity (no false
      Contradicted fusion); original `EvaluatePageBelief` unchanged (back-compat).
- [x] A3 — Container: `RefreshSemanticSnapshot(..., bool verifiedLocalContinuity)`
      routes to the verified belief evaluation; default false = existing behavior.
- [x] A4 — Agent: `IsVerifiedLocalContinuity` predicate (conditions 1–7; fresh
      structural evidence = row/control elements, not bare text; PageAnalysis for
      other-page/contradiction checks; action scope = ScrollForward/SetSwitch).
- [x] A5 — Agent post-action path: absolute-null → predicate → verified acceptance →
      fresh binding/state refresh → continue same Goal on same Container.
- [x] A6 — Agent post-scroll (strict reconciliation) path: same fallback.
- [x] A7 — Tests T1–T15 + T8b (VerifiedLocalContinuityTests: 13 tests, all pass).
- [x] A8 — Real-device corpus re-run: FALSE_SEMANTIC_CONTRADICTION ELIMINATED
      (6/24 → 0/24); residual ASU state-change runs end in truthful
      BindingUnresolved (toggle row scrolled past viewport); already-satisfied fast
      path unchanged; WiFi multilevel 14/14 Satisfied (navigation intact).

## Test matrix

| # | Test | Status |
|---|---|---|
| T1 | title visible → absolute recognition succeeds → no fallback | ✅ |
| T2 | scroll → title disappears → fresh evidence → page preserved | ✅ |
| T3 | multiple consecutive scrolls → page remains verified | ✅ |
| T4 | below-fold SetSwitch → post-action title absent → no false contradiction | ✅ |
| T5 | positive other-page match → continuity rejected | ✅ |
| T6 | foreground change → continuity rejected | ✅ |
| T7 | fresh evidence insufficient → unknown → fail-closed | ✅ |
| T8 | previous page identity alone → insufficient | ✅ |
| T8b | Container mechanical accept rejects stale sequence / foreground mismatch | ✅ |
| T9 | element indices reorder → continuity unaffected | ✅ |
| T10 | normal page recognition unchanged | ✅ |
| T11 | real page navigation still produces transition | ✅ |
| T12 | popup/overlay breaks ownership → continuity rejected | ✅ |
| T13 | real ASU OFF→ON — false page contradiction eliminated (real corpus) | ✅ |
| T14 | real ASU ON→OFF — same | ✅ |
| T15 | already-satisfied ASU — unchanged fast path | ✅ |
