## ADDED Requirements

### Requirement: TextMatchMode enum distinguishes Exact and Contains matching

`DynamicMatcher` SHALL support a `TextMatchMode` enum with two values: `Exact` and `Contains` (default `Contains`). `MatchCondition` SHALL include a `TextMatchMode` field. `Exact` mode SHALL use string equality comparison. `Contains` mode SHALL use substring match (case-insensitive). The default SHALL be `Contains` for backward compatibility.

#### Scenario: TextMatchMode enum has exactly 2 values
- **WHEN** `Enum.GetValues<TextMatchMode>().Length` is evaluated
- **THEN** the result SHALL equal 2 (Exact, Contains)

#### Scenario: Exact mode matches only identical strings
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.TextPattern = "Settings"` and `condition.TextMatchMode = TextMatchMode.Exact` and the item's text is "Settings"
- **THEN** `MatchResult.matched` SHALL be true

#### Scenario: Exact mode rejects substring matches
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.TextPattern = "Settings"` and `condition.TextMatchMode = TextMatchMode.Exact` and the item's text is "Network Settings"
- **THEN** `MatchResult.matched` SHALL be false

#### Scenario: Contains mode matches substring
- **WHEN** `DynamicMatcher.match(condition, item)` is called with `condition.TextPattern = "Settings"` and `condition.TextMatchMode = TextMatchMode.Contains` and the item's text is "Network Settings"
- **THEN** `MatchResult.matched` SHALL be true

#### Scenario: Default TextMatchMode is Contains for backward compatibility
- **WHEN** a `MatchCondition` is created without specifying `TextMatchMode`
- **THEN** the `TextMatchMode` field SHALL default to `Contains`
