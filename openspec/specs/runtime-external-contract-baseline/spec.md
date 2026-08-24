# Spec: runtime-external-contract-baseline

## Purpose

Define the Runtime to External Intelligence Harness contract boundary and its five-plane baseline.

## Requirements

### Requirement: Five-plane contract definition

The contract MUST define the five planes (Goal / Data / Assistance / Guidance /
Execution Handoff) with direction, target messages, semantics, and status, exactly
as captured in the buyer's target architecture.

#### Scenario: each plane has an explicit status

Given the contract document,
When each plane is inspected,
Then it carries a status of IMPLEMENTED (Goal, Data) or DEFERRED (Assistance,
Guidance, Execution Handoff), matching repository reality.

#### Scenario: deferred planes declare boundaries only

Given a DEFERRED plane,
When its contract text is inspected,
Then it contains boundary semantics and authority constraints but NO frozen wire
format (no SHALL clause on message fields).

### Requirement: Implemented-plane freezing

The Goal and Data planes' implemented surfaces MUST be frozen as contract clauses
with their exact current semantics.

#### Scenario: goal plane maps to run.start

Given the Goal plane clause,
When it is inspected,
Then it references exactly `run.start` with `RunStartRequest { goal, objects,
capabilities, device }` → `RunAccepted { accepted, runId, runState }`,
DriverHost-owned runId, asynchronous, deterministic `request_rejected`.

#### Scenario: data plane maps to the frozen read-only surface

Given the Data plane clause,
When it is inspected,
Then it references exactly the 8 read-only methods (`ping`, `run.list`,
`run.snapshot.get`, `run.trap.get`, `run.events.after`, `run.events.drain`,
`evidence.get`, `control.support`), the 13-field classified `RunSnapshot`, the
18-family `RuntimeEvent` vocabulary, and logical `EvidenceRef`.

### Requirement: Versioning policy

The contract MUST define additive-first evolution, the frozen 9-method set (8
read-only + `run.start`), backward-compatibility obligations, and explicit
deprecation rules.

#### Scenario: additive evolution is the only evolution

Given the versioning policy,
When a new plane is added,
Then it MUST add new methods/messages and MUST NOT modify the semantics of the
frozen 9-method set.

#### Scenario: contract version and wire version are distinct

Given the versioning policy,
When both versions are inspected,
Then the contract baseline version is the change archive name and the wire
protocol version is `UniClawWireContract.ProtocolVersion = 1`; they are not the
same concept.

### Requirement: Correlation and world-version primitives

The contract MUST pre-define the correlation identity and the world-version
binding rule using existing raw fields only (no new code).

#### Scenario: primitives reuse existing fields

Given the primitive definitions,
When they are inspected,
Then correlation references `RuntimeEvent.CorrelationId`/`EventId` and world
version references `Observation.SequenceNumber` + `RuntimeEvent.ObservationSequence`.

#### Scenario: staleness rule is defined

Given the world-version primitive,
When the staleness rule is inspected,
Then it states that advice/guidance bound to an old world version never mutates
current belief and the Runtime re-observes fresh evidence before applying anything.

### Requirement: Deferred-plane declarations

The contract MUST declare Assistance, Guidance, and Execution Handoff as DEFERRED
with zero-implementation evidence and boundary semantics.

#### Scenario: assistance is a capability-gap expression

Given the Assistance declaration,
When it is inspected,
Then it states the request expresses a missing Runtime capability, is NOT an LLM
invocation, and the Runtime keeps final decision authority (L1 CONSULT).

#### Scenario: guidance is not authority

Given the Guidance declaration,
When it is inspected,
Then it states Guidance ≠ Truth ≠ Authorization ≠ Goal completion and completion
still requires Kernel GoalEvidence (I-10).

#### Scenario: execution handoff requires lease semantics

Given the Execution Handoff declaration,
When it is inspected,
Then it states a temporary execution lease with re-observe/reconcile after
`ExecutionReturn`, beyond the current `RunState` model, and that no implementation
exists today.

### Requirement: Authority clauses

The contract MUST fix the authority clauses unchanged from current reality and
state the mechanical guards that enforce them.

#### Scenario: authority clauses match the frozen boundary

Given the authority clauses,
When they are inspected,
Then DSH has no physical/GoalEvidence/binding/belief authority; Agent has no DSH
dependency; the plugin never owns the DriverHost process; control path is
zero-model.

#### Scenario: guards are cited

Given the authority clauses,
When the enforcement section is inspected,
Then it cites the actual mechanical guards (Guard 1/2/10a/10b/10c/10d,
`PluginIntegrationGuardTests`, node F16/F17).

### Requirement: Collaboration levels

The contract MUST define collaboration levels L0–L3 with the additive property.

#### Scenario: levels map to planes

Given the collaboration levels,
When they are inspected,
Then L0 = autonomous (implemented), L1 = CONSULT (Plane 3, MISSING), L2 =
DELEGATE_PLANNING (Plane 4, MISSING), L3 = YIELD (Plane 5, MISSING), and a higher
level never removes a lower level's authority.

### Requirement: Documentation-only gate

This change MUST NOT add or modify any production/test/plugin code.

#### Scenario: zero code footprint

Given the change's file set,
When it is inspected,
Then every file lives under `openspec/changes/runtime-external-contract-baseline/`
and no `src/`, `tests/`, or `dsh-plugin-uniclaw/` file is modified.

### Requirement: Repository-truth consistency

Every current-reality statement in the contract MUST match verified repository
evidence.

#### Scenario: no phantom implementations

Given the contract text,
When deferred planes are described,
Then they are described as having zero implementation (verified token absence) and
no future design (TaskSpec/AgentProfile/intelligence settings) is assumed to exist.
