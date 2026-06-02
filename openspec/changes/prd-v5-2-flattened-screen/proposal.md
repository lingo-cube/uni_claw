# Proposal: PRD V5.2 两步视觉管道实现

**Change ID**: `prd-v5-2-flattened-screen`
**Created**: 2026-06-02
**Status**: Design Phase
**Based on**: PRD_V5_2-flattened-screen.md

---

## 摘要 (Summary)

实现两步视觉管道架构，将视觉感知与逻辑推理解耦。通过多模态模型负责"看图说话"（输出扁平化元素列表），文本模型负责逻辑推理（组装层级、推断行为），预期实现：
- **Token 消耗减少 60%+**
- **速度提升 30%~50%**
- **成本减半**
- **层级准确率从 70% 提升到 90%+**

---

## 背景与动机 (Background & Motivation)

### 当前问题

现有的一体化视觉方案存在三个核心问题：

| 问题 | 描述 | 影响 |
|------|------|------|
| **准确率低** | 多模态模型不擅长逻辑推理（层级判断、父子关系、行为推断） | 误判率高，层级准确率仅 70% |
| **耗时长** | 输出复杂结构导致响应延迟 | 用户体验差，遍历效率低 |
| **成本高** | 大量 Token 消耗在结构化输出上 | 运营成本高，限制大规模应用 |

### 解决方案：感知与认知解耦

```
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│  多模态模型     │      │   文本模型      │      │  PageAnalysis   │
│  (感知)         │  →   │   (认知)         │  →   │  (最终输出)     │
└─────────────────┘      └─────────────────┘      └─────────────────┘
        ↓                        ↓                        ↓
   FlattenedScreen        PageAnalysisAssembler    完整层级结构
   (扁平化元素)           (逻辑组装与推断)          + 行为推断
```

**核心思想**：让每个模型做它最擅长的事
- **多模态模型**：识别视觉元素（是什么、在哪里）
- **文本模型**：推理逻辑关系（谁属于谁、点击后会怎样）

---

## 目标 (Goals)

### 主要目标

1. **实现 FlattenedScreen 数据模型**
   - 定义 BoundingBox、Region、TypeHint、SelectionState、FlattenedElement、FlattenedScreen 等核心数据结构
   - 支持归一化坐标、视觉状态、区域划分等功能

2. **实现 MultimodalAnalyzer**
   - 调用多模态模型（Claude Sonnet）分析截图
   - 输出 FlattenedScreen（扁平化元素列表）
   - 支持缓存机制

3. **实现 PageAnalysisAssembler**
   - 调用文本模型（DeepSeek）进行逻辑推理
   - 将 FlattenedScreen 组装为 PageAnalysis
   - 实现层级推断、行为推断、弹窗识别等功能

4. **实现双模式运行机制**
   - 支持新旧方案切换（legacy/flattened/dual）
   - 实现降级机制（新方案失败时自动切换到旧方案）
   - 保持向后兼容性

5. **实现缓存系统**
   - FlattenedScreen 缓存（基于截图哈希）
   - PageAnalysis 缓存（基于 FlattenedScreen + 上下文哈希）
   - TTL 和淘汰策略

### 性能目标

| 指标 | 当前 (baseline) | 目标 (target) | 改善幅度 |
|------|-----------------|---------------|----------|
| **Token 消耗** | 100% | ≤40% | -60% |
| **响应延迟** | 1x | ≤0.7x | +30% |
| **层级准确率** | 70% | ≥90% | +20% |
| **行为推断准确率** | 65% | ≥85% | +20% |
| **弹窗检测准确率** | 80% | ≥95% | +15% |

---

## 范围 (Scope)

### 包含 (In Scope)

1. **数据模型**
   - `src/models/vision/flattened_screen.py` - FlattenedScreen 相关数据模型
   - `src/models/vision/bounding_box.py` - BoundingBox 模型
   - `src/models/vision/region.py` - Region 模型
   - `src/models/vision/type_hint.py` - TypeHint 枚举
   - `src/models/vision/selection_state.py` - SelectionState 枚举

2. **视觉分析器**
   - `src/ai/vision/multimodal_analyzer.py` - 多模态视觉分析器
   - `src/ai/vision/prompts/multimodal_prompt.py` - 多模态分析 Prompt

3. **页面组装器**
   - `src/ai/vision/page_analysis_assembler.py` - 页面组装器
   - `src/ai/vision/prompts/assembler_prompt.py` - 组装器 Prompt

4. **视觉服务**
   - `src/ai/vision/flattened_vision_service.py` - 新两步管道视觉服务
   - `src/ai/vision/legacy_vision_service.py` - 旧方案（兜底）
   - `src/ai/vision/vision_service_factory.py` - 工厂模式，支持切换

5. **缓存系统**
   - `src/ai/vision/cache/screen_cache.py` - 截图缓存
   - `src/ai/vision/cache/page_analysis_cache.py` - 页面分析缓存
   - `src/ai/vision/cache/hash_generator.py` - 哈希生成器

6. **配置**
   - 更新 `config/settings.py` - 添加视觉服务配置
   - 支持模式切换和模型配置

7. **测试**
   - 单元测试（数据模型、分析器、组装器）
   - 集成测试（端到端流程）
   - 性能对比测试（新旧方案对比）
   - 准确率测试（基于标注数据集）

### 不包含 (Out of Scope)

1. **核心业务模型修改**
   - PageAnalysis、MenuItem 等现有模型保持不变
   - 仅新增 FlattenedScreen 相关模型

2. **遍历引擎修改**
   - 遍历引擎继续使用 PageAnalysis 接口
   - 视觉管道变化对遍历引擎透明

3. **状态管理修改**
   - 状态管理模块不涉及视觉分析逻辑

4. **UI/Dashboard 更新**
   - Dashboard 更新作为独立任务

---

## 成功标准 (Success Criteria)

### 功能验收

- [ ] 多模态模型能正确输出 `FlattenedScreen`
- [ ] 文本模型能正确组装 `PageAnalysis`
- [ ] 新旧方案切换功能正常
- [ ] 降级机制工作正常（新方案失败时自动切换到旧方案）
- [ ] 缓存系统工作正常
- [ ] 层级推断准确率 ≥ 90%
- [ ] 行为推断准确率 ≥ 85%
- [ ] 弹窗检测准确率 ≥ 95%

### 性能验收

- [ ] Token 消耗减少 ≥ 60%
- [ ] 响应延迟减少 ≥ 30%
- [ ] 缓存命中率 ≥ 70%（重复页面）
- [ ] 降级延迟增加 ≤ 20%

### 质量验收

- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 集成测试通过率 100%
- [ ] 性能对比测试通过
- [ ] 代码审查通过
- [ ] 测试数据集完整（≥10张标准截图）

---

## 风险 (Risks)

### 技术风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 多模态模型类型识别不准 | 中 | 中 | 优化 Prompt，增加示例，使用 Few-shot |
| 文本模型层级推断错误 | 高 | 低 | 提供清晰规则，思维链引导 |
| 两步调用延迟增加 | 中 | 低 | 缓存 FlattenedScreen，并行优化 |
| 缓存指纹冲突 | 低 | 低 | 使用鲁棒哈希算法，设置 TTL |
| 降级机制复杂度 | 中 | 低 | 统一接口，透明切换 |

### 业务风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 两步调用总成本增加 | 中 | 低 | Token 消耗监控，设置预算告警 |
| 模型可用性 | 高 | 低 | 多模型备选，降级到旧方案 |
| 成本超预期 | 低 | 低 | 设置 Token 预算告警 |

---

## 依赖 (Dependencies)

### 内部依赖

- 现有的 PageAnalysis 模型（`src/models/page_analysis.py`）
- 现有的 MenuItem 模型（`src/models/menu_item.py`）
- UniBrain AI 提供者（`src/ai/`）

### 外部依赖

- Claude Sonnet 3.5（多模态模型）
- DeepSeek V4（文本模型）
- ADB 设备（用于测试）

---

## 实施计划 (Implementation Plan)

### 阶段划分

| 阶段 | 任务 | 估时 |
|------|------|------|
| **P1 - 数据模型** | 定义 FlattenedScreen 相关模型 | 1天 |
| **P2 - 多模态分析器** | 实现 MultimodalAnalyzer | 2天 |
| **P3 - 页面组装器** | 实现 PageAnalysisAssembler | 3天 |
| **P4 - 双模式集成** | 实现新旧方案共存 | 2天 |
| **P5 - 缓存系统** | 实现双层缓存 | 1天 |
| **P6 - 测试框架** | 性能对比和准确率测试 | 2天 |
| **P7 - 验证与优化** | 端到端测试和 Prompt 优化 | 2天 |

**总计**: 约 13 天

---

## 参考资料 (References)

- [PRD V5.2 完整文档](../../docs/PRD_V5_2-flattened-screen.md)
- [核心业务模型](../../docs/core_business_models.md)
- [架构总览](../../docs/ARCHITECTURE.md)
- [AI 部署指南](../../docs/ai_deployment_guide.md)

---

**文档版本**: 1.0
**最后更新**: 2026-06-02
