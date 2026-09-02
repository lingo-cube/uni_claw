# P26-V2 Run-6 Residual Repairs — Sticky Label Demotion + Twin Cleanup

STATUS: `BOTH_RESIDUALS_CLOSED_REGRESSION_GREEN_FAST_BASELINE_FROZEN`

Date: 2026-09-02

Closes the two residuals recorded in
`P26-V2-LABEL-HEIGHT-RULE-RESULT.md` §Residuals (owner already established:
perception channel; deterministic; no Runtime change).

## Residual 1 — cross-frame height jitter role flip (run 6 seq 24–25)

- **Expected Reality**: once the perception channel has demoted a
  small-font in-column line ('Color') to a NonInteractive section label, the
  same container's later frames keep it non-actionable even when per-frame
  detection-height jitter moves the raw box height across the 0.75 ratio.
- **Observed Reality**: 'Color' briefly recomposed as a phantom `menu_item`
  for two frames (per-frame detection-height jitter), then flipped back.
- **Reality Gap**: the label-height demotion is recomputed from scratch each
  frame — no memory of the established classification.
- **First Divergence**: the jittered frame where 'Color' height ≥ 0.75 ×
  neighbor recomposed it as a row despite the established demotion.
- **Owner**: perception fusion assembly + the existing caller-supplied
  cross-frame context channel (C# `RowIdentityContext` / `X-Known-Rows`).
  The operator stays pure (frozen inputs G-4 / determinism G-7); the server
  stays stateless.
- **Fix**: *sticky label demotion* rides the existing context channel.
  - C# `RowIdentityContext` records each known row's LATEST upstream
    `PerceptionType` and exports it as an additive `type` field in the
    X-Known-Rows header (memory only — never changes identity, never grants
    actionability, never leaks across container domains).
  - Python `_apply_sticky_label_demotion` (engine assembly, after column
    promotion, before row-band supporting ownership): a composed or
    column-promoted `menu_item` whose normalized text UNIQUELY matches a
    known row whose latest type is `NonInteractive` is re-demoted in place
    to a NonInteractive `section_label` satellite
    (`typeInferred: sticky_label_demotion`, `knownRowId` recorded,
    diagnostics `stickyLabelDemotion`).
  - Fail-closed: multiple known ids for the same text, or any non-
    NonInteractive sighting of the same id ⇒ ambiguous ⇒ never demoted
    (mirrors the stabilizer's unique-match discipline). No context ⇒ no-op
    (single-frame baseline byte-identical). Worst case is a withheld action
    (fail-closed), never a fabricated one.

## Residual 2 — demoted candidate raw-twin cleanup (run 6 seq 27+)

- **Expected Reality**: a demoted section label has exactly ONE
  representation in the published candidates (the NonInteractive
  `section_label` satellite).
- **Observed Reality**: the raw 'Color' `text_block` candidate from initial
  construction floated next to the satellite (both 'Color' text_block and
  NonInteractive).
- **First Divergence**: `_assign_row_band_supporting_ownership` had no rule
  matching a label line ABOVE its row (fix #1 needs center-Y inside the
  band extent; fix #2 needs a parent row ABOVE the text_block).
- **Owner**: perception fusion engine `_assign_row_band_supporting_ownership`.
- **Fix (v2, after falsification — see below)**: *duplicate section-label
  dedup* — a text_block whose normalized text, title column, and vertical
  extent coincide with an EXISTING NonInteractive `section_label` satellite
  (operator label-height rule OR sticky demotion output) is absorbed as
  `duplicate_section_label_supporting` of that satellite (annotated, removed
  from independent emission). One line, one representation.

### Design falsification recorded (evidence-first discipline)

The first implementation followed the prescription "a label ABOVE its row
needs the symmetric attachment" as a GENERAL geometric rule (small-font
text_block above a row within `label_pair_gap_ratio` gap attaches as
`row_label_supporting`). The frozen S1 corpus **falsified it**:
`uniform_list_ambiguous_midpoint_rejected` — two distinct short texts in one
cadence slot that must stay unresolved ('Candidate A' h=20 y=[275,295],
'Candidate B' h=20 y=[305,325], row 'Confirmed 3' h=30 y=[385,415]; gap 60 ≤
3.0×30, height 20 < 0.75×30) — is geometrically indistinguishable from the
real 'Color'-above-'Colors' shape, and the general rule wrongly absorbed the
pair into the row below (3 frozen-baseline gate failures: S1 ZERO-DIFF).
Only the presence of the operator's role-decided satellite separates a
duplicate representation from a genuine unresolved element, so the rule was
narrowed to satellite-coincidence dedup. The frozen baseline is byte-stable
again.

## VALIDATED

- New focused tests (RED→GREEN proven for BOTH fixes by disable-and-rerun):
  - `tests/test_p26_residual_repairs.py` (engine-level, real run-2/6
    Display-page geometry; flip frame h=20 reproduces the phantom
    composition without context):
    - sticky: known-NonInteractive context re-demotes; no-type / menu_item-
      type / multi-id / mixed-type contexts all fail closed; settled-frame
      demotion unchanged with context; flip frame composes without context
      (control).
    - twin: demoted label ends with exactly ONE 'Color' representation
      (NonInteractive section_label); absorption annotated
      (`duplicate_section_label_supporting`); rows below still compose.
  - `tests/test_row_band_ownership_and_cadence_consensus.py`
    `DuplicateSectionLabelTests` (unit-level: absorption, the S1 corpus
    falsifier pair stays unresolved, different-text / different-position /
    off-column satellites never match).
  - C# `RowIdentityContextDomainTests` +5 sticky-type tests (NonInteractive
    export, latest-sighting-wins, interactive-not-sticky, no cross-domain
    leak, Reset clears).
- Full frozen regression:
  - perception pytest: **384 passed + 95 subtests**; unittest: **337 OK**.
  - S1 frozen baseline gates (`test_row_composition_equivalence`,
    `test_operator_pipeline_wiring` byte-equal + trace replay):
    **byte-stable** with the final design.
  - `dotnet build src/UniClaw.Runtime.sln`: 0 errors.
  - `scripts/check-consistency.sh`: ALL PASS (C1–C15).
  - Full `dotnet test src/UniClaw.Runtime.sln`: recorded in the campaign
    log (running at write time; RowIdentityContext focused set 20/20).

## FAST_BASELINE_FROZEN

Per the authorized execution order, with both residuals closed and the
focused + frozen regression green, the Fast-only perception baseline is
hereby FROZEN for this campaign: **no further special-case perception rules
are added per new variance instance.** New blockers observed in fresh runs
are recorded with evidence, first divergence, and owner only.

## Artifacts

- `platforms/perception/uniclaw_perception/fusion/engine.py`
  (`_apply_sticky_label_demotion`, `_duplicate_section_label`, wiring,
  diagnostics `stickyLabelDemotion` / `rowBandSupporting`)
- `src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/RowIdentityContext.cs`
  (`_idToType`, additive `type` export)
- Tests: `test_p26_residual_repairs.py` (new),
  `test_row_band_ownership_and_cadence_consensus.py`
  (`DuplicateSectionLabelTests`),
  `tests/UniClaw.Runtime.Tests/ValidationHarness/RowIdentityContextDomainTests.cs`
- Live validation: pending the next fresh campaign rounds (Shadow stage);
  governance identity receipt must be rebuilt before live runs (perception
  source changed ⇒ pipelineRevision changes).

## RISKS

- Sticky demotion is text-unique within a container domain: a genuinely NEW
  interactive row appearing later with the exact same text as an
  established non-interactive label would be withheld (fail-closed miss,
  never a phantom action). Recorded as the accepted worst case.
- The `type` field is latest-sighting-wins; a flapping classification
  alternates the sticky evidence frame-by-frame (mixed sightings fail
  closed only within one header; across frames the latest wins). The
  residual class this targets (detection-height jitter) is closed because
  the sticky demotion keeps the C# side seeing NonInteractive.

## NEXT_WORKITEM

Wire the Slow Shadow experiment (SLOW_SEMANTIC_SHADOW_APPROVED_BOUNDED):
harness-side Shadow replication of the V2 lifecycle with a concrete
provider, revision-bound inputs, zero production Runtime effect, and the
required metric ledger — then fresh campaign rounds.
