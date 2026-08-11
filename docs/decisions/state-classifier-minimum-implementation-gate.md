# StateClassifier — Minimum Implementation Gate

> 2026-08-11 | Status: GATE_REQUIRED
> Baseline: REAL_WORLD_STATE_EVIDENCE_BRIDGE_CAPABILITY_GATE_REQUIRED
> Scope: Implementation contract only. No code.

---

## 1. StateClassifier Contract

### 1.1 Signature

```csharp
public static bool? Classify(
    object screenshot,          // platform image type (SkiaSharp.SKBitmap, System.Drawing.Bitmap, etc.)
    ElementBounds bounds,       // normalized [0,1]×[0,1] in canonical full-screenshot frame
    float minConfidence = 0.80f // threshold below which result is null
)
```

### 1.2 Input

| Parameter | Type | Description |
|---|---|---|
| `screenshot` | Platform image | The raw screenshot that YOLO + OCR processed. Same image used for perception pipeline. |
| `bounds` | `ElementBounds` | Normalized switch/toggle region. Validated: `bounds.IsValid == true`. |
| `minConfidence` | `float` (default 0.80) | Classification confidence threshold. Result is `null` if confidence < threshold. |

### 1.3 Output

| Value | Meaning | When |
|---|---|---|
| `true` | Switch is visually ON | Classifier confidence ≥ `minConfidence` AND ON predicted |
| `false` | Switch is visually OFF | Classifier confidence ≥ `minConfidence` AND OFF predicted |
| `null` | Cannot determine | Confidence < `minConfidence`, OR region is not a recognizable switch, OR bounds invalid |

### 1.4 Confidence Semantics

```
Confidence is a classifier-internal metric. It is NOT exposed in the public API.
The public API returns only bool? — qualitative three-state.

Internal confidence rules:
  - ≥ minConfidence → return true or false
  - <  minConfidence → return null (uncertainty preserved)
  - Region does not contain recognizable switch → return null
  - Bounds.IsValid == false → return null (no classification attempted)

This preserves the Runtime's qualitative evidence model:
  true/false → Container.ObjectStateBeliefs populated
  null       → Container.ObjectStateBeliefs remains UNKNOWN
                → Agent returns StateEvidenceRequired (safe, no blind toggle)
```

### 1.5 Stateless

```
StateClassifier is a static class with pure functions.
No instance state. No mutable fields. No singleton.
Same pattern as PageAnalysis, ElementAnalysis, SemanticReconciliation.

Rationale: StateClassifier produces EVIDENCE, not truth.
           Evidence is immutable and stateless.
           The consumer (Container) owns the mutable belief state (I-2).
```

---

## 2. Fast / Slow Boundary

### 2.1 Fast Path — Local Image Classifier

```
StateClassifier.Classify(screenshot, bounds)
        ↓
1. Crop switch region from screenshot using bounds
2. Classify using lightweight model (template match / small CNN / heuristic)
3. Return bool? based on confidence vs minConfidence

Invocation: ALWAYS — the fast path runs for every switch/toggle candidate.

Authority: Perception adapter (IEnvironment implementation).
           Populates ObservedElement.SwitchState during ObserveAsync.

Model: Lightweight. Options:
  - Template matching: compare against ON/OFF prototype crops. 
    Fastest. Device-specific prototypes needed.
  - Heuristic: count bright pixels in track region, detect circle position.
    Fastest. Theme-dependent.
  - Small CNN: ~100KB model. Trained on ON/OFF toggle crops.
    Robust across themes. One-time training cost.

For minimum purchase: heuristic or template match is sufficient.
CNN is a future enhancement if heuristic proves brittle.
```

### 2.2 Slow Path — VLM Fallback

```
Agent may invoke VLM when:
  - StateClassifier returned null (fast path uncertain)
  - Agent.RunSemanticGoalAsync received StateEvidenceRequired
  - AND the semantic goal is high-priority (Agent adjudicates)

VLM Contract:
  VLM.Classify(screenshot, bounds) → SemanticEvidence
    Claim:  "{ObjectIdentity}.Enabled = true/false"
    Stance: Supports / Contradicts / Insufficient
    Reason: "VLM: toggle circle is on the right, track is blue → ON"

Authority:
  - VLM output is SemanticEvidence (I-14: AI is pluggable, not truth)
  - Agent receives VLM evidence, fuses with other evidence
  - Agent adjudicates — does NOT automatically accept VLM verdict

Invocation: Agent decision. NOT automatic. NOT in StateClassifier.
            Agent owns the slow-path invocation policy.

NOT IN MINIMUM PURCHASE:
  The slow path is documented here for boundary clarity only.
  The minimum purchase implements ONLY the fast path.
```

### 2.3 Boundary Diagram

```
FAST PATH (always, perception adapter):
  Screenshot + Bounds → StateClassifier.Classify() → bool?
        ↓
  ObservedElement.SwitchState populated
        ↓
  Container.RefreshObjectStateBeliefs → belief populated
        ↓
  Agent reads belief → Satisfied / Dispatch / StateEvidenceRequired


SLOW PATH (conditional, Agent-invoked):
  Agent receives StateEvidenceRequired
        ↓
  Agent decides: is this goal important enough for VLM?
        ↓ YES
  VLM.Classify(screenshot, bounds) → SemanticEvidence
        ↓
  Agent fuses VLM evidence with existing evidence
        ↓
  Agent adjudicates → retry semantic loop with new evidence
```

---

## 3. Minimum Falsifying Scenarios

### 3.1 F1 — Correct ON Detection

```
Given: Screenshot with a visually ON toggle at known bounds
       (B1 golden: Airplane mode switch, visually ON)
When:  StateClassifier.Classify(screenshot, bounds)
Then:  Returns true
       Confidence ≥ minConfidence (0.80)

Pass: Classifier correctly identifies a standard ON toggle.
```

### 3.2 F2 — Correct OFF Detection

```
Given: Screenshot with a visually OFF toggle at known bounds
       (Synthetic or captured: Wi‑Fi switch, visually OFF)
When:  StateClassifier.Classify(screenshot, bounds)
Then:  Returns false
       Confidence ≥ minConfidence (0.80)

Pass: Classifier correctly identifies a standard OFF toggle.
```

### 3.3 F3 — Visually Ambiguous Switch

```
Given: Screenshot with a partially-animated toggle (mid-transition)
       OR a toggle with unusual/custom styling
When:  StateClassifier.Classify(screenshot, bounds)
Then:  Returns null
       Confidence < minConfidence

Pass: Uncertainty is preserved. No false ON/OFF claim.
```

### 3.4 F4 — Wrong Crop (Non-Switch Region)

```
Given: Screenshot + bounds of a text label or icon (not a switch)
When:  StateClassifier.Classify(screenshot, textBounds)
Then:  Returns null
       Region does not contain a recognizable switch

Pass: Classifier does not hallucinate ON/OFF for non-toggle regions.
```

### 3.5 F5 — Invalid Bounds

```
Given: ElementBounds with IsValid == false (negative, > 1, inverted)
When:  StateClassifier.Classify(screenshot, invalidBounds)
Then:  Returns null
       No classification attempted

Pass: Invalid bounds are rejected before any image processing.
```

### 3.6 F6 — Resolution Independence

```
Given: Same toggle at 1080×2400 and 1440×3168
       (normalized bounds produce same relative crop region)
When:  StateClassifier.Classify(screenshot1080, bounds)
       StateClassifier.Classify(screenshot1440, bounds)
Then:  Same classification result
       Both ON or both OFF (not one ON and one OFF)

Pass: Normalized bounds produce resolution-independent classification.
```

### 3.7 F7 — Stateless

```
Given: Same screenshot + bounds
When:  StateClassifier.Classify(screenshot, bounds) called twice
Then:  Same result both times
       No internal state affects the outcome

Pass: Deterministic pure function.
```

---

## 4. Architecture Validation

### 4.1 Frozen Ownership — CONFIRMED UNCHANGED

| Component | Impact | Evidence |
|---|---|---|
| **Agent** | **NONE** | `RunSemanticGoalAsync` already handles `bool?` belief correctly. Agent may invoke VLM slow path (future) — but that's Agent's existing adjudication authority. |
| **Container** | **NONE** | `RefreshObjectStateBeliefs` already reads `ObservedElement.SwitchState`. No method signature change. |
| **Traversal** | **NONE** | `LowerAction` already checks `SwitchState` for safety (null → StateUnknown). No change. |
| **Environment** | **NONE** | IEnvironment contract unchanged. Perception adapter implementation populates `SwitchState` — this is the adapter's responsibility, not a contract change. |
| **Domain types** | **NONE** | `ObservedElement.SwitchState` field already exists. `ElementBounds` already exists. No new types in Runtime Model. |

### 4.2 Frozen Dependencies — CONFIRMED UNCHANGED

```
Agent → Container → Traversal → Environment  ✓ unchanged
StateClassifier depends on: platform image type + ElementBounds  ✓ Model only
No upward dependency introduced  ✓
```

### 4.3 Frozen Rules — CONFIRMED RESPECTED

| Rule | Status |
|---|---|
| Agent semantic authority | ✓ Agent still decides, not StateClassifier |
| Container state ownership | ✓ Container still owns ObjectStateBeliefs |
| Traversal execution authority | ✓ Traversal still lowers |
| No shared mutable state | ✓ StateClassifier is stateless |
| Evidence ≠ Truth | ✓ Classifier produces bool?, not semantic verdict |
| I-14: AI pluggable | ✓ VLM slow path is optional, not required |

---

## 5. Implementation Budget

### 5.1 New Files

| File | Purpose |
|---|---|
| `src/UniClaw.Runtime/Perception/StateClassifier.cs` | Static class. `Classify(screenshot, bounds, minConfidence) → bool?`. Fast-path image classification. |
| `tests/UniClaw.Runtime.Tests/Perception/StateClassifierTests.cs` | F1-F7 falsifier tests. Uses B1 golden screenshot crop + synthetic crops. |

### 5.2 Modified Files

| File | Change |
|---|---|
| `tests/.../Fakes/ScriptedEnvironment.cs` | `ObserveAsync` optionally invokes `StateClassifier` for elements with `PerceptionType` in ("switch", "toggle"). Populates `SwitchState`. |
| `tests/.../Fakes/RealitySeededSettingsFixture.cs` | May wire `StateClassifier` into observation path for reality-seeded tests. |

### 5.3 Tests Required

| ID | Test | Type |
|---|---|---|
| F1 | Correct ON detection | Reality (B1 golden) |
| F2 | Correct OFF detection | Reality-seeded |
| F3 | Ambiguous switch → null | Synthetic |
| F4 | Non-switch crop → null | Synthetic |
| F5 | Invalid bounds → null | Unit |
| F6 | Resolution independence | Unit |
| F7 | Deterministic replay | Unit |
| E2E | End-to-end: perception → Container belief | Integration |

### 5.4 Runtime Model Changes

```
NONE.

ObservedElement.SwitchState: already exists (bool?).
ElementBounds: already exists.
Container.RefreshObjectStateBeliefs: already consumes SwitchState.
Agent.RunSemanticGoalAsync: already handles true/false/null.
Traversal.LowerAction: already enforces safety on null.
```

### 5.5 Architecture Guard Impact

```
ArchitectureGuardTests:
  - No new project references (StateClassifier is in Runtime)
  - No legacy namespace references
  - No coordinate/hierarchy model declarations (ElementBounds already allowlisted)
  - No new Trap types
  - No RecoveryRequest type
  - Guard 6 (coordinate model) may need StateClassifier allowlist entry
    IF StateClassifier type name matches coordinate regex (unlikely: "StateClassifier")
```

---

## 6. Summary

```
STATE_CLASSIFIER_IMPLEMENTATION_GATE_REQUIRED

Contract:
  StateClassifier.Classify(screenshot, bounds, minConfidence=0.80) → bool?
  - true:  ON,  confidence ≥ threshold
  - false: OFF, confidence ≥ threshold
  - null:  uncertain, invalid, or not-a-switch

Fast/Slow:
  FAST (always):  image classifier → bool?  (minimum purchase)
  SLOW (conditional, future): VLM → SemanticEvidence  (Agent-invoked)

Runtime Architecture Impact:  NONE
  - ObservedElement.SwitchState already exists
  - Container.RefreshObjectStateBeliefs already consumes it
  - Agent.RunSemanticGoalAsync already handles true/false/null
  - Traversal.LowerAction already enforces safety on null
  - StateClassifier is stateless, perception-side

Implementation Budget:
  - 1 new production file (StateClassifier.cs)
  - 1 new test file
  - 2 modified test fixture files (ScriptedEnvironment, RealitySeededSettingsFixture)
  - 0 modified production files
  - 8 tests
  - 0 new Runtime model types

Frozen rules: ALL RESPECTED.

Next:
  PURCHASE_STATE_CLASSIFIER — implement minimum image classifier.

STOP.
```
