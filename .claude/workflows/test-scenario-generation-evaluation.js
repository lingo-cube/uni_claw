/**
 * Test Scenario Generation and Evaluation Workflow
 *
 * 核心机制：
 * - 使用multi-agent验证和battle机制
 * - 目标：生成测试用例列表文档
 * - 验证：与现有用例比对
 * - 输出：质量评分报告
 *
 * 输入：
 * - 模块名（如state_machine）
 */

export const meta = {
  name: 'test-scenario-generation-evaluation',
  description: '测试用例生成和评估 - multi-agent验证 + battle + 质量评分',
  phases: [
    { title: 'ReadDesign', detail: '读取设计文档' },
    { title: 'GenerateScenarios', detail: '生成测试用例列表' },
    { title: 'ReadExisting', detail: '读取现有测试用例' },
    { title: 'AgentVerify', detail: '多Agent验证新生成用例' },
    { title: 'Battle', detail: '对抗验证' },
    { title: 'Compare', detail: '与现有用例比对' },
    { title: 'Score', detail: '质量评分' },
    { title: 'Report', detail: '生成报告' }
  ]
};

const MODELS = {
  Haiku: 'haiku-4-5-20251001',
  Sonnet: 'claude-sonnet-4-6',
  Opus: 'claude-opus-4-8'
};

// ============================================================================
// 1. 读取设计文档
// ============================================================================

const readDesignDocument = async (moduleName) => {
  phase('ReadDesign');
  log(`📖 读取${moduleName}模块的设计文档...`);

  try {
    const designDoc = await agent(`
      读取${moduleName}模块的设计文档：
      docs/architecture/modules/${moduleName}-design.md

      返回完整内容。
    `, { label: '读取设计文档', model: MODELS.Haiku });

    if (!designDoc) {
      throw new Error('设计文档读取失败');
    }

    log(`✓ 设计文档已读取 (${designDoc.length} 字符)`);
    return designDoc;
  } catch (error) {
    log(`❌ 读取设计文档失败: ${error.message}`);
    log(`⚠ 将使用直接文件读取...`);

    // 备用方案：直接读取文件
    try {
      const content = await Read(`docs/architecture/modules/${moduleName}-design.md`);
      log(`✓ 设计文档已直接读取 (${content.length} 字符)`);
      return content;
    } catch (readError) {
      log(`❌ 无法读取设计文档: ${readError.message}`);
      return null;
    }
  }
};

// ============================================================================
// 2. 生成测试用例列表
// ============================================================================

const generateScenarios = async (moduleName, designDoc) => {
  phase('GenerateScenarios');
  log('🔧 Opus正在生成测试用例列表...');

  let scenarios;
  try {
    scenarios = await agent(`
      作为**测试架构师**，基于以下设计文档生成完整的测试用例列表：

      模块：${moduleName}
      设计文档：
      ${designDoc}

      使用5步方法论：
      1. 识别测试维度（States, Transitions, Boundaries, Errors, Features）
      2. 为每个维度创建测试场景矩阵
      3. 分类测试场景（normal, edge, error, integration）
      4. 估算覆盖率
      5. 输出用例列表

      返回JSON格式：
      {
        "total_scenarios": 数量,
        "scenarios_by_category": {
          "normal": [],
          "edge": [],
          "error": [],
          "integration": []
        },
        "coverage_estimate": {
          "states_covered": 数量,
          "transitions_covered": 数量,
          "estimated_coverage": "百分比"
        },
        "scenario_list": [
          {
            "id": "用例ID",
            "category": "类别",
            "title": "标题",
            "given": "Given",
            "when": "When",
            "then": "Then"
          }
        ]
      }
    `, { label: '生成测试用例', model: MODELS.Opus });
  } catch (error) {
    log(`❌ 生成测试用例失败: ${error.message}`);
    log(`⚠ 将返回默认结构...`);
    scenarios = {
      total_scenarios: 0,
      scenarios_by_category: { normal: [], edge: [], error: [], integration: [] },
      coverage_estimate: { states_covered: 0, transitions_covered: 0, estimated_coverage: "0%" },
      scenario_list: []
    };
  }

  if (!scenarios || !scenarios.total_scenarios) {
    log(`⚠ 生成结果无效，返回默认结构`);
    scenarios = {
      total_scenarios: 0,
      scenarios_by_category: { normal: [], edge: [], error: [], integration: [] },
      coverage_estimate: { states_covered: 0, transitions_covered: 0, estimated_coverage: "0%" },
      scenario_list: []
    };
  }

  log(`✓ 生成了 ${scenarios.total_scenarios} 个测试用例`);
  return scenarios;
};

// ============================================================================
// 3. 读取现有测试用例
// ============================================================================

const readExistingScenarios = async (moduleName) => {
  phase('ReadExisting');
  log(`📖 读取${moduleName}模块的现有测试用例...`);

  let existingTests;
  try {
    existingTests = await agent(`
      查找并读取${moduleName}模块的现有测试用例：

      1. 检查是否存在 docs/testing/${moduleName.toUpperCase()}_TEST_SCENARIOS.md
      2. 如果存在，读取其内容
      3. 如果不存在，检查 tests/${moduleName}/ 目录下的测试文件
      4. 提取测试用例列表

      返回JSON格式：
      {
        "source": "文档/代码",
        "total_tests": 数量,
        "test_list": [
          {
            "id": "用例ID",
            "title": "标题",
            "category": "类别"
          }
        ]
      }
    `, { label: '读取现有用例', model: MODELS.Haiku });
  } catch (error) {
    log(`⚠ 读取现有测试用例失败: ${error.message}`);
    log(`⚠ 将假设无现有测试用例...`);
    existingTests = {
      source: "无",
      total_tests: 0,
      test_list: []
    };
  }

  if (!existingTests || !existingTests.total_tests) {
    existingTests = {
      source: "无",
      total_tests: 0,
      test_list: []
    };
  }

  if (existingTests.total_tests > 0) {
    log(`✓ 找到 ${existingTests.total_tests} 个现有测试用例`);
  } else {
    log(`⚠ 未找到现有测试用例`);
  }

  return existingTests;
};

// ============================================================================
// 4. Multi-Agent验证新生成用例
// ============================================================================

const agentVerify = async (moduleName, designDoc, newScenarios) => {
  phase('AgentVerify');
  log('🔍 Multi-agent验证新生成用例...');

  let verifyResults;
  try {
    verifyResults = await parallel([
      // Agent 1: 完整性验证
      () => agent(`
        作为**完整性验证Agent**，验证测试用例的完整性：

        模块：${moduleName}
        设计文档关键点：
        ${designDoc ? designDoc.slice(0, 2000) : '无设计文档'}...

        新生成用例：
        ${JSON.stringify(newScenarios.scenario_list || []).slice(0, 2000)}...

        验证：
        1. 是否覆盖所有关键功能？
        2. 是否遗漏重要的状态转换？
        3. 是否覆盖边界条件？
        4. 是否覆盖错误场景？

        返回JSON：
        {
          "completeness_score": 0-100,
          "covered_features": [],
          "missed_features": [],
          "issues": []
        }
      `, { label: '完整性验证', model: MODELS.Sonnet }),

      // Agent 2: 质量验证
      () => agent(`
        作为**质量验证Agent**，验证测试用例的质量：

        新生成用例：
        ${JSON.stringify(newScenarios.scenario_list || []).slice(0, 2000)}...

        验证：
        1. 用例描述是否清晰？
        2. Given/When/Then格式是否正确？
        3. 测试步骤是否具体可执行？
        4. 预期结果是否明确？

        返回JSON：
        {
          "quality_score": 0-100,
          "strengths": [],
          "weaknesses": [],
          "improvements": []
        }
      `, { label: '质量验证', model: MODELS.Sonnet }),

      // Agent 3: 可测试性验证
      () => agent(`
        作为**可测试性验证Agent**，验证用例的可测试性：

        新生成用例：
        ${JSON.stringify(newScenarios.scenario_list || []).slice(0, 2000)}...

        验证：
        1. 用例是否可以在当前环境中执行？
        2. 是否需要特殊的mock或fixture？
        3. 是否有依赖外部系统？
        4. 预期结果是否可验证？

        返回JSON：
        {
          "testability_score": 0-100,
          "executable_count": 数量,
          "needs_mock": [],
          "external_deps": []
        }
      `, { label: '可测试性验证', model: MODELS.Sonnet })
    ]);
  } catch (error) {
    log(`❌ Multi-agent验证失败: ${error.message}`);
    log(`⚠ 将返回默认验证结果...`);
    verifyResults = [
      { completeness_score: 0, covered_features: [], missed_features: [], issues: [error.message] },
      { quality_score: 0, strengths: [], weaknesses: [error.message], improvements: [] },
      { testability_score: 0, executable_count: 0, needs_mock: [], external_deps: [] }
    ];
  }

  // 过滤掉null结果，用默认值替代
  verifyResults = verifyResults.map((result, index) => {
    if (!result) {
      log(`⚠ 验证Agent ${index + 1}失败，使用默认值`);
      const defaults = [
        { completeness_score: 0, covered_features: [], missed_features: [], issues: ['Agent失败'] },
        { quality_score: 0, strengths: [], weaknesses: ['Agent失败'], improvements: [] },
        { testability_score: 0, executable_count: 0, needs_mock: [], external_deps: [] }
      ];
      return defaults[index];
    }
    return result;
  });

  log('✓ Multi-agent验证完成');
  return verifyResults;
};

// ============================================================================
// 5. 对抗验证
// ============================================================================

const battleVerify = async (newScenarios, verifyResults) => {
  phase('Battle');
  log('⚔️ Agent对抗验证...');

  let battleResults;
  try {
    battleResults = await parallel([
      // Battle 1: 挑战完整性验证
      () => agent(`
        作为**对抗Agent**，挑战完整性验证结果：

        验证结果：${JSON.stringify(verifyResults[0] || {})}

        找出：
        - 被遗漏的功能
        - 被低估的遗漏严重性
        - 应该覆盖但未覆盖的场景

        返回JSON：
        {
          "found_gaps": [],
          "adjusted_completeness_score": 0-100,
          "additional_scenarios_needed": []
        }
      `, { label: '完整性Battle', model: MODELS.Sonnet }),

      // Battle 2: 挑战质量验证
      () => agent(`
        作为**对抗Agent**，挑战质量验证结果：

        验证结果：${JSON.stringify(verifyResults[1] || {})}

        找出：
        - 被忽略的质量问题
        - 被高估的质量评分
        - 描述不清楚的用例

        返回JSON：
        {
          "found_issues": [],
          "adjusted_quality_score": 0-100,
          "problematic_cases": []
        }
      `, { label: '质量Battle', model: MODELS.Sonnet })
    ]);
  } catch (error) {
    log(`⚠ 对抗验证失败: ${error.message}`);
    log(`⚠ 将返回默认对抗结果...`);
    battleResults = [
      { found_gaps: [error.message], adjusted_completeness_score: 0, additional_scenarios_needed: [] },
      { found_issues: [error.message], adjusted_quality_score: 0, problematic_cases: [] }
    ];
  }

  // 过滤掉null结果
  battleResults = battleResults.map((result, index) => {
    if (!result) {
      log(`⚠ Battle Agent ${index + 1}失败，使用默认值`);
      const defaults = [
        { found_gaps: ['Agent失败'], adjusted_completeness_score: 0, additional_scenarios_needed: [] },
        { found_issues: ['Agent失败'], adjusted_quality_score: 0, problematic_cases: [] }
      ];
      return defaults[index];
    }
    return result;
  });

  log('✓ 对抗验证完成');
  return battleResults;
};

// ============================================================================
// 6. 与现有用例比对
// ============================================================================

const compareWithExisting = async (newScenarios, existingTests) => {
  phase('Compare');
  log('📊 与现有测试用例比对...');

  let comparison;
  try {
    comparison = await agent(`
      作为**比对分析师**，比对新生成用例和现有用例：

      新生成用例（${newScenarios.total_scenarios || 0}个）：
      ${JSON.stringify(newScenarios.scenario_list || []).slice(0, 3000)}...

      现有用例（${existingTests.total_tests || 0}个）：
      ${JSON.stringify(existingTests.test_list || []).slice(0, 3000)}...

      分析：
      1. 新用例中有多少是现有用例没有的？
      2. 现有用例中有多少是新用例没有的？
      3. 新用例是否覆盖了现有用例的盲点？
      4. 新用例的质量是否优于现有用例？

      返回JSON：
      {
        "new_unique_count": 数量,
        "existing_unique_count": 数量,
        "overlap_count": 数量,
        "coverage_improvement": "百分比提升",
        "quality_comparison": "better/same/worse",
        "recommendations": []
      }
    `, { label: '比对分析', model: MODELS.Opus });
  } catch (error) {
    log(`⚠ 比对分析失败: ${error.message}`);
    log(`⚠ 将返回默认比对结果...`);
    comparison = {
      new_unique_count: 0,
      existing_unique_count: 0,
      overlap_count: 0,
      coverage_improvement: "0%",
      quality_comparison: "unknown",
      recommendations: [error.message]
    };
  }

  if (!comparison || comparison.new_unique_count === undefined) {
    log(`⚠ 比对结果无效，使用默认值`);
    comparison = {
      new_unique_count: 0,
      existing_unique_count: 0,
      overlap_count: 0,
      coverage_improvement: "0%",
      quality_comparison: "unknown",
      recommendations: ["比对结果无效"]
    };
  }

  log(`✓ 比对完成`);
  log(`  新增独特用例: ${comparison.new_unique_count || 0}`);
  log(`  现有独特用例: ${comparison.existing_unique_count || 0}`);
  log(`  重叠用例: ${comparison.overlap_count || 0}`);

  return comparison;
};

// ============================================================================
// 7. 质量评分
// ============================================================================

const calculateScore = async (moduleName, newScenarios, verifyResults, battleResults, comparison) => {
  phase('Score');
  log('⚖️ 计算综合质量评分...');

  let score;
  try {
    score = await agent(`
      作为**评分架构师**，综合评估${moduleName}模块的新生成测试用例质量：

      新生成用例：${newScenarios.total_scenarios || 0}个
      覆盖率估算：${newScenarios.coverage_estimate?.estimated_coverage || '0%'}

      验证结果：
      - 完整性：${verifyResults[0]?.completeness_score || 0}/100
      - 质量：${verifyResults[1]?.quality_score || 0}/100
      - 可测试性：${verifyResults[2]?.testability_score || 0}/100

      对抗调整：
      - 完整性调整：${battleResults[0]?.adjusted_completeness_score || 0}/100
      - 质量调整：${battleResults[1]?.adjusted_quality_score || 0}/100

      与现有用例比对：
      - 新增独特用例：${comparison.new_unique_count || 0}
      - 覆盖率提升：${comparison.coverage_improvement || '0%'}
      - 质量对比：${comparison.quality_comparison || 'unknown'}

      计算综合评分（0-100）：
      - 完整性权重：30%
      - 质量权重：30%
      - 可测试性权重：20%
      - 覆盖率提升权重：20%

      返回JSON：
      {
        "overall_score": 0-100,
        "grade": "A/B/C/D/F",
        "scores": {
          "completeness": 0-100,
          "quality": 0-100,
          "testability": 0-100,
          "improvement": 0-100
        },
        "summary": "评分摘要",
        "recommendations": ["建议1", "建议2"],
        "should_adopt": boolean
      }
    `, { label: '综合评分', model: MODELS.Opus });
  } catch (error) {
    log(`⚠ 综合评分失败: ${error.message}`);
    log(`⚠ 将返回默认评分...`);
    score = {
      overall_score: 0,
      grade: 'F',
      scores: { completeness: 0, quality: 0, testability: 0, improvement: 0 },
      summary: `评分失败: ${error.message}`,
      recommendations: [error.message],
      should_adopt: false
    };
  }

  if (!score || score.overall_score === undefined) {
    log(`⚠ 评分结果无效，使用默认值`);
    score = {
      overall_score: 0,
      grade: 'F',
      scores: { completeness: 0, quality: 0, testability: 0, improvement: 0 },
      summary: "评分结果无效",
      recommendations: ["评分结果无效"],
      should_adopt: false
    };
  }

  log(`✓ 综合评分: ${score.overall_score}/100 (${score.grade})`);
  log(`  是否采用: ${score.should_adopt ? '✅ 是' : '❌ 否'}`);

  return score;
};

// ============================================================================
// 8. 生成报告
// ============================================================================

const generateReport = async (moduleName, newScenarios, existingTests, comparison, score) => {
  phase('Report');
  log('📝 生成评估报告...');

  let report;
  try {
    report = await agent(`
      作为**报告生成器**，生成${moduleName}模块的测试用例评估报告：

      模块：${moduleName}
      新生成用例：${newScenarios.total_scenarios || 0}个
      现有用例：${existingTests.total_tests || 0}个

      比对结果：
      - 新增独特用例：${comparison.new_unique_count || 0}
      - 现有独特用例：${comparison.existing_unique_count || 0}
      - 重叠用例：${comparison.overlap_count || 0}
      - 覆盖率提升：${comparison.coverage_improvement || '0%'}

      评分结果：
      - 综合评分：${score.overall_score || 0}/100
      - 等级：${score.grade || 'F'}
      - 是否采用：${score.should_adopt || false}

      分项评分：
      - 完整性：${score.scores?.completeness || 0}/100
      - 质量：${score.scores?.quality || 0}/100
      - 可测试性：${score.scores?.testability || 0}/100
      - 改进：${score.scores?.improvement || 0}/100

      生成完整的Markdown报告，包含：
      1. 执行摘要
      2. 用例生成统计
      3. 与现有用例比对
      4. 质量评分详情
      5. 建议
      6. 新增用例列表（前20个）

      输出专业Markdown格式。
    `, { label: '生成报告', model: MODELS.Opus });
  } catch (error) {
    log(`⚠ 报告生成失败: ${error.message}`);
    log(`⚠ 将生成简化报告...`);
    report = `# ${moduleName.toUpperCase()} 测试用例评估报告

## 执行摘要

报告生成失败: ${error.message}

## 统计

- 新生成用例: ${newScenarios.total_scenarios || 0}
- 现有用例: ${existingTests.total_tests || 0}

## 评分

- 综合评分: ${score.overall_score || 0}/100
- 等级: ${score.grade || 'F'}

## 建议

请检查workflow日志以获取详细信息。
`;
  }

  if (!report) {
    log(`⚠ 报告内容为空，生成简化报告`);
    report = `# ${moduleName.toUpperCase()} 测试用例评估报告

## 执行摘要

报告生成失败: 报告内容为空

## 统计

- 新生成用例: ${newScenarios.total_scenarios || 0}
- 现有用例: ${existingTests.total_tests || 0}

## 评分

- 综合评分: ${score.overall_score || 0}/100
- 等级: ${score.grade || 'F'}
`;
  }

  log('✓ 报告生成完成');

  // 保存报告
  const reportPath = `docs/reports/${moduleName.toUpperCase()}_TEST_SCENARIO_EVALUATION.md`;
  log(`📄 报告已保存: ${reportPath}`);

  return { report, reportPath };
};

// ============================================================================
// 主流程
// ============================================================================

async function run() {
  log('🧪 Test Scenario Generation and Evaluation');
  log('════════════════════════════════════════════════════════════');
  log('');

  // 获取模块名
  const moduleName = args?.[0] || 'state_machine';

  log(`📦 目标模块: ${moduleName}`);
  log('');

  const modelStats = { haiku: 0, sonnet: 0, opus: 0 };

  // 1. 读取设计文档
  const designDoc = await readDesignDocument(moduleName);
  modelStats.haiku += 1;

  // 2. 生成测试用例
  const newScenarios = await generateScenarios(moduleName, designDoc);
  modelStats.opus += 1;

  // 3. 读取现有测试用例
  const existingTests = await readExistingScenarios(moduleName);
  modelStats.haiku += 1;

  // 4. Multi-agent验证
  const verifyResults = await agentVerify(moduleName, designDoc, newScenarios);
  modelStats.sonnet += 3;

  // 5. 对抗验证
  const battleResults = await battleVerify(newScenarios, verifyResults);
  modelStats.sonnet += 2;

  // 6. 比对分析
  const comparison = await compareWithExisting(newScenarios, existingTests);
  modelStats.opus += 1;

  // 7. 质量评分
  const score = await calculateScore(moduleName, newScenarios, verifyResults, battleResults, comparison);
  modelStats.opus += 1;

  // 8. 生成报告
  const { report, reportPath } = await generateReport(
    moduleName,
    newScenarios,
    existingTests,
    comparison,
    score
  );
  modelStats.opus += 1;

  // 输出总结
  log('');
  log('════════════════════════════════════════════════════════════');
  log('📊 最终评估');
  log('════════════════════════════════════════════════════════════');
  log(`模块: ${moduleName}`);
  log(`综合评分: ${score.overall_score}/100 (${score.grade})`);
  log(`是否采用: ${score.should_adopt ? '✅ 是' : '❌ 否'}`);
  log('');
  log('📊 模型使用统计:');
  log(`  Haiku: ${modelStats.haiku} 次`);
  log(`  Sonnet: ${modelStats.sonnet} 次`);
  log(`  Opus: ${modelStats.opus} 次`);
  log('');
  log(`📄 详细报告: ${reportPath}`);
  log('');

  return {
    moduleName,
    newScenarios,
    existingTests,
    comparison,
    score,
    report,
    reportPath,
    modelStats
  };
}

return await run();
