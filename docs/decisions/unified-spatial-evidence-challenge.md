# Unified Spatial Evidence Challenge

> Generated: 2026-08-10
> Role: Runtime Architecture Analyst
> Baseline: `docs/decisions/page-analysis-semantic-contract-challenge.md` (Finalized)
> Inputs: Observation/ObservedElement · fusion.py · analysis.jsonl (A3/A4) · B1 real-device golden · DeviceAction/Tap · ScriptedEnvironment · IEnvironment port
> Scope: Analysis + executable falsifier only. No production architecture expansion unless a pure-representation minimal gap is found.

---

## 1. Current Coordinate Inventory

### 1.1 Coordinate Spaces (Upstream — Perception Pipeline)

| Space | Origin | W×H | Transform | Invertible? | Obs-Linked? | Used By |
|---|---|---|---|---|---|---|
| **Device Screenshot** | Top-left (0,0) | 1440×3168 (B1 PKJ110); 1080×2400 (A3 emulator) | — | — | No | Raw perception |
| **Vision Input (resized)** | Top-left (0,0) | ~640×1408 (varies by model) | Screenshot → resize | Yes (scale factor) | No | YOLO inference, OCR |
| **YOLO Bounding Box** | Top-left (0,0) in vision-input pixels | Vision W×H | Detection in vision-input space | Via resize → device pixels | No | Element detection |
| **OCR Bounding Box** | Top-left (0,0) in vision-input pixels | Vision W×H | Token detection in vision-input space | Via resize → device pixels | No | Text detection |
| **Fusion Normalized** | Top-left (0,0) | [0,1]×[0,1] | `box.normalized(image_width, image_height)` | Yes (multiply by vision W×H, then un-resize) | No | Candidate generation (`fusion.py:75`) |
| **Fusion Pixel** | Top-left (0,0) | Vision W×H | `round(box.x1/y1/x2/y2)` | Yes (un-resize) | No | Debug/recording (`fusion.py:76-80`) |
| **analysis.jsonl** | Top-left (0,0) | [0,1]×[0,1] | Normalized center point (x, y) | Yes (× vision W×H → pixel, then un-resize → device) | No | Legacy recording |
| **A4 E-10 Fixture** | Top-left (0,0) | [0,1]×[0,1] | `.At(x, y)` normalized | Yes | No | Legacy test replay |

### 1.2 Coordinate Spaces (Runtime — After Observation Boundary)

| Space | Exists? | Details |
|---|---|---|
| **Observation viewport** | **NO** | Observation has no Width, Height, or coordinate reference frame |
| **ObservedElement bounds** | **NO** | ObservedElement has Text (string) + SwitchState (bool?) + Index (int). No x, y, width, height, bounds, or spatial reference. |
| **ObservedElement type label** | **NO** | No `type` field. SwitchState is a DERIVED signal (SwitchState≠null → StateChangingControl), not the original YOLO type label. |
| **Index** | **YES** | `int` — stable ordinal within the Observation. Documentation explicitly says "不是坐标" (not a coordinate — 裁决 3). |
| **DeviceAction coordinates** | **NO** | `Tap(int? TargetElementIndex)` uses Index, not coordinates. `ScrollForward` has no spatial parameters at all. |
| **Canonical Observation Space** | **NO** | No single coordinate frame defined. No viewport dimensions. No source spatial reference. |

### 1.3 The Spatial Boundary

```
UPSTREAM (perception pipeline — rich spatial data):
  analysis.jsonl: { name, type, x, y, expectedAction, ... }
  fusion.py: { bounds, boundsPx, center, centerPx, type, text, confidence }

        ↓ OBSERVATION BOUNDARY ↓
        ALL spatial data DISCARDED

RUNTIME (Observation model — no spatial data):
  ObservedElement: { Text, SwitchState?, Index }
  Observation: { Elements, ForegroundApplication?, SequenceNumber }
```

**GeometryLostBeforePageAnalysis: YES** — all spatial data (bounds, type labels, viewport dimensions, coordinate reference) is discarded at the Observation boundary, before PageAnalysis or any Runtime semantic capability can consume it.

---

## 2. Canonical Observation Space — Does Not Exist

### 2.1 Current State

There is NO canonical spatial frame in the Runtime Observation model. The `Index` field on `ObservedElement` is an ordinal (position in the element list), explicitly documented as "不是坐标" (not a coordinate — 裁决 3).

### 2.2 What a Canonical Space Would Require

```
Observation {
    Elements: ImmutableArray<ObservedElement>,
    ForegroundApplication: string?,
    SequenceNumber: long,
    ViewportWidth: int?,       // MISSING — canonical frame width
    ViewportHeight: int?,      // MISSING — canonical frame height
    CoordinateSpace: ...,      // MISSING — reference frame identity
}

ObservedElement {
    Text: string,
    SwitchState: bool?,
    Index: int,
    Bounds: ...,               // MISSING — element bounds in canonical space
    SourceType: string?,       // MISSING — original perception type label
}
```

### 2.3 Minimum Canonical Space Candidate

The simplest canonical space: **normalized [0,1]×[0,1] with origin at top-left.** This is what `analysis.jsonl` already stores (x, y in normalized space) and what `fusion.py` already produces (`bounds` as normalized). No new transform needed — the upstream pipeline already normalizes.

For the Runtime to adopt this:
1. Add `ViewportWidth`, `ViewportHeight` (nullable int) to `Observation`
2. Add `Bounds` (nullable, normalized [0,1]×[0,1] rect) to `ObservedElement`
3. Preserve backward compatibility — existing elements have null bounds

**This is the minimum spatial purchase candidate.** It adds NO new coordinate spaces, NO new transforms — it simply stops DISCARDING data that already exists upstream.

---

## 3. Reality Falsifiers

### 3.1 F1: SUBTITLE PHANTOM

**Evidence:** A3 EP-04 SettingsRoot, element [9]: "Bluetooth, pairing" at normalized position (x=0.3111, y=0.5786), type=`menuItem`, expectedAction=`navigate`.

**The phantom:** This is a SECTION HEADER / SUBTITLE, not an interactive menu item. It's classified as `menuItem` (91.9% rate across 123 pairs per VE-05) because:
1. The chevron heuristic (`fusion.py:139-142`) upgrades `text_block` to `menu_item` when OCR text is on the same row as a right-side YOLO icon
2. In the Runtime, there's NO spatial data to detect that this element is structurally different from "Network & internet" (a real menu item)

**Spatial signal that would expose it:** "Bluetooth, pairing" at y=0.5786 is on the SAME ROW as "Network & internet" at y=0.40? No — they're at different y. But the issue is: without spatial bounds, the Runtime cannot tell that "Bluetooth, pairing" has no interactive YOLO bounding box overlap and is positioned as a section divider, not a tappable row.

**Falsifier:** Given two elements with the same `type=menuItem` but different spatial context (one is a standalone row, one is a same-row subtitle), a text-only Runtime cannot distinguish them. A spatially-aware Runtime CAN detect: subtitle has no YOLO detection bounds overlap (or the YOLO detection is an icon, not the text).

### 3.2 F2: ROW + SWITCH

**Evidence:** A4 E-10 Internet page: Wi‑Fi entry at (0.5, 0.15) type=`menuItem` + Mobile data switch at (0.85, 0.28) type=`toggle`.

**The challenge:** The Wi‑Fi row contains: title text ("Wi‑Fi"), subtitle/SSID ("AndroidWifi"), and in the WifiPage: a SwitchState-bearing toggle. In the Runtime, these are THREE SEPARATE `ObservedElement` instances with NO spatial relationship information.

**Without spatial data:** "Wi‑Fi" (Index 6), "AndroidWifi" (Index 8), and the switch are peers in a flat list. The Runtime cannot represent "these three elements belong to the same semantic row."

**Spatial signal that would help:** Elements at similar y with sequential x positions form a row. Bounds overlap or proximity indicates grouping.

### 3.3 F3: DUPLICATE TEXT

**Evidence:** A3 SettingsRoot: "Network&internet" at Index 4 (y=0.40) and Index 6 (y=0.40) — SAME text, SAME y position (duplicate). A3 NetworkInternet: "Internet" at Index 1 (y=0.2938) and Index 3 — duplicate.

**Without spatial data:** Two elements with identical Text are indistinguishable. The Runtime picks the first one (deterministic Index-based selection). If the first "Internet" is wrong and the second is correct, the Runtime cannot distinguish them.

**Spatial signal that would help:** Different x positions or bounds distinguish duplicates. The correct "Internet" entry might be at x=0.26 while the duplicate might be at x=0.50 (different column or different visual position).

### 3.4 F4: POPUP VS PAGE

**Evidence:** Legacy `analysis.jsonl` has `isPopup` flag. Legacy `LocalVisionProvider` detects popups via `nonItemLabels` + ANR text. Runtime has `IsLocalObstructionHypothesis` but no spatial popup detection.

**Without spatial data:** A center-positioned modal overlay and a full-screen page are indistinguishable from text alone. Both have text elements. The Runtime relies on `IsStillMine(observation)` returning false + `reconciledSemanticPage is null` to detect obstruction — no spatial evidence.

**Spatial signal that would expose it:** Popup elements are clustered in the center of the screen (x: 0.2-0.8, y: 0.3-0.7). Full-screen pages have elements distributed across the full y range (0.05-0.95). Element count is typically lower for popups. Bounds distribution alone is a strong popup signal.

### 3.5 F5: SCROLL

**Evidence:** A3 trace-replay has 5 scroll actions among 19 total actions. Legacy `analysis.jsonl` tracks `hasScroll` and `isEndOfList`. The same semantic page at different scroll positions has DIFFERENT fingerprints (proving fingerprint ≠ identity).

**Without spatial data:** The Runtime CAN detect scroll via `TryVerifyViewportContinuity` (fresh seq + compatible foreground + IsStillMine + same reconciled page). But it has NO spatial evidence about WHAT changed — only that the element set changed.

**Spatial signal that would help:** y-range shift of elements indicates scroll direction and magnitude. New elements appearing at bottom y (0.8-0.95) while top elements disappear confirms downward scroll. y-range stability of persistent elements (e.g., headers at y=0.05-0.10) provides continuity evidence.

### 3.6 F6: ROI TRANSFORM

**Evidence:** Legacy `SnapshotComparer` uses ROI (region-of-interest) pixel comparison for scroll detection. The ROI is a sub-region of the screenshot. `fusion.py` uses `image_width`, `image_height` of the VISION INPUT (resized), NOT the original device screenshot.

**The transform chain:**
```
Device Screenshot (e.g., 1440×3168)
    ↓ resize
Vision Input (e.g., 640×1408)
    ↓ YOLO/OCR detection
Detection boxes in vision-input pixels
    ↓ normalize(image_width, image_height)
Normalized [0,1]×[0,1] bounds
    ↓ center point
analysis.jsonl: (x, y)
```

**To map back to device action coordinates:**
1. `normalized_center * (vision_width, vision_height)` → vision-input pixel center
2. `vision_pixel_center / resize_scale` → device screenshot pixel center
3. Device screenshot pixel → device touch coordinate (1:1 for most devices)

**This chain is COMPLETE in the perception pipeline but BROKEN at the Runtime boundary.** The Runtime receives only `Index` and has NO path from Index → device coordinates except through the Environment adapter's internal mapping.

---

## 4. PageAnalysis Impact

### 4.1 Current Capability Level

**PageAnalysisCurrentSemanticLevel: TEXT_ATTRIBUTE**

Current PageAnalysis sources operate on:
- `FOREGROUND`: ForegroundApplication string match
- `TEXT_ANCHOR`: Text string presence/absence in Elements
- `TEXT_ANCHOR_NEGATIVE`: Text string presence as contradiction
- `SWITCH_DISTRIBUTION`: SwitchState≠null on named text

All four sources use ONLY text and SwitchState. None use spatial position, bounds, element type labels, or structural layout.

### 4.2 What Spatial Data Would Enable

| Current (TEXT_ATTRIBUTE) | With Spatial Data (STRUCTURAL_SCREEN) |
|---|---|
| "Does text 'T-Mobile' exist?" | "Is 'T-Mobile' at y≈0.30 (menu area, not header)?" |
| "Does 'Wi‑Fi' have SwitchState?" | "Is the SwitchState-bearing 'Wi‑Fi' at x>0.7 (right-side toggle position)?" |
| "How many elements?" | "What's the y-distribution shape? (full-page vs popup vs list)" |
| "Is 'Auto-connect' present?" | "Is 'Auto-connect' at the expected y-position for WifiPage?" |
| Text-only duplicate resolution | Spatial proximity groups rows |

### 4.3 Impact Classification

**PageAnalysisImpact: USEFUL_LATER**

Spatial data would strengthen PageAnalysis from TEXT_ATTRIBUTE to STRUCTURAL_SCREEN level. However, for the CURRENT known-domain fast semantic recognition (4 Settings pages with distinctive text anchors), text+SwitchState is SUFFICIENT. The alias-collapse falsifier is already detected by multi-source text evidence disagreement.

**Not BLOCKING** because:
- Current PageAnalysis is KNOWN-DOMAIN (caller provides per-page text anchors)
- Text anchors alone distinguish all 4 EP-04 pages
- ForegroundApplication + text multiset is a strong signal

**HIGH_LEVERAGE for future** because:
- Spatial data would enable structural page signatures (element distribution shapes)
- Row/group detection → stronger element identity evidence
- Popup vs page distinction → obstruction detection without relying solely on IsStillMine
- Scroll magnitude/direction → viewport continuity evidence

---

## 5. Element Analysis Impact

**Without canonical geometry, future ElementAnalysis CANNOT truthfully:**

| Capability | Blocked? | Why |
|---|---|---|
| Title vs subtitle distinction | **YES** | Same text "Bluetooth" could be title or subtitle; only spatial position (y-offset from section header) and YOLO type label distinguish them |
| Row vs embedded switch | **YES** | "Wi‑Fi" text + SwitchState toggle are separate elements in a flat list; without spatial bounds, their row-membership is invisible |
| Semantic group identification | **YES** | No spatial proximity data → no grouping evidence |
| Interaction surface identification | **PARTIAL** | SwitchState≠null signals a toggle, but without spatial position, you can't tell WHICH "Wi‑Fi" element the SwitchState belongs to |
| Duplicate candidate disambiguation | **YES** | Two "Internet" candidates at different positions are identical in text+SwitchState+Index space; only spatial position distinguishes them |

**ElementAnalysisImpact: BLOCKING for precise element identity.** Text+SwitchState is sufficient for coarse element existence detection but insufficient for distinguishing same-text elements, identifying semantic groups, or detecting non-interactive phantom elements.

---

## 6. Grounding Impact

### 6.1 Current Grounding Chain

```
PlanStep.TargetDescription ("Wi‑Fi")
    ↓ Traversal.Select
Matches ObservedElement where Text == "Wi‑Fi"
    ↓
Selected Index (e.g., 6)
    ↓
DeviceAction.Tap(TargetElementIndex: 6)
    ↓ Environment.ExecuteAsync
ScriptedEnvironment maps Index → ScreenConfig element
    ↓
Simulated tap on that element
```

### 6.2 Chain Assessment

**GroundingCoordinateChain: PARTIAL**

The chain works for the ScriptedEnvironment because:
- The fake environment maps Index → element config internally
- Index is stable within a ScriptedEnvironment observation
- No real device coordinates are needed

The chain is BROKEN for real devices because:
- A real device needs SCREEN COORDINATES to tap
- Index has NO mapping to screen coordinates in the Runtime
- The Environment adapter would need to maintain its own Index→Coordinate mapping internally
- This mapping is NOT part of the Observation contract — it's an adapter implementation detail
- If the adapter's mapping drifts (element moves between observations), the Runtime has no way to detect it

### 6.3 First Missing Link

**semantic candidate → observation bounds**

The `ObservedElement` has no bounds field. Even if the Environment adapter internally maps Index → screen coordinate, that mapping is opaque to the Runtime. The Runtime cannot verify that the element it selected by Index is actually at the position the adapter will tap.

---

## 7. OpenWorld Impact

### 7.1 Assessment

**OpenWorldCanProceedWithoutSpatialPurchase: YES**

Current OpenWorld page semantic operations use:
- `_resolveSemanticPage(observation)` → page name string (for CreateContainer)
- `IsStillMine(observation)` → boolean (for continuity)
- `_belief.SemanticPage` string comparison (for parent return verification)

None of these require spatial data. PageAnalysis integration into OpenWorld (deferred from the production integration task) can proceed with TEXT_ATTRIBUTE level semantics.

**Why it can proceed:**
1. OpenWorld page identity currently depends on the old resolver's string verdict
2. Replacing it with PageAnalysis evidence (FOREGROUND + TEXT_ANCHOR) uses the same text-level signals
3. Child container creation needs a page name string — PageAnalysis provides evidence about candidate pages, which the resolver can still map to strings
4. Parent return verification compares page name strings — this is a naming operation, not a spatial one

**Spatial purchase would enhance (but is not required for):**
- Distinguishing parent pages from child pages when text overlap is high (F3: Persistent Header)
- Detecting popup obstructions during navigation (F4: Popup vs Page)
- Verifying scroll-based viewport changes (F5: Scroll)

---

## 8. Minimum Spatial Contract

### 8.1 The Gap

**Classification: OBSERVATION_REPRESENTATION_GAP**

Spatial data exists in the perception pipeline (bounds, type labels, viewport dimensions) but is discarded at the Observation boundary. The Runtime's `ObservedElement` has no spatial fields. This is a REPRESENTATION gap — the data exists upstream; the Runtime model simply doesn't carry it.

### 8.2 Minimum Purchase Candidate

The minimum spatial purchase adds NO new coordinate spaces, NO new transforms, NO new perception capabilities:

```
Observation:
  + ViewportWidth: int?     // canonical frame width (null = unknown)
  + ViewportHeight: int?    // canonical frame height (null = unknown)

ObservedElement:
  + Bounds: ElementBounds?  // normalized [0,1]×[0,1] bounds (null = unknown)
  + SourceType: string?     // original perception type label (null = unknown)

ElementBounds (immutable record):
  X1: float  // left edge, normalized [0,1]
  Y1: float  // top edge, normalized [0,1]
  X2: float  // right edge, normalized [0,1]
  Y2: float  // bottom edge, normalized [0,1]
```

**Why normalized [0,1]×[0,1]:**
- Already produced by `fusion.py` (`detection.box.normalized()`)
- Already stored in `analysis.jsonl` (x, y as center point)
- Resolution-independent — works across devices (B1 1440×3168, A3 1080×2400)
- Directly mappable to device coordinates: `deviceX = x * viewportWidth`, `deviceY = y * viewportHeight`
- No new transform infrastructure needed

**Why NOT pixel coordinates:**
- Device resolution varies (B1 vs A3 emulator)
- Vision input is resized — pixel coords are in VISION space, not device space
- Normalized coords are resolution-independent

**What this does NOT purchase (yet):**
- Element grouping / row detection
- Popup geometry detection
- Scroll magnitude quantification
- Spatial-based duplicate disambiguation

These are algorithmic concerns that CONSUME spatial data but don't require additional representation.

### 8.3 Architecture Delta

**ArchitectureDelta: NONE (for the representation gap identification)**

Adding optional nullable fields to `Observation` and `ObservedElement`:
- Backward compatible (existing elements have null bounds)
- No new project references
- No new boundaries
- No new owners
- Follows the existing pattern (SwitchState was added as `bool?` — Bounds would be `ElementBounds?`)

The actual addition of these fields is a SEPARATE purchase decision. This challenge only identifies the gap.

---

## 9. Coordinate Role

### 9.1 Frozen Principles

| Principle | Status |
|---|---|
| **Coordinate ≠ Element Identity** | FROZEN — position is evidence, not identity. Two elements at the same position may be different elements at different times. |
| **Bounds ≠ Page Identity** | FROZEN — element bounds distribution is structural evidence, not page identity. Same page at different scroll positions has different bounds distribution. |
| **Fingerprint ≠ Semantic Identity** | FROZEN — I-6. Already proven: fingerprint changes on scroll while semantic page is unchanged. |

### 9.2 What Coordinates MAY Serve As

| Role | Example |
|---|---|
| **Structural evidence** | "Elements clustered at y=0.3-0.5 with x=0.1-0.9 → likely a list section" |
| **Grouping evidence** | "Elements at y≈0.40 with sequential x → same row" |
| **Relation evidence** | "Toggle at x=0.85 is right-aligned sibling of text at x=0.26" |
| **Grounding evidence** | "Selected Index 6 has bounds (0.26,0.15)-(0.74,0.18) → tap center at (0.5,0.165)" |
| **Action localization** | "Map normalized center to device touch coordinate" |
| **Change evidence** | "y-range shifted by +0.15 → downward scroll of ~15% viewport" |

**CoordinateRole: SUPPORTING_EVIDENCE only. Never authoritative identity.**

---

## 10. Operational Semantic Identity Debt

### 10.1 Current State

`_resolveSemanticPage` still supplies operational page-name strings for:
- `CreateContainer(pageName)` — child container creation
- Parent return verification — `_belief.SemanticPage == parent.SemanticPageName`
- Child identity — `_belief.SemanticPage` → child page name
- Branch completion — `_resolveSemanticPage(childEvidence)` direct call
- OpenWorld operations — all page name string comparisons

### 10.2 Classification

**OperationalSemanticIdentityDebt: ACTIVE**

The old resolver is still the sole source of page-name strings for operational use. PageAnalysis produces SemanticEvidence about page identity claims, but Agent still needs a STRING to name Containers.

**This is NOT a spatial concern.** It's recorded here as architectural debt to correct the prior report.

### 10.3 Dual Truth Path Correction

**DualTruthPathRemaining: PARTIAL**

The prior production integration report classified this as "NO" with the rationale that the old resolver provides strings for container naming while PageAnalysis provides semantic belief. This is technically correct but understates the risk:

- If `_resolveSemanticPage` returns "WifiSub" and PageAnalysis evidence contradicts it, Agent STILL creates a Container named "WifiSub" (because `CreateContainer` needs a string)
- The Container's `_semanticPageName` is then "WifiSub" (immutable post-construction)
- Container's LOCAL_IDENTITY evidence (derived from `_identityRule`) will SUPPORT "page is WifiSub"
- PageAnalysis evidence may CONTRADICT "page is WifiSub" → CONTRADICTED belief

This is a real dual-truth scenario: the Container's NAME says "WifiSub" while the evidence says "not WifiSub." The belief state captures the contradiction, but the Container's identity string is not revisable in-place (requires CreateContainer + Bind = discard + rebuild).

**Resolution path:** When Container.LocalPageBeliefState is CONTRADICTED and external evidence strongly supports a different page, Agent should CreateContainer with the evidence-supported page name (not the old resolver's string). This is an Agent adjudication decision, not a spatial concern.

---

## 11. Summary

```
UNIFIED_SPATIAL_EVIDENCE_CHALLENGE_RESULT

ModelRouting:
  HaikuWork:
    - Coordinate inventory: 7 upstream spaces (device→vision→YOLO→OCR→fusion→analysis.jsonl→fixture),
      0 Runtime spaces (no bounds, no viewport dimensions, no type labels, no coordinate reference)
    - fusion.py trace: produces normalized bounds + pixel bounds + center for every candidate
    - analysis.jsonl trace: 185 frames with x, y, type, expectedAction per item
    - A4 E-10 fixtures: real coordinates from recorded reality (Wi‑Fi@(0.5,0.15), Switch@(0.85,0.28))
    - B1 real-device: 1440×3168, WLAN/Wi‑Fi alias, Chinese ROM
    - Falsifiers: 6 defined (subtitle phantom, row+switch, duplicate text, popup vs page, scroll, ROI transform)
    - Grounding chain: Index-based (works for ScriptedEnvironment, broken for real devices)
  OpusDecisions:
    - Canonical space: normalized [0,1]×[0,1] (already produced upstream, just discarded)
    - Minimum contract: add ViewportWidth/Height (int?) to Observation, Bounds (ElementBounds?) + SourceType (string?) to ObservedElement
    - Coordinate ≠ identity (frozen). Fingerprint ≠ identity (frozen). Bounds ≠ page identity (frozen).
    - Architecture Delta: NONE for gap identification; field additions are backward-compatible nullable options

CurrentCoordinateSpaces:
  UPSTREAM (7): Device Screenshot, Vision Input (resized), YOLO BBox, OCR BBox,
    Fusion Normalized [0,1]×[0,1], Fusion Pixel, analysis.jsonl Normalized
  RUNTIME (0): NO canonical space. NO bounds. NO viewport dimensions. NO type labels.
  Index is ordinal, not coordinate (裁决 3).

CanonicalMappingExists: PARTIAL
  Upstream: complete chain from device pixels → vision resize → YOLO/OCR boxes → normalized → analysis.jsonl
  Runtime: BROKEN — all spatial data discarded at Observation boundary
  Environment adapter: internal Index→coordinate mapping, opaque to Runtime

GeometryPreservedInObservation: NO
  ObservedElement has Text + SwitchState + Index only.
  No bounds, x, y, width, height, type label, or spatial reference.

GeometryLostBeforePageAnalysis: YES
  All spatial data (bounds, type, viewport dimensions) exists in perception pipeline
  but is discarded when constructing ObservedElement for the Runtime Observation.

PageAnalysisCurrentSemanticLevel: TEXT_ATTRIBUTE
  Current sources (FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE, SWITCH_DISTRIBUTION)
  operate exclusively on text strings and SwitchState. No spatial or structural signals.

PageAnalysisImpact: USEFUL_LATER
  Text+SwitchState is sufficient for current known-domain page recognition (4 EP-04 pages
  with distinctive text anchors). Spatial data would elevate to STRUCTURAL_SCREEN level.
  Not BLOCKING for PageAnalysis; HIGH_LEVERAGE for future capability.

ElementAnalysisImpact:
  BLOCKING for precise element identity. Without spatial data, ElementAnalysis cannot:
  - Distinguish title vs subtitle (same text, different spatial context)
  - Identify row+switch groups (separate elements, no grouping evidence)
  - Disambiguate duplicate text candidates (same text, different positions)
  - Detect non-interactive phantom elements (text without interactive YOLO bounds)

GroundingCoordinateChain: PARTIAL
  Works for ScriptedEnvironment (Index → config mapping internal to fake).
  BROKEN for real devices: Index has no mapping to screen coordinates in Runtime.
  First missing link: semantic candidate → observation bounds.

MinimumSpatialContract:
  Observation +ViewportWidth (int?), +ViewportHeight (int?)
  ObservedElement +Bounds (ElementBounds? {X1,Y1,X2,Y2} normalized [0,1]×[0,1]),
                   +SourceType (string?)
  Backward compatible (nullable, existing elements have null).
  No new coordinate spaces — uses normalized space already produced by fusion.py.
  No new transforms — perception pipeline already normalizes.

CoordinateRole:
  SUPPORTING_EVIDENCE only. Structural evidence, grouping evidence, relation evidence,
  grounding evidence, action localization, change evidence.
  Never authoritative identity.

IdentityRole: NOT_AUTHORITATIVE
  Coordinate ≠ Element Identity (frozen)
  Bounds ≠ Page Identity (frozen)
  Fingerprint ≠ Semantic Identity (frozen, I-6)

Classification: OBSERVATION_REPRESENTATION_GAP
  Spatial data exists in perception pipeline but is not carried by the Runtime
  Observation model. This is a pure representation gap — the data exists upstream;
  the Runtime model simply drops it.

OperationalSemanticIdentityDebt:
  _resolveSemanticPage still supplies page-name strings for CreateContainer,
  parent return verification, child identity, branch completion, OpenWorld ops.
  PageAnalysis provides semantic belief; old resolver provides operational strings.
  When they disagree, Container is named by the old resolver while evidence
  contradicts that name → dual-truth scenario.

DualTruthPathRemaining: PARTIAL
  Correcting prior report: old resolver verdict and PageAnalysis belief can diverge.
  Container name (from old resolver) may contradict Container belief (from PageAnalysis).
  Resolution: Agent adjudication → CreateContainer with evidence-supported name.

OpenWorldCanProceedWithoutSpatialPurchase: YES
  Current OpenWorld page semantics use string-based operations (resolver verdicts,
  page name comparisons). PageAnalysis integration (text anchors + foreground app)
  is sufficient. Spatial data would enhance but is not required for OpenWorld wiring.

ArchitectureDelta: NONE
  Gap identification only. No production changes. No new types created.
  Minimum spatial contract (nullable bounds on ObservedElement) is a separate
  purchase decision for a future task.

RecommendedNextTask:
  PURCHASE_MINIMUM_SPATIAL_OBSERVATION_CONTRACT
  Add ElementBounds? to ObservedElement, ViewportWidth/Height to Observation.
  Backward-compatible nullable fields. No new coordinate spaces.
  Enables: spatial falsifiers (F1-F6), structural PageAnalysis evidence,
  ElementAnalysis grouping, real-device grounding coordinate chain.

STOP.
```
