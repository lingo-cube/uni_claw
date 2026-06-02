"""Integration tests for PageAnalysisAssembler.

These tests verify the complete assembly flow using realistic
flattened screen data and scenarios.
"""

import json
from unittest.mock import Mock

import pytest

from src.ai.vision.page_analysis_assembler import (
    DeepSeekPageAnalysisAssembler,
    AssemblyResult,
)
from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.type_hint import TypeHint
from src.models.vision.selection_state import SelectionState


class MockResponse:
    """Mock AI response with usage tracking."""

    def __init__(self, content: str, input_tokens: int = 500, output_tokens: int = 800):
        self.content = content
        self.usage = Mock()
        self.usage.input_tokens = input_tokens
        self.usage.output_tokens = output_tokens


class MockTextAIProvider:
    """Mock AI provider that returns predefined responses."""

    def __init__(self, response_map=None):
        """Initialize with optional response map.

        Args:
            response_map: Dict mapping scenario names to response JSON
        """
        self.response_map = response_map or {}
        self.call_count = 0
        self.last_prompt = None

    def complete(self, prompt, model, response_format=None):
        """Mock complete method."""
        self.call_count += 1
        self.last_prompt = prompt

        # Try to find a matching response based on prompt content
        for scenario, response in self.response_map.items():
            if scenario and scenario in prompt:
                return MockResponse(response)

        # Default response
        return MockResponse(self._default_response())

    def _default_response(self) -> str:
        """Return default mock response for settings page."""
        return json.dumps({
            'layout_type': 'split_pane',
            'level1_dir': 'left',
            'level1_menus': [
                {
                    'name': 'WiFi',
                    'coordinate': {'x': 0.1, 'y': 0.15},
                    'active': True,
                },
                {
                    'name': 'Bluetooth',
                    'coordinate': {'x': 0.1, 'y': 0.25},
                    'active': False,
                },
                {
                    'name': 'Network',
                    'coordinate': {'x': 0.1, 'y': 0.35},
                    'active': False,
                },
            ],
            'level2_dir': 'top',
            'level2_menus': [
                {
                    'name': 'General',
                    'coordinate': {'x': 0.35, 'y': 0.08},
                    'active': True,
                },
                {
                    'name': 'Advanced',
                    'coordinate': {'x': 0.6, 'y': 0.08},
                    'active': False,
                },
            ],
            'current_path': ['WiFi', 'General'],
            'items': [
                {
                    'name': 'Mobile Data',
                    'type': 'switch',
                    'coordinate': {'x': 0.35, 'y': 0.2},
                    'expected_action': 'toggle',
                    'expects_page_change': False,
                    'expects_state_change': True,
                    'parent': None,
                    'confidence': 1.0,
                    'safety_tag': 'safe',
                },
                {
                    'name': 'Roaming',
                    'type': 'switch',
                    'coordinate': {'x': 0.35, 'y': 0.3},
                    'expected_action': 'toggle',
                    'expects_page_change': False,
                    'expects_state_change': True,
                    'parent': None,
                    'confidence': 1.0,
                    'safety_tag': 'safe',
                },
                {
                    'name': 'Network Mode',
                    'type': 'menu_item',
                    'coordinate': {'x': 0.35, 'y': 0.45},
                    'expected_action': 'navigate',
                    'expects_page_change': True,
                    'expects_state_change': False,
                    'parent': None,
                    'confidence': 1.0,
                    'safety_tag': 'safe',
                },
            ],
            'is_popup': False,
            'popup_info': None,
            'close_button': None,
            'back_button': {'x': 0.05, 'y': 0.05},
            'has_scroll': True,
            'is_end_of_list': False,
        })


@pytest.fixture
def settings_flattened_screen():
    """Create a realistic flattened screen for settings page."""
    elements = [
        # Level 1 menu items (left panel)
        FlattenedElement(
            id=0,
            text="WiFi",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.05, y=0.15, w=0.25, h=0.06),
            region="left_panel",
            selection_state=SelectionState.SELECTED,
            visual_state={"bold": True, "has_indicator": "filled_circle"},
        ),
        FlattenedElement(
            id=1,
            text="Bluetooth",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.05, y=0.25, w=0.25, h=0.06),
            region="left_panel",
            selection_state=SelectionState.NORMAL,
        ),
        FlattenedElement(
            id=2,
            text="Network",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.05, y=0.35, w=0.25, h=0.06),
            region="left_panel",
            selection_state=SelectionState.NORMAL,
        ),
        # Level 2 tabs (top)
        FlattenedElement(
            id=3,
            text="General",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.35, y=0.08, w=0.15, h=0.05),
            region="tabs",
            selection_state=SelectionState.SELECTED,
            visual_state={"has_indicator": "underline"},
        ),
        FlattenedElement(
            id=4,
            text="Advanced",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.6, y=0.08, w=0.15, h=0.05),
            region="tabs",
            selection_state=SelectionState.NORMAL,
        ),
        # Content items
        FlattenedElement(
            id=5,
            text="Mobile Data",
            type_hint=TypeHint.SWITCH,
            bbox=BoundingBox(x=0.35, y=0.2, w=0.5, h=0.06),
            region="content_area",
            selection_state=SelectionState.NORMAL,
            visual_state={"switch_state": "on"},
        ),
        FlattenedElement(
            id=6,
            text="Roaming",
            type_hint=TypeHint.SWITCH,
            bbox=BoundingBox(x=0.35, y=0.3, w=0.5, h=0.06),
            region="content_area",
            selection_state=SelectionState.NORMAL,
            visual_state={"switch_state": "off"},
        ),
        FlattenedElement(
            id=7,
            text="Network Mode",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.35, y=0.45, w=0.5, h=0.06),
            region="content_area",
            selection_state=SelectionState.NORMAL,
            visual_state={"has_indicator": "chevron_right"},
        ),
        # Back button
        FlattenedElement(
            id=8,
            text="",
            type_hint=TypeHint.ICON,
            bbox=BoundingBox(x=0.02, y=0.04, w=0.06, h=0.04),
            region="top_bar",
            selection_state=SelectionState.NORMAL,
            visual_state={"icon_type": "back_arrow"},
        ),
    ]

    return FlattenedScreen(
        elements=elements,
        screen_hints={
            'top_bar_text': 'Settings',
            'layout_type': 'split_pane',
            'overlay_detected': False,
            'scroll_detected': True,
        },
    )


@pytest.fixture
def dialog_flattened_screen():
    """Create a flattened screen for a confirmation dialog."""
    elements = [
        # Overlay background indicator
        FlattenedElement(
            id=0,
            text="",
            type_hint=TypeHint.IMAGE,
            bbox=BoundingBox(x=0.0, y=0.0, w=1.0, h=1.0),
            region="overlay",
            selection_state=SelectionState.NORMAL,
            visual_state={"alpha": "dimmed"},
        ),
        # Dialog content
        FlattenedElement(
            id=1,
            text="Confirm Reset",
            type_hint=TypeHint.TEXT,
            bbox=BoundingBox(x=0.3, y=0.35, w=0.4, h=0.05),
            region="overlay",
            selection_state=SelectionState.NORMAL,
            visual_state={"bold": True, "font_size": "large"},
        ),
        FlattenedElement(
            id=2,
            text="This will reset all settings to default. Continue?",
            type_hint=TypeHint.TEXT,
            bbox=BoundingBox(x=0.3, y=0.42, w=0.4, h=0.08),
            region="overlay",
            selection_state=SelectionState.NORMAL,
        ),
        FlattenedElement(
            id=3,
            text="Cancel",
            type_hint=TypeHint.BUTTON,
            bbox=BoundingBox(x=0.3, y=0.55, w=0.15, h=0.08),
            region="overlay",
            selection_state=SelectionState.NORMAL,
        ),
        FlattenedElement(
            id=4,
            text="Confirm",
            type_hint=TypeHint.BUTTON,
            bbox=BoundingBox(x=0.55, y=0.55, w=0.15, h=0.08),
            region="overlay",
            selection_state=SelectionState.NORMAL,
            visual_state={"bold": True, "color": "primary"},
        ),
    ]

    return FlattenedScreen(
        elements=elements,
        screen_hints={
            'top_bar_text': '',
            'layout_type': 'overlay',
            'overlay_detected': True,
            'scroll_detected': False,
        },
    )


@pytest.fixture
def tabbed_view_flattened_screen():
    """Create a flattened screen for a tabbed view."""
    elements = [
        # Top tabs
        FlattenedElement(
            id=0,
            text="Home",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.1, y=0.08, w=0.15, h=0.05),
            region="tabs",
            selection_state=SelectionState.SELECTED,
            visual_state={"has_indicator": "underline"},
        ),
        FlattenedElement(
            id=1,
            text="Media",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.3, y=0.08, w=0.15, h=0.05),
            region="tabs",
            selection_state=SelectionState.NORMAL,
        ),
        FlattenedElement(
            id=2,
            text="Phone",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.5, y=0.08, w=0.15, h=0.05),
            region="tabs",
            selection_state=SelectionState.NORMAL,
        ),
        FlattenedElement(
            id=3,
            text="Navigation",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.7, y=0.08, w=0.15, h=0.05),
            region="tabs",
            selection_state=SelectionState.NORMAL,
        ),
        # Content items
        FlattenedElement(
            id=4,
            text="Recent Calls",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.1, y=0.2, w=0.3, h=0.08),
            region="content_area",
            selection_state=SelectionState.NORMAL,
        ),
        FlattenedElement(
            id=5,
            text="Contacts",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.1, y=0.3, w=0.3, h=0.08),
            region="content_area",
            selection_state=SelectionState.NORMAL,
        ),
        FlattenedElement(
            id=6,
            text="Dialer",
            type_hint=TypeHint.CLICKABLE_TEXT,
            bbox=BoundingBox(x=0.1, y=0.4, w=0.3, h=0.08),
            region="content_area",
            selection_state=SelectionState.NORMAL,
        ),
    ]

    return FlattenedScreen(
        elements=elements,
        screen_hints={
            'top_bar_text': 'Phone',
            'layout_type': 'tabbed',
            'overlay_detected': False,
            'scroll_detected': True,
        },
    )


class TestIntegrationSettingsPage:
    """Integration tests for settings page assembly."""

    def test_assemble_settings_page_complete_flow(self, settings_flattened_screen):
        """Test complete assembly flow for settings page."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        # Verify result structure
        assert isinstance(result, AssemblyResult)
        assert result.page_analysis is not None
        assert result.latency_ms >= 0
        assert result.input_tokens >= 0
        assert result.output_tokens >= 0

        # Verify provider was called
        assert provider.call_count == 1

    def test_settings_page_output_format(self, settings_flattened_screen):
        """Test that assembled settings page has correct format."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        pa = result.page_analysis

        # Verify level 1 menus
        assert len(pa.level1_menus) == 3
        assert pa.level1_menus[0].name == 'WiFi'
        assert pa.level1_menus[0].active is True
        assert pa.level1_menus[0].coordinate.x == 0.1
        assert pa.level1_menus[1].name == 'Bluetooth'
        assert pa.level1_menus[1].active is False

        # Verify level 2 menus
        assert len(pa.level2_menus) == 2
        assert pa.level2_menus[0].name == 'General'
        assert pa.level2_menus[0].active is True

        # Verify current path
        assert pa.current_path == ['WiFi', 'General']

        # Verify items
        assert len(pa.items) == 3
        assert pa.items[0].name == 'Mobile Data'
        assert pa.items[0].expected_action == 'toggle' or pa.items[0].expected_action.value == 'toggle'
        assert pa.items[0].expects_state_change is True

        # Verify back button
        assert pa.back_button is not None
        assert pa.back_button.x == 0.05

        # Verify not a popup
        assert pa.is_popup is False

    def test_settings_page_with_context(self, settings_flattened_screen):
        """Test assembly with traversal context."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        context = {
            'current_path': ['Settings'],
            'previous_screen': 'main_menu',
            'navigation_history': ['main_menu', 'Settings'],
        }

        result = assembler.assemble(settings_flattened_screen, context)

        # Verify context was included in prompt
        assert 'Settings' in provider.last_prompt
        assert 'previous_screen' in provider.last_prompt

        # Verify result
        assert result.page_analysis is not None


class TestIntegrationDialog:
    """Integration tests for dialog/popup assembly."""

    def test_assemble_dialog_complete_flow(self, dialog_flattened_screen):
        """Test complete assembly flow for confirmation dialog."""
        dialog_response = json.dumps({
            'layout_type': 'overlay',
            'level1_dir': None,
            'level1_menus': [],
            'level2_dir': None,
            'level2_menus': [],
            'current_path': [],
            'items': [],
            'is_popup': True,
            'popup_info': {
                'title': 'Confirm Reset',
                'content': 'This will reset all settings to default. Continue?',
                'close_button': None,
            },
            'close_button': {'x': 0.3, 'y': 0.55},  # Cancel button
            'back_button': None,
            'has_scroll': False,
            'is_end_of_list': False,
        })

        provider = MockTextAIProvider(response_map={'Confirm Reset': dialog_response})
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(dialog_flattened_screen, {})

        # Verify result structure
        assert isinstance(result, AssemblyResult)
        assert result.page_analysis is not None

        # Verify popup detected
        assert result.page_analysis.is_popup is True
        assert result.page_analysis.popup_info is not None
        assert result.page_analysis.popup_info.title == 'Confirm Reset'

    def test_dialog_output_format(self, dialog_flattened_screen):
        """Test that assembled dialog has correct format."""
        dialog_response = json.dumps({
            'level1_dir': None,
            'level1_menus': [],
            'level2_dir': None,
            'level2_menus': [],
            'current_path': [],
            'items': [
                {
                    'name': 'Cancel',
                    'type': 'button',
                    'coordinate': {'x': 0.3, 'y': 0.55},
                    'expected_action': 'action',
                    'expects_page_change': False,
                    'expects_state_change': False,
                    'parent': None,
                    'confidence': 1.0,
                    'safety_tag': 'safe',
                },
                {
                    'name': 'Confirm',
                    'type': 'button',
                    'coordinate': {'x': 0.55, 'y': 0.55},
                    'expected_action': 'action',
                    'expects_page_change': False,
                    'expects_state_change': False,
                    'parent': None,
                    'confidence': 1.0,
                    'safety_tag': 'safe',
                },
            ],
            'is_popup': True,
            'popup_info': {
                'title': 'Confirm Reset',
                'content': 'This will reset all settings',
                'close_button': None,
            },
            'close_button': {'x': 0.3, 'y': 0.55},
            'back_button': None,
            'has_scroll': False,
            'is_end_of_list': False,
        })

        provider = MockTextAIProvider(response_map={'Confirm': dialog_response})
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(dialog_flattened_screen, {})

        pa = result.page_analysis

        # Verify popup structure
        assert pa.is_popup is True
        assert pa.popup_info.title == 'Confirm Reset'
        assert pa.close_button is not None

        # Verify no menus in popup
        assert len(pa.level1_menus) == 0
        assert len(pa.level2_menus) == 0


class TestIntegrationTabbedView:
    """Integration tests for tabbed view assembly."""

    def test_assemble_tabbed_view_complete_flow(self, tabbed_view_flattened_screen):
        """Test complete assembly flow for tabbed view."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(tabbed_view_flattened_screen, {})

        # Verify result structure
        assert isinstance(result, AssemblyResult)
        assert result.page_analysis is not None

        # Verify tabs in level2_menus
        assert len(result.page_analysis.level2_menus) >= 1

    def test_tabbed_view_correct_tab_identification(self, tabbed_view_flattened_screen):
        """Test that tabs are correctly identified."""
        tabbed_response = json.dumps({
            'layout_type': 'tabbed',
            'level1_dir': None,
            'level1_menus': [],
            'level2_dir': 'top',
            'level2_menus': [
                {'name': 'Home', 'coordinate': {'x': 0.1, 'y': 0.08}, 'active': True},
                {'name': 'Media', 'coordinate': {'x': 0.3, 'y': 0.08}, 'active': False},
                {'name': 'Phone', 'coordinate': {'x': 0.5, 'y': 0.08}, 'active': False},
                {'name': 'Navigation', 'coordinate': {'x': 0.7, 'y': 0.08}, 'active': False},
            ],
            'current_path': ['Home'],
            'items': [
                {
                    'name': 'Recent Calls',
                    'type': 'menu_item',
                    'coordinate': {'x': 0.1, 'y': 0.2},
                    'expected_action': 'navigate',
                    'expects_page_change': True,
                    'expects_state_change': False,
                    'parent': None,
                    'confidence': 1.0,
                    'safety_tag': 'safe',
                },
            ],
            'is_popup': False,
            'popup_info': None,
            'close_button': None,
            'back_button': None,
            'has_scroll': True,
            'is_end_of_list': False,
        })

        provider = MockTextAIProvider(response_map={'Phone': tabbed_response})
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(tabbed_view_flattened_screen, {})

        pa = result.page_analysis

        # Verify tabs identified
        assert len(pa.level2_menus) == 4
        assert pa.level2_menus[0].name == 'Home'
        assert pa.level2_menus[0].active is True
        assert pa.level2_dir.value == 'top'


class TestAccuracyVerification:
    """Tests to verify assembly accuracy."""

    def test_coordinate_accuracy(self, settings_flattened_screen):
        """Test that coordinates are preserved accurately."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        pa = result.page_analysis

        # Check that coordinates are in expected range (0-1)
        for menu in pa.level1_menus:
            assert 0 <= menu.coordinate.x <= 1
            assert 0 <= menu.coordinate.y <= 1

        for item in pa.items:
            assert 0 <= item.coordinate.x <= 1
            assert 0 <= item.coordinate.y <= 1

    def test_type_mapping_accuracy(self, settings_flattened_screen):
        """Test that type hints are mapped correctly."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        pa = result.page_analysis

        # Switches should map to 'switch' type with toggle action
        switch_items = [i for i in pa.items if str(i.type) == 'switch' or (hasattr(i.type, 'value') and i.type.value == 'switch')]
        for item in switch_items:
            action = item.expected_action.value if hasattr(item.expected_action, 'value') else item.expected_action
            assert action == 'toggle'
            assert item.expects_state_change is True

        # Menu items should have navigate action
        menu_items = [i for i in pa.items if str(i.type) == 'menu_item' or (hasattr(i.type, 'value') and i.type.value == 'menu_item')]
        for item in menu_items:
            action = item.expected_action.value if hasattr(item.expected_action, 'value') else item.expected_action
            assert action == 'navigate'

    def test_current_path_accuracy(self, settings_flattened_screen):
        """Test that current path is correctly inferred."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        pa = result.page_analysis

        # Current path should reflect active selections
        assert len(pa.current_path) >= 1
        assert pa.current_path[0] == 'WiFi'
        assert pa.current_path[1] == 'General'

    def test_selection_state_accuracy(self, settings_flattened_screen):
        """Test that selection states are correctly inferred."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        pa = result.page_analysis

        # Verify active menu matches selected state from input
        active_l1 = [m for m in pa.level1_menus if m.active]
        assert len(active_l1) == 1
        assert active_l1[0].name == 'WiFi'

        active_l2 = [m for m in pa.level2_menus if m.active]
        assert len(active_l2) == 1
        assert active_l2[0].name == 'General'


class TestErrorHandling:
    """Integration tests for error handling."""

    def test_malformed_json_response(self, settings_flattened_screen):
        """Test handling of malformed JSON response."""
        # Create a provider that returns invalid JSON
        provider = Mock()
        provider.complete = Mock(return_value=MockResponse("invalid json {{{"))
        assembler = DeepSeekPageAnalysisAssembler(provider)

        with pytest.raises(RuntimeError, match="Failed to assemble page analysis"):
            assembler.assemble(settings_flattened_screen, {})

    def test_missing_required_fields(self, settings_flattened_screen):
        """Test handling of response with missing required fields."""
        incomplete_response = json.dumps({
            'level1_dir': 'left',
            # Missing level1_menus and other required fields
        })

        provider = MockTextAIProvider(response_map={None: incomplete_response})
        assembler = DeepSeekPageAnalysisAssembler(provider)

        # Should not crash, but return minimal PageAnalysis
        result = assembler.assemble(settings_flattened_screen, {})
        assert result.page_analysis is not None

    def test_empty_flattened_screen(self):
        """Test assembly with empty flattened screen."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        empty_screen = FlattenedScreen(elements=[], screen_hints={})

        result = assembler.assemble(empty_screen, {})

        # Should still return valid result
        assert result.page_analysis is not None


class TestPerformanceMetrics:
    """Integration tests for performance metrics."""

    def test_latency_measurement(self, settings_flattened_screen):
        """Test that latency is measured accurately."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        assert result.latency_ms >= 0
        assert isinstance(result.latency_ms, (int, float))

    def test_token_tracking(self, settings_flattened_screen):
        """Test that token usage is tracked."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        result = assembler.assemble(settings_flattened_screen, {})

        assert result.input_tokens >= 0
        assert result.output_tokens >= 0

    def test_model_tracking(self, settings_flattened_screen):
        """Test that model is tracked in result."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(
            provider,
            model="deepseek-v4-pro"
        )

        result = assembler.assemble(settings_flattened_screen, {})

        assert result.model == "deepseek-v4-pro"
