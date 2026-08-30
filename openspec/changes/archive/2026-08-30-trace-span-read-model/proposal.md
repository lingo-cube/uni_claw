## Why

The Runtime and Harness now produce immutable hierarchical `TraceRun` / `TraceSpan` data, and capture persistence can write that data, but consumers can only inspect projected Runtime events: there is no bounded read model for a known run and no validated reader for a known persisted capture. This change purchases those two read-only capabilities without reopening Runtime execution authority or the frozen Protocol v1 wire surface.

## What Changes

- Add an in-process, read-only trace summary and cursor-paged span query for an explicitly identified registered run.
- Add a Harness-owned reader for an explicitly identified persisted `CaptureSessionId`, including schema, path, manifest, record, artifact, and TraceRun structural validation.
- Use typed exact-match span filters and a stable read-model sequence; do not parse prompts, reasons, messages, or diagnostics into query authority.
- Return explicit not-found, trace-absent, unsupported-schema, and invalid-capture outcomes; never fabricate an empty success or partially trusted bundle.
- Keep Runtime activity emission, Agent lifecycle, FSM, Traversal, Recovery, GoalEvidence, capture publication, and existing `run.*` wire operations unchanged.
- Defer any new DriverHost/DSH/CLI wire method to a separate additive Protocol v1 gate with a concrete external consumer.

## Capabilities

### New Capabilities

- `trace-span-read-model`: Provides bounded in-process lookup of trace metadata and stable cursor-paged spans for one explicitly identified registered run.
- `persisted-trace-capture-read`: Loads one explicitly identified published capture through a Harness-owned, fail-closed read boundary and exposes its optional immutable TraceRun without inferring correlation or behavior.

### Modified Capabilities

- None.

## Impact

- `src/UniClaw.Runtime.DriverHost/`: additive read-only projection/query contracts over already registered immutable `TraceRun` values; no transport method change.
- `src/UniClaw.Runtime.Harness/`: additive persisted-capture reader and shared structural/integrity validation; existing append-only save lifecycle remains unchanged.
- `tests/UniClaw.Runtime.Tests/`: query determinism, cursor/filter, authority-firewall, corruption, compatibility, and regression proofs.
- `openspec/specs/`: two new capability specifications; no existing main specification is weakened or redefined.
- No new package, database, index service, remote telemetry backend, UI, Runtime dependency, or Protocol v1 wire operation.
