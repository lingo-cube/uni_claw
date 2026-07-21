## ADDED Requirements

### Requirement: TraversalPlan supports full JSON round-trip via DomainJsonOptions.Default
TraversalPlan SHALL serialize to JSON and deserialize back to an equal TraversalPlan instance using `DomainJsonOptions.Default`. C# self-roundtrip only — no Python interop required. All public properties on TraversalPlan and its dependency tree types SHALL be annotated with `[JsonPropertyName("camelCaseName")]` for explicit key mapping. STJ case-insensitive parameter matching handles constructor parameter → property mapping; `[JsonPropertyName]` is NOT needed on manual constructor parameters. `DomainJsonOptions.Default` SHALL register `ObjectDictionaryConverter` (for `Dictionary<string, object>` Meta field) and `ImmutableObjectDictionaryConverter` (for `ImmutableDictionary<string, object>` in Operation.Params, Target.Meta, RestoreAction.Params).

#### Scenario: Full TraversalPlan round-trip preserves all 12 fields
- **WHEN** a TraversalPlan with all 12 fields populated (including StaticNodes, Meta with mixed CLR types, RootNode with nested TraversalNode) is serialized then deserialized via DomainJsonOptions.Default
- **THEN** the deserialized instance equals the original via record `Equals()` — all fields match

#### Scenario: Minimal TraversalPlan round-trip (EntryApp + EntryPolicy only)
- **WHEN** a TraversalPlan with only EntryApp and EntryPolicy (all optional fields default/null) is serialized then deserialized
- **THEN** the deserialized instance equals the original; null/missing fields correctly default

#### Scenario: Meta field preserves string, int, bool, null, and nested JsonElement
- **WHEN** Meta contains `{ "key_str": "hello", "key_int": 42, "key_bool": true, "key_null": null, "key_obj": { "nested": true } }`
- **THEN** after round-trip: string→string, int→int, bool→bool, null→null, nested object→JsonElement (preserved, no data loss)

#### Scenario: StaticNodes Dictionary keys are NOT camelCase-transformed
- **WHEN** StaticNodes contains key `"network_menu"`
- **THEN** after round-trip the key remains `"network_menu"` (STJ doesn't transform Dictionary keys by default)

### Requirement: ObjectDictionaryConverter infers CLR type from JsonElement.ValueKind
ObjectDictionaryConverter SHALL deserialize JSON `object` values by inferring CLR type: String→string, Number→int (if fits)/long/double, True/False→bool, Null→null, Array/Object→preserve as JsonElement. Write SHALL serialize by CLR type. Only handles `Dictionary<string, object>` — does NOT intercept typed dictionaries.

#### Scenario: ObjectDictionaryConverter round-trips each primitive type
- **WHEN** `Dictionary<string, object>` with string/int/bool/null/nested entries is serialized then deserialized
- **THEN** each value matches its original CLR type

### Requirement: ImmutableObjectDictionaryConverter handles ImmutableDictionary<string, object>
ImmutableObjectDictionaryConverter SHALL deserialize via ObjectDictionaryConverter logic then convert to `ImmutableDictionary<string, object>`. Empty JSON object `"{}"` SHALL short-circuit to `ImmutableDictionary<string, object>.Empty`. Write SHALL iterate ImmutableDictionary entries by CLR type. Empty SHALL short-circuit to `"{}"` without iteration.

#### Scenario: ImmutableObjectDictionaryConverter round-trips populated and empty dictionaries
- **WHEN** populated `ImmutableDictionary<string, object>` with mixed types is serialized then deserialized
- **THEN** all entries match original CLR types and values
- **WHEN** `ImmutableDictionary<string, object>.Empty` is serialized then deserialized
- **THEN** result is Empty (O(1) shortcut)

### Requirement: Fail-fast validation preserved on deserialization
Deserialization SHALL preserve DomainValidationException fail-fast behavior. Invalid JSON (empty EntryApp, invalid EntryStrategy enum) SHALL trigger constructor validation → DomainValidationException. Extra unknown fields SHALL be silently ignored (forward-compatible).

#### Scenario: Deserialize invalid JSON throws DomainValidationException
- **WHEN** JSON with empty EntryApp is deserialized
- **THEN** DomainValidationException is thrown (constructor validation)

#### Scenario: Unknown fields are silently ignored
- **WHEN** JSON contains extra fields not on TraversalPlan
- **THEN** deserialization succeeds; extra fields are dropped

### Requirement: TraversalPlan convenience methods
TraversalPlan SHALL expose `ToJson()` (serialize via DomainJsonOptions.Default) and `FromJson(string)` (deserialize + throw DomainValidationException on null/malformed input). No file I/O in Domain layer.

#### Scenario: ToJson produces valid camelCase JSON
- **WHEN** TraversalPlan.ToJson() is called
- **THEN** result is a valid JSON string with camelCase keys

#### Scenario: FromJson wraps null input as DomainValidationException
- **WHEN** TraversalPlan.FromJson("null") or null string is called
- **THEN** DomainValidationException is thrown
