# Upstream Interaction Surface Evidence Challenge

> Generated: 2026-08-10
> Role: Runtime Architecture Analyst
> Baseline: `docs/decisions/unified-spatial-evidence-challenge.md` (Finalized) · `docs/decisions/environment-spatial-action-mapping-result.md`
> Inputs: B1 real-device golden (PKJ110, 1440×3168) · A3 EP-04 analysis.jsonl (InternetPage) · fusion.py · label-mapping.json · RealitySeededSettingsFixture
> Scope: Fact-finding only. No ElementAnalysis design. No SemanticElement implementation.
> Question: Does the INTERACTION_SURFACE_GAP belong to observation representation or perception evidence?

---

## 1. Reality Asset Used

**RealityAssetUsed: B1 real-device golden (PKJ110, Chinese ROM, 1440×3168)**

File: `uni-claw/artifacts/local-vision/settings-real.android-ui.yolo.evidence.json`
32 candidates from YOLO + OCR fusion. Types: `menu_item`, `switch`, `toggle`, `icon`, `text_block`, `button`, `input`.

**Supplemented by:** A3 EP-04 analysis.jsonl (InternetPage, 14 items, type=toggle present at x=0.8986).

---

## 2. End-to-End Trace: Wi‑Fi Row Through the Pipeline

### 2.1 Raw Screenshot → YOLO Detection

B1 Settings root page (1440×3168 screenshot):

| YOLO Detection | Type | Position (normalized) | Text (OCR match) |
|---|---|---|---|
| Detection [7] | **`switch`** | x1=0.8055, y1=0.3948 | **empty** (no OCR token matches) |
| Detection [24] | `menu_item` | x1=0.2257, y1=0.3999 | "飞行模式" (Airplane mode) |
| Detection [25] | `menu_item` | x1=0.2167, y1=0.4656 | "WLAN" (Wi‑Fi entry) |

**Key finding:** YOLO produces a SEPARATE `switch` detection at the right side (x=0.80) of the row. It has its OWN bounds. It is typed `switch` by the detector (Deki-Yolo label → `switch`).

The switch and the row text are TWO DISTINCT YOLO detections with TWO DISTINCT bounding boxes.

### 2.2 YOLO → Fusion

`fusion.py` processes each YOLO detection independently:

1. Each detection in `DEFAULT_INTERACTIVE_LABELS` (includes `switch`, `toggle`, `menu_item`, `icon`, `checkbox`, `button`, `input`, `text_block`) becomes a candidate
2. OCR tokens are matched to each detection by spatial proximity (`_match_score`, max_distance = screen_diag × 0.055)
3. **No merging of multiple YOLO detections** — each detection is one candidate
4. **The switch detection gets empty text** — the right-side toggle position has no OCR text nearby

Fusion output for the Airplane mode row:

```
Candidate A: type=switch,    text="",          bounds=(0.8055, 0.3948, ...)
Candidate B: type=menu_item, text="飞行模式",    bounds=(0.2257, 0.3999, ...)
```

**Fusion does NOT merge the switch into the row.** They remain separate candidates.

### 2.3 Fusion → analysis.jsonl

The analysis.jsonl (A3 EP-04 InternetPage) preserves type labels:

```
Item: name=''        type='toggle'   x=0.8986  y=0.4196  action='toggle'
Item: name='Wi-Fi'   type='menuItem' x=0.1125  y=0.4205  action='navigate'
```

Types: `text`, `menuItem`, `toggle`. Each item has a center point (x, y) and an `expectedAction`.

**Type labels survive fusion and are recorded in analysis artifacts.**

### 2.4 analysis.jsonl → Runtime ObservedElement

THIS IS WHERE THE LOSS OCCURS:

| Upstream Field | Survives to ObservedElement? |
|---|---|
| `name` (text) | ✅ → `Text` |
| `type` (menuItem/toggle/switch/text) | ❌ **DISCARDED** |
| `x`, `y` (center) | ✅ → `ElementBounds.CenterX/Y` (NEW purchase) |
| `bounds` (bbox) | ✅ → `ElementBounds` (NEW purchase) |
| `expectedAction` | ❌ **DISCARDED** |
| SwitchState (ON/OFF) | ❌ **NOT IN UPSTREAM** — derived from fixture config |

**SwitchSpecificBounds: YES** — upstream has separate switch/toggle bounds.

**SourceType/role evidence: DISCARDED** at Runtime boundary.

---

## 3. Row vs Switch Reconstruction

### 3.1 Airplane Mode Row (B1 — best available reality)

| Region | Upstream? | Bounds (normalized) | Type Label | Text |
|---|---|---|---|---|
| **ROW_BOUNDS** | PARTIAL — row is implicit from co-located y positions | y≈0.395-0.400 | N/A (no "row" type) | N/A |
| **TITLE_BOUNDS** | YES — separate detection | x=0.2257, y=0.3999 | `menu_item` | "飞行模式" |
| **SUBTITLE_BOUNDS** | NO — no subtitle in this row | — | — | — |
| **SWITCH_BOUNDS** | **YES** — separate detection | x=0.8055, y=0.3948 | `switch` | "" (empty) |
| **CURRENT_RUNTIME_BOUNDS** | FUSED in test fixture | N/A — synthetic only | — | SwitchState from config |

### 3.2 Wi‑Fi Settings Entry (B1)

| Region | Upstream? | Bounds (normalized) | Type Label | Text |
|---|---|---|---|---|
| **ROW_BOUNDS** | PARTIAL | y≈0.466 | N/A | N/A |
| **TITLE_BOUNDS** | YES | x=0.2167, y=0.4656 | `menu_item` | "WLAN" |
| **SUBTITLE_BOUNDS** | NO — not in this frame | — | — | — |
| **SWITCH_BOUNDS** | **NO** — this is Settings ROOT; WLAN is a NavigableContainer, not a StateChangingControl | — | — | — |

### 3.3 InternetPage Wi‑Fi Entry (A3 EP-04)

| Region | Upstream? | Bounds (normalized) | Type Label | Text |
|---|---|---|---|---|
| **TITLE_BOUNDS** | YES | x=0.1125, y=0.4205 | `menuItem` | "Wi-Fi" |
| **SUBTITLE_BOUNDS** | YES | x=0.3028, y=0.5170 | `menuItem` | "AndroidWifi" |
| **TOGGLE_BOUNDS** | YES (empty Mobile Data toggle) | x=0.8986, y=0.4196 | `toggle` | "" (empty) |

---

## 4. SwitchState Origin

### 4.1 Trace

```
Upstream perception:    NO SwitchState detection
                        YOLO detects switch/toggle presence (type label)
                        YOLO does NOT detect ON/OFF state
                        OCR does NOT detect state text
                        
analysis.jsonl:         NO SwitchState field
                        expectedAction='toggle' but no state value

Runtime fixture:        SwitchState HARDCODED in ElementConfig
                        RealitySeededSettingsFixture:
                          E("Wi‑Fi", false, ...)  → SwitchState = false
                          E("Wi‑Fi", true, ...)   → SwitchState = true
                          
Runtime perception:     NO SwitchState from real perception
                        Would need: OCR of "ON"/"OFF" text
                        OR: visual state detection (future VLM)
                        OR: accessibility state query (not available)
```

### 4.2 Verdict

**SwitchStateOrigin: CALLER-CONFIGURED (test fixtures) / NOT FROM PERCEPTION**

There is NO SwitchState in the upstream perception pipeline for real devices. The Runtime's `ObservedElement.SwitchState` is populated from:
- Test fixtures: hardcoded per `ElementConfig`
- Real device: would need OCR of state indicator text OR VLM OR accessibility API

**SwitchStateGeometryAligned: PARTIAL**

- In the B1 golden: the `switch` YOLO detection at x=0.80 is SEPARATE from the text at x=0.22
- SwitchState (ON/OFF) is NOT produced by this detection
- If SwitchState were available, it would need to be associated with the switch candidate, not the text candidate
- Currently in Runtime fixtures: SwitchState is attached to the SAME ObservedElement that carries the text (e.g., `ObservedElement("Wi‑Fi", false, 0)`) — NOT the switch-only candidate

---

## 5. Source Type / Role Evidence

### 5.1 Upstream Type Labels

| Label | Source | Classification | Reason |
|---|---|---|---|
| `menu_item` | YOLO → fusion → analysis.jsonl | **RAW_PROVIDER_LABEL** | Detector output. Often correct but unreliable (subtitle phantom: "Bluetooth, pairing" → menuItem at 91.9% rate). |
| `switch` | YOLO → fusion | **STRUCTURAL_EVIDENCE** | Indicates a switch UI control. Strong evidence for interaction surface type. Discarded before Runtime. |
| `toggle` | YOLO → fusion → analysis.jsonl | **STRUCTURAL_EVIDENCE** | Indicates a toggle control. Discarded before Runtime. |
| `text` / `text_block` | YOLO → fusion → analysis.jsonl | **RAW_PROVIDER_LABEL** | Non-interactive text. Chevron heuristic may upgrade to menuItem. |
| `icon` | YOLO → fusion | **STRUCTURAL_EVIDENCE** | Visual icon. Used in chevron heuristic for row detection. Discarded. |
| `button` | YOLO → fusion | **STRUCTURAL_EVIDENCE** | Button control. Discarded. |
| `input` | YOLO → fusion | **STRUCTURAL_EVIDENCE** | Text input field. Discarded. |

### 5.2 Key Distinction

```
provider says "switch"
        ≠
Runtime truth "this semantic target supports SetDesiredState"
```

The type label is EVIDENCE, not authority. But discarding it entirely removes a critical signal for:
- Distinguishing interaction surfaces (switch vs row text)
- Associating SwitchState with the correct candidate (switch, not row)
- TypeLevelDispatch (which currently derives category from `SwitchState != null → StateChangingControl`, losing the original type signal)

---

## 6. Fusion Behavior

### 6.1 Merge Rules

**Fusion does NOT merge multiple YOLO detections.** Each detection → one candidate.

The chevron heuristic (`_apply_chevron_heuristic`) DOES modify types:
- OCR `text_block` on the same row as a YOLO `icon`/`switch`/`toggle`/`checkbox` → reclassified as `menu_item`
- This is a TYPE reclassification, not a bounds merge
- The chevron icon detection and the text candidate remain SEPARATE

**FusionBehavior: NO_MERGE — each YOLO detection remains a separate candidate. Type reclassification only (text_block → menu_item via chevron heuristic).**

### 6.2 Information Loss Location

**InformationLossLocation: AT_RUNTIME_BOUNDARY**

The upstream pipeline preserves:
- Separate candidates per YOLO detection ✓
- Type labels (menuItem, toggle, switch, text) ✓
- Per-candidate bounds (normalized [0,1]×[0,1]) ✓
- Expected action hints (navigate, toggle, none) ✓

All of these are DISCARDED when constructing `ObservedElement` for the Runtime. Only `Text` and `SwitchState` survive, plus the newly-purchased `Bounds`.

**BEFORE_RUNTIME:** No loss. Fusion preserves per-detection candidates.
**AT_RUNTIME_BOUNDARY:** Type labels, expected actions, and the association between switch-bounds and row-bounds are discarded.
**BOTH:** N/A — no loss before Runtime.

---

## 7. Gap Classification

### 7.1 Primary Gap

**GapClassification: OBSERVATION_REPRESENTATION_GAP**

Upstream perception HAS:
- Separate switch/toggle bounds ✓
- Type labels (switch vs menuItem vs toggle vs text) ✓
- Per-candidate bounds (not merged into coarse row regions) ✓

Runtime Observation LOSES:
- Type labels → no way to know an element is a switch vs a row entry
- Switch-specific bounds → the switch candidate at x=0.80 is a separate ObservedElement, but with empty text and no type label, it's invisible to Traversal.Select
- Expected action → no way to dispatch SetSwitch to the right candidate

### 7.2 Secondary Gap

**PERCEPTION_EVIDENCE_GAP for SwitchState**

SwitchState (ON/OFF) is NOT detected by current perception. It must come from:
- OCR of state indicator text (e.g., "ON"/"OFF")
- Visual state detection (future VLM)
- Accessibility API (not available in current pipeline)
- Caller configuration (current approach, synthetic only)

### 7.3 Why Not PERCEPTION_FUSION_GAP

Fusion does NOT merge candidates. Each detection stays separate. The loss is at the Runtime boundary, not in fusion. Classification is OBSERVATION_REPRESENTATION_GAP, not PERCEPTION_FUSION_GAP.

---

## 8. Executable Falsifier

**UPSTREAM_INTERACTION_SURFACE_PRESERVATION_FALSIFIER**

Given the B1 real-device golden:

```
YOLO Detection [7]:  type=switch,    bounds=(0.8055, 0.3948, ...),  text=""
YOLO Detection [24]: type=menu_item, bounds=(0.2257, 0.3999, ...),  text="飞行模式"
```

**Current Runtime behavior (AFTER spatial purchase):**
- Detection [7] → `ObservedElement { Text="", SwitchState=null, Index=7, Bounds=(0.80, 0.39, ...) }`
- Detection [24] → `ObservedElement { Text="飞行模式", SwitchState=null, Index=24, Bounds=(0.22, 0.40, ...) }`

**Problem:** The switch has empty text → Traversal.Select cannot ground a SetSwitch action to it by Text matching. The switch's Bounds ARE preserved (separate from the row), but the Runtime has no way to SELECT it.

**Without type label evidence:**
- Tap "飞行模式" → targets Index 24 (row text bounds) ✓
- SetSwitch "飞行模式" true → **CANNOT TARGET THE SWITCH** — the switch at Index 7 has empty text, and Index 24 is the row text, not the switch

**With type label evidence preserved:**
- Tap "飞行模式" → targets Index 24 (row text bounds) ✓
- SetSwitch → TypeLevelDispatch can identify Index 7 as `type=switch` → targets switch bounds ✓
- SwitchState (ON/OFF) still UNRESOLVED (PERCEPTION_EVIDENCE_GAP) but at least the interaction surface is targetable

**Pass:** Upstream has separate switch bounds; Runtime loses the ability to target them because type labels are discarded.

**The falsifier proves:** The INTERACTION_SURFACE_GAP is an OBSERVATION_REPRESENTATION_GAP (type labels exist upstream, discarded at Runtime boundary), compounded by a PERCEPTION_EVIDENCE_GAP for SwitchState.

---

## 9. Center Point Policy

**CenterPointMappingValid: YES** — Bounds.Center is a valid default interaction point.

**NormalizationProblem: NO** — normalized coordinates are correct and consistent.

The only issue is WHICH Bounds get mapped:
- Current: row text bounds (the only matchable candidate)
- Needed: switch-specific bounds (invisible to Traversal.Select due to empty text + no type label)

---

## 10. Minimum Purchase Options

### Option A — PRESERVE EXISTING UPSTREAM EVIDENCE (SELECTED)

Preserve `type` label as `SourceType` on `ObservedElement`. This:
- Makes switch/toggle candidates identifiable (even with empty text)
- Enables TypeLevelDispatch to associate SetSwitch with the switch candidate, not the row text
- Does NOT solve SwitchState detection (remains PERCEPTION_EVIDENCE_GAP)

### Option B — PRESERVE PROVIDER ROLE AS EVIDENCE

Same as Option A, but explicitly documented as "provider evidence, not semantic truth."

### Option C — CHANGE FUSION OUTPUT

Not needed — fusion already preserves separate candidates. Loss is at Runtime boundary.

### Option D — ELEMENT ANALYSIS

Premature. Representation gap must be closed first.

### Option E — PERCEPTION ENHANCEMENT

Needed for SwitchState detection, but separate from the representation gap.

**MinimumNextPurchase: OPTION A — Preserve `type` label as `SourceType` on `ObservedElement`**

This is the smallest step that makes switch candidates targetable. Combined with the already-purchased `Bounds`, this enables SetSwitch to target the switch-specific interaction surface instead of the row text region.

---

## 11. Architecture Boundary

```
Perception:     produces raw type labels (switch, menuItem, toggle, text)
                        ↓
Observation:    preserves type label as SourceType (evidence, not authority)
                        ↓
ElementAnalysis: future — interprets type + bounds → interaction surface
                        ↓
Traversal:      Select targets by Text; TypeLevelDispatch may use SourceType
                        ↓
Environment:    maps selected Bounds → physical coordinates
```

**Environment does NOT compensate for missing perception semantics.** If SourceType is absent (legacy), Environment falls back to the existing Index-based or Bounds-center path.

---

## Summary

```
UPSTREAM_INTERACTION_SURFACE_EVIDENCE_CHALLENGE_RESULT

Status: READY

ModelRouting:
  HaikuWork:
    - B1 real-device golden: YOLO produces type=switch as SEPARATE detection at x=0.80,
      separate from row text at x=0.22. Switch has empty text (no OCR match).
    - A3 EP-04 analysis.jsonl: type=toggle at x=0.8986, type=menuItem at x=0.1125.
      Types survive fusion. Types are recorded.
    - fusion.py: NO merge of multiple YOLO detections. Each stays separate.
      Chevron heuristic reclassifies text_block→menu_item (type only, not bounds).
    - Runtime boundary: type labels, expectedAction, per-candidate association DISCARDED.
    - SwitchState: NOT from perception. Hardcoded in test fixtures.
      PERCEPTION_EVIDENCE_GAP for real-device ON/OFF state.
    - Falsifier: switch candidate at Index 7 (type=switch, bounds at x=0.80) is invisible
      to Traversal.Select because Text="" — the Runtime CANNOT target the switch interaction surface.

  OpusDecisions:
    - Primary gap: OBSERVATION_REPRESENTATION_GAP — type labels exist upstream, discarded at Runtime.
    - Secondary gap: PERCEPTION_EVIDENCE_GAP — SwitchState not detected by perception.
    - NOT PERCEPTION_FUSION_GAP — fusion preserves separate candidates.
    - Minimum purchase: preserve type label as SourceType on ObservedElement.
    - SourceType is evidence, not semantic truth. Provider says "switch" ≠ Runtime truth "supports SetDesiredState."
    - Architecture Delta: NONE — backward-compatible nullable field.

RealityAssetUsed:
  B1 real-device golden (PKJ110, 1440×3168, settings-real.android-ui-yolo.evidence.json)
  + A3 EP-04 analysis.jsonl (InternetPage, 14 items, type=toggle present)

SwitchSpecificBounds: YES
  B1 golden: YOLO detection type=switch at x=0.8055, y=0.3948 — SEPARATE bounds from row text.
  A3 EP-04: type=toggle at x=0.8986, y=0.4196 — SEPARATE from Wi‑Fi entry at x=0.1125.
  Upstream has distinct switch/toggle bounds. Not merged at fusion.

RowBounds:
  PARTIAL — row is implicit from co-located y positions (y≈0.395-0.400 for Airplane mode row).
  No explicit "row" type. Row membership is inferable from same-y + sequential-x patterns.

TitleBounds: YES
  B1: "飞行模式" at x=0.2257, y=0.3999 (menu_item, separate bounds)
  B1: "WLAN" at x=0.2167, y=0.4656 (menu_item, separate bounds)

SubtitleBounds: YES (where applicable)
  A3: "AndroidWifi" at x=0.3028, y=0.5170 (menu_item, separate bounds from Wi‑Fi entry)

SwitchBounds: YES
  B1: switch at x=0.8055, y=0.3948 (separate YOLO detection, type=switch, empty text)
  A3: toggle at x=0.8986, y=0.4196 (type=toggle, empty text)

CurrentRuntimeBoundsRepresents:
  FUSED in test fixtures (Text + SwitchState on same ObservedElement).
  For B1 real data: switch would be a SEPARATE ObservedElement with Text="" and Bounds at x=0.80.
  Row text would be a separate ObservedElement with Text="飞行模式" and Bounds at x=0.22.
  But switch is invisible to Traversal.Select (empty text).

SwitchStateOrigin:
  CALLER-CONFIGURED (test fixtures). NOT FROM PERCEPTION.
  RealitySeededSettingsFixture hardcodes SwitchState per ElementConfig.
  B1 golden: YOLO detects switch/toggle presence (type label) but NOT ON/OFF state.
  Real-device SwitchState requires: OCR of state text, VLM, or accessibility API.

SwitchStateGeometryAligned: PARTIAL
  In B1: switch YOLO detection at x=0.80 is geometrically separate from text at x=0.22.
  But SwitchState (if available) would need to be associated with the switch candidate,
  not the text candidate. Currently in fixtures, SwitchState is on the same element as text.

UpstreamRoleEvidence:
  RAW_PROVIDER_LABEL: menu_item, text, text_block
  STRUCTURAL_EVIDENCE: switch, toggle, icon, button, input
  SEMANTIC_ROLE_CANDIDATE: (none — all are provider labels, not Runtime semantic roles)
  UNRELIABLE: menu_item (subtitle phantom 91.9% rate)
  ABSENT: row, container, group (no explicit grouping types)

FusionBehavior:
  NO_MERGE — each YOLO detection → one candidate.
  Chevron heuristic: type reclassification only (text_block→menu_item when same-row YOLO icon).
  No bounds merge. No candidate merge. No SwitchState attachment.

InformationLossLocation: AT_RUNTIME_BOUNDARY
  Upstream fusion preserves per-detection candidates with type labels and bounds.
  Runtime ObservedElement drops: type label, expectedAction, per-candidate association.
  BEFORE_RUNTIME: no loss. AT_RUNTIME_BOUNDARY: type + action discarded.

GapClassification:
  OBSERVATION_REPRESENTATION_GAP (primary)
  + PERCEPTION_EVIDENCE_GAP (secondary, for SwitchState)

CenterPointMappingValid: YES

NormalizationProblem: NO

ExecutableFalsifier:
  B1 Airplane Mode row: YOLO detection type=switch at (0.8055, 0.3948) with text="".
  Runtime constructs ObservedElement { Text="", SwitchState=null, Bounds=(0.80,0.39,...) }.
  Traversal.Select cannot ground SetSwitch to this element (Text="" matches nothing).
  The switch interaction surface is PRESENT in upstream data but INACCESSIBLE to Runtime.
  PASS: upstream has switch bounds; Runtime cannot target them → OBSERVATION_REPRESENTATION_GAP.

MinimumNextPurchase:
  PRESERVE_TYPE_LABEL_AS_SOURCETYPE
  Add SourceType (string?) to ObservedElement — carries upstream type label
  (switch/toggle/menuItem/text) as provider evidence, NOT as semantic truth.
  Combined with existing Bounds, enables Traversal/TypeLevelDispatch to:
    - Identify switch/toggle candidates (even with empty text)
    - Target switch-specific interaction surfaces for SetSwitch
    - Preserve provider label without promoting it to Runtime semantic authority
  Does NOT solve SwitchState — that remains PERCEPTION_EVIDENCE_GAP.
  Architecture Delta: NONE — backward-compatible nullable field.

STOP.
```
