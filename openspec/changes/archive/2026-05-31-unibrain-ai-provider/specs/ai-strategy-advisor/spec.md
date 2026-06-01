## MODIFIED Requirements

### Requirement: AI Strategy Advisor 抽象接口
系统 SHALL 提供抽象基类 `AIStrategyAdvisor`，定义三个核心方法用于 AI 辅助决策。

#### Scenario: 接口定义
- **WHEN** 开发者查看 `AIStrategyAdvisor` 抽象类
- **THEN** 该类包含以下抽象方法：
  - `infer_container_type(ui: PageAnalysis, context: TraversalContext) -> ContainerInference`
  - `decide_next_action(goal: DecisionGoal, ui: PageAnalysis, context: TraversalContext) -> Tuple[DecisionResult, Optional[TraversalNode]]`
  - `handle_exception(exception: ExceptionContext, ui: PageAnalysis, context: TraversalContext) -> Tuple[DecisionResult, Optional[TraversalNode]]`

### Requirement: 容器类型推断
系统 SHALL 提供容器类型推断能力，当规则引擎无法确定容器类型时调用 AI 进行推断。

#### Scenario: 成功推断容器类型
- **WHEN** 规则引擎无法识别当前页面容器类型
- **THEN** 调用 `infer_container_type` 方法
- **AND** 返回 `ContainerInference` 包含容器类型、置信度和匹配模板

#### Scenario: 推断失败
- **WHEN** AI 无法确定容器类型
- **THEN** 返回 `ContainerInference` 类型为 `UNKNOWN` 且置信度低于阈值

### Requirement: 目标决策
系统 SHALL 提供目标决策能力，当需要达成特定目标但规则无法定位时调用 AI 决策。

#### Scenario: 成功决策下一步操作
- **WHEN** 需要返回设置根页面或关闭弹窗但规则无法定位目标
- **THEN** 调用 `decide_next_action` 方法
- **AND** 返回 `(DecisionResult.SUCCESS, TraversalNode)` 元组
- **AND** `TraversalNode` 包含可执行的操作

#### Scenario: 决策不确定
- **WHEN** AI 无法确定下一步操作
- **THEN** 返回 `(DecisionResult.UNSURE, None)` 元组
- **AND** 规则引擎接管决策

#### Scenario: 放弃决策
- **WHEN** AI 判断无法达成目标且无法恢复
- **THEN** 返回 `(DecisionResult.GIVE_UP, None)` 元组
- **AND** 遍历终止

### Requirement: 异常兜底处理
系统 SHALL 提供异常兜底能力，当责任链所有处理器无法处理异常时调用 AI。

#### Scenario: 成功恢复异常
- **WHEN** 责任链耗尽且 AI 能够提供恢复方案
- **THEN** 返回 `(DecisionResult.SUCCESS, TraversalNode)` 元组
- **AND** `TraversalNode` 包含恢复操作

#### Scenario: 无法恢复
- **WHEN** AI 判断异常无法恢复
- **THEN** 返回 `(DecisionResult.GIVE_UP, None)` 元组
- **AND** 记录审计日志

### Requirement: NoOp 默认实现
系统 SHALL 提供 `NoOpAIAdvisor` 作为默认实现，保证现有功能不受影响。

#### Scenario: NoOp 容器推断
- **WHEN** 调用 `NoOpAIAdvisor.infer_container_type`
- **THEN** 返回 `ContainerInference` 类型为 `UNKNOWN` 且置信度为 0.0

#### Scenario: NoOp 目标决策
- **WHEN** 调用 `NoOpAIAdvisor.decide_next_action`
- **THEN** 返回 `(DecisionResult.UNSURE, None)` 元组

#### Scenario: NoOp 异常处理
- **WHEN** 调用 `NoOpAIAdvisor.handle_exception`
- **THEN** 返回 `(DecisionResult.GIVE_UP, None)` 元组

### Requirement: Mock 测试实现
系统 SHALL 提供 `MockAIAdvisor` 用于单元测试和集成测试。

#### Scenario: Mock 容器推断
- **WHEN** 调用 `MockAIAdvisor.infer_container_type`
- **THEN** 返回预定义的 `ContainerInference` 结果

#### Scenario: Mock 目标决策
- **WHEN** 调用 `MockAIAdvisor.decide_next_action`
- **THEN** 返回预定义的 `(DecisionResult.SUCCESS, TraversalNode)` 元组

#### Scenario: Mock 异常处理
- **WHEN** 调用 `MockAIAdvisor.handle_exception`
- **THEN** 返回预定义的 `(DecisionResult.SUCCESS, TraversalNode)` 元组

## ADDED Requirements

### Requirement: UniBrain Provider 实现
系统 SHALL 提供 `UniBrain` 类实现 `AIStrategyAdvisor` 接口，使用 DeepSeek LLM 和 Vision Service 提供能力。

#### Scenario: 创建 UniBrain 实例
- **WHEN** 创建 `UniBrain(provider_config, vision_service)`
- **THEN** 初始化 LLM 客户端和响应验证器
- **AND** 初始化 Prompt 注册表
- **AND** 注册所有解析器
- **AND** 初始化五个 AI 能力

#### Scenario: 容器类型推断使用 AI
- **WHEN** 调用 `UniBrain.infer_container_type`
- **THEN** 使用 `VerifyPageTypeCapability` 分析页面
- **AND** 返回 AI 推断的容器类型

#### Scenario: 目标决策使用 AI
- **WHEN** 调用 `UniBrain.decide_next_action`
- **THEN** 使用 `ContextDecisionCapability` 做出决策
- **AND** 返回 AI 决定的下一步操作

#### Scenario: 异常处理使用 AI
- **WHEN** 调用 `UniBrain.handle_exception`
- **THEN** 将异常转换为决策目标
- **AND** 使用 `ContextDecisionCapability` 提供恢复方案

### Requirement: 视觉分析能力
系统 SHALL 通过 `UniBrain` 提供视觉分析能力，直接分析截图获取页面结构。

#### Scenario: 分析截图
- **WHEN** 调用 `UniBrain.analyze_screenshot(image_data)`
- **THEN** 使用 `VisionAnalysisCapability` 分析截图
- **AND** 返回完整的 `PageAnalysis` 对象

#### Scenario: 结合 Vision 验证页面类型
- **WHEN** 调用 `UniBrain.verify_page_with_vision(image_data, expected_type)`
- **THEN** 先使用 Vision 分析屏幕
- **AND** 再使用 AI 验证页面类型
- **AND** 返回 `PageTypeVerification` 对象

### Requirement: 能力注册
系统 SHALL 在 UniBrain 中注册所有 AI 能力。

#### Scenario: 五个能力初始化
- **WHEN** UniBrain 初始化
- **THEN** 创建以下能力：
  - `ParseToPlanCapability` - 指令解析
  - `VerifyPageTypeCapability` - 页面验证
  - `ScreenSafetyCapability` - 安全筛选
  - `VisionAnalysisCapability` - 视觉分析
  - `ContextDecisionCapability` - 上下文决策

#### Scenario: 能力通过配置访问
- **WHEN** 需要访问特定能力
- **THEN** 通过 `capabilities` 字典按键名访问
- **AND** 键名为：parse, verify, safety, vision, decision

### Requirement: 解析器注册
系统 SHALL 在 UniBrain 初始化时注册所有响应解析器。

#### Scenario: 注册所有解析器
- **WHEN** UniBrain 初始化
- **THEN** 调用 `_register_parsers()` 方法
- **AND** 注册以下解析器：
  - `TraversalPlan` - 遍历计划
  - `PageTypeVerification` - 页面验证
  - `SafetyScreeningResult` - 安全筛选
  - `PageAnalysis` - 页面分析
  - `ContextDecisionResult` - 上下文决策

### Requirement: 置信度阈值处理
系统 SHALL 在决策时检查置信度阈值。

#### Scenario: 置信度低于阈值
- **WHEN** AI 决策的置信度低于 0.7
- **THEN** 返回 `DecisionResult.UNSURE`
- **AND** 让规则引擎接管

#### Scenario: 置信度高于阈值
- **WHEN** AI 决策的置信度高于 0.7
- **THEN** 返回 `DecisionResult.SUCCESS`
- **AND** 包含 AI 决策的操作节点

### Requirement: 安全验证集成
系统 SHALL 在 UniBrain 中集成安全验证机制。

#### Scenario: 决策前安全验证
- **WHEN** 做出上下文决策
- **THEN** 先调用 `ScreenSafetyCapability` 筛选元素
- **AND** 决策遵守安全筛选结果
- **AND** `safety_verified` 标志为 true

#### Scenario: 安全筛选失败处理
- **WHEN** `ScreenSafetyCapability` 执行失败
- **THEN** 进入安全模式
- **AND** 决策仅允许 back 操作
- **AND** 记录安全事件到审计日志
