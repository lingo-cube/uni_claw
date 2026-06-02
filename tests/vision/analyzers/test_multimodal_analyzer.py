"""Unit tests for MultimodalAnalyzer."""

import json
from unittest.mock import Mock, MagicMock

import pytest

from src.ai.vision.multimodal_analyzer import (
    MultimodalAnalyzer,
    ClaudeMultimodalAnalyzer,
    MultimodalAnalysisResult,
)
from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.flattened_element import FlattenedElement


class MockResponse:
    """Mock AI response."""

    def __init__(self, content: str, input_tokens: int = 100, output_tokens: int = 200):
        self.content = content
        self.usage = Mock()
        self.usage.input_tokens = input_tokens
        self.usage.output_tokens = output_tokens


class MockAIProvider:
    """Mock AI provider for testing."""

    def __init__(self, response_content: str = None):
        self.response_content = response_content or self._default_response()
        self.call_count = 0

    def complete(self, prompt, image_data, model, response_format=None):
        """Mock complete method."""
        self.call_count += 1
        return MockResponse(self.response_content)

    def _default_response(self) -> str:
        """Return default mock response."""
        return json.dumps({
            'elements': [
                {
                    'id': 0,
                    'text': 'WiFi',
                    'type_hint': 'clickable_text',
                    'bbox': {'x': 0.1, 'y': 0.2, 'w': 0.3, 'h': 0.05},
                    'region': 'left_panel',
                    'selection_state': 'selected',
                    'visual_state': {'bold': True},
                    'confidence': 0.95,
                },
                {
                    'id': 1,
                    'text': 'Mobile Data',
                    'type_hint': 'switch',
                    'bbox': {'x': 0.3, 'y': 0.4, 'w': 0.15, 'h': 0.05},
                    'region': 'content_area',
                    'selection_state': 'normal',
                    'visual_state': {},
                    'confidence': 0.98,
                },
            ],
            'screen_hints': {
                'top_bar_text': 'Settings',
                'layout_type': 'split_pane',
                'overlay_detected': False,
                'scroll_detected': True,
            },
        })


class TestMultimodalAnalyzer:
    """Tests for MultimodalAnalyzer interface."""

    def test_is_abstract(self):
        """Test that MultimodalAnalyzer cannot be instantiated directly."""
        with pytest.raises(TypeError):
            MultimodalAnalyzer()


class TestClaudeMultimodalAnalyzerCreation:
    """Tests for ClaudeMultimodalAnalyzer creation and initialization."""

    def test_creation_with_provider(self):
        """Test creating analyzer with AI provider."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(provider)
        assert analyzer.ai_provider == provider
        assert analyzer.model == "claude-3-5-sonnet-20241022"

    def test_creation_with_custom_model(self):
        """Test creating analyzer with custom model."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(
            provider,
            model="claude-3-opus-20240229"
        )
        assert analyzer.model == "claude-3-opus-20240229"

    def test_creation_with_custom_prompt(self):
        """Test creating analyzer with custom prompt."""
        provider = MockAIProvider()
        custom_prompt = "Custom prompt for testing"
        analyzer = ClaudeMultimodalAnalyzer(
            provider,
            prompt=custom_prompt
        )
        assert analyzer._prompt == custom_prompt

    def test_default_prompt_loaded(self):
        """Test that default prompt is loaded when none provided."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(provider)
        assert "UI visual analysis expert" in analyzer._prompt


class TestClaudeMultimodalAnalyzerAnalyze:
    """Tests for analyze() method."""

    def test_analyze_success(self):
        """Test successful screenshot analysis."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(provider)

        result = analyzer.analyze(b"fake_image_data")

        assert isinstance(result, MultimodalAnalysisResult)
        assert isinstance(result.flattened_screen, FlattenedScreen)
        assert result.latency_ms > 0
        assert result.input_tokens == 100
        assert result.output_tokens == 200
        assert not result.cached
        assert result.model == "claude-3-5-sonnet-20241022"

    def test_analyze_returns_elements(self):
        """Test that analysis returns expected elements."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(provider)

        result = analyzer.analyze(b"fake_image_data")

        assert len(result.flattened_screen.elements) == 2
        assert result.flattened_screen.elements[0].text == "WiFi"
        assert result.flattened_screen.elements[1].text == "Mobile Data"

    def test_analyze_returns_screen_hints(self):
        """Test that analysis returns screen hints."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(provider)

        result = analyzer.analyze(b"fake_image_data")

        hints = result.flattened_screen.screen_hints
        assert hints['top_bar_text'] == 'Settings'
        assert hints['layout_type'] == 'split_pane'
        assert hints['overlay_detected'] is False
        assert hints['scroll_detected'] is True

    def test_analyze_empty_image_data(self):
        """Test that empty image data raises ValueError."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(provider)

        with pytest.raises(ValueError, match="image_data cannot be empty"):
            analyzer.analyze(b"")

    def test_analyze_ai_failure(self):
        """Test that AI provider failure raises RuntimeError."""
        provider = Mock()
        provider.complete = Mock(side_effect=Exception("AI call failed"))
        analyzer = ClaudeMultimodalAnalyzer(provider)

        with pytest.raises(RuntimeError, match="Failed to analyze screenshot"):
            analyzer.analyze(b"fake_image_data")


class TestResponseParsing:
    """Tests for response parsing."""

    def test_parse_valid_response(self):
        """Test parsing a valid AI response."""
        provider = MockAIProvider()
        analyzer = ClaudeMultimodalAnalyzer(provider)

        result = analyzer.analyze(b"fake_image_data")

        assert result.flattened_screen.element_count() == 2

    def test_parse_malformed_json(self):
        """Test parsing malformed JSON response."""
        provider = MockAIProvider(response_content="not valid json")
        analyzer = ClaudeMultimodalAnalyzer(provider)

        with pytest.raises(RuntimeError, match="Failed to analyze screenshot"):
            analyzer.analyze(b"fake_image_data")

    def test_parse_missing_elements(self):
        """Test parsing response without elements array."""
        response = json.dumps({"screen_hints": {}})
        provider = MockAIProvider(response_content=response)
        analyzer = ClaudeMultimodalAnalyzer(provider)

        with pytest.raises(RuntimeError, match="Failed to analyze screenshot"):
            analyzer.analyze(b"fake_image_data")

    def test_parse_handles_invalid_element(self):
        """Test that invalid elements are skipped with warning."""
        response = json.dumps({
            'elements': [
                {
                    'id': 0,
                    'text': 'Valid',
                    'type_hint': 'text',
                    'bbox': {'x': 0, 'y': 0, 'w': 0.1, 'h': 0.1},
                },
                {
                    'id': 1,
                    # Missing required fields - should be skipped
                },
            ],
            'screen_hints': {},
        })
        provider = MockAIProvider(response_content=response)
        analyzer = ClaudeMultimodalAnalyzer(provider)

        result = analyzer.analyze(b"fake_image_data")
        # Only the valid element should be parsed
        assert result.flattened_screen.element_count() == 1

    def test_fuzzy_type_hint_parsing(self):
        """Test that fuzzy type hint matching works."""
        response = json.dumps({
            'elements': [
                {
                    'id': 0,
                    'text': 'Test',
                    'type_hint': 'toggle',  # Should map to SWITCH
                    'bbox': {'x': 0, 'y': 0, 'w': 0.1, 'h': 0.1},
                },
            ],
            'screen_hints': {},
        })
        provider = MockAIProvider(response_content=response)
        analyzer = ClaudeMultimodalAnalyzer(provider)

        result = analyzer.analyze(b"fake_image_data")
        from src.models.vision.type_hint import TypeHint
        assert result.flattened_screen.elements[0].type_hint == TypeHint.SWITCH

    def test_fuzzy_selection_state_parsing(self):
        """Test that fuzzy selection state matching works."""
        response = json.dumps({
            'elements': [
                {
                    'id': 0,
                    'text': 'Test',
                    'type_hint': 'text',
                    'bbox': {'x': 0, 'y': 0, 'w': 0.1, 'h': 0.1},
                    'selection_state': 'active',  # Should map to SELECTED
                },
            ],
            'screen_hints': {},
        })
        provider = MockAIProvider(response_content=response)
        analyzer = ClaudeMultimodalAnalyzer(provider)

        result = analyzer.analyze(b"fake_image_data")
        from src.models.vision.selection_state import SelectionState
        assert result.flattened_screen.elements[0].selection_state == SelectionState.SELECTED


class TestMultimodalAnalysisResult:
    """Tests for MultimodalAnalysisResult dataclass."""

    def test_creation(self):
        """Test creating MultimodalAnalysisResult."""
        screen = FlattenedScreen()
        result = MultimodalAnalysisResult(
            flattened_screen=screen,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
        )
        assert result.flattened_screen == screen
        assert result.latency_ms == 100.0
        assert result.input_tokens == 50
        assert result.output_tokens == 100
        assert result.cached is False
        assert result.model == ""

    def test_creation_with_optional_fields(self):
        """Test creating result with optional fields."""
        screen = FlattenedScreen()
        result = MultimodalAnalysisResult(
            flattened_screen=screen,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
            cached=True,
            model="claude-3-opus-20240229",
        )
        assert result.cached is True
        assert result.model == "claude-3-opus-20240229"
