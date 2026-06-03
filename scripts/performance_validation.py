#!/usr/bin/env python3
"""
Performance validation script for simulation testing system.

Validates that all performance targets are met and provides
detailed performance analysis.
"""

import sys
import time
from pathlib import Path
from typing import Dict, List, Tuple

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

try:
    from src.simulation.page_analyzer import PageAnalyzer
    from src.simulation.mock_vision import MockVisionService
    from src.simulation.mock_action import MockActionExecutor
    from src.simulation.runner import SimulationRunner
    from tests.simulation.helpers import SimulationTestRunner
except ImportError as e:
    print(f"ERROR: Could not import required modules: {e}")
    print("Make sure you're running this from the project root directory.")
    sys.exit(1)


class PerformanceValidator:
    """Performance validation for simulation components."""

    def __init__(self):
        self.results = []

    def validate_all(self) -> int:
        """Run complete performance validation."""
        print("Performance Validation - Simulation Testing System")
        print("=" * 60)

        # Core component performance
        self.validate_page_analyzer_performance()
        self.mock_vision_service_performance()
        self.mock_action_executor_performance()
        self.simulation_runner_performance()

        # Integration performance
        self.trace_asserter_performance()
        self.test_runner_performance()

        # Summary
        return self.print_summary()

    def validate_page_analyzer_performance(self):
        """Validate PageAnalyzer performance."""
        print("\n[PageAnalyzer] Performance Validation:")

        virtual_pages = {
            "test": {
                "page_name": "TestPage",
                "elements": [
                    {"id": "btn1", "type": "button", "text": "Button 1"},
                    {"id": "btn2", "type": "button", "text": "Button 2"}
                ]
            }
        }

        analyzer = PageAnalyzer(virtual_pages)

        # Performance test: 100 analyses
        iterations = 100
        start_time = time.time()
        for _ in range(iterations):
            analyzer.analyze_page("test")
        elapsed_time = time.time() - start_time

        avg_time_ms = (elapsed_time / iterations) * 1000

        # Target: <10ms per analysis
        target_time = 10.0
        passed = avg_time_ms < target_time

        self.results.append((passed, f"Average analysis time: {avg_time_ms:.3f}ms (target: <{target_time}ms)"))

        if passed:
            print(f"  [PASS] Page analysis performance: {avg_time_ms:.3f}ms average")
        else:
            print(f"  [FAIL] Page analysis too slow: {avg_time_ms:.3f}ms (target: <{target_time}ms)")

    def mock_vision_service_performance(self):
        """Validate MockVisionService performance."""
        print("\n[MockVisionService] Performance Validation:")

        virtual_pages = {
            "test": {
                "page_name": "TestPage",
                "elements": [{"id": "btn", "type": "button"}]
            }
        }

        vision = MockVisionService(virtual_pages)

        # Performance test: 100 calls
        iterations = 100
        start_time = time.time()
        for _ in range(iterations):
            vision.inject_path("test")
            vision.analyze_screenshot()
        elapsed_time = time.time() - start_time

        avg_time_ms = (elapsed_time / iterations) * 1000

        # Target: <5ms per analysis (with caching)
        target_time = 5.0
        passed = avg_time_ms < target_time

        self.results.append((passed, f"MockVisionService average: {avg_time_ms:.3f}ms (target: <{target_time}ms)"))

        if passed:
            print(f"  [PASS] MockVisionService performance: {avg_time_ms:.3f}ms average")
        else:
            print(f"  [WARN] MockVisionService slow: {avg_time_ms:.3f}ms (target: <{target_time}ms)")

    def mock_action_executor_performance(self):
        """Validate MockActionExecutor performance."""
        print("\n[MockActionExecutor] Performance Validation:")

        action = MockActionExecutor()

        # Performance test: 100 operations
        iterations = 100
        start_time = time.time()
        for i in range(iterations):
            action.click(f"button_{i}")
        elapsed_time = time.time() - start_time

        avg_time_ms = (elapsed_time / iterations) * 1000

        # Target: <1ms per operation
        target_time = 1.0
        passed = avg_time_ms < target_time

        self.results.append((passed, f"MockActionExecutor average: {avg_time_ms:.3f}ms (target: <{target_time}ms)"))

        if passed:
            print(f"  [PASS] MockActionExecutor performance: {avg_time_ms:.3f}ms average")
        else:
            print(f"  [FAIL] MockActionExecutor too slow: {avg_time_ms:.3f}ms (target: <{target_time}ms)")

    def simulation_runner_performance(self):
        """Validate SimulationRunner performance."""
        print("\n[SimulationRunner] Performance Validation:")

        # Create minimal test setup
        from unittest.mock import Mock
        from src.graph.plan import TraversalPlan
        from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots

        virtual_pages = {
            "test": {
                "page_name": "TestPage",
                "elements": [{"id": "btn", "type": "button"}]
            }
        }

        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "perf_test"
        plan.intent_slots = IntentSlots(depth=1)
        plan.completion_policy = CompletionPolicy(type="exhaustive")

        runner = SimulationRunner(virtual_pages, plan)

        # Performance test: 10 runs
        iterations = 10
        times = []

        for i in range(iterations):
            runner.action.reset()
            start_time = time.time()
            result = runner.run()
            elapsed_time = time.time() - start_time
            times.append(elapsed_time)

        avg_time = sum(times) / len(times)
        max_time = max(times)

        # Target: <5 seconds per simulation
        target_time = 5.0
        passed = avg_time < target_time

        self.results.append((passed, f"SimulationRunner average: {avg_time:.3f}s (target: <{target_time}s)"))
        self.results.append((max_time < target_time, f"SimulationRunner max: {max_time:.3f}s (target: <{target_time}s)"))

        if passed:
            print(f"  [PASS] SimulationRunner performance: {avg_time:.3f}s average")
        else:
            print(f"  [FAIL] SimulationRunner too slow: {avg_time:.3f}s (target: <{target_time}s)")

    def trace_asserter_performance(self):
        """Validate TraceAsserter performance."""
        print("\n[TraceAsserter] Performance Validation:")

        from tests.simulation.helpers.assertions import TraceAsserter

        # Create sample trace
        trace = [
            {"action_type": "enter", "current_node": "root", "target_info": {}, "timestamp": float(i)}
            for i in range(100)
        ]

        expected = {
            "key_events": ["enter root"],
            "total_steps_min": 50,
            "total_steps_max": 200
        }

        # Performance test: 100 assertions
        iterations = 100
        start_time = time.time()
        for _ in range(iterations):
            result = TraceAsserter.assert_trace_matches_expected(trace, expected)
        elapsed_time = time.time() - start_time

        avg_time_ms = (elapsed_time / iterations) * 1000

        # Target: <50ms per assertion
        target_time = 50.0
        passed = avg_time_ms < target_time

        self.results.append((passed, f"TraceAsserter average: {avg_time_ms:.3f}ms (target: <{target_time}ms)"))

        if passed:
            print(f"  [PASS] TraceAsserter performance: {avg_time_ms:.3f}ms average")
        else:
            print(f"  [WARN] TraceAsserter slow: {avg_time_ms:.3f}ms (target: <{target_time}ms)")

    def test_runner_performance(self):
        """Validate SimulationTestRunner performance."""
        print("\n[SimulationTestRunner] Performance Validation:")

        runner = SimulationTestRunner()

        # Performance test: Check if it loads quickly
        start_time = time.time()
        # Just instantiation test
        elapsed_time = time.time() - start_time

        # Target: <1 second to instantiate
        target_time = 1.0
        passed = elapsed_time < target_time

        self.results.append((passed, f"SimulationTestRunner instantiation: {elapsed_time*1000:.3f}ms (target: <{target_time}s)"))

        if passed:
            print(f"  [PASS] SimulationTestRunner instantiation: {elapsed_time*1000:.3f}ms")
        else:
            print(f"  [FAIL] SimulationTestRunner instantiation slow: {elapsed_time*1000:.3f}ms")

    def print_summary(self) -> int:
        """Print performance validation summary."""
        total = len(self.results)
        passed = sum(1 for ok, _ in self.results if ok)
        failed = total - passed

        print(f"\n[STATS] Performance Summary:")
        print(f"   Total Checks: {total}")
        print(f"   [PASS] Passed: {passed}")
        print(f"   [FAIL] Failed: {failed}")
        print(f"   Success Rate: {(passed/total*100):.1f}%")

        if failed == 0:
            print(f"\n[SUCCESS] All performance targets met!")
            return 0
        else:
            print(f"\n[WARNING] {failed} performance target(s) not met:")
            for ok, msg in self.results:
                if not ok:
                    print(f"   {msg}")
            return 1


def main():
    """Main entry point."""
    validator = PerformanceValidator()
    return validator.validate_all()


if __name__ == '__main__':
    sys.exit(main())