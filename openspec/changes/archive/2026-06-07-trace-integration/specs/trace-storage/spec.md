## ADDED Requirements

### Requirement: TraceStorage 抽象接口
系统 SHALL 定义 TraceStorage 抽象接口，支持可插拔的存储后端。

#### Scenario: 写入节点
- **WHEN** 调用 TraceStorage.write(node)
- **THEN** 将节点写入存储后端
- **AND** 支持所有 TraceNode 子类

#### Scenario: 读取 Trace
- **WHEN** 调用 TraceStorage.read(trace_id)
- **THEN** 返回该 Trace ID 的所有节点列表
- **AND** 节点按写入顺序排列

### Requirement: FileStorage 实现
系统 SHALL 提供 FileStorage 实现，将 Trace 节点写入 JSONL 文件。

#### Scenario: 创建 Trace 目录
- **WHEN** 初始化 FileStorage(session_dir)
- **THEN** 创建 traces/{trace_id}/ 目录
- **AND** 创建 trace.jsonl 文件

#### Scenario: 写入 JSONL 格式
- **WHEN** 调用 FileStorage.write(node)
- **THEN** 将节点序列化为 JSON
- **AND** 追加写入 trace.jsonl 文件
- **AND** 每行一个节点

#### Scenario: 读取 JSONL 文件
- **WHEN** 调用 FileStorage.read(trace_id)
- **THEN** 读取 trace.jsonl 文件
- **AND** 解析每行 JSON 为节点对象
- **AND** 返回节点列表

#### Scenario: 队列缓冲写入
- **WHEN** 调用 FileStorage.write(node)
- **THEN** 节点进入写入队列
- **AND** 后台线程处理队列写入
- **AND** 主线程不阻塞

#### Scenario: 队列满时处理
- **WHEN** 写入队列达到容量上限
- **THEN** 阻塞直到队列有空间
- **AND** 防止内存溢出

### Requirement: MemoryStorage 实现
系统 SHALL 提供 MemoryStorage 实现，用于仿真环境。

#### Scenario: 内存存储节点
- **WHEN** 调用 MemoryStorage.write(node)
- **THEN** 节点存储在内存列表
- **AND** 不涉及任何 I/O 操作

#### Scenario: 内存读取节点
- **WHEN** 调用 MemoryStorage.read(trace_id)
- **THEN** 返回内存中的节点列表
- **AND** 无延迟访问

#### Scenario: 多 Trace 隔离
- **WHEN** 多个 Trace 写入同一个 MemoryStorage
- **THEN** 按 trace_id 隔离节点
- **AND** read() 只返回指定 Trace 的节点

### Requirement: 存储目录结构
系统 SHALL 使用标准化的存储目录结构。

#### Scenario: Trace 目录组织
- **WHEN** 创建新的 Trace
- **THEN** 创建 traces/{trace_id}/ 目录
- **AND** trace_id 作为目录名

#### Scenario: Session 文件位置
- **WHEN** 存储 Session 元数据
- **THEN** 写入 traces/{trace_id}/session.json
- **AND** Session 字段序列化为 JSON

#### Scenario: Trace 文件位置
- **WHEN** 存储 Trace 节点
- **THEN** 写入 traces/{trace_id}/trace.jsonl
- **AND** 每行一个节点 JSON

#### Scenario: 截图目录
- **WHEN** 存储截图
- **THEN** 创建 traces/{trace_id}/screenshots/ 目录
- **AND** 创建 screenshots/index.json 映射文件

### Requirement: 截图引用机制
系统 SHALL 使用 ID 引用机制存储截图。

#### Scenario: 生成截图引用 ID
- **WHEN** 捕获新截图
- **THEN** 生成唯一引用 ID（格式：s_{ulid}）
- **AND** 在 Span 中使用 screenshot_ref 字段引用

#### Scenario: 截图文件存储
- **WHEN** 保存截图文件
- **THEN** 文件名为 {context}_{timestamp}.png
- **AND** 存储在 traces/{trace_id}/screenshots/ 目录

#### Scenario: 截图索引映射
- **WHEN** 保存截图
- **THEN** 更新 screenshots/index.json
- **AND** 映射引用 ID 到文件路径
