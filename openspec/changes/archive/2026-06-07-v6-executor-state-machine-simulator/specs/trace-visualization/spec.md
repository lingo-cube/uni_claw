## ADDED Requirements

### Requirement: InMemoryTracer 定义

系统 SHALL 提供 InMemoryTracer 类用于内存中的 Trace 记录。

#### Scenario: 创建 Tracer
- **WHEN** 创建 InMemoryTracer 实例
- **THEN** 系统 SHALL 初始化：
  - steps: 空列表
  - visited_tree: 空字典

### Requirement: 状态转换记录

系统 SHALL 提供 record_transition() 方法记录状态转换。

#### Scenario: 记录完整转换
- **WHEN** 调用 record_transition(transition)
- **THEN** 系统 SHALL 创建 TraceStep 并添加到 steps

#### Scenario: TraceStep 内容
- **WHEN** 创建 TraceStep
- **THEN** 系统 SHALL 包含：
  - step_number: 递增的步数
  - timestamp: 当前时间戳
  - from_state: 源状态
  - to_state: 目标状态
  - node_id: 节点 ID（可选）
  - action: 执行的动作（可选）
  - screen_info: 屏幕信息字典
  - metadata: 元数据字典

#### Scenario: 更新访问树
- **WHEN** 转换包含 node_id
- **THEN** 系统 SHALL 更新 visited_tree

### Requirement: 访问树更新

系统 SHALL 维护遍历访问树结构。

#### Scenario: 添加节点
- **WHEN** 记录包含新 node_id 的转换
- **THEN** 系统 SHALL 在 visited_tree 中创建或更新节点

#### Scenario: 标记访问
- **WHEN** 节点被访问
- **THEN** 系统 SHALL 设置 visited: true

#### Scenario: 标记恢复
- **WHEN** 节点执行后恢复
- **THEN** 系统 SHALL 设置 restored: true

#### Scenario: 构建层次结构
- **WHEN** 子节点被访问
- **THEN** 系统 SHALL 在父节点的 children 中添加子节点

### Requirement: ASCII 树渲染

系统 SHALL 提供 render_tree() 方法生成 ASCII 格式的遍历树。

#### Scenario: 渲染基本树
- **WHEN** 调用 render_tree()
- **THEN** 系统 SHALL 返回缩进格式的树结构

#### Scenario: 节点格式
- **WHEN** 渲染节点
- **THEN** 系统 SHALL 使用格式：`name [type] ✓`

#### Scenario: 恢复标记
- **WHEN** 节点设置了 restored
- **THEN** 系统 SHALL 添加 `(已恢复)` 标记

#### Scenario: 未访问标记
- **WHEN** 节点未设置 visited
- **THEN** 系统 SHALL 使用 ✗ 标记

#### Scenario: 连接字符
- **WHEN** 渲染树结构
- **THEN** 系统 SHALL 使用以下字符：
  - `│   `: 继续分支
  - `├── `: 中间子节点
  - `└── `: 最后子节点

#### Scenario: 根节点无前缀
- **WHEN** 渲染根节点
- **THEN** 系统 SHALL 不添加缩进前缀

### Requirement: Mermaid 图渲染

系统 SHALL 提供 render_mermaid() 方法生成 Mermaid 状态图。

#### Scenario: 渲染 Mermaid 图
- **WHEN** 调用 render_mermaid()
- **THEN** 系统 SHALL 返回有效的 Mermaid 代码

#### Scenario: 图类型
- **WHEN** 渲染 Mermaid
- **THEN** 系统 SHALL 使用 stateDiagram-v2 格式

#### Scenario: 初始状态
- **WHEN** 渲染 Mermaid
- **THEN** 系统 SHALL 包含 `[*] --> NODE_SELECT`

#### Scenario: 状态转换
- **WHEN** 渲染每个 TraceStep
- **THEN** 系统 SHALL 添加对应的转换行

#### Scenario: 转换格式
- **WHEN** 添加状态转换
- **THEN** 系统 SHALL 使用格式：`from_state --> to_state : Step N`

#### Scenario: 终止状态
- **WHEN** 渲染 Mermaid
- **THEN** 系统 SHALL 包含 `COMPLETED --> [*]`

### Requirement: HTML 报告渲染

系统 SHALL 提供 render_html() 方法生成 HTML 报告。

#### Scenario: 渲染 HTML
- **WHEN** 调用 render_html()
- **THEN** 系统 SHALL 返回有效的 HTML 文档

#### Scenario: HTML 结构
- **WHEN** 渲染 HTML
- **THEN** 系统 SHALL 包含：
  - <html>、<head>、<body> 标签
  - 标题区域
  - 遍历树区域
  - 状态转换表格

#### Scenario: 树嵌入
- **WHEN** 渲染 HTML
- **THEN** 系统 SHALL 在 <pre> 标签中嵌入 ASCII 树

#### Scenario: 表格嵌入
- **WHEN** 渲染 HTML
- **THEN** 系统 SHALL 在 <table> 标签中列出所有转换

#### Scenario: 表格列
- **WHEN** 渲染转换表格
- **THEN** 系统 SHALL 包含以下列：
  - 步数
  - 源状态
  - 目标状态

### Requirement: JSONL 导出

系统 SHALL 支持 JSONL 格式的 Trace 导出。

#### Scenario: 导出 JSONL
- **WHEN** 调用 export_trace("jsonl")
- **THEN** 系统 SHALL 返回 JSONL 格式字符串

#### Scenario: JSONL 格式
- **WHEN** 导出 JSONL
- **THEN** 每个 TraceStep SHALL 为一行 JSON

#### Scenario: 字段序列化
- **WHEN** 导出 TraceStep
- **THEN** 系统 SHALL 包含所有字段

### Requirement: Trace 获取

系统 SHALL 提供 get_trace() 方法获取完整 Trace。

#### Scenario: 获取 Trace
- **WHEN** 调用 get_trace()
- **THEN** 系统 SHALL 返回 steps 列表的副本

#### Scenario: Trace 不可变
- **WHEN** 修改 get_trace() 返回的列表
- **THEN** 系统 SHALL 不影响内部 steps

### Requirement: 格式验证

系统 SHALL 确保所有输出格式有效。

#### Scenario: Mermaid 验证
- **WHEN** 渲染 Mermaid
- **THEN** 输出 SHALL 可被 Mermaid 解析器解析

#### Scenario: HTML 验证
- **WHEN** 渲染 HTML
- **THEN** 输出 SHALL 为有效的 HTML 文档

#### Scenario: JSONL 验证
- **WHEN** 导出 JSONL
- **THEN** 每行 SHALL 为有效的 JSON

### Requirement: 可视化定制

系统 SHALL 支持可视化输出的定制。

#### Scenario: 树深度限制
- **WHEN** 指定最大深度
- **THEN** render_tree() SHALL 仅渲染到指定深度

#### Scenario: 状态过滤
- **WHEN** 指定状态过滤器
- **THEN** render_mermaid() SHALL 仅包含过滤的状态

#### Scenario: 步数范围
- **WHEN** 指定步数范围
- **THEN** 所有渲染方法 SHALL 仅包含范围内的步骤

### Requirement: 可视化性能

系统 SHALL 确保可视化操作高效执行。

#### Scenario: 大 Trace 渲染
- **WHEN** Trace 包含超过 1000 步
- **THEN** 渲染操作 SHALL 在 2 秒内完成

#### Scenario: 内存使用
- **WHEN** 存储大型 Trace
- **THEN** 内存使用 SHALL 保持合理

### Requirement: 嵌入式样式

系统 SHALL 为 HTML 输出提供基本样式。

#### Scenario: HTML 样式
- **WHEN** 渲染 HTML
- **THEN** 系统 SHALL 包含基本 CSS 样式

#### Scenario: 表格样式
- **WHEN** 渲染 HTML 表格
- **THEN** 系统 SHALL 添加边框和间距样式

#### Scenario: 代码块样式
- **WHEN** 渲染 HTML 代码块
- **THEN** 系统 SHALL 使用等宽字体
