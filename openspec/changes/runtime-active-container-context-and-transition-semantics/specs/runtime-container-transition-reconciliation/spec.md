## Purpose

Define immutable typed Container-transition results and one fail-closed atomic reconciliation boundary that honestly accepts fresh world location without granting recovery, action, route, or completion authority.

## ADDED Requirements

### Requirement: ContainerTransition is an immutable evidence result
For each accepted fresh observation reconciliation, the Runtime SHALL produce an immutable `ContainerTransition` result containing a derived `TransitionRef`, `FromObservedLocation`, `ToObservedLocation`, the pre-commit `ActiveExecutionContainer`, optional `ActiveParentAtObservation`, `FreshObservationRef`, optional existing `CompletenessRef`, `Kind`, and `Disposition`. `PreviousContainer` SHALL be derived from `FromObservedLocation`; completeness SHALL be referenced, not copied; and the Agent SHALL NOT store a mutable latest-transition field.

#### Scenario: Transition can be projected without mutable execution state
- **WHEN** a transition is committed
- **THEN** its immutable event/result SHALL be sufficient for a trace/read-model projection and no `_latestTransition`-equivalent mutable execution field SHALL be created

### Requirement: Transition kind and disposition vocabularies are closed
`Kind` SHALL be exactly one of `SAME_CONTAINER`, `ENTER_CHILD`, `VERIFIED_RETURN_TO_ACTIVE_PARENT`, `PREMATURE_RETURN_TO_ACTIVE_PARENT`, `KNOWN_NON_PARENT_TRANSITION`, `EXTERNAL_EXIT`, or `UNKNOWN_TRANSITION`. `Disposition` SHALL be exactly one of `OBSERVED_AND_EXECUTION_ADVANCED`, `OBSERVED_AND_EXECUTION_RESUMED`, `OBSERVED_EXECUTION_PRESERVED`, or `NO_COMMIT_FAIL_CLOSED`. Classification SHALL describe evidence and SHALL NOT authorize recovery, action, completion, route selection, or re-entry.

#### Scenario: Same Container is explicit
- **WHEN** an accepted fresh location equals the active execution Container
- **THEN** the transition SHALL be `SAME_CONTAINER` with `OBSERVED_EXECUTION_PRESERVED`

#### Scenario: Known destination is not authorization
- **WHEN** fresh evidence identifies a known non-parent Container
- **THEN** the transition SHALL be `KNOWN_NON_PARENT_TRANSITION`, the observed WorldBelief SHALL update, and no action or destination authorization SHALL be implied

### Requirement: Reconciliation uses validation before one commit
The Runtime SHALL prepare the candidate WorldBelief, validated current execution context, existing completeness snapshot/reference, immutable transition, candidate execution-context replacement, optional existing progress-ledger replacement, and optional Container observation acceptance before mutating live state. A single synchronous Agent-owned commit seam SHALL then accept all permitted replacements and emit the transition event, or commit none. The commit seam SHALL contain no observation, action, recovery, or asynchronous work.

#### Scenario: Grounding succeeds but classification cannot validate context
- **WHEN** a fresh observation can form a candidate WorldBelief but the active execution context is inconsistent or transition classification cannot complete
- **THEN** the observation SHALL remain unaccepted, `Disposition` SHALL be `NO_COMMIT_FAIL_CLOSED`, and WorldBelief, execution context, progress, and Container observations SHALL remain unchanged

#### Scenario: Accepted fresh grounded location updates belief
- **WHEN** grounding and transition validation succeed and the observation is accepted
- **THEN** the committed WorldBelief SHALL reference that fresh observation even when the active execution obligation remains on another Container

### Requirement: Enter-child normal path remains semantically equivalent
An `ENTER_CHILD` transition SHALL update observed location, append the existing parent/child-obligation entry to `ActiveAncestorPath`, and advance `ActiveExecutionContainer` only after existing action authorization and fresh child-transition evidence succeed. It SHALL NOT create a navigation edge or new authorization.

#### Scenario: Root enters Display
- **WHEN** observed and active execution location are `SettingsRoot`, an existing authorized child action executes, and fresh evidence grounds `Display`
- **THEN** the transition SHALL be `ENTER_CHILD`, observed and execution location SHALL become `Display`, and the path SHALL retain `SettingsRoot` as the active parent

### Requirement: Verified return remains completeness-gated
A `VERIFIED_RETURN_TO_ACTIVE_PARENT` transition SHALL require the existing child completeness/authorized-obligation evidence plus fresh exact-parent continuity before it resumes the parent and pops the path. Observing the parent, returning to the parent, or dispatching a return action SHALL NOT independently prove child or subtree completion.

#### Scenario: Complete Display returns to Root
- **WHEN** `Display` has the existing required completion evidence and fresh exact-parent evidence proves `SettingsRoot`
- **THEN** the transition SHALL be `VERIFIED_RETURN_TO_ACTIVE_PARENT`, observed and execution location SHALL become `SettingsRoot`, and existing completion evidence SHALL be preserved

#### Scenario: Incomplete Display appears at Root
- **WHEN** fresh accepted evidence grounds immediate parent `SettingsRoot` while `Display` lacks the required completion evidence
- **THEN** the transition SHALL be `PREMATURE_RETURN_TO_ACTIVE_PARENT`, observed location SHALL become `SettingsRoot`, execution obligation and path SHALL remain on `Display`, and completion, recovery, and re-entry SHALL remain unauthorized

### Requirement: Non-parent, external, and unknown destinations fail closed without hidden authority
Fresh accepted evidence for a known non-parent Container SHALL update observed WorldBelief and preserve execution obligation. A changed external foreground SHALL classify as `EXTERNAL_EXIT` and remain subject to the existing boundary-obligation policy. Accepted evidence whose semantic location is Unknown SHALL update WorldBelief to Unknown and classify as `UNKNOWN_TRANSITION`. None of these kinds SHALL mutate execution context unless a separate existing contract already authorizes that exact update.

#### Scenario: Known sibling-like destination is observed
- **WHEN** the Runtime observes a known Container that is neither active execution nor its immediate active parent
- **THEN** it SHALL record `KNOWN_NON_PARENT_TRANSITION`, update observed WorldBelief, preserve execution context, and choose no next action from the classification

#### Scenario: Runtime leaves the owned foreground
- **WHEN** fresh accepted evidence proves a different external foreground
- **THEN** it SHALL record `EXTERNAL_EXIT`, preserve existing boundary/completion authority, and SHALL NOT model the external destination as a recursive child

#### Scenario: Destination identity is unknown
- **WHEN** a fresh observation is accepted but semantic grounding resolves to Unknown
- **THEN** WorldBelief SHALL honestly become Unknown, the transition SHALL be `UNKNOWN_TRANSITION`, execution context SHALL remain unchanged, and subsequent policy SHALL fail closed under existing contracts

### Requirement: Sibling continuation remains an independent Agent decision
After a verified return to a parent, the transition result and active ancestor path SHALL NOT select, authorize, or imply the next sibling. The Agent SHALL continue to use existing fresh inventory, progress, and action-authorization evidence.

#### Scenario: Next sibling is considered after return
- **WHEN** a child has verified return to its parent and another sibling remains pending
- **THEN** the parent SHALL resume first and the next sibling SHALL require a separate existing Agent authorization decision

### Requirement: Normal-path control flow remains equivalent
For same-Container work, authorized child entry, verified parent return, sibling continuation, and authorized external boundary handling, the change SHALL preserve existing action count, authorization conditions, branch-progress semantics, completeness conditions, GoalEvidence authority, and terminal outcome for equal deterministic inputs.

#### Scenario: Deterministic normal-path replay
- **WHEN** equal deterministic normal-path inputs are executed before and after consolidation
- **THEN** action dispatches, accepted observations, progress evidence, GoalEvidence, and terminal result SHALL be semantically equivalent, with transition events as the only additive diagnostic evidence
