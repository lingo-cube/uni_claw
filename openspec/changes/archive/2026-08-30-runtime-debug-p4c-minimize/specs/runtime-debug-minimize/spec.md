## ADDED Requirements

### Requirement: Mechanical minimal failure-preserving slice
The Toolchain SHALL minimize one validated replay fixture: keep the mechanically failing step, drop trailing steps, and greedily drop earlier steps while the dry-run projection still reports the same first mechanically non-OK order. Output SHALL include the minimal steps, removed orders, iterations, and a mechanical-only note. A fixture without mechanical failure SHALL be a no-op (hadFailure=false). The input SHALL never be mutated.

#### Scenario: Rejected result reduces to the failing step
- **WHEN** a fixture fails only by one stored Rejected result
- **THEN** the minimal slice SHALL contain exactly that step, with all others reported removed

#### Scenario: No failure is a no-op
- **WHEN** every result outcome is OK
- **THEN** the minimizer SHALL report hadFailure=false and remove nothing

### Requirement: Fail-closed and read-only
Missing/malformed fixtures SHALL fail closed (`EVIDENCE_UNAVAILABLE` / `SCHEMA_VIOLATION`) before any minimization; the algorithm SHALL be deterministic and SHALL NOT simulate state or alter the fixture file.

#### Scenario: Missing fixture fails closed
- **WHEN** the fixture file does not exist
- **THEN** the command SHALL return `EVIDENCE_UNAVAILABLE` without a slice
