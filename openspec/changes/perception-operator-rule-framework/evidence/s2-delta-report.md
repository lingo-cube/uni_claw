# S2 Delta Report — S1 corpus through the routed pipeline

This report documents the OLD→NEW candidate difference of running the **full
S1 navigation-row corpus (28 cases)** through the S2ii-wired pipeline
(`[uniform-list-row-grouping, row-relation-head, spacing-verifier]` with the
frozen-input runner adapter) versus the frozen S1 baseline
(`evidence/s1-equivalence-baseline/baseline.json`).  It **reports only** — the
S1 baseline and corpus are NOT regenerated; any sanctioned baseline regen is a
Leader decision after this review.

Change: `perception-operator-rule-framework` S2 (second half).
Base revision: `e6c6f4b5eb927d05338f86058d391cc23a3ba`.

## Method

* Each corpus case is replayed through the routed pipeline via the S1
  equivalence harness interpretation (`fuse_evidence` / `fuse_evidence_from_crops`
  with the case's `yolo`/`ocr` arrays); candidates are canonicalized exactly as
  the frozen gate does (floats rounded to 6dp, sorted by type/text/x1/y1/id,
  sorted keys, LF).
* "UNCHANGED" = byte-identical canonical candidates vs the frozen baseline.
  "CHANGED" = byte-different (old→new diff below), with structural
  justification.  All ≥4-confirmed-anchor cases MUST be UNCHANGED (G-2); every
  changed case is a low-anchor (<4 confirmed row anchors) case.
* Determinism (G-7): every case replayed twice ⇒ byte-identical candidates and
  trace bytes (verified for all 28).

## Per-case table (28 cases)

| case_id | route | anchors | baseline | delta |
|---|---|---|---|---|
| single_row_overlapping_boxes_scale_1_0 | relation-head (dedup) | <4 | **UNCHANGED** | — |
| single_row_overlapping_boxes_scale_0_5 | relation-head (dedup) | <4 | **UNCHANGED** | — |
| single_row_overlapping_boxes_scale_1_5 | relation-head (dedup) | <4 | **UNCHANGED** | — |
| title_only_row_remains_one_item | relation-head (dedup) | <4 | **UNCHANGED** | — |
| repeated_labels_distinct_anchors | relation-head (dedup) | <4 | **UNCHANGED** | — |
| tightly_adjacent_rows_unique_anchors | relation-head (dedup) | <4 | **UNCHANGED** | — |
| equidistant_anchor_ambiguity_fail_closed | relation-head | <4 | **CHANGED** | +1 menu_item `Ambiguous` |
| legacy_crop_fusion_same_row_composition | relation-head (dedup) | <4 | **UNCHANGED** | — |
| uniform_list_bracketed_row_recovered | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_title_description_grouped | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_compact_description_not_title | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_trailing_control_rejects_slot | uniform-list | ≥4 | **UNCHANGED** | — |
| left_switch_false_positive_still_anchors_row | relation-head (dedup) | <4 | **UNCHANGED** | — |
| empty_text_detector_artifact_not_emitted | fail-closed | <4 | **UNCHANGED** | — |
| icon_read_noise_absorbed_into_row | relation-head (line-dup suppressed) | <4 | **UNCHANGED** | — (see veto note) |
| uniform_list_bounded_lower_continuation | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_duplicate_box_absorbed | uniform-list (delegated) | ≥4 | **UNCHANGED** | — |
| uniform_list_complete_upper_continuation | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_lower_edge_title_description | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_two_bracketed_rows | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_incomplete_bracket_rejected | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_irregular_spacing_no_activation | uniform-list (delegated) | ≥4 | **UNCHANGED** | — |
| uniform_list_ambiguous_midpoint_rejected | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_off_column_section_rejected | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_high_inference_ratio_disabled | uniform-list (delegated) | ≥4 | **UNCHANGED** | — |
| uniform_list_proven_clipped_top_row_demoted | uniform-list | ≥4 | **UNCHANGED** | — |
| uniform_list_complete_top_row_kept | uniform-list | ≥4 | **UNCHANGED** | — |
| v1n_low_anchor_viewport_subtitle_fail_closed | relation-head | <4 | **CHANGED** | +4 menu_items (real rows) |

**Summary: 26 UNCHANGED, 2 CHANGED.  Every ≥4-anchor case is UNCHANGED
(byte-identical, G-2 green).  Both changed cases are low-anchor.**  The S1
equivalence gate (`test_row_composition_equivalence.py`) therefore fails ONLY
on `equidistant_anchor_ambiguity_fail_closed` and
`v1n_low_anchor_viewport_subtitle_fail_closed` — the anticipated G-2 STOP
condition: **this WorkItem terminates BLOCKED pending Leader review** (baseline
regen is sanctioned by the Leader only, per the WorkItem gates).

## Changed case 1: `equidistant_anchor_ambiguity_fail_closed`

OLD (frozen S1):

| type | text | id | typeInferred |
|---|---|---|---|
| icon | | candidate_1 | – |
| icon | | candidate_3 | – |
| text_block | Ambiguous | candidate_2 | – |

NEW (routed):

| type | text | id | typeInferred |
|---|---|---|---|
| icon | | candidate_1 | – |
| icon | | candidate_3 | – |
| **menu_item** | **Ambiguous** | **relation_head_band_1** | **row_relation_head** |
| text_block | Ambiguous | candidate_2 | – |

Pipeline steps: uniform-list `noop` → row-relation-head `activated`
(`composed 1 … merged 1, suppressed 0`) → spacing-verifier `verified`.

Structural justification: below the 4-anchor floor, relation-head's geometric
head election finds ONE text-bearing `text_block` detection
(`title`, 100,105,250,135) at the band's leftmost text column (OCR `Ambiguous`
at x 102).  Its own fail-closed domains (no text column / unanchored column /
too narrow / equal-width same-line tie / subtitle continuation) do NOT fire —
the two icons cluster into a separate text-less band (rejected NO_TEXT).  S1's
"equidistant to two icons ⇒ not promoted" rule is the chevron heuristic's
unique-nearest-widget model, which the uniform-list path preserves but the
relation-head path does not inherit below the activation floor.  This is a
deliberate, documented low-anchor semantic relaxation — the row has real
geometric evidence (detection + OCR at the title column) — but it is a TRUE
delta for the Leader to adjudicate (it also turns
`test_navigation_row_composition.py::test_equal_distance_to_two_anchors_is_not_promoted`
red with the routed pipeline).

## Changed case 2: `v1n_low_anchor_viewport_subtitle_fail_closed`

OLD: `input "Q Search settings"` + 3 menu_items (Sound & vibration, Security &
privacy, Location) + text_blocks (Accessibility, Dark theme…, Passwords…,
Safety…, Wallpaper).  Subtitle absent from candidates (absorbed into the fused
Sound & vibration row).

NEW adds (only): `menu_item` rows for **Accessibility**, **Wallpaper**,
**Safety & emergency**, **Passwords, passkeys & accounts** (all
`typeInferred: row_relation_head`, ids `relation_head_band_3/2/8/9`).  Fused
menu_items are NOT duplicated (Sound, Security, Location suppressed as
same-line duplicates).  `input "Q Search settings"` is NOT promoted (the
merge suppresses the relation-head search-band candidate because the engine
classified that line as `input`).  Pipeline steps: uniform-list `noop` →
row-relation-head `activated` (`composed 7 … merged 4, suppressed 3`) →
spacing-verifier `verified` (`verified 7 generated row(s)`).

**G-1 proof (routed): the v1n subtitle `Volume, vibration, Do Not Disturb` is
NEVER a menu_item.**  In the operator's band record on the real frame it is
absorbed in-band (caption satellite of the search band, which is itself
suppressed as the engine-classified input line; the subtitle has no
detector-anchor band of its own — OCR-only subtitle lines fail closed by
construction, see the module docstring of `row_relation_head.py`).  Asserted
in `tests/test_cross_ui_row_composition.py::RoutedV1nIntegrationTests`.

Structural justification: Wallpaper/Accessibility/Safety & emergency/Passwords
each carry a raw `text_block` detection at the title column bearing an OCR
line — real rows that the S1 uniform-list path left uncomposed only because
the frame has <4 confirmed anchors.  Relation-head composes them from raw
geometry (never text semantics); no fabrication — every composed row has a
detector anchor + OCR at the text column.

## Veto note (fail-closed preservation, `icon_read_noise_absorbed_into_row`)

With the line-occupancy merge rule the routed output is byte-identical to the
baseline AND the verifier stays `verified` on every corpus case (0 vetoes).  A
degenerate emission is nonetheless worth recording for the Leader: on
`icon_read_noise_absorbed_into_row` the operator's raw run elects the leading
icon as a band head reading the OCR noise ("100%" — the same visual line as
the fused `Battery` row).  The adapter suppresses it as an already-composed
`menu_item` line duplicate (span overlap ≥ ½ the shorter span) **and**, as a
second line of defense, the verifier's strict-ordering necessity check
rejects any same-line set (`"generated rows are not strictly vertically
ordered"`) — the executor then rolls the generators' mutations back and the
frame stays byte-identical to S1 (G-6).  Neither mechanism is a new rejection
surface: both express the one-menu-item-per-visual-line invariant the verifier
already encoded.

## G-2 byte-identity proof

All 18 ≥4-confirmed-anchor corpus cases (all `uniform_list_*` families) are
UNCHANGED byte-for-byte.  The routed adapter returns `noop`/`delegated` on
every ≥4 frame (including `uniform_list_irregular_spacing_no_activation` and
`uniform_list_high_inference_ratio_disabled`, where uniform-list itself noops —
relation-head never overrides the ≥4 path), so the uniform-list runner output
and the pipeline rollback semantics are untouched.  Additionally the 10
low-anchor frames whose relation-head output fully duplicates already-composed
rows (`single_row*`, `title_only`, `repeated_labels`, `tightly_adjacent`,
`legacy_crop`, `left_switch`, `empty_text`, `icon_read_noise`) are also
UNCHANGED.

## G-5 verifier envelope (S1 corpus + cross-UI corpus)

`GENERATED_ROW_REASONS` now includes `row_relation_head` (additive).  All
relation-head activations on the S1 corpus (12 frames) pass the verifier
(`verified`), as do all cross-UI corpus cases
(`tests/corpus/cross_ui_row_corpus.json`, 6 cases; G-3/G-5 pinned in
`tests/test_cross_ui_row_composition.py` — 13 tests + 22 subtests green).
Verifier parameters stay `tighten_only`; no validator bypass.  0 vetoes across
both corpora (the degenerate icon-noise line is suppressed before the
verifier; see veto note).

## G-7 determinism (S1 corpus + cross-UI corpus)

Same inputs + same resolved rule set ⇒ byte-identical candidates **and** trace
bytes for all 28 S1 cases and all 6 cross-UI cases (double replay assertions in
both suites).

## Trace shape change (expected consequence of the S2ii append)

Frames now carry THREE pipeline steps (uniform-list, row-relation-head,
spacing-verifier) instead of two.  For ≥4 frames the relation-head step is a
deterministic `noop`/`delegated` notice (candidates untouched, trace
byte-deterministic); for low-anchor frames it records the operator's decision
record.  Existing tests that pin the 2-step topology or the 2-operator
declared topology are red as an anticipated consequence (Leader-owned
assertion updates; the WorkItem forbids touching existing test files):
`test_row_relation_head.py::…test_s1_topology_unchanged_until_engine_routing`,
`test_operator_pipeline_wiring.py::test_declared_topology_and_authority`,
`test_operator_pipeline_wiring.py::test_trace_steps_record_each_decision`,
`test_operator_pipeline_wiring.py::test_generator_disabled_by_rule_is_noop`,
`test_operator_pipeline_wiring.py::test_pipeline_rolls_back_on_verifier_veto`.
Two further existing gates compare the routed pipeline against the S1 frozen
behavior and diverge only on the two CHANGED low-anchor cases:
`test_operator_pipeline_wiring.py::test_pipeline_byte_equal_to_legacy_shim`,
`test_operator_pipeline_wiring.py::test_replay_matches_frozen_baseline`
(together with the S1 equivalence gate itself).  After a Leader-sanctioned
baseline regen (S2-scoped v2 baseline per `S2-acceptance-protocol.md` G-2) and
the 2-op→3-op assertion updates, these return to green.

## Files changed / evidence

* Routed wiring: `operators/relation_head_router.py` (new adapter),
  `operators/registry_defaults.py` (pipeline append + runner), `operators/trace.py`
  (raw-source forwarding), `fusion/engine.py` (raw-source bundle),
  `operators/spacing_verifier.py` (`GENERATED_ROW_REASONS` + note).
* Cross-UI corpus + tests: `tests/corpus/cross_ui_row_corpus.json` (new),
  `tests/test_cross_ui_row_composition.py` (new).
* This report.  S1 baseline/corpus and existing test files untouched.
---

## Leader Adjudication (2026-08-27, appended by the Leader)

**Ruling: BOTH deltas SANCTIONED.**

- **v1n case**: sanctioned as-is — this IS S2's purpose (4 real rows composed from
  detector-anchored title-column geometry; subtitle never a menu_item; search
  suppressed). G-1 holds.
- **equidistant case**: sanctioned. The S1 refusal was an artifact of the
  **chevron-attachment model** (text attaches to its unique nearest interactive widget;
  equidistant ⇒ attachment ambiguity ⇒ refuse). The relation-head **band model** performs
  no attachment: the text-bearing detection (wide, co-located with OCR, at the title
  column) is its own band's head; the two icons cluster into a text-less band (rejected
  NO_TEXT). Evidence for the row is unambiguous; the safety invariants that DO matter all
  hold (OCR-only bands fail closed — cross-UI F2 pins section headers never promoted;
  subtitle guard; verifier envelope). The old assertion
  `test_equal_distance_to_two_anchors_is_not_promoted` remains TRUE for the
  fusion/attachment layer (unchanged there) and is scoped accordingly; the routed
  promotion is asserted to carry `row_relation_head` provenance.
- **Baseline regeneration**: SANCTIONED for exactly the two changed low-anchor cases
  (regen executed by the Leader via the sanctioned P26_REGEN_BASELINE flow after the
  assertion updates land; all 26 other cases must remain byte-identical — the gate
  enforces this on every future run).
