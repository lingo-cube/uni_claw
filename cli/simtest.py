#!/usr/bin/env python3
"""
simtest CLI tool for simulation testing.

Provides command-line interface for running simulation tests,
generating reports, and managing test execution.
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Optional

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from tests.simulation.helpers import SimulationTestRunner


def run_test(test_path: str, output: Optional[str] = None, format: str = "json") -> int:
    """
    Run a single simulation test.

    Args:
        test_path: Path to test case directory or file
        output: Optional output file for results
        format: Output format (json, text)

    Returns:
        Exit code (0 for success, 1 for failure)
    """
    try:
        runner = SimulationTestRunner()

        # Handle both directory and file paths
        test_path_obj = Path(test_path)
        if test_path_obj.is_file():
            result = runner.run_simulation_test(str(test_path_obj))
        else:
            # Assume it's a directory with test_case.json
            test_file = test_path_obj / "test_case.json"
            result = runner.run_simulation_test(str(test_file))

        # Output results
        if format == "json":
            output_data = {
                "passed": result["passed"],
                "test_case": result["test_case"],
                "assertion_details": {
                    "success": result["assertion_result"].success,
                    "key_events_matched": result["assertion_result"].key_events_matched,
                    "missing_events": result["assertion_result"].missing_events,
                    "violations": result["assertion_result"].violations,
                }
            }
            output_json = json.dumps(output_data, indent=2)
            if output:
                with open(output, 'w') as f:
                    f.write(output_json)
                print(f"Results saved to {output}")
            else:
                print(output_json)
        else:
            # Text format
            print(f"Test: {result['test_case']['test_id']}")
            print(f"Description: {result['test_case']['description']}")
            print(f"Status: {'PASS' if result['passed'] else 'FAIL'}")
            if not result['passed']:
                print(f"Missing events: {result['assertion_result'].missing_events}")
                print(f"Violations: {result['assertion_result'].violations}")

        return 0 if result["passed"] else 1

    except Exception as e:
        print(f"Error running test: {e}", file=sys.stderr)
        return 1


def run_suite(tests_dir: str, report: Optional[str] = None, pattern: str = "test_case.json") -> int:
    """
    Run all tests in a directory.

    Args:
        tests_dir: Directory containing test cases
        report: Optional report file for aggregated results
        pattern: File pattern to match test cases

    Returns:
        Exit code (0 for all success, 1 for any failure)
    """
    try:
        runner = SimulationTestRunner()
        results = runner.run_all_tests(tests_dir, pattern)

        # Generate report
        if report:
            with open(report, 'w') as f:
                json.dump(results, f, indent=2)
            print(f"Report saved to {report}")

        # Print summary
        print(f"\nTest Suite Results:")
        print(f"Total Tests: {results['total_tests']}")
        print(f"Passed: {results['passed_tests']}")
        print(f"Failed: {results['failed_tests']}")
        print(f"Success Rate: {results['success_rate']:.1f}%")

        return 0 if results['failed_tests'] == 0 else 1

    except Exception as e:
        print(f"Error running test suite: {e}", file=sys.stderr)
        return 1


def show_report(report_path: str) -> int:
    """
    Display test report.

    Args:
        report_path: Path to report file

    Returns:
        Exit code (0 for success, 1 for failure)
    """
    try:
        with open(report_path, 'r') as f:
            report = json.load(f)

        # Display report based on type
        if "total_tests" in report:
            # Test suite report
            print(f"\nTest Suite Report:")
            print(f"Total Tests: {report['total_tests']}")
            print(f"Passed: {report['passed_tests']}")
            print(f"Failed: {report['failed_tests']}")
            print(f"Success Rate: {report['success_rate']:.1f}%")
        elif "test_case" in report:
            # Single test report
            print(f"\nTest Report:")
            print(f"Test: {report['test_case']['test_id']}")
            print(f"Status: {'PASS' if report['passed'] else 'FAIL'}")
            if not report['passed']:
                print(f"Missing events: {report['assertion_details']['missing_events']}")
                print(f"Violations: {report['assertion_details']['violations']}")
        else:
            print("Unknown report format")
            return 1

        return 0

    except Exception as e:
        print(f"Error reading report: {e}", file=sys.stderr)
        return 1


def main():
    """Main CLI entry point."""
    parser = argparse.ArgumentParser(
        description="simtest - Simulation Testing CLI Tool",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  simtest run tests/simulation/fixtures/e2e_all_traversal
  simtest suite tests/simulation/fixtures --report results.json
  simtest show results.json
        """
    )

    subparsers = parser.add_subparsers(dest='command', help='Available commands')

    # Run command
    run_parser = subparsers.add_parser('run', help='Run a single simulation test')
    run_parser.add_argument('test_path', help='Path to test case')
    run_parser.add_argument('--output', '-o', help='Output file for results')
    run_parser.add_argument('--format', '-f', choices=['json', 'text'], default='json',
                          help='Output format')

    # Suite command
    suite_parser = subparsers.add_parser('suite', help='Run test suite')
    suite_parser.add_argument('tests_dir', help='Directory containing test cases')
    suite_parser.add_argument('--report', '-r', help='Report file for aggregated results')
    suite_parser.add_argument('--pattern', '-p', default='test_case.json',
                             help='File pattern to match (default: test_case.json)')

    # Show command
    show_parser = subparsers.add_parser('show', help='Display test report')
    show_parser.add_argument('report_path', help='Path to report file')

    # Global options
    parser.add_argument('--verbose', '-v', action='store_true', help='Verbose output')
    parser.add_argument('--quiet', '-q', action='store_true', help='Quiet output')

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return 1

    # Execute command
    if args.command == 'run':
        return run_test(args.test_path, args.output, args.format)
    elif args.command == 'suite':
        return run_suite(args.tests_dir, args.report, args.pattern)
    elif args.command == 'show':
        return show_report(args.report_path)
    else:
        parser.print_help()
        return 1


if __name__ == '__main__':
    sys.exit(main())