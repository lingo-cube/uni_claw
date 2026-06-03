# Development Workflow

本文档描述 uni-claw 项目的开发工作流程和规范。

## 临时文件管理

### 命名规范

临时开发文件应使用描述性前缀：

- `tmp_` - 通用临时文件
- `debug_` - 调试和诊断脚本
- `exp_` - 实验性代码

### 文件放置

**临时脚本** - 放在 `scripts/tmp/` 目录
```bash
scripts/tmp/debug_component.py
scripts/tmp/experiment_feature.py
```

**临时测试** - 使用 `tmp_test_` 前缀
```bash
tests/tmp_test_new_feature.py
```

### 生命周期

1. **创建时**: 使用正确的前缀和位置
2. **完成时**: 移至正确位置或删除
3. **清理时**: 每周审查 `scripts/tmp/` 目录

## 测试组织

### 单元测试

单元测试（测试单个类/函数）应放在被测试代码的同目录：

```
src/
  ai/
    advisor.py
    test_advisor.py          # 单元测试
  traversal/
    engine.py
    test_engine.py           # 单元测试
```

### 集成测试

集成测试（测试多组件协作）应放在 `tests/` 目录：

```
tests/
  integration/              # 多组件集成测试
  simulation/              # Mock 环境仿真测试
  performance/            # 性能基准测试
```

### 测试运行

```bash
# 运行所有单元测试
pytest src/ -v

# 运行所有集成测试
pytest tests/ -v

# 运行特定模块测试
pytest src/ai/ -v
pytest tests/integration/ -v
```

## 清理流程

### 日常清理

1. **每周五**: 清理 `scripts/tmp/` 目录
2. **每月**: 检查根目录是否有遗漏文件
3. **每季度**: 归档旧的测试报告

### 临时文件处理

**创建时**:
- 使用正确的前缀
- 放在正确位置
- 设置清理提醒

**完成时**:
- 移至正确位置（如 scripts/analysis/）
- 或删除（如调试脚本）

### Git 清理

提交前检查：
```bash
# 检查是否有意外提交的临时文件
git status

# 检查 .gitignore 是否覆盖
git check-ignore -v test_*.md
```

## 脚本组织

### scripts/ 目录结构

```
scripts/
  analysis/        # 数据分析和检查脚本
  debug/          # 调试脚本
  verify/         # 验证脚本
  visualization/  # 报告生成和可视化
  tmp/            # 临时脚本（定期清理）
```

### 脚本命名

- 分析脚本: `analyze_*.py`
- 调试脚本: `debug_*.py`
- 验证脚本: `verify_*.py` 或 `check_*.py`
- 可视化脚本: `show_*.py` 或 `generate_*.py`

## 文档管理

### 文档位置

所有文档应放在 `docs/` 目录：

```
docs/
  ARCHITECTURE.md          # 架构文档
  TESTING_GUIDE.md        # 测试指南
  DEVELOPMENT_WORKFLOW.md # 本文档
  ...
```

### 临时文档

- 测试报告: `tests/reports/`
- 设计草稿: 直接删除，不提交
- 临时笔记: 使用临时文件，完成后删除

## 故障排除

### 测试发现失败

如果 pytest 无法发现测试：

```bash
# 检查配置
cat pyproject.toml | grep pytest

# 手动验证
pytest --collect-only

# 检查文件命名
ls src/**/test_*.py
```

### 导入错误

移动测试后可能出现导入错误：

```bash
# 检查导入路径
python -c "from src.ai.advisor import AIStrategyAdvisor"

# 运行特定测试验证
pytest src/ai/test_advisor.py -v
```

## 相关文档

- [TEST_GUIDE.md](TEST_GUIDE.md) - 测试规范
- [TESTING_WORKFLOWS.md](TESTING_WORKFLOWS.md) - 测试工作流程
- [SIMULATION_TESTING_GUIDE.md](SIMULATION_TESTING_GUIDE.md) - 仿真测试指南
- [ARCHITECTURE.md](ARCHITECTURE.md) - 系统架构
