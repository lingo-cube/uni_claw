## Context

enumerate 场景真实 run 产物 `20260805T052309367Z-1bc7a25ea6384e3` 显示 DFS 遍历进入 depth=4（Settings → Network & internet → Internet → Wi‑Fi → Advanced），超出 `boundaries.maxDepth=2`。trace replay 复现并确认根因。

上游发现：trace-analyzer 深度诊断 + trace replay harness `MaxSubframeDepth()` 诊断工具。

## Goals / Non-Goals

**Goals:**
- `NodeStack.Push` 在 `depth >= effectiveMaxDepth` 时拒绝，从源头阻止深度越界
- 仿真测试 L2 `DepthConstraint_StopsAtLevel2` 从 FAIL → PASS
- `SettingsEnumerateRegression` 持续作为基线回归

**Non-Goals:**
- 不改 ContainerHandler 的 MaxDepth 检查（保留为 defense-in-depth）
- 不改 DynamicMatch 子节点生成逻辑
- 不涉及视觉层（P0 已独立修复）

## Decisions

### D1: effectiveMaxDepth 提前计算，传给 NodeStack

**选择**: 在 `TraversalEngine.Initialize()` 中把 `effectiveMaxDepth` 计算移到 `TraversalRuntimeContext` 构造之前，传给 `maxDepth` 参数。

**替代**: 在 `TraversalRuntimeContext` 构造时单独计算 `effectiveMaxDepth`（重复逻辑）。

**理由**:
1. 单点计算、多处复用 —— `TraversalRuntimeContext` 和 `StepContext` 用同一个值
2. `NodeStack(maxDepth=2)` → Push 在 depth≥2 时返回 false，子帧不会被推入
3. ContainerHandler 的 `ctx.CurrentDepth > ctx.MaxDepth` 检查作为 defense-in-depth 保持不变
4. 对非 enumerate 场景（如 locate）同样适用 —— plan.IntentSlots.Depth 总是生效

**改动** (TraversalEngine.cs:112-164):
```
Before:
  _ctx = new TraversalRuntimeContext(maxDepth: _config.MaxDepth);    // = 10
  ...
  var effectiveMaxDepth = min(_config.MaxDepth, plan.IntentSlots.Depth); // = 2

After:
  var effectiveMaxDepth = min(_config.MaxDepth, plan.IntentSlots?.Depth);
  _ctx = new TraversalRuntimeContext(maxDepth: effectiveMaxDepth);   // = 2
  ...
  // 复用 effectiveMaxDepth，去掉重复计算
```

## Risks / Trade-offs

**R1: 现有测试可能依赖 MaxDepth=10**
- Risk: 某些测试 fixture 在 depth > plan.IntentSlots.Depth 时行为改变
- Mitigation: IntentSlots.Depth 为 null 时 `effectiveMaxDepth = _config.MaxDepth`（向后兼容）。`_config.MaxDepth=10` 不变，仅当 plan 显式指定 depth 时收紧。
- Trade-off: 无。Plan 指定 depth 就应该生效，之前是 bug。

**R2: Locate 场景影响**
- Risk: locate_one_item 的 plan 也有 depth 约束，收紧后可能阻止合法深度遍历
- Mitigation: locate 场景 target 在第一层，depth 约束不影响。且 locate 的 plan 中 IntentSlots.Depth=2 已在 ContainerHandler 侧生效，NodeStack 侧从未生效过。
