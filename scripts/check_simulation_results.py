#!/usr/bin/env python3
"""
Check simulation results script.

Analyzes simulation test results and provides diagnostics
for debugging and validation.
"""

import json
import sys
from pathlib import Path
from typing import Any, Dict, List


def load_json_file(file_path: str) -> Dict[str, Any]:
    """Load JSON file."""
    with open(file_path, 'r', encoding='utf-8') as f:
        return json.load(f)


def check_simulation_result(result_file: str) -> int:
    """Check simulation result and provide diagnostics."""
    try:
        result = load_json_file(result_file)
    except Exception as e:
        print(f"❌ Error loading result file: {e}")
        return 1

    print(f"📊 Simulation Result Analysis")
    print(f"File: {result_file}")

    # Check if it's a single test result or suite result
    if "test_case" in result:
        return check_single_test_result(result)
    elif "total_tests" in result:
        return check_suite_result(result)
    else:
        print("❌ Unknown result format")
        return 1


def check_single_test_result(result: Dict[str, Any]) -> int:
    """Check single test result."""
    test_case = result.get("test_case", {})
    sim_result = result.get("simulation_result", {})
    assertion_result = result.get("assertion_result", {})
    passed = result.get("passed", False)

    print(f"\n🧪 Test: {test_case.get('test_id', 'unknown')}")
    print(f"📝 Description: {test_case.get('description', 'N/A')}")

    if passed:
        print(f"✅ Status: PASSED")
    else:
        print(f"❌ Status: FAILED")

    # Simulation details
    print(f"\n📈 Simulation Statistics:")
    stats = sim_result.get('statistics', {})
    print(f"   Total Steps: {stats.get('total_steps', 'N/A')}")
    print(f"   Unique Nodes: {stats.get('unique_nodes', 'N/A')}")
    print(f"   Action Count: {stats.get('action_count', 'N/A')}")
    print(f"   Execution Time: {sim_result.get('elapsed_seconds', 'N/A'):.3f}s")

    # Assertion details
    print(f"\n🔍 Assertion Details:")
    print(f"   Success: {assertion_result.get('success', False)}")
    print(f"   Key Events Matched: {assertion_result.get('key_events_matched', 0)}")
    print(f"   Missing Events: {assertion_result.get('missing_events', [])}")
    print(f"   Violations: {assertion_result.get('violations', [])}")
    print(f"   Steps Valid: {assertion_result.get('steps_valid', True)}")

    if not passed:
        print(f"\n🔧 Failure Analysis:")
        if assertion_result.get('missing_events'):
            print(f"   ⚠️  Missing expected events: {assertion_result.get('missing_events')}")
        if assertion_result.get('violations'):
            print(f"   ⚠️  Found violations: {assertion_result.get('violations')}")
        if not assertion_result.get('steps_valid'):
            print(f"   ⚠️  Step count out of expected range")
        if not assertion_result.get('completion_reason_match'):
            print(f"   ⚠️  Completion reason mismatch")

    return 0 if passed else 1


def check_suite_result(result: Dict[str, Any]) -> int:
    """Check test suite result."""
    total_tests = result.get('total_tests', 0)
    passed_tests = result.get('passed_tests', 0)
    failed_tests = result.get('failed_tests', 0)
    success_rate = result.get('success_rate', 0)

    print(f"\n🏛️  Test Suite Summary:")
    print(f"   Total Tests: {total_tests}")
    print(f"   ✅ Passed: {passed_tests}")
    print(f"   ❌ Failed: {failed_tests}")
    print(f"   📊 Success Rate: {success_rate:.1f}%")

    if failed_tests > 0:
        print(f"\n🔍 Failed Tests Details:")
        test_results = result.get('test_results', [])
        for test_result in test_results:
            if "error" in test_result:
                print(f"   ❌ {test_result['test_file']}: {test_result['error']}")
            elif not test_result.get('result', {}).get('passed', False):
                test_file = test_result.get('test_file', 'unknown')
                print(f"   ❌ {test_file}: Test failed")

    return 0 if failed_tests == 0 else 1


def main():
    """Main entry point."""
    if len(sys.argv) < 2:
        print("Usage: check_simulation_results.py <result_file.json>")
        return 1

    result_file = sys.argv[1]

    if not Path(result_file).exists():
        print(f"❌ Result file not found: {result_file}")
        return 1

    return check_simulation_result(result_file)


if __name__ == '__main__':
    sys.exit(main())