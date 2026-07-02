## ADDED Requirements

### Requirement: DomainValidationException carries field name and illegal value
`DomainValidationException` SHALL be a dedicated exception type in `Domain/` whose message and/or properties include the offending field name and the illegal value that caused construction to fail.

#### Scenario: Exception message names the field
- **WHEN** a record throws `DomainValidationException` due to an illegal `confidence` field
- **THEN** the exception exposes the field name (`confidence`) and the illegal value (e.g. `1.5`)

#### Scenario: Exception is thrown at construction
- **WHEN** an illegal value is passed to a primary constructor
- **THEN** the exception is thrown before the object is observable to the caller

### Requirement: Construction-time fail-fast validation on all Domain records
Every Domain record SHALL validate its invariants in its primary constructor and throw `DomainValidationException` for illegal inputs. Domain SHALL NOT silently construct illegal objects (no clamping/normalizing to defaults).

#### Scenario: Negative dimension is rejected, not clamped
- **WHEN** `BoundingBox` is created with `w=-0.1`
- **THEN** construction throws `DomainValidationException`; no object with a clamped `w` is produced

#### Scenario: Out-of-range confidence is rejected, not clamped
- **WHEN** `FlattenedElement` is created with `confidence=2.0`
- **THEN** construction throws `DomainValidationException`; no object with clamped `confidence` is produced

### Requirement: Domain collections are ImmutableArray
All collection-valued fields on Domain records SHALL be `ImmutableArray<T>`. Domain records SHALL NOT expose `List<T>`, `IList<T>`, or mutable backing collections.

#### Scenario: No mutable collection exposure
- **WHEN** the public surface of Domain records is inspected
- **THEN** no field or property exposes `List<T>` or `IList<T>`; collection fields are `ImmutableArray<T>`

### Requirement: with expressions yield independent collection copies
A `with` copy of a Domain record containing an `ImmutableArray` SHALL produce a record whose collection is independent of the original's.

#### Scenario: with independence
- **WHEN** `original` is a Domain record with `ImmutableArray<T>`, and `copy = original with { <collectionField> = <newArray> }`
- **THEN** `original`'s `<collectionField>` is unchanged and references its original `ImmutableArray`

### Requirement: Single-direction JSON serialization with camelCase
Domain SHALL configure `System.Text.Json` with `PropertyNameCasePolicy = CamelCase` globally. `[JsonPropertyName]` SHALL be used only as an override. Hand-written `ToDictionary`/`FromDictionary` SHALL NOT exist. Object → JSON SHALL be guaranteed; JSON → object round-trip SHALL NOT be guaranteed this phase (deferred, R-6). No DTO types SHALL be introduced.

#### Scenario: Object to JSON succeeds with camelCase
- **WHEN** any Domain record is serialized with the global options
- **THEN** valid JSON is produced and keys are `camelCase` (unless an explicit `[JsonPropertyName]` overrides)

#### Scenario: No hand-written dict converters
- **WHEN** the Domain assembly is inspected
- **THEN** no `ToDictionary`/`FromDictionary` methods exist on Domain records

#### Scenario: No DTO types
- **WHEN** the Domain assembly is inspected
- **THEN** no types are defined whose name or purpose is a Data-Transfer-Object wrapper around a Domain record

#### Scenario: Round-trip is not asserted as a success
- **WHEN** a JSON document missing a required field or carrying an illegal value is deserialized into a Domain record
- **THEN** deserialization throws (acknowledged deferred limitation; tests do NOT assert successful round-trip this phase)

### Requirement: Domain has no I/O and no reverse dependencies
The Domain layer SHALL NOT perform I/O (file/DB/network/ADB/pixel conversion) and SHALL NOT depend on any upper layer (Graph/StateMachine/Traversal/Trace/AI). Its only dependencies SHALL be BCL.

#### Scenario: No I/O usings
- **WHEN** the `Domain/` namespace source is inspected
- **THEN** there are no file/DB/network/ADB/pixel-conversion APIs used

#### Scenario: No upper-layer usings
- **WHEN** the `Domain/` namespace source is inspected
- **THEN** it does not reference `UniClaw.Core.Graph`/`StateMachine`/`Traversal`/`Trace`/`AI`

### Requirement: Build and coverage gates
`dotnet build` SHALL produce 0 errors and 0 warnings. `dotnet test` SHALL be all green with Domain-layer coverage > 80%.

#### Scenario: Clean build
- **WHEN** `dotnet build` is run on the solution
- **THEN** it reports 0 errors and 0 warnings

#### Scenario: Tests green with coverage gate
- **WHEN** `dotnet test` is run with coverage
- **THEN** all tests pass and Domain-layer line/branch coverage exceeds 80%
