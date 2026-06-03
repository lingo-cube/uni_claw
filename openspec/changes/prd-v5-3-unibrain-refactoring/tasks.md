# Tasks: PRD V5.3 UniBrain架构重构

**Change ID**: `prd-v5-3-unibrain-refactoring`
**Created**: 2026-06-02
**Status**: Design Phase

---

## 任务概览 (Task Overview)

| 阶段 | 任务数 | 估时 | 状态 |
|------|--------|------|------|
| P0 - Mock数据基础设施 | 6 | 1周 | 待开始 |
| P1 - Provider抽象层 | 8 | 1-2周 | 待开始 |
| P2 - 提示词管理系统 | 5 | 1周 | 待开始 |
| P3 - 追踪系统集成 | 4 | 1周 | 待开始 |
| P4 - UniBrain重构 | 5 | 1周 | 待开始 |
| P5 - 测试迁移和验证 | 7 | 1周 | 待开始 |
| P6 - 数据采集和性能监控 | 5 | 1周 | 待开始 |

**总计**: 40 任务，约 7-8 周

---

## P0 - Mock数据基础设施 (1 周)

### T0.1 创建Mock Provider基础

**描述**: 创建Mock Provider基础实现

**文件**:
- `tests/ai/fixtures/mock_providers.py`
- `tests/ai/fixtures/__init__.py`

**任务**:
- [x] 创建 `MockProvider` 基础类
- [x] 实现三种模式Mock (text, vision, multimodal)
- [x] 创建预录制响应数据结构
- [x] 实现简单的Mock响应逻辑

**验收**:
- MockProvider功能完整
- 可以用于单元测试

---

### T0.2 录制真实API响应数据

**描述**: 录制真实API响应用于测试

**文件**:
- `tests/ai/fixtures/recorded_responses.py`

**任务**:
- [x] 录制DeepSeek文本响应 (5个场景)
- [x] 录制Claude视觉响应 (5个场景)
- [x] 录制MiMo多模态响应 (3个场景)
- [x] 创建响应数据结构
- [x] 添加token和性能数据

**验收**:
- 录制数据覆盖主要场景
- 数据格式正确

---

### T0.3 实现录制回放机制

**描述**: 实现录制回放机制

**文件**:
- `tests/ai/fixtures/recording.py`

**任务**:
- [x] 实现响应录制器
- [x] 实现回放器
- [x] 添加录制脚本
- [x] 创建回放测试逻辑

**验收**:
- 录制回放功能正常
- 测试可以使用回放数据

---

### T0.4 创建测试fixtures

**描述**: 创建pytest fixtures

**文件**:
- `tests/conftest.py`

**任务**:
- [x] 创建 `mock_provider` fixture
- [x] 创建 `unibrain_with_mocks` fixture
- [ ] 创建 `real_provider` fixture (可选)
- [x] 添加fixture配置

**验收**:
- Fixtures功能正常
- 测试可以使用fixtures

---

### T0.5 配置pytest使用mock

**描述**: 配置pytest默认使用mock

**文件**:
- `pytest.ini`
- `tests/conftest.py`

**任务**:
- [x] 配置pytest.ini
- [x] 添加测试标记
- [x] 配置默认使用mock
- [x] 添加真实API测试标记

**验收**:
- 默认测试零API成本
- 可以选择性使用真实API

---

### T0.6 编写Mock使用文档

**描述**: 编写Mock数据使用文档

**文件**:
- `tests/ai/MOCK_USAGE.md`

**任务**:
- [x] 编写Mock数据使用说明
- [x] 添加录制回放说明
- [x] 添加测试配置说明
- [x] 添加成本控制说明

**验收**:
- 文档完整清晰
- 开发者可以理解使用

---

## P1 - Provider抽象层 (1-2 周)

### T1.1 创建providers目录

**描述**: 创建Provider模块目录结构

**文件**:
- `src/ai/providers/__init__.py`
- `src/ai/providers/base.py`

**任务**:
- [x] 创建 `src/ai/providers/` 目录
- [x] 创建 `__init__.py`
- [x] 创建基础模块文件

**验收**:
- 目录结构符合设计

---

### T1.2 实现AIProvider抽象接口

**描述**: 实现AIProvider统一接口

**文件**:
- `src/ai/providers/base.py`

**任务**:
- [x] 实现 `AIResponse` dataclass
- [x] 实现 `AIProvider` ABC
- [x] 定义 `provider_id` 属性
- [x] 定义 `supported_modes` 属性
- [x] 实现 `complete_text()` 抽象方法
- [x] 实现 `complete_vision()` 抽象方法
- [x] 实现 `complete_multimodal()` 抽象方法
- [x] 实现 `get_token_estimate()` 方法
- [x] 实现 `get_performance_rating()` 方法
- [x] 实现 `health_check()` 方法

**测试**:
- [x] 创建 `tests/ai/providers/test_base.py`
- [x] 测试接口定义
- [x] 测试抽象方法

**验收**:
- 接口定义清晰
- 所有方法签名正确

---

### T1.3 实现DeepSeekProvider

**描述**: 实现DeepSeek Provider

**文件**:
- `src/ai/providers/deepseek.py`

**任务**:
- [x] 实现 `DeepSeekProvider` 类
- [x] 实现 `provider_id` 属性
- [x] 实现 `supported_modes` 属性
- [x] 实现 `complete_text()` 方法
- [x] 实现 `complete_vision()` 方法 (不支持)
- [x] 实现 `complete_multimodal()` 方法 (不支持)
- [x] 实现 `get_token_estimate()` 方法
- [x] 实现 `get_performance_rating()` 方法
- [x] 添加错误处理

**测试**:
- [x] 创建 `tests/ai/providers/test_deepseek.py`
- [x] Mock HTTP请求
- [x] 测试文本补全
- [x] 测试错误处理
- [x] 测试token估算

**验收**:
- 所有单元测试通过
- 错误处理完善

---

### T1.4 实现ClaudeProvider

**描述**: 实现Claude Provider

**文件**:
- `src/ai/providers/claude.py`

**任务**:
- [x] 实现 `ClaudeProvider` 类
- [x] 实现 `provider_id` 属性
- [x] 实现 `supported_modes` 属性
- [x] 实现 `complete_text()` 方法
- [x] 实现 `complete_vision()` 方法
- [x] 实现 `complete_multimodal()` 方法
- [x] 实现 `get_token_estimate()` 方法
- [x] 实现 `get_performance_rating()` 方法
- [x] 添加错误处理

**测试**:
- [x] 创建 `tests/ai/providers/test_claude.py`
- [x] Mock HTTP请求
- [x] 测试三种模式
- [x] 测试错误处理

**验收**:
- 所有单元测试通过
- 支持所有三种模式

---

### T1.5 实现MiMoProvider

**描述**: 实现MiMo Provider

**文件**:
- `src/ai/providers/mimo.py`

**任务**:
- [x] 实现 `MiMoProvider` 类
- [x] 实现 `provider_id` 属性
- [x] 实现 `supported_modes` 属性
- [x] 实现 `complete_text()` 方法 (不支持)
- [x] 实现 `complete_vision()` 方法
- [x] 实现 `complete_multimodal()` 方法
- [x] 实现 `get_token_estimate()` 方法
- [x] 实现 `get_performance_rating()` 方法
- [x] 添加错误处理

**测试**:
- [x] 创建 `tests/ai/providers/test_mimo.py`
- [x] Mock HTTP请求
- [x] 测试视觉和多模态模式
- [x] 测试错误处理

**验收**:
- 所有单元测试通过
- 支持视觉和多模态

---

### T1.6 实现Provider配置

**描述**: 实现Provider配置管理

**文件**:
- `src/ai/core/config.py`

**任务**:
- [x] 实现 `AIProviderConfig` dataclass
- [x] 添加配置验证
- [x] 支持环境变量注入
- [x] 添加配置文档

**验收**:
- 配置管理清晰
- 支持环境变量

---

### T1.7 更新providers模块导出

**描述**: 更新providers模块导出

**文件**:
- `src/ai/providers/__init__.py`

**任务**:
- [x] 导出 AIProvider, AIResponse
- [x] 导出 DeepSeekProvider
- [x] 导出 ClaudeProvider
- [x] 导出 MiMoProvider
- [x] 导出 AIProviderConfig

**验收**:
- 所有类可以正常导入

---

### T1.8 实现Provider集成测试

**描述**: 实现Provider集成测试

**文件**:
- `tests/ai/providers/test_providers_integration.py`

**任务**:
- [x] 测试所有Provider一致性
- [x] 测试Provider切换
- [x] 测试错误处理
- [x] 测试性能评级

**验收**:
- 集成测试通过
- 一致性验证通过

---

## P2 - 提示词管理系统 (1 周)

### T2.1 创建提示词目录

**描述**: 创建提示词模块目录结构

**文件**:
- `src/ai/prompts/__init__.py`
- `src/ai/prompts/manager.py`

**任务**:
- [x] 创建 `src/ai/prompts/` 目录
- [x] 创建 `__init__.py`
- [x] 创建Manager文件

**验收**:
- 目录结构符合设计

---

### T2.2 实现PromptManager

**描述**: 实现提示词管理器

**文件**:
- `src/ai/prompts/manager.py`

**任务**:
- [x] 实现 `PromptTemplate` dataclass
- [x] 实现 `PromptManager` 类
- [x] 实现 `_load_prompts()` 方法
- [x] 实现 `_parse_prompt_file()` 方法
- [x] 实现 `get_prompt()` 方法
- [x] 实现 `inject_variables()` 方法
- [x] 实现 `list_capabilities()` 方法
- [x] 实现 `reload_prompts()` 方法

**测试**:
- [x] 创建 `tests/ai/prompts/test_manager.py`
- [x] 测试提示词加载
- [x] 测试变量注入
- [x] 测试版本管理

**验收**:
- 所有单元测试通过
- 提示词加载正常

---

### T2.3 创建提示词模板文件

**描述**: 创建5大核心能力提示词

**文件**:
- `src/ai/prompts/analyze_visual.md`
- `src/ai/prompts/parse_instruction.md`
- `src/ai/prompts/verify_page_type.md`
- `src/ai/prompts/decide_next_action.md`
- `src/ai/prompts/screen_safety.md`

**任务**:
- [x] 编写 analyze_visual 提示词
- [x] 编写 parse_instruction 提示词
- [x] 编写 verify_page_type 提示词
- [x] 编写 decide_next_action 提示词
- [x] 编写 screen_safety 提示词
- [x] 添加变量定义
- [x] 添加示例JSON

**验收**:
- 所有提示词格式正确
- 变量定义完整

---

### T2.4 实现提示词验证

**描述**: 实现提示词格式验证

**文件**:
- `src/ai/prompts/validator.py`

**任务**:
- [x] 实现YAML front matter验证
- [x] 实现变量验证
- [x] 实现格式验证
- [x] 添加验证工具

**验收**:
- 验证功能正常
- 可以检测格式错误

---

### T2.5 实现提示词热重载

**描述**: 实现提示词热重载功能

**文件**:
- `src/ai/prompts/manager.py`

**任务**:
- [x] 实现 `reload_prompts()` 方法
- [x] 添加文件监听 (可选)
- [x] 测试热重载功能

**验收**:
- 热重载功能正常
- 不影响运行时服务

---

## P3 - 追踪系统集成 (1 周)

### T3.1 创建trace目录

**描述**: 创建追踪模块目录结构

**文件**:
- `src/ai/trace/__init__.py`
- `src/ai/trace/integration.py`
- `src/ai/trace/models.py`

**任务**:
- [x] 创建 `src/ai/trace/` 目录
- [x] 创建 `__init__.py`
- [x] 创建模块文件

**验收**:
- 目录结构符合设计

---

### T3.2 实现追踪数据模型

**描述**: 实现追踪数据模型

**文件**:
- `src/ai/trace/models.py`

**任务**:
- [x] 实现 `SpanContext` dataclass
- [x] 实现 `AICallTrace` dataclass
- [x] 实现 `ProviderPerformanceMetrics` dataclass
- [x] 添加转换方法

**测试**:
- [x] 创建 `tests/ai/trace/test_models.py`
- [x] 测试数据模型
- [x] 测试序列化

**验收**:
- 数据模型功能完整

---

### T3.3 实现TraceIntegration

**描述**: 实现追踪集成系统

**文件**:
- `src/ai/trace/integration.py`

**任务**:
- [x] 实现 `TraceIntegration` 类
- [x] 实现 `start_span()` 方法
- [x] 实现 `inject_context()` 方法
- [x] 实现 `finish_span()` 方法
- [x] 实现 `record_metrics()` 方法
- [x] 实现 `get_active_spans()` 方法
- [x] 添加日志记录

**测试**:
- [x] 创建 `tests/ai/trace/test_integration.py`
- [x] 测试span生命周期
- [x] 测试上下文注入
- [x] 测试指标记录

**验收**:
- 所有单元测试通过
- 追踪功能正常

---

### T3.4 集成现有TraceLogger

**描述**: 集成现有的TraceLogger

**文件**:
- `src/ai/trace/integration.py`

**任务**:
- [x] 检测TraceLogger可用性
- [x] 实现TraceLogger适配器
- [x] 添加降级处理
- [x] 测试集成

**验收**:
- 与现有TraceLogger兼容
- 降级处理正常

---

## P4 - UniBrain重构 (1 周)

### T4.1 创建Provider路由配置

**描述**: 创建Provider路由配置文件

**文件**:
- `config/ai_providers.yaml`

**任务**:
- [x] 创建配置文件结构
- [x] 定义Provider配置
- [x] 定义能力路由映射
- [x] 定义默认设置
- [x] 添加配置验证

**验收**:
- 配置文件格式正确
- 路由映射完整

---

### T4.2 实现配置加载

**描述**: 实现配置加载逻辑

**文件**:
- `src/ai/unibrain.py`

**任务**:
- [x] 实现 `_load_routing_config()` 方法
- [x] 实现 `_load_providers_from_config()` 方法
- [x] 实现 `_resolve_env_var()` 方法
- [x] 添加配置验证
- [x] 添加错误处理

**验收**:
- 配置加载正常
- 环境变量解析正确

---

### T4.3 实现能力路由

**描述**: 实现声明式能力路由

**文件**:
- `src/ai/unibrain.py`

**任务**:
- [x] 实现 `_select_provider()` 方法
- [x] 实现路由映射缓存
- [x] 添加降级逻辑
- [x] 测试路由功能

**验收**:
- 路由功能正常
- 降级处理正确

---

### T4.4 重构5大核心能力

**描述**: 重构UniBrain的5大核心能力

**文件**:
- `src/ai/unibrain.py`

**任务**:
- [x] 重构 `analyze_visual()` 方法
- [x] 重构 `parse_instruction()` 方法
- [x] 重构 `verify_page_type()` 方法
- [x] 重构 `decide_next_action()` 方法
- [x] 重构 `screen_safety()` 方法
- [x] 集成提示词管理
- [x] 集成追踪系统
- [x] 保持接口兼容

**测试**:
- [x] 创建 `tests/ai/test_unibrain_refactored.py`
- [x] 测试所有能力
- [x] 测试接口兼容性

**验收**:
- 所有单元测试通过
- 接口保持兼容

---

### T4.5 实现错误处理和降级

**描述**: 实现完整的错误处理和降级机制

**文件**:
- `src/ai/unibrain.py`

**任务**:
- [x] 实现Provider降级
- [x] 实现错误恢复
- [x] 添加详细日志
- [x] 实现重试逻辑 (可选)

**验收**:
- 错误处理完善
- 降级机制正常

---

## P5 - 测试迁移和验证 (1 周)

### T5.1 迁移test_ai_advisor

**描述**: 迁移test_ai_advisor.py

**文件**:
- `tests/ai/test_unibrain.py`

**任务**:
- [ ] 分析现有测试
- [ ] 迁移到新架构
- [ ] 使用Mock Provider
- [ ] 验证功能等价

**验收**:
- 所有迁移测试通过
- 功能等价验证通过

---

### T5.2 迁移test_ai_capabilities

**描述**: 迁移test_ai_capabilities.py

**文件**:
- `tests/ai/test_providers.py`

**任务**:
- [ ] 分析现有测试
- [ ] 迁移到Provider抽象
- [ ] 使用Mock Provider
- [ ] 验证功能等价

**验收**:
- 所有迁移测试通过
- 功能等价验证通过

---

### T5.3 迁移test_ai_vision_service

**描述**: 迁移test_ai_vision_service.py

**文件**:
- `tests/ai/test_vision_service.py`

**任务**:
- [ ] 分析现有测试
- [ ] 迁移到新架构
- [ ] 使用Mock Provider
- [ ] 验证功能等价

**验收**:
- 所有迁移测试通过
- 功能等价验证通过

---

### T5.4 扩展test_ai_unibrain

**描述**: 扩展test_ai_unibrain.py

**文件**:
- `tests/ai/test_unibrain.py`

**任务**:
- [ ] 添加新架构测试
- [ ] 测试Provider路由
- [ ] 测试提示词管理
- [ ] 测试追踪集成

**验收**:
- 新测试覆盖率>90%

---

### T5.5 更新验证脚本

**描述**: 更新scripts/verify_refactor.py

**文件**:
- `scripts/verify_refactor.py`

**任务**:
- [ ] 添加Provider健康检查
- [ ] 添加提示词验证
- [ ] 添加路由配置验证
- [ ] 添加Mock数据验证

**验收**:
- 验证脚本功能完整
- 所有检查项通过

---

### T5.6 运行完整测试套件

**描述**: 运行所有测试验证功能

**任务**:
- [ ] 运行单元测试
- [ ] 运行集成测试
- [ ] 运行迁移测试
- [ ] 检查测试覆盖率
- [ ] 验证零API成本

**验收标准**:
- 所有测试100%通过
- 测试覆盖率≥90%
- 默认测试零API成本

---

### T5.7 性能回归测试

**描述**: 运行性能回归测试

**任务**:
- [ ] 测试响应延迟
- [ ] 测试token消耗
- [ ] 对比基准性能
- [ ] 验证无性能退化

**验收标准**:
- 无性能退化
- 延迟变化<10%

---

## P6 - 数据采集和性能监控 (1 周)

### T6.1 实现性能数据采集

**描述**: 实现性能数据采集系统

**文件**:
- `src/ai/performance/collector.py`

**任务**:
- [ ] 实现数据采集器
- [ ] 采集延迟数据
- [ ] 采集token数据
- [ ] 采集成功率数据
- [ ] 添加数据存储

**验收**:
- 数据采集功能正常
- 数据存储完整

---

### T6.2 实现token统计监控

**描述**: 实现token统计监控

**文件**:
- `src/ai/performance/token_monitor.py`

**任务**:
- [ ] 实现token统计
- [ ] 实现token效率分析
- [ ] 添加token告警
- [ ] 生成token报告

**验收**:
- token统计准确
- 告警功能正常

---

### T6.3 实现延迟监控

**描述**: 实现延迟监控系统

**文件**:
- `src/ai/performance/latency_monitor.py`

**任务**:
- [ ] 实现延迟统计
- [ ] 实现P50/P95/P99计算
- [ ] 添加延迟告警
- [ ] 生成延迟报告

**验收**:
- 延迟统计准确
- 告警功能正常

---

### T6.4 实现质量监控

**描述**: 实现质量监控系统

**文件**:
- `src/ai/performance/quality_monitor.py`

**任务**:
- [ ] 实现准确率监控
- [ ] 实现错误率监控
- [ ] 添加质量告警
- [ ] 生成质量报告

**验收**:
- 质量监控正常
- 告警功能正常

---

### T6.5 创建监控仪表板

**描述**: 创建监控仪表板

**文件**:
- `dashboards/ai_performance.html`

**任务**:
- [ ] 创建仪表板界面
- [ ] 添加延迟图表
- [ ] 添加token图表
- [ ] 添加质量图表
- [ ] 添加实时更新

**验收**:
- 仪表板功能完整
- 数据可视化正常

---

## 无法实现的任务 (TODO)

以下任务在当前阶段暂不实现，记录在 TODO.md 中：

1. **自动Prompt优化** - 基于反馈自动优化Prompt
   - 原因：需要更多数据积累
   - 优先级：低

2. **分布式追踪** - 实现完整的分布式追踪
   - 原因：初期单机追踪足够
   - 优先级：低

3. **Provider动态发现** - 动态发现和注册Provider
   - 原因：初期静态配置足够
   - 优先级：低

4. **高级缓存策略** - 实现更复杂的缓存策略
   - 原因：初期简单缓存足够
   - 优先级：低

---

**文档版本**: 1.0
**最后更新**: 2026-06-02
