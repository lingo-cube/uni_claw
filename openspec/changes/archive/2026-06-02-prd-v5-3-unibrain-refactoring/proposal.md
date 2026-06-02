# Proposal: PRD V5.3 UniBrain架构重构

**Change ID**: `prd-v5-3-unibrain-refactoring`
**Created**: 2026-06-02
**Status**: Design Phase
**Based on**: PRD_V5_3-unibrain-refactoring.md

---

## 摘要 (Summary)

重构UniBrain AI能力层，实现从5层架构到3层架构的简化，并建立统一的基础设施：
- **Provider抽象层** - 统一的AI提供者接口 (AIProvider)
- **声明式路由** - 基于配置的能力→Provider映射
- **提示词管理系统** - 集中式管理、版本控制、变量注入
- **追踪集成** - 自动追踪 + 自定义追踪 (A+E)
- **全面测试迁移** - 100%测试覆盖率 + 性能验证

预期成果：
- 架构简化：从5层减少到3层
- 代码减少：核心代码减少>20%
- 测试覆盖率：从当前提升到>90%
- Token统计：完整的延迟、token、质量数据采集
- 测试成本：默认测试零API成本

---

## 背景与动机 (Background & Motivation)

### 当前问题

#### 1. 层次复杂度过高
**现状**：5层架构
```
AIStrategyAdvisor → UniBrain → BaseCapability → MultimodalAnalyzer + PageAnalysisAssembler → UnifiedAIProvider
```

**问题**：
- 接口层次过多，概念理解成本高
- 跨层调用频繁，性能开销大
- 调试困难，问题定位复杂

#### 2. Provider管理混乱
**现状**：多个Provider实现分散在不同模块
- `DeepSeekProvider` 在 `core/llm_client.py`
- `ClaudeProvider` 在 `vision/claude_service.py`
- `MiMoProvider` 在 `core/multimodal_client.py`

**问题**：
- 缺乏统一抽象，难以添加新Provider
- 路由逻辑分散，难以维护
- 无法根据需求动态选择Provider

#### 3. 提示词管理分散
**现状**：提示词硬编码在各个能力模块中
- `ParseToPlanCapability` 提示词在 `core/prompts.py`
- `VisionAnalysisCapability` 提示词在 `vision/prompts/`
- 缺乏版本管理和A/B测试能力

**问题**：
- 提示词优化困难，无法快速迭代
- 无法复用提示词模板
- 缺乏失败分析和优化机制

#### 4. 追踪不完整
**现状**：基础追踪存在，但不完整
- 只有简单的span记录
- 缺少业务上下文信息
- 无法追踪Provider选择和token消耗

**问题**：
- 问题定位困难
- 无法分析token消耗和性能
- 缺少业务指标的关联分析

### 改进目标

#### 1. 简化架构层次
**目标**：从5层简化到3层
```
UniBrain (统一接口) → Provider抽象层 → 具体Provider实现
```

#### 2. 统一Provider管理
**目标**：
- 所有Provider实现统一接口
- 声明式路由配置
- 支持Provider性能评估

#### 3. 集中式提示词管理
**目标**：
- 所有提示词集中存储
- 支持版本控制和变量注入
- 便于A/B测试和优化

#### 4. 完整追踪系统
**目标**：
- 自动追踪所有AI调用
- 支持业务上下文注入
- Token消耗和性能可视化

---

## 目标 (Goals)

### 主要目标

1. **实现Provider抽象层**
   - 定义AIProvider统一接口
   - 实现DeepSeekProvider、ClaudeProvider、MiMoProvider
   - 支持文本、视觉、多模态三种模式
   - 提供token估算和性能评级

2. **实现声明式路由**
   - 创建Provider路由配置文件
   - 实现能力→Provider映射
   - 支持动态Provider选择
   - 換Provider无需修改代码

3. **实现提示词管理系统**
   - 创建PromptManager类
   - 支持变量注入和版本控制
   - 提供提示词模板化能力
   - 支持热重载

4. **实现追踪集成系统**
   - 创建TraceIntegration类
   - 自动追踪所有AI调用
   - 支持业务上下文注入
   - 收集token和性能指标

5. **重构UniBrain统一接口**
   - 简化UniBrain实现
   - 集成Provider、提示词、追踪
   - 保持5大核心能力接口不变
   - 提供配置化能力

6. **全面测试迁移**
   - 迁移所有现有测试到新架构
   - 实现Mock数据基础设施
   - 实现零API成本测试
   - 实现性能验证测试

### 架构目标

| 指标 | 当前 (baseline) | 目标 (target) | 改善幅度 |
|------|-----------------|---------------|----------|
| **架构层次** | 5层 | 3层 | -40% |
| **代码量** | 100% | ≤80% | -20% |
| **测试覆盖率** | 当前 | ≥90% | +90% |
| **数据采集** | 部分采集 | 完整采集 | 100% |

---

## 范围 (Scope)

### 包含 (In Scope)

1. **Provider抽象层**
   - `src/ai/providers/base.py` - AIProvider抽象接口
   - `src/ai/providers/deepseek.py` - DeepSeekProvider实现
   - `src/ai/providers/claude.py` - ClaudeProvider实现
   - `src/ai/providers/mimo.py` - MiMoProvider实现

2. **提示词管理系统**
   - `src/ai/prompts/manager.py` - PromptManager实现
   - `src/ai/prompts/*.md` - 提示词模板文件
   - 支持变量注入和版本控制

3. **追踪集成系统**
   - `src/ai/trace/integration.py` - TraceIntegration实现
   - `src/ai/trace/models.py` - 追踪数据模型
   - 自动追踪和指标收集

4. **UniBrain重构**
   - `src/ai/unibrain.py` - 简化的UniBrain实现
   - 集成Provider、提示词、追踪
   - 声明式路由配置

5. **配置管理**
   - `config/ai_providers.yaml` - Provider路由配置
   - 支持环境变量注入
   - 配置验证

6. **测试迁移**
   - 所有现有测试迁移到新架构
   - Mock数据基础设施
   - 零API成本测试机制
   - 性能验证测试

### 不包含 (Out of Scope)

1. **核心业务逻辑修改**
   - 遍历引擎不修改
   - 状态管理不修改
   - 仅AI层重构

2. **UI/Dashboard更新**
   - Dashboard更新作为独立任务

3. **新功能添加**
   - 仅重构，不添加新功能

---

## 成功标准 (Success Criteria)

### 功能验收

- [ ] 所有Provider实现AIProvider接口
- [ ] 支持3种模式：text, vision, multimodal
- [ ] 声明式路由功能正常
- [ ] 提示词管理功能正常
- [ ] 追踪系统功能正常
- [ ] 所有现有测试100%通过
- [ ] 新测试覆盖率≥90%

### 架构验收

- [ ] 架构层次从5层减少到3层
- [ ] 核心代码减少>20%
- [ ] 接口数量减少>30%
- [ ] 配置项减少>40%

### 质量验收

- [ ] 单元测试覆盖率>90%
- [ ] 集成测试覆盖率>80%
- [ ] 所有测试100%通过
- [ ] 代码审查通过

### 测试成本验收

- [ ] 默认测试使用Mock，API成本为$0
- [ ] CI/CD管道零API成本
- [ ] 录制回放机制功能完整
- [ ] 健康检查可选真实API调用

---

## 风险 (Risks)

### 技术风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| API兼容性风险 | 高 | 中 | 保留适配器层，充分测试 |
| Provider稳定性风险 | 高 | 低 | 健康检查，多Provider备选 |
| 性能回归风险 | 中 | 低 | 性能基准，持续监控 |
| 测试迁移风险 | 中 | 中 | 详细计划，覆盖率监控 |

### 业务风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 重构工作量超预期 | 中 | 中 | 分阶段实施，及时调整 |
| 测试不完整 | 高 | 低 | 充分测试，回归验证 |
| Provider配置复杂 | 低 | 低 | 配置验证，详细文档 |

---

## 依赖 (Dependencies)

### 内部依赖

- 现有的UniBrain模块 (保留接口)
- 现有的测试套件 (需要迁移)
- PRD V5.2 两步视觉管道 (已完成)

### 外部依赖

- Claude Sonnet 3.5 (视觉模型)
- DeepSeek V4 (文本模型)
- MiMo API (多模态模型)
- ADB设备 (用于测试)

---

## 实施计划 (Implementation Plan)

### 阶段划分

| 阶段 | 任务 | 估时 |
|------|------|------|
| **P0 - Mock数据基础设施** | 建立Mock数据系统，确保零测试成本 | 1周 |
| **P1 - Provider抽象层** | 实现Provider抽象和具体实现 | 1-2周 |
| **P2 - 提示词管理系统** | 建立集中式提示词管理 | 1周 |
| **P3 - 追踪系统集成** | 集成完整追踪系统 | 1周 |
| **P4 - UniBrain重构** | 重构UniBrain统一接口 | 1周 |
| **P5 - 测试迁移和验证** | 迁移所有测试，确保100%通过 | 1周 |
| **P6 - 数据采集和性能监控** | 建立完整的数据采集和监控 | 1周 |

**总计**: 约 7-8 周

---

## 参考资料 (References)

- [PRD V5.3 完整文档](../../docs/PRD_V5_3-unibrain-refactoring.md)
- [架构总览](../../docs/ARCHITECTURE.md)
- [AI模块文档](../../src/ai/README.md)
- [测试指南](../../tests/README.md)

---

**文档版本**: 1.0
**最后更新**: 2026-06-02
