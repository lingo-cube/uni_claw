# V6.10.4 调试文档与指南

**版本**: V6.10.4
**日期**: 2026-06-08
**依赖**: V6.10.3 code-quality
**状态**: 设计阶段
**优先级**: P3
**预计工时**: 4h

---

## 1. 背景

### 1.1 问题回顾

V6.10.1-V6.10.3 实施后，调试工具和代码质量已得到显著提升，但文档支持不足：

| 类别 | 具体问题 | 影响 |
|------|----------|------|
| **知识分散** | 调试知识分散在代码注释、PRD、个人笔记中 | 新开发者上手困难 |
| **缺少诊断指南** | 没有系统的问题诊断流程 | 解决问题依赖经验 |
| **Trace 参考缺失** | Trace 事件类型没有文档说明 | 难以理解 trace 输出 |

### 1.2 改进目标

| 维度 | 当前状态 | 目标状态 |
|------|----------|----------|
| **文档集中度** | 分散在各处 | 集中的调试指南 |
| **诊断流程** | 依赖经验 | 系统化的诊断步骤 |
| **Trace 文档** | 缺失 | 完整的事件类型参考 |

---

## 2. 目标

### 2.1 功能目标

1. **状态机调试指南**：系统化的问题诊断流程
2. **Trace 事件参考**：所有事件类型的文档说明
3. **快速诊断流程图**：常见问题的快速定位

### 2.2 质量目标

| 指标 | 目标值 |
|------|--------|
| 新手上手时间 | < 10 分钟定位常见问题 |
| 文档完整性 | 所有 Trace 事件类型有说明 |

---

## 3. 详细设计

### 3.1 状态机调试指南

#### 3.1.1 文件位置

新建文件：`docs/debugging/STATE_MACHINE_DEBUGGING.md`

#### 3.1.2 内容结构

```markdown
# 状态机调试指南

## 快速诊断

### 问题：无限循环

**症状**：
- 步数达到上限（500/1000）
- 状态在 NODE_SELECT/BRANCH 间循环
- Trace 显示大量重复的状态转换

**诊断步骤**：

1. **检查 BRANCH 状态处理**
   ```bash
   # 查看最近的 BRANCH 状态转换
   cat trace.jsonl | jq 'select(.to_state == "branch")' | tail -20
   ```

2. **检查 visited_children 记录**
   ```bash
   # 查看哪些子节点被标记为已访问
   cat trace.jsonl | jq 'select(.visited_children)' | tail -10
   ```

3. **使用状态堆栈查看器**
   ```python
   from dashboards.state_stack_viewer import StateStackViewer
   viewer = StateStackViewer()
   viewer.show_stack(engine)  # 在循环中调用查看当前状态
   ```

**常见原因**：
- `_handle_branch` 对 DYNAMIC_MATCH 节点总是返回 `has_unvisited_children = True`
- `visited_children` 未正确更新
- `FRAME_COMPLETE` 转换条件错误

**相关文件**：
- `src/state_machine/traversal_fsm.py:_handle_branch`
- `src/traversal/graph_engine.py:_get_next_unvisited_child`

---

### 问题：子节点未入栈

**症状**：
- 父节点完成后直接返回，未处理下一个子节点
- Trace 中 `child_pushed` 为空

**诊断步骤**：

1. **检查 should_complete_frame 标志**
   ```bash
   cat trace.jsonl | jq 'select(.should_complete_frame == true)'
   ```

2. **检查 FRAME_COMPLETE → NODE_SELECT 转换**
   ```bash
   cat trace.jsonl | jq 'select(.from_state == "frame_complete" and .to_state == "node_select")'
   ```

**常见原因**：
- `_get_next_unvisited_child` 错误返回 None
- 状态转换到 FRAME_COMPLETE 过早
- `_push_node` 未被调用

**相关文件**：
- `src/traversal/graph_engine.py:1037-1090`

---

## Trace 分析技巧

### 过滤特定事件

```bash
# 查看所有决策点
jq 'select(.span_type == "decision")' trace.jsonl

# 查看所有动态匹配（包括跳过的元素）
jq 'select(.span_type == "dynamic_matching")' trace.jsonl

# 查看状态转换序列
jq 'select(.span_type == "state_transition")' trace.jsonl | \
  jq -r '"\(.from_state) → \(.to_state) | \(.node_id)"'
```

### 统计状态转换

```bash
jq 'select(.span_type == "state_transition")' trace.jsonl | \
  jq -r '.to_state' | sort | uniq -c | sort -rn
```

---

## 常见错误信息解读

### ValueError: Invalid state transition

**错误信息示例**：
```
ValueError: Invalid state transition: node_select → branch
  Current node: menu_container-Wi-Fi-0-root
  Target node: N/A
  Recent transitions:
    branch → node_select (node: menu_container-Wi-Fi-0-root)
    execute → branch (node: switch-Wi-Fi-0)
    node_select → execute (node: switch-Wi-Fi-0)
  Valid transitions from node_select: [execute]
```

**含义**：尝试进行无效的状态转换
**原因**：状态机逻辑错误或状态不一致
**排查**：检查 `from_state` 的有效转换列表

---

### AssertionError: Invariant violation

**错误信息示例**：
```
AssertionError: Stack too deep: 101. This may indicate an infinite loop.
```

**含义**：堆栈深度超过限制（100 层）
**原因**：可能的无限循环或深度递归
**排查**：
1. 检查是否有状态循环
2. 检查 `FRAME_COMPLETE` 转换是否正确触发

---

## 调试工具使用

### StateStackViewer

**显示堆栈状态**：
```python
from dashboards.state_stack_viewer import StateStackViewer

viewer = StateStackViewer()
viewer.show_stack(engine)
```

**输出示例**：
```
============================================================
State Stack (depth: 2)
Current State: TraversalState.BRANCH
Current Path: ['root', 'Wi-Fi']
============================================================
→ menu_container-Wi-Fi-0-root (Wi-Fi Menu)
   Visited: ['switch-Wi-Fi-0']
  menu_container-root-0 (Root)
   Visited: ['Wi-Fi', 'Bluetooth']
```

**显示最近转换**：
```python
viewer.show_transitions(engine, last_n=5)
```

---

## 最佳实践

1. **始终使用 StateStackViewer**：在调试循环时优先使用堆栈查看器
2. **保存 Trace 文件**：每次测试都保存 trace，便于后续分析
3. **使用 jq 分析**：用 jq 过滤和统计 trace 数据
4. **检查不变量违反**：不变量检查通常能早期发现根本问题
```

---

### 3.2 Trace 事件参考

#### 3.2.1 文件位置

新建文件：`docs/TRACE_EVENT_REFERENCE.md`

#### 3.2.2 内容结构

```markdown
# Trace 事件类型参考

## decision

记录关键决策点和上下文。

**何时记录**：
- 进入/退出 FRAME_COMPLETE
- 决定跳过子节点生成
- 选择恢复策略

**字段说明**：
- `span_type`: "decision"
- `action`: 决策类型（如 "branch_complete_frame"）
- `metadata`: 决策上下文

**示例**：
```json
{
  "span_type": "decision",
  "action": "branch_complete_frame",
  "metadata": {
    "reason": "no_more_children",
    "node": "menu_container-Wi-Fi-0-root",
    "stack_depth": 2,
    "current_state": "branch",
    "visited_count": 1
  }
}
```

---

## dynamic_matching

记录动态匹配结果，包括跳过的元素。

**何时记录**：
- 元素匹配成功
- 元素不匹配任何规则（跳过）
- 元素匹配但动作不是 GENERATE_CHILD

**字段说明**：
- `span_type`: "dynamic_matching"
- `metadata`: 匹配结果

**示例**：
```json
{
  "span_type": "dynamic_matching",
  "metadata": {
    "reason": "no_match",
    "item": {
      "type": "menu_item",
      "text": "HomeNetwork",
      "index": 1
    }
  }
}
```

---

## state_transition

记录状态机转换。

**何时记录**：
- 每次状态机转换

**字段说明**：
- `span_type`: "state_transition"
- `from_state`: 源状态
- `to_state`: 目标状态
- `node_id`: 相关节点ID
- `action`: 触发动作（push_child, no_more_children等）

**示例**：
```json
{
  "span_type": "state_transition",
  "from_state": "branch",
  "to_state": "node_select",
  "node_id": "menu_container-Wi-Fi-0-root",
  "action": "push_child",
  "metadata": {
    "child_id": "switch-Wi-Fi-0"
  }
}
```

---

## 错误事件

### invariant_violation

不变量违反时记录。

**示例**：
```json
{
  "span_type": "error",
  "action": "invariant_violation",
  "metadata": {
    "message": "Stack too deep: 101",
    "stack_depth": 101,
    "visited_nodes": 5000
  }
}
```

---

## 事件类型汇总

| 事件类型 | 用途 | 记录时机 |
|----------|------|----------|
| `decision` | 决策点 | 关键决策时 |
| `dynamic_matching` | 动态匹配 | 每个元素匹配 |
| `state_transition` | 状态转换 | 每次转换 |
| `error` | 错误 | 异常发生时 |
| `node_enter` | 节点进入 | 进入新节点 |
| `node_exit` | 节点退出 | 退出节点 |
```

---

### 3.3 快速诊断流程图

#### 3.3.1 文件位置

在 `docs/debugging/STATE_MACHINE_DEBUGGING.md` 末尾添加

#### 3.3.2 内容

```markdown
## 快速诊断流程图

### 无限循环问题

```
开始
  │
  ▼
步数达到上限？
  │
  ├─ 是 → 查看 Trace 中的状态转换
  │        │
  │        ▼
  │      有重复的状态转换模式？
  │        │
  │        ├─ 是 → 检查 BRANCH 状态处理
  │        │         │
  │        │         ▼
  │        │       visited_children 正确？
  │        │         │
  │        │         ├─ 否 → 修复 visited_children 更新
  │        │         │
  │        │         └─ 是 → 检查 has_unvisited_children 逻辑
  │        │
  │        └─ 否 → 检查 FRAME_COMPLETE 转换
  │
  └─ 否 → 问题不是无限循环
```

---

### 子节点未入栈问题

```
开始
  │
  ▼
父节点完成后直接退出？
  │
  ├─ 是 → 查看 Trace 中的 child_pushed
  │        │
  │        ▼
  │      child_pushed 为空？
  │        │
  │        ├─ 是 → 检查 _get_next_unvisited_child 返回值
  │        │         │
  │        │         ▼
  │        │       返回 None？
  │        │         │
  │        │         ├─ 是 → 检查子节点生成逻辑
  │        │         │
  │        │         └─ 否 → 检查 _push_node 调用
  │        │
  │        └─ 否 → 检查状态转换逻辑
  │
  └─ 否 → 问题不是子节点未入栈
```
```

---

## 4. 修改文件清单

| 文件 | 类型 | 内容 | 位置 |
|------|------|------|------|
| `docs/debugging/STATE_MACHINE_DEBUGGING.md` | 新建 | 状态机调试指南 | `docs/debugging/` |
| `docs/TRACE_EVENT_REFERENCE.md` | 新建 | Trace 事件类型参考 | `docs/` |
| `docs/debugging/README.md` | 新建 | 调试文档索引 | `docs/debugging/` |
| `docs/debugging/QUICK_START.md` | 新建 | 10分钟快速诊断 | `docs/debugging/` |

---

## 5. 测试矩阵

### 5.1 文档完整性测试

| 场景 | 验证方式 | 预期 |
|------|----------|------|
| 所有 Trace 事件有文档 | 对照代码和文档 | 所有事件类型有说明 |
| 调试指南可执行 | 按照指南操作 | 能定位问题 |
| 示例代码正确 | 运行示例代码 | 无语法错误 |

### 5.2 文档可用性测试

| 场景 | 测试方式 | 预期 |
|------|----------|------|
| 新手上手时间 | 记录定位常见问题时间 | < 10 分钟 |
| 文档导航 | 查找特定问题 | < 1 分钟找到相关章节 |

---

## 6. 实施步骤

| Step | 内容 | 可验证 | 预计时间 |
|------|------|--------|----------|
| 1 | 创建 `docs/debugging/STATE_MACHINE_DEBUGGING.md` | 所有章节有内容 | 1.5h |
| 2 | 创建 `docs/TRACE_EVENT_REFERENCE.md` | 所有事件类型有说明 | 1h |
| 3 | 创建 `docs/debugging/README.md` 和 `QUICK_START.md` | 导航清晰 | 0.5h |
| 4 | 审查和完善文档 | 文档可执行，无错别字 | 1h |

**总计**: 4 小时

---

## 7. 成功标准

### 7.1 功能验证

- ✅ 调试指南包含无限循环和子节点未入栈的完整诊断流程
- ✅ Trace 事件参考包含所有事件类型的说明
- ✅ 快速诊断流程图清晰易懂
- ✅ 所有示例代码正确可运行

### 7.2 文档质量

- ✅ 所有章节有完整内容
- ✅ 无错别字和格式错误
- ✅ 代码示例有语法高亮
- ✅ 文档结构清晰，易于导航

### 7.3 可用性验证

- ✅ 新开发者能在 10 分钟内定位常见问题
- ✅ 能在 1 分钟内找到特定问题的相关章节
- ✅ 按照调试指南操作能成功定位问题

### 7.4 文档一致性

- ✅ 文档中的代码示例与实际代码一致
- ✅ Trace 事件字段与实际输出一致
- ✅ 文件路径引用正确
- ✅ 变更已更新 `CLAUDE_STATUS.md` 和 `docs/INDEX.md`

---

## 8. 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-08 | V6.10.4.0 | 初始版本：调试文档与指南 |

---

## 9. 已知限制

| 限制 | 影响 | 后续版本 |
|------|------|----------|
| 文档仅覆盖常见问题 | 罕见问题需要额外排查 | 持续更新 |
| Trace 事件可能增加 | 新事件需要添加文档 | 持续更新 |

---

## 10. 参考文档

- `docs/V6_OPTIMIZATION_IMPROVEMENTS.md` - 源改进方案
- `docs/prd/PRD_V6_10_1_debugging_tools.md` - 调试工具 PRD
- `docs/prd/PRD_V6_10_2_state_machine_logic.md` - 状态机逻辑 PRD
- `docs/superpowers/specs/2026-06-08-v6-10-prd-series-design.md` - 系列设计文档
