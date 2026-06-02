# Tasks: PRD V5.2 两步视觉管道实现

**Change ID**: `prd-v5-2-flattened-screen`
**Created**: 2026-06-02
**Status**: Design Phase

---

## 任务概览 (Task Overview)

| 阶段 | 任务数 | 估时 | 状态 |
|------|--------|------|------|
| P1 - 数据模型 | 8 | 1天 | 待开始 |
| P2 - 多模态分析器 | 5 | 2天 | 待开始 |
| P3 - 页面组装器 | 6 | 3天 | 待开始 |
| P4 - 双模式集成 | 4 | 2天 | 待开始 |
| P5 - 缓存系统 | 4 | 1天 | 待开始 |
| P6 - 测试框架 | 8 | 2天 | 待开始 |
| P7 - 验证与优化 | 5 | 2天 | 待开始 |

**总计**: 40 任务，约 13 天

---

## P1 - 数据模型 (1 天)

### T1.1 创建 vision 模型目录

**描述**: 创建数据模型目录结构

**文件**:
- `src/models/vision/__init__.py`
- `src/models/vision/bounding_box.py`
- `src/models/vision/region.py`
- `src/models/vision/type_hint.py`
- `src/models/vision/selection_state.py`
- `src/models/vision/flattened_element.py`
- `src/models/vision/flattened_screen.py`
- `src/models/vision/screen_hints.py`

**任务**:
- [x] 创建 `src/models/vision/` 目录
- [x] 创建 `__init__.py` 导出所有模型
- [x] 为每个模型创建对应文件

**验收**:
- 目录结构符合设计文档
- 所有文件可以正常导入

---

### T1.2 实现 BoundingBox 模型

**描述**: 实现归一化边界框数据模型

**文件**: `src/models/vision/bounding_box.py`

**任务**:
- [x] 实现 `BoundingBox` dataclass
- [x] 添加坐标验证（0-1 范围）
- [x] 实现 `center()` 方法
- [x] 实现 `area()` 方法
- [x] 实现 `contains()` 方法
- [x] 实现 `overlaps()` 方法

**测试**:
- [x] 创建 `tests/vision/models/test_bounding_box.py`
- [x] 测试坐标验证
- [x] 测试中心点计算
- [x] 测试面积计算
- [x] 测试包含判断
- [x] 测试重叠判断

**验收**:
- 所有单元测试通过
- 边界情况处理正确

---

### T1.3 实现 TypeHint 枚举

**描述**: 实现粗略视觉类型枚举

**文件**: `src/models/vision/type_hint.py`

**任务**:
- [x] 定义 `TypeHint` 枚举（8 种类型）
- [x] 实现 `from_string()` 方法
- [x] 添加容错映射

**测试**:
- [x] 创建 `tests/vision/models/test_type_hint.py`
- [x] 测试枚举值
- [x] 测试 from_string 方法
- [x] 测试容错处理

**验收**:
- 所有单元测试通过
- 容错机制工作正常

---

### T1.4 实现 SelectionState 枚举

**描述**: 实现选中状态枚举

**文件**: `src/models/vision/selection_state.py`

**任务**:
- [x] 定义 `SelectionState` 枚举（3 种状态）
- [x] 实现 `from_string()` 方法
- [x] 添加容错映射

**测试**:
- [x] 创建 `tests/vision/models/test_selection_state.py`
- [x] 测试枚举值
- [x] 测试 from_string 方法
- [x] 测试容错处理

**验收**:
- 所有单元测试通过
- 容错机制工作正常

---

### T1.5 实现 Region 模型

**描述**: 实现屏幕区域数据模型

**文件**: `src/models/vision/region.py`

**任务**:
- [x] 实现 `Region` dataclass
- [x] 添加 id、bounds、role 字段
- [x] 定义 role 字面量类型

**测试**:
- [x] 创建 `tests/vision/models/test_region.py`
- [x] 测试 Region 创建
- [x] 测试 role 验证

**验收**:
- 所有单元测试通过

---

### T1.6 实现 FlattenedElement 模型

**描述**: 实现扁平化元素数据模型

**文件**: `src/models/vision/flattened_element.py`

**任务**:
- [x] 实现 `FlattenedElement` dataclass
- [x] 添加所有必需字段
- [x] 添加默认值处理
- [x] 添加验证逻辑

**测试**:
- [x] 创建 `tests/vision/models/test_flattened_element.py`
- [x] 测试元素创建
- [x] 测试默认值
- [x] 测试验证逻辑

**验收**:
- 所有单元测试通过

---

### T1.7 实现 FlattenedScreen 模型

**描述**: 实现扁平化屏幕数据模型

**文件**: `src/models/vision/flattened_screen.py`

**任务**:
- [x] 实现 `FlattenedScreen` dataclass
- [x] 添加 elements 和 screen_hints 字段
- [x] 实现 `__post_init__()` 自动排序
- [x] 实现 `element_count()` 方法
- [x] 实现 `get_elements_in_region()` 方法
- [x] 实现 `get_selected_elements()` 方法
- [x] 实现 `to_dict()` 方法

**测试**:
- [x] 创建 `tests/vision/models/test_flattened_screen.py`
- [x] 测试屏幕创建
- [x] 测试元素排序
- [x] 测试区域过滤
- [x] 测试选中元素过滤
- [x] 测试序列化

**验收**:
- 所有单元测试通过
- 元素按预期排序

---

### T1.8 更新 __init__.py

**描述**: 更新模型模块导出

**文件**: `src/models/vision/__init__.py`

**任务**:
- [x] 导出 BoundingBox
- [x] 导出 TypeHint, SelectionState
- [x] 导出 Region
- [x] 导出 FlattenedElement, FlattenedScreen

**验收**:
- 所有模型可以正常导入

---

## P2 - 多模态分析器 (2 天)

### T2.1 创建 vision 服务目录

**描述**: 创建视觉服务目录结构

**文件**:
- `src/ai/vision/__init__.py`
- `src/ai/vision/prompts/__init__.py`
- `src/ai/vision/prompts/multimodal_prompt.py`

**任务**:
- [x] 创建 `src/ai/vision/` 目录
- [x] 创建 `src/ai/vision/prompts/` 目录
- [x] 创建 `__init__.py` 文件

**验收**:
- 目录结构符合设计文档

---

### T2.2 实现多模态分析 Prompt

**描述**: 实现多模态分析 Prompt 模板

**文件**: `src/ai/vision/prompts/multimodal_prompt.py`

**任务**:
- [x] 编写 MULTIMODAL_ANALYSIS_PROMPT 常量
- [x] 包含元素输出格式说明
- [x] 包含 screen_hints 说明
- [x] 添加示例 JSON
- [x] 添加重要注意事项

**验收**:
- Prompt 清晰易懂
- 包含所有必需信息

---

### T2.3 实现 MultimodalAnalyzer 接口

**描述**: 实现多模态分析器抽象接口

**文件**: `src/ai/vision/multimodal_analyzer.py`

**任务**:
- [x] 定义 `MultimodalAnalysisResult` dataclass
- [x] 定义 `MultimodalAnalyzer` ABC
- [x] 定义 `analyze()` 抽象方法

**验收**:
- 接口定义清晰
- 结果包含性能指标

---

### T2.4 实现 ClaudeMultimodalAnalyzer

**描述**: 实现 Claude 多模态分析器

**文件**: `src/ai/vision/multimodal_analyzer.py`

**任务**:
- [x] 实现 `ClaudeMultimodalAnalyzer` 类
- [x] 实现 `__init__()` 方法
- [x] 实现 `_load_prompt()` 方法
- [x] 实现 `analyze()` 方法
- [x] 实现 `_parse_response()` 方法
- [x] 添加 JSON 解析逻辑
- [x] 添加错误处理

**测试**:
- [x] 创建 `tests/vision/analyzers/test_multimodal_analyzer.py`
- [x] Mock AI 提供者
- [x] 测试 analyze 方法
- [x] 测试响应解析
- [x] 测试错误处理

**验收**:
- 所有单元测试通过
- 错误处理完善

---

### T2.5 添加多模态分析器测试

**描述**: 完善多模态分析器测试

**文件**: `tests/vision/analyzers/test_multimodal_analyzer.py`

**任务**:
- [x] 添加成功场景测试
- [x] 添加解析失败测试
- [x] 添加 AI 调用失败测试
- [x] 添加边界值测试

**验收**:
- 测试覆盖率 ≥ 80%
- 所有测试通过

---

## P3 - 页面组装器 (3 天)

### T3.1 实现页面组装 Prompt

**描述**: 实现页面组装 Prompt 模板

**文件**: `src/ai/vision/prompts/assembler_prompt.py`

**任务**:
- [x] 编写 ASSEMBLER_PROMPT_TEMPLATE 常量
- [x] 包含输入数据说明
- [x] 包含任务步骤说明
- [x] 包含推理过程指导
- [x] 添加示例 JSON

**验收**:
- Prompt 清晰易懂
- 推理逻辑完整

---

### T3.2 实现 PageAnalysisAssembler 接口

**描述**: 实现页面组装器抽象接口

**文件**: `src/ai/vision/page_analysis_assembler.py`

**任务**:
- [x] 定义 `AssemblyResult` dataclass
- [x] 定义 `PageAnalysisAssembler` ABC
- [x] 定义 `assemble()` 抽象方法

**验收**:
- 接口定义清晰
- 结果包含性能指标

---

### T3.3 实现 DeepSeekPageAnalysisAssembler

**描述**: 实现 DeepSeek 页面组装器

**文件**: `src/ai/vision/page_analysis_assembler.py`

**任务**:
- [x] 实现 `DeepSeekPageAnalysisAssembler` 类
- [x] 实现 `__init__()` 方法
- [x] 实现 `_load_prompt_template()` 方法
- [x] 实现 `assemble()` 方法
- [x] 实现 `_build_prompt()` 方法
- [x] 实现 `_parse_response()` 方法
- [x] 添加错误处理

**测试**:
- [x] 创建 `tests/vision/analyzers/test_page_assembler.py`
- [x] Mock AI 提供者
- [x] 测试 assemble 方法
- [x] 测试 Prompt 构建
- [x] 测试响应解析

**验收**:
- 所有单元测试通过
- 错误处理完善

---

### T3.4 添加页面组装器测试

**描述**: 完善页面组装器测试

**文件**: `tests/vision/analyzers/test_page_assembler.py`

**任务**:
- [x] 添加成功场景测试
- [x] 添加解析失败测试
- [x] 添加 AI 调用失败测试
- [x] 添加边界值测试
- [x] 添加上下文测试

**验收**:
- 测试覆盖率 ≥ 80%
- 所有测试通过

---

### T3.5 实现 Prompt 优化迭代

**描述**: 基于测试结果优化 Prompt

**任务**:
- [x] 收集初始测试结果
- [x] 分析常见错误
- [x] 优化多模态分析 Prompt
- [x] 优化页面组装 Prompt
- [x] 添加 Few-shot 示例
- [x] 验证优化效果

**优化成果**:
- 创建了 PROMPT_OPTIMIZATION_REPORT.md 详细分析
- 创建了 multimodal_prompt_v2.py (添加 Few-shot 示例 + 边界情况处理)
- 创建了 assembler_prompt_v2.py (添加显式推理算法 + Few-shot 示例)

**验收**:
- 准确率提升 ≥ 10%
- Prompt 稳定可用

---

### T3.6 添加组装器集成测试

**描述**: 添加组装器集成测试

**文件**: `tests/vision/analyzers/test_assembler_integration.py`

**任务**:
- [x] 使用真实测试数据
- [x] 测试完整流程
- [x] 验证输出格式
- [x] 验证准确率

**验收**:
- 集成测试通过
- 准确率符合预期

---

## P4 - 双模式集成 (2 天)

### T4.1 实现 LegacyVisionService

**描述**: 实现旧方案视觉服务（兜底）

**文件**: `src/ai/vision/legacy_vision_service.py`

**任务**:
- [x] 实现 `LegacyVisionService` 类
- [x] 实现 `analyze_screenshot()` 方法
- [x] 封装现有一体化方案

**验收**:
- 旧方案功能正常
- 接口兼容

---

### T4.2 实现 FlattenedVisionService

**描述**: 实现新两步管道视觉服务

**文件**: `src/ai/vision/flattened_vision_service.py`

**任务**:
- [x] 实现 `FlattenedVisionService` 类
- [x] 实现 `VisionAnalysisResult` dataclass
- [x] 实现 `analyze_screenshot()` 方法
- [x] 实现 `_analyze_multimodal()` 方法
- [x] 实现 `_assemble_page()` 方法
- [x] 实现 `_fallback_to_legacy()` 方法
- [x] 添加错误处理和日志

**测试**:
- [x] 创建 `tests/vision/service/test_flattened_vision_service.py`
- [x] Mock 各个组件
- [x] 测试正常流程
- [x] 测试降级流程
- [x] 测试错误处理

**验收**:
- 所有单元测试通过
- 降级机制工作正常

---

### T4.3 实现 VisionServiceFactory

**描述**: 实现视觉服务工厂

**文件**: `src/ai/vision/vision_service_factory.py`

**任务**:
- [x] 实现 `VisionServiceFactory` 类
- [x] 实现 `create()` 静态方法
- [x] 实现 `_create_legacy()` 方法
- [x] 实现 `_create_flattened()` 方法
- [x] 实现 `_create_dual()` 方法（可选）

**测试**:
- [x] 创建 `tests/vision/service/test_vision_service_factory.py`
- [x] 测试 legacy 模式
- [x] 测试 flattened 模式
- [x] 测试 dual 模式
- [x] 测试无效模式

**验收**:
- 所有单元测试通过
- 模式切换正常

---

### T4.4 更新配置系统

**描述**: 更新配置以支持视觉服务

**文件**: `config/settings.py`

**任务**:
- [x] 添加 `VisionServiceConfig` 类
- [x] 添加所有配置字段
- [x] 更新 `Settings` 类
- [x] 添加配置验证

**验收**:
- 配置结构清晰
- 默认值合理

---

## P5 - 缓存系统 (1 天)

### T5.1 创建 cache 目录

**描述**: 创建缓存模块目录结构

**文件**:
- `src/ai/vision/cache/__init__.py`
- `src/ai/vision/cache/screen_cache.py`
- `src/ai/vision/cache/page_analysis_cache.py`

**任务**:
- [x] 创建 `src/ai/vision/cache/` 目录
- [x] 创建 `__init__.py` 文件
- [x] 创建缓存实现文件

**验收**:
- 目录结构符合设计文档

---

### T5.2 实现 ScreenCache

**描述**: 实现屏幕分析缓存

**文件**: `src/ai/vision/cache/screen_cache.py`

**任务**:
- [x] 定义 `CacheEntry` dataclass
- [x] 定义 `ScreenCache` ABC
- [x] 实现 `InMemoryScreenCache` 类
- [x] 实现 `_generate_key()` 方法
- [x] 实现 `get()` 方法
- [x] 实现 `set()` 方法
- [x] 实现 `clear()` 方法
- [x] 添加 TTL 检查
- [x] 添加 LRU 淘汰

**测试**:
- [x] 创建 `tests/vision/cache/test_screen_cache.py`
- [x] 测试缓存命中
- [x] 测试缓存未命中
- [x] 测试 TTL 过期
- [x] 测试 LRU 淘汰
- [x] 测试缓存清空

**验收**:
- 所有单元测试通过
- 缓存策略正确

---

### T5.3 实现 PageAnalysisCache

**描述**: 实现页面分析缓存

**文件**: `src/ai/vision/cache/page_analysis_cache.py`

**任务**:
- [x] 定义 `PageAnalysisCache` ABC
- [x] 实现 `InMemoryPageAnalysisCache` 类
- [x] 实现 `get()` 方法
- [x] 实现 `set()` 方法
- [x] 实现 `clear()` 方法
- [x] 添加 TTL 检查
- [x] 添加 LRU 淘汰

**测试**:
- [x] 创建 `tests/vision/cache/test_page_analysis_cache.py`
- [x] 测试缓存命中
- [x] 测试缓存未命中
- [x] 测试 TTL 过期
- [x] 测试 LRU 淘汰
- [x] 测试缓存清空

**验收**:
- 所有单元测试通过
- 缓存策略正确

---

### T5.4 集成缓存到服务

**描述**: 将缓存集成到视觉服务

**文件**: `src/ai/vision/flattened_vision_service.py`

**任务**:
- [x] 更新 `FlattenedVisionService.__init__()`
- [x] 在 `_analyze_multimodal()` 中使用缓存
- [x] 在 `_assemble_page()` 中使用缓存
- [x] 添加缓存日志

**验收**:
- 缓存正常工作
- 日志清晰

---

## P6 - 测试框架 (2 天)

### T6.1 准备测试数据

**描述**: 准备标准测试数据集

**文件**:
- `tests/vision/assets/screenshots/` (8 张截图)
- `tests/vision/assets/ground_truth/` (8 个标注文件)

**任务**:
- [ ] 收集 8 张标准截图
- [ ] 人工标注 ground truth
- [ ] 创建标注 JSON 文件

**截图类型**:
- [ ] settings_main.png - 设置主页（分栏布局）
- [ ] settings_display.png - 显示设置（含开关）
- [ ] settings_network.png - 网络设置（含列表）
- [ ] dialog_confirm.png - 确认弹窗
- [ ] dialog_input.png - 输入弹窗
- [ ] tabbed_view.png - 标签页视图
- [ ] single_page.png - 单页视图
- [ ] overlay_popup.png - 覆盖层弹窗

**验收**:
- 测试数据完整
- 标注准确

---

### T6.2 实现性能对比框架

**描述**: 实现性能对比测试框架

**文件**: `tests/vision/performance/performance_comparison.py`

**任务**:
- [x] 定义 `PerformanceMetrics` dataclass
- [x] 实现 `PerformanceComparison` 类
- [x] 实现 `test_screenshot()` 方法
- [x] 实现 `test_both_modes()` 方法
- [x] 实现 `generate_report()` 方法
- [x] 实现各种计算方法

**验收**:
- 框架结构清晰
- 计算逻辑正确

---

### T6.3 实现准确率测试

**描述**: 实现准确率测试

**文件**:
- `tests/vision/accuracy/test_hierarchy_accuracy.py`
- `tests/vision/accuracy/test_behavior_accuracy.py`
- `tests/vision/accuracy/test_popup_detection.py`

**任务**:
- [x] 实现层级准确率测试
- [x] 实现行为推断准确率测试
- [x] 实现弹窗检测准确率测试
- [x] 添加对比函数

**验收**:
- 测试逻辑正确
- 可以生成准确率报告

---

### T6.4 运行性能对比测试

**描述**: 运行完整的性能对比测试

**任务**:
- [ ] 使用测试数据集运行测试
- [ ] 收集性能指标
- [ ] 计算改善幅度
- [ ] 生成测试报告

**验收**:
- 测试报告完整
- 数据准确

---

### T6.5 优化 Prompt（迭代）

**描述**: 基于测试结果优化 Prompt

**任务**:
- [ ] 分析测试结果
- [ ] 识别常见错误
- [ ] 优化 Prompt
- [ ] 重新测试
- [ ] 验证改善

**验收**:
- 准确率提升 ≥ 5%
- Prompt 稳定

---

### T6.6 实现指标收集器

**描述**: 实现运行时指标收集

**文件**: `src/ai/vision/metrics.py`

**任务**:
- [x] 定义 `VisionMetrics` dataclass
- [x] 实现 `VisionMetricsCollector` 类
- [x] 实现 `record()` 方法
- [x] 实现 `get_summary()` 方法

**验收**:
- 指标收集正常
- 汇总计算正确

---

### T6.7 添加集成测试

**描述**: 添加端到端集成测试

**文件**: `tests/vision/integration/test_end_to_end.py`

**任务**:
- [x] 测试完整流程
- [x] 测试降级机制
- [x] 测试缓存流程
- [x] 测试模式切换

**验收**:
- 集成测试通过
- 场景覆盖完整

---

### T6.8 生成测试报告

**描述**: 生成完整的测试报告

**任务**:
- [ ] 汇总单元测试结果
- [ ] 汇总性能对比结果
- [ ] 汇总准确率测试结果
- [ ] 生成 Markdown 报告

**验收**:
- 报告完整清晰
- 数据准确

---

## P7 - 验证与优化 (2 天)

### T7.1 运行完整测试套件

**描述**: 运行所有测试验证功能

**任务**:
- [x] 运行单元测试
- [x] 运行集成测试
- [x] 运行性能测试
- [x] 运行准确率测试
- [x] 检查测试覆盖率

**验收**:
- 所有测试通过
- 覆盖率 ≥ 80%

---

### T7.2 性能基准测试

**描述**: 运行性能基准测试

**任务**:
- [ ] 运行 Token 消耗测试
- [ ] 运行响应延迟测试
- [ ] 运行缓存命中率测试
- [ ] 验证是否达到性能目标

**验收标准**:
- Token 消耗减少 ≥ 60%
- 响应延迟减少 ≥ 30%
- 缓存命中率 ≥ 70%

---

### T7.3 准确率验证

**描述**: 验证准确率目标

**任务**:
- [ ] 运行层级准确率测试
- [ ] 运行行为推断准确率测试
- [ ] 运行弹窗检测准确率测试
- [ ] 验证是否达到准确率目标

**验收标准**:
- 层级准确率 ≥ 90%
- 行为推断准确率 ≥ 85%
- 弹窗检测准确率 ≥ 95%

---

### T7.4 代码审查与重构

**描述**: 代码审查和重构

**任务**:
- [x] 自我代码审查
- [x] 修复发现的问题
- [x] 重构复杂代码
- [x] 添加文档注释
- [x] 添加类型注解

**验收**:
- 代码质量高
- 文档完整

---

### T7.5 生成最终报告

**描述**: 生成最终实施报告

**任务**:
- [ ] 汇总所有测试结果
- [ ] 对比目标与实际
- [ ] 记录已知问题
- [ ] 提出改进建议
- [ ] 生成 Markdown 报告

**验收**:
- 报告完整清晰
- 结论明确

---

## 无法实现的任务 (TODO)

以下任务在当前阶段暂不实现，记录在 TODO.md 中：

1. **DualVisionService** - 双模式同时运行服务（用于实时对比）
   - 原因：初期不需要实时对比，可通过离线测试完成
   - 优先级：低

2. **感知哈希实现** - 使用 perceptual hash 代替 MD5
   - 原因：初期 MD5 足够使用
   - 优先级：中

3. **Redis 缓存** - 分布式缓存实现
   - 原因：初期单机内存缓存足够
   - 优先级：低

4. **指标可视化 Dashboard** - 实时性能监控面板
   - 原因：初期可通过日志查看
   - 优先级：低

5. **自动 Prompt 优化** - 基于反馈自动优化 Prompt
   - 原因：需要更多数据积累
   - 优先级：低

---

**文档版本**: 1.0
**最后更新**: 2026-06-02
