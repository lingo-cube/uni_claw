#!/usr/bin/env python3
"""
显示E2E测试的详细输入输出信息
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
    print_section("TEST INPUT (测试输入)")

    print(f"\n[TEST CASE] 测试用例: {test_case['test_id']}")
    print(f"[DESCRIPTION] 描述: {test_case['description']}")

    print(f"\n[INTENT SLOTS] 意图槽位:")
    for key, value in test_case['intent_slots'].items():
        print(f"  - {key}: {value}")

    print(f"\n[EXPECTED RESULTS] 预期结果:")
    expected = test_case['expected']
    print(f"  - Completion Reason: {expected['completion_reason']}")
    print(f"  - Step Range: {expected['total_steps_min']} - {expected['total_steps_max']} steps")
    print(f"  - Key Events Count: {len(expected['key_events'])} events")

    print(f"\n[KEY EVENTS] 关键事件序列:")
    for i, event in enumerate(expected['key_events'], 1):
        print(f"  {i}. {event}")

    print(f"\n[RESTRICTIONS] 禁止事项: {expected['must_not_contain']}")

    print(f"\n[ASSERTIONS] 断言要求:")
    assertions = test_case.get('assertions', {})
    for key, value in assertions.items():
        if isinstance(value, list):
            print(f"  - {key}: {', '.join(value)}")
        else:
            print(f"  - {key}: {value}")

    print(f"\n[PLAN STRUCTURE] 遍历计划结构:")
    print(f"  - Entry App: {plan.get('entry_app', 'N/A')}")
    print(f"  - Mode: {plan.get('mode', 'N/A')}")
    print(f"  - Root Node: {plan['root_node']['node_id']} ({plan['root_node']['name']})")
    print(f"  - Children Strategy: {plan['root_node']['children_strategy']['type']}")
    print(f"  - Dynamic Rules Count: {len(plan['root_node']['children_strategy'].get('dynamic_rules', {}))}")

    if 'dynamic_rules' in plan['root_node']['children_strategy']:
        print(f"\n  [DYNAMIC RULES] 动态规则:")
        for rule_name, rule_data in plan['root_node']['children_strategy']['dynamic_rules'].items():
            print(f"    - {rule_name}: {rule_data.get('child_template', 'N/A')}")

    print(f"\n[VIRTUAL PAGES] 虚拟页面结构:")
    for path, page_data in pages.items():
        print(f"  - Page '{path}': {len(page_data['items'])} items")
        if 'current_path' in page_data:
            print(f"    Current Path: {page_data['current_path']}")
        for item in page_data['items'][:2]:
            print(f"      - {item['name']} ({item['type']}): {item.get('expected_action', 'N/A')}")

def print_test_output(result):
    """打印测试输出"""
    print_section("TEST OUTPUT (测试输出)")

    sim_result = result['simulation_result']
    assertion = result['assertion_result']

    print(f"\n[RESULT] 执行结果:")
    print(f"  - Status: {'PASS (通过)' if result['passed'] else 'FAIL (失败)'}")
    print(f"  - Completion Reason: {sim_result.completion_reason}")

    print(f"\n[STATISTICS] 统计信息:")
    stats = sim_result.statistics
    print(f"  - Total Steps: {stats.get('total_steps', 'N/A')}")
    print(f"  - Visited Nodes: {stats.get('visited_nodes', 'N/A')}")
    print(f"  - Execution Time: {stats.get('execution_time_ms', 'N/A')} ms")

    print(f"\n[VISITED TREE] 访问树结构:")
    tree_data = sim_result.visited_tree
    if isinstance(tree_data, dict):
        print(f"  - Tree Type: Dictionary format")
        print(f"  - Root Keys: {list(tree_data.keys())[:5]}")
        print(f"  - Total Nodes: {len(tree_data)}")
        # 显示第一个节点的详情
        if tree_data:
            first_key = list(tree_data.keys())[0]
            first_node = tree_data[first_key]
            print(f"  - First Node ({first_key}):")
            if isinstance(first_node, dict):
                for key, value in first_node.items():
                    if not isinstance(value, dict):
                        print(f"    - {key}: {value}")

    print(f"\n[EXECUTION TRACE] 执行追踪:")
    trace = sim_result.trace
    if trace and len(trace) > 0:
        print(f"  - Total Events: {len(trace)}")
        for i, event in enumerate(trace[:10]):
            event_type = event.get('type', 'unknown')
            event_desc = event.get('description', event.get('step', ''))
            print(f"    {i+1}. [{event_type}] {event_desc}")
        if len(trace) > 10:
            print(f"    ... and {len(trace) - 10} more events")
    else:
        print(f"  - No trace events recorded")

    print(f"\n[ASSERTION RESULTS] 断言结果:")
    print(f"  - Assertion Success: {'YES (是)' if assertion.success else 'NO (否)'}")
    print(f"  - Events Matched: {assertion.key_events_matched}")
    print(f"  - Missing Events: {len(assertion.missing_events)}")
    print(f"  - Extra Events: {len(assertion.extra_events)}")
    print(f"  - Steps Valid: {'YES (是)' if assertion.steps_valid else 'NO (否)'}")
    print(f"  - Completion Reason Match: {'YES (是)' if assertion.completion_reason_match else 'NO (否)'}")

    if assertion.missing_events:
        print(f"\n  [MISSING EVENTS] 缺失事件:")
        for event in assertion.missing_events[:7]:
            print(f"    - {event}")
        if len(assertion.missing_events) > 7:
            print(f"    ... and {len(assertion.missing_events) - 7} more")

    if assertion.extra_events:
        print(f"\n  [EXTRA EVENTS] 额外事件:")
        for event in assertion.extra_events[:3]:
            print(f"    - {event}")

    if assertion.violations:
        print(f"\n  [VIOLATIONS] 违规:")
        for violation in assertion.violations[:3]:
            print(f"    - {violation}")

def main():
    """主函数"""
    print_section("E2E ALL TRAVERSAL TEST EXECUTION")

    test_case_path = "tests/simulation/fixtures/e2e_all_traversal/test_case.json"

    if not Path(test_case_path).exists():
        print(f"[ERROR] Test file not found: {test_case_path}")
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
    print_section("RUNNING TEST EXECUTION")

    try:
        runner = SimulationTestRunner()
        result = runner.run_simulation_test(test_case_path)

        # 显示输出
        print_test_output(result)

        # 最终结果
        print_section("TEST COMPLETION SUMMARY")
        if result['passed']:
            print("[SUCCESS] Test PASSED! All expected results matched.")
            return 0
        else:
            print("[FAILURE] Test FAILED! Some expected results not matched.")
            if result['assertion_result'].violations:
                print(f"\n[VIOLATION DETAILS]")
                for violation in result['assertion_result'].violations[:5]:
                    print(f"  - {violation}")
            return 1

    except Exception as e:
        print(f"[ERROR] Test execution failed: {e}")
        import traceback
        traceback.print_exc()
        return 2

if __name__ == "__main__":
    exit_code = main()
    print(f"\n[FINAL EXIT CODE: {exit_code}]")
    sys.exit(exit_code)