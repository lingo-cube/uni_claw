# 异常处理与状态机集成设计

> 统一的异常处理机制，确保状态机在异常情况下能够正确恢复

---

## 一、异常分类体系

### 1.1 异常类型定义

```python
class TraversalException(Exception):
    """遍历异常基类"""
    pass

class TraversalError(TraversalException):
    """遍历错误（非致命）"""
    pass

# === 定位异常 ===
class LocationException(TraversalException):
    """定位相关异常基类"""
    pass

class ElementNotFoundException(LocationException):
    """元素未找到"""
    pass

class PathMismatchException(LocationException):
    """路径不匹配"""
    pass

class CoordinateExpiredException(LocationException):
    """坐标失效（元素已移动）"""
    pass

# === 操作异常 ===
class OperationException(TraversalException):
    """操作相关异常基类"""
    pass

class ClickFailedException(OperationException):
    """点击失败（无响应）"""
    pass

class InputFailedException(OperationException):
    """输入失败"""
    pass

class SwipeFailedException(OperationException):
    """滑动失败"""
    pass

# === 设备异常 ===
class DeviceException(TraversalException):
    """设备相关异常基类"""
    pass

class ADBDisconnectedException(DeviceException):
    """ADB 连接断开"""
    pass

class AppCrashException(DeviceException):
    """APP 崩溃"""
    pass

class DeviceOfflineException(DeviceException):
    """设备离线"""
    pass

# === 界面异常 ===
class UIException(TraversalException):
    """界面相关异常基类"""
    pass

class PopupDetectedException(UIException):
    """检测到弹窗"""
    pass

class PageRedirectException(UIException):
    """页面跳转"""
    pass

class LoadingTimeoutException(UIException):
    """加载超时"""
    pass

# === AI 异常 ===
class AIException(TraversalException):
    """AI 分析相关异常基类"""
    pass

class AIAnalysisFailedException(AIException):
    """AI 分析失败"""
    pass

class AIResponseInvalidException(AIException):
    """AI 响应无效"""
    pass
```

### 1.2 异常严重级别

```python
class ExceptionSeverity(Enum):
    """异常严重级别"""
    INFO = "info"           # 信息级（可忽略）
    WARNING = "warning"     # 警告级（需记录）
    ERROR = "error"         # 错误级（需重试）
    CRITICAL = "critical"   # 严重级（需回退）
    FATAL = "fatal"         # 致命级（终止遍历）
```

### 1.3 异常元数据

```python
@dataclass
class ExceptionContext:
    """异常上下文信息"""
    exception: TraversalException
    severity: ExceptionSeverity
    state: TraversalState          # 发生异常时的状态
    node: Optional[TreeNode]      # 发生异常时的节点
    operation: str                # 正在执行的操作
    timestamp: datetime
    screenshot: Optional[bytes]   # 异常时的截图
    ai_result: Optional[dict]     # AI 分析结果
    retry_count: int              # 已重试次数
```

---

## 二、状态机异常处理框架

### 2.1 异常处理器接口

```python
class ExceptionHandler(ABC):
    """异常处理器基类"""

    @abstractmethod
    def can_handle(self, context: ExceptionContext) -> bool:
        """判断是否能处理该异常"""
        pass

    @abstractmethod
    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """处理异常，返回处理结果"""
        pass

@dataclass
class ExceptionHandlingResult:
    """异常处理结果"""
    action: ExceptionAction
    new_state: Optional[TraversalState]
    message: str

class ExceptionAction(Enum):
    """异常处理动作"""
    RETRY = "retry"              # 重试当前操作
    SKIP = "skip"                # 跳过当前元素
    BACKTRACK = "backtrack"      # 回退到上级
    RECOVER = "recover"          # 尝试恢复
    TERMINATE = "terminate"      # 终止遍历
    IGNORE = "ignore"            # 忽略继续
```

### 2.2 内置异常处理器

```python
class RetryHandler(ExceptionHandler):
    """重试处理器"""

    def __init__(self, max_retries: int = 3):
        self.max_retries = max_retries

    def can_handle(self, context: ExceptionContext) -> bool:
        """可重试的异常"""
        return (
            context.severity in [ExceptionSeverity.ERROR, ExceptionSeverity.WARNING]
            and context.retry_count < self.max_retries
        )

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行重试"""
        context.retry_count += 1
        return ExceptionHandlingResult(
            action=ExceptionAction.RETRY,
            new_state=context.state,
            message=f"重试 {context.retry_count}/{self.max_retries}"
        )


class BacktrackHandler(ExceptionHandler):
    """回退处理器"""

    def can_handle(self, context: ExceptionContext) -> bool:
        """需要回退的异常"""
        return (
            context.severity in [ExceptionSeverity.CRITICAL, ExceptionSeverity.ERROR]
            and context.retry_count >= self.max_retries
        )

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行回退"""
        if context.node:
            context.node.state = NodeState.FAILED
            context.node.last_error = str(context.exception)

        return ExceptionHandlingResult(
            action=ExceptionAction.BACKTRACK,
            new_state=TraversalState.RECOVERING,
            message=f"回退到上级分支"
        )


class DeviceExceptionHandler(ExceptionHandler):
    """设备异常处理器"""

    def can_handle(self, context: ExceptionContext) -> bool:
        """设备相关异常"""
        return isinstance(context.exception, DeviceException)

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """处理设备异常"""
        if isinstance(context.exception, ADBDisconnectedException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.RECOVERING,
                message="尝试重新连接 ADB"
            )
        elif isinstance(context.exception, AppCrashException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.RECOVERING,
                message="尝试重启 APP"
            )
        return ExceptionHandlingResult(
            action=ExceptionAction.TERMINATE,
            new_state=TraversalState.ERROR,
            message="设备异常无法恢复"
        )


class UIExceptionHandler(ExceptionHandler):
    """界面异常处理器"""

    def can_handle(self, context: ExceptionContext) -> bool:
        """界面相关异常"""
        return isinstance(context.exception, UIException)

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """处理界面异常"""
        if isinstance(context.exception, PopupDetectedException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.HANDLING_POPUP,
                message="处理弹窗"
            )
        elif isinstance(context.exception, PageRedirectException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.HANDLING_REDIRECT,
                message="处理页面跳转"
            )
        return ExceptionHandlingResult(
            action=ExceptionAction.IGNORE,
            new_state=context.state,
            message="界面变化，继续执行"
        )


class FatalExceptionHandler(ExceptionHandler):
    """致命异常处理器"""

    def can_handle(self, context: ExceptionContext) -> bool:
        """致命异常"""
        return context.severity == ExceptionSeverity.FATAL

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """终止遍历"""
        return ExceptionHandlingResult(
            action=ExceptionAction.TERMINATE,
            new_state=TraversalState.ERROR,
            message=f"致命异常: {context.exception}"
        )
```

### 2.3 异常处理链

```python
class ExceptionHandlingChain:
    """异常处理链"""

    def __init__(self):
        self.handlers: List[ExceptionHandler] = [
            FatalExceptionHandler(),
            DeviceExceptionHandler(),
            UIExceptionHandler(),
            RetryHandler(max_retries=3),
            BacktrackHandler(max_retries=3),
        ]

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """使用处理链处理异常"""
        for handler in self.handlers:
            if handler.can_handle(context):
                result = handler.handle(context)
                logger.info(f"异常处理: {handler.__class__.__name__} -> {result.action}")
                return result

        # 默认处理
        return ExceptionHandlingResult(
            action=ExceptionAction.TERMINATE,
            new_state=TraversalState.ERROR,
            message="未知异常，终止遍历"
        )
```

---

## 三、状态机集成异常处理

### 3.1 增强的状态机

```python
class HierarchicalStateMachine:
    """融合异常处理的状态机"""

    def __init__(self):
        # ... 原有字段 ...

        # 异常处理
        self.exception_chain = ExceptionHandlingChain()
        self.exception_history: List[ExceptionContext] = []

    def execute_with_exception_handling(
        self,
        operation: Callable,
        context: dict = None
    ) -> Any:
        """执行操作并处理异常"""
        max_attempts = 4  # 1次初始 + 3次重试

        for attempt in range(max_attempts):
            try:
                # 执行操作
                return operation()

            except TraversalException as e:
                # 构建异常上下文
                exc_context = ExceptionContext(
                    exception=e,
                    severity=self._get_severity(e),
                    state=self.global_state,
                    node=self.current_node,
                    operation=context.get("operation", "unknown") if context else "unknown",
                    timestamp=datetime.now(),
                    retry_count=attempt,
                )

                # 记录异常
                self.exception_history.append(exc_context)

                # 异常处理
                result = self.exception_chain.handle(exc_context)

                # 根据处理结果执行相应动作
                if result.action == ExceptionAction.RETRY:
                    logger.info(result.message)
                    continue

                elif result.action == ExceptionAction.SKIP:
                    self._skip_current_node()
                    return None

                elif result.action == ExceptionAction.BACKTRACK:
                    self._backtrack()
                    return None

                elif result.action == ExceptionAction.RECOVER:
                    self._recover(result.new_state)
                    continue

                elif result.action == ExceptionAction.IGNORE:
                    return None

                elif result.action == ExceptionAction.TERMINATE:
                    self.global_state = TraversalState.ERROR
                    raise

        # 超过最大重试次数
        self.global_state = TraversalState.ERROR
        raise TraversalException(f"操作失败，已重试 {max_attempts} 次")

    def _get_severity(self, exception: TraversalException) -> ExceptionSeverity:
        """获取异常严重级别"""
        severity_map = {
            ElementNotFoundException: ExceptionSeverity.ERROR,
            PathMismatchException: ExceptionSeverity.CRITICAL,
            ADBDisconnectedException: ExceptionSeverity.CRITICAL,
            AppCrashException: ExceptionSeverity.CRITICAL,
            PopupDetectedException: ExceptionSeverity.INFO,
            PageRedirectException: ExceptionSeverity.INFO,
            AIAnalysisFailedException: ExceptionSeverity.ERROR,
        }
        return severity_map.get(type(exception), ExceptionSeverity.ERROR)

    def _skip_current_node(self):
        """跳过当前节点"""
        if self.current_node:
            self.current_node.state = NodeState.SKIPPED
            self.backtrack_to_continuable_level()

    def _backtrack(self):
        """回退到可继续的位置"""
        self.backtrack_to_continuable_level()

    def _recover(self, target_state: TraversalState):
        """执行恢复操作"""
        self.global_state = target_state
        # 具体的恢复逻辑由对应状态的处理器执行
```

### 3.2 操作执行包装

```python
    def click_node(self, node: TreeNode) -> bool:
        """点击节点（带异常处理）"""
        def _click():
            # 1. 验证节点状态
            if node.state != NodeState.PENDING:
                raise TraversalError(f"节点状态不正确: {node.state}")

            # 2. 获取坐标
            if not node.coordinates:
                # 调用 AI 获取坐标
                coords = self._get_coordinates_via_ai(node.name)
                if not coords:
                    raise ElementNotFoundException(f"找不到元素: {node.name}")
                node.coordinates = coords

            # 3. 执行点击
            x, y = node.coordinates
            self.adb.tap(x, y)

            # 4. 等待响应
            time.sleep(self.WAIT_TIME)

            return True

        # 带异常处理执行
        return self.execute_with_exception_handling(
            _click,
            context={"operation": f"click {node.name}"}
        )

    def navigate_to_path(self, path: List[str]) -> bool:
        """导航到指定路径（带异常处理）"""
        def _navigate():
            # 查找目标节点
            target = self._find_node_by_path(path)
            if not target:
                raise ElementNotFoundException(f"找不到路径: {'/'.join(path)}")

            # 计算路径差异
            current_path_names = [n.name for n in self.current_path]
            target_path_names = [n.name for n in self._get_path_to_node(target)]

            # 逐级导航
            for i, target_name in enumerate(target_path_names):
                if i < len(current_path_names):
                    if current_path_names[i] != target_name:
                        # 路径不匹配，需要回退
                        raise PathMismatchException(
                            f"路径不匹配: {current_path_names[i]} != {target_name}"
                        )
                else:
                    # 向下导航
                    target_node = self._find_node_by_path(target_path_names[:i+1])
                    if not target_node:
                        raise ElementNotFoundException(f"找不到节点: {target_name}")

                    if not self.click_node(target_node):
                        return False

            return True

        return self.execute_with_exception_handling(
            _navigate,
            context={"operation": f"navigate to {'/'.join(path)}"}
        )
```

---

## 四、异常恢复策略

### 4.1 恢复动作定义

```python
class RecoveryAction(ABC):
    """恢复动作基类"""

    @abstractmethod
    def execute(self, context: ExceptionContext) -> bool:
        """执行恢复动作"""
        pass


class ADBReconnectAction(RecoveryAction):
    """ADB 重新连接"""

    def execute(self, context: ExceptionContext) -> bool:
        """重新连接 ADB"""
        try:
            context.state_machine.adb.kill_server()
            context.state_machine.adb.start_server()
            return context.state_machine.adb.is_connected()
        except Exception:
            return False


class AppRestartAction(RecoveryAction):
    """APP 重启"""

    def execute(self, context: ExceptionContext) -> bool:
        """重启 APP"""
        try:
            # 启动 APP
            context.state_machine.adb.start_app(context.state_machine.app_package)
            # 恢复位置
            return context.state_machine.restore_position()
        except Exception:
            return False


class PositionRestoreAction(RecoveryAction):
    """位置恢复"""

    def execute(self, context: ExceptionContext) -> bool:
        """恢复到之前的位置"""
        # 使用保存的路径重新导航
        saved_path = context.state_machine.get_saved_path()
        return context.state_machine.navigate_to_path(saved_path)
```

### 4.2 恢复管理器

```python
class RecoveryManager:
    """恢复管理器"""

    def __init__(self):
        self.recovery_actions: Dict[Type[TraversalException], RecoveryAction] = {
            ADBDisconnectedException: ADBReconnectAction(),
            AppCrashException: AppRestartAction(),
            PathMismatchException: PositionRestoreAction(),
        }

    def recover(self, context: ExceptionContext) -> bool:
        """执行恢复"""
        exc_type = type(context.exception)
        action = self.recovery_actions.get(exc_type)

        if action:
            logger.info(f"执行恢复: {action.__class__.__name__}")
            return action.execute(context)

        return False
```

---

## 五、状态转换与异常处理对照

### 5.1 状态机异常流程

```
当前状态: TRAVERSING_ITEM
  ↓
执行操作 (click_node)
  ↓
抛出异常: ElementNotFoundException
  ↓
构建 ExceptionContext
  ↓
异常处理链: RetryHandler.can_handle() = True
  ↓
返回结果: RETRY
  ↓
重新执行 click_node
  ↓
再次抛出异常 (retry_count = 3)
  ↓
异常处理链: BacktrackHandler.can_handle() = True
  ↓
返回结果: BACKTRACK
  ↓
状态转换: TRAVERSING_ITEM → RECOVERING
  ↓
执行回退: backtrack_to_continuable_level()
  ↓
找到可继续的节点
  ↓
状态转换: RECOVERING → TRAVERSING_ITEM
  ↓
继续遍历
```

### 5.2 异常状态转换表

| 当前状态 | 异常类型 | 严重级别 | 处理动作 | 目标状态 |
|----------|----------|----------|----------|----------|
| TRAVERSING_ITEM | ElementNotFoundException | ERROR | RETRY | TRAVERSING_ITEM |
| TRAVERSING_ITEM | ElementNotFoundException | ERROR (retry>=3) | BACKTRACK | RECOVERING |
| TRAVERSING_ITEM | PathMismatchException | CRITICAL | BACKTRACK | RECOVERING |
| TRAVERSING_ITEM | ADBDisconnectedException | CRITICAL | RECOVER | RECOVERING |
| TRAVERSING_ITEM | AppCrashException | CRITICAL | RECOVER | RECOVERING |
| TRAVERSING_ITEM | PopupDetectedException | INFO | IGNORE | HANDLING_POPUP |
| TRAVERSING_ITEM | PageRedirectException | INFO | IGNORE | HANDLING_REDIRECT |
| RECOVERING | 恢复成功 | - | - | TRAVERSING_ITEM |
| RECOVERING | 恢复失败 | CRITICAL | TERMINATE | ERROR |
| any | Fatal | FATAL | TERMINATE | ERROR |

---

## 六、使用示例

### 6.1 基础使用

```python
# 初始化
sm = HierarchicalStateMachine()

# 正常使用（自动异常处理）
sm.click_node(target_node)  # 内部自动处理异常

# 手动异常处理
try:
    sm.navigate_to_path(["车辆设置", "DiLink", "互联"])
except TraversalException as e:
    print(f"遍历异常: {e}")
    # 异常已被状态机处理，这里可以额外处理
```

### 6.2 自定义异常处理器

```python
class MyCustomHandler(ExceptionHandler):
    """自定义异常处理器"""

    def can_handle(self, context: ExceptionContext) -> bool:
        return isinstance(context.exception, MyCustomException)

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        # 自定义处理逻辑
        return ExceptionHandlingResult(
            action=ExceptionAction.RETRY,
            new_state=context.state,
            message="自定义重试"
        )

# 添加到处理链
sm.exception_chain.handlers.insert(0, MyCustomHandler())
```

### 6.3 异常历史查询

```python
# 查询异常历史
for exc_context in sm.exception_history:
    print(f"{exc_context.timestamp}: {exc_context.exception}")
    print(f"  状态: {exc_context.state}")
    print(f"  节点: {exc_context.node.name if exc_context.node else 'N/A'}")

# 统计异常类型
from collections import Counter
exc_types = Counter(type(ctx.exception) for ctx in sm.exception_history)
print(exc_types)
```

---

## 七、与 ADB 错误回调集成

### 7.1 统一异常入口

```python
class HierarchicalStateMachine:
    def __init__(self):
        # ...
        self.adb.set_error_callback(self._on_adb_error)

    def _on_adb_error(self, operation: str, message: str, exception=None):
        """ADB 错误回调统一入口"""
        # 转换为 TraversalException
        if "no devices" in message:
            exc = ADBDisconnectedException(message)
        elif "command failed" in message:
            exc = OperationException(message)
        else:
            exc = DeviceException(message)

        # 构建异常上下文
        context = ExceptionContext(
            exception=exc,
            severity=self._get_severity(exc),
            state=self.global_state,
            node=self.current_node,
            operation=operation,
            timestamp=datetime.now(),
            retry_count=0,
        )

        # 执行异常处理
        result = self.exception_chain.handle(context)

        # 执行处理动作
        if result.action == ExceptionAction.BACKTRACK:
            self._backtrack()
        elif result.action == ExceptionAction.RECOVER:
            self.global_state = result.new_state
```

---

## 八、总结

### 异常处理集成要点

1. **统一异常体系** - 所有异常继承自 TraversalException
2. **异常严重级别** - 根据严重程度决定处理策略
3. **异常处理链** - 多个处理器按优先级处理
4. **状态机集成** - 异常处理触发状态转换
5. **恢复策略** - 针对不同异常类型的恢复动作
6. **历史记录** - 记录所有异常供分析和重测
7. **回调统一** - ADB 错误回调接入异常处理体系
