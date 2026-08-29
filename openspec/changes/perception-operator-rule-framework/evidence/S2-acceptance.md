# S2. Deterministic Relation Head — Acceptance Evidence

## Hard-gate verdicts (all Leader-verified 2026-08-27)

| Gate | Verdict | Evidence |
|---|---|---|
| G-1 v1n counterexample | **PASS** | Subtitle `Volume, vibration, Do Not Disturb` NEVER a menu_item — asserted on the routed real frame (`RoutedV1nIntegrationTests`) + delta report (absorbed in-band / OCR-only fails closed by construction); `Q Search settings` never promoted |
| G-2 four-anchor no-regression | **PASS** | All ≥4-anchor cases byte-identical through the entire process (delta table + wiring byte-equal assert on delegated frames) |
| G-3 cross-UI regression | **PASS** | 13 tests + 22 subtests over 3 non-Settings families (dense mixed list w/ same-text distinct positions; preferences page w/ OCR-only section headers never promoted + toggles never navigation; low-anchor + evidence-insufficient variants) |
| G-4 input freeze | **PASS** | Adapter consumes engine-forwarded raw_sources via `handles_raw_sources` marker — never composed candidates; asserted in tests |
| G-5 verifier envelope | **PASS** | 0 vetoes; `GENERATED_ROW_REASONS` extended additively; verified on all activations |
| G-6 fail-closed preservation | **PASS** | OCR-only bands/headers fail closed; evidence-insufficient case composes NOTHING (noop); empty-artifact case unchanged |
| G-7 determinism + trace | **PASS** | Byte-deterministic replays on all 34 corpus cases; 3-op ordered trace steps (delegated/fail-closed reasons recorded) |

## Final verification (leader runs)

- Equivalence gate (regenerated baseline): **1 passed** — all 28 S1 cases match the routed
  pipeline (26 byte-identical + 2 sanctioned deltas).
- Full suite: **194 passed + 25 subtests, 1 failed (RPER-06 pre-existing)**.
- Governance: **48 passed, 1 failed (RSI08 pre-existing)**.

## Leader rulings recorded during acceptance

1. S2i register-only deviation SANCTIONED (adapter/routing = S2ii).
2. Both low-anchor deltas SANCTIONED (`s2-delta-report.md` adjudication section):
   v1n = S2's purpose achieved; equidistant = chevron-attachment-model artifact, band
   model has unambiguous evidence, all safety invariants hold (old test scoped to the
   fusion layer; routed promotion carries row_relation_head provenance).
3. Baseline regeneration SANCTIONED and EXECUTED by the Leader (regen + verify passes
   green; the gate now locks the routed behavior byte-level on every future run).
4. 8 stale asserts updated truthfully (S2iii; no safety assertion weakened).

## Net effect (the IR-G0 traversal unblock)

With S2, low-anchor viewports now compose REAL detector-anchored rows structurally
(the v1n frame gains Wallpaper/Accessibility/Safety & emergency/Passwords as genuine
navigation rows) while every safety invariant holds. Combined with S1's duplicate-box
repair, the composed pipeline's candidate-per-provable-navigation-row property is the
Phase 2.6 re-entry precondition — final confirmation happens in the S4+ entry check on
real frames.
