> **Gate state**: PROPOSAL (design/spec only). Implementation requires a separate
> explicit Human Gate. Phase 2.6 remains STOPPED. Scope = the existing post-scroll
> buyer ONLY.

## D. Design / Spec (this stage)

- [x] D.1 Source-verify the existing gate (`ConfirmScrollStabilityAsync` :2200 /
  `IsViewportStable` :2274 / `NavigationRowCenters` :2290 — `TryAdd` multiplicity
  collapse confirmed).
- [x] D.2 Freeze the 7 capability principles (proposal; spec requirement 1).
- [x] D.3 Freeze the 8-scenario matrix incl. RED basis (Scenarios 1/5/7) and the
  Scenario-6 design choice for reviewer adjudication (design D2–D4; spec scenarios).
- [x] D.4 Owner analysis: repair-in-place, RuntimeAgent observation-acceptance seam,
  no new owner / no parallel loop (design D1; spec requirement 2).
- [x] D.5 Lifecycle dispositions applied: exhaustion-confirmation WITHDRAWN;
  unique-corroboration ABANDONED_AS_PRIMARY_FIX; STOP-3 erratum #2 appended.
- [x] D.6 Strict validation + mapping check → stop, await Implementation Human Gate.
- [x] D.7 Gate amendment (2026-08-28): Scenario 6 frozen GATE_LEVEL_NON_CONFIRMABILITY;
  RED set reclassified (1/2/5/6/7); Principle 8 Terminal Supervisory Handoff +
  Scenarios 9-12 added; exhaustion-confirmation change ARCHIVED; implementation
  authorization auto-activated by the amending Gate.

## I. Implementation (AUTHORIZED — Gate amendment 2026-08-28 auto-activated)

- [x] I.1 RED tests first (Gate-frozen RED set: Scenarios 1/2/5/6/7 — fail on the
  pre-repair implementation via TryAdd multiplicity loss / unordered-comparison
  masking / duplicate-pair false confirmation; controls 3/4/8 green).
- [x] I.2 Multiplicity- and order-preserving stability evidence (ordered occurrence
  list; per-index signature + drift comparison; in-frame duplicate ⇒ non-confirmable).
  (I.2 and I.3 were implemented in one worker pass.)
- [x] I.3 Confirmation logic + additive trace + Terminal Supervisory Handoff
  (per-attempt count/multiplicity/drift/reason; exhaustion reason names quiescence
  admission budget exhausted + last seq + attempts + classification; existing
  RunFailed Surface B only; zero new EventKind/wire/DTO/callback/mid-Run transport).
- [x] I.4 Verification: Scenarios 1/2/5/6/7 RED→GREEN; 3/4/8 controls stay GREEN;
  9/10/11/12 pass; ScrollStability/normalization/traversal/open-world suites; Phase 2 /
  2.5 deterministic regression; architecture guards; consistency; `git diff --check`;
  zero RuntimeEventKind / wire DTO / DriverHost-method changes.
- [x] I.5 Independent review (fresh verifier: repair in the existing observation-
  admission owner; no multiplicity loss; no unstable-frame admission; no action
  re-dispatch; RunFailed via existing Surface B; UniAgent read-only; AuthorityDelta
  NONE; Phase 2.6 not auto-resumed). Bounded remediation (WI-QA-4) noted below.
- [x] I.6 Bounded remediation (WI-QA-4): freshness sequence-number check;
  factual failure classification (re-observe failed / left container — no
  ReorderOrSignatureMismatch masquerade); backward revisit terminal parity
  (consume _lastStabilityExhaustionDetail like the forward path); Surface B real
  projection verification (RuntimeEventProjector.Project — exactly one RunFailed +
  reason detail + no RunCompleted + idempotent); lifecycle sync. Done.

## R. Downstream (after graduation)

- [ ] R.1 Phase 2.6 reentry campaign from Stage A under the standing Gate #2
  conditions (separate authorization, unchanged).
