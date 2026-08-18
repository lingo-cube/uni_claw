# Tasks: settings-full-tree-enumeration-integration

> Revised 2026-08-18 (BASELINE_REVISION): task order reorganized into five
> integration-pressure phases. Each phase uses the SAME first-failure
> classification (A–H); the FIRST real production failure in a phase stops the
> phase (fix at most one failure per run, then re-run).

## Failure classification (all phases)

- A `ROOT_EVIDENCE_GAP`
- B `SOURCE_AUTHORIZATION_GAP`
- C `RECURSIVE_COMPLETION_GAP`
- D `SETTINGS_DESTINATION_ALIAS_PRESSURE`
- E `SETTINGS_DYNAMIC_INVENTORY_PRESSURE`
- F `EXTERNAL_BOUNDARY_PRESSURE`
- G `DEPTH_BUDGET_PRESSURE`
- H `EXISTING_MECHANISM_DEFECT`

## PHASE 1 — SETTINGS_ROOT_REALITY_BASELINE

Only a real Settings Root reality baseline (real emulator + production
pipeline). NO recursion implementation.

- [ ] 1.1 Real `com.android.settings` foreground ownership verified
- [ ] 1.2 Root semantic identity established from the first real observation
      (structured-first, OCR fallback)
- [ ] 1.3 Root structured evidence captured (initial sources, per-source
      classification: AUTHORIZED_CHILD | UNAUTHORIZED | LOCAL_CONTROL |
      EXTERNAL_BOUNDARY | UNRESOLVED)
- [ ] 1.4 Real scrollability probe (below-fold sections discovered)
- [ ] 1.5 Interactive Unknown inventory recorded
- [ ] 1.6 Initial authorization surface recorded
- [ ] 1.7 Whether the graduated Runtime can prove Root Container inventory
      (ContainerComplete(Root)) — evidence only, no recursion

First production pressure -> STOP (classify A–H). Do not enter recursion
implementation.

## PHASE 2 — SINGLE RECURSIVE CHILD

- [ ] 2.1 Root -> ONE authorized child -> child Container completeness
      (one layer of recursive composition proven)
- [ ] 2.2 Child transition settle + contextual parent-return control reused

First failure -> STOP.

## PHASE 3 — GRANDCHILD + VERIFIED RETURN

- [ ] 3.1 Root -> Child -> Grandchild -> SubtreeComplete(Grandchild) ->
      verified return Child (real ≥3-level semantic recursion proven)
- [ ] 3.2 Verified parent return via fresh world evidence at each level

First failure -> STOP.

## PHASE 4 — SIBLING + SUBTREE LEDGER

- [ ] 4.1 Grandchild complete -> sibling continuation
- [ ] 4.2 Required/Completed child obligations recorded (AUTHORIZED_CHILD only)
- [ ] 4.3 VerifiedBoundaryDispositions recorded for verified EXTERNAL_BOUNDARY
      sources (source/provenance reference + verified boundary evidence +
      disposition; never silently dropped, never recursed)
- [ ] 4.4 SubtreeComplete bookkeeping per child/root (ledger records proven
      facts only; NOT a truth authority)

First failure -> STOP.

## PHASE 5 — SETTINGS-TREE-01 REAL CAPSTONE

Real Android Settings (NOT COMPOSE-05):

- [ ] 5.1 Root -> Child A -> Grandchild A1 (leaf) -> sibling A2 -> return Root
      -> sibling B -> recursively discharge all required obligations
- [ ] 5.2 SubtreeComplete(Root) proven
- [ ] 5.3 Fresh GoalEvidence / tree-completion evidence -> FullTreeComplete
      (SubtreeComplete(Root) proven AND fresh GoalEvidence true; GoalEvidence
      alone SHALL NOT infer SubtreeComplete(Root))
- [ ] 5.4 Deterministic prerequisite suites stay green + architecture guards

First failure -> STOP.

## Deferred / explicitly NOT PURCHASED

- Alias merging (`SETTINGS_DESTINATION_ALIAS_PRESSURE` -> STOP)
- Dynamic Settings inventory recovery (`SETTINGS_DYNAMIC_INVENTORY_PRESSURE` -> STOP)
- External-boundary recovery/traversal (record `VerifiedBoundaryDispositions`
  only — disposition bookkeeping is NOT external-boundary recovery)
- Persistent navigation graph
- Generic Android app traversal
- LLM/VLM navigation
- Generic popup recovery
- Treating COMPOSE-05 as full-tree evidence
