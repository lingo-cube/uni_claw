#!/usr/bin/env python3
"""Refactoring verification script - run before/after any refactoring.

This script ensures that refactoring doesn't break existing functionality
by running all tests, coverage checks, and static analysis.

Usage:
    python scripts/verify_refactor.py
    python scripts/verify_refactor.py --fast          # Skip coverage
    python scripts/verify_refactor.py --fix           # Auto-fix linting issues
"""

import argparse
import subprocess
import sys
from pathlib import Path
from typing import List, Tuple, Optional


# Colors for output
class Colors:
    GREEN = "\033[92m"
    RED = "\033[91m"
    YELLOW = "\033[93m"
    BLUE = "\033[94m"
    BOLD = "\033[1m"
    END = "\033[0m"


def print_header(text: str) -> None:
    """Print a section header."""
    print(f"\n{Colors.BLUE}{Colors.BOLD}{text}{Colors.END}")
    print("=" * 60)


def print_success(text: str) -> None:
    """Print success message."""
    print(f"{Colors.GREEN}✅ {text}{Colors.END}")


def print_error(text: str) -> None:
    """Print error message."""
    print(f"{Colors.RED}❌ {text}{Colors.END}")


def print_warning(text: str) -> None:
    """Print warning message."""
    print(f"{Colors.YELLOW}⚠️  {text}{Colors.END}")


def run_command(
    cmd: List[str],
    description: str,
    allow_failure: bool = False,
    show_output: bool = True
) -> Tuple[bool, str]:
    """Run a command and return success status and output."""
    print(f"\n{Colors.BOLD}Running: {' '.join(cmd)}{Colors.END}")

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            cwd=Path(__file__).parent.parent
        )

        if show_output and result.stdout:
            print(result.stdout)

        success = result.returncode == 0

        if success:
            print_success(description)
        elif not allow_failure:
            print_error(f"{description} failed")
            if result.stderr:
                print(result.stderr)

        return success, result.stdout + result.stderr

    except Exception as e:
        print_error(f"Command failed with exception: {e}")
        return False, str(e)


def check_dependencies() -> bool:
    """Check if required tools are available."""
    print_header("Checking Dependencies")

    tools = {
        "pytest": "python -m pytest --version",
        "ruff": "ruff --version",
        "mypy": "mypy --version",
    }

    all_available = True
    for tool, version_cmd in tools.items():
        success, _ = run_command(
            version_cmd.split(),
            f"{tool} available",
            allow_failure=True,
            show_output=False
        )
        if not success:
            print_error(f"{tool} is not installed")
            all_available = False
        else:
            print_success(f"{tool} available")

    return all_available


def run_model_tests(fast: bool = False) -> bool:
    """Run all model tests."""
    print_header("Running Model Tests")

    cmd = ["python", "-m", "pytest", "tests/models/", "-v"]
    if fast:
        cmd.append("-x")  # Stop on first failure

    success, _ = run_command(cmd, "Model tests passed")
    return success


def check_coverage(fast: bool = False) -> bool:
    """Check test coverage."""
    if fast:
        print_warning("Skipping coverage check in fast mode")
        return True

    print_header("Checking Coverage")

    cmd = [
        "python", "-m", "pytest",
        "tests/models/",
        "--cov=src",
        "--cov-report=term-missing",
        "--cov-report=json",
        "--cov-fail-under=60"  # Minimum 60% coverage
    ]

    success, output = run_command(cmd, "Coverage check passed")

    # Parse coverage report if available
    if success and "coverage.json" in output:
        print_success("Coverage report generated: coverage.json")

    return success


def run_type_check() -> bool:
    """Run mypy type checker."""
    print_header("Type Checking with mypy")

    cmd = ["mypy", "src/"]
    success, _ = run_command(cmd, "Type checking passed", allow_failure=True)
    return success


def run_linter(fix: bool = False) -> bool:
    """Run ruff linter."""
    print_header("Linting with ruff")

    if fix:
        # Auto-fix issues
        cmd = ["ruff", "check", "src/", "--fix"]
        success, _ = run_command(cmd, "Linting issues auto-fixed")
    else:
        # Check only
        cmd = ["ruff", "check", "src/"]
        success, output = run_command(cmd, "No linting issues found")

    return success


def run_format_check(fix: bool = False) -> bool:
    """Check code formatting with ruff format."""
    print_header("Checking Code Format")

    if fix:
        cmd = ["ruff", "format", "src/"]
        success, _ = run_command(cmd, "Code formatted")
    else:
        cmd = ["ruff", "format", "src/", "--check"]
        success, _ = run_command(cmd, "Code formatting correct")

    return success


def print_summary(results: dict) -> int:
    """Print summary of all checks."""
    print_header("Verification Summary")

    for check, passed in results.items():
        status = Colors.GREEN + "✅" + Colors.END if passed else Colors.RED + "❌" + Colors.END
        print(f"{status} {check}")

    print()

    passed_count = sum(results.values())
    total_count = len(results)

    if passed_count == total_count:
        print_success(f"All {total_count} checks passed! 🎉")
        print("\n✨ Your refactoring is safe to commit.")
        return 0
    else:
        print_error(f"{total_count - passed_count}/{total_count} checks failed")
        print("\n🔧 Please fix the issues above before committing.")
        return 1


def main() -> int:
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Verify code quality before/after refactoring"
    )
    parser.add_argument(
        "--fast",
        action="store_true",
        help="Skip slow checks (coverage)"
    )
    parser.add_argument(
        "--fix",
        action="store_true",
        help="Auto-fix linting and formatting issues"
    )
    parser.add_argument(
        "--skip-type-check",
        action="store_true",
        help="Skip type checking with mypy"
    )
    parser.add_argument(
        "--skip-lint",
        action="store_true",
        help="Skip linting with ruff"
    )

    args = parser.parse_args()

    print(f"{Colors.BOLD}{Colors.BLUE}")
    print("╔══════════════════════════════════════════════════════════╗")
    print("║       Refactoring Verification Script                    ║")
    print("╚══════════════════════════════════════════════════════════╝")
    print(f"{Colors.END}")

    results = {}

    # Check dependencies
    if not check_dependencies():
        print_error("Missing dependencies. Please install:")
        print("  pip install pytest pytest-cov ruff mypy")
        return 1

    # Run tests (always)
    results["Model Tests"] = run_model_tests(fast=args.fast)

    # Check coverage (skip in fast mode)
    results["Coverage"] = check_coverage(fast=args.fast)

    # Type checking (optional)
    if not args.skip_type_check:
        results["Type Check"] = run_type_check()
    else:
        print_warning("Skipping type check")

    # Linting (optional)
    if not args.skip_lint:
        results["Linting"] = run_linter(fix=args.fix)
        results["Formatting"] = run_format_check(fix=args.fix)
    else:
        print_warning("Skipping linting")

    return print_summary(results)


if __name__ == "__main__":
    sys.exit(main())
