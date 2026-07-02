## ADDED Requirements

### Requirement: BoundingBox is normalized to [0,1] with positive dimensions
The `BoundingBox` (`readonly record struct`) SHALL represent normalized coordinates in `[0,1]` and SHALL require `w > 0` and `h > 0`. It SHALL NOT define `BoundingBoxPixel` or any `ToPixel(...)` method (pixel conversion is out of Domain scope).

#### Scenario: BoundingBox accepts valid normalized rect
- **WHEN** `BoundingBox` is created with `x=0.1, y=0.2, w=0.5, h=0.3`
- **THEN** the instance stores those values and does not throw

#### Scenario: BoundingBox rejects non-positive width
- **WHEN** `BoundingBox` is created with `w=0` (or `w<0`)
- **THEN** construction throws `DomainValidationException` whose message contains the field name (`w`) and the illegal value

#### Scenario: BoundingBox rejects non-positive height
- **WHEN** `BoundingBox` is created with `h=0`
- **THEN** construction throws `DomainValidationException` naming `h`

#### Scenario: BoundingBox rejects out-of-range coordinates
- **WHEN** any of `x/y/w/h` is outside `[0,1]` (e.g. `x=1.5`)
- **THEN** construction throws `DomainValidationException` naming the offending field

#### Scenario: No pixel-space type exists
- **WHEN** the `UniClaw.Core.Domain.Models.Vision` namespace is inspected
- **THEN** there is no `BoundingBoxPixel` type and no `ToPixel` member on `BoundingBox`

### Requirement: TypeHint has no Unknown and falls back to Text
The `TypeHint` enum SHALL NOT define `Unknown`. `TypeHint.FromString` SHALL resolve in order: precise match → alias set → fallback to `Text`. The enum SHALL expose `Values` and `IsValid`.

#### Scenario: FromString precise match
- **WHEN** `TypeHint.FromString("button")` (precise member name)
- **THEN** it returns the matching `TypeHint` value

#### Scenario: FromString alias
- **WHEN** `TypeHint.FromString(<alias>)` for a known alias
- **THEN** it returns the canonical `TypeHint` that alias maps to

#### Scenario: FromString unrecognized falls back to Text
- **WHEN** `TypeHint.FromString("something-unknown")`
- **THEN** it returns `TypeHint.Text` (never `Unknown`)

#### Scenario: Unknown member is absent
- **WHEN** the `TypeHint` enum members are enumerated
- **THEN** no member named `Unknown` exists

### Requirement: SelectionState resolves aliases via FromString
The `SelectionState` enum SHALL provide `FromString` with alias mapping: `checked`/`highlight` → `Selected`; `inactive`/`hidden` → `Disabled`; plus precise matches for the remaining members.

#### Scenario: checked maps to Selected
- **WHEN** `SelectionState.FromString("checked")`
- **THEN** it returns `SelectionState.Selected`

#### Scenario: highlight maps to Selected
- **WHEN** `SelectionState.FromString("highlight")`
- **THEN** it returns `SelectionState.Selected`

#### Scenario: inactive maps to Disabled
- **WHEN** `SelectionState.FromString("inactive")`
- **THEN** it returns `SelectionState.Disabled`

#### Scenario: hidden maps to Disabled
- **WHEN** `SelectionState.FromString("hidden")`
- **THEN** it returns `SelectionState.Disabled`

### Requirement: Region role is restricted
The `Region` record SHALL restrict `role` to a defined allowed set; any other value throws `DomainValidationException` naming `role`.

#### Scenario: Region accepts allowed role
- **WHEN** `Region` is created with an allowed `role`
- **THEN** it stores the role without throwing

#### Scenario: Region rejects disallowed role
- **WHEN** `Region` is created with a `role` not in the allowed set
- **THEN** construction throws `DomainValidationException` naming `role` and the illegal value

### Requirement: FlattenedElement bbox is nullable with 0.001 default and bounded confidence
The `FlattenedElement` record SHALL make `bbox` nullable (default `0.001` sentinel) and SHALL require `confidence ∈ [0,1]`.

#### Scenario: FlattenedElement accepts valid confidence
- **WHEN** `FlattenedElement` is created with `confidence=0.87`
- **THEN** it stores the value without throwing

#### Scenario: FlattenedElement rejects confidence > 1
- **WHEN** `FlattenedElement` is created with `confidence=1.5`
- **THEN** construction throws `DomainValidationException` naming `confidence`

#### Scenario: FlattenedElement rejects confidence < 0
- **WHEN** `FlattenedElement` is created with `confidence=-0.1`
- **THEN** construction throws `DomainValidationException` naming `confidence`

### Requirement: ScreenHints.Extra is a nested independent field
The `ScreenHints` record SHALL expose `extra` as a nested field (not flattened into top-level keys).

#### Scenario: Extra is nested
- **WHEN** a `ScreenHints` instance with `extra` content is serialized to JSON
- **THEN** the output contains a nested `extra` object rather than `extra` keys merged at the root

### Requirement: FlattenedScreen elements are an ImmutableArray sorted by (y,x)
The `FlattenedScreen` record SHALL store `elements` as `ImmutableArray<FlattenedElement>`, sorted by `(y, x)` at construction. `with` expressions SHALL produce a copy whose `elements` collection is independent from the original.

#### Scenario: Construction sorts by (y,x)
- **WHEN** `FlattenedScreen` is created with elements given out of `(y,x)` order
- **THEN** `screen.Elements` is ordered ascending by `y`, then by `x`

#### Scenario: elements is ImmutableArray
- **WHEN** the type of `FlattenedScreen.Elements` is inspected
- **THEN** it is `ImmutableArray<FlattenedElement>` (not `List<>`/`IList<>`/`IEnumerable<>` backed by a mutable list)

#### Scenario: with produces an independent collection
- **WHEN** a `with` copy of `FlattenedScreen` is made and the original `elements` reference is checked
- **THEN** modifying the copy's collection (via a new `ImmutableArray`) does not alter the original's `elements`

#### Scenario: Empty elements is allowed
- **WHEN** `FlattenedScreen` is created with an empty element set
- **THEN** it stores an empty `ImmutableArray` without throwing
