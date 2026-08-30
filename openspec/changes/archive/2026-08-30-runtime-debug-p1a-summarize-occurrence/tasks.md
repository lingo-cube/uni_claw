## 1. Read-only foundation

^- [x] 1.1 Add the stdlib-only `tools/runtime_debug` package with closed statuses, stable exit-code mapping, canonical JSON serialization, and fail-closed P0 packet reader validation.
^- [x] 1.2 Add deterministic source identity and EvidenceRef indexing helpers without dereferencing artifact URIs or accessing Runtime/Trace processes.

## 2. P1a command projections

^- [x] 2.1 Implement `summarize <packet>` as the contract-limited projection of terminal, target scope, evidence availability, missing evidence, and repair blockers.
^- [x] 2.2 Implement `occurrence <packet>` with exactly one typed selector, stored-status preservation, linked EvidenceRefs, stable ordering, and closed ambiguity/coverage/mismatch outcomes.
^- [x] 2.3 Add the `tools/runtime-debug` executable entry and concise README documenting interfaces, non-interfaces, authority boundaries, result envelope, and exit codes.

## 3. Contract verification

^- [x] 3.1 Add offline AgentWorkflow tests covering all five P0 fixtures for summarize and occurrence success behavior.
^- [x] 3.2 Add negative tests for omitted/multiple selectors, unsupported/malformed packets, missing evidence, identity mismatch, ambiguity, deterministic byte output, and input byte immutability.
^- [x] 3.3 Run focused tests, strict OpenSpec validation, Skill validation, repository consistency checks, and scoped diff checks; record the P1a result without claiming P1b or Runtime capability.

## Design Docs

- `tools/runtime_debug/` and `tools/runtime-debug` → `docs/analysis/runtime-debugging-capability-p0-contract.md`, `.ai/skills/evidence-driven-debugging/references/runtime/tooling-contract.md`, and this change's `design.md`.
- `tests/AgentWorkflow/test_runtime_debug_cli.py` → this change's `specs/runtime-debug-read-only-projection/spec.md` and the P0 acceptance fixtures under `.ai/skills/evidence-driven-debugging/references/runtime/fixtures/`.
