"""Unit tests for performance comparison framework."""

from datetime import datetime
from unittest.mock import Mock

import pytest

from tests.vision.performance.performance_comparison import (
    PerformanceMetrics,
    ComparisonResult,
    PerformanceComparison,
)


class MockVisionAnalysisResult:
    """Mock VisionAnalysisResult."""

    def __init__(
        self,
        latency_ms: float = 500,
        tokens: int = 1000,
        cached: bool = False,
    ):
        self.multimodal_latency_ms = latency_ms * 0.3
        self.assembler_latency_ms = latency_ms * 0.7
        self.total_latency_ms = latency_ms
        self.multimodal_tokens = int(tokens * 0.6)
        self.assembler_tokens = int(tokens * 0.4)
        self.total_tokens = tokens
        self.multimodal_cached = cached
        self.assembler_cached = cached
        self.page_analysis = Mock()


class MockLegacyResult:
    """Mock legacy PageAnalysis result."""

    def __init__(self, latency_ms: float = 1000):
        self.latency_ms = latency_ms
        self._timestamp = datetime.now()


class MockVisionService:
    """Mock vision service for testing."""

    def __init__(self, latency_ms: float = 1000, tokens: int = 2000, cached: bool = False):
        self.latency_ms = latency_ms
        self.tokens = tokens
        self.cached = cached
        self.call_count = 0
        self.start_time = None

    def analyze_screenshot(self, image_data: bytes):
        """Mock analyze method."""
        self.call_count += 1
        self.start_time = datetime.now()

        if self.tokens > 0:
            # Return flattened-style result
            return MockVisionAnalysisResult(
                latency_ms=self.latency_ms,
                tokens=self.tokens,
                cached=self.cached,
            )
        else:
            # Return legacy-style result with latency tracking
            return MockLegacyResult(latency_ms=self.latency_ms)


class TestPerformanceMetrics:
    """Tests for PerformanceMetrics dataclass."""

    def test_creation(self):
        """Test creating performance metrics."""
        metrics = PerformanceMetrics(
            screenshot="test.png",
            mode="legacy",
            total_latency_ms=1000,
            total_tokens=2000,
        )

        assert metrics.screenshot == "test.png"
        assert metrics.mode == "legacy"
        assert metrics.total_latency_ms == 1000
        assert metrics.total_tokens == 2000

    def test_output_tokens_property(self):
        """Test output_tokens property."""
        metrics = PerformanceMetrics(
            screenshot="test.png",
            mode="flattened",
            multimodal_output_tokens=600,
            text_output_tokens=400,
        )

        assert metrics.output_tokens == 1000

    def test_to_dict(self):
        """Test converting to dictionary."""
        metrics = PerformanceMetrics(
            screenshot="test.png",
            mode="flattened",
            total_latency_ms=500,
            total_tokens=1000,
        )

        result = metrics.to_dict()

        assert result['screenshot'] == "test.png"
        assert result['mode'] == "flattened"
        assert result['total_latency_ms'] == 500
        assert result['total_tokens'] == 1000
        assert 'timestamp' in result


class TestComparisonResult:
    """Tests for ComparisonResult dataclass."""

    def test_creation(self):
        """Test creating comparison result."""
        result = ComparisonResult(
            token_reduction_percent=60.0,
            speed_improvement_percent=30.0,
            avg_latency_legacy=1000,
            avg_latency_flattened=700,
            avg_tokens_legacy=2000,
            avg_tokens_flattened=800,
            cache_hit_rate=0.5,
            accuracy_comparison={'hierarchy': 0.9},
        )

        assert result.token_reduction_percent == 60.0
        assert result.speed_improvement_percent == 30.0

    def test_to_dict(self):
        """Test converting to dictionary."""
        result = ComparisonResult(
            token_reduction_percent=50.0,
            speed_improvement_percent=25.0,
            avg_latency_legacy=800,
            avg_latency_flattened=600,
            avg_tokens_legacy=1500,
            avg_tokens_flattened=750,
            cache_hit_rate=0.7,
            accuracy_comparison={},
        )

        result_dict = result.to_dict()

        assert result_dict['token_reduction_percent'] == 50.0
        assert result_dict['cache_hit_rate'] == 0.7


class TestPerformanceComparison:
    """Tests for PerformanceComparison class."""

    def test_creation(self):
        """Test creating performance comparison instance."""
        legacy = MockVisionService()
        flattened = MockVisionService()

        comp = PerformanceComparison(legacy, flattened)

        assert comp.legacy_service == legacy
        assert comp.flattened_service == flattened
        assert comp.results == []

    def test_test_screenshot_both_modes(self):
        """Test analyzing screenshot with both modes."""
        legacy = MockVisionService(latency_ms=1000, tokens=0)
        flattened = MockVisionService(latency_ms=700, tokens=800)

        comp = PerformanceComparison(legacy, flattened)

        image_data = b"test_image"
        legacy_metrics, flattened_metrics = comp.test_screenshot("test.png", image_data)

        assert legacy_metrics.mode == "legacy"
        assert flattened_metrics.mode == "flattened"
        assert len(comp.results) == 2

    def test_test_screenshot_latency_tracking(self):
        """Test that latency is tracked correctly."""
        legacy = MockVisionService(latency_ms=1200, tokens=0)
        flattened = MockVisionService(latency_ms=800, tokens=1000)

        comp = PerformanceComparison(legacy, flattened)

        image_data = b"test_image"
        _, flattened_metrics = comp.test_screenshot("test.png", image_data)

        assert flattened_metrics.total_latency_ms > 0
        assert flattened_metrics.multimodal_latency_ms > 0
        assert flattened_metrics.multimodal_latency_ms + flattened_metrics.text_latency_ms == \
            flattened_metrics.total_latency_ms

    def test_test_screenshot_token_tracking(self):
        """Test that tokens are tracked correctly."""
        legacy = MockVisionService(latency_ms=1000, tokens=0)
        flattened = MockVisionService(latency_ms=700, tokens=1500)

        comp = PerformanceComparison(legacy, flattened)

        image_data = b"test_image"
        _, flattened_metrics = comp.test_screenshot("test.png", image_data)

        assert flattened_metrics.multimodal_output_tokens > 0
        assert flattened_metrics.text_output_tokens > 0
        assert flattened_metrics.total_tokens == \
            flattened_metrics.multimodal_output_tokens + flattened_metrics.text_output_tokens

    def test_test_screenshot_cache_tracking(self):
        """Test that cache status is tracked."""
        legacy = MockVisionService(latency_ms=1000, tokens=0)
        flattened = MockVisionService(latency_ms=700, tokens=1000, cached=True)

        comp = PerformanceComparison(legacy, flattened)

        image_data = b"test_image"
        _, flattened_metrics = comp.test_screenshot("test.png", image_data)

        assert flattened_metrics.multimodal_cached is True
        assert flattened_metrics.assembler_cached is True

    def test_test_screenshot_error_handling(self):
        """Test error handling when service fails."""
        legacy = MockVisionService(latency_ms=1000, tokens=0)

        # Create a failing flattened service
        flattened = Mock()
        flattened.analyze_screenshot = Mock(side_effect=Exception("Test error"))

        comp = PerformanceComparison(legacy, flattened)

        image_data = b"test_image"
        legacy_metrics, flattened_metrics = comp.test_screenshot("test.png", image_data)

        assert legacy_metrics.error is None
        assert flattened_metrics.error == "Test error"

    def test_generate_report(self):
        """Test generating comparison report."""
        # Use positive tokens for both so they use the VisionAnalysisResult format
        legacy = MockVisionService(latency_ms=1000, tokens=2000)
        flattened = MockVisionService(latency_ms=600, tokens=800)

        comp = PerformanceComparison(legacy, flattened)

        # Run multiple tests
        for i in range(3):
            comp.test_screenshot(f"test_{i}.png", b"test_image")

        report = comp.generate_report()

        # Check improvements
        assert report.token_reduction_percent > 0  # Flattened uses fewer tokens
        assert report.speed_improvement_percent > 0  # Flattened is faster
        assert report.avg_latency_legacy > report.avg_latency_flattened
        assert report.avg_tokens_legacy > report.avg_tokens_flattened

    def test_generate_report_with_no_results(self):
        """Test generating report with no test results."""
        legacy = MockVisionService()
        flattened = MockVisionService()

        comp = PerformanceComparison(legacy, flattened)
        report = comp.generate_report()

        assert report.token_reduction_percent == 0
        assert report.speed_improvement_percent == 0

    def test_clear_results(self):
        """Test clearing stored results."""
        legacy = MockVisionService()
        flattened = MockVisionService()

        comp = PerformanceComparison(legacy, flattened)
        comp.test_screenshot("test.png", b"test_image")

        assert len(comp.results) > 0

        comp.clear_results()

        assert len(comp.results) == 0

    def test_get_results(self):
        """Test getting results copy."""
        legacy = MockVisionService()
        flattened = MockVisionService()

        comp = PerformanceComparison(legacy, flattened)
        comp.test_screenshot("test.png", b"test_image")

        results = comp.get_results()

        assert len(results) == len(comp.results)

        # Verify it's a copy
        results.append(Mock())
        assert len(results) != len(comp.results)

    def test_token_reduction_calculation(self):
        """Test token reduction calculation."""
        # Legacy: 2000 tokens, Flattened: 800 tokens
        # Reduction: (2000 - 800) / 2000 * 100 = 60%
        reduction = PerformanceComparison._calculate_token_reduction(None, 2000, 800)

        assert abs(reduction - 60.0) < 0.01

    def test_token_reduction_zero_division(self):
        """Test token reduction with zero legacy tokens."""
        reduction = PerformanceComparison._calculate_token_reduction(None, 0, 800)

        assert reduction == 0.0

    def test_speed_improvement_calculation(self):
        """Test speed improvement calculation."""
        # Legacy: 1000ms, Flattened: 700ms
        # Improvement: (1000 - 700) / 1000 * 100 = 30%
        improvement = PerformanceComparison._calculate_speed_improvement(None, 1000, 700)

        assert abs(improvement - 30.0) < 0.01

    def test_speed_improvement_zero_division(self):
        """Test speed improvement with zero legacy latency."""
        improvement = PerformanceComparison._calculate_speed_improvement(None, 0, 700)

        assert improvement == 0.0
