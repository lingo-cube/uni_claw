## ADDED Requirements

### Requirement: SimulationRunner 集成 V6.3 Trace 系统
SimulationRunner SHALL 使用 `TraceRecorder` + `MemoryStorage` 替代旧版 `InMemoryTracer`，并将 `TraceRecorder` 注入 `GraphTraversalEngine`。

#### Scenario: 引擎创建时传入 TraceRecorder
- **WHEN** `SimulationRunner` 初始化
- **THEN** 创建 `MemoryStorage` 和 `TraceRecorder` 实例
- **AND** `GraphTraversalEngine` 的 `trace_recorder` 参数为创建的 `TraceRecorder`

#### Scenario: 仿真结果从 MemoryStorage 提取
- **WHEN** 仿真运行完成
- **THEN** `_build_simulation_result()` 从 `MemoryStorage.read(trace_id)` 读取节点
- **AND** 使用 `TraceAnalyzer` 提取分析数据

### Requirement: SimulationResult 数据源为 TraceAnalyzer
`SimulationResult` 的统计数据 SHALL 来自 `TraceAnalyzer` 提取方法，而非直接访问 mock 内部状态。

#### Scenario: trace 字段包含 action_sequence
- **WHEN** 仿真运行完成
- **THEN** `result.trace` 等于 `TraceAnalyzer.extract_action_sequence()` 的结果

#### Scenario: statistics 字段包含分析数据
- **WHEN** 仿真运行完成
- **THEN** `result.statistics` 包含 `time`、`errors`、`coverage` 三个键
- **AND** 每个键的值来自 `TraceAnalyzer` 对应提取方法

### Requirement: 删除 InMemoryTracer 引用
系统 SHALL 从 `SimulationRunner` 中删除所有 `InMemoryTracer` 的导入和使用。

#### Scenario: InMemoryTracer 不再被引用
- **WHEN** 查看 `SimulationRunner` 类
- **THEN** 不存在 `self.tracer` 属性
- **AND** 不存在 `from .visualizer import InMemoryTracer` 导入语句
- **AND** `InMemoryTracer` 类和 `visualizer.py` 文件本身保留不删除

### Requirement: 删除手写 DFS fallback
系统 SHALL 删除 `SimulationRunner` 中的手写 DFS 兜底逻辑，不再维护两套遍历代码路径。

#### Scenario: fallback 方法不存在
- **WHEN** 查看 `SimulationRunner` 类
- **THEN** 不存在 `_execute_fallback_simulation` 方法
- **AND** 不存在 `_interact_with_next_element` 方法
- **AND** 不存在 `_execute_element_action` 方法
- **AND** 不存在 `_go_back` 方法

#### Scenario: run() 无 fallback 分支
- **WHEN** 查看 `SimulationRunner.run()` 方法
- **THEN** 不存在 `if self.engine: ... else: ...` 条件分支
- **AND** 直接调用 `self.engine.run()`

### Requirement: 删除冗余运行时状态
SimulationRunner SHALL 删除引擎 `TraversalRuntimeContext` 已管理的冗余状态字段。

#### Scenario: 冗余字段不存在
- **WHEN** 查看 `SimulationRunner` 实例
- **THEN** 不存在 `current_path` 属性
- **AND** 不存在 `visited_pages` 属性
- **AND** 不存在 `visited_elements` 属性
- **AND** 不存在 `current_element_index` 属性
- **AND** 不存在 `step_count` 属性
