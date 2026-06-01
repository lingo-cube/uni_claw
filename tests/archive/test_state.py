"""Tests for state management."""

import json
from pathlib import Path

import pytest

from src.state.content_tree import (
    ContentNode,
    ContentTree,
    Coordinate,
    ExpectedAction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    TraversalState,
    VisitFingerprint,
)
from src.state.state_manager import StateManager


class TestCoordinate:
    """Test Coordinate model."""

    def test_coordinate_creation(self):
        """Test creating valid coordinates."""
        coord = Coordinate(x=0.5, y=0.5)
        assert coord.x == 0.5
        assert coord.y == 0.5

    def test_coordinate_validation(self):
        """Test coordinate validation."""
        # Valid range
        Coordinate(x=0.0, y=0.0)
        Coordinate(x=1.0, y=1.0)

        # Invalid - should raise validation error
        with pytest.raises(Exception):
            Coordinate(x=1.5, y=0.5)

    def test_to_adb_tap(self):
        """Test conversion to ADB tap command."""
        coord = Coordinate(x=0.5, y=0.25)
        adb_cmd = coord.to_adb_tap()

        # Should produce pixel coordinates for 1080x1920
        assert "540" in adb_cmd  # 0.5 * 1080
        assert "480" in adb_cmd  # 0.25 * 1920


class TestMenuInfo:
    """Test MenuInfo model."""

    def test_menu_info_creation(self):
        """Test creating menu info."""
        info = MenuInfo(
            name="TestMenu",
            coordinate=Coordinate(x=0.1, y=0.2),
            active=True,
        )
        assert info.name == "TestMenu"
        assert info.active


class TestMenuItem:
    """Test MenuItem model."""

    def test_menu_item_creation(self):
        """Test creating menu item."""
        item = MenuItem(
            name="TestItem",
            type="item",
            coordinate=Coordinate(x=0.5, y=0.5),
        )
        assert item.name == "TestItem"
        assert item.parent is None

    def test_menu_item_with_parent(self):
        """Test menu item with parent reference."""
        item = MenuItem(
            name="ChildItem",
            type="switch",
            coordinate=Coordinate(x=0.8, y=0.5),
            parent="ParentItem",
        )
        assert item.parent == "ParentItem"

    def test_get_fingerprint(self):
        """Test fingerprint generation."""
        item = MenuItem(
            name="MyItem",
            type="item",
            coordinate=Coordinate(x=0.5, y=0.5),
        )
        fp = item.get_fingerprint("Level1", "Level2")
        assert fp == "Level1|Level2|MyItem"


class TestVisitFingerprint:
    """Test VisitFingerprint model."""

    def test_fingerprint_string(self):
        """Test string representation."""
        fp = VisitFingerprint(level1="L1", level2="L2", item_name="Item")
        assert str(fp) == "L1|L2|Item"

    def test_fingerprint_from_string(self):
        """Test parsing from string."""
        fp = VisitFingerprint.from_string("L1|L2|Item")
        assert fp.level1 == "L1"
        assert fp.level2 == "L2"
        assert fp.item_name == "Item"

    def test_fingerprint_invalid_string(self):
        """Test parsing invalid string raises error."""
        with pytest.raises(ValueError):
            VisitFingerprint.from_string("invalid")


class TestContentTree:
    """Test ContentTree model."""

    def test_empty_tree(self):
        """Test creating empty tree."""
        tree = ContentTree()
        assert len(tree.nodes) == 0
        assert tree.root_title == "Root"

    def test_add_node(self):
        """Test adding nodes to tree."""
        tree = ContentTree()

        node = tree.add_node(
            title="TestNode",
            level=1,
            node_type="item",
        )

        assert node.title == "TestNode"
        assert len(tree.nodes) == 1
        assert "1" in tree.nodes

    def test_add_child_node(self):
        """Test adding child node."""
        tree = ContentTree()

        parent = tree.add_node(title="Parent", level=1)
        child = tree.add_node(
            title="Child",
            level=2,
            parent_id=parent.id,
        )

        assert child.parent_id == parent.id
        assert parent.id in tree.nodes[parent.id].children

    def test_mark_visited(self):
        """Test marking node as visited."""
        tree = ContentTree()
        node = tree.add_node(title="Test", level=1)

        assert not node.visited
        tree.mark_visited(node.id)
        assert tree.nodes[node.id].visited

    def test_to_markdown(self):
        """Test markdown export."""
        tree = ContentTree(root_title="MyApp")
        tree.add_node(title="Menu1", level=1)

        markdown = tree.to_markdown()
        assert "MyApp" in markdown
        assert "Menu1" in markdown


class TestTraversalState:
    """Test TraversalState model."""

    def test_empty_state(self):
        """Test creating empty state."""
        state = TraversalState()
        assert len(state.current_path) == 0
        assert len(state.visited) == 0
        assert len(state.all_level1_menus) == 0

    def test_get_current_cache_key(self):
        """Test cache key generation."""
        state = TraversalState(current_path=["L1", "L2"])
        key = state.get_current_cache_key()
        assert key == "L1|L2"

    def test_cache_key_short_path(self):
        """Test cache key with short path."""
        state = TraversalState(current_path=["L1"])
        key = state.get_current_cache_key()
        assert key == "root"

    def test_visited_tracking(self):
        """Test visited element tracking."""
        state = TraversalState()
        fp = VisitFingerprint(level1="L1", level2="L2", item_name="Item")

        assert not state.is_visited(fp)
        state.mark_visited(fp)
        assert state.is_visited(fp)

    def test_menu_caching(self):
        """Test menu caching."""
        state = TraversalState()

        menu = MenuInfo(name="Menu1", coordinate=Coordinate(x=0.1, y=0.1), active=True)
        state.add_level1_menu(menu)

        assert "Menu1" in state.all_level1_menus
        assert state.all_level1_menus["Menu1"] == menu

    def test_level2_caching(self):
        """Test level2 menu caching by level1."""
        state = TraversalState()

        menus = [
            MenuInfo(name="Tab1", coordinate=Coordinate(x=0.1, y=0.05), active=True),
            MenuInfo(name="Tab2", coordinate=Coordinate(x=0.3, y=0.05), active=False),
        ]
        state.add_level2_menus("Menu1", menus)

        retrieved = state.get_level2_menus("Menu1")
        assert len(retrieved) == 2
        assert retrieved[0].name == "Tab1"

    def test_items_caching(self):
        """Test items caching."""
        state = TraversalState()

        items = [
            MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3)),
        ]
        state.add_items("Menu1|Tab1", items)

        retrieved = state.get_items("Menu1|Tab1")
        assert len(retrieved) == 1
        assert retrieved[0].name == "Item1"


class TestStateManager:
    """Test StateManager persistence."""

    def test_new_state_on_missing_file(self, tmp_path):
        """Test creating new state when file doesn't exist."""
        state_file = tmp_path / "state.json"
        manager = StateManager(state_file)

        state = manager.load()
        assert isinstance(state, TraversalState)
        assert len(state.current_path) == 0

    def test_save_and_load(self, tmp_path):
        """Test saving and loading state."""
        state_file = tmp_path / "state.json"
        manager = StateManager(state_file)

        # Modify state
        manager.state.current_path = ["Menu1", "Tab1"]
        manager.state.target_app = "TestApp"
        manager.save()

        # Load new manager
        manager2 = StateManager(state_file)
        assert manager2.state.current_path == ["Menu1", "Tab1"]
        assert manager2.state.target_app == "TestApp"

    def test_reset(self, tmp_path):
        """Test resetting state."""
        state_file = tmp_path / "state.json"
        manager = StateManager(state_file)

        manager.state.current_path = ["Test"]
        manager.save()

        manager.reset()
        assert len(manager.state.current_path) == 0

    def test_update(self, tmp_path):
        """Test updating fields."""
        state_file = tmp_path / "state.json"
        manager = StateManager(state_file)

        manager.update(step_count=10, current_phase="running")

        assert manager.state.step_count == 10
        assert manager.state.current_phase == "running"


class TestMenuItemType:
    """Test extended MenuItemType enum."""

    def test_navigation_types(self):
        """Test navigation-related types."""
        assert MenuItemType.MENU_ITEM.value == "menu_item"
        assert MenuItemType.TAB.value == "tab"
        assert MenuItemType.BACK_BUTTON.value == "back_button"

    def test_action_types(self):
        """Test action-related types."""
        assert MenuItemType.SWITCH.value == "switch"
        assert MenuItemType.TOGGLE.value == "toggle"
        assert MenuItemType.BUTTON.value == "button"

    def test_other_types(self):
        """Test other element types."""
        assert MenuItemType.ICON.value == "icon"
        assert MenuItemType.LINK.value == "link"
        assert MenuItemType.TEXT.value == "text"
        assert MenuItemType.READONLY.value == "readonly"

    def test_legacy_compatibility(self):
        """Test legacy ITEM type is still available."""
        assert MenuItemType.ITEM.value == "item"
        assert MenuItemType.ITEM == "item"


class TestExpectedAction:
    """Test ExpectedAction enum."""

    def test_navigate_action(self):
        """Test NAVIGATE action."""
        assert ExpectedAction.NAVIGATE.value == "navigate"

    def test_toggle_action(self):
        """Test TOGGLE action."""
        assert ExpectedAction.TOGGLE.value == "toggle"

    def test_action(self):
        """Test ACTION action."""
        assert ExpectedAction.ACTION.value == "action"

    def test_none_action(self):
        """Test NONE action."""
        assert ExpectedAction.NONE.value == "none"

    def test_all_actions(self):
        """Test all expected action values."""
        actions = [ExpectedAction.NAVIGATE, ExpectedAction.TOGGLE,
                   ExpectedAction.ACTION, ExpectedAction.NONE]
        values = [a.value for a in actions]
        assert "navigate" in values
        assert "toggle" in values
        assert "action" in values
        assert "none" in values


class TestExtendedMenuItem:
    """Test MenuItem with new behavior fields."""

    def test_menu_item_with_defaults(self):
        """Test MenuItem uses default values for new fields."""
        item = MenuItem(
            name="TestItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
        )

        # New fields should have defaults
        assert item.expected_action == ExpectedAction.ACTION
        assert item.expects_page_change is False
        assert item.expects_state_change is False

    def test_menu_item_with_navigate_action(self):
        """Test MenuItem with NAVIGATE action."""
        item = MenuItem(
            name="MenuItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NAVIGATE,
            expects_page_change=True,
            expects_state_change=False,
        )

        assert item.expected_action == ExpectedAction.NAVIGATE
        assert item.expects_page_change is True
        assert item.expects_state_change is False

    def test_menu_item_with_toggle_action(self):
        """Test MenuItem with TOGGLE action."""
        item = MenuItem(
            name="Switch",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            expected_action=ExpectedAction.TOGGLE,
            expects_page_change=False,
            expects_state_change=True,
        )

        assert item.expected_action == ExpectedAction.TOGGLE
        assert item.expects_page_change is False
        assert item.expects_state_change is True

    def test_menu_item_with_none_action(self):
        """Test MenuItem with NONE action (read-only)."""
        item = MenuItem(
            name="ReadOnlyText",
            type=MenuItemType.READONLY,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NONE,
            expects_page_change=False,
            expects_state_change=False,
        )

        assert item.expected_action == ExpectedAction.NONE
        assert item.type == MenuItemType.READONLY

    def test_menu_item_serialization(self):
        """Test MenuItem can be serialized/deserialized with new fields."""
        item = MenuItem(
            name="TestItem",
            type=MenuItemType.BUTTON,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.ACTION,
            expects_page_change=True,
            expects_state_change=False,
        )

        # Serialize to dict
        item_dict = item.model_dump()

        # Check new fields are included
        assert "expected_action" in item_dict
        assert "expects_page_change" in item_dict
        assert "expects_state_change" in item_dict

        # Deserialize
        restored = MenuItem(**item_dict)
        assert restored.name == item.name
        assert restored.expected_action == item.expected_action
        assert restored.expects_page_change == item.expects_page_change

    def test_menu_item_backward_compatibility(self):
        """Test loading old MenuItem data without new fields."""
        # Old format (without new fields)
        old_data = {
            "name": "OldItem",
            "type": "item",
            "coordinate": {"x": 0.5, "y": 0.5},
            "parent": None,
            "description": None,
        }

        # Should load with defaults
        item = MenuItem(**old_data)
        assert item.name == "OldItem"
        assert item.expected_action == ExpectedAction.ACTION  # Default
        assert item.expects_page_change is False  # Default
        assert item.expects_state_change is False  # Default


class TestBackwardCompatibility:
    """Test backward compatibility with old state files."""

    def test_load_old_state_file_without_new_fields(self, tmp_path):
        """Test loading state file created before button type enhancement."""
        import json

        # Create an old-style state file (without new MenuItem fields)
        state_file = tmp_path / "old_state.json"
        old_state_data = {
            "current_path": ["Menu1", "Tab1"],
            "visited": ["Menu1|Tab1|Item1"],
            "all_level1_menus": {
                "Menu1": {"name": "Menu1", "coordinate": {"x": 0.1, "y": 0.1}, "active": True}
            },
            "level2_menus_cache": {
                "Menu1": [
                    {"name": "Tab1", "coordinate": {"x": 0.1, "y": 0.05}, "active": True}
                ]
            },
            "items_cache": {
                "Menu1|Tab1": [
                    {
                        "name": "Item1",
                        "type": "item",
                        "coordinate": {"x": 0.5, "y": 0.3},
                        "parent": None,
                    }
                ]
            },
            "content_tree": {
                "root_title": "Root",
                "nodes": {},
                "_level_counters": {},
            },
            "step_count": 0,
            "current_phase": "initialized",
            "consecutive_errors": 0,
            "last_error": None,
            "target_app": None,
        }

        with open(state_file, "w") as f:
            json.dump(old_state_data, f)

        # Load the old state file
        manager = StateManager(state_file)
        state = manager.load()

        # Should load successfully
        assert state.current_path == ["Menu1", "Tab1"]

        # Items should have new fields with defaults
        items = state.get_items("Menu1|Tab1")
        assert len(items) == 1
        assert items[0].name == "Item1"
        assert items[0].expected_action == ExpectedAction.ACTION  # Default
        assert items[0].expects_page_change is False
        assert items[0].expects_state_change is False

    def test_save_and_load_new_state_file(self, tmp_path):
        """Test saving and loading state with new fields."""
        state_file = tmp_path / "new_state.json"
        manager = StateManager(state_file)

        # Create items with new fields
        items = [
            MenuItem(
                name="EnhancedItem",
                type=MenuItemType.MENU_ITEM,
                coordinate=Coordinate(x=0.5, y=0.3),
                expected_action=ExpectedAction.NAVIGATE,
                expects_page_change=True,
                expects_state_change=False,
            )
        ]

        manager.state.current_path = ["Menu1", "Tab1"]
        manager.state.add_items("Menu1|Tab1", items)
        manager.save()

        # Load in a new manager
        manager2 = StateManager(state_file)
        loaded_state = manager2.load()

        # New fields should be preserved
        loaded_items = loaded_state.get_items("Menu1|Tab1")
        assert len(loaded_items) == 1
        assert loaded_items[0].name == "EnhancedItem"
        assert loaded_items[0].expected_action == ExpectedAction.NAVIGATE
        assert loaded_items[0].expects_page_change is True
        assert loaded_items[0].expects_state_change is False

    def test_json_serialization_roundtrip(self, tmp_path):
        """Test JSON serialization and deserialization roundtrip."""
        state_file = tmp_path / "roundtrip_state.json"
        manager = StateManager(state_file)

        # Create state with various button types
        items = [
            MenuItem(
                name="MenuItem",
                type=MenuItemType.MENU_ITEM,
                coordinate=Coordinate(x=0.5, y=0.3),
                expected_action=ExpectedAction.NAVIGATE,
                expects_page_change=True,
            ),
            MenuItem(
                name="Switch",
                type=MenuItemType.SWITCH,
                coordinate=Coordinate(x=0.8, y=0.5),
                expected_action=ExpectedAction.TOGGLE,
                expects_state_change=True,
            ),
            MenuItem(
                name="ReadOnly",
                type=MenuItemType.READONLY,
                coordinate=Coordinate(x=0.5, y=0.7),
                expected_action=ExpectedAction.NONE,
            ),
        ]

        manager.state.current_path = ["Menu1", "Tab1"]
        manager.state.add_items("Menu1|Tab1", items)
        manager.save()

        # Load and verify
        manager2 = StateManager(state_file)
        loaded_state = manager2.load()
        loaded_items = loaded_state.get_items("Menu1|Tab1")

        assert len(loaded_items) == 3

        # Verify each item preserved its properties
        assert loaded_items[0].expected_action == ExpectedAction.NAVIGATE
        assert loaded_items[0].expects_page_change is True

        assert loaded_items[1].expected_action == ExpectedAction.TOGGLE
        assert loaded_items[1].expects_state_change is True

        assert loaded_items[2].expected_action == ExpectedAction.NONE
        assert loaded_items[2].type == MenuItemType.READONLY
