"""Tests for content tree models.

This module tests the models from src/state/content_tree.py including:
- Coordinate
- MenuInfo
- MenuItem
- PopupInfo
- PageAnalysis
- ContentNode
- ContentTree
- VisitFingerprint
- TraversalState (persistence model)
"""

import pytest
from src.state.content_tree import (
    Coordinate,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    PopupInfo,
    PageAnalysis,
    Direction,
    ContentNode,
    ContentTree,
    VisitFingerprint,
    TraversalState,
)


class TestCoordinate:
    """Tests for Coordinate model."""

    def test_coordinate_creation(self):
        """Test creating valid coordinates."""
        coord = Coordinate(x=0.5, y=0.5)
        assert coord.x == 0.5
        assert coord.y == 0.5

    def test_coordinate_validation(self):
        """Test coordinate validation (0-1 range)."""
        # Valid range
        Coordinate(x=0.0, y=0.0)
        Coordinate(x=1.0, y=1.0)
        Coordinate(x=0.5, y=0.5)

        # Invalid - should raise validation error
        with pytest.raises(Exception):
            Coordinate(x=1.5, y=0.5)
        with pytest.raises(Exception):
            Coordinate(x=0.5, y=-0.1)

    def test_coordinate_serialization(self):
        """Test coordinate serialization."""
        coord = Coordinate(x=0.5, y=0.25)
        data = coord.model_dump()
        assert data["x"] == 0.5
        assert data["y"] == 0.25


class TestMenuInfo:
    """Tests for MenuInfo model."""

    def test_menu_info_creation(self):
        """Test creating menu info."""
        info = MenuInfo(
            name="TestMenu",
            coordinate=Coordinate(x=0.1, y=0.2),
            active=True,
        )
        assert info.name == "TestMenu"
        assert info.active is True

    def test_menu_info_defaults(self):
        """Test MenuInfo default values."""
        info = MenuInfo(
            name="Test",
            coordinate=Coordinate(x=0.5, y=0.5),
        )
        assert info.active is False


class TestMenuItem:
    """Tests for MenuItem model."""

    def test_menu_item_creation(self):
        """Test creating menu item."""
        item = MenuItem(
            name="TestItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
        )
        assert item.name == "TestItem"
        assert item.parent is None

    def test_menu_item_with_parent(self):
        """Test menu item with parent reference."""
        item = MenuItem(
            name="ChildItem",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            parent="ParentItem",
        )
        assert item.parent == "ParentItem"

    def test_get_fingerprint(self):
        """Test fingerprint generation."""
        item = MenuItem(
            name="MyItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
        )
        fp = item.get_fingerprint("Level1", "Level2")
        assert fp == "Level1|Level2|MyItem"

    def test_menu_item_with_expected_action(self):
        """Test MenuItem with ExpectedAction fields."""
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


class TestExpectedAction:
    """Tests for ExpectedAction enum."""

    def test_expected_action_values(self):
        """Test ExpectedAction has correct values."""
        assert ExpectedAction.NAVIGATE.value == "navigate"
        assert ExpectedAction.TOGGLE.value == "toggle"
        assert ExpectedAction.ACTION.value == "action"
        assert ExpectedAction.NONE.value == "none"


class TestMenuItemType:
    """Tests for MenuItemType enum."""

    def test_menu_item_type_values(self):
        """Test MenuItemType has correct values."""
        assert MenuItemType.MENU_ITEM.value == "menu_item"
        assert MenuItemType.TAB.value == "tab"
        assert MenuItemType.SWITCH.value == "switch"
        assert MenuItemType.BUTTON.value == "button"

    def test_legacy_compatibility(self):
        """Test legacy ITEM type is still available."""
        assert MenuItemType.ITEM.value == "item"
        assert MenuItemType.ITEM == "item"


class TestPopupInfo:
    """Tests for PopupInfo model."""

    def test_popup_info_creation(self):
        """Test creating popup info."""
        popup = PopupInfo(
            title="Confirm",
            content="Are you sure?",
            close_button=Coordinate(x=0.7, y=0.5),
        )
        assert popup.title == "Confirm"
        assert popup.content == "Are you sure?"

    def test_popup_info_optional_fields(self):
        """Test PopupInfo with optional fields."""
        popup = PopupInfo()
        assert popup.title is None
        assert popup.content is None


class TestPageAnalysis:
    """Tests for PageAnalysis model."""

    def test_page_analysis_creation(self):
        """Test creating page analysis."""
        analysis = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="Menu1", coordinate=Coordinate(x=0.1, y=0.5)),
            ],
            level2_dir=Direction.TOP,
            level2_menus=[
                MenuInfo(name="Tab1", coordinate=Coordinate(x=0.3, y=0.1), active=True),
            ],
            current_path=["Menu1", "Tab1"],
            items=[
                MenuItem(
                    name="Item1",
                    type=MenuItemType.MENU_ITEM,
                    coordinate=Coordinate(x=0.5, y=0.3),
                )
            ],
        )
        assert analysis.level1_dir == Direction.LEFT
        assert analysis.level2_dir == Direction.TOP
        assert len(analysis.items) == 1

    def test_page_analysis_with_popup(self):
        """Test PageAnalysis with popup info."""
        analysis = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.BOTTOM,
            level2_menus=[],
            current_path=["Settings"],
            items=[],
            is_popup=True,
            popup_info=PopupInfo(
                title="Confirm Delete",
                content="Delete this item?",
            ),
        )
        assert analysis.is_popup is True
        assert analysis.popup_info.title == "Confirm Delete"


class TestContentNode:
    """Tests for ContentNode model."""

    def test_content_node_creation(self):
        """Test creating content node."""
        node = ContentNode(
            id="1",
            title="TestNode",
            level=1,
        )
        assert node.id == "1"
        assert node.title == "TestNode"
        assert node.level == 1
        assert node.visited is False

    def test_content_node_with_parent(self):
        """Test content node with parent."""
        parent = ContentNode(id="1", title="Parent", level=1)
        child = ContentNode(
            id="2",
            title="Child",
            level=2,
            parent_id="1",
        )
        assert child.parent_id == "1"

    def test_to_markdown(self):
        """Test markdown conversion."""
        node = ContentNode(
            id="1",
            title="Test Node",
            level=2,
            node_type="container",
        )
        markdown = node.to_markdown()
        assert "Test Node" in markdown
        assert "container" in markdown


class TestContentTree:
    """Tests for ContentTree model."""

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
        assert tree.nodes[node.id].visited is True

    def test_to_markdown(self):
        """Test markdown export."""
        tree = ContentTree(root_title="MyApp")
        tree.add_node(title="Menu1", level=1)

        markdown = tree.to_markdown()
        assert "MyApp" in markdown
        assert "Menu1" in markdown


class TestVisitFingerprint:
    """Tests for VisitFingerprint model."""

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


class TestTraversalState:
    """Tests for TraversalState (persistence) model."""

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
            MenuItem(name="Item1", type=MenuItemType.MENU_ITEM, coordinate=Coordinate(x=0.5, y=0.3)),
        ]
        state.add_items("Menu1|Tab1", items)

        retrieved = state.get_items("Menu1|Tab1")
        assert len(retrieved) == 1
        assert retrieved[0].name == "Item1"
