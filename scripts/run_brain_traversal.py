#!/usr/bin/env python3
"""
Uni-Brain Traversal Executor

使用 UniBrain AI 解析用户指令，生成有向无环图(DAG)遍历计划，然后执行任务。

流程：
1. UniBrain 解析用户指令 → TraversalPlan (DAG)
2. 按照 Plan 执行遍历任务
3. 实时输出遍历进度
"""

import argparse
import logging
import sys
import json
from pathlib import Path
from typing import Dict, Any, Optional

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.adb.adb_client import RealADBClient
from src.config import get_settings
from src.state.state_manager import StateManager
from src.state.content_tree import Coordinate
from src.ai.provider import UniBrain
from src.ai.core.config import AIProviderConfig
from src.ai.capabilities.types import TraversalPlan, TraversalNode, NodeOperation, NodeStrategy
from src.vision.mimo_vision_cc import MiMoCCVisionServiceFactory

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


class BrainTraversalExecutor:
    """执行基于 UniBrain 解析的遍历任务。"""

    def __init__(self, device_id: str, max_steps: int = 200):
        """初始化执行器。

        Args:
            device_id: ADB 设备 ID
            max_steps: 最大遍历步数
        """
        settings = get_settings()
        if device_id:
            settings.adb_device_id = device_id

        # 初始化 ADB 客户端
        logger.info("初始化 ADB 客户端...")
        self.adb = RealADBClient(
            adb_path=settings.adb_path,
            device_id=settings.adb_device_id or None,
        )

        if not self.adb.is_connected():
            raise RuntimeError("没有连接的 ADB 设备")

        # 初始化 UniBrain AI
        logger.info("初始化 UniBrain AI...")
        # 对于文本AI能力，使用DeepSeek配置
        ai_config = AIProviderConfig(
            api_key=getattr(settings, "deepseek_api_key", ""),
            model="deepseek-v4-flash",
            base_url="https://api.deepseek.com/v1",
        )

        self.brain = UniBrain(ai_config)
        self.max_steps = max_steps
        self.step_count = 0

        # 状态跟踪
        self.visited_pages = set()
        self.current_path = []
        self.failed_nodes = []

    def parse_instruction(self, instruction: str) -> TraversalPlan:
        """使用 UniBrain 解析用户指令。

        Args:
            instruction: 用户自然语言指令

        Returns:
            TraversalPlan 遍历计划（包含 DAG）
        """
        logger.info(f"解析指令: {instruction}")
        logger.info("=" * 60)

        # 使用 UniBrain 的 parse capability
        from src.ai.capabilities.parse_to_plan import ParseToPlanCapability

        parse_capability = self.brain.capabilities["parse"]
        plan = parse_capability.execute(instruction)

        logger.info("解析成功！")
        logger.info(f"入口应用: {plan.entry_app or '默认（设置）'}")
        logger.info(f"遍历模式: {plan.mode}")
        logger.info(f"推理过程: {plan.reasoning}")
        logger.info(f"置信度: {plan.confidence}")

        return plan

    def visualize_plan(self, plan: TraversalPlan) -> None:
        """可视化遍历计划（DAG）。

        Args:
            plan: 遍历计划
        """
        print("\n" + "=" * 60)
        print("遍历计划 (DAG)")
        print("=" * 60)

        def print_node(node: TraversalNode, indent: int = 0):
            """递归打印节点树。"""
            prefix = "  " * indent
            print(f"{prefix}├─ 节点: {node.name} ({node.node_type})")
            print(f"{prefix}   操作: {node.operation.action}")
            if node.operation.target:
                print(f"{prefix}   目标: {node.operation.target}")
            if node.precondition:
                print(f"{prefix}   前置条件: {node.precondition}")
            print(f"{prefix}   子节点策略: {node.children_strategy.type}")

            # 递归打印子节点（如果有静态定义）
            if node.children_strategy.type == "static" and node.children_strategy.static_children:
                for child_name in node.children_strategy.static_children:
                    print(f"{prefix}   └─ {child_name}")

        print_node(plan.root_node)

        if plan.static_nodes:
            print("\n静态节点:")
            for node in plan.static_nodes:
                print_node(node, indent=1)

        print("=" * 60 + "\n")

    def execute_node(self, node: TraversalNode, page_analysis: Any = None) -> bool:
        """执行单个节点操作。

        Args:
            node: 要执行的节点
            page_analysis: 当前页面分析结果（可选）

        Returns:
            执行是否成功
        """
        logger.info(f"执行节点: {node.name}")
        self.step_count += 1

        if self.step_count > self.max_steps:
            logger.warning(f"达到最大步数限制 ({self.max_steps})")
            return False

        # 检查前置条件
        if node.precondition:
            if not self._check_precondition(node.precondition, page_analysis):
                logger.info(f"前置条件不满足，跳过节点: {node.name}")
                return False

        # 执行操作
        action = node.operation.action
        target = node.operation.target or {}
        params = node.operation.params or {}

        try:
            if action == "click":
                return self._execute_click(target, params)
            elif action == "back":
                return self._execute_back(params)
            elif action == "swipe":
                return self._execute_swipe(target, params)
            elif action == "scroll_down":
                return self._execute_scroll(params)
            elif action == "wait":
                return self._execute_wait(params)
            elif action == "no_action":
                logger.info("无操作")
                return True
            else:
                logger.warning(f"未知操作: {action}")
                return False

        except Exception as e:
            logger.error(f"执行节点 {node.name} 失败: {e}")
            self.failed_nodes.append(node.name)
            return False

    def _check_precondition(self, precondition: Dict, page_analysis: Any) -> bool:
        """检查前置条件是否满足。

        Args:
            precondition: 前置条件字典
            page_analysis: 当前页面分析结果

        Returns:
            条件是否满足
        """
        if not page_analysis:
            return True

        # 检查页面名
        if "page_name" in precondition:
            current_page = self.current_path[-1] if self.current_path else ""
            if precondition["page_name"] not in current_page:
                return False

        # 检查 UI 条件
        if "ui_condition" in precondition:
            condition = precondition["ui_condition"]
            if condition == "has_items":
                return len(page_analysis.items) > 0
            elif condition == "no_popup":
                return not page_analysis.is_popup

        return True

    def _execute_click(self, target: Dict, params: Dict) -> bool:
        """执行点击操作。

        Args:
            target: 目标描述 {"by": "text|coordinate", "value": "..."}
            params: 额外参数

        Returns:
            是否成功
        """
        by = target.get("by", "text")
        value = target.get("value")

        if by == "coordinate":
            # 通过坐标点击
            coord = Coordinate(x=float(value.get("x", 0.5)), y=float(value.get("y", 0.5)))
            logger.info(f"点击坐标: ({coord.x:.2f}, {coord.y:.2f})")
            self.adb.tap(coord.x, coord.y)
            return True

        elif by == "text":
            # 通过文本查找并点击（需要 Vision）
            logger.info(f"点击文本: {value}")
            # 这里应该调用 Vision 查找文本位置
            # 简化实现：假设 Vision 返回了坐标
            self.adb.tap(0.5, 0.5)  # 临时使用中心坐标
            return True

        return False

    def _execute_back(self, params: Dict) -> bool:
        """执行返回操作。

        Args:
            params: 参数

        Returns:
            是否成功
        """
        logger.info("返回上一页")
        self.adb.press_back()

        if self.current_path:
            self.current_path.pop()

        return True

    def _execute_swipe(self, target: Dict, params: Dict) -> bool:
        """执行滑动操作。

        Args:
            target: 滑动目标
            params: 参数

        Returns:
            是否成功
        """
        direction = target.get("direction", "down")
        logger.info(f"滑动: {direction}")

        # 实现滑动逻辑
        if direction == "down":
            self.adb.swipe(0.5, 0.7, 0.5, 0.3)
        elif direction == "up":
            self.adb.swipe(0.5, 0.3, 0.5, 0.7)
        elif direction == "left":
            self.adb.swipe(0.7, 0.5, 0.3, 0.5)
        elif direction == "right":
            self.adb.swipe(0.3, 0.5, 0.7, 0.5)

        return True

    def _execute_scroll(self, params: Dict) -> bool:
        """执行滚动操作。

        Args:
            params: 参数

        Returns:
            是否成功
        """
        logger.info("向下滚动")
        self.adb.swipe(0.5, 0.8, 0.5, 0.2)
        return True

    def _execute_wait(self, params: Dict) -> bool:
        """执行等待操作。

        Args:
            params: 参数

        Returns:
            是否成功
        """
        import time
        wait_seconds = params.get("seconds", 2)
        logger.info(f"等待 {wait_seconds} 秒")
        time.sleep(wait_seconds)
        return True

    def execute_plan(self, plan: TraversalPlan) -> Dict:
        """执行遍历计划。

        Args:
            plan: 遍历计划

        Returns:
            执行结果摘要
        """
        logger.info("开始执行遍历计划...")
        logger.info("=" * 60)

        result = {
            "total_steps": 0,
            "successful_steps": 0,
            "failed_nodes": [],
            "visited_pages": list(self.visited_pages),
        }

        # 执行根节点
        if self.execute_node(plan.root_node):
            result["successful_steps"] += 1

        result["total_steps"] = self.step_count
        result["failed_nodes"] = self.failed_nodes
        result["visited_pages"] = list(self.visited_pages)

        return result

    def print_summary(self, result: Dict) -> None:
        """打印执行摘要。

        Args:
            result: 执行结果
        """
        print("\n" + "=" * 60)
        print("遍历完成")
        print("=" * 60)
        print(f"总步数: {result['total_steps']}")
        print(f"成功步数: {result['successful_steps']}")
        print(f"失败节点: {len(result['failed_nodes'])}")
        if result['failed_nodes']:
            print(f"失败列表: {result['failed_nodes']}")
        print(f"已访问页面: {len(result['visited_pages'])}")
        print("=" * 60)


def main():
    """主入口。"""
    parser = argparse.ArgumentParser(description="Uni-Brain Traversal Executor")
    parser.add_argument("instruction", help="用户指令（如：遍历所有系统设置的选项）")
    parser.add_argument("--device", default="127.0.0.1:6555", help="ADB 设备 ID")
    parser.add_argument("--max-steps", type=int, default=200, help="最大遍历步数")
    parser.add_argument("--visualize", action="store_true", help="可视化遍历计划")

    args = parser.parse_args()

    try:
        # 创建执行器
        executor = BrainTraversalExecutor(
            device_id=args.device,
            max_steps=args.max_steps
        )

        # 1. 解析指令
        plan = executor.parse_instruction(args.instruction)

        # 2. 可视化计划
        if args.visualize:
            executor.visualize_plan(plan)

        # 3. 执行计划
        result = executor.execute_plan(plan)

        # 4. 打印摘要
        executor.print_summary(result)

    except KeyboardInterrupt:
        logger.info("\n用户中断")
        sys.exit(0)
    except Exception as e:
        logger.error(f"执行失败: {e}", exc_info=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
