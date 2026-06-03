"""
Integration tests for SimulationTestRunner.

Tests complete integration with test fixtures, error handling,
and suite execution functionality.
"""

import pytest
import json
from pathlib import Path
from unittest.mock import Mock, patch
from tests.simulation.helpers.test_runner import SimulationTestRunner


class TestSimulationTestRunner:
    """Test suite for SimulationTestRunner integration."""

    @pytest.fixture
    def runner(self):
        """Create SimulationTestRunner instance."""
        return SimulationTestRunner()

    @pytest.fixture
    def test_fixtures_dir(self):
        """Provide path to test fixtures directory."""
        return Path(__file__).parent.parent / "fixtures"

    def test_runner_initialization(self, runner):
        """Test runner initialization."""
        assert runner is not None
        assert runner.config == {}

    def test_runner_initialization_with_config(self):
        """Test runner initialization with configuration."""
        config = {"action_delay": 0.1, "verbose": True}
        runner = SimulationTestRunner(config)
        assert runner.config == config

    def test_load_valid_test_case(self, runner, test_fixtures_dir):
        """Test loading a valid test case."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if template_path.exists():
            test_case = runner._load_test_case(str(template_path))
            assert test_case is not None
            assert "test_id" in test_case
            assert "description" in test_case
            assert "intent_slots" in test_case
            assert "expected" in test_case

    def test_load_invalid_test_case_path(self, runner):
        """Test loading test case with invalid path."""
        with pytest.raises(FileNotFoundError):
            runner._load_test_case("nonexistent/path/test_case.json")

    def test_load_malformed_test_case(self, runner, tmp_path):
        """Test loading malformed test case JSON."""
        malformed_file = tmp_path / "bad_test_case.json"
        malformed_file.write_text("{ invalid json }")

        with pytest.raises(ValueError, match="Invalid JSON"):
            runner._load_test_case(str(malformed_file))

    def test_load_fixtures_success(self, runner, test_fixtures_dir):
        """Test successful fixture loading."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if template_path.exists():
            test_case = runner._load_test_case(str(template_path))
            plan, pages = runner._load_fixtures(test_case)

            assert plan is not None
            assert pages is not None
            assert isinstance(pages, dict)

    def test_load_fixtures_missing_plan(self, runner, tmp_path):
        """Test fixture loading with missing plan file."""
        # Create test case with non-existent plan
        test_case = {
            "test_id": "test",
            "description": "Test",
            "test_dir": str(tmp_path),
            "fixtures": {
                "plan_file": "nonexistent_plan.json",
                "pages_file": "pages.json"
            }
        }

        with pytest.raises(FileNotFoundError):
            runner._load_fixtures(test_case)

    def test_load_fixtures_missing_pages(self, runner, tmp_path):
        """Test fixture loading with missing pages file."""
        # Create test case with non-existent pages
        test_case = {
            "test_id": "test",
            "description": "Test",
            "test_dir": str(tmp_path),
            "fixtures": {
                "plan_file": "plan.json",
                "pages_file": "nonexistent_pages.json"
            }
        }

        with pytest.raises(FileNotFoundError):
            runner._load_fixtures(test_case)

    def test_validate_test_case_valid(self, runner):
        """Test validation of valid test case."""
        valid_test_case = {
            "test_id": "test_123",
            "description": "Test description",
            "intent_slots": {
                "target_app": "TestApp",
                "scope": "all"
            },
            "expected": {
                "key_events": ["event1", "event2"],
                "completion_reason": "completed"
            },
            "fixtures": {
                "plan_file": "plan.json",
                "pages_file": "pages.json"
            }
        }

        errors = runner.validate_test_case(valid_test_case)
        assert len(errors) == 0

    def test_validate_test_case_missing_fields(self, runner):
        """Test validation of test case with missing fields."""
        invalid_test_case = {
            "test_id": "test_123"
        }

        errors = runner.validate_test_case(invalid_test_case)
        assert len(errors) > 0
        assert any("description" in error for error in errors)
        assert any("intent_slots" in error for error in errors)

    def test_validate_test_case_invalid_expected(self, runner):
        """Test validation of test case with invalid expected section."""
        invalid_test_case = {
            "test_id": "test_123",
            "description": "Test",
            "intent_slots": {"target_app": "Test"},
            "expected": {},
            "fixtures": {
                "plan_file": "plan.json",
                "pages_file": "pages.json"
            }
        }

        errors = runner.validate_test_case(invalid_test_case)
        assert len(errors) > 0
        assert any("Expected section" in error for error in errors)

    def test_validate_test_case_invalid_fixtures(self, runner):
        """Test validation of test case with invalid fixtures."""
        invalid_test_case = {
            "test_id": "test_123",
            "description": "Test",
            "intent_slots": {"target_app": "Test"},
            "expected": {"key_events": ["event1"]},
            "fixtures": {
                "plan_file": "plan.json"
            }
        }

        errors = runner.validate_test_case(invalid_test_case)
        assert len(errors) > 0
        assert any("Fixtures" in error for error in errors)

    @pytest.mark.integration
    def test_run_simulation_test_template(self, runner, test_fixtures_dir):
        """Test running simulation test with template fixture."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if not template_path.exists():
            pytest.skip("Template fixture not found")

        result = runner.run_simulation_test(str(template_path))

        assert result is not None
        assert "test_case" in result
        assert "simulation_result" in result
        assert "assertion_result" in result
        assert "passed" in result

    @pytest.mark.integration
    def test_run_simulation_test_structure(self, runner, test_fixtures_dir):
        """Test that simulation test result has correct structure."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if not template_path.exists():
            pytest.skip("Template fixture not found")

        result = runner.run_simulation_test(str(template_path))

        # Check simulation result structure
        sim_result = result["simulation_result"]
        assert hasattr(sim_result, 'trace')
        assert hasattr(sim_result, 'executed_actions')
        assert hasattr(sim_result, 'visited_tree')
        assert hasattr(sim_result, 'elapsed_seconds')

        # Check assertion result structure
        assert_result = result["assertion_result"]
        assert hasattr(assert_result, 'success')
        assert hasattr(assert_result, 'key_events_matched')
        assert hasattr(assert_result, 'missing_events')

    @pytest.mark.integration
    def test_run_all_tests_empty_directory(self, runner, tmp_path):
        """Test running all tests with empty directory."""
        results = runner.run_all_tests(str(tmp_path))

        assert results is not None
        assert results["total_tests"] == 0
        assert results["passed_tests"] == 0
        assert results["failed_tests"] == 0

    @pytest.mark.integration
    def test_run_all_tests_with_fixtures(self, runner, test_fixtures_dir):
        """Test running all tests with fixtures directory."""
        if not test_fixtures_dir.exists():
            pytest.skip("Fixtures directory not found")

        results = runner.run_all_tests(str(test_fixtures_dir))

        assert results is not None
        assert "total_tests" in results
        assert "passed_tests" in results
        assert "failed_tests" in results
        assert "success_rate" in results
        assert "test_results" in results

    @pytest.mark.integration
    def test_run_all_tests_error_handling(self, runner, tmp_path):
        """Test error handling when running test suite."""
        # Create a malformed test case
        bad_test_dir = tmp_path / "bad_tests"
        bad_test_dir.mkdir()
        bad_test_file = bad_test_dir / "test_case.json"
        bad_test_file.write_text("{ invalid json }")

        results = runner.run_all_tests(str(bad_test_dir))

        # Should handle error gracefully
        assert results is not None
        assert results["failed_tests"] >= 1

    def test_config_override_in_run_all_tests(self, runner, test_fixtures_dir):
        """Test configuration override in run_all_tests."""
        if not test_fixtures_dir.exists():
            pytest.skip("Fixtures directory not found")

        config = {"action_delay": 0.05, "verbose": True}
        results = runner.run_all_tests(str(test_fixtures_dir), config=config)

        # Config should be updated
        assert runner.config == config

    def test_pattern_filtering_in_run_all_tests(self, runner, test_fixtures_dir):
        """Test pattern filtering in run_all_tests."""
        if not test_fixtures_dir.exists():
            pytest.skip("Fixtures directory not found")

        # Use specific pattern
        results = runner.run_all_tests(str(test_fixtures_dir), pattern="test_case.json")

        assert results is not None
        # Should only find files matching the pattern

    @pytest.mark.slow
    def test_end_to_end_template_execution(self, runner, test_fixtures_dir):
        """Test complete end-to-end execution with template."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if not template_path.exists():
            pytest.skip("Template fixture not found")

        # Load and validate test case
        test_case = runner._load_test_case(str(template_path))
        errors = runner.validate_test_case(test_case)
        assert len(errors) == 0, f"Test case validation failed: {errors}"

        # Load fixtures
        plan, pages = runner._load_fixtures(test_case)
        assert plan is not None
        assert pages is not None

        # Run simulation test
        result = runner.run_simulation_test(str(template_path))
        assert result["simulation_result"] is not None

    def test_runner_error_handling_invalid_directory(self, runner):
        """Test error handling with invalid directory."""
        results = runner.run_all_tests("/nonexistent/directory")
        assert results is not None
        # Should handle gracefully - likely with FileNotFoundError caught

    def test_config_persistence_across_runs(self, runner, test_fixtures_dir):
        """Test that config persists across multiple runs."""
        if not test_fixtures_dir.exists():
            pytest.skip("Fixtures directory not found")

        config = {"test_mode": True}
        runner.config = config

        # First run
        runner.run_all_tests(str(test_fixtures_dir))

        # Config should persist
        assert runner.config == config

        # Second run with override
        new_config = {"test_mode": False}
        runner.run_all_tests(str(test_fixtures_dir), config=new_config)
        assert runner.config == new_config