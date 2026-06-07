# Tasks: State Machine Intelligence

实施任务清单。

## 1. 基础验证与准备

- [ ] 1.1 确认 `src/graph/node.py` 中 `FallbackAction.AUTO_ESCAPE` 已存在
- [ ] 1.2 确认 `src/trace/context.py` 中 `TraversalRuntimeContext` 包含 `last_error`、`consecutive_errors`、`failed_nodes` 字段
- [ ] 1.3 备份 `src/state_machine/traversal_fsm.py` 当前版本

## 2. 实现 `classify_relation` 纯函数

- [ ] 2.1 在 `traversal_fsm.py` 中添加 `classify_relation` 函数
- [ ] 2.2 实现 MATCH 判断逻辑（当前路径末位等于预期页面）
- [ ] 2.3 实现 DEEPER 判断逻辑（预期页面在当前路径中但非末位）
- [ ] 2.4 实现 NAVIGABLE 判断逻辑（预期页面在菜单中）
- [ ] 2.5 返回 UNKNOWN 作为默认情况
- [ ] 2.6 添加函数文档注释（包括 "回退过头" 场景说明）

## 3. 添加 `step()` 异常处理包装

- [ ] 3.1 在 `step()` 方法中添加 try-catch 包装
- [ ] 3.2 实现异常捕获时设置 `context.last_error`
- [ ] 3.3 实现异常捕获时增加 `context.consecutive_errors`
- [ ] 3.4 实现异常捕获时设置 `next_state = ERROR_HANDLING`
- [ ] 3.5 确保 metadata 中记录 error_type

## 4. 重写 `_handle_precondition_check`

- [ ] 4.1 更新 handler 签名，添加 `vision` 参数
- [ ] 4.2 实现最多 3 轮重试的循环逻辑
- [ ] 4.3 每轮开始时调用 `vision.analyze_screenshot()`
- [ ] 4.4 满足条件时记录 `ai_call` metrics 并返回 EXECUTE
- [ ] 4.5 调用 `classify_relation` 判断关系
- [ ] 4.6 实现 NAVIGABLE 关系的点击逻辑
- [ ] 4.7 实现 DEEPER/UNKNOWN 关系的 back 逻辑
- [ ] 4.8 **新增**：纠正后立即调用 vision 验证
- [ ] 4.9 **新增**：纠正成功时记录 correction metrics 并提前退出
- [ ] 4.10 重试耗尽时记录错误 metrics 并返回 ERROR_HANDLING
- [ ] 4.11 实现 `wait_ms` 延迟支持（使用 `context.wait_after_action_ms`）

## 5. 重写 `_handle_frame_complete_state`

- [ ] 5.1 读取节点的 `exit_condition.fallback`，默认为 AUTO_ESCAPE
- [ ] 5.2 实现 BACK 逻辑（弹栈，返回 NODE_SELECT）
- [ ] 5.3 实现 ABORT 逻辑（设置 TERMINATED，返回 BRANCH）
- [ ] 5.4 实现 SKIP 逻辑（直接弹栈）
- [ ] 5.5 实现 AUTO_ESCAPE：从 context 获取 current_page_analysis
- [ ] 5.6 实现 AUTO_ESCAPE：收集未访问的同级菜单
- [ ] 5.7 实现 AUTO_ESCAPE：无未访问菜单时降级 back
- [ ] 5.8 实现 AUTO_ESCAPE：点击目标菜单
- [ ] 5.9 **新增**：点击后强制调用 vision 获取最新页面
- [ ] 5.10 **新增**：验证页面路径变化，成功则不弹栈
- [ ] 5.11 **新增**：切换失败重试 1 次
- [ ] 5.12 **新增**：重试失败后降级 back
- [ ] 5.13 记录相应的 execution 和 ai_call metrics

## 6. 重写 `_handle_popup_state`

- [ ] 6.1 从 context 获取 current_page_analysis
- [ ] 6.2 定义安全按钮关键词列表
- [ ] 6.3 遍历 page.items 查找安全按钮
- [ ] 6.4 找到按钮时点击并返回 RESULT_VERIFY
- [ ] 6.5 找不到按钮时执行 back
- [ ] 6.6 记录 execution metrics
- [ ] 6.7 **可选**：实现按钮类型验证（`item.type == 'button'`）

## 7. 重写 `_handle_error_state`

- [ ] 7.1 从 context 获取 last_error
- [ ] 7.2 实现 Layer 1：节点 error_policy 处理
- [ ] 7.3 实现 retry 逻辑（检查 retry_count < max_retries）
- [ ] 7.4 实现 retry 时更新 context.failed_nodes.retry_count
- [ ] 7.5 实现 skip 逻辑
- [ ] 7.6 实现 backtrack 逻辑（弹栈）
- [ ] 7.7 实现 abort 逻辑（设置 TERMINATED）
- [ ] 7.8 实现 fallback 逻辑
- [ ] 7.9 实现 Layer 2：ExceptionHandlingChain（占位）
- [ ] 7.10 实现 Layer 3：AI 异常处理（占位）
- [ ] 7.11 记录 error metrics
- [ ] 7.12 更新 context.consecutive_errors 和 context.failed_nodes
- [ ] 7.13 **可选**：添加 failed_nodes 辅助方法

## 8. 更新其他 handler 签名（如需要）

- [ ] 8.1 确认 `_handle_node_select` 签名无需变更
- [ ] 8.2 确认 `_handle_branch` 签名无需变更
- [ ] 8.3 确认 `_handle_execute` 签名已包含 vision 参数
- [ ] 8.4 确认 `_handle_result_verify` 签名已包含 vision 参数

## 9. Vision 延迟配置支持

- [ ] 9.1 在 `TraversalRuntimeContext` 中添加 `wait_after_action_ms` 字段（如不存在）
- [ ] 9.2 在需要的地方读取并应用延迟
- [ ] 9.3 仿真环境测试时设为 0
- [ ] 9.4 生产环境建议使用默认值 100ms

## 10. 单元测试

- [ ] 10.1 添加 `classify_relation` 单元测试（5 个场景）
- [ ] 10.2 添加 precondition handler 单元测试（NAVIGABLE 场景）
- [ ] 10.3 添加 precondition handler 单元测试（DEEPER 场景）
- [ ] 10.4 添加 precondition handler 单元测试（重试耗尽场景）
- [ ] 10.5 添加 frame_complete handler 单元测试（auto_escape 成功场景）
- [ ] 10.6 添加 frame_complete handler 单元测试（auto_escape 降级 back 场景）
- [ ] 10.7 添加 popup handler 单元测试（找到按钮场景）
- [ ] 10.8 添加 popup handler 单元测试（找不到按钮场景）
- [ ] 10.9 添加 error handler 单元测试（retry 场景）
- [ ] 10.10 添加 error handler 单元测试（skip/backtrack/abort 场景）
- [ ] 10.11 添加 step() 异常处理单元测试

## 11. 仿真测试

- [ ] 11.1 创建 precondition 纠正成功仿真测试
- [ ] 11.2 创建 precondition 纠正失败仿真测试
- [ ] 11.3 创建 auto_escape 同级切换仿真测试
- [ ] 11.4 创建 auto_escape 降级 back 仿真测试
- [ ] 11.5 创建弹窗关闭仿真测试
- [ ] 11.6 创建 error policy retry 仿真测试
- [ ] 11.7 创建完整遍历流程仿真测试

## 12. 全量回归测试

- [ ] 12.1 运行现有 state_machine 测试套件
- [ ] 12.2 运行 traversal_engine 测试套件
- [ ] 12.3 运行 simulation 测试套件
- [ ] 12.4 验证 trace metrics 正确记录
- [ ] 12.5 验证无性能退化

## 13. 文档更新

- [ ] 13.1 更新 `src/state_machine/traversal_fsm.py` 模块文档
- [ ] 13.2 更新 `docs/architecture/modules/state-machine-design.md`（如存在）
- [ ] 13.3 更新 PRD V6.7 修订记录（已完成）

## 14. 提交与发布

- [ ] 14.1 代码审查
- [ ] 14.2 提交变更到 git
- [ ] 14.3 创建 Pull Request
- [ ] 14.4 灰度发布准备
