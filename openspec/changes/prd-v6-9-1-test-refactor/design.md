# Design: V6.9.1 Test System Refactor

## Context

### Background

Uni-Claw V6 系列引入了大量新功能（V6.3 Trace 系统、V6.6 Handler Metrics、V6.7 状态机智能化、V6.8 引擎初始化、V6.9 动态匹配与计划编译），但测试体系未能同步演进。

### Current State

- **死代码问题**：`tests/conftest.py` 引用不存在的 `tests.ai.fixtures` 模块，`tests/integration/test_simulation_e2e.py` 引用不存在的 `tests.simulation.helpers`
- **测试重复**：`test_simulation.py` 与 `test_v6_4_simulation_alignment.py` 高度重复
- **覆盖缺口**：V6.7-V6.9 新增功能无对应测试场景

### Constraints

- 测试必须使用 Mock 组件（VisionService、ActionExecutor），不依赖真实设备
- 虚拟页面 JSON 格式必须符合 MockVisionService 解析规则
- 测试执行必须快速（单文件 < 1s，全量 < 30s）
- 不修改生产代码，仅添加/修改测试代码

### Stakeholders

- 开发团队：需要可靠的回归测试基线
- QA 团队：需要仿真测试验证核心功能
- 项目维护者：需要清晰易维护的测试资产

## Goals / Non-Goals

**Goals:**
- 清理所有死代码，确保 `pytest tests/v6/ tests/integration/` 可执行
- 建立 L1-L4 四层次测试覆盖（模型契约、组件行为、协作、端到端）
- 补全 V6.7-V6.9 功能测试场景（44 基础 + 20+ 复杂）
- 创建可复用的测试辅助模块

**Non-Goals:**
- 真实 AI 服务集成测试（另案处理）
- 性能基准建立（仅验证无明显退化）
- UI 自动化测试框架重构

## Decisions

### Decision 1: 共享辅助模块渐进式实施

**选择**：分三批实施辅助模块，而非一次性全部实现。

**理由**：
- 第一批（必需）提供基础支持，可快速开始测试编写
- 第二批（增强）在基础测试稳定后引入，降低风险
- 第三批（可选）作为优化项，有时间再实施

**替代方案**：一次性全部实现 → 风险过高，可能因辅助模块开发延迟测试实施。

### Decision 2: 测试驱动缺陷发现而非验证正确性

**选择**：测试重点放在发现边界条件、故障恢复、状态不一致等问题。

**理由**：
- 状态机是复杂机制（1800+ 行代码），简单场景无法发现深层次问题
- 随机化、故障注入可暴露竞态条件、异常处理缺陷
- 深度状态验证可发现栈与路径不一致等隐秘 bug

**替代方案**：只验证 PRD 场景 → 无法发现设计缺陷和边界问题。

### Decision 3: MockVisionService JSON 格式使用 coordinate 字段

**选择**：虚拟页面 JSON 使用 `coordinate: {"x": 0.5, "y": 0.5}` 而非 `bounds: [x1, y1, x2, y2]`。

**理由**：
- MockVisionService 实际解析代码读取 `coordinate.x/y`（而非 bounds）
- 对齐实际实现可确保测试正确运行

**替代方案**：修改 MockVisionService 支持 bounds → 增加生产代码复杂度，违反"不修改生产代码"原则。

### Decision 4: 保留 v6/ 目录结构

**选择**：保持 `tests/v6/` 目录，不扁平化为 `tests/simulation/`。

**理由**：
- 项目有版本演进历史（V4→V5→V6），版本化目录结构便于未来 V7 迁移
- 已有 23 个文件在 v6/ 下，迁移成本高但收益低
- 与 docs/ 结构一致（PRD_V6_x.md）

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| 辅助模块开发延迟影响测试编写 | 分批实施，第一批提供最小可用功能 |
| 虚拟页面 JSON 格式错误导致测试失败 | 严格对齐 MockVisionService 解析代码，编写 M 系列验证 |
| 测试数量过多导致执行缓慢 | 按 marker 分类（slow/unit/integration），CI 可选择性运行 |
| 状态机复杂度高，测试难以覆盖 | 使用边界测试、故障注入、随机化补充覆盖 |
| V6.9 功能未完成导致测试无法运行 | V6.9 已实现（commit 17d9058），测试可同步开发 |

## Migration Plan

### 阶段 1：清理与基础（第 1 周）

1. 清理 `conftest.py` 死代码
2. 删除重复文件
3. 重命名 `test_simulation.py`
4. 修复 `test_simulation_e2e.py`

### 阶段 2-3：辅助模块与 Fixture（第 1-2 周）

5. 实施第一批辅助模块（factories、state_inspector、trace_analyzer）
6. 创建 5 个虚拟页面 JSON fixture

### 阶段 4-7：测试系列实施（第 2-4 周）

7. D 系列审查修正（13 场景）
8. C 系列补充（12 场景）
9. P 系列扩展（10 场景）
10. E2E 与 M 系列（12 场景）
11. 第二批辅助模块（chaos、boundary、fault_injector）

### 阶段 9：回归与验收（第 5 周）

12. 全量测试执行
13. 覆盖率检查
14. 性能基准对比
15. REFERENCE.md 同步

### Rollback Strategy

- 测试代码失败不应阻塞主分支
- 每个阶段完成后提交，便于回滚
- 如发现设计缺陷，可暂停实施并修正 PRD

## Open Questions

1. **测试执行时间预算**：全量测试 < 30s 是否可接受？（待性能基准确定）
2. **覆盖率目标**：核心模块 > 80% 是否足够？（可能需要提高到 90%）
3. **第三批辅助模块**：realism_simulator 和 stress_tester 是否必需？（根据时间预算决定）
