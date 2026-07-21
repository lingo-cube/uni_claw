## ADDED Requirements

### Requirement: Source generator SHALL detect [TraceHandler] on methods
The Roslyn incremental source generator SHALL scan for methods decorated with [TraceHandler] and extract SpanType, Action, containing class, method name, return type, and parameters.

#### Scenario: Generator detects decorated method
- **WHEN** a public method has [TraceHandler(SpanType.ErrorHandling, "handle_error")]
- **THEN** the generator extracts SpanType=ErrorHandling, Action="handle_error", and the return type

### Requirement: Source generator SHALL emit async wrapper method
For each detected [TraceHandler] method, the generator SHALL emit a partial class containing an async wrapper method that delegates to the original and records lifecycle trace.

#### Scenario: Wrapper returns original result
- **WHEN** the generated wrapper calls the original method
- **THEN** the wrapper returns the original method's return value unchanged

#### Scenario: Wrapper records lifecycle on success
- **WHEN** the original method returns without throwing
- **THEN** the wrapper calls RecordHandlerLifecycleAsync with the attribute's SpanType, Action, and success status

#### Scenario: Wrapper records error on exception
- **WHEN** the original method throws an exception
- **THEN** the wrapper records RecordHandlerLifecycleAsync with status="fail" and metadata containing the exception type, then rethrows

### Requirement: Source generator SHALL auto-extract return type properties as metadata
The generator SHALL emit code that reads all readable properties of the return type, converts enums to string and structs via ToString(), skips null values, and produces a metadata dictionary.

#### Scenario: Enum properties become strings
- **WHEN** the return type has an enum property Strategy = ErrorStrategy.Retry
- **THEN** the metadata dictionary contains "strategy": "Retry"

#### Scenario: Null properties are skipped
- **WHEN** the return type has a string? property that is null
- **THEN** the metadata dictionary does NOT contain the key

#### Scenario: [TraceIgnore] excludes property
- **WHEN** a return type property is decorated with [TraceIgnore]
- **THEN** the generated code does NOT include that property in metadata

### Requirement: Source generator wrapper SHALL accept extraMetadata
The generated wrapper SHALL accept an optional Dictionary\<string, object\>? extraMetadata parameter and merge it with auto-extracted metadata.

#### Scenario: extraMetadata overrides auto-extracted
- **WHEN** extraMetadata contains a key that also exists in auto-extracted metadata
- **THEN** the extraMetadata value takes precedence (merged last)

#### Scenario: extraMetadata null is no-op
- **WHEN** extraMetadata is null
- **THEN** only auto-extracted metadata is used

### Requirement: Source generator SHALL generate PushSpan/PopSpan in try/finally
The generated wrapper SHALL call PushSpan() before the original method and PopSpan() in a finally block.

#### Scenario: PushSpan → method → PopSpan sequence
- **WHEN** the generated wrapper is called
- **THEN** PushSpan() is called before the original method, and PopSpan() is called in finally after (even on exception)

### Requirement: Source generator SHALL coexist with manual trace calls
Generated wrappers SHALL NOT interfere with existing manual RecordHandlerLifecycleAsync call sites.

#### Scenario: Manual and generated coexist during migration
- **WHEN** both a manual RecordHandlerLifecycleAsync call and a generated wrapper exist
- **THEN** both produce valid ExecutionRecords without conflict
