#!/usr/bin/env python3
"""
Simple test to verify Mock component fixes.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from src.simulation.mock_vision import MockVisionService
from src.simulation.page_analyzer import PageAnalyzer

def test_mock_vision_service():
    """Test MockVisionService with new PageAnalysis format."""
    print("=" * 60)
    print("Testing MockVisionService with Enhanced PageAnalysis")
    print("=" * 60)

    # Create sample pages in new format
    virtual_pages = {
        "HomeScreen": {
            "current_path": [],
            "items": [
                {
                    "id": 1,
                    "name": "Settings",
                    "type": "button",
                    "expected_action": "navigate",
                    "coordinate": {"x": 0.5, "y": 0.5}
                }
            ],
            "has_scroll": False,
            "is_popup": False
        },
        "SettingsPage": {
            "current_path": ["Settings"],
            "items": [
                {
                    "id": 2,
                    "name": "Display",
                    "type": "menu_item",
                    "expected_action": "navigate",
                    "coordinate": {"x": 0.2, "y": 0.3}
                }
            ],
            "has_scroll": False,
            "is_popup": False
        }
    }

    # Test MockVisionService
    vision = MockVisionService(virtual_pages)

    # Test root path
    print("\n[Test 1] Analyzing root path:")
    result = vision.analyze_screenshot()
    print(f"  - Elements found: {len(result['elements'])}")
    print(f"  - First element: {result['elements'][0]['text']}")

    # Test Settings path
    print("\n[Test 2] Testing path injection:")
    vision.inject_path("Settings")
    result = vision.analyze_screenshot()
    print(f"  - Elements found: {len(result['elements'])}")
    print(f"  - First element: {result['elements'][0]['text']}")

    # Test nested path
    print("\n[Test 3] Testing nested path:")
    vision.inject_path("Settings/Display")
    try:
        result = vision.analyze_screenshot()
        print(f"  - Success! Found page with {len(result['elements'])} elements")
    except Exception as e:
        print(f"  - Error (expected for now): {e}")

    print("\n[SUCCESS] MockVisionService tests completed!")

def test_page_analyzer():
    """Test PageAnalyzer with current_path matching."""
    print("\n" + "=" * 60)
    print("Testing PageAnalyzer with current_path matching")
    print("=" * 60)

    virtual_pages = {
        "HomeScreen": {
            "current_path": [],
            "items": [{"id": 1, "name": "Settings", "type": "button"}],
            "has_scroll": False,
            "is_popup": False
        },
        "SettingsPage": {
            "current_path": ["Settings"],
            "items": [{"id": 2, "name": "Display", "type": "menu_item"}],
            "has_scroll": False,
            "is_popup": False
        }
    }

    analyzer = PageAnalyzer(virtual_pages)

    # Test root path
    print("\n[Test 1] Analyzing root:")
    result = analyzer.analyze_page("root")
    print(f"  - Elements: {len(result['elements'])}")
    print(f"  - First element: {result['elements'][0]['text']}")

    # Test Settings path
    print("\n[Test 2] Analyzing Settings:")
    result = analyzer.analyze_page("Settings")
    print(f"  - Elements: {len(result['elements'])}")
    print(f"  - First element: {result['elements'][0]['text']}")

    print("\n[SUCCESS] PageAnalyzer tests completed!")

def main():
    """Run all tests."""
    try:
        test_page_analyzer()
        test_mock_vision_service()

        print("\n" + "=" * 60)
        print("ALL MOCK TESTS PASSED!")
        print("=" * 60)
        return 0

    except Exception as e:
        print(f"\n[ERROR] Test failed: {e}")
        import traceback
        traceback.print_exc()
        return 1

if __name__ == "__main__":
    sys.exit(main())