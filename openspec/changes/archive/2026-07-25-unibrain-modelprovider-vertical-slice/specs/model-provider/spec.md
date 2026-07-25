## MODIFIED Requirements

### Requirement: IModelProvider defines AI model call abstraction with 3 completion methods

IModelProvider SHALL define:
- `string ProviderId { get; }` (identifies the provider, e.g. "claude", "deepseek")
- `Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)`
- `Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)`
- `Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)`

IModelProvider is the transport abstraction (HTTP call, serialization, protocol error mapping). AICallRecord observation SHALL be performed exclusively by an `ObservingModelProvider` decorator that implements IModelProvider. The decorator SHALL be applied at `ModelRouter` composition time to every bare provider, so that every call resolved through the router produces an AICallRecord — observation cannot be bypassed by callers. Concrete transport providers (`DeepSeekModelProvider`, `MockModelProvider`, `AnthropicModelProvider`) SHALL NOT record AICallRecords directly; they are wrapped by the decorator.

#### Scenario: CompleteVisionAsync sends prompt with screenshot
- **WHEN** ClaudePageAnalyzer calls modelProvider.CompleteVisionAsync(request, screenshotBytes)
- **THEN** ModelResponse is returned with Content, ProviderId, Mode="vision", token counts, and latency

#### Scenario: CompleteTextAsync sends pure text prompt
- **WHEN** ClaudeTraversalAdvisor calls modelProvider.CompleteTextAsync(request)
- **THEN** ModelResponse is returned with Content, Mode="text", token counts, and latency

#### Scenario: ProviderId identifies the backend
- **WHEN** AnthropicModelProvider is constructed
- **THEN** ProviderId returns "claude"

#### Scenario: Observation enforced by decorator at router composition
- **WHEN** ModelRouter is constructed with a bare provider and a recorder, and `Resolve(capability).CompleteTextAsync(...)` is called
- **THEN** the call passes through `ObservingModelProvider` and an AICallRecord is recorded; the bare provider is never reached unobserved

### Requirement: ModelRequest is sealed record class with prompt and optional schema

ModelRequest SHALL be a sealed record class with:
- `string Prompt`
- `string? SystemPrompt = null`
- `object? Schema = null` (for structured output)
- `int MaxTokens = 4096`
- `string? Capability = null` (semantic label flowing through router / decorator / transport, e.g. "parse_instruction")

Capability SHALL be optional and backward-compatible (default null). When present, it SHALL flow unchanged through `IModelRouter.Resolve` and `ObservingModelProvider` into `AICallRecord.Capability`. Transport providers MAY ignore it.

#### Scenario: ModelRequest carries structured output schema
- **WHEN** PageAnalyzer sends request with Schema=PageAnalysisSchema
- **THEN** ModelProvider uses schema to guide structured output

#### Scenario: Capability flows through router and decorator
- **WHEN** ModelRequest is constructed with Capability="parse_instruction" and routed via ModelRouter
- **THEN** router.Resolve(request.Capability) routes on the capability and ObservingModelProvider records it in AICallRecord.Capability

## ADDED Requirements

### Requirement: ModelCapabilities defines 5 capability string constants aligned with Python

`ModelCapabilities` SHALL be a static class with exactly 5 public `const string` values:
- `ParseInstruction = "parse_instruction"` (ITextUnderstanding)
- `VerifyPageType = "verify_page_type"` (IPageAnalyzer.VerifyPageTypeAsync)
- `DecideNextAction = "decide_next_action"` (ITraversalAdvisor)
- `ScreenSafety = "screen_safety"` (ITraversalAdvisor)
- `AnalyzeVisual = "analyze_visual"` (IPageAnalyzer)

`ModelCapabilities` SHALL NOT include `verify_page_with_vision` (C# YAGNI per IPageAnalyzer). These 5 constants are the canonical capability vocabulary for `ModelRequest.Capability` and `IModelRouter` routing keys.

#### Scenario: All 5 capabilities defined with Python-aligned names
- **WHEN** ModelCapabilities is referenced
- **THEN** ParseInstruction, VerifyPageType, DecideNextAction, ScreenSafety, AnalyzeVisual are accessible as const strings matching the Python capability names

### Requirement: IModelRouter resolves a capability to an observed IModelProvider

`IModelRouter` SHALL define `IModelProvider Resolve(string capability)`.

`ModelRouter` SHALL be a `sealed class` implementing `IModelRouter`, constructed with:
- `ImmutableDictionary<string, string> capabilityRouting` (capability → providerId)
- `ImmutableDictionary<string, IModelProvider> providers` (providerId → bare provider)
- `ITraceRecorder recorder`
- `string defaultProviderId`

At construction, `ModelRouter` SHALL wrap every bare provider in `new ObservingModelProvider(inner, recorder)` and store the wrapped instances internally. Construction SHALL throw `DomainValidationException` when any `capabilityRouting` value references a providerId absent from `providers`.

`Resolve` SHALL: (1) look up `capabilityRouting[capability]`; (2) if missing, fall back to `defaultProviderId`; (3) if still missing, throw `DomainValidationException`. `Resolve` SHALL return only pre-wrapped (observed) providers — callers cannot obtain a bare provider from the router.

#### Scenario: Resolve routes by capability
- **WHEN** `ModelRouter.Resolve("parse_instruction")` is called with routing `{parse_instruction → "deepseek"}`
- **THEN** returns the `ObservingModelProvider` wrapping the deepseek provider

#### Scenario: Default fallback for unmapped capability
- **WHEN** `Resolve` is called with a capability not present in `capabilityRouting`
- **THEN** returns the provider for `defaultProviderId` (wrapped)

#### Scenario: Unknown capability with unknown default fails fast
- **WHEN** `Resolve` is called with a capability not in routing AND `defaultProviderId` has no provider entry
- **THEN** `DomainValidationException` is thrown

#### Scenario: Routing references unknown provider fails fast at construction
- **WHEN** `ModelRouter` is constructed with a `capabilityRouting` value "foo" but `providers` has no "foo"
- **THEN** `DomainValidationException` is thrown at construction

#### Scenario: Returned provider is always observed
- **WHEN** `Resolve` returns a provider and `CompleteTextAsync` is called on it
- **THEN** an `AICallRecord` is recorded (decorator is in place)

### Requirement: ObservingModelProvider decorates IModelProvider as the sole AICallRecord hook

`ObservingModelProvider` SHALL be a `sealed class` implementing `IModelProvider`, constructed with `IModelProvider inner` and `ITraceRecorder recorder`. `ProviderId` SHALL delegate to `inner.ProviderId`.

`CompleteTextAsync` SHALL: (1) start a Stopwatch; (2) call `inner.CompleteTextAsync`; (3) call `recorder.RecordAICallAsync` with an `AICallRecord` carrying `Capability` (from `request.Capability ?? ""`), `ProviderId` (`inner.ProviderId`), `Success` (`resp.Success`), `LatencyMs` (elapsed milliseconds), `Tokens` (`resp.InputTokens + resp.OutputTokens`), and `Metadata` populated with at least `model`, `mode="text"`, and `error` when `resp.Success == false`; (4) return `resp` unchanged.

`CompleteVisionAsync` / `CompleteMultimodalAsync` SHALL behave symmetrically with `mode` "vision" / "multimodal". `ObservingModelProvider` SHALL NOT alter the response and SHALL NOT swallow exceptions raised by `inner`.

#### Scenario: Successful call records AICallRecord
- **WHEN** `CompleteTextAsync` is called and `inner` returns a `Success=true` response after ~150ms
- **THEN** `recorder.RecordAICallAsync` received an `AICallRecord` with `Success=true`, `LatencyMs≈150`, `Capability` from the request, and the original response is returned unchanged

#### Scenario: Failed call records failure metadata
- **WHEN** `inner` returns `Success=false` with `ErrorMessage`
- **THEN** the `AICallRecord` has `Success=false` and `Metadata["error"]` carries the ErrorMessage

#### Scenario: ProviderId delegates to inner
- **WHEN** `new ObservingModelProvider(deepSeekInner, recorder).ProviderId` is accessed
- **THEN** returns `inner.ProviderId` (e.g. "deepseek")

### Requirement: DeepSeekModelProvider implements OpenAI-compatible HTTP transport

`DeepSeekModelProvider` SHALL be a `sealed class` implementing `IModelProvider`, constructed with `HttpClient http` and `DeepSeekProviderConfig config`. `ProviderId` SHALL return `"deepseek"`.

`CompleteTextAsync` SHALL POST to `{config.BaseUrl}/chat/completions` with header `Authorization: Bearer {config.ApiKey}` and a JSON body containing `model = config.Model`, a `messages` array (an optional `{role:"system", content:SystemPrompt}` when `SystemPrompt != null`, followed by `{role:"user", content:Prompt}`), `max_tokens`, and `response_format = {type:"json_object"}` when `Schema != null`.

The response SHALL map `choices[0].message.content` → `ModelResponse.Content`, `usage.prompt_tokens` → `InputTokens`, `usage.completion_tokens` → `OutputTokens`, with `Mode = "text"`. Transport errors (HTTP non-2xx, timeout, JSON parse failure) SHALL produce `ModelResponse(Success: false, ErrorMessage: ...)` — `CompleteTextAsync` SHALL NOT throw on transport errors.

`CompleteVisionAsync` / `CompleteMultimodalAsync` SHALL throw `NotImplementedException` (text-only in the vertical slice).

#### Scenario: Successful text completion
- **WHEN** `CompleteTextAsync` POSTs and DeepSeek returns HTTP 200 with `choices[0].message.content` and `usage`
- **THEN** `ModelResponse` with `Success=true`, Content from choices, InputTokens/OutputTokens from usage, `Mode="text"`

#### Scenario: Structured output requested via Schema
- **WHEN** `ModelRequest.Schema != null`
- **THEN** the request body includes `response_format = {type:"json_object"}`

#### Scenario: HTTP error is graceful, not thrown
- **WHEN** DeepSeek returns HTTP 500
- **THEN** `ModelResponse` with `Success=false` and `ErrorMessage` set; no exception thrown

#### Scenario: Vision and Multimodal not implemented in vertical slice
- **WHEN** `CompleteVisionAsync` or `CompleteMultimodalAsync` is called
- **THEN** `NotImplementedException` is thrown

### Requirement: DeepSeekProviderConfig is validated at construction

`DeepSeekProviderConfig` SHALL be a sealed record class with `string ApiKey`, `string Model`, `string BaseUrl`, `int MaxConcurrentRequests = 4`, `double RequestTimeoutSeconds = 30.0`. Construction SHALL throw `DomainValidationException` when `ApiKey`, `Model`, or `BaseUrl` is null/empty, when `MaxConcurrentRequests <= 0`, or when `RequestTimeoutSeconds <= 0`.

#### Scenario: Valid config constructs with defaults
- **WHEN** `DeepSeekProviderConfig("key", "deepseek-chat", "https://api.deepseek.com")` is constructed
- **THEN** the record is created with `MaxConcurrentRequests=4` and `RequestTimeoutSeconds=30.0`

#### Scenario: Missing ApiKey fails fast
- **WHEN** `DeepSeekProviderConfig` is constructed with `ApiKey=null` or `ApiKey=""`
- **THEN** `DomainValidationException` is thrown with FieldName="ApiKey"

### Requirement: MockModelProvider and MockModelFixture provide transport-level declarative mock

`MockModelEntry` SHALL be a sealed record class with `string Content`, `int InputTokens = 0`, `int OutputTokens = 0`, `double LatencyMs = 0`, `bool Success = true`, `string? ErrorMessage = null`.

`MockModelFixture` SHALL be a sealed record class holding `ImmutableDictionary<string, MockModelEntry> Responses` (capability → preset). It SHALL validate at construction (non-null `Responses`, else `DomainValidationException`). It SHALL expose `MockModelEntry? Resolve(string capability)` and `static MockModelFixture FromJson(string json)` using an internal DTO deserialized with DomainJsonOptions. This structural pattern (sealed-record + FromJson + internal DTO) parallels `StateFixture`; the `DomainValidationException` validation is additive design (`StateFixture` itself does not validate).

`MockModelProvider` SHALL be a `sealed class` implementing `IModelProvider`, constructed with `MockModelFixture fixture, string providerId = "mock"`. `ProviderId` SHALL return `providerId`. `CompleteTextAsync` SHALL look up `fixture.Resolve(request.Capability ?? "")`; if null, throw `DomainValidationException`; else return `ModelResponse` with the entry's Content / tokens / latency / Success / ErrorMessage, the provider's ProviderId, and `Mode="text"`. `CompleteVisionAsync` / `CompleteMultimodalAsync` SHALL throw `NotImplementedException` (vertical slice is text-only).

#### Scenario: Preset response returned by capability
- **WHEN** `MockModelProvider.CompleteTextAsync` is called with `request.Capability="parse_instruction"` and the fixture has a preset for it
- **THEN** `ModelResponse` with the preset Content, tokens, and `Mode="text"`

#### Scenario: Missing preset fails fast
- **WHEN** `fixture.Resolve` returns null for the requested capability
- **THEN** `DomainValidationException` is thrown

#### Scenario: Fixture loaded from JSON
- **WHEN** `MockModelFixture.FromJson(json)` is called with valid JSON mapping capabilities to entries
- **THEN** returns a `MockModelFixture` with `Responses` populated
