"""End-to-end integration tests for the vision service pipeline.

These tests verify the complete two-step vision pipeline including:
1. Multimodal visual perception → FlattenedScreen
2. Text-based logical assembly → PageAnalysis
3. Caching behavior
4. Fallback mechanisms
5. Mode switching
"""

import json
from unittest.mock import Mock, MagicMock

import pytest

from src.ai.vision.vision_service_factory import VisionServiceFactory
from src.ai.vision.flattened_vision_service import (
    FlattenedVisionService,
    VisionAnalysisResult,
)
from src.ai.vision.legacy_vision_service import LegacyVisionService
from src.ai.vision.multimodal_analyzer import MultimodalAnalysisResult
from src.ai.vision.page_analysis_assembler import AssemblyResult
from src.models.vision.flattened_screen import FlattenedScreen
from src.models.vision.flattened_element import FlattenedElement
from src.models.vision.bounding_box import BoundingBox
from src.models.vision.type_hint import TypeHint
from src.models.vision.selection_state import SelectionState
from src.ai.vision.cache import InMemoryScreenCache, InMemoryPageAnalysisCache


class MockResponse:
    """Mock AI response with usage tracking."""

    def __init__(self, content: str, input_tokens: int = 100, output_tokens: int = 200):
        self.content = content
        self.usage = Mock()
        self.usage.input_tokens = input_tokens
        self.usage.output_tokens = output_tokens


class MockAIProvider:
    """Mock AI provider that returns predefined responses."""

    def __init__(self, multimodal_response=None, text_response=None):
        """Initialize with optional responses.

        Args:
            multimodal_response: JSON string for multimodal analysis
            text_response: JSON string for text assembly
        """
        self.multimodal_response = multimodal_response or self._default_multimodal_response()
        self.text_response = text_response or self._default_text_response()
        self.call_count = 0
        self.last_call_type = None

    def complete(self, prompt, image_data=None, model=None, response_format=None):
        """Mock complete method."""
        self.call_count += 1

        # Determine response type based on call signature
        if image_data is not None:
            self.last_call_type = "multimodal"
            return MockResponse(self.multimodal_response, input_tokens=500, output_tokens=800)
        else:
            self.last_call_type = "text"
            return MockResponse(self.text_response, input_tokens=1000, output_tokens=1500)

    def _default_multimodal_response(self) -> str:
        """Return default mock response for multimodal analysis."""
        return json.dumps({
            'elements': [
                {
                    'id': 0,
                    'text': 'WiFi',
                    'type_hint': 'clickable_text',
                    'bbox': {'x': 0.1, 'y': 0.15, 'w': 0.25, 'h': 0.06},
                    'region': 'left_panel',
                    'selection_state': 'selected',
                    'visual_state': {'bold': True},
                    'confidence': 0.95,
                },
                {
                    'id': 1,
                    'text': 'Bluetooth',
                    'type_hint': 'clickable_text',
                    'bbox': {'x': 0.1, 'y': 0.25, 'w': 0.25, 'h': 0.06},
                    'region': 'left_panel',
                    'selection_state': 'normal',
                    'confidence': 0.92,
                },
                {
                    'id': 2,
                    'text': 'Mobile Data',
                    'type_hint': 'switch',
                    'bbox': {'x': 0.35, 'y': 0.2, 'w': 0.5, 'h': 0.06},
                    'region': 'content_area',
                    'selection_state': 'normal',
                    'visual_state': {'switch_state': 'on'},
                    'confidence': 0.98,
                },
                {
                    'id': 3,
                    'text': 'General',
                    'type_hint': 'clickable_text',
                    'bbox': {'x': 0.35, 'y': 0.08, 'w': 0.15, 'h': 0.05},
                    'region': 'tabs',
                    'selection_state': 'selected',
                    'visual_state': {'has_indicator': 'underline'},
                    'confidence': 0.90,
                },
            ],
            'screen_hints': {
                'top_bar_text': 'Settings',
                'layout_type': 'split_pane',
                'overlay_detected': False,
                'scroll_detected': True,
            },
        })

    def _default_text_response(self) -> str:
        """Return default mock response for text assembly."""
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
            ],
            'is_popup': False,
            'popup_info': None,
            'close_button': None,
            'back_button': {'x': 0.05, 'y': 0.05},
            'has_scroll': True,
            'is_end_of_list': False,
        })


@pytest.fixture
def sample_image_data():
    """Create sample image data for testing."""
    # In real tests, this would be actual PNG data
    return b"fake_png_image_data_for_testing"


class TestCompletePipelineFlow:
    """Tests for the complete two-step vision pipeline."""

    def test_complete_pipeline_from_image_to_analysis(self, sample_image_data):
        """Test complete pipeline from screenshot to PageAnalysis."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': False},
        )

        result = service.analyze_screenshot(sample_image_data)

        # Verify complete result
        assert isinstance(result, VisionAnalysisResult)
        assert result.page_analysis is not None
        assert result.total_latency_ms >= 0
        assert result.total_tokens > 0

        # Verify both steps were called
        assert provider.call_count == 2  # One multimodal, one text

    def test_pipeline_preserves_element_information(self, sample_image_data):
        """Test that element information is preserved through pipeline."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': False},
        )

        result = service.analyze_screenshot(sample_image_data)

        pa = result.page_analysis

        # Verify elements from multimodal step are reflected in final result
        assert len(pa.level1_menus) >= 1
        assert pa.level1_menus[0].name == 'WiFi'
        assert pa.current_path == ['WiFi', 'General']

    def test_pipeline_with_context(self, sample_image_data):
        """Test pipeline with traversal context."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': False},
        )

        context = {
            'current_path': ['Settings'],
            'previous_action': 'click',
            'navigation_history': ['main_menu', 'Settings'],
        }

        result = service.analyze_screenshot(sample_image_data, context=context)

        # Verify result includes context consideration
        assert result.page_analysis is not None

    def test_pipeline_metrics_tracking(self, sample_image_data):
        """Test that pipeline metrics are tracked correctly."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': False},
        )

        result = service.analyze_screenshot(sample_image_data)

        # Verify metrics
        assert result.multimodal_latency_ms > 0
        assert result.assembler_latency_ms > 0
        assert result.total_latency_ms == result.multimodal_latency_ms + result.assembler_latency_ms
        assert result.multimodal_tokens > 0
        assert result.assembler_tokens > 0
        assert result.total_tokens == result.multimodal_tokens + result.assembler_tokens


class TestCachingBehavior:
    """Tests for caching behavior in the pipeline."""

    def test_screen_cache_hit_skips_multimodal_call(self, sample_image_data):
        """Test that screen cache hit skips multimodal analysis."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True, 'screen_cache_ttl': 300},
        )

        # First call - cache miss
        result1 = service.analyze_screenshot(sample_image_data)
        assert not result1.multimodal_cached
        call_count_after_first = provider.call_count

        # Second call - should hit screen cache (and page cache too, since same context)
        result2 = service.analyze_screenshot(sample_image_data)
        assert result2.multimodal_cached
        assert result2.multimodal_latency_ms == 0

        # Since both caches hit on second call (same screen and context),
        # no additional AI calls should be made
        assert provider.call_count == call_count_after_first

    def test_page_analysis_cache_hit_skips_assembly(self, sample_image_data):
        """Test that page analysis cache hit skips assembly."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True, 'page_analysis_cache_ttl': 600},
        )

        # First call
        result1 = service.analyze_screenshot(sample_image_data)
        call_count_after_first = provider.call_count

        # Second call with same context - should hit page cache
        result2 = service.analyze_screenshot(sample_image_data, context={})
        assert result2.assembler_cached
        assert result2.assembler_latency_ms == 0

        # No additional calls should be made (both caches hit)
        assert provider.call_count == call_count_after_first

    def test_different_context_bypasses_page_cache(self, sample_image_data):
        """Test that different context bypasses page analysis cache."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True},
        )

        # First call
        service.analyze_screenshot(sample_image_data, context={'path': 'A'})
        call_count_after_first = provider.call_count

        # Second call with different context - should miss page cache
        result = service.analyze_screenshot(sample_image_data, context={'path': 'B'})
        assert not result.assembler_cached

        # Should have made additional calls
        assert provider.call_count > call_count_after_first

    def test_cache_expiration(self, sample_image_data):
        """Test that cache entries expire correctly."""
        import time

        provider = MockAIProvider()

        # Use very short TTL
        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True, 'screen_cache_ttl': 1},
        )

        # First call
        result1 = service.analyze_screenshot(sample_image_data)
        assert not result1.multimodal_cached

        # Wait for cache to expire
        time.sleep(1.1)

        # Second call should miss expired cache
        result2 = service.analyze_screenshot(sample_image_data)
        assert not result2.multimodal_cached

    def test_cache_clear(self, sample_image_data):
        """Test that cache can be cleared."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True},
        )

        # Populate cache
        service.analyze_screenshot(sample_image_data)

        # Clear caches
        service.screen_cache.clear()
        service.page_analysis_cache.clear()

        # Next call should be cache miss
        result = service.analyze_screenshot(sample_image_data)
        assert not result.multimodal_cached
        assert not result.assembler_cached


class TestFallbackMechanism:
    """Tests for fallback to legacy service."""

    def test_fallback_on_multimodal_failure(self, sample_image_data):
        """Test fallback when multimodal analyzer fails."""
        class FailingProvider:
            def __init__(self):
                self.call_count = 0

            def complete(self, *args, **kwargs):
                self.call_count += 1
                if self.call_count == 1:
                    # First call (multimodal) fails
                    raise Exception("Multimodal analysis failed")
                else:
                    # Subsequent calls succeed
                    return MockResponse("{}", input_tokens=100, output_tokens=200)

        provider = FailingProvider()
        legacy_service = Mock(spec=LegacyVisionService)
        legacy_service.analyze_screenshot = Mock(return_value=MockPageAnalysis())

        # Create service with fallback
        service = FlattenedVisionService(
            multimodal_analyzer=Mock(analyze=Mock(side_effect=Exception("Failed"))),
            assembler=Mock(),
            legacy_service=legacy_service,
        )

        result = service.analyze_screenshot(sample_image_data)

        # Verify fallback was called
        assert result.page_analysis is not None
        legacy_service.analyze_screenshot.assert_called_once()

    def test_fallback_on_assembler_failure(self, sample_image_data):
        """Test fallback when assembler fails."""
        legacy_service = Mock(spec=LegacyVisionService)
        legacy_service.analyze_screenshot = Mock(return_value=MockPageAnalysis())

        # Create service with fallback
        service = FlattenedVisionService(
            multimodal_analyzer=Mock(
                analyze=Mock(return_value=MultimodalAnalysisResult(
                    flattened_screen=FlattenedScreen(elements=[], screen_hints={}),
                    latency_ms=100,
                    input_tokens=100,
                    output_tokens=100,
                ))
            ),
            assembler=Mock(assemble=Mock(side_effect=Exception("Assembly failed"))),
            legacy_service=legacy_service,
        )

        result = service.analyze_screenshot(sample_image_data)

        # Verify fallback was called
        assert result.page_analysis is not None
        legacy_service.analyze_screenshot.assert_called_once()

    def test_no_fallback_when_legacy_unavailable(self, sample_image_data):
        """Test that error propagates when no legacy service."""
        service = FlattenedVisionService(
            multimodal_analyzer=Mock(analyze=Mock(side_effect=Exception("Failed"))),
            assembler=Mock(),
            legacy_service=None,
        )

        with pytest.raises(Exception, match="Failed"):
            service.analyze_screenshot(sample_image_data)


class MockPageAnalysis:
    """Mock PageAnalysis for testing."""

    def __init__(self):
        self.current_path = ["Settings", "WiFi"]
        self.items = []
        self.level1_menus = []
        self.level2_menus = []
        self.is_popup = False
        self.popup_info = None
        self.back_button = None
        self.close_button = None
        self.has_scroll = False
        self.is_end_of_list = False


class TestModeSwitching:
    """Tests for switching between vision service modes."""

    def test_legacy_mode_creation(self):
        """Test creating legacy mode service."""
        service = VisionServiceFactory.create(mode="legacy")
        assert isinstance(service, LegacyVisionService)

    def test_flattened_mode_creation(self):
        """Test creating flattened mode service."""
        provider = MockAIProvider()
        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
        )
        assert isinstance(service, FlattenedVisionService)

    def test_flattened_mode_with_cache_disabled(self):
        """Test flattened mode with cache disabled."""
        provider = MockAIProvider()
        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': False, 'enable_fallback': False},
        )

        assert service.screen_cache is None
        assert service.page_analysis_cache is None
        assert service.legacy_service is None

    def test_flattened_mode_with_fallback_disabled(self):
        """Test flattened mode with fallback disabled."""
        provider = MockAIProvider()
        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_fallback': False},
        )

        assert service.legacy_service is None

    def test_invalid_mode_raises_error(self):
        """Test that invalid mode raises error."""
        provider = MockAIProvider()
        with pytest.raises(ValueError, match="Invalid mode"):
            VisionServiceFactory.create(mode="invalid", ai_provider=provider)


class TestErrorHandling:
    """Tests for error handling in the pipeline."""

    def test_empty_image_data_raises_error(self):
        """Test that empty image data raises ValueError."""
        provider = MockAIProvider()
        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
        )

        with pytest.raises(ValueError, match="image_data cannot be empty"):
            service.analyze_screenshot(b"")

    def test_none_image_data_raises_error(self):
        """Test that None image data raises error."""
        provider = MockAIProvider()
        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
        )

        with pytest.raises((ValueError, TypeError)):
            service.analyze_screenshot(None)

    def test_malformed_multimodal_response_fallback(self):
        """Test handling of malformed multimodal response."""
        class BadResponseProvider:
            def complete(self, *args, **kwargs):
                return MockResponse("invalid json {{{", input_tokens=100, output_tokens=200)

        legacy_service = Mock(spec=LegacyVisionService)
        legacy_service.analyze_screenshot = Mock(return_value=MockPageAnalysis())

        service = FlattenedVisionService(
            multimodal_analyzer=Mock(analyze=Mock(side_effect=Exception("Parse error"))),
            assembler=Mock(),
            legacy_service=legacy_service,
        )

        result = service.analyze_screenshot(b"test_data")

        # Should fallback to legacy
        assert result.page_analysis is not None

    def test_malformed_assembler_response_fallback(self):
        """Test handling of malformed assembler response."""
        class BadResponseProvider:
            def complete(self, *args, **kwargs):
                return MockResponse("invalid json {{{", input_tokens=100, output_tokens=200)

        legacy_service = Mock(spec=LegacyVisionService)
        legacy_service.analyze_screenshot = Mock(return_value=MockPageAnalysis())

        service = FlattenedVisionService(
            multimodal_analyzer=Mock(
                analyze=Mock(return_value=MultimodalAnalysisResult(
                    flattened_screen=FlattenedScreen(elements=[], screen_hints={}),
                    latency_ms=100,
                    input_tokens=100,
                    output_tokens=100,
                ))
            ),
            assembler=Mock(assemble=Mock(side_effect=Exception("Parse error"))),
            legacy_service=legacy_service,
        )

        result = service.analyze_screenshot(b"test_data")

        # Should fallback to legacy
        assert result.page_analysis is not None


class TestRealWorldScenarios:
    """Tests for real-world usage scenarios."""

    def test_repeated_same_screenshot_caching(self, sample_image_data):
        """Test caching behavior with repeated same screenshot."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True},
        )

        # First call - full pipeline
        result1 = service.analyze_screenshot(sample_image_data)
        initial_calls = provider.call_count
        assert not result1.multimodal_cached
        assert not result1.assembler_cached

        # Same screenshot repeated - both caches should hit
        result2 = service.analyze_screenshot(sample_image_data)
        assert result2.multimodal_cached
        assert result2.assembler_cached

        # No additional AI calls should be made
        assert provider.call_count == initial_calls

    def test_different_screenshots_no_cache_collision(self):
        """Test that different screenshots don't share cache."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True},
        )

        # Analyze different screenshots
        result1 = service.analyze_screenshot(b"screen_1_data")
        calls_after_1 = provider.call_count

        result2 = service.analyze_screenshot(b"screen_2_data")
        calls_after_2 = provider.call_count

        # Both should miss cache
        assert not result1.multimodal_cached
        assert not result2.multimodal_cached
        assert calls_after_2 > calls_after_1

    def test_traversal_sequence_with_context(self):
        """Test a realistic traversal sequence with context changes."""
        provider = MockAIProvider()

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config={'enable_cache': True},
        )

        screen_data = b"settings_screen"

        # Simulate traversal: Settings → WiFi → Configure
        contexts = [
            {'current_path': []},
            {'current_path': ['Settings']},
            {'current_path': ['Settings', 'WiFi']},
        ]

        results = []
        for context in contexts:
            result = service.analyze_screenshot(screen_data, context=context)
            results.append(result)

        # Each context change should trigger new assembly
        # But multimodal should be cached after first call
        assert not results[0].multimodal_cached
        assert results[1].multimodal_cached  # Same screen
        assert results[2].multimodal_cached  # Same screen

        # Context changes mean page cache misses
        assert not results[0].assembler_cached
        assert not results[1].assembler_cached
        assert not results[2].assembler_cached


class TestConfigurationValidation:
    """Tests for configuration validation."""

    def test_flattened_mode_requires_ai_provider(self):
        """Test that flattened mode requires AI provider."""
        with pytest.raises(RuntimeError, match="ai_provider is required"):
            VisionServiceFactory.create(mode="flattened", ai_provider=None)

    def test_custom_model_configuration(self):
        """Test custom model configuration."""
        provider = MockAIProvider()

        config = {
            'multimodal_model': 'claude-3-opus-20240229',
            'text_model': 'deepseek-v4',
        }

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config=config,
        )

        assert service.multimodal_analyzer.model == 'claude-3-opus-20240229'
        assert service.assembler.model == 'deepseek-v4'

    def test_cache_configuration_validation(self):
        """Test cache configuration values."""
        provider = MockAIProvider()

        config = {
            'screen_cache_ttl': 600,
            'page_analysis_cache_ttl': 1200,
            'cache_max_size': 500,
        }

        service = VisionServiceFactory.create(
            mode="flattened",
            ai_provider=provider,
            config=config,
        )

        assert service.screen_cache.ttl == 600
        assert service.page_analysis_cache.ttl == 1200
        assert service.screen_cache.max_size == 500
        assert service.page_analysis_cache.max_size == 500
