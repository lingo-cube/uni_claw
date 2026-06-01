## Why

uni-claw needs a unified AI provider to replace the current scattered AI integrations and provide five core AI capabilities for vehicle menu traversal. The current approach has inconsistent interfaces, duplicate code, and no unified error handling or observability. Implementing a centralized provider now will establish a solid foundation for AI-driven traversal as the framework grows.

## What Changes

- **New AI Provider Architecture**: Create `UniBrain` - a unified AI provider implementing the `AIStrategyAdvisor` interface
- **Five AI Capabilities**:
  - `parse-to-plan`: Parse natural language instructions into traversal plans (DeepSeek LLM)
  - `verify-page-type`: Verify current page type matches expectations (DeepSeek LLM)
  - `screen-safety`: Screen elements for safety before interaction (DeepSeek LLM)
  - `vision-analysis`: Analyze screenshots to extract page structure (Vision Service)
  - `context-decision`: Make next-action decisions based on context (DeepSeek LLM)
- **Core Infrastructure**: Config management, LLM client with retry, response validator with parser registry, generic capability base class
- **Vision Service Integration**: Pluggable vision services (Claude, MiMo, Mock) for screenshot analysis
- **Error Handling & Observability**: Unified logging, metrics collection, failure archiving, fallback strategies

## Capabilities

### New Capabilities

- `ai-provider`: Core AI provider infrastructure (config, LLM client, validator, base capability)
- `parse-to-plan`: Natural language instruction parsing to traversal plans
- `verify-page-type`: Page type verification and mismatch detection
- `screen-safety`: Element safety screening with task context awareness
- `vision-analysis`: Screenshot analysis extracting page structure and elements
- `context-decision`: Context-aware next-action decision making
- `vision-service`: Pluggable vision service architecture (Claude/MiMo/Mock)

### Modified Capabilities

- `ai-strategy-advisor`: Implementing this existing interface with UniBrain provider

## Impact

- **Affected Code**:
  - New module: `src/ai/provider.py` (UniBrain implementation)
  - New module: `src/ai/core/` (LLM client, validator, config, base capability)
  - New module: `src/ai/capabilities/` (five AI capabilities)
  - New module: `src/ai/vision/` (vision service implementations)
  - Integration: `TraversalEngine` will use UniBrain via AIStrategyAdvisor interface

- **API Changes**:
  - New: `AIStrategyAdvisor.infer_container_type()` - AI-based container type inference
  - New: `AIStrategyAdvisor.decide_next_action()` - AI-powered decision making
  - New: `AIStrategyAdvisor.handle_exception()` - AI-assisted exception recovery

- **Dependencies**:
  - DeepSeek API client (`anthropic` SDK or DeepSeek SDK)
  - Optional: Anthropic Claude API (for vision analysis)
  - Optional: MiMo API (alternative vision service)
  - `jsonschema` for response validation
  - `aiohttp` for async HTTP calls

- **Systems**:
  - ADB client integration unchanged (works through Vision Service)
  - State management unchanged (AI operates on current state)
  - Configuration system extended with AI provider settings
