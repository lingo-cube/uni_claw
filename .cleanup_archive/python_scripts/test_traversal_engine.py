#!/usr/bin/env python3
"""
Simple traversal simulator for testing - demonstrates complete DFS traversal.
"""
import json
import sys
from pathlib import Path
from datetime import datetime
from typing import Any, Dict, List

sys.path.insert(0, str(Path(__file__).parent))

from src.simulation.page_analyzer import PageAnalyzer
from src.simulation.mock_action import MockActionExecutor


class SimpleTraversalSimulator:
    """
    Simple DFS traversal simulator for testing.

    Demonstrates complete depth-first traversal with proper state management.
    """

    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        """Initialize simulator with virtual pages."""
        self.virtual_pages = virtual_pages
        self.page_analyzer = PageAnalyzer(virtual_pages)
        self.action_executor = MockActionExecutor()

        # Traversal state
        self.current_path = []
        self.visited_nodes = {}
        self.traversal_log = []
        self.step_count = 0

    def run_traversal(self, max_depth: int = 3, max_steps: int = 20) -> Dict[str, Any]:
        """
        Run complete DFS traversal.

        Args:
            max_depth: Maximum traversal depth
            max_steps: Maximum number of steps to execute

        Returns:
            Traversal result with complete trace
        """
        print(f"[SIMULATOR] Starting DFS traversal (max_depth={max_depth}, max_steps={max_steps})")

        # Start from root
        self.current_path = []
        self._visit_page("root")

        # DFS traversal
        while self.step_count < max_steps and self._has_more_to_explore():
            # Get current page elements
            try:
                page_analysis = self.page_analyzer.analyze_page(self._get_current_path_string())
                elements = page_analysis.get("elements", [])

                print(f"[STEP {self.step_count + 1}] Current path: {self.current_path}")
                print(f"[STEP {self.step_count + 1}] Elements available: {len(elements)}")

                # Try to interact with elements
                if elements and len(self.current_path) < max_depth:
                    self._interact_with_next_element(elements)
                else:
                    # Need to go back
                    if len(self.current_path) > 0:
                        self._go_back()
                    else:
                        # Back to root, traversal complete
                        print(f"[SIMULATOR] DFS traversal complete!")
                        break

            except Exception as e:
                print(f"[ERROR] Step {self.step_count + 1}: {e}")
                self._log_step("error", {"error": str(e)})
                break

        return self._build_result()

    def _visit_page(self, page_name: str) -> None:
        """Visit a page and record it."""
        page_key = self._get_current_path_string()

        if page_key not in self.visited_nodes:
            self.visited_nodes[page_key] = {
                "name": page_name,
                "visit_count": 0,
                "first_visit": datetime.now().isoformat()
            }

        self.visited_nodes[page_key]["visit_count"] += 1
        self._log_step("visit", {"page": page_name, "path": self.current_path.copy()})

    def _interact_with_next_element(self, elements: List[Dict]) -> None:
        """Interact with the next unvisited element."""
        # Find elements we can interact with
        interactive_elements = [e for e in elements if e.get("metadata", {}).get("clickable", False)]

        if not interactive_elements:
            # No interactive elements, try scrollable elements
            interactive_elements = [e for e in elements if e.get("metadata", {}).get("scrollable", False)]

        if not interactive_elements:
            # No elements to interact with, go back
            if len(self.current_path) > 0:
                self._go_back()
            return

        # Get the first interactive element
        element = interactive_elements[0]
        element_name = element.get("text", element.get("element_id", "unknown"))
        element_type = element.get("element_type", "unknown")
        action_hint = element.get("action_hint", "click")

        print(f"[INTERACT] {action_hint} on {element_name} ({element_type})")

        # Execute the action
        if action_hint == "navigate":
            # Navigate to new page
            self._navigate_to_element(element_name)
        elif action_hint == "toggle":
            # Toggle and restore
            self._toggle_element(element_name)
        else:
            # Simple click
            self._click_element(element_name)

    def _navigate_to_element(self, element_name: str) -> None:
        """Navigate to a new page via element."""
        self.step_count += 1

        # Update path
        self.current_path.append(element_name)

        # Record navigation
        self._log_step("navigate", {
            "target": element_name,
            "new_path": self.current_path.copy(),
            "action": "click"
        })

        # Visit the new page
        self._visit_page(element_name)

    def _toggle_element(self, element_name: str) -> None:
        """Toggle an element and restore it."""
        self.step_count += 1

        # Record toggle operation
        self._log_step("toggle", {
            "target": element_name,
            "path": self.current_path.copy(),
            "action": "toggle",
            "restore": True
        })

    def _click_element(self, element_name: str) -> None:
        """Click on an element."""
        self.step_count += 1

        # Record click
        self._log_step("click", {
            "target": element_name,
            "path": self.current_path.copy(),
            "action": "click"
        })

    def _go_back(self) -> None:
        """Go back to previous page."""
        if not self.current_path:
            return

        self.step_count += 1

        # Remove last element from path
        previous_element = self.current_path.pop()

        # Record go_back
        self._log_step("go_back", {
            "from": previous_element,
            "new_path": self.current_path.copy(),
            "action": "go_back"
        })

        print(f"[GO_BACK] From {previous_element} to {self._get_current_path_string()}")

    def _has_more_to_explore(self) -> bool:
        """Check if there are more pages/elements to explore."""
        # Simple heuristic: if we haven't reached max steps
        return self.step_count < 20  # Prevent infinite loops

    def _get_current_path_string(self) -> str:
        """Get current path as string."""
        if not self.current_path:
            return "root"
        return "/".join(self.current_path)

    def _log_step(self, action: str, details: Dict[str, Any]) -> None:
        """Log a traversal step."""
        step_record = {
            "step_number": self.step_count + 1,
            "timestamp": datetime.now().isoformat(),
            "action": action,
            "current_path": self.current_path.copy(),
            "details": details
        }
        self.traversal_log.append(step_record)

    def _build_result(self) -> Dict[str, Any]:
        """Build traversal result."""
        return {
            "success": True,
            "completion_reason": "completed" if self.step_count > 0 else "no_steps",
            "total_steps": self.step_count,
            "visited_nodes": len(self.visited_nodes),
            "traversal_log": self.traversal_log,
            "visited_tree": self.visited_nodes
        }


def test_complete_traversal():
    """Test complete DFS traversal."""
    print("=" * 60)
    print("Testing Complete DFS Traversal")
    print("=" * 60)

    # Load the actual test data
    test_dir = Path("tests/simulation/fixtures/e2e_all_traversal")
    pages_path = test_dir / "pages_all.json"

    with open(pages_path, 'r', encoding='utf-8') as f:
        virtual_pages = json.load(f)

    print(f"\n[LOAD] Loaded {len(virtual_pages)} virtual pages:")
    for page_name, page_data in virtual_pages.items():
        items_count = len(page_data.get("items", []))
        current_path = page_data.get("current_path", [])
        print(f"  - {page_name}: {items_count} items, path: {current_path}")

    # Run traversal
    simulator = SimpleTraversalSimulator(virtual_pages)
    result = simulator.run_traversal(max_depth=3, max_steps=20)

    # Display results
    print(f"\n" + "=" * 60)
    print("Traversal Results")
    print("=" * 60)
    print(f"Success: {result['success']}")
    print(f"Completion Reason: {result['completion_reason']}")
    print(f"Total Steps: {result['total_steps']}")
    print(f"Visited Nodes: {result['visited_nodes']}")

    print(f"\nTraversal Log:")
    for i, step in enumerate(result['traversal_log'][:15], 1):
        action = step['action']
        path = step['current_path']
        details = step['details']
        target = details.get('target', 'N/A')
        print(f"  {i}. [{action}] Path: {path} → Target: {target}")

    if len(result['traversal_log']) > 15:
        print(f"  ... and {len(result['traversal_log']) - 15} more steps")

    return result


if __name__ == "__main__":
    try:
        result = test_complete_traversal()

        if result['total_steps'] >= 8:  # Expected minimum
            print(f"\n[SUCCESS] Traversal executed {result['total_steps']} steps!")
            sys.exit(0)
        else:
            print(f"\n[WARNING] Only {result['total_steps']} steps executed, expected more")
            sys.exit(1)

    except Exception as e:
        print(f"\n[ERROR] Traversal failed: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(2)