## ADDED Requirements

### Requirement: AI empty response is a structural error and SHALL NOT be retried
When the AI vision model returns an empty content response (0 output tokens after successful HTTP), the system SHALL classify this as a structural error and SHALL NOT retry.

#### Scenario: AI returns empty content
- **WHEN** `CompleteVisionAsync` returns `Success=true` but `Content` is empty or whitespace
- **THEN** `PageAnalyzer.AnalyzeOnceAsync` SHALL throw `DomainValidationException` immediately
- **AND** `IsTransient` SHALL return `false` for this exception
- **AND** `AnalyzeCurrentPageAsync` SHALL NOT retry

#### Scenario: AI returns empty content on intent extraction
- **WHEN** `IntentExtractor.ExtractAsync` receives an empty AI response
- **THEN** the call SHALL fail immediately
- **AND** `ScenarioPlanCompiler.ResolveIntentSlots` SHALL fall back to mechanical mapping

### Requirement: Transient errors are retried up to a configured maximum
Network errors, invalid JSON, and coordinate range errors SHALL be classified as transient and retried.

#### Scenario: Network timeout
- **WHEN** `CompleteVisionAsync` throws `HttpRequestException`
- **THEN** `PageAnalyzer` SHALL retry up to `MaxAnalyzeAttempts` times
- **AND** each retry SHALL re-capture a fresh screenshot

#### Scenario: Malformed JSON
- **WHEN** AI returns valid HTTP but unparseable JSON
- **THEN** the call SHALL be retried once
- **AND** the retry SHALL use `useJsonMode=false`

### Requirement: UIA fallback is NOT used after AI failure
When AI vision fails (empty response or structural error), the pipeline SHALL NOT return UIA-only analysis as a fallback.

#### Scenario: AI fails after UIA was skipped
- **WHEN** UIA was insufficient (items < N or popup detected) AND AI returns empty
- **THEN** the observation SHALL fail with `DomainValidationException`
- **AND** UIA-derived PageAnalysis SHALL NOT be returned
