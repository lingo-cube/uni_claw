"""
Simulation runner for V6 offline testing.

Provides end-to-end simulation capabilities with mock components.
"""

import json
import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional, Set

from src.graph.plan import TraversalPlan
from src.graph.node import TraversalNode, CompletionPolicy, CompletionPolicyType
from src.state_machine.global_fsm import GlobalState

from .mock_vision import MockVisionService
from .mock_action import MockActionExecutor
from .visualizer import InMemoryTracer, TraceStep


@dataclass
class SimulationResult:
    """Result of a simulation run."""

    engine_result: Dict[str, Any]  # Result from traversal engine
    trace: List[Dict[str, Any]]  # Full trace
    executed_actions: List[Dict[str, Any]]  # Action history
    visited_tree: Dict[str, Dict[str, Any]]  # Visited nodes tree
    elapsed_seconds: float  # Total execution time


@dataclass
class ModifiedPlan:
    """Result of plan modification."""

    plan: TraversalPlan
    modification: str
    original_values: Dict[str, Any] = field(default_factory=dict)


class SimulationRunner:
    """
    Simulation runner for V6 offline testing.

    Executes TraversalPlan using mock components for fast, reliable testing.
    """

    def __init__(
        self,
        virtual_pages: Dict[str, Dict[str, Any]],
        plan: TraversalPlan,
    ):
        """
        Initialize simulation runner.

        Args:
            virtual_pages: Mapping of paths to virtual page analyses
            plan: TraversalPlan to execute
        """
        self.virtual_pages = virtual_pages
        self.plan = plan

        # Mock components
        self.vision = MockVisionService(virtual_pages)
        self.action = MockActionExecutor()
        self.tracer = InMemoryTracer()

        # Timing
        self._start_time: Optional[float] = None
        self._end_time: Optional[float] = None

    def run(self) -> SimulationResult:
        """
        Execute the simulation.

        Returns:
            SimulationResult with execution details
        """
        self._start_time = time.time()

        try:
            # Inject tracer into vision for path tracking
            self.vision.set_context(self.tracer)

            # Start trace
            self.tracer.start_traversal(self.plan)

            # Execute simulation (simplified - would use actual engine)
            self._execute_simulation()

            # Get results
            return SimulationResult(
                engine_result={
                    "status": "completed",
                    "visited_nodes": list(self.tracer.visited_tree.keys()),
                },
                trace=[step.to_dict() for step in self.tracer.steps],
                executed_actions=self.action.get_history(),
                visited_tree={
                    nid: {
                        "name": node.name,
                        "visited": node.visited,
                        "restored": node.restored,
                    }
                    for nid, node in self.tracer.visited_tree.items()
                },
                elapsed_seconds=time.time() - self._start_time,
            )

        finally:
            self._end_time = time.time()

    def _execute_simulation(self) -> None:
        """Execute the simulation (placeholder for actual logic)."""
        # Placeholder: Would use GraphTraversalEngine
        # For now, just add some dummy steps
        self.tracer._step_counter = 0

        # Add initial step
        self.tracer.steps.append(TraceStep(
            step_number=1,
            timestamp=datetime.now(),
            from_state="node_select",
            to_state="precondition_check",
            node_id="root",
        ))

    def render_tree(self) -> str:
        """Render traversal tree."""
        return self.tracer.render_tree()

    def render_mermaid(self) -> str:
        """Render Mermaid diagram."""
        return self.tracer.render_mermaid()

    def export_trace(self, format: str = "jsonl") -> str:
        """Export trace in specified format."""
        return self.tracer.export_trace(format)

    def get_statistics(self) -> Dict[str, Any]:
        """Get simulation statistics."""
        return {
            "total_steps": len(self.tracer.steps),
            "visited_nodes": len(self.tracer.visited_tree),
            "action_count": self.action.get_action_count(),
            "elapsed_time": time.time() - self._start_time if self._start_time else 0,
        }


class PlanDebugger:
    """
    Debugging tool for traversal plans.

    Provides methods to modify and test plans interactively.
    """

    def __init__(self, plan: TraversalPlan):
        """
        Initialize plan debugger.

        Args:
            plan: TraversalPlan to debug
        """
        self.original_plan = plan
        self.current_plan = plan
        self.modifications: List[ModifiedPlan] = []

    def remove_rule(self, rule_id: str) -> TraversalPlan:
        """
        Remove a dynamic rule from the plan.

        Args:
            rule_id: ID of the rule to remove

        Returns:
            Modified plan
        """
        # Create a copy of the plan
        modified = TraversalPlan.from_json(self.current_plan.to_json())

        # Remove rule from all nodes
        removed_count = 0
        for node in [modified.root_node] + list(modified.static_nodes.values()):
            if node and node.children_strategy.dynamic_rules:
                if rule_id in node.children_strategy.dynamic_rules:
                    del node.children_strategy.dynamic_rules[rule_id]
                    removed_count += 1

        # Record modification
        self.modifications.append(ModifiedPlan(
            plan=modified,
            modification=f"Removed rule: {rule_id}",
            original_values={"rule_id": rule_id, "removed_count": removed_count},
        ))

        self.current_plan = modified
        return modified

    def set_target(self, target_name: str, match_mode: str = "contains") -> TraversalPlan:
        """
        Set target completion policy.

        Args:
            target_name: Name of target to find
            match_mode: Match mode ("exact" or "contains")

        Returns:
            Modified plan
        """
        # Create a copy of the plan
        modified = TraversalPlan.from_json(self.current_plan.to_json())

        # Set completion policy
        from src.graph.node import CompletionPolicy, CompletionPolicyType, MatchMode

        old_policy = modified.completion_policy
        modified.completion_policy = CompletionPolicy(
            type=CompletionPolicyType.TARGET_FOUND,
            target_name=target_name,
            match_mode=MatchMode.CONTAINS if match_mode == "contains" else MatchMode.EXACT,
        )

        # Record modification
        self.modifications.append(ModifiedPlan(
            plan=modified,
            modification=f"Set target: {target_name}",
            original_values={"old_policy": old_policy},
        ))

        self.current_plan = modified
        return modified

    def reset_visited(self) -> TraversalPlan:
        """
        Reset visited nodes and node stack.

        Returns:
            Modified plan (or same plan if nothing to reset)
        """
        # This would need to be applied to the context, not the plan
        # Record modification
        self.modifications.append(ModifiedPlan(
            plan=self.current_plan,
            modification="Reset visited nodes",
            original_values={},
        ))

        return self.current_plan

    def set_max_depth(self, max_depth: int) -> TraversalPlan:
        """
        Set maximum traversal depth.

        Args:
            max_depth: Maximum depth to traverse

        Returns:
            Modified plan
        """
        # Create a copy of the plan
        modified = TraversalPlan.from_json(self.current_plan.to_json())

        # Update intent slots depth
        if not modified.intent_slots:
            from src.graph.node import IntentSlots
            modified.intent_slots = IntentSlots(depth=max_depth)
        else:
            modified.intent_slots.depth = max_depth

        # Record modification
        self.modifications.append(ModifiedPlan(
            plan=modified,
            modification=f"Set max depth: {max_depth}",
            original_values={},
        ))

        self.current_plan = modified
        return modified

    def undo_last(self) -> Optional[TraversalPlan]:
        """
        Undo the last modification.

        Returns:
            Previous plan, or None if no modifications
        """
        if not self.modifications:
            return None

        # Remove last modification
        self.modifications.pop()

        # Restore previous plan
        if self.modifications:
            self.current_plan = self.modifications[-1].plan
        else:
            self.current_plan = self.original_plan

        return self.current_plan

    def reset_to_original(self) -> TraversalPlan:
        """
        Reset to the original plan.

        Returns:
            Original plan
        """
        self.modifications = []
        self.current_plan = self.original_plan
        return self.original_plan

    def get_modification_history(self) -> List[Dict[str, Any]]:
        """Get history of all modifications."""
        return [
            {
                "modification": m.modification,
                "original_values": m.original_values,
            }
            for m in self.modifications
        ]
