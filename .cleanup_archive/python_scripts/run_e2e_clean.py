#!/usr/bin/env python3
"""
可执行的E2E仿真测试脚本 (无特殊字符版本)
运行：python run_e2e_clean.py
"""
import json
import sys
from pathlib import Path

# 添加项目路径
sys.path.insert(0, str(Path(__file__).parent))

from tests.simulation.helpers.test_runner import SimulationTestRunner

def main():
    """主函数 - 运行E2E测试"""
    print("=" * 70)
    print("E2E Simulation Test Runner")
    print("=" * 70)

    # 测试用例路径
    test_case_path = "tests/simulation/fixtures/e2e_all_traversal/test_case.json"

    # 检查文件是否存在
    if not Path(test_case_path).exists():
        print(f"[ERROR] Test file not found: {test_case_path}")
        return 1

    try:
        # 创建测试运行器
        runner = SimulationTestRunner()

        # 运行测试
        print(f"\n[INFO] Running test: {test_case_path}")
        print("-" * 70)

        result = runner.run_simulation_test(test_case_path)

        # 显示结果摘要
        print("\n" + "=" * 70)
        print("Test Result Summary")
        print("=" * 70)

        sim_result = result['simulation_result']
        assertion = result['assertion_result']

        status = "PASS" if result['passed'] else "FAIL"
        symbol = "[PASS]" if result['passed'] else "[FAIL]"
        print(f"Test Status: {symbol} {status}")
        print(f"Completion Reason: {sim_result.completion_reason}")
        print(f"Total Steps: {sim_result.statistics.get('total_steps', 'N/A')}")
        print(f"Visited Nodes: {sim_result.statistics.get('visited_nodes', 'N/A')}")

        print(f"\nAssertion Results:")
        assert_symbol = "[PASS]" if assertion.success else "[FAIL]"
        print(f"  - Assertion Success: {assert_symbol} {assertion.success}")
        print(f"  - Events Matched: {assertion.key_events_matched}")
        print(f"  - Missing Events: {len(assertion.missing_events)}")
        print(f"  - Extra Events: {len(assertion.extra_events)}")

        if assertion.missing_events:
            print(f"\n[MISSING] Key Events:")
            for event in assertion.missing_events[:5]:
                print(f"    - {event}")
            if len(assertion.missing_events) > 5:
                print(f"    ... and {len(assertion.missing_events) - 5} more")

        if assertion.extra_events:
            print(f"\n[EXTRA] Unexpected Events:")
            for event in assertion.extra_events[:3]:
                print(f"    - {event}")

        print("\n" + "=" * 70)

        # 返回退出码
        return 0 if result['passed'] else 1

    except Exception as e:
        print(f"\n[ERROR] Test execution failed: {e}")
        import traceback
        traceback.print_exc()
        return 2

if __name__ == "__main__":
    exit_code = main()
    print(f"\nExit Code: {exit_code}")
    sys.exit(exit_code)