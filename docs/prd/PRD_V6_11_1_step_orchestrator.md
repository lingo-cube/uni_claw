# V6.11.1 StepOrchestrator + TraceCoordinator 提取

**版本**: V6.11.1
**日期**: 2026-06-08
**依赖**: V6.11.0 (engine refactor Phase 1)
**状态**: 设计阶段

---

## 1. 背景

V6.11.0 完成了 5 个组件的提取（PageSnapshotManager、DynamicChildManager、EntryPolicyExecutor、PlanValidator、PageCacheManager），Engine 从 1990 行降到 1293 行。

两个组件被推迟：
- **StepOrchestrator** — `_step_once` 依赖 `self` 上 10+ 个内部状态（context、state_machine、vision_service、trace_recorder、_child_mgr、_node_registry、_last_known_path、_last_recorded_path、_last_recorded_action），完整提取需要将这些状态全部参数化或注入
- **TraceCoordinator** — 15 个 `_record_*` 方法，需要传给 Engine、StepOrchestrator、DynamicChildManager、EntryPolicyExecutor 四个消费方

当前 Engine 的 `_step_once` 仍然是最复杂的单个方法（~200 行），包含 FRAME_COMPLETE 拦截、BRANCH 子节点推入、路径变化检测等逻辑。"跳过"是由于风险收益比，不是不需要。

## 2. 方案

### 2.1 StepOrchestrator 提取

`_step_once` 需要访问的所有状态集中在一个 `StepContext` 值对象中：

```python
@dataclass
class StepContext:
    stack: _NodeStackAdapter
    context: TraversalRuntimeContext
    state_machine: TraversalStateMachine
    vision: VisionService
    action: ActionExecutor
    child_mgr: DynamicChildManager
    node_registry: Dict[str, TraversalNode]
    trace: TraceCoordinator
    snapshot_mgr: PageSnapshotManager
```

然后 `StepOrchestrator.execute_step(ctx)` 持有完整逻辑。Engine 的 `run()` 创建此上下文，循环调用。

### 2.2 TraceCoordinator 提取

15 个 `_record_*` 方法提取为独立类，持有 `trace_recorder` 引用。在 `StepContext` 中作为 `trace` 字段注入，所有组件通过它记录 span。

- Engine 通过 `StepContext.trace` 调用
- DynamicChildManager 已有 `record_lifecycle` / `record_skip` 回调 → 替换为 `TraceCoordinator` 引用
- EntryPolicyExecutor 已有 `trace_recorder` + `should_record` → 替换为 `TraceCoordinator` 引用

### 2.3 迁移顺序

1. 提取 TraceCoordinator（减少 Engine 行数 ~300 行）
2. 提取 StepOrchestrator（减少 Engine 行数 ~200 行）
3. Engine 降到 ~800 行，只保留编排逻辑

### 2.4 硬性约束

每步后仿真测试必须通过：`test_settings_simulation_run` → 138 步 COMPLETED、19 节点。

## 3. 当前基线

| 指标 | 值 |
|------|-----|
| Engine | 1293 行 |
| Simulation | 138 步 COMPLETED, 19 节点 |
| 测试 | 79/79 passed |
| 已提取组件 | 5 个 (516 行) |
| 待提取 | StepOrchestrator, TraceCoordinator |

## 4. 修订记录

| 日期 | 版本 | 内容 |
|------|------|------|
| 2026-06-08 | 1.0 | 初始记录 |
