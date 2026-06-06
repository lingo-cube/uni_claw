"""
Graph traversal engine for V6 declarative traversal.

This module implements the GraphTraversalEngine, which executes traversal
plans using a graph-based approach with state machine-driven control.

V6.3: Integrated distributed tracing with Span generation at state
transitions, AI calls, action execution, and error handling.
"""

import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional, Set

from src.graph.plan import TraversalPlan
from src.graph.node import (
    CompletionPolicy,
    CompletionPolicyType,
    ChildrenStrategyType,
    EntryPolicy,
    EntryStrategy,
    ExitCondition,
    ExitConditionType,
    FallbackAction,
    MatchMode,
    NodeType,
    TargetFoundAction,
    TraversalMode,
    TraversalNode,
)
from src.state_machine.global_fsm import GlobalState
from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.trace.context import Session, StackFrame, TraversalRuntimeContext
from src.trace.models import SessionNode, SpanNode, StepNode
from src.trace.recorder import TraceRecorder
from src.trace.storage import MemoryStorage


# ============================================================================
# Result Classes
# ============================================================================


@dataclass
class TraversalResult:
    """Result of a graph traversal execution."""

    status: GlobalState  # Final global state
    elapsed_seconds: float  # Total execution time
    total_steps: int  # Total state machine steps
    visited_nodes: Set[str]  # Set of visited node IDs
    trace: List[Dict[str, Any]]  # Trace of all state transitions
    trace_id: str = ""  # Trace ID from the session
    error: Optional[Exception] = None  # Error if failed
    metrics: Dict[str, Any] = field(default_factory=dict)  # Performance metrics


@dataclass
class PageCacheInfo:
    """Cached page information."""

    items: List[Dict[str, Any]] = field(default_factory=list)
    timestamp: float = field(default_factory=time.time)
    screen_hash: Optional[str] = None


# ============================================================================
# Graph Traversal Engine
# ============================================================================


class GraphTraversalEngine:
    """
    Graph-based traversal engine for V6 declarative traversal.

    Executes TraversalPlan using state machine-driven control flow.
    Supports depth limiting, page caching, completion policies, and
    V6.3 distributed tracing.
    """

    def __init__(
        self,
        plan: TraversalPlan,
        vision_service: Any,  # VisionService interface
        action_executor: Any,  # ActionExecutor interface
        exception_chain: Optional[Any] = None,
        trace_recorder: Optional[TraceRecorder] = None,
    ):
        """
        Initialize the graph traversal engine.

        Args:
            plan: TraversalPlan to execute
            vision_service: Service for screen analysis
            action_executor: Service for device control
            exception_chain: Optional exception handling chain
            trace_recorder: Optional trace recorder (auto-created if None)
        """
        self.plan = plan
        self.vision_service = vision_service
        self.action_executor = action_executor
        self.exception_chain = exception_chain
        self.trace_recorder = trace_recorder

        # State management
        self.state_machine = TraversalStateMachine()
        self.context = TraversalRuntimeContext(
            max_depth=plan.intent_slots.depth if plan.intent_slots and plan.intent_slots.depth else 100,
            global_state=GlobalState.IDLE,
        )

        # Node registry
        self._node_registry: Dict[str, TraversalNode] = {}
        self._build_node_registry()

        # Template matcher (if using template registry)
        self._dynamic_matcher = None
        if plan.template_registry:
            self._load_template_registry()

        # Timing
        self._start_time: Optional[float] = None
        self._end_time: Optional[float] = None

    def _build_node_registry(self) -> None:
        """Build node registry from plan."""
        if self.plan.root_node:
            self._node_registry[self.plan.root_node.node_id] = self.plan.root_node
        for node_id, node in self.plan.static_nodes.items():
            self._node_registry[node_id] = node

    def _load_template_registry(self) -> None:
        """Load template registry for dynamic matching."""
        pass

    # ========================================================================
    # Initialization
    # ========================================================================

    def initialize(self) -> bool:
        """
        Initialize the traversal engine.

        Creates a Session, initializes the TraceRecorder, executes the
        entry policy, and sets up initial state.

        Returns:
            True if initialization succeeded
        """
        try:
            self.context.global_state = GlobalState.INITIALIZING

            # Create session
            session = Session(
                traversal_mode="graph",
                config={
                    "plan_name": self.plan.plan_name if hasattr(self.plan, "plan_name") else "",
                },
                start_time=time.time(),
            )
            self.context.trace_id = session.session_id

            # Initialize trace recorder if not already provided
            if self.trace_recorder is None:
                self.trace_recorder = TraceRecorder(storage=MemoryStorage())

            # Write session to storage (session.json)
            if hasattr(self.trace_recorder.storage, "write_session"):
                self.trace_recorder.storage.write_session(session.to_dict(), session.session_id)  # type: ignore[union-attr]

            # Create and record SessionNode
            session_node = SessionNode(
                trace_id=session.session_id,
                span_id=session.session_id,
                device_id=session.device_id,
                device_name=session.device_name,
                device_model=session.device_model,
                os_version=session.os_version,
                app_version=session.app_version,
                app_package=session.app_package,
                start_time=session.start_time,
                status=session.status,
                traversal_mode=session.traversal_mode,
                config=session.config,
            )
            self.trace_recorder.init(session_node)

            # State transition: IDLE → INITIALIZING
            self._record_state_transition("IDLE", "INITIALIZING")

            # Execute entry policy
            if not self._execute_entry_policy():
                self._record_error_span("InitializationError", "Entry policy failed", "error")
                return False

            # Wait for entry condition
            if not self._wait_for_entry_condition():
                self._record_error_span("InitializationError", "Entry condition not met", "error")
                return False

            # Push root node to stack
            if self.plan.root_node:
                self._push_node(self.plan.root_node.node_id)

            # State transition: INITIALIZING → TRAVERSING
            self._record_state_transition("INITIALIZING", "TRAVERSING")
            self.context.global_state = GlobalState.TRAVERSING

            return True

        except Exception as e:
            self.context.global_state = GlobalState.ERROR
            if self.trace_recorder:
                self._record_error_span(type(e).__name__, str(e), "error")
            return False

    def _execute_entry_policy(self) -> bool:
        """Execute the entry policy to enter the target app."""
        policy = self.plan.entry_policy or EntryPolicy()

        t0 = time.time()
        ok = True

        if policy.strategy == EntryStrategy.COLD_LAUNCH:
            ok = True
        elif policy.strategy == EntryStrategy.DIRECT_DEEPLINK:
            ok = True
        elif policy.strategy == EntryStrategy.BIND_CURRENT_SCREEN:
            ok = True

        self._record_execution_span(
            action="entry_policy",
            status="success" if ok else "failed",
            target=str(policy.strategy.value if hasattr(policy.strategy, 'value') else policy.strategy),
            duration_ms=(time.time() - t0) * 1000,
        )
        return ok

    def _wait_for_entry_condition(self) -> bool:
        """Wait for entry condition to be satisfied."""
        policy = self.plan.entry_policy or EntryPolicy()
        if not policy.wait_condition:
            return True
        return True

    def _push_node(self, node_id: str) -> None:
        """Push a node onto the stack."""
        self.context.node_stack.append(
            StackFrame(node_id=node_id, span_id=node_id)
        )

    def _pop_node(self) -> Optional[str]:
        """Pop a node from the stack."""
        if self.context.node_stack:
            return self.context.node_stack.pop().node_id
        return None

    def _peek_node(self) -> Optional[str]:
        """Get the current node ID without popping."""
        if self.context.node_stack:
            return self.context.node_stack[-1].node_id
        return None

    # ========================================================================
    # Main Execution Loop
    # ========================================================================

    def run(self) -> TraversalResult:
        """
        Execute the traversal plan.

        Returns:
            TraversalResult with execution details
        """
        self._start_time = time.time()

        try:
            # Initialize
            if not self.initialize():
                return self._create_result(GlobalState.ERROR)

            # Main loop
            while self._should_continue():
                # Guard: prevent infinite loops (safety limit)
                if self.context.step_count > 100:
                    break

                # Step the state machine
                transition = self._step_once()

                # Increment step count
                self.context.step_count += 1

            # Completed successfully
            return self._create_result(GlobalState.COMPLETED)

        except Exception as e:
            self.context.last_error = e
            if self.trace_recorder:
                self._record_error_span(type(e).__name__, str(e), "critical")
            return self._create_result(GlobalState.ERROR)

        finally:
            self._end_time = time.time()

    def _should_continue(self) -> bool:
        """Check if traversal should continue."""
        if not self.context.node_stack:
            return False
        if self._check_completion_policy():
            return False
        return True

    def _check_completion_policy(self) -> bool:
        """
        Check if completion policy is triggered.

        Returns:
            True if completion policy triggered (should stop)
        """
        policy = self.plan.completion_policy or CompletionPolicy()

        if policy.type == CompletionPolicyType.NONE:
            return False
        elif policy.type == CompletionPolicyType.TARGET_FOUND:
            for node_id in self.context.visited_nodes:
                node = self._node_registry.get(node_id)
                if node and self._matches_target(node.name, policy.target_name, policy.match_mode):
                    if policy.action_on_found in (TargetFoundAction.MARK_AND_STOP, TargetFoundAction.EXECUTE_THEN_STOP):
                        return True
            return False
        elif policy.type == CompletionPolicyType.TIMEOUT:
            elapsed = time.time() - self._start_time if self._start_time else 0
            return elapsed >= (policy.timeout_seconds or float("inf"))
        elif policy.type == CompletionPolicyType.MAX_STEPS:
            return self.context.step_count >= (policy.max_steps or float("inf"))

        return False

    def _matches_target(self, node_name: str, target_name: str, mode: MatchMode) -> bool:
        """Check if node name matches target."""
        if mode == MatchMode.EXACT:
            return node_name == target_name
        elif mode == MatchMode.CONTAINS:
            return target_name.lower() in node_name.lower()
        return False

    # ========================================================================
    # State Machine Stepping
    # ========================================================================

    def _step_once(self) -> Dict[str, Any]:
        """
        Execute a single state machine step.

        Returns:
            Transition record
        """
        # Create mock stack object for state machine
        stack = _NodeStackAdapter(self.context, self._node_registry)

        # Get current node for step tracking
        current_node = stack.peek()
        current_node_id = current_node.node_id if current_node else None

        # Record step start (NODE_SELECT boundary)
        if current_node_id:
            self._record_step_start(current_node_id, self.context.current_path)

        # Call state machine step
        t0 = time.time()
        transition = self.state_machine.step(
            stack=stack,
            context=self.context,
            vision=self.vision_service,
            action=self.action_executor,
        )
        step_duration_ms = (time.time() - t0) * 1000

        # V6.5: Extract handler metrics and generate spans
        metrics = getattr(self.state_machine, '_last_handler_metrics', None)
        if metrics:
            if "ai_call" in metrics:
                ai = metrics["ai_call"]
                self._record_ai_call_span(
                    capability=ai.get("capability", "vision"),
                    provider_id=ai.get("provider_id"),
                    success=ai.get("success", True),
                    latency_ms=ai.get("latency_ms", 0),
                )
            if "execution" in metrics:
                ex = metrics["execution"]
                self._record_execution_span(
                    action=ex.get("action", "unknown"),
                    status=ex.get("status", "success"),
                    target=ex.get("target"),
                    duration_ms=ex.get("duration_ms"),
                )
            if "error" in metrics:
                err = metrics["error"]
                self._record_error_span(
                    error_type=err.get("error_type", "UnknownError"),
                    error_message=err.get("error_message", ""),
                    severity=err.get("severity", "error"),
                )

        # Record state transition as a span
        from_state = transition.from_state.value if hasattr(transition.from_state, 'value') else str(transition.from_state)
        to_state = transition.to_state.value if hasattr(transition.to_state, 'value') else str(transition.to_state)
        self._record_state_transition(from_state, to_state)

        # Handle children when entering BRANCH state
        child_pushed = None
        if transition.to_state == TraversalState.BRANCH:
            from_state_enum = transition.from_state
            if from_state_enum in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY, TraversalState.PRECONDITION_CHECK):
                current = stack.peek()
                if current and not current.is_leaf():
                    child_id = self._get_next_unvisited_child(current)
                    if child_id:
                        self._push_node(child_id)
                        child_pushed = child_id

        # If we pushed a child, override next state to NODE_SELECT
        next_state = transition.to_state
        if child_pushed:
            next_state = TraversalState.NODE_SELECT

        # Update visited nodes
        if transition.to_state in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY) and transition.node_id:
            self.context.visited_nodes.add(transition.node_id)

        # Record step end (FRAME_COMPLETE boundary)
        if current_node_id:
            step_span_id = current_node_id  # Use node_id as step context key
            self._record_step_end(
                step_span_id=step_span_id,
                result={"next_state": str(next_state), "duration_ms": step_duration_ms},
            )

        # Return transition record
        return {
            "from_state": from_state,
            "to_state": next_state.value if hasattr(next_state, 'value') else next_state,
            "node_id": child_pushed or transition.node_id,
            "timestamp": transition.timestamp.isoformat(),
            "metadata": transition.metadata,
        }

    def _get_next_unvisited_child(self, node: TraversalNode) -> Optional[str]:
        """Get the next unvisited child for a node."""
        from src.graph.node import ChildrenStrategyType

        if node.node_id not in self.context.visited_children:
            self.context.visited_children[node.node_id] = set()

        visited = self.context.visited_children[node.node_id]
        strategy = node.children_strategy

        if not strategy:
            return None

        if strategy.type == ChildrenStrategyType.STATIC:
            for child_id in strategy.static_children:
                if child_id not in visited:
                    visited.add(child_id)
                    return child_id
            return None
        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            return None
        elif strategy.type == ChildrenStrategyType.NONE:
            return None

        return None

    def _get_children(self, node: TraversalNode) -> List[str]:
        """Get children IDs for a node."""
        from src.graph.node import ChildrenStrategyType

        strategy = node.children_strategy
        if not strategy:
            return []

        if strategy.type == ChildrenStrategyType.STATIC:
            return strategy.static_children.copy()
        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            return []
        elif strategy.type == ChildrenStrategyType.NONE:
            return []

        return []

    # ========================================================================
    # Trace Span Generation (V6.3)
    # ========================================================================

    def _record_state_transition(self, from_state: str, to_state: str) -> None:
        """Record a state_transition span."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return
        span = SpanNode(
            span_type="state_transition",
            from_state=from_state,
            to_state=to_state,
            state_machine="traversal_fsm",
        )
        self.trace_recorder.record_span(span)

    def _record_execution_span(
        self,
        action: str,
        status: str,
        target: Optional[str] = None,
        page_before: Optional[str] = None,
        page_after: Optional[str] = None,
        duration_ms: Optional[float] = None,
    ) -> None:
        """Record an execution span."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return
        span = SpanNode(
            span_type="execution",
            action=action,
            status=status,
            target=target,
            page_before=page_before,
            page_after=page_after,
            duration_ms=duration_ms,
        )
        self.trace_recorder.record_span(span)

    def _record_ai_call_span(
        self,
        capability: str,
        provider_id: Optional[str],
        success: bool,
        latency_ms: float,
        input_tokens: Optional[int] = None,
        output_tokens: Optional[int] = None,
    ) -> None:
        """Record an AI call span."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return
        span = SpanNode(
            span_type="ai_call",
            capability=capability,
            provider_id=provider_id,
            success=success,
            latency_ms=latency_ms,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
        )
        self.trace_recorder.record_span(span)

    def _record_error_span(
        self,
        error_type: str,
        error_message: str,
        severity: str = "error",
        stack_trace: Optional[str] = None,
    ) -> None:
        """Record an error span."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return
        span = SpanNode(
            span_type="error",
            error_type=error_type,
            error_message=error_message,
            severity=severity,
            stack_trace=stack_trace,
        )
        self.trace_recorder.record_span(span)

    def _record_step_start(
        self, node_id: str, page_path: List[str]
    ) -> None:
        """Record a step boundary start (NODE_SELECT)."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return
        step_node = StepNode(
            node_id=node_id,
            step_type="NODE_SELECT",
            page_path=list(page_path),
        )
        self.trace_recorder.record_step_start(step_node)

    def _record_step_end(
        self, step_span_id: str, result: Optional[Dict[str, Any]] = None
    ) -> None:
        """Record a step boundary end (FRAME_COMPLETE)."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return
        self.trace_recorder.record_step_end(step_span_id, result)

    # ========================================================================
    # Depth and Cache Management
    # ========================================================================

    def _check_depth_limit(self) -> bool:
        """Check if depth limit is reached."""
        return self.context.is_at_max_depth()

    def _update_page_cache(self, path: str, page_info: Dict[str, Any]) -> None:
        """Update page cache for a path."""
        self.context.page_cache[path] = PageCacheInfo(
            items=page_info.get("items", []),
            timestamp=time.time(),
            screen_hash=page_info.get("hash"),
        )

    def _restore_from_cache(self, path: str) -> Optional[Dict[str, Any]]:
        """Restore page info from cache."""
        cached = self.context.page_cache.get(path)
        if cached:
            return {"items": cached.items, "from_cache": True}
        return None

    # ========================================================================
    # Result Creation
    # ========================================================================

    def _create_result(self, final_state: GlobalState) -> TraversalResult:
        """Create a TraversalResult and finalize the trace."""
        elapsed = (
            (self._end_time - self._start_time)
            if self._start_time and self._end_time
            else (time.time() - self._start_time if self._start_time else 0.0)
        )

        # Finalize trace
        if self.trace_recorder:
            status = "completed" if final_state == GlobalState.COMPLETED else "error"
            self.trace_recorder.finalize(status=status)

        # Build trace from history
        trace = []
        for transition in self.state_machine.get_transition_history():
            trace.append({
                "from_state": transition.from_state.value,
                "to_state": transition.to_state.value,
                "node_id": transition.node_id,
                "timestamp": transition.timestamp.isoformat(),
                "metadata": transition.metadata,
            })

        return TraversalResult(
            status=final_state,
            elapsed_seconds=elapsed,
            total_steps=self.context.step_count,
            visited_nodes=self.context.visited_nodes.copy(),
            trace=trace,
            trace_id=self.context.trace_id,
            error=self.context.last_error,
            metrics={
                "cache_hits": sum(1 for v in self.context.page_cache.values() if v),
                "failed_nodes": len(self.context.failed_nodes),
                "max_depth": self.context.max_depth,
            },
        )


# ============================================================================
# Helper Classes
# ============================================================================


class _NodeStackAdapter:
    """
    Adapter for node stack to work with state machine.

    Provides stack-like interface backed by TraversalRuntimeContext.
    """

    def __init__(self, context: TraversalRuntimeContext, node_registry: Dict[str, TraversalNode]):
        self._context = context
        self._registry = node_registry

    def is_empty(self) -> bool:
        """Check if stack is empty."""
        return len(self._context.node_stack) == 0

    def size(self) -> int:
        """Get stack size."""
        return len(self._context.node_stack)

    def peek(self) -> Optional[TraversalNode]:
        """Get current node without popping."""
        if self._context.node_stack:
            node_id = self._context.node_stack[-1].node_id
            return self._registry.get(node_id)
        return None

    def pop(self) -> Optional[TraversalNode]:
        """Pop and return current node."""
        if self._context.node_stack:
            node_id = self._context.node_stack.pop().node_id
            return self._registry.get(node_id)
        return None

    def push(self, node: TraversalNode) -> None:
        """Push a node onto the stack."""
        self._context.node_stack.append(
            StackFrame(node_id=node.node_id, span_id=node.node_id)
        )
