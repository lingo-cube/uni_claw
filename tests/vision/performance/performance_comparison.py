"""Performance comparison framework for vision services.

This module provides tools to compare the performance of legacy
and flattened vision services in terms of latency, token usage,
and accuracy.
"""

import logging
import statistics
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import List, Dict, Any, Optional

from src.state.content_tree import PageAnalysis


logger = logging.getLogger(__name__)


@dataclass
class PerformanceMetrics:
    """Performance metrics for a single vision analysis run.

    Attributes:
        screenshot: Identifier for the screenshot
        mode: Service mode (legacy or flattened)
        multimodal_latency_ms: Latency for multimodal analysis step
        text_latency_ms: Latency for text assembly step
        total_latency_ms: Total latency
        input_tokens: Input tokens consumed
        multimodal_output_tokens: Output tokens from multimodal step
        text_output_tokens: Output tokens from text step
        total_tokens: Total tokens consumed
        multimodal_cached: Whether multimodal result was cached
        assembler_cached: Whether assembler result was cached
        hierarchy_accuracy: Hierarchy inference accuracy (0-1)
        behavior_accuracy: Behavior inference accuracy (0-1)
        popup_detection_accuracy: Popup detection accuracy (0-1)
        error: Error message if analysis failed
        timestamp: When the analysis was performed
    """

    screenshot: str
    mode: str

    # Latency metrics
    multimodal_latency_ms: float = 0.0
    text_latency_ms: float = 0.0
    total_latency_ms: float = 0.0

    # Token metrics
    input_tokens: int = 0
    multimodal_output_tokens: int = 0
    text_output_tokens: int = 0
    total_tokens: int = 0

    # Cache metrics
    multimodal_cached: bool = False
    assembler_cached: bool = False

    # Accuracy metrics
    hierarchy_accuracy: float = 0.0
    behavior_accuracy: float = 0.0
    popup_detection_accuracy: float = 0.0

    # Error tracking
    error: Optional[str] = None

    # Metadata
    timestamp: datetime = field(default_factory=datetime.now)

    @property
    def output_tokens(self) -> int:
        """Total output tokens."""
        return self.multimodal_output_tokens + self.text_output_tokens

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        return {
            'screenshot': self.screenshot,
            'mode': self.mode,
            'multimodal_latency_ms': self.multimodal_latency_ms,
            'text_latency_ms': self.text_latency_ms,
            'total_latency_ms': self.total_latency_ms,
            'input_tokens': self.input_tokens,
            'multimodal_output_tokens': self.multimodal_output_tokens,
            'text_output_tokens': self.text_output_tokens,
            'total_tokens': self.total_tokens,
            'multimodal_cached': self.multimodal_cached,
            'assembler_cached': self.assembler_cached,
            'hierarchy_accuracy': self.hierarchy_accuracy,
            'behavior_accuracy': self.behavior_accuracy,
            'popup_detection_accuracy': self.popup_detection_accuracy,
            'error': self.error,
            'timestamp': self.timestamp.isoformat(),
        }


@dataclass
class ComparisonResult:
    """Result of comparing performance metrics.

    Attributes:
        token_reduction_percent: Percentage reduction in tokens
        speed_improvement_percent: Percentage improvement in speed
        avg_latency_legacy: Average latency for legacy mode
        avg_latency_flattened: Average latency for flattened mode
        avg_tokens_legacy: Average tokens for legacy mode
        avg_tokens_flattened: Average tokens for flattened mode
        cache_hit_rate: Cache hit rate for flattened mode
        accuracy_comparison: Accuracy metrics comparison
    """

    token_reduction_percent: float
    speed_improvement_percent: float
    avg_latency_legacy: float
    avg_latency_flattened: float
    avg_tokens_legacy: int
    avg_tokens_flattened: int
    cache_hit_rate: float
    accuracy_comparison: Dict[str, Any]

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        return {
            'token_reduction_percent': self.token_reduction_percent,
            'speed_improvement_percent': self.speed_improvement_percent,
            'avg_latency_legacy': self.avg_latency_legacy,
            'avg_latency_flattened': self.avg_latency_flattened,
            'avg_tokens_legacy': self.avg_tokens_legacy,
            'avg_tokens_flattened': self.avg_tokens_flattened,
            'cache_hit_rate': self.cache_hit_rate,
            'accuracy_comparison': self.accuracy_comparison,
        }


class PerformanceComparison:
    """Performance comparison framework for vision services.

    Compares legacy and flattened vision services across multiple
    dimensions: latency, token usage, cache effectiveness, and accuracy.
    """

    def __init__(
        self,
        legacy_service,
        flattened_service,
        ground_truth_dir: Optional[Path] = None,
    ):
        """Initialize the performance comparison framework.

        Args:
            legacy_service: Legacy vision service instance
            flattened_service: Flattened vision service instance
            ground_truth_dir: Directory containing ground truth annotations
        """
        self.legacy_service = legacy_service
        self.flattened_service = flattened_service
        self.ground_truth_dir = ground_truth_dir

        self.results: List[PerformanceMetrics] = []

        logger.info("PerformanceComparison initialized")

    def test_screenshot(
        self,
        screenshot_path: str,
        image_data: bytes,
        ground_truth: Optional[Dict[str, Any]] = None,
    ) -> tuple[PerformanceMetrics, PerformanceMetrics]:
        """Test a single screenshot with both services.

        Args:
            screenshot_path: Identifier for the screenshot
            image_data: PNG format screenshot data
            ground_truth: Optional ground truth annotation

        Returns:
            Tuple of (legacy_metrics, flattened_metrics)
        """
        # Test legacy service
        legacy_metrics = self._test_legacy(screenshot_path, image_data)

        # Test flattened service
        flattened_metrics = self._test_flattened(screenshot_path, image_data)

        # Calculate accuracy if ground truth provided
        if ground_truth is not None and self.ground_truth_dir:
            self._calculate_accuracy(legacy_metrics, ground_truth)
            self._calculate_accuracy(flattened_metrics, ground_truth)

        # Store results
        self.results.append(legacy_metrics)
        self.results.append(flattened_metrics)

        logger.info(
            f"Tested {screenshot_path}: "
            f"legacy={legacy_metrics.total_latency_ms:.0f}ms, "
            f"flattened={flattened_metrics.total_latency_ms:.0f}ms"
        )

        return legacy_metrics, flattened_metrics

    def _test_legacy(self, screenshot_path: str, image_data: bytes) -> PerformanceMetrics:
        """Test legacy service with a screenshot."""
        try:
            # Legacy service returns PageAnalysis directly
            result = self.legacy_service.analyze_screenshot(image_data)

            # Check if result has embedded metrics (for testing)
            if hasattr(result, 'latency_ms'):
                # Result has embedded latency (test mock)
                latency_ms = result.latency_ms
                tokens = 0
            elif hasattr(result, 'total_latency_ms'):
                # Result is VisionAnalysisResult format
                latency_ms = result.total_latency_ms
                tokens = result.total_tokens if hasattr(result, 'total_tokens') else 0
            else:
                # Fallback: measure actual time
                start_time = datetime.now()
                result = self.legacy_service.analyze_screenshot(image_data)
                end_time = datetime.now()
                latency_ms = (end_time - start_time).total_seconds() * 1000
                tokens = 0

            return PerformanceMetrics(
                screenshot=screenshot_path,
                mode='legacy',
                total_latency_ms=latency_ms,
                # Legacy service typically doesn't provide token metrics
                input_tokens=0,
                total_tokens=tokens,
            )

        except Exception as e:
            logger.error(f"Legacy service failed for {screenshot_path}: {e}")
            return PerformanceMetrics(
                screenshot=screenshot_path,
                mode='legacy',
                error=str(e),
            )

    def _test_flattened(self, screenshot_path: str, image_data: bytes) -> PerformanceMetrics:
        """Test flattened service with a screenshot."""
        try:
            # Flattened service returns VisionAnalysisResult
            result = self.flattened_service.analyze_screenshot(image_data)

            return PerformanceMetrics(
                screenshot=screenshot_path,
                mode='flattened',
                multimodal_latency_ms=result.multimodal_latency_ms,
                text_latency_ms=result.assembler_latency_ms,
                total_latency_ms=result.total_latency_ms,
                multimodal_output_tokens=result.multimodal_tokens,
                text_output_tokens=result.assembler_tokens,
                total_tokens=result.total_tokens,
                multimodal_cached=result.multimodal_cached,
                assembler_cached=result.assembler_cached,
            )

        except Exception as e:
            logger.error(f"Flattened service failed for {screenshot_path}: {e}")
            return PerformanceMetrics(
                screenshot=screenshot_path,
                mode='flattened',
                error=str(e),
            )

    def _calculate_accuracy(
        self,
        metrics: PerformanceMetrics,
        ground_truth: Dict[str, Any],
    ) -> None:
        """Calculate accuracy metrics against ground truth.

        Args:
            metrics: Performance metrics to update
            ground_truth: Ground truth annotation
        """
        # Placeholder: accuracy calculation would go here
        # This requires comparing the actual PageAnalysis with ground truth
        metrics.hierarchy_accuracy = 0.0
        metrics.behavior_accuracy = 0.0
        metrics.popup_detection_accuracy = 0.0

    def generate_report(self) -> ComparisonResult:
        """Generate performance comparison report.

        Returns:
            ComparisonResult with aggregated metrics
        """
        # Filter successful results
        legacy_results = [r for r in self.results if r.mode == 'legacy' and r.error is None]
        flattened_results = [r for r in self.results if r.mode == 'flattened' and r.error is None]

        if not legacy_results or not flattened_results:
            logger.warning("Insufficient data for comparison")
            return self._empty_comparison_result()

        # Calculate average latencies
        avg_latency_legacy = statistics.mean(r.total_latency_ms for r in legacy_results)
        avg_latency_flattened = statistics.mean(r.total_latency_ms for r in flattened_results)

        # Calculate average tokens
        avg_tokens_legacy = int(statistics.mean(r.total_tokens for r in legacy_results))
        avg_tokens_flattened = int(statistics.mean(r.total_tokens for r in flattened_results))

        # Calculate improvements
        token_reduction_percent = self._calculate_token_reduction(
            avg_tokens_legacy,
            avg_tokens_flattened,
        )
        speed_improvement_percent = self._calculate_speed_improvement(
            avg_latency_legacy,
            avg_latency_flattened,
        )

        # Calculate cache hit rate
        cache_hits = sum(
            1 for r in flattened_results
            if r.multimodal_cached or r.assembler_cached
        )
        cache_hit_rate = cache_hits / len(flattened_results) if flattened_results else 0.0

        # Compare accuracy
        accuracy_comparison = self._compare_accuracy(legacy_results, flattened_results)

        return ComparisonResult(
            token_reduction_percent=token_reduction_percent,
            speed_improvement_percent=speed_improvement_percent,
            avg_latency_legacy=avg_latency_legacy,
            avg_latency_flattened=avg_latency_flattened,
            avg_tokens_legacy=avg_tokens_legacy,
            avg_tokens_flattened=avg_tokens_flattened,
            cache_hit_rate=cache_hit_rate,
            accuracy_comparison=accuracy_comparison,
        )

    def _calculate_token_reduction(self, legacy_tokens: int, flattened_tokens: int) -> float:
        """Calculate token reduction percentage.

        Args:
            legacy_tokens: Average tokens for legacy mode
            flattened_tokens: Average tokens for flattened mode

        Returns:
            Percentage reduction (0-100)
        """
        if legacy_tokens == 0:
            return 0.0
        return ((legacy_tokens - flattened_tokens) / legacy_tokens) * 100

    def _calculate_speed_improvement(
        self,
        legacy_latency: float,
        flattened_latency: float,
    ) -> float:
        """Calculate speed improvement percentage.

        Args:
            legacy_latency: Average latency for legacy mode
            flattened_latency: Average latency for flattened mode

        Returns:
            Percentage improvement (0-100)
        """
        if legacy_latency == 0:
            return 0.0
        return ((legacy_latency - flattened_latency) / legacy_latency) * 100

    def _compare_accuracy(
        self,
        legacy_results: List[PerformanceMetrics],
        flattened_results: List[PerformanceMetrics],
    ) -> Dict[str, Any]:
        """Compare accuracy metrics between modes.

        Args:
            legacy_results: Legacy mode results
            flattened_results: Flattened mode results

        Returns:
            Dictionary with accuracy comparison
        """
        return {
            'legacy_hierarchy_accuracy': statistics.mean(
                [r.hierarchy_accuracy for r in legacy_results]
            ) if legacy_results else 0.0,
            'flattened_hierarchy_accuracy': statistics.mean(
                [r.hierarchy_accuracy for r in flattened_results]
            ) if flattened_results else 0.0,
            'legacy_behavior_accuracy': statistics.mean(
                [r.behavior_accuracy for r in legacy_results]
            ) if legacy_results else 0.0,
            'flattened_behavior_accuracy': statistics.mean(
                [r.behavior_accuracy for r in flattened_results]
            ) if flattened_results else 0.0,
            'legacy_popup_accuracy': statistics.mean(
                [r.popup_detection_accuracy for r in legacy_results]
            ) if legacy_results else 0.0,
            'flattened_popup_accuracy': statistics.mean(
                [r.popup_detection_accuracy for r in flattened_results]
            ) if flattened_results else 0.0,
        }

    def _empty_comparison_result(self) -> ComparisonResult:
        """Return empty comparison result when insufficient data."""
        return ComparisonResult(
            token_reduction_percent=0.0,
            speed_improvement_percent=0.0,
            avg_latency_legacy=0.0,
            avg_latency_flattened=0.0,
            avg_tokens_legacy=0,
            avg_tokens_flattened=0,
            cache_hit_rate=0.0,
            accuracy_comparison={},
        )

    def clear_results(self) -> None:
        """Clear all stored results."""
        self.results.clear()
        logger.info("Cleared all comparison results")

    def get_results(self) -> List[PerformanceMetrics]:
        """Get all stored results.

        Returns:
            List of all performance metrics
        """
        return self.results.copy()
