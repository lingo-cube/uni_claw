# Exception Handling System - Design Document

## Context

### 当前状态

Uni-claw V1 实现中，异常处理分散且不完整：

```python
# 当前代码中的异常处理
- consecutive_errors 计数器（简单计数）
- try-except 块分散在各个方法中
- 无统一的异常分类
- 无异常恢复机制
```

### 设计文档参考

本项目有两份详细的异常处理设计文档：
1. `docs/ai_driven_exception_handling.md` - AI 驱动的异常处理
2. `docs/exception_handling_integration.md` - 异常处理与状态机集成

本设计文档综合这两份文档，提供可实现的方案。

### 约束条件

- 不能破坏现有遍历流程
- 异常处理应该是透明的，不影响正常流程
- 需要支持未来扩展（AI 驱动处理）

## Goals / Non-Goals

**Goals:**
- 建立统一的异常分类体系
- 实现可扩展的异常处理器接口
- 提供内置的常用处理器（重试、回退、恢复）
- 与遍历引擎无缝集成
- 记录异常历史供调试

**Non-Goals:**
- AI 驱动的异常处理（Phase 2）
- 复杂的恢复策略（先实现基础恢复）
- 状态机的完整异常处理（配合状态机实现）

## Decisions

### 决策 1: 异常分类体系

**选择**: 五层分类：定位、操作、设备、界面、AI

**理由**:
- 覆盖遍历过程中的所有异常场景
- 便于针对性处理
- 易于扩展新类型

**异常层次结构**:
```python
TraversalException (基类)
├── LocationException (定位相关)
│   ├── ElementNotFoundException
│   ├── PathMismatchException
│   └── CoordinateExpiredException
├── OperationException (操作相关)
│   ├── ClickFailedException
│   └── InputFailedException
├── DeviceException (设备相关)
│   ├── ADBDisconnectedException
│   ├── AppCrashException
│   └── DeviceOfflineException
├── UIException (界面相关)
│   ├── PopupDetectedException
│   ├── PageRedirectException
│   └── LoadingTimeoutException
└── AIException (AI相关)
    ├── AIAnalysisFailedException
    └── AIResponseInvalidException
```

### 决策 2: 严重级别定义

**选择**: 五级严重度：INFO < WARNING < ERROR < CRITICAL < FATAL

**理由**:
- 清晰的优先级
- 支持不同的处理策略
- 便于日志分级

**级别定义**:
```python
class ExceptionSeverity(Enum):
    INFO = "info"           # 信息级（弹窗、跳转等正常变化）
    WARNING = "warning"     # 警告级（需要注意但不需要恢复）
    ERROR = "error"         # 错误级（需要重试）
    CRITICAL = "critical"   # 严重级（需要回退）
    FATAL = "fatal"         # 致命级（终止遍历）
```

### 决策 3: 处理器接口设计

**选择**: 责任链模式，每个处理器独立判断是否能处理

**理由**:
- 灵活可扩展
- 处理器可独立测试
- 优先级可通过顺序控制

**接口定义**:
```python
class ExceptionHandler(ABC):
    @abstractmethod
    def can_handle(self, context: ExceptionContext) -> bool:
        """判断是否能处理该异常"""
        pass
    
    @abstractmethod
    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """处理异常，返回处理结果"""
        pass
```

### 决策 4: 内置处理器优先级

**选择**: 按严重度从高到低的优先级

**处理链顺序**:
1. FatalExceptionHandler - 致命异常直接终止
2. DeviceExceptionHandler - 设备异常优先恢复
3. UIExceptionHandler - 界面异常自动处理
4. RetryHandler - 可重试的异常
5. BacktrackHandler - 回退到上级

**理由**:
- 致命异常必须立即处理
- 设备和界面异常有明确恢复策略
- 重试和回退是兜底策略

### 决策 5: 异常上下文设计

**选择**: 丰富的上下文信息，不依赖截图（Phase 1）

**上下文内容**:
```python
@dataclass
class ExceptionContext:
    exception: TraversalException
    severity: ExceptionSeverity
    state: TraversalState
    node: Optional[TreeNode]
    operation: str
    timestamp: datetime
    retry_count: int
    # screenshot: Optional[bytes]  # Phase 2: AI 分析时需要
    # ai_result: Optional[dict]     # Phase 2: AI 决策结果
```

**理由**:
- Phase 1 不依赖 AI，无需截图
- 减少 AI 调用成本
- Phase 2 可扩展 AI 分析

### 决策 6: 与遍历引擎集成方式

**选择**: 装饰器模式包装关键操作

**实现方式**:
```python
def execute_with_exception_handling(operation, context=None):
    """包装操作，自动处理异常"""
    for attempt in range(max_attempts):
        try:
            return operation()
        except TraversalException as e:
            result = exception_chain.handle(build_context(e))
            if result.action == RETRY:
                continue
            # 处理其他动作...
```

**理由**:
- 最小侵入性
- 操作代码保持简洁
- 统一的异常入口

## 异常处理流程

### 基础流程（Phase 1）

```
异常发生
    ↓
构建 ExceptionContext
    ↓
异常处理链遍历
    ↓
can_handle() 判断
    ↓
┌─────────────────────────────┐
│ 按优先级寻找可处理的处理器   │
│ 1. FatalExceptionHandler    │
│ 2. DeviceExceptionHandler  │
│ 3. UIExceptionHandler      │
│ 4. RetryHandler           │
│ 5. BacktrackHandler       │
└──────────────┬──────────────┘
               ↓
        handler.handle(context)
               ↓
     ExceptionHandlingResult
               ↓
    根据动作执行:
    - RETRY: 重试操作
    - SKIP: 跳过当前元素
    - BACKTRACK: 回退上级
    - RECOVER: 执行恢复
    - TERMINATE: 终止遍历
```

### AI 驱动流程（Phase 2 规划）

```
异常发生
    ↓
构建 ExceptionContext（含截图）
    ↓
AI 异常处理器
    ↓
AI 分析（理解上下文 + 视觉理解）
    ↓
AI 决策 (RETRY/SKIP/NAVIGATE/RECOVER)
    ↓
执行决策
```

## 内置处理器实现

### 1. RetryHandler

```python
class RetryHandler(ExceptionHandler):
    def __init__(self, max_retries: int = 3):
        self.max_retries = max_retries
    
    def can_handle(self, context: ExceptionContext) -> bool:
        return (
            context.severity in [ExceptionSeverity.ERROR]
            and context.retry_count < self.max_retries
        )
    
    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        return ExceptionHandlingResult(
            action=ExceptionAction.RETRY,
            message=f"重试 {context.retry_count + 1}/{self.max_retries}"
        )
```

### 2. DeviceExceptionHandler

```python
class DeviceExceptionHandler(ExceptionHandler):
    def can_handle(self, context: ExceptionContext) -> bool:
        return isinstance(context.exception, DeviceException)
    
    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        if isinstance(context.exception, ADBDisconnectedException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.RECOVERING,
                recovery_action="RECONNECT_ADB"
            )
        elif isinstance(context.exception, AppCrashException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.RECOVERING,
                recovery_action="RESTART_APP"
            )
        return ExceptionHandlingResult(action=ExceptionAction.TERMINATE)
```

### 3. UIExceptionHandler

```python
class UIExceptionHandler(ExceptionHandler):
    def can_handle(self, context: ExceptionContext) -> bool:
        return isinstance(context.exception, UIException)
    
    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        if isinstance(context.exception, PopupDetectedException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.HANDLING_POPUP,
                message="关闭弹窗"
            )
        elif isinstance(context.exception, PageRedirectException):
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.HANDLING_REDIRECT,
                message="处理页面跳转"
            )
        return ExceptionHandlingResult(action=ExceptionAction.IGNORE)
```

## 异常历史记录

### 设计

```python
class ExceptionHistory:
    """异常历史记录"""
    
    def __init__(self, max_records: int = 1000):
        self.records: List[ExceptionContext] = []
        self.max_records = max_records
    
    def record(self, context: ExceptionContext):
        """记录异常"""
        self.records.append(context)
        if len(self.records) > self.max_records:
            self.records.pop(0)
    
    def get_by_type(self, exc_type: Type) -> List[ExceptionContext]:
        """按类型查询"""
        return [r for r in self.records if isinstance(r.exception, exc_type)]
    
    def get_by_severity(self, severity: ExceptionSeverity) -> List[ExceptionContext]:
        """按严重级别查询"""
        return [r for r in self.records if r.severity == severity]
    
    def get_statistics(self) -> dict:
        """获取统计信息"""
        return {
            "total": len(self.records),
            "by_type": Counter(type(r.exception) for r in self.records),
            "by_severity": Counter(r.severity for r in self.records),
        }
```

## TraversalEngine 集成

### 集成方式

```python
class TraversalEngine:
    def __init__(self, ...):
        # 现有初始化
        ...
        
        # 新增：异常处理
        self.exception_chain = ExceptionHandlingChain()
        self.exception_history = ExceptionHistory()
        
        # 构建处理链
        self._build_exception_chain()
    
    def _build_exception_chain(self):
        """构建异常处理链"""
        self.exception_chain.handlers = [
            FatalExceptionHandler(),
            DeviceExceptionHandler(self.adb),
            UIExceptionHandler(self.adb),
            RetryHandler(max_retries=3),
            BacktrackHandler(),
        ]
    
    def execute_with_exception_handling(self, operation, **context):
        """包装操作，自动处理异常"""
        max_attempts = 4  # 1次初始 + 3次重试
        
        for attempt in range(max_attempts):
            try:
                return operation()
            
            except TraversalException as e:
                exc_context = ExceptionContext(
                    exception=e,
                    severity=self._get_severity(e),
                    state=self.state.current_phase,
                    operation=context.get("operation", "unknown"),
                    retry_count=attempt,
                    timestamp=datetime.now(),
                )
                
                # 记录异常
                self.exception_history.record(exc_context)
                
                # 处理异常
                result = self.exception_chain.handle(exc_context)
                
                # 执行处理动作
                if result.action == ExceptionAction.RETRY:
                    continue
                elif result.action == ExceptionAction.SKIP:
                    return None
                elif result.action == ExceptionAction.BACKTRACK:
                    self._backtrack()
                    return None
                elif result.action == ExceptionAction.RECOVER:
                    self._recover(result.recovery_action)
                    continue
                elif result.action == ExceptionAction.TERMINATE:
                    raise
        
        raise TraversalException("操作失败，已重试最大次数")
```

## Risks / Trade-offs

### 风险 1: 过度复杂的异常处理

**风险**: 异常处理逻辑复杂化，影响正常流程

**缓解措施**:
- 保持处理器简单
- 异常处理对正常流程透明
- 充分的单元测试

### 风险 2: 性能影响

**风险**: 异常处理开销影响遍历速度

**缓解措施**:
- 轻量级上下文构建
- 处理器快速判断
- 历史记录异步写入

### 权衡 1: 异常颗粒度

**选择**: 中等颗粒度，按场景分类

**权衡**:
- 更细颗粒度 → 更精准处理，但类别多
- 更粗颗粒度 → 简单，但无法针对性处理

**平衡点**: 按场景分5大类，足够覆盖主要场景

### 权衡 2: 重试策略

**选择**: 固定最大重试次数（3次）

**权衡**:
- 更多重试 → 成功率高，但耗时
- 更少重试 → 快速失败，但成功率低

**平衡点**: 默认3次，可配置

## Migration Plan

### 阶段 1: 异常基础结构

1. 创建 `src/exception/` 模块
2. 定义异常类
3. 定义严重级别枚举
4. 定义异常上下文

### 阶段 2: 处理器实现

1. 实现 ExceptionHandler 接口
2. 实现内置处理器
3. 实现异常处理链
4. 单元测试

### 阶段 3: 引擎集成

1. TraversalEngine 集成异常处理链
2. 包装关键操作
3. 实现异常历史记录
4. 集成测试

### 阶段 4: 测试验证

1. 单元测试：各处理器
2. 集成测试：完整遍历流程
3. 异常注入测试：模拟各种异常

### 回滚策略

如果出现问题：
1. 可以通过配置禁用异常处理链
2. 关键操作的 try-except 保持独立
3. 异常类定义保留，不删除

## Open Questions

1. **AI 驱动处理的优先级**: 是否需要立即实现 AI 驱动处理，还是作为 Phase 2？
2. **异常恢复策略**: 需要实现哪些恢复动作？ADB 重连？APP 重启？
3. **异常报告**: 是否需要将异常发送到远程监控？
4. **性能监控**: 异常处理的性能开销如何？

## Phase 2 规划

### AI 驱动异常处理

基于 `docs/ai_driven_exception_handling.md` 的设计，Phase 2 将增加：

1. AIDrivenExceptionHandler
2. AI 分析提示词
3. AI 决策执行
4. 决策历史记录
5. 反馈学习机制

### 与状态机集成

当状态机实现后，异常处理将触发状态转换：
- 异常 → RECOVERING 状态
- 恢复成功 → TRAVERSING_ITEM 状态
- 恢复失败 → ERROR 状态
