## Context

### 当前状态
GraphTraversalEngine 已有完整的遍历执行逻辑（V6.7 智能状态机）和 Trace 系统（V6.6），但初始化流程是占位符实现：
- `_execute_entry_policy()` 总是返回 True，无实际逻辑
- `_wait_for_entry_condition()` 总是返回 True，无验证
- 根节点直接压入栈，无验证和 StepTracker 初始化
- `initialize()` 返回 bool，错误信息不明确

### 约束
- 必须保持与现有 TraversalStateMachine 接口兼容
- 必须在仿真和生产环境都能工作
- Vision 调用成本需要控制
- 初始化失败原因需要明确（区分可恢复/不可恢复错误）

### 相关方
- 遍历引擎用户（需要端到端运行能力）
- 测试团队（需要稳定的初始化测试）
- AI 服务（Vision 调用增加）

## Goals / Non-Goals

**Goals:**
1. 实现完整的初始化流程（计划验证 → 入口策略 → 条件验证 → 根节点压入）
2. 支持自动降级链（strategy → fallback → bind_current_screen）
3. 类型安全的配置（EntryConfig 替代 meta 字符串键）
4. 明确的错误处理（特定异常类型，区分可恢复性）
5. StepTracker 初始化（根节点压入时记录 StepNode）

**Non-Goals:**
1. 不修改全局状态机（只改 traversal state machine）
2. 不改变 TraversalNode 数据结构
3. 不实现深度恢复（如桌面翻页、文件夹查找）- 留作扩展点
4. 不引入新的外部依赖

## Decisions

### Decision 1: 异常处理而非布尔返回

**选择**：初始化方法失败时抛出特定异常，而不是返回 False。

**理由**：
- 错误类型明确（ConfigurationError vs EntryPolicyError）
- 可恢复性清晰（通过异常的 recoverable 属性）
- 调用者可以精确捕获特定错误类型

**替代方案**：返回 Result 对象（包含 status 和 error）- 被拒绝，因为异常处理更 Pythonic

### Decision 2: EntryConfig 数据类替代 meta 字符串键

**选择**：引入 EntryConfig 数据类，获得类型安全和 IDE 提示。

**理由**：
- 编译时类型检查，避免拼写错误
- IDE 自动完成和文档提示
- __post_init__ 验证配置合法性

**替代方案**：继续使用 meta 字典 - 被拒绝，因为容易拼写错误且无类型提示

**向后兼容**：同时支持 meta 字典（EntryConfig 优先，meta 作为后备）

### Decision 3: 自动降级链

**选择**：入口策略失败时自动尝试 fallback 策略，最终兜底 bind_current_screen。

**理由**：
- 用户无需处理降级逻辑
- 提高初始化成功率
- 降级顺序：deeplink（最快）→ cold_launch（通用）→ bind_current_screen（最安全）

**替代方案**：失败后返回错误，由上层决定 - 被拒绝，增加调用复杂度

### Decision 4: 等待条件可配置（快速/轮询）

**选择**：支持 fast（单次检查）和 polling（循环检查直到超时）两种模式。

**理由**：
- fast 模式：减少 vision 调用，适用于稳定场景
- polling 模式：提高成功率，适用于 UI 动画较长场景
- 可配置适应不同设备和应用

**权衡**：polling 模式增加 vision 调用次数，但成功率高

### Decision 5: 冷启动应用查找保持简单

**选择**：使用简单名称匹配，不实现桌面翻页或文件夹查找。

**理由**：
- 遵循 YAGNI 原则
- 大多数场景应用在首屏
- 失败时自动降级到 bind_current_screen

**扩展点**：在代码中添加 EXTENSION POINT 注释，未来可扩展

## Risks / Trade-offs

### Risk 1: Vision 调用成本增加

**风险**：入口策略和等待验证增加 vision 调用次数。

**缓解措施**：
- fast 模式默认启用，减少调用
- polling 模式可配置间隔和超时
- 自动降级减少不必要的重试

### Risk 2: 冷启动应用查找失败

**风险**：多页桌面或文件夹中应用查找失败。

**缓解措施**：
- 自动降级到 bind_current_screen
- 明确文档说明此限制
- 代码中标记扩展点

### Risk 3: 仿真与生产环境差异

**风险**：仿真环境 UI 无延迟，生产环境需要等待。

**缓解措施**：
- EntryConfig.action_delay_ms 可配置
- 仿真环境设为 0，生产环境使用实际延迟

### Risk 4: EntryConfig 序列化兼容性

**风险**：JSON 反序列化时 EntryConfig 字段不匹配。

**缓解措施**：
- __post_init__ 验证配置合法性
- meta 字典作为后备（向后兼容）

## Migration Plan

### 部署步骤

1. **代码变更**：
   - 在 `src/graph/node.py` 添加 EntryConfig 数据类
   - 在 `src/graph/plan.py` 添加 entry_config 字段
   - 在 `GraphTraversalEngine` 添加初始化方法

2. **测试验证**：
   - 单元测试：EntryConfig 序列化/反序列化
   - 单元测试：入口策略各场景
   - 仿真测试：完整初始化流程
   - 全量回归测试

3. **灰度发布**：
   - 先在仿真环境验证
   - 小规模生产环境测试
   - 全量发布

### 回滚策略

- PRD 变更集中在初始化方法，回滚只需恢复旧 initialize() 逻辑
- EntryConfig 和 meta 双模式支持，回滚风险低
- 保留详细的 Trace 记录，便于问题排查

## Open Questions

### Q1: EntryConfig 字段验证

**问题**：EntryConfig 需要哪些验证规则？

**决策**：
- wait_mode: 必须是 "fast" 或 "polling"
- trace_level: 必须是 "minimal"、"standard" 或 "detailed"
- wait_timeout: 必须为正数
- wait_interval: 必须为正数

### Q2: StepTracker 集成方式

**问题**：StepTracker 已在 V6.5 实现，如何正确初始化？

**决策**：在根节点压入时调用 `step_tracker.on_step_start()` 和 `trace_recorder.record_step_start()`

### Q3: 异常类型位置

**问题**：异常类型应该定义在哪里？

**决策**：在 `src/exception/` 目录下创建 `initialization.py` 文件，与异常处理架构保持一致

## References

- PRD V6.8: `docs/PRD_V6_8_engine_initialization.md`
- 现有状态机: `src/state_machine/traversal_fsm.py`
- 上下文模型: `src/trace/context.py`
- 节点定义: `src/graph/node.py`
