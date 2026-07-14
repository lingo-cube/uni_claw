## ADDED Requirements

### Requirement: DynamicMatch shall traverse all sibling navigation children

A DynamicMatch parent node whose current page exposes multiple navigation children (matched elements whose `ExpectedAction` is `Navigate` / `ExpectsPageChange` is true) SHALL have every sibling navigation child entered — not only the first. For each navigation child, the engine SHALL navigate into its destination sub-page, traverse that sub-page fully (including its own scroll-to-end via the action+judgment loop), then return to the parent's original page and continue selecting remaining unvisited sibling children. This SHALL hold for arbitrarily deep navigation trees (a navigation child's sub-page may itself contain further navigation children, restored layer-by-layer via PressBack).

#### Scenario: Two sibling navigation branches both traversed
- **WHEN** a DynamicMatch parent page has two navigation buttons `to_A` (→ page listA) and `to_B` (→ page listB), each backed by scrollable content
- **THEN** the engine SHALL visit every item of listA AND every item of listB
- **AND** the traversal SHALL execute a PressBack returning from listA to the parent page before entering listB

#### Scenario: Deep navigation tree is fully covered
- **WHEN** navigation chains root → page1 → page2, where each page has scrollable content
- **THEN** the engine SHALL traverse page1's items AND page2's items
- **AND** SHALL PressBack from page2 to page1, then from page1 to root, restoring each parent page before regenerating its remaining children

### Requirement: Navigation children shall push a sub-page frame attributed to the navigation child

When a DynamicMatch child whose matched element has `ExpectedAction == Navigate` is executed and the page changes, the engine SHALL push a DynamicMatch sub-page frame whose children are generated from the navigation destination page and attributed to that navigation child's NodeId (not to the root). The engine SHALL distinguish navigation-caused page changes (which require a sub-page frame and page restore on return) from scroll-caused page-item changes (which require child regeneration) using the matched element's navigation metadata, NOT by inferring from page-fingerprint changes.

#### Scenario: Navigated sub-page items are attributed to the navigation child frame
- **WHEN** a navigation child `to_A` is executed and navigates from hub to listA
- **THEN** listA's generated children SHALL belong to `to_A`'s sub-page frame (parent NodeId = the navigation child), not to the root frame
- **AND** exhausting listA SHALL pop `to_A`'s frame (at depth ≥ 2) and trigger PressBack to hub before the root regenerates hub's children

#### Scenario: Scroll page-item change still regenerates (not framed)
- **WHEN** a scroll reveals new items on the same page (no navigation)
- **THEN** the engine SHALL regenerate children from the new page content via the action+judgment loop (existing behavior), NOT push a sub-page frame or PressBack

#### Scenario: False navigation falls back to leaf
- **WHEN** a child is marked navigation (`ExpectedAction.Navigate`) but its execution does NOT change the page fingerprint
- **THEN** the engine SHALL treat it as an ordinary leaf child (no sub-page frame is pushed)

### Requirement: all_visited completion shall require all navigation branches

A DynamicMatch node SHALL report completion (`all_visited`) only after every sibling navigation child has been entered and its destination sub-page fully traversed. `VisitedNodes` SHALL deduplicate navigation children across frames so each navigation child is counted as visited exactly once. This corrects the prior behavior where un-regenerated sibling navigation children made `all_visited` trivially true despite incomplete coverage.

#### Scenario: all_visited false until sibling branch entered
- **WHEN** a DynamicMatch parent has navigation children `to_A` and `to_B`, and only `to_A`'s sub-page has been traversed
- **THEN** the parent SHALL NOT be considered `all_visited`
- **AND** after `to_B`'s sub-page is also traversed, the parent SHALL be `all_visited`

#### Scenario: Navigation child counted once across frames
- **WHEN** a navigation child is visited, then its sub-page frame is popped and the parent page restored
- **THEN** the navigation child SHALL appear in `VisitedNodes` exactly once
- **AND** re-generation of the parent's children SHALL mark it visited (not re-entered)
