# Proposal: State Machine Intelligence

## Why

当前状态机能流转但决策机械：前置条件失败一律靠外部循环解决，容器完成只会 `back`，弹窗和异常无智能策略。这种机械决策导致：
- 不必要的重试和 back 操作，降低遍历效率
- 无法利用已有的上下文信息（`current_path`、`level1_menus`、`page_tree`）做出精准决策
- 同级菜单遍历时每次都 back 退出，错失直接切换的机会

本变更引入**关系驱动的精准纠正**，利用已有视觉分析结果做出智能决策，提升遍历效率和稳定性。

## What Changes

### 核心变更

- **新增 `classify_relation` 纯函数**：判断当前页面与预期页面的关系（MATCH/NAVIGABLE/DEEPER/UNKNOWN）
- **新增 `FallbackAction.AUTO_ESCAPE`**（已存在于代码中）：支持同级菜单智能切换
- **重构 4 个状态处理 handler**：
  - `_handle_precondition_check`：智能纠正前置条件，最多 3 轮重试
  - `_handle_frame_complete_state`：auto_escape 同级切换，失败降级 back
  - `_handle_popup_state`：利用 `page.items` 查找安全按钮关闭弹窗
  - `_handle_error_state`：集成节点级 ErrorPolicy 处理
- **新增 `step()` 异常处理**：try-catch 包装，确保 `context.last_error` 正确设置

### 行为变更

- **前置条件纠正**：不再盲目 back，而是根据页面关系决定点击导航或 back
- **容器完成处理**：优先尝试同级菜单切换，而非直接 back
- **弹窗处理**：优先查找安全按钮（取消/关闭），找不到才 back
- **异常处理**：支持 retry/skip/backtrack/abort 策略，带重试计数

## Capabilities

### New Capabilities

- `state-machine-intelligence`: 状态机智能决策能力，包括关系判断、智能纠正、同级切换、弹窗处理、异常策略

### Modified Capabilities

- `traversal-fsm`: 状态机 handler 签名变更（`_handle_precondition_check` 新增 `vision` 参数）
- `traversal-context`: 新增 `last_error`、`consecutive_errors` 字段确认（已存在）

## Impact

### 代码影响

- **主要修改文件**：
  - `src/state_machine/traversal_fsm.py`：handler 重写、step() 异常处理
  - `src/graph/node.py`：FallbackAction.AUTO_ESCAPE（已存在）

- **新增文件**：
  - 无（classify_relation 作为纯函数直接在 traversal_fsm.py 中）

### API 影响

- `TraversalStateMachine.step()` 签名不变，但内部添加异常处理
- `_handle_precondition_check(stack, context, vision, action)` 新增 `vision` 参数

### 依赖影响

- **Vision 服务**：需要更频繁调用（precondition 纠正后、frame_complete 切换后）
- **TraversalRuntimeContext**：依赖 `last_error`、`consecutive_errors`、`failed_nodes` 字段（已存在）

### 测试影响

- 需新增仿真测试覆盖新增的智能决策路径
- 需回归测试确保现有行为不被破坏

### 性能影响

- Vision 调用次数增加：每轮 precondition 纠正 +1 次（最多 3 次），每次 frame_complete 尝试 +1 次（最多 2 次）
- 预计总体成本降低 10-20%（减少错误重试和无效 back 操作）
