## 1. Phase 1: 图模型基础

- [x] 1.1 创建 `src/graph/` 目录结构
- [x] 1.2 实现 `TraversalNode` 数据类及关联类（Operation、Target、Precondition）
- [x] 1.3 实现 `ChildrenStrategy` 数据类及 `DynamicRule`
- [x] 1.4 实现 `ErrorPolicy` 数据类
- [x] 1.5 实现内置默认模板（menu_container、switch_leaf、slider_leaf）
- [x] 1.6 实现模板注册表加载器（从 JSON 加载）
- [x] 1.7 实现模板占位符替换逻辑（{{item_text}} 等）
- [x] 1.8 实现模板实例化逻辑（生成具体 TraversalNode）
- [x] 1.9 实现动态匹配器（根据 MenuItem 匹配规则）
- [x] 1.10 实现模板验证器（格式、引用、占位符）

## 2. Phase 1: 图模型测试

- [x] 2.1 编写 TraversalNode 数据类单元测试
- [x] 2.2 编写 Operation 和 Target 单元测试
- [x] 2.3 编写 Precondition 条件验证测试
- [x] 2.4 编写 ChildrenStrategy 动态匹配测试
- [x] 2.5 编写模板加载器单元测试
- [x] 2.6 编写占位符替换单元测试
- [x] 2.7 编写模板实例化单元测试
- [x] 2.8 编写动态匹配器单元测试
- [x] 2.9 编写内置默认模板测试

## 3. Phase 2: 状态机引擎

- [x] 3.1 创建 `src/state_machine/` 目录结构
- [x] 3.2 实现全局状态机（GlobalStateMachine）及状态枚举
- [x] 3.3 实现遍历状态机（TraversalStateMachine）及状态枚举
- [x] 3.4 实现 `StackFrame` 数据类
- [x] 3.5 实现节点栈（NodeStack）push/pop/top 操作
- [x] 3.6 实现全局状态机状态转换逻辑
- [x] 3.7 实现遍历状态机状态转换逻辑
- [x] 3.8 实现 Precondition 验证和自动导航
- [x] 3.9 实现节点栈深度限制（默认 10）
- [x] 3.10 实现状态机与图模型的交互接口

## 4. Phase 2: 状态机测试

- [x] 4.1 编写全局状态机状态转换测试
- [x] 4.2 编写遍历状态机状态转换测试
- [x] 4.3 编写节点栈 push/pop/top 操作测试
- [x] 4.4 编写节点栈深度限制测试
- [x] 4.5 编写 Precondition 验证测试
- [x] 4.6 编写自动导航逻辑测试
- [x] 4.7 编写状态机与图模型交互集成测试

## 5. Phase 3: Trace 系统

- [x] 5.1 创建 `src/trace/` 目录结构
- [x] 5.2 实现 `TraversalTrace` 数据类
- [x] 5.3 实现 `TraceStep` 数据类
- [x] 5.4 实现 `StateSnapshot` 数据类
- [x] 5.5 实现 `TraceSummary` 数据类
- [x] 5.6 实现 `TraceRecorder` 记录器
- [x] 5.7 实现状态转换时记录逻辑
- [x] 5.8 实现 EXECUTE 前后记录逻辑
- [x] 5.9 实现周期性状态快照（每 10 步）
- [x] 5.10 实现 JSON Lines 格式输出
- [x] 5.11 实现截图独立存储和引用
- [x] 5.12 实现历史清理功能（保留最近 N 次）
- [x] 5.13 实现 Trace 配置项（enabled、path、count）

## 6. Phase 3: Trace 回放

- [x] 6.1 实现回放引擎基础接口
- [x] 6.2 实现严格回放模式
- [x] 6.3 实现决策回放模式
- [x] 6.4 实现模拟回放模式
- [x] 6.5 实现截图哈希比较
- [x] 6.6 实现运行时节点图重建
- [x] 6.7 实现动态匹配效果分析

## 7. Phase 3: Trace 测试

- [x] 7.1 编写 Trace 数据类单元测试
- [x] 7.2 编写 TraceRecorder 记录逻辑测试
- [x] 7.3 编写 JSON Lines 输出测试
- [x] 7.4 编写严格回放模式测试
- [x] 7.5 编写决策回放模式测试
- [x] 7.6 编写模拟回放模式测试

## 8. Phase 4: 系统集成

- [x] 8.1 在 `TraversalEngine` 中添加 `use_graph_mode` 配置项
- [x] 8.2 在 `TraversalEngine` 中添加 `template_registry_path` 配置项
- [x] 8.3 在 `TraversalEngine` 中添加 `trace_config` 配置项
- [x] 8.4 实现图模式初始化流程（加载静态图或根节点+模板）
- [x] 8.5 实现图模式遍历流程（激活状态机和节点栈）
- [x] 8.6 实现图模式异常处理流程
- [x] 8.7 实现 V3.0 线性模式兼容（use_graph_mode=false）
- [x] 8.8 扩展 `TraversalState` 支持节点栈字段
- [x] 8.9 实现 V3.0 到图模式的数据适配

## 9. Phase 4: 集成测试

- [x] 9.1 编写端到端图模式遍历测试
- [x] 9.2 编写静态图遍历测试
- [x] 9.3 编写动态图遍历测试
- [x] 9.4 编写混合模式遍历测试
- [x] 9.5 编写配置开关测试（use_graph_mode 切换）
- [x] 9.6 编写 V3.0 兼容性测试（确保无破坏性变更）
- [x] 9.7 编写深度优先遍历正确性测试
- [x] 9.8 编写异常回退测试

## 10. Phase 4: 文档与验证

- [x] 10.1 更新 README 说明图模式功能
- [x] 10.2 更新配置文档说明新增配置项
- [x] 10.3 编写模板注册表示例和文档
- [x] 10.4 编写 Trace 使用说明文档
- [x] 10.5 运行现有测试套件验证 V3.0 兼容性
- [x] 10.6 在测试环境启用图模式验证集成
- [x] 10.7 在真实车机上验证三级深度菜单遍历（功能验证完成）
