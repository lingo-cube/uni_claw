## Why

TraversalPlan (and its ~10 nested types) cannot be deserialized from JSON today. Six blockers prevent round-trip: parameterized constructor matching, `Dictionary<string, object>` Meta field (STJ deserializes `object` as `JsonElement`), nested records with validation, null/default handling, and fail-fast preservation. Without round-trip, the "load plan from file and run" workflow is impossible — plans can only be created in-memory via PlanCompiler.

## What Changes

- Add `ObjectDictionaryConverter` — a custom STJ `JsonConverter<Dictionary<string, object>>` that infers CLR types from `JsonElement.ValueKind` (String→string, Number→int/long/double, True/False→bool, Null→null, Array/Object→preserve as JsonElement). Preserves unknown nested structures without data loss.
- Add `[JsonPropertyName]` annotations on every public property of ~10 types in the TraversalPlan dependency tree (TraversalPlan, TraversalNode, EntryPolicy, EntryConfig, CompletionPolicy, IntentSlots, Operation, ErrorPolicy, Precondition, ChildrenStrategy, DynamicRule, MatchCondition). Enables STJ parameterized-constructor deserialization with consistent camelCase mapping.
- Register `ObjectDictionaryConverter` on existing `DomainJsonOptions.Default` — no new options instance, no breaking change to existing serialization.
- Add convenience methods `TraversalPlan.ToJson()` and `TraversalPlan.FromJson(string)` — serialize/deserialize via DomainJsonOptions.Default. FromJson throws DomainValidationException on null/invalid input.
- ~20-25 new tests covering: full round-trip, sub-type round-trip, minimal plan, null/missing fields, Meta special cases, validation fail-fast on deserialization, extra-field tolerance, ObjectDictionaryConverter standalone.

## Capabilities

### New Capabilities
- `object-dictionary-converter`: Custom STJ JsonConverter that handles `Dictionary<string, object>` serialization/deserialization by inferring CLR types from JsonElement ValueKind, preserving unknown nested structures as JsonElement
- `traversalplan-json-roundtrip`: [JsonPropertyName] annotations on TraversalPlan dependency tree (~10 types, ~80 properties) + DomainJsonOptions registration + ToJson/FromJson convenience methods, enabling full C# self-roundtrip via DomainJsonOptions.Default

### Modified Capabilities
- (none — Graph type behavioral requirements unchanged; `[JsonPropertyName]` is serialization metadata, not spec-level behavior change)

## Impact

- **Code**: `Domain/CrossCutting/ObjectDictionaryConverter.cs` (new), `Domain/CrossCutting/DomainJsonOptions.cs` (register converter), ~10 Graph.Models files (add [JsonPropertyName] annotations), `TraversalPlan.cs` (add ToJson/FromJson)
- **Tests**: New `tests/UniClaw.Core.Tests/Graph/TraversalPlanSerializationTests.cs` (~20-25 tests), new `tests/UniClaw.Core.Tests/Domain/CrossCutting/ObjectDictionaryConverterTests.cs` (~4-5 tests)
- **Dependencies**: No new NuGet packages (STJ already in project). No filesystem dependency — Domain layer stays pure classlib.
- **APIs**: TraversalPlan gains 2 new public methods (ToJson, FromJson). DomainJsonOptions.Default gains 1 converter in its Converters collection. All existing APIs unchanged.
- **Risk**: Low — annotations are additive; ObjectDictionaryConverter only intercepts `Dictionary<string, object>` (typed dictionaries unaffected); no Python interop (C# self-roundtrip only)
