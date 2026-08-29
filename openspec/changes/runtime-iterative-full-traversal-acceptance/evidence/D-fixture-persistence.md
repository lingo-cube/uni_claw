# D. Knowledge Persistence / Versioning — Acceptance Evidence

## Leader's independent verification

- Build: 0 errors. Tests (leader re-run): `ScenarioKnowledgeStoreTests` → **9/9**.
- Purity: only new files (`ScenarioKnowledgeStore.cs`, `FrozenFixture.cs`,
  `ScenarioKnowledgeStoreTests.cs`); C-group files untouched; Runtime byte-identity holds.
- `scripts/check-consistency.sh` ALL PASS (worker-run, leader spot-verified via the same
  script in the M-group regression later).

## Worker WorkResult (module-worker-d) — accepted summary

- Layout `validation/knowledge/settings/<scenario>/v<N>/{records.json, manifest.json, FIXTURE.md}`.
- Determinism: zero DateTime/paths/machine names; RecordId-sorted; canonical 15-field
  JsonNode order; indent 2 / UTF-8 no BOM / LF; confidence invariant "R"; CreatedFromRunIds
  sorted (set semantics) — two freezes of the same content are byte-identical (tested,
  incl. run-set order invariance).
- Load = layered gate: container integrity (missing/corrupt/schema/recordsSha256/recordCount
  → InvalidOperationException: a tampered container is not per-record rejection), then
  per-record: strict parse → SHA-256 RecordId recompute (tamper → reject) → scope Matches
  gate (reason names the first differing field) → fixture Admit revalidation. Every
  rejection reported in `LoadRejection`, never silently fixed.
- Supersession: explicit `supersedesVersion` (null for v1) in manifest + directory; Load
  never auto-merges historical versions.
- FIXTURE.md: header/scope block/one line per record/lifecycle statistics table.

DEVIATIONS (accepted): optional `supersedesVersion` parameter on Freeze (required for the
v2 chain); manifest adds schema + recordCount cross-checks (same fail-closed family).

BLOCKED: none.

## Spec scenario coverage

| Spec scenario | Evidence |
|---|---|
| Human-readable persisted asset | records.json + FIXTURE.md; never opaque-blob-only |
| Freeze/load round-trip fidelity | 9/9 tests |
| Version supersession across freezes | v1→v2 chain test |
| No cross-scope leakage on load | one-field mismatch → all rejected, zero leaked |
