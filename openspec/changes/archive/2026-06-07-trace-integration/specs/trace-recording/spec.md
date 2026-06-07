## ADDED Requirements

### Requirement: 分布式追踪术语
系统 SHALL 使用行业标准的分布式追踪术语：Trace ID（全局追踪 ID）、Span ID（节点 ID）、Parent Span ID（父节点 ID）。

#### Scenario: Trace ID 全局唯一
- **WHEN** 开始新的遍历任务
- **THEN** 生成全局唯一的 Trace ID
- **AND** 所有节点共享相同的 Trace ID

#### Scenario: Span ID 节点唯一
- **WHEN** 创建新的 Trace 节点
- **THEN** 生成节点唯一的 Span ID
- **AND** Span ID 使用 ULID 格式

#### Scenario: Parent Span ID 建立调用链
- **WHEN** 创建子节点
- **THEN** 设置 parent_span_id 指向父节点的 Span ID
- **AND** 建立完整的调用链关系

### Requirement: ULID 标识符
系统 SHALL 使用 ULID 作为 Trace ID 和 Span ID 的生成格式。

#### Scenario: ULID 格式验证
- **WHEN** 生成新标识符
- **THEN** 返回 26 字符的 Base32 编码字符串
- **AND** 按字典序可排序（按时间）
- **AND** URL 安全

### Requirement: Trace 节点类型
系统 SHALL 支持三种 Trace 节点类型：SessionNode（会话根节点）、StepNode（遍历步骤）、SpanNode（操作单元）。

#### Scenario: SessionNode 作为根节点
- **WHEN** 开始新的遍历任务
- **THEN** 创建 SessionNode
- **AND** SessionNode.parent_span_id 为 None
- **AND** SessionNode.span_id 作为 Trace 根节点

#### Scenario: StepNode 表示遍历步骤
- **WHEN** 引擎选择新的遍历节点
- **THEN** 创建 StepNode
- **AND** StepNode.parent_span_id 指向父步骤或 Session

#### Scenario: SpanNode 表示操作单元
- **WHEN** 组件执行操作（AI 调用、动作执行等）
- **THEN** 创建 SpanNode
- **AND** SpanNode.parent_span_id 指向所属步骤或调用链父节点

### Requirement: Span 类型定义
系统 SHALL 支持以下 Span 类型：state_transition、execution、ai_call、error、step_end、session_end。

#### Scenario: state_transition Span
- **WHEN** 状态机发生状态转换
- **THEN** 记录 state_transition Span
- **AND** 包含 from_state、to_state、state_machine 字段

#### Scenario: execution Span
- **WHEN** 执行动作（点击、滑动、返回、输入等）
- **THEN** 记录 execution Span
- **AND** 包含 action、status、target、page_before、page_after 字段

#### Scenario: ai_call Span
- **WHEN** 调用 AI 服务（视觉分析、决策等）
- **THEN** 记录 ai_call Span
- **AND** 包含 capability、provider_id、success、latency_ms、input_tokens、output_tokens 字段

#### Scenario: error Span
- **WHEN** 发生错误
- **THEN** 记录 error Span
- **AND** 包含 error_type、error_message、severity、stack_trace 字段

#### Scenario: step_end Span
- **WHEN** 遍历步骤结束
- **THEN** 记录 step_end Span
- **AND** 回填对应 StepNode 的 result 字段

#### Scenario: session_end Span
- **WHEN** 遍历任务结束
- **THEN** 记录 session_end Span
- **AND** 回填 SessionNode 的 status 和 end_time 字段

### Requirement: TraceRecorder 接口
系统 SHALL 提供 TraceRecorder 接口，支持初始化、步骤记录、Span 记录和结束。

#### Scenario: 初始化 Session
- **WHEN** 调用 TraceRecorder.init(session_node, trace_id)
- **THEN** 设置 session_node.trace_id
- **AND** 写入 session_node 到存储

#### Scenario: 记录步骤开始
- **WHEN** 调用 TraceRecorder.record_step_start(step_node, parent_span_id)
- **THEN** 设置 step.parent_span_id
- **AND** 写入 step_node 到存储
- **AND** 压栈到 StepTracker

#### Scenario: 记录 Span
- **WHEN** 调用 TraceRecorder.record_span(span, parent_span_id)
- **THEN** 设置 span.parent_span_id
- **AND** 写入 span 到存储

#### Scenario: 记录步骤结束
- **WHEN** 调用 TraceRecorder.record_step_end(step_span_id, result)
- **THEN** 创建 step_end Span
- **AND** 写入到存储
- **AND** 从 StepTracker 弹栈

#### Scenario: 结束 Session
- **WHEN** 调用 TraceRecorder.finalize(status, end_time, trace_id)
- **THEN** 创建 session_end Span
- **AND** 写入到存储

### Requirement: StepTracker 栈管理
系统 SHALL 使用 StepTracker 管理步骤栈，自动计算 parent_span_id。

#### Scenario: 节点进入时压栈
- **WHEN** 调用 StepTracker.on_node_enter(span_id)
- **THEN** span_id 压入栈顶
- **AND** 栈顶元素成为新的 parent_span_id

#### Scenario: 节点退出时弹栈
- **WHEN** 调用 StepTracker.on_node_exit()
- **THEN** 弹出栈顶元素
- **AND** 恢复上一个 parent_span_id

#### Scenario: 获取当前父 Span ID
- **WHEN** 调用 StepTracker.get_parent_span_id()
- **THEN** 返回栈顶元素的 span_id
- **AND** 栈为空时返回 None

### Requirement: Trace 写入失败处理
系统 SHALL 采用"日志继续"策略，写入失败不应中断遍历。

#### Scenario: 写入失败时记录警告
- **WHEN** TraceStorage.write() 抛出异常
- **THEN** 记录警告日志
- **AND** 不中断主流程
- **AND** 继续执行遍历
