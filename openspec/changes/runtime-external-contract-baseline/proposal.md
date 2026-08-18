# Proposal: runtime-external-contract-baseline

## Buyer

The Runtime ↔ External Intelligence Harness architecture evolution
(Runtime.Agent autonomous → executable subagent → collaborative → hybrid) needs a
**frozen, versioned External Contract** that fixes the boundary between the UniClaw
Runtime and any external Intelligence Harness (DSH today). The contract is the
anchor that prevents protocol pollution: each future plane (Assistance, Guidance,
Execution Handoff) must be added inside a defined contract frame instead of growing
its own ad-hoc wire format.

This is the gate recommended by
`docs/decisions/runtime-dsh-architecture-gap-analysis.md`
(NextGate: `RUNTIME_EXTERNAL_CONTRACT_GATE`).

## Gap

The Goal plane (`run.start`, dsh-runtime-agent-subagent-run-entry) and the Data
plane (`run.snapshot.get` / `run.events.after` / `run.trap.get` / `evidence.get`,
graduated control-plane baseline) are **implemented and high quality, but have
never been fixed as one unified External Contract**:

- No plane taxonomy exists as a contract: the five target planes
  (Goal / Data / Assistance / Guidance / Execution Handoff) are not defined as a
  single contract document with boundary semantics.
- Versioning is a single wire integer (`UniClawWireContract.ProtocolVersion = 1`)
  with no contract-level evolution rules (how a plane is added, how a field is
  deprecated, backward-compat obligations).
- The Assistance, Guidance, and Execution Handoff planes have ZERO implementation
  (verified: no `AssistanceRequest` / `GuidanceProposal` / `ExecutionYield` /
  `ExecutionReturn` tokens anywhere in `src/` or `dsh-plugin-uniclaw/src/`).
  Their boundary semantics must be declared so later gates implement inside the
  contract frame.
- Correlation and world-version primitives exist in raw form
  (`RuntimeEvent.CorrelationId` reusing TraceId; `Observation.SequenceNumber`
  monotonic) but are not defined as contract primitives — a future Assistance
  plane needs these pre-defined to prevent stale/uncorrelated responses.

## What this change does

**Document-only contract baseline** (no code, no DTO, no Runtime modification):

1. Defines the **five-plane Runtime External Contract** (Goal / Data / Assistance /
   Guidance / Execution Handoff): direction, target messages, semantics, and
   authority constraints — exactly as described by the buyer's target architecture
   and verified against current repository reality.
2. **Maps each implemented plane to its current wire surface**: Goal plane =
   `run.start` / `RunStartRequest` / `RunAccepted`; Data plane = the frozen 8
   read-only methods + `RunSnapshot` + `RuntimeEvent` + `EvidenceRef`. The
   implemented methods' semantics are frozen as contract clauses.
3. Defines the **contract versioning policy**: additive-first evolution, frozen
   method set (the 8 read-only + `run.start`), backward-compatibility obligations,
   explicit deprecation rules.
4. Pre-defines the **correlation and world-version primitives** as contract
   concepts (not code): request correlation shape and the
   `Observation.SequenceNumber` world-version binding rule, so the future
   Assistance plane has a defined frame.
5. **Declares the deferred planes** (Assistance / Guidance / Execution Handoff):
   boundary semantics + authority constraints only; NO message wire format is
   frozen, NO implementation is claimed. These belong to later gates
   (`RUNTIME_ASSISTANCE_SEAM_GATE`, Guidance / Yield gates).
6. Fixes the **authority clauses** of the contract (Guidance ≠ Truth ≠
   Authorization ≠ Goal completion; Assistance = capability-gap expression, not an
   LLM call; DSH has no physical/GoalEvidence/binding/belief authority) and the
   **collaboration levels** L0–L3.

## Non-goals (explicitly out of scope)

- Any code change: no new DTOs, no wire methods, no Runtime modification, no DSH
  plugin change (pure documentation gate).
- Implementing the Assistance seam (`AssistanceRequest`), Guidance
  (`GuidanceProposal`), or Execution Handoff (`ExecutionYield`/`ExecutionReturn`):
  deferred planes declared as boundaries only.
- Freezing wire formats for deferred planes (conceptual shapes may be sketched in
  design as boundary illustration, never as SHALL spec clauses).
- Introducing DSH/Cordis types into the Runtime namespace (unchanged).
- TaskSpec / AgentProfile / intelligence settings (still future-architecture
  concepts; not assumed to exist).
- Changing any existing method semantics (the 8 read-only methods and `run.start`
  keep their exact current semantics).

## Required output

`PROJECT_LEADER_RUNTIME_EXTERNAL_CONTRACT_BASELINE_RESULT` with
Decision `CONTRACT_BASELINE_CREATED` (or `REPAIR_REQUIRED`), the OpenSpec change
(proposal/design/spec/tasks/README) created and validated, and `NEXT_GATE =
RUNTIME_ASSISTANCE_SEAM_GATE` (L1 CONSULT) — the first plane that requires a
Runtime-side seam.

## Authority (contract clauses — unchanged from current reality)

- `DirectDSHPhysicalAuthority = MUST_BE_NO`
- `DirectDSHGoalEvidenceAuthority = MUST_BE_NO`
- `DirectDSHBindingAuthority = MUST_BE_NO`
- `DirectDSHStateBeliefAuthority = MUST_BE_NO`
- `AgentDependsOnDSH = MUST_BE_NO`
- `DriverHostProcessOwnedByPlugin = MUST_BE_NO`
- `GuidanceIsNotTruth = MUST_HOLD` (Guidance ≠ Truth ≠ Authorization ≠ Goal completion)
- `AssistanceIsCapabilityGapExpression = MUST_HOLD` (not an LLM invocation)
- `ModelCallsForControlPath = MUST_BE_0`

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | code change | the gate adds/changes any production or test code (DTOs, wire methods, Runtime, plugin) |
| F2 | DSH into Runtime | the contract introduces DSH/Cordis types into the Runtime namespace or assumes Runtime depends on any Harness |
| F3 | phantom implementation | the contract claims Assistance/Guidance/Execution Handoff are implemented (they have zero repository presence) |
| F4 | frozen semantics change | the contract alters the semantics of the 8 read-only methods or `run.start` |
| F5 | guidance-as-authority | the contract presents Guidance as truth, authorization, or goal completion |
| F6 | DSH authority grant | the contract grants DSH physical/GoalEvidence/binding/belief authority |
| F7 | future-as-existing | the contract assumes TaskSpec/AgentProfile/intelligence settings or any unimplemented future design already exists |
| F8 | fabricated repository claims | any statement about current implementation contradicts repository evidence (verified file/method/token facts) |
| F9 | premature wire freeze | the contract freezes wire formats for deferred planes as SHALL clauses |

## Validation

- `openspec validate runtime-external-contract-baseline --strict --no-interactive`
- `scripts/check-consistency.sh`
- Cross-check against `docs/decisions/runtime-dsh-architecture-gap-analysis.md`
  (matrix classifications and the recommended gate).
