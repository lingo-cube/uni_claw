# Change: dsh-runtime-agent-subagent-run-entry

> **Buyer-confirmed gap**: the DSH outer agent has no authorized cross-process entry
> to start an existing `Runtime.Agent` semantic run. The DriverHost/DSH control
> plane is read-only (`run.start` verified absent at source); the earliest missing
> system link is `AUTHORIZED_ASYNC_RUN_START_ENTRY`.

## Flow

```
DSH outer agent
  → uniclaw-run-goal (DSH tool, zero-model)
  → dsh-plugin-uniclaw (existing plugin surface)
  → existing loopback TCP JSON-RPC transport
  → DriverHost run.start (new ADDITIVE wire method)
  → RunExecutionCoordinator (new)
  → existing Runtime.Agent semantic entry (RunSemanticGoalAsync)
  → existing IEnvironment composition (current Android path only)
  → RunAccepted(runId) returned immediately
```

Observation reuses the graduated read-only surfaces with the returned runId:
`run.events.after`, `run.snapshot.get`, `run.trap.get`, `run.events.drain`,
`evidence.get`. No second result protocol.

## Scope guardrails

- Additive only: the frozen 8 read-only wire methods keep exact semantics.
- Minimum request: `{ goal, objects, capabilities, device }` — no TaskSpec, no
  AgentProfile, no BusinessIntent, no capability sequence, no execution DSL.
- `ONE_ACTIVE_RUN_PER_DEVICE` enforced in the DriverHost control layer (never in
  Agent); distinct devices run concurrently.
- No IntelligenceSeam, no new event kinds/emitters, no device discovery, no
  iOS/Web/Desktop adapters, no pause/resume/abort, no DSH process supervision.
- Authority frozen: DSH = control/cognitive host; Kernel = semantic decision,
  authorization, execution, verification, GoalEvidence. Zero model calls in the
  control path.

## Documents

- `proposal.md` — buyer/gap/outcome/non-goals/falsifiers/authority
- `design.md` — verified source baseline, wire contract, coordinator, device
  factory boundary, concurrency, observability reuse, failure semantics
- `specs/dsh-runtime-agent-subagent-run-entry/spec.md` — R1–R10 requirements +
  scenarios
- `tasks.md` — slices, test plan T1–T12, falsifier mapping

## Planned sequence (planning context only — does not buy future changes)

1. `dsh-runtime-agent-subagent-run-entry` (this change)
2. `needs-intelligence-semantic-baseline`
3. `dsh-intelligence-provider-integration`
4. `taskspec-intent-entry` (with a real caller buyer)
5. `second-device-adapter` (with an actual second-device buyer)
