#!/usr/bin/env python3
"""
Simple test to debug SimulationRunner DFS traversal.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from src.simulation.runner import SimulationRunner
from src.graph.plan import TraversalPlan

def test_simulation_runner():
    """Test SimulationRunner with current implementation."""
    print("=" * 70)
    print("Testing SimulationRunner DFS Implementation")
    print("=" * 70)

    # Load test data
    test_dir = Path("tests/simulation/fixtures/e2e_all_traversal")
    pages_path = test_dir / "pages_all.json"
    plan_path = test_dir / "plan_all.json"

    with open(pages_path, 'r', encoding='utf-8') as f:
        virtual_pages = json.load(f)

    with open(plan_path, 'r', encoding='utf-8') as f:
        plan_data = json.load(f)

    print(f"\n[LOAD] Loaded virtual pages: {len(virtual_pages)}")
    print(f"[LOAD] Plan entry_app: {plan_data.get('entry_app', 'N/A')}")

    try:
        # Create TraversalPlan
        plan_json_str = json.dumps(plan_data)
        plan = TraversalPlan.from_json(plan_json_str)

        print(f"[PLAN] TraversalPlan created successfully")
        print(f"[PLAN] Entry app: {plan.entry_app}")
        print(f"[PLAN] Intent slots: {plan.intent_slots}")

        # Create SimulationRunner
        runner = SimulationRunner(
            virtual_pages=virtual_pages,
            plan=plan,
            config={"action_delay": 0.0}
        )

        print(f"[RUNNER] SimulationRunner created successfully")

        # Run simulation
        print(f"\n[RUN] Starting simulation...")
        result = runner.run()

        print(f"\n[RESULT] Simulation completed!")
        print(f"[RESULT] Completion reason: {result.completion_reason}")
        print(f"[RESULT] Total steps in trace: {len(result.trace)}")
        print(f"[RESULT] Visited nodes: {len(result.visited_tree)}")

        # Show trace summary
        print(f"\n[TRACE] First 10 steps:")
        for i, step in enumerate(result.trace[:10], 1):
            step_type = step.get('type', 'unknown')
            node = step.get('node_id', 'N/A')
            action = step.get('action', 'N/A')
            print(f"  {i}. [{step_type}] Node: {node}, Action: {action}")

        if len(result.trace) > 10:
            print(f"  ... and {len(result.trace) - 10} more steps")

        return result

    except Exception as e:
        print(f"\n[ERROR] Test failed: {e}")
        import traceback
        traceback.print_exc()
        return None

if __name__ == "__main__":
    result = test_simulation_runner()

    if result and len(result.trace) >= 8:
        print(f"\n[SUCCESS] Simulation executed {len(result.trace)} steps!")
        sys.exit(0)
    else:
        print(f"\n[WARNING] Simulation incomplete: {len(result.trace) if result else 0} steps")
        sys.exit(1)