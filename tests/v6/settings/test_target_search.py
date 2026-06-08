"""场景：目标搜索 — 找到 Dark mode 后立即停止."""

import json
import pytest
from pathlib import Path
from typing import Dict, Any, List

from src.graph.plan import TraversalPlan
from src.graph.node import (
    TraversalNode, NodeType, Operation, ChildrenStrategy, ChildrenStrategyType,
    DynamicRule, Precondition, ExitCondition, ExitConditionType, FallbackAction,
    CompletionPolicy, CompletionPolicyType, TargetFoundAction, MatchMode,
    EntryPolicy, EntryStrategy, ErrorPolicy
)
from src.simulation.state_fixture import StateFixture, PageState, PageElement, PageTransition
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor
from src.trace.storage import FileStorage
from src.trace.recorder import TraceRecorder
from src.traversal.graph_engine import GraphTraversalEngine
from src.state_machine.global_fsm import GlobalState


class TestTargetSearch:
    """目标搜索 — 找到 Dark mode 后立即停止，验证提前终止."""

    @pytest.fixture(autouse=True)
    def setup(self):
        # Load same fixture as baseline
        page_file = Path(__file__).parent / "settings_page.json"
        with open(page_file, "r") as f:
            settings_page_data = json.load(f)

        # Build StateFixture (same as test_settings_simulation.py)
        pages: Dict[str, PageState] = {}
        page_id_map: Dict[str, str] = {}
        for page_path, page_data in settings_page_data.items():
            page_id = page_path.strip("/").replace("/", "_")
            page_id_map[page_path] = page_id
            elements = []
            for elem in page_data.get("elements", []):
                bounds = elem.get("bounds", [0, 0, 500, 1080])
                x = (bounds[0] + bounds[2]) / 2 / 500
                y = (bounds[1] + bounds[3]) / 2 / 1080
                class_name = elem.get("class", "button")
                if "Switch" in class_name:
                    elem_type = "switch"
                elif "Button" in class_name:
                    elem_type = "button"
                elif "TextView" in class_name or "LinearLayout" in class_name:
                    elem_type = "menu_item"
                else:
                    elem_type = "button"
                elements.append(PageElement(
                    id=elem["id"], type=elem_type, text=elem.get("text", ""),
                    coordinate={"x": x, "y": y},
                    action_target=elem.get("action_target"),
                ))
            pages[page_id] = PageState(
                id=page_id,
                page_name=page_data.get("screen_info", {}).get("title", page_path),
                elements=elements, is_complete=False,
            )

        # Transitions from home page
        transitions: List[PageTransition] = []
        home_id = page_id_map.get("/settings/home", "settings_home")
        for elem in settings_page_data.get("/settings/home", {}).get("elements", [])[:4]:
            elem_text = elem.get("text", "").lower()
            for path_key, path_id in page_id_map.items():
                if elem_text in path_key.lower() and path_key != "/settings/home":
                    transitions.append(PageTransition(
                        id=f"{home_id}_to_{path_id}", trigger=elem["id"],
                        from_page=home_id, to_page=path_id, action="click",
                    ))
                    break

        self.fixture = StateFixture(
            pages=pages, transitions=transitions,
            initial_page_id=home_id, history_depth=10,
        )
        self.plan = self._build_target_search_plan()

    def _build_target_search_plan(self) -> TraversalPlan:
        root = TraversalNode(
            node_id="root",
            name="设置主页",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            precondition=Precondition(page_name="Settings"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={
                    "menu_rule": DynamicRule(
                        rule_id="menu_rule",
                        match_condition={"type": "menu_item"},
                        child_template="menu_container",
                    ),
                    "switch_rule": DynamicRule(
                        rule_id="switch_rule",
                        match_condition={"type": "switch"},
                        child_template="switch_leaf",
                    ),
                    "slider_rule": DynamicRule(
                        rule_id="slider_rule",
                        match_condition={"type": "slider"},
                        child_template="slider_leaf",
                    ),
                },
            ),
            exit_condition=ExitCondition(
                type=ExitConditionType.ALL_CHILDREN_VISITED,
                fallback=FallbackAction.AUTO_ESCAPE,
            ),
            error_policy=ErrorPolicy(on_error="skip", max_retries=1),
            meta={"max_depth": 10},
        )

        completion = CompletionPolicy(
            type=CompletionPolicyType.TARGET_FOUND,
            target_name="Dark mode",
            match_mode=MatchMode.EXACT,
            action_on_found=TargetFoundAction.MARK_AND_STOP,
        )

        return TraversalPlan(
            plan_name="Target Search - Dark Mode",
            plan_id="settings-target-search-dark-mode-v1",
            entry_app="com.example.settings",
            entry_policy=EntryPolicy(strategy=EntryStrategy.COLD_LAUNCH),
            root_node=root,
            completion_policy=completion,
            mode="hybrid",
        )

    def test_target_search_stops_at_dark_mode(self):
        """目标搜索：找到 Dark mode 后立即停止."""
        vision = StatefulMockVisionService(self.fixture)
        action = StatefulMockActionExecutor(vision)
        recorder = TraceRecorder(storage=FileStorage(base_dir=".traces"))

        engine = GraphTraversalEngine(
            plan=self.plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
            test_metadata={
                "test_name": "test_target_search_stops_at_dark_mode",
                "test_scenario": "target_search",
                "completion_policy": "TARGET_FOUND: Dark mode (EXACT, MARK_AND_STOP)",
                "expected_status": "GlobalState.COMPLETED",
                "expected_steps_max": 80,
            },
        )
        result = engine.run()

        # Build visited names from node registry
        visited_names = set()
        for node_id in result.visited_nodes:
            node = engine._node_registry.get(node_id)
            if node:
                visited_names.add(node.name)

        # --- assertions ---

        # 1. Completion — must be COMPLETED (target found triggers policy)
        assert result.status == GlobalState.COMPLETED, \
            f"Expected COMPLETED, got {result.status}"

        # 2. Depth-first order: menus before Display must have been served first
        assert "Wi-Fi" in visited_names, "Wi-Fi must be visited before target"
        assert "Bluetooth" in visited_names, "Bluetooth must be visited before target"

        # 3. Target and its parent must be visited
        assert any("Display" in n for n in visited_names), \
            f"Display (parent of target) not found in {visited_names}"
        assert any("Dark mode" in n for n in visited_names), \
            f"Dark mode (target) not found in {visited_names}"

        # 4. Target found early — siblings after Display must NOT be visited
        visited_str = " ".join(visited_names)
        assert "Storage" not in visited_str, \
            f"Storage must NOT be visited, got {visited_names}"
        assert "Apps" not in visited_str, \
            f"Apps must NOT be visited, got {visited_names}"

        # 5. Steps must be well under full-traversal baseline (118 steps)
        assert result.total_steps < 80, \
            f"Should stop quickly, got {result.total_steps} (full traversal = 118)"

        print(f"\n  Visited: {sorted(visited_names)}")
        print(f"  Steps: {result.total_steps}")
        print(f"  Status: {result.status}")
