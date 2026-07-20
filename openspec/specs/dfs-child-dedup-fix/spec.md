## ADDED Requirements (2026-07-20 — baseline-completion-fix)

### Requirement: DynamicChildManager dedup key SHALL use fingerprint-based scope (D-89 REVERTED)

`DynamicChildManager._generatedPairs` dedup key SHALL remain as `(fingerprint.ToString(), childName)` — the `(parentNodeId, childName)` scope was attempted and REVERTED (D-89) because it creates infinite nesting for non-navigable containers on the same page. The fingerprint-based scope correctly prevents circular nesting by deduping all same-childName generation on the same page. Non-navigable containers (buttons that don't lead to sub-pages) correctly get 0 DynamicMatch children under this scope, and are treated as leaves with Pop-only (D-90 parent-frame fingerprint comparison).

#### Scenario: Same-page dedup prevents non-navigable containers from generating circular children
- **WHEN** a menu_container child (e.g., `dyn_menu_container_HomeNetwork_root`) is on the same page as its parent (same fingerprint)
- **AND** the parent has already generated DynamicMatch children with the same childNames
- **THEN** the menu_container's Generate SHALL produce 0 children (all childNames already in `_generatedPairs` for this fingerprint)
- **AND** the menu_container SHALL be treated as a leaf node (Pop-only via D-90)

#### Scenario: Different pages dedup independently (different fingerprints)
- **WHEN** two parent nodes are on different pages (different fingerprints, e.g., home vs wifi)
- **THEN** the same childName (e.g., "switch_leaf_ON") SHALL be allowed for both pages
- **AND** no cross-page dedup collision SHALL occur (fingerprint differs)

#### Scenario: Invalidate preserves dedup across re-generation
- **WHEN** `Invalidate(nodeId)` removes the `_dynamicChildren` cache entry for scroll invalidation
- **THEN** `_generatedPairs` dedup entries SHALL persist (existing behavior: D-3)
- **AND** after re-generation, previously generated childNames for that page SHALL still be deduped
