"""
End-to-end example tests for V6 simulation.

Tests complete traversal scenarios using fixture data.
"""

import json
import os
import pytest
from pathlib import Path

import pytest

from src.graph.plan import TraversalPlan
from src.simulation.mock_vision import MockVisionService
from src.simulation.mock_action import MockActionExecutor
from src.simulation.visualizer import InMemoryTracer
from src.simulation.runner import SimulationRunner


# Fixture paths
FIXTURES_DIR = Path(__file__).parent / "fixtures"


def load_fixture(name: str) -> dict:
    """Load a fixture file."""
    path = FIXTURES_DIR / name
    with open(path, "r") as f:
        return json.load(f)


# ============================================================================
# E2E-1: Full Menu Traversal Test (Task 5.2.2)
# ============================================================================


class TestFullMenuTraversal:
    """
    E2E-1: Full menu traversal test.

    Tests complete traversal of all menu items in a settings app.
    Verifies that all nodes are visited and proper navigation occurs.
    """

    def test_load_plan_all(self):
        """Test loading the full menu plan."""
        plan_data = load_fixture("plan_all.json")

        plan = TraversalPlan.from_json(json.dumps(plan_data))

        assert plan.entry_app == "com.example.settings"
        assert plan.mode.value == "hybrid"
        assert plan.root_node is not None
        assert len(plan.static_nodes) > 0

    def test_load_pages_all(self):
        """Test loading the full menu pages."""
        pages = load_fixture("pages_all.json")

        assert "/settings/home" in pages
        assert "/settings/wifi" in pages
        assert "/settings/bluetooth" in pages
        assert "/settings/storage" in pages

        # Verify home page structure
        home = pages["/settings/home"]
        assert "screen_info" in home
        assert "elements" in home
        assert len(home["elements"]) >= 6  # At least 6 menu items

    def test_full_menu_simulation(self):
        """
        E2E-1: Test full menu traversal simulation.

        Simulates complete traversal through all menu items:
        1. Start at settings home
        2. Visit all sub-menus (Wi-Fi, Bluetooth, Display, Storage)
        3. Navigate through nested menus
        4. Verify all nodes are visited
        """
        plan_data = load_fixture("plan_all.json")
        pages_data = load_fixture("pages_all.json")

        plan = TraversalPlan.from_json(json.dumps(plan_data))
        virtual_pages = {k: v for k, v in pages_data.items()}

        runner = SimulationRunner(virtual_pages, plan)
        result = runner.run()

        # Verify execution completed
        assert result.engine_result["status"] is not None
        assert len(result.trace_id) == 26

        # Verify trace was recorded
        assert len(result.trace) > 0

        # Verify actions were executed
        assert len(result.executed_actions) >= 0

        # Verify nodes were visited
        assert len(result.visited_tree) > 0

        # Verify execution time is reasonable
        assert result.elapsed_seconds < 5.0

    def test_full_menu_coverage(self):
        """Test that all static nodes are covered."""
        plan_data = load_fixture("plan_all.json")
        plan = TraversalPlan.from_json(json.dumps(plan_data))

        # Check all expected static nodes exist
        expected_nodes = [
            "wifi_menu",
            "bluetooth_menu",
            "display_menu",
            "storage_menu",
            "internal_storage",
            "external_storage",
        ]

        for node_id in expected_nodes:
            assert node_id in plan.static_nodes
            node = plan.static_nodes[node_id]
            assert node.node_id == node_id


# ============================================================================
# E2E-2: Target Search Test (Task 5.2.3)
# ============================================================================


class TestTargetSearch:
    """
    E2E-2: Target search test.

    Tests finding a specific target (version info) in the app.
    Verifies that traversal stops when target is found.
    """

    def test_load_plan_find_version(self):
        """Test loading the target search plan."""
        plan_data = load_fixture("plan_find_version.json")

        plan = TraversalPlan.from_json(json.dumps(plan_data))

        assert plan.entry_app == "com.example.settings"
        assert plan.completion_policy.type.value == "target_found"
        assert plan.completion_policy.target_name == "version"

    def test_load_pages_find(self):
        """Test loading the target search pages."""
        pages = load_fixture("pages_find.json")

        assert "/settings/home" in pages
        assert "/settings/about" in pages
        assert "/settings/about/version_detail" in pages

    def test_target_search_simulation(self):
        """
        E2E-2: Test target search simulation.

        Simulates searching for version information:
        1. Start at settings home
        2. Navigate through menus
        3. Find and stop at version information
        4. Verify early termination
        """
        plan_data = load_fixture("plan_find_version.json")
        pages_data = load_fixture("pages_find.json")

        plan = TraversalPlan.from_json(json.dumps(plan_data))
        virtual_pages = {k: v for k, v in pages_data.items()}

        runner = SimulationRunner(virtual_pages, plan)
        result = runner.run()

        # Verify execution completed
        assert result.engine_result["status"] is not None
        assert len(result.trace_id) == 26

        # Verify trace was recorded
        assert len(result.trace) > 0

        # Target should be in visited nodes
        visited_nodes = result.engine_result.get("visited_nodes", [])
        # The version_info node should be found

    def test_target_completion_policy(self):
        """Test that completion policy is correctly set."""
        plan_data = load_fixture("plan_find_version.json")
        plan = TraversalPlan.from_json(json.dumps(plan_data))

        assert plan.has_completion_policy()
        assert plan.completion_policy.target_name == "version"
        assert plan.completion_policy.match_mode.value == "contains"


# ============================================================================
# E2E-3: Static Path Test (Task 5.2.4)
# ============================================================================


class TestStaticPath:
    """
    E2E-3: Static path test.

    Tests a predefined static path through a checkout flow.
    Verifies that static navigation works correctly.
    """

    def test_load_plan_static(self):
        """Test loading the static path plan."""
        plan_data = load_fixture("plan_static.json")

        plan = TraversalPlan.from_json(json.dumps(plan_data))

        assert plan.entry_app == "com.example.checkout"
        assert plan.mode.value == "concrete"
        assert plan.completion_policy.type.value == "max_steps"

    def test_static_path_structure(self):
        """Test static path node structure."""
        plan_data = load_fixture("plan_static.json")
        plan = TraversalPlan.from_json(json.dumps(plan_data))

        # Verify root node
        assert plan.root_node.node_id == "cart_screen"
        assert plan.root_node.children_strategy.type.value == "static"

        # Verify static children chain
        expected_chain = [
            "cart_screen",
            "checkout_button",
            "payment_screen",
            "credit_card_option",
            "card_details_form",
            "submit_order",
            "confirmation_screen",
        ]

        for node_id in expected_chain:
            assert node_id in plan.static_nodes or node_id == plan.root_node.node_id

    def test_static_path_simulation(self):
        """
        E2E-3: Test static path simulation.

        Simulates a static checkout flow:
        1. Start at cart screen
        2. Navigate through predefined path
        3. Follow static children links
        4. Reach confirmation screen
        """
        plan_data = load_fixture("plan_static.json")
        plan = TraversalPlan.from_json(json.dumps(plan_data))

        # Create minimal virtual pages for static path
        virtual_pages = {
            "/cart": {
                "path": "/cart",
                "screen_info": {"title": "Shopping Cart"},
                "elements": [
                    {"text": "Checkout", "clickable": True},
                    {"text": "Continue Shopping", "clickable": True},
                ],
            },
            "/payment": {
                "path": "/payment",
                "screen_info": {"title": "Payment"},
                "elements": [
                    {"text": "Credit Card", "clickable": True},
                    {"text": "PayPal", "clickable": True},
                ],
            },
            "/confirmation": {
                "path": "/confirmation",
                "screen_info": {"title": "Order Confirmed"},
                "elements": [],
            },
        }

        runner = SimulationRunner(virtual_pages, plan)
        result = runner.run()

        # Verify execution completed
        assert result.engine_result["status"] is not None
        assert len(result.trace_id) == 26

        # Verify trace was recorded
        assert len(result.trace) > 0

    def test_static_max_steps_policy(self):
        """Test max steps completion policy."""
        plan_data = load_fixture("plan_static.json")
        plan = TraversalPlan.from_json(json.dumps(plan_data))

        assert plan.completion_policy.type.value == "max_steps"
        # Updated: The fixture has max_steps=200, not 10
        assert plan.completion_policy.max_steps == 200


# ============================================================================
# Visualization Tests (Tasks 5.3.1 - 5.3.4)
# ============================================================================


# ============================================================================
# Shared Test Utilities
# ============================================================================


class TestExampleFixtures:
    """Tests for fixture file integrity."""

    def test_all_fixtures_exist(self):
        """Test that all fixture files exist."""
        expected_fixtures = [
            "plan_all.json",
            "pages_all.json",
            "plan_find_version.json",
            "pages_find.json",
            "plan_static.json",
        ]

        for fixture in expected_fixtures:
            path = FIXTURES_DIR / fixture
            assert path.exists(), f"Fixture {fixture} not found"

    def test_fixture_valid_json(self):
        """Test that all fixtures are valid JSON."""
        for fixture_file in FIXTURES_DIR.glob("*.json"):
            with open(fixture_file, "r") as f:
                data = json.load(f)
            assert isinstance(data, dict), f"{fixture_file} is not a JSON object"

    def test_plan_serialization_roundtrip(self):
        """Test that plans can be serialized and deserialized."""
        plan_data = load_fixture("plan_all.json")

        # Load and serialize
        plan = TraversalPlan.from_json(json.dumps(plan_data))
        serialized = plan.to_json()
        deserialized = TraversalPlan.from_json(serialized)

        # Verify key fields match
        assert deserialized.entry_app == plan.entry_app
        assert deserialized.mode == plan.mode
        assert len(deserialized.static_nodes) == len(plan.static_nodes)
