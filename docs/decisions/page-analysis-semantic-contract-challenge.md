# PageAnalysis Semantic Contract Challenge

> Generated: 2026-08-10
> Role: Runtime Architecture Analyst
> Baseline: `docs/decisions/semantic-evidence-minimum-contract-challenge.md` (Finalized 2026-08-10)
> Inputs: SemanticEvidence Phase 1 purchase (verified 12/12 tests) · Architecture Gap Review · RealitySeededSettingsFixture · AliasCollapseSourceIndependenceTests · 14 Architecture Invariants (I-1..I-14) · Agent→Container adjudication seam
> Scope: Analysis only — no production implementation, no OpenSpec, no Runtime modification, no capability purchase
> Question: Fresh Observation → what entitles the Runtime to propose "what page is this"?

---

## 1. Current Gap

### 1.1 What SemanticEvidence Solved

The SemanticEvidence Minimum Contract (finalized) established:

```
SemanticClaim { Assertion }
SemanticEvidence { Source, Stance, Reason? }
  where Stance = SUPPORTS / CONTRADICTS / INSUFFICIENT
SemanticBeliefState = SUPPORTED / UNRESOLVED / CONTRADICTED
```

This answers: **"How does evidence express itself, conflict, and fuse?"**

It does NOT answer: **"Where does page semantic evidence come from?"**

### 1.2 The Missing Producer

Current page semantic pipeline:

```
Observation → resolveSemanticPage(obs) → string → WorldBelief(Confidence=1.0)
     ↑              ↑                      ↑              ↑
  evidence      classifier               claim       belief==truth
```

The injected lambda `resolveSemanticPage: Func<Observation, string?>` simultaneously:
1. Reads evidence (observation)
2. Produces a claim (page name string)
3. Evaluates the claim (it IS the answer — no source, no stance)
4. Establishes belief (Confidence=1.0 binary)
5. Is treated as truth (no independent verification)

**Evidence = Claim = Belief = Truth** — a four-way collapse. The SemanticEvidence contract provides the types to un-collapse this, but **no production code produces SemanticEvidence about page identity from an Observation**. `Container.EvaluatePageBelief` exists but is production-unwired — only tests invoke it, and its LOCAL_IDENTITY stance is still derived from the same injected `_identityRule` lambda.

### 1.3 The Gap Statement

**SEMANTIC_GAP: PAGE_EVIDENCE_PRODUCER_MISSING**

There is no observation-scoped capability that, given a fresh Observation, produces source-attributed SemanticEvidence about page identity. The Runtime has the evidence *contract* (SemanticEvidence) and the fusion *logic* (SemanticReconciliation.FuseBelief) and the belief *owner* (Container._localPageBeliefState), but no evidence *producer*.

---

## 2. Current Runtime Truth

### 2.1 Observation Facts (raw perception — no semantic interpretation)

| Field | Type | Role |
|---|---|---|
| `Elements` | `ImmutableArray<ObservedElement>` | Raw element list — Text + SwitchState + Index |
| `ForegroundApplication` | `string?` | Raw foreground app identifier; null pre-attach |
| `SequenceNumber` | `long` | Monotonic observation ordinal — not a timestamp |
| `ObservedElement.Text` | `string` | Raw OCR/accessibility text |
| `ObservedElement.SwitchState` | `bool?` | Raw switch state; null = not a switch carrier |
| `ObservedElement.Index` | `int` | Stable in-observation ordinal — not a coordinate |

**No:** Fingerprint, ElementKind, coordinates, hierarchy, type labels, confidence — all explicitly deferred.

### 2.2 Page Identity Entry Points (all caller-injected)

| Entry Point | Type | Holder | Role |
|---|---|---|---|
| `resolveSemanticPage` | `Func<Observation, string?>` | Agent + Startup | Page classifier → `WorldBelief.SemanticPage` |
| `identityRule` | `Func<Observation, bool>` | Container | Still-mine verifier → `IsStillMine(obs)` |
| `containerFactory` | `Func<string, Container>` | Agent | Assembles Container with identityRule + stepExecutor |
| `Container._semanticPageName` | `string` (readonly) | Container | Immutable page name set at construction |
| `WorldBelief.SemanticPage` | `string?` | Agent (`_belief`) | Reconciled page name, recomputed every post-action observation |
| `Container._localPageBeliefState` | `SemanticBeliefState?` | Container | **NEW** — fused belief state from EvaluatePageBelief (production-unwired) |

### 2.3 Where SemanticEvidence Could Enter (but doesn't)

`Container.EvaluatePageBelief(Observation, params SemanticEvidence[])` — implemented, tested (12/12 pass), but **no production code path invokes it**. Agent never calls it. Traversal never calls it. The only caller is `AliasCollapseSourceIndependenceTests`.

The `additionalEvidence` parameter is the injection point where a PageAnalysis capability would deliver its output, but nothing currently fills it.

### 2.4 The Reconciliation Seam

`Reconcile.FromObservation(Observation, Func<Observation, string?>)` → `WorldBelief`:
- Stateless pure function
- Called by Agent (14 sites), NOT by Container
- Confidence is binary: 1.0 (resolver returned non-null) or 0.0 (null)
- Result held by Agent (`_belief`), NOT by Container

Target model (from Semantic Evidence contract): Container invokes pure reconciliation over (previous belief + fresh SemanticEvidence + adjudication) → Container holds revised belief. This is a SEMANTIC_GAP (logic relocation), not an ARCHITECTURE_PRESSURE (boundary change).

---

## 3. Legacy PageAnalysis Findings

Comprehensive search across the entire repository for PageAnalysis-related concepts.

### 3.1 Classification Summary

| Finding | Classification | Reason |
|---|---|---|
| `resolveSemanticPage` lambda pattern (Agent.cs + all test fixtures) | **REALITY_SUPPORTED** | The concept of a page classifier exists and works, but is collapsed into a single injected oracle |
| `Page()` heuristic in RealitySeededSettingsFixture (line 275-296) | **REALITY_SUPPORTED** | Element-anchor-based page classification using real EP-04 data — works but is self-referential (same function as classifier + verifier) |
| `Container._semanticPageName` as immutable string | **USEFUL_PRIOR** | Page-name-as-string is the right granularity; immutability is the wrong property (target: dynamic belief) |
| `Reconcile.FromObservation` as stateless pure function | **REALITY_SUPPORTED** | The pure-function pattern is correct; invocation location (Agent vs Container) is wrong |
| `WorldBelief.Confidence` as binary 0.0/1.0 | **OBSOLETE** | Replaced by SemanticBeliefState (SUPPORTED/UNRESOLVED/CONTRADICTED) |
| `Container.IsStillMine` via injected identity rule | **USEFUL_PRIOR** | Still-mine check is needed but must be evidence-backed, not a single oracle |
| `TryVerifyLocalContinuity` 4-condition check | **REALITY_SUPPORTED** | Freshness + foreground + identity + page-name equality is the right structure, but identity check is still single-lambda |
| `IsAgentScopeDrift` (Agent.cs:1566-1572) | **REALITY_SUPPORTED** | Foreground ≠ baseline AND !IsStillMine AND SemanticPage==null → drift — real pattern |
| Parent-return 1:1 text=page-name assumption (Agent.cs:601-640) | **CONTRADICTED** | Real Android Settings page names don't appear as visible UI text; synthetic fixtures fake this |
| No Fingerprint field in Observation (explicitly deferred, 裁决 2) | **REALITY_SUPPORTED** | Correct decision — fingerprint is evidence, not identity |
| No ElementKind/coordinates/hierarchy in ObservedElement (裁决 3/9) | **REALITY_SUPPORTED** | Correct decision — keeps Observation as evidence, not interpretation |
| `TargetGroundingEvidence` bool? tri-state pattern | **USEFUL_PRIOR** | The qualitative tri-state pattern (confirmed/rejected/insufficient) is the precedent SemanticEvidence generalizes |
| `CandidateAuthorizationEvidence` bool? tri-state pattern | **USEFUL_PRIOR** | Same pattern — caller-injected evaluator → immutable evidence value |
| `BranchProgressEvidence` composite evidence | **USEFUL_PRIOR** | Multi-source evidence aggregation precedent |
| No legacy "PageAnalysis" type ever existed in this codebase | **OBSOLETE** | No prior art to migrate — clean slate |
| No legacy "PageFingerprint" implementation | **OBSOLETE** | Explicitly deferred by architecture rulings; no code to migrate |
| No legacy "PageState" type | **OBSOLETE** | Page identity was always a string, never a state machine |
| No OCR aggregation / Vision result in Runtime | **OBSOLETE** | Correctly deferred to perception provider |
| No scroll state tracking | **UNRESOLVED** | ViewportExplorationObservations exists (Container.cs:84) but scroll identity is not modeled |
| No popup detection in Runtime | **UNRESOLVED** | IsLocalObstructionHypothesis exists but popup-specific evidence is not modeled |
| No page cache / prototype system | **UNRESOLVED** | Deferred to P3 (Semantic Memory) |

### 3.2 Legacy UniClaw.Core PageAnalysis (the predecessor system)

The sibling repo `uni-claw/` contains the predecessor `UniClaw.Core` with a full `PageAnalysis` record and AI-vision `PageAnalyzer`. Key legacy artifacts:

| Legacy Artifact | File | Classification | Why |
|---|---|---|---|
| `PageAnalysis` record | `uni-claw/.../PageAnalysisRecords.cs:31-138` | **OBSOLETE** | Ported from Python `content_models.py`; superseded by Runtime `Observation` |
| `PageAnalyzer` (AI vision) | `uni-claw/.../PageAnalyzer.cs:24-107` | **OBSOLETE** | Screenshot → sensenova VLM → JSON → PageAnalysis; replaced by perception-provider pattern |
| `PageFingerprint` | `uni-claw/.../PageAnalysisRecords.cs:79-90` | **CONTRADICTED** | Deterministic hash of sorted (type,name) tuples; recorded scroll evidence shows fingerprint changes mid-page on same page |
| `PageSnapshotManager.Fingerprint()` | `uni-claw/.../PageSnapshotManager.cs:20-58` | **CONTRADICTED** | Fingerprint-as-identity; OCR variants and scroll-driven item-set changes both alter fingerprint |
| `PageCacheManager` | `uni-claw/.../PageCacheManager.cs:21-50` | **CONTRADICTED** | Stale DynamicMatch caches caused max_steps exhaustion and error-retry loops on real runs (D-57/F-20) |
| `PageState` + `PageTransition` | `uni-claw/.../StateFixture.cs:12-121` | **SYNTHETIC_ONLY** | Simulation assumption: "page identity is known from fixture — no page-recognition step" |
| `HasScroll` / `IsEndOfList` fields | `PageAnalysisRecords.cs:76-77, 93-94` | **OBSOLETE** | Marked `[Obsolete]` — replaced by ROI scroll decisions (D9) |
| `PopupHandler` + `PopupDetector` | `uni-claw/.../PopupHandler.cs:46-525` | **USEFUL_PRIOR** | Regex heuristic classification with per-type dismiss strategies; ANR fix recorded as real-device fix |
| `SnapshotComparer` (ROI) | `uni-claw/.../SnapshotComparer.cs:21-74` | **REALITY_SUPPORTED** | Perceptual-hash + mean-absolute-difference for scroll stability — the production scroll mechanism |
| `VisualPageAnalyzer` | `uni-claw/.../HostCommands.cs:599-606` | **CONTRADICTED** | `CurrentPath=[]` hardcode made `expectedPageIdentities` never match on real device (D-197/D-235) |
| `AdbScreenStateProvider` | legacy scroll detection | **CONTRADICTED** | Swallowed ADB failures → `IsEnd=true` (E-13); scroll failure indistinguishable from end-of-list |
| `fusion.py` OCR-YOLO fusion | `uni-claw/tools/local_vision/fusion.py` | **USEFUL_PRIOR** | Per-detection nearest-token matching + chevron-alignment heuristic; NOT a text multiset |
| `label-mapping.json` | `uni-claw/tools/local_vision/label-mapping.json` | **REALITY_SUPPORTED** | Deki-Yolo 21 labels → element types + nonItemLabels; drove recorded `analysis.jsonl` |
| A3 recorded `analysis.jsonl` | 185 frames, run `20260805T052309367Z` | **REALITY_SUPPORTED** | Real PageAnalysis frames with fingerprints, itemCount, hasScroll, isEndOfList, isPopup, items[] |

**Root failure mode:** Legacy `PageFingerprint` (hash of type+name tuples) was used as page identity. Recorded reality proved this wrong: scroll changes visible items → fingerprint changes; OCR variants produce different text → fingerprint changes; same semantic page gets different fingerprints. The Runtime's answer — `Observation` without Fingerprint field, `WorldBelief` tri-state, qualitative `SemanticEvidence` fusion — is the direct, evidence-backed replacement.

### 3.3 Key Insight

**This is a greenfield Runtime.** No legacy PageAnalysis type exists in `src/UniClaw.Runtime/`. The legacy `UniClaw.Core.PageAnalysis` record and `PageAnalyzer` are in the sibling repo and have been superseded. The gap is not "we had PageAnalysis and removed it" — it's "we never had an observation-scoped, multi-source page semantic evidence producer." The entire page semantic pipeline is caller-injected lambdas. This simplifies the contract design: no backward compatibility constraints, no migration burden.

---

## 4. Reality Evidence

### 4.1 Reality Pages (from EP-04 sim-replay + A4 E-10 TraceReplay + B1 real-device)

**A3 EP-04 sim-replay** (`trace-replay-export.json`, run `20260805T083146853Z`, 19 actions: 14 clicks + 5 scrolls):

| Page | ForegroundApp | Elements (A3 type labels: menuitem / text / toggle) | Recorded? |
|---|---|---|---|
| Launcher (root, 5 elements) | null (pre-attach) | "GOoQle" (text), "Gallery" (text), 3× empty menuitem | EP-04 |
| SettingsRoot (16 elements) | com.android.settings | "Settings" (text title), "QSearch settings"×3 (text), "Network&internet"×2 (menuitem, DUPLICATE), "Connected devices" (menuitem), empty menuitem×5, "Bluetooth, pairing" (menuitem **SUBTITLE PHANTOM**), "Apps" (menuitem), "Recent apps,default apps" (menuitem, OCR-collapsed), "Notifications" (menuitem), "Notification history, conversations" (menuitem) | EP-04 |
| NetworkInternet (21 elements) | com.android.settings | "Network & internet" (text title), "Internet"×2 (menuitem, DUPLICATE), "SIMs"×4 (menuitem), empty menuitem×5, empty **toggle**×1, "Airplane mode" (menuitem), "Hotspot & tethering"×2 (text), "Off" (text), "Data Saver"×2 (menuitem), "VPN"×2 (text) | EP-04 |
| InternetPage (14 elements) | com.android.settings | "Internet" (text title), "T-Mobile"×2 (menuitem), empty menuitem×4, empty **toggle**×1 (presumed Mobile data), "Wi‑Fi" (menuitem entry, NO SwitchState), "AndroidWifi" (menuitem, connected SSID), "Add network" (menuitem), "Networkpreferences" (text, OCR-collapsed), "Wi-Fi doesn't turn backon automatically" (text, FALSE substring anchor for "Wi‑Fi"), "Non-carrier data usage" (text) | EP-04 |
| WifiPage (4 elements) | com.android.settings | "Wi‑Fi" **with SwitchState**, "AndroidWifi", "Auto-connect" (SwitchState=true), "Network preferences" | **SYNTHETIC** |
| WifiOnPage (4 elements) | com.android.settings | "Wi‑Fi" **SwitchState=true**, "AndroidWifi", "Auto-connect" (SwitchState=true), "Connected devices" | **SYNTHETIC** |

**A4 E-10 TraceReplay** (recorded coordinates, hand-reconstructed from `analysis.jsonl` of run `20260805T052309367Z`):
- Real depth hierarchy: Wi‑Fi entry sits at **depth 3** (Settings → Network & internet → Internet → Wi‑Fi), falsifying the synthetic depth-1 assumption
- Real self-loop: `Internet → Internet` transition recorded in A4 fixtures
- Real coordinates: Wi‑Fi @(0.5,0.15), Mobile data switch @(0.85,0.28), "Bluetooth, pairing" subtitle same-row as "Network&internet" (dy_full=0.0336 vs 0.035 threshold)

**B1 Real-device golden** (PKJ110, Chinese ROM, 1440×3168):
- Same semantic element, different text: `"WLAN"` (Chinese ROM) vs `"Wi‑Fi"` (A3/A4 emulator)
- Aliases: `["Wi-Fi","WiFi","无线局域网"]`
- 59 YOLO detections, 17 OCR strings → 53 candidates (many empty-named)
- Page-level implication: text anchors for page identity must support aliases; "WLAN page" == "Wi‑Fi page" semantically

### 4.2 Page-Level Evidence Signals Extractable from Observation Alone

| Signal | Present in Observation? | Distinguishing Power | Reality Example |
|---|---|---|---|
| **ForegroundApplication** | ✅ `Observation.ForegroundApplication` | Coarse: separates Launcher (pre-attach) from Settings pages | "com.android.settings" vs null |
| **OCR text multiset** | ✅ `ObservedElement.Text` on all elements | High: each page has a distinctive text signature | "Network&internet"+"SIMs" → NetworkInternet; "Wi‑Fi"+"T-Mobile" → InternetPage |
| **Semantic text anchors** | ✅ Specific known text strings | High for known pages: presence of anchor strongly supports page | "Network&internet" → SettingsRoot; "Airplane mode" → NetworkInternet; "T-Mobile" → InternetPage |
| **Duplicate label count** | ✅ Multiple elements with same Text | Medium: SettingsRoot has "Network&internet"×2; NetworkInternet has "Internet"×2, "SIMs"×3 | Duplicate count is a structural signal |
| **Empty text count** | ✅ Elements with Text=="" | Low-Medium: SettingsRoot=5, NetworkInternet=5, InternetPage=5 — not distinguishing alone | But ratio of empty:non-empty varies |
| **SwitchState distribution** | ✅ `ObservedElement.SwitchState` | High for specific pages: WifiPage has SwitchState-bearing "Wi‑Fi" | Presence of SwitchState on known anchor text is a strong signal |
| **Coarse element count** | ✅ `Elements.Length` | Medium: SettingsRoot=16, NetworkInternet=21, InternetPage=14 | Element count ranges can narrow candidates |
| **Has SwitchState elements?** | ✅ Derived from Elements | Medium-High: SettingsRoot has 0, InternetPage has 1 (empty toggle), WifiPage has 2 | SwitchState count is a structural signal |
| **Known anchor presence** | ✅ Derived from text matching | High: "Auto-connect" → WifiPage/WifiOnPage; "Connected devices" → SettingsRoot OR WifiOnPage | Anchors can be page-specific |
| **Element role distribution** | ❌ Not in ObservedElement | Would be High if available: toggle count, menuitem count, text label count | Deferred — requires ElementCategory evidence |
| **Scroll position** | ❌ Not observed | Would distinguish viewport variations of same page | Deferred — no scroll signal in Observation |
| **Visual layout / spatial** | ❌ Not in ObservedElement | Would be High: spatial arrangement, hierarchy depth | Deferred — perception provider concern |

### 4.3 Distinguishing Challenging Page Pairs

**InternetPage vs WifiPage (the alias-collapse pair):**
- InternetPage: "Wi‑Fi" (no SwitchState), "AndroidWifi" (no SwitchState), "T-Mobile" ×2, "Add network", "Networkpreferences", "Wi-Fi doesn't turn back on automatically"
- WifiPage: "Wi‑Fi" (SwitchState≠null), "AndroidWifi", "Auto-connect" (SwitchState=true), "Network preferences"
- **Distinguishing signals:** presence of SwitchState on "Wi‑Fi" text; absence of "T-Mobile"; presence of "Auto-connect"
- **Ambiguity:** Both have "Wi‑Fi" text and "AndroidWifi"; text-anchor alone is insufficient → TRANSITION evidence needed to resolve

**WifiPage vs WifiOnPage (state mutation pair):**
- WifiPage: "Wi‑Fi" with SwitchState=false, "Auto-connect" with SwitchState=true
- WifiOnPage: "Wi‑Fi" with SwitchState=true, "Auto-connect" with SwitchState=true, "Connected devices"
- **Distinguishing signals:** "Wi‑Fi" SwitchState value; presence of "Connected devices"
- **Semantic identity:** Same semantic page (Wi‑Fi settings), different state (OFF vs ON)

**SettingsRoot vs NetworkInternet (parent-child with shared text):**
- SettingsRoot: "Network&internet" ×2, "Connected devices", "Apps", "Notifications"
- NetworkInternet: "Internet" ×2, "SIMs" ×3, "Airplane mode", "Hotspot & tethering"
- **Distinguishing signals:** "SIMs" is unique to NetworkInternet; "Apps"/"Notifications" unique to SettingsRoot
- **Semantic similarity risk:** "Network" text appears in both — but different specific strings

### 4.4 Cross-Device Reality (B1)

- PKJ110 (Chinese ROM): element text "WLAN" with aliases ["Wi‑Fi", "WiFi", "无线局域网"]
- A3/A4 emulator: literal "Wi‑Fi"
- **Page-level implication:** Text anchors for page identity must support aliases. "WLAN page" == "Wi‑Fi page" semantically.

---

## 5. PageAnalysis Definition

### 5.1 Canonical Definition

> **PageAnalysis is an observation-scoped, stateless, evidence-producing semantic capability that, given a single fresh Observation, generates source-attributed SemanticEvidence hypotheses about page identity — it is evidence, not truth, and does not own mutable state, verify page continuity, or verify navigation transitions.**

### 5.2 Key Properties

| Property | Meaning |
|---|---|
| **Observation-scoped** | Operates on ONE Observation. Does not compare across observations. |
| **Stateless** | Pure function. No mutable state. Same input → same output. |
| **Evidence-producing** | Output is SemanticEvidence[], not a page name string, not a belief state. |
| **Not a truth oracle** | Can be wrong. Can be unresolved. Can expose competing hypotheses. |
| **Not a state owner** | Does not hold local page belief — Container does (I-2). |
| **Not a continuity verifier** | Does not answer "same page as before?" — that's reconciliation. |
| **Not a transition verifier** | Does not answer "did navigation succeed?" — that's Traversal. |

### 5.3 What PageAnalysis Is NOT

| NOT | Why |
|---|---|
| **NOT a page classifier** | A classifier returns a single answer. PageAnalysis returns evidence (which may be insufficient or contradictory). |
| **NOT a page engine** | No internal state machine, no page graph, no navigation model. |
| **NOT a page cache** | No memory of previous pages. P3 concern. |
| **NOT a fingerprint system** | Fingerprint is evidence, not identity. PageAnalysis may USE fingerprint-like signals as evidence, but does not equate fingerprint with identity. |
| **NOT a VLM/Vector consumer** | Provider-neutral. Evidence from any source arrives as SemanticEvidence. |
| **NOT a Container replacement** | Container owns local belief. PageAnalysis produces evidence for Container to reconcile. |

---

## 6. PageAnalysis vs SemanticEvidence

### 6.1 Relationship

```
PageAnalysis capability
        │
        │ produces
        ↓
SemanticEvidence[]
        │
        │ fused by
        ↓
SemanticReconciliation.FuseBelief()
        │
        │ produces
        ↓
SemanticBeliefState
        │
        │ stored by
        ↓
Container._localPageBeliefState
```

**PageAnalysis PRODUCES SemanticEvidence. It does not replace or bypass it.**

### 6.2 Example

```text
Observation: InternetPage elements (Wi‑Fi entry, AndroidWifi, T-Mobile, ...)

PageAnalysis produces:
  SemanticEvidence {
    Source = "TEXT_ANCHOR",
    Claim  = "page is InternetPage",
    Stance = Supports,
    Reason = "text anchors 'T-Mobile' + 'Add network' + 'Networkpreferences' present"
  }
  SemanticEvidence {
    Source = "TEXT_ANCHOR",
    Claim  = "page is WifiPage",
    Stance = Contradicts,
    Reason = "no 'Auto-connect' anchor; Wi‑Fi element lacks SwitchState"
  }
  SemanticEvidence {
    Source = "FOREGROUND",
    Claim  = "page is within com.android.settings",
    Stance = Supports,
    Reason = "ForegroundApplication matches expected Settings app"
  }
```

### 6.3 The SemanticEvidence Contract Unchanged

PageAnalysis uses the EXISTING SemanticEvidence contract without modification:

```
SemanticEvidence { Source, Claim, Stance, Reason? }
  where Stance = SUPPORTS / CONTRADICTS / INSUFFICIENT
```

No new fields. No new stance values. No new contract.

---

## 7. PageAnalysis vs Container PageBelief

### 7.1 The Distinction

| | PageAnalysis | Container PageBelief |
|---|---|---|
| **Scope** | Single Observation | Cross-observation, reconciled |
| **Answers** | "What does THIS observation look like?" | "What page am I on?" |
| **State** | Stateless pure function | Mutable, owned by Container (I-2) |
| **Inputs** | Fresh Observation only | Previous belief + current PageAnalysis + transition evidence + possible Agent adjudication |
| **Outputs** | SemanticEvidence[] | SemanticBeliefState |
| **Authority** | None — produces evidence | Container is sole state owner |
| **Can be wrong?** | Yes — evidence can be insufficient or contradictory | Yes — belief can be wrong; external world authoritative (I-4) |

### 7.2 The Pipeline

```
Fresh Observation
        │
        ↓
PageAnalysis (stateless capability)
        │
        ↓ SemanticEvidence[]
        │
Container.EvaluatePageBelief(observation, ...PageAnalysis evidence)
        │
        ├── LOCAL_IDENTITY evidence (Container's own identity rule)
        │
        ↓ fusion via SemanticReconciliation.FuseBelief()
        │
Container._localPageBeliefState ← SUPPORTED / UNRESOLVED / CONTRADICTED
        │
        │ if UNRESOLVED or CONTRADICTED:
        ↓
Container → Trap → Agent adjudication (existing seam)
```

### 7.3 PageAnalysis != PageBelief != Truth

```
PageAnalysis evidence ≠ Container PageBelief ≠ External world truth
```

PageAnalysis says "this observation supports page X" (evidence).
Container PageBelief says "I believe I am on page X" (fused conclusion).
External world says "the device IS on page X" (authoritative, I-4).

---

## 8. Minimum Inputs

### 8.1 Candidate Inputs — Challenged

| Candidate Input | Required? | Rationale |
|---|---|---|
| **Fresh Observation** | **REQUIRED** | The sole input. Elements + ForegroundApplication + SequenceNumber. |
| Previous PageBelief | **NOT_INPUT** | Belongs to reconciliation, not observation-scoped analysis. |  
| Goal context | **NOT_INPUT** | PageAnalysis is goal-agnostic — same page looks the same regardless of task. |
| Last verified action | **NOT_INPUT** | Belongs to transition verification (Traversal), not page identity evidence. |
| Parent Container belief | **NOT_INPUT** | Belongs to cross-container context (Agent adjudication). |
| Transition history | **NOT_INPUT** | Belongs to continuity/transition (Container reconciliation + Traversal journal). |
| Task scope / intent | **NOT_INPUT** | PageAnalysis should produce the same evidence regardless of why we're looking. |

### 8.2 Minimum Input Contract

```
PageAnalysis.Input = Fresh Observation
  ├── Elements: ImmutableArray<ObservedElement>
  │     ├── Text: string
  │     ├── SwitchState: bool?
  │     └── Index: int
  ├── ForegroundApplication: string?
  └── SequenceNumber: long
```

**That's it.** One input. No context. No history. No goal.

---

## 9. Minimum Outputs

### 9.1 Candidate Output Forms — Challenged

**Option A: PageAnalysis = SemanticEvidence[]**

Simplest. Directly produces the evidence contract. No new types.

```
PageAnalysis.FromObservation(Observation) → SemanticEvidence[]
```

**Option B: PageAnalysisResult { Hypotheses, Evidence }**

Adds a grouping type. Useful if multiple claims need to be grouped.

```
PageAnalysisResult {
  Claims: SemanticClaim[]         // "page is X", "page is Y"
  Evidence: SemanticEvidence[]    // per-claim, per-source stances
}
```

**Option C: No production type — capability only**

The capability exists as a pure function. No named return type beyond what already exists.

### 9.2 Verdict

**PageAnalysisConceptRequired: YES**

The capability gap is real and purchased by the alias-collapse falsifier. Currently, 100% of page semantic evidence comes from a single injected lambda. A multi-source, observation-scoped evidence producer is the missing piece.

**PageAnalysisTypeRequiredNow: NO**

Following the `Reconcile` pattern (stateless pure function, no instance state), a `PageAnalysis` static class with a pure `FromObservation` method is sufficient for the minimum purchase. The return type is `SemanticEvidence[]` — the existing evidence contract. No new type definition is required now.

A `PageAnalysisResult` record MAY become useful later when competing hypotheses need explicit grouping, but no current falsifier purchases it.

### 9.3 Minimum Output Contract

```
PageAnalysis → SemanticEvidence[]
  where each SemanticEvidence = { Source, Claim, Stance, Reason? }
    Source  ∈ { FOREGROUND, TEXT_ANCHOR, STRUCTURAL, ... }
    Claim   = "page is <semantic-page-name>"
    Stance  ∈ { Supports, Contradicts, Insufficient }
    Reason  = optional explanation
```

No new types. No new stances. No new fields on SemanticEvidence.

---

## 10. Element Dependency

### 10.1 The Challenge

The original (now-rejected) semantic model assumed:

```
Element Semantics → Page Semantics  (linear dependency)
```

The corrected model (Architecture Gap Review §2.2):

```
Raw Observation
        |
        +----------------+
        |                |
        v                v
 Element Evidence    Page Evidence
        |                |
        +-------+--------+
                |
                v
        Semantic Belief
```

**Element and Page are parallel semantic projections, not linear dependencies.**

### 10.2 Can PageAnalysis Work Without Full Element Identity?

**YES.** PageAnalysis can consume screen-level signals that do not require knowing "this specific element is the Wi‑Fi entry":

| Screen-Level Signal | Element Identity Required? | Example |
|---|---|---|
| ForegroundApplication match | No | "com.android.settings" → supports Settings-family pages |
| Text multiset presence/absence | No — just text inventory | "SIMs" present → supports NetworkInternet; "T-Mobile" present → supports InternetPage |
| SwitchState distribution | No — just count + which text has it | "Wi‑Fi" with SwitchState≠null → supports WifiPage |
| Coarse element count | No | 14 elements → narrows candidates |
| Empty text ratio | No | 5/16 vs 5/21 vs 5/14 |
| Known anchor presence | Partial — text match, not identity resolution | "Auto-connect" → strongly supports WifiPage/WifiOnPage |

### 10.3 Verdict

**PAGE_ANALYSIS_DEPENDS_ON_FULL_ELEMENT_MODEL: PARTIAL**

PageAnalysis can produce useful page identity evidence from screen-level signals (text multiset, foreground app, SwitchState distribution, coarse element count) WITHOUT requiring every element to have resolved semantic identity.

However, some page distinctions MAY benefit from element-level evidence:
- "Wi‑Fi entry" vs "AndroidWifi SSID" → element identity helps
- "Subtitle phantom" vs "real menu entry" → element existence helps

These are OPTIMIZATIONS, not prerequisites. The minimum PageAnalysis works with what Observation already provides.

**Precise element grounding remains a separate semantic lane.** PageAnalysis and ElementAnalysis are parallel consumers of the same Observation, not sequential dependencies.

---

## 11. Fast Semantic Path

### 11.1 Role

The fast semantic path produces SemanticEvidence from deterministic, low-latency signals available directly in Observation. No external service calls. No model inference.

### 11.2 Candidate Fast Evidence Sources

| Source Label | Signal | How |
|---|---|---|
| `FOREGROUND` | `Observation.ForegroundApplication` | Match against expected app; Supports if matches, Contradicts if different app |
| `TEXT_ANCHOR` | Specific known text strings in Elements | Presence of anchor → Supports page claim; absence of expected anchor → Contradicts |
| `TEXT_ANCHOR_NEGATIVE` | Specific known text strings that SHOULD NOT be present | Presence of "SIMs" contradicts "page is SettingsRoot" (SIMs is in NetworkInternet) |
| `STRUCTURAL_COARSE` | Element count range, SwitchState count, empty text count | Coarse structural signals as supporting/contradicting evidence |
| `SWITCH_DISTRIBUTION` | Which elements have SwitchState, what values | "Wi‑Fi" with SwitchState → likely WifiPage; "Auto-connect" with SwitchState → confirms |

### 11.3 Fast Path Produces SemanticEvidence

Same contract. Same stances. No special status. Fast evidence can be wrong — it is evidence, not truth.

```
FAST: SemanticEvidence { Source=TEXT_ANCHOR, Claim="page is InternetPage",
       Stance=Supports, Reason="anchors 'T-Mobile' + 'Add network' present" }
FAST: SemanticEvidence { Source=TEXT_ANCHOR, Claim="page is WifiPage",
       Stance=Contradicts, Reason="missing 'Auto-connect' anchor" }
```

### 11.4 What the Fast Path Does NOT Do

- ❌ No text embedding similarity (deferred to perception provider, I-14)
- ❌ No YOLO type label trust (RM-09: type labels are perception outputs, not world facts)
- ❌ No element role classification (requires ElementCategory evidence — separate lane)
- ❌ No hierarchy / depth analysis (no hierarchy data in ObservedElement)
- ❌ No numeric scoring (qualitative stances only)

---

## 12. Slow Semantic Path

### 12.1 Role

The slow semantic path produces SemanticEvidence from higher-intelligence sources: VLM reasoning, broader task context, cross-container context. Same evidence contract, different Source label.

### 12.2 Candidate Slow Evidence Sources

| Source Label | Signal | How |
|---|---|---|
| `VISUAL_SEMANTIC` | VLM reasoning about screen content | "VLM identifies this as Wi‑Fi settings page" |
| `TASK_CONTEXT` | Goal/intent context (Agent-scope) | "Goal requires Wi‑Fi toggle — this page has Wi‑Fi switch → likely correct page" |
| `CROSS_CONTAINER` | Parent/child relationship, transition history | "We navigated here from InternetPage via Tap 'Wi‑Fi' → this should be WifiPage" |

### 12.3 Slow Path Constraints

- Same contract: `SemanticEvidence { Source, Stance, Reason? }`
- No special truth status: VLM evidence is still evidence, not truth (I-4, I-14)
- Provider-neutral: Runtime does not call VLM directly — evidence arrives as immutable values
- Agent-scope: Slow evidence may come from Agent adjudication (higher semantic authority)
- Not required now: No current falsifier purchases VLM evidence for page identity

### 12.4 Fast/Slow Boundary

```
FAST: deterministic, Observation-only, stateless
SLOW: model-inferred, context-aware, may use Agent context

Both produce: SemanticEvidence { Source, Claim, Stance, Reason? }
Neither produces: truth, belief, or mutable state
```

The contract is provider-neutral. Whether evidence came from OCR or VLM is recorded in Source but does not change the fusion logic.

---

## 13. Confidence

### 13.1 The Challenge

Legacy systems (YOLO, OCR) produce numeric confidence scores. The subtitle phantom had 91.9% classification rate — numeric confidence did not prevent the error.

**Score != semantic correctness.**

### 13.2 Current State

`WorldBelief.Confidence` is binary: 1.0 (resolver returned non-null) or 0.0 (null). `SemanticBeliefState` is qualitative: SUPPORTED / UNRESOLVED / CONTRADICTED.

### 13.3 Verdict

**NUMERIC_CONFIDENCE: USEFUL_LATER**

| Reason | Detail |
|---|---|
| **No falsifier purchases it now** | Alias-collapse needs CONTRADICTED (qualitative). Scroll needs UNRESOLVED (qualitative). State mutation needs SUPPORTED (qualitative). |
| **Existing pattern is qualitative** | TargetGroundingEvidence uses `bool?` tri-state. SemanticEvidence uses enum stance. |
| **Score can mislead** | 91.9% subtitle phantom. High confidence ≠ correct. |
| **Future uses exist** | Ranking competing hypotheses, fast/slow routing thresholds, ambiguity detection, candidate comparison. |

**Numeric confidence must not become semantic truth.** A 0.95 confidence is not "this IS page X" — it is "multiple evidence sources strongly support page X, with no contradiction."

---

## 14. Fingerprint Role

### 14.1 Classification by Fingerprint Type

| Fingerprint Type | Role | Rationale |
|---|---|---|
| **ScreenshotFingerprint** | **CACHE_HINT** + **CHANGE_SIGNAL** | Pixel-level similarity can suggest "this looks like a previously-seen page" but scroll, theme, device resolution change it. Not identity. |
| **ElementFingerprint** (element hash set) | **CHANGE_SIGNAL** | Element set changes on scroll, dynamic content, state mutation. Useful as a change detector, not as identity. |
| **TextFingerprint** (text multiset) | **SUPPORTING_EVIDENCE** | Text inventory is strong evidence for page identity but not authoritative — two pages can share text (InternetPage and WifiPage both have "Wi‑Fi"). |
| **SemanticVectorEmbedding** | **SUPPORTING_EVIDENCE** | Embedding similarity is evidence, not identity. Belongs to perception provider (I-14), not Runtime. |

### 14.2 None Is Authoritative

**No fingerprint type is AUTHORITATIVE_IDENTITY.** Charter principle: "Fingerprint 是 evidence，不是 identity" (I-6).

Scroll changes visible elements → fingerprint changes → page semantic identity unchanged.
Dynamic content loads → fingerprint changes → page semantic identity unchanged.
State mutation (OFF→ON) → SwitchState changes → fingerprint changes → page semantic identity unchanged.

**Fingerprint = CHANGE_SIGNAL.** It says "something changed," not "the page changed."

### 14.3 Fingerprint as Evidence

Any fingerprint can be expressed as SemanticEvidence:

```
SemanticEvidence {
  Source = "TEXT_FINGERPRINT",
  Claim  = "page is SettingsRoot",
  Stance = Supports,
  Reason = "text multiset overlap 13/16 with known SettingsRoot prototype"
}
```

Fingerprint does not need a separate representation — it fits the evidence contract.

---

## 15. Continuity Boundary

### 15.1 What PageAnalysis Does NOT Answer

PageAnalysis answers: "What does THIS observation look like?"

PageAnalysis does NOT answer: "Is this the SAME page as the previous observation?"

### 15.2 What Continuity Requires

Page continuity is a RECONCILIATION decision, not an observation-scoped analysis:

```
Page Continuity = f(
    Previous Container PageBelief,     // "I believed I was on page X"
    Current PageAnalysis evidence,     // "This observation supports page Y"
    Last Verified Action,              // "I just performed action A"
    Transition Evidence                // "Action A's expected effect was ..."
)
```

### 15.3 Why Semantic Similarity != Page Continuity

| Scenario | Semantic Similarity | Page Continuity |
|---|---|---|
| Scroll down same page | Text set partially different → similarity < 1.0 | **Same page** |
| Tap Wi‑Fi (InternetPage → WifiPage) | Both have "Wi‑Fi" + "AndroidWifi" → similarity HIGH | **Different page** |
| SetSwitch Wi‑Fi OFF→ON | SwitchState changes → text set almost identical | **Same page** |
| Dynamic content loads | New elements appear → similarity drops | **Same page** |
| Navigate to completely different app | Similarity ≈ 0 | **Different page** |

**High semantic similarity does NOT imply continuity. Low similarity does NOT imply discontinuity.**

### 15.4 Boundary

```
PageAnalysis: observation-scoped, produces identity evidence
Continuity:    cross-observation, reconciles evidence + action + transition
```

These are distinct semantic operations. PageAnalysis provides INPUT to continuity decisions; it does not MAKE continuity decisions.

---

## 16. Transition Boundary

### 16.1 What PageAnalysis Does NOT Answer

PageAnalysis answers: "What page does this observation support?"

PageAnalysis does NOT answer: "Did action A successfully navigate from page X to page Y?"

### 16.2 What Transition Verification Requires

Transition verification is owned by Traversal:

```
Traversal.Verify(postActionObservation):
  1. SequenceNumber advanced? (freshness)
  2. PostActionEvaluator (caller-injected) verifies expected destination
  3. TraversalJournalEntry records action + post-action observation
```

### 16.3 PageAnalysis as Transition Evidence Provider

PageAnalysis CAN provide evidence USED BY transition verification:

```
PageAnalysis(postActionObservation) → SemanticEvidence[]
   SemanticEvidence { Source=TEXT_ANCHOR, Claim="page is WifiPage", Stance=Supports }

Transition verification consumes this as input to:
  "Did Tap 'Wi‑Fi' successfully produce navigation to WifiPage?"
```

But PageAnalysis itself does not verify the transition. Traversal owns local effect verification.

### 16.4 Boundary

```
PageAnalysis:      "This observation supports page WifiPage" (identity evidence)
Transition Verif:  "Tap 'Wi‑Fi' → WifiPage transition confirmed" (effect verification)
```

These are distinct semantic lanes. PageAnalysis provides INPUT to transition verification; it does not REPLACE it.

---

## 17. Agent Authority / Container Ownership

### 17.1 Ownership

**Container is the sole owner (I-2) of local page belief state.**

```
Container owns:
  _localPageBeliefState: SemanticBeliefState?
  _semanticPageName: string (currently immutable — target: dynamic)
  _observation: Observation?
  _executedSteps: ImmutableArray<PlanStep>
  _viewportExplorationObservations: ImmutableArray<Observation>
```

**Agent does NOT hold a duplicate of Container local semantic state.** Agent holds `_belief: WorldBelief?` (run-level, reconciled every observation), which REFERENCES page identity but does not independently maintain local page truth.

### 17.2 Authority

**Agent has higher semantic adjudication authority (≠ state ownership).**

| | Container | Agent |
|---|---|---|
| **State Ownership** | SOLE OWNER of local semantic state (I-2) | SOLE OWNER of run-level state |
| **Decision Authority** | Local continuity, local completion | Run-level: completion (I-10), drift, recovery, Container switching, semantic adjudication |

### 17.3 The Adjudication Flow

```
PageAnalysis evidence
        ↓
Container local belief ← Container owns (I-2)
        │
        │ if UNRESOLVED or CONTRADICTED:
        ↓
Container → Trap (escalation up, I-8)  ← EXISTING seam
        ↓
Agent high intelligence:
  - Goal context
  - Task intent
  - Cross-container history
  - Transition history
  - VLM reasoning (future)
        ↓
Agent adjudication:
  ADJUDICATE — determine correct page
  CORRECT     — CreateContainer(correctedPage) + Bind (EXISTING seam)
  REBIND      — Bind new observation
  INVALIDATE  — Drift detection → Trap → recovery
        ↓
Container ← revised/rebuilt
Container still owns local state (I-2)
```

**Authority ≠ Ownership.** Agent adjudicates; Container owns state.

### 17.4 Existing Seam Sufficient

The existing Trap + CreateContainer/Bind seam carries the adjudication for the minimum purchase. No new API needed.

**Deferred:** "Revision without rebind" — Agent sends semantic revision to EXISTING Container, preserving local progress. Not purchased by first falsifier.

---

## 18. Reality Falsifiers

### 18.1 F1: Alias Collapse (PRIMARY — already executable)

**Scenario:** InternetPage and WifiPage both match a coarse heuristic for "WifiSub."

**Current behavior:** `resolveSemanticPage` returns "WifiSub" for both. `identityRule` uses same `Page()` function → confirms "WifiSub" → unfalsifiable.

**Required behavior:** TEXT_ANCHOR source SUPPORTS "WifiSub" (both have SwitchState Wi‑Fi). TRANSITION source CONTRADICTS "WifiSub" (navigation changed inventory). Fusion → CONTRADICTED → alias-collapse detected.

**Status:** ✅ Executable — `AliasCollapseSourceIndependenceTests.cs` (12/12 pass)

### 18.2 F2: Scroll Same-Page

**Scenario:** Observation A (top viewport) and Observation B (bottom viewport) of the same page have substantially different visible element sets.

**Challenge:** PageAnalysis must produce consistent page identity evidence (not "different page" just because visible elements changed).

**Falsifier:** PageAnalysis(Observation A) → SemanticEvidence SUPPORTS "SettingsRoot". PageAnalysis(Observation B) → SemanticEvidence SUPPORTS "SettingsRoot". Belief remains SUPPORTED despite fingerprint change.

**Evidence gap:** No scroll observation pairs in committed reality assets. SYNTHETIC only.

### 18.3 F3: Persistent Header Parent/Child

**Scenario:** Parent (SettingsRoot) and Child (NetworkInternet) both have "Network" + "Internet" text. Semantic similarity is high.

**Challenge:** PageAnalysis must NOT conflate semantic similarity with page identity. High text overlap does not mean same page.

**Falsifier:** PageAnalysis(SettingsRoot) → SUPPORTS "SettingsRoot". PageAnalysis(NetworkInternet) → SUPPORTS "NetworkInternet". The same evidence channels produce DIFFERENT page claims despite text overlap.

### 18.4 F4: Same-Page State Mutation

**Scenario:** Wi‑Fi OFF → SetSwitch → Wi‑Fi ON. SwitchState changes but page semantic identity is stable.

**Challenge:** PageAnalysis must produce consistent page identity evidence across state mutation. SwitchState is a state signal, not an identity signal.

**Falsifier:** PageAnalysis(WifiPage_OFF) → SUPPORTS "WifiPage". PageAnalysis(WifiPage_ON) → SUPPORTS "WifiPage". SwitchState change does not trigger page identity change.

**Evidence gap:** No OFF→ON observation pair in committed reality. SYNTHETIC only.

### 18.5 F5: Unknown Page

**Scenario:** New observation with no matching known anchors, unfamiliar foreground app, unrecognizable element inventory.

**Challenge:** PageAnalysis must produce INSUFFICIENT evidence, not force a nearest-match classification.

**Falsifier:** PageAnalysis(UnknownObs) → all SemanticEvidence have Stance=Insufficient → fusion → UNRESOLVED. No "best guess" page name forced.

### 18.6 F6: Reordered/Dynamic Elements

**Scenario:** Same page, same elements, different Index ordering (dynamic list, async load).

**Challenge:** PageAnalysis must not depend on element ordering for page identity. Index is an observation artifact, not a semantic signal.

**Falsifier:** PageAnalysis produces same page identity evidence regardless of element ordering. Index is irrelevant to page identity.

**Evidence gap:** No reordered-element observation pairs in committed reality.

---

## 19. Minimum Purchase Candidate

### 19.1 The Minimum Contract

```
PageAnalysis capability:
  Input:   Fresh Observation
  Output:  SemanticEvidence[]
           where each SemanticEvidence = { Source, Claim, Stance, Reason? }
             Source  ∈ { FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE,
                         STRUCTURAL_COARSE, SWITCH_DISTRIBUTION }
             Claim   = "page is <semantic-page-name>"
             Stance  ∈ { Supports, Contradicts, Insufficient }
             Reason  = optional explanation
```

### 19.2 Why This Is Minimum

1. **One input** — Fresh Observation. Nothing else.
2. **One output type** — SemanticEvidence[] (existing contract, no new types).
3. **No new stances** — Supports/Contradicts/Insufficient are already purchased.
4. **No new belief states** — SUPPORTED/UNRESOLVED/CONTRADICTED are already purchased.
5. **Stateless** — Pure function, following the Reconcile pattern.
6. **Provider-neutral** — Fast sources today; slow sources later; same contract.

### 19.3 Why This Is Sufficient

The contract makes every falsifier detectable:

| Falsifier | How PageAnalysis Minimum Contract Detects It |
|---|---|
| **F1 Alias Collapse** | TEXT_ANCHOR supports "WifiSub" + TRANSITION contradicts → CONTRADICTED |
| **F2 Scroll** | TEXT_ANCHOR + STRUCTURAL_COARSE produce consistent evidence across viewport changes |
| **F3 Persistent Header** | TEXT_ANCHOR_NEGATIVE ("SIMs" present → NOT SettingsRoot) prevents parent-child collapse |
| **F4 State Mutation** | SWITCH_DISTRIBUTION tracks state separately from identity; TEXT_ANCHOR stays stable |
| **F5 Unknown** | All sources produce Insufficient → UNRESOLVED |
| **F6 Reordered** | Element Index not an input to any evidence source |

### 19.4 What Is Explicitly NOT Purchased

| Rejected | Reason |
|---|---|
| **PageAnalysis type** | Pure function capability sufficient; Reconcile pattern. |
| **PageAnalysisResult record** | SemanticEvidence[] is the output type that matters. |
| **Numeric confidence** | USEFUL_LATER — no falsifier purchases float scores. |
| **Fingerprint as identity** | CHANGE_SIGNAL + CACHE_HINT only. |
| **Full Element model dependency** | PageAnalysis consumes screen-level signals. |
| **Continuity logic** | Separate reconciliation step. |
| **Transition verification** | Traversal owns. |
| **VLM / Vector integration** | Provider-neutral; evidence arrives as SemanticEvidence. |
| **Page cache / prototypes** | P3 (Semantic Memory) — deferred. |
| **Context model** | Derivable; not an input to observation-scoped analysis. |
| **Revision without rebind** | Deferred capability. |
| **New evidence stances** | Supports/Contradicts/Insufficient sufficient. |
| **New belief states** | SUPPORTED/UNRESOLVED/CONTRADICTED sufficient. |
| **PROBABLE belief state** | Already rejected by Semantic Evidence contract. |

---

## 20. Deferred Capabilities

| Capability | Deferred To | Why |
|---|---|---|
| **PageAnalysis production type** | If SemanticEvidence[] proves insufficient for grouping competing hypotheses | Pure function + array sufficient now |
| **Numeric confidence** | P2 (Perception Routing) | Ranking, thresholds, fast/slow routing |
| **Evidence reliability scoring** | P2 | Source quality for routing decisions |
| **VLM page reasoning** | P2 | Provider-neutral contract already supports it |
| **Semantic vector similarity** | P2 | Perception provider concern (I-14) |
| **Page prototype / cache** | P3 (Semantic Memory) | Must follow Evidence → Belief → Memory |
| **Context-aware page analysis** | P1 | Goal/task context as additional evidence source |
| **Revision without rebind** | Future refinement | Agent→Container revision preserving local progress |
| **Scroll identity** | When scroll observation pairs exist in committed reality | REALITY_EVIDENCE_GAP |
| **OFF→ON state-change pairs** | When recorded reality includes state transitions | REALITY_EVIDENCE_GAP |
| **Cross-device page identity** | When multi-device reality exists | B1 WLAN/Wi‑Fi alias is known; multi-device observations not in committed reality |

---

## 21. PageAnalysis Semantic Knowledge Source

> This section is the final gate before PageAnalysis purchase.
> Question: **Where does page semantic knowledge come from?**

### 21.1 Fresh Observation Is Only World-State Input

**FreshObservationIsOnlyWorldStateInput: YES**

The Fresh Observation is the only *current world state* input to PageAnalysis. It provides raw perception facts (Elements, ForegroundApplication, SequenceNumber) — nothing else about the current device state is available or needed.

BUT: PageAnalysis also requires **semantic recognition capability** — the ability to turn observation signals into semantic claims. This capability is NOT world state; it is *knowledge* about what signals indicate what pages.

These are distinct:

| | World State Input | Semantic Knowledge |
|---|---|---|
| **What it is** | "What does the device show right now?" | "What do pages look like?" |
| **Changes** | Every observation | Slowly (new pages, new devices) |
| **Provided by** | Perception provider (Environment) | Caller / configuration |
| **Example** | `Elements[6].Text = "Wi‑Fi"` | "A page with 'T-Mobile' + 'Add network' + 'Wi‑Fi' entry without SwitchState is likely InternetPage" |

The PageAnalysis contract must make this distinction explicit. Any design that hides semantic knowledge inside an opaque lambda that also produces verdicts is the current broken model.

### 21.2 Current Semantic Knowledge Sources — Classified

Every caller-injected function in the Runtime, classified by what it provides:

| Injected Function | Holder | Classification | What It Contains |
|---|---|---|---|
| `resolveSemanticPage: Func<Observation, string?>` | Agent + Startup | **SEMANTIC KNOWLEDGE + VERDICT** (collapsed) | Knows text→page mapping AND produces page name verdict. Binary confidence. This is the root collapse. |
| `identityRule: Func<Observation, bool>` | Container | **SEMANTIC KNOWLEDGE + VERDICT** (collapsed) | Knows what observation patterns mean "my page" AND produces boolean verdict. Same oracle family as resolveSemanticPage. |
| `CategoryClassifier: Func<ObservedElement, TypeLevelElementCategory?>` | Goal | **SEMANTIC KNOWLEDGE + VERDICT** (collapsed) | Knows element→category mapping AND produces category verdict. |
| `EvidenceEvaluator: Func<Observation, GoalEvidence>` | Goal | **SEMANTIC VERDICT** | Judges goal satisfaction. Verdict, not knowledge. |
| `CandidateAuthorizationEvaluator` | Goal | **SEMANTIC VERDICT** | Judges candidate safety. Verdict, not knowledge. |
| `ViewportExplorationEvaluator` | Goal | **EXECUTION POLICY** | Decides whether to continue scrolling. |
| `BranchInventoryEvaluator` | Goal | **EXECUTION POLICY** | Decides branch inventory for open-world dispatch. |
| `TargetGroundingCriterion.CandidateEvaluator` | PlanStep | **SEMANTIC VERDICT** | Judges whether candidate matches target. |
| `TargetGroundingCriterion.PostActionEvaluator` | PlanStep | **SEMANTIC VERDICT** | Judges whether post-action observation confirms effect. |
| `stepExecutor` | Container | **EXECUTION POLICY** | Executes a step — mechanical, not semantic. |
| `parseRestoreRecipe` | Recovery | **SEMANTIC KNOWLEDGE** | Knows restore recipe string→actions mapping. |
| `resolveRecoveryAction` | Recovery | **SEMANTIC KNOWLEDGE + VERDICT** | Knows recovery strategy AND produces action verdict. |
| `verifyCriteria` | Recovery | **SEMANTIC VERDICT** | Judges recovery success. |
| `_observeInitial` | Agent | **RAW EVIDENCE** | Produces raw Observation from perception. NOT semantic. |

**Key finding:** `resolveSemanticPage` and `identityRule` are the primary page semantic injection points. Both collapse SEMANTIC KNOWLEDGE (what pages look like) and SEMANTIC VERDICT (what page this observation IS) into a single function. This is the Evidence=Claim=Belief=Truth collapse diagnosed by the Semantic Evidence Minimum Contract.

### 21.3 The Current Collapse — Why It Must Be Broken

```
resolveSemanticPage(observation)
        ↓
  "InternetPage"    ← This is simultaneously:
                      - knowledge application (pattern matching)
                      - claim production (page name)
                      - verdict (confidence=1.0)
                      - truth (no independent verification)
```

The new PageAnalysis must NOT merely rename this lambda. It must break the collapse:

```
OLD: Func<Observation, string?>           → returns VERDICT (page name)
NEW: Func<Observation, SemanticEvidence[]> → returns EVIDENCE (multi-source stances)
```

The caller still provides the semantic knowledge (how to recognize pages), but now:
- Knowledge is expressed as EVIDENCE-PRODUCING functions, not VERDICT-PRODUCING functions
- Multiple evidence sources can be encoded, each with Source attribution
- Sources can DISAGREE (one Supports, another Contradicts)
- The output is SemanticEvidence, not a page name string
- Uncertainty is explicit (Insufficient stance), not null

### 21.4 Semantic Source Options — Evaluated as Capability Shapes

**Option A: DETERMINISTIC SEMANTIC RULES**

```
Observation signals (text anchors, foreground app, SwitchState distribution, element count)
        ↓
SemanticClaim evidence (per-source, per-page stances)
```

- **Purchased now:** YES — the alias-collapse falsifier directly purchases this. TEXT_ANCHOR source vs TRANSITION source disagreeing = CONTRADICTED.
- **Risk:** Hand-written page catalog. But for the minimum purchase with known pages (A3 EP-04: 4 real pages), this is sufficient.
- **Verdict:** **MINIMUM_VIABLE** — the first executable path.

**Option B: SEMANTIC VECTOR MODEL**

```
Observation semantic representation
        ↓
similarity against page prototypes/concepts
        ↓
SemanticEvidence (similarity as supporting/contradicting evidence)
```

- **Purchased now:** NO — no current falsifier purchases vector similarity. The qualitative stances (Supports/Contradicts/Insufficient) are sufficient.
- **Risk:** Similarity != identity. High similarity can mean same page OR parent-child with shared text. The alias-collapse falsifier proves this.
- **Verdict:** **USEFUL_LATER** — as an additional evidence source with Source=VECTOR_SIMILARITY. Provider-neutral (I-14).

**Option C: VLM / HIGH INTELLIGENCE**

```
Observation → VLM reasoning → SemanticEvidence
```

- **Purchased now:** NO — no current falsifier requires VLM. The qualitative evidence contract supports it (Source=VISUAL_SEMANTIC), but no executable scenario purchases it.
- **Risk:** Model verdict becoming truth. VLM output is SemanticEvidence, not SemanticTruth (I-4, I-14).
- **Verdict:** **USEFUL_LATER** — same evidence contract, different Source label. Agent authority for invocation.

**Option D: VERIFIED SEMANTIC PROTOTYPES / CACHE**

```
Previously-verified page experience → comparison evidence
```

- **Purchased now:** NO — deferred to P3 (Semantic Memory). Must follow Evidence → Belief → Memory to avoid self-reinforcing wrong belief.
- **Risk:** Legacy `PageCacheManager` caused stale-cache error-retry loops (D-57/F-20). Cache of wrong classification is worse than no cache.
- **Verdict:** **DEFERRED** — P3. Prototypes are useful but must be evidence, not identity.

**Option E: CALLER-PROVIDED SEMANTIC EXPECTATION**

```
Caller/Compiler provides: what pages are relevant, what signals indicate them
        ↓
PageAnalysis applies recognition criteria to Observation
        ↓
SemanticEvidence (per-page, per-source stances)
```

- **Purchased now:** YES — partially. The caller ALREADY provides semantic knowledge (injected lambdas). The change is making it explicit and multi-source.
- **Key distinction:** Caller provides SEMANTIC KNOWLEDGE (recognition criteria, candidate page space). Caller does NOT provide SEMANTIC VERDICT ("this IS page X").
- **Verdict:** **REQUIRED_NOW** — this is the correct shape for the minimum purchase. Caller provides knowledge; PageAnalysis produces evidence.

### 21.5 Caller Contract — What May and Must Not Be Provided

**CallerMayProvide (semantic knowledge, not verdict):**

- Candidate page space: what pages might exist in this app context
- Recognition criteria per page: what signals (text anchors, structural patterns, SwitchState distribution) support/contradict each page
- Evidence source definitions: what channels to use (TEXT_ANCHOR, FOREGROUND, STRUCTURAL_COARSE, SWITCH_DISTRIBUTION)
- Task relevance: which pages are relevant to the current Goal (for prioritization, not for verdict)
- Semantic priors: previously-verified page prototypes (as evidence, not as identity)

**CallerMustNotProvideAsTruth:**

- ❌ "This observation IS page X" — that is a verdict, not knowledge
- ❌ A single page name string with binary confidence — that is the current broken model
- ❌ A function that returns one page name and treats all other pages as null — unfalsifiable
- ❌ A single evidence source that always Supports and never Contradicts or is Insufficient — structurally unfalsifiable

### 21.6 Fast / Slow Semantic Ownership

**FastSemanticSourceOwner: Caller-injected (invoked by Container or before Container reconciliation)**

Fast semantic sources are deterministic rules operating on Observation signals. They are stateless, synchronous, and cheap. The caller provides them as injected functions — same injection pattern as today, but with the evidence contract.

```
Observation
    ↓
Fast PageAnalysis (caller-injected recognition criteria)
    ↓
SemanticEvidence[] (FOREGROUND, TEXT_ANCHOR, STRUCTURAL_COARSE, SWITCH_DISTRIBUTION)
    ↓
Container.EvaluatePageBelief(observation, ...fastEvidence)
    ↓
Container._localPageBeliefState
```

Container does NOT own the recognition criteria — the caller injects them. Container owns only the fused belief state (I-2).

**SlowSemanticIntelligenceOwner: Agent**

Slow semantic intelligence (VLM, broader reasoning, cross-container context) operates under Agent authority. Agent has higher semantic intelligence and adjudication authority. Container does NOT own VLM invocation policy.

```
Container belief = UNRESOLVED or CONTRADICTED
    ↓
Container → Trap → Agent (EXISTING seam, I-8)
    ↓
Agent may invoke slow intelligence:
  - VLM page reasoning
  - Cross-container context
  - Task/goal context
    ↓
Additional SemanticEvidence (Source=VISUAL_SEMANTIC, TASK_CONTEXT, CROSS_CONTAINER)
    ↓
Agent adjudication:
  ADJUDICATE — determine correct page
  CORRECT     — CreateContainer(correctedPage) + Bind
  REBIND      — Bind new observation
    ↓
Container ← revised/rebuilt (still owns local state, I-2)
```

**Why Agent owns slow intelligence:**
- Agent has Goal context (what the task requires)
- Agent has cross-container history (where we've been)
- Agent has transition history (what actions were taken)
- Agent has higher semantic authority (I-3: one decision, one authority)
- VLM is provider-neutral (I-14) — Agent invokes it, Runtime does not couple to it
- Container is local state owner, not global intelligence owner

### 21.7 Minimum Implementation Shape

**MinimumImplementationShape: EXISTING_SEAM_WITH_EVIDENCE_CONTRACT**

The minimum PageAnalysis purchase requires NO new types, NO new boundaries, NO new components. It requires only a **contract change on the existing injection seam**:

```
BEFORE (current):
  Injected:  Func<Observation, string?>  resolveSemanticPage
  Returns:   page name string (verdict/truth) or null (unknown)
  Confidence: binary (non-null=1.0, null=0.0)
  Sources:   one (the lambda itself)
  Falsifiable: NO — same oracle as classifier + verifier

AFTER (minimum purchase):
  Injected:  Func<Observation, SemanticEvidence[]>  pageSemanticAnalyzer
  Returns:   SemanticEvidence[] (multi-source evidence stances)
  Confidence: qualitative (Supports/Contradicts/Insufficient per source)
  Sources:   multiple (each SemanticEvidence has its own Source label)
  Falsifiable: YES — sources can disagree, evidence is not verdict
```

**Why this is minimum:**

1. **No new types** — `SemanticEvidence[]` already exists. `SemanticEvidence` already has Source, Claim, Stance, Reason.
2. **No new boundaries** — same injection point (caller provides semantic knowledge as a function).
3. **No new owners** — Container still owns belief state (I-2). Agent still owns adjudication authority.
4. **No new components** — pure function, like `Reconcile.FromObservation`.
5. **Contract re-use** — PageAnalysis produces the SAME SemanticEvidence that `Container.EvaluatePageBelief` already accepts as `params SemanticEvidence[] additionalEvidence`.
6. **Wire-up is one line** — `container.EvaluatePageBelief(observation, pageSemanticAnalyzer(observation))`.

**What this is NOT:**

- ❌ NOT a new `PageAnalysis` class with instance state
- ❌ NOT a provider framework (`IPageSemanticProvider`, `PageAnalyzerEngine`, `ProviderRegistry`)
- ❌ NOT a VLM/vector/embedding integration
- ❌ NOT a page catalog database
- ❌ NOT a semantic ontology
- ❌ NOT a renamed `resolveSemanticPage` that still returns a single verdict

### 21.8 First PageAnalysis Executable Falsifier

**PageAnalysisExecutableFalsifier: Alias-Collapse with Observation-Derived Evidence**

**Setup:** RealitySeededSettingsFixture — InternetPage observation and WifiPage observation.

**Current (unfalsifiable):**
```csharp
string? Page(Observation o) => ...;  // single oracle
ContainerFactory(page) => new Container(page, o => Page(o) == page, ...);
// identityRule == Page() — same oracle as classifier = structurally unfalsifiable
```

**Required (falsifiable):**
```csharp
// Caller provides MULTI-SOURCE recognition criteria as SemanticEvidence producers:
SemanticEvidence[] AnalyzePage(Observation observation)
{
    var evidence = new List<SemanticEvidence>();

    // SOURCE 1: FOREGROUND — same app?
    evidence.Add(new SemanticEvidence("FOREGROUND", "page is within com.android.settings",
        observation.ForegroundApplication == "com.android.settings" ? Supports : Contradicts,
        $"foreground={observation.ForegroundApplication}"));

    // SOURCE 2: TEXT_ANCHOR — InternetPage signals
    bool hasInternetPageAnchors = observation.Elements.Any(e => e.Text == "T-Mobile")
        && observation.Elements.Any(e => e.Text == "Add network");
    evidence.Add(new SemanticEvidence("TEXT_ANCHOR", "page is InternetPage",
        hasInternetPageAnchors ? Supports : Insufficient,
        hasInternetPageAnchors ? "anchors 'T-Mobile' + 'Add network' present" : "no InternetPage anchors"));

    // SOURCE 3: TEXT_ANCHOR — WifiPage signals
    bool hasWifiPageAnchors = observation.Elements.Any(e =>
        e.Text == "Wi‑Fi" && e.SwitchState is not null)
        && observation.Elements.Any(e => e.Text == "Auto-connect");
    evidence.Add(new SemanticEvidence("TEXT_ANCHOR", "page is WifiPage",
        hasWifiPageAnchors ? Supports : Insufficient,
        hasWifiPageAnchors ? "SwitchState-bearing 'Wi‑Fi' + 'Auto-connect' present" : "no WifiPage anchors"));

    // SOURCE 4: STRUCTURAL_COARSE — element count range
    int count = observation.Elements.Length;
    evidence.Add(new SemanticEvidence("STRUCTURAL_COARSE", "page is InternetPage",
        count >= 10 && count <= 18 ? Supports : Insufficient,
        $"element count={count}"));

    return evidence.ToArray();
}

// Wire-up:
var evidence = AnalyzePage(observation);
var belief = container.EvaluatePageBelief(observation, evidence);
```

**Pass criterion:**
- InternetPage observation → TEXT_ANCHOR Supports "InternetPage", TEXT_ANCHOR Insufficient for "WifiPage" → FOREGROUND + TEXT_ANCHOR agree → SUPPORTED for InternetPage
- WifiPage observation → TEXT_ANCHOR Supports "WifiPage", TEXT_ANCHOR Insufficient for "InternetPage" → SUPPORTED for WifiPage
- BUT if both sets of anchors somehow match (alias-collapse) → one Supports, one Contradicts → CONTRADICTED → detected

**Fail criterion:**
- Caller simply supplies the expected page name as verdict
- Single source always Supports, never Contradicts or is Insufficient
- `SemanticEvidence` wrapper around the same old `resolveSemanticPage` string

**Pass is NOT:** "classifier returns correct page name."
**Pass IS:** "independent observation-derived signal channels can produce agreeing or conflicting SemanticEvidence about page identity."

### 21.9 Architecture Fit

**ArchitectureDelta: NONE**

The contract change fits entirely within existing boundaries:

| What Changes | What Does NOT Change |
|---|---|
| Contract of injected function: `string?` → `SemanticEvidence[]` | Injection point exists (caller provides semantic knowledge) |
| Output is evidence, not verdict | Container owns belief state (I-2) |
| Multiple sources can disagree | Agent owns adjudication authority |
| Uncertainty is explicit (Insufficient) | Trap + CreateContainer/Bind seam exists |
| Wires into `Container.EvaluatePageBelief` (already exists) | SemanticReconciliation.FuseBelief is unchanged |
| | SemanticEvidence contract is unchanged |

**No new:**
- Types, classes, interfaces
- Components, engines, providers
- Boundaries, owners, authorities
- Mutable state
- External service dependencies (VLM, vector, embedding)

---

## Summary

```
PAGE_ANALYSIS_SEMANTIC_CONTRACT_CHALLENGE_READY

ModelRouting:
  HaikuWork:
    - Current code mining: every page semantic entry point traced → all caller-injected
    - Legacy PageAnalysis search: no legacy type exists → greenfield, no migration
    - Reality evidence extraction: 6 pages (4 recorded + 2 synthetic), 8 extractable signal types
    - Candidate field minimization: 12 candidate inputs challenged, 1 retained (Fresh Observation)
    - Falsifier construction: F1 executable, F2-F6 defined with evidence gaps noted
  OpusDecisions:
    - PageAnalysis definition: observation-scoped, stateless, evidence-producing
    - PageAnalysis vs SemanticEvidence: PageAnalysis PRODUCES SemanticEvidence, doesn't replace it
    - PageAnalysis vs PageBelief: distinct — analysis is evidence, belief is fused conclusion
    - PageAnalysis != Continuity: semantic similarity != page continuity
    - PageAnalysis != Transition: identity evidence != effect verification
    - Element dependency: PARTIAL — screen-level signals sufficient; precise element grounding is separate lane
    - Fast/Slow boundary: same contract, different Source labels, no special truth status
    - Numeric confidence: USEFUL_LATER, not purchased by any current falsifier
    - Fingerprint: CHANGE_SIGNAL + CACHE_HINT, not AUTHORITATIVE_IDENTITY
    - Architecture Delta: NONE — pure function capability, existing evidence contract, existing adjudication seam
    - Minimum purchase: PageAnalysis capability (Observation → SemanticEvidence[])

CurrentPageAnalysisCapability:
  NONE — 100% of page semantic evidence is caller-injected lambdas.
  Container.EvaluatePageBelief exists but is production-unwired.
  No observation-scoped page evidence producer exists.

PageAnalysisDefinition:
  "An observation-scoped, stateless, evidence-producing semantic capability that,
   given a single fresh Observation, generates source-attributed SemanticEvidence
   hypotheses about page identity — it is evidence, not truth, and does not own
   mutable state, verify page continuity, or verify navigation transitions."

PageAnalysisConceptRequired: YES
  Purchased by alias-collapse falsifier. The missing piece in the Evidence→Belief pipeline.

PageAnalysisTypeRequiredNow: NO
  Pure function capability (Reconcile pattern) sufficient for minimum purchase.
  SemanticEvidence[] is the output type that matters; no new named type required.

MinimumInputs:
  Fresh Observation { Elements, ForegroundApplication, SequenceNumber }
  NOT: previous belief, Goal, last action, parent context, transition history

MinimumOutputs:
  SemanticEvidence[] where each = { Source, Claim, Stance, Reason? }
  Claim = "page is <semantic-page-name>"
  Source ∈ { FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE, STRUCTURAL_COARSE, SWITCH_DISTRIBUTION }

PageHypothesisSemantics:
  A page hypothesis IS a SemanticEvidence about a page identity claim.
  Hypothesis = Claim ("page is X") + evidence channels evaluate it with Stances.
  No new semantics beyond the existing SemanticEvidence contract.
  Competing hypotheses are expressed as multiple SemanticEvidence values with different claims.
  Unresolved = all sources Insufficient → no forced classification.

ElementDependency:
  PAGE_ANALYSIS_DEPENDS_ON_FULL_ELEMENT_MODEL: PARTIAL
  Screen-level signals (text multiset, foreground app, SwitchState distribution,
  coarse element count) are sufficient for minimum page identity evidence.
  Precise element grounding is a separate, parallel semantic lane.
  Some page distinctions may BENEFIT from element-level evidence but do not REQUIRE it.

FastPathRole:
  Produce SemanticEvidence from deterministic Observation signals:
  FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE, STRUCTURAL_COARSE, SWITCH_DISTRIBUTION.
  Stateless. No model inference. No external services.
  Same evidence contract as slow path. No special truth status.

SlowPathRole:
  Produce SemanticEvidence from higher-intelligence sources:
  VISUAL_SEMANTIC (VLM), TASK_CONTEXT, CROSS_CONTAINER.
  Same evidence contract. Provider-neutral. No direct VLM interface in Runtime.
  Evidence arrives as immutable SemanticEvidence values regardless of source.

NumericConfidence: USEFUL_LATER
  No current falsifier purchases numeric scoring.
  Qualitative stances (Supports/Contradicts/Insufficient) sufficient.
  Score != semantic correctness (91.9% subtitle phantom).
  Future uses: ranking, routing, ambiguity thresholds, candidate comparison.

FingerprintRole:
  ScreenshotFingerprint → CACHE_HINT + CHANGE_SIGNAL
  ElementFingerprint   → CHANGE_SIGNAL
  TextFingerprint      → SUPPORTING_EVIDENCE
  SemanticVectorEmbedding → SUPPORTING_EVIDENCE (perception provider, I-14)
  None → AUTHORITATIVE_IDENTITY
  Fingerprint is evidence, not identity (I-6).

ContinuityBoundary:
  PageAnalysis produces identity evidence for a single observation.
  Continuity = f(Previous PageBelief + Current PageAnalysis + Last Action + Transition Evidence).
  Semantic similarity != page continuity.
  PageAnalysis provides INPUT to continuity decisions; does not MAKE them.

TransitionBoundary:
  PageAnalysis produces page identity evidence.
  Transition verification = Traversal.Verify(postActionObservation).
  PageAnalysis provides INPUT to transition verification.
  PageAnalysis != Transition verifier.

ContainerOwnership:
  Container is SOLE OWNER (I-2) of local page belief state.
  Container._localPageBeliefState ← fusion of PageAnalysis evidence + LOCAL_IDENTITY.
  No second owner. No duplicate state in Agent.

AgentAuthority:
  Agent has HIGHER SEMANTIC ADJUDICATION AUTHORITY (≠ state ownership).
  Can ADJUDICATE, CORRECT, REBIND, INVALIDATE using broader context.
  EXISTING SEAM sufficient: Container→Agent Trap + Agent→Container CreateContainer/Bind.
  Agent does not own Container local state.

ArchitectureDelta: NONE
  PageAnalysis fits as a pure function capability producing SemanticEvidence.
  No new boundaries. No new owners. No new components.
  Existing evidence contract (SemanticEvidence). Existing fusion (SemanticReconciliation).
  Existing belief owner (Container). Existing adjudication seam (Trap + CreateContainer/Bind).
  Follows Reconcile pattern (stateless pure function).

FirstExecutableFalsifier:
  F1 Alias Collapse — ALREADY EXECUTABLE ✓ (AliasCollapseSourceIndependenceTests, 12/12 pass)
  Next: F2 Scroll — PageAnalysis produces consistent page identity across viewport changes.
  Pass: PageAnalysis(Obs_top) and PageAnalysis(Obs_bottom) both SUPPORT same page.

MinimumNextPurchase:
  PageAnalysis capability as a stateless pure function:
    Input:  Fresh Observation
    Output: SemanticEvidence[] about page identity claims
    Using:  FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE, STRUCTURAL_COARSE,
            SWITCH_DISTRIBUTION evidence sources
    Wire:   Container.EvaluatePageBelief(observation, ...PageAnalysis(observation))
  NO new types. NO new boundaries. NO new owners.

PAGE_ANALYSIS_SEMANTIC_SOURCE_CHALLENGE_RESULT

FreshObservationIsOnlyWorldStateInput: YES
  Fresh Observation is the only current world state input.
  PageAnalysis also requires semantic recognition capability (knowledge),
  which is distinct from world state and is caller-provided.

SemanticKnowledgeCurrentSource:
  CALLER-INJECTED LAMBDAS (collapsed knowledge + verdict).
  resolveSemanticPage: Func<Observation, string?> — knows text→page mapping
    AND produces page name verdict in a single opaque function.
  identityRule: Func<Observation, bool> — knows observation→"my page"
    mapping AND produces boolean verdict.
  CategoryClassifier: Func<ObservedElement, TypeLevelElementCategory?>
    — knows element→category mapping AND produces category verdict.
  These three are the primary collapsed injection points.
  All other injected functions are verdicts, execution policies, or raw evidence.

SemanticVerdictStillCallerInjected: YES
  The current Runtime has zero internal semantic knowledge.
  100% of page classification, element classification, and identity
  verification is caller-injected AND produces verdicts directly.
  PageAnalysis must break this: caller still provides knowledge,
  but output is evidence, not verdict.

PageAnalysisSemanticSourceDefinition:
  "Caller-provided semantic recognition criteria that, when applied to
   a Fresh Observation, produce multi-source, falsifiable SemanticEvidence
   about page identity — the caller provides knowledge (what signals
   indicate what pages), not verdict (what page this observation IS)."

FastSemanticSourceOwner:
  Caller-injected (invoked by Container or before Container reconciliation).
  Fast sources are deterministic rules on Observation signals.
  Stateless, synchronous, cheap. Same injection pattern as today,
  but contract returns SemanticEvidence[] instead of string?.
  Container does NOT own the recognition criteria — it owns only the
  fused belief state (I-2).

SlowSemanticIntelligenceOwner:
  Agent. Slow intelligence (VLM, broader reasoning, cross-container context)
  operates under Agent authority. Agent has Goal context, transition history,
  cross-container context, and higher semantic adjudication authority (I-3).
  Container does NOT own VLM invocation policy.
  Slow evidence arrives as SemanticEvidence — same contract, different Source label.

CallerMayProvide:
  - Candidate page space (what pages might exist)
  - Recognition criteria per page (signals → evidence stances)
  - Evidence source definitions (which channels to use)
  - Task relevance (which pages matter for current Goal)
  - Semantic priors / prototypes (as evidence, not identity)
  - Multiple independent recognition criteria that CAN disagree

CallerMustNotProvideAsTruth:
  - "This observation IS page X" (verdict, not knowledge)
  - A single page name string with binary confidence
  - A single evidence source that always Supports, never Contradicts/Insufficient
  - SemanticEvidence wrapper around the same old resolveSemanticPage string

PageAnalysisExecutableFalsifier:
  Alias-Collapse with Observation-Derived Multi-Source Evidence.
  Given RealitySeededSettingsFixture InternetPage + WifiPage observations:
    FOREGROUND source: checks ForegroundApplication match
    TEXT_ANCHOR source: checks page-specific text anchors (T-Mobile, Auto-connect, etc.)
    STRUCTURAL_COARSE source: checks element count ranges
  Each source independently produces Supports/Contradicts/Insufficient.
  Sources can disagree → CONTRADICTED → alias-collapse detected.
  Pass: independent observation-derived signal channels produce agreeing
    or conflicting SemanticEvidence about page identity.
  Fail: caller supplies expected page name as verdict; single source always Supports.

MinimumImplementationShape:
  EXISTING_SEAM_WITH_EVIDENCE_CONTRACT
  Contract change on the existing injection seam:
    BEFORE: Func<Observation, string?>  → returns VERDICT (page name or null)
    AFTER:  Func<Observation, SemanticEvidence[]> → returns EVIDENCE (multi-source stances)
  NO new types. NO new boundaries. NO new components.
  Same injection point. Same SemanticEvidence contract.
  Wires directly into Container.EvaluatePageBelief (already exists).

ReadyForPageAnalysisPurchase: YES
  The semantic source challenge is resolved.
  Minimum purchase: contract change on existing injection seam
  from "return verdict" to "return evidence."
  ArchitectureDelta: NONE.

No production changes.
No Runtime changes.
No OpenSpec.

STOP.
```
