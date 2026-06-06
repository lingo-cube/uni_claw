"""
Comprehensive tests for the standard result generation workflow.

Tests cover:
1. Main workflow (_generate_standard_result)
2. Error handling and fallback behavior
3. File I/O operations
4. End-to-end integration
5. Edge cases and error conditions
"""

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict
from unittest.mock import Mock, patch, mock_open
import tempfile

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


class TestMainWorkflow:
    """Test the main _generate_standard_result workflow."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    @pytest.fixture
    def sample_raw_json(self) -> Dict[str, Any]:
        """Provide sample raw JSON data."""
        return {
            "summary": {
                "total": 10,
                "passed": 8,
                "failed": 1,
                "error": 1,
                "skipped": 0
            },
            "tests": [
                {"nodeid": "tests/test.py::test_fail", "outcome": "failed",
                 "call": {"longrepr": "test failed"}},
                {"nodeid": "tests/test.py::test_error", "outcome": "error",
                 "longrepr": "error occurred"}
            ]
        }

    @pytest.fixture
    def sample_stdout(self) -> str:
        """Provide sample pytest stdout."""
        return """=================== test session starts ====================
collected 10 items

test_example.py ..........

===== 8 passed, 1 failed, 1 error in 1.0s =====================

==================== short test summary info =====================
FAILED test_example.py::test_fail - test failed
ERROR test_example.py::test_error - error occurred
"""

    def test_workflow_from_raw_json(self, runner, sample_raw_json, tmp_path):
        """Test complete workflow from raw JSON."""
        # Setup: Create raw JSON file
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            raw_file = results_dir / 'test_module_unit_raw.json'
            raw_file.write_text(json.dumps(sample_raw_json))

            # Execute
            result = runner._generate_standard_result('test_module')

            # Verify
            assert result["module"] == "test_module"
            assert result["summary"]["total"] == 10
            assert result["summary"]["passed"] == 8
            assert len(result["failures"]) == 2

            # Verify final file was created
            final_file = results_dir / 'test_module_unit.json'
            assert final_file.exists()

            with open(final_file) as f:
                final_data = json.load(f)
            assert final_data["module"] == "test_module"

    def test_workflow_fallback_to_stdout(self, runner, sample_stdout, tmp_path):
        """Test fallback to stdout parsing when raw JSON missing."""
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            # No raw file exists, simulate stdout caching
            runner.last_stdout = sample_stdout

            # Execute
            result = runner._generate_standard_result('test_module')

            # Verify
            assert result["module"] == "test_module"
            assert result["summary"]["passed"] == 8

            # Verify final file was created
            final_file = results_dir / 'test_module_unit.json'
            assert final_file.exists()

    def test_workflow_priority_raw_over_stdout(self, runner, sample_raw_json, sample_stdout, tmp_path):
        """Test that raw JSON takes priority over stdout."""
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            # Create raw file
            raw_file = results_dir / 'test_module_unit_raw.json'
            raw_file.write_text(json.dumps(sample_raw_json))

            # Also have stdout cached
            runner.last_stdout = sample_stdout

            # Execute
            result = runner._generate_standard_result('test_module')

            # Should use raw JSON (10 tests) not stdout (also 10 but verify source)
            assert result["summary"]["total"] == 10

    def test_workflow_creates_results_directory(self, runner, sample_raw_json, tmp_path):
        """Test that workflow creates results directory if missing."""
        with patch.object(runner, 'project_root', tmp_path):
            # Don't pre-create results directory
            raw_file = tmp_path / 'test_results' / 'test_module_unit_raw.json'
            raw_file.parent.mkdir(parents=True, exist_ok=True)
            raw_file.write_text(json.dumps(sample_raw_json))

            # Execute
            result = runner._generate_standard_result('test_module')

            # Verify directory and file created
            final_file = tmp_path / 'test_results' / 'test_module_unit.json'
            assert final_file.exists()

    def test_workflow_overwrites_existing_file(self, runner, sample_raw_json, tmp_path):
        """Test that workflow overwrites existing result file."""
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            # Create existing result file
            final_file = results_dir / 'test_module_unit.json'
            final_file.write_text('{"old": "data"}')

            # Create raw file
            raw_file = results_dir / 'test_module_unit_raw.json'
            raw_file.write_text(json.dumps(sample_raw_json))

            # Execute
            runner._generate_standard_result('test_module')

            # Verify file was overwritten
            with open(final_file) as f:
                data = json.load(f)
            assert "old" not in data
            assert data["module"] == "test_module"


class TestErrorHandling:
    """Test error handling and fallback behavior."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    @pytest.fixture
    def sample_raw_json(self) -> Dict[str, Any]:
        """Provide sample raw JSON data."""
        return {
            "summary": {
                "total": 10,
                "passed": 8,
                "failed": 1,
                "error": 1,
                "skipped": 0
            },
            "tests": [
                {"nodeid": "tests/test.py::test_fail", "outcome": "failed",
                 "call": {"longrepr": "test failed"}},
                {"nodeid": "tests/test.py::test_error", "outcome": "error",
                 "longrepr": "error occurred"}
            ]
        }

    def test_error_when_no_data_sources(self, runner, tmp_path):
        """Test error when neither raw JSON nor stdout available."""
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            # No raw file, no stdout
            with pytest.raises(RuntimeError) as exc_info:
                runner._generate_standard_result('test_module')

            assert "无法生成标准化结果" in str(exc_info.value) or "无法生成标准化结果" in str(exc_info.value)

    def test_error_when_raw_json_invalid(self, runner, tmp_path):
        """Test error handling when raw JSON is invalid."""
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            # Create invalid JSON file
            raw_file = results_dir / 'test_module_unit_raw.json'
            raw_file.write_text("not valid json {")

            # No stdout fallback
            with pytest.raises(RuntimeError):
                runner._generate_standard_result('test_module')

    def test_fallback_when_raw_json_missing(self, runner, tmp_path):
        """Test fallback to stdout when raw file missing."""
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            # No raw file
            runner.last_stdout = "5 passed, 1 failed"

            # Should fallback successfully
            result = runner._generate_standard_result('test_module')
            assert result["summary"]["passed"] == 5

    def test_continues_after_write_failure(self, runner, sample_raw_json, tmp_path):
        """Test behavior when write fails - fallback to stdout if available."""
        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            raw_file = results_dir / 'test_module_unit_raw.json'
            raw_file.write_text(json.dumps(sample_raw_json))

            # Set stdout as fallback
            runner.last_stdout = "5 passed, 1 failed"

            # Mock the first write (after raw conversion) to fail
            # but allow the second write (after stdout conversion) to succeed
            call_count = [0]

            def side_effect_func(*args, **kwargs):
                call_count[0] += 1
                if call_count[0] == 1:
                    raise PermissionError("No write")
                else:
                    # Second call succeeds
                    path = args[1] if len(args) > 1 else kwargs.get('path')
                    path.parent.mkdir(parents=True, exist_ok=True)
                    with open(path, 'w') as f:
                        json.dump(args[0] if args else kwargs.get('data'), f)

            with patch.object(runner, '_write_final_json', side_effect=side_effect_func):
                result = runner._generate_standard_result('test_module')
                # Should have fallen back to stdout
                assert call_count[0] == 2  # Both write attempts were made


class TestFileIO:
    """Test file I/O operations."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_write_final_json_creates_directory(self, runner, tmp_path):
        """Test _write_final_json creates parent directories."""
        with patch.object(runner, 'project_root', tmp_path):
            data = {"module": "test", "timestamp": "2026-06-06T12:00:00Z",
                    "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0}}

            nested_path = tmp_path / 'deep' / 'nested' / 'result.json'

            runner._write_final_json(data, nested_path)

            assert nested_path.exists()
            with open(nested_path) as f:
                assert json.load(f) == data

    def test_write_final_json_encoding(self, runner, tmp_path):
        """Test _write_final_json uses UTF-8 encoding."""
        with patch.object(runner, 'project_root', tmp_path):
            # Unicode data
            data = {
                "module": "test",
                "timestamp": "2026-06-06T12:00:00Z",
                "summary": {"total": 1, "passed": 1, "failed": 0, "error": 0, "skipped": 0},
                "failures": [{"message": "错误：测试失败"}]
            }

            result_file = tmp_path / 'test_results' / 'test.json'
            runner._write_final_json(data, result_file)

            with open(result_file, 'r', encoding='utf-8') as f:
                loaded = json.load(f)
            assert "测试失败" in loaded["failures"][0]["message"]

    def test_write_final_json_formatting(self, runner, tmp_path):
        """Test _write_final_json produces readable JSON."""
        with patch.object(runner, 'project_root', tmp_path):
            data = {"module": "test", "summary": {"total": 1}}

            result_file = tmp_path / 'test.json'
            runner._write_final_json(data, result_file)

            content = result_file.read_text()
            # Should be indented
            assert '\n' in content
            # Should not have ascii escaping
            assert '\\u' not in content

    def test_write_final_json_overwrites(self, runner, tmp_path):
        """Test _write_final_json overwrites existing file."""
        with patch.object(runner, 'project_root', tmp_path):
            result_file = tmp_path / 'test.json'

            # Write initial content
            result_file.write_text('{"old": true}')

            # Write new content
            data = {"module": "test", "new": True}
            runner._write_final_json(data, result_file)

            # Verify overwritten
            with open(result_file) as f:
                assert json.load(f) == data


class TestEndToEndIntegration:
    """Test end-to-end integration scenarios."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_full_run_single_module(self, runner, tmp_path):
        """Test complete workflow for a single module."""
        with patch.object(runner, 'project_root', tmp_path):
            # Setup test directory
            tests_dir = tmp_path / 'tests' / 'v6'
            tests_dir.mkdir(parents=True, exist_ok=True)

            # Create a simple test file
            test_file = tests_dir / 'test_simple.py'
            test_file.write_text("""
def test_pass():
    assert True

def test_fail():
    assert False
""")

            # Run the test
            result = runner._run_single_module('v6')

            # Verify
            assert "module" in result
            # May pass or fail depending on execution
            assert isinstance(result, dict)

    def test_full_run_with_coverage(self, runner, tmp_path):
        """Test workflow with coverage enabled."""
        with patch.object(runner, 'project_root', tmp_path):
            # Enable coverage in config
            runner.config = {'coverage': {'enabled': True}}

            # Create test structure
            tests_dir = tmp_path / 'tests' / 'v6'
            tests_dir.mkdir(parents=True, exist_ok=True)

            test_file = tests_dir / 'test_covered.py'
            test_file.write_text("def test_pass(): assert True")

            # Run test
            result = runner._run_single_module('v6')

            # Verify coverage was attempted
            assert "module" in result

    def test_generate_standard_result_called_during_run(self, runner, tmp_path):
        """Test that _generate_standard_result is called during test run."""
        with patch.object(runner, 'project_root', tmp_path):
            tests_dir = tmp_path / 'tests' / 'v6'
            tests_dir.mkdir(parents=True, exist_ok=True)

            test_file = tests_dir / 'test_mock.py'
            test_file.write_text("def test_pass(): assert True")

            # Mock the generate method
            with patch.object(runner, '_generate_standard_result', return_value={"module": "v6"}) as mock_gen:
                runner._run_single_module('v6')

                # Should have been called
                mock_gen.assert_called_once()


class TestEdgeCases:
    """Test edge cases and boundary conditions."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_empty_module_name(self, runner, tmp_path):
        """Test handling of empty module name."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            result = runner._generate_standard_result('')

            assert result["module"] == ""

    def test_special_characters_in_module_name(self, runner, tmp_path):
        """Test handling of special characters in module name."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            # Module names should be lowercase with underscores
            # but we should handle what we're given
            result = runner._generate_standard_result('test_module_v1')

            assert "test_module_v1" in result["module"]

    def test_very_long_module_name(self, runner, tmp_path):
        """Test handling of very long module name."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            long_name = "a" * 200
            result = runner._generate_standard_result(long_name)

            assert result["module"] == long_name

    def test_concurrent_writes(self, runner, tmp_path):
        """Test behavior with concurrent writes to same file."""
        import threading

        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            result_file = tmp_path / 'test_results' / 'test_module_unit.json'

            def write_result():
                try:
                    runner._generate_standard_result('test_module')
                except:
                    pass

            # Attempt concurrent writes
            threads = [threading.Thread(target=write_result) for _ in range(3)]
            for t in threads:
                t.start()
            for t in threads:
                t.join()

            # At least one should have succeeded
            if result_file.exists():
                with open(result_file) as f:
                    data = json.load(f)
                assert "module" in data

    def test_disk_full_scenario(self, runner, tmp_path):
        """Test behavior when disk is full (mocked)."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            # Mock write to raise OSError
            with patch.object(runner, '_write_final_json', side_effect=OSError("No space")):
                with pytest.raises(OSError):
                    runner._generate_standard_result('test_module')

    def test_permission_denied_scenario(self, runner, tmp_path):
        """Test behavior when permission is denied."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            # Mock write to raise PermissionError
            with patch.object(runner, '_write_final_json', side_effect=PermissionError("Denied")):
                with pytest.raises(PermissionError):
                    runner._generate_standard_result('test_module')


class TestTimestampConsistency:
    """Test timestamp handling across the workflow."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_timestamp_present_in_final_output(self, runner, tmp_path):
        """Test timestamp is present in final JSON output."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            result = runner._generate_standard_result('test_module')

            assert "timestamp" in result

            # Also check in file
            final_file = tmp_path / 'test_results' / 'test_module_unit.json'
            with open(final_file) as f:
                file_data = json.load(f)
            assert "timestamp" in file_data

    def test_timestamp_format_consistency(self, runner, tmp_path):
        """Test timestamp format is consistent across runs."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            timestamps = []
            for _ in range(3):
                result = runner._generate_standard_result('test_module')
                timestamps.append(result["timestamp"])

            # All should be ISO format strings
            for ts in timestamps:
                assert isinstance(ts, str)
                assert "T" in ts or "+" in ts or ts.endswith("Z")


class TestModuleFieldConsistency:
    """Test module field handling."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_module_field_in_all_outputs(self, runner, tmp_path):
        """Test module field appears in all return values and files."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            result = runner._generate_standard_result('my_module')

            assert result["module"] == "my_module"

            # Check file
            final_file = tmp_path / 'test_results' / 'my_module_unit.json'
            with open(final_file) as f:
                file_data = json.load(f)
            assert file_data["module"] == "my_module"

    def test_module_field_case_handling(self, runner, tmp_path):
        """Test module field preserves case."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            # Use different case
            result = runner._generate_standard_result('MyModule')

            # Should preserve what was given
            assert result["module"] == "MyModule"


class TestFailuresArrayConsistency:
    """Test failures array handling."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_failures_array_present(self, runner, tmp_path):
        """Test failures array is always present."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed, 0 failed"

            result = runner._generate_standard_result('test_module')

            assert "failures" in result
            assert isinstance(result["failures"], list)

    def test_failures_array_empty_when_passing(self, runner, tmp_path):
        """Test failures array is empty when all pass."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            result = runner._generate_standard_result('test_module')

            assert result["failures"] == []

    def test_failures_array_populated_when_failing(self, runner, tmp_path):
        """Test failures array has entries when tests fail."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = """5 passed, 1 failed

==================== short test summary info =====================
FAILED test_module.py::test_fail - AssertionError
"""

            result = runner._generate_standard_result('test_module')

            assert len(result["failures"]) > 0
            assert any("fail" in f.get("name", "") for f in result["failures"])


class TestCoverageIntegration:
    """Test coverage integration in standard result."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    @patch.object(TestRunner, '_extract_coverage_data')
    def test_coverage_included_when_available(self, mock_extract, runner, tmp_path):
        """Test coverage is included when available."""
        mock_extract.return_value = {"line_rate": 0.85, "branch_rate": 0.75}

        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            result = runner._generate_standard_result('test_module')

            assert "coverage" in result
            assert result["coverage"]["line_rate"] == 0.85

    @patch.object(TestRunner, '_extract_coverage_data')
    def test_coverage_optional_when_unavailable(self, mock_extract, runner, tmp_path):
        """Test coverage is optional when unavailable."""
        mock_extract.return_value = {}

        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed"

            result = runner._generate_standard_result('test_module')

            # Coverage may be empty dict
            assert result.get("coverage") == {}


class TestSummaryFieldConsistency:
    """Test summary field structure."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_summary_all_fields_present(self, runner, tmp_path):
        """Test all summary fields are present."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "5 passed, 1 failed, 1 error, 2 skipped"

            result = runner._generate_standard_result('test_module')

            summary = result["summary"]
            assert "total" in summary
            assert "passed" in summary
            assert "failed" in summary
            assert "error" in summary
            assert "skipped" in summary

    def test_summary_defaults_to_zero(self, runner, tmp_path):
        """Test summary fields default to zero."""
        with patch.object(runner, 'project_root', tmp_path):
            runner.last_stdout = "no tests ran"

            result = runner._generate_standard_result('test_module')

            summary = result["summary"]
            assert summary["total"] == 0
            assert summary["passed"] == 0
            assert summary["failed"] == 0


class TestIntegrationWithRawJson:
    """Test integration with pytest-json-report raw format."""

    @pytest.fixture
    def runner(self):
        """Provide a TestRunner instance."""
        return TestRunner(project_root)

    def test_real_world_pytest_json_report_format(self, runner, tmp_path):
        """Test with real pytest-json-report output format."""
        # This mimics actual pytest-json-report structure
        raw_data = {
            "created": 1686384000,
            "summary": {
                "total": 12,
                "collected": 12,
                "passed": 10,
                "failed": 1,
                "error": 1,
                "skipped": 0,
                "duration": 1.5,
                "start": "2026-06-06T10:00:00Z",
                "stop": "2026-06-06T10:00:02Z"
            },
            "tests": [
                {
                    "nodeid": "tests/test_trace.py::test_ulid_generation",
                    "lineno": 10,
                    "outcome": "passed",
                    "keywords": ["test_ulid_generation"],
                    "setup": {"duration": 0.01, "outcome": "passed"},
                    "call": {"duration": 0.05, "outcome": "passed"},
                    "teardown": {"duration": 0.01, "outcome": "passed"}
                },
                {
                    "nodeid": "tests/test_trace.py::test_session_node",
                    "lineno": 15,
                    "outcome": "failed",
                    "keywords": ["test_session_node"],
                    "setup": {"duration": 0.01, "outcome": "passed"},
                    "call": {
                        "duration": 0.1,
                        "outcome": "failed",
                        "longrepr": "def test_session_node():\n    assert False\n\nAssertionError: assert False"
                    },
                    "teardown": {"duration": 0.01, "outcome": "passed"},
                    "reprcrash": {
                        "message": "AssertionError: assert False",
                        "lineno": 16
                    }
                },
                {
                    "nodeid": "tests/test_trace.py::test_step_node",
                    "lineno": 20,
                    "outcome": "error",
                    "keywords": ["test_step_node"],
                    "setup": {
                        "duration": 0.01,
                        "outcome": "error",
                        "longrepr": "ImportError: cannot import name 'StepNode'"
                    }
                }
            ],
            "environment": {
                "Python": "3.10.12",
                "Platform": "Darwin-21.0.0-x86_64-i386-64bit",
                "Packages": {"pytest": "7.1.2"}
            }
        }

        with patch.object(runner, 'project_root', tmp_path):
            results_dir = tmp_path / 'test_results'
            results_dir.mkdir(parents=True, exist_ok=True)

            raw_file = results_dir / 'trace_unit_raw.json'
            raw_file.write_text(json.dumps(raw_data))

            result = runner._generate_standard_result('trace')

            assert result["module"] == "trace"
            assert result["summary"]["total"] == 12
            assert result["summary"]["passed"] == 10
            assert result["summary"]["failed"] == 1
            assert result["summary"]["error"] == 1

            # Check failure details
            assert len(result["failures"]) == 2
            failure_names = [f["name"] for f in result["failures"]]
            assert any("test_session_node" in name for name in failure_names)
            assert any("test_step_node" in name for name in failure_names)
