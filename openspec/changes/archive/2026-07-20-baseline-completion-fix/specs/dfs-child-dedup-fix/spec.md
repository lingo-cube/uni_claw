## ADDED Requirements

### Requirement: DynamicChildManager dedup key SHALL scope per-parent, not per-page-fingerprint

`DynamicChildManager._generatedPairs` dedup key SHALL change from `(fingerprint.ToString(), childName)` to `(parentNodeId, childName)`. This allows different parent nodes on the same page (e.g., a wifi sub-frame and a menu_container child) to independently generate DynamicMatch children from the same page elements without dedup collision. The `childName` format (`"{rule.ChildTemplate}_{result.MatchedItem.Text ?? "item"}"` ) remains unchanged.

#### Scenario: Nested menu_container generates its own DynamicMatch children on same page
- **WHEN** a menu_container child (e.g., `dyn_menu_container_HomeNetwork_root`) is pushed onto the stack with DynamicMatch ChildrenStrategy
- **AND** the current page is the same page where the parent (wifi sub-frame) generated its children (same fingerprint)
- **THEN** the menu_container's DynamicMatch Generate SHALL produce child nodes for matching page elements (switch_leaf, menu_container) without dedup collision with the parent's previously generated children
- **AND** the NodeId format `dyn_{rule.ChildTemplate}_{itemText}_{node.NodeId}` SHALL remain unchanged, producing unique NodeIds per parent

#### Scenario: Same element text on different parent nodes produces distinct children
- **WHEN** element "ON" appears on both wifi and bluetooth pages (different parent containers)
- **THEN** dedup key `(parentNodeId_1, "switch_leaf_ON")` and `(parentNodeId_2, "switch_leaf_ON")` SHALL be distinct
- **AND** both switch_leaf children SHALL be generated and tracked independently

#### Scenario: Invalidate preserves dedup across re-generation
- **WHEN** `Invalidate(nodeId)` removes the `_dynamicChildren` cache entry for scroll invalidation
- **THEN** `_generatedPairs` dedup entries for that parent SHALL persist (existing behavior: D-3)
- **AND** after re-generation, previously generated childNames for that parent SHALL still be deduped (prevent duplicate generation within same parent scope)

#### Scenario: Backward compatibility — different pages still dedup correctly
- **WHEN** two parent nodes are on different pages (different fingerprints, e.g., home vs wifi)
- **THEN** the same childName (e.g., "switch_leaf_ON") SHALL be allowed for both parents
- **AND** no cross-page dedup collision SHALL occur (parentNodeId differs)
