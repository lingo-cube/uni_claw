"""
Simulation test runner helper.

Provides simplified test execution with result parsing
and error handling for simulation test framework.
"""

import json
from pathlib import Path
from typing import Any, Dict, List, Optional

from src.simulation.runner import SimulationRunner
from src.graph.plan import TraversalPlan
from .assertions import TraceAsserter, AssertionResult


class SimulationTestRunner:
    """
    Helper class for running simulation tests.

    Simplifies test execution with automatic loading of test cases,
    plan execution, and result validation.
    """

    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """
        Initialize test runner.

        Args:
            config: Optional configuration for test execution
        """
        self.config = config or {}

    def run_simulation_test(
        self,
        test_case_path: str,
    ) -> Dict[str, Any]:
        """
        Run a single simulation test.

        Args:
            test_case_path: Path to test case JSON file

        Returns:
            Dictionary with test results and assertion information

        Raises:
            FileNotFoundError: If test case file not found
            ValueError: If test case is malformed
        """
        # Load test case
        test_case = self._load_test_case(test_case_path)

        # Load fixtures
        plan, virtual_pages = self._load_fixtures(test_case)

        # Create and run simulation
        runner = SimulationRunner(
            virtual_pages=virtual_pages,
            plan=plan,
            config=self.config
        )

        # Execute simulation
        result = runner.run()

        # Assert results
        assertion_result = TraceAsserter.assert_trace_matches_expected(
            result.trace,
            test_case.get("expected", {})
        )

        # Return combined results
        return {
            "test_case": test_case,
            "simulation_result": result,
            "assertion_result": assertion_result,
            "passed": assertion_result.success,
        }

    def run_all_tests(
        self,
        tests_dir: str,
        pattern: str = "test_case.json",
        config: Optional[Dict[str, Any]] = None,
    ) -> Dict[str, Any]:
        """
        Run all tests in a directory.

        Args:
            tests_dir: Directory containing test cases
            pattern: File pattern to match test cases
            config: Optional configuration override

        Returns:
            Dictionary with aggregated test results
        """
        tests_path = Path(tests_dir)
        if not tests_path.exists():
            raise FileNotFoundError(f"Tests directory not found: {tests_dir}")

        # Find all test cases
        test_files = list(tests_path.rglob(pattern))

        results = {
            "total_tests": len(test_files),
            "passed_tests": 0,
            "failed_tests": 0,
            "test_results": []
        }

        # Update config if provided
        if config:
            self.config.update(config)

        # Run each test
        for test_file in test_files:
            try:
                result = self.run_simulation_test(str(test_file))
                results["test_results"].append({
                    "test_file": str(test_file),
                    "result": result
                })

                if result["passed"]:
                    results["passed_tests"] += 1
                else:
                    results["failed_tests"] += 1

            except Exception as e:
                results["test_results"].append({
                    "test_file": str(test_file),
                    "error": str(e)
                })
                results["failed_tests"] += 1

        # Add summary statistics
        results["success_rate"] = (
            results["passed_tests"] / results["total_tests"] * 100
            if results["total_tests"] > 0 else 0
        )

        return results

    def _load_test_case(self, test_case_path: str) -> Dict[str, Any]:
        """
        Load test case from JSON file.

        Args:
            test_case_path: Path to test case file

        Returns:
            Test case dictionary

        Raises:
            FileNotFoundError: If file not found
            ValueError: If JSON is invalid
        """
        test_path = Path(test_case_path)
        if not test_path.exists():
            raise FileNotFoundError(f"Test case not found: {test_case_path}")

        with open(test_path, 'r', encoding='utf-8') as f:
            try:
                return json.load(f)
            except json.JSONDecodeError as e:
                raise ValueError(f"Invalid JSON in test case: {e}")

    def _load_fixtures(
        self,
        test_case: Dict[str, Any]
    ) -> tuple[TraversalPlan, Dict[str, Dict[str, Any]]]:
        """
        Load plan and virtual pages from test case fixtures.

        Args:
            test_case: Test case dictionary

        Returns:
            Tuple of (TraversalPlan, virtual_pages)

        Raises:
            FileNotFoundError: If fixture files not found
            ValueError: If fixtures are invalid
        """
        test_dir = Path(test_case.get("test_dir", ""))
        fixtures = test_case.get("fixtures", {})

        # Load plan
        plan_file = fixtures.get("plan_file", "plan.json")
        plan_path = test_dir / plan_file

        if not plan_path.exists():
            raise FileNotFoundError(f"Plan file not found: {plan_path}")

        with open(plan_path, 'r', encoding='utf-8') as f:
            try:
                plan_data = json.load(f)
                # Convert dict to JSON string for from_json method
                plan_json_str = json.dumps(plan_data)
                plan = TraversalPlan.from_json(plan_json_str)
            except Exception as e:
                raise ValueError(f"Invalid plan file: {e}")

        # Load virtual pages
        pages_file = fixtures.get("pages_file", "pages.json")
        pages_path = test_dir / pages_file

        if not pages_path.exists():
            raise FileNotFoundError(f"Pages file not found: {pages_path}")

        with open(pages_path, 'r', encoding='utf-8') as f:
            try:
                virtual_pages = json.load(f)
            except Exception as e:
                raise ValueError(f"Invalid pages file: {e}")

        return plan, virtual_pages

    def validate_test_case(self, test_case: Dict[str, Any]) -> List[str]:
        """
        Validate test case structure.

        Args:
            test_case: Test case dictionary

        Returns:
            List of validation errors (empty if valid)
        """
        errors = []

        # Required fields
        required_fields = ["test_id", "description", "intent_slots", "expected"]
        for field in required_fields:
            if field not in test_case:
                errors.append(f"Missing required field: {field}")

        # Validate expected section
        if "expected" in test_case:
            expected = test_case["expected"]
            if "key_events" not in expected and "completion_reason" not in expected:
                errors.append("Expected section should have key_events or completion_reason")

        # Validate fixtures if present
        if "fixtures" in test_case:
            fixtures = test_case["fixtures"]
            if "plan_file" not in fixtures or "pages_file" not in fixtures:
                errors.append("Fixtures should have both plan_file and pages_file")

        return errors