# 任务驱动的测试生成工作流

> **版本**: V1.0 | **日期**: 2026-06-08
> **核心**: 针对任务需求生成测试，而非针对模块

---

## 任务视角 vs 模块视角

### 模块视角 (原有设计)

```bash
/Workflow multi-agent-test-validation-tiered state_machine
```

**问题**:
- ❌ 与实际开发流程脱节
- ❌ 不清楚测什么（测新增功能？测全部？）
- ❌ 无法针对具体需求

### 任务视角 (正确设计)

```bash
/Workflow multi-agent-test-validation-tiered --task prd-v6-9-1:2.2
# 或
/Workflow multi-agent-test-validation-tiered "实现 factories.py 的 create_minimal_plan()、create_test_node()、create_mock_vision()"
```

**优势**:
- ✅ 直接关联开发任务
- ✅ 明确测什么（针对任务需求）
- ✅ 可验证（测试覆盖任务要求）

---

## 任务格式示例

### 从 tasks.md 解析

```markdown
## 2. 共享辅助模块 - 第一批

- [ ] 2.2 实现 factories.py - create_minimal_plan()、create_test_node()、create_mock_vision()
- [ ] 2.3 实现 state_inspector.py - verify_stack_consistency()、verify_cache_coherency()
- [ ] 2.4 实现 trace_analyzer.py - build_tree()、extract_operations()
```

### 输入格式

```bash
# 格式1: 任务ID
/Workflow multi-agent-test-validation-tiered --change prd-v6-9-1 --task 2.2

# 格式2: 任务描述
/Workflow multi-agent-test-validation-tiered "实现 factories.py 的 create_minimal_plan()、create_test_node()、create_mock_vision()"

# 格式3: 从文件读取
/Workflow multi-agent-test-validation-tiered --tasks-file openspec/changes/prd-v6-9-1-test-refactor/tasks.md --task 2.2
```

---

## Workflow 输入解析

### Phase 0: 任务解析

```
┌─────────────────────────────────────────┐
│  输入: --task 2.2                        │
├─────────────────────────────────────────┤
│  解析:                                   │
│    - 任务ID: 2.2                         │
│    - 涉及模块: helpers/factories.py      │
│    - 实现函数: create_minimal_plan()     │
│                create_test_node()        │
│                create_mock_vision()      │
│    - 相关模块: graph (TraversalPlan)     │
│                  simulation (MockVision)  │
└─────────────────────────────────────────┘
```

### 任务信息结构

```json
{
  "task_id": "2.2",
  "change": "prd-v6-9-1-test-refactor",
  "title": "实现 factories.py",
  "requirements": [
    "create_minimal_plan()",
    "create_test_node()",
    "create_mock_vision()"
  ],
  "modules": ["helpers/factories", "graph", "simulation"],
  "files": [
    "tests/helpers/factories.py",
    "src/graph/node.py",
    "src/simulation/mock_vision.py"
  ]
}
```

---

## 基于任务的测试生成

### Phase 1: Plan (任务分析)

**Opus分析任务需求**:

```javascript
await agent(`
  分析以下任务，制定测试计划：

  任务: ${taskInfo.title}
  需求: ${taskInfo.requirements}
  相关模块: ${taskInfo.modules}

  输出:
  {
    "test_scope": "针对这三个函数的单元测试",
    "test_approach": "参数化测试 + 边界条件",
    "mock_requirements": ["需要 mock TraversalPlan", "需要 mock MockVisionService"],
    "acceptance_criteria": [
      "create_minimal_plan() 接受 node_id 返回最小有效 plan",
      "create_test_node() 支持所有参数组合",
      "create_mock_vision() 返回可配置的 mock 对象"
    ]
  }
`);
```




### Phase 2: Execute (任务相关分析)

**3个Agent并行分析**:

```javascript
await parallel([
  // 分析函数签名
  () => agent(`分析 ${taskInfo.requirements} 的函数签名和参数`),

  // 分析相关模块
  () => agent(`分析 ${taskInfo.modules} 的依赖关系`),

  // 准备测试数据
  () => agent(`为 ${taskInfo.requirements} 准备测试用例`)
]);
```

### Phase 3-9: 继续标准流程...

---

## 使用场景

### 场景1: 实现新功能

```bash
# 任务: 实现 GraphCompiler.compile()
/Workflow multi-agent-test-validation-tiered "实现 GraphCompiler.compile() 支持 TraversalPlan 编译为执行序列"

# Workflow 自动:
# 1. 分析 compile() 函数签名
# 2. 分析 TraversalPlan 结构
# 3. 生成针对 compile() 的测试
# 4. 验证测试覆盖所有场景
```

### 场景2: 修复Bug

```bash
# 任务: 修复状态机无限循环
/Workflow multi-agent-test-validation-tiered "修复 TraversalStateMachine.has_unvisited_children() 无限循环"

# Workflow 自动:
# 1. 分析 bug 描述
# 2. 生成回归测试
# 3. 生成修复验证测试
```

### 场景3: 重构代码

```bash
# 任务: 重构 AI provider 接口
/Workflow multi-agent-test-validation-tiered "重构 AI provider 为统一接口"

# Workflow 自动:
# 1. 分析新旧接口差异
# 2. 生成迁移测试
# 3. 生成兼容性测试
```

---

## Workflow 输入扩展

### 当前 (需要修改)

```javascript
const moduleName = args?.[0] || 'state_machine';
```

### 修改后

```javascript
// 支持多种输入格式
let input = args?.[0];

// 解析输入
if (input.startsWith('--task')) {
  // 从 tasks.md 读取
  const taskInfo = parseTaskId(input);
} else if (input.includes('--change')) {
  // 从 change 读取任务
  const changeInfo = parseChange(input);
} else if (input.includes('.md')) {
  // 从文件读取
  const taskInfo = parseFile(input);
} else {
  // 直接是任务描述
  const taskDescription = input;
}
```

---

## 完整示例

### 示例: 任务 2.2

```bash
# 开始任务
/opsx:apply prd-v6-9-1-test-refactor

# 选择任务 2.2
> 任务: 2.2 实现 factories.py

# 实现代码 (手动)
# 编辑 tests/helpers/factories.py

# 生成测试 (针对任务)
/Workflow multi-agent-test-validation-tiered --task 2.2

# 输出
✓ 分析任务: 实现 factories.py 的三个函数
✓ 生成测试: test_factories.py
✓ 测试覆盖:
  - test_create_minimal_plan_with_valid_input
  - test_create_minimal_plan_with_invalid_input
  - test_create_test_node_with_all_params
  - test_create_test_node_with_minimal_params
  - test_create_mock_vision_with_config
  - test_create_mock_vision_default

# 运行测试
/Skill module-test helpers

# 验证覆盖率
pytest tests/helpers/factories.py --cov=tests/helpers/factories.py
```

---

## 需要修改的地方

### 1. Workflow 输入解析

```javascript
// .claude/workflows/multi-agent-test-validation-tiered-models.js

// 添加任务解析逻辑
function parseTaskInput(input) {
  // 支持 --task, --change, 直接描述
}

// 修改 Phase 1
const plan = await architectPlanning(taskInfo); // 而非 moduleName
```

### 2. 文档更新

- [WORKFLOW_INTEGRATION_GUIDE.md](WORKFLOW_INTEGRATION_GUIDE.md) - 使用任务ID
- [CLAUDE_WORKFLOW.md](CLAUDE_WORKFLOW.md) - 任务驱动流程

### 3. 新增文档

- [WORKFLOW_TASK_ORIENTED_GUIDE.md](WORKFLOW_TASK_ORIENTED_GUIDE.md) - 任务驱动指南

---

## 总结

| 维度 | 模块视角 | 任务视角 |
|------|----------|----------|
| 输入 | 模块名 | 任务ID/描述 |
| 范围 | 整个模块 | 具体需求 |
| 测试 | 全部功能 | 针对性测试 |
| 验证 | 模块完整性 | 任务完成度 |
| 集成 | /opsx:apply | 自然集成 |

---

**建议**: 修改 workflow 支持任务输入，使其更贴合开发流程。

**维护者**: Uni-Claw Development Team
**最后更新**: 2026-06-08
