# REALITY_GROUNDED_UI_MINIMUM_SLICE_RESULT

> Generated: 2026-08-09
> Role: Project Leader — reality-grounding exercise, not semantic design
> Target: "确保 Wi‑Fi 已开启" — consume production-shaped recorded UI reality
> Principle: real asset first, falsify before designing

---

## Reality Assets Found

**Count: 5 asset families across 2 branches. REAL_DEVICE: 1, EMULATOR: 0, RECORDED_REALITY: 4, SYNTHETIC: 0 (excluded by design).**

| Asset | Location | Type | Provenance | Content |
|---|---|---|---|---|
| **EP-04 Sim-Replay Export** | `feature/refactor:artifacts/sim-replay/trace-replay-export.json` | Page inventory from recorded run | **RECORDED_REALITY** (E3) | 4 pages (5+16+21+14 elements), run 20260805T083146853Z, 19 steps, all_visited. Real Android Settings hierarchy: settings → network_internet → internet. |
| **EP-03 Success Trace** | `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/success/` | Real trace.jsonl + result.json | **RECORDED_REALITY** (E4) | Run 20260801T124355012Z, 8 steps, 3 actions succeeded, locate "About emulated device", emulator-5554, sensenova provider |
| **EP-03 Failure Trace** | `feature/refactor:tests/UniClaw.TraceTool.Tests/Fixtures/failure/` | Real trace.jsonl + result.json | **RECORDED_REALITY** (E4) | Run 20260803T131333575Z, 4 steps, 0 actions, target_page_identity_not_verified, local provider |
| **Vision Golden Corpus** | `feature/refactor:tests/UniClaw.Core.Tests/Fixtures/Screenshots/` | Real screenshot + YOLO detections + OCR + expected | **REAL_DEVICE** (E2) | PKJ110, 1440×3168. 3 expected items: WLAN (y=0.31, aliases: Wi‑Fi/WiFi/无线局域网), 蓝牙 (y=0.35), 移动网络 (y=0.40). YOLO confidence 0.94-0.98. Real pixel coordinates. |
| **E-10 TraceReplay Fixtures** | `feature/refactor:tests/UniClaw.Core.Tests/Simulation/TraceReplay/` | Hand-reconstructed StateFixture from real analysis.jsonl | **RECORDED_REALITY** (E3) | Run 20260805T052309367Z. Settings page with REAL coordinates: "Network & internet" (0.38,0.40), "Bluetooth, pairing" (0.31,0.58). Internet page: "Wi‑Fi" (0.5,0.15), toggle. |

**No raw analysis.jsonl files found on disk** — gitignored, keepRuns=5, cited runs may no longer exist.

---

## Selected Reality Case

**EP-04 Sim-Replay Export — Real Android Settings Hierarchy**

Run: `20260805T083146853Z-4382ac2d41f841b`
Source: `feature/refactor:artifacts/sim-replay/trace-replay-export.json`
Completion: `all_visited`, 19 steps, 4 pages

This is the ONLY committed asset that contains a COMPLETE multi-page Settings hierarchy with typed elements from a real recorded run. It covers the exact path needed for "ensure Wi‑Fi is ON":

```
Settings root ("settings", 16 elements)
  → "Network&internet" (menuitem, appears ×2)
    → Network & Internet page ("network_internet", 21 elements)
      → "Internet" (menuitem, appears ×2)
        → Internet page ("internet", 14 elements)
          → "Wi‑Fi" (menuitem)
          → "AndroidWifi" (menuitem)
          → toggle
```

**Wi‑Fi is 3 levels deep.** The user task "确保 Wi‑Fi 已开启" requires navigating Settings → Network & internet → Internet → Wi‑Fi. The synthetic fixtures (E-03, 7-page Settings) have Wi‑Fi at depth 1 from the Settings root — this is FICTION relative to the real Android Settings hierarchy.

---

## L3 Replay

**REALITY_CAPTURE_REQUIRED** — but with specific findings from existing assets.

The committed RECORDED_REALITY assets are sufficient to FALSIFY current assumptions (see below) but INSUFFICIENT to construct an executable L3 replay against the current Runtime. Why:

1. **The sim-replay export is a static page inventory, not a runnable fixture.** It records what pages existed and what elements they contained, but not the transition graph (which tap leads to which page). The current Runtime needs transitions to execute.

2. **The TraceReplay fixtures (E-10) DO run against the current Runtime** but they were hand-reconstructed from analysis.jsonl. The reconstruction involved human judgment about element selection, type assignment, and transition definition. This is RECORDED_REALITY derived, not raw-to-Runtime.

3. **No raw analysis.jsonl or trace.jsonl from a real run exists on disk** in the current repository. The cited runs (20260805T052309367Z, 20260806T072534Z) are gitignored and may no longer be present.

**What a REALITY_CAPTURE would require for L3 replay:**

```
Minimum capture from emulator/device:
  - Settings root screenshot + accessibility dump
  - Parsed observation (element types, text, real coordinates)
  - Action: tap "Network & internet"
  - Post-action screenshot + accessibility dump
  - Action: tap "Internet"  
  - Post-action screenshot + accessibility dump
  - Pre-condition: Wi‑Fi state (ON or OFF)
  - Action: toggle Wi‑Fi (if needed)
  - Post-action: fresh observation confirming Wi‑Fi state
```

This is 3 screenshots with parsed observations + 1 state-change pair. No generic page model required — just the recorded data.

---

## Falsification: What Real Data Reveals

### A. Can current Observation represent the actual page?

**OBSERVATION_REPRESENTATION_GAP**

The Observation model CAN hold element lists with types and text. But the real "settings" page has 16 elements, of which:
- **5 have empty text** (type=menuitem, text="") — the Observation model represents these but has no concept of "this element's text is empty/unreliable"
- **3 "QSearch settings" text elements** — duplicates that don't correspond to distinct interactive elements
- **"Network&internet" appears TWICE** (elements [4] and [6]) — the Observation model doesn't flag duplicate text
- **"Bluetooth, pairing" classified as menuitem** — the subtitle phantom (VE-05), the Observation model trusts the type label

**The synthetic fixtures have NONE of this noise.** They provide exactly one element per logical UI item, all with unique text, all with correct types. The Observation model was validated against this clean data.

### B. Can current Container semantics truthfully describe the page?

**CONTAINER_SEMANTIC_GAP**

Container has `SemanticPageName` and `IsStillMine`. But:
- **"Network&internet" appears on BOTH the settings page AND the network_internet page** — same text, different pages. Container cannot use element text alone for page identity. With 5 empty-text elements on the settings page, Container has fewer distinguishing features than synthetic fixtures assume.
- **The Internet page has "Wi‑Fi" menuitem AND "AndroidWifi" menuitem** — which one is the correct Wi‑Fi entry? Container has no concept of semantic disambiguation at the page-content level.
- **Page identity inference from real element inventory is qualitatively harder** than from synthetic data. The 20 committed ExpectedBehavior snapshots (AF-12) were calibrated against synthetic pages with perfect element inventories.

### C. Can current target grounding identify Wi‑Fi without baseline index assumptions?

**GROUNDING_GAP**

The real hierarchy reveals that "Wi‑Fi" text does NOT appear on the Settings root page. It appears only on the Internet page (depth 3). The user's intent "确保 Wi‑Fi 已开启" must be decomposed into:
1. Find "Network & internet" on Settings root → tap
2. Find "Internet" on Network & internet page → tap
3. Find "Wi‑Fi" on Internet page → check state, toggle if needed

The current Runtime's target grounding works on ONE page at a time. It has NO capability to decompose a multi-step intent into navigation actions. This is CP-14 territory (Intent ≠ Execution Method). But even at the single-page level:

- On the Settings root, "Network & internet" appears TWICE — CP-12 multi-candidate grounding
- On the Network & internet page, "Internet" appears TWICE — same problem
- On the Internet page, "Wi‑Fi" and "AndroidWifi" both contain "Wi‑Fi" — substring collision

**The real data is a CP-12 stress test at EVERY navigation level.**

### D. Can element identity survive real layout/text variation?

**GROUNDING_GAP**

Real variation observed across assets:

| Element | EP-04 (sim-replay) | E-10 (reconstructed) | Vision Golden |
|---|---|---|---|
| Wi‑Fi entry | "Wi‑Fi" (menuitem) | "Wi‑Fi" (menuitem) | "WLAN" (menu_item, alias: "Wi‑Fi") |
| Network entry | "Network&internet" | "Network & internet" | Not on this screen |
| Bluetooth entry | "Bluetooth, pairing" (menuitem — phantom!) | "Bluetooth, pairing" (menuitem) | "蓝牙" (menu_item, alias: "Bluetooth") |

- **Same element, different text:** "Network&internet" vs "Network & internet" — OCR normalization handles this (9-case). But "WLAN" vs "Wi‑Fi" is a SYNONYM, not a text variant — OCR normalization doesn't handle synonyms.
- **Same text, different type:** "Bluetooth, pairing" is classified as menuitem in EP-04 but is actually a subtitle (VE-05). Type label is wrong.
- **Different devices use different text:** The vision golden (PKJ110, Chinese ROM) shows "WLAN" / "蓝牙" / "移动网络". The EP-04 emulator shows "Network&internet" / "Wi‑Fi" / "Bluetooth, pairing". Same logical Settings app, different text.

### E. Can fresh post-action evidence establish Wi‑Fi state?

**REALITY_EVIDENCE_GAP**

The Internet page (EP-04) shows "Wi‑Fi" menuitem and a `toggle` element at [5]. But:
- We do NOT have a before/after pair showing the toggle state change
- The toggle has empty text — we can't tell what it controls from the element data
- The sim-replay is a static traversal snapshot, not a state-change record
- No committed asset records "Wi‑Fi was OFF, user toggled it ON, now Wi‑Fi is ON"

**To falsify "can fresh evidence establish Wi‑Fi state," we need a state-change pair that doesn't exist in committed assets.**

### F. Does current runtime accidentally depend on synthetic-only information?

**NEW_SEMANTIC_PRESSURE — YES.**

The current Runtime was validated against synthetic fixtures that provide information the real world does not:

| Synthetic assumption | Real world |
|---|---|
| Every element has a unique, correct type label | 5 of 16 elements have empty text; "Bluetooth, pairing" is phantom menuitem |
| Every element has unique, non-empty text | 5 empty-text elements; "QSearch settings" ×3; "Network&internet" ×2 |
| Pages have 5-7 elements each | Settings root has 16 elements (many noise) |
| Wi‑Fi is at depth 1 | Wi‑Fi is at depth 3 |
| Page transitions are known a priori | Transitions must be discovered by interaction |
| "Wi‑Fi" text appears on the first page | "Wi‑Fi" appears only on the 3rd-level page |
| Element text is consistent across devices | "WLAN" vs "Wi‑Fi"; "Network&internet" vs "Network & internet" |

**The gap between synthetic and real observation is NOT just "add more test cases."** It is a semantic gap: the Runtime's Observation, Container, and Grounding models were validated against data that doesn't exist in the real world. Real data is noisier, deeper, more ambiguous, and more varied than any synthetic fixture.

---

## Current Runtime Can Actually Do

With the existing committed RECORDED_REALITY assets, the current Runtime can:

1. **Represent the element inventory** of a real page — the Observation model holds typed elements with text and coordinates. The representation is structurally sufficient.

2. **Execute a pre-planned navigation sequence** through the 3-level hierarchy IF the plan specifies exact element text and coordinates — this is the locate scenario (closed-world plan mode).

3. **NOT autonomously navigate to Wi‑Fi from the task intent "ensure Wi‑Fi is ON"** — this requires Intent→Goal decomposition (CP-14, deferred) and multi-level navigation grounding.

4. **NOT reliably ground "Network & internet" when it appears twice** — CP-12 multi-candidate grounding gap.

5. **NOT distinguish "Bluetooth, pairing" (subtitle phantom) from a real menu item** — CP-11 type reliability gap.

6. **NOT verify Wi‑Fi state from a toggle with empty text** — the toggle has no text label, so the Runtime cannot confirm what it controls from observation alone.

**Plain-language capability: the Runtime can walk a pre-planned route through real Settings if given exact element text at each step. It cannot discover the route, ground targets under ambiguity, or verify state changes from noisy observation.**

---

## Reality Contradictions

| Synthetic Belief | Real Evidence | Contradiction |
|---|---|---|
| "Wi‑Fi is a first-level Settings entry" | EP-04: Wi‑Fi is at depth 3 (Settings → Network & internet → Internet → Wi‑Fi) | **Depth assumption wrong** — the entire U1 navigation model based on 1-2 level depth is fiction |
| "Settings root has ~5-7 clean elements" | EP-04: 16 elements, 5 empty-text, 3 duplicates, 1 subtitle phantom | **Observation noise is an order of magnitude worse** than synthetic fixtures |
| "Element text uniquely identifies targets" | EP-04: "Network&internet" ×2, "Internet" ×2, "QSearch settings" ×3 | **Multi-candidate grounding is the NORM, not the exception** |
| "Element types are reliable" | EP-04: "Bluetooth, pairing" = menuitem (phantom); vision golden: WLAN has aliases | **Type labels and text are both unreliable** in production perception |
| "Page identity is inferable from 5-10 elements" | EP-04: 5 of 16 elements have empty text; fewer distinguishing features | **Page identity inference from real data is harder** than synthetic calibration assumed |

---

## True Capability Gaps

| Gap | Classification | Evidence |
|---|---|---|
| **Multi-level navigation from intent** | NEW_SEMANTIC_PRESSURE | Real Wi‑Fi is at depth 3; current Runtime requires pre-planned route |
| **Multi-candidate grounding at every level** | GROUNDING_GAP (CP-12) | "Network&internet" ×2, "Internet" ×2, "Wi‑Fi" vs "AndroidWifi" |
| **Noisy observation handling** | OBSERVATION_REPRESENTATION_GAP | 5 empty-text elements, 3 duplicates, subtitle phantom on ONE page |
| **Synonym/alias resolution** | GROUNDING_GAP | "WLAN" vs "Wi‑Fi" across devices |
| **State-change verification from ambiguous controls** | REALITY_EVIDENCE_GAP | Toggle with empty text on Internet page — what does it control? |
| **Page identity from noisy inventory** | CONTAINER_SEMANTIC_GAP | Fewer distinguishing features when 5/16 elements have empty text |

---

## New Semantic Pressure

**YES** — two pressures:

1. **Multi-level navigation discovery:** The real Settings hierarchy requires navigating through intermediate pages to reach the target. This is NOT covered by CP-14 (Intent→Goal synthesis, deferred) because CP-14 assumes the intent can be decomposed — but the decomposition itself requires knowledge of the app's page hierarchy, which is a REALITY problem (what pages exist between here and the target?), not just an intent problem.

2. **Observation quality awareness:** The Observation model has no representation of element quality/reliability. An element with empty text, an element whose type is low-confidence, a duplicate element — these are all represented identically to a high-confidence, uniquely-identified element. The Runtime has no way to say "I'm less sure about this observation than that one."

---

## Runtime Changes

**NONE** — this is a reality-grounding exercise. No Runtime code modified.

---

## Promoted Assets

**EP-04 Sim-Replay Export** promoted as `L3_RECORDED_REALITY` calibration asset.

- **Original:** `feature/refactor:artifacts/sim-replay/trace-replay-export.json`
- **Contains:** Real 4-page Android Settings hierarchy with typed elements
- **Calibration value:** Falsifies depth assumption (3 levels, not 1), demonstrates observation noise (empty text, duplicates, phantom elements), provides real CP-12 multi-candidate scenarios
- **Linked to:** U1 desired-state capability, CP-12 grounding, U3-F1 variation assets
- **Limitation:** Static snapshot — no state-change pairs, no before/after toggles

**E-10 TraceReplay Fixtures** promoted as `L3_RECORDED_REALITY` executable assets.

- **Original:** `feature/refactor:tests/UniClaw.Core.Tests/Simulation/TraceReplay/`
- **Contains:** Hand-reconstructed StateFixtures from real analysis.jsonl with REAL element coordinates
- **Calibration value:** Proves the current Runtime CAN execute against reconstructed real data (these tests PASS)
- **Limitation:** Reconstruction involved human judgment — not raw-to-Runtime

---

## Recommended Next Task

**REALITY_CAPTURE_WIFI_STATE_CHANGE** — capture the minimum emulator/device asset for Wi‑Fi state verification:

1. Settings root observation (screenshot + parsed elements)
2. Navigate to Network & internet
3. Network & internet observation
4. Navigate to Internet
5. Internet observation (pre-condition: Wi‑Fi state BEFORE action)
6. Toggle Wi‑Fi if needed
7. Internet observation (post-condition: Wi‑Fi state AFTER action)

This 3-page navigation + 1 state-change pair is the minimum reality capture needed to construct a true L3 replay that tests Observation → Container → Grounding → GoalEvidence against production-shaped recorded reality.

**Do not return to synthetic semantic design.** The real data has already falsified 5 of 6 assumptions about what the Runtime can do. The next step is to capture the missing evidence, not to design around it.

STOP.
