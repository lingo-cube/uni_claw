"""
OpenSpec集成测试Hook集成

在OpenSpec工作流中集成集成测试执行，确保系统集成正确性。
"""

import subprocess
import sys
import json
from pathlib import Path
from typing import Dict, Any, List


class IntegrationTestHook:
    """集成测试Hook类"""

    def __init__(self, project_root: Path = None):
        self.project_root = project_root or Path.cwd()
        self.skill_path = self.project_root / ".claude" / "skills" / "module-test" / "SKILL.md"

    def pre_task_hook(self, task_info: Dict[str, Any]) -> Dict[str, Any]:
        """任务执行前Hook - 捕获集成测试基线"""
        print("🔄 IntegrationTestHook: 任务前置检查")

        # 检查当前change是否需要集成测试
        needs_integration = self._needs_integration_tests(task_info)

        if not needs_integration:
            print("✅ 当前任务不需要集成测试，跳过检查")
            return {"status": "skipped", "reason": "no_integration_needed"}

        print("📋 检测到集成测试需求")

        # 捕获集成测试基线
        baseline = self._capture_integration_baseline()

        return {
            "status": "baseline_captured",
            "baseline": baseline
        }

    def post_task_hook(self, task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
        """任务执行后Hook - 运行集成测试"""
        print("🔄 IntegrationTestHook: 任务后置验证")

        # 检查是否应该运行集成测试
        should_run = self._should_run_integration_tests(task_info, changes)

        if not should_run:
            print("ℹ️  跳过集成测试执行")
            return {"status": "skipped", "reason": "conditions_not_met"}

        print("🔔 运行集成测试...")
        test_result = self._run_integration_tests()

        return test_result

    def _needs_integration_tests(self, task_info: Dict[str, Any]) -> bool:
        """检查任务是否需要集成测试"""
        task_desc = task_info.get('description', '').lower()
        task_name = task_info.get('name', '').lower()

        # 集成测试关键词
        integration_keywords = [
            'integration', 'system', 'e2e', 'end-to-end',
            'workflow', 'complete', 'full', 'combined',
            'interaction', 'api', 'service'
        ]

        has_keyword = any(keyword in task_desc or keyword in task_name
                         for keyword in integration_keywords)

        # 检查change名称
        change_name = task_info.get('change', '').lower()
        is_integration_change = any(keyword in change_name
                                   for keyword in ['integration', 'system', 'e2e', 'validation'])

        return has_keyword or is_integration_change

    def _capture_integration_baseline(self) -> Dict[str, Any]:
        """捕获集成测试基线"""
        try:
            print("  🧪 运行集成测试基线检查...")

            # 运行集成测试获取基线
            result = subprocess.run(
                [sys.executable, "-m", "pytest", "tests/integration/", "-v", "--tb=short"],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            return self._parse_integration_result(result.stdout, result.stderr)

        except Exception as e:
            return {"status": "error", "error": str(e)}

    def _run_integration_tests(self) -> Dict[str, Any]:
        """运行集成测试"""
        try:
            print("  🧪 运行集成测试...")

            # 运行集成测试
            result = subprocess.run(
                [sys.executable, "-m", "pytest", "tests/integration/", "-v", "--tb=short"],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            test_result = self._parse_integration_result(result.stdout, result.stderr)

            # 如果有失败，触发module-test技能处理
            if test_result.get('failed', 0) > 0:
                print("  ⚠️  集成测试有失败，触发module-test技能")
                return {
                    "status": "skill_triggered",
                    "action_required": True,
                    "skill_name": "module-test",
                    "test_result": test_result,
                    "message": "集成测试有失败，请使用module-test技能处理"
                }

            return {
                "status": "passed",
                "test_result": test_result,
                "message": "所有集成测试通过"
            }

        except Exception as e:
            return {
                "status": "error",
                "error": str(e),
                "message": f"集成测试执行失败: {e}"
            }

    def _should_run_integration_tests(self, task_info: Dict[str, Any], changes: Dict[str, Any]) -> bool:
        """判断是否应该运行集成测试"""
        # 检查任务是否需要集成测试
        if not self._needs_integration_tests(task_info):
            return False

        # 检查是否有代码变更
        has_changes = changes.get('modified_files') or changes.get('new_files')

        if has_changes:
            print("✅ 检测到变更且需要集成测试，将运行集成测试")
            return True

        # 检查是否是集成类型的任务
        task_type = task_info.get('type', '').lower()
        if task_type in ['integration', 'system', 'e2e']:
            print("✅ 集成类型任务，将运行集成测试")
            return True

        return False

    def _parse_integration_result(self, stdout: str, stderr: str) -> Dict[str, Any]:
        """解析集成测试结果"""
        lines = stdout.split('\n')

        passed = sum(1 for line in lines if 'PASSED' in line)
        failed = sum(1 for line in lines if 'FAILED' in line)
        errors = sum(1 for line in lines if 'ERROR' in line)

        total = passed + failed + errors

        if failed > 0 or errors > 0:
            status = "failed"
        else:
            status = "passed"

        return {
            "status": status,
            "summary": {
                "total": total,
                "passed": passed,
                "failed": failed,
                "errors": errors
            },
            "output": stdout
        }


# ============================================================================
# Hook函数接口（供OpenSpec工作流调用）
# ============================================================================

# 全局hook实例
_integration_test_hook = None

def pre_task_hook(task_info: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行前Hook - 集成测试基线捕获"""
    global _integration_test_hook

    if _integration_test_hook is None:
        _integration_test_hook = IntegrationTestHook()

    return _integration_test_hook.pre_task_hook(task_info)

def post_task_hook(task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行后Hook - 集成测试执行"""
    global _integration_test_hook

    if _integration_test_hook is None:
        _integration_test_hook = IntegrationTestHook()

    return _integration_test_hook.post_task_hook(task_info, changes)


# ============================================================================
# 直接调用接口（供其他脚本使用）
# ============================================================================

def run_integration_tests_after_change() -> Dict[str, Any]:
    """在代码变更后运行集成测试"""
    hook = IntegrationTestHook()

    task_info = {"name": "integration_test", "description": "Integration testing"}
    changes = {"modified_files": ["src/some_module.py"]}

    return hook.post_task_hook(task_info, changes)

def main():
    """主函数 - 用于测试hook功能"""
    print("🔄 IntegrationTestHook 功能测试")
    print("=" * 70)

    hook = IntegrationTestHook()

    # 测试前置hook
    print("\n=== 测试前置Hook ===")
    task_info = {
        "name": "integration_validation",
        "description": "Validate integration completeness",
        "change": "integration-validation"
    }
    baseline = hook.pre_task_hook(task_info)
    print(f"基线结果: {baseline}")

    # 测试后置hook
    print("\n=== 测试后置Hook ===")
    changes = {"modified_files": ["tests/integration/test_api.py"]}
    result = hook.post_task_hook(task_info, changes)
    print(f"后置结果: {result}")

    print("\n" + "=" * 70)
    print("✅ IntegrationTestHook 测试完成")


if __name__ == "__main__":
    main()