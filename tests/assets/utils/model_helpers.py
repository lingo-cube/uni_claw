"""Model testing helper functions.

This module provides utility functions for testing business models,
including sample data generation and validation helpers.
"""

from typing import Any, Dict
from datetime import datetime


def create_sample_coordinate(x: float = 0.5, y: float = 0.5) -> Dict[str, float]:
    """Create a sample coordinate dict for testing.

    Args:
        x: X coordinate (0-1)
        y: Y coordinate (0-1)

    Returns:
        Dictionary with x and y keys
    """
    return {"x": x, "y": y}


def create_sample_menu_info(name: str = "Test Menu", active: bool = False) -> Dict[str, Any]:
    """Create a sample MenuInfo dict for testing.

    Args:
        name: Menu name
        active: Whether menu is active

    Returns:
        Dictionary with MenuInfo fields
    """
    return {
        "name": name,
        "coordinate": create_sample_coordinate(),
        "active": active
    }


def create_sample_menu_item(
    name: str = "Test Item",
    item_type: str = "menu_item",
    expected_action: str = "navigate"
) -> Dict[str, Any]:
    """Create a sample MenuItem dict for testing.

    Args:
        name: Item name
        item_type: MenuItemType value
        expected_action: ExpectedAction value

    Returns:
        Dictionary with MenuItem fields
    """
    return {
        "name": name,
        "type": item_type,
        "coordinate": create_sample_coordinate(),
        "expected_action": expected_action,
        "expects_page_change": expected_action == "navigate",
        "expects_state_change": expected_action == "toggle",
        "parent": None,
        "confidence": 1.0,
        "safety_tag": None
    }


def create_sample_page_analysis() -> Dict[str, Any]:
    """Create a sample PageAnalysis dict for testing.

    Returns:
        Dictionary with PageAnalysis fields
    """
    return {
        "level1_dir": "left",
        "level1_menus": [
            create_sample_menu_info("Menu1"),
            create_sample_menu_info("Menu2")
        ],
        "level2_dir": "top",
        "level2_menus": [
            create_sample_menu_info("Tab1", active=True),
            create_sample_menu_info("Tab2")
        ],
        "current_path": ["Root", "Menu1"],
        "items": [
            create_sample_menu_item("Item1"),
            create_sample_menu_item("Item2", item_type="switch", expected_action="toggle")
        ],
        "is_popup": False,
        "popup_info": None,
        "close_button": None,
        "back_button": create_sample_coordinate(0.1, 0.1),
        "has_scroll": True,
        "is_end_of_list": False
    }


def create_sample_operation(
    action: str = "click",
    by: str = "text",
    value: Any = "Test"
) -> Dict[str, Any]:
    """Create a sample Operation dict for testing.

    Args:
        action: Operation action type
        by: Target by type
        value: Target value

    Returns:
        Dictionary with Operation fields
    """
    return {
        "action": action,
        "target": {"by": by, "value": value, "meta": {}} if by else None,
        "params": {},
        "restore": None
    }


def create_sample_traversal_node(
    node_id: str = "test_node",
    name: str = "Test Node",
    node_type: str = "container"
) -> Dict[str, Any]:
    """Create a sample TraversalNode dict for testing.

    Args:
        node_id: Node ID
        name: Node name
        node_type: Node type

    Returns:
        Dictionary with TraversalNode fields
    """
    return {
        "node_id": node_id,
        "name": name,
        "node_type": node_type,
        "operation": create_sample_operation(),
        "precondition": None,
        "children_strategy": {
            "type": "static",
            "static_children": [],
            "dynamic_rules": {},
            "max_children": 100
        },
        "exit_condition": None,
        "error_policy": None,
        "meta": {}
    }


def validate_model_timestamp(timestamp: datetime) -> bool:
    """Validate that a timestamp is recent (within last hour).

    Args:
        timestamp: Timestamp to validate

    Returns:
        True if timestamp is within acceptable range
    """
    if not timestamp:
        return False
    time_diff = datetime.now() - timestamp
    return time_diff.total_seconds() >= 0 and time_diff.total_seconds() <= 3600


def assert_dict_contains_keys(data: Dict[str, Any], required_keys: set) -> None:
    """Assert that dictionary contains all required keys.

    Args:
        data: Dictionary to check
        required_keys: Set of required key names

    Raises:
        AssertionError: If any required key is missing
    """
    missing_keys = required_keys - set(data.keys())
    if missing_keys:
        raise AssertionError(f"Missing required keys: {missing_keys}")


def assert_enum_value_valid(enum_class: type, value: str) -> None:
    """Assert that a value is valid for an enum class.

    Args:
        enum_class: Enum class to check against
        value: Value to validate

    Raises:
        AssertionError: If value is not valid for the enum
    """
    if value not in [e.value for e in enum_class]:
        valid_values = [e.value for e in enum_class]
        raise AssertionError(
            f"Invalid value '{value}' for {enum_class.__name__}. "
            f"Valid values: {valid_values}"
        )
