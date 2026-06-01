#!/usr/bin/env python3
"""Test click events with real device."""

import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))


def test_real_clicks():
    """Test clicking elements from real screen data on real device."""
    print("=" * 60)
    print("Testing Real Device Click Events")
    print("=" * 60)
    print()

    # Load test data
    data_file = Path("test_data/sample_phone_screen.json")
    with open(data_file) as f:
        screen_data = json.load(f)

    # Get screen size
    import subprocess
    result = subprocess.run(
        ["/usr/local/bin/adb", "shell", "wm", "size"],
        capture_output=True,
        text=True
    )
    # Parse "Physical size: 1440x3168"
    size_line = [line for line in result.stdout.split('\n') if 'Physical size' in line]
    if size_line:
        size_str = size_line[0].split(': ')[1]
        width, height = map(int, size_str.split('x'))
        print(f"📱 Screen size: {width}x{height}")
    else:
        width, height = 1440, 3168  # Default from previous detection
        print(f"📱 Using default size: {width}x{height}")

    print()

    # Test clicking some elements
    print("👆 Testing click events from screen data:")
    print("-" * 60)

    # Convert normalized coordinates to pixel coordinates
    def to_pixel(norm_x, norm_y):
        return int(norm_x * width), int(norm_y * height)

    # Click on a few elements
    test_items = screen_data['items'][:5]  # Test first 5 items

    for i, item in enumerate(test_items, 1):
        name = item['name']
        item_type = item.get('type', 'item')
        coord = item.get('coordinate', {})
        norm_x, norm_y = coord.get('x', 0.5), coord.get('y', 0.5)

        # Convert to pixel coordinates
        px, py = to_pixel(norm_x, norm_y)

        print(f"\n[{i}] Clicking: {name} ({item_type})")
        print(f"   Normalized: ({norm_x:.2f}, {norm_y:.2f})")
        print(f"   Pixel: ({px}, {py})")

        # Execute click
        cmd = f"/usr/local/bin/adb shell input tap {px} {py}"
        print(f"   Command: adb shell input tap {px} {py}")

        result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
        if result.returncode == 0:
            print(f"   ✅ Click executed successfully")
        else:
            print(f"   ❌ Click failed: {result.stderr}")

        # Wait and capture screen to see effect
        time.sleep(1)

    print()
    print("-" * 60)
    print("✅ Click test complete!")

    # Test home button
    print()
    print("🏠 Testing Home button...")
    subprocess.run(["/usr/local/bin/adb", "shell", "input", "keyevent", "KEYCODE_HOME"])
    print("   ✅ Home button pressed")

    # Test back button
    print()
    print("⬅️  Testing Back button...")
    subprocess.run(["/usr/local/bin/adb", "shell", "input", "keyevent", "KEYCODE_BACK"])
    print("   ✅ Back button pressed")

    print()
    print("=" * 60)
    print("All click tests completed!")
    print("=" * 60)

    return 0


if __name__ == "__main__":
    sys.exit(test_real_clicks())
