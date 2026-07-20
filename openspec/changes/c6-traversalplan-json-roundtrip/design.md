## Context

TraversalPlan and its ~10 nested types cannot deserialize from JSON today. Serialization works (any record → JSON via DomainJsonOptions.Default), but 6 blockers prevent round-trip:

1. **Parameterized constructors with validation** — STJ must match constructor parameter names exactly; manual constructors with DomainValidationException fail-fast must fire on deserialization
2. **Dictionary<string, object> Meta** — STJ deserializes `object` as `JsonElement`, losing CLR type info (string, int, bool)
3. **ImmutableDictionary<string, object>** — Operation.Params, Target.Meta, RestoreAction.Params use ImmutableDictionary, which STJ cannot deserialize into (no built-in support) AND contains `object` values (same JsonElement problem)
4. **Nested records** — each has custom constructors with validation
5. **Null/default handling** — missing keys vs explicit nulls
6. **Fail-fast validation** — DomainValidationException must fire on invalid deserialized input

DomainJsonOptions.Default currently only has `JsonStringEnumConverter(CamelCase)` — no `ObjectDictionaryConverter` or `ImmutableObjectDictionaryConverter`.

Source code findings:
- `DomainJsonOptions.cs` is at `Domain/DomainJsonOptions.cs` (not `Domain/CrossCutting/` as assumed in the original spec)
- EntryConfig enums (WaitMode, TraceLevel) already have `[JsonPropertyName]` on members — no changes needed there
- 7 types use manual constructors (TraversalPlan, TraversalNode, EntryPolicy, CompletionPolicy, ChildrenStrategy, DynamicRule, ErrorPolicy, Precondition, EntryConfig, Operation, Target, RestoreAction) — STJ must match their parameter names
- 2 types use primary constructors (IntentSlots, MatchCondition) — STJ handles these automatically
- `Dictionary<string, object>?` appears in: TraversalPlan.Meta, TraversalNode.Meta, EntryPolicy.WaitCondition, MatchCondition.Custom
- `ImmutableDictionary<string, object>` appears in: Operation.Params, Target.Meta, RestoreAction.Params

## Goals / Non-Goals

**Goals:**
- TraversalPlan + all dependency-tree types round-trip via DomainJsonOptions.Default (serialize → deserialize → `Assert.Equal(original, deserialized)`)
- ObjectDictionaryConverter handles `Dictionary<string, object>` with CLR type inference (no JsonElement leaking)
- ImmutableObjectDictionaryConverter handles `ImmutableDictionary<string, object>` with same type inference + ImmutableDictionary construction
- `[JsonPropertyName]` on every public property of dependency-tree types enables STJ parameter name matching
- DomainValidationException fires on deserialization of invalid values (same fail-fast as manual construction)
- Convenience methods `ToJson()`/`FromJson()` on TraversalPlan for ergonomic usage
- ~25 new tests covering all scenarios

**Non-Goals:**
- Python JSON interop (snake_case ↔ camelCase bridge) — C# self-roundtrip only
- TraversalResult serialization
- Other Graph type serialization (outside dependency tree)
- File I/O — Domain layer stays pure classlib, no filesystem dependency
- Plan validation beyond constructor fail-fast (no separate PlanValidator service)
- Changing any type's field types (Dictionary stays Dictionary, ImmutableDictionary stays ImmutableDictionary)

## Decisions

### D-1: ObjectDictionaryConverter placement — `Domain/` (same directory as DomainJsonOptions)

**Choice**: Place `ObjectDictionaryConverter.cs` and `ImmutableObjectDictionaryConverter.cs` in `Domain/` alongside `DomainJsonOptions.cs`.

**Alternatives considered**:
- `Domain/CrossCutting/` — this directory doesn't exist; the design spec incorrectly assumed it
- `Graph/Models/` — wrong; `Dictionary<string, object>` is not Graph-specific (EntryPolicy.WaitCondition is in TraversalPlan.cs but the pattern is cross-cutting)

**Rationale**: DomainJsonOptions.cs is the single point of registration. Placing converters next to it follows "proximity to consumer" principle. Any layer may encounter `Dictionary<string, object>`, so Domain is the right layer.

### D-2: ImmutableDictionary converter as separate converter (not merged into ObjectDictionaryConverter)

**Choice**: Create `ImmutableObjectDictionaryConverter : JsonConverter<ImmutableDictionary<string, object>>` as a separate class.

**Alternatives considered**:
- Merge into ObjectDictionaryConverter — STJ `JsonConverter<T>` is type-specific; one converter can't serve both `Dictionary<string, object>` and `ImmutableDictionary<string, object>`
- Convert ImmutableDictionary fields to Dictionary — changes type signatures, breaks existing code
- Use `[JsonConverter]` attribute on individual properties — works but scattered, harder to maintain

**Rationale**: Two separate converters, each targeting its exact type. Both share the same CLR type inference logic (extracted into a private `InferClrValue` helper). Register both on DomainJsonOptions.Default.

### D-3: [JsonPropertyName] strategy — annotate every property, not rely on CamelCase naming policy alone

**Choice**: Add explicit `[JsonPropertyName("camelCaseKey")]` on every public property in the dependency tree.

**Alternatives considered**:
- Rely solely on `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` — works for serialization but STJ deserialization with parameterized constructors requires exact parameter name matching; `PropertyNamingPolicy` doesn't apply to constructor parameter resolution in all STJ versions
- Use `[JsonConstructor]` — would work but is more invasive and doesn't solve the naming mismatch for properties that differ from camelCase (e.g. `WaitCondition` → `waitCondition`)

**Rationale**: Explicit annotations are deterministic, version-safe, and serve as documentation of the JSON contract. They make the mapping visible and auditable. The slight verbosity (~80 annotations across ~12 types) is worth the reliability gain.

### D-4: Constructor parameter name matching — [JsonPropertyName] on constructor parameters too

**Choice**: For types with manual constructors, add `[JsonPropertyName]` on constructor parameters to ensure STJ maps JSON keys to constructor arguments correctly.

**Rationale**: STJ resolves constructor parameters by name. If a JSON key is `entryApp` but the constructor parameter is `EntryApp`, STJ won't match them without `[JsonPropertyName("entryApp")]` on the parameter. This is the critical blocker for deserialization of manual-constructor types.

### D-5: Convenience methods — ToJson/FromJson on TraversalPlan, not on each sub-type

**Choice**: Add `ToJson()` and `FromJson(string)` only on TraversalPlan. Sub-types don't get convenience methods.

**Alternatives considered**:
- Add convenience methods on every type — too much surface area, most sub-types are never serialized independently in the "load plan from file" workflow
- Add convenience methods as extension methods — less discoverable

**Rationale**: The "load plan from file and run" workflow starts and ends with TraversalPlan. Sub-types round-trip automatically as part of the parent. Users who need to serialize sub-types independently can call `JsonSerializer.Serialize<T>(value, DomainJsonOptions.Default)` directly.

### D-6: ImmutableDictionary deserialization — deserialize as Dictionary then convert

**Choice**: `ImmutableObjectDictionaryConverter.Read()` deserializes the JSON object into a temporary `Dictionary<string, object>` using the same type inference, then converts via `.ToImmutableDictionary()`.

**Rationale**: ImmutableDictionary has no public constructor STJ can target. Building via Dictionary → ToImmutableDictionary is the standard pattern. The conversion is O(1) for empty dictionaries (most Params/Meta are empty in practice) and O(n) for populated ones.

## Risks / Trade-offs

- **[STJ constructor matching fragility]** → Mitigation: explicit `[JsonPropertyName]` on both properties AND constructor parameters. Test round-trip for every type. If STJ version changes behavior, tests catch it immediately.

- **[ObjectDictionaryConverter type inference edge cases]** — `double` vs `int` ambiguity for numbers like `1.0` → Mitigation: prefer `long` for integers, `double` for anything with a decimal point. Edge case: very large integers (>long.MaxValue) → fall to double. This matches Python's JSON number handling.

- **[ImmutableDictionary round-trip overhead]** — extra Dictionary → ImmutableDictionary conversion → Mitigation: negligible cost. Empty ImmutableDictionary (most common case) is O(1). The converter short-circuits on empty JSON objects.

- **[Annotation sprawl]** — ~80 `[JsonPropertyName]` attributes across ~12 files → Mitigation: each annotation is a one-liner, mechanically derivable from the property name. No cognitive complexity.

- **[DomainJsonOptions.Default is `get`-only (init unavailable)]** — the `Default` property uses `{ get; } = new() { ... }` syntax, which means Converters can only be added at initialization. → Mitigation: add `ObjectDictionaryConverter` and `ImmutableObjectDictionaryConverter` in the collection initializer alongside `JsonStringEnumConverter`. No runtime modification needed.

- **[Computed properties (IsContainer, IsLeaf, StaticChildren) on TraversalNode]** — these are not in the constructor, so STJ won't try to deserialize them → Mitigation: `[JsonIgnore]` on computed properties to prevent STJ from expecting them in JSON input. Already not serialized (init-only properties with no setter), but explicit `[JsonIgnore]` is safer.
