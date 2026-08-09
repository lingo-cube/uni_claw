# LEGACY_VISUAL_PERCEPTION_PRESSURE_RESULT

> Generated: 2026-08-09
> Primary inputs: Steps 1–6 of the legacy extraction pipeline
> Legacy truth source: `feature/refactor` (read-only Git objects)
> Role: Supplemental — does NOT modify SP-01…SP-13

---

## Visual Corpus Reviewed

| Source | Type | Key Evidence |
|---|---|---|
| `VisionGoldenIntegrationTests.cs` | INTEGRATION | Cloud AI golden: 3 items, tolerance 0.08–0.1, name OR coordinate match, extra items allowed |
| `RealVisionIntegrationTests.cs` | INTEGRATION | Real sensenova call, writes analysis.json, silently passes on missing screenshot |
| `AdbVisionActionIntegrationTests.cs` | INTEGRATION | Coordinate-based tap, FindSafeNavigation whitelist, no post-tap visual verification |
| `FixVerificationTests.cs` L5/L6/L8 | EXECUTABLE_REGRESSION | Empty OCR, 9 OCR variants, subtitle double-click at dy_full=0.0336 |
| `20260805T052309367Z_EnumerateFixtures.cs` | RECORDED_REALITY_DERIVED | Search box misclassification (type=menu_item instead of input), DFS revisit loop elements |
| `TextTargetResolutionTests.cs` | EXECUTABLE_REGRESSION | Type-blind Contains matching, "Flash notifications" misclick, substring overmatch |
| `local-vision-analyzer-memory/` | DOCUMENTATION | YOLO label→type mapping, subtitle phantom source (chevron heuristic), V5 search exclusion zone (y<0.10, real y=0.31) |
| `Screenshot_*_fc704e6b*.jpg` | REAL_DEVICE | PKJ110, 1440×3168, 59 YOLO detections, 17 OCR strings → 53 candidates (many empty-named) |
| `internal-gaps-calibrated.md` | DOCUMENTATION | C4 dual-path shape disagreement, G2 scroll-state gap, D-57 stale DynamicMatch cache |
| `roi-scroll-detection-prd.md` | DOCUMENTATION | ROI pixel comparison, verifier ignores production end signals, fingerprint-as-page-identity |
| `observation-pipeline/` | DOCUMENTATION | UIA deleted, AI-only pipeline, source equivalence contract unenforced |

---

## Pass A — Atomic Visual Evidence

### VE-01 — Golden Vision Matching Tolerates Coordinate Drift

- **Source:** `VisionGoldenIntegrationTests.cs` + `VisionGoldenComparer.cs`
- **External world:** Real device PKJ110, 1440×3168, Android Settings home screen
- **Raw visual evidence:** JPEG screenshot → sensenova AI → `PageAnalysis` with element names, types, coordinates (normalized 0–1)
- **Observation source:** AI vision provider (sensenova-6.7-flash-lite)
- **Produced observation:** 3 `menu_item` elements: WLAN (0.5, 0.31), 蓝牙 (0.5, 0.35), 移动网络 (0.5, 0.4)
- **Semantic interpretation:** Each matched as `ExpectedAction.Navigate`
- **Matching rule:** Name match (substring containment in either direction after normalize) OR coordinate match (Euclidean distance ≤ 0.08–0.1 on normalized coords). Extra items in actual output are explicitly allowed ("识别结果中额外项允许存在").
- **Failure mode:** A coordinate-drifted output where the recognized element is at (0.55, 0.35) — 0.05 normalized units from golden — still passes because Euclidean distance 0.05 ≤ 0.1. The element at the wrong coordinates is accepted as "correctly recognized."
- **Provenance:** REAL_DEVICE (screenshot) + INTEGRATION (real AI call)

### VE-02 — Coordinate-Only Tap Without Post-Action Visual Verification

- **Source:** `AdbVisionActionIntegrationTests.cs`
- **External world:** Android emulator running Settings app
- **Raw visual evidence:** ADB screencap → AI vision → `MenuItem.Coordinate` (normalized 0–1)
- **Observation source:** AI vision (sensenova)
- **Produced observation:** Target coordinate for the whitelist-matched element (Wi‑Fi/WiFi/WLAN/Network & internet)
- **Action based on interpretation:** `TapAsync(target.Coordinate.X, target.Coordinate.Y)` — raw ADB tap at vision-provided coordinates
- **Fresh evidence after action:** `WaitAsync(1500)` then `PressBackAsync()` — NO post-tap visual verification of whether the correct page was reached
- **Observed failure:** A tap landing on the wrong pixel (coordinate drift, wrong element at that position) would still pass because the test only verifies the tap was dispatched, not what page appeared after
- **Provenance:** EMULATOR + INTEGRATION

### VE-03 — Empty / Whitespace-Only OCR Text

- **Source:** `FixVerificationTests.cs` L5
- **External world:** Simulated Settings page with 5 elements including `""` and `"   "` text
- **Raw visual evidence:** OCR returns empty string or whitespace-only string for elements that exist in the visual field but have no readable text
- **Perception output:** Element with `type=menu_item`, `text=""` or `text="   "`
- **Normalization:** Both normalize to `""` → generated nodeId would be `dyn_menu_container__root` (double underscore) or `dyn_menu_container___root` (triple underscore)
- **Observed failure:** If treated as navigable: Click child with empty Text target → "Click Text target 异常" (exception). Fix: empty-text items generate no child nodes.
- **Provenance:** DETERMINISTIC_SIMULATION

### VE-04 — OCR Text Variant Normalization (9 cases)

- **Source:** `FixVerificationTests.cs` L6
- **External world:** Same logical element observed across multiple OCR runs
- **Raw visual evidence:** OCR text variations: `"Bluetooth, pairing"`, `"Bluetooth,pairing"`, `"Bluetooth , pairing"`, `"  Bluetooth   pairing  "`, `"Notification history, conversations"`, `"Notification history,conversations"`, `""`, `"   "`, `null`
- **Normalization rules:** lowercase, collapse whitespace runs (regex `\s+` → `" "` + trim), normalize comma spacing (regex `\s*,\s*` → `", "`), null/whitespace → `""`
- **Purpose:** Produce stable dedup keys and nodeIds across OCR runs so the same logical element is recognized as the same
- **Observed failure without normalization:** `"Bluetooth, pairing"` and `"Bluetooth,pairing"` treated as two different elements → duplicate navigation tasks, double-counted progress
- **Provenance:** DETERMINISTIC_SIMULATION

### VE-05 — Subtitle Text Misclassified as Navigable Menu Item

- **Source:** `FixVerificationTests.cs` L8 + `local-vision-analyzer-memory/lessons.md`
- **External world:** Android Settings page with "Connected devices" menu item at (0.38, 0.54) and subtitle "Bluetooth, pairing" at (0.31, 0.57)
- **Raw visual evidence:** YOLO detection of both elements. Subtitle at dy_full = 0.0336 from the menu item above it
- **Perception output:** Subtitle classified as `menu_item` (not downgraded to `text`) because V2 downgrade threshold 0.035 is evaluated in crop space: dy_crop = 0.0336/0.875 = 0.0384 ≥ 0.035 → threshold misses → subtitle stays `menu_item`
- **Observed failure:** 113/123 subtitle pairs missed downgrade (91.9%). Subtitle treated as independent navigation target → tapped → navigates to same page as "Connected devices" → "Connected devices 双击" (double-click into same page)
- **Chevron heuristic source:** Python `fusion.py` `_apply_chevron_heuristic` — same-row OCR text next to YOLO icon/switch/toggle → force-upgrade to `menu_item`
- **Provenance:** EXECUTABLE_REGRESSION + DOCUMENTATION (production perception analysis)

### VE-06 — Search Box Misclassified as Navigable

- **Source:** `20260805T052309367Z_TraceReplayTests.cs` + `EnumerateFixtures.cs`
- **External world:** Android Settings home with search box at y=0.28 (real y_full=0.31 after crop)
- **Raw visual evidence:** YOLO detection of search box element
- **Perception output case A (correct):** type=`input` → DynamicRule skips it (no `input` rule in safe_mode plan) → not navigable
- **Perception output case B (misclassified):** type=`menu_item` → matches `menu_container` rule → treated as navigable → tapped → enters search UI → search result self-loops → stuck
- **V5 search exclusion zone:** requires `y < 0.10` AND text contains "search" — real y=0.31 far below threshold → search box never excluded
- **Provenance:** RECORDED_REALITY_DERIVED (from real run `20260805T052309367Z`)

### VE-07 — Type-Blind Contains Target Matching

- **Source:** `TextTargetResolutionTests.cs` + run `20260806T072558649Z` analysis
- **External world:** Settings page with text-type element "Flash notifications" at (0.26, 0.73) and menu_item "Notifications" at (0.32, 0.78)
- **Perception output:** Both elements present in PageAnalysis with different types
- **Matching rule:** `FindMatchingItem` ③ Contains — substring match, historically type-blind
- **Observed failure:** Container target "notifications" → ③ Contains matched text-type "Flash notifications" → engine tapped a text element → navigated to depth 3 (maxDepth=2 violation). Fix: Contains match SHALL skip text-type items.
- **Substring overmatch:** `"Network_1"` ⊆ `"Network_10"` → false coverage count. `val?.ToString()?.Contains(reqId)` marks Network_10 as "covered" when Network_1 is visited.
- **Provenance:** EXECUTABLE_REGRESSION (from real run step 36)

### VE-08 — Observation Source Shape Disagreement (UIA vs AI)

- **Source:** `host-target-architecture-design.md` C4, `host-implementation-map.md` §6.3, `spec-defect-analysis.md` D4
- **External world:** Same device, same screen — two observation paths produce structurally different `PageAnalysis`
- **UIA path:** fills `Items`, `CurrentPath`, `HasScroll`, `IsEndOfList`; hardcodes `Direction.Left`; 1s latency; zero popup/WebView recognition
- **AI path:** fills `Level1Menus`, `Level2Menus`, `Items`; popup-aware; ~60s latency
- **Observed failure:** Same `PageAnalysis` record type, different field population — consumer cannot tell which path produced the data. C4: "prose contracts drift — C4 happened precisely because there was no test." D4: "sources producing diverging shapes are rejected as provider-response failure, not silently substituted."
- **Current state (feature/refactor):** UIA deleted. AI-only pipeline. Source equivalence contract adopted but unenforced after delete-uia.
- **Provenance:** DOCUMENT_ONLY (historical architecture gap)

### VE-09 — Visual Similarity False Success (20% Byte-Length Heuristic)

- **Source:** `spec-defect-analysis.md` D1, `evidence.md:217-225`
- **External world:** Device after navigation action
- **Raw visual evidence:** Screenshot byte length changed ≥20% from pre-action screenshot
- **Semantic interpretation:** `LooksLikeVisualTransition` → "the page changed" → action succeeded
- **Observed failure:** Real run `20260729T200940861Z-bf24ff268b9b4df` tagged `target_page_visual_transition_verified` — screenshot byte length changed ≥20% BUT UIAutomator hierarchy still described the old page. Byte-length change was from visual noise (scroll bar appearance, animation frame), not from actual page navigation.
- **Provenance:** DOCUMENT_ONLY (historical false-success case)

### VE-10 — ROI End-of-Scroll Signal Ignored by Verifier

- **Source:** `roi-scroll-detection-prd.md`, `ScenarioCompletionVerifier.cs:124-130`, `local-vision-analyzer-memory/lessons.md`
- **External world:** Scrollable list after scroll action
- **Perception output:** ROI pixel comparison produces `scroll_roi_end_reached` or `scroll_roi_content_guard` decision
- **Verifier expectation:** Only accepts legacy `scroll_no_new_elements_end_reached` → production ROI end signals never prove end-of-list → `endProven` always false
- **Observed failure:** The production scroll-detection mechanism correctly identifies end-of-list, but the verification layer ignores its output. Engine cannot prove it reached the end even when the visual evidence supports it.
- **Provenance:** DOCUMENT_ONLY + PRODUCTION_CODE

---

## Pass B — Visual Reality Distinctions

### VRD-01 — OCR Text Output != Semantic Element Identity

**Statement:** OCR Text Output != Semantic Element Identity

**Meaning:** The raw text string returned by OCR is not equivalent to the semantic identity of the element it describes. The same logical element can produce different OCR text across observations. Empty or whitespace-only OCR output does not mean the element has no identity — it means OCR could not read it.

**Supporting Evidence:**
- VE-04: 9 OCR variant normalization cases — same element, different text strings
- VE-03: Empty/whitespace OCR → normalization to "" → must not generate navigable children
- Local vision: "lothmicationsonTockscreen" garbage OCR (20 frames ×2)
- `"Recent apps,default apps"` (no space after comma) vs expected `"Recent apps, default apps"`

**Observed Failure When Conflated:**
- Empty-text elements generate invalid navigation targets with empty Text → exception
- OCR variants treated as unique elements → duplicate navigation tasks, double-counted progress, inflated step counts
- Garbage OCR creates phantom elements that don't correspond to any real UI element

**Evidence Strength:** EXECUTABLE_REGRESSION + RECORDED_REALITY_DERIVED

---

### VRD-02 — Element Classification Output != Interaction Capability

**Statement:** Element Classification Output != Interaction Capability

**Meaning:** The type label assigned by an element classifier (YOLO → AI type mapping, chevron heuristic, subtitle downgrade) is not equivalent to proof that the element can be interacted with as that type. A `menu_item` classification does not prove the element navigates. A `text` classification does not prove the element is non-interactive.

**Supporting Evidence:**
- VE-05: Subtitle classified as `menu_item` (chevron heuristic, downgrade threshold missed) → double-click → same page
- VE-06: Search box classified as `menu_item` (instead of `input`) → tapped → entered search UI → stuck
- VE-07: Text-type "Flash notifications" matched as navigation target via type-blind Contains → tapped → depth violation

**Observed Failure When Conflated:**
- Phantom subtitles become independent navigation targets (91.9% of pairs)
- Search box becomes navigable entry → engine enters non-traversable search UI → stuck
- Text elements become clickable coordinates for navigation targets they don't match

**Evidence Strength:** RECORDED_REALITY_DERIVED + EXECUTABLE_REGRESSION

---

### VRD-03 — Coordinate/Text Match != Semantic Target Identity

**Statement:** Coordinate/Text Match != Semantic Target Identity

**Meaning:** A coordinate overlap or text substring match between a target description and an observed element is not equivalent to proof that the element is the semantically correct target. "Close enough" in coordinate space or "contains the right substring" in text space does not confirm target identity.

**Supporting Evidence:**
- VE-07: Contains substring match "notifications" → matched text-type "Flash notifications" → wrong element tapped
- VE-07: `"Network_1"` ⊆ `"Network_10"` substring overmatch → coverage falsely counted
- VE-02: Coordinate-only tap with no post-action visual verification — a tap at the "right" coordinates could hit the wrong element if the screen layout differs from expectation
- VE-01: Golden tolerance 0.08–0.1 allows coordinate drift without questioning whether the same semantic element was recognized

**Observed Failure When Conflated:**
- Wrong element type dispatched (text element instead of menu_item)
- Substring overmatch inflates coverage counts (70/75 reported, actually ~68/75)
- Coordinate drift accepted as "correct recognition" when the element at the drifted position may be different

**Evidence Strength:** EXECUTABLE_REGRESSION (substring overmatch, type-blind match) + INTEGRATION (coordinate-only tap)

---

### VRD-04 — Observation Source Output != Authoritative World Evidence

**Statement:** Observation Source Output != Authoritative World Evidence

**Meaning:** The output of any single observation source (AI vision, UIAutomator, golden comparison) is not equivalent to authoritative proof about the external world. Different sources can disagree about the same world state. No single source is inherently trustworthy.

**Supporting Evidence:**
- VE-08: UIA vs AI shape disagreement — same PageAnalysis record, different field population
- VE-09: 20% byte-length heuristic false success — screenshot changed but page didn't
- VE-10: Production ROI end signals ignored by verifier — correct observation, wrong consumer
- D4: "sources producing diverging shapes are rejected as provider-response failure, not silently substituted"

**Observed Failure When Conflated:**
- False navigation success from screenshot byte-length change (real run `20260729T200940861Z`)
- Scroll end-of-list unprovable because verifier ignores the production signal
- Consumer cannot determine which observation path produced the data

**Evidence Strength:** DOCUMENT_ONLY

---

## Pass C — Visual Scenario Pressures

### VSP-01 — Target Grounding Must Verify Semantic Identity Beyond Coordinate/Text Match

**Scenario Pressure ID:** VSP-01
**Title:** Target grounding must verify that the element at matched coordinates/text is semantically the intended target, not just that it matches a coordinate or substring
**Primary VRD:** VRD-03 (Coordinate/Text Match != Semantic Target Identity)
**Secondary VRDs:** VRD-02 (Element Classification Output != Interaction Capability)
**Source Evidence:** VE-07 (type-blind Contains, substring overmatch), VE-02 (coordinate-only tap, no post-action verification), VE-01 (golden coordinate tolerance)
**Evidence Strength:** EXECUTABLE_REGRESSION + INTEGRATION
**Scenario Type:** GROUNDING

---

**Intent:**
Tap the "Notifications" entry on the Settings home page to navigate to the Notifications sub-page.

**Given:**
- The Settings home page is visible with multiple elements including:
  - `menu_item` "Notifications" at (0.32, 0.78)
  - `text` "Flash notifications" at (0.26, 0.73) — a decorative text label near the Notifications entry
  - `menu_item` "Notification history, conversations" at (0.43, 0.81)
- The target description is "notifications" (lowercase substring).
- The matching algorithm uses substring containment (case-insensitive) to find the target.

**Available Raw Evidence:**
- Element observations: text, type, coordinates for all visible elements
- Target description: a string to match against observed element text
- Element types distinguish interactive elements (menu_item) from decorative elements (text)

**Perception Output:**
The matching algorithm finds two candidates whose text contains "notifications":
- "Flash notifications" (type: text, coords: 0.26, 0.73)
- "Notifications" (type: menu_item, coords: 0.32, 0.78)

**Interpretation Pressure:**
If the matching algorithm is type-blind (legacy behavior), it may select "Flash notifications" because it appears first or has a longer shared substring. The text element is not a navigation target — tapping it will not navigate to the Notifications page.

**When:**
The system selects a target element based on text match and dispatches a tap at its coordinates.

**Then:**
The system must verify that the selected element's type is consistent with the intended interaction. A text-type element must not be selected as a navigation target even if its text contains the target substring. If multiple candidates match the target text, the system must prefer the candidate whose type matches the expected interaction (e.g., `menu_item` for navigation). After the tap, the system must verify through fresh observation that the resulting page matches the expected destination — not merely that a tap was dispatched.

**Must Not:**
- Select a text-type element as a navigation target because its text contains the target substring
- Treat "Network_1" as equivalent to "Network_10" for coverage accounting (substring overmatch)
- Accept coordinate-proximity alone as proof of correct target identification
- Omit post-tap visual verification of the destination page

**Pass Oracle:**
Target "notifications" resolves to `menu_item` "Notifications" at (0.32, 0.78). Tap dispatched. Post-tap observation confirms the Notifications sub-page is displayed (page identity matches expected destination). Text-type "Flash notifications" is never tapped.

**Fail Oracle:**
Target "notifications" resolves to `text` "Flash notifications" via type-blind Contains match. Tap dispatched at (0.26, 0.73). Page does not change (text element tapped) OR wrong page appears. System reports navigation success without verifying the destination page identity. (Legacy run `20260806T072558649Z` step 36: Flash notifications misclick → depth=3 violation.)

**Evidence Maturity Needed:** S2 (production-shaped perception with real YOLO/OCR type labels)

**Legacy Mechanisms Excluded:**
FindMatchingItem ③ Contains (type-blind), substring overmatch in ExpectedBehavior.Verify, coordinate-only tap without post-action observation, golden tolerance accepting coordinate drift.

---

### VSP-02 — OCR Text Variants Must Normalize to Stable Element Identities

**Scenario Pressure ID:** VSP-02
**Title:** Different OCR text outputs for the same logical element across observations must normalize to the same element identity
**Primary VRD:** VRD-01 (OCR Text Output != Semantic Element Identity)
**Secondary VRDs:** NONE
**Source Evidence:** VE-04 (9 OCR variant normalization cases), VE-03 (empty/whitespace OCR)
**Evidence Strength:** EXECUTABLE_REGRESSION
**Scenario Type:** PERCEPTION

---

**Intent:**
Enumerate the entries on a page where some element labels vary slightly across OCR observations.

**Given:**
- A page has a menu item whose text is "Bluetooth, pairing."
- Across two observations of the same page (e.g., after returning from a sub-page, or after a scroll), OCR produces slightly different text for this element:
  - Observation 1: "Bluetooth, pairing"
  - Observation 2: "Bluetooth,pairing" (OCR dropped the space after comma)
- A third element has empty OCR output: "" (OCR read nothing for this element)

**Available Raw Evidence:**
- Observation 1: element with text "Bluetooth, pairing", type menu_item
- Observation 2: element with text "Bluetooth,pairing", type menu_item
- Observation 3: element with text "", type menu_item

**Perception Output:**
Without normalization, the system sees three distinct elements. With normalization: "bluetooth, pairing", "bluetooth, pairing", "".

**Interpretation Pressure:**
If raw OCR text is used directly as element identity, Observation 1 and Observation 2 will be treated as different elements → the system will generate navigation tasks for both → the same page will be visited twice → step count inflated, progress double-counted. The empty-text element will generate a navigation task with an empty target → exception or invalid dispatch.

**When:**
The system observes the same page twice with OCR variations and must determine which elements are already known vs newly discovered.

**Then:**
The system must normalize OCR text before using it as element identity. Normalization must: lowercase, collapse all whitespace runs to single spaces, normalize comma spacing to `", "`, and treat null/empty/whitespace as empty string. Elements whose normalized text is empty must not generate navigation tasks. Two observations with the same normalized text must be recognized as the same element.

**Must Not:**
- Treat "Bluetooth, pairing" and "Bluetooth,pairing" as different elements
- Generate navigation tasks from elements with empty or whitespace-only normalized text
- Use raw OCR output directly as element identity without normalization

**Pass Oracle:**
Observation 1 and Observation 2 produce the same normalized element identity "bluetooth, pairing." Only one navigation task is generated. Observation 3 (empty text) produces no navigation task. No duplicate visits. No empty-target exceptions.

**Fail Oracle:**
Observation 1 and Observation 2 produce different element identities. Two navigation tasks generated for the same logical element. Page visited twice. Step count inflated. Observation 3 generates a navigation task with empty target → exception. (Legacy pattern: duplicate navigation from OCR variants; empty-text crash.)

**Evidence Maturity Needed:** S1 (recorded replay of OCR variants from real analysis.jsonl)

**Legacy Mechanisms Excluded:**
NormalizeItemText implementation details (regex patterns, specific whitespace rules), DynamicChildManager nodeId generation.

---

### VSP-03 — Element Classification Must Be Verified Against Interaction Outcome

**Scenario Pressure ID:** VSP-03
**Title:** An element classified as navigable must produce observable navigation evidence; classification alone is insufficient
**Primary VRD:** VRD-02 (Element Classification Output != Interaction Capability)
**Secondary VRDs:** VRD-01 (ActionExecution != ActionEffect — RD-01)
**Source Evidence:** VE-05 (subtitle double-click, 91.9% downgrade missed), VE-06 (search box misclassified as menu_item → stuck)
**Evidence Strength:** RECORDED_REALITY_DERIVED + EXECUTABLE_REGRESSION
**Scenario Type:** GROUNDING

---

**Intent:**
Navigate to the "Connected devices" sub-page from the Settings home page.

**Given:**
- The Settings home page has:
  - `menu_item` "Connected devices" at (0.38, 0.54)
  - Adjacent element at (0.31, 0.57) — this is a subtitle text "Bluetooth, pairing" that sits in the same row as "Connected devices" (dy_full = 0.0336)
- The element classifier labels both as `menu_item` (the subtitle should be `text` but the downgrade threshold was not met)

**Available Raw Evidence:**
- Two elements with type=menu_item in close vertical proximity
- Element text: "Connected devices" and "Bluetooth, pairing"
- Spatial relationship: dy_full = 0.0336 between them

**Perception Output:**
Both elements are classified as `menu_item` → both are treated as independent navigation targets.

**Interpretation Pressure:**
The subtitle "Bluetooth, pairing" is not a navigation target — it is descriptive text associated with "Connected devices." Tapping either element navigates to the same "Connected devices" sub-page. If both are treated as independent targets, the system will tap both, entering the same page twice.

**When:**
The system taps "Bluetooth, pairing" (misclassified as menu_item). It navigates to the Connected devices page. It returns. It then taps "Connected devices" and navigates to the same page again.

**Then:**
The system must not treat adjacent elements in close vertical proximity as independent navigation targets without evidence that they navigate to different destinations. After tapping an element and observing the resulting page, if another nearby element navigates to the same page, the system must recognize the duplication and not dispatch a second navigation. Element classification as `menu_item` is a hypothesis; the observed navigation outcome is the verification.

**Must Not:**
- Treat every element classified as `menu_item` as an independent navigation target
- Double-tap adjacent elements that navigate to the same destination
- Rely on classification alone without verifying navigation outcome

**Pass Oracle:**
"Connected devices" tapped once. Post-tap observation confirms Connected devices page. "Bluetooth, pairing" (if classified as menu_item) is also evaluated — but after observing that tapping "Connected devices" already reached the destination, the subtitle is not independently dispatched. Only one navigation to Connected devices occurs.

**Fail Oracle:**
Both "Bluetooth, pairing" and "Connected devices" are tapped. Same page entered twice. (Legacy: "Connected devices 双击" — double-click from subtitle phantom menu_item. 91.9% of subtitle pairs affected.)

**Evidence Maturity Needed:** S2 (production-shaped YOLO/OCR with real classification errors)

**Legacy Mechanisms Excluded:**
V2 subtitle downgrade threshold (0.035 in crop space), chevron heuristic (fusion.py `_apply_chevron_heuristic`), dy_full spatial proximity threshold.

---

## Attachments To Existing SPs

### ATTACH to SP-02 (Navigation Action Must Verify Page Change)

**Visual Evidence Attached:** VE-09 (20% byte-length false success), VE-02 (coordinate-only tap without post-action visual verification)

**What This Adds:** Concrete recorded evidence of false-positive action-effect detection. The legacy system treated screenshot byte-length change ≥20% as proof of page navigation — a purely visual heuristic that produced a verified false success. This strengthens SP-02's requirement that post-action verification must use semantic page evidence, not visual-proxy heuristics.

**S1 Replay Value:** HIGH — the false-success run `20260729T200940861Z` is a recorded artifact that could be replayed to demonstrate the failure mode.

---

### ATTACH to SP-05 (Observation Failure Must Not Become Content Exhaustion)

**Visual Evidence Attached:** VE-10 (ROI end-of-scroll signal ignored by verifier)

**What This Adds:** A concrete case where the CORRECT observation (ROI pixel comparison correctly identified end-of-list) was IGNORED by the verification layer (only accepted legacy signal name). This is the inverse of SP-05: not "failure misinterpreted as success" but "correct observation ignored." It strengthens SP-05's requirement that observation evidence must be authoritative — the consumer must not selectively accept only legacy-format signals.

**S1 Replay Value:** MEDIUM — the ROI signal mismatch is a production-code behavior, not a recorded-run artifact.

---

### ATTACH to SP-07 (Element Visibility Must Not Imply Navigability)

**Visual Evidence Attached:** VE-05 (subtitle double-click), VE-06 (search box misclassification), VE-07 (type-blind Contains match), VE-03 (empty OCR text)

**What This Adds:** Three concrete visual perception failure modes that all reduce to the same semantic conflation: "the perception pipeline says this is navigable → it is navigable." The subtitle phantom (YOLO + chevron heuristic), search box (YOLO label→type mapping), and type-blind text matching are three different perception-stage failures that produce the same semantic error.

**VSP-02 and VSP-03 are visual subcases of SP-07** — they add perception-specific evidence (OCR normalization, classification verification) without changing the underlying semantic distinction.

**S1 Replay Value:** HIGH for VE-06 (search box misclassification from real run `20260805T052309367Z`). MEDIUM for VE-05 (subtitle requires production-shaped perception).

---

### ATTACH to SP-10 (Same Logical Page Must Be Recognized Across Observations)

**Visual Evidence Attached:** VE-04 (OCR variant normalization for stable identity), scroll fingerprint evidence (fingerprint = (type,name) hash — scroll changes fingerprint because item set changes)

**What This Adds:** OCR text variants directly threaten page recognition: if the same page produces different element text on different observations, the fingerprint changes, and the page is not recognized as previously visited. Normalization is prerequisite to stable page identity. The scroll fingerprint evidence shows that scroll within one page changes the item set → fingerprint changes → the system must distinguish "same page, different scroll position" from "different page."

**S1 Replay Value:** MEDIUM — OCR variant replay from recorded analysis.jsonl frames.

---

### ATTACH to SP-12 (Plan Validity Must Not Imply Execution Success)

**Visual Evidence Attached:** VE-08 (UIA vs AI observation source shape disagreement)

**What This Adds:** The observation-source disagreement evidence shows that a plan validated against one observation source's output may fail against another source's output for the same world state. The plan's assumptions about element shape, scroll fields, and page identity depend on which observation path produced the data. This strengthens SP-12's requirement that plan-world correspondence must be verified at execution time against fresh evidence.

**S1 Replay Value:** LOW — the UIA source is deleted; this is historical architecture evidence.

---

## OCR / Text

### Classification of OCR/Text Failures

| Failure | Classification | Rationale |
|---|---|---|
| Empty/whitespace OCR → invalid navigation target | **Normalization-only** → covered by VSP-02 | The fix is text normalization (empty → skip). No semantic-role ambiguity. |
| OCR comma/whitespace variants → duplicate identity | **Normalization-only** → covered by VSP-02 | The fix is deterministic normalization rules. No semantic-role ambiguity. |
| Subtitle "Bluetooth, pairing" → phantom menu_item | **Semantic-role ambiguity** → covered by VSP-03 | The element IS present and HAS text. The failure is in its semantic role (decorative text vs navigation target), not in OCR quality. |
| "Flash notifications" → type-blind target match | **Target-grounding ambiguity** → covered by VSP-01 | The text matches the target substring. The failure is in selecting the wrong element type, not in OCR quality. |
| "lothmicationsonTockscreen" garbage OCR | **Observation-quality failure** → S2 production-shaped concern | This is raw OCR quality, not a semantic distinction. |

---

## Target Grounding

### Evidence Strength Assessment

| Evidence | Strength | Supports |
|---|---|---|
| Type-blind Contains match (VE-07) | EXECUTABLE_REGRESSION | VSP-01 — text match must respect element type |
| Substring overmatch Network_1 ⊆ Network_10 (VE-07) | EXECUTABLE_REGRESSION | VSP-01 — substring containment ≠ identity |
| Coordinate-only tap without post-action verification (VE-02) | INTEGRATION | VSP-01 — coordinate dispatch ≠ correct target |
| Golden coordinate tolerance 0.08–0.1 (VE-01) | INTEGRATION | VSP-01 — coordinate proximity ≠ recognition |

**No evidence found for:** coordinate drift between plan and observation causing wrong-element tap in a real run. The ADB test's coordinate-only tap is a design observation, not a recorded failure. This is a **EVIDENCE_GAP_GROUNDING** for plan-coordinate mismatch → wrong-target scenarios.

**No evidence found for:** hierarchy bounds vs visual bounds disagreement causing tap offset. UIA was deleted; the remaining AI pipeline uses vision-provided coordinates without cross-referencing against hierarchy bounds.

---

## Page Understanding

SP-10 (RawPageEvidence != SemanticPageIdentity) already captures the semantic pressure. The visual evidence (OCR variants affecting fingerprint stability, scroll changing item-set fingerprint) adds perception-specific implementation concerns:

- **Fingerprint = (type,name) hash** — OCR variants and scroll-driven item-set changes both alter the fingerprint. This is an S2 implementation concern (how to compute a stable page fingerprint from noisy visual input), not a new semantic distinction.
- **Scroll identity vs page identity** — the legacy system uses fingerprint for both, meaning scroll within one page changes the "page identity." The current Runtime separates Container identity (SemanticPageName + IsStillMine) from viewport state (ViewportExplorationEvidence). The visual evidence supports this separation but does not require a new distinction.

**Classification:** SP-10 already covers the semantic pressure. Visual derivation (fingerprint computation, scroll-vs-page identity) is S2 production-shaped evidence.

---

## Observation Source Disagreement

**Does legacy evidence demonstrate disagreement on the same external world?** YES — but the evidence is historical. The UIA source has been deleted from the branch. The two-source shape disagreement (C4) is documented but no longer exercisable on the current codebase.

**Current state:** AI-only pipeline. Source equivalence contract adopted but unenforced.

**Classification:** PRODUCTION_SHAPED_RESEARCH for S2. If the current Runtime adds a second observation source (e.g., hierarchy alongside vision in Phase 4), the C4/D4 evidence becomes directly applicable. For S0/S1, the single-source pipeline means source disagreement cannot occur — there is only one source to disagree with.

---

## Observation Freshness

**Evidence found:** VE-09 (stale screenshot byte-length heuristic), D2 (stale hierarchy not detected), D-57 (stale DynamicMatch cache → max_steps exhaustion).

**Classification:** ATTACH_TO_EXISTING_SP — these reinforce SP-05 (ObservationFailed != ContentExhausted) and the general fresh-observation requirement already frozen in the current Runtime (post-action Observe is mandatory, Traversal journal records observations by sequence number).

**No new visual pressure required.** The current Runtime already requires fresh observation after every action. The legacy freshness failures demonstrate why — they are evidence attachments, not new distinctions.

---

## Evidence Gaps

| Gap | Description |
|---|---|
| EVIDENCE_GAP_GROUNDING | No recorded real-run evidence of plan-coordinate mismatch causing wrong-element tap. VE-02 is a design observation (no post-tap verification), not a recorded failure. |
| EVIDENCE_GAP_OCCLUSION | No evidence of partial element occlusion or clipping causing tap failure. The crop-space model trims edges but does not model partial visibility. |
| EVIDENCE_GAP_HIERARCHY_BOUNDS | No evidence of hierarchy bounds vs visual bounds disagreement. UIA deleted; remaining pipeline is vision-only. |

---

## S1-Replay-Worthy Visual Evidence

| Priority | Evidence | What to Replay |
|---|---|---|
| **P0** | VE-06 + VE-07 (search box misclassification + type-blind match from run `20260805T052309367Z`) | Recorded analysis.jsonl frames showing misclassified element types → replay against current CandidateAuthorizationEvidence model → verify correct rejection |
| **P1** | VE-05 (subtitle phantom from run `20260806T072558649Z`) | Recorded subtitle elements at dy_full=0.0336 with menu_item classification → replay to verify classification-to-navigability gap |
| **P1** | VE-03 + VE-04 (OCR variants from recorded analysis.jsonl) | Recorded OCR text variations across frames → replay to verify normalization produces stable identities |
| **P2** | VE-09 (20% byte-length false success) | Historical false-success run `20260729T200940861Z` → replay to demonstrate the failure mode SP-02 prevents |

---

## S2 Production-Shaped Evidence Targets

These require real perception pipeline evidence (YOLO, OCR, real screenshots) and are not actionable in S0/S1:

| Evidence | What S2 Would Provide |
|---|---|
| YOLO label→type mapping accuracy (search box, subtitle, unknown labels) | Real classification error rates → tune CandidateAuthorizationEvidence thresholds |
| OCR quality on real device screenshots (empty text rate, garbage rate, variant rate) | Real normalization effectiveness → tune text-matching tolerance |
| ROI scroll detection accuracy vs ground-truth end-of-list | Real observation-failure rate → tune ViewportExplorationEvidence confidence |
| Fingerprint stability across observations of the same page | Real page-recognition accuracy → tune Container identity rules |

---

## Portfolio Merge

**Existing SPs:** 13 (SP-01 through SP-13)
**New VSPs:** 3 (VSP-01 through VSP-03)
**Attach-only cases:** 5 evidence groups attached to SP-02, SP-05, SP-07, SP-10, SP-12
**Combined independent pressures:** 16 (13 semantic + 3 visual)

### Merged Portfolio Summary

| ID | Type | Primary Distinction | Priority |
|---|---|---|---|
| SP-01 | NEGATIVE_CONTROL | ActionExecution != ActionEffect | P0 |
| SP-02 | ATOMIC_BEHAVIOR | ActionExecution != ActionEffect (+VE-09 freshness evidence) | P1 |
| SP-03 | NEGATIVE_CONTROL | WorkDispatched != WorkCompleted | P0 |
| SP-04 | ATOMIC_BEHAVIOR | ConstraintDeclared != ConstraintEnforced | P0 |
| SP-05 | NEGATIVE_CONTROL | ObservationFailed != ContentExhausted (+VE-10 verifier-ignore evidence) | P0 |
| SP-06 | ATOMIC_BEHAVIOR | ObservationFailed != ContentExhausted | P1 |
| SP-07 | NEGATIVE_CONTROL | ElementPresence != ElementNavigability (+VE-03/05/06/07 classification evidence) | P1 |
| SP-08 | DISTURBANCE | RecoveryAction != ErrorStateReset | P1 |
| SP-09 | TRANSFORMATION | TaskIntent != ExecutionMethod | P2 |
| SP-10 | ATOMIC_BEHAVIOR | RawPageEvidence != SemanticPageIdentity (+VE-04 OCR stability evidence) | P1 |
| SP-11 | NEGATIVE_CONTROL | GoalExpression != GoalState | P2 |
| SP-12 | NEGATIVE_CONTROL | PlanConstructed != ExecutionGuaranteed (+VE-08 source disagreement evidence) | P2 |
| SP-13 | ATOMIC_BEHAVIOR | PreviouslyVisited != Unexplored | P1 |
| **VSP-01** | **GROUNDING** | **Coordinate/Text Match != Semantic Target Identity** | **P1** |
| **VSP-02** | **PERCEPTION** | **OCR Text Output != Semantic Element Identity** | **P2** |
| **VSP-03** | **GROUNDING** | **Element Classification Output != Interaction Capability** | **P1** |

---

## Readiness

**VISUAL_PRESSURE_SUPPLEMENT_READY_FOR_CHALLENGE**

3 new visual scenario pressures formulated. 5 evidence groups attached to existing SPs. 3 evidence gaps documented. S1-replay-worthy and S2 production-shaped evidence targets identified. Portfolio merge complete: 16 combined independent pressures.

---

## Repository Changes

`docs/decisions/legacy-visual-perception-pressure-supplement.md` ONLY
