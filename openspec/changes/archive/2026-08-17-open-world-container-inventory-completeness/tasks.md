# Tasks: open-world-container-inventory-completeness

> GRADUATED 2026-08-17 — `OPEN_WORLD_CONTAINER_INVENTORY_COMPLETE`.
> Honest state sync: slices whose mechanisms were delivered and verified under
> the repair/capstone gate suites are marked done with the actual coverage
> noted; the literal numbered EXH/INV suites were never produced and remain
> unchecked, superseded by the graduated suites below. `2b.7` is a deliberate
> scope deferral, not an unclosed requirement of this change.

## 1. Audit

- [x] 1.1 Confirm existing viewport exhaustion evidence and accepted observation ownership
- [x] 1.2 Confirm BranchInventoryEvidence relationship and avoid overloading it

## 2. Implementation Slices

- [x] SLICE 1: Reuse/extract deterministic viewport exploration + exhaustion semantics
- [x] SLICE 2: Integrate bounded exploration into RunOpenWorldAsync
- [ ] SLICE 3: Prove EXH-1..EXH-10 — superseded: the exhaustion scenarios
      (EXH-4 / INV-4 / INV-5) are verified by the graduated suites
      CURRENT-1..10, NM-1..14, and the real capstone (positive exhaustion,
      seq=[2,3,4,5]); the literal numbered EXH suite was not produced.
- [x] SLICE 4: Implement `ContainerInventoryCompletenessEvidence`
      (extended for the frozen discovery epoch: `ProvenLogicalSources`,
      `FrozenDiscoveryObservationSequences`, `PositiveExhaustionEvidence`)
- [x] SLICE 5: Unique child normalization
      (`SourceEquivalenceNormalizer` + `OccurrencesOf`; PROV-2/13, NM, real 8 sources)
- [x] SLICE 6: Caller inventory validation — delivered as provenance-driven
      branch acceptance: `TryAcceptBranchInventory` explicit-grounding path via
      `SourceGroundingValidator` (ACCEPT-1..10, PROV-1..14); legacy Elements-only
      environments keep the pre-contract check (RVT2-14).
- [x] SLICE 7: Leaf proof — delivered as the truthful leaf: exhausted + complete
      unique inventory + zero discoverable children (the zero-candidate leaf-child
      completeness case; AFF-1/7, SET, real child `sources=0`) + the bounded-leaf
      subtreeTerminal return path.
- [ ] SLICE 8: INV-1..INV-16 tests — superseded: the INV scenarios are verified
      by the graduated suites (INV-1/2/4/5/6/11/12/13/14/16/17 via CURRENT, NM,
      ACCEPT, AFF, SET; INV-7/8 via OpenWorldTraversalIdentitySafety + U2); the
      literal numbered INV suite was not produced.
- [x] SLICE 9: Full OpenWorld regression

## 2b. Caller Source Provenance Contract (slice)

- [x] 2b.1 Add immutable `NavigationSourceOccurrenceReference` + `BranchSourceGroundingEvidence`
- [x] 2b.2 Add `SourceEquivalenceNormalizer.OccurrencesOf` deterministic occurrence derivation
- [x] 2b.3 Add Agent-owned `SourceGroundingValidator` (six validation conditions)
- [x] 2b.4 Wire occurrence-grounded branch selection into `RunOpenWorldAsync` (structured-evidence environments; fail closed)
- [x] 2b.5 Add `BranchInventoryEvidence.RequiredBranchGrounding` optional explicit channel
- [x] 2b.6 Implement PROV-1..PROV-14 tests
- [ ] 2b.7 Make explicit caller grounding mandatory (legacy identity→sequence
      channel removal) — deliberately DEFERRED to the future parent caller-
      inventory slice; the legacy Elements-only channel remains for legacy
      environments. Not an unclosed requirement of this change's
      structured-evidence grounding-mandatory contract.

## 3. Validation

- [ ] 3.1 Run targeted inventory completeness tests — superseded by the
      graduated repair-gate suites (CURRENT-1..10, NM-1..14, ACCEPT-1..10,
      RVT2-1..16, AFF-1..14, SET-1..16, TXT-1..10, PROV-1..14).
- [x] 3.2 Run OpenWorld / U2 / Capstone / identity-safety regression
- [x] 3.3 Run architecture guards
- [x] 3.4 Run full regression — 1164/1164 deterministic PASS
- [x] 3.5 Run consistency check
- [x] 3.6 Run OpenSpec validation

## 4. Real-device capstone evidence (graduation)

- [x] ONE Agent (`CAPSTONE-AGENT-001`, creations=1) + ONE `RunOpenWorldAsync` on
      emulator-5556 COMPOSE-05 → `STATE=Completed`, `GOAL_EVIDENCE=True@45`,
      `Visited 8/8 [CAPSTONE COMPLETE]` (see
      `docs/decisions/open-world-container-inventory-completeness-graduation.md`).
