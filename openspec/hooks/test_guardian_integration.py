"""
OpenSpec测试守护者集成

在OpenSpec工作流中集成测试质量检查，防止AI应付测试行为。
"""

import json
import subprocess
import sys
from pathlib import Path
from typing import Any, Dict, List, Optional
from dataclasses import dataclass
from datetime import datetime


@dataclass
class TestBaseline:
    """测试基线数据"""
    timestamp: str
    module: str
    total_tests: int
    passed: int
    failed: int
    errors: int
    skipped: int
    coverage: float
    test_files: Dict[str, str]  # 文件路径 -> 内容哈希


@dataclass
class TestIssue:
    """测试问题"""
    severity: str  # "warning", "error", "critical"
    category: str  # "data_tampering", "assertion_weakening", "test_failure", etc.
    message: str
    file: Optional[str] = None
    suggestion: Optional[str] = None


class TestGuardian:
    """测试守护者主类"""

    def __init__(self, project_root: Path):
        self.project_root = project_root
        self.baseline: Optional[TestBaseline] = None
        self.issues: List[TestIssue] = []

    def get_module_from_task(self, task_info: Dict[str, Any]) -> str:
        """从任务信息中推断相关模块"""
        # 从任务描述或文件路径中提取模块名
        task_files = task_info.get('files', [])
        if task_files:
            # 分析文件路径确定模块
            for file_path in task_files:
                if 'src/' in file_path:
                    parts = Path(file_path).parts
                    if len(parts) > 1 and 'src' in parts:
                        src_index = parts.index('src')
                        if src_index + 1 < len(parts):
                            return parts[src_index + 1]

        # 默认回退到graph模块（试点）
        return 'graph'

    def capture_baseline(self, module: str) -> TestBaseline:
        """捕获测试基线"""
        print(f"📊 正在捕获 {module} 模块的测试基线...")

        # 运行模块测试
        test_script = self.project_root / f'src/{module}/run_tests.py'
        if not test_script.exists():
            print(f"⚠️  警告: 未找到测试脚本 {test_script}")
            return self._create_empty_baseline(module)

        try:
            result = subprocess.run(
                [sys.executable, str(test_script)],
                capture_output=True,
                text=True,
                timeout=120,
                cwd=self.project_root
            )

            # 解析测试报告
            report_file = self.project_root / f'src/{module}/test/test_report.json'
            if report_file.exists():
                with open(report_file, 'r', encoding='utf-8') as f:
                    report_data = json.load(f)
                return self._parse_report_to_baseline(module, report_data)

        except Exception as e:
            print(f"❌ 捕获基线失败: {e}")

        return self._create_empty_baseline(module)

    def _create_empty_baseline(self, module: str) -> TestBaseline:
        """创建空基线"""
        return TestBaseline(
            timestamp=datetime.now().isoformat(),
            module=module,
            total_tests=0,
            passed=0,
            failed=0,
            errors=0,
            skipped=0,
            coverage=0.0,
            test_files={}
        )

    def _parse_report_to_baseline(self, module: str, report_data: Dict) -> TestBaseline:
        """从测试报告解析基线数据"""
        summary = report_data.get('summary', {})
        coverage_data = report_data.get('coverage', {})

        return TestBaseline(
            timestamp=report_data.get('timestamp', datetime.now().isoformat()),
            module=module,
            total_tests=summary.get('total', 0),
            passed=summary.get('passed', 0),
            failed=summary.get('failed', 0),
            errors=summary.get('errors', 0),
            skipped=summary.get('skipped', 0),
            coverage=coverage_data.get('percent_covered', 0.0),
            test_files={}  # 可以扩展为记录测试文件哈希
        )

    def detect_changes(self, current: TestBaseline) -> List[TestIssue]:
        """检测测试变化"""
        if not self.baseline:
            return []

        issues = []

        # 检测测试失败增加
        if current.failed > self.baseline.failed:
            issues.append(TestIssue(
                severity="error",
                category="test_failure",
                message=f"新增失败测试: {current.failed - self.baseline.failed} 个",
                suggestion="请修复失败的测试后再继续"
            ))

        # 检测覆盖率下降
        coverage_drop = self.baseline.coverage - current.coverage
        if coverage_drop > 5.0:
            issues.append(TestIssue(
                severity="error",
                category="coverage_regression",
                message=f"覆盖率显著下降: {self.baseline.coverage:.1f}% -> {current.coverage:.1f}%",
                suggestion="请确保测试覆盖率不会显著下降"
            ))

        # 检测测试总数减少（可能删除测试）
        if current.total_tests < self.baseline.total_tests:
            issues.append(TestIssue(
                severity="warning",
                category="test_deletion",
                message=f"测试总数减少: {self.baseline.total_tests} -> {current.total_tests}",
                suggestion="请确认是否删除了必要的测试用例"
            ))

        return issues

    def check_test_file_modifications(self, modified_files: List[str]) -> List[TestIssue]:
        """检查测试文件修改是否合理"""
        issues = []

        test_files = [f for f in modified_files if self._is_test_file(f)]

        for test_file in test_files:
            # 这里可以添加更复杂的检测逻辑
            # 例如：检查是否只修改了断言值而没有修改逻辑
            file_path = self.project_root / test_file
            if file_path.exists():
                content = file_path.read_text(encoding='utf-8')

                # 检测可疑的修改模式
                if self._detect_suspicious_patterns(content):
                    issues.append(TestIssue(
                        severity="error",
                        category="data_tampering",
                        message=f"检测到测试文件 {test_file} 有可疑修改",
                        file=test_file,
                        suggestion="请确保测试逻辑的完整性，不要只修改断言值"
                    ))

        return issues

    def _is_test_file(self, file_path: str) -> bool:
        """判断是否为测试文件"""
        return 'test' in file_path and file_path.endswith('.py')

    def _detect_suspicious_patterns(self, content: str) -> bool:
        """检测可疑的测试修改模式"""
        # 这里可以实现更复杂的模式检测
        # 例如：检查是否只修改了断言值
        suspicious_patterns = [
            'assert == ',  # 简单的相等断言
            'assertTrue',   # 过于宽泛的断言
        ]

        # 简化版本：实际应该比较修改前后的差异
        return any(pattern in content for pattern in suspicious_patterns)

    def evaluate_results(self, current: TestBaseline) -> bool:
        """评估测试结果是否可接受"""
        # 严重问题：必须阻断
        critical_issues = [i for i in self.issues if i.severity == "critical"]
        if critical_issues:
            print(f"❌ 发现 {len(critical_issues)} 个严重问题，必须修复")
            return False

        # 错误问题：应该阻断
        error_issues = [i for i in self.issues if i.severity == "error"]
        if error_issues:
            print(f"❌ 发现 {len(error_issues)} 个错误，建议修复")
            return False

        # 警告问题：可以继续但需要记录
        warning_issues = [i for i in self.issues if i.severity == "warning"]
        if warning_issues:
            print(f"⚠️  发现 {len(warning_issues)} 个警告，请关注")
            return True  # 警告不阻断

        return True


# ============================================================================
# OpenSpec钩子函数
# ============================================================================

# 全局守护者实例
_guardian_instance: Optional[TestGuardian] = None


def pre_task_hook(task_info: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行前钩子"""
    print("🛡️  TestGuardian: 任务前置检查")

    global _guardian_instance
    project_root = Path.cwd()

    # 创建守护者实例
    _guardian_instance = TestGuardian(project_root)

    # 推断相关模块
    module = _guardian_instance.get_module_from_task(task_info)
    print(f"📦 检测到相关模块: {module}")

    # 捕获测试基线
    baseline = _guardian_instance.capture_baseline(module)
    _guardian_instance.baseline = baseline

    print(f"✅ 基线已捕获: {baseline.total_tests} 个测试，{baseline.passed} 通过，{baseline.failed} 失败")

    return {
        'baseline': {
            'module': module,
            'total_tests': baseline.total_tests,
            'passed': baseline.passed,
            'failed': baseline.failed,
            'coverage': baseline.coverage
        }
    }


def post_task_hook(task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行后钩子"""
    print("🛡️  TestGuardian: 任务后置验证")

    if not _guardian_instance:
        print("⚠️  警告: 守护者实例未找到，跳过验证")
        return {'status': 'skipped'}

    module = _guardian_instance.baseline.module if _guardian_instance.baseline else 'graph'

    # 捕获当前测试状态
    current = _guardian_instance.capture_baseline(module)
    print(f"📊 当前状态: {current.total_tests} 个测试，{current.passed} 通过，{current.failed} 失败")

    # 检测变化
    issues = _guardian_instance.detect_changes(current)

    # 检查文件修改
    modified_files = changes.get('modified_files', [])
    file_issues = _guardian_instance.check_test_file_modifications(modified_files)
    issues.extend(file_issues)

    # 合并问题
    _guardian_instance.issues = issues

    # 评估结果
    if issues:
        print(f"\n🚨 发现 {len(issues)} 个测试问题:")
        for i, issue in enumerate(issues, 1):
            icon = {"error": "❌", "warning": "⚠️", "critical": "🔴"}.get(issue.severity, "⚡")
            print(f"  {icon} [{i}] {issue.message}")
            if issue.suggestion:
                print(f"     💡 建议: {issue.suggestion}")

    # 评估是否可以接受
    acceptable = _guardian_instance.evaluate_results(current)

    if not acceptable:
        print(f"\n🛑 测试质量检查未通过，建议修复问题后再继续")
    else:
        print(f"\n✅ 测试质量检查通过")

    return {
        'status': 'passed' if acceptable else 'failed',
        'current_state': {
            'module': module,
            'total_tests': current.total_tests,
            'passed': current.passed,
            'failed': current.failed,
            'coverage': current.coverage
        },
        'issues': [
            {
                'severity': i.severity,
                'category': i.category,
                'message': i.message,
                'suggestion': i.suggestion
            }
            for i in issues
        ]
    }


# ============================================================================
# 直接调用接口（用于测试和调试）
# ============================================================================

def main():
    """主函数，用于测试"""
    print("🧪 TestGuardian 集成测试")

    project_root = Path.cwd()
    guardian = TestGuardian(project_root)

    # 模拟任务信息
    task_info = {
        'name': 'test_task',
        'description': '测试任务',
        'files': ['src/graph/test/test_graph_models.py']
    }

    # 前置钩子
    print("\n=== 前置钩子 ===")
    pre_result = pre_task_hook(task_info)
    print(f"前置结果: {pre_result}")

    # 后置钩子
    print("\n=== 后置钩子 ===")
    changes = {'modified_files': []}
    post_result = post_task_hook(task_info, changes)
    print(f"后置结果: {post_result}")

    print("\n✅ 测试完成")


if __name__ == "__main__":
    main()