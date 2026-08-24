## Purpose

Defines immutable and explicit Scenario asset lookup with fail-closed validation of identifiers, versions, provenance, hashes, paths, and references.

## ADDED Requirements

### Requirement: Catalog validation precedes replay
ScenarioCatalog SHALL reject duplicate IDs, dangling references, path escape, unsupported schema, hash mismatch, and provenance inconsistency before returning a replay asset.

#### Scenario: Invalid catalog is rejected
- **WHEN** a catalog contains a duplicate ID, dangling replay or frame reference, bad hash, unsupported version, or escaping path
- **THEN** loading fails before any Runtime execution begins

### Requirement: Scenario selection is explicit
Callers SHALL request a Scenario by explicit ScenarioId. The catalog MUST NOT infer Scenario selection from business intent, Runtime state, or captured outcome.

#### Scenario: Explicit lookup returns one validated asset
- **WHEN** a caller requests a valid registered ScenarioId
- **THEN** the catalog returns the corresponding immutable validated Scenario asset and no other Scenario is selected

### Requirement: Captured outcome is not expected behavior
Captured run results MUST NOT define or rewrite normative Scenario expectations. Asset admission SHALL preserve approved specification behavior and provenance.

#### Scenario: Discovery capture disagrees with approved behavior
- **WHEN** a candidate capture outcome differs from the approved Scenario expectation
- **THEN** the capture remains evidence requiring review and does not alter the expected result automatically
