## 1. Core Infrastructure

- [x] 1.1 Create `src/ai/core/` module directory structure
- [x] 1.2 Implement `AIProviderConfig` data class with API, retry, and fallback configurations
- [x] 1.3 Implement `RetryConfig` and `FallbackConfig` data classes
- [x] 1.4 Implement `LLMClient` class with DeepSeek API integration
- [x] 1.5 Add retry logic with exponential backoff to `LLMClient`
- [x] 1.6 Add concurrent request control with semaphore to `LLMClient`
- [x] 1.7 Implement `ResponseValidator` class with parser registry pattern
- [x] 1.8 Implement `BaseCapability[T_IN, T_OUT]` generic abstract base class
- [x] 1.9 Implement `PromptRegistry` class with variable injection
- [x] 1.10 Add unit tests for all core infrastructure classes

## 2. Vision Service Integration

- [x] 2.1 Create `src/ai/vision/` module directory structure
- [x] 2.2 Define `VisionService` abstract base class interface
- [x] 2.3 Implement `BaseVisionService` with image encoding and JSON extraction
- [x] 2.4 Implement `ClaudeVisionService` using Anthropic API
- [x] 2.5 Implement `MiMoVisionService` (if API available)
- [x] 2.6 Implement `MockVisionService` for testing
- [x] 2.7 Define `PageAnalysis` and related data structures
- [x] 2.8 Implement `VisionConfig` data class
- [x] 2.9 Implement `create_vision_service()` factory function
- [x] 2.10 Add integration tests for all vision service implementations

## 3. AI Capabilities Implementation

- [x] 3.1 Create `src/ai/capabilities/` module directory structure
- [x] 3.2 Implement `ParseToPlanCapability` with prompt templates and schema
- [x] 3.3 Implement `VerifyPageTypeCapability` with prompt templates and schema
- [x] 3.4 Implement `ScreenSafetyCapability` with prompt templates and schema
- [x] 3.5 Implement `VisionAnalysisCapability` using Vision Service
- [x] 3.6 Implement `ContextDecisionCapability` with prompt templates and schema
- [x] 3.7 Implement `TraversalPlan`, `PageTypeVerification`, `SafetyScreeningResult`, `ContextDecisionResult` data classes
- [x] 3.8 Add unit tests for each capability
- [x] 3.9 Add integration tests for capability execution

## 4. UniBrain Provider Implementation

- [x] 4.1 Create `src/ai/provider.py` module
- [x] 4.2 Implement `UniBrain` class constructor with config and vision service injection
- [x] 4.3 Implement `_register_parsers()` method to register all response parsers
- [x] 4.4 Implement `infer_container_type()` using `VerifyPageTypeCapability`
- [x] 4.5 Implement `decide_next_action()` using `ContextDecisionCapability`
- [x] 4.6 Implement `handle_exception()` using `ContextDecisionCapability`
- [x] 4.7 Implement `analyze_screenshot()` using `VisionAnalysisCapability`
- [x] 4.8 Implement `verify_page_with_vision()` combining vision and page verification
- [x] 4.9 Add confidence threshold handling (0.7 default)
- [x] 4.10 Add unit tests for UniBrain provider

## 5. AIStrategyAdvisor Interface Integration

- [x] 5.1 Verify `UniBrain` implements `AIStrategyAdvisor` interface correctly
- [x] 5.2 Update `TraversalEngine` to accept `UniBrain` as AI advisor
- [x] 5.3 Add feature flag for AI advisor enablement
- [x] 5.4 Add integration tests with `TraversalEngine`
- [x] 5.5 Test async/sync execution wrapper compatibility

## 6. Configuration Management

- [x] 6.1 Add environment variables for AI provider configuration
- [x] 6.2 Add DEEPSEEK_API_KEY configuration
- [x] 6.3 Add DEEPSEEK_MODEL configuration (default: deepseek-v4-flash)
- [x] 6.4 Add AI_PROVIDER_MAX_CONCURRENT configuration
- [x] 6.5 Add AI_PROVIDER_TIMEOUT configuration
- [x] 6.6 Add AI_PROVIDER_REASONING_LEVEL configuration
- [x] 6.7 Add VISION_SERVICE_TYPE configuration (claude/mimo/mock)
- [x] 6.8 Add retry and fallback configuration options
- [x] 6.9 Add configuration validation on startup

## 7. Error Handling & Observability

- [x] 7.1 Implement unified logging format for AI operations
- [x] 7.2 Add execution duration logging to `BaseCapability`
- [x] 7.3 Implement failure archiving to JSONL file
- [x] 7.4 Implement `AIMetrics` class for metrics collection
- [x] 7.5 Add token usage tracking (if available from API)
- [x] 7.6 Add confidence distribution tracking
- [x] 7.7 Add error count and type tracking
- [x] 7.8 Implement safety mode fallback on `ScreenSafetyCapability` failure
- [x] 7.9 Add performance benchmarks (P50/P95/P99 latency)

## 8. Testing

- [x] 8.1 Add unit tests for all core infrastructure (LLMClient, ResponseValidator, BaseCapability)
- [x] 8.2 Add unit tests for all vision service implementations
- [x] 8.3 Add unit tests for all five AI capabilities
- [x] 8.4 Add unit tests for UniBrain provider
- [x] 8.5 Add integration tests with mock Vision Service
- [x] 8.6 Add integration tests with real Vision Service (optional)
- [x] 8.7 Add end-to-end tests with TraversalEngine
- [x] 8.8 Add performance benchmark tests
- [x] 8.9 Add failure scenario tests

## 9. Documentation

- [x] 9.1 Add module documentation for `src/ai/`
- [x] 9.2 Add API documentation for all public classes and methods
- [x] 9.3 Add configuration guide for AI provider
- [x] 9.4 Add usage examples for each capability
- [x] 9.5 Add troubleshooting guide for common issues
- [x] 9.6 Add prompt optimization guide

## 10. Deployment & Rollout

- [x] 10.1 Add dependency declarations (anthropic, aiohttp, jsonschema)
- [x] 10.2 Add pre-commit hooks for AI-related code
- [x] 10.3 Create staging environment configuration
- [x] 10.4 Test deployment in staging environment
- [x] 10.5 Create production configuration template
- [x] 10.6 Set up monitoring dashboards for AI metrics
- [x] 10.7 Configure alerts for high token usage or error rates
- [x] 10.8 Plan gradual rollout strategy (vision → other capabilities)
- [x] 10.9 Document rollback procedure
