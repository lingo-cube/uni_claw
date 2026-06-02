"""Unit tests for FlattenedVisionService."""

import json
from unittest.mock import Mock, MagicMock, patch

import pytest

from src.ai.vision.flattened_vision_service import (
    FlattenedVisionService,
    VisionAnalysisResult,
)
from src.ai.vision.multimodal_analyzer import MultimodalAnalysisResult
from src.ai.vision.page_analysis_assembler import AssemblyResult
from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.type_hint import TypeHint
from src.models.vision.selection_state import SelectionState
from src.ai.vision.cache import InMemoryScreenCache, InMemoryPageAnalysisCache
from src.ai.vision.legacy_vision_service import LegacyVisionService
from src.state.content_tree import PageAnalysis


class MockResponse:
    """Mock AI response."""

    def __init__(self, content: str, input_tokens: int = 100, output_tokens: int = 200):
        self.content = content
        self.usage = Mock()
        self.usage.input_tokens = input_tokens
        self.usage.output_tokens = output_tokens


class MockAIProvider:
    """Mock AI provider for testing."""

    def __init__(self, response_content: str = None, latency_ms: float = 100):
        self.response_content = response_content or self._default_response()
        self.latency_ms = latency_ms
        self.call_count = 0

    def complete(self, prompt, image_data=None, model=None, response_format=None):
        """Mock complete method."""
        import time
        self.call_count += 1
        time.sleep(self.latency_ms / 1000)  # Simulate latency
        return MockResponse(self.response_content)

    def _default_response(self) -> str:
        """Return default mock response for multimodal analysis."""
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
            ],
            'screen_hints': {
                'top_bar_text': 'Settings',
                'layout_type': 'split_pane',
                'overlay_detected': False,
                'scroll_detected': True,
            },
        })


class MockPageAnalysis:
    """Mock PageAnalysis for testing."""

    def __init__(self):
        self.current_path = ["Settings", "WiFi"]
        self.items = [
            {"id": "item1", "text": "WiFi", "clickable": True},
            {"id": "item2", "text": "Mobile Data", "clickable": True},
        ]


class TestFlattenedVisionServiceCreation:
    """Tests for FlattenedVisionService creation and initialization."""

    def test_creation_with_required_components(self):
        """Test creating service with required components."""
        multimodal_analyzer = Mock()
        assembler = Mock()

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
        )

        assert service.multimodal_analyzer == multimodal_analyzer
        assert service.assembler == assembler
        assert service.screen_cache is None
        assert service.page_analysis_cache is None
        assert service.legacy_service is None

    def test_creation_with_caches(self):
        """Test creating service with cache instances."""
        multimodal_analyzer = Mock()
        assembler = Mock()
        screen_cache = InMemoryScreenCache(ttl=300)
        page_analysis_cache = InMemoryPageAnalysisCache(ttl=600)

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            screen_cache=screen_cache,
            page_analysis_cache=page_analysis_cache,
        )

        assert service.screen_cache == screen_cache
        assert service.page_analysis_cache == page_analysis_cache

    def test_creation_with_legacy_fallback(self):
        """Test creating service with legacy fallback."""
        multimodal_analyzer = Mock()
        assembler = Mock()
        legacy_service = Mock(spec=LegacyVisionService)

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            legacy_service=legacy_service,
        )

        assert service.legacy_service == legacy_service


class TestAnalyzeScreenshot:
    """Tests for analyze_screenshot() method."""

    def test_analyze_screenshot_success(self):
        """Test successful screenshot analysis with mocked components."""
        # Setup mocks
        flattened_screen = FlattenedScreen(
            elements=[
                FlattenedElement(
                    id=0,
                    text="WiFi",
                    type_hint=TypeHint.CLICKABLE_TEXT,
                    bbox=BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05),
                    region="left_panel",
                )
            ],
            screen_hints={
                "top_bar_text": "Settings",
                "layout_type": "split_pane",
            }
        )

        page_analysis = MockPageAnalysis()

        multimodal_analyzer = Mock()
        multimodal_analyzer.analyze = Mock(return_value=MultimodalAnalysisResult(
            flattened_screen=flattened_screen,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
            cached=False,
        ))

        assembler = Mock()
        assembler.assemble = Mock(return_value=AssemblyResult(
            page_analysis=page_analysis,
            latency_ms=150.0,
            input_tokens=200,
            output_tokens=300,
            cached=False,
        ))

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
        )

        # Execute
        result = service.analyze_screenshot(b"fake_image_data")

        # Verify
        assert isinstance(result, VisionAnalysisResult)
        assert result.page_analysis == page_analysis
        assert result.total_latency_ms == 250.0
        assert result.multimodal_latency_ms == 100.0
        assert result.assembler_latency_ms == 150.0
        assert result.total_tokens == 400
        assert result.multimodal_tokens == 100
        assert result.assembler_tokens == 300
        assert result.multimodal_cached is False
        assert result.assembler_cached is False

        # Verify calls
        multimodal_analyzer.analyze.assert_called_once_with(b"fake_image_data")
        assembler.assemble.assert_called_once()

    def test_analyze_screenshot_with_context(self):
        """Test analysis with traversal context."""
        flattened_screen = FlattenedScreen(elements=[], screen_hints={})
        page_analysis = MockPageAnalysis()

        multimodal_analyzer = Mock()
        multimodal_analyzer.analyze = Mock(return_value=MultimodalAnalysisResult(
            flattened_screen=flattened_screen,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
            cached=False,
        ))

        assembler = Mock()
        assembler.assemble = Mock(return_value=AssemblyResult(
            page_analysis=page_analysis,
            latency_ms=150.0,
            input_tokens=200,
            output_tokens=300,
            cached=False,
        ))

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
        )

        context = {"current_path": ["Settings"], "previous_action": "click"}
        result = service.analyze_screenshot(b"fake_image_data", context=context)

        assert result.page_analysis == page_analysis
        # Verify context was passed to assembler
        assembler.assemble.assert_called_once_with(flattened_screen, context)

    def test_analyze_screenshot_empty_image_data(self):
        """Test that empty image data raises ValueError."""
        service = FlattenedVisionService(
            multimodal_analyzer=Mock(),
            assembler=Mock(),
        )

        with pytest.raises(ValueError, match="image_data cannot be empty"):
            service.analyze_screenshot(b"")

    def test_analyze_screenshot_multimodal_cached(self):
        """Test analysis with multimodal cache hit."""
        flattened_screen = FlattenedScreen(elements=[], screen_hints={})
        page_analysis = MockPageAnalysis()

        screen_cache = InMemoryScreenCache(ttl=300)
        screen_cache.set(b"cached_image", flattened_screen)

        multimodal_analyzer = Mock()
        assembler = Mock()
        assembler.assemble = Mock(return_value=AssemblyResult(
            page_analysis=page_analysis,
            latency_ms=150.0,
            input_tokens=200,
            output_tokens=300,
            cached=False,
        ))

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            screen_cache=screen_cache,
        )

        result = service.analyze_screenshot(b"cached_image")

        # Verify cache hit - multimodal analyzer not called
        assert result.multimodal_cached is True
        assert result.multimodal_latency_ms == 0
        multimodal_analyzer.analyze.assert_not_called()

    def test_analyze_screenshot_assembler_cached(self):
        """Test analysis with assembler cache hit."""
        flattened_screen = FlattenedScreen(elements=[], screen_hints={})
        page_analysis = MockPageAnalysis()

        page_cache = InMemoryPageAnalysisCache(ttl=600)

        multimodal_analyzer = Mock()
        multimodal_analyzer.analyze = Mock(return_value=MultimodalAnalysisResult(
            flattened_screen=flattened_screen,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
            cached=False,
        ))

        assembler = Mock()
        assembler.assemble = Mock(return_value=AssemblyResult(
            page_analysis=page_analysis,
            latency_ms=150.0,
            input_tokens=200,
            output_tokens=300,
            cached=False,
        ))

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            page_analysis_cache=page_cache,
        )

        # First call to populate cache
        service.analyze_screenshot(b"test_image")

        # Reset mock to verify second call doesn't invoke assembler
        assembler.assemble.reset_mock()

        # Second call should hit cache
        result = service.analyze_screenshot(b"test_image")

        # Verify cache hit
        assert result.assembler_cached is True
        assert result.assembler_latency_ms == 0
        assembler.assemble.assert_not_called()

    def test_analyze_screenshot_both_cached(self):
        """Test analysis with both caches hit."""
        flattened_screen = FlattenedScreen(elements=[], screen_hints={})
        page_analysis = MockPageAnalysis()

        screen_cache = InMemoryScreenCache(ttl=300)
        screen_cache.set(b"cached_image", flattened_screen)

        page_cache = InMemoryPageAnalysisCache(ttl=600)
        # Pre-populate page cache by generating key
        cache_key = page_cache.generate_key(
            flattened_screen.to_dict(),
            {}
        )
        page_cache.set(cache_key, page_analysis)

        service = FlattenedVisionService(
            multimodal_analyzer=Mock(),
            assembler=Mock(),
            screen_cache=screen_cache,
            page_analysis_cache=page_cache,
        )

        result = service.analyze_screenshot(b"cached_image")

        # Both should be cached
        assert result.multimodal_cached is True
        assert result.assembler_cached is True
        assert result.total_latency_ms == 0
        assert result.total_tokens == 0


class TestFallbackToLegacy:
    """Tests for fallback to legacy service."""

    def test_fallback_on_multimodal_failure(self):
        """Test fallback when multimodal analyzer fails."""
        page_analysis = MockPageAnalysis()

        multimodal_analyzer = Mock()
        multimodal_analyzer.analyze = Mock(side_effect=Exception("AI failure"))

        legacy_service = Mock(spec=LegacyVisionService)
        legacy_service.analyze_screenshot = Mock(return_value=page_analysis)

        assembler = Mock()

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            legacy_service=legacy_service,
        )

        result = service.analyze_screenshot(b"test_image")

        # Verify fallback was called
        assert result.page_analysis == page_analysis
        legacy_service.analyze_screenshot.assert_called_once_with(b"test_image", None)

    def test_fallback_on_assembler_failure(self):
        """Test fallback when assembler fails."""
        flattened_screen = FlattenedScreen(elements=[], screen_hints={})
        page_analysis = MockPageAnalysis()

        multimodal_analyzer = Mock()
        multimodal_analyzer.analyze = Mock(return_value=MultimodalAnalysisResult(
            flattened_screen=flattened_screen,
            latency_ms=100.0,
            input_tokens=50,
            output_tokens=100,
            cached=False,
        ))

        assembler = Mock()
        assembler.assemble = Mock(side_effect=Exception("Assembly failed"))

        legacy_service = Mock(spec=LegacyVisionService)
        legacy_service.analyze_screenshot = Mock(return_value=page_analysis)

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            legacy_service=legacy_service,
        )

        result = service.analyze_screenshot(b"test_image")

        # Verify fallback was called
        assert result.page_analysis == page_analysis
        legacy_service.analyze_screenshot.assert_called_once()

    def test_fallback_with_context(self):
        """Test fallback passes context to legacy service."""
        page_analysis = MockPageAnalysis()

        multimodal_analyzer = Mock()
        multimodal_analyzer.analyze = Mock(side_effect=Exception("Failed"))

        legacy_service = Mock(spec=LegacyVisionService)
        legacy_service.analyze_screenshot = Mock(return_value=page_analysis)

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=Mock(),
            legacy_service=legacy_service,
        )

        context = {"current_path": ["Settings"]}
        result = service.analyze_screenshot(b"test_image", context=context)

        # Verify context was passed
        legacy_service.analyze_screenshot.assert_called_once_with(b"test_image", context)
        assert result.page_analysis == page_analysis

    def test_error_without_legacy_service(self):
        """Test that error propagates when no legacy service available."""
        multimodal_analyzer = Mock()
        multimodal_analyzer.analyze = Mock(side_effect=Exception("AI failure"))

        service = FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=Mock(),
            legacy_service=None,
        )

        with pytest.raises(Exception, match="AI failure"):
            service.analyze_screenshot(b"test_image")


class TestCacheKeyGeneration:
    """Tests for cache key generation."""

    def test_generate_assembly_cache_key(self):
        """Test cache key generation for page assembly."""
        flattened_screen = FlattenedScreen(
            elements=[
                FlattenedElement(
                    id=0,
                    text="Test",
                    type_hint=TypeHint.TEXT,
                    bbox=BoundingBox(x=0.0, y=0.0, w=0.1, h=0.1),
                )
            ],
            screen_hints={"layout_type": "split_pane"}
        )

        service = FlattenedVisionService(
            multimodal_analyzer=Mock(),
            assembler=Mock(),
        )

        key1 = service._generate_assembly_cache_key(flattened_screen, {"path": "test"})
        key2 = service._generate_assembly_cache_key(flattened_screen, {"path": "test"})

        # Same inputs should generate same key
        assert key1 == key2

    def test_different_context_generates_different_key(self):
        """Test that different context generates different cache key."""
        flattened_screen = FlattenedScreen(elements=[], screen_hints={})

        service = FlattenedVisionService(
            multimodal_analyzer=Mock(),
            assembler=Mock(),
        )

        key1 = service._generate_assembly_cache_key(flattened_screen, {"path": "test1"})
        key2 = service._generate_assembly_cache_key(flattened_screen, {"path": "test2"})

        assert key1 != key2

    def test_none_context_handled(self):
        """Test that None context is handled gracefully."""
        flattened_screen = FlattenedScreen(elements=[], screen_hints={})

        service = FlattenedVisionService(
            multimodal_analyzer=Mock(),
            assembler=Mock(),
        )

        # Should not raise
        key = service._generate_assembly_cache_key(flattened_screen, None)
        assert isinstance(key, str)


class TestVisionAnalysisResult:
    """Tests for VisionAnalysisResult dataclass."""

    def test_creation(self):
        """Test creating VisionAnalysisResult."""
        page_analysis = MockPageAnalysis()
        result = VisionAnalysisResult(
            page_analysis=page_analysis,
            total_latency_ms=300.0,
            multimodal_latency_ms=100.0,
            assembler_latency_ms=200.0,
            total_tokens=500,
            multimodal_tokens=150,
            assembler_tokens=350,
            multimodal_cached=False,
            assembler_cached=True,
        )

        assert result.page_analysis == page_analysis
        assert result.total_latency_ms == 300.0
        assert result.multimodal_latency_ms == 100.0
        assert result.assembler_latency_ms == 200.0
        assert result.total_tokens == 500
        assert result.multimodal_tokens == 150
        assert result.assembler_tokens == 350
        assert result.multimodal_cached is False
        assert result.assembler_cached is True
