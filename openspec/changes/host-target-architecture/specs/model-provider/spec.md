## MODIFIED Requirements

### Requirement: MockModelProvider and MockModelFixture provide transport-level declarative mock

`MockModelEntry` SHALL be a sealed record class with `string Content`, `int InputTokens = 0`, `int OutputTokens = 0`, `double LatencyMs = 0`, `bool Success = true`, `string? ErrorMessage = null`.

`MockModelFixture` SHALL be a sealed record class holding `ImmutableDictionary<string, MockModelEntry> Responses` (capability → preset). It SHALL validate at construction (non-null `Responses`, else `DomainValidationException`). It SHALL expose `MockModelEntry? Resolve(string capability)` and `static MockModelFixture FromJson(string json)` using an internal DTO deserialized with DomainJsonOptions. This structural pattern (sealed-record + FromJson + internal DTO) parallels `StateFixture`; the `DomainValidationException` validation is additive design (`StateFixture` itself does not validate).

`MockModelProvider` SHALL be a `sealed class` implementing `IModelProvider`, constructed with `MockModelFixture fixture, string providerId = "mock"`. `ProviderId` SHALL return `providerId`. `CompleteTextAsync` SHALL look up `fixture.Resolve(request.Capability ?? "")`; if null, throw `DomainValidationException`; else return `ModelResponse` with the entry's Content / tokens / latency / Success / ErrorMessage, the provider's ProviderId, and `Mode="text"`. `CompleteVisionAsync` SHALL look up `fixture.Resolve(request.Capability ?? "")`; if a preset entry exists, return a `ModelResponse` with the entry's Content / tokens / latency / Success / ErrorMessage, the provider's ProviderId, and `Mode="vision"`; if no preset exists, throw `DomainValidationException` (symmetric with `CompleteTextAsync`). `CompleteMultimodalAsync` SHALL behave symmetrically with `CompleteVisionAsync` but set `Mode="multimodal"`. `MockModelFixture` SHALL accept preset entries usable by all three completion modes — the entry is keyed by capability and is mode-agnostic.

#### Scenario: Preset response returned by capability
- **WHEN** `MockModelProvider.CompleteTextAsync` is called with `request.Capability="parse_instruction"` and the fixture has a preset for it
- **THEN** `ModelResponse` with the preset Content, tokens, and `Mode="text"`

#### Scenario: Missing preset fails fast
- **WHEN** `fixture.Resolve` returns null for the requested capability
- **THEN** `DomainValidationException` is thrown

#### Scenario: Vision preset returned by capability
- **WHEN** `MockModelProvider.CompleteVisionAsync` is called with `request.Capability="analyze_visual"` and the fixture has a preset for it
- **THEN** `ModelResponse` with the preset Content, tokens, latency, Success/ErrorMessage, the provider's ProviderId, and `Mode="vision"`

#### Scenario: Missing vision preset fails fast
- **WHEN** `MockModelProvider.CompleteVisionAsync` is called with a capability for which `fixture.Resolve` returns null
- **THEN** `DomainValidationException` is thrown (symmetric with the text-mode missing-preset path)

#### Scenario: Fixture loaded from JSON
- **WHEN** `MockModelFixture.FromJson(json)` is called with valid JSON mapping capabilities to entries
- **THEN** returns a `MockModelFixture` with `Responses` populated

## ADDED Requirements

### Requirement: MockModelFixture supports vision and multimodal preset entries

`MockModelFixture.Responses` (capability → `MockModelEntry`) SHALL satisfy `CompleteVisionAsync` / `CompleteMultimodalAsync` lookups the same way `CompleteTextAsync` does: a single capability-keyed entry is consumed by whichever completion mode resolves it, making the replay link shape a Core config selection rather than a Host-owned provider. No per-mode dictionary is introduced; the entry is mode-agnostic and the mode is set by the completion method that consumes it.

#### Scenario: Vision capability preset returned by CompleteVisionAsync
- **WHEN** a `MockModelFixture` has a preset entry for `"analyze_visual"` and `MockModelProvider.CompleteVisionAsync` is called with `request.Capability="analyze_visual"`
- **THEN** the same entry is returned as a `ModelResponse` with `Mode="vision"`, identical Content/tokens/latency/Success/ErrorMessage to what `CompleteTextAsync` would produce for that capability except for `Mode`