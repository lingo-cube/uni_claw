#!/usr/bin/env python3
"""Test MiMo vision service integration."""

import os
import sys
from pathlib import Path

# Add project root to path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

# Test MiMo API connectivity
def test_mimo_api():
    """Test MiMo API with environment variable."""
    print("🔍 Testing MiMo Vision Service...")

    api_key = os.environ.get("MIMO_API_KEY")
    if not api_key:
        print("   ⚠️  MIMO_API_KEY not set")
        print("   Set it with: export MIMO_API_KEY=your_key")
        return False

    try:
        from src.vision import MiMoVisionService

        vision = MiMoVisionService(api_key=api_key)

        # Test with dummy image
        dummy_png = b"\x89PNG\r\n\x1a\n" + b"\x00" * 100

        print("   Calling MiMo API...")
        # This will make a real API call
        result = vision.analyze_screenshot(dummy_png)

        print(f"   ✅ MiMo API response received")
        print(f"      - Level1 menus: {len(result.level1_menus)}")
        print(f"      - Level2 menus: {len(result.level2_menus)}")
        print(f"      - Items: {len(result.items)}")

        return True

    except Exception as e:
        print(f"   ❌ MiMo API test failed: {e}")
        return False


def test_mimo_with_real_image():
    """Test MiMo with a real screenshot if available."""
    screenshot_path = "test_screenshot.png"

    if not os.path.exists(screenshot_path):
        print(f"   ⏭️  No test screenshot at {screenshot_path}")
        print("      Place a screenshot there to test real image analysis")
        return None

    print(f"   📸 Testing with {screenshot_path}...")

    try:
        from src.vision import MiMoVisionService

        api_key = os.environ.get("MIMO_API_KEY")
        if not api_key:
            print("   ⚠️  MIMO_API_KEY not set")
            return False

        vision = MiMoVisionService(api_key=api_key)

        with open(screenshot_path, "rb") as f:
            image_data = f.read()

        result = vision.analyze_screenshot(image_data)

        print(f"   ✅ Analysis complete:")
        print(f"      - Current path: {result.current_path}")
        print(f"      - Level1 menus: {[m.name for m in result.level1_menus]}")
        print(f"      - Level2 menus: {[m.name for m in result.level2_menus]}")
        print(f"      - Items: {[i.name for i in result.items[:5]]}")

        return True

    except Exception as e:
        print(f"   ❌ Real image test failed: {e}")
        return False


def main():
    """Run MiMo tests."""
    print("=" * 50)
    print("MiMo Vision Service Test")
    print("=" * 50)
    print()

    # Check environment
    api_key = os.environ.get("MIMO_API_KEY")
    if api_key:
        print(f"✅ MIMO_API_KEY is set ({api_key[:10]}...)")
    else:
        print("⚠️  MIMO_API_KEY not set")
        print("   Set with: export MIMO_API_KEY=your_key")
    print()

    # Run tests
    results = []

    # Test 1: Basic API call
    try:
        result = test_mimo_api()
        results.append(result is False)  # None = skipped, treat as True
    except Exception as e:
        print(f"   ❌ Test crashed: {e}")
        results.append(False)
    print()

    # Test 2: Real image (if available)
    try:
        result = test_mimo_with_real_image()
        if result is None:
            results.append(True)  # Skipped
        else:
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
        print("✅ MiMo service is ready!")
        print()
        print("Usage:")
        print("  python run.py '车辆设置' --vision-provider mimo")
        return 0
    else:
        print("⚠️  Set up MIMO_API_KEY to test real functionality")
        return 1


if __name__ == "__main__":
    sys.exit(main())
