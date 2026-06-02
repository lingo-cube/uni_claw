"""
Graph traversal engine for V6 declarative traversal.

This module implements the GraphTraversalEngine, which executes traversal
plans using a graph-based approach with state machine-driven control.
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
    error: Optional[Exception] = None  # Error if failed
    metrics: Dict[str, Any] = field(default_factory=dict)  # Performance metrics


@dataclass
class TraversalContext:
    """
    Runtime context for traversal execution.

    Contains all mutable state during traversal execution.
    """

    # Stack management
    node_stack: List[str] = field(default_factory=list)  # Node ID stack
    current_path: List[str] = field(default_factory=list)  # Screen path

    # Runtime state
    global_state: GlobalState = GlobalState.IDLE
    step_count: int = 0
    max_depth: int = 100  # Maximum traversal depth
    retry_count: int = 0  # Current retry count

    # Tracking
    visited_nodes: Set[str] = field(default_factory=set)
    visited_pages: Set[str] = field(default_factory=set)
    failed_nodes: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    visited_children: Dict[str, Set[str]] = field(default_factory=dict)  # Track visited children per node

    # Caching
    page_cache: Dict[str, Dict[str, Any]] = field(default_factory=dict)

    # History
    action_history: List[Dict[str, Any]] = field(
        default_factory=list
    )  # Recent actions (max 5)

    # Error handling
    last_error: Optional[Exception] = None

    # Optional dependencies (injected)
    exception_chain: Optional[Any] = None  # ExceptionHandlingChain
    ai_provider: Optional[Any] = None  # AIProvider

    def get_current_depth(self) -> int:
        """Get current traversal depth from stack size."""
        return len(self.node_stack)

    def is_at_max_depth(self) -> bool:
        """Check if at maximum depth."""
        return self.get_current_depth() >= self.max_depth

    def record_action(self, action: str, **kwargs) -> None:
        """Record an action in history."""
        self.action_history.append(
            {"action": action, "timestamp": datetime.now(), **kwargs}
        )
        # Keep only last 5 actions
        if len(self.action_history) > 5:
            self.action_history = self.action_history[-5:]


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
    Supports depth limiting, page caching, and completion policies.
    """

    def __init__(
        self,
        plan: TraversalPlan,
        vision_service: Any,  # VisionService interface
        action_executor: Any,  # ActionExecutor interface
        exception_chain: Optional[Any] = None,
        trace_recorder: Optional[Any] = None,
    ):
        """
        Initialize the graph traversal engine.

        Args:
            plan: TraversalPlan to execute
            vision_service: Service for screen analysis
            action_executor: Service for device control
            exception_chain: Optional exception handling chain
            trace_recorder: Optional trace recorder
        """
        self.plan = plan
        self.vision_service = vision_service
        self.action_executor = action_executor
        self.exception_chain = exception_chain
        self.trace_recorder = trace_recorder

        # State management
        self.state_machine = TraversalStateMachine()
        self.context = TraversalContext(
            max_depth=plan.intent_slots.depth if plan.intent_slots and plan.intent_slots.depth else 100,
            exception_chain=exception_chain,
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
        # Add root node if present
        if self.plan.root_node:
            self._node_registry[self.plan.root_node.node_id] = self.plan.root_node

        # Add static nodes
        for node_id, node in self.plan.static_nodes.items():
            self._node_registry[node_id] = node

    def _load_template_registry(self) -> None:
        """Load template registry for dynamic matching."""
        # Placeholder for template registry loading
        # Would load from plan.template_registry path
        pass

    # ========================================================================
    # Initialization
    # ========================================================================

    def initialize(self) -> bool:
        """
        Initialize the traversal engine.

        Executes entry policy, waits for conditions, and sets up initial state.

        Returns:
            True if initialization succeeded
        """
        try:
            self.context.global_state = GlobalState.INITIALIZING

            # Execute entry policy
            if not self._execute_entry_policy():
                return False

            # Wait for entry condition
            if not self._wait_for_entry_condition():
                return False

            # Push root node to stack
            if self.plan.root_node:
                self._push_node(self.plan.root_node.node_id)

            # Initialize trace
            if self.trace_recorder:
                self.trace_recorder.start_traversal(self.plan)

            self.context.global_state = GlobalState.TRAVERSING
            return True

        except Exception as e:
            self.context.last_error = e
            self.context.global_state = GlobalState.ERROR
            return False

    def _execute_entry_policy(self) -> bool:
        """Execute the entry policy to enter the target app."""
        policy = self.plan.entry_policy or EntryPolicy()

        if policy.strategy == EntryStrategy.COLD_LAUNCH:
            # Return to home screen
            # self.action_executor.press_home()
            # Find and click app icon
            # ...
            return True

        elif policy.strategy == EntryStrategy.DIRECT_DEEPLINK:
            # Use adb/am start
            # self.action_executor.start_app(self.plan.entry_app)
            return True

        elif policy.strategy == EntryStrategy.BIND_CURRENT_SCREEN:
            # Verify current screen is target
            # current_screen = self.vision_service.get_current_screen()
            # return current_screen.app_name == self.plan.entry_app
            return True

        return True

    def _wait_for_entry_condition(self) -> bool:
        """Wait for entry condition to be satisfied."""
        policy = self.plan.entry_policy or EntryPolicy()

        if not policy.wait_condition:
            return True

        # Placeholder: wait for condition
        # Would poll screen state until condition met or timeout
        return True

    def _push_node(self, node_id: str) -> None:
        """Push a node onto the stack."""
        self.context.node_stack.append(node_id)

    def _pop_node(self) -> Optional[str]:
        """Pop a node from the stack."""
        if self.context.node_stack:
            return self.context.node_stack.pop()
        return None

    def _peek_node(self) -> Optional[str]:
        """Get the current node ID without popping."""
        if self.context.node_stack:
            return self.context.node_stack[-1]
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
                # Step the state machine
                transition = self._step_once()

                # Record trace
                if self.trace_recorder:
                    self.trace_recorder.record_transition(transition)

                # Increment step count
                self.context.step_count += 1

            # Completed successfully
            return self._create_result(GlobalState.COMPLETED)

        except Exception as e:
            self.context.last_error = e
            return self._create_result(GlobalState.ERROR)

        finally:
            self._end_time = time.time()

    def _should_continue(self) -> bool:
        """Check if traversal should continue."""
        # Check if stack is empty
        if not self.context.node_stack:
            return False

        # Check completion policy
        if self._check_completion_policy():
            return False

        # Check global state
        if self.context.global_state in (GlobalState.TERMINATED, GlobalState.ERROR):
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
            # Check if target node found
            for node_id in self.context.visited_nodes:
                node = self._node_registry.get(node_id)
                if node and self._matches_target(node.name, policy.target_name, policy.match_mode):
                    # Target found
                    if policy.action_on_found == TargetFoundAction.MARK_AND_STOP:
                        return True
                    elif policy.action_on_found == TargetFoundAction.EXECUTE_THEN_STOP:
                        # Execute and then stop
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

        # Call state machine step
        transition = self.state_machine.step(
            stack=stack,
            context=self.context,
            vision=self.vision_service,
            action=self.action_executor,
        )

        # Handle children when entering BRANCH state from EXECUTE/RESULT_VERIFY
        child_pushed = None
        if transition.to_state == TraversalState.BRANCH:
            from_state = transition.from_state
            if from_state in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY, TraversalState.PRECONDITION_CHECK):
                # We just finished executing a node, check if it has children to process
                current_node = stack.peek()
                if current_node and not current_node.is_leaf():
                    # Get the first unvisited child and push it
                    child_id = self._get_next_unvisited_child(current_node)
                    if child_id:
                        self._push_node(child_id)
                        child_pushed = child_id

        # If we pushed a child, override next state to NODE_SELECT
        next_state = transition.to_state
        if child_pushed:
            next_state = TraversalState.NODE_SELECT

        # Update visited nodes (only when actually executed, not just pushed to stack)
        # Only mark as visited if we're in EXECUTE or RESULT_VERIFY state
        if transition.to_state in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY) and transition.node_id:
            self.context.visited_nodes.add(transition.node_id)

        # Return transition record
        return {
            "from_state": transition.from_state.value,
            "to_state": next_state.value if hasattr(next_state, 'value') else next_state,
            "node_id": child_pushed or transition.node_id,
            "timestamp": transition.timestamp.isoformat(),
            "metadata": transition.metadata,
        }

    def _get_next_unvisited_child(self, node: TraversalNode) -> Optional[str]:
        """
        Get the next unvisited child for a node.

        Args:
            node: TraversalNode to get next child for

        Returns:
            Next unvisited child ID, or None if all children visited
        """
        from src.graph.node import ChildrenStrategyType

        # Initialize visited children set if needed
        if node.node_id not in self.context.visited_children:
            self.context.visited_children[node.node_id] = set()

        visited = self.context.visited_children[node.node_id]
        strategy = node.children_strategy

        if not strategy:
            return None

        if strategy.type == ChildrenStrategyType.STATIC:
            # Find first unvisited child
            for child_id in strategy.static_children:
                if child_id not in visited:
                    # Mark as visited
                    visited.add(child_id)
                    return child_id
            return None

        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            # TODO: Implement dynamic child generation
            return None

        elif strategy.type == ChildrenStrategyType.NONE:
            return None

        return None

    def _get_children(self, node: TraversalNode) -> List[str]:
        """
        Get children IDs for a node.

        Args:
            node: TraversalNode to get children for

        Returns:
            List of child node IDs
        """
        from src.graph.node import ChildrenStrategyType

        strategy = node.children_strategy
        if not strategy:
            return []

        if strategy.type == ChildrenStrategyType.STATIC:
            return strategy.static_children.copy()

        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            # TODO: Implement dynamic child generation
            # Would use vision service to find matching elements
            return []

        elif strategy.type == ChildrenStrategyType.NONE:
            return []

        return []

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
        """Create a TraversalResult."""
        elapsed = (
            (self._end_time - self._start_time)
            if self._start_time and self._end_time
            else (time.time() - self._start_time if self._start_time else 0.0)
        )

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

    Provides stack-like interface backed by TraversalContext.
    """

    def __init__(self, context: TraversalContext, node_registry: Dict[str, TraversalNode]):
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
            node_id = self._context.node_stack[-1]
            return self._registry.get(node_id)
        return None

    def pop(self) -> Optional[TraversalNode]:
        """Pop and return current node."""
        if self._context.node_stack:
            node_id = self._context.node_stack.pop()
            return self._registry.get(node_id)
        return None

    def push(self, node: TraversalNode) -> None:
        """Push a node onto the stack."""
        self._context.node_stack.append(node.node_id)
