/**
 * Test Scenario Evaluation - Simplified
 */

export const meta = {
  name: 'test-scenario-evaluation',
  description: '测试用例生成评估'
};

const MODELS = { Opus: 'claude-opus-4-8' };

async function run() {
  log('🧪 Test Scenario Evaluation');
  log('═══════════════════════════════');
  log('');

  // 直接从参数获取，简化处理
  const input = args || '';
  const parts = String(input).split('|');
  const module = parts[0]?.trim() || 'state_machine';
  const task = parts[1]?.trim() || '为state_machine生成测试用例';

  log(`📦 模块: ${module}`);
  log(`📝 任务: ${task}`);
  log('');

  // 读取文档
  let designDoc = '';
  let existingTests = '';

  try {
    designDoc = await Read(`docs/architecture/modules/${module}-design.md`);
    log(`✓ 设计文档已读取 (${designDoc.length}字符)`);
  } catch (e) {
    log(`⚠ 设计文档读取失败`);
  }

  try {
    existingTests = await Read(`docs/testing/${module.toUpperCase()}_TEST_SCENARIOS.md`);
    log(`✓ 现有测试用例已读取`);
  } catch (e) {
    log(`⚠ 无现有测试用例`);
  }

  log('');

  // 生成测试用例并评估
  phase('Generate');
  log('🔧 生成测试用例并评估...');
  log('');

  let result;
  try {
    result = await agent(`
作为测试架构师，完成以下任务：

模块：${module}
任务：${task}

设计文档（${designDoc.length}字符）：
${designDoc.slice(0, 3000)}...

现有测试用例文档（${existingTests.length}字符）：
${existingTests.slice(0, 2000)}...

请完成：
1. 生成完整测试用例列表（使用5步法）
2. 与现有用例比对
3. 评估质量

重要：直接返回JSON对象，不要有任何其他解释文字。

返回JSON格式：
{
  "scenarios": {
    "total": 数量,
    "list": [{"id", "title", "given", "when", "then"}]
  },
  "comparison": {
    "new_unique": 数量,
    "existing_unique": 数量
  },
  "evaluation": {
    "score": 0-100,
    "grade": "A/B/C/D/F",
    "summary": "摘要"
  }
}
    `, { label: '生成并评估', model: MODELS.Opus });
  } catch (e) {
    log(`❌ Agent调用失败: ${e.message}`);
    log(`⚠ 使用默认值`);
    result = {
      scenarios: { total: 0, list: [] },
      comparison: { new_unique: 0, existing_unique: 0 },
      evaluation: { score: 0, grade: 'F', summary: '生成失败' }
    };
  }

  // 确保result有正确的结构
  if (!result || !result.evaluation) {
    log(`⚠ 返回格式不对，使用默认值`);
    result = {
      scenarios: { total: 0, list: [] },
      comparison: { new_unique: 0, existing_unique: 0 },
      evaluation: { score: 0, grade: 'F', summary: '返回格式错误' }
    };
  }

  log(`✓ 评分: ${result.evaluation.score}/100 (${result.evaluation.grade})`);
  log('');

  // 生成报告内容
  phase('Report');
  log('📝 生成报告内容...');

  const report = `# ${module.toUpperCase()} 测试用例评估报告

**任务**: ${task}

## 执行摘要

- **评分**: ${result.evaluation.score}/100 (${result.evaluation.grade})
- **摘要**: ${result.evaluation.summary}

## 统计

- **新生成用例**: ${result.scenarios.total}
- **新增独特用例**: ${result.comparison.new_unique}
- **现有独特用例**: ${result.comparison.existing_unique}

## 测试用例列表

${result.scenarios.list?.slice(0, 20).map(s => `
### ${s.id} - ${s.title}
**Given**: ${s.given}
**When**: ${s.when}
**Then**: ${s.then}
`).join('') || '无'}

---
*本报告由 test-scenario-evaluation workflow 自动生成*
`;

  log(`✓ 报告内容已生成 (${report.length}字符)`);
  log('');

  // 生成遗留问题列表
  phase('Issues');
  log('📋 生成遗留问题列表...');

  const issues = [];

  // 基于评估结果生成issues
  if (result.evaluation.score < 80) {
    issues.push({
      id: `ISSUE-${module.toUpperCase()}-001`,
      title: `${module}测试覆盖率不足`,
      severity: 'HIGH',
      description: `当前评分${result.evaluation.score}/100低于80分`,
      recommendation: '补充缺失的测试用例'
    });
  }

  if (result.comparison.existing_unique > 0) {
    issues.push({
      id: `ISSUE-${module.toUpperCase()}-002`,
      title: `${module}现有测试用例有独特场景未被生成用例覆盖`,
      severity: 'MEDIUM',
      description: `现有${result.comparison.existing_unique}个独特用例未在生成用例中体现`,
      recommendation: '审查现有测试用例，确保生成用例覆盖所有场景'
    });
  }

  if (result.scenarios.total < 20) {
    issues.push({
      id: `ISSUE-${module.toUpperCase()}-003`,
      title: `${module}生成测试用例数量不足`,
      severity: 'MEDIUM',
      description: `仅生成${result.scenarios.total}个测试用例，可能覆盖不全`,
      recommendation: '重新审查设计文档，补充测试场景'
    });
  }

  // 生成issues内容
  let issuesContent = '';
  if (issues.length > 0) {
    issuesContent = `# ${module.toUpperCase()} 遗留问题

> 生成时间: ${new Date().toISOString().split('T')[0]}
> 来源: Self-Driven Workflow - Test Scenario Evaluation
> 模块: ${module}

## 问题列表

${issues.map((issue, index) => `
### ${index + 1}. ${issue.id} - ${issue.title}

**严重性**: ${issue.severity}

**描述**: ${issue.description}

**建议**: ${issue.recommendation}

---
`).join('')}

## 后续行动

1. 根据优先级处理上述问题
2. 更新测试用例文档
3. 补充缺失的测试代码
4. 重新运行workflow验证

---
*本文件由 Self-Driven Workflow 自动生成*
`;
  }

  log(`✓ 识别到 ${issues.length} 个遗留问题`);
  log('');

  log('═══════════════════════════════');
  log('📊 完成');
  log('═══════════════════════════════');
  log('');
  log('📁 输出文件:');
  log(`  - 报告: docs/reports/${module.toUpperCase()}_TEST_EVAL.md`);
  if (issues.length > 0) {
    log(`  - 问题: docs/issues/${module.toUpperCase()}_ISSUES.md`);
  }
  log('');

  return {
    module,
    result,
    report,
    issuesContent,
    reportPath: `docs/reports/${module.toUpperCase()}_TEST_EVAL.md`,
    issuesPath: issues.length > 0 ? `docs/issues/${module.toUpperCase()}_ISSUES.md` : null
  };
}

return await run();
