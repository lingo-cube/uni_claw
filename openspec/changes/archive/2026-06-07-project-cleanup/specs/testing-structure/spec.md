# Spec: Testing Structure

## ADDED Requirements

### Requirement: Unit tests shall coexist with source code
Unit tests (tests for individual classes and functions) SHALL be placed in the same directory as the code they test, under the src/ directory structure.

#### Scenario: Unit test file location
- **WHEN** creating a unit test for a module
- **THEN** the test file SHALL be named test_<module>.py or <module>_test.py
- **AND** the test file SHALL be placed in the same directory as the module under src/

#### Scenario: Unit test import paths
- **WHEN** writing unit tests in src/ module directories
- **THEN** tests SHALL import from sibling modules using relative imports or direct module paths
- **AND** tests SHALL NOT rely on sys.path manipulation

### Requirement: Integration tests shall be in tests/ directory
Integration tests (tests that verify multiple components working together) SHALL be placed in the tests/ directory with appropriate subdirectories.

#### Scenario: Integration test organization
- **WHEN** viewing the tests/ directory
- **THEN** the following subdirectories SHALL exist:
  - tests/integration/ - Multi-component integration tests
  - tests/simulation/ - Mock environment simulation tests
  - tests/performance/ - Performance and benchmark tests
  - tests/e2e/ - End-to-end tests with real devices/services

#### Scenario: Integration test file location
- **WHEN** creating an integration test
- **THEN** the test file SHALL be placed in tests/integration/ or appropriate subdirectory
- **AND** the test file name clearly indicate it is an integration test

### Requirement: Test type classification
Tests SHALL be classified based on what they test:
- **Unit tests**: Test individual classes/functions in isolation using mocks
- **Integration tests**: Test multiple modules working together
- **Simulation tests**: Test using mock environments (no real devices)
- **Performance tests**: Benchmark and load testing
- **E2E tests**: Complete workflows with real services/devices

#### Scenario: Unit test characteristics
- **WHEN** reviewing a unit test
- **THEN** the test SHALL exercise a single class or function
- **AND** the test SHALL use mocks for external dependencies
- **AND** the test SHALL complete quickly (< 1 second per test)

#### Scenario: Integration test characteristics
- **WHEN** reviewing an integration test
- **THEN** the test SHALL verify multiple components working together
- **AND** the test MAY use real services or carefully controlled mocks
- **AND** the test SHALL verify component interfaces and contracts

### Requirement: Pytest configuration
The project SHALL configure pytest to discover tests in both src/ and tests/ directories.

#### Scenario: Pytest discovers all tests
- **WHEN** running pytest without arguments
- **THEN** pytest SHALL discover tests in src/ directories (unit tests)
- **AND** pytest SHALL discover tests in tests/ directories (integration/simulation tests)
- **AND** all test files matching test_*.py or *_test.py patterns SHALL be found

#### Scenario: Selective test execution
- **WHEN** running pytest with directory argument
- **THEN** only tests in specified directory SHALL be executed
- **AND** developers MAY run only unit tests (pytest src/) or only integration tests (pytest tests/)

### Requirement: Shared test fixtures
Common test fixtures and utilities SHALL be organized in tests/ directory and accessible to all tests.

#### Scenario: Fixture location
- **WHEN** creating shared test fixtures
- **THEN** fixtures SHALL be placed in tests/fixtures/ directory
- **AND** fixture files SHALL be clearly named by what they provide

#### Scenario: Test helpers and utilities
- **WHEN** creating test helper functions
- **THEN** helpers SHALL be placed in appropriate subdirectories (tests/simulation/helpers/, etc.)
- **AND** helper modules SHALL be clearly named for their purpose
