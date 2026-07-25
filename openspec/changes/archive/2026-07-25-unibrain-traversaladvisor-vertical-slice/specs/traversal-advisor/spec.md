## ADDED Requirements

### Requirement: TraversalAdvisor real implementation consumes IModelRouter and IPromptLibrary

`TraversalAdvisor` SHALL be a `sealed class` implementing `ITraversalAdvisor`, constructed with `IModelRouter router` and `IPromptLibrary promptLibrary`. It SHALL be provider-agnostic — it SHALL NOT reference any concrete provider type (DeepSeek / Claude / Mock); all routing is delegated to `IModelRouter`.

`DecideNextActionAsync` SHALL:
1. Obtain the prompt template via `promptLibrary.GetTemplate(ModelCapabilities.DecideNextAction)`; if null, throw `DomainValidationException` indicating the prompt template is missing.
2. Serialize `pageAnalysis` to a JSON string via `DomainJsonOptions.Default` and resolve the template with an `IReadOnlyDictionary<string, string>` populated with `["goal"] = goal`, `["page_analysis"] = <serialized pageAnalysis>`, `["current_node_id"] = currentNodeId ?? ""`, `["depth"] = (depth?.ToString() ?? "")`.
3. Build a `ModelRequest` with `Prompt = resolved.User`, `SystemPrompt = resolved.System`, `Schema = Schemas.DecideNextAction`, `MaxTokens = 1024`, `Capability = ModelCapabilities.DecideNextAction`.
4. Resolve the provider via `router.Resolve(ModelCapabilities.DecideNextAction)`.
5. Call `provider.CompleteTextAsync(modelRequest, ct)`.
6. If `resp.Success == false`, throw `DomainValidationException` carrying `resp.ErrorMessage`.
7. Parse `resp.Content` (JSON) into a `ContextDecisionResult` (`Result`, `Action`, `Target`, `Params`, `Reasoning`, `Confidence`, `SafetyVerified`). The `result` field SHALL be parsed case-insensitively into the `DecisionResult` enum (Success / Unsure / GiveUp); an unrecognized value SHALL throw `DomainValidationException`. The `params` object, when present, SHALL be mapped into `ImmutableDictionary<string, object>?` with each value converted by JSON `ValueKind` to a CLR primitive (`string` / `double` / `bool`); a null or absent `params` SHALL yield `null`.

The three other `ITraversalAdvisor` methods (`InferContainerTypeAsync`, `HandleExceptionAsync`, `ScreenSafetyAsync`) SHALL throw `NotImplementedException` carrying a message indicating the method is pending a future slice.

#### Scenario: Happy path decides next action

- **WHEN** `DecideNextActionAsync` is called with `goal="find WiFi settings"`, a `PageAnalysis`, `currentNodeId="node_1"`, `depth=3`, and the resolved provider returns JSON with `result="Success"`, `action="tap"`, `target="wifi_item"`, `params={"timeout":5000}`, `reasoning="visible list item"`, `confidence=0.9`, `safety_verified=true`
- **THEN** returns a `ContextDecisionResult` with `Result=Success`, `Action="tap"`, `Target="wifi_item"`, `Params` containing `timeout` as a `double`, `Reasoning="visible list item"`, `Confidence=0.9`, `SafetyVerified=true`

#### Scenario: Missing prompt template fails fast

- **WHEN** `promptLibrary.GetTemplate(DecideNextAction)` returns null
- **THEN** `DomainValidationException` is thrown before any model call is made

#### Scenario: Model call failure propagates

- **WHEN** the resolved provider returns a `ModelResponse` with `Success = false` and `ErrorMessage`
- **THEN** `DomainValidationException` is thrown carrying the ErrorMessage

#### Scenario: Invalid result enum fails fast

- **WHEN** the resolved provider returns JSON with `result="Maybe"` (not a recognized `DecisionResult`)
- **THEN** `DomainValidationException` is thrown

#### Scenario: Provider-agnostic routing

- **WHEN** `TraversalAdvisor` is constructed with a router whose `decide_next_action` route targets either DeepSeek or Mock provider
- **THEN** `DecideNextActionAsync` delegates via `router.Resolve` without referencing any concrete provider type

#### Scenario: Other three interface methods are not implemented

- **WHEN** `InferContainerTypeAsync`, `HandleExceptionAsync`, or `ScreenSafetyAsync` is called on a `TraversalAdvisor` instance
- **THEN** `NotImplementedException` is thrown indicating the method is pending a future slice
