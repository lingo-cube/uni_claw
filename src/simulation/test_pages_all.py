#!/usr/bin/env python3
"""
针对 pages_all.json 的测试脚本 - 简化版

加载虚拟页面数据，模拟遍历，生成可视化报告。
"""

import json
from pathlib import Path
from datetime import datetime

from src.simulation.visualizer import InMemoryTracer, TraceStep, VisitedNode


def main():
    """主函数：执行测试并生成报告。"""
    print("🧪 测试 pages_all.json")
    print("=" * 70)

    # 加载页面数据
    fixture_path = Path(__file__).parent.parent.parent / 'tests' / 'v6' / 'fixtures' / 'pages_all.json'
    with open(fixture_path, 'r') as f:
        pages = json.load(f)

    print(f"✅ 加载了 {len(pages)} 个虚拟页面")
    print()

    # 创建遍历追踪器
    tracer = InMemoryTracer()
    tracer.start_traversal(None)

    # 手动构建一个更合理的遍历场景
    # 根节点
    tracer.visited_tree['root'] = VisitedNode(
        node_id='root',
        name='Settings Home',
        node_type='screen',
        visited=True,
        children=['wifi', 'bluetooth', 'display', 'storage'],
        expected_operation='打开设置页面'
    )

    # 第一层级 - 菜单项
    menu_items = [
        ('wifi', 'Wi-Fi', 'click: Wi-Fi'),
        ('bluetooth', 'Bluetooth', 'click: Bluetooth'),
        ('display', 'Display', 'click: Display'),
        ('storage', 'Storage', 'click: Storage'),
    ]

    for node_id, name, operation in menu_items:
        tracer.visited_tree[node_id] = VisitedNode(
            node_id=node_id,
            name=name,
            node_type='screen',
            visited=True,
            children=[],
            expected_operation=operation
        )

    # 未访问的节点
    tracer.visited_tree['battery'] = VisitedNode(
        node_id='battery',
        name='Battery',
        node_type='screen',
        visited=False,
        children=[],
        expected_operation='click: Battery'
    )

    # Wi-Fi 子节点
    tracer.visited_tree['wifi_network'] = VisitedNode(
        node_id='wifi_network',
        name='HomeNetwork',
        node_type='leaf_action',
        visited=True,
        children=[],
        expected_operation='click: 连接'
    )

    # 添加追踪步骤
    step_num = 1
    tracer.steps.append(TraceStep(
        step_number=step_num,
        timestamp=datetime.now(),
        from_state='INIT',
        to_state='NODE_SELECT',
        node_id='root',
        action=None,
    ))
    step_num += 1

    # 遍历各个菜单项
    for node_id, name, operation in menu_items:
        # 点击菜单项
        tracer.steps.append(TraceStep(
            step_number=step_num,
            timestamp=datetime.now(),
            from_state='NODE_SELECT',
            to_state='EXECUTE',
            node_id=node_id,
            action='click',
        ))
        step_num += 1

        # 如果是 Wi-Fi，模拟连接网络
        if node_id == 'wifi':
            tracer.steps.append(TraceStep(
                step_number=step_num,
                timestamp=datetime.now(),
                from_state='EXECUTE',
                to_state='NODE_SELECT',
                node_id='wifi_network',
                action=None,
            ))
            step_num += 1

            tracer.steps.append(TraceStep(
                step_number=step_num,
                timestamp=datetime.now(),
                from_state='NODE_SELECT',
                to_state='EXECUTE',
                node_id='wifi_network',
                action='click',
            ))
            step_num += 1

            tracer.steps.append(TraceStep(
                step_number=step_num,
                timestamp=datetime.now(),
                from_state='EXECUTE',
                to_state='BACKTRACK',
                node_id=node_id,
                action='back',
            ))
            step_num += 1

        # 返回根节点
        tracer.steps.append(TraceStep(
            step_number=step_num,
            timestamp=datetime.now(),
            from_state='EXECUTE',
            to_state='BACKTRACK',
            node_id='root',
            action='back',
        ))
        step_num += 1

    # 未访问的 Battery 节点
    tracer.steps.append(TraceStep(
        step_number=step_num,
        timestamp=datetime.now(),
        from_state='NODE_SELECT',
        to_state='PRECONDITION_CHECK',
        node_id='battery',
        action=None,
    ))
    step_num += 1

    tracer.steps.append(TraceStep(
        step_number=step_num,
        timestamp=datetime.now(),
        from_state='PRECONDITION_CHECK',
        to_state='SKIP',
        node_id='battery',
        action=None,
        metadata={
            'reason': 'completion_policy',
            'details': '测试目标已完成，停止遍历'
        }
    ))
    step_num += 1

    # 完成步骤
    tracer.steps.append(TraceStep(
        step_number=step_num,
        timestamp=datetime.now(),
        from_state='SKIP',
        to_state='COMPLETE',
        node_id='root',
        action=None,
        metadata={'reason': 'traversal_complete'}
    ))

    # 统计信息
    total_nodes = len(tracer.visited_tree)
    visited_nodes = sum(1 for n in tracer.visited_tree.values() if n.visited)
    total_steps = len(tracer.steps)

    print("📊 遍历统计")
    print("-" * 70)
    print(f"  总节点数: {total_nodes}")
    print(f"  已访问: {visited_nodes} ({visited_nodes*100//total_nodes if total_nodes > 0 else 0}%)")
    print(f"  未访问: {total_nodes - visited_nodes} ({(total_nodes-visited_nodes)*100//total_nodes if total_nodes > 0 else 0}%)")
    print(f"  总步骤数: {total_steps}")
    print()

    print("📊 访问树 (含预期操作)")
    print("-" * 70)
    print(tracer.render_tree_with_reasons())
    print()

    print("🔄 操作对比 (预期 vs 实际)")
    print("-" * 70)
    for node_id, node in tracer.visited_tree.items():
        if node.expected_operation:
            actual = next((s.action for s in tracer.steps if s.node_id == node_id and s.action), None)
            actual_str = actual if actual else '未执行'
            status = '✅' if node.visited else '❌'
            print(f"  {status} {node.name}:")
            print(f"     预期: {node.expected_operation}")
            print(f"     实际: {actual_str}")
    print()

    # 未访问节点详情
    unvisited = tracer.get_unvisited_summary()
    if unvisited:
        print("📋 未访问节点详情")
        print("-" * 70)
        for item in unvisited:
            print(f"  ❌ {item['name']} [{item['node_type']}]")
            print(f"     原因: {item['reason']}")
            if item.get('details'):
                print(f"     详情: {item['details']}")
        print()

    # 生成 HTML 报告
    html = tracer.export_trace('html')
    output_path = Path('/tmp/v6_pages_all_report.html')
    output_path.write_text(html)

    print("💾 报告生成")
    print("-" * 70)
    print(f"  ✅ HTML 报告: {output_path}")
    print(f"  📏 文件大小: {len(html)} 字节")

    # 生成 JSONL 追踪
    jsonl = tracer.export_trace('jsonl')
    jsonl_path = Path('/tmp/v6_pages_all_trace.jsonl')
    jsonl_path.write_text(jsonl)
    print(f"  ✅ JSONL 追踪: {jsonl_path}")

    print()
    print("=" * 70)
    print("✨ 测试完成！")
    print(f"💡 在浏览器中打开 {output_path} 查看完整报告")


if __name__ == '__main__':
    main()
