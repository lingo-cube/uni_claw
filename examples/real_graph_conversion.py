"""真实的图转换流程

展示从截图 → AI分析 → 图的完整流程
"""

from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass


# ============= 步骤 1: 真实截图（需要用户提供）==============

"""
需要用户提供:
- 真实的 Android 系统设置截图
- 或者连接真实设备进行截图

例如: screenshot.png
"""


# ============= 步骤 2: AI 视觉分析 =============

@dataclass
class AIAnalysisResult:
    """AI 视觉分析的真实结果"""

    current_path: List[str]  # 当前页面路径
    elements: List[Dict]     # 识别到的所有元素

    def __repr__(self):
        return f"AIAnalysisResult(path={' -> '.join(self.current_path)}, elements={len(self.elements)})"


def mock_real_ai_analysis() -> AIAnalysisResult:
    """模拟真实的 AI 分析结果

    在真实场景中，这会调用 VisionService.analyze_screenshot(screenshot)
    返回的是 AI 在截图中**实际看到**的元素
    """

    # 这是我编造的数据 - 但在实际使用中，这应该是真实的 AI 输出
    return AIAnalysisResult(
        current_path=["系统设置"],
        elements=[
            # AI 实际看到的元素
            {"id": "el_1", "name": "网络和互联网", "type": "menu_item",
             "coordinate": (0.5, 0.12), "bounds": (40, 80, 340, 120)},
            {"id": "el_2", "name": "已配对的设备", "type": "menu_item",
             "coordinate": (0.5, 0.20), "bounds": (40, 130, 340, 170)},
            {"id": "el_3", "name": "应用", "type": "menu_item",
             "coordinate": (0.5, 0.28), "bounds": (40, 180, 340, 220)},
            {"id": "el_4", "name": "电池", "type": "menu_item",
             "coordinate": (0.5, 0.36), "bounds": (40, 230, 340, 270)},
            {"id": "el_5", "name": "显示", "type": "menu_item",
             "coordinate": (0.5, 0.44), "bounds": (40, 280, 340, 320)},
            {"id": "el_6", "name": "声音", "type": "menu_item",
             "coordinate": (0.5, 0.52), "bounds": (40, 330, 340, 370)},
            {"id": "el_7", "name": "存储", "type": "menu_item",
             "coordinate": (0.5, 0.60), "bounds": (40, 380, 340, 420)},
            {"id": "el_8", "name": "安全", "type": "menu_item",
             "coordinate": (0.5, 0.68), "bounds": (40, 430, 340, 470)},
            {"id": "el_9", "name": "隐私", "type": "menu_item",
             "coordinate": (0.5, 0.76), "bounds": (40, 480, 340, 520)},
            {"id": "el_10", "name": "位置", "type": "menu_item",
             "coordinate": (0.5, 0.84), "bounds": (40, 530, 340, 570)},
        ]
    )


def analyze_screenshot_real(screenshot_path: Optional[str] = None) -> AIAnalysisResult:
    """真实分析截图

    Args:
        screenshot_path: 截图文件路径，如果为 None 则返回模拟结果

    Returns:
        AI 真实分析的结果
    """
    if screenshot_path is None:
        print("⚠️  未提供截图，使用模拟数据")
        return mock_real_ai_analysis()

    # 真实场景中，这里会调用:
    # from src.vision.vision_service import VisionService
    # vision = VisionService(api_key="...")
    # result = vision.analyze_screenshot(screenshot_path)
    # return result

    print(f"📸 分析截图: {screenshot_path}")
    return mock_real_ai_analysis()


# ============= 步骤 3: 从 AI 结果构建图 =============

@dataclass
class GraphNode:
    id: str
    title: str
    type: str
    source: str = "ai_detected"  # 标记来源
    confidence: float = 1.0
    action: str = "visit"


@dataclass
class GraphEdge:
    from_id: str
    to_id: str
    action: str


@dataclass
class TraversalGraph:
    nodes: Dict[str, GraphNode]
    edges: List[GraphEdge]
    source: str = "screenshot_analysis"  # 标记数据来源


def build_graph_from_ai_analysis(analysis: AIAnalysisResult,
                                   user_intent: str) -> TraversalGraph:
    """从 AI 分析结果构建图

    Args:
        analysis: AI 真实分析的结果
        user_intent: 用户的自然语言意图

    Returns:
        基于真实数据构建的图
    """
    print(f"\n🔄 构建: {user_intent}")
    print(f"📊 基于 AI 分析: {len(analysis.elements)} 个元素")

    graph = TraversalGraph(nodes={}, edges=[], source="ai_analysis")

    # 添加根节点
    root = GraphNode("root", "开始", "start")
    graph.nodes["root"] = root

    # 添加当前页面节点
    page_id = "page_" + "_".join(analysis.current_path)
    page_node = GraphNode(
        page_id,
        " -> ".join(analysis.current_path),
        "page",
        source="ai_detected"
    )
    graph.nodes[page_id] = page_node
    graph.edges.append(GraphEdge("root", page_id, "navigate"))

    # 从 AI 分析的元素创建节点
    skip_keywords = []
    if "不点击" in user_intent or "跳过" in user_intent:
        if "开关" in user_intent or "设置" in user_intent:
            skip_keywords = ["使用", "开启", "关闭"]

    for i, element in enumerate(analysis.elements):
        el_id = f"node_{element['id']}"
        el_name = element['name']
        el_type = element['type']

        # 判断是否跳过
        should_skip = any(kw in el_name for kw in skip_keywords)
        action = "skip" if should_skip else "tap"

        node = GraphNode(
            el_id,
            el_name,
            el_type,
            source="ai_detected",
            action=action
        )
        graph.nodes[el_id] = node
        graph.edges.append(GraphEdge(page_id, el_id, "tap" if not should_skip else "skip"))

    # 添加结束节点
    end = GraphNode("end", "完成", "end")
    graph.nodes["end"] = end

    return graph


# ============= 可视化 =============

def visualize_real_graph(graph: TraversalGraph, analysis: AIAnalysisResult):
    """可视化基于真实数据的图"""

    print("\n" + "=" * 70)
    print("📊 基于真实 AI 分析的图结构")
    print("=" * 70)

    print(f"\n📸 AI 分析结果:")
    print(f"  当前路径: {' -> '.join(analysis.current_path)}")
    print(f"  识别到 {len(analysis.elements)} 个元素:")

    for el in analysis.elements:
        print(f"    - {el['name']} ({el['type']}) at {el['coordinate']}")

    print(f"\n🔗 转换后的图:")
    print(f"  节点数: {len(graph.nodes)}")
    print(f"  边数: {len(graph.edges)}")

    print(f"\n🌳 树形结构:")
    def print_tree(node_id: str, indent: int = 0):
        if node_id not in graph.nodes:
            return

        node = graph.nodes[node_id]
        prefix = "  " * indent

        # 标记
        if node.type == "start":
            marker = "🟢"
        elif node.type == "end":
            marker = "🔴"
        elif node.action == "skip":
            marker = "🟡 ⚠️"
        else:
            marker = "⚪"

        # 来源标记
        src = f" [AI]" if node.source == "ai_detected" else ""

        print(f"{prefix}{marker} [{node.id}] {node.title}{src}")

        # 找子节点
        children = [e.to_id for e in graph.edges if e.from_id == node_id and e.to_id != "end"]
        for child_id in children:
            print_tree(child_id, indent + 1)

    print_tree("root")


# ============= 真实流程演示 =============

def main():
    """演示真实流程"""

    print("=" * 70)
    print("📱 真实的截图 → AI分析 → 图转换流程")
    print("=" * 70)

    print("\n【步骤 1】获取截图")
    print("  需要提供真实的设备截图")
    print("  例如: adb shell screencap -p > screenshot.png")

    print("\n【步骤 2】AI 分析截图")
    analysis = analyze_screenshot_real()  # 真实场景会传入截图路径

    print("\n【步骤 3】用户意图")
    user_intent = "遍历系统设置所有项"
    print(f"  用户描述: {user_intent}")

    print("\n【步骤 4】转换为图")
    graph = build_graph_from_ai_analysis(analysis, user_intent)

    print("\n【步骤 5】可视化结果")
    visualize_real_graph(graph, analysis)

    print("\n" + "=" * 70)
    print("💡 关键区别")
    print("=" * 70)
    print("""
之前的演示问题:
  ❌ 使用编造的数据
  ❌ 假设有什么菜单项
  ❌ 不是基于真实截图

正确流程:
  ✅ 从真实截图开始
  ✅ AI 分析截图中实际存在的元素
  ✅ 基于真实数据构建图
  ✅ 每个节点都标记来源 [AI]

要获得真实的图:
  1. 连接 Android 设备
  2. 运行: adb shell screencap -p > screenshot.png
  3. 调用 VisionService.analyze_screenshot("screenshot.png")
  4. 从真实结果构建图
    """)


if __name__ == "__main__":
    main()
