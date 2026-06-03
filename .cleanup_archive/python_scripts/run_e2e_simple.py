#!/usr/bin/env python3
"""
可执行的E2E仿真测试脚本
运行：python run_e2e_simple.py
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
    print("E2E仿真测试执行器")
    print("=" * 70)

    # 测试用例路径
    test_case_path = "tests/simulation/fixtures/e2e_all_traversal/test_case.json"

    # 检查文件是否存在
    if not Path(test_case_path).exists():
        print(f"❌ 测试文件不存在: {test_case_path}")
        return 1

    try:
        # 创建测试运行器
        runner = SimulationTestRunner()

        # 运行测试
        print(f"\n📝 运行测试: {test_case_path}")
        print("-" * 70)

        result = runner.run_simulation_test(test_case_path)

        # 显示结果摘要
        print("\n" + "=" * 70)
        print("测试结果摘要")
        print("=" * 70)

        sim_result = result['simulation_result']
        assertion = result['assertion_result']

        print(f"测试状态: {'✅ PASS' if result['passed'] else '❌ FAIL'}")
        print(f"完成原因: {sim_result.completion_reason}")
        print(f"总步数: {sim_result.statistics.get('total_steps', 'N/A')}")
        print(f"访问节点: {sim_result.statistics.get('visited_nodes', 'N/A')}")

        print(f"\n断言结果:")
        print(f"  - 断言成功: {'✅' if assertion.success else '❌'}")
        print(f"  - 事件匹配: {assertion.key_events_matched}")
        print(f"  - 缺失事件: {len(assertion.missing_events)}")
        print(f"  - 额外事件: {len(assertion.extra_events)}")

        if assertion.missing_events:
            print(f"\n❌ 缺失的关键事件:")
            for event in assertion.missing_events[:5]:
                print(f"    - {event}")
            if len(assertion.missing_events) > 5:
                print(f"    ... 还有 {len(assertion.missing_events) - 5} 个")

        if assertion.extra_events:
            print(f"\n⚠️  意外事件:")
            for event in assertion.extra_events[:3]:
                print(f"    - {event}")

        print("\n" + "=" * 70)

        # 返回退出码
        return 0 if result['passed'] else 1

    except Exception as e:
        print(f"\n❌ 测试执行失败: {e}")
        import traceback
        traceback.print_exc()
        return 2

if __name__ == "__main__":
    exit_code = main()
    sys.exit(exit_code)