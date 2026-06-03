# UniBrain AI Provider Documentation

## Overview

UniBrain is a unified AI provider for the uni-claw framework that implements the `AIStrategyAdvisor` interface. It provides five core AI capabilities for intelligent vehicle menu traversal:

1. **analyze_visual** - Analyze screenshots to extract page structure
2. **parse_instruction** - Parse natural language instructions into traversal plans
3. **verify_page_type** - Verify if current page matches expected type
4. **decide_next_action** - Make context-aware next-action decisions
5. **screen_safety** - Screen elements for safety before interaction

## Architecture (V5.3+)

The AI module has been refactored with three new subsystems that form the foundation:

### Provider Abstraction Layer (`src/ai/providers/`)

Unified interface for different AI providers:

```python
from src.ai.providers import AIProvider, AIResponse, create_provider

# Create a provider
provider = create_provider("claude", AIProviderConfig(
    api_key="your-key",
    model="claude-3-5-sonnet-20241022",
    base_url="https://api.anthropic.com/v1",
))

# Use the provider
response = await provider.complete_vision(
    prompt="Analyze this screenshot",
    image_data=screenshot_bytes,
)
```

**Supported Providers:**
- `DeepSeekProvider` - Text mode (fast, efficient)
- `ClaudeProvider` - Text + Vision + Multimodal (high quality)
- `MiMoProvider` - Vision + Multimodal (cost-effective)

### Prompt Management System (`src/ai/prompts/`)

Centralized prompt template management with variable injection:

```python
from src.ai.prompts import PromptManager

manager = PromptManager("src/ai/prompts")
template = manager.get_prompt("analyze_visual")

# Inject variables
formatted = template.format(
    image_description="Vehicle home screen",
    context_info='{"current_path": "/Home"}',
)
```

**Features:**
- YAML front matter for metadata
- Variable injection
- Version control
- Hot reload support
- Validation utilities

### Trace Integration (`src/ai/trace/`)

Distributed tracing for AI calls:

```python
from src.ai.trace import TraceIntegration

trace = TraceIntegration()

# Start a span
span = trace.start_span("analyze_visual", tags={"capability": "vision"})

# Record metrics
trace.record_metrics(
    capability="analyze_visual",
    provider_id="claude",
    latency_ms=1500,
    tokens={"input": 500, "output": 300},
    success=True,
)

# Finish span
trace.finish_span(span)
```

**Features:**
- Span context management
- Performance metrics collection
- Provider health monitoring
- Integration with existing TraceLogger

## Configuration

The provider routing is configured in `config/ai_providers.yaml`:

```yaml
providers:
  claude:
    class: "ClaudeProvider"
    config:
      api_key: "${ANTHROPIC_API_KEY}"
      model: "claude-3-5-sonnet-20241022"
      base_url="https://api.anthropic.com/v1"

routing:
  analyze_visual: claude
  parse_instruction: deepseek
  decide_next_action: deepseek
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                       UniBrain                             │
│              (AIStrategyAdvisor Interface)                  │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │
│  │   Prompt    │  │    Trace    │  │  Provider    │       │
│  │   Manager   │  │ Integration  │  │   Router    │       │
│  └─────────────┘  └─────────────┘  └─────────────┘       │
└─────────────────────────────────────────────────────────────┘
                           ↓
              ┌─────────────────────────┐
              │   Provider Abstraction  │
              │   (DeepSeek/Claude/MiMo)│
              └─────────────────────────┘
                           ↓
              ┌─────────────────────────┐
              │    5 Core Capabilities  │
              │ analyze|parse|verify     │
              │ decide|safety           │
              └─────────────────────────┘
```

## Configuration

The provider routing is configured in `config/ai_providers.yaml`:

```yaml
providers:
  claude:
    class: "ClaudeProvider"
    config:
      api_key: "${ANTHROPIC_API_KEY}"
      model: "claude-3-5-sonnet-20241022"
      base_url: "https://api.anthropic.com/v1"

routing:
  analyze_visual: claude
  parse_instruction: deepseek
```

## Architecture

### Original Three-Layer Design

```
AIStrategyAdvisor (interface)
         ↓
UniBrain (provider)
         ↓
5 Capabilities (implementation)
```

### New Extended Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       UniBrain                             │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │
│  │   Prompt    │  │    Trace    │  │  Provider    │       │
│  │   Manager   │  │ Integration  │  │   Router    │       │
│  └─────────────┘  └─────────────┘  └─────────────┘       │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│                     5 Capabilities                          │
│  ParseToPlan | VerifyPageType | ScreenSafety | Vision       │
│              | ContextDecision                               │
└─────────────────────────────────────────────────────────────┘
```

### Core Components

- **Core Infrastructure** (`src/ai/core/`)
  - `AIProviderConfig` - Configuration management
  - `LLMClient` - DeepSeek API client with retry logic
  - `ResponseValidator` - Response validation with parser registry
  - `BaseCapability` - Generic base class for all capabilities
  - `PromptRegistry` - Centralized prompt template management

- **Vision Services** (`src/ai/vision/`)
  - `VisionService` - Abstract interface
  - `ClaudeVisionService` - Anthropic Claude implementation
  - `MockVisionService` - Testing implementation
  - `create_vision_service` - Factory function

- **Capabilities** (`src/ai/capabilities/`)
  - Type definitions and implementations for each capability

- **Metrics & Archiving** (`src/ai/metrics.py`)
  - `AIMetrics` - Performance and usage metrics
  - `FailureArchiver` - Failure tracking for prompt optimization

## Quick Start

### Basic Usage

```python
from src.ai import UniBrain, UniBrainConfig

# Create with default configuration
unibrain = UniBrain()

# Or with custom configuration
config = UniBrainConfig(
    routing_config_path="config/ai_providers.yaml",
    prompt_dir="src/ai/prompts",
    enable_trace=True,
    default_provider="deepseek",
)
unibrain = UniBrain(config=config)

# Use the provider
container = unibrain.infer_container_type(page_analysis, context)
decision, node_data = unibrain.decide_next_action(goal, page_analysis, context)
```

### With Mock Providers (Testing)

```python
from src.ai import UniBrain, UniBrainConfig
from src.ai.providers.base import AIProvider, AIResponse, AIProviderConfig
from src.ai.providers import create_provider

# Create mock providers for testing
mock_providers = {
    "mock": create_provider("claude", AIProviderConfig(
        api_key="test-key",
        model="test-model",
        base_url="http://mock",
    ))
}

config = UniBrainConfig(
    enable_trace=False,  # Faster tests
    default_provider="mock",
)
unibrain = UniBrain(config=config, providers=mock_providers)
```

### Environment Variables

```bash
# Required
export DEEPSEEK_API_KEY="your-api-key"

# Optional
export DEEPSEEK_MODEL="deepseek-v4-flash"
export AI_PROVIDER_MAX_CONCURRENT="4"
export AI_PROVIDER_TIMEOUT="30.0"
export AI_PROVIDER_REASONING_LEVEL="detailed"
export VISION_SERVICE_TYPE="claude"
export VISION_API_KEY="your-anthropic-key"
```

## Configuration Guide

### AI Provider Configuration

```python
from src.ai.core import AIProviderConfig, RetryConfig, FallbackConfig

config = AIProviderConfig(
    # Required
    api_key="your-api-key",
    
    # API Settings
    model="deepseek-v4-flash",
    base_url="https://api.deepseek.com/v1",
    
    # Performance
    max_concurrent_requests=4,
    request_timeout=30.0,
    
    # Output quality
    reasoning_detail="detailed",  # concise, step_by_step, detailed
    
    # Retry configuration
    retry=RetryConfig(
        max_attempts=3,
        base_delay=1.0,
        max_delay=8.0,
        exponential_base=2.0,
    ),
    
    # Fallback strategy
    fallback=FallbackConfig(
        strategy="partial",  # none, partial, full
        partial_allowlist=["verify", "vision"],
    ),
)
```

### Vision Service Configuration

```python
from src.ai.vision import VisionConfig

vision_config = VisionConfig(
    service_type="claude",  # claude, mimo, mock
    api_key="your-anthropic-key",
    model="claude-3-5-sonnet-20241022",
    timeout=30.0,
    max_retries=3,
)
```

### Loading from Environment

```python
from src.ai.config_loader import load_ai_config, load_vision_config

ai_config = load_ai_config()
vision_config = load_vision_config()

provider = UniBrain(ai_config, vision_config)
```

## Usage Examples

### 1. Instruction Parsing

```python
from src.ai import UniBrain, AIProviderConfig

provider = UniBrain(AIProviderConfig(api_key="key"))

# Parse natural language instruction
plan = provider.capabilities["parse"].execute(
    "Go to WiFi settings and enable it"
)

print(plan.entry_app)  # "Settings"
print(plan.root_node.name)  # Root node details
```

### 2. Page Type Verification

```python
# Verify current page matches expected type
verification = provider.capabilities["verify"].execute({
    "page_analysis": page_analysis,
    "expected_type": "menu_list",
    "expected_page_name": "Connectivity",
})

if verification.is_match:
    print(f"Confirmed: {verification.actual_type}")
else:
    print(f"Expected menu_list, got {verification.actual_type}")
    print(f"Suggestion: {verification.suggestion.action}")
```

### 3. Safety Screening

```python
# Screen elements for safety
safety_result = provider.capabilities["safety"].execute({
    "page_analysis": page_analysis,
    "instruction": "Navigate to WiFi settings",
    "page_type": "settings_group",
})

# Check if safe to proceed
if safety_result.page_level_guidance.overall_safe_to_proceed:
    # Filter to safe elements only
    safe_items = [
        e.name for e in safety_result.evaluations
        if e.safety_tag == "safe"
    ]
    print(f"Safe to interact with: {safe_items}")
```

### 4. Context Decision Making

```python
# Make next action decision
decision = provider.capabilities["decision"].execute({
    "reason": "Explore WiFi settings",
    "page_analysis": page_analysis,
    "context": traversal_context,
    "safety_result": safety_result,
})

if decision.result == "success" and decision.confidence >= 0.7:
    action = decision.action  # "click", "back", etc.
    target = decision.target  # {"by": "text", "value": "WiFi"}
    print(f"Action: {action} on {target}")
```

### 5. Vision Analysis

```python
# Analyze screenshot
page_analysis = provider.analyze_screenshot(image_bytes)

print(f"Current path: {page_analysis.current_path}")
print(f"Menu items: {[item.name for item in page_analysis.items]}")
```

### 6. Metrics and Monitoring

```python
# Get metrics summary
metrics = provider.get_metrics_summary()
print(metrics)
# {
#     "call_counts": {"ParseToPlanCapability": {"success": 10, "failure": 1}},
#     "error_counts": {},
#     "capabilities": ["ParseToPlanCapability", ...],
# }

# Get latency stats
latency = provider.get_latency_stats("ParseToPlanCapability")
print(f"P95 latency: {latency['p95']}ms")

# Get failure summary
failures = provider.get_failure_summary()
print(f"Total failures: {failures['total_failures']}")
```

## Troubleshooting Guide

### Common Issues

#### 1. API Key Not Found

**Error:** `DEEPSEEK_API_KEY environment variable is required`

**Solution:**
```bash
export DEEPSEEK_API_KEY="your-api-key"
```

#### 2. Rate Limit Errors

**Error:** `RateLimitError: Rate limit exceeded`

**Solution:** Adjust retry configuration
```python
config = AIProviderConfig(
    api_key="key",
    retry=RetryConfig(
        max_attempts=5,  # Increase retry attempts
        base_delay=2.0,  # Increase base delay
    ),
)
```

#### 3. Low Confidence Decisions

**Issue:** AI returns decisions with confidence < 0.7

**Solution:** 
- Check if instruction is clear
- Verify page analysis is accurate
- Consider confidence threshold tuning:
```python
# In your code
if decision.confidence < 0.7:
    # Handle low confidence
    return DecisionResult.UNSURE, None
```

#### 4. Vision Service Errors

**Error:** `VisionError: Failed to parse JSON from response`

**Solution:**
- For Claude: Check model supports vision (claude-3-5-sonnet-20241022)
- Ensure image data is valid PNG format
- Check API key has appropriate permissions

#### 5. Memory Issues

**Issue:** Failure archive growing too large

**Solution:**
```python
# Disable archiving if not needed
provider = UniBrain(
    ai_config, 
    vision_config,
    enable_archiving=False,
)

# Or set lower max_records
from src.ai.metrics import FailureArchiver
archiver = FailureArchiver(max_records=100)
```

### Debug Mode

Enable verbose logging:
```python
import logging
logging.basicConfig(level=logging.DEBUG)
```

### Failure Analysis

Review archived failures:
```python
failures = provider.get_failures(capability="ParseToPlanCapability")
for failure in failures[-10:]:  # Last 10 failures
    print(f"{failure['timestamp']}: {failure['error_message']}")
    print(f"Input: {failure['input_data'][:100]}...")
```

## Prompt Optimization Guide

### Current Prompts

Prompts are defined in `src/ai/core/prompts.py` within the `PromptRegistry` class.

### Optimization Process

1. **Review Failures**
   ```python
   failures = provider.get_failures()
   # Analyze patterns in failures
   ```

2. **Identify Issues**
   - Common parsing errors
   - Low confidence predictions
   - Incorrect classifications

3. **Iterate Prompts**
   - Edit prompt in `PromptRegistry._get_*()` methods
   - Add specific examples for common edge cases
   - Clarify instructions for ambiguous cases

4. **Test Changes**
   - Use MockVisionService with predefined responses
   - Run integration tests
   - Monitor metrics for improvement

### Prompt Tips

- **Be Specific**: Define exact output format requirements
- **Add Examples**: Include few-shot examples in prompts
- **Handle Edge Cases**: Explicitly mention what to do with ambiguous cases
- **Version Control**: Track prompt changes like code

### Example Prompt Improvement

**Before:**
```
"Analyze this screenshot and return the page structure."
```

**After:**
```
"You are analyzing a vehicle infotainment system screenshot.

Return a JSON structure with:
1. Menu positions (level 1 and level 2)
2. Current active path
3. All interactive elements with:
   - Type classification (menu_item, switch, button, etc.)
   - Expected action (navigate, toggle, action)
   - Normalized coordinates (0-1)

IMPORTANT:
- Coordinates must be normalized 0-1 relative to screen size
- Mark parent-child relationships clearly
- Default to expected_action='action' if uncertain"
```

## Performance Considerations

### Latency

- **LLM calls**: 500-2000ms typical
- **Vision analysis**: 1000-5000ms typical
- **Total decision loop**: 2-7 seconds

### Optimization Tips

1. **Enable Response Caching**
   - Cache responses for repeated inputs
   - Use `AIResponseCache` in TraversalEngine

2. **Reduce Context**
   - Limit `visited_pages` history size
   - Truncate long action histories

3. **Adjust Concurrency**
   ```python
   config = AIProviderConfig(
       api_key="key",
       max_concurrent_requests=8,  # Increase for parallel operations
   )
   ```

4. **Use Faster Models**
   - `deepseek-v4-flash` for text (faster)
   - Reserve high-quality models for complex decisions

### Cost Management

Monitor token usage:
```python
metrics = provider.metrics.get_token_usage()
print(f"Total tokens: {metrics['total']}")
```

Best practices:
- Use response caching
- Batch similar requests
- Set appropriate `max_retries`
- Monitor usage thresholds

## API Reference

### UniBrain

```python
class UniBrain(AIStrategyAdvisor):
    def __init__(
        self,
        ai_config: AIProviderConfig,
        vision_config: Optional[VisionConfig] = None,
        enable_metrics: bool = True,
        enable_archiving: bool = True,
    )
    
    def infer_container_type(
        self, ui: PageAnalysis, context: TraversalContext
    ) -> ContainerInference
    
    def decide_next_action(
        self, goal: str, ui: PageAnalysis, context: TraversalContext
    ) -> Tuple[DecisionResult, Optional[dict]]
    
    def handle_exception(
        self, exception: dict, ui: PageAnalysis, context: TraversalContext
    ) -> Tuple[DecisionResult, Optional[dict]]
    
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis
    
    def verify_page_with_vision(
        self, image_data: bytes, expected_type: str
    ) -> PageTypeVerification
```

### AIMetrics

```python
class AIMetrics:
    def record_call(
        self, capability: str, success: bool, latency_ms: float,
        confidence: Optional[float] = None, token_count: Optional[int] = None
    )
    
    def get_latency_stats(self, capability: str) -> Dict[str, float]
    
    def get_confidence_distribution(self, capability: str) -> Dict[str, Any]
    
    def get_summary(self) -> Dict[str, Any]
```

### FailureArchiver

```python
class FailureArchiver:
    def archive_failure(
        self, capability: str, input_data: Any, 
        error: Exception, context: Optional[Dict] = None
    )
    
    def get_failures(
        self, capability: Optional[str] = None, limit: int = 100
    ) -> List[Dict]
    
    def get_failure_summary(self) -> Dict[str, Any]
```

## Further Reading

- [Design Decisions](../../openspec/changes/unibrain-ai-provider/design.md)
- [Implementation Tasks](../../openspec/changes/unibrain-ai-provider/tasks.md)
- [Capability Specifications](../../openspec/changes/unibrain-ai-provider/specs/)
