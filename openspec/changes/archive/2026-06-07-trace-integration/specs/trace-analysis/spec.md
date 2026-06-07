## ADDED Requirements

### Requirement: TraceAnalyzer 接口
系统 SHALL 提供 TraceAnalyzer 类，从 Trace 数据提取多种分析视图。

#### Scenario: 初始化分析器
- **WHEN** 创建 TraceAnalyzer(nodes)
- **THEN** 接收 Trace 节点列表
- **AND** 内部重建树结构

### Requirement: 树重建算法
系统 SHALL 支持从 Span 流重建完整的树结构，包括调用链。

#### Scenario: 建立节点索引
- **WHEN** 调用 build_tree(nodes)
- **THEN** 按 span_id 建立节点索引
- **AND** 建立 children_map 映射父子关系

#### Scenario: 建立父子关系
- **WHEN** 处理每个节点
- **THEN** 通过 parent_span_id 建立父子关系
- **AND** 将节点添加到父节点的 children 列表

#### Scenario: 处理 step_end 回填
- **WHEN** 遇到 span_type="step_end" 的 SpanNode
- **THEN** 找到对应的 StepNode
- **AND** 回填 StepNode.result 字段

#### Scenario: 处理 session_end 回填
- **WHEN** 遇到 span_type="session_end" 的 SpanNode
- **THEN** 找到 SessionNode
- **AND** 回填 SessionNode.status 和 end_time 字段

#### Scenario: 返回根节点
- **WHEN** 树重建完成
- **THEN** 返回 SessionNode 作为根节点
- **AND** 所有节点包含 children 列表

### Requirement: 页面树提取
系统 SHALL 支持从 Trace 数据提取页面层级树。

#### Scenario: 提取页面树
- **WHEN** 调用 TraceAnalyzer.extract_page_tree()
- **THEN** 从 StepNode.page_path 聚合页面层级
- **AND** 返回嵌套的页面树结构
- **AND** 包含每个页面的访问次数

### Requirement: 状态序列提取
系统 SHALL 支持提取状态转移序列。

#### Scenario: 提取状态序列
- **WHEN** 调用 TraceAnalyzer.extract_state_sequence()
- **THEN** 从 state_transition Span 提取序列
- **AND** 返回按时间排序的状态转移列表
- **AND** 每项包含 from_state、to_state、timestamp

### Requirement: Span 调用链提取
系统 SHALL 支持提取完整的 Span 调用链。

#### Scenario: 提取调用链
- **WHEN** 调用 TraceAnalyzer.extract_span_chain(span_id)
- **THEN** 从指定 Span 开始遍历调用链
- **AND** 返回从根到指定 Span 的完整路径
- **AND** 按调用顺序排列

### Requirement: AI 调用提取
系统 SHALL 支持提取所有 AI 调用记录。

#### Scenario: 提取 AI 调用
- **WHEN** 调用 TraceAnalyzer.extract_ai_calls()
- **THEN** 从 ai_call Span 提取调用记录
- **AND** 返回包含步骤上下文的 AI 调用列表
- **AND** 每项包含 capability、latency_ms、tokens、结果

### Requirement: 动作序列提取
系统 SHALL 支持提取动作执行序列。

#### Scenario: 提取动作序列
- **WHEN** 调用 TraceAnalyzer.extract_action_sequence()
- **THEN** 从 execution Span 提取动作记录
- **AND** 返回按时间排序的动作列表
- **AND** 每项包含 action、target、status、page_context

### Requirement: 错误统计提取
系统 SHALL 支持提取错误统计和分类。

#### Scenario: 提取错误统计
- **WHEN** 调用 TraceAnalyzer.extract_error_statistics()
- **THEN** 从 error Span 聚合错误信息
- **AND** 返回按类型、严重度、页面分类的统计
- **AND** 包含总错误数和各类别计数

### Requirement: 时间分析提取
系统 SHALL 支持提取时间序列分析。

#### Scenario: 提取时间分析
- **WHEN** 调用 TraceAnalyzer.extract_time_analysis()
- **THEN** 计算总耗时、平均耗时、百分位
- **AND** 识别最慢的步骤和操作
- **AND** 返回时间统计摘要

### Requirement: 覆盖率分析提取
系统 SHALL 支持提取页面覆盖率分析。

#### Scenario: 提取覆盖率分析
- **WHEN** 调用 TraceAnalyzer.extract_coverage_analysis()
- **THEN** 统计总页面数和已访问页面数
- **AND** 计算访问率百分比
- **AND** 识别未访问页面
- **AND** 生成页面访问热力图

### Requirement: Span 字段验证
系统 SHALL 对关键 Span 字段进行选择性验证。

#### Scenario: 验证内部字段
- **WHEN** 验证 Span 数据
- **THEN** 严格验证内部字段（如 from_state、to_state、action、status）
- **AND** 缺失必需字段时抛出验证错误

#### Scenario: 跳过外部字段验证
- **WHEN** 验证 Span 数据
- **THEN** 不验证外部字段（如 confidence、output_summary、stack_trace）
- **AND** 允许外部字段缺失或格式变化
