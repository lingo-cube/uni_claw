"""演示：从自然语言描述到图的实际转换

展示如何将"遍历系统设置所有项"这句话转换为具体的图结构
"""

from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum


# ============= 图数据结构定义 =============

class NodeType(str, Enum):
    START = "start"
    PAGE = "page"
    MENU = "menu"
    SUBMENU = "submenu"
    LIST_ITEM = "list_item"
    TOGGLE = "toggle"
    ACTION = "action"
    SKIP = "skip"
    END = "end"


@dataclass
class GraphNode:
    id: str
    title: str
    type: NodeType
    description: str = ""
    coordinate: Optional[Tuple[float, float]] = None
    action: str = "visit"  # visit, skip, tap
    visited: bool = False

    def to_dict(self):
        return {
            "id": self.id,
            "title": self.title,
            "type": self.type.value,
            "description": self.description,
            "action": self.action
        }


@dataclass
class GraphEdge:
    from_id: str
    to_id: str
    action: str  # navigate, tap, back, skip


@dataclass
class TraversalGraph:
    nodes: Dict[str, GraphNode]
    edges: List[GraphEdge]
    root_id: str = "root"
    name: str = "Graph"


# ============= AI 意图分析 =============

@dataclass
class UserIntent:
    action: str  # traverse_all, skip_modifies, read_only
    constraints: Dict[str, List[str]]
    target: str


def analyze_user_intent(description: str) -> UserIntent:
    """分析用户的自然语言意图

    模拟 AI 的意图分析过程
    """
    # 简化的关键词匹配 (实际会调用 AI)
    keywords = {
        "traverse": ["遍历", "访问", "查看"],
        "skip": ["不点击", "跳过", "不要", "避免"],
        "modify": ["设置", "修改", "更改", "删除"],
        "all": ["所有", "全部", "完整"]
    }

    intent = UserIntent(
        action="traverse_all",
        constraints={"skip_patterns": [], "max_depth": 10},
        target="all"
    )

    # 分析描述
    if "不点击" in description or "不要点击" in description:
        if "设置" in description or "修改" in description:
            intent.action = "skip_modifies"
            intent.constraints["skip_patterns"] = ["设置", "修改", "更改", "删除", "移除"]

    if "所有层级" in description or "全部" in description:
        intent.target = "all"
        intent.constraints["max_depth"] = 10

    return intent


# ============= 页面结构分析 =============

@dataclass
class PageAnalysis:
    current_path: List[str]
    level1_menus: List[Dict]
    level2_menus: List[Dict]
    items: List[Dict]
    is_root: bool = True


def mock_analyze_settings_page() -> PageAnalysis:
    """模拟分析系统设置页面

    这是一个典型的 Android 系统设置的结构
    """
    return PageAnalysis(
        current_path=["系统设置"],
        level1_menus=[
            {"id": "wifi", "name": "网络和互联网", "coordinate": (0.5, 0.1)},
            {"id": "bluetooth", "name": "已配对的设备", "coordinate": (0.5, 0.2)},
            {"id": "apps", "name": "应用", "coordinate": (0.5, 0.3)},
            {"id": "battery", "name": "电池", "coordinate": (0.5, 0.4)},
            {"id": "display", "name": "显示", "coordinate": (0.5, 0.5)},
            {"id": "sound", "name": "声音和振动", "coordinate": (0.5, 0.6)},
            {"id": "storage", "name": "存储", "coordinate": (0.5, 0.7)},
            {"id": "security", "name": "安全", "coordinate": (0.5, 0.8)},
        ],
        level2_menus=[],
        items=[],
        is_root=True
    )


def mock_analyze_submenu(menu_id: str) -> PageAnalysis:
    """模拟分析子菜单页面"""
    submenus = {
        "wifi": PageAnalysis(
            current_path=["系统设置", "网络和互联网"],
            level1_menus=[],
            level2_menus=[
                {"id": "wifi_menu", "name": "Wi‑Fi", "coordinate": (0.3, 0.1)},
                {"id": "sim", "name": "SIM 卡", "coordinate": (0.3, 0.2)},
                {"id": "hotspot", "name": "热点和网络共享", "coordinate": (0.3, 0.3)},
            ],
            items=[
                {"id": "wifi_toggle", "name": "使用 Wi‑Fi", "type": "toggle", "coordinate": (0.7, 0.1)},
                {"id": "wifi_network", "name": "ChinaNet-xxx", "type": "list_item", "coordinate": (0.5, 0.2)},
            ]
        ),
        "bluetooth": PageAnalysis(
            current_path=["系统设置", "已配对的设备"],
            level1_menus=[],
            level2_menus=[
                {"id": "bt_devices", "name": "蓝牙", "coordinate": (0.3, 0.1)},
            ],
            items=[
                {"id": "bt_toggle", "name": "使用蓝牙", "type": "toggle", "coordinate": (0.7, 0.1)},
                {"id": "bt_device1", "name": "AirPods", "type": "list_item", "coordinate": (0.5, 0.2)},
            ]
        ),
        "display": PageAnalysis(
            current_path=["系统设置", "显示"],
            level1_menus=[],
            level2_menus=[
                {"id": "brightness", "name": "亮度级别", "coordinate": (0.3, 0.1)},
                {"id": "wallpaper", "name": "壁纸", "coordinate": (0.3, 0.2)},
            ],
            items=[
                {"id": "dark_mode", "name": "深色模式", "type": "toggle", "coordinate": (0.7, 0.1)},
                {"id": "auto_brightness", "name": "自适应亮度", "type": "toggle", "coordinate": (0.7, 0.15)},
                {"id": "sleep", "name": "屏幕超时", "type": "action", "coordinate": (0.5, 0.3)},
            ]
        ),
    }

    return submenus.get(menu_id, PageAnalysis([], [], [], []))


# ============= 图构建器 =============

class GraphBuilder:
    """将页面分析和用户意图转换为遍历图"""

    def build(self, description: str) -> TraversalGraph:
        """从自然语言描述构建图

        Args:
            description: 用户的自然语言描述

        Returns:
            TraversalGraph: 构建的遍历图
        """
        # 1. 分析用户意图
        intent = analyze_user_intent(description)
        print(f"\n【1】用户意图分析:")
        print(f"  动作: {intent.action}")
        print(f"  目标: {intent.target}")
        print(f"  约束: {intent.constraints}")

        # 2. 分析初始页面
        page_analysis = mock_analyze_settings_page()
        print(f"\n【2】页面分析:")
        print(f"  当前路径: {page_analysis.current_path}")
        print(f"  一级菜单数: {len(page_analysis.level1_menus)}")

        # 3. 构建图
        graph = TraversalGraph(nodes={}, edges=[], root_id="root",
                              name=f"Graph({description[:20]}...)")

        # 添加根节点
        root = GraphNode("root", "开始", NodeType.START)
        graph.nodes["root"] = root

        # 添加设置页面节点
        settings_node = GraphNode(
            "settings",
            "系统设置",
            NodeType.PAGE,
            "系统设置首页"
        )
        graph.nodes["settings"] = settings_node
        graph.edges.append(GraphEdge("root", "settings", "navigate"))

        # 4. 根据意图和约束构建子节点
        self._build_children(graph, page_analysis, intent, "settings", depth=1)

        # 5. 添加结束节点
        end_node = GraphNode("end", "完成", NodeType.END)
        graph.nodes["end"] = end_node

        # 连接所有未完成的一级菜单到结束节点
        for menu in page_analysis.level1_menus:
            menu_id = f"l1_{menu['id']}"
            if menu_id in graph.nodes:
                graph.edges.append(GraphEdge(menu_id, "end", "complete"))

        return graph

    def _build_children(self, graph: TraversalGraph, analysis: PageAnalysis,
                        intent: UserIntent, parent_id: str, depth: int):
        """递归构建子节点"""

        if depth > intent.constraints.get("max_depth", 10):
            return

        skip_patterns = intent.constraints.get("skip_patterns", [])

        # 处理一级菜单
        for menu in analysis.level1_menus:
            menu_id = f"l1_{menu['id']}"
            menu_name = menu['name']

            # 检查是否需要跳过
            should_skip = any(pattern in menu_name for pattern in skip_patterns)

            node_type = NodeType.SKIP if should_skip else NodeType.MENU
            action = "skip" if should_skip else "tap"

            menu_node = GraphNode(
                menu_id,
                menu_name,
                node_type,
                f"一级菜单 - {menu_name}",
                coordinate=menu.get("coordinate"),
                action=action
            )
            graph.nodes[menu_id] = menu_node
            graph.edges.append(GraphEdge(parent_id, menu_id, "navigate"))

            # 如果不跳过，继续探索子菜单
            if not should_skip:
                submenu_analysis = mock_analyze_submenu(menu['id'])
                self._build_submenu_level(graph, submenu_analysis, intent,
                                        menu_id, depth + 1)

        # 处理页面上的项目 (开关、列表项等)
        for item in analysis.items:
            item_id = f"item_{item['id']}"
            item_name = item['name']

            # 检查是否需要跳过
            should_skip = any(pattern in item_name for pattern in skip_patterns)

            if item.get("type") == "toggle":
                node_type = NodeType.SKIP if should_skip else NodeType.TOGGLE
            elif item.get("type") == "action":
                node_type = NodeType.SKIP if should_skip else NodeType.ACTION
            else:
                node_type = NodeType.SKIP if should_skip else NodeType.LIST_ITEM

            action = "skip" if should_skip else "tap"

            item_node = GraphNode(
                item_id,
                item_name,
                node_type,
                f"{item.get('type', 'item')} - {item_name}",
                coordinate=item.get("coordinate"),
                action=action
            )
            graph.nodes[item_id] = item_node
            graph.edges.append(GraphEdge(parent_id, item_id, "tap" if not should_skip else "skip"))

    def _build_submenu_level(self, graph: TraversalGraph, analysis: PageAnalysis,
                            intent: UserIntent, parent_id: str, depth: int):
        """构建子菜单层级"""

        skip_patterns = intent.constraints.get("skip_patterns", [])

        # 处理二级菜单
        for menu in analysis.level2_menus:
            menu_id = f"{parent_id}_sub_{menu['id']}"
            menu_name = menu['name']

            should_skip = any(pattern in menu_name for pattern in skip_patterns)

            node_type = NodeType.SKIP if should_skip else NodeType.SUBMENU
            action = "skip" if should_skip else "tap"

            menu_node = GraphNode(
                menu_id,
                menu_name,
                node_type,
                f"二级菜单 - {menu_name}",
                coordinate=menu.get("coordinate"),
                action=action
            )
            graph.nodes[menu_id] = menu_node
            graph.edges.append(GraphEdge(parent_id, menu_id, "enter"))

        # 处理项目
        for item in analysis.items:
            item_id = f"{parent_id}_item_{item['id']}"
            item_name = item['name']

            should_skip = any(pattern in item_name for pattern in skip_patterns)

            if item.get("type") == "toggle":
                node_type = NodeType.SKIP if should_skip else NodeType.TOGGLE
            else:
                node_type = NodeType.SKIP if should_skip else NodeType.LIST_ITEM

            action = "skip" if should_skip else "tap"

            item_node = GraphNode(
                item_id,
                item_name,
                node_type,
                f"{item.get('type', 'item')} - {item_name}",
                coordinate=item.get("coordinate"),
                action=action
            )
            graph.nodes[item_id] = item_node
            graph.edges.append(GraphEdge(parent_id, item_id, "tap" if not should_skip else "skip"))


# ============= 可视化 =============

def visualize_graph(graph: TraversalGraph):
    """可视化图结构"""

    print(f"\n【图结构】: {graph.name}")
    print("=" * 70)

    # 打印节点列表
    print("\n📍 节点列表:")
    node_types = {}
    for node_id, node in graph.nodes.items():
        node_types[node.type.value] = node_types.get(node.type.value, 0) + 1
        marker = "🟢" if node.type == NodeType.START else \
                 "🔴" if node.type == NodeType.END else \
                 "🟡" if node.type == NodeType.SKIP else "⚪"
        skip_mark = " ⚠️ SKIP" if node.action == "skip" else ""
        print(f"  {marker} [{node_id}] {node.title} ({node.type.value}){skip_mark}")

    print(f"\n📊 节点统计: {node_types}")

    # 打印边列表
    print(f"\n🔗 边列表:")
    for edge in graph.edges:
        print(f"  {edge.from_id} --[{edge.action}]--> {edge.to_id}")

    # 打印遍历路径
    print(f"\n🛤️  预期遍历路径:")
    print_paths(graph, graph.root_id, [])

    # Mermaid 图表
    print(f"\n📊 Mermaid 图表:")
    print(graph.to_mermaid())


def print_paths(graph: TraversalGraph, node_id: str, path: List[str]):
    """递归打印所有路径"""
    node = graph.nodes.get(node_id)
    if not node:
        return

    current_path = path + [node_id]

    if node.type == NodeType.END:
        print("  → ".join([graph.nodes.get(n, n).title for n in current_path]))
        return

    # 找到从当前节点出发的所有边
    outgoing_edges = [e for e in graph.edges if e.from_id == node_id]

    if not outgoing_edges:
        print("  → ".join([graph.nodes.get(n, n).title for n in current_path]))
        return

    for edge in outgoing_edges:
        print_paths(graph, edge.to_id, current_path)


def TraversalGraph_to_mermaid(graph: TraversalGraph) -> str:
    """生成 Mermaid 图表"""
    lines = ["graph TD"]

    # 样式定义
    lines.append("    classDef startNode fill:#90EE90,stroke:#333,stroke-width:2px")
    lines.append("    classDef endNode fill:#FFB6C1,stroke:#333,stroke-width:2px")
    lines.append("    classDef skipNode fill:#FFD700,stroke:#FFA500,stroke-width:2px")
    lines.append("    classDef toggleNode fill:#87CEEB,stroke:#333,stroke-width:1px")
    lines.append("")

    # 添加节点和边
    for edge in graph.edges:
        from_node = graph.nodes.get(edge.from_id)
        to_node = graph.nodes.get(edge.to_id)

        if not from_node or not to_node:
            continue

        # 节点标签
        label = f"{from_node.title}"
        if from_node.action == "skip":
            label += " ⚠️"
        if from_node.type == NodeType.SKIP:
            label = f"{label}:::skipNode"
        elif from_node.type == NodeType.START:
            label = f"{label}:::startNode"
        elif from_node.type == NodeType.END:
            label = f"{label}:::endNode"
        elif from_node.type == NodeType.TOGGLE:
            label = f"{label}:::toggleNode"

        # 边的标签
        edge_label = f"|{edge.action}|" if edge.action and edge.action != "navigate" else ""

        lines.append(f"    {from_node.id}{edge_label}{to_node}")

    return "\n".join(lines)


# 绑定方法
TraversalGraph.to_mermaid = TraversalGraph_to_mermaid


# ============= 主演示 =============

def main():
    """主演示函数"""

    print("=" * 70)
    print("自然语言 → 图转换演示")
    print("=" * 70)

    # 场景 1: 完整遍历
    print("\n" + "=" * 70)
    print("【场景 1】用户描述: \"遍历系统设置所有项\"")
    print("=" * 70)

    builder1 = GraphBuilder()
    graph1 = builder1.build("遍历系统设置所有项")
    visualize_graph(graph1)

    # 场景 2: 跳过修改操作
    print("\n" + "=" * 70)
    print("【场景 2】用户描述: \"遍历系统设置，但不点击修改按钮\"")
    print("=" * 70)

    builder2 = GraphBuilder()
    graph2 = builder2.build("遍历系统设置，但不点击修改按钮")
    visualize_graph(graph2)

    # 对比说明
    print("\n" + "=" * 70)
    print("【对比总结】")
    print("=" * 70)
    print(f"""
场景 1 (完整遍历):
  - 节点数: {len(graph1.nodes)}
  - 边数: {len(graph1.edges)}
  - 所有节点都会被访问和执行
  - 包括开关(toggle)和操作(action)

场景 2 (跳过修改):
  - 节点数: {len(graph2.nodes)}
  - 边数: {len(graph2.edges)}
  - 黄色 🟡 节点会被跳过
  - 这些节点的 action = "skip"

关键差异:
  - 场景 2 中的"使用 Wi‑Fi"、"使用蓝牙"、"深色模式"等开关被标记为 SKIP
  - 遍历器会记录这些节点但不会实际点击
  - 这样可以完整探索层级结构，但避免修改设置
    """)


if __name__ == "__main__":
    main()
