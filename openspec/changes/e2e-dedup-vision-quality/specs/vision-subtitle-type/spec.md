## ADDED Requirements

### Requirement: Subtitle text items are classified as text type
The vision provider SHALL classify subtitle text (text positioned directly below a primary menu item label within the same UI row) as `text` type, not `menuItem`. The classification SHALL be based on vertical proximity: if an item's Y coordinate differs from the preceding `menuItem` type item's Y coordinate by less than a row-height threshold, it SHALL be downgraded to `text`.

#### Scenario: Subtitle below main label
- **WHEN** an item with text "28% used - 5.72GB free" is positioned at Y=0.429, and the preceding item "Storage" is at Y=0.396 (delta < threshold)
- **THEN** the subtitle item's type is `text`, not `menuItem`

#### Scenario: Separate row item is not downgraded
- **WHEN** an item is positioned at Y significantly different from any preceding `menuItem` (delta >= threshold)
- **THEN** the item retains its original YOLO-derived type
