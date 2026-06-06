# Spec: Module Test Enhancement

## ADDED Requirements

### Requirement: Automatic JSON generation
The test runner SHALL automatically generate standardized JSON output files during test execution without requiring additional user configuration.

#### Scenario: JSON generation during test execution
- **WHEN** module tests are executed via test_runner.py
- **THEN** system automatically generates `{module}_unit.json` in test_results/
- **AND** generation occurs as part of normal test flow
- **AND** no additional flags or configuration are required

### Requirement: Hybrid generation strategy
The test runner SHALL attempt pytest-json-report plugin first and automatically fall back to stdout parsing if plugin is unavailable.

#### Scenario: Primary method with plugin available
- **WHEN** pytest-json-report plugin is installed
- **THEN** test runner uses plugin to generate raw JSON
- **AND** transforms raw JSON into minimal contract format
- **AND** reports successful generation

#### Scenario: Fallback to stdout parsing
- **WHEN** pytest-json-report plugin is not available
- **THEN** test runner falls back to stdout parsing method
- **AND** extracts summary statistics from pytest output
- **AND** generates compliant JSON using parsed data
- **AND** reports fallback method was used

### Requirement: Command line argument modification
The test runner SHALL add appropriate pytest arguments to enable JSON report generation.

#### Scenario: JSON report arguments added
- **WHEN** building pytest command
- **THEN** system adds `--json-report` flag
- **AND** adds `--json-report-file` with path to `{module}_unit_raw.json`
- **AND** raw file is used as intermediate for transformation

### Requirement: Stdout caching for fallback
The test runner SHALL cache test execution stdout to enable fallback parsing when plugin method fails.

#### Scenario: Stdout is cached during execution
- **WHEN** tests are executed via subprocess
- **THEN** test runner stores stdout in memory
- **AND** cached stdout is available for fallback parsing
- **AND** cache is cleared after successful transformation

### Requirement: Coverage integration
The test runner SHALL include coverage data in JSON output when coverage is enabled in test configuration.

#### Scenario: Coverage XML generation
- **WHEN** coverage is enabled in test config
- **THEN** test runner adds `--cov` and `--cov-report` arguments
- **AND** generates coverage XML file in test_results/
- **AND** includes coverage metrics in final JSON

### Requirement: Graceful degradation
JSON generation failure SHALL NOT prevent test execution from completing successfully.

#### Scenario: JSON generation failure does not block tests
- **WHEN** JSON generation encounters an error
- **THEN** test execution completes normally
- **AND** error is logged but does not cause failure
- **AND** test results are still returned to caller
