"""Unit tests for StatefulMockVisionService module."""

import pytest
from pathlib import Path

from src.simulation.state_fixture import StateFixture
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.state.content_tree import MenuItemType, ExpectedAction


# -- Test fixtures -----------------------------------------------------------

def get_simple_fixture():
    """Load the simple two page fixture for testing."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"
    return StateFixture.from_yaml(fixture_path)


# -- Task 2.6: test_initial_page --------------------------------------------

def test_initial_page():
    """Test initial page state is set correctly."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    assert vision.current_page_id == "home"
    assert len(vision.navigation_history) == 0
    assert vision.call_count == 0


def test_get_current_page():
    """Test get_current_page returns correct info."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    page_info = vision.get_current_page()
    assert page_info is not None
    assert page_info["page_id"] == "home"
    assert page_info["page_name"] == "HomeScreen"
    assert page_info["path"] == ["home"]
    assert page_info["is_complete"] is False


# -- Task 2.7: test_page_transition -----------------------------------------

def test_page_transition():
    """Test successful page transition."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Initial state
    assert vision.current_page_id == "home"

    # Simulate clicking btn_detail to go to detail page
    success = vision.simulate_action(element_id="btn_detail", action="click")

    assert success is True
    assert vision.current_page_id == "detail"
    assert vision.navigation_history == ["home"]


def test_page_transition_via_settings():
    """Test transition from home to settings."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    success = vision.simulate_action(element_id="btn_settings", action="click")

    assert success is True
    assert vision.current_page_id == "settings"
    assert vision.navigation_history == ["home"]


def test_page_transition_invalid_action():
    """Test transition fails with wrong action type."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Transition defines action="click", try "swipe"
    success = vision.simulate_action(element_id="btn_detail", action="swipe")

    assert success is False
    assert vision.current_page_id == "home"
    assert len(vision.navigation_history) == 0


def test_page_transition_wrong_page():
    """Test transition fails when triggered from wrong page."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # First navigate to detail
    vision.simulate_action(element_id="btn_detail", action="click")
    assert vision.current_page_id == "detail"

    # Try to trigger home_to_detail transition from detail page
    success = vision.simulate_action(element_id="btn_detail", action="click")

    assert success is False
    assert vision.current_page_id == "detail"  # No change


def test_page_transition_nonexistent_element():
    """Test transition fails with nonexistent element."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    success = vision.simulate_action(element_id="btn_nonexistent", action="click")

    assert success is False
    assert vision.current_page_id == "home"


# -- Task 2.8: test_navigation_back -----------------------------------------

def test_navigation_back():
    """Test navigating back to previous page."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Navigate: home -> detail
    vision.simulate_action(element_id="btn_detail", action="click")
    assert vision.current_page_id == "detail"
    assert vision.navigation_history == ["home"]

    # Navigate back
    success = vision.navigate_back()

    assert success is True
    assert vision.current_page_id == "home"
    assert vision.navigation_history == []


def test_navigation_back_multiple_pages():
    """Test navigating back through multiple pages."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Navigate: home -> detail -> settings
    vision.simulate_action(element_id="btn_detail", action="click")
    vision.simulate_action(element_id="btn_settings", action="click")

    assert vision.current_page_id == "settings"
    assert vision.navigation_history == ["home", "detail"]

    # Navigate back
    vision.navigate_back()
    assert vision.current_page_id == "detail"
    assert vision.navigation_history == ["home"]

    # Navigate back again
    vision.navigate_back()
    assert vision.current_page_id == "home"
    assert vision.navigation_history == []


def test_navigation_back_at_root():
    """Test navigating back at root page fails."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    assert vision.current_page_id == "home"
    assert len(vision.navigation_history) == 0

    success = vision.navigate_back()

    assert success is False
    assert vision.current_page_id == "home"


def test_reset_to_initial():
    """Test resetting to initial page state."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Navigate away
    vision.simulate_action(element_id="btn_detail", action="click")
    vision.simulate_action(element_id="btn_settings", action="click")

    assert vision.current_page_id == "settings"
    assert len(vision.navigation_history) == 2

    # Reset
    vision.reset_to_initial()

    assert vision.current_page_id == "home"
    assert len(vision.navigation_history) == 0
    assert vision.call_count == 0


# -- Task 2.9: test_page_analysis_field_mapping -------------------------------

def test_page_analysis_field_mapping():
    """Test PageAnalysis has correct field mapping for MenuItem compatibility."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    page_analysis = vision.analyze_screenshot(b"fake_image_data")

    # Verify PageAnalysis structure
    assert hasattr(page_analysis, "items")  # NOT "menu_items"
    assert not hasattr(page_analysis, "menu_items")

    # Verify items is a list
    assert isinstance(page_analysis.items, list)

    # Check first item (btn_settings from home page)
    btn_settings = page_analysis.items[0]
    assert hasattr(btn_settings, "name")  # NOT "text"
    assert btn_settings.name == "Settings"
    assert btn_settings.type == MenuItemType.BUTTON
    assert btn_settings.coordinate.x == 0.5
    assert btn_settings.coordinate.y == 0.9


def test_menu_item_text_to_name_mapping():
    """Test fixture.text maps to MenuItem.name (not MenuItem.text)."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    page_analysis = vision.analyze_screenshot(b"fake_image_data")

    # Find the "switch_feature" element
    switch_feature = next((item for item in page_analysis.items if "Feature" in item.name), None)

    assert switch_feature is not None
    assert switch_feature.name == "Enable Feature"  # Mapped from fixture.text
    assert switch_feature.type == MenuItemType.SWITCH


def test_menu_item_expected_action_inference():
    """Test ExpectedAction is inferred correctly."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    page_analysis = vision.analyze_screenshot(b"fake_image_data")

    # Button with action_target should expect NAVIGATE
    btn_settings = next((item for item in page_analysis.items if item.name == "Settings"), None)
    assert btn_settings is not None
    assert btn_settings.expected_action == ExpectedAction.NAVIGATE
    assert btn_settings.expects_page_change is True

    # Switch should expect TOGGLE
    switch_feature = next((item for item in page_analysis.items if "Feature" in item.name), None)
    assert switch_feature is not None
    assert switch_feature.expected_action == ExpectedAction.TOGGLE
    assert switch_feature.expects_state_change is True


def test_page_analysis_is_end_of_list():
    """Test is_complete maps to is_end_of_list."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Home page is not complete
    page_analysis = vision.analyze_screenshot(b"fake_image_data")
    assert page_analysis.is_end_of_list is False

    # Navigate to settings (complete page)
    vision.simulate_action(element_id="btn_settings", action="click")
    page_analysis = vision.analyze_screenshot(b"fake_image_data")
    assert page_analysis.is_end_of_list is True


def test_page_analysis_current_path():
    """Test current_path includes navigation history."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Initial state - uses page_name from fixture
    page_analysis = vision.analyze_screenshot(b"fake_image_data")
    assert page_analysis.current_path == ["HomeScreen"]

    # Navigate to detail
    vision.simulate_action(element_id="btn_detail", action="click")
    page_analysis = vision.analyze_screenshot(b"fake_image_data")
    assert page_analysis.current_path == ["HomeScreen", "DetailScreen"]


# -- Task 2.10: test_menu_item_compatible_with_dynamic_matcher --------------

def test_menu_item_compatible_with_dynamic_matcher():
    """Test MenuItem format is compatible with DynamicMatcher input."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    page_analysis = vision.analyze_screenshot(b"fake_image_data")

    # Convert items to DynamicMatcher input format
    # DynamicMatcher expects items with: type, text, index, coordinate, expected_action
    for item in page_analysis.items:
        # Verify required attributes exist
        assert hasattr(item, "type")
        assert hasattr(item, "name")  # Used as "text" by DynamicMatcher
        assert hasattr(item, "coordinate")
        assert hasattr(item, "expected_action")

        # Verify types are correct
        # Note: Pydantic's use_enum_values=True stores string values, not enum objects
        assert isinstance(item.type, (MenuItemType, str))
        assert isinstance(item.name, str)
        assert isinstance(item.coordinate.x, float)
        assert isinstance(item.coordinate.y, float)

        # Convert to dict format (as DynamicMatcher would receive)
        # Handle both enum and string types (Pydantic use_enum_values behavior)
        type_value = item.type if isinstance(item.type, str) else item.type.value
        expected_action_value = (
            item.expected_action
            if isinstance(item.expected_action, str)
            else item.expected_action.value
        )

        item_dict = {
            "type": type_value,
            "text": item.name,  # name maps to text for DynamicMatcher
            "coordinate": {"x": item.coordinate.x, "y": item.coordinate.y},
            "expected_action": expected_action_value,
        }

        # Verify dict has expected keys
        assert "type" in item_dict
        assert "text" in item_dict
        assert "coordinate" in item_dict
        assert "expected_action" in item_dict


def test_element_type_parsing():
    """Test _parse_element_type converts strings to MenuItemType enum."""
    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Verify type conversion works
    assert vision._parse_element_type("button") == MenuItemType.BUTTON
    assert vision._parse_element_type("switch") == MenuItemType.SWITCH
    assert vision._parse_element_type("back_button") == MenuItemType.BACK_BUTTON
    assert vision._parse_element_type("tab") == MenuItemType.TAB

    # Test invalid type falls back to BUTTON
    assert vision._parse_element_type("invalid_type") == MenuItemType.BUTTON


def test_infer_expected_action():
    """Test _infer_expected_action logic."""
    from src.simulation.state_fixture import PageElement

    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Element with action_target
    elem_with_target = PageElement(
        id="btn1", type="button", text="Next", coordinate={"x": 0.5, "y": 0.5}, action_target="detail"
    )
    assert vision._infer_expected_action(elem_with_target) == ExpectedAction.NAVIGATE

    # Switch element
    elem_switch = PageElement(
        id="sw1", type="switch", text="Toggle", coordinate={"x": 0.5, "y": 0.5}
    )
    assert vision._infer_expected_action(elem_switch) == ExpectedAction.TOGGLE

    # Toggle element
    elem_toggle = PageElement(
        id="tg1", type="toggle", text="Favorite", coordinate={"x": 0.5, "y": 0.5}
    )
    assert vision._infer_expected_action(elem_toggle) == ExpectedAction.TOGGLE

    # Text element
    elem_text = PageElement(
        id="txt1", type="text", text="Label", coordinate={"x": 0.5, "y": 0.5}
    )
    assert vision._infer_expected_action(elem_text) == ExpectedAction.NONE

    # Generic button (no action_target)
    elem_button = PageElement(
        id="btn2", type="button", text="Action", coordinate={"x": 0.5, "y": 0.5}
    )
    assert vision._infer_expected_action(elem_button) == ExpectedAction.ACTION


# -- Additional tests ---------------------------------------------------------

def test_vision_service_interface_compatibility():
    """Test StatefulMockVisionService implements VisionService ABC."""
    from src.ai.vision_service import VisionService

    fixture = get_simple_fixture()
    vision = StatefulMockVisionService(fixture)

    # Verify it's an instance of VisionService
    assert isinstance(vision, VisionService)

    # Verify required methods exist
    assert hasattr(vision, "analyze_screenshot")
    assert hasattr(vision, "find_app_entry")
    assert hasattr(vision, "get_current_page")


def test_navigation_history_depth_limit():
    """Test navigation history respects depth limit."""
    # Create fixture with shallow history depth
    fixture_yaml = """
pages:
  home:
    page_name: "Home"
    elements:
      - id: "btn_next"
        type: "button"
        text: "Next"
        coordinate: {x: 0.5, y: 0.5}
        action_target: "page1"
  page1:
    page_name: "Page 1"
    elements:
      - id: "btn_next"
        type: "button"
        text: "Next"
        coordinate: {x: 0.5, y: 0.5}
        action_target: "page2"
  page2:
    page_name: "Page 2"
    elements:
      - id: "btn_next"
        type: "button"
        text: "Next"
        coordinate: {x: 0.5, y: 0.5}
        action_target: "page3"
  page3:
    page_name: "Page 3"
    elements: []

transitions:
  t1:
    trigger: "btn_next"
    from_page: "home"
    to_page: "page1"
    action: "click"
  t2:
    trigger: "btn_next"
    from_page: "page1"
    to_page: "page2"
    action: "click"
  t3:
    trigger: "btn_next"
    from_page: "page2"
    to_page: "page3"
    action: "click"

history_depth: 2
"""

    import tempfile
    from pathlib import Path

    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        f.write(fixture_yaml)
        temp_path = f.name

    try:
        fixture = StateFixture.from_yaml(temp_path)
        vision = StatefulMockVisionService(fixture)

        # Navigate 3 times (home -> page1 -> page2 -> page3)
        vision.simulate_action("btn_next", "click")
        vision.simulate_action("btn_next", "click")
        vision.simulate_action("btn_next", "click")

        # History should only contain last 2 pages (depth=2)
        assert len(vision.navigation_history) <= 2
        assert vision.current_page_id == "page3"
    finally:
        Path(temp_path).unlink()


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
