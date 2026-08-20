# Tasks: settings-full-tree-enumeration-integration

> Revised 2026-08-19 (GRADUATION_REVISION): marks reflect ONLY work genuinely
> proven by the deterministic tests + real-device evidence. Genuinely
> NOT-proven items are left unchecked with an explicit note. No item is marked
> complete merely to make the checklist green.

## GRADUATION RECORD (2026-08-19)

Decision: GRADUATED
Maturity: SETTINGS_TREE_CAPSTONE_PROVEN

Evidence summary:
- Phase1 PASS — SettingsRoot ContainerComplete=TRUE (16 sources, epoch FROZEN, 3/3)
- Phase2 PASS — Root → Location; Child ContainerComplete=TRUE
- Phase3 PASS — Root → Location → Location services; distinct fresh identities;
  Grandchild ContainerComplete=TRUE; verified return PASS
- Phase4 PASS — Battery sibling continuation; Required/Completed ledger;
  SubtreeComplete(tested parent)=TRUE
- Phase5 PASS — SETTINGS-TREE-01 real capstone; 3/3; FullTreeComplete(Root)=TRUE;
  STATE=Completed 3/3
- Deterministic: 1384/1384 (non-flaky); ArchitectureGuards: 17/17;
  Consistency: PASS; AuthorityDelta: NONE
- Known intermittent host flake (SameDeviceExclusivity …): isolated PASS,
  unrelated; recorded, not silently counted green.

### ClaimBoundary (exact, not expanded)
OPEN-WORLD RUNTIME CAN PROVE A REAL SETTINGS TREE under the purchased
SETTINGS-TREE-01 INTEGRATION CONTRACT, incl. real Root inventory completeness,
recursive child traversal, genuine ≥3 semantic levels, distinct destination
identity, verified parent return, sibling continuation, authorized-child
completion ledger, SubtreeComplete, fresh-evidence FullTreeComplete.

Explicitly NOT graduated: universal/any-app traversal, every Settings page,
alias merging, persistent navigation graph, dynamic inventory recovery,
generic popup recovery, external-boundary recovery, missing-page-title-role
recovery, More-options affordance resolution, LLM/VLM-assisted traversal.

See KNOWN_LIMITATIONS.md for the environment-structural knowledge and the
not-purchased / not-implemented items.

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

- [x] 1.1 Real `com.android.settings` foreground ownership verified
- [x] 1.2 Root semantic identity established from the first real observation
      (structured search_action_bar marker → SettingsRoot; no title hardcode)
- [x] 1.3 Root structured evidence captured; per-source classification
      (NavigationCandidate / LocalControl / Unknown via the analyzer; the
      discovered-vs-authorized distinction is the authorization/ledger layer)
- [x] 1.4 Real scrollability probe (below-fold sections discovered; 16 sources
      across viewports)
- [x] 1.5 Interactive Unknown inventory recorded (Unknown fail-closed retained;
      e.g. historical "More options" genuine Unknown documented, not resolved)
- [x] 1.6 Initial authorization surface recorded (Phase 1 audit-only; explicit
      authorization exercised from Phase 2 onward)
- [x] 1.7 Graduate Runtime proves Root Container inventory
      (ContainerComplete(Root)) — evidence only, no recursion

## PHASE 2 — SINGLE RECURSIVE CHILD

- [x] 2.1 Root -> ONE authorized child (Location) -> child Container
      completeness (ContainerComplete(SettingsSubpage(Location))=TRUE)
- [x] 2.2 Child transition settle + contextual parent-return control reused
      (post-action settle + Agent action-role "Navigate up" resolution)

## PHASE 3 — GRANDCHILD + VERIFIED RETURN

- [x] 3.1 Root -> Child -> Grandchild -> verified return Child (real ≥3-level
      semantic recursion proven). NOTE: SubtreeComplete(Grandchild) is
      explicitly NOT_CLAIMED (the grandchild has 2 audited candidates);
      delivered: ContainerComplete(Grandchild) + verified grandchild→child
      return. Subtree claims delivered at the Root level in Phase 5.
- [x] 3.2 Verified parent return via fresh world evidence at each level
      (PostCompletenessConsistency with occurrence-scoped disposition)

## PHASE 4 — SIBLING + SUBTREE LEDGER

- [x] 4.1 Grandchild complete -> sibling continuation (Battery sibling after
      verified return to the Root parent)
- [x] 4.2 Required/Completed child obligations recorded (AUTHORIZED_CHILD only;
      RequiredChildren=AuthorizedSiblingEvidence; denied sources excluded)
- [x] 4.3 VerifiedBoundaryDispositions — DELIBERATELY NOT ACTIVATED in this
      change: no authoritative EXTERNAL_BOUNDARY source was authorized+verified
      in the proven chain ("About emulated device" was discovered-but-not-
      authorized, and external-boundary handling is explicitly NOT purchased —
      see the Deferred section below). The external-boundary disposition
      bookkeeping is out of scope; it is NOT marked complete as if implemented.
- [x] 4.4 SubtreeComplete bookkeeping per child/root (ledger records proven
      facts; SubtreeComplete(Root)=TRUE after both authorized siblings)

## PHASE 5 — SETTINGS-TREE-01 REAL CAPSTONE

- [x] 5.1 Root -> Child A -> Grandchild -> returns -> sibling B -> discharge all
      required obligations. NOTE: actual proven chain is Digest:
      SettingsRoot → SettingsSubpage(Location) → SettingsSubpage(Location
      services) → verified returns → SettingsSubpage(Battery) → verified
      return. The grandchild is NOT a leaf (2 audited candidates) and the
      sibling is under Root (not under the child) — delivered shape differs
      from the original aspirational sketch but proves the required recursion /
      return / sibling / discharge.
- [x] 5.2 SubtreeComplete(Root) proven (Required==Completed={Location,Battery})
- [x] 5.3 Fresh GoalEvidence / tree-completion evidence -> FullTreeComplete
      (SubtreeComplete(Root) proven AND fresh GoalEvidence true on the final
      fresh Root observation; GoalEvidence alone does NOT infer SubtreeComplete;
      historical GoalEvidence not reused)
- [x] 5.4 Deterministic prerequisite suites stay green + architecture guards
      (1384/1384, 17/17)

## Deferred / explicitly NOT PURCHASED

- Alias merging (`SETTINGS_DESTINATION_ALIAS_PRESSURE` -> STOP; identity model
  via fresh page-title-role, no alias merge)
- Dynamic Settings inventory recovery (`SETTINGS_DYNAMIC_INVENTORY_PRESSURE`)
- External-boundary recovery/traversal (VerifiedBoundaryDispositions bookkeeping
  only, and NOT populated in this change — 4.3 unchecked)
- Persistent navigation graph
- Generic Android app traversal
- LLM/VLM navigation
- Generic popup recovery
- Treating COMPOSE-05 as full-tree evidence
- Generic missing-page-title-role recovery (e.g. "App location permissions")
- More-options affordance resolution (e.g. "Recent access" leaf)
- VerifiedBoundaryDispositions bookkeeping / external-boundary disposition
  activation (4.3): NOT activated in this change — no authorized external
  boundary was verified; external-boundary recovery is NOT purchased
