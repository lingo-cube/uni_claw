# Tasks — runtime-debug-p1c-asset-index

## 1. Bundle source adapter

- [x] 1.1 Add `sources/bundle.py`: fail-closed reader (manifest/records/checksums; artifact-id uniqueness; checksum coverage; path-free error messages)
- [x] 1.2 Build Foundation-schema AssetRefs (stored ContentType or `capture.artifact`, relative path, sha256, parentAssetRef, metadata, FrameId→observationSeq join; honest nulls for absent refs)

## 2. Query Core + CLI

- [x] 2.1 Add `query.assets` / `asset_show` / `asset_related` (sorted; parent/child by DerivedFromArtifactId; no content reads)
- [x] 2.2 Add `assets` / `asset-show` / `asset-related` CLI commands (bundle-scoped; envelope source without paths); route before packet commands

## 3. Contract verification

- [x] 3.1 Add tests: assets list + frame correlation + no abs path, show/related parent-child, checksum mismatch fails closed, missing bundle `EVIDENCE_UNAVAILABLE`
- [x] 3.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, and repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/sources/bundle.py` | `openspec/changes/runtime-debug-p1c-asset-index/design.md` |
| `tools/runtime_debug/query.py` | `openspec/changes/runtime-debug-p1c-asset-index/design.md` |
| `tools/runtime_debug/cli.py` | `openspec/changes/runtime-debug-p1c-asset-index/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p1c-asset-index/design.md` |