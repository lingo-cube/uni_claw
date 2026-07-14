## 1. Domain types — new Expectation records

- [x] 1.1 Create `OperationRulesExpectation.cs`: sealed record class with `DepthFirstOrder: bool = false` and `NoDuplicateActionsMax: int = 0`; xmldoc describing each field's rule semantics
- [x] 1.2 Create `TraceIntegrityExpectation.cs`: sealed record class with `RequiredSpanTypes: ImmutableArray<SpanType> = default` and `MinPageTransitions: int = 0`; xmldoc describing each field's rule semantics

## 2. ExpectedBehavior record extension

- [x] 2.1 Add `OperationRulesExpectation OperationRules` and `TraceIntegrityExpectation TraceIntegrity` as positional parameters 9 and 10 to the main `ExpectedBehavior` sealed partial record class (after `NumericAnchor`); both with `= new()` default for backward compatibility
- [x] 2.2 Add `OperationRulesExpectationDto` and `TraceIntegrityExpectationDto` internal sealed classes to the DTO region; wire into `FromJson` (null DTO → default Expectation; non-null → construct with field values)
- [x] 2.3 `FromJson`: handle `Enum.Parse<SpanType>` for `TraceIntegrityExpectationDto.RequiredSpanTypes` with try-catch safe default (malformed SpanType name → log warning, skip)

## 3. Verification methods

- [x] 3.1 Implement `ExpectedBehavior.VerifyOperationRules(TraversalResult)`: List<RuleResult>
  - `depth_first_order` (RuleId `"operation_rules:depth_first_order"`): iterate ActionHistory, tap(non-back)=push(+1), back=pop(-1); FAIL if depth < 0 at any point OR never back OR no forward; PASS if depth ≥ 0 throughout AND ≥ 1 back
  - `no_duplicate_actions` (RuleId `"operation_rules:no_duplicate_actions"`): scan consecutive groups by `Parameters["element_id"]`; FAIL if any group length > NoDuplicateActionsMax; include element_id and count in message
  - Both guarded by field defaults (false / 0 → skip, return empty)
- [x] 3.2 Implement `ExpectedBehavior.VerifyTraceIntegrity(TraversalResult)`: List<RuleResult>
  - `span_types_present` (RuleId `"trace_integrity:span_type:<SpanTypeName>"`): for each SpanType in RequiredSpanTypes, collect union of all SpanTypes across result.Trace; PASS if type is present in union; one RuleResult per type
  - `page_transitions` (RuleId `"trace_integrity:page_transitions"`): count result.Trace records where PageTransitionType != null; PASS if count ≥ MinPageTransitions
  - Both guarded by field defaults (empty array / 0 → skip, return empty)
- [x] 3.3 Wire both methods into main `Verify()` dispatch: `details.AddRange(VerifyOperationRules(result))` and `details.AddRange(VerifyTraceIntegrity(result))` between VerifyDfsProperties and VerifyNumericAnchor sections

## 4. Engine instrumentation — PageFrom/PageTo tracking

- [x] 4.1 In `TraversalEngine.RunAsync()`: add `string? _lastPageId = null` before the for loop; at TraceRecord creation, populate `PageFrom: _lastPageId`, `PageTo: _lastPageId != GetCurrentPageId() ? GetCurrentPageId() : null`, `PageTransitionType: _lastPageId != null && _lastPageId != GetCurrentPageId() ? "navigation" : null`; update `_lastPageId = GetCurrentPageId()` after RecordPageVisit

## 5. Baseline JSON fixtures

- [x] 5.1 Add optional `operationRules` and `traceIntegrity` to `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/settings-full-traversal.json`:
  - `"operationRules": { "depthFirstOrder": true, "noDuplicateActionsMax": 5 }`
  - `"traceIntegrity": { "requiredSpanTypes": ["StateDecision", "PageAnalysis", "DfsForward", "AICall", "ErrorHandling"], "minPageTransitions": 10 }`
- [x] 5.2 Add optional `operationRules` and `traceIntegrity` to `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/settings-target-search.json`:
  - `"operationRules": { "depthFirstOrder": true, "noDuplicateActionsMax": 3 }`
  - `"traceIntegrity": { "requiredSpanTypes": ["StateDecision", "PageAnalysis", "DfsForward", "AICall", "ErrorHandling"], "minPageTransitions": 5 }`
  (note: target-search 深度优先到目标即停, minPageTransitions 设 5 而非 10)

## 6. Documentation

- [x] 6.1 Update `docs/system/layers/simulation-baseline.md` §2 TODO 维度表: mark operation_rules and trace_integrity as completed with rule count and data source
- [x] 6.2 Append decision entry to `docs/system/decisions/log.md` (next D-N): ExecutionPlanDigest — Path A (no new service, static methods on existing data); operation_rules depth_first_order 与 dfs_properties:back_after_forward 正交互补关系
- [x] 6.3 Update `docs/system/decisions/log.md` D-E4 entry: mark operation_rules and trace_integrity as resolved (no longer TODO)

## 7. Validation

- [ ] 7.1 `dotnet build` clean (0 errors, 0 functional warnings)
- [ ] 7.2 `dotnet test` full suite: all existing tests green; settings-full-traversal and settings-target-search baseline tests pass with new optional rules
- [ ] 7.3 `openspec validate execution-plan-digest` (if validate command available)
