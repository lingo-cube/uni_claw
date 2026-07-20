## ADDED Requirements

### Requirement: TraversalPlan JSON round-trip via DomainJsonOptions
TraversalPlan SHALL support full JSON round-trip (serialize + deserialize) via `DomainJsonOptions.Default`, producing a TraversalPlan instance that is structurally equal to the original.

#### Scenario: Full plan round-trip
- **WHEN** a TraversalPlan with all 12 fields populated is serialized to JSON via DomainJsonOptions.Default, then deserialized back
- **THEN** the resulting TraversalPlan SHALL be equal to the original (record Equals() comparison passes)

#### Scenario: Minimal plan round-trip
- **WHEN** a TraversalPlan with only required fields (EntryApp + EntryPolicy) is serialized then deserialized
- **THEN** the resulting TraversalPlan SHALL have EntryApp and EntryPolicy matching the original, with optional fields at their default values (PlanName="", Mode=Hybrid, etc.)

#### Scenario: Null/missing optional fields
- **WHEN** JSON omits all optional fields (RootNode, StaticNodes, IntentSlots, CompletionPolicy, EntryConfig, TemplateRegistry, Meta are absent)
- **THEN** deserialization SHALL produce a TraversalPlan with those fields at their default values (null for nullable fields, "" for string defaults)

### Requirement: Sub-type JSON round-trip
Each nested record type in TraversalPlan's dependency tree SHALL independently support JSON round-trip via DomainJsonOptions.Default.

#### Scenario: TraversalNode round-trip
- **WHEN** a TraversalNode with all 8 fields is serialized then deserialized
- **THEN** the resulting TraversalNode SHALL be equal to the original

#### Scenario: EntryPolicy round-trip
- **WHEN** an EntryPolicy is serialized then deserialized
- **THEN** the resulting EntryPolicy SHALL be equal to the original

#### Scenario: IntentSlots round-trip
- **WHEN** an IntentSlots with all 9 fields is serialized then deserialized
- **THEN** the resulting IntentSlots SHALL be equal to the original

### Requirement: ObjectDictionaryConverter for Dictionary<string, object>
The `ObjectDictionaryConverter` SHALL enable `Dictionary<string, object>` round-trip by inferring CLR types from JsonElement during deserialization.

#### Scenario: Primitive value round-trip
- **WHEN** a Dictionary<string, object> containing string, int, bool, null values is serialized then deserialized
- **THEN** each value SHALL be restored to its original CLR type (string, int, bool, null)

#### Scenario: Nested structure preservation
- **WHEN** a Dictionary<string, object> containing nested JSON objects or arrays is serialized then deserialized
- **THEN** nested structures SHALL be preserved as JsonElement (no data loss, consumer parses as needed)

### Requirement: Fail-fast validation on deserialization
Deserialization SHALL preserve DomainValidationException fail-fast behavior. Invalid JSON data SHALL trigger the same validation exceptions as manual construction.

#### Scenario: Invalid EntryApp
- **WHEN** JSON with empty or null EntryApp is deserialized
- **THEN** DomainValidationException SHALL be thrown with FieldName="EntryApp"

#### Scenario: Invalid EntryStrategy
- **WHEN** JSON with unrecognized EntryStrategy value is deserialized
- **THEN** STJ SHALL throw JsonException (invalid enum value)

### Requirement: Extra fields tolerance
Deserialization SHALL silently ignore unknown JSON fields, enabling forward compatibility.

#### Scenario: JSON with unknown fields
- **WHEN** JSON contains fields not present on TraversalPlan (e.g., "futureField": "value")
- **THEN** deserialization SHALL succeed without error, and the unknown fields SHALL be ignored

### Requirement: Convenience methods
TraversalPlan SHALL provide `ToJson()` and `FromJson(string)` convenience methods for one-line serialization/deserialization.

#### Scenario: ToJson produces valid JSON
- **WHEN** TraversalPlan.ToJson() is called
- **THEN** the result SHALL be a JSON string that can be deserialized back via FromJson()

#### Scenario: FromJson on null input
- **WHEN** FromJson(null) or FromJson("") is called
- **THEN** DomainValidationException SHALL be thrown
