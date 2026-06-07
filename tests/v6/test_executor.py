"""
Tests for V6 graph traversal engine.

Tests GraphTraversalEngine initialization, main loop, and helper methods.
"""

import time

from src.graph.node import (
    ChildrenStrategy,
    ChildrenStrategyType,
    CompletionPolicy,
    CompletionPolicyType,
    EntryPolicy,
    EntryStrategy,
    ExitCondition,
    ExitConditionType,
    FallbackAction,
    MatchMode,
    NodeType,
    Operation,
    TargetFoundAction,
    TraversalMode,
    TraversalNode,
)
from src.graph.plan import TraversalPlan
from src.state_machine.global_fsm import GlobalState
from src.traversal.graph_engine import (
    GraphTraversalEngine,
    TraversalResult,
    PageCacheInfo,
)
from src.trace.context import TraversalRuntimeContext as TraversalContext


# ============================================================================
# Mock Services for Testing
# ============================================================================


class MockVisionService:
    """Mock vision service for testing."""

    def __init__(self):
        self.call_count = 0
        self.current_screen = {"app_name": "TestApp", "elements": []}

    def analyze_screenshot(self):
        """Return mock screen analysis."""
        self.call_count += 1
        return self.current_screen


class MockActionExecutor:
    """Mock action executor for testing."""

    def __init__(self):
        self.action_history = []

    def tap(self, x, y):
        """Record tap action."""
        self.action_history.append({"action": "tap", "x": x, "y": y, "timestamp": time.time()})

    def swipe(self, start, end):
        """Record swipe action."""
        self.action_history.append({"action": "swipe", "start": start, "end": end})

    def press_back(self):
        """Record back action."""
        self.action_history.append({"action": "back", "timestamp": time.time()})

    def press_home(self):
        """Record home action."""
        self.action_history.append({"action": "home", "timestamp": time.time()})


# ============================================================================
# Test GraphTraversalEngine Creation (Tasks 3.1.1 - 3.1.3)
# ============================================================================


class TestGraphTraversalEngineCreation:
    """Tests for GraphTraversalEngine initialization."""

    def test_create_engine_with_plan(self):
        """Test creating engine with TraversalPlan."""
        plan = TraversalPlan(entry_app="TestApp")
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        assert engine.plan == plan
        assert engine.vision_service == vision
        assert engine.action_executor == action
        assert engine.context.global_state == GlobalState.IDLE

    def test_create_engine_with_exception_chain(self):
        """Test creating engine with exception chain."""
        plan = TraversalPlan(entry_app="TestApp")
        vision = MockVisionService()
        action = MockActionExecutor()
        exception_chain = None  # Mock chain

        engine = GraphTraversalEngine(plan, vision, action, exception_chain)

        assert engine.exception_chain == exception_chain
        assert engine.context.exception_chain == exception_chain

    def test_engine_creates_state_machine(self):
        """Test that engine creates state machine."""
        plan = TraversalPlan(entry_app="TestApp")
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        assert engine.state_machine is not None
        assert hasattr(engine.state_machine, "state")


# ============================================================================
# Test Initialization (Tasks 3.2.1 - 3.2.7)
# ============================================================================


class TestEngineInitialization:
    """Tests for engine initialization methods."""

    def test_initialize_sets_global_state(self):
        """Test that initialize sets global state to INITIALIZING then TRAVERSING."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.STATIC, static_children=[]),
            ),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        # V6.8: initialize() now returns None, raises exceptions on failure
        engine.initialize()

        assert engine.context.global_state == GlobalState.TRAVERSING

    def test_initialize_with_root_node(self):
        """Test that initialize pushes root node to stack."""
        from src.graph.node import ChildrenStrategy, ChildrenStrategyType

        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.STATIC, static_children=[]),
        )
        plan = TraversalPlan(
            entry_app="TestApp",
            root_node=root,
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)
        engine.initialize()

        assert "root" in engine.context.node_stack

    def test_entry_policy_cold_launch(self):
        """Test COLD_LAUNCH entry strategy."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.COLD_LAUNCH),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.STATIC, static_children=[]),
            ),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        # V6.8: initialize() returns None, raises exceptions on failure
        engine.initialize()
        assert engine.context.global_state == GlobalState.TRAVERSING

    def test_entry_policy_direct_deeplink(self):
        """Test DIRECT_DEEPLINK entry strategy."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.DIRECT_DEEPLINK),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.STATIC, static_children=[]),
            ),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)
        engine.initialize()
        assert engine.context.global_state == GlobalState.TRAVERSING

    def test_entry_policy_bind_current_screen(self):
        """Test BIND_CURRENT_SCREEN entry strategy."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=TraversalNode(
                node_id="root",
                name="Root",
                node_type=NodeType.CONTAINER,
                operation=Operation(action="no_action"),
                children_strategy=ChildrenStrategy(type=ChildrenStrategyType.STATIC, static_children=[]),
            ),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)
        engine.initialize()
        assert engine.context.global_state == GlobalState.TRAVERSING


# ============================================================================
# Test Main Loop (Tasks 3.3.1 - 3.3.6)
# ============================================================================


class TestMainLoop:
    """Tests for main execution loop."""

    def test_run_returns_result(self):
        """Test that run() returns TraversalResult."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        result = engine.run()

        assert isinstance(result, TraversalResult)
        assert result.status in (GlobalState.COMPLETED, GlobalState.ERROR)

    def test_run_records_elapsed_time(self):
        """Test that run() records execution time."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        result = engine.run()

        assert result.elapsed_seconds >= 0

    def test_run_counts_steps(self):
        """Test that run() counts state machine steps."""
        plan = TraversalPlan(
            entry_app="TestApp",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        result = engine.run()

        assert result.total_steps >= 0

    def test_run_tracks_visited_nodes(self):
        """Test that run() tracks visited nodes."""
        from src.graph.node import ChildrenStrategy, ChildrenStrategyType

        root = TraversalNode(
            node_id="root",
            name="Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(type=ChildrenStrategyType.STATIC),
        )
        plan = TraversalPlan(
            entry_app="TestApp",
            root_node=root,
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        )
        vision = MockVisionService()
        action = MockActionExecutor()

        engine = GraphTraversalEngine(plan, vision, action)

        result = engine.run()

        # Root node should be visited
        assert "root" in result.visited_nodes


# ============================================================================
# Test Depth Limit (Tasks 3.4.1 - 3.4.3)
# ============================================================================


class TestDepthLimit:
    """Tests for depth limiting functionality."""

    def test_context_has_max_depth(self):
        """Test that context has max_depth field."""
        context = TraversalContext(max_depth=10)
        assert context.max_depth == 10

    def test_context_get_current_depth(self):
        """Test getting current depth from stack."""
        context = TraversalContext()
        context.node_stack = ["node1", "node2", "node3"]

        assert context.get_current_depth() == 3

    def test_context_is_at_max_depth(self):
        """Test checking if at max depth."""
        context = TraversalContext(max_depth=2)
        context.node_stack = ["node1", "node2"]

        assert context.is_at_max_depth() is True

    def test_context_not_at_max_depth(self):
        """Test not at max depth."""
        context = TraversalContext(max_depth=5)
        context.node_stack = ["node1"]

        assert context.is_at_max_depth() is False


# ============================================================================
# Test Cache Management (Tasks 3.4.2 - 3.4.3)
# ============================================================================


class TestCacheManagement:
    """Tests for page cache management."""

    def test_update_page_cache(self):
        """Test updating page cache."""
        engine = GraphTraversalEngine(
            TraversalPlan(entry_app="TestApp"),
            MockVisionService(),
            MockActionExecutor(),
        )

        path = "Main/SubMenu"
        page_info = {"items": [{"name": "Button1"}], "hash": "abc123"}

        engine._update_page_cache(path, page_info)

        assert path in engine.context.page_cache
        assert engine.context.page_cache[path].items == page_info["items"]

    def test_restore_from_cache(self):
        """Test restoring from cache."""
        engine = GraphTraversalEngine(
            TraversalPlan(entry_app="TestApp"),
            MockVisionService(),
            MockActionExecutor(),
        )

        path = "Main/SubMenu"
        page_info = {"items": [{"name": "Button1"}], "hash": "abc123"}

        engine._update_page_cache(path, page_info)
        restored = engine._restore_from_cache(path)

        assert restored is not None
        assert restored["items"] == page_info["items"]
        assert restored["from_cache"] is True

    def test_restore_from_cache_miss(self):
        """Test cache miss."""
        engine = GraphTraversalEngine(
            TraversalPlan(entry_app="TestApp"),
            MockVisionService(),
            MockActionExecutor(),
        )

        restored = engine._restore_from_cache("nonexistent")

        assert restored is None


# ============================================================================
# Test Completion Policy (Tasks 3.6.1 - 3.6.3)
# ============================================================================


class TestCompletionPolicy:
    """Tests for completion policy checking."""

    def test_none_policy_never_completes_early(self):
        """Test NONE policy doesn't trigger early completion."""
        plan = TraversalPlan(
            entry_app="TestApp",
            completion_policy=CompletionPolicy(type=CompletionPolicyType.NONE),
        )
        engine = GraphTraversalEngine(
            plan,
            MockVisionService(),
            MockActionExecutor(),
        )

        assert engine._check_completion_policy() is False

    def test_target_found_policy_checks_visited(self):
        """Test TARGET_FOUND policy checks visited nodes."""
        plan = TraversalPlan(
            entry_app="TestApp",
            completion_policy=CompletionPolicy(
                type=CompletionPolicyType.TARGET_FOUND,
                target_name="Version",
            ),
        )
        engine = GraphTraversalEngine(
            plan,
            MockVisionService(),
            MockActionExecutor(),
        )

        # Initially no matches
        assert engine._check_completion_policy() is False

        # Add matching node
        engine.context.visited_nodes.add("version_node")

        # Still no match because node not in registry
        assert engine._check_completion_policy() is False

    def test_timeout_policy(self):
        """Test TIMEOUT policy."""
        plan = TraversalPlan(
            entry_app="TestApp",
            completion_policy=CompletionPolicy(
                type=CompletionPolicyType.TIMEOUT,
                timeout_seconds=1.0,
            ),
        )
        engine = GraphTraversalEngine(
            plan,
            MockVisionService(),
            MockActionExecutor(),
        )
        engine._start_time = time.time()

        # Initially not timed out
        assert engine._check_completion_policy() is False

        # Simulate timeout
        engine._start_time = time.time() - 2.0

        # Should trigger
        assert engine._check_completion_policy() is True

    def test_max_steps_policy(self):
        """Test MAX_STEPS policy."""
        plan = TraversalPlan(
            entry_app="TestApp",
            completion_policy=CompletionPolicy(
                type=CompletionPolicyType.MAX_STEPS,
                max_steps=10,
            ),
        )
        engine = GraphTraversalEngine(
            plan,
            MockVisionService(),
            MockActionExecutor(),
        )

        # Initially not at limit
        engine.context.step_count = 5
        assert engine._check_completion_policy() is False

        # At limit
        engine.context.step_count = 10
        assert engine._check_completion_policy() is True


# ============================================================================
# Test TraversalContext (Tasks 6.1.1 - 6.1.6)
# ============================================================================


class TestTraversalContext:
    """Tests for TraversalRuntimeContext (V6.3+)."""

    def test_context_has_all_v6_fields(self):
        """Test that context has all V6+ fields."""
        context = TraversalContext()
        assert hasattr(context, "page_cache")
        assert hasattr(context, "max_depth")
        assert hasattr(context, "step_count")
        assert hasattr(context, "visited_nodes")
        assert hasattr(context, "trace_id")

    def test_context_initial_values(self):
        """Test initial field values."""
        context = TraversalContext()
        assert context.page_cache == {}
        assert context.max_depth == 100
        assert context.step_count == 0
        assert context.visited_nodes == set()

    def test_context_record_action(self):
        """Test recording actions."""
        context = TraversalContext()
        context.record_action("tap", x=100, y=200)
        assert len(context.action_history) == 1
        assert context.action_history[0]["action"] == "tap"

    def test_context_action_history_limit(self):
        """Test action history is limited to 5 entries."""
        context = TraversalContext()
        for i in range(7):
            context.record_action(f"action_{i}")
        assert len(context.action_history) == 5


# ============================================================================
# Test PageCacheInfo
# ============================================================================


class TestPageCacheInfo:
    """Tests for PageCacheInfo data class."""

    def test_create_page_cache_info(self):
        """Test creating PageCacheInfo."""
        info = PageCacheInfo(items=[{"name": "Button1"}])

        assert len(info.items) == 1
        assert info.items[0]["name"] == "Button1"
        assert info.timestamp is not None
        assert info.screen_hash is None

    def test_page_cache_info_with_hash(self):
        """Test PageCacheInfo with screen hash."""
        info = PageCacheInfo(
            items=[{"name": "Button1"}],
            screen_hash="abc123",
        )

        assert info.screen_hash == "abc123"


# ============================================================================
# Test TraversalResult
# ============================================================================


class TestTraversalResult:
    """Tests for TraversalResult data class."""

    def test_create_result(self):
        """Test creating TraversalResult."""
        result = TraversalResult(
            status=GlobalState.COMPLETED,
            elapsed_seconds=1.5,
            total_steps=10,
            visited_nodes={"node1", "node2"},
            trace=[],
        )

        assert result.status == GlobalState.COMPLETED
        assert result.elapsed_seconds == 1.5
        assert result.total_steps == 10
        assert "node1" in result.visited_nodes

    def test_result_with_error(self):
        """Test result with error."""
        error = Exception("Test error")
        result = TraversalResult(
            status=GlobalState.ERROR,
            elapsed_seconds=0.5,
            total_steps=2,
            visited_nodes=set(),
            trace=[],
            error=error,
        )

        assert result.status == GlobalState.ERROR
        assert result.error == error
