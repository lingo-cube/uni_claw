## Purpose

Define a scenario-neutral boundary through which externally owned semantic
knowledge can contribute typed candidate evidence without acquiring Runtime,
Agent, FSM, Traversal, recovery, or Goal-completion authority.

## ADDED Requirements

### Requirement: Scenario knowledge is externally owned

Scenario classifiers, application vocabulary, page identities, parent/child
relations, locale or platform recognition rules, and scenario corpora MUST be
owned by an external Scenario Knowledge Package and Semantic Capability Binding.
Generic Runtime production source MUST NOT contain or infer that knowledge.

#### Scenario: Runtime is scenario neutral
- **WHEN** Runtime production dependencies and executable classification rules are inspected
- **THEN** no scenario package, application label, page classifier, locale label, route, or scenario corpus is owned by Generic Runtime

### Requirement: Semantic capability input is evidence-only

A Semantic Capability Binding MUST accept only the current source-qualified
Observation facts and bounded verified history explicitly admitted by the
Runtime contract. It MUST NOT receive Goal, Strategy, expected state, Action,
Planning state, FSM state, recovery command, or an Agent callback.

#### Scenario: Authority-bearing context is unavailable
- **WHEN** a Semantic Capability Binding is invoked
- **THEN** its input contains no Goal, Strategy, expected-state, Action, Planning, FSM, recovery-command, or Agent-callback value

### Requirement: Semantic Evidence Protocol V2 is typed and versioned

Runtime MUST own a versioned Semantic Evidence Protocol V2 whose authority-
bearing fields use closed evidence kinds, typed observation and occurrence
references, and manifest-resolved semantic symbols. The protocol MUST NOT carry
free scenario strings, hidden selectors, coordinates as execution targets,
routes, DeviceAction, completion flags, FSM commands, Run commands, or callbacks.

#### Scenario: Valid typed candidate is admitted
- **WHEN** an installed compatible binding emits a typed candidate with a registered manifest symbol, current source-qualified observation reference, and valid provenance
- **THEN** Runtime may admit it as candidate evidence while retaining belief and decision authority

#### Scenario: Unregistered or executable payload fails closed
- **WHEN** evidence contains an unregistered semantic symbol, selector, route, action, completion flag, FSM command, Run command, callback, or unsupported protocol version
- **THEN** Runtime rejects the evidence without executing a fallback behavior

### Requirement: Candidate evidence kinds do not assert facts

Protocol V2 MAY represent container-identity, element-affordance, and container-
relation candidates. Every such value MUST remain candidate evidence with source,
freshness, scope, and provenance; it MUST NOT assert verified identity,
authorization, world fact, destination truth, or completion.

#### Scenario: Parent-return evidence remains advisory
- **WHEN** a binding emits a container-relation or return-affordance candidate for a current observation occurrence
- **THEN** Agent independently checks freshness, uniqueness, grounding, authorization, and post-action verification before any return action or destination belief

### Requirement: Coverage requirements are separate from observation evidence

Coverage requirements MUST be represented as versioned, admitted descriptors or
criterion bindings rather than SemanticEvidence. They MUST describe bounded
required evidence and MUST NOT expose `Complete`, `Satisfied`, `GoalReached`, or
terminal-state values.

#### Scenario: Coverage descriptor cannot complete work
- **WHEN** a capability supplies a valid coverage requirement descriptor
- **THEN** Runtime may use it as a bounded requirement input, but only Agent evaluation of fresh Runtime evidence may produce GoalEvidence or terminal completion

### Requirement: Runtime owns admission, fusion, and reconciliation

Runtime MUST validate protocol version, installed capability manifest, symbol
registration, source tier, source availability, observation freshness, frame
alignment, scope, references, and contradictions before candidate evidence may
influence WorldBelief. A Semantic Capability MUST NOT create Fact or mutate
WorldBelief directly.

#### Scenario: Contradictory sources remain unresolved
- **WHEN** primary visual evidence and auxiliary evidence contradict and Runtime lacks sufficient corroboration
- **THEN** Runtime records the contradiction and remains Unknown, rejects the candidate, or requests fresh observation without silently selecting the auxiliary claim

### Requirement: Runtime provides source-neutral canonical occurrence grounding

Runtime MUST normalize source-qualified current Observation occurrences into an
immutable canonical occurrence representation before generic affordance,
source-equivalence, or Agent DFS grounding consumes them. The canonical model
MUST preserve source tier, source-local reference, observation sequence, frame,
bounds, and provenance. It MUST NOT contain scenario meaning, selector, route,
action, completion, lifecycle command, or callback.

#### Scenario: Primary occurrence grounds without structured evidence
- **WHEN** admitted typed evidence references a fresh primary Vision occurrence and no auxiliary hierarchy is available
- **THEN** Runtime resolves the candidate to a primary-supported canonical occurrence without creating a structured surrogate

#### Scenario: Auxiliary-only occurrence remains ineligible
- **WHEN** a typed candidate or raw occurrence has only auxiliary source support
- **THEN** Runtime may retain it for diagnostics or reconciliation but marks it ineligible for action, verified identity, coverage, parent return, GoalEvidence, or lifecycle use

### Requirement: Evidence provenance must match its referenced occurrence

An admitted typed candidate referencing an occurrence MUST have provenance
consistent with that occurrence's source and tier. A capability MUST NOT point
at an auxiliary occurrence while declaring primary provenance. Runtime MUST
reject mismatches fail closed.

#### Scenario: Auxiliary occurrence with primary provenance is rejected
- **WHEN** a capability emits primary-provenance evidence whose occurrence reference resolves only to an auxiliary source
- **THEN** admission or canonical grounding rejects the evidence with zero action and zero authority effect

### Requirement: Semantic processing cannot upgrade source authority

Semantic interpretation MUST preserve the authority tier and provenance of every
input source. Evidence derived solely from an auxiliary ADB hierarchy source MUST
remain auxiliary after semantic interpretation and fusion.

#### Scenario: ADB-derived semantics remain auxiliary
- **WHEN** a Semantic Capability derives an identity, affordance, relation, or coverage candidate solely from ADB hierarchy evidence
- **THEN** the candidate remains auxiliary and cannot by itself authorize Action, establish verified Container identity, prove coverage, or satisfy GoalEvidence

### Requirement: Agent and lifecycle authority remain unchanged

Agent MUST remain the sole owner of continuation, candidate authorization,
action approval, recovery decision, verification, GoalEvidence evaluation, and
terminal decision. FSM MUST remain the lifecycle-transition owner and Traversal
MUST remain concrete execution owner. Semantic Capability and RuntimeAgent MUST
NOT start another Run or orchestrate Multi-Run continuation.

#### Scenario: Evidence cannot bypass Agent
- **WHEN** an admitted semantic candidate contributes to Runtime reasoning
- **THEN** any action still requires independent Agent authorization and follows FSM and Traversal ownership through fresh Observation and GoalEvidence

### Requirement: Existing Runtime achievements remain behaviorally preserved

The OpenWorld DFS model, SETTINGS-TREE-01 scenario behavior, RuntimeAgent Phase
1-4 loop, Strategy Contract model, FSM lifecycle, Traversal protocol, recovery
authority, and GoalEvidence terminal path MUST remain unchanged except for
replacing embedded scenario interpretation with admitted external evidence.

#### Scenario: Settings capability runs through the external boundary
- **WHEN** the Settings knowledge package and compatible binding are installed for SETTINGS-TREE-01
- **THEN** the scenario continues to validate the existing DFS and authority behavior through Generic Runtime without placing Settings knowledge in Runtime core

### Requirement: Agent DFS consumes validated canonical evidence

Agent DFS MUST retain discovery, authorization, execution ordering, recovery,
fresh verification, and completion authority while consuming validated
canonical occurrences instead of structured-source indices. DFS MUST NOT decide
whether Vision is trustworthy, interpret scenario vocabulary, or promote an
auxiliary source.

#### Scenario: Vision-only discovery and parent return
- **WHEN** external semantic evidence identifies fresh primary Vision
  navigation and parent-return occurrences
- **THEN** Agent may independently discover, ground, authorize, execute, and
  verify them without ADB hierarchy being present

#### Scenario: ADB-only parent return is rejected
- **WHEN** only an auxiliary hierarchy occurrence supports a parent-return candidate
- **THEN** Agent authorizes no return action and produces no identity, completion, or lifecycle proof
