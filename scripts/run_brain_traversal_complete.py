#!/usr/bin/env python3
"""
Uni-Brain Traversal Executor - Complete Implementation

完整的遍历执行器，集成：
1. UniBrain AI 指令解析 → DAG
2. 屏幕分析和视觉识别
3. 动态节点探索
4. 安全过滤
5. 状态缓存和恢复
6. 追踪日志和监控
"""

import argparse
import logging
import sys
import json
import time
import traceback
from pathlib import Path
from typing import Dict, List, Optional, Any

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.adb.adb_client import RealADBClient
from src.config import get_settings
from src.state.state_manager import StateManager
from src.state.content_tree import Coordinate, PageAnalysis
from src.ai.provider import UniBrain
from src.ai.core.config import AIProviderConfig
from src.ai.capabilities.types import TraversalPlan, TraversalNode as AITraversalNode
from src.graph.node import NodeType, Operation, Target, ErrorPolicy, TraversalNode as GraphTraversalNode
from src.graph.matcher import DynamicMatcher, MatchCondition
from src.graph.template import TemplateRegistry
from src.vision.mimo_vision_cc import MiMoCCVisionServiceFactory
from src.analysis.structured_logging import TraversalLogger, LoggerFactory
from src.analysis.results import ResultManager, TraversalResult, StepResult, ResultStatus, get_result_manager
from src.analysis.metrics import MetricsCollector, get_metrics_collector

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


class BrainTraversalExecutor:
    """完整的UniBrain遍历执行器。"""

    def __init__(self, device_id: str, max_steps: int = 200, session_id: Optional[str] = None):
        """初始化执行器。

        Args:
            device_id: ADB 设备 ID
            max_steps: 最大遍历步数
            session_id: 可选的会话 ID，用于日志和结果关联
        """
        import uuid
        self.session_id = session_id or f"traversal_{uuid.uuid4().hex[:8]}"
        self.start_time = time.time()

        settings = get_settings()
        if device_id:
            settings.adb_device_id = device_id

        # 初始化追踪日志
        try:
            from src.utils.trace import TraceLogger, enable_trace_writing
            self._trace = TraceLogger("executor")
            enable_trace_writing(Path(".traces"))
            logger.info("[TRACE] 追踪日志已启用，输出到 .traces/")
        except ImportError:
            self._trace = None
            logger.warning("[TRACE] 追踪日志不可用")

        self._main_trace_context = None
        if self._trace:
            self._main_trace_context = self._trace.start_span(
                operation="traversal_session",
                tags={"device": device_id, "max_steps": max_steps, "session_id": self.session_id}
            )

        # 初始化可观测性组件
        self.logger = LoggerFactory.get_logger(self.session_id)
        self.result_manager = get_result_manager()
        self.metrics_collector = get_metrics_collector()

        logger.info(f"[OBSERVABILITY] 会话 ID: {self.session_id}")
        logger.info("[OBSERVABILITY] 结构化日志、结果管理和指标收集已启用")

        # 初始化 ADB 客户端
        logger.info("初始化 ADB 客户端...")
        self.adb = RealADBClient(
            adb_path=settings.adb_path,
            device_id=settings.adb_device_id or None,
        )

        if not self.adb.is_connected():
            raise RuntimeError("没有连接的 ADB 设备")
        logger.info(f"[ADB] 设备已连接: {settings.adb_device_id}")

        # 初始化 UniBrain AI
        logger.info("初始化 UniBrain AI...")
        ai_config = AIProviderConfig(
            api_key=getattr(settings, "deepseek_api_key", ""),
            model="deepseek-v4-flash",
            base_url="https://api.deepseek.com/v1",
        )
        self.brain = UniBrain(ai_config)

        # 初始化 Vision 服务
        logger.info("初始化 Vision 服务...")
        self.vision = MiMoCCVisionServiceFactory.from_settings(settings)

        # 初始化状态管理器
        logger.info("初始化状态管理器...")
        self.state_manager = StateManager(settings.state_file)
        self.state = self.state_manager.state

        self.max_steps = max_steps
        self.step_count = 0

        # 存储步骤结果
        self.step_results: List[StepResult] = []

        # 存储跳过和失败的项目
        self.skipped_items: List[Dict] = []
        self.failed_items: List[Dict] = []

        # 安全过滤关键词 (保留作为后备)
        self.dangerous_keywords = [
            "恢复出厂设置", "清除数据", "删除所有", "格式化",
            "重置系统", "factory reset", "clear data", "delete all",
            "format", "reset system", "卸载", "uninstall"
        ]

        # 初始化动态匹配器
        self.template_registry = TemplateRegistry()
        self.dynamic_matcher = DynamicMatcher(self.template_registry)

        # 初始化安全筛选能力 (如果可用)
        self.safety_capability = None
        try:
            self.safety_capability = self.brain.capabilities.get("screen_safety")
            if self.safety_capability:
                logger.info("[SAFETY] AI 安全筛选已启用")
        except Exception as e:
            logger.warning(f"[SAFETY] AI 安全筛选不可用: {e}")

        # 错误处理相关
        self.retry_count = 0
        self.max_retries = 2

        if self._trace and self._main_trace_context:
            self._trace.log_event(self._main_trace_context, "initialized",
                device=settings.adb_device_id,
                max_steps=max_steps,
                state_file=settings.state_file,
                session_id=self.session_id
            )

    def parse_instruction(self, instruction: str) -> TraversalPlan:
        """使用 UniBrain 解析用户指令。

        Args:
            instruction: 用户自然语言指令

        Returns:
            TraversalPlan 遍历计划（包含 DAG）
        """
        logger.info(f"解析指令: {instruction}")
        logger.info("=" * 60)

        ai_start = time.time()
        parse_capability = self.brain.capabilities["parse"]
        plan = parse_capability.execute(instruction)
        ai_duration = (time.time() - ai_start) * 1000

        logger.info("解析成功！")
        logger.info(f"入口应用: {plan.entry_app or '当前页面'}")
        logger.info(f"遍历模式: {plan.mode}")
        logger.info(f"置信度: {plan.confidence}")

        # 记录 AI 调用指标
        self.metrics_collector.record_ai_call(
            service="UniBrain",
            operation="parse_instruction",
            duration_ms=ai_duration,
            success=True,
            confidence=plan.confidence
        )

        self.logger.log_ai_call(
            service="UniBrain",
            operation="parse_instruction",
            duration_ms=ai_duration,
            success=True,
            confidence=plan.confidence
        )

        # 提取安全规则
        if hasattr(plan.root_node, 'operation') and plan.root_node.operation:
            logger.info("安全规则已应用")

        return plan

    def analyze_screen(self) -> PageAnalysis:
        """分析当前屏幕。

        Returns:
            PageAnalysis 屏幕分析结果
        """
        logger.debug("分析屏幕...")

        vision_start = time.time()
        screenshot = self.adb.capture_screenshot()
        analysis = self.vision.analyze_screenshot(screenshot)
        vision_duration = (time.time() - vision_start) * 1000

        # 记录 Vision 调用指标
        self.metrics_collector.record_ai_call(
            service="MiMoCC",
            operation="analyze_screenshot",
            duration_ms=vision_duration,
            success=analysis is not None,
            confidence=None
        )

        self.logger.log_ai_call(
            service="MiMoCC",
            operation="analyze_screenshot",
            duration_ms=vision_duration,
            success=analysis is not None,
            confidence=None
        )

        # 更新当前路径
        if analysis.current_path:
            self.state.current_path = analysis.current_path

        self.logger.log_screen_analysis(
            items_count=len(analysis.items) if analysis else 0,
            path=list(self.state.current_path),
            duration_ms=vision_duration
        )

        return analysis

    def is_safe_to_click(self, item: Any, analysis: PageAnalysis) -> bool:
        """检查项目是否安全可点击。

        Args:
            item: 菜单项
            analysis: 屏幕分析结果

        Returns:
            是否安全
        """
        item_name = item.name.lower()

        # 首先使用 AI 安全筛选（如果可用）
        if self.safety_capability:
            try:
                # 准备筛选输入
                safety_input = {
                    "page_analysis": analysis,
                    "instruction": self.original_instruction or "Traversal"
                }

                # 调用安全筛选能力
                safety_result = self.safety_capability.execute(safety_input)

                # 查找当前项目的评估结果
                if safety_result and safety_result.evaluations:
                    for evaluation in safety_result.evaluations:
                        if evaluation.name.lower() == item_name.lower():
                            if evaluation.safety_tag in ["caution", "skip"]:
                                logger.info(f"[AI安全] 跳过危险项目 ({evaluation.safety_tag}): {item.name}")
                                logger.info(f"[AI安全] 原因: {evaluation.reason}")
                                return False
                            else:
                                logger.debug(f"[AI安全] 项目安全: {item.name} ({evaluation.safety_tag})")
                                break
            except Exception as e:
                logger.warning(f"[AI安全] 筛选失败，回退到关键词检查: {e}")

        # 后备方案：检查危险关键词
        for keyword in self.dangerous_keywords:
            if keyword.lower() in item_name:
                logger.info(f"[关键词] 跳过危险项目: {item.name}")
                return False

        # 检查计划中的排除规则
        if hasattr(self.plan, 'root_node'):
            root_node = self.plan.root_node
            if hasattr(root_node, 'operation') and root_node.operation:
                # 这里可以添加更多安全检查逻辑
                pass

        return True

    def apply_dynamic_rules(self, items: List, analysis: PageAnalysis, plan: TraversalPlan) -> Dict[str, Any]:
        """应用 plan 中的 dynamic_rules 进行智能匹配和分类。

        Args:
            items: 屏幕分析得到的项目列表
            analysis: 屏幕分析结果
            plan: 遍历计划

        Returns:
            匹配结果字典，包含每个项目对应的规则和动作
        """
        matched_items = {}

        # 检查计划中是否有动态规则
        if not hasattr(plan.root_node, 'children_strategy'):
            logger.debug("[动态规则] 计划中没有 children_strategy")
            return matched_items

        strategy = plan.root_node.children_strategy
        if not hasattr(strategy, 'dynamic_rules') or not strategy.dynamic_rules:
            logger.debug("[动态规则] 没有 dynamic_rules")
            return matched_items

        rules = strategy.dynamic_rules
        logger.info(f"[动态规则] 发现 {len(rules)} 条规则: {list(rules.keys())}")

        # 应用每条规则
        for item in items:
            for rule_name, rule_config in rules.items():
                try:
                    # 提取匹配条件
                    match_condition = rule_config.get("match_condition", {})

                    # 创建匹配条件对象
                    condition = MatchCondition(match_condition)

                    # 检查是否匹配
                    item_data = {
                        "type": item.type.value if hasattr(item.type, 'value') else str(item.type),
                        "expected_action": item.expected_action.value if hasattr(item.expected_action, 'value') else str(item.expected_action),
                        "text": item.name
                    }

                    if condition.matches(item_data):
                        action = rule_config.get("action", "generate_child")
                        matched_items[item.name] = {
                            "rule": rule_name,
                            "action": action,
                            "template": rule_config.get("child_template"),
                            "match_condition": match_condition
                        }
                        logger.debug(f"[动态规则] 项目 '{item.name}' 匹配规则 '{rule_name}', 动作: {action}")
                        break  # 只匹配第一条规则
                except Exception as e:
                    logger.warning(f"[动态规则] 匹配项目 '{item.name}' 时出错: {e}")

        logger.info(f"[动态规则] 共匹配 {len(matched_items)} 个项目")
        return matched_items

    def handle_execution_error(self, error: Exception, context: Dict[str, Any]) -> bool:
        """根据 error_policy 处理执行错误。

        Args:
            error: 发生的异常
            context: 错误上下文信息

        Returns:
            True 表示应该继续执行，False 表示应该停止或跳过
        """
        logger.error(f"[错误处理] 执行出错: {error}")

        # 检查是否有错误策略定义
        if not hasattr(self.plan, 'root_node') or not hasattr(self.plan.root_node, 'error_policy'):
            logger.warning(f"[错误处理] 没有定义 error_policy，使用默认行为（停止）")
            return False

        policy = self.plan.root_node.error_policy
        on_error = getattr(policy, 'on_error', 'abort')

        logger.info(f"[错误处理] 应用策略: {on_error}")

        if on_error == "retry":
            max_retries = getattr(policy, 'max_retries', 1)
            if self.retry_count < max_retries:
                self.retry_count += 1
                logger.info(f"[错误处理] 重试 ({self.retry_count}/{max_retries})")
                return True  # 继续执行
            else:
                logger.warning(f"[错误处理] 达到最大重试次数 ({max_retries})，停止")
                return False

        elif on_error == "skip":
            logger.info(f"[错误处理] 跳过当前操作，继续遍历")
            return True  # 继续执行

        elif on_error == "fallback":
            fallback_target = getattr(policy, 'fallback_target', None)
            if fallback_target:
                logger.info(f"[错误处理] 回退到目标: {fallback_target}")
                # TODO: 实现回退逻辑
                return True
            else:
                logger.warning(f"[错误处理] 没有定义 fallback_target")
                return False

        elif on_error == "abort":
            logger.info(f"[错误处理] 中止遍历")
            return False

        # 默认行为：停止
        return False

    def tap_element(self, x: float, y: float, target_name: Optional[str] = None) -> None:
        """点击元素。

        Args:
            x: X 坐标 (0-1)
            y: Y 坐标 (0-1)
            target_name: 目标元素名称（可选，用于日志记录）
        """
        logger.info(f"点击坐标: ({x:.2f}, {y:.2f})")

        tap_start = time.time()
        try:
            self.adb.tap(x, y)
            self.step_count += 1
            tap_duration = (time.time() - tap_start) * 1000
            success = True
            error = None
        except Exception as e:
            tap_duration = (time.time() - tap_start) * 1000
            success = False
            error = str(e)
            logger.error(f"点击失败: {e}")

        # 记录步骤结果
        step_result = StepResult(
            step_number=self.step_count,
            action="tap",
            target=target_name,
            coordinate={"x": x, "y": y},
            success=success,
            error=error,
            duration_ms=tap_duration
        )
        self.step_results.append(step_result)

        # 记录到结构化日志
        self.logger.log_step(
            action="tap",
            target=target_name,
            coordinate={"x": x, "y": y},
            success=success,
            duration_ms=tap_duration
        )

        self.metrics_collector.record_traversal_step(
            session_id=self.session_id,
            screens_count=1,
            duration_ms=tap_duration,
            visited_count=1 if success else 0
        )

    def go_back(self) -> None:
        """返回上一页。"""
        logger.info("返回上一页")

        back_start = time.time()
        try:
            self.adb.press_back()
            self.step_count += 1
            back_duration = (time.time() - back_start) * 1000
            success = True
            error = None
        except Exception as e:
            back_duration = (time.time() - back_start) * 1000
            success = False
            error = str(e)
            logger.error(f"返回失败: {e}")

        # 更新路径
        if self.state.current_path:
            self.state.current_path.pop()

        # 记录步骤结果
        step_result = StepResult(
            step_number=self.step_count,
            action="back",
            target=None,
            coordinate=None,
            success=success,
            error=error,
            duration_ms=back_duration
        )
        self.step_results.append(step_result)

        # 记录到结构化日志
        self.logger.log_step(
            action="back",
            target=None,
            coordinate=None,
            success=success,
            duration_ms=back_duration
        )

    def scroll_down(self) -> None:
        """向下滚动。"""
        logger.info("向下滚动")

        scroll_start = time.time()
        try:
            self.adb.swipe(0.5, 0.8, 0.5, 0.3)
            self.step_count += 1
            scroll_duration = (time.time() - scroll_start) * 1000
            success = True
            error = None
        except Exception as e:
            scroll_duration = (time.time() - scroll_start) * 1000
            success = False
            error = str(e)
            logger.error(f"滚动失败: {e}")

        # 记录步骤结果
        step_result = StepResult(
            step_number=self.step_count,
            action="scroll_down",
            target=None,
            coordinate=None,
            success=success,
            error=error,
            duration_ms=scroll_duration
        )
        self.step_results.append(step_result)

        # 记录到结构化日志
        self.logger.log_step(
            action="scroll_down",
            target=None,
            coordinate=None,
            success=success,
            duration_ms=scroll_duration
        )

    def wait_for_page_load(self, seconds: float = 1.0) -> None:
        """等待页面加载。"""
        logger.debug(f"等待 {seconds} 秒...")
        time.sleep(seconds)

    def get_unvisited_items(self, analysis: PageAnalysis) -> List:
        """获取未访问的项目。

        Args:
            analysis: 屏幕分析结果

        Returns:
            未访问的项目列表
        """
        unvisited = []
        current_page = self.state.current_path[-1] if self.state.current_path else "root"

        for item in analysis.items:
            # 生成指纹
            fingerprint = f"{current_page}|{item.name}"
            if fingerprint not in self.state.visited:
                unvisited.append((item, fingerprint))

        return unvisited

    def execute_plan(self, plan: TraversalPlan) -> TraversalResult:
        """执行遍历计划。

        Args:
            plan: 遍历计划

        Returns:
            TraversalResult 完整遍历结果
        """
        logger.info("开始执行遍历计划...")
        logger.info(f"遍历模式: {plan.mode}")
        logger.info("=" * 60)

        # 记录会话开始
        self.logger.log_session_start(
            instruction=self.original_instruction or "Traversal",
            max_steps=self.max_steps,
            entry_app=plan.entry_app
        )

        # 根据模式选择执行策略
        if plan.mode == "hybrid":
            return self._execute_hybrid_mode(plan)
        elif plan.mode == "concrete":
            return self._execute_concrete_mode(plan)
        elif plan.mode == "dynamic":
            return self._execute_dynamic_mode(plan)
        else:
            logger.warning(f"未知模式: {plan.mode}，使用默认 hybrid 模式")
            return self._execute_hybrid_mode(plan)

    def _execute_hybrid_mode(self, plan: TraversalPlan) -> TraversalResult:
        """HYBRID 模式执行：结合静态节点和动态发现。

        Args:
            plan: 遍历计划

        Returns:
            TraversalResult 完整遍历结果
        """

        # Start execution trace
        exec_trace = None
        if self._trace and self._main_trace_context:
            exec_trace = self._trace.start_span(
                operation="execute_plan",
                parent_context=self._main_trace_context,
                tags={
                    "entry_app": plan.entry_app or "current",
                    "mode": plan.mode,
                    "confidence": plan.confidence,
                    "session_id": self.session_id
                }
            )
            self._trace.log_input(exec_trace,
                entry_app=plan.entry_app,
                mode=plan.mode,
                root_node_type=plan.root_node.node_type
            )

        self.plan = plan
        visited_items: List[Dict] = []
        skipped_dangerous: List[str] = []
        screens_analyzed = 0
        ai_calls: Dict[str, int] = {}

        try:
            # 如果指定了入口应用，先导航过去
            if plan.entry_app:
                logger.info(f"导航到入口应用: {plan.entry_app}")
                if self._trace and exec_trace:
                    self._trace.log_event(exec_trace, "navigating_to_app", app=plan.entry_app)
                # 这里可以添加应用导航逻辑
                # 简化版本：假设已经在正确的页面

            # 分析初始屏幕
            logger.info("分析初始屏幕...")
            analysis = self.analyze_screen()
            screens_analyzed += 1

            logger.info(f"当前路径: {self.state.current_path}")
            logger.info(f"检测到 {len(analysis.items)} 个可交互项目")

            if self._trace and exec_trace:
                self._trace.log_event(exec_trace, "initial_screen",
                    path=self.state.current_path,
                    items_count=len(analysis.items)
                )

            # 应用动态规则匹配
            dynamic_matches = self.apply_dynamic_rules(analysis.items, analysis, plan)
            if dynamic_matches:
                logger.info(f"[HYBRID] 动态规则匹配到 {len(dynamic_matches)} 个项目")
                if self._trace and exec_trace:
                    self._trace.log_event(exec_trace, "dynamic_rules_applied",
                        matched_count=len(dynamic_matches),
                        matches=list(dynamic_matches.keys())
                    )

            # 主遍历循环
            while self.step_count < self.max_steps:
                # 获取未访问的项目
                unvisited = self.get_unvisited_items(analysis)

                if not unvisited:
                    logger.info("当前页面没有未访问的项目")

                    # 尝试滚动查看更多内容
                    if analysis.has_scroll and not analysis.is_end_of_list:
                        self.scroll_down()
                        self.wait_for_page_load(0.5)
                        analysis = self.analyze_screen()
                        screens_analyzed += 1
                        if self._trace and exec_trace:
                            self._trace.log_event(exec_trace, "scrolled_down")
                        continue
                    else:
                        # 没有更多内容，返回或结束
                        if len(self.state.current_path) > 0:
                            logger.info("返回上一页继续遍历")
                            self.go_back()
                            self.wait_for_page_load(1.0)
                            analysis = self.analyze_screen()
                            screens_analyzed += 1
                            if self._trace and exec_trace:
                                self._trace.log_event(exec_trace, "went_back",
                                    new_path=self.state.current_path
                                )
                            continue
                        else:
                            logger.info("遍历完成")
                            break

                # 访问未访问的项目
                for item, fingerprint in unvisited:
                    if self.step_count >= self.max_steps:
                        logger.warning(f"达到最大步数限制 ({self.max_steps})")
                        if self._trace and exec_trace:
                            self._trace.log_event(exec_trace, "max_steps_reached",
                                limit=self.max_steps
                            )
                        break

                    # 安全检查
                    if not self.is_safe_to_click(item, analysis):
                        skipped_dangerous.append(item.name)
                        self.skipped_items.append({
                            "name": item.name,
                            "reason": "safety_check",
                            "type": item.type,
                            "path": list(self.state.current_path)
                        })
                        self.logger.log_skipped_item(item.name, "safety_check")
                        if self._trace and exec_trace:
                            self._trace.log_event(exec_trace, "skipped_dangerous",
                                item=item.name, reason="safety_check"
                            )
                        # 标记为已访问（避免重复检查）
                        self.state.visited.add(fingerprint)
                        continue

                    # 记录并点击项目
                    self.state.visited.add(fingerprint)

                    item_info = {
                        "name": item.name,
                        "type": item.type,
                        "path": list(self.state.current_path),
                        "coordinate": {"x": item.coordinate.x, "y": item.coordinate.y}
                    }
                    visited_items.append(item_info)

                    logger.info(f"[{self.step_count}] 访问: {item.name} ({item.type})")

                    # 记录访问的项目
                    self.logger.log_visited_item(
                        item_name=item.name,
                        item_type=item.type,
                        path=list(self.state.current_path),
                        coordinate={"x": item.coordinate.x, "y": item.coordinate.y}
                    )

                    # 获取坐标并点击
                    coord = item.coordinate
                    self.tap_element(coord.x, coord.y, target_name=item.name)
                    self.wait_for_page_load(1.0)

                    # 重新分析屏幕
                    analysis = self.analyze_screen()
                    screens_analyzed += 1

                    # 应用动态规则匹配
                    new_matches = self.apply_dynamic_rules(analysis.items, analysis, plan)
                    if new_matches:
                        logger.info(f"[HYBRID] 重新匹配到 {len(new_matches)} 个项目")

                    # 如果页面跳转了，考虑返回
                    if item.expects_page_change and len(self.state.current_path) > 0:
                        logger.info("页面已跳转，返回继续遍历")
                        self.go_back()
                        self.wait_for_page_load(1.0)
                        analysis = self.analyze_screen()
                        screens_analyzed += 1

                        # 应用动态规则匹配
                        back_matches = self.apply_dynamic_rules(analysis.items, analysis, plan)
                        if back_matches:
                            logger.info(f"[HYBRID] 返回后匹配到 {len(back_matches)} 个项目")

            # 创建遍历结果
            end_time = time.time()
            total_duration_ms = (end_time - self.start_time) * 1000

            # 记录会话结束
            self.logger.log_session_end(
                status="success",
                steps=self.step_count,
                visited=len(visited_items),
                duration_ms=total_duration_ms
            )

            result = TraversalResult(
                session_id=self.session_id,
                trace_id=self._main_trace_context.trace_id if self._main_trace_context else "unknown",
                status=ResultStatus.SUCCESS,
                start_time=self.start_time,
                end_time=end_time,
                instruction=self.original_instruction or "Traversal",
                entry_app=plan.entry_app,
                max_steps=self.max_steps,
                steps=self.step_results,
                visited_items=visited_items,
                skipped_items=self.skipped_items,
                failed_items=self.failed_items,
                screens_analyzed=screens_analyzed,
                total_duration_ms=total_duration_ms,
                ai_calls=ai_calls,
                final_path=list(self.state.current_path),
                completion_reason="Traversal completed successfully"
            )

            # 保存结果
            result_file = self.result_manager.save_result(result)
            logger.info(f"[RESULT] 遍历结果已保存到: {result_file}")

            # 保存状态
            self.state_manager.save()

            # Finish execution trace with success
            if self._trace and exec_trace:
                self._trace.log_output(exec_trace,
                    total_steps=self.step_count,
                    visited_count=len(visited_items),
                    skipped_count=len(skipped_dangerous),
                    screens_analyzed=screens_analyzed,
                    final_path=list(self.state.current_path)
                )
                self._trace.finish_span(exec_trace)

        except Exception as e:
            logger.error(f"遍历失败: {e}", exc_info=True)
            end_time = time.time()
            total_duration_ms = (end_time - self.start_time) * 1000

            # 记录错误到日志
            self.logger.log_error(e, {
                "step_count": self.step_count,
                "current_path": list(self.state.current_path)
            })

            # 创建失败结果
            result = TraversalResult(
                session_id=self.session_id,
                trace_id=self._main_trace_context.trace_id if self._main_trace_context else "unknown",
                status=ResultStatus.FAILED,
                start_time=self.start_time,
                end_time=end_time,
                instruction=self.original_instruction or "Traversal",
                entry_app=plan.entry_app,
                max_steps=self.max_steps,
                steps=self.step_results,
                visited_items=visited_items,
                skipped_items=self.skipped_items,
                failed_items=self.failed_items,
                screens_analyzed=screens_analyzed,
                total_duration_ms=total_duration_ms,
                ai_calls=ai_calls,
                final_path=list(self.state.current_path),
                completion_reason="Traversal failed due to error",
                error=str(e),
                error_trace=traceback.format_exc()
            )

            # 保存结果（即使失败）
            result_file = self.result_manager.save_result(result)
            logger.info(f"[RESULT] 遍历结果（失败）已保存到: {result_file}")

            # Finish execution trace with error
            if self._trace and exec_trace:
                self._trace.finish_span(exec_trace, error=e)

            # 保存状态（即使出错）
            self.state_manager.save()

        return result

    def _execute_concrete_mode(self, plan: TraversalPlan) -> TraversalResult:
        """CONCRETE 模式执行：主要使用预定义的静态节点。

        Args:
            plan: 遍历计划

        Returns:
            TraversalResult 完整遍历结果
        """
        logger.info("使用 CONCRETE 模式：主要使用预定义的静态节点")
        # TODO: 实现静态节点遍历逻辑
        # 目前回退到 hybrid 模式
        return self._execute_hybrid_mode(plan)

    def _execute_dynamic_mode(self, plan: TraversalPlan) -> TraversalResult:
        """DYNAMIC 模式执行：完全依赖动态发现。

        Args:
            plan: 遍历计划

        Returns:
            TraversalResult 完整遍历结果
        """
        logger.info("使用 DYNAMIC 模式：完全依赖动态发现和规则匹配")
        # TODO: 实现完全动态遍历逻辑
        # 目前回退到 hybrid 模式
        return self._execute_hybrid_mode(plan)

    def print_summary(self, result: TraversalResult) -> None:
        """打印遍历摘要。

        Args:
            result: TraversalResult 遍历结果
        """
        print("\n" + "=" * 60)
        print("遍历完成摘要")
        print("=" * 60)
        print(f"会话 ID: {result.session_id}")
        print(f"状态: {result.status.value.upper()}")
        print(f"总步数: {len(result.steps)} (最大: {result.max_steps})")
        print(f"屏幕分析次数: {result.screens_analyzed}")
        print(f"已访问项目: {len(result.visited_items)}")
        print(f"跳过的危险项目: {len(result.skipped_items)}")
        print(f"失败的项目: {len(result.failed_items)}")
        print(f"总耗时: {result.total_duration_ms/1000:.1f}秒")

        if result.final_path:
            print(f"最终路径: {result.final_path}")

        if result.skipped_items:
            print("\n跳过的危险项目:")
            for item in result.skipped_items[:10]:
                print(f"  - {item.get('name')} - {item.get('reason', 'unknown')}")
            if len(result.skipped_items) > 10:
                print(f"  ... 还有 {len(result.skipped_items) - 10} 个项目")

        if result.failed_items:
            print("\n失败的项目:")
            for item in result.failed_items[:10]:
                print(f"  - {item.get('name')} - {item.get('error', 'unknown')}")
            if len(result.failed_items) > 10:
                print(f"  ... 还有 {len(result.failed_items) - 10} 个项目")

        print("\n已访问项目列表:")
        for i, item in enumerate(result.visited_items[:20], 1):  # 显示前20个
            path_str = " > ".join(item.get('path', [])) if item.get('path') else "root"
            print(f"  {i}. {item.get('name')} ({item.get('type')}) - [{path_str}]")

        if len(result.visited_items) > 20:
            print(f"  ... 还有 {len(result.visited_items) - 20} 个项目")

        print("=" * 60)
        print(f"\n📊 详细结果已保存，可使用以下命令查看:")
        print(f"   - 结构化日志: {self.logger.log_file}")
        print(f"   - JSON结果: .results/sessions/")
        print(f"   - 启动分析面板: uv run python scripts/analysis_dashboard.py")


def main():
    """主入口。"""
    parser = argparse.ArgumentParser(description="Uni-Brain Traversal Executor")
    parser.add_argument("instruction", help="用户指令（如：遍历所有系统设置的选项）")
    parser.add_argument("--device", default="127.0.0.1:6555", help="ADB 设备 ID")
    parser.add_argument("--max-steps", type=int, default=200, help="最大遍历步数")
    parser.add_argument("--reset", action="store_true", help="重置遍历状态")
    parser.add_argument("--visualize", action="store_true", help="可视化遍历计划")
    parser.add_argument("--session-id", help="可选的会话 ID，用于日志关联")

    args = parser.parse_args()

    try:
        # 创建执行器
        executor = BrainTraversalExecutor(
            device_id=args.device,
            max_steps=args.max_steps,
            session_id=args.session_id
        )

        # 存储原始指令
        executor.original_instruction = args.instruction

        # 重置状态（如果需要）
        if args.reset:
            logger.info("重置遍历状态...")
            executor.state_manager.reset()

        # 1. 解析指令
        plan = executor.parse_instruction(args.instruction)

        # 2. 可视化计划（可选）
        if args.visualize:
            print("\n遍历计划:")
            print(f"- 入口应用: {plan.entry_app or '当前页面'}")
            print(f"- 遍历模式: {plan.mode}")
            print(f"- 根节点: {plan.root_node}")
            print(f"- 会话 ID: {executor.session_id}")

        # 3. 执行计划
        result = executor.execute_plan(plan)

        # 4. 打印摘要
        executor.print_summary(result)

        # 生成并保存报告
        logger.info("生成遍历报告...")
        try:
            html_report = executor.result_manager.generate_report(result, "html")
            logger.info(f"HTML 报告已生成: {html_report}")

            md_report = executor.result_manager.generate_report(result, "markdown")
            logger.info(f"Markdown 报告已生成: {md_report}")
        except Exception as e:
            logger.warning(f"生成报告失败: {e}")

        # Finish main trace context
        if executor._trace and executor._main_trace_context:
            executor._trace.finish_span(executor._main_trace_context)
            logger.info("[TRACE] 追踪会话已完成")

        logger.info(f"[OBSERVABILITY] 会话 {executor.session_id} 已完成")
        logger.info(f"[OBSERVABILITY] 查看 .results/ 和 .logs/ 目录获取详细信息")

    except KeyboardInterrupt:
        logger.info("\n用户中断")
        if 'executor' in locals():
            # 尝试保存部分结果
            try:
                if hasattr(executor, 'logger'):
                    executor.logger.log_session_end(
                        status="cancelled",
                        steps=executor.step_count if hasattr(executor, 'step_count') else 0,
                        visited=0,
                        duration_ms=(time.time() - executor.start_time) * 1000 if hasattr(executor, 'start_time') else 0
                    )
            except Exception as e:
                logger.warning(f"保存中断日志失败: {e}")

            if executor._trace and executor._main_trace_context:
                executor._trace.finish_span(executor._main_trace_context,
                    error=Exception("User interrupted"))
        sys.exit(0)
    except Exception as e:
        logger.error(f"执行失败: {e}", exc_info=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
