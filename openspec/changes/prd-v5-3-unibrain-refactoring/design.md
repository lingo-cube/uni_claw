# Design: PRD V5.3 UniBrain架构重构

**Change ID**: `prd-v5-3-unibrain-refactoring`
**Created**: 2026-06-02
**Status**: Design Phase

---

## 1. 架构设计 (Architecture)

### 1.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Uni-Claw 遍历引擎                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    UniBrain (统一接口)                          │   │
│  ├─────────────────────────────────────────────────────────────────┤   │
│  │                                                                 │   │
│  │  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────┐ │   │
│  │  │  能力路由器      │  │  提示词管理器    │  │  追踪集成器  │ │   │
│  │  │  (Router)        │  │  (PromptManager) │  │  (Trace)     │ │   │
│  │  └────────┬─────────┘  └────────┬─────────┘  └──────┬───────┘ │   │
│  │           │                     │                  │           │   │
│  │           └─────────────────────┴──────────────────┘           │   │
│  │                              ↓                                  │   │
│  │           ┌──────────────────────────────────────┐            │   │
│  │           │       Provider抽象层                 │            │   │
│  │           │  ┌────────────────────────────────┐  │            │   │
│  │           │  │     AIProvider (接口)           │  │            │   │
│  │           │  └──────────────┬─────────────────┘  │            │   │
│  │           │                 │                    │            │   │
│  │           │  ┌──────────────┴─────────────────┐  │            │   │
│  │           │  │                               │  │            │   │
│  │           │  │ 具体Provider实现              │  │            │   │
│  │           │  │ ┌─────────┐ ┌─────────┐      │  │            │   │
│  │           │  │ │DeepSeek │ │ Claude  │      │  │            │   │
│  │           │  │ │Provider│ │Provider│      │  │            │   │
│  │           │  │ └────┬────┘ └────┬────┘      │  │            │   │
│  │           │  │       │          │           │  │            │   │
│  │           │  │ ┌─────┴──────┐   │           │  │            │   │
│  │           │  │ │   MiMo     │   │           │  │            │   │
│  │           │  │ │  Provider  │   │           │  │            │   │
│  │           │  │ └────────────┘   │           │  │            │   │
│  │           │  └───────────────────┴───────────┘  │            │   │
│  │           └──────────────────────────────────────┘            │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              ↓                                          │
│                         配置文件 (ai_providers.yaml)                      │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 组件关系图

```
┌──────────────┐      ┌──────────────┐      ┌──────────────┐
│  业务请求     │      │  Provider    │      │  AI响应      │
│  (5大能力)    │  →   │  抽象层      │  →   │  (统一格式)   │
└──────────────┘      └──────────────┘      └──────────────┘
       ↓                      ↓                        ↓
  analyze_visual         AIProvider             AIResponse
  parse_instruction      complete_text          content
  verify_page_type      complete_vision        tokens
  decide_next_action    complete_multimodal    latency
  screen_safety           ...
```

### 1.3 数据流图

```
┌──────────────┐
│ 业务请求      │
│ (analyze_    │
│  visual)     │
└──────┬───────┘
       │
       ▼
┌─────────────────────────────────────────────────────────────────┐
│                    UniBrain                                    │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ 1. 能力路由 (capability → provider_id)                     │ │
│  │    analyze_visual → "claude"                               │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ 2. 获取提示词 (PromptManager)                             │ │
│  │    get_prompt("analyze_visual", "latest")                  │ │
│  │    inject_variables({image_description, context_info})    │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ 3. 开始追踪 (TraceIntegration)                             │ │
│  │    start_span(operation="unibrain.analyze_visual")          │ │
│  │    inject_context({custom business context})              │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ 4. 执行Provider调用                                        │ │
│  │    provider.complete_vision(prompt, image_data)            │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ 5. 结束追踪 + 记录指标                                      │ │
│  │    finish_span(result=AIResponse)                          │ │
│  │    record_metrics(tokens, latency, success)               │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
       │
       ▼
┌──────────────┐
│  返回结果     │
│  (PageAnalysis│
│   + metrics)  │
└──────────────┘
```

---

## 2. Provider抽象层设计 (Provider Abstraction)

### 2.1 AIProvider接口

**文件**: `src/ai/providers/base.py`

```python
from abc import ABC, abstractmethod
from typing import Dict, List, Optional
from dataclasses import dataclass

@dataclass
class AIResponse:
    """AI响应统一格式"""
    content: str
    provider_id: str
    mode: str
    input_tokens: int
    output_tokens: int
    latency_ms: float
    model: str = ""
    
    @property
    def total_tokens(self) -> int:
        return self.input_tokens + self.output_tokens
    
    @property
    def estimated_cost(self) -> float:
        """估算成本"""
        return 0.0  # 子类实现具体计算

class AIProvider(ABC):
    """AI提供者抽象基类"""
    
    def __init__(self, config: AIProviderConfig):
        self.config = config
        self._client = None
    
    @property
    @abstractmethod
    def provider_id(self) -> str:
        """Provider唯一标识"""
        pass
    
    @property
    @abstractmethod
    def supported_modes(self) -> List[str]:
        """支持的模式：text, vision, multimodal"""
        pass
    
    @abstractmethod
    async def complete_text(
        self, 
        prompt: str, 
        schema: Optional[Dict] = None,
        max_tokens: int = 2048,
        **kwargs
    ) -> AIResponse:
        """文本补全能力"""
        pass
    
    @abstractmethod
    async def complete_vision(
        self,
        prompt: str,
        image_data: bytes,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """视觉补全能力"""
        pass
    
    @abstractmethod
    async def complete_multimodal(
        self,
        prompt: str,
        image_data: bytes,
        additional_context: Optional[Dict] = None,
        schema: Optional[Dict] = None,
        max_tokens: int = 4096,
        **kwargs
    ) -> AIResponse:
        """多模态补全能力"""
        pass
    
    def get_token_estimate(
        self, 
        mode: str, 
        avg_request_tokens: int = 500
    ) -> Dict[str, int]:
        """估算token使用量"""
        return {
            "input": avg_request_tokens,
            "output": avg_request_tokens // 2,
            "total": avg_request_tokens + avg_request_tokens // 2
        }
    
    def get_performance_rating(self, mode: str) -> Dict[str, float]:
        """获取Provider性能评级"""
        return {"latency": 0.5, "quality": 0.5, "efficiency": 0.5}
    
    async def health_check(self) -> bool:
        """健康检查"""
        try:
            test_response = await self.complete_text("ping", max_tokens=10)
            return bool(test_response.content)
        except Exception:
            return False
```

### 2.2 DeepSeekProvider实现

**文件**: `src/ai/providers/deepseek.py`

```python
import aiohttp
import logging
from typing import Dict, Optional
from .base import AIProvider, AIResponse

logger = logging.getLogger(__name__)

class DeepSeekProvider(AIProvider):
    """DeepSeek API提供者 - 专注文本处理"""
    
    @property
    def provider_id(self) -> str:
        return "deepseek"
    
    @property
    def supported_modes(self) -> List[str]:
        return ["text"]
    
    async def complete_text(
        self, 
        prompt: str, 
        schema: Optional[Dict] = None,
        max_tokens: int = 2048,
        **kwargs
    ) -> AIResponse:
        """DeepSeek文本补全实现"""
        import time
        start_time = time.time()
        
        payload = {
            "model": self.config.model,
            "messages": [{"role": "user", "content": prompt}],
            "max_tokens": max_tokens,
        }
        
        if schema:
            payload["response_format"] = {"type": "json_object"}
        
        headers = {
            "Authorization": f"Bearer {self.config.api_key}",
            "Content-Type": "application/json",
        }
        
        try:
            timeout = aiohttp.ClientTimeout(total=self.config.request_timeout)
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(
                    f"{self.config.base_url}/chat/completions",
                    headers=headers,
                    json=payload,
                ) as response:
                    if response.status >= 400:
                        error_text = await response.text()
                        raise RuntimeError(f"DeepSeek API error {response.status}: {error_text}")
                    
                    data = await response.json()
                    
                    if "error" in data:
                        raise RuntimeError(f"DeepSeek API error: {data['error']}")
                    
                    message = data["choices"][0]["message"]
                    content = message["content"]
                    
                    usage = data.get("usage", {})
                    input_tokens = usage.get("prompt_tokens", 0)
                    output_tokens = usage.get("completion_tokens", 0)
                    
                    latency_ms = (time.time() - start_time) * 1000
                    
                    logger.info(
                        f"[DeepSeek] Success. Tokens: {input_tokens} in, {output_tokens} out, "
                        f"latency: {latency_ms:.0f}ms"
                    )
                    
                    return AIResponse(
                        content=content,
                        provider_id=self.provider_id,
                        mode="text",
                        input_tokens=input_tokens,
                        output_tokens=output_tokens,
                        latency_ms=latency_ms,
                        model=self.config.model,
                    )
        
        except Exception as e:
            logger.error(f"[DeepSeek] Request failed: {e}")
            raise RuntimeError(f"DeepSeek request failed: {e}") from e
    
    async def complete_vision(self, prompt: str, image_data: bytes, schema: Optional[Dict] = None, **kwargs) -> AIResponse:
        """DeepSeek不支持视觉能力"""
        raise NotImplementedError(f"{self.provider_id} does not support vision mode")
    
    async def complete_multimodal(self, prompt: str, image_data: bytes, additional_context: Optional[Dict] = None, schema: Optional[Dict] = None, **kwargs) -> AIResponse:
        """DeepSeek不支持多模态能力"""
        raise NotImplementedError(f"{self.provider_id} does not support multimodal mode")
```

---

## 3. 提示词管理系统设计 (Prompt Management)

### 3.1 PromptManager实现

**文件**: `src/ai/prompts/manager.py`

```python
import yaml
import json
import logging
from pathlib import Path
from typing import Dict, List, Optional
from dataclasses import dataclass

logger = logging.getLogger(__name__)

@dataclass
class PromptTemplate:
    """提示词模板"""
    capability: str
    version: str
    system_prompt: str
    user_template: str
    variables: List[str]
    metadata: Dict
    
    def format(self, **kwargs) -> str:
        """格式化提示词"""
        missing_vars = set(self.variables) - set(kwargs.keys())
        if missing_vars:
            raise ValueError(f"Missing required variables: {missing_vars}")
        
        user_prompt = self.user_template
        for var_name in self.variables:
            if var_name in kwargs:
                user_prompt = user_prompt.replace(f"{{{var_name}}}", str(kwargs[var_name]))
        
        if self.system_prompt:
            return f"{self.system_prompt}\n\n{user_prompt}"
        return user_prompt

class PromptManager:
    """提示词管理系统"""
    
    def __init__(self, prompt_dir: str = "src/ai/prompts"):
        self.prompt_dir = Path(prompt_dir)
        self._prompts: Dict[str, Dict] = {}
        self._load_prompts()
        logger.info(f"PromptManager initialized with {len(self._prompts)} capabilities")
    
    def _load_prompts(self):
        """加载所有提示词"""
        if not self.prompt_dir.exists():
            logger.warning(f"Prompt directory not found: {self.prompt_dir}")
            return
        
        for prompt_file in self.prompt_dir.glob("*.md"):
            try:
                capability_name = prompt_file.stem
                self._prompts[capability_name] = self._parse_prompt_file(prompt_file)
                logger.debug(f"Loaded prompt for capability: {capability_name}")
            except Exception as e:
                logger.error(f"Failed to load prompt file {prompt_file}: {e}")
    
    def _parse_prompt_file(self, file_path: Path) -> Dict:
        """解析提示词文件"""
        content = file_path.read_text(encoding='utf-8')
        
        if content.startswith("---"):
            try:
                _, front_matter, prompt_body = content.split("---", 2)
                metadata = yaml.safe_load(front_matter)
            except yaml.YAMLError as e:
                logger.warning(f"Invalid YAML in {file_path}: {e}, using empty metadata")
                metadata = {}
                prompt_body = content
        else:
            metadata = {}
            prompt_body = content
        
        return {
            "metadata": metadata,
            "system": metadata.get("system", ""),
            "user": prompt_body.strip(),
            "variables": metadata.get("variables", []),
            "versions": metadata.get("versions", {}),
            "file_path": str(file_path),
        }
    
    def get_prompt(self, capability: str, version: str = "latest") -> PromptTemplate:
        """获取能力提示词"""
        if capability not in self._prompts:
            available = list(self._prompts.keys())
            raise ValueError(
                f"Prompt not found for capability: {capability}. "
                f"Available capabilities: {available}"
            )
        
        prompt_data = self._prompts[capability]
        
        if version != "latest":
            if "versions" not in prompt_data or version not in prompt_data["versions"]:
                raise ValueError(f"Version {version} not found for capability {capability}")
            prompt_data = prompt_data["versions"][version]
        
        metadata = prompt_data.get("metadata", {})
        
        return PromptTemplate(
            capability=capability,
            version=version,
            system_prompt=prompt_data.get("system", ""),
            user_template=prompt_data.get("user", ""),
            variables=prompt_data.get("variables", []),
            metadata=metadata,
        )
```

---

## 4. 追踪集成设计 (Trace Integration)

### 4.1 TraceIntegration实现

**文件**: `src/ai/trace/integration.py`

```python
import logging
import time
from typing import Dict, Any, Optional
from dataclasses import dataclass, field
from datetime import datetime

logger = logging.getLogger(__name__)

@dataclass
class SpanContext:
    """追踪上下文"""
    span_id: str
    parent_span_id: Optional[str] = None
    start_time: float = field(default_factory=time.time)
    tags: Dict[str, Any] = field(default_factory=dict)
    custom_context: Dict[str, Any] = field(default_factory=dict)
    
    @property
    def duration_ms(self) -> float:
        return (time.time() - self.start_time) * 1000

class TraceIntegration:
    """追踪集成系统"""
    
    def __init__(self, trace_logger=None, enable_auto: bool = True):
        self.trace_logger = trace_logger
        self.enable_auto = enable_auto
        self._active_spans: Dict[str, SpanContext] = {}
        
        if self.trace_logger is None:
            try:
                from src.utils.trace import TraceLogger
                self.trace_logger = TraceLogger("unibrain")
                logger.info("Created new TraceLogger for UniBrain")
            except ImportError:
                logger.warning("TraceLogger not available, tracing disabled")
                self.enable_auto = False
    
    def start_span(
        self, 
        operation: str, 
        tags: Dict[str, Any] = None,
        parent_context: SpanContext = None
    ) -> SpanContext:
        """开始追踪span"""
        import uuid
        span_id = f"{operation}_{uuid.uuid4().hex[:8]}"
        
        span_context = SpanContext(
            span_id=span_id,
            parent_span_id=parent_context.span_id if parent_context else None,
            tags=tags or {},
        )
        
        self._active_spans[span_id] = span_context
        logger.debug(f"Started span: {span_id} for operation: {operation}")
        return span_context
    
    def inject_context(self, span_context: SpanContext, custom_context: Dict[str, Any]) -> None:
        """注入自定义追踪上下文"""
        span_context.custom_context.update(custom_context)
        logger.debug(f"Injected custom context into span: {span_context.span_id}")
    
    def finish_span(
        self, 
        span_context: SpanContext, 
        result: Any = None, 
        error: Exception = None
    ) -> None:
        """结束追踪span"""
        if span_context.span_id not in self._active_spans:
            logger.warning(f"Span not found: {span_context.span_id}")
            return
        
        duration_ms = span_context.duration_ms
        
        if error:
            logger.error(
                f"Span {span_context.span_id} failed after {duration_ms:.0f}ms: {error}"
            )
        else:
            logger.info(
                f"Span {span_context.span_id} completed in {duration_ms:.0f}ms"
            )
        
        del self._active_spans[span_context.span_id]
    
    def record_metrics(
        self, 
        capability: str, 
        provider_id: str,
        latency_ms: float,
        tokens: Dict[str, int],
        success: bool = True
    ) -> None:
        """记录指标"""
        metrics_data = {
            "capability": capability,
            "provider_id": provider_id,
            "latency_ms": latency_ms,
            "input_tokens": tokens.get("input", 0),
            "output_tokens": tokens.get("output", 0),
            "total_tokens": tokens.get("input", 0) + tokens.get("output", 0),
            "success": success,
            "timestamp": datetime.now().isoformat(),
        }
        
        logger.info(
            f"[Metrics] {capability} via {provider_id}: "
            f"{latency_ms:.0f}ms, {tokens.get('input', 0)}+{tokens.get('output', 0)} tokens, "
            f"success={success}"
        )
```

---

## 5. UniBrain重构设计 (UniBrain Refactoring)

### 5.1 简化的UniBrain实现

**文件**: `src/ai/unibrain.py`

```python
import yaml
import logging
from pathlib import Path
from typing import Dict, Optional, List
from dataclasses import dataclass

from .providers.base import AIProvider, AIResponse
from .prompts.manager import PromptManager
from .trace.integration import TraceIntegration, SpanContext

logger = logging.getLogger(__name__)

@dataclass
class UniBrainConfig:
    """UniBrain配置"""
    routing_config_path: str = "config/ai_providers.yaml"
    prompt_dir: str = "src/ai/prompts"
    enable_trace: bool = True
    enable_cache: bool = True
    default_provider: str = "deepseek"

class UniBrain:
    """统一AI能力接口 - 重构版"""
    
    def __init__(
        self,
        providers: Dict[str, AIProvider] = None,
        config: UniBrainConfig = None
    ):
        self.config = config or UniBrainConfig()
        
        # 加载Provider
        if providers:
            self.providers = providers
        else:
            self.providers = self._load_providers_from_config()
        
        # 初始化提示词管理器
        self.prompt_manager = PromptManager(self.config.prompt_dir)
        
        # 初始化追踪系统
        self.trace_integration = TraceIntegration(enable_auto=self.config.enable_trace)
        
        # 加载路由配置
        self.routing_config = self._load_routing_config()
        
        # 构建能力到Provider的映射
        self._capability_provider_map = self.routing_config.get("routing", {})
        
        logger.info(
            f"UniBrain initialized with {len(self.providers)} providers "
            f"and {len(self._capability_provider_map)} capabilities"
        )
    
    def _load_providers_from_config(self) -> Dict[str, AIProvider]:
        """从配置加载Provider"""
        routing_config = self._load_routing_config()
        providers = {}
        
        for provider_id, provider_config in routing_config.get("providers", {}).items():
            try:
                api_key = self._resolve_env_var(provider_config["config"]["api_key"])
                
                ai_config = AIProviderConfig(
                    api_key=api_key,
                    model=provider_config["config"]["model"],
                    base_url=provider_config["config"]["base_url"],
                )
                
                class_name = provider_config["class"]
                module = __import__(f"src.ai.providers.{class_name.lower()}", fromlist=[class_name])
                provider_class = getattr(module, class_name)
                
                provider = provider_class(ai_config)
                providers[provider_id] = provider
                
                logger.info(f"Loaded provider: {provider_id}")
            
            except Exception as e:
                logger.error(f"Failed to load provider {provider_id}: {e}")
        
        return providers
    
    def _select_provider(self, capability: str) -> AIProvider:
        """选择Provider（声明式路由）"""
        provider_id = self._capability_provider_map.get(capability)
        
        if not provider_id:
            logger.warning(f"No provider configured for capability: {capability}, using default")
            provider_id = self.config.default_provider
        
        if provider_id not in self.providers:
            raise RuntimeError(f"Provider not found: {provider_id}")
        
        return self.providers[provider_id]
    
    async def analyze_visual(
        self,
        image_data: bytes,
        context: Optional[Dict] = None,
        trace_context: Optional[Dict] = None,
    ) -> Any:
        """视觉分析能力 - 简化版"""
        import json
        
        provider = self._select_provider("analyze_visual")
        prompt_template = self.prompt_manager.get_prompt("analyze_visual")
        
        formatted_prompt = self.prompt_manager.inject_variables(
            prompt_template,
            {
                "image_description": "Vehicle infotainment system screenshot",
                "context_info": json.dumps(context or {}, indent=2),
            }
        )
        
        span_context = self.trace_integration.start_span(
            operation=f"unibrain.analyze_visual",
            tags={
                "capability": "analyze_visual",
                "provider_id": provider.provider_id,
                "mode": "vision",
            }
        )
        
        if trace_context:
            self.trace_integration.inject_context(span_context, trace_context)
        
        try:
            response = await provider.complete_vision(
                prompt=formatted_prompt,
                image_data=image_data,
                max_tokens=4096,
            )
            
            result_data = json.loads(response.content)
            from src.state.content_tree import PageAnalysis
            result = PageAnalysis.from_dict(result_data)
            
            self.trace_integration.finish_span(span_context, result=result)
            self.trace_integration.record_metrics(
                capability="analyze_visual",
                provider_id=provider.provider_id,
                latency_ms=response.latency_ms,
                tokens={"input": response.input_tokens, "output": response.output_tokens},
                success=True,
            )
            
            return result
        
        except Exception as e:
            self.trace_integration.finish_span(span_context, error=e)
            raise
```

---

## 6. 配置管理设计 (Configuration)

### 6.1 Provider路由配置

**文件**: `config/ai_providers.yaml`

```yaml
# Provider路由配置
version: 1.0

# Provider定义
providers:
  deepseek:
    class: "DeepSeekProvider"
    config:
      api_key: "${DEEPSEEK_API_KEY}"
      model: "deepseek-v4-flash"
      base_url: "https://api.deepseek.com/v1"
    capabilities:
      - parse_instruction
      - verify_page_type
      - screen_safety
      - decide_next_action
    performance:
      latency: 0.8
      quality: 0.7
      efficiency: 0.9
  
  claude:
    class: "ClaudeProvider"
    config:
      api_key: "${ANTHROPIC_API_KEY}"
      model: "claude-3-5-sonnet-20241022"
      base_url: "https://api.anthropic.com/v1"
    capabilities:
      - analyze_visual
      - verify_page_with_vision
    performance:
      latency: 0.6
      quality: 0.95
      efficiency: 0.6
  
  mimo:
    class: "MiMoProvider"
    config:
      api_key: "${MIMO_API_KEY}"
      model: "mimo-v2.5"
      base_url: "https://token-plan-cn.xiaomimimo.com/anthropic"
    capabilities:
      - analyze_visual
    performance:
      latency: 0.7
      quality: 0.9
      efficiency: 0.8

# 能力路由映射
routing:
  analyze_visual: claude
  parse_instruction: deepseek
  verify_page_type: deepseek
  verify_page_with_vision: claude
  screen_safety: deepseek
  decide_next_action: deepseek

# 默认设置
defaults:
  fallback_enabled: true
  cache_enabled: true
  trace_enabled: true
```

---

## 7. 测试策略设计 (Testing Strategy)

### 7.1 Mock数据基础设施

**零测试成本原则**：默认情况下，所有测试都不应产生实际的API调用成本

**分层测试策略**：

1. **单元测试 (Mock优先)** - $0 API成本
2. **集成测试 (录制回放)** - $0 API成本  
3. **性能测试 (Mock数据)** - $0 API成本
4. **健康检查 (可选真实调用)** - $0.01-0.05/次

### 7.2 Mock Provider实现

```python
class MockProvider(AIProvider):
    """Mock Provider用于测试"""
    
    def __init__(self, use_recorded_data: bool = True):
        self.use_recorded_data = use_recorded_data
        self.recorded_data = RECORDED_RESPONSES
    
    async def complete_text(self, prompt: str, schema: Optional[Dict] = None, **kwargs) -> AIResponse:
        """返回预录制的响应，不调用真实API"""
        if self.use_recorded_data:
            for key, data in self.recorded_data.items():
                if key.startswith("deepseek") and "text" in key:
                    return AIResponse(
                        content=data["response"]["content"],
                        provider_id="mock_deepseek",
                        mode="text",
                        input_tokens=data["response"]["input_tokens"],
                        output_tokens=data["response"]["output_tokens"],
                        latency_ms=50.0,
                    )
        
        return AIResponse(
            content='{"result": "mock_response"}',
            provider_id="mock",
            mode="text",
            input_tokens=10,
            output_tokens=20,
            latency_ms=10.0,
        )
```

### 7.3 测试配置

**pytest.ini**:
```ini
[pytest]
# 默认使用mock，不调用真实API
addopts = --disable-socket --use-mock-providers

# 标记需要真实API的测试
markers =
    real_api: 需要真实API调用的测试
    slow: 慢速测试
    integration: 集成测试
```

---

**文档版本**: 1.0
**最后更新**: 2026-06-02
