## Purpose

Define the structured, contract-driven expected-outcome schema (`ExpectedBehavior`) that simulation baseline tests verify `TraversalResult` against. Grounds completeness verification in the deterministic model definition (fixture chrome ∪ enumerated scroll universe) and proves coverage via precise set-diff (`element_coverage:completeness` exact/subset modes), rather than implicit ratio thresholds. Schema changes follow the C-11 constitution change flow.
## Requirements
### Requirement: ExpectedBehavior is a sealed record class defining structured expected traversal outcome schema
ExpectedBehavior SHALL be a `sealed record class` serving as the schema contract for expected traversal results. It SHALL contain: `Scenario` (string, required), `Description` (string, required), `Completion` (CompletionExpectation, required), `PageCoverage` (PageCoverageExpectation, required), `ElementCoverage` (ElementCoverageExpectation, required), `CollisionProof` (ImmutableArray<CollisionProof>, required — may contain single `"auto_derive"` sentinel in JSON which expands to array after derivation), `DfsProperties` (DfsPropertiesExpectation, required), `NumericAnchor` (NumericAnchor, required), `OperationRules` (OperationRulesExpectation?, optional — defaults to null), `TraceIntegrity` (TraceIntegrityExpectation?, optional — defaults to null). ExpectedBehavior record structure changes SHALL follow C-11 constitution change flow (schema locked at same level as enum values).

#### Scenario: ExpectedBehavior constructed from JSON file
- **WHEN** `ExpectedBehavior.FromJson(path)` is called with a valid JSON file path
- **THEN** ExpectedBehavior record is deserialized with all 7 sub-records populated from JSON fields, using DomainJsonOptions serialization conventions (camelCase, enum-as-string)

#### Scenario: ExpectedBehavior serialization round-trip
- **WHEN** an ExpectedBehavior record is serialized to JSON and then deserialized back
- **THEN** all fields SHALL match the original record exactly (round-trip fidelity)

### Requirement: CompletionExpectation defines expected traversal completion state
CompletionExpectation SHALL be a sealed record class with fields: `Success` (bool, required), `Reason` (string, required — values: "all_visited", "target_found", "anti_loop", "max_steps", "timeout", "error", "cancelled"), `FinalState` (string?, optional — FSM terminal state name). Verification SHALL compare TraversalResult.Success, CompletionReason, and FinalState (if present) against expected values.

#### Scenario: Completion verification passes for all_visited
- **WHEN** ExpectedBehavior.Completion = { Success=true, Reason="all_visited" } and TraversalResult.CompletionReason is "all_visited" with Success=true
- **THEN** VerificationReport includes RuleResult with RuleId="completion", Passed=true

#### Scenario: Completion verification fails for wrong reason
- **WHEN** ExpectedBehavior.Completion = { Success=true, Reason="all_visited" } but TraversalResult.CompletionReason is "target_found"
- **THEN** VerificationReport includes RuleResult with RuleId="completion", Passed=false, Message="Expected reason 'all_visited', got 'target_found'"

### Requirement: PageCoverageExpectation defines expected page visit coverage
PageCoverageExpectation SHALL be a sealed record class with fields: `Required` (ImmutableArray<string>, required — page names or "auto_derive" sentinel), `Forbidden` (ImmutableArray<string>, required — page names expected NOT to be visited). Verification SHALL check that every Required page name appears in TraversalResult.VisitedPages (using Contains semantics) and that no Forbidden page name appears. When Required contains the sentinel value "auto_derive", it SHALL be replaced by fixture-derived page names via WithFixtureDerivation().

#### Scenario: Page coverage auto_derive expands from fixture
- **WHEN** PageCoverageExpectation.Required = ["auto_derive"] and WithFixtureDerivation(fixture) is called
- **THEN** Required SHALL be replaced with fixture.Pages.Keys (excluding initialPage), containing all page IDs like "home", "wifi", "bluetooth", etc.

#### Scenario: Page coverage required pages all visited
- **WHEN** Required = ["Wi-Fi", "Bluetooth", "Display"] and TraversalResult.VisitedPages contains pages matching all three names
- **THEN** VerificationReport includes RuleResult with RuleId="page_coverage", Passed=true, Message listing each visited page

#### Scenario: Page coverage forbidden pages not visited
- **WHEN** Forbidden = ["Storage", "Internal Storage", "SD Card"] and TraversalResult.VisitedPages contains no pages matching any forbidden name
- **THEN** VerificationReport includes RuleResult with RuleId="page_coverage", Passed=true for forbidden check

#### Scenario: Page coverage forbidden page violation
- **WHEN** Forbidden = ["Storage"] and TraversalResult.VisitedPages contains a page matching "Storage"
- **THEN** VerificationReport includes RuleResult with RuleId="page_coverage", Passed=false, Message="Forbidden page 'Storage' was visited"

### Requirement: ElementCoverageExpectation defines expected element interaction coverage
ElementCoverageExpectation SHALL be a sealed record class with fields: `Required` (ImmutableArray<string>, required — element IDs or "auto_derive" sentinel), `Mode` (ElementCoverageMode, required — enum values: `exact`, `subset`, `legacy_ratio`), `AllowedMisses` (ImmutableArray<ElementMiss>, optional, default empty — each ElementMiss is a sealed record class with `Id` (string) and `Reason` (string); exact-mode explicit exemptions). The legacy `RequiredRatio` field SHALL be removed.

When `Required` contains the sentinel value "auto_derive", it SHALL be replaced via `WithDerivation(StateFixture, SimulatedScreen)` (or `WithFixtureDerivation(StateFixture)` for no-scroll scenarios) with the union of: (a) all non-readonly, non-back_button element IDs from `fixture.Pages`, AND (b) every element ID enumerated from each registered scroll content source via `IScrollContentSource.GetPage(0..LastPageIndex)`. This grounds the "should-traverse" universe in the deterministic model definition, not in engine observation.

When `Mode` is absent in JSON, `WithDerivation` SHALL auto-derive it from the plan's `CompletionPolicy.Type`: `TargetFound` → `subset`; all others (including null) → `exact`. An explicit `Mode` in JSON SHALL override auto-derivation.

Verification semantics by Mode — element matching uses **exact string equality** on each action's `element_id` (not substring containment), producing a precise tapped set:
- `exact`: Compute `matched = Required ∩ tapped`, `missed = Required − tapped`, `extra = tapped − Required`. The rule SHALL pass iff `missed ⊆ AllowedMisses.Ids` AND `extra` is empty. The RuleResult SHALL enumerate `missed` and `extra` IDs precisely (not a ratio). (RuleId: `element_coverage:completeness`.)
- `subset`: No coverage assertion. Verify an over-traversal guard: locate the target element tap (the action whose `element_id` contains `CompletionPolicy.TargetName`); every subsequent action SHALL be `back`, `scroll`, or exit — no new element `tap`. The rule SHALL fail if any new element tap occurs after the target tap.
- `legacy_ratio`: Transitional behavior preserving the pre-change ratio semantics (RequiredRatio threshold). SHALL be used only for not-yet-migrated JSON files and SHALL be removed once all expected-behavior JSON files migrate to `exact`/`subset`.

#### Scenario: exact mode passes when all required elements tapped and no extra
- **WHEN** Mode=exact, Required=["Network_0","Network_1","wifi_switch"], and ActionHistory taps exactly those three element IDs
- **THEN** VerificationReport includes RuleResult RuleId="element_coverage:completeness", Passed=true, with missed=[] and extra=[]

#### Scenario: exact mode fails listing missed elements precisely
- **WHEN** Mode=exact, Required=["Network_0","Network_1","Network_2","Network_3","Network_4"], and ActionHistory taps only Network_0, Network_1, Network_2
- **THEN** RuleResult Passed=false, and Message/Actual enumerates missed=["Network_3","Network_4"] (not a percentage)

#### Scenario: exact mode fails when an extra element is tapped
- **WHEN** Mode=exact, Required=["wifi_switch"], and ActionHistory taps "wifi_switch" and a phantom "ghost_btn" not in Required
- **THEN** RuleResult Passed=false, and Message/Actual enumerates extra=["ghost_btn"]

#### Scenario: exact mode passes when missed elements are within AllowedMisses
- **WHEN** Mode=exact, Required=["A","B","C","D"], ActionHistory taps only A and B, and AllowedMisses=[{Id="C",Reason="duplicate-dedup at scroll boundary"},{Id="D",Reason="popup-blocked"}]
- **THEN** RuleResult Passed=true (missed=["C","D"] ⊆ AllowedMisses.Ids), extra=[]

#### Scenario: exact mode matching uses exact equality not substring
- **WHEN** Mode=exact, Required=["Network_1"], and ActionHistory taps "Network_17" (but not "Network_1")
- **THEN** RuleResult Passed=false, missed=["Network_1"] (substring match "Network_1"⊆"Network_17" SHALL NOT count as a match)

#### Scenario: subset mode over-traversal guard passes when no new tap after target
- **WHEN** Mode=subset (TargetFound plan), CompletionPolicy.TargetName="App15", and after the action tapping "App15" the ActionHistory contains only back/scroll actions
- **THEN** RuleResult Passed=true

#### Scenario: subset mode over-traversal guard fails when a new element is tapped after target
- **WHEN** Mode=subset (TargetFound plan), CompletionPolicy.TargetName="App15", and after the "App15" tap the ActionHistory contains a tap on a different element "App22"
- **THEN** RuleResult Passed=false (over-traversal detected)

#### Scenario: auto_derive expands to fixture chrome union scroll universe
- **WHEN** Required=["auto_derive"], a SimulatedScreen has one scrollable page with PagedItemGenerator(totalCount=25, pageSize=5, fillRatio=1.0, namePrefix="Network_"), and WithDerivation(fixture, screen) is called
- **THEN** Required SHALL contain all 25 Network_0..Network_24 IDs plus fixture chrome element IDs (the dynamically-generated scroll elements SHALL be included, not omitted)

#### Scenario: exact mode rejects infinite scroll source via fail-fast
- **WHEN** a scroll content source has TotalCount=null (infinite stream) and Mode=exact is requested for that universe
- **THEN** GetScrollableUniverse SHALL throw DomainValidationException (infinite stream cannot yield a bounded exact universe; Mode must be subset)

#### Scenario: legacy_ratio transitional behavior for unmigrated JSON
- **WHEN** a JSON file has no "mode" field but has "requiredRatio" and has not been migrated
- **THEN** FromJson SHALL set Mode=legacy_ratio and Verify SHALL apply the legacy ratio-threshold semantics

#### Scenario: Mode auto-derived from CompletionPolicy.Type when absent in JSON
- **WHEN** JSON omits "mode" and the plan has CompletionPolicy.Type=TargetFound
- **THEN** WithDerivation SHALL set Mode=subset; for any other CompletionPolicy.Type (or null), Mode SHALL be exact

### Requirement: CollisionProof defines expected NodeId collision resolution verification
CollisionProof SHALL be a sealed record class with fields: `Text` (string, required — element display text, e.g. "ON"), `ExpectedDistinct` (int, required — expected number of distinct nodes sharing this text), `ParentPages` (ImmutableArray<string>?, optional — restrict check to specific pages). Verification SHALL count how many distinct VisitedPages entries contain both the Text and each ParentPage (if specified), and verify the count matches ExpectedDistinct. When the JSON field `collision_proof` value is "auto_derive", it SHALL be replaced by fixture-derived CollisionProof entries via WithFixtureDerivation().

#### Scenario: Collision proof verifies distinct nodes for same text
- **WHEN** CollisionProof = { Text="ON", ExpectedDistinct=2 } and TraversalResult.VisitedPages contains 2 distinct entries matching "ON" (Wi-Fi switch and Bluetooth switch)
- **THEN** VerificationReport includes RuleResult with RuleId="collision_proof:ON", Passed=true

#### Scenario: Collision proof detects collision bug
- **WHEN** CollisionProof = { Text="ON", ExpectedDistinct=2 } but TraversalResult.VisitedPages contains only 1 entry matching "ON" (collision bug: Bluetooth switch skipped)
- **THEN** VerificationReport includes RuleResult with RuleId="collision_proof:ON", Passed=false, Message="Expected 2 distinct nodes with text 'ON', found 1"

#### Scenario: Collision proof auto_derive expands from fixture
- **WHEN** collision_proof field in JSON is "auto_derive" and WithFixtureDerivation(fixture) is called
- **THEN** CollisionProof SHALL be replaced with entries for each text that appears on different pages in the fixture (e.g. "ON" appearing on both wifi and bluetooth pages → CollisionProof(Text="ON", ExpectedDistinct=2))

### Requirement: DfsPropertiesExpectation defines DFS traversal order properties
DfsPropertiesExpectation SHALL be a sealed record class with fields: `RootFirst` (bool, required), `ParentBeforeChild` (bool, required), `BackAfterForward` (bool, required). Verification SHALL check: RootFirst — VisitedPages[0] contains "root"; ParentBeforeChild — for each parent-child pair derived from fixture transitions, parent appears before child in VisitedPages; BackAfterForward — for each forward transition, a corresponding back transition follows in the sequence.

#### Scenario: DFS root first property verified
- **WHEN** DfsPropertiesExpectation.RootFirst = true and VisitedPages[0] contains "root"
- **THEN** VerificationReport includes RuleResult with RuleId="dfs_properties:root_first", Passed=true

#### Scenario: DFS parent before child property verified
- **WHEN** DfsPropertiesExpectation.ParentBeforeChild = true and for every transition from_page → to_page in fixture, from_page appears before to_page in VisitedPages
- **THEN** VerificationReport includes RuleResult with RuleId="dfs_properties:parent_before_child", Passed=true

#### Scenario: DFS back after forward property verified
- **WHEN** DfsPropertiesExpectation.BackAfterForward = true and each forward step into a child page has a corresponding back step before visiting the next sibling
- **THEN** VerificationReport includes RuleResult with RuleId="dfs_properties:back_after_forward", Passed=true

#### Scenario: DFS parent before child violation
- **WHEN** DfsPropertiesExpectation.ParentBeforeChild = true but a child page (e.g. "Wi-Fi") appears in VisitedPages before its parent ("root")
- **THEN** VerificationReport includes RuleResult with RuleId="dfs_properties:parent_before_child", Passed=false

### Requirement: NumericAnchor defines reference baseline values with tolerance
NumericAnchor SHALL be a sealed record class with fields: `TotalSteps` (int, required), `VisitedPagesCount` (int, required), `ActionHistoryCount` (int, required), `ElapsedSecondsMax` (double, required). Verification SHALL compare actual TraversalResult values against anchor values with ±5% tolerance for numeric counts and ≤ for elapsed time. NumericAnchor results SHALL be informational (non-blocking) — RuleResult.Passed reflects tolerance compliance but AllPassed does not require numeric_anchor to pass. NumericAnchor SHALL NOT be considered a completeness proof; the authoritative completeness verification is `ElementCoverageExpectation` with Mode=exact. NumericAnchor serves only as an informational smoke check on aggregate counts.

#### Scenario: Numeric anchor within tolerance
- **WHEN** NumericAnchor.TotalSteps = 145 and actual TotalSteps = 143 (within ±5%)
- **THEN** VerificationReport includes RuleResult with RuleId="numeric_anchor:total_steps", Passed=true, Actual="143 (expected 145 ±5%=137.25~152.75)"

#### Scenario: Numeric anchor outside tolerance
- **WHEN** NumericAnchor.TotalSteps = 145 and actual TotalSteps = 160 (outside ±5%)
- **THEN** VerificationReport includes RuleResult with RuleId="numeric_anchor:total_steps", Passed=false, Actual="160 (expected 145 ±5%=137.25~152.75)"

#### Scenario: Numeric anchor informational status does not block and is not a completeness proof
- **WHEN** numeric_anchor RuleResult has Passed=false (values outside tolerance) but ElementCoverageExpectation Mode=exact passes
- **THEN** AllPassed=true (numeric_anchor excluded), and the completeness verdict SHALL be determined by ElementCoverageExpectation, not by numeric_anchor

### Requirement: VerificationReport summarizes all rule verification results
VerificationReport SHALL be a sealed record class with fields: `AllPassed` (bool, required — true only when all non-informational rules pass), `Summary` (string, required — human-readable summary of pass/fail), `Details` (ImmutableArray<RuleResult>, required). AllPassed SHALL exclude numeric_anchor from the pass requirement (numeric_anchor is informational). Summary SHALL list each RuleResult in order with PASS/FAIL status and actual values for failures.

#### Scenario: All rules pass
- **WHEN** all non-informational RuleResults have Passed=true
- **THEN** AllPassed=true, Summary includes all 7 blocking dimensions as PASS and numeric_anchor as INFO

#### Scenario: Some rules fail
- **WHEN** completion RuleResult has Passed=true but collision_proof RuleResult has Passed=false
- **THEN** AllPassed=false, Summary includes "completion: PASS" and "collision_proof:ON: FAIL — Expected 2, found 1"

#### Scenario: numeric_anchor informational status
- **WHEN** numeric_anchor RuleResult has Passed=false (values outside tolerance)
- **THEN** AllPassed still = true (if all other rules pass), numeric_anchor entry in Summary marked as "INFO" not "FAIL"

### Requirement: ExpectedBehavior.FromJson deserializes JSON expected definition files
ExpectedBehavior SHALL provide a static method `FromJson(string path)` that reads a JSON file and deserializes it into an ExpectedBehavior record using DomainJsonOptions conventions. The JSON format SHALL use camelCase keys matching record property names. The `collision_proof` field MAY be either an array of CollisionProof objects or the string sentinel `"auto_derive"`.

#### Scenario: FromJson reads valid JSON with explicit values
- **WHEN** FromJson is called with a path to a JSON file containing all explicit values (no "auto_derive")
- **THEN** ExpectedBehavior record is created with all sub-records populated from JSON values

#### Scenario: FromJson reads JSON with auto_derive sentinels
- **WHEN** FromJson is called with a path to a JSON file where page_coverage.required = "auto_derive" and collision_proof = "auto_derive"
- **THEN** ExpectedBehavior record is created with sentinel placeholders; WithFixtureDerivation() MUST be called before Verify() to expand these

### Requirement: ExpectedBehavior.WithFixtureDerivation expands auto_derive sentinels from StateFixture
ExpectedBehavior SHALL provide a method `WithFixtureDerivation(StateFixture fixture)` that returns a new ExpectedBehavior with all "auto_derive" sentinel values replaced by fixture-derived values. Derivation logic: page_coverage.required → fixture page keys (excluding initialPage); element_coverage.required → all non-readonly element IDs from fixture pages; collision_proof → entries for text values appearing on multiple pages.

#### Scenario: WithFixtureDerivation fills page coverage from fixture
- **WHEN** page_coverage.required was "auto_derive" and fixture has pages {home, wifi, bluetooth, display, storage, storage_internal, storage_external}
- **THEN** page_coverage.required becomes ["wifi", "bluetooth", "display", "storage", "storage_internal", "storage_external"] (excluding home as initialPage)

#### Scenario: WithFixtureDerivation fills collision proof from fixture
- **WHEN** collision_proof was "auto_derive" and fixture has element text "ON" appearing on both wifi and bluetooth pages
- **THEN** collision_proof becomes [{ Text="ON", ExpectedDistinct=2 }]

#### Scenario: WithFixtureDerivation preserves explicit values
- **WHEN** page_coverage.required was already ["Wi-Fi", "Bluetooth", "Display"] (explicit, not auto_derive)
- **THEN** WithFixtureDerivation does not change it — only auto_derive sentinels are replaced

### Requirement: ExpectedBehavior.Verify compares expected against actual TraversalResult
ExpectedBehavior SHALL provide a method `Verify(TraversalResult result)` that returns a VerificationReport. Verify SHALL run all 7 verification dimensions + numeric_anchor in order: completion, page_coverage, element_coverage, collision_proof, dfs_properties, operation_rules, trace_integrity, numeric_anchor. Each dimension SHALL produce one or more RuleResult entries. Verify SHALL use semantic name matching (Contains semantics) for **page** comparisons (page_coverage required/forbidden page names, collision_proof text matching), but `element_coverage` SHALL use **exact string equality** on element IDs (precise set operations per ElementCoverageExpectation.Mode), not substring containment.

#### Scenario: Verify produces full VerificationReport
- **WHEN** Verify is called with a TraversalResult from a successful full traversal
- **THEN** VerificationReport contains RuleResults for all 8 dimensions (7 blocking + 1 informational), each with Passed/Failed status and actual values

#### Scenario: Verify on target search result
- **WHEN** Verify is called with a TraversalResult from a target search that found "Dark mode"
- **THEN** completion RuleResult = Passed (reason=target_found), page_coverage RuleResult = Passed (required pages visited, forbidden pages NOT visited)

### Requirement: ExpectedBehavior SHALL support operation_rules verification dimension

ExpectedBehavior SHALL include an `OperationRules` field of type `OperationRulesExpectation` (sealed record class with `DepthFirstOrder: bool = false` and `NoDuplicateActionsMax: int = 0`). When both fields are at their default values (false/0), the `VerifyOperationRules` method SHALL produce no `RuleResult`. This dimension validates that the traversal engine's action sequence is operationally sound: DFS stack discipline is maintained and no element is repeatedly clicked in a dead loop.

#### Scenario: depth_first_order passes when DFS stack discipline is correct
- **WHEN** `OperationRules.DepthFirstOrder` is true
- **AND** traversal of a hub→listA→(back)→listB→(back) action sequence exhibits correct stack discipline (tap pushes +1, back pops -1, depth never negative, at least one back action exists)
- **THEN** rule `operation_rules:depth_first_order` SHALL pass

#### Scenario: depth_first_order fails when engine never backs (single-branch traversal)
- **WHEN** `OperationRules.DepthFirstOrder` is true
- **AND** the action history contains forward (tap) actions but zero back actions (engine terminates without returning from any branch)
- **THEN** rule `operation_rules:depth_first_order` SHALL fail

#### Scenario: depth_first_order fails on stack underflow (back before forward)
- **WHEN** `OperationRules.DepthFirstOrder` is true
- **AND** the action history contains a back action that occurs before any forward action (stack depth would go negative)
- **THEN** rule `operation_rules:depth_first_order` SHALL fail

#### Scenario: no_duplicate_actions passes when no element exceeds consecutive repeat limit
- **WHEN** `OperationRules.NoDuplicateActionsMax` is 3
- **AND** no element_id appears more than 3 times consecutively in the action history
- **THEN** rule `operation_rules:no_duplicate_actions` SHALL pass

#### Scenario: no_duplicate_actions fails when an element is clicked in a dead loop
- **WHEN** `OperationRules.NoDuplicateActionsMax` is 3
- **AND** element_id "button_x" appears 5 times consecutively in the action history
- **THEN** rule `operation_rules:no_duplicate_actions` SHALL fail with a message identifying the element and its consecutive count

#### Scenario: OperationRules with default values produces no RuleResult
- **WHEN** `OperationRules` is `OperationRulesExpectation()` (DepthFirstOrder=false, NoDuplicateActionsMax=0)
- **THEN** `VerifyOperationRules` SHALL return an empty list (no RuleResult produced)

### Requirement: ExpectedBehavior SHALL support trace_integrity verification dimension

ExpectedBehavior SHALL include a `TraceIntegrity` field of type `TraceIntegrityExpectation` (sealed record class with `RequiredSpanTypes: ImmutableArray<SpanType> = default` and `MinPageTransitions: int = 0`). When both fields are at their default values (empty/0), the `VerifyTraceIntegrity` method SHALL produce no `RuleResult`. This dimension validates that the trace data captured during traversal is complete: expected span types are recorded and page transitions are properly tracked.

#### Scenario: span_types_present passes when required span types exist in trace
- **WHEN** `TraceIntegrity.RequiredSpanTypes` contains `[StateDecision, PageAnalysis, DfsForward, AICall, ErrorHandling]`
- **AND** every specified SpanType appears in at least one `TraceRecord.SpanTypes` array
- **THEN** each SpanType SHALL produce a passing `RuleResult` with rule ID `trace_integrity:span_type:<SpanTypeName>`

#### Scenario: span_types_present fails when a required span type is missing
- **WHEN** `TraceIntegrity.RequiredSpanTypes` contains `[ErrorHandling]`
- **AND** no `TraceRecord` in the trace emits `SpanType.ErrorHandling` (error-free traversal)
- **THEN** rule `trace_integrity:span_type:ErrorHandling` SHALL fail

#### Scenario: page_transitions passes when sufficient transitions are recorded
- **WHEN** `TraceIntegrity.MinPageTransitions` is 10
- **AND** the trace contains at least 10 records where `PageTransitionType` is not null
- **THEN** rule `trace_integrity:page_transitions` SHALL pass

#### Scenario: page_transitions fails when too few transitions are recorded
- **WHEN** `TraceIntegrity.MinPageTransitions` is 10
- **AND** the trace contains only 3 records with non-null `PageTransitionType`
- **THEN** rule `trace_integrity:page_transitions` SHALL fail with actual count in message

#### Scenario: TraceIntegrity with default values produces no RuleResult
- **WHEN** `TraceIntegrity` is `TraceIntegrityExpectation()` (RequiredSpanTypes=empty, MinPageTransitions=0)
- **THEN** `VerifyTraceIntegrity` SHALL return an empty list (no RuleResult produced)

### Requirement: ExpectedBehavior JSON schema SHALL be backward-compatible with new optional keys

The `expected-behavior` JSON schema SHALL accept optional `operationRules` and `traceIntegrity` objects at the top level alongside existing keys (`scenario`, `description`, `completion`, `pageCoverage`, `elementCoverage`, `collisionProof`, `dfsProperties`, `numericAnchor`). When either key is absent from a JSON file, the `FromJson` method SHALL construct the corresponding Expectation with default values (all false/0/empty), ensuring existing baseline JSON files produce identical `VerificationReport` results without modification.

#### Scenario: Existing JSON without new keys deserializes correctly
- **WHEN** a JSON file contains only the original keys (scenario through numericAnchor) without `operationRules` or `traceIntegrity`
- **THEN** `ExpectedBehavior.FromJson` SHALL deserialize it with `OperationRules = OperationRulesExpectation()` and `TraceIntegrity = TraceIntegrityExpectation()`
- **AND** `Verify()` produces the same `AllPassed` result as before the schema extension

#### Scenario: JSON with new keys enables the new verification rules
- **WHEN** a JSON file contains `"operationRules": { "depthFirstOrder": true, "noDuplicateActionsMax": 3 }`
- **THEN** `VerifyOperationRules` SHALL produce RuleResult entries for both rules

### Requirement: ExpectedBehavior.WithDerivation expands auto_derive sentinels from StateFixture union scroll universe
ExpectedBehavior SHALL provide a method `WithDerivation(StateFixture fixture, SimulatedScreen screen)` that returns a new ExpectedBehavior with all "auto_derive" sentinel values replaced by the union of fixture-derived values AND scroll-universe values. Derivation logic: page_coverage.required → fixture page keys (excluding initialPage); element_coverage.required → all non-readonly, non-back_button element IDs from fixture pages UNION every element ID enumerated from each scroll content source registered on `screen` via `IScrollContentSource.GetPage(0..LastPageIndex)`; collision_proof → entries for text values appearing on multiple pages. When `element_coverage.mode` is absent in JSON, `WithDerivation` SHALL auto-derive it from the plan's `CompletionPolicy.Type` (`TargetFound` → `subset`; otherwise → `exact`); an explicit JSON `mode` SHALL override auto-derivation. The existing `WithFixtureDerivation(StateFixture)` SHALL remain available for no-scroll scenarios and transitional coexistence, deriving element_coverage.required from fixture chrome only.

#### Scenario: WithDerivation fills element coverage from fixture union scroll universe
- **WHEN** element_coverage.required was "auto_derive", fixture has chrome elements, and screen has one scrollable page with PagedItemGenerator(totalCount=25, pageSize=5, namePrefix="Network_")
- **THEN** element_coverage.required becomes the union of fixture chrome element IDs AND all 25 Network_0..Network_24 IDs

#### Scenario: WithDerivation auto-derives Mode from CompletionPolicy when JSON omits mode
- **WHEN** element_coverage JSON omits "mode" and the plan has CompletionPolicy.Type=TargetFound
- **THEN** element_coverage.Mode becomes `subset`; for any other CompletionPolicy.Type it becomes `exact`

#### Scenario: WithDerivation preserves explicit Required and Mode values
- **WHEN** element_coverage.required was already explicit (not "auto_derive") and JSON had an explicit "mode"
- **THEN** WithDerivation does not change Required and keeps the explicit Mode — only auto_derive sentinels are replaced and only absent Mode is auto-derived

#### Scenario: WithDerivation fail-fasts on infinite scroll source
- **WHEN** a registered scroll content source has TotalCount=null and WithDerivation enumerates the scroll universe
- **THEN** GetScrollableUniverse SHALL throw DomainValidationException (infinite stream cannot yield a bounded universe)

