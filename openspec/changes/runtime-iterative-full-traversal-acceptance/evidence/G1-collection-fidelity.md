# G1. Real-Emulator Collection Fidelity — Acceptance Evidence

## Leader's independent verification

- Worker's mechanism proof refines the leader's initial hypothesis (pinned admission
  projection, not post-release Unknown) — accepted; IR-G1 updated.
- Fix scope: `SettingsCampaignProgram.cs` only (composition layer); zero edits to
  ResultCollector/ScenarioRunner/TierAHost/Runtime/DriverHost — verified via git status.
- Real-emulator verify (worker-run, evidence `/tmp/p26-g1-verify.json`): terminal =
  Failed with truthful reason ("Source normalization is unresolved…" — the separate
  IR-G0 phenomenon, leader-owned), events = 4 non-empty, `terminalWait.timedOut=false`,
  gates/autonomy/invariants all pass on the REAL terminal, exactly one accepted
  `run.strategy.start`. Collection fidelity = SUCCESS (run success is NOT this item's
  criterion — truthful collection is).

## Proven mechanism (worker, file:line)

1. `RunExecutionCoordinator.StartStrategyRun` (:211-214) registers
   `AgentStateSnapshot.From(graph.Agent)` (by-value Idle copy) + empty trace.
2. `DriverHostObservability.RegisterOrReplace` (:137-144) pins that snapshot; event store
   appends the empty projection's events (zero).
3. During the run: `GetRunSnapshot` serves ONLY the pinned Idle snapshot; events empty.
   True terminal facts are invisible until finalization.
4. `ResultCollector.WaitForTerminalAsync` (600×100ms) cannot see Completed/Failed →
   final-reads the placeholder → Idle/null/empty. r1e exactly.
5. Coordinator finally-block `ReplaceRunProjection` (:404) materializes terminal state +
   full stream; `ReleaseReservation` removes only the coordinator entry — observability
   retains the final projection forever → post-terminal reads serve the truth. r1c proved
   the fast-run path; the fix extends the same truth to long runs.

## Fix

`SettingsCampaignProgram.BuildExecutor`: pre-collection `WaitForRunTerminalAsync` — polls
`host.Runs.ContainsKey(runId)` (release = deterministic end-of-run) + observability
terminal state, ~2s interval, 40-min bound, ct-honoring; on timeout never fabricates
(distinct ledger + truthful collection). The collector's own wait then passes trivially
against the persistent finalized projection.

DEVIATIONS: none material (cap 30→40 min per leader spec; behavior-neutral).

BLOCKED: no.
