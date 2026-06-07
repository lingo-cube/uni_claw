"""Unit tests for StateFixture module."""

import pytest
from pathlib import Path

from src.simulation.state_fixture import (
    PageElement,
    PageState,
    PageTransition,
    StateFixture,
)


# -- Task 2.2: test_state_fixture_loading ------------------------------------

def test_state_fixture_loading():
    """Test loading a valid YAML fixture."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"

    fixture = StateFixture.from_yaml(fixture_path)

    # Verify pages were loaded
    assert len(fixture.pages) == 3
    assert "home" in fixture.pages
    assert "detail" in fixture.pages
    assert "settings" in fixture.pages

    # Verify initial page
    assert fixture.initial_page_id == "home"
    assert fixture.current_page_id == "home"

    # Verify home page elements
    home = fixture.pages["home"]
    assert home.page_name == "HomeScreen"
    assert len(home.elements) == 3
    assert home.elements[0].id == "btn_settings"
    assert home.elements[0].type == "button"
    assert home.elements[0].text == "Settings"
    assert home.elements[0].coordinate == {"x": 0.5, "y": 0.9}
    assert home.elements[0].action_target == "settings"

    # Verify transitions were loaded
    assert len(fixture.transitions) == 5
    transition_ids = {t.id for t in fixture.transitions}
    assert "home_to_detail" in transition_ids
    assert "home_to_settings" in transition_ids


# -- Task 2.3: test_state_fixture_transitions --------------------------------

def test_state_fixture_transitions():
    """Test transition rules work correctly."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"
    fixture = StateFixture.from_yaml(fixture_path)

    # Find home_to_detail transition
    transition = fixture.get_transition(
        trigger_element_id="btn_detail",
        from_page_id="home",
        action="click",
    )

    assert transition is not None
    assert transition.id == "home_to_detail"
    assert transition.from_page == "home"
    assert transition.to_page == "detail"
    assert transition.trigger == "btn_detail"
    assert transition.action == "click"

    # Test wrong action doesn't match
    wrong_action = fixture.get_transition(
        trigger_element_id="btn_detail",
        from_page_id="home",
        action="swipe",
    )
    assert wrong_action is None

    # Test wrong page doesn't match
    wrong_page = fixture.get_transition(
        trigger_element_id="btn_detail",
        from_page_id="detail",
        action="click",
    )
    assert wrong_page is None


def test_state_fixture_get_page():
    """Test get_page method."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"
    fixture = StateFixture.from_yaml(fixture_path)

    home = fixture.get_page("home")
    assert home is not None
    assert home.id == "home"
    assert home.page_name == "HomeScreen"

    # Test non-existent page
    nonexistent = fixture.get_page("nonexistent")
    assert nonexistent is None


def test_state_fixture_get_initial_page():
    """Test get_initial_page method."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"
    fixture = StateFixture.from_yaml(fixture_path)

    initial = fixture.get_initial_page()
    assert initial is not None
    assert initial.id == "home"


def test_state_fixture_default_initial_page():
    """Test that first page is default when initial_page not specified."""
    import tempfile
    import yaml

    fixture_yaml = """
pages:
  page_a:
    page_name: "Page A"
    elements: []
    is_complete: false
  page_b:
    page_name: "Page B"
    elements: []
    is_complete: false
"""

    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        f.write(fixture_yaml)
        temp_path = f.name

    try:
        fixture = StateFixture.from_yaml(temp_path)
        # First page should be initial
        assert fixture.initial_page_id == "page_a"
        assert fixture.get_initial_page().id == "page_a"
    finally:
        Path(temp_path).unlink()


# -- Task 2.4: test_state_fixture_validation ---------------------------------

def test_state_fixture_validation_valid():
    """Test validation passes for valid fixture."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"
    fixture = StateFixture.from_yaml(fixture_path)

    errors = fixture.validate()
    assert len(errors) == 0


def test_state_fixture_validation_missing_target_page():
    """Test validation detects missing target page."""
    import tempfile

    fixture_yaml = """
pages:
  home:
    page_name: "Home"
    elements:
      - id: "btn_next"
        type: "button"
        text: "Next"
        coordinate: {x: 0.5, y: 0.5}
        action_target: "detail"
    is_complete: false

transitions:
  to_detail:
    trigger: "btn_next"
    from_page: "home"
    to_page: "detail"
    action: "click"
"""

    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        f.write(fixture_yaml)
        temp_path = f.name

    try:
        fixture = StateFixture.from_yaml(temp_path)
        errors = fixture.validate()

        assert len(errors) > 0
        assert any("to_page 'detail' not found" in e for e in errors)
    finally:
        Path(temp_path).unlink()


def test_state_fixture_validation_missing_trigger_element():
    """Test validation detects missing trigger element."""
    import tempfile

    fixture_yaml = """
pages:
  home:
    page_name: "Home"
    elements:
      - id: "btn_settings"
        type: "button"
        text: "Settings"
        coordinate: {x: 0.5, y: 0.5}
    is_complete: false

  detail:
    page_name: "Detail"
    elements: []
    is_complete: false

transitions:
  to_detail:
    trigger: "btn_detail"
    from_page: "home"
    to_page: "detail"
    action: "click"
"""

    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        f.write(fixture_yaml)
        temp_path = f.name

    try:
        fixture = StateFixture.from_yaml(temp_path)
        errors = fixture.validate()

        assert len(errors) > 0
        assert any("trigger element 'btn_detail' not found" in e for e in errors)
    finally:
        Path(temp_path).unlink()


def test_state_fixture_validation_missing_from_page():
    """Test validation detects missing from page."""
    import tempfile

    fixture_yaml = """
pages:
  home:
    page_name: "Home"
    elements: []
    is_complete: false

transitions:
  invalid_transition:
    trigger: "btn_next"
    from_page: "nonexistent"
    to_page: "home"
    action: "click"
"""

    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        f.write(fixture_yaml)
        temp_path = f.name

    try:
        fixture = StateFixture.from_yaml(temp_path)
        errors = fixture.validate()

        assert len(errors) > 0
        assert any("from_page 'nonexistent' not found" in e for e in errors)
    finally:
        Path(temp_path).unlink()


# -- Additional tests for PageElement -----------------------------------------

def test_page_element_to_dict():
    """Test PageElement to_dict conversion."""
    element = PageElement(
        id="btn1",
        type="button",
        text="Button 1",
        coordinate={"x": 0.5, "y": 0.5},
        action_target="detail",
    )

    result = element.to_dict()
    assert result["id"] == "btn1"
    assert result["type"] == "button"
    assert result["text"] == "Button 1"
    assert result["coordinate"] == {"x": 0.5, "y": 0.5}
    assert result["action_target"] == "detail"


# -- Additional tests for PageTransition ------------------------------------

def test_page_transition_to_dict():
    """Test PageTransition to_dict conversion."""
    transition = PageTransition(
        id="t1",
        trigger="btn1",
        from_page="home",
        to_page="detail",
        action="click",
    )

    result = transition.to_dict()
    assert result["id"] == "t1"
    assert result["trigger"] == "btn1"
    assert result["from_page"] == "home"
    assert result["to_page"] == "detail"
    assert result["action"] == "click"


# -- Additional tests for PageState -----------------------------------------

def test_page_state_get_element():
    """Test PageState.get_element method."""
    elements = [
        PageElement(id="btn1", type="button", text="Button 1", coordinate={"x": 0.5, "y": 0.5}),
        PageElement(id="btn2", type="switch", text="Switch 1", coordinate={"x": 0.5, "y": 0.7}),
    ]
    page = PageState(id="home", page_name="Home", elements=elements)

    element = page.get_element("btn1")
    assert element is not None
    assert element.id == "btn1"
    assert element.text == "Button 1"

    # Test non-existent element
    nonexistent = page.get_element("btn3")
    assert nonexistent is None


def test_page_state_to_dict():
    """Test PageState to_dict conversion."""
    elements = [
        PageElement(id="btn1", type="button", text="Button 1", coordinate={"x": 0.5, "y": 0.5}),
    ]
    page = PageState(id="home", page_name="Home", elements=elements, is_complete=True)

    result = page.to_dict()
    assert result["id"] == "home"
    assert result["page_name"] == "Home"
    assert len(result["elements"]) == 1
    assert result["is_complete"] is True


# -- Tests for StateFixture.to_dict -----------------------------------------

def test_state_fixture_to_dict():
    """Test StateFixture to_dict conversion."""
    fixture_path = Path(__file__).parent.parent / "fixtures" / "simple_two_page.yaml"
    fixture = StateFixture.from_yaml(fixture_path)

    result = fixture.to_dict()
    assert "pages" in result
    assert "transitions" in result
    assert result["initial_page"] == "home"
    assert result["history_depth"] == 10
    assert len(result["pages"]) == 3
    assert len(result["transitions"]) == 5


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
