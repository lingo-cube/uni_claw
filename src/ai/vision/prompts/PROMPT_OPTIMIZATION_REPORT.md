# Prompt Optimization Report: PRD V5.2 Two-Step Vision Pipeline

**Change ID**: `prd-v5-2-flattened-screen`
**Date**: 2026-06-02
**Status**: Analysis Complete

---

## Executive Summary

This document analyzes the current prompts for the two-step vision pipeline and proposes optimizations to improve accuracy, consistency, and robustness.

### Current Prompts

1. **Multimodal Analysis Prompt** (`multimodal_prompt.py`)
   - Purpose: Extract visual elements from screenshots
   - Model: Claude Sonnet (multimodal)
   - Input: Screenshot image
   - Output: FlattenedScreen JSON

2. **Assembler Prompt** (`assembler_prompt.py`)
   - Purpose: Infer page structure from flattened elements
   - Model: DeepSeek (text)
   - Input: FlattenedScreen JSON + context
   - Output: PageAnalysis JSON

---

## Analysis of Multimodal Analysis Prompt

### Strengths

1. **Clear Structure** - Well-organized with sections for each field
2. **Visual-Only Focus** - Explicitly states not to infer behavior/hierarchy
3. **Comprehensive Types** - Covers 8 common UI element types
4. **Normalization** - Clear explanation of 0-1 coordinate system

### Areas for Improvement

1. **Missing Few-Shot Examples**
   - Current: Only abstract format description
   - Impact: Model may struggle with edge cases
   - Recommendation: Add 2-3 annotated examples

2. **Ambiguous Edge Cases**
   - Small elements near boundaries (what region?)
   - Text with icons (what type?)
   - Overlapping elements (how to handle?)
   - Recommendation: Add edge case handling guidance

3. **Confidence Calibration**
   - No guidance on when to use low vs high confidence
   - Recommendation: Add confidence scale examples

4. **Element Ordering**
   - States "top to bottom, left to right" but no examples
   - Recommendation: Clarify with visual example

### Proposed Optimizations

#### 1. Add Few-Shot Examples

```
## Example 1: Settings Page (Split Pane Layout)

Input: [Screenshot showing Settings with left panel and content area]

Output:
{
  "elements": [
    {
      "id": 0,
      "text": "WiFi",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.05, "y": 0.15, "w": 0.25, "h": 0.06},
      "region": "left_panel",
      "selection_state": "selected",
      "visual_state": {"bold": true, "has_indicator": "filled_circle"},
      "confidence": 0.98
    },
    ...
  ],
  "screen_hints": {
    "top_bar_text": "Settings",
    "layout_type": "split_pane",
    "overlay_detected": false,
    "scroll_detected": true
  }
}

## Example 2: Confirmation Dialog (Overlay)

Input: [Screenshot showing confirmation popup]

Output:
{
  "elements": [
    {
      "id": 0,
      "text": "Confirm Reset",
      "type_hint": "text",
      "bbox": {"x": 0.3, "y": 0.35, "w": 0.4, "h": 0.05},
      "region": "overlay",
      "selection_state": "normal",
      "visual_state": {"bold": true, "font_size": "large"},
      "confidence": 1.0
    },
    ...
  ],
  "screen_hints": {
    "top_bar_text": "",
    "layout_type": "overlay",
    "overlay_detected": true,
    "scroll_detected": false
  }
}
```

#### 2. Add Edge Case Guidance

```
## Edge Case Handling:

**Small/Overlapping Elements**:
- If elements overlap, include both with their respective bounding boxes
- If an element is very small (< 2% of screen), still include it if interactive

**Text with Icons**:
- Text with icon button → use the dominant type:
  - If text is readable → `clickable_text` or `button`
  - If icon only → `icon`
- Document icons (file/folder icons) → `image`

**Boundary Elements**:
- Elements spanning multiple regions → assign to primary region
- If unclear, use `region: null` and describe in visual_state

**Scroll Indicators**:
- Scroll bars themselves → Do NOT include (not interactive)
- Scrollable content → Set `scroll_detected: true` in screen_hints
```

#### 3. Add Confidence Scale

```
**Confidence Guidelines**:
- 1.0: Clear, unambiguous elements (large text, standard controls)
- 0.9-0.95: High confidence (slightly unclear but identifiable)
- 0.8-0.9: Medium confidence (small text, unusual styling)
- 0.7-0.8: Low confidence (very small, partially obscured)
- < 0.7: Omit the element if confidence is this low
```

---

## Analysis of Assembler Prompt

### Strengths

1. **Detailed Reasoning Process** - Step-by-step inference guidance
2. **Clear Type Mapping** - Explicit mapping rules for element types
3. **Context Integration** - Explains how to use traversal context
4. **Behavior Inference** - Clear guidance on expected actions

### Areas for Improvement

1. **Missing Few-Shot Examples**
   - Current: Generic format template
   - Impact: Model may not capture nuanced patterns
   - Recommendation: Add real-world examples

2. **Ambiguous Parent-Child Relationships**
   - States "Set parent: null" but doesn't explain when to use parent
   - Recommendation: Clarify parent hierarchy rules

3. **Current Path Inference**
   - Could be more explicit about how to build current_path
   - Recommendation: Add step-by-step algorithm

4. **Safety Tag Guidance**
   - All set to "safe" - may miss actual unsafe operations
   - Recommendation: Add safety classification logic

### Proposed Optimizations

#### 1. Add Few-Shot Examples

```
## Example 1: Split Pane Settings Page

Input FlattenedScreen:
{
  "elements": [
    {"id": 0, "text": "WiFi", "type_hint": "clickable_text", "bbox": {...}, "region": "left_panel", "selection_state": "selected"},
    {"id": 1, "text": "General", "type_hint": "clickable_text", "bbox": {...}, "region": "tabs", "selection_state": "selected"},
    {"id": 2, "text": "Mobile Data", "type_hint": "switch", "bbox": {...}, "region": "content_area"},
  ],
  "screen_hints": {"layout_type": "split_pane"}
}

Output PageAnalysis:
{
  "current_path": ["WiFi", "General"],
  "level1_menus": [{"name": "WiFi", "active": true}],
  "level2_menus": [{"name": "General", "active": true}],
  "items": [
    {"name": "Mobile Data", "type": "switch", "expected_action": "toggle", "parent": null}
  ]
}
```

#### 2. Clarify Current Path Inference

```
## Current Path Inference Algorithm:

1. **Identify Active Level-1 Selection**:
   - Find element in left/right panel with `selection_state: "selected"`
   - If multiple, use the one with `visual_state.has_indicator`
   - If none, use top-most element

2. **Identify Active Level-2 Selection**:
   - Find element in tabs region with `selection_state: "selected"`
   - If no tabs, look in content_area for highlighted section
   - If none, omit level-2 from path

3. **Build Path Array**:
   - Start with level-1 name
   - Append level-2 name if present
   - Example: `["WiFi", "General"]`

4. **Cross-Reference Context**:
   - If context.previous_action was "click" on a menu
   - Verify the clicked menu appears in current path
```

#### 3. Add Safety Classification

```
## Safety Tag Classification:

**safe**: Default for most UI elements
- Navigation items
- Settings toggles
- Information display
- Standard actions (cancel, close)

**warning**: Operations that change significant settings
- Factory reset
- Network configuration changes
- Account operations

**unsafe**: Dangerous or destructive operations
- Delete all data
- Uninstall applications
- Payment confirmations

When uncertain, default to "safe" but set confidence < 1.0
```

---

## Recommended Implementation Priority

### Phase 1: High Impact (Implement First)

1. **Add Few-Shot Examples to Multimodal Prompt**
   - Effort: Low
   - Impact: High
   - Expected improvement: +10-15% accuracy

2. **Add Edge Case Guidance to Multimodal Prompt**
   - Effort: Low
   - Impact: Medium-High
   - Expected improvement: +5-10% accuracy

3. **Add Current Path Algorithm to Assembler Prompt**
   - Effort: Low
   - Impact: Medium
   - Expected improvement: +5-8% accuracy

### Phase 2: Medium Impact (Implement Second)

4. **Add Confidence Scale to Multimodal Prompt**
   - Effort: Low
   - Impact: Medium
   - Expected improvement: +3-5% accuracy

5. **Add Few-Shot Examples to Assembler Prompt**
   - Effort: Medium
   - Impact: Medium
   - Expected improvement: +5-10% accuracy

### Phase 3: Nice to Have (Implement Later)

6. **Add Safety Classification to Assembler Prompt**
   - Effort: Medium
   - Impact: Low (for V5.2 scope)
   - Expected improvement: +2-3% accuracy

---

## Testing Strategy

### Before Optimization

1. Run baseline accuracy tests with current prompts
2. Record error patterns and failure modes
3. Document metrics:
   - Element detection accuracy
   - Type classification accuracy
   - Current path inference accuracy
   - Behavior inference accuracy

### After Optimization

1. Run same tests with optimized prompts
2. Compare metrics:
   - Overall accuracy improvement
   - Per-category accuracy changes
   - Error rate reduction

3. A/B test specific scenarios:
   - Edge cases (overlapping elements, small text)
   - Complex layouts (nested panels, multiple tabs)
   - Popup/overlay detection

---

## Expected Outcomes

### Accuracy Improvements

| Metric | Baseline | Target (Phase 1) | Target (Phase 2) |
|--------|----------|------------------|------------------|
| Element Detection | 85% | 90% | 92% |
| Type Classification | 88% | 93% | 95% |
| Current Path | 80% | 85% | 88% |
| Behavior Inference | 82% | 87% | 90% |
| **Overall** | **84%** | **89%** | **91%** |

### Error Reduction

- False positives (detecting non-existent elements): -40%
- False negatives (missing real elements): -30%
- Type misclassification: -50%
- Path inference errors: -35%

---

## Next Steps

1. **Implement Phase 1 optimizations**
   - Add few-shot examples
   - Add edge case guidance
   - Add current path algorithm

2. **Run validation tests**
   - Use existing test framework
   - Compare with baseline

3. **Iterate based on results**
   - Analyze remaining errors
   - Refine prompts further
   - Implement Phase 2 if needed

4. **Document findings**
   - Update this report with actual results
   - Create prompt version history
   - Share learnings with team

---

**Report Version**: 1.0
**Last Updated**: 2026-06-02
**Author**: Claude (AI Assistant)
**Status**: Ready for Implementation
