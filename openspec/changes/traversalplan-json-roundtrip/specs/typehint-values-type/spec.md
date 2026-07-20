## MODIFIED Requirements

### Requirement: TypeHint.Values return type
TypeHint.Values SHALL return `IReadOnlyList<string>` containing the 8 canonical snake_case enum member names, consistent with Direction, MenuItemType, ExpectedAction, and NodeType enums.

#### Scenario: Values returns string list
- **WHEN** TypeHint.Values is accessed
- **THEN** it SHALL return `IReadOnlyList<string>` with exactly 8 elements: ["clickable_text", "switch", "slider", "button", "icon", "input_field", "text", "image"]

#### Scenario: Values consistency with other enums
- **WHEN** comparing TypeHint.Values type signature with Direction.Values, MenuItemType.Values, ExpectedAction.Values, NodeType.Values
- **THEN** all SHALL have the same return type `IReadOnlyList<string>`
