# V6.10.1 调试工具与 BRANCH 处理测试增强 - 任务清单

> **变更**: v6-10-1-debugging-tools
> **预计工时**: 5 小时

---

## 任务概览

| 任务 | 描述 | 预计时间 | 依赖 |
|------|------|----------|------|
| T1 | 创建 StateStackViewer 调试工具 | 2h | 无 |
| T2 | 添加决策点 Trace 增强 | 1h | 无 |
| T3 | 编写 BRANCH 处理单元测试 | 1h | 无 |
| T4 | 编写完整遍历集成测试 | 1h | 无 |

---

## T1: 创建 StateStackViewer 调试工具

**文件**: `dashboards/state_stack_viewer.py`

### 任务步骤

1. **创建文件和类结构**
   - [ ] 新建 `dashboards/state_stack_viewer.py`
   - [ ] 添加导入语句（Optional, Any, GraphTraversalEngine）
   - [ ] 创建 `StateStackViewer` 类

2. **实现 show_stack() 方法**
   - [ ] 添加方法签名和 docstring
   - [ ] 获取堆栈深度并显示
   - [ ] 获取当前状态（注意：engine.state_machine._state）
   - [ ] 获取当前路径
   - [ ] 遍历堆栈显示每层节点
   - [ ] 显示每层的已访问子节点

3. **实现 show_transitions() 方法**
   - [ ] 添加方法签名和 docstring
   - [ ] 调用 engine.state_machine.get_transition_history()
   - [ ] 处理历史记录不足的情况
   - [ ] 显示最近 N 条转换

4. **测试验证**
   - [ ] 手动测试：创建 mock engine 对象
   - [ ] 验证 show_stack() 能正确显示堆栈
   - [ ] 验证 show_transitions() 能正确显示转换

**验证方式**: 运行查看器，能显示堆栈

---

## T2: 添加决策点 Trace 增强

**文件**: `src/traversal/graph_engine.py`

### 任务步骤

1. **新增 _record_decision() 方法**
   - [ ] 在 GraphTraversalEngine 类中添加方法
   - [ ] 添加类型注解（decision: str, context: dict[str, Any]）
   - [ ] 检查 trace_recorder 是否存在
   - [ ] 创建 SpanNode，包含完整元数据
   - [ ] 调用 trace_recorder.record_span()

2. **集成到 BRANCH 处理**
   - [ ] 在 should_complete_frame 处调用 _record_decision()
   - [ ] 在跳过子节点生成处调用 _record_decision()
   - [ ] 传递完整的上下文信息

3. **测试验证**
   - [ ] 运行测试，检查 trace 输出
   - [ ] 验证 decision spans 包含所有字段
   - [ ] 验证 stack_depth、current_state、visited_children 正确

**验证方式**: 检查 trace 输出包含 decision span

---

## T3: 编写 BRANCH 处理单元测试

**文件**: `tests/state_machine/test_branch_handling.py`

### 任务步骤

1. **创建测试文件**
   - [ ] 新建 `tests/state_machine/test_branch_handling.py`
   - [ ] 添加导入语句（pytest, TraversalStateMachine, TraversalNode 等）
   - [ ] 创建 TestBranchHandling 类

2. **编写测试用例（6个场景）**
   - [ ] test_branch_with_no_children_static - 无子节点 → FRAME_COMPLETE
   - [ ] test_branch_with_all_children_visited_static - 全部已访问 → FRAME_COMPLETE
   - [ ] test_branch_with_unvisited_child_static - 有未访问 → NODE_SELECT
   - [ ] test_branch_with_all_children_visited_dynamic - **核心测试**
   - [ ] test_branch_with_unvisited_child_dynamic - 有未访问动态 → NODE_SELECT
   - [ ] test_branch_with_no_children_dynamic - 无子节点动态 → FRAME_COMPLETE

3. **Mock fixture 设计**
   - [ ] 为 DYNAMIC_MATCH 测试创建 mock_engine
   - [ ] Mock _get_next_unvisited_child 返回值
   - [ ] 验证 mock 按预期被调用

4. **测试验证**
   - [ ] 运行 `pytest tests/state_machine/test_branch_handling.py -v`
   - [ ] 验证所有 6 个测试通过
   - [ ] 验证覆盖率 > 90%

**验证方式**: `pytest tests/state_machine/test_branch_handling.py -v` 通过

---

## T4: 编写完整遍历集成测试

**文件**: `tests/v6/settings/test_settings_full_traversal.py`

### 任务步骤

1. **创建测试文件**
   - [ ] 新建 `tests/v6/settings/test_settings_full_traversal.py`
   - [ ] 添加导入语句（包括 settings_traversal_plan, settings_fixture）
   - [ ] 添加 @pytest.mark.integration 装饰器

2. **编写主测试**
   - [ ] test_settings_depth_first_traversal()
   - [ ] Given: 设置遍历计划和 fixture
   - [ ] Act: 运行 GraphTraversalEngine.run()
   - [ ] Assert: 验证基本结果（status, steps）
   - [ ] Assert: 验证所有主要菜单项被访问（7个页面）
   - [ ] Assert: 验证深度优先顺序（Wi-Fi < Bluetooth）

3. **实现辅助函数**
   - [ ] extract_page_names() - 提取页面名称
   - [ ] get_visit_order() - **真实实现**
     - [ ] 从 TraceRecorder 读取 trace 文件
     - [ ] 解析 state_transition 事件
     - [ ] 按时间顺序提取 node_id
     - [ ] 返回访问顺序列表

4. **测试验证**
   - [ ] 运行 `pytest tests/v6/settings/test_settings_full_traversal.py -v`
   - [ ] 验证测试通过
   - [ ] 验证覆盖率 > 85%
   - [ ] 验证无无限循环

**验证方式**: `pytest tests/v6/settings/test_settings_full_traversal.py -v` 通过

---

## 并行执行

T1 和 T3 可以并行开始（都是新建文件）。
T2 可以与 T1、T3 并行进行。
T4 依赖 T2（需要 trace_recorder），但可以提前编写测试代码。

---

## 验收检查清单

### 功能验收

- [ ] StateStackViewer.show_stack() 能正确显示堆栈深度、节点名称、状态信息
- [ ] StateStackViewer.show_transitions() 能显示最近 N 条状态转换
- [ ] Trace 记录包含所有关键决策点的完整上下文（stack_depth、current_state、visited_children）
- [ ] BRANCH 处理单元测试覆盖 DYNAMIC_MATCH 边界条件（6 个场景全部通过）
- [ ] 完整遍历集成测试无无限循环，访问所有主要菜单项（7 个页面）

### 代码质量验收

- [ ] StateStackViewer 类通过 mypy strict 类型检查
- [ ] 所有方法有完整类型注解（参数 + 返回值）
- [ ] 禁用 Any 类型，使用具体类型
- [ ] 通过 ruff linting（零警告）
- [ ] 符合依赖注入原则（CLAUDE_CONVENTIONS.md §2.2）

### 测试覆盖验收

- [ ] test_branch_handling.py 覆盖率 > 90%
- [ ] test_settings_full_traversal.py 覆盖率 > 85%
- [ ] 所有测试命名符合 `test_<method>_<scenario>_<expected>` 格式
- [ ] 测试文件放置在正确目录

### 文档验收

- [ ] dashboards/README.md 已创建，说明 StateStackViewer 用法
- [ ] StateStackViewer 类有完整的 docstring
- [ ] _record_decision() 方法有 docstring
- [ ] 变更已更新 CLAUDE_STATUS.md 的版本信息

---

## 完成标准

所有任务完成且所有验收检查通过后，本变更即可提交实施。
