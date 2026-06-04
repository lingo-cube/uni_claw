"""
OpenSpec Apply技能的TestGuardian包装器

这是一个实用的包装器，可以在现有的openspec-apply-change技能中
直接集成测试守护者功能，无需修改原有技能代码。
"""

from pathlib import Path
import sys
import json

# 添加hooks目录到路径
hooks_dir = Path(__file__).parent
sys.path.insert(0, str(hooks_dir))

try:
    from test_guardian_integration import pre_task_hook, post_task_hook
    TEST_GUARDIAN_AVAILABLE = True
except ImportError:
    print("⚠️  TestGuardian不可用，将跳过测试检查")
    TEST_GUARDIAN_AVAILABLE = False


class OpenSpecApplyWithGuardian:
    """
    OpenSpec Apply的测试守护者包装器

    用法:
        # 在openspec-apply-change技能中
        from openspec_hooks.openspec_apply_wrapper import OpenSpecApplyWithGuardian

        # 创建包装器
        guardian = OpenSpecApplyWithGuardian()

        # 在任务执行前调用
        guardian.pre_check(task_info)

        # 执行任务...

        # 在任务执行后调用
        result = guardian.post_check(changes)

        # 检查是否应该继续
        if not result.is_acceptable():
            # 处理失败情况
            pass
    """

    def __init__(self):
        self.baseline_data = None
        self.post_result = None
        self.enabled = TEST_GUARDIAN_AVAILABLE

    def pre_check(self, task_info: dict) -> dict:
        """任务执行前检查"""
        if not self.enabled:
            print("⏭️  跳过前置测试检查（TestGuardian不可用）")
            return {'status': 'skipped'}

        try:
            print("🛡️  TestGuardian: 开始前置检查...")
            self.baseline_data = pre_task_hook(task_info)
            print("✅ 前置检查完成")
            return self.baseline_data
        except Exception as e:
            print(f"⚠️  前置检查失败: {e}")
            return {'status': 'error', 'error': str(e)}

    def post_check(self, task_info: dict, changes: dict) -> dict:
        """任务执行后检查"""
        if not self.enabled:
            print("⏭️  跳过后置测试检查（TestGuardian不可用）")
            return {'status': 'skipped', 'acceptable': True}

        try:
            print("🛡️  TestGuardian: 开始后置检查...")
            self.post_result = post_test_hook_with_result(task_info, changes)

            # 转换为更易用的格式
            result = ApplyGuardianResult(
                status=self.post_result.get('status', 'unknown'),
                acceptable=self.post_result.get('status') == 'passed',
                issues=self.post_result.get('issues', []),
                baseline=self.baseline_data,
                current=self.post_result.get('current_state', {})
            )

            if result.is_acceptable():
                print("✅ 后置检查通过")
            else:
                print("🛑 后置检查未通过")

            return result.to_dict()

        except Exception as e:
            print(f"⚠️  后置检查失败: {e}")
            return {'status': 'error', 'error': str(e), 'acceptable': False}

    def is_acceptable(self) -> bool:
        """检查结果是否可接受"""
        if not self.enabled or not self.post_result:
            return True  # 守护者不可用时不阻断

        return self.post_result.get('status') == 'passed'

    def get_issues(self) -> list:
        """获取发现的问题"""
        if not self.post_result:
            return []
        return self.post_result.get('issues', [])


def post_test_hook_with_result(task_info: dict, changes: dict) -> dict:
    """后置钩子的结果处理版本"""
    # 直接调用test_guardian_integration中的函数
    result = post_task_hook(task_info, changes)

    # 添加acceptable字段以便快速判断
    if 'acceptable' not in result:
        result['acceptable'] = result.get('status') == 'passed'

    return result


class ApplyGuardianResult:
    """Apply结果的数据类"""

    def __init__(self, status: str, acceptable: bool, issues: list, baseline: dict, current: dict):
        self.status = status
        self.acceptable = acceptable
        self.issues = issues
        self.baseline = baseline
        self.current = current

    def is_acceptable(self) -> bool:
        """结果是否可接受"""
        return self.acceptable

    def has_blocking_issues(self) -> bool:
        """是否有阻塞性问题"""
        return not self.acceptable

    def get_error_issues(self) -> list:
        """获取错误级别的问题"""
        return [issue for issue in self.issues if issue.get('severity') == 'error']

    def get_warning_issues(self) -> list:
        """获取警告级别的问题"""
        return [issue for issue in self.issues if issue.get('severity') == 'warning']

    def to_dict(self) -> dict:
        """转换为字典"""
        return {
            'status': self.status,
            'acceptable': self.acceptable,
            'issues': self.issues,
            'baseline': self.baseline,
            'current': self.current
        }


# ============================================================================
# 集成到现有技能的示例
# ============================================================================

def integrate_into_openspec_apply():
    """
    展示如何在现有的openspec-apply-change技能中集成测试守护者

    在openspec-apply-change技能的适当位置添加以下代码：
    """

    example_code = '''
    # 在openspec-apply-change技能中添加导入
    from openspec_hooks.openspec_apply_wrapper import OpenSpecApplyWithGuardian

    # 在技能的开始处创建守护者实例
    def apply_change_with_guardian(change_name):
        """应用变更（带测试守护）"""

        # 创建守护者
        guardian = OpenSpecApplyWithGuardian()

        # 获取任务信息
        task_info = get_task_info(change_name)  # 你现有的代码

        # === 步骤1: 前置检查 ===
        print("\\n📋 步骤1: 前置测试检查")
        baseline = guardian.pre_check(task_info)

        # === 步骤2: 执行变更任务 ===
        print("\\n🔧 步骤2: 执行变更任务")
        try:
            # 调用你现有的任务执行逻辑
            execute_tasks(change_name)  # 你现有的代码
        except Exception as e:
            print(f"❌ 任务执行失败: {e}")
            return {"status": "failed"}

        # === 步骤3: 收集变更 ===
        print("\\n📊 步骤3: 收集代码变更")
        changes = collect_changes()  # 你现有的代码

        # === 步骤4: 后置检查 ===
        print("\\n🧪 步骤4: 后置测试验证")
        result = guardian.post_check(task_info, changes)

        # === 步骤5: 处理结果 ===
        if not guardian.is_acceptable():
            print("\\n🛑 测试质量检查未通过")
            print("发现的问题:")
            for issue in guardian.get_issues():
                print(f"  - {issue.get('message')}")

            return {
                "status": "blocked",
                "reason": "测试质量检查未通过",
                "issues": guardian.get_issues()
            }

        print("\\n✅ 变更应用成功")
        return {
            "status": "success",
            "baseline": baseline,
            "current": result.get('current')
        }

    # 然后替换原有的apply_change调用
    # 将: apply_change(change_name)
    # 改为: apply_change_with_guardian(change_name)
    '''

    return example_code


# ============================================================================
# 快速开始指南
# ============================================================================

def quick_start_guide():
    """快速开始指南"""

    guide = '''
    🚀 TestGuardian快速开始指南

    1. 确保文件结构:
       openspec/
       ├── hooks/
       │   ├── test_guardian_integration.py  # 核心守护者
       │   ├── openspec_apply_wrapper.py     # 此文件
       │   └── openspec_apply_integration.py  # 完整集成示例

    2. 在openspec-apply-change技能中添加导入:
       from openspec_hooks.openspec_apply_wrapper import OpenSpecApplyWithGuardian

    3. 在apply_change函数中集成:
       guardian = OpenSpecApplyWithGuardian()
       baseline = guardian.pre_check(task_info)
       # ... 执行任务 ...
       result = guardian.post_check(task_info, changes)

    4. 处理测试结果:
       if not guardian.is_acceptable():
           # 处理测试失败情况

    配置完成! 🎉
    '''

    return guide


if __name__ == "__main__":
    print("📚 TestGuardian集成示例")

    print("\n" + "=" * 70)
    print("快速开始指南")
    print("=" * 70)
    print(quick_start_guide())

    print("\n" + "=" * 70)
    print("集成代码示例")
    print("=" * 70)
    print(integrate_into_openspec_apply())