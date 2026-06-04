"""
OpenSpec Apply技能与TestGuardian集成示例

展示如何在/opsx:apply技能中集成测试守护者功能
"""

from pathlib import Path
from typing import Dict, Any, List
import subprocess
import sys


# 导入测试守护者
sys.path.insert(0, str(Path(__file__).parent.parent))
from hooks.test_guardian_integration import pre_task_hook, post_task_hook, TestGuardian


class OpenSpecApplyWithTestGuardian:
    """增强的OpenSpec Apply执行器，集成测试守护者"""

    def __init__(self):
        self.guardian: TestGuardian = None
        self.baseline_data = None
        self.task_info = None

    def apply_change(self, change_name: str) -> Dict[str, Any]:
        """应用变更（带测试守护）"""
        print(f"🚀 开始应用变更: {change_name}")
        print("=" * 70)

        # 1. 获取任务信息
        self.task_info = self._get_task_info(change_name)

        # 2. 前置测试检查
        print("\n📋 阶段1: 前置测试检查")
        try:
            baseline_result = pre_task_hook(self.task_info)
            self.baseline_data = baseline_result.get('baseline', {})
            print("✅ 前置检查完成")
        except Exception as e:
            print(f"⚠️  前置检查失败: {e}")
            # 前置检查失败不阻断流程，但记录警告

        # 3. 执行变更任务
        print(f"\n🔧 阶段2: 执行变更任务")
        try:
            execution_result = self._execute_tasks(change_name)
            print("✅ 任务执行完成")
        except Exception as e:
            print(f"❌ 任务执行失败: {e}")
            return {
                'status': 'failed',
                'error': str(e)
            }

        # 4. 收集变更信息
        changes = self._collect_changes()

        # 5. 后置测试验证
        print(f"\n🧪 阶段3: 后置测试验证")
        try:
            post_result = post_task_hook(self.task_info, changes)
            test_status = post_result.get('status', 'unknown')
            issues = post_result.get('issues', [])

            print(f"测试状态: {test_status}")
            if issues:
                print(f"发现问题: {len(issues)} 个")

            # 6. 根据测试结果决定是否阻断
            if test_status == 'failed':
                print(f"\n🛑 测试质量检查未通过")
                print(f"建议:")
                print(f"  1. 修复失败的测试")
                print(f"  2. 确保测试覆盖率不下降")
                print(f"  3. 避免修改测试数据而非修复问题")

                return {
                    'status': 'blocked',
                    'reason': '测试质量检查未通过',
                    'issues': issues,
                    'baseline': self.baseline_data,
                    'current': post_result.get('current_state')
                }
            else:
                print(f"\n✅ 变更应用成功，测试质量检查通过")
                return {
                    'status': 'success',
                    'baseline': self.baseline_data,
                    'current': post_result.get('current_state'),
                    'issues': issues
                }

        except Exception as e:
            print(f"⚠️  后置检查失败: {e}")
            # 后置检查失败建议回退，但不强制
            return {
                'status': 'warning',
                'error': str(e),
                'baseline': self.baseline_data
            }

    def _get_task_info(self, change_name: str) -> Dict[str, Any]:
        """获取任务信息"""
        # 这里应该从OpenSpec的任务文件中读取真实信息
        # 为示例提供简化版本
        return {
            'name': change_name,
            'description': f'OpenSpec变更: {change_name}',
            'files': self._get_related_files(change_name),
            'tasks': self._get_task_list(change_name)
        }

    def _get_related_files(self, change_name: str) -> List[str]:
        """获取变更相关的文件"""
        # 简化实现：从changes目录推断
        change_dir = Path.cwd() / 'openspec' / 'changes' / change_name
        if change_dir.exists():
            # 读取tasks.md或其他文件确定相关模块
            tasks_file = change_dir / 'tasks.md'
            if tasks_file.exists():
                content = tasks_file.read_text(encoding='utf-8')
                # 简单的模块推断
                if 'graph' in content.lower():
                    return ['src/graph/test/test_graph_models.py']
                elif 'ai' in content.lower():
                    return ['src/ai/test/test_unibrain.py']

        return []

    def _get_task_list(self, change_name: str) -> List[Dict[str, Any]]:
        """获取任务列表"""
        # 这里应该从tasks.md解析真实任务
        return [
            {'id': '1.1', 'description': '示例任务1', 'status': 'pending'},
            {'id': '1.2', 'description': '示例任务2', 'status': 'pending'},
        ]

    def _execute_tasks(self, change_name: str) -> Dict[str, Any]:
        """执行变更任务"""
        # 这里应该调用实际的OpenSpec执行逻辑
        # 为示例简化实现
        print(f"  📝 执行变更任务...")

        # 模拟执行一些代码变更
        print(f"  ✅ 任务1完成")
        print(f"  ✅ 任务2完成")

        return {'tasks_completed': 2}

    def _collect_changes(self) -> Dict[str, Any]:
        """收集代码变更信息"""
        # 这里应该使用git diff等工具收集真实的变更信息
        # 简化实现
        try:
            result = subprocess.run(
                ['git', 'diff', '--name-only', 'HEAD'],
                capture_output=True,
                text=True,
                timeout=10
            )

            modified_files = []
            if result.stdout:
                modified_files = result.stdout.strip().split('\n')

            return {
                'modified_files': modified_files,
                'has_changes': len(modified_files) > 0
            }

        except Exception as e:
            print(f"  ⚠️  无法收集变更信息: {e}")
            return {'modified_files': [], 'has_changes': False}


# ============================================================================
# 使用示例
# ============================================================================

def example_usage():
    """使用示例"""
    print("📚 OpenSpec Apply with TestGuardian 使用示例\n")

    applier = OpenSpecApplyWithTestGuardian()

    # 示例1: 正常成功的变更
    print("=" * 70)
    print("示例1: 正常成功的变更")
    print("=" * 70)
    result = applier.apply_change('test-successful-change')
    print(f"\n结果: {result['status']}\n")

    # 示例2: 测试失败的变更
    print("=" * 70)
    print("示例2: 测试失败的变更（模拟）")
    print("=" * 70)
    # 这里可以模拟测试失败的场景
    result = applier.apply_change('test-failed-change')
    print(f"\n结果: {result['status']}\n")


if __name__ == "__main__":
    example_usage()