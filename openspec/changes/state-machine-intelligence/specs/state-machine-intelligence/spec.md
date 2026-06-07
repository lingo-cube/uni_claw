# Spec: State Machine Intelligence

状态机智能决策能力规格说明。

## ADDED Requirements

### Requirement: Page relationship classification

系统 SHALL 提供 `classify_relation` 纯函数，用于判断当前页面与预期页面的关系。函数返回以下四种关系之一：
- **MATCH**: 当前已在预期页面
- **NAVIGABLE**: 预期页面在当前菜单中，可直接点击导航
- **DEEPER**: 预期页面在当前路径中但非末位（走深了）
- **UNKNOWN**: 无法确定关系

#### Scenario: 已在预期页面返回 MATCH
- **GIVEN** 预期页面为 "显示"
- **AND** 当前路径为 ["设置", "显示"]
- **WHEN** 调用 `classify_relation(current_path, "显示", menus)`
- **THEN** 返回 "MATCH"

#### Scenario: 可导航返回 NAVIGABLE
- **GIVEN** 预期页面为 "声音"
- **AND** 当前路径为 ["设置", "显示"]
- **AND** 菜单包含 "声音"
- **WHEN** 调用 `classify_relation(current_path, "声音", menus)`
- **THEN** 返回 "NAVIGABLE"

#### Scenario: 走深了返回 DEEPER
- **GIVEN** 预期页面为 "显示"
- **AND** 当前路径为 ["设置", "显示", "亮度"]
- **WHEN** 调用 `classify_relation(current_path, "显示", menus)`
- **THEN** 返回 "DEEPER"

#### Scenario: 无法确定返回 UNKNOWN
- **GIVEN** 预期页面为 "显示"
- **AND** 当前路径为 ["桌面"]
- **AND** 菜单不包含 "显示"
- **WHEN** 调用 `classify_relation(current_path, "显示", menus)`
- **THEN** 返回 "UNKNOWN"

---

### Requirement: Precondition intelligent correction

系统 SHALL 在前置条件检查失败时执行智能纠正，最多重试 3 次。纠正策略基于页面关系：
- **NAVIGABLE**: 点击目标菜单项
- **DEEPER**: 执行 back 操作
- **UNKNOWN**: 执行 back 操作

每次纠正后，系统 SHALL 立即调用 vision 服务验证结果，若满足条件则提前退出。

#### Scenario: NAVIGABLE 关系点击目标菜单
- **GIVEN** 节点有前置条件要求页面 "声音"
- **AND** 当前页面不在预期页面
- **AND** 关系为 NAVIGABLE
- **WHEN** 执行 precondition handler
- **THEN** 点击目标菜单项
- **AND** 调用 vision 验证
- **AND** 若成功则进入 EXECUTE 状态

#### Scenario: DEEPER 关系执行 back
- **GIVEN** 节点有前置条件要求页面 "显示"
- **AND** 当前路径为 ["设置", "显示", "亮度"]
- **AND** 关系为 DEEPER
- **WHEN** 执行 precondition handler
- **THEN** 执行 back 操作
- **AND** 调用 vision 验证

#### Scenario: 重试耗尽进入错误处理
- **GIVEN** 前置条件检查失败
- **AND** 已重试 3 次仍不满足
- **WHEN** 执行 precondition handler
- **THEN** 进入 ERROR_HANDLING 状态
- **AND** 记录 PreconditionTimeout 错误

---

### Requirement: Frame complete auto-escape

当容器节点完成且 fallback 为 AUTO_ESCAPE 时，系统 SHALL 优先尝试切换到未访问的同级菜单，而非直接 back。

系统 SHALL：
1. 收集未访问的同级菜单（level1_menus + level2_menus）
2. 若存在未访问菜单，点击切换
3. 调用 vision 获取最新页面状态验证切换
4. 若切换成功（页面路径变化），不弹栈，返回 NODE_SELECT
5. 若切换失败或无未访问菜单，执行 back 并弹栈

#### Scenario: 存在未访问同级菜单成功切换
- **GIVEN** 容器节点完成
- **AND** fallback 为 AUTO_ESCAPE
- **AND** 存在未访问的同级菜单 "网络"
- **WHEN** 执行 frame_complete handler
- **THEN** 点击 "网络" 菜单
- **AND** 调用 vision 验证页面变化
- **AND** 若路径变化，不弹栈
- **AND** 返回 NODE_SELECT 状态

#### Scenario: 无未访问菜单执行 back
- **GIVEN** 容器节点完成
- **AND** fallback 为 AUTO_ESCAPE
- **AND** 所有同级菜单已访问
- **WHEN** 执行 frame_complete handler
- **THEN** 执行 back 操作
- **AND** 弹出当前栈帧
- **AND** 返回 NODE_SELECT 状态

#### Scenario: 切换失败降级 back
- **GIVEN** 容器节点完成
- **AND** fallback 为 AUTO_ESCAPE
- **AND** 存在未访问菜单但切换失败
- **WHEN** 执行 frame_complete handler
- **THEN** 重试 1 次后降级为 back
- **AND** 弹出当前栈帧

---

### Requirement: Popup intelligent handling

系统 SHALL 在检测到弹窗时优先查找安全按钮并点击，找不到安全按钮时才执行 back。

安全按钮关键词：["取消", "关闭", "否", "忽略", "稍后", "Cancel", "Close", "No"]

#### Scenario: 找到安全按钮点击
- **GIVEN** 进入 POPUP_HANDLING 状态
- **AND** 页面 items 包含 "取消" 按钮
- **WHEN** 执行 popup handler
- **THEN** 点击 "取消" 按钮
- **AND** 返回 RESULT_VERIFY 状态

#### Scenario: 找不到安全按钮执行 back
- **GIVEN** 进入 POPUP_HANDLING 状态
- **AND** 页面 items 不包含任何安全按钮
- **WHEN** 执行 popup handler
- **THEN** 执行 back 操作
- **AND** 返回 RESULT_VERIFY 状态

---

### Requirement: Error policy integration

系统 SHALL 支持节点级 ErrorPolicy 处理，根据 policy.on_error 执行相应动作：
- **retry**: 重试当前节点操作（不超过 max_retries）
- **skip**: 跳过当前节点，选择下一节点
- **backtrack**: 弹出当前栈帧，进入 FRAME_COMPLETE
- **abort**: 终止遍历
- **fallback**: 尝试导航到 fallback_target

系统 SHALL 在 context.failed_nodes 中记录失败信息，包括错误类型、错误消息、重试次数和时间戳。

#### Scenario: Retry policy with remaining retries
- **GIVEN** 节点 ErrorPolicy.on_error 为 "retry"
- **AND** max_retries 为 3
- **AND** 已重试 1 次
- **WHEN** 进入 ERROR_HANDLING 状态
- **THEN** 重试计数加 1
- **AND** 返回 EXECUTE 状态

#### Scenario: Retry policy exceeded max retries
- **GIVEN** 节点 ErrorPolicy.on_error 为 "retry"
- **AND** max_retries 为 3
- **AND** 已重试 3 次
- **WHEN** 进入 ERROR_HANDLING 状态
- **THEN** 跳过当前节点
- **AND** 返回 NODE_SELECT 状态

#### Scenario: Backtrack policy
- **GIVEN** 节点 ErrorPolicy.on_error 为 "backtrack"
- **WHEN** 进入 ERROR_HANDLING 状态
- **THEN** 弹出当前栈帧
- **AND** 返回 FRAME_COMPLETE 状态

#### Scenario: Abort policy
- **GIVEN** 节点 ErrorPolicy.on_error 为 "abort"
- **WHEN** 进入 ERROR_HANDLING 状态
- **THEN** 设置 context.global_state 为 TERMINATED
- **AND** 返回 BRANCH 状态

---

### Requirement: Step exception handling

系统 SHALL 在 `step()` 方法中使用 try-catch 包装所有状态处理逻辑。

当任何异常抛出时，系统 SHALL：
1. 将异常对象设置到 `context.last_error`
2. 将 `context.consecutive_errors` 加 1
3. 将下一状态设置为 ERROR_HANDLING

#### Scenario: Handler 抛出异常被捕获
- **GIVEN** 任意 handler 抛出异常
- **WHEN** 执行 step() 方法
- **THEN** 异常被 try-catch 捕获
- **AND** context.last_error 被设置为该异常
- **AND** context.consecutive_errors 加 1
- **AND** 下一状态为 ERROR_HANDLING

#### Scenario: 正常执行无异常
- **GIVEN** 所有 handler 正常执行
- **WHEN** 执行 step() 方法
- **THEN** context.last_error 保持不变
- **AND** 下一状态由 handler 决定

---

### Requirement: Vision call timing and delay

系统 SHALL 支持在 action.execute() 后调用 vision 时添加可配置延迟。

延迟配置通过 `context.wait_after_action_ms` 控制：
- 默认值：100ms
- 仿真环境：可为 0ms
- 生产环境：建议 100ms

#### Scenario: 使用配置的延迟
- **GIVEN** context.wait_after_action_ms 为 100
- **WHEN** action.execute() 后调用 vision
- **THEN** 等待 100ms 后执行 vision.analyze_screenshot()

#### Scenario: 仿真环境零延迟
- **GIVEN** context.wait_after_action_ms 为 0
- **WHEN** action.execute() 后调用 vision
- **THEN** 立即执行 vision.analyze_screenshot()

---

### Requirement: Trace metrics recording

系统 SHALL 在每个 handler 执行过程中记录相应的 trace metrics：
- **ai_call**: Vision 调用指标（capability, success, latency_ms, page_id, element_count）
- **execution**: 动作执行指标（action, status, target, duration_ms）
- **error**: 错误指标（error_type, error_message）

metrics 存储在 `TraversalStateMachine._last_handler_metrics` 中，供引擎读取并写入 trace。

#### Scenario: Precondition vision call 记录 ai_call metrics
- **GIVEN** precondition handler 调用 vision.analyze_screenshot()
- **AND** 调用成功，耗时 150ms
- **WHEN** vision 调用返回
- **THEN** _last_handler_metrics 包含 ai_call 指标
- **AND** capability 为 "vision"
- **AND** success 为 true
- **AND** latency_ms 为 150

#### Scenario: Execution action 记录 execution metrics
- **GIVEN** handler 执行 click 动作
- **AND** 动作成功
- **WHEN** action.execute() 返回
- **THEN** _last_handler_metrics 包含 execution 指标
- **AND** action 为 "click"
- **AND** status 为 "success"

#### Scenario: Error 记录 error metrics
- **GIVEN** 进入 ERROR_HANDLING 状态
- **AND** last_error 为 VisionTimeout
- **WHEN** 执行 error handler
- **THEN** _last_handler_metrics 包含 error 指标
- **AND** error_type 为 "VisionTimeout"
