"""Single-step execution pipeline — state machine call + engine-level gates."""

import time
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from src.graph.node import ChildrenStrategyType, TraversalNode
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.trace.context import TraversalRuntimeContext


@dataclass
class StepContext:
    """Bundles all dependencies needed for a single state machine step."""

    context: TraversalRuntimeContext
    state_machine: TraversalStateMachine
    vision: Any
    action: Any
    child_mgr: Any
    node_registry: Dict[str, TraversalNode]
    trace: Any
    # Mutable tracking fields
    last_known_path: List[str]
    last_recorded_path: List[str]
    last_recorded_action: Optional[str] = None

    # Internal — set by execute_step
    _stack: Any = field(default=None, repr=False)


class StepOrchestrator:
    """Executes one state machine step with engine-level gates.

    Responsibilities:
    - Call state_machine.step()
    - FRAME_COMPLETE interception for DYNAMIC_MATCH
    - BRANCH child push
    - NODE_SELECT child push
    - Path change detection & cache invalidation
    - Trace recording at boundaries
    """

    def execute_step(self, ctx: StepContext) -> Dict[str, Any]:
        # Create stack adapter
        from src.traversal.graph_engine import _NodeStackAdapter

        stack = _NodeStackAdapter(ctx.context, ctx.node_registry)
        ctx._stack = stack

        current_node = stack.peek()
        current_node_id = current_node.node_id if current_node else None

        # Record step start
        if current_node_id:
            ctx.trace.record_step_start(current_node_id, ctx.context.current_path)

        # Call state machine
        t0 = time.time()
        transition = ctx.state_machine.step(
            stack=stack,
            context=ctx.context,
            vision=ctx.vision,
            action=ctx.action,
        )
        step_duration_ms = (time.time() - t0) * 1000

        # Record page snapshot when path changes
        if hasattr(ctx.context, 'current_page_analysis') and ctx.context.current_page_analysis:
            current_path = list(ctx.context.current_path) if ctx.context.current_path else []
            if current_path != ctx.last_recorded_path:
                ctx.trace.record_page_analysis(ctx.context.current_page_analysis)
                ctx.last_recorded_path = current_path

        # Record action execution from metrics
        execution_metrics = None
        metrics = getattr(ctx.state_machine, '_last_handler_metrics', None)
        if metrics and "execution" in metrics:
            execution_metrics = metrics["execution"]
            if isinstance(execution_metrics, list):
                if execution_metrics:
                    execution_metrics = execution_metrics[-1]
                else:
                    execution_metrics = None
            if execution_metrics:
                action = execution_metrics.get("action")
                if action and action != ctx.last_recorded_action:
                    ctx.trace.record_action_execution(
                        action=action,
                        target=execution_metrics.get("target"),
                        success=execution_metrics.get("status", "success") == "success",
                    )
                    ctx.last_recorded_action = action

        # Record metrics as spans
        ctx.trace.record_metrics_as_spans(metrics)

        # Record state transition
        from_state = transition.from_state.value if hasattr(transition.from_state, 'value') else str(transition.from_state)
        to_state = transition.to_state.value if hasattr(transition.to_state, 'value') else str(transition.to_state)
        ctx.trace.record_state_transition(from_state, to_state)

        # Child push / frame complete logic
        child_pushed = None
        should_complete_frame = False

        # BRANCH state — push children
        if transition.to_state == TraversalState.BRANCH:
            # V6: Allow BRANCH handling from NODE_SELECT (returning from child)
            # to properly check for remaining children and complete frame if needed
            if transition.from_state in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY, TraversalState.PRECONDITION_CHECK, TraversalState.NODE_SELECT):
                current = stack.peek()
                if current and not current.is_leaf():
                    child_id = ctx.child_mgr.get_next_unvisited_child(current, ctx.context)
                    if child_id:
                        self._push(stack, ctx, child_id)
                        child_pushed = child_id
                    else:
                        should_complete_frame = True
                        if current:
                            ctx.trace.record_decision("branch_complete_frame", {
                                "reason": "no_more_children",
                                "node": current.node_id,
                                "node_name": current.name,
                                "visited_count": len(ctx.context.visited_children.get(current.node_id, [])),
                            })

        # NODE_SELECT — push children for DYNAMIC_MATCH
        if transition.to_state == TraversalState.NODE_SELECT:
            from datetime import datetime
            from src.simulation.operation_executor import ExecutionContext

            current = stack.peek()
            if current and current.children_strategy:
                if current.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
                    child_id = ctx.child_mgr.get_next_unvisited_child(current, ctx.context)
                    if child_id:
                        self._push(stack, ctx, child_id)
                        child_pushed = child_id
                        ctx.trace.record_decision("dynamic_child_pushed", {
                            "reason": "unvisited_child_found",
                            "parent_node": current.node_id,
                            "parent_name": current.name,
                            "child_id": child_id,
                            "from_state": "NODE_SELECT",
                        })
                    else:
                        # No more children - directly execute back and pop stack
                        # This prevents the BRANCH -> NODE_SELECT loop
                        all_metrics = {"execution": [], "auto_escape": None}
                        t0 = time.time()
                        try:
                            # Execute back action
                            exec_ctx = ExecutionContext(
                                node_id=current.node_id if current else "unknown",
                                node_name="node_select_back",
                                operation={"action": "back"},
                                timestamp=datetime.now(),
                            )
                            result = ctx.action.execute(exec_ctx)
                            elapsed = (time.time() - t0) * 1000

                            all_metrics["execution"].append({
                                "action": "back",
                                "status": "success" if result.success else "failed",
                                "duration_ms": elapsed,
                            })

                            # Pop the current node from stack
                            if not stack.is_empty:
                                stack.pop()

                            # Update metrics
                            ctx.state_machine._last_handler_metrics = {
                                "execution": all_metrics["execution"][-1] if all_metrics["execution"] else None,
                            }

                            # Record decision
                            ctx.trace.record_decision("node_select_back_pop", {
                                "reason": "no_more_children",
                                "node": current.node_id,
                                "node_name": current.name,
                            })

                            # Continue to NODE_SELECT (for parent)
                            # Return immediately to skip rest of processing
                            return {
                                "from_state": transition.from_state,
                                "to_state": TraversalState.NODE_SELECT,
                                "next_state": TraversalState.NODE_SELECT,
                                "node_id": None,  # No specific node, will select from stack
                                "action": "back_pop",
                                "metrics": all_metrics,
                            }
                        except Exception as e:
                            elapsed = (time.time() - t0) * 1000
                            all_metrics["execution"].append({
                                "action": "back",
                                "status": "failed",
                                "duration_ms": elapsed,
                                "error": str(e),
                            })
                            # On error, fall through to normal processing

        # FRAME_COMPLETE interception — check for remaining dynamic children
        if transition.to_state == TraversalState.FRAME_COMPLETE:
            current = stack.peek()
            if current and current.children_strategy:
                if current.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
                    remaining_child_id = ctx.child_mgr.get_next_unvisited_child(current, ctx.context)
                    if remaining_child_id:
                        self._push(stack, ctx, remaining_child_id)
                        child_pushed = remaining_child_id
                        ctx.trace.record_decision("frame_complete_override", {
                            "reason": "remaining_dynamic_child",
                            "parent_node": current.node_id,
                            "parent_name": current.name,
                            "child_id": remaining_child_id,
                            "from_state": "FRAME_COMPLETE",
                        })

        # Determine next state
        next_state = transition.to_state
        if should_complete_frame and transition.to_state != TraversalState.BRANCH:
            # Force transition to BRANCH (which can then go to FRAME_COMPLETE)
            next_state = TraversalState.BRANCH
            ctx.state_machine.transition_to(TraversalState.BRANCH, action="no_more_children")

        if child_pushed:
            # Override to NODE_SELECT and update state machine
            if ctx.state_machine._state != TraversalState.NODE_SELECT:
                ctx.state_machine.transition_to(TraversalState.NODE_SELECT, node_id=child_pushed, action="push_child")
            else:
                ctx.state_machine.set_current_node(child_pushed)
            return {
                "from_state": transition.from_state,
                "to_state": TraversalState.NODE_SELECT,
                "next_state": next_state,
                "node_id": transition.node_id,
                "child_pushed": child_pushed,
            }

        # Update visited nodes
        if transition.to_state in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY):
            if transition.node_id:
                ctx.context.visited_nodes.add(transition.node_id)
            current = stack.peek()
            if current:
                ctx.context.visited_nodes.add(current.node_id)

        # Path change detection & cache invalidation
        path_now = list(ctx.context.current_path)
        if path_now != ctx.last_known_path:
            ctx.trace.record_page_transition(ctx.last_known_path, path_now, transition)

            current = stack.peek()
            if current:
                ctx.child_mgr.invalidate(current.node_id)
            ctx.last_known_path = path_now

        # Record step end
        if current_node_id:
            ctx.trace.record_step_end(
                step_span_id=current_node_id,
                result={"next_state": str(next_state), "duration_ms": step_duration_ms},
            )

        return {
            "from_state": from_state,
            "to_state": next_state.value if hasattr(next_state, 'value') else next_state,
            "node_id": child_pushed or transition.node_id,
            "timestamp": transition.timestamp.isoformat(),
            "metadata": transition.metadata,
        }

    @staticmethod
    def _push(stack: Any, ctx: StepContext, node_id: str) -> None:
        from src.trace.context import StackFrame
        ctx.context.node_stack.append(StackFrame(node_id=node_id, span_id=node_id))
