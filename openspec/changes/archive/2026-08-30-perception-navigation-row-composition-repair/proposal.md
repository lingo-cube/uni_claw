## Why

Real Android Settings evidence falsifies the current navigation-row fusion assumption: YOLO emits separate title, description, and overlapping text boxes for one physical row, and the fusion heuristic promotes each aligned box to an independent `menu_item`. This creates duplicate navigation signatures and description-only navigation sources, so the graduated Runtime correctly fails closed before bounded traversal can begin.

Human authorization for the perception-side repair was granted after IR-G0 evidence confirmed that the first divergence is inside production fusion rather than Runtime normalization.

## What Changes

- Compose widget-aligned title, description, and overlapping detector boxes into one logical navigation-row candidate.
- Select one deterministic primary title/bounds for the row and preserve contributing raw YOLO/OCR identifiers as provenance.
- Prevent a row description from becoming an independent actionable `menu_item`.
- Keep spatially distinct rows, including repeated labels, distinct when they do not share a row anchor.
- Recover one to three consecutive, uniquely bracketed missing rows inside a visually proven uniform vertical navigation-list segment by using that frame's adaptive row cadence and title column.
- Permit tightly bounded upper/lower continuation only after the same frame proves the list model; inferred roles never exceed confirmed anchors and never exceed 50 percent of the final row inventory.
- Treat row cadence as grouping evidence rather than content meaning; exclude trailing-control rows, ambiguous slots, and partial viewport-edge rows.
- Classify an overlapping same-text primary visual box as a non-interactive duplicate only when one unique primary `menu_item` proves the same row.
- Enforce the existing Vision-primary boundary in Runtime normalization and the campaign buyer: auxiliary hierarchy rows remain diagnostic/corroborating evidence and cannot define completeness identity or traversal branches.
- Preserve ambiguous evidence as fail-closed; do not merge candidates without sufficient same-row evidence.
- Add deterministic falsification tests plus live Settings pipeline evidence across root/subpage captures.
- Do not invoke LLM/VLM, add a second perception pass, relax Runtime fail-closed semantics, or alter Agent/FSM/Traversal authority.

## Capabilities

### New Capabilities

- `perception-navigation-row-composition`: Deterministic, provenance-preserving composition of Settings-style navigation-row evidence into one candidate per provable physical row.

### Modified Capabilities

None.

## Impact

- Production: perception-internal row grouping, narrow Settings semantic duplicate disposition, and primary-tier filtering in `SourceEquivalenceNormalizer`.
- Validation buyer: Settings campaign inventory/branch selection filters auxiliary-only occurrences.
- Tests: fast synthetic fusion regressions and production-pipeline reality checks under `platforms/perception/tests/`.
- Evidence: IR-G0 before/after artifacts under this change and re-execution of the existing real-emulator acceptance campaign.
- No adapter, Agent, FSM, Traversal, GoalEvidence, ExplorationLedger, authority-boundary, or public Runtime contract change.
