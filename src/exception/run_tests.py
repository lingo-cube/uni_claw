#!/usr/bin/env python3
"""Exception 模块统一单元测试脚本"""

import argparse
import json
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional


class TestReport:
    def __init__(self):
        self.timestamp = datetime.now().isoformat()
        self.module = "exception"
        self.python_version = f"{sys.version_info.major}.{sys.version_info.minor}"
        self.summary = {"total": 0, "passed": 0, "failed": 0, "skipped": 0, "errors": 0, "duration": 0.0}
        self.tests: List[Dict[str, Any]] = []
        self.failures: List[Dict[str, Any]] = []
        self.errors: List[Dict[str, Any]] = []
        self.coverage: Optional[Dict[str, Any]] = None
        self.status = "unknown"

    def to_dict(self) -> Dict[str, Any]:
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
            "metadata": {"framework": "pytest", "report_version": "1.0"},
        }


def run_pytest(test_path: Path, coverage: bool = False) -> subprocess.CompletedProcess:
    cmd = [sys.executable, "-m", "pytest", str(test_path), "-v", "--tb=short"]
    if coverage:
        cmd.extend(["--cov=src.exception", "--cov-report=term-missing"])
    result = subprocess.run(cmd, cwd=Path(__file__).parent.parent.parent, capture_output=True, text=True)
    return result


def parse_output(output: str) -> Dict[str, Any]:
    data = {"summary": {"total": 0, "passed": 0, "failed": 0, "skipped": 0, "errors": 0, "duration": 0.0}, "tests": [], "failures": [], "errors": []}
    lines = output.split('\n')
    for line in lines:
        test_match = re.match(r'(.+\.py)::(.+)::(.+)\s+(PASSED|FAILED|ERROR|SKIPPED)', line)
        if test_match:
            file_path, test_class, test_name, outcome = test_match.groups()
            data["summary"]["total"] += 1
            data["tests"].append({"file": file_path, "class": test_class, "name": test_name, "outcome": outcome})
            if outcome == "PASSED": data["summary"]["passed"] += 1
            elif outcome == "FAILED": data["summary"]["failed"] += 1
            elif outcome == "ERROR": data["summary"]["errors"] += 1
            elif outcome == "SKIPPED": data["summary"]["skipped"] += 1
    return data


def main():
    parser = argparse.ArgumentParser(description="Exception 模块单元测试")
    parser.add_argument("-c", "--coverage", action="store_true", help="生成覆盖率报告")
    args = parser.parse_args()
    
    test_path = Path(__file__).parent / "test"
    print("🧪 Exception 模块测试")
    result = run_pytest(test_path, args.coverage)
    print(result.stdout)
    
    data = parse_output(result.stdout)
    report = TestReport()
    report.summary = data["summary"]
    report.tests = data["tests"]
    report.status = "passed" if report.summary['failed'] == 0 and report.summary['errors'] == 0 else "failed"
    
    print(f"\n✓ 通过: {report.summary['passed']}")
    print(f"✗ 失败: {report.summary['failed']}")
    print(f"⊘ 跳过: {report.summary['skipped']}")
    
    sys.exit(2 if report.summary['errors'] > 0 else (1 if report.summary['failed'] > 0 else 0))


if __name__ == "__main__":
    main()
