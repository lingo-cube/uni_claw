## ADDED Requirements

### Requirement: OCR text is scoped to individual YOLO bounding boxes
The vision provider SHALL perform OCR independently within each YOLO-detected bounding box. Text from adjacent bounding boxes SHALL NOT be concatenated into a single item name.

#### Scenario: Adjacent labels are not merged
- **WHEN** three YOLO bounding boxes are detected in proximity with text "Dark theme", "font size", "brightness"
- **THEN** three separate items are emitted, not one item named "Dark theme,font size,brightness"

#### Scenario: Multi-line text within a single bbox
- **WHEN** a single YOLO bounding box contains two lines of text
- **THEN** both lines are OCR'd and the result is concatenated with a space separator (existing behavior preserved)

### Requirement: Item identity uses normalized text for cross-frame stability
The vision provider SHALL normalize item text for identity purposes: collapse consecutive whitespace to a single space, normalize punctuation variants. The display text SHALL remain unchanged. Normalization SHALL be applied to the key used for cross-frame item association and fingerprint computation.

#### Scenario: Space variant normalization
- **WHEN** OCR reads "Appsecurity,deviceock" in frame 1 and "App security,device lock" in frame 2
- **THEN** the normalized identity key is the same for both frames

#### Scenario: Display text is not modified
- **WHEN** an item's text is normalized for identity purposes
- **THEN** the item's `Name` field retains the original OCR text
