"""
Pytest integration for simulation testing framework.

Provides pytest fixtures, test discovery, and parameterized
tests for simulation-based testing.
"""

import pytest
import json
from pathlib import Path
from typing import Dict, Any, List

from tests.simulation.helpers import SimulationTestRunner, TraceAsserter


# Pytest fixtures
@pytest.fixture
def simulation_runner():
    """Provide SimulationTestRunner instance."""
    return SimulationTestRunner()


@pytest.fixture
def sample_config():
    """Provide sample configuration for testing."""
    return {
        "action_delay": 0.0,
        "verbose": False
    }


@pytest.fixture
def test_fixtures_dir():
    """Provide path to test fixtures directory."""
    return Path(__file__).parent / "fixtures"


# Pytest test discovery and execution
class TestSimulationFramework:
    """Test suite for simulation framework using pytest."""

    def test_template_fixture_valid(self, simulation_runner, test_fixtures_dir):
        """Test that template fixture is valid."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if template_path.exists():
            result = simulation_runner.run_simulation_test(str(template_path))
            assert result is not None
            assert "test_case" in result

    @pytest.mark.parametrize("fixture_name", [
        "e2e_all_traversal",
        "e2e_target_found",
        "e2e_static_path",
        "e2e_popup_handling",
        "e2e_auto_escape"
    ])
    def test_core_fixtures_loadable(self, simulation_runner, test_fixtures_dir, fixture_name):
        """Test that all core fixtures can be loaded."""
        fixture_path = test_fixtures_dir / fixture_name / "test_case.json"
        if fixture_path.exists():
            try:
                test_case = simulation_runner._load_test_case(str(fixture_path))
                assert test_case is not None
                assert "test_id" in test_case
                assert "fixtures" in test_case
                assert "expected" in test_case
            except Exception as e:
                pytest.fail(f"Failed to load fixture {fixture_name}: {e}")

    @pytest.mark.parametrize("fixture_name", [
        "e2e_all_traversal",
        "e2e_target_found"
    ])
    def test_core_fixtures_validated(self, simulation_runner, test_fixtures_dir, fixture_name):
        """Test that core fixtures pass validation."""
        fixture_path = test_fixtures_dir / fixture_name / "test_case.json"
        if fixture_path.exists():
            test_case = simulation_runner._load_test_case(str(fixture_path))
            errors = simulation_runner.validate_test_case(test_case)
            assert len(errors) == 0, f"Validation errors for {fixture_name}: {errors}"

    def test_test_case_format_compliant(self, simulation_runner, test_fixtures_dir):
        """Test that test cases follow AI-friendly format."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if template_path.exists():
            test_case = simulation_runner._load_test_case(str(template_path))

            # Check required fields
            required_fields = ["test_id", "description", "intent_slots", "expected"]
            for field in required_fields:
                assert field in test_case, f"Missing required field: {field}"

            # Check intent_slots structure
            assert "target_app" in test_case["intent_slots"]
            assert "scope" in test_case["intent_slots"]

            # Check expected structure
            assert "key_events" in test_case["expected"] or "completion_reason" in test_case["expected"]

    def test_traceserter_functionality(self):
        """Test TraceAsserter basic functionality."""
        sample_trace = [
            {
                "action_type": "enter",
                "current_node": "root",
                "target_info": {},
                "timestamp": 1.0
            },
            {
                "action_type": "click",
                "current_node": "root",
                "target_info": {"element_id": "SettingsButton"},
                "timestamp": 1.1
 }
        ]

        expected = {
            "key_events": ["进入 root", "点击 SettingsButton"],
            "total_steps_min": 1,
            "total_steps_max": 10
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)
        assert result.success is True

    def test_simulation_runner_discovery(self, test_fixtures_dir):
        """Test that simulation runner can discover all test cases."""
        runner = SimulationTestRunner()
        fixtures_dir = str(test_fixtures_dir)

        # Find all test cases
        test_files = list(Path(fixtures_dir).rglob("test_case.json"))
        assert len(test_files) > 0, "No test cases found"

        # Try to load each one
        for test_file in test_files:
            try:
                test_case = runner._load_test_case(str(test_file))
                assert test_case is not None
                assert "test_id" in test_case
            except Exception as e:
                pytest.fail(f"Failed to load {test_file}: {e}")

    def test_end_to_end_template_execution(self, simulation_runner, test_fixtures_dir, sample_config):
        """Test complete end-to-end execution with template fixture."""
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if not template_path.exists():
            pytest.skip("Template fixture not found")

        result = simulation_runner.run_simulation_test(str(template_path))
        assert result is not None
        assert "simulation_result" in result
        assert "assertion_result" in result

    @pytest.mark.integration
    def test_simulation_runner_suite_execution(self, simulation_runner, test_fixtures_dir):
        """Test suite execution with multiple fixtures."""
        fixtures_path = test_fixtures_dir

        if not fixtures_path.exists():
            pytest.skip("Fixtures directory not found")

        # Run all tests in the fixtures directory
        results = simulation_runner.run_all_tests(
            str(fixtures_path),
            pattern="test_case.json"
        )

        assert results is not None
        assert "total_tests" in results
        assert "passed_tests" in results
        assert "failed_tests" in results
        assert results["total_tests"] >= 0

    def test_assertion_result_structure(self):
        """Test that AssertionResult has correct structure."""
        sample_trace = [
            {
                "action_type": "enter",
                "current_node": "test",
                "target_info": {},
                "timestamp": 1.0,
                "completion_reason": "completed"
            }
        ]

        expected = {
            "key_events": ["进入 test"],
            "completion_reason": "completed",
            "total_steps_min": 1,
            "total_steps_max": 5
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)

        # Check AssertionResult structure
        assert hasattr(result, 'success')
        assert hasattr(result, 'key_events_matched')
        assert hasattr(result, 'missing_events')
        assert hasattr(result, 'violations')
        assert hasattr(result, 'steps_valid')
        assert hasattr(result, 'completion_reason_match')
        assert hasattr(result, 'details')


# Pytest configuration and markers
def pytest_configure(config):
    """Configure pytest with custom markers."""
    config.addinivalue_line(
        "markers", "integration: marks tests as integration tests (deselect with '-m \"not integration\"')"
    )
    config.addinivalue_line(
        "markers", "simulation: marks tests as simulation tests"
    )
    config.addinivalue_line(
        "markers", "slow: marks tests as slow running"
    )


# Test discovery helpers
def discover_test_cases(fixtures_dir: Path) -> List[Dict[str, Any]]:
    """Discover all test cases in fixtures directory."""
    test_files = list(fixtures_dir.rglob("test_case.json"))
    test_cases = []

    for test_file in test_files:
        try:
            with open(test_file, 'r', encoding='utf-8') as f:
                test_case = json.load(f)
                test_cases.append(test_case)
        except Exception:
            continue

    return test_cases


def validate_test_case_schema(test_case: Dict[str, Any]) -> List[str]:
    """Validate test case against schema."""
    errors = []

    required_fields = ["test_id", "description", "intent_slots", "expected"]
    for field in required_fields:
        if field not in test_case:
            errors.append(f"Missing required field: {field}")

    if "expected" in test_case:
        expected = test_case["expected"]
        if not any(key in expected for key in ["key_events", "completion_reason"]):
            errors.append("Expected section should have key_events or completion_reason")

    return errors


# Parameterized test generator
@pytest.mark.parametrize("test_case_data", discover_test_cases(Path(__file__).parent / "fixtures"))
def test_all_fixtures_parametrized(test_case_data):
    """Parameterized test for all fixtures."""
    errors = validate_test_case_schema(test_case_data)
    assert len(errors) == 0, f"Test case validation failed: {errors}"