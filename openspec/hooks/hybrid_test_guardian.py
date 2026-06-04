"""
混合测试守护者：规则检测 + AI智能分析

第一层：快速规则检测（处理明确问题）
第二层：AI智能分析（处理复杂判断）
"""

import subprocess
import sys
from pathlib import Path
from typing import Dict, Any, List, Optional
from dataclasses import dataclass
import json
import re


@dataclass
class TestIssue:
    """测试问题"""
    severity: str
    category: str
    message: str
    confidence: float  # 0-1的置信度
    file: Optional[str] = None
    suggestion: Optional[str] = None
    requires_ai_review: bool = False  # 是否需要AI复审


class RuleBasedDetector:
    """基于规则的快速检测器"""

    def __init__(self):
        self.rules = {
            'test_failure': self._check_test_failures,
            'coverage_drop': self._check_coverage_regression,
            'test_count_decrease': self._check_test_deletion,
        }

    def detect(self, baseline: Dict, current: Dict, changes: Dict) -> List[TestIssue]:
        """执行快速规则检测"""
        issues = []

        # 执行所有规则
        for rule_name, rule_func in self.rules.items():
            try:
                rule_issues = rule_func(baseline, current, changes)
                issues.extend(rule_issues)
            except Exception as e:
                print(f"⚠️  规则 {rule_name} 执行失败: {e}")

        return issues

    def _check_test_failures(self, baseline: Dict, current: Dict, changes: Dict) -> List[TestIssue]:
        """检查测试失败增加（明确问题）"""
        issues = []
        failed_increase = current.get('failed', 0) - baseline.get('failed', 0)

        if failed_increase > 0:
            issues.append(TestIssue(
                severity="error",
                category="test_failure",
                message=f"新增 {failed_increase} 个失败测试",
                confidence=1.0,  # 规则检测置信度高
                suggestion="请修复失败的测试后再继续",
                requires_ai_review=False  # 明确问题，无需AI复审
            ))

        return issues

    def _check_coverage_regression(self, baseline: Dict, current: Dict, changes: Dict) -> List[TestIssue]:
        """检查覆盖率下降（明确问题）"""
        issues = []
        coverage_drop = baseline.get('coverage', 0) - current.get('coverage', 0)

        if coverage_drop > 5.0:
            issues.append(TestIssue(
                severity="error",
                category="coverage_regression",
                message=f"覆盖率下降 {coverage_drop:.1f}%",
                confidence=0.9,
                suggestion="确保测试覆盖率不会显著下降",
                requires_ai_review=False
            ))

        return issues

    def _check_test_deletion(self, baseline: Dict, current: Dict, changes: Dict) -> List[TestIssue]:
        """检查测试删除（需要AI判断是否合理）"""
        issues = []
        test_count_diff = current.get('total_tests', 0) - baseline.get('total_tests', 0)

        if test_count_diff < 0:
            # 规则检测到测试减少，但需要AI判断是否合理
            issues.append(TestIssue(
                severity="warning",
                category="test_deletion",
                message=f"测试总数减少 {abs(test_count_diff)} 个",
                confidence=0.7,
                suggestion="需要AI复审：测试减少是否合理",
                requires_ai_review=True  # 需要AI智能判断
            ))

        return issues


class AIAnalysisDetector:
    """AI智能分析检测器"""

    def __init__(self):
        self.available_skills = [
            'superpowers:systematic-debugging',
            'code-review',
            'simplify'
        ]

    def is_available(self) -> bool:
        """检查AI分析能力是否可用"""
        # 检查是否在Claude Code环境中
        return 'claude' in sys.modules or Path('.claude').exists()

    def analyze_suspicious_changes(self, changes: Dict, context: Dict) -> List[TestIssue]:
        """AI分析可疑的测试修改"""
        issues = []

        if not self.is_available():
            print("⚠️  AI分析不可用，跳过智能检测")
            return issues

        # 提取测试文件修改
        test_files = self._extract_test_files(changes.get('modified_files', []))

        if not test_files:
            return issues

        print(f"🤖 AI正在分析 {len(test_files)} 个测试文件的修改...")

        # 对每个可疑的测试修改进行AI分析
        for test_file in test_files:
            file_issues = self._analyze_test_file_modification(test_file, context)
            issues.extend(file_issues)

        return issues

    def _extract_test_files(self, files: List[str]) -> List[str]:
        """提取测试文件"""
        return [f for f in files if self._is_test_file(f)]

    def _is_test_file(self, file_path: str) -> bool:
        """判断是否为测试文件"""
        return 'test' in file_path and file_path.endswith('.py')

    def _analyze_test_file_modification(self, test_file: str, context: Dict) -> List[TestIssue]:
        """AI分析单个测试文件的修改"""
        issues = []

        try:
            # 获取文件内容
            file_path = Path(test_file)
            if not file_path.exists():
                return issues

            content = file_path.read_text(encoding='utf-8')

            # 使用git diff获取修改内容
            diff_result = self._get_file_diff(test_file)
            if not diff_result:
                return issues

            # AI分析调用（这里需要设计合适的prompt）
            ai_analysis = self._call_ai_analysis(test_file, diff_result, content, context)

            if ai_analysis:
                issues.extend(ai_analysis)

        except Exception as e:
            print(f"⚠️  AI分析 {test_file} 失败: {e}")

        return issues

    def _get_file_diff(self, file_path: str) -> Optional[str]:
        """获取文件的git diff"""
        try:
            result = subprocess.run(
                ['git', 'diff', 'HEAD', file_path],
                capture_output=True,
                text=True,
                timeout=10
            )
            return result.stdout if result.stdout else None
        except Exception:
            return None

    def _call_ai_analysis(self, test_file: str, diff: str, content: str, context: Dict) -> List[TestIssue]:
        """调用AI进行分析（需要设计合适的prompt）"""

        analysis_prompt = f"""
        分析以下测试文件的修改是否合理：

        文件: {test_file}
        修改内容:
        {diff}

        请检查：
        1. 是否只修改了断言值而没有修复逻辑？
        2. 是否删除了重要的测试断言？
        3. 是否让异常处理过于宽泛？
        4. 修改是否真的修复了问题还是"应付"测试？

        返回JSON格式：
        {{
            "has_issue": true/false,
            "issues": [
                {{
                    "severity": "error/warning",
                    "category": "data_tampering/assertion_weakening/test_quality",
                    "message": "具体问题描述",
                    "confidence": 0.8,
                    "suggestion": "改进建议"
                }}
            ]
        }}
        """

        # 这里需要实际的AI调用
        # 在Claude Code环境中，这可能通过Agent工具或其他方式实现
        print(f"🤖 AI分析提示已生成，等待实现...")

        return []


class HybridTestGuardian:
    """混合测试守护者"""

    def __init__(self):
        self.rule_detector = RuleBasedDetector()
        self.ai_detector = AIAnalysisDetector()
        self.baseline = None
        self.context = {}

    def pre_check(self, task_info: Dict) -> Dict:
        """前置检查（规则检测）"""
        print("🛡️  TestGuardian: 前置检查（快速规则检测）")

        # 记录上下文
        self.context = {
            'task_info': task_info,
            'timestamp': self._get_timestamp()
        }

        # 捕获测试基线
        self.baseline = self._capture_test_baseline()

        return self.baseline

    def post_check(self, changes: Dict) -> Dict:
        """后置检查（规则 + AI检测）"""
        print("🛡️  TestGuardian: 后置检查（混合检测）")

        # 捕获当前状态
        current = self._capture_test_baseline()

        # 第一层：快速规则检测
        print("📋 第一层：规则检测")
        rule_issues = self.rule_detector.detect(self.baseline, current, changes)

        # 第二层：AI智能分析（仅对需要复审的问题）
        ai_issues = []
        suspicious_items = [issue for issue in rule_issues if issue.requires_ai_review]

        if suspicious_items:
            print("🤖 第二层：AI智能分析")
            ai_issues = self.ai_detector.analyze_suspicious_changes(changes, self.context)

        # 合并结果
        all_issues = rule_issues + ai_issues

        # 评估结果
        result = self._evaluate_results(all_issues, current)

        return {
            'status': result['status'],
            'acceptable': result['acceptable'],
            'issues': [self._issue_to_dict(issue) for issue in all_issues],
            'baseline': self.baseline,
            'current': current,
            'detection_summary': {
                'rule_issues': len(rule_issues),
                'ai_issues': len(ai_issues),
                'total_issues': len(all_issues)
            }
        }

    def _capture_test_baseline(self) -> Dict:
        """捕获测试基线"""
        # 简化实现
        return {
            'total_tests': 42,
            'passed': 40,
            'failed': 2,
            'errors': 0,
            'skipped': 0,
            'coverage': 85.0
        }

    def _get_timestamp(self) -> str:
        """获取时间戳"""
        from datetime import datetime
        return datetime.now().isoformat()

    def _evaluate_results(self, issues: List[TestIssue], current: Dict) -> Dict:
        """评估检测结果"""
        critical_issues = [i for i in issues if i.severity == 'error']
        warning_issues = [i for i in issues if i.severity == 'warning']

        if critical_issues:
            return {
                'status': 'failed',
                'acceptable': False,
                'message': f'发现 {len(critical_issues)} 个严重问题'
            }
        elif warning_issues:
            return {
                'status': 'warning',
                'acceptable': True,
                'message': f'发现 {len(warning_issues)} 个警告'
            }
        else:
            return {
                'status': 'passed',
                'acceptable': True,
                'message': '测试质量检查通过'
            }

    def _issue_to_dict(self, issue: TestIssue) -> Dict:
        """转换问题为字典"""
        return {
            'severity': issue.severity,
            'category': issue.category,
            'message': issue.message,
            'confidence': issue.confidence,
            'file': issue.file,
            'suggestion': issue.suggestion,
            'requires_ai_review': issue.requires_ai_review
        }


# ============================================================================
# 使用示例
# ============================================================================

def example_usage():
    """使用示例"""
    print("🔬 混合测试守护者示例\n")

    guardian = HybridTestGuardian()

    # 前置检查
    print("=== 前置检查 ===")
    task_info = {'name': 'test_change', 'description': '测试变更'}
    baseline = guardian.pre_check(task_info)
    print(f"基线: {baseline}\n")

    # 后置检查
    print("=== 后置检查 ===")
    changes = {
        'modified_files': ['src/graph/test/test_graph_models.py']
    }
    result = guardian.post_check(changes)

    print(f"\n检测结果:")
    print(f"状态: {result['status']}")
    print(f"可接受: {result['acceptable']}")
    print(f"检测摘要: {result['detection_summary']}")

    if result['issues']:
        print(f"\n发现的问题:")
        for i, issue in enumerate(result['issues'], 1):
            print(f"  [{i}] {issue['severity']}: {issue['message']}")
            if issue.get('suggestion'):
                print(f"      💡 {issue['suggestion']}")


if __name__ == "__main__":
    example_usage()