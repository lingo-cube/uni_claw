/**
 * Self-Driven Task Execution Workflow - Optimized
 *
 * 核心优化：
 * - 渐进式升级: Haiku → Sonnet → Opus
 * - 智能路由: 记住任务类型，下次直接用合适的模型
 * - 快速并行: 能并行的都并行
 * - 可观测: 记录每个任务的执行路径
 *
 * 流程保持不变，但实现层优化了速度和成本
 */

export const meta = {
  name: 'self-driven-task-execution-optimized',
  description: '自我驱动任务执行 - 优化版（渐进升级+智能路由）',
  phases: [
    { title: 'FetchTasks', detail: '从opsx:apply获取任务（Haiku）' },
    { title: 'AssignTask', detail: '分配任务给Agent（保持逻辑）' },
    { title: 'Implement', detail: '渐进实现（Haiku→Sonnet→Opus）' },
    { title: 'SelfVerify', detail: '快速验证（2×Haiku + 1×Sonnet）' },
    { title: 'Battle', detail: 'Battle验证（2×Haiku）' },
    { title: 'Judge', detail: 'Opus裁决+路径记录' },
    { title: 'Complete', detail: '标记完成（Haiku）' },
    { title: 'NextTask', detail: '智能路由下一个任务' }
  ]
};

const MODELS = {
  Haiku: 'haiku-4-5-20251001',
  Sonnet: 'claude-sonnet-4-6',
  Opus: 'claude-opus-4-8'
};

// ============================================================================
// 任务路由记忆（智能路由的核心）
// ============================================================================

// 记录每种任务类型应该用哪一层实现
const taskTypeRouting = {
  // 格式: "task_type": "Haiku|Sonnet|Opus"
  // 首次用Haiku尝试，失败后升级，并记住下次直接用升级后的层
};

const executionPaths = []; // 记录所有执行路径

// ============================================================================
// 1. FetchTasks - 优化: 用Haiku（简单解析）
// ============================================================================

const fetchTasksFromOpenSpec = async (changeName) => {
  phase('FetchTasks');
  log('📋 从opsx:apply获取任务列表...');

  const prompt = `
    从opsx:apply获取任务列表。

    Change：${changeName}

    执行：
    1. 运行: openspec instructions apply --change "${changeName}" --json
    2. 解析JSON，提取任务列表
    3. 找出所有未完成的任务（-[ ]开头的）

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
          "dependencies": ["依赖任务"],
          "type": "任务类型（测试/实现/重构等）"
        }
      ]
    }
  `;

  // 用Haiku就够了，这是简单解析任务
  const tasksInfo = await agent(prompt, {
    label: '获取任务列表',
    model: MODELS.Haiku,
    timeout: 60000
  });

  log(`✓ 获取到 ${tasksInfo.pending_tasks?.length || 0} 个待完成任务`);
  tasksInfo.pending_tasks?.forEach(task => {
    log(`  - [${task.id}] ${task.title} (${task.type || 'general'})`);
  });
  log('');

  return tasksInfo;
};

// ============================================================================
// 2. AssignTask - 保持不变（纯逻辑，无需AI）
// ============================================================================

const assignTask = (pendingTasks, completedTasks = []) => {
  phase('AssignTask');

  // 智能路由：优先选择历史上成功的任务类型
  const sortedTasks = [...pendingTasks].sort((a, b) => {
    const aRouteLevel = getRouteLevel(a.type);
    const bRouteLevel = getRouteLevel(b.type);
    // 优先用更低层的（更快的）
    return aRouteLevel - bRouteLevel;
  });

  const nextTask = sortedTasks.find(task => {
    if (!task.dependencies || task.dependencies.length === 0) {
      return true;
    }
    return task.dependencies.every(dep => completedTasks.includes(dep));
  });

  if (!nextTask) {
    log('⏸ 没有可执行的任务（可能存在依赖阻塞）');
    return null;
  }

  // 检查是否有历史路由记录
  const suggestedLevel = taskTypeRouting[nextTask.type] || 'Haiku';
  log(`📌 分配任务: [${nextTask.id}] ${nextTask.title}`);
  log(`   类型: ${nextTask.type || 'general'}`);
  log(`   建议层级: ${suggestedLevel}`);
  log('');

  return { ...nextTask, suggestedLevel };
};

// 辅助：获取路由层级（用于排序）
const getRouteLevel = (taskType) => {
  const level = taskTypeRouting[taskType];
  if (level === 'Haiku') return 1;
  if (level === 'Sonnet') return 2;
  if (level === 'Opus') return 3;
  return 0; // 未知类型优先尝试
};

// ============================================================================
// 3. Implement - 核心优化: 渐进升级
// ============================================================================

const implementTask = async (task, changeName) => {
  phase('Implement');
  log(`🔧 实现任务: [${task.id}] ${task.title}`);
  log('');

  const startTime = Date.now();
  let result, level = task.suggestedLevel || 'Haiku';
  const upgradeReasons = [];

  // 尝试Haiku
  if (level === 'Haiku') {
    log('  📍 尝试 Haiku 层...');
    result = await haikuImplement(task, changeName);

    if (result.passed) {
      log('  ✓ Haiku 成功，跳过升级');
      recordExecutionPath(task.id, 'Haiku', Date.now() - startTime, []);
    } else {
      upgradeReasons.push(...result.issues.map(i => i.reason));
      level = 'Sonnet';
      log(`  ⚠ Haiku 失败: ${result.issues[0]?.reason}`);
      log('  📍 升级到 Sonnet 层...');
    }
  }

  // 尝试Sonnet
  if (level === 'Sonnet') {
    result = await sonnetImplement(task, changeName, result?.draft);

    if (result.passed) {
      log('  ✓ Sonnet 成功');
      recordExecutionPath(task.id, 'Sonnet', Date.now() - startTime, upgradeReasons);
    } else {
      upgradeReasons.push(...result.issues.map(i => i.reason));
      level = 'Opus';
      log(`  ⚠ Sonnet 失败: ${result.issues[0]?.reason}`);
      log('  📍 升级到 Opus 层...');
    }
  }

  // 尝试Opus（兜底）
  if (level === 'Opus') {
    result = await opusImplement(task, changeName);
    log('  ✓ Opus 完成（兜底保证）');
    recordExecutionPath(task.id, 'Opus', Date.now() - startTime, upgradeReasons);
  }

  // 更新路由记忆
  if (task.type) {
    taskTypeRouting[task.type] = level;
    log(`  📝 记住: ${task.type} 类型的任务下次直接用 ${level}`);
  }

  log('');
  return result.implementation;
};

// Haiku实现层
const haikuImplement = async (task, changeName) => {
  try {
    // 1. 快速读取关键文件（并行）
    const files = await parallel([
      () => agent(`
        读取 tasks.md 文件，提取任务 ${task.id} 的详细描述。
        返回: {description: "...", acceptance_criteria: [...]}
      `, { model: MODELS.Haiku, timeout: 30000 }),
      () => agent(`
        查找并读取与任务相关的设计文档。
        任务: ${task.title}
        返回: {doc_content: "...", doc_path: "..."}
      `, { model: MODELS.Haiku, timeout: 30000 })
    ]);

    // 2. 生成代码初稿
    const draft = await agent(`
      任务: ${task.title}
      描述: ${task.description}
      设计文档: ${JSON.stringify(files[1])}

      生成代码实现：
      - 遵循现有代码风格
      - 包含基本错误处理
      - 添加关键注释

      返回JSON：
      {
        "implemented": true,
        "files_modified": ["文件1"],
        "key_changes": ["变更1"],
        "code": "生成的代码",
        "implementation_summary": "摘要"
      }
    `, { model: MODELS.Haiku, timeout: 60000 });

    // 3. Haiku自检
    const checks = await parallel([
      () => agent(`
        检查代码是否有明显语法错误或逻辑问题：
        ${JSON.stringify(draft)}

        返回: {has_error: bool, errors: [], reason: "..."}
      `, { model: MODELS.Haiku }),
      () => agent(`
        检查代码是否满足基本需求：
        需求: ${task.description}
        代码: ${JSON.stringify(draft)}

        返回: {meets_requirement: bool, gaps: [], reason: "..."}
      `, { model: MODELS.Haiku })
    ]);

    const hasErrors = checks[0].has_error;
    const meetsReq = checks[1].meets_requirement;

    return {
      passed: !hasErrors && meetsReq,
      draft,
      implementation: draft,
      issues: [
        ...(hasErrors ? [{reason: checks[0].reason}] : []),
        ...(meetsReq ? [] : [{reason: checks[1].reason}])
      ]
    };
  } catch (e) {
    return {
      passed: false,
      issues: [{reason: `Haiku执行失败: ${e.message}`}]
    };
  }
};

// Sonnet实现层
const sonnetImplement = async (task, changeName, previousDraft) => {
  try {
    const analysis = await agent(`
      分析为什么之前的实现失败，并修复：
      任务: ${task.title}
      描述: ${task.description}
      之前的问题: ${previousDraft ? JSON.stringify(previousDraft) : '无'}

      分析原因并生成修复后的代码。

      返回JSON：
      {
        "implemented": true,
        "files_modified": ["文件1"],
        "key_changes": ["变更1"],
        "code": "修复后的代码",
        "implementation_summary": "摘要",
        "fixes_applied": ["修复1"]
      }
    `, { model: MODELS.Sonnet, timeout: 120000 });

    // Sonnet自检
    const check = await agent(`
      检查修复后的代码质量：
      ${JSON.stringify(analysis)}

      是否满足需求且无明显问题？
      返回: {ok: bool, remaining_issues: [], reason: "..."}
    `, { model: MODELS.Sonnet });

    return {
      passed: check.ok,
      draft: analysis,
      implementation: analysis,
      issues: check.ok ? [] : [{reason: check.reason}]
    };
  } catch (e) {
    return {
      passed: false,
      issues: [{reason: `Sonnet执行失败: ${e.message}`}]
    };
  }
};

// Opus实现层（兜底）
const opusImplement = async (task, changeName) => {
  const implementation = await agent(`
    作为资深架构师，实现以下任务：

    任务ID: ${task.id}
    任务标题: ${task.title}
    任务描述: ${task.description}
    Change: ${changeName}

    要求：
    1. 阅读相关设计文档
    2. 理解架构上下文
    3. 实现高质量代码
    4. 考虑边界条件
    5. 添加适当注释

    返回JSON：
    {
      "implemented": true,
      "files_modified": ["文件1", "文件2"],
      "key_changes": ["变更1", "变更2"],
      "code": "生成的代码",
      "implementation_summary": "实现摘要",
      "notes": "注意事项"
    }
  `, { model: MODELS.Opus, timeout: 180000 });

  return {
    passed: true,
    implementation
  };
};

// ============================================================================
// 4. SelfVerify - 优化: 2×Haiku + 1×Sonnet
// ============================================================================

const selfVerify = async (task, implementation) => {
  phase('SelfVerify');
  log('🔍 快速验证...');

  const verifyResults = await parallel([
    // Agent 1: 语法检查 (Haiku)
    () => agent(`
      检查代码的语法和基本错误：
      ${JSON.stringify(implementation)}

      返回JSON：
      {
        "status": "PASS|FAIL",
        "score": 0-100,
        "syntax_errors": [],
        "basic_issues": []
      }
    `, { label: '语法检查', model: MODELS.Haiku }),

    // Agent 2: 需求符合性 (Haiku)
    () => agent(`
      检查代码是否满足明确的需求：
      任务: ${task.title}
      需求: ${task.description}
      实现: ${JSON.stringify(implementation)}

      返回JSON：
      {
        "status": "PASS|FAIL",
        "score": 0-100,
        "met_requirements": [],
        "gaps": []
      }
    `, { label: '需求检查', model: MODELS.Haiku }),

    // Agent 3: 代码质量 (Sonnet - 需要更深理解)
    () => agent(`
      评估代码质量（设计模式、可维护性）：
      ${JSON.stringify(implementation)}

      返回JSON：
      {
        "status": "PASS|FAIL",
        "score": 0-100,
        "quality_issues": [],
        "suggestions": []
      }
    `, { label: '质量评估', model: MODELS.Sonnet })
  ]);

  log('✓ 验证完成');
  return verifyResults;
};

// ============================================================================
// 5. Battle - 优化: 2×Haiku（找遗漏）
// ============================================================================

const battleVerify = async (task, implementation, verifyResults) => {
  phase('Battle');
  log('⚔️ 对抗验证...');

  const battleResults = await parallel([
    // Battle 1: 挑战语法/需求检查 (Haiku)
    () => agent(`
      作为对抗者，检查验证结果是否有遗漏：
      ${JSON.stringify(verifyResults[0])}
      ${JSON.stringify(verifyResults[1])}

      找被忽略的问题。
      返回：{found_issues: [], adjusted_score: 0-100}
    `, { label: '检查Battle', model: MODELS.Haiku }),

    // Battle 2: 挑战质量评估 (Haiku)
    () => agent(`
      作为对抗者，检查质量评估是否有遗漏：
      ${JSON.stringify(verifyResults[2])}

      找被忽略的质量问题。
      返回：{found_issues: [], adjusted_score: 0-100}
    `, { label: '质量Battle', model: MODELS.Haiku })
  ]);

  log('✓ 对抗验证完成');
  return battleResults;
};

// ============================================================================
// 6. Judge - 优化: Opus裁决 + 路径记录
// ============================================================================

const opusJudge = async (task, implementation, verifyResults, battleResults) => {
  phase('Judge');
  log('⚖️ Opus综合裁决...');

  const judgment = await agent(`
    作为裁决架构师，综合评估：

    任务: ${task.title}
    验证: ${JSON.stringify(verifyResults)}
    对抗: ${JSON.stringify(battleResults)}

    返回JSON：
    {
      "overall_status": "PASS|FAIL",
      "overall_score": 0-100,
      "can_complete": boolean,
      "critical_issues": [],
      "recommendations": []
    }
  `, { label: 'Opus裁决', model: MODELS.Opus });

  log(`✓ 裁决: ${judgment.overall_status} (${judgment.overall_score}/100)`);
  log(`  可完成: ${judgment.can_complete ? '✅' : '❌'}`);

  if (judgment.critical_issues?.length > 0) {
    log('  关键问题:');
    judgment.critical_issues.forEach(issue => log(`    ❌ ${issue}`));
  }

  log('');
  return judgment;
};

// ============================================================================
// 7. Complete - 保持不变（Haiku）
// ============================================================================

const markTaskComplete = async (changeName, taskId) => {
  phase('Complete');
  log(`✅ 标记任务完成: [${taskId}]`);

  const result = await agent(`
    在tasks.md中标记任务完成：
    Change: ${changeName}
    任务ID: ${taskId}

    将 - [ ] 改为 - [x]

    返回：{marked: true, file_updated: "路径"}
  `, { label: '标记完成', model: MODELS.Haiku });

  log('✓ 任务已标记完成');
  log('');

  return result;
};

// ============================================================================
// 8. NextTask - 优化: 智能路由
// ============================================================================

// 已集成到 AssignTask 中

// ============================================================================
// 辅助函数
// ============================================================================

const recordExecutionPath = (taskId, level, duration, reasons) => {
  executionPaths.push({
    taskId,
    level,
    duration,
    upgradeReasons: reasons
  });
};

const printExecutionSummary = () => {
  log('');
  log('═════════════════════════════════════════════════════');
  log('📊 执行路径分析');
  log('═════════════════════════════════════════════════════');
  log('');

  // 按层级统计
  const byLevel = {};
  executionPaths.forEach(path => {
    byLevel[path.level] = (byLevel[path.level] || 0) + 1;
  });

  log('层级分布:');
  Object.entries(byLevel).forEach(([level, count]) => {
    log(`  ${level}: ${count} 个任务`);
  });
  log('');

  // 路由记忆
  if (Object.keys(taskTypeRouting).length > 0) {
    log('任务类型路由记忆:');
    Object.entries(taskTypeRouting).forEach(([type, level]) => {
      log(`  ${type}: → ${level}`);
    });
    log('');
  }

  // 升级原因分析
  const upgrades = executionPaths.filter(p => p.upgradeReasons.length > 0);
  if (upgrades.length > 0) {
    log('升级原因统计:');
    const reasons = {};
    upgrades.forEach(path => {
      path.upgradeReasons.forEach(reason => {
        reasons[reason] = (reasons[reason] || 0) + 1;
      });
    });
    Object.entries(reasons).forEach(([reason, count]) => {
      log(`  ${count}x: ${reason}`);
    });
  }
};

// ============================================================================
// 主流程
// ============================================================================

async function run() {
  log('🚀 Self-Driven Task Execution - Optimized');
  log('═════════════════════════════════════════════════════');
  log('');

  const changeName = args?.[0];
  if (!changeName) {
    log('❌ 请指定change名称');
    log('用法: /Workflow self-driven-task-execution-optimized <change-name>');
    return;
  }

  log(`📋 Change: ${changeName}`);
  log('');

  // 获取任务
  const tasksInfo = await fetchTasksFromOpenSpec(changeName);
  if (!tasksInfo.pending_tasks || tasksInfo.pending_tasks.length === 0) {
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

    // 分配任务（带智能路由）
    const task = assignTask(tasksInfo.pending_tasks, completedTasks);
    if (!task) break;

    // 实现（渐进升级）
    const implementation = await implementTask(task, changeName);

    // 验证（优化后）
    const verifyResults = await selfVerify(task, implementation);

    // 对抗（优化后）
    const battleResults = await battleVerify(task, implementation, verifyResults);

    // 裁决
    const judgment = await opusJudge(task, implementation, verifyResults, battleResults);

    // 完成或重试
    if (judgment.can_complete) {
      await markTaskComplete(changeName, task.id);
      completedTasks.push(task.id);
      tasksInfo.pending_tasks = tasksInfo.pending_tasks.filter(t => t.id !== task.id);

      if (tasksInfo.pending_tasks.length === 0) {
        log('🎉 所有任务已完成！');
        break;
      }
      log(`⏭ 剩余任务: ${tasksInfo.pending_tasks.length}`);
      log('');
    } else {
      log('⚠️ 任务未通过，下一轮重试');
      log('');
    }
  }

  // 打印执行分析
  printExecutionSummary();

  return {
    changeName,
    completedTasks,
    pendingTasks: tasksInfo.pending_tasks,
    iterations: iteration,
    executionPaths,
    taskTypeRouting
  };
}

return await run();
