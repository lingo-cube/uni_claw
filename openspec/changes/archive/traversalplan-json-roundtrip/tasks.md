## 1. Infrastructure — ObjectDictionaryConverter + DomainJsonOptions

- [ ] 1.1 Create `ObjectDictionaryConverter.cs` in `Domain/CrossCutting/` — custom `JsonConverter<Dictionary<string, object>>` with Read (infer CLR type from JsonElement) and Write (by CLR type)
- [ ] 1.2 Register `ObjectDictionaryConverter` on `DomainJsonOptions.Default.Converters`
- [ ] 1.3 Write `ObjectDictionaryConverterTests.cs` — 4-5 tests covering string/int/bool/null/nested JsonElement round-trip

## 2. [JsonPropertyName] Annotations — TraversalPlan Dependency Tree

- [ ] 2.1 Add `[JsonPropertyName]` annotations to TraversalPlan (12 properties)
- [ ] 2.2 Add `[JsonPropertyName]` annotations to TraversalNode (8 properties)
- [ ] 2.3 Add `[JsonPropertyName]` annotations to EntryPolicy (4 properties)
- [ ] 2.4 Add `[JsonPropertyName]` annotations to CompletionPolicy (6 properties)
- [ ] 2.5 Add `[JsonPropertyName]` annotations to IntentSlots (9 properties)
- [ ] 2.6 Add `[JsonPropertyName]` annotations to EntryConfig (2 properties)
- [ ] 2.7 Add `[JsonPropertyName]` annotations to Operation (3 properties)
- [ ] 2.8 Add `[JsonPropertyName]` annotations to ErrorPolicy (6 properties)
- [ ] 2.9 Add `[JsonPropertyName]` annotations to Precondition, DynamicRule, MatchCondition (if they have properties needing annotation)
- [ ] 2.10 Verify all annotations produce correct camelCase keys matching constructor parameter names

## 3. Convenience Methods + TypeHint.Values Fix

- [ ] 3.1 Add `ToJson()` and `FromJson(string)` methods on TraversalPlan
- [ ] 3.2 Change `TypeHint.Values` return type from `IReadOnlyList<TypeHint>` to `IReadOnlyList<string>` — derive from `[JsonPropertyName]` reflection like other Domain enums
- [ ] 3.3 Update any callers of `TypeHint.Values` (likely minimal — check with MCP find_references)

## 4. Round-Trip Tests

- [ ] 4.1 Create `TraversalPlanSerializationTests.cs` in `tests/UniClaw.Core.Tests/Graph/`
- [ ] 4.2 Write full TraversalPlan round-trip test (Serialize → Deserialize → Assert.Equal) with all 12 fields
- [ ] 4.3 Write minimal plan round-trip test (only EntryApp + EntryPolicy)
- [ ] 4.4 Write null/missing optional fields test
- [ ] 4.5 Write sub-type round-trip tests (EntryPolicy, CompletionPolicy, IntentSlots, TraversalNode, Operation)
- [ ] 4.6 Write fail-fast validation tests (empty EntryApp, invalid EntryStrategy)
- [ ] 4.7 Write extra fields tolerance test
- [ ] 4.8 Write Meta special cases tests (string/int/bool/null/nested JsonElement in Meta field)
- [ ] 4.9 Run full test suite — 721+ existing tests must all pass

## 5. Verification + Decision Recording

- [ ] 5.1 Run `dotnet test src/UniClaw.Core.sln` — all tests green
- [ ] 5.2 Record decisions D-91 (Hook-only, already in B1 spec) and update P3 status in decisions/log.md if needed
- [ ] 5.3 Update CLAUDE.md test count if total changed
