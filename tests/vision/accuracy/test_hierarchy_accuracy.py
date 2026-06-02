"""Hierarchy accuracy tests for vision analysis.

This module provides functions to evaluate the accuracy of hierarchy
inference (level 1/level 2 menus, current path) compared to ground truth.
"""

import logging
from typing import Dict, Any, List

from src.state.content_tree import PageAnalysis


logger = logging.getLogger(__name__)


class HierarchyAccuracyEvaluator:
    """Evaluates hierarchy inference accuracy.

    Compares predicted PageAnalysis with ground truth annotation
    to measure accuracy of:
    - Level 1 menu detection
    - Level 2 menu detection
    - Current path inference
    """

    def __init__(self):
        """Initialize the evaluator."""
        self.results: List[Dict[str, Any]] = []

    def evaluate(
        self,
        predicted: PageAnalysis,
        ground_truth: Dict[str, Any],
        screenshot_id: str = "unknown",
    ) -> Dict[str, Any]:
        """Evaluate hierarchy accuracy for a single prediction.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation
            screenshot_id: Identifier for the screenshot

        Returns:
            Dictionary with accuracy metrics
        """
        result = {
            'screenshot_id': screenshot_id,
            'level1_accuracy': self._evaluate_level1_menus(predicted, ground_truth),
            'level2_accuracy': self._evaluate_level2_menus(predicted, ground_truth),
            'current_path_accuracy': self._evaluate_current_path(predicted, ground_truth),
            'overall_accuracy': 0.0,  # Will be calculated
        }

        # Calculate overall accuracy (average of component accuracies)
        result['overall_accuracy'] = (
            result['level1_accuracy'] +
            result['level2_accuracy'] +
            result['current_path_accuracy']
        ) / 3

        self.results.append(result)
        logger.info(
            f"Hierarchy evaluation for {screenshot_id}: "
            f"overall={result['overall_accuracy']:.2%}"
        )

        return result

    def _evaluate_level1_menus(
        self,
        predicted: PageAnalysis,
        ground_truth: Dict[str, Any],
    ) -> float:
        """Evaluate level 1 menu accuracy.

        Uses intersection over union (IoU) of menu names.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation

        Returns:
            Accuracy score (0-1)
        """
        predicted_names = {menu.name for menu in predicted.level1_menus}

        gt_menus = ground_truth.get('level1_menus', [])
        if isinstance(gt_menus, list):
            gt_names = {m.get('name', '') for m in gt_menus}
        else:
            gt_names = set()

        if not gt_names:
            return 1.0 if not predicted_names else 0.0

        # Calculate IoU
        intersection = len(predicted_names & gt_names)
        union = len(predicted_names | gt_names)

        return intersection / union if union > 0 else 0.0

    def _evaluate_level2_menus(
        self,
        predicted: PageAnalysis,
        ground_truth: Dict[str, Any],
    ) -> float:
        """Evaluate level 2 menu accuracy.

        Uses intersection over union (IoU) of menu names.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation

        Returns:
            Accuracy score (0-1)
        """
        predicted_names = {menu.name for menu in predicted.level2_menus}

        gt_menus = ground_truth.get('level2_menus', [])
        if isinstance(gt_menus, list):
            gt_names = {m.get('name', '') for m in gt_menus}
        else:
            gt_names = set()

        if not gt_names:
            return 1.0 if not predicted_names else 0.0

        # Calculate IoU
        intersection = len(predicted_names & gt_names)
        union = len(predicted_names | gt_names)

        return intersection / union if union > 0 else 0.0

    def _evaluate_current_path(
        self,
        predicted: PageAnalysis,
        ground_truth: Dict[str, Any],
    ) -> float:
        """Evaluate current path accuracy.

        Compares predicted current path with ground truth.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation

        Returns:
            Accuracy score (0-1)
        """
        predicted_path = tuple(predicted.current_path)
        gt_path = tuple(ground_truth.get('current_path', []))

        if not gt_path:
            return 1.0 if not predicted_path else 0.0

        # Exact match
        if predicted_path == gt_path:
            return 1.0

        # Partial match: score based on how many elements match
        max_len = max(len(predicted_path), len(gt_path))
        if max_len == 0:
            return 1.0

        matches = sum(
            1 for p, g in zip(predicted_path, gt_path)
            if p == g
        )

        return matches / max_len

    def get_summary(self) -> Dict[str, float]:
        """Get summary of all evaluations.

        Returns:
            Dictionary with average accuracy metrics
        """
        if not self.results:
            return {
                'avg_level1_accuracy': 0.0,
                'avg_level2_accuracy': 0.0,
                'avg_current_path_accuracy': 0.0,
                'avg_overall_accuracy': 0.0,
                'count': 0,
            }

        return {
            'avg_level1_accuracy': sum(r['level1_accuracy'] for r in self.results) / len(self.results),
            'avg_level2_accuracy': sum(r['level2_accuracy'] for r in self.results) / len(self.results),
            'avg_current_path_accuracy': sum(r['current_path_accuracy'] for r in self.results) / len(self.results),
            'avg_overall_accuracy': sum(r['overall_accuracy'] for r in self.results) / len(self.results),
            'count': len(self.results),
        }

    def clear(self) -> None:
        """Clear all results."""
        self.results.clear()
