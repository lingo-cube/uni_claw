# Spec: Test Result Validation

## ADDED Requirements

### Requirement: JSON structure validation
The system SHALL provide optional validation tool to verify JSON structure compliance with the minimal test result contract.

#### Scenario: Validation of compliant JSON file
- **WHEN** validation tool runs on compliant `{module}_unit.json`
- **THEN** tool reports successful validation
- **AND** confirms all required fields are present
- **AND** verifies data consistency constraints are satisfied

#### Scenario: Validation detects missing required field
- **WHEN** validation tool runs on JSON with missing required field
- **THEN** tool reports validation failure
- **AND** identifies which required field is missing

### Requirement: Data consistency verification
The validation tool SHALL verify mathematical consistency between summary counts and failures array length.

#### Scenario: Summary counts consistency check
- **WHEN** validation tool checks summary field
- **THEN** tool verifies total equals sum of passed, failed, error, skipped
- **AND** reports error if counts are inconsistent

#### Scenario: Failures array consistency check
- **WHEN** validation tool checks failures array
- **THEN** tool verifies array is empty when failed + error equals 0
- **AND** reports error if failures exist but summary shows no failures

### Requirement: Batch validation capability
The validation tool SHALL support validating all test result JSON files in the test_results directory.

#### Scenario: Validate all modules
- **WHEN** validation tool runs without specifying module
- **THEN** tool validates all `*_unit.json` files in test_results/
- **AND** reports overall pass/fail status
- **AND** lists specific files that failed validation

#### Scenario: Validate single module
- **WHEN** validation tool runs with specific module name
- **THEN** tool validates only that module's JSON file
- **AND** reports detailed results for that file
