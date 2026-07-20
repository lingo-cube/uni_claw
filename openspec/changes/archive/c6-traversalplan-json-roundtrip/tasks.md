## 1. ObjectDictionaryConverter

- [x] 1.1 Create `Domain/ObjectDictionaryConverter.cs` — sealed class inheriting `JsonConverter<Dictionary<string, object>>` with CLR type inference logic
- [x] 1.2 Create `Domain/ImmutableObjectDictionaryConverter.cs`
- [x] 1.3 Register both converters on `DomainJsonOptions.Default`
- [x] 1.4 Create `ObjectDictionaryConverterTests.cs` (4-5 tests)
- [x] 1.5 Create `ImmutableObjectDictionaryConverterTests.cs` (3-4 tests)
- [x] 1.6 Run `dotnet test`

## 2. JsonPropertyName Annotations — Graph Types (TraversalPlan.cs)

- [x] 2.1-2.5 All properties annotated (properties only, not constructor params)

## 3. JsonPropertyName Annotations — Graph Types (TraversalNode.cs)

- [x] 3.1-3.7 All properties annotated; [JsonIgnore] on computed properties; enums verified

## 4. JsonPropertyName Annotations — Domain.Common Types

- [x] 4.1-4.5 All Domain.Common types annotated; Vision types verified unchanged

## 5. JsonPropertyName Annotations — EntryConfig

- [x] 5.1 EntryConfig properties annotated

## 6. Convenience Methods

- [x] 6.1 Add `ToJson()` method on `TraversalPlan`
- [x] 6.2 Add `FromJson(string json)` static method on `TraversalPlan` (with JsonException → DomainValidationException wrapping)

## 7. Round-Trip Tests

- [x] 7.1 Create `TraversalPlanSerializationTests.cs` with `AssertRoundTrip<T>` helper
- [x] 7.2 TraversalPlan round-trip: full, minimal, Meta mixed, StaticNodes, RootNode
- [x] 7.3 Sub-type round-trip: all 14 types (with field-level comparison for collection fields)
- [x] 7.4 Fail-fast validation: 5 scenarios (empty EntryApp, malformed RootNode, etc.)
- [x] 7.5 Null/missing fields: required-only JSON → defaults populated correctly
- [x] 7.6 Extra fields tolerance: unknown fields silently ignored
- [x] 7.7 StaticNodes keys preserved: `"network_menu"` not camelCased
- [x] 7.8 Computed properties: omitted from serialization, tolerated in input

## 8. Integration Verification

- [x] 8.1 `dotnet build` — zero errors, zero warnings
- [x] 8.2 `dotnet test` — **803/803 pass** (0 failures, 0 skipped)
- [x] 8.3 ArchitectureGuardTests — **46/46 pass**
- [x] 8.4 Domain serialization tests — **2/2 pass**, no regression from converter registration
