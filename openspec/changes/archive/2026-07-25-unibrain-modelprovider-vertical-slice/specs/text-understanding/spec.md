## ADDED Requirements

### Requirement: TextUnderstanding real implementation consumes IModelRouter and IPromptLibrary

TextUnderstanding SHALL be a `sealed class` implementing `ITextUnderstanding`, constructed with `IModelRouter router` and `IPromptLibrary promptLibrary`. It SHALL be provider-agnostic — it SHALL NOT reference any concrete provider type (DeepSeek / Claude / Mock); all routing is delegated to `IModelRouter`.

`UnderstandTextAsync` SHALL:
1. Obtain the prompt template via `promptLibrary.GetTemplate(ModelCapabilities.ParseInstruction)`; if null, throw `DomainValidationException` indicating the prompt template is missing.
2. Resolve the template with an `IReadOnlyDictionary<string, string>` populated with `["text"] = request.Text` and `["context"] = request.Context ?? ""`.
3. Build a `ModelRequest` with `Prompt = resolved.User`, `SystemPrompt = resolved.System`, `Schema = ParseInstructionSchema`, `MaxTokens = 1024`, `Capability = ModelCapabilities.ParseInstruction`.
4. Resolve the provider via `router.Resolve(request.Capability)`.
5. Call `provider.CompleteTextAsync(modelRequest, ct)`.
6. If `resp.Success == false`, throw `DomainValidationException` carrying `resp.ErrorMessage`.
7. Parse `resp.Content` (JSON) into a `TextUnderstandingResult` (Category, Confidence, Entities, Summary).

#### Scenario: Happy path parses instruction
- **WHEN** `UnderstandTextAsync` is called with `Text="打开设置"`, `Context="主页"`, and the resolved provider returns JSON with `category`, `confidence`, `entities`, `summary`
- **THEN** returns a `TextUnderstandingResult` with the parsed Category, Confidence, Entities, and Summary

#### Scenario: Missing prompt template fails fast
- **WHEN** `promptLibrary.GetTemplate(ParseInstruction)` returns null
- **THEN** `DomainValidationException` is thrown before any model call is made

#### Scenario: Model call failure propagates
- **WHEN** the resolved provider returns a `ModelResponse` with `Success = false` and `ErrorMessage`
- **THEN** `DomainValidationException` is thrown carrying the ErrorMessage

#### Scenario: Provider-agnostic routing
- **WHEN** `TextUnderstanding` is constructed with a router whose `parse_instruction` route targets either DeepSeek or Mock provider
- **THEN** `UnderstandTextAsync` delegates via `router.Resolve` without referencing any concrete provider type
