"""Core traversal engine for automated UI exploration."""

import logging
import time
from dataclasses import dataclass
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Any, Callable, Optional, Dict, List, Tuple

from ..adb.adb_client import ADBClient
from ..exception import (
    ExceptionAction,
    ExceptionContext,
    ExceptionHandlingChain,
    ExceptionHandlingResult,
    ExceptionHistory,
    ExceptionSeverity,
    RecoveryAction,
    TraversalException,
)
from ..state.content_tree import (
    ContentNode,
    Coordinate,
    ExpectedAction,
    MenuInfo,
    MenuItem,
    PageAnalysis,
    TraversalState,
    VisitFingerprint,
)
from ..vision.vision_service import VisionService

# AI Strategy Advisor imports (V5.0)
from ..ai import AIStrategyAdvisor, NoOpAIAdvisor, DecisionResult
from ..safety import SafetyFilter
from ..context import TraversalContext
from ..ai.cache import AIResponseCache, DebounceTracker

logger = logging.getLogger(__name__)


class ClickResult(str, Enum):
    """Result of a click operation."""

    NO_CHANGE = "no_change"
    POPUP = "popup"
    PAGE_JUMP = "page_jump"
    NORMAL = "normal"
    NO_FEEDBACK = "no_feedback"
    ERROR = "error"


@dataclass
class TraversalEvent:
    """Event emitted during traversal."""

    event_type: str
    step: int
    data: dict

    def __str__(self) -> str:
        """String representation."""
        return f"[{self.event_type}] Step {self.step}: {self.data}"


@dataclass
class TraversalConfig:
    """Configuration for traversal behavior."""

    max_steps: int = 200
    wait_time: float = 0.5
    max_retries: int = 2
    timeout: int = 30
    save_screenshots: bool = True
    screenshot_dir: Optional[str] = None
    skip_readonly: bool = True  # Skip read-only elements during traversal

    # Exception handling configuration
    enable_exception_handling: bool = True  # Enable/disable exception handling
    exception_max_retries: int = 3  # Max retries for exception handling
    exception_history_max_records: int = 1000  # Max exception history records
    recovery_timeout: float = 10.0  # Timeout for recovery actions (seconds)
    verbose_exception_logging: bool = False  # Enable verbose exception logging

    # Graph mode configuration (V4.0)
    use_graph_mode: bool = False  # Enable graph-based traversal mode
    template_registry_path: Optional[str] = None  # Path to template registry JSON
    max_stack_depth: int = 10  # Maximum depth for node stack

    # Trace configuration (V4.0)
    trace_enabled: bool = False  # Enable trace recording
    trace_output_path: Optional[str] = None  # Path for trace output
    trace_keep_count: int = 10  # Number of traces to keep
    trace_snapshot_interval: int = 10  # Steps between state snapshots

    # AI Strategy Advisor configuration (V5.0)
    enable_ai_advisor: bool = False  # Enable AI strategy advisor
    ai_call_timeout: float = 30.0  # Timeout for AI calls in seconds
    ai_min_confidence: float = 0.7  # Minimum confidence threshold for AI decisions
    ai_cache_ttl: int = 300  # AI response cache TTL in seconds (default 5 min)


class TraversalEngine:
    """Main engine for UI traversal and exploration.

    This class implements the core traversal logic following the PRD:
    1. Navigate to target app
    2. Analyze and cache menu structure
    3. Systematically traverse all items
    4. Handle popups, jumps, and state recovery

    Integrated with exception handling system for robust error recovery.
    """

    def __init__(
        self,
        adb_client: ADBClient,
        vision_service: VisionService,
        state: TraversalState,
        config: TraversalConfig,
        event_callback: Optional[Callable[[TraversalEvent], None]] = None,
    ):
        """Initialize traversal engine.

        Args:
            adb_client: ADB client for device control
            vision_service: Vision service for screen analysis
            state: Traversal state to use
            config: Traversal configuration
            event_callback: Optional callback for traversal events
        """
        self.adb = adb_client
        self.vision = vision_service
        self.state = state
        self.config = config
        self.event_callback = event_callback
        self._step = 0

        # Exception handling (tasks 7.1)
        if config.enable_exception_handling:
            self.exception_chain = self._build_exception_chain()
            self.exception_history = ExceptionHistory(
                max_records=config.exception_history_max_records
            )
        else:
            self.exception_chain = None
            self.exception_history = None

        # Graph mode components (V4.0)
        self.use_graph_mode = config.use_graph_mode
        if self.use_graph_mode:
            self._init_graph_mode()

        # AI Strategy Advisor components (V5.0) - Tasks 5.1-5.7
        if config.enable_ai_advisor:
            self.ai_advisor: AIStrategyAdvisor = NoOpAIAdvisor()  # Default, can be overridden
            self.safety_filter = SafetyFilter(enable_audit_log=True)
            self.ai_cache = AIResponseCache(maxsize=100, ttl_seconds=config.ai_cache_ttl)
            self.debounce_tracker = DebounceTracker()
        else:
            self.ai_advisor = None
            self.safety_filter = None
            self.ai_cache = None
            self.debounce_tracker = None

    def _init_graph_mode(self) -> None:
        """Initialize graph mode components."""
        from ..graph.template import TemplateRegistry
        from ..graph.matcher import DynamicMatcher
        from ..state_machine import StateMachineOrchestrator
        from ..trace import TraceRecorder, TraceConfig

        # Initialize template registry
        if self.config.template_registry_path:
            from pathlib import Path
            self.template_registry = TemplateRegistry(custom_path=Path(self.config.template_registry_path))
        else:
            self.template_registry = TemplateRegistry()

        # Initialize dynamic matcher
        self.dynamic_matcher = DynamicMatcher(self.template_registry)

        # Initialize state machine orchestrator
        self.state_machine = StateMachineOrchestrator(max_stack_depth=self.config.max_stack_depth)

        # Initialize trace recorder
        if self.config.trace_enabled:
            trace_config = TraceConfig(
                enabled=True,
                output_path=Path(self.config.trace_output_path) if self.config.trace_output_path else None,
                keep_count=self.config.trace_keep_count,
                snapshot_interval=self.config.trace_snapshot_interval,
            )
            self.trace_recorder = TraceRecorder(trace_config)
        else:
            self.trace_recorder = None

    # AI Strategy Advisor helper methods (V5.0) - Tasks 5.1-5.7

    def set_ai_advisor(self, advisor: AIStrategyAdvisor) -> None:
        """Set the AI advisor implementation.

        Args:
            advisor: AI advisor instance to use
        """
        self.ai_advisor = advisor

    def _build_traversal_context(self) -> TraversalContext:
        """Build TraversalContext from current state.

        Task 5.6: Build read-only context for AI calls.

        Returns:
            TraversalContext with current runtime state
        """
        from datetime import datetime

        # Build context from TraversalState
        context = TraversalContext(
            node_stack=[],  # Could be populated from state machine if available
            current_path=list(self.state.current_path),
            visited_pages=set(self.state.visited.keys()) if self.state.visited else set(),
            failed_nodes={},  # Could be populated from exception history
            action_history=[],  # Recent actions
            inference_history=[],  # Recent container inferences
            goal_attempts={},  # Goal attempt tracking
        )
        return context

    def _call_ai_with_validation(
        self,
        ai_method: Callable,
        node_data: dict,
        ui_hash: str = "default",
        path_hash: str = "default",
    ) -> Optional[dict]:
        """Call AI method with safety validation.

        Task 5.5: Validate AI output through SafetyFilter before execution.

        Args:
            ai_method: AI advisor method to call
            node_data: Raw node data from AI
            ui_hash: Hash of UI state for caching
            path_hash: Hash of path for caching

        Returns:
            Validated node data or None if rejected
        """
        if not self.ai_advisor or not self.safety_filter:
            return None

        # Validate through safety filter
        context = {"current_path": list(self.state.current_path)}
        safety_result = self.safety_filter.validate(node_data, context)

        if not safety_result.is_safe:
            logger.warning(f"AI output rejected: {safety_result.reason}")
            self._emit("ai_output_rejected", {
                "reason": safety_result.reason,
                "fallback": safety_result.fallback_node,
            })
            return safety_result.fallback_node

        return node_data

    def _handle_ai_exception_fallback(self) -> Tuple[DecisionResult, None]:
        """Handle AI call failure with degradation.

        Task 5.7: Return UNSURE on AI failure for rule engine fallback.

        Returns:
            Tuple of (UNSURE, None)
        """
        logger.warning("AI call failed, returning UNSURE for rule engine fallback")
        self._emit("ai_call_failed", {"fallback": "UNSURE"})
        return (DecisionResult.UNSURE, None)

    def _emit(self, event_type: str, data: dict) -> None:
        """Emit a traversal event."""
        if self.event_callback:
            event = TraversalEvent(event_type=event_type, step=self._step, data=data)
            self.event_callback(event)

    def _capture_and_analyze(self) -> PageAnalysis:
        """Capture screenshot and analyze with AI."""
        screenshot = self.adb.capture_screenshot()
        analysis = self.vision.analyze_screenshot(screenshot)

        self._emit("page_analyzed", {
            "current_path": analysis.current_path,
            "items_count": len(analysis.items),
            "is_popup": analysis.is_popup,
        })

        return analysis

    def _wait(self) -> None:
        """Wait after UI actions."""
        time.sleep(self.config.wait_time)

    # Exception handling methods (tasks 7.2-7.9)

    def _build_exception_chain(self) -> ExceptionHandlingChain:
        """Build the default exception handling chain.

        Returns:
            ExceptionHandlingChain with handlers in priority order
        """
        return ExceptionHandlingChain.create_default(
            adb_client=self.adb,
            max_retries=self.config.exception_max_retries,
        )

    def _get_severity(self, exception: TraversalException) -> ExceptionSeverity:
        """Get severity from exception instance.

        Args:
            exception: TraversalException instance

        Returns:
            ExceptionSeverity value
        """
        if hasattr(exception, "severity"):
            return exception.severity
        return ExceptionSeverity.ERROR  # Default to ERROR

    def execute_with_exception_handling(
        self, operation: Callable, **context
    ) -> Optional[Any]:
        """Execute operation with exception handling wrapper.

        Wraps the operation in try-except and processes any TraversalException
        through the exception handling chain.

        Args:
            operation: Callable to execute
            **context: Additional context for exception handling

        Returns:
            Result of operation, or None if skipped/backtracked

        Raises:
            TraversalException: If handler returns TERMINATE action
        """
        if not self.config.enable_exception_handling or not self.exception_chain:
            # Exception handling disabled, execute directly
            return operation()

        max_attempts = 4  # 1 initial + 3 retries (task 7.11)

        for attempt in range(max_attempts):
            try:
                return operation()

            except TraversalException as e:
                # Build exception context
                exc_context = ExceptionContext(
                    exception=e,
                    severity=self._get_severity(e),
                    state=self.state,
                    node=None,  # Could be enhanced to include current node
                    operation=context.get("operation", "unknown"),
                    timestamp=datetime.now(),
                    retry_count=attempt,
                )

                # Record in history
                if self.exception_history:
                    self.exception_history.record(exc_context)

                # Emit exception event
                self._emit("exception_occurred", {
                    "exception_type": type(e).__name__,
                    "severity": exc_context.severity.value,
                    "operation": exc_context.operation,
                    "retry_count": attempt,
                })

                # Process through handler chain
                result = self.exception_chain.handle(exc_context)

                if self.config.verbose_exception_logging:
                    logger.info(f"Exception handling result: {result.action.value} - {result.message}")

                # Execute based on action (tasks 7.5-7.9)
                if result.action == ExceptionAction.RETRY:
                    # Task 7.5: RETRY - increment retry_count and continue
                    continue

                elif result.action == ExceptionAction.SKIP:
                    # Task 7.6: SKIP - return None, continue
                    self._emit("operation_skipped", {"message": result.message})
                    return None

                elif result.action == ExceptionAction.BACKTRACK:
                    # Task 7.7: BACKTRACK - call _backtrack, return None
                    self._emit("operation_backtrack", {"message": result.message})
                    self._backtrack()
                    return None

                elif result.action == ExceptionAction.RECOVER:
                    # Task 7.8: RECOVER - call _recover, continue/retry
                    recovery_start_time = time.time()
                    self._emit("recovery_start", {
                        "action": result.recovery_action.value if result.recovery_action else None,
                        "message": result.message,
                    })
                    recovery_success = self._recover(result.recovery_action)
                    recovery_duration = time.time() - recovery_start_time

                    if recovery_success:
                        # Task 9.7: Include recovery action and duration in events
                        self._emit("recovery_success", {
                            "action": result.recovery_action.value,
                            "duration": round(recovery_duration, 2),
                        })
                        continue  # Retry after successful recovery
                    else:
                        self._emit("recovery_failed", {
                            "action": result.recovery_action.value,
                            "duration": round(recovery_duration, 2),
                        })
                        # Recovery failed, will re-raise below

                elif result.action == ExceptionAction.TERMINATE:
                    # Task 7.9: TERMINATE - raise original exception
                    self._emit("traversal_terminated", {"message": result.message})
                    raise

                elif result.action == ExceptionAction.IGNORE:
                    # IGNORE - continue as if nothing happened
                    self._emit("exception_ignored", {"message": result.message})
                    return None

        # Max attempts reached without success
        # Task 5.4: Try AI advisor as last resort before giving up
        if self.ai_advisor and self.config.enable_ai_advisor:
            logger.info("Exception chain exhausted, trying AI advisor fallback")
            try:
                # Build context for AI
                context = self._build_traversal_context()
                exc_dict = {
                    "type": "TraversalException",
                    "message": "Exception chain exhausted",
                    "attempts": max_attempts,
                }

                # Get current UI for AI (simplified - would capture screenshot in production)
                ui_info = PageAnalysis(
                    level1_dir=Direction.LEFT,
                    level1_menus=[],
                    level2_dir=Direction.TOP,
                    level2_menus=[],
                    current_path=list(self.state.current_path),
                    items=[],
                )

                # Call AI exception handler (timeout handled by decorator when implemented)
                result, node_data = self.ai_advisor.handle_exception(
                    exc_dict, ui_info, context
                )

                # Task 5.5: Validate AI output through safety filter
                if node_data and self.safety_filter:
                    safety_result = self.safety_filter.validate(
                        node_data, {"current_path": list(self.state.current_path)}
                    )
                    if not safety_result.is_safe:
                        logger.warning(f"AI fallback rejected: {safety_result.reason}")
                        self._emit("ai_fallback_rejected", {"reason": safety_result.reason})
                        node_data = safety_result.fallback_node

                # If AI provided recovery action, try to execute it
                if result == DecisionResult.SUCCESS and node_data:
                    self._emit("ai_fallback_success", {"node": node_data})
                    action = node_data.get("action", "no_action")
                    if action == "back":
                        self.adb.press_back()
                        self._wait()
                        return None
                    elif action == "no_action":
                        return None

            except Exception as ai_error:
                logger.error(f"AI fallback failed: {ai_error}")
                self._emit("ai_fallback_error", {"error": str(ai_error)})
                # Task 5.7: Fall through to raise exception on AI failure

        raise TraversalException(f"Operation failed after {max_attempts} attempts")

    def _backtrack(self) -> None:
        """Navigate back to previous position in traversal.

        Task 7.7: BACKTRACK action logic.
        Presses back button and waits for navigation to complete.
        """
        logger.info("Backtracking to previous position")
        self.adb.press_back()
        self._wait()

    def _recover(self, recovery_action: Optional[RecoveryAction]) -> bool:
        """Execute recovery action.

        Task 8.1: Main recovery dispatcher.
        Routes to specific recovery implementations.

        Args:
            recovery_action: RecoveryAction to execute

        Returns:
            True if recovery succeeded, False otherwise
        """
        if recovery_action is None:
            logger.warning("No recovery action specified")
            return False

        logger.info(f"Executing recovery action: {recovery_action.value}")

        try:
            if recovery_action == RecoveryAction.RECONNECT_ADB:
                return self._recover_reconnect_adb()
            elif recovery_action == RecoveryAction.RESTART_APP:
                return self._recover_restart_app()
            elif recovery_action == RecoveryAction.CLOSE_POPUP:
                return self._recover_close_popup()
            elif recovery_action == RecoveryAction.NAVIGATE_BACK:
                return self._recover_navigate_back()
            elif recovery_action == RecoveryAction.WAIT_AND_RETRY:
                return self._recover_wait_and_retry()
            elif recovery_action == RecoveryAction.IGNORE_UI_CHANGE:
                return self._recover_ignore_ui_change()
            else:
                logger.warning(f"Unknown recovery action: {recovery_action}")
                return False

        except Exception as e:
            logger.error(f"Recovery action {recovery_action.value} failed: {e}")
            return False

    def _recover_reconnect_adb(self) -> bool:
        """Reconnect ADB connection.

        Task 8.2: RECONNECT_ADB recovery.
        - Call adb.reconnect()
        - Wait for connection
        - Verify connection active

        Returns:
            True if reconnection succeeded
        """
        logger.info("Reconnecting ADB...")
        try:
            self.adb.reconnect()
            time.sleep(2.0)  # Wait for connection to establish

            # Verify connection is active
            if self.adb.is_connected():
                logger.info("ADB reconnected successfully")
                return True
            else:
                logger.error("ADB reconnection failed: not connected")
                return False

        except Exception as e:
            logger.error(f"ADB reconnection error: {e}")
            return False

    def _recover_restart_app(self) -> bool:
        """Restart the target application.

        Task 8.3: RESTART_APP recovery.
        - Stop app via adb
        - Start app via adb
        - Wait for app ready
        - Navigate to last position if possible

        Returns:
            True if restart succeeded
        """
        logger.info("Restarting app...")
        try:
            app_name = self.state.target_app
            if not app_name:
                logger.error("No target app configured for restart")
                return False

            # Stop the app
            self.adb.stop_app(app_name)
            time.sleep(1.0)

            # Start the app
            self.adb.start_app(app_name)
            time.sleep(3.0)  # Wait for app to initialize

            logger.info("App restarted successfully")

            # Task 8.3.4: Navigate to last position if possible
            if self.state.current_path and len(self.state.current_path) > 0:
                logger.info(f"Navigating to last position: {' -> '.join(self.state.current_path)}")
                try:
                    # Capture initial screen to find menu items
                    screenshot = self.adb.capture_screenshot()
                    analysis = self.vision.analyze_screenshot(screenshot)

                    # Navigate through each level in the path
                    for i, level_name in enumerate(self.state.current_path):
                        if i == 0:
                            # Find and click level1 menu
                            for menu in analysis.level1_menus:
                                if menu.name == level_name and not menu.active:
                                    self._tap_and_wait(menu.coordinate)
                                    logger.info(f"Navigated to level1: {level_name}")
                                    time.sleep(0.5)
                                    break
                        elif i == 1:
                            # Re-analyze after level1 navigation
                            screenshot = self.adb.capture_screenshot()
                            analysis = self.vision.analyze_screenshot(screenshot)

                            # Find and click level2 menu
                            for menu in analysis.level2_menus:
                                if menu.name == level_name and not menu.active:
                                    self._tap_and_wait(menu.coordinate)
                                    logger.info(f"Navigated to level2: {level_name}")
                                    time.sleep(0.5)
                                    break

                    logger.info("Successfully navigated to last position")
                except Exception as e:
                    logger.warning(f"Failed to navigate to last position: {e}")

            return True

        except Exception as e:
            logger.error(f"App restart error: {e}")
            return False

    def _recover_close_popup(self) -> bool:
        """Close detected popup.

        Task 8.4: CLOSE_POPUP recovery.
        - Analyze current screen for popup
        - Identify close button
        - Click close button
        - Wait for popup dismiss

        Returns:
            True if popup closed successfully
        """
        logger.info("Closing popup...")
        try:
            # Capture current screen
            screenshot = self.adb.capture_screenshot()
            analysis = self.vision.analyze_screenshot(screenshot)

            if analysis.close_button:
                # Click the close button
                self.adb.tap(analysis.close_button.x, analysis.close_button.y)
                time.sleep(0.5)
                logger.info("Popup closed successfully")
                return True
            else:
                # No close button found, try back button
                self.adb.press_back()
                time.sleep(0.5)
                logger.info("Popup closed via back button")
                return True

        except Exception as e:
            logger.error(f"Popup close error: {e}")
            return False

    def _recover_navigate_back(self) -> bool:
        """Navigate back to previous page.

        Task 8.5: NAVIGATE_BACK recovery.
        - Press back button via adb
        - Wait for navigation
        - Verify position changed

        Returns:
            True if navigation succeeded
        """
        logger.info("Navigating back...")
        try:
            before_path = self.state.current_path.copy()

            self.adb.press_back()
            time.sleep(1.0)

            # Analyze new position
            screenshot = self.adb.capture_screenshot()
            analysis = self.vision.analyze_screenshot(screenshot)

            # Update state
            self.state.current_path = analysis.current_path

            # Verify position changed
            if analysis.current_path != before_path:
                logger.info(f"Navigated back to: {' -> '.join(analysis.current_path)}")
                return True
            else:
                logger.warning("Navigation back didn't change position")
                return False

        except Exception as e:
            logger.error(f"Navigate back error: {e}")
            return False

    def _recover_wait_and_retry(self) -> bool:
        """Wait before retrying.

        Task 8.6: WAIT_AND_RETRY recovery.
        - Wait for configured duration (default 1.0s)
        - Return True to signal retry should proceed

        Returns:
            True (always succeeds)
        """
        wait_time = 1.0  # Default wait time
        logger.info(f"Waiting {wait_time}s before retry...")
        time.sleep(wait_time)
        return True

    def _recover_ignore_ui_change(self) -> bool:
        """Log and continue ignoring UI change.

        Task 8.7: IGNORE_UI_CHANGE recovery.
        - Log the UI change
        - Return True to continue normally

        Returns:
            True (always succeeds)
        """
        logger.info("Ignoring UI change, continuing traversal")
        return True

    def sync_exception_history(self) -> None:
        """Sync exception history to state for persistence.

        Task 10.2: Save exception history to state file.
        Converts ExceptionHistory records to dict format for storage.
        """
        if not self.exception_history:
            return

        # Convert ExceptionContext objects to dicts
        self.state.exception_history_records = [
            ctx.to_dict() for ctx in self.exception_history.records
        ]

    def load_exception_history(self) -> None:
        """Load exception history from state.

        Task 10.3: Load exception history from state file on resume.
        Restores ExceptionHistory from stored dict records.
        """
        if not self.state.exception_history_records:
            return

        # This would require reconstructing ExceptionContext from dicts
        # For now, the records remain in state.exception_history_records
        # and can be queried via the state methods
        logger.info(f"Loaded {len(self.state.exception_history_records)} exception history records")

    def get_exception_statistics(self) -> dict:
        """Get exception statistics from history.

        Task 10.5: Statistics endpoint.
        Returns combined statistics from both in-memory history and state.
        """
        # Prefer in-memory history if available
        if self.exception_history:
            return self.exception_history.get_statistics()

        # Fallback to state records
        return self.state.get_exception_history_summary()

    def _get_wait_time(self, item: MenuItem) -> float:
        """Get wait time based on button type/expected action.

        Args:
            item: MenuItem to determine wait time for

        Returns:
            Wait time in seconds based on expected_action
        """
        if item.expected_action == ExpectedAction.NAVIGATE:
            # Page navigation needs longer wait
            return max(self.config.wait_time, 1.0)
        elif item.expected_action == ExpectedAction.TOGGLE:
            # Toggle/switch changes are instant
            return min(self.config.wait_time, 0.3)
        elif item.expected_action == ExpectedAction.NONE:
            # Read-only elements need minimal wait for verification
            return 0.1
        else:
            # Default to configured wait time
            return self.config.wait_time

    def _tap_and_wait(self, coord: Coordinate, item: Optional[MenuItem] = None) -> None:
        """Tap at coordinate and wait.

        Args:
            coord: Coordinate to tap
            item: Optional MenuItem for determining wait time
        """
        self.adb.tap(coord.x, coord.y)

        # Use calculated wait time if item provided, otherwise use default
        if item:
            wait_time = self._get_wait_time(item)
            time.sleep(wait_time)
        else:
            self._wait()

    # Task 7.10: Wrapped key operations with exception handling
    def _capture_and_analyze_safe(self) -> Optional[PageAnalysis]:
        """Capture screenshot and analyze with exception handling.

        Returns:
            PageAnalysis if successful, None if operation failed/skipped
        """
        return self.execute_with_exception_handling(
            operation=self._capture_and_analyze,
            context={"operation_name": "capture_and_analyze"}
        )

    def _tap_and_wait_safe(self, coord: Coordinate, item: Optional[MenuItem] = None) -> bool:
        """Tap at coordinate and wait with exception handling.

        Args:
            coord: Coordinate to tap
            item: Optional MenuItem for determining wait time

        Returns:
            True if successful, False if operation failed/skipped
        """
        result = self.execute_with_exception_handling(
            operation=lambda: self._tap_and_wait(coord, item),
            context={"operation_name": "tap_and_wait"}
        )
        return result is not False  # Returns True unless wrapped operation failed

    def navigate_to_app_safe(self, target: str) -> bool:
        """Navigate to target app with exception handling.

        Args:
            target: App name to find

        Returns:
            True if successful, False otherwise
        """
        result = self.execute_with_exception_handling(
            operation=lambda: self.navigate_to_app(target),
            context={"operation_name": "navigate_to_app"}
        )
        return result is True

    def initialize_structure_safe(self) -> bool:
        """Analyze and cache initial structure with exception handling.

        Returns:
            True if successful, False otherwise
        """
        result = self.execute_with_exception_handling(
            operation=self.initialize_structure,
            context={"operation_name": "initialize_structure"}
        )
        return result is True

    def navigate_to_app(self, target: str) -> bool:
        """Navigate to target app from home screen.

        Args:
            target: App name to find

        Returns:
            True if successful
        """
        self._emit("navigate_start", {"target": target})

        screenshot = self.adb.capture_screenshot()
        entry = self.vision.find_app_entry(screenshot, target)

        if not entry:
            self._emit("navigate_failed", {"target": target, "reason": "not_found"})
            return False

        self._tap_and_wait(Coordinate(x=entry["x"], y=entry["y"]))
        self._emit("navigate_success", {"target": target})

        return True

    def initialize_structure(self) -> bool:
        """Analyze and cache initial structure.

        This follows PRD Stage 1:
        1. Analyze current page
        2. Cache all level1 menus
        3. Cache level2 menus for current level1
        4. Navigate to first level1, first level2

        Returns:
            True if successful
        """
        self._emit("initialize_start", {})

        analysis = self._capture_and_analyze()

        # Cache all level1 menus
        for menu in analysis.level1_menus:
            self.state.add_level1_menu(menu)

        # Set current path from AI analysis
        self.state.current_path = analysis.current_path.copy()

        # Cache level2 menus for current level1
        if len(analysis.current_path) >= 1:
            level1 = analysis.current_path[0]
            self.state.add_level2_menus(level1, analysis.level2_menus)

        # Cache items for current location
        cache_key = self.state.get_current_cache_key()
        self.state.add_items(cache_key, analysis.items)

        # Build initial tree structure
        self._build_tree_from_analysis(analysis)

        # Navigate to first level1 if needed
        if analysis.level1_menus and not analysis.level1_menus[0].active:
            first_l1 = analysis.level1_menus[0]
            self._tap_and_wait(first_l1.coordinate)
            self._wait()  # Extra wait for menu transition

        # Navigate to first level2 if exists
        if analysis.level2_menus and not analysis.level2_menus[0].active:
            first_l2 = analysis.level2_menus[0]
            self._tap_and_wait(first_l2.coordinate)
            self._wait()

        # Update state after navigation
        self.state.current_path = [
            analysis.level1_menus[0].name if analysis.level1_menus else analysis.current_path[0],
            analysis.level2_menus[0].name if analysis.level2_menus else analysis.current_path[1] if len(analysis.current_path) > 1 else "",
        ]
        self.state.current_phase = "traversing"

        self._emit("initialize_complete", {
            "level1_count": len(analysis.level1_menus),
            "level2_count": len(analysis.level2_menus),
            "items_count": len(analysis.items),
        })

        return True

    def _build_tree_from_analysis(self, analysis: PageAnalysis) -> None:
        """Build content tree from page analysis."""
        # Find or create parent nodes
        parent_id = "0"  # Root

        # Add level1 menu
        if analysis.current_path:
            level1 = analysis.current_path[0]
            l1_node = self.state.content_tree.add_node(
                title=level1,
                level=1,
                parent_id=parent_id,
                node_type="menu",
            )
            parent_id = l1_node.id

        # Add level2 menu
        if len(analysis.current_path) >= 2:
            level2 = analysis.current_path[1]
            l2_node = self.state.content_tree.add_node(
                title=level2,
                level=2,
                parent_id=parent_id,
                node_type="tab",
            )
            parent_id = l2_node.id

        # Add items
        for item in analysis.items:
            self.state.content_tree.add_node(
                title=item.name,
                level=3,
                parent_id=parent_id,
                node_type="item",
                coordinate=item.coordinate,
            )

    def _select_next_item(self) -> Optional[MenuItem]:
        """Select next unvisited item from current location.

        Returns:
            Next item to visit, or None if all visited
        """
        cache_key = self.state.get_current_cache_key()
        items = self.state.get_items(cache_key)

        for item in items:
            # Skip read-only elements if configured
            if self.config.skip_readonly and (
                item.type == "readonly" or item.expected_action == ExpectedAction.NONE
            ):
                continue

            fingerprint = item.get_fingerprint(
                self.state.current_path[-2] if len(self.state.current_path) >= 2 else "",
                self.state.current_path[-1] if len(self.state.current_path) >= 1 else "",
            )
            if not self.state.is_visited(VisitFingerprint(
                level1=self.state.current_path[-2] if len(self.state.current_path) >= 2 else "",
                level2=self.state.current_path[-1] if len(self.state.current_path) >= 1 else "",
                item_name=item.name,
            )):
                return item

        return None

    def _click_item(self, item: MenuItem) -> ClickResult:
        """Click an item and determine the result.

        Args:
            item: MenuItem to click

        Returns:
            ClickResult indicating what happened
        """
        self._emit("click_start", {"item": item.name, "type": item.type, "expected_action": item.expected_action})

        # Take before screenshot for comparison
        before_analysis = self._capture_and_analyze()

        # Click the item with type-specific wait time
        self._tap_and_wait(item.coordinate, item)

        # Analyze after click
        after_analysis = self._capture_and_analyze()

        # Handle popup first (highest priority)
        if after_analysis.is_popup:
            self._handle_popup(after_analysis)
            return ClickResult.POPUP

        # Use action-based verification
        return self._verify_by_expected_action(item, before_analysis, after_analysis)

    def _verify_by_expected_action(
        self, item: MenuItem, before: PageAnalysis, after: PageAnalysis
    ) -> ClickResult:
        """Verify click result based on expected action.

        Args:
            item: MenuItem that was clicked
            before: PageAnalysis before click
            after: PageAnalysis after click

        Returns:
            ClickResult indicating what happened
        """
        if item.expected_action == ExpectedAction.NAVIGATE:
            return self._verify_navigate(item, before, after)
        elif item.expected_action == ExpectedAction.TOGGLE:
            return self._verify_toggle(item, before, after)
        else:
            return self._verify_generic(before, after)

    def _check_expected_behavior_violation(
        self, item: MenuItem, before: PageAnalysis, after: PageAnalysis
    ) -> None:
        """Check and emit event if actual behavior differs from expected.

        Args:
            item: MenuItem that was clicked
            before: PageAnalysis before click
            after: PageAnalysis after click
        """
        # Navigate action without path change
        if item.expected_action == ExpectedAction.NAVIGATE:
            if after.current_path == before.current_path:
                self._emit("expected_behavior_violation", {
                    "item": item.name,
                    "expected": "navigate",
                    "actual": "no_change",
                    "expected_path_change": True,
                    "actual_path_change": False,
                })

        # Toggle action with path change (unexpected side effect)
        elif item.expected_action == ExpectedAction.TOGGLE:
            if after.current_path != before.current_path:
                self._emit("expected_behavior_violation", {
                    "item": item.name,
                    "expected": "toggle",
                    "actual": "navigate",
                    "expected_path_change": False,
                    "actual_path_change": True,
                })

    def _verify_navigate(self, item: MenuItem, before: PageAnalysis, after: PageAnalysis) -> ClickResult:
        """Verify navigate-type click result.

        Args:
            item: MenuItem that was clicked
            before: PageAnalysis before click
            after: PageAnalysis after click

        Returns:
            ClickResult.PAGE_JUMP if path changed, NO_CHANGE otherwise
        """
        # Check for expected behavior violation
        self._check_expected_behavior_violation(item, before, after)

        if after.current_path != before.current_path:
            self._handle_page_jump(after)
            return ClickResult.PAGE_JUMP

        # Navigate action didn't cause navigation - unexpected
        return ClickResult.NO_CHANGE

    def _verify_toggle(self, item: MenuItem, before: PageAnalysis, after: PageAnalysis) -> ClickResult:
        """Verify toggle-type click result.

        Args:
            item: MenuItem that was clicked
            before: PageAnalysis before click
            after: PageAnalysis after click

        Returns:
            ClickResult.NORMAL if state changed, NO_CHANGE otherwise
        """
        # Check for expected behavior violation
        self._check_expected_behavior_violation(item, before, after)

        # Toggle should NOT cause path change
        if after.current_path != before.current_path:
            # Unexpected side effect - treat as page jump
            self._handle_page_jump(after)
            return ClickResult.PAGE_JUMP

        # Check for state change (items count or content)
        if self._has_state_changed(before, after):
            return ClickResult.NORMAL

        # No state change detected
        return ClickResult.NO_CHANGE

    def _verify_generic(self, before: PageAnalysis, after: PageAnalysis) -> ClickResult:
        """Verify generic action-type click result.

        Args:
            before: PageAnalysis before click
            after: PageAnalysis after click

        Returns:
            Appropriate ClickResult based on what occurred
        """
        # Check for path change
        if after.current_path != before.current_path:
            self._handle_page_jump(after)
            return ClickResult.PAGE_JUMP

        # Check for items/content change
        if len(after.items) != len(before.items):
            return ClickResult.NORMAL

        # No visible change
        return ClickResult.NO_CHANGE

    def _has_state_changed(self, before: PageAnalysis, after: PageAnalysis) -> bool:
        """Check if UI state changed (for toggle-type items).

        Args:
            before: PageAnalysis before click
            after: PageAnalysis after click

        Returns:
            True if state changed, False otherwise
        """
        # Simple check: items count changed
        if len(after.items) != len(before.items):
            return True

        # More detailed check: look for toggle state changes in items
        # Compare items with same name - check if their type/state differs
        after_items_by_name = {item.name: item for item in after.items}
        for before_item in before.items:
            after_item = after_items_by_name.get(before_item.name)
            if after_item and after_item.type != before_item.type:
                # Item type changed (e.g., switch state)
                return True

        return False

    def _handle_popup(self, analysis: PageAnalysis) -> None:
        """Handle popup by recording and closing."""
        self._emit("popup_detected", {"info": analysis.popup_info})

        # Record popup in tree as child of current location
        if len(self.state.current_path) >= 2:
            level1, level2 = self.state.current_path[-2], self.state.current_path[-1]
            cache_key = f"{level1}|{level2}"

            # Find parent node (current tab level 2)
            # For now, add popup to tree with proper metadata
            if analysis.popup_info:
                popup_title = analysis.popup_info.title or "弹窗"
                self.state.content_tree.add_child_node(
                    title=popup_title,
                    parent_id=self._find_current_tab_node_id(),
                    node_type="popup",
                    description=analysis.popup_info.content,
                )

        # Close popup
        if analysis.close_button:
            self._tap_and_wait(analysis.close_button)
        else:
            self.adb.press_back()
            self._wait()

    def _find_current_tab_node_id(self) -> str:
        """Find the current tab node ID in tree."""
        # Search for node matching current path
        if len(self.state.current_path) >= 2:
            level1, level2 = self.state.current_path[-2], self.state.current_path[-1]
            for node_id, node in self.state.content_tree.nodes.items():
                if node.title == level2 and node.level == 2:
                    return node_id
        return "0"  # Fallback to root

    def _handle_page_jump(self, analysis: PageAnalysis) -> None:
        """Handle page jump by recording and returning."""
        self._emit("page_jump", {"path": analysis.current_path})

        # Record jump in tree
        jump_target = " -> ".join(analysis.current_path) if analysis.current_path else "Unknown"
        self.state.content_tree.add_child_node(
            title=f"跳转到: {jump_target}",
            parent_id=self._find_current_tab_node_id(),
            node_type="jump",
        )

        # Return to previous page
        if analysis.back_button:
            self._tap_and_wait(analysis.back_button)
        else:
            self.adb.press_back()
            self._wait()

    def _handle_no_feedback(self, item: MenuItem) -> None:
        """Handle item with no feedback - strategy depends on button type.

        Args:
            item: MenuItem that produced no feedback
        """
        self._emit("no_feedback", {"item": item.name, "expected_action": item.expected_action})

        # Toggle-type items don't retry children - toggle is binary
        if item.expected_action == ExpectedAction.TOGGLE:
            # Mark as state unchanged
            self.state.content_tree.add_child_node(
                title=item.name,
                parent_id=self._find_current_tab_node_id(),
                node_type="no_feedback",
            )
            return

        # Navigate-type items might retry navigation
        if item.expected_action == ExpectedAction.NAVIGATE:
            # Navigation failed - mark and continue
            self.state.content_tree.add_child_node(
                title=item.name,
                parent_id=self._find_current_tab_node_id(),
                node_type="no_feedback",
            )
            return

        # Action-type items try children first
        child_clicked = False

        if not item.parent:
            # Look for child items in the current analysis
            cache_key = self.state.get_current_cache_key()
            items = self.state.get_items(cache_key)

            for child in items:
                if child.parent == item.name and child.coordinate:
                    # Try clicking the child
                    self._emit("trying_child", {"child": child.name})
                    self._tap_and_wait(child.coordinate, child)

                    # Check if something happened
                    new_analysis = self._capture_and_analyze()
                    if new_analysis.is_popup:
                        self._handle_popup(new_analysis)
                        child_clicked = True
                        break
                    elif new_analysis.current_path != self.state.current_path:
                        self._handle_page_jump(new_analysis)
                        child_clicked = True
                        break

        # If no child worked or no children exist, mark as no_feedback
        if not child_clicked:
            self.state.content_tree.add_child_node(
                title=item.name,
                parent_id=self._find_current_tab_node_id(),
                node_type="no_feedback",
            )

    def _switch_to_next_level2(self) -> bool:
        """Switch to next level2 tab.

        Returns:
            True if successful, False if no more tabs
        """
        if len(self.state.current_path) < 2:
            return False

        current_l1 = self.state.current_path[0]
        current_l2 = self.state.current_path[1]
        level2_menus = self.state.get_level2_menus(current_l1)

        # Find current tab index
        for i, menu in enumerate(level2_menus):
            if menu.name == current_l2:
                if i + 1 < len(level2_menus):
                    # Switch to next
                    next_menu = level2_menus[i + 1]
                    self._tap_and_wait(next_menu.coordinate)
                    self.state.current_path[1] = next_menu.name
                    return True
                break

        return False

    def _switch_to_next_level1(self) -> bool:
        """Switch to next level1 menu.

        Returns:
            True if successful, False if no more menus
        """
        if not self.state.current_path:
            return False

        current_l1 = self.state.current_path[0]
        level1_menus = list(self.state.all_level1_menus.values())

        # Find current menu index
        for i, menu in enumerate(level1_menus):
            if menu.name == current_l1:
                if i + 1 < len(level1_menus):
                    # Switch to next
                    next_menu = level1_menus[i + 1]
                    self._tap_and_wait(next_menu.coordinate)
                    self._wait()

                    # Analyze new menu's level2 tabs
                    analysis = self._capture_and_analyze()
                    self.state.add_level2_menus(next_menu.name, analysis.level2_menus)

                    # Update path
                    self.state.current_path = [next_menu.name]
                    if analysis.level2_menus:
                        self.state.current_path.append(analysis.level2_menus[0].name)

                    # Cache items for new location
                    cache_key = self.state.get_current_cache_key()
                    self.state.add_items(cache_key, analysis.items)

                    return True
                break

        return False

    def run_step(self) -> bool:
        """Execute one traversal step.

        Returns:
            True if traversal should continue, False if done
        """
        self._step += 1

        if self._step > self.config.max_steps:
            self._emit("max_steps_reached", {"step": self._step})
            return False

        self._emit("step_start", {"step": self._step})

        # Get next item
        item = self._select_next_item()

        if item is None:
            # No more items at current location - try moving
            self._emit("location_exhausted", {"path": self.state.current_path})

            # Try next level2
            if self._switch_to_next_level2():
                return True

            # Try next level1
            if self._switch_to_next_level1():
                return True

            # All done
            self._emit("traversal_complete", {"total_steps": self._step})
            return False

        # Click and handle
        result = self._click_item(item)

        # Mark visited
        fingerprint = VisitFingerprint(
            level1=self.state.current_path[-2] if len(self.state.current_path) >= 2 else "",
            level2=self.state.current_path[-1] if len(self.state.current_path) >= 1 else "",
            item_name=item.name,
        )
        self.state.mark_visited(fingerprint)

        # Handle different results
        if result == ClickResult.NO_CHANGE:
            self._handle_no_feedback(item)
        elif result == ClickResult.ERROR:
            self.state.consecutive_errors += 1
            if self.state.consecutive_errors >= 3:
                self._emit("too_many_errors", {"step": self._step})
                return False
        else:
            self.state.consecutive_errors = 0

        return True

    def run(self) -> dict:
        """Run complete traversal.

        Returns:
            Summary dict with results
        """
        self._emit("traversal_start", {"max_steps": self.config.max_steps})

        start_time = time.time()

        while self.run_step():
            pass

        elapsed = time.time() - start_time

        summary = {
            "total_steps": self._step,
            "elapsed_time": elapsed,
            "visited_count": len(self.state.visited),
            "final_path": self.state.current_path,
            "tree": self.state.content_tree.to_markdown(),
        }

        self._emit("traversal_finished", summary)

        return summary
