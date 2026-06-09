/**
 * Self-Driven Task Execution - Final Optimized Version
 *
 * 核心设计：
 * - 智能单次执行：基于路由选择Haiku或Opus
 * - 持续学习：记住每种任务类型适合哪个模型
 * - 顺序验证：2×Haiku + 1×Sonnet + 1×Haiku + 1×Sonnet + Opus（避免503）
 * - 质量保证：完整验证+Battle+Judge流程
 * - 问题记录：自动生成ISSUES文档
 *
 * 与原版兼容：
 * - 从/opsx:apply触发方式不变
 * - 调用skill方式不变
 * - 读取设计文档方式不变（Agent自主发现）
 */

export const meta = {
  name: 'self-driven-task-execution-final',
  description: '自我驱动任务执行 - 最终优化版（智能路由+顺序执行+持续学习+问题记录）',
  phases: [
    { title: 'FetchTasks', detail: '从opsx:apply获取任务' },
    { title: 'AssignTask', detail: '分配任务+智能路由' },
    { title: 'Implement', detail: '实现任务(Haiku或Opus)' },
    { title: 'SelfVerify', detail: '顺序验证(2×Haiku + 1×Sonnet)' },
    { title: 'Battle', detail: '顺序对抗(1×Haiku + 1×Sonnet)' },
    { title: 'Judge', detail: 'Opus裁决' },
    { title: 'Complete', detail: '标记完成' },
    { title: 'NextTask', detail: '继续下一个任务' }
  ]
};

const MODELS = {
  Haiku: 'haiku-4-5-20251001',
  Sonnet: 'claude-sonnet-4-6',
  Opus: 'claude-opus-4-8'
};

// ============================================================================
// 路由记忆（持续学习）
// ============================================================================

const routingMemory = {};
const executionHistory = [];
const collectedIssues = [];

// ============================================================================
// 1. 从opsx:apply获取任务列表（与原版一致，用Opus）
// ============================================================================

const fetchTasksFromOpenSpec = async (changeName) => {
  phase('FetchTasks');
  log('📋 从OpenSpec获取任务列表...');

  const prompt = `
    作为**任务协调器**，从OpenSpec获取任务列表：

    Change名称：${changeName || '自动检测'}

    请执行：
    1. 运行 npx openspec show "${changeName}" --json 查看change信息
    2. 读取文件 openspec/changes/${changeName}/tasks.md 获取任务列表
    3. 解析任务列表，识别所有未完成的任务（- [ ] 开头的）
    4. 按优先级排序

    返回JSON格式：
    {
      "change": "change名称",
      "total_tasks": 数量,
      "completed_tasks": 数量,
      "pending_tasks": [
        {
          "id": "任务ID (如 1.1, 1.2)",
          "title": "任务标题",
          "description": "任务描述",
          "priority": "优先级 (High/Medium/Low)",
          "dependencies": ["依赖任务ID列表"]
        }
      ]
    }
  `;

  const tasksInfo = await agent(prompt, {
    label: '获取任务列表',
    model: MODELS.Haiku,
    timeout: 60000
  });

  log(`✓ 获取到 ${tasksInfo.pending_tasks?.length || 0} 个待完成任务`);
  if (tasksInfo.pending_tasks) {
    tasksInfo.pending_tasks.forEach(task => {
      log(`  - [${task.id}] ${task.title}`);
    });
  }
  log('');

  return tasksInfo;
};

// ============================================================================
// 2. 分配任务+智能路由
// ============================================================================

const assignTask = (pendingTasks, completedTasks = []) => {
  phase('AssignTask');

  // 找到下一个可执行的任务（没有未满足的依赖）
  const nextTask = pendingTasks.find(task => {
    if (!task.dependencies || task.dependencies.length === 0) {
      return true;
    }
    return task.dependencies.every(dep => completedTasks.includes(dep));
  });

  if (!nextTask) {
    log('⏸ 没有可执行的任务（可能存在依赖阻塞）');
    return null;
  }

  // 智能路由：确定使用哪个模型
  const taskType = inferTaskType(nextTask);
  const route = smartRoute(taskType);

  log(`📌 分配任务: [${nextTask.id}] ${nextTask.title}`);
  log(`   类型推断: ${taskType}`);
  log(`   路由决策: ${route.model} - ${route.reason}`);
  log('');

  return { ...nextTask, _route: route, _type: taskType };
};

// 推断任务类型
const inferTaskType = (task) => {
  const title = (task.title || '').toLowerCase();

  if (title.includes('测试') || title.includes('test')) return '测试';
  if (title.includes('文档') || title.includes('doc')) return '文档';
  if (title.includes('配置') || title.includes('config')) return '配置';
  if (title.includes('重构') || title.includes('refactor')) return '重构';
  if (title.includes('架构') || title.includes('architecture')) return '架构';
  if (title.includes('实现') || title.includes('implement')) return '实现';
  if (title.includes('修复') || title.includes('fix')) return '修复';

  return '通用';
};

// 智能路由决策
const smartRoute = (taskType) => {
  const memory = routingMemory[taskType];

  // 有历史记录
  if (memory && memory.success >= 2) {
    const successRate = memory.success / (memory.success + memory.failure);
    if (successRate >= 0.7) {
      return { model: memory.model, reason: `历史成功率${(successRate*100).toFixed(0)}%` };
    }
  }

  // 无历史或成功率低，启发式判断
  const simpleTypes = ['测试', '文档', '配置'];
  const complexTypes = ['架构', '重构'];

  if (complexTypes.includes(taskType)) {
    return { model: 'Opus', reason: '复杂任务类型' };
  }
  if (simpleTypes.includes(taskType)) {
    return { model: 'Haiku', reason: '简单任务类型' };
  }

  // 默认Haiku（便宜）
  return { model: 'Haiku', reason: '默认先试Haiku' };
};

// ============================================================================
// 3. 实现任务（智能路由：Haiku或Opus）
// ============================================================================

const implementTask = async (task, changeName) => {
  phase('Implement');
  log(`🔧 实现任务: [${task.id}] ${task.title}`);
  log(`   使用模型: ${task._route.model}`);
  log('');

  const startTime = Date.now();
  let result;

  if (task._route.model === 'Haiku') {
    result = await haikuImplement(task, changeName);
  } else {
    result = await opusImplement(task, changeName);
  }

  const duration = Date.now() - startTime;

  if (result.passed) {
    log(`✓ 任务实现完成 (${duration}ms)`);
    log(`  修改文件: ${result.implementation.files_modified?.join(', ') || '见代码'}`);
    updateRoutingMemory(task._type, task._route.model, true);
    recordExecution(task, task._route.model, true, duration);
  } else {
    log(`✗ 实现失败: ${result.reason}`);
    updateRoutingMemory(task._type, task._route.model, false);
    recordExecution(task, task._route.model, false, duration);

    // 如果Haiku失败，建议下次用Opus
    if (task._route.model === 'Haiku') {
      log(`💡 已记录: ${task._type}类型的任务下次尝试Opus`);
    }
  }
  log('');

  return result;
};

// Haiku实现
const haikuImplement = async (task, changeName) => {
  try {
    const contextPrompt = `
      作为**实现Agent**，实现以下任务：

      任务ID: ${task.id}
      任务标题: ${task.title}
      任务描述: ${task.description}

      Change: ${changeName}

      请执行：
      1. 读取相关的设计文档和规范
      2. 理解任务需求和验收标准
      3. 实现代码
      4. 确保代码质量

      返回JSON格式：
      {
        "implemented": true,
        "files_modified": ["文件1", "文件2"],
        "key_changes": ["变更1", "变更2"],
        "code": "生成的代码",
        "implementation_summary": "实现摘要",
        "notes": "注意事项"
      }
    `;

    const implementation = await agent(contextPrompt, {
      label: `实现任务${task.id}`,
      model: MODELS.Haiku,
      timeout: 90000
    });

    return { passed: true, implementation };
  } catch (e) {
    return { passed: false, reason: e.message };
  }
};

// Opus实现
const opusImplement = async (task, changeName) => {
  try {
    const contextPrompt = `
      作为**实现Agent**，实现以下任务：

      任务ID: ${task.id}
      任务标题: ${task.title}
      任务描述: ${task.description}

      Change: ${changeName}

      请执行：
      1. 读取相关的设计文档和规范
      2. 理解任务需求和验收标准
      3. 实现代码
      4. 确保代码质量

      返回JSON格式：
      {
        "implemented": true,
        "files_modified": ["文件1", "文件2"],
        "key_changes": ["变更1", "变更2"],
        "code": "生成的代码",
        "implementation_summary": "实现摘要",
        "notes": "注意事项"
      }
    `;

    const implementation = await agent(contextPrompt, {
      label: `实现任务${task.id}`,
      model: MODELS.Opus,
      timeout: 180000
    });

    return { passed: true, implementation };
  } catch (e) {
    return { passed: false, reason: e.message };
  }
};

// ============================================================================
// 4. SelfVerify（优化：2×Haiku + 1×Sonnet）
// ============================================================================

const selfVerify = async (task, implementation) => {
  phase('SelfVerify');
  log('🔍 Multi-agent自我验证（顺序执行）...');

  // 顺序执行以避免503错误（从parallel改为顺序）
  const verifyResults = [];

  // Agent 1: 需求符合性（Haiku）
  log('  [1/3] 需求验证...');
  verifyResults.push(await agent(`
    作为**需求验证Agent**，验证实现是否符合任务需求：

    任务: ${task.title}
    需求: ${task.description}
    实现: ${JSON.stringify(implementation)}

    返回JSON：
    {
      "status": "PASS|FAIL",
      "score": 0-100,
      "issues": [],
      "gaps": []
    }
  `, { label: '需求验证', model: MODELS.Haiku }));

  // Agent 2: 代码质量（Haiku）
  log('  [2/3] 质量验证...');
  verifyResults.push(await agent(`
    作为**质量验证Agent**，验证代码质量：

    实现: ${JSON.stringify(implementation)}

    返回JSON：
    {
      "status": "PASS|FAIL",
      "score": 0-100,
      "issues": [],
      "suggestions": []
    }
  `, { label: '质量验证', model: MODELS.Haiku }));

  // Agent 3: 边界异常（Sonnet - 需要理解）
  log('  [3/3] 边界验证...');
  verifyResults.push(await agent(`
    作为**边界验证Agent**，验证边界和异常处理：

    任务: ${task.title}
    实现: ${JSON.stringify(implementation)}

    返回JSON：
    {
      "status": "PASS|FAIL",
      "score": 0-100,
      "missed_edge_cases": [],
      "exception_gaps": []
    }
  `, { label: '边界验证', model: MODELS.Sonnet }));

  log('✓ 自我验证完成');
  return verifyResults;
};

// ============================================================================
// 5. Battle（优化：1×Haiku + 1×Sonnet）
// ============================================================================

const battleVerify = async (task, implementation, verifyResults) => {
  phase('Battle');
  log('⚔️ Agent对抗验证（顺序执行）...');

  // 顺序执行以避免503错误（从parallel改为顺序）
  const battleResults = [];

  // Battle 1: 挑战需求验证（Haiku - 快速找明显遗漏）
  log('  [1/2] 需求Battle...');
  battleResults.push(await agent(`
    作为**对抗Agent**，挑战需求验证结果：

    任务: ${task.title}
    实现: ${JSON.stringify(implementation).slice(0, 1000)}
    验证结果: ${JSON.stringify(verifyResults[0])}

    找出明显的遗漏和不严谨的判断。

    返回JSON：
    {
      "challenges": [],
      "found_issues": [],
      "adjusted_score": 0-100
    }
  `, { label: '需求Battle', model: MODELS.Haiku }));

  // Battle 2: 挑战质量验证（Sonnet - 深度挑战）
  log('  [2/2] 质量Battle...');
  battleResults.push(await agent(`
    作为**对抗Agent**，挑战质量验证结果：

    任务: ${task.title}
    实现: ${JSON.stringify(implementation).slice(0, 1000)}
    验证结果: ${JSON.stringify(verifyResults[1])}

    找出被忽略的问题、低估的严重性。

    返回JSON：
    {
      "challenges": [],
      "found_issues": [],
      "adjusted_score": 0-100
    }
  `, { label: '质量Battle', model: MODELS.Sonnet }));

  log('✓ 对抗验证完成');
  return battleResults;
};

// ============================================================================
// 6. Opus裁决（与原版一致）
// ============================================================================

const opusJudge = async (task, implementation, verifyResults, battleResults) => {
  phase('Judge');
  log('⚖️ Opus综合裁决...');

  const judgment = await agent(`
    作为**裁决架构师**，综合评估任务实现质量：

    任务: ${task.title}
    实现: ${JSON.stringify(implementation)}
    验证结果: ${JSON.stringify(verifyResults)}
    对抗结果: ${JSON.stringify(battleResults)}

    返回JSON：
    {
      "overall_status": "PASS|FAIL",
      "overall_score": 0-100,
      "can_complete": boolean,
      "critical_issues": [],
      "recommendations": [],
      "reasoning": "裁决理由"
    }
  `, { label: 'Opus裁决', model: MODELS.Opus });

  log(`✓ 裁决完成: ${judgment.overall_status} (${judgment.overall_score}/100)`);
  log(`  可完成: ${judgment.can_complete ? '✅' : '❌'}`);
  log('');

  if (judgment.critical_issues && judgment.critical_issues.length > 0) {
    log('关键问题:');
    judgment.critical_issues.forEach(issue => log(`  ❌ ${issue}`));
  }

  if (!judgment.can_complete && judgment.recommendations) {
    log('建议:');
    judgment.recommendations.forEach(rec => log(`  💡 ${rec}`));
  }
  log('');

  return judgment;
};

// ============================================================================
// 7. 标记完成（与原版一致）
// ============================================================================

const markTaskComplete = async (changeName, taskId) => {
  phase('Complete');
  log(`✅ 标记任务完成: [${taskId}]`);

  const result = await agent(`
    标记任务完成：

    Change: ${changeName}
    任务ID: ${taskId}
    文件路径: openspec/changes/${changeName}/tasks.md

    在tasks.md文件中，找到任务ID为 ${taskId} 的任务，将其状态从 - [ ] 改为 - [x]

    使用Edit工具精确修改。

    返回JSON：
    {
      "marked": true,
      "file_updated": "文件路径"
    }
  `, { label: '标记完成', model: MODELS.Haiku });

  log('✓ 任务已标记完成');
  log('');

  return result;
};

// ============================================================================
// 辅助函数
// ============================================================================

const recordExecution = (task, model, success, duration) => {
  executionHistory.push({
    taskId: task.id,
    type: task._type,
    model,
    success,
    duration,
    timestamp: Date.now()
  });
};

const updateRoutingMemory = (type, model, success) => {
  if (!routingMemory[type]) {
    routingMemory[type] = { model, success: 0, failure: 0, lastUsed: Date.now() };
  }

  if (success) {
    routingMemory[type].success++;
    routingMemory[type].model = model;
  } else {
    routingMemory[type].failure++;
    if (model === 'Haiku') {
      routingMemory[type].model = 'Opus';
    }
  }
  routingMemory[type].lastUsed = Date.now();
};

const collectIssue = (taskId, issue, severity = 'MEDIUM') => {
  collectedIssues.push({
    id: `ISSUE-${taskId}-${collectedIssues.length + 1}`,
    taskId,
    ...issue,
    severity,
    timestamp: Date.now()
  });
};

const generateIssuesContent = (changeName) => {
  if (collectedIssues.length === 0) return null;

  const today = new Date().toISOString().split('T')[0];

  let content = `# ${changeName.toUpperCase().replace(/-/g, '_')}_ISSUES_${today}

> **生成时间**: ${today}
> **来源**: Self-Driven Workflow - Final Optimized
> **Change**: ${changeName}

---

## 问题列表

`;

  collectedIssues.forEach((issue, index) => {
    content += `### ${index + 1}. ${issue.id} - ${issue.title}

**严重性**: ${issue.severity}
**任务**: ${issue.taskId}

**描述**:
${issue.description}

**建议**:
${issue.recommendation}

---
`;
  });

  const bySeverity = { CRITICAL: 0, HIGH: 0, MEDIUM: 0, LOW: 0 };
  collectedIssues.forEach(i => bySeverity[i.severity] = (bySeverity[i.severity] || 0) + 1);

  content += `

## 严重性统计

| 严重性 | 数量 |
|--------|------|
| CRITICAL | ${bySeverity.CRITICAL} |
| HIGH | ${bySeverity.HIGH} |
| MEDIUM | ${bySeverity.MEDIUM} |
| LOW | ${bySeverity.LOW} |

---

## 后续行动计划

### 立即处理 (CRITICAL)
${collectedIssues.filter(i => i.severity === 'CRITICAL').map(i => `- ${i.id}: ${i.title}`).join('\n') || '(无)'}

### 本周处理 (HIGH)
${collectedIssues.filter(i => i.severity === 'HIGH').map(i => `- ${i.id}: ${i.title}`).join('\n') || '(无)'}

### 有时间处理 (MEDIUM/LOW)
${collectedIssues.filter(i => i.severity === 'MEDIUM' || i.severity === 'LOW').map(i => `- ${i.id}: ${i.title}`).join('\n') || '(无)'}

---

## 验证方式

重新运行workflow验证修复效果：
\`\`\`bash
/Workflow self-driven-task-execution-final ${changeName}
\`\`\`

---

*本文件由 Self-Driven Workflow 自动生成*
`;

  return content;
};

const printRoutingSummary = () => {
  log('═════════════════════════════════════════════════════');
  log('📊 路由记忆分析');
  log('═════════════════════════════════════════════════════');
  log('');

  if (Object.keys(routingMemory).length === 0) {
    log('(暂无路由记忆)');
    return;
  }

  Object.entries(routingMemory).forEach(([type, mem]) => {
    const total = mem.success + mem.failure;
    const rate = total > 0 ? ((mem.success / total) * 100).toFixed(0) : '0';
    log(`  ${type}: → ${mem.model} (成功率${rate}%, ${mem.success}成功/${mem.failure}失败)`);
  });
  log('');

  const byModel = { Haiku: 0, Opus: 0 };
  executionHistory.forEach(h => byModel[h.model]++);
  log('执行统计:');
  log(`  Haiku: ${byModel.Haiku}次`);
  log(`  Opus: ${byModel.Opus}次`);

  const totalTime = executionHistory.reduce((sum, h) => sum + h.duration, 0);
  log(`  总耗时: ${(totalTime/1000).toFixed(1)}秒`);
  log('');
};

// ============================================================================
// 主流程（与原版兼容）
// ============================================================================

async function run() {
  log('🚀 Self-Driven Task Execution - Final Optimized');
  log('═════════════════════════════════════════════════════');
  log('');

  // 获取change名称
  const changeName = args?.[0];

  if (!changeName) {
    log('❌ 请指定change名称');
    log('用法: /Workflow self-driven-task-execution-final <change-name>');
    log('');
    return;
  }

  log(`📋 Change: ${changeName}`);
  log('');

  // 获取任务列表（与原版一致）
  const tasksInfo = await fetchTasksFromOpenSpec(changeName);

  if (tasksInfo.pending_tasks.length === 0) {
    log('✅ 所有任务已完成！');
    return;
  }

  // 任务循环
  const completedTasks = [];
  let iteration = 0;
  const maxIterations = tasksInfo.pending_tasks.length * 2;

  while (iteration < maxIterations) {
    iteration++;

    log('═════════════════════════════════════════════════════');
    log(`📍 循环 ${iteration}/${maxIterations}`);
    log('═════════════════════════════════════════════════════');
    log('');

    // 分配任务（含智能路由）
    const task = assignTask(tasksInfo.pending_tasks, completedTasks);

    if (!task) {
      log('⏸ 没有可执行的任务，结束流程');
      break;
    }

    // 实现任务（Haiku或Opus）
    const implResult = await implementTask(task, changeName);

    if (!implResult.passed) {
      collectIssue(task.id, {
        title: `${task.title} 实现失败`,
        description: implResult.reason || '未知原因',
        recommendation: '建议手动实现或升级到Opus重试'
      }, 'HIGH');
      continue;
    }

    // 自我验证
    const verifyResults = await selfVerify(task, implResult.implementation);

    // 对抗验证
    const battleResults = await battleVerify(task, implResult.implementation, verifyResults);

    // Opus裁决
    const judgment = await opusJudge(task, implResult.implementation, verifyResults, battleResults);

    // 收集关键问题
    if (judgment.critical_issues && judgment.critical_issues.length > 0) {
      judgment.critical_issues.forEach(issue => {
        collectIssue(task.id, {
          title: `${task.title} - ${issue.substring(0, 50)}...`,
          description: issue,
          recommendation: judgment.recommendations?.[0] || '请检查实现'
        }, 'CRITICAL');
      });
    }

    // 如果通过，标记完成
    if (judgment.can_complete) {
      await markTaskComplete(changeName, task.id);
      completedTasks.push(task.id);

      // 从待处理列表中移除
      tasksInfo.pending_tasks = tasksInfo.pending_tasks.filter(t => t.id !== task.id);

      if (tasksInfo.pending_tasks.length === 0) {
        log('');
        log('🎉 所有任务已完成！');
        break;
      }

      log(`⏭ 剩余任务: ${tasksInfo.pending_tasks.length}`);
      log('');
    } else {
      collectIssue(task.id, {
        title: `${task.title} 未通过验证`,
        description: judgment.recommendations?.join('; ') || '质量未达标',
        recommendation: '下一轮重试'
      }, 'MEDIUM');
      log('⚠️ 任务未通过验证，将在下一轮重新尝试');
      log('');
    }
  }

  // 最终总结
  log('═════════════════════════════════════════════════════');
  log('📊 执行总结');
  log('═════════════════════════════════════════════════════');
  log(`总迭代次数: ${iteration}`);
  log(`完成任务数: ${completedTasks.length}`);
  log(`剩余任务数: ${tasksInfo.pending_tasks.length}`);
  log('');

  // 打印路由记忆
  printRoutingSummary();

  // 生成问题内容
  const issuesContent = generateIssuesContent(changeName);

  if (issuesContent) {
    log(`📋 识别到 ${collectedIssues.length} 个遗留问题`);
    log(`   问题文档: docs/issues/${changeName.toUpperCase()}_ISSUES_${new Date().toISOString().split('T')[0]}.md`);
    log('');
  }

  if (tasksInfo.pending_tasks.length === 0) {
    log('🎉 恭喜！所有任务已完成！');
    log('💡 提示: 可以运行 /opsx:archive 归档此change');
  } else {
    log('⏸ 部分任务尚未完成，可以稍后继续');
  }

  return {
    changeName,
    completedTasks,
    pendingTasks: tasksInfo.pending_tasks,
    iterations: iteration,
    routingMemory,
    executionHistory,
    issues: collectedIssues,
    issuesContent,
    issuesPath: issuesContent ? `docs/issues/${changeName.toUpperCase()}_ISSUES_${new Date().toISOString().split('T')[0]}.md` : null
  };
}

return await run();
