# 模块单元测试Hook集成说明

## 概述

本文档说明如何将`module-test`技能集成到OpenSpec工作流中，确保代码变更时测试完整性得到保障。

## 核心组件

### 1. 技能文件
- **位置**: `.claude/skills/module-test/SKILL.md`
- **用途**: 定义模块单元测试执行的完整流程和最佳实践
- **触发**: 在代码变更完成后自动触发

### 2. Hook文件
- **位置**: `openspec/hooks/module_test_hook.py`
- **用途**: 在OpenSpec工作流中集成测试检查
- **功能**: 识别变更、触发技能、记录结果

### 3. 配置文件
- **位置**: `.test-config.yaml`
- **用途**: 自定义测试行为和参数
- **可选**: 不存在时使用默认配置

### 4. 决策日志
- **位置**: `.test_fix_log.md`
- **用途**: 记录测试失败的分析过程和处理决策
- **维护**: 自动生成，可手动补充

## 集成方式

### 方式1: 自动集成（推荐）

在`openspec/hooks/`目录中已创建`module_test_hook.py`，OpenSpec工作流会自动调用。

**工作流程**:
```
OpenSpec任务开始
    ↓
pre_task_hook - 捕获测试基线
    ↓
执行代码变更
    ↓
post_task_hook - 验证测试完整性
    ↓
触发module-test技能
    ↓
按照技能文档执行测试
    ↓
记录决策到.test_fix_log.md
    ↓
任务完成
```

### 方式2: 手动触发

在执行OpenSpec任务后，手动调用hook:

```bash
# 使用Python调用
python -c "
from openspec.hooks.module_test_hook import check_module_tests_after_change
result = check_module_tests_after_change()
print(result)
"
```

### 方式3: 技能调用

直接调用module-test技能:

```bash
# 如果使用Claude Code
/module-test

# 如果使用其他AI助手
# 读取.claude/skills/module-test/SKILL.md并按照文档执行
```

## Hook函数接口

### pre_task_hook

**参数**:
- `task_info`: 任务信息字典
  - `name`: 任务名称
  - `description`: 任务描述
  - `files`: 相关文件列表（可选）

**返回**:
```python
{
    "status": "baseline_captured",
    "modules": ["graph", "models"],
    "baseline": {
        "graph": {...},
        "models": {...}
    }
}
```

### post_task_hook

**参数**:
- `task_info`: 任务信息字典
- `changes`: 变更信息字典
  - `modified_files`: 修改的文件列表
  - `added_files`: 新增的文件列表
  - `deleted_files`: 删除的文件列表

**返回**:
```python
{
    "status": "skill_triggered",
    "action_required": True,
    "skill_name": "module-test",
    "modules": ["graph"],
    "message": "已触发module-test技能"
}
```

## 使用示例

### 示例1: 图模块变更后的测试

```bash
# 1. 完成代码变更
git add src/graph/node.py
git commit -m "fix: 修复节点深度计算逻辑"

# 2. OpenSpec工作流自动触发post_task_hook
# 3. Hook自动调用module-test技能
# 4. 按照技能文档执行测试
python -m pytest src/graph/test/ -v --tb=short

# 5. 检查结果
cat src/graph/test/test_report.json
```

### 示例2: 多模块变更的测试

```bash
# 1. 变更涉及多个模块
git diff --name-only
# 输出:
# src/graph/node.py
# src/models/context.py

# 2. Hook自动识别依赖关系
# graph模块依赖models模块
# traversal模块依赖graph模块

# 3. 自动测试所有相关模块
python -m pytest src/graph/test/ -v
python -m pytest src/models/test/ -v
python -m pytest src/traversal/test/ -v

# 4. 检查覆盖率
python -m pytest src/graph/test/ --cov=src.graph --cov-report=term
```

### 示例3: 测试失败的处理

```bash
# 1. 测试失败
python -m pytest src/graph/test/ -v
# 输出: 39 passed, 1 failed

# 2. 按照module-test技能的优先级处理
# Level 0: 检查环境问题
# Level 1: 辅助分析代码实现
# Level 2: 检查设计文档
# Level 3: 询问用户意见
# Level 4: 谨慎修改测试用例

# 3. 记录决策过程
# 自动记录到.test_fix_log.md
```

## 配置选项

### .test-config.yaml

可以创建项目根目录的`.test-config.yaml`来自定义测试行为:

```yaml
# 测试框架选择
test_runner: auto  # auto/pytest/unittest/tox

# 覆盖率要求
coverage:
  enabled: true
  threshold: 80

# 并行测试
parallel:
  enabled: true
  workers: "auto"

# 模块依赖关系
dependencies:
  - "src/utils -> src/graph"
  - "src/models -> src/ai"
```

## 故障排除

### Hook未触发

**症状**: 代码变更后没有自动触发测试检查

**解决**:
1. 检查`openspec/hooks/module_test_hook.py`是否存在
2. 检查文件权限是否正确
3. 手动运行测试: `python openspec/hooks/module_test_hook.py`

### 技能文档未找到

**症状**: 提示"module-test技能不存在"

**解决**:
1. 检查`.claude/skills/module-test/SKILL.md`是否存在
2. 如果不存在，创建技能文件或使用默认测试流程

### 测试路径识别失败

**症状**: 提示"未找到模块的测试路径"

**解决**:
1. 检查是否有`src/{module}/test/`目录
2. 检查是否有`tests/{module}/`目录
3. 创建`.test-config.yaml`指定自定义路径

## 最佳实践

1. **定期更新测试基线**: 在重要变更前后运行完整测试套件
2. **保持配置同步**: 确保`.test-config.yaml`反映项目结构
3. **审查决策日志**: 定期查看`.test_fix_log.md`了解历史问题
4. **维护依赖关系**: 更新模块依赖关系配置
5. **监控覆盖率趋势**: 确保覆盖率不随时间下降

## 扩展和定制

### 添加自定义测试命令

在`.test-config.yaml`中配置:

```yaml
custom_command:
  setup: "npm install"      # 前置命令
  test: "npm test"           # 测试命令
  teardown: "npm run clean"  # 后置清理
```

### 集成CI/CD

在CI/CD流水线中:

```yaml
# .github/workflows/test.yml
- name: Checkout code
  uses: actions/checkout@v2

- name: Run module tests
  run: |
    python openspec/hooks/module_test_hook.py
    # 或直接调用技能
    /module-test
```

---

**最后更新**: 2024-06-04  
**相关文档**: `.claude/skills/module-test/SKILL.md`, `.test-config.yaml`  
**维护者**: dev-team