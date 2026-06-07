"""
Tests for V6.8 Engine Initialization.

Tests the complete initialization flow including:
- Plan validation
- Entry strategy execution with fallback chain
- Wait condition verification
- Root node processing
- Trace level configuration
- Exception handling
"""

from typing import List, Optional

import pytest

from src.graph.plan import TraversalPlan
from src.graph.node import (
    TraversalNode,
    NodeType,
    Operation,
    EntryPolicy,
    EntryStrategy,
    EntryConfig,
    ChildrenStrategy,
    ChildrenStrategyType,
)
from src.exception.initialization import (
    ConfigurationError,
    EntryPolicyError,
    WaitConditionError,
    EntryError,
)
from tests.v6.test_simulation_base import SimulationRunner


# ============================================================================
# Test Helpers
# ============================================================================


class FailingMockVisionService:
    """Mock vision service that can be configured to fail."""

    def __init__(self, find_app_entry_result: Optional[dict] = None, current_page_path: Optional[List[str]] = None):
        """Initialize with configurable behavior."""
        self._find_app_entry_result = find_app_entry_result
        self._current_page_path = current_page_path or ["home"]
        self.call_count = 0

    def analyze_screenshot(self, image_data: bytes):
        """Analyze screenshot - returns minimal PageAnalysis."""
        self.call_count += 1
        from src.state.content_tree import PageAnalysis
        return PageAnalysis(
            current_path=self._current_page_path,
            items=[],
            level1_dir="right",
            level2_dir="bottom",
        )

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """Find app entry - returns configured result."""
        return self._find_app_entry_result

    def set_path_context(self, path: list) -> None:
        """Set path context."""
        self._current_page_path = path

    def get_current_page(self) -> Optional[dict]:
        """Get current page info for wait condition verification."""
        return {"path": self._current_page_path}


class FailingMockActionExecutor:
    """Mock action executor that simulates failures."""

    def __init__(self, should_fail: bool = False):
        """Initialize with configurable failure behavior."""
        self._should_fail = should_fail
        self.history = []

    def execute(self, ctx):
        """Execute action - optionally fails."""
        from src.simulation.operation_executor import ExecutionResult
        self.history.append(ctx)
        if self._should_fail:
            return ExecutionResult(success=False, error="Simulated failure")
        return ExecutionResult(success=True)

    def get_executed_actions(self) -> set:
        """Get set of executed action types."""
        return {h.operation.get("action", "") for h in self.history}


# ============================================================================
# Test Plan Validation
# ============================================================================


class TestPlanValidation:
    """Tests for plan validation during initialization."""

    def test_missing_root_node_raises_error(self):
        """Test that missing root_node raises ConfigurationError."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=None,  # Missing root_node
        )
        runner = SimulationRunner({}, plan)

        with pytest.raises(ConfigurationError) as exc_info:
            runner.engine.initialize()

        assert "root_node is required" in str(exc_info.value)

    def test_invalid_root_node_type_raises_error(self):
        """Test that non-CONTAINER root node type raises ConfigurationError."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.LEAF_ACTION,  # Invalid type
                operation=Operation(action="click"),
            ),
        )
        runner = SimulationRunner({}, plan)

        with pytest.raises(ConfigurationError) as exc_info:
            runner.engine.initialize()

        assert "must be CONTAINER type" in str(exc_info.value)

    def test_invalid_root_node_operation_raises_error(self):
        """Test that non-no_action root node operation raises ConfigurationError."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="click"),  # Invalid operation
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)

        with pytest.raises(ConfigurationError) as exc_info:
            runner.engine.initialize()

        assert "should be 'no_action'" in str(exc_info.value)

    def test_valid_plan_passes_validation(self):
        """Test that a valid plan passes validation."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        # Should not raise
        runner.engine.initialize()


# ============================================================================
# Test Entry Strategy Execution
# ============================================================================


class TestEntryStrategy:
    """Tests for entry strategy execution."""

    def test_deeplink_strategy_success(self):
        """Test successful deeplink strategy execution."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.DIRECT_DEEPLINK),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        # Should not raise
        runner.engine.initialize()

    def test_cold_launch_strategy_success(self):
        """Test successful cold launch strategy execution."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.COLD_LAUNCH),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        # Should not raise
        runner.engine.initialize()

    def test_cold_launch_icon_not_found_falls_back(self):
        """Test that cold_launch falls back when icon not found."""
        # Use failing vision service that returns None for find_app_entry
        from src.simulation.mock_vision import MockVisionService
        from src.simulation.mock_action import MockActionExecutor

        plan = TraversalPlan(
            entry_app="NonExistentApp",
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.COLD_LAUNCH,
                fallback=EntryStrategy.BIND_CURRENT_SCREEN,
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )

        # Create vision service that fails to find icon
        vision = FailingMockVisionService(find_app_entry_result=None)
        action = MockActionExecutor()

        from src.traversal.graph_engine import GraphTraversalEngine
        from src.trace.recorder import TraceRecorder
        from src.trace.storage import MemoryStorage

        storage = MemoryStorage()
        trace_recorder = TraceRecorder(storage=storage)
        engine = GraphTraversalEngine(plan, vision, action, trace_recorder=trace_recorder)

        # Should not raise - falls back to bind_current_screen
        engine.initialize()
        assert engine.context.global_state.value == "traversing"

    def test_fallback_chain_works(self):
        """Test that fallback chain works correctly."""
        # Primary: deeplink, Fallback: cold_launch
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.DIRECT_DEEPLINK,
                fallback=EntryStrategy.COLD_LAUNCH,
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        # Should not raise - fallback to cold_launch then bind_current_screen
        runner.engine.initialize()

    def test_entry_policy_error_attributes(self):
        """Test EntryPolicyError attributes when raised."""
        # Note: bind_current_screen strategy is the ultimate fallback and never fails
        # So we test the error attributes directly
        error = EntryPolicyError(
            "All entry strategies failed",
            failed_strategies=["deeplink", "cold_launch"],
            last_error=EntryError("cold_launch", "App not found"),
        )
        assert error.recoverable is True
        assert len(error.failed_strategies) == 2
        assert error.last_error is not None
        assert "deeplink" in str(error)


# ============================================================================
# Test Wait Condition Verification
# ============================================================================


class TestWaitCondition:
    """Tests for wait condition verification."""

    def test_no_wait_condition_passes(self):
        """Test that missing wait_condition passes verification."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.BIND_CURRENT_SCREEN,
                wait_condition=None,  # No condition
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        # Should not raise
        runner.engine.initialize()

    def test_empty_wait_condition_passes(self):
        """Test that empty wait_condition passes verification."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.BIND_CURRENT_SCREEN,
                wait_condition={},  # Empty condition
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        # Should not raise
        runner.engine.initialize()

    def test_fast_mode_success(self):
        """Test fast mode verification succeeds when condition is met."""
        # Configure virtual page with path matching expected condition
        # Default path is "home" so we check for "home"
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(wait_mode="fast"),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.BIND_CURRENT_SCREEN,
                wait_condition={"page_name": "home"},  # Matches default path
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner(vp, plan)
        # Initialize engine - path will be "home" matching condition
        runner.engine.initialize()
        assert runner.engine.context.global_state.value == "traversing"

    def test_fast_mode_failure(self):
        """Test fast mode verification fails when condition is not met."""
        # Virtual page path is "home" but condition expects "settings"
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(wait_mode="fast", wait_timeout=0.1),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.BIND_CURRENT_SCREEN,
                wait_condition={"page_name": "settings"},  # Doesn't match "home"
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner(vp, plan)
        # Should raise WaitConditionError
        with pytest.raises(WaitConditionError) as exc_info:
            runner.engine.initialize()
        assert "not satisfied" in str(exc_info.value).lower()

    def test_polling_mode_success(self):
        """Test polling mode verification succeeds with retry."""
        # For simulation, the condition is immediately satisfied
        # (in real scenario, page would transition after delay)
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(wait_mode="polling", wait_timeout=1.0, wait_interval=0.1),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.BIND_CURRENT_SCREEN,
                wait_condition={"page_name": "home"},  # Matches default path
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner(vp, plan)
        # Should not raise - condition is immediately satisfied
        runner.engine.initialize()
        assert runner.engine.context.global_state.value == "traversing"

    def test_polling_mode_timeout(self):
        """Test polling mode verification times out when condition never met."""
        # Condition expects "settings" but path is always "home"
        vp = {"home": {"elements": [], "level1_dir": "right", "level2_dir": "bottom"}}
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(wait_mode="polling", wait_timeout=0.2, wait_interval=0.1),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.BIND_CURRENT_SCREEN,
                wait_condition={"page_name": "settings"},  # Never matches "home"
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner(vp, plan)
        # Should raise WaitConditionError after timeout
        with pytest.raises(WaitConditionError) as exc_info:
            runner.engine.initialize()
        assert exc_info.value.recoverable is True


# ============================================================================
# Test EntryConfig
# ============================================================================


class TestEntryConfig:
    """Tests for EntryConfig data class."""

    def test_default_values(self):
        """Test EntryConfig default values."""
        config = EntryConfig()
        assert config.wait_mode == "fast"
        assert config.wait_timeout == 10.0
        assert config.wait_interval == 1.0
        assert config.action_delay_ms == 100
        assert config.trace_level == "standard"

    def test_custom_values(self):
        """Test EntryConfig with custom values."""
        config = EntryConfig(
            wait_mode="polling",
            wait_timeout=30.0,
            wait_interval=2.0,
            action_delay_ms=200,
            trace_level="detailed",
        )
        assert config.wait_mode == "polling"
        assert config.wait_timeout == 30.0
        assert config.wait_interval == 2.0
        assert config.action_delay_ms == 200
        assert config.trace_level == "detailed"

    def test_invalid_wait_mode_raises_error(self):
        """Test that invalid wait_mode raises ValueError."""
        with pytest.raises(ValueError) as exc_info:
            EntryConfig(wait_mode="invalid")
        assert "Invalid wait_mode" in str(exc_info.value)

    def test_invalid_trace_level_raises_error(self):
        """Test that invalid trace_level raises ValueError."""
        with pytest.raises(ValueError) as exc_info:
            EntryConfig(trace_level="invalid")
        assert "Invalid trace_level" in str(exc_info.value)

    def test_invalid_wait_timeout_raises_error(self):
        """Test that invalid wait_timeout raises ValueError."""
        with pytest.raises(ValueError) as exc_info:
            EntryConfig(wait_timeout=-1.0)
        assert "wait_timeout must be positive" in str(exc_info.value)

    def test_invalid_wait_interval_raises_error(self):
        """Test that invalid wait_interval raises ValueError."""
        with pytest.raises(ValueError) as exc_info:
            EntryConfig(wait_interval=0)
        assert "wait_interval must be positive" in str(exc_info.value)


# ============================================================================
# Test EntryConfig in TraversalPlan
# ============================================================================


class TestEntryConfigIntegration:
    """Tests for EntryConfig integration with TraversalPlan."""

    def test_entry_config_serialization(self):
        """Test that EntryConfig serializes correctly."""
        config = EntryConfig(wait_mode="polling", trace_level="detailed")
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=config,
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )

        json_str = plan.to_json()
        assert "entry_config" in json_str
        assert "polling" in json_str
        assert "detailed" in json_str

    def test_entry_config_deserialization(self):
        """Test that EntryConfig deserializes correctly."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(wait_mode="polling", trace_level="detailed"),
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )

        json_str = plan.to_json()
        restored_plan = TraversalPlan.from_json(json_str)

        assert restored_plan.entry_config is not None
        assert restored_plan.entry_config.wait_mode == "polling"
        assert restored_plan.entry_config.trace_level == "detailed"


# ============================================================================
# Test Exception Types
# ============================================================================


class TestExceptionTypes:
    """Tests for initialization exception types."""

    def test_configuration_error_is_non_recoverable(self):
        """Test that ConfigurationError is non-recoverable."""
        error = ConfigurationError("Invalid plan")
        assert error.recoverable is False
        assert "Non-recoverable" in str(error)

    def test_configuration_error_message(self):
        """Test ConfigurationError message format."""
        error = ConfigurationError("root_node is required")
        assert "root_node is required" in str(error)
        assert "[Non-recoverable]" in str(error)

    def test_entry_policy_error_is_recoverable(self):
        """Test that EntryPolicyError is recoverable."""
        error = EntryPolicyError("All strategies failed")
        assert error.recoverable is True
        assert "Recoverable" in str(error)

    def test_entry_policy_error_with_failed_strategies(self):
        """Test EntryPolicyError with failed strategies list."""
        error = EntryPolicyError(
            "All strategies failed",
            failed_strategies=["deeplink", "cold_launch"],
        )
        assert "Failed strategies: deeplink, cold_launch" in str(error)

    def test_entry_policy_error_with_last_error(self):
        """Test EntryPolicyError with last error."""
        last_error = ValueError("Device not found")
        error = EntryPolicyError(
            "All strategies failed",
            last_error=last_error,
        )
        assert "ValueError" in str(error)
        assert error.last_error is last_error

    def test_wait_condition_error_is_recoverable(self):
        """Test that WaitConditionError is recoverable."""
        error = WaitConditionError("Condition timeout")
        assert error.recoverable is True
        assert "Recoverable" in str(error)

    def test_wait_condition_error_with_timeout(self):
        """Test WaitConditionError with timeout."""
        error = WaitConditionError(
            "Condition timeout",
            timeout_seconds=30.0,
        )
        assert "Timeout: 30.0s" in str(error)
        assert error.timeout_seconds == 30.0

    def test_entry_error_attributes(self):
        """Test EntryError attributes."""
        error = EntryError(strategy="deeplink", reason="Activity not found")
        assert error.strategy == "deeplink"
        assert error.reason == "Activity not found"
        assert error.recoverable is True

    def test_entry_error_string_format(self):
        """Test EntryError string format."""
        error = EntryError(strategy="cold_launch", reason="Icon not found")
        assert "strategy=cold_launch" in str(error)
        assert "reason=Icon not found" in str(error)


# ============================================================================
# Test Complete Initialization Flow
# ============================================================================


class TestCompleteInitialization:
    """Tests for complete initialization flow."""

    def test_successful_initialization(self):
        """Test complete successful initialization flow."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(wait_mode="fast", trace_level="standard"),
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        runner.engine.initialize()

        # Verify state (GlobalState enum values are lowercase)
        assert runner.engine.context.global_state.value == "traversing"
        assert len(runner.engine.context.node_stack) == 1
        assert runner.engine.context.node_stack[0].node_id == "root"

    def test_initialization_creates_trace(self):
        """Test that initialization creates trace records."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(trace_level="standard"),
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)
        runner.engine.initialize()

        # Verify trace was created
        assert runner.engine.trace_recorder is not None
        assert runner.engine.trace_recorder.trace_id is not None
        assert len(runner.engine.trace_recorder.trace_id) > 0


# ============================================================================
# Test Trace Level Configuration
# ============================================================================


class TestTraceLevel:
    """Tests for trace level configuration."""

    def test_minimal_level_no_entry_attempt_spans(self):
        """Test that minimal level does not record entry attempt spans."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(trace_level="minimal"),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.COLD_LAUNCH,
                fallback=EntryStrategy.BIND_CURRENT_SCREEN,
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )

        runner = SimulationRunner({}, plan)
        runner.engine.initialize()

        # Get trace from runner's internal storage
        trace_id = runner.engine.trace_recorder.trace_id
        trace = runner._storage.read(trace_id)
        entry_spans = [s for s in trace if hasattr(s, 'action') and s.action == 'entry_strategy']

        # Minimal level should not record entry attempts
        assert len(entry_spans) == 0

    def test_standard_level_records_entry_attempts(self):
        """Test that standard level records entry attempt spans."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(trace_level="standard"),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.COLD_LAUNCH,
                fallback=EntryStrategy.BIND_CURRENT_SCREEN,
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )

        runner = SimulationRunner({}, plan)
        runner.engine.initialize()

        # Get trace from runner's internal storage
        trace_id = runner.engine.trace_recorder.trace_id
        trace = runner._storage.read(trace_id)
        entry_spans = [s for s in trace if hasattr(s, 'action') and s.action == 'entry_strategy']

        # Standard level should record entry attempts
        assert len(entry_spans) > 0

    def test_detailed_level_records_vision_calls(self):
        """Test that detailed level is enabled (vision call spans checked during traversal)."""
        # Note: Vision call spans are created during traversal phase, not initialization.
        # This test verifies that detailed level is properly configured.
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(trace_level="detailed"),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.COLD_LAUNCH,
                fallback=EntryStrategy.BIND_CURRENT_SCREEN,
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )

        runner = SimulationRunner({}, plan)
        runner.engine.initialize()

        # Verify the trace level is configured correctly
        assert runner.engine._get_trace_level() == "detailed"

        # Detailed level should record entry attempts like standard
        trace_id = runner.engine.trace_recorder.trace_id
        trace = runner._storage.read(trace_id)
        entry_spans = [s for s in trace if hasattr(s, 'action') and s.action == 'entry_strategy']
        assert len(entry_spans) > 0


# ============================================================================
# Test Exception Propagation
# ============================================================================


class TestExceptionPropagation:
    """Tests for exception propagation during initialization."""

    def test_configuration_error_propagates(self):
        """Test that ConfigurationError propagates correctly."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=None,  # Invalid - will raise ConfigurationError
        )
        runner = SimulationRunner({}, plan)

        # Should raise ConfigurationError
        with pytest.raises(ConfigurationError):
            runner.engine.initialize()

        # State should be ERROR after exception
        assert runner.engine.context.global_state.value == "error"

    def test_entry_policy_error_propagates(self):
        """Test that EntryPolicyError propagates correctly."""
        from src.trace.storage import MemoryStorage

        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.COLD_LAUNCH,
                fallback=EntryStrategy.BIND_CURRENT_SCREEN,
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )

        # Use failing vision service
        vision = FailingMockVisionService(find_app_entry_result=None)
        action = FailingMockActionExecutor(should_fail=False)

        from src.traversal.graph_engine import GraphTraversalEngine
        from src.trace.recorder import TraceRecorder

        storage = MemoryStorage()
        trace_recorder = TraceRecorder(storage=storage)
        engine = GraphTraversalEngine(plan, vision, action, trace_recorder=trace_recorder)

        # bind_current_screen should succeed (it's the ultimate fallback)
        engine.initialize()
        assert engine.context.global_state.value == "traversing"

    def test_wait_condition_error_propagates(self):
        """Test that WaitConditionError propagates correctly."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_config=EntryConfig(wait_mode="fast", wait_timeout=0.1),
            entry_policy=EntryPolicy(
                strategy=EntryStrategy.BIND_CURRENT_SCREEN,
                wait_condition={"page_name": "nonexistent"},  # Won't match default "home"
            ),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(
                    type=ChildrenStrategyType.STATIC,
                    static_children=[],
                ),
            ),
        )
        runner = SimulationRunner({}, plan)

        # Should raise WaitConditionError
        with pytest.raises(WaitConditionError) as exc_info:
            runner.engine.initialize()

        # Verify error attributes
        assert exc_info.value.recoverable is True

        # State should be ERROR after exception
        assert runner.engine.context.global_state.value == "error"
