# Real-World State Evidence Bridge — Capability Challenge

> 2026-08-11 | Status: GATE_REQUIRED
> Baseline: SEMANTIC_COMPONENT_FREEZE_READY · 661/661 tests · frozen ownership
> Scope: Analysis only. No implementation.

---

## 1. The Missing Path

```
B1 Real Screenshot (1440×3168)
        ↓
YOLO: switch detection at pixel [1160,1251,1314,1346]
      type="switch", confidence=0.81, text="", riskFlags=["no_text_evidence"]
        ↓
Fusion: candidate { type="switch", text="", bounds=(0.805,0.395,0.913,0.425) }
        ↓
analysis.jsonl: NO state field (only name, type, x, y, expectedAction)
        ↓
ObservedElement: { Text="", SwitchState=null, Bounds=(0.805,0.395,0.913,0.425), PerceptionType="switch" }
        ↓
                              ← STATE CLASSIFICATION BRIDGE (MISSING)
                              ← SwitchState remains null on real perception path
        ↓
Container.RefreshObjectStateBeliefs:
  toggle found ✓, PerceptionType="switch" ✓
  SwitchState=null → ObjectStateBeliefs[key] = null (UNKNOWN)
        ↓
Agent.RunSemanticGoalAsync:
  currentBelief=null → StateEvidenceRequired (safe, but cannot complete)
```

**The Runtime is architecturally complete.** The gap is isolated to a single field: `ObservedElement.SwitchState` is never populated from real perception.

---

## 2. Current Available Signals

### 2.1 B1 Golden — Switch Candidate

| Signal | Value | Source |
|---|---|---|
| `type` | `"switch"` | YOLO label (Deki-Yolo) |
| `bounds` | `{x1:0.805, y1:0.395, x2:0.913, y2:0.425}` | YOLO detection box, normalized to full screenshot |
| `boundsPx` | `[1160, 1251, 1314, 1346]` | YOLO detection box, device pixels |
| `text` | `""` (empty) | OCR token matching — no text within detection distance |
| `confidence` | `0.81` | Combined YOLO+OCR confidence |
| `riskFlags` | `["no_text_evidence"]` | No OCR token matched this detection |
| **ON/OFF state** | **MISSING** | Not produced by YOLO, OCR, or fusion |

### 2.2 Adjacent OCR Tokens

```
OCR tokens near switch y-range (0.39-0.43): ZERO
```

No "ON"/"OFF" text, no state indicator text, no label text adjacent to the switch.

### 2.3 Screenshot

```
Resolution: 1440×3168 (PKJ110, Chinese ROM)
Switch region: 154×95 pixels at top-left position (1160, 1251)
The region contains a Material Design toggle widget.
```

---

## 3. Approach Evaluation

### 3.1 OCR / Text Only

```
Approach: Detect "ON"/"OFF" text near switch bounds.
Feasibility: NOT VIABLE
Evidence: B1 golden — zero OCR tokens near switch. Android Settings toggles
          do not display state text adjacent to the control.
Confidence: N/A
Verdict: REJECTED — no text signal exists to consume.
```

### 3.2 UI Hierarchy Attributes

```
Approach: Query Android AccessibilityNodeInfo.isChecked or contentDescription.
Feasibility: NOT IN CURRENT PIPELINE
Evidence: UIAutomator path was deleted from uni-claw (delete-uia refactor).
          Current pipeline is pure vision (screenshot → YOLO → OCR → fusion).
          Re-introducing accessibility would require a second perception channel.
Confidence: Would be high if available (direct OS API).
Verdict: REJECTED for minimum purchase — introduces a second perception channel.
         Viable as FUTURE_ENHANCEMENT if vision-only proves insufficient.
```

### 3.3 Image Classification — Switch Region Crop

```
Approach: Crop the switch bounds region from the screenshot.
          Classify the cropped region as ON or OFF using a lightweight
          image classifier.

Input:  (screenshot_image, ElementBounds)
Output: bool? (true=ON, false=OFF, null=uncertain)

Feasibility: VIABLE — minimum viable approach.

Evidence:
  - Android Material Design toggles have high visual distinction:
    ON:  filled track color + circle positioned right
    OFF: gray track + circle positioned left
  - Switch region is standardized (Material Design spec)
  - B1 PKJ110 uses ColorOS/HarmonyOS — toggle style is consistent
  - Region size: ~154×95 pixels — sufficient for classification
  - YOLO already provides precise bounds → crop is accurate

Implementation options:
  a) Traditional CV: threshold track color, detect circle position.
     Fast, no model. Brittle to theme variations.
  b) Lightweight classifier: small CNN (MobileNet/EfficientNet).
     Trained on ON/OFF toggle crops. Robust to themes.
     Requires training data (~100-200 labeled crops).
  c) Pixel comparison: compare against known ON/OFF prototype images.
     Simplest. Requires prototype capture per device/theme.

Confidence:
  - Unambiguous switch: HIGH (distinct ON vs OFF visual states)
  - Edge cases (partially animated, unusual themes): MEDIUM
  - Non-standard toggle widgets: LOW — would return null

Ownership: Perception-side. Stateless pure function.
           Same pattern as PageAnalysis / ElementAnalysis.

Verdict: PRIMARY FAST PATH — minimum viable bridge.
```

### 3.4 Embedding Retrieval

```
Approach: Compute embedding of switch region. Compare against stored
          embeddings of known ON and known OFF switch states.

Feasibility: VIABLE but over-engineered for minimum purchase.

Evidence:
  - Requires embedding model (e.g., CLIP, DINOv2)
  - Requires prototype database per device/theme
  - Embedding similarity ≠ state classification
  - Same complexity as VLM without VLM's flexibility

Confidence: Similar to image classification but adds infrastructure.

Verdict: REJECTED for minimum purchase.
         Too much infrastructure for a binary classification problem.
```

### 3.5 VLM Verification

```
Approach: Send cropped switch region to VLM.
          Prompt: "Is this toggle switch ON or OFF? Answer only ON, OFF, or UNCERTAIN."

Feasibility: VIABLE — most flexible approach.

Evidence:
  - VLM can handle any switch style, any theme, any platform
  - No training data needed
  - Zero-shot — works on first encounter
  - Can explain reasoning ("circle is on the right, track is blue")

Drawbacks:
  - Latency: 500ms-2s per VLM call
  - Cost: per-token pricing
  - Overkill for binary classification of standardized widgets

Confidence:
  - Standard toggles: HIGH
  - Unusual/custom toggles: HIGH (VLM's strength)
  - Ambiguous states (animation mid-transition): MEDIUM

Ownership: Perception-side. Slow path.
           Agent may invoke when fast path returns null/uncertain.
           VLM output is SemanticEvidence (I-14: AI is pluggable, not truth).

Verdict: SLOW FALLBACK PATH.
         Not needed for minimum purchase.
         Use when fast classifier returns null/uncertain.
```

---

## 4. Fast / Slow Perception Split

### 4.1 Design

```
SwitchStateClassifier (stateless, perception-side)

  FAST PATH (always runs):
    ImageClassifier.Classify(screenshot, bounds) → bool?
    - Crops region from screenshot
    - Classifies ON/OFF using lightweight model
    - Returns null if uncertain (< confidence threshold)

  SLOW PATH (runs only if fast path returns null):
    VLM.Classify(screenshot, bounds) → bool?
    - Sends cropped region to VLM
    - Returns ON/OFF/UNCERTAIN
    - Agent may invoke this (I-14: AI is pluggable)
```

### 4.2 Ownership

| Path | Owner | Pattern |
|---|---|---|
| **Fast** (image classifier) | Perception adapter (IEnvironment implementation) | Stateless pure function. Same pattern as PageAnalysis. Populates `ObservedElement.SwitchState`. |
| **Slow** (VLM) | Agent may invoke when fast returns null | VLM output is `SemanticEvidence`. Agent adjudicates (I-14: AI is pluggable, not truth). |

### 4.3 Attachment Point

```
Perception Adapter (IEnvironment implementation):
  ObserveAsync() →
    1. YOLO + OCR + fusion → candidates
    2. For each switch/toggle candidate:
       StateClassifier.Classify(screenshot, candidate.bounds) → bool?
    3. Populate ObservedElement.SwitchState = result
    4. Return Observation with populated SwitchState fields

Container.RefreshObjectStateBeliefs:
  Already consumes SwitchState. No change needed.

Agent.RunSemanticGoalAsync:
  Already handles true/false/null. No change needed.
```

---

## 5. Minimum Falsifying Scenario

### 5.1 Setup

B1 real-device golden (PKJ110, 1440×3168):
- Screenshot: Settings root page
- Switch candidate: type="switch", bounds=(0.805, 0.395, 0.913, 0.425)
- The switch controls "飞行模式" (Airplane mode) at same y-row

### 5.2 Scenario A — Classifiable Switch (PASS)

```
Given: B1 screenshot + switch bounds
When:  StateClassifier.Classify(screenshot, bounds)
Then:  Returns true (switch is ON) OR false (switch is OFF)
       Not null — the switch is visually unambiguous.

Pass criteria:
  - Classifier returns a definite bool for a standard Material Design toggle
  - Same region at different resolutions returns the same classification
```

### 5.3 Scenario B — Unclassifiable Region (PASS)

```
Given: B1 screenshot + bounds of a non-switch region (e.g., text label)
When:  StateClassifier.Classify(screenshot, textLabelBounds)
Then:  Returns null — region does not contain a recognizable toggle

Pass criteria:
  - Classifier does not hallucinate ON/OFF for non-toggle regions
```

### 5.4 Scenario C — End-to-End (PASS)

```
Given: B1 screenshot
When:  Full perception pipeline runs:
        YOLO → fusion → StateClassifier → ObservedElement
Then:  ObservedElement.SwitchState is populated from classifier output
       Container.RefreshObjectStateBeliefs correctly maps to belief
       Agent.RunSemanticGoalAsync can decide Satisfied/StateEvidenceRequired

Pass criteria:
  - SwitchState flows from perception → Observation → Container → Agent
  - No Runtime code changes needed
```

---

## 6. Evidence Contract

### 6.1 StateClassifier Contract

```
StateClassifier.Classify(
    screenshot: Image,
    bounds: ElementBounds  // normalized [0,1]×[0,1], canonical full-screenshot frame
) → bool?
    // true  = visually ON
    // false = visually OFF
    // null  = cannot determine (not a switch, ambiguous, low confidence)
```

### 6.2 Mapping to ObservedElement

```
ObservedElement.SwitchState ← StateClassifier.Classify(screenshot, element.Bounds)
```

No new Runtime types. No new evidence contracts. No new Model fields. The `ObservedElement.SwitchState` field already exists and is already consumed by `Container.RefreshObjectStateBeliefs`.

### 6.3 Confidence — Deferred

```
Numeric confidence (0.0-1.0) is NOT purchased now.

Reason:
  - Binary classification (ON/OFF/UNKNOWN) is sufficient for the safety rules
  - Unknown state already blocks dispatch (no blind toggle)
  - Numeric confidence adds complexity without changing behavior
  - Same pattern as SemanticEvidence: qualitative, not numeric

Future: if ranking multiple state hypotheses becomes necessary,
        add confidence as a separate purchase.
```

---

## 7. Ownership Impact

```
FROZEN (no change):
  Agent:        semantic decision authority unchanged
  Container:    state ownership unchanged (already consumes SwitchState)
  Traversal:    lowering unchanged (already checks SwitchState)
  Environment:  physical execution unchanged

NEW (perception-side, stateless):
  StateClassifier: stateless pure function
    - Owns: visual state classification logic
    - Does NOT own: Runtime state, Agent authority, Container belief
    - Pattern: follows PageAnalysis / ElementAnalysis (stateless, pure)
    - Location: perception adapter (IEnvironment implementation)
    - Architecture Delta: NONE for Runtime
```

---

## 8. Summary

```
REAL_WORLD_STATE_EVIDENCE_BRIDGE_CAPABILITY_GATE_REQUIRED

Minimum purchase: StateClassifier — stateless image classifier for switch regions.

Approach:        Image classification of cropped switch region → bool?
                 (FAST PATH — lightweight, always runs)

Fallback:        VLM verification → bool?
                 (SLOW PATH — Agent-invoked when fast path returns null)

OCR/text:        REJECTED — no state text near switches
UI hierarchy:    REJECTED for minimum — requires second perception channel
Embedding:       REJECTED — over-engineered for binary classification
Image classify:  SELECTED — minimum viable, standardized widget, high visual distinction
VLM:             FUTURE — fallback for uncertain cases

Contract:
  StateClassifier.Classify(screenshot, bounds) → bool?

Evidence flow:
  Perception → StateClassifier → ObservedElement.SwitchState
  → Container.RefreshObjectStateBeliefs (already exists, no change)
  → Agent.RunSemanticGoalAsync (already handles true/false/null)

Ownership impact:
  Agent:        NONE
  Container:    NONE
  Traversal:    NONE
  Environment:  NONE
  New:          StateClassifier (stateless, perception-side, PageAnalysis pattern)
  Architecture: NONE

Frozen rules respected:
  - Agent semantic authority unchanged
  - Container state ownership unchanged  
  - Traversal execution authority unchanged
  - No shared mutable state
  - Stateless producer pattern (PageAnalysis)
  - I-14: AI is pluggable (VLM as slow fallback, not required path)

Next:
  PURCHASE_STATE_CLASSIFIER — implement minimum image classifier
  for switch/toggle ON/OFF detection from screenshot regions.

STOP.
```
