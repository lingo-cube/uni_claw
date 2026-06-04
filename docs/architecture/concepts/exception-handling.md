# 异常处理架构设计

> **整合文档**: AI驱动异常处理 + 状态机集成 + 模块实现  
> **版本**: V6.0  
> **最后更新**: 2026-06-04

---

## 文档说明

本文档整合了原本分散的3个异常处理文档：
1. AI驱动的异常处理概念设计
2. 状态机集成设计  
3. 异常处理模块实现

提供统一的异常处理架构视图，涵盖概念、集成和实现三个层面。

---

## 一、核心概念

### 1.1 传统异常处理的问题

```python
# 传统方式：硬编码规则
if "no devices" in error_message:
    reconnect_adb()
elif "element not found" in error_message:
    retry_with_ai()
elif "popup detected" in error_message:
    close_popup()
# ... 规则越来越多，难以维护
```

**问题：**
- 规则固化，无法应对新场景
- 无法理解"为什么"出错
- 恢复策略单一

### 1.2 统一异常处理架构

```python
# 统一方式：理解上下文，智能决策
exception_handler.handle_exception(exception_context)
# 处理器分析：
# 1. 当前在哪（状态树路径）
# 2. 截图显示什么（视觉理解）
# 3. 出了什么问题（异常类型）
# 4. 应该怎么恢复（智能决策）
```

---

## 二、异常分类体系

### 2.1 异常类型定义

```python
class TraversalException(Exception):
    """遍历异常基类"""
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

### 2.2 异常严重级别

```python
class ExceptionSeverity(Enum):
    """异常严重级别"""
    INFO = "info"           # 信息级（可忽略）
    WARNING = "warning"     # 警告级（需记录）
    ERROR = "error"         # 错误级（需重试）
    CRITICAL = "critical"   # 严重级（需回退）
    FATAL = "fatal"         # 致命级（终止遍历）
```

---

## 三、处理架构设计

### 3.1 异常处理器接口

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

### 3.2 异常处理链

```python
class ExceptionHandlingChain:
    """异常处理链（责任链模式）"""

    def __init__(self, vision_service=None, state_machine=None):
        self.handlers: List[ExceptionHandler] = [
            FatalExceptionHandler(),                    # 1. 致命异常（最高优先级）
            AIDrivenExceptionHandler(vision_service),   # 2. AI 驱动处理（智能核心）
            DeviceExceptionHandler(),                   # 3. 设备异常（专项处理）
            UIExceptionHandler(),                       # 4. 界面异常（专项处理）
            RetryHandler(max_retries=1),                # 5. 简单重试（兜底）
            BacktrackHandler(max_retries=3),            # 6. 回退处理（最后手段）
        ]
        self.executor = AIDecisionExecutor(state_machine)

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

### 3.3 内置处理器

#### **重试处理器**
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
```

#### **回退处理器**
```python
class BacktrackHandler(ExceptionHandler):
    """回退处理器"""

    def can_handle(self, context: ExceptionContext) -> bool:
        """需要回退的异常"""
        return (
            context.severity in [ExceptionSeverity.CRITICAL, ExceptionSeverity.ERROR]
            and context.retry_count >= 3  # 超过重试次数
        )

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行回退"""
        if context.node:
            context.node.state = NodeState.FAILED
            context.node.last_error = str(context.exception)

        return ExceptionHandlingResult(
            action=ExceptionAction.BACKTRACK,
            new_state=TraversalState.RECOVERING,
            message="回退到上级分支"
        )
```

#### **设备异常处理器**
```python
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
```

#### **界面异常处理器**
```python
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
```

---

## 四、AI驱动异常处理

### 4.1 AI异常处理器

```python
class AIDrivenExceptionHandler(ExceptionHandler):
    """AI 驱动的异常处理器"""

    def __init__(self, vision_service, max_retries: int = 3):
        self.vision = vision_service
        self.max_retries = max_retries
        self.decision_history: List[AIDecision] = []

    def can_handle(self, context: ExceptionContext) -> bool:
        """优先使用 AI 处理所有可恢复的异常"""
        return (
            context.severity in [ExceptionSeverity.ERROR, ExceptionSeverity.CRITICAL]
            and context.retry_count < self.max_retries
        )

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """使用 AI 分析并处理异常"""
        # 1. 收集上下文信息
        analysis_input = self._build_analysis_input(context)

        # 2. AI 分析
        ai_decision = self._analyze_with_ai(analysis_input)
        self.decision_history.append(ai_decision)

        # 3. 执行 AI 决策
        return self._execute_decision(ai_decision, context)

    def _build_analysis_input(self, context: ExceptionContext) -> dict:
        """构建 AI 分析输入"""
        current_node = context.node
        state_machine = context.state_machine

        return {
            # 异常信息
            "exception_type": type(context.exception).__name__,
            "exception_message": str(context.exception),

            # 当前状态
            "current_state": context.state.value,
            "current_path": [n.name for n in state_machine.current_path],
            "current_level": len(state_machine.current_path) - 1,

            # 目标信息
            "target_node": {
                "name": current_node.name if current_node else None,
                "type": current_node.node_type.value if current_node else None,
                "coordinates": current_node.coordinates if current_node else None,
            } if current_node else None,

            # 状态树结构
            "state_tree": self._serialize_tree(state_machine.root),

            # 可用的导航选项
            "navigation_options": self._get_navigation_options(state_machine),

            # 重试信息
            "retry_count": context.retry_count,
            "max_retries": self.max_retries,
        }

    def _analyze_with_ai(self, input_data: dict) -> AIDecision:
        """使用 AI 分析异常并给出决策"""

        # 构建 AI 提示词
        prompt = self._build_ai_prompt(input_data)

        # 获取当前截图
        screenshot = self._capture_screenshot()

        # 调用 AI 分析
        response = self.vision.analyze_with_context(
            prompt=prompt,
            image=screenshot,
            context=input_data
        )

        # 解析 AI 决策
        return AIDecision.from_ai_response(response)
```

### 4.2 AI决策类型

| 决策类型 | 说明 | 使用场景 | 参数 |
|----------|------|----------|------|
| **RETRY** | 重试当前操作 | 临时问题（加载中、动画） | wait_time |
| **SKIP** | 跳过当前节点 | 节点不可访问 | skip_to (目标节点) |
| **BACKTRACK** | 回退到上级 | 当前路径无法继续 | backtrack_level |
| **NAVIGATE** | 导航到指定路径 | 路径错误，需要重新定位 | target_path |
| **RECOVER** | 执行恢复动作 | 设备/APP 异常 | recovery_action |
| **WAIT_AND_RETRY** | 等待后重试 | 需要等待加载 | wait_time |

### 4.3 AI决策数据结构

```python
@dataclass
class AIDecision:
    """AI 的异常处理决策"""
    analysis: str              # 问题分析
    decision: str              # 决策类型
    reason: str                # 决策理由
    action_params: dict        # 动作参数
    confidence: float = 0.0    # 置信度

    @classmethod
    def from_ai_response(cls, response: str) -> 'AIDecision':
        """从 AI 响应解析决策"""
        try:
            data = json.loads(response)
            return cls(
                analysis=data.get('analysis', ''),
                decision=data.get('decision', 'SKIP'),
                reason=data.get('reason', ''),
                action_params=data.get('action_params', {}),
                confidence=data.get('confidence', 0.0)
            )
        except json.JSONDecodeError:
            # AI 响应无效，使用默认决策
            return cls(
                analysis='AI 响应无效',
                decision='SKIP',
                reason='无法解析 AI 响应，使用安全策略',
                action_params={},
                confidence=0.0
            )
```

---

## 五、状态机集成

### 5.1 状态机异常处理框架

```python
class HierarchicalStateMachine:
    """融合异常处理的状态机"""

    def __init__(self, vision_service=None):
        # ... 原有字段 ...

        # 异常处理
        self.exception_chain = ExceptionHandlingChain(vision_service, self)
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
                    state_machine=self,
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
                    self.global_state = result.new_state
                    continue

                elif result.action == ExceptionAction.IGNORE:
                    return None

                elif result.action == ExceptionAction.TERMINATE:
                    self.global_state = TraversalState.ERROR
                    raise

        # 超过最大重试次数
        self.global_state = TraversalState.ERROR
        raise TraversalException(f"操作失败，已重试 {max_attempts} 次")
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

## 六、模块实现

### 6.1 模块结构

```
src/exception/
├── __init__.py           # 公共API导出
├── chain.py            # 异常处理链
├── context.py          # 异常上下文和结果数据结构
├── exceptions.py       # 异常类定义
├── handlers.py         # 内置异常处理器
└── history.py          # 异常历史记录
```

### 6.2 公共API

```python
from .chain import ExceptionHandlingChain
from .context import ExceptionAction, ExceptionContext, ExceptionHandlingResult, RecoveryAction
from .exceptions import (
    ADBDisconnectedException,
    AIAnalysisFailedException,
    AIException,
    AIResponseInvalidException,
    AppCrashException,
    ClickFailedException,
    CoordinateExpiredException,
    DeviceException,
    DeviceOfflineException,
    ElementNotFoundException,
    ExceptionSeverity,
    InputFailedException,
    LocationException,
    OperationException,
    PageRedirectException,
    PathMismatchException,
    PopupDetectedException,
    TraversalException,
    UIException,
    LoadingTimeoutException,
)
from .handlers import (
    BacktrackHandler,
    DeviceExceptionHandler,
    ExceptionHandler,
    FatalExceptionHandler,
    RetryHandler,
    UIExceptionHandler,
)
from .history import ExceptionHistory
```

### 6.3 设计原则

- **Severity-Based**: 按严重程度分类处理
- **Chain of Responsibility**: 处理器按优先级处理
- **Context-Rich**: 异常携带丰富上下文信息
- **AI-Enhanced**: AI提供智能决策能力
- **Recovery-Oriented**: 侧重于恢复而非终止

---

## 七、使用示例

### 7.1 基础使用

```python
# 初始化（自动集成异常处理）
sm = HierarchicalStateMachine(vision_service=vision)

# 正常使用（异常自动处理）
sm.click_node(target_node)

# AI自动分析异常并决策
# 例如：
# - 元素正在加载 → WAIT_AND_RETRY
# - 路径错误 → NAVIGATE  
# - 节点不存在 → SKIP
```

### 7.2 查看异常历史

```python
# 查看所有异常记录
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

## 八、设计优势

### 8.1 AI vs 传统规则对比

| 方面 | 传统规则 | AI 驱动 |
|------|----------|---------|
| **适应性** | 固化规则，新场景需添加规则 | 理解上下文，自动适应 |
| **决策质量** | 基于预定义条件 | 基于视觉和状态理解 |
| **可维护性** | 规则增多难以维护 | 提示词集中管理 |
| **可解释性** | 决策过程隐式 | AI 给出分析理由 |
| **学习能力** | 无 | 可从失败中学习 |

### 8.2 关键设计点

1. **上下文感知** - AI理解当前状态树 + 截图
2. **决策可解释** - AI给出分析理由  
3. **学习反馈** - 记录决策结果，持续优化
4. **性能优化** - 可缓存高频决策
5. **兜底策略** - AI失败时回退到规则

---

**相关文档**:
- [状态机设计](../../concepts/state-machine-design.md)
- [异常处理模块](../../modules/exception-design.md)