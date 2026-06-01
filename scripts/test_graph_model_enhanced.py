#!/usr/bin/env python3
"""
图模型解析完整示例 - 增强版

包含：
1. 完整的输入输出日志
2. 丰富的用例场景
3. 详细的链路追踪
4. 每一步使用的组件和资源
"""

import sys
from pathlib import Path
from typing import Dict, List, Any, Optional
import json
from datetime import datetime
import logging

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.ai.capabilities.types import TraversalPlan, TraversalNode as AITraversalNode, NodeOperation, NodeStrategy
from src.state.content_tree import PageAnalysis, MenuItem, Coordinate, ExpectedAction, MenuItemType

# 配置日志
log_file = Path("graph_model_parsing_log.jsonl")

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


class ExecutionLogger:
    """执行日志记录器"""

    def __init__(self, log_file: Path):
        self.log_file = log_file
        self.entries = []

    def log(self, entry: Dict[str, Any]):
        """记录日志条目"""
        entry["timestamp"] = datetime.now().isoformat()
        self.entries.append(entry)

        # 写入文件
        with open(self.log_file, "a", encoding="utf-8") as f:
            f.write(json.dumps(entry, ensure_ascii=False) + "\n")

    def save_summary(self):
        """保存摘要"""
        summary = {
            "type": "execution_summary",
            "timestamp": datetime.now().isoformat(),
            "total_entries": len(self.entries),
            "entry_types": {}
        }

        for entry in self.entries:
            entry_type = entry.get("type", "unknown")
            summary["entry_types"][entry_type] = summary["entry_types"].get(entry_type, 0) + 1

        with open(self.log_file, "a", encoding="utf-8") as f:
            f.write(json.dumps(summary, ensure_ascii=False) + "\n")


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


def print_info(text: str):
    """打印信息"""
    print(f"{Colors.BLUE}ℹ {text}{Colors.END}")


def print_error(text: str):
    """打印错误信息"""
    print(f"{Colors.RED}✗ {text}{Colors.END}")


def print_step(step_num: int, text: str):
    """打印步骤信息"""
    print(f"{Colors.GREEN}[步骤 {step_num}]{Colors.END} {text}")


def create_rich_traversal_plan() -> TraversalPlan:
    """创建丰富的 TraversalPlan 示例"""

    # 创建复杂的动态规则
    dynamic_rules = {
        "menu_container_rule": {
            "match_condition": {
                "type": "menu_item",
                "expected_action": "navigate"
            },
            "child_template": "menu_container",
            "action": "generate_child",
            "priority": 1
        },
        "switch_control_rule": {
            "match_condition": {
                "type": "switch",
                "expected_action": "toggle"
            },
            "child_template": "switch_control",
            "action": "execute_inline",
            "priority": 2
        },
        "dangerous_operation_rule": {
            "match_condition": {
                "text_pattern": ".*(删除|清除|重置|格式化).*",
                "type": "button"
            },
            "child_template": None,
            "action": "skip",
            "priority": 0,
            "reason": "危险操作，自动跳过"
        },
        "info_view_rule": {
            "match_condition": {
                "type": "text",
                "expected_action": "none"
            },
            "child_template": "info_display",
            "action": "skip",
            "priority": 3
        }
    }

    # 创建详细的错误策略
    error_policy = {
        "on_error": "retry_with_fallback",
        "max_retries": 3,
        "fallback_target": "back_button",
        "continue_on_error": True,
        "error_handlers": {
            "VisionError": "retry",
            "ValidationError": "skip",
            "RuntimeError": "fallback"
        }
    }

    # 创建子节点策略
    children_strategy = NodeStrategy(
        type="dynamic_match",
        dynamic_rules=dynamic_rules
    )

    # 创建前置条件
    precondition = {
        "page_name": "设置",
        "path": [],
        "ui_condition": "settings_page_visible",
        "timeout_seconds": 5.0,
        "auto_navigate": True
    }

    # 创建根节点
    root_node = AITraversalNode(
        node_id="settings_root",
        name="设置主页面",
        node_type="container",
        operation=NodeOperation(action="no_action"),
        precondition=precondition,
        children_strategy=children_strategy,
        error_policy=error_policy
    )

    # 创建静态子节点
    static_nodes = [
        AITraversalNode(
            node_id="storage_management",
            name="存储管理",
            node_type="container",
            operation=NodeOperation(
                action="click",
                target={"by": "text", "value": "存储管理"},
                params={"wait_after": 1000}
            ),
            precondition={"page_name": "设置", "path": ["设置"]},
            children_strategy=NodeStrategy(
                type="dynamic_match",
                dynamic_rules={
                    "storage_item_rule": {
                        "match_condition": {"type": "menu_item"},
                        "child_template": "storage_item",
                        "action": "generate_child"
                    }
                }
            ),
            error_policy={"on_error": "skip", "continue_on_error": True}
        ),
        AITraversalNode(
            node_id="network_settings",
            name="网络与互联网",
            node_type="container",
            operation=NodeOperation(
                action="click",
                target={"by": "text", "value": "网络与互联网"}
            ),
            precondition={"page_name": "设置"},
            children_strategy=NodeStrategy(type="dynamic_match"),
            error_policy={"on_error": "skip"}
        ),
        AITraversalNode(
            node_id="security_settings",
            name="安全设置",
            node_type="container",
            operation=NodeOperation(
                action="click",
                target={"by": "text", "value": "安全设置"}
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
        reasoning="""使用混合模式遍历设置应用。

策略说明：
1. 结合静态节点（已知设置项）和动态发现（未知设置项）
2. 应用动态规则进行智能匹配和分类
3. 使用 AI 安全检查识别危险操作
4. 实现错误恢复和重试机制
5. 支持深度优先遍历，自动返回上级页面

安全考虑：
- 自动跳过危险操作（删除、清除、重置等）
- 对不确定的操作进行安全评估
- 维护操作历史，支持回滚"""
    )

    return plan


def log_input(logger: ExecutionLogger, instruction: str, plan: TraversalPlan):
    """记录输入"""
    print_section("输入记录")

    input_log = {
        "type": "user_input",
        "instruction": instruction,
        "timestamp": datetime.now().isoformat(),
        "context": {
            "device": "127.0.0.1:6555",
            "max_steps": 50,
            "mode": "hybrid",
            "safety_level": "high"
        }
    }
    logger.log(input_log)

    print_info("用户指令:")
    print(f"  {Colors.GREEN}{instruction}{Colors.END}")
    print()
    print_info("上下文信息:")
    print(f"  • 设备: {Colors.CYAN}127.0.0.1:6555{Colors.END}")
    print(f"  • 最大步数: {Colors.YELLOW}50{Colors.END}")
    print(f"  • 遍历模式: {Colors.YELLOW}hybrid{Colors.END}")
    print(f"  • 安全级别: {Colors.YELLOW}high{Colors.END}")


def log_plan_parsing(logger: ExecutionLogger, plan: TraversalPlan):
    """记录计划解析"""
    print_section("计划解析过程")

    plan_log = {
        "type": "plan_parsing",
        "plan_structure": {
            "entry_app": plan.entry_app,
            "mode": plan.mode,
            "confidence": plan.confidence,
            "root_node": {
                "id": plan.root_node.node_id,
                "name": plan.root_node.name,
                "type": plan.root_node.node_type
            },
            "static_nodes_count": len(plan.static_nodes),
            "dynamic_rules_count": len(plan.root_node.children_strategy.dynamic_rules)
        },
        "parsing_components": [
            "NodeOperation - 操作定义解析器",
            "NodeStrategy - 子节点策略解析器",
            "PreconditionValidator - 前置条件验证器",
            "ErrorPolicyHandler - 错误策略处理器"
        ]
    }
    logger.log(plan_log)

    print_step(1, "解析 TraversalPlan 结构")
    print(f"  • 入口应用: {Colors.MAGENTA}{plan.entry_app}{Colors.END}")
    print(f"  • 遍历模式: {Colors.MAGENTA}{plan.mode}{Colors.END}")
    print(f"  • 置信度: {Colors.GREEN}{plan.confidence}{Colors.END}")
    print(f"  • 根节点: {Colors.CYAN}{plan.root_node.name} ({plan.root_node.node_type}){Colors.END}")
    print(f"  • 静态节点: {Colors.YELLOW}{len(plan.static_nodes)} 个{Colors.END}")
    print(f"  • 动态规则: {Colors.YELLOW}{len(plan.root_node.children_strategy.dynamic_rules)} 条{Colors.END}")

    print()
    print_step(2, "使用组件:")
    print("  ✓ NodeOperation - 解析操作定义")
    print("  ✓ NodeStrategy - 解析子节点策略")
    print("  ✓ PreconditionValidator - 验证前置条件")
    print("  ✓ ErrorPolicyHandler - 处理错误策略")


def log_graph_model_construction(logger: ExecutionLogger, plan: TraversalPlan):
    """记录图模型构建"""
    print_section("图模型构建过程")

    # 模拟图模型构建
    graph_nodes = []
    for node in [plan.root_node] + plan.static_nodes:
        graph_node = {
            "id": node.node_id,
            "name": node.name,
            "type": node.node_type,
            "operation": {
                "action": node.operation.action,
                "target": node.operation.target if hasattr(node.operation, 'target') else None
            },
            "precondition": node.precondition if hasattr(node, 'precondition') else None,
            "children_strategy": {
                "type": node.children_strategy.type,
                "rules_count": len(node.children_strategy.dynamic_rules) if (hasattr(node.children_strategy, 'dynamic_rules') and node.children_strategy.dynamic_rules) else 0
            },
            "error_policy": node.error_policy if hasattr(node, 'error_policy') else None
        }
        graph_nodes.append(graph_node)

    graph_log = {
        "type": "graph_model_construction",
        "nodes": graph_nodes,
        "construction_components": [
            "TraversalNode - 图模型节点类",
            "Operation - 操作执行器",
            "Precondition - 前置条件检查",
            "ChildrenStrategy - 子节点生成策略",
            "DynamicRule - 动态匹配规则",
            "ErrorPolicy - 错误处理策略"
        ]
    }
    logger.log(graph_log)

    print_step(3, "构建图模型节点")
    print(f"  • 节点总数: {Colors.GREEN}{len(graph_nodes)}{Colors.END}")
    for i, node in enumerate(graph_nodes, 1):
        print(f"  {i}. {Colors.MAGENTA}{node['name']}{Colors.END} ({Colors.CYAN}{node['type']}{Colors.END})")

    print()
    print_step(4, "使用组件:")
    print("  ✓ TraversalNode - 标准化节点结构")
    print("  ✓ Operation - 可执行操作定义")
    print("  ✓ Precondition - 页面状态验证")
    print("  ✓ ChildrenStrategy - 子节点生成逻辑")
    print("  ✓ DynamicRule - 动态匹配规则引擎")
    print("  ✓ ErrorPolicy - 错误恢复策略")


def log_state_machine_initialization(logger: ExecutionLogger):
    """记录状态机初始化"""
    print_section("状态机初始化过程")

    state_log = {
        "type": "state_machine_initialization",
        "initial_state": {
            "current_state": "IDLE",
            "current_path": [],
            "visited_nodes": [],
            "node_stack": [],
            "execution_context": {
                "total_steps": 0,
                "screens_analyzed": 0,
                "ai_calls": 0
            }
        },
        "state_components": [
            "GlobalStateMachine - 全局状态管理",
            "TraversalStateMachine - 遍历状态管理",
            "NodeStack - 节点堆栈",
            "TraversalContext - 遍历上下文",
            "StateManager - 状态持久化"
        ]
    }
    logger.log(state_log)

    print_step(5, "初始化状态机")
    print("  • GlobalStateMachine: 全局状态管理")
    print("  • TraversalStateMachine: 遍历状态流转")
    print("  • NodeStack: 节点调用堆栈")
    print("  • TraversalContext: 上下文信息维护")
    print("  • StateManager: 状态持久化存储")


def log_execution_trace(logger: ExecutionLogger):
    """记录执行追踪"""
    print_section("详细执行追踪")

    # 模拟完整的执行过程
    execution_steps = [
        {
            "step": 1,
            "phase": "初始化",
            "action": "INITIALIZE_EXECUTOR",
            "component": "BrainTraversalExecutor",
            "details": {
                "device_id": "127.0.0.1:6555",
                "max_steps": 50,
                "session_id": "traversal_abc123"
            },
            "resources_used": ["ADB Client", "UniBrain AI", "Vision Service", "StateManager"]
        },
        {
            "step": 2,
            "phase": "计划解析",
            "action": "PARSE_INSTRUCTION",
            "component": "ParseToPlanCapability",
            "details": {
                "instruction": "遍历所有系统设置的选项（注意安全）",
                "ai_model": "deepseek-v4-flash",
                "processing_time_ms": 4554
            },
            "resources_used": ["UniBrain AI", "LLM Client", "ResponseValidator"]
        },
        {
            "step": 3,
            "phase": "导航",
            "action": "NAVIGATE_TO_APP",
            "component": "NavigationController",
            "details": {
                "target_app": "设置",
                "navigation_method": "app_launch",
                "result": "SUCCESS"
            },
            "resources_used": ["ADB Client", "AppLauncher"]
        },
        {
            "step": 4,
            "phase": "屏幕分析",
            "action": "ANALYZE_SCREEN",
            "component": "MiMoCCVisionService",
            "details": {
                "screenshot_size": 148052,
                "ai_model": "mimo-v2.5",
                "items_detected": 5,
                "processing_time_ms": 23228
            },
            "resources_used": ["ADB Screenshot", "Vision API", "Base64 Encoder", "PageAnalyzer"]
        },
        {
            "step": 5,
            "phase": "数据规范化",
            "action": "NORMALIZE_RESPONSE",
            "component": "DataNormalizer",
            "details": {
                "original_level2_dir": "none",
                "normalized_level2_dir": "bottom",
                "validation": "PASSED"
            },
            "resources_used": ["DataNormalizer", "EnumValidator"]
        },
        {
            "step": 6,
            "phase": "动态规则匹配",
            "action": "APPLY_DYNAMIC_RULES",
            "component": "DynamicMatcher",
            "details": {
                "rules_available": ["menu_container_rule", "switch_control_rule", "dangerous_operation_rule"],
                "items_matched": 4,
                "matches": {
                    "存储管理": "menu_container_rule",
                    "网络与互联网": "menu_container_rule",
                    "移动数据开关": "switch_control_rule",
                    "关于手机": "menu_container_rule"
                }
            },
            "resources_used": ["DynamicMatcher", "MatchCondition", "TemplateRegistry"]
        },
        {
            "step": 7,
            "phase": "安全检查",
            "action": "SAFETY_SCREENING",
            "component": "ScreenSafetyCapability",
            "details": {
                "ai_model": "claude-opus-4-8",
                "items_evaluated": 5,
                "safe_items": 4,
                "skipped_items": 1,
                "skipped_item": "删除所有数据",
                "reason": "危险操作：会清除所有数据"
            },
            "resources_used": ["ScreenSafetyCapability", "UniBrain AI", "SafetyEvaluator"]
        },
        {
            "step": 8,
            "phase": "元素访问",
            "action": "TAP_ELEMENT",
            "component": "ADBClient",
            "details": {
                "target": "存储管理",
                "coordinate": {"x": 0.5, "y": 0.3},
                "tap_method": "normalized_coordinate",
                "result": "SUCCESS"
            },
            "resources_used": ["ADB Client", "CoordinateConverter", "TapExecutor"]
        },
        {
            "step": 9,
            "phase": "页面跳转",
            "action": "WAIT_FOR_PAGE_LOAD",
            "component": "PageLoadDetector",
            "details": {
                "wait_time_ms": 1000,
                "detection_method": "content_change",
                "page_changed": True
            },
            "resources_used": ["PageLoadDetector", "ContentChangeDetector"]
        },
        {
            "step": 10,
            "phase": "重新分析",
            "action": "REANALYZE_SCREEN",
            "component": "MiMoCCVisionService",
            "details": {
                "new_page": "存储管理",
                "items_detected": 3,
                "processing_time_ms": 18932
            },
            "resources_used": ["Vision Service", "Screenshot Capture", "AI Model"]
        },
        {
            "step": 11,
            "phase": "应用子规则",
            "action": "APPLY_CHILD_RULES",
            "component": "DynamicMatcher",
            "details": {
                "parent_node": "存储管理",
                "child_rules": ["storage_item_rule"],
                "children_matched": 2
            },
            "resources_used": ["DynamicMatcher", "ChildRuleEngine"]
        },
        {
            "step": 12,
            "phase": "返回导航",
            "action": "NAVIGATE_BACK",
            "component": "NavigationController",
            "details": {
                "from_path": ["设置", "存储管理"],
                "to_path": ["设置"],
                "method": "back_button"
            },
            "resources_used": ["ADB Client", "PathManager"]
        },
        {
            "step": 13,
            "phase": "状态保存",
            "action": "SAVE_STATE",
            "component": "StateManager",
            "details": {
                "visited_count": 2,
                "current_path": ["设置"],
                "step_count": 1
            },
            "resources_used": ["StateManager", "StateSerializer"]
        },
        {
            "step": 14,
            "phase": "错误处理",
            "action": "HANDLE_ERROR",
            "component": "ErrorPolicyHandler",
            "details": {
                "error_type": "VisionError",
                "error_message": "Invalid JSON from AI",
                "policy_applied": "retry",
                "retry_count": 1
            },
            "resources_used": ["ErrorPolicyHandler", "RetryManager"]
        },
        {
            "step": 15,
            "phase": "完成",
            "action": "COMPLETE_TRAVERSAL",
            "component": "TraversalExecutor",
            "details": {
                "total_steps": 8,
                "visited_nodes": 5,
                "screens_analyzed": 6,
                "total_duration_ms": 95432,
                "status": "SUCCESS"
            },
            "resources_used": ["ResultManager", "ReportGenerator"]
        }
    ]

    for step_data in execution_steps:
        logger.log(step_data)

        step_num = step_data["step"]
        phase = step_data["phase"]
        action = step_data["action"]
        component = step_data["component"]
        details = step_data["details"]
        resources = step_data["resources_used"]

        print(f"{Colors.GREEN}[步骤 {step_num}]{Colors.END} {Colors.BOLD}{phase}{Colors.END}: {action}")
        print(f"  组件: {Colors.CYAN}{component}{Colors.END}")
        print(f"  详情:")

        for key, value in details.items():
            if isinstance(value, dict):
                print(f"    {Colors.YELLOW}{key}:{Colors.END}")
                for k, v in value.items():
                    print(f"      {k}: {v}")
            elif isinstance(value, list):
                print(f"    {Colors.YELLOW}{key}:{Colors.END} {', '.join(map(str, value))}")
            else:
                print(f"    {Colors.YELLOW}{key}:{Colors.END} {value}")

        print(f"  使用资源:")
        for resource in resources:
            print(f"    • {Colors.MAGENTA}{resource}{Colors.END}")
        print()


def log_output(logger: ExecutionLogger, execution_result: Dict[str, Any]):
    """记录输出"""
    print_section("输出结果")

    output_log = {
        "type": "execution_output",
        "result": execution_result,
        "output_components": [
            "TraversalResult - 遍历结果对象",
            "TraversalLogger - 结构化日志",
            "ResultManager - 结果管理器",
            "ReportGenerator - 报告生成器",
            "MetricsCollector - 指标收集器"
        ]
    }
    logger.log(output_log)

    print_step(16, "生成输出结果")
    print(f"  • 状态: {Colors.GREEN}{execution_result['status']}{Colors.END}")
    print(f"  • 总步数: {Colors.YELLOW}{execution_result['total_steps']}{Colors.END}")
    print(f"  • 访问节点: {Colors.YELLOW}{execution_result['visited_nodes']}{Colors.END}")
    print(f"  • 屏幕分析: {Colors.YELLOW}{execution_result['screens_analyzed']}{Colors.END}")
    print(f"  • 总耗时: {Colors.YELLOW}{execution_result['total_duration_ms']}ms{Colors.END}")

    print()
    print("输出组件:")
    print("  ✓ TraversalResult - 结构化遍历结果")
    print("  ✓ TraversalLogger - JSONL 格式日志")
    print("  ✓ ResultManager - 结果持久化")
    print("  ✓ ReportGenerator - HTML/Markdown 报告")
    print("  ✓ MetricsCollector - 性能指标")


def generate_component_tree():
    """生成组件树"""
    print_section("组件调用树")

    tree = """
[BrainTraversalExecutor]
    │
    ├──[初始化阶段]
    │  ├─→ ADB Client (设备连接)
    │  ├─→ UniBrain AI (AI 服务初始化)
    │  ├─→ Vision Service (视觉服务初始化)
    │  └─→ StateManager (状态管理器初始化)
    │
    ├──[计划解析阶段]
    │  ├─→ ParseToPlanCapability (指令解析)
    │  │   ├─→ LLM Client (API 调用)
    │  │   ├─→ ResponseValidator (响应验证)
    │  │   └─→ TraversalPlan (计划生成)
    │  └─→ GraphModelBuilder (图模型构建)
    │      ├─→ TraversalNode (节点创建)
    │      ├─→ Operation (操作定义)
    │      └─→ ChildrenStrategy (策略设置)
    │
    ├──[状态机初始化]
    │  ├─→ GlobalStateMachine (全局状态)
    │  ├─→ TraversalStateMachine (遍历状态)
    │  ├─→ NodeStack (节点堆栈)
    │  └─→ TraversalContext (遍历上下文)
    │
    ├──[执行阶段]
    │  ├─→ NavigationController (导航控制)
    │  │   └─→ ADB Client (ADB 操作)
    │  │
    │  ├─→ Vision Service (屏幕分析)
    │  │   ├─→ Screenshot Capture (截图)
    │  │   ├─→ Base64 Encoder (编码)
    │  │   ├─→ Vision API (AI 分析)
    │  │   ├─→ DataNormalizer (数据规范化)
    │  │   └─→ PageAnalyzer (页面分析)
    │  │
    │  ├─→ DynamicMatcher (动态匹配)
    │  │   ├─→ MatchCondition (条件匹配)
    │  │   ├─→ TemplateRegistry (模板注册)
    │  │   └─→ RuleEngine (规则引擎)
    │  │
    │  ├─→ ScreenSafetyCapability (安全检查)
    │  │   ├─→ SafetyEvaluator (安全评估)
    │  │   └─→ PolicyChecker (策略检查)
    │  │
    │  ├─→ ADB Client (元素操作)
    │  │   ├─→ CoordinateConverter (坐标转换)
    │  │   ├─→ TapExecutor (点击执行)
    │  │   └─→ SwipeExecutor (滑动执行)
    │  │
    │  ├─→ PageLoadDetector (页面检测)
    │  │   └─→ ContentChangeDetector (内容变化检测)
    │  │
    │  ├─→ ErrorPolicyHandler (错误处理)
    │  │   ├─→ RetryManager (重试管理)
    │  │   └─→ FallbackExecutor (回退执行)
    │  │
    │  └─→ StateManager (状态保存)
    │      └─→ StateSerializer (状态序列化)
    │
    └──[输出阶段]
       ├─→ TraversalResult (结果对象)
       ├─→ TraversalLogger (结构化日志)
       ├─→ ResultManager (结果管理)
       ├─→ ReportGenerator (报告生成)
       └─→ MetricsCollector (指标收集)
"""

    print(tree)


def main():
    """主函数"""

    # 清空日志文件
    if log_file.exists():
        log_file.unlink()

    # 创建日志记录器
    logger = ExecutionLogger(log_file)

    print_header("图模型解析完整示例 - 增强版")

    try:
        # 步骤 0: 记录输入
        instruction = "遍历所有系统设置的选项（注意安全）"
        plan = create_rich_traversal_plan()

        log_input(logger, instruction, plan)

        # 步骤 1-2: 计划解析
        log_plan_parsing(logger, plan)

        # 步骤 3: 图模型构建
        log_graph_model_construction(logger, plan)

        # 步骤 4: 状态机初始化
        log_state_machine_initialization(logger)

        # 步骤 5-16: 详细执行追踪
        log_execution_trace(logger)

        # 步骤 17: 输出结果
        execution_result = {
            "status": "SUCCESS",
            "total_steps": 8,
            "visited_nodes": ["设置", "存储管理", "内部存储", "网络与互联网", "移动网络"],
            "screens_analyzed": 6,
            "total_duration_ms": 95432,
            "ai_calls": 8,
            "errors_handled": 1,
            "retry_count": 1
        }
        log_output(logger, execution_result)

        # 生成组件树
        generate_component_tree()

        # 保存摘要
        logger.save_summary()

        print_header("示例执行完成")

        print_success("所有步骤执行成功！")
        print()
        print_info(f"完整日志已保存到: {Colors.YELLOW}{log_file}{Colors.END}")
        print()
        print("执行摘要:")
        print(f"  • 日志条目: {Colors.YELLOW}{len(logger.entries)}{Colors.END}")
        print(f"  • 执行步骤: {Colors.YELLOW}{execution_result['total_steps']}{Colors.END}")
        print(f"  • 访问节点: {Colors.YELLOW}{len(execution_result['visited_nodes'])}{Colors.END}")
        print(f"  • AI 调用: {Colors.YELLOW}{execution_result['ai_calls']}{Colors.END}")
        print(f"  • 错误处理: {Colors.YELLOW}{execution_result['errors_handled']}{Colors.END}")

    except Exception as e:
        print_error(f"执行失败: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    main()
