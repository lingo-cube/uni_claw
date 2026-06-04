#!/usr/bin/env python3
"""
{MODULE_NAME} 模块统一单元测试脚本

运行 {module} 模块的所有单元测试并生成 AI 可识别的测试报告。

用法:
    python run_tests.py              # 运行所有测试
    python run_tests.py -v          # 详细输出
    python run_tests.py --coverage   # 生成覆盖率报告

输出:
    测试报告: test/test_report.json (AI可读)
    控制台输出: 结构化JSON格式
"""

import argparse
import json
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Any, Dict


class TestRunner:
    """统一的测试运行器"""

    def __init__(self, module_name: str):
        self.module_name = module_name
        self.module_path = Path(__file__).parent
        self.test_path = self.module_path / "test"

    def run_tests(self, verbose: bool = False, coverage: bool = False) -> int:
        """运行测试并返回退出码"""
        print(f"🧪 运行 {self.module_name} 模块测试...")

        # 构建pytest命令
        cmd = [sys.executable, "-m", "pytest", str(self.test_path)]

        if verbose:
            cmd.append("-v")

        cmd.append("--tb=short")  # 简洁的错误追踪

        if coverage:
            cmd.extend([
                "--cov", f"src.{self.module_name}",
                "--cov-report=term-missing"
            ])

        # 运行测试
        print(f"📂 测试路径: {self.test_path}")
        print(f"🔧 命令: {' '.join(cmd)}")

        result = subprocess.run(
            cmd,
            cwd=self.module_path.parent.parent,
            capture_output=True,
            text=True
        )

        # 显示输出
        if result.stdout:
            print("\n" + result.stdout)

        # 解析结果
        test_result = self._parse_test_result(result.stdout, result.stderr)

        # 生成AI可读报告
        self._generate_ai_report(test_result)

        # 返回退出码
        return 0 if test_result["status"] == "passed" else 1

    def _parse_test_result(self, stdout: str, stderr: str) -> Dict[str, Any]:
        """解析pytest输出"""
        lines = stdout.split('\n')

        # 统计测试结果
        passed = sum(1 for line in lines if 'PASSED' in line)
        failed = sum(1 for line in lines if 'FAILED' in line)
        errors = sum(1 for line in lines if 'ERROR' in line)
        skipped = sum(1 for line in lines if 'SKIPPED' in line)

        # 尝试解析pytest的摘要行
        for line in lines:
            # 匹配类似 "15 passed, 2 failed in 2.34s" 的行
            match = re.search(r'(\d+) passed(?:, (\d+) failed)?(?:, (\d+) skipped)?(?:, (\d+) errors)? in ([\d.]+s)?', line)
            if match:
                passed = int(match.group(1))
                failed = int(match.group(2)) if match.group(2) else 0
                skipped = int(match.group(3)) if match.group(3) else 0
                errors = int(match.group(4)) if match.group(4) else 0
                break

        total = passed + failed + errors + skipped

        # 确定状态
        if failed > 0 or errors > 0:
            status = "failed"
            ai_interpretation = f"测试失败：{failed} 个失败，{errors} 个错误，需要修复"
        else:
            status = "passed"
            ai_interpretation = f"测试通过：{total} 个测试全部通过"

        return {
            "module": self.module_name,
            "timestamp": datetime.now().isoformat(),
            "status": status,
            "summary": {
                "total": total,
                "passed": passed,
                "failed": failed,
                "errors": errors,
                "skipped": skipped
            },
            "ai_interpretation": ai_interpretation,
            "exit_code": 0 if status == "passed" else 1
        }

    def _generate_ai_report(self, test_result: Dict[str, Any]):
        """生成AI可读的测试报告"""
        # 保存JSON报告
        report_path = self.test_path / "test_report.json"

        # 确保test目录存在
        self.test_path.mkdir(parents=True, exist_ok=True)

        with open(report_path, 'w', encoding='utf-8') as f:
            json.dump(test_result, f, indent=2, ensure_ascii=False)

        print(f"\n📄 测试报告已保存: {report_path}")

        # 输出AI可读的JSON到stdout
        print("\n" + "="*70)
        print("📊 AI可读测试结果:")
        print("="*70)
        print(json.dumps(test_result, ensure_ascii=False, indent=2))
        print("="*70)

        # 显示简洁状态
        if test_result["status"] == "passed":
            print(f"✅ 测试通过: {test_result['summary']['passed']}/{test_result['summary']['total']} 个测试通过")
        else:
            print(f"❌ 测试失败: {test_result['summary']['failed']} 失败, {test_result['summary']['errors']} 错误")


def main():
    """主函数"""
    # 从脚本路径推断模块名
    module_name = Path(__file__).parent.name

    parser = argparse.ArgumentParser(
        description=f"{module_name} 模块单元测试脚本",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument("-v", "--verbose", action="store_true", help="详细输出")
    parser.add_argument("-c", "--coverage", action="store_true", help="生成覆盖率报告")

    args = parser.parse_args()

    # 运行测试
    runner = TestRunner(module_name)
    exit_code = runner.run_tests(verbose=args.verbose, coverage=args.coverage)

    sys.exit(exit_code)


if __name__ == "__main__":
    main()