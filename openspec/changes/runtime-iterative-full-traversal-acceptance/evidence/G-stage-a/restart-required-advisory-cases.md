# RestartRequiredAdvisoryCase Records — Stage G campaign (Phase 2.6)

Campaign source: `stageAB-adaptive-campaign.json` (4 independent real-emulator runs, 2026-08-27).
Case class: NORMALIZATION_AMBIGUITY (all observed cases share one type this campaign).

## Case 1 (SRC-26-NORM-1)

- **SourceRunId**: run-1 (strategy p26-adapt-r1)
- **EvidenceRefs**: `run:run-1:terminal`, `run:run-1:events`, `run:run-1:snapshot`;
  frames: `frames-stageAB.json` (per-run slice)
- **Runtime terminal reason**: `Failed: "Source normalization is unresolved; completeness
  cannot be proven."` (fail-closed at the root completeness proof, after real scroll dispatch)
- **Uncertainty type**: duplicate same-signature navigation occurrences within one frame —
  the Runtime cannot decide whether two identical `Text|PerceptionType` occurrences are (a)
  the same visual row detected twice by the perception fusion or (b) two genuinely distinct
  identical controls (e.g. two rows with the same label). Deciding (a) vs (b) changes the
  required-branch inventory; the frozen contract refuses to guess (fail-closed).
- **Hypothetical advisory question**: "Frame seq=N contains k identical navigation
  occurrences ('Network & internet', menu_item). Are these the same logical row (duplicate
  detection) or distinct rows?" — a UniAgent checkpoint could ask exactly this.
- **Allowable advisory answer type**: SAME_LOGICAL_SOURCE (merge, proceed) vs
  DISTINCT_SOURCES (fail or split inventory) — a typed semantic disposition, never a
  coordinate/action instruction.
- **Why Runtime could not decide alone**: its evidence contract deliberately excludes
  bounds/position from the vision identity key (STABLE SOURCE EQUIVALENCE KEY —
  `Text|PerceptionType` only), so within-frame occurrence disambiguation has no authorized
  input; the normalizer's duplicate-signature rejection is the graduated fail-closed
  behavior (PROV repair).
- **Restart actually required**: YES — every campaign round terminated; continuation was
  impossible without changing the observation content itself.

## Cases 2–3 (SRC-26-NORM-2 / SRC-26-NORM-3)

- **SourceRunId**: run-2 (p26-adapt-r2); run-3 (p26-adapt-r3)
- **EvidenceRefs**: `run:run-2:terminal|events|snapshot`; `run:run-3:terminal|events|snapshot`
- Identical class and structure to Case 1 (same terminal reason, same uncertainty type,
  same hypothetical question/answer type). Recorded separately because each is an
  independent reproduction across independent runs (round independence verified:
  distinct StrategyId/RunId per round).

## Case 4 (SRC-26-NORM-4, partial)

- **SourceRunId**: run-4 (p26-adapt-r4) — terminal identical; no PlanningRound was produced
  after it (round budget reached), so its knowledge candidate was not admitted; recorded
  here for completeness of the run-accounting.

## Campaign statistics (per §10 of the implementing instruction)

- RestartRequiredAdvisoryCase count: **4** (3 admitted-knowledge rounds + 1 terminal round)
- Types: NORMALIZATION_AMBIGUITY ×4 (single type this campaign — a traversable campaign
  would be expected to add UNKNOWN_ROLE, EXTERNAL_DISPOSITION, and DEPTH_BOUNDARY classes)
- Advisory-value assessment: in ALL 4 cases a single SAME_LOGICAL_SOURCE/DISTINCT_SOURCES
  answer at the first ViewportExplorationDecision would have allowed the run to continue
  without restart. This is genuine Buyer Evidence that a constrained advisory checkpoint
  could remove whole-run restarts for this failure class — BUT the underlying ambiguity is
  a perception-composition artifact (see STOP report); purchasing Assisted Exploration
  would treat the symptom, not the cause.

## Non-goals honored

No mid-run UniAgent consultation occurred; no advisory wire exists; no hypothetical answer
was counted as execution evidence; the campaign's runs all ran to their own terminals with
zero mid-run intervention (per-round autonomy assertions passed).
