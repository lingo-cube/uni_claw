## ADDED Requirements

### Requirement: Operation.action is restricted to the Phase 1 set
The `Operation` record SHALL restrict `action` to `{click, swipe, back, input_text, no_action}`. `Wait` and `LongPress` SHALL NOT be members. `params` SHALL default to empty when not supplied. Illegal `action` values throw `DomainValidationException` naming `action`.

#### Scenario: click is allowed
- **WHEN** `Operation` is created with `action=click`
- **THEN** it stores the action without throwing

#### Scenario: no_action is allowed
- **WHEN** `Operation` is created with `action=no_action`
- **THEN** it stores the action without throwing

#### Scenario: Wait is not a member
- **WHEN** the `OperationAction` (or equivalent) enum is enumerated
- **THEN** no member named `Wait` exists

#### Scenario: LongPress is not a member
- **WHEN** the `OperationAction` enum is enumerated
- **THEN** no member named `LongPress` exists

#### Scenario: Illegal action throws
- **WHEN** `Operation` is created with an `action` outside the allowed set
- **THEN** construction throws `DomainValidationException` naming `action` and the illegal value

#### Scenario: params defaults to empty
- **WHEN** `Operation` is created without supplying `params`
- **THEN** `params` is an empty value (empty collection/default), not null-unsafe

### Requirement: Target.by is restricted to text/coordinate/ui_index
The `Target` record SHALL restrict `by` to `{text, coordinate, ui_index}`. `ResourceId` and `ElementType` SHALL NOT be members. Illegal `by` values throw `DomainValidationException` naming `by`.

#### Scenario: text is allowed
- **WHEN** `Target` is created with `by=text`
- **THEN** it stores `by` without throwing

#### Scenario: coordinate is allowed
- **WHEN** `Target` is created with `by=coordinate`
- **THEN** it stores `by` without throwing

#### Scenario: ui_index is allowed
- **WHEN** `Target` is created with `by=ui_index`
- **THEN** it stores `by` without throwing

#### Scenario: ResourceId is not a member
- **WHEN** the `TargetBy` (or equivalent) enum is enumerated
- **THEN** no member named `ResourceId` exists

#### Scenario: ElementType is not a member
- **WHEN** the `TargetBy` enum is enumerated
- **THEN** no member named `ElementType` exists

#### Scenario: Illegal by throws
- **WHEN** `Target` is created with a `by` outside the allowed set
- **THEN** construction throws `DomainValidationException` naming `by`

### Requirement: RestoreAction reuses Operation validation
The `RestoreAction` record SHALL apply the same `action` restriction and `params` default as `Operation`.

#### Scenario: RestoreAction rejects illegal action
- **WHEN** `RestoreAction` is created with an `action` outside the Operation allowed set
- **THEN** construction throws `DomainValidationException` naming `action`

#### Scenario: RestoreAction accepts no_action
- **WHEN** `RestoreAction` is created with `action=no_action`
- **THEN** it stores the action without throwing
