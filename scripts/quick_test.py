#!/usr/bin/env python3
"""Quick integration test - verifies all components work together."""

import sys
from pathlib import Path

# Add project root to path (parent of scripts directory)
sys.path.insert(0, str(Path(__file__).parent.parent))

def test_imports():
    """Test all modules can be imported."""
    print("📦 Testing imports...")
    try:
        from src.config import get_settings, Settings
        from src.adb import ADBClient, MockADBClient
        from src.vision import VisionService, MockVisionService
        from src.state import (
            TraversalState,
            StateManager,
            ContentTree,
            PageAnalysis,
            Coordinate,
        )
        from src.traversal import TraversalEngine, TraversalConfig, TraversalEvent
        print("   ✅ All imports successful")
        return True
    except ImportError as e:
        print(f"   ❌ Import failed: {e}")
        return False


def test_mock_clients():
    """Test mock clients work."""
    print("🤖 Testing mock clients...")
    try:
        from src.adb import MockADBClient
        from src.vision import MockVisionService

        adb = MockADBClient()
        vision = MockVisionService()

        # Test ADB
        assert adb.is_connected(), "ADB should be connected"
        size = adb.get_screen_size()
        assert size.width > 0 and size.height > 0, "Invalid screen size"

        # Test Vision
        result = vision.analyze_screenshot(b"test")
        assert result is not None, "Vision should return analysis"
        assert len(result.level1_menus) > 0, "Should have level1 menus"

        # Test find entry
        entry = vision.find_app_entry(b"test", "TestApp")
        assert entry is not None, "Should find app"
        assert entry["name"] == "TestApp", "Wrong app name"

        print("   ✅ Mock clients working")
        return True
    except Exception as e:
        print(f"   ❌ Mock test failed: {e}")
        return False


def test_data_models():
    """Test data models."""
    print("📊 Testing data models...")
    try:
        from src.state import (
            Coordinate,
            MenuInfo,
            MenuItem,
            PageAnalysis,
            ContentTree,
            TraversalState,
            VisitFingerprint,
        )

        # Test Coordinate
        coord = Coordinate(x=0.5, y=0.5)
        assert 0.0 <= coord.x <= 1.0, "Invalid x coordinate"

        # Test MenuInfo
        menu = MenuInfo(name="Test", coordinate=coord, active=True)
        assert menu.name == "Test", "Wrong menu name"

        # Test MenuItem
        item = MenuItem(
            name="Item1",
            type="item",
            coordinate=coord,
        )
        fp = item.get_fingerprint("L1", "L2")
        assert fp == "L1|L2|Item1", "Wrong fingerprint"

        # Test ContentTree
        tree = ContentTree(root_title="TestApp")
        node = tree.add_node(title="Menu1", level=1, node_type="menu")
        child = tree.add_child_node(title="Item1", parent_id=node.id)
        assert child is not None, "Child node creation failed"
        assert child.id == f"{node.id}.1", f"Wrong child ID: {child.id}"

        # Test TraversalState
        state = TraversalState(current_path=["L1", "L2"])
        key = state.get_current_cache_key()
        assert key == "L1|L2", f"Wrong cache key: {key}"

        # Test VisitFingerprint
        fp_obj = VisitFingerprint(level1="L1", level2="L2", item_name="Item")
        assert str(fp_obj) == "L1|L2|Item", "Wrong fingerprint string"

        print("   ✅ Data models working")
        return True
    except Exception as e:
        print(f"   ❌ Data model test failed: {e}")
        import traceback
        traceback.print_exc()
        return False


def test_state_manager():
    """Test state persistence."""
    print("💾 Testing state manager...")
    try:
        import tempfile
        from src.state import StateManager, TraversalState

        with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as f:
            state_file = f.name

        try:
            manager = StateManager(state_file)
            manager.state.current_path = ["Menu1", "Tab1"]
            manager.state.target_app = "TestApp"
            manager.save()

            # Load new instance
            manager2 = StateManager(state_file)
            assert manager2.state.current_path == ["Menu1", "Tab1"], "State not persisted"
            assert manager2.state.target_app == "TestApp", "Target not persisted"

            print("   ✅ State persistence working")
            return True
        finally:
            import os
            if os.path.exists(state_file):
                os.unlink(state_file)
    except Exception as e:
        print(f"   ❌ State manager test failed: {e}")
        return False


def test_traversal_flow():
    """Test basic traversal flow."""
    print("🔄 Testing traversal flow...")
    try:
        from src.adb import MockADBClient
        from src.vision import MockVisionService
        from src.state import TraversalState
        from src.traversal import TraversalEngine, TraversalConfig

        adb = MockADBClient()
        vision = MockVisionService()
        state = TraversalState()
        config = TraversalConfig(max_steps=2)

        engine = TraversalEngine(
            adb_client=adb,
            vision_service=vision,
            state=state,
            config=config,
        )

        # Test navigation
        result = engine.navigate_to_app("TestApp")
        assert result is True, "Navigation failed"

        # Test initialization
        result = engine.initialize_structure()
        assert result is True, "Initialization failed"
        assert len(state.all_level1_menus) > 0, "No level1 menus cached"

        # Test single step
        should_continue = engine.run_step()
        # Should stop since mock returns empty items after first call
        assert isinstance(should_continue, bool), "run_step should return bool"

        print("   ✅ Traversal flow working")
        return True
    except Exception as e:
        print(f"   ❌ Traversal test failed: {e}")
        import traceback
        traceback.print_exc()
        return False


def main():
    """Run all tests."""
    print("=" * 50)
    print("Uni-claw Quick Integration Test")
    print("=" * 50)
    print()

    tests = [
        test_imports,
        test_mock_clients,
        test_data_models,
        test_state_manager,
        test_traversal_flow,
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
        print("✅ All tests passed! Ready to use.")
        print()
        print("Next steps:")
        print("  - Run mock demo: python run.py '测试' --mock")
        print("  - Run unit tests: pytest tests/ -v")
        return 0
    else:
        print("❌ Some tests failed. Check output above.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
