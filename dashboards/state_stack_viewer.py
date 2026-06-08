"""
State Stack Visualization Tool.

Provides real-time viewing of state machine stack and recent state transitions.
"""

from typing import Optional

from src.traversal.graph_engine import GraphTraversalEngine


class StateStackViewer:
    """State stack visualization tool."""

    def show_stack(self, engine: GraphTraversalEngine) -> None:
        """
        Display current stack state.

        Args:
            engine: Graph traversal engine instance

        Display content:
            - Stack depth
            - Current state
            - Current path
            - Each stack layer node and its visited children
        """
        stack = engine.context.node_stack
        print(f"\n{'='*60}")
        print(f"State Stack (depth: {len(stack)})")
        print(f"Current State: {engine.state_machine.state}")
        print(f"Current Path: {engine.context.current_path}")
        print(f"{'='*60}")

        # Display stack from top (most recent) to bottom
        for i, stack_frame in enumerate(reversed(stack)):
            indent = "  " * i
            marker = "→ " if i == 0 else "  "
            node_id = stack_frame.node_id

            # Try to get node name from registry
            node_name = node_id
            if node_id in engine._node_registry:
                node_name = engine._node_registry[node_id].name

            print(f"{indent}{marker}{node_id} ({node_name})")

            # Show visited children for this node
            visited = engine.context.visited_children.get(node_id, set())
            if visited:
                print(f"{indent}   Visited: {sorted(visited)}")

    def show_transitions(
        self,
        engine: GraphTraversalEngine,
        last_n: int = 10
    ) -> None:
        """
        Display recent state transitions.

        Args:
            engine: Graph traversal engine instance
            last_n: Number of recent transitions to display, default 10
        """
        history = engine.state_machine.get_transition_history()
        recent = history[-last_n:] if len(history) > last_n else history

        print(f"\nRecent Transitions (last {len(recent)}):")
        for trans in recent:
            print(
                f"  {trans.from_state} → {trans.to_state} | "
                f"node: {trans.node_id}"
            )
