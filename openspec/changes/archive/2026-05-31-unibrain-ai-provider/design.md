## Context

The uni-claw framework currently lacks a unified AI provider for intelligent menu traversal. AI capabilities are scattered across the codebase with inconsistent interfaces, no centralized error handling, and limited observability. As the framework evolves to support more complex traversal scenarios, a robust AI provider architecture is needed.

**Current State:**
- No centralized AI provider exists
- TraversalEngine relies on hardcoded rules for navigation
- Vision service exists but is not integrated with AI capabilities
- No unified configuration or error handling for AI operations

**Constraints:**
- Must implement the existing `AIStrategyAdvisor` interface
- Must support both async and sync execution (TraversalEngine is sync)
- Must be compatible with existing ADB and Vision Service infrastructure
- API costs need to be controlled (DeepSeek V4 Flash for text, Vision Service for images)

**Stakeholders:**
- uni-claw framework users need reliable AI-powered traversal
- Platform team needs observability and debugging capabilities
- Cost management requires token usage monitoring

## Goals / Non-Goals

**Goals:**
- Create a unified AI provider (`UniBrain`) that implements `AIStrategyAdvisor`
- Provide five AI capabilities: instruction parsing, page verification, safety screening, vision analysis, and context decision
- Establish core infrastructure: config, LLM client, response validator, generic capability base class
- Integrate pluggable Vision Service for screenshot analysis
- Implement comprehensive error handling, retry logic, and fallback strategies
- Add observability: unified logging, metrics, and failure archiving

**Non-Goals:**
- Complete replacement of all rule-based logic (AI augments, doesn't replace)
- Multi-model support per capability (each capability has a designated model)
- Real-time streaming responses (all capabilities use structured output)
- Prompt engineering optimization (prompts will be iterated post-deployment)

## Decisions

### 1. Three-Layer Architecture

**Decision:** Use Interface → Provider → Capabilities layered architecture

```
AIStrategyAdvisor (interface)
         ↓
UniBrain (provider)
         ↓
5 Capabilities (ParseTask, VerifyPage, ScreenSafety, VisionAnalysis, ContextDecision)
```

**Rationale:**
- Clean separation of concerns
- Easy to test each layer independently
- Capabilities can be swapped without changing provider
- Matches existing AIStrategyAdvisor interface pattern

**Alternatives Considered:**
- *Single monolithic provider*: Rejected - too hard to test and maintain
- *Direct AIStrategyAdvisor implementation*: Rejected - no reuse between capabilities

### 2. Generic BaseCapability with Type Parameters

**Decision:** Use `BaseCapability[T_IN, T_OUT]` generic base class

```python
class BaseCapability(ABC, Generic[T_IN, T_OUT]):
    @abstractmethod
    def execute(self, input_data: T_IN) -> T_OUT:
        pass
```

**Rationale:**
- Compile-time type safety for each capability
- Shared execution logic (validation, error handling, logging)
- Easy to add new capabilities

**Alternatives Considered:**
- *Separate base classes per capability*: Rejected - code duplication
- *No base class*: Rejected - no shared patterns

### 3. DeepSeek V4 Flash for Text, Vision Service for Images

**Decision:** Use DeepSeek V4 Flash for 4 text capabilities, separate Vision Service for vision

**Rationale:**
- DeepSeek V4 Flash offers best cost/performance for structured text output
- Vision capabilities require specialized models (Claude, MiMo)
- Separation allows independent optimization and cost management

**Alternatives Considered:**
- *Use Claude for all*: Rejected - too expensive for text operations
- *Use DeepSeek for vision*: Rejected - DeepSeek vision not available
- *Use cheaper models*: Rejected - insufficient quality for structured output

### 4. Parser Registry Pattern for Response Validation

**Decision:** Use ResponseValidator with parser registration

```python
validator.register_parser("TraversalPlan", parse_traversal_plan)
result = validator.validate_and_parse(response, "TraversalPlan")
```

**Rationale:**
- Decouples validation logic from capabilities
- Easy to add new response types
- Centralized error handling for parsing failures

**Alternatives Considered:**
- *Inline parsing in each capability*: Rejected - code duplication
- *Use pydantic everywhere*: Rejected - too rigid for complex nested structures

### 5. Async Core with Sync Wrapper

**Decision:** Implement capabilities as async, provide sync wrapper

```python
async def execute_async(self, input_data: T_IN) -> T_OUT:
    # Actual async implementation

def execute(self, input_data: T_IN) -> T_OUT:
    loop = asyncio.get_event_loop()
    return loop.run_until_complete(self.execute_async(input_data))
```

**Rationale:**
- LLM calls are natively async
- Allows concurrent request processing
- Sync wrapper maintains compatibility with TraversalEngine

**Alternatives Considered:**
- *Sync-only*: Rejected - poor performance for concurrent calls
- *Async-only*: Rejected - breaking change for TraversalEngine

### 6. Prompt Registry with Variable Injection

**Decision:** Centralized PromptRegistry with template variable injection

```python
prompt = "Analyze: {instruction}, context: {context}"
formatted = inject_variables(prompt, {"instruction": "Go to Settings", "context": "Desktop"})
```

**Rationale:**
- Prompts defined in one place, easy to iterate
- Type-safe variable substitution
- Supports prompt versioning and A/B testing

**Alternatives Considered:**
- *Inline prompts*: Rejected - hard to maintain and version
- *External prompt files*: Rejected - adds complexity

### 7. Retry with Exponential Backoff

**Decision:** Implement configurable retry with exponential backoff

```python
retry = RetryConfig(
    max_attempts=3,
    base_delay=1.0,
    max_delay=8.0,
    exponential_base=2.0
)
```

**Rationale:**
- Handles transient API failures
- Exponential backoff prevents API overload
- Configurable per environment

**Alternatives Considered:**
- *No retry*: Rejected - fragile in production
- *Fixed delay retry*: Rejected - less efficient

### 8. Safety-First Fallback Strategy

**Decision:** When safety screening fails, enter "safe mode" (back operations only)

**Rationale:**
- Safety failures are critical - default to safe behavior
- Prevents accidental dangerous operations
- Clear escalation path

**Alternatives Considered:**
- *Continue without screening*: Rejected - safety risk
- *Abort traversal*: Rejected - too disruptive

## Risks / Trade-offs

### API Cost Risk

**Risk:** High token usage could lead to unexpected costs

**Mitigation:**
- Token usage monitoring with alerts at 100k tokens
- Response caching for repeated inputs
- Context compression (limit visited_pages history)
- Concurrent request limits (max 4)

### Quality Risk

**Risk:** AI model outputs may be inconsistent or incorrect

**Mitigation:**
- Internal validation of structured outputs
- Confidence thresholds (require >0.7 for decisions)
- Failure archiving for prompt iteration
- Human-in-the-loop for critical decisions

### Dependency Risk

**Risk:** DeepSeek API or Vision Service may be unavailable

**Mitigation:**
- Configurable retry with exponential backoff
- Mock implementations for testing
- Fallback to rule-based logic on critical failures
- Health check before critical operations

### Performance Risk

**Risk:** LLM calls add latency to traversal

**Mitigation:**
- Async processing for concurrent capability calls
- Response caching for repeated queries
- Performance SLAs tracked (P50/P95/P99)
- Optional capability execution (non-critical capabilities can fail)

### Migration Risk

**Risk:** Existing integrations may break with new AI provider

**Mitigation:**
- Implements existing AIStrategyAdvisor interface (no breaking changes)
- Feature flag to enable/disable AI capabilities
- Gradual rollout (start with vision analysis only)
- Comprehensive integration tests

## Implementation Plan

### Phase 1: Core Infrastructure (Week 1)
1. Create `src/ai/core/` module
2. Implement `AIProviderConfig`, `RetryConfig`, `FallbackConfig`
3. Implement `LLMClient` with retry logic
4. Implement `ResponseValidator` with parser registry
5. Implement `BaseCapability` generic base class
6. Add comprehensive unit tests

### Phase 2: Vision Service Integration (Week 1-2)
1. Create `src/ai/vision/` module
2. Implement `VisionService` abstract base class
3. Implement `ClaudeVisionService`
4. Implement `MiMoVisionService` (if available)
5. Implement `MockVisionService` for testing
6. Define `PageAnalysis` data structures
7. Integration tests with sample screenshots

### Phase 3: Five AI Capabilities (Week 2-3)
1. Create `src/ai/capabilities/` module
2. Implement `ParseToPlanCapability` (instruction → plan)
3. Implement `VerifyPageTypeCapability` (page type verification)
4. Implement `ScreenSafetyCapability` (element safety screening)
5. Implement `VisionAnalysisCapability` (screenshot analysis)
6. Implement `ContextDecisionCapability` (next-action decision)
7. Create `PromptRegistry` with all prompt templates
8. Unit and integration tests for each capability

### Phase 4: Provider Integration (Week 3)
1. Implement `UniBrain` provider class
2. Implement `AIStrategyAdvisor` interface methods
3. Wire up capabilities to provider methods
4. Integrate with `TraversalEngine`
5. Configuration management (environment variables)
6. End-to-end integration tests

### Phase 5: Observability & Testing (Week 4)
1. Implement unified logging format
2. Add metrics collection (latency, tokens, confidence)
3. Implement failure archiving
4. Performance benchmarking
5. Create monitoring dashboard
6. Load testing with realistic scenarios

### Phase 6: Rollout & Iteration (Week 5+)
1. Feature flag deployment
2. Gradual rollout to test environments
3. Monitor metrics and failures
4. Iterate on prompts based on real data
5. Production rollout

## Open Questions

1. **Token Budget:** What is the acceptable monthly token budget? (Affects caching strategy)
2. **Confidence Thresholds:** Should confidence thresholds be configurable per capability?
3. **Vision Model Choice:** Which vision model should be default? (Claude vs MiMo)
4. **Prompt Versioning:** How will we track and roll back prompt changes?
5. **Failure Archiving:** How long should we retain failure records?

## Dependencies

- Requires DeepSeek API account and key
- Requires Anthropic API key (for Claude vision)
- Optional: MiMo API access
- Existing: ADB client, state management, base Vision Service
