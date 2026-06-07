## ADDED Requirements

### Requirement: SimulationRunner 定义

系统 SHALL 提供 SimulationRunner 类用于离线遍历测试。

#### Scenario: 创建仿真模拟器
- **WHEN** 创建 SimulationRunner 实例
- **THEN** 系统 SHALL 接受以下参数：
  - virtual_pages: Dict[str, PageAnalysis]（必需）
  - plan: TraversalPlan（必需）

#### Scenario: 初始化组件
- **WHEN** 创建 SimulationRunner
- **THEN** 系统 SHALL 初始化：
  - MockVisionService
  - MockActionExecutor
  - InMemoryTracer
  - GraphTraversalEngine

### Requirement: 仿真执行

系统 SHALL 提供 run() 方法执行仿真遍历。

#### Scenario: 执行仿真
- **WHEN** 调用 run()
- **THEN** 系统 SHALL：
  1. 创建 Mock 组件
  2. 创建 GraphTraversalEngine
  3. 调用 engine.run()
  4. 返回 SimulationResult

#### Scenario: 返回结果
- **WHEN** 仿真完成
- **THEN** run() SHALL 返回包含以下信息的 SimulationResult：
  - engine_result: TraversalResult
  - trace: Trace 数据
  - executed_actions: 操作历史列表

### Requirement: 虚拟页面匹配

系统 SHALL 根据当前路径返回对应的虚拟页面。

#### Scenario: 路径匹配
- **WHEN** 当前路径为 ["设置", "显示"]
- **THEN** 系统 SHALL 返回 "显示" 对应的虚拟页面

#### Scenario: 根路径匹配
- **WHEN** 当前路径为 ["设置"]
- **THEN** 系统 SHALL 返回 "设置" 对应的虚拟页面

#### Scenario: 未匹配页面
- **WHEN** 当前路径无对应虚拟页面
- **THEN** 系统 SHALL 返回空 PageAnalysis

### Requirement: 操作历史记录

系统 SHALL 记录所有执行的操作。

#### Scenario: 记录点击
- **WHEN** 执行点击操作
- **THEN** 系统 SHALL 记录：
  - action: "tap"
  - x, y: 坐标
  - timestamp: 时间戳

#### Scenario: 记录滑动
- **WHEN** 执行滑动操作
- **THEN** 系统 SHALL 记录：
  - action: "swipe"
  - start: 起始坐标
  - end: 结束坐标
  - timestamp: 时间戳

#### Scenario: 记录返回
- **WHEN** 执行返回操作
- **THEN** 系统 SHALL 记录：
  - action: "back"
  - timestamp: 时间戳

#### Scenario: 获取历史
- **WHEN** 调用 get_history()
- **THEN** 系统 SHALL 返回所有操作历史的副本

### Requirement: 可视化输出

系统 SHALL 支持多种可视化输出格式。

#### Scenario: ASCII 树输出
- **WHEN** 调用 render_tree()
- **THEN** 系统 SHALL 返回缩进格式的遍历树

#### Scenario: 树结构格式
- **WHEN** 渲染树结构
- **THEN** 系统 SHALL 使用以下格式：
  - 节点名称 [类型] ✓/✗
  - 子节点缩进显示
  - 使用 │   └── ├── 等字符

#### Scenario: Mermaid 图输出
- **WHEN** 调用 render_mermaid()
- **THEN** 系统 SHALL 返回 Mermaid 状态图

#### Scenario: Mermaid 格式
- **WHEN** 渲染 Mermaid 图
- **THEN** 系统 SHALL 使用 stateDiagram-v2 格式

#### Scenario: JSONL 导出
- **WHEN** 调用 export_trace("jsonl")
- **THEN** 系统 SHALL 返回 JSONL 格式的 Trace

#### Scenario: HTML 导出
- **WHEN** 调用 export_trace("html")
- **THEN** 系统 SHALL 返回 HTML 格式的报告

### Requirement: Trace 记录

系统 SHALL 记录仿真过程中的所有状态转换。

#### Scenario: 记录转换
- **WHEN** 状态转换发生
- **THEN** 系统 SHALL 记录 TraceStep

#### Scenario: TraceStep 内容
- **WHEN** 记录 TraceStep
- **THEN** 系统 SHALL 包含：
  - step_number: 步数
  - timestamp: 时间戳
  - from_state: 源状态
  - to_state: 目标状态
  - node_id: 节点 ID
  - action: 执行的动作
  - screen_info: 屏幕信息
  - metadata: 元数据

#### Scenario: 更新访问树
- **WHEN** 记录包含 node_id 的转换
- **THEN** 系统 SHALL 更新 visited_tree

### Requirement: 计划调试

系统 SHALL 提供计划调试工具。

#### Scenario: PlanDebugger 定义
- **WHEN** 提供 PlanDebugger 类
- **THEN** 系统 SHALL 支持以下方法：
  - remove_rule(): 删除动态规则
  - set_target(): 设置目标搜索
  - reset_visited(): 清空访问记录

#### Scenario: 删除规则测试
- **WHEN** 删除某条动态规则
- **THEN** 系统 SHALL 返回修改后的 plan

#### Scenario: 设置目标测试
- **WHEN** 动态设置目标搜索
- **THEN** 系统 SHALL 修改 completion_policy

#### Scenario: 重置访问记录
- **WHEN** 清空访问记录
- **THEN** 系统 SHALL 清空 visited_nodes 和 node_stack

### Requirement: 仿真验证

系统 SHALL 支持验证仿真结果。

#### Scenario: 验证操作序列
- **WHEN** 比较两次仿真结果
- **THEN** 系统 SHALL 比较操作历史是否一致

#### Scenario: 验证状态转换
- **WHEN** 比较两次仿真结果
- **THEN** 系统 SHALL 比较状态转换序列是否一致

#### Scenario: 验证访问树
- **WHEN** 比较两次仿真结果
- **THEN** 系统 SHALL 比较访问树结构是否一致

### Requirement: 仿真性能

系统 SHALL 确保仿真能够快速执行。

#### Scenario: 执行时间
- **WHEN** 运行仿真
- **THEN** 系统 SHALL 在 1 秒内完成简单场景

#### Scenario: 内存使用
- **WHEN** 运行仿真
- **THEN** 系统 SHALL 内存使用保持在合理范围

### Requirement: 错误处理

系统 SHALL 正确处理仿真中的错误。

#### Scenario: 虚拟页面缺失
- **WHEN** 路径无对应虚拟页面
- **THEN** 系统 SHALL 返回空页面并记录警告

#### Scenario: 操作执行失败
- **WHEN** Mock 操作执行失败
- **THEN** 系统 SHALL 记录失败并继续

#### Scenario: 状态转移异常
- **WHEN** 状态转移失败
- **THEN** 系统 SHALL 记录异常并返回失败结果
