# perception-navigation-row-composition Specification

## Purpose

Provide deterministic, provenance-preserving composition of Settings-style row evidence so downstream consumers receive one actionable navigation candidate for each physical row that perception can prove.

## Requirements

### Requirement: One logical navigation candidate per provable physical row

The perception fusion output SHALL contain exactly one actionable `menu_item` candidate for a physical navigation row when title text and a compatible interactive row anchor establish that row. Overlapping detector interpretations and subordinate description lines belonging to that row SHALL NOT be emitted as independent actionable candidates.

#### Scenario: Title and description share one row anchor

- **WHEN** one row anchor aligns with a title line, a subordinate description line, and one or more overlapping detector boxes
- **THEN** fusion SHALL emit exactly one `menu_item` for that row
- **AND** the emitted candidate text SHALL be the primary title rather than the description

#### Scenario: Title-only row remains valid

- **WHEN** one row anchor aligns with one unambiguous title line and no description line
- **THEN** fusion SHALL emit exactly one `menu_item` for that row

### Requirement: Distinct rows are not collapsed

Fusion SHALL retain separate candidates for spatially distinct physical rows. Text equality alone SHALL NOT be sufficient evidence to compose or deduplicate rows.

#### Scenario: Repeated label on different row anchors

- **WHEN** two spatially distinct row anchors each align with a separate OCR occurrence containing the same text
- **THEN** fusion SHALL emit one `menu_item` for each row
- **AND** SHALL NOT merge the two rows because their labels are equal

#### Scenario: Tightly adjacent rows

- **WHEN** two nearby title lines are each uniquely associated with different row anchors
- **THEN** fusion SHALL preserve two independent `menu_item` candidates

### Requirement: Ambiguous row membership fails closed

Fusion SHALL NOT promote text to an actionable row solely from an ambiguous anchor assignment or an ambiguous primary-title choice. It SHALL preserve the underlying raw evidence without inventing a row composition.

#### Scenario: Text is equally compatible with multiple anchors

- **WHEN** a text candidate cannot be uniquely assigned to one row anchor
- **THEN** fusion SHALL NOT promote that text candidate to `menu_item` through row composition
- **AND** SHALL NOT merge the competing anchors

### Requirement: Uniform-list inference is frame-local and gated

Fusion MAY infer a uniform vertical navigation-list model only from confirmed actionable rows in the current screenshot. It SHALL require a stable title column, stable adaptive cadence, and sufficient confirmed-row support. It SHALL NOT use fixed device pixels, XML/UI hierarchy, historical frames, Memory, or content meaning to activate the model.

#### Scenario: Stable navigation list activates grouping

- **WHEN** at least four confirmed navigation rows establish a shared title column and stable direct row cadence in one screenshot
- **THEN** fusion MAY activate frame-local uniform-list grouping for that segment
- **AND** SHALL derive cadence from those current-frame rows

#### Scenario: Variable-height or irregular layout remains inactive

- **WHEN** confirmed rows do not establish a stable cadence or title column
- **THEN** fusion SHALL NOT promote unanchored text through uniform-list grouping

### Requirement: Only complete bounded bracket gaps may be recovered

Fusion SHALL promote unanchored visual title groups only when two confirmed navigation rows bracket one to three missing cadence slots, exactly one title group fits every slot, no trailing local control occupies a slot, inferred roles do not outnumber confirmed rows, and the frame-wide inferred-row ratio remains at or below 50 percent.

#### Scenario: Complete bounded gap between confirmed neighbors

- **WHEN** two confirmed navigation rows are separated by two to four frame-local row pitches
- **AND** one uniquely aligned visual title group occupies every interior cadence slot
- **THEN** fusion SHALL emit one inferred `menu_item` for each interior title group
- **AND** SHALL retain an evidence reason identifying uniform-list bracket inference

#### Scenario: Incomplete consecutive gap fails closed

- **WHEN** any interior cadence slot is missing or ambiguous
- **THEN** fusion SHALL NOT promote any text in that bracket through grouping

#### Scenario: Trailing control blocks navigation inference

- **WHEN** a candidate slot contains a switch, toggle, checkbox, or slider
- **THEN** fusion SHALL NOT infer that row as a navigation `menu_item`

#### Scenario: Ambiguous midpoint remains non-actionable

- **WHEN** more than one independent title group fits the missing slot
- **THEN** fusion SHALL leave all competing candidates non-actionable

### Requirement: Proven partial edge rows do not enter inventory

Fusion SHALL leave a topmost or bottommost row non-actionable when a sufficiently supported frame-local row model proves that its visible title geometry is materially clipped. It SHALL NOT demote a normal-height edge row.

#### Scenario: Clipped top row is excluded

- **WHEN** the topmost row title is materially shorter than at least four complete peer titles and occupies the viewport edge slot
- **THEN** fusion SHALL preserve its raw evidence but SHALL NOT emit it as `menu_item`

#### Scenario: Complete top row remains actionable

- **WHEN** the topmost row has normal title geometry relative to its peers
- **THEN** fusion SHALL preserve its existing actionable classification

### Requirement: Continuation is current-frame bounded

Fusion MAY classify observed title groups immediately above or below the confirmed segment only when the current frame proves at least four anchors, every continuation slot is consecutive and unambiguous, no local control occupies it, and the tighter continuation inference cap is satisfied. It SHALL stop at the first failed slot and SHALL NOT use prior frames.

#### Scenario: Complete lower continuation

- **WHEN** a proven list has a unique title group at the next cadence slot
- **AND** the inferred-role cap remains satisfied
- **THEN** fusion MAY emit that group as `menu_item`

#### Scenario: Three-anchor frame remains inactive

- **WHEN** fewer than four confirmed actionable rows remain in a frame
- **THEN** uniform-list inference SHALL remain inactive

### Requirement: Composition preserves evidence provenance

The selected row candidate SHALL retain the identifiers of every YOLO detection, OCR occurrence, and row anchor used in its composition. The raw `yolo` and `ocr` evidence collections SHALL remain unchanged by row composition.

#### Scenario: Multiple detector boxes describe one row

- **WHEN** title, description, and overlapping detector candidates are composed into one row
- **THEN** the row candidate evidence SHALL include the union of their contributing evidence identifiers
- **AND** the raw YOLO and OCR records SHALL remain available unchanged

### Requirement: Perception-only deterministic processing

Navigation-row composition SHALL use the existing screenshot, YOLO detections, OCR occurrences, and deterministic geometry in the existing fusion pass. It SHALL NOT invoke LLM/VLM, capture a second screenshot, run a second model pass, or depend on historical Memory.

#### Scenario: Production Settings analysis

- **WHEN** the production perception pipeline analyzes a Settings screenshot
- **THEN** exactly the existing single YOLO pass, single OCR pass, and single fusion pass SHALL be used
- **AND** no Runtime, Agent, Memory, or advisory call SHALL participate in row composition

### Requirement: Runtime authority remains unchanged

The repair SHALL NOT relax Runtime normalization, action authorization, FSM, Traversal, GoalEvidence, ExplorationLedger, completion authority, or Runtime contracts. When explicit source metadata identifies primary Vision, auxiliary-only rows SHALL NOT define completeness identity or campaign traversal branches.

#### Scenario: Row composition result is consumed

- **WHEN** Runtime consumes the repaired perception output
- **THEN** Runtime SHALL continue to apply exact ordered-overlap normalization and fail-closed rules
- **AND** SHALL filter auxiliary-only occurrences from authorization-bearing normalization and campaign inventory
- **AND** perception output SHALL remain evidence rather than Runtime truth or action authority
