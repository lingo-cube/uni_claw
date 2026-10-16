# TRAJECTORY-001 — Plan

> Change State: `changes/TRAJECTORY-001/state.md`
> Design input: `docs/analysis/agt019-followup-optimization-and-research-2026-10-17.md` v3.4
> Execution order: protocol contract → owner event/coverage → Kernel trigger → real transport/evidence

## Slice A — Protocol contract and generated artifacts

**Owner:** Leader + protocol Worker

- Add optional push-only `heartbeat`, `casePacket`, and `contextReferenceIndex`
  projections to `AgentDecisionContext`.
- Add typed `AgentDecision.Skip` with `DecisionId`, target, reason and
  evidence references; zero Effect is enforced by Kernel.
- Add public records to the generated schema authority path:
  `.NET records → ProductProtocolSchema → JSON Schema/d.ts → DSH codec →
  handshake hash`.
- Update `AuthorityTypes`, strict decision converter/correlation checks,
  runtime public-surface allowlist and protocol round-trip/schema tests.
- Do not add Product `ConsultationId` or transport-correlation fields.

**Acceptance:** new fields and Skip round-trip; unknown/extra fields fail closed;
DecisionId mismatch rejects; generated artifacts match runtime schema/hash;
existing four/five decision paths remain green.

## Slice B — Owner Event Stream and Coverage Mark

**Owner:** Coverage Worker

- Formalize existing ledger observation, popup, receipt/verification projections
  with owner-local sequence and typed event records.
- Add `TargetCoverageMarked` for ReachedOverlay/RouteEntered/Skipped;
  Pending remains absence of a mark.
- Add Agent Skip ledger API and immutable event evidence.
- Keep RuntimeRunEvent as the outer Runtime lifecycle index; do not merge Owner
  event sequences into it.
- Keep structural skips separate from Agent Skip and expose both in reports.

**Acceptance:** replayable event artifact; Agent Skip is zero Effect, idempotent,
separate from structural skip; reached/skipped/structural counts are distinct;
coverage report does not claim Agent coverage from structural exclusions alone.

## Slice C — Kernel event-triggered consultation

**Owner:** Trigger Worker + Leader

- Add the smallest typed interruption/event seam into KernelRunDriver's existing
  NeedDecision/P25 path; never call ConsultAgent twice from Director.
- Freeze one event cutoff and build Heartbeat/Case Packet projections before the
  next Effect.
- Mint new DecisionId through the existing Kernel journal path for each new
  semantic consultation; transport retry replays the existing journal entry.
- Implement signalKey/rearm/coalescing and preserve HardCap/ExecutionContract/
  RenewalWindow precedence.
- Keep existing E2/E3/Defer/restart behavior unchanged.

**Acceptance:** RUN-006 replay triggers one semantic consultation per frozen signal;
new evidence re-arms; no-effect blocked/replan path works; same transport retry
does not consume another consultation; no duplicate Effect.

## Slice D — H7 and real-device evidence

**Owner:** Leader

- Run a post-dc43ee0f real DSH request and capture the actual outbound/ingress
  context.
- Verify `localView`, Heartbeat and Case Packet survive the bridge.
- Regenerate/synchronize plugin schema artifacts and verify reported hash equals
  `ProductProtocolSchema.Current`.
- Repair only the proven bridge/schema seam; preserve evidence of the old
  pre-fix 97/97 result.

**Acceptance:** real request receives the fields, handshake accepts the current
schema, audit records the post-decoration context, and the device run produces
replayable evidence.

## Slice E — M6 v2 follow-up (deferred)

Agent-driven Context Pull, full resume semantics and trust-ladder orchestration
remain outside this Change. The current Change only ships the push-only reference
catalog and its capability/epistemic metadata.

## Verification order

1. Targeted protocol and ledger tests.
2. Kernel/Host/DSH integration tests and generated artifact checks.
3. Full solution tests and scenario certification.
4. H7 real-device run and bridge audit.
5. Review, then Verify with method/expected/actual/evidence in state.md.
