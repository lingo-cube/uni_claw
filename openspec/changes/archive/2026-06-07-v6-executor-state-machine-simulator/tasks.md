## 1. 图模型补全

### 1.1 新增枚举类型

- [x] 1.1.1 在 `src/graph/node.py` 中添加 ExitConditionType 枚举
- [x] 1.1.2 在 `src/graph/node.py` 中添加 FallbackAction 枚举
- [x] 1.1.3 在 `src/graph/node.py` 中添加 CompletionPolicyType 枚举
- [x] 1.1.4 在 `src/graph/node.py` 中添加 TargetFoundAction 枚举
- [x] 1.1.5 在 `src/graph/node.py` 中添加 MatchMode 枚举
- [x] 1.1.6 在 `src/graph/node.py` 中添加 EntryStrategy 枚举
- [x] 1.1.7 在 `src/graph/node.py` 中添加 TraversalMode 枚举

### 1.2 新增数据类

- [x] 1.2.1 在 `src/graph/node.py` 中添加 ExitCondition 数据类
- [x] 1.2.2 在 `src/graph/node.py` 中添加 CompletionPolicy 数据类
- [x] 1.2.3 在 `src/graph/node.py` 中添加 EntryPolicy 数据类
- [x] 1.2.4 在 `src/graph/node.py` 中添加 IntentSlots 数据类

### 1.3 创建 TraversalPlan

- [x] 1.3.1 创建 `src/graph/plan.py` 文件
- [x] 1.3.2 在 `plan.py` 中实现 TraversalPlan 数据类
- [x] 1.3.3 实现 TraversalPlan.to_json() 方法
- [x] 1.3.4 实现 TraversalPlan.from_json() 类方法
- [x] 1.3.5 在 `src/graph/__init__.py` 中导出新模型

### 1.4 扩展现有模型

- [x] 1.4.1 在 TraversalNode 中添加 exit_condition 字段
- [x] 1.4.2 在 ErrorPolicy 中添加 BACKTRACK 动作值支持

### 1.5 图模型单元测试

- [x] 1.5.1 创建 `tests/v6/test_graph_models.py`
- [x] 1.5.2 测试所有新枚举类型
- [x] 1.5.3 测试所有新数据类
- [x] 1.5.4 测试 TraversalPlan 序列化/反序列化
- [x] 1.5.5 测试 TraversalNode 扩展

## 2. 状态机扩展

### 2.1 添加新状态

- [x] 2.1.1 在 TraversalState 枚举中添加 FRAME_COMPLETE 状态
- [x] 2.1.2 在 TraversalState 枚举中添加 ERROR_HANDLING 状态
- [x] 2.1.3 在 TraversalState 枚举中添加 POPUP_HANDLING 状态

### 2.2 更新状态转移

- [x] 2.2.1 在 VALID_TRANSITIONS 中添加 FRAME_COMPLETE 相关转移
- [x] 2.2.2 在 VALID_TRANSITIONS 中添加 ERROR_HANDLING 相关转移
- [x] 2.2.3 在 VALID_TRANSITIONS 中添加 POPUP_HANDLING 相关转移

### 2.3 实现状态处理方法

- [x] 2.3.1 实现 handle_frame_complete() 方法
- [x] 2.3.2 实现 handle_error() 方法
- [x] 2.3.3 实现 handle_popup() 方法

### 2.4 支持回退动作

- [x] 2.4.1 实现 BACK 回退动作
- [x] 2.4.2 实现 AUTO_ESCAPE 回退动作
- [x] 2.4.3 实现 SKIP 回退动作
- [x] 2.4.4 实现 ABORT 回退动作

### 2.5 三层异常处理

- [x] 2.5.1 实现第一层：节点 error_policy 处理
- [x] 2.5.2 实现第二层：ExceptionHandlingChain 处理
- [x] 2.5.3 实现第三层：AI 异常处理接口（预留）

### 2.6 弹窗处理逻辑

- [x] 2.6.1 实现查找取消按钮逻辑
- [x] 2.6.2 实现 Back 操作尝试
- [x] 2.6.3 实现 AI 决策调用
- [x] 2.6.4 实现弹窗验证逻辑

### 2.7 状态机单元测试

- [x] 2.7.1 创建 `tests/v6/test_state_machine.py`
- [x] 2.7.2 测试 FRAME_COMPLETE 状态转移
- [x] 2.7.3 测试 ERROR_HANDLING 状态转移
- [x] 2.7.4 测试 POPUP_HANDLING 状态转移
- [x] 2.7.5 测试所有回退动作
- [x] 2.7.6 测试三层异常处理

## 3. 执行器实现

### 3.1 创建 GraphTraversalEngine

- [x] 3.1.1 创建 `src/traversal/graph_engine.py` 文件
- [x] 3.1.2 实现 GraphTraversalEngine.__init__()
- [x] 3.1.3 实现组件初始化逻辑

### 3.2 实现初始化流程

- [x] 3.2.1 实现 initialize() 方法
- [x] 3.2.2 实现入口策略执行（COLD_LAUNCH）
- [x] 3.2.3 实现入口策略执行（DIRECT_DEEPLINK）
- [x] 3.2.4 实现入口策略执行（BIND_CURRENT_SCREEN）
- [x] 3.2.5 实现等待条件验证
- [x] 3.2.6 实现根节点压栈
- [x] 3.2.7 实现 Trace 初始化

### 3.3 实现主循环

- [x] 3.3.1 实现 run() 方法框架
- [x] 3.3.2 实现栈为空检查
- [x] 3.3.3 实现 CompletionPolicy 检查
- [x] 3.3.4 实现状态机步进调用
- [x] 3.3.5 实现 Trace 记录调用
- [x] 3.3.6 实现 TraversalResult 返回

### 3.4 深度限制与缓存

- [x] 3.4.1 实现 generate_children() 深度限制
- [x] 3.4.2 实现 update_page_cache()
- [x] 3.4.3 实现 restore_from_cache()

### 3.5 模板注册表集成

- [x] 3.5.1 实现模板注册表加载
- [x] 3.5.2 实现 DynamicMatcher 创建

### 3.6 完成策略检查

- [x] 3.6.1 实现 TARGET_FOUND 策略检查
- [x] 3.6.2 实现 TIMEOUT 策略检查
- [x] 3.6.3 实现 MAX_STEPS 策略检查

### 3.7 执行器单元测试

- [x] 3.7.1 创建 `tests/v6/test_executor.py`
- [x] 3.7.2 测试初始化流程
- [x] 3.7.3 测试主循环
- [x] 3.7.4 测试深度限制
- [x] 3.7.5 测试缓存管理
- [x] 3.7.6 测试完成策略

## 4. 仿真模拟器

### 4.1 创建仿真目录结构

- [x] 4.1.1 创建 `src/simulation/` 目录
- [x] 4.1.2 创建 `src/simulation/__init__.py`

### 4.2 实现 MockVisionService

- [x] 4.2.1 创建 `src/simulation/mock_vision.py`
- [x] 4.2.2 实现 MockVisionService 类
- [x] 4.2.3 实现 analyze_screenshot() 方法
- [x] 4.2.4 实现当前路径获取逻辑

### 4.3 实现 MockActionExecutor

- [x] 4.3.1 创建 `src/simulation/mock_action.py`
- [x] 4.3.2 实现 MockActionExecutor 类
- [x] 4.3.3 实现 tap() 方法
- [x] 4.3.4 实现 swipe() 方法
- [x] 4.3.5 实现 press_back() 方法
- [x] 4.3.6 实现 get_history() 方法

### 4.4 实现 InMemoryTracer

- [x] 4.4.1 创建 `src/simulation/visualizer.py`
- [x] 4.4.2 实现 InMemoryTracer 类
- [x] 4.4.3 实现 record_transition() 方法
- [x] 4.4.4 实现 render_tree() 方法
- [x] 4.4.5 实现 render_mermaid() 方法
- [x] 4.4.6 实现 render_html() 方法
- [x] 4.4.7 实现 export_trace() 方法

### 4.5 实现 SimulationRunner

- [x] 4.5.1 创建 `src/simulation/runner.py`
- [x] 4.5.2 实现 SimulationRunner 类
- [x] 4.5.3 实现 run() 方法
- [x] 4.5.4 实现可视化接口

### 4.6 实现 PlanDebugger

- [x] 4.6.1 实现 PlanDebugger 类
- [x] 4.6.2 实现 remove_rule() 方法
- [x] 4.6.3 实现 set_target() 方法
- [x] 4.6.4 实现 reset_visited() 方法

### 4.7 仿真模拟器测试

- [x] 4.7.1 创建 `tests/v6/test_simulation.py`
- [x] 4.7.2 测试 MockVisionService
- [x] 4.7.3 测试 MockActionExecutor
- [x] 4.7.4 测试 InMemoryTracer
- [x] 4.7.5 测试 SimulationRunner

## 5. 端到端示例

### 5.1 创建示例数据

- [x] 5.1.1 创建 `tests/v6/fixtures/plan_all.json`
- [x] 5.1.2 创建 `tests/v6/fixtures/pages_all.json`
- [x] 5.1.3 创建 `tests/v6/fixtures/plan_find_version.json`
- [x] 5.1.4 创建 `tests/v6/fixtures/pages_find.json`
- [x] 5.1.5 创建 `tests/v6/fixtures/plan_static.json`

### 5.2 实现示例测试

- [x] 5.2.1 创建 `tests/v6/test_examples.py`
- [x] 5.2.2 实现全菜单遍历测试（E2E-1）
- [x] 5.2.3 实现目标搜索测试（E2E-2）
- [x] 5.2.4 实现静态路径测试（E2E-3）

### 5.3 可视化测试

- [x] 5.3.1 测试 render_tree 输出（VIS-1）
- [x] 5.3.2 测试 render_mermaid 输出（VIS-2）
- [x] 5.3.3 测试 export_trace(jsonl)（VIS-3）
- [x] 5.3.4 测试 export_trace(html)（VIS-4）

## 6. 集成与验证

### 6.1 上下文扩展

- [x] 6.1.1 扩展 TraversalContext 添加新字段
- [x] 6.1.2 实现 page_cache 管理
- [x] 6.1.3 实现 max_depth 管理
- [x] 6.1.4 实现 step_count 统计
- [x] 6.1.5 实现 global_state 管理
- [x] 6.1.6 实现 visited_nodes 记录

### 6.2 Trace 扩展

- [x] 6.2.1 扩展 TraceRecorder 支持新格式
- [x] 6.2.2 实现状态转换记录

### 6.3 集成测试

- [x] 6.3.1 运行所有单元测试
- [x] 6.3.2 运行所有端到端测试
- [x] 6.3.3 验证测试覆盖率 >= 80%

### 6.4 回归测试

- [x] 6.4.1 运行现有测试套件
- [x] 6.4.2 确保无破坏性变更

### 6.5 文档更新

- [x] 6.5.1 更新 CLAUDE.md
- [x] 6.5.2 更新 README.md
- [x] 6.5.3 创建 ARCHITECTURE_V6.md

### 6.6 性能验证

- [x] 6.6.1 测试仿真执行性能
- [x] 6.6.2 测试 Trace 记录性能
- [x] 6.6.3 测试内存使用

## 7. 可选任务

### 7.1 AI 集成预留

- [x] 7.1.1 定义 AIProvider.handle_exception() 接口
- [x] 7.1.2 添加 AI 决策调用注释

### 7.2 增强功能

- [x] 7.2.1 实现 Trace 实时写入
- [x] 7.2.2 实现可视化定制选项
- [x] 7.2.3 实现并发遍历支持（预留）
