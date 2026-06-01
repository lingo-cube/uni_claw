## Context

uni-claw V5.0 架构已实现核心功能：图模型、三层状态机、Trace 系统、规则型异常处理。当前完成度约 60%，缺失的是 AI 增强层。

**现有架构约束**：
- 图模型和状态机已稳定运行，改动需保持兼容
- 视觉分析已有 MiMo/Claude 集成，可复用
- 规则型异常处理已实现，AI 需作为增强而非替换

**设计目标**：
- AI 功能完全可选，默认禁用
- 各 AI 模块独立，可单独启用
- 保持现有 API 不变，通过扩展而非修改

---

## Goals / Non-Goals

**Goals:**
1. 实现 AIProvider 四项核心能力，支持智能决策
2. 建立四层安全防护，确保零破坏性操作
3. 提供自然语言测试接口，降低使用门槛
4. 增强异常处理，支持 AI 驱动的恢复决策
5. 所有功能保持向后兼容，可渐进式启用

**Non-Goals:**
- 不修改现有图模型和状态机逻辑
- 不改变规则型异常处理的基础行为
- 不实现 Prompt 自动优化（留待 Phase 3）
- 不实现分布式/多设备协同

---

## Decisions

### 1. AIProvider 架构设计

**决策**：采用接口-实现分离，提供 NoOpAIProvider 作为默认实现

```python
class AIProvider(ABC):
    def __init__(self, safety_policy: SafetyPolicy):
        self._safety = safety_policy  # 强制注入

    @abstractmethod
    def parse_task_to_plan(self, task: str) -> TraversalPlan: ...

    @abstractmethod
    def verify_page_type(self, analysis: PageAnalysis, expectation: PageExpectation) -> TypeCheckResult: ...

    @abstractmethod
    def screen_elements(self, items: List[MenuItem], context: dict) -> List[MenuItem]: ...

    @abstractmethod
    def make_decision(self, context: DecisionContext) -> DecisionResult: ...
```

**理由**：
- 接口定义确保所有实现满足安全策略绑定
- NoOpAIProvider 保证无 AI 时系统正常运行
- 便于测试和本地开发

**替代方案考虑**：
- ❌ 直接集成 LLM 调用：缺乏抽象，难以测试和切换模型
- ❌ 可选注入 SafetyPolicy：无法强制安全，存在风险

---

### 2. 安全策略分层设计

**决策**：四层防护，每层独立验证

| 层级 | 位置 | 触发时机 | 实现方式 |
|------|------|----------|----------|
| **第零层** | 元素预筛 | 视觉分析后 | 规则黑名单 + AI 标记 |
| **第一层** | AIProvider 内部 | AI 生成节点时 | SafetyPolicy.validate() |
| **第二层** | 全局过滤器 | 执行前最终检查 | SafetyFilter 全局单例 |
| **第三层** | 设备驱动层 | ADB 执行时 | 系统级权限检查 |

**理由**：
- 多层冗余确保任何单点失效不会导致破坏性操作
- 第零层在 AI 前过滤，减少 AI 调用成本
- 每层职责清晰，易于维护和测试

---

### 3. 自然语言 API 集成方式

**决策**：作为 TraversalEngine 的扩展方法，不侵入核心流程

```python
class TraversalEngine:
    # 现有方法不变...

    def execute(self, command: str) -> ExecutionResult:
        """自然语言命令入口（新增）"""
        if not self.nl_executor:
            raise RuntimeError("Natural language API not enabled")
        return self.nl_executor.execute(command)
```

**理由**：
- 保持现有 `run()` 方法不变
- 自然语言作为独立接口，互不影响
- 便于未来支持更多交互模式（如 REPL）

---

### 4. AI 异常处理集成

**决策**：作为责任链中的一环，优先级高于规则处理

```python
class ExceptionHandlingChain:
    handlers = [
        FatalExceptionHandler(),
        AIDrivenExceptionHandler(vision, ai),  # 新增，优先级 1
        DeviceExceptionHandler(),
        UIExceptionHandler(),
        RetryHandler(),
        BacktrackHandler(),
    ]
```

**理由**：
- AI 处理在规则前，能够应对更多未知场景
- 保持兜底规则，AI 失效时仍可恢复
- 符合"智能为主，规则为辅"的设计理念

---

### 5. 配置管理设计

**决策**：功能开关独立，默认全部关闭

```python
@dataclass
class TraversalConfig:
    # 现有配置...

    # AI 功能开关（新增）
    enable_ai_provider: bool = False
    enable_natural_language: bool = False
    enable_ai_exception_handling: bool = False

    # AI 配置
    ai_model: str = "claude-sonnet-4-6"
    ai_confidence_threshold: float = 0.7
    ai_timeout: int = 30
```

**理由**：
- 默认禁用确保向后兼容
- 独立开关支持渐进式启用
- 集中配置便于管理和调试

---

### 6. 模块组织结构

**决策**：按功能划分模块，保持清晰边界

```
src/
├── ai/                    # AIProvider 核心
│   ├── __init__.py
│   ├── provider.py        # AIProvider 接口
│   ├── noop.py            # No-op 实现
│   ├── claude.py          # Claude 实现
│   └── prompts.py         # Prompt 模板
│
├── safety/                # 安全策略
│   ├── __init__.py
│   ├── policy.py          # SafetyPolicy 接口
│   ├── filter.py          # 全局过滤器
│   └── rules/             # 规则定义
│       ├── blacklist.py
│       └── whitelist.py
│
├── nl/                    # 自然语言 API
│   ├── __init__.py
│   ├── executor.py        # NaturalLanguageExecutor
│   ├── parser.py          # CommandParser
│   └── operations.py      # 操作定义
│
├── exception/             # 扩展异常处理
│   ├── ai_handler.py      # AIDrivenExceptionHandler（新增）
│   └── ...（现有文件）
│
└── traversal/
    └── engine.py          # 扩展 execute() 方法
```

**理由**：
- 模块职责单一，易于理解和维护
- 新模块不影响现有结构
- 便于未来扩展（如新增 AI 提供商）

---

## Risks / Trade-offs

### 风险 1：AI 决策准确性

**风险**：AI 生成错误的遍历计划或安全判断，导致破坏性操作

**缓解措施**：
- 所有 AI 输出必须通过 SafetyPolicy 验证
- 置信度阈值过滤低置信度决策
- 保留规则型处理作为兜底

---

### 风险 2：AI 调用性能影响

**风险**：频繁 AI 调用导致遍历速度显著下降

**缓解措施**：
- 元素预筛批量处理，减少调用次数
- 异步 AI 调用，不阻塞主流程
- 缓存高频决策结果

---

### 风险 3：自然语言解析歧义

**风险**：复杂命令解析错误，执行非预期操作

**缓解措施**：
- 解析结果在执行前需要用户确认（交互模式）
- 提供命令预览功能
- 支持分步执行，逐步验证

---

### 风险 4：安全策略覆盖不全

**风险**：新的破坏性操作未被黑名单覆盖

**缓解措施**：
- 建立安全策略反馈机制，记录拦截日志
- 定期审计拦截日志，补充黑名单
- 提供用户自定义扩展点

---

## Migration Plan

### 阶段 1：基础架构（1 周）
1. 创建 `src/ai/`, `src/safety/`, `src/nl/` 模块骨架
2. 实现 SafetyPolicy 核心逻辑
3. 实现 NoOpAIProvider
4. 扩展 TraversalConfig

### 阶段 2：AIProvider 实现（1.5 周）
1. 实现 ClaudeAIProvider
2. 实现四项能力的 Prompt 工程
3. 集成安全策略验证
4. 单元测试

### 阶段 3：自然语言 API（1 周）
1. 实现命令解析器
2. 实现操作执行器
3. 集成到 TraversalEngine
4. 集成测试

### 阶段 4：AI 异常处理（1 周）
1. 实现 AIDrivenExceptionHandler
2. 集成到异常处理链
3. 实现决策学习记录
4. 端到端测试

### 阶段 5：集成与文档（0.5 周）
1. 全量集成测试
2. 性能基准测试
3. 更新用户文档
4. 更新 PRD_V5.1 进度

---

## Open Questions

1. **AI 模型选择**：是否需要支持多个 AI 提供商同时运行？
   - 倾向：先单一提供商，未来按需扩展

2. **安全策略更新频率**：黑名单更新是否需要热加载？
   - 倾向：先静态加载，Phase 3 考虑动态更新

3. **自然语言支持范围**：是否需要支持条件语句、循环等复杂语法？
   - 倾向：Phase 1 仅支持顺序执行，复杂语法留待 Phase 3

4. **AI 决策缓存策略**：缓存失效条件是什么？
   - 倾向：基于时间 TTL + 上下文指纹
