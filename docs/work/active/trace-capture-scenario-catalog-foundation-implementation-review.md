# Trace Capture and Scenario Catalog Foundation — Implementation Review

> DocumentType: IMPLEMENTATION_REVIEW
> Authority: NONE
> Date: 2026-08-22
> Change: `trace-capture-scenario-catalog-foundation`
> Reviewer role: Project Leader / independent verifier

This is an implementation and validation record. It is not a graduation
decision, Human receipt, architecture amendment, or archive authorization.

## Implemented scope

- reusable asset and replay contracts live in `UniClaw.Runtime.Harness` with no
  duplicate test-owned definitions;
- Harness-owned capture lifecycle records ordered environment-boundary facts,
  preserves the public Runtime trace snapshot, and separates Runtime and
  capture outcomes;
- append-only filesystem persistence validates content hashes in staging,
  refuses collisions, and atomically publishes complete capture directories;
- `PhysicalEnvironment` has one optional, failure-isolated artifact tap without
  changing `IEnvironment`;
- deep Scenario catalog loading validates explicit IDs, schema versions,
  provenance, references, manifest paths/hashes, and referenced artifact
  paths/hashes before replay;
- the reviewed golden already-ON, OFF-to-ON, and retained unknown-state cases
  replay through the unchanged Runtime boundary from canonical assets;
- original screenshots, perception JSON, and ad-hoc source traces remain
  unchanged. Ad-hoc traces are retained as untyped `SourceEvidence` and are not
  promoted into inferred Runtime events.

## Independent verification evidence

| Check | Result |
|---|---|
| Solution build | PASS — 0 warnings, 0 errors |
| SC-TC-001..004, SC-CAT-001..002, SC-REG-001 | PASS — 7/7 |
| Capture/catalog/physical/replay support regression | PASS — 55/55 |
| Deterministic full regression, excluding seven unavailable device tests and one independently identified unrelated Guard | PASS — Runtime 1727/1727; Semantic 32/32 |
| Trace/Catalog architecture guards | PASS — 4/4 |
| All architecture guards | BLOCKED — 31/32 pass; existing Semantic source comment contains `DeveloperOptions` and triggers `ScrollContainerSemanticTraversalGuardTests` |
| Consistency | PASS — C1..C10 |
| This change, strict OpenSpec | PASS |
| All main specs, strict OpenSpec | PASS — 43/43 |
| All active changes, strict OpenSpec | BLOCKED — 13/14 pass; concurrent `runtime-external-semantic-capability-boundary` has no delta spec |
| Diff whitespace check | PASS |
| Asset/provenance/sensitivity audit | PASS — only the minimized canonical JSON manifest is new; existing reviewed PNG/perception/trace bytes were not overwritten; no credential-like material found |

The seven excluded physical tests require `emulator-5554` and the local Vision
socket. `adb devices -l` returned no attached device and the expected socket was
absent, so these are environment-unavailable rather than passing evidence.

## Authority and lifecycle review

```text
RuntimeSemanticDelta: NONE
RuntimeAuthorityDelta: NONE
EnvironmentPortDelta: NONE
AgentContainerTraversalCaptureDependency: NONE
GraduationRecommendation: BLOCKED_PENDING_REPOSITORY_CLEARANCE_AND_HUMAN_RECEIPT
```

TC-01 through TC-06 and targeted TC-07 validation are implemented. TC-07.2
remains open because the repository-wide Guard and all-change strict validation
are not green. TC-07.3 remains open because no Human graduation receipt has
been granted. The change must not be archived yet.

## Projection conflict note

The global `current-gates` and latest snapshot currently project three active
changes, while the live OpenSpec workspace contains additional concurrent
changes. This task does not reclassify or overwrite those unrelated lifecycle
states. A separate lifecycle reconciliation is required before global counts
can be updated safely.
