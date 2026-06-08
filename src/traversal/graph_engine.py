"""
Graph traversal engine for V6 declarative traversal.

This module implements the GraphTraversalEngine, which executes traversal
plans using a graph-based approach with state machine-driven control.

V6.3: Integrated distributed tracing with Span generation at state
transitions, AI calls, action execution, and error handling.

V6.8: Added complete engine initialization including:
- Plan validation (root_node requirements)
- Entry strategy execution with automatic fallback chain
- Wait condition verification (fast/polling modes)
- EntryConfig support for type-safe configuration
- Initialization exception types (ConfigurationError, EntryPolicyError, WaitConditionError)
"""

import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional, Set

from src.graph.plan import TraversalPlan
from .page_snapshot_manager import PageSnapshotManager
from .dynamic_child_manager import DynamicChildManager
from .entry_policy_executor import EntryPolicyExecutor
from .plan_validator import PlanValidator
from .page_cache_manager import PageCacheInfo, PageCacheManager
from .trace_coordinator import TraceCoordinator
from .step_orchestrator import StepOrchestrator, StepContext
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
from src.trace.models import (
    SessionNode,
    SpanNode,
    StepNode,
    PageTransitionSpan,
    DynamicNodeLifecycleSpan,
    StateDecisionSpan,
)
from src.trace.recorder import TraceRecorder
from src.trace.storage import MemoryStorage


# ============================================================================
# Dynamic Matching (V6.9)
# ============================================================================

from src.graph.matcher import DynamicMatcher
from src.graph.template import TemplateRegistry


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
        test_metadata: Optional[Dict[str, Any]] = None,
    ):
        """
        Initialize the graph traversal engine.

        Args:
            plan: TraversalPlan to execute
            vision_service: Service for screen analysis
            action_executor: Service for device control
            exception_chain: Optional exception handling chain
            trace_recorder: Optional trace recorder (auto-created if None)
            test_metadata: Optional test identification (test_name, expected_steps, etc.)
                           Written to session config for dashboard correlation.
        """
        self.plan = plan
        self.vision_service = vision_service
        self.action_executor = action_executor
        self.exception_chain = exception_chain
        self.trace_recorder = trace_recorder
        self._test_metadata = test_metadata or {}

        # State management
        self.state_machine = TraversalStateMachine()
        self.context = TraversalRuntimeContext(
            max_depth=plan.intent_slots.depth if plan.intent_slots and plan.intent_slots.depth else 100,
            global_state=GlobalState.IDLE,
        )
        self.context.wait_after_action_ms = 0

        # Node registry
        self._node_registry: Dict[str, TraversalNode] = {}
        self._build_node_registry()

        # Template registry and dynamic matcher (V6.9)
        self.template_registry: Optional[TemplateRegistry] = None
        self.dynamic_matcher: Optional[DynamicMatcher] = None
        self._dynamic_children: Dict[str, List[TraversalNode]] = {}
        self._last_known_path: List[str] = []
        self._load_template_registry()

        # Trace coordinator (V6.11.1) — created first, consumed by other components
        self._trace = TraceCoordinator(
            recorder=self.trace_recorder,
            plan=self.plan,
            context=self.context,
        )

        # Dynamic child manager (V6.11.0)
        self._child_mgr = DynamicChildManager(
            dynamic_matcher=self.dynamic_matcher,
            node_registry=self._node_registry,
            trace=self._trace,
        )

        # Page cache manager (V6.11.0)
        self._page_cache = PageCacheManager(self.context)

        # Entry policy executor (V6.11.0)
        self._entry_executor = EntryPolicyExecutor(
            plan=self.plan,
            vision_service=self.vision_service,
            action_executor=self.action_executor,
            trace=self._trace,
        )

        # Timing
        self._start_time: Optional[float] = None
        self._end_time: Optional[float] = None

        # Page/Action recording tracking
        self._last_recorded_path: List[str] = []
        self._last_recorded_action: Optional[str] = None

        # V6.10.2: Inject trace_recorder to state_machine for trace recording
        if self.trace_recorder:
            self.state_machine._trace_recorder = self.trace_recorder

    def _build_node_registry(self) -> None:
        """Build node registry from plan."""
        if self.plan.root_node:
            self._node_registry[self.plan.root_node.node_id] = self.plan.root_node
        for node_id, node in self.plan.static_nodes.items():
            self._node_registry[node_id] = node

    def _load_template_registry(self) -> None:
        """
        Load template registry for dynamic matching.

        Creates TemplateRegistry with built-in templates and optionally
        loads custom templates from file if specified in plan.
        """
        from pathlib import Path

        # Create template registry with built-in templates
        self.template_registry = TemplateRegistry()

        # Load custom templates if specified
        if self.plan.template_registry:
            custom_path = Path(self.plan.template_registry)
            if custom_path.exists():
                try:
                    self.template_registry.load_from_file(custom_path)
                except Exception as e:
                    # Log warning but continue with built-ins
                    import logging
                    logger = logging.getLogger(__name__)
                    logger.warning(
                        f"Failed to load custom templates from {custom_path}: {e}. "
                        f"Using built-in templates only."
                    )
            else:
                # File doesn't exist, log warning but continue
                import logging
                logger = logging.getLogger(__name__)
                logger.warning(
                    f"Template file not found: {custom_path}. "
                    f"Using built-in templates only."
                )

        # Create dynamic matcher
        self.dynamic_matcher = DynamicMatcher(self.template_registry)

    # ========================================================================
    # Initialization
    # ========================================================================

    def initialize(self) -> None:
        """
        Initialize the traversal engine.

        Creates a Session, initializes the TraceRecorder, validates the plan,
        executes the entry policy, verifies entry conditions, and sets up
        the initial traversal state.

        Raises:
            ConfigurationError: If plan validation fails
            EntryPolicyError: If all entry strategies fail
            WaitConditionError: If entry condition verification fails
            Exception: For unexpected errors (sets global_state to ERROR)
        """
        # Track whether we should set global_state to ERROR on unexpected exception
        initialization_failed = False

        try:
            self.context.global_state = GlobalState.INITIALIZING

            # Validate plan (V6.8)
            PlanValidator.validate(self.plan)

            # Create session
            config = {
                "plan_name": self.plan.plan_name,
                "plan_id": self.plan.plan_id,
            }
            if self._test_metadata:
                config.update(self._test_metadata)
            session = Session(
                traversal_mode="graph",
                config=config,
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
            self._trace.record_state_transition("IDLE", "INITIALIZING")

            # Execute entry policy (may raise EntryPolicyError)
            self._entry_executor.execute()

            # Wait for entry condition (may raise WaitConditionError)
            self._entry_executor.wait_for_condition()

            # Validate and push root node (V6.8)
            self._validate_and_push_root_node()

            # State transition: INITIALIZING → TRAVERSING
            self._trace.record_state_transition("INITIALIZING", "TRAVERSING")
            self.context.global_state = GlobalState.TRAVERSING

        except Exception as e:
            # Set global_state to ERROR on any exception
            self.context.global_state = GlobalState.ERROR

            # Record error in trace
            if self.trace_recorder:
                self._trace.record_error_span(type(e).__name__, str(e), "error")

            # Re-raise to propagate exception
            raise

    # ========================================================================
    # Entry Policy Framework (V6.8)
    # ========================================================================

    def _push_node(self, node_id: str) -> None:
        """Push a node onto the stack."""
        self.context.node_stack.append(
            StackFrame(node_id=node_id, span_id=node_id)
        )

        # V6.9.2: Record lifecycle event if this is a dynamic node
        is_dynamic = node_id not in self.plan.static_nodes and node_id != self.plan.root_node.node_id

        if is_dynamic:
            parent_id = None
            if len(self.context.node_stack) > 1:
                parent_id = self.context.node_stack[-2].node_id

            self._trace.record_dynamic_lifecycle(
                event="pushed",
                node_id=node_id,
                parent_id=parent_id,
            )

    def _pop_node(self) -> Optional[str]:
        """Pop a node from the stack."""
        if self.context.node_stack:
            node_id = self.context.node_stack.pop().node_id

            is_dynamic = node_id not in self.plan.static_nodes and node_id != self.plan.root_node.node_id

            if is_dynamic:
                self._trace.record_dynamic_lifecycle(
                    event="popped",
                    node_id=node_id,
                )

            return node_id
        return None

    def _peek_node(self) -> Optional[str]:
        """Get the current node ID without popping."""
        if self.context.node_stack:
            return self.context.node_stack[-1].node_id
        return None

    # ========================================================================
    # Root Node Processing (V6.8)
    # ========================================================================

    def _validate_and_push_root_node(self) -> None:
        """
        Validate and push root node to stack.

        This is the final step of initialization, after all entry strategies
        and condition verification have succeeded.

        Raises:
            ConfigurationError: If root_node is not configured (should not happen
                after _validate_plan() succeeds)
        """
        from src.exception.initialization import ConfigurationError

        if self.plan.root_node is None:
            raise ConfigurationError("root_node must be configured for traversal")

        node_id = self.plan.root_node.node_id

        # Initialize StepTracker for root node
        self._initialize_root_step(node_id)

        # Push root node to stack
        self._push_node(node_id)

        # Record root node pushed
        self._trace.record_root_node_pushed(node_id)

    def _initialize_root_step(self, node_id: str) -> None:
        """
        Initialize StepTracker for the root node step.

        This records the start of the root node step in the trace,
        establishing the initial traversal context.

        Args:
            node_id: ID of the root node
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        # Record step start with initial empty path
        step_node = StepNode(
            node_id=node_id,
            step_type="NODE_SELECT",
            page_path=[],  # Root node starts with empty path
        )
        self.trace_recorder.record_step_start(step_node)

    # ========================================================================
    # Trace Level Configuration (V6.8)
    # ========================================================================

    def _get_trace_level(self) -> str:
        """
        Get trace level from entry_config or meta.

        Returns:
            Trace level: "minimal", "standard", or "detailed"
        """
        if self.plan.entry_config:
            return self.plan.entry_config.trace_level
        return self.plan.meta.get("trace_level", "standard")

    def _should_record_entry_attempt(self) -> bool:
        """
        Check if entry strategy attempts should be recorded.

        Returns:
            True if trace level is "standard" or "detailed"
        """
        level = self._trace._get_trace_level()
        return level in ("standard", "detailed")

    def _should_record_vision_call(self) -> bool:
        """
        Check if individual vision calls should be recorded.

        Returns:
            True only if trace level is "detailed"
        """
        level = self._trace._get_trace_level()
        return level == "detailed"

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
            # Initialize (may raise ConfigurationError, EntryPolicyError, etc.)
            self.initialize()

            # Step orchestration (V6.11.1)
            self._step_executor = StepOrchestrator()
            step_ctx = StepContext(
                context=self.context,
                state_machine=self.state_machine,
                vision=self.vision_service,
                action=self.action_executor,
                child_mgr=self._child_mgr,
                node_registry=self._node_registry,
                trace=self._trace,
                last_known_path=self._last_known_path,
                last_recorded_path=self._last_recorded_path,
                last_recorded_action=self._last_recorded_action,
            )

            # Main loop
            while self._should_continue():
                if self.context.step_count > 500:
                    break

                transition = self._step_executor.execute_step(step_ctx)
                self.context.step_count += 1

            # Completed successfully
            return self._create_result(GlobalState.COMPLETED)

        except Exception as e:
            self.context.last_error = e
            if self.trace_recorder:
                import traceback
                stack_trace = traceback.format_exc()
                self._trace.record_error_span(type(e).__name__, str(e), "critical", stack_trace)
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

    # Delegation wrappers for extracted components (backward compatibility)

    def _has_unvisited_children(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> Optional[bool]:
        return self._child_mgr.has_unvisited(node, context)

    # -- depth

    def _check_depth_limit(self) -> bool:
        """Check if depth limit is reached."""
        return self.context.is_at_max_depth()

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

    @property
    def is_empty(self) -> bool:
        """Check if stack is empty."""
        return len(self._context.node_stack) == 0

    @property
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
