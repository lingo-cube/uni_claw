"""演示：将"遍历系统设置所有项"转换为图结构

这个示例展示如何将系统设置的层级结构转换为有向无环图 (DAG)
"""

from typing import Dict, List
from enum import Enum


class NodeType(str, Enum):
    """图中节点的类型"""
    START = "start"
    PAGE = "page"           # 完整页面
    MENU = "menu"           # 菜单项
    SUBMENU = "submenu"     # 子菜单项
    ACTION = "action"       # 操作按钮
    SKIP = "skip"           # 跳过的节点
    END = "end"             # 结束节点


class GraphNode:
    """图节点"""

    def __init__(self, id: str, title: str, node_type: NodeType,
                 description: str = "", action: str = "tap"):
        self.id = id
        self.title = title
        self.node_type = node_type
        self.description = description
        self.action = action
        self.children: List[str] = []
        self.visited = False

    def __repr__(self):
        return f"Node({self.id}, {self.title}, {self.node_type.value})"


class GraphEdge:
    """图边"""

    def __init__(self, from_node: str, to_node: str, action: str = "navigate"):
        self.from_node = from_node
        self.to_node = to_node
        self.action = action

    def __repr__(self):
        return f"Edge({self.from_node} -> {self.to_node}, {self.action})"


class TraversalGraph:
    """遍历图 (DAG)"""

    def __init__(self, name: str = "System Settings"):
        self.name = name
        self.nodes: Dict[str, GraphNode] = {}
        self.edges: List[GraphEdge] = []
        self.root_id: str = "root"

    def add_node(self, node: GraphNode) -> None:
        """添加节点"""
        self.nodes[node.id] = node

    def add_edge(self, edge: GraphEdge) -> None:
        """添加边"""
        self.edges.append(edge)
        if edge.from_node in self.nodes:
            self.nodes[edge.from_node].children.append(edge.to_node)

    def to_mermaid(self) -> str:
        """导出为 Mermaid 图表"""
        lines = ["graph TD"]

        # 添加节点样式
        lines.append("    classDef startNode fill:#90EE90")
        lines.append("    classDef endNode fill:#FFB6C1")
        lines.append("    classDef skipNode fill:#FFD700,stroke:#FFA500")
        lines.append("")

        # 添加边
        for edge in self.edges:
            label = f"|{edge.action}|" if edge.action else ""
            line = f"    {edge.from_node} {label} {edge.to_node}"

            # 添加节点样式
            from_node = self.nodes.get(edge.from_node)
            to_node = self.nodes.get(edge.to_node)

            if from_node and from_node.node_type == NodeType.START:
                line += ":::startNode"
            if to_node and to_node.node_type == NodeType.END:
                line += ":::endNode"
            if to_node and to_node.node_type == NodeType.SKIP:
                line += ":::skipNode"

            lines.append(line)

        return "\n".join(lines)

    def print_structure(self) -> None:
        """打印图结构"""
        print(f"\n=== {self.name} Graph Structure ===")
        print(f"Nodes: {len(self.nodes)}")
        print(f"Edges: {len(self.edges)}")
        print("\nNode Tree:")
        self._print_node(self.root_id, 0)

    def _print_node(self, node_id: str, indent: int) -> None:
        """递归打印节点树"""
        if node_id not in self.nodes:
            return

        node = self.nodes[node_id]
        prefix = "  " * indent + "├──"
        print(f"{prefix} [{node.id}] {node.title} ({node.node_type.value})")
        if node.description:
            print(f"{'  ' * (indent + 1)}    desc: {node.description}")
        if node.action != "tap":
            print(f"{'  ' * (indent + 1)}    action: {node.action}")

        for child_id in node.children:
            self._print_node(child_id, indent + 1)

    def get_statistics(self) -> Dict:
        """获取图统计信息"""
        node_types = {}
        skip_count = 0

        for node in self.nodes.values():
            node_types[node.node_type.value] = node_types.get(node.node_type.value, 0) + 1
            if node.node_type == NodeType.SKIP:
                skip_count += 1

        return {
            "total_nodes": len(self.nodes),
            "total_edges": len(self.edges),
            "node_types": node_types,
            "skip_count": skip_count,
            "max_depth": self._calculate_max_depth(self.root_id, 0)
        }

    def _calculate_max_depth(self, node_id: str, current_depth: int) -> int:
        """计算最大深度"""
        if node_id not in self.nodes or not self.nodes[node_id].children:
            return current_depth

        max_child_depth = 0
        for child_id in self.nodes[node_id].children:
            depth = self._calculate_max_depth(child_id, current_depth + 1)
            max_child_depth = max(max_child_depth, depth)

        return max_child_depth


def create_system_settings_graph() -> TraversalGraph:
    """创建系统设置遍历图

    典型的 Android 系统设置结构:
    - 设置首页 (一级菜单)
        - Wi-Fi (二级菜单)
            - 已保存网络 (列表项)
            - 添加网络 (操作)
        - 蓝牙 (二级菜单)
            - 设备列表 (列表项)
            - 可见性 (开关)
        - 显示 (二级菜单)
            - 亮度 (滑块)
            - 壁纸 (操作)
        - 存储 (二级菜单)
            - 内部存储 (信息)
            - 清理 (操作)
    """
    graph = TraversalGraph("System Settings")

    # 根节点 (开始)
    root = GraphNode("root", "开始", NodeType.START, action="start")
    graph.add_node(root)

    # 设置首页
    settings_home = GraphNode("settings_home", "设置", NodeType.PAGE, "系统设置首页")
    graph.add_node(settings_home)
    graph.add_edge(GraphEdge("root", "settings_home", "进入设置"))

    # 一级菜单
    level1_menus = [
        ("wifi", "Wi-Fi", "网络和连接"),
        ("bluetooth", "蓝牙", "网络和连接"),
        ("display", "显示", "设备设置"),
        ("storage", "存储", "设备设置"),
        ("battery", "电池", "设备设置"),
        ("apps", "应用", "设备设置"),
        ("security", "安全", "系统设置"),
        ("privacy", "隐私", "系统设置"),
    ]

    # 添加一级菜单节点
    for menu_id, menu_name, category in level1_menus:
        menu_node = GraphNode(
            f"l1_{menu_id}",
            menu_name,
            NodeType.MENU,
            f"{category} - {menu_name}设置"
        )
        graph.add_node(menu_node)
        graph.add_edge(GraphEdge("settings_home", menu_node.id, "导航"))

    # 二级菜单和操作 (Wi-Fi)
    graph.add_node(GraphNode("wifi_list", "已保存网络", NodeType.SUBMENU, "查看已保存的 Wi-Fi 网络"))
    graph.add_edge(GraphEdge("l1_wifi", "wifi_list", "进入"))

    graph.add_node(GraphNode("wifi_add", "添加网络", NodeType.ACTION, "添加新的 Wi-Fi 网络"))
    graph.add_edge(GraphEdge("wifi_list", "wifi_add", "操作"))

    # 二级菜单和操作 (蓝牙)
    graph.add_node(GraphNode("bt_devices", "已配对设备", NodeType.SUBMENU, "查看已配对的蓝牙设备"))
    graph.add_edge(GraphEdge("l1_bluetooth", "bt_devices", "进入"))

    graph.add_node(GraphNode("bt_visibility", "可见性", NodeType.ACTION, "设置蓝牙可见性"))
    graph.add_edge(GraphEdge("bt_devices", "bt_visibility", "操作"))

    # 二级菜单和操作 (显示)
    graph.add_node(GraphNode("display_brightness", "亮度", NodeType.SUBMENU, "调整屏幕亮度"))
    graph.add_edge(GraphEdge("l1_display", "display_brightness", "进入"))

    graph.add_node(GraphNode("display_wallpaper", "壁纸", NodeType.ACTION, "更换壁纸"))
    graph.add_edge(GraphEdge("l1_display", "display_wallpaper", "操作"))

    graph.add_node(GraphNode("display_font", "字体", NodeType.SUBMENU, "调整字体大小"))
    graph.add_edge(GraphEdge("l1_display", "display_font", "进入"))

    # 二级菜单和操作 (存储)
    graph.add_node(GraphNode("storage_internal", "内部存储", NodeType.SUBMENU, "查看内部存储使用情况"))
    graph.add_edge(GraphEdge("l1_storage", "storage_internal", "进入"))

    graph.add_node(GraphNode("storage_cleanup", "清理空间", NodeType.ACTION, "清理存储空间"))
    graph.add_edge(GraphEdge("l1_storage", "storage_cleanup", "操作"))

    # 结束节点
    end = GraphNode("end", "完成", NodeType.END, action="end")
    graph.add_node(end)

    # 所有一级菜单完成后都连接到结束节点
    for menu_id, _, _ in level1_menus:
        graph.add_edge(GraphEdge(f"l1_{menu_id}", "end", "完成"))

    return graph


def create_system_settings_with_skip_graph() -> TraversalGraph:
    """创建系统设置遍历图 - 跳过修改操作

    这是对用户需求的实现：遍历所有层级，但跳过实际修改设置的操作
    """
    graph = TraversalGraph("System Settings (Skip Modify)")

    # 根节点
    root = GraphNode("root", "开始", NodeType.START, action="start")
    graph.add_node(root)

    # 设置首页
    settings_home = GraphNode("settings_home", "设置", NodeType.PAGE, "系统设置首页")
    graph.add_node(settings_home)
    graph.add_edge(GraphEdge("root", "settings_home", "进入设置"))

    # 一级菜单
    level1_menus = ["Wi-Fi", "蓝牙", "显示", "存储"]

    for i, menu_name in enumerate(level1_menus):
        menu_id = f"l1_{i}"
        menu_node = GraphNode(
            menu_id,
            menu_name,
            NodeType.MENU,
            f"{menu_name}设置"
        )
        graph.add_node(menu_node)
        graph.add_edge(GraphEdge("settings_home", menu_node.id, "导航"))

        # 每个一级菜单下的子项
        # 二级菜单 - 可以进入查看
        submenu_node = GraphNode(
            f"{menu_id}_submenu",
            f"{menu_name}详情",
            NodeType.SUBMENU,
            f"查看{menu_name}详细信息"
        )
        graph.add_node(submenu_node)
        graph.add_edge(GraphEdge(menu_node.id, submenu_node.id, "进入"))

        # 操作 - 标记为跳过
        action_node = GraphNode(
            f"{menu_id}_action",
            f"修改{menu_name}",
            NodeType.SKIP,
            f"修改{menu_name}的操作 - 根据需求跳过",
            action="skip"  # 关键：标记为 skip
        )
        graph.add_node(action_node)
        graph.add_edge(GraphEdge(menu_node.id, action_node.id, "跳过"))

    # 结束节点
    end = GraphNode("end", "完成", NodeType.END, action="end")
    graph.add_node(end)

    for i in range(len(level1_menus)):
        graph.add_edge(GraphEdge(f"l1_{i}", "end", "完成"))

    return graph


def main():
    """主函数 - 演示图转换"""
    print("=" * 60)
    print("系统设置遍历图转换演示")
    print("=" * 60)

    # 场景 1: 完整遍历
    print("\n【场景 1】完整遍历系统设置所有项")
    print("-" * 60)

    graph1 = create_system_settings_graph()
    graph1.print_structure()

    stats1 = graph1.get_statistics()
    print(f"\n统计信息:")
    print(f"  总节点数: {stats1['total_nodes']}")
    print(f"  总边数: {stats1['total_edges']}")
    print(f"  最大深度: {stats1['max_depth']}")
    print(f"  节点类型分布: {stats1['node_types']}")

    print("\nMermaid 图表:")
    print(graph1.to_mermaid())

    # 场景 2: 跳过修改操作
    print("\n" + "=" * 60)
    print("【场景 2】遍历系统设置，但跳过修改操作")
    print("-" * 60)

    graph2 = create_system_settings_with_skip_graph()
    graph2.print_structure()

    stats2 = graph2.get_statistics()
    print(f"\n统计信息:")
    print(f"  总节点数: {stats2['total_nodes']}")
    print(f"  跳过节点数: {stats2['skip_count']}")
    print(f"  节点类型分布: {stats2['node_types']}")

    print("\nMermaid 图表 (绿色=开始, 粉色=结束, 黄色=跳过):")
    print(graph2.to_mermaid())

    # 对比说明
    print("\n" + "=" * 60)
    print("图结构说明")
    print("=" * 60)
    print("""
图节点类型:
  - START: 开始节点 (绿色)
  - PAGE: 完整页面
  - MENU: 菜单项
  - SUBMENU: 子菜单项
  - ACTION: 操作按钮
  - SKIP: 跳过的节点 (黄色) - 标记为 skip 的操作不会执行
  - END: 结束节点 (粉色)

边上的标签:
  - "进入": 导航到该页面/菜单
  - "操作": 执行具体操作
  - "跳过": 跳过该操作 (不会实际点击)
  - "完成": 该分支遍历完成

遍历策略:
  1. 从 root 节点开始
  2. 按照边的顺序访问子节点
  3. 遇到 action="skip" 的节点时，只记录不执行
  4. 所有分支完成后到达 end 节点
    """)


if __name__ == "__main__":
    main()
