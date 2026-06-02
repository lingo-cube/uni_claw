"""Unit tests for PageAnalysisAssembler."""

import json
from unittest.mock import Mock

import pytest

from src.ai.vision.page_analysis_assembler import (
    PageAnalysisAssembler,
    DeepSeekPageAnalysisAssembler,
    AssemblyResult,
)
from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.type_hint import TypeHint


class MockResponse:
    """Mock AI response."""

    def __init__(self, content: str, input_tokens: int = 100, output_tokens: int = 200):
        self.content = content
        self.usage = Mock()
        self.usage.input_tokens = input_tokens
        self.usage.output_tokens = output_tokens


class MockTextAIProvider:
    """Mock AI provider for text completion."""

    def __init__(self, response_content: str = None):
        self.response_content = response_content or self._default_response()
        self.call_count = 0

    def complete(self, prompt, model, response_format=None):
        """Mock complete method."""
        self.call_count += 1
        return MockResponse(self.response_content)

    def _default_response(self) -> str:
        """Return default mock response."""
        return json.dumps({
            'layout_type': 'split_pane',
            'level1_dir': 'left',
            'level1_menus': [
                {
                    'name': 'WiFi',
                    'coordinate': {'x': 0.1, 'y': 0.2},
                    'active': True,
                },
                {
                    'name': 'Bluetooth',
                    'coordinate': {'x': 0.1, 'y': 0.3},
                    'active': False,
                },
            ],
            'level2_dir': 'top',
            'level2_menus': [
                {
                    'name': 'General',
                    'coordinate': {'x': 0.3, 'y': 0.1},
                    'active': True,
                },
            ],
            'current_path': ['WiFi', 'General'],
            'items': [
                {
                    'name': 'Mobile Data',
                    'type': 'switch',
                    'coordinate': {'x': 0.3, 'y': 0.4},
                    'expected_action': 'toggle',
                    'expects_page_change': False,
                    'expects_state_change': True,
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


class TestPageAnalysisAssembler:
    """Tests for PageAnalysisAssembler interface."""

    def test_is_abstract(self):
        """Test that PageAnalysisAssembler cannot be instantiated directly."""
        with pytest.raises(TypeError):
            PageAnalysisAssembler()


class TestDeepSeekPageAnalysisAssemblerCreation:
    """Tests for DeepSeekPageAnalysisAssembler creation."""

    def test_creation_with_provider(self):
        """Test creating assembler with AI provider."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)
        assert assembler.ai_provider == provider
        assert assembler.model == "deepseek-v4-flash"

    def test_creation_with_custom_model(self):
        """Test creating assembler with custom model."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(
            provider,
            model="deepseek-v4-pro"
        )
        assert assembler.model == "deepseek-v4-pro"

    def test_default_prompt_loaded(self):
        """Test that default prompt is loaded."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)
        assert "UI logic analysis expert" in assembler._prompt_template


class TestDeepSeekPageAnalysisAssemblerAssemble:
    """Tests for assemble() method."""

    def test_assemble_success(self):
        """Test successful page assembly."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen(
            elements=[],
            screen_hints={'layout_type': 'split_pane'},
        )

        result = assembler.assemble(flattened_screen, {})

        assert isinstance(result, AssemblyResult)
        assert result.page_analysis is not None
        assert result.latency_ms > 0
        assert result.input_tokens == 100
        assert result.output_tokens == 200
        assert not result.cached
        assert result.model == "deepseek-v4-flash"

    def test_assemble_returns_page_analysis(self):
        """Test that assembly returns PageAnalysis with expected structure."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen(
            elements=[],
            screen_hints={'layout_type': 'split_pane'},
        )

        result = assembler.assemble(flattened_screen, {})

        # PageAnalysis doesn't have layout_type directly, but we can check the structure
        assert len(result.page_analysis.level1_menus) == 2
        assert result.page_analysis.level1_menus[0].name == 'WiFi'
        assert result.page_analysis.current_path == ['WiFi', 'General']

    def test_assemble_with_context(self):
        """Test assembly with traversal context."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen()
        context = {'current_path': ['Settings', 'WiFi']}

        result = assembler.assemble(flattened_screen, context)

        # Verify context was passed through (check prompt was built)
        assert provider.call_count == 1

    def test_assemble_none_flattened_screen(self):
        """Test that None flattened_screen raises ValueError."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        with pytest.raises(ValueError, match="flattened_screen cannot be None"):
            assembler.assemble(None, {})

    def test_assemble_ai_failure(self):
        """Test that AI provider failure raises RuntimeError."""
        provider = Mock()
        provider.complete = Mock(side_effect=Exception("AI call failed"))
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen()

        with pytest.raises(RuntimeError, match="Failed to assemble page analysis"):
            assembler.assemble(flattened_screen, {})


class TestPromptBuilding:
    """Tests for _build_prompt() method."""

    def test_build_prompt_includes_flattened_screen(self):
        """Test that prompt includes flattened screen data."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen(
            elements=[
                FlattenedElement(
                    id=0,
                    text="Test",
                    type_hint=TypeHint.TEXT,
                    bbox=BoundingBox(x=0, y=0, w=0.1, h=0.1),
                ),
            ],
            screen_hints={'layout_type': 'single'},
        )

        prompt = assembler._build_prompt(flattened_screen, {})

        assert "flattened_screen" in prompt
        assert "Test" in prompt

    def test_build_prompt_includes_context(self):
        """Test that prompt includes context data."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen()
        context = {'current_path': ['Settings']}

        prompt = assembler._build_prompt(flattened_screen, context)

        assert "context" in prompt
        assert "Settings" in prompt


class TestResponseParsing:
    """Tests for response parsing."""

    def test_parse_valid_response(self):
        """Test parsing a valid AI response."""
        provider = MockTextAIProvider()
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen()
        result = assembler.assemble(flattened_screen, {})

        assert result.page_analysis.level1_menus[0].name == 'WiFi'

    def test_parse_malformed_json(self):
        """Test parsing malformed JSON response."""
        provider = MockTextAIProvider(response_content="not valid json")
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen()

        with pytest.raises(RuntimeError, match="Failed to assemble page analysis"):
            assembler.assemble(flattened_screen, {})

    def test_parse_response_with_missing_fields(self):
        """Test parsing response with missing optional fields."""
        response = json.dumps({
            'level1_dir': 'left',
            'level1_menus': [],
            'level2_dir': None,  # Test None handling
            'level2_menus': [],
            'current_path': [],
            'items': [],
            'is_popup': False,
        })
        provider = MockTextAIProvider(response_content=response)
        assembler = DeepSeekPageAnalysisAssembler(provider)

        flattened_screen = FlattenedScreen()
        result = assembler.assemble(flattened_screen, {})

        # Should handle None level2_dir gracefully
        assert len(result.page_analysis.level1_menus) == 0


class TestAssemblyResult:
    """Tests for AssemblyResult dataclass."""

    def test_creation(self):
        """Test creating AssemblyResult."""
        from src.state.content_tree import PageAnalysis

        page_analysis = PageAnalysis(
            level1_dir='left',
            level1_menus=[],
            level2_dir='top',
            level2_menus=[],
            current_path=[],
            items=[],
        )

        result = AssemblyResult(
            page_analysis=page_analysis,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
        )

        assert result.page_analysis == page_analysis
        assert result.latency_ms == 100.0
        assert result.input_tokens == 50
        assert result.output_tokens == 100
        assert result.cached is False
        assert result.model == ""

    def test_creation_with_optional_fields(self):
        """Test creating result with optional fields."""
        from src.state.content_tree import PageAnalysis

        page_analysis = PageAnalysis(
            level1_dir='left',
            level1_menus=[],
            level2_dir='top',
            level2_menus=[],
            current_path=[],
            items=[],
        )

        result = AssemblyResult(
            page_analysis=page_analysis,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
            cached=True,
            model="deepseek-v4-pro",
        )

        assert result.cached is True
        assert result.model == "deepseek-v4-pro"
