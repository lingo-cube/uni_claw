## ADDED Requirements

### Requirement: Settings scenarios use a versioned and validated JSON contract

The system SHALL load Android Settings scenarios from versioned JSON files. A scenario MUST declare `schemaVersion`, `scenarioId`, `appPackage`, `mode`, `boundaries`, `allowedActions`, `safetyPolicy`, `successCriteria`, and `resetProcedure`. Unknown schema versions, missing required fields, duplicate scenario IDs, invalid string-vocabulary values, and non-positive budgets MUST fail before any device action.

#### Scenario: Valid scenario is accepted
- **WHEN** a scenario declares a supported schema version, a unique ID, a supported mode, and valid positive budgets
- **THEN** the scenario is normalized and made available for validation or execution

#### Scenario: Invalid scenario fails before device access
- **WHEN** a scenario has an unsupported schema version or a non-positive maximum step count
- **THEN** loading fails with the field name and illegal value before screenshot capture or action execution

### Requirement: Locate-one-item scenario has a bounded success contract

The catalog SHALL provide a `locate-one-item` scenario whose mode is `locate_one_item`. The scenario MUST accept a target label and optional aliases, start from the Settings home page, allow only bounded navigation/back/scroll actions, and define success as verified arrival at the target page within the configured step, scroll, depth, and time budgets.

#### Scenario: Target page is verified
- **WHEN** the target row is found, its navigation action is allowed, and the post-action page title or visible identity matches the target or an alias
- **THEN** the scenario completes successfully and records the matching evidence

#### Scenario: Target is not found within budget
- **WHEN** the target row is not observed before the configured scroll, step, or time budget is exhausted
- **THEN** the scenario completes unsuccessfully with an explicit budget or not-found reason and MUST NOT claim target arrival

### Requirement: Safe-enumeration scenario is limited to discoverable first-level entries

The catalog SHALL provide an `enumerate-settings-safely` scenario whose mode is `enumerate_first_level`. It SHALL enumerate unique first-level Settings home entries until a verified end-of-list or an explicit budget boundary. For each allowed entry it SHALL enter the page, collect the page identity and visible menu items without activating child controls, and return to a verified Settings home page. Dangerous first-level entries SHALL be recorded and skipped without entry.

#### Scenario: Safe first-level entry is sampled
- **WHEN** an unseen first-level entry passes the safety policy
- **THEN** the runner enters it, captures its page identity and visible items, returns to Settings home, and marks the entry sampled

#### Scenario: Dangerous first-level entry is skipped
- **WHEN** an unseen first-level entry matches a deterministic deny rule
- **THEN** the runner records the entry as skipped with the rule ID and does not click it

#### Scenario: End of list cannot be proven
- **WHEN** the runner exhausts its scroll budget or screen-state analysis fails before a verified end-of-list
- **THEN** the scenario result is incomplete or failed and MUST NOT report exhaustive enumeration

### Requirement: Scenario inputs are immutable within a run

At run start, the system SHALL normalize the selected scenario, compute its content hash, and persist a snapshot. All planning, safety, verification, and reporting within that run MUST use the snapshot rather than re-reading the source file.

#### Scenario: Source scenario changes during execution
- **WHEN** the source scenario file is edited after a run has started
- **THEN** the active run continues using its original snapshot and reports the original content hash

### Requirement: Scenario reset procedure establishes a known starting page

Every Settings scenario SHALL define a reset procedure that returns the selected device to the Settings home page and verifies the page identity before the first planned action. Reset failure MUST stop that run without executing the scenario.

#### Scenario: Reset reaches Settings home
- **WHEN** the reset procedure completes and the observed page matches the configured Settings home identity
- **THEN** the runner may begin the scenario loop

#### Scenario: Reset cannot establish Settings home
- **WHEN** the reset procedure times out or verification observes another application/page
- **THEN** the run fails in the preparation phase and sends no scenario action
