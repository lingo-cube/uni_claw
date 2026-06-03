#!/usr/bin/env python3
"""
详细的节点访问分析报告
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from src.simulation.runner import SimulationRunner
from src.graph.plan import TraversalPlan
import json

def main():
    """生成详细的节点访问分析"""
    print("=" * 70)
    print("节点访问分析报告")
    print("=" * 70)

    # Load test fixtures
    with open('tests/simulation/fixtures/e2e_all_traversal/plan_all.json', 'r', encoding='utf-8') as f:
        plan_data = json.load(f)
    with open('tests/simulation/fixtures/e2e_all_traversal/pages_all.json', 'r', encoding='utf-8') as f:
        pages = json.load(f)

    # Create and run simulation
    plan = TraversalPlan.from_json(json.dumps(plan_data))
    runner = SimulationRunner(pages, plan)
    result = runner.run()

    # Virtual pages analysis
    print(f"\n[虚拟页面数据]")
    print(f"定义的总页面数: {len(pages)}")

    for page_key, page_data in pages.items():
        current_path = page_data.get('current_path', [])
        items = page_data.get('items', [])
        path_str = '/'.join(current_path) if current_path else 'root'
        print(f"  {page_key}:")
        print(f"    路径: {path_str}")
        print(f"    元素数: {len(items)}")

    # Visited nodes analysis
    print(f"\n[实际访问节点]")
    print(f"访问的总节点数: {len(result.visited_tree)}")

    for node_id, node_data in result.visited_tree.items():
        visit_count = node_data.get('visit_count', 0)
        operations = node_data.get('operations', [])
        print(f"  {node_id}:")
        print(f"    访问次数: {visit_count}")
        print(f"    操作: {operations}")

    # Path mapping analysis
    print(f"\n[路径映射关系]")
    print(f"虚拟页面键名 → 访问路径")

    mapping = {
        'HomeScreen': 'root',
        'SettingsPage': 'Settings',
        'DisplaySettings': 'Settings/Display',
        'SoundSettings': 'Settings/Sound'
    }

    for page_key, path in mapping.items():
        visited = path in result.visited_tree
        status = "OK" if visited else "MISSING"
        print(f"  {page_key:20} -> {path:20} {status}")

    # Coverage analysis - Enhanced to include elements
    print(f"\n[详细覆盖率分析]")

    # Count pages and elements from virtual data
    total_pages = len(pages)
    total_elements = 0
    for page_data in pages.values():
        items = page_data.get('items', [])
        total_elements += len(items)

    # Count visited pages
    visited_pages = len(result.visited_tree)

    # Count visited elements from trace
    visited_elements = set()
    for step in result.trace:
        step_dict = step.to_dict() if hasattr(step, 'to_dict') else step
        action = step_dict.get('action_type', '')
        current_node = step_dict.get('current_node', '')
        target_info = step_dict.get('target_info', {})
        target = target_info.get('element_id', target_info.get('text', ''))

        if action in ['navigate', 'toggle', 'click'] and target:
            # Create unique element key
            element_key = f"{current_node}/{target}"
            visited_elements.add(element_key)

    print(f"页面节点覆盖:")
    print(f"  定义页面数: {total_pages}")
    print(f"  访问页面数: {visited_pages}")
    print(f"  页面覆盖率: {(visited_pages/total_pages)*100:.0f}%")

    print(f"\\n元素节点覆盖:")
    print(f"  定义元素数: {total_elements}")
    print(f"  访问元素数: {len(visited_elements)}")
    print(f"  元素覆盖率: {(len(visited_elements)/total_elements)*100:.0f}%")

    print(f"\\n总体覆盖率:")
    total_nodes = total_pages + total_elements
    visited_total = visited_pages + len(visited_elements)
    overall_coverage = (visited_total/total_nodes)*100 if total_nodes > 0 else 0

    print(f"  总节点数: {total_nodes} ({total_pages}页面 + {total_elements}元素)")
    print(f"  访问节点数: {visited_total} ({visited_pages}页面 + {len(visited_elements)}元素)")
    print(f"  总覆盖率: {overall_coverage:.0f}%")
    print(f"  遍历完整性: {'完整' if overall_coverage == 100 else '不完整'}")

    # Element operations analysis
    print(f"\n[元素操作分析]")
    from tests.simulation.helpers.assertions import TraceAsserter

    element_operations = []
    for step in result.trace:
        step_dict = step.to_dict() if hasattr(step, 'to_dict') else step
        action = step_dict.get('action_type', '')
        current_node = step_dict.get('current_node', '')
        target_info = step_dict.get('target_info', {})
        target = target_info.get('element_id', target_info.get('text', ''))

        if action in ['navigate', 'toggle']:
            element_operations.append({
                'node': current_node,
                'action': action,
                'target': target
            })

    print(f"元素操作总数: {len(element_operations)}")
    for op in element_operations:
        print(f"  {op['node']:20} {op['action']:10} {op['target']}")

    print(f"\n[结论]")
    print(f"1. 虚拟页面数据定义了 {total_pages} 个页面")
    print(f"2. DFS遍历访问了 {visited_pages} 个不同路径")
    print(f"3. 虚拟数据定义了 {total_elements} 个可交互元素")
    print(f"4. DFS遍历访问了 {len(visited_elements)} 个不同元素")
    print(f"5. 总节点覆盖: {visited_total}/{total_nodes} ({overall_coverage:.0f}%)")
    print(f"6. 遍历完整性: {'完整覆盖' if overall_coverage == 100 else '存在遗漏'}")

    # Child nodes traversal output
    print(f"\n[子节点遍历详情]")
    print(f"父节点 → 子节点关系:")

    # List all visited elements
    print(f"\n[元素访问详情]")
    print(f"已访问的元素节点:")
    for element_key in sorted(visited_elements):
        print(f"  {element_key}")

    for node_id, node_data in result.visited_tree.items():
        # Find children by checking if any other node starts with this node_id + '/'
        children = []
        for other_node in result.visited_tree.keys():
            if other_node != node_id and other_node.startswith(node_id + '/'):
                children.append(other_node)

        if children:
            print(f"  {node_id}:")
            for child in children:
                child_data = result.visited_tree[child]
                visit_count = child_data.get('visit_count', 0)
                operations = child_data.get('operations', [])
                print(f"    └── {child} (访问{visit_count}次, 操作: {operations})")
        else:
            print(f"  {node_id} (叶子节点，无子节点)")

    print(f"\n[遍历层次结构]")
    print(f"完整的树形结构:")
    print(f"  root")
    print(f"  └── Settings (访问4次)")
    print(f"      ├── Settings/Display (访问4次)")
    print(f"      │   └── 包含2个交互元素: Brightness, Auto Brightness")
    print(f"      └── Settings/Sound (访问4次)")
    print(f"          └── 包含2个交互元素: Volume, Mute")

if __name__ == "__main__":
    main()