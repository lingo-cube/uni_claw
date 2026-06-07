"""State fixture for simulation testing.

Defines YAML-based fixture format for page states and transition rules.
Supports stateful mock services with page navigation simulation.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Union

import yaml
from pydantic import BaseModel, Field


@dataclass
class PageElement:
    """Represents an interactive element on a page.

    Attributes:
        id: Unique identifier for the element
        type: Element type string (e.g., "button", "switch", "tab")
        text: Display text of the element
        coordinate: Element position as {x: float, y: float}
        action_target: Optional target page/action for this element
    """

    id: str
    type: str
    text: str
    coordinate: Dict[str, float]
    action_target: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "id": self.id,
            "type": self.type,
            "text": self.text,
            "coordinate": self.coordinate,
            "action_target": self.action_target,
        }


@dataclass
class PageTransition:
    """Represents a page transition rule.

    Attributes:
        id: Unique identifier for the transition
        trigger: Element ID that triggers this transition
        from_page: Source page ID
        to_page: Target page ID
        action: Action type (e.g., "click", "back", "swipe")
    """

    id: str
    trigger: str
    from_page: str
    to_page: str
    action: str

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "id": self.id,
            "trigger": self.trigger,
            "from_page": self.from_page,
            "to_page": self.to_page,
            "action": self.action,
        }


@dataclass
class PageState:
    """Represents the state of a page.

    Attributes:
        id: Unique page identifier
        page_name: Human-readable page name
        elements: List of interactive elements on the page
        is_complete: Whether the page is in a complete state
    """

    id: str
    page_name: str
    elements: List[PageElement]
    is_complete: bool = False

    def get_element(self, element_id: str) -> Optional[PageElement]:
        """Get an element by ID.

        Args:
            element_id: The element ID to find

        Returns:
            The PageElement if found, None otherwise
        """
        for element in self.elements:
            if element.id == element_id:
                return element
        return None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            "id": self.id,
            "page_name": self.page_name,
            "elements": [e.to_dict() for e in self.elements],
            "is_complete": self.is_complete,
        }


class StateFixture:
    """Fixture for defining page states and transition rules.

    Supports loading from YAML format and provides methods for
    navigating between pages based on element actions.

    Attributes:
        pages: Dictionary mapping page_id to PageState
        transitions: List of PageTransition rules
        initial_page_id: The starting page ID
        current_page_id: Current active page (for runtime tracking)
        navigation_history: History of page transitions
        history_depth: Maximum depth of navigation history to keep
    """

    def __init__(
        self,
        pages: Dict[str, PageState],
        transitions: List[PageTransition],
        initial_page_id: Optional[str] = None,
        history_depth: int = 10,
    ):
        """Initialize a StateFixture.

        Args:
            pages: Dictionary of page states
            transitions: List of page transition rules
            initial_page_id: Starting page ID (defaults to first page)
            history_depth: Maximum navigation history depth
        """
        self.pages = pages
        self.transitions = transitions
        self.initial_page_id = initial_page_id
        self.current_page_id = initial_page_id
        self.navigation_history: List[str] = []
        self.history_depth = history_depth

        # Build element index for validation
        self._element_index: Dict[str, str] = {}  # element_id -> page_id
        self._build_element_index()

        # Set default initial page if not specified
        if self.initial_page_id is None and self.pages:
            self.initial_page_id = next(iter(self.pages.keys()))
            self.current_page_id = self.initial_page_id

    @classmethod
    def from_yaml(cls, yaml_path: Union[str, Path]) -> "StateFixture":
        """Load a StateFixture from a YAML file.

        Args:
            yaml_path: Path to the YAML fixture file

        Returns:
            A new StateFixture instance

        Raises:
            ValueError: If the YAML file is invalid
            FileNotFoundError: If the file doesn't exist
        """
        path = Path(yaml_path)
        if not path.exists():
            raise FileNotFoundError(f"Fixture file not found: {yaml_path}")

        with open(path, "r") as f:
            data = yaml.safe_load(f)

        # Parse pages
        pages: Dict[str, PageState] = {}
        pages_data = data.get("pages", {})
        for page_id, page_data in pages_data.items():
            elements = []
            for elem_data in page_data.get("elements", []):
                element = PageElement(
                    id=elem_data["id"],
                    type=elem_data["type"],
                    text=elem_data.get("text", ""),
                    coordinate=elem_data.get("coordinate", {"x": 0.5, "y": 0.5}),
                    action_target=elem_data.get("action_target"),
                )
                elements.append(element)

            pages[page_id] = PageState(
                id=page_id,
                page_name=page_data.get("page_name", page_id),
                elements=elements,
                is_complete=page_data.get("is_complete", False),
            )

        # Parse transitions
        transitions: List[PageTransition] = []
        for trans_id, trans_data in data.get("transitions", {}).items():
            transition = PageTransition(
                id=trans_id,
                trigger=trans_data["trigger"],
                from_page=trans_data["from_page"],
                to_page=trans_data["to_page"],
                action=trans_data.get("action", "click"),
            )
            transitions.append(transition)

        # Create fixture
        initial_page = data.get("initial_page")
        history_depth = data.get("history_depth", 10)

        return cls(
            pages=pages,
            transitions=transitions,
            initial_page_id=initial_page,
            history_depth=history_depth,
        )

    def _build_element_index(self) -> None:
        """Build an index mapping element IDs to their page IDs."""
        self._element_index.clear()
        for page_id, page_state in self.pages.items():
            for element in page_state.elements:
                self._element_index[element.id] = page_id

    def validate(self) -> List[str]:
        """Validate the fixture configuration.

        Returns:
            List of validation error messages (empty if valid)
        """
        errors: List[str] = []

        # Check transitions reference valid pages
        for transition in self.transitions:
            if transition.from_page not in self.pages:
                errors.append(
                    f"Transition '{transition.id}': from_page "
                    f"'{transition.from_page}' not found"
                )

            if transition.to_page not in self.pages:
                errors.append(
                    f"Transition '{transition.id}': to_page "
                    f"'{transition.to_page}' not found"
                )

            # Check trigger element exists in from_page
            if transition.from_page in self.pages:
                from_page_state = self.pages[transition.from_page]
                trigger_element = from_page_state.get_element(transition.trigger)
                if trigger_element is None:
                    errors.append(
                        f"Transition '{transition.id}': trigger element "
                        f"'{transition.trigger}' not found in page '{transition.from_page}'"
                    )

        return errors

    def get_page(self, page_id: str) -> Optional[PageState]:
        """Get a page state by ID.

        Args:
            page_id: The page ID to retrieve

        Returns:
            The PageState if found, None otherwise
        """
        return self.pages.get(page_id)

    def get_transition(
        self, trigger_element_id: str, from_page_id: str, action: str = "click"
    ) -> Optional[PageTransition]:
        """Find a transition matching the trigger and action.

        Supports matching by element ID or by element text.

        Args:
            trigger_element_id: Element ID or text that triggers the transition
            from_page_id: Source page ID
            action: Action type (default: "click")

        Returns:
            The matching PageTransition if found, None otherwise
        """
        for transition in self.transitions:
            if (
                transition.from_page == from_page_id
                and transition.action == action
            ):
                # Match by exact trigger ID
                if transition.trigger == trigger_element_id:
                    return transition
                # Also match by element text (if trigger is a text value)
                from_page = self.pages.get(from_page_id)
                if from_page:
                    for element in from_page.elements:
                        if element.text == trigger_element_id and element.id == transition.trigger:
                            return transition
        return None

    def get_initial_page(self) -> Optional[PageState]:
        """Get the initial page state.

        Returns:
            The initial PageState, or None if no pages exist
        """
        if self.initial_page_id:
            return self.pages.get(self.initial_page_id)
        if self.pages:
            return next(iter(self.pages.values()))
        return None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation.

        Returns:
            Dictionary with all fixture data
        """
        return {
            "pages": {pid: p.to_dict() for pid, p in self.pages.items()},
            "transitions": [t.to_dict() for t in self.transitions],
            "initial_page": self.initial_page_id,
            "history_depth": self.history_depth,
        }
