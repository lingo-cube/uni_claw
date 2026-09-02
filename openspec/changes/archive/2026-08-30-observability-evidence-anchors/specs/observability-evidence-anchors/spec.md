## ADDED Requirements

### Requirement: Frame/action anchor tags on observe and execute boundaries
The Environment observe boundary SHALL attach `observation.seq` (the observation SequenceNumber) and `observation.frame` (the frame reference used by the observation sources) as span attributes; the Environment execute boundary SHALL attach `action.kind` (the device action type). Attachment SHALL be fail-open and SHALL NOT change TraceRun schema/wire/Runtime semantics; attributes are candidate correlation anchors, never world truth.

#### Scenario: Observe span carries observation anchors
- **WHEN** an Environment observe completes
- **THEN** its span SHALL carry observation.seq and observation.frame attributes equal to the stored sequence and frame reference

### Requirement: Toolchain consumes anchors and joins assets
The execution-tree projection SHALL surface span anchors (observation.seq/frame, action.kind) and SHALL join spans to bundle AssetRefs by observation sequence (sorted by asset id; empty when no asset matches). View models SHALL pass anchors through unchanged.

#### Scenario: FAILED span resolves its frame asset
- **WHEN** a bundle stores an asset with observationSeq equal to a span's observation.seq
- **THEN** the execution-tree node SHALL list that asset in frameAssetRefs (assetId/path/hash/frameId)
