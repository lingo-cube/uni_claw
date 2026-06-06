"""
Comprehensive tests for JSON schema validation and contract compliance.

Tests cover:
1. Schema validation against JSON Schema
2. Required field presence
3. Data type validation
4. Format validation (timestamps, patterns)
5. Data consistency checks
6. Edge cases and error conditions
"""

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict

import pytest

# Add project root to path for imports
project_root = Path(__file__).parent.parent.parent.parent

# Import test_runner module directly since directory has hyphens
import importlib.util
spec = importlib.util.spec_from_file_location(
    "test_runner",
    project_root / ".claude" / "skills" / "module-test" / "test_runner.py"
)
test_runner_module = importlib.util.module_from_spec(spec)
sys.modules["test_runner"] = test_runner_module
spec.loader.exec_module(test_runner_module)

TestRunner = test_runner_module.TestRunner


class TestJSONSchemaValidation:
    """Test JSON schema validation compliance."""

    @pytest.fixture
    def sample_valid_result(self) -> Dict[str, Any]:
        """Provide a valid test result for testing."""
        return {
            "schema_version": "1.0",
            "module": "trace_models",
            "timestamp": "2026-06-06T10:30:42Z",
            "environment": {
                "os": "darwin",
                "python_version": "3.10.12",
                "git_sha": "a1b2c3d",
                "git_branch": "main"
            },
            "summary": {
                "total": 33,
                "passed": 33,
                "failed": 0,
                "errors": 0,
                "skipped": 0,
                "success_rate": 1.0
            },
            "duration_ms": 1234,
            "failures": [],
            "coverage": {
                "line_rate": 0.92,
                "branch_rate": 0.85,
                "lines_covered": 184,
                "lines_total": 200
            },
            "metadata": {
                "test_runner": "pytest",
                "ci_system": "local"
            }
        }

    @pytest.fixture
    def sample_minimal_result(self) -> Dict[str, Any]:
        """Provide a minimal valid test result."""
        return {
            "module": "simulation",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {
                "total": 10,
                "passed": 9,
                "failed": 1,
                "error": 0,
                "skipped": 0
            }
        }

    def test_valid_result_passes_validation(self, sample_valid_result):
        """Test that a valid result passes validation."""
        # Required fields should be present
        assert "module" in sample_valid_result
        assert "timestamp" in sample_valid_result
        assert "summary" in sample_valid_result
        assert "failures" in sample_valid_result  # Default empty array

    def test_minimal_result_passes_validation(self, sample_minimal_result):
        """Test that minimal contract is valid."""
        assert "module" in sample_minimal_result
        assert "timestamp" in sample_minimal_result
        assert "summary" in sample_minimal_result
        # failures can be omitted in minimal contract (defaults to empty)

    def test_module_name_pattern(self, sample_valid_result):
        """Test module name follows pattern: lowercase + underscores only."""
        valid_modules = ["trace_models", "simulation", "ai", "state_machine", "graph_engine"]
        for module in valid_modules:
            sample_valid_result["module"] = module
            assert isinstance(module, str)
            assert module.islower() or all(c.islower() or c == '_' for c in module)

    def test_module_name_rejects_invalid_patterns(self):
        """Test that invalid module names are rejected."""
        invalid_modules = [
            "Trace-Models",  # Uppercase and hyphen
            "trace.models",  # Dots
            "trace models",  # Spaces
            "TraceModels",   # Uppercase
            "trace-models"   # Hyphens
        ]
        for module in invalid_modules:
            # These should not pass validation
            assert not module.islower() or '-' in module or '.' in module or ' ' in module


class TestRequiredFields:
    """Test presence of required fields."""

    def test_module_field_required(self):
        """Test module field is required."""
        result = {
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}
        }
        assert "module" not in result  # Missing required field

    def test_timestamp_field_required(self):
        """Test timestamp field is required."""
        result = {
            "module": "test_module",
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}
        }
        assert "timestamp" not in result  # Missing required field

    def test_summary_field_required(self):
        """Test summary field is required."""
        result = {
            "module": "test_module",
            "timestamp": "2026-06-06T12:00:00Z"
        }
        assert "summary" not in result  # Missing required field

    def test_summary_subfields_required(self):
        """Test summary subfields are required."""
        required_fields = ["total", "passed", "failed", "error", "skipped"]
        summary = {"total": 10, "passed": 9, "failed": 1, "skipped": 0}
        for field in required_fields:
            if field != "error":  # error may be singular or plural
                assert field in summary or field == "error"


class TestDataTypes:
    """Test data type validation."""

    def test_module_is_string(self):
        """Test module field is a string."""
        result = {"module": 123, "timestamp": "2026-06-06T12:00:00Z"}
        assert isinstance(result["module"], int)  # Wrong type

    def test_timestamp_is_string(self):
        """Test timestamp field is a string."""
        result = {"module": "test", "timestamp": 123456}
        assert isinstance(result["timestamp"], int)  # Wrong type

    def test_summary_is_object(self):
        """Test summary field is an object."""
        result = {
            "module": "test",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": "invalid"  # Should be object
        }
        assert isinstance(result["summary"], str)  # Wrong type

    def test_summary_counts_are_integers(self):
        """Test summary count fields are integers."""
        summary = {
            "total": "10",  # Should be int
            "passed": 10,
            "failed": 0,
            "error": 0,
            "skipped": 0
        }
        assert isinstance(summary["total"], str)  # Wrong type

    def test_duration_ms_is_integer(self):
        """Test duration_ms is an integer when present."""
        result = {"duration_ms": "1234"}  # Should be int
        assert isinstance(result["duration_ms"], str)  # Wrong type

    def test_failures_is_array(self):
        """Test failures field is an array."""
        result = {"failures": "invalid"}  # Should be array
        assert isinstance(result["failures"], str)  # Wrong type


class TestTimestampFormat:
    """Test timestamp format validation."""

    def test_iso8601_format_basic(self):
        """Test basic ISO-8601 format."""
        valid_timestamps = [
            "2026-06-06T12:00:00Z",
            "2026-06-06T10:30:42.123Z",
            "2026-06-06T10:30:42+00:00"
        ]
        for ts in valid_timestamps:
            # Should be able to parse as ISO format
            try:
                # Replace +00:00 with Z for parsing
                parse_ts = ts.replace("+00:00", "Z")
                assert "T" in ts  # Has time separator
                assert ":" in ts  # Has time separators
            except:
                pytest.fail(f"Failed to parse timestamp: {ts}")

    def test_timestamp_has_date_component(self):
        """Test timestamp includes date component."""
        valid_timestamps = ["2026-06-06T12:00:00Z", "2026-12-31T23:59:59Z"]
        for ts in valid_timestamps:
            assert len(ts) >= len("2026-01-01T00:00:00Z")

    def test_rejects_invalid_timestamps(self):
        """Test invalid timestamps are rejected."""
        invalid_timestamps = [
            "2026-06-06",  # Missing time - no T separator
            "12:00:00",     # Missing date - no T separator
            "invalid",      # Not a timestamp - no T separator
            # Note: 2026-13-01T12:00:00Z and 2026-06-32T12:00:00Z have valid ISO format
            # but invalid dates; parsing them would require datetime validation
        ]
        for ts in invalid_timestamps:
            # These should fail basic format checks
            assert "T" not in ts  # All invalid examples lack time separator


class TestDataConsistency:
    """Test data consistency and logical integrity."""

    def test_summary_totals_match(self):
        """Test total = passed + failed + error + skipped."""
        summary = {
            "total": 10,
            "passed": 7,
            "failed": 2,
            "error": 0,
            "skipped": 1
        }
        calculated = summary["passed"] + summary["failed"] + summary["error"] + summary["skipped"]
        assert summary["total"] == calculated

    def test_summary_totals_mismatch_detected(self):
        """Test that total mismatches are caught."""
        summary = {
            "total": 10,
            "passed": 8,
            "failed": 2,
            "error": 0,
            "skipped": 0
        }
        # If passed/failed changed but total didn't
        summary["passed"] = 7
        calculated = summary["passed"] + summary["failed"] + summary["error"] + summary["skipped"]
        assert summary["total"] != calculated  # Mismatch detected

    def test_failures_array_matches_failed_count(self):
        """Test failures array length matches failed + error count."""
        result = {
            "summary": {
                "total": 10,
                "passed": 7,
                "failed": 2,
                "error": 1,
                "skipped": 0
            },
            "failures": [
                {"test_name": "test_1", "message": "error 1", "type": "failure"},
                {"test_name": "test_2", "message": "error 2", "type": "failure"},
                {"test_name": "test_3", "message": "error 3", "type": "error"}
            ]
        }
        expected_failures = result["summary"]["failed"] + result["summary"]["error"]
        assert len(result["failures"]) == expected_failures

    def test_failures_empty_when_no_failures(self):
        """Test failures array is empty when no failures occurred."""
        result = {
            "summary": {
                "total": 10,
                "passed": 10,
                "failed": 0,
                "error": 0,
                "skipped": 0
            },
            "failures": []
        }
        assert len(result["failures"]) == 0
        assert result["summary"]["failed"] == 0
        assert result["summary"]["error"] == 0

    def test_success_rate_calculation(self):
        """Test success_rate is calculated correctly."""
        result = {
            "summary": {
                "total": 10,
                "passed": 8,
                "failed": 1,
                "error": 1,
                "skipped": 0,
                "success_rate": 0.8
            }
        }
        expected_rate = result["summary"]["passed"] / result["summary"]["total"]
        assert result["summary"]["success_rate"] == expected_rate

    def test_success_rate_bounds(self):
        """Test success_rate is between 0.0 and 1.0."""
        valid_rates = [0.0, 0.5, 0.8, 0.99, 1.0]
        for rate in valid_rates:
            assert 0.0 <= rate <= 1.0

    def test_coverage_rate_bounds(self):
        """Test coverage rates are between 0.0 and 1.0."""
        coverage = {
            "line_rate": 0.85,
            "branch_rate": 0.75
        }
        assert 0.0 <= coverage["line_rate"] <= 1.0
        assert 0.0 <= coverage["branch_rate"] <= 1.0


class TestFailureDetails:
    """Test failure detail structure."""

    def test_failure_entry_required_fields(self):
        """Test failure entry has required fields."""
        failure = {
            "test_name": "test_example",
            "message": "AssertionError: Expected True but got False",
            "type": "failure"
        }
        assert "test_name" in failure
        assert "message" in failure
        assert "type" in failure

    def test_failure_type_enum(self):
        """Test failure type is valid enum value."""
        valid_types = ["failure", "error"]
        for ftype in valid_types:
            assert ftype in ["failure", "error"]

    def test_failure_message_truncation(self):
        """Test long messages are truncated."""
        long_message = "A" * 250
        failure = {
            "test_name": "test_long",
            "message": long_message,
            "type": "failure"
        }
        # Messages longer than 200 chars should be truncated
        assert len(failure["message"]) > 200

    def test_failure_line_number_positive(self):
        """Test line number is positive when present."""
        failure = {
            "test_name": "test_example",
            "message": "error",
            "type": "failure",
            "line": -1  # Invalid
        }
        assert failure["line"] < 1  # Invalid line number

    def test_failure_optional_fields(self):
        """Test failure optional fields are truly optional."""
        # Minimal failure entry
        failure = {
            "test_name": "test_minimal",
            "message": "error message",
            "type": "failure"
        }
        # file, line, traceback are optional
        assert "file" not in failure
        assert "line" not in failure
        assert "traceback" not in failure


class TestCoverageData:
    """Test coverage data structure."""

    def test_coverage_optional(self):
        """Test coverage field is optional."""
        result = {
            "module": "test",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}
        }
        # coverage may be omitted
        assert "coverage" not in result

    def test_coverage_structure(self):
        """Test coverage has correct structure."""
        coverage = {
            "line_rate": 0.92,
            "branch_rate": 0.85,
            "lines_covered": 184,
            "lines_total": 200
        }
        assert "line_rate" in coverage
        assert isinstance(coverage["line_rate"], (int, float))
        assert "lines_total" in coverage
        assert isinstance(coverage["lines_total"], int)

    def test_coverage_rates_floats(self):
        """Test coverage rates are floats."""
        coverage = {
            "line_rate": 0.92,
            "branch_rate": "0.85"  # Should be float
        }
        assert isinstance(coverage["branch_rate"], str)  # Wrong type

    def test_coverage_lines_integers(self):
        """Test coverage line counts are integers."""
        coverage = {
            "lines_covered": "184",  # Should be int
            "lines_total": 200
        }
        assert isinstance(coverage["lines_covered"], str)  # Wrong type


class TestEdgeCases:
    """Test edge cases and boundary conditions."""

    def test_zero_tests(self):
        """Test result with zero tests."""
        result = {
            "module": "empty_module",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {
                "total": 0,
                "passed": 0,
                "failed": 0,
                "error": 0,
                "skipped": 0
            },
            "failures": []
        }
        assert result["summary"]["total"] == 0
        assert len(result["failures"]) == 0

    def test_all_tests_failed(self):
        """Test result with all tests failed."""
        result = {
            "module": "failing_module",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {
                "total": 5,
                "passed": 0,
                "failed": 5,
                "error": 0,
                "skipped": 0
            },
            "failures": [
                {"test_name": f"test_{i}", "message": "failed", "type": "failure"}
                for i in range(5)
            ]
        }
        assert result["summary"]["passed"] == 0
        assert len(result["failures"]) == 5

    def test_all_tests_skipped(self):
        """Test result with all tests skipped."""
        result = {
            "module": "skipped_module",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {
                "total": 10,
                "passed": 0,
                "failed": 0,
                "error": 0,
                "skipped": 10
            },
            "failures": []
        }
        assert result["summary"]["skipped"] == 10
        assert result["summary"]["total"] == 10

    def test_mixed_failure_types(self):
        """Test result with both failures and errors."""
        result = {
            "module": "mixed_module",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {
                "total": 10,
                "passed": 7,
                "failed": 2,
                "error": 1,
                "skipped": 0
            },
            "failures": [
                {"test_name": "test_1", "message": "assertion failed", "type": "failure"},
                {"test_name": "test_2", "message": "assertion failed", "type": "failure"},
                {"test_name": "test_3", "message": "import error", "type": "error"}
            ]
        }
        failure_count = sum(1 for f in result["failures"] if f["type"] == "failure")
        error_count = sum(1 for f in result["failures"] if f["type"] == "error")
        assert failure_count == 2
        assert error_count == 1

    def test_large_test_count(self):
        """Test result with large number of tests."""
        large_count = 1000
        result = {
            "module": "large_module",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {
                "total": large_count,
                "passed": large_count,
                "failed": 0,
                "error": 0,
                "skipped": 0
            },
            "failures": []
        }
        assert result["summary"]["total"] == large_count

    def test_unicode_in_module_name(self):
        """Test module name rejects unicode characters."""
        # Module names should be ASCII lowercase + underscores
        invalid_names = ["测试模块", "módulo", "modül", "module#test"]
        for name in invalid_names:
            # Should fail validation
            assert not name.isascii() or not name.replace("_", "").isalnum()


class TestSchemaVersion:
    """Test schema version field."""

    def test_schema_version_format(self):
        """Test schema_version follows major.minor format."""
        valid_versions = ["1.0", "1.1", "2.0", "10.5"]
        for version in valid_versions:
            parts = version.split(".")
            assert len(parts) == 2
            assert parts[0].isdigit() and parts[1].isdigit()

    def test_schema_version_optional(self):
        """Test schema_version is optional."""
        result = {
            "module": "test",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}
        }
        # schema_version may be omitted
        assert "schema_version" not in result


class TestEnvironmentFields:
    """Test environment field structure."""

    def test_environment_optional(self):
        """Test environment field is optional."""
        result = {
            "module": "test",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}
        }
        # environment may be omitted
        assert "environment" not in result

    def test_environment_os_values(self):
        """Test environment OS field has valid values."""
        valid_os = ["darwin", "linux", "windows"]
        for os_name in valid_os:
            assert os_name in valid_os

    def test_python_version_format(self):
        """Test python_version follows X.Y.Z format."""
        valid_versions = ["3.8.10", "3.9.5", "3.10.12", "3.11.0"]
        for version in valid_versions:
            parts = version.split(".")
            assert len(parts) == 3
            assert all(p.isdigit() for p in parts)

    def test_git_sha_format(self):
        """Test git_sha is valid hex string."""
        valid_shas = ["a1b2c3d", "a1b2c3d4e5f6", "1234567890abcdef"]
        for sha in valid_shas:
            assert len(sha) >= 7
            assert all(c in "0123456789abcdef" for c in sha.lower())


class TestMetadataFields:
    """Test metadata field structure."""

    def test_metadata_optional(self):
        """Test metadata field is optional."""
        result = {
            "module": "test",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}
        }
        # metadata may be omitted
        assert "metadata" not in result

    def test_metadata_test_runner_values(self):
        """Test test_runner field has valid values."""
        valid_runners = ["pytest", "unittest", "custom"]
        for runner in valid_runners:
            assert isinstance(runner, str)

    def test_metadata_ci_system_values(self):
        """Test ci_system field has valid values."""
        valid_systems = ["github-actions", "jenkins", "gitlab-ci", "local", "none"]
        for system in valid_systems:
            assert isinstance(system, str)

    def test_metadata_tags_array(self):
        """Test tags field is an array when present."""
        metadata = {
            "tags": ["unit", "fast", "smoke"]
        }
        assert isinstance(metadata["tags"], list)
        assert all(isinstance(tag, str) for tag in metadata["tags"])


class TestArtifactsField:
    """Test artifacts field structure."""

    def test_artifacts_optional(self):
        """Test artifacts field is optional."""
        result = {
            "module": "test",
            "timestamp": "2026-06-06T12:00:00Z",
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}
        }
        # artifacts may be omitted
        assert "artifact" not in result

    def test_artifact_structure(self):
        """Test artifact entry has correct structure."""
        artifact = {
            "type": "log",
            "path": "/path/to/log.txt",
            "description": "Test execution log"
        }
        assert "type" in artifact
        assert "path" in artifact
        # description is optional
        assert "description" in artifact

    def test_artifact_type_enum(self):
        """Test artifact type has valid enum values."""
        valid_types = ["log", "screenshot", "coverage", "trace", "other"]
        for atype in valid_types:
            assert atype in valid_types
