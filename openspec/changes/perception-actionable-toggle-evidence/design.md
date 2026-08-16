# Design: Perception Actionable Toggle Evidence

## Audit

### Pipeline Trace

1. **Python server.py** `_run_pipeline`:
   - Loads YOLO model, runs inference
   - Loads OCR (PaddleOCR)
   - Calls `fusion/engine.py:fuse_evidence` (or `fuse_evidence_from_crops`)
   - Returns `candidates` with `type` = YOLO detection label

2. **Python fusion/engine.py** `fuse_evidence`:
   - Iterates YOLO detections, creates candidates with `type` = detection.label
   - Promotes unmatched OCR tokens with `type` = "text_block"
   - Calls `apply_search_box_labeling` → sets `type` = "input" for search boxes
   - Calls `apply_chevron_heuristic` → reclassifies text_block → menu_item if aligned with YOLO widget

3. **Python fusion/heuristics.py** `apply_chevron_heuristic`:
   - Checks if any YOLO detections have labels in `_ROW_WIDGET_LABELS = {"icon", "switch", "toggle", "checkbox"}`
   - If none: returns early (no reclassification)
   - **This is the gap**: on API 35, YOLO detects NO widgets, so no reclassification occurs

4. **C# LocalVisionPerceptionSource**:
   - Deserializes JSON to `VisionEvidence`
   - Creates `PerceptionCandidate(Text, Type, Bounds)` per candidate
   - `Type` = raw `type` field from JSON

5. **C# PhysicalEnvironment.ObserveAsync**:
   - Converts `PerceptionCandidate` → `ObservedElement(Text, SwitchState, Index, Bounds, PerceptionType)`
   - `PerceptionType` = `candidate.Type` (raw YOLO label, e.g., "text_block", "switch", "button")

6. **C# BindingAnalysis.Analyze**:
   - Looks for `PerceptionType == "toggle"` (exact string match)
   - Pairs with text-anchor elements via `SameRow` geometry

7. **C# BindingReconciler**:
   - Aggregates `BindingEvidence` proposals into `ObjectBinding`

8. **C# StateBeliefReducer**:
   - From `ObjectBinding`, finds elements with `PerceptionType == "toggle"` AND `SwitchState != null`
   - Returns state belief (true/false/null)

### Gap Classification

**Primary**: DETECTOR_CLASS_GAP — YOLO does not detect control element labels on API 35

**Secondary**: STATE_EXTRACTION_GAP — even if toggle is detected, SWITCH state extraction may be affected by visual rendering differences

### Solution

Extend fusion heuristics in `heuristics.py` to infer toggle type from structural/geometric evidence when YOLO does not provide labels:

1. **Toggle geometry inference**: Look for compact, right-side-aligned elements near the end of a text row that have the aspect ratio of a Settings toggle switch
2. **Row association**: Use vertical overlap and horizontal proximity to associate inferred toggles with their label rows
3. **State inference**: For inferred toggles, determine ON/OFF from visual features (knob position relative to track, brightness distribution)
4. **Canonical type**: Emit `type = "switch"` (which maps to `perception_type = "toggle"` via label mapping) for inferred toggles

### Authority Boundary

- Perception provides EVIDENCE (type, bounds, state)
- Binding provides semantic association
- StateBeliefReducer provides state belief
- Agent provides semantic decision
- External world provides truth
- No authority shift

## Implementation

### Slice 1: OpenSpec + pipeline trace (this document)
### Slice 2: Fusion heuristic implementation
### Slice 3: Canonical type/state mapping
### Slice 4: PER-T1..T12 tests
### Slice 5: Binding integration tests
### Slice 6: StateBeliefReducer integration tests
### Slice 7: API 35 asset validation
### Slice 8: Full regression
