## Context

See `proposal.md` for motivation. Agent Concept Model v1 and Protocol invariants PI-24 through PI-27 freeze the ownership and separation of Primary Goal, Execution Goal, Runtime Outcome, and Goal Evaluation. Protocol v1 already provides Session `decisionRefs` / `evaluationRefs` at the semantic level but no UniClaw-side record contract, DTO, persistence, or consumer implementation.

This design deliberately stops at a semantic contract. Selecting a code owner now would either make DSH-specific types architectural or invent a UniClaw module with no runtime consumer.

## Goals / Non-Goals

**Goals:**

- Define the minimum information and invariants that any future record representation must preserve.
- Make SC-A, SC-B, and SC-C expressible without mutating Runtime truth.
- Provide a buyer-ready contract for later host/representation selection.
- Preserve append-oriented Session semantics and producer authority.

**Non-Goals:**

- A C# or TypeScript DTO, interface, storage adapter, database table, event store, DSH command/UI, Runtime Protocol method, or model integration.
- RuntimeAgent Decision unification, generic Agent event hierarchy, Task model, Session database model, or complete Trace/Fact/Evidence schema.
- Any code/storage/transport representation before a host/consumer representation gate is authorized.

## Decisions

### Decision 1: Freeze semantic record obligations before representation

The change defines records by required meanings, owners, correlations, and references rather than field names or serialization. A later Apply gate must select the representation owner and demonstrate a consumer.

Alternative rejected: immediately add shared DTOs. No shared assembly or DSH-neutral host package is currently authorized, and a DTO would prematurely choose ownership.

### Decision 2: UniAgent Decision contract is supervisory-only

The minimum Decision record contains these semantic roles:

- stable Decision identity;
- producer identity/role;
- Session and Primary-Goal correlation;
- one bounded supervisory disposition;
- basis references to producer-owned facts, outcomes, evidence, or earlier decisions;
- optional candidate Directive reference;
- optional superseded-Decision reference where a later judgment replaces a projection.

RuntimeAgent's internal semantic Decisions remain in their existing specific forms and are not forced behind a generic base record.

Alternative rejected: one universal Decision type for both agents. Their authority, consumers, and consequences differ, and a shared type risks letting supervisory judgments masquerade as execution decisions.

### Decision 3: Goal Evaluation is a distinct append-oriented record

The minimum Goal Evaluation contains these semantic roles:

- stable Evaluation identity and producer identity/role;
- Session and Primary-Goal correlation;
- relevant Runtime Outcome and Evidence references;
- `Completion = Completed | Incomplete | Indeterminate`;
- `Satisfaction = Satisfied | Unsatisfied | Indeterminate`;
- optional supporting Decision references;
- optional superseded-Evaluation reference.

The latest Goal Evaluation is a projection over records, not a mutable record.

Alternative rejected: reuse Runtime Result or a success boolean. It collapses Runtime lifecycle ownership and user-goal judgment.

### Decision 4: Operator participation uses producer-authored supersession

Operator judgment does not edit a UniAgent record. It appends an operator-authored Decision or Goal Evaluation that explicitly references what it supersedes. The projection may select the latest applicable evaluation.

Alternative rejected: an `isHumanOverride` mutable flag on an existing record. It erases producer authorship and history.

### Decision 5: AssistanceRequired is not part of the terminal or evaluation vocabulary

The contract may reference a non-terminal escalation as Decision basis, but it does not add a RunState, Goal Evaluation value, or transport. A later non-terminal escalation buyer remains required.

Alternative rejected: treat AssistanceRequired as Failed or Indeterminate completion. An escalation requests adjudication; it is not itself the Runtime or Goal conclusion.

### Decision 6: Documentation-only Apply is authorized; representation remains gated

The user's direct-completion instruction authorizes publishing and graduating the
transport/storage-independent semantic contract. The first producer is UniAgent,
operator judgment is a separate producer, and the consumers are User/Application,
Operator, and Session projection readers. The canonical representation owner for
this slice is the architecture contract document; dependency flows from the
contract to references of producer-owned Runtime records, never from Runtime back
to DSH or storage. Record lifetime is append-oriented semantically, while physical
persistence remains unspecified.

No code or durable schema may be added until a later human gate identifies:

1. the first real producer and consumer;
2. the representation owner and dependency direction;
3. whether DSH Session is sufficient or a host-neutral contract package is needed;
4. the minimum persistence/lifetime requirement;
5. validation scenarios for operator supersession and restart behavior, if persistence is purchased.

## Risks / Trade-offs

- **Risk: semantic contract is mistaken for implemented DTOs** → The contract declares documentation-only graduation and marks every code/storage/transport representation as not authorized.
- **Risk: generic records become a second truth store** → Records retain producer references and never reconstruct Runtime state or evidence.
- **Risk: closed Decision vocabulary is too small** → The six dispositions are the minimum buyer set; extensions require a spec amendment rather than an untyped string.
- **Risk: representation choice is deferred** → This is intentional; current architecture lacks an authorized host-neutral owner.
- **Trade-off: no immediate UI value** → The contract removes semantic ambiguity first and prevents incompatible DSH/UniClaw implementations.

## Migration Plan

There is no production migration. This Apply publishes the semantic contract only.
After a future representation authorization, implementation must begin with
buyer/owner selection, add the smallest representation, validate SC-A/B/C plus
append/supersession behavior, and only then expose a projection or UI if
separately purchased.

Rollback of this proposal removes only the proposed contract artifacts; no production state is affected.
