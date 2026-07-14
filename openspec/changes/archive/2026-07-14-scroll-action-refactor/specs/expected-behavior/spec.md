## MODIFIED Requirements

### Requirement: NumericAnchor defines reference baseline values with tolerance

NumericAnchor SHALL be a sealed record class with fields: `TotalSteps` (int, required), `VisitedPagesCount` (int, required), `ActionHistoryCount` (int, required), `ElapsedSecondsMax` (double, required), and informational scroll fields `ScrollCount` (int, default 0), `ScrollUpCount` (int, default 0), `ScrollDistance` (double, default 0.0), `FinalProgress` (double, default 0.0). The previously-present `JumpDetected`, `JumpRecovered`, and `AdaptiveStepIncreases` fields SHALL be REMOVED — the ScrollHandler pipeline that produced them is deleted and they have no data source. Verification SHALL compare actual TraversalResult values against anchor values with ±5% tolerance for numeric counts and ≤ for elapsed time. NumericAnchor results SHALL be informational (non-blocking). This NumericAnchor field-set change is a C-11 constitution-level ExpectedBehavior schema change.

#### Scenario: Numeric anchor within tolerance
- **WHEN** NumericAnchor.TotalSteps = 145 and actual TotalSteps = 143 (within ±5%)
- **THEN** VerificationReport includes RuleResult with RuleId="numeric_anchor:total_steps", Passed=true, Actual="143 (expected 145 ±5%=137.25~152.75)"

#### Scenario: Numeric anchor outside tolerance
- **WHEN** NumericAnchor.TotalSteps = 145 and actual TotalSteps = 160 (outside ±5%)
- **THEN** VerificationReport includes RuleResult with RuleId="numeric_anchor:total_steps", Passed=false, Actual="160 (expected 145 ±5%=137.25~152.75)"

#### Scenario: Removed jump fields are absent
- **WHEN** a NumericAnchor record is constructed
- **THEN** it exposes no `JumpDetected`, `JumpRecovered`, or `AdaptiveStepIncreases` member (compilation fails on any reference to them)
