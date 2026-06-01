#!/usr/bin/env python3
"""
图模型解析完整示例

展示执行器和状态机如何解析和遍历 TraversalPlan：
1. TraversalPlan 解析
2. 图模型节点构建
3. 状态机遍历过程
4. 树状输出和日志
"""

import sys
from pathlib import Path
from typing import Dict, List, Any
import json
from datetime import datetime

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.ai.capabilities.types import TraversalPlan, TraversalNode as AITraversalNode, NodeOperation, NodeStrategy
from src.graph.node import (
    TraversalNode, NodeType, Operation, Target, RestoreAction,
    Precondition, ChildrenStrategy, DynamicRule, ErrorPolicy
)
from src.state.content_tree import PageAnalysis, MenuItem, Coordinate, ExpectedAction, MenuItemType

# ANSI 颜色代码
class Colors:
    HEADER = '\033[95m'
    BLUE = '\033[94m'
    CYAN = '\033[96m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    MAGENTA = '\033[35m'
    END = '\033[0m'
    BOLD = '\033[1m'


def print_header(text: str):
    """打印标题"""
    print(f"\n{Colors.HEADER}{Colors.BOLD}{'='*80}{Colors.END}")
    print(f"{Colors.HEADER}{Colors.BOLD}{text.center(80)}{Colors.END}")
    print(f"{Colors.HEADER}{Colors.BOLD}{'='*80}{Colors.END}\n")


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


def print_tree_node(node: Any, indent: int = 0, prefix: str = ""):
    """打印树状节点"""
    indent_str = "    " * indent
    connector = "├──" if indent > 0 else ""
    print(f"{indent_str}{prefix}{connector}{Colors.MAGENTA}{node.get('name', 'Unknown')}{Colors.END}")

    # 打印节点属性
    if node.get('type'):
        print(f"{indent_str}    │   {Colors.CYAN}类型: {node['type']}{Colors.END}")
    if node.get('action'):
        print(f"{indent_str}    │   {Colors.CYAN}操作: {node['action']}{Colors.END}")
    if node.get('children'):
        print(f"{indent_str}    │   {Colors.YELLOW}子节点: {len(node['children'])} 个{Colors.END}")

    # 递归打印子节点
    children = node.get('children', [])
    for i, child in enumerate(children):
        is_last = (i == len(children) - 1)
        child_prefix = "└──" if is_last else "├──"
        print_tree_node(child, indent + 1, child_prefix if indent > 0 else "")


def create_mock_traversal_plan() -> TraversalPlan:
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
        }
    }

    # 创建错误策略
    error_policy = {
        "on_error": "retry",
        "max_retries": 2,
        "continue_on_error": True
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
        children_strategy=children_strategy,
        error_policy=error_policy
    )

    # 创建静态子节点
    static_nodes = [
        AITraversalNode(
            node_id="storage",
            name="存储管理",
            node_type="container",
            operation=NodeOperation(
                action="click",
                target={"by": "text", "value": "存储管理"}
            ),
            precondition={"page_name": "设置"},
            children_strategy=NodeStrategy(type="dynamic_match"),
            error_policy={"on_error": "skip"}
        ),
        AITraversalNode(
            node_id="network",
            name="网络与互联网",
            node_type="container",
            operation=NodeOperation(
                action="click",
                target={"by": "text", "value": "网络与互联网"}
            ),
            precondition={"page_name": "设置"},
            children_strategy=NodeStrategy(type="dynamic_match"),
            error_policy={"on_error": "skip"}
        )
    ]

    # 创建遍历计划
    plan = TraversalPlan(
        entry_app="设置",
        root_node=root_node,
        static_nodes=static_nodes,
        mode="hybrid",
        confidence=0.95,
        reasoning="使用混合模式遍历设置应用，结合静态节点和动态发现"
    )

    return plan


def parse_ai_plan_to_graph_model(ai_plan: TraversalPlan) -> Dict[str, Any]:
    """将 AI 计划解析为图模型结构"""

    print_section("步骤 1: AI 计划解析")

    print_info("接收到 TraversalPlan:")
    print(f"  • 入口应用: {Colors.GREEN}{ai_plan.entry_app}{Colors.END}")
    print(f"  • 遍历模式: {Colors.GREEN}{ai_plan.mode}{Colors.END}")
    print(f"  • 置信度: {Colors.GREEN}{ai_plan.confidence}{Colors.END}")
    print(f"  • 推理原因: {Colors.CYAN}{ai_plan.reasoning}{Colors.END}")
    print(f"  • 静态节点数: {Colors.YELLOW}{len(ai_plan.static_nodes)}{Colors.END}")

    # 解析根节点
    root_node = parse_ai_node_to_graph_node(ai_plan.root_node)

    # 解析静态节点
    static_nodes = [parse_ai_node_to_graph_node(node) for node in ai_plan.static_nodes]

    # 构建图模型树
    graph_model = {
        "name": "Traversal Graph",
        "type": "root",
        "entry_app": ai_plan.entry_app,
        "mode": ai_plan.mode,
        "root": root_node,
        "static_nodes": static_nodes,
        "children": [root_node] + static_nodes
    }

    return graph_model


def parse_ai_node_to_graph_node(ai_node: AITraversalNode) -> Dict[str, Any]:
    """将 AI 节点解析为图模型节点"""

    # 解析操作
    operation = {
        "action": ai_node.operation.action,
        "target": ai_node.operation.target if hasattr(ai_node.operation, 'target') else None,
        "params": ai_node.operation.params if hasattr(ai_node.operation, 'params') else None
    }

    # 解析前置条件
    precondition = None
    if ai_node.precondition:
        precondition = {
            "page_name": ai_node.precondition.get("page_name"),
            "path": ai_node.precondition.get("path"),
            "ui_condition": ai_node.precondition.get("ui_condition")
        }

    # 解析子节点策略
    children_strategy = {
        "type": ai_node.children_strategy.type,
        "static_children": ai_node.children_strategy.static_children if hasattr(ai_node.children_strategy, 'static_children') else [],
        "dynamic_rules": ai_node.children_strategy.dynamic_rules if hasattr(ai_node.children_strategy, 'dynamic_rules') else {}
    }

    # 解析错误策略
    error_policy = None
    if ai_node.error_policy:
        error_policy = {
            "on_error": ai_node.error_policy.get("on_error", "abort") if isinstance(ai_node.error_policy, dict) else getattr(ai_node.error_policy, 'on_error', 'abort'),
            "max_retries": ai_node.error_policy.get("max_retries", 1) if isinstance(ai_node.error_policy, dict) else getattr(ai_node.error_policy, 'max_retries', 1),
            "continue_on_error": ai_node.error_policy.get("continue_on_error", False) if isinstance(ai_node.error_policy, dict) else getattr(ai_node.error_policy, 'continue_on_error', False)
        }

    # 构建节点
    graph_node = {
        "id": ai_node.node_id,
        "name": ai_node.name,
        "type": ai_node.node_type,
        "operation": operation,
        "precondition": precondition,
        "children_strategy": children_strategy,
        "error_policy": error_policy,
        "children": []  # 将在遍历过程中动态填充
    }

    return graph_node


def simulate_state_machine_traversal(graph_model: Dict[str, Any]) -> Dict[str, Any]:
    """模拟状态机遍历过程"""

    print_section("步骤 2: 状态机初始化")

    # 模拟状态机状态
    state_machine = {
        "current_state": "IDLE",
        "current_path": [],
        "visited_nodes": [],
        "node_stack": [],
        "execution_trace": []
    }

    print_info("状态机初始化:")
    print(f"  • 初始状态: {Colors.YELLOW}{state_machine['current_state']}{Colors.END}")
    print(f"  • 当前路径: {Colors.CYAN}{state_machine['current_path']}{Colors.END}")
    print(f"  • 已访问节点: {Colors.GREEN}{len(state_machine['visited_nodes'])}{Colors.END}")

    print_section("步骤 3: 开始遍历执行")

    # 模拟遍历过程
    traversal_log = []

    # 步骤 1: 进入根节点
    log_entry = {
        "step": 1,
        "action": "ENTER_NODE",
        "node": graph_model["root"]["name"],
        "node_type": graph_model["root"]["type"],
        "precondition_check": "PASS",
        "state_change": "IDLE → EXECUTING"
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 1]{Colors.END} 进入根节点: {Colors.MAGENTA}{graph_model['root']['name']}{Colors.END}")
    print(f"  前置条件检查: {Colors.GREEN}✓ 通过{Colors.END}")
    print(f"  状态转换: {Colors.YELLOW}IDLE → EXECUTING{Colors.END}")

    # 步骤 2: 分析屏幕
    log_entry = {
        "step": 2,
        "action": "ANALYZE_SCREEN",
        "items_detected": 5,
        "screen_analysis": {
            "level1_dir": "left",
            "level2_dir": "bottom",
            "items": ["存储管理", "网络与互联网", "移动数据开关", "关于手机", "返回"]
        }
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 2]{Colors.END} 分析屏幕")
    print(f"  检测到项目: {Colors.YELLOW}{log_entry['items_detected']} 个{Colors.END}")
    print(f"  项目列表: {Colors.CYAN}{', '.join(log_entry['screen_analysis']['items'])}{Colors.END}")

    # 步骤 3: 应用动态规则
    log_entry = {
        "step": 3,
        "action": "APPLY_DYNAMIC_RULES",
        "rules_applied": ["menu_rule", "switch_rule"],
        "matched_items": {
            "存储管理": {"rule": "menu_rule", "action": "generate_child"},
            "网络与互联网": {"rule": "menu_rule", "action": "generate_child"},
            "移动数据开关": {"rule": "switch_rule", "action": "execute_inline"},
            "关于手机": {"rule": "menu_rule", "action": "generate_child"}
        }
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 3]{Colors.END} 应用动态规则")
    print(f"  使用规则: {Colors.YELLOW}{', '.join(log_entry['rules_applied'])}{Colors.END}")
    print(f"  匹配结果: {Colors.GREEN}{len(log_entry['matched_items'])} 个项目{Colors.END}")
    for item, match in log_entry['matched_items'].items():
        print(f"    • {Colors.CYAN}{item}{Colors.END} → {match['rule']} ({match['action']})")

    # 步骤 4: 安全检查
    log_entry = {
        "step": 4,
        "action": "SAFETY_CHECK",
        "safe_items": ["存储管理", "网络与互联网", "移动数据开关", "关于手机"],
        "skipped_items": [],
        "ai_safety_enabled": True
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 4]{Colors.END} 安全检查")
    print(f"  AI 安全筛选: {Colors.GREEN}已启用{Colors.END}")
    print(f"  安全项目: {Colors.GREEN}{len(log_entry['safe_items'])} 个{Colors.END}")
    print(f"  跳过项目: {Colors.YELLOW}{len(log_entry['skipped_items'])} 个{Colors.END}")

    # 步骤 5: 访问第一个项目
    log_entry = {
        "step": 5,
        "action": "VISIT_ITEM",
        "item": "存储管理",
        "coordinate": {"x": 0.5, "y": 0.3},
        "action_taken": "tap",
        "result": "SUCCESS"
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 5]{Colors.END} 访问项目: {Colors.MAGENTA}{log_entry['item']}{Colors.END}")
    print(f"  操作: {Colors.CYAN}{log_entry['action_taken']}{Colors.END}")
    print(f"  坐标: ({Colors.YELLOW}{log_entry['coordinate']['x']}, {log_entry['coordinate']['y']}{Colors.YELLOW}){Colors.END}")
    print(f"  结果: {Colors.GREEN}✓ 成功{Colors.END}")

    # 步骤 6: 页面跳转
    log_entry = {
        "step": 6,
        "action": "PAGE_NAVIGATION",
        "from_page": "设置",
        "to_page": "存储管理",
        "path_change": "['设置'] -> ['设置', '存储管理']"
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 6]{Colors.END} 页面导航")
    print(f"  从: {Colors.CYAN}{log_entry['from_page']}{Colors.END}")
    print(f"  到: {Colors.MAGENTA}{log_entry['to_page']}{Colors.END}")
    print(f"  路径变化: {Colors.YELLOW}['设置'] → ['设置', '存储管理']{Colors.END}")

    # 步骤 7: 返回上一页
    log_entry = {
        "step": 7,
        "action": "NAVIGATE_BACK",
        "from_path": ["设置", "存储管理"],
        "to_path": ["设置"],
        "result": "SUCCESS"
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 7]{Colors.END} 返回上一页")
    print(f"  路径: {Colors.CYAN}{log_entry['from_path']}{Colors.END} → {Colors.CYAN}{log_entry['to_path']}{Colors.END}")

    # 步骤 8: 继续遍历
    log_entry = {
        "step": 8,
        "action": "CONTINUE_TRAVERSAL",
        "remaining_items": ["网络与互联网", "移动数据开关", "关于手机"],
        "next_item": "网络与互联网"
    }
    traversal_log.append(log_entry)
    print(f"{Colors.GREEN}[步骤 8]{Colors.END} 继续遍历")
    print(f"  剩余项目: {Colors.YELLOW}{len(log_entry['remaining_items'])} 个{Colors.END}")
    print(f"  下一个: {Colors.MAGENTA}{log_entry['next_item']}{Colors.END}")

    # 更新状态机状态
    state_machine["current_state"] = "COMPLETED"
    state_machine["current_path"] = ["设置"]
    state_machine["visited_nodes"] = ["设置", "存储管理"]

    return {
        "traversal_log": traversal_log,
        "final_state": state_machine
    }


def generate_tree_output(graph_model: Dict[str, Any], traversal_result: Dict[str, Any]):
    """生成树状输出"""

    print_section("步骤 4: 生成树状结构输出")

    print(f"{Colors.BOLD}{Colors.MAGENTA}遍历图模型结构:{Colors.END}\n")

    # 构建树状结构
    tree_root = {
        "name": f"设置应用 ({graph_model['mode']} 模式)",
        "type": "root",
        "action": "entry_app",
        "children": [
            {
                "name": "设置主页面",
                "type": "container",
                "action": "root_node",
                "children": [
                    {
                        "name": "存储管理",
                        "type": "container",
                        "action": "menu_item → navigate",
                        "children": [
                            {"name": "内部存储", "type": "leaf", "action": "view"},
                            {"name": "外部存储", "type": "leaf", "action": "view"}
                        ]
                    },
                    {
                        "name": "网络与互联网",
                        "type": "container",
                        "action": "menu_item → navigate",
                        "children": [
                            {"name": "移动网络", "type": "leaf_switch", "action": "toggle"},
                            {"name": "Wi-Fi", "type": "leaf_switch", "action": "toggle"}
                        ]
                    },
                    {
                        "name": "移动数据开关",
                        "type": "leaf_switch",
                        "action": "switch → toggle"
                    },
                    {
                        "name": "关于手机",
                        "type": "leaf_info",
                        "action": "menu_item → view"
                    }
                ]
            }
        ]
    }

    print_tree_node(tree_root)


def generate_detailed_logs(traversal_result: Dict[str, Any]):
    """生成详细日志"""

    print_section("步骤 5: 详细执行日志")

    print(f"{Colors.BOLD}{Colors.CYAN}状态转换日志:{Colors.END}\n")

    for log_entry in traversal_result["traversal_log"]:
        step = log_entry["step"]
        action = log_entry["action"]
        timestamp = datetime.now().strftime("%H:%M:%S.%f")[:-3]

        print(f"{Colors.GREEN}[{timestamp}]{Colors.END} {Colors.BOLD}步骤 {step}{Colors.END}")
        print(f"  动作: {Colors.YELLOW}{action}{Colors.END}")

        # 打印详细信息
        for key, value in log_entry.items():
            if key not in ["step", "action"]:
                if isinstance(value, dict):
                    print(f"  {Colors.CYAN}{key}:{Colors.END}")
                    for k, v in value.items():
                        print(f"    {k}: {v}")
                elif isinstance(value, list):
                    print(f"  {Colors.CYAN}{key}:{Colors.END} {', '.join(map(str, value))}")
                else:
                    print(f"  {Colors.CYAN}{key}:{Colors.END} {value}")
        print()


def generate_json_output(graph_model: Dict[str, Any], traversal_result: Dict[str, Any]):
    """生成 JSON 格式输出"""

    print_section("步骤 6: JSON 格式输出")

    output = {
        "traversal_plan": {
            "entry_app": graph_model["entry_app"],
            "mode": graph_model["mode"],
            "root_node": graph_model["root"],
            "static_nodes": graph_model["static_nodes"]
        },
        "execution_summary": {
            "total_steps": len(traversal_result["traversal_log"]),
            "visited_nodes": traversal_result["final_state"]["visited_nodes"],
            "final_path": traversal_result["final_state"]["current_path"],
            "final_state": traversal_result["final_state"]["current_state"]
        },
        "execution_trace": traversal_result["traversal_log"]
    }

    print(f"{Colors.BOLD}{Colors.CYAN}完整执行结果 (JSON):{Colors.END}\n")
    print(json.dumps(output, ensure_ascii=False, indent=2))


def main():
    """主函数"""

    print_header("图模型解析完整示例")

    try:
        # 步骤 1: 创建模拟计划
        print_section("初始化: 创建 TraversalPlan")
        ai_plan = create_mock_traversal_plan()
        print_success("✓ TraversalPlan 创建成功")

        # 步骤 2: 解析为图模型
        graph_model = parse_ai_plan_to_graph_model(ai_plan)
        print_success("✓ 图模型解析成功")

        # 步骤 3: 模拟状态机遍历
        traversal_result = simulate_state_machine_traversal(graph_model)
        print_success("✓ 状态机遍历完成")

        # 步骤 4: 生成树状输出
        generate_tree_output(graph_model, traversal_result)
        print_success("✓ 树状结构生成完成")

        # 步骤 5: 生成详细日志
        generate_detailed_logs(traversal_result)
        print_success("✓ 详细日志生成完成")

        # 步骤 6: 生成 JSON 输出
        generate_json_output(graph_model, traversal_result)
        print_success("✓ JSON 输出生成完成")

        print_header("示例执行完成")

        print_success("所有步骤执行成功！")
        print()
        print("执行摘要:")
        print(f"  • 图模型节点: {Colors.YELLOW}{len(graph_model['children'])} 个{Colors.END}")
        print(f"  • 执行步骤: {Colors.YELLOW}{len(traversal_result['traversal_log'])} 步{Colors.END}")
        print(f"  • 访问节点: {Colors.YELLOW}{len(traversal_result['final_state']['visited_nodes'])} 个{Colors.END}")
        print(f"  • 最终状态: {Colors.GREEN}{traversal_result['final_state']['current_state']}{Colors.END}")

    except Exception as e:
        print_error(f"执行失败: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    main()
