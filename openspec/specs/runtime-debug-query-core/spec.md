# runtime-debug-query-core Specification

## Purpose
TBD - created by archiving change runtime-debugging-toolchain. Update Purpose after archive.

## Requirements

### Requirement: Read-only deterministic Query Core
The Debug Toolchain SHALL expose one read-only, deterministic Query Core with Run, Trace, Time, Evidence, Log, and Asset query families. Run queries SHALL cover runs, latest (explicit input only), summary, terminal state, blockers, observations, and unknowns. Trace queries SHALL distinguish the EXECUTION tree (Run→Span→Event→ChildSpan) from the CAUSAL/EVIDENCE tree (Observation→Occurrence→Evidence→OperatorDecision→SemanticAdmission→Affordance→RuntimeState→Terminal), support path/ancestors/descendants and type/owner/stage/observation/occurrence pruning, and SHALL prune (hide) without mutating or deleting original records. Time queries SHALL support wall-clock and run-relative ranges, around-event/FDP windows, and observation time windows. Evidence queries SHALL expose the occurrence evidence chain, observation evidence, raw→normalized→fused→canonical→semantic→affordance stages, and related refs. Log and Asset queries SHALL be filtered by the same correlation keys.

#### Scenario: Causal tree query by type pruning
- **WHEN** a user requests the causal tree pruned to Observation→Fusion→Semantic→Affordance→Completeness
- **THEN** the query SHALL hide the pruned node types (including HTTP/serialization/metrics/bookkeeping) in the projection and SHALL NOT alter the underlying trace

#### Scenario: Ambiguity and missing evidence fail closed
- **WHEN** a query matches multiple candidates or lacks required evidence
- **THEN** the Query Core SHALL return the closed status (`AMBIGUOUS_OCCURRENCE`, `INSUFFICIENT_TRACE_COVERAGE`, or `EVIDENCE_UNAVAILABLE`) instead of guessing

#### Scenario: Execution and causal trees are distinct
- **WHEN** a user requests `trace causal` for a run
- **THEN** the projection SHALL present the causal/evidence tree (FDP main view) and SHALL NOT present execution spans as causal evidence
