#!/usr/bin/env python3
"""Test traversal with real device screen data."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))


def test_with_real_screen_data():
    """Test using real phone screen analysis data."""
    print("=" * 60)
    print("Testing with Real Device Screen Data")
    print("=" * 60)
    print()

    # Load sample data
    data_file = Path("test_data/sample_phone_screen.json")
    if not data_file.exists():
        print(f"❌ Data file not found: {data_file}")
        return 1

    with open(data_file) as f:
        screen_data = json.load(f)

    print("📱 Real Screen Analysis:")
    print(f"   Level1 menus: {len(screen_data['level1_menus'])}")
    for menu in screen_data['level1_menus']:
        status = "🟢" if menu['active'] else "⚪"
        coord = menu.get('coordinate', {})
        x, y = coord.get('x', 0), coord.get('y', 0)
        print(f"      {status} {menu['name']} at ({x:.2f}, {y:.2f})")

    print(f"   Level2 menus: {len(screen_data['level2_menus'])}")
    for menu in screen_data['level2_menus']:
        status = "🟢" if menu['active'] else "⚪"
        coord = menu.get('coordinate', {})
        x, y = coord.get('x', 0), coord.get('y', 0)
        print(f"      {status} {menu['name']} at ({x:.2f}, {y:.2f})")

    print(f"   Interactive items: {len(screen_data['items'])}")

    # Test data parsing
    print()
    print("🔍 Testing data parsing...")

    from src.state.content_tree import PageAnalysis

    try:
        analysis = PageAnalysis(**screen_data)
        print("   ✅ PageAnalysis created successfully")
        print(f"      Current path: {analysis.current_path}")
        print(f"      Has popup: {analysis.is_popup}")

    except Exception as e:
        print(f"   ❌ Failed to parse: {e}")
        return 1

    # Test fingerprint generation
    print()
    print("🔍 Testing item fingerprinting...")

    for item in analysis.items[:3]:  # Test first 3 items
        fp = item.get_fingerprint("Level1", "Level2")
        print(f"   {item.name}: {fp}")

    # Test coordinate validation
    print()
    print("🔍 Testing coordinate validation...")

    invalid_coords = []
    for item in analysis.items:
        x, y = item.coordinate.x, item.coordinate.y
        if not (0.0 <= x <= 1.0 and 0.0 <= y <= 1.0):
            invalid_coords.append((item.name, x, y))

    if invalid_coords:
        print(f"   ❌ Found {len(invalid_coords)} invalid coordinates:")
        for name, x, y in invalid_coords:
            print(f"      {name}: ({x:.2f}, {y:.2f})")
    else:
        print(f"   ✅ All {len(analysis.items)} coordinates valid (0-1 range)")

    # Test tree building
    print()
    print("🔍 Testing content tree building...")

    from src.state.content_tree import ContentTree

    tree = ContentTree(root_title="手机桌面")
    tree.add_node(title="工具", level=1, node_type="menu")
    l1_node = list(tree.nodes.values())[0]

    tree.add_child_node(title="美团视频", parent_id=l1_node.id, node_type="app")
    tree.add_child_node(title="停车助手", parent_id=l1_node.id, node_type="app")

    print(f"   ✅ Tree built with {len(tree.nodes)} nodes")
    print(tree.to_markdown())

    print()
    print("=" * 60)
    print("✅ Real data test complete!")
    return 0


if __name__ == "__main__":
    sys.exit(test_with_real_screen_data())
