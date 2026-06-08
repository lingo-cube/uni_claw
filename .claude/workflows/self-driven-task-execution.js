/**
 * Self-Driven Task Execution Workflow
 *
 * 核心机制：
 * - Workflow作为主控，自我驱动整个流程
 * - 调用opsx:apply获取任务列表
 * - 分配任务给subagent实现
 * - Multi-agent自我验证(battle)
 * - 确认高质量后标记完成
 * - 循环直到所有任务完成
 *
 * 流程：
 * Workflow → opsx:apply → 分配任务 → 实现 → 验证 → 标记完成 → 下一个任务
 */

export const meta = {
  name: 'self-driven-task-execution',
  description: '自我驱动任务执行 - 获取任务→分配→实现→验证→完成',
  phases: [
    { title: 'FetchTasks', detail: '从opsx:apply获取任务' },
    { title: 'AssignTask', detail: '分配任务给Agent' },
    { title: 'Implement', detail: '实现任务' },
    { title: 'SelfVerify', detail: 'Multi-agent自我验证' },
    { title: 'Battle', detail: 'Agent对抗验证' },
    { title: 'Judge', detail: 'Opus裁决质量' },
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
// 1. 从opsx:apply获取任务列表
// ============================================================================

const fetchTasksFromOpenSpec = async (changeName) => {
  phase('FetchTasks');
  log('📋 从opsx:apply获取任务列表...');

  const prompt = `
    作为**任务协调器**，从opsx:apply获取任务列表：

    Change名称：${changeName || '自动检测'}

    请执行：
    1. 运行 openspec instructions apply --change "${changeName}" --json
    2. 解析任务列表
    3. 识别所有未完成的任务（-[ ]开头的）
    4. 按优先级排序

    返回JSON格式：
    {
      "change": "change名称",
      "total_tasks": 数量,
      "completed_tasks": 数量,
      "pending_tasks": [
        {
          "id": "任务ID",
          "title": "任务标题",
          "description": "任务描述",
          "priority": "优先级",
          "dependencies": ["依赖任务"]
        }
      ]
    }
  `;

  const tasksInfo = await agent(prompt, {
    label: '获取任务列表',
    model: MODELS.Opus
  });

  log(`✓ 获取到 ${tasksInfo.pending_tasks.length} 个待完成任务`);
  tasksInfo.pending_tasks.forEach(task => {
    log(`  - [${task.id}] ${task.title}`);
  });
  log('');

  return tasksInfo;
};

// ============================================================================
// 2. 分配任务
// ============================================================================

const assignTask = (pendingTasks, completedTasks = []) => {
  phase('AssignTask');

  // 找到下一个可执行的任务（没有未满足的依赖）
  const nextTask = pendingTasks.find(task => {
    if (!task.dependencies || task.dependencies.length === 0) {
      return true;
    }
    // 检查所有依赖是否已完成
    return task.dependencies.every(dep => completedTasks.includes(dep));
  });

  if (!nextTask) {
    log('⏸ 没有可执行的任务（可能存在依赖阻塞）');
    return null;
  }

  log(`📌 分配任务: [${nextTask.id}] ${nextTask.title}`);
  log('');

  return nextTask;
};

// ============================================================================
// 3. 实现任务
// ============================================================================

const implementTask = async (task, changeName) => {
  phase('Implement');
  log(`🔧 实现任务: [${task.id}] ${task.title}`);
  log('');

  // 读取上下文文件（design, specs等）
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
      "implementation_summary": "实现摘要",
      "notes": "注意事项"
    }
  `;

  const implementation = await agent(contextPrompt, {
    label: `实现任务${task.id}`,
    model: MODELS.Opus
  });

  log('✓ 任务实现完成');
  log(`  修改文件: ${implementation.files_modified.join(', ')}`);
  log('');

  return implementation;
};

// ============================================================================
// 4. Multi-agent自我验证
// ============================================================================

const selfVerify = async (task, implementation) => {
  phase('SelfVerify');
  log('🔍 Multi-agent自我验证...');

  const verifyResults = await parallel([
    // Agent 1: 需求符合性
    () => agent(`
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
    `, { label: '需求验证', model: MODELS.Sonnet }),

    // Agent 2: 代码质量
    () => agent(`
      作为**质量验证Agent**，验证代码质量：

      实现: ${JSON.stringify(implementation)}

      返回JSON：
      {
        "status": "PASS|FAIL",
        "score": 0-100,
        "issues": [],
        "suggestions": []
      }
    `, { label: '质量验证', model: MODELS.Sonnet }),

    // Agent 3: 边界异常
    () => agent(`
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
    `, { label: '边界验证', model: MODELS.Sonnet })
  ]);

  log('✓ 自我验证完成');
  return verifyResults;
};

// ============================================================================
// 5. Agent对抗验证
// ============================================================================

const battleVerify = async (task, implementation, verifyResults) => {
  phase('Battle');
  log('⚔️ Agent对抗验证...');

  const battleResults = await parallel([
    // Battle 1: 挑战需求验证
    () => agent(`
      作为**对抗Agent**，挑战需求验证结果：

      验证结果: ${JSON.stringify(verifyResults[0])}

      找出漏洞、不严谨的判断、遗漏的点。

      返回JSON：
      {
        "challenges": [],
        "found_issues": [],
        "adjusted_score": 0-100
      }
    `, { label: '需求Battle', model: MODELS.Sonnet }),

    // Battle 2: 挑战质量验证
    () => agent(`
      作为**对抗Agent**，挑战质量验证结果：

      验证结果: ${JSON.stringify(verifyResults[1])}

      找出被忽略的问题、低估的严重性。

      返回JSON：
      {
        "challenges": [],
        "found_issues": [],
        "adjusted_score": 0-100
      }
    `, { label: '质量Battle', model: MODELS.Sonnet })
  ]);

  log('✓ 对抗验证完成');
  return battleResults;
};

// ============================================================================
// 6. Opus裁决
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

  if (!judgment.can_complete) {
    log('建议:');
    judgment.recommendations.forEach(rec => log(`  💡 ${rec}`));
    log('');
  }

  return judgment;
};

// ============================================================================
// 7. 标记完成
// ============================================================================

const markTaskComplete = async (changeName, taskId) => {
  phase('Complete');
  log(`✅ 标记任务完成: [${taskId}]`);

  // 更新tasks.md文件
  const result = await agent(`
    标记任务完成：

    Change: ${changeName}
    任务ID: ${taskId}

    在tasks.md文件中，将任务状态从 - [ ] 改为 - [x]

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
// 主流程
// ============================================================================

async function run() {
  log('🚀 Self-Driven Task Execution Workflow');
  log('═════════════════════════════════════════════════════');
  log('');

  // 获取change名称
  const changeName = args?.[0];

  if (!changeName) {
    log('❌ 请指定change名称');
    log('用法: /Workflow self-driven-task-execution <change-name>');
    log('');
    return;
  }

  log(`📋 Change: ${changeName}`);
  log('');

  // 获取任务列表
  const tasksInfo = await fetchTasksFromOpenSpec(changeName);

  if (tasksInfo.pending_tasks.length === 0) {
    log('✅ 所有任务已完成！');
    return;
  }

  // 任务循环
  const completedTasks = [];
  let iteration = 0;
  const maxIterations = tasksInfo.pending_tasks.length * 2; // 防止无限循环

  while (iteration < maxIterations) {
    iteration++;

    log('═════════════════════════════════════════════════════');
    log(`📍 循环 ${iteration}/${maxIterations}`);
    log('═════════════════════════════════════════════════════');
    log('');

    // 分配任务
    const task = assignTask(tasksInfo.pending_tasks, completedTasks);

    if (!task) {
      log('⏸ 没有可执行的任务，结束流程');
      break;
    }

    // 实现任务
    const implementation = await implementTask(task, changeName);

    // 自我验证
    const verifyResults = await selfVerify(task, implementation);

    // 对抗验证
    const battleResults = await battleVerify(task, implementation, verifyResults);

    // Opus裁决
    const judgment = await opusJudge(task, implementation, verifyResults, battleResults);

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
      log('⚠️ 任务未通过验证，将在下一轮重新尝试');
      log('');
      // 不标记完成，任务仍在pending_tasks中
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
    iterations: iteration
  };
}

return await run();
