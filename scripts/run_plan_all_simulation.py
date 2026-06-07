#!/usr/bin/env python
"""
Run simulation using pages_all.json and plan_all.json fixtures.

This script loads the complete traversal plan from plan_all.json and
executes it against the page scenarios defined in pages_all.json,
generating trace data for dashboard visualization.
"""

import json
import sys
from pathlib import Path
from typing import Any, Dict

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.graph.plan import TraversalPlan
from src.trace.storage import FileStorage
from src.trace.recorder import TraceRecorder
from src.traversal.graph_engine import GraphTraversalEngine
from src.simulation.state_fixture import StateFixture, PageState, PageElement, PageTransition
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor


def load_pages_all(json_path: str) -> Dict[str, PageState]:
    """Load pages from pages_all.json format.

    Args:
        json_path: Path to pages_all.json file

    Returns:
        Dictionary mapping page path to PageState objects
    """
    with open(json_path, 'r') as f:
        data = json.load(f)

    pages: Dict[str, PageState] = {}

    for page_path, page_data in data.items():
        # Convert elements from JSON format to PageElement
        elements = []
        for elem in page_data.get('elements', []):
            # Calculate normalized coordinates from bounds
            bounds = elem.get('bounds', [0, 0, 500, 900])
            x = (bounds[0] + bounds[2]) / 2 / 500  # Normalize to 0-1
            y = (bounds[1] + bounds[3]) / 2 / 900  # Normalize to 0-1

            element = PageElement(
                id=elem['id'],
                type=elem.get('class', 'unknown').split('.')[-1],  # Get simple class name
                text=elem.get('text', ''),
                coordinate={'x': x, 'y': y},
            )
            elements.append(element)

        # Create page state using path as ID
        page_id = page_path.strip('/')
        pages[page_id] = PageState(
            id=page_id,
            page_name=page_data.get('screen_info', {}).get('title', page_id),
            elements=elements,
            is_complete=True,
        )

    return pages


def create_transitions_from_pages(pages: Dict[str, PageState]) -> Dict[str, PageTransition]:
    """Create transitions based on page structure.

    Maps transitions from the settings home page to sub-pages based on
    the plan structure in plan_all.json.

    Args:
        pages: Dictionary of PageState objects

    Returns:
        Dictionary of transition ID to PageTransition
    """
    transitions: Dict[str, PageTransition] = {}

    # Define transitions based on the settings menu structure
    # From home page to sub-pages
    home_page = pages.get('settings/home')
    if home_page:
        # Map menu items to their target pages
        menu_transitions = {
            'menu_wifi': 'settings/wifi',
            'menu_bluetooth': 'settings/bluetooth',
            'menu_display': 'settings/display',
            'menu_storage': 'settings/storage',
        }

        for elem_id, target_page in menu_transitions.items():
            trans_id = f"to_{target_page.replace('/', '_')}"
            transitions[trans_id] = PageTransition(
                id=trans_id,
                trigger=elem_id,
                from_page='settings/home',
                to_page=target_page,
                action='click',
            )

    # Storage sub-page transitions
    if 'settings/storage' in pages:
        transitions['to_internal_storage'] = PageTransition(
            id='to_internal_storage',
            trigger='internal_storage',
            from_page='settings/storage',
            to_page='settings/storage/internal',
            action='click',
        )
        transitions['to_external_storage'] = PageTransition(
            id='to_external_storage',
            trigger='external_storage',
            from_page='settings/storage',
            to_page='settings/storage/external',
            action='click',
        )

    # Add back transitions for navigation
    back_transitions = [
        ('wifi_list', 'settings/wifi', 'settings/home'),
        ('bluetooth_list', 'settings/bluetooth', 'settings/home'),
        ('back', 'settings/display', 'settings/home'),
        ('back', 'settings/storage/internal', 'settings/storage'),
        ('back', 'settings/storage/external', 'settings/storage'),
        ('back', 'settings/storage', 'settings/home'),
    ]

    for i, (trigger, from_page, to_page) in enumerate(back_transitions):
        transitions[f'back_{i}'] = PageTransition(
            id=f'back_{i}',
            trigger=trigger,
            from_page=from_page,
            to_page=to_page,
            action='back',
        )

    return transitions


def create_state_fixture_from_pages_all(json_path: str) -> StateFixture:
    """Create a StateFixture from pages_all.json.

    Args:
        json_path: Path to pages_all.json file

    Returns:
        StateFixture instance with pages and transitions
    """
    pages = load_pages_all(json_path)
    transitions = create_transitions_from_pages(pages)

    return StateFixture(
        pages=pages,
        transitions=list(transitions.values()),
        initial_page_id='settings/home',
        history_depth=10,
    )


def load_plan_all(json_path: str) -> TraversalPlan:
    """Load traversal plan from plan_all.json.

    Args:
        json_path: Path to plan_all.json file

    Returns:
        TraversalPlan instance
    """
    with open(json_path, 'r') as f:
        data = json.load(f)

    return TraversalPlan._from_dict(data)


def run_simulation_with_plan_all(
    pages_json: str,
    plan_json: str,
    trace_dir: str = '.traces',
) -> str:
    """Run simulation using pages_all.json and plan_all.json.

    Args:
        pages_json: Path to pages_all.json
        plan_json: Path to plan_all.json
        trace_dir: Directory for trace files

    Returns:
        Trace ID of the simulation run
    """
    print("="*60)
    print("Plan & Pages All Simulation")
    print("="*60)

    # Load fixtures
    print(f"\n[1/5] Loading pages from: {pages_json}")
    fixture = create_state_fixture_from_pages_all(pages_json)
    print(f"  ✓ Loaded {len(fixture.pages)} pages")
    print(f"  ✓ Initial page: {fixture.initial_page_id}")
    print(f"  ✓ Transitions: {len(fixture.transitions)}")

    # Load plan
    print(f"\n[2/5] Loading plan from: {plan_json}")
    plan = load_plan_all(plan_json)
    print(f"  ✓ Entry app: {plan.entry_app}")
    print(f"  ✓ Mode: {plan.mode}")
    print(f"  ✓ Static nodes: {len(plan.static_nodes)}")

    # Create stateful services
    print(f"\n[3/5] Creating stateful mock services")
    vision = StatefulMockVisionService(fixture)
    action = StatefulMockActionExecutor(vision)
    print(f"  ✓ Vision service initialized")
    print(f"  ✓ Action executor initialized")

    # Set up trace recording
    print(f"\n[4/5] Setting up trace recording")
    storage = FileStorage(base_dir=trace_dir)
    recorder = TraceRecorder(storage=storage)
    print(f"  ✓ Trace storage: {trace_dir}")
    print(f"  ✓ Engine will record page snapshots and actions")

    # Create engine
    print(f"\n[5/5] Creating traversal engine")
    engine = GraphTraversalEngine(
        plan=plan,
        vision_service=vision,
        action_executor=action,
        trace_recorder=recorder,
    )
    print(f"  ✓ Engine initialized")

    # Run simulation
    print(f"\n{'='*60}")
    print("Running Simulation...")
    print(f"{'='*60}")

    result = engine.run()

    # Get trace data
    trace_nodes = storage.read(result.trace_id)

    print(f"\n{'='*60}")
    print("Simulation Complete")
    print(f"{'='*60}")
    print(f"  Trace ID: {result.trace_id}")
    print(f"  Status: {result.status}")
    print(f"  Nodes recorded: {len(trace_nodes)}")
    print(f"  Storage: {trace_dir}")
    print(f"\nDashboard: http://localhost:8080")
    print(f"{'='*60}")

    return result.trace_id


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Run simulation with pages_all.json and plan_all.json"
    )
    parser.add_argument(
        '--pages',
        default='tests/assets/fixtures/pages_all.json',
        help='Path to pages_all.json file'
    )
    parser.add_argument(
        '--plan',
        default='tests/assets/fixtures/plan_all.json',
        help='Path to plan_all.json file'
    )
    parser.add_argument(
        '--trace-dir',
        default='.traces',
        help='Directory for trace files'
    )

    args = parser.parse_args()

    # Verify files exist
    pages_path = Path(args.pages)
    plan_path = Path(args.plan)

    if not pages_path.exists():
        print(f"Error: Pages file not found: {args.pages}")
        sys.exit(1)

    if not plan_path.exists():
        print(f"Error: Plan file not found: {args.plan}")
        sys.exit(1)

    try:
        trace_id = run_simulation_with_plan_all(
            pages_json=str(pages_path),
            plan_json=str(plan_path),
            trace_dir=args.trace_dir,
        )
        print(f"\n✅ Success! Trace ID: {trace_id}")
        sys.exit(0)
    except Exception as e:
        print(f"\n❌ Failed: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
