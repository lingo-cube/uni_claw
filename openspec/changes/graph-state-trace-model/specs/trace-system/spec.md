## ADDED Requirements

### Requirement: TraversalTrace 结构
系统 SHALL 提供 `TraversalTrace` 数据类封装遍历全过程记录。

#### Scenario: Trace 包含必要信息
- **WHEN** 创建 `TraversalTrace` 实例
- **THEN** 该实例包含以下字段：
  - `session_info` - 会话信息（设备、应用版本、时间、遍历模式）
  - `steps` - `List[TraceStep]` 步骤记录列表
  - `state_snapshots` - `List[StateSnapshot]` 状态快照列表
  - `summary` - `TraceSummary` 统计摘要

### Requirement: TraceStep 步骤记录
系统 SHALL 记录遍历过程中的每一步操作。

#### Scenario: TraceStep 包含必要信息
- **WHEN** 创建 `TraceStep` 实例
- **THEN** 该实例包含以下字段：
  - `step_id` - 自增序号
  - `timestamp` - 时间戳
  - `global_state` - 当前全局状态
  - `traversal_state` - 当前遍历状态
  - `page_analysis_summary` - 当前屏幕摘要
  - `decision` - 决策详情（节点 ID、类型、操作目标）
  - `execution` - 执行结果（成功/失败、耗时、截图引用）
  - `stack_snapshot` - 节点栈快照（从底到顶的节点 ID 列表）
  - `path_before` - 操作前 `current_path`
  - `path_after` - 操作后 `current_path`
  - `screenshot_ref` - 截图文件路径或哈希
  - `error` - 异常信息（如果发生）

### Requirement: 记录时机
系统 SHALL 在关键时机触发 Trace 记录。

#### Scenario: 状态转换时记录
- **WHEN** 全局状态机或遍历状态机状态转换
- **THEN** 创建新的 `TraceStep` 并记录当前状态

#### Scenario: EXECUTE 前后记录
- **WHEN** 遍历状态机在 `EXECUTE` 状态
- **THEN** 在执行操作前记录 `TraceStep`（包含决策信息）
- **AND** 在执行操作后更新 `TraceStep`（包含执行结果）

#### Scenario: BRANCH 决策时记录
- **WHEN** 遍历状态机在 `BRANCH` 状态
- **THEN** 记录分支决策（生成子节点、返回、异常处理）

#### Scenario: 异常发生时记录
- **WHEN** 遍历过程中发生异常
- **THEN** 除了标准 `TraceStep` 外附加异常上下文
- **AND** 记录异常类型、消息和堆栈

### Requirement: StateSnapshot 状态快照
系统 SHALL 定期创建状态快照用于快速恢复和跳转。

#### Scenario: 周期性快照
- **WHEN** 遍历进行中
- **THEN** 每隔 N 步（默认 10）创建 `StateSnapshot`
- **AND** 记录当前完整的遍历状态

#### Scenario: 快照内容
- **WHEN** 创建 `StateSnapshot`
- **THEN** 记录以下信息：
  - `snapshot_id` - 唯一标识
  - `timestamp` - 时间戳
  - `step_id` - 对应的步骤 ID
  - `full_state` - 完整的状态数据（节点栈、访问记录、失败节点等）

### Requirement: TraceSummary 统计摘要
系统 SHALL 在遍历完成后生成统计摘要。

#### Scenario: 摘要内容
- **WHEN** 生成 `TraceSummary`
- **THEN** 包含以下统计信息：
  - 总步骤数
  - 成功/失败/跳过操作数
  - 总耗时
  - 访问页面数
  - 异常统计
  - 截图数量

### Requirement: 存储与输出
系统 SHALL 将 Trace 数据持久化存储。

#### Scenario: 文件组织
- **WHEN** 遍历任务完成
- **THEN** 生成以下文件结构：
  - `trace.jsonl` - 步骤记录（JSON Lines 格式）
  - `snapshots.jsonl` - 状态快照（JSON Lines 格式）
  - `screenshots/` - 截图目录
  - `summary.json` - 统计摘要

#### Scenario: JSON Lines 格式
- **WHEN** 写入 `trace.jsonl`
- **THEN** 每行一个 JSON 对象（一个 `TraceStep`）
- **AND** 支持流式写入，避免内存占用

#### Scenario: 截图独立存储
- **WHEN** 记录 `TraceStep` 并包含截图
- **THEN** 截图保存到 `screenshots/` 目录
- **AND** `TraceStep.screenshot_ref` 记录文件路径或哈希

#### Scenario: 历史清理
- **WHEN** Trace 文件数量超过配置上限
- **THEN** 删除最旧的 Trace 文件夹
- **AND** 保留最近 N 次（默认 10）遍历记录

### Requirement: 回放功能
系统 SHALL 支持基于 Trace 文件的回放。

#### Scenario: 严格回放模式
- **WHEN** 使用严格回放模式
- **THEN** 按照 Trace 记录的节点和路径重新执行操作
- **AND** 验证结果一致性（用于回归测试）
- **AND** 比较截图哈希，要求 90% 以上匹配

#### Scenario: 决策回放模式
- **WHEN** 使用决策回放模式
- **THEN** 复用 Trace 中的决策序列（节点图）
- **AND** 忽略时序差异，适配界面微调
- **AND** 执行相同的遍历路径

#### Scenario: 模拟回放模式
- **WHEN** 使用模拟回放模式
- **THEN** 不连接真实设备
- **AND** 直接在 Trace 数据上进行路径分析
- **AND** 计算覆盖率和遍历完整性

#### Scenario: 回放引擎接口
- **WHEN** 实现回放引擎
- **THEN** 回放引擎与实时遍历引擎共享相同的状态机接口
- **AND** 通过注入 Trace 数据源替代视觉分析

### Requirement: Trace 与图模型关联
系统 SHALL 支持通过 Trace 重建运行时节点图。

#### Scenario: 重建节点图
- **WHEN** 分析 Trace 数据
- **THEN** 可以重建完整的运行时节点图
- **AND** 显示哪些分支被遍历、哪些未触及

#### Scenario: 动态匹配效果分析
- **WHEN** 分析 Trace 中的动态匹配记录
- **THEN** 可以评估 `dynamic_rules` 的匹配效果
- **AND** 帮助调优规则

### Requirement: Trace 配置
系统 SHALL 支持配置 Trace 行为。

#### Scenario: 启用/禁用 Trace
- **WHEN** 配置 `trace_enabled = false`
- **THEN** 系统不记录 Trace
- **AND** 遍历性能不受影响

#### Scenario: 配置存储路径
- **WHEN** 配置 `trace_output_path`
- **THEN** Trace 文件保存到指定路径

#### Scenario: 配置保留数量
- **WHEN** 配置 `trace_keep_count`
- **THEN** 系统保留指定数量的最新 Trace
