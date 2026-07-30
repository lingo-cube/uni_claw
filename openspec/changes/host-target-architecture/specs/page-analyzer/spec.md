## ADDED Requirements

### Requirement: PageAnalysis shape contract is test-enforced across observation paths

A Core-defined shape contract (enforced by tests, not prose) SHALL govern both the AI observation path and the UIAutomator observation path. Both paths SHALL fill the contracted fields `Level1Menus` / `Level2Menus` / `Items` / `CurrentPath` / `HasScroll` / `IsEndOfList` to a common rule, so the same `PageAnalysis` record shape is produced regardless of which path observed it. A contract test SHALL run both the AI observation path and the UIAutomator observation path over the same fixture and assert structural equivalence on the fields the runner and safety gate consume. "Mock green" SHALL imply "real-path-shape green": if the contract test passes for the mock/AI path on a fixture, the contract test SHALL also pass for the real/UIAutomator path on that same fixture.

#### Scenario: Both paths fill the contracted fields

- **WHEN** the AI observation path and the UIAutomator observation path each produce a `PageAnalysis` for the same page fixture
- **THEN** both `PageAnalysis` instances populate `Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, and `IsEndOfList`, and neither leaves any contracted field at its default/empty value when the page has that content

#### Scenario: Contract test passes for both paths on the same fixture

- **WHEN** the Core-defined contract test runs both the AI observation path and the UIAutomator observation path over the same fixture
- **THEN** the contract test asserts structural equivalence on the fields the runner and safety gate consume (`Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, `IsEndOfList`) and passes for both paths

#### Scenario: Mock-path shape equivalence implies real-path shape equivalence

- **WHEN** the mock/AI observation path satisfies the shape contract on a given fixture (mock green)
- **THEN** the real/UIAutomator observation path also satisfies the shape contract on that same fixture, so "mock green" implies "real-path-shape green" for the contracted fields

### Requirement: UIAutomator observation path fills Level1Menus and Level2Menus

The UIAutomator observation path SHALL populate `Level1Menus` and `Level2Menus` on the produced `PageAnalysis`, matching the AI observation path. The UIAutomator path SHALL NOT leave `Level1Menus` or `Level2Menus` empty when the page under observation has level-1 or level-2 menus.

#### Scenario: UIAutomator path produces non-empty Level1Menus and Level2Menus where the page has them

- **WHEN** the UIAutomator observation path observes a page that has level-1 and level-2 menus
- **THEN** the resulting `PageAnalysis` has non-empty `Level1Menus` and non-empty `Level2Menus`, matching the shape produced by the AI observation path for the same page

### Requirement: UIAutomator observation path derives Direction from layout

The UIAutomator observation path SHALL derive `Direction` from layout instead of hardcoding `Direction.Left`. The UIAutomator path SHALL NOT assign `Direction.Left` by default without consulting the layout.

#### Scenario: UIAutomator path sets Direction from layout instead of a hardcoded Left

- **WHEN** the UIAutomator observation path observes a page and computes `Direction`
- **THEN** the `Direction` value is derived from the observed layout, not assigned as a hardcoded `Direction.Left` independent of layout

#### Scenario: Left-layout page yields Direction.Left via derivation

- **WHEN** the UIAutomator observation path observes a page whose layout indicates a left direction
- **THEN** the resulting `PageAnalysis.Direction` equals `Direction.Left` as the product of layout derivation, not as a hardcoded default

#### Scenario: Different layout yields the derived Direction value

- **WHEN** the UIAutomator observation path observes a page whose layout indicates a direction other than left
- **THEN** the resulting `PageAnalysis.Direction` equals the value derived from that layout rather than `Direction.Left`

### Requirement: PageAnalysis shape contract guards the runner and safety gate consumers

The runner and the safety gate SHALL consume `PageAnalysis` only through the contracted fields (`Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, `IsEndOfList`). The shape contract SHALL make observation-path failure observable rather than maskable: an observation path that omits a contracted field SHALL fail the contract test rather than produce a silently partial `PageAnalysis` that the runner or safety gate consumes as if it were complete. This supports spec defect D4.

#### Scenario: A path that omits a contracted field fails the contract test

- **WHEN** an observation path produces a `PageAnalysis` that omits a contracted field the runner or safety gate consumes
- **THEN** the Core-defined contract test fails for that path, rather than allowing a silently partial `PageAnalysis` to be consumed by the runner or safety gate

#### Scenario: Runner and safety gate consume only contracted fields

- **WHEN** the runner or the safety gate reads a `PageAnalysis`
- **THEN** it reads only the contracted fields (`Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, `IsEndOfList`), so a path that fails the shape contract cannot be masked by a consumer that tolerates missing fields