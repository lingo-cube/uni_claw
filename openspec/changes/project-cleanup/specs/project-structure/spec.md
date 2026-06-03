# Spec: Project Structure

## ADDED Requirements

### Requirement: Root directory shall be clean
The project root directory SHALL only contain essential configuration and documentation files. Temporary scripts, test files, and development artifacts SHALL NOT be placed in the root directory.

#### Scenario: Root directory contains only essential files
- **WHEN** listing files in the project root directory
- **THEN** only configuration files (pyproject.toml, .gitignore, CLAUDE.md, README.md) and essential documentation are present
- **AND** no test_*.py, run_*.py, or analyze_*.py temporary scripts exist in root

#### Scenario: Temporary scripts are organized
- **WHEN** a developer creates a temporary script
- **THEN** the script MUST be placed in scripts/tmp/ or appropriate subdirectory
- **AND** the script MUST use a descriptive prefix (tmp_, debug_, exp_)

### Requirement: Scripts directory shall have clear organization
The scripts/ directory SHALL be organized by function with subdirectories for different types of utility scripts.

#### Scenario: Scripts subdirectory structure exists
- **WHEN** viewing the scripts/ directory
- **THEN** the following subdirectories SHALL exist:
  - scripts/analysis/ - Data analysis and inspection scripts
  - scripts/debug/ - Debugging and diagnosis scripts
  - scripts/verify/ - Verification and validation scripts
  - scripts/visualization/ - Report generation and visualization scripts
  - scripts/tmp/ - Temporary scripts (cleared periodically)

### Requirement: Documents shall be properly organized
All documentation SHALL be organized in the docs/ directory with clear categorization. Temporary documentation SHALL NOT be created in the project root.

#### Scenario: Documentation location
- **WHEN** creating project documentation
- **THEN** documentation MUST be placed in docs/ directory
- **AND** temporary documentation (test reports, quick notes) MUST be placed in appropriate subdirectories or deleted after use

#### Scenario: Temporary test reports
- **WHEN** generating test reports
- **THEN** reports MUST be placed in tests/reports/ directory
- **AND** report files MUST be added to .gitignore
- **AND** reports MUST be named with timestamp or git SHA for traceability

### Requirement: Archive directory for cleanup process
During cleanup operations, a .cleanup_archive/ directory MAY be created for temporary file staging. Files in this archive SHALL be reviewed and either restored to correct locations or deleted.

#### Scenario: Cleanup archive creation
- **WHEN** performing project cleanup
- **THEN** a .cleanup_archive/ directory MAY be created
- **AND** files moved to archive SHALL be reviewed within 7 days
- **AND** after review, useful files SHALL be moved to correct locations
- **AND** unused files SHALL be deleted
