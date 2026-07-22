## ADDED Requirements

### Requirement: ITextUnderstanding defines text comprehension interface

ITextUnderstanding SHALL define exactly 1 async method:
- `Task<TextUnderstandingResult> UnderstandTextAsync(TextUnderstandingRequest request, CancellationToken ct = default)`

This aligns with Python parse_instruction capability.

#### Scenario: UnderstandTextAsync processes text with optional context
- **WHEN** ITextUnderstanding.UnderstandTextAsync(new TextUnderstandingRequest("tap Settings", context: "launcher page")) is called
- **THEN** returns TextUnderstandingResult with Category, Confidence, Entities, and optional Summary

### Requirement: TextUnderstandingRequest is sealed record class with Text and optional Context

TextUnderstandingRequest SHALL be a sealed record class with:
- `string Text`
- `string? Context = null`

Text SHALL be non-null and non-empty. DomainValidationException SHALL be thrown when Text is null or empty.

#### Scenario: TextUnderstandingRequest validates non-empty text
- **WHEN** TextUnderstandingRequest is constructed with Text=null or Text=""
- **THEN** DomainValidationException is thrown with FieldName="Text"

### Requirement: TextUnderstandingResult carries classification and confidence

TextUnderstandingResult SHALL be a sealed record class with:
- `string Category`
- `double Confidence` (validated 0-1 range)
- `ImmutableArray<string> Entities`
- `string? Summary = null`

Confidence SHALL be validated in range [0, 1]. DomainValidationException SHALL be thrown for out-of-range values.

#### Scenario: TextUnderstandingResult carries analysis output
- **WHEN** text understanding completes
- **THEN** result has Category (text classification), Confidence (0-1), Entities (extracted items), and optional Summary
