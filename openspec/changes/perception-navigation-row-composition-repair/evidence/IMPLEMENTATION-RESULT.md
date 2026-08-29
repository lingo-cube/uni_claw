# PROJECT_LEADER_IR_G0_PERCEPTION_ROW_COMPOSITION_IMPLEMENTATION_RESULT

## Decision

`PARTIAL_REPAIR_PROVEN__UNSAFE_EXPANSION_REVERTED__HUMAN_GATE_REQUIRED`

IR-G0 is not closed. Phase 2.6A remains blocked and Phase 2.6B was not entered.
The canonical perception deployment was not promoted.

## Implemented and Retained

- Same-frame title/description/overlapping detector boxes compose into one
  provenance-preserving navigation row.
- A frame-local row-relation operator derives adaptive cadence and title column
  from at least four confirmed rows; it supports complete one-to-three-slot
  brackets plus bounded edge continuation.
- Trailing controls, ambiguous slots, irregular layouts, and clipped edge
  fragments fail closed. Raw YOLO/OCR arrays remain immutable.
- Exact same-text, overlapping primary visual duplicates of one unique
  `menu_item` are classified `NonInteractive`, never as a second action source.
- Explicit primary Vision remains the completeness/authorization source;
  auxiliary hierarchy rows cannot define Runtime normalization or campaign
  traversal branches.
- Search/title dispositions were narrowed in the existing Settings semantic
  capability. No ordinary `text_block` is promoted merely because it has text
  and bounds.

## Reverted Experiment

The operator was temporarily allowed to activate from three YOLO row anchors
and infer up to two observed text roles per anchor. Candidate `v1n` then emitted
the subtitle `Volume, vibration, Do Not Disturb` as a `menu_item` while omitting
real titles. This was a real-emulator false positive, not a synthetic concern.

The three-anchor activation and 2/3 inference cap were removed. The retained
operator again requires four confirmed anchors and never infers more roles than
confirmed rows.

## Reality Result

The original diagnosis was correct: the provider treated multiple visual
interpretations and child descriptions as independent menu items. That defect
is repaired when sufficient row anchors exist.

The remaining first divergence is row-anchor stability in primary perception.
The current detector can drop below four reliable anchors in a viewport. A
geometry-only operator then has two choices: remain fail-closed, or guess from
OCR layout. Real evidence proved the latter unsafe.

Text semantic similarity is not a safe replacement. Subtitles may be related,
unrelated, stateful, localized, or dynamic. The recommended future direction is
a dedicated visual Row Grouping / Relation Head whose output is checked by the
deterministic operator. Text semantics may veto or lower confidence, but must
not create an actionable menu row.

## Validation

- Focused row composition/grouping: `27 passed`, `3 subtests passed`.
- Focused Runtime normalizer + Settings semantic + campaign buyer: `28/28` PASS.
- Perception tests (isolated from governance mutation tests): `81 passed`,
  `1 failed`, `3 subtests passed`. The failure is the pre-existing RPER-06
  source assertion expecting Adapter rewriting from `switch` to `toggle`; this
  change does not modify the Adapter.
- Vision governance: `48 passed`, `1 failed`. RSI08 is the expected canonical
  convergence rejection because candidate source differs from CURRENT ACTIVE.
- Runtime solution build: PASS, `0 warnings`, `0 errors`.
- Semantic .NET suite: `32/32` PASS.
- Runtime .NET suite: `2215/2220` PASS. Three failures are expected Vision
  identity-convergence gates while promotion is withheld; the other two are
  real-emulator scenario/environment failures (Capstone terminal `Failed` and
  permission-controller boundary not observed).
- `scripts/check-consistency.sh`: ALL PASS.
- Strict OpenSpec validation: PASS.
- `git diff --check`: PASS.
- A combined perception+governance pytest invocation is order-dependent because
  governance tests temporarily replace model/source artifacts; its temporary
  model-path errors were excluded and both suites were rerun separately.

## Real-Emulator Evidence

- Multiple isolated candidates traversed the Settings root from its top to
  `About emulated device` and moved the failure between exact normalization and
  residual unknown-affordance gates as perception varied.
- Each campaign admitted exactly one fresh run, performed zero mid-run
  intervention, and kept campaign authority invariants/gates green.
- Runtime terminal remained honestly `Failed`; no campaign result was relabelled
  as successful.
- Negative side-effect evidence is frozen in
  `evidence/after/candidate-v1n-campaign-round1-frames.json`.

## Deployment Receipts

- Final retained safe shadow candidate:
  `deploy:e42452710efad076881e25dffb948221513a2f1a3dc20b45ebb1b4de23127ea7`.
- Final retained PipelineRevision:
  `prev:837a4ea88d244518a509afbabd8814682eb20e1622772c7dbd8760c99268a809`.
- Canonical CURRENT ACTIVE receipt SHA-256 remains
  `9d7f80d7d5745c4058bef9a46e390397a7819b4332653ca2b0fdbbbf9218cbc6`.
- Canonical promotion: `NOT_AUTHORIZED_NOT_PERFORMED`.

## AuthorityDelta

`NONE`

The Runtime source change filters auxiliary-only occurrences when an explicit
primary Vision source exists. This enforces the already-frozen source authority;
it does not make a new source authoritative or relax fail-closed completeness.

## ArchitectureDelta

`NONE` for retained work. A new row-grouping model/relation-head contract would
be a future Human-gated ArchitectureDelta and requires a separate OpenSpec.

## Remaining Risk

- Primary row-anchor recall remains viewport-sensitive.
- OCR/title-role instability still breaks exact ordered overlap.
- Geometry-only low-anchor recovery creates false menus.
- Existing fixed label space has no stable navigation-row relation role.

## Stop Condition

Triggered and honored: further geometric relaxation caused an uncontrolled
description-as-menu side effect. Continue only after Human selects detector
retraining, a dedicated visual row-relation capability, or acceptance of the
current fail-closed boundary.
