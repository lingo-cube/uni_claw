# Layout-Clustering Feasibility Validation (offline, evidence-only — no code changed)

Question (Human): can in-frame layout clustering resolve IR-G0?

**Answer: YES on the captured real frames — 30/30 duplicate navigation occurrences
clustered away, 0 distinct rows mis-merged, with a >5x separation margin.** Implementation
requires a production-perception change (STOP option a) — outside this change's
authorization.

## Method

Replayed the captured real frames (`frames-stageAB.json`, 13 frames, both AVDs) through a
candidate fusion-layer dedup rule:

> Same frame + same type (`menu_item`) + same text + **same visual row** — where "same
> row" = vertical overlap OR vertical gap < half the taller box, AND same column
> (left-edge within 0.05 or horizontal overlap > 30% of the narrower box).

## Results

| Metric | Value |
|---|---|
| Duplicate nav occurrences before | 30 (across all frames) |
| After row-clustering | **0** — signature uniqueness ACHIEVED on every captured frame |
| Same-text duplicate-pair vertical gaps | [0.0000, 0.0100] (n=28) |
| Different-text adjacent-row vertical gaps | p10 = 0.0531, max = 0.6506 (n=215) |
| Pairs the rule could mis-merge | **0** — the only 4 cross-text pairs with gap ≤ 0.010 have DIFFERENT text ('System' vs its caption; 'Location' vs its caption), and text equality is a rule precondition |

## Why the geometry is this clean

The duplicates are the fusion layer assigning the same OCR text to the row's composite
box AND its title/caption sub-boxes (E5): duplicates are vertically nested/adjacent
within one list row (gap ≤ 0.01), while genuine Settings list rows are separated by
≥ 0.05 — a >5x margin. The rule keys on ROW identity (geometry), not on text alone, so
genuinely distinct same-named rows in different positions are never merged.

## Why this does NOT violate the frozen STABLE SOURCE EQUIVALENCE KEY contract

The contract excludes bounds from **cross-frame identity** so a row keeps its identity
while scrolling. Row clustering is **intra-frame only**: it decides whether two boxes in
ONE frame belong to the same visual row (a within-frame geometric fact that cannot drift
across frames). Cross-frame identity remains `Text|PerceptionType`. The two concerns are
orthogonal — intra-frame dedup does not weaken scroll invariance.

## Implementation shape (for the Human Gate, option a)

- Location: `platforms/perception/uniclaw_perception/fusion/heuristics.py` — a dedup pass
  next to the existing switch/toggle IoU≥0.6 dedup (:359) and raw-pixel region dedup (:542);
  applies to navigation-row candidates (menu_item).
- Conservative form: merge same-text same-type same-row boxes into ONE candidate; record
  the merged boxes as evidence on the survivor (e.g. `mergedFrom` list) — no truth lost.
- Verification assets already exist: this offline validation script's rule (as a unit
  test over the archived frames), the standalone perception probe, and the real-emulator
  campaign re-run (`settingscampaign … --adapt`).
- Estimated size: small (one fusion pass + tests); no Runtime/contract change.
