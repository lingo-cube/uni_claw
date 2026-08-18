# DSH Control Plane Event Stream — Graduation Decision Record

> Status: GRADUATED (INDEPENDENT POST-APPLY REVIEW) | Decision: `PROJECT_LEADER_DSH_CONTROL_PLANE_EVENT_STREAM_GRADUATION_REVIEW` | Date: 2026-08-16
> Maturity: `DSH_CONTROL_PLANE_REALTIME_EVENT_STREAM_INTEGRATED`
> Change: `dsh-control-plane-event-stream`
> Change artifacts: `openspec/changes/dsh-control-plane-event-stream/` (archived same day)

## Decision

`GRADUATED` — the control plane now has a truthful, durable, regression-protected
real RuntimeEvent stream through the already-frozen read-only wire method
`run.events.after`. The real chain — DSH command registry → `uniclaw-events-after`
→ plugin adapter → frozen `run.events.after` → DriverHost read-only event
store/projection → formatted command result → control-plane client parse/load →
incremental event timeline — is integrated and durably regression-protected.

## Buyer

`CONTROL_PLANE_REALTIME_OBSERVABILITY_GAP`

## Command Contract

- **Command**: `uniclaw-events-after <runId> [--cursor <n>]`
- **WireMethod**: `run.events.after` (frozen; already existed before this change)
- **CursorSemantics**: `EXCLUSIVE_SEQ_GREATER_THAN_CURSOR` — events with
  `sequence > cursor`; no duplicate boundary event, no skipped event, original
  ordering preserved (independently verified: cursor=3 over seq 1,3,5 returns 5 only)
- **RegisteredCommands**: 6 (`uniclaw-events-after`, `uniclaw-evidence-open`,
  `uniclaw-inspect-run`, `uniclaw-inspect-trap`, `uniclaw-runs-list`,
  `uniclaw-shadow-analyze`) — no mutating commands, no duplicates

## Client Channel

- **ClientChannel**: real command channel → `uniclaw-events-after` → `run.events.after`
- **ShadowEventFallback**: NONE — event retrieval has zero dependency on
  `uniclaw-shadow-analyze` / ShadowAnalysis / shadow digest / LLM summary;
  Shadow remains an independent cognition surface (all remaining `shadow-*`
  references in client source belong to the ShadowCard command component)
- **Polling**: 2000ms bounded polling, starts only when control plane is open AND
  a run is selected; stops on unmount; no duplicate timer loops after rerender
- **Dedupe**: `eventId` (same eventId → one UI event; never by sequence/kind/text)
- **RunIsolation**: PASS — `lastSeqRef[runId]` keyed; task switch resets cursor;
  A→B→A selection shows no cross-run timeline mixing (simulated overlap-page
  test verified incremental merge retains old events, appends new in order,
  removes duplicates)

## Data Truthfulness

- **OBS-F9**: PASS — `RuntimeEvent.Sequence` and `ObservationSequence` remain
  independent domains; `obs=` omitted (never derived from `seq`) when
  ObservationSequence is unavailable
- **StableEventFormat**: PASS — `event: <eventId> [<kind>] seq=<n> ...`; optional
  `obs=` / `payload=` / `refs=` appear only when source data exists; nothing fabricated
- **PayloadFormatting**: PASS — bounded, deterministic, single-line parser-safe
- **Pagination**: PASS — `nextCursor` / `hasMore` truthfully represent the returned
  page from the wire response; formatter never invents pagination state
- **EmptyPage**: PASS — truthful empty state, no fake event, no Shadow fallback
- **UnknownRun**: PASS — deterministic `run_not_found` error, no synthetic timeline

## Zero Model

- **LlmCalls**: 0
- **VlmCalls**: 0
- Command parsing, event retrieval, pagination, client polling, formatting are
  all deterministic zero-model control-plane observability

## Authority / Freeze

- **NewWireMethods**: 0 (wire table remains the frozen 8 read-only methods;
  `run.events.after` existed before this change — this change only exposes it
  through DSH command/client surfaces)
- **RuntimeModified**: NO
- **DriverHostSemanticChanged**: NO
- **AuthorityDelta**: NONE (the command only calls `run.events.after`; no
  run.start/execute/dispatch/Tap/Scroll/SetSwitch/Agent control/Container
  mutation/GoalEvidence creation)

## Validation (fresh runs this review)

- Real pinned DSH host regression (real registry, all 6 commands register,
  representative read command executes): **8/8 PASS**
- Cross-process DriverHostPluginE2ETests + PluginIntegrationGuardTests +
  ArchitectureGuardTests: **23/23 PASS**
- Node suite: **105 passed / 1 known unrelated failure** (F16, see below)
- `dotnet build src/UniClaw.Runtime.sln`: **0 errors** (25 pre-existing XML doc warnings)
- `scripts/check-consistency.sh`: **ALL PASS**
- `openspec validate dsh-control-plane-event-stream --strict --no-interactive`: **PASS**

## F16 Truthful Record

- **F16Guard**: `FAIL_PRE_EXISTING_WORKTREE_INTERFERENCE`
- **F16AttributableToEventStream**: NO
- **Evidence**: the guard fails because unrelated pre-existing worktree residue
  `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` is modified (belongs to the
  separate `open-world-traversal-identity-safety` change, archived independently).
  Stash-verification: removing that file's interference makes F16 pass (3/3),
  proving the failure is unrelated. This change touches no Runtime file.
- The whole Node suite is **NOT** classified as fully green; it is exactly
  **105 passed / 1 known unrelated failure**.

## Live Proof

- **LiveControlPlaneProof**: `LIVE_DEPLOYMENT_PROOF_UNAVAILABLE`
- **EquivalentDurableRealChannelProof**: PASS
- **Reason**: the real pinned-host integration durably executes the complete
  command path (real registry → plugin → adapter → frozen wire → formatted result);
  browser-visible operator confirmation remains optional manual corroboration,
  not a graduation blocker. The deployed 3081 instance carries the new bundle and
  fixture logs recorded real `run.events.after` requests during review.

## Remaining Limitations (explicitly recorded)

- browser-visible timeline still awaits optional human corroboration (hard
  refresh 3081 → open control plane → select run → confirm event timeline renders)
- 2s polling, not push (event arrival latency ≤ 2s)
- single-page consumption (no paginated full-history fetch)
- stale test subtitle "five commands" (assertion itself is correct: 6 commands,
  count=6)
- unrelated OpenWorld.cs worktree interference (F16, pre-existing)

## Maturity Meaning

This maturity means: real Kernel RuntimeEvents → frozen DriverHost read surface
→ frozen `run.events.after` wire method → DSH native zero-model command → client
incremental cursor polling → human-readable real-time control-plane timeline is
independently integrated and regression-protected.

It does NOT mean: Kernel intelligence, TaskSpec execution, Adjudication, Advisory
cognition, new Runtime events, or new mutation controls.

## Next Buyer

`CONTROL_PLANE_SCENARIO_DEVICE_CATALOG_GAP` is the leading candidate per the
architecture baseline (control-plane order: 1. realtime event stream, 2.
scenario/device catalog, 3. Adjudication trace after IntelligenceSeam), but a
fresh buyer audit must be performed before any OpenSpec creation.
