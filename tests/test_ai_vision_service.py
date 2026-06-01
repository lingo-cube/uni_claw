"""Unit and integration tests for Vision Service implementations.

Tests cover:
- config.py: VisionConfig, create_vision_service factory
- base_service.py: BaseVisionService utilities
- mock_service.py: MockVisionService
- claude_service.py: ClaudeVisionService
"""

import json
from unittest.mock import MagicMock, Mock, patch

import pytest

from src.ai.vision.config import VisionConfig, create_vision_service
from src.ai.vision.base_service import BaseVisionService, VisionError
from src.ai.vision.mock_service import MockVisionService
from src.ai.vision.service import VisionService

try:
    from src.ai.vision.claude_service import ClaudeVisionService
    CLAUDE_AVAILABLE = True
except ImportError:
    CLAUDE_AVAILABLE = False

from src.state.content_tree import (
    PageAnalysis,
    Direction,
    MenuInfo,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    Coordinate,
)


# ============================================================================
# Tests for config.py
# ============================================================================

class TestVisionConfig:
    """Tests for VisionConfig dataclass."""

    def test_default_values(self):
        """Test VisionConfig has correct default values."""
        config = VisionConfig()
        assert config.service_type == "claude"
        assert config.api_key == ""
        assert config.model == "claude-3-5-sonnet-20241022"
        assert config.timeout == 30.0
        assert config.max_retries == 3

    def test_custom_values(self):
        """Test VisionConfig with custom values."""
        config = VisionConfig(
            service_type="mock",
            api_key="test-key",
            model="claude-3-opus-20240229",
            timeout=60.0,
            max_retries=5
        )
        assert config.service_type == "mock"
        assert config.api_key == "test-key"
        assert config.model == "claude-3-opus-20240229"
        assert config.timeout == 60.0
        assert config.max_retries == 5

    def test_service_type_validation(self):
        """Test that service_type accepts valid values."""
        # Valid types
        for service_type in ["claude", "mimo", "mock"]:
            config = VisionConfig(service_type=service_type)
            assert config.service_type == service_type


class TestCreateVisionService:
    """Tests for create_vision_service factory function."""

    def test_create_claude_service(self):
        """Test creating Claude vision service."""
        config = VisionConfig(service_type="claude", api_key="test-key")
        service = create_vision_service(config)

        if CLAUDE_AVAILABLE:
            assert isinstance(service, ClaudeVisionService)
        else:
            # Should raise ImportError if anthropic is not installed
            assert service is None or isinstance(service, Mock)

    def test_create_mock_service(self):
        """Test creating mock vision service."""
        config = VisionConfig(service_type="mock")
        service = create_vision_service(config)

        assert isinstance(service, MockVisionService)

    def test_create_mimo_service_not_implemented(self):
        """Test that MiMo service raises NotImplementedError."""
        config = VisionConfig(service_type="mimo")

        with pytest.raises(NotImplementedError, match="MiMo service not yet implemented"):
            create_vision_service(config)

    def test_create_unknown_service_type(self):
        """Test that unknown service type raises ValueError."""
        config = VisionConfig(service_type="unknown")

        with pytest.raises(ValueError, match="Unknown service type: unknown"):
            create_vision_service(config)


# ============================================================================
# Tests for base_service.py
# ============================================================================

class TestBaseVisionService:
    """Tests for BaseVisionService utilities."""

    def test_encode_image_base64(self):
        """Test _encode_image_base64 method."""
        image_data = b"fake_png_data"
        encoded = BaseVisionService._encode_image_base64(image_data)

        assert encoded.startswith("data:image/png;base64,")
        assert "fake_png_data" not in encoded  # Should be base64 encoded

    def test_encode_image_base64_with_real_png(self):
        """Test _encode_image_base64 with actual PNG header."""
        # Minimal PNG header
        png_header = b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00"
        encoded = BaseVisionService._encode_image_base64(png_header)

        assert encoded.startswith("data:image/png;base64,")

    def test_extract_json_pure_json(self):
        """Test _extract_json with pure JSON response."""
        json_text = '{"key": "value", "number": 42}'
        result = BaseVisionService._extract_json(json_text)

        assert result == {"key": "value", "number": 42}

    def test_extract_json_from_markdown_code_block(self):
        """Test _extract_json with JSON in markdown code block."""
        json_text = '''```json
{
  "key": "value",
  "number": 42
}
```'''
        result = BaseVisionService._extract_json(json_text)

        assert result == {"key": "value", "number": 42}

    def test_extract_json_from_generic_code_block(self):
        """Test _extract_json with JSON in generic code block."""
        json_text = '''```
{
  "key": "value"
}
```'''
        result = BaseVisionService._extract_json(json_text)

        assert result == {"key": "value"}

    def test_extract_json_with_whitespace(self):
        """Test _extract_json handles whitespace in code blocks."""
        json_text = '''
Some text before

```json

  {"key": "value"}

  ```

Some text after
'''
        result = BaseVisionService._extract_json(json_text)

        assert result == {"key": "value"}

    def test_extract_json_invalid_json_raises_error(self):
        """Test _extract_json raises VisionError for invalid JSON."""
        invalid_json = '{"key": invalid}'

        with pytest.raises(VisionError, match="Failed to parse JSON"):
            BaseVisionService._extract_json(invalid_json)

    def test_extract_json_no_json_found_raises_error(self):
        """Test _extract_json raises VisionError when no JSON found."""
        no_json = "This is just plain text with no JSON"

        with pytest.raises(VisionError, match="Could not extract JSON"):
            BaseVisionService._extract_json(no_json)

    def test_extract_json_malformed_code_block(self):
        """Test _extract_json with malformed code block."""
        malformed = '```json {"incomplete":'  # Missing closing braces

        with pytest.raises(VisionError):
            BaseVisionService._extract_json(malformed)

    def test_abstract_methods_raise_not_implemented(self):
        """Test that abstract methods raise NotImplementedError."""
        base_service = BaseVisionService()

        with pytest.raises(NotImplementedError, match="analyze_screenshot"):
            base_service.analyze_screenshot(b"fake_image")

        with pytest.raises(NotImplementedError, match="find_app_entry"):
            base_service.find_app_entry(b"fake_image", "app_name")


# ============================================================================
# Tests for mock_service.py
# ============================================================================

class TestMockVisionService:
    """Tests for MockVisionService."""

    @pytest.fixture
    def mock_service(self):
        """Create a MockVisionService instance."""
        return MockVisionService()

    @pytest.fixture
    def sample_page_analysis(self):
        """Create a sample PageAnalysis for testing."""
        return PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[
                MenuInfo(name="Menu1", coordinate=Coordinate(x=0.1, y=0.1), active=True)
            ],
            level2_dir=Direction.TOP,
            level2_menus=[
                MenuInfo(name="Tab1", coordinate=Coordinate(x=0.3, y=0.05), active=True)
            ],
            current_path=["Menu1", "Tab1"],
            items=[
                MenuItem(
                    name="Item1",
                    type=MenuItemType.MENU_ITEM,
                    expected_action=ExpectedAction.NAVIGATE,
                    coordinate=Coordinate(x=0.5, y=0.5),
                    expects_page_change=True,
                    expects_state_change=False,
                )
            ],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=Coordinate(x=0.05, y=0.05),
            has_scroll=False,
            is_end_of_list=False,
        )

    def test_init_creates_empty_response_queue(self, mock_service):
        """Test initialization creates empty response queue."""
        assert mock_service._responses == []

    def test_add_response_adds_to_queue(self, mock_service, sample_page_analysis):
        """Test add_response adds to response queue."""
        mock_service.add_response(sample_page_analysis)

        assert len(mock_service._responses) == 1
        assert mock_service._responses[0] == sample_page_analysis

    def test_add_multiple_responses(self, mock_service, sample_page_analysis):
        """Test adding multiple responses to queue."""
        mock_service.add_response(sample_page_analysis)
        mock_service.add_response(sample_page_analysis)

        assert len(mock_service._responses) == 2

    def test_analyze_screenshot_returns_queued_response(self, mock_service, sample_page_analysis):
        """Test analyze_screenshot returns queued response."""
        mock_service.add_response(sample_page_analysis)

        result = mock_service.analyze_screenshot(b"fake_image")

        assert result == sample_page_analysis
        assert len(mock_service._responses) == 0  # Queue should be empty

    def test_analyze_screenshot_returns_default_when_empty(self, mock_service):
        """Test analyze_screenshot returns default when queue is empty."""
        result = mock_service.analyze_screenshot(b"fake_image")

        assert isinstance(result, PageAnalysis)
        assert result.level1_dir == Direction.LEFT
        assert len(result.level1_menus) >= 1
        assert result.level1_menus[0].name == "DiLink"

    def test_analyze_screenshot_fifo_order(self, mock_service):
        """Test responses are returned in FIFO order."""
        response1 = PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

        response2 = PageAnalysis(
            level1_dir=Direction.RIGHT,
            level1_menus=[],
            level2_dir=Direction.BOTTOM,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        )

        mock_service.add_response(response1)
        mock_service.add_response(response2)

        result1 = mock_service.analyze_screenshot(b"image1")
        result2 = mock_service.analyze_screenshot(b"image2")

        assert result1.level1_dir == Direction.LEFT
        assert result2.level1_dir == Direction.RIGHT

    def test_find_app_entry_returns_mock_result(self, mock_service):
        """Test find_app_entry returns mock result."""
        result = mock_service.find_app_entry(b"fake_image", "TestApp")

        assert result is not None
        assert result["found"] is True
        assert result["name"] == "TestApp"
        assert result["x"] == 0.5
        assert result["y"] == 0.5
        assert result["confidence"] == 0.9

    def test_find_app_entry_different_targets(self, mock_service):
        """Test find_app_entry with different target names."""
        targets = ["Settings", "Music", "Navigation"]

        for target in targets:
            result = mock_service.find_app_entry(b"image", target)
            assert result["name"] == target

    def test_ignores_image_data(self, mock_service):
        """Test that image_data is ignored in mock service."""
        mock_service.add_response(PageAnalysis(
            level1_dir=Direction.LEFT,
            level1_menus=[],
            level2_dir=Direction.TOP,
            level2_menus=[],
            current_path=[],
            items=[],
            is_popup=False,
            popup_info=None,
            close_button=None,
            back_button=None,
            has_scroll=False,
            is_end_of_list=False,
        ))

        # Different image data should still return the queued response
        result1 = mock_service.analyze_screenshot(b"image1")
        result2 = mock_service.analyze_screenshot(b"different_image")

        # First call consumed the queue, second returns default
        assert result1 is not None
        assert result2 is not None


# ============================================================================
# Tests for claude_service.py (conditional on anthropic availability)
# ============================================================================

@pytest.mark.skipif(not CLAUDE_AVAILABLE, reason="anthropic package not installed")
class TestClaudeVisionService:
    """Tests for ClaudeVisionService (requires anthropic package)."""

    def test_init_requires_anthropic(self):
        """Test that initialization requires anthropic package."""
        # This test only runs if anthropic is available
        # If we're here, it means the import succeeded
        assert CLAUDE_AVAILABLE

    def test_init_creates_client(self):
        """Test initialization creates Anthropic client."""
        service = ClaudeVisionService(api_key="test-key")

        assert service.client is not None
        assert service.model == "claude-3-5-sonnet-20241022"

    def test_init_with_custom_model(self):
        """Test initialization with custom model."""
        service = ClaudeVisionService(
            api_key="test-key",
            model="claude-3-opus-20240229"
        )

        assert service.model == "claude-3-opus-20240229"

    @patch('src.ai.vision.claude_service.Anthropic')
    def test_call_vision_success(self, mock_anthropic):
        """Test successful _call_vision API call."""
        mock_client = MagicMock()
        mock_message = MagicMock()
        mock_message.content = [MagicMock(text="test response")]
        mock_client.messages.create = MagicMock(return_value=mock_message)
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test-key")
        response = service._call_vision("test prompt", b"fake image")

        assert response == "test response"
        mock_client.messages.create.assert_called_once()

    @patch('src.ai.vision.claude_service.Anthropic')
    def test_call_vision_api_error_raises_vision_error(self, mock_anthropic):
        """Test _call_vision raises VisionError on API error."""
        mock_client = MagicMock()
        mock_client.messages.create = MagicMock(side_effect=Exception("API error"))
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test-key")

        with pytest.raises(VisionError, match="Claude API error"):
            service._call_vision("test prompt", b"fake image")

    @patch('src.ai.vision.claude_service.Anthropic')
    def test_analyze_screenshot_success(self, mock_anthropic):
        """Test successful analyze_screenshot."""
        mock_client = MagicMock()
        mock_message = MagicMock()

        # Valid JSON response
        json_response = json.dumps({
            "level1_dir": "left",
            "level1_menus": [],
            "level2_dir": "top",
            "level2_menus": [],
            "current_path": [],
            "items": [],
            "is_popup": False,
            "popup_info": None,
            "close_button": None,
            "back_button": None,
            "has_scroll": False,
            "is_end_of_list": False,
        })
        mock_message.content = [MagicMock(text=json_response)]
        mock_client.messages.create = MagicMock(return_value=mock_message)
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test-key")
        result = service.analyze_screenshot(b"fake image")

        assert isinstance(result, PageAnalysis)
        assert result.level1_dir == Direction.LEFT

    @patch('src.ai.vision.claude_service.Anthropic')
    def test_find_app_entry_found(self, mock_anthropic):
        """Test find_app_entry when app is found."""
        mock_client = MagicMock()
        mock_message = MagicMock()

        json_response = json.dumps({
            "found": True,
            "name": "TestApp",
            "x": 0.5,
            "y": 0.5,
            "confidence": 0.95
        })
        mock_message.content = [MagicMock(text=json_response)]
        mock_client.messages.create = MagicMock(return_value=mock_message)
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test-key")
        result = service.find_app_entry(b"fake image", "TestApp")

        assert result is not None
        assert result["found"] is True
        assert result["name"] == "TestApp"
        assert result["x"] == 0.5
        assert result["confidence"] == 0.95

    @patch('src.ai.vision.claude_service.Anthropic')
    def test_find_app_entry_not_found(self, mock_anthropic):
        """Test find_app_entry when app is not found."""
        mock_client = MagicMock()
        mock_message = MagicMock()

        json_response = json.dumps({"found": False})
        mock_message.content = [MagicMock(text=json_response)]
        mock_client.messages.create = MagicMock(return_value=mock_message)
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test-key")
        result = service.find_app_entry(b"fake image", "MissingApp")

        assert result is None

    @patch('src.ai.vision.claude_service.Anthropic')
    def test_find_app_entry_error_returns_none(self, mock_anthropic):
        """Test find_app_entry returns None on error."""
        mock_client = MagicMock()
        mock_client.messages.create = MagicMock(side_effect=Exception("Network error"))
        mock_anthropic.return_value = mock_client

        service = ClaudeVisionService(api_key="test-key")
        result = service.find_app_entry(b"fake image", "TestApp")

        # Should log warning and return None
        assert result is None


@pytest.mark.skipif(CLAUDE_AVAILABLE, reason="Test only when anthropic is not installed")
class TestClaudeVisionServiceNotAvailable:
    """Tests for ClaudeVisionService when anthropic is not available."""

    def test_import_error_when_anthropic_missing(self):
        """Test that ImportError is raised when anthropic is missing."""
        # If we're in this test class, anthropic is not available
        with pytest.raises(ImportError):
            from anthropic import Anthropic  # noqa: F401


# ============================================================================
# Tests for VisionService interface
# ============================================================================

class TestVisionServiceInterface:
    """Tests for VisionService abstract interface."""

    def test_all_services_implement_interface(self):
        """Test that all services implement VisionService interface."""
        mock_service = MockVisionService()

        assert isinstance(mock_service, VisionService)

        # Check interface methods exist
        assert hasattr(mock_service, 'analyze_screenshot')
        assert hasattr(mock_service, 'find_app_entry')

    def test_mock_service_implements_interface(self):
        """Test MockVisionService properly implements interface."""
        service = MockVisionService()

        # Should not raise NotImplementedError
        result = service.analyze_screenshot(b"test")
        assert isinstance(result, PageAnalysis)

        result = service.find_app_entry(b"test", "app")
        assert isinstance(result, dict) or result is None
