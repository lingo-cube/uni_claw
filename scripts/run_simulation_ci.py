#!/usr/bin/env python
"""
Simulation Test CI Runner

Run E2E simulation tests based on configuration in tests/simulation-ci.yaml.

Usage:
    python scripts/run_simulation_ci.py
    python scripts/run_simulation_ci.py --suite e2e_simulation
    python scripts/run_simulation_ci.py --verbose
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Any
import yaml


# Configuration path
CONFIG_PATH = Path(__file__).parent.parent / "tests" / "simulation-ci.yaml"


def load_config(config_path: Path = CONFIG_PATH) -> Dict[str, Any]:
    """Load simulation CI configuration."""
    with open(config_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


def run_pytest(
    test_files: List[str],
    verbose: bool = False,
    timeout: int = 300,
    coverage_packages: List[str] = None
) -> Dict[str, Any]:
    """Run pytest on given test files."""
    cmd = ["pytest", "-v" if verbose else "-q"]

    # Add coverage if specified
    if coverage_packages:
        for pkg in coverage_packages:
            cmd.extend([f"--cov={pkg}", "--cov-report=term-missing"])

    # Add test files
    cmd.extend(test_files)

    print(f"[+] Running: {' '.join(cmd)}")
    print()

    result = subprocess.run(
        cmd,
        capture_output=False,
        text=True
    )

    return {
        "returncode": result.returncode,
        "success": result.returncode == 0
    }


def run_test_suite(
    suite_name: str,
    suite_config: Dict[str, Any],
    verbose: bool = False,
    coverage_packages: List[str] = None
) -> Dict[str, Any]:
    """Run a single test suite."""
    print(f"\n{'='*60}")
    print(f"[*] Running Suite: {suite_name}")
    print(f"   {suite_config.get('description', '')}")
    print(f"{'='*60}\n")

    test_files = suite_config.get("test_files", [])

    if not test_files:
        print(f"[!]  No test files configured for {suite_name}")
        return {"success": True, "skipped": True}

    # Run tests
    result = run_pytest(
        test_files=test_files,
        verbose=verbose,
        timeout=suite_config.get("timeout", 300),
        coverage_packages=coverage_packages if suite_name == list(suite_config.keys())[0] else None
    )

    return {
        "suite": suite_name,
        "success": result["success"],
        "required": suite_config.get("required", True),
        "returncode": result["returncode"]
    }


def main():
    parser = argparse.ArgumentParser(
        description="Run simulation tests for CI"
    )
    parser.add_argument(
        "--suite",
        help="Run specific test suite only"
    )
    parser.add_argument(
        "-v", "--verbose",
        action="store_true",
        help="Verbose output"
    )
    parser.add_argument(
        "--no-coverage",
        action="store_true",
        help="Disable coverage reporting"
    )

    args = parser.parse_args()

    # Load configuration
    try:
        config = load_config()
    except FileNotFoundError:
        print(f"[FAIL] Configuration file not found: {CONFIG_PATH}")
        print(f"   Please create {CONFIG_PATH} to use simulation CI.")
        return 1
    except yaml.YAMLError as e:
        print(f"[FAIL] Error parsing configuration: {e}")
        return 1

    print(f"""
╔════════════════════════════════════════════════════════════╗
║           Simulation Test CI - Version {config['version']}              ║
╚════════════════════════════════════════════════════════════╝
""")

    # Prepare coverage packages
    coverage_packages = []
    if not args.no_coverage and config.get("coverage", {}).get("enabled", True):
        coverage_packages = config.get("coverage", {}).get("packages", [])

    # Run test suites
    results = []
    test_suites = config.get("test_suites", {})

    if args.suite:
        # Run single suite
        if args.suite not in test_suites:
            print(f"[FAIL] Unknown suite: {args.suite}")
            print(f"   Available suites: {', '.join(test_suites.keys())}")
            return 1
        suites_to_run = {args.suite: test_suites[args.suite]}
    else:
        # Run all suites
        suites_to_run = test_suites

    for suite_name, suite_config in suites_to_run.items():
        result = run_test_suite(
            suite_name=suite_name,
            suite_config=suite_config,
            verbose=args.verbose,
            coverage_packages=coverage_packages
        )
        results.append(result)

    # Print summary
    print(f"\n{'='*60}")
    print("[*] Test Summary")
    print(f"{'='*60}\n")

    required_passed = 0
    required_total = 0
    all_passed = True

    for result in results:
        suite = result["suite"]
        success = result["success"]
        required = result.get("required", True)
        skipped = result.get("skipped", False)

        if skipped:
            status = "⏭️  SKIPPED"
        elif success:
            status = "[OK] PASSED"
            if required:
                required_passed += 1
                required_total += 1
        else:
            status = "[FAIL] FAILED"
            all_passed = False
            if required:
                required_total += 1

        required_mark = " [REQUIRED]" if required else ""
        print(f"{suite}{required_mark}: {status}")

    print()
    print(f"Required tests: {required_passed}/{required_total} passed")

    if all_passed:
        print("\n[*] All simulation tests passed!")
        return 0
    else:
        print("\n[FAIL] Some simulation tests failed.")
        print("   Check the output above for details.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
