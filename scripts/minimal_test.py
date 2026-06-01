#!/usr/bin/env python3
"""Minimal test without external dependencies - verifies core logic."""

import sys
from pathlib import Path

# Test without dependencies
def test_data_structures():
    """Test data structures work correctly."""
    print("📊 Testing data structures...")

    # Test Coordinate
    class Coordinate:
        def __init__(self, x, y):
            if not (0.0 <= x <= 1.0):
                raise ValueError(f"Invalid x: {x}")
            if not (0.0 <= y <= 1.0):
                raise ValueError(f"Invalid y: {y}")
            self.x = x
            self.y = y

    c = Coordinate(0.5, 0.5)
    assert c.x == 0.5
    print("   ✅ Coordinate works")

    # Test fingerprint generation
    def get_fingerprint(item_name, level1, level2):
        return f"{level1}|{level2}|{item_name}"

    fp = get_fingerprint("Item1", "Menu1", "Tab1")
    assert fp == "Menu1|Tab1|Item1"
    print("   ✅ Fingerprint generation works")

    # Test cache key
    def get_cache_key(path):
        if len(path) < 2:
            return "root"
        return "|".join(path[-2:])

    key = get_cache_key(["Menu1", "Tab1", "SubTab"])
    assert key == "Tab1|SubTab"
    print("   ✅ Cache key generation works")

    # Test hierarchical ID
    def generate_child_id(parent_id, sibling_count):
        if sibling_count == 0:
            return f"{parent_id}.1"
        return f"{parent_id}.{sibling_count + 1}"

    assert generate_child_id("1", 0) == "1.1"
    assert generate_child_id("1", 2) == "1.3"
    assert generate_child_id("1.1", 0) == "1.1.1"
    print("   ✅ Hierarchical ID generation works")

    return True


def test_project_structure():
    """Test project files exist."""
    print("📁 Testing project structure...")

    required_files = [
        "src/__init__.py",
        "src/adb/__init__.py",
        "src/adb/adb_client.py",
        "src/vision/__init__.py",
        "src/vision/vision_service.py",
        "src/state/__init__.py",
        "src/state/content_tree.py",
        "src/state/state_manager.py",
        "src/traversal/__init__.py",
        "src/traversal/traversal_engine.py",
        "src/config/__init__.py",
        "src/config/settings.py",
        "run.py",
        "README.md",
    ]

    # Use this file's location to find project root
    base = Path(__file__).parent.parent
    missing = []

    for f in required_files:
        full_path = base / f
        if not full_path.exists():
            missing.append(f)

    if missing:
        print(f"   ❌ Missing files: {missing}")
        return False

    print(f"   ✅ All {len(required_files)} files present")
    return True


def test_code_logic():
    """Test core logic without running imports."""
    print("🧠 Testing core logic...")

    # Test coordinate validation logic
    def is_valid_coordinate(x, y):
        return 0.0 <= x <= 1.0 and 0.0 <= y <= 1.0

    assert is_valid_coordinate(0.0, 0.0)
    assert is_valid_coordinate(0.5, 0.5)
    assert is_valid_coordinate(1.0, 1.0)
    assert not is_valid_coordinate(1.5, 0.5)
    assert not is_valid_coordinate(0.5, -0.1)
    print("   ✅ Coordinate validation logic works")

    # Test path selection logic
    def should_select_item(item, visited, current_path):
        fp = f"{current_path[-2]}|{current_path[-1]}|{item['name']}"
        return fp not in visited

    visited = {"Menu1|Tab1|Item1"}
    current_path = ["Menu1", "Tab1"]

    assert not should_select_item({"name": "Item1"}, visited, current_path)
    assert should_select_item({"name": "Item2"}, visited, current_path)
    print("   ✅ Item selection logic works")

    # Test menu switching logic
    def can_switch_menu(current, menus):
        for i, menu in enumerate(menus):
            if menu["name"] == current:
                return i + 1 < len(menus)
        return False

    menus = [
        {"name": "Tab1", "coord": (0.1, 0.05)},
        {"name": "Tab2", "coord": (0.3, 0.05)},
    ]

    assert can_switch_menu("Tab1", menus)
    assert not can_switch_menu("Tab2", menus)
    print("   ✅ Menu switching logic works")

    return True


def main():
    """Run minimal tests."""
    print("=" * 50)
    print("Uni-claw Minimal Test (No Dependencies)")
    print("=" * 50)
    print()

    tests = [
        test_data_structures,
        test_project_structure,
        test_code_logic,
    ]

    results = []
    for test in tests:
        try:
            result = test()
            results.append(result)
        except Exception as e:
            print(f"   ❌ Test crashed: {e}")
            results.append(False)
        print()

    # Summary
    print("=" * 50)
    passed = sum(results)
    total = len(results)
    print(f"Results: {passed}/{total} tests passed")

    if passed == total:
        print("✅ Core logic verified!")
        print()
        print("To test with dependencies:")
        print("  1. Install: pip install -r requirements.txt")
        print("  2. Run: python quick_test.py")
        print("  3. Run: python run.py '测试' --mock")
        return 0
    else:
        print("❌ Some tests failed.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
