#!/usr/bin/env python3
"""
统一测试协调器

Uni-Claw项目的统一测试执行和validation报告生成入口。
这是项目唯一的测试入口，替代所有模块的run_tests.py。
"""

import argparse
import json
import subprocess
import sys
import re
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Any, Optional


class UnifiedTestCoordinator:
    """统一测试协调器 - Uni-Claw项目的唯一测试入口"""

    def __init__(self, project_root: Optional[Path] = None):
        self.project_root = project_root or Path.cwd()
        self.validation_dir = self.project_root / "docs" / "validation"
        self.test_results = {
            "timestamp": None,
            "unit_tests": {},
            "integration_tests": {},
            "summary": {}
        }

    def run_all_tests(self, scope: str = "all") -> Dict[str, Any]:
        """
        运行所有测试并自动生成validation报告

        Args:
            scope: 测试范围
                - "all": 所有测试（单元+集成）
                - "unit": 仅单元测试
                - "integration": 仅集成测试
                - "v6": V6测试
                - "ci": CI快速测试
        """
        print(f"🧪 Uni-Claw统一测试协调器")
        print(f"📋 测试范围: {scope}")
        print("=" * 70)

        # 1. 运行测试
        if scope in ["all", "unit", "v6", "ci"]:
            self.test_results["unit_tests"] = self._run_unit_tests(scope)

        if scope in ["all", "integration", "v6"]:
            self.test_results["integration_tests"] = self._run_integration_tests(scope)

        self.test_results["timestamp"] = datetime.now().isoformat()

        # 2. 生成汇总
        self.test_results["summary"] = self._generate_summary()

        # 3. 自动生成validation报告
        self._generate_validation_reports()

        # 4. 显示结果
        self._display_results()

        return self.test_results

    def _run_unit_tests(self, scope: str) -> Dict[str, Any]:
        """运行单元测试"""
        print("\n📋 运行单元测试...")

        if scope == "v6":
            test_paths = ["tests/v6/"]
        elif scope == "ci":
            test_paths = ["tests/v6/test_simulation.py", "tests/v6/test_state_machine.py"]
        else:
            test_paths = ["tests/v6/", "tests/models/", "tests/graph/"]

        all_results = {}

        for test_path in test_paths:
            path = Path(test_path)
            if not path.exists():
                continue

            print(f"  🧪 测试路径: {test_path}")

            result = subprocess.run(
                [sys.executable, "-m", "pytest", str(test_path), "-v", "--tb=short"],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            parsed = self._parse_pytest_output(result.stdout, test_path)
            all_results[test_path] = parsed

            # 实时显示简要结果
            total = parsed['summary']['total']
            passed = parsed['summary']['passed']
            failed = parsed['summary']['failed']
            print(f"    📊 {test_path}: {passed}/{total} passed, {failed} failed")

        return all_results

    def _run_integration_tests(self, scope: str) -> Dict[str, Any]:
        """运行集成测试"""
        print("\n🔄 运行集成测试...")

        if scope == "v6":
            test_paths = ["tests/v6/test_examples.py"]
        else:
            test_paths = ["tests/integration/", "tests/v6/test_examples.py"]

        all_results = {}

        for test_path in test_paths:
            path = Path(test_path)
            if not path.exists():
                continue

            print(f"  🧪 测试路径: {test_path}")

            result = subprocess.run(
                [sys.executable, "-m", "pytest", str(test_path), "-v", "--tb=short"],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            parsed = self._parse_pytest_output(result.stdout, test_path)
            all_results[test_path] = parsed

            total = parsed['summary']['total']
            passed = parsed['summary']['passed']
            failed = parsed['summary']['failed']
            print(f"    📊 {test_path}: {passed}/{total} passed, {failed} failed")

        return all_results

    def _parse_pytest_output(self, output: str, test_path: str) -> Dict[str, Any]:
        """解析pytest输出为结构化数据"""
        lines = output.split('\n')

        tests = []
        passed, failed, skipped, errors = 0, 0, 0, 0

        # 解析测试行
        test_pattern = re.compile(r'(.+\.py)::(.+)::(.+)\s+(PASSED|FAILED|ERROR|SKIPPED)')

        for line in lines:
            match = test_pattern.match(line)
            if match:
                file_path, test_class, test_name, outcome = match.groups()
                tests.append({
                    "file": file_path,
                    "class": test_class,
                    "name": test_name,
                    "outcome": outcome
                })

                if outcome == "PASSED":
                    passed += 1
                elif outcome == "FAILED":
                    failed += 1
                elif outcome == "ERROR":
                    errors += 1
                elif outcome == "SKIPPED":
                    skipped += 1

        # 解析摘要行
        summary_pattern = re.compile(r'(\d+)\s+passed(?:\s+(\d+)\s+failed)?(?:\s+(\d+)\s+skipped)?(?:\s+(\d+)\s+error)?(?:\s+(\d+)\s+xfailed)?(?:\s+(\d+)\s+xpassed)?\s+in\s+([\d.]+s)?')

        for line in lines:
            match = summary_pattern.search(line)
            if match:
                groups = match.groups()
                passed = int(groups[0]) if groups[0] else passed
                failed = int(groups[1]) if groups[1] else failed
                skipped = int(groups[2]) if groups[2] else skipped
                errors = int(groups[3]) if groups[3] else errors

        total = passed + failed + skipped + errors

        return {
            "test_path": test_path,
            "summary": {
                "total": total,
                "passed": passed,
                "failed": failed,
                "skipped": skipped,
                "errors": errors
            },
            "tests": tests,
            "status": "passed" if failed == 0 and errors == 0 else "failed"
        }

    def _generate_summary(self) -> Dict[str, Any]:
        """生成测试汇总"""
        total_tests = 0
        total_passed = 0
        total_failed = 0
        total_skipped = 0
        total_errors = 0

        # 汇总单元测试
        for path, result in self.test_results.get("unit_tests", {}).items():
            if isinstance(result, dict) and "summary" in result:
                summary = result["summary"]
                total_tests += summary["total"]
                total_passed += summary["passed"]
                total_failed += summary["failed"]
                total_skipped += summary["skipped"]
                total_errors += summary["errors"]

        # 汇总集成测试
        for path, result in self.test_results.get("integration_tests", {}).items():
            if isinstance(result, dict) and "summary" in result:
                summary = result["summary"]
                total_tests += summary["total"]
                total_passed += summary["passed"]
                total_failed += summary["failed"]
                total_skipped += summary["skipped"]
                total_errors += summary["errors"]

        return {
            "total_tests": total_tests,
            "total_passed": total_passed,
            "total_failed": total_failed,
            "total_skipped": total_skipped,
            "total_errors": total_errors,
            "pass_rate": (total_passed / total_tests * 100) if total_tests > 0 else 0
        }

    def _generate_validation_reports(self):
        """自动生成validation报告"""
        print("\n📝 生成validation报告...")

        # 确保validation目录存在
        self.validation_dir.mkdir(parents=True, exist_ok=True)

        # 生成unit_test_status.md
        self._generate_unit_test_status()

        # 生成integration_test_status.md
        if self.test_results.get("integration_tests"):
            self._generate_integration_test_status()

        print("✅ Validation报告生成完成")

    def _generate_unit_test_status(self):
        """生成单元测试状态报告"""
        unit_tests = self.test_results.get("unit_tests", {})

        # 收集所有模块的测试结果
        all_modules = []
        for path, result in unit_tests.items():
            if isinstance(result, dict) and "tests" in result:
                for test in result["tests"]:
                    all_modules.append(test)

        if not all_modules:
            print("  ⏭️  跳过unit_test_status.md生成（无测试数据）")
            return

        # 按模块分类
        modules_data = {}
        for test in all_modules:
            # 从文件路径推断模块
            if "test_simulation" in test["file"]:
                module_name = "simulation"
            elif "test_state_machine" in test["file"]:
                module_name = "state_machine"
            elif "test_executor" in test["file"]:
                module_name = "graph_engine"
            elif "test_examples" in test["file"]:
                module_name = "integration"
            else:
                module_name = "other"

            if module_name not in modules_data:
                modules_data[module_name] = {
                    "total": 0,
                    "passed": 0,
                    "failed": 0,
                    "skipped": 0,
                    "tests": []
                }

            modules_data[module_name]["total"] += 1
            if test["outcome"] == "PASSED":
                modules_data[module_name]["passed"] += 1
            elif test["outcome"] == "FAILED":
                modules_data[module_name]["failed"] += 1
            elif test["outcome"] == "SKIPPED":
                modules_data[module_name]["skipped"] += 1

            modules_data[module_name]["tests"].append(test)

        # 生成markdown报告
        summary = self.test_results["summary"]
        content = f"""# Unit Test Status

**Generated**: {self.test_results['timestamp']}
**Status**: {'COMPLETE' if summary['total_failed'] == 0 else 'HAS_FAILURES'}
**Test Coordinator**: UnifiedTestCoordinator

---

## Executive Summary

- **Total Tests**: {summary['total_tests']}
- **Passed**: {summary['total_passed']} ({summary['pass_rate']:.1f}%)
- **Failed**: {summary['total_failed']}
- **Skipped**: {summary['total_skipped']}

---

## Detailed Results by Module

"""

        for module_name, data in modules_data.items():
            pass_rate = (data["passed"] / data["total"] * 100) if data["total"] > 0 else 0
            status_icon = "✅" if data["failed"] == 0 else "❌"

            content += f"""
### {status_icon} {module_name.replace('_', ' ').title()} Module ({data['passed']}/{data['total']} - {pass_rate:.1f}%)

"""

            # 列出前几个测试
            for test in data["tests"][:5]:
                icon = {"PASSED": "✅", "FAILED": "❌", "SKIPPED": "⏭️", "ERROR": "⚠️"}[test["outcome"]]
                content += f"- {icon} `{test['class']}::{test['name']}`\n"

            if len(data["tests"]) > 5:
                content += f"- ... and {len(data['tests']) - 5} more tests\n"

        content += f"""
---

## Test Execution Details

**Test Scope**: {', '.join([f'{{path}}' for path in unit_tests.keys()])}
**Execution Time**: {self.test_results['timestamp']}
**Coordinator Version**: 1.0

---

*This report was automatically generated by UnifiedTestCoordinator*
"""

        output_path = self.validation_dir / "unit_test_status.md"
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(content)

        print(f"  ✅ 生成: {output_path}")

    def _generate_integration_test_status(self):
        """生成集成测试状态报告"""
        integration_tests = self.test_results.get("integration_tests", {})

        # 收集所有测试
        all_tests = []
        for path, result in integration_tests.items():
            if isinstance(result, dict) and "tests" in result:
                all_tests.extend(result["tests"])

        if not all_tests:
            print("  ⏭️  跳过integration_test_status.md生成（无测试数据）")
            return

        summary = self.test_results["summary"]
        content = f"""# Integration Test Status

**Generated**: {self.test_results['timestamp']}
**Status**: {'COMPLETE' if summary['total_failed'] == 0 else 'HAS_FAILURES'}
**Test Coordinator**: UnifiedTestCoordinator

---

## Executive Summary

- **Total Tests**: {len(all_tests)}
- **Passed**: {summary['total_passed']}
- **Failed**: {summary['total_failed']}
- **Success Rate**: {summary['pass_rate']:.1f}%

---

## Test Results

"""

        for test in all_tests:
            icon = {"PASSED": "✅", "FAILED": "❌", "SKIPPED": "⏭️"}[test["outcome"]]
            content += f"{icon} `{test['class']}::{test['name']}` - {test['outcome']}\n"

        content += f"""
---

## Execution Details

**Test Paths**: {', '.join([f'{{path}}' for path in integration_tests.keys()])}
**Execution Time**: {self.test_results['timestamp']}

---

*This report was automatically generated by UnifiedTestCoordinator*
"""

        output_path = self.validation_dir / "integration_test_status.md"
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(content)

        print(f"  ✅ 生成: {output_path}")

    def _display_results(self):
        """显示测试结果"""
        summary = self.test_results["summary"]

        print("\n" + "=" * 70)
        print("📊 测试执行汇总")
        print("=" * 70)
        print(f"📋 总测试数: {summary['total_tests']}")
        print(f"✅ 通过: {summary['total_passed']} ({summary['pass_rate']:.1f}%)")
        print(f"❌ 失败: {summary['total_failed']}")
        print(f"⏭️  跳过: {summary['total_skipped']}")

        if summary['total_failed'] > 0:
            print("\n⚠️  有测试失败，建议检查:")
            print("   1. 查看validation文档了解详情")
            print("   2. 检查失败的测试用例")
            print("   3. 使用module-test技能处理失败")
        else:
            print("\n✅ 所有测试通过！")

    def export_json_report(self, output_path: Optional[Path] = None) -> Path:
        """导出JSON格式的测试报告"""
        if output_path is None:
            output_path = self.project_root / "test_results.json"

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(self.test_results, f, indent=2, ensure_ascii=False)

        print(f"📄 JSON报告已导出: {output_path}")
        return output_path

    def update_dashboard_data(self):
        """更新dashboard数据"""
        dashboard_data = {
            "timestamp": self.test_results["timestamp"],
            "test_results": self.test_results,
            "last_updated": datetime.now().isoformat()
        }

        dashboard_path = self.project_root / "docs" / "validation" / "dashboard_data.json"
        with open(dashboard_path, 'w', encoding='utf-8') as f:
            json.dump(dashboard_data, f, indent=2)

        print(f"📊 Dashboard数据已更新: {dashboard_path}")


def main():
    """主函数"""
    parser = argparse.ArgumentParser(
        description="Uni-Claw统一测试协调器",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )

    parser.add_argument(
        "scope",
        nargs="?",
        default="all",
        choices=["all", "unit", "integration", "v6", "ci"],
        help="测试范围"
    )

    parser.add_argument(
        "--export-json",
        action="store_true",
        help="导出JSON格式测试报告"
    )

    parser.add_argument(
        "--update-dashboard",
        action="store_true",
        help="更新dashboard数据"
    )

    args = parser.parse_args()

    coordinator = UnifiedTestCoordinator()

    # 运行测试
    coordinator.run_all_tests(args.scope)

    # 可选：导出JSON报告
    if args.export_json:
        coordinator.export_json_report()

    # 可选：更新dashboard
    if args.update_dashboard:
        coordinator.update_dashboard_data()


if __name__ == "__main__":
    main()