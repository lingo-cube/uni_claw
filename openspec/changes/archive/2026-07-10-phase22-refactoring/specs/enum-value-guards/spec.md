## MODIFIED Requirements

### Requirement: EnumValueGuardTests includes SpanType_Has11Values

EnumValueGuardTests SHALL be extended with a `[Fact]` test `SpanType_Has11Values` that asserts `Enum.GetValues<SpanType>().Length == 11`. This MUST be added alongside the existing 10 Phase2 enum tests and 2 Phase1 Domain enum tests.

#### Scenario: SpanType value count locked at 11
- **WHEN** `Enum.GetValues<SpanType>().Length` is queried
- **THEN** it MUST equal 11
- **THEN** any addition or removal of SpanType values MUST fail this CI-blocking test

#### Scenario: SpanType guard test coexists with existing guards
- **WHEN** all EnumValueGuardTests run
- **THEN** the new SpanType_Has11Values test MUST pass alongside all existing 12 enum value tests
