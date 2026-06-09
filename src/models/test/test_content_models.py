"""Tests for content models migrated from src.state.content_tree."""

import json
import pytest

from src.models.content_models import (
    Coordinate,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    PageAnalysis,
    PopupInfo,
    ContentTree,
    ContentNode,
    VisitFingerprint,
    SimulationState,
)


# ============================================================================
# Coordinate Tests
# ============================================================================

class TestCoordinate:
    """Tests for Coordinate model."""

    def test_valid_coordinate_creation(self):
        """Test creating a valid coordinate."""
        coord = Coordinate(x=0.5, y=0.5)
        assert coord.x == 0.5
        assert coord.y == 0.5

    def test_coordinate_boundary_values(self):
        """Test coordinates at boundary values."""
        coord_min = Coordinate(x=0.0, y=0.0)
        assert coord_min.x == 0.0
        assert coord_min.y == 0.0

        coord_max = Coordinate(x=1.0, y=1.0)
        assert coord_max.x == 1.0
        assert coord_max.y == 1.0

    def test_invalid_coordinate_rejection(self):
        """Test that invalid coordinates are rejected."""
        with pytest.raises(ValueError):
            Coordinate(x=-0.1, y=0.5)

        with pytest.raises(ValueError):
            Coordinate(x=0.5, y=1.1)


# ============================================================================
# Direction Tests
# ============================================================================

class TestDirection:
    """Tests for Direction enum."""

    def test_enum_values(self):
        """Test enum value access."""
        assert Direction.LEFT.value == "left"
        assert Direction.RIGHT.value == "right"
        assert Direction.TOP.value == "top"
        assert Direction.BOTTOM.value == "bottom"

    def test_values_method(self):
        """Test values() class method."""
        values = Direction.values()
        assert values == ["left", "right", "top", "bottom"]

    def test_from_value_method(self):
        """Test from_value() class method."""
        assert Direction.from_value("left") == Direction.LEFT
        assert Direction.from_value("right") == Direction.RIGHT

    def test_from_value_invalid(self):
        """Test from_value() with invalid value."""
        with pytest.raises(ValueError, match="Invalid Direction"):
            Direction.from_value("up")

    def test_is_valid_method(self):
        """Test is_valid() class method."""
        assert Direction.is_valid("left") is True
        assert Direction.is_valid("up") is False


# ============================================================================
# MenuInfo Tests
# ============================================================================

class TestMenuInfo:
    """Tests for MenuInfo model."""

    def test_menu_info_creation(self):
        """Test creating MenuInfo."""
        coord = Coordinate(x=0.5, y=0.5)
        menu = MenuInfo(name="Settings", coordinate=coord)
        assert menu.name == "Settings"
        assert menu.coordinate.x == 0.5
        assert menu.active is False

    def test_menu_info_with_active(self):
        """Test MenuInfo with active=True."""
        coord = Coordinate(x=0.5, y=0.5)
        menu = MenuInfo(name="Settings", coordinate=coord, active=True)
        assert menu.active is True


# ============================================================================
# MenuItemType Tests
# ============================================================================

class TestMenuItemType:
    """Tests for MenuItemType enum."""

    def test_enum_values(self):
        """Test enum value access."""
        assert MenuItemType.MENU_ITEM.value == "menu_item"
        assert MenuItemType.BUTTON.value == "button"

    def test_values_method(self):
        """Test values() class method."""
        values = MenuItemType.values()
        assert "menu_item" in values
        assert "button" in values
        assert "switch" in values

    def test_from_value_method(self):
        """Test from_value() class method."""
        assert MenuItemType.from_value("menu_item") == MenuItemType.MENU_ITEM
        assert MenuItemType.from_value("button") == MenuItemType.BUTTON


# ============================================================================
# ExpectedAction Tests
# ============================================================================

class TestExpectedAction:
    """Tests for ExpectedAction enum."""

    def test_enum_values(self):
        """Test enum value access."""
        assert ExpectedAction.NAVIGATE.value == "navigate"
        assert ExpectedAction.TOGGLE.value == "toggle"
        assert ExpectedAction.ACTION.value == "action"
        assert ExpectedAction.NONE.value == "none"

    def test_values_method(self):
        """Test values() class method."""
        values = ExpectedAction.values()
        assert values == ["navigate", "toggle", "action", "none"]


# ============================================================================
# MenuItem Tests
# ============================================================================

class TestMenuItem:
    """Tests for MenuItem model."""

    def test_menu_item_creation(self):
        """Test creating MenuItem."""
        coord = Coordinate(x=0.5, y=0.5)
        item = MenuItem(
            name="Save",
            type=MenuItemType.BUTTON,
            coordinate=coord
        )
        assert item.name == "Save"
        assert item.type == MenuItemType.BUTTON
        assert item.coordinate.x == 0.5
        assert item.expected_action == ExpectedAction.ACTION
        assert item.expects_page_change is False

    def test_get_fingerprint(self):
        """Test get_fingerprint() method."""
        coord = Coordinate(x=0.5, y=0.5)
        item = MenuItem(name="Save", coordinate=coord)
        fingerprint = item.get_fingerprint("Settings", "General")
        assert fingerprint == "Settings|General|Save"

    def test_serialization(self):
        """Test MenuItem serialization/deserialization."""
        coord = Coordinate(x=0.5, y=0.5)
        item = MenuItem(
            name="Save",
            type=MenuItemType.BUTTON,
            coordinate=coord,
            expected_action=ExpectedAction.NAVIGATE
        )
        # Serialize and deserialize
        item_dict = item.model_dump()
        item2 = MenuItem(**item_dict)
        assert item2.name == "Save"
        assert item2.expected_action == ExpectedAction.NAVIGATE


# ============================================================================
# PageAnalysis Tests
# ============================================================================

class TestPageAnalysis:
    """Tests for PageAnalysis model."""

    def test_page_analysis_creation(self):
        """Test creating PageAnalysis."""
        coord = Coordinate(x=0.5, y=0.5)
        menu = MenuInfo(name="Settings", coordinate=coord)
        page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[menu],
            level2_dir=Direction.RIGHT,
            level2_menus=[],
            current_path=["Settings"],
            items=[]
        )
        assert page.level1_dir == Direction.LEFT
        assert len(page.level1_menus) == 1
        assert page.current_path == ["Settings"]

    def test_page_analysis_with_popup(self):
        """Test PageAnalysis with popup info."""
        coord = Coordinate(x=0.5, y=0.5)
        page = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.RIGHT,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=True,
            popup_info=PopupInfo(title="Alert", content="Confirm action")
        )
        assert page.is_popup is True
        assert page.popup_info.title == "Alert"


# ============================================================================
# ContentTree Tests
# ============================================================================

class TestContentTree:
    """Tests for ContentTree model."""

    def test_add_node(self):
        """Test adding a node to the tree."""
        tree = ContentTree()
        node = tree.add_node("Settings", 1)
        assert node.id == "1"
        assert node.title == "Settings"
        assert node.level == 1
        assert "1" in tree.nodes

    def test_add_child_node(self):
        """Test adding a child node."""
        tree = ContentTree()
        parent = tree.add_node("Settings", 1)
        child = tree.add_child_node("General", parent_id="1")
        assert child.id == "1.1"
        assert child.title == "General"
        assert child.level == 2
        assert child.parent_id == "1"
        assert "1.1" in parent.children

    def test_mark_visited(self):
        """Test marking a node as visited."""
        tree = ContentTree()
        node = tree.add_node("Settings", 1)
        tree.mark_visited("1")
        assert tree.nodes["1"].visited is True

    def test_get_unvisited_children(self):
        """Test getting unvisited children."""
        tree = ContentTree()
        parent = tree.add_node("Settings", 1)
        child1 = tree.add_child_node("General", parent_id="1")
        child2 = tree.add_child_node("Advanced", parent_id="1")
        tree.mark_visited(child1.id)

        unvisited = tree.get_unvisited_children("1")
        assert len(unvisited) == 1
        assert unvisited[0].id == "1.2"

    def test_to_markdown(self):
        """Test exporting tree to markdown."""
        tree = ContentTree()
        tree.add_node("Settings", 1)
        tree.add_node("Home", 1)
        parent = tree.add_node("Profile", 1)
        tree.add_child_node("Edit", parent_id="3")

        markdown = tree.to_markdown()
        assert "0. Root" in markdown
        assert "1. Settings" in markdown
        assert "3. Profile" in markdown
        assert "3.1. Edit" in markdown


# ============================================================================
# VisitFingerprint Tests
# ============================================================================

class TestVisitFingerprint:
    """Tests for VisitFingerprint model."""

    def test_fingerprint_creation(self):
        """Test creating VisitFingerprint."""
        fp = VisitFingerprint(level1="Settings", level2="General", item_name="Save")
        assert fp.level1 == "Settings"
        assert fp.level2 == "General"
        assert fp.item_name == "Save"

    def test_fingerprint_string_representation(self):
        """Test __str__ method."""
        fp = VisitFingerprint(level1="Settings", level2="General", item_name="Save")
        assert str(fp) == "Settings|General|Save"

    def test_fingerprint_from_string(self):
        """Test from_string() class method."""
        fp = VisitFingerprint.from_string("Settings|General|Save")
        assert fp.level1 == "Settings"
        assert fp.level2 == "General"
        assert fp.item_name == "Save"

    def test_fingerprint_from_string_invalid(self):
        """Test from_string() with invalid format."""
        with pytest.raises(ValueError, match="Invalid fingerprint format"):
            VisitFingerprint.from_string("InvalidFormat")


# ============================================================================
# SimulationState Tests
# ============================================================================

class TestSimulationState:
    """Tests for SimulationState model."""

    def test_state_creation(self):
        """Test creating SimulationState."""
        state = SimulationState()
        assert state.current_path == []
        assert state.visited == set()
        assert state.current_phase == "initialized"
        assert state.step_count == 0
        assert state.consecutive_errors == 0

    def test_get_current_cache_key(self):
        """Test get_current_cache_key() method."""
        state = SimulationState(current_path=["Settings", "General"])
        assert state.get_current_cache_key() == "Settings|General"

    def test_get_current_cache_key_root(self):
        """Test get_current_cache_key() at root."""
        state = SimulationState(current_path=["Settings"])
        assert state.get_current_cache_key() == "root"

    def test_is_visited(self):
        """Test is_visited() method."""
        state = SimulationState()
        state.mark_visited("Settings|General|Save")
        assert state.is_visited("Settings|General|Save") is True
        assert state.is_visited("Other|Path") is False

    def test_mark_visited(self):
        """Test mark_visited() method."""
        state = SimulationState()
        state.mark_visited("Settings|General|Save")
        assert "Settings|General|Save" in state.visited

    def test_add_level1_menu(self):
        """Test add_level1_menu() method."""
        state = SimulationState()
        coord = Coordinate(x=0.5, y=0.5)
        menu = MenuInfo(name="Settings", coordinate=coord)
        state.add_level1_menu(menu)
        assert "Settings" in state.all_level1_menus
        assert state.all_level1_menus["Settings"].name == "Settings"

    def test_add_level2_menus(self):
        """Test add_level2_menus() method."""
        state = SimulationState()
        coord = Coordinate(x=0.5, y=0.5)
        menus = [
            MenuInfo(name="General", coordinate=coord),
            MenuInfo(name="Advanced", coordinate=coord),
        ]
        state.add_level2_menus("Settings", menus)
        assert "Settings" in state.level2_menus_cache
        assert len(state.level2_menus_cache["Settings"]) == 2

    def test_get_level2_menus(self):
        """Test get_level2_menus() method."""
        state = SimulationState()
        coord = Coordinate(x=0.5, y=0.5)
        menus = [MenuInfo(name="General", coordinate=coord)]
        state.add_level2_menus("Settings", menus)

        retrieved = state.get_level2_menus("Settings")
        assert len(retrieved) == 1
        assert retrieved[0].name == "General"

    def test_get_level2_menus_empty(self):
        """Test get_level2_menus() with no cached menus."""
        state = SimulationState()
        retrieved = state.get_level2_menus("Nonexistent")
        assert retrieved == []

    def test_add_items(self):
        """Test add_items() method."""
        state = SimulationState()
        coord = Coordinate(x=0.5, y=0.5)
        items = [
            MenuItem(name="Save", coordinate=coord),
            MenuItem(name="Cancel", coordinate=coord),
        ]
        state.add_items("Settings|General", items)
        assert "Settings|General" in state.items_cache

    def test_get_items(self):
        """Test get_items() method."""
        state = SimulationState()
        coord = Coordinate(x=0.5, y=0.5)
        items = [MenuItem(name="Save", coordinate=coord)]
        state.add_items("Settings|General", items)

        retrieved = state.get_items("Settings|General")
        assert len(retrieved) == 1
        assert retrieved[0].name == "Save"

    def test_exception_history_summary(self):
        """Test get_exception_history_summary() method."""
        state = SimulationState(
            exception_history_records=[
                {"exception_type": "ValidationError", "severity": "high"},
                {"exception_type": "ValidationError", "severity": "medium"},
                {"exception_type": "TimeoutError", "severity": "low"},
            ]
        )
        summary = state.get_exception_history_summary()
        assert summary["total"] == 3
        assert summary["by_type"]["ValidationError"] == 2
        assert summary["by_type"]["TimeoutError"] == 1

    def test_get_exceptions_by_type(self):
        """Test get_exceptions_by_type() method."""
        state = SimulationState(
            exception_history_records=[
                {"exception_type": "ValidationError", "severity": "high"},
                {"exception_type": "TimeoutError", "severity": "low"},
            ]
        )
        validation_errors = state.get_exceptions_by_type("ValidationError")
        assert len(validation_errors) == 1
        assert validation_errors[0]["exception_type"] == "ValidationError"

    def test_get_exceptions_by_severity(self):
        """Test get_exceptions_by_severity() method."""
        state = SimulationState(
            exception_history_records=[
                {"exception_type": "ValidationError", "severity": "high"},
                {"exception_type": "TimeoutError", "severity": "high"},
                {"exception_type": "Warning", "severity": "low"},
            ]
        )
        high_severity = state.get_exceptions_by_severity("high")
        assert len(high_severity) == 2

    def test_json_serialization_with_aliases(self):
        """Test JSON serialization uses aliases."""
        state = SimulationState(
            exception_history_records=[{"type": "test"}],
            node_stack=[{"node": "test"}]
        )
        # Serialize with by_alias=True to use aliases
        state_dict = json.loads(state.model_dump_json(by_alias=True))
        assert "_exception_history_records" in state_dict
        assert "_node_stack" in state_dict

    def test_json_serialization_without_aliases(self):
        """Test JSON serialization without aliases uses field names."""
        state = SimulationState(
            exception_history_records=[{"type": "test"}],
            node_stack=[{"node": "test"}]
        )
        # Serialize without by_alias to use field names
        state_dict = json.loads(state.model_dump_json(by_alias=False))
        assert "exception_history_records" in state_dict
        assert "node_stack" in state_dict

    def test_instantiation_with_field_names(self):
        """Test instantiation with field names."""
        state = SimulationState(
            exception_history_records=[{"type": "test"}],
            node_stack=[{"node": "test"}]
        )
        assert len(state.exception_history_records) == 1
        assert len(state.node_stack) == 1

    def test_instantiation_with_aliases(self):
        """Test instantiation with alias names."""
        state = SimulationState(
            _exception_history_records=[{"type": "test"}],
            _node_stack=[{"node": "test"}]
        )
        assert len(state.exception_history_records) == 1
        assert len(state.node_stack) == 1
