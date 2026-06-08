# V6.11.0 GraphTraversalEngine 架构重构

**版本**: V6.11.0
**日期**: 2026-06-08
**依赖**: V6.10.x 系列
**状态**: 设计阶段

---

## 1. 背景

### 1.1 当前问题

`GraphTraversalEngine`（1990 行，54 个方法）承担了过多职责：初始化、入口策略、动态匹配、缓存、Trace 记录、状态机调度。这导致：

- `_step_once` 混合了状态机调用、FRAME_COMPLETE 拦截、子节点推入、路径变化检测、页面快照等，无法单独测试。
- 动态匹配逻辑（`_generate_dynamic_children`、`_get_next_unvisited_child`、`invalidate_children_cache`、`_generated_pairs`）散落在 Engine 中，与页面状态管理耦合。
- 入口策略（`_execute_entry_policy`、`_wait_for_entry_condition`）使 Engine 承载了本该独立的初始化流程。
- `_record_*` 方法 12 个，混杂在 Engine 中。

### 1.2 目标

将**流转逻辑**与**组件职责**分离，让 Engine 成为纯粹的编排者。

---

## 2. 目标架构

```
GraphTraversalEngine (编排者)
├── PlanValidator                   # 计划验证
├── EntryPolicyExecutor             # 入口策略 & 等待条件
├── StepOrchestrator                # 核心步骤调度（状态机调用、FRAME_COMPLETE 拦截、子节点推入）
├── DynamicChildManager             # 动态子节点生成、缓存、失效、_generated_pairs 去重
├── PageSnapshotManager             # 页面指纹计算（纯函数，无状态）
├── TraceCoordinator                # Metrics → Span 转换 & 记录
└── PageCacheManager                # 页面缓存存取
```

**Engine 只做**：
1. 持有上述组件
2. 实现主循环 `run()`：调用入口执行器 → 循环调用步骤编排器
3. 检查完成策略和深度限制
4. 创建 `TraversalResult`

### 2.1 硬性约束

**仿真测试是唯一的成功标准。** 重构不改变任何外部行为，每一步迁移后仿真测试必须通过 — 89 步 COMPLETED，19 个节点，全部一级菜单 + 二级菜单遍历。如果测试失败，重构即失败，无论架构多清晰。

### 2.2 关键设计决策

| 决策 | 结论 | 理由 |
|------|------|------|
| 拆多少 | 按职责全拆，不设体量阈值 | 小如 PlanValidator 也独立，保持一致性 |
| 状态机 vs 编排器 | StepOrchestrator 覆盖状态机结果 | 状态机不应知道子节点如何生成 |
| `_generated_pairs` 归属 | DynamicChildManager 内部管理 | 去重是动态子节点生成的核心策略 |
| PageSnapshotManager | 纯函数，无状态 | 仅提供 fingerprint hash，不参与去重 |
| 迁移顺序 | DynamicChildManager → PageSnapshotManager → StepOrchestrator → 其余 | 按独立性从高到低，每步仿真测试验证 |

---

## 3. 组件接口

### 3.1 PlanValidator

```python
class PlanValidator:
    def validate(self, plan: TraversalPlan) -> None:
        """验证计划合法性，不合法抛出 ConfigurationError"""
```

### 3.2 EntryPolicyExecutor

```python
class EntryPolicyExecutor:
    def execute(self, plan: TraversalPlan, vision: VisionService, action: ActionExecutor) -> None:
        """按策略链尝试进入目标应用，全部失败抛出 EntryPolicyError"""
```

### 3.3 StepOrchestrator

```python
class StepOrchestrator:
    def execute_step(
        self,
        stack: NodeStack,
        context: TraversalRuntimeContext,
        state_machine: TraversalStateMachine,
        vision: VisionService,
        action: ActionExecutor,
    ) -> StepResult:
        """
        执行一个完整的状态机步骤：
        1. 获取当前节点
        2. 调用状态机
        3. FRAME_COMPLETE 拦截（动态匹配容器仍有未访问子节点时覆盖状态）
        4. BRANCH 后子节点推入
        5. 路径变化检测 & 缓存失效
        6. 页面未变化检测（EXECUTE 后页面不变 → 标记目标无效）
        """
```

### 3.4 DynamicChildManager

```python
class DynamicChildManager:
    def generate_children(
        self, container: TraversalNode, page_analysis: PageAnalysis,
        fingerprint: int, context: TraversalRuntimeContext,
    ) -> List[TraversalNode]:
        """生成动态子节点，内部检查 _generated_pairs 去重"""

    def get_next_unvisited_child(
        self, container: TraversalNode, context: TraversalRuntimeContext,
    ) -> Optional[str]:
        """获取下一个未访问的动态子节点 ID"""

    def invalidate_cache(self, container_id: str) -> None:
        """使指定容器的缓存失效"""

    def mark_element_invalid(self, fingerprint: int, element_name: str) -> None:
        """标记某页面上的某元素点击无效"""
```

### 3.5 PageSnapshotManager

```python
class PageSnapshotManager:
    @staticmethod
    def fingerprint(page_analysis: PageAnalysis) -> int:
        """计算页面指纹 hash（纯函数，无副作用）"""

    @staticmethod
    def has_changed(before: int, after: int) -> bool:
        """两个指纹是否不同"""
```

### 3.6 TraceCoordinator

```python
class TraceCoordinator:
    def record_metrics(self, metrics: Dict, context: TraversalRuntimeContext) -> None: ...
    def record_step_start(self, node_id: str, page_path: List[str]) -> None: ...
    def record_step_end(self, node_id: str, result: Dict) -> None: ...
```

---

## 4. 流转模型

### 4.1 主循环

```
Engine.run()
  ├── PlanValidator.validate(plan)
  ├── EntryPolicyExecutor.execute(plan, vision, action)
  ├── Push root_node to NodeStack
  └── loop:
       ├── Check depth limit, completion policy
       └── result = StepOrchestrator.execute_step(stack, context, sm, vision, action)
```

### 4.2 StepOrchestrator 内部

```
execute_step():
  1. snapshot_before = PageSnapshotManager.fingerprint(context.current_page_analysis)
  2. transition = state_machine.step(stack, context, vision, action)
  3. TraceCoordinator.record_metrics(transition.metrics)
  4. if to_state == FRAME_COMPLETE and container is DYNAMIC_MATCH:
       child = DynamicChildManager.get_next_unvisited_child(container)
       if child: stack.push(child); override to_state = NODE_SELECT
  5. if to_state == BRANCH and from in (EXECUTE, RESULT_VERIFY, PRECONDITION_CHECK):
       child = DynamicChildManager.get_next_unvisited_child(container)
       if child: stack.push(child)
  6. if current_path changed:
       DynamicChildManager.invalidate_cache(container_id)
  7. if from_state == EXECUTE and to_state == RESULT_VERIFY:
       snapshot_after = PageSnapshotManager.fingerprint(vision.analyze())
       if not PageSnapshotManager.has_changed(snapshot_before, snapshot_after):
           DynamicChildManager.mark_element_invalid(snapshot_before, element_name)
  8. TraceCoordinator.record_step_end(node_id, result)
```

---

## 5. 重构收益

- **单一职责**：每个组件 50-300 行，可独立单测
- **流转清晰**：`StepOrchestrator.execute_step` 完整描述一步的 8 个阶段
- **AI 友好**：小粒度接口 + 明确流转，AI 辅助开发时上下文更可控
- **回归安全**：现有仿真测试覆盖外部行为，重构内部结构后复用
- **可替换性**：单一接口可独立重写（如 DynamicChildManager 用 Go 实现）

---

## 6. 迁移步骤

每步完成后必须运行 `tests/v6/settings/test_settings_simulation.py::test_settings_simulation_run`，验证不退化：

| 步骤 | 内容 | 硬性验证 |
|------|------|----------|
| 1 | 提取 DynamicChildManager + PageSnapshotManager | 89 步 COMPLETED，19 节点，6 菜单 + 二级 |
| 2 | 提取 StepOrchestrator | 同上 + `test_branch_handling` 12/12 |
| 3 | 提取 EntryPolicyExecutor + TraceCoordinator | 同上 + `test_engine_initialization` 全部 |
| 4 | 清理 Engine，只保留编排逻辑 | 全量 V6 测试通过 |

---

## 7. 修订记录

| 日期 | 版本 | 内容 |
|------|------|------|
| 2026-06-08 | 1.0 | 初始设计 |
