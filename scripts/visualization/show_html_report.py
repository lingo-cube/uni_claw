#!/usr/bin/env python3
"""
显示HTML报告的关键信息
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from src.simulation.runner import SimulationRunner
from src.graph.plan import TraversalPlan
import json

def main():
    """运行测试并显示报告信息"""
    print("=" * 70)
    print("E2E测试HTML报告生成")
    print("=" * 70)

    # Load test fixtures
    with open('tests/simulation/fixtures/e2e_all_traversal/plan_all.json', 'r', encoding='utf-8') as f:
        plan_data = json.load(f)
    with open('tests/simulation/fixtures/e2e_all_traversal/pages_all.json', 'r', encoding='utf-8') as f:
        virtual_pages = json.load(f)

    # Create and run simulation
    plan = TraversalPlan.from_json(json.dumps(plan_data))
    runner = SimulationRunner(virtual_pages, plan)
    result = runner.run()

    # Generate HTML report
    html_content = runner.export_trace('html')

    # Save to file
    html_path = Path('e2e_test_report.html')
    with open(html_path, 'w', encoding='utf-8') as f:
        f.write(html_content)

    print(f"\n[OK] HTML report generated: {html_path.absolute()}")
    print(f"File size: {html_path.stat().st_size / 1024:.1f} KB")

    # Display statistics
    print(f"\n[Test Statistics]")
    print(f"  - Total steps: {len(result.trace)}")
    print(f"  - Visited nodes: {len(result.visited_tree)}")
    print(f"  - Execution time: {result.elapsed_seconds:.3f} seconds")
    print(f"  - Completion reason: {result.completion_reason}")

    # Display event sequence
    print(f"\n[Event Sequence]")
    from tests.simulation.helpers.assertions import TraceAsserter
    for i, step in enumerate(result.trace[:5]):
        # step is already a dict from to_dict()
        event = TraceAsserter.step_to_nl(step)
        print(f"  {i+1}. {event}")

    if len(result.trace) > 5:
        print(f"  ... and {len(result.trace) - 5} more events")

    # Display visited tree
    print(f"\n[Visited Tree]")
    tree_ascii = runner.render_tree(max_depth=2)
    for line in tree_ascii.split('\n'):
        print(f"  {line}")

    print(f"\n[Report Contents]")
    print(f"  - Execution statistics dashboard")
    print(f"  - State transition tracking table")
    print(f"  - Operation comparison analysis")
    print(f"  - Visited tree visualization")
    print(f"  - Timestamp records")

    print(f"\n[Usage Instructions]")
    print(f"  1. Report opened in browser")
    print(f"  2. Double-click e2e_test_report.html to reopen")
    print(f"  3. Supports all modern browsers (Chrome, Firefox, Edge)")

    print(f"\n[Tips]")
    print(f"  - Check the operation comparison table")
    print(f"  - Review state transition timestamps")
    print(f"  - Verify visited tree completeness")

if __name__ == "__main__":
    main()