# V6.8 图模型与执行器初始化 PRD

**版本**: V6.8
**日期**: 2026-06-07
**依赖**: V6.7 state-machine-intelligence（已完成）
**修订**: V6.8.0 - 基于头脑风暴讨论优化的初始化流程

---

## 1. 背景

我们已有完整的数据模型（`TraversalPlan`、`TraversalNode`、`DynamicRule` 等）、Trace 系统、以及 V6.7 智能状态机。但引擎目前缺少"启动"环节——即如何从一份 `TraversalPlan` JSON 初始化引擎、执行入口策略、压入根节点并开始遍历。本 PRD 聚焦于补全这一链路，使引擎能端到端运行。

---

## 2. 目标

实现 `GraphTraversalEngine` 的初始化流程，包括：
1. **计划验证**：验证 `TraversalPlan` 配置的正确性（根节点、策略等）
2. **执行入口策略**：根据 `EntryPolicy` 将设备导航到目标应用（如"设置"），支持自动降级链
3. **等待条件验证**：验证入口成功后页面状态，支持快速/轮询两种模式
4. **初始化遍历上下文**：创建 `TraversalRuntimeContext`，注入 `session_id`，压入根节点
5. **启动主循环**：进入 `TRAVERSING` 状态，开始遍历状态机循环

---

## 3. 核心原则

| 原则 | 含义 |
|------|------|
| **自动降级** | 入口策略失败时自动尝试 fallback 策略，无需外部干预 |
| **可配置性** | 等待模式、Trace 级别等支持配置，适应不同场景 |
| **明确异常** | 区分可恢复/不可恢复错误，抛出特定异常类型 |
| **完整验证** | 初始化时验证根节点类型、前置条件等关键配置 |
| **Trace 可配置** | 支持 minimal/standard/detailed 三级 Trace 记录 |

---

## 4. 完整初始化流程

```mermaid
sequenceDiagram
    participant Engine as GraphTraversalEngine
    participant Plan as TraversalPlan
    participant Vision as VisionService
    participant Action as ActionExecutor
    participant Stack as NodeStack
    participant Recorder as TraceRecorder

    Engine->>Engine: 1. 状态转移: IDLE → INITIALIZING
    Engine->>Recorder: 2. 创建 Session，初始化 TraceRecorder
    
    Engine->>Plan: 3. 验证计划配置
    alt root_node 为 None
        Plan-->>Engine: 抛出 ConfigurationError
    else root_node 类型非 CONTAINER
        Plan-->>Engine: 抛出 ConfigurationError
    end
    
    Engine->>Engine: 4. 构建策略降级链
    Note over Engine: strategy → fallback → bind_current_screen
    
    loop 遍历策略链
        Engine->>Action: 5. 执行入口策略
        alt direct_deeplink
            Engine->>Action: 发送深度链接
        else cold_launch
            Engine->>Action: 返回桌面
            Engine->>Vision: 分析桌面，查找目标图标
            Engine->>Action: 点击目标应用图标
        else bind_current_screen
            Engine->>Vision: 检查当前屏幕是否匹配
        end
        
        Engine->>Vision: 6. 验证等待条件
        alt 快速模式
            Engine->>Vision: 单次 vision 调用
        else 轮询模式
            loop 直到满足或超时
                Engine->>Vision: 调用 vision 检查
            end
        end
        
        alt 验证成功
            Engine->>Recorder: 记录成功 Span
            break
        else 验证失败
            Engine->>Recorder: 记录失败 Span
            Engine->>Engine: 尝试下一个策略
        end
    end
    
    alt 全部策略失败
        Engine-->>Engine: 抛出 EntryPolicyError
    end
    
    Engine->>Stack: 7. 压入根节点并验证
    Engine->>Recorder: 初始化 StepTracker，记录 StepNode
    
    Engine->>Engine: 8. 状态转移: INITIALIZING → TRAVERSING
    Engine->>Engine: 9. 启动主循环
```

---

## 5. 详细设计

### 5.1 计划验证

初始化时验证 `TraversalPlan` 的关键配置：

```python
def _validate_plan(self) -> None:
    """验证计划配置"""
    if not self.plan.root_node:
        raise ConfigurationError("root_node is required in traversal plan")
    
    root = self.plan.root_node
    
    # 类型检查
    if root.node_type != NodeType.CONTAINER:
        raise ConfigurationError(
            f"Root node must be CONTAINER type, got {root.node_type.value}"
        )
    
    # 操作检查
    if root.operation and root.operation.action != "no_action":
        raise ConfigurationError(
            f"Root node operation should be 'no_action', got {root.operation.action}"
        )
```

### 5.2 入口策略执行（自动降级链）

```python
def _execute_entry_policy(self) -> None:
    """执行入口策略（自动降级链）"""
    policy = self.plan.entry_policy or EntryPolicy()
    strategies = self._build_strategy_chain(policy)
    
    last_error = None
    
    for strategy in strategies:
        try:
            self._execute_single_strategy(strategy)
            
            # 验证入口是否成功
            if self._verify_entry_success():
                self._record_entry_success(strategy)
                return
            else:
                self._record_entry_failure(strategy, "Verification failed")
                
        except Exception as e:
            last_error = e
            self._record_entry_failure(strategy, str(e))
            continue
    
    # 所有策略失败
    raise EntryPolicyError(
        f"All entry strategies failed: {[s.value for s in strategies]}. "
        f"Last error: {last_error}"
    )

def _build_strategy_chain(self, policy: EntryPolicy) -> List[EntryStrategy]:
    """构建策略降级链"""
    strategies = [policy.strategy]
    
    # 添加 fallback（如果不同）
    if policy.fallback and policy.fallback != policy.strategy:
        strategies.append(policy.fallback)
    
    # 最终兜底：bind_current_screen
    if EntryStrategy.BIND_CURRENT_SCREEN not in strategies:
        strategies.append(EntryStrategy.BIND_CURRENT_SCREEN)
    
    return strategies

def _execute_single_strategy(self, strategy: EntryStrategy) -> None:
    """执行单个入口策略"""
    if strategy == EntryStrategy.DIRECT_DEEPLINK:
        self._execute_deeplink_strategy()
    elif strategy == EntryStrategy.COLD_LAUNCH:
        self._execute_cold_launch_strategy()
    elif strategy == EntryStrategy.BIND_CURRENT_SCREEN:
        self._execute_bind_current_screen_strategy()

def _execute_deeplink_strategy(self) -> None:
    """执行深度链接策略"""
    deeplink = f"{self.plan.entry_app}://"
    self.action_executor.execute(ExecutionContext(
        node_id="entry_policy",
        name="entry_deeplink",
        operation={"action": "send_deeplink", "params": {"url": deeplink}},
        timestamp=datetime.now(),
    ))

def _execute_cold_launch_strategy(self) -> None:
    """执行冷启动策略"""
    # 1. 返回桌面
    self.action_executor.execute(ExecutionContext(
        node_id="entry_policy",
        name="entry_home",
        operation={"action": "press_home"},
        timestamp=datetime.now(),
    ))
    
    # 2. 等待 UI 稳定
    time.sleep(self._get_action_delay())
    
    # 3. 分析桌面
    page = self.vision_service.analyze_screenshot(b"")
    
    # 4. 查找目标应用图标
    target_item = self._find_app_icon(page, self.plan.entry_app)
    
    if not target_item:
        raise EntryError(f"App icon not found on home screen: {self.plan.entry_app}")
    
    # 5. 点击图标
    self.action_executor.execute(ExecutionContext(
        node_id="entry_policy",
        name="entry_click_app",
        operation={"action": "click", "target": target_item.coordinate},
        timestamp=datetime.now(),
    ))

def _execute_bind_current_screen_strategy(self) -> None:
    """执行绑定当前屏幕策略（假设已在目标应用内）"""
    # 不执行任何操作，直接进入验证阶段
    pass

def _find_app_icon(self, page: PageAnalysis, app_name: str) -> Optional[Dict]:
    """在桌面元素中查找应用图标

    EXTENSION POINT: 当前实现仅做简单名称匹配。
    未来可扩展：
    - 桌面翻页搜索（多页场景）
    - 文件夹展开查找（应用在文件夹中）
    - AI 辅助定位（视觉相似度匹配）

    限制：如果桌面有多页或应用在文件夹中，当前方法会失败。
    """
    for item in page.items or []:
        if app_name.lower() in (item.name or "").lower():
            return {"coordinate": item.coordinate, "name": item.name}
    return None
```

### 5.3 等待条件验证（可配置）

```python
def _verify_entry_success(self) -> bool:
    """验证入口是否成功"""
    policy = self.plan.entry_policy or EntryPolicy()
    wait_condition = policy.wait_condition

    if not wait_condition:
        return True

    # 获取等待模式（优先使用 entry_config）
    if self.plan.entry_config:
        wait_mode = self.plan.entry_config.wait_mode
    else:
        wait_mode = self.plan.meta.get("entry_wait_mode", "fast")

    if wait_mode == "fast":
        return self._verify_condition_once(wait_condition)
    else:  # polling
        if self.plan.entry_config:
            timeout = self.plan.entry_config.wait_timeout
            interval = self.plan.entry_config.wait_interval
        else:
            timeout = self.plan.meta.get("entry_wait_timeout", 10)
            interval = self.plan.meta.get("entry_wait_interval", 1)
        return self._verify_condition_polling(wait_condition, timeout, interval)

def _verify_condition_once(self, condition: Precondition) -> bool:
    """单次验证条件（快速模式）"""
    time.sleep(self._get_action_delay())
    page = self.vision_service.analyze_screenshot(b"")

    if condition.get("page_name"):
        return page.current_path and page.current_path[-1] == condition["page_name"]

    return True

def _verify_condition_polling(
    self, condition: Precondition, timeout: int, interval: int
) -> bool:
    """轮询验证条件（轮询模式）"""
    start_time = time.time()

    while time.time() - start_time < timeout:
        time.sleep(self._get_action_delay())
        page = self.vision_service.analyze_screenshot(b"")

        page_name = condition.get("page_name")
        if page_name:
            if page.current_path and page.current_path[-1] == page_name:
                return True

        time.sleep(interval)

    return False

def _get_action_delay(self) -> float:
    """获取动作后延迟（秒）"""
    if self.plan.entry_config:
        delay_ms = self.plan.entry_config.action_delay_ms
    else:
        delay_ms = self.plan.meta.get("action_delay_ms", 100)
    return delay_ms / 1000.0
```

### 5.4 根节点处理

```python
def _validate_and_push_root_node(self, session: Session) -> None:
    """验证根节点并压入栈"""
    if not self.plan.root_node:
        raise ConfigurationError("root_node is required in traversal plan")
    
    root = self.plan.root_node
    
    # 类型检查
    if root.node_type != NodeType.CONTAINER:
        raise ConfigurationError(
            f"Root node must be CONTAINER type, got {root.node_type.value}"
        )
    
    # 压入栈
    self._push_node(root.node_id)
    
    # 初始化 StepTracker
    self._initialize_root_step(root, session)
    
    # 记录 Trace
    self._record_root_node_pushed(root)

def _initialize_root_step(self, root: TraversalNode, session: Session) -> None:
    """初始化根节点步骤"""
    root_step = StepNode(
        trace_id=self._generate_span_id(),
        session_id=session.session_id,
        step_id=1,
        node_id=root.node_id,
        node_type=root.node_type.value,
        page_path=[],
    )
    
    parent_id = self.trace_recorder.step_tracker.on_step_start(root_step.trace_id)
    self.trace_recorder.record_step_start(root_step, parent_id or session.session_id)
```

### 5.5 异常类型定义

```python
class InitializationError(Exception):
    """初始化错误基类"""
    def __init__(self, message: str, recoverable: bool = True):
        super().__init__(message)
        self.recoverable = recoverable

class ConfigurationError(InitializationError):
    """计划配置错误（不可恢复）"""
    def __init__(self, message: str):
        super().__init__(message, recoverable=False)

class EntryPolicyError(InitializationError):
    """入口策略失败（可恢复）"""
    def __init__(self, message: str, last_error: Optional[Exception] = None):
        super().__init__(message, recoverable=True)
        self.last_error = last_error

class WaitConditionError(InitializationError):
    """等待条件失败（可恢复）"""
    def __init__(self, message: str):
        super().__init__(message, recoverable=True)

class EntryError(Exception):
    """入口策略执行错误"""
    pass
```

### 5.6 EntryConfig 数据类

为获得类型安全和 IDE 提示，引入 `EntryConfig` 数据类：

```python
@dataclass
class EntryConfig:
    """
    入口策略配置。

    定义入口策略执行和验证的详细参数。
    """
    # 等待模式
    wait_mode: str = "fast"  # "fast"（单次检查）或 "polling"（轮询）

    # 轮询参数
    wait_timeout: int = 10  # 轮询超时（秒）
    wait_interval: int = 1  # 轮询间隔（秒）

    # 动作延迟
    action_delay_ms: int = 100  # 动作后延迟（毫秒）

    # Trace 级别
    trace_level: str = "standard"  # "minimal" / "standard" / "detailed"

    def __post_init__(self):
        """验证配置"""
        valid_modes = {"fast", "polling"}
        if self.wait_mode not in valid_modes:
            raise ValueError(f"wait_mode must be one of {valid_modes}, got {self.wait_mode}")

        valid_levels = {"minimal", "standard", "detailed"}
        if self.trace_level not in valid_levels:
            raise ValueError(f"trace_level must be one of {valid_levels}, got {self.trace_level}")

        if self.wait_timeout <= 0:
            raise ValueError(f"wait_timeout must be positive, got {self.wait_timeout}")

        if self.wait_interval <= 0:
            raise ValueError(f"wait_interval must be positive, got {self.wait_interval}")
```

在 `TraversalPlan` 中添加 `entry_config` 字段：

```python
class TraversalPlan:
    # ... 其他字段 ...
    entry_config: Optional[EntryConfig] = None  # 入口策略配置
```

### 5.7 Trace 级别配置

```python
class TraceLevel(str, Enum):
    MINIMAL = "minimal"    # 只记录关键状态转移
    STANDARD = "standard"  # 记录策略尝试和结果
    DETAILED = "detailed"  # 记录所有 vision 调用和重试

def _get_trace_level(self) -> TraceLevel:
    """获取 Trace 记录级别"""
    if self.plan.entry_config and self.plan.entry_config.trace_level:
        level_str = self.plan.entry_config.trace_level
    else:
        level_str = self.plan.meta.get("trace_level", "standard")

    return TraceLevel.from_value(level_str) if isinstance(level_str, str) else level_str

def _should_record_entry_attempt(self) -> bool:
    """是否记录入口策略尝试"""
    level = self._get_trace_level()
    return level in (TraceLevel.STANDARD, TraceLevel.DETAILED)

def _should_record_vision_call(self) -> bool:
    """是否记录 vision 调用详情"""
    level = self._get_trace_level()
    return level == TraceLevel.DETAILED
```

**性能提示**：
- `minimal` 模式：仅记录关键状态转移，适用于生产环境
- `standard` 模式：记录策略尝试和结果，适用于大多数场景
- `detailed` 模式：记录所有 vision 调用和重试，**仅用于调试**。在一次遍历可能产生数百次 vision 调用的情况下，Trace 文件会迅速膨胀，生产环境不建议使用。

---

## 6. 与现有组件集成

- **状态机 Handler**：已有 V6.7 的智能 Handler，直接调用。引擎负责注入 `vision` 和 `action`。
- **Trace 记录**：`_record_metrics_as_spans` 已在 V6.6 中实现，无需修改。
- **StepTracker**：管理步骤栈，提供 `parent_span_id`。已在 V6.5 中实现。
- **NodeStack**：已实现，引擎初始化时压入根节点。

---

## 7. 测试用例

### 7.1 入口策略

| 用例 | 策略 | 模拟条件 | 预期结果 |
|------|------|----------|----------|
| deeplink 成功 | `direct_deeplink` | 视觉分析返回目标页面 | 直接进入目标应用 |
| cold_launch 成功 | `cold_launch` | 桌面元素包含目标应用图标 | 点击图标后进入应用 |
| 降级链 | `deeplink` 失败 → `cold_launch` | deeplink 不支持 | 自动降级到冷启动 |
| 全部失败 | 所有策略失败 | 桌面无目标图标 | 抛出 EntryPolicyError |

### 7.2 等待条件验证

| 用例 | 模式 | 模拟条件 | 预期结果 |
|------|------|----------|----------|
| 快速模式成功 | `fast` | vision 返回目标页面 | 单次检查后返回 True |
| 快速模式失败 | `fast` | vision 返回其他页面 | 单次检查后返回 False |
| 轮询模式成功 | `polling` | 2 秒后到达目标页面 | 循环检查直到超时前返回 True |
| 轮询模式超时 | `polling` | 始终未到达目标页面 | 超时后返回 False |

### 7.3 引擎初始化

| 用例 | 计划配置 | 预期行为 |
|------|----------|----------|
| 正常初始化 | root_node 存在且类型正确 | 成功压入根节点，进入 TRAVERSING |
| 无根节点 | `root_node = null` | 抛出 ConfigurationError |
| 根节点类型错误 | `node_type = LEAF` | 抛出 ConfigurationError |
| StepTracker 初始化 | 正常计划 | 记录 StepNode，step_id = 1 |

### 7.4 异常处理

| 用例 | 错误场景 | 预期异常 | 可恢复性 |
|------|----------|----------|----------|
| 配置错误 | root_node 为 None | ConfigurationError | 不可恢复 |
| 入口失败 | 所有策略失败 | EntryPolicyError | 可恢复 |
| 等待超时 | 轮询模式超时 | WaitConditionError | 可恢复 |

---

## 8. 实施步骤

### Phase A：数据模型扩展

1. 在 `src/graph/node.py` 中添加 `EntryConfig` 数据类
2. 在 `TraversalPlan` 中添加 `entry_config` 字段
3. 更新 `TraversalPlan.to_json()` 和 `from_json()` 方法
4. 添加单元测试验证 EntryConfig 序列化/反序列化

### Phase B：基础实现

5. 在 `GraphTraversalEngine` 中添加异常类型定义
6. 实现 `_validate_plan()` 计划验证方法
7. 实现 `_build_strategy_chain()` 策略链构建方法
8. 实现 `_execute_entry_policy()` 入口策略执行框架
9. 实现 `_verify_entry_success()` 等待条件验证框架（支持 entry_config 和 meta）

### Phase C：策略实现

10. 实现 `_execute_deeplink_strategy()` 深度链接策略
11. 实现 `_execute_cold_launch_strategy()` 冷启动策略
12. 实现 `_execute_bind_current_screen_strategy()` 绑定屏幕策略
13. 实现 `_find_app_icon()` 应用图标查找方法（含扩展点注释）
14. 实现等待条件的快速模式和轮询模式

### Phase D：根节点处理

15. 实现 `_validate_and_push_root_node()` 根节点验证和压入
16. 实现 `_initialize_root_step()` StepTracker 初始化
17. 实现 Trace 级别配置和记录（含性能提示）

### Phase E：测试验证

18. 单元测试：EntryConfig 序列化/反序列化
19. 单元测试：入口策略各场景
20. 单元测试：等待条件验证各场景（含 entry_config 和 meta）
21. 单元测试：根节点验证各场景
22. 单元测试：异常处理各场景
23. 仿真测试：完整初始化流程
24. 仿真测试：入口策略降级链
25. 全量回归测试

---

## 9. 配置示例

### 9.1 完整 TraversalPlan 示例

```json
{
  "entry_app": "com.android.settings",
  "entry_policy": {
    "strategy": "direct_deeplink",
    "fallback": "cold_launch",
    "wait_condition": {
      "page_name": "设置"
    }
  },
  "entry_config": {
    "wait_mode": "fast",
    "wait_timeout": 10,
    "wait_interval": 1,
    "action_delay_ms": 100,
    "trace_level": "standard"
  },
  "root_node": {
    "node_id": "root",
    "name": "设置根节点",
    "node_type": "container",
    "operation": {
      "action": "no_action"
    },
    "children_strategy": {
      "type": "static",
      "static_children": ["wifi", "bluetooth", "display"]
    }
  },
  "completion_policy": {
    "type": "max_steps",
    "max_steps": 100
  }
}
```

**注意**：推荐使用 `entry_config` 字段进行类型安全的配置。若使用 `meta` 字典，键名需与 `EntryConfig` 字段一致。

### 9.2 EntryConfig 字段说明

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `wait_mode` | str | `"fast"` | 等待模式：`"fast"`（单次检查）/ `"polling"`（轮询） |
| `wait_timeout` | int | `10` | 轮询模式超时时间（秒） |
| `wait_interval` | int | `1` | 轮询模式检查间隔（秒） |
| `action_delay_ms` | int | `100` | 动作后延迟（毫秒） |
| `trace_level` | str | `"standard"` | Trace 记录级别：`"minimal"` / `"standard"` / `"detailed"` |

### 9.3 meta 字典键名约定（向后兼容）

| 键名 | 默认值 | 说明 |
|------|--------|------|
| `entry_wait_mode` | `"fast"` | 等待模式（当 `entry_config` 不存在时使用） |
| `entry_wait_timeout` | `10` | 轮询超时（秒） |
| `entry_wait_interval` | `1` | 轮询间隔（秒） |
| `action_delay_ms` | `100` | 动作延迟（毫秒） |
| `trace_level` | `"standard"` | Trace 级别 |

---

## 10. 修订记录

| 日期 | 版本 | 修订内容 |
|------|------|----------|
| 2026-06-07 | V6.8.0 | 初始版本，基于头脑风暴讨论优化 |
| 2026-06-07 | V6.8.0 | 添加自动降级链设计 |
| 2026-06-07 | V6.8.0 | 添加可配置等待验证 |
| 2026-06-07 | V6.8.0 | 添加异常类型定义 |
| 2026-06-07 | V6.8.0 | 添加 Trace 级别配置 |
| 2026-06-07 | V6.8.0 | 添加根节点验证和 StepTracker 初始化 |
| 2026-06-07 | V6.8.1 | **架构 Review 更新** |
| 2026-06-07 | V6.8.1 | 添加 `_find_app_icon` 扩展点注释和已知限制 |
| 2026-06-07 | V6.8.1 | 引入 `EntryConfig` 数据类替代 `meta` 字符串键 |
| 2026-06-07 | V6.8.1 | 添加 Trace 性能提示（detailed 仅用于调试） |
| 2026-06-07 | V6.8.1 | 确认现有代码对齐（NodeType、StepTracker、Trace、Context） |
| 2026-06-07 | V6.8.1 | 更新配置示例使用 `entry_config` |

---

## 11. 已知限制

| 限制 | 影响 | 后续版本 |
|------|------|----------|
| 冷启动应用查找过于简化 | 多页桌面或文件夹中应用会失败 | V6.9+ AI 辅助定位 |
| 深度链接依赖应用支持 | 部分应用不支持 deeplink | 自动降级到 cold_launch |
| 轮询模式固定间隔 | 可能错过快速变化的 UI | 可配置动态间隔 |
| detailed Trace 模式性能影响 | Trace 文件迅速膨胀 | 生产环境使用 standard |
