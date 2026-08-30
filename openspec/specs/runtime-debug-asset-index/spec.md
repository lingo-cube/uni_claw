# runtime-debug-asset-index Specification

## Purpose
定义 Harness capture bundle 的只读 AssetRef 索引与完整性边界，使 frame/artifact 可被确定关联和查询，同时不取得 Runtime、Trace 或 artifact 内容语义权威。

## Requirements

### Requirement: Capture bundle AssetRef index
The Toolchain SHALL project a Harness capture bundle (manifest + records + checksums) into an AssetRef index: one AssetRef per manifest artifact with assetId, assetType (stored ContentType or `capture.artifact`), traceId (when stored), relative path, sha256, optional parentAssetRef (DerivedFromArtifactId), and metadata (fileName, frameId, byteCount). `observationSeq` SHALL be attached when the artifact's FrameId resolves to a record SequenceNumber; absent associations (runId, spanId, occurrenceId) SHALL be explicit nulls. The reader SHALL verify manifest artifact-id uniqueness and checksums.sha256 coverage, SHALL fail closed with `SCHEMA_VIOLATION` on malformed manifests or inconsistent checksums, and SHALL NOT read artifact file content.

#### Scenario: Bundle assets are queryable with frame correlation
- **WHEN** a bundle stores artifacts with FrameId and a record maps that FrameId to SequenceNumber 7
- **THEN** the AssetRef index SHALL carry observationSeq=7 plus traceId, sha256, relative path, and metadata without reading the artifact bytes

#### Scenario: Semantic asset labels are not guessed
- **WHEN** an artifact's stored ContentType is absent
- **THEN** the index SHALL use `capture.artifact` and SHALL NOT infer screenshot/crop/overlay labels

#### Scenario: Checksum inconsistency fails closed
- **WHEN** the checksums manifest disagrees with the manifest artifacts or listed files
- **THEN** the reader SHALL return `SCHEMA_VIOLATION` without projecting any AssetRef

### Requirement: Asset show and related queries
`asset-show` SHALL return the single AssetRef metadata by asset id (`EVIDENCE_UNAVAILABLE` when absent); `asset-related` SHALL return parent/child AssetRefs by DerivedFromArtifactId order (sorted), never dereferencing file content.

#### Scenario: Child and parent asset relations
- **WHEN** artifact B declares DerivedFromArtifactId A
- **THEN** `asset-related` for B SHALL list A as parent and for A SHALL list B as a child
