"""V6 测试 API 迁移辅助层

封装 V6.10.x → V6.14.0 API 差异，提供稳定的测试接口。

此模块提供测试辅助函数来隔离 API 变化，使测试代码更加稳定，
降低未来 API 变更对测试的影响。
"""

from typing import Optional, Dict, Any, List, Set
from src.state_machine.popup_handler import (
    PopupInfo,
    PopupType,
    UrgencyLevel,
    BlockingType,
)
from src.traversal.graph_engine import GraphTraversalEngine
from src.trace.context import TraversalRuntimeContext


class PopupTestHelper:
    """Popup 测试辅助类 - 封装 PopupInfo API 差异

    V6.10.x → V6.14.0 变更:
    - PopupInfo 构造函数字段完全不同
    - 枚举值部分重命名
    - 字段语义发生变化
    """

    # 旧 API 枚举值 → 新 API 枚举值映射
    _POPUP_TYPE_MAP = {
        "NOTIFICATION": PopupType.DIALOG,
        # 其他映射按需添加
    }

    _URGENCY_MAP = {
        "DEFERABLE": UrgencyLevel.LOW,
        # 其他映射按需添加
    }

    _BLOCKING_MAP = {
        "PARTIAL_BLOCK": BlockingType.NON_MODAL,
        "FULL_BLOCK": BlockingType.MODAL,
        "NON_BLOCKING": BlockingType.NON_MODAL,
        # 其他映射按需添加
    }

    @classmethod
    def create_from_old_style(
        cls,
        popup_type: Optional[str] = None,
        title: Optional[str] = None,
        element_id: Optional[str] = None,
        urgency: Optional[str] = None,
        blocking: Optional[str] = None,
        content: Optional[str] = None,
        screen_context: Optional[str] = None,
        action_buttons: Optional[List[str]] = None,
        dismissible: Optional[bool] = None,
        recurring: Optional[bool] = None,
        **kwargs
    ) -> PopupInfo:
        """
        从旧 API 风格创建 PopupInfo

        Args:
            popup_type: 旧 API 的 PopupType (如 "NOTIFICATION")
            title: 弹窗标题
            element_id: 元素 ID
            urgency: 旧 API 的 UrgencyLevel (如 "DEFERABLE")
            blocking: 旧 API 的 BlockingType (如 "PARTIAL_BLOCK")
            content: 弹窗内容
            screen_context: 屏幕上下文
            action_buttons: 操作按钮列表
            dismissible: 是否可关闭
            recurring: 是否重复出现
            **kwargs: 其他旧 API 参数

        Returns:
            符合新 API 的 PopupInfo 实例
        """
        # 映射枚举值
        mapped_type = cls._map_popup_type(popup_type, title)
        mapped_urgency = cls._URGENCY_MAP.get(urgency, UrgencyLevel.MEDIUM)
        mapped_blocking = cls._BLOCKING_MAP.get(blocking, BlockingType.MODAL)

        # 构建目标元素
        target_element = None
        if title or element_id:
            target_element = {
                "text": title or "",
                "element_id": element_id or "",
            }
            if content:
                target_element["content"] = content
            if screen_context:
                target_element["screen_context"] = screen_context
            if action_buttons:
                target_element["action_buttons"] = action_buttons

        return PopupInfo(
            popup_type=mapped_type,
            confidence=kwargs.get('confidence', 0.8),
            target_element=target_element,
            urgency_level=mapped_urgency,
            blocking_type=mapped_blocking,
            dismiss_strategy="auto_close" if dismissible else "wait_timeout",
            timeout_seconds=kwargs.get('timeout_seconds', 10),
        )

    @classmethod
    def _map_popup_type(cls, popup_type: Optional[str], title: Optional[str]) -> PopupType:
        """映射 PopupType"""
        if popup_type:
            mapped = cls._POPUP_TYPE_MAP.get(popup_type)
            if mapped:
                return mapped
            # 尝试直接从枚举获取
            try:
                return PopupType[popup_type]
            except KeyError:
                pass

        # 根据 title 推断
        if title:
            title_lower = title.lower()
            if "permission" in title_lower or "allow" in title_lower:
                return PopupType.PERMISSION
            elif "error" in title_lower or "failed" in title_lower:
                return PopupType.ERROR
            elif "ad" in title_lower or "sponsored" in title_lower:
                return PopupType.AD

        return PopupType.DIALOG


class DynamicChildTestHelper:
    """动态子节点测试辅助类 - 封装 DynamicChildManager API 差异

    V6.10.x → V6.14.0 变更:
    - 子节点管理从 GraphTraversalEngine 移至 DynamicChildManager
    - 方法签名发生变化
    - 缓存机制从主动失效变为自动失效（基于 page_fingerprint）
    """

    @staticmethod
    def generate_children(
        engine: GraphTraversalEngine,
        node,
        page_analysis: Optional[Dict[str, Any]] = None
    ) -> List:
        """
        兼容旧的 _generate_dynamic_children 调用

        Args:
            engine: GraphTraversalEngine 实例
            node: 父节点
            page_analysis: 页面分析 (可选，新 API 使用 context.current_page_analysis)

        Returns:
            生成的子节点列表
        """
        if page_analysis:
            # 如果提供了 page_analysis，更新 context
            engine.context.current_page_analysis = page_analysis

        engine._child_mgr.generate(node, engine.context)
        return engine._child_mgr._dynamic_children.get(node.node_id, [])

    @staticmethod
    def get_next_unvisited_child(
        engine: GraphTraversalEngine,
        node
    ) -> Optional[str]:
        """
        兼容旧的 _get_next_unvisited_child 调用

        Args:
            engine: GraphTraversalEngine 实例
            node: 父节点

        Returns:
            下一个未访问子节点的 ID，或 None
        """
        return engine._child_mgr.get_next_unvisited_child(node, engine.context)

    @staticmethod
    def has_unvisited_children(
        engine: GraphTraversalEngine,
        node
    ) -> Optional[bool]:
        """
        兼容旧的 has_unvisited_children 检查

        Args:
            engine: GraphTraversalEngine 实例
            node: 父节点

        Returns:
            True 如果有未访问子节点，False 如果没有，None 如果无法确定
        """
        return engine._child_mgr.has_unvisited(node, engine.context)

    @staticmethod
    def invalidate_cache(
        engine: GraphTraversalEngine,
        node_id: str
    ) -> None:
        """
        兼容旧的 invalidate_children_cache 调用

        注意：新架构使用 page_fingerprint 自动失效，
        此方法通过直接调用 invalidate 实现类似效果。

        Args:
            engine: GraphTraversalEngine 实例
            node_id: 节点 ID
        """
        engine._child_mgr.invalidate(node_id)

    @staticmethod
    def get_visited_children(
        engine: GraphTraversalEngine,
        node_id: str
    ) -> Set[str]:
        """
        兼容旧的 _visited_nodes 属性访问

        Args:
            engine: GraphTraversalEngine 实例
            node_id: 节点 ID

        Returns:
            已访问子节点集合
        """
        return engine.context.visited_children.get(node_id, set())

    @staticmethod
    def get_dynamic_children(
        engine: GraphTraversalEngine,
        node_id: str
    ) -> List:
        """
        访问动态子节点缓存

        Args:
            engine: GraphTraversalEngine 实例
            node_id: 节点 ID

        Returns:
            动态生成的子节点列表
        """
        return engine._child_mgr._dynamic_children.get(node_id, [])
