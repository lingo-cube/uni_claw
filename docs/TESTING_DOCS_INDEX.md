# E2E测试文档导航

## 📚 文档概览

本文档集提供了Uni-Claw E2E仿真测试系统的完整技术说明，从架构原理到实际操作，涵盖所有关键方面。

## 🎯 按需阅读指南

### 🔰 我想快速了解
→ 阅读 [TESTING_QUICK_REFERENCE.md](TESTING_QUICK_REFERENCE.md)
- 核心流程 (3步)
- 关键规则速查
- 常用命令
- 快速修复清单

### 🏗️ 我想了解系统架构
→ 阅读 [TESTING_ARCHITECTURE.md](TESTING_ARCHITECTURE.md)
- 完整测试流程
- 组件架构设计
- 数据结构详解
- 断言引擎机制

### 📊 我想看可视化流程
→ 阅读 [TESTING_FLOWCHARTS.md](TESTING_FLOWCHARTS.md)
- 执行流程图
- 数据转换图
- 决策流程图
- 诊断流程图

### 🛠️ 我想创建测试用例
→ 阅读 [TESTING_QUICK_REFERENCE.md](TESTING_QUICK_REFERENCE.md) 的 "扩展指南" 章节
- 测试数据文件结构
- 事件描述格式
- 断言规则设置

### 🐛 我想调试测试失败
→ 阅读 [TESTING_QUICK_REFERENCE.md](TESTING_QUICK_REFERENCE.md) 的 "常见陷阱" 章节
- 测试失败诊断流程
- 常见问题和解决方案
- 调试技巧

### 📝 我想生成自定义报告
→ 阅读 [TESTING_ARCHITECTURE.md](TESTING_ARCHITECTURE.md) 的 "报告生成流程" 章节
- 各格式报告生成器
- 扩展报告格式
- 数据转换规则

## 📋 文档详细内容

### [TESTING_ARCHITECTURE.md](TESTING_ARCHITECTURE.md)
**完整的系统架构文档 (8章节)**

#### 目录
1. **测试流程概述**
   - 完整测试流程图
   - 数据流转关系

2. **核心组件架构**
   - MockVisionService
   - MockActionExecutor
   - PageAnalyzer
   - InMemoryTracer
   - SimulationRunner

3. **测试数据结构**
   - test_case.json 格式
   - pages_all.json 格式
   - plan_all.json 格式

4. **断言引擎机制**
   - TraceAsserter 工作原理
   - 事件转换规则
   - 匹配算法

5. **报告生成流程**
   - 文本报告生成器
   - ASCII树生成器
   - Mermaid图生成器
   - HTML报告生成器
   - JSONL数据导出器

6. **数据转换规则**
   - TraceStep到Dict转换
   - 自然语言事件生成
   - 字段映射表

7. **扩展指南**
   - 添加新测试用例
   - 自定义断言规则
   - 扩展报告格式

8. **常见问题和解决方案**
   - 事件匹配失败
   - 追踪数据缺失
   - 报告生成错误
   - 路径解析问题

---

### [TESTING_QUICK_REFERENCE.md](TESTING_QUICK_REFERENCE.md)
**快速参考和操作指南**

#### 核心内容
- **3步核心流程**: 测试数据 → Mock执行 → 断言验证
- **数据结构层次**: 5层架构说明
- **规则速查表**: 事件格式、字段映射、断言验证
- **测试数据文件**: 必需文件和结构模板
- **常用命令**: 运行测试、生成报告、调试技巧
- **报告格式选择**: 5种格式对比
- **常见陷阱**: 4个典型错误和正确做法
- **快速修复清单**: 测试失败和报告生成的检查项
- **性能考虑**: 优化建议
- **学习路径**: 初学者→中级→高级

#### 特色功能
- **快速查找**: 问题类型 → 解决方案
- **代码示例**: 可直接复制的代码片段
- **决策表格**: 格式选择、规则应用
- **检查清单**: 验证测试完整性

---

### [TESTING_FLOWCHARTS.md](TESTING_FLOWCHARTS.md)
**可视化流程和决策图**

#### 流程图集合
1. **主流程**: 从测试启动到报告生成的完整流程
2. **数据转换**: 测试数据到最终报告的转换链路
3. **断言验证**: 事件匹配和验证的详细流程
4. **报告生成**: 5种格式的生成流程
5. **DFS遍历**: 深度优先搜索算法实现
6. **事件转换**: TraceStep到自然语言事件的转换
7. **失败诊断**: 测试失败的诊断和修复流程

#### 决策点分析
- **遍历决策**: should_go_back() 的判断逻辑
- **事件描述规则**: step_to_nl() 的优先级
- **报告格式选择**: 不同场景的格式选择建议

---

## 🔍 关键概念索引

### 核心组件
- **MockVisionService**: 模拟视觉分析和元素识别
- **MockActionExecutor**: 模拟设备操作和状态管理
- **PageAnalyzer**: 页面数据解析和元素处理
- **InMemoryTracer**: 追踪数据收集和存储
- **SimulationRunner**: 协调整个仿真测试流程
- **TraceAsserter**: 断言引擎和事件匹配

### 数据结构
- **TraceStep**: 单步追踪记录
- **SimulationResult**: 完整测试结果
- **AssertionResult**: 断言验证结果
- **VisitedNode**: 访问节点信息

### 关键流程
- **DFS遍历**: 深度优先搜索页面树
- **事件转换**: TraceStep → 自然语言事件
- **断言匹配**: 子序列匹配验证
- **报告生成**: 多格式输出

### 文件格式
- **test_case.json**: 测试用例定义
- **pages_all.json**: 虚拟页面数据
- **plan_all.json**: 遍历计划配置
- **JSONL**: 机器可读追踪数据
- **Mermaid**: 可视化图表格式

## 🚀 快速开始

### 1. 运行第一个测试
```bash
# 使用Python API
from tests.simulation.helpers.test_runner import SimulationTestRunner
runner = SimulationTestRunner()
result = runner.run_simulation_test('tests/simulation/fixtures/e2e_all_traversal/test_case.json')
print(f"Test: {'PASS' if result['passed'] else 'FAIL'}")
```

### 2. 查看测试报告
```bash
# 已生成的报告文件
cat test_simulation_report.txt    # 文本报告
cat test_traversal_tree.txt       # ASCII树
cat test_traversal_mermaid.md     # Mermaid图

# 在浏览器中打开HTML报告
# Windows: start test_trace_report.html
# Mac: open test_trace_report.html
```

### 3. 创建自己的测试
```bash
# 1. 复制现有测试用例
cp -r tests/simulation/fixtures/e2e_all_traversal tests/simulation/fixtures/my_test

# 2. 修改测试数据
# 编辑 my_test/pages_all.json
# 编辑 my_test/test_case.json

# 3. 运行测试
python -c "
from tests.simulation.helpers.test_runner import SimulationTestRunner
result = SimulationTestRunner().run_simulation_test('tests/simulation/fixtures/my_test/test_case.json')
print(f'Result: {\"PASS\" if result[\"passed\"] else \"FAIL\"}')"
```

## 📞 使用帮助

### 遇到问题时

1. **查看现有示例**
   - 已通过的测试: `tests/simulation/fixtures/e2e_all_traversal/`
   - 报告文件: `test_*.txt`, `test_*.html`, `test_*.jsonl`

2. **参考文档**
   - 快速问题: [TESTING_QUICK_REFERENCE.md](TESTING_QUICK_REFERENCE.md)
   - 深入理解: [TESTING_ARCHITECTURE.md](TESTING_ARCHITECTURE.md)
   - 可视化流程: [TESTING_FLOWCHARTS.md](TESTING_FLOWCHARTS.md)

3. **调试技巧**
   ```python
   # 打印实际生成的事件
   from tests.simulation.helpers.assertions import TraceAsserter
   for step in trace:
       event = TraceAsserter.step_to_nl(step)
       print(f"{step['step_number']}: {event}")
   
   # 检查断言结果
   print(f"Matched: {result['assertion_result'].key_events_matched}")
   print(f"Missing: {result['assertion_result'].missing_events}")
   print(f"Violations: {result['assertion_result'].violations}")
   ```

## 📈 文档维护

### 文档更新日志
- **v1.0** (2026-06-03): 初始版本
  - 完整的架构文档
  - 快速参考指南
  - 可视化流程图

### 贡献指南
如需更新或补充文档，请保持：
1. 结构的一致性
2. 示例的可运行性
3. 图示的清晰性
4. 说明的准确性

---

**相关资源**:
- 项目主文档: [CLAUDE.md](../CLAUDE.md)
- 测试指南: [tests/README.md](../tests/README.md)
- 报告摘要: [REPORT_SUMMARY.md](../REPORT_SUMMARY.md)

**文档版本**: v1.0
**最后更新**: 2026-06-03
**维护团队**: Uni-Claw开发团队