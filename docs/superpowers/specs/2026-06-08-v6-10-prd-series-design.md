# V6.10.x PRD 系列设计文档

> **设计日期**: 2026-06-08
> **目标**: 为 V6_OPTIMIZATION_IMPROVEMENTS.md 创建标准化的 PRD 系列框架
> **状态**: 设计阶段

---

## 1. 背景

### 1.1 来源文档

`docs/V6_OPTIMIZATION_IMPROVEMENTS.md` 是基于 `test_settings_simulation` 问题修复过程总结的调试优化改进方案。该方案包含 5 大类改进，总计约 25 小时工作量。

### 1.2 设计目标

将大型优化方案拆分为多个独立的小 PRD，每个 PRD：
- 范围明确，可独立实施
- 有清晰的依赖关系
- 遵循统一的 PRD 格式标准
- 符合 CLAUDE 系列文档的代码质量要求

---

## 2. 拆分策略

### 2.1 按优先级分批（方案 A）

| PRD | 优先级 | 内容 | 预计工时 |
|-----|--------|------|----------|
| V6.10.1 | P0 | 调试工具增强 + BRANCH 处理测试 | 5h |
| V6.10.2 | P1 | 状态机逻辑优化 + 可观测性增强 | 5h |
| V6.10.3 | P2 | 代码质量改进 + 集成测试 | 9h |
| V6.10.4 | P3 | 文档完善 | 4h |

### 2.2 依赖关系

```
V6.9 (已完成)
    │
    ▼
V6.10.1 (P0) ──→ V6.10.2 (P1) ──→ V6.10.3 (P2) ──→ V6.10.4 (P3)
  调试工具+测试      状态机逻辑         代码质量          文档
```

---

## 3. PRD 统一框架

### 3.1 标准章节结构

每个 PRD 必须包含以下章节：

```markdown
# V6.10.x [标题]

**版本**: V6.10.x
**日期**: 2026-06-08
**依赖**: V6.10.x-1 (或 V6.9)
**状态**: 设计阶段

## 1. 背景
### 1.1 问题回顾
### 1.2 根本原因

## 2. 目标
[量化指标表格]

## 3. 详细设计
### 3.x [各子项]

## 4. 修改文件清单
[文件清单表格]

## 5. 测试矩阵
### 5.x [各测试场景表格]

## 6. 实施步骤
[Step 清单]

## 7. 成功标准
### 7.1 功能验证
### 7.2 代码质量
### 7.3 测试覆盖
### 7.4 文档一致性

## 8. 修订记录
## 9. 已知限制（可选）
```

### 3.2 成功标准四类要求

每个 PRD 的成功标准必须包含以下 4 类：

#### 功能验证
- ✅ 具体功能点的可验证陈述

#### 代码质量（符合 CLAUDE_CONVENTIONS.md）
- ✅ 通过 mypy strict 类型检查
- ✅ 所有新增函数/方法有完整类型注解
- ✅ 禁用 `Any` 类型，使用具体类型
- ✅ 通过 ruff linting（零警告）
- ✅ 符合强类型要求（CLAUDE_CONVENTIONS.md §1）

#### 测试覆盖
- ✅ 新增代码测试覆盖率 > 80%
- ✅ 所有公共接口有单元测试
- ✅ 关键路径有集成测试
- ✅ 测试命名符合 `test_<method>_<scenario>_<expected>` 格式
- ✅ 测试文件放置在正确目录

#### 文档一致性
- ✅ 新增模块有 `README.md`
- ✅ 公共接口有 docstring
- ✅ 变更已更新 `CLAUDE_STATUS.md`
- ✅ 复杂逻辑有代码注释

---

## 4. 各 PRD 详细内容

### 4.1 V6.10.1 - 调试工具与 BRANCH 处理测试增强（P0）

**文件名**: `docs/prd/PRD_V6_10_1_debugging_tools.md`

#### 核心内容

**详细设计章节**:

##### 3.1 状态堆栈查看器 (dashboards/state_stack_viewer.py)

```python
class StateStackViewer:
    """状态堆栈可视化工具"""

    def show_stack(self, engine: GraphTraversalEngine) -> None:
        """显示当前堆栈状态"""

    def show_transitions(self, engine: GraphTraversalEngine, last_n: int = 10) -> None:
        """显示最近的状态转换"""
```

##### 3.2 决策点 Trace 增强 (src/traversal/graph_engine.py)

新增 `_record_decision()` 方法，记录关键决策点和上下文。

##### 3.3 BRANCH 处理单元测试 (tests/state_machine/test_branch_handling.py)

测试场景：
- 静态节点无子节点 → FRAME_COMPLETE
- 静态节点所有子节点已访问 → FRAME_COMPLETE
- 静态节点有未访问子节点 → NODE_SELECT
- **DYNAMIC_MATCH 节点所有子节点已访问 → FRAME_COMPLETE**（核心边界）

##### 3.4 完整遍历集成测试 (tests/v6/settings/test_settings_full_traversal.py)

验证深度优先遍历访问所有主要菜单项，无无限循环。

**修改文件清单**:

| 文件 | 类型 | 内容 |
|------|------|------|
| dashboards/state_stack_viewer.py | 新建 | StateStackViewer 类 |
| dashboards/README.md | 新建 | 模块文档 |
| src/traversal/graph_engine.py | 修改 | 新增 _record_decision() |
| tests/state_machine/test_branch_handling.py | 新建 | BRANCH 单元测试 |
| tests/v6/settings/test_settings_full_traversal.py | 新建 | 集成测试 |

**成功标准**:

```markdown
### 7.1 功能验证
- ✅ StateStackViewer.show_stack() 能正确显示堆栈深度、节点名称、状态信息
- ✅ StateStackViewer.show_transitions() 能显示最近 N 条状态转换
- ✅ Trace 记录包含所有关键决策点的完整上下文（stack_depth、current_state、visited_children）
- ✅ BRANCH 处理单元测试覆盖 DYNAMIC_MATCH 边界条件
- ✅ 完整遍历集成测试无无限循环，访问所有主要菜单项

### 7.2 代码质量
- ✅ StateStackViewer 类通过 mypy strict 类型检查
- ✅ 所有方法有完整类型注解（参数 + 返回值）
- ✅ 禁用 Any 类型
- ✅ 通过 ruff linting（零警告）
- ✅ 符合依赖注入原则（CLAUDE_CONVENTIONS.md §2.2）

### 7.3 测试覆盖
- ✅ test_branch_handling.py 覆盖率 > 90%
- ✅ test_settings_full_traversal.py 覆盖率 > 85%
- ✅ 所有测试命名符合 test_<method>_<scenario>_<expected> 格式
- ✅ 测试文件放置在 tests/state_machine/ 和 tests/v6/settings/

### 7.4 文档一致性
- ✅ dashboards/README.md 更新，说明 StateStackViewer 用法
- ✅ StateStackViewer 类有 docstring
- ✅ 变更已更新 CLAUDE_STATUS.md 的版本信息
```

---

### 4.2 V6.10.2 - 状态机逻辑与可观测性增强（P1）

**文件名**: `docs/prd/PRD_V6_10_2_state_machine_logic.md`

#### 核心内容

**详细设计章节**:

##### 3.1 提取 has_unvisited_children() 方法

将 DYNAMIC_MATCH 节点处理逻辑从 `_handle_branch` 提取到独立方法。

```python
def has_unvisited_children(
    self,
    node: TraversalNode,
    context: TraversalContext,
    engine: Optional[GraphTraversalEngine] = None
) -> Optional[bool]:
    """
    检查节点是否有未访问的子节点。

    Returns:
        - True: 有未访问子节点
        - False: 无未访问子节点
        - None: 无法确定（需要 engine 进一步检查）
    """
```

##### 3.2 状态转换断言增强

优化 `transition_to()` 方法，增强错误信息：

```python
raise ValueError(
    f"Invalid state transition: {self._state} → {target_state}\n"
    f"  Current node: {self._current_node_id}\n"
    f"  Target node: {node_id}\n"
    f"  Recent transitions:\n" +
    "\n".join(...) +
    f"\n  Valid transitions from {self._state}: ..."
)
```

##### 3.3 状态转换 Trace 标准化

确保所有状态转换都有 Trace 记录，包含完整上下文。

**修改文件清单**:

| 文件 | 类型 | 内容 |
|------|------|------|
| src/state_machine/traversal_fsm.py | 修改 | has_unvisited_children() + transition_to()增强 |
| src/traversal/graph_engine.py | 修改 | 调用 has_unvisited_children() |

---

### 4.3 V6.10.3 - 代码质量改进与集成测试（P2）

**文件名**: `docs/prd/PRD_V6_10_3_code_quality.md`

#### 核心内容

**详细设计章节**:

##### 3.1 不变量检查 (_assert_invariants)

```python
def _assert_invariants(self) -> None:
    """断言关键不变量，提前发现异常。"""
    stack = self.context.node_stack

    assert stack.size() <= 100, f"Stack too deep: {stack.size()}"
    assert len(self.context.visited_nodes) <= 10000
    assert len(self.context.current_path) == stack.size()
    # ...
```

##### 3.2 重构复杂条件逻辑 (_handle_branch_for_children)

将嵌套过深的条件逻辑提取为独立方法。

##### 3.3 集成测试扩展

增加更多端到端测试场景。

**修改文件清单**:

| 文件 | 类型 | 内容 |
|------|------|------|
| src/traversal/graph_engine.py | 修改 | _assert_invariants() + 重构 |
| tests/v6/ | 扩展 | 新增集成测试场景 |

**成功标准**:

```markdown
### 7.2 代码质量
- ✅ 不变量检查增加 < 5% 运行时间
- ✅ 条件逻辑嵌套深度 < 3 层
- ✅ 通过 mypy strict + ruff linting
```

---

### 4.4 V6.10.4 - 调试文档与指南（P3）

**文件名**: `docs/prd/PRD_V6_10_4_debugging_docs.md`

#### 核心内容

**详细设计章节**:

##### 3.1 状态机调试指南 (docs/debugging/STATE_MACHINE_DEBUGGING.md)

- 无限循环问题诊断步骤
- 子节点未入栈问题诊断步骤
- Trace 分析技巧

##### 3.2 Trace 事件参考 (docs/TRACE_EVENT_REFERENCE.md)

- decision 事件
- dynamic_matching 事件
- state_transition 事件

**修改文件清单**:

| 文件 | 类型 | 内容 |
|------|------|------|
| docs/debugging/STATE_MACHINE_DEBUGGING.md | 新建 | 调试指南 |
| docs/TRACE_EVENT_REFERENCE.md | 新建 | 事件参考 |

**成功标准**:

```markdown
### 7.1 功能验证
- ✅ 新开发者能在 10 分钟内定位常见问题
- ✅ 所有 Trace 事件类型有文档说明
```

---

## 5. 实施计划

### 5.1 实施顺序

1. **V6.10.1（P0）** - 立即见效，1-2天
2. **V6.10.2（P1）** - 增强可观测性，3-5天
3. **V6.10.3（P2）** - 长期质量，1-2周
4. **V6.10.4（P3）** - 文档完善，同时进行

### 5.2 总工时

| 优先级 | 工时 | 累计 |
|--------|------|------|
| P0 | 5h | 5h |
| P1 | 5h | 10h |
| P2 | 9h | 19h |
| P3 | 4h | 23h |

---

## 6. 设计原则

### 6.1 遵循现有标准

- PRD 格式参考 `docs/prd/PRD_V6_9_plan_compilation_and_matching.md`
- 代码质量遵循 `CLAUDE_CONVENTIONS.md`
- 成功标准符合项目质量门控

### 6.2 独立性与依赖

- 每个 PRD 可独立实施和验证
- 明确声明依赖关系
- 避免 circular dependencies

### 6.3 可验证性

- 每个功能点有明确的验证方式
- 成功标准可量化
- 测试矩阵完整

---

## 7. 下一步行动

1. ✅ 设计文档完成
2. ⏳ 用户审核设计
3. ⏳ 编写 V6.10.1 完整 PRD
4. ⏳ 实施 V6.10.1
5. ⏳ 依次完成 V6.10.2 - V6.10.4

---

## 附录

### A. 参考文档

- `docs/V6_OPTIMIZATION_IMPROVEMENTS.md` - 源文档
- `docs/prd/PRD_V6_9_plan_compilation_and_matching.md` - PRD 格式参考
- `CLAUDE_CONVENTIONS.md` - 代码质量标准
- `CLAUDE_STATUS.md` - 项目状态

### B. 修订记录

| 日期 | 版本 | 内容 |
|------|------|------|
| 2026-06-08 | 1.0 | 初始设计版本 |
