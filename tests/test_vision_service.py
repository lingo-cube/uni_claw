"""Tests for vision service implementations."""

import base64
from unittest.mock import MagicMock, patch

import pytest

from src.state.content_tree import (
    Coordinate,
    Direction,
    MenuInfo,
    MenuItem,
    PageAnalysis,
)
from src.ai.vision_service import (
    ClaudeVisionService,
    MockVisionService,
    PROMPT_FIND_ENTRY,
    PROMPT_STRUCTURE,
    VisionError,
)


class TestMockVisionService:
    """Test mock vision service."""

    def test_analyze_screenshot_returns_default(self):
        """Test default mock analysis."""
        service = MockVisionService()
        result = service.analyze_screenshot(b"fake_png")

        assert isinstance(result, PageAnalysis)
        assert len(result.level1_menus) > 0
        assert len(result.level2_menus) > 0
        assert len(result.items) > 0

    def test_find_app_entry_always_succeeds(self):
        """Test mock always finds target app."""
        service = MockVisionService()
        result = service.find_app_entry(b"fake_png", "TestApp")

        assert result is not None
        assert result["name"] == "TestApp"
        assert "x" in result
        assert "y" in result

    def test_custom_response_queue(self):
        """Test adding custom responses."""
        service = MockVisionService()

        custom = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
        )
        service.add_response(custom)

        result = service.analyze_screenshot(b"fake")
        assert result == custom

    def test_call_count_tracking(self):
        """Test that call count is tracked."""
        service = MockVisionService()

        assert service.call_count == 0
        service.analyze_screenshot(b"fake")
        assert service.call_count == 1


class TestClaudeVisionService:
    """Test Claude vision service."""

    def test_encode_image(self):
        """Test image encoding."""
        service = ClaudeVisionService(api_key="test")

        image_data = b"test_data"
        encoded = service._encode_image(image_data)

        assert encoded == base64.b64encode(image_data).decode("utf-8")

    @patch("src.vision.vision_service.Anthropic")
    def test_analyze_screenshot_success(self, mock_anthropic):
        """Test successful screenshot analysis."""
        # Mock the API response
        mock_message = MagicMock()
        mock_message.content = [MagicMock(text='{"level1_dir": "left", "level1_menus": [], "level2_dir": "top", "level2_menus": [], "current_path": [], "items": []}')]
        mock_client = MagicMock()
        mock_client.messages.create.return_value = mock_message
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test")
        result = service.analyze_screenshot(b"fake_png")

        assert isinstance(result, PageAnalysis)
        mock_client.messages.create.assert_called_once()

    @patch("src.vision.vision_service.Anthropic")
    def test_analyze_screenshot_json_embedded(self, mock_anthropic):
        """Test parsing JSON embedded in markdown."""
        mock_message = MagicMock()
        mock_message.content = [MagicMock(text='Some text\n```json\n{"level1_dir": "left", "level1_menus": [], "level2_dir": "top", "level2_menus": [], "current_path": [], "items": []}\n```\nMore text')]
        mock_client = MagicMock()
        mock_client.messages.create.return_value = mock_message
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test")
        result = service.analyze_screenshot(b"fake_png")

        assert isinstance(result, PageAnalysis)

    @patch("src.vision.vision_service.Anthropic")
    def test_analyze_screenshot_invalid_json_raises_error(self, mock_anthropic):
        """Test that invalid JSON raises VisionError."""
        mock_message = MagicMock()
        mock_message.content = [MagicMock(text="Not valid JSON")]
        mock_client = MagicMock()
        mock_client.messages.create.return_value = mock_message
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test")

        with pytest.raises(VisionError):
            service.analyze_screenshot(b"fake_png")

    @patch("src.vision.vision_service.Anthropic")
    def test_api_error_propagates(self, mock_anthropic):
        """Test that API errors are wrapped in VisionError."""
        mock_client = MagicMock()
        mock_client.messages.create.side_effect = Exception("API Error")
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test")

        with pytest.raises(VisionError):
            service.analyze_screenshot(b"fake_png")


class TestPrompts:
    """Test prompt templates."""

    def test_structure_prompt_is_defined(self):
        """Test that structure prompt exists."""
        assert PROMPT_STRUCTURE
        assert "menu" in PROMPT_STRUCTURE.lower()
        assert "json" in PROMPT_STRUCTURE.lower()

    def test_find_entry_prompt_uses_target(self):
        """Test find entry prompt template."""
        prompt = PROMPT_FIND_ENTRY.format(target="MyApp")
        assert "MyApp" in prompt
        assert "found" in prompt.lower()


class TestPageAnalysis:
    """Test PageAnalysis data model."""

    def test_page_analysis_creation(self):
        """Test creating PageAnalysis."""
        analysis = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="Menu1", coordinate=Coordinate(x=0.1, y=0.1), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[
                MenuInfo(name="Tab1", coordinate=Coordinate(x=0.5, y=0.05), active=True)
            ],
            current_path=["Menu1", "Tab1"],
            items=[
                MenuItem(
                    name="Item1",
                    type="item",
                    coordinate=Coordinate(x=0.5, y=0.5),
                )
            ],
        )

        assert len(analysis.level1_menus) == 1
        assert analysis.level1_menus[0].name == "Menu1"
        assert analysis.current_path == ["Menu1", "Tab1"]
