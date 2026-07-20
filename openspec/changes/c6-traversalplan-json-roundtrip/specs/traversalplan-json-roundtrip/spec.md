## ADDED Requirements

### Requirement: TraversalPlan dependency tree types SHALL have [JsonPropertyName] annotations on all public properties

Every public init-only property on each type in the TraversalPlan dependency tree SHALL be annotated with `[JsonPropertyName]` specifying its camelCase key name. The dependency tree includes: `TraversalPlan` (12 properties), `TraversalNode` (8 properties), `EntryPolicy` (4 properties), `EntryConfig` (5 properties), `CompletionPolicy` (6 properties), `IntentSlots` (9 properties), `Operation` (4 properties), `Target` (3 properties), `RestoreAction` (3 properties), `ChildrenStrategy` (4 properties), `DynamicRule` (4 properties), `MatchCondition` (7 properties), `ErrorPolicy` (4 properties), `Precondition` (4 properties). `[JsonPropertyName]` SHALL also be added on constructor parameters for types with manual constructors (to enable STJ constructor parameter matching during deserialization). The annotation key SHALL match the camelCase form of the C# property name (e.g., `EntryApp` → `"entryApp"`, `RootNode` → `"rootNode"`, `WaitTimeoutSeconds` → `"waitTimeoutSeconds"`). EntryConfig enum members (WaitMode, TraceLevel) already have `[JsonPropertyName]` and SHALL NOT be modified.

#### Scenario: TraversalPlan round-trips via DomainJsonOptions.Default
- **WHEN** a fully populated `TraversalPlan` (all 12 fields including Meta, StaticNodes, RootNode with nested TraversalNode) is serialized via `JsonSerializer.Serialize(plan, DomainJsonOptions.Default)` then deserialized via `JsonSerializer.Deserialize<TraversalPlan>(json, DomainJsonOptions.Default)`
- **THEN** `Assert.Equal(original, deserialized)` succeeds (record Equals does field-level comparison)

#### Scenario: Minimal TraversalPlan round-trips (only required fields)
- **WHEN** a minimal `TraversalPlan` (EntryApp + EntryPolicy only, all optional fields null/default) is serialized then deserialized
- **THEN** the deserialized plan has `EntryApp` matching, `EntryPolicy` matching, and all optional fields null/default
- **AND** `Assert.Equal(original, deserialized)` succeeds

#### Scenario: EntryPolicy round-trips independently
- **WHEN** an `EntryPolicy` with Strategy=ColdLaunch, WaitCondition containing `Dictionary<string, object>` entries, and TimeoutSeconds=30 is serialized then deserialized
- **THEN** the deserialized EntryPolicy equals the original, including WaitCondition with CLR-typed values (string/long/bool, not JsonElement)

#### Scenario: EntryConfig round-trips independently
- **WHEN** an `EntryConfig` with WaitMode=Polling, WaitTimeoutSeconds=15.0, WaitIntervalMs=1000, ActionDelayMs=500, TraceLevel=Detailed is serialized then deserialized
- **THEN** the deserialized EntryConfig equals the original
- **AND** WaitMode serializes as `"polling"` (existing [JsonPropertyName] preserved)
- **AND** TraceLevel serializes as `"detailed"` (existing [JsonPropertyName] preserved)

#### Scenario: CompletionPolicy round-trips independently for each CompletionPolicyType
- **WHEN** `CompletionPolicy` instances of type Exhaustive, TargetFound, Timeout, and MaxSteps are each serialized then deserialized
- **THEN** each deserialized CompletionPolicy equals its original

#### Scenario: IntentSlots round-trips independently
- **WHEN** an `IntentSlots` with TargetApp="Settings", Scope="target_only", Target="WiFi", Depth=3 is serialized then deserialized
- **THEN** the deserialized IntentSlots equals the original

#### Scenario: TraversalNode round-trips independently with nested sub-types
- **WHEN** a `TraversalNode` with NodeId, Name, NodeType=Screen, Operation(Click), ChildrenStrategy(DynamicMatch), Precondition, ErrorPolicy, and Meta is serialized then deserialized
- **THEN** the deserialized TraversalNode equals the original
- **AND** nested Operation, ChildrenStrategy, Precondition, ErrorPolicy all round-trip correctly

#### Scenario: Operation round-trips independently
- **WHEN** an `Operation` with Action=Click, Target(Text,"WiFi switch"), Params containing `ImmutableDictionary<string, object>` entries, and RestoreAction(Back) is serialized then deserialized
- **THEN** the deserialized Operation equals the original
- **AND** Params contains CLR-typed values (string/long/bool, not JsonElement)

#### Scenario: Target round-trips independently
- **WHEN** a `Target` with By=Text, Value="button", Meta containing `ImmutableDictionary<string, object>` entries is serialized then deserialized
- **THEN** the deserialized Target equals the original

#### Scenario: ChildrenStrategy round-trips independently
- **WHEN** a `ChildrenStrategy` with Type=DynamicMatch, DynamicRules containing 2 DynamicRule entries, MaxChildren=500 is serialized then deserialized
- **THEN** the deserialized ChildrenStrategy equals the original

#### Scenario: DynamicRule round-trips independently
- **WHEN** a `DynamicRule` with RuleId="menu_rule", MatchCondition(MenuItemType="menu_item"), ChildTemplate="menu_container", Action=GenerateChild is serialized then deserialized
- **THEN** the deserialized DynamicRule equals the original

#### Scenario: MatchCondition round-trips independently
- **WHEN** a `MatchCondition` with Type="switch", ExpectedAction="click", TextPattern="WiFi", TextMatchMode=Exact, Custom containing `Dictionary<string, object>` entries is serialized then deserialized
- **THEN** the deserialized MatchCondition equals the original

#### Scenario: ErrorPolicy round-trips independently
- **WHEN** an `ErrorPolicy` with OnError=Retry, MaxRetries=3, FallbackTarget="home", ContinueOnError=true is serialized then deserialized
- **THEN** the deserialized ErrorPolicy equals the original

#### Scenario: Precondition round-trips independently
- **WHEN** a `Precondition` with PageName="settings", Path=["home","settings"], UiCondition="visible", TimeoutSeconds=10 is serialized then deserialized
- **THEN** the deserialized Precondition equals the original

### Requirement: TraversalPlan SHALL provide ToJson and FromJson convenience methods

`TraversalPlan` SHALL have two public convenience methods: `ToJson()` which serializes the plan to a JSON string via `DomainJsonOptions.Default`, and `FromJson(string json)` which deserializes a JSON string to a `TraversalPlan` via `DomainJsonOptions.Default`. `FromJson` SHALL throw `DomainValidationException` with FieldName `"TraversalPlan"` and IllegalValue `"null JSON input"` when the deserialization result is null (empty/invalid JSON). No file I/O SHALL be performed — these methods operate on strings only.

#### Scenario: ToJson produces valid camelCase JSON
- **WHEN** `plan.ToJson()` is called on a TraversalPlan with EntryApp="Settings", EntryPolicy(ColdLaunch)
- **THEN** the result is a JSON string containing `"entryApp": "Settings"` and `"entryPolicy": { "strategy": "coldLaunch" }`

#### Scenario: FromJson deserializes to equal TraversalPlan
- **WHEN** `TraversalPlan.FromJson(plan.ToJson())` is called
- **THEN** the result equals the original plan (`Assert.Equal(original, deserialized)` succeeds)

#### Scenario: FromJson throws DomainValidationException on null JSON input
- **WHEN** `TraversalPlan.FromJson("")` or `TraversalPlan.FromJson("null")` is called
- **THEN** `DomainValidationException` is thrown with FieldName `"TraversalPlan"`

#### Scenario: FromJson throws DomainValidationException on invalid EntryApp
- **WHEN** `TraversalPlan.FromJson("{ \"entryApp\": \"\", \"entryPolicy\": { \"strategy\": \"coldLaunch\", \"timeoutSeconds\": 10 } }")` is called
- **THEN** `DomainValidationException` is thrown (constructor fail-fast for empty EntryApp)

### Requirement: Deserialization SHALL preserve DomainValidationException fail-fast behavior

When JSON input contains values that violate a type's constructor validation rules, deserialization via `DomainJsonOptions.Default` SHALL throw `DomainValidationException` with the same FieldName and behavior as manual construction. This applies to all types in the dependency tree: TraversalPlan (EntryApp empty, RootNode malformed), EntryPolicy (TimeoutSeconds out of range), CompletionPolicy (TargetFound without TargetName, out-of-range TimeoutSeconds/MaxSteps), TraversalNode (NodeId/Name empty), Operation (Action undefined), EntryConfig (out-of-range parameters), ChildrenStrategy (MaxChildren out of range), ErrorPolicy (MaxRetries out of range), Precondition (TimeoutSeconds out of range), DynamicRule (RuleId/ChildTemplate empty).

#### Scenario: Deserialize TraversalPlan with empty EntryApp throws DomainValidationException
- **WHEN** JSON with `entryApp: ""` is deserialized into `TraversalPlan`
- **THEN** `DomainValidationException` is thrown with FieldName `"EntryApp"`

#### Scenario: Deserialize TraversalPlan with malformed RootNode throws DomainValidationException
- **WHEN** JSON with a RootNode of NodeType=Leaf is deserialized into `TraversalPlan`
- **THEN** `DomainValidationException` is thrown with FieldName `"RootNode.NodeType"`

#### Scenario: Deserialize CompletionPolicy TargetFound without TargetName throws DomainValidationException
- **WHEN** JSON with CompletionPolicy Type=TargetFound but TargetName=null/empty is deserialized
- **THEN** `DomainValidationException` is thrown with FieldName `"TargetName"`

#### Scenario: Deserialize EntryPolicy with TimeoutSeconds=0 throws DomainValidationException
- **WHEN** JSON with EntryPolicy TimeoutSeconds=0 is deserialized
- **THEN** `DomainValidationException` is thrown with FieldName `"TimeoutSeconds"`

#### Scenario: Deserialize TraversalNode with empty NodeId throws DomainValidationException
- **WHEN** JSON with TraversalNode NodeId="" is deserialized
- **THEN** `DomainValidationException` is thrown with FieldName `"NodeId"`

### Requirement: JSON with extra unknown fields SHALL be tolerated during deserialization

When JSON input contains fields not present in the target type's properties or constructor parameters, deserialization SHALL complete normally without throwing. Unknown fields SHALL be silently ignored. This enables forward-compatible plan files — newer versions can add fields that older code gracefully ignores.

#### Scenario: TraversalPlan with extra unknown field deserializes normally
- **WHEN** JSON containing `{ "entryApp": "Settings", "entryPolicy": { ... }, "futureField": "someValue" }` is deserialized into `TraversalPlan`
- **THEN** deserialization succeeds and the resulting TraversalPlan has `EntryApp="Settings"` and `EntryPolicy` populated, with `futureField` silently ignored

### Requirement: Computed properties on TraversalNode SHALL have JsonIgnore

`TraversalNode` computed properties `IsContainer`, `IsLeaf`, and `StaticChildren` SHALL be annotated with `[JsonIgnore]` to prevent STJ from expecting them in JSON input or including them in output. These properties are derived from other fields and have no constructor parameters.

#### Scenario: TraversalNode serialization omits computed properties
- **WHEN** a `TraversalNode` is serialized via `DomainJsonOptions.Default`
- **THEN** the JSON output does NOT contain keys `"isContainer"`, `"isLeaf"`, or `"staticChildren"`

#### Scenario: TraversalNode deserialization ignores computed properties in input
- **WHEN** JSON containing `"isContainer": true` alongside other TraversalNode fields is deserialized
- **THEN** deserialization succeeds and `isContainer` is silently ignored

### Requirement: StaticNodes dictionary keys SHALL NOT be camelCase-transformed

`TraversalPlan.StaticNodes` is a `Dictionary<string, TraversalNode>` where keys are semantic node IDs (e.g., `"network_menu"`, `"wifi_switch"`). STJ SHALL NOT apply camelCase naming policy to Dictionary keys — keys SHALL preserve their original form in JSON output and match exactly during deserialization. This is the default STJ behavior for Dictionary keys (PropertyNamingPolicy only applies to object properties, not Dictionary keys).

#### Scenario: StaticNodes keys preserve original form
- **WHEN** a `TraversalPlan` with `StaticNodes = { "network_menu": node1, "wifi_switch": node2 }` is serialized
- **THEN** the JSON output has keys `"network_menu"` and `"wifi_switch"` (not `"networkMenu"` or `"wifiSwitch"`)
- **AND** deserialization produces `StaticNodes` with exactly those keys
