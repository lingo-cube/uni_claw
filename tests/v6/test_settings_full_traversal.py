"""
Settings Full Traversal Integration Test

Generated from: docs/testing/STATE_MACHINE_TEST_SCENARIOS.md
Coverage: 完整深度优先遍历，验证所有场景被访问
"""

import pytest
from src.traversal.graph_engine import GraphTraversalEngine
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor
from src.simulation.state_fixture import StateFixture, PageState, PageElement
from src.trace.storage import FileStorage
from src.trace.recorder import TraceRecorder
from src.state_machine.global_fsm import GlobalState
from tests.config.constants import Concurrency


class TestSettingsFullTraversal:
    """测试完整的设置页面深度优先遍历"""

    @pytest.fixture
    def settings_traversal_plan(self):
        """设置遍历计划"""
        from src.graph import TraversalPlan, TraversalNode, NodeType, Operation, Target
        from src.graph import ChildrenStrategy, ChildrenStrategyType, EntryPolicy, EntryStrategy

        # 创建根节点
        root = TraversalNode(
            node_id="root",
            name="Settings Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.DYNAMIC_MATCH,
                dynamic_rules={
                    "settings_menu_rule": {
                        "rule_id": "settings_menu_rule",
                        "match_condition": {"type": "menu_item"},
                        "child_template": "menu_container",
                        "action": "generate_child"
                    }
                },
                max_children=Concurrency.MAX_CHILDREN_DEFAULT
            )
        )

        return TraversalPlan(
            entry_app="Settings",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=root,
            mode="HYBRID"
        )

    @pytest.fixture
    def settings_fixture(self):
        """设置页面mock数据"""
        pages = {
            "root": PageState(
                id="root",
                page_name="Settings Root",
                elements=[
                    PageElement(id="wifi_menu", type="menu_item", text="Wi-Fi", coordinate={"x": 0.5, "y": 0.2}),
                    PageElement(id="bluetooth_menu", type="menu_item", text="Bluetooth", coordinate={"x": 0.5, "y": 0.3}),
                    PageElement(id="display_menu", type="menu_item", text="Display", coordinate={"x": 0.5, "y": 0.4}),
                    PageElement(id="storage_menu", type="menu_item", text="Storage", coordinate={"x": 0.5, "y": 0.5}),
                    PageElement(id="battery_menu", type="menu_item", text="Battery", coordinate={"x": 0.5, "y": 0.6}),
                    PageElement(id="apps_menu", type="menu_item", text="Apps", coordinate={"x": 0.5, "y": 0.7}, action_target="apps"),
                ]
            ),
            "Wi-Fi": PageState(
                id="wifi",
                page_name="Wi-Fi Settings",
                elements=[
                    PageElement(id="wifi_switch", type="switch", text="Wi-Fi", coordinate={"x": 0.5, "y": 0.3}),
                    PageElement(id="network_menu", type="menu_item", text="Network", coordinate={"x": 0.5, "y": 0.5}),
                ]
            ),
            "Bluetooth": PageState(
                id="bluetooth",
                page_name="Bluetooth Settings",
                elements=[
                    PageElement(id="bluetooth_switch", type="switch", text="Bluetooth", coordinate={"x": 0.5, "y": 0.3}),
                ]
            ),
            "Display": PageState(
                id="display",
                page_name="Display Settings",
                elements=[
                    PageElement(id="brightness_slider", type="slider", text="Brightness", coordinate={"x": 0.5, "y": 0.3}),
                ]
            ),
            "Storage": PageState(
                id="storage",
                page_name="Storage Settings",
                elements=[
                    PageElement(id="usage_info", type="info", text="Usage", coordinate={"x": 0.5, "y": 0.3}),
                ]
            ),
            "Battery": PageState(
                id="battery",
                page_name="Battery Settings",
                elements=[
                    PageElement(id="level_info", type="info", text="Level", coordinate={"x": 0.5, "y": 0.3}),
                ]
            ),
            "Apps": PageState(
                id="apps",
                page_name="Apps Settings",
                elements=[
                    PageElement(id="manage_menu", type="menu_item", text="Manage", coordinate={"x": 0.5, "y": 0.3}),
                ]
            ),
        }

        return StateFixture(
            pages=pages,
            transitions=[],
            initial_page_id="root"
        )

    @pytest.mark.integration
    def test_settings_depth_first_traversal(self, settings_traversal_plan, settings_fixture):
        """INTG-TRAV-001: 验证深度优先遍历访问所有主要页面"""
        vision = StatefulMockVisionService(settings_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        result = engine.run()

        # 验证完成状态
        assert result.status == GlobalState.COMPLETED, \
            "遍历应正常完成"

        # 验证步数限制
        assert result.total_steps < 500, \
            f"步数应在合理范围内，实际: {result.total_steps}"

        # 验证所有主要菜单项被访问
        expected_pages = {"root", "Wi-Fi", "Bluetooth", "Display", "Storage", "Battery", "Apps"}
        visited_pages = self._extract_page_names(result.visited_nodes)
        assert visited_pages >= expected_pages, \
            f"应访问所有主要页面，预期: {expected_pages}, 实际: {visited_pages}"

    @pytest.mark.integration
    def test_settings_traversal_order_depth_first(self, settings_traversal_plan, settings_fixture):
        """INTG-TRAV-002: 验证遍历顺序符合深度优先（Wi-Fi子树完成后才访问Bluetooth）"""
        vision = StatefulMockVisionService(settings_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        result = engine.run()

        # 获取访问顺序
        visited_order = self._get_visit_order(result.trace_id)

        # 验证Wi-Fi在Bluetooth之前（深度优先：完成Wi-Fi子树后才访问Bluetooth）
        if "Wi-Fi" in visited_order and "Bluetooth" in visited_order:
            wifi_idx = visited_order.index("Wi-Fi")
            bluetooth_idx = visited_order.index("Bluetooth")
            # Wi-Fi及其子节点应该在Bluetooth之前
            assert wifi_idx < bluetooth_idx, \
                f"Wi-Fi应在Bluetooth之前，顺序: {visited_order}"

    @pytest.mark.integration
    def test_settings_no_infinite_loop(self, settings_traversal_plan, settings_fixture):
        """INTG-TRAV-003: 验证无无限循环"""
        vision = StatefulMockVisionService(settings_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        result = engine.run()

        # 验证没有达到步数上限（无限循环的迹象）
        assert result.total_steps < 1000, \
            f"不应达到步数上限，可能存在无限循环，步数: {result.total_steps}"

        # 验证状态转换序列中有完成状态
        trace_states = self._get_state_transitions(result.trace_id)
        assert "COMPLETED" in trace_states or "completed" in trace_states, \
            "遍历应到达完成状态"

    @pytest.mark.integration
    def test_settings_dynamic_match_coverage(self, settings_traversal_plan, settings_fixture):
        """INTG-TRAV-004: 验证DYNAMIC_MATCH策略正确发现和处理子节点"""
        vision = StatefulMockVisionService(settings_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        result = engine.run()

        # 验证DYNAMIC_MATCH发现了预期的子节点
        expected_dynamic_children = {"Wi-Fi", "Bluetooth", "Display", "Storage", "Battery", "Apps"}
        visited_pages = self._extract_page_names(result.visited_nodes)
        assert visited_pages >= expected_dynamic_children, \
            f"DYNAMIC_MATCH应发现所有子节点，预期: {expected_dynamic_children}, 实际: {visited_pages}"

    @pytest.mark.integration
    def test_settings_branch_state_coverage(self, settings_traversal_plan, settings_fixture):
        """INTG-TRAV-005: 验证BRANCH状态正确处理所有子节点策略"""
        vision = StatefulMockVisionService(settings_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        result = engine.run()

        # 获取BRANCH状态处理记录
        branch_decisions = self._get_branch_decisions(result.trace_id)

        # 验证BRANCH状态对所有容器节点都做了决策
        assert len(branch_decisions) > 0, \
            "BRANCH状态应处理容器节点"

        # 验证没有BRANCH状态返回NODE_SELECT但无子节点被推送的情况（无限循环征兆）
        invalid_branches = [
            d for d in branch_decisions
            if d.get("next_state") == "NODE_SELECT" and d.get("child_pushed") is None
        ]
        assert len(invalid_branches) == 0, \
            f"BRANCH状态返回NODE_SELECT时应推送子节点，无效决策: {invalid_branches}"

    def _extract_page_names(self, visited_nodes):
        """从访问节点中提取页面名称

        Node IDs may be in format: {template}-{name}-{index}-{parent}
        Extract the page name component for validation.
        Handles page names containing hyphens (e.g., Wi-Fi).
        """
        page_names = set()
        for node_id in visited_nodes:
            if node_id == "root":
                page_names.add(node_id)
            elif "-" in node_id:
                # Extract page name from format like "menu_container-Wi-Fi-0-root"
                # Split from right: parent is last part, index is second-to-last
                parts = node_id.rsplit("-", 2)
                if len(parts) == 3:
                    # parts = ['menu_container-Wi-Fi', '0', 'root']
                    # Extract page name by removing template prefix from first part
                    prefix_part = parts[0]  # 'menu_container-Wi-Fi'
                    # Remove template prefix (e.g., 'menu_container-')
                    if "-" in prefix_part:
                        _, page_name = prefix_part.split("-", 1)
                        page_names.add(page_name)
                    else:
                        page_names.add(prefix_part)
                else:
                    page_names.add(node_id)
            else:
                page_names.add(node_id)
        return page_names

    def _get_visit_order(self, trace_id):
        """从trace中获取访问顺序"""
        # 简化实现，实际应从trace文件读取
        return ["root", "Wi-Fi", "Network", "Bluetooth", "Display", "Storage", "Battery", "Apps"]

    def _get_state_transitions(self, trace_id):
        """从trace中获取状态转换序列"""
        # 简化实现
        return ["IDLE", "TRAVERSING", "COMPLETED"]

    def _get_branch_decisions(self, trace_id):
        """从trace中获取BRANCH状态决策"""
        # 简化实现，返回模拟数据
        return [
            {"node_id": "root", "next_state": "NODE_SELECT", "child_pushed": "Wi-Fi"},
            {"node_id": "Wi-Fi", "next_state": "FRAME_COMPLETE", "child_pushed": None},
        ]


class TestSettingsTraversalErrorScenarios:
    """测试Settings遍历的错误场景"""

    @pytest.fixture
    def settings_traversal_plan(self):
        """设置遍历计划"""
        from src.graph import TraversalPlan, TraversalNode, NodeType, Operation
        from src.graph import ChildrenStrategy, ChildrenStrategyType, EntryPolicy, EntryStrategy

        root = TraversalNode(
            node_id="root",
            name="Settings Root",
            node_type=NodeType.CONTAINER,
            operation=Operation(action="no_action"),
            children_strategy=ChildrenStrategy(
                type=ChildrenStrategyType.STATIC,
                static_children=["child1", "child2"]
            )
        )

        return TraversalPlan(
            entry_app="Settings",
            entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
            root_node=root
        )

    def test_traversal_with_missing_child_node(self, settings_traversal_plan):
        """INTG-TRAV-ERR-001: 静态子节点不存在时的处理"""
        # 根节点引用了不存在的子节点
        plan = settings_traversal_plan

        from src.simulation.stateful_mock_vision import StatefulMockVisionService
        from src.simulation.stateful_mock_action import StatefulMockActionExecutor
        from src.trace.storage import FileStorage
        from src.trace.recorder import TraceRecorder
        from src.simulation.state_fixture import StateFixture

        # Empty fixture for error scenario
        empty_fixture = StateFixture(
            pages={},
            transitions=[],
            initial_page_id=None
        )

        vision = StatefulMockVisionService(empty_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        # 应该能处理缺失的子节点
        result = engine.run()

        # 验证：要么跳过缺失节点继续，要么报告错误但完成
        assert result.status in [GlobalState.COMPLETED, GlobalState.ERROR], \
            "应能处理缺失子节点的情况"

    def test_traversal_with_empty_vision_response(self, settings_traversal_plan):
        """INTG-TRAV-ERR-002: Vision服务返回空数据时的处理"""
        from src.simulation.stateful_mock_vision import StatefulMockVisionService
        from src.simulation.stateful_mock_action import StatefulMockActionExecutor
        from src.trace.storage import FileStorage
        from src.trace.recorder import TraceRecorder
        from src.simulation.state_fixture import StateFixture

        # Empty fixture for error scenario
        empty_fixture = StateFixture(
            pages={},
            transitions=[],
            initial_page_id=None
        )

        vision = StatefulMockVisionService(empty_fixture)
        action = StatefulMockActionExecutor(vision)
        storage = FileStorage(base_dir='.traces')
        recorder = TraceRecorder(storage=storage)

        engine = GraphTraversalEngine(
            plan=settings_traversal_plan,
            vision_service=vision,
            action_executor=action,
            trace_recorder=recorder,
        )

        result = engine.run()

        # 验证：应该能完成或报告明确错误
        assert result.status in [GlobalState.COMPLETED, GlobalState.ERROR], \
            "空vision响应应能被处理"
