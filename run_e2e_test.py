"""
E2E测试运行脚本 - 运行单个仿真测试并显示详细输入输出
"""
import json
from pathlib import Path
from tests.simulation.helpers.test_runner import SimulationTestRunner

def print_section(title: str):
    """打印分节标题"""
    try:
        print(f"\n{'='*60}")
        print(f"  {title}")
        print(f"{'='*60}")
    except UnicodeEncodeError:
        # Fallback for encoding issues
        print(f"\n{'='*60}")
        print(f"  {title.encode('ascii', 'ignore').decode('ascii')}")
        print(f"{'='*60}")

def print_test_input(test_case: dict, plan: dict, pages: dict):
    """打印测试输入"""
    print_section("📥 测试输入 (TEST INPUT)")

    print(f"\n📋 测试用例: {test_case['test_id']}")
    print(f"📝 描述: {test_case['description']}")

    print(f"\n🎯 意图槽位 (Intent Slots):")
    for key, value in test_case['intent_slots'].items():
        print(f"  • {key}: {value}")

    print(f"\n✅ 预期结果 (Expected):")
    expected = test_case['expected']
    print(f"  • 完成原因: {expected['completion_reason']}")
    print(f"  • 步数范围: {expected['total_steps_min']} - {expected['total_steps_max']} 步")
    print(f"  • 必须包含事件: {len(expected['key_events'])} 个")

    print(f"\n  关键事件序列:")
    for i, event in enumerate(expected['key_events'], 1):
        print(f"    {i}. {event}")

    print(f"\n🚫 禁止事项: {expected['must_not_contain']}")

    print(f"\n📊 断言要求:")
    assertions = test_case.get('assertions', {})
    for key, value in assertions.items():
        print(f"  • {key}: {value}")

    print(f"\n🗺️  遍历计划结构 (Plan Structure):")
    print(f"  • 根节点: {plan['root_node']['node_id']} ({plan['root_node']['name']})")
    print(f"  • 静态节点: {len(plan['static_nodes'])} 个")
    for node_id, node_data in plan['static_nodes'].items():
        print(f"    - {node_id}: {node_data['name']}")
    print(f"  • 最大深度: {plan['completion_policy']['max_depth']}")

    print(f"\n📱 虚拟页面结构 (Virtual Pages):")
    for path, page_data in pages.items():
        print(f"  • 页面 '{path}': {page_data['page_name']}")
        print(f"    元素: {len(page_data['elements'])} 个")
        for elem in page_data['elements'][:3]:  # 只显示前3个元素
            print(f"      - {elem['id']} ({elem['type']}): {elem.get('text', 'N/A')}")
        if len(page_data['elements']) > 3:
            print(f"      ... 还有 {len(page_data['elements']) - 3} 个元素")

def print_test_output(result: dict):
    """打印测试输出"""
    print_section("📤 测试输出 (TEST OUTPUT)")

    sim_result = result['simulation_result']

    print(f"\n✨ 执行结果:")
    print(f"  • 状态: {'✅ 通过' if result['passed'] else '❌ 失败'}")
    print(f"  • 完成原因: {sim_result.completion_reason}")

    print(f"\n📈 统计信息:")
    stats = sim_result.statistics
    print(f"  • 总步数: {stats.get('total_steps', 'N/A')}")
    print(f"  • 访问节点数: {stats.get('visited_nodes', 'N/A')}")
    print(f"  • 执行时间: {stats.get('execution_time_ms', 'N/A')} ms")

    print(f"\n🌲 访问树:")
    tree_str = sim_result.visited_tree
    if tree_str:
        lines = tree_str.split('\n')[:20]  # 只显示前20行
        for line in lines:
            print(f"  {line}")
        if len(tree_str.split('\n')) > 20:
            print(f"  ... (共 {len(tree_str.split('\n'))} 行)")

    print(f"\n🔗 执行追踪 (Execution Trace):")
    trace = sim_result.trace
    if trace and len(trace) > 0:
        print(f"  • 总追踪事件: {len(trace)} 个")
        for i, event in enumerate(trace[:15]):  # 只显示前15个事件
            event_type = event.get('type', 'unknown')
            event_desc = event.get('description', event.get('step', ''))
            print(f"    {i+1}. [{event_type}] {event_desc}")
        if len(trace) > 15:
            print(f"    ... 还有 {len(trace) - 15} 个事件")

    print(f"\n🎯 断言结果:")
    assertion = result['assertion_result']
    print(f"  • 断言成功: {'✅ 是' if assertion.success else '❌ 否'}")
    print(f"  • 匹配的关键事件: {len(assertion.matched_events)} 个")
    print(f"  • 缺失事件: {len(assertion.missing_events)} 个")
    print(f"  • 意外事件: {len(assertion.unexpected_events)} 个")

    if assertion.matched_events:
        print(f"\n  ✅ 匹配的事件:")
        for event in assertion.matched_events[:10]:
            print(f"    - {event}")
        if len(assertion.matched_events) > 10:
            print(f"    ... 还有 {len(assertion.matched_events) - 10} 个")

    if assertion.missing_events:
        print(f"\n  ❌ 缺失的事件:")
        for event in assertion.missing_events:
            print(f"    - {event}")

    if assertion.unexpected_events:
        print(f"\n  ⚠️  意外的事件:")
        for event in assertion.unexpected_events[:10]:
            print(f"    - {event}")
        if len(assertion.unexpected_events) > 10:
            print(f"    ... 还有 {len(assertion.unexpected_events) - 10} 个")

def main():
    """主函数"""
    # 运行 e2e_all_traversal 测试
    test_case_path = "tests/simulation/fixtures/e2e_all_traversal/test_case.json"

    print_section("🚀 E2E仿真测试运行")
    print(f"测试用例: {test_case_path}")

    # 加载测试文件
    test_path = Path(test_case_path)
    if not test_path.exists():
        print(f"❌ 测试文件不存在: {test_case_path}")
        return

    with open(test_path, 'r', encoding='utf-8') as f:
        test_case = json.load(f)

    # 加载 fixtures
    test_dir = Path(test_case['test_dir'])

    plan_path = test_dir / test_case['fixtures']['plan_file']
    with open(plan_path, 'r', encoding='utf-8') as f:
        plan_data = json.load(f)

    pages_path = test_dir / test_case['fixtures']['pages_file']
    with open(pages_path, 'r', encoding='utf-8') as f:
        pages_data = json.load(f)

    # 打印测试输入
    print_test_input(test_case, plan_data, pages_data)

    # 运行测试
    print_section("🔄 执行中...")
    try:
        runner = SimulationTestRunner()
        result = runner.run_simulation_test(test_case_path)

        # 打印测试输出
        print_test_output(result)

        # 最终结果
        print_section("🏁 测试完成")
        if result['passed']:
            print("✅ 测试通过！所有预期结果都匹配。")
        else:
            print("❌ 测试失败！某些预期结果未匹配。")
            print(f"\n失败原因: {result.get('assertion_result', {}).get('failure_reason', 'Unknown')}")

    except Exception as e:
        print(f"❌ 测试执行出错: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()