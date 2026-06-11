# test-constants Specification

## Purpose
TBD - created by archiving change test-hardcoding-reduction. Update Purpose after archive.
## Requirements
### Requirement: Test configuration constants shall be centrally managed
The test framework SHALL provide a centralized constants module at `tests/config/constants.py` that defines configuration constants for test execution control.

#### Scenario: Constants module exists
- **WHEN** test code imports from `tests.config.constants`
- **THEN** the module SHALL be importable without errors
- **AND** the module SHALL provide `Timeout`, `Retry`, `Concurrency`, and `ScrollThreshold` classes

### Requirement: Timeout constants shall define standard time intervals
The `Timeout` class SHALL define standard timeout values for different operation categories.

#### Scenario: Timeout constants are accessible
- **WHEN** test code accesses `Timeout.SHORT`, `Timeout.NORMAL`, `Timeout.LONG`, or `Timeout.FLUSH`
- **THEN** the values SHALL be 2, 5, 10, and 5.0 seconds respectively

#### Scenario: Timeout constant is used in test
- **WHEN** a test uses `timeout=Timeout.LONG`
- **THEN** the timeout value SHALL be 10 seconds
- **AND** the test code SHALL be more readable than `timeout=10`

### Requirement: Retry constants shall define retry limits
The `Retry` class SHALL define standard retry count values.

#### Scenario: Retry constants are accessible
- **WHEN** test code accesses `Retry.MAX_DEFAULT`, `Retry.MAX_EXTENDED`, `Retry.COUNT_ZERO`, or `Retry.COUNT_ONE`
- **THEN** the values SHALL be 3, 5, 0, and 1 respectively

#### Scenario: Retry constant is used in test
- **WHEN** a test uses `max_retries=Retry.MAX_DEFAULT`
- **THEN** the retry count SHALL be 3
- **AND** all tests using this constant SHALL be synchronized to the same value

### Requirement: Concurrency constants shall define concurrent operation limits
The `Concurrency` class SHALL define standard concurrent operation limits.

#### Scenario: Concurrency constants are accessible
- **WHEN** test code accesses `Concurrency.REQUESTS`, `Concurrency.MAX_CHILDREN_DEFAULT`, or `Concurrency.MAX_CHILDREN_SMALL`
- **THEN** the values SHALL be 20, 10, and 2 respectively

### Requirement: ScrollThreshold constants shall provide optional semantic position values
The `ScrollThreshold` class SHALL define semantic scroll position constants that MAY be used optionally.

#### Scenario: ScrollThreshold constants are accessible
- **WHEN** test code accesses `ScrollThreshold.START`, `ScrollThreshold.HALF`, or `ScrollThreshold.END`
- **THEN** the values SHALL be 0.0, 0.5, and 1.0 respectively

#### Scenario: Tests may use magic numbers for scroll positions
- **WHEN** a test uses `threshold=0.33` or any other value
- **THEN** the test SHALL function correctly
- **AND** the test SHALL NOT be required to use ScrollThreshold constants

### Requirement: Coordinate and ScreenSize shall NOT be defined as constants
The constants module SHALL NOT define `Coordinate` or `ScreenSize` classes as these represent business-generated values or test data.

#### Scenario: Coordinate class is absent
- **WHEN** test code attempts to access `constants.Coordinate.CENTER`
- **THEN** an AttributeError SHALL be raised
- **AND** tests SHALL use `CoordinateFactory` or direct values instead

### Requirement: Constants form design specifications
Once a value is defined as a constant, it SHALL be considered a design specification that SHOULD NOT be modified without impact assessment.

#### Scenario: Constant change requires consideration
- **WHEN** a developer considers changing `Timeout.LONG` from 10 to 15
- **THEN** the developer SHALL assess impact on all tests using this constant
- **AND** the change SHALL be documented in the change log

