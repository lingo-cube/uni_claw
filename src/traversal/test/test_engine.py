"""Tests for traversal engine."""

from unittest.mock import MagicMock, patch

import pytest

from src.adb.adb_client import MockADBClient
from src.state.content_tree import (
    Coordinate,
    Direction,
    ExpectedAction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    PageAnalysis,
    TraversalState,
    VisitFingerprint,
)
from src.traversal import ClickResult, TraversalConfig, TraversalEngine, TraversalEvent
from src.vision.vision_service import MockVisionService


class TestClickResult:
    """Test ClickResult enum."""

    def test_values(self):
        assert ClickResult.NO_CHANGE.value == "no_change"
        assert ClickResult.POPUP.value == "popup"
        assert ClickResult.PAGE_JUMP.value == "page_jump"
        assert ClickResult.NORMAL.value == "normal"
        assert ClickResult.NO_FEEDBACK.value == "no_feedback"
        assert ClickResult.ERROR.value == "error"

    def test_is_str_enum(self):
        assert isinstance(ClickResult.NO_CHANGE, str)
        assert ClickResult.NO_CHANGE == "no_change"


class TestTraversalConfig:
    """Test TraversalConfig."""

    def test_default_config(self):
        """Test default configuration values."""
        config = TraversalConfig()
        assert config.max_steps == 200
        assert config.wait_time == 0.5
        assert config.max_retries == 2

    def test_custom_config(self):
        """Test custom configuration."""
        config = TraversalConfig(max_steps=100, wait_time=1.0)
        assert config.max_steps == 100
        assert config.wait_time == 1.0


class TestTraversalEvent:
    """Test TraversalEvent."""

    def test_event_creation(self):
        """Test creating event."""
        event = TraversalEvent(
            event_type="test_event",
            step=5,
            data={"key": "value"},
        )
        assert event.event_type == "test_event"
        assert event.step == 5
        assert event.data == {"key": "value"}

    def test_event_string(self):
        """Test event string representation."""
        event = TraversalEvent(
            event_type="click",
            step=1,
            data={"item": "test"},
        )
        str_repr = str(event)
        assert "click" in str_repr
        assert "1" in str_repr


class TestTraversalEngine:
    """Test TraversalEngine."""

    def setup_method(self):
        """Set up test fixtures."""
        self.adb = MockADBClient()
        self.vision = MockVisionService()
        self.state = TraversalState()
        self.config = TraversalConfig(max_steps=10)
        self.events = []

        def capture_event(event):
            self.events.append(event)

        self.engine = TraversalEngine(
            adb_client=self.adb,
            vision_service=self.vision,
            state=self.state,
            config=self.config,
            event_callback=capture_event,
        )

    def test_initialization(self):
        """Test engine initialization."""
        assert self.engine.adb == self.adb
        assert self.engine.vision == self.vision
        assert self.engine.state == self.state
        assert self.engine._step == 0

    def test_capture_and_analyze(self):
        """Test screenshot and analysis."""
        analysis = self.engine._capture_and_analyze()

        assert isinstance(analysis, PageAnalysis)
        # Should emit event
        assert any(e.event_type == "page_analyzed" for e in self.events)

    def test_tap_and_wait(self):
        """Test tap with wait."""
        coord = Coordinate(x=0.5, y=0.5)
        self.engine._tap_and_wait(coord)

        # Should have logged tap
        assert any("tap" in cmd for cmd in self.adb.command_log)

    def test_navigate_to_app_success(self):
        """Test successful app navigation."""
        result = self.engine.navigate_to_app("TestApp")

        assert result is True
        assert "tap" in self.adb.command_log[-1]

        # Should emit events
        assert any(e.event_type == "navigate_start" for e in self.events)
        assert any(e.event_type == "navigate_success" for e in self.events)

    def test_navigate_to_app_not_found(self):
        """Test navigation when app not found."""
        # Make vision service return None
        self.vision.find_app_entry = lambda *_: None

        result = self.engine.navigate_to_app("MissingApp")

        assert result is False
        assert any(e.event_type == "navigate_failed" for e in self.events)

    def test_initialize_structure(self):
        """Test structure initialization."""
        result = self.engine.initialize_structure()

        assert result is True
        assert len(self.state.all_level1_menus) > 0
        assert len(self.state.current_path) > 0

        # Should emit events
        assert any(e.event_type == "initialize_start" for e in self.events)
        assert any(e.event_type == "initialize_complete" for e in self.events)

    def test_select_next_item_unvisited(self):
        """Test selecting next unvisited item."""
        # Add some items to state
        self.state.current_path = ["Menu1", "Tab1"]
        items = [
            MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3)),
            MenuItem(name="Item2", type="item", coordinate=Coordinate(x=0.5, y=0.5)),
        ]
        self.state.add_items("Menu1|Tab1", items)

        # First call should return Item1
        item = self.engine._select_next_item()
        assert item is not None
        assert item.name == "Item1"

        # After marking visited, should return Item2
        self.state.mark_visited(VisitFingerprint(level1="Menu1", level2="Tab1", item_name="Item1"))
        item = self.engine._select_next_item()
        assert item.name == "Item2"

    def test_select_next_item_all_visited(self):
        """Test selecting item when all visited."""
        self.state.current_path = ["Menu1", "Tab1"]
        items = [
            MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3)),
        ]
        self.state.add_items("Menu1|Tab1", items)

        # Mark all as visited
        self.state.mark_visited(VisitFingerprint(level1="Menu1", level2="Tab1", item_name="Item1"))

        item = self.engine._select_next_item()
        assert item is None

    def test_click_item_normal(self):
        """Test clicking item with normal result."""
        # Set up state
        self.state.current_path = ["Menu1", "Tab1"]
        item = MenuItem(name="TestItem", type="item", coordinate=Coordinate(x=0.5, y=0.5))

        # Mock vision to return same analysis (no change)
        same_analysis = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )
        self.vision.add_response(same_analysis)
        self.vision.add_response(same_analysis)

        result = self.engine._click_item(item)

        # Should have tapped
        assert any("tap" in cmd for cmd in self.adb.command_log)

    def test_switch_to_next_level2(self):
        """Test switching to next level2 tab."""
        # Set up state with level2 menus
        self.state.current_path = ["Menu1", "Tab1"]
        menus = [
            MenuInfo(name="Tab1", coordinate=Coordinate(x=0.1, y=0.05), active=True),
            MenuInfo(name="Tab2", coordinate=Coordinate(x=0.3, y=0.05), active=False),
        ]
        self.state.add_level2_menus("Menu1", menus)

        result = self.engine._switch_to_next_level2()

        assert result is True
        assert self.state.current_path == ["Menu1", "Tab2"]
        # Should have tapped Tab2
        assert any("tap" in cmd for cmd in self.adb.command_log)

    def test_switch_to_next_level2_last_tab(self):
        """Test switching when on last tab."""
        self.state.current_path = ["Menu1", "Tab2"]
        menus = [
            MenuInfo(name="Tab1", coordinate=Coordinate(x=0.1, y=0.05), active=False),
            MenuInfo(name="Tab2", coordinate=Coordinate(x=0.3, y=0.05), active=True),
        ]
        self.state.add_level2_menus("Menu1", menus)

        result = self.engine._switch_to_next_level2()

        assert result is False

    def test_switch_to_next_level1(self):
        """Test switching to next level1 menu."""
        # Set up state
        self.state.add_level1_menu(
            MenuInfo(name="Menu1", coordinate=Coordinate(x=0.1, y=0.1), active=True)
        )
        self.state.add_level1_menu(
            MenuInfo(name="Menu2", coordinate=Coordinate(x=0.1, y=0.2), active=False)
        )
        self.state.current_path = ["Menu1", "Tab1"]

        # Mock vision response for new menu
        new_analysis = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[
                MenuInfo(name="Tab1", coordinate=Coordinate(x=0.1, y=0.05), active=True)
            ],
            current_path=["Menu2", "Tab1"],
            items=[],
        )
        self.vision.add_response(new_analysis)

        result = self.engine._switch_to_next_level1()

        assert result is True
        assert self.state.current_path[0] == "Menu2"

    def test_run_step(self):
        """Test running a single step."""
        # Set up state with unvisited item
        self.state.current_path = ["Menu1", "Tab1"]
        items = [
            MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3)),
        ]
        self.state.add_items("Menu1|Tab1", items)

        # Mock analysis responses
        analysis = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )
        self.vision.add_response(analysis)
        self.vision.add_response(analysis)

        should_continue = self.engine.run_step()

        assert should_continue is True
        assert self.engine._step == 1

    def test_run_step_max_steps(self):
        """Test stopping at max steps."""
        self.config.max_steps = 1
        self.engine._step = 1

        should_continue = self.engine.run_step()

        assert should_continue is False
        assert any(e.event_type == "max_steps_reached" for e in self.events)

    def test_mock_traversal_run(self):
        """Test running a mock traversal."""
        # This is a very basic integration test
        self.state.current_path = ["Menu1", "Tab1"]

        # Add items that will be "visited" immediately
        # (mock vision returns empty items, so next call returns None -> done)
        self.state.add_items("Menu1|Tab1", [])

        summary = self.engine.run()

        assert "total_steps" in summary
        assert "tree" in summary
        assert any(e.event_type == "traversal_start" for e in self.events)


class TestWaitTimeCalculation:
    """Test wait time calculation based on button type."""

    def setup_method(self):
        """Set up test fixtures."""
        self.adb = MockADBClient()
        self.vision = MockVisionService()
        self.state = TraversalState()
        self.config = TraversalConfig(wait_time=0.5)
        self.engine = TraversalEngine(
            adb_client=self.adb,
            vision_service=self.vision,
            state=self.state,
            config=self.config,
        )

    def test_navigate_wait_time(self):
        """Test NAVIGATE action uses longer wait time."""
        item = MenuItem(
            name="MenuItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NAVIGATE,
        )

        wait_time = self.engine._get_wait_time(item)
        assert wait_time >= 1.0  # Should be at least 1 second

    def test_toggle_wait_time(self):
        """Test TOGGLE action uses shorter wait time."""
        item = MenuItem(
            name="Switch",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            expected_action=ExpectedAction.TOGGLE,
        )

        wait_time = self.engine._get_wait_time(item)
        assert wait_time <= 0.3  # Should be at most 0.3 seconds

    def test_none_wait_time(self):
        """Test NONE action uses minimal wait time."""
        item = MenuItem(
            name="ReadOnly",
            type=MenuItemType.READONLY,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NONE,
        )

        wait_time = self.engine._get_wait_time(item)
        assert wait_time == 0.1  # Should be 0.1 seconds

    def test_action_wait_time_default(self):
        """Test ACTION action uses default config wait time."""
        item = MenuItem(
            name="Button",
            type=MenuItemType.BUTTON,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.ACTION,
        )

        wait_time = self.engine._get_wait_time(item)
        assert wait_time == 0.5  # Should use config default

    def test_unknown_action_fallback(self):
        """Test unknown action falls back to config wait time."""
        item = MenuItem(
            name="Unknown",
            type=MenuItemType.ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            # No expected_action set, should use default
        )

        wait_time = self.engine._get_wait_time(item)
        assert wait_time == 0.5  # Should use config default

    def test_custom_config_wait_time(self):
        """Test custom config wait time is respected."""
        custom_config = TraversalConfig(wait_time=0.8)
        custom_engine = TraversalEngine(
            adb_client=self.adb,
            vision_service=self.vision,
            state=self.state,
            config=custom_config,
        )

        item = MenuItem(
            name="Button",
            type=MenuItemType.BUTTON,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.ACTION,
        )

        wait_time = custom_engine._get_wait_time(item)
        assert wait_time == 0.8  # Should use custom config


class TestActionBasedVerification:
    """Test action-based verification logic."""

    def setup_method(self):
        """Set up test fixtures."""
        self.adb = MockADBClient()
        self.vision = MockVisionService()
        self.state = TraversalState()
        self.config = TraversalConfig()
        self.events = []

        def capture_event(event):
            self.events.append(event)

        self.engine = TraversalEngine(
            adb_client=self.adb,
            vision_service=self.vision,
            state=self.state,
            config=self.config,
            event_callback=capture_event,
        )

    def test_verify_navigate_path_changed(self):
        """Test navigate verification when path changes."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu2", "Tab1"],  # Path changed
            items=[],
        )

        item = MenuItem(
            name="MenuItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NAVIGATE,
        )

        result = self.engine._verify_navigate(item, before, after)
        assert result.value == "page_jump"

    def test_verify_navigate_no_path_change(self):
        """Test navigate verification when path doesn't change."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],  # Same path
            items=[],
        )

        item = MenuItem(
            name="MenuItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NAVIGATE,
        )

        result = self.engine._verify_navigate(item, before, after)
        assert result.value == "no_change"

    def test_verify_toggle_state_changed(self):
        """Test toggle verification when state changes."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[
                MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3)),
            ],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],  # Same path
            items=[
                MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3)),
                MenuItem(name="Item2", type="item", coordinate=Coordinate(x=0.5, y=0.5)),  # New item
            ],
        )

        item = MenuItem(
            name="Switch",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            expected_action=ExpectedAction.TOGGLE,
        )

        result = self.engine._verify_toggle(item, before, after)
        assert result.value == "normal"

    def test_verify_toggle_no_state_change(self):
        """Test toggle verification when state doesn't change."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],  # Same path
            items=[],  # Same items
        )

        item = MenuItem(
            name="Switch",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            expected_action=ExpectedAction.TOGGLE,
        )

        result = self.engine._verify_toggle(item, before, after)
        assert result.value == "no_change"

    def test_verify_toggle_with_unexpected_path_change(self):
        """Test toggle verification with unexpected path change."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu2", "Tab1"],  # Unexpected path change
            items=[],
        )

        item = MenuItem(
            name="Switch",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            expected_action=ExpectedAction.TOGGLE,
        )

        result = self.engine._verify_toggle(item, before, after)
        assert result.value == "page_jump"

    def test_verify_generic_path_change(self):
        """Test generic verification handles path change."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu2", "Tab1"],
            items=[],
        )

        result = self.engine._verify_generic(before, after)
        assert result.value == "page_jump"

    def test_verify_generic_items_change(self):
        """Test generic verification handles items change."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3))],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[
                MenuItem(name="Item1", type="item", coordinate=Coordinate(x=0.5, y=0.3)),
                MenuItem(name="Item2", type="item", coordinate=Coordinate(x=0.5, y=0.5)),
            ],
        )

        result = self.engine._verify_generic(before, after)
        assert result.value == "normal"

    def test_verify_generic_no_change(self):
        """Test generic verification when nothing changes."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        result = self.engine._verify_generic(before, after)
        assert result.value == "no_change"

    def test_verify_by_expected_action_routes_correctly(self):
        """Test verification routing based on expected action."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu2", "Tab1"],
            items=[],
        )

        # Test NAVIGATE action
        navigate_item = MenuItem(
            name="MenuItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NAVIGATE,
        )
        result = self.engine._verify_by_expected_action(navigate_item, before, after)
        assert result.value == "page_jump"

        # Test TOGGLE action (with unexpected path change)
        toggle_item = MenuItem(
            name="Switch",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            expected_action=ExpectedAction.TOGGLE,
        )
        result = self.engine._verify_by_expected_action(toggle_item, before, after)
        assert result.value == "page_jump"

        # Test ACTION action
        action_item = MenuItem(
            name="Button",
            type=MenuItemType.BUTTON,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.ACTION,
        )
        result = self.engine._verify_by_expected_action(action_item, before, after)
        assert result.value == "page_jump"


class TestReadOnlyHandling:
    """Test read-only element handling."""

    def setup_method(self):
        """Set up test fixtures."""
        self.adb = MockADBClient()
        self.vision = MockVisionService()
        self.state = TraversalState()
        self.config = TraversalConfig(skip_readonly=True)
        self.engine = TraversalEngine(
            adb_client=self.adb,
            vision_service=self.vision,
            state=self.state,
            config=self.config,
        )

    def test_select_next_item_skips_readonly(self):
        """Test selection skips read-only elements when configured."""
        self.state.current_path = ["Menu1", "Tab1"]
        items = [
            MenuItem(
                name="ReadOnlyText",
                type=MenuItemType.READONLY,
                coordinate=Coordinate(x=0.5, y=0.3),
                expected_action=ExpectedAction.NONE,
            ),
            MenuItem(
                name="ClickableItem",
                type=MenuItemType.BUTTON,
                coordinate=Coordinate(x=0.5, y=0.5),
                expected_action=ExpectedAction.ACTION,
            ),
        ]
        self.state.add_items("Menu1|Tab1", items)

        # Should skip readonly and return clickable item
        item = self.engine._select_next_item()
        assert item is not None
        assert item.name == "ClickableItem"

    def test_select_next_item_includes_readonly_when_disabled(self):
        """Test selection includes read-only when skip_readonly is False."""
        self.config.skip_readonly = False
        self.state.current_path = ["Menu1", "Tab1"]
        items = [
            MenuItem(
                name="ReadOnlyText",
                type=MenuItemType.READONLY,
                coordinate=Coordinate(x=0.5, y=0.3),
                expected_action=ExpectedAction.NONE,
            ),
        ]
        self.state.add_items("Menu1|Tab1", items)

        # Should return readonly item
        item = self.engine._select_next_item()
        assert item is not None
        assert item.name == "ReadOnlyText"

    def test_select_next_item_skips_none_action(self):
        """Test selection skips items with NONE expected action."""
        self.state.current_path = ["Menu1", "Tab1"]
        items = [
            MenuItem(
                name="StaticText",
                type=MenuItemType.TEXT,
                coordinate=Coordinate(x=0.5, y=0.3),
                expected_action=ExpectedAction.NONE,
            ),
            MenuItem(
                name="ClickableItem",
                type=MenuItemType.BUTTON,
                coordinate=Coordinate(x=0.5, y=0.5),
                expected_action=ExpectedAction.ACTION,
            ),
        ]
        self.state.add_items("Menu1|Tab1", items)

        # Should skip NONE action item
        item = self.engine._select_next_item()
        assert item is not None
        assert item.name == "ClickableItem"


class TestBehaviorViolationDetection:
    """Test expected behavior violation detection."""

    def setup_method(self):
        """Set up test fixtures."""
        self.adb = MockADBClient()
        self.vision = MockVisionService()
        self.state = TraversalState()
        self.config = TraversalConfig()
        self.events = []

        def capture_event(event):
            self.events.append(event)

        self.engine = TraversalEngine(
            adb_client=self.adb,
            vision_service=self.vision,
            state=self.state,
            config=self.config,
            event_callback=capture_event,
        )

    def test_navigate_violation_detected(self):
        """Test violation event when navigate doesn't change path."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],  # No path change
            items=[],
        )

        item = MenuItem(
            name="MenuItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NAVIGATE,
        )

        self.engine._check_expected_behavior_violation(item, before, after)

        # Should emit violation event
        violation_events = [e for e in self.events if e.event_type == "expected_behavior_violation"]
        assert len(violation_events) == 1
        assert violation_events[0].data["expected"] == "navigate"
        assert violation_events[0].data["actual"] == "no_change"

    def test_toggle_violation_detected(self):
        """Test violation event when toggle changes path unexpectedly."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu2", "Tab1"],  # Unexpected path change
            items=[],
        )

        item = MenuItem(
            name="Switch",
            type=MenuItemType.SWITCH,
            coordinate=Coordinate(x=0.8, y=0.5),
            expected_action=ExpectedAction.TOGGLE,
        )

        self.engine._check_expected_behavior_violation(item, before, after)

        # Should emit violation event
        violation_events = [e for e in self.events if e.event_type == "expected_behavior_violation"]
        assert len(violation_events) == 1
        assert violation_events[0].data["expected"] == "toggle"
        assert violation_events[0].data["actual"] == "navigate"

    def test_no_violation_when_behavior_matches(self):
        """Test no violation event when behavior matches expectation."""
        before = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu1", "Tab1"],
            items=[],
        )

        after = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=["Menu2", "Tab1"],  # Path changed as expected
            items=[],
        )

        item = MenuItem(
            name="MenuItem",
            type=MenuItemType.MENU_ITEM,
            coordinate=Coordinate(x=0.5, y=0.5),
            expected_action=ExpectedAction.NAVIGATE,
        )

        self.events.clear()
        self.engine._check_expected_behavior_violation(item, before, after)

        # Should NOT emit violation event
        violation_events = [e for e in self.events if e.event_type == "expected_behavior_violation"]
        assert len(violation_events) == 0
