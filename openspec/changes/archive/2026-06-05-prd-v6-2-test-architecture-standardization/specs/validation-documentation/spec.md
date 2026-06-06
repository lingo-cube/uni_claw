# Spec: Validation Documentation Enhancement

## ADDED Requirements

### Requirement: Standardized data ingestion
The validation documentation generator SHALL read test data exclusively from standardized JSON files in test_results/ directory.

#### Scenario: Data availability check
- **WHEN** validation documentation generation begins
- **THEN** system checks for `*_unit.json` files in test_results/
- **AND** proceeds if at least one file exists
- **AND** prompts user to run tests if no files found

#### Scenario: Multi-module data aggregation
- **WHEN** multiple module JSON files are present
- **THEN** system reads all `*_unit.json` files
- **AND** aggregates statistics across all modules
- **AND** calculates global test counts and pass rates

### Requirement: Data freshness validation
The validation documentation SHALL check timestamp of test results and warn if data is older than 48 hours.

#### Scenario: Fresh data for reporting
- **WHEN** test result timestamps are within 48 hours
- **THEN** validation report proceeds without warnings
- **AND** data is considered current for validation purposes

#### Scenario: Stale data warning
- **WHEN** any test result is older than 48 hours
- **THEN** system includes prominent warning in report
- **AND** lists specific modules with stale data
- **AND** suggests re-running tests for current validation

### Requirement: JSON structure validation
The validation documentation SHALL verify basic JSON structure before processing.

#### Scenario: Valid JSON structure
- **WHEN** JSON file has all required fields
- **THEN** system proceeds with report generation
- **AND** uses data for statistics and analysis

#### Scenario: Invalid JSON structure
- **WHEN** JSON file is malformed or missing required fields
- **THEN** system reports specific validation error
- **AND** identifies problematic file and field
- **AND** continues with other valid files if available

### Requirement: Aggregated statistics calculation
The validation documentation SHALL calculate global statistics from individual module results.

#### Scenario: Global test count aggregation
- **WHEN** processing multiple module JSON files
- **THEN** system calculates total tests across all modules
- **AND** calculates total passed, failed, error, skipped counts
- **AND** computes overall pass rate percentage

#### Scenario: Overall status determination
- **WHEN** aggregating results from all modules
- **THEN** system sets overall status to "PASSED" if total failed equals 0
- **AND** sets overall status to "HAS FAILURES" if any failures exist

### Requirement: Failure analysis integration
The validation documentation SHALL include failure details from standardized JSON in validation reports.

#### Scenario: Failure details in report
- **WHEN** generating validation report with failures
- **THEN** report includes test names from failures array
- **AND** includes error messages for each failure
- **AND** categorizes by failure type (failure vs error)

### Requirement: Coverage metrics inclusion
The validation documentation SHALL include coverage data when available in test results.

#### Scenario: Coverage data present
- **WHEN** JSON files include coverage metrics
- **THEN** validation report includes line and branch coverage rates
- **AND** aggregates coverage across modules when possible
- **AND** notes modules without coverage data

#### Scenario: Coverage data absent
- **WHEN** JSON files do not include coverage metrics
- **THEN** validation report omits coverage section
- **AND** does not indicate error or missing data
