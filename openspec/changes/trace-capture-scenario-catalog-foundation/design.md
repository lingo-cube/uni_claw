## Context

The graduated Runtime and replay harness already expose observations, actions, results, and Runtime trace evidence, but physical capture is in-memory and golden replay is assembled from separate ad-hoc assets. The approved architecture gate freezes this work as a Harness capability with zero Runtime semantic delta.

## Goals / Non-Goals

**Goals:**

- Capture one run's public evidence in deterministic external-call order.
- Persist immutable bundles through a narrow append-only store.
- Load reviewed assets through an immutable, explicit Scenario catalog.
- Replay catalog assets through the unchanged `IEnvironment` boundary.

**Non-Goals:**

- Runtime, Agent, Container, Traversal, Observation, GoalEvidence, or `IEnvironment` contract changes.
- Provider frameworks, Planner, Brain, intent routing, automatic Scenario generation, or trace-derived expected behavior.
- Remote storage, databases, background services, or automatic asset publication.

## Decisions

### D1 — Harness owns capture state

`TraceCaptureSession` owns only mechanism-local buffers, ordering, hashes, and lifecycle. Runtime semantic truth and completion remain owned by RuntimeAgent. A `CapturingEnvironment` wrapper observes the existing public environment boundary without changing it.

### D2 — Capture faults are isolated

Capture and artifact-tap failures are latched as capture results and never change a Runtime result, authorize or repeat an action, or trigger Traversal recovery. Runtime failure and capture failure remain separate outcomes.

### D3 — Persistence is narrow and append-only

`ITraceCaptureStore` exposes one atomic save operation. The filesystem implementation writes and validates in staging, refuses an existing capture ID, and publishes only a complete immutable directory.

### D4 — Catalog lookup is immutable and explicit

`ScenarioCatalog` validates IDs, schema versions, paths, hashes, provenance, and references before returning an explicitly requested Scenario. It is not a registry, watcher, intent router, or Runtime selection authority.

### D5 — Captures do not define expected behavior

Approved Scenario/spec behavior remains normative. Captured outcomes are candidate evidence only; reviewed assets are admitted separately and regression assertions target public behavior rather than private method order or diagnostic prose.

### D6 — Existing replay contracts migrate without duplication

Reusable asset records, validation, JSON options, and replay adapters move into `UniClaw.Runtime.Harness`, which depends on Runtime. Runtime never references Harness. Existing versioned manifests remain readable or use an explicit tested migration.

## Risks / Trade-offs

- **Capture overhead or capture faults perturb a physical run** → keep callbacks narrow and non-throwing; prove dispatch count and Runtime outcome are unchanged.
- **Partial or corrupt publication** → stage, validate, hash, and atomically publish; quarantine incomplete captures outside the catalog.
- **Captured history is mistaken for semantic truth** → keep capture/result types separate from WorldBelief and forbid diagnostic-string inference.
- **Raw data enters version control accidentally** → keep raw captures local by default and require explicit review, minimization, provenance, and sensitivity audit before promotion.
- **Manifest compatibility drifts** → preserve schema-v1 reads or add an explicit versioned migration test before replacing existing replay assembly.

## Migration Plan

1. Extract existing reusable replay contracts into the Harness project without changing Runtime behavior.
2. Add in-memory capture and an in-memory falsifier before filesystem persistence.
3. Add append-only local persistence and physical artifact attachment.
4. Add immutable catalog loading and migrate golden assets only after equivalence is proven.
5. Run targeted, full regression, guards, consistency, strict OpenSpec, provenance, and sensitivity checks before a separate graduation decision.

Rollback is additive: retain the existing replay path until catalog-driven equivalence is proven, and do not overwrite existing golden bytes or published capture IDs.
