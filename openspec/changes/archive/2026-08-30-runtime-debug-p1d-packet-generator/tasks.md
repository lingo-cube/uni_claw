# Tasks — runtime-debug-p1d-packet-generator

## 1. Generator

- [x] 1.1 Add `query.generate_packet(bundle, case_id, target_seq)` producing a P0-schema-compatible base packet (stored facts only)
- [x] 1.2 Emit `CAPTURE_ASSET` evidenceIndex entries (AssetRef fields + observationSeq/frameId); bind target-frame assets as the target occurrence's evidenceRefs
- [x] 1.3 Emit deterministicInputDigest per P0 convention; declare missing semantic facets as MissingEvidence; never emit semantic fields

## 2. CLI surface

- [x] 2.1 Add `packet-generate <bundle> --case-id <name> [--observation-seq N]` (stdout canonical envelope; unknown seq → `EVIDENCE_UNAVAILABLE`)

## 3. Contract verification

- [x] 3.1 Add tests: generated packet round-trips through summarize/occurrence/evidence; byte-determinism; no semantic fabrication; AssetRef binding; unknown-seq fail-closed
- [x] 3.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, and repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/query.py` | `openspec/changes/runtime-debug-p1d-packet-generator/design.md` |
| `tools/runtime_debug/cli.py` | `openspec/changes/runtime-debug-p1d-packet-generator/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p1d-packet-generator/design.md` |