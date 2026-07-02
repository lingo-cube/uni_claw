## ADDED Requirements

### Requirement: Content models are ported as validated immutable records
The following 12 types SHALL be ported from `content_models.py` into `Domain/Models/Content/` as `record` types with construction-time validation: `Coordinate`, `Direction`, `MenuInfo`, `MenuItemType`, `ExpectedAction`, `MenuItem`, `PopupInfo`, `PageAnalysis`, `VisitFingerprint`, `ContentNode`, `ContentTree`, `SimulationState`. Pydantic `BaseModel` validation SHALL translate to C# primary-constructor validation that throws `DomainValidationException`.

#### Scenario: All twelve types exist in the Content namespace
- **WHEN** the `UniClaw.Core.Domain.Models.Content` namespace is inspected
- **THEN** all of `Coordinate`, `Direction`, `MenuInfo`, `MenuItemType`, `ExpectedAction`, `MenuItem`, `PopupInfo`, `PageAnalysis`, `VisitFingerprint`, `ContentNode`, `ContentTree`, `SimulationState` are defined exactly once

#### Scenario: Construction validation mirrors pydantic
- **WHEN** any Content record is constructed with a field value that pydantic rejected (e.g. out-of-range, negative, empty-where-required)
- **THEN** construction throws `DomainValidationException` naming the offending field

### Requirement: PageAnalysis and PopupInfo have a single full source in Content
The Content layer SHALL own the full `PageAnalysis` and `PopupInfo` definitions. The AI-layer simplified versions in `AI/IAIStrategyAdvisor.cs` are an acknowledged transitional duplicate (R-4) to be replaced in a later phase; they SHALL NOT be treated as a second authoritative source this phase.

#### Scenario: Full PageAnalysis lives in Content
- **WHEN** the canonical `PageAnalysis` type is referenced
- **THEN** it resolves to `UniClaw.Core.Domain.Models.Content.PageAnalysis`

#### Scenario: PopupInfo single source
- **WHEN** the canonical `PopupInfo` type is referenced
- **THEN** it resolves to `UniClaw.Core.Domain.Models.Content.PopupInfo`

### Requirement: Content collections are ImmutableArray
Any collection-valued field on a Content record (e.g. `PageAnalysis.items`, `ContentTree.nodes`, `SimulationState` history) SHALL be `ImmutableArray<T>` and SHALL NOT expose `List<T>`/`IList<T>`.

#### Scenario: PageAnalysis items is ImmutableArray
- **WHEN** the type of `PageAnalysis.Items` is inspected
- **THEN** it is an `ImmutableArray<T>` of the item type

#### Scenario: ContentTree nodes is ImmutableArray
- **WHEN** the type of `ContentTree.Nodes` is inspected
- **THEN** it is an `ImmutableArray<T>` of `ContentNode`

### Requirement: Content records support with-independence
A `with` copy of any Content record containing a collection SHALL yield a copy whose collection is independent of the original.

#### Scenario: with independence on a collection-bearing Content record
- **WHEN** a `with` copy is made of a Content record holding an `ImmutableArray` field, and the copy is given a replacement collection
- **THEN** the original record's collection is unchanged

### Requirement: Content models serialize single-direction to camelCase JSON
Each Content record SHALL be serializable to JSON via `System.Text.Json` with camelCase keys (global `PropertyNameCasePolicy = CamelCase`); `[JsonPropertyName]` only overrides. JSON → object round-trip is NOT guaranteed this phase.

#### Scenario: Object to JSON outputs camelCase
- **WHEN** a Content record with a `PascalCase` property is serialized with the global camelCase policy
- **THEN** the JSON key is `camelCase` (unless overridden by `[JsonPropertyName]`)

#### Scenario: Round-trip is not a Phase 1 guarantee
- **WHEN** a Content record with required/validated fields is round-tripped through JSON with a missing or illegal field
- **THEN** reconstruction throws (acknowledged deferred limitation; not asserted as a success)
