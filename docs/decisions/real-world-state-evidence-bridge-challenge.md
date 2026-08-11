# Real-World State Evidence Bridge Challenge

> 2026-08-11 | Status: GATE_REQUIRED
> Baseline: SEMANTIC_CORE_FREEZE_READY · 661/661 tests pass · 5/6 phases validated
> Scope: Analysis only. No Runtime modification.

---

## 1. The Missing Path

```
Real Observation (B1 golden, A3 EP-04)
        ↓
ObservedElement { Text="", Bounds=(0.80,0.39,0.91,0.42), PerceptionType="switch" }
        ↓
                              ← SWITCH STATE DETECTION BRIDGE (MISSING)
                              ← No SwitchState in real perception
        ↓
Container.ObjectStateBeliefs["WifiConnectivity.Enabled"] = ???
```

**Current truth:** `RefreshObjectStateBeliefs` reads `ObservedElement.SwitchState`. Real perception produces `Text`, `Bounds`, `PerceptionType` ("switch"/"toggle"), and `confidence` — but NO `SwitchState`. The field exists on `ObservedElement` but is only populated in synthetic test fixtures.

**Result:** On real devices, `Container.ObjectStateBeliefs["WifiConnectivity.Enabled"]` is always `null` (UNKNOWN). `Agent.RunSemanticGoalAsync` correctly returns `StateEvidenceRequired` — the loop is safe, but it cannot autonomously complete.

---

## 2. Current Perception Inputs Available

### 2.1 From Real Perception (B1 golden — `settings-real.android-ui-yolo.evidence.json`)

| Signal | Present? | Detail |
|---|---|---|
| **Element type** | **YES** | `"switch"`, `"toggle"`, `"menu_item"`, `"text_block"`, `"icon"`, `"button"`, `"input"` — raw YOLO label |
| **Element bounds** | **YES** | Normalized `{x1, y1, x2, y2}` + pixel `boundsPx` |
| **Element center** | **YES** | Normalized `{x, y}` + pixel `centerPx` |
| **OCR text** | **YES** | Per-element text from OCR token matching |
| **Detection confidence** | **YES** | Combined YOLO+OCR confidence (0-1) |
| **Detection provenance** | **YES** | `evidence: { yoloId, ocrIds }` |
| **ON/OFF state** | **NO** | No state field in candidates, YOLO, OCR, or analysis.jsonl |
| **Screenshot** | **YES** | Available as input image (1440×3168 raw, resized for vision) |

### 2.2 From analysis.jsonl (A3 EP-04 recorded runs)

| Signal | Present? |
|---|---|
| `name`, `type`, `x`, `y`, `expectedAction` | **YES** |
| ON/OFF state | **NO** |

### 2.3 From Runtime Model (ObservedElement)

| Field | Populated from real perception? |
|---|---|
| `Text` | **YES** — from OCR |
| `SwitchState` | **NO** — null on real path |
| `Index` | **YES** — ordinal from Environment |
| `Bounds` | **PARTIAL** — model supports it; production perception adapter not implemented |
| `PerceptionType` | **PARTIAL** — model supports it; production perception adapter not implemented |

---

## 3. The Bridge Gap

### 3.1 What the Runtime Needs

```
Container.RefreshObjectStateBeliefs reads:
  ObservedElement.SwitchState ∈ { true, false, null }
  ObservedElement.PerceptionType == "toggle"

For exactly 1 toggle in the binding:
  SwitchState=true  → ObjectStateBeliefs[key] = true
  SwitchState=false → ObjectStateBeliefs[key] = false
  0 or >1 toggles   → ObjectStateBeliefs[key] = null (UNKNOWN)
```

**The Runtime already has the correct consumption logic.** The gap is purely PRODUCTION: `ObservedElement.SwitchState` needs to be populated from real perception.

### 3.2 What Real Perception Can Provide

The switch/toggle region IS detected: bounds at (0.805, 0.395, 0.913, 0.425). The type IS known: "switch". The screenshot IS available. What's missing is the state CLASSIFICATION of that region.

### 3.3 Minimum Bridge Candidates

| Option | Mechanism | Input → Output | Feasibility |
|---|---|---|---|
| **A — Screenshot Region Classifier** | Crop switch bounds from screenshot. Classify ON/OFF via pixel analysis (traditional CV or small classifier). | `(screenshot, bounds) → bool?` | **VIABLE** — switch region is a fixed-size UI widget. Toggle position (left=OFF, right=ON) is visually distinct. Does not require VLM. |
| **B — VLM State Query** | Send cropped switch region to VLM. "Is this switch ON or OFF?" | `(switch_image) → bool?` | **VIABLE but heavy** — VLM per toggle is slow. Better as fallback. |
| **C — OCR State Text** | OCR the area around the switch for "ON"/"OFF" text. | `(screenshot, switch_bounds) → bool?` | **UNRELIABLE** — Android Settings switches don't display "ON"/"OFF" text adjacent to the toggle. |
| **D — Accessibility API** | Query Android `AccessibilityNodeInfo.isChecked`. | `(element) → bool` | **NOT IN CURRENT PIPELINE** — UIAutomator path was deleted. Would need re-introduction. |

### 3.4 Minimum Viable Bridge

**Option A — Screenshot Region Classifier** is the minimum.

Contract:
```
StateClassifier.Classify(screenshot, bounds) → bool?
```

Where:
- `screenshot`: raw screenshot image (the same one YOLO/OCR processed)
- `bounds`: normalized `ElementBounds` — same space as YOLO detection
- Returns: `true` (ON), `false` (OFF), or `null` (cannot determine)

This is a **perception-side capability**, not a Runtime change. The Runtime's `ObservedElement.SwitchState` field already exists. The bridge fills it from real perception.

---

## 4. Ownership Impact Analysis

### 4.1 Where the Bridge Attaches

```
Perception Pipeline (external)
        ↓
StateClassifier.Classify(screenshot, bounds)
        ↓ bool?
        ↓
ObservedElement.SwitchState  ← populated HERE
        ↓
Container.RefreshObjectStateBeliefs  ← already consumes SwitchState
        ↓
Container.ObjectStateBeliefs["WifiConnectivity.Enabled"]
        ↓
Agent.RunSemanticGoalAsync  ← already reads belief
```

### 4.2 Ownership — UNCHANGED

| Owner | Impact |
|---|---|
| **Agent** | **NONE** — `RunSemanticGoalAsync` already handles true/false/null correctly. No change. |
| **Container** | **NONE** — `RefreshObjectStateBeliefs` already reads `ObservedElement.SwitchState`. No change. |
| **Traversal** | **NONE** — `LowerAction` already checks SwitchState for safety. No change. |
| **Perception (IEnvironment)** | **PRODUCER** — `ObserveAsync` must populate `ObservedElement.SwitchState` from the classifier output. This is the perception adapter's responsibility. |
| **StateClassifier** | **NEW STATELESS CAPABILITY** — pure function. No mutable state. No new owner. Follows PageAnalysis/ElementAnalysis pattern. |

### 4.3 Architecture Delta — NONE (for Runtime)

The Runtime model (`ObservedElement.SwitchState`) already exists. The consumption path (`RefreshObjectStateBeliefs`) already exists. The safety rules (`LowerAction` → StateUnknown) already exist.

**The bridge is a perception-adapter concern, not a Runtime architecture change.**

---

## 5. Minimum Falsifying Scenario

### 5.1 Scenario

Given B1 real-device golden:
- Switch candidate: type="switch", bounds=(0.805, 0.395, 0.913, 0.425), text=""
- Screenshot: 1440×3168, PKJ110, Chinese ROM

### 5.2 Expected Behavior

```
StateClassifier.Classify(
    screenshot = B1_Screenshot,
    bounds = ElementBounds(0.805, 0.395, 0.913, 0.425)
)
→ true   (switch is visually ON)
OR
→ false  (switch is visually OFF)
OR
→ null   (cannot determine from this region)
```

### 5.3 Expected Evidence Contract

The classifier output maps to the existing `ObservedElement.SwitchState`:

```
ObservedElement {
    Text = "",
    SwitchState = true/false/null,  ← populated by StateClassifier
    Bounds = (0.805, 0.395, 0.913, 0.425),
    PerceptionType = "switch"
}
```

### 5.4 Pass Criteria

1. **PASS**: StateClassifier returns `true` or `false` for a visually unambiguous switch region.
2. **PASS**: StateClassifier returns `null` for a region that does not contain a recognizable switch.
3. **PASS**: `Container.RefreshObjectStateBeliefs` correctly maps the classified SwitchState to `ObjectStateBeliefs`.
4. **PASS**: `Agent.RunSemanticGoalAsync` correctly decides: known state → dispatch if needed, unknown state → StateEvidenceRequired.

### 5.5 End-to-End Reality Proof

```
B1 real-device screenshot
        ↓
YOLO: switch detection at (0.805, 0.395, 0.913, 0.425), type="switch"
        ↓
StateClassifier: crop region → classify → bool?
        ↓
ObservedElement { SwitchState = bool? }
        ↓
ElementAnalysis: bind to WifiConnectivity (Text anchor + PerceptionType + spatial)
        ↓
Container.ObjectStateBeliefs["WifiConnectivity.Enabled"] = true/false/null
        ↓
Agent.RunSemanticGoalAsync → Satisfied / StateEvidenceRequired / Dispatched
```

---

## 6. Summary

```
REAL_WORLD_STATE_EVIDENCE_BRIDGE_GATE_REQUIRED

Missing capability: SwitchState detection from real perception

Current state:
  - ObservedElement.SwitchState exists (model field) ✓
  - Container.RefreshObjectStateBeliefs consumes it ✓
  - Agent.RunSemanticGoalAsync handles true/false/null correctly ✓
  - Safety rules prevent blind toggle ✓
  - SwitchState is NULL on real perception path ✗

Minimum purchase:
  StateClassifier.Classify(screenshot, bounds) → bool?
  - Stateless pure function (PageAnalysis pattern)
  - Perception-side, not Runtime architecture change
  - Populates existing ObservedElement.SwitchState field

Impact:
  - Agent ownership: NONE
  - Container ownership: NONE
  - Traversal ownership: NONE
  - Architecture invariants: NONE
  - Runtime model changes: NONE
  - New mutable state: NONE

The bridge closes the PERCEPTION_EVIDENCE_GAP for SwitchState.
It does NOT require any Runtime architecture change.

Without this bridge, Agent.RunSemanticGoalAsync is safe but cannot
autonomously complete on real devices — it always returns
StateEvidenceRequired because SwitchState is never populated.

STOP.
```
