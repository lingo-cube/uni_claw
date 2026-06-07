## ADDED Requirements

### Requirement: Session 数据模型
系统 SHALL 定义统一的 Session 数据类，管理遍历任务元数据。

#### Scenario: Session 字段定义
- **WHEN** 查看 Session 定义
- **THEN** 包含以下字段：
  - `session_id: str` - 全局唯一标识（即 Trace ID）
  - `device_id: Optional[str]` - 设备 ID（可选）
  - `device_name: Optional[str]` - 设备名称（可选）
  - `device_model: str` - 设备型号
  - `os_version: str` - 操作系统版本
  - `app_version: Optional[str]` - 应用版本（可选）
  - `app_package: Optional[str]` - 应用包名（可选）
  - `start_time: float` - 开始时间戳
  - `end_time: Optional[float]` - 结束时间戳（可选）
  - `status: str` - 会话状态（默认 "running"）
  - `traversal_mode: str` - 遍历模式（默认 "graph"）
  - `config: Dict[str, Any]` - 配置信息

### Requirement: Session 创建
系统 SHALL 在遍历开始前创建 Session 实例。

#### Scenario: 生成 Session ID
- **WHEN** 引擎开始新的遍历任务
- **THEN** 生成 ULID 格式的 session_id
- **AND** session_id 作为全局 Trace ID

#### Scenario: 初始化 Session 状态
- **WHEN** 创建 Session
- **THEN** 设置 start_time 为当前时间戳
- **AND** 设置 status 为 "running"
- **AND** 设置 traversal_mode 为配置的模式

### Requirement: Session 独立存储
系统 SHALL 将 Session 元数据独立存储为 session.json 文件。

#### Scenario: 存储 Session 文件
- **WHEN** Session 创建完成
- **THEN** 将 Session 序列化为 JSON
- **AND** 写入 traces/{trace_id}/session.json
- **AND** 文件使用 trace_id 作为目录名

#### Scenario: 更新 Session 状态
- **WHEN** 遍历状态变化（完成、错误、终止）
- **THEN** 更新 Session.status 和 Session.end_time
- **AND** 重新写入 session.json 文件

### Requirement: Session 与 SessionNode 分离
系统 SHALL 维护 Session 和 SessionNode 的职责分离。

#### Scenario: Session 用途
- **WHEN** 需要访问任务元数据
- **THEN** 从 session.json 读取 Session
- **AND** Session 作为权威元数据源

#### Scenario: SessionNode 用途
- **WHEN** 分析 Trace 树
- **THEN** 从 Trace 读取 SessionNode
- **AND** SessionNode 是 Session 的快照

#### Scenario: Trace ID 一致性
- **WHEN** 创建 SessionNode
- **THEN** SessionNode.trace_id = Session.session_id
- **AND** 确保全局 Trace ID 一致

### Requirement: Session 状态管理
系统 SHALL 支持标准的会话状态转换。

#### Scenario: 初始状态
- **WHEN** Session 创建
- **THEN** status 为 "running"

#### Scenario: 正常完成
- **WHEN** 遍历正常完成
- **THEN** status 更新为 "completed"
- **AND** 设置 end_time

#### Scenario: 错误终止
- **WHEN** 遍历因错误终止
- **THEN** status 更新为 "error"
- **AND** 设置 end_time

#### Scenario: 手动终止
- **WHEN** 遍历被手动终止
- **THEN** status 更新为 "terminated"
- **AND** 设置 end_time

### Requirement: Session 配置存储
系统 SHALL 支持在 Session 中存储遍历配置。

#### Scenario: 存储配置快照
- **WHEN** 创建 Session
- **THEN** 将遍历配置存储到 config 字段
- **AND** config 包含所有影响遍历行为的参数

#### Scenario: 配置用于重放
- **WHEN** 分析或重放 Trace
- **THEN** 从 Session.config 读取配置
- **AND** 使用相同配置重现遍历
