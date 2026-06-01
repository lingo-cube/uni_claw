## 1. Phase 1: 基础数据结构与接口

- [x] 1.1 创建 `src/ai/` 目录结构
- [x] 1.2 实现 `DecisionResult` 枚举（SUCCESS, UNSURE, GIVE_UP）
- [x] 1.3 实现 `ContainerInference` 数据类（类型、置信度、匹配模板）
- [x] 1.4 实现 `AIStrategyAdvisor` 抽象基类（三个抽象方法）
- [x] 1.5 实现 `TraversalContext` 数据类（只读运行时状态）
- [x] 1.6 实现 `NoOpAIAdvisor`（返回默认值的空实现）
- [x] 1.7 实现 `MockAIAdvisor`（返回预定义值的测试实现）

## 2. Phase 1: 安全过滤器

- [x] 2.1 创建 `src/safety/` 目录结构
- [x] 2.2 实现 `SafetyResult` 数据类（is_safe, reason, fallback_node）
- [x] 2.3 实现 `SafetyFilter.validate()` 方法（白名单 + 黑名单验证）
- [x] 2.4 定义操作类型白名单（click, swipe, back, input_text, no_action）
- [x] 2.5 定义危险文本黑名单（恢复出厂设置、清除数据等）
- [x] 2.6 实现 fallback 节点生成（no_action 跳过操作）
- [x] 2.7 实现审计日志记录功能

## 3. Phase 1: 单元测试

- [x] 3.1 编写 `DecisionResult` 枚举测试
- [x] 3.2 编写 `ContainerInference` 数据类测试
- [x] 3.3 编写 `AIStrategyAdvisor` 接口测试（抽象类验证）
- [x] 3.4 编写 `NoOpAIAdvisor` 行为测试
- [x] 3.5 编写 `MockAIAdvisor` 行为测试
- [x] 3.6 编写 `TraversalContext` 序列化测试
- [x] 3.7 编写 `SafetyFilter` 白名单验证测试
- [x] 3.8 编写 `SafetyFilter` 黑名单验证测试
- [x] 3.9 编写 `SafetyFilter` fallback 生成测试

## 4. Phase 2: AI 调用缓存与超时

- [x] 4.1 实现 `AICallDecorator` 装饰器（超时控制）
- [x] 4.2 实现 `AIResponseCache` 类（TTL 缓存）
- [x] 4.3 实现缓存 key 生成（ui_hash + path_hash）
- [x] 4.4 实现防抖机制（同一节点同异常最多 2 次）
- [x] 4.5 实现置信度阈值检查

## 5. Phase 2: 引擎集成

- [x] 5.1 在 `TraversalEngine` 中添加 AI 配置项（enable_ai_advisor, ai_call_timeout）
- [x] 5.2 在容器类型推断处嵌入 AI 调用点
- [x] 5.3 在目标决策处嵌入 AI 调用点
- [x] 5.4 在异常兜底处嵌入 AI 调用点（责任链末尾）
- [x] 5.5 集成 `SafetyFilter` 验证（执行 AI 返回节点前）
- [x] 5.6 实现 `TraversalContext` 构建逻辑（从 TraversalState 提取）
- [x] 5.7 实现 AI 调用失败时的降级处理（返回 UNSURE）

## 6. Phase 2: 集成测试

- [x] 6.1 编写容器推断集成测试（规则失败 → AI 调用）
- [x] 6.2 编写目标决策集成测试（规则无法定位 → AI 调用）
- [x] 6.3 编写异常兜底集成测试（责任链耗尽 → AI 调用）
- [x] 6.4 编写 SafetyFilter 拒绝场景测试
- [x] 6.5 编写 AI 超时场景测试
- [x] 6.6 编写缓存命中场景测试
- [x] 6.7 编写防抖机制测试
- [x] 6.8 编写配置开关禁用测试（NoOp 验证）

## 7. Phase 2: 验证与文档

- [x] 7.1 运行现有测试套件，确保无破坏性变更
- [x] 7.2 在测试环境启用 AI 模式验证集成
- [x] 7.3 更新 README 文档说明 AI 功能
- [x] 7.4 更新配置文档说明新增配置项
