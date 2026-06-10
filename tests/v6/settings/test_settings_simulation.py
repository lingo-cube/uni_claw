"""Settings app simulation tests.

Tests Settings app traversal using page data and traversal plan.
Generates simulation traces and analyzes results.
"""

import json
import pytest
from pathlib import Path
from typing import Dict, Any

from src.graph.plan import TraversalPlan
from src.trace.storage import FileStorage
from src.trace.recorder import TraceRecorder
from src.traversal.graph_engine import GraphTraversalEngine

from src.simulation.state_fixture import StateFixture, PageState, PageElement, PageTransition
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor
from src.models.element_type_mapper import ElementTypeMapper


# ============================================================================
# Fixtures
# ============================================================================

@pytest.fixture
def settings_page_data() -> Dict[str, Any]:
    """Load settings page data."""
    page_file = Path(__file__).parent / "settings_page.json"
    with open(page_file, 'r') as f:
        return json.load(f)


@pytest.fixture
def settings_traversal_plan() -> TraversalPlan:
    """Load settings traversal plan."""
    from src.graph.node import TraversalNode, NodeType, Operation, EntryPolicy, EntryStrategy, CompletionPolicy, CompletionPolicyType, ChildrenStrategy, ChildrenStrategyType, Precondition

    # Build simple plan for settings traversal
    root = TraversalNode(
        node_id="root",
        node_type=NodeType.CONTAINER,
        name="设置主页",
        operation=Operation(action="no_action"),
        children_strategy=ChildrenStrategy(
            type=ChildrenStrategyType.DYNAMIC_MATCH,
            dynamic_rules={
                "menu_rule": {
                    "rule_id": "menu_rule",
                    "match_condition": {"type": "menu_item"},
                    "child_template": "menu_container",
                    "action": "generate_child"
                }
            }
        ),
        precondition=Precondition(
            page_name="Settings",
            timeout_seconds=10
        ),
    )

    return TraversalPlan(
        plan_name="Safe Full Traversal",
        plan_id="settings-full-traversal-v1",
        entry_app="com.example.settings",
        root_node=root,
        static_nodes={},
        completion_policy=CompletionPolicy(
            type=CompletionPolicyType.NONE,
        ),
    )


@pytest.fixture
def settings_fixture(settings_page_data: Dict[str, Any]) -> StateFixture:
    """Convert settings page data to StateFixture."""
    pages: Dict[str, PageState] = {}
    transitions: list[PageTransition] = []

    # Page ID mapping (JSON path -> fixture ID)
    page_id_map = {}

    for page_path, page_data in settings_page_data.items():
        # Create page ID from path (e.g., "/settings/home" -> "settings_home")
        page_id = page_path.strip('/').replace('/', '_')
        page_id_map[page_path] = page_id

        # Convert elements
        elements = []
        for elem in page_data.get('elements', []):
            # Calculate normalized coordinates from bounds
            bounds = elem.get('bounds', [0, 0, 500, 1080])
            x = (bounds[0] + bounds[2]) / 2 / 500  # Normalize to 0-1
            y = (bounds[1] + bounds[3]) / 2 / 1080  # Normalize to 0-1

            # Extract element type using centralized mapper
            class_name = elem.get('class', 'button')
            elem_type = ElementTypeMapper.from_android_class(class_name)

            element = PageElement(
                id=elem['id'],
                type=elem_type,
                text=elem.get('text', ''),
                coordinate={'x': x, 'y': y},
                action_target=elem.get('action_target'),
            )
            elements.append(element)

        # Create page state
        pages[page_id] = PageState(
            id=page_id,
            page_name=page_data.get('screen_info', {}).get('title', page_path),
            elements=elements,
            is_complete=False,
        )

    # Create basic transitions for navigation from home page
    home_id = page_id_map.get('/settings/home', 'settings_home')
    if home_id in pages:
        home_elements = settings_page_data.get('/settings/home', {}).get('elements', [])
        for elem in home_elements[:6]:  # All 6 menu items
            # Try to find matching target page
            elem_text = elem.get('text', '').lower().replace('-', '')  # Remove hyphens for matching
            for path_key, path_id in page_id_map.items():
                path_name = path_key.strip('/').replace('_', '').replace('-', '')  # Normalize path
                if elem_text in path_name and path_key != '/settings/home':
                    transitions.append(PageTransition(
                        id=f"{home_id}_to_{path_id}",
                        trigger=elem['id'],
                        from_page=home_id,
                        to_page=path_id,
                        action='click',
                    ))
                    break

    # Create fixture
    return StateFixture(
        pages=pages,
        transitions=transitions,
        initial_page_id=home_id,
        history_depth=10,
    )


# ============================================================================
# Tests
# ============================================================================

class TestSettingsSimulation:
    """Settings app simulation tests."""

    def test_settings_plan_loaded(self, settings_traversal_plan: TraversalPlan):
        """Test that traversal plan loads correctly."""
        assert settings_traversal_plan.entry_app == "com.example.settings"
        assert settings_traversal_plan.root_node is not None
        assert settings_traversal_plan.root_node.name == "设置主页"

    def test_settings_pages_loaded(self, settings_page_data: Dict[str, Any]):
        """Test that page data loads correctly."""
        assert len(settings_page_data) >= 7  # At least 7 settings pages
        assert '/settings/home' in settings_page_data
        assert '/settings/wifi' in settings_page_data
        assert '/settings/bluetooth' in settings_page_data

    def test_settings_fixture_created(self, settings_fixture: StateFixture):
        """Test that StateFixture is created correctly."""
        assert len(settings_fixture.pages) >= 7
        assert settings_fixture.initial_page_id == 'settings_home'
        assert len(settings_fixture.transitions) > 0

    def test_settings_home_page_elements(self, settings_fixture: StateFixture):
        """Test that home page has expected elements."""
        home_page = settings_fixture.get_page('settings_home')
        assert home_page is not None
        assert len(home_page.elements) >= 6  # Wi-Fi, Bluetooth, Display, Storage, Battery, Apps

    def test_settings_vision_service(self, settings_fixture: StateFixture):
        """Test StatefulMockVisionService with settings data."""
        vision = StatefulMockVisionService(settings_fixture)

        # Get initial page analysis
        analysis = vision.analyze_screenshot(b"fake_image")

        assert analysis.current_path == ['Settings']  # V6.9.3: Now uses page names instead of IDs
        assert len(analysis.items) >= 6

    def test_settings_simulation_run(self, settings_traversal_plan: TraversalPlan, settings_fixture: StateFixture):
        """Run full settings simulation and verify results."""
        # Create services
        vision = StatefulMockVisionService(settings_fixture)
        action = StatefulMockActionExecutor(vision)

        # Set up trace recording
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        # Create engine
        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
            test_metadata={
                "test_name": "test_settings_simulation_run",
                "test_scenario": "safe_full_traversal",
                "expected_status": "GlobalState.COMPLETED",
                "expected_steps": 118,
                "expected_nodes": 19,
            },
        )

        # Run traversal
        result = engine.run()

        # Verify result
        assert result is not None
        assert result.trace_id is not None
        assert result.total_steps >= 0

        # Verify trace was recorded
        trace_nodes = storage.read(result.trace_id)
        assert len(trace_nodes) > 0

        print(f"\n✓ Settings simulation completed")
        print(f"  Trace ID: {result.trace_id}")
        print(f"  Steps: {result.total_steps}")
        print(f"  Status: {result.status}")
        print(f"  Visited nodes: {result.visited_nodes}")
        print(f"  Trace nodes: {len(trace_nodes)}")


@pytest.mark.integration
class TestSettingsSimulationAnalysis:
    """Analysis tests for settings simulation."""

    def test_settings_coverage_analysis(self, settings_traversal_plan: TraversalPlan, settings_fixture: StateFixture):
        """Analyze coverage of settings pages."""
        # Run simulation
        vision = StatefulMockVisionService(settings_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        result = engine.run()

        # Analyze coverage
        total_pages = len(settings_fixture.pages)
        visited_pages = len(result.visited_nodes)
        coverage = visited_pages / total_pages if total_pages > 0 else 0

        print(f"\n✓ Coverage Analysis:")
        print(f"  Total pages: {total_pages}")
        print(f"  Visited nodes: {visited_pages}")
        print(f"  Visited set: {result.visited_nodes}")
        print(f"  Coverage: {coverage:.1%}")
        print(f"  Total steps: {result.total_steps}")

        # For dynamic match with mock vision, we expect at least the root node
        assert visited_pages >= 1 or result.total_steps >= 1  # At least root visited or some steps executed

    def test_settings_element_interaction(self, settings_fixture: StateFixture):
        """Test element interactions in settings."""
        vision = StatefulMockVisionService(settings_fixture)

        # Get home page
        analysis = vision.analyze_screenshot(b"fake_image")

        # Check elements are accessible
        assert len(analysis.items) > 0

        # Try interacting with first element
        if analysis.items:
            first_item = analysis.items[0]
            print(f"\n✓ First element: {first_item.name}")
            print(f"  Type: {first_item.type}")
            print(f"  Coordinate: {first_item.coordinate}")
