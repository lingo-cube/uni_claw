# Spec: Standardized Test Results

## ADDED Requirements

### Requirement: JSON output directory structure
The system SHALL create a `test_results/` directory at project root with standardized subdirectories for storing test result JSON files.

#### Scenario: Directory creation on first run
- **WHEN** test execution runs for the first time
- **THEN** system creates `test_results/` directory automatically
- **AND** creates `test_results/schema/` subdirectory

### Requirement: Minimal JSON contract
The system SHALL generate test result JSON files following a minimal 5-field contract containing module identifier, timestamp, summary statistics, failure details, and optional coverage metrics.

#### Scenario: JSON file generation after test execution
- **WHEN** module tests complete execution
- **THEN** system generates `{module}_unit.json` file
- **AND** file contains all required fields (module, timestamp, summary, failures)
- **AND** file contains coverage data if available

#### Scenario: JSON structure compliance
- **WHEN** JSON file is generated
- **THEN** `module` field contains lowercase alphanumeric name with underscores
- **AND** `timestamp` field contains ISO-8601 formatted UTC timestamp
- **AND** `summary` object contains total, passed, failed, error, skipped counts
- **AND** `failures` array contains failure details for failed/error tests
- **AND** summary counts satisfy: total = passed + failed + error + skipped

### Requirement: File naming convention
The system SHALL use consistent naming convention `{module}_unit.json` where module name matches code module identifier.

#### Scenario: Standard naming for simulation module
- **WHEN** simulation module tests are executed
- **THEN** output file is named `simulation_unit.json`
- **AND** file is located in `test_results/` directory

#### Scenario: No version or date in filenames
- **WHEN** JSON files are generated
- **THEN** filenames contain only module identifier and `_unit.json` suffix
- **AND** no version numbers or timestamps appear in filename

### Requirement: File overwrite behavior
The system SHALL overwrite existing JSON files on each test run without maintaining historical versions.

#### Scenario: Subsequent test run overwrites previous file
- **WHEN** module tests are executed a second time
- **THEN** existing `{module}_unit.json` file is overwritten
- **AND** only the most recent test results are stored

### Requirement: Coverage data inclusion
The system SHALL include coverage metrics in JSON output when coverage collection is enabled.

#### Scenario: Coverage data present when enabled
- **WHEN** tests run with coverage enabled
- **THEN** `coverage` object contains line_rate and branch_rate
- **AND** both values are floats between 0.0 and 1.0

#### Scenario: Coverage omitted when disabled
- **WHEN** tests run without coverage enabled
- **THEN** `coverage` field may be omitted or empty object
- **AND** JSON remains valid without coverage data

### Requirement: Failure detail extraction
The system SHALL extract and include failure information for each failed or errored test in the failures array.

#### Scenario: Failed test appears in failures array
- **WHEN** a test fails during execution
- **THEN** failures array contains entry with test name, error message, and type
- **AND** message is truncated to maximum 200 characters if longer

#### Scenario: Empty failures array for all-passing suite
- **WHEN** all tests pass without failures
- **THEN** failures array is empty `[]`
- **AND** summary.failed and summary.error are both 0
