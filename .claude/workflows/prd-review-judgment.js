/**
 * PRD 多代理审阅与评判工作流
 *
 * 流程：
 * 1. Sonnet 代理 1: 全面审阅 PRD，提出批评意见
 * 2. Sonnet 代理 2: 对抗性审阅，从不同角度挑战
 * 3. Opus 代理: 作为架构师做最终评判
 */

export const meta = {
  name: 'prd-review-judgment',
  description: 'PRD 多代理审阅与评判 - Sonnet对抗审阅，Opus最终评判',
  phases: [
    { title: '审阅准备', detail: '读取所有PRD文档' },
    { title: 'Sonnet全面审阅', detail: '第一个Sonnet代理全面审阅' },
    { title: 'Sonnet对抗审阅', detail: '第二个Sonnet代理对抗性审阅' },
    { title: 'Opus架构师评判', detail: 'Opus作为架构师做最终评判' },
    { title: '生成报告', detail: '汇总评判结果' }
  ]
};

// 读取所有PRD文档
async function loadPRDs() {
  const prdFiles = [
    'docs/prd/PRD_V6_10_1_debugging_tools.md',
    'docs/prd/PRD_V6_10_2_state_machine_logic.md',
    'docs/prd/PRD_V6_10_3_code_quality.md',
    'docs/prd/PRD_V6_10_4_debugging_docs.md'
  ];

  const prds = {};
  for (const file of prdFiles) {
    const content = await agent(`Read the file ${file} and return its full content as a string`);
    prds[file] = content;
  }

  return prds;
}

// 第一阶段：Sonnet 全面审阅
async function sonnetComprehensiveReview(prds) {
  phase('Sonnet全面审阅');

  return await agent(
    `作为第一个审阅代理，请全面审阅以下 V6.10.x 系列 PRD：

${Object.keys(prds).map((f, i) => `
=== PRD ${i + 1}: ${f} ===

${prds[f]}

---
`).join('\n')}

请从以下角度进行审阅：

1. **完整性检查**：每个PRD是否包含所有必需章节？
2. **逻辑一致性**：章节之间是否有矛盾？
3. **可行性分析**：实施步骤是否合理？工时估算是否准确？
4. **依赖关系**：PRD之间的依赖是否正确？
5. **成功标准**：成功标准是否具体可验证？
6. **代码质量**：是否符合 CLAUDE_CONVENTIONS.md 的要求？

请提供详细的审阅报告，包括：
- 每个PRD的主要问题（如果有）
- 改进建议
- 总体评分（1-10分）

返回JSON格式：
{
  "review_summary": "总体评价",
  "prd_scores": { "PRD_V6_10_1": 8, "PRD_V6_10_2": 7, ... },
  "issues": [
    { "prd": "PRD_V6_10_1", "issue": "问题描述", "severity": "high/medium/low" }
  ],
  "recommendations": ["建议1", "建议2"]
}`,
    {
      label: 'Sonnet全面审阅',
      phase: 'Sonnet全面审阅',
      model: 'sonnet'
    }
  );
}

// 第二阶段：Sonnet 对抗审阅
async function sonnetAdversarialReview(prds, comprehensiveReview) {
  phase('Sonnet对抗审阅');

  return await agent(
    `作为第二个审阅代理，请对抗性审阅以下 V6.10.x 系列 PRD。

第一个审阅代理的报告：
${JSON.stringify(comprehensiveReview, null, 2)}

PRD 内容：
${Object.keys(prds).map((f, i) => `
=== PRD ${i + 1}: ${f} ===
${prds[f]}
---
`).join('\n')}

请从**挑战者角度**进行对抗性审阅：

1. **挑战假设**：PRD中的假设是否站得住脚？
2. **发现遗漏**：第一个审阅可能遗漏了什么？
3. **质疑优先级**：P0/P1/P2/P3的划分是否合理？
4. **质疑工时**：工时估算是否过于乐观？
5. **质疑依赖**：依赖关系是否真的必要？
6. **风险评估**：什么情况下这个PRD会失败？

请提供对抗性审阅报告，专门指出：
- 第一个审阅可能遗漏的严重问题
- PRD中过于乐观的假设
- 潜在的实施风险

返回JSON格式：
{
  "adversary_summary": "对抗性审阅总结",
  "challenges": [
    { "prd": "PRD_V6_10_1", "challenge": "挑战内容", "risk_level": "high/medium/low" }
  ],
  "overlooked_issues": ["被遗漏的问题1", "被遗漏的问题2"],
  "risk_assessment": { "overall_risk": "medium", "risks": [...] }
}`,
    {
      label: 'Sonnet对抗审阅',
      phase: 'Sonnet对抗审阅',
      model: 'sonnet'
    }
  );
}

// 第三阶段：Opus 架构师评判
async function opusArchitectJudgment(prds, comprehensiveReview, adversarialReview) {
  phase('Opus架构师评判');

  return await agent(
    `作为架构师，请对 V6.10.x 系列 PRD 做最终评判。

参考材料：

1. **PRD文档**：
${Object.keys(prds).map((f, i) => `
=== PRD ${i + 1}: ${f} ===
${prds[f]}
---
`).join('\n')}

2. **第一个审阅报告（Sonnet）**：
${JSON.stringify(comprehensiveReview, null, 2)}

3. **第二个审阅报告（Sonnet对抗）**：
${JSON.stringify(adversarialReview, null, 2)}

请作为架构师进行最终评判：

1. **综合评估**：权衡两个审阅报告，给出你的判断
2. **架构一致性**：这4个PRD作为系列是否一致？
3. **实施可行性**：整体实施计划是否可行？
4. **优先级调整**：是否需要调整实施顺序或优先级？
5. **最终决策**：
   - 批准（可以开始实施）
   - 有条件批准（需要修改后实施）
   - 拒绝（需要重大修改）

请提供架构师评判报告，包括：
- 最终决策（批准/有条件批准/拒绝）
- 每个PRD的架构师评分（1-10分）
- 必须修改的问题（如果有）
- 实施建议

返回JSON格式：
{
  "final_decision": "approve/conditional_approve/reject",
  "architect_scores": { "PRD_V6_10_1": 8, "PRD_V6_10_2": 7, ... },
  "required_changes": [
    { "prd": "PRD_V6_10_1", "change": "必须修改的内容", "priority": "must_have/should_have/nice_to_have" }
  ],
  "implementation_advice": ["实施建议1", "实施建议2"],
  "rationale": "决策理由"
}`,
    {
      label: 'Opus架构师评判',
      phase: 'Opus架构师评判',
      model: 'opus'
    }
  );
}

// 第四阶段：生成报告
async function generateReport(judgment) {
  phase('生成报告');

  const report = `
# V6.10.x PRD 多代理审阅与评判报告

## 最终决策

**决策**: ${judgment.final_decision === 'approve' ? '✅ 批准' : judgment.final_decision === 'conditional_approve' ? '⚠️ 有条件批准' : '❌ 拒绝'}

**理由**: ${judgment.rationale}

---

## 架构师评分

| PRD | 评分 |
|-----|------|
${Object.entries(judgment.architect_scores || {}).map(([prd, score]) => `| ${prd} | ${score}/10 |`).join('\n')}

---

## 必须修改的问题

${judgment.required_changes && judgment.required_changes.length > 0 ? judgment.required_changes.map(c => `
### ${c.prd}

**问题**: ${c.change}
**优先级**: ${c.priority === 'must_have' ? '🔴 必须' : c.priority === 'should_have' ? '🟡 应该' : '🟢 最好'}
`).join('\n') : '✅ 无必须修改的问题'}

---

## 实施建议

${(judgment.implementation_advice || []).map((advice, i) => `${i + 1}. ${advice}`).join('\n')}

---

*报告生成完成后添加时间戳*
`;

  log(report);

  return {
    judgment,
    report
  };
}

// 主工作流
async function run() {
  log('🏛️  V6.10.x PRD 多代理审阅与评判');
  log('');

  // 阶段1：加载PRD
  phase('审阅准备');
  log('读取所有PRD文档...');
  const prds = await loadPRDs();
  log(`✓ 已加载 ${Object.keys(prds).length} 个PRD文档`);
  log('');

  // 阶段2：Sonnet全面审阅
  const comprehensiveReview = await sonnetComprehensiveReview(prds);
  log('✓ Sonnet全面审阅完成');
  log(`  总体评价: ${comprehensiveReview.review_summary || '处理中...'}`);
  log('');

  // 阶段3：Sonnet对抗审阅
  const adversarialReview = await sonnetAdversarialReview(prds, comprehensiveReview);
  log('✓ Sonnet对抗审阅完成');
  log(`  对抗总结: ${adversarialReview.adversary_summary || '处理中...'}`);
  log('');

  // 阶段4：Opus架构师评判
  const judgment = await opusArchitectJudgment(prds, comprehensiveReview, adversarialReview);
  log('✓ Opus架构师评判完成');
  log(`  最终决策: ${judgment.final_decision || '处理中...'}`);
  log('');

  // 阶段5：生成报告
  const result = await generateReport(judgment);
  log('✓ 报告生成完成');
  log('');

  return result;
}

return await run();
