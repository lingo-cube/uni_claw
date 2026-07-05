## MODIFIED Requirements

### Requirement: PageSnapshotManager provides pure-function deterministic fingerprint and has_changed

`PageSnapshotManager` SHALL be a sealed class with pure-function semantics (no mutable state). `fingerprint(page_analysis)` SHALL compute an integer hash from sorted `(type, name)` tuples extracted from `page_analysis.items`, returning 0 for null or empty input. The hash SHALL be **deterministic**: the same input MUST always produce the same output across different process runs. `has_changed(before, after)` SHALL return `true` when `fingerprint(before) != fingerprint(after)` and `false` when they are equal. Both methods SHALL NOT use `string.GetHashCode()` (which is non-deterministic across process runs in .NET).

#### Scenario: Fingerprint is deterministic across multiple calls
- **WHEN** `PageSnapshotManager.fingerprint(page_analysis)` is called twice with the same `PageAnalysis` instance (or an identical copy)
- **THEN** both calls SHALL return the same integer value

#### Scenario: Fingerprint uses deterministic character-based hashing
- **WHEN** `PageSnapshotManager.fingerprint(page_analysis)` computes the hash
- **THEN** it SHALL use a deterministic algorithm (e.g., `hash = hash * 31 + (int)ch` per character) rather than `string.GetHashCode()`
- **AND** the same input SHALL produce the same hash value regardless of which machine or process runs the computation

### Requirement: TraceCoordinator Log-and-Continue logs warnings

All trace write operations SHALL use a try-catch wrapper that catches exceptions and logs them as warnings without interrupting the traversal. The catch block SHALL NOT be empty or silently swallow exceptions. A warning-level output SHALL be produced containing the method name and exception summary.

#### Scenario: Trace write failure produces warning output
- **WHEN** a trace write method (e.g., `RecordStateTransition`) throws an exception during execution
- **THEN** the exception SHALL be caught and a warning SHALL be output (via `Console.WriteLine` or equivalent)
- **AND** the warning SHALL contain the exception type name and message
- **AND** the traversal SHALL continue without interruption
