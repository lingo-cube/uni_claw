## MODIFIED Requirements

### Requirement: 遍历上下文数据结构

系统 SHALL 提供 `TraversalContext` 数据类，封装传递给 AI 的只读运行时状态。

#### Scenario: 上下文包含必要字段
- **WHEN** 查看 `TraversalContext` 定义
- **THEN** 包含以下字段：
  - `node_stack: List[str]` - 逻辑任务栈
  - `current_path: List[str]` - 界面位置（真相源）
  - `visited_pages: Set[str]` - 已访问页面集合
  - `failed_nodes: Dict[str, ErrorRecord]` - 失败节点记录
  - `action_history: List[ActionRecord]` - 最近 5 步操作历史
  - `inference_history: List[ContainerInference]` - 最近 3 次容器推断历史
  - `goal_attempts: Dict[str, int]` - 目标尝试次数统计
  - `page_cache: Dict[str, PageCacheInfo]` - 页面缓存（V6 新增）
  - `max_depth: int` - 最大遍历深度（V6 新增）
  - `step_count: int` - 当前步数（V6 新增）
  - `global_state: GlobalState` - 全局状态（V6 新增）
  - `visited_nodes: Set[str]` - 已访问节点集合（V6 新增）

## ADDED Requirements

### Requirement: 页面缓存管理

系统 SHALL 支持页面信息缓存以优化性能。

#### Scenario: 更新页面缓存
- **WHEN** 获得新的 PageAnalysis
- **THEN** 系统 SHALL 更新 page_cache

#### Scenario: 缓存键生成
- **WHEN** 更新缓存
- **THEN** 系统 SHALL 使用路径字符串作为缓存键

#### Scenario: 缓存信息
- **WHEN** 存储 PageCacheInfo
- **THEN** 系统 SHALL 包含：
  - items: 页面元素列表
  - timestamp: 缓存时间戳

#### Scenario: 从缓存恢复
- **WHEN** 返回到已缓存的路径
- **THEN** 系统 SHALL 可从 cache 恢复页面信息

### Requirement: 深度限制管理

系统 SHALL 支持深度限制以防止无限递归。

#### Scenario: 设置最大深度
- **WHEN** 初始化 TraversalContext
- **THEN** 系统 SHALL 设置 max_depth

#### Scenario: 检查当前深度
- **WHEN** 需要检查当前深度
- **THEN** 系统 SHALL 可通过 node_stack 长度计算

### Requirement: 步数统计

系统 SHALL 统计遍历执行的步数。

#### Scenario: 增加步数
- **WHEN** 每次状态转换
- **THEN** 系统 SHALL 增加 step_count

#### Scenario: 获取步数
- **WHEN** 访问 step_count
- **THEN** 系统 SHALL 返回当前总步数

### Requirement: 全局状态管理

系统 SHALL 维护全局遍历状态。

#### Scenario: 初始状态
- **WHEN** 初始化 TraversalContext
- **THEN** global_state SHALL 为 IDLE

#### Scenario: 更新状态
- **WHEN** 遍历开始
- **THEN** global_state SHALL 更新为 TRAVERSING

#### Scenario: 完成状态
- **WHEN** 遍历完成
- **THEN** global_state SHALL 更新为 COMPLETED 或 TERMINATED

### Requirement: 访问节点记录

系统 SHALL 记录所有已访问的节点。

#### Scenario: 记录访问
- **WHEN** 节点被成功处理
- **THEN** 系统 SHALL 将节点 ID 添加到 visited_nodes

#### Scenario: 检查访问
- **WHEN** 需要检查节点是否已访问
- **THEN** 系统 SHALL 可查询 visited_nodes

#### Scenario: 访问节点导出
- **WHEN** 导出遍历结果
- **THEN** 系统 SHALL 包含 visited_nodes 列表
