# runtime-debug-data-model Specification

## Purpose
TBD - created by archiving change runtime-debugging-toolchain. Update Purpose after archive.

## Requirements

### Requirement: Unified debug ref and data model
The Debug Toolchain SHALL correlate evidence through one ref family — RunRef, TraceRef, SpanRef, EventRef, ObservationRef, OccurrenceRef, EvidenceRef, LogRef, AssetRef, ArtifactRef, DecisionRef, StateRef — over the correlation keys RunId, TraceId, SpanId, EventId, ObservationSeq, OccurrenceId, StableKey, RowId, EvidenceRef, AssetRef, Timestamp, and RelativeTimestamp. `StableKey`, `RowId`, `Bounds`, and `Text` SHALL be usable for query/candidate correlation only and SHALL NEVER gain identity or Runtime authority; they are not SameOccurrence/SameSource/Identity proofs.

#### Scenario: Occurrence correlation by StableKey
- **WHEN** a query correlates occurrences by StableKey across observations
- **THEN** the result SHALL be labeled candidate correlation with status/proof, SHALL fail closed (`AMBIGUOUS_OCCURRENCE`) when multiple candidates match, and SHALL NOT claim SameOccurrence proof from StableKey alone

#### Scenario: Missing correlation evidence
- **WHEN** a ref family member lacks a truthful correlation value
- **THEN** the Toolchain SHALL report the value as unavailable and SHALL NOT synthesize an identity from text, bounds, index, StableKey, or RowId

### Requirement: AssetRef is a first-class reference
Assets — screenshots, viewport/cropped screenshots, raw/annotated frames, stage images, detector visualizations, semantic overlays, video, trace/JSON/log artifacts, replay fixtures — SHALL be referenced by AssetRef with at least: assetId, assetType, runId, timestamp and relativeTimestamp when available, optional observationSeq/traceId/spanId/occurrenceId, producer, path/uri, mimeType, sha256 when a content identity exists, and optional parentAssetRef / cropBounds / annotations / metadata. Evidence, Trace/Event, Observation, and Occurrence SHALL project to AssetRefs (screenshot/frame; crop/overlay respectively). Debug IR SHALL reference Assets by AssetRef and SHALL NOT copy asset bodies into the IR.

#### Scenario: Occurrence points to screenshot and crop
- **WHEN** an occurrence is diagnosed
- **THEN** its packet SHALL carry the EvidenceRef, the screenshot/frame AssetRef, and (when cropped) the crop AssetRef, without embedding image bytes in the IR

#### Scenario: Asset is not world truth
- **WHEN** an asset is referenced in an evidence chain
- **THEN** the Toolchain SHALL treat the AssetRef as evidence reference only; the asset SHALL NOT become Runtime truth or decision authority
