/**
 * Single Agent vs Multi-Agent Comparison
 *
 * 对比单Agent和多Agent在状态机分析和测试场景覆盖方面的差异
 */

export const meta = {
  name: 'single-vs-multi-comparison',
  description: '单Agent vs 多Agent对比分析',
  phases: [
    { title: 'Setup', detail: '设置对比环境' },
    { title: 'Single', detail: '单Agent分析' },
    { title: 'Multi', detail: '多Agent分析' },
    { title: 'Compare', detail: '对比分析' },
    { title: 'Report', detail: '生成对比报告' }
  ]
};

// ============================================================================
// Phase 1: 设置
// ============================================================================

const setupComparison = async () => {
  phase('Setup');

  return await agent(`
    作为对比分析架构师，设置单Agent vs 多Agent的对比实验。

    任务: 为 state_machine 模块准备对比分析

    输出:
    {
      "target_module": "state_machine",
      "analysis_dimensions": [
        "代码实现分析",
        "设计文档分析",
        "测试场景提取",
        "边界条件识别",
        "错误场景识别",
        "覆盖度评估"
      ],
      "success_criteria": {
        "completeness": "识别所有关键方法",
        "accuracy": "准确的依赖和副作用分析",
        "coverage": "测试场景覆盖度"
      }
    }
  `, { label: '设置对比', model: 'opus' });
};

// ============================================================================
// Phase 2: 单Agent分析
// ============================================================================

const singleAgentAnalysis = async (setup: any) => {
  phase('Single');

  const startTime = Date.now();

  // 单Agent完成所有任务
  const result = await agent(`
    作为**全能型测试分析师**，独立完成 state_machine 模块的完整分析。

    你需要独自完成以下所有任务：

    1. **代码实现分析**
       - 读取 src/state_machine/ 目录
       - 识别所有类和方法
       - 分析每个方法的：
         * 参数和返回值
         * 外部依赖
         * 副作用
         * 边界条件
         * 错误处理

    2. **设计文档分析**
       - 读取 docs/architecture/concepts/state-machine-design.md
       - 读取 docs/architecture/modules/state-machine-design.md
       - 提取：
         * 行为规范
         * 状态转换规则
         * 测试场景建议

    3. **测试场景提取**
       - 结合代码和文档，提取所有测试场景
       - 覆盖：
         * 正常路径
         * 边界条件
         * 错误场景
         * 集成场景

    4. **覆盖度评估**
       - 评估测试场景的完整性
       - 识别缺失的场景

    输出完整分析报告，包含所有发现。
    记录你遇到的困难和遗漏。
  `, {
    label: '单Agent分析',
    model: 'opus',
    timeout: 300000  // 5分钟
  });

  const duration = Date.now() - startTime;

  // 提取单Agent的结果
  const singleResult = await agent(`
    从以下分析结果中提取结构化信息：

    ${result}

    提取:
    {
      "methods_found": ["method1", "method2", ...],
      "dependencies_found": ["dep1", "dep2", ...],
      "scenarios_extracted": [
        {"id": "S001", "method": "...", "type": "normal|boundary|error"}
      ],
      "coverage_estimate": "XX%",
      "difficulties_encountered": ["difficulty1", ...],
      "potential_gaps": ["gap1", ...]
    }

    只返回JSON。
  `, { label: '提取单Agent结果', model: 'haiku' });

  return {
    result,
    structured: singleResult,
    duration,
    agentCount: 1,
    modelUsage: { opus: 1 }
  };
};

// ============================================================================
// Phase 3: 多Agent分析
// ============================================================================

const multiAgentAnalysis = async (setup: any) => {
  phase('Multi');

  const startTime = Date.now();
  const modelStats = { haiku: 0, sonnet: 0, opus: 0 };

  // Phase 3.1: 并行分析 (Haiku)
  log('  Phase 3.1: 并行分析 (Haiku)...');
  const analysisResults = await parallel([
    () => agent(`
      分析 state_machine 的代码实现。
      识别：类、方法、依赖、副作用。
      返回JSON列表。
    `, { label: '代码分析', model: 'haiku' }),
    () => agent(`
      分析 state_machine 的设计文档。
      识别：行为、场景、边界。
      返回JSON列表。
    `, { label: '文档分析', model: 'haiku' }),
    () => agent(`
      分析 state_machine 的现有测试。
      识别：已覆盖场景、缺失场景。
      返回JSON列表。
    `, { label: '测试分析', model: 'haiku' })
  ]);
  modelStats.haiku += 3;

  // Phase 3.2: Agent间Battle验证
  log('  Phase 3.2: Agent间Battle验证...');
  const battleResults = await parallel([
    () => agent(`
      作为**对抗者**，审查代码分析结果：
      ${JSON.stringify(analysisResults[0])}

      找遗漏的方法、依赖、副作用。
      返回: {missed: [...]}
    `, { label: '代码Battle', model: 'haiku' }),
    () => agent(`
      作为**对抗者**，审查文档分析结果：
      ${JSON.stringify(analysisResults[1])}

      找遗漏的场景、边界。
      返回: {missed: [...]}
    `, { label: '文档Battle', model: 'haiku' }),
    () => agent(`
      作为**仲裁者**，检查代码vs文档一致性：
      代码: ${JSON.stringify(analysisResults[0])}
      文档: ${JSON.stringify(analysisResults[1])}

      找不一致的地方。
      返回: {inconsistencies: [...]}
    `, { label: '一致性Battle', model: 'sonnet' })
  ]);
  modelStats.haiku += 2;
  modelStats.sonnet += 1;

  // Phase 3.3: 场景提取 (Haiku + Opus)
  log('  Phase 3.3: 场景提取 (Haiku + Opus)...');
  const rawScenarios = await agent(`
    基于分析结果，快速提取测试场景：
    ${JSON.stringify(analysisResults).slice(0, 2000)}

    Battle发现的问题：
    ${JSON.stringify(battleResults).slice(0, 1000)}

    为每个方法生成3-5个场景。
    返回场景列表。
  `, { label: '场景生成', model: 'haiku' });
  modelStats.haiku += 1;

  const refinedScenarios = await agent(`
    作为**测试架构师**，优化和补充以下场景：

    ${JSON.stringify(rawScenarios)}

    分析结果：
    ${JSON.stringify(analysisResults).slice(0, 1000)}

    Battle问题：
    ${JSON.stringify(battleResults).slice(0, 500)}

    补充关键场景，标注优先级。
  `, { label: '场景优化', model: 'opus' });
  modelStats.opus += 1;

  // Phase 3.4: 综合评估
  log('  Phase 3.4: 综合评估...');
  const assessment = await agent(`
    综合评估多Agent分析的结果：

    分析结果：
    ${JSON.stringify(analysisResults).slice(0, 1500)}

    Battle结果：
    ${JSON.stringify(battleResults).slice(0, 800)}

    最终场景：
    ${JSON.stringify(refinedScenarios).slice(0, 1500)}

    评估：
    1. 方法识别完整性
    2. 依赖分析准确性
    3. 场景覆盖全面性
    4. 对比单Agent可能的改进

    返回评估报告。
  `, { label: '综合评估', model: 'opus' });
  modelStats.opus += 1;

  const duration = Date.now() - startTime;

  return {
    analysisResults,
    battleResults,
    scenarios: refinedScenarios,
    assessment,
    duration,
    agentCount: 10,  // 3分析 + 3Battle + 1生成 + 1优化 + 1评估 + 1综合
    modelUsage: modelStats
  };
};

// ============================================================================
// Phase 4: 对比分析
// ============================================================================

const compareResults = async (single: any, multi: any) => {
  phase('Compare');

  return await agent(`
    作为**对比分析专家**，比较单Agent和多Agent的分析结果。

    ## 单Agent结果

    分析结果 (摘要):
    ${JSON.stringify(single.structured).slice(0, 1500)}

    执行时间: ${single.duration}ms
    Agent数量: ${single.agentCount}
    模型使用: ${JSON.stringify(single.modelUsage)}

    困难和遗漏:
    ${single.structured?.potential_gaps || '未记录'}

    ## 多Agent结果

    分析结果 (摘要):
    ${JSON.stringify(multi.assessment).slice(0, 1500)}

    执行时间: ${multi.duration}ms
    Agent数量: ${multi.agentCount}
    模型使用: ${JSON.stringify(multi.modelUsage)}

    Battle发现的问题数量:
    - 代码Battle: ${multi.battleResults[0]?.missed?.length || 0}
    - 文档Battle: ${multi.battleResults[1]?.missed?.length || 0}
    - 一致性Battle: ${multi.battleResults[2]?.inconsistencies?.length || 0}

    ## 对比维度

    请对比以下维度：

    1. **完整性**
       - 单Agent找到的方法数量 vs 多Agent
       - 单Agent找到的依赖数量 vs 多Agent
       - 单Agent提取的场景数量 vs 多Agent

    2. **准确性**
       - 单Agent遗漏了多少关键项
       - 多Agent通过Battle发现了多少额外项

    3. **效率**
       - 执行时间对比
       - 模型使用对比
       - 成本对比

    4. **质量**
       - 场景覆盖度对比
       - 分析深度对比
       - 错误发现能力对比

    输出详细的对比表格和结论。
  `, {
    label: '对比分析',
    model: 'opus'
  });
};

// ============================================================================
// Phase 5: 生成报告
// ============================================================================

const generateComparisonReport = async (
  setup: any,
  single: any,
  multi: any,
  comparison: any
) => {
  phase('Report');

  return await agent(`
    生成单Agent vs 多Agent对比实验报告。

    对比结果:
    ${JSON.stringify(comparison).slice(0, 3000)}

    生成包含以下部分的报告：

    # 单Agent vs 多Agent对比报告

    ## 实验设计

    ## 结果对比表格

    ### 维度1: 完整性
    ### 维度2: 准确性
    ### 维度3: 效率
    ### 维度4: 质量

    ## 关键发现

    ## 结论和建议

    输出Markdown格式。
  `, {
    label: '生成报告',
    model: 'opus'
  });
};

// ============================================================================
// 主流程
// ============================================================================

async function run() {
  log('🔬 Single Agent vs Multi-Agent Comparison');
  log('');

  // Phase 1: 设置
  log('Phase 1: Setup...');
  const setup = await setupComparison();
  log(`✓ 目标模块: ${setup.target_module}`);
  log(`✓ 分析维度: ${setup.analysis_dimensions.length}`);
  log('');

  // Phase 2: 单Agent分析
  log('Phase 2: Single Agent Analysis...');
  log('  (这可能需要几分钟...)');
  const single = await singleAgentAnalysis(setup);
  log(`✓ 单Agent分析完成`);
  log(`  - 方法数: ${single.structured?.methods_found?.length || 0}`);
  log(`  - 场景数: ${single.structured?.scenarios_extracted?.length || 0}`);
  log(`  - 耗时: ${single.duration}ms`);
  log(`  - 困难: ${single.structured?.difficulties_encountered?.length || 0} 项`);
  log('');

  // Phase 3: 多Agent分析
  log('Phase 3: Multi-Agent Analysis...');
  const multi = await multiAgentAnalysis(setup);
  log(`✓ 多Agent分析完成`);
  log(`  - 场景数: ${multi.scenarios?.scenarios?.length || 0}`);
  log(`  - 耗时: ${multi.duration}ms`);
  log(`  - Agent数: ${multi.agentCount}`);
  log(`  - 模型使用: Haiku=${multi.modelUsage.haiku}, Sonnet=${multi.modelUsage.sonnet}, Opus=${multi.modelUsage.opus}`);
  log(`  - Battle发现: ${multi.battleResults.reduce((sum, r) => sum + (Object.values(r)[0]?.length || 0), 0)} 个问题`);
  log('');

  // Phase 4: 对比
  log('Phase 4: Comparing Results...');
  const comparison = await compareResults(single, multi);
  log(`✓ 对比完成`);
  log('');

  // Phase 5: 报告
  log('Phase 5: Generating Report...');
  const report = await generateComparisonReport(setup, single, multi, comparison);
  log(`✓ 报告完成`);
  log('');

  log('📊 Comparison Report:');
  log('');
  log(report);

  // 统计
  log('📊 Experiment Statistics:');
  log('');
  log('Single Agent:');
  log(`  - Agents: ${single.agentCount}`);
  log(`  - Time: ${single.duration}ms`);
  log(`  - Models: ${JSON.stringify(single.modelUsage)}`);
  log('');
  log('Multi Agent:');
  log(`  - Agents: ${multi.agentCount}`);
  log(`  - Time: ${multi.duration}ms`);
  log(`  - Models: ${JSON.stringify(multi.modelUsage)}`);
  log('');
  log('Speed Comparison:');
  const speedup = (single.duration / multi.duration).toFixed(2);
  log(`  - Multi Agent is ${speedup}x ${speedup > 1 ? 'faster' : 'slower'}`);

  return {
    setup,
    single,
    multi,
    comparison,
    report
  };
}

return await run();
