#!/usr/bin/env python3
"""
Improved DFS traversal simulator with proper backtracking.
"""
import json
import sys
from pathlib import Path
from datetime import datetime
from typing import Any, Dict, List, Set

sys.path.insert(0, str(Path(__file__).parent))

from src.simulation.page_analyzer import PageAnalyzer


class ImprovedTraversalSimulator:
    """
    Improved DFS traversal simulator with proper element tracking and backtracking.
    """

    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        """Initialize simulator with virtual pages."""
        self.virtual_pages = virtual_pages
        self.page_analyzer = PageAnalyzer(virtual_pages)

        # Traversal state
        self.current_path = []
        self.visited_pages: Set[str] = set()
        self.visited_elements: Set[str] = set()
        self.traversal_log = []
        self.step_count = 0

        # Track current position in elements
        self.current_element_index = {}

    def run_traversal(self, max_depth: int = 3, max_steps: int = 20) -> Dict[str, Any]:
        """Run complete DFS traversal with proper backtracking."""
        print(f"[SIMULATOR] Starting improved DFS traversal")
        print(f"[SIMULATOR] Max depth: {max_depth}, Max steps: {max_steps}")

        # Start traversal from root
        self.current_path = []
        self._visit_and_explore_page("root", max_depth)

        # Main traversal loop
        while self.step_count < max_steps:
            # Get current page analysis
            current_page_str = self._get_path_string()

            try:
                page_analysis = self.page_analyzer.analyze_page(current_page_str)
                elements = page_analysis.get("elements", [])

                print(f"\n[STEP {self.step_count + 1}] Path: {current_page_str}")
                print(f"[STEP {self.step_count + 1}] Elements: {len(elements)} available, {len(self.visited_elements)} visited")

                # Check if we need to go back
                if self._should_go_back(elements, max_depth):
                    if not self._go_back():
                        print(f"[SIMULATOR] Traversal complete - returned to root")
                        break
                else:
                    # Interact with next element
                    if not self._interact_with_next_unvisited_element(elements):
                        # No more unvisited elements, go back
                        if not self._go_back():
                            print(f"[SIMULATOR] Traversal complete - no more exploration")
                            break

            except Exception as e:
                print(f"[ERROR] Step {self.step_count + 1}: {e}")
                self._log_step("error", {"error": str(e)})
                break

        return self._build_result()

    def _visit_and_explore_page(self, page_name: str, max_depth: int) -> None:
        """Visit a page and mark it as visited."""
        page_key = self._get_path_string()

        if page_key not in self.visited_pages:
            self.visited_pages.add(page_key)
            self._log_step("visit", {"page": page_name, "path": self.current_path.copy()})
            print(f"[VISIT] Visited page: {page_name} at {page_key}")

            # Initialize element index for this page
            self.current_element_index[page_key] = 0

    def _should_go_back(self, elements: List[Dict], max_depth: int) -> bool:
        """Determine if we should go back."""
        # Go back if:
        # 1. At max depth
        if len(self.current_path) >= max_depth:
            print(f"[DECISION] Go back - at max depth ({max_depth})")
            return True

        # 2. All elements visited
        if self._all_elements_visited(elements):
            print(f"[DECISION] Go back - all elements visited")
            return True

        # 3. No interactive elements
        interactive = [e for e in elements if e.get("metadata", {}).get("clickable", False)]
        if not interactive:
            scrollable = [e for e in elements if e.get("metadata", {}).get("scrollable", False)]
            if not scrollable:
                print(f"[DECISION] Go back - no interactive elements")
                return True

        return False

    def _all_elements_visited(self, elements: List[Dict]) -> bool:
        """Check if all elements have been visited."""
        page_key = self._get_path_string()

        # Create element keys for this page
        element_keys = [self._make_element_key(page_key, e) for e in elements]

        # Check if all are visited
        for key in element_keys:
            if key not in self.visited_elements:
                return False

        return True

    def _interact_with_next_unvisited_element(self, elements: List[Dict]) -> bool:
        """Find and interact with next unvisited element."""
        page_key = self._get_path_string()

        # Find next unvisited element
        start_index = self.current_element_index.get(page_key, 0)

        for i in range(start_index, len(elements)):
            element = elements[i]
            element_key = self._make_element_key(page_key, element)

            if element_key not in self.visited_elements:
                # Found unvisited element
                self.current_element_index[page_key] = i + 1  # Update index

                element_name = element.get("text", element.get("element_id", "unknown"))
                element_type = element.get("element_type", "unknown")
                action_hint = element.get("action_hint", "click")

                print(f"[INTERACT] {action_hint} {element_name} ({element_type})")

                # Execute action based on type
                if action_hint == "navigate":
                    self._navigate_and_explore(element_name, element)
                elif action_hint == "toggle":
                    self._toggle_and_restore(element_name, element)
                else:
                    self._simple_click(element_name, element)

                self.visited_elements.add(element_key)
                return True

        # No unvisited elements found
        return False

    def _navigate_and_explore(self, element_name: str, element: Dict) -> None:
        """Navigate to new page and explore it."""
        self.step_count += 1

        # Update path
        self.current_path.append(element_name)
        new_page_str = self._get_path_string()

        # Record navigation
        self._log_step("navigate", {
            "target": element_name,
            "from": self._get_path_string(exclude_last=True),
            "to": new_page_str,
            "element_type": element.get("element_type", "unknown")
        })

        # Visit and initialize new page
        self._visit_and_explore_page(element_name, max_depth=3)
        print(f"[NAVIGATE] Moved to {new_page_str}")

    def _toggle_and_restore(self, element_name: str, element: Dict) -> None:
        """Toggle element and simulate restore."""
        self.step_count += 1

        # Record toggle operation
        self._log_step("toggle", {
            "target": element_name,
            "path": self.current_path.copy(),
            "action": "toggle",
            "restore": True,
            "element_type": element.get("element_type", "unknown")
        })

        print(f"[TOGGLE] Toggled {element_name} and restored")

    def _simple_click(self, element_name: str, element: Dict) -> None:
        """Simple click on element."""
        self.step_count += 1

        # Record click
        self._log_step("click", {
            "target": element_name,
            "path": self.current_path.copy(),
            "element_type": element.get("element_type", "unknown")
        })

    def _go_back(self) -> bool:
        """Go back to previous page."""
        if not self.current_path:
            return False  # Already at root

        self.step_count += 1

        # Remove last element from path
        previous_element = self.current_path.pop()
        old_path_str = f"{self._get_path_string()}/{previous_element}"
        new_path_str = self._get_path_string()

        # Record go_back
        self._log_step("go_back", {
            "from": previous_element,
            "new_path": self.current_path.copy(),
            "old_path": old_path_str
        })

        print(f"[GO_BACK] From {previous_element} to {new_path_str}")
        return True

    def _make_element_key(self, page_key: str, element: Dict) -> str:
        """Create unique key for element."""
        element_id = element.get("element_id", element.get("text", "unknown"))
        return f"{page_key}/{element_id}"

    def _get_path_string(self, exclude_last: bool = False) -> str:
        """Get current path as string."""
        path = self.current_path[:-1] if exclude_last and self.current_path else self.current_path

        if not path:
            return "root"
        return "/".join(path)

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
            "completion_reason": "completed",
            "total_steps": self.step_count,
            "visited_nodes": len(self.visited_pages),
            "visited_elements": len(self.visited_elements),
            "traversal_log": self.traversal_log
        }


def test_improved_traversal():
    """Test improved DFS traversal."""
    print("=" * 70)
    print("Testing Improved DFS Traversal with Backtracking")
    print("=" * 70)

    # Load test data
    test_dir = Path("tests/simulation/fixtures/e2e_all_traversal")
    pages_path = test_dir / "pages_all.json"

    with open(pages_path, 'r', encoding='utf-8') as f:
        virtual_pages = json.load(f)

    print(f"\n[LOAD] Loaded {len(virtual_pages)} virtual pages")

    # Run traversal
    simulator = ImprovedTraversalSimulator(virtual_pages)
    result = simulator.run_traversal(max_depth=3, max_steps=20)

    # Display results
    print(f"\n" + "=" * 70)
    print("Traversal Results")
    print("=" * 70)
    print(f"Success: {result['success']}")
    print(f"Total Steps: {result['total_steps']}")
    print(f"Visited Pages: {result['visited_nodes']}")
    print(f"Visited Elements: {result['visited_elements']}")

    print(f"\nTraversal Sequence:")
    for i, step in enumerate(result['traversal_log'][:20], 1):
        action = step['action']
        path = step['current_path']
        details = step['details']

        if action == "navigate":
            target = details.get('target', 'N/A')
            to_page = details.get('to', 'N/A')
            print(f"  {i}. [NAVIGATE] {target} → {to_page}")
        elif action == "go_back":
            from_elem = details.get('from', 'N/A')
            to_path = details.get('new_path', 'N/A')
            print(f"  {i}. [GO_BACK] From {from_elem} → {to_path}")
        elif action == "toggle":
            target = details.get('target', 'N/A')
            restore = details.get('restore', False)
            print(f"  {i}. [TOGGLE] {target} (restore={restore})")
        else:
            print(f"  {i}. [{action.upper()}] {details}")

    if len(result['traversal_log']) > 20:
        print(f"  ... and {len(result['traversal_log']) - 20} more steps")

    return result


if __name__ == "__main__":
    try:
        result = test_improved_traversal()

        if result['total_steps'] >= 8:
            print(f"\n[SUCCESS] Traversal executed {result['total_steps']} steps with proper DFS!")
            sys.exit(0)
        else:
            print(f"\n[WARNING] Only {result['total_steps']} steps, expected more")
            sys.exit(1)

    except Exception as e:
        print(f"\n[ERROR] Traversal failed: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(2)