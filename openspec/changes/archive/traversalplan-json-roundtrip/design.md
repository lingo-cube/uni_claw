## Context

TraversalPlan has 12 fields and depends on ~10 nested record types (TraversalNode, EntryPolicy, CompletionPolicy, IntentSlots, etc.) each with custom constructors that throw DomainValidationException on invalid input. DomainJsonOptions.Default produces camelCase JSON with JsonStringEnumConverter, but:

1. STJ deserialization requires constructor parameter names matching JSON keys — no `[JsonPropertyName]` annotations exist on Graph.Models types
2. `Dictionary<string, object>` Meta field cannot round-trip — STJ deserializes `object` as `JsonElement`
3. DomainJsonOptions doc states: "仅保证对象→JSON 单向可输出，不保证 JSON→对象往返"

Full design spec at: `docs/refactor/2026-07-20-traversalplan-json-roundtrip-design.md`

## Goals / Non-Goals

**Goals:**
- TraversalPlan full JSON round-trip (serialize → deserialize → Assert.Equal) via DomainJsonOptions.Default
- Reusable `ObjectDictionaryConverter` for `Dictionary<string, object>` round-trip (C-7 and future can reuse)
- TypeHint.Values type alignment with other Domain enums (`IReadOnlyList<string>`)
- Fail-fast validation preserved during deserialization (DomainValidationException)

**Non-Goals:**
- Python JSON interop (snake_case ↔ camelCase bridge)
- TraversalResult or other Graph type serialization
- File I/O operations (Core classlib has no filesystem dependency)
- Separate PlanValidator service

## Decisions

1. **C# self-roundtrip only** — no snake_case compatibility needed. camelCase is consistent within C# ecosystem. Python interop is a future change if needed.

2. **ObjectDictionaryConverter in Domain/CrossCutting** — `Dictionary<string, object>` is not a Graph-specific problem. Placing it alongside DomainJsonOptions enables reuse by C-7 (Trace JSONL) and any future type with `object` fields. Alternative: place in Graph layer only — rejected because it's a general STJ problem.

3. **[JsonPropertyName] per-property annotation** — annotate each public property with its camelCase key name. Alternative: DTO intermediate layer + FromJson factory method — rejected because it doubles type count. Alternative: JsonConstructor attribute — rejected because constructor parameter names may not match camelCase after transformation. Annotation is the least-invasive approach that preserves validation constructors.

4. **TypeHint.Values type merged into this change** — P3-4 was the only remaining P3 item (other 4 already done). Merging avoids a separate micro-change for a 5-line fix.

5. **Convenience methods (ToJson/FromJson) optional** — not required for round-trip; upper layer can call JsonSerializer directly. Decided to include as they reduce boilerplate and establish a clear API pattern.

## Risks / Trade-offs

- **Annotation volume**: ~50+ `[JsonPropertyName]` attributes across ~10 types. Tedious but mechanical. No runtime cost — annotations are metadata only. → Risk mitigated: each annotation is a simple string matching the property name in camelCase.

- **Dictionary<string, object> inference**: ObjectDictionaryConverter infers CLR types from JsonElement (string/int/bool/null → native types; array/object → JsonElement). Nested structures preserved but not fully round-tripped. → Acceptable: Meta field is arbitrary metadata, not domain data. Full round-trip of nested structures would require recursive type mapping — YAGNI.

- **Constructor validation on deserialization**: STJ calls parameterized constructors during deserialization. If JSON contains invalid data (empty EntryApp, wrong EntryStrategy), DomainValidationException fires. This is correct fail-fast behavior but may surprise callers expecting graceful error handling. → Acceptable: fail-fast is a project-wide design principle (D-83).

- **Existing tests unchanged**: 721 tests remain untouched. New tests only add. → Risk: none.
