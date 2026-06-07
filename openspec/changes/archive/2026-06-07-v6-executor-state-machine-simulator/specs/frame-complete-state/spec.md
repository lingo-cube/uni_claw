## ADDED Requirements

### Requirement: FRAME_COMPLETE 状态

系统 SHALL 在 TraversalStateMachine 中添加 FRAME_COMPLETE 状态。

#### Scenario: 进入帧完成状态
- **WHEN** 当前容器的所有子节点已处理完成
- **THEN** 系统 SHALL 转移到 FRAME_COMPLETE 状态

#### Scenario: 执行帧完成处理
- **WHEN** 处于 FRAME_COMPLETE 状态
- **THEN** 系统 SHALL 调用 handle_frame_complete() 方法

### Requirement: BACK 回退动作

系统 SHALL 在 FRAME_COMPLETE 状态支持 BACK 回退动作。

#### Scenario: 执行 Back 并弹栈
- **WHEN** fallback 为 BACK
- **THEN** 系统 SHALL：
  1. 执行 Back 键操作
  2. 弹出当前帧
  3. 转移到 NODE_SELECT 状态

### Requirement: AUTO_ESCAPE 回退动作

系统 SHALL 在 FRAME_COMPLETE 状态支持 AUTO_ESCAPE 回退动作。

#### Scenario: 存在未访问同级菜单
- **WHEN** fallback 为 AUTO_ESCAPE 且存在未访问的同级菜单
- **THEN** 系统 SHALL：
  1. 点击未访问的同级菜单
  2. 不弹栈
  3. 重新生成子节点
  4. 转移到 NODE_SELECT 状态

#### Scenario: 无未访问同级菜单
- **WHEN** fallback 为 AUTO_ESCAPE 且不存在未访问的同级菜单
- **THEN** 系统 SHALL：
  1. 执行 Back 键操作
  2. 弹出当前帧
  3. 转移到 NODE_SELECT 状态

### Requirement: SKIP 回退动作

系统 SHALL 在 FRAME_COMPLETE 状态支持 SKIP 回退动作。

#### Scenario: 跳过帧
- **WHEN** fallback 为 SKIP
- **THEN** 系统 SHALL：
  1. 直接弹出当前帧
  2. 不执行任何操作
  3. 转移到 NODE_SELECT 状态

### Requirement: ABORT 回退动作

系统 SHALL 在 FRAME_COMPLETE 状态支持 ABORT 回退动作。

#### Scenario: 终止遍历
- **WHEN** fallback 为 ABORT
- **THEN** 系统 SHALL：
  1. 设置全局状态为 TERMINATED
  2. 转移到 COMPLETED 状态

### Requirement: 默认帧完成行为

系统 SHALL 在未设置 exit_condition 时使用默认行为。

#### Scenario: 默认 Back 动作
- **WHEN** 当前节点未设置 exit_condition
- **THEN** 系统 SHALL：
  1. 执行 Back 键操作
  2. 弹出当前帧
  3. 转移到 NODE_SELECT 状态

### Requirement: 帧完成失败处理

系统 SHALL 在帧完成操作失败时转移状态。

#### Scenario: 返回操作失败
- **WHEN** 执行 Back 或其他退出动作失败
- **THEN** 系统 SHALL 转移到 ERROR_HANDLING 状态

### Requirement: 状态转移验证

系统 SHALL 确保 FRAME_COMPLETE 状态仅从合法状态转移。

#### Scenario: 从 BRANCH 转移
- **WHEN** BRANCH 状态判定当前帧完成
- **THEN** 系统 SHALL 转移到 FRAME_COMPLETE 状态

#### Scenario: 非法转移
- **WHEN** 尝试从除 BRANCH 外的状态转移到 FRAME_COMPLETE
- **THEN** 系统 SHALL 拒绝该转移
