## ADDED Requirements

### Requirement: ObjectDictionaryConverter infers CLR types from JsonElement ValueKind

`ObjectDictionaryConverter` SHALL be a sealed class inheriting `JsonConverter<Dictionary<string, object>>` in namespace `UniClaw.Core.Domain`. It SHALL deserialize JSON objects into `Dictionary<string, object>` by inferring CLR types from `JsonElement.ValueKind`: `String` → `string`, `Number` → `long` (if fits in `long.MinValue`–`long.MaxValue` without loss) / `double` (if the value has a decimal point or exceeds `long` range), `True`/`False` → `bool`, `Null` → `null` (the C# `null` reference, not a JsonElement), `Array`/`Object` → preserve as `JsonElement` (no data loss). On serialization, it SHALL write each CLR value by type: `string` → write as JSON string, `long` → write as JSON number, `double` → write as JSON number, `bool` → write as JSON boolean, `null` → write as JSON null, `JsonElement` → write raw. The converter MUST NOT intercept `Dictionary<string, T>` for any typed `T` — it SHALL only match `Dictionary<string, object>`.

#### Scenario: String value round-trips as string
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "name": "Settings" }`
- **THEN** the resulting `Dictionary<string, object>` contains key `"name"` with value `"Settings"` (CLR type `string`, not `JsonElement`)
- **AND** serializing this dictionary back produces `{ "name": "Settings" }`

#### Scenario: Integer value round-trips as long
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "count": 42 }`
- **THEN** the resulting `Dictionary<string, object>` contains key `"count"` with value `42L` (CLR type `long`)
- **AND** serializing this dictionary back produces `{ "count": 42 }`

#### Scenario: Large integer that exceeds long range becomes double
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "big": 99999999999999999999 }` (value exceeds `long.MaxValue`)
- **THEN** the resulting `Dictionary<string, object>` contains key `"big"` with value of CLR type `double`

#### Scenario: Floating-point value round-trips as double
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "ratio": 3.14 }`
- **THEN** the resulting `Dictionary<string, object>` contains key `"ratio"` with value `3.14` (CLR type `double`)
- **AND** serializing this dictionary back produces `{ "ratio": 3.14 }`

#### Scenario: Boolean value round-trips as bool
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "enabled": true }`
- **THEN** the resulting `Dictionary<string, object>` contains key `"enabled"` with value `true` (CLR type `bool`)

#### Scenario: Null value becomes C# null
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "optional": null }`
- **THEN** the resulting `Dictionary<string, object>` contains key `"optional"` with value `null` (C# null reference, not a JsonElement)

#### Scenario: Nested object preserved as JsonElement without data loss
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "nested": { "a": 1, "b": "x" } }`
- **THEN** the resulting `Dictionary<string, object>` contains key `"nested"` with value of CLR type `JsonElement`
- **AND** serializing this dictionary back produces `{ "nested": { "a": 1, "b": "x" } }` (no data loss)

#### Scenario: Array preserved as JsonElement without data loss
- **WHEN** `ObjectDictionaryConverter` deserializes a JSON object `{ "items": [1, 2, 3] }`
- **THEN** the resulting `Dictionary<string, object>` contains key `"items"` with value of CLR type `JsonElement`
- **AND** serializing this dictionary back produces `{ "items": [1, 2, 3] }` (no data loss)

#### Scenario: Empty JSON object produces empty Dictionary
- **WHEN** `ObjectDictionaryConverter` deserializes `{ }`
- **THEN** the resulting `Dictionary<string, object>` has `Count == 0`

#### Scenario: Converter does not intercept typed dictionaries
- **WHEN** a `Dictionary<string, TraversalNode>` is serialized/deserialized via `DomainJsonOptions.Default`
- **THEN** `ObjectDictionaryConverter` is NOT invoked — STJ handles the typed dictionary natively

### Requirement: ImmutableObjectDictionaryConverter handles ImmutableDictionary<string, object> round-trip

`ImmutableObjectDictionaryConverter` SHALL be a sealed class inheriting `JsonConverter<ImmutableDictionary<string, object>>` in namespace `UniClaw.Core.Domain`. On deserialization, it SHALL first deserialize the JSON object into a `Dictionary<string, object>` using the same CLR type inference logic as `ObjectDictionaryConverter`, then convert to `ImmutableDictionary<string, object>` via `.ToImmutableDictionary()`. On serialization, it SHALL write the ImmutableDictionary contents identically to how `ObjectDictionaryConverter` writes a regular `Dictionary<string, object>`. The converter MUST NOT intercept `ImmutableDictionary<string, T>` for any typed `T`.

#### Scenario: ImmutableDictionary with string value round-trips
- **WHEN** `ImmutableObjectDictionaryConverter` deserializes `{ "key": "value" }`
- **THEN** the result is an `ImmutableDictionary<string, object>` containing `"key"` → `"value"` (CLR type `string`)
- **AND** serializing it back produces `{ "key": "value" }`

#### Scenario: Empty JSON object produces empty ImmutableDictionary
- **WHEN** `ImmutableObjectDictionaryConverter` deserializes `{ }`
- **THEN** the result is `ImmutableDictionary<string, object>.Empty`

#### Scenario: ImmutableDictionary with mixed types round-trips
- **WHEN** `ImmutableObjectDictionaryConverter` deserializes `{ "name": "test", "count": 5, "flag": true }`
- **THEN** the result is an `ImmutableDictionary<string, object>` with `"name"` → `"test"` (string), `"count"` → `5L` (long), `"flag"` → `true` (bool)

#### Scenario: Converter does not intercept typed ImmutableDictionaries
- **WHEN** an `ImmutableDictionary<string, string>` is serialized/deserialized via `DomainJsonOptions.Default`
- **THEN** `ImmutableObjectDictionaryConverter` is NOT invoked — STJ handles typed ImmutableDictionary natively

### Requirement: Both converters SHALL be registered on DomainJsonOptions.Default

`DomainJsonOptions.Default` SHALL include both `ObjectDictionaryConverter` and `ImmutableObjectDictionaryConverter` in its `Converters` collection, alongside the existing `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`. The registration SHALL occur in the collection initializer — no runtime modification of the options instance after initialization.

#### Scenario: DomainJsonOptions.Default has 3 converters
- **WHEN** `DomainJsonOptions.Default.Converters` is inspected
- **THEN** it contains exactly 3 converters: `JsonStringEnumConverter`, `ObjectDictionaryConverter`, `ImmutableObjectDictionaryConverter`

#### Scenario: Existing serialization behavior unchanged after converter registration
- **WHEN** any Domain type (BoundingBox, Region, etc.) is serialized/deserialized via `DomainJsonOptions.Default`
- **THEN** the output is identical to before converter registration (no regression)
