# Observability Emission Expansion — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_OBSERVABILITY_EMISSION_EXPANSION` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-observability-emission-expansion/`
> Authority: Runtime Architecture Contract I-1..I-14 and Architecture v1 remain the governing baselines; observability remains a fail-open, non-semantic, structural-only channel.

## 1. Buyer and exact claim boundary

**Buyer:** UniClaw Runtime observable correlation + concurrent-run isolation + exercised-boundary provenance.

This receipt claims only that:

1. the four deferred instrumentation boundaries are activated at their now-active seams — caller-owned `runtime.invocation` root (DriverHost, opened synchronously in `StartRun`/`StartStrategyRun` before scheduling), `recovery.attempt` (Recovery mechanism seam, dispatch-closure outcome only), `capability.invocation` (Agent selection/execution seam), `intent.execution` (open-world Intent shared seam) — with unexercised boundaries never fabricated;
2. per-run recorder capture is scoped to the run's W3C trace id: foreign-trace activities are skipped with a Harness diagnostic, caller-supplied `TraceRun.TraceId` correlation is preserved separately, and the projected `CorrelationId` is non-null for runs that recorded evidence;
3. structured point-event emission exists (`AddEvent` with attributes, `decision.*` vocabulary) and recorded events carry their own monotonic offset and attributes;
4. the Agent-owned semantic journal type is renamed `TraceEvent` → `DecisionRecord` (vocabulary-only; Trace = protocol chain, Event = point occurrence attached to a trace, DecisionRecord = Agent decision journal; wire/schema/persisted behavior unchanged; frozen wire/DTO bytes untouched);
5. conformance requires exercised active boundaries and rejects fabricated unexercised boundaries.

No claim is made for: OTLP export, sampling/retention, Links/Ref taxonomy, `TraceRun` schema v2, semantic-outcome use of observability (outcomes remain structural; I-3/I-8 decision authority unchanged), or any change to Agent / Container / Traversal / Environment / Recovery / Capability / Planning / Harness ownership.

## 2. Validation evidence

- `dotnet build src/UniClaw.Runtime.sln`: **0 errors**.
- Full solution suite: **2324/2329 passed ×2 consecutive runs** (5 failures are pre-existing environment-only: RealEmulator, RealDevice, Vision-host ×3 — present in every run before and after this change; zero observability-trace failures).
- New conformance: golden exercised-boundary presence + unexercised-boundary absence; action-exercising Traversal; recovery seam; coordinator root + `TraceId` via the public read surface; concurrent-recorder run-scoped isolation; event timing/attributes.
- Frozen wire/DTO byte-identity guard (`HarnessSourceShapeGuardTests`): **5/5 PASS** (the DecisionRecord rename was reverted on the frozen surface, which retains historical prose).
- Strict OpenSpec validation: **PASS** (`openspec validate observability-emission-expansion`).
- Consistency: **PASS** (`scripts/check-consistency.sh` ALL PASS; projections regenerated). Formatting: **PASS** (`git diff --check`).

## 3. Scenario receipts and falsifiers

| Falsifier | Result |
|---|---|
| Parallel test/pre-claim interference on the process-global listener could pre-claim a recorder's trace scope (observed as flaky coordinator-root and recovery tests under full parallel load) | **Not falsified (fixed)**: root span moved to synchronous `StartRun` open (adjacent to recorder creation, before scheduling) and all recorder-emitter test classes serialized in one non-parallel collection; two full-suite reruns identical. |
| Unexercised deferred boundaries are fabricated to satisfy a fixed span shape | **Not falsified**: golden run asserts absence of root/recovery/capability/intent spans when not exercised. |
| Frozen wire/DTO byte identity (HarnessSourceShapeGuardTests SHA-256) | **Not falsified**: 5/5 PASS; rename kept off the frozen surface. |
| Observability outcome becomes semantic evidence (I-3/I-8 authority change) | **Not falsified**: all new emissions are structural outcomes; decision authority unchanged; guards green. |
| The rename alters wire/persisted behavior | **Not falsified**: type-rename only; property names, JSON surface, replay `TraceEventAsset` and validator behavior unchanged. |
| Interference from the concurrently-edited `SourceEquivalenceNormalizer` (logical-order projection) | **Not falsified (external)**: three Evidence-suite failures appeared only after that external working-tree change landed, reproduce in isolation, and share zero code paths with this change; they resolved when the external change settled. |

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- OTLP export / exporters, sampling / retention policies, Links / Ref (EvidenceRef / FactRef / OccurrenceRef / ArtifactRef) taxonomy.
- `TraceRun` schema v2 / typed attributes / absolute timestamps.
- Frozen wire/DTO surface (`RunSnapshot`, `RuntimeEventEnvelope`, `RuntimeEventKind`) vocabulary migration.
- `AgentStateSnapshot.Trace` property rename (wire-adjacent surface).
- Double-track bridge: linking `DecisionRecord` journal entries to spans/events (FDP walk along one chain).
- GAP-07 vocabulary convergence beyond `decision.*`.

## 5. Final conclusion

**GRADUATED.** The activated emission boundaries, run-scoped capture, structured decision events, and DecisionRecord vocabulary are human-authorized, evidence-verified, and archived; deferred scope remains unauthorized for separate gate.