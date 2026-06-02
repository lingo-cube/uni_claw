"""
Tree-based Trace Visualization System

Provides hierarchical tree visualization for trace data,
optimized for state machine integration and workflow usage.
"""

import json
import logging
from pathlib import Path
from typing import Dict, List, Any, Optional
from dataclasses import dataclass
from collections import defaultdict
import time
from datetime import datetime

logger = logging.getLogger(__name__)


class TraceTreeVisualizer:
    """Tree-based trace visualization for state machines"""

    def __init__(self):
        self.assets = []
        self.tree_structure = defaultdict(list)
        self.span_hierarchy = {}  # span_id -> children mapping

    def build_tree_from_assets(self, assets: List[Dict]) -> Dict[str, Any]:
        """Build hierarchical tree structure from flat assets"""

        # Reset structure
        self.span_hierarchy = {}
        root_spans = []

        # Build parent-child relationships
        for asset in assets:
            span_id = asset['trace_context']['span_id']
            parent_span_id = asset['trace_context'].get('parent_span_id')

            # Create node
            node = {
                'span_id': span_id,
                'capability': asset['capability'],
                'provider_id': asset['provider_id'],
                'mode': asset['mode'],
                'latency_ms': asset['latency_ms'],
                'tokens': asset['total_tokens'],
                'timestamp': asset['created_at'],
                'depth': 0,
                'children': []
            }

            # Add to hierarchy
            if parent_span_id and parent_span_id in self.span_hierarchy:
                self.span_hierarchy[parent_span_id]['children'].append(node)
                # Calculate depth
                parent_depth = self.span_hierarchy[parent_span_id]['depth']
                node['depth'] = parent_depth + 1
            else:
                root_spans.append(node)

            self.span_hierarchy[span_id] = node

        # Build tree structure
        return {
            'tree': root_spans,
            'total_spans': len(assets),
            'max_depth': self._calculate_max_depth(root_spans),
            'total_latency': sum(asset['latency_ms'] for asset in assets),
            'total_tokens': sum(asset['total_tokens'] for asset in assets)
        }

    def _calculate_max_depth(self, nodes: List[Dict], current_depth: int = 0) -> int:
        """Calculate maximum depth of tree"""
        if not nodes:
            return current_depth

        max_child_depth = current_depth
        for node in nodes:
            if node['children']:
                child_depth = self._calculate_max_depth(node['children'], current_depth + 1)
                max_child_depth = max(max_child_depth, child_depth)

        return max_child_depth

    def print_tree(self, tree_data: Dict[str, Any], detailed: bool = True):
        """Print tree structure with ASCII art"""

        print("\n" + "=" * 80)
        print("[TRACE TREE VISUALIZATION]")
        print("=" * 80)

        print(f"\n[SUMMARY]")
        print(f"  Total Spans: {tree_data['total_spans']}")
        print(f"  Max Depth: {tree_data['max_depth']}")
        print(f"  Total Latency: {tree_data['total_latency']:.1f}ms")
        print(f"  Total Tokens: {tree_data['total_tokens']}")

        print(f"\n[TREE STRUCTURE]")
        self._print_nodes(tree_data['tree'], "", detailed)

        print("=" * 80)

    def _print_nodes(self, nodes: List[Dict], prefix: str, detailed: bool):
        """Recursively print tree nodes"""

        for i, node in enumerate(nodes):
            is_last = (i == len(nodes) - 1)
            current_prefix = "└──" if is_last else "├──"
            child_prefix = "    " if is_last else "│   "

            # Print node info
            node_info = f"[{node['capability']}] via {node['provider_id']} ({node['mode']})"
            perf_info = f"{node['latency_ms']:.0f}ms, {node['tokens']} tokens"

            print(f"{prefix}{current_prefix} {node_info}")
            print(f"{prefix}{child_prefix}    Performance: {perf_info}")
            print(f"{prefix}{child_prefix}    Span ID: {node['span_id'][:20]}...")

            if detailed:
                print(f"{prefix}{child_prefix}    Depth: {node['depth']}, Timestamp: {node['timestamp'][:19]}")

            # Recursively print children
            if node['children']:
                new_prefix = prefix + child_prefix
                self._print_nodes(node['children'], new_prefix, detailed)

    def print_state_machine_view(self, assets: List[Dict]):
        """Print state machine friendly view"""

        print("\n" + "=" * 80)
        print("[STATE MACHINE TRACE VIEW]")
        print("=" * 80)

        # Group by scenario (simulating state transitions)
        scenarios = defaultdict(list)
        for asset in assets:
            scenarios[asset['scenario']].append(asset)

        print(f"\n[STATE TRANSITIONS]")
        for scenario_name, scenario_assets in scenarios.items():
            print(f"\n  Scenario: {scenario_name}")
            print(f"  States: {len(scenario_assets)}")

            for i, asset in enumerate(scenario_assets):
                state_info = f"    [{i+1}] {asset['capability']}"
                transition_info = f"via {asset['provider_id']} -> {asset['output_data'].get('page_type', 'unknown')}"

                print(f"      {state_info}")
                print(f"          {transition_info}")

                if asset.get('custom_context'):
                    print(f"          Context: {asset['custom_context']}")

        print("=" * 80)

    def export_mermaid_diagram(self, assets: List[Dict], output_file: Optional[Path] = None):
        """Export trace as Mermaid diagram"""

        mermaid_lines = [
            "graph TD",
            "    %% Trace Tree Visualization",
            "    %% Generated for State Machine Integration",
            ""
        ]

        # Build node definitions
        node_id = 0
        for i, asset in enumerate(assets):
            span_short = asset['trace_context']['span_id'][:15]
            node_label = f"{asset['capability']}\\nvia {asset['provider_id']}"

            mermaid_lines.append(f"    node{node_id}[\"{node_label}\"]")
            node_id += 1

            # Add connection to previous if not first
            if i > 0:
                mermaid_lines.append(f"    node{i-1} -->|{asset['latency_ms']:.0f}ms| node{i}")

        mermaid_diagram = "\n".join(mermaid_lines)

        if output_file:
            output_file.parent.mkdir(parents=True, exist_ok=True)
            with open(output_file, 'w') as f:
                f.write(mermaid_diagram)
            print(f"[EXPORTED] Mermaid diagram to: {output_file}")
        else:
            print("\n[MERMAID DIAGRAM]")
            print(mermaid_diagram)

        return mermaid_diagram


class StateMachineTraceFormatter:
    """Format trace data for state machine consumption"""

    def __init__(self):
        self.trace_events = []
        self.state_transitions = []

    def format_assets_for_state_machine(self, assets: List[Dict]) -> Dict[str, Any]:
        """Format assets for state machine integration"""

        state_machine_data = {
            'trace_log': [],
            'state_transitions': [],
            'performance_metrics': {},
            'error_events': []
        }

        for asset in assets:
            # Create trace event
            trace_event = {
                'event_type': 'state_transition',
                'event_id': asset['trace_context']['span_id'],
                'from_state': asset.get('custom_context', {}).get('previous_state', 'initial'),
                'to_state': asset['output_data'].get('page_type', 'unknown'),
                'capability': asset['capability'],
                'timestamp': asset['created_at'],
                'performance': {
                    'latency_ms': asset['latency_ms'],
                    'tokens': asset['total_tokens']
                },
                'context': asset.get('custom_context', {}),
                'success': 'error' not in str(asset['output_data']).lower()
            }

            state_machine_data['trace_log'].append(trace_event)

            # Track state transitions
            if trace_event['success']:
                state_machine_data['state_transitions'].append({
                    'from': trace_event['from_state'],
                    'to': trace_event['to_state'],
                    'trigger': asset['capability']
                })

            # Track errors
            if not trace_event['success']:
                state_machine_data['error_events'].append({
                    'error_type': asset['output_data'].get('error', 'unknown'),
                    'state': trace_event['to_state'],
                    'capability': asset['capability']
                })

        # Calculate performance metrics
        if state_machine_data['trace_log']:
            total_latency = sum(event['performance']['latency_ms'] for event in state_machine_data['trace_log'])
            total_tokens = sum(event['performance']['tokens'] for event in state_machine_data['trace_log'])

            state_machine_data['performance_metrics'] = {
                'total_transitions': len(state_machine_data['state_transitions']),
                'total_latency_ms': total_latency,
                'total_tokens': total_tokens,
                'avg_latency_ms': total_latency / len(state_machine_data['trace_log']) if state_machine_data['trace_log'] else 0,
                'error_rate': len(state_machine_data['error_events']) / len(state_machine_data['trace_log']) if state_machine_data['trace_log'] else 0
            }

        return state_machine_data

    def print_state_machine_report(self, sm_data: Dict[str, Any]):
        """Print state machine friendly report"""

        print("\n" + "=" * 80)
        print("[STATE MACHINE INTEGRATION REPORT]")
        print("=" * 80)

        print(f"\n[TRANSITION LOG]")
        for i, event in enumerate(sm_data['trace_log']):
            status = "[SUCCESS]" if event['success'] else "[ERROR]"
            print(f"  {i+1}. {status} {event['from_state']} -> {event['to_state']}")
            print(f"      Trigger: {event['capability']}")
            print(f"      Performance: {event['performance']['latency_ms']:.0f}ms, {event['performance']['tokens']} tokens")

        print(f"\n[STATE TRANSITIONS]")
        for transition in sm_data['state_transitions']:
            from_state = transition.get('from', 'initial')
            to_state = transition.get('to', 'unknown')
            trigger = transition.get('trigger', 'unknown')
            print(f"  {from_state} -> {to_state} (via {trigger})")

        if sm_data['error_events']:
            print(f"\n[ERROR EVENTS]")
            for error in sm_data['error_events']:
                print(f"  State: {error['state']}, Error: {error['error_type']}, Capability: {error['capability']}")

        print(f"\n[PERFORMANCE SUMMARY]")
        metrics = sm_data['performance_metrics']
        print(f"  Total Transitions: {metrics['total_transitions']}")
        print(f"  Total Latency: {metrics['total_latency_ms']:.1f}ms")
        print(f"  Average Latency: {metrics['avg_latency_ms']:.1f}ms")
        print(f"  Total Tokens: {metrics['total_tokens']}")
        print(f"  Error Rate: {metrics['error_rate']:.2%}")

        print("=" * 80)


async def demonstrate_tree_visualization():
    """Demonstrate tree visualization with collected assets"""

    print("[DEMO] Tree-based Trace Visualization")

    # Load collected assets
    assets_dir = Path("tests/ai/assets/traces")
    if not assets_dir.exists():
        print("[ERROR] No collected assets found. Run collection first.")
        return

    all_assets = []
    for json_file in assets_dir.glob("*.json"):
        if json_file.name != "overview.json":
            with open(json_file, 'r', encoding='utf-8') as f:
                assets = json.load(f)
                all_assets.extend(assets)

    print(f"[LOADED] {len(all_assets)} assets from {len(list(assets_dir.glob('*.json')))-1} files")

    # Create visualizer
    visualizer = TraceTreeVisualizer()

    # Build and print tree
    tree_data = visualizer.build_tree_from_assets(all_assets)
    visualizer.print_tree(tree_data, detailed=True)

    # Print state machine view
    visualizer.print_state_machine_view(all_assets)

    # Format for state machine
    formatter = StateMachineTraceFormatter()
    sm_data = formatter.format_assets_for_state_machine(all_assets)
    formatter.print_state_machine_report(sm_data)

    # Export Mermaid diagram
    mermaid_file = Path("tests/ai/assets/trace_diagram.mmd")
    visualizer.export_mermaid_diagram(all_assets, mermaid_file)

    print(f"\n[SUCCESS] Tree visualization complete!")
    print(f"[ASSETS] Generated trace tree, state machine view, and Mermaid diagram")

    return visualizer, formatter


if __name__ == "__main__":
    import asyncio
    asyncio.run(demonstrate_tree_visualization())