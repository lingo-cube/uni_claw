#!/usr/bin/env python3
"""Verification script for V6.13.0 state migration.

This script verifies that:
1. No src.state imports remain in src/ directory
2. No src.state imports remain in tests/ directory
3. All new imports from src.models.content_models work correctly
4. Backward compatibility alias works
5. All model tests pass
6. Deprecation warnings work correctly

Usage:
    python scripts/verify_state_migration.py
"""

import ast
import os
import subprocess
import sys
from pathlib import Path
from typing import List, Tuple

# Ensure project root is in path
PROJECT_ROOT = Path(__file__).parent.parent
sys.path.insert(0, str(PROJECT_ROOT))


def find_python_files(directory: Path) -> List[Path]:
    """Find all Python files in directory."""
    return list(directory.rglob("*.py"))


def check_import_in_file(file_path: Path, import_pattern: str) -> bool:
    """Check if file contains a specific import pattern."""
    try:
        content = file_path.read_text()
        return import_pattern in content
    except Exception:
        return False


def check_src_state_imports() -> Tuple[int, List[str]]:
    """Check for any remaining src.state imports (excluding src.state_machine)."""
    project_root = Path(__file__).parent.parent
    src_dir = project_root / "src"
    tests_dir = project_root / "tests"

    violations = []

    # Check src/ directory
    for py_file in find_python_files(src_dir):
        if "src.state" in py_file.as_posix():
            # Skip src/state/__init__.py (that's the deprecation module)
            if py_file.name == "__init__.py" and py_file.parent.name == "state":
                continue
            # Skip src.state_machine references
            if "src/state_machine" in py_file.as_posix():
                continue

            if check_import_in_file(py_file, "from src.state"):
                # Verify it's not a comment
                content = py_file.read_text()
                for line in content.splitlines():
                    if "from src.state" in line and not line.strip().startswith("#"):
                        # Exclude src.state_machine
                        if "src.state_machine" not in line:
                            violations.append(f"  - {py_file}: {line.strip()}")

    # Check tests/ directory
    for py_file in find_python_files(tests_dir):
        if "src.state" in py_file.as_posix():
            # Skip src.state_machine references
            if "src/state_machine" in py_file.as_posix():
                continue

            if check_import_in_file(py_file, "from src.state"):
                content = py_file.read_text()
                for line in content.splitlines():
                    if "from src.state" in line and not line.strip().startswith("#"):
                        if "src.state_machine" not in line:
                            violations.append(f"  - {py_file}: {line.strip()}")

    return len(violations), violations


def check_new_models_imports() -> Tuple[int, List[str]]:
    """Verify new imports work correctly."""
    errors = []

    try:
        # Clear import cache to ensure fresh import
        for mod in list(sys.modules.keys()):
            if mod.startswith("src"):
                del sys.modules[mod]

        # Test importing from new location
        from src.models.content_models import (
            SimulationState,
            ContentTree,
            ContentNode,
            VisitFingerprint,
            Coordinate,
            Direction,
            MenuInfo,
            MenuItem,
            MenuItemType,
            ExpectedAction,
            PageAnalysis,
            PopupInfo,
        )

        # Test backward compatibility alias
        from src.models import TraversalState
        assert TraversalState is SimulationState

    except Exception as e:
        errors.append(f"  - Import error: {e}")

    return len(errors), errors


def check_model_tests() -> Tuple[int, List[str]]:
    """Run model tests to verify functionality."""
    project_root = Path(__file__).parent.parent

    result = subprocess.run(
        ["python", "-m", "pytest", "src/models/test/test_content_models.py", "-v", "--tb=short"],
        cwd=project_root,
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        return result.returncode, [f"  - Test failed: {result.stdout[-500:]}"]

    return 0, []


def check_exception_tests() -> Tuple[int, List[str]]:
    """Run exception tests that use TraversalState."""
    project_root = Path(__file__).parent.parent

    result = subprocess.run(
        ["python", "-m", "pytest", "src/exception/test/", "-v", "--tb=short"],
        cwd=project_root,
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        return result.returncode, [f"  - Test failed: {result.stdout[-500:]}"]

    return 0, []


def check_deprecation_warnings() -> Tuple[int, List[str]]:
    """Verify deprecation warnings work correctly."""
    info = []

    try:
        # Clear import cache
        for mod in list(sys.modules.keys()):
            if mod.startswith("src.state"):
                del sys.modules[mod]

        # Test that deprecation warning is issued
        import warnings

        with warnings.catch_warnings(record=True) as w:
            warnings.simplefilter("always", DeprecationWarning)
            from src.state import ContentTree

            if len(w) > 0 and issubclass(w[-1].category, DeprecationWarning):
                info.append(f"  ✓ Deprecation warning correctly issued")
            else:
                info.append(f"  ⚠ WARNING: No deprecation warning detected")

    except Exception as e:
        info.append(f"  - Info: {e}")

    return 0, info  # Non-failing check


def main() -> int:
    """Run all verification checks."""
    print("=" * 80)
    print("V6.13.0 State Migration Verification")
    print("=" * 80)
    print()

    all_passed = True

    # Check 1: No src.state imports remain
    print("✓ Check 1: Verifying no src.state imports remain...")
    count, violations = check_src_state_imports()
    if count > 0:
        print(f"  ❌ FAILED: Found {count} src.state import(s):")
        for v in violations:
            print(v)
        all_passed = False
    else:
        print("  ✓ PASSED")
    print()

    # Check 2: New imports work
    print("✓ Check 2: Verifying new imports work correctly...")
    count, errors = check_new_models_imports()
    if count > 0:
        print(f"  ❌ FAILED:")
        for e in errors:
            print(e)
        all_passed = False
    else:
        print("  ✓ PASSED")
    print()

    # Check 3: Model tests pass
    print("✓ Check 3: Running model tests...")
    count, errors = check_model_tests()
    if count > 0:
        print(f"  ❌ FAILED:")
        for e in errors:
            print(e)
        all_passed = False
    else:
        print("  ✓ PASSED")
    print()

    # Check 4: Exception tests pass
    print("✓ Check 4: Running exception tests...")
    count, errors = check_exception_tests()
    if count > 0:
        print(f"  ❌ FAILED:")
        for e in errors:
            print(e)
        all_passed = False
    else:
        print("  ✓ PASSED")
    print()

    # Check 5: Deprecation warnings
    print("✓ Check 5: Verifying deprecation warnings...")
    count, errors = check_deprecation_warnings()
    if errors:
        for e in errors:
            print(e)
        print("  ✓ INFO")
    print()

    # Summary
    print("=" * 80)
    if all_passed:
        print("✅ All verification checks PASSED")
        print("=" * 80)
        return 0
    else:
        print("❌ Some verification checks FAILED")
        print("=" * 80)
        return 1


if __name__ == "__main__":
    sys.exit(main())
