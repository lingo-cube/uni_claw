# Uni-Claw Workflows & Skills Index

> **可用工具索引** - 跨Session可用的自动化工具

---

## Quick Reference

| 类型 | 名称 | 用途 | 命令 |
|------|------|------|------|
| **Workflow** | self-driven-task-execution | 自我驱动任务执行 | `/Workflow self-driven-task-execution <change>` |
| **Workflow** | test-scenario-generation-evaluation | 测试用例生成评估 | `/Workflow test-scenario-generation-evaluation <module>` |
| **Skill** | test-extraction | 测试场景提取 | `/skill test-extraction <module>` |

---

## Workflows (.claude/workflows/)

### 1. self-driven-task-execution.js ⭐ **主要使用**

**用途**: 自我驱动任务执行，自动获取→实现→验证→完成任务

```bash
/Workflow self-driven-task-execution prd-v6-9-1-test-refactor
```

**流程**:
1. 调用opsx:apply获取任务列表
2. 循环每个任务:
   - Opus实现任务
   - Multi-agent并行验证
   - Agent对抗验证
   - Opus裁决
   - 标记完成
3. 问题追踪和需求演化

**文档**: [docs/architecture/workflows/SELF_DRIVEN_TASK_EXECUTION.md](docs/architecture/workflows/SELF_DRIVEN_TASK_EXECUTION.md)

---

### 2. test-scenario-generation-evaluation.js

**用途**: 测试用例生成和评估，multi-agent验证+battle+质量评分

```bash
/Workflow test-scenario-generation-evaluation state_machine
```

**流程**:
1. 读取设计文档
2. 生成测试用例列表
3. 读取现有测试用例
4. Multi-agent验证新生成用例
5. 对抗验证
6. 与现有用例比对
7. 质量评分
8. 生成报告

**输出**: `docs/reports/{MODULE}_TEST_SCENARIO_EVALUATION.md`

---

### 3. claude-modular-refactor-workflow.js

**用途**: 模块化重构workflow（中间版本，已废弃）

**状态**: ⚠️ 被 self-driven-task-execution 替代

---

### 4. integrated-test-gen.js

**用途**: 集成测试生成（中间版本，已废弃）

**状态**: ⚠️ 被 test-scenario-generation-evaluation 替代

---

### 5. single-vs-multi-agent-comparison.js

**用途**: 单Agent vs Multi-Agent对比（实验性质）

**状态**: 📊 实验性workflow，用于研究对比

---

### 6. rules-integration-closed-loop.js

**用途**: 规则集成闭环（审计→补充→创建→验证）

**状态**: 🔧 特定用途workflow

---

## Skills (.claude/skills/)

### 1. test-extraction ⭐ **主要使用**

**用途**: 从设计文档系统化提取测试场景

```bash
/skill test-extraction traversal
```

**流程**:
1. 定位设计文档
2. 识别测试维度 (States, Transitions, Boundaries, Errors, Features)
3. 创建测试矩阵
4. 分类测试 (normal, edge, errors, integration)
5. 估算覆盖率
6. 生成 `docs/testing/{MODULE}_TEST_SCENARIOS.md`

**支持模块**: graph, state-machine, traversal, exception, adb, config, analysis, safety, ai, simulation

---

## 如何使用

### 在Claude Code中

```bash
# 使用Workflow
/Workflow {workflow-name} {args}

# 使用Skill
/skill {skill-name} {args}
```

### 跨Session使用

**Workflows和Skills都是持久化的**，任何新session都可以使用：

1. 通过CLI命令调用
2. 通过Workflow/Skill工具调用
3. 自动被发现和加载

### 查看可用工具

```bash
# 查看所有workflows
ls .claude/workflows/

# 查看所有skills
ls .claude/skills/
```

---

## 文件结构

```
.claude/
├── workflows/
│   ├── self-driven-task-execution.js        # ⭐ 主要
│   ├── test-scenario-generation-evaluation.js # ⭐ 主要
│   ├── claude-modular-refactor-workflow.js    # ⚠️ 已废弃
│   ├── integrated-test-gen.js                # ⚠️ 已废弃
│   ├── single-vs-multi-agent-comparison.js   # 📊 实验
│   └── rules-integration-closed-loop.js     # 🔧 特定用途
│
└── skills/
    └── test-extraction/
        └── skill.md                          # ⭐ 主要
```

---

## 清理建议

需要清理的文件（中间版本）：
- `claude-modular-refactor-workflow.js` - 被 self-driven-task-execution 替代
- `integrated-test-gen.js` - 被 test-scenario-generation-evaluation 替代

保留的文件（核心功能）：
- `self-driven-task-execution.js` - 主要任务执行workflow
- `test-scenario-generation-evaluation.js` - 测试评估workflow
- `test-extraction/skill.md` - 测试场景提取skill

---

## 相关文档

- [自我驱动任务执行指南](docs/architecture/workflows/SELF_DRIVEN_TASK_EXECUTION.md)
- [任务分配策略](docs/testing/TASK_ALLOCATION_STRATEGY.md)
- [问题追踪机制](docs/architecture/workflows/ISSUE_TRACKING_AND_REQUIREMENT_EVOLUTION.md)
- [测试提取方法论](docs/testing/TEST_EXTRACTION_METHODOLOGY.md)

---

**最后更新**: 2026-06-08
