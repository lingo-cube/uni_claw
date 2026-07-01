---
name: test-extraction
description: 从设计文档自动提取测试场景并生成测试代码
---

# Test Extraction Skill

从设计文档系统化提取测试场景的完整流程。

## 什么时候使用

- ✅ 需要为新模块生成测试场景
- ✅ 需要提高模块测试覆盖率
- ✅ 设计文档更新后需要更新测试
- ✅ 代码审查前验证测试完整性

## 前置条件

1. 设计文档必须存在于 `docs/architecture/modules/{module}-design.md`
2. 设计文档必须包含必需章节（参考 module-design-template.md）

## 执行流程

### Step 1: 定位设计文档

查找并读取设计文档：
```
docs/architecture/modules/{module}-design.md
```

如果设计文档不存在，提示用户先创建设计文档。

### Step 2: 应用5步方法论

使用 [TEST_EXTRACTION_METHODOLOGY.md](../../docs/testing/TEST_EXTRACTION_METHODOLOGY.md) 的5步流程：

1. **定位设计文档** - 确认文档存在
2. **识别测试维度** - 提取 States, Transitions, Boundaries, Errors, Features
3. **创建测试矩阵** - 为每个维度创建场景表
4. **分类测试** - normal, edge, errors, integration
5. **估算覆盖率** - 计算预期覆盖率

### Step 3: 生成测试场景文档

输出到：`docs/testing/{MODULE}_TEST_SCENARIOS.md`

文档应包含：
- Step 1-5 的完整分析
- 所有测试维度矩阵
- 测试文件结构
- 示例测试实现
- 覆盖率估算

### Step 4: 验证完整性

使用检查清单验证场景完整性：
- 所有枚举值都有测试
- 所有边界值都有测试
- 所有错误类型都有测试
- 集成场景覆盖主要流程

### Step 5: 提供下一步指导

告诉用户如何：
- 根据场景实现测试代码
- 运行测试验证覆盖率
- 集成到开发流程

## 支持的模块

当前已有测试场景文档的模块：
- ✅ graph (205+ scenarios)
- ✅ state-machine (112+ scenarios)

待提取的模块：
- ⏳ traversal
- ⏳ exception
- ⏳ adb
- ⏳ config
- ⏳ analysis
- ⏳ safety
- ⏳ ai
- ⏳ simulation

## 使用示例

### 为新模块提取测试场景

```markdown
/test-extraction traversal
```

将：
1. 读取 `docs/architecture/modules/traversal-design.md`
2. 应用5步方法论分析
3. 提取所有测试维度
4. 生成 `docs/testing/TRAVERSAL_TEST_SCENARIOS.md`
5. 估算 150-200 测试场景
6. 提供实现指导

### 验证已有模块的测试场景

```markdown
/test-extraction graph --verify
```

验证现有的 `GRAPH_TEST_SCENARIOS.md` 是否完整。

## 输出格式

生成的测试场景文档应遵循 [GRAPH_TEST_SCENARIOS.md](../../docs/testing/GRAPH_TEST_SCENARIOS.md) 的格式：

```markdown
# {Module} Test Scenarios

## Step 1: Design Document Located
## Step 2: Test Dimensions Identified
## Step 3: Test Scenario Matrix
### 3.1 Category Tests
### 3.2 Category Tests
...
## Step 4: Test Categories
## Step 5: Coverage Estimation
## Example Test Implementation
## Key Takeaways
```

## 相关文档

- **方法论**: [TEST_EXTRACTION_METHODOLOGY.md](../../docs/testing/TEST_EXTRACTION_METHODOLOGY.md)
- **示例**: [GRAPH_TEST_SCENARIOS.md](../../docs/testing/GRAPH_TEST_SCENARIOS.md)
- **标准**: [STANDARDS.md](../../docs/testing/STANDARDS.md)
- **固化方案**: [TEST_EXTRACTION_SOLIDIFICATION.md](../../docs/testing/TEST_EXTRACTION_SOLIDIFICATION.md)

## 快速命令

```bash
# 使用脚本提取（快速估算）
python scripts/extract_test_scenarios.py {module}

# 使用 skill 完整提取
/skill test-extraction {module}

# 验证覆盖率
python scripts/validate_test_coverage.py {module}
```
