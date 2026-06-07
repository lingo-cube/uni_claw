# Design: State Machine Intelligence

## Context

### Current State

当前遍历状态机（`TraversalStateMachine`）采用机械式状态转移：
- 前置条件检查失败 → 直接进 BRANCH/EXECUTE，无纠正
- 容器完成 → 固定执行 back 操作
- 弹窗检测 → 简单 back 或忽略
- 异常处理 → 默认 skip，无策略选择

这种设计在复杂 UI 场景下效率低下，特别是：
- 多级菜单遍历时频繁 back/重新进入
- 前置条件失败时无智能恢复
- 弹窗和异常处理缺乏策略

### Constraints

- 必须保持与现有 `TraversalStateMachine` 接口兼容
- 必须在仿真和生产环境都能工作
- Vision 调用成本需要控制（不能无限增长）
- 状态转移逻辑必须清晰，避免无限循环

### Stakeholders

- 遍历引擎用户（需要更高效的遍历）
- 测试团队（需要稳定的仿真测试）
- AI 服务（Vision 调用增加）

## Goals / Non-Goals

**Goals:**
1. 引入关系驱动的精准纠正，减少不必要的 back 操作
2. 实现同级菜单智能切换（auto_escape）
3. 提供弹窗关闭的智能策略
4. 集成节点级 ErrorPolicy 处理
5. 确保异常正确传递到 ERROR_HANDLING 状态

**Non-Goals:**
1. 不引入新的状态机状态（使用现有状态）
2. 不修改全局状态机（只改 traversal state machine）
3. 不引入新的外部依赖
4. 不改变 TraversalNode 数据结构（除了已有的 fallback 字段）

## Decisions

### Decision 1: `classify_relation` 作为纯函数

**选择**：实现为独立的纯函数，不依赖类状态。

**理由**：
- 易于测试（无副作用）
- 可在不同 handler 中复用
- 逻辑简单，不需要复杂的状态管理

**替代方案**：
- 作为 TraversalStateMachine 方法 → 被拒绝，因为不需要访问实例状态

### Decision 2: 纠正后立即 Vision 验证

**选择**：在执行纠正动作后立即调用 vision 验证结果，而不是等到下一轮循环。

**理由**：
- 减少不必要的重试（纠正成功可立即退出）
- 确保 metrics 记录准确的页面状态
- 避免使用过期的页面数据

**替代方案**：
- 等到下一轮循环 → 被拒绝，会浪费一次重试机会

**权衡**：增加 vision 调用次数，但总体成本更低（避免错误操作）

### Decision 3: `step()` 异常处理包装

**选择**：在 `step()` 方法中添加 try-catch，捕获所有异常并设置 `context.last_error`。

**理由**：
- 确保异常能正确传递到 ERROR_HANDLING 状态
- 统一的异常处理入口点
- 不需要修改每个 handler 的异常处理逻辑

**替代方案**：
- 在每个 handler 内部捕获 → 被拒绝，重复代码且容易遗漏

### Decision 4: Vision 调用延迟策略

**选择**：支持可配置延迟（`context.wait_after_action_ms`），默认 100ms。

**理由**：
- UI 动画需要时间完成
- 不同设备/场景可能需要不同延迟
- 仿真环境可以设为 0，生产环境使用实际延迟

**替代方案**：
- 固定延迟 → 被拒绝，不够灵活
- 无延迟 → 被拒绝，可能获取到过期 UI

### Decision 5: `failed_nodes` 使用简单字典

**选择**：继续使用 `Dict[str, Dict[str, Any]]` 结构，可选添加辅助方法。

**理由**：
- 与现有 `TraversalRuntimeContext` 兼容
- 简单直接，不需要额外的数据类
- 可选添加辅助方法提高可读性

**替代方案**：
- 引入新的 ErrorRecord 类 → 被拒绝，过度设计

## Risks / Trade-offs

### Risk 1: Vision 调用成本增加

**风险**：precondition 纠正和 frame_complete 切换都会增加 vision 调用。

**缓解措施**：
- 纠正成功可立即退出，减少重试次数
- 总体成本预计降低 10-20%（错误操作减少）
- 可通过延迟配置进一步优化

### Risk 2: 状态转移逻辑复杂化

**风险**：handler 内部逻辑变复杂，可能引入新的 bug。

**缓解措施**：
- 充分的单元测试和仿真测试
- 详细的 trace metrics 记录
- 逐步重构，每个 handler 独立验证

### Risk 3: "回退过头"场景处理

**风险**：当回退过头时，`classify_relation` 返回 UNKNOWN，可能继续 back 使情况更糟。

**缓解措施**：
- 最多 3 次重试，失败后进异常处理
- 在文档中明确说明此限制
- Phase B 可引入深度恢复机制

### Risk 4: 仿真与生产环境差异

**风险**：仿真环境中 UI 无延迟，生产环境需要等待。

**缓解措施**：
- 使用可配置的 `wait_after_action_ms`
- 仿真环境设为 0，生产环境使用实际延迟
- 测试时覆盖两种场景

## Migration Plan

### 部署步骤

1. **代码变更**：
   - 更新 `src/state_machine/traversal_fsm.py`
   - 确认 `src/graph/node.py` 中 `FallbackAction.AUTO_ESCAPE` 存在

2. **测试验证**：
   - 运行仿真测试覆盖新逻辑
   - 运行全量回归测试
   - 验证 trace metrics 正确记录

3. **灰度发布**：
   - 先在仿真环境验证
   - 小规模生产环境测试
   - 全量发布

### 回滚策略

- PRD 变更集中在 handler 内部，回滚只需恢复旧 handler 逻辑
- 状态机接口未变，回滚风险低
- 保留详细的 trace 记录，便于问题排查

## Open Questions

### Q1: Vision 延迟默认值

**问题**：`wait_after_action_ms` 默认值应该是多少？

**建议**：100ms（平衡响应速度和 UI 稳定性）

**决策者**：待团队确认

### Q2: 弹窗按钮类型验证

**问题**：是否需要添加 `item.type == 'button'` 检查来提高匹配精度？

**建议**：可选增强，需要确认 `MenuItem` 模型支持 `type` 字段

**决策者**：待技术调研

### Q3: failed_nodes 辅助方法

**问题**：是否应该在 `TraversalRuntimeContext` 中添加辅助方法？

**建议**：可选，提高代码可读性

**决策者**：待团队讨论

## References

- PRD V6.7: `docs/PRD_V6_7-state-machine-intelligence.md`
- 现有状态机: `src/state_machine/traversal_fsm.py`
- 上下文模型: `src/trace/context.py`
- 节点定义: `src/graph/node.py`
