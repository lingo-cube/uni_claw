# Semantic Evidence Minimum Contract Challenge (Finalized)

> Generated: 2026-08-10
> Finalized: 2026-08-10 — FINALIZE_SEMANTIC_EVIDENCE_MINIMUM_CONTRACT
> Role: Runtime Architecture Analyst
> Baseline: `docs/decisions/semantic-architecture-gap-review.md` (Revised 2026-08-10)
> Inputs: RM-01..RM-11 (accepted Reality Model corpus) · Reality Model Admission Contract (frozen v1.0) · CP-12 Challenge Result (5/5 GAP) · A3/A4/B1 recorded reality · Runtime source (`src/UniClaw.Runtime/`) · 14 Architecture Invariants (I-1..I-14) · Agent→Container adjudication seam investigation
> Scope: Analysis only — no production implementation, no OpenSpec, no Runtime modification, no capability purchase

---

## 1. Question

**What must exist between Raw Observation and Semantic Belief so that Runtime semantic judgments are:**

- **evidence-backed** — every semantic conclusion traces to observable records, not to a single injected oracle
- **falsifiable** — a semantic hypothesis can be contradicted by evidence from a different source
- **revisable** — a belief can change when fresh evidence contradicts a prior conclusion
- **source-aware** — the Runtime knows which evidence channel produced a claim and can detect disagreement
- **usable by Element/Page semantics** — both Element Evidence and Page Evidence are first-class projections of the same Observation
- **independent of a single caller oracle** — no single injected lambda acts as both classifier and verifier

**This is NOT a complete semantic framework.** The objective is to discover the MINIMUM common Semantic Evidence contract purchased by current executable reality — the smallest representation that makes the alias-collapse falsifier (Section 3) impossible.

The Reality Model Admission Contract already establishes the layering this challenge operates within: World Fact (WF) / Observation Record (OB) / Reality Inference (RI) / Expected Requirement (ER). The question is what subset of that layering the Runtime must internalize.

---

## 2. Existing Runtime Truth

### 2.1 The Physical Layer (what exists)

| Type | File | Fields | Role |
|---|---|---|---|
| `Observation` | `Model/Observation/Observation.cs:14` | `Elements` (ImmutableArray<ObservedElement>), `ForegroundApplication` (string?), `SequenceNumber` (long) | Immutable snapshot — "evidence, not semantic truth" (I-4) |
| `ObservedElement` | `Model/Observation/ObservedElement.cs:11` | `Text` (string), `SwitchState` (bool?), `Index` (int) | Physical element fact — no coordinates, no type label, no hierarchy, no confidence |
| `WorldBelief` | `Model/WorldBelief.cs:13` | `SemanticPage` (string?), `Confidence` (float), `Evidence` (string?), `SourceObservationSequence` (long?) | Run-level belief — held solely by Agent (B7). Confidence is binary (0.0/1.0) |
| `Reconcile` | `World/Reconcile.cs:10` | static method `FromObservation(Observation, Func<Observation,string?>)` | **Stateless pure function** — no decision authority, no aggregation, no fusion, no state |
| `Container` | `Container/Container.cs:18` | `_semanticPageName` (readonly), `_identityRule` (readonly), `_observation`, `_executedSteps`, `_viewportExplorationObservations`, `_isLocalComplete` | Page-local state sole owner (I-2). Semantic identity is **immutable post-construction** |
| `TargetGroundingEvidence` | `Model/TargetGroundingEvidence.cs:7` | `Supported` (bool?), `Reason` (string) | **Qualitative tri-state evidence value** — confirmed/rejected/insufficient |
| `CandidateAuthorizationEvidence` | `Model/CandidateAuthorizationEvidence.cs` | `Authorized` (bool?), `Reason` (string) | **Qualitative tri-state evidence value** — authorized/rejected/unresolved |
| `TypeLevelDispatchPolicy` | `Model/TypeLevelDispatchPolicy.cs` | `CategoryHandling` (Category→Handling map) | Category→Handling only; 2 categories (NavigableContainer, StateChangingControl) |

### 2.2 The Missing Semantic Layer (what does NOT exist)

| Gap | Evidence |
|---|---|
| **No SemanticElement type** | `ObservedElement` is a physical tuple (Text + SwitchState + Index). There is no type that represents "an element with a semantic identity." |
| **No SemanticPage type** | "SemanticPage" is a `string?` field on `WorldBelief`, not a type. Page state is split: `WorldBelief.SemanticPage` (name, held by Agent) + `Container._semanticPageName` (name, immutable, held by Container). |
| **No ContainerSemanticsEngine** | Container semantics come from injected delegates (`_identityRule`, `_resolveSemanticPage`). No component owns semantic interpretation. |
| **No evidence aggregation** | `Reconcile.FromObservation` calls one injected lambda → directly produces `WorldBelief`. Confidence is binary: 1.0 (resolver returned non-null) or 0.0 (null). No multi-source fusion. |
| **No source attribution** | The resolver `Func<Observation, string?>` has no source channel. The Runtime cannot tell whether a page-name came from text matching, structural analysis, or a VLM. |
| **No disagreement detection** | Because there is only one source (the injected lambda), two sources cannot disagree. The system is structurally incapable of detecting contradiction. |
| **No Container local dynamic belief** | `Container._semanticPageName` is `readonly` — immutable post-construction. Container has no dynamic local semantic belief that can be revised by reconciliation. |
| **No Agent→Container revision path** | Agent always creates a NEW Container when semantic identity diverges (every correction path is `CreateContainer(newPage)` + `Bind`). There is no "send revision to existing Container" API. |

### 2.3 The Existing Qualitative Evidence Pattern

The Runtime already has a **family of immutable qualitative evidence value types**:

| Evidence Type | Tri-state Field | String Field | Produced By | Consumed By |
|---|---|---|---|---|
| `TargetGroundingEvidence` | `bool? Supported` | `Reason` | `TargetGroundingCriterion.CandidateEvaluator` / `PostActionEvaluator` (caller-injected) | `Traversal` (Select + Verify) |
| `CandidateAuthorizationEvidence` | `bool? Authorized` | `Reason` | `Goal.CandidateAuthorizationEvaluator` (caller-injected) | `Agent` (pre-dispatch gate) |
| `GoalEvidence` | `bool Satisfied` | `Reason` | `Goal.EvidenceEvaluator` (caller-injected) | `Agent` (completion authority, I-10) |
| `BranchProgressEvidence` | (composite) | (composite) | `Goal.BranchInventoryEvaluator` (caller-injected) | `Agent` (branch progress) |
| `ViewportExplorationEvidence` | (tri-state) | (composite) | `Goal.ViewportExplorationEvaluator` (caller-injected) | `Agent` (viewport) |

**Pattern:** `bool?` tri-state (confirmed / rejected / insufficient) + `string` reason. NOT numeric confidence. Produced by caller-injected evaluators, consumed by the single authority that owns the corresponding decision (I-3).

**This is the precedent.** The minimum Semantic Evidence contract should generalize this existing pattern, not invent a new one.

### 2.4 The Agent→Container Correction Seam (Current)

**Investigation findings (repository evidence):**

| Question | Answer |
|---|---|
| Is `Container._semanticPageName` mutable? | **NO** — `readonly`, set at construction (line 44/61), never updated post-construction |
| Is `Container._identityRule` mutable? | **NO** — `readonly`, set at construction, never replaced |
| Does Container have a semantic-revision API? | **NO** — only `Bind` (full reset) + observation-update methods (`TryVerifyLocalContinuity`, `TryAcceptLocalObstruction`) |
| How does Agent correct Container semantic identity? | **Creates a NEW Container** via `CreateContainer(newPage)` + `Bind(obs)` — every correction path (drift, continuity failure, navigation) |
| Does Agent hold duplicate Container semantic state? | **Overlapping but different**: Agent holds `_belief: WorldBelief?` (volatile, reconciled every observation); Container holds `_semanticPageName` (immutable, construction-time). They're compared for equality but never written one into the other. |
| Does Container store a reconciled belief from Agent? | **NO** — `Reconcile.FromObservation` result is held ONLY by Agent. Container receives `reconciledSemanticPage` as a method argument for comparison only, never stores it. |
| Can Agent override Container's identity rule? | **NO** — must create a new Container with a different injected `identityRule` |

**The current seam is one-directional and create-new-only:**
```
Container → Agent: via Trap (escalation up, I-8)
Agent → Container: via CreateContainer + Bind (discard + rebuild, NOT in-place revision)
```

There is **no write path that pushes a reconciled belief into an existing Container**. When Agent's reconciled page diverges from Container's fixed identity, Agent discards and rebuilds.

---

## 3. Reality Falsifiers

### 3.1 The Alias-Collapse Falsifier (Primary)

**Source:** `tests/UniClaw.Runtime.Tests/Scenario/OpenWorldTypeDirectedScenarioTests.cs:152-160`

The injected `Page()` resolver:

```csharp
static string? Page(Observation o) =>
    o.Elements.Any(e => e.Text == "Network&internet") && ... ? "Root"
    : o.Elements.Any(e => e.Text == "InternetPage")
        && o.Elements.Any(e => e.Text == "Wi‑Fi" && e.SwitchState is not null) ? "WifiSub"
    : ...;
```

The `Factory` uses the **same `Page()` function** as both classifier and verifier:

```csharp
RuntimeContainer Factory(string page) =>
    new(page, o => Page(o) == page, traversal.ExecuteStep, ...);
//                     ^^^^^^^^^^^^^^^^
//                     identityRule (verifier) == same oracle as classifier
```

**What collapses:** `WifiOff` screen (has "InternetPage" + "Wi‑Fi" with `SwitchState=false`) and `WifiOn` screen (has "InternetPage" + "Wi‑Fi" with `SwitchState=true`) both resolve to `"WifiSub"` because `e.SwitchState is not null` is true for both. The Container for "WifiSub" cannot detect it is looking at the wrong page — `IsStillMine` uses the same `Page()` function that classified it.

**Why it is unfalsifiable:** The identity rule and the verify rule are the same function. There is no independent evidence channel that can contradict the classifier. The system is **structurally self-referential** — it cannot be wrong by construction, which means it cannot be right by evidence.

**Recorded reality confirms:** `RealitySeededSettingsFixture` (Page 3 "InternetPage" has Wi-Fi entry + AndroidWifi + empty-text toggle; Page 4 "WifiPage" has WiFi switch) — two distinct real pages that would collapse under this heuristic.

### 3.2 The Subtitle Phantom Falsifier

**Source:** A3 EP-04 sim-replay (`trace-replay-export.json`) — SettingsRoot page element [9]: `"Bluetooth, pairing"` typed `menuitem`. RM-09 WF-28: the chevron heuristic in `fusion.py:292-343` fabricates phantom `menu_item` elements from subtitle text.

**What it proves:** Perception can detect something that is not an interactive element. `ObservedElement` has no field to distinguish "perception detected an element" from "an interactive element exists."

### 3.3 The Wi-Fi vs AndroidWifi Falsifier

**Source:** A3 EP-04 Internet page — element [6] `"Wi-Fi"` (type `menuitem`, the entry) and element [8] `"AndroidWifi"` (type `menuitem`, connected SSID). Both contain "Wi-Fi" text. CP-12 Case 2: GAP.

**What it proves:** Text matching cannot establish semantic identity. Two elements with overlapping text are different semantic objects. `ObservedElement.Text` alone is insufficient — there is no field for "which specific semantic element this is."

### 3.4 The Empty-Text Candidate Falsifier

**Source:** A3 EP-04 SettingsRoot — 5 of 16 elements have `text: ""`. CP-12 Case 5, RealitySeededWifiScenarioTests `VariantD_NoisyCandidates`.

**What it proves:** Elements with no text exist and are interactable (toggles, icons). Text-based grounding fails. There is no field for "this element exists and is interactable despite having no text."

### 3.5 The Parent-Return 1:1 Assumption Falsifier

**Source:** `Agent.cs:601-640` — parent-return logic:

```csharp
var returnCandidates = current.Elements
    .Where(element => string.Equals(element.Text, parent.SemanticPageName,
                                   StringComparison.Ordinal))
    .ToArray();
// requires exactly ONE candidate (line 605)
// requires reconciled page == parent.SemanticPageName (line 621)
```

**What it proves:** The Runtime assumes element text == semantic page name (1:1 screen↔page↔text). Real Android Settings does not have this property — internal page names ("SettingsRoot", "WifiSub") do not appear as visible UI text. Synthetic fixtures work around this by embedding matching text; reality-seeded fixtures with real EP-04 data cannot pass.

### 3.6 The WLAN/Wi-Fi Cross-Device Alias Falsifier

**Source:** B1 real-device golden (PKJ110, Chinese ROM) — element `WLAN` with aliases `["Wi-Fi","WiFi","无线局域网"]`. A3/A4 emulator uses literal `"Wi-Fi"`.

**What it proves:** The same semantic element has different text across devices. Text identity is not stable across the world. There is no field for "this element's semantic identity is X, regardless of text variant."

---

## 4. Element Evidence Findings

### 4.1 Challenge: raw perception candidate vs semantic element hypothesis

An `ObservedElement` (Text + SwitchState + Index) is a raw perception candidate. A semantic element hypothesis is "this specific element is the Wi-Fi entry" or "this is a subtitle, not interactive." The evidence needed to move from candidate to hypothesis:

### 4.2 Evidence Dimension Assessment

**Identity ≠ Category.** These are distinct semantic dimensions. Two elements can share an identity label but have different categories (a "Wi-Fi" NavigableContainer entry vs a "Wi-Fi" StateChangingControl switch). Knowing identity does NOT imply category.

| Evidence Dimension | Required? | Purchased By |
|---|---|---|
| **ElementExistence** | **REQUIRED_NOW** | RM-09 WF-28 (chevron heuristic fabricates phantom elements), VE-05 (subtitle "Bluetooth, pairing" classified menu_item — 91.9% rate), VE-03 (empty OCR → navigable empty target), VE-06 (search box misclassified). The system must distinguish "perception detected something" from "an interactive element exists." |
| **ElementIdentity** | **REQUIRED_NOW** | VRD-01 (OCR Text Output ≠ Semantic Element Identity), RI-16 (text identity ≠ substring containment), VE-07 (substring overmatch "notifications" ⊆ "Flash notifications"), Wi-Fi entry vs AndroidWifi vs "Wi-Fi doesn't turn back on automatically" description text. The system must establish "which specific semantic element this is" beyond text matching. |
| **ElementCategory** | **REQUIRED_NOW** | RM-09 WF-26 (type labels sometimes semantically wrong), RI-13 (vision pipeline type labels are perception outputs, not world facts). Category determines dispatch (`TypeLevelDispatchPolicy` maps Category→Handling). Distinct from identity — "Wi-Fi" NavigableContainer (entry) and "Wi-Fi" StateChangingControl (switch) have same identity text but different categories. |
| **ElementInteractionCapability** | **REQUIRED_NOW** | VRD-02 (Element Classification Output ≠ Interaction Capability), RI-18 (visibility is necessary but not sufficient for navigability). "Can I tap/toggle/read this?" is distinct from both identity and category. A subtitle and a menu entry can have the same category label but different capabilities. |
| **ElementState** | **PARTIALLY_PURCHASED** | `SwitchState` (bool?) is the only state dimension currently observed. Desired state change (OFF→ON) is action semantics (P1, deferred). State remains open-ended — future states (selected, enabled, expanded, checked, connected, loading, focused) are INSUFFICIENT_EVIDENCE (not observed in committed reality). Only `SwitchState` is purchased now. |

### 4.3 Element Semantic Dimensions (distinct, not implementation types)

These are **semantic dimensions**, NOT five implementation types. The minimum contract expresses all five as `SemanticClaim` + `SemanticEvidence` values — the contract does not require separate types per dimension.

| Dimension | Example: Wi-Fi entry | Example: Wi-Fi switch | Example: AndroidWifi |
|---|---|---|---|
| **Identity** | Wi-Fi settings entry | Wi-Fi master control | network instance |
| **Category** | NavigableContainer | StateChangingControl | NetworkItem |
| **Capability** | Navigate | SetDesiredState | Inspect / Connect |
| **Existence** | real interactive element | real interactive element | real informational text |
| **State** | (none) | ON / OFF | connected / disconnected |

**Identity does NOT imply category.** The Wi-Fi entry (NavigableContainer) and the Wi-Fi switch (StateChangingControl) both have identity "Wi-Fi" but different categories. Category must be independently evidenced.

**Category does NOT imply capability.** Two NavigableContainer elements may have different capabilities (one navigates to a sub-page, another opens a popup). Capability must be independently evidenced.

**State is open-ended.** `SwitchState` is one observed dimension. Future state dimensions remain evidence-driven — the contract must not hardcode a fixed state enumeration.

---

## 5. Page Evidence Findings

### 5.1 Challenge: "This observation semantically represents page P"

The Runtime currently delegates 100% of page identity to `resolveSemanticPage: Func<Observation, string?>` — a single injected lambda. The alias-collapse falsifier (Section 3.1) proves this is unfalsifiable when the same oracle acts as both classifier and verifier.

### 5.2 Evidence Dimension Assessment

| Evidence Dimension | Required? | Purchased By |
|---|---|---|
| **PageIdentity** | **REQUIRED_NOW** | RD-08 (RawPageEvidence ≠ SemanticPageIdentity), RM-01 RI-01 (page identity inferable from element inventory but not equivalent to it). The system must establish "which semantic page is this" from multiple sources, not from a single text-matching heuristic. |
| **PageTransition** | **REQUIRED_NOW** | RM-05 WF-15 (navigation changes observable element inventory), RI-08 (page-change verification requires semantic comparison). The alias-collapse falsifier directly proves this: InternetPage and WifiPage collapse because there is no independent transition evidence — the same `Page()` oracle classifies both. Transition evidence is an independent source that can CONTRADICT text-anchor evidence. |
| **PageContinuity** | **REQUIRED_NOW** | RM-05 (navigation must verify observable page change), scroll/dynamic content must not break identity (RM-01 WF-04: two screens may share text but differ in inventory). Continuity is "identity persists across observations" — supported by identity + transition evidence. |
| **PageContext** | **DEFER** | Parent/sibling/child position is derivable from transition history + Container hierarchy. No current falsifier purchases Context as an independent evidence dimension. Context derives from: foreground application, current observation, previous Container belief, parent Container, last verified action, transition history, task scope, Goal context. Agent may use broader context than Container. |

### 5.3 Candidate Evidence Sources (NOT selected — enumerated only)

| Source | Evidence Type | Available? |
|---|---|---|
| foreground application / activity | `Observation.ForegroundApplication` | ✅ already in Observation |
| semantic text anchors | `ObservedElement.Text` | ✅ already in ObservedElement |
| text embedding similarity | vector match | ❌ not in Runtime (perception provider) |
| structural evidence | element count, inventory shape, depth | ❌ not extracted |
| coarse element roles | element type / capability | ❌ not in ObservedElement (no type field) |
| previous page belief | `WorldBelief.SemanticPage` + `SourceObservationSequence` | ✅ Agent holds prior belief (B7) |
| last verified action | `TraversalJournalEntry` (DispatchedAction + PostActionObservation) | ✅ Traversal journal |
| visual / VLM evidence | external semantic reasoning | ❌ not coupled (correctly deferred) |

**No algorithm or model is selected.** The point is that multiple sources already exist in the Runtime (foreground app, text anchors, previous belief, action journal) but are not fused — only the single `resolveSemanticPage` lambda is consulted.

### 5.4 Minimum Page Evidence Purchase

Three evidence dimensions (REQUIRED_NOW):

1. **Page identity** — "Which semantic page is this?" From multiple sources (text anchors, foreground app, structural shape), not from a single oracle.
2. **Page transition** — "Did navigation produce a page change, and is this the expected destination?" From action journal + post-action observation comparison.
3. **Page continuity** — "Is this still the same page as the previous observation?" Identity-persistence across observations.

**Context** is DEFER — derivable from transition history + Container hierarchy.

---

## 6. Shared Evidence Requirements

### 6.1 Do Element and Page Evidence require a shared representation?

**Yes.** Both lanes share the same structural need:

- Both must be **source-attributed** (the alias-collapse falsifier purchases this for pages; the subtitle phantom purchases it for elements — both fail because a single source is trusted without independent corroboration).
- Both must be **falsifiable** (a claim from source A can be contradicted by source B).
- Both must be **qualitative** (support / contradict / insufficient), not numeric — the existing evidence pattern (`TargetGroundingEvidence`, `CandidateAuthorizationEvidence`) establishes this.
- Both are **projections of the same Observation** — they share the same freshness (SequenceNumber) and the same raw evidence base.

### 6.2 Candidate Contract Shape (Hypothesis)

```
SemanticClaim
{
    Assertion   // "this is the Wi-Fi entry" / "this is Internet page"
}

SemanticEvidence
{
    Source      // which evidence channel produced this stance
    Stance      // SUPPORTS / CONTRADICTS / INSUFFICIENT
    Reason      // optional — why (following existing Reason pattern)
}
```

Evidence evaluates a claim: **"Source S has Stance T about Claim C."** Multiple pieces of evidence (from different sources) can exist for the same claim — one source SUPPORTS while another CONTRADICTS.

### 6.3 Challenging Each Property

| Property | Required? | Purchased By | Verdict |
|---|---|---|---|
| **Assertion (Claim)** | **REQUIRED** | The reality contract Section 14 separates raw perception output (OB) from interpreted claims (RI). OCR "Wi-Fi" is evidence; "this is the Wi-Fi navigation element" is a claim. The claim is the NEW layer between observation and belief. | **REQUIRED** |
| **Source** | **REQUIRED** | RM-01 ER-04 (page identity evidence must be source-attributed). The alias-collapse falsifier directly purchases this: the same oracle as both classifier and verifier = unfalsifiable. Source independence REQUIRES knowing which channel produced a stance. | **REQUIRED** |
| **Stance** | **REQUIRED** | The falsifiers need SUPPORTS (alias-collapse: transition source supports different page), CONTRADICTS (subtitle: structural source contradicts text-anchor), INSUFFICIENT (Wi-Fi vs AndroidWifi: no source can distinguish). | **REQUIRED** |
| **Reason** | REQUIRED *(optional)* | Following the existing `TargetGroundingEvidence` pattern (`bool? Supported` + `string Reason`). Reason is optional — the stance is the essential field; reason is supporting text. | **Optional** (following existing pattern) |
| **Subject** | REQUIRED *(implicit)* | Evidence must be about something. BUT subject is implicit — the evidence is evaluated against a claim that is attached TO an element/page. The attachment point IS the subject. | **Implicit** (attachment point) |
| **Support (evidence payload)** | REQUIRED *(implicit)* | The Observation the evidence was derived from — already available in context (Agent holds `SourceObservationSequence`, Traversal journal holds `PostActionObservation`). An explicit field duplicates available context. | **Implicit** (Observation in context) |
| **Provenance** | **NOT_YET_REQUIRED** | Full provenance (file/commit/run ID, E0-E4 grade) is a Reality Model Admission Contract concern (Sections 5, 17), not a Runtime concern. | **DEFERRED** |
| **Freshness** | REQUIRED *(already purchased)* | `Observation.SequenceNumber` already provides temporal ordering (Decision 6 — deterministic monotonic sequence, no real clock). | **Already exists** |

### 6.4 Rejected Properties

| Property | Verdict | Reason |
|---|---|---|
| **numeric confidence** | **REJECTED** | No current falsifier purchases a float score. Falsifiers need agreement/disagreement (qualitative), not fine-grained scoring. The existing pattern is `bool?` tri-state. See Section 9. |
| **timestamps** | **REJECTED** | Runtime uses `SequenceNumber` (Decision 6 — no real clock). Timestamps are provenance, not evidence. |
| **semantic IDs** | **REJECTED** | Circular — the contract's purpose is to ESTABLISH semantic identity, not assume it. A "semantic element ID" field would embed the answer (violates contract Section 12: no answers embedded in reality). |
| **embeddings stored in Runtime** | **REJECTED** | Runtime does not own embeddings (I-14: AI is pluggable capability, not Runtime's only path). Embeddings belong to the perception provider. |
| **generic metadata bags** | **REJECTED** | Violates I-12 (YAGNI — no complexity without requirement) and I-13 (no God Context re-aggregation). |
| **inheritance hierarchies** | **REJECTED** | Over-abstraction. The existing evidence types are flat `sealed record` values. No falsifier purchases polymorphic evidence. |
| **full provenance chains** | **REJECTED** | Reality-model-admission concern (E0-E4 grading, file/commit/run ID). Runtime needs Source + Stance, not artifact traceability. |

### 6.5 Minimum Shared Contract

After challenging every property, the minimum is:

```
SemanticClaim { Assertion }

SemanticEvidence { Source, Stance, Reason? }
  where Stance = SUPPORTS / CONTRADICTS / INSUFFICIENT
```

- **Subject** is implicit (the element/page the claim is evaluated against).
- **Support** is the `Reason` string (optional, following existing pattern) or the Observation in context.
- **Freshness** is `Observation.SequenceNumber` (already exists).
- **Provenance** is deferred (reality-model-admission concern).
- **Numeric confidence** is rejected (USEFUL_LATER, not purchased).

**This separates Evidence (source stance) from Claim (hypothesis) from Belief (fusion result).** Evidence ≠ Claim. Claim ≠ Belief. Belief ≠ Truth.

---

## 7. Claim / Evidence / Belief Separation

### 7.1 The Four Levels (must not collapse)

```
Evidence     ≠ Claim
Claim        ≠ Belief
Belief       ≠ Truth
```

| Level | Definition | Example | Runtime Representation |
|---|---|---|---|
| **Evidence** (OB) | Raw observable record — no interpretation | OCR text "Wi-Fi" at element index 6; SwitchState=false; foreground="com.android.settings" | `ObservedElement.Text`, `ObservedElement.SwitchState`, `Observation.ForegroundApplication` |
| **Claim** (SemanticClaim) | Semantic hypothesis — what a source might assert | "This element is the Wi-Fi navigation entry"; "This screen is Internet page" | **MISSING** — this is the SemanticClaim layer |
| **Evidence Stance** (SemanticEvidence) | A source's evaluation of a claim | Source=text-anchor, Stance=SUPPORTS, Reason="text matches 'Wi-Fi'"; Source=structural, Stance=CONTRADICTS, Reason="chevron artifact, not interactive" | **MISSING** — this is the SemanticEvidence layer |
| **Belief** | Fused conclusion from multiple evidence stances | SUPPORTED (sources agree); CONTRADICTED (sources disagree); UNRESOLVED (insufficient) | `WorldBelief` — but currently binary (resolver output == truth), no fusion |
| **Truth** | External world fact | The element IS the Wi-Fi entry; the screen IS the Internet settings page | External world is authoritative (I-4, charter) |

### 7.2 Evidence Stance ≠ Belief State

**This is the critical correction.** Evidence stance and belief state are DIFFERENT:

| Evidence Stance (per source) | Belief State (after fusion) |
|---|---|
| SUPPORTS — this source's evidence supports the claim | SUPPORTED — ≥1 source SUPPORTS, 0 CONTRADICTS |
| CONTRADICTS — this source's evidence contradicts the claim | CONTRADICTED — ≥1 SUPPORTS AND ≥1 CONTRADICTS |
| INSUFFICIENT — this source has insufficient evidence | UNRESOLVED — all INSUFFICIENT or no sources |

**Evidence answers:** "What does Source S say about Claim C?"
**Belief answers:** "What does the semantic owner currently believe after reconciling all sources?"

These must not be mixed. Evidence stance is per-source; belief state is the fusion result. A source that SUPPORTS does not directly produce a SUPPORTED belief — only fusion produces belief.

### 7.3 Current Collapse

The current Runtime **collapses all four levels into one**:

```
resolveSemanticPage(observation)  →  string  →  WorldBelief(Confidence=1.0)
     ↑ evidence              ↑ claim        ↑ belief == truth
```

The injected lambda simultaneously:
1. Reads evidence (observation)
2. Produces a claim (page name)
3. Evaluates the claim (no source, no stance — it IS the answer)
4. Establishes belief (Confidence=1.0)
5. Is treated as truth (no independent verification)

**Evidence = Claim = Belief = Truth** — a four-way collapse. The alias-collapse falsifier is the direct consequence: when the same function is claim-producer, evidence-evaluator, and belief-verifier, the system cannot detect its own errors.

### 7.4 Required Separation

The minimum contract must preserve:

- **Evidence → Claim**: multiple sources produce independent stances about the same claim (text-anchor SUPPORTS vs transition CONTRADICTS vs structural CONTRADICTS).
- **Claim → Belief**: belief is the FUSION of evidence stances, not any single stance. Fusion produces SUPPORTED (sources agree), UNRESOLVED (insufficient), or CONTRADICTED (sources disagree).
- **Belief → Truth**: belief is the Runtime's best conclusion, but the external world remains authoritative (I-4). A belief can be wrong; only post-action world observation can confirm.

---

## 8. Source Independence

### 8.1 Current System Failure

```
Page classifier (resolveSemanticPage)  ==  Page verifier (IsStillMine)
```

In the alias-collapse test (`OpenWorldTypeDirectedScenarioTests.cs:160`):
```csharp
new(page, o => Page(o) == page, ...)
//         ^^^^^^^^^^^^^^^^  == same Page() function
```

There is no independent evidence channel. The system is **structurally unfalsifiable** — no observation can contradict the classifier because the verifier IS the classifier.

### 8.2 Required Falsifier Tests

The semantic contract MUST permit disagreement between sources. Four test cases:

| Test | Source A | Source B | Required Behavior |
|---|---|---|---|
| **A** | Text/vector suggests "Internet page" (text anchors match) | Transition evidence suggests "Wi-Fi detail" (navigated from InternetPage, element inventory changed) | **CONTRADICTED** — refuse to collapse to either; signal ambiguity |
| **B** | Fast classifier says "same page" (text anchors match) | Navigation effect proves child transition (action journal shows Tap dispatched, post-action observation shows different inventory) | **CONTRADICTED** — transition evidence overrides text-anchor "same page" |
| **C** | VLM/page semantic source says "WiFi settings" | Structural evidence says "not a settings page" (element count/shape inconsistent) | **CONTRADICTED** — structural evidence can override VLM |
| **D** | Cached/prototype evidence says "Wi-Fi is at position X" | Fresh observation shows no element at X (element moved or page scrolled) | **CONTRADICTED** — fresh observation overrides cached |

### 8.3 What the Contract Must Permit

The contract must allow:
1. **Multiple evidence stances about the same claim** from different sources — not a single fused value.
2. **Stances can disagree** — one source SUPPORTS while another CONTRADICTS → fusion produces CONTRADICTED, not "pick one silently."
3. **Disagreement is detectable** — the Runtime can distinguish "all sources SUPPORT" from "sources disagree" from "all INSUFFICIENT."
4. **No single source is authoritative** — not even VLM (I-14: AI output is Semantic Evidence, not world truth).

**This is the core purchase.** Without source independence, the alias-collapse is structurally unavoidable. With source independence, the alias-collapse becomes detectable: text-anchor source SUPPORTS "WifiSub" (both pages have SwitchState-bearing Wi-Fi), transition source CONTRADICTS "WifiSub" (navigation changed the inventory) → CONTRADICTED → refuse to collapse.

### 8.4 Source Families (conceptual only — NOT implemented)

| Source Family | Conceptual |
|---|---|
| TEXT_SEMANTIC | OCR text matching, alias resolution, semantic text anchors |
| STRUCTURAL | element count, inventory shape, depth, spatial arrangement |
| TRANSITION | action journal, post-action observation comparison, navigation effect |
| OBSERVED_STATE | SwitchState, element state evidence |
| VISUAL_SEMANTIC | VLM, LLM semantic reasoning (future, provider-neutral) |
| MEMORY | cached prototype, previous-run evidence (P3, deferred) |

**No source is authoritative simply because it produced the hypothesis.** A TEXT_SEMANTIC source that SUPPORTS a claim is not more authoritative than a TRANSITION source that CONTRADICTS it.

---

## 9. Confidence Assessment

### 9.1 Four Distinct Concepts (must not conflate)

| Concept | Definition | Current Status |
|---|---|---|
| **Evidence strength** | How directly is this claim supported? (DIRECT = observed; INFERRED = derived through reasoning) | Reality model contract uses DIRECT/INFERRED on WFs. Runtime has no equivalent. |
| **Evidence reliability** | How trustworthy is this source? (OCR is noisy; VLM is slower but more reliable; structural is deterministic) | Not tracked. All sources treated as equally authoritative. |
| **Hypothesis confidence** | How confident is the BELIEF? (SUPPORTED / UNRESOLVED / CONTRADICTED) | `WorldBelief.Confidence` is binary (0.0 or 1.0). No intermediate. |
| **Decision threshold** | What confidence level triggers action / deferral / refusal? | No grounding threshold exists (CP-12 Case 5: GAP). |

### 9.2 Which Are Needed Now?

| Concept | Required? | Purchased By |
|---|---|---|
| **Evidence strength** | **REQUIRED_NOW** | The contract must know whether a claim is DIRECT (observed: OCR text, SwitchState, foreground app) or INFERRED (derived: "this looks like Internet page because it has Wi-Fi"). DIRECT/INFERRED is already in the reality model contract (Section 5). |
| **Evidence reliability** | **USEFUL_LATER** | Source quality matters but no current falsifier purchases a reliability score. The falsifiers need source INDEPENDENCE (can disagree), not source QUALITY scoring. A simple source label (which channel) is sufficient now. |
| **Hypothesis confidence** | **REQUIRED_NOW** | The Runtime must express SUPPORTED / UNRESOLVED / CONTRADICTED. The alias-collapse needs "CONTRADICTED." The subtitle phantom needs a source CONTRADICTING. The Wi-Fi vs AndroidWifi needs "UNRESOLVED" (insufficient evidence to distinguish). BUT this is **ordinal, not numeric** — it maps to the existing `bool?` tri-state plus a "CONTRADICTED" fusion state. |
| **Decision threshold** | **USEFUL_LATER** | The refusal path (ER-28: "refuse to act when grounding is ambiguous") needs a threshold, but it is qualitative ("insufficient evidence → refuse" = UNRESOLVED → refuse), not a numeric cutoff. |

### 9.3 Belief State Assessment — Is PROBABLE Purchased?

**Candidate belief states:** SUPPORTED / PROBABLE / UNKNOWN / CONTRADICTED

**Challenge:** Is PROBABLE (single source, medium confidence, no contradiction) purchased by any current falsifier?

| Falsifier | What it needs | Uses PROBABLE? |
|---|---|---|
| Alias-collapse | Source disagreement → CONTRADICTED | NO |
| Subtitle phantom | Source contradicts → CONTRADICTED | NO |
| Wi-Fi vs AndroidWifi | Insufficient evidence → UNRESOLVED | NO |
| Empty-text candidate | Source supports → SUPPORTED | NO |
| Parent-return 1:1 | Transition vs text mismatch → CONTRADICTED | NO |

**No falsifier purchases PROBABLE.** Every falsifier needs SUPPORTED, UNRESOLVED, or CONTRADICTED. The distinction between "1 source SUPPORTS" and "2 sources SUPPORT" is evidence STRENGTH, not a separate belief state. It's useful for future ranking but not purchased now.

**Minimum belief states:** SUPPORTED / UNRESOLVED / CONTRADICTED

```
SUPPORTED    = ≥1 source SUPPORTS, 0 CONTRADICTS
UNRESOLVED   = all INSUFFICIENT, or no sources
CONTRADICTED = ≥1 SUPPORTS AND ≥1 CONTRADICTS
```

### 9.4 Numeric Confidence Assessment

```
NumericConfidence: USEFUL_LATER
```

Numeric confidence is NOT purchased by any current falsifier. All need qualitative belief states. The existing Runtime evidence pattern (`bool?` tri-state) already provides the foundation. Numeric may help future ranking/escalation/fast-slow routing but **must not become semantic truth** — a 0.95 confidence is not "this IS the Wi-Fi entry," it is "one source is 95% sure."

---

## 10. Fast / Slow Compatibility

### 10.1 The Seam Requirement

The minimum Semantic Evidence contract must accept evidence from both fast and slow perception without the Runtime coupling to any specific provider.

| Layer | Sources | Evidence Type |
|---|---|---|
| **FAST** | deterministic context, OCR, semantic/vector match, local structural analysis | `SemanticEvidence` with Source = TEXT_SEMANTIC / STRUCTURAL / OBSERVED_STATE / TRANSITION |
| **SLOW** | VLM, stronger semantic reasoning | `SemanticEvidence` with Source = VISUAL_SEMANTIC |

### 10.2 Contract Compatibility

The minimum contract `{ SemanticClaim { Assertion }, SemanticEvidence { Source, Stance, Reason? } }` accepts both:

```
FAST: SemanticEvidence { Source=TEXT_SEMANTIC, Stance=SUPPORTS, Reason="text matches Wi-Fi" }
FAST: SemanticEvidence { Source=STRUCTURAL, Stance=CONTRADICTS, Reason="chevron artifact" }
SLOW: SemanticEvidence { Source=VISUAL_SEMANTIC, Stance=SUPPORTS, Reason="VLM identifies WiFi settings" }
```

These can agree or disagree. The fusion (Belief) is the same regardless of source speed. The contract does not need to know whether a source is "fast" or "slow" — it only needs the Source label (which channel) and the Stance (what it says).

### 10.3 What Is NOT Required

- ❌ **No Vector DB** — embeddings belong to the perception provider, not Runtime (I-14)
- ❌ **No VLM interface** — VLM evidence arrives as `SemanticEvidence` values, same as OCR evidence. The Runtime does not call VLM directly.
- ❌ **No model selection** — which source to consult is a perception-routing concern (P2, deferred). The contract only needs to ACCEPT evidence from any source, not DECIDE which source to use.
- ❌ **No escalation logic** — "when to upgrade to slow" is a perception-routing decision (P2). The contract permits it but does not implement it.

**The contract is provider-neutral.** Evidence arrives as immutable values; the Runtime fuses them into belief. Whether a value came from OCR or VLM is recorded in Source but does not change the fusion logic.

---

## Container Semantic Ownership vs Agent Semantic Authority

> This section establishes the ownership/authority distinction that governs how Semantic Evidence flows through the Runtime.

### Principle A — Container Owns Local Semantic State

Container is the sole owner (I-2) of its local semantic state. This includes:

| Local Semantic State | Current Runtime | Target Model |
|---|---|---|
| Local page semantic belief | `_semanticPageName` (immutable, construction-time) | ContainerLocalBelief (dynamic, revised by reconciliation) |
| Local continuity belief | `TryVerifyLocalContinuity` (observation comparison) | Part of ContainerLocalBelief |
| Local semantic element state | `_observation` (raw snapshot) | Local element evidence interpretation |
| Current local observation interpretation | `_observation` + `_identityRule` | Reconciled local belief |

**Agent does not hold a duplicate of Container semantic state.** Agent holds `AgentWorldBelief` (run-level), which REFERENCES Container beliefs but does not COPY and independently maintain the same local semantic truth.

### Principle B — Agent Has Higher-Level Semantic Intelligence

Agent may possess stronger semantic intelligence than Container. Agent can use:

| Agent Intelligence Source | Scope |
|---|---|
| Goal context | Run-level (what the task requires) |
| Task intent | Run-level (what the user wants) |
| Parent/child Container context | Cross-Container |
| Transition history | Cross-Container (Traversal journal + trace) |
| Cross-Container context | Run-level (which Containers are valid) |
| Slow semantic intelligence (VLM) | Run-level (higher-order semantic reasoning) |
| Higher-level semantic constraints | Run-level (safety, scope, completion) |

Therefore Agent can ADJUDICATE competing Container semantic claims. Agent may:

| Agent Action | Meaning | Current Seam |
|---|---|---|
| **ADJUDICATE** | Determine which semantic claim is correct when sources disagree | Agent decision logic (drift/recovery) |
| **CORRECT** | Override a Container's wrong belief | `CreateContainer(correctedPage)` + `Bind` (discard + rebuild) |
| **REBIND** | Reset Container to a new observation | `Bind(obs)` (existing) |
| **INVALIDATE** | Declare Container's belief no longer valid | Drift detection → Trap → recovery |
| **ESCALATE** | Raise to recovery / failure | `EmitDriftTrap` → `RecoverFromDriftAsync` |

**But Agent does not directly steal Container semantic state ownership.** The correct model:

```
Agent adjudication (decision)
        ↓
Container applies semantic revision (state update)
        ↓
Container remains state owner (I-2)
```

### Principle C — Authority ≠ Ownership

**STATE OWNERSHIP ≠ SEMANTIC DECISION AUTHORITY**

| | Container | Agent |
|---|---|---|
| **State Ownership** | SOLE OWNER of local semantic state (I-2) | SOLE OWNER of run-level state (RunState, WorldBelief instance, trace, branch progress) |
| **Decision Authority** | Local continuity, local completion | Run-level: completion (I-10), drift, recovery, Container switching, semantic adjudication |

Container is the **state owner**; Agent is the **higher semantic authority**. Agent's adjudication does not transfer state ownership — Container applies and holds the resulting semantic revision.

**This fits I-1..I-14:**
- I-2 (one mutable state, one owner): Container owns local semantic state; Agent owns run-level state. ✓
- I-3 (one decision, one authority): Agent has semantic adjudication authority; Container has local state ownership. These are DIFFERENT (ownership ≠ authority). ✓
- I-8 (lower scope can escalate up, cannot steal higher authority): Container can emit Trap (escalate); Agent adjudicates. ✓
- I-13 (no God Context): ContainerLocalBelief and AgentWorldBelief are distinct, not re-aggregated. ✓

### Principle D — Local vs Run-Level Belief

| Belief | Owner | Describes | Current Runtime |
|---|---|---|---|
| **ContainerLocalBelief** | Container | "This Container's current self-understanding: what page am I, how does the current observation interpret, do I still have continuity" | `_semanticPageName` (static) + `_observation` + `_identityRule` — **GAP: not dynamic, not a belief** |
| **AgentWorldBelief** | Agent | "The whole Run's semantic world: which Containers are valid, Goal progress, which Container should be active semantic scope" | `WorldBelief` (SemanticPage + Confidence) — **conflated with local page identity** |

**These two beliefs must not become duplicate state.** AgentWorldBelief REFERENCES/summarizes Container beliefs but does not independently maintain the same local semantic truth. Currently `WorldBelief.SemanticPage` (held by Agent) and `Container._semanticPageName` (held by Container) are overlapping page-identity strings — the closest thing to an I-2 dual-ownership seam. They are different KINDS (volatile/reconciled vs immutable/construction-time) but represent overlapping semantic truth.

**Target:** Container owns a dynamic ContainerLocalBelief (revised by reconciliation). Agent owns an AgentWorldBelief that references Container beliefs (which Container is active, goal progress) without duplicating local page identity.

### Agent Adjudication Mechanism

**Investigation finding:** Container's semantic identity (`_semanticPageName`) and identity rule (`_identityRule`) are both `readonly`, immutable post-construction. Agent has NO API to revise an existing Container's semantic identity — every correction path creates a NEW Container via `CreateContainer(correctedPage)` + `Bind(obs)`.

**Current seam:**
```
Container → Agent: via Trap (escalation up, I-8) — Container detects problem, emits evidence
Agent → Container: via CreateContainer + Bind (discard + rebuild) — Agent adjudicates, creates new Container
```

**Does the first executable falsifier require a new seam?**

**NO.** The alias-collapse falsifier needs:
1. Container detects CONTRADICTED belief (multi-source evidence disagrees) → emits Trap (EXISTING seam)
2. Agent receives Trap, adjudicates using higher semantic context → decides to rebind/create (EXISTING seam)

The existing Trap + CreateContainer/Bind seam carries the adjudication. The Agent's decision logic (drift/recovery) already adjudicates. **No new public representation needed for the minimum purchase.**

**Deferred:** "Revision without rebind" (Agent sends a semantic revision to an EXISTING Container, preserving local progress) is NOT purchased by the first falsifier. The first falsifier only needs DETECTION (CONTRADICTED belief → Trap), not CORRECTION-WITHOUT-RESET. The "Agent adjudication → Container applies revision → Container remains owner" model is the target for future refinement, not the minimum.

### Reconcile Responsibility

**Reconcile remains a pure operation — NOT a stateful owner.**

```
Container:
  previous local belief
  + fresh local evidence (SemanticEvidence values)
  + Agent adjudication (if present)
        ↓
  pure reconciliation (operation, not owner)
        ↓
  revised Container belief (Container still owns)
```

**Current state:** `Reconcile.FromObservation` is a stateless pure function called by Agent. The result (`WorldBelief`) is held by Agent, NOT Container. Container never stores a reconciled belief.

**Target model:** Container invokes pure reconciliation over (previous belief + fresh evidence + adjudication) → produces revised belief → Container holds it. Reconcile is the OPERATION; Container is the OWNER.

**This is a SEMANTIC_GAP (logic location), not an ARCHITECTURE_PRESSURE (boundary).** The boundary (Container owns local state, Agent owns run-level state) is correct. Moving the reconciliation invocation from Agent to Container is a logic relocation, not a new boundary. Reconcile remains a pure function regardless of who invokes it.

### Agent Is NOT a Perception Provider

Agent does not become:
- OCR engine
- VLM adapter
- Vector database
- Page classifier implementation

Evidence providers produce `SemanticEvidence` values. Agent may invoke/use higher-level intelligence and **adjudicate competing semantic claims** — but it does not produce raw perception. Agent consumes evidence stances, adjudicates, and decides.

```
Evidence providers → SemanticEvidence values
Agent → adjudicates competing claims (uses Goal context, cross-Container, VLM reasoning)
Container → owns revised local belief
External world → authoritative
```

---

## 11. Ownership and Authority Fit

### 11.1 Current Architecture Prior (from 14 Invariants + state-owner table)

| Component | Owns | Does NOT Own |
|---|---|---|
| **Perception provider** (external, `IEnvironment` port) | Produces `Observation` (evidence) | Task decisions, semantic interpretation |
| **Semantic interpretation** | Produces claims/evidence stances | — (currently 100% caller-injected as single lambda — THIS IS THE GAP) |
| **Container** (I-2) | Local semantic state (`_semanticPageName`, `_identityRule`, `_observation`, `_viewportExplorationObservations`) | Global goal, world truth, run-level decisions |
| **Traversal** (I-2) | Action-effect verification evidence (`TraversalJournalEntry`: DispatchedAction + PostActionObservation) | World-level semantics, Agent Goal, run termination |
| **Agent** (I-2) | Run-level state (`_belief`, `_state`, `_trace`, `_branchProgress`) + semantic adjudication authority | Element matching implementation, click implementation, OCR, raw perception |
| **External world** | Authoritative | — |

### 11.2 Does the Minimum Evidence Contract Fit?

| Contract Element | Fits Which Owner? | Rationale |
|---|---|---|
| Evidence production (OB → Claim + Stance) | **Perception provider / caller-injected evaluators** | Following the existing pattern: `Goal.EvidenceEvaluator`, `TargetGroundingCriterion.CandidateEvaluator` are caller-injected. Evidence producers stay caller-side, cross the seam as immutable values. |
| Evidence fusion (Stances → Belief) | **Container** (local belief) + **Agent** (run-level adjudication) | Container owns local semantic state; invokes pure reconciliation. Agent owns run-level state; adjudicates when sources disagree or higher context is needed. |
| Belief consumption | **Agent** (completion, drift, recovery decisions) | Agent is the sole completion authority (I-10) and sole semantic adjudication authority. |

### 11.3 Architecture Delta Assessment

**The evidence contract fits the existing boundaries IF AND ONLY IF it is expressed as immutable evidence values** — like the existing `TargetGroundingEvidence`, `CandidateAuthorizationEvidence`, `GoalEvidence` family:

- Produced by caller-injected evaluators (crossing the seam as immutable values)
- Consumed by the single authority that owns the corresponding decision (Container for local continuity; Agent for completion/drift/adjudication)
- Qualitative tri-state (Stance: SUPPORTS/CONTRADICTS/INSUFFICIENT), not numeric
- Not treated as truth (I-4)

**The only seam pressure:** the current single-lambda injection (`resolveSemanticPage: Func<Observation, string?>`) must be augmented with multi-source evidence injection. But this is an **evolution of the existing injection pattern** (Goal already injects 6 evaluators), not a new boundary.

### 11.4 Architecture Delta

```
ArchitectureDelta: NONE
```

The ownership boundaries (I-1..I-14) are correct and sufficient. The evidence contract fits within them:
- Evidence producers: caller-side (injected, as existing evaluators are)
- Evidence fusion: in existing belief owners (Container local, Agent run-level)
- Evidence values: immutable, qualitative, following the existing `TargetGroundingEvidence` pattern
- Agent adjudication: existing seam (Trap + CreateContainer/Bind), no new public representation needed for minimum

The gap is **SEMANTIC** (the evidence model and fusion logic do not exist), not **ARCHITECTURAL** (the ownership boundaries are correct). No new component, no new owner, no new boundary.

**Noted SEMANTIC_GAPs (non-blocking, logic relocations):**
1. `Reconcile.FromObservation` is currently called by Agent; target model has Container invoke reconciliation. Relocation, not new boundary.
2. `Container._semanticPageName` is currently immutable; target model has Container own a dynamic ContainerLocalBelief. Logic gap, not boundary change.
3. `WorldBelief` (held by Agent) is currently conflated with local page identity; target model separates ContainerLocalBelief (Container) from AgentWorldBelief (Agent). Semantic separation, not boundary change.

---

## 12. Action / Compiler Compatibility

### 12.1 Action Semantics Compatibility (P1, not implemented)

The revised gap review defines Action as `{ target semantic, current context, expected effect }`. Can the evidence contract later support this?

| Action Component | Evidence Contract Support | How |
|---|---|---|
| **target semantic** | ✅ | Element Evidence produces identity claims ("this is the Wi-Fi entry"). The Action's target semantic references the element's semantic identity, not its text. |
| **current context** | ✅ | Page Evidence produces page identity claims ("this is Internet page"). Context is DERIVED (Correction 6) from: foreground app, observation, previous Container belief, parent Container, last verified action, transition history, task scope, Goal context. Agent may use broader context. |
| **expected effect** | ✅ (future) | A `SemanticClaim` with Assertion="tapping this element navigates to WiFi detail" — this is a CLAIM about the future, supported by transition history. The contract shape supports it. |
| **fresh verification evidence** | ✅ | `TraversalJournalEntry.PostActionObservation` already exists. Post-action evidence is `SemanticEvidence` with Source=TRANSITION, Stance=SUPPORTS/CONTRADICTS, about the effect claim. |

**No redesign needed.** The contract's `SemanticClaim { Assertion }` + `SemanticEvidence { Source, Stance, Reason? }` shape can express all four Action components.

### 12.2 Compiler Compatibility (P1, not implemented)

The revised gap review proposes a Semantic Execution Contract. Can the evidence contract support this?

| Contract Component | Evidence Contract Support | How |
|---|---|---|
| **desired semantic capability** | ✅ | A `SemanticClaim` about what capability is needed ("this intent requires distinguishing switch from label"). This is a CLAIM about the task, not about the current world. |
| **allowed interaction** | ✅ | Already exists as `TypeLevelSafetyBoundary.AllowedInteractionCategories`. No change needed. |
| **expected semantic object kinds** | ✅ | A `SemanticClaim` about what elements/pages should be found ("Wi-Fi entry should be on Internet page"). This is an EXPECTATION (claim about what should exist), NOT a current-world assertion. |
| **safety constraints** | ✅ | Already exists as `TypeLevelSafetyBoundary` + `CandidateAuthorizationEvidence`. |
| **completion semantics** | ✅ | Already exists as `GoalEvidence` (Satisfied + Reason). |

### 12.3 Critical Constraint: Compiler Must Not Reference Current-World Claims

The Compiler (Semantic Execution Contract) operates in the **expectation** layer — it produces claims about what SHOULD be, not what IS.

```
Compiler produces:  expected semantic object kinds (CLAIM about future)
Evidence produces:  observed element evidence (STANCE about present claim)
Belief fuses:       current-world belief (FUSION of present stances)
```

The Compiler's expected semantic object kinds are `SemanticClaim` values (expectations), NOT `SemanticEvidence` stances (current-world observations). The contract cleanly separates expectations (Compiler) from observations (Evidence) from belief (fusion).

**No redesign needed.** The evidence contract supports both Action Semantics and Compiler Contract as future consumers.

---

## 13. Minimum Semantic Purchase Candidate

### 13.1 The Minimum Contract

```
SemanticClaim { Assertion }

SemanticEvidence { Source, Stance, Reason? }
  where Stance = SUPPORTS / CONTRADICTS / INSUFFICIENT
```

- `SemanticClaim` = the semantic hypothesis ("this is the Wi-Fi entry" / "this is Internet page")
- `SemanticEvidence` = a source's evaluation of a claim: which channel, what stance, optional reason
- **Subject** is implicit (the element/page the claim is evaluated against)
- **Support** is `Reason` string (optional, following existing pattern)
- **Freshness** is `Observation.SequenceNumber` (already exists)
- **Provenance** is deferred (reality-model-admission concern)
- **Numeric confidence** is rejected (USEFUL_LATER, not purchased)

### 13.2 How It Generalizes the Existing Pattern

| Existing Type | Generalized As |
|---|---|
| `TargetGroundingEvidence` (bool? Supported + Reason) | `SemanticEvidence` with Source="grounding-evaluator", Stance = Supported ? SUPPORTS : CONTRADICTS, Reason |
| `CandidateAuthorizationEvidence` (bool? Authorized + Reason) | `SemanticEvidence` with Source="authorization-evaluator", Stance = Authorized ? SUPPORTS : CONTRADICTS, Reason |
| `GoalEvidence` (bool Satisfied + Reason) | `SemanticEvidence` with Source="goal-evaluator", Stance = Satisfied ? SUPPORTS : CONTRADICTS, Reason |

The minimum contract adds **Source** (which channel) and **CONTRADICTS** (explicit negative stance) and **INSUFFICIENT** (explicit neutral stance) to the existing pattern. The `SemanticClaim` (Assertion) is the new claim layer that separates hypothesis from evidence stance.

### 13.3 Element Evidence Minimum

Five distinct semantic dimensions (NOT implementation types), all expressible as `SemanticClaim` + `SemanticEvidence`:

1. **ElementExistence**: `SemanticClaim { "this is/is-not a real interactive element" }` → evidence from STRUCTURAL, TEXT_SEMANTIC sources
2. **ElementIdentity**: `SemanticClaim { "this is element <X>" }` → evidence from TEXT_SEMANTIC, STRUCTURAL, VISUAL_SEMANTIC sources
3. **ElementCategory**: `SemanticClaim { "this element is category <C>" }` → evidence from STRUCTURAL, TEXT_SEMANTIC sources (NOT from YOLO label alone — RM-09)
4. **ElementInteractionCapability**: `SemanticClaim { "this element supports <navigate/toggle/read>" }` → evidence from STRUCTURAL, TRANSITION sources
5. **ElementState**: `SemanticClaim { "this element state is <on/off/...>" }` → evidence from OBSERVED_STATE source (SwitchState is one dimension; future states open-ended)

### 13.4 Page Evidence Minimum

Three evidence dimensions, all expressible as `SemanticClaim` + `SemanticEvidence`:

1. **PageIdentity**: `SemanticClaim { "this is page <P>" }` → evidence from TEXT_SEMANTIC, STRUCTURAL, OBSERVED_STATE sources
2. **PageTransition**: `SemanticClaim { "navigation from <A> produced page <B>" }` → evidence from TRANSITION source (action journal + post-action observation)
3. **PageContinuity**: `SemanticClaim { "this is still page <P>" }` → evidence from TRANSITION, TEXT_SEMANTIC sources (identity-persistence)

### 13.5 Belief Fusion (Minimum)

Belief is the fusion of multiple `SemanticEvidence` stances about the same `SemanticClaim`:

| Fusion Result | Condition |
|---|---|
| **SUPPORTED** | ≥1 source SUPPORTS, 0 CONTRADICTS |
| **UNRESOLVED** | all INSUFFICIENT, or no sources |
| **CONTRADICTED** | ≥1 SUPPORTS AND ≥1 CONTRADICTS |

**PROBABLE is NOT purchased** — no falsifier needs "single source, medium confidence." The minimum is 3 belief states.

**This is the minimum that makes the alias-collapse falsifiable:** text-anchor source (SUPPORTS "WifiSub") and transition source (CONTRADICTS "WifiSub") → CONTRADICTED → refuse to collapse → alias-collapse detected.

---

## 14. Rejected / Deferred Abstractions

### 14.1 Rejected (not purchased by any current falsifier)

| Abstraction | Reason Rejected |
|---|---|
| **Numeric confidence (float 0.0–1.0)** | No falsifier purchases a numeric score. All need qualitative stances (support/contradict/insufficient). Existing pattern is `bool?` tri-state. Violates "Confidence ≠ Truth" if used as semantic truth. |
| **Timestamps** | Runtime uses `SequenceNumber` (Decision 6 — no real clock). Timestamps are provenance, not evidence. |
| **Semantic element/page IDs** | Circular — the contract ESTABLISHES identity; it cannot assume it. Embedding IDs violates "no answers in reality" (contract Section 12). |
| **Embeddings stored in Runtime** | Runtime does not own embeddings (I-14). Perception provider owns vector representations. |
| **Generic metadata bags** | Violates I-12 (YAGNI) and I-13 (no God Context). No falsifier purchases unstructured metadata. |
| **Inheritance hierarchies** | Over-abstraction. Existing evidence types are flat `sealed record` values. No falsifier purchases polymorphism. |
| **Full provenance chains** | Reality-model-admission concern (E0-E4 grading). Runtime needs Source + Stance, not artifact traceability. |
| **Context object** | Context is DERIVED from existing signals (foreground app, observation, previous belief, parent Container, action journal, transition history, task scope, Goal context). No falsifier purchases a Context model. |
| **PROBABLE belief state** | No falsifier needs "single source, medium confidence." SUPPORTED/UNRESOLVED/CONTRADICTED suffice. |

### 14.2 Deferred (purchased by future falsifiers, not current)

| Abstraction | Deferred To | Why Deferred |
|---|---|---|
| **Evidence reliability scoring** | P2 (Perception Routing) | Source QUALITY matters for routing, but current falsifiers need source INDEPENDENCE, not quality scoring. |
| **Numeric confidence** | P2 (Perception Routing) | Useful for ranking/escalation/fast-slow routing, but not purchased by any current falsifier. Must not become semantic truth. |
| **Decision thresholds** | P1 (Belief Model) | "UNRESOLVED → refuse" is qualitative (ER-28). Numeric thresholds for action/deferral/refusal are policy, not evidence. |
| **Memory / prototypes** | P3 (Semantic Memory) | Must follow Evidence → Belief → Memory. Memory of wrong classifications would be reinforced. |
| **Perception routing / escalation** | P2 | The contract ACCEPTS evidence from any source but does not DECIDE which source. |
| **Element identity algorithm** | P0 implementation | The contract defines WHAT evidence is needed, not HOW to produce it. |
| **VLM / vector interfaces** | P2 | The contract is provider-neutral. VLM/vector evidence arrives as `SemanticEvidence` values. |
| **Action semantics** | P1 | The contract SUPPORTS it (Section 12) but does not implement it. |
| **Compiler / Semantic Execution Contract** | P1 | The contract SUPPORTS it (Section 12) but does not implement it. |
| **Revision without rebind** | Future refinement | Agent→Container semantic revision preserving local progress. First falsifier only needs DETECTION (CONTRADICTED → Trap), not CORRECTION-WITHOUT-RESET. |
| **Full provenance** | Reality model admission | E0-E4 grading and artifact traceability are governance concerns, not Runtime. |
| **Future ElementState dimensions** | Evidence-driven | selected, enabled, expanded, checked, connected, loading, focused — INSUFFICIENT_EVIDENCE (not observed in committed reality). |
| **ContainerLocalBelief (dynamic)** | P0 implementation | Container._semanticPageName is currently immutable. Dynamic local belief is the target, but the boundary (Container owns local state) is correct. |

---

## 15. Executable Scenarios Required

### 15.1 First Executable Falsifier (Minimum)

**Alias-Collapse Source Independence Test**

Given `RealitySeededSettingsFixture` or `OpenWorldTypeDirectedScenarioTests` fixture:
- InternetPage (has "Wi-Fi" entry + "AndroidWifi" + empty-text toggle)
- WifiPage (has "Wi-Fi" switch with SwitchState)

**Current behavior (unfalsifiable):**
- `resolveSemanticPage` returns "WifiSub" for both (both have SwitchState-bearing Wi-Fi element)
- `IsStillMine` uses same `Page()` function → confirms "WifiSub" for both
- Container cannot detect it is looking at the wrong page

**Required behavior (falsifiable):**
- Source A (TEXT_SEMANTIC): `SemanticEvidence { Source=TEXT_SEMANTIC, Stance=SUPPORTS, Reason="both pages have SwitchState-bearing Wi-Fi" }` about Claim "page is WifiSub"
- Source B (TRANSITION): `SemanticEvidence { Source=TRANSITION, Stance=CONTRADICTS, Reason="navigated from InternetPage, inventory changed — this is WifiPage" }` about Claim "page is WifiSub"
- Fusion: Source A SUPPORTS and Source B CONTRADICTS → **CONTRADICTED**
- Belief: page identity = CONTRADICTED → refuse to collapse → Container detects it might be looking at the wrong page → emit Trap (existing seam)

Then Agent may adjudicate using higher semantic context if required (existing drift/recovery decision logic).

**Pass condition:** The Runtime produces `CONTRADICTED` (not silently "WifiSub") when text-anchor and transition evidence disagree about page identity. A wrong semantic claim can be contradicted by independent evidence.

**Fail condition:** The Runtime collapses to "WifiSub" (alias-collapse persists) — the contract is unfalsifiable.

**Pass is NOT:** "classifier changed to return correct answer."
**Pass IS:** "wrong semantic claim can be contradicted by independent evidence."

### 15.2 Element Existence Falsifier

Given A3 SettingsRoot with "Bluetooth, pairing" subtitle phantom (element [9]):
- Source A (TEXT_SEMANTIC): `SemanticEvidence { Source=TEXT_SEMANTIC, Stance=SUPPORTS, Reason="text matches Bluetooth" }` about Claim "element is Bluetooth menu entry"
- Source B (STRUCTURAL): `SemanticEvidence { Source=STRUCTURAL, Stance=CONTRADICTS, Reason="chevron artifact, not interactive" }` about Claim "element is Bluetooth menu entry"
- Fusion: SUPPORTS vs CONTRADICTS → **CONTRADICTED** → refuse to ground on this element

### 15.3 Element Identity Ambiguity Falsifier

Given A3 Internet page with "Wi-Fi" (entry) and "AndroidWifi" (SSID):
- Source A (TEXT_SEMANTIC): both elements match target "Wi-Fi" → two candidates
- No additional source can distinguish → Stance=**INSUFFICIENT** for both
- Belief: element identity = **UNRESOLVED** → refuse to select → signal ambiguity (ER-28)

### 15.4 Additional Scenarios Needed (for evidence maturity upgrade)

| Scenario | Reality Asset | Tests |
|---|---|---|
| Wi-Fi OFF→ON state-change pair | **MISSING** (synthetic in fixture) | E4 evidence of desired-state chain |
| Real popup / obstruction | **MISSING** | `IsLocalObstructionHypothesis` with real data |
| Real scroll continuity | **MISSING** | Viewport continuity with real scroll data |
| Real drift / recovery | **MISSING** | `IsAgentScopeDrift` with real app-switch |

These are **REALITY_EVIDENCE_GAP** — the contract can be validated against synthetic data (E1), but E3/E4 evidence is needed for production confidence.

---

## 16. Recommendation

### 16.1 The Minimum Purchase

The minimum Semantic Evidence contract purchased by current executable reality is:

```
SemanticClaim { Assertion }

SemanticEvidence { Source, Stance, Reason? }
  where Stance = SUPPORTS / CONTRADICTS / INSUFFICIENT

Belief fusion → SUPPORTED / UNRESOLVED / CONTRADICTED
```

**Plus:** Container-owned local semantic belief (ContainerLocalBelief, dynamic, revised by pure reconciliation) + Agent semantic adjudication authority (existing Trap + CreateContainer/Bind seam).

### 16.2 Why This Is Minimum

1. **SemanticClaim (Assertion)** is purchased by the Evidence ≠ Claim separation (reality contract Section 14: raw perception output ≠ interpreted claim).
2. **Source** is purchased by the alias-collapse falsifier (RM-01 ER-04: source attribution required; same oracle as classifier+verifier = unfalsifiable).
3. **Stance (SUPPORTS/CONTRADICTS/INSUFFICIENT)** is purchased by every falsifier — each needs sources that can agree, disagree, or be insufficient.
4. **Belief fusion (SUPPORTED/UNRESOLVED/CONTRADICTED)** is purchased by the alias-collapse (CONTRADICTED), subtitle phantom (CONTRADICTED), Wi-Fi vs AndroidWifi (UNRESOLVED).
5. **Nothing else is purchased** — numeric confidence, timestamps, semantic IDs, embeddings, provenance, metadata bags, inheritance, Context, PROBABLE are all rejected.

### 16.3 Why This Is Sufficient

The contract makes every falsifier in Section 3 detectable:
- Alias-collapse: TEXT_SEMANTIC vs TRANSITION → CONTRADICTED
- Subtitle phantom: TEXT_SEMANTIC vs STRUCTURAL → CONTRADICTED
- Wi-Fi vs AndroidWifi: single source, two candidates → UNRESOLVED → refuse
- Empty-text candidate: existence evidence without text → SUPPORTED
- Parent-return 1:1: TRANSITION vs TEXT_SEMANTIC → CONTRADICTED

### 16.4 Architecture Fit

**ArchitectureDelta = NONE.** The contract fits the existing ownership boundaries (I-1..I-14):
- Evidence producers: caller-injected (existing pattern)
- Evidence fusion: Container (local belief) + Agent (run-level adjudication)
- Evidence values: immutable, qualitative, following existing `TargetGroundingEvidence` family
- Agent adjudication: existing seam (Trap + CreateContainer/Bind), no new public representation
- Reconcile: remains a pure operation, not a stateful owner
- Authority ≠ Ownership: Container owns state, Agent adjudicates

### 16.5 First Recommended Challenge

**Challenge: Can the Runtime detect the alias-collapse using source-independent evidence?**

Build the first executable falsifier (Section 15.1) against `RealitySeededSettingsFixture`:
- Inject TWO evidence sources (TEXT_SEMANTIC + TRANSITION) instead of ONE `resolveSemanticPage` lambda
- Fuse: if one SUPPORTS and one CONTRADICTS → CONTRADICTED → refuse to collapse
- Assert: InternetPage and WifiPage are NOT collapsed to "WifiSub"

**This is the minimum vertical slice that proves the Semantic Evidence contract is falsifiable.** If the Runtime can detect the alias-collapse, the contract is purchased by reality. If it cannot, the contract is insufficient and must be expanded.

### 16.6 Classification Summary

| Item | Classification |
|---|---|
| Evidence layer missing | **SEMANTIC_GAP** (P0 — SEMANTIC_EVIDENCE_MODEL_GAP) |
| Single-lambda → multi-source injection | **SEMANTIC_GAP** (evolution of existing injection pattern) |
| Container._semanticPageName immutable → dynamic belief | **SEMANTIC_GAP** (logic gap, boundary correct) |
| WorldBelief conflated with local page identity | **SEMANTIC_GAP** (semantic separation, boundary correct) |
| Reconcile called by Agent → Container invokes | **SEMANTIC_GAP** (logic relocation, not new boundary) |
| Agent adjudication seam | **EXISTING** (Trap + CreateContainer/Bind suffices for minimum) |
| Revision without rebind | **DEFERRED_CAPABILITY** (not purchased by first falsifier) |
| Numeric confidence | **DEFERRED_CAPABILITY** (USEFUL_LATER, not purchased) |
| PROBABLE belief state | **REJECTED** (not purchased by any falsifier) |
| Context model | **DEFERRED** (derivable from existing signals) |
| Wi-Fi OFF→ON reality pair | **REALITY_EVIDENCE_GAP** (E4 evidence missing) |
| Action semantics | **DEFERRED_CAPABILITY** (P1, contract supports but does not implement) |
| Compiler contract | **DEFERRED_CAPABILITY** (P1, contract supports but does not implement) |
| Perception routing | **DEFERRED_CAPABILITY** (P2, contract permits but does not implement) |
| Memory | **DEFERRED_CAPABILITY** (P3, must follow Evidence → Belief → Memory) |
| Future ElementState dimensions | **INSUFFICIENT_EVIDENCE** (not observed in committed reality) |

---

## SEMANTIC_EVIDENCE_MINIMUM_CONTRACT_FINALIZED

```
ModelRouting:
  Opus = <canonical semantic distinctions, evidence/claim/belief/truth boundary,
          container vs agent ownership/authority, minimum purchase, architecture fit,
          rejecting over-design, final document conclusion>
  Haiku = <current code inspection, reality evidence mining, legacy mining,
           contract minimization field-checking, ownership evidence, document drafting tables>

ExactEvidenceContract:
  SemanticClaim { Assertion }
  SemanticEvidence { Source, Stance, Reason? }
    where Stance = SUPPORTS / CONTRADICTS / INSUFFICIENT
  Evidence evaluates a claim: "Source S has Stance T about Claim C."
  Multiple SemanticEvidence values (from different sources) can exist for the same claim.
  Subject is implicit (attachment point).
  Support is optional Reason string (following existing TargetGroundingEvidence pattern).
  Freshness is Observation.SequenceNumber (already exists).
  Provenance is deferred (reality-model-admission concern).
  Numeric confidence is rejected (USEFUL_LATER).
  This generalizes the existing TargetGroundingEvidence (bool? + Reason) with
  added Source, explicit CONTRADICTS stance, and a separated SemanticClaim layer.

ElementSemanticDimensions:
  Five DISTINCT semantic dimensions (NOT implementation types):
    1. ElementExistence    — REQUIRED_NOW (subtitle phantom, empty OCR, search box misclass)
    2. ElementIdentity     — REQUIRED_NOW (Wi-Fi vs AndroidWifi, VRD-01, substring overmatch)
    3. ElementCategory     — REQUIRED_NOW (RM-09 type labels unreliable; dispatch depends on it)
    4. ElementInteractionCapability — REQUIRED_NOW (VRD-02; distinct from category)
    5. ElementState        — PARTIALLY_PURCHASED (SwitchState only; future states INSUFFICIENT_EVIDENCE)
  Identity ≠ Category (Wi-Fi entry=NavigableContainer vs Wi-Fi switch=StateChangingControl).
  Category ≠ Capability (same category, different capabilities possible).
  State is open-ended (SwitchState is one dimension; selected/enabled/expanded/... future).

EvidenceStance:
  SUPPORTS / CONTRADICTS / INSUFFICIENT
  (per source, per claim — a source's evaluation of a claim)

BeliefStates:
  SUPPORTED / UNRESOLVED / CONTRADICTED
  (fusion result after reconciling all source stances)
  SUPPORTED    = ≥1 source SUPPORTS, 0 CONTRADICTS
  UNRESOLVED   = all INSUFFICIENT, or no sources
  CONTRADICTED = ≥1 SUPPORTS AND ≥1 CONTRADICTS
  PROBABLE is REJECTED (not purchased by any falsifier).

ContainerSemanticOwnership:
  Container is the SOLE OWNER (I-2) of local semantic state:
    - local page semantic belief (currently _semanticPageName, immutable — GAP: target is dynamic)
    - local continuity belief
    - local semantic element state interpretation
    - current local observation interpretation
  Agent does NOT hold a duplicate of Container local semantic state.
  AgentWorldBelief REFERENCES Container beliefs, does not COPY and independently maintain them.
  (Currently WorldBelief.SemanticPage and Container._semanticPageName are overlapping
   page-identity strings held by two owners — the closest thing to an I-2 seam.
   Target: Container owns dynamic ContainerLocalBelief; Agent owns run-level AgentWorldBelief.)

AgentSemanticAuthority:
  Agent has HIGHER SEMANTIC AUTHORITY (≠ state ownership):
    - can ADJUDICATE competing Container semantic claims
    - can CORRECT (CreateContainer + Bind with corrected page)
    - can REBIND (Bind new observation)
    - can INVALIDATE (drift detection → Trap)
    - can ESCALATE (Trap → recovery/failure)
  Agent uses: Goal context, task intent, parent/child Container context,
              transition history, cross-Container context, VLM reasoning, higher constraints.
  Agent is NOT a perception provider (not OCR/VLM/vector/classifier).
  Agent consumes SemanticEvidence values and adjudicates — does not produce raw perception.
  Authority ≠ Ownership: Container owns state; Agent adjudicates decisions.

AgentAdjudicationMechanism:
  EXISTING SEAM for minimum purchase.
  Container → Agent: via Trap (escalation up, I-8) — Container detects CONTRADICTED, emits evidence.
  Agent → Container: via CreateContainer(correctedPage) + Bind(obs) (discard + rebuild).
  Agent decision logic (drift/recovery) already adjudicates — no new API needed.
  "Revision without rebind" (Agent sends revision to EXISTING Container, preserving progress)
    is DEFERRED — not purchased by first falsifier (first falsifier needs DETECTION not CORRECTION-WITHOUT-RESET).

ReconcileResponsibility:
  PURE OPERATION — NOT a stateful owner.
  Container provides: previous local belief + fresh local evidence + Agent adjudication (if present)
    → pure reconciliation (operation) → revised Container belief (Container still owns).
  Currently Reconcile.FromObservation is stateless pure function called by Agent;
    WorldBelief held by Agent, NOT Container.
  Target: Container invokes pure reconciliation, holds revised ContainerLocalBelief.
  This is a SEMANTIC_GAP (logic location), not ARCHITECTURE_PRESSURE (boundary).
  Reconcile remains a pure function regardless of who invokes it.

ArchitectureDelta:
  NONE
  Ownership boundaries I-1..I-14 are correct and sufficient.
  Evidence producers: caller-injected (existing pattern).
  Evidence fusion: Container (local belief) + Agent (run-level adjudication).
  Evidence values: immutable, qualitative, following existing TargetGroundingEvidence family.
  Agent adjudication: existing seam (Trap + CreateContainer/Bind).
  Reconcile: pure operation, not stateful owner.
  Authority ≠ Ownership (Container owns state, Agent adjudicates).
  Gap is SEMANTIC (evidence model + fusion logic missing), not ARCHITECTURAL (boundaries wrong).
  Noted SEMANTIC_GAPs (non-blocking): Reconcile invocation location, Container belief dynamism,
    WorldBelief/ContainerLocalBelief separation — all logic relocations, not boundary changes.

FirstExecutableFalsifier:
  Alias-Collapse Source Independence Test —
  Given InternetPage (Wi-Fi entry + AndroidWifi + empty-text toggle) and
  WifiPage (Wi-Fi switch with SwitchState):
    Claim: "page is WifiSub"
    Source A (TEXT_SEMANTIC): Stance=SUPPORTS (both pages have SwitchState-bearing Wi-Fi)
    Source B (TRANSITION): Stance=CONTRADICTS (navigated from InternetPage, inventory changed)
    Fusion: SUPPORTS + CONTRADICTS → CONTRADICTED → refuse to collapse.
  Pass: Runtime produces CONTRADICTED (not silently "WifiSub").
        A wrong semantic claim is contradicted by independent evidence.
  Fail: Runtime collapses to "WifiSub" (alias-collapse persists, contract unfalsifiable).
  Then Agent may adjudicate using higher semantic context (existing drift/recovery logic).

ReadyForSemanticPurchase:
  YES

MinimumNextPurchase:
  SemanticClaim { Assertion } value type
  + SemanticEvidence { Source, Stance, Reason? } value type
    where Stance = SUPPORTS / CONTRADICTS / INSUFFICIENT
  + belief fusion producing SUPPORTED / UNRESOLVED / CONTRADICTED
  + multi-source evidence injection replacing single resolveSemanticPage lambda
  + first executable falsifier (alias-collapse source independence test)
    against RealitySeededSettingsFixture
  using EXISTING adjudication seam (Container→Agent Trap + Agent→Container CreateContainer/Bind).

ThingsExplicitlyDeferred:
  - Numeric confidence (USEFUL_LATER — ranking, escalation, fast/slow routing)
  - Evidence reliability scoring (P2 — source quality for perception routing)
  - Decision thresholds (P1 — qualitative "UNRESOLVED → refuse" suffices now)
  - PROBABLE belief state (REJECTED — not purchased by any falsifier)
  - Memory / prototypes (P3 — must follow Evidence → Belief → Memory)
  - Perception routing / escalation (P2 — contract accepts any source, does not select)
  - Element identity algorithm (P0 implementation — contract defines WHAT, not HOW)
  - VLM / vector interfaces (P2 — provider-neutral, no direct interface in Runtime)
  - Action semantics (P1 — contract supports, does not implement)
  - Compiler / Semantic Execution Contract (P1 — contract supports, does not implement)
  - Revision without rebind (future refinement — Agent→Container revision preserving progress)
  - Context model (DEFER — derivable from existing signals)
  - Full provenance chains (reality-model-admission concern, not Runtime)
  - Wi-Fi OFF→ON reality pair (REALITY_EVIDENCE_GAP — E4 evidence missing)
  - Future ElementState dimensions (INSUFFICIENT_EVIDENCE — selected/enabled/expanded/...
    not observed in committed reality)
  - ContainerLocalBelief dynamism (P0 implementation — boundary correct, logic gap)
  - WorldBelief/ContainerLocalBelief separation (P0 implementation — semantic separation)
```

> Production Changes: NONE
> Runtime Changes: NONE
> OpenSpec: NONE
> Implementation: NONE

STOP.
