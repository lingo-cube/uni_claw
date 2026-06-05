"""
OpenSpec Validation Documentation Hook Integration

在OpenSpec工作流中集成validation-documentation技能，确保验证文档的标准化生成和命名。
"""

import subprocess
import sys
import json
from pathlib import Path
from typing import Dict, Any, List
from datetime import datetime


class ValidationDocumentationHook:
    """验证文档Hook类"""

    def __init__(self, project_root: Path = None):
        self.project_root = project_root or Path.cwd()
        self.skill_path = self.project_root / ".claude" / "skills" / "validation-documentation" / "SKILL.md"
        self.validation_dir = self.project_root / "docs" / "validation"

    def pre_task_hook(self, task_info: Dict[str, Any]) -> Dict[str, Any]:
        """任务执行前Hook - 检查validation文档需求"""
        print("[VALIDATION] ValidationDocumentationHook: 任务前置检查")

        # 检查当前change是否需要生成validation文档
        needs_validation = self._needs_validation_docs(task_info)

        if not needs_validation:
            print("[OK] 当前任务不需要validation文档，跳过检查")
            return {"status": "skipped", "reason": "no_validation_needed"}

        print("[INFO] 检测到validation文档需求")

        # 检查现有的validation文档状态
        existing_docs = self._check_existing_validation_docs()

        return {
            "status": "validation_required",
            "existing_docs": existing_docs,
            "standards_check": self._check_document_standards()
        }

    def post_task_hook(self, task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
        """任务执行后Hook - 验证文档生成检查"""
        print("[VALIDATION] ValidationDocumentationHook: 任务后置验证")

        # 检查是否应该触发validation-documentation技能
        should_trigger = self._should_trigger_validation_skill(task_info, changes)

        if not should_trigger:
            print("[INFO] 跳过validation-documentation技能触发")
            return {"status": "skipped", "reason": "conditions_not_met"}

        # 触发validation-documentation技能
        print("[TRIGGER] 触发validation-documentation技能...")
        validation_result = self._trigger_validation_documentation_skill(task_info, changes)

        return validation_result

    def _needs_validation_docs(self, task_info: Dict[str, Any]) -> bool:
        """检查任务是否需要生成validation文档"""
        task_desc = task_info.get('description', '').lower()
        task_name = task_info.get('name', '').lower()

        # 关键词检查
        validation_keywords = [
            'validation', 'verify', 'test', 'check', 'validate',
            'testing', 'verification', 'analysis', 'report', 'status'
        ]

        has_keyword = any(keyword in task_desc or keyword in task_name
                         for keyword in validation_keywords)

        # 检查change名称
        change_name = task_info.get('change', '').lower()
        is_validation_change = any(keyword in change_name
                                   for keyword in ['validation', 'testing', 'verification'])

        return has_keyword or is_validation_change

    def _check_existing_validation_docs(self) -> Dict[str, Any]:
        """检查现有的validation文档"""
        if not self.validation_dir.exists():
            return {"status": "no_validation_dir", "documents": []}

        # 获取所有validation文档
        doc_files = list(self.validation_dir.glob("*.md"))
        doc_files = [f for f in doc_files if not f.name.startswith("_")]

        documents = []
        for doc_file in doc_files:
            documents.append({
                "name": doc_file.name,
                "size": doc_file.stat().st_size,
                "modified": datetime.fromtimestamp(doc_file.stat().st_mtime).isoformat()
            })

        return {
            "status": "found",
            "count": len(documents),
            "documents": documents
        }

    def _check_document_standards(self) -> Dict[str, Any]:
        """检查现有文档是否符合标准"""
        if not self.validation_dir.exists():
            return {"status": "no_validation_dir", "compliant": 0, "non_compliant": 0}

        # 检查命名标准
        compliant_files = []
        non_compliant_files = []

        standard_names = {
            'final_report.md',
            'unit_test_status.md',
            'integration_test_status.md',
            'system_infrastructure_analysis.md',
            'test_data_quality.md',
            'progress_report.md',
            'planned_features.md'
        }

        for doc_file in self.validation_dir.glob("*.md"):
            if doc_file.name.startswith("_"):
                continue

            if doc_file.name in standard_names:
                compliant_files.append(doc_file.name)
            else:
                # 检查是否包含非标准模式
                issues = []
                if "V6" in doc_file.name or "V5" in doc_file.name:
                    issues.append("version_prefix")
                if "2026-" in doc_file.name or "2025-" in doc_file.name:
                    issues.append("date_pattern")
                if "_v2" in doc_file.name.lower() or "_v1" in doc_file.name.lower():
                    issues.append("version_number")

                non_compliant_files.append({
                    "name": doc_file.name,
                    "issues": issues
                })

        return {
            "status": "checked",
            "compliant_count": len(compliant_files),
            "non_compliant_count": len(non_compliant_files),
            "compliant_files": compliant_files,
            "non_compliant_files": non_compliant_files
        }

    def _should_trigger_validation_skill(self, task_info: Dict[str, Any], changes: Dict[str, Any]) -> bool:
        """判断是否应该触发validation-documentation技能"""
        # 检查技能文件是否存在
        if not self.skill_path.exists():
            print("[WARN] validation-documentation技能不存在，跳过")
            return False

        # 检查任务是否需要validation文档
        if not self._needs_validation_docs(task_info):
            return False

        # 检查是否有代码变更或文档需求
        has_changes = changes.get('modified_files') or changes.get('new_files')

        if has_changes:
            print("[OK] 检测到变更且需要validation文档，将触发validation-documentation技能")
            return True

        # 检查是否是纯验证任务
        task_type = task_info.get('type', '').lower()
        if task_type in ['validation', 'testing', 'verification']:
            print("[OK] 纯验证任务，将触发validation-documentation技能")
            return True

        return False

    def _trigger_validation_documentation_skill(self, task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
        """触发validation-documentation技能"""
        print("[DOC] 使用validation-documentation技能生成标准化文档...")
        print(f"[TARGET] 技能文档: {self.skill_path}")
        print(f"[TASK] 任务信息: {task_info.get('name', 'unknown')}")
        print()
        print("=== validation-documentation技能核心原则 ===")
        print("技能名称: validation-documentation")
        print("技能描述: Generate standardized validation reports with consistent naming and formatting")
        print()
        print("[PRINCIPLES] 核心原则:")
        print("1. 固定命名（覆盖模式）- 使用版本无关的固定名称")
        print("2. 版本信息在内容中 - 从不在文件名中包含版本号")
        print("3. 标准化文档结构 - 统一的模板和元数据")
        print("4. 累积式更新 - 多模块测试时合并结果而非覆盖")
        print()
        print("[FILES] 标准文件名:")
        print("   - final_report.md")
        print("   - unit_test_status.md")
        print("   - integration_test_status.md")
        print("   - system_infrastructure_analysis.md")
        print("   - test_data_quality.md")
        print("   - progress_report.md")
        print("   - planned_features.md")
        print()
        print("[AVOID] 避免的命名模式:")
        print("   - 版本特定: V6_unimplemented_features.md")
        print("   - 日期基础: progress_report_2026-06-04.md")
        print("   - 编号版本: final_report_v2.md")
        print()
        print("[TEMPLATE] 文档结构模板:")
        print("```markdown")
        print("# [Report Title]")
        print()
        print("**Generated**: [Date]")
        print("**Status**: [COMPLETE/IN_PROGRESS]")
        print("**Change**: [Change Name]")
        print("**Task**: [Task ID] - [Task Description]")
        print()
        print("---")
        print()
        print("## Executive Summary")
        print("[Brief overview]")
        print()
        print("## Detailed Analysis")
        print("[Comprehensive analysis]")
        print()
        print("## Conclusions & Recommendations")
        print("[Key findings]")
        print("```")
        print()
        print("[ACTION] 请按照技能文档执行:")
        print("1. 识别文档类型")
        print("2. 选择标准名称")
        print("3. 检查现有文档（如果存在）")
        print("4. 使用累积式更新（合并而非覆盖）")
        print("5. 保存到docs/validation/（覆盖模式）")
        print()
        print("[IMPORTANT] 多模块测试处理:")
        print("- 模块1测试完成 → 读取现有文档 → 追加模块1结果")
        print("- 模块2测试完成 → 读取现有文档 → 追加模块2结果")
        print("- 模块3测试完成 → 读取现有文档 → 追加模块3结果")
        print("- 最终生成综合报告，包含所有模块结果")
        print()

        # 分析当前任务应该生成什么类型的文档
        doc_type = self._suggest_document_type(task_info, changes)

        if doc_type:
            print(f"[SUGGEST] 建议文档类型: {doc_type['name']}")
            print(f"[FILENAME] 建议文件名: docs/validation/{doc_type['filename']}")
            print(f"[CONTENT] 建议内容: {doc_type['description']}")

            # 检查文档是否已存在
            doc_path = self.validation_dir / doc_type['filename']
            if doc_path.exists():
                print(f"[INFO] 文档已存在: {doc_type['filename']}")
                print(f"[ACTION] 请使用累积式更新，读取并合并现有内容")
                print(f"[INFO] 现有文档大小: {doc_path.stat().st_size} bytes")
                print(f"[INFO] 现有文档修改时间: {datetime.fromtimestamp(doc_path.stat().st_mtime).strftime('%Y-%m-%d %H:%M:%S')}")

        # 返回建议结果
        return {
            "status": "skill_triggered",
            "action_required": True,
            "skill_name": "validation-documentation",
            "task_info": task_info,
            "suggested_document": doc_type,
            "update_mode": "cumulative",  # 新增：指定累积式更新模式
            "message": "已触发validation-documentation技能，请使用累积式更新生成标准化验证报告"
        }

    def _suggest_document_type(self, task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, str]:
        """根据任务信息建议文档类型"""
        task_name = task_info.get('name', '').lower()
        task_desc = task_info.get('description', '').lower()

        # 基于任务内容建议文档类型
        if 'unit test' in task_name or 'unit test' in task_desc:
            return {
                "name": "Unit Test Status",
                "filename": "unit_test_status.md",
                "description": "单元测试状态分析"
            }
        elif 'integration test' in task_name or 'integration' in task_desc:
            return {
                "name": "Integration Test Status",
                "filename": "integration_test_status.md",
                "description": "集成测试状态分析"
            }
        elif 'infrastructure' in task_name or 'infrastructure' in task_desc:
            return {
                "name": "System Infrastructure Analysis",
                "filename": "system_infrastructure_analysis.md",
                "description": "系统基础设施分析"
            }
        elif 'test data' in task_name or 'fixture' in task_desc or 'data quality' in task_desc:
            return {
                "name": "Test Data Quality",
                "filename": "test_data_quality.md",
                "description": "测试数据质量分析"
            }
        elif 'final' in task_name or 'complete' in task_desc or 'summary' in task_desc:
            return {
                "name": "Final Report",
                "filename": "final_report.md",
                "description": "最终验证报告"
            }
        elif 'progress' in task_name or 'status' in task_desc:
            return {
                "name": "Progress Report",
                "filename": "progress_report.md",
                "description": "当前进度报告"
            }
        elif 'planned' in task_name or 'roadmap' in task_desc or 'future' in task_desc:
            return {
                "name": "Planned Features",
                "filename": "planned_features.md",
                "description": "计划功能/路线图"
            }
        else:
            # 默认建议
            return {
                "name": "Progress Report",
                "filename": "progress_report.md",
                "description": "通用进度报告"
            }


# ============================================================================
# Hook函数接口（供OpenSpec工作流调用）
# ============================================================================

# 全局hook实例
_validation_documentation_hook = None

def pre_task_hook(task_info: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行前Hook - Validation文档需求检查"""
    global _validation_documentation_hook

    if _validation_documentation_hook is None:
        _validation_documentation_hook = ValidationDocumentationHook()

    return _validation_documentation_hook.pre_task_hook(task_info)

def post_task_hook(task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
    """任务执行后Hook - Validation文档生成检查"""
    global _validation_documentation_hook

    if _validation_documentation_hook is None:
        _validation_documentation_hook = ValidationDocumentationHook()

    return _validation_documentation_hook.post_task_hook(task_info, changes)


# ============================================================================
# 直接调用接口（供其他脚本使用）
# ============================================================================

def check_validation_documentation_after_task(task_info: Dict[str, Any], changes: Dict[str, Any]) -> Dict[str, Any]:
    """在任务完成后检查validation文档"""
    hook = ValidationDocumentationHook()
    return hook.post_task_hook(task_info, changes)

def main():
    """主函数 - 用于测试hook功能"""
    print("[TEST] ValidationDocumentationHook 功能测试")
    print("=" * 70)

    hook = ValidationDocumentationHook()

    # 测试前置hook
    print("\n=== 测试前置Hook ===")
    task_info = {
        "name": "implementation_validation",
        "description": "Validate implementation completeness",
        "change": "implementation-validation"
    }
    baseline = hook.pre_task_hook(task_info)
    print(f"基线结果: {baseline}")

    # 测试后置hook
    print("\n=== 测试后置Hook ===")
    changes = {"modified_files": ["tests/v6/test_simulation.py"]}
    result = hook.post_task_hook(task_info, changes)
    print(f"后置结果: {result}")

    print("\n" + "=" * 70)
    print("[OK] ValidationDocumentationHook 测试完成")


if __name__ == "__main__":
    main()