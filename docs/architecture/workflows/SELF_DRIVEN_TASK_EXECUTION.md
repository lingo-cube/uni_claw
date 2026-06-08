# Self-Driven Task Execution Workflow

> **Uni-Claw项目的自我驱动任务执行工作流**
>
> **核心机制**: Workflow自动获取任务→分配→实现→验证→完成，循环执行所有任务

---

## 快速开始

```bash
# 启动自我驱动workflow
/Workflow self-driven-task-execution <change-name>

# 示例
/Workflow self-driven-task-execution prd-v6-9-1-test-refactor
```

---

## 工作流架构

```
Workflow启动
  │
  ├─→ 调用opsx:apply获取任务列表
  │
  └─→ 循环每个任务:
       ├─→ 分配任务（考虑依赖关系）
       ├─→ Opus实现任务
       ├─→ Multi-agent并行验证（4个Agent）
       ├─→ Agent对抗验证（3个Battle）
       ├─→ Opus综合裁决
       ├─→ 如果通过：标记完成
       └─→ 如果失败：记录问题→演化新需求→重新尝试
  │
  └─→ 所有任务完成
```

---

## 执行流程

### 1. 获取任务列表

```
调用: openspec instructions apply --change "<name>" --json
输出: 任务列表、进度、上下文文件
```

### 2. 分配任务

```
规则:
- 优先处理无依赖的任务
- 检查依赖任务是否已完成
- 按优先级排序
```

### 3. 实现任务

```
Agent: Opus (架构师级别)
流程:
1. 读取设计文档和规范
2. 理解任务需求
3. 实现代码
4. 确保代码质量
```

### 4. Multi-agent验证

```
并行执行:
- Agent 1 (Sonnet): 需求符合性验证
- Agent 2 (Sonnet): 代码质量验证
- Agent 3 (Sonnet): 边界异常验证
- Agent 4 (Sonnet): 安全性验证
```

### 5. Agent对抗验证

```
对抗机制:
- Battle 1: 挑战需求验证结果
- Battle 2: 挑战质量验证结果
- Battle 3: 挑战边界验证结果

目标: 找出具漏洞、不严谨的判断、遗漏的点
```

### 6. Opus裁决

```
综合评估:
- 整体质量评分
- 关键问题识别
- 是否可以完成
- 改进建议

决策:
- can_complete = true → 标记完成
- can_complete = false → 记录问题→演化需求
```

---

## 问题追踪和需求演化

### 验证失败的处理

```
验证失败
  │
  ├─→ 记录到 issues.md
  │   - 问题描述
  │   - 改进建议
  │   - 后续行动
  │
  └─→ 演化为 tasks.md 新任务
      - 基于改进建议生成新任务
      - 格式: [原ID].[序号] [原任务]改进
```

### 示例

```bash
# 第一次执行
[2.2] factories.py → 验证85/100 → 发现问题
  ↓
记录到 issues.md:
  - 缺少参数验证
  - 边界条件处理不完整
  ↓
演化新任务:
  - [ ] 2.2.1 factories.py参数验证
  - [ ] 2.2.2 factories.py单元测试
  - [ ] 2.2.3 factories.py文档完善

# 第二次执行
[2.2.1] 参数验证 → 实现 → 验证95/100 → 关闭issue → 完成
```

---

## 输出示例

完整的输出示例请参考: [SELF_DRIVEN_WORKFLOW_OUTPUT_EXAMPLE.md](../SELF_DRIVEN_WORKFLOW_OUTPUT_EXAMPLE.md)

---

## 文件结构

```
.claude/workflows/
└── self-driven-task-execution.js  # 主workflow文件

docs/
├── SELF_DRIVEN_WORKFLOW_GUIDE.md  # 本文档
├── SELF_DRIVEN_WORKFLOW_OUTPUT_EXAMPLE.md  # 输出示例
└── ISSUE_TRACKING_AND_REQUIREMENT_EVOLUTION.md  # 问题追踪机制

docs/testing/
└── TASK_ALLOCATION_STRATEGY.md  # 任务分配策略

openspec/changes/{change}/
├── tasks.md  # 任务列表（会被workflow更新）
└── issues.md  # 问题追踪（workflow自动创建）
```

---

## 与/opsx:apply的关系

### 传统方式

```
用户 → /opsx:apply → 手动实现 → 手动验证 → 手动标记
```

### 自我驱动方式

```
用户 → Workflow → 自动获取 → 自动实现 → 自动验证 → 自动标记
```

---

## 模型使用统计

典型执行（5个任务）:
- Opus (主控/裁决/实现): ~15次
- Sonnet (验证/Battle): ~20次
- Haiku (辅助): ~5次

---

## 注意事项

1. **首次运行**: 确保change已创建，tasks.md存在
2. **依赖处理**: 自动处理任务依赖关系
3. **质量把关**: 只有验证通过才标记完成
4. **问题追踪**: 验证失败自动记录问题和演化需求
5. **循环限制**: 防止无限循环，最多2*任务数次迭代

---

## 相关文档

- [任务分配策略](testing/TASK_ALLOCATION_STRATEGY.md)
- [问题追踪和需求演化](../ISSUE_TRACKING_AND_REQUIREMENT_EVOLUTION.md)
- [输出示例](../SELF_DRIVEN_WORKFLOW_OUTPUT_EXAMPLE.md)

---

**维护者**: Uni-Claw Development Team
**最后更新**: 2026-06-08
**版本**: 1.0
