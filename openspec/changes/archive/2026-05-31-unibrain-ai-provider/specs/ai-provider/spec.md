## ADDED Requirements

### Requirement: AI Provider Configuration
系统 SHALL 提供 `AIProviderConfig` 数据类，用于配置 AI Provider 的运行参数。

#### Scenario: 基础配置
- **WHEN** 创建 `AIProviderConfig` 实例
- **THEN** 配置包含以下字段：
  - `api_key`: API 密钥
  - `model`: 模型名称（默认 `deepseek-v4-flash`）
  - `base_url`: API 基础 URL（默认 DeepSeek API）
  - `max_concurrent_requests`: 最大并发请求数（默认 4）
  - `request_timeout`: 请求超时时间（默认 30.0 秒）
  - `reasoning_detail`: 推理级别（concise/step_by_step/detailed）

#### Scenario: 重试配置
- **WHEN** 配置重试策略
- **THEN** `RetryConfig` 包含：
  - `max_attempts`: 最大尝试次数（默认 1，即不重试）
  - `base_delay`: 基础延迟（默认 1.0 秒）
  - `max_delay`: 最大延迟（默认 8.0 秒）
  - `exponential_base`: 指数退避基数（默认 2.0）

#### Scenario: 降级配置
- **WHEN** 配置降级策略
- **THEN** `FallbackConfig` 包含：
  - `strategy`: 降级策略（none/partial/full）
  - `partial_allowlist`: 允许降级的非关键能力列表

### Requirement: LLM 客户端
系统 SHALL 提供 `LLMClient` 类，负责调用 DeepSeek API 并处理重试逻辑。

#### Scenario: 成功调用 API
- **WHEN** 调用 `LLMClient.call()` 方法
- **THEN** 返回结构化的 JSON 响应
- **AND** 响应符合提供的 JSON Schema

#### Scenario: 重试机制
- **WHEN** API 调用失败（速率限制、超时、API 错误）
- **THEN** 自动重试，使用指数退避策略
- **AND** 最多重试 `max_attempts` 次
- **AND** 重试延迟在 `base_delay` 和 `max_delay` 之间

#### Scenario: 并发控制
- **WHEN** 同时发起多个 API 调用
- **THEN** 最多 `max_concurrent_requests` 个并发请求
- **AND** 超出的请求在队列中等待

### Requirement: 响应验证器
系统 SHALL 提供 `ResponseValidator` 类，使用注册解析器模式验证和解析 AI 响应。

#### Scenario: 注册解析器
- **WHEN** 调用 `register_parser(response_type, parser)` 方法
- **THEN** 将解析器函数注册到该响应类型
- **AND** 后续该类型的响应使用注册的解析器

#### Scenario: 验证和解析响应
- **WHEN** 调用 `validate_and_parse(response, response_type)` 方法
- **THEN** 使用注册的解析器解析响应
- **AND** 验证响应符合 JSON Schema
- **AND** 返回解析后的数据对象

#### Scenario: 解析器未找到
- **WHEN** 响应类型没有注册的解析器
- **THEN** 抛出 `ParserNotFoundError` 异常

#### Scenario: 验证失败
- **WHEN** 响应不符合 JSON Schema
- **THEN** 抛出 `ValidationError` 异常

### Requirement: 泛型能力基类
系统 SHALL 提供 `BaseCapability[T_IN, T_OUT]` 抽象基类，定义所有 AI 能力的统一执行流程。

#### Scenario: 异步执行
- **WHEN** 调用 `execute_async(input_data)` 方法
- **THEN** 准备输入变量
- **AND** 获取 Prompt 模板并注入变量
- **AND** 调用 LLM API
- **AND** 验证和解析响应
- **AND** 执行内部验证（如果启用）
- **AND** 返回类型安全的 `T_OUT` 结果

#### Scenario: 同步执行包装
- **WHEN** 调用 `execute(input_data)` 方法
- **THEN** 在事件循环中运行异步执行
- **AND** 返回同步结果

#### Scenario: 记录执行耗时
- **WHEN** 能力执行完成
- **THEN** 记录执行耗时到日志
- **AND** 格式为 "Response received in {duration:.2f}s"

#### Scenario: 归档失败信息
- **WHEN** 能力执行失败
- **THEN** 记录错误到日志
- **AND** 归档失败信息（输入、错误、时间戳）
- **AND** 抛出异常

### Requirement: Prompt 注册表
系统 SHALL 提供 `PromptRegistry` 类，统一管理所有 AI 能力的 Prompt 模板。

#### Scenario: 获取 Prompt 模板
- **WHEN** 调用 `get(key)` 方法
- **THEN** 返回对应的 Prompt 模板
- **AND** 替换 `{{REASONING_LEVEL}}` 为配置的推理级别文本

#### Scenario: 注册自定义 Prompt
- **WHEN** 调用 `register(key, prompt)` 方法
- **THEN** 将自定义 Prompt 模板注册到该键
- **AND** 后续该键返回自定义模板

#### Scenario: 推理级别注入
- **WHEN** 配置 `reasoning_detail` 为不同值
- **THEN** `{{REASONING_LEVEL}}` 被替换为：
  - `concise`: "简要说明你的分析过程"
  - `step_by_step`: "分步骤说明你的分析过程"
  - `detailed`: "详细分析每个因素和决策依据"

### Requirement: 变量注入机制
系统 SHALL 支持模板变量注入，将运行时数据注入到 Prompt 模板中。

#### Scenario: 基础变量注入
- **WHEN** 模板包含 `{variable_name}` 占位符
- **THEN** 替换为 `variables['variable_name']` 的值

#### Scenario: 推理级别注入
- **WHEN** 模板包含 `{{REASONING_LEVEL}}` 占位符
- **THEN** 替换为配置的推理级别文本

### Requirement: 失败归档
系统 SHALL 归档所有 AI 能力执行失败的信息，用于后续分析和 Prompt 优化。

#### Scenario: 归档失败记录
- **WHEN** AI 能力执行失败
- **THEN** 创建失败记录包含：
  - `capability`: 能力名称
  - `input_data`: 输入数据
  - `error`: 错误信息
  - `timestamp`: 时间戳
- **AND** 写入失败归档文件（JSONL 格式）

### Requirement: 指标收集
系统 SHALL 收集 AI 能力执行的指标，用于性能监控和成本管理。

#### Scenario: 记录调用指标
- **WHEN** AI 能力执行完成
- **THEN** 记录以下指标：
  - 调用次数（按能力和成功/失败分类）
  - 调用延迟
  - 置信度分布
  - Token 使用量（如果可用）
