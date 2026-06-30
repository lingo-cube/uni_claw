/**
 * 代码对抗审查工作流
 *
 * 用法: Workflow({ name: "code-review", args: ["<change-name>"] })
 *       Workflow({ name: "code-review", args: ["<change-name>", "--incremental"] })
 *
 * 流程：
 *    Haiku 逐 task 核对 + diff 行级审计 → Sonnet 回归链 + 六原则 QA → Opus 最终裁决
 *
 * 输入：
 *    OpenSpec change 名称 → 自动解析 artifacts + git diff
 *
 * 产出：openspec/changes/{name}/code-review.md
 */

export const meta = {
  name: 'code-review',
  description: '代码对抗审查 - Haiku逐task核对，Sonnet回归链+原则审计，Opus裁决',
  phases: [
    { title: '加载变更上下文', detail: '读取 git diff + OpenSpec artifacts + 项目规范' },
    { title: 'Haiku实施核对', detail: '逐 task 核对 + diff 范围约定审计' },
    { title: 'Sonnet回归攻击', detail: '回归链追踪 + 六原则审计 + QA 故障' },
    { title: 'Opus架构裁决', detail: '证据裁定 + 四维度评分' },
    { title: '输出报告', detail: '报告写入 openspec/changes/' }
  ]
};

// ============================================================
// Schemas
// ============================================================

const HAIKU_SCHEMA = {
  type: "object",
  required: ["review_summary", "task_coverage", "issues", "convention_lag_report", "coverage_boundary"],
  properties: {
    review_summary: { type: "string" },
    task_coverage: {
      type: "array",
      items: {
        type: "object",
        required: ["task_id", "task_desc", "status", "evidence"],
        properties: {
          task_id: { type: "string" },
          task_desc: { type: "string" },
          status: { type: "string", enum: ["matched", "partial", "missing", "extra_diff_without_task"] },
          evidence: { type: "string" }
        }
      }
    },
    issues: {
      type: "array",
      items: {
        type: "object",
        required: ["file", "line", "issue", "category", "severity", "evidence", "suggestion"],
        properties: {
          file: { type: "string" },
          line: { type: "string" },
          issue: { type: "string" },
          category: { type: "string", enum: ["convention_violation", "missing_test", "design_deviation", "bug_risk", "unclear"] },
          severity: { type: "string", enum: ["high", "medium", "low"] },
          evidence: { type: "string" },
          suggestion: { type: "string" }
        }
      }
    },
    convention_lag_report: {
      type: "object",
      required: ["rules_enforced", "rules_lagged"],
      properties: {
        rules_enforced: {
          type: "array",
          items: { type: "object", required: ["rule", "sample_evidence"], properties: { rule: { type: "string" }, sample_evidence: { type: "string" } } }
        },
        rules_lagged: {
          type: "array",
          items: { type: "object", required: ["rule", "sample_evidence"], properties: { rule: { type: "string" }, sample_evidence: { type: "string" } } }
        }
      }
    },
    coverage_boundary: {
      type: "object",
      required: ["diff_files_examined", "not_examined"],
      properties: {
        diff_files_examined: { type: "array", items: { type: "string" } },
        not_examined: { type: "array", items: { type: "string" } },
        reason_skipped: { type: "string" }
      }
    }
  }
};

const SONNET_SCHEMA = {
  type: "object",
  required: ["adversary_summary", "risk_level", "haiku_verification", "regression_chain", "principle_audit", "failure_scenarios"],
  properties: {
    adversary_summary: { type: "string" },
    risk_level: { type: "string", enum: ["low", "medium", "high", "critical"] },
    haiku_verification: {
      type: "object",
      required: ["haiku_correct", "haiku_wrong", "haiku_blind_spots"],
      properties: {
        haiku_correct: { type: "array", items: { type: "string" } },
        haiku_wrong: { type: "array", items: { type: "string" } },
        haiku_blind_spots: { type: "array", items: { type: "string" } }
      }
    },
    regression_chain: {
      type: "array",
      items: {
        type: "object",
        required: ["changed_api", "downstream_file", "impact", "likely_break"],
        properties: {
          changed_api: { type: "string" },
          downstream_file: { type: "string" },
          impact: { type: "string" },
          likely_break: { type: "string", enum: ["yes", "no", "needs_verification"] },
          discovery_method: { type: "string", enum: ["import_grep", "di_registration", "reflection", "convention"] }
        }
      }
    },
    principle_audit: {
      type: "array",
      items: {
        type: "object",
        required: ["principle", "verdict", "evidence"],
        properties: {
          principle: { type: "string", enum: ["interface_first", "dependency_injection", "immutability", "state_separation", "observability", "test_first"] },
          verdict: { type: "string", enum: ["pass", "partial_violation", "clear_violation"] },
          evidence: { type: "string" },
          code_ref: { type: "string" }
        }
      }
    },
    failure_scenarios: {
      type: "array",
      items: {
        type: "object",
        required: ["scenario", "trigger", "impact", "code_evidence"],
        properties: {
          scenario: { type: "string" },
          trigger: { type: "string" },
          impact: { type: "string" },
          code_evidence: { type: "string" }
        }
      }
    }
  }
};

const OPUS_SCHEMA = {
  type: "object",
  required: ["final_decision", "rationale", "scores", "dispute_resolution", "principle_verdict", "required_fixes", "overall_assessment"],
  properties: {
    final_decision: { type: "string", enum: ["approve", "conditional_approve", "reject"] },
    rationale: { type: "string" },
    scores: {
      type: "object",
      required: ["design_consistency", "regression_safety", "convention_compliance", "maintainability", "overall"],
      properties: {
        design_consistency: { type: "object", required: ["score", "reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        regression_safety: { type: "object", required: ["score", "reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        convention_compliance: { type: "object", required: ["score", "reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        maintainability: { type: "object", required: ["score", "reason"], properties: { score: { type: "number", minimum: 1, maximum: 10 }, reason: { type: "string" } } },
        overall: { type: "number", minimum: 1, maximum: 10 }
      }
    },
    dispute_resolution: {
      type: "object",
      required: ["haiku_correct", "sonnet_correct", "both_wrong", "manual_review_needed"],
      properties: {
        haiku_correct: { type: "array", items: { type: "string" } },
        sonnet_correct: { type: "array", items: { type: "string" } },
        both_wrong: { type: "array", items: { type: "string" } },
        manual_review_needed: { type: "array", items: { type: "string" } }
      }
    },
    principle_verdict: {
      type: "array",
      items: {
        type: "object",
        required: ["principle", "final_verdict", "action"],
        properties: {
          principle: { type: "string" },
          final_verdict: { type: "string", enum: ["block", "tolerate", "lagged_convention", "pass"] },
          action: { type: "string" }
        }
      }
    },
    required_fixes: {
      type: "array",
      items: {
        type: "object",
        required: ["file", "fix", "priority", "reason"],
        properties: {
          file: { type: "string" },
          fix: { type: "string" },
          priority: { type: "string", enum: ["must_have", "should_have", "nice_to_have"] },
          reason: { type: "string" }
        }
      }
    },
    overall_assessment: { type: "string" }
  }
};

// ============================================================
// 工具
// ============================================================

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

async function loadContext() {
  phase('加载变更上下文');

  const changeName = Array.isArray(args) ? args[0] : String(args || '').replace(/^--/, '');
  const isIncremental = Array.isArray(args) && args.includes('--incremental');

  if (!changeName || changeName.startsWith('-')) {
    throw new Error('用法: Workflow({ name: "code-review", args: ["<change-name>"] })');
  }

  log(`变更: ${changeName}${isIncremental ? ' (增量模式)' : ''}`);

  // 并行加载
  const [diff, tasks, design, proposal, claudeMd, conventions] = await Promise.all([
    agent('Run: git diff main...HEAD -- . :(exclude)docs/ :(exclude)temp/ | head -2000. Return the output.', { label: 'git diff' }),
    tryRead(`openspec/changes/${changeName}/tasks.md`),
    tryRead(`openspec/changes/${changeName}/design.md`),
    tryRead(`openspec/changes/${changeName}/proposal.md`),
    tryRead('CLAUDE.md'),
    tryRead('CLAUDE_CONVENTIONS.md')
  ]);

  const ctx = {
    changeName,
    isIncremental,
    diff: diff !== 'FILE_NOT_FOUND' ? diff : 'NO_DIFF',
    tasks: tasks !== 'FILE_NOT_FOUND' ? tasks : null,
    design: design !== 'FILE_NOT_FOUND' ? design : null,
    proposal: proposal !== 'FILE_NOT_FOUND' ? proposal : null,
    claudeMd: claudeMd !== 'FILE_NOT_FOUND' ? claudeMd : null,
    conventions: conventions !== 'FILE_NOT_FOUND' ? conventions : null
  };

  if (!ctx.diff || ctx.diff === 'NO_DIFF') {
    log('⚠️  无 git diff，可能没有变更或分支已合并');
  }
  if (!ctx.tasks) log('⚠️  未找到 tasks.md');
  if (!ctx.claudeMd) log('⚠️  未找到 CLAUDE.md');

  log(`✓ diff: ${ctx.diff.length} 字符 | tasks: ${ctx.tasks ? 'Y' : 'N'} | design: ${ctx.design ? 'Y' : 'N'} | CLAUDE.md: ${ctx.claudeMd ? 'Y' : 'N'}`);
  return ctx;
}

// ============================================================
// 阶段 1: Haiku 逐 task 核对 + diff 范围审计
// ============================================================

async function haikuAudit(ctx) {
  phase('Haiku实施核对');

  const incrementalNote = ctx.isIncremental
    ? '\n## 增量模式\n只检查上一轮 required_fixes 涉及的代码变更，不重新审计所有文件。'
    : '';

  return await agent(
    `你是代码审查的第一道关卡。任务：**逐 task 核对实施质量 + diff 范围内约定审计**。

${ctx.tasks ? `## Tasks.md\n\n${ctx.tasks}` : '## Tasks 缺失\n依赖 proposal + design + git diff 推断实施意图。'}

${ctx.design ? `## Design.md\n\n${ctx.design}` : ''}

## Git Diff（实际变更）

\`\`\`diff
${ctx.diff}
\`\`\`
${incrementalNote}
${ctx.claudeMd ? `## 项目架构\n\n${ctx.claudeMd}` : ''}
${ctx.conventions ? `## 代码约定\n\n${ctx.conventions}` : ''}

## 工作流程

### 第一步：约定有效性采样（出发前）

1. 读 CLAUDE_CONVENTIONS → 提取可自动检查的规则（类型注解、命名、import 顺序、测试命名等）
2. 对每条规则，在 diff 涉及的模块的**现有代码**中采样 5-10 处
3. 标注：enforced（实际遵循）/ lagged（已被广泛忽略）

### 第二步：逐 task 核对（只读 diff 中标记为 + 的代码行）

1. 读 tasks.md → 提取每个 task 的描述 + 预期文件
2. 对每个 task，从 git diff 找到对应改动 → 验证实现是否匹配 task 描述
3. 标注：matched / partial / missing / extra_diff_without_task

### 第三步：diff 范围约定审计（只用 enforced 规则）

**只审计 git diff 中标记为 + 的代码行和它们所在的函数/类。** 不改动的旧代码不审计。

1. 类型注解？命名规范？DI 注入？Import 顺序？
2. 每条违规引用具体的 diff 行号

### 第四步：关键组件检查

对照 CLAUDE.md Module Map → 如果改动触及关键组件或 V6 新模块：
- 标记为 high risk
- 读该组件的主接口 → 验证改动未破坏契约

## 输出格式

返回 JSON：
{
  "review_summary": "审计总结（2-3 句话）",
  "task_coverage": [
    { "task_id": "T1.1", "task_desc": "...", "status": "matched/partial/missing/extra_diff_without_task", "evidence": "..." }
  ],
  "issues": [
    { "file": "src/...", "line": "+L42", "issue": "...", "category": "convention_violation/missing_test/design_deviation/bug_risk/unclear", "severity": "high/medium/low", "evidence": "具体代码", "suggestion": "..." }
  ],
  "convention_lag_report": {
    "rules_enforced": [{ "rule": "...", "sample_evidence": "3/5 遵循" }],
    "rules_lagged": [{ "rule": "...", "sample_evidence": "0/5 遵循，整个模块忽略此规则" }]
  },
  "coverage_boundary": {
    "diff_files_examined": ["...这解释了如何实现..."],
    "not_examined": ["..."],
    "reason_skipped": "..."
  }
}`,
    { label: 'Haiku实施核对', phase: 'Haiku实施核对', model: 'haiku', schema: HAIKU_SCHEMA }
  );
}

// ============================================================
// 阶段 2: Sonnet 回归链 + 原则审计
// ============================================================

async function sonnetAttack(ctx, haikuResult) {
  phase('Sonnet回归攻击');

  const haikuJson = JSON.stringify(haikuResult, null, 2);

  return await agent(
    `你是第二道关卡——**回归攻击 + 原则审计**。

${ctx.proposal ? `## Proposal（原始意图）\n\n${ctx.proposal}` : ''}

## Git Diff

\`\`\`diff
${ctx.diff}
\`\`\`

## Haiku 审计结果

${haikuJson}

${ctx.claudeMd ? `## 项目架构（判据）\n\n${ctx.claudeMd}` : ''}

## 工作流程（严格按此顺序）

### 第一步：验证 Haiku 的高严重度问题

Haiku severity="high" 的问题 → 各自独立读代码验证 → 记录 haiku_correct / haiku_wrong

### 第二步：回归链追踪

1. 从 git diff 提取所有被改动的**公共接口**（类名、方法签名、接口定义）
2. 从三个维度搜下游：
   - **import grep**: 谁 import/调用 了这些？
   - **DI 注册**: 搜 services.AddSingleton/AddScoped/AddTransient/RegisterType
   - **反射**: 搜 getattr/importlib/Type.GetType/Activator.CreateInstance
3. 对每个下游 → 读其代码 → 判断变更是否会断裂
4. 核心模块被改？追两层传递依赖

### 第三步：六原则逐条审计（硬约束，不依赖约定）

逐条追问：
1. **接口优先**: 新增/改动的模块有接口吗？消费者依赖接口还是实现？
2. **依赖注入**: 构造函数依赖可替换吗？有没有 hard-instantiate？
3. **不可变性**: 新增 model 是 record 吗？集合字段是 IReadOnlyList 吗？
4. **状态分离**: 状态管理和业务逻辑分开了吗？
5. **可观测性**: diff 中有 trace/metric 埋点吗？
6. **测试覆盖率**: 新增/改动的方法有对应测试吗？

每一条: pass / partial_violation / clear_violation + 代码行证据

### 第四步：QA 故障场景

基于以上全部发现 → 构造 3+ 具体故障场景
每个场景: 触发条件 + 代码行证据 + 影响链

## 输出格式

返回 JSON：
{
  "adversary_summary": "对抗审阅总结",
  "risk_level": "low/medium/high/critical",
  "haiku_verification": {
    "haiku_correct": ["Haiku 正确指出的问题 + 验证证据"],
    "haiku_wrong": ["Haiku 判断错误 + 反证"],
    "haiku_blind_spots": ["Haiku 遗漏的关键问题 + 代码证据"]
  },
  "regression_chain": [
    {
      "changed_api": "被改的接口/类",
      "downstream_file": "下游文件",
      "impact": "影响描述",
      "likely_break": "yes/no/needs_verification",
      "discovery_method": "import_grep/di_registration/reflection/convention"
    }
  ],
  "principle_audit": [
    { "principle": "interface_first/dependency_injection/immutability/state_separation/observability/test_first", "verdict": "pass/partial_violation/clear_violation", "evidence": "代码行证据", "code_ref": "文件:行号" }
  ],
  "failure_scenarios": [
    { "scenario": "...", "trigger": "...", "impact": "...", "code_evidence": "..." }
  ]
}`,
    { label: 'Sonnet回归攻击', phase: 'Sonnet回归攻击', model: 'sonnet', schema: SONNET_SCHEMA }
  );
}

// ============================================================
// 阶段 3: Opus 架构裁决
// ============================================================

async function opusJudge(ctx, haikuResult, sonnetResult) {
  phase('Opus架构裁决');

  const haikuJson = JSON.stringify(haikuResult, null, 2);
  const sonnetJson = JSON.stringify(sonnetResult, null, 2);

  return await agent(
    `你是架构师，做**最终裁决**。

## 裁决法则

1. **证据优先**：有具体代码行证据的一方胜出。双方证据都不足 → 标记「人工复核」
2. **原则为纲**：六原则是硬约束。违反核心原则 = reject
3. **约定滞后不追责**：被实际代码广泛忽略的约定不是违规

${ctx.design ? `## Design.md（设计蓝本）\n\n${ctx.design}` : ''}

${ctx.proposal ? `## Proposal（原始意图）\n\n${ctx.proposal}` : ''}

${ctx.claudeMd ? `## 项目架构\n\n${ctx.claudeMd}` : ''}

## Haiku 审计

${haikuJson}

## Sonnet 对抗审阅

${sonnetJson}

## Git Diff

\`\`\`diff
${ctx.diff}
\`\`\`

## 你的工作

### 第一步：争议裁定

Haiku 和 Sonnet 的分歧点 → 有代码证据者胜 → 证据不足自己去读代码 → 还不行标记人工

### 第二步：原则终判

审阅 Sonnet 的 principle_audit → 对每个 clear_violation:
- **block**（阻塞合并）：违反核心架构约束
- **tolerate**（容忍）：实际问题但此次变更范围外，另建整改任务
- **lagged_convention**（约定滞后）：规则已被广泛忽略，不追责
- **pass**（通过）

### 第三步：设计一致性

对比 design.md 和实际实现：接口签名一致？架构分层遵守？偏离是否合理？

### 第四步：四维度评分

| 维度 | 权重 | 评价标准 |
|------|:---:|------|
| 设计一致性 | 30% | 实施是否匹配 design.md 的接口/架构 |
| 回归安全性 | 30% | Sonnet 回归链和故障场景的严重程度 |
| 规范合规性 | 20% | 基于 enforced 规则的审计结果 |
| 可维护性 | 20% | 测试覆盖 + 代码清晰度 + 文档 |

### 第五步：最终决策

- **approve**：四维度 ≥6，无违反核心架构约束
- **conditional_approve**：1-2 个 must_have 修复项
- **reject**：违反核心架构约束或多维度 <6

## 输出格式

返回 JSON：
{
  "final_decision": "approve/conditional_approve/reject",
  "rationale": "决策理由（3-5 句话）",
  "scores": {
    "design_consistency": { "score": 8, "reason": "..." },
    "regression_safety": { "score": 7, "reason": "..." },
    "convention_compliance": { "score": 6, "reason": "..." },
    "maintainability": { "score": 7, "reason": "..." },
    "overall": 7.0
  },
  "dispute_resolution": {
    "haiku_correct": ["Haiku 正确、Sonnet 错误的点"],
    "sonnet_correct": ["Sonnet 正确、Haiku 错误的点"],
    "both_wrong": ["双方都不准确的判断"],
    "manual_review_needed": ["证据都不足的点"]
  },
  "principle_verdict": [
    { "principle": "interface_first", "final_verdict": "block/tolerate/lagged_convention/pass", "action": "..." }
  ],
  "required_fixes": [
    { "file": "...", "fix": "...", "priority": "must_have/should_have/nice_to_have", "reason": "..." }
  ],
  "overall_assessment": "总体评价（3-5 句话）"
}`,
    { label: 'Opus架构裁决', phase: 'Opus架构裁决', model: 'opus', schema: OPUS_SCHEMA }
  );
}

// ============================================================
// 阶段 4: 输出报告
// ============================================================

async function writeReport(ctx, judgment, haikuResult, sonnetResult) {
  phase('输出报告');

  const outPath = `openspec/changes/${ctx.changeName}/code-review.md`;

  const riskEmoji = r => r === 'critical' ? '🔴' : r === 'high' ? '🟠' : r === 'medium' ? '🟡' : '🟢';
  const decisionEmoji = judgment.final_decision === 'approve'
    ? '✅ 批准' : judgment.final_decision === 'conditional_approve' ? '⚠️ 有条件批准' : '❌ 拒绝';

  const report = `# 代码审查报告

**变更**: ${ctx.changeName}
**审阅链**: Haiku 逐 task 核对 → Sonnet 回归链 + 原则审计 → Opus 裁决

---

## 最终决策

| 决策 | 综合评分 | 风险等级 |
|------|:---:|:---:|
| ${decisionEmoji} | **${judgment.scores.overall.toFixed(1)}/10** | ${riskEmoji(sonnetResult.risk_level)} ${sonnetResult.risk_level} |

**理由**: ${judgment.rationale}

---

## 四维度评分

| 维度 | 权重 | 评分 | 评语 |
|------|:---:|:---:|------|
| 设计一致性 | 30% | ${judgment.scores.design_consistency.score}/10 | ${judgment.scores.design_consistency.reason} |
| 回归安全性 | 30% | ${judgment.scores.regression_safety.score}/10 | ${judgment.scores.regression_safety.reason} |
| 规范合规性 | 20% | ${judgment.scores.convention_compliance.score}/10 | ${judgment.scores.convention_compliance.reason} |
| 可维护性 | 20% | ${judgment.scores.maintainability.score}/10 | ${judgment.scores.maintainability.reason} |

---

## 原则裁定

| 原则 | 裁决 | 行动 |
|------|------|------|
${judgment.principle_verdict.map(p => `| ${p.principle} | ${p.final_verdict === 'block' ? '🔴 阻塞' : p.final_verdict === 'tolerate' ? '🟡 容忍' : p.final_verdict === 'lagged_convention' ? '⚪ 约定滞后' : '🟢 通过'} | ${p.action} |`).join('\n')}

---

## 回归链分析

${sonnetResult.regression_chain.length > 0
    ? sonnetResult.regression_chain.map((r, i) =>
      `### 链 ${i + 1}: ${r.changed_api}\n- **下游**: ${r.downstream_file}\n- **影响**: ${r.impact}\n- **断裂风险**: ${r.likely_break}\n- **发现方式**: ${r.discovery_method}\n`
    ).join('\n')
    : '无回归链发现'}

---

## 故障场景

${sonnetResult.failure_scenarios.length > 0
    ? sonnetResult.failure_scenarios.map((f, i) =>
      `### 场景 ${i + 1}: ${f.scenario}\n- **触发**: ${f.trigger}\n- **影响**: ${f.impact}\n- **代码证据**: ${f.code_evidence}\n`
    ).join('\n')
    : '无故障场景被识别'}

---

## 必须修复

${judgment.required_fixes.length > 0
    ? judgment.required_fixes.map(f =>
      `### ${f.file}\n- **修复**: ${f.fix}\n- **优先级**: ${f.priority === 'must_have' ? '🔴 必须' : f.priority === 'should_have' ? '🟡 应该' : '🟢 建议'}\n- **原因**: ${f.reason}\n`
    ).join('\n')
    : '✅ 无必须修复的问题'}

---

## 争议裁定

| 判定 | 内容 |
|------|------|
| Haiku 正确 | ${judgment.dispute_resolution.haiku_correct.map(s => `- ${s}`).join('\n') || '无'} |
| Sonnet 正确 | ${judgment.dispute_resolution.sonnet_correct.map(s => `- ${s}`).join('\n') || '无'} |
| 双方不准确 | ${judgment.dispute_resolution.both_wrong.map(s => `- ${s}`).join('\n') || '无'} |
| 需人工 | ${judgment.dispute_resolution.manual_review_needed.map(s => `- ${s}`).join('\n') || '无'} |

---

## Haiku 审计统计

| 类别 | 数量 |
|------|:---:|
| 总问题 | ${haikuResult.issues.length} |
| High | ${haikuResult.issues.filter(i => i.severity === 'high').length} |
| Diff 文件 | ${haikuResult.coverage_boundary.diff_files_examined.join(', ') || '未记录'} |
| 未检查 | ${haikuResult.coverage_boundary.not_examined.join(', ') || '无'} |
| 约定滞后 | ${haikuResult.convention_lag_report.rules_lagged.map(r => r.rule).join(', ') || '无'} |

---

*报告由 code-review 工作流自动生成*
`;

  await agent(
    `Write the following report to ${outPath}. Create directory if needed.
Write content exactly as-is, starting from "# 代码审查报告" to "工作流自动生成*".

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
  log('🔍 代码对抗审查');
  log('   审阅链: Haiku 逐 task 核对 → Sonnet 回归链 + 原则审计 → Opus 裁决');
  log('');

  // 阶段 0
  const ctx = await loadContext();

  // 阶段 1: Haiku
  const haikuResult = await haikuAudit(ctx);
  if (!haikuResult) { log('❌ Haiku 审计失败'); return null; }
  log('✓ Haiku 审计完成');
  log(`  问题: ${haikuResult.issues.length} 个 (high: ${haikuResult.issues.filter(i => i.severity === 'high').length})`);
  log(`  task 覆盖: ${haikuResult.task_coverage.filter(t => t.status === 'matched').length}/${haikuResult.task_coverage.length} matched`);
  log(`  约定滞后: ${haikuResult.convention_lag_report.rules_lagged.length} 条`);
  log('');

  // 阶段 2: Sonnet
  const sonnetResult = await sonnetAttack(ctx, haikuResult);
  if (!sonnetResult) { log('❌ Sonnet 对抗审阅失败'); return null; }
  log('✓ Sonnet 回归攻击完成');
  log(`  回归链: ${sonnetResult.regression_chain.length} 条`);
  log(`  原则违规: ${sonnetResult.principle_audit.filter(p => p.verdict === 'clear_violation').length} 条`);
  log(`  故障场景: ${sonnetResult.failure_scenarios.length} 个`);
  log('');

  // 阶段 3: Opus
  const judgment = await opusJudge(ctx, haikuResult, sonnetResult);
  if (!judgment) { log('❌ Opus 裁决失败'); return null; }
  log('✓ Opus 裁决完成');
  log(`  决策: ${judgment.final_decision}`);
  log(`  综合评分: ${judgment.scores.overall.toFixed(1)}/10`);
  log(`  必须修复: ${judgment.required_fixes.length} 项`);
  log('');

  // 阶段 4: 输出
  const result = await writeReport(ctx, judgment, haikuResult, sonnetResult);

  return result;
}

return await run();
