# Tasks: dsh-runtime-agent-subagent-run-entry

> System of record for implementation progress. Check each box the moment the
> task is complete; final counts are reported in the leader result.

## Slices

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — DriverHost `run.start` wire method + `IUniClawRunExecution` seam
      (additive dispatch case, `RunStartRequestDto`/`RunAcceptedDto`, typed
      `request_rejected` rejection, frozen 8 methods untouched)
- [x] Slice 2 — `RunExecutionCoordinator` (request validation, DriverHost-owned
      runId, runtime-graph build via injected device factory, async start of
      `Agent.RunSemanticGoalAsync`, observability registration reuse)
- [x] Slice 3 — Device composition factory boundary for the current Android path
      (explicit selector → RunExecutionGraph beside `PhysicalHostComposition`;
      unknown selector → `request_rejected`)
- [x] Slice 4 — `ONE_ACTIVE_RUN_PER_DEVICE` enforcement in the coordinator
      (device-key → active-run mapping; no locking inside Agent; distinct devices
      concurrent)
- [x] Slice 5 — Live observability registration + replace (`RuntimeTraceRecorder` +
      `AgentStateSnapshot.From` + accept-time `RegisterRun` (empty stream) +
      terminal `ReplaceRunProjection`; no new event kinds/emitters)
- [x] Slice 6 — `ControlSupportAudit` truthfulness amendment for the `start` row
      (reason `AUTHORIZED_RUN_START_ENTRY`; pause/resume/stop/abort stay deferred)
- [x] Slice 7 — DSH command `uniclaw-run-goal` + `adapter.runStart` (strict input
      validation, returns runId + runState, zero inference calls)
- [x] Slice 8 — Test plan T1–T12 (below)
- [x] Validation — build, dotnet test, node suite, consistency, openspec validate

## Implementation evidence (test plan T1–T12)

- [x] T1 — Real DriverHost accepts `run.start` and returns runId
      (`RunStartWireTests.RunStart_AcceptsAndReturnsRunId_AsyncShape_NoBlocking`,
      `DriverHostRunStartE2ETests` node client `E2E_RUN_START_ACCEPTED`)
- [x] T2 — runId immediately visible through existing `run.list` / snapshot / event
      surfaces (`RunExecutionCoordinatorTests.StartRun_ReturnsRunIdImmediately_AndRunIsImmediatelyObservable`,
      wire `run.list` + snapshot + events-after right after accept)
- [x] T3 — Runtime.Agent actually executes through the existing semantic run entry
      (`CompletedPath` — same Agent entry, `RunCompleted` present)
- [x] T4 — Completed path observable (`CompletedPath_SameRunId_ExistingSurfacesShowCompletedAndRunCompleted`;
      E2E `E2E_SNAPSHOT_COMPLETED_OK` / `E2E_EVENTS_COMPLETED_OK`)
- [x] T5 — Failed path observable (`FailedPath_AcceptedThenFailed_RpcAccept_ExistingSurfacesShowFailed` —
      RunFailed event + Failed snapshot; RPC itself stayed accepted)
- [x] T6 — invalid request rejected before run creation, no fake run
      (`InvalidGoal_Rejected_NoRunCreated`, wire `RunStart_InvalidGoal_...`,
      `RunStart_MalformedPayload_BadRequest` — run.list stays empty)
- [x] T7 — plugin/DSH disconnect does not corrupt run state
      (design: run is DriverHost-owned after acceptance; existing session/event
      reconnect semantics preserved; no cancellation tied to socket lifetime)
- [x] T8 — existing 8 read-only methods retain exact compatibility
      (`FrozenReadOnlyMethods_StillWork_OnServerWithExecutionSeam`; adapter F16
      guard = frozen 8 + additive run.start; `UniClawDriverHostServerTests`/
      `DriverHostPluginE2ETests` still green)
- [x] T9 — zero model calls in command/control path (F16/F17 guards: control-plane
      src free of llm/vlm/model/ADB tokens; run-goal handler performs one
      `adapter.runStart` only — commands test asserts exactly one call)
- [x] T10 — no Agent → DSH/plugin dependency (Guard 10a/10b/10d +
      `RunStartExecutionSeam_NotInAgentSemantics_AndSurfaceStaysReadOnly`;
      `PluginIntegrationGuardTests.GuardA/B/C/D/F` green)
- [x] T11 — device selector resolves the existing Android environment composition
      (`AndroidCompositionTests.SerialSelector_BuildsCurrentAndroidExecutionGraph_NoIo`
      — real AdbScreenshotSource + LocalVisionPerceptionSource + AdbDispatchTarget
      stack, no live device required)
- [x] T12 — same-device concurrency policy enforced
      (`SameDeviceExclusivity_SecondConcurrentRejected_ReleasedAfterTerminal`,
      wire `RunStart_BusyDevice_RequestRejected_NoSecondRun` with a gated env)

## Falsifier mapping

- [x] F1 — no coordinates / DeviceAction / ElementIndex as semantic authority in the
      request or response (RunStartRequest carries only goal/objects/capabilities/device)
- [x] F2 — `run.start` never blocks until completion (RunAccepted returned before
      execution; E2E proves accept-then-observe flow)
- [x] F3 — runId always DriverHost-created (`RunExecutionCoordinator` runId factory)
- [x] F4 — no second event/result protocol (existing surfaces only; store replace is
      on the SAME store)
- [x] F5 — Agent/Runtime carries no DSH/plugin dependency (Guard 2 / 10a/10b/10d green)
- [x] F6 — plugin never launches/supervises/restarts DriverHost (plugin connects;
      no supervision code)
- [x] F7 — zero model calls in `run.start` / `uniclaw-run-goal` (node F16/F17 guards green)
- [x] F8 — no physical bypass (no device-operation path from DSH; run-goal returns runId only)
- [x] F9 — same-device exclusivity enforced in the control layer (coordinator; T12)
- [x] F10 — no TaskSpec / IntelligenceSeam introduced (request = minimum four fields;
      no new semantic abstractions)
