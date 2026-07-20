## 1. ObjectDictionaryConverter

- [x] 1.1 Create `Domain/ObjectDictionaryConverter.cs` — sealed class inheriting `JsonConverter<Dictionary<string, object>>` with CLR type inference logic (String→string, Number→long/double, True/False→bool, Null→null, Array/Object→JsonElement). Write method dispatches by CLR type.
- [x] 1.2 Create `Domain/ImmutableObjectDictionaryConverter.cs` — sealed class inheriting `JsonConverter<ImmutableDictionary<string, object>>`. Read: deserialize as Dictionary via same type inference, then `.ToImmutableDictionary()`. Write: iterate ImmutableDictionary entries, write each value by CLR type (same logic as ObjectDictionaryConverter.Write).
- [x] 1.3 Register both converters on `DomainJsonOptions.Default` — add to Converters collection initializer alongside existing `JsonStringEnumConverter`
- [x] 1.4 Create `tests/UniClaw.Core.Tests/Domain/CrossCutting/ObjectDictionaryConverterTests.cs` — 4-5 tests: string round-trip, number (int+double) round-trip, bool round-trip, null round-trip, nested object/array preserved as JsonElement, empty dictionary, typed Dictionary<string, T> not intercepted
- [x] 1.5 Create `tests/UniClaw.Core.Tests/Domain/CrossCutting/ImmutableObjectDictionaryConverterTests.cs` — 3-4 tests: string round-trip, empty→Empty, mixed types, typed ImmutableDictionary<string, T> not intercepted
- [x] 1.6 Run `dotnet test` — verify all existing tests + new converter tests pass, no regression

## 2. JsonPropertyName Annotations — Graph Types (TraversalPlan.cs)

- [x] 2.1 Add `[JsonPropertyName]` on all properties AND constructor parameters of `TraversalPlan` (12 fields: entryApp, planName, planId, entryPolicy, entryConfig, rootNode, staticNodes, templateRegistry, mode, completionPolicy, intentSlots, meta)
- [x] 2.2 Add `[JsonPropertyName]` on all properties AND constructor parameters of `EntryPolicy` (4 fields: strategy, fallback, waitCondition, timeoutSeconds)
- [x] 2.3 Add `[JsonPropertyName]` on all properties AND constructor parameters of `CompletionPolicy` (6 fields: type, targetName, matchMode, actionOnFound, timeoutSeconds, maxSteps)
- [x] 2.4 Add `[JsonPropertyName]` on all properties of `IntentSlots` primary constructor (9 fields: targetApp, scope, target, depth, elementHandling, navigation, restore, completion, entry)
- [x] 2.5 Add `[JsonPropertyName]` on all enums defined in TraversalPlan.cs — only needed on properties/constructor params (already done in 2.1-2.3), NOT on enum members (EntryStrategy, CompletionPolicyType, MatchMode, TargetFoundAction, TraversalMode) — only needed on properties/constructor params, NOT on enum members (JsonStringEnumConverter handles C# self-roundtrip)

## 3. JsonPropertyName Annotations — Graph Types (TraversalNode.cs)

- [ ] 3.1 Add `[JsonPropertyName]` on all properties AND constructor parameters of `TraversalNode` (8 fields: nodeId, name, nodeType, operation, childrenStrategy, precondition, errorPolicy, meta). Add `[JsonIgnore]` on computed properties (IsContainer, IsLeaf, StaticChildren).
- [ ] 3.2 Add `[JsonPropertyName]` on all properties AND constructor parameters of `ChildrenStrategy` (4 fields: type, staticChildren, dynamicRules, maxChildren)
- [ ] 3.3 Add `[JsonPropertyName]` on all properties AND constructor parameters of `DynamicRule` (4 fields: ruleId, matchCondition, childTemplate, action)
- [ ] 3.4 Add `[JsonPropertyName]` on all properties of `MatchCondition` primary constructor (7 fields: type, expectedAction, textPattern, textMatchMode, minIndex, maxIndex, custom)
- [ ] 3.5 Add `[JsonPropertyName]` on all properties AND constructor parameters of `ErrorPolicy` (4 fields: onError, maxRetries, fallbackTarget, continueOnError)
- [ ] 3.6 Add `[JsonPropertyName]` on all properties AND constructor parameters of `Precondition` (4 fields: pageName, path, uiCondition, timeoutSeconds)
- [ ] 3.7 Add `[JsonPropertyName]` on enums defined in TraversalNode.cs that DON'T already have member annotations (TextMatchMode, ChildrenStrategyType, MatchAction, ErrorPolicyType, FallbackAction) — only on properties/constructor params, NOT on enum members

## 4. JsonPropertyName Annotations — Domain.Common Types

- [ ] 4.1 Add `[JsonPropertyName]` on all properties AND constructor parameters of `Operation` (4 fields: action, target, params, restore) — note: `params` is a C# keyword context; use `[JsonPropertyName("params")]` to map JSON key
- [ ] 4.2 Add `[JsonPropertyName]` on all properties AND constructor parameters of `Target` (3 fields: by, value, meta)
- [ ] 4.3 Add `[JsonPropertyName]` on all properties AND constructor parameters of `RestoreAction` (3 fields: action, target, params)
- [ ] 4.4 Add `[JsonPropertyName]` on Domain.Common enums (OperationType, TargetType) — only on properties/constructor params, NOT on enum members
- [ ] 4.5 Verify Domain.Vision types (BoundingBox, Region, etc.) still have their existing `[JsonPropertyName]` annotations and are unchanged

## 5. JsonPropertyName Annotations — EntryConfig

- [ ] 5.1 Add `[JsonPropertyName]` on all properties AND constructor parameters of `EntryConfig` (5 fields: waitMode, waitTimeoutSeconds, waitIntervalMs, actionDelayMs, traceLevel) — enum members (WaitMode, TraceLevel) already have [JsonPropertyName], do NOT modify them

## 6. Convenience Methods

- [ ] 6.1 Add `ToJson()` method on `TraversalPlan` — `JsonSerializer.Serialize(this, DomainJsonOptions.Default)`
- [ ] 6.2 Add `FromJson(string json)` static method on `TraversalPlan` — deserialize + throw DomainValidationException on null result. Add `using System.Text.Json` and `using UniClaw.Core.Domain` if not already present.

## 7. Round-Trip Tests

- [ ] 7.1 Create `tests/UniClaw.Core.Tests/Graph/TraversalPlanSerializationTests.cs` — test file with helper method `AssertRoundTrip<T>(T original)` that serializes then deserializes and checks `Assert.Equal(original, deserialized)`
- [ ] 7.2 Add TraversalPlan round-trip tests: full plan (all 12 fields populated), minimal plan (EntryApp + EntryPolicy only), plan with Meta containing mixed types (string/long/bool/null/JsonElement), plan with StaticNodes containing 2+ nodes, plan with RootNode containing nested TraversalNode
- [ ] 7.3 Add sub-type round-trip tests: EntryPolicy, EntryConfig, CompletionPolicy (all 4 types), IntentSlots, TraversalNode, Operation, Target, RestoreAction, ChildrenStrategy, DynamicRule, MatchCondition, ErrorPolicy, Precondition — each independently
- [ ] 7.4 Add fail-fast validation tests: deserialize with empty EntryApp → DomainValidationException, deserialize with malformed RootNode → DomainValidationException, deserialize CompletionPolicy TargetFound without TargetName → DomainValidationException, deserialize EntryPolicy TimeoutSeconds=0 → DomainValidationException, deserialize TraversalNode empty NodeId → DomainValidationException
- [ ] 7.5 Add null/missing fields test: JSON with only required fields, all optional fields omitted → verify defaults populated correctly
- [ ] 7.6 Add extra fields tolerance test: JSON with unknown `"futureField"` → deserialization succeeds, extra field silently ignored
- [ ] 7.7 Add StaticNodes key preservation test: keys like `"network_menu"` preserved (not camelCased to `"networkMenu"`)
- [ ] 7.8 Add computed properties test: serialized TraversalNode JSON does NOT contain `"isContainer"`/`"isLeaf"`/`"staticChildren"`; input JSON containing these keys is tolerated during deserialization

## 8. Integration Verification

- [ ] 8.1 Run `dotnet build src/UniClaw.Core.sln` — zero errors, zero functional warnings
- [ ] 8.2 Run `dotnet test src/UniClaw.Core.sln` — all existing tests + ~25 new tests pass (0 failures)
- [ ] 8.3 Verify ArchitectureGuardTests pass — enum value guards, dependency direction guards unchanged
- [ ] 8.4 Verify Domain.Vision + Domain.Content serialization tests still pass (no regression from converter registration)
