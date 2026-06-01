## ADDED Requirements

### Requirement: AI-Driven Exception Handler

系统 SHALL 提供 `AIDrivenExceptionHandler` 类。

Handler SHALL 实现 `ExceptionHandler` 接口。

Handler SHALL 在处理异常时调用 AIProvider.make_decision()。

Handler SHALL 优先于规则型 Handler 执行。

Handler 仅处理 ERROR 和 CRITICAL 严重性的异常。

#### Scenario: AI Handler 处理异常
- **WHEN** 发生 ElementNotFoundException
- **AND** 严重性为 ERROR
- **THEN** AI Handler 处理该异常
- **AND** 调用 AIProvider.make_decision()

#### Scenario: FATAL 异常不处理
- **WHEN** 异常严重性为 FATAL
- **THEN** AI Handler 返回 can_handle=False
- **AND** 传递给 FatalExceptionHandler

---

### Requirement: AI Decision Types

系统 SHALL 支持以下 AI 决策类型：

- `RETRY`: 重试当前操作
- `SKIP`: 跳过当前节点
- `BACKTRACK`: 回退到上级节点
- `NAVIGATE`: 导航到指定路径
- `RECOVER`: 执行恢复操作
- `WAIT_AND_RETRY`: 等待后重试

每种决策类型 SHALL 关联特定的 action_params。

#### Scenario: RETRY 决策
- **WHEN** AI 决策为 RETRY
- **AND** action_params.wait_time = 2.0
- **THEN** 等待 2 秒后重试操作

#### Scenario: SKIP 决策
- **WHEN** AI 决策为 SKIP
- **THEN** 标记当前节点为 SKIPPED
- **AND** 移动到下一个节点

#### Scenario: NAVIGATE 决策
- **WHEN** AI 决策为 NAVIGATE
- **AND** action_params.target_path = ["车辆设置", "DiLink"]
- **THEN** 执行路径导航
- **AND** 验证导航结果

---

### Requirement: Decision Context Building

系统 SHALL 构建 `DecisionContext` 传递给 AI。

Context SHALL 包含：
- `exception`: 异常信息
- `exception_type`: 异常类型
- `retry_count`: 当前重试次数
- `current_path`: 当前遍历路径
- `current_state`: 当前状态
- `node_stack`: 节点栈快照
- `screenshot_data`: 当前截图（可选）

Context SHALL 在每次异常处理时动态构建。

#### Scenario: 构建完整的决策上下文
- **WHEN** 发生异常
- **THEN** DecisionContext 包含所有必需字段
- **AND** 截图数据存在时包含在 Context 中

#### Scenario: 截图不存在时继续处理
- **WHEN** 无法获取截图
- **THEN** screenshot_data 为 None
- **AND** AI 决策继续进行（不依赖截图）

---

### Requirement: Decision Execution

系统 SHALL 提供 `AIDecisionExecutor` 执行 AI 决策。

Executor SHALL 根据决策类型调用对应的方法。

Executor SHALL 处理执行失败的情况。

Executor SHALL 返回 `ExceptionHandlingResult`。

#### Scenario: 成功执行 RETRY 决策
- **WHEN** 决策为 RETRY
- **AND** action_params.wait_time = 1.5
- **THEN** 等待 1.5 秒
- **AND** 返回 ExceptionHandlingResult(action=RETRY)

#### Scenario: 执行失败时的回退
- **WHEN** 决策执行失败
- **THEN** 返回 ExceptionHandlingResult(action=BACKTRACK)
- **AND** message 包含"执行失败，回退"

---

### Requirement: Decision History Recording

系统 SHALL 记录所有 AI 决策历史。

记录 SHALL 包含：
- 决策时间戳
- 异常上下文
- AI 决策内容
- 决策执行结果
- 最终结果

历史记录 SHALL 可查询和导出。

#### Scenario: 记录决策
- **WHEN** AI 做出决策
- **THEN** 创建 AIDecisionRecord
- **AND** 添加到决策历史列表

#### Scenario: 查询决策历史
- **WHEN** 调用 get_decision_history()
- **THEN** 返回所有决策记录
- **AND** 记录按时间倒序排列

---

### Requirement: Decision Learning

系统 SHALL 提供 `AIDecisionLearner` 分析决策效果。

Learner SHALL 计算以下统计：
- 总决策次数
- 各决策类型分布
- 成功/失败结果统计
- 成功率

Learner SHALL 能够生成改进建议。

#### Scenario: 分析决策有效性
- **WHEN** 调用 analyze_effectiveness()
- **THEN** 返回包含成功率的统计
- **AND** 返回各决策类型的次数

#### Scenario: 生成改进建议
- **WHEN** 存在失败的决策记录
- **THEN** generate_improvement_prompt() 返回建议文本
- **AND** 建议基于失败案例分析

---

### Requirement: Feedback Loop

系统 SHALL 支持决策结果反馈。

用户或系统 SHALL 能够标记决策的最终结果。

反馈 SHALL 关联到原始决策记录。

系统 SHALL 使用反馈优化决策策略。

#### Scenario: 标记决策结果
- **WHEN** 决策执行完成
- **THEN** 更新 AIDecisionRecord.final_outcome
- **AND** final_outcome 为 "success" 或 "failure"

#### Scenario: 使用反馈优化
- **WHEN** 收集到足够的反馈数据
- **THEN** 系统分析失败模式
- **AND** 生成优化建议

---

### Requirement: Confidence-Based Execution

系统 SHALL 基于置信度决定是否执行 AI 决策。

如果置信度低于阈值（默认 0.6），系统 SHALL 回退到规则处理。

置信度阈值 SHALL 可配置。

#### Scenario: 高置信度决策执行
- **WHEN** 决策置信度为 0.85
- **AND** 阈值为 0.6
- **THEN** 执行 AI 决策

#### Scenario: 低置信度决策回退
- **WHEN** 决策置信度为 0.5
- **AND** 阈值为 0.6
- **THEN** 回退到规则型处理
- **AND** 记录"置信度过低"日志

---

### Requirement: Fallback to Rule-Based

系统 SHALL 在 AI 不可用时回退到规则型处理。

回退条件包括：
- AI 服务超时
- AI 返回无效响应
- AI 调用失败

回退时 SHALL 调用下一个优先级的 Handler。

#### Scenario: AI 超时回退
- **WHEN** AI 调用超时（>30秒）
- **THEN** 返回 IGNORE
- **AND** 传递给下一个 Handler

#### Scenario: AI 无效响应回退
- **WHEN** AI 返回无法解析的 JSON
- **THEN** 回退到规则型处理
- **AND** 记录"AI 响应无效"日志

---

### Requirement: Exception Handler Integration

AIDrivenExceptionHandler SHALL 集成到异常处理链。

Handler 优先级 SHALL 为 1（仅次于 FatalExceptionHandler）。

处理链 SHALL 按以下顺序执行：
1. FatalExceptionHandler
2. AIDrivenExceptionHandler（新增）
3. DeviceExceptionHandler
4. UIExceptionHandler
5. RetryHandler
6. BacktrackHandler

#### Scenario: AI Handler 在链中的位置
- **WHEN** 发生可恢复异常
- **THEN** AI Handler 优先执行
- **AND** AI Handler 失败后传递给后续 Handler

#### Scenario: AI Handler 返回 IGNORE
- **WHEN** AI Handler 返回 IGNORE
- **THEN** 传递给 DeviceExceptionHandler
- **AND** 继续处理链执行

---

### Requirement: Screenshot Analysis

系统 SHALL 支持将截图传递给 AI 进行异常分析。

截图 SHALL 作为 DecisionContext 的一部分。

AI SHALL 能够基于截图识别异常原因（如弹窗、加载状态等）。

截图分析 SHALL 可选，不影响无截图时的处理。

#### Scenario: 截图辅助异常分析
- **WHEN** 异常发生且截图可用
- **THEN** 截图传递给 AI
- **AND** AI 基于截图做出更准确的决策

#### Scenario: 无截图时正常处理
- **WHEN** 截图不可用
- **THEN** AI 基于文本上下文决策
- **AND** 不抛出异常
