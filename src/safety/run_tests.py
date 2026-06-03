#!/usr/bin/env python3
"""
Safety 模块统一单元测试脚本

运行 safety 模块的所有单元测试并生成 AI 可识别的测试报告。

用法:
    python run_tests.py                    # 运行所有测试
    python run_tests.py -v                # 详细输出
    python run_tests.py --coverage         # 生成覆盖率报告

输出:
    测试报告: src/safety/test_report.json
    HTML 报告: src/safety/test_report.html
"""

import argparse
import json
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional


class TestReport:
    """测试报告数据类。"""

    def __init__(self):
        self.timestamp = datetime.now().isoformat()
        self.module = "safety"
        self.python_version = f"{sys.version_info.major}.{sys.version_info.minor}"
        self.summary = {
            "total": 0,
            "passed": 0,
            "failed": 0,
            "skipped": 0,
            "errors": 0,
            "duration": 0.0,
        }
        self.tests: List[Dict[str, Any]] = []
        self.failures: List[Dict[str, Any]] = []
        self.errors: List[Dict[str, Any]] = []
        self.coverage: Optional[Dict[str, Any]] = None
        self.status = "unknown"

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典格式（AI 可识别）。"""
        return {
            "timestamp": self.timestamp,
            "module": self.module,
            "python_version": self.python_version,
            "status": self.status,
            "summary": self.summary,
            "tests": self.tests,
            "failures": self.failures,
            "errors": self.errors,
            "coverage": self.coverage,
            "metadata": {
                "framework": "pytest",
                "report_version": "1.0",
            },
        }


def run_pytest_verbose(test_path: Path, coverage: bool = False) -> subprocess.CompletedProcess:
    """运行 pytest 并解析详细输出。"""
    cmd = [sys.executable, "-m", "pytest", str(test_path), "-v", "--tb=short"]

    if coverage:
        cmd.extend([
            "--cov=src.safety",
            "--cov-report=term-missing",
        ])

    print(f"运行命令: {' '.join(cmd)}")
    result = subprocess.run(
        cmd,
        cwd=Path(__file__).parent.parent.parent,
        capture_output=True,
        text=True,
    )
    return result


def parse_pytest_verbose_output(output: str) -> Dict[str, Any]:
    """解析 pytest -v 输出。"""
    data = {
        "summary": {
            "total": 0,
            "passed": 0,
            "failed": 0,
            "skipped": 0,
            "errors": 0,
            "duration": 0.0,
        },
        "tests": [],
        "failures": [],
        "errors": [],
    }

    lines = output.split('\n')
    in_failure_block = False
    in_error_block = False
    current_failure = []
    current_error = []

    for line in lines:
        # 解析测试行
        test_match = re.match(r'(.+\.py)::(.+)::(.+)\s+(PASSED|FAILED|ERROR|SKIPPED)', line)
        if test_match and not in_failure_block and not in_error_block:
            file_path, test_class, test_name, outcome = test_match.groups()
            data["summary"]["total"] += 1
            test_entry = {
                "file": file_path,
                "class": test_class,
                "name": test_name,
                "outcome": outcome,
            }
            data["tests"].append(test_entry)

            if outcome == "PASSED":
                data["summary"]["passed"] += 1
            elif outcome == "FAILED":
                data["summary"]["failed"] += 1
            elif outcome == "ERROR":
                data["summary"]["errors"] += 1
            elif outcome == "SKIPPED":
                data["summary"]["skipped"] += 1
            continue

        # 检测失败块开始
        if "FAILURES" in line:
            in_failure_block = True
            continue

        # 检测错误块开始
        if "ERRORS" in line:
            in_error_block = True
            continue

        # 解析摘要行
        if "=" * 70 in line:
            in_failure_block = False
            in_error_block = False
            continue

        # 解析摘要
        if "passed" in line and "failed" in line:
            match = re.search(r'(\d+) passed(?:, (\d+) failed)?(?:, (\d+) skipped)?(?:, (\d+) errors)? in ([\d.]+s)?', line)
            if match:
                groups = match.groups()
                data["summary"]["passed"] = int(groups[0]) if groups[0] else 0
                data["summary"]["failed"] = int(groups[1]) if groups[1] else 0
                data["summary"]["skipped"] = int(groups[2]) if groups[2] else 0
                data["summary"]["errors"] = int(groups[3]) if groups[3] else 0
                data["summary"]["duration"] = float(groups[4].rstrip('s')) if groups[4] else 0.0
            continue

        # 收集失败详情
        if in_failure_block and line.strip():
            if line.startswith("_") or line.startswith("---") or line.startswith("= "):
                continue
            if "::" in line and "FAILED" not in line:
                if current_failure:
                    data["failures"].append("\n".join(current_failure))
                current_failure = [line]
            else:
                current_failure.append(line)

        # 收集错误详情
        if in_error_block and line.strip():
            if line.startswith("_") or line.startswith("---") or line.startswith("= "):
                continue
            if "::" in line and "ERROR" not in line:
                if current_error:
                    data["errors"].append("\n".join(current_error))
                current_error = [line]
            else:
                current_error.append(line)

    # 添加最后一个失败/错误
    if current_failure:
        data["failures"].append("\n".join(current_failure))
    if current_error:
        data["errors"].append("\n".join(current_error))

    return data


def print_report_summary(report: TestReport):
    """打印测试报告摘要。"""
    print("\n" + "=" * 70)
    print(f"Safety 模块单元测试报告 - {report.timestamp}")
    print("=" * 70)

    summary = report.summary
    print(f"\n📊 测试结果:")
    print(f"  总计: {summary['total']}")
    print(f"  ✓ 通过: {summary['passed']}")
    print(f"  ✗ 失败: {summary['failed']}")
    print(f"  ⊘ 跳过: {summary['skipped']}")
    print(f"  ⚠ 错误: {summary['errors']}")
    print(f"  ⏱ 耗时: {summary['duration']:.2f} 秒")

    if report.tests:
        print(f"\n📋 测试列表:")
        for test in report.tests:
            status_icon = {"PASSED": "✓", "FAILED": "✗", "ERROR": "⚠", "SKIPPED": "⊘"}.get(test['outcome'], "?")
            print(f"  {status_icon} {test['class']}::{test['name']}")

    if report.failures:
        print(f"\n❌ 失败详情:")
        for failure in report.failures:
            lines = failure.split('\n')[:5]
            print("  " + "\n      ".join(lines))

    # 状态
    status_icon = {"passed": "✓", "failed": "✗", "error": "⚠"}.get(
        "passed" if report.summary['failed'] == 0 and report.summary['errors'] == 0 else
        "failed" if report.summary['failed'] > 0 else "error",
        "?"
    )
    print(f"\n{status_icon} 状态: {report.status.upper()}")

    print("\n" + "=" * 70)


def generate_html_report(report: TestReport, output_path: Path) -> Path:
    """生成 HTML 测试报告。"""
    html_content = f"""<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Safety 模块测试报告</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        h1 {{ color: #333; border-bottom: 2px solid #007acc; padding-bottom: 10px; }}
        .summary {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin: 20px 0; }}
        .metric {{ background: #f8f9fa; padding: 15px; border-radius: 6px; border-left: 4px solid #007acc; }}
        .metric-label {{ font-size: 12px; color: #666; text-transform: uppercase; }}
        .metric-value {{ font-size: 24px; font-weight: bold; color: #333; }}
        .metric.passed {{ border-left-color: #28a745; }}
        .metric.failed {{ border-left-color: #dc3545; }}
        .metric.errors {{ border-left-color: #ffc107; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th {{ background: #007acc; color: white; padding: 12px; text-align: left; }}
        td {{ padding: 10px; border-bottom: 1px solid #ddd; }}
        tr:hover {{ background: #f8f9fa; }}
        .status-passed {{ color: #28a745; font-weight: bold; }}
        .status-failed {{ color: #dc3545; font-weight: bold; }}
        .status-error {{ color: #ffc107; font-weight: bold; }}
    </style>
</head>
<body>
    <div class="container">
        <h1>📊 Safety 模块测试报告</h1>
        <p><strong>时间:</strong> {report.timestamp}</p>
        <p><strong>Python 版本:</strong> {report.python_version}</p>

        <div class="summary">
            <div class="metric">
                <div class="metric-label">总计</div>
                <div class="metric-value">{report.summary['total']}</div>
            </div>
            <div class="metric passed">
                <div class="metric-label">通过</div>
                <div class="metric-value">{report.summary['passed']}</div>
            </div>
            <div class="metric failed">
                <div class="metric-label">失败</div>
                <div class="metric-value">{report.summary['failed']}</div>
            </div>
            <div class="metric errors">
                <div class="metric-label">错误</div>
                <div class="metric-value">{report.summary['errors']}</div>
            </div>
            <div class="metric">
                <div class="metric-label">耗时</div>
                <div class="metric-value">{report.summary['duration']:.2f}s</div>
            </div>
        </div>

        <h2>测试列表</h2>
        <table>
            <thead>
                <tr>
                    <th>状态</th>
                    <th>测试类</th>
                    <th>测试名称</th>
                    <th>文件</th>
                </tr>
            </thead>
            <tbody>
"""

    for test in report.tests:
        status_class = f"status-{test['outcome'].lower()}"
        html_content += f"""
                <tr>
                    <td class="{status_class}">{test['outcome']}</td>
                    <td>{test['class']}</td>
                    <td>{test['name']}</td>
                    <td>{test['file']}</td>
                </tr>
"""

    html_content += """
            </tbody>
        </table>
    </div>
</body>
</html>
"""

    html_path = output_path.with_suffix('.html')
    with open(html_path, 'w', encoding='utf-8') as f:
        f.write(html_content)

    return html_path


def save_report(report: TestReport, output_path: Path):
    """保存 JSON 测试报告。"""
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(report.to_dict(), f, indent=2, ensure_ascii=False)
    print(f"\n📄 JSON 报告已保存: {output_path}")


def get_exit_code(report: TestReport) -> int:
    """根据测试结果获取退出码。"""
    if report.summary['errors'] > 0:
        return 2  # 有错误
    if report.summary['failed'] > 0:
        return 1  # 有失败
    return 0  # 全部通过


def main():
    """主函数。"""
    parser = argparse.ArgumentParser(
        description="Safety 模块单元测试脚本",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("-v", "--verbose", action="store_true", help="详细输出")
    parser.add_argument("-c", "--coverage", action="store_true", help="生成覆盖率报告")
    parser.add_argument("-o", "--output", type=Path, default=None, help="报告输出路径")
    args = parser.parse_args()

    # 设置路径
    test_path = Path(__file__).parent
    output_path = args.output or (test_path / "test_report.json")

    print("=" * 70)
    print("🧪 运行 Safety 模块单元测试")
    print("=" * 70)

    # 运行测试
    result = run_pytest_verbose(test_path, coverage=args.coverage)

    # 解析输出
    pytest_data = parse_pytest_verbose_output(result.stdout)

    # 创建报告
    report = TestReport()
    report.summary = pytest_data["summary"]
    report.tests = pytest_data["tests"]
    report.failures = [{"detail": f} for f in pytest_data["failures"]]
    report.errors = [{"detail": e} for e in pytest_data["errors"]]

    # 确定状态
    if report.summary['errors'] > 0:
        report.status = "error"
    elif report.summary['failed'] > 0:
        report.status = "failed"
    else:
        report.status = "passed"

    # 打印摘要
    print_report_summary(report)

    # 保存报告
    save_report(report, output_path)
    html_path = generate_html_report(report, output_path)
    print(f"📄 HTML 报告已保存: {html_path}")

    # 返回退出码
    exit_code = get_exit_code(report)
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
