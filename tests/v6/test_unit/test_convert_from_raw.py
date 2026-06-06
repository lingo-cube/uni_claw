"""
Comprehensive tests for converting pytest-json-report raw output to standard format.

Tests cover:
1. Normal conversion scenarios
2. Missing field handling
3. Coverage data extraction
4. Failure detail extraction
5. Edge cases and error conditions
"""

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict
from unittest.mock import Mock, patch

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


class TestNormalConversion:
    """Test normal conversion scenarios from pytest-json-report format."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    @pytest.fixture
    def sample_raw_json(self) -> Dict[str, Any]:
        """Provide sample pytest-json-report raw JSON."""
        return {
            "summary": {
                "total": 25,
                "passed": 23,
                "failed": 1,
                "error": 1,
                "skipped": 0
            },
            "tests": [
                {
                    "nodeid": "tests/test_example.py::test_passing",
                    "name": "test_passing",
                    "outcome": "passed"
                },
                {
                    "nodeid": "tests/test_example.py::test_failing",
                    "name": "test_failing",
                    "outcome": "failed",
                    "call": {
                        "longrepr": "AssertionError: Expected True but got False"
                    }
                },
                {
                    "nodeid": "tests/test_example.py::test_error",
                    "name": "test_error",
                    "outcome": "error",
                    "longrepr": "ImportError: Module not found"
                }
            ]
        }

    def test_convert_basic_summary(self, runner, sample_raw_json):
        """Test basic summary conversion."""
        result = runner._convert_from_raw(sample_raw_json, "test_module")

        assert result["module"] == "test_module"
        assert "timestamp" in result
        assert result["summary"]["total"] == 25
        assert result["summary"]["passed"] == 23
        assert result["summary"]["failed"] == 1
        assert result["summary"]["error"] == 1
        assert result["summary"]["skipped"] == 0

    def test_convert_passing_tests(self, runner):
        """Test conversion with all passing tests."""
        raw_json = {
            "summary": {
                "total": 10,
                "passed": 10,
                "failed": 0,
                "error": 0,
                "skipped": 0
            },
            "tests": [
                {"nodeid": f"tests/test.py::test_{i}", "name": f"test_{i}", "outcome": "passed"}
                for i in range(10)
            ]
        }
        result = runner._convert_from_raw(raw_json, "passing_module")

        assert result["summary"]["total"] == 10
        assert result["summary"]["passed"] == 10
        assert result["summary"]["failed"] == 0
        assert len(result["failures"]) == 0

    def test_convert_mixed_outcomes(self, runner):
        """Test conversion with mixed test outcomes."""
        raw_json = {
            "summary": {
                "total": 20,
                "passed": 15,
                "failed": 2,
                "error": 1,
                "skipped": 2
            },
            "tests": [
                {"nodeid": f"tests/test.py::test_pass_{i}", "outcome": "passed"}
                for i in range(15)
            ] + [
                {"nodeid": "tests/test.py::test_fail_1", "outcome": "failed",
                 "call": {"longrepr": "failed 1"}},
                {"nodeid": "tests/test.py::test_fail_2", "outcome": "failed",
                 "call": {"longrepr": "failed 2"}},
                {"nodeid": "tests/test.py::test_error", "outcome": "error",
                 "longrepr": "error message"},
                {"nodeid": "tests/test.py::test_skip_1", "outcome": "skipped"},
                {"nodeid": "tests/test.py::test_skip_2", "outcome": "skipped"}
            ]
        }
        result = runner._convert_from_raw(raw_json, "mixed_module")

        assert result["summary"]["total"] == 20
        assert result["summary"]["passed"] == 15
        assert result["summary"]["failed"] == 2
        assert result["summary"]["error"] == 1
        assert result["summary"]["skipped"] == 2


class TestMissingFieldHandling:
    """Test handling of missing or incomplete fields in raw JSON."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_missing_summary_field(self, runner):
        """Test handling when summary field is missing."""
        raw_json = {
            "tests": []
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        # Should use default values
        assert result["summary"]["total"] == 0
        assert result["summary"]["passed"] == 0
        assert result["summary"]["failed"] == 0
        assert result["summary"]["error"] == 0
        assert result["summary"]["skipped"] == 0

    def test_missing_tests_field(self, runner):
        """Test handling when tests field is missing."""
        raw_json = {
            "summary": {
                "total": 10,
                "passed": 10,
                "failed": 0,
                "error": 0,
                "skipped": 0
            }
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert result["summary"]["total"] == 10
        assert len(result["failures"]) == 0

    def test_missing_summary_subfields(self, runner):
        """Test handling when summary subfields are missing."""
        raw_json = {
            "summary": {
                "total": 5
                # passed, failed, error, skipped missing
            },
            "tests": []
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        # Missing fields should default to 0
        assert result["summary"]["total"] == 5
        assert result["summary"]["passed"] == 0
        assert result["summary"]["failed"] == 0
        assert result["summary"]["error"] == 0
        assert result["summary"]["skipped"] == 0

    def test_empty_raw_json(self, runner):
        """Test handling of completely empty raw JSON."""
        raw_json = {}
        result = runner._convert_from_raw(raw_json, "test_module")

        assert result["module"] == "test_module"
        assert "timestamp" in result
        assert result["summary"]["total"] == 0
        assert len(result["failures"]) == 0


class TestCoverageExtraction:
    """Test coverage data extraction from XML files."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    @pytest.fixture
    def mock_coverage_xml(self, tmp_path):
        """Create a mock coverage XML file."""
        coverage_content = '''<?xml version="1.0" encoding="UTF-8"?>
<coverage line-rate="0.85" branch-rate="0.75" lines-covered="170" lines-total="200">
</coverage>'''
        coverage_file = tmp_path / "test_module_coverage.xml"
        coverage_file.write_text(coverage_content)
        return coverage_file

    def test_extract_coverage_data_exists(self, runner, tmp_path):
        """Test extracting coverage when XML file exists."""
        # Create coverage XML with proper attributes (uses hyphens in XML)
        coverage_content = '''<?xml version="1.0" encoding="UTF-8"?>
<coverage line-rate="0.85" branch-rate="0.75">
</coverage>'''
        results_dir = tmp_path / "test_results"
        results_dir.mkdir(parents=True, exist_ok=True)
        coverage_file = results_dir / "test_module_coverage.xml"
        coverage_file.write_text(coverage_content)

        with patch.object(runner, 'project_root', tmp_path):
            result = runner._extract_coverage_data("test_module")

            assert result["line_rate"] == 0.85
            assert result["branch_rate"] == 0.75

    def test_extract_coverage_data_missing(self, runner):
        """Test handling when coverage XML doesn't exist."""
        result = runner._extract_coverage_data("nonexistent_module")

        assert result == {}

    def test_extract_coverage_invalid_xml(self, runner, tmp_path):
        """Test handling when coverage XML is malformed."""
        # Create invalid XML file
        invalid_file = tmp_path / "test_results" / "invalid_coverage.xml"
        invalid_file.parent.mkdir(parents=True, exist_ok=True)
        invalid_file.write_text("not valid xml")

        with patch.object(runner, 'project_root', tmp_path):
            result = runner._extract_coverage_data("invalid")

            # Should return empty dict on error
            assert result == {}

    def test_extract_coverage_missing_attributes(self, runner, tmp_path):
        """Test handling when XML is missing required attributes."""
        # Create XML with missing attributes
        incomplete_xml = '''<?xml version="1.0"?>
<coverage>
</coverage>'''
        coverage_file = tmp_path / "test_results" / "test_coverage.xml"
        coverage_file.parent.mkdir(parents=True, exist_ok=True)
        coverage_file.write_text(incomplete_xml)

        with patch.object(runner, 'project_root', tmp_path):
            result = runner._extract_coverage_data("test")

            # Missing attributes default to 0.0
            assert result == {"line_rate": 0.0, "branch_rate": 0.0}

    def test_extract_coverage_invalid_format(self, runner, tmp_path):
        """Test handling when coverage values have invalid format."""
        # Create XML with non-numeric values
        bad_xml = '''<?xml version="1.0"?>
<coverage line-rate="invalid" branch-rate="also_invalid">
</coverage>'''
        coverage_file = tmp_path / "test_results" / "bad_coverage.xml"
        coverage_file.parent.mkdir(parents=True, exist_ok=True)
        coverage_file.write_text(bad_xml)

        with patch.object(runner, 'project_root', tmp_path):
            result = runner._extract_coverage_data("bad")

            # Invalid formats should result in empty dict
            assert result == {}


class TestFailureDetailExtraction:
    """Test extraction of failure details from test results."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_extract_failure_with_call_longrepr(self, runner):
        """Test extracting failure message from call.longrepr."""
        raw_json = {
            "summary": {"total": 1, "passed": 0, "failed": 1, "error": 0, "skipped": 0},
            "tests": [
                {
                    "nodeid": "tests/test.py::test_fail",
                    "name": "test_fail",
                    "outcome": "failed",
                    "call": {
                        "longrepr": "AssertionError: Expected 5 but got 3"
                    }
                }
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert len(result["failures"]) == 1
        assert result["failures"][0]["name"] == "tests/test.py::test_fail"
        assert result["failures"][0]["message"] == "AssertionError: Expected 5 but got 3"
        # The actual implementation uses outcome directly, which can be 'failed' or 'error'
        assert result["failures"][0]["type"] in ("failed", "failure")

    def test_extract_failure_with_longrepr(self, runner):
        """Test extracting failure message from top-level longrepr."""
        raw_json = {
            "summary": {"total": 1, "passed": 0, "failed": 1, "error": 0, "skipped": 0},
            "tests": [
                {
                    "nodeid": "tests/test.py::test_error",
                    "name": "test_error",
                    "outcome": "error",
                    "longrepr": "ImportError: No module named 'missing'"
                }
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert len(result["failures"]) == 1
        assert "missing" in result["failures"][0]["message"]

    def test_extract_failure_with_message(self, runner):
        """Test extracting failure message from message field."""
        raw_json = {
            "summary": {"total": 1, "passed": 0, "failed": 1, "error": 0, "skipped": 0},
            "tests": [
                {
                    "nodeid": "tests/test.py::test_fail",
                    "name": "test_fail",
                    "outcome": "failed",
                    "message": "Test failed intentionally"
                }
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert len(result["failures"]) == 1
        assert result["failures"][0]["message"] == "Test failed intentionally"

    def test_extract_failure_without_message(self, runner):
        """Test handling failure when no message is available."""
        raw_json = {
            "summary": {"total": 1, "passed": 0, "failed": 1, "error": 0, "skipped": 0},
            "tests": [
                {
                    "nodeid": "tests/test.py::test_fail",
                    "name": "test_fail",
                    "outcome": "failed"
                    # No message available
                }
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert len(result["failures"]) == 1
        # Message should be empty string when not available
        assert result["failures"][0]["message"] == ""

    def test_message_truncation(self, runner):
        """Test that long messages are truncated."""
        long_message = "A" * 300
        raw_json = {
            "summary": {"total": 1, "passed": 0, "failed": 1, "error": 0, "skipped": 0},
            "tests": [
                {
                    "nodeid": "tests/test.py::test_long",
                    "name": "test_long",
                    "outcome": "failed",
                    "call": {"longrepr": long_message}
                }
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        # Message should be truncated to 200 chars + '...'
        assert len(result["failures"][0]["message"]) <= 203
        assert result["failures"][0]["message"].endswith("...")


class TestEdgeCases:
    """Test edge cases and boundary conditions."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_convert_with_null_module_name(self, runner):
        """Test handling of None/empty module name."""
        raw_json = {
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0},
            "tests": []
        }
        result = runner._convert_from_raw(raw_json, "")

        assert result["module"] == ""

    def test_convert_with_special_characters_in_name(self, runner):
        """Test handling of special characters in test names."""
        raw_json = {
            "summary": {"total": 1, "passed": 0, "failed": 1, "error": 0, "skipped": 0},
            "tests": [
                {
                    "nodeid": "tests/test.py::test_with-special_chars[1-2]",
                    "name": "test_with-special_chars[1-2]",
                    "outcome": "failed",
                    "call": {"longrepr": "failed"}
                }
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert "special_chars" in result["failures"][0]["name"]

    def test_convert_with_unicode_characters(self, runner):
        """Test handling of unicode characters in messages."""
        raw_json = {
            "summary": {"total": 1, "passed": 0, "failed": 1, "error": 0, "skipped": 0},
            "tests": [
                {
                    "nodeid": "tests/test.py::test_unicode",
                    "name": "test_unicode",
                    "outcome": "failed",
                    "call": {"longrepr": "AssertionError: 值不等于 expected"}
                }
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert "值" in result["failures"][0]["message"]

    def test_convert_with_very_large_test_count(self, runner):
        """Test handling of large test counts."""
        large_count = 10000
        raw_json = {
            "summary": {
                "total": large_count,
                "passed": large_count,
                "failed": 0,
                "error": 0,
                "skipped": 0
            },
            "tests": []
        }
        result = runner._convert_from_raw(raw_json, "large_module")

        assert result["summary"]["total"] == large_count

    def test_convert_with_all_test_types(self, runner):
        """Test conversion with all possible test outcomes."""
        raw_json = {
            "summary": {
                "total": 4,
                "passed": 1,
                "failed": 1,
                "error": 1,
                "skipped": 1
            },
            "tests": [
                {"nodeid": "tests/test.py::test_pass", "outcome": "passed"},
                {"nodeid": "tests/test.py::test_fail", "outcome": "failed",
                 "call": {"longrepr": "failed"}},
                {"nodeid": "tests/test.py::test_error", "outcome": "error",
                 "longrepr": "error"},
                {"nodeid": "tests/test.py::test_skip", "outcome": "skipped"}
            ]
        }
        result = runner._convert_from_raw(raw_json, "all_types")

        assert result["summary"]["passed"] == 1
        assert result["summary"]["failed"] == 1
        assert result["summary"]["error"] == 1
        assert result["summary"]["skipped"] == 1
        assert len(result["failures"]) == 2  # failed + error


class TestTimestampGeneration:
    """Test timestamp generation in converted results."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_timestamp_is_present(self, runner):
        """Test that timestamp is always present."""
        raw_json = {
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0},
            "tests": []
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert "timestamp" in result
        assert isinstance(result["timestamp"], str)

    def test_timestamp_is_recent(self, runner):
        """Test that timestamp is recent (within last minute)."""
        import time
        raw_json = {
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0},
            "tests": []
        }
        before_conversion = time.time()
        result = runner._convert_from_raw(raw_json, "test_module")
        after_conversion = time.time()

        # Parse timestamp
        from datetime import datetime
        timestamp = datetime.fromisoformat(result["timestamp"].replace('Z', '+00:00'))
        timestamp_seconds = timestamp.timestamp()

        # Should be within conversion time window
        assert before_conversion <= timestamp_seconds <= after_conversion

    def test_timestamp_is_utc(self, runner):
        """Test that timestamp is in UTC."""
        raw_json = {
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0},
            "tests": []
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        # Should end with Z or +00:00
        assert result["timestamp"].endswith('Z') or '+00:00' in result["timestamp"]


class TestModuleFieldAssignment:
    """Test module field is correctly assigned."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_module_field_from_parameter(self, runner):
        """Test module field comes from parameter."""
        raw_json = {
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0},
            "tests": []
        }
        result = runner._convert_from_raw(raw_json, "custom_module")

        assert result["module"] == "custom_module"

    def test_module_field_not_from_raw(self, runner):
        """Test module field doesn't come from raw JSON."""
        raw_json = {
            "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0},
            "tests": []
        }
        # Even if raw had different info, use parameter
        result = runner._convert_from_raw(raw_json, "override_module")

        assert result["module"] == "override_module"


class TestFailuresArrayGeneration:
    """Test failures array generation from raw test data."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_failures_array_empty_when_no_failures(self, runner):
        """Test failures array is empty when all tests pass."""
        raw_json = {
            "summary": {"total": 5, "passed": 5, "failed": 0, "error": 0, "skipped": 0},
            "tests": [
                {"nodeid": f"tests/test.py::test_{i}", "outcome": "passed"}
                for i in range(5)
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert result["failures"] == []

    def test_failures_array_contains_all_failures(self, runner):
        """Test failures array contains all failed tests."""
        raw_json = {
            "summary": {"total": 3, "passed": 0, "failed": 3, "error": 0, "skipped": 0},
            "tests": [
                {"nodeid": f"tests/test.py::test_fail_{i}", "outcome": "failed",
                 "call": {"longrepr": f"failure {i}"}}
                for i in range(3)
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        assert len(result["failures"]) == 3

    def test_failures_array_ignores_skipped(self, runner):
        """Test skipped tests don't appear in failures."""
        raw_json = {
            "summary": {"total": 3, "passed": 1, "failed": 1, "error": 0, "skipped": 1},
            "tests": [
                {"nodeid": "tests/test.py::test_pass", "outcome": "passed"},
                {"nodeid": "tests/test.py::test_fail", "outcome": "failed",
                 "call": {"longrepr": "failed"}},
                {"nodeid": "tests/test.py::test_skip", "outcome": "skipped"}
            ]
        }
        result = runner._convert_from_raw(raw_json, "test_module")

        # Only failed test should be in failures
        assert len(result["failures"]) == 1
        assert result["failures"][0]["name"] == "tests/test.py::test_fail"
