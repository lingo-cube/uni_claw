## ADDED Requirements

### Requirement: PageTransition record tracks page-to-page navigation distinct from FSM StateTransition

UniClaw.Core.Observability namespace SHALL define a `sealed record class PageTransition` with fields: FromPage (string), ToPage (string), TransitionType (string — "forward", "back", "sub_page", "popup_dismiss"), NodeId (string? = null), DurationMs (double? = null), Timestamp (DateTimeOffset = default), Metadata (Dictionary<string, object>? = null). PageTransition MUST be distinct from StateTransition — StateTransition records FSM state changes (Idle→Traversing), PageTransition records user-facing page navigation (home→wifi).

#### Scenario: PageTransition record structure
- **WHEN** a PageTransition is constructed with FromPage="home", ToPage="wifi", TransitionType="forward"
- **THEN** the record MUST contain all 7 fields with correct values and defaults

#### Scenario: PageTransition distinct from StateTransition
- **WHEN** trace data contains both StateTransition and PageTransition records
- **THEN** StateTransition.FromState/ToState are FSM enum names, PageTransition.FromPage/ToPage are page ID strings — no overlap in field semantics

#### Scenario: TransitionType values
- **WHEN** TransitionType is set
- **THEN** it MUST be one of: "forward" (entering child page), "back" (pressing back), "sub_page" (sub-page completion), "popup_dismiss" (popup dismissed then page transition)
