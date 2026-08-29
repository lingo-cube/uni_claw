# S2 Acceptance Protocol & Cross-UI Corpus Requirements (leader-prepared, pre-dispatch)

Frozen before S2 dispatch so the S2 WorkItem inherits hard, checkable gates
(per Human Gate #2: v1n counterexample + four-anchor no-regression + cross-UI
regression; shortfall = STOP, no auto-S3).

## S2 hard gates (all must pass; any failure = STOP)

| # | Gate | Check |
|---|---|---|
| G-1 | v1n counterexample | The `v1n_low_anchor_viewport_subtitle_fail_closed` corpus case: subtitle NEVER becomes a menu_item; with relation-head active, the case either composes correctly (3+ anchors → 3 menu_items) or fail-closes — never fabricates. Baseline entry updated ONLY through the S2 acceptance process with leader review (never by the implementer silently). |
| G-2 | Four-anchor no-regression | All uniform-list family corpus cases unchanged (the S1E equivalence test extended: S2 must add its cases to a NEW v2 baseline or an S2-scoped baseline — the S1 baseline stays frozen as the pre-S2 reference; any four-anchor case change = regression). |
| G-3 | Cross-UI regression | NEW corpus family ≥3 non-Settings UIs (see below): one navigation candidate per provable navigation visual row; descriptions/subtitles/local controls/ambiguous rows never fabricated. |
| G-4 | Input freeze | row-relation-head consumes ONLY raw visual regions (uncombined detector boxes + OCR text blocks) and pairwise geometric relation candidates — verified by code review + a wiring test asserting no fusion-composed candidates enter the operator. |
| G-5 | Verifier envelope | All relation-head outputs pass spacing-verifier; verifier params tighten_only; no validator bypass. |
| G-6 | Fail-closed preservation | Evidence-insufficient viewports remain fail-closed (S1 behavior unchanged on those cases). |
| G-7 | Determinism + trace | Same inputs + rule-set hash → identical outputs and traces. |

## Cross-UI corpus requirements (S2 must construct; deterministic synthetic frames)

Family coverage (≥3 distinct non-Settings UI shapes, exercising generality):

1. **Dense list with mixed rows** (e.g., a store/catalog-style list): rows with title-only,
   title+caption, icon+title — same-text rows at DIFFERENT positions (must NOT merge).
2. **Grouped settings-like page from a NON-settings app** (e.g., a profile/preferences
   screen in a third-party app shape): section headers (non-interactive), rows, local
   controls (toggles) — toggles never become navigation candidates.
3. **Low-anchor variant of each** (<4 detector anchors): relation-head must compose
   structurally or fail closed — subtitle/description text never promoted.

Construction rules: synthetic (yolo + ocr arrays) like the existing corpus, deterministic,
no network; each case pins its expectation in the baseline through the acceptance run.

## Leader independent verification (post-S2, before any S2 checkbox)

1. Re-run: equivalence suite (S1 gate), S2 corpus suite, full perception suite
   (pre-existing-failure parity: RPER-06/RSI08 only).
2. Code review: input freeze (G-4), verifier envelope (G-5), no validator bypass.
3. Trace determinism spot-check.
4. Only then: S2 tasks checked, S4 dispatched.

## Deferred (not S2)

- S1C governance binding (rule-set hash into config manifest; no CURRENT-ACTIVE change)
  — dispatched after S1B verifies.
- S3/S5: NOT_AUTHORIZED / DEFERRED (Gate #2).
