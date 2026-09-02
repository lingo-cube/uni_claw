## Purpose

Define how revision-bound semantic correction facts reach the UniAgent obligation boundary so that task obligations can be recomputed without granting Slow or ContainerGraph planning, action, recovery, or completion authority.

## ADDED Requirements

### Requirement: Semantic corrections bind to exact evidence references
A semantic correction SHALL reference the Observation, Node, parent/source Node when present, trigger occurrence, TransitionOccurrence, and assessment revision it corrects. A correction SHALL NOT apply across a newer fresh evidence revision or to an unrelated occurrence.

#### Scenario: Wrong-child correction is fully grounded
- **WHEN** Slow determines that a transition interpreted as trigger C to child CPage actually used trigger D and reached DPage
- **THEN** the correction SHALL reference the exact parent, trigger occurrence, destination node, transition occurrence, and observation revision

#### Scenario: Correction target no longer current
- **WHEN** any required target reference has been superseded by newer fresh evidence
- **THEN** the correction SHALL NOT modify the current semantic or obligation view

### Requirement: UniAgent owns obligation recomputation
The Runtime SHALL expose accepted corrected semantic facts to one Agent-owned correction consumer. The consumer SHALL validate the exact Runtime occurrence and owner obligation binding, then use only the existing immutable obligation/progress owner. UniAgent SHALL remain the authority that decides whether an item remains pending, becomes satisfied through existing completion policy, requires return/re-entry, changes supervisory strategy, or fails. Slow, Graph, and Runtime composition SHALL NOT directly edit the obligation ledger.

#### Scenario: Traversal mis-click leaves intended item pending
- **WHEN** a correction proves D was visited although Fast interpreted C
- **THEN** the consumer SHALL keep or restore C as pending, SHALL retain D as observed occurrence evidence, and SHALL NOT mark D completed merely because it was observed

#### Scenario: Observed D already has valid completion evidence
- **WHEN** D is an authorized obligation whose completion was independently recorded by existing Agent policy
- **THEN** correction SHALL preserve that valid D completion while leaving all unrelated A/B and boundary progress unchanged

#### Scenario: Directed entry reaches wrong branch
- **WHEN** a correction proves the observed branch differs from the directed target
- **THEN** the consumer SHALL expose the directed target as unsatisfied and require a separate UniAgent decision through existing action authorization, while correction itself dispatches nothing

### Requirement: Correction consumption is fail-closed and idempotent
The Agent consumer SHALL reject a correction whose Run, Observation/revision, TransitionOccurrence, TriggerOccurrence, source/destination Node, parent scope, or intended obligation does not exactly match existing Runtime and owner evidence. Replaying an identical accepted correction SHALL produce no additional mutation and SHALL require no mutable consumed-correction registry.

#### Scenario: Wrong transition or trigger reference
- **WHEN** correction references a different transition or trigger occurrence from the owner-bound event
- **THEN** the consumer SHALL leave all obligation/progress evidence unchanged and report explicit rejection

#### Scenario: Identical correction is replayed
- **WHEN** an accepted traversal correction is consumed after intended C is already pending
- **THEN** the consumer SHALL report an idempotent no-change result and preserve all progress evidence

### Requirement: Correction consumption has no control or Goal authority
The correction consumer SHALL NOT construct or dispatch `DeviceAction`, invoke recovery, mutate GoalEvidence, declare Container/subtree/Goal completion, rewrite CurrentContainer, or add a second visited/pending/completion owner.

#### Scenario: Consumer corrects historical attribution after leaving child
- **WHEN** a valid correction for an older occurrence arrives after newer fresh current evidence
- **THEN** the consumer MAY correct only the historical obligation attribution and SHALL leave the newer current physical truth and GoalEvidence unchanged

### Requirement: Suggested recovery remains advisory
A semantic assessment MAY suggest recovery to a derived checkpoint, resolve an overlay, wait for a transient, or collect more evidence. Such a suggestion SHALL NOT be an action authorization, recovery plan, or FSM transition.

#### Scenario: Off-path suggestion names checkpoint
- **WHEN** a revision-bound assessment marks the current node off-path and identifies the last sufficiently confirmed node on the correct execution path
- **THEN** it MAY expose that node as a checkpoint proposal and UniAgent SHALL separately decide any recovery action

### Requirement: Checkpoint is a derived projection only
Any checkpoint exposed by this capability SHALL be derived from the current execution path and confirmation evidence. The Runtime SHALL NOT create a mutable checkpoint lifecycle, Graph object subtype, canonical parent, or recovery FSM in this change.

#### Scenario: No sufficiently confirmed path node
- **WHEN** current execution-path evidence cannot identify a sufficiently confirmed correct node
- **THEN** no checkpoint SHALL be fabricated and recovery remains an unresolved UniAgent decision
