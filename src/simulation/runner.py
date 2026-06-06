"""
Simulation runner for V6.4 offline testing.

Uses real GraphTraversalEngine with mock VisionService and ActionExecutor
implementing real interfaces. Trace data flows through V6.3 TraceRecorder
into MemoryStorage and is extracted via TraceAnalyzer.
"""

import json
import time
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from src.graph.plan import TraversalPlan
from src.graph.node import TraversalNode, CompletionPolicy, CompletionPolicyType
from src.trace.analyzer import TraceAnalyzer
from src.trace.recorder import TraceRecorder
from src.trace.storage import MemoryStorage
from src.traversal.graph_engine import GraphTraversalEngine

from .mock_vision import MockVisionService
from .mock_action import MockActionExecutor


@dataclass
class SimulationResult:
    """Complete result of a simulation run."""

    engine_result: Dict[str, Any]
    trace: List[Dict[str, Any]]
    executed_actions: List[Dict[str, Any]]
    visited_tree: Dict[str, Dict[str, Any]]
    elapsed_seconds: float
    completion_reason: str = ""
    statistics: Dict[str, Any] = field(default_factory=dict)
    trace_id: str = ""


@dataclass
class StructuredResult:
    """Structured result from engine execution."""
    success: bool
    completion_reason: str
    visited_nodes: List[str]
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class ModifiedPlan:
    """Result of plan modification."""
    plan: TraversalPlan
    modification: str
    original_values: Dict[str, Any] = field(default_factory=dict)


class SimulationRunner:
    """Simulation runner using real GraphTraversalEngine with mock services.

    MockVisionService and MockActionExecutor implement real ABCs
    (VisionService, OperationExecutor). Trace data flows through
    V6.3 TraceRecorder → MemoryStorage → TraceAnalyzer.
    """

    def __init__(
        self,
        virtual_pages: Dict[str, Dict[str, Any]],
        plan: TraversalPlan,
        config: Optional[Dict[str, Any]] = None,
    ):
        self.virtual_pages = virtual_pages
        self.plan = plan
        self.config = config or {}

        # Mock services implementing real interfaces
        self.vision = MockVisionService(virtual_pages)
        self.action = MockActionExecutor(
            simulate_delay=self.config.get("action_delay", 0.0)
        )

        # V6.3 Trace system
        self._storage = MemoryStorage()
        self._recorder = TraceRecorder(storage=self._storage)

        # Real engine — no fallback
        self.engine = GraphTraversalEngine(
            plan=plan,
            vision_service=self.vision,
            action_executor=self.action,
            trace_recorder=self._recorder,
        )

        # Timing
        self._start_time: Optional[float] = None
        self._end_time: Optional[float] = None
        self._result: Optional[SimulationResult] = None

    # -- run -----------------------------------------------------------------

    def run(self) -> SimulationResult:
        self._start_time = time.time()
        try:
            engine_result = self.engine.run()
            self._result = self._build_simulation_result(engine_result)
            return self._result
        except Exception as e:
            return self._handle_error(e)
        finally:
            self._end_time = time.time()

    # -- result building -----------------------------------------------------

    def _build_simulation_result(self, engine_result: Any) -> SimulationResult:
        tid = getattr(engine_result, 'trace_id', '')
        nodes = self._storage.read(tid)
        analyzer = TraceAnalyzer(nodes)

        return SimulationResult(
            engine_result={"status": str(getattr(engine_result, 'status', 'completed'))},
            trace=analyzer.extract_action_sequence(),
            executed_actions=analyzer.extract_action_sequence(),
            visited_tree=analyzer.extract_page_tree(),
            elapsed_seconds=time.time() - self._start_time if self._start_time else 0,
            completion_reason=str(getattr(engine_result, 'status', 'completed')),
            statistics={
                "time": analyzer.extract_time_analysis(),
                "errors": analyzer.extract_error_statistics(),
                "coverage": analyzer.extract_coverage_analysis(),
            },
            trace_id=tid,
        )

    def _handle_error(self, error: Exception) -> SimulationResult:
        return SimulationResult(
            engine_result={
                "success": False,
                "error": str(error),
                "error_type": type(error).__name__,
            },
            trace=[],
            executed_actions=[],
            visited_tree={},
            elapsed_seconds=time.time() - self._start_time if self._start_time else 0,
            completion_reason="error",
            statistics={"error": True, "error_message": str(error)},
        )

    # -- result accessors ----------------------------------------------------

    def get_statistics(self) -> Dict[str, Any]:
        if self._result:
            return self._result.statistics
        return {}

    def get_result(self) -> Optional[SimulationResult]:
        return self._result

    def get_trace_id(self) -> str:
        return self._result.trace_id if self._result else ""

    @property
    def storage(self) -> MemoryStorage:
        return self._storage


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
        modified = TraversalPlan.from_json(self.current_plan.to_json())
        removed_count = 0
        for node in [modified.root_node] + list(modified.static_nodes.values()):
            if node and node.children_strategy.dynamic_rules:
                if rule_id in node.children_strategy.dynamic_rules:
                    del node.children_strategy.dynamic_rules[rule_id]
                    removed_count += 1
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
        from src.graph.node import CompletionPolicy, CompletionPolicyType, MatchMode
        modified = TraversalPlan.from_json(self.current_plan.to_json())
        old_policy = modified.completion_policy
        modified.completion_policy = CompletionPolicy(
            type=CompletionPolicyType.TARGET_FOUND,
            target_name=target_name,
            match_mode=MatchMode.CONTAINS if match_mode == "contains" else MatchMode.EXACT,
        )
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
        modified = TraversalPlan.from_json(self.current_plan.to_json())
        if not modified.intent_slots:
            from src.graph.node import IntentSlots
            modified.intent_slots = IntentSlots(depth=max_depth)
        else:
            modified.intent_slots.depth = max_depth
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
        self.modifications.pop()
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
