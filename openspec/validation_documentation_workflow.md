# OpenSpec Validation Documentation Workflow Guide

**Version**: 1.0  
**Created**: 2026-06-05  
**Purpose**: 集成validation-documentation技能到OpenSpec工作流

---

## 概述

本指南描述如何将`validation-documentation`技能集成到OpenSpec变更管理流程中，确保验证文档的标准化生成和命名。

## 集成架构

### 已集成的组件

1. **ValidationDocumentationHook** (`openspec/hooks/validation_documentation_hook.py`)
   - 自动检测需要生成validation文档的任务
   - 验证现有文档是否符合命名标准
   - 在适当时机触发validation-documentation技能

2. **OpenSpecApplyWithGuardian** (`openspec/hooks/openspec_apply_wrapper.py`)
   - 已集成validation-documentation检查
   - 在任务执行前后自动运行hook
   - 提供统一的检查接口

### 自动触发条件

validation-documentation技能会在以下情况自动触发：

1. **任务类型检测**
   - 任务名称或描述包含关键词：`validation`, `verify`, `test`, `check`, `validate`, `testing`, `verification`, `analysis`, `report`, `status`
   - Change名称包含：`validation`, `testing`, `verification`

2. **文档需求检测**
   - 任务涉及测试结果分析
   - 任务需要状态报告
   - 任务需要基础设施分析

3. **变更检测**
   - 有代码变更且需要validation文档
   - 纯验证类型的任务

## 使用指南

### 在OpenSpec Tasks中引用

#### 方法1: 任务描述模板

在`tasks.md`中创建需要生成validation文档的任务时，使用以下模板：

```markdown
### Task X.Y: Generate [Document Type]

**Description**: [What to document and why]

**Output**: `docs/validation/[standard_name].md` (overwrite mode)

**Validation Guidance**: 
- Follow `validation-documentation` skill naming standards
- Use comprehensive report approach (project-level, not module-level)
- Include timestamped sections for multiple test runs
- Reference: `.claude/skills/validation-documentation/SKILL.md`
```

#### 示例任务模板

**单元测试状态任务**:
```markdown
### Task 2.3: Verify Unit Test Suite Completeness

**Description**: Ensure unit test coverage is complete for critical components.

**Output**: `docs/validation/unit_test_status.md` (overwrite mode)

**Validation Guidance**: 
- Use validation-documentation skill for standardized formatting
- Generate comprehensive project-level report
- Include all module test results in single document
- Add timestamped sections for test history
```

**集成测试状态任务**:
```markdown
### Task 3.1: Run Integration Tests

**Description**: Run integration tests and document results.

**Output**: `docs/validation/integration_test_status.md`

**Validation Guidance**: 
- Follow validation-documentation skill standards
- Document both passing and failing tests
- Categorize failures by root cause
- Provide actionable recommendations
```

### 方法2: 任务完成检查清单

在任务定义的末尾添加完成标准：

```markdown
**Acceptance Criteria**:
- ✅ Tests executed and results documented
- ✅ Report uses validation-documentation skill standards
- ✅ Document follows overwrite-mode naming (no dates/versions in filename)
- ✅ Content includes proper metadata header
- ✅ Comprehensive analysis provided
- ✅ Git committed with clear message
```

## 标准文档类型映射

| 任务类型 | 标准文档名 | 内容要求 |
|---------|-----------|----------|
| 单元测试分析 | `unit_test_status.md` | 所有模块单元测试结果、通过率、覆盖率 |
| 集成测试结果 | `integration_test_status.md` | 集成测试状态、失败分类、根因分析 |
| 基础设施分析 | `system_infrastructure_analysis.md` | 系统组件分析、架构验证、质量评估 |
| 测试数据质量 | `test_data_quality.md` | Fixtures验证、数据完整性、覆盖率 |
| 最终验证报告 | `final_report.md` | 综合验证总结、生产就绪评估、建议 |
| 当前进度 | `progress_report.md` | 当前状态、阻塞问题、下一步计划 |
| 未来路线图 | `planned_features.md` | 待实现功能、优先级、时间估算 |

## 工作流集成

### 完整的OpenSpec变更工作流

```bash
# 1. 创建变更
/opsx:propose

# 2. 在tasks.md中定义任务（包含validation文档任务）
# 编辑 openspec/changes/[change-name]/tasks.md
# 添加validation文档生成任务，使用上述模板

# 3. 应用变更（自动触发validation-documentation hook）
/opsx:apply

# Hook自动执行：
# - pre_task_hook: 检查validation文档需求
# - [执行任务]
# - post_task_hook: 触发validation-documentation技能

# 4. 按照技能指引生成文档
# - 读取.claude/skills/validation-documentation/SKILL.md
# - 选择合适的标准文档名
# - 生成内容（使用综合报告方法）
# - 保存到docs/validation/（覆盖模式）
```

### 手动触发validation-documentation技能

如果需要手动生成validation文档：

```bash
# 1. 直接调用技能
Skill tool: validation-documentation

# 2. 或调用hook
python -c "from openspec.hooks.validation_documentation_hook import check_validation_documentation_after_task; ..."
```

## 文档内容指南

### 综合报告方法 (Project-Level)

处理多模块测试时，使用project-level综合报告：

**推荐结构**:
```markdown
# Unit Test Status

**Generated**: 2026-06-05 14:30  
**Status**: COMPLETE  
**Change**: implementation-validation  
**Task**: 2.3 - Verify Unit Test Suite Completeness

---

## Executive Summary
- Total tests: 84/84 passing (100%)
- Simulation module: 33/33 passing
- State machine module: 20/20 passing
- Graph engine module: 31/31 passing

## Latest Test Run (2026-06-05 14:30)
### Simulation Module Tests
- test_mock_vision_service: 7/7 passing
- test_mock_action_executor: 8/8 passing
- test_in_memory_tracer: 7/7 passing
- test_simulation_runner: 11/11 passing

### State Machine Tests
- test_state_initialization: 5/5 passing
- test_state_transitions: 8/8 passing
- test_error_handling: 7/7 passing

### Graph Engine Tests
- test_engine_creation: 6/6 passing
- test_main_loop: 10/10 passing
- test_completion_policies: 15/15 passing

## Historical Comparison
| Date | Total Passing | Simulation | State Machine | Graph Engine |
|------|--------------|-----------|---------------|--------------|
| 2026-06-05 | 84/84 (100%) | 33/33 | 20/20 | 31/31 |
| 2026-06-04 | 81/84 (96%) | 30/33 | 20/20 | 31/31 |
| 2026-06-03 | 75/84 (89%) | 28/33 | 18/20 | 29/31 |

## Conclusions & Recommendations
- All critical unit tests passing at 100%
- Test coverage comprehensive across all modules
- Production readiness confirmed
```

## Hook配置

### 启用/禁用validation-documentation hook

在`openspec_apply_wrapper.py`中：

```python
# 自动检测并启用
VALIDATION_DOCUMENTATION_AVAILABLE = True  # 启用
VALIDATION_DOCUMENTATION_AVAILABLE = False  # 禁用
```

### Hook执行流程

```python
# 创建guardian（自动包含validation-documentation）
guardian = OpenSpecApplyWithGuardian()

# 前置检查（自动运行）
baseline = guardian.pre_check(task_info)
# 包含: validation_pre_hook()

# 执行任务
execute_tasks(change_name)

# 后置检查（自动运行）
result = guardian.post_check(task_info, changes)
# 包含: validation_post_hook()
# 如果需要validation文档，会自动触发技能
```

## 验证和测试

### 测试hook安装

```bash
# 测试validation-documentation hook
python openspec/hooks/validation_documentation_hook.py

# 测试完整wrapper
python -c "from openspec.hooks.openspec_apply_wrapper import OpenSpecApplyWithGuardian; guardian = OpenSpecApplyWithGuardian(); print('Validation enabled:', guardian.validation_documentation_enabled)"
```

### 验证文档命名标准

```bash
# 运行验证脚本
python scripts/standardize_validation_docs.py verify

# 预期输出: All files compliant
```

## 故障排除

### Hook未触发

**症状**: validation-documentation hook没有自动触发

**解决方案**:
1. 检查任务描述是否包含关键词
2. 验证hook是否正确导入
3. 检查`VALIDATION_DOCUMENTATION_AVAILABLE`状态

### 文档命名不符合标准

**症状**: 生成的文档名包含日期或版本号

**解决方案**:
1. 参考validation-documentation skill的标准名称映射
2. 使用覆盖式命名（无日期、版本）
3. 运行`python scripts/standardize_validation_docs.py verify`

### 多模块测试覆盖问题

**症状**: 后面的测试结果覆盖前面的结果

**解决方案**:
1. 使用project-level综合报告方法
2. 在文档内添加时间戳分隔不同运行
3. 合并所有模块结果到单个文档
4. 使用历史对比表格展示趋势

## 最佳实践

### DO ✅

1. **使用project-level综合报告**: 将所有模块结果合并到一个文档
2. **添加历史对比**: 在文档中展示多个时间点的结果
3. **遵循标准命名**: 使用固定的标准文档名，不包含日期/版本
4. **包含完整元数据**: 在文档头部添加项目、版本、日期信息
5. **Git追踪变更**: 依赖Git历史而不是文件名版本控制

### DON'T ❌

1. **不要创建module-level文档**: 避免为每个模块创建单独的validation文档
2. **不要使用日期命名**: 避免类似`unit_test_2026-06-05.md`的文件名
3. **不要简单覆盖**: 更新文档时应该读取并合并现有内容
4. **不要忽略历史**: 应该在文档中保留历史数据对比
5. **不要创建版本文件**: 避免类似`report_v2.md`的版本化命名

## 文件结构

```
openspec/
├── hooks/
│   ├── validation_documentation_hook.py    # 核心hook实现
│   ├── openspec_apply_wrapper.py           # 集成wrapper
│   └── [其他hooks]
├── changes/
│   └── [change-name]/
│       └── tasks.md                        # 包含validation文档任务
└── validation_documentation_workflow.md    # 本指南

.claude/skills/
└── validation-documentation/
    └── SKILL.md                            # validation-documentation技能文档

docs/
└── validation/
    ├── final_report.md                    # 标准validation文档
    ├── unit_test_status.md
    ├── integration_test_status.md
    └── [其他标准文档]
```

## 相关文档

- **Skill文档**: `.claude/skills/validation-documentation/SKILL.md`
- **Hook实现**: `openspec/hooks/validation_documentation_hook.py`
- **标准化脚本**: `scripts/standardize_validation_docs.py`
- **OpenSpec文档**: 参考项目OpenSpec使用指南

---

**维护者**: Uni-Claw开发团队  
**最后更新**: 2026-06-05  
**状态**: 生产就绪 ✅