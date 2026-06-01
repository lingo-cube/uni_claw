"""清晰展示图的实际结构

使用 ASCII 和简化 Mermaid 来展示图
"""

from typing import Dict, List, Tuple


class GraphVisualizer:
    """图可视化工具"""

    @staticmethod
    def print_ascii_tree(nodes: Dict, edges: List, root_id: str = "root"):
        """打印 ASCII 树形结构"""
        print("\n" + "=" * 70)
        print("📊 图的树形结构")
        print("=" * 70)

        def print_node(node_id: str, prefix: str = "", is_last: bool = True):
            if node_id not in nodes:
                return

            node = nodes[node_id]

            # 节点符号
            if node.get("type") == "start":
                symbol = "🟢"
            elif node.get("type") == "end":
                symbol = "🔴"
            elif node.get("type") == "skip":
                symbol = "🟡"
            elif node.get("type") == "toggle":
                symbol = "🔵"
            elif node.get("type") == "action":
                symbol = "🟣"
            else:
                symbol = "⚪"

            # 节点信息
            skip_mark = " ⚠️ SKIP" if node.get("action") == "skip" else ""
            print(f"{prefix}{'└── ' if is_last else '├── ├── '}{symbol} [{node_id}] {node['title']}{skip_mark}")

            # 找到子节点
            children = GraphVisualizer._get_children(node_id, edges)
            for i, child_id in enumerate(children):
                is_last_child = (i == len(children) - 1)
                new_prefix = prefix + ("    " if is_last else "│   ")
                print_node(child_id, new_prefix, is_last_child)

        print_node(root_id)

    @staticmethod
    def print_mermaid_simple(nodes: Dict, edges: List):
        """打印简化的 Mermaid 图表"""
        print("\n" + "=" * 70)
        print("📊 Mermaid 图表 (复制到 https://mermaid.live 查看)")
        print("=" * 70)
        print("```mermaid")
        print("graph TD")

        # 节点样式
        print("\n    %% 节点样式")
        print("    classDef start fill:#90EE90,stroke:#333,stroke-width:3px")
        print("    classDef end fill:#FFB6C1,stroke:#333,stroke-width:3px")
        print("    classDef skip fill:#FFD700,stroke:#FFA500,stroke-width:2px,stroke-dasharray: 5 5")
        print("    classDef toggle fill:#87CEEB,stroke:#333,stroke-width:2px")
        print("    classDef menu fill:#E8E8E8,stroke:#666,stroke-width:1px")
        print("    classDef page fill:#F0F8FF,stroke:#333,stroke-width:1px")

        # 节点定义
        print("\n    %% 节点")
        for node_id, node in nodes.items():
            title = node['title'].replace('"', '\\"')
            node_type = node.get('type', 'menu')

            # 根据类型选择样式类
            if node_type == 'start':
                style = ":::start"
            elif node_type == 'end':
                style = ":::end"
            elif node_type == 'skip':
                style = ":::skip"
            elif node_type == 'toggle':
                style = ":::toggle"
            elif node_type == 'page':
                style = ":::page"
            else:
                style = ":::menu"

            # 添加 SKIP 标记
            if node.get('action') == 'skip':
                title = f"{title} ⚠️"

            print(f'    {node_id}["{title}"]{style}')

        # 边定义
        print("\n    %% 边")
        for edge in edges:
            from_id = edge['from_id']
            to_id = edge['to_id']
            action = edge.get('action', '')

            if action == 'navigate':
                label = ''
            elif action == 'tap':
                label = ' -->|点击|'
            elif action == 'skip':
                label = ' -->|.跳过.|'
            elif action == 'enter':
                label = ' -->|进入|'
            elif action == 'complete':
                label = ' -->|完成|'
            else:
                label = f' -->|{action}|'

            print(f'    {from_id}{label}{to_id}')

        print("```")

    @staticmethod
    def print_table_view(nodes: Dict, edges: List):
        """打印表格视图"""
        print("\n" + "=" * 70)
        print("📊 节点详情表")
        print("=" * 70)
        print(f"{'ID':<20} {'标题':<15} {'类型':<10} {'动作':<10} {'描述'}")
        print("-" * 70)

        for node_id, node in nodes.items():
            print(f"{node_id:<20} {node['title']:<15} {node.get('type', 'menu'):<10} "
                  f"{node.get('action', 'visit'):<10} {node.get('description', '')}")

    @staticmethod
    def print_execution_plan(nodes: Dict, edges: List, root_id: str = "root"):
        """打印执行计划"""
        print("\n" + "=" * 70)
        print("📋 执行计划 (遍历顺序)")
        print("=" * 70)

        step = 1
        def traverse(node_id: str, depth: int = 0):
            nonlocal step
            if node_id not in nodes:
                return

            node = nodes[node_id]
            indent = "  " * depth

            # 执行标记
            if node.get('action') == 'skip':
                exec_mark = "⏭️  跳过"
            elif node.get('type') == 'start':
                exec_mark = "▶️ 开始"
            elif node.get('type') == 'end':
                exec_mark = "🏁 完成"
            else:
                exec_mark = "✅ 执行"

            print(f"{indent}{step}. {exec_mark} {node['title']} ({node.get('type', 'node')})")
            step += 1

            # 获取子节点并递归
            children = GraphVisualizer._get_children(node_id, edges)
            for child_id in children:
                traverse(child_id, depth + 1)

        traverse(root_id)

    @staticmethod
    def _get_children(node_id: str, edges: List) -> List:
        """获取节点的直接子节点"""
        children = []
        seen = set()

        for edge in edges:
            if edge['from_id'] == node_id:
                child_id = edge['to_id']
                # 避免重复和循环
                if child_id not in seen and child_id != 'end':
                    children.append(child_id)
                    seen.add(child_id)

        return children


# ============= 演示数据 =============

def create_demo_graph_data():
    """创建演示图数据"""
    nodes = {
        "root": {"id": "root", "title": "开始", "type": "start", "action": "start"},
        "settings": {"id": "settings", "title": "系统设置", "type": "page", "action": "navigate",
                    "description": "系统设置首页"},
        "l1_wifi": {"id": "l1_wifi", "title": "Wi‑Fi", "type": "menu", "action": "tap",
                    "description": "一级菜单 - Wi‑Fi设置"},
        "l1_wifi_sub": {"id": "l1_wifi_sub", "title": "Wi‑Fi详情", "type": "submenu", "action": "tap",
                        "description": "Wi‑Fi子菜单"},
        "l1_wifi_toggle": {"id": "l1_wifi_toggle", "title": "使用 Wi‑Fi", "type": "toggle", "action": "tap",
                           "description": "Wi‑Fi开关"},
        "l1_bluetooth": {"id": "l1_bluetooth", "title": "蓝牙", "type": "menu", "action": "tap",
                         "description": "一级菜单 - 蓝牙设置"},
        "l1_bluetooth_toggle": {"id": "l1_bluetooth_toggle", "title": "使用蓝牙", "type": "toggle", "action": "skip",
                                "description": "蓝牙开关 - 跳过"},
        "l1_display": {"id": "l1_display", "title": "显示", "type": "menu", "action": "tap",
                       "description": "一级菜单 - 显示设置"},
        "l1_display_dark": {"id": "l1_display_dark", "title": "深色模式", "type": "toggle", "action": "skip",
                            "description": "深色模式开关 - 跳过"},
        "l1_display_wallpaper": {"id": "l1_display_wallpaper", "title": "壁纸", "type": "action", "action": "tap",
                                  "description": "更换壁纸"},
        "l1_battery": {"id": "l1_battery", "title": "电池", "type": "menu", "action": "tap",
                       "description": "一级菜单 - 电池设置"},
        "end": {"id": "end", "title": "完成", "type": "end", "action": "end"},
    }

    edges = [
        {"from_id": "root", "to_id": "settings", "action": "navigate"},
        {"from_id": "settings", "to_id": "l1_wifi", "action": "navigate"},
        {"from_id": "settings", "to_id": "l1_bluetooth", "action": "navigate"},
        {"from_id": "settings", "to_id": "l1_display", "action": "navigate"},
        {"from_id": "settings", "to_id": "l1_battery", "action": "navigate"},
        {"from_id": "l1_wifi", "to_id": "l1_wifi_sub", "action": "enter"},
        {"from_id": "l1_wifi", "to_id": "l1_wifi_toggle", "action": "tap"},
        {"from_id": "l1_bluetooth", "to_id": "l1_bluetooth_toggle", "action": "skip"},
        {"from_id": "l1_display", "to_id": "l1_display_dark", "action": "skip"},
        {"from_id": "l1_display", "to_id": "l1_display_wallpaper", "action": "tap"},
        {"from_id": "l1_wifi", "to_id": "end", "action": "complete"},
        {"from_id": "l1_bluetooth", "to_id": "end", "action": "complete"},
        {"from_id": "l1_display", "to_id": "end", "action": "complete"},
        {"from_id": "l1_battery", "to_id": "end", "action": "complete"},
    ]

    return nodes, edges


def main():
    """主演示"""
    print("\n" + "=" * 70)
    print("🔄 自然语言 → 图转换可视化演示")
    print("=" * 70)
    print("\n📝 用户描述: \"遍历系统设置，但不点击开关按钮\"")

    nodes, edges = create_demo_graph_data()

    # 1. 树形结构
    GraphVisualizer.print_ascii_tree(nodes, edges)

    # 2. 表格视图
    GraphVisualizer.print_table_view(nodes, edges)

    # 3. 执行计划
    GraphVisualizer.print_execution_plan(nodes, edges)

    # 4. Mermaid 图表
    GraphVisualizer.print_mermaid_simple(nodes, edges)

    print("\n" + "=" * 70)
    print("🎯 关键点说明")
    print("=" * 70)
    print("""
图节点类型:
  🟢 start   - 开始节点，遍历起点
  🔴 end     - 结束节点，遍历终点
  ⚪ page    - 页面节点，表示一个完整页面
  ⚪ menu    - 菜单节点，可点击进入
  ⚪ submenu - 子菜单节点，二级菜单
  🔵 toggle  - 开关节点，会改变状态
  🟣 action  - 操作节点，触发某个动作
  🟡 skip    - 跳过节点，只记录不执行 ⚠️

执行动作:
  ✅ 执行   - 会实际点击/操作
  ⏭️  跳过   - 只记录，不执行
  ▶️ 开始   - 开始遍历
  🏁 完成   - 遍历结束

边类型:
  navigate - 导航到页面/菜单
  tap      - 点击操作
  skip     - 跳过操作
  enter    - 进入子菜单
  complete - 该分支完成
    """)


if __name__ == "__main__":
    main()
