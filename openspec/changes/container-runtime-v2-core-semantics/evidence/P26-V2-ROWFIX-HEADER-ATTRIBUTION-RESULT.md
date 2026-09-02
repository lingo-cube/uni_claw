# Row-Composition Header-Attribution Guards — Leader-Executed Final Fix

STATUS: `IMPLEMENTED_AND_VALIDATED` (Leader-executed per direct Human
instruction; fixed and stopped as ordered)

## Root cause (confirmed by RED reproduction before any production change)

The type-mapping layer never mismatches text (it only renames types), but it
also emits no heading/subtitle semantics — so "which text belongs to this
row" is decided by geometry at three composition sites, and two of them
could attribute a SECTION HEADER's text to the row below it:

1. `apply_chevron_heuristic` primary election sorts by topmost-y with no
   vertical-overlap requirement — RED: menu text became `'SECTION'` while
   the row's own title was absorbed as subordinate.
2. `row_relation_head._elect_band_head` elects the topmost in-column
   detection with no widget-anchor requirement — RED: band head became
   `'DISPLAY'` (header) instead of `'Brightness'` (row title).
3. OCR-token → box attribution feeds `primary_line_text` (top-line pick):
   a header token just above a title box (inside the center-distance window)
   became the title box's text — RED observed live in the engine path
   (`candidate_3` text `'SECTION'`).

## Guards implemented (one geometric principle, 0.25x-height tolerance)

Principle: *a text may be a ROW's primary text only if it vertically overlaps
that row's anchor content; a text entirely above the anchor content belongs
to a higher line.* Pure geometry; zero Android-version/device/`Settings`
scenario tokens; no validator loosening.

- **A (chevron, fusion/heuristics.py)**: assignment eligibility requires
  vertical overlap with the widget anchor (overlap > 0, or center within
  0.25x anchor height). Rejected texts keep their own candidate.
- **B (operators/row_relation_head.py)**: two complementary demotions in
  head election — (i) band-local: head candidates must overlap the band's
  leading widget content (narrow, text-less non-OCR boxes; captions bear
  text and are never leading content, so title-above-caption is unaffected);
  (ii) frame-level: when the icon did not cluster into the band (e.g. 40px
  column gap at 720-width), candidates anchoring to NO frame widget while a
  same-band sibling does and sits below them are demoted (section headers).
  Bands with no widget reference anywhere keep topmost election (never
  guess). New fail-closed reason `_REASON_HEAD_ABOVE_CONTENT`.
- **C (fusion/engine.py `_vertically_attributable`, both OCR paths)**: a
  token whose bottom edge is above the box top minus 0.25x box height is a
  higher line and cannot win the top-line text pick. Deliberately
  one-sided: tokens BELOW the box are kept (they can never win the top-line
  pick and legitimately carry same-row description evidence — the first
  symmetric version broke the `single_row_overlapping_boxes` corpus case
  and was corrected before landing).

## Validation

- RED first: `test_header_text_attribution_guards.py` reproduced
  `'SECTION'`/`'DISPLAY'` misattribution on unmodified production (guards
  stashed → 3 failures return).
- GREEN: 5/5 new tests; **unittest 315 OK**; **pytest 362 passed** (+95
  subtests); governance suite unchanged (2 pre-existing failures from other
  dirty-tree work, proven unrelated by stash isolation earlier).
- S1 frozen baseline: all 28 corpus cases byte-stable (`test_fused_
  candidates_match_frozen_baseline`, wiring and replay equivalence green) —
  the guards are no-ops on known-good geometry, including title-above-
  caption, off-column headers, and OCR-only headers.
- ROWFIX-A/B suites green (no interference with row-band ownership or
  cadence consensus).
- `git diff --check` clean. Production changes: exactly three files
  (`fusion/heuristics.py`, `fusion/engine.py`,
  `operators/row_relation_head.py`).

## End-to-end fresh-emulator proof (run 2, single round, Slow Disabled)

After rebuilding the governance receipt truthfully (operator source changed
→ new pipeline revision), one fresh `settingscampaign 1` round:

- Run 1 (pre-fix): root-epoch failure — "Unknown interaction affordances
  remain; completeness cannot be proven" (dual-emission class).
- Run 2 (post-fix): **root epoch completed**; terminal advanced to the
  post-completeness consistency layer — "fresh evidence contains an
  UNRESOLVED interactive UNKNOWN affordance (occurrence 1C5408EC…)".
  13 frames; **zero same-text dual emissions** (run-1 frames had the
  Sound&vibration/Display dual-typed rows; the only remaining multi-type
  pairs are empty-text icon/toggle widget detections, by design distinct).
- Failure-layer migration mirrors the historical bounded-repair pattern
  (terminal advances to the next layer; each layer fails closed correctly).
  Evidence: `evidence/p26-v2-run2/` (run2.log + frames + fusion traces).

## Known remaining gap (not guessed at, by design)

A band whose row has NO detectable widget anywhere (icon missed entirely)
still elects the topmost text: header-above-title vs title-above-caption is
geometrically indistinguishable without widget or semantic cues — the
documented BLOCKING gap (`docs/decisions/unified-spatial-evidence-challenge.
md` §5). The guards never guess in that case; closing it needs the detector
or a semantic tier, not geometry.

## Next blocker (for a future WorkItem, not this one)

Post-completeness consistency: a post-completion fresh frame exposing an
unresolved interactive UNKNOWN affordance (scroll-settle variance class).
Owner: perception channel / post-completeness policy — record, classify,
then repair under its own authorization.
