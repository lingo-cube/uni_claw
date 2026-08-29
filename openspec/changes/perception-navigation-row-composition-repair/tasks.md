## 1. Baseline and Falsification

- [x] 1.1 Persist the live Settings root before-capture and a compact fused-candidate/provenance report for IR-G0.
- [x] 1.2 Record the Human Gate authorization and verify Runtime production files are unchanged before implementation.

## 2. Test-First Row Composition

- [x] 2.1 Add fast tests for title plus description plus overlapping boxes producing one primary row candidate.
- [x] 2.2 Add falsifiers for title-only rows, repeated labels on distinct anchors, tightly adjacent rows, ambiguous anchor assignment, and provenance union.
- [x] 2.3 Add a production-pipeline reality regression using captured Android Settings evidence.

## 3. Perception Repair

- [x] 3.1 Implement deterministic unique-anchor grouping and primary-title selection in the fusion heuristic.
- [x] 3.2 Remove proven subordinate/duplicate row components from fused candidates while retaining raw YOLO/OCR and unioned provenance.
- [x] 3.3 Apply identical composition behavior to the full-image and legacy crop fusion paths without adding a model pass.

## 4. Validation

- [x] 4.1 Run focused and full Python perception tests plus strict OpenSpec validation.
- [x] 4.2 Re-run production perception on live Settings root and subpage captures; prove one actionable candidate per row and no description-only candidates.
- [x] 4.3 Run repository build/tests/consistency checks and verify Runtime/Agent/FSM/Traversal diffs remain empty.
- [x] 4.4 Re-run the existing Phase 2.6 real-emulator campaign through its authorized gates and record whether IR-G0 is closed or a new Human Gate is required.

## 5. Documentation Sync

- [x] 5.1 Complete the Knowledge System documentation-sync checklist and record UPDATE/NO_CHANGE decisions.
- [x] 5.2 Update this change's implementation evidence and task status without altering Phase 2 graduation or Runtime authority.

## 6. Uniform Vertical List Row Grouping V1 (Human-approved continuation)

- [x] 6.1 Record Human approval to try the perception-only layout rule and stop if side effects become broad or uncontrollable.
- [x] 6.2 Extend proposal/design/spec with frame-local activation, bounded bracket recovery, control exclusion, edge clipping, and fail-closed boundaries.
- [x] 6.3 Add test-first falsifiers for bracketed recovery, description grouping, local controls, irregular layouts, consecutive gaps, ambiguity, inference cap, and edge rows.
- [x] 6.4 Implement perception-internal uniform-list grouping without XML, VLM, Memory, Adapter, or public schema changes; enforce existing primary-source eligibility in Runtime/campaign consumers.
- [x] 6.5 Run focused perception and .NET boundary tests, strict OpenSpec, repository consistency/build/tests, and scoped authority diff.
- [x] 6.6 Recompute isolated candidate identities and re-run real-emulator campaigns; stop after the three-anchor relaxation produced a description-as-menu false positive.

## Validation Receipt (2026-08-27, final candidate evaluation)

- Focused row/grouping suite after reverting unsafe sparse-anchor activation: `27 passed, 3 subtests passed`.
- Focused Runtime/Settings semantic/campaign-buyer boundary suite: `28/28` PASS.
- Multiple isolated real-emulator candidates proved the root list can reach `About emulated device`; normalization and unknown-affordance failures moved as the frame-level row recall varied.
- The experimental three-anchor relaxation was not retained: candidate `v1n` promoted subtitle `Volume, vibration, Do Not Disturb` as a menu item and omitted several real titles in the same frame.
- Every campaign admitted one fresh run exactly once, had zero mid-run intervention, and kept the four authority invariants and campaign gates green; Runtime terminal remained honestly `Failed` (`Source normalization is unresolved` or `Unknown interaction affordances remain`).
- XML remained auxiliary-only. No VLM, Memory, UniAgent action, fuzzy Runtime identity, canonical deployment promotion, or Phase 2 authority change occurred.
- Remaining full-suite/consistency/OpenSpec receipts are recorded in `evidence/IMPLEMENTATION-RESULT.md`; the decision remains `HUMAN_GATE_REQUIRED`.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `platforms/perception/` | `docs/architecture/vision.md` |
| `platforms/perception/tests/` | `docs/architecture/vision.md` |
