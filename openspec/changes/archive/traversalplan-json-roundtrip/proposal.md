## Why

TraversalPlan has zero JSON round-trip capability today — deserialization (JSON → TraversalPlan) fails due to 6 blockers (parameterized constructor matching, `Dictionary<string, object>` Meta, nested records with validation, null/default handling, fail-fast preservation). This is a **functional prerequisite** for the "load plan from file and run" workflow (C-6 from the Python-C# gap triage). Without it, plans can only be constructed programmatically, never persisted or shared.

## What Changes

- Add `ObjectDictionaryConverter` (custom `JsonConverter<Dictionary<string, object>>`) to Domain cross-cutting — enables round-trip of arbitrary `object` values in Meta fields
- Add `[JsonPropertyName("camelCaseKey")]` annotations to all public properties on TraversalPlan's dependency tree (~10 types + ~8 enums) — ensures STJ deserialization parameter matching
- Register `ObjectDictionaryConverter` on `DomainJsonOptions.Default.Converters`
- Change `TypeHint.Values` return type from `IReadOnlyList<TypeHint>` to `IReadOnlyList<string>` — align with other Domain enums (P3-4, merged into this change)
- Add `ToJson()` / `FromJson()` convenience methods on TraversalPlan (optional)
- Add ~20-25 round-trip serialization tests for TraversalPlan + sub-types + ObjectDictionaryConverter

## Capabilities

### New Capabilities
- `plan-json-roundtrip`: TraversalPlan full JSON serialize + deserialize via DomainJsonOptions.Default, enabling plan persistence and file-based execution

### Modified Capabilities
- `typehint-values-type`: TypeHint.Values return type change from `IReadOnlyList<TypeHint>` to `IReadOnlyList<string>` (consistency with Direction/MenuItemType/ExpectedAction/NodeType)

## Impact

- **Graph.Models** (~10 types): `[JsonPropertyName]` annotations added — no behavioral change, only serialization metadata
- **Domain/CrossCutting**: New `ObjectDictionaryConverter` + `DomainJsonOptions.Default` registration
- **Domain/Vision**: TypeHint.Values type change — minor signature change, no behavioral change
- **Tests**: ~20-25 new tests in `Graph/TraversalPlanSerializationTests.cs` + `Domain/Vision/TypeHintValuesTests.cs`
- **No breaking changes**: All annotations are additive; TypeHint.Values type change is compatible (IReadOnlyList<string> vs IReadOnlyList<TypeHint> — callers need string representation anyway)
