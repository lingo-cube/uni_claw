# V6.7 遍历状态机智能化 PRD

**版本**: V6.7.1
**日期**: 2026-06-07
**依赖**: V6.6 trace-handler-metrics-enhancement（已完成）
**修订**: V6.7.1 修正 vision 刷新策略、添加 engine integration

---

## 1. 概述

当前状态机能流转但决策机械：前置条件失败一律靠外部循环解决，容器完成只会 `back`，弹窗和异常无智能策略。本 PRD 引入**关系驱动的精准纠正**，用已有上下文信息（`current_path`、`level1_menus`、`page_tree`）做出精准决策。

---

## 2. 核心原则

| 原则 | 含义 |
|------|------|
| **关系驱动** | 先判"我在哪"和"我要去哪"的关系，再决定动作 |
| **精准优先** | 能点击导航就不 back，能同级切换就不 back |
| **back 受限** | 只在"走深了"或"迷失"时用，每次一步 |
| **已有上下文复用** | 基于 `current_path`、`level1_menus`、`page_tree`，不引入新字段 |
| **受限恢复** | 精准纠正最多 3 次，耗尽进 ERROR_HANDLING |
| **默认智能，可覆盖** | `auto_escape` 默认，`exit_condition.fallback = "back"` 显式覆盖 |

---

## 3. 完整状态转移图

```
                    ┌─────────────────────────────────────┐
                    │                                     │
                    ▼                                     │
              NODE_SELECT                                  │
              /         \                                 │
    NODE_READY           FRAME_DONE                       │
         /                    \                           │
        ▼                      ▼                          │
PRECONDITION_CHECK        FRAME_COMPLETE                  │
   │      │      │          │         │                   │
   │  MATCH  │  TIMEOUT     │     ERROR                   │
   │    │    │    │         │         │                   │
   │    ▼    │    ▼         ▼         ▼                   │
   │ EXECUTE │  ERROR     NODE_SELECT  ERROR              │
   │   │    │              (back完成)                      │
   │   │    │                                             │
   │   ▼    │◄─── 自循环（最多3次）                        │
   │ RESULT_VERIFY ──┬── POPUP_DETECTED ──► POPUP_HANDLING│
   │       │        │                         │    │      │
   │       ▼        │                    RESOLVED FAILED  │
   │     BRANCH ◄───┘                         │    │      │
   │    /     \                               ▼    ▼      │
   │ CHILDREN  LEAF                     RESULT     ERROR  │
   │    │       │                                           │
   │    ▼       ▼                                           │
   │ NODE     FRAME                                         │
   │_SELECT  _COMPLETE                                      │
   │                                                        │
   └────────────────────────────────────────────────────────┘

ERROR_HANDLING (任意状态进入)
   ├── retry_count < max_retries → EXECUTE (RETRY)
   ├── fallback = "skip"         → NODE_SELECT (SKIP)
   ├── fallback = "backtrack"    → FRAME_COMPLETE (BACKTRACK)
   └── fallback = "abort"        → 全局终止
```

---

## 4. `classify_relation`：关系判断

```python
def classify_relation(current_path, expected_page, page_menus):
    """判断当前页面与预期页面的关系。

    Args:
        current_path: list[str], 视觉分析返回的当前路径
        expected_page: str, 预期页面名
        page_menus: list[str], 当前页面的 level1_menus + level2_menus 名字列表

    Returns: "MATCH" | "NAVIGABLE" | "DEEPER" | "UNKNOWN"

    NOTE: 当回退过头（current_depth < expected_depth）且菜单中无目标时，
    返回 UNKNOWN 并执行 back。这是可接受的简化，因为:
    1) 继续 back 最终可能回到有效页面
    2) 3次重试后进入异常处理兜底
    """
    if not current_path:
        return "UNKNOWN"

    # 1. 已在预期页面
    if current_path[-1] == expected_page:
        return "MATCH"

    # 2. 预期页面名在当前路径中但非末位 → 走深了
    if expected_page in current_path:
        return "DEEPER"

    # 3. 预期页面名在当前页面的菜单中 → 可直接导航
    if expected_page in page_menus:
        return "NAVIGABLE"

    # 4. 完全无法确定
    return "UNKNOWN"
```

---

## 5. 各 Handler 详细设计

### 5.1 `_handle_precondition_check`

**新签名**: `(self, stack, context, vision, action)` — `vision` 由 `step()` 传入。

**流程**：
1. 若节点无 precondition，直接进 EXECUTE
2. 最多 3 轮智能纠正
3. 每轮：调用 `vision.analyze_screenshot` → 获取 `PageAnalysis`
4. 调用 `classify_relation` 判断关系
5. 根据关系执行纠正动作
6. **纠正后立即调用 vision 验证**（避免浪费重试）
7. 若纠正后满足条件 → EXECUTE
8. 耗尽 → ERROR_HANDLING

**注意**：纠正动作执行后，立即调用 `vision.analyze_screenshot` 获取最新页面状态，而不是等到下一轮循环。这样可以:
- 减少不必要的重试次数
- 更快地检测到纠正成功的情况
- 确保 metrics 记录准确的页面状态

```python
def _handle_precondition_check(self, stack, context, vision, action):
    current_node = stack.peek()
    if not current_node or not current_node.has_precondition():
        self._last_handler_metrics = None
        return TraversalState.EXECUTE

    expected_page = current_node.precondition.page_name
    if not expected_page:
        self._last_handler_metrics = None
        return TraversalState.EXECUTE

    max_retries = 3
    wait_ms = 0  # 仿真环境为 0，生产可用 simulate_delay

    for retry in range(max_retries):
        import time
        t0 = time.time()
        try:
            page = vision.analyze_screenshot(b"")
        except Exception:
            continue  # 视觉失败，重试

        elapsed = (time.time() - t0) * 1000
        if hasattr(context, 'current_page_analysis'):
            context.current_page_analysis = page

        # 满足条件 → 直接通过
        if page.current_path and page.current_path[-1] == expected_page:
            self._last_handler_metrics = {
                "ai_call": self._build_ai_call_metrics(page, elapsed, vision)
            }
            return TraversalState.EXECUTE

        # 关系驱动纠正
        menus = [m.name for m in ((page.level1_menus or []) + (page.level2_menus or []))]
        relation = classify_relation(
            page.current_path or [], expected_page, menus
        )

        if relation == "NAVIGABLE":
            # 优先重试原目标
            from src.simulation.operation_executor import ExecutionContext
            from datetime import datetime
            target = getattr(current_node.operation, 'target', None)
            op = {"action": "click", "target": str(target) if target else expected_page}
            ctx = ExecutionContext(current_node.node_id, current_node.name, op, timestamp=datetime.now())
            action.execute(ctx)
            if wait_ms:
                time.sleep(wait_ms / 1000.0)

        elif relation == "DEEPER":
            ctx = ExecutionContext(current_node.node_id, current_node.name,
                                   {"action": "back"}, timestamp=datetime.now())
            action.execute(ctx)
            if wait_ms:
                time.sleep(wait_ms / 1000.0)

        else:  # UNKNOWN
            ctx = ExecutionContext(current_node.node_id, current_node.name,
                                   {"action": "back"}, timestamp=datetime.now())
            action.execute(ctx)
            if wait_ms:
                time.sleep(wait_ms / 1000.0)

        self._last_handler_metrics = {
            "execution": {
                "action": "back" if relation in ("DEEPER", "UNKNOWN") else "click",
                "status": "success",
                "target": expected_page,
            }
        }

        # 纠正后立即验证，避免浪费重试
        try:
            t1 = time.time()
            page = vision.analyze_screenshot(b"")
            elapsed_verify = (time.time() - t1) * 1000
            if hasattr(context, 'current_page_analysis'):
                context.current_page_analysis = page

            # 纠正成功，提前退出
            if page.current_path and page.current_path[-1] == expected_page:
                self._last_handler_metrics = {
                    "ai_call": self._build_ai_call_metrics(page, elapsed_verify, vision),
                    "correction": {"action": "click" if relation == "NAVIGABLE" else "back", "success": True}
                }
                return TraversalState.EXECUTE
        except Exception:
            pass  # 继续重试

    # 重试耗尽
    self._last_handler_metrics = {
        "error": {"error_type": "PreconditionTimeout", "error_message": f"Failed to reach {expected_page} after {max_retries} retries"}
    }
    return TraversalState.ERROR_HANDLING
```

**Trace 输出**：每轮纠正产生 `execution` span，最终满足条件时产生 `ai_call` span。

---

### 5.2 `_handle_frame_complete_state`

**签名不变**: `(self, stack, context, action)`。

**流程**：
1. 读取 `exit_condition.fallback`，默认 `auto_escape`
2. 若显式 `back`：弹栈，返回 NODE_SELECT
3. 若 `auto_escape`：
   - 从 context 获取当前 PageAnalysis
   - 收集未访问的同级菜单
   - 有 → 点击切换，**强制调用 vision 获取最新页面**，验证变化；成功则不弹栈回到 NODE_SELECT，失败重试 1 次后降级 back
   - 无 → back 弹栈

```python
def _handle_frame_complete_state(self, stack, context, action):
    from src.graph.node import ExitConditionType, FallbackAction
    from src.simulation.operation_executor import ExecutionContext
    from datetime import datetime

    current_node = stack.peek()
    fallback = FallbackAction.AUTO_ESCAPE if not current_node or not current_node.exit_condition \
        else current_node.exit_condition.fallback

    if fallback == FallbackAction.BACK:
        while stack.peek() and stack.peek().node_id != current_node.node_id:
            stack.pop()
        if stack.peek() and stack.peek().node_id == current_node.node_id:
            stack.pop()
        return TraversalState.NODE_SELECT

    # auto_escape
    page = getattr(context, 'current_page_analysis', None)
    if page is None:
        # 无页面信息，降级 back
        stack.pop()
        return TraversalState.NODE_SELECT

    menus = (page.level1_menus or []) + (page.level2_menus or [])
    unvisited = [
        m for m in menus
        if m.name not in context.visited_level1_menus
        and m.name not in context.visited_level2_menus
    ]

    if not unvisited:
        # 无未访问同级 → back
        stack.pop()
        self._last_handler_metrics = {"execution": {"action": "back", "status": "success"}}
        return TraversalState.NODE_SELECT

    # 尝试切换同级
    target_menu = unvisited[0]
    for attempt in range(2):
        ctx = ExecutionContext(current_node.node_id, current_node.name,
                               {"action": "click", "target": target_menu.name},
                               timestamp=datetime.now())
        action.execute(ctx)

        # 强制刷新页面分析，确保获取最新状态
        try:
            import time
            t1 = time.time()
            new_page = vision.analyze_screenshot(b"")
            elapsed = (time.time() - t1) * 1000
            if hasattr(context, 'current_page_analysis'):
                context.current_page_analysis = new_page

            # 验证页面变化
            if new_page and new_page.current_path != page.current_path:
                context.visited_level2_menus.add(target_menu.name)
                self._last_handler_metrics = {
                    "execution": {"action": "click", "target": target_menu.name, "status": "success"},
                    "ai_call": self._build_ai_call_metrics(new_page, elapsed, vision)
                }
                return TraversalState.NODE_SELECT  # 不弹栈
        except Exception:
            # Vision 失败，重试或降级
            continue

    # 切换失败 → 降级 back
    stack.pop()
    self._last_handler_metrics = {"execution": {"action": "back", "status": "success"}}
    return TraversalState.NODE_SELECT
```

---

### 5.3 `_handle_popup_state`

**签名不变**: `(self, stack, context, vision, action)`。

**流程**：从 `page.items` 中查找安全按钮文本 → 点击 → 回到 RESULT_VERIFY。找不到则 back。

```python
def _handle_popup_state(self, stack, context, vision, action):
    from src.simulation.operation_executor import ExecutionContext
    from datetime import datetime

    page = getattr(context, 'current_page_analysis', None)
    safe_keywords = ["取消", "关闭", "否", "忽略", "稍后", "Cancel", "Close", "No", "取消"]

    current_node = stack.peek()
    if page and page.items:
        for keyword in safe_keywords:
            for item in page.items:
                if keyword in (item.name or ""):
                    ctx = ExecutionContext(
                        current_node.node_id if current_node else "popup",
                        item.name,
                        {"action": "click", "target": item.name},
                        timestamp=datetime.now(),
                    )
                    action.execute(ctx)
                    self._last_handler_metrics = {"execution": {"action": "click", "target": keyword, "status": "success"}}
                    return TraversalState.RESULT_VERIFY

    # 无安全按钮 → back
    if current_node:
        ctx = ExecutionContext(current_node.node_id, current_node.name,
                               {"action": "back"}, timestamp=datetime.now())
        action.execute(ctx)
    self._last_handler_metrics = {"execution": {"action": "back", "status": "success"}}
    return TraversalState.RESULT_VERIFY
```

---

### 5.4 `_handle_error_state`

**签名不变**: `(self, stack, context, vision, action)`。

**流程**：读取节点 `ErrorPolicy` → 维护 `failed_nodes` 计数 → 决定动作。

```python
def _handle_error_state(self, stack, context, vision, action):
    current_node = stack.peek() if not stack.is_empty() else None
    error = context.last_error

    # Layer 1: 节点 ErrorPolicy
    if current_node and current_node.error_policy:
        policy = current_node.error_policy
        retry_count = context.failed_nodes.get(current_node.node_id, {}).get("retry_count", 0)

        if policy.on_error == "retry":
            if retry_count < policy.max_retries:
                context.failed_nodes[current_node.node_id] = {
                    "error_type": type(error).__name__ if error else "UnknownError",
                    "error_message": str(error) if error else "",
                    "retry_count": retry_count + 1,
                    "timestamp": datetime.now(),
                }
                self._last_handler_metrics = {"execution": {"action": "retry", "status": "initiated"}}
                return TraversalState.EXECUTE

        elif policy.on_error == "skip":
            self._last_handler_metrics = {"error": {"error_type": type(error).__name__, "error_message": str(error)}}
            return TraversalState.NODE_SELECT

        elif policy.on_error == "backtrack":
            self._last_handler_metrics = {"error": {"error_type": type(error).__name__, "error_message": str(error)}}
            return TraversalState.FRAME_COMPLETE

        elif policy.on_error == "abort":
            context.global_state = GlobalState.TERMINATED
            return TraversalState.BRANCH

    # 默认：skip
    if error and hasattr(context, 'consecutive_errors'):
        context.consecutive_errors += 1
    if error and hasattr(context, 'failed_nodes') and current_node:
        context.failed_nodes[current_node.node_id] = {
            "error_type": type(error).__name__ if error else "UnknownError",
            "error_message": str(error) if error else "",
            "timestamp": datetime.now(),
        }
    self._last_handler_metrics = {"error": {"error_type": type(error).__name__ if error else "UnknownError",
                                             "error_message": str(error) if error else ""}}
    return TraversalState.NODE_SELECT
```

---

## 6. 引擎集成：`step()` 异常处理

为确保 `context.last_error` 在进入 `ERROR_HANDLING` 状态前被正确设置，需要在 `step()` 方法中添加异常捕获机制。

### 6.1 `step()` Try-Catch 包装

```python
def step(
    self,
    stack: "NodeStack",
    context: "TraversalContext",
    vision: "VisionService",
    action: "ActionExecutor",
) -> TraversalStateTransition:
    from_state = self._state
    next_state = None
    metadata = {}
    node_id = self._current_node_id

    try:
        # State machine switch
        if from_state == TraversalState.NODE_SELECT:
            next_state = self._handle_node_select(stack, context)
            metadata["action"] = "select_node"

        elif from_state == TraversalState.PRECONDITION_CHECK:
            next_state = self._handle_precondition_check(stack, context, vision, action)
            metadata["action"] = "check_precondition"

        # ... other states ...

        else:
            raise ValueError(f"Unknown state: {from_state}")

    except Exception as e:
        # 捕获异常，设置 context.last_error，进入 ERROR_HANDLING
        context.last_error = e
        context.consecutive_errors = getattr(context, 'consecutive_errors', 0) + 1
        next_state = TraversalState.ERROR_HANDLING
        metadata["action"] = "error_caught"
        metadata["error_type"] = type(e).__name__

    # Perform transition
    if next_state:
        self.transition_to(next_state, node_id=self._current_node_id, **metadata)

    return TraversalStateTransition(
        from_state=from_state,
        to_state=next_state or from_state,
        node_id=self._current_node_id,
        metadata=metadata,
    )
```

### 6.2 `TraversalRuntimeContext` 字段确认

确保 `TraversalRuntimeContext` 包含以下字段（已存在）：

```python
@dataclass
class TraversalRuntimeContext:
    # ... 其他字段 ...

    last_error: Optional[Exception] = None  # ✅ 已存在
    consecutive_errors: int = 0            # ✅ 已存在
    failed_nodes: Dict[str, Dict[str, Any]] = field(default_factory=dict)  # ✅ 已存在
```

---

## 7. 分两期

| Phase A（本 PRD） | Phase B（V6.8+） |
|-------------------|-----------------|
| `classify_relation` 纯函数 | `_deep_recovery` 深度恢复 |
| 4 个 handler 重写（含 vision 刷新） | `generate_children` 动态子节点刷新 |
| `auto_escape` 同级切换 | AI 兜底（ERROR_HANDLING 调 advisor） |
| 弹窗关闭优先 | 全局 `ExceptionHandlingChain` |
| 节点级 ErrorPolicy | |
| `step()` 异常处理集成 | |

---

## 8. Handler 签名对齐

```python
# step() 中的调用 — _handle_precondition_check 新增 vision 参数
next_state = self._handle_precondition_check(stack, context, vision, action)

# 其他 handler 签名不变
next_state = self._handle_frame_complete_state(stack, context, action)
next_state = self._handle_popup_state(stack, context, vision, action)
next_state = self._handle_error_state(stack, context, vision, action)
next_state = self._handle_execute(stack, context, vision, action)
next_state = self._handle_result_verify(stack, context, vision)
next_state = self._handle_branch(stack, context)
next_state = self._handle_node_select(stack, context)
```

---

## 9. FallbackAction 新增值

```python
class FallbackAction(str, Enum):
    BACK = "back"
    AUTO_ESCAPE = "auto_escape"  # ← 新增
    SKIP = "skip"
    ABORT = "abort"
```

---

## 10. 测试用例

### 9.1 classify_relation

| 用例 | current_path | expected_page | menus | 预期结果 |
|------|-------------|---------------|-------|----------|
| 匹配 | `["设置","显示"]` | `"显示"` | - | MATCH |
| 可导航 | `["设置","显示"]` | `"声音"` | `["声音","网络"]` | NAVIGABLE |
| 走深了 | `["设置","显示","亮度"]` | `"显示"` | - | DEEPER |
| 迷失 | `["桌面"]` | `"显示"` | `[]` | UNKNOWN |
| 空路径 | `[]` | `"显示"` | - | UNKNOWN |

### 9.2 各 Handler

（见原 PRD 第 7 节测试用例，对齐后无变化）

---

## 11. 实施步骤

1. `FallbackAction` 加 `AUTO_ESCAPE`（已存在，确认即可）
2. `step()` 中添加 try-catch 异常处理（新增）
3. `step()` 中 `_handle_precondition_check` 调用加 `vision` 参数
4. 实现 `classify_relation`（纯函数，直接在 `traversal_fsm.py`）
5. 重写 `_handle_precondition_check`（含纠正后立即验证）
6. 重写 `_handle_frame_complete_state`（含 vision 强制刷新）
7. 重写 `_handle_popup_state`
8. 重写 `_handle_error_state`
9. 仿真验证
10. 全量回归

---

## 12. 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-07 | V6.7 | 初始版本 |
| 2026-06-07 | V6.7.1 | 添加 engine integration (step() 异常处理) |
| 2026-06-07 | V6.7.1 | 添加 precondition 纠正后立即验证 |
| 2026-06-07 | V6.7.1 | 添加 frame_complete vision 强制刷新 |
| 2026-06-07 | V6.7.1 | 添加 classify_relation "回退过头" 说明 |
| 2026-06-07 | V6.7.1 | 确认 TraversalRuntimeContext 字段存在 |
| 2026-06-07 | V6.7.1 | 添加 vision 调用时机与延迟说明 |
| 2026-06-07 | V6.7.1 | 添加 failed_nodes 辅助方法建议 |
| 2026-06-07 | V6.7.1 | 添加弹窗按钮匹配精度增强（可选） |
| 2026-06-07 | V6.7.1 | 添加已知限制章节 |

---

## 13. 实现注意事项

### 13.1 Vision 调用时机与延迟

为避免获取到过时的 UI 状态，在 `action.execute()` 后调用 `vision.analyze_screenshot()` 时应考虑：

1. **默认延迟**：添加 50-100ms 延迟，让 UI 动画完成
2. **可配置**：通过 `context.wait_after_action_ms` 控制延迟时长
3. **生产 vs 仿真**：仿真环境延迟可为 0，生产环境建议 100ms

```python
# 示例：在 action.execute() 后
delay_ms = getattr(context, 'wait_after_action_ms', 100)
if delay_ms > 0:
    time.sleep(delay_ms / 1000.0)
new_page = vision.analyze_screenshot(b"")
```

### 13.2 Vision 调用成本考虑

本 PRD 中的改进会增加 vision 调用次数：
- **precondition check**：每轮纠正后额外 1 次调用（最多 3 次）
- **frame_complete**：每次同级切换尝试 1 次调用（最多 2 次）

**成本收益分析**：
- 额外调用可减少不必要的重试和错误操作
- 错误导航导致的成本远高于 vision 调用
- 总体成本预计降低 10-20%（减少错误重试）

### 13.3 `failed_nodes` 辅助方法（可选）

为简化对 `context.failed_nodes` 的访问，可在 `TraversalRuntimeContext` 中添加辅助方法：

```python
def record_failure(self, node_id: str, error: Exception, retry_count: int = 0) -> None:
    """Record a node failure with retry tracking."""
    self.failed_nodes[node_id] = {
        "error_type": type(error).__name__ if error else "UnknownError",
        "error_message": str(error) if error else "",
        "retry_count": retry_count,
        "timestamp": datetime.now(),
    }

def get_retry_count(self, node_id: str) -> int:
    """Get retry count for a failed node."""
    return self.failed_nodes.get(node_id, {}).get("retry_count", 0)
```

这样 handler 代码可以更简洁：
```python
# 之前
context.failed_nodes[node_id] = {...}
retry_count = context.failed_nodes.get(node_id, {}).get("retry_count", 0)

# 之后
context.record_failure(node_id, error, retry_count)
retry_count = context.get_retry_count(node_id)
```

### 13.4 弹窗按钮匹配精度（可选增强）

当前 `_handle_popup_state` 通过 `keyword in item.name` 匹配按钮，可能误匹配（如 "取消订阅"）。

**可选增强**：优先匹配完整名称，其次匹配按钮类型：

```python
if page and page.items:
    for keyword in safe_keywords:
        for item in page.items:
            # 优先匹配完整名称
            if item.name == keyword:
                # Click this item
                return
            # 次选：按钮类型且包含关键词（如果 item.type 存在）
            if getattr(item, 'type', None) == 'button' and keyword in item.name:
                # Click this item
                return
```

**注意**：此增强依赖 `item.type` 字段的存在，需确认 `MenuItem` 模型支持。

---

## 14. 已知限制

| 限制 | 影响 | 后续版本 |
|------|------|----------|
| "回退过头"场景返回 UNKNOWN | 可能需要额外 back 恢复 | V6.8+ 深度恢复 |
| 弹窗按钮匹配可能误触 | 低概率错误点击 | V6.8+ 类型验证 |
| vision 调用增加 | 成本略有上升 | V6.8+ 缓存优化 |
