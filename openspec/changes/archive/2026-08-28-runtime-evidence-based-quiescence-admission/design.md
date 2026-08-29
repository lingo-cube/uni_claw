## Context

Authority: Runtime Architecture Contract I-1..I-14; the existing post-scroll stability
gate (Agent.OpenWorld.cs: `ConfirmScrollStabilityAsync` :2200, `IsViewportStable`
:2274, `NavigationRowCenters` :2290, called at :1112/:796); the STOP-3 evidence chain
(transient artifacts die ≤1.7s; settled frames clean; run-6 obs-13/14 duplicate pair
admitted as stable); Human Gate `..._QUIESCENCE_ADMISSION_OPENSPEC_PROPOSAL`
capability semantics (verbatim principles 1–7) and scenario matrix (1–8).

## Evidence → FDP → Owner (locked by the Gate, source-verified)

- **Evidence**: run-6 timeline (duplicate pair in the confirmed settle pair);
  STOP-3 E0–E3 (settled clean; transients ≤1.7s); source: `NavigationRowCenters`
  builds `Dictionary<string,float>` with `TryAdd` — duplicate signatures collapse;
  `IsViewportStable` compares unordered distinct-signature maps + center drift.
- **FDP**: stability comparison evidence drops occurrence multiplicity, so an
  in-frame-ambiguous observation can be CONFIRMED as the stable decision basis.
- **Owner**: RuntimeAgent's observation-acceptance seam (the existing gate). No new
  owner; no cross-layer contract.

## Decisions

### D1 — Repair the existing gate; define the general principle, buy only this buyer

The capability semantics (7 principles) are frozen as the reusable internal principle,
but the ONLY implementation scope is the existing post-scroll gate. Future buyers
(page transitions, popups, expand/collapse, loading, relayout, recovery restore) are
explicitly out of scope and MUST NOT be wired opportunistically.

### D2 — Multiplicity- and order-preserving stability evidence

Replace the unordered `Dictionary<signature, centerY>` comparison input with an
ordered, multiplicity-preserving occurrence list per frame: for each
`OccurrencesOf` entry (unchanged eligibility filter), record
`(signature, centerY)` IN OCCURRENCE ORDER. Stability comparison requires, across two
consecutive fresh frames: equal occurrence COUNT; equal per-index signature (ordered
identity — a reorder is instability); per-index center drift ≤ the existing epsilon
(deterministic correspondence = same index, no best-match search — ambiguity cannot be
matched away); and NO duplicate signature within either frame (an in-frame duplicate
makes the frame non-confirmable: it counts as "not yet stable", never as stable).

### D3 — Ambiguity-aware admission, not ambiguity-resolution

The gate does not resolve duplicates (no dedupe, no topmost-wins, no
DistinctBy — all forbidden). Ambiguous frames may be observed as pending evidence but
never become the confirmed decision frame. Persistent ambiguity → budget exhaustion →
Unresolved (no frame admitted, no action, no redispatch).

### D4 — Existing semantics preserved

Normalizer/SourceIdentity/ordered-overlap rules untouched: a CONFIRMED frame that
genuinely contains two identical real rows (Scenario 6) still reaches the normalizer
and fails closed THERE — the stability gate is not a normalizer substitute. Wait: per
D2, in-frame duplicates make a frame non-confirmable → Scenario 6's persistent real
duplicates now fail at the GATE (budget exhaustion) instead of the normalizer — both
fail-closed, same outcome class, earlier and cheaper; the scenario's requirement
("must preserve both and follow existing normalization fail-closed semantics") is
satisfied in outcome (fail-closed, no relaxation, no identity change) — the design
chooses gate-level non-confirmability as the honest reading of principle 3 (ambiguous
frames never become decision bases), recorded explicitly for reviewer adjudication.

### D5 — Fresh/latest-frame/budget/trace

Unchanged from the existing gate (already correct): strictly fresh observations;
only the final confirmed frame is returned/admitted; bounded attempts; full trace.
Add to the trace: occurrence count, multiplicity summary (duplicate signatures
present), per-index drift, and the specific non-stability reason (count mismatch /
reorder / drift / in-frame ambiguity).

## Authority proof

| Forbidden edge | Guard |
|---|---|
| Second parallel settle loop | single gate, same call sites, repair-only |
| Sleep as correctness | convergence-evidence-only; no time-based pass condition |
| Multiplicity loss reintroduced | ordered occurrence-list evidence; scenario tests |
| Ambiguous frame admitted | principle 3 + Scenario 1/2/5/7 tests |
| Stability gate becomes normalizer authority | normalizer untouched; Scenario 6 outcome remains fail-closed |
| New owners / wire / API | RuntimeAgent seam only; additive trace fields |
| Other buyers wired | scope forbids; tasks gate the single buyer |

## Spec → FDP → Owner → Scenario → Test mapping

| Scenario (Gate 1–8) | Requirement in spec | Test (capability, deterministic) |
|---|---|---|
| 1 dup artifacts then clean | Ambiguity-Aware Admission | dup frames pending → clean pair confirms → last clean admitted |
| 2 persistent dups | Bounded Fail-Closed | budget exhausted; nothing admitted; no action |
| 3 moving then stops | Evidence-Based Convergence (drift) | A/B pending; final C admitted |
| 4 normal stable list | Minimal confirmation | confirms at min attempts; no unbounded wait |
| 5 Item×2 → Item×1 | Multiplicity Preservation | count change = unstable (no set-equality pass) |
| 6 persistent real duplicate rows | GATE_LEVEL_NON_CONFIRMABILITY (D4) | persistent dup → budget exhausted → fail-closed; nothing admitted |
| 7 reorder | Ordered correspondence | per-index signature mismatch = unstable |
| 8 left container | Existing page/foreground sanity | fail-closed; new page never admitted as scroll result |
| 9 exhaustion → terminal report | Terminal Supervisory Handoff (D6) | budget exhausted → RunFailed via existing Surface B; reason carries attempts/last-seq/classification |
| 10 UniAgent reads, cannot intervene | Terminal Supervisory Handoff | terminal read distinguishes exhaustion; no continuation/state mutation |
| 11 normal stability → no fallback | Minimal confirmation | stable confirm; no RunFailed; loop continues |
| 12 projection unavailability | Idempotent terminal fact | no reader → fact unchanged; idempotent re-read |

RED/GREEN (Gate-frozen classification): Scenarios 1/2/5/6/7 are RED against today's gate
(multiplicity collapse / unordered map mask them) and GREEN after the repair — the
deterministic RED basis the implementation gate requires. Scenario 6 records the D4
outcome-choice for reviewer adjudication.

## Risks / Trade-offs

- [Stricter gate rejects previously-passing sequences] — only sequences containing
  ambiguity/multiplicity/reorder instability, which downstream could not consume
  anyway (run-6 class); regression suites confirm.
- [D4 gate-level vs normalizer-level failure for real duplicates] — explicit design
  choice recorded for the implementation gate's reviewer.

## Stop conditions

Any pressure to: dedupe/merge occurrences, add time-based pass, touch the normalizer,
relax identity, wire other buyers, or admit an ambiguous frame → STOP → Human Gate.


### D6 — Terminal Supervisory Handoff (Principle 8, frozen by the Gate)

Budget exhaustion keeps every local safety property (fail-closed, no provisional
admission, no redispatch, no fabricated completion) and reports upward through the
EXISTING terminal chain only: `ConfirmScrollStabilityAsync` → null →
`OpenWorldViewportOutcome.Unresolved` → RunFailed → existing
`RuntimeEventKind.RunFailed` + `RunFailedPayload.Reason` + existing
trace/snapshot/evidence projections (Surface B). The reason string carries:
"quiescence admission budget exhausted", last observation sequence, attempt count, and
the final failure classification (duplicate ambiguity / multiplicity mismatch /
reorder / position drift / left container); the trace already records per-attempt
detail (this amendment adds count/multiplicity/drift/reason fields per attempt).
FORBIDDEN: new DriverHost method, wire DTO, RuntimeEventKind, callback, mid-Run
escalation transport, pause/resume, in-Run UniAgent guidance, auto-continuation Run,
auto re-dispatch. UniAgent consumes the terminal result only (read-only; may form
supervisory judgments AFTER the Run ends). A future non-terminal supervisory
escalation contract requires its own OpenSpec + Human Gate.
