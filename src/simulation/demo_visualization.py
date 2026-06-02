#!/usr/bin/env python3
"""
V6 仿真机可视化演示脚本

生成各种格式的可视化输出，帮助理解遍历过程。
"""

import json
from datetime import datetime
from pathlib import Path

from src.graph.plan import TraversalPlan
from src.simulation.mock_vision import MockVisionService
from src.simulation.mock_action import MockActionExecutor
from src.simulation.visualizer import InMemoryTracer, TraceStep, VisitedNode
from src.simulation.runner import SimulationRunner


def create_demo_traversal():
    """创建一个演示用的遍历场景。"""
    tracer = InMemoryTracer()
    tracer.start_traversal(None)

    # 构建设置应用的访问树，包含预期操作
    tracer.visited_tree['root'] = VisitedNode(
        node_id='root',
        name='Settings Home',
        node_type='screen',
        visited=True,
        children=['wifi', 'bluetooth', 'display', 'storage', 'about'],
        expected_operation='等待加载完成'
    )

    tracer.visited_tree['wifi'] = VisitedNode(
        node_id='wifi',
        name='Wi-Fi Settings',
        node_type='screen',
        visited=True,
        children=['network1', 'network2'],
        expected_operation='click: Wi-Fi'
    )

    tracer.visited_tree['network1'] = VisitedNode(
        node_id='network1',
        name='HomeWiFi',
        node_type='leaf_action',
        visited=True,
        children=[],
        expected_operation='toggle: ON'
    )

    tracer.visited_tree['network2'] = VisitedNode(
        node_id='network2',
        name='OfficeWiFi',
        node_type='leaf_action',
        visited=True,
        children=[],
        expected_operation='toggle: ON'
    )

    tracer.visited_tree['bluetooth'] = VisitedNode(
        node_id='bluetooth',
        name='Bluetooth Settings',
        node_type='screen',
        visited=True,
        children=['device1'],
        expected_operation='click: Bluetooth'
    )

    tracer.visited_tree['device1'] = VisitedNode(
        node_id='device1',
        name='AirPods Pro',
        node_type='leaf_action',
        visited=True,
        children=[],
        expected_operation='click: Connect'
    )

    tracer.visited_tree['display'] = VisitedNode(
        node_id='display',
        name='Display Settings',
        node_type='screen',
        visited=True,
        children=[],
        expected_operation='click: Display'
    )

    tracer.visited_tree['storage'] = VisitedNode(
        node_id='storage',
        name='Storage Settings',
        node_type='screen',
        visited=True,
        children=['internal', 'sd_card'],
        expected_operation='click: Storage'
    )

    tracer.visited_tree['internal'] = VisitedNode(
        node_id='internal',
        name='Internal Storage',
        node_type='leaf_info',
        visited=True,
        children=[],
        expected_operation='view: 详情'
    )

    tracer.visited_tree['sd_card'] = VisitedNode(
        node_id='sd_card',
        name='SD Card',
        node_type='leaf_info',
        visited=False,
        children=[],
        expected_operation='view: 容量'
    )

    tracer.visited_tree['about'] = VisitedNode(
        node_id='about',
        name='About Phone',
        node_type='screen',
        visited=False,
        children=['version'],
        expected_operation='click: About'
    )

    tracer.visited_tree['version'] = VisitedNode(
        node_id='version',
        name='Version Info',
        node_type='leaf_info',
        visited=False,
        children=[],
        expected_operation='view: 版本号'
    )

    # 添加追踪步骤（包含实际执行的操作）
    steps_data = [
        # 格式: (from_state, to_state, node_id, action, metadata)
        ('INIT', 'NODE_SELECT', 'root', None, {}),
        ('NODE_SELECT', 'PRECONDITION_CHECK', 'root', None, {}),
        ('PRECONDITION_CHECK', 'EXECUTE', 'wifi', 'click', {}),
        ('EXECUTE', 'NODE_SELECT', 'network1', None, {}),
        ('NODE_SELECT', 'EXECUTE', 'network1', 'toggle', {}),
        ('EXECUTE', 'BACKTRACK', 'wifi', 'back', {}),
        ('NODE_SELECT', 'EXECUTE', 'network2', 'toggle', {}),
        ('EXECUTE', 'BACKTRACK', 'root', 'back', {}),
        ('NODE_SELECT', 'PRECONDITION_CHECK', 'bluetooth', None, {}),
        ('PRECONDITION_CHECK', 'EXECUTE', 'bluetooth', 'click', {}),
        ('EXECUTE', 'NODE_SELECT', 'device1', None, {}),
        ('NODE_SELECT', 'EXECUTE', 'device1', 'click', {}),
        ('EXECUTE', 'BACKTRACK', 'root', 'back', {}),
        ('NODE_SELECT', 'PRECONDITION_CHECK', 'display', None, {}),
        ('PRECONDITION_CHECK', 'EXECUTE', 'display', 'click', {}),
        ('EXECUTE', 'BACKTRACK', 'root', 'back', {}),
        ('NODE_SELECT', 'PRECONDITION_CHECK', 'storage', None, {}),
        ('PRECONDITION_CHECK', 'EXECUTE', 'storage', 'click', {}),
        ('EXECUTE', 'NODE_SELECT', 'internal', None, {}),
        ('NODE_SELECT', 'EXECUTE', 'internal', 'view', {}),
        ('EXECUTE', 'BACKTRACK', 'storage', 'back', {}),
        ('NODE_SELECT', 'PRECONDITION_CHECK', 'sd_card', None, {}),
        ('PRECONDITION_CHECK', 'SKIP', 'sd_card', None, {
            'reason': 'precondition_failed',
            'details': 'SD card not detected on device'
        }),
        ('SKIP', 'BACKTRACK', 'root', 'back', {}),
        ('NODE_SELECT', 'PRECONDITION_CHECK', 'about', None, {}),
        ('PRECONDITION_CHECK', 'SKIP', 'about', None, {
            'reason': 'completion_policy',
            'details': 'Max depth reached, stopping traversal'
        }),
    ]

    for i, (from_state, to_state, node_id, action, metadata) in enumerate(steps_data, 1):
        tracer.steps.append(TraceStep(
            step_number=i,
            timestamp=datetime.now(),
            from_state=from_state,
            to_state=to_state,
            node_id=node_id,
            action=action,
            metadata=metadata,
        ))

    return tracer


def main():
    """主函数：生成所有可视化输出。"""
    print("🎨 V6 仿真机可视化演示")
    print("=" * 70)

    # 创建演示遍历
    tracer = create_demo_traversal()

    # 1. ASCII 树（基础版）
    print("\n📊 ASCII 树结构")
    print("-" * 70)
    tree = tracer.render_tree()
    print(tree)

    # 1.5 ASCII 树（带原因）
    print("\n📊 ASCII 树结构 (含未访问原因)")
    print("-" * 70)
    tree_with_reasons = tracer.render_tree_with_reasons()
    print(tree_with_reasons)

    # 2. Mermaid 图表
    print("\n📈 Mermaid 状态图")
    print("-" * 70)
    mermaid = tracer.render_mermaid()
    print(mermaid)
    print("\n💡 复制上述代码到 https://mermaid.live/ 查看交互式图表")

    # 3. JSONL 导出
    print("\n📋 JSONL 追踪数据")
    print("-" * 70)
    jsonl = tracer.export_trace('jsonl')
    print(jsonl)

    # 4. 统计信息
    print("\n📊 统计信息")
    print("-" * 70)
    total = len(tracer.visited_tree)
    visited = sum(1 for n in tracer.visited_tree.values() if n.visited)
    print(f"  总节点数: {total}")
    print(f"  总步骤数: {tracer.get_step_count()}")
    print(f"  已访问: {visited}/{total} ({visited*100//total if total > 0 else 0}%)")
    print(f"  未访问: {total-visited}/{total} ({(total-visited)*100//total if total > 0 else 0}%)")

    # 4.5 操作对比 (预期 vs 实际)
    print("\n🔄 操作对比 (预期 vs 实际)")
    print("-" * 70)
    for node_id, node in tracer.visited_tree.items():
        if node.expected_operation:
            # 找到实际执行的操作
            actual = next((s.action for s in tracer.steps if s.node_id == node_id and s.action), None)
            actual_str = actual if actual else '未执行'
            status = '✅' if node.visited else '❌'
            print(f"  {status} {node.name}:")
            print(f"     预期: {node.expected_operation}")
            print(f"     实际: {actual_str}")

    # 4.6 未访问节点总结
    unvisited = tracer.get_unvisited_summary()
    if unvisited:
        print("\n📋 未访问节点详情")
        print("-" * 70)
        for item in unvisited:
            print(f"  ❌ {item['name']} [{item['node_type']}]")
            print(f"     原因: {item['reason']}")
            if item.get('details'):
                print(f"     详情: {item['details']}")

    # 5. 保存 HTML 报告
    print("\n💾 保存 HTML 报告")
    print("-" * 70)
    html = tracer.export_trace('html')
    output_path = Path('/tmp/v6_simulation_demo.html')
    output_path.write_text(html)
    print(f"  ✅ 已保存到: {output_path}")
    print(f"  📏 文件大小: {len(html)} 字节")

    # 6. 保存 JSONL
    jsonl_path = Path('/tmp/v6_simulation_trace.jsonl')
    jsonl_path.write_text(jsonl)
    print(f"  ✅ JSONL 已保存到: {jsonl_path}")

    print("\n" + "=" * 70)
    print("✨ 可视化演示完成！")
    print(f"💡 在浏览器中打开 {output_path} 查看完整报告")


if __name__ == '__main__':
    main()
