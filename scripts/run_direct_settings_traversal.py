#!/usr/bin/env python3
"""
Direct Settings Traversal - 简化版

直接执行"遍历所有系统设置的选项"任务，无需外部LLM API调用。
"""

import argparse
import logging
import sys
import json
from pathlib import Path
from typing import Dict, List, Optional

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.adb.adb_client import RealADBClient
from src.config import get_settings
from src.state.state_manager import StateManager
from src.state.content_tree import Coordinate, PageAnalysis
from src.vision.mimo_vision_cc import MiMoCCVisionServiceFactory

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


class DirectSettingsTraversal:
    """直接执行设置遍历任务。"""

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

        # 初始化 Vision 服务
        logger.info("初始化 Vision 服务...")
        self.vision = MiMoCCVisionServiceFactory.from_settings(settings)

        self.max_steps = max_steps
        self.step_count = 0

        # 状态跟踪
        self.visited_items = set()
        self.current_path = []
        self.content_tree = []

    def analyze_screen(self) -> PageAnalysis:
        """分析当前屏幕。

        Returns:
            PageAnalysis 屏幕分析结果
        """
        logger.info("分析屏幕...")
        screenshot = self.adb.capture_screenshot()
        analysis = self.vision.analyze_screenshot(screenshot)
        return analysis

    def tap_element(self, x: float, y: float) -> None:
        """点击元素。

        Args:
            x: X 坐标 (0-1)
            y: Y 坐标 (0-1)
        """
        logger.info(f"点击坐标: ({x:.2f}, {y:.2f})")
        self.adb.tap(x, y)
        self.step_count += 1

    def go_back(self) -> None:
        """返回上一页。"""
        logger.info("返回上一页")
        self.adb.press_back()
        self.step_count += 1

    def scroll_down(self) -> None:
        """向下滚动。"""
        logger.info("向下滚动")
        self.adb.swipe(0.5, 0.8, 0.5, 0.3)
        self.step_count += 1

    def get_unvisited_items(self, analysis: PageAnalysis) -> List:
        """获取未访问的项目。

        Args:
            analysis: 屏幕分析结果

        Returns:
            未访问的项目列表
        """
        unvisited = []
        for item in analysis.items:
            # 生成指纹
            fingerprint = f"{self.current_path[-1] if self.current_path else 'root'}|{item.name}"
            if fingerprint not in self.visited_items:
                unvisited.append(item)
        return unvisited

    def execute_traversal(self) -> Dict:
        """执行设置遍历。

        Returns:
            遍历结果摘要
        """
        logger.info("开始执行设置遍历...")
        logger.info("=" * 60)

        result = {
            "total_steps": 0,
            "visited_items": [],
            "content_tree": [],
        }

        try:
            # 分析初始屏幕
            analysis = self.analyze_screen()
            logger.info(f"检测到 {len(analysis.items)} 个项目")

            # 遍历所有未访问的项目
            unvisited = self.get_unvisited_items(analysis)

            for item in unvisited:
                if self.step_count >= self.max_steps:
                    logger.warning(f"达到最大步数限制 ({self.max_steps})")
                    break

                # 跳过危险项目
                if self._is_dangerous_item(item):
                    logger.info(f"跳过危险项目: {item.name}")
                    continue

                # 记录并点击项目
                fingerprint = f"{self.current_path[-1] if self.current_path else 'root'}|{item.name}"
                self.visited_items.add(fingerprint)
                result["visited_items"].append(fingerprint)

                logger.info(f"访问项目: {item.name} ({item.type})")

                # 获取坐标
                coord = item.coordinate
                self.tap_element(coord.x, coord.y)

                # 等待页面加载
                import time
                time.sleep(1)

                # 如果是导航类型，可能需要返回
                if item.type in ["menu_item", "tab"]:
                    # 可以选择返回或继续深入
                    # 这里简化处理：返回上一页继续遍历
                    time.sleep(1)
                    self.go_back()
                    time.sleep(1)

            result["total_steps"] = self.step_count
            result["content_tree"] = self.content_tree

        except Exception as e:
            logger.error(f"遍历失败: {e}", exc_info=True)
            result["error"] = str(e)

        return result

    def _is_dangerous_item(self, item) -> bool:
        """检查项目是否危险。

        Args:
            item: 菜单项

        Returns:
            是否危险
        """
        dangerous_keywords = [
            "恢复出厂设置", "清除数据", "删除", "卸载",
            "格式化", "重置", "恢复默认", "factory reset",
            "clear data", "delete", "uninstall", "format"
        ]

        item_name_lower = item.name.lower()
        for keyword in dangerous_keywords:
            if keyword.lower() in item_name_lower:
                return True

        return False

    def print_summary(self, result: Dict) -> None:
        """打印遍历摘要。

        Args:
            result: 遍历结果
        """
        print("\n" + "=" * 60)
        print("设置遍历完成")
        print("=" * 60)
        print(f"总步数: {result['total_steps']}")
        print(f"已访问项目: {len(result['visited_items'])}")
        print("\n已访问项目列表:")
        for i, item in enumerate(result['visited_items'], 1):
            print(f"  {i}. {item}")
        print("=" * 60)


def main():
    """主入口。"""
    parser = argparse.ArgumentParser(description="Direct Settings Traversal")
    parser.add_argument("--device", default="127.0.0.1:6555", help="ADB 设备 ID")
    parser.add_argument("--max-steps", type=int, default=200, help="最大遍历步数")

    args = parser.parse_args()

    try:
        # 创建执行器
        executor = DirectSettingsTraversal(
            device_id=args.device,
            max_steps=args.max_steps
        )

        # 执行遍历
        result = executor.execute_traversal()

        # 打印摘要
        executor.print_summary(result)

    except KeyboardInterrupt:
        logger.info("\n用户中断")
        sys.exit(0)
    except Exception as e:
        logger.error(f"执行失败: {e}", exc_info=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
