## ADDED Requirements

### Requirement: EntryConfig data class validation
The EntryConfig data class SHALL validate all fields in __post_init__.

#### Scenario: Invalid wait_mode
- **WHEN** EntryConfig.wait_mode is not "fast" or "polling"
- **THEN** __post_init__ raises ValueError with list of valid modes

#### Scenario: Invalid trace_level
- **WHEN** EntryConfig.trace_level is not "minimal", "standard", or "detailed"
- **THEN** __post_init__ raises ValueError with list of valid levels

#### Scenario: Invalid wait_timeout
- **WHEN** EntryConfig.wait_timeout is less than or equal to 0
- **THEN** __post_init__ raises ValueError

#### Scenario: Invalid wait_interval
- **WHEN** EntryConfig.wait_interval is less than or equal to 0
- **THEN** __post_init__ raises ValueError

### Requirement: EntryConfig field defaults
The EntryConfig data class SHALL provide sensible defaults for all fields.

#### Scenario: Default values
- **WHEN** EntryConfig is created without arguments
- **THEN** wait_mode is "fast"
- **AND** wait_timeout is 10
- **AND** wait_interval is 1
- **AND** action_delay_ms is 100
- **AND** trace_level is "standard"

### Requirement: EntryConfig priority over meta
The system SHALL read entry_config values first, falling back to meta dictionary.

#### Scenario: EntryConfig is present
- **WHEN** TraversalPlan.entry_config is not None
- **THEN** system uses entry_config values for configuration

#### Scenario: EntryConfig absent, meta present
- **WHEN** TraversalPlan.entry_config is None
- **AND** meta dictionary contains configuration keys
- **THEN** system uses meta values with default fallback

#### Scenario: EntryConfig absent, meta absent
- **WHEN** TraversalPlan.entry_config is None
- **AND** meta dictionary does not contain configuration keys
- **THEN** system uses default values

### Requirement: EntryConfig serialization
The EntryConfig data class SHALL be serializable to and from JSON.

#### Scenario: Serialize EntryConfig to JSON
- **WHEN** TraversalPlan.to_json() is called with entry_config
- **THEN** entry_config is included in JSON output

#### Scenario: Deserialize EntryConfig from JSON
- **WHEN** TraversalPlan.from_json() is called with entry_config in JSON
- **THEN** EntryConfig object is reconstructed with validation
