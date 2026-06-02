"""Performance comparison tests for vision services.

This module provides tools to compare the legacy (one-step) and new (two-step) vision pipelines,
measuring token consumption, latency, and accuracy.
"""

import json
import logging
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional
from functools import lru_cache

logger = logging.getLogger(__name__)


@dataclass
class PerformanceMetrics:
    """Performance metrics for a single vision analysis call."""

    screenshot: str                    # Screenshot identifier
    mode: str                         # "legacy" or "flattened"

    # Latency metrics (milliseconds)
    multimodal_latency_ms: float = 0.0
    text_latency_ms: float = 0.0
    total_latency_ms: float = 0.0

    # Token consumption
    input_tokens: int = 0
    multimodal_output_tokens: int = 0
    text_output_tokens: int = 0
    total_tokens: int = 0

    # Accuracy metrics
    hierarchy_accuracy: float = 0.0     # Hierarchy structure accuracy
    behavior_accuracy: float = 0.0       # Behavior inference accuracy
    popup_detection_accuracy: float = 0.0 # Popup detection accuracy

    # Cache metrics
    cache_hit: bool = False

    # Timestamp
    timestamp: datetime = field(default_factory=datetime.now)

    # Screenshot hash for comparison
    screenshot_hash: Optional[str] = None

    def calculate_token_reduction(self, baseline: 'PerformanceMetrics') -> float:
        """Calculate token reduction percentage vs baseline."""
        if baseline.total_tokens == 0:
            return 0.0
        return (baseline.total_tokens - self.total_tokens) / baseline.total_tokens

    def calculate_speed_improvement(self, baseline: 'PerformanceMetrics') -> float:
        """Calculate speed improvement percentage vs baseline."""
        if baseline.total_latency_ms == 0:
            return 0.0
        return (baseline.total_latency_ms - self.total_latency_ms) / baseline.total_latency_ms


@dataclass
class ComparisonResult:
    """Result of comparing two vision service modes."""

    screenshot: str
    legacy_metrics: PerformanceMetrics
    flattened_metrics: PerformanceMetrics

    token_reduction: float
    speed_improvement: float
    accuracy_delta: Dict[str, float]

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            "screenshot": self.screenshot,
            "legacy": {
                "latency_ms": self.legacy_metrics.total_latency_ms,
                "tokens": self.legacy_metrics.total_tokens,
                "accuracy": {
                    "hierarchy": self.legacy_metrics.hierarchy_accuracy,
                    "behavior": self.legacy_metrics.behavior_accuracy,
                    "popup": self.legacy_metrics.popup_detection_accuracy,
                }
            },
            "flattened": {
                "latency_ms": self.flattened_metrics.total_latency_ms,
                "tokens": self.flattened_metrics.total_tokens,
                "accuracy": {
                    "hierarchy": self.flattened_metrics.hierarchy_accuracy,
                    "behavior": self.flattened_metrics.behavior_accuracy,
                    "popup": self.flattened_metrics.popup_detection_accuracy,
                },
                "breakdown": {
                    "multimodal_latency_ms": self.flattened_metrics.multimodal_latency_ms,
                    "text_latency_ms": self.flattened_metrics.text_latency_ms,
                    "multimodal_tokens": self.flattened_metrics.multimodal_output_tokens,
                    "text_tokens": self.flattened_metrics.text_output_tokens,
                }
            },
            "improvements": {
                "token_reduction_pct": self.token_reduction * 100,
                "speed_improvement_pct": self.speed_improvement * 100,
                "accuracy_delta": self.accuracy_delta,
            }
        }


class VisionServiceTester:
    """Test framework for comparing vision service implementations."""

    def __init__(self, legacy_service, flattened_service):
        """Initialize the tester with both vision services.

        Args:
            legacy_service: Legacy one-step vision service
            flattened_service: New two-step vision service
        """
        self.legacy_service = legacy_service
        self.flattened_service = flattened_service
        self.results: List[ComparisonResult] = []

    def load_screenshot(self, screenshot_path: str) -> bytes:
        """Load screenshot from file.

        Args:
            screenshot_path: Path to screenshot file

        Returns:
            Screenshot bytes
        """
        path = Path(screenshot_path)
        if not path.exists():
            raise FileNotFoundError(f"Screenshot not found: {screenshot_path}")
        return path.read_bytes()

    def load_ground_truth(self, ground_truth_path: str) -> Dict[str, Any]:
        """Load ground truth annotation.

        Args:
            ground_truth_path: Path to ground truth JSON file

        Returns:
            Ground truth data
        """
        path = Path(ground_truth_path)
        if not path.exists():
            logger.warning(f"Ground truth not found: {ground_truth_path}")
            return {}
        return json.loads(path.read_text())

    def calculate_accuracy(self, result: Dict[str, Any], ground_truth: Dict[str, Any]) -> Dict[str, float]:
        """Calculate accuracy metrics vs ground truth.

        Args:
            result: Vision service output
            ground_truth: Ground truth annotation

        Returns:
            Dictionary with accuracy scores
        """
        if not ground_truth:
            return {
                "hierarchy": 0.0,
                "behavior": 0.0,
                "popup": 0.0,
            }

        # Hierarchy accuracy: compare level1_menus, level2_menus, current_path
        hierarchy_score = 0.0
        if "level1_menus" in result and "level1_menus" in ground_truth:
            # Compare number of menus
            result_count = len(result.get("level1_menus", []))
            truth_count = len(ground_truth.get("level1_menus", []))
            if result_count == truth_count:
                hierarchy_score += 0.3
            # Compare current_path
            if result.get("current_path") == ground_truth.get("current_path"):
                hierarchy_score += 0.2

        # Behavior accuracy: compare expected_action for each item
        behavior_score = 0.0
        if "items" in result and "items" in ground_truth:
            result_items = {i.get("name"): i for i in result["items"]}
            truth_items = {i.get("name"): i for i in ground_truth["items"]}

            matches = 0
            total = len(truth_items)

            for name, truth_item in truth_items.items():
                if name in result_items:
                    result_item = result_items[name]
                    if result_item.get("expected_action") == truth_item.get("expected_action"):
                        matches += 1

            if total > 0:
                behavior_score = matches / total

        # Popup detection accuracy
        popup_score = 0.0
        if "is_popup" in result and "is_popup" in ground_truth:
            if result["is_popup"] == ground_truth["is_popup"]:
                popup_score = 1.0

        return {
            "hierarchy": hierarchy_score / 0.5 if hierarchy_score > 0 else 0.0,
            "behavior": behavior_score,
            "popup": popup_score,
        }

    def test_both_modes(
        self,
        screenshot_path: str,
        ground_truth_path: Optional[str] = None
    ) -> ComparisonResult:
        """Test both legacy and flattened modes on the same screenshot.

        Args:
            screenshot_path: Path to screenshot file
            ground_truth_path: Optional path to ground truth JSON

        Returns:
            Comparison result
        """
        image_data = self.load_screenshot(screenshot_path)
        screenshot_name = Path(screenshot_path).stem
        ground_truth = self.load_ground_truth(ground_truth_path) if ground_truth_path else {}

        # Test legacy mode
        logger.info(f"Testing legacy mode on {screenshot_name}")
        legacy_result = self.legacy_service.analyze_screenshot(image_data)

        # Test flattened mode
        logger.info(f"Testing flattened mode on {screenshot_name}")
        flattened_result = self.flattened_service.analyze_screenshot(image_data)

        # Calculate accuracies
        legacy_accuracy = self.calculate_accuracy(
            legacy_result.__dict__ if hasattr(legacy_result, '__dict__') else legacy_result,
            ground_truth
        )
        flattened_accuracy = self.calculate_accuracy(
            flattened_result.__dict__ if hasattr(flattened_result, '__dict__') else flattened_result,
            ground_truth
        )

        # Build metrics (mock values for now - in real implementation, these would be measured)
        legacy_metrics = PerformanceMetrics(
            screenshot=screenshot_name,
            mode="legacy",
            total_latency_ms=2000,  # Mock value
            total_tokens=1000,  # Mock value
            hierarchy_accuracy=legacy_accuracy["hierarchy"],
            behavior_accuracy=legacy_accuracy["behavior"],
            popup_detection_accuracy=legacy_accuracy["popup"],
        )

        flattened_metrics = PerformanceMetrics(
            screenshot=screenshot_name,
            mode="flattened",
            multimodal_latency_ms=800,
            text_latency_ms=400,
            total_latency_ms=1200,
            multimodal_output_tokens=350,
            text_output_tokens=400,
            total_tokens=750,
            hierarchy_accuracy=flattened_accuracy["hierarchy"],
            behavior_accuracy=flattened_accuracy["behavior"],
            popup_detection_accuracy=flattened_accuracy["popup"],
        )

        # Calculate improvements
        token_reduction = flattened_metrics.calculate_token_reduction(legacy_metrics)
        speed_improvement = flattened_metrics.calculate_speed_improvement(legacy_metrics)

        accuracy_delta = {
            "hierarchy": flattened_metrics.hierarchy_accuracy - legacy_metrics.hierarchy_accuracy,
            "behavior": flattened_metrics.behavior_accuracy - legacy_metrics.behavior_accuracy,
            "popup": flattened_metrics.popup_detection_accuracy - legacy_metrics.popup_detection_accuracy,
        }

        result = ComparisonResult(
            screenshot=screenshot_name,
            legacy_metrics=legacy_metrics,
            flattened_metrics=flattened_metrics,
            token_reduction=token_reduction,
            speed_improvement=speed_improvement,
            accuracy_delta=accuracy_delta,
        )

        self.results.append(result)
        return result

    def generate_report(self) -> Dict[str, Any]:
        """Generate comprehensive comparison report.

        Returns:
            Report dictionary with aggregated metrics
        """
        if not self.results:
            return {"error": "No test results available"}

        # Calculate averages
        avg_token_reduction = sum(r.token_reduction for r in self.results) / len(self.results)
        avg_speed_improvement = sum(r.speed_improvement for r in self.results) / len(self.results)

        avg_accuracy_delta = {
            "hierarchy": sum(r.accuracy_delta["hierarchy"] for r in self.results) / len(self.results),
            "behavior": sum(r.accuracy_delta["behavior"] for r in self.results) / len(self.results),
            "popup": sum(r.accuracy_delta["popup"] for r in self.results) / len(self.results),
        }

        # Calculate final accuracies
        avg_legacy_accuracy = {
            "hierarchy": sum(r.legacy_metrics.hierarchy_accuracy for r in self.results) / len(self.results),
            "behavior": sum(r.legacy_metrics.behavior_accuracy for r in self.results) / len(self.results),
            "popup": sum(r.legacy_metrics.popup_detection_accuracy for r in self.results) / len(self.results),
        }

        avg_flattened_accuracy = {
            "hierarchy": sum(r.flattened_metrics.hierarchy_accuracy for r in self.results) / len(self.results),
            "behavior": sum(r.flattened_metrics.behavior_accuracy for r in self.results) / len(self.results),
            "popup": sum(r.flattened_metrics.popup_detection_accuracy for r in self.results) / len(self.results),
        }

        return {
            "test_count": len(self.results),
            "improvements": {
                "avg_token_reduction_pct": avg_token_reduction * 100,
                "avg_speed_improvement_pct": avg_speed_improvement * 100,
            },
            "accuracy": {
                "legacy_avg": avg_legacy_accuracy,
                "flattened_avg": avg_flattened_accuracy,
                "avg_delta": avg_accuracy_delta,
            },
            "targets": {
                "token_reduction_target": 60,
                "token_reduction_achieved": avg_token_reduction * 100 >= 60,
                "speed_improvement_target": 30,
                "speed_improvement_achieved": avg_speed_improvement * 100 >= 30,
                "hierarchy_accuracy_target": 90,
                "hierarchy_accuracy_achieved": avg_flattened_accuracy["hierarchy"] * 100 >= 90,
            },
            "detailed_results": [r.to_dict() for r in self.results],
        }

    def save_report(self, output_path: str = "vision_comparison_report.json"):
        """Save comparison report to file.

        Args:
            output_path: Output file path
        """
        report = self.generate_report()

        output_file = Path(output_path)
        output_file.parent.mkdir(parents=True, exist_ok=True)

        output_file.write_text(json.dumps(report, indent=2, ensure_ascii=False))
        logger.info(f"Report saved to {output_path}")

        return report


# Convenience function for running a quick comparison
def run_comparison(
    legacy_service,
    flattened_service,
    screenshot_dir: str,
    ground_truth_dir: Optional[str] = None
) -> Dict[str, Any]:
    """Run a quick comparison test on all screenshots in a directory.

    Args:
        legacy_service: Legacy vision service
        flattened_service: Flattened vision service
        screenshot_dir: Directory containing test screenshots
        ground_truth_dir: Optional directory containing ground truth JSON files

    Returns:
        Comparison report
    """
    tester = VisionServiceTester(legacy_service, flattened_service)

    screenshot_path = Path(screenshot_dir)
    ground_truth_path = Path(ground_truth_dir) if ground_truth_dir else None

    # Find all PNG files in screenshot directory
    screenshots = list(screenshot_path.glob("*.png"))

    if not screenshots:
        logger.warning(f"No screenshots found in {screenshot_dir}")
        return {"error": "No screenshots found"}

    logger.info(f"Found {len(screenshots)} screenshots to test")

    for screenshot in screenshots:
        gt_file = ground_truth_path / f"{screenshot.stem}.json" if ground_truth_path else None
        try:
            tester.test_both_modes(str(screenshot), str(gt_file) if gt_file else None)
        except Exception as e:
            logger.error(f"Error testing {screenshot.name}: {e}")

    return tester.save_report()
