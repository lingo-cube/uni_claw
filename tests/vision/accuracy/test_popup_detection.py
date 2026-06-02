"""Popup detection accuracy tests for vision analysis.

This module provides functions to evaluate the accuracy of popup
and dialog detection compared to ground truth.
"""

import logging
from typing import Dict, Any, List

from src.state.content_tree import PageAnalysis


logger = logging.getLogger(__name__)


class PopupDetectionAccuracyEvaluator:
    """Evaluates popup detection accuracy.

    Compares predicted PageAnalysis with ground truth to measure
    accuracy of:
    - Popup detection (is_popup)
    - Popup info accuracy (title, content)
    - Close button detection
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
        """Evaluate popup detection accuracy.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation
            screenshot_id: Identifier for the screenshot

        Returns:
            Dictionary with accuracy metrics
        """
        gt_is_popup = ground_truth.get('is_popup', False)

        # Detection accuracy
        detection_correct = (predicted.is_popup == gt_is_popup)

        # Popup info accuracy (only if popup detected in both)
        info_accuracy = 0.0
        close_button_accuracy = 0.0

        if predicted.is_popup and gt_is_popup:
            # Evaluate popup info
            info_accuracy = self._evaluate_popup_info(predicted, ground_truth)

            # Evaluate close button
            close_button_accuracy = self._evaluate_close_button(predicted, ground_truth)
        elif not predicted.is_popup and not gt_is_popup:
            # Both correctly identified as non-popup
            info_accuracy = 1.0
            close_button_accuracy = 1.0

        # Overall accuracy: detection + info + close button
        if gt_is_popup:
            # For popups, all three matter
            overall_accuracy = (
                (1.0 if detection_correct else 0.0) +
                info_accuracy +
                close_button_accuracy
            ) / 3
        else:
            # For non-popups, only detection matters
            overall_accuracy = 1.0 if detection_correct else 0.0

        result = {
            'screenshot_id': screenshot_id,
            'detection_accuracy': 1.0 if detection_correct else 0.0,
            'info_accuracy': info_accuracy,
            'close_button_accuracy': close_button_accuracy,
            'overall_accuracy': overall_accuracy,
            'gt_is_popup': gt_is_popup,
            'predicted_is_popup': predicted.is_popup,
        }

        self.results.append(result)
        logger.info(
            f"Popup detection evaluation for {screenshot_id}: "
            f"overall={result['overall_accuracy']:.2%}"
        )

        return result

    def _evaluate_popup_info(
        self,
        predicted: PageAnalysis,
        ground_truth: Dict[str, Any],
    ) -> float:
        """Evaluate popup information accuracy.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation

        Returns:
            Accuracy score (0-1)
        """
        gt_info = ground_truth.get('popup_info', {})
        pred_info = predicted.popup_info

        if not gt_info:
            return 1.0 if not pred_info else 0.0

        if not pred_info:
            return 0.0

        # Compare title and content
        title_match = pred_info.title == gt_info.get('title', '')
        content_match = pred_info.content == gt_info.get('content', '')

        # Score: title is more important than content
        return (0.7 if title_match else 0.0) + (0.3 if content_match else 0.0)

    def _evaluate_close_button(
        self,
        predicted: PageAnalysis,
        ground_truth: Dict[str, Any],
    ) -> float:
        """Evaluate close button detection accuracy.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation

        Returns:
            Accuracy score (0-1)
        """
        gt_close = ground_truth.get('close_button')
        pred_close = predicted.close_button

        # If ground truth has no close button, any prediction is acceptable
        if not gt_close:
            return 1.0

        # If ground truth has close button but prediction doesn't
        if not pred_close:
            return 0.0

        # Compare coordinates (within threshold)
        gt_x = gt_close.get('x', 0)
        gt_y = gt_close.get('y', 0)
        pred_x = pred_close.x
        pred_y = pred_close.y

        distance = ((gt_x - pred_x) ** 2 + (gt_y - pred_y) ** 2) ** 0.5

        # Within 5% of screen is considered correct
        return 1.0 if distance < 0.05 else 0.0

    def get_summary(self) -> Dict[str, float]:
        """Get summary of all evaluations.

        Returns:
            Dictionary with average accuracy metrics
        """
        if not self.results:
            return {
                'avg_detection_accuracy': 0.0,
                'avg_info_accuracy': 0.0,
                'avg_close_button_accuracy': 0.0,
                'avg_overall_accuracy': 0.0,
                'popup_count': 0,
                'count': 0,
            }

        popup_results = [r for r in self.results if r['gt_is_popup']]
        non_popup_results = [r for r in self.results if not r['gt_is_popup']]

        return {
            'avg_detection_accuracy': sum(r['detection_accuracy'] for r in self.results) / len(self.results),
            'avg_info_accuracy': sum(r['info_accuracy'] for r in popup_results) / len(popup_results) if popup_results else 0.0,
            'avg_close_button_accuracy': sum(r['close_button_accuracy'] for r in popup_results) / len(popup_results) if popup_results else 0.0,
            'avg_overall_accuracy': sum(r['overall_accuracy'] for r in self.results) / len(self.results),
            'popup_count': len(popup_results),
            'count': len(self.results),
        }

    def clear(self) -> None:
        """Clear all results."""
        self.results.clear()
