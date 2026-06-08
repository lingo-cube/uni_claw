/**
 * Integrated Test Generation Workflow
 *
 * 可靠的测试生成流程 - 确保每个环节都能实际执行
 *
 * 核心闭环：
 * 1. 检查设计文档是否有测试场景章节
 * 2. 如果没有，执行test-extraction流程
 * 3. 基于设计文档和测试规则，执行multi-agent测试生成
 *
 * 依赖说明：
 * - 不依赖外部程序执行（如Node.js的rule-engine.js）
 * - 依赖Agent理解和应用规则文本
 * - 所有文件都通过Agent读取和分析
 */

export const meta = {
  name: 'integrated-test-gen',
  description: '可靠的集成测试生成：检查→提取→生成→验证',
  phases: [
    { title: 'Check', detail: '检查设计文档状态' },
    { title: 'Extract', detail: '提取测试场景（如需要）' },
    { title: 'Generate', detail: '生成测试代码' },
    { title: 'Verify', detail: '验证测试质量' },
    { title: 'Report', detail: '生成完整报告' }
  ]
};

// ============================================================================
// 辅助函数 - 使用Agent来检查文件
// ============================================================================

const checkFileExists = async (agent, path) => {
  try {
    const result = await agent(`
      检查文件是否存在：${path}

      只返回JSON: {"exists": true/false, "has_test_scenarios": true/false}
    `, { label: '检查文件', model: 'haiku' });
    return JSON.parse(result);
  } catch (e) {
    return { exists: false, has_test_scenarios: false };
  }
};

const readFileContent = async (agent, path) => {
  const result = await agent(`
    读取文件内容：${path}

    只返回文件原始内容。
  `, { label: '读取文件', model: 'haiku' });
  return result;
};

// ============================================================================
// Phase 1: 检查设计文档状态
// ============================================================================

const checkDesignDoc = async (moduleName) => {
  phase('Check');
  log(`🔍 检查 ${moduleName} 设计文档状态...`);

  // 检查设计文档
  const designDocPath = `docs/architecture/modules/${moduleName}-design.md`;
  const testScenariosPath = `docs/testing/${moduleName.toUpperCase()}_TEST_SCENARIOS.md`;

  const designDocCheck = await checkFileExists(agent, designDocPath);
  const testScenariosCheck = await checkFileExists(agent, testScenariosPath);

  // 检查设计文档是否有测试场景章节
  let hasTestSection = false;
  if (designDocCheck.exists) {
    const content = await readFileContent(agent, designDocPath);
    hasTestSection = content.includes('## Testing') ||
                      content.includes('## Test Scenarios') ||
                      content.includes('测试场景');
  }

  const result = {
    designDoc: {
      path: designDocPath,
      exists: designDocCheck.exists,
      hasTestSection: hasTestSection
    },
    testScenarios: {
      path: testScenariosPath,
      exists: testScenariosCheck.exists
    }
  };

  log(`  设计文档: ${result.designDoc.exists ? '✅' : '❌'} ${designDocPath}`);
  log(`  测试场景章节: ${result.designDoc.hasTestSection ? '✅' : '❌'}`);
  log(`  独立测试场景文档: ${result.testScenarios.exists ? '✅' : '❌'} ${testScenariosPath}`);

  return result;
};

// ============================================================================
// Phase 2: 提取测试场景（使用test-extraction skill）
// ============================================================================

const extractTestScenarios = async (moduleName, checkResult) => {
  phase('Extract');
  log(`📋 为 ${moduleName} 提取测试场景...`);

  // 使用专门的test-extraction逻辑
  const extractionResult = await agent(`
    执行完整的test-extraction流程，为 ${moduleName} 模块提取测试场景。

    设计文档位置: docs/architecture/modules/${moduleName}-design.md

    参考 docs/testing/TEST_EXTRACTION_METHODOLOGY.md 的5步方法论：
    1. 定位设计文档
    2. 识别测试维度 (States, Transitions, Boundaries, Errors, Features)
    3. 创建测试矩阵 (为每个维度创建场景表)
    4. 分类测试 (normal, edge, errors, integration)
    5. 估算覆盖率

    输出完整的测试场景分析，包括：
    - Step 1-5 的完整分析
    - 测试维度列表
    - 场景矩阵表格
    - 核心场景ID (使用标准前缀，如 SM-XXX-001)
    - Mock依赖清单
    - Given/When/Then示例
  `, {
    label: '提取测试场景',
    model: 'opus'
  });

  log(`✓ 测试场景已提取`);

  return extractionResult;
};

// ============================================================================
// Phase 3: 生成测试代码
// ============================================================================

const generateTests = async (moduleName, checkResult) => {
  phase('Generate');
  log(`🔨 为 ${moduleName} 生成测试代码...`);

  // 读取设计文档内容
  const designDocPath = `docs/architecture/modules/${moduleName}-design.md`;
  const designDocContent = await readFileContent(agent, designDocPath);

  // 读取测试规则
  const rules = await agent(`
    读取并解析 docs/rules/testing-rules.yaml

    只返回规则摘要，包括：
    - 命名规范
    - 断言要求
    - 覆盖率目标
  `, { label: '读取测试规则', model: 'haiku' });

  // 并行执行多Agent分析
  const analysis = await parallel([
    // Agent 1: 代码分析
    () => agent(`
      分析 ${moduleName} 模块的代码实现。

      源代码位置: src/${moduleName}/

      快速提取：
      - 类定义
      - 方法签名
      - 外部依赖
      - 关键行为

      返回JSON格式的分析结果。
    `, { label: '代码分析', model: 'haiku' }),

    // Agent 2: 测试场景分析
    () => agent(`
      从以下内容中提取测试场景要求：

      设计文档: ${designDocContent.slice(0, 3000)}...

      提取：
      - 核心测试场景
      - 边界条件
      - 错误场景
      - Mock要求

      返回JSON格式的测试场景列表。
    `, { label: '测试场景分析', model: 'haiku' }),

    // Agent 3: 测试数据准备
    () => agent(`
      为 ${moduleName} 模块准备测试数据。

      生成：
      - Fixture模板
      - Mock数据示例
      - 测试用例数据

      返回JSON格式的测试数据。
    `, { label: '测试数据准备', model: 'haiku' })
  ]);

  log(`✓ 分析完成 (3个Agent并行)`);

  // Battle验证 - Agent互相找问题
  const battle = await parallel([
    () => agent(`
      审查Agent 1的代码分析，找遗漏。

      分析结果: ${JSON.stringify(analysis[0])}

      返回: {missed_items: [...]}
    `, { label: '代码分析Battle', model: 'haiku' }),

    () => agent(`
      审查Agent 2的测试场景，找遗漏。

      场景结果: ${JSON.stringify(analysis[1])}

      返回: {missed_scenarios: [...]}
    `, { label: '测试场景Battle', model: 'haiku' })
  ]);

  log(`✓ Battle验证完成`);

  // 生成测试代码
  const testCode = await agent(`
    基于以下分析和规则生成pytest测试代码：

    代码分析: ${JSON.stringify(analysis[0]).slice(0, 1000)}...
    测试场景: ${JSON.stringify(analysis[1]).slice(0, 1000)}...
    测试数据: ${JSON.stringify(analysis[2]).slice(0, 500)}...
    Battle问题: ${JSON.stringify(battle).slice(0, 500)}...
    测试规则: ${rules}

    要求：
    1. 使用清晰的命名 (test_{feature}_{condition})
    2. 每个测试至少3个断言
    3. 验证副作用和不变量
    4. 使用fixture管理测试数据
    5. Mock所有外部依赖

    只返回完整的测试代码。
  `, { label: '生成测试代码', model: 'opus' });

  log(`✓ 测试代码已生成`);

  return { analysis, battle, testCode };
};

// ============================================================================
// Phase 4: 验证测试质量
// ============================================================================

const verifyTests = async (moduleName, generated) => {
  phase('Verify');
  log(`✅ 验证 ${moduleName} 测试质量...`);

  // 并行验证
  const verification = await parallel([
    // Mock验证
    () => agent(`
      验证测试代码的Mock使用。

      代码: ${generated.testCode}

      检查：
      1. 所有外部依赖都有mock
      2. Mock配置正确
      3. 没有真实调用外部服务

      返回: {score: 0-100, issues: [...]}
    `, { label: 'Mock验证', model: 'haiku' }),

    // 断言验证
    () => agent(`
      验证测试代码的断言质量。

      代码: ${generated.testCode}

      检查：
      1. 每个测试至少3个断言
      2. 验证了副作用
      3. 断言有意义的条件
      4. 错误消息清晰

      返回: {score: 0-100, issues: [...]}
    `, { label: '断言验证', model: 'haiku' }),

    // 覆盖度验证
    () => agent(`
      估算测试覆盖度。

      代码: ${generated.testCode}

      分析：
      1. 场景覆盖百分比
      2. 边界条件覆盖
      3. 错误路径覆盖

      返回: {coverage_estimate: 0-100, gaps: [...]}
    `, { label: '覆盖度验证', model: 'haiku' })
  ]);

  // 综合评估
  const synthesis = await agent(`
    综合评估以下验证结果：

    Mock验证: ${JSON.stringify(verification[0])}
    断言验证: ${JSON.stringify(verification[1])}
    覆盖度验证: ${JSON.stringify(verification[2])}

    给出：
    1. 总体质量评分 (0-100)
    2. 关键问题列表
    3. 改进建议
    4. 是否通过质量门禁

    返回JSON格式的评估报告。
  `, { label: '综合评估', model: 'opus' });

  log(`✓ 验证完成`);
  log(`  总体评分: ${synthesis.score || 'N/A'}/100`);
  log(`  质量门禁: ${synthesis.passed ? '✅' : '❌'}`);

  return { verification, synthesis };
};

// ============================================================================
// Phase 5: 生成报告
// ============================================================================

const generateReport = async (moduleName, allResults) => {
  phase('Report');
  log(`📊 生成 ${moduleName} 测试生成报告...`);

  const report = await agent(`
    生成完整的测试生成报告。

    模块: ${moduleName}

    检查结果: ${JSON.stringify(allResults.check)}
    生成结果: ${JSON.stringify(allResults.generation, null, 2).slice(0, 1000)}...
    验证结果: ${JSON.stringify(allResults.verification, null, 2).slice(0, 1000)}...

    报告要求：
    1. 执行摘要
    2. 设计文档状态
    3. 测试生成统计
    4. 质量评分详情
    5. 关键发现
    6. 下一步建议

    输出专业的Markdown报告。
  `, { label: '生成报告', model: 'opus' });

  log(`✓ 报告已生成`);

  return report;
};

// ============================================================================
// 主流程
// ============================================================================

async function run() {
  log('🔄 Integrated Test Generation Workflow');
  log('');
  log('可靠闭环：检查 → 提取 → 生成 → 验证 → 报告');
  log('');

  const moduleName = args?.[0] || 'state_machine';
  log(`📍 目标模块: ${moduleName}`);
  log('');

  // Phase 1: 检查
  log('═══ Phase 1: Check ═══');
  const checkResult = await checkDesignDoc(moduleName);
  log('');

  // Phase 2: 提取（如果需要）
  let extractionResult = null;
  if (!checkResult.designDoc.hasTestSection && !checkResult.testScenarios.exists) {
    log('═══ Phase 2: Extract ═══');
    log('⚠ 缺少测试场景，开始提取...');
    extractionResult = await extractTestScenarios(moduleName, checkResult);
    log('');
  } else {
    log('═══ Phase 2: Extract ═══');
    log('⏭ 设计文档已有测试场景，跳过提取');
    log('');
  }

  // Phase 3: 生成
  log('═══ Phase 3: Generate ═══');
  const generationResult = await generateTests(moduleName, checkResult);
  log('');

  // Phase 4: 验证
  log('═══ Phase 4: Verify ═══');
  const verificationResult = await verifyTests(moduleName, generationResult);
  log('');

  // Phase 5: 报告
  log('═══ Phase 5: Report ═══');
  const report = await generateReport(moduleName, {
    check: checkResult,
    extraction: extractionResult,
    generation: generationResult,
    verification: verificationResult
  });
  log('');

  log('📊 最终报告:');
  log('');
  log(report);

  return {
    moduleName,
    check: checkResult,
    extraction: extractionResult,
    generation: generationResult,
    verification: verificationResult,
    report
  };
}

return await run();
