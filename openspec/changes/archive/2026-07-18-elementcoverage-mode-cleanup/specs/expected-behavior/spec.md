## MODIFIED Requirements

### Requirement: ElementCoverageExpectation defines expected element interaction coverage
ElementCoverageExpectation SHALL be a sealed record class with fields: `Required` (ImmutableArray<string>, required — element IDs or "auto_derive" sentinel), `Mode` (ElementCoverageMode, required — enum values: `exact`, `subset`; defaults to `exact` when absent from JSON), `AllowedMisses` (ImmutableArray<ElementMiss>, optional, default empty — each ElementMiss is a sealed record class with `Id` (string) and `Reason` (string); exact-mode explicit exemptions). There SHALL be no `RequiredRatio` field and no `legacy_ratio` Mode value — the pre-cleanup ratio-threshold transitional path is removed (all expected-behavior JSON files migrated to `exact`/`subset`).

When `Required` contains the sentinel value "auto_derive", it SHALL be replaced via `WithDerivation(StateFixture, SimulatedScreen, CompletionPolicy?)` (or `WithFixtureDerivation(StateFixture, CompletionPolicy?)` for no-scroll scenarios) with the union of: (a) all non-readonly, non-back_button element IDs from `fixture.Pages`, AND (b) every element ID enumerated from each registered scroll content source via `IScrollContentSource.GetPage(0..LastPageIndex)`. This grounds the "should-traverse" universe in the deterministic model definition, not in engine observation.

`Mode` is an explicit JSON field; it SHALL NOT be auto-derived. When `Mode` is absent from JSON, it defaults to `exact`. `WithDerivation`/`WithFixtureDerivation` accept an optional `CompletionPolicy?` used solely to capture `TargetName` for the subset over-traversal guard — it SHALL NOT change `Mode`.

Verification semantics by Mode — element matching uses **exact string equality** on each action's `element_id` (not substring containment), producing a precise tapped set:
- `exact`: Compute `matched = Required ∩ tapped`, `missed = Required − tapped`, `extra = tapped − Required`. The rule SHALL pass iff `missed ⊆ AllowedMisses.Ids` AND `extra` is empty. The RuleResult SHALL enumerate `missed` and `extra` IDs precisely (not a ratio). (RuleId: `element_coverage:completeness`.)
- `subset`: No coverage assertion. Verify an over-traversal guard: locate the target element tap (the action whose `element_id` contains `CompletionPolicy.TargetName`); every subsequent action SHALL be `back`, `scroll`, or exit — no new element `tap`. The rule SHALL fail if any new element tap occurs after the target tap. (MarkAndStop — target reached via analysis but not tapped, completion=`target_found` — passes, as the engine halts and over-traversal is impossible.)

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

#### Scenario: subset mode MarkAndStop passes when target found but not tapped
- **WHEN** Mode=subset (TargetFound, MarkAndStop), CompletionPolicy.TargetName="Dark mode", ActionHistory never taps the target, and TraversalResult.CompletionReason="target_found"
- **THEN** RuleResult Passed=true (engine halted at find; over-traversal impossible)

#### Scenario: auto_derive expands to fixture chrome union scroll universe
- **WHEN** Required=["auto_derive"], a SimulatedScreen has one scrollable page with PagedItemGenerator(totalCount=25, pageSize=5, fillRatio=1.0, namePrefix="Network_"), and WithDerivation(fixture, screen) is called
- **THEN** Required SHALL contain all 25 Network_0..Network_24 IDs plus fixture chrome element IDs (the dynamically-generated scroll elements SHALL be included, not omitted)

#### Scenario: exact mode rejects infinite scroll source via fail-fast
- **WHEN** a scroll content source has TotalCount=null (infinite stream) and Mode=exact is requested for that universe
- **THEN** GetScrollableUniverse SHALL throw DomainValidationException (infinite stream cannot yield a bounded exact universe; Mode must be subset)

#### Scenario: absent Mode defaults to exact
- **WHEN** a JSON file omits the "mode" field
- **THEN** FromJson SHALL set Mode=exact (no ratio fallback, no CompletionPolicy-based derivation)

### Requirement: ExpectedBehavior.WithDerivation expands auto_derive sentinels from StateFixture union scroll universe
ExpectedBehavior SHALL provide a method `WithDerivation(StateFixture fixture, SimulatedScreen screen, CompletionPolicy? completionPolicy = null)` that returns a new ExpectedBehavior with all "auto_derive" sentinel values replaced by the union of fixture-derived values AND scroll-universe values. Derivation logic: page_coverage.required → fixture page keys (excluding initialPage); element_coverage.required → all non-readonly, non-back_button element IDs from fixture pages UNION every element ID enumerated from each scroll content source registered on `screen` via `IScrollContentSource.GetPage(0..LastPageIndex)`; collision_proof → entries for text values appearing on multiple pages. `Mode` SHALL NOT be auto-derived — it is taken verbatim from JSON (defaulting to `exact` if absent). The optional `completionPolicy` SHALL be used solely to capture `TargetName` for subset's over-traversal guard. The existing `WithFixtureDerivation(StateFixture, CompletionPolicy?)` SHALL remain available for no-scroll scenarios, deriving element_coverage.required from fixture chrome only.

#### Scenario: WithDerivation fills element coverage from fixture union scroll universe
- **WHEN** element_coverage.required was "auto_derive", fixture has chrome elements, and screen has one scrollable page with PagedItemGenerator(totalCount=25, pageSize=5, namePrefix="Network_")
- **THEN** element_coverage.required becomes the union of fixture chrome element IDs AND all 25 Network_0..Network_24 IDs

#### Scenario: WithDerivation preserves explicit Required and Mode values
- **WHEN** element_coverage.required was already explicit (not "auto_derive") and JSON had an explicit "mode"
- **THEN** WithDerivation does not change Required or Mode — only auto_derive sentinels are replaced (and TargetName captured for subset)

#### Scenario: WithDerivation fail-fasts on infinite scroll source
- **WHEN** a registered scroll content source has TotalCount=null and WithDerivation enumerates the scroll universe
- **THEN** GetScrollableUniverse SHALL throw DomainValidationException (infinite stream cannot yield a bounded universe)
