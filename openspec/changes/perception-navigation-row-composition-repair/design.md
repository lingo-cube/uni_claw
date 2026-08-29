## Context

See `proposal.md` for the IR-G0 motivation. Live 1080×2400 Android 15 evidence shows that one Settings row can produce separate YOLO title, combined, and description boxes. Full-image OCR proximity matching assigns the same title/description tokens to several boxes, after which the current row-alignment heuristic promotes every aligned text candidate. Raw YOLO and OCR evidence are not duplicated; the multiplicity first appears in fusion.

Constraints are a single existing perception pass, no adapter or public Runtime-contract change, no VLM/Memory advisory, and preservation of fail-closed behavior, Vision-primary authority, and raw provenance. Narrow Runtime and campaign-buyer filtering may enforce that already-frozen source-tier boundary.

## Goals / Non-Goals

**Goals:**

- Turn provable row components into one primary navigation candidate.
- Keep repeated labels on different physical rows distinct.
- Preserve all raw evidence and contributing identifiers.
- Make ambiguous anchor assignment non-actionable.
- Recover one to three consecutive missing rows only when two confirmed navigation rows bracket every otherwise regular visual-list slot and every slot is unambiguous.
- Allow bounded current-frame continuation at viewport edges without using prior frames or hierarchy.
- Exclude incomplete viewport-edge rows from actionable inventory when their clipped geometry is proven against the frame-local row model.

**Non-Goals:**

- General document-layout understanding or semantic classification.
- YOLO retraining, threshold tuning, or a new model deployment.
- Runtime normalization tolerance, fuzzy identity, or auxiliary-defined identity.
- Toggle/local-control redesign, VLM fallback, or Memory.
- XML/UI-hierarchy dependence, content-meaning rules, variable-height/card/grid layout inference, three-anchor activation, or cross-frame state.

## Decisions

### 1. Compose by unique row anchor, never by text equality

The existing interactive row-widget detections remain the only row anchors. Each eligible text candidate is assigned to the nearest compatible anchor only when that nearest assignment is unique and within the existing bounded vertical alignment window. Candidates with a tied/ambiguous nearest anchor are not promoted.

Text equality is not a grouping key. This preserves two real rows with the same label when their anchors are distinct.

Alternative rejected: global same-text deduplication. It would solve the duplicate signature symptom but could merge genuinely repeated controls and would not stop descriptions from becoming independent sources.

### 2. Select one primary candidate, absorb proven subordinate components

Within a uniquely anchored group, candidate detector boxes are ordered deterministically by visual reading order: upper edge first, then tighter height, higher confidence, and stable candidate identifier. The first logical line becomes the primary title candidate. Detector candidates sharing OCR evidence with that primary are duplicate interpretations; lower, horizontally aligned candidates inside the same anchor group are subordinate row text. These proven components are removed from the fused candidate list, while their evidence identifiers are unioned into the primary candidate.

The primary candidate keeps its original title bounds so Runtime grounding targets visible title text. Candidate IDs are not renumbered; they remain frame-local evidence identifiers.

Alternative rejected: expand the primary bounds to the union of all row components. It makes tap geometry larger and can overlap adjacent controls without adding identity value.

### 3. Raw evidence is immutable; only fused candidates are composed

The `yolo` and `ocr` arrays are untouched. The primary candidate's `evidence.ocrIds` and `evidence.allIds` become deterministic de-duplicated unions of every absorbed component and anchor. The singular `yoloId` stays the primary detector identifier for compatibility; all additional detector identifiers remain in `allIds`.

Alternative rejected: add a new response schema or Runtime field. The existing provenance envelope is sufficient and avoids a cross-layer contract.

### 4. Apply after existing type heuristics and before response construction

Composition operates within the current fusion pass after search labeling and row alignment have identified candidate types, and before toggle inference returns the final candidate collection. The crop-OCR legacy path uses the same composition rule. No additional image/model pass is introduced.

### 5. Infer a frame-local uniform-list model from confirmed rows

After unique-anchor composition, confirmed `menu_item` title candidates provide a non-authoritative frame-local layout sample. The grouper may activate only when at least four confirmed titles prove a shared title column and a stable vertical cadence. Cadence is derived from robust statistics over the shorter adjacent gaps; it is never a fixed pixel constant. Direct gaps and one-slot doubled gaps must fit the derived cadence within a bounded relative deviation.

The model is recomputed independently for every screenshot. It is not Memory, is not carried across Runs, and does not alter Runtime depth, ledger, normalization, or completion behavior.

### 6. Recover only complete, uniquely bracketed cadence slots

When adjacent confirmed navigation titles are separated by two to four row pitches, the interior positions define one to three candidate row slots. Fusion may promote the bracket only when every interior slot has exactly one provable visual title group:

- its title column and center fit the frame-local model;
- the title choice is unique;
- no switch/toggle/checkbox/slider occupies the slot;
- no interior slot is missing or ambiguous;
- inferred roles do not outnumber confirmed rows and remain at or below 50 percent of the final row inventory.

The title and its proven duplicate/subordinate description components form one internal `RowGroup`; only the title candidate is emitted as `menu_item`, with unioned provenance. One-sided upper/lower continuation is allowed only after the same frame has at least four confirmed anchors, stays on consecutive cadence slots, stops at the first missing/ambiguous/control slot, and remains under the tighter 30 percent cap unless a multi-slot bracket already provides two-sided proof. Competing titles remain unchanged and non-actionable.

This is structural semantics only (`TitleOf`, `DescriptionOf`, `PreviousRow`, `NextRow`, `TrailingControlOf`). It never interprets text content such as `Apps`, `Battery`, or danger meaning.

### 7. Exclude proven partial edge rows from fused occurrences

The topmost or bottommost row may be omitted from fused occurrences only when at least four confirmed rows establish the normal title height/cadence and the edge title is materially clipped or an edge slot remains ambiguous. Normal-height complete edge rows remain untouched. Raw YOLO/OCR evidence is retained; the rule prevents a partial OCR fragment from entering source normalization before a complete frame observes it.

### 8. Keep primary evidence and downstream buyers explicit

The Settings semantic capability may mark a second primary visual box as `NonInteractive` only when it has identical text, overlapping bounds, and exactly one primary `menu_item` peer. It never promotes ordinary `text_block` evidence to navigation.

When explicit source metadata declares primary Vision, `SourceEquivalenceNormalizer` excludes auxiliary-only occurrences from completeness identity. The Settings campaign likewise filters `EligibleForAuthorization` before inventory and branch creation. XML remains fresh same-frame corroboration/diagnostic evidence and cannot become an action or traversal source.

## Risks / Trade-offs

- **[Risk] A missing anchor for a neighboring row could make its description appear compatible with another anchor.** → Require unique nearest-anchor assignment plus bounded horizontal/vertical subordinate geometry; otherwise retain raw non-actionable evidence and do not compose.
- **[Risk] Primary selection could choose a combined box rather than the title box.** → Prefer upper edge and tighter height before confidence, with reality fixtures for title+description rows.
- **[Risk] Removing secondary fused candidates reduces candidate count.** → This is intentional at the fused evidence boundary; the immutable raw YOLO/OCR arrays and unioned provenance preserve diagnostic evidence.
- **[Risk] An unseen layout has fewer than four reliable row anchors.** → The operator stays inactive. A tested three-anchor relaxation produced description-as-menu false positives and was reverted.
- **[Risk] A static note between navigation rows could resemble a missing row.** → V1 requires a proven uniform navigation segment, exact one-slot bracketing, title-column fit, unique title, no trailing control, and a bounded inferred-row ratio; any failed predicate leaves the text non-actionable.
- **[Risk] Variable-height lists or section breaks could produce false cadence.** → Require stable direct-gap support and reject gaps that are not one or two pitches. No extrapolation across section/card/grid layouts.
- **[Risk] A short but complete edge title could be demoted.** → Edge demotion requires an abnormal title-height ratio against at least four complete peer rows; normal-height first/last rows remain actionable.

## Migration Plan

1. Capture before evidence from the current production pipeline and freeze the duplicate/description falsifier.
2. Add fast geometry/provenance tests, then implement the bounded composition helper.
3. Run the complete perception suite and production pipeline against live Settings root/subpage screenshots.
4. Exercise V1 first in deterministic falsifiers, then re-run the existing real-emulator Phase 2.6 campaign with the frozen primary-source boundary enforced.
5. Roll back by reverting only this change's perception helper/call-site edits if any falsifier or existing regression fails.
