# S2i. Row-Relation-Head Core — Acceptance Evidence

## Leader ruling on the worker's deviation (recorded before acceptance)

**SANCTIONED: register-only.** The worker correctly refused to append row-relation-head
to the executed pipeline list: the executor's runner protocol is `(candidates, yolo,
values)` while the operator's FROZEN inputs require raw arrays `(detections,
ocr_tokens, width, height, params)` — appending without an adapter (which would require
touching trace.py/engine.py, outside this slice's scope) would crash every engine call
and break the S1 byte gates. Pipeline append + runner adapter + engine routing are
S2ii's scope. This is exactly the "escalate rather than improvise" behavior the
process requires.

## Leader's independent verification

- `tests/test_row_relation_head.py` → **16 passed** (v1n subtitle-never-menu guard incl.
  whitespace/comment variants; same-text distinct rows; ambiguity fail-closed with
  reason; OCR-only band fail-closed; determinism bytes; input-freeze signature;
  registration + lint 0; topology-unchanged; satellite cap; invalid geometry).
- S1 zero-diff gate + navigation gate: **28 passed (+3 subtests)** — no behavior change.
- Wiring suite: 3 counting asserts failed (expected consequence of registration);
  **leader fixed them truthfully** (registered set = 3 root rules; EXECUTED topology
  still 2 until S2ii; comments in the test file) → wiring + relation-head +
  equivalence = **32 passed**; full suite **181 passed + 1 pre-existing (RPER-06)**.
- Determinism sweep (worker): all 28 corpus frames byte-identical; the real v1n frame
  never emits the subtitle as a candidate.

## Design summary (accepted)

- Head election: widest raw DETECTION at the band's leftmost OCR column, overlapping a
  text box, ≥ min_head_width_ratio × band width; ties → dup-text merge / topmost
  stacked / else fail-closed; OCR-only bands never produce heads (v1n-safe by
  construction).
- Subtitle protection: in-band absorption + detector-anchor fail-closed (both verified
  on the real v1n frame) + spec-named continuation predicate (defensive; white-box
  pinned).
- Banding: union-find over containment / shared-column / h-overlap + adjacency
  (gap ≤ ratio × min-height) or v-overlap; fully sorted → byte-deterministic.
- Satellites: caption/icon/toggle/control roles with allIds/ocrIds/headId provenance,
  capped, emitted NonInteractive.

## S2ii scope (next)

Runner adapter (frozen-input signature ↔ executor protocol); pipeline append +
`<4`-anchor engine routing (≥4 path byte-unchanged); add `row_relation_head` to
`spacing_verifier.GENERATED_ROW_REASONS` (G-5); cross-UI corpus (≥3 non-Settings
families); `s2-delta-report.md` (S1 corpus old→new; subtitle-still-never-menu proof);
baseline regen ONLY after leader review.
