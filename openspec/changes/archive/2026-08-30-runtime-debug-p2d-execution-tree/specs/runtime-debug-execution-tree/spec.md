## ADDED Requirements

### Requirement: Pruned execution tree projection
The Toolchain SHALL project one bundle's `observability-trace.json` as an EXECUTION tree (Run→Span→Event→ChildSpan) with nesting by parentSpanId and deterministic root ordering, supporting: absolute structural pruning by span layer / component / name (the matched span AND its whole subtree are excluded), and filter pruning with causal-spine preservation for `--only-errors` (keep FAILED/CANCELLED spans plus every ancestor on the path to a root) and a monotonic time window `--time-from`/`--time-to` (keep overlapping spans plus their ancestors). Pruning SHALL be projection-only: the trace and bundle files SHALL stay byte-identical. A bundle without a trace SHALL fail closed with `EVIDENCE_UNAVAILABLE`.

#### Scenario: Layer prune cuts the whole subtree
- **WHEN** a layer is hidden and a hidden span is the parent of visible-layer spans
- **THEN** those descendants SHALL also be excluded, and the trace file SHALL remain byte-identical

#### Scenario: Only-errors keeps the causal spine
- **WHEN** `--only-errors` is applied and a FAILED span exists deep in the tree
- **THEN** the projection SHALL keep that span and every ancestor to a root, hiding other leaf spans

#### Scenario: Time window keeps overlapping spans
- **WHEN** a time window is applied
- **THEN** spans overlapping the window SHALL remain, spans fully outside SHALL be hidden, and ancestors of kept spans SHALL be preserved

### Requirement: Trace source validation
The bundle adapter SHALL read `observability-trace.json` (camelCase TraceRun, schemaVersion 1) with fail-closed validation: unique non-empty spanIds, string-or-null parent/name/layer/component/outcome, non-negative integer offsets; the file must be a regular file (no symlink). Malformed traces SHALL fail closed with `SCHEMA_VIOLATION`.

#### Scenario: Malformed trace fails closed
- **WHEN** the trace file declares duplicate spanIds or negative offsets
- **THEN** the reader SHALL return `SCHEMA_VIOLATION` without projecting a tree
