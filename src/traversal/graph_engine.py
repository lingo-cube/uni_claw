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

        # Template registry and dynamic matcher (V6.9)
        self.template_registry: Optional[TemplateRegistry] = None
        self.dynamic_matcher: Optional[DynamicMatcher] = None
        self._dynamic_children: Dict[str, List[TraversalNode]] = {}
        self._last_known_path: List[str] = []
        self._load_template_registry()

        # Timing
        self._start_time: Optional[float] = None
        self._end_time: Optional[float] = None

        # Page/Action recording tracking
        self._last_recorded_path: List[str] = []
        self._last_recorded_action: Optional[str] = None

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

    def _validate_plan(self) -> None:
        """
        Validate the traversal plan before initialization.

        Raises:
            ConfigurationError: If plan validation fails
        """
        from src.exception.initialization import ConfigurationError

        # Check root_node existence
        if self.plan.root_node is None:
            raise ConfigurationError("root_node is required in traversal plan")

        root = self.plan.root_node

        # Check root_node type (must be CONTAINER)
        if root.node_type != NodeType.CONTAINER:
            raise ConfigurationError(
                f"Root node must be CONTAINER type, got {root.node_type.value}"
            )

        # Check root_node operation (must be no_action)
        if root.operation.action != "no_action":
            raise ConfigurationError(
                f"Root node operation should be 'no_action', got '{root.operation.action}'"
            )

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
            self._validate_plan()

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

            # Execute entry policy (may raise EntryPolicyError)
            self._execute_entry_policy()

            # Wait for entry condition (may raise WaitConditionError)
            self._wait_for_entry_condition()

            # Validate and push root node (V6.8)
            self._validate_and_push_root_node()

            # State transition: INITIALIZING → TRAVERSING
            self._record_state_transition("INITIALIZING", "TRAVERSING")
            self.context.global_state = GlobalState.TRAVERSING

        except Exception as e:
            # Set global_state to ERROR on any exception
            self.context.global_state = GlobalState.ERROR

            # Record error in trace
            if self.trace_recorder:
                self._record_error_span(type(e).__name__, str(e), "error")

            # Re-raise to propagate exception
            raise

    # ========================================================================
    # Entry Policy Framework (V6.8)
    # ========================================================================

    def _build_strategy_chain(self) -> List[EntryStrategy]:
        """
        Build the fallback strategy chain.

        The chain is: primary strategy → fallback strategy → bind_current_screen (default).

        Returns:
            List of strategies to attempt in order
        """
        from src.graph.node import EntryStrategy

        policy = self.plan.entry_policy or EntryPolicy()
        chain = []

        # Add primary strategy
        if isinstance(policy.strategy, str):
            try:
                primary = EntryStrategy.from_value(policy.strategy)
                chain.append(primary)
            except ValueError:
                chain.append(EntryStrategy.COLD_LAUNCH)
        else:
            chain.append(policy.strategy)

        # Add fallback strategy if configured
        if policy.fallback:
            if isinstance(policy.fallback, str):
                try:
                    fallback = EntryStrategy.from_value(policy.fallback)
                    if fallback != chain[0]:  # Avoid duplicate
                        chain.append(fallback)
                except ValueError:
                    pass  # Invalid fallback, skip

        # Always add bind_current_screen as final fallback (if not already present)
        if EntryStrategy.BIND_CURRENT_SCREEN not in chain:
            chain.append(EntryStrategy.BIND_CURRENT_SCREEN)

        return chain

    def _execute_entry_policy(self) -> bool:
        """
        Execute the entry policy with automatic fallback chain.

        Attempts each strategy in the fallback chain until one succeeds.
        If all strategies fail, raises EntryPolicyError.

        Returns:
            True if entry policy succeeded

        Raises:
            EntryPolicyError: If all strategies in the chain fail
        """
        from src.exception.initialization import EntryError, EntryPolicyError

        policy = self.plan.entry_policy or EntryPolicy()
        strategy_chain = self._build_strategy_chain()

        failed_strategies = []
        last_error = None

        for strategy in strategy_chain:
            try:
                # Execute single strategy
                self._execute_single_strategy(strategy)

                # If we get here, strategy succeeded
                self._record_entry_success(strategy)
                return True

            except EntryError as e:
                # Strategy failed, try next in chain
                failed_strategies.append(strategy.value)
                last_error = e
                self._record_entry_failure(strategy, str(e))

        # All strategies failed
        raise EntryPolicyError(
            f"All entry strategies failed for app '{self.plan.entry_app}'",
            failed_strategies=failed_strategies,
            last_error=last_error,
        )

    def _execute_single_strategy(self, strategy: EntryStrategy) -> None:
        """
        Execute a single entry strategy.

        Args:
            strategy: The entry strategy to execute

        Raises:
            EntryError: If strategy execution fails
        """
        from src.exception.initialization import EntryError

        if strategy == EntryStrategy.DIRECT_DEEPLINK:
            self._execute_deeplink_strategy()
        elif strategy == EntryStrategy.COLD_LAUNCH:
            self._execute_cold_launch_strategy()
        elif strategy == EntryStrategy.BIND_CURRENT_SCREEN:
            self._execute_bind_current_screen_strategy()
        else:
            raise EntryError(strategy.value, f"Unknown strategy: {strategy.value}")

    def _execute_deeplink_strategy(self) -> None:
        """
        Execute deeplink entry strategy.

        Sends deeplink intent to target application.

        Raises:
            EntryError: If deeplink execution fails
        """
        from src.exception.initialization import EntryError

        deeplink = f"{self.plan.entry_app}://"

        try:
            # Send deeplink via action executor
            self.action_executor.execute_deeplink(deeplink)

            # Apply action delay
            delay_ms = self._get_action_delay()
            if delay_ms > 0:
                time.sleep(delay_ms / 1000.0)

        except Exception as e:
            raise EntryError("direct_deeplink", f"Failed to send deeplink: {e}") from e

    def _execute_cold_launch_strategy(self) -> None:
        """
        Execute cold launch entry strategy.

        Returns to home screen and clicks target app icon.

        Raises:
            EntryError: If app icon cannot be found or clicked
        """
        from src.exception.initialization import EntryError

        try:
            # Press home to ensure we're at home screen
            self.action_executor.press_home()

            # Wait for home screen to settle
            time.sleep(0.5)

            # Find app icon
            icon_target = self._find_app_icon()
            if not icon_target:
                raise EntryError("cold_launch", f"App icon not found for '{self.plan.entry_app}'")

            # Click the icon
            self.action_executor.click(icon_target)

            # Apply action delay
            delay_ms = self._get_action_delay()
            if delay_ms > 0:
                time.sleep(delay_ms / 1000.0)

        except EntryError:
            raise
        except Exception as e:
            raise EntryError("cold_launch", f"Failed to launch app: {e}") from e

    def _execute_bind_current_screen_strategy(self) -> None:
        """
        Execute bind current screen strategy.

        Assumes device is already on target screen, takes no action.

        This strategy always succeeds but may wait for action delay.
        """
        # Apply action delay (if configured)
        delay_ms = self._get_action_delay()
        if delay_ms > 0:
            time.sleep(delay_ms / 1000.0)

        # No action to take - device should already be on target screen

    def _find_app_icon(self) -> Optional[str]:
        """
        Find target app icon on home screen.

        Returns:
            Target description for clicking, or None if not found

        Note:
            EXTENSION POINT: Future enhancements could include:
            - Desktop page swiping for multi-page home screens
            - Folder detection and opening
            - Icon text matching with variations
        """
        try:
            # Use vision service to find app icon
            result = self.vision_service.find_element(
                query=f"App icon for {self.plan.entry_app}",
                screen_context="home_screen"
            )

            if result and result.get("found"):
                return result.get("target")

            return None

        except Exception:
            # Vision failure - treat as not found
            return None

    def _get_action_delay(self) -> int:
        """
        Get action delay from entry_config or meta.

        Returns:
            Delay in milliseconds
        """
        # Priority 1: Check entry_config
        if self.plan.entry_config:
            return self.plan.entry_config.action_delay_ms

        # Priority 2: Check meta dictionary
        return self.plan.meta.get("action_delay_ms", 100)  # Default 100ms

    def _record_entry_success(self, strategy: EntryStrategy) -> None:
        """Record successful entry strategy execution."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        # Check trace level (V6.8)
        if not self._should_record_entry_attempt():
            return

        span = SpanNode(
            span_type="execution",
            action="entry_strategy",
            status="success",
            target=strategy.value,
        )
        self.trace_recorder.record_span(span)

    def _record_entry_failure(self, strategy: EntryStrategy, reason: str) -> None:
        """Record failed entry strategy execution."""
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        # Check trace level (V6.8)
        if not self._should_record_entry_attempt():
            return

        span = SpanNode(
            span_type="execution",
            action="entry_strategy",
            status="failed",
            target=strategy.value,
            error_message=reason,
        )
        self.trace_recorder.record_span(span)

    def _wait_for_entry_condition(self) -> bool:
        """
        Wait for entry condition to be satisfied.

        Returns:
            True if condition satisfied or no condition configured

        Raises:
            WaitConditionError: If condition verification fails
        """
        from src.exception.initialization import WaitConditionError

        policy = self.plan.entry_policy or EntryPolicy()

        # No wait condition configured - proceed
        if not policy.wait_condition:
            return True

        # Verify entry success
        try:
            success = self._verify_entry_success()
            if not success:
                raise WaitConditionError(
                    f"Entry condition not satisfied for app '{self.plan.entry_app}'",
                    condition=policy.wait_condition,
                    timeout_seconds=self._get_wait_timeout(),
                )
            return True
        except WaitConditionError:
            raise
        except Exception as e:
            raise WaitConditionError(
                f"Error verifying entry condition: {e}",
                condition=policy.wait_condition,
            ) from e

    def _verify_entry_success(self) -> bool:
        """
        Verify that entry was successful by checking wait condition.

        Supports both fast (single check) and polling (repeated checks) modes.

        Returns:
            True if entry condition is satisfied
        """
        policy = self.plan.entry_policy or EntryPolicy()
        wait_condition = policy.wait_condition or {}

        # Get wait mode from entry_config or meta
        wait_mode = self._get_wait_mode()

        if wait_mode == "fast":
            return self._verify_condition_once(wait_condition)
        else:  # polling mode
            return self._verify_condition_polling(wait_condition)

    def _verify_condition_once(self, condition: dict) -> bool:
        """
        Verify entry condition with a single vision check (fast mode).

        Args:
            condition: Wait condition dict with expected page state

        Returns:
            True if current page matches expected condition
        """
        try:
            # Get current page info from vision service
            current_path = self._get_current_page_path()

            # Check if path matches expected page_name
            expected_page = condition.get("page_name")
            if not expected_page:
                return True  # No specific page expected, auto-pass

            # Match last component of path (current page)
            if current_path and current_path[-1] == expected_page:
                return True

            return False

        except Exception:
            # Vision error - treat as verification failure
            return False

    def _verify_condition_polling(self, condition: dict) -> bool:
        """
        Verify entry condition with repeated polling.

        Args:
            condition: Wait condition dict with expected page state

        Returns:
            True if condition is satisfied before timeout
        """
        timeout = self._get_wait_timeout()
        interval = self._get_wait_interval()
        expected_page = condition.get("page_name")

        start_time = time.time()
        elapsed = 0

        while elapsed < timeout:
            # Check condition
            if self._verify_condition_once(condition):
                return True

            # Wait for interval
            time.sleep(interval)
            elapsed = time.time() - start_time

        # Timeout - condition not satisfied
        return False

    def _get_current_page_path(self) -> Optional[List[str]]:
        """
        Get current page path from vision service.

        Returns:
            Current page path as list of page names, or None if unavailable
        """
        try:
            result = self.vision_service.get_current_page()
            if result:
                return result.get("path")
            return None
        except Exception:
            return None

    def _get_wait_mode(self) -> str:
        """Get wait mode from entry_config or meta."""
        if self.plan.entry_config:
            return self.plan.entry_config.wait_mode
        return self.plan.meta.get("wait_mode", "fast")

    def _get_wait_timeout(self) -> float:
        """Get wait timeout from entry_config or meta."""
        if self.plan.entry_config:
            return self.plan.entry_config.wait_timeout
        return self.plan.meta.get("wait_timeout", 10.0)

    def _get_wait_interval(self) -> float:
        """Get wait interval from entry_config or meta."""
        if self.plan.entry_config:
            return self.plan.entry_config.wait_interval
        return self.plan.meta.get("wait_interval", 1.0)

    def _push_node(self, node_id: str) -> None:
        """Push a node onto the stack."""
        self.context.node_stack.append(
            StackFrame(node_id=node_id, span_id=node_id)
        )

        # V6.9.2: Record lifecycle event if this is a dynamic node
        # Check if node is in any dynamic children cache (was generated dynamically)
        is_dynamic = any(
            node_id in [child.node_id for child in children]
            for children in self._dynamic_children.values()
        )

        if is_dynamic:
            # Get parent ID from the node stack (second from top)
            parent_id = None
            if len(self.context.node_stack) > 1:
                parent_id = self.context.node_stack[-2].node_id

            self._record_dynamic_lifecycle(
                event="pushed",
                node_id=node_id,
                parent_id=parent_id,
            )

    def _pop_node(self) -> Optional[str]:
        """Pop a node from the stack."""
        if self.context.node_stack:
            node_id = self.context.node_stack.pop().node_id

            # V6.9.2: Record lifecycle event if this is a dynamic node
            # Check if node is in any dynamic children cache (was generated dynamically)
            is_dynamic = any(
                node_id in [child.node_id for child in children]
                for children in self._dynamic_children.values()
            )

            if is_dynamic:
                self._record_dynamic_lifecycle(
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
        self._record_root_node_pushed(node_id)

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

    def _record_root_node_pushed(self, node_id: str) -> None:
        """
        Record root node push event in trace.

        Args:
            node_id: ID of the root node being pushed
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        span = SpanNode(
            span_type="state_transition",
            from_state="INITIALIZING",
            to_state="TRAVERSING",
            state_machine="graph_engine",
        )
        self.trace_recorder.record_span(span)

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
        level = self._get_trace_level()
        return level in ("standard", "detailed")

    def _should_record_vision_call(self) -> bool:
        """
        Check if individual vision calls should be recorded.

        Returns:
            True only if trace level is "detailed"
        """
        level = self._get_trace_level()
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

            # Main loop
            while self._should_continue():
                # Guard: prevent infinite loops (safety limit)
                # V6.9.4: Increased limit for multi-layer traversal scenarios
                if self.context.step_count > 500:
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
                import traceback
                stack_trace = traceback.format_exc()
                self._record_error_span(type(e).__name__, str(e), "critical", stack_trace)
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

        # V6.9.2: Record page snapshot when path changes
        if hasattr(self.context, 'current_page_analysis') and self.context.current_page_analysis:
            current_path = list(self.context.current_path) if self.context.current_path else []
            if current_path != self._last_recorded_path:
                self._record_page_analysis(self.context.current_page_analysis)
                self._last_recorded_path = current_path

        # V6.9.2: Record action execution
        execution_metrics = None
        metrics = getattr(self.state_machine, '_last_handler_metrics', None)
        if metrics and "execution" in metrics:
            execution_metrics = metrics["execution"]
            # Handle case where execution is a list (from precondition_check retries)
            if isinstance(execution_metrics, list):
                if execution_metrics:
                    execution_metrics = execution_metrics[-1]  # Get last execution
                else:
                    execution_metrics = None
            if execution_metrics:
                action = execution_metrics.get("action")
                if action and action != self._last_recorded_action:
                    self._record_action_execution(
                        action=action,
                        target=execution_metrics.get("target"),
                        success=execution_metrics.get("status", "success") == "success",
                    )
                    self._last_recorded_action = action

        # V6.6: Extract handler metrics and generate spans
        self._record_metrics_as_spans(metrics)

        # Record state transition as a span
        from_state = transition.from_state.value if hasattr(transition.from_state, 'value') else str(transition.from_state)
        to_state = transition.to_state.value if hasattr(transition.to_state, 'value') else str(transition.to_state)
        self._record_state_transition(from_state, to_state)

        # Handle children when entering BRANCH state
        child_pushed = None
        should_complete_frame = False
        if transition.to_state == TraversalState.BRANCH:
            from_state_enum = transition.from_state
            if from_state_enum in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY, TraversalState.PRECONDITION_CHECK):
                current = stack.peek()
                if current and not current.is_leaf():
                    child_id = self._get_next_unvisited_child(current)
                    if child_id:
                        self._push_node(child_id)
                        child_pushed = child_id
                    else:
                        # V6.9.5: No more unvisited children - complete the frame
                        should_complete_frame = True

        # V6.9.3: Handle DYNAMIC_MATCH children when entering NODE_SELECT
        # This handles cases where:
        # 1. _handle_branch returns NODE_SELECT for DYNAMIC_MATCH nodes
        # 2. FRAME_COMPLETE returns to NODE_SELECT after a leaf completes
        if transition.to_state == TraversalState.NODE_SELECT:
            current = stack.peek()
            if current and current.children_strategy:
                from src.graph.node import ChildrenStrategyType
                if current.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
                    # Try to get next unvisited child
                    child_id = self._get_next_unvisited_child(current)
                    if child_id:
                        self._push_node(child_id)
                        child_pushed = child_id

        # V6.9: FRAME_COMPLETE interception - check for remaining dynamic children
        if transition.to_state == TraversalState.FRAME_COMPLETE:
            current = stack.peek()
            if current and current.children_strategy:
                from src.graph.node import ChildrenStrategyType
                if current.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
                    # Check if there are unvisited dynamic children
                    remaining_child_id = self._get_next_unvisited_child(current)
                    if remaining_child_id:
                        # Push the remaining child and override to NODE_SELECT
                        self._push_node(remaining_child_id)
                        child_pushed = remaining_child_id

        # Determine next state
        next_state = transition.to_state

        # V6.9.5: Override to FRAME_COMPLETE if no more children (should_complete_frame)
        if should_complete_frame:
            next_state = TraversalState.FRAME_COMPLETE
            # Update state machine to FRAME_COMPLETE
            self.state_machine.transition_to(TraversalState.FRAME_COMPLETE, action="no_more_children")

        if child_pushed:
            next_state = TraversalState.NODE_SELECT
            # V6.9.4/5: Update state machine to NODE_SELECT when child is pushed
            # This ensures the child node starts from NODE_SELECT instead of continuing from parent's state
            # V6.9.5: Avoid invalid transition if already in NODE_SELECT state
            if self.state_machine._state != TraversalState.NODE_SELECT:
                self.state_machine.transition_to(TraversalState.NODE_SELECT, node_id=child_pushed, action="push_child")
            else:
                # Already in NODE_SELECT - just update the node_id
                self.state_machine.set_current_node(child_pushed)
            # Return immediately to let next iteration handle the new node
            return {
                "from_state": transition.from_state,
                "to_state": TraversalState.NODE_SELECT,  # Override to_state to reflect actual state
                "next_state": next_state,
                "node_id": transition.node_id,
                "child_pushed": child_pushed,
            }

        # Update visited nodes
        # V6.9.4: Add both the transition node and current stack node to visited_nodes
        # This ensures that dynamically pushed children are also tracked
        if transition.to_state in (TraversalState.EXECUTE, TraversalState.RESULT_VERIFY):
            # Add the node being transitioned from (if available)
            if transition.node_id:
                self.context.visited_nodes.add(transition.node_id)
            # Also add the current node on the stack (handles dynamically pushed children)
            current = stack.peek()
            if current:
                self.context.visited_nodes.add(current.node_id)

        # V6.9: Path change detection and cache invalidation
        path_now = list(self.context.current_path)
        if path_now != self._last_known_path:
            # V6.9.2: Record page transition if path changed
            self._record_page_transition(self._last_known_path, path_now, transition)

            # Path changed - invalidate cache for current container
            current = stack.peek()
            if current:
                self.invalidate_children_cache(current.node_id)
            self._last_known_path = path_now

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
        """
        Get the next unvisited child for a node.

        V6.9: Extended to support DYNAMIC_MATCH strategy with dynamic child generation.
        """
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
            # V6.9: Generate dynamic children on first access
            if node.node_id not in self._dynamic_children:
                self._generate_dynamic_children(node)

            # Return next unvisited child from cache
            children = self._dynamic_children.get(node.node_id, [])
            for child in children:
                if child.node_id not in visited:
                    visited.add(child.node_id)
                    return child.node_id
            return None
        elif strategy.type == ChildrenStrategyType.NONE:
            return None

        return None

    def _generate_dynamic_children(self, node: TraversalNode) -> None:
        """
        Generate dynamic children for a node using DynamicMatcher.

        V6.9: Converts DynamicRule objects to dicts, maps MenuItem fields,
        calls match_all, and caches generated children.
        """
        from src.graph.node import DynamicRule
        from src.graph.matcher import MatchAction

        if not self.dynamic_matcher:
            return

        # 1. Convert DynamicRule objects to dict format for matcher
        # V6.9.3: Handle both DynamicRule objects and plain dicts (from test data)
        rules = {}
        if node.children_strategy and node.children_strategy.dynamic_rules:
            for rule_id, rule in node.children_strategy.dynamic_rules.items():
                # Handle both DynamicRule objects and plain dicts
                if isinstance(rule, dict):
                    # Already a dict - use as-is
                    rule_dict = rule
                else:
                    # DynamicRule object - extract fields
                    rule_dict = {
                        "match_condition": rule.match_condition,
                        "child_template": rule.child_template,
                        "action": rule.action if isinstance(rule.action, str) else rule.action.value,
                    }
                rules[rule_id] = rule_dict

        # Load rules into matcher
        if rules:
            self.dynamic_matcher.load_rules(rules)

        # 2. Get current page analysis items and convert to matcher format
        # DynamicMatcher expects: type, text, index, coordinate_x, coordinate_y
        items = []
        page_analysis = self.context.current_page_analysis
        if page_analysis and hasattr(page_analysis, "items") and page_analysis.items:
            for idx, item in enumerate(page_analysis.items):
                # Extract type value (handle enum)
                item_type = item.type.value if hasattr(item.type, "value") else str(item.type)

                # Extract coordinates
                coord_x = 0.5
                coord_y = 0.5
                if hasattr(item, "coordinate") and item.coordinate:
                    coord_x = getattr(item.coordinate, "x", 0.5) or 0.5
                    coord_y = getattr(item.coordinate, "y", 0.5) or 0.5

                items.append({
                    "type": item_type,
                    "text": getattr(item, "name", ""),  # matcher expects "text", not "name"
                    "index": idx,
                    "coordinate_x": coord_x,
                    "coordinate_y": coord_y,
                })

        # 3. Match and instantiate children
        results = self.dynamic_matcher.match_all(items, parent_node=node)
        children = []

        for r in results:
            if r.matched and r.action == MatchAction.GENERATE_CHILD:
                # Instantiate with parent_path for concatenation
                child = self.dynamic_matcher.instantiate_match(r)
                if child:
                    # V6.9.3: Ensure precondition exists for path concatenation
                    # If template doesn't define precondition, create a minimal one
                    if not child.precondition:
                        from src.graph.node import Precondition
                        child.precondition = Precondition(
                            page_name=None,  # No page restriction
                            timeout_seconds=5.0,
                        )

                    # V6.9.3: For dynamic children, don't set page_name precondition
                    # This allows execution regardless of current page
                    # Instead, rely on path matching and element presence
                    if child.precondition.page_name is None:
                        child.precondition.page_name = None  # Explicitly None

                    # V6.9.2: Record lifecycle event for node creation
                    self._record_dynamic_lifecycle(
                        event="created",
                        node_id=child.node_id,
                        parent_id=node.node_id,
                        match_rule_id=getattr(r, 'rule_id', None),
                        element_id=getattr(r, 'element_id', None),
                    )

                    # V6.9: Concatenate path
                    child.precondition.path = list(self.context.current_path) + [child.name]
                    # Register child
                    self._node_registry[child.node_id] = child
                    children.append(child)
            else:
                # Record skipped item for debugging
                self._record_skip_span(r)

        # 4. Cache generated children
        self._dynamic_children[node.node_id] = children

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

    def invalidate_children_cache(self, node_id: str) -> None:
        """
        Invalidate cached children for a node.

        V6.9: Called when page changes to trigger regeneration of dynamic children.
        """
        self._dynamic_children.pop(node_id, None)

    # ========================================================================
    # Trace Span Generation (V6.3)
    # ========================================================================

    def _record_skip_span(self, match_result) -> None:
        """
        Record a skipped element span for debugging.

        V6.9: Records items that didn't match any rule or had non-generate actions.
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        from src.graph.matcher import MatchAction

        # Determine skip reason
        if not match_result.matched:
            reason = "no_match"
        elif match_result.action != MatchAction.GENERATE_CHILD:
            reason = f"action_{match_result.action.value}"
        else:
            reason = "unknown"

        # Get item info
        item_info = {}
        if match_result.menu_item:
            item_info = {
                "type": match_result.menu_item.get("type"),
                "text": match_result.menu_item.get("text"),
                "index": match_result.menu_item.get("index"),
            }

        span = SpanNode(
            span_type="dynamic_matching",
            action="skip_element",
            status="skipped",
            target=item_info.get("text"),
            metadata={
                "reason": reason,
                "item": item_info,
            },
        )
        self.trace_recorder.record_span(span)

    def _record_metrics_as_spans(self, metrics: Optional[Dict[str, Any]]) -> None:
        """Convert handler metrics dict to SpanNode and write to TraceRecorder.

        V6.9.3: Handle both single metric dict and list of metrics (from retry loops).
        """
        if not metrics:
            return
        if "ai_call" in metrics:
            ai = metrics["ai_call"]
            # Handle list case (from precondition_check retries)
            if isinstance(ai, list):
                # Record only the last ai_call (most recent)
                if ai:
                    ai = ai[-1]
                else:
                    ai = None
            if ai:
                self.trace_recorder.record_span(SpanNode(
                    span_type="ai_call",
                    capability=ai.get("capability", "vision"),
                    provider_id=ai.get("provider_id"),
                    success=ai.get("success", True),
                    latency_ms=ai.get("latency_ms", 0),
                    input_tokens=ai.get("input_tokens"),
                    output_tokens=ai.get("output_tokens"),
                    page_id=ai.get("page_id"),
                    element_count=ai.get("element_count"),
                ))
        if "execution" in metrics:
            ex = metrics["execution"]
            # Handle list case (from precondition_check retries)
            if isinstance(ex, list):
                # Record all executions in order
                for exec_item in ex:
                    self.trace_recorder.record_span(SpanNode(
                        span_type="execution",
                        action=exec_item.get("action", "unknown"),
                        status=exec_item.get("status", "success"),
                        target=exec_item.get("target"),
                        duration_ms=exec_item.get("duration_ms"),
                    ))
            elif ex:
                # Single execution metric
                self.trace_recorder.record_span(SpanNode(
                    span_type="execution",
                    action=ex.get("action", "unknown"),
                    status=ex.get("status", "success"),
                    target=ex.get("target"),
                    duration_ms=ex.get("duration_ms"),
                ))
        if "error" in metrics:
            err = metrics["error"]
            self.trace_recorder.record_span(SpanNode(
                span_type="error",
                error_type=err.get("error_type", "UnknownError"),
                error_message=err.get("error_message", ""),
                severity=err.get("severity", "error"),
            ))

    # V6.9.2: Page and action recording for trace visualization -------------------

    def _record_page_analysis(self, page_analysis: Any) -> None:
        """Record a page snapshot with element tree.

        Args:
            page_analysis: PageAnalysis object with items list
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        # Extract element tree from PageAnalysis.items
        elements = []
        try:
            if hasattr(page_analysis, "items"):
                for item in page_analysis.items:
                    # Check if item has coordinate attribute
                    coord_x, coord_y = 0.5, 0.5
                    if hasattr(item, "coordinate") and item.coordinate:
                        if hasattr(item.coordinate, "x"):
                            coord_x = item.coordinate.x
                        elif isinstance(item.coordinate, dict):
                            coord_x = item.coordinate.get("x", 0.5)

                    elements.append({
                        "name": item.name if hasattr(item, "name") else str(item),
                        "type": item.type.value if hasattr(item.type, "value") else str(item.type),
                        "coordinate": {
                            "x": coord_x,
                            "y": coord_y,
                        },
                        "expected_action": item.expected_action.value if hasattr(item, "expected_action") and hasattr(item.expected_action, "value") else "",
                    })
        except Exception as e:
            # If extraction fails, record error but continue
            import logging
            logging.getLogger(__name__).warning(f"Failed to extract elements: {e}")
            elements = []

        # Get current page info
        current_path = list(self.context.current_path) if self.context.current_path else []
        page_id = current_path[-1] if current_path else "unknown"

        span = SpanNode(
            span_type="page_snapshot",
            metadata={
                "page_id": page_id,
                "page_path": current_path,
                "timestamp": time.time(),
                "element_count": len(elements),
                "elements": elements,
            },
        )
        self.trace_recorder.record_span(span)

    def _record_action_execution(
        self,
        action: str,
        target: Any,
        success: bool,
        page_context: Optional[Dict[str, Any]] = None,
    ) -> None:
        """Record an action execution with page context.

        Args:
            action: Action type (click, back, swipe, etc.)
            target: Target element or coordinate
            success: Whether action succeeded
            page_context: Optional page analysis dict at time of action
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        # Extract element ID from target
        element_id = None
        if target:
            if isinstance(target, str):
                element_id = target
            elif isinstance(target, dict):
                element_id = target.get("element_id") or target.get("value")
            elif hasattr(target, "id"):
                element_id = getattr(target, "id", None)

        # Get current page context
        page_id = None
        page_elements = []
        if page_context:
            page_id = page_context.get("page_id")
            page_elements = page_context.get("elements", [])
        elif self.context.current_path:
            page_id = self.context.current_path[-1] if self.context.current_path else None
            # Try to get elements from current_page_analysis
            if hasattr(self.context, "current_page_analysis") and self.context.current_page_analysis:
                for item in self.context.current_page_analysis.items:
                    page_elements.append({
                        "name": item.name if hasattr(item, "name") else str(item),
                        "type": item.type.value if hasattr(item.type, "value") else str(item.type),
                    })

        span = SpanNode(
            span_type="action_execution",
            metadata={
                "action": action,
                "target": str(target) if target else None,
                "element_id": element_id,
                "success": success,
                "page_id": page_id,
                "page_elements": page_elements,
            },
        )
        self.trace_recorder.record_span(span)

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

    # V6.9.2: Enhanced trace recording -------------------------------------------

    def _record_page_transition(
        self,
        from_path: List[str],
        to_path: List[str],
        transition: Any,
    ) -> None:
        """Record a page_transition span if path changed.

        Args:
            from_path: Previous page path
            to_path: New page path
            transition: State machine transition object
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        # Only record if the path actually changed
        if not from_path or not to_path or from_path == to_path:
            return

        # Extract trigger info from transition metadata if available
        trigger_element = None
        trigger_action = None
        if hasattr(transition, 'metadata'):
            trigger_element = transition.metadata.get('trigger_element')
            trigger_action = transition.metadata.get('trigger_action')

        # Get from/to pages (last element of path)
        from_page = from_path[-1] if from_path else None
        to_page = to_path[-1] if to_path else None

        span = PageTransitionSpan(
            from_page=from_page,
            to_page=to_page,
            trigger_element=trigger_element,
            trigger_action=trigger_action,
        )
        self.trace_recorder.record_span(span)

    def _record_dynamic_lifecycle(
        self,
        event: str,
        node_id: str,
        parent_id: Optional[str] = None,
        match_rule_id: Optional[str] = None,
        element_id: Optional[str] = None,
    ) -> None:
        """Record a dynamic_lifecycle span.

        Args:
            event: Lifecycle event (created, matched, pushed, executed, popped)
            node_id: ID of the dynamic node
            parent_id: ID of the parent node
            match_rule_id: ID of the match rule that created the node
            element_id: ID of the element that matched
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        span = DynamicNodeLifecycleSpan(
            event=event,
            node_id=node_id,
            parent_id=parent_id,
            match_rule_id=match_rule_id,
            element_id=element_id,
        )
        self.trace_recorder.record_span(span)

    def _record_state_decision(
        self,
        current_state: str,
        decision: str,
        reason: str,
        context: Dict[str, Any],
    ) -> None:
        """Record a state_decision span.

        Args:
            current_state: Current state when decision was made
            decision: The decision that was made
            reason: Explanation of why the decision was made
            context: Additional context information
        """
        if not self.trace_recorder or not self.trace_recorder.trace_id:
            return

        span = StateDecisionSpan(
            current_state=current_state,
            decision=decision,
            reason=reason,
            context=context,
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
