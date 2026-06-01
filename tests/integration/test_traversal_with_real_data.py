#!/usr/bin/env python3
"""Test traversal with real device screen data."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))


class MockVisionWithData:
    """Mock vision service that returns real test data."""

    def __init__(self, data_file: str):
        """Initialize with test data file."""
        with open(data_file) as f:
            self.data = json.load(f)
        self.call_count = 0

    def analyze_screenshot(self, image_data):
        """Return mock analysis with real data."""
        self.call_count += 1
        from src.state.content_tree import PageAnalysis
        return PageAnalysis(**self.data)

    def find_app_entry(self, image_data, target):
        """Mock find entry."""
        return {"x": 0.5, "y": 0.5, "name": target}


def test_traversal_with_real_data():
    """Test traversal using real screen analysis data."""
    print("=" * 60)
    print("Testing Traversal with Real Device Data")
    print("=" * 60)
    print()

    # Load test data
    data_file = Path("test_data/sample_phone_screen.json")
    if not data_file.exists():
        print(f"❌ Data file not found: {data_file}")
        return 1

    with open(data_file) as f:
        screen_data = json.load(f)

    print("📱 Real Screen Data:")
    print(f"   Level1 menus: {len(screen_data['level1_menus'])}")
    for menu in screen_data['level1_menus']:
        status = "🟢" if menu['active'] else "⚪"
        coord = menu.get('coordinate', {})
        print(f"      {status} {menu['name']} at ({coord.get('x', 0):.2f}, {coord.get('y', 0):.2f})")

    print(f"   Level2 menus: {len(screen_data['level2_menus'])}")
    for menu in screen_data['level2_menus']:
        status = "🟢" if menu['active'] else "⚪"
        coord = menu.get('coordinate', {})
        print(f"      {status} {menu['name']} at ({coord.get('x', 0):.2f}, {coord.get('y', 0):.2f})")

    print(f"   Interactive items: {len(screen_data['items'])}")
    print()

    # Create components
    from src.adb import MockADBClient
    from src.state import TraversalState
    from src.traversal import TraversalConfig, TraversalEngine

    adb = MockADBClient()
    vision = MockVisionWithData(str(data_file))
    state = TraversalState()

    # Set current path to match data
    state.current_path = ["工具", "购物"]

    config = TraversalConfig(max_steps=10)

    # Create event tracker
    events = []

    def track_event(event):
        events.append(event)
        print(f"[EVENT] {event.event_type}: {event.data}")

    # Create engine
    engine = TraversalEngine(
        adb_client=adb,
        vision_service=vision,
        state=state,
        config=config,
        event_callback=track_event,
    )

    print("🚀 Starting Traversal Test")
    print("-" * 60)

    # Simulate initialization
    print("\n📋 Simulating initialization...")
    from src.state.content_tree import PageAnalysis
    analysis = PageAnalysis(**screen_data)

    # Cache menus
    for menu in analysis.level1_menus:
        state.add_level1_menu(menu)

    level1_name = analysis.level1_menus[0].name if analysis.level1_menus else "工具"
    state.add_level2_menus(level1_name, analysis.level2_menus)

    cache_key = f"{level1_name}|购物"
    state.add_items(cache_key, analysis.items)

    # Build tree
    state.content_tree.root_title = "手机桌面"
    state.content_tree.add_node(title=level1_name, level=1, node_type="menu")

    print(f"   ✅ Cached {len(state.all_level1_menus)} level1 menus")
    print(f"   ✅ Cached {len(state.get_level2_menus(level1_name))} level2 menus")
    print(f"   ✅ Cached {len(state.get_items(cache_key))} items")

    # Test item selection
    print("\n🔍 Testing item selection...")
    items = state.get_items(cache_key)
    unvisited = [item for item in items if not state.is_visited(
        type('Fingerprint', (), {'level1': level1_name, 'level2': '购物', 'item_name': item.name})
    )]

    print(f"   Total items: {len(items)}")
    print(f"   Unvisited items: {len(unvisited)}")

    for item in unvisited[:3]:
        fp = item.get_fingerprint(level1_name, "购物")
        print(f"      - {item.name}: {fp}")

    # Test coordinate clicking simulation
    print("\n👆 Testing click simulation...")
    for item in items[:2]:
        coord = item.coordinate
        print(f"   Would click: {item.name} at ({coord.x:.2f}, {coord.y:.2f})")
        # Mark as visited
        state.visited.add(item.get_fingerprint(level1_name, "购物"))

    print(f"\n   ✅ Marked 2 items as visited")

    # Test visited tracking
    print("\n🔍 Testing visited tracking...")
    visited_count = len([item for item in items if state.is_visited(
        type('Fingerprint', (), {'level1': level1_name, 'level2': '购物', 'item_name': item.name})
    )])
    print(f"   Visited items: {visited_count}")

    # Display final tree
    print("\n📊 Content Tree:")
    print(state.content_tree.to_markdown())

    print("\n" + "=" * 60)
    print("✅ Real Data Traversal Test Complete!")
    print(f"   Events captured: {len(events)}")
    print(f"   Vision calls: {vision.call_count}")
    print(f"   ADB commands: {len(adb.command_log)}")
    print("=" * 60)

    return 0


if __name__ == "__main__":
    sys.exit(test_traversal_with_real_data())
