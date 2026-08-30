## 1. Falsifiers

- [ ] 1.1 Add RED tests for full P0 required shape, closed vocabularies, forbidden properties, and all nested EvidenceRef closure.
- [ ] 1.2 Add RED tests for malformed/unsafe bundle metadata, real numeric record kinds, artifact byte/byteCount/digest mismatch, and parent/path integrity.
- [ ] 1.3 Add RED tests for Schema-valid generated packets, canonical terminal chain order, and absent-field preservation.

## 2. Read Boundary Repair

- [ ] 2.1 Implement complete stdlib P0 packet/Debug IR validation without dereferencing EvidenceRef URIs.
- [ ] 2.2 Implement fail-closed camelCase bundle parsing, record normalization, safe identity/path checks, relation checks, and streamed artifact integrity verification.

## 3. Projection Repair

- [ ] 3.1 Generate complete P0 packets using explicit absence states, closed EvidenceRef kinds, schemaDigest, deterministic digest, and consistent repair blockers.
- [ ] 3.2 Project terminal chain in canonical stage order and conditionally preserve optional stored fields without synthetic nulls.
- [ ] 3.3 Re-run P2a/P2b/P2c normal and adversarial tests to verify upstream compatibility.

## 4. Documentation and Graduation Gate

- [ ] 4.1 Correct main spec Purpose text, README integrity/schema claims, and stale current-state analysis without rewriting historical decisions.
- [ ] 4.2 Run focused and full AgentWorkflow tests with ResourceWarning as error, independent Draft 2020-12 Schema validation, deterministic/read-only checks, OpenSpec strict validation, consistency checks, and diff hygiene.
- [ ] 4.3 Produce a post-graduation correction/revalidation receipt and archive this change only when every gate passes.
