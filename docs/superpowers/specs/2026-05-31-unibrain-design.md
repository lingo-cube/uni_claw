# UniBrain 设计文档

## 1. 概述

### 1.1 目标
实现统一的 AI Provider，为 uni-claw 车机菜单遍历框架提供五项核心 AI 能力。

**核心能力分配**：
- 4 个文本能力使用 **DeepSeek V4 Flash** LLM
- 1 个视觉能力使用独立的 **Vision Service**（Claude/MiMo/自定义）

**命名说明**：命名为 `UniBrain`（"uni" 取自 uni-claw，"Brain" 体现 AI 智能），统一管理多个 AI 服务（DeepSeek + Vision），为车机遍历提供智能决策能力。

### 1.2 设计原则
- **准确性优先**：所有 AI 输出经过内部验证
- **类型安全**：使用泛型基类确保编译时类型检查
- **可扩展性**：注册解析器模式，无需修改核心代码即可添加新能力
- **可观测性**：统一日志和指标收集
- **兼容性**：Async/sync 包装，与现有同步 TraversalEngine 兼容

---

## 2. 架构设计

### 2.1 三层架构

```
┌─────────────────────────────────────────────────────────────┐
│                   Interface Layer                            │
│               AIStrategyAdvisor                              │
│            (TraversalEngine 使用)                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Provider Layer                           │
│                  UniBrain                             │
│              (implements AIStrategyAdvisor)                  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                   Capabilities Layer (5 个)                   │
│  ┌──────────┬──────────┬──────────┬──────────┬───────────┐  │
│  │ ParseTo  │ Verify   │  Screen  │  Vision  │  Context  │  │
│  │  Plan    │  Page    │ Safety   │ Analysis │ Decision  │  │
│  │          │   Type   │          │          │           │  │
│  │(DeepSeek)│(DeepSeek)│(DeepSeek)│(Vision  │(DeepSeek)│  │
│  │          │          │          │ Service) │           │  │
│  └──────────┴──────────┴──────────┴──────────┴───────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┴────────────────────┐
         │                                           │
┌────────┴─────────┐                   ┌─────────────┴────────┐
│  Vision Service  │                   │  DeepSeek LLM         │
│  (Claude/MiMo)   │                   │  (Text Capabilities)  │
│                 │                   │                       │
│ 为 VisionAnalysis│                   │ 为其他 4 个能力提供动力 │
│ 能力提供动力     │                   │                       │
└──────────────────┘                   └──────────────────────┘
```

### 2.2 模块职责

| 层级 | 模块 | 职责 |
|------|------|------|
| Interface | `AIStrategyAdvisor` | TraversalEngine 使用的 AI 策略接口 |
| Provider | `UniBrain` | 实现 AIStrategyAdvisor，组装能力 |
| Capabilities | `BaseCapability[T_IN, T_OUT]` | 泛型基类，统一执行流程 |
| Capabilities | 五个具体能力 | ParseToPlan、VerifyPageType、ScreenSafety、VisionAnalysis、ContextDecision |
| Core | `LLMClient` | DeepSeek API 调用、重试逻辑、并发控制 |
| Core | `ResponseValidator` | JSON Schema 验证、解析错误处理 |
| Core | `AIProviderConfig` | 配置管理（并发、重试、降级） |
| External | `VisionService` | 独立的视觉分析服务（Claude/MiMo/自定义），为 VisionAnalysis 能力提供动力 |

---

## 3. 核心类设计

### 3.1 AIProviderConfig

```python
@dataclass
class RetryConfig:
    """重试配置"""
    max_attempts: int = 1           # 最大尝试次数（1 = 不重试）
    base_delay: float = 1.0         # 基础延迟（秒）
    max_delay: float = 8.0          # 最大延迟（秒）
    exponential_base: float = 2.0   # 指数退避基数

@dataclass
class FallbackConfig:
    """降级配置"""
    strategy: Literal["none", "partial", "full"] = "partial"
    partial_allowlist: List[str] = field(default_factory=list)

@dataclass
class AIProviderConfig:
    """AI Provider 配置"""
    # API 配置
    api_key: str
    model: str = "deepseek-v4-flash"
    base_url: str = "https://api.deepseek.com/v1"

    # 并发控制
    max_concurrent_requests: int = 4
    request_timeout: float = 30.0

    # 输出配置
    reasoning_detail: Literal["concise", "step_by_step", "detailed"] = "detailed"

    # 重试和降级
    retry: RetryConfig = field(default_factory=RetryConfig)
    fallback: FallbackConfig = field(default_factory=FallbackConfig)

    # 验证配置
    enable_internal_validation: bool = True
```

### 3.2 LLMClient

```python
class LLMClient:
    """DeepSeek API 客户端"""

    def __init__(self, config: AIProviderConfig):
        self.config = config
        self._semaphore = asyncio.Semaphore(config.max_concurrent_requests)
        self._session: Optional[aiohttp.ClientSession] = None

    async def _call_with_retry(
        self,
        messages: List[Dict],
        response_format: Dict,
    ) -> Dict:
        """带指数退避的重试调用"""
        last_error = None

        for attempt in range(self.config.retry.max_attempts):
            try:
                async with self._semaphore:
                    response = await self._call_api(messages, response_format)
                    return response
            except (RateLimitError, TimeoutError, APIError) as e:
                last_error = e
                if attempt < self.config.retry.max_attempts - 1:
                    delay = min(
                        self.config.retry.base_delay
                        * (self.config.retry.exponential_base ** attempt),
                        self.config.retry.max_delay
                    )
                    await asyncio.sleep(delay)
                else:
                    raise

        raise last_error

    async def call(
        self,
        prompt: str,
        schema: Dict,
        variables: Optional[Dict] = None,
    ) -> Dict:
        """
        调用 DeepSeek API

        Args:
            prompt: 提示模板
            schema: JSON Schema for structured output
            variables: 模板变量

        Returns:
            解析后的 JSON 响应
        """
        # 变量注入
        formatted_prompt = self._inject_variables(prompt, variables)

        # 构建消息
        messages = [{"role": "user", "content": formatted_prompt}]

        # 调用 API
        response = await self._call_with_retry(
            messages,
            {"type": "json_schema", "json_schema": schema}
        )

        return response
```

### 3.3 ResponseValidator

```python
Parser = Callable[[Dict], Any]

class ResponseValidator:
    """响应验证器（注册解析器模式）"""

    def __init__(self):
        self._parsers: Dict[str, Parser] = {}

    def register_parser(self, response_type: str, parser: Parser) -> None:
        """注册解析器"""
        self._parsers[response_type] = parser

    def validate_and_parse(
        self,
        response: Dict,
        response_type: str,
    ) -> Any:
        """
        验证并解析响应

        Args:
            response: 原始 JSON 响应
            response_type: 响应类型（用于查找解析器）

        Returns:
            解析后的数据对象

        Raises:
            ValidationError: 验证失败
            ParserNotFoundError: 解析器未注册
        """
        if response_type not in self._parsers:
            raise ParserNotFoundError(response_type)

        # 使用注册的解析器
        return self._parsers[response_type](response)

    def _validate_schema(self, response: Dict, schema: Dict) -> None:
        """内部 Schema 验证（使用 jsonschema）"""
        jsonschema.validate(response, schema)
```

### 3.4 BaseCapability

```python
T_IN = TypeVar("T_IN")
T_OUT = TypeVar("T_OUT")

class BaseCapability(ABC, Generic[T_IN, T_OUT]):
    """AI 能力泛型基类"""

    def __init__(
        self,
        client: LLMClient,
        validator: ResponseValidator,
        config: AIProviderConfig,
    ):
        self.client = client
        self.validator = validator
        self.config = config
        self._logger = logging.getLogger(f"ai.{self.__class__.__name__}")

    @property
    @abstractmethod
    def prompt_template(self) -> str:
        """Prompt 模板"""
        pass

    @property
    @abstractmethod
    def response_schema(self) -> Dict:
        """响应 JSON Schema"""
        pass

    @property
    @abstractmethod
    def response_type(self) -> str:
        """响应类型（用于查找解析器）"""
        pass

    @abstractmethod
    def prepare_input(self, raw_input: T_IN) -> Dict:
        """准备输入变量"""
        pass

    async def execute_async(self, input_data: T_IN) -> T_OUT:
        """异步执行"""
        try:
            # 准备输入
            variables = self.prepare_input(input_data)

            # 调用 LLM
            self._logger.info(f"Calling {self.response_type}")
            start_time = time.time()

            response = await self.client.call(
                prompt=self.prompt_template,
                schema=self.response_schema,
                variables=variables,
            )

            duration = time.time() - start_time
            self._logger.info(f"Response received in {duration:.2f}s")

            # 验证并解析
            result = self.validator.validate_and_parse(
                response,
                self.response_type,
            )

            # 内部验证（可选）
            if self.config.enable_internal_validation:
                self._validate_result(result)

            return result

        except Exception as e:
            self._logger.error(f"Execution failed: {e}")
            self._archive_failure(input_data, e)
            raise

    def execute(self, input_data: T_IN) -> T_OUT:
        """同步执行（async 包装）"""
        loop = asyncio.get_event_loop()
        return loop.run_until_complete(self.execute_async(input_data))

    def _validate_result(self, result: T_OUT) -> None:
        """内部 AI 验证（子类可覆盖）"""
        pass

    def _archive_failure(self, input_data: T_IN, error: Exception) -> None:
        """归档失败信息"""
        failure_record = {
            "capability": self.__class__.__name__,
            "input": input_data,
            "error": str(error),
            "timestamp": datetime.now().isoformat(),
        }
        # 写入失败归档
```

---

## 4. 五个 AI 能力详细设计

### 4.1 ParseToPlanCapability - 指令解析器

**功能**：将自然语言指令解析为遍历计划结构。

**输入**：自然语言指令（字符串）
**输出**：`TraversalPlan` 数据结构

```python
@dataclass
class NodeStrategy:
    """节点策略"""
    element_type: str       # menu_item, option, toggle, button, etc.
    action: str             # click, skip, toggle, etc.
    reasoning: str          # 推理说明

@dataclass
class TraversalPlan:
    """遍历计划"""
    target_container: str   # 目标容器/页面
    scan_mode: str          # full, breadth_first, depth_first
    node_strategies: List[NodeStrategy]
    reasoning: str
    confidence: float
```

**上下文需求**：无需当前页面上下文，仅需要指令本身。

**Prompt 模板**：
```
你是一个车机 UI 遍历规划器。根据用户的指令，生成一个遍历计划。

## 用户指令
{instruction}

## 你的任务
1. 识别用户想要遍历的容器或页面
2. 决定扫描模式（全面扫描/广度优先/深度优先）
3. 为每种元素类型指定策略

## 元素类型说明
- menu_item: 菜单项，应该点击进入
- option: 选项项，不点击，跳过
- toggle: 开关，可能需要切换状态
- button: 功能按钮，通常不点击
- text: 文本标签，不交互

## 输出格式
{{REASONING_LEVEL}} 分析用户的意图，然后输出 JSON。
```

**Response Schema**：
```json
{
  "type": "object",
  "properties": {
    "entry_app": {"type": ["string", "null"]},
    "root_node": {
      "type": "object",
      "properties": {
        "node_id": {"type": "string"},
        "name": {"type": "string"},
        "node_type": {"type": "string"},
        "operation": {"type": "object"},
        "precondition": {"type": ["object", "null"]},
        "children_strategy": {"type": "object"},
        "error_policy": {"type": "null"}
      },
      "required": ["node_id", "name", "node_type", "operation", "precondition", "children_strategy", "error_policy"]
    },
    "static_nodes": {
      "type": "array",
      "items": {"type": "object"}
    },
    "template_registry": {"type": "string"},
    "mode": {"type": "string", "enum": ["hybrid", "concrete", "dynamic"]}
  },
  "required": ["entry_app", "root_node", "template_registry", "mode"]
}
```

### 4.2 VerifyPageTypeCapability - 页面类型验证

**功能**：验证当前页面的类型是否符合预期。

**输入**：`PageAnalysis` + `expected_page_type`
**输出**：`PageTypeVerification`

```python
@dataclass
class PageTypeVerification:
    """页面类型验证结果"""
    is_correct_type: bool
    detected_type: str
    confidence: float
    reasoning: str
```

**上下文需求**：仅需 `PageAnalysis`（当前页面元素）。

**Response Schema**：
```json
{
  "type": "object",
  "properties": {
    "is_match": {"type": "boolean"},
    "confidence": {"type": "number", "minimum": 0, "maximum": 1},
    "actual_type": {"type": "string", "enum": ["menu_list", "settings_group", "dialog", "home_desktop", "leaf_page", "unknown"]},
    "reasoning": {"type": "string"},
    "mismatch_details": {
      "type": "object",
      "properties": {
        "missing_items": {"type": "array", "items": {"type": "string"}},
        "unexpected_items": {"type": "array", "items": {"type": "string"}},
        "type_conflict": {"type": "string"}
      }
    },
    "suggestion": {
      "type": "object",
      "properties": {
        "action": {"type": "string", "enum": ["back", "retry", "skip", "close_popup", "renavigate"]},
        "target": {"type": ["string", "null"]},
        "reason": {"type": "string"}
      }
    }
  },
  "required": ["is_match", "confidence", "actual_type", "reasoning"]
}
```

### 4.3 ScreenSafetyCapability - 元素安全筛选

**功能**：筛选页面中的安全/不安全元素，结合用户任务指令进行上下文感知的安全评估。

**输入**：`PageAnalysis` + `instruction`（用户任务指令）
**输出**：`SafetyScreeningResult`

```python
@dataclass
class SafetyEvaluation:
    """单个元素的安全评估"""
    name: str
    safety_tag: Literal["safe", "caution", "skip", "unknown"]
    confidence: float
    reason: str
    context_dependency: Optional[str] = None
    task_relevance: Optional[str] = None  # 新增：与任务的相关性说明

@dataclass
class PageLevelGuidance:
    """页面级安全指导"""
    overall_safe_to_proceed: bool
    recommended_max_parallel: int
    special_precautions: List[str]
    task_suitability: Optional[str] = None  # 新增：页面与任务的匹配度

@dataclass
class SafetyScreeningResult:
    """安全筛选结果"""
    evaluations: List[SafetyEvaluation]
    page_level_guidance: Optional[PageLevelGuidance]
```

**上下文需求**：需要 `PageAnalysis` + `instruction`（用户任务指令）。

**Prompt 模板**：
```
你是一个车机 UI 安全筛选器。识别页面中的危险元素，并结合用户的任务指令进行上下文感知的安全评估。

## 用户任务指令
{instruction}

## 当前页面路径
{current_path}

## 页面类型
{page_type}

## 页面弹窗状态
{is_popup}

## 待评估元素列表（名称|类型|预期行为|bbox）
{elements_list}

## 安全等级定义
- safe: 常规菜单项、开关、标签页、返回按钮等，操作不会产生不可逆后果
- caution: 含义模糊的按钮、可能触发下载/付费/外部跳转、需要用户确认的操作
- skip:
  · 包含破坏性词汇：恢复出厂设置、清除数据、删除、卸载、格式化、重置
  · 可能退出当前应用：退出、注销、登出、关机
  · 涉及敏感权限：读取通讯录、读取短信、定位权限（非设置开关）
  · 明显是广告或推广内容
  · 支付相关：购买、支付、充值、付款
- unknown: 信息不足无法判断

## 上下文感知规则
1. **任务相关性优先**：与用户任务直接相关的元素优先标记为 safe（除非明显危险）
2. **路径依赖性**：在任务路径上的元素，即使含义模糊也可以谨慎探索
3. **危险操作绝对禁止**：无论任务如何，包含破坏性词汇的元素始终标记为 skip
4. **分支选择建议**：对于 caution 元素，根据任务相关性给出是否探索的建议

## 输出格式
{
  "evaluations": [
    {
      "name": "元素名称（与输入一致）",
      "safety_tag": "safe|caution|skip|unknown",
      "confidence": 0.0-1.0,
      "reason": "简短理由（必填）",
      "context_dependency": "当前路径和任务影响判断的原因（可选）",
      "task_relevance": "该元素与用户任务的相关性（可选）"
    }
  ],
  "page_level_guidance": {
    "overall_safe_to_proceed": true/false,
    "recommended_max_parallel": 3,
    "special_precautions": ["注意事项"],
    "task_suitability": "当前页面与用户任务的匹配度说明"
  }
}

{{REASONING_LEVEL}} 结合任务指令和页面内容，对每个元素进行安全性评估。
```

### 4.4 ContextDecisionCapability - 上下文决策

**功能**：在遍历过程中做出下一步动作决策，**严格遵循安全筛选结果**。

**输入**：`DecisionGoal` + `PageAnalysis` + `SafetyScreeningResult` + `TraversalContext`
**输出**：`ContextDecisionResult`

```python
@dataclass
class ContextDecisionResult:
    """上下文决策结果"""
    result: Literal["success", "unsure", "give_up", "wait", "safe_mode"]
    action: Literal["click", "back", "swipe", "scroll_down", "wait", "skip", "no_action"]
    target: Optional[Dict]  # {"by": "text|coordinate", "value": "..."}
    params: Optional[Dict]  # For swipe: direction, etc.
    reasoning: str
    confidence: float
    safety_verified: bool = True  # 新增：是否经过安全验证
```

**上下文需求**：需要 `PageAnalysis` + `SafetyScreeningResult` + `TraversalContext`。

**Response Schema**：
```json
{
  "type": "object",
  "properties": {
    "result": {"type": "string", "enum": ["success", "unsure", "give_up", "wait", "safe_mode"]},
    "action": {"type": "string", "enum": ["click", "back", "swipe", "scroll_down", "wait", "skip", "no_action"]},
    "target": {
      "type": ["object", "null"],
      "properties": {
        "by": {"type": "string", "enum": ["text", "coordinate"]},
        "value": {"type": "string"}
      }
    },
    "params": {"type": ["object", "null"]},
    "reasoning": {"type": "string"},
    "confidence": {"type": "number", "minimum": 0, "maximum": 1},
    "safety_verified": {"type": "boolean"}
  },
  "required": ["result", "action", "reasoning", "confidence", "safety_verified"]
}
```

**Prompt 模板**：
```
你是一个车机遍历决策器。根据当前状态决定下一步操作，**绝对遵守安全约束**。

## 当前目标
{goal_description}

## 当前页面
{page_analysis}

## 安全筛选结果 ⚠️ 重要
- 整体安全状态: {overall_safe_to_proceed}
- 安全元素: {safe_elements}
- 谨慎元素: {caution_elements}
- 禁止元素: {skip_elements}
- 特殊注意事项: {special_precautions}

## 遍历上下文
- 当前路径: {current_path}
- 已访问页面: {visited_pages}
- 失败节点: {failed_nodes}
- 目标尝试次数: {goal_attempts}

## 安全约束（绝对遵守）
1. **禁止点击标记为 skip 的元素**
2. **谨慎操作标记为 caution 的元素** - 优先选择 safe 元素
3. **无 safe 元素时** - 执行 back 返回上一级
4. **弹窗处理优先级** - cancel > 关闭 > back
5. **异常恢复** - 连续失败 3 次后 back 到根页面

## 输出格式
{{REASONING_LEVEL}} 根据安全约束和当前状态，决定下一步操作。
```

### 4.5 VisionAnalysisCapability - 屏幕视觉分析

**功能**：使用视觉 AI 分析车机屏幕截图，提取页面结构和元素信息。

**输入**：`bytes` (PNG 图片数据)
**输出**：`PageAnalysis`

```python
@dataclass
class PageAnalysis:
    """完整页面分析结果"""
    # 菜单结构
    level1_dir: Direction
    level1_menus: list[MenuInfo]
    level2_dir: Direction
    level2_menus: list[MenuInfo]

    # 当前位置
    current_path: list[str]

    # 内容项
    items: list[MenuItem]

    # 特殊元素
    is_popup: bool = False
    popup_info: Optional[PopupInfo] = None
    close_button: Optional[Coordinate] = None
    back_button: Optional[Coordinate] = None

    # 导航提示
    has_scroll: bool = False
    is_end_of_list: bool = False
```

**上下文需求**：仅需图片数据，无需其他上下文。

**Prompt 模板**：
```text
你是一个车机 UI 屏幕分析器。分析截图并提供完整的页面结构信息。

## 分析任务
1. 识别菜单结构（一级和二级菜单的位置和激活状态）
2. 当前路径（哪些菜单被激活/高亮）
3. 内容区域的所有可点击项，并分类
4. 任何弹窗、对话框或特殊 UI 元素

## 按钮类型分类
对于每个元素，确定其类型和预期行为：

类型：
- menu_item: 列表项，导航到子页面
- tab: 标签页按钮，切换视图
- back_button: 返回导航按钮
- switch: 开关，切换状态
- toggle: 切换按钮
- button: 通用操作按钮
- link: 导航链接
- icon: 无文字的图标按钮
- text: 非交互文本
- readonly: 只读元素

预期行为：
- navigate: 按钮将改变当前页面/视图
- toggle: 按钮将改变 UI 状态
- action: 按钮触发操作（可能显示弹窗）
- none: 无响应

## 输出格式
{{REASONING_LEVEL}} 分析截图并提供以下 JSON：
{
  "level1_dir": "left|right|top|bottom",
  "level1_menus": [{"name": "...", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "level2_dir": "left|right|top|bottom",
  "level2_menus": [{"name": "...", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "current_path": ["level1_name", "level2_name"],
  "items": [
    {
      "name": "item_name",
      "type": "menu_item|tab|back_button|switch|toggle|button|link|icon|text|readonly",
      "expected_action": "navigate|toggle|action|none",
      "expects_page_change": true|false,
      "expects_state_change": true|false,
      "x": 0.0-1.0,
      "y": 0.0-1.0,
      "parent": "parent_name_or_null"
    }
  ],
  "is_popup": false,
  "popup_info": {"title": "...", "content": "...", "close_button": {"x": 0.0, "y": 0.0}} or null,
  "close_button": {"x": 0.0, "y": 0.0} or null,
  "back_button": {"x": 0.0, "y": 0.0} or null,
  "has_scroll": false,
  "is_end_of_list": false
}
```

**Response Schema**：
```json
{
  "type": "object",
  "properties": {
    "level1_dir": {"type": "string", "enum": ["left", "right", "top", "bottom"]},
    "level1_menus": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": {"type": "string"},
          "x": {"type": "number", "minimum": 0, "maximum": 1},
          "y": {"type": "number", "minimum": 0, "maximum": 1},
          "active": {"type": "boolean"}
        },
        "required": ["name", "x", "y", "active"]
      }
    },
    "level2_dir": {"type": "string", "enum": ["left", "right", "top", "bottom"]},
    "level2_menus": {"type": "array", "items": {...}},
    "current_path": {"type": "array", "items": {"type": "string"}},
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": {"type": "string"},
          "type": {"type": "string"},
          "expected_action": {"type": "string"},
          "expects_page_change": {"type": "boolean"},
          "expects_state_change": {"type": "boolean"},
          "x": {"type": "number", "minimum": 0, "maximum": 1},
          "y": {"type": "number", "minimum": 0, "maximum": 1},
          "parent": {"type": ["string", "null"]}
        },
        "required": ["name", "type", "expected_action", "x", "y"]
      }
    },
    "is_popup": {"type": "boolean"},
    "popup_info": {"type": ["object", "null"]},
    "close_button": {"type": ["object", "null"]},
    "back_button": {"type": ["object", "null"]},
    "has_scroll": {"type": "boolean"},
    "is_end_of_list": {"type": "boolean"}
  },
  "required": ["level1_dir", "level1_menus", "level2_dir", "level2_menus", "current_path", "items"]
}
```

---

## 5. Refined Prompt Templates (Final Version)

### 5.1 PromptRegistry 统一管理

```python
class PromptRegistry:
    """Prompt 模板注册表 - 统一管理所有 AI 能力的提示词"""

    def __init__(self, config: AIProviderConfig):
        self._prompts: Dict[str, str] = {}
        self.config = config
        self._load_defaults()

    def _load_defaults(self):
        """加载默认 Prompt 模板"""
        self._prompts = {
            # 能力1：指令解析
            "parse_task.system": self._get_parse_task_system(),
            "parse_task.user": self._get_parse_task_user(),

            # 能力2：页面验证
            "verify_page.system": self._get_verify_page_system(),
            "verify_page.user": self._get_verify_page_user(),

            # 能力3：元素安全预筛
            "screen_elements.system": self._get_screen_elements_system(),
            "screen_elements.user": self._get_screen_elements_user(),

            # 能力4：视觉分析（使用 Vision Service，不使用 DeepSeek Prompt）
            "vision_analysis.system": self._get_vision_analysis_system(),
            "vision_analysis.user": self._get_vision_analysis_user(),

            # 能力5：上下文决策
            "make_decision.system": self._get_decision_system(),
            "make_decision.user": self._get_decision_user(),
        }

    def get(self, key: str) -> str:
        """获取 Prompt 模板"""
        template = self._prompts.get(key, "")
        # 注入推理级别
        if template:
            reasoning_level = self._get_reasoning_prompt()
            template = template.replace("{{REASONING_LEVEL}}", reasoning_level)
        return template

    def register(self, key: str, prompt: str) -> None:
        """注册自定义 Prompt 模板"""
        self._prompts[key] = prompt

    def _get_reasoning_prompt(self) -> str:
        """获取推理级别的提示"""
        levels = {
            "concise": "简要说明你的分析过程",
            "step_by_step": "分步骤说明你的分析过程",
            "detailed": "详细分析每个因素和决策依据",
        }
        return levels.get(self.config.reasoning_detail, "详细分析每个因素和决策依据")

    # ============ Prompt 模板定义 ============

    def _get_parse_task_system(self) -> str:
        return """你是车机自动化测试的任务解析器。根据用户的自然语言指令，生成一个遍历计划 JSON。

## 输出格式
严格返回以下 JSON，不含任何额外字段：
{
  "entry_app": "应用名（如'设置'）或 null",
  "root_node": { … },
  "static_nodes": [ … ],  // 可选，仅用户明确指定路径时提供
  "template_registry": "default",
  "mode": "hybrid"
}

## root_node 结构
- node_id: 唯一标识（如 "root"）
- name: 显示名称
- node_type: "container"
- operation: 统一格式见下方
- precondition: {"page_name": "页面名"} 或 {"ui_condition": "screen_contains('文本')"} 或 null
- children_strategy: 默认使用动态匹配
- error_policy: null

## operation 统一格式
所有操作都使用统一结构，避免变体：
- 点击: {"action": "click", "target": {"by": "text", "value": "文本"}, "params": null, "restore": null}
- 滑动: {"action": "swipe", "target": {"by": "text", "value": "目标名"}, "params": {"target_fraction": 0.2}, "restore": {"action": "swipe", "params": {"target_fraction": "<original>"}}}
- 返回: {"action": "back", "target": null, "params": null, "restore": null}
- 无操作: {"action": "no_action", "target": null, "params": null, "restore": null}

## children_strategy 默认结构
{
  "type": "dynamic_match",
  "dynamic_rules": {
    "menu_rule": {
      "match_condition": {"type": "menu_item", "expected_action": "navigate"},
      "child_template": "menu_container",
      "action": "generate_child"
    },
    "switch_rule": {
      "match_condition": {"type": "switch"},
      "child_template": "switch_leaf",
      "action": "generate_child"
    },
    "slider_rule": {
      "match_condition": {"type": "slider"},
      "child_template": "slider_leaf",
      "action": "generate_child"
    }
  }
}

## 规则
1. 默认使用动态匹配探索，不要预置静态路径。
2. 绝对禁止生成危险操作：target.value 不能包含"恢复出厂设置"、"清除数据"、"删除"、"卸载"、"格式化"、"重置"。
3. 未指定应用时默认 entry_app="设置"。
4. action 只能是：click, back, swipe, input_text, no_action。
5. 无法解析时返回默认计划：entry_app="设置"，动态匹配容器，mode="hybrid"。

## 示例
指令："遍历系统设置"
输出：
{
  "entry_app": "设置",
  "root_node": {
    "node_id": "root",
    "name": "设置主页",
    "node_type": "container",
    "operation": {"action": "click", "target": {"by": "text", "value": "设置"}, "params": null, "restore": null},
    "precondition": {"page_name": "桌面"},
    "children_strategy": {
      "type": "dynamic_match",
      "dynamic_rules": {
        "menu_rule": {"match_condition": {"type": "menu_item", "expected_action": "navigate"}, "child_template": "menu_container", "action": "generate_child"},
        "switch_rule": {"match_condition": {"type": "switch"}, "child_template": "switch_leaf", "action": "generate_child"}
      }
    },
    "error_policy": null
  },
  "template_registry": "default",
  "mode": "hybrid"
}

指令："只检查设置→显示→亮度"
输出：
{
  "entry_app": "设置",
  "root_node": {
    "node_id": "root",
    "name": "设置入口",
    "node_type": "container",
    "operation": {"action": "click", "target": {"by": "text", "value": "设置"}, "params": null, "restore": null},
    "precondition": {"page_name": "桌面"},
    "children_strategy": {"type": "static", "static_children": ["display"]}
  },
  "static_nodes": [
    {
      "node_id": "display",
      "name": "显示",
      "node_type": "container",
      "operation": {"action": "click", "target": {"by": "text", "value": "显示"}, "params": null, "restore": null},
      "precondition": {"page_name": "设置"},
      "children_strategy": {"type": "static", "static_children": ["brightness"]}
    },
    {
      "node_id": "brightness",
      "name": "亮度",
      "node_type": "leaf_slider",
      "operation": {"action": "swipe", "target": {"by": "text", "value": "亮度"}, "params": {"target_fraction": 0.2}, "restore": {"action": "swipe", "params": {"target_fraction": "{{original_value}}"}}},
      "precondition": {"page_name": "显示"},
      "children_strategy": {"type": "none"}
    }
  ],
  "template_registry": "default",
  "mode": "concrete"
}
"""

    def _get_parse_task_user(self) -> str:
        return """用户指令：{instruction}

{{REASONING_LEVEL}} 分析指令并生成遍历计划 JSON。"""

    def _get_verify_page_system(self) -> str:
        return """你是车机页面类型验证器。根据当前页面的元素分布特征，判断页面实际类型是否匹配预期类型。

## 页面类型定义
- menu_list: 顶部有水平一级菜单，可能有二级标签页，内容区大量 menu_item（占比>70%），少量其他控件
- settings_group: 内容区混合 menu_item、switch、slider 等多种控件，通常是一级菜单下的具体设置页
- dialog: 弹窗特征，元素数量少（<5），有"确定/取消"或"关闭"按钮，通常 is_popup=true
- home_desktop: 大量应用图标、文件夹，通常有底部固定栏（电话、短信等）
- leaf_page: 纯信息展示页，无可交互元素或只有少量按钮
- unknown: 无法归类

## 输出格式
{
  "is_match": true/false,
  "confidence": 0.0-1.0,
  "actual_type": "推断的实际页面类型（menu_list/settings_group/dialog/home_desktop/leaf_page/unknown）",
  "reasoning": "判断依据（简短）",
  "mismatch_details": {
    "missing_items": ["缺失的必要元素"],
    "unexpected_items": ["不应出现的元素"],
    "type_conflict": "类型冲突说明"
  },
  "suggestion": {
    "action": "back|retry|skip|close_popup|renavigate",
    "target": "操作目标或null",
    "reason": "建议理由"
  }
}

## 判断规则
1. 优先匹配 expected_type 描述的特征。
2. 如果当前页面有弹窗（is_popup=true），应判定为 dialog。
3. 如果有 level1_menus 且元素多为 menu_item，判定为 menu_list。
4. 如果混合多种控件类型且无明确菜单结构，判定为 settings_group。
5. 如果 items 为空或仅有 text/readonly，判定为 leaf_page。
"""

    def _get_verify_page_user(self) -> str:
        return """预期页面类型：{expected_type}
预期页面名：{expected_page_name}
预期必要元素：{required_items}

当前页面信息：
- 路径：{current_path}
- 弹窗状态：{is_popup}
- 一级菜单：{level1_menus_summary}
- 二级标签：{level2_menus_summary}
- 元素列表（名称|类型|预期行为|bbox）：
{elements_detail}

{{REASONING_LEVEL}} 判断当前页面是否匹配预期类型。"""

    def _get_screen_elements_system(self) -> str:
        return """你是车机界面安全分析助手。对给定的界面元素列表进行安全性评估，判断每个元素是否可以被自动化测试安全操作。

## 安全等级定义
- safe: 常规菜单项、开关、标签页、返回按钮等，操作不会产生不可逆后果
- caution: 含义模糊的按钮、可能触发下载/付费/外部跳转、需要用户确认的操作（如"立即升级"、"查看详情"）
- skip:
  · 包含破坏性词汇：恢复出厂设置、清除数据、删除、卸载、格式化、重置
  · 可能退出当前应用：退出、注销、登出、关机
  · 涉及敏感权限：读取通讯录、读取短信、定位权限（非设置开关）
  · 明显是广告或推广内容
  · 支付相关：购买、支付、充值、付款
- unknown: 信息不足无法判断

## 输出格式
{
  "evaluations": [
    {
      "name": "元素名称（与输入一致）",
      "safety_tag": "safe|caution|skip|unknown",
      "confidence": 0.0-1.0,
      "reason": "简短理由（必填）",
      "context_dependency": "当前路径和任务影响判断的原因（可选）",
      "task_relevance": "该元素与用户任务的相关性（可选）"
    }
  ],
  "page_level_guidance": {
    "overall_safe_to_proceed": true/false,
    "recommended_max_parallel": 3,
    "special_precautions": ["注意事项"],
    "task_suitability": "当前页面与用户任务的匹配度说明"
  }
}

## 判断优先级（结合任务上下文）
1. **任务相关性优先**：与用户任务直接相关的元素优先标记为 safe（除非明显危险）
2. **绝对危险优先**：先检查是否包含破坏性或敏感词汇（→ skip），无论任务如何
3. **控件类型判断**：switch/slider 在设置页面通常是 safe，在未知页面是 caution
4. **按钮类分析**：看名称是否有明确功能描述，模糊的标记为 caution
5. **路径上下文**：在任务路径上的元素，即使含义模糊也可以谨慎探索
"""

    def _get_screen_elements_user(self) -> str:
        return """## 用户任务指令
{instruction}

## 当前页面路径
{current_path}

## 当前页面类型
{page_type}

## 页面弹窗状态
{is_popup}

## 待评估元素列表（名称|类型|预期行为|bbox）
{elements_list}

{{REASONING_LEVEL}} 结合任务指令和页面内容，对每个元素进行安全性评估，输出 JSON。"""

    def _get_decision_system(self) -> str:
        return """你是车机遍历决策助手。根据当前遍历上下文和页面状况，决定下一步要执行的具体操作。

## 你可使用的动作
- click: 点击目标元素（通过 text 或 coordinate 定位）
- back: 返回上一级
- swipe: 滑动操作（需指定方向）
- scroll_down: 向下滚动列表
- wait: 等待 2 秒后重新检查
- skip: 跳过当前目标，继续下一个
- no_action: 不执行操作

## 输出格式
{
  "result": "success|unsure|give_up|wait",
  "action": "click|back|swipe|scroll_down|wait|skip|no_action",
  "target": {"by": "text|coordinate", "value": "目标文本或[x,y]坐标"} or null,
  "params": {} or null,  // swipe时的方向参数
  "reasoning": "决策理由（简短）",
  "confidence": 0.0-1.0
}

## 坐标定位规则
1. 优先使用文本定位 (by: text)
2. 仅当元素无可识别文本时使用坐标 (by: coordinate)
3. 坐标值使用输入中提供的 bbox 中心点，不要估计
4. 坐标格式：[x, y] 范围 [0.0, 1.0]

## 决策原则

### 弹窗处理
1. 优先点击"取消"、"关闭"、"返回"、"否"等非破坏性按钮
2. 若弹窗是权限请求且无法跳过，可点击"允许"但需标注 reason
3. 若无明显关闭按钮，尝试点击弹窗外部区域（使用坐标）或执行 back

### 异常恢复
1. "元素未找到"时：先尝试 back 返回并重试，若仍失败则 skip
2. "点击无响应"时：尝试点击同一坐标偏移 5% 的位置，或等待后重试
3. "页面跳转异常"时：连续 back 直到回到已知页面（检查 visited_pages）
4. 连续失败 3 次以上：建议 back 到根页面并 skip 当前分支

### 分支选择
1. 优先选择未被访问的 menu_item
2. 避开已标记为 skip 或 caution 的元素
3. 当前层级无可用项时：back 到父级

### 安全约束
1. 绝对不要点击包含"恢复出厂设置"、"清除数据"、"删除"等文本的元素
2. 不要执行 input_text 操作（除非明确授权）
"""

    def _get_decision_user(self) -> str:
        return """## 决策触发原因
{reason}

## 当前页面信息
- 路径：{current_path}
- 弹窗状态：{is_popup}
- 弹窗详情：{popup_info}
- 可用元素（名称|类型|行为|安全标记|bbox）：
{elements_detail}

## 安全筛选结果 ⚠️
- 整体安全：{overall_safe_to_proceed}
- 安全元素：{safe_elements}
- 谨慎元素：{caution_elements}
- 禁止元素：{skip_elements}
- 特殊注意：{special_precautions}

## 遍历上下文
- 节点栈（底→顶）：{node_stack}
- 已访问页面：{visited_pages}
- 失败节点：{failed_nodes}
- 最近操作：{action_history}
- 额外信息：{extra}

{{REASONING_LEVEL}} 根据安全约束和当前状态，决定下一步操作。"""

    def _get_vision_analysis_system(self) -> str:
        return """你是一个车机 UI 屏幕分析器。分析截图并提供完整的页面结构信息。

## 分析任务
1. 识别菜单结构（一级和二级菜单的位置和激活状态）
2. 当前路径（哪些菜单被激活/高亮）
3. 内容区域的所有可点击项，并分类
4. 任何弹窗、对话框或特殊 UI 元素

## 按钮类型分类
对于每个元素，确定其类型和预期行为：

类型：
- menu_item: 列表项，导航到子页面
- tab: 标签页按钮，切换视图
- back_button: 返回导航按钮
- switch: 开关，切换状态
- toggle: 切换按钮
- button: 通用操作按钮
- link: 导航链接
- icon: 无文字的图标按钮
- text: 非交互文本
- readonly: 只读元素

预期行为：
- navigate: 按钮将改变当前页面/视图
- toggle: 按钮将改变 UI 状态
- action: 按钮触发操作（可能显示弹窗）
- none: 无响应

字段指南：
- expects_page_change: navigate/action 为 true，toggle/none 为 false
- expects_state_change: toggle 为 true，其他为 false

## 输出格式
返回以下 JSON 结构：
{
  "level1_dir": "left|right|top|bottom",
  "level1_menus": [{"name": "menu_name", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "level2_dir": "left|right|top|bottom",
  "level2_menus": [{"name": "tab_name", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "current_path": ["level1_name", "level2_name"],
  "items": [
    {
      "name": "item_name",
      "type": "menu_item|tab|back_button|switch|toggle|button|link|icon|text|readonly",
      "expected_action": "navigate|toggle|action|none",
      "expects_page_change": true|false,
      "expects_state_change": true|false,
      "x": 0.0-1.0,
      "y": 0.0-1.0,
      "parent": "parent_name_or_null"
    }
  ],
  "is_popup": false,
  "popup_info": {"title": "...", "content": "...", "close_button": {"x": 0.0, "y": 0.0}} or null,
  "close_button": {"x": 0.0, "y": 0.0} or null,
  "back_button": {"x": 0.0, "y": 0.0} or null,
  "has_scroll": false,
  "is_end_of_list": false
}

## 示例输出
{
  "level1_dir": "left",
  "level1_menus": [
    {"name": "DiLink", "x": 0.08, "y": 0.12, "active": true},
    {"name": "DiPilot", "x": 0.08, "y": 0.20, "active": false}
  ],
  "level2_dir": "top",
  "level2_menus": [
    {"name": "互联", "x": 0.28, "y": 0.06, "active": true},
    {"name": "音响", "x": 0.45, "y": 0.06, "active": false}
  ],
  "current_path": ["DiLink", "互联"],
  "items": [
    {
      "name": "移动数据",
      "type": "menu_item",
      "expected_action": "navigate",
      "expects_page_change": true,
      "expects_state_change": false,
      "x": 0.45,
      "y": 0.35,
      "parent": null
    },
    {
      "name": "[开关]移动数据开关",
      "type": "switch",
      "expected_action": "toggle",
      "expects_page_change": false,
      "expects_state_change": true,
      "x": 0.85,
      "y": 0.35,
      "parent": "移动数据"
    }
  ],
  "is_popup": false,
  "popup_info": null,
  "close_button": null,
  "back_button": {"x": 0.05, "y": 0.05},
  "has_scroll": true,
  "is_end_of_list": false
}

## 重要提示
- 所有坐标必须归一化到 0-1（相对于屏幕尺寸）
- 使用 "parent" 字段标记父子关系
- 使用 current_path 指示当前激活的菜单
- 对于无文字的图标，命名为 "[icon] 描述"
- 包含所有交互元素，不仅仅是文字
- 不确定时默认使用 expected_action="action"
- navigate/action 类型使用 expects_page_change=true
- 仅 toggle 类型使用 expects_state_change=true
"""

    def _get_vision_analysis_user(self) -> str:
        return """{{REASONING_LEVEL}} 分析截图并提供页面结构 JSON。"""

    # 变量格式说明
    VARIABLE_FORMATS = {
        "elements_detail": "格式：{name}|{type}|{expected_action}|{bbox}\\n示例：移动数据|menu_item|navigate|[0.1,0.2,0.3,0.4]",
        "current_path": "格式：['父级', '当前']\\n示例：['设置', '显示']",
        "node_stack": "格式：node1 → node2 → node3\\n示例：root → display_entry → brightness",
        "visited_pages": "格式：page1, page2, page3\\n示例：设置, 显示, 亮度",
        "action_history": "格式：操作1→结果, 操作2→结果\\n示例：点击'自动亮度'→成功, 点击'蓝牙'→失败",
    }
```

### 5.2 上下文变量格式规范

为了确保 AI 能正确解析输入，所有变量必须遵循以下格式：

| 变量名 | 格式 | 示例 |
|--------|------|------|
| `elements_detail` | `{name}\|{type}\|{expected_action}\|{bbox}\n...` | `移动数据\|menu_item\|navigate\|[0.1,0.2,0.3,0.4]\n` |
| `current_path` | `['父级', '当前']` | `['设置', '显示']` |
| `node_stack` | `node1 → node2 → node3` | `root → display → brightness` |
| `visited_pages` | `page1, page2, page3` | `设置, 显示, 亮度` |
| `action_history` | `操作→结果, 操作→结果` | `点击'自动亮度'→成功, 点击'蓝牙'→失败` |
| `bbox` | `[x1, y1, x2, y2]` (归一化 0.0-1.0) | `[0.1, 0.2, 0.3, 0.4]` |
| `coordinate` | `[x, y]` (归一化 0.0-1.0) | `[0.2, 0.5]` |

### 5.3 BaseCapability 与 PromptRegistry 集成

```python
class BaseCapability(ABC, Generic[T_IN, T_OUT]):
    """AI 能力泛型基类 - 与 PromptRegistry 集成"""

    def __init__(
        self,
        client: LLMClient,
        validator: ResponseValidator,
        config: AIProviderConfig,
        prompt_registry: PromptRegistry,
    ):
        self.client = client
        self.validator = validator
        self.config = config
        self.prompt_registry = prompt_registry
        self._logger = logging.getLogger(f"ai.{self.__class__.__name__}")

    @property
    @abstractmethod
    def system_prompt_key(self) -> str:
        """系统 Prompt 模板键名（如 "parse_task.system"）"""
        pass

    @property
    @abstractmethod
    def user_prompt_key(self) -> str:
        """用户 Prompt 模板键名（如 "parse_task.user"）"""
        pass

    @property
    @abstractmethod
    def response_schema(self) -> Dict:
        """响应 JSON Schema"""
        pass

    @property
    @abstractmethod
    def response_type(self) -> str:
        """响应类型（用于查找解析器）"""
        pass

    @abstractmethod
    def prepare_input(self, raw_input: T_IN) -> Dict:
        """准备输入变量"""
        pass

    async def execute_async(self, input_data: T_IN) -> T_OUT:
        """异步执行"""
        try:
            # 准备输入
            variables = self.prepare_input(input_data)

            # 获取 Prompt 模板
            system_prompt = self.prompt_registry.get(self.system_prompt_key)
            user_prompt = self.prompt_registry.get(self.user_prompt_key)

            # 注入变量到用户 Prompt
            formatted_user_prompt = self._inject_variables(user_prompt, variables)

            # 构建完整消息
            messages = [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": formatted_user_prompt}
            ]

            # 调用 LLM
            self._logger.info(f"Calling {self.response_type}")
            start_time = time.time()

            response = await self.client.call(
                messages=messages,
                schema=self.response_schema,
            )

            duration = time.time() - start_time
            self._logger.info(f"Response received in {duration:.2f}s")

            # 验证并解析
            result = self.validator.validate_and_parse(
                response,
                self.response_type,
            )

            # 内部验证（可选）
            if self.config.enable_internal_validation:
                self._validate_result(result)

            return result

        except Exception as e:
            self._logger.error(f"Execution failed: {e}")
            self._archive_failure(input_data, e)
            raise

    def _inject_variables(self, template: str, variables: Dict) -> str:
        """注入变量到模板"""
        result = template
        for key, value in variables.items():
            placeholder = f"{{{key}}}"
            result = result.replace(placeholder, str(value))
        return result

    def _validate_result(self, result: T_OUT) -> None:
        """内部 AI 验证（子类可覆盖）"""
        pass

    def _archive_failure(self, input_data: T_IN, error: Exception) -> None:
        """归档失败信息"""
        failure_record = {
            "capability": self.__class__.__name__,
            "input": input_data,
            "error": str(error),
            "timestamp": datetime.now().isoformat(),
        }
        # 写入失败归档
```

#### 具体能力实现示例

```python
class ParseTaskCapability(BaseCapability[str, TraversalPlan]):
    """指令解析能力"""

    @property
    def system_prompt_key(self) -> str:
        return "parse_task.system"

    @property
    def user_prompt_key(self) -> str:
        return "parse_task.user"

    @property
    def response_schema(self) -> Dict:
        return {
            "type": "object",
            "properties": {
                "entry_app": {"type": ["string", "null"]},
                "root_node": {...},
                "static_nodes": {...},
                "template_registry": {"type": "string"},
                "mode": {"type": "string", "enum": ["hybrid", "concrete", "dynamic"]}
            },
            "required": ["entry_app", "root_node", "template_registry", "mode"]
        }

    @property
    def response_type(self) -> str:
        return "TraversalPlan"

    def prepare_input(self, raw_input: str) -> Dict:
        return {"instruction": raw_input}
```

---

## 6. Vision 服务集成

Vision 服务负责将车机屏幕截图转换为结构化的 `PageAnalysis` 数据，为 AI 能力提供上下文信息。

### 6.1 Vision 服务架构

```
┌─────────────────────────────────────────────────────────────┐
│                   Vision Service Layer                      │
│  ┌──────────────────┬──────────────────┬────────────────┐ │
│  │ ClaudeVisionSvc │  MiMoVisionSvc  │ MockVisionSvc │ │
│  │  (Claude API)    │  (MiMo API)     │   (Testing)   │ │
│  └──────────────────┴──────────────────┴────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   BaseVisionService                          │
│  ┌──────────────┬──────────────┬────────────────────────┐ │
│  │_encode_image │ _extract_json│ _call_vision (abstract)│ │
│  └──────────────┴──────────────┴────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      VisionService (ABC)                     │
│  ┌──────────────────────┬────────────────────────────────┐ │
│  │ analyze_screenshot   │ find_app_entry                   │ │
│  └──────────────────────┴────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 Vision 接口定义

```python
class VisionService(ABC):
    """Vision 服务抽象基类"""

    @abstractmethod
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """分析截图并返回页面结构

        Args:
            image_data: PNG 图片字节数据

        Returns:
            PageAnalysis 包含检测到的元素信息
        """
        pass

    @abstractmethod
    def find_app_entry(self, image_data: bytes, target: str) -> dict | None:
        """在主页面上查找目标应用图标

        Args:
            image_data: PNG 图片字节数据
            target: 要查找的应用名称

        Returns:
            dict with x, y coordinates if found, None otherwise
        """
        pass
```

### 6.3 PageAnalysis 数据结构

`PageAnalysis` 是 Vision 服务输出和 AI 能力输入的核心数据结构：

```python
class Direction(str, Enum):
    """菜单方向枚举"""
    LEFT = "left"
    RIGHT = "right"
    TOP = "top"
    BOTTOM = "bottom"


class Coordinate(BaseModel):
    """归一化坐标 (0-1)"""
    x: float = Field(ge=0.0, le=1.0)
    y: float = Field(ge=0.0, le=1.0)


class MenuInfo(BaseModel):
    """菜单项信息"""
    name: str
    coordinate: Coordinate
    active: bool = False


class MenuItemType(str, Enum):
    """菜单项类型（细粒度分类）"""
    # 导航类型
    MENU_ITEM = "menu_item"      # 可点击菜单项
    TAB = "tab"                   # 标签页按钮
    BACK_BUTTON = "back_button"  # 返回按钮
    # 动作类型
    SWITCH = "switch"             # 开关（改变状态）
    TOGGLE = "toggle"            # 切换按钮
    BUTTON = "button"            # 通用按钮
    # 其他类型
    ICON = "icon"
    LINK = "link"
    TEXT = "text"
    READONLY = "readonly"


class ExpectedAction(str, Enum):
    """预期行为类型"""
    NAVIGATE = "navigate"  # 期望页面导航
    TOGGLE = "toggle"      # 期望状态改变
    ACTION = "action"      # 期望触发操作
    NONE = "none"          # 无响应


class MenuItem(BaseModel):
    """屏幕上的可点击项"""
    name: str
    type: MenuItemType
    coordinate: Coordinate
    parent: Optional[str] = None
    description: Optional[str] = None

    # 行为预测字段
    expected_action: ExpectedAction = ExpectedAction.ACTION
    expects_page_change: bool = False
    expects_state_change: bool = False


class PopupInfo(BaseModel):
    """弹窗信息"""
    title: str
    content: str
    close_button: Optional[Coordinate] = None


class PageAnalysis(BaseModel):
    """完整页面分析结果"""
    # 菜单结构
    level1_dir: Direction
    level1_menus: list[MenuInfo]
    level2_dir: Direction
    level2_menus: list[MenuInfo]

    # 当前位置
    current_path: list[str]

    # 内容项
    items: list[MenuItem]

    # 特殊元素
    is_popup: bool = False
    popup_info: Optional[PopupInfo] = None
    close_button: Optional[Coordinate] = None
    back_button: Optional[Coordinate] = None

    # 导航提示
    has_scroll: bool = False
    is_end_of_list: bool = False
```

### 6.4 Vision Prompt 模板

#### 6.4.1 屏幕结构分析 Prompt

```python
PROMPT_STRUCTURE = """You are analyzing a mobile app screen for UI traversal.

Analyze this screenshot and provide:
1. Menu structure (level 1 and level 2 menus with their positions and active state)
2. Current path (which menus are currently active/highlighted)
3. All clickable items in the content area with BUTTON TYPE CLASSIFICATION
4. Any popups, dialogs, or special UI elements

BUTTON TYPE CLASSIFICATION:
For each item, determine its type and expected behavior:

Types:
- menu_item: List items that navigate to sub-pages (e.g., settings entries)
- tab: Tab buttons that switch between top-level views
- back_button: Back/return navigation buttons
- switch: On/off toggle switches (typically with sliding animation)
- toggle: Buttons that toggle between states (e.g., favorite buttons)
- button: Generic action buttons (triggers operations, dialogs, etc.)
- link: Navigation links or hypertext
- icon: Icon-only buttons without text labels
- text: Non-interactive text elements
- readonly: Elements that display but don't respond to clicks

Expected Actions:
- navigate: Button will change the current page/view (menu_item, tab, back_button)
- toggle: Button will change UI state without page change (switch, toggle)
- action: Button triggers an operation (button, link) - may show popup or jump
- none: No expected response (readonly, text)

Field Guidelines:
- expects_page_change: true for navigate/action, false for toggle/none
- expects_state_change: true for toggle, false for navigate/action/none

Return JSON with this exact structure:
{
  "level1_dir": "left|right|top|bottom",
  "level1_menus": [{"name": "menu_name", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "level2_dir": "left|right|top|bottom",
  "level2_menus": [{"name": "tab_name", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "current_path": ["level1_name", "level2_name"],
  "items": [
    {
      "name": "item_name",
      "type": "menu_item|tab|back_button|switch|toggle|button|link|icon|text|readonly",
      "expected_action": "navigate|toggle|action|none",
      "expects_page_change": true|false,
      "expects_state_change": true|false,
      "x": 0.0-1.0,
      "y": 0.0-1.0,
      "parent": "parent_name_or_null"
    }
  ],
  "is_popup": false,
  "popup_info": {"title": "...", "content": "...", "close_button": {"x": 0.0, "y": 0.0}} or null,
  "close_button": {"x": 0.0, "y": 0.0} or null,
  "back_button": {"x": 0.0, "y": 0.0} or null,
  "has_scroll": false,
  "is_end_of_list": false
}

Important:
- All coordinates must be normalized 0-1 (relative to screen size)
- Mark parent-child relationships using the "parent" field
- Use current_path to indicate which menus are currently active
- For icons without text, name them like "[icon] description"
- Include all interactive elements, not just text
- Default to expected_action="action" if uncertain
- Use expects_page_change=true for navigate/action types
- Use expects_state_change=true only for toggle types
"""
```

#### 6.4.2 应用入口查找 Prompt

```python
PROMPT_FIND_ENTRY = """You are helping to navigate to a specific app on a mobile device.

Target app: "{target}"

Analyze this screenshot and find the app icon. Return JSON:
{
  "found": true|false,
  "name": "exact_app_name",
  "x": 0.0-1.0,
  "y": 0.0-1.0,
  "confidence": 0.0-1.0
}

If not found, set found=false and return null coordinates.
"""
```

### 6.5 Vision 与 AI 能力集成

Vision 能力输出的 `PageAnalysis` 是其他四个 AI 能力的关键输入：

| AI 能力 | 动力来源 | Vision 输入用途 | 使用的 PageAnalysis 字段 |
|---------|----------|----------------|------------------------|
| **VisionAnalysisCapability** | **Vision Service** | 产生 `PageAnalysis` | - |
| ParseTaskCapability | DeepSeek LLM | 无需 Vision | - |
| PageVerifyCapability | DeepSeek LLM | 页面类型验证 | `level1_menus`, `level2_menus`, `items`, `is_popup`, `current_path` |
| ElementScreenCapability | DeepSeek LLM | 元素安全筛选 | `items`, `current_path`, `is_popup` |
| DecisionCapability | DeepSeek LLM | 上下文决策 | `items`, `current_path`, `is_popup`, `popup_info`, `back_button` |

#### 6.5.1 PageAnalysis → Prompt 变量映射

```python
def page_analysis_to_variables(
    page: PageAnalysis,
    expected_type: Optional[str] = None,
) -> Dict:
    """将 PageAnalysis 转换为 AI Prompt 变量"""

    # 格式化元素列表
    elements_detail = "\\n".join([
        f"{item.name}|{item.type}|{item.expected_action}|[{item.coordinate.x},{item.coordinate.y}]"
        for item in page.items
    ])

    # 格式化一级菜单
    level1_menus_summary = ", ".join([m.name for m in page.level1_menus])

    # 格式化二级菜单
    level2_menus_summary = ", ".join([m.name for m in page.level2_menus])

    # 格式化当前路径
    current_path = str(page.current_path)

    # 弹窗信息
    popup_info = (
        f"标题: {page.popup_info.title}, 按钮: 确定/取消"
        if page.popup_info else "无"
    )

    return {
        "expected_type": expected_type or "auto_detect",
        "current_path": current_path,
        "is_popup": str(page.is_popup),
        "level1_menus_summary": level1_menus_summary,
        "level2_menus_summary": level2_menus_summary,
        "elements_detail": elements_detail,
        "popup_info": popup_info,
        "page_type": _infer_page_type(page),  # 从 Vision 推断
    }

def _infer_page_type(page: PageAnalysis) -> str:
    """根据 PageAnalysis 推断页面类型"""
    if page.is_popup:
        return "dialog"
    if page.level1_menus and len(page.items) > 0:
        menu_ratio = sum(1 for i in page.items if i.type == MenuItemType.MENU_ITEM) / len(page.items)
        if menu_ratio > 0.7:
            return "menu_list"
    if not page.items:
        return "leaf_page"
    return "settings_group"
```

### 6.6 Vision 服务实现类

#### 6.6.1 ClaudeVisionService

使用官方 Claude API 的 Vision 服务：

```python
class ClaudeVisionService(VisionService):
    """使用 Claude API 的 Vision 服务"""

    def __init__(self, api_key: str, model: str = "claude-3-5-sonnet-20241022"):
        self.client = Anthropic(api_key=api_key)
        self.model = model

    def _call_vision(self, prompt: str, image_data: bytes) -> str:
        """调用 Claude Vision API"""
        image_base64 = BaseVisionService._encode_image_base64(image_data)

        message = self.client.messages.create(
            model=self.model,
            max_tokens=4096,
            messages=[{
                "role": "user",
                "content": [
                    {"type": "text", "text": prompt},
                    {
                        "type": "image",
                        "source": {
                            "type": "base64",
                            "media_type": "image/png",
                            "data": image_base64,
                        },
                    },
                ],
            }],
        )

        return message.content[0].text
```

#### 6.6.2 MiMoVisionService

使用小米 MiMo API 的 Vision 服务（如果存在）：

```python
class MiMoVisionService(VisionService):
    """使用小米 MiMo API 的 Vision 服务"""

    def __init__(self, api_key: str):
        self.api_key = api_key
        self.base_url = "https://mimo.api.xiaomi.com/v1"

    def _call_vision(self, prompt: str, image_data: bytes) -> str:
        """调用 MiMo Vision API"""
        # MiMo API 实现细节
        pass
```

#### 6.6.3 MockVisionService

用于测试的 Mock Vision 服务：

```python
class MockVisionService(VisionService):
    """Mock Vision 服务用于测试"""

    def __init__(self):
        self._responses: list[PageAnalysis] = []

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """返回预设的 mock 响应"""
        if self._responses:
            return self._responses.pop(0)
        return _get_default_mock_analysis()

    def add_response(self, response: PageAnalysis) -> None:
        """添加预设响应"""
        self._responses.append(response)
```

### 6.7 Vision 服务配置

```python
@dataclass
class VisionConfig:
    """Vision 服务配置"""
    service_type: Literal["claude", "mimo", "mock"] = "claude"
    api_key: str = ""
    model: str = "claude-3-5-sonnet-20241022"
    timeout: float = 30.0
    max_retries: int = 3

def create_vision_service(config: VisionConfig) -> VisionService:
    """工厂函数：创建 Vision 服务"""
    if config.service_type == "claude":
        return ClaudeVisionService(
            api_key=config.api_key,
            model=config.model,
        )
    elif config.service_type == "mimo":
        return MiMoVisionService(api_key=config.api_key)
    elif config.service_type == "mock":
        return MockVisionService()
    else:
        raise ValueError(f"Unknown service type: {config.service_type}")
```

### 6.8 Vision 服务与 UniBrain 集成

Vision 服务作为第五个能力集成到 UniBrain 中，但使用独立的 Vision Service（Claude/MiMo）而不是 DeepSeek LLM。

```python
class UniBrain(AIStrategyAdvisor):
    """DeepSeek V4 Flash 实现的 AI Provider（集成 Vision）"""

    def __init__(
        self,
        ai_config: AIProviderConfig,
        vision_service: VisionService,  # 注入 Vision 服务
    ):
        # 初始化核心组件
        self.client = LLMClient(ai_config)
        self.validator = ResponseValidator()
        self.config = ai_config
        self.vision_service = vision_service

        # 初始化 Prompt Registry
        self.prompt_registry = PromptRegistry(ai_config)

        # 注册解析器
        self._register_parsers()

        # 初始化五个能力
        # 注意：VisionAnalysisCapability 使用 Vision Service，其他能力使用 DeepSeek LLM
        self.capabilities = {
            "parse": ParseTaskCapability(self.client, self.validator, ai_config, self.prompt_registry),
            "verify": VerifyPageTypeCapability(self.client, self.validator, ai_config, self.prompt_registry),
            "safety": ScreenSafetyCapability(self.client, self.validator, ai_config, self.prompt_registry),
            "vision": VisionAnalysisCapability(self.vision_service, self.validator),  # 使用 Vision Service
            "decision": ContextDecisionCapability(self.client, self.validator, ai_config, self.prompt_registry),
        }

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """使用 Vision 能力分析屏幕截图"""
        return self.capabilities["vision"].execute(image_data)

    def verify_page_with_vision(
        self,
        image_data: bytes,
        expected_type: str,
    ) -> PageTypeVerification:
        """结合 Vision 验证页面类型"""
        # 1. 使用 Vision 分析屏幕
        page_analysis = self.analyze_screenshot(image_data)

        # 2. 使用 AI 验证页面类型
        verification = self.capabilities["verify"].execute({
            "page_analysis": page_analysis,
            "expected_type": expected_type,
        })

        return verification
```

### 6.9 Vision 错误处理

```python
class VisionError(Exception):
    """Vision 服务错误"""
    pass

# 错误处理策略
def handle_vision_error(error: Exception, context: str) -> None:
    """处理 Vision 服务错误"""
    if isinstance(error, VisionError):
        logger.error(f"Vision error in {context}: {error}")
        # 可选：降级到 Mock 服务或重试
    else:
        logger.exception(f"Unexpected error in {context}: {error}")
```

---

## 7. Prompt 模板变量系统（原版，已整合）

### 7.1 变量注入机制

```python
class VariableInjector:
    """模板变量注入器"""

    def inject(self, template: str, variables: Dict) -> str:
        """
        注入变量到模板

        支持的变量类型:
        - 普通字符串: {variable_name}
        - 可选块: {{optional:variable_name}}
        - 推理级别: {{REASONING_LEVEL}}
        """
        result = template
        result = result.replace("{{REASONING_LEVEL}}", self._get_reasoning_prompt())

        for key, value in variables.items():
            placeholder = f"{{{key}}}"
            result = result.replace(placeholder, str(value))

        return result

    def _get_reasoning_prompt(self) -> str:
        """获取推理级别的提示"""
        levels = {
            "concise": "简要说明",
            "step_by_step": "分步骤说明",
            "detailed": "详细分析每个因素",
        }
        return levels.get(self.config.reasoning_detail, "详细分析")
```

### 7.2 上下文压缩策略

为了最小化 token 使用：

| 能力 | 包含的上下文 | 排除的上下文 |
|------|-------------|-------------|
| ParseToPlan | 仅指令 | 无页面信息 |
| VerifyPageType | 仅 PageAnalysis | 无遍历历史 |
| ScreenSafety | 仅 PageAnalysis | 无遍历历史 |
| ContextDecision | PageAnalysis + 简化上下文 | 完整 action_history |

**上下文简化示例**：
```python
def prepare_context(self, context: TraversalContext) -> Dict:
    """简化的上下文准备"""
    return {
        "current_path": context.current_path,
        "visited_pages": list(context.visited_pages.keys())[-10:],  # 仅最近 10 个
        "failed_nodes": list(context.failed_nodes)[-5:],            # 仅最近 5 个
        "goal_attempts": context.goal_attempts,
        # 不包含完整的 action_history 和 inference_history
    }
```

---

## 8. 错误处理与降级策略

### 8.1 错误处理流程

```
┌─────────────────┐
│   Capability    │
│   Execute       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐      ┌──────────────────┐
│   LLM Call      │ NO   │  Retry?          │
│   Success?      │─────▶│  (Exponential)   │
└────────┬────────┘      └──────────────────┘
         │                        │
        YES                       ▼
         │                ┌──────────────────┐
         ▼                │  Max Attempts?   │
┌─────────────────┐        └────┬─────────────┘
│   Validate &    │             │
│   Parse         │             YES
└────────┬────────┘             │
         │                     ▼
         ▼              ┌──────────────────┐
┌─────────────────┐      │   Archive        │
│   Internal     │      │   + Exit         │
│   Validation?  │      └──────────────────┘
└────────┬────────┘
         │
        YES
         ▼
┌─────────────────┐
│   Return        │
│   Result        │
└─────────────────┘
```

### 8.2 降级策略

**混合降级（Partial Fallback）**：
- 配置 `fallback.strategy = "partial"`
- 配置 `fallback.partial_allowlist = ["VerifyPageType"]`
- 当非关键能力失败时，记录日志并继续
- 当关键能力失败时，归档并退出

**关键能力定义**：
- `ParseToPlan`: **关键** - 无法解析则无法继续
- `ContextDecision`: **关键** - 决策失败则无法继续
- `ScreenSafety`: **关键** - 安全筛选失败必须降级到安全模式
- `VerifyPageType`: **非关键** - 验证失败可使用默认值
- `VisionAnalysis`: **关键** - 视觉分析失败无法获取页面信息

**ScreenSafety 安全降级策略**：
当 `ScreenSafety` 能力失败时，系统自动进入**安全模式**：
1. **仅允许安全操作**：只允许 back、skip、no_action
2. **禁止所有点击**：不点击任何页面元素
3. **返回上一级**：执行 back 操作回到已知安全状态
4. **记录安全事件**：归档失败记录并标记为安全事件

```python
class SafetyFallbackHandler:
    """安全降级处理器"""

    SAFE_ACTIONS_ONLY = {"back", "skip", "no_action"}

    def handle_safety_failure(self, error: Exception) -> DecisionResult:
        """处理安全筛选失败"""
        logger.critical(f"Safety screening failed: {error}. Entering SAFE MODE.")

        # 记录安全事件
        FailureArchiver.archive(FailureRecord(
            capability="ScreenSafety",
            error_type="SAFETY_FAILURE",
            error_message=str(error),
            timestamp=datetime.now().isoformat(),
        ))

        # 返回安全操作：返回上一级
        return DecisionResult(
            result="safe_mode",
            action="back",
            target=None,
            reasoning="Safety screening failed - entering safe mode, going back",
            confidence=1.0,  # 安全模式具有最高优先级
        )
```

### 8.3 失败归档

```python
@dataclass
class FailureRecord:
    """失败记录"""
    timestamp: str
    capability: str
    input_data: Dict
    error_type: str
    error_message: str
    stack_trace: Optional[str]
    config_snapshot: Dict

class FailureArchiver:
    """失败归档器"""

    def archive(self, record: FailureRecord) -> None:
        """归档失败记录"""
        with open("failures.jsonl", "a") as f:
            f.write(record.json() + "\n")

        # 同时写入审计日志
        logger.critical(f"AI Capability Failed: {record.capability} - {record.error_message}")
```

---

## 9. 与 AIStrategyAdvisor 接口集成

### 7.1 UniBrain 实现

```python
class UniBrain(AIStrategyAdvisor):
    """DeepSeek V4 Flash 实现的 AI Provider"""

    def __init__(self, config: AIProviderConfig):
        # 初始化核心组件
        self.client = LLMClient(config)
        self.validator = ResponseValidator()
        self.config = config

        # 注册解析器
        self._register_parsers()

        # 初始化能力
        self.capabilities = {
            "parse": ParseToPlanCapability(self.client, self.validator, config),
            "verify": VerifyPageTypeCapability(self.client, self.validator, config),
            "safety": ScreenSafetyCapability(self.client, self.validator, config),
            "decision": ContextDecisionCapability(self.client, self.validator, config),
        }

    def _register_parsers(self) -> None:
        """注册响应解析器"""
        self.validator.register_parser("TraversalPlan", self._parse_traversal_plan)
        self.validator.register_parser("PageTypeVerification", self._parse_page_verification)
        self.validator.register_parser("SafetyScreeningResult", self._parse_safety_result)
        self.validator.register_parser("ContextDecisionResult", self._parse_context_decision)

    def infer_container_type(
        self,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> ContainerInference:
        """推断容器类型（使用 VerifyPageType 能力）"""
        verification = self.capabilities["verify"].execute(
            {"page_analysis": ui, "expected_type": "auto_detect"}
        )
        return ContainerInference(
            container_type=verification.detected_type,
            confidence=verification.confidence,
        )

    def decide_next_action(
        self,
        goal: DecisionGoal,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[TraversalNode]]:
        """决定下一步操作（使用 ContextDecision 能力）"""
        decision = self.capabilities["decision"].execute({
            "goal": goal,
            "page_analysis": ui,
            "context": context,
        })

        if decision.confidence < 0.7:
            return DecisionResult.UNSURE, None

        node = TraversalNode(
            action=decision.action,
            target=decision.target,
            input_value=decision.input_value,
        )
        return DecisionResult.SUCCESS, node

    def handle_exception(
        self,
        exception: ExceptionContext,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[TraversalNode]]:
        """处理异常（使用 ContextDecision 能力）"""
        # 将异常转换为决策目标
        recovery_goal = DecisionGoal(
            type="exception_recovery",
            description=f"Recover from: {exception.type}",
        )

        return self.decide_next_action(recovery_goal, ui, context)
```

### 7.2 解析器实现

```python
def _parse_traversal_plan(self, response: Dict) -> TraversalPlan:
    """解析遍历计划响应"""
    return TraversalPlan(
        target_container=response["target_container"],
        scan_mode=response["scan_mode"],
        node_strategies=[
            NodeStrategy(**s) for s in response["node_strategies"]
        ],
        reasoning=response["reasoning"],
        confidence=response["confidence"],
    )

def _parse_context_decision(self, response: Dict) -> ContextDecisionResult:
    """解析上下文决策响应"""
    return ContextDecisionResult(
        action=response["action"],
        target=response.get("target"),
        input_value=response.get("input_value"),
        reasoning=response["reasoning"],
        confidence=response["confidence"],
    )
```

---

## 10. 配置与部署

### 8.1 环境变量

```bash
# DeepSeek API 配置
DEEPSEEK_API_KEY=your_api_key
DEEPSEEK_MODEL=deepseek-v4-flash
DEEPSEEK_BASE_URL=https://api.deepseek.com/v1

# AI Provider 配置
AI_PROVIDER_MAX_CONCURRENT=4
AI_PROVIDER_TIMEOUT=30.0
AI_PROVIDER_REASONING_LEVEL=detailed
AI_PROVIDER_ENABLE_VALIDATION=true

# 重试配置
AI_RETRY_MAX_ATTEMPTS=1
AI_RETRY_BASE_DELAY=1.0
AI_RETRY_MAX_DELAY=8.0

# 降级配置
AI_FALLBACK_STRATEGY=partial
AI_FALLBACK_ALLOWLIST=VerifyPageType,ScreenSafety
```

### 8.2 Python 配置

```python
from src.ai.provider import AIProviderConfig, RetryConfig, FallbackConfig
from src.ai.deepseek_provider import UniBrain

# 创建配置
config = AIProviderConfig(
    api_key=os.getenv("DEEPSEEK_API_KEY"),
    model=os.getenv("DEEPSEEK_MODEL", "deepseek-v4-flash"),
    base_url=os.getenv("DEEPSEEK_BASE_URL"),
    max_concurrent_requests=int(os.getenv("AI_PROVIDER_MAX_CONCURRENT", 4)),
    request_timeout=float(os.getenv("AI_PROVIDER_TIMEOUT", 30.0)),
    reasoning_detail=os.getenv("AI_PROVIDER_REASONING_LEVEL", "detailed"),
    enable_internal_validation=os.getenv("AI_PROVIDER_ENABLE_VALIDATION", "true").lower() == "true",
    retry=RetryConfig(
        max_attempts=int(os.getenv("AI_RETRY_MAX_ATTEMPTS", 1)),
        base_delay=float(os.getenv("AI_RETRY_BASE_DELAY", 1.0)),
        max_delay=float(os.getenv("AI_RETRY_MAX_DELAY", 8.0)),
    ),
    fallback=FallbackConfig(
        strategy=os.getenv("AI_FALLBACK_STRATEGY", "partial"),
        partial_allowlist=os.getenv("AI_FALLLOW_ALLOWLIST", "").split(","),
    ),
)

# 创建 Provider
provider = UniBrain(config)

# 集成到 TraversalEngine
engine = TraversalEngine(
    adb_client=adb,
    vision_service=vision,
    state=state,
    config=config,
    ai_advisor=provider,  # 注入 AI Provider
)
```

---

## 11. 测试策略

### 9.1 单元测试

```python
class TestBaseCapability:
    """测试 BaseCapability 泛型基类"""

    def test_prepare_input_abstract(self):
        """测试 prepare_input 是抽象方法"""
        with pytest.raises(TypeError):
            BaseCapability()

    def test_execute_async_logs_duration(self, caplog):
        """测试执行记录耗时"""
        # Mock implementation
        class MockCapability(BaseCapability[str, int]):
            @property
            def prompt_template(self) -> str:
                return "Test"

            @property
            def response_schema(self) -> Dict:
                return {"type": "object"}

            @property
            def response_type(self) -> str:
                return "MockResult"

            def prepare_input(self, raw_input: str) -> Dict:
                return {"input": raw_input}

        capability = MockCapability(mock_client, mock_validator, mock_config)
        # Test execution...
```

### 9.2 集成测试

```python
class TestUniBrainIntegration:
    """测试 UniBrain 与 TraversalEngine 集成"""

    def test_parse_to_plan_integration(self):
        """测试指令解析集成"""
        provider = UniBrain(test_config)

        plan = provider.capabilities["parse"].execute(
            "遍历系统设置所有项"
        )

        assert plan.target_container is not None
        assert plan.confidence > 0.7

    def test_container_inference_integration(self):
        """测试容器推断集成"""
        provider = UniBrain(test_config)

        mock_ui = PageAnalysis(
            level1_menus=[MenuInfo(name="Settings", ...)],
            # ...
        )

        result = provider.infer_container_type(mock_ui, mock_context)

        assert result.container_type != "UNKNOWN"
```

### 9.3 端到端测试

```python
@pytest.mark.integration
class TestE2EWithRealDevice:
    """使用真实设备的端到端测试"""

    def test_full_traversal_with_ai(self):
        """测试完整遍历流程"""
        # 真实 ADB + Vision
        adb = RealADBClient()
        vision = MiMoVisionService(api_key=os.getenv("MIMO_API_KEY"))

        # AI Provider
        config = AIProviderConfig(api_key=os.getenv("DEEPSEEK_API_KEY"))
        provider = UniBrain(config)

        # Engine
        engine = TraversalEngine(
            adb_client=adb,
            vision_service=vision,
            state=StateManager(".test_state").state,
            config=TraversalConfig(enable_ai_advisor=True),
            ai_advisor=provider,
        )

        # 运行
        summary = engine.run()

        # 验证
        assert summary["total_nodes"] > 0
        assert summary["ai_calls"] > 0
```

---

## 12. 监控与可观测性

### 10.1 统一日志格式

```python
logger.info(
    "ai_capability_call",
    extra={
        "capability": "ParseToPlan",
        "input_hash": hashlib.sha256(json.dumps(input_data).encode()).hexdigest()[:8],
        "duration_ms": duration * 1000,
        "confidence": result.confidence,
        "token_estimate": estimate_tokens(prompt),
    }
)
```

### 10.2 指标收集

```python
class AIMetrics:
    """AI 指标收集器"""

    def __init__(self):
        self.call_count = Counter()
        self.call_duration = Histogram()
        self.confidence_distribution = Histogram()
        self.error_count = Counter()

    def record_call(
        self,
        capability: str,
        duration: float,
        confidence: float,
        success: bool,
    ) -> None:
        """记录一次调用"""
        self.call_count.labels(capability=capability, success=success).inc()
        self.call_duration.labels(capability=capability).observe(duration)

        if success:
            self.confidence_distribution.labels(capability=capability).observe(confidence)
        else:
            self.error_count.labels(capability=capability).inc()
```

### 10.3 性能指标与 SLA

```python
class PerformanceMetrics:
    """性能指标收集器"""

    def __init__(self):
        # 延迟指标
        self.p50_latency = Histogram()
        self.p95_latency = Histogram()
        self.p99_latency = Histogram()

        # 吞吐量指标
        self.requests_per_second = Gauge()
        self.concurrent_requests = Gauge()

        # Token 使用指标
        self.tokens_per_request = Histogram()
        self.total_tokens_used = Counter()

        # 缓存性能
        self.cache_hit_rate = Gauge()
        self.cache_latency = Histogram()

    def record_latency(self, capability: str, latency: float) -> None:
        """记录延迟"""
        self.p50_latency.labels(capability=capability).observe(latency)
        self.p95_latency.labels(capability=capability).observe(latency)
        self.p99_latency.labels(capability=capability).observe(latency)

    def record_token_usage(self, capability: str, input_tokens: int, output_tokens: int) -> None:
        """记录 Token 使用"""
        total = input_tokens + output_tokens
        self.tokens_per_request.labels(capability=capability).observe(total)
        self.total_tokens_used.labels(capability=capability).inc(total)
```

### 10.4 性能 SLA 定义

| 能力 | P50 延迟 | P95 延迟 | P99 延迟 | 超时阈值 |
|------|----------|----------|----------|----------|
| ParseToPlan | < 2s | < 5s | < 10s | 30s |
| VerifyPageType | < 1s | < 3s | < 5s | 30s |
| ScreenSafety | < 1.5s | < 4s | < 8s | 30s |
| VisionAnalysis | < 3s | < 8s | < 15s | 30s |
| ContextDecision | < 1s | < 2.5s | < 5s | 30s |

---

## 13. 性能优化策略

### 13.1 并发处理策略

**适用场景**：当需要同时评估多个元素的安全性时

```python
class ConcurrentSafetyEvaluator:
    """并发安全评估器"""

    def __init__(self, max_workers: int = 4):
        self.executor = concurrent.futures.ThreadPoolExecutor(max_workers=max_workers)

    async def evaluate_elements_concurrent(
        self,
        elements: List[MenuItem],
        page_analysis: PageAnalysis,
        instruction: str,
    ) -> List[SafetyEvaluation]:
        """并发评估多个元素的安全性"""
        # 创建评估任务
        tasks = [
            self.capabilities["safety"].execute_async({
                "element": element,
                "page_analysis": page_analysis,
                "instruction": instruction,
            })
            for element in elements
        ]

        # 并发执行
        results = await asyncio.gather(*tasks, return_exceptions=True)

        # 处理结果
        evaluations = []
        for element, result in zip(elements, results):
            if isinstance(result, Exception):
                # 保守处理：未知元素标记为 caution
                evaluations.append(SafetyEvaluation(
                    name=element.name,
                    safety_tag="caution",
                    confidence=0.0,
                    reason=f"评估失败: {str(result)}",
                ))
            else:
                evaluations.append(result)

        return evaluations
```

**并发控制原则**：
- Vision 调用：串行执行（API 限制）
- DeepSeek 调用：最大 4 个并发
- 同一元素类型：可以并发
- 不同页面：必须串行（依赖关系）

### 13.2 缓存策略

#### 13.2.1 响应缓存

```python
@dataclass
class CacheConfig:
    """缓存配置"""
    enabled: bool = True
    ttl_seconds: int = 3600  # 1 小时
    max_size: int = 1000
    cache_key_strategy: Literal["hash", "semantic"] = "hash"

class ResponseCache:
    """AI 响应缓存"""

    def __init__(self, config: CacheConfig):
        self.config = config
        self._cache: Dict[str, Tuple[Any, float]] = {}
        self._lock = asyncio.Lock()

    async def get_or_compute(
        self,
        cache_key: str,
        compute_fn: Callable,
    ) -> Any:
        """获取缓存或计算"""
        if not self.config.enabled:
            return await compute_fn()

        async with self._lock:
            # 检查缓存
            if cache_key in self._cache:
                result, timestamp = self._cache[cache_key]
                if time.time() - timestamp < self.config.ttl_seconds:
                    logger.info(f"Cache hit: {cache_key}")
                    return result

        # 计算结果
        result = await compute_fn()

        # 存入缓存
        async with self._lock:
            self._cache[cache_key] = (result, time.time())

            # 清理过期缓存
            if len(self._cache) > self.config.max_size:
                self._cleanup_cache()

        return result

    def _cleanup_cache(self) -> None:
        """清理过期缓存"""
        now = time.time()
        expired = [
            key for key, (_, timestamp) in self._cache.items()
            if now - timestamp > self.config.ttl_seconds
        ]
        for key in expired:
            del self._cache[key]
```

#### 13.2.2 缓存键生成策略

```python
def generate_cache_key(
    capability: str,
    input_data: Dict,
    strategy: str = "hash",
) -> str:
    """生成缓存键"""
    if strategy == "hash":
        # 基于输入哈希
        content = json.dumps(input_data, sort_keys=True)
        hash_key = hashlib.sha256(content.encode()).hexdigest()[:16]
        return f"{capability}:{hash_key}"

    elif strategy == "semantic":
        # 语义化缓存键（忽略细节差异）
        key_parts = [
            capability,
            input_data.get("page_type", "unknown"),
            str(len(input_data.get("items", []))),  # 元素数量
        ]
        return ":".join(key_parts)
```

**缓存适用场景**：
- ✅ `VerifyPageType` - 同一页面类型验证可缓存
- ✅ `ScreenSafety` - 相同元素组合可缓存
- ⚠️ `ContextDecision` - 仅当状态完全相同时可缓存
- ❌ `ParseToPlan` - 通常不缓存（每次指令可能不同）

### 13.3 Token 优化策略

#### 13.3.1 上下文压缩

```python
class ContextCompressor:
    """上下文压缩器"""

    def compress_traversal_context(
        self,
        context: TraversalContext,
        max_items: int = 10,
    ) -> Dict:
        """压缩遍历上下文"""
        return {
            # 仅保留最近 N 个页面
            "visited_pages": list(context.visited_pages.keys())[-max_items:],

            # 仅保留最近 N 个失败节点
            "failed_nodes": list(context.failed_nodes)[-max_items // 2:],

            # 统计信息代替完整历史
            "total_visits": len(context.visited_pages),
            "total_failures": len(context.failed_nodes),

            # 当前路径
            "current_path": context.current_path,

            # 当前深度
            "current_depth": len(context.current_path),
        }

    def compress_elements(
        self,
        elements: List[MenuItem],
        max_elements: int = 20,
    ) -> List[Dict]:
        """压缩元素列表"""
        if len(elements) <= max_elements:
            return [self._element_to_dict(e) for e in elements]

        # 优先级排序
        priority_elements = [
            e for e in elements
            if e.type in [MenuItemType.BUTTON, MenuItemType.SWITCH]
        ]
        other_elements = [
            e for e in elements
            if e.type not in [MenuItemType.BUTTON, MenuItemType.SWITCH]
        ]

        # 组合：高优先级 + 部分低优先级
        combined = (
            priority_elements[:max_elements // 2] +
            other_elements[:max_elements - len(priority_elements[:max_elements // 2])]
        )

        return [self._element_to_dict(e) for e in combined]

    def _element_to_dict(self, element: MenuItem) -> Dict:
        """元素转字典（精简字段）"""
        return {
            "name": element.name,
            "type": element.type.value,
            "expected_action": element.expected_action.value,
            "coordinate": {"x": element.coordinate.x, "y": element.coordinate.y},
        }
```

#### 13.3.2 Token 使用监控

```python
class TokenUsageTracker:
    """Token 使用跟踪器"""

    def __init__(self):
        self.usage_by_capability: Dict[str, List[int]] = {}
        self.alert_threshold = 100_000  # 10 万 token 警告

    def track_usage(self, capability: str, input_tokens: int, output_tokens: int) -> None:
        """跟踪 Token 使用"""
        if capability not in self.usage_by_capability:
            self.usage_by_capability[capability] = []

        self.usage_by_capability[capability].append(input_tokens + output_tokens)

        # 检查是否超过阈值
        total = sum(self.usage_by_capability[capability])
        if total > self.alert_threshold:
            logger.warning(
                f"Token usage alert: {capability} used {total} tokens"
            )

    def get_usage_report(self) -> Dict:
        """获取使用报告"""
        return {
            capability: {
                "total": sum(usage),
                "avg": sum(usage) / len(usage) if usage else 0,
                "count": len(usage),
            }
            for capability, usage in self.usage_by_capability.items()
        }
```

### 13.4 批处理策略

```python
class BatchRequestProcessor:
    """批量请求处理器"""

    def __init__(self, batch_size: int = 5, max_wait_time: float = 1.0):
        self.batch_size = batch_size
        self.max_wait_time = max_wait_time
        self._pending_requests: List = []
        self._batch_timer: Optional[asyncio.Task] = None

    async def submit_request(
        self,
        capability: BaseCapability,
        input_data: Any,
    ) -> Any:
        """提交请求到批处理队列"""
        future = asyncio.Future()
        self._pending_requests.append((capability, input_data, future))

        # 达到批次大小或超时则执行
        if len(self._pending_requests) >= self.batch_size:
            await self._process_batch()
        elif self._batch_timer is None:
            self._batch_timer = asyncio.ensure_future(self._batch_timer_callback())

        return await future

    async def _batch_timer_callback(self) -> None:
        """批次定时器回调"""
        await asyncio.sleep(self.max_wait_time)
        if self._pending_requests:
            await self._process_batch()

    async def _process_batch(self) -> None:
        """处理批次请求"""
        if not self._pending_requests:
            return

        batch = self._pending_requests[:]
        self._pending_requests = []

        # 并发执行批次中的请求
        results = await asyncio.gather(*[
            capability.execute_async(input_data)
            for capability, input_data, _ in batch
        ], return_exceptions=True)

        # 设置 Future 结果
        for (_, _, future), result in zip(batch, results):
            if isinstance(result, Exception):
                future.set_exception(result)
            else:
                future.set_result(result)
```

### 13.5 性能基准测试

```python
class PerformanceBenchmark:
    """性能基准测试"""

    async def benchmark_capability(
        self,
        capability: BaseCapability,
        test_cases: List[Dict],
        iterations: int = 10,
    ) -> Dict:
        """对能力进行基准测试"""
        latencies = []

        for _ in range(iterations):
            for test_case in test_cases:
                start = time.time()
                try:
                    await capability.execute_async(test_case)
                    latency = time.time() - start
                    latencies.append(latency)
                except Exception as e:
                    logger.error(f"Benchmark failed: {e}")

        # 计算统计数据
        return {
            "p50": np.percentile(latencies, 50),
            "p95": np.percentile(latencies, 95),
            "p99": np.percentile(latencies, 99),
            "avg": np.mean(latencies),
            "min": np.min(latencies),
            "max": np.max(latencies),
            "count": len(latencies),
        }

    async def run_all_benchmarks(self) -> Dict:
        """运行所有能力的基准测试"""
        results = {}

        # ParseToPlan 基准
        results["ParseToPlan"] = await self.benchmark_capability(
            self.capabilities["parse"],
            test_cases=[{"instruction": "遍历系统设置"}] * 10,
        )

        # VerifyPageType 基准
        results["VerifyPageType"] = await self.benchmark_capability(
            self.capabilities["verify"],
            test_cases=[{"page_analysis": mock_page(), "expected_type": "settings"}] * 10,
        )

        # ScreenSafety 基准
        results["ScreenSafety"] = await self.benchmark_capability(
            self.capabilities["safety"],
            test_cases=[{"page_analysis": mock_page(), "instruction": "test"}] * 10,
        )

        # ContextDecision 基准
        results["ContextDecision"] = await self.benchmark_capability(
            self.capabilities["decision"],
            test_cases=[{"goal": "navigate", "page_analysis": mock_page()}] * 10,
        )

        return results
```

---

## 14. 实施路线图

### Phase 1: 核心基础设施
1. 实现 `AIProviderConfig` 数据类
2. 实现 `LLMClient` 带重试逻辑
3. 实现 `ResponseValidator` 注册解析器模式
4. 实现 `BaseCapability` 泛型基类
5. 单元测试覆盖

### Phase 2: 五个能力实现
1. `ParseToPlanCapability` 实现（DeepSeek LLM）
2. `VerifyPageTypeCapability` 实现（DeepSeek LLM）
3. `ScreenSafetyCapability` 实现（DeepSeek LLM）
4. `VisionAnalysisCapability` 实现（Vision Service）
5. `ContextDecisionCapability` 实现（DeepSeek LLM）
6. 各能力的 Prompt 模板优化
7. 单元测试 + 集成测试

### Phase 3: Provider 集成
1. 实现 `UniBrain`
2. 实现 `AIStrategyAdvisor` 接口方法
3. 注册解析器
4. 集成测试

### Phase 4: 可观测性
1. 统一日志格式
2. 指标收集
3. 失败归档
4. 监控仪表板（可选）

### Phase 5: 端到端验证
1. Mock 设备完整流程测试
2. 真实设备端到端测试
3. 性能基准测试
4. Prompt 效果评估

---

## 15. 开放问题

1. **Prompt 优化迭代**：DeepSeek V4 Flash 的最佳 prompt 格式需要实际测试后迭代
2. **上下文压缩阈值**：visited_pages 和 failed_nodes 的保留数量需要根据实际效果调整
3. **置信度阈值**：0.7 的默认阈值是否合适需要验证
4. **Token 使用监控**：需要建立 token 使用量监控和预警机制

---

## 16. 参考文档

- DeepSeek API 文档: https://api.deepseek.com/docs
- uni-claw 项目 README: `README.md`
- AIStrategyAdvisor 接口: `openspec/specs/ai-strategy-advisor/spec.md`
- AI 集成规格: `openspec/specs/ai-integration/spec.md`
