# S1 Equivalence Baseline — perception-operator-rule-framework

## What this freezes

`baseline.json` is the byte-level answer to "equivalent to *what*?" for the S1
zero-difference hard gate. It captures the **current fusion pipeline's fused
candidate output** — the exact candidate list the runtime would consume after
`fuse_evidence` / `fuse_evidence_from_crops` plus all heuristics and the
retained `uniform-list-row-grouping` operator — for every case in
`platforms/perception/tests/corpus/navigation_row_corpus.json` (28 cases).

The corpus is a deterministic fusion-level input set harvested from the
retained candidate's fixtures in
`tests/test_navigation_row_composition.py` (27 tests; construction helper code
copied with source-test citations in `tmp`-free corpus builder) plus a
constructed v1n false-positive viewport. Coverage:

- typical multi-row uniform lists and single rows (title/description/
  overlapping boxes → one row; scale stability at 0.5 / 1.0 / 1.5);
- same-text duplicate boxes in one frame (original IR-G0 shape);
- v1n low-anchor viewport (< 4 confirmed anchors) with the
  `Volume, vibration, Do Not Disturb`-style subtitle present — the retained
  candidate must fail closed: subtitle is NEVER a `menu_item`, real titles stay
  `text_block`, and only the 3 icon-confirmed rows become menu items;
- edge/clipped fragments, trailing controls, ambiguous midpoints,
  off-column text, irregular cadence, high inference ratios (fail-closed
  shapes), and the legacy per-crop fusion path.

## Determinism contract

- Candidate floats are rounded to 6 decimals; candidates are ordered by
  `(type, text, x1, y1, id)`; JSON is dumped with sorted keys, 2-space indent,
  LF, `ensure_ascii=False`, no timestamps.
- Same inputs ⇒ same bytes. Regeneration is a two-capture round-trip assertion:
  the test regenerates twice in memory and requires identical bytes.

## How to regenerate

Regeneration is explicit opt-in only — the default test run never rewrites the
baseline and never silently passes without it:

```bash
cd platforms/perception
P26_REGEN_BASELINE=1 ../../.venv-local-vision/bin/python -m pytest tests/test_row_composition_equivalence.py -q
```

If `baseline.json` is absent, the default run FAILS with these instructions.

## The S1B gate

`platforms/perception/tests/test_row_composition_equivalence.py` is the
permanent S1.7 regression gate: after S1B ports the pipeline into the operator
framework, this test must stay green **byte-identically**. Any byte difference
between the ported output and this baseline = S1 failure (STOP per the batch
ruling: zero behavior difference is required; do not hand-edit this baseline —
regenerate only after an intentional, authorized behavior change).
