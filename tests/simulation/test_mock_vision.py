"""
Integration tests for MockVisionService.

Tests path mapping functionality, context integration, and
interaction with PageAnalyzer.
"""

import pytest
from unittest.mock import Mock
from src.simulation.mock_vision import MockVisionService
from src.simulation.visualizer import InMemoryTracer


class MockTraversalContext:
    """Mock traversal context for testing."""
    def __init__(self, path):
        self.current_path = path


class TestMockVisionService:
    """Test suite for MockVisionService integration."""

    @pytest.fixture
    def sample_virtual_pages(self):
        """Create sample virtual pages for testing."""
        return {
            "root": {
                "page_name": "HomeScreen",
                "elements": [
                    {
                        "id": "btn_settings",
                        "type": "button",
                        "text": "Settings",
                        "clickable": True
                    }
                ]
            },
            "settings/display": {
                "page_name": "DisplaySettings",
                "elements": [
                    {
                        "id": "slider_brightness",
                        "type": "slider",
                        "text": "Brightness",
                        "scrollable": True
                    }
                ]
            },
            "settings/sound": {
                "page_name": "SoundSettings",
                "elements": [
                    {
                        "id": "slider_volume",
                        "type": "slider",
                        "text": "Volume",
                        "scrollable": True
                    }
                ]
            }
        }

    @pytest.fixture
    def vision_service(self, sample_virtual_pages):
        """Create MockVisionService instance."""
        return MockVisionService(sample_virtual_pages)

    def test_init_with_page_analyzer(self, vision_service):
        """Test initialization with PageAnalyzer integration."""
        assert vision_service._analyzer is not None
        assert vision_service._call_count == 0
        assert vision_service.virtual_pages is not None

    def test_path_mapping_building(self, sample_virtual_pages):
        """Test path mapping functionality."""
        service = MockVisionService(sample_virtual_pages)
        mapping = service._path_mapping

        assert "HomeScreen" in mapping
        assert mapping["HomeScreen"] == "root"
        assert "DisplaySettings" in mapping
        assert mapping["DisplaySettings"] == "settings/display"

    def test_analyze_screenshot_success(self, vision_service):
        """Test successful screenshot analysis."""
        vision_service.inject_path("root")
        result = vision_service.analyze_screenshot()

        assert result is not None
        assert result["page_path"] == "root"
        assert result["metadata"]["source"] == "simulation"
        assert len(result["elements"]) > 0

    def test_analyze_screenshot_page_not_found(self, vision_service):
        """Test screenshot analysis when page not found."""
        vision_service.inject_path("nonexistent")
        result = vision_service.analyze_screenshot()

        # Should return empty page analysis
        assert result is not None
        assert result["page_name"] == ""
        assert result["items"] == []

    def test_context_integration_with_traversal_context(self, vision_service):
        """Test context integration with TraversalContext."""
        context = MockTraversalContext(["settings", "display"])
        vision_service.set_context(context)

        result = vision_service.analyze_screenshot()

        # Should use path from context
        assert result is not None
        assert result["page_path"] == "settings/display"

    def test_context_integration_with_tracer(self, vision_service):
        """Test context integration with InMemoryTracer."""
        tracer = InMemoryTracer()

        # Simulate some tracer state
        class MockStep:
            def __init__(self):
                self.current_path = "settings/display"

        tracer.steps = [MockStep()]
        vision_service.set_context(tracer)

        result = vision_service.analyze_screenshot()

        # Should infer path from tracer
        assert result is not None

    def test_context_fallback_to_default(self, vision_service):
        """Test fallback to default path when no context available."""
        # No context set
        result = vision_service.analyze_screenshot()

        # Should default to "root"
        assert result is not None
        assert result["page_path"] == "root"

    def test_call_counting(self, vision_service):
        """Test call counting functionality."""
        assert vision_service.get_call_count() == 0

        vision_service.analyze_screenshot()
        assert vision_service.get_call_count() == 1

        vision_service.analyze_screenshot()
        vision_service.analyze_screenshot()
        assert vision_service.get_call_count() == 3

    def test_path_injection(self, vision_service):
        """Test path injection functionality."""
        # Inject specific path
        vision_service.inject_path("settings/display")
        result = vision_service.analyze_screenshot()

        assert result["page_path"] == "settings/display"

        # Clear injection
        vision_service.clear_injected_path()
        result = vision_service.analyze_screenshot()

        # Should use default path
        assert result["page_path"] == "root"

    def test_reset_functionality(self, vision_service):
        """Test reset functionality."""
        # Set some state
        vision_service.inject_path("settings/display")
        context = MockTraversalContext(["settings"])
        vision_service.set_context(context)
        vision_service.analyze_screenshot()

        assert vision_service.get_call_count() == 1
        assert vision_service._current_context is not None

        # Reset
        vision_service.reset()

        # Check state is cleared
        assert vision_service.get_call_count() == 0
        assert vision_service._current_context is None
        assert vision_service._injected_path is None

    def test_cache_statistics(self, vision_service):
        """Test cache statistics retrieval."""
        # Initially empty
        stats = vision_service.get_cache_stats()
        assert stats["cache_size"] == 0
        assert stats["cached_paths"] == []

        # After some analyses
        vision_service.inject_path("root")
        vision_service.analyze_screenshot()

        vision_service.inject_path("settings/display")
        vision_service.analyze_screenshot()

        stats = vision_service.get_cache_stats()
        assert stats["cache_size"] == 2
        assert len(stats["cached_paths"]) == 2

    def test_page_analyzer_integration(self, vision_service):
        """Test that PageAnalyzer is properly integrated."""
        vision_service.inject_path("root")
        result = vision_service.analyze_screenshot()

        # Check that PageAnalyzer processed the data
        assert "page_type" in result
        assert "elements" in result
        assert "metadata" in result

        # Check that elements have proper structure
        for element in result["elements"]:
            assert "element_id" in element
            assert "element_type" in element
            assert "action_hint" in element
            assert "metadata" in element

    def test_multiple_context_types(self, vision_service):
        """Test handling of multiple context types."""
        # TraversalContext
        context1 = MockTraversalContext(["settings", "display"])
        vision_service.set_context(context1)
        result1 = vision_service.analyze_screenshot()
        assert result1["page_path"] == "settings/display"

        # InMemoryTracer
        tracer = InMemoryTracer()
        vision_service.set_context(tracer)
        result2 = vision_service.analyze_screenshot()
        assert result2 is not None  # Should handle tracer

        # No context
        vision_service.reset()
        result3 = vision_service.analyze_screenshot()
        assert result3["page_path"] == "root"

    def test_performance_integration(self, vision_service):
        """Test performance of MockVisionService operations."""
        import time

        # Test multiple analyses
        iterations = 100
        vision_service.inject_path("root")

        start_time = time.time()
        for _ in range(iterations):
            vision_service.analyze_screenshot()
        elapsed_time = time.time() - start_time

        avg_time = (elapsed_time / iterations) * 1000  # Convert to ms
        assert avg_time < 10.0  # Should be very fast (<10ms per analysis)

    def test_error_handling_for_invalid_paths(self, vision_service):
        """Test error handling for invalid page paths."""
        # Inject invalid path
        vision_service.inject_path("definitely/not/a/real/path")
        result = vision_service.analyze_screenshot()

        # Should handle gracefully and return empty analysis
        assert result is not None
        assert result["page_name"] == ""
        assert result["items"] == []

    def test_concurrent_context_changes(self, vision_service):
        """Test handling of concurrent context changes."""
        # Set initial context
        context1 = MockTraversalContext(["settings", "display"])
        vision_service.set_context(context1)

        result1 = vision_service.analyze_screenshot()
        assert result1["page_path"] == "settings/display"

        # Change context
        context2 = MockTraversalContext(["settings", "sound"])
        vision_service.set_context(context2)

        result2 = vision_service.analyze_screenshot()
        assert result2["page_path"] == "settings/sound"

    def test_reset_clears_page_analyzer_cache(self, vision_service):
        """Test that reset clears PageAnalyzer cache."""
        # Do some analyses
        vision_service.inject_path("root")
        vision_service.analyze_screenshot()

        vision_service.inject_path("settings/display")
        vision_service.analyze_screenshot()

        stats_before = vision_service.get_cache_stats()
        assert stats_before["cache_size"] > 0

        # Reset should clear cache
        vision_service.reset()
        stats_after = vision_service.get_cache_stats()
        assert stats_after["cache_size"] == 0