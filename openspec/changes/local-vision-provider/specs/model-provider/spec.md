# model-provider delta Specification

## ADDED Requirements

### Requirement: LocalVisionProvider as new IModelProvider implementation

A new `LocalVisionProvider` SHALL be added in independent project `UniClaw.LocalVisionProvider` implementing `IModelProvider`. `ProviderId` SHALL return `"local-vision"`. It SHALL implement `CompleteVisionAsync` via HTTP POST to a local Python FastAPI service, mapping the returned evidence JSON to `PageAnalysisDto`. `CompleteTextAsync` and `CompleteMultimodalAsync` SHALL throw `NotImplementedException`.

This implementation SHALL NOT modify the `IModelProvider` interface. The existing interface method count SHALL remain unchanged.

#### Scenario: New provider does not change interface

- **WHEN** `IModelProvider` interface is inspected after `LocalVisionProvider` is added
- **THEN** the interface still defines exactly 3 completion methods and `ProviderId`

#### Scenario: ProviderId is local-vision

- **WHEN** `LocalVisionProvider.ProviderId` is accessed
- **THEN** returns `"local-vision"`

#### Scenario: Vision calls succeed with valid HTTP

- **WHEN** `CompleteVisionAsync` is called and Python responds with 200 and valid evidence JSON
- **THEN** `ModelResponse.Success` is true and `Content` is valid `PageAnalysisDto` JSON

#### Scenario: HTTP failure returns Success=false

- **WHEN** Python is unreachable or returns non-2xx
- **THEN** `ModelResponse.Success` is false, `ErrorMessage` is set, and no exception is thrown

### Requirement: LocalVisionProvider registered in Host assembly

Host assembly (`HostCommands.CreateProviders`) SHALL register `"local-vision"` as a provider identifier in the provider dictionary. When `UNICLAW_VISION_MODE` is `"local"`, the router SHALL route `analyze_visual` capability to `"local-vision"`.

#### Scenario: Local vision provider registered in Host

- **WHEN** `HostCommands.CreateProviders()` is called with `UNICLAW_VISION_MODE=local` and Python is available
- **THEN** the provider dictionary contains `"local-vision"` mapping to a `LocalVisionProvider` instance wrapped in `ObservingModelProvider`

#### Scenario: analyze_visual routes to local when mode is local

- **WHEN** `IModelRouter.Resolve("analyze_visual")` is called with `UNICLAW_VISION_MODE=local`
- **THEN** returns the observed `LocalVisionProvider` instance
