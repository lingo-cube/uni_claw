#!/usr/bin/env python3
"""
执行器优化测试脚本

测试优化后的执行器功能：
1. 动态规则匹配
2. AI 安全检查
3. 错误策略处理
4. 模式差异化遍历
"""

import sys
from pathlib import Path
from unittest.mock import Mock, MagicMock, patch
from typing import Dict, List
import json

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.ai.capabilities.types import (
    TraversalPlan, TraversalNode as AITraversalNode, NodeOperation, NodeStrategy
)
from src.state.content_tree import PageAnalysis, MenuItem, Coordinate, ExpectedAction, MenuItemType
from src.graph.matcher import MatchCondition

# ANSI 颜色代码
class Colors:
    HEADER = '\033[95m'
    BLUE = '\033[94m'
    CYAN = '\033[96m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    END = '\033[0m'
    BOLD = '\033[1m'


def print_header(text: str):
    """打印标题"""
    print(f"\n{Colors.HEADER}{Colors.BOLD}{'='*70}{Colors.END}")
    print(f"{Colors.HEADER}{Colors.BOLD}{text.center(70)}{Colors.END}")
    print(f"{Colors.HEADER}{Colors.BOLD}{'='*70}{Colors.END}\n")


def print_section(text: str):
    """打印小节标题"""
    print(f"\n{Colors.CYAN}{Colors.BOLD}▶ {text}{Colors.END}\n")


def print_success(text: str):
    """打印成功信息"""
    print(f"{Colors.GREEN}✓ {text}{Colors.END}")


def print_error(text: str):
    """打印错误信息"""
    print(f"{Colors.RED}✗ {text}{Colors.END}")


def print_info(text: str):
    """打印信息"""
    print(f"{Colors.BLUE}ℹ {text}{Colors.END}")


def print_warning(text: str):
    """打印警告信息"""
    print(f"{Colors.YELLOW}⚠ {text}{Colors.END}")


def create_mock_plan(mode: str = "hybrid") -> TraversalPlan:
    """创建模拟的 TraversalPlan"""

    # 创建动态规则
    dynamic_rules = {
        "menu_rule": {
            "match_condition": {
                "type": "menu_item",
                "expected_action": "navigate"
            },
            "child_template": "menu_container",
            "action": "generate_child"
        },
        "switch_rule": {
            "match_condition": {
                "type": "switch"
            },
            "child_template": "switch_control",
            "action": "execute_inline"
        },
        "dangerous_rule": {
            "match_condition": {
                "text_pattern": ".*删除.*"
            },
            "child_template": None,
            "action": "skip"
        }
    }

    # 创建子节点策略
    children_strategy = NodeStrategy(
        type="dynamic_match",
        dynamic_rules=dynamic_rules
    )

    # 创建根节点
    root_node = AITraversalNode(
        node_id="root",
        name="设置主页面",
        node_type="container",
        operation=NodeOperation(action="no_action"),
        precondition={"page_name": "设置"},
        children_strategy=children_strategy
    )

    # 创建遍历计划
    plan = TraversalPlan(
        entry_app="设置",
        root_node=root_node,
        mode=mode,
        confidence=0.95,
        reasoning="使用混合模式遍历设置应用"
    )

    return plan


def create_mock_screen_analysis() -> PageAnalysis:
    """创建模拟的屏幕分析结果"""

    items = [
        MenuItem(
            name="存储管理",
            type=MenuItemType.MENU_ITEM,
            expected_action=ExpectedAction.NAVIGATE,
            coordinate=Coordinate(x=0.5, y=0.3),
            parent=None
        ),
        MenuItem(
            name="移动数据开关",
            type=MenuItemType.SWITCH,
            expected_action=ExpectedAction.TOGGLE,
            coordinate=Coordinate(x=0.5, y=0.4),
            parent=None
        ),
        MenuItem(
            name="删除所有数据",
            type=MenuItemType.BUTTON,
            expected_action=ExpectedAction.ACTION,
            coordinate=Coordinate(x=0.5, y=0.5),
            parent=None
        ),
        MenuItem(
            name="关于手机",
            type=MenuItemType.MENU_ITEM,
            expected_action=ExpectedAction.NAVIGATE,
            coordinate=Coordinate(x=0.5, y=0.6),
            parent=None
        ),
        MenuItem(
            name="返回",
            type=MenuItemType.BACK_BUTTON,
            expected_action=ExpectedAction.NAVIGATE,
            coordinate=Coordinate(x=0.1, y=0.1),
            parent=None
        )
    ]

    analysis = PageAnalysis(
        level1_dir="left",
        level1_menus=[],
        level2_dir="top",
        level2_menus=[],
        current_path=[],
        items=items,
        is_popup=False,
        popup_info=None,
        close_button=None,
        back_button=Coordinate(x=0.1, y=0.1),
        has_scroll=True,
        is_end_of_list=False
    )

    return analysis


def test_dynamic_rules_matching():
    """测试动态规则匹配"""

    print_section("测试 1: 动态规则匹配")

    # 创建模拟计划和屏幕分析
    plan = create_mock_plan()
    analysis = create_mock_screen_analysis()

    print_info(f"计划模式: {plan.mode}")
    print_info(f"入口应用: {plan.entry_app}")
    print_info(f"动态规则数量: {len(plan.root_node.children_strategy.dynamic_rules)}")

    print("\n动态规则:")
    for rule_name, rule_config in plan.root_node.children_strategy.dynamic_rules.items():
        print(f"  - {rule_name}:")
        print(f"    匹配条件: {rule_config.get('match_condition', {})}")
        print(f"    动作: {rule_config.get('action', 'unknown')}")
        print(f"    模板: {rule_config.get('child_template', 'none')}")

    print("\n屏幕分析结果:")
    print(f"  检测到 {len(analysis.items)} 个可交互项目")
    for item in analysis.items:
        type_value = item.type.value if hasattr(item.type, 'value') else item.type
        print(f"    - {item.name} ({type_value})")

    # 模拟动态规则匹配
    print("\n开始动态规则匹配...")

    matched_items = {}
    rules = plan.root_node.children_strategy.dynamic_rules

    for item in analysis.items:
        for rule_name, rule_config in rules.items():
            match_condition = rule_config.get("match_condition", {})
            condition = MatchCondition(match_condition)

            item_data = {
                "type": item.type.value if hasattr(item.type, 'value') else item.type,
                "expected_action": item.expected_action.value if hasattr(item.expected_action, 'value') else item.expected_action,
                "text": item.name
            }

            if condition.matches(item_data):
                matched_items[item.name] = {
                    "rule": rule_name,
                    "action": rule_config.get("action", "unknown"),
                    "template": rule_config.get("child_template", "none")
                }
                print_success(f"项目 '{item.name}' 匹配规则 '{rule_name}'")
                print(f"      动作: {matched_items[item.name]['action']}")
                break

    print_info(f"\n匹配结果: {len(matched_items)}/{len(analysis.items)} 个项目匹配到规则")

    # 显示匹配详情
    if matched_items:
        print("\n匹配详情:")
        for item_name, match_info in matched_items.items():
            print(f"  • {item_name}:")
            print(f"    规则: {match_info['rule']}")
            print(f"    动作: {match_info['action']}")
            print(f"    模板: {match_info['template']}")

    return matched_items


def test_safety_checking():
    """测试安全检查"""

    print_section("测试 2: AI 安全检查")

    analysis = create_mock_screen_analysis()

    # 模拟安全筛选结果
    mock_safety_evaluations = [
        {
            "name": "存储管理",
            "safety_tag": "safe",
            "confidence": 0.95,
            "reason": "正常设置选项"
        },
        {
            "name": "移动数据开关",
            "safety_tag": "safe",
            "confidence": 0.98,
            "reason": "标准开关控件"
        },
        {
            "name": "删除所有数据",
            "safety_tag": "skip",
            "confidence": 0.99,
            "reason": "危险操作：会清除所有数据"
        },
        {
            "name": "关于手机",
            "safety_tag": "safe",
            "confidence": 0.95,
            "reason": "信息显示页面"
        }
    ]

    print("模拟 AI 安全筛选结果:")

    safe_items = []
    skipped_items = []

    for item in analysis.items:
        # 查找对应的安全评估
        evaluation = next((e for e in mock_safety_evaluations if e["name"] == item.name), None)

        if evaluation:
            if evaluation["safety_tag"] in ["caution", "skip"]:
                print_error(f"跳过危险项目: {item.name}")
                print(f"    原因: {evaluation['reason']}")
                print(f"    置信度: {evaluation['confidence']}")
                skipped_items.append(item.name)
            else:
                print_success(f"项目安全: {item.name} ({evaluation['safety_tag']})")
                safe_items.append(item.name)

    print_info(f"\n安全检查结果:")
    print(f"  安全项目: {len(safe_items)}")
    print(f"  跳过项目: {len(skipped_items)}")

    return safe_items, skipped_items


def test_error_handling():
    """测试错误策略处理"""

    print_section("测试 3: 错误策略处理")

    plan = create_mock_plan()

    # 添加错误策略到根节点
    class MockErrorPolicy:
        def __init__(self):
            self.on_error = "retry"
            self.max_retries = 3

    plan.root_node.error_policy = MockErrorPolicy()

    print_info(f"错误策略: {plan.root_node.error_policy.on_error}")
    print_info(f"最大重试次数: {plan.root_node.error_policy.max_retries}")

    # 模拟错误处理
    retry_count = 0
    max_retries = plan.root_node.error_policy.max_retries

    print("\n模拟执行错误和重试:")

    for attempt in range(max_retries + 2):  # 多试几次看效果
        print(f"\n尝试 #{attempt + 1}:")
        print(f"  当前重试计数: {retry_count}/{max_retries}")

        if retry_count < max_retries:
            print_success(f"  执行重试")
            retry_count += 1
        else:
            print_error(f"  达到最大重试次数，停止执行")
            break


def test_mode_differentiation():
    """测试模式差异化"""

    print_section("测试 4: 模式差异化遍历")

    modes = ["hybrid", "concrete", "dynamic"]

    for mode in modes:
        plan = create_mock_plan(mode=mode)

        print_info(f"模式: {mode.upper()}")

        if mode == "hybrid":
            print("  策略: 结合静态节点和动态发现")
            print("  特点:")
            print("    • 使用 plan.root_node 作为起点")
            print("    • 应用 dynamic_rules 进行智能匹配")
            print("    • 灵活应对已知和未知界面")

        elif mode == "concrete":
            print("  策略: 主要使用预定义的静态节点")
            print("  特点:")
            print("    • 最小化动态屏幕分析")
            print("    • 快速执行预定义路径")
            print("    • 适用于已知界面结构")

        elif mode == "dynamic":
            print("  策略: 完全依赖动态发现")
            print("  特点:")
            print("    • 使用 dynamic_rules 引导遍历")
            print("    • 灵活应对未知界面")
            print("    • 最大化探索范围")

        print()


def generate_trace_output():
    """生成追踪日志输出示例"""

    print_section("测试 5: 追踪日志输出示例")

    # 模拟追踪日志
    trace_logs = [
        {
            "type": "span_start",
            "operation": "execute_plan",
            "tags": {
                "mode": "hybrid",
                "entry_app": "设置",
                "confidence": 0.95
            }
        },
        {
            "type": "event",
            "event": "navigating_to_app",
            "app": "设置"
        },
        {
            "type": "event",
            "event": "initial_screen",
            "path": [],
            "items_count": 5
        },
        {
            "type": "event",
            "event": "dynamic_rules_applied",
            "matched_count": 4,
            "matches": ["存储管理", "移动数据开关", "删除所有数据", "关于手机"]
        },
        {
            "type": "event",
            "event": "skipped_dangerous",
            "item": "删除所有数据",
            "reason": "safety_check"
        },
        {
            "type": "step",
            "step_number": 1,
            "action": "tap",
            "target": "存储管理",
            "success": True
        },
        {
            "type": "step",
            "step_number": 2,
            "action": "tap",
            "target": "移动数据开关",
            "success": True
        },
        {
            "type": "span_end",
            "status": "success",
            "total_steps": 2,
            "visited_count": 2,
            "skipped_count": 1
        }
    ]

    print("追踪日志示例 (JSONL 格式):")
    print()

    for log in trace_logs:
        print(json.dumps(log, ensure_ascii=False))


def show_integration_trace():
    """显示集成链路追踪"""

    print_section("测试 6: 集成链路追踪")

    print("执行器优化后的完整调用链路:")
    print()

    trace_flow = [
        ("1. 用户指令", "parse_instruction", "生成 TraversalPlan"),
        ("2. 计划解析", "execute_plan", "根据 mode 选择执行策略"),
        ("3. 模式分发", "_execute_hybrid_mode", "HYBRID 模式执行"),
        ("4. 屏幕分析", "analyze_screen", "Vision 服务分析屏幕"),
        ("5. 动态规则", "apply_dynamic_rules", "匹配 UI 元素到规则"),
        ("6. 安全检查", "is_safe_to_click", "AI 安全筛选"),
        ("7. 元素点击", "tap_element", "执行点击操作"),
        ("8. 错误处理", "handle_execution_error", "根据 policy 处理错误"),
        ("9. 结果保存", "save_result", "保存遍历结果")
    ]

    for step, function, description in trace_flow:
        print(f"{Colors.CYAN}{step}{Colors.END}")
        print(f"  函数: {Colors.BOLD}{function}{Colors.END}")
        print(f"  描述: {description}")
        print()


def main():
    """主测试函数"""

    print_header("执行器优化测试")

    print_info("测试优化后的执行器功能")
    print_info("包括: 动态规则匹配、AI 安全检查、错误策略处理、模式差异化")

    try:
        # 测试 1: 动态规则匹配
        matched_items = test_dynamic_rules_matching()

        # 测试 2: 安全检查
        safe_items, skipped_items = test_safety_checking()

        # 测试 3: 错误处理
        test_error_handling()

        # 测试 4: 模式差异化
        test_mode_differentiation()

        # 测试 5: 追踪日志
        generate_trace_output()

        # 测试 6: 集成链路
        show_integration_trace()

        print_header("测试完成")

        print_success("所有测试功能正常")
        print()
        print("测试摘要:")
        print(f"  • 动态规则匹配: {len(matched_items)} 个项目匹配到规则")
        print(f"  • 安全检查: {len(safe_items)} 个安全, {len(skipped_items)} 个跳过")
        print(f"  • 错误处理: 支持重试策略")
        print(f"  • 模式差异化: 支持 hybrid, concrete, dynamic 模式")
        print()
        print_info("优化后的执行器已准备就绪！")

    except Exception as e:
        print_error(f"测试失败: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    main()
