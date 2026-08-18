# Spec: settings-navigation-candidate-evidence

## ADDED Requirements

### Requirement: Structured Android UI evidence reaches Observation

The Runtime SHALL expose a narrow, deterministic structured Android UI evidence source for accepted observed elements in Settings-scoped contexts.

The evidence SHALL contain raw structured facts only, not semantic navigation claims. It SHALL NOT require destination page identity.

#### Scenario: SNE-5 navigation candidate with unknown destination

- **WHEN** an accepted Settings element is deterministically identified as a navigation candidate but its destination semantic page is unknown
- **THEN** the element is representable as `NAVIGATION_CANDIDATE` with destination unknown

#### Scenario: SNE-8 new navigation row after Scroll

- **WHEN** a new Settings navigation row appears in an accepted post-Scroll viewport
- **THEN** the structured evidence independently makes that candidate discoverable without caller inventory supplying it

### Requirement: Runtime InteractionAffordanceEvidence

The Runtime SHALL add an immutable `InteractionAffordanceEvidence` value with at least three classifications:

- `NAVIGATION_CANDIDATE`
- `LOCAL_CONTROL`
- `UNKNOWN`

This value SHALL NOT represent authorization, destination identity, branch completion, or Goal completion.

#### Scenario: SNE-1 normal Settings navigation Preference row

- **WHEN** structured Android evidence identifies a Settings Preference row as navigation-capable
- **THEN** the Runtime classifies it as `NAVIGATION_CANDIDATE`

#### Scenario: SNE-2 SwitchPreference

- **WHEN** structured Android evidence identifies a SwitchPreference or switch-like local control
- **THEN** the Runtime classifies it as `LOCAL_CONTROL` or `UNKNOWN`, and never fabricates `NAVIGATION_CANDIDATE`

#### Scenario: SNE-3 standalone switch/toggle

- **WHEN** a standalone switch/toggle element is observed
- **THEN** the Runtime classifies it as `LOCAL_CONTROL`

#### Scenario: SNE-4 button/local command

- **WHEN** a button or local command is observed and structured evidence does not prove navigation
- **THEN** the Runtime classifies it as `LOCAL_CONTROL` or `UNKNOWN`, not `NAVIGATION_CANDIDATE`

### Requirement: Clickability and menu_item are not navigation proof

The Runtime SHALL NOT treat `clickable == true`, `PerceptionType == menu_item`, or text-row presence as sufficient proof of navigation.

The classification SHALL require structured evidence that supports navigation affordance or SHALL return `UNKNOWN`.

#### Scenario: SNE-10 ambiguous structured evidence

- **WHEN** structured evidence is ambiguous or insufficient
- **THEN** the Runtime returns `UNKNOWN` rather than forcing a navigation classification

#### Scenario: SNE-14 candidate produces local effect

- **WHEN** an authorized candidate is acted on and the fresh Observation shows a local effect rather than a page transition
- **THEN** the fresh effect does not retroactively fabricate pre-action `NAVIGATION_CANDIDATE` evidence

### Requirement: Deterministic correlation between structured evidence and observed elements

Structured Android UI evidence SHALL be correlated to existing `ObservedElement` instances using deterministic keys such as bounds, stable index, resource-id, or structural node identity.

If correlation is ambiguous, the element classification SHALL be `UNKNOWN`.

#### Scenario: SNE-7 same navigation source visible in overlapping viewports

- **WHEN** the same navigation source appears in overlapping accepted viewports
- **THEN** it is deterministically correlatable and not double-counted as a new source

#### Scenario: SNE-9 popup/dialog button

- **WHEN** a popup/dialog button is observed
- **THEN** it is not classified as a normal Settings child navigation candidate

### Requirement: Settings-scoped separation of navigation and local controls

The mechanism SHALL be Settings-scoped and SHALL distinguish navigation candidates from local controls in a mixed viewport.

#### Scenario: SNE-6 mixed viewport with three navigation rows and two local controls

- **WHEN** accepted Settings evidence contains three navigation rows and two local controls
- **THEN** the Runtime independently separates the three navigation candidates from the two local controls

### Requirement: Caller independence and future falsifiability

The prerequisite evidence SHALL be sufficient to later falsify caller omission and fabrication without using caller inventory as discovery evidence.

#### Scenario: SNE-11 caller omits Runtime-visible candidate

- **WHEN** accepted Settings evidence independently yields navigation candidates A, B, C and a future caller inventory proposes only A, B
- **THEN** the prerequisite evidence is sufficient for the future completeness validator to detect C as omitted

#### Scenario: SNE-12 caller invents absent candidate

- **WHEN** accepted Settings evidence independently yields navigation candidates A, B and a future caller inventory proposes A, B, C
- **THEN** the prerequisite evidence is sufficient for the future completeness validator to reject C as lacking accepted source evidence

### Requirement: Post-action verification does not rewrite pre-action evidence

Fresh post-action Observation and page reconciliation may resolve a destination after authorized traversal, but SHALL NOT rewrite the original raw pre-action evidence.

#### Scenario: SNE-13 authorized candidate navigates

- **WHEN** an authorized candidate navigates and fresh Observation resolves the destination semantic page
- **THEN** destination may become known after the action while the pre-action source evidence remains unchanged

### Requirement: Real Settings evidence source required

Implementation SHALL prove at least one real Settings evidence source before graduation.

Synthetic-only evidence is forbidden.

The real source SHALL contain a mix of navigation Preference rows and SwitchPreference/local controls.

#### Scenario: SNE-15 real Settings evidence baseline

- **WHEN** real emulator/device Settings evidence is collected
- **THEN** the structured source is proven available and correlated with current visual/Observation elements
