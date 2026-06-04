"""
AI技能集成测试守护者

展示如何将现有的superpowers技能集成到测试守护者中
"""

import subprocess
import sys
from pathlib import Path
from typing import Dict, Any, List, Optional
import json
import re


class AISkillIntegrator:
    """AI技能集成器"""

    def __init__(self):
        # 可用的superpowers技能
        self.available_skills = {
            'systematic-debugging': self._use_systematic_debugging,
            'code-review': self._use_code_review,
            'simplify': self._use_simplify,
        }

    def analyze_test_modification(self, test_file: str, diff_content: str, context: Dict) -> Dict[str, Any]:
        """分析测试文件修改的主入口"""

        print(f"🤖 AI正在分析测试修改: {test_file}")

        # 策略1: 使用systematic-debugging技能分析测试失败原因
        if self._contains_test_failure(diff_content):
            print("🔍 检测到测试失败，使用systematic-debugging分析...")
            return self._use_systematic_debugging(test_file, diff_content, context)

        # 策略2: 使用code-review技能审查测试质量
        if self._contains_suspicious_changes(diff_content):
            print("👀 检测到可疑修改，使用code-review审查...")
            return self._use_code_review(test_file, diff_content, context)

        # 策略3: 默认使用简单分析
        return self._simple_analysis(test_file, diff_content, context)

    def _contains_test_failure(self, diff: str) -> bool:
        """检查diff中是否包含测试失败"""
        failure_indicators = ['FAILED', 'ERROR', 'AssertionError', 'assert']
        return any(indicator in diff for indicator in failure_indicators)

    def _contains_suspicious_changes(self, diff: str) -> bool:
        """检查是否包含可疑的修改模式"""
        suspicious_patterns = [
            r'assert.*==.*\d+.*->.*assert.*==.*\d+',  # 修改断言值
            r'-.*assert.*\+.*\+.*assert',               # 删除重要断言
            r'Exception.*->.*.*Exception',              # 扩大异常捕获
        ]

        return any(re.search(pattern, diff, re.MULTILINE) for pattern in suspicious_patterns)

    def _use_systematic_debugging(self, test_file: str, diff: str, context: Dict) -> Dict:
        """
        使用systematic-debugging技能分析测试失败

        这是一个模拟实现，实际应该调用superpowers:systematic-debugging技能
        """

        # 在实际实现中，这里会调用systematic-debugging技能
        # 例如：通过Agent工具或skill调用

        debug_prompt = f"""
        使用systematic-debugging方法分析以下测试失败：

        测试文件: {test_file}
        修改内容:
        {diff}

        上下文: {context.get('task_info', {})}

        请分析：
        1. 测试失败的根因是什么？
        2. 这种修改是否真正修复了问题？
        3. 还是只是"应付"测试（修改断言值、删除测试等）？
        4. 如果是应付测试，正确的方法应该是什么？

        返回结构化的分析结果。
        """

        # 模拟AI分析结果
        analysis_result = {
            'has_issue': True,
            'confidence': 0.85,
            'issues': [{
                'severity': 'error',
                'category': 'test_failure',
                'message': '测试失败根因：逻辑错误，修改只是修改断言值',
                'suggestion': '应该修复实际的代码逻辑，而非修改测试断言'
            }]
        }

        return analysis_result

    def _use_code_review(self, test_file: str, diff: str, context: Dict) -> Dict:
        """
        使用code-review技能审查测试修改

        这是一个模拟实现，实际应该调用code-review技能
        """

        review_prompt = f"""
        请进行code-review，重点关注测试质量：

        测试文件: {test_file}
        修改内容: {diff}

        请检查：
        1. 测试逻辑是否完整？
        2. 断言是否充分？
        3. 是否存在弱化断言的情况？
        4. 修改是否符合测试最佳实践？

        返回审查意见。
        """

        # 模拟审查结果
        review_result = {
            'has_issue': True,
            'confidence': 0.75,
            'issues': [{
                'severity': 'warning',
                'category': 'test_quality',
                'message': '测试断言过于宽泛，可能无法捕获边界条件',
                'suggestion': '添加更具体的断言验证'
            }]
        }

        return review_result

    def _use_simplify(self, test_file: str, diff: str, context: Dict) -> Dict:
        """
        使用simplify技能分析测试代码

        这是一个模拟实现，实际应该调用simplify技能
        """

        simplify_prompt = f"""
        分析测试代码是否可以简化：

        测试文件: {test_file}
        修改内容: {diff}

        分析：
        1. 测试代码是否过于复杂？
        2. 是否有重复逻辑？
        3. 是否可以更清晰？

        返回简化建议。
        """

        return {
            'has_issue': False,
            'confidence': 0.6,
            'issues': []
        }

    def _simple_analysis(self, test_file: str, diff: str, context: Dict) -> Dict:
        """简单的规则分析（无需AI）"""
        issues = []

        # 基本规则检查
        if 'assert == ' in diff and '->' in diff:
            issues.append({
                'severity': 'warning',
                'category': 'data_tampering',
                'message': '检测到断言值修改，需要确认是否合理',
                'suggestion': '确保修改是基于合理的逻辑变更',
                'confidence': 0.7
            })

        return {
            'has_issue': len(issues) > 0,
            'confidence': 0.7,
            'issues': issues
        }


class SmartTestGuardian:
    """智能测试守护者（集成AI技能）"""

    def __init__(self):
        self.ai_integrator = AISkillIntegrator()
        self.baseline = None
        self.context = {}

    def pre_check(self, task_info: Dict) -> Dict:
        """前置检查"""
        print("🛡️  SmartTestGuardian: 前置检查")

        self.context = {'task_info': task_info}
        self.baseline = self._run_tests()

        print(f"✅ 基线捕获完成: {self.baseline['total_tests']} 个测试")
        return self.baseline

    def post_check(self, changes: Dict) -> Dict:
        """后置检查（集成AI技能）"""
        print("🛡️  SmartTestGuardian: 后置检查")

        # 1. 运行当前测试
        current = self._run_tests()

        # 2. 基本对比
        basic_issues = self._basic_comparison(self.baseline, current)

        # 3. AI智能分析测试修改
        ai_issues = []
        if changes.get('modified_files'):
            ai_issues = self._ai_analyze_changes(changes, current)

        # 4. 合并结果
        all_issues = basic_issues + ai_issues

        # 5. 评估结果
        result = self._evaluate_result(all_issues, current)

        return {
            'status': result['status'],
            'acceptable': result['acceptable'],
            'issues': all_issues,
            'baseline': self.baseline,
            'current': current,
            'analysis_summary': {
                'basic_issues': len(basic_issues),
                'ai_issues': len(ai_issues),
                'ai_skills_used': self._get_used_skills(ai_issues)
            }
        }

    def _run_tests(self) -> Dict:
        """运行测试（简化实现）"""
        # 实际应该调用run_tests.py
        return {
            'total_tests': 42,
            'passed': 40,
            'failed': 2,
            'errors': 0,
            'skipped': 0,
            'coverage': 85.0,
            'timestamp': self._get_timestamp()
        }

    def _basic_comparison(self, baseline: Dict, current: Dict) -> List[Dict]:
        """基本对比"""
        issues = []

        # 检查测试失败增加
        failed_increase = current['failed'] - baseline['failed']
        if failed_increase > 0:
            issues.append({
                'severity': 'error',
                'category': 'test_failure',
                'message': f'新增 {failed_increase} 个失败测试',
                'confidence': 1.0,
                'suggestion': '请修复失败的测试',
                'ai_skill_used': None
            })

        # 检查覆盖率下降
        coverage_drop = baseline['coverage'] - current['coverage']
        if coverage_drop > 5.0:
            issues.append({
                'severity': 'error',
                'category': 'coverage_regression',
                'message': f'覆盖率下降 {coverage_drop:.1f}%',
                'confidence': 0.95,
                'suggestion': '确保测试覆盖率',
                'ai_skill_used': None
            })

        return issues

    def _ai_analyze_changes(self, changes: Dict, current: Dict) -> List[Dict]:
        """AI分析变更"""
        ai_issues = []

        # 获取测试文件修改
        test_files = self._get_test_files(changes.get('modified_files', []))

        for test_file in test_files:
            # 获取文件diff
            diff = self._get_file_diff(test_file)
            if not diff:
                continue

            # 使用AI技能分析
            analysis = self.ai_integrator.analyze_test_modification(
                test_file, diff, self.context
            )

            if analysis.get('has_issue'):
                for issue in analysis.get('issues', []):
                    issue['ai_skill_used'] = analysis.get('skill_used', 'unknown')
                    issue['file'] = test_file
                    ai_issues.append(issue)

        return ai_issues

    def _get_test_files(self, files: List[str]) -> List[str]:
        """获取测试文件"""
        return [f for f in files if 'test' in f and f.endswith('.py')]

    def _get_file_diff(self, file_path: str) -> Optional[str]:
        """获取文件diff"""
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

    def _get_timestamp(self) -> str:
        """获取时间戳"""
        from datetime import datetime
        return datetime.now().isoformat()

    def _evaluate_result(self, issues: List[Dict], current: Dict) -> Dict:
        """评估结果"""
        critical_issues = [i for i in issues if i.get('severity') == 'error']

        if critical_issues:
            return {
                'status': 'failed',
                'acceptable': False,
                'message': f'发现 {len(critical_issues)} 个严重问题'
            }
        else:
            return {
                'status': 'passed',
                'acceptable': True,
                'message': '测试质量检查通过'
            }

    def _get_used_skills(self, ai_issues: List[Dict]) -> List[str]:
        """获取使用的AI技能"""
        skills = set()
        for issue in ai_issues:
            if issue.get('ai_skill_used'):
                skills.add(issue['ai_skill_used'])
        return list(skills)


# ============================================================================
# 实际使用示例
# ============================================================================

def demonstrate_ai_skill_integration():
    """演示AI技能集成"""

    print("🤖 AI技能集成测试守护者演示\n")
    print("=" * 70)

    guardian = SmartTestGuardian()

    # 模拟任务执行
    task_info = {
        'name': 'fix_graph_bug',
        'description': '修复Graph模块中的bug',
        'module': 'graph'
    }

    # 前置检查
    print("\n📋 阶段1: 前置检查")
    baseline = guardian.pre_check(task_info)
    print(f"基线: {baseline['total_tests']} 个测试，{baseline['passed']} 通过")

    # 模拟代码变更
    print("\n🔧 阶段2: 执行变更...")

    # 后置检查
    print("\n🧪 阶段3: 后置检查（AI分析）")
    changes = {
        'modified_files': [
            'src/graph/test/test_graph_models.py',
            'src/graph/node.py'
        ]
    }

    result = guardian.post_check(changes)

    # 显示结果
    print(f"\n📊 检查结果:")
    print(f"状态: {result['status']}")
    print(f"可接受: {result['acceptable']}")
    print(f"分析摘要: {result['analysis_summary']}")

    if result['issues']:
        print(f"\n🚨 发现的问题:")
        for i, issue in enumerate(result['issues'], 1):
            skill = issue.get('ai_skill_used', '规则检测')
            print(f"  [{i}] [{skill}] {issue['severity']}: {issue['message']}")
            if issue.get('suggestion'):
                print(f"      💡 {issue['suggestion']}")

    print("\n" + "=" * 70)


if __name__ == "__main__":
    demonstrate_ai_skill_integration()