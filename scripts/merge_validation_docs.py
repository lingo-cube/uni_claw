#!/usr/bin/env python3
"""
Validation Documentation Merge Tool

帮助在多模块测试场景中实现累积式更新，避免覆盖问题。
"""

import sys
import re
from pathlib import Path
from datetime import datetime
from typing import Dict, List, Optional


class ValidationDocMerger:
    """Validation文档合并工具"""

    def __init__(self, validation_dir: Path = None):
        self.validation_dir = validation_dir or Path.cwd() / "docs" / "validation"

    def merge_integration_test_results(self, new_suite_results: Dict[str, Dict]) -> str:
        """
        合并新的集成测试结果到现有文档

        Args:
            new_suite_results: 新测试套件结果
                {
                    "api_integration": {"total": 15, "passed": 14, "failed": 1, "timestamp": "..."},
                    "ui_integration": {"total": 8, "passed": 7, "failed": 1, "timestamp": "..."}
                }

        Returns:
            更新后的文档内容
        """
        doc_path = self.validation_dir / "integration_test_status.md"

        if not doc_path.exists():
            return self._create_new_integration_test_doc(new_suite_results)

        existing_content = doc_path.read_text(encoding='utf-8')
        existing_suites = self._parse_existing_integration_test_doc(existing_content)
        merged_suites = {**existing_suites, **new_suite_results}

        return self._generate_updated_integration_test_doc(merged_suites, existing_content)

    def _create_new_integration_test_doc(self, suite_results: Dict[str, Dict]) -> str:
        """创建新的集成测试文档"""
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

        total_tests = sum(s['total'] for s in suite_results.values())
        total_passed = sum(s['passed'] for s in suite_results.values())
        total_failed = sum(s['failed'] for s in suite_results.values())

        doc = f"""# Integration Test Status

**Generated**: {timestamp}
**Status**: COMPLETE
**Type**: Multi-Suite Integration Test Report

---

## Executive Summary

- **Total Tests**: {total_passed}/{total_tests} passing ({total_passed/total_tests*100:.1f}%)
- **Failed Tests**: {total_failed}
- **Test Suites**: {len(suite_results)}

## Latest Test Run ({timestamp})

"""

        for suite_name, results in suite_results.items():
            doc += f"### {suite_name.replace('_', ' ').title()} Integration Tests\n"
            doc += f"- Total: {results['total']} tests\n"
            doc += f"- Passed: {results['passed']}\n"
            doc += f"- Failed: {results['failed']}\n"
            if results.get('timestamp'):
                doc += f"- Test Time: {results['timestamp']}\n"
            doc += "\n"

        return doc

    def _parse_existing_integration_test_doc(self, content: str) -> Dict[str, Dict]:
        """解析现有集成测试文档"""
        suites = {}
        suite_pattern = r"### ([\w\s]+) Integration Tests\n"

        for match in re.finditer(suite_pattern, content):
            suite_name = match.group(1).lower().replace(' ', '_')
            section_start = match.start()
            section_end = content.find("\n###", section_start + 1)
            if section_end == -1:
                section_end = len(content)

            section_content = content[section_start:section_end]

            total_match = re.search(r"- Total: (\d+) tests", section_content)
            passed_match = re.search(r"- Passed: (\d+)", section_content)
            failed_match = re.search(r"- Failed: (\d+)", section_content)
            timestamp_match = re.search(r"- Test Time: ([^\n]+)", section_content)

            if total_match and passed_match:
                suites[suite_name] = {
                    "total": int(total_match.group(1)),
                    "passed": int(passed_match.group(1)),
                    "failed": int(failed_match.group(1)) if failed_match else 0,
                    "timestamp": timestamp_match.group(1) if timestamp_match else ""
                }

        return suites

    def _generate_updated_integration_test_doc(self, all_suites: Dict[str, Dict], original_content: str) -> str:
        """生成更新后的集成测试文档"""
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

        total_tests = sum(s['total'] for s in all_suites.values())
        total_passed = sum(s['passed'] for s in all_suites.values())
        total_failed = sum(s['failed'] for s in all_suites.values())

        # 更新执行摘要
        updated_content = re.sub(
            r"- \*\*Total Tests\*\*: [\d/]+ passing \([.\d]+%\)",
            f"- **Total Tests**: {total_passed}/{total_tests} passing ({total_passed/total_tests*100:.1f}%)",
            original_content
        )

        updated_content = re.sub(
            r"- \*\*Failed Tests\*\*: \d+",
            f"- **Failed Tests**: {total_failed}",
            updated_content
        )

        updated_content = re.sub(
            r"- \*\*Test Suites\*\*: \d+",
            f"- **Test Suites**: {len(all_suites)}",
            updated_content
        )

        # 更新时间戳
        updated_content = re.sub(
            r"\*\*Generated\*\*: [^\n]+",
            f"**Generated**: {timestamp}",
            updated_content
        )

        # 重新生成所有套件部分
        latest_section_pattern = r"(## Latest Test Run[^\n]+\n\n)"
        latest_section = f"## Latest Test Run ({timestamp})\n\n"

        updated_content = re.sub(
            r"## Latest Test Run[^\n]+\n\n",
            latest_section,
            updated_content
        )

        suites_section = ""
        for suite_name, results in all_suites.items():
            suites_section += f"### {suite_name.replace('_', ' ').title()} Integration Tests\n"
            suites_section += f"- Total: {results['total']} tests\n"
            suites_section += f"- Passed: {results['passed']}\n"
            suites_section += f"- Failed: {results['failed']}\n"
            if results.get('timestamp'):
                suites_section += f"- Test Time: {results['timestamp']}\n"
            suites_section += "\n"

        latest_start = updated_content.find("## Latest Test Run")
        if latest_start != -1:
            updated_content = updated_content[:latest_start] + latest_section + suites_section

        return updated_content

    def append_integration_suite_result(self, suite_name: str, test_results: Dict) -> bool:
        """向集成测试文档追加测试套件结果"""
        try:
            test_results['timestamp'] = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            updated_content = self.merge_integration_test_results({suite_name: test_results})

            doc_path = self.validation_dir / "integration_test_status.md"
            doc_path.write_text(updated_content, encoding='utf-8')

            print(f"[OK] 成功追加 {suite_name} 集成测试结果到 integration_test_status.md")
            return True

        except Exception as e:
            print(f"[ERROR] 追加集成测试结果失败: {e}")
            return False

    def update_progress_report(self, new_progress: Dict) -> str:
        """
        更新进度报告（累积式）

        Args:
            new_progress: 新的进度信息
                {
                    "phase": "Phase 2",
                    "status": "75% complete",
                    "achievements": ["Task 2.1完成", "Task 2.2完成"],
                    "blockers": [],
                    "next_steps": ["开始Task 2.3"]
                }

        Returns:
            更新后的文档内容
        """
        doc_path = self.validation_dir / "progress_report.md"

        if not doc_path.exists():
            return self._create_new_progress_doc(new_progress)

        existing_content = doc_path.read_text(encoding='utf-8')
        return self._generate_updated_progress_doc(new_progress, existing_content)

    def _create_new_progress_doc(self, progress: Dict) -> str:
        """创建新的进度报告"""
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

        doc = f"""# Progress Report

**Generated**: {timestamp}
**Current Phase**: {progress.get('phase', 'Unknown')}
**Overall Status**: {progress.get('status', '0%')}

---

## Latest Update ({timestamp})

### Current Status
- **Phase**: {progress.get('phase', 'Unknown')}
- **Status**: {progress.get('status', 'Unknown')}

### Recent Achievements
"""

        for achievement in progress.get('achievements', []):
            doc += f"- ✅ {achievement}\n"

        if progress.get('blockers'):
            doc += "\n### Current Blockers\n"
            for blocker in progress['blockers']:
                doc += f"- 🚧 {blocker}\n"

        doc += "\n### Next Steps\n"
        for step in progress.get('next_steps', []):
            doc += f"- 📋 {step}\n"

        return doc

    def _generate_updated_progress_doc(self, new_progress: Dict, existing_content: str) -> str:
        """生成更新后的进度报告"""
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

        # 保留历史进度记录
        history_pattern = r"(## Update History\n\n)"
        history_section = "\n## Update History\n\n"

        # 如果没有历史部分，创建它
        if "## Update History" not in existing_content:
            existing_content += history_section
        else:
            history_section = ""

        # 在历史部分之前插入新的更新
        new_update = f"### {timestamp} - {new_progress.get('phase', 'Update')}\n"
        new_update += f"- **Status**: {new_progress.get('status', 'Unknown')}\n"

        if new_progress.get('achievements'):
            new_update += "- **Achievements**:\n"
            for achievement in new_progress['achievements']:
                new_update += f"  - ✅ {achievement}\n"

        if new_progress.get('blockers'):
            new_update += "- **Blockers**:\n"
            for blocker in new_progress['blockers']:
                new_update += f"  - 🚧 {blocker}\n"

        new_update += "- **Next Steps**:\n"
        for step in new_progress.get('next_steps', []):
            new_update += f"  - 📋 {step}\n"

        new_update += "\n"

        # 更新最新的更新部分
        latest_pattern = r"(## Latest Update[^\n]+\n\n.*?)(?=\n\n## Update History|$)"
        latest_update_section = f"## Latest Update ({timestamp})\n\n### Current Status\n- **Phase**: {new_progress.get('phase', 'Unknown')}\n- **Status**: {new_progress.get('status', 'Unknown')}\n\n"

        if new_progress.get('achievements'):
            latest_update_section += "### Recent Achievements\n"
            for achievement in new_progress['achievements']:
                latest_update_section += f"- ✅ {achievement}\n"
            latest_update_section += "\n"

        if new_progress.get('blockers'):
            latest_update_section += "### Current Blockers\n"
            for blocker in new_progress['blockers']:
                latest_update_section += f"- 🚧 {blocker}\n"
            latest_update_section += "\n"

        latest_update_section += "### Next Steps\n"
        for step in new_progress.get('next_steps', []):
            latest_update_section += f"- 📋 {step}\n"

        updated_content = re.sub(
            latest_pattern,
            latest_update_section,
            existing_content,
            flags=re.DOTALL
        )

        # 添加到历史记录
        history_start = updated_content.find("## Update History")
        if history_start != -1:
            updated_content = (updated_content[:history_start + len("## Update History\n\n")] +
                             new_update +
                             updated_content[history_start + len("## Update History\n\n"):])

        return updated_content

    def add_progress_update(self, phase: str, status: str, achievements: list, blockers: list, next_steps: list) -> bool:
        """添加进度更新"""
        try:
            progress = {
                "phase": phase,
                "status": status,
                "achievements": achievements,
                "blockers": blockers,
                "next_steps": next_steps
            }
            updated_content = self.update_progress_report(progress)

            doc_path = self.validation_dir / "progress_report.md"
            doc_path.write_text(updated_content, encoding='utf-8')

            print(f"[OK] 成功添加进度更新到 progress_report.md")
            return True

        except Exception as e:
            print(f"[ERROR] 添加进度更新失败: {e}")
            return False

    def merge_unit_test_results(self, new_module_results: Dict[str, Dict]) -> str:
        """
        合并新的单元测试结果到现有文档

        Args:
            new_module_results: 新模块测试结果
                {
                    "simulation": {"total": 33, "passed": 33, "failed": 0, "timestamp": "..."},
                    "state_machine": {"total": 20, "passed": 20, "failed": 0, "timestamp": "..."}
                }

        Returns:
            更新后的文档内容
        """
        doc_path = self.validation_dir / "unit_test_status.md"

        # 如果文档不存在，创建新文档
        if not doc_path.exists():
            return self._create_new_unit_test_doc(new_module_results)

        # 读取现有文档
        existing_content = doc_path.read_text(encoding='utf-8')

        # 解析现有文档，提取已有模块结果
        existing_modules = self._parse_existing_unit_test_doc(existing_content)

        # 合并新结果
        merged_modules = {**existing_modules, **new_module_results}

        # 生成更新后的文档
        return self._generate_updated_unit_test_doc(merged_modules, existing_content)

    def _create_new_unit_test_doc(self, module_results: Dict[str, Dict]) -> str:
        """创建新的单元测试文档"""
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

        # 计算总体统计
        total_tests = sum(m['total'] for m in module_results.values())
        total_passed = sum(m['passed'] for m in module_results.values())
        total_failed = sum(m['failed'] for m in module_results.values())

        doc = f"""# Unit Test Status

**Generated**: {timestamp}
**Status**: COMPLETE
**Type**: Multi-Module Test Report

---

## Executive Summary

- **Total Tests**: {total_passed}/{total_tests} passing ({total_passed/total_tests*100:.1f}%)
- **Failed Tests**: {total_failed}
- **Modules Tested**: {len(module_results)}

## Latest Test Run ({timestamp})

"""

        # 添加每个模块的结果
        for module_name, results in module_results.items():
            doc += f"### {module_name.replace('_', ' ').title()} Module\n"
            doc += f"- Total: {results['total']} tests\n"
            doc += f"- Passed: {results['passed']}\n"
            doc += f"- Failed: {results['failed']}\n"
            if results.get('timestamp'):
                doc += f"- Test Time: {results['timestamp']}\n"
            doc += "\n"

        return doc

    def _parse_existing_unit_test_doc(self, content: str) -> Dict[str, Dict]:
        """解析现有单元测试文档，提取模块结果"""
        modules = {}

        # 查找所有模块部分
        module_pattern = r"### (\w+) Module\n"
        for match in re.finditer(module_pattern, content):
            module_name = match.group(1).lower().replace(' ', '_')

            # 提取统计数据
            section_start = match.start()
            section_end = content.find("\n###", section_start + 1)
            if section_end == -1:
                section_end = len(content)

            section_content = content[section_start:section_end]

            # 解析测试结果
            total_match = re.search(r"- Total: (\d+) tests", section_content)
            passed_match = re.search(r"- Passed: (\d+)", section_content)
            failed_match = re.search(r"- Failed: (\d+)", section_content)
            timestamp_match = re.search(r"- Test Time: ([^\n]+)", section_content)

            if total_match and passed_match:
                modules[module_name] = {
                    "total": int(total_match.group(1)),
                    "passed": int(passed_match.group(1)),
                    "failed": int(failed_match.group(1)) if failed_match else 0,
                    "timestamp": timestamp_match.group(1) if timestamp_match else ""
                }

        return modules

    def _generate_updated_unit_test_doc(self, all_modules: Dict[str, Dict], original_content: str) -> str:
        """生成更新后的单元测试文档"""
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

        # 计算总体统计
        total_tests = sum(m['total'] for m in all_modules.values())
        total_passed = sum(m['passed'] for m in all_modules.values())
        total_failed = sum(m['failed'] for m in all_modules.values())

        # 保持原始文档的头部，更新执行摘要
        header_pattern = r"(# Unit Test Status\n\n.*?\n\n---\n\n## Executive Summary\n\n)(.*?)(\n\n## Latest)"
        header_replacement = f"\\1- **Total Tests**: {total_passed}/{total_tests} passing ({total_passed/total_tests*100:.1f}%)\n- **Failed Tests**: {total_failed}\n- **Modules Tested**: {len(all_modules)}\\3"

        updated_content = re.sub(header_pattern, header_replacement, original_content, flags=re.DOTALL)

        # 更新时间戳
        updated_content = re.sub(
            r"\*\*Generated\*\*: [^\n]+",
            f"**Generated**: {timestamp}",
            updated_content
        )

        # 更新或添加模块结果部分
        latest_section_pattern = r"(## Latest Test Run[^\n]+\n\n)"
        latest_section = f"## Latest Test Run ({timestamp})\n\n"

        # 替换Latest Test Run部分
        updated_content = re.sub(
            r"## Latest Test Run[^\n]+\n\n",
            latest_section,
            updated_content
        )

        # 重新生成所有模块部分
        modules_section = ""
        for module_name, results in all_modules.items():
            modules_section += f"### {module_name.replace('_', ' ').title()} Module\n"
            modules_section += f"- Total: {results['total']} tests\n"
            modules_section += f"- Passed: {results['passed']}\n"
            modules_section += f"- Failed: {results['failed']}\n"
            if results.get('timestamp'):
                modules_section += f"- Test Time: {results['timestamp']}\n"
            modules_section += "\n"

        # 替换模块部分（从Latest Test Run后到文档末尾）
        latest_start = updated_content.find("## Latest Test Run")
        if latest_start != -1:
            updated_content = updated_content[:latest_start] + latest_section + modules_section

        return updated_content

    def append_module_result(self, module_name: str, test_results: Dict) -> bool:
        """
        向现有文档追加单个模块的结果

        Args:
            module_name: 模块名称
            test_results: 测试结果 {"total": 33, "passed": 33, "failed": 0}

        Returns:
            是否成功更新
        """
        try:
            # 添加时间戳
            test_results['timestamp'] = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

            # 合并结果
            updated_content = self.merge_unit_test_results({module_name: test_results})

            # 写入文件
            doc_path = self.validation_dir / "unit_test_status.md"
            doc_path.write_text(updated_content, encoding='utf-8')

            print(f"[OK] 成功追加 {module_name} 模块测试结果到 unit_test_status.md")
            return True

        except Exception as e:
            print(f"[ERROR] 追加模块结果失败: {e}")
            return False


def main():
    """主函数 - 用于演示累积式更新"""
    print("Validation Documentation Merge Tool")
    print("=" * 70)
    print()

    merger = ValidationDocMerger()

    # 示例：模拟多模块测试场景
    print("[DEMO] 多模块测试累积式更新示例")
    print()

    # 模块1测试完成
    print("[STEP 1] 模块1 (simulation) 测试完成")
    simulation_results = {
        "total": 33,
        "passed": 33,
        "failed": 0
    }
    merger.append_module_result("simulation", simulation_results)
    print()

    # 模块2测试完成
    print("[STEP 2] 模块2 (state_machine) 测试完成")
    state_machine_results = {
        "total": 20,
        "passed": 20,
        "failed": 0
    }
    merger.append_module_result("state_machine", state_machine_results)
    print()

    # 模块3测试完成
    print("[STEP 3] 模块3 (graph_engine) 测试完成")
    graph_engine_results = {
        "total": 31,
        "passed": 31,
        "failed": 0
    }
    merger.append_module_result("graph_engine", graph_engine_results)
    print()

    print("=" * 70)
    print("[RESULT] 所有模块测试结果已合并到 unit_test_status.md")
    print("[INFO] 总计: 84/84 tests passing (100%)")


if __name__ == "__main__":
    main()