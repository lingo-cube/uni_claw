#!/usr/bin/env python3
"""
检查trace数据结构，理解操作信息来源
"""
import sys
import json
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from src.simulation.runner import SimulationRunner
from src.graph.plan import TraversalPlan

def main():
    """分析trace数据结构"""
    print("=" * 70)
    print("分析Trace数据结构")
    print("=" * 70)

    # Load test fixtures
    with open('tests/simulation/fixtures/e2e_all_traversal/plan_all.json', 'r', encoding='utf-8') as f:
        plan_data = json.load(f)
    with open('tests/simulation/fixtures/e2e_all_traversal/pages_all.json', 'r', encoding='utf-8') as f:
        virtual_pages = json.load(f)

    # Create and run simulation
    plan = TraversalPlan.from_json(json.dumps(plan_data))
    runner = SimulationRunner(virtual_pages, plan)
    result = runner.run()

    # Analyze trace structure
    print(f"\n总步数: {len(result.trace)}")
    print(f"访问节点数: {len(result.visited_tree)}")

    # Check trace step structure
    print(f"\n[TraceStep结构分析]")
    if result.trace:
        step = result.trace[0]
        print(f"第一个步骤的字段:")
        if hasattr(step, 'to_dict'):
            step_dict = step.to_dict()
            for key, value in step_dict.items():
                print(f"  - {key}: {value}")
        else:
            print(f"  {step}")

    # Check visited tree structure
    print(f"\n[VisitedTree结构分析]")
    for node_id, node in result.visited_tree.items():
        print(f"节点 {node_id}:")
        print(f"  - 类型: {type(node)}")
        print(f"  - 字段: {node.__dict__.keys() if hasattr(node, '__dict__') else 'N/A'}")
        if hasattr(node, 'expected_operation'):
            print(f"  - 预期操作: {node.expected_operation}")
        if hasattr(node, 'actual_action'):
            print(f"  - 实际操作: {node.actual_action}")
        break  # 只显示第一个节点

    # Analyze operations per node
    print(f"\n[每个节点的操作分析]")
    from tests.simulation.helpers.assertions import TraceAsserter

    operations_by_node = {}
    for step in result.trace:
        step_dict = step.to_dict() if hasattr(step, 'to_dict') else step
        node_id = step_dict.get('current_node', 'unknown')
        action = step_dict.get('action_type', 'unknown')
        target_info = step_dict.get('target_info', {})
        target = target_info.get('element_id', target_info.get('text', ''))

        if node_id not in operations_by_node:
            operations_by_node[node_id] = []

        operation_desc = f"{action}:{target}" if target else action
        operations_by_node[node_id].append(operation_desc)

    for node_id, operations in operations_by_node.items():
        print(f"节点 {node_id}:")
        for op in operations:
            print(f"  - {op}")

    # Check HTML report expectations
    print(f"\n[HTML报告需求分析]")
    print(f"访问树需要:")
    print(f"  - 节点名称和类型")
    print(f"  - 访问状态 (visited/not visited)")
    print(f"  - 预期操作 (从哪里获取?)")
    print(f"  - 实际执行的操作 (从trace提取)")
    print(f"  - 未访问原因 (如果适用)")

    print(f"\n操作对比表需要:")
    print(f"  - 节点名称")
    print(f"  - 预期操作")
    print(f"  - 实际执行的操作")
    print(f"  - 匹配状态")

if __name__ == "__main__":
    main()