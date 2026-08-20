# Archived: settings-full-tree-enumeration-integration

**Decision: GRADUATED** — maturity `SETTINGS_TREE_CAPSTONE_PROVEN`

The open-world Runtime proves a real Android Settings tree under the purchased
SETTINGS-TREE-01 integration contract.

## Evidence summary

- Phase 1 PASS — SettingsRoot ContainerComplete=TRUE (16 sources, epoch FROZEN, 3/3)
- Phase 2 PASS — Root → Location; ContainerComplete(SettingsSubpage(Location))=TRUE
- Phase 3 PASS — Root → Location → Location services (distinct fresh identities);
  Grandchild ContainerComplete=TRUE; verified return PASS
- Phase 4 PASS — Battery sibling continuation; Required/Completed ledger;
  SubtreeComplete(tested parent)=TRUE
- Phase 5 PASS — SETTINGS-TREE-01 real capstone; 3/3 official real runs;
  FullTreeComplete(Root)=TRUE; STATE=Completed 3/3

- Deterministic: 1384/1384 (non-flaky)
- ArchitectureGuards: 17/17
- Consistency: PASS
- AuthorityDelta: NONE

Known intermittent host timing flake (`SameDeviceExclusivity_…`): isolated PASS,
unrelated; recorded, not silently counted green.

## ClaimBoundary (exact, not expanded)

OPEN-WORLD RUNTIME CAN PROVE A REAL SETTINGS TREE under the purchased
SETTINGS-TREE-01 INTEGRATION CONTRACT, including: real Root inventory
completeness, recursive child traversal, genuine ≥3 semantic levels, distinct
destination identity, verified parent return, sibling continuation,
authorized-child completion ledger, SubtreeComplete, fresh-evidence
FullTreeComplete.

Explicitly NOT graduated: universal/any-app traversal, every Settings page,
alias merging, persistent navigation graph, dynamic inventory recovery, generic
popup recovery, external-boundary recovery, missing-page-title-role recovery,
More-options affordance resolution, LLM/VLM-assisted traversal.

## Known limitations (see KNOWN_LIMITATIONS.md)

Environment-structural knowledge present in production code (page-title-role
`collapsing_toolbar` resource-id; `Navigate up` action-role label) is
version/locale-dependent and recorded as known-limitation items P-1/P-2.
Candidate optimizations and the not-purchased items are documented.

## Artifacts

- proposal.md / design.md / spec.md / tasks.md / KNOWN_LIMITATIONS.md
