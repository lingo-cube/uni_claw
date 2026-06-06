# Tasks: Test Architecture Standardization

## 1. Infrastructure Setup

- [x] 1.1 Create `test_results/` directory at project root
- [x] 1.2 Create `test_results/schema/` subdirectory
- [x] 1.3 Create `test_results/README.md` with directory usage documentation
- [x] 1.4 Create `test_results/schema/unit_result.schema.json` reference file

## 2. Test Runner Core Implementation

- [x] 2.1 Add required imports to `test_runner.py` (json, re, datetime, pathlib, typing)
- [x] 2.2 Modify `_build_test_command` method to add JSON report arguments
- [x] 2.3 Implement `_generate_standard_result` method for JSON generation
- [x] 2.4 Implement `_convert_from_raw` method for plugin-based transformation
- [x] 2.5 Implement `_convert_from_stdout` method for fallback parsing
- [x] 2.6 Implement `_write_final_json` method for file output
- [x] 2.7 Modify `_run_single_module` method to call JSON generation and cache stdout

## 3. Skill Documentation Updates

- [x] 3.1 Update `.claude/skills/module-test/SKILL.md` with standardized output section
- [x] 3.2 Update `.claude/skills/validation-documentation/SKILL.md` with data input protocol

## 4. Optional Validation Tool

- [x] 4.1 Create `scripts/validate_test_result.py` with validation logic
- [x] 4.2 Add executable permissions and usage documentation

## 5. Testing and Verification

- [x] 5.1 Test JSON generation with pytest-json-report plugin available
- [x] 5.2 Test JSON generation with stdout parsing fallback (plugin unavailable)
- [x] 5.3 Verify JSON output structure matches minimal contract
- [x] 5.4 Test with passing test suite (empty failures array)
- [x] 5.5 Test with failing test suite (failures populated correctly)
- [x] 5.6 Test coverage data inclusion when enabled
- [x] 5.7 Verify graceful degradation when JSON generation fails
- [x] 5.8 End-to-end test: module-test → validation-documentation workflow

## 6. Documentation Finalization

- [x] 6.1 Update main CLAUDE.md with reference to test architecture standardization
- [x] 6.2 Verify all documentation is accurate and complete
- [x] 6.3 Create examples demonstrating the new workflow
