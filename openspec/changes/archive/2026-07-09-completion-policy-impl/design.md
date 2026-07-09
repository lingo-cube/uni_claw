## Context

TraversalEngine.RunAsync 有 5 条硬终止路径 (AllVisited, AntiLoop, MaxSteps, Cancelled, Error)，全部是引擎级机制。`CompletionPolicy` 类型已完整定义 (TargetFound/Timeout/MaxSteps + MatchMode + TargetFoundAction)，`TraversalRuntimeContext._completionPolicy` 已能存储它，但 RunAsync 循环从不检查它 — 用户意图层面的终止条件全部无效。

当前 TraversalEngine.cs RunAsync 循环结构 (line 166-258):
```
for loop → DelayPerStep → ExecuteStep → pop/sync stack → trace → page visit
→ AllVisited check (line 218)
→ AntiLoop check (line 223)
→ [缺失: CompletionPolicy check]
→ MaxSteps exhausted (line 231)
→ catch Cancelled → catch Error
```

**关键发现**: TraversalNode.Name = template.TemplateId (如 "switch_leaf")，不是元素文本 (如 "Dark mode")。TargetFound 匹配必须用 Operation.Target.Value (PlaceholderResolver 解析 {{item_text}} 后的元素文本)。

## Goals / Non-Goals

**Goals:**
- RunAsync 循环插入 CompletionPolicy 检查块 (TargetFound + Timeout + MaxSteps 三个分支)
- TraversalResult.Reasons 新增 TargetFound 和 Timeout 常量
- Done() GlobalState 映射新增 TargetFound → Completed, Timeout → Terminated
- 5 个单元测试覆盖 CompletionPolicy 各终止维度
- Phase A 对 ExecuteThenStop 等价 MarkAndStop 处理

**Non-Goals:**
- ExecuteThenStop 完整实现 (先执行操作再终止 — Phase 3)
- 基线测试场景 (Phase B, → simulation-baseline-tests change)
- 7 类规则验证框架 (Phase C)
- TraversalNode.Name 字段语义变更 (不改 Name = template ID 约定)

## Decisions

### D-A1: CompletionPolicy 检查位置 — AntiLoop 之后, MaxSteps 之前

**选择**: AntiLoop check 之后 (line ~225) 插入
**理由**: CompletionPolicy 是用户意图维度，优先级低于引擎级安全机制 (AllVisited/AntiLoop) 但高于引擎硬上限 (MaxSteps hard limit)
**替代方案**:
- StepOrchestrator 内部检查 → ❌ 维度不同, StepOrchestrator 是 14 步拦截层
- RunAsync 最前面检查 → ❌ 语义不对, 应在每步完成后检查

### D-A2: TargetFound 匹配字段 — Operation.Target.Value, 不用 Name

**选择**: `_ctx.CurrentFrame.Operation?.Target?.Value` (元素文本), Name 作为 static/root 节点 fallback
**理由**:
- Name = template.TemplateId ("switch_leaf"), 永远不等于元素文本 ("Dark mode")
- Operation.Target.Value = PlaceholderResolver 解析 {{item_text}} 后的值 ("Dark mode")
- NodeId 包含元素文本但格式不稳定 ("dyn_switch_leaf_Dark mode"), 不适合精确匹配
**替代方案**:
- 匹配 NodeId (Contains) → ❌ 复合格式, 精确匹配无法工作
- 改 Name 为 item_text → ❌ 改 TraversalNode.Name 语义影响太大, 暂不改

### D-A3: ExecuteThenStop 等价 MarkAndStop

**选择**: Phase A 对 ExecuteThenStop 检测到目标后立即终止, 不先执行操作
**理由**: Phase A 重点是检查逻辑本身, ExecuteThenStop 的"先执行再终止"语义是 Phase 3 增强功能
**替代方案**:
- 完整实现 ExecuteThenStop → ❌ 需要额外的操作执行+结果收集逻辑, 超出 Phase A 范围

### D-A4: MatchMode 使用 StringComparison.OrdinalIgnoreCase

**选择**: Exact 和 Contains 均用 OrdinalIgnoreCase
**理由**: UI 元素文本大小写不统一 (如 "Wi-Fi" vs "wifi"), TargetName 来自用户 IntentSlots, 需容忍大小写差异
**替代方案**:
- 精确大小写匹配 → ❌ 用户配置 "dark mode" 无法命中 "Dark mode"

## Risks / Trade-offs

- [Operation.Target.Value 为 null 的静态节点] → fallback 到 Name, 但 Name 也可能不匹配; 风险低 (静态节点通常不是 TargetFound 目标)
- [ExecuteThenStop 等价处理] → Phase 3 需改此处逻辑; 标注为 Phase 3 增量, 不算 design debt
- [Python 对齐] → Python 检查 visited_nodes 中的 name, C# 检查 CurrentFrame; 匹配时机不同但语义等价 (每步完成后检查当前节点)
