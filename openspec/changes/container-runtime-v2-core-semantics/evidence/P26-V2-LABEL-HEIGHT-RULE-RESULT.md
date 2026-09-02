# Label-Height Rule — Governed Registration Result

STATUS: `RULE_REGISTERED_AND_LIVE_VALIDATED` (partial residuals recorded below)

## The rule (established as requested — a governed rule, not a hidden constant)

**Label-height rule** (`row-relation-head` operator, docstring §6):

> A composed head whose text box is significantly SHORTER than a vertically
> adjacent in-column head AND than the page's median head height is a
> small-font section label / subtitle line, not a row. Box height tracks
> FONT SIZE, not text length. The short head is demoted to a NonInteractive
> `section_label` satellite of the taller adjacent row — never silently
> dropped, never actionable.

Registered in the perception rule framework:

- Parameters `label_height_ratio` (default 0.75, bounds (0,1]) and
  `label_pair_gap_ratio` (default 3.0, bounds (0,10]) in
  `ROW_RELATION_HEAD_PARAM_DEFAULTS` / `_BOUNDS` → flow into the operator
  contract, the auto-generated `DEFAULT_RULE_SET` root rules,
  `resolvedParams` and `ruleSetHash` in every trace.
- Fail-closed guards: requires ≥3 composed heads (page-height modality from
  two heads is unreliable — real single-line rows vary in detection-box
  height); strict `<` so equal-height rows never demote each other; pair
  must share the text column and sit within `label_pair_gap_ratio` × taller
  height; demotion reason `_REASON_LABEL_HEIGHT` recorded in band records.
- Origin evidence: YOLO raw output on the real Display page — both 'Color'
  (group label) and 'Colors' (row) are class `Text` (0.92/0.89); the ONLY
  distinguishing signal is box height (17px vs 24px = font size).

## Validation

- `test_label_height_rule.py` 8/8: real run-2 geometry ('Color' 17px above
  'Colors' 24px + two modal rows) → label demoted, rows compose; controls:
  equal-height rows compose; lone short head composes (no modality);
  distant short text composes (no pair); subtitle-below-row demoted on a
  modal page; params registered in contract + registry.
- RED→GREEN proven (rule stashed → 4 failures return).
- Full suites: **unittest 323 OK; pytest 370 passed + 95 subtests** — S1
  frozen baseline byte-stable (rule is a no-op on known-good geometry).
- Governance RSI05 failure proven pre-existing by stash isolation
  (parallel OCR dirty-work; identical with/without this change).
- One synthetic FDP fixture updated to uniform row heights (its leftover
  "rows" were accidentally half-height — an artifact unrelated to its
  delegation-intent purpose; cadence-gap assertions unchanged).

## Live emulator validation (runs 5 & 6)

- Run 5: failed at root with NEW variance (garbage OCR row 'stem';
  unresolved subtitle line 'Display, interaction, audio'; two rows
  type-flapping) — **label rule fired 0 times** (trace-verified): the
  failure is run-to-run perception variance, not the rule.
- Run 6: reached Display; **the rule fired live**: frames seq 21–31 show
  'Color' as NonInteractive `section_label` and 'Colors' as the menu row —
  **the phantom 'Color' navigation row is eliminated on the real device**.

## Residuals (recorded, NOT fixed — next iteration's scope)

1. **Cross-frame height jitter flapping**: in run 6 seq 24–25 'Color'
   briefly recomposed as menu_item (per-frame detection-height jitter moves
   it across the 0.75 ratio). A hysteresis/stability requirement (e.g.
   demotion sticky across frames once observed, or median over k frames)
   would close it.
2. **Raw-twin cleanup**: after demotion, the raw 'Color' text_block
   candidate from initial construction still floats in later frames
   (seq 27+ show 'Color' as both text_block and NonInteractive). The
   engine's band-ownership pass attaches text_blocks BELOW composed rows;
   a label ABOVE its row needs the symmetric attachment.
3. Run-to-run root-page perception variance (garbage rows, subtitle
   leftovers, flapping) remains the dominant NotCompleted driver — the
   documented perception-channel class; structural fixes each remove their
   own class while the variance pool keeps sampling new instances.

## Artifacts

- Rule + tests: `operators/row_relation_head.py` (§6, params, demotion),
  `tests/test_label_height_rule.py`
- Live evidence: `evidence/p26-v2-run5-6/` (run5/run6 logs, frames, fusion
  traces — search `section_label` in traces for live firings)
- Prior context: `evidence/P26-V2-ROWFIX-HEADER-ATTRIBUTION-RESULT.md`
