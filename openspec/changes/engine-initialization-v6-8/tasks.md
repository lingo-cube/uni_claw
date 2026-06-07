## 1. 数据模型扩展

- [x] 1.1 在 `src/graph/node.py` 中添加 EntryConfig 数据类
- [x] 1.2 在 EntryConfig.__post_init__ 中实现字段验证（wait_mode、trace_level、wait_timeout、wait_interval）
- [x] 1.3 在 TraversalPlan 中添加 entry_config 字段（Optional[EntryConfig]）
- [x] 1.4 更新 TraversalPlan._to_dict() 方法以序列化 entry_config
- [x] 1.5 更新 TraversalPlan._from_dict() 方法以反序列化 entry_config
- [x] 1.6 添加单元测试验证 EntryConfig 字段验证
- [x] 1.7 添加单元测试验证 EntryConfig 序列化/反序列化

## 2. 异常类型定义

- [x] 2.1 在 `src/exception/` 目录下创建 `initialization.py` 文件
- [x] 2.2 实现 InitializationError 基类（含 recoverable 属性）
- [x] 2.3 实现 ConfigurationError（不可恢复）
- [x] 2.4 实现 EntryPolicyError（可恢复，含 last_error 属性）
- [x] 2.5 实现 WaitConditionError（可恢复）
- [x] 2.6 实现 EntryError（策略执行错误）
- [x] 2.7 添加单元测试验证异常类型正确性

## 3. 计划验证实现

- [x] 3.1 在 GraphTraversalEngine 中实现 _validate_plan() 方法
- [x] 3.2 实现 root_node 存在性检查
- [x] 3.3 实现 root_node 类型检查（必须是 CONTAINER）
- [x] 3.4 实现 root_node 操作检查（必须是 no_action）
- [x] 3.5 在 initialize() 方法开始时调用 _validate_plan()
- [x] 3.6 添加单元测试：root_node 为 None 抛出 ConfigurationError
- [x] 3.7 添加单元测试：root_node 类型错误抛出 ConfigurationError
- [x] 3.8 添加单元测试：root_node 操作错误抛出 ConfigurationError

## 4. 入口策略框架实现

- [x] 4.1 实现 _build_strategy_chain() 方法（构建降级链）
- [x] 4.2 实现 _execute_entry_policy() 方法框架（含异常处理）
- [x] 4.3 实现 _execute_single_strategy() 方法（策略分发）
- [x] 4.4 实现 _record_entry_success() 方法
- [x] 4.5 实现 _record_entry_failure() 方法
- [x] 4.6 修改 initialize() 方法调用新的 _execute_entry_policy()

## 5. 入口策略具体实现

- [x] 5.1 实现 _execute_deeplink_strategy() 方法
- [x] 5.2 实现 _execute_cold_launch_strategy() 方法
- [x] 5.3 实现 _execute_bind_current_screen_strategy() 方法
- [x] 5.4 实现 _find_app_icon() 方法（含 EXTENSION POINT 注释）
- [x] 5.5 实现 _get_action_delay() 方法（支持 entry_config 和 meta）
- [x] 5.6 添加单元测试：deeplink 策略成功场景
- [x] 5.7 添加单元测试：cold_launch 策略成功场景
- [x] 5.8 添加单元测试：cold_launch 策略失败（图标未找到）
- [x] 5.9 添加单元测试：降级链（deeplink 失败 → cold_launch 成功）
- [x] 5.10 添加单元测试：EntryPolicyError 属性验证

## 6. 等待条件验证实现

- [x] 6.1 实现 _verify_entry_success() 方法（支持 entry_config 和 meta）
- [x] 6.2 实现 _verify_condition_once() 方法（快速模式）
- [x] 6.3 实现 _verify_condition_polling() 方法（轮询模式）
- [x] 6.4 实现无 wait_condition 时直接返回 True
- [x] 6.5 修改 initialize() 方法调用 _verify_entry_success()
- [x] 6.6 添加单元测试：快速模式成功场景
- [x] 6.7 添加单元测试：快速模式失败场景
- [x] 6.8 添加单元测试：轮询模式成功场景
- [x] 6.9 添加单元测试：轮询模式超时场景
- [x] 6.10 添加单元测试：无 wait_condition 直接通过

## 7. 根节点处理实现

- [x] 7.1 实现 _validate_and_push_root_node() 方法
- [x] 7.2 实现 _initialize_root_step() 方法（StepTracker 初始化）
- [x] 7.3 实现 _record_root_node_pushed() 方法
- [x] 7.4 修改 initialize() 方法调用 _validate_and_push_root_node()
- [x] 7.5 添加单元测试：正常根节点验证和压入
- [x] 7.6 添加单元测试：StepTracker 正确初始化

## 8. Trace 级别配置实现

- [x] 8.1 实现 _get_trace_level() 方法（支持 entry_config 和 meta）
- [x] 8.2 实现 _should_record_entry_attempt() 方法
- [x] 8.3 实现 _should_record_vision_call() 方法
- [x] 8.4 更新 _record_entry_success() 和 _record_entry_failure() 以检查 trace level
- [x] 8.5 添加单元测试：minimal 级别不记录入口尝试
- [x] 8.6 添加单元测试：standard 级别记录入口尝试
- [x] 8.7 添加单元测试：detailed 级别配置验证

## 9. initialize() 方法重构

- [x] 9.1 修改 initialize() 签名为抛出异常而非返回 bool
- [x] 9.2 移除 initialize() 中的 try-catch（让异常传播）
- [x] 9.3 保留异常时设置 global_state 到 ERROR 的逻辑
- [x] 9.4 更新文档字符串说明异常行为
- [x] 9.5 添加单元测试：验证异常正确传播

## 10. 仿真测试

- [x] 10.1 创建仿真测试：完整初始化流程（成功场景）
- [x] 10.2 创建仿真测试：deeplink 入口成功
- [x] 10.3 创建仿真测试：cold_launch 入口成功
- [x] 10.4 创建仿真测试：降级链场景
- [x] 10.5 创建仿真测试：轮询模式验证成功
- [x] 10.6 创建仿真测试：配置错误抛出异常

## 11. 集成与回归测试

- [x] 11.1 运行 state_machine 测试套件验证无破坏
- [x] 11.2 运行 traversal_engine 测试套件验证兼容性
- [x] 11.3 运行 simulation 测试套件
- [x] 11.4 运行全量回归测试
- [x] 11.5 验证 Trace 输出正确性

## 12. 文档更新

- [x] 12.1 更新 src/traversal/graph_engine.py 模块文档
- [x] 12.2 更新 src/graph/node.py EntryConfig 文档
- [x] 12.3 更新 docs/architecture/modules/state-machine-design.md（如有需要）
- [ ] 12.4 提交代码变更到 git
