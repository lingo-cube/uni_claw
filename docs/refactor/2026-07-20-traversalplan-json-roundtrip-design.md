# C-6: TraversalPlan JSON Read/Write — Design Spec

> Date: 2026-07-20
> Priority: P3 (C-bucket backlog, last code item)
> Branch: feature/refactor

## 1. Summary

Make TraversalPlan support full JSON round-trip (serialize + deserialize) via `DomainJsonOptions.Default`, enabling the "load plan from file and run" workflow. C# self-roundtrip only — no Python interop required.

## 2. Current State

- **Serialization** (→JSON): Partially works — any record can be serialized via `DomainJsonOptions.Default`, but never tested for TraversalPlan.
- **Deserialization** (JSON→): Does NOT work. 6 blockers:
  1. Parameterized constructors with validation — STJ parameter name matching
  2. `Dictionary<string, object>` Meta — STJ deserializes `object` as `JsonElement`
  3. Nested records (TraversalNode, IntentSlots, etc.) — each has custom constructors
  4. Enum naming (resolved: C# self-roundtrip only, no snake_case needed)
  5. Null/default handling — missing keys vs explicit nulls
  6. Fail-fast validation on deserialization — must preserve DomainValidationException behavior

## 3. Architecture & Layering

### Scope: TraversalPlan Dependency Tree

```
TraversalPlan (12 fields)
  ├── EntryPolicy (4 fields) + EntryStrategy enum (3 values)
  ├── EntryConfig? (2 fields) + WaitMode/TraceLevel enums
  ├── TraversalNode? (8 fields) ← recursive nesting
  │   ├── ChildrenStrategy + MatchMode + TargetFoundAction enums
  │   ├── Operation (3 fields) + OperationType enum
  │   ├── ErrorPolicy? (6 fields) + ErrorActionType enum
  │   ├── Precondition? + DynamicRule? + MatchCondition?
  ├── StaticNodes: Dictionary<string, TraversalNode>
  ├── CompletionPolicy? (6 fields) + CompletionPolicyType enum
  ├── IntentSlots? (9 fields) + Scope enum
  ├── TraversalMode enum
  └── Meta: Dictionary<string, object> ← custom Converter
```

~10 types + ~8 enums. Non-dependency-tree Graph types unchanged.

### Infrastructure Placement

- `ObjectDictionaryConverter` → `Domain/` (same directory as DomainJsonOptions.cs — `Domain/CrossCutting/` does not exist)
  - Reason: `Dictionary<string, object>` is not a Graph-specific problem. Any layer may encounter it. Placing alongside DomainJsonOptions follows "proximity to consumer" principle.
- `ImmutableObjectDictionaryConverter` → `Domain/` (separate sealed class for `ImmutableDictionary<string, object>`, required by Operation.Params, Target.Meta, RestoreAction.Params)
- `[JsonPropertyName]` annotations → each type inline (on PROPERTIES only, NOT on manual constructor parameters — STJ case-insensitive matching handles camelCase↔PascalCase mapping; `[property: JsonPropertyName]` on primary constructor parameters only)
- `DomainJsonOptions.Default` → register both converters on existing instance

### What We DON'T Change

- Graph types outside TraversalPlan dependency tree (DynamicMatcher, TraversalEngine, etc.)
- Domain types (already have `[JsonPropertyName]` or don't need it)
- Enum members — `[JsonPropertyName]` on enum members is for Python compat; C-6 doesn't need it (JsonStringEnumConverter with CamelCase handles C# self-roundtrip)

## 4. Component Details

### 4.1 ObjectDictionaryConverter

```csharp
// Domain/CrossCutting/ObjectDictionaryConverter.cs
public sealed class ObjectDictionaryConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object>? Read(ref Utf8JsonReader reader, 
        Type typeToConvert, JsonSerializerOptions options)
    {
        // Iterate JSON object properties
        // Infer CLR type from JsonElement.ValueKind:
        //   String → string
        //   Number → int (if fits) / long / double
        //   True/False → bool
        //   Null → null
        //   Array/Object → preserve as JsonElement (can't safely infer)
    }

    public override void Write(Utf8JsonWriter writer, 
        Dictionary<string, object> value, JsonSerializerOptions options)
    {
        // Write by CLR type: string/int/bool/null/JsonElement→write raw
    }
}
```

- Only handles `Dictionary<string, object>` — does NOT intercept `Dictionary<string, T>` (STJ handles typed dictionaries naturally)
- Preserves unknown nested structures as JsonElement — no data loss

### 4.2 ImmutableObjectDictionaryConverter

```csharp
// Domain/ImmutableObjectDictionaryConverter.cs
public sealed class ImmutableObjectDictionaryConverter : JsonConverter<ImmutableDictionary<string, object>>
{
    public override ImmutableDictionary<string, object>? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialize as Dictionary<string, object> using same type inference as ObjectDictionaryConverter
        // Short-circuit on empty JSON object ("{}" → ImmutableDictionary<string, object>.Empty)
        // Then call .ToImmutableDictionary() for populated objects
    }

    public override void Write(
        Utf8JsonWriter writer, ImmutableDictionary<string, object> value, JsonSerializerOptions options)
    {
        // Iterate ImmutableDictionary entries, write each value by CLR type
        // Short-circuit on Empty (write "{}" without iterating)
    }
}
```

- Required because STJ cannot deserialize into `ImmutableDictionary<string, object>` natively (no public constructor)
- Both converters share `InferClrValue`/`WriteClrValue` type inference logic
- Empty dictionary shortcut (most Operation.Params are empty in practice): O(1)

### 4.3 [JsonPropertyName] Annotation Strategy

For every public property on each record in the dependency tree, annotate with its camelCase key name. Annotations on PROPERTIES only — manual constructor parameters use case-insensitive matching (no `[JsonPropertyName]` needed):

```csharp
// TraversalPlan
[JsonPropertyName("entryApp")]    public string EntryApp { get; }
[JsonPropertyName("planName")]    public string PlanName { get; }
// ...

// TraversalNode
[JsonPropertyName("childrenStrategy")]  public ChildrenStrategy? ChildrenStrategy { get; }
// ...
```

- Nullable fields: `DefaultIgnoreCondition = WhenWritingNull` already configured. Missing keys on deserialization = default/null — consistent with constructor defaults.
- Dictionary keys: `StaticNodes` and `Meta` keys are semantic IDs, not camelCase-transformed (STJ doesn't transform Dictionary keys by default).
- Enum serialization: handled by existing `JsonStringEnumConverter(CamelCase)` — no `[JsonPropertyName]` on enum members needed.

### 4.4 DomainJsonOptions Change

Register both converters on the existing `Default` instance:

```csharp
public static readonly JsonSerializerOptions Default = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    // ... existing ...
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                   new ObjectDictionaryConverter(),
                   new ImmutableObjectDictionaryConverter() }
};
```

### 4.4 Convenience Methods (Optional)

```csharp
// In TraversalPlan
public string ToJson()
    => JsonSerializer.Serialize(this, DomainJsonOptions.Default);

public static TraversalPlan FromJson(string json)
    => JsonSerializer.Deserialize<TraversalPlan>(json, DomainJsonOptions.Default)
       ?? throw new DomainValidationException("TraversalPlan", "null JSON input");
```

No file I/O in Domain layer. Core classlib has no filesystem dependency. Upper layer (TraversalEngine or CLI host) responsible for file read/write.

## 5. Data Flow & Error Handling

### Serialization (TraversalPlan → JSON)

```
TraversalPlan record
  → JsonSerializer.Serialize(plan, DomainJsonOptions.Default)
  → camelCase JSON string
  → Upper layer writes to file (File.WriteAllText etc.)
```

### Deserialization (JSON → TraversalPlan)

```
JSON string
  → JsonSerializer.Deserialize<TraversalPlan>(json, DomainJsonOptions.Default)
  → STJ matches [JsonPropertyName] → calls parameterized constructor
  → Constructor triggers DomainValidationException fail-fast
  → Returns valid TraversalPlan or throws
```

### Error Handling Matrix

| Scenario | Behavior | Reason |
|----------|----------|--------|
| JSON missing required field (EntryApp) | Constructor receives null/empty → DomainValidationException | fail-fast, consistent with manual construction |
| JSON has extra unknown fields | STJ ignores silently (no throw) | Forward-compatible, new fields don't break old plans |
| JSON value type mismatch (string where int expected) | STJ throws JsonException | Upper layer can catch and wrap |
| Meta: uninferrable nested structure | Preserved as JsonElement | No data loss, consumer parses as needed |
| Empty JSON / null input | STJ returns null | FromJson() convenience method throws DomainValidationException |

## 6. Testing Strategy

### New Test File

`tests/UniClaw.Core.Tests/Graph/TraversalPlanSerializationTests.cs`

### Test Matrix

| Test Group | Coverage | Est. Count |
|------------|----------|------------|
| Round-trip baseline | Serialize → Deserialize → Assert.Equal on TraversalPlan full instance | 3-5 |
| Sub-type round-trip | EntryPolicy, CompletionPolicy, IntentSlots, TraversalNode, Operation independently | 6-8 |
| Minimal Plan | Only EntryApp + EntryPolicy (minimal valid plan) round-trip | 1 |
| Null/missing fields | All optional fields omitted → verify defaults correctly populated | 1-2 |
| Meta special cases | Meta with string/int/bool/null/nested JsonElement round-trip | 3-4 |
| Validation fail-fast | Deserialize invalid JSON (empty EntryApp, invalid EntryStrategy) → DomainValidationException | 2-3 |
| Extra fields tolerance | JSON with unknown fields → normal deserialization, no throw | 1 |
| ObjectDictionaryConverter standalone | Converter Read/Write for each JsonElement type independently | 4-5 |

**Total: ~20-25 new tests**. All existing 721 tests remain unchanged.

### Test Method

Use `DomainJsonOptions.Default` for all serialization/deserialization. Verify via `Assert.Equal(original, deserialized)` — record `Equals()` does field-level comparison.

## 7. Out of Scope

- Python JSON interop (snake_case ↔ camelCase bridge)
- TraversalResult serialization
- Other Graph type serialization (outside dependency tree)
- File I/O (filesystem operations)
- Plan validation beyond constructor fail-fast (no separate PlanValidator service)
