/**
 * PRD 多代理审阅与评判工作流
 *
 * 用法: Workflow({ name: "prd-review-judgment", args: ["docs/prd/xxx.md"] })
 *
 * 流程：
 *    Haiku 广撒网 → Sonnet 精准打击 → Opus 裁定
 *
 * 数据流：
 *    Haiku: PRD + 项目规范 + 代码库探索（广） → 问题 + 探索边界
 *    Sonnet: PRD + Haiku结论（含边界） → 独立验证 + 对抗报告
 *    Opus:  PRD + 双方证据 + 项目规范 → 四维度裁决
 *
 * 产出：docs/prd/reviews/{name}_review.md（git 追踪）
 */

export const meta = {
  name: 'prd-review-judgment',
  description: 'PRD 多代理审阅与评判 - Haiku广撒网，Sonnet精准打击，Opus裁决',
  phases: [
    { title: '加载文档与上下文', detail: '读取 PRD + 项目规范' },
    { title: 'Haiku广撒网', detail: '代码库探索 + 基础审阅 + 探索边界' },
    { title: 'Sonnet精准打击', detail: '验证 Haiku 高严重度问题 + 盲区检查 + QA 对抗' },
    { title: 'Opus裁决', detail: '证据质量裁定 + 四维度评分' },
    { title: '输出报告', detail: 'Markdown 报告写入磁盘' }
  ]
};

// ============================================================
// JSON Schemas — 确保 agent 返回值是对象，非 string
// ============================================================

const HAIKU_SCHEMA = {
  type: "object",
  required: ["review_summary", "completeness_scores", "issues", "cross_prd_conflicts", "exploration_boundary"],
  properties: {
    review_summary: { type: "string" },
    completeness_scores: { type: "object" },
    issues: {
      type: "array",
      items: {
        type: "object",
        required: ["prd", "issue", "category", "severity", "evidence", "suggestion"],
        properties: {
          prd: { type: "string" },
          issue: { type: "string" },
          category: { type: "string", enum: ["factual_error","incomplete","inconsistent","convention_violation","todo","unclear"] },
          severity: { type: "string", enum: ["high","medium","low"] },
          evidence: { type: "string" },
          suggestion: { type: "string" }
        }
      }
    },
    cross_prd_conflicts: {
      type: "array",
      items: {
        type: "object",
        required: ["prd_a", "prd_b", "conflict"],
        properties: { prd_a: { type: "string" }, prd_b: { type: "string" }, conflict: { type: "string" } }
      }
    },
    exploration_boundary: {
      type: "object",
      required: ["explored_modules", "not_explored", "reason_skipped"],
      properties: {
        explored_modules: { type: "array", items: { type: "string" } },
        not_explored: { type: "array", items: { type: "string" } },
        reason_skipped: { type: "string" }
      }
    }
  }
};

const SONNET_SCHEMA = {
  type: "object",
  required: ["adversary_summary", "risk_level", "haiku_verification", "failure_scenarios", "risk_assessment"],
  properties: {
    adversary_summary: { type: "string" },
    risk_level: { type: "string", enum: ["low","medium","high","critical"] },
    haiku_verification: {
      type: "object",
      required: ["haiku_correct", "haiku_wrong", "haiku_overstated", "haiku_blind_spots"],
      properties: {
        haiku_correct: { type: "array", items: { type: "string" } },
        haiku_wrong: { type: "array", items: { type: "string" } },
        haiku_overstated: { type: "array", items: { type: "string" } },
        haiku_blind_spots: { type: "array", items: { type: "string" } }
      }
    },
    failure_scenarios: {
      type: "array",
      items: {
        type: "object",
        required: ["scenario", "trigger", "impact", "prd_reference", "code_evidence"],
        properties: {
          scenario: { type: "string" },
          trigger: { type: "string" },
          impact: { type: "string" },
          prd_reference: { type: "string" },
          code_evidence: { type: "string" }
        }
      }
    },
    risk_assessment: {
      type: "object",
      required: ["overall_risk", "top_risks", "mitigation_suggestions"],
      properties: {
        overall_risk: { type: "string", enum: ["low","medium","high","critical"] },
        top_risks: { type: "array", items: { type: "string" } },
        mitigation_suggestions: { type: "array", items: { type: "string" } }
      }
    }
  }
};

const OPUS_SCHEMA = {
  type: "object",
  required: ["final_decision", "rationale", "scores", "dispute_resolution", "required_changes", "risk_verdict", "implementation_advice", "overall_assessment"],
  properties: {
    final_decision: { type: "string", enum: ["approve","conditional_approve","reject"] },
    rationale: { type: "string" },
    scores: {
      type: "object",
      required: ["factual_accuracy","architecture_compatibility","risk_exposure","executability","overall"],
      properties: {
        factual_accuracy: { type: "object", required: ["score","reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        architecture_compatibility: { type: "object", required: ["score","reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        risk_exposure: { type: "object", required: ["score","reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        executability: { type: "object", required: ["score","reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        overall: { type: "number", minimum: 1, maximum: 10 }
      }
    },
    dispute_resolution: {
      type: "object",
      required: ["haiku_correct","sonnet_correct","both_wrong","manual_review_needed"],
      properties: {
        haiku_correct: { type: "array", items: { type: "string" } },
        sonnet_correct: { type: "array", items: { type: "string" } },
        both_wrong: { type: "array", items: { type: "string" } },
        manual_review_needed: { type: "array", items: { type: "string" } }
      }
    },
    required_changes: {
      type: "array",
      items: {
        type: "object",
        required: ["prd","change","priority","reason"],
        properties: {
          prd: { type: "string" },
          change: { type: "string" },
          priority: { type: "string", enum: ["must_have","should_have","nice_to_have"] },
          reason: { type: "string" }
        }
      }
    },
    risk_verdict: {
      type: "object",
      required: ["agreed_risk_level", "disagreement_note"],
      properties: {
        agreed_risk_level: { type: "string", enum: ["low","medium","high","critical"] },
        disagreement_note: { type: "string" }
      }
    },
    implementation_advice: { type: "array", items: { type: "string" } },
    overall_assessment: { type: "string" }
  }
};

// ============================================================
// 工具函数
// ============================================================

function baseName(files) {
  const first = Array.isArray(files) ? files[0] : String(files);
  return first.replace(/^.*[\\/]/, '').replace(/\.md$/, '');
}

async function tryRead(path) {
  try {
    return await agent(`Read ${path}. Return content verbatim. If file not found, say FILE_NOT_FOUND.`, { label: path });
  } catch {
    return 'FILE_NOT_FOUND';
  }
}

// ============================================================
// 阶段 0: 加载
// ============================================================

async function loadDocuments() {
  phase('加载文档与上下文');

  const prdFiles = Array.isArray(args) ? args : (args ? [args] : []);
  if (!prdFiles.length) {
    throw new Error('未指定 PRD 文件。用法: Workflow({ name: "prd-review-judgment", args: ["docs/prd/xxx.md"] })');
  }

  const [prdContents, claudeMd, conventions, status] = await Promise.all([
    parallel(prdFiles.map(f => () => tryRead(f))),
    tryRead('CLAUDE.md'),
    tryRead('CLAUDE_CONVENTIONS.md'),
    tryRead('CLAUDE_STATUS.md')
  ]);

  const prds = {};
  prdFiles.forEach((f, i) => {
    if (prdContents[i] && prdContents[i] !== 'FILE_NOT_FOUND') prds[f] = prdContents[i];
  });
  if (!Object.keys(prds).length) throw new Error('所有指定文件都无法读取。');

  const projectContext = {};
  if (claudeMd !== 'FILE_NOT_FOUND') projectContext['CLAUDE.md'] = claudeMd;
  if (conventions !== 'FILE_NOT_FOUND') projectContext['CLAUDE_CONVENTIONS.md'] = conventions;
  if (status !== 'FILE_NOT_FOUND') projectContext['CLAUDE_STATUS.md'] = status;

  log(`✓ 已加载 ${Object.keys(prds).length} 个 PRD + ${Object.keys(projectContext).length} 个项目规范`);
  return { prds, projectContext };
}

// ============================================================
// 阶段 1: Haiku 广撒网
// ============================================================

async function haikuExplore(prds, projectContext) {
  phase('Haiku广撒网');

  const ctxBlock = Object.keys(projectContext).length
    ? `\n## 项目架构规范\n\n${Object.entries(projectContext).map(([k, v]) => `### ${k}\n\n${v}`).join('\n\n---\n\n')}`
    : '';

  const prdBlock = Object.entries(prds).map(([f, c], i) => `### PRD ${i + 1}: ${f}\n\n${c}`).join('\n\n---\n\n');

  return await agent(
    `你是 PRD 审阅的第一道关卡。你的任务是**广撒网**——全面探索 PRD 涉及的代码库，找出所有问题。

## PRD 文档

${prdBlock}
${ctxBlock}

## 你的工作流程

### 第一步：逐条验证技术声明（声明驱动，不全文探索）

1. 从 PRD 中提取所有对代码库的**具体技术声明**，按优先级排序：
   - **P0 文件路径**：PRD 引用的文件路径是否存在
   - **P1 接口签名**：类名、方法签名、接口定义是否与代码库一致
   - **P2 模块职责**：PRD 对模块职责的描述是否与 README 一致
   - **P3 依赖关系**：模块间的依赖关系是否正确

2. **只读验证每条声明所需的最小代码片段**，不要通读整个模块：
   - 验证路径 → 检查文件是否存在
   - 验证签名 → 只读对应接口/方法定义
   - 验证模块职责 → 只读 README 首段
   - 验证依赖 → 只读 import 段

3. 不相关的内容跳过。目标是验证 PRD 的声明，不是审查全部代码。

### 第二步：轻量纵深抽查（每个核心模块一个探针）

对 PRD 涉及的**核心模块**（声明数最多的前 3 个），每个多读一个文件验证整体一致性：
- 读 README 首段 → 确认模块职责描述和 PRD 一致
- 读主接口文件 → 确认对外 API 未被 PRD 遗漏或误述

### 第三步：基础审阅

1. **事实准确性**：逐条记录每个声明的验证结果（准确/不准确/无法验证）
2. **完整性**：是否遗漏关键章节？有 TODO/TBD/FIXME 吗？
3. **一致性**：PRD 内部是否有矛盾？
4. **规范对齐**：是否符合项目架构规范？

### 第四步：输出探索边界

- 你验证了哪些声明
- 哪些声明无法验证（需要下一阶段深入）
- 哪些模块你没探索及原因`,
    { label: 'Haiku广撒网', phase: 'Haiku广撒网', model: 'haiku', schema: HAIKU_SCHEMA }
  );
}

// ============================================================
// 阶段 2: Sonnet 精准打击
// ============================================================

async function sonnetStrike(prds, haikuResult) {
  phase('Sonnet精准打击');

  const prdBlock = Object.entries(prds).map(([f, c], i) => `### ${f}\n\n${c}`).join('\n\n---\n\n');

  // haikuResult 现在是 schema 验证过的对象，JSON.stringify 不会双转义
  const haikuJson = JSON.stringify(haikuResult, null, 2);

  return await agent(
    `你是第二道关卡——**精准打击**。Haiku 已经做了一次广撒网审阅，你的任务是利用 Haiku 的输出作为导航，做更深入的独立验证。

## PRD 文档

${prdBlock}

## Haiku 审阅结果（含探索边界）

${haikuJson}

## 你的工作流程（严格按此顺序）

### 第一步：验证 Haiku 的高严重度问题

1. 找出 Haiku 标记为 severity="high" 的所有问题
2. 针对每个 high 问题，**自己去读对应的代码文件**，独立判断 Haiku 是否正确
3. 记录你的验证结论和你的证据

### 第二步：检查 Haiku 的盲区

1. 看 Haiku 的 exploration_boundary.not_explored——这些是 Haiku 跳过的模块
2. 判断 Haiku 是否遗漏了关键模块——PRD 涉及但 Haiku 没探索的
3. 如果有遗漏，**自己去读这些模块**

### 第三步：QA 工程师对抗

现在你戴上 QA 工程师的帽子。你的目标是：**找到 3 个以上会让这个变更在生产环境出故障的具体场景**。

要求：
- 每个故障场景必须引用 PRD 中的**具体段落**
- 每个故障场景必须引用你或 Haiku 在代码库中发现的**具体证据**（文件路径、代码行）
- 说明：触发条件 → 影响范围 → 为什么 PRD 没有覆盖这个场景`,
    { label: 'Sonnet精准打击', phase: 'Sonnet精准打击', model: 'sonnet', schema: SONNET_SCHEMA }
  );
}

// ============================================================
// 阶段 3: Opus 裁决
// ============================================================

async function opusJudge(prds, haikuResult, sonnetResult) {
  phase('Opus裁决');

  const prdBlock = Object.entries(prds).map(([f, c], i) => `### ${f}\n\n${c}`).join('\n\n---\n\n');

  const claudeMd = await tryRead('CLAUDE.md');
  const ctxBlock = claudeMd !== 'FILE_NOT_FOUND'
    ? `\n## 项目架构规范\n\n${claudeMd}`
    : '';

  const haikuJson = JSON.stringify(haikuResult, null, 2);
  const sonnetJson = JSON.stringify(sonnetResult, null, 2);

  return await agent(
    `你是架构师，做**最终裁决**。

## 裁决法则

1. **证据优先**：有具体代码行级证据的一方胜出。双方都没有具体证据 → 标记"需人工验证"
2. **规范为纲**：以项目架构规范为最高判据。违反核心架构约束的 = reject
3. **不信任任何一方**：Haiku 可能错，Sonnet 也可能错。你的工作是独立判断

## PRD 文档

${prdBlock}
${ctxBlock}

## Haiku 审阅

${haikuJson}

## Sonnet 对抗审阅

${sonnetJson}

## 你的工作

### 第一步：裁定争议

对比 Haiku 和 Sonnet 的分歧点：
- 谁有更具体的代码证据？具体代码引用 > 模糊描述 > 无证据
- 标记双方证据都不足、无法裁定的点

### 第二步：四维度评分

| 维度 | 权重 | 评价标准 | 数据来源 |
|------|:---:|------|------|
| 事实准确性 | 30% | PRD 对代码库现状的描述是否准确 | Haiku + Sonnet 验证结论 |
| 架构兼容性 | 25% | 是否符合项目架构规范的六原则 | CLAUDE.md + 你的独立判断 |
| 风险暴露度 | 25% | Sonnet 找到的故障场景是否严重、PRD 是否已覆盖 | Sonnet failure_scenarios |
| 可执行性 | 20% | 实施步骤是否具体可执行、依赖是否合理 | PRD 原文 + 你的经验 |

每个维度 1-10 分，给出评分理由。不评"完整性"——那是 Haiku 的职责。

### 第三步：最终决策

- **approve**：四维度都 ≥6，无违反架构约束
- **conditional_approve**：1-2 个维度 <6，有 must_have 级修改项
- **reject**：多个关键维度 <6，或违反核心架构约束`,
    { label: 'Opus裁决', phase: 'Opus裁决', model: 'opus', schema: OPUS_SCHEMA }
  );
}

// ============================================================
// 阶段 4: 输出报告
// ============================================================

async function writeReport(judgment, prds, haikuResult, sonnetResult) {
  phase('输出报告');

  const bname = baseName(args);
  const outPath = `docs/prd/reviews/${bname}_review.md`;

  const prdFiles = Object.keys(prds);
  const riskEmoji = r => r === 'critical' ? '🔴' : r === 'high' ? '🟠' : r === 'medium' ? '🟡' : '🟢';
  const decisionEmoji = judgment.final_decision === 'approve'
    ? '✅ 批准' : judgment.final_decision === 'conditional_approve' ? '⚠️ 有条件批准' : '❌ 拒绝';

  // judgment/ haikuResult/ sonnetResult 现在是 schema 验证过的对象
  const report = `# PRD 审阅与评判报告

**审阅对象**: ${prdFiles.join(', ')}
**审阅链**: Haiku 广撒网 → Sonnet 精准打击 → Opus 裁决

---

## 最终决策

| 决策 | 综合评分 | 风险等级 |
|------|:---:|:---:|
| ${decisionEmoji} | **${judgment.scores.overall.toFixed(1)}/10** | ${riskEmoji(judgment.risk_verdict.agreed_risk_level)} ${judgment.risk_verdict.agreed_risk_level} |

**理由**: ${judgment.rationale}

---

## 四维度评分

| 维度 | 权重 | 评分 | 评语 |
|------|:---:|:---:|------|
| 事实准确性 | 30% | ${judgment.scores.factual_accuracy.score}/10 | ${judgment.scores.factual_accuracy.reason} |
| 架构兼容性 | 25% | ${judgment.scores.architecture_compatibility.score}/10 | ${judgment.scores.architecture_compatibility.reason} |
| 风险暴露度 | 25% | ${judgment.scores.risk_exposure.score}/10 | ${judgment.scores.risk_exposure.reason} |
| 可执行性 | 20% | ${judgment.scores.executability.score}/10 | ${judgment.scores.executability.reason} |

---

## 争议裁定

| 判定 | 内容 |
|------|------|
| Haiku 正确 | ${judgment.dispute_resolution.haiku_correct.map(s => `- ${s}`).join('\n') || '无'} |
| Sonnet 正确 | ${judgment.dispute_resolution.sonnet_correct.map(s => `- ${s}`).join('\n') || '无'} |
| 双方均不准确 | ${judgment.dispute_resolution.both_wrong.map(s => `- ${s}`).join('\n') || '无'} |
| 需人工验证 | ${judgment.dispute_resolution.manual_review_needed.map(s => `- ${s}`).join('\n') || '无'} |

---

## 必须修改的问题

${judgment.required_changes.length > 0
    ? judgment.required_changes.map(c =>
      `### ${c.prd}\n- **问题**: ${c.change}\n- **优先级**: ${c.priority === 'must_have' ? '🔴 必须修改' : c.priority === 'should_have' ? '🟡 应该修改' : '🟢 建议修改'}\n- **后果**: ${c.reason}\n`
    ).join('\n')
    : '✅ 无必须修改的问题'}

---

## 故障场景（Sonnet QA 对抗）

${sonnetResult.failure_scenarios.length > 0
    ? sonnetResult.failure_scenarios.map((f, i) =>
      `### 场景 ${i + 1}: ${f.scenario}\n- **触发条件**: ${f.trigger}\n- **影响范围**: ${f.impact}\n- **PRD 引用**: ${f.prd_reference}\n- **代码证据**: ${f.code_evidence}\n`
    ).join('\n')
    : '无故障场景被识别'}

---

## 风险总览

| 来源 | 风险等级 | 核心风险 |
|------|:---:|------|
| Sonnet 评估 | ${riskEmoji(sonnetResult.risk_level)} ${sonnetResult.risk_level} | ${sonnetResult.risk_assessment.top_risks.join('; ') || '无'} |
| Opus 裁定 | ${riskEmoji(judgment.risk_verdict.agreed_risk_level)} ${judgment.risk_verdict.agreed_risk_level} | ${judgment.risk_verdict.disagreement_note || '与 Sonnet 一致'} |

---

## 实施建议

${judgment.implementation_advice.map((a, i) => `${i + 1}. ${a}`).join('\n')}

---

## Haiku 审阅统计

| 类别 | 数量 |
|------|:---:|
| 总问题 | ${haikuResult.issues.length} |
| High 严重度 | ${haikuResult.issues.filter(i => i.severity === 'high').length} |
| Medium | ${haikuResult.issues.filter(i => i.severity === 'medium').length} |
| Low | ${haikuResult.issues.filter(i => i.severity === 'low').length} |
| PRD 间冲突 | ${haikuResult.cross_prd_conflicts.length} |
| 探索模块 | ${haikuResult.exploration_boundary.explored_modules.join(', ') || '未记录'} |
| 未探索 | ${haikuResult.exploration_boundary.not_explored.join(', ') || '无'} |

---

*报告由 prd-review-judgment 工作流自动生成*
`;

  // 通过 agent 写入磁盘 — 这是唯一落盘的文件
  await agent(
    `Write the following report to ${outPath}. Create directory if needed.
Write the content exactly as-is, starting from "# PRD 审阅" down to "工作流自动生成*".
Do NOT include "---CONTENT---" or "---END---" markers in the written file.

${report}`,
    { label: `写入报告 ${outPath}` }
  );

  log('');
  log(`📄 报告已写入: ${outPath}`);
  log('');

  return { reportPath: outPath, report, judgment, haikuResult, sonnetResult };
}

// ============================================================
// 主流程
// ============================================================

async function run() {
  log('🏛️  PRD 多代理审阅与评判');
  log(`   审阅链: Haiku 广撒网 → Sonnet 精准打击 → Opus 裁决`);
  log(`   文档: ${Array.isArray(args) ? args.join(', ') : args}`);
  log('');

  // 阶段 0: 加载
  const { prds, projectContext } = await loadDocuments();

  // 阶段 1: Haiku（schema 保证返回对象）
  const haikuResult = await haikuExplore(prds, projectContext);
  if (!haikuResult) { log('❌ Haiku 审阅失败'); return null; }
  log('✓ Haiku 广撒网完成');
  log(`  问题: ${haikuResult.issues.length} 个 (high: ${haikuResult.issues.filter(i => i.severity === 'high').length})`);
  log(`  探索: ${haikuResult.exploration_boundary.explored_modules.join(', ') || '未记录'}`);
  log(`  未探索: ${haikuResult.exploration_boundary.not_explored.join(', ') || '无'}`);
  log('');

  // 阶段 2: Sonnet（schema 保证返回对象）
  const sonnetResult = await sonnetStrike(prds, haikuResult);
  if (!sonnetResult) { log('❌ Sonnet 对抗审阅失败'); return null; }
  log('✓ Sonnet 精准打击完成');
  log(`  风险等级: ${sonnetResult.risk_level}`);
  log(`  故障场景: ${sonnetResult.failure_scenarios.length} 个`);
  log(`  Haiku 正确: ${sonnetResult.haiku_verification.haiku_correct.length} 项`);
  log(`  Haiku 错误: ${sonnetResult.haiku_verification.haiku_wrong.length} 项`);
  log(`  Haiku 盲区: ${sonnetResult.haiku_verification.haiku_blind_spots.length} 项`);
  log('');

  // 阶段 3: Opus（schema 保证返回对象）
  const judgment = await opusJudge(prds, haikuResult, sonnetResult);
  if (!judgment) { log('❌ Opus 裁决失败'); return null; }
  log('✓ Opus 裁决完成');
  log(`  决策: ${judgment.final_decision}`);
  log(`  综合评分: ${judgment.scores.overall.toFixed(1)}/10`);
  log(`  必须修改: ${judgment.required_changes.length} 项`);
  log('');

  // 阶段 4: 输出报告
  const result = await writeReport(judgment, prds, haikuResult, sonnetResult);

  return result;
}

return await run();
