#!/usr/bin/env python3
"""
详细的E2E仿真测试脚本 - 显示完整输入输出
运行：python run_e2e_detailed.py
"""
import json
import sys
from pathlib import Path

# 添加项目路径
sys.path.insert(0, str(Path(__file__).parent))

from tests.simulation.helpers.test_runner import SimulationTestRunner

def print_section(title):
    """打印分节标题"""
    print(f"\n{'='*70}")
    print(f"  {title}")
    print('='*70)

def print_test_input(test_case, plan, pages):
    """打印测试输入"""
    print_section("📥 测试输入 (INPUT)")

    print(f"\n📋 测试用例: {test_case['test_id']}")
    print(f"📝 描述: {test_case['description']}")

    print(f"\n🎯 意图槽位 (Intent Slots):")
    for key, value in test_case['intent_slots'].items():
        print(f"  • {key}: {value}")

    print(f"\n✅ 预期结果 (Expected):")
    expected = test_case['expected']
    print(f"  • 完成原因: {expected['completion_reason']}")
    print(f"  • 步数范围: {expected['total_steps_min']} - {expected['total_steps_max']} 步")

    print(f"\n  关键事件序列:")
    for i, event in enumerate(expected['key_events'], 1):
        print(f"    {i}. {event}")

    print(f"\n📊 断言要求:")
    assertions = test_case.get('assertions', {})
    for key, value in assertions.items():
        print(f"  • {key}: {value}")

    print(f"\n🗺️  遍历计划结构:")
    print(f"  • 根节点: {plan['root_node']['node_id']} ({plan['root_node']['name']})")
    print(f"  • 静态节点: {len(plan['static_nodes'])} 个")
    for node_id, node_data in plan['static_nodes'].items():
        print(f"    - {node_id}: {node_data['name']}")

    print(f"\n📱 虚拟页面结构:")
    for path, page_data in pages.items():
        print(f"  • 页面 '{path}': {page_data['page_name']}")
        print(f"    元素: {len(page_data['elements'])} 个")
        for elem in page_data['elements'][:2]:
            print(f"      - {elem['id']} ({elem['type']}): {elem.get('text', 'N/A')}")

def print_test_output(result):
    """打印测试输出"""
    print_section("📤 测试输出 (OUTPUT)")

    sim_result = result['simulation_result']
    assertion = result['assertion_result']

    print(f"\n✨ 执行结果:")
    print(f"  • 状态: {'✅ PASS' if result['passed'] else '❌ FAIL'}")
    print(f"  • 完成原因: {sim_result.completion_reason}")

    print(f"\n📈 统计信息:")
    stats = sim_result.statistics
    print(f"  • 总步数: {stats.get('total_steps', 'N/A')}")
    print(f"  • 访问节点: {stats.get('visited_nodes', 'N/A')}")
    print(f"  • 执行时间: {stats.get('execution_time_ms', 'N/A')} ms")

    print(f"\n🔗 执行追踪 (Trace):")
    trace = sim_result.trace
    if trace and len(trace) > 0:
        print(f"  • 总追踪事件: {len(trace)} 个")
        for i, event in enumerate(trace[:10]):
            event_type = event.get('type', 'unknown')
            event_desc = event.get('description', event.get('step', ''))
            print(f"    {i+1}. [{event_type}] {event_desc}")
        if len(trace) > 10:
            print(f"    ... 还有 {len(trace) - 10} 个事件")

    print(f"\n🎯 断言结果:")
    print(f"  • 断言成功: {'✅ YES' if assertion.success else '❌ NO'}")
    print(f"  • 匹配事件: {assertion.key_events_matched}")
    print(f"  • 缺失事件: {len(assertion.missing_events)} 个")
    print(f"  • 额外事件: {len(assertion.extra_events)} 个")
    print(f"  • 步数有效: {'✅ YES' if assertion.steps_valid else '❌ NO'}")
    print(f"  • 完成原因匹配: {'✅ YES' if assertion.completion_reason_match else '❌ NO'}")

    if assertion.missing_events:
        print(f"\n  ❌ 缺失的事件:")
        for event in assertion.missing_events[:5]:
            print(f"    - {event}")
        if len(assertion.missing_events) > 5:
            print(f"    ... 还有 {len(assertion.missing_events) - 5} 个")

    if assertion.extra_events:
        print(f"\n  ⚠️  额外的事件:")
        for event in assertion.extra_events[:3]:
            print(f"    - {event}")

def main():
    """主函数"""
    print_section("🚀 E2E仿真测试运行")

    test_case_path = "tests/simulation/fixtures/e2e_all_traversal/test_case.json"

    if not Path(test_case_path).exists():
        print(f"❌ 测试文件不存在: {test_case_path}")
        return 1

    # 加载测试文件以显示输入
    with open(test_case_path, 'r', encoding='utf-8') as f:
        test_case = json.load(f)

    test_dir = Path(test_case['test_dir'])
    plan_path = test_dir / test_case['fixtures']['plan_file']
    pages_path = test_dir / test_case['fixtures']['pages_file']

    with open(plan_path, 'r', encoding='utf-8') as f:
        plan = json.load(f)

    with open(pages_path, 'r', encoding='utf-8') as f:
        pages = json.load(f)

    # 显示输入
    print_test_input(test_case, plan, pages)

    # 运行测试
    print_section("🔄 执行中...")

    try:
        runner = SimulationTestRunner()
        result = runner.run_simulation_test(test_case_path)

        # 显示输出
        print_test_output(result)

        # 最终结果
        print_section("🏁 测试完成")
        if result['passed']:
            print("✅ 测试通过！所有预期结果都匹配。")
            return 0
        else:
            print("❌ 测试失败！某些预期结果未匹配。")
            if result['assertion_result'].violations:
                print(f"\n失败原因:")
                for violation in result['assertion_result'].violations[:3]:
                    print(f"  - {violation}")
            return 1

    except Exception as e:
        print(f"❌ 测试执行出错: {e}")
        import traceback
        traceback.print_exc()
        return 2

if __name__ == "__main__":
    exit_code = main()
    print(f"\n退出码: {exit_code}")
    sys.exit(exit_code)