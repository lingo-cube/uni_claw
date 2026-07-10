## ADDED Requirements

### Requirement: ExpectedBehavior is a sealed record class defining structured expected traversal outcome schema
ExpectedBehavior SHALL be a `sealed record class` serving as the schema contract for expected traversal results. It SHALL contain: `Scenario` (string, required), `Description` (string, required), `Completion` (CompletionExpectation, required), `PageCoverage` (PageCoverageExpectation, required), `ElementCoverage` (ElementCoverageExpectation, required), `CollisionProof` (ImmutableArray<CollisionProof>, required — may contain single `"auto_derive"` sentinel in JSON which expands to array after derivation), `DfsProperties` (DfsPropertiesExpectation, required), `NumericAnchor` (NumericAnchor, required). ExpectedBehavior record structure changes SHALL follow C-11 constitution change flow (schema locked at same level as enum values).

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
ElementCoverageExpectation SHALL be a sealed record class with fields: `Required` (ImmutableArray<string>, required — element IDs or "auto_derive" sentinel), `RequiredRatio` (double, required, default 0.95 — coverage threshold). Verification SHALL check that the ratio of Required element IDs appearing in TraversalResult.ActionHistory meets or exceeds RequiredRatio. When Required contains the sentinel value "auto_derive", it SHALL be replaced by fixture-derived non-readonly element IDs via WithFixtureDerivation().

#### Scenario: Element coverage meets threshold
- **WHEN** RequiredRatio = 0.95 and 95%+ of Required element IDs appear in ActionHistory
- **THEN** VerificationReport includes RuleResult with RuleId="element_coverage", Passed=true, Actual="36/38 (94.7%)" or similar

#### Scenario: Element coverage below threshold
- **WHEN** RequiredRatio = 0.95 and only 80% of Required element IDs appear in ActionHistory
- **THEN** VerificationReport includes RuleResult with RuleId="element_coverage", Passed=false, Actual="30/38 (78.9%)"

#### Scenario: Element coverage auto_derive expands from fixture
- **WHEN** Required = ["auto_derive"] and WithFixtureDerivation(fixture) is called
- **THEN** Required SHALL be replaced with all non-readonly element IDs from fixture pages

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
NumericAnchor SHALL be a sealed record class with fields: `TotalSteps` (int, required), `VisitedPagesCount` (int, required), `ActionHistoryCount` (int, required), `ElapsedSecondsMax` (double, required). Verification SHALL compare actual TraversalResult values against anchor values with ±5% tolerance for numeric counts and ≤ for elapsed time. NumericAnchor results SHALL be informational (non-blocking) — RuleResult.Passed reflects tolerance compliance but AllPassed does not require numeric_anchor to pass.

#### Scenario: Numeric anchor within tolerance
- **WHEN** NumericAnchor.TotalSteps = 145 and actual TotalSteps = 143 (within ±5%)
- **THEN** VerificationReport includes RuleResult with RuleId="numeric_anchor:total_steps", Passed=true, Actual="143 (expected 145 ±5%=137.25~152.75)"

#### Scenario: Numeric anchor outside tolerance
- **WHEN** NumericAnchor.TotalSteps = 145 and actual TotalSteps = 160 (outside ±5%)
- **THEN** VerificationReport includes RuleResult with RuleId="numeric_anchor:total_steps", Passed=false, Actual="160 (expected 145 ±5%=137.25~152.75)"

### Requirement: VerificationReport summarizes all rule verification results
VerificationReport SHALL be a sealed record class with fields: `AllPassed` (bool, required — true only when all non-informational rules pass), `Summary` (string, required — human-readable summary of pass/fail), `Details` (ImmutableArray<RuleResult>, required). AllPassed SHALL exclude numeric_anchor from the pass requirement (numeric_anchor is informational). Summary SHALL list each RuleResult in order with PASS/FAIL status and actual values for failures.

#### Scenario: All rules pass
- **WHEN** all non-informational RuleResults have Passed=true
- **THEN** AllPassed=true, Summary="completion: PASS | page_coverage: PASS | element_coverage: PASS | collision_proof: PASS | dfs_properties: PASS | numeric_anchor: INFO"

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
ExpectedBehavior SHALL provide a method `Verify(TraversalResult result)` that returns a VerificationReport. Verify SHALL run all 5 verification dimensions + numeric_anchor in order: completion, page_coverage, element_coverage, collision_proof, dfs_properties, numeric_anchor. Each dimension SHALL produce one or more RuleResult entries. Verify SHALL use semantic name matching (Contains semantics) for page/element comparisons, not NodeId exact equality.

#### Scenario: Verify produces full VerificationReport
- **WHEN** Verify is called with a TraversalResult from a successful full traversal
- **THEN** VerificationReport contains RuleResults for all 6 dimensions (5 blocking + 1 informational), each with Passed/Failed status and actual values

#### Scenario: Verify on target search result
- **WHEN** Verify is called with a TraversalResult from a target search that found "Dark mode"
- **THEN** completion RuleResult = Passed (reason=target_found), page_coverage RuleResult = Passed (required pages visited, forbidden pages NOT visited)
