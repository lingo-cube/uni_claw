## Context

uni-claw 当前版本（V3.0）实现了基于规则引擎的菜单遍历核心能力，包括：
- `TraversalEngine`: 主遍历引擎，控制遍历流程
- `StateManager`: 状态管理，维护 `current_path` 和 `visited_pages`
- `ExceptionChain`: 异常处理责任链
- `VisionService`: 视觉分析服务

当前架构在遇到以下场景时会中断遍历：
1. 未知容器类型 - 规则引擎无法识别菜单结构
2. 目标元素定位失败 - UI 变化导致元素无法找到
3. 异常场景 - 责任链耗尽后无法恢复

PRD V5.0 定义了 AI 策略顾问抽象，用于在这些边缘场景提供智能决策。Phase 1-2 聚焦于建立基础架构，为后续接入真实 LLM 做好准备。

## Goals / Non-Goals

**Goals:**
- 建立 `AIStrategyAdvisor` 抽象接口，定义三个核心方法（容器推断、目标决策、异常兜底）
- 实现默认的 `NoOpAIAdvisor`，保证现有功能不受影响
- 实现测试用的 `MockAIAdvisor`，支持单元测试和集成测试
- 在 `TraversalEngine` 中嵌入 AI 调用点，配置开关控制是否启用
- 实现 `SafetyFilter`，验证 AI 输出的操作安全性
- 实现 `TraversalContext`，封装传递给 AI 的只读上下文
- 实现 AI 调用的超时控制和响应缓存机制
- 单元测试覆盖新增组件

**Non-Goals:**
- 不接入真实 LLM（Phase 3）
- 不实现 Prompt 模板（Phase 3）
- 不实现成本跟踪和模型版本管理（Phase 4）
- 不修改现有规则引擎逻辑
- 不实现 Trace 系统（独立变更）

## Decisions

### 1. AI Advisor 接口设计

**决策**: 使用抽象基类定义三个核心方法

```python
class AIStrategyAdvisor(ABC):
    @abstractmethod
    def infer_container_type(self, ui: PageAnalysis, context: TraversalContext) -> ContainerInference:
        """推断当前页面容器类型"""
        
    @abstractmethod
    def decide_next_action(self, goal: DecisionGoal, ui: PageAnalysis, context: TraversalContext) -> Tuple[DecisionResult, Optional[TraversalNode]]:
        """决策下一个操作"""
        
    @abstractmethod
    def handle_exception(self, exception: ExceptionContext, ui: PageAnalysis, context: TraversalContext) -> Tuple[DecisionResult, Optional[TraversalNode]]:
        """处理异常"""
```

**理由**:
- 抽象基类提供清晰的接口契约
- 三个方法对应三个不同的 AI 应用场景
- 返回元组 `(DecisionResult, Optional[TraversalNode])` 提供决策置信度和结果

**替代方案**:
- 使用 Protocol 而非 ABC: 优点更灵活，缺点缺少运行时检查
- 单一决策方法: 过于简化，无法区分不同场景

### 2. SafetyFilter 验证逻辑

**决策**: 白名单 + 黑名单双重验证

```python
class SafetyFilter:
    ALLOWED_ACTIONS = {"click", "swipe", "back", "input_text", "no_action"}
    BLOCKED_TEXTS = {"恢复出厂设置", "清除数据", "删除所有", "格式化", ...}
    
    def validate(self, node: TraversalNode, context: TraversalContext) -> SafetyResult:
        # 1. 检查操作类型白名单
        # 2. 检查目标文本黑名单
        # 3. 返回 is_safe + reason + fallback_node
```

**理由**:
- 白名单确保只允许已验证的操作类型
- 黑名单防止危险操作（如恢复出厂设置）
- 提供安全的 fallback 节点（跳过当前操作）

### 3. AI 调用点集成

**决策**: 在三个位置嵌入 AI 调用，配置开关控制

| 调用点 | 方法 | 触发条件 |
|--------|------|----------|
| 容器推断 | `infer_container_type` | 规则引擎无法确定容器类型 |
| 目标决策 | `decide_next_action` | 需要达成目标但规则无法定位 |
| 异常兜底 | `handle_exception` | 责任链耗尽后 |

**理由**:
- 清晰的三类场景，覆盖主要 AI 应用场景
- 每个调用点都有明确的触发条件
- 配置开关允许默认关闭 AI 功能

### 4. 超时与缓存机制

**决策**:

```python
class AICallDecorator:
    def __init__(self, timeout=30, cache_ttl=300):
        self.timeout = timeout
        self.cache = TTLCache(maxsize=100, ttl=cache_ttl)
    
    def __call__(self, func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            # 1. 检查缓存
            cache_key = self._make_cache_key(func.__name__, args, kwargs)
            if cache_key in self.cache:
                return self.cache[cache_key]
            # 2. 执行并超时控制
            result = func(*args, **kwargs)
            # 3. 存入缓存
            self.cache[cache_key] = result
            return result
```

**理由**:
- 装饰器模式不侵入业务逻辑
- 30 秒超时防止 AI 调用阻塞遍历
- 5 分钟 TTL 缓存相同上下文的 AI 响应，减少重复调用

### 5. 目录结构

**决策**:

```
src/
├── ai/
│   ├── __init__.py
│   ├── advisor.py          # 抽象接口
│   ├── noop_advisor.py     # 默认实现
│   ├── mock_advisor.py     # 测试实现
│   └── cache.py            # 缓存装饰器
├── safety/
│   ├── __init__.py
│   └── filter.py           # 安全过滤器
├── context/
│   ├── __init__.py
│   └── traversal_context.py # 遍历上下文
└── traversal/
    └── traversal_engine.py # 嵌入 AI 调用点
```

**理由**:
- 按功能模块组织目录
- `ai/` 目录为 Phase 3-4 预留空间
- `safety/` 和 `context/` 独立模块便于复用

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| AI 调用阻塞遍历流程 | 超时控制 + 默认关闭 AI + NoOp 实现 |
| SafetyFilter 过于严格导致遍历中断 | 提供 fallback 节点（跳过），记录审计日志 |
| 缓存导致决策不一致 | 仅缓存相同上下文（ui hash + path hash） |
| 单元测试覆盖不足 | MockAIAdvisor 提供可预测输出 |

## Migration Plan

1. **Phase 1 (1 周)**: 实现核心数据结构和 NoOp 实现
2. **Phase 2 (1-2 周)**: 嵌入 AI 调用点，集成测试
3. **验证**: 运行现有测试套件，确保无破坏性变更
4. **灰度**: 配置开关 `enable_ai_advisor=false`，逐步开放给测试环境

## Open Questions

1. **Q**: 缓存 key 是否需要包含 `action_history`？
   - **A**: 初期仅包含 `ui_hash` 和 `path_hash`，如遇缓存命中导致的问题再调整

2. **Q**: SafetyFilter 黑名单如何维护？
   - **A**: 初始硬编码常见危险操作，后期考虑配置文件

3. **Q**: AI 调用失败是否需要重试？
   - **A**: 不重试，直接返回 `DecisionResult.UNSURE`，让规则引擎处理
