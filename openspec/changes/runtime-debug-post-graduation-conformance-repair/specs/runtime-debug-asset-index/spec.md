## MODIFIED Requirements

### Requirement: Capture bundle AssetRef index
The Toolchain SHALL project a Harness capture bundle using its actual camelCase persisted wire shape (manifest + records + checksums + artifacts) into an AssetRef index: one AssetRef per manifest artifact with assetId, assetType (stored contentType or `capture.artifact`), traceId (when stored), relative path, sha256, optional parentAssetRef (derivedFromArtifactId), and metadata (fileName, frameId, byteCount). `observationSeq` SHALL be attached when the artifact's frameId resolves to a record sequenceNumber; absent associations (runId, spanId, occurrenceId) SHALL be explicit nulls. Before projection, the reader SHALL fail closed on unsafe/duplicate artifact identities, malformed records, unresolved parent relations, checksum coverage mismatch, missing or non-regular artifact paths, byteCount mismatch, or artifact-byte digest mismatch. Artifact verification SHALL stream bytes read-only and SHALL NOT copy, decode, mutate, or inline artifact content.

#### Scenario: Bundle assets are queryable with frame correlation
- **WHEN** a conforming bundle stores artifacts with frameId and a record maps that frameId to sequenceNumber 7
- **THEN** the AssetRef index SHALL carry observationSeq=7 plus traceId, verified sha256, relative path, and metadata

#### Scenario: Semantic asset labels are not guessed
- **WHEN** an artifact's stored contentType is absent
- **THEN** the index SHALL use `capture.artifact` and SHALL NOT infer screenshot/crop/overlay labels

#### Scenario: Checksum inconsistency fails closed
- **WHEN** checksums, artifact bytes, byteCount, path identity, parent relation, or manifest metadata are inconsistent
- **THEN** the reader SHALL return `SCHEMA_VIOLATION` without projecting any AssetRef

### Requirement: Asset show and related queries
`asset-show` SHALL return the single verified AssetRef metadata by asset id (`EVIDENCE_UNAVAILABLE` when absent); `asset-related` SHALL return parent/child AssetRefs by derivedFromArtifactId order (sorted), without returning artifact content.

#### Scenario: Child and parent asset relations
- **WHEN** artifact B declares derivedFromArtifactId A and both are valid manifest artifacts
- **THEN** `asset-related` for B SHALL list A as parent and for A SHALL list B as a child
