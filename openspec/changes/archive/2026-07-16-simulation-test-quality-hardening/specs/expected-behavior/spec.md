## MODIFIED Requirements

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

### Requirement: ExpectedBehavior.Verify compares expected against actual TraversalResult
ExpectedBehavior SHALL provide a method `Verify(TraversalResult result)` that returns a VerificationReport. Verify SHALL run all 7 verification dimensions + numeric_anchor in order: completion, page_coverage, element_coverage, collision_proof, dfs_properties, operation_rules, trace_integrity, numeric_anchor. Each dimension SHALL produce one or more RuleResult entries. Verify SHALL use semantic name matching (Contains semantics) for **page** comparisons (page_coverage required/forbidden page names, collision_proof text matching), but `element_coverage` SHALL use **exact string equality** on element IDs (precise set operations per ElementCoverageExpectation.Mode), not substring containment.

#### Scenario: Verify produces full VerificationReport
- **WHEN** Verify is called with a TraversalResult from a successful full traversal
- **THEN** VerificationReport contains RuleResults for all 8 dimensions (7 blocking + 1 informational), each with Passed/Failed status and actual values

#### Scenario: Verify on target search result
- **WHEN** Verify is called with a TraversalResult from a target search that found "Dark mode"
- **THEN** completion RuleResult = Passed (reason=target_found), page_coverage RuleResult = Passed (required pages visited, forbidden pages NOT visited)

## ADDED Requirements

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
