"""
OpenSpec模块单元测试Hook集成

在OpenSpec工作流中集成module-test技能，确保代码变更时测试完整性。
"""

import subprocess
import sys
import json
from pathlib import Path
from typing import Dict, Any, List


class ModuleTestHook:
    """模块单元测试Hook类"""

    def __init__(self, project_root: Path = None):
        self.project_root = project_root or Path.cwd()
        self.skill_path = self.project_root / ".claude" / "skills" / "module-test" / "SKILL.md"

    def pre_task_hook(self, task_info: Dict[str, Any]) -> Dict[str, Any]:
        """任务执行前Hook - 捕获测试基线"""
        print("🛡️  ModuleTestHook: 任务前置检查")

        # 识别受影响的模块
        changed_modules = self._identify_changed_modules()

        if not changed_modules:
            print("✅ 无代码变更，跳过测试检查")
            return {"status": "skipped", "reason": "no_code_changes"}

        print(f"📋 受影响的模块: {', '.join(changed_modules)}")

        # 捕获测试基线
        baseline = self._capture_test_baseline(changed_modules)

        return {
            "status": "baseline_captured",
            "modules": changed_modules,
            "baseline": baseline
        }

    def post_task_hook(self, task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
        """任务执行后Hook - 验证测试完整性"""
        print("🛡️  ModuleTestHook: 任务后置验证")

        # 识别受影响的模块
        changed_modules = self._identify_changed_modules()

        if not changed_modules:
            print("✅ 无代码变更，跳过测试检查")
            return {"status": "skipped", "reason": "no_code_changes"}

        print(f"📋 验证模块: {', '.join(changed_modules)}")

        # 检查是否应该触发module-test技能
        should_trigger = self._should_trigger_module_test_skill(changed_modules)

        if not should_trigger:
            print("ℹ️  跳过module-test技能触发")
            return {"status": "skipped", "reason": "conditions_not_met"}

        # 触发module-test技能
        print("🔔 触发module-test技能...")
        test_result = self._trigger_module_test_skill(changed_modules)

        return test_result

    def _identify_changed_modules(self) -> List[str]:
        """识别变更的模块"""
        try:
            result = subprocess.run(
                ['git', 'diff', '--name-only'],
                capture_output=True,
                text=True,
                cwd=self.project_root
            )

            if result.returncode != 0:
                return []

            changed_files = result.stdout.strip().split('\n')

            # 提取模块名
            modules = set()
            for file in changed_files:
                if file.startswith('src/') and file.endswith('.py'):
                    parts = Path(file).parts
                    if len(parts) > 2:
                        modules.add(parts[1])  # src/graph/node.py → graph
                elif file.startswith('lib/') and file.endswith('.py'):
                    parts = Path(file).parts
                    if len(parts) > 2:
                        modules.add(parts[1])  # lib/util/helper.py → util
                elif file.startswith('app/') and file.endswith('.py'):
                    parts = Path(file).parts
                    if len(parts) > 2:
                        modules.add(parts[1])  # app/service/api.py → service

            return sorted(list(modules))

        except Exception as e:
            print(f"⚠️  识别变更模块失败: {e}")
            return []

    def _capture_test_baseline(self, modules: List[str]) -> Dict[str, Any]:
        """捕获测试基线"""
        baseline = {}

        for module in modules:
            # 检查模块是否有测试
            if self._has_tests(module):
                # 运行测试获取基线
                result = self._run_module_tests(module)
                baseline[module] = result
            else:
                baseline[module] = {"status": "no_tests", "message": f"{module}模块暂无测试"}

        return baseline

    def _has_tests(self, module: str) -> bool:
        """检查模块是否有测试"""
        possible_paths = [
            self.project_root / f"src/{module}/test",
            self.project_root / f"tests/{module}",
            self.project_root / f"test/{module}",
            self.project_root / f"src/{module}/tests",
        ]

        for path in possible_paths:
            if path.exists() and any(path.glob("test_*.py")):
                return True

        return False

    def _run_module_tests(self, module: str) -> Dict[str, Any]:
        """运行模块测试"""
        try:
            # 检查是否有统一测试脚本
            test_script = self.project_root / f"src/{module}/run_tests.py"

            if test_script.exists():
                print(f"  🧪 运行 {module} 测试脚本...")
                result = subprocess.run(
                    [sys.executable, str(test_script)],
                    capture_output=True,
                    text=True,
                    cwd=self.project_root
                )
            else:
                print(f"  🧪 使用pytest运行 {module} 测试...")
                test_path = self._find_test_path(module)
                if test_path:
                    result = subprocess.run(
                        [sys.executable, "-m", "pytest", str(test_path), "-v"],
                        capture_output=True,
                        text=True,
                        cwd=self.project_root
                    )
                else:
                    return {"status": "no_tests", "message": f"未找到{module}的测试路径"}

            # 解析测试结果
            return self._parse_test_result(result.stdout, result.stderr, module)

        except Exception as e:
            return {"status": "error", "message": f"测试执行失败: {e}"}

    def _find_test_path(self, module: str) -> Path:
        """查找模块的测试路径"""
        possible_paths = [
            self.project_root / f"src/{module}/test",
            self.project_root / f"tests/{module}",
            self.project_root / f"test/{module}",
            self.project_root / f"src/{module}/tests",
        ]

        for path in possible_paths:
            if path.exists():
                return path

        return None

    def _parse_test_result(self, stdout: str, stderr: str, module: str) -> Dict[str, Any]:
        """解析测试结果"""
        # 简单解析pytest输出
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
            "module": module,
            "status": status,
            "summary": {
                "total": total,
                "passed": passed,
                "failed": failed,
                "errors": errors
            },
            "output": stdout
        }

    def _should_trigger_module_test_skill(self, modules: List[str]) -> bool:
        """判断是否应该触发module-test技能"""
        # 检查技能文件是否存在
        if not self.skill_path.exists():
            print("⚠️  module-test技能不存在，跳过")
            return False

        # 检查是否有模块存在测试
        modules_with_tests = [m for m in modules if self._has_tests(m)]

        if not modules_with_tests:
            print("ℹ️  所有变更模块都无测试，跳过module-test技能")
            return False

        print(f"✅ {len(modules_with_tests)}个模块有测试，将触发module-test技能")
        return True

    def _trigger_module_test_skill(self, modules: List[str]) -> Dict[str, Any]:
        """触发module-test技能"""
        print("📖 使用module-test技能处理测试...")
        print(f"🎯 技能文档: {self.skill_path}")
        print(f"📋 处理模块: {', '.join(modules)}")
        print()
        print("=== module-test技能内容摘要 ===")
        print("技能名称: module-test")
        print("技能描述: 通用模块单元测试执行与失败处理")
        print()
        print("核心步骤:")
        print("1. 识别变更范围")
        print("2. 读取测试配置")
        print("3. 分析依赖关系")
        print("4. 环境准备与隔离")
        print("5. 自动检测测试框架")
        print("6. 执行测试")
        print("7. 检查测试覆盖率")
        print("8. 处理测试失败")
        print("9. 记录决策过程")
        print("10. 回归测试验证")
        print()
        print("📋 请按照技能文档的步骤执行测试")
        print("🔧 如果遇到测试失败，请按照技能中的优先级处理：")
        print("   Level 0: 环境问题")
        print("   Level 1: 代码实现分析")
        print("   Level 2: 设计文档检查")
        print("   Level 3: 询问用户意见")
        print("   Level 4: 谨慎修改测试用例")
        print()

        # 返回建议结果
        return {
            "status": "skill_triggered",
            "action_required": True,
            "skill_name": "module-test",
            "modules": modules,
            "message": "已触发module-test技能，请按照技能文档执行测试"
        }


# ============================================================================
# Hook函数接口（供OpenSpec工作流调用）
# ============================================================================

# 全局hook实例
_module_test_hook = None

def pre_task_hook(task_info: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行前Hook - 模块单元测试基线捕获"""
    global _module_test_hook

    if _module_test_hook is None:
        _module_test_hook = ModuleTestHook()

    return _module_test_hook.pre_task_hook(task_info)

def post_task_hook(task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行后Hook - 模块单元测试验证"""
    global _module_test_hook

    if _module_test_hook is None:
        _module_test_hook = ModuleTestHook()

    return _module_test_hook.post_task_hook(task_info, changes)


# ============================================================================
# 直接调用接口（供其他脚本使用）
# ============================================================================

def check_module_tests_after_change() -> Dict[str, Any]:
    """在代码变更后检查模块测试"""
    hook = ModuleTestHook()

    # 模拟task_info和changes
    task_info = {"name": "code_change", "description": "代码变更"}
    changes = {"modified_files": hook._identify_changed_modules()}

    return hook.post_task_hook(task_info, changes)

def main():
    """主函数 - 用于测试hook功能"""
    print("🧪 ModuleTestHook 功能测试")
    print("=" * 70)

    hook = ModuleTestHook()

    # 测试前置hook
    print("\n=== 测试前置Hook ===")
    task_info = {"name": "test_change", "description": "测试变更"}
    baseline = hook.pre_task_hook(task_info)
    print(f"基线结果: {baseline}")

    # 测试后置hook
    print("\n=== 测试后置Hook ===")
    # 模拟一些变更
    changes = {"modified_files": ["src/graph/node.py"]}
    result = hook.post_task_hook(task_info, changes)
    print(f"后置结果: {result}")

    print("\n" + "=" * 70)
    print("✅ ModuleTestHook 测试完成")


if __name__ == "__main__":
    main()