# 测试场景提取流程固化方案

> **目的**: 将测试场景提取方法论系统化、自动化、集成到开发流程
> **创建**: 2026-06-08

---

## 固化目标

将 **5步测试提取流程** 从手动实践固化为项目标准流程：

| 维度 | 当前状态 | 目标状态 |
|------|----------|----------|
| **文档化** | ✅ 已有方法论指南 | 维护和更新 |
| **工具化** | ❌ 手动执行 | 自动化脚本 |
| **流程集成** | ❌ 独立实践 | 集成到开发流程 |
| **技能化** | ❌ 需要人工操作 | Skill/Workflow |
| **标准化** | ❌ 一次性实践 | 每个模块标准流程 |

---

## 固化方案

### 1. 自动化脚本

#### 1.1 测试场景提取器

**文件**: `scripts/extract_test_scenarios.py`

```python
#!/usr/bin/env python3
"""
自动从设计文档提取测试场景
Usage: python scripts/extract_test_scenarios.py <module_name>
"""

import sys
from pathlib import Path

def extract_scenarios(module_name: str):
    """
    从设计文档提取测试场景

    Args:
        module_name: 模块名称 (graph, state_machine, traversal, etc.)
    """
    design_doc = Path(f"docs/architecture/modules/{module_name}-design.md")

    if not design_doc.exists():
        print(f"❌ 设计文档不存在: {design_doc}")
        return 1

    # 调用AI分析设计文档
    scenarios = analyze_design_document(design_doc)

    # 生成测试场景文档
    output = Path(f"docs/testing/{module_name.upper()}_TEST_SCENARIOS.md")
    generate_scenarios_document(scenarios, output)

    print(f"✅ 已生成测试场景文档: {output}")
    return 0

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: extract_test_scenarios.py <module_name>")
        sys.exit(1)
    sys.exit(extract_scenarios(sys.argv[1]))
```

#### 1.2 测试场景验证器

**文件**: `scripts/validate_test_coverage.py`

```python
#!/usr/bin/env python3
"""
验证测试场景覆盖是否完整
Usage: python scripts/validate_test_coverage.py <module_name>
"""

import sys
from pathlib import Path

def validate_coverage(module_name: str):
    """
    验证模块的测试覆盖是否完整
    """
    scenarios_doc = Path(f"docs/testing/{module_name.upper()}_TEST_SCENARIOS.md")
    test_dir = Path(f"tests/{module_name}/")

    if not scenarios_doc.exists():
        print(f"❌ 测试场景文档不存在")
        return 1

    # 读取测试场景
    scenarios = parse_scenarios(scenarios_doc)

    # 检查测试实现
    implemented = check_implemented_tests(test_dir)

    # 对比和报告
    report_coverage(scenarios, implemented)

    return 0

if __name__ == "__main__":
    sys.exit(validate_coverage(sys.argv[1]))
```

### 2. Skill 集成

#### 2.1 test-extraction Skill

**文件**: `.claude/skills/test-extraction/skill.md`

```markdown
---
name: test-extraction
description: 从设计文档自动提取测试场景并生成测试代码
---

# Test Extraction Skill

从设计文档系统化提取测试场景并生成测试代码。

## 什么时候使用

- 需要为新模块生成测试场景
- 需要提高模块测试覆盖率
- 设计文档更新后需要更新测试

## 流程

1. **读取设计文档**: 读取 `docs/architecture/modules/{module}-design.md`
2. **提取测试维度**: 识别 States, Transitions, Boundaries, Errors, Features
3. **生成测试矩阵**: 创建完整的测试场景表
4. **估算覆盖率**: 计算预期测试覆盖率
5. **生成文档**: 输出到 `docs/testing/{MODULE}_TEST_SCENARIOS.md`

## 示例

```
/test-extraction graph
```

将:
1. 读取 `docs/architecture/modules/graph-design.md`
2. 提取 200+ 测试场景
3. 生成 `docs/testing/GRAPH_TEST_SCENARIOS.md`
4. 估算 95%+ 覆盖率
```

### 3. Workflow 集成

**文件**: `.claude/workflows/test-extraction-workflow.js`

```javascript
/**
 * Test Extraction Workflow
 *
 * 自动化测试场景提取流程
 */

export const meta = {
  name: 'test-extraction',
  description: '从设计文档提取测试场景并生成测试',
  phases: [
    { title: 'Analyze', detail: '分析设计文档' },
    { title: 'Extract', detail: '提取测试维度' },
    { title: 'Generate', detail: '生成测试场景' },
    { title: 'Estimate', detail: '估算覆盖率' },
    { title: 'Document', detail: '生成文档' }
  ]
};

async function run() {
  const moduleName = args?.[0] || 'graph';

  // Phase 1: 分析设计文档
  phase('Analyze');
  const designDoc = await agent(`
    读取并分析设计文档: docs/architecture/modules/${moduleName}-design.md

    识别:
    1. 所有数据模型类
    2. 所有枚举类型及其值
    3. 所有操作/方法
    4. 所有边界/限制值
    5. 所有错误类型/策略
    6. 所有功能特性

    返回结构化分析结果。
  `, { label: '分析设计文档' });

  // Phase 2-5: 继续执行...
  // [完整 workflow 实现]

  return { success: true, scenarios: count };
}

return await run();
```

### 4. 集成到开发流程

#### 4.1 模块开发标准流程

将测试场景提取集成到模块开发的必需步骤：

```
1. 设计阶段
   ├── 编写设计文档 (docs/architecture/modules/{module}-design.md)
   └── 评审设计文档

2. 测试设计阶段 ⭐ 新增
   ├── 运行 /test-extraction {module}
   ├── 生成 docs/testing/{MODULE}_TEST_SCENARIOS.md
   └── 评审测试场景覆盖

3. 实现阶段
   ├── 根据测试场景实现测试代码
   └── 实现功能代码

4. 验证阶段
   ├── 运行 /skill module-test
   └── 检查覆盖率达标

5. 完成阶段
   ├── 测试全部通过
   └── 覆盖率达到标准
```

#### 4.2 PRD 模板更新

在 `CLAUDE_WORKFLOW.md` 中添加测试场景提取步骤：

```markdown
## 模块开发流程

1. ✏️ 编写设计文档
2. 🧪 提取测试场景 (/test-extraction)
3. 💻 实现测试和代码
4. ✅ 验证覆盖率 (/skill module-test)
5. 📦 完成开发
```

#### 4.3 质量门禁

在 `docs/testing/STANDARDS.md` 中添加测试场景要求：

```markdown
## 测试场景要求

### 新模块开发

- [ ] 必须先编写设计文档
- [ ] 必须运行 /test-extraction 生成测试场景
- [ ] 测试场景文档必须存在于 docs/testing/
- [ ] 预期覆盖率必须达到 95%+

### 现有模块增强

- [ ] 设计变更后重新提取测试场景
- [ ] 新功能必须补充测试场景
- [ ] 覆盖率不得下降
```

### 5. 模板和检查清单

#### 5.1 设计文档模板

**文件**: `docs/architecture/templates/module-design-template.md`

```markdown
# {Module} Design Document

> **模板**: 使用此模板创建新模块设计文档

## 必需章节

### 1. Module Overview
- Purpose
- Key Responsibilities
- Module Structure

### 2. Core Abstractions
- 所有数据类定义
- 所有枚举类型定义

### 3. Data Model
- 所有数据结构
- JSON Schema (如适用)

### 4. Operations/Methods
- 所有公开方法
- API 签名

### 5. Configuration/Limits
- 所有配置参数
- 边界值定义

### 6. Error Handling
- 所有错误类型
- 错误策略

### 7. Usage Examples
- 至少3个使用示例
- 覆盖主要功能

## 附录

- Enum Values (所有枚举值列表)
- Validation Rules (所有验证规则)
```

#### 5.2 测试场景检查清单

**文件**: `docs/testing/TEST_SCENARIO_CHECKLIST.md`

```markdown
# 测试场景完整性检查清单

使用此清单确保测试场景文档完整。

## 数据模型覆盖

- [ ] 所有数据类都有测试场景
- [ ] 所有必填字段都有验证测试
- [ ] 所有可选字段都有测试
- [ ] 所有字段类型都有验证测试

## 枚举覆盖

- [ ] 每个枚举类型都有完整测试
- [ ] 每个枚举值至少1个测试
- [ ] 枚举组合有测试（如适用）

## 操作覆盖

- [ ] 所有公开方法都有测试
- [ ] 所有参数组合都有测试
- [ ] 返回值有验证测试

## 边界覆盖

- [ ] 最小边界值有测试
- [ ] 最大边界值有测试
- [ ] 空值/None有测试
- [ ] 超出范围有测试

## 错误覆盖

- [ ] 每种错误类型都有测试
- [ ] 错误恢复有测试
- [ ] 错误传播有测试

## 集成覆盖

- [ ] 主要集成流程有测试
- [ ] 与依赖模块有集成测试
```

---

## 实施步骤

### Phase 1: 工具准备 (Week 1)

- [ ] 创建 `scripts/extract_test_scenarios.py`
- [ ] 创建 `scripts/validate_test_coverage.py`
- [ ] 创建 test-extraction skill
- [ ] 创建 test-extraction workflow

### Phase 2: 文档更新 (Week 1)

- [ ] 更新 `CLAUDE_WORKFLOW.md` - 添加测试提取步骤
- [ ] 更新 `docs/testing/STANDARDS.md` - 添加测试场景要求
- [ ] 创建设计文档模板
- [ ] 创建测试场景检查清单

### Phase 3: 应用到现有模块 (Week 2-4)

- [ ] Graph 模块 ✅ 已完成
- [ ] State Machine 模块 ✅ 已完成
- [ ] Traversal 模块
- [ ] Exception 模块
- [ ] ADB 模块
- [ ] Config 模块
- [ ] Analysis 模块
- [ ] Safety 模块

### Phase 4: 持续维护 (Ongoing)

- [ ] 新模块自动应用流程
- [ ] 设计变更自动更新测试场景
- [ ] 定期验证覆盖率

---

## 成功指标

| 指标 | 当前 | 目标 |
|------|------|------|
| **有测试场景文档的模块** | 2 | 17 |
| **平均测试覆盖率** | ~60% | 95%+ |
| **测试场景提取自动化率** | 0% | 80%+ |
| **开发流程遵循率** | 0% | 100% |

---

## 使用指南

### 为新模块提取测试场景

```bash
# 1. 确保设计文档存在
ls docs/architecture/modules/{module}-design.md

# 2. 运行测试场景提取
/skill test-extraction {module}

# 3. 查看生成的文档
cat docs/testing/{MODULE}_TEST_SCENARIOS.md

# 4. 根据场景实现测试
pytest tests/{module}/ -v
```

### 验证现有模块测试覆盖

```bash
# 1. 检查测试场景文档是否存在
ls docs/testing/*_TEST_SCENARIOS.md

# 2. 验证覆盖率
python scripts/validate_test_coverage.py {module}

# 3. 生成覆盖率报告
pytest tests/{module}/ --cov=src/{module} --cov-report=html
```

---

**维护者**: Uni-Claw Development Team
**更新频率**: 每季度评审
**相关文档**: TEST_EXTRACTION_METHODOLOGY.md, STANDARDS.md
