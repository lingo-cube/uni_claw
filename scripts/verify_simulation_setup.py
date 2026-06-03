#!/usr/bin/env python3
"""
Verify simulation setup script.

Checks that all required components are properly installed
and configured for simulation testing.
"""

import sys
import importlib
from pathlib import Path
from typing import List, Tuple


def check_python_version() -> Tuple[bool, str]:
    """Check Python version compatibility."""
    version = sys.version_info
    if version >= (3, 10):
        return True, f"✅ Python {version.major}.{version.minor}.{version.micro}"
    else:
        return False, f"❌ Python {version.major}.{version.minor}.{version.micro} (requires 3.10+)"


def check_dependencies() -> List[Tuple[bool, str]]:
    """Check required dependencies."""
    dependencies = [
        ("json", "JSON library"),
        ("dataclasses", "Dataclasses"),
        ("pathlib", "Pathlib"),
        ("typing", "Typing"),
        ("time", "Time module"),
        ("pytest", "Pytest"),
    ]

    results = []
    for module_name, description in dependencies:
        try:
            importlib.import_module(module_name)
            results.append((True, f"✅ {description}"))
        except ImportError:
            results.append((False, f"❌ {description} - NOT FOUND"))

    return results


def check_project_structure() -> List[Tuple[bool, str]]:
    """Check project structure."""
    required_paths = [
        ("src/simulation/", "Simulation source directory"),
        ("src/simulation/page_analyzer.py", "PageAnalyzer component"),
        ("src/simulation/mock_vision.py", "MockVisionService component"),
        ("src/simulation/mock_action.py", "MockActionExecutor component"),
        ("src/simulation/runner.py", "SimulationRunner component"),
        ("src/simulation/visualizer.py", "Visualizer component"),
        ("tests/simulation/", "Simulation tests directory"),
        ("tests/simulation/helpers/", "Test helpers directory"),
        ("tests/simulation/fixtures/", "Test fixtures directory"),
        ("cli/simtest.py", "CLI tool"),
    ]

    results = []
    project_root = Path(__file__).parent.parent

    for path_str, description in required_paths:
        path = project_root / path_str
        if path.exists():
            results.append((True, f"✅ {description}"))
        else:
            results.append((False, f"❌ {description} - NOT FOUND"))

    return results


def check_test_fixtures() -> List[Tuple[bool, str]]:
    """Check test fixtures availability."""
    fixture_dirs = [
        ("tests/simulation/fixtures/template/", "Template fixture"),
        ("tests/simulation/fixtures/e2e_all_traversal/", "E2E all traversal fixture"),
        ("tests/simulation/fixtures/e2e_target_found/", "E2E target found fixture"),
        ("tests/simulation/fixtures/e2e_static_path/", "E2E static path fixture"),
        ("tests/simulation/fixtures/e2e_popup_handling/", "E2E popup handling fixture"),
        ("tests/simulation/fixtures/e2e_auto_escape/", "E2E auto escape fixture"),
    ]

    results = []
    project_root = Path(__file__).parent.parent

    for path_str, description in fixture_dirs:
        path = project_root / path_str
        test_case = path / "test_case.json"
        plan = path / "plan_*.json" if "*" in path_str else path / "plan.json"
        pages = path / "pages_*.json" if "*" in path_str else path / "pages.json"

        if path.exists() and test_case.exists():
            results.append((True, f"✅ {description}"))
        else:
            results.append((False, f"❌ {description} - INCOMPLETE"))

    return results


def check_ci_cd_setup() -> List[Tuple[bool, str]]:
    """Check CI/CD setup."""
    ci_paths = [
        (".github/workflows/simulation-tests.yml", "GitHub Actions workflow"),
        ("cli/simtest.py", "CLI tool"),
    ]

    results = []
    project_root = Path(__file__).parent.parent

    for path_str, description in ci_paths:
        path = project_root / path_str
        if path.exists():
            results.append((True, f"✅ {description}"))
        else:
            results.append((False, f"❌ {description} - NOT FOUND"))

    return results


def verify_simulation_setup() -> int:
    """Run complete verification."""
    print("🔍 Simulation Testing Setup Verification")
    print("=" * 50)

    # Check Python version
    python_ok, python_msg = check_python_version()
    print(f"\n🐍 Python Version:")
    print(f"   {python_msg}")

    # Check dependencies
    print(f"\n📦 Dependencies Check:")
    dep_results = check_dependencies()
    for ok, msg in dep_results:
        print(f"   {msg}")

    # Check project structure
    print(f"\n📁 Project Structure:")
    structure_results = check_project_structure()
    for ok, msg in structure_results:
        print(f"   {msg}")

    # Check test fixtures
    print(f"\n🧪 Test Fixtures:")
    fixture_results = check_test_fixtures()
    for ok, msg in fixture_results:
        print(f"   {msg}")

    # Check CI/CD setup
    print(f"\n🔄 CI/CD Setup:")
    cicd_results = check_ci_cd_setup()
    for ok, msg in cicd_results:
        print(f"   {msg}")

    # Summary
    all_results = (
        [python_ok] +
        [ok for ok, _ in dep_results] +
        [ok for ok, _ in structure_results] +
        [ok for ok, _ in fixture_results] +
        [ok for ok, _ in cicd_results]
    )

    total = len(all_results)
    passed = sum(all_results)
    failed = total - passed

    print(f"\n📊 Verification Summary:")
    print(f"   Total Checks: {total}")
    print(f"   ✅ Passed: {passed}")
    print(f"   ❌ Failed: {failed}")

    if failed == 0:
        print(f"\n🎉 All checks passed! Simulation testing is properly configured.")
        return 0
    else:
        print(f"\n⚠️  {failed} check(s) failed. Please fix the issues above.")
        return 1


def main():
    """Main entry point."""
    return verify_simulation_setup()


if __name__ == '__main__':
    sys.exit(main())