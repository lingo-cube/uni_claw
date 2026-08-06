## ADDED Requirements

### Requirement: Same-row items with identical or containing text are merged
The vision provider SHALL merge items that share approximately the same Y coordinate (within a row-height threshold) and have identical or containing text relationships. Only one representative item SHALL be emitted.

#### Scenario: Duplicate detections of same element
- **WHEN** YOLO produces multiple overlapping bounding boxes for the same UI element (e.g., Battery detected 3 times at nearly identical Y coordinates with identical text)
- **THEN** only one item is emitted in the aggregated item list

#### Scenario: Different elements at different Y coordinates
- **WHEN** two items have identical text but different Y coordinates exceeding the row-height threshold
- **THEN** both items are kept as separate entries
