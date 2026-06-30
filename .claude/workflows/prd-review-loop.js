/**
 * PRD 审阅循环 — 自动修复直到达标
 *
 * 用法: Workflow({ name: "prd-review-loop", args: ["docs/prd/xxx.md"] })
 *
 * 流程：
 *   while (评分不达标) {
 *      原子审阅 (Haiku→Sonnet→Opus)   ← schema 保证返回对象
 *      if 达标 → 退出
 *      else   → agent 根据完整审阅结论修复 PRD → 重新审阅
 *   }
 *
 * 达标条件: factual_accuracy >= 9 且 overall >= 8.5
 * 最大迭代: 5 次（防无限循环）
 */

export const meta = {
  name: 'prd-review-loop',
  description: 'PRD 审阅循环 - 自动修复直到评分>=8.5 且 准确性>=9',
  phases: [
    { title: '审阅', detail: '运行原子审阅' },
    { title: '修复', detail: '根据 required_changes 修复 PRD' }
  ]
};

const TARGET = {
  factual_accuracy: 9,
  overall: 8.5
};
const MAX_ITERATIONS = 5;

// ============================================================
// 主循环
// ============================================================

async function run() {
  const prdFiles = Array.isArray(args) ? args : (args ? [args] : []);
  if (!prdFiles.length) {
    throw new Error('未指定 PRD 文件。用法: Workflow({ name: "prd-review-loop", args: ["docs/prd/xxx.md"] })');
  }

  log('🔄 PRD 审阅循环启动');
  log(`   文档: ${prdFiles.join(', ')}`);
  log(`   达标阈值: factual_accuracy >= ${TARGET.factual_accuracy}, overall >= ${TARGET.overall}`);
  log(`   最大迭代: ${MAX_ITERATIONS}`);
  log('');

  let iteration = 0;
  let finalJudgment = null;
  let prevFa = null;       // 上一轮 factual_accuracy
  let prevFa2 = null;      // 上上轮 factual_accuracy
  let prevChangeCount = null; // 上一轮 required_changes 数量

  while (iteration < MAX_ITERATIONS) {
    iteration++;
    log(`═══ 第 ${iteration} 轮 ═══`);
    log('');

    // 步骤 1: 运行原子审阅（返回 schema 验证过的对象）
    phase('审阅');
    let reviewResult;
    try {
      reviewResult = await workflow('prd-review-judgment', prdFiles);
    } catch (e) {
      log(`❌ 审阅异常: ${String(e)}`);
      break;
    }

    if (!reviewResult) {
      log('❌ 审阅返回空值，中止循环');
      break;
    }

    const judgment = reviewResult.judgment;
    if (!judgment) {
      log('❌ 审阅未返回裁决，中止循环');
      break;
    }

    // schema 保证 scores 是对象，factual_accuracy.score 是 number
    const fa = judgment.scores.factual_accuracy.score;
    const ov = judgment.scores.overall;

    log(`  事实准确性: ${fa}/10 (需 >= ${TARGET.factual_accuracy})`);
    log(`  综合评分:   ${ov.toFixed(1)}/10 (需 >= ${TARGET.overall})`);
    log(`  决策:       ${judgment.final_decision}`);
    log('');

    finalJudgment = judgment;

    // 步骤 2: 检查是否达标
    if (fa >= TARGET.factual_accuracy && ov >= TARGET.overall) {
      log(`✅ 达标！第 ${iteration} 轮通过`);
      log('');
      break;
    }

    // 步骤 3: 趋势检测 — 连续下降或修复无效则中断
    if (prevFa2 !== null && prevFa !== null) {
      // 连续 2 轮 factual_accuracy 下降
      if (fa < prevFa && prevFa < prevFa2) {
        log(`⚠️  factual_accuracy 连续 2 轮下降 (${prevFa2} → ${prevFa} → ${fa})，中止循环`);
        break;
      }
    }

    const requiredChanges = judgment.required_changes || [];
    const mustHaves = requiredChanges.filter(c => c.priority === 'must_have');
    const shouldHaves = requiredChanges.filter(c => c.priority === 'should_have');

    if (mustHaves.length === 0 && shouldHaves.length === 0) {
      log('⚠️  无更多可修复项，但评分仍未达标。可能 PRD 本身有结构性问题，需人工介入。');
      log(`   当前: factual_accuracy=${fa}, overall=${ov.toFixed(1)}`);
      break;
    }

    // 修复数量未减少（连续 2 轮），说明 fix 无效
    const totalChanges = mustHaves.length + shouldHaves.length;
    if (prevChangeCount !== null && totalChanges >= prevChangeCount && iteration > 1) {
      log(`⚠️  required_changes 未减少 (${prevChangeCount} → ${totalChanges})，修复可能无效，中止循环`);
      break;
    }

    prevFa2 = prevFa;
    prevFa = fa;
    prevChangeCount = totalChanges;

    // 步骤 4: 修复（传入完整审阅结论 + 代码库核实）
    phase('修复');
    log(`  修复 must_have: ${mustHaves.length} 项, should_have: ${shouldHaves.length} 项`);
    log('');

    const fixTargets = [...mustHaves, ...shouldHaves];

    // schema 保证以下对象可用；JSON.stringify 一次转义，不双转义
    const haikuJson = JSON.stringify(reviewResult.haikuResult, null, 2);
    const sonnetJson = JSON.stringify(reviewResult.sonnetResult, null, 2);
    const opusJson = JSON.stringify(judgment, null, 2);

    const fixPrompt = `你是 PRD 修复者。请阅读以下全部审阅结论，**理解问题的根因后**再修改 PRD 源文件。

## 1. Opus 最终裁决（你的首要依据）

${opusJson}

## 2. Haiku 审阅（含探索边界 — 知道哪里没看）

${haikuJson}

## 3. Sonnet 对抗审阅（含故障场景 — 知道哪里会出问题）

${sonnetJson}

## 本轮必须修复

事实准确性: ${fa}/10（目标 >= ${TARGET.factual_accuracy}）
综合评分: ${ov.toFixed(1)}/10（目标 >= ${TARGET.overall}）

${fixTargets.map((c, i) => `
### 修复 ${i + 1}: ${c.prd}
- **问题**: ${c.change}
- **优先级**: ${c.priority}
- **原因**: ${c.reason}
`).join('\n')}

## 修复流程

1. 先读每个需要修复的 PRD 原文
2. 读审阅结论中的关键证据：
   - Haiku 的 exploration_boundary — 了解哪些代码模块已被探索
   - Haiku 的 issues[].evidence — 具体代码文件/行号
   - Sonnet 的 haiku_verification — Haiku 哪里对、哪里错
   - Sonnet 的 failure_scenarios — 不修复会导致什么故障
   - Opus 的 dispute_resolution — 争议的最终裁定
3. **对照代码库核实** — 涉及事实准确性的问题，自己去读相关模块的实际代码，确保修复后与代码库一致
4. 针对每个问题，**直接编辑文件**
5. 修改原则：
   - 事实准确性问题是最高优先级 — 必须对照代码库修正，不凭空猜测
   - 如果审阅说某引用不存在，先搜代码库确认正确路径再改
   - 不要为了凑评分而过度修改 — 只修真正有问题的部分
   - 保持 PRD 的结构和风格
6. 修复完成后，告诉我你改了哪些文件、改了什么、引用了哪些代码文件作为依据

请开始修复。`;

    try {
      await agent(fixPrompt, {
        label: `修复 PRD (第${iteration}轮)`,
        phase: '修复'
      });
    } catch (e) {
      log(`⚠️  修复异常: ${String(e)}，尝试继续下一轮...`);
    }

    log(`  第 ${iteration} 轮修复完成，进入下一轮审阅...`);
    log('');
  }

  // ============================================================
  // 最终报告
  // ============================================================

  if (iteration >= MAX_ITERATIONS && finalJudgment) {
    const fa = finalJudgment.scores.factual_accuracy.score;
    const ov = finalJudgment.scores.overall;
    if (fa < TARGET.factual_accuracy || ov < TARGET.overall) {
      log(`❌ 已达最大迭代次数 (${MAX_ITERATIONS})，评分仍未达标`);
      log(`   最终: factual_accuracy=${fa}/10, overall=${ov.toFixed(1)}/10`);
      log(`   需人工介入`);
    }
  }

  log('');
  log('═══════════════════════════════════════════');
  log('循环结束');
  log(`  总迭代: ${iteration}`);
  if (finalJudgment) {
    const fa = finalJudgment.scores.factual_accuracy.score;
    const ov = finalJudgment.scores.overall;
    log(`  最终评分: factual_accuracy=${fa}/10, overall=${ov.toFixed(1)}/10`);
    log(`  决策: ${finalJudgment.final_decision}`);
    const passed = fa >= TARGET.factual_accuracy && ov >= TARGET.overall;
    log(`  结果: ${passed ? '✅ 达标' : '❌ 未达标（需人工介入）'}`);
    log(`  报告: docs/prd/reviews/${prdFiles[0].replace(/^.*[\\/]/, '').replace(/\.md$/, '')}_review.md`);
  }
  log('═══════════════════════════════════════════');

  const passed = finalJudgment
    ? finalJudgment.scores.factual_accuracy.score >= TARGET.factual_accuracy
      && finalJudgment.scores.overall >= TARGET.overall
    : false;

  return {
    iterations: iteration,
    final_judgment: finalJudgment,
    passed
  };
}

return await run();
