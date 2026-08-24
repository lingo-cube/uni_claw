## Purpose

Defines explicit catalog-driven replay through the existing Runtime environment boundary with integrity checks and fail-closed public-behavior assertions.

## ADDED Requirements

### Requirement: Replay uses the unchanged environment boundary
Catalog-driven replay SHALL execute the existing Runtime through `IEnvironment`-compatible replay assets and SHALL NOT add a production replay path inside Agent.

#### Scenario: Catalog Scenario replays through Runtime
- **WHEN** a caller resolves an explicit valid ScenarioId and starts its replay
- **THEN** the unchanged Runtime consumes the replay environment and assertions evaluate public result, dispatch safety, fresh observation, and GoalEvidence

### Requirement: Replay divergence fails closed
Action divergence, observation exhaustion, missing frames, invalid hashes, and unsupported schema SHALL fail the regression and MUST NOT produce a false pass.

#### Scenario: Runtime dispatch diverges from asset
- **WHEN** Runtime requests an action that does not match the admitted replay sequence
- **THEN** replay fails closed without fabricating an action result or successful completion

### Requirement: Existing replay assets remain compatible
Existing versioned replay manifests SHALL remain readable or use an explicit, tested version migration before replacement.

#### Scenario: Existing golden manifest is migrated
- **WHEN** canonical catalog representation replaces hard-coded golden assembly
- **THEN** an equivalence test proves the old manifest remains readable or is transformed by a declared versioned migration without inventing Runtime facts
