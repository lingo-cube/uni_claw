## ADDED Requirements

### Requirement: Entry strategy automatic fallback chain
The system SHALL attempt entry strategies in automatic fallback order until one succeeds.

#### Scenario: Primary strategy succeeds
- **WHEN** primary entry strategy succeeds and wait_condition is satisfied
- **THEN** system proceeds to initialization without trying fallback strategies

#### Scenario: Primary strategy fails, fallback succeeds
- **WHEN** primary entry strategy fails
- **AND** fallback strategy is configured
- **AND** fallback strategy succeeds
- **THEN** system proceeds to initialization

#### Scenario: All configured strategies fail, default succeeds
- **WHEN** primary and fallback strategies fail
- **AND** bind_current_screen strategy succeeds
- **THEN** system proceeds to initialization

#### Scenario: All strategies fail
- **WHEN** all entry strategies (primary, fallback, bind_current_screen) fail
- **THEN** system SHALL raise EntryPolicyError with list of failed strategies and last error

### Requirement: Direct deeplink strategy execution
The system SHALL execute direct_deeplink strategy by sending deeplink to target app.

#### Scenario: Deeplink strategy execution
- **WHEN** entry_strategy is DIRECT_DEEPLINK
- **THEN** system sends deeplink "{entry_app}://" via action_executor

### Requirement: Cold launch strategy execution
The system SHALL execute cold_launch strategy by returning to home screen and clicking target app icon.

#### Scenario: Cold launch strategy succeeds
- **WHEN** entry_strategy is COLD_LAUNCH
- **AND** vision service finds target app icon on home screen
- **THEN** system clicks the icon and proceeds to verification

#### Scenario: Cold launch strategy fails
- **WHEN** entry_strategy is COLD_LAUNCH
- **AND** vision service does not find target app icon on home screen
- **THEN** system raises EntryError and proceeds to next strategy in chain

### Requirement: Bind current screen strategy execution
The system SHALL execute bind_current_screen strategy by assuming device is already in target app.

#### Scenario: Bind current screen strategy
- **WHEN** entry_strategy is BIND_CURRENT_SCREEN
- **THEN** system skips any action and proceeds directly to verification

### Requirement: Entry strategy attempt recording
The system SHALL record each entry strategy attempt in Trace.

#### Scenario: Standard trace level
- **WHEN** trace_level is "standard" or "detailed"
- **THEN** system records entry strategy attempt with result (success/failure)

#### Scenario: Minimal trace level
- **WHEN** trace_level is "minimal"
- **THEN** system does not record entry strategy attempts
