#!/usr/bin/env python3
"""Test MiMo vision service with images from test_images folder."""

import os
import sys
from pathlib import Path

# Add project root to path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))


def test_all_images():
    """Test all images in test_images folder."""
    print("=" * 60)
    print("MiMo Vision Service - Image Analysis Test")
    print("=" * 60)
    print()

    # Check for API key
    api_key = os.environ.get("MIMO_API_KEY")
    if not api_key:
        print("❌ MIMO_API_KEY not set")
        print("   Set with: export MIMO_API_KEY=your_key")
        return 1

    # Find test images
    test_dir = Path("test_images")
    if not test_dir.exists():
        print(f"❌ Folder '{test_dir}' not found")
        print(f"   Create it: mkdir {test_dir}")
        return 1

    # Find all image files
    image_extensions = {".png", ".jpg", ".jpeg"}
    images = [
        f for f in test_dir.iterdir()
        if f.suffix.lower() in image_extensions
    ]

    if not images:
        print(f"❌ No images found in {test_dir}/")
        print(f"   Supported formats: {', '.join(image_extensions)}")
        return 1

    print(f"✅ Found {len(images)} image(s) in {test_dir}/")
    print()

    # Import vision service
    try:
        from src.vision import MiMoVisionService
        vision = MiMoVisionService(api_key=api_key)
    except Exception as e:
        print(f"❌ Failed to create MiMo service: {e}")
        return 1

    # Test each image
    for i, image_path in enumerate(images, 1):
        print(f"\n[{i}/{len(images)}] Testing: {image_path.name}")
        print("-" * 60)

        try:
            with open(image_path, "rb") as f:
                image_data = f.read()

            print(f"   Size: {len(image_data)} bytes")

            # Analyze
            print("   📸 Analyzing...")
            result = vision.analyze_screenshot(image_data)

            # Display results
            print("   ✅ Analysis complete:")
            print(f"      Current path: {result.current_path}")
            print(f"      Level1 menus: {len(result.level1_menus)}")
            for menu in result.level1_menus:
                status = "🟢" if menu.active else "⚪"
                print(f"         {status} {menu.name} ({menu.coordinate.x:.2f}, {menu.coordinate.y:.2f})")

            print(f"      Level2 menus: {len(result.level2_menus)}")
            for menu in result.level2_menus:
                status = "🟢" if menu.active else "⚪"
                print(f"         {status} {menu.name} ({menu.coordinate.x:.2f}, {menu.coordinate.y:.2f})")

            print(f"      Items: {len(result.items)}")
            for item in result.items[:5]:  # Show first 5
                print(f"         - {item.name} ({item.type}) at ({item.coordinate.x:.2f}, {item.coordinate.y:.2f})")
            if len(result.items) > 5:
                print(f"         ... and {len(result.items) - 5} more")

            if result.is_popup:
                print(f"      📱 Popup detected: {result.popup_info}")

        except Exception as e:
            print(f"   ❌ Failed: {e}")
            import traceback
            traceback.print_exc()

    print()
    print("=" * 60)
    print("Test complete!")
    return 0


def main():
    """Main entry."""
    return test_all_images()


if __name__ == "__main__":
    sys.exit(main())
