# Spec: Scroll Adaptive Strategies

## ADDED Requirements

### Requirement: Adaptive step calculation

The system SHALL calculate the next scroll step based on duplicate element ratio in the current result.

#### Scenario: High duplicate ratio increases step
- **WHEN** duplicate ratio is 0.8 (80%) and new element count is 5
- **THEN** next step = current step * 1.5 (clamped to MaxScrollStep)

#### Scenario: Low duplicate ratio keeps step
- **WHEN** duplicate ratio is 0.3 (30%)
- **THEN** next step = current step (unchanged)

#### Scenario: Ratio at threshold increases step
- **WHEN** duplicate ratio is exactly 0.7 (70%) and new element count is 3
- **THEN** next step = current step * 1.5

#### Scenario: Insufficient sample size keeps step
- **WHEN** duplicate ratio is 0.8 but new element count is 2 (< MinSampleSize of 3)
- **THEN** next step = current step (unchanged)

#### Scenario: Empty after elements keeps step
- **WHEN** after element set is empty
- **THEN** next step = current step (no ratio calculation)

#### Scenario: Adaptive step disabled
- **WHEN** EnableAdaptiveStep is false in config
- **THEN** next step always equals current step (adaptive calculation skipped)

### Requirement: Step clamping

The system SHALL clamp calculated step size between MinScrollStep and MaxScrollStep.

#### Scenario: Step below minimum
- **WHEN** calculated step is 0.005 and MinScrollStep is 0.01
- **THEN** final step is clamped to 0.01

#### Scenario: Step above maximum
- **WHEN** calculated step is 0.6 and MaxScrollStep is 0.5
- **THEN** final step is clamped to 0.5

#### Scenario: Step within bounds
- **WHEN** calculated step is 0.3 and bounds are [0.01, 0.5]
- **THEN** final step is 0.3 (unchanged)

### Requirement: Jump recovery step reduction

The system SHALL reduce step size by recovery factor during jump recovery attempts.

#### Scenario: First recovery halves step
- **WHEN** initial step is 0.3 and JumpRecoveryFactor is 0.5
- **THEN** recovery step = 0.3 * 0.5 = 0.15

#### Scenario: Second recovery quarters step
- **WHEN** first recovery step 0.15 failed, second recovery with same factor
- **THEN** recovery step = 0.15 * 0.5 = 0.075

#### Scenario: Recovery step clamping
- **WHEN** recovery calculation produces step below MinScrollStep
- **THEN** step is clamped to MinScrollStep

#### Scenario: Multiple retry configuration
- **WHEN** MaxJumpRetryCount is 5
- **THEN** recovery allows up to 5 retry attempts with successive step reduction

### Requirement: Safe step calculation

The system SHALL calculate safe step size that does not exceed remaining scroll distance.

#### Scenario: Step within remaining distance
- **WHEN** current progress is 0.3, max threshold is 1.0, preferred step is 0.3
- **THEN** safe step = 0.3 (unchanged)

#### Scenario: Step exceeds remaining distance
- **WHEN** current progress is 0.8, max threshold is 1.0, preferred step is 0.3
- **THEN** safe step = 0.2 (clamped to remaining distance)

#### Scenario: At maximum threshold
- **WHEN** current progress is 1.0, max threshold is 1.0
- **THEN** safe step = 0.0 (no distance remaining)

#### Scenario: Zero current progress
- **WHEN** current progress is 0.0, max threshold is 1.0, preferred step is 0.5
- **THEN** safe step = 0.5 (full distance available)

### Requirement: Progress epsilon comparison

The system SHALL use epsilon tolerance for progress boundary comparisons to handle floating-point precision.

#### Scenario: At bottom within epsilon
- **WHEN** current progress is 0.9995, max threshold is 1.0, epsilon is 0.001
- **THEN** progress is considered at bottom (difference < epsilon)

#### Scenario: Not at bottom beyond epsilon
- **WHEN** current progress is 0.99, max threshold is 1.0, epsilon is 0.001
- **THEN** progress is NOT considered at bottom (difference > epsilon)

#### Scenario: Exact match
- **WHEN** current progress equals max threshold exactly
- **THEN** always considered at bottom regardless of epsilon

### Requirement: Adaptive step calculator isolation

The system SHALL provide `AdaptiveStepCalculator` as a pure function component without side effects.

#### Scenario: Calculation without state mutation
- **WHEN** CalculateNextStep is called with inputs
- **THEN** returns calculated step without mutating config or verify result

#### Scenario: Repeatability
- **WHEN** CalculateNextStep is called twice with same inputs
- **THEN** returns identical step values

### Requirement: Jump recovery handler isolation

The system SHALL provide `JumpRecoveryHandler` as a component with controlled side effects through executor interface.

#### Scenario: Recovery via executor interface
- **WHEN** recovery needs to execute scroll
- **THEN** calls ScrollActionExecutor.Execute (not direct vision service)

#### Scenario: Rollback via executor interface
- **WHEN** recovery needs to rollback progress
- **THEN** calls appropriate rollback method on action executor

#### Scenario: Recovery statistics recording
- **WHEN** recovery completes
- **THEN** returns JumpRecoveryResult with retry count and final progress
