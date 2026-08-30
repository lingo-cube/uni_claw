# Design — runtime-debug-p1c-asset-index

## Context

P1a/P1b consume P0 Evidence Packet JSON. P1c adds a second source adapter consuming Harness capture bundle directories (`FileTraceCaptureStore` layout: `capture-manifest.json` with embedded `Artifacts[]` incl. FrameId/ContentType/DerivedFromArtifactId/ContentHash, `records.json`, `checksums.sha256` in `<hash>  artifacts/<id>.bin` lines, `artifacts/*.bin`). Foundation froze the AssetRef schema this adapter fills.

## Goals / Non-Goals

Goals: AssetRef first-class from bundles (list/show/related), checksum-verified fail-closed, FrameId→observationSeq correlation, zero content reads.

Non-Goals: semantic assetType inference; occurrence/crop assignment (needs upstream labels); runId/spanId/occurrenceId (not stored in bundles — honest nulls); any harness-side change; full JSON-Schema validation.

## Decisions

### D1 — Second source adapter behind the same Query Core
**Decision:** `sources/bundle.py` produces a CaptureBundle model consumed by `query.assets/asset_show/asset_related`; CLI routes bundle commands before packet commands. Proves the extension seam (source-replaceability) and keeps one Query Core.
**Alternatives:** a packet-from-bundle projection — rejected: bundles carry no Debug IR; asset metadata is schema-different from evidence packets.

### D2 — assetType = stored ContentType, else `capture.artifact`
**Decision:** the index layer never guesses semantics. Stored ContentType is projected verbatim; absence yields `capture.artifact`. Screenshot/crop/overlay labels remain producer-attributed (records/frames/annotations) in a later slice.
**Why:** identity discipline — an inference dressed as a stored fact is exactly what the gate forbids.

### D3 — observationSeq via FrameId→record join; absent refs are explicit nulls
**Decision:** artifacts carry FrameId; records carry FrameId+SequenceNumber; the first record mapping yields observationSeq. runId/spanId/occurrenceId absent in bundles are explicit nulls in every AssetRef (schema requires the key; the value says "not stored").
**Why:** honest correlation without inventing identity; the Foundation's "candidate correlation only" rule.

### D4 — Checksum coverage verified, content never read
**Decision:** checksums.sha256 must cover exactly the manifest artifacts and reference existing files; a mismatch is `SCHEMA_VIOLATION`. Artifact bytes are never opened by any command.
**Why:** mirrors the Harness reader's integrity posture without expanding the tool's IO surface.

## Risks / Trade-offs

- [Checksums with duplicated hashes (content-addressed sharing)] → uniqueness is enforced on artifact paths, not hashes.
- [Envelope absolute-path ban] → error messages never embed bundle paths; AssetRef.path is always relative (`artifacts/<id>.bin`); envelope source carries only bundleId/traceId/scenarioId.
- [Asset semantic labels unavailable at index layer] → documented; a later producer-labeled slice (metadata annotations) is the extension path.

## Migration Plan

None — additive source adapter; no harness/schema/wire change.

## Open Questions

None that would change the contracts; occurrence→crop assignment and stage-image tagging are producer-label slices deferred to later work.