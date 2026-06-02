#!/usr/bin/env python3
"""
使用真实状态机和执行器的仿真测试

测试 GraphTraversalEngine 和 TraversalStateMachine 在仿真环境中的运行。
"""

import json
from pathlib import Path

from src.graph.plan import TraversalPlan
from src.simulation.mock_vision import MockVisionService
from src.simulation.visualizer import InMemoryTracer
from src.traversal.graph_engine import GraphTraversalEngine
from src.simulation.operation_executor import MockOperationExecutor


def main():
    """主函数：使用真实组件执行仿真测试。"""
    print("🧪 V6 仿真机真实组件测试")
    print("=" * 70)

    # 加载测试计划
    fixture_path = Path(__file__).parent.parent.parent / 'tests' / 'v6' / 'fixtures' / 'plan_static.json'
    with open(fixture_path, 'r') as f:
        plan_data = json.load(f)

    plan = TraversalPlan.from_json(json.dumps(plan_data))

    print(f"✅ 加载计划: {plan.meta.get('description', 'N/A')}")
    print(f"   入口应用: {plan.entry_app}")
    print(f"   遍历模式: {plan.mode.value}")
    print(f"   完成策略: {plan.completion_policy.type.value}")
    print()

    # 加载虚拟页面
    pages_path = Path(__file__).parent.parent.parent / 'tests' / 'v6' / 'fixtures' / 'pages_all.json'
    with open(pages_path, 'r') as f:
        virtual_pages = json.load(f)

    print(f"✅ 加载了 {len(virtual_pages)} 个虚拟页面")
    print()

    # 创建仿真组件
    vision = MockVisionService(virtual_pages)
    action = MockActionExecutor()
    tracer = InMemoryTracer()

    print("🔧 创建仿真组件:")
    print(f"   - MockVisionService: {len(vision.virtual_pages)} 页")
    print(f"   - MockActionExecutor: 已就绪")
    print(f"   - InMemoryTracer: 已就绪")
    print()

    # 创建遍历引擎
    engine = GraphTraversalEngine(
        plan=plan,
        vision_service=vision,
        action_executor=action,
        trace_recorder=tracer,
    )

    print("✅ 创建 GraphTraversalEngine")
    print(f"   - 状态机: {type(engine.state_machine).__name__}")
    print(f"   - 最大深度: {engine.context.max_depth}")
    print(f"   - 节点注册数: {len(engine._node_registry)}")
    print()

    # 执行遍历 (run() 会自动调用 initialize())
    print("🔄 执行遍历...")
    try:
        result = engine.run()

        print(f"   状态: {result.status}")
        print(f"   访问节点数: {len(result.visited_nodes)}")
        print(f"   执行步骤数: {result.total_steps}")
        print(f"   执行时间: {result.elapsed_seconds:.3f}秒")

        # 如果有错误，显示错误信息
        if result.error:
            print(f"   ❌ 错误: {result.error}")

        print(f"   状态: {result.status}")
        print(f"   访问节点数: {len(result.visited_nodes)}")
        print(f"   执行步骤数: {result.total_steps}")
        print(f"   执行时间: {result.elapsed_seconds:.3f}秒")
        print()

        # 显示追踪结果
        print("📊 遍历结果追踪")
        print("-" * 70)
        print(tracer.render_tree_with_reasons())
        print()

        # 状态转换统计
        print("📈 状态转换统计")
        print("-" * 70)
        state_counts = {}
        for step in tracer.steps:
            state_counts[step.to_state] = state_counts.get(step.to_state, 0) + 1
        for state, count in sorted(state_counts.items()):
            print(f"   {state}: {count} 次")
        print()

        # 操作对比
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

        # 生成报告
        html = tracer.export_trace('html')
        output_path = Path('/tmp/v6_real_engine_report.html')
        output_path.write_text(html)

        print("💾 报告生成")
        print("-" * 70)
        print(f"  ✅ HTML 报告: {output_path}")
        print(f"  📏 文件大小: {len(html)} 字节")

    except Exception as e:
        print(f"   ❌ 执行失败: {e}")
        import traceback
        traceback.print_exc()

        # 显示当前状态机状态
        print()
        print("🔍 状态机调试信息:")
        print(f"   当前状态: {engine.state_machine.state}")
        print(f"   上下文: {engine.context}")
        if hasattr(engine, '_node_registry'):
            print(f"   注册节点: {list(engine._node_registry.keys())}")

    print()
    print("=" * 70)
    print("✨ 测试完成！")


if __name__ == '__main__':
    main()
