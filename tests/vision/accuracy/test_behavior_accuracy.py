"""Behavior inference accuracy tests for vision analysis.

This module provides functions to evaluate the accuracy of behavior
inference (expected_action, expects_page_change, expects_state_change)
compared to ground truth.
"""

import logging
from typing import Dict, Any, List

from src.state.content_tree import PageAnalysis


logger = logging.getLogger(__name__)


class BehaviorAccuracyEvaluator:
    """Evaluates behavior inference accuracy.

    Compares predicted PageAnalysis items with ground truth
    to measure accuracy of:
    - Expected action classification
    - Page change prediction
    - State change prediction
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
        """Evaluate behavior accuracy for a single prediction.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation
            screenshot_id: Identifier for the screenshot

        Returns:
            Dictionary with accuracy metrics
        """
        # Match predicted items with ground truth items by coordinate proximity
        matches = self._match_items(predicted, ground_truth)

        if not matches:
            result = {
                'screenshot_id': screenshot_id,
                'action_accuracy': 0.0,
                'page_change_accuracy': 0.0,
                'state_change_accuracy': 0.0,
                'overall_accuracy': 0.0,
                'match_count': 0,
            }
        else:
            correct_action = sum(1 for m in matches if m['action_match'])
            correct_page_change = sum(1 for m in matches if m['page_change_match'])
            correct_state_change = sum(1 for m in matches if m['state_change_match'])

            result = {
                'screenshot_id': screenshot_id,
                'action_accuracy': correct_action / len(matches),
                'page_change_accuracy': correct_page_change / len(matches),
                'state_change_accuracy': correct_state_change / len(matches),
                'overall_accuracy': (
                    correct_action + correct_page_change + correct_state_change
                ) / (3 * len(matches)) if matches else 0.0,
                'match_count': len(matches),
            }

        self.results.append(result)
        logger.info(
            f"Behavior evaluation for {screenshot_id}: "
            f"overall={result['overall_accuracy']:.2%}, "
            f"matches={result['match_count']}"
        )

        return result

    def _match_items(
        self,
        predicted: PageAnalysis,
        ground_truth: Dict[str, Any],
    ) -> List[Dict[str, Any]]:
        """Match predicted items with ground truth items.

        Uses coordinate proximity to match items.

        Args:
            predicted: Predicted PageAnalysis
            ground_truth: Ground truth annotation

        Returns:
            List of match dictionaries with comparison results
        """
        matches = []

        gt_items = ground_truth.get('items', [])

        for pred_item in predicted.items:
            # Find closest ground truth item by coordinate
            best_match = None
            best_distance = float('inf')

            for gt_item in gt_items:
                gt_coord = gt_item.get('coordinate', {})
                gt_x = gt_coord.get('x', 0)
                gt_y = gt_coord.get('y', 0)

                # Calculate Euclidean distance
                distance = (
                    (pred_item.coordinate.x - gt_x) ** 2 +
                    (pred_item.coordinate.y - gt_y) ** 2
                ) ** 0.5

                # Threshold for considering items matched
                if distance < 0.1 and distance < best_distance:  # 10% of screen
                    best_match = gt_item
                    best_distance = distance

            if best_match:
                matches.append({
                    'predicted': pred_item,
                    'ground_truth': best_match,
                    'action_match': self._compare_action(pred_item, best_match),
                    'page_change_match': self._compare_page_change(pred_item, best_match),
                    'state_change_match': self._compare_state_change(pred_item, best_match),
                })

        return matches

    def _compare_action(self, pred_item, gt_item) -> bool:
        """Compare expected action.

        Args:
            pred_item: Predicted menu item
            gt_item: Ground truth item

        Returns:
            True if actions match
        """
        pred_action = pred_item.expected_action.value if hasattr(pred_item.expected_action, 'value') else str(pred_item.expected_action)
        gt_action = gt_item.get('expected_action', '')

        return pred_action == gt_action

    def _compare_page_change(self, pred_item, gt_item) -> bool:
        """Compare page change expectation.

        Args:
            pred_item: Predicted menu item
            gt_item: Ground truth item

        Returns:
            True if page change expectations match
        """
        return pred_item.expects_page_change == gt_item.get('expects_page_change', False)

    def _compare_state_change(self, pred_item, gt_item) -> bool:
        """Compare state change expectation.

        Args:
            pred_item: Predicted menu item
            gt_item: Ground truth item

        Returns:
            True if state change expectations match
        """
        return pred_item.expects_state_change == gt_item.get('expects_state_change', False)

    def get_summary(self) -> Dict[str, float]:
        """Get summary of all evaluations.

        Returns:
            Dictionary with average accuracy metrics
        """
        if not self.results:
            return {
                'avg_action_accuracy': 0.0,
                'avg_page_change_accuracy': 0.0,
                'avg_state_change_accuracy': 0.0,
                'avg_overall_accuracy': 0.0,
                'total_matches': 0,
                'count': 0,
            }

        valid_results = [r for r in self.results if r['match_count'] > 0]

        if not valid_results:
            return {
                'avg_action_accuracy': 0.0,
                'avg_page_change_accuracy': 0.0,
                'avg_state_change_accuracy': 0.0,
                'avg_overall_accuracy': 0.0,
                'total_matches': 0,
                'count': len(self.results),
            }

        return {
            'avg_action_accuracy': sum(r['action_accuracy'] for r in valid_results) / len(valid_results),
            'avg_page_change_accuracy': sum(r['page_change_accuracy'] for r in valid_results) / len(valid_results),
            'avg_state_change_accuracy': sum(r['state_change_accuracy'] for r in valid_results) / len(valid_results),
            'avg_overall_accuracy': sum(r['overall_accuracy'] for r in valid_results) / len(valid_results),
            'total_matches': sum(r['match_count'] for r in valid_results),
            'count': len(self.results),
        }

    def clear(self) -> None:
        """Clear all results."""
        self.results.clear()
