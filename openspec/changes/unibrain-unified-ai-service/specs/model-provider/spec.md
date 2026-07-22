## ADDED Requirements

### Requirement: IModelProvider defines AI model call abstraction with 3 completion methods

IModelProvider SHALL define:
- `string ProviderId { get; }` (identifies the provider, e.g. "claude", "deepseek")
- `Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)`
- `Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)`
- `Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)`

IModelProvider SHALL NOT carry ITraceRecorder reference. IModelProvider SHALL NOT record AICallRecords — that is the sub-interface implementation's responsibility. IModelProvider is pure transport (call + retry + timeout).

#### Scenario: CompleteVisionAsync sends prompt with screenshot
- **WHEN** ClaudePageAnalyzer calls modelProvider.CompleteVisionAsync(request, screenshotBytes)
- **THEN** ModelResponse is returned with Content, ProviderId, Mode="vision", token counts, and latency

#### Scenario: CompleteTextAsync sends pure text prompt
- **WHEN** ClaudeTraversalAdvisor calls modelProvider.CompleteTextAsync(request)
- **THEN** ModelResponse is returned with Content, Mode="text", token counts, and latency

#### Scenario: ProviderId identifies the backend
- **WHEN** AnthropicModelProvider is constructed
- **THEN** ProviderId returns "claude"

### Requirement: ModelRequest is sealed record class with prompt and optional schema

ModelRequest SHALL be a sealed record class with:
- `string Prompt`
- `string? SystemPrompt = null`
- `object? Schema = null` (for structured output)
- `int MaxTokens = 4096`

#### Scenario: ModelRequest carries structured output schema
- **WHEN** PageAnalyzer sends request with Schema=PageAnalysisSchema
- **THEN** ModelProvider uses schema to guide structured output

### Requirement: ModelResponse aligns with Python AIResponse fields

ModelResponse SHALL be a sealed record class with:
- `string Content`
- `string ProviderId`
- `string Mode` ("text", "vision", "multimodal")
- `int InputTokens`
- `int OutputTokens`
- `double LatencyMs`
- `string Model = ""`
- `bool Success = true`
- `string? ErrorMessage = null`

#### Scenario: ModelResponse carries full response metadata
- **WHEN** model call succeeds
- **THEN** Content, ProviderId, Mode, InputTokens, OutputTokens, LatencyMs, Model are populated; Success=true; ErrorMessage=null

#### Scenario: ModelResponse records failure
- **WHEN** model call fails
- **THEN** Success=false, ErrorMessage contains reason, Content may be empty
