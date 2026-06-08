# 问题追踪和需求演化机制

> **核心**: 验证失败 → 问题追踪 → 演化为新需求 → 继续执行

---

## 机制设计

### 验证失败的处理流程

```
任务实现 → 验证 → Battle → 裁决
                      │
                      ▼
                 can_complete = false
                      │
        ┌─────────────┴─────────────┐
        │                           │
        ▼                           ▼
   【问题追踪】              【需求演化】
        │                           │
        ▼                           ▼
  记录到issues.md           添加到tasks.md
        │                           │
        └─────────────┬─────────────┘
                      ▼
               下一次实现参考
```

---

## 实现方式

### 1. 问题追踪文件

创建 `openspec/changes/{change}/issues.md`:

```markdown
# Issues - prd-v6-9-1-test-refactor

## 未解决的问题

### [2.2] 实现 factories.py - 85/100

**状态**: CONDITIONAL_PASS
**验证时间**: 2026-06-08

**发现的问题**:
- ❌ 缺少参数验证 (空值、None、类型错误)
- ⚠️ 边界条件处理不完整

**改进建议**:
- 💡 添加参数类型验证
- 💡 处理None和空字符串情况
- 💡 添加参数验证的单元测试

**后续行动**:
- [ ] 在2.2.1中添加参数验证
- [ ] 添加单元测试覆盖边界情况
```

### 2. 需求演化机制

在 `tasks.md` 中演化需求：

```markdown
## 2. 共享辅助模块

### 第一批
- [x] 2.2 实现 factories.py - 已完成，有改进建议
- [x] 2.3 实现 state_inspector.py
- [x] 2.4 实现 trace_analyzer.py

### 第一批改进 (从issues演化而来)
- [ ] 2.2.1 factories.py参数验证 - 添加参数类型和边界验证
- [ ] 2.2.2 factories.py单元测试 - 补充边界情况测试
- [ ] 2.3.1 state_inspector性能优化
```

---

## Workflow实现

### 修改裁决逻辑

```javascript
const opusJudge = async (task, implementation, verifyResults, battleResults) => {
  // ... 原有裁决逻辑

  if (!judgment.can_complete) {
    // 记录问题到issues.md
    await recordIssues(changeName, task, judgment);

    // 演化新需求到tasks.md
    await evolveRequirements(changeName, task, judgment);
  }

  return judgment;
};
```

### 记录问题函数

```javascript
const recordIssues = async (changeName, task, judgment) => {
  phase('RecordIssues');

  const issuesContent = `
## [${task.id}] ${task.title}

**状态**: ${judgment.overall_status}
**评分**: ${judgment.overall_score}/100
**时间**: ${new Date().toISOString()}

**关键问题**:
${judgment.critical_issues.map(i => `- ❌ ${i}`).join('\n')}

**阻塞问题**:
${judgment.blocking_issues.map(i => `- 🛑 ${i}`).join('\n')}

**改进建议**:
${judgment.recommendations.map(r => `- 💡 ${r}`).join('\n')}

**后续行动**:
${judgment.next_steps.map(s => `- [ ] ${s}`).join('\n')}
`;

  // 追加到issues.md
  await appendToFile(
    `openspec/changes/${changeName}/issues.md`,
    issuesContent
  );

  log('✓ 问题已记录到issues.md');
};
```

### 演化需求函数

```javascript
const evolveRequirements = async (changeName, task, judgment) => {
  phase('EvolveRequirements');

  // 基于改进建议生成新任务
  const newTasks = judgment.recommendations.map((rec, index) => {
    return `- [ ] ${task.id}.${index + 1} ${task.title}改进 - ${rec}`;
  });

  const newTasksContent = `
## ${task.title}改进任务

${newTasks.join('\n')}
`;

  // 追加到tasks.md
  await appendToFile(
    `openspec/changes/${changeName}/tasks.md`,
    newTasksContent
  );

  log(`✓ 已演化 ${newTasks.length} 个新任务到tasks.md`);
};
```

---

## 完整流程示例

### 第一次执行

```
📍 循环 1/10
📌 任务: [2.2] 实现 factories.py
🔧 实现...
🔍 验证...
⚔️ Battle...
⚖️ 裁决: CONDITIONAL_PASS (85/100)
  可完成: ✅ (有改进建议但不阻塞)

✅ 标记完成: [2.2]
✓ 问题已记录到issues.md
✓ 已演化 3 个新任务到tasks.md
```

### 演化的新任务

```
## 2.2 factories.py改进任务
- [ ] 2.2.1 factories.py参数验证 - 添加参数类型和边界验证
- [ ] 2.2.2 factories.py单元测试 - 补充边界情况测试
- [ ] 2.2.3 factories.py文档完善 - 添加使用示例
```

### 第二次执行（处理改进任务）

```
📍 循环 6/10
📌 任务: [2.2.1] factories.py参数验证
🔧 实现...
  添加参数验证:
    - 检查node_id非空
    - 检查kwargs类型
    - 处理None值
🔍 验证...
⚔️ Battle...
⚖️ 裁决: PASS (95/100)
  可完成: ✅

✅ 标记完成: [2.2.1]
✓ issues.md中对应问题已关闭
```

---

## 闭环机制

### 问题闭环

```
发现问题 → 记录issues.md → 演化为新任务 → 实现解决 → 关闭issue
```

### 实现闭环

```javascript
// 在任务完成时检查是否解决了issues
const closeRelatedIssues = async (changeName, taskId) => {
  const issues = await readIssuesFile(changeName);

  // 查找相关的未解决问题
  const relatedIssues = issues.filter(issue =>
    issue.taskId === taskId &&
    issue.status === 'OPEN'
  );

  if (relatedIssues.length > 0) {
    // 标记为已解决
    await markIssuesResolved(changeName, relatedIssues);
    log(`✓ 已关闭 ${relatedIssues.length} 个相关问题`);
  }
};
```

---

## 完整的workflow更新

### 在主循环中添加

```javascript
// 完成任务后
if (judgment.can_complete) {
  await markTaskComplete(changeName, task.id);

  // 关闭相关问题
  await closeRelatedIssues(changeName, task.id);
} else {
  // 记录问题
  await recordIssues(changeName, task, judgment);

  // 演化新需求
  await evolveRequirements(changeName, task, judgment);
}
```

---

## 输出示例

### 第一次执行失败

```
⚖️ Opus综合裁决...
裁决: FAIL
整体评分: 45/100
可完成: ❌

══════════════════════════════════════════════════════════════════
Phase: RecordIssues
══════════════════════════════════════════════════════════════════

✓ 问题已记录到issues.md
  文件: openspec/changes/prd-v6-9-1-test-refactor/issues.md
  记录问题: 3个

══════════════════════════════════════════════════════════════════
Phase: EvolveRequirements
══════════════════════════════════════════════════════════════════

✓ 已演化 3 个新任务到tasks.md
  新任务:
    - [2.2.1] 修复核心函数缺失
    - [2.2.2] 添加必要的导入语句
    - [2.2.3] 补充类型注解

⚠️ 任务未通过验证，将在下一轮重新尝试演化后的任务
```

---

## 总结

**核心机制**:
```
验证失败 → 问题追踪 → 需求演化 → 继续执行 → 问题闭环
```

**文件**:
- `issues.md` - 追踪所有问题
- `tasks.md` - 演化的新需求

**优势**:
- 问题不会丢失
- 自动转化为可执行的改进任务
- 形成持续改进的闭环

---

**维护者**: Uni-Claw Development Team
**最后更新**: 2026-06-08
