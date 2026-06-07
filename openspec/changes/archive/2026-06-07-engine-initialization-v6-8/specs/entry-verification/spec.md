## ADDED Requirements

### Requirement: Entry condition verification modes
The system SHALL support two verification modes: fast (single check) and polling (repeated checks until timeout).

#### Scenario: Fast mode single check
- **WHEN** wait_mode is "fast"
- **THEN** system performs single vision check after entry strategy

#### Scenario: Polling mode repeated checks
- **WHEN** wait_mode is "polling"
- **THEN** system performs repeated vision checks until timeout or success

### Requirement: Fast mode verification
The system SHALL verify entry condition with a single vision call in fast mode.

#### Scenario: Fast mode succeeds
- **WHEN** wait_mode is "fast"
- **AND** vision returns current_path matching expected page_name
- **THEN** verification returns True

#### Scenario: Fast mode fails
- **WHEN** wait_mode is "fast"
- **AND** vision returns current_path not matching expected page_name
- **THEN** verification returns False

### Requirement: Polling mode verification
The system SHALL repeatedly verify entry condition until timeout in polling mode.

#### Scenario: Polling mode succeeds before timeout
- **WHEN** wait_mode is "polling"
- **AND** vision returns current_path matching expected page_name before wait_timeout
- **THEN** verification returns True immediately

#### Scenario: Polling mode times out
- **WHEN** wait_mode is "polling"
- **AND** vision never returns current_path matching expected page_name within wait_timeout
- **THEN** verification returns False after timeout

### Requirement: No wait condition
The system SHALL skip verification when wait_condition is not configured.

#### Scenario: No wait condition configured
- **WHEN** entry_policy.wait_condition is None or empty
- **THEN** verification returns True immediately

### Requirement: Action delay before verification
The system SHALL wait for configured delay after action before verification.

#### Scenario: Configured delay is applied
- **WHEN** action_delay_ms is configured
- **THEN** system waits action_delay_ms milliseconds before calling vision
