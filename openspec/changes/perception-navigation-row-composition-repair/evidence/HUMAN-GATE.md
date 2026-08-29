# IR-G0 Stable Navigation Row Evidence Human Gate

Date: 2026-08-27

## Decision

`HUMAN_GATE_REQUIRED__CANDIDATE_NOT_PROMOTED`

The same-frame duplicate/description composition defect is repaired, and a
four-anchor frame-local row-relation operator materially improves the real
Settings root traversal. It is not sufficient to close IR-G0 because the
current YOLO deployment sometimes leaves only three reliable row anchors.

An experimental three-anchor relaxation was evaluated and then reverted. It
produced a real description-as-menu false positive, which meets the Human stop
condition for broad or uncontrollable side effects.

## Evidence -> Ownership -> Boundary -> Decision

### Evidence

- Candidate `v1m` produced stable root navigation rows across most viewports and
  passed exact source normalization, but Runtime failed closed on residual
  unknown affordances.
- Candidate `v1n` removed that residual through an exact same-text/overlap
  duplicate disposition, but a low-anchor viewport emitted
  `Volume, vibration, Do Not Disturb` as `menu_item` while omitting real titles
  such as `Display`, `Wallpaper`, and `Accessibility`.
- The false positive occurred with fresh primary Vision evidence. XML was not
  used to create or authorize the row.
- Every run remained autonomous after admission and Runtime rejected incomplete
  evidence honestly.

### Ownership

The remaining gap is primary perception row-role evidence quality. It is not:

- Exploration Memory;
- UniAgent planning;
- Runtime state, FSM, Traversal, Ledger, or Completion logic;
- a reason to make Android hierarchy authoritative;
- a reason to introduce fuzzy Runtime identity.

### Boundary

The retained deterministic operator activates only with at least four confirmed
row anchors. Lowering that requirement is unsafe with the current detector/OCR
distribution. Closing the gap now requires one of the following separately
authorized perception directions:

1. Improve/retrain the existing detector so Settings-style row anchors have
   stable recall across scroll positions.
2. Add a dedicated visual row-grouping model or relation head whose output is
   verified by the deterministic geometry operator and carries provenance.
3. Accept the present fail-closed boundary and defer Phase 2.6A.

Changing Runtime normalization or using XML as canonical identity is not
recommended because it would move perception uncertainty into an authority
layer and conflict with the Vision-primary boundary.

### Recommendation

Evaluate Option 1 first. The deterministic row operator is useful as a verifier
and composition layer, but it should consume more stable row anchors rather than
become an OCR-only menu classifier. Option 2 is viable if retraining cannot
deliver adequate recall, but it needs a new model/deployment contract, latency
budget, provenance rules, unavailable behavior, and cross-UI falsifiers.

## Frozen Results

- Three-anchor activation: `REVERTED_UNSAFE`.
- Four-anchor deterministic row operator: `RETAINED_CANDIDATE_ONLY`.
- Canonical perception deployment: `UNCHANGED_NOT_PROMOTED`.
- Runtime fail-closed semantics: `UNCHANGED`.
- Phase 2 Graduation: `UNCHANGED`.
- Phase 2.6B: `NOT_ENTERED`.

## Human Questions

1. Authorize detector retraining/evaluation for stable navigation-row anchors?
2. If retraining is insufficient, authorize a dedicated visual row-grouping
   model evaluation under a new OpenSpec?
3. Or accept the boundary and keep Phase 2.6A blocked?

## AuthorityDelta

`NONE`

## ArchitectureDelta

`NONE` for retained changes. Any new model/relation-head boundary requires a
separate Human Gate and OpenSpec.
