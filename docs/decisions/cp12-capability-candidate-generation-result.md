# CP12_CAPABILITY_CANDIDATE_GENERATION_RESULT

> Generated: 2026-08-09
> Role: Project Leader / Capability Research Coordinator
> Mode: AUTHORIZED_CAPABILITY_CANDIDATE_GENERATION
> Authorization: `HUMAN_AUTHORIZE_CP12_CAPABILITY_CANDIDATE_GENERATION` (2026-08-09)
> Inputs: RM-10 (ACCEPTED) · CP-12 Challenge (5/5 GAP) · Semantic Gate (CAPABILITY_GAP) · RM-09 · Contract §19

---

## Authorization

`HUMAN_AUTHORIZE_CP12_CAPABILITY_CANDIDATE_GENERATION` — granted 2026-08-09.

## Reality Requirement

CP-12 / RM-10 requires the system to establish target identity under perceptual ambiguity: when N > 1 observable elements plausibly match a target description, the system must select the intended target using evidence beyond text matching — or refuse to act when evidence is insufficient.

The current Runtime's target grounding ("best text match → tap → hope") satisfies none of RM-10's four Expected Requirements. A new behavioral capability is needed.

---

## Semantic Question: PRE-ACTION, POST-ACTION, or COMPOSED?

**Can CP-12 be honestly satisfied by pre-action evidence alone?**

No. NC-05 (insufficient evidence) demonstrates that two candidates can be materially indistinguishable from pre-action observation alone. Two menu_items with related text ("Wi‑Fi" / "Wi‑Fi Calling") on the same screen at nearby coordinates — the only distinguishing property is their navigation target, which is not observable without interaction.

**Can CP-12 be honestly satisfied by post-action verification alone ("tap first, verify later")?**

No. Some actions are irreversible or unsafe. The capability must compose with safety authorization — a target may be safe but wrong, or correct but unsafe. "Tap first" is not universally admissible. CP-12 cannot assume all actions are harmless or reversible.

**Reality requires: PRE-ACTION + POST-ACTION + REFUSAL as a composed behavioral capability.**

Pre-action evidence narrows the candidate set where possible. Post-action observation verifies the selection where pre-action evidence was insufficient. Refusal prevents action when neither stage can establish identity with sufficient confidence. These are three phases of one indivisible grounding decision, not three separate capabilities.

---

## Candidate Set

**Count: 5** genuine behavioral approaches. Each defined in behavioral terms — no implementation, no architecture, no algorithms.

---

### GC-01 — Pre-Action Discriminative Grounding

**Candidate ID:** GC-01

**Title:** Target Identity Established Through Pre-Action Observable Distinguishing Evidence

**Behavioral Thesis:** Before dispatching any action, the system gathers observable evidence about each candidate beyond text matching. If one candidate possesses a distinguishing observable property that the others lack, that candidate is selected as the intended target. If no candidate can be distinguished, the system refuses to act.

**RM-10 ER Coverage:**

| ER | Coverage | Explanation |
|---|---|---|
| ER-25 (evidence beyond text) | **FULL** | Requires additional observable evidence categories for selection |
| ER-26 (type-interaction consistency) | **FULL** | Observable properties must be consistent with intended interaction before selection |
| ER-27 (post-selection verification) | **NONE** | No post-action verification capability — refuses when pre-action evidence insufficient |
| ER-28 (ambiguity refusal) | **FULL** | Explicitly refuses when candidates cannot be distinguished |

**Required Inputs:** Target description, observable element inventory (text, type, coordinates, spatial context), intended interaction type.

**Evidence Used:** Element type, screen region / spatial position relative to other elements, element ordering in the visual hierarchy, prior interaction history on the current screen.

**Decision Boundary:** One candidate possesses a distinguishing observable property that no other matching candidate possesses AND that property is consistent with the intended interaction. Example: among "Wi‑Fi" and "Wi‑Fi Calling", "Wi‑Fi" is at the expected screen position (top of list, below "Network & Internet" header) while "Wi‑Fi Calling" is further down — spatial context distinguishes them.

**What establishes sufficient identity?** A candidate has at least one observable property that: (a) distinguishes it from all other matching candidates, (b) is consistent with the intended interaction type, and (c) is stable across the current observation (not a transient perception artifact).

**Ambiguity Behavior:** When no candidate can be distinguished — all matching candidates share the same distinguishing properties or no distinguishing property exists — the system refuses to act. It signals "grounding ambiguous: N candidates, insufficient distinguishing evidence."

**Action Relationship:** Dispatch requires confirmed identity. The system does not act on a hypothesis — it acts only when one candidate is distinguishable. Before dispatch: "this candidate IS the intended target." Not: "this candidate MIGHT BE the intended target."

**Post-Action Relationship:** Not applicable — GC-01 does not dispatch when identity is uncertain. Post-action observation is not part of the grounding decision.

**Failure Modes:**
- **False Positive Risk: LOW.** The system only acts when one candidate is distinguishable. The risk is that the distinguishing property is accidentally shared by a wrong candidate — e.g., two elements at the same screen position (overlapping elements, one hidden).
- **False Negative / Refusal Risk: HIGH.** The system refuses when candidates are materially indistinguishable from pre-action observation. NC-05 (Wi‑Fi vs Wi‑Fi Calling at similar positions) will cause refusal even though interaction with either would reveal the correct one. The capability sacrifices action for certainty.

**Safety Interaction:** Composes with safety gating: a distinguishable candidate may still be unsafe to act on. Safety rejection does not change the grounding decision — it prevents action on a correctly-grounded but unsafe target.

**CP-11 Dependency:** HIGH. GC-01 requires type labels to be reliable enough that type can serve as a distinguishing property. If type labels are wrong (CP-11 failure), a wrong candidate may appear distinguishable when it's not, or the correct candidate may appear undistinguishable when it is.

**Architecture-Invariant Compatibility:**
- Agent owns completion (I-10): ✓ — grounding decision is part of action selection, not completion
- External world authoritative: ✓ — distinguishing properties are observed from the world, not inferred from plan
- Plan ≠ reality (I-5): ✓ — the plan's target description is a search key, not an identity proof

**What It Does NOT Solve:** Cases where all matching candidates are materially indistinguishable from pre-action observation (NC-05). Any scenario where the only distinguishing property is the navigation target (which requires interaction to observe).

**Pass Oracle:** Target "Wi‑Fi" on Settings home. GC-01 observes menu_item "Wi‑Fi" at (0.5, 0.31) and menu_item "Wi‑Fi Calling" at (0.5, 0.54). Spatial context distinguishes them — "Wi‑Fi" is at the expected position. GC-01 selects "Wi‑Fi." Action dispatched. Correct.

**Fail Oracle:** Target "Notifications" on Settings home. GC-01 observes menu_item "Notifications" at (0.32, 0.78). Text matches. Type matches. One candidate. GC-01 selects it. But perception misclassified a text element as menu_item at nearby coordinates — the distinguishable candidate is the WRONG one (CP-11 failure leaking into CP-12). GC-01 dispatches on wrong target.

---

### GC-02 — Expected-Effect Identity Grounding

**Candidate ID:** GC-02

**Title:** Target Identity Defined Partly Through Expected World Transition

**Behavioral Thesis:** The intended target is not fully defined by its observable properties before interaction — it is also defined by the world transition that interacting with it produces. "Wi‑Fi" is the element whose tap leads to the Wi‑Fi settings page. The system selects a candidate, acts on it, and verifies target identity by observing whether the resulting world state matches the expected transition.

**RM-10 ER Coverage:**

| ER | Coverage | Explanation |
|---|---|---|
| ER-25 (evidence beyond text) | **FULL** | Expected world transition IS evidence beyond text matching |
| ER-26 (type-interaction consistency) | **FULL** | Observable properties must be consistent pre-action; confirmed post-action |
| ER-27 (post-selection verification) | **FULL** | Core mechanism — identity verified through post-action observation |
| ER-28 (ambiguity refusal) | **PARTIAL** | Refusal only after failed verification; acts on unverified hypothesis first |

**Required Inputs:** Target description, observable element inventory, intended interaction type, expected world transition (page identity or element inventory change after successful interaction).

**Evidence Used:** Pre-action observable properties (text, type, coordinates) for candidate selection. Post-action world state (page identity, element inventory) for identity verification.

**Decision Boundary:** A candidate is selected using available pre-action evidence (text matching + type + coordinates — accepting that this may be wrong). Action is dispatched. After action, the world state is observed. If the world state matches the expected transition, target identity is CONFIRMED. If it does not match, the selection is REJECTED.

**What establishes sufficient identity?** Identity is established when: (a) a candidate matches the target description, (b) the candidate's observable properties are consistent with the intended interaction, AND (c) after acting on the candidate, the observed world state matches the expected transition. Identity is provisional until (c) is satisfied.

**Ambiguity Behavior:** When pre-action evidence does not distinguish candidates, GC-02 selects the best-matching candidate as a hypothesis and acts on it. Ambiguity is resolved post-action — if the expected transition occurs, the hypothesis was correct. If not, GC-02 rejects and may try another candidate or signal failure. GC-02 does not have a pre-action refusal path.

**Action Relationship:** Dispatch requires a bounded hypothesis, not confirmed identity. The system acts knowing the selection may be wrong. The action is the test. "I believe this is the target. Let me verify by acting."

**Post-Action Relationship:** Post-action observation is the identity verification step. Without it, identity is unconfirmed. The world transition IS the identity evidence.

**Failure Modes:**
- **False Positive Risk: MODERATE.** GC-02 may act on a wrong target. The risk is bounded by post-action verification — wrong targets are detected. But the action has already been dispatched. For reversible actions (navigation, scroll), this is acceptable. For irreversible actions (delete, purchase), it is not.
- **False Negative / Refusal Risk: LOW.** GC-02 almost always acts — refusal only occurs after repeated verification failures exhaust candidates. The risk is acting too readily, not refusing too often.

**Safety Interaction:** GC-02 MUST compose with safety gating. It must NOT act on a hypothesis for an irreversible or unsafe action. Safety authorization gates whether hypothesis-based action is permitted for this specific action type. For safe, reversible actions (navigation), hypothesis-based action is acceptable. For unsafe or irreversible actions, hypothesis-based action is rejected — GC-02 must fall back to pre-action discrimination or refusal.

**CP-11 Dependency:** MODERATE. GC-02 can detect wrong-target selections post-action (navigation to wrong page) even when type labels are wrong. But if CP-11 failures cause the wrong element to be selected AND the wrong element happens to lead to a page that matches the expected transition (unlikely but possible — e.g., both "Wi‑Fi" and "Wi‑Fi Calling" lead to pages with "Wi‑Fi" in the title), post-action verification may false-confirm.

**Architecture-Invariant Compatibility:**
- Agent owns completion (I-10): ✓ — grounding is pre-completion
- External world authoritative: ✓ — world transition is the authoritative identity signal
- Plan ≠ reality (I-5): ✓ — expected transition is from the plan; actual transition is from the world
- Recovery must be verified (I-9): potential interaction — wrong-target action is a form of disturbance

**What It Does NOT Solve:** Irreversible or unsafe actions where hypothesis-based dispatch is not permitted. Cases where two distinct targets produce visually identical world transitions (post-action verification false-confirms). The "tap first" approach is not universally admissible.

**Pass Oracle:** Target "Wi‑Fi." Two candidates: "Wi‑Fi" and "Wi‑Fi Calling" — both menu_item, indistinguishable pre-action. GC-02 selects "Wi‑Fi" (best text match). Tap dispatched. Post-action: Wi‑Fi settings page observed → identity CONFIRMED. Correct.

**Fail Oracle:** Target "Wi‑Fi." GC-02 selects "Wi‑Fi Calling" (wrong candidate, indistinguishable pre-action). Tap dispatched → Wi‑Fi Calling page. Post-action: page identity does NOT match expected Wi‑Fi settings page → identity REJECTED. The action was wrong but the rejection is correct. The fail oracle for GC-02 is: the system acts on a wrong target AND the post-action verification false-confirms (wrong page matches expected identity).

---

### GC-03 — Hypothesis-with-Fresh-Verification

**Candidate ID:** GC-03

**Title:** Provisional Target Hypothesis Verified or Rejected by Fresh World Observation

**Behavioral Thesis:** Target identity is established through a two-phase process: (1) a bounded hypothesis is formed from pre-action evidence — "candidate X is the most likely intended target because..." — with explicit confidence and stated limitations; (2) the hypothesis is tested by acting on the candidate and observing fresh world evidence. The hypothesis is CONFIRMED when fresh evidence is consistent with expectation. It is REJECTED when fresh evidence contradicts. It remains UNCONFIRMED when fresh evidence is ambiguous.

**RM-10 ER Coverage:**

| ER | Coverage | Explanation |
|---|---|---|
| ER-25 (evidence beyond text) | **FULL** | Hypothesis formation uses multiple evidence categories |
| ER-26 (type-interaction consistency) | **FULL** | Hypothesis must include type-interaction consistency check |
| ER-27 (post-selection verification) | **FULL** | Core mechanism — fresh observation confirms or rejects hypothesis |
| ER-28 (ambiguity refusal) | **FULL** | Explicitly refuses when hypothesis confidence is below threshold OR post-action evidence is ambiguous |

**Required Inputs:** Target description, observable element inventory, intended interaction type, expected world transition, confidence threshold.

**Evidence Used:** Pre-action: text, type, coordinates, spatial context — combined into a confidence-scored hypothesis. Post-action: fresh world state (page identity, element inventory, screen change).

**Decision Boundary:** A hypothesis is formed when pre-action evidence supports one candidate over others. The hypothesis is acted on only when confidence exceeds a threshold AND the action is safe for hypothesis-based dispatch. After action, fresh observation confirms, rejects, or leaves the hypothesis unconfirmed. If unconfirmed, the system must not treat the hypothesis as established.

**What establishes sufficient identity?** Identity is established when: (a) pre-action hypothesis confidence exceeds threshold, (b) the action is safe for hypothesis-based dispatch, AND (c) post-action fresh observation is CONSISTENT with the expected transition. All three must hold.

**Ambiguity Behavior:** Three distinct refusal paths:
1. Pre-action: hypothesis confidence below threshold → refuse, do not act
2. Pre-action: action is unsafe for hypothesis-based dispatch AND no candidate has distinguishing pre-action evidence → refuse, do not act
3. Post-action: fresh observation is INCONSISTENT with expected transition → reject hypothesis; if another candidate exists, may form new hypothesis; if no viable candidate remains → refuse, signal failure

**Action Relationship:** Dispatch requires a bounded hypothesis with explicit confidence and stated limitations. The system knows WHY it selected this candidate and WHAT would prove it wrong. Dispatch is not an assertion of identity — it's a test of a hypothesis.

**Post-Action Relationship:** Fresh observation is the verification step. The system explicitly compares observed world state to expected world state. The comparison is recorded — "expected Wi‑Fi Settings page, observed Wi‑Fi Calling page → hypothesis REJECTED." This is distinct from GC-02's "post-action observation IS the identity signal."

**Failure Modes:**
- **False Positive Risk: LOW-MODERATE.** Bounded by confidence threshold + safety gate + post-action verification. The risk is that all three layers agree but are wrong (hypothesis confidence high, action safe, post-action confirms — but the "confirmation" is a false match).
- **False Negative / Refusal Risk: MODERATE.** GC-03 may refuse when confidence is below threshold even though the best-guess candidate is correct. It may refuse when the action type is classified as unsafe for hypothesis dispatch even though the specific action is harmless.

**Safety Interaction:** GC-03 explicitly gates hypothesis-based dispatch on action safety. Safe actions (navigation) permit hypothesis-based dispatch. Unsafe actions require higher confidence or pre-action distinguishing evidence. This is a COMPOSITION — GC-03 does not define safety policy, it queries it.

**CP-11 Dependency:** MODERATE. Hypothesis confidence incorporates type label reliability. When type labels are known to be unreliable (CP-11 domain), hypothesis confidence is lower → more refusals. When type labels are reliable, confidence is higher → fewer refusals. GC-03 degrades gracefully with CP-11 failures rather than being broken by them.

**Architecture-Invariant Compatibility:**
- Agent owns completion (I-10): ✓
- External world authoritative: ✓ — fresh observation is the authority
- Plan ≠ reality (I-5): ✓ — expected transition is from plan; actual is from world
- Observation ≠ truth (I-4): ✓ — hypothesis can be wrong; fresh observation corrects it
- Recovery must be verified (I-9): potential interaction with wrong-target recovery

**What It Does NOT Solve:** Cases where pre-action confidence is below threshold, the action is unsafe for hypothesis dispatch, AND no alternative approach exists. The system refuses and the task cannot proceed — this is correct behavior (the task SHOULD NOT proceed when grounding is impossible), but it means some tasks will be refused that a human could perform (by using world knowledge not available to the system).

**Pass Oracle:** Target "Wi‑Fi." Two indistinguishable candidates. GC-03 forms hypothesis: "Wi‑Fi" (best text match, confidence 0.75, above threshold 0.7). Action type "navigation" is safe for hypothesis dispatch. Tap dispatched. Post-action: Wi‑Fi Settings page observed → hypothesis CONFIRMED. Identity established. Correct.

**Fail Oracle:** Same scenario. GC-03 forms hypothesis: "Wi‑Fi" (confidence 0.75). Action dispatched. Post-action: Wi‑Fi Calling page observed → hypothesis REJECTED. System records rejection reason. Does not repeat the action. May try alternative candidate or signal failure. The fail oracle is: the system continues to treat the rejected hypothesis as established identity despite contradictory fresh evidence. GC-03 prevents this.

---

### GC-04 — Ambiguity-Preserving Refusal

**Candidate ID:** GC-04

**Title:** Grounding Decision Gated by Candidate Set Collapse

**Behavioral Thesis:** The system maintains an explicit candidate set — all observable elements that plausibly match the target description. Target identity is established ONLY when the candidate set collapses to exactly one element through the application of distinguishing evidence. While the candidate set contains N > 1 elements, the system refuses to act. The system does not select — it eliminates, and acts only when elimination leaves exactly one candidate.

**RM-10 ER Coverage:**

| ER | Coverage | Explanation |
|---|---|---|
| ER-25 (evidence beyond text) | **FULL** | Requires distinguishing evidence for candidate elimination |
| ER-26 (type-interaction consistency) | **FULL** | Candidates inconsistent with intended interaction are eliminated |
| ER-27 (post-selection verification) | **NONE** | No post-action verification — acts only when candidate set is {1} |
| ER-28 (ambiguity refusal) | **FULL** | Core mechanism — refuses while N > 1 |

**Required Inputs:** Target description, observable element inventory, intended interaction type, elimination rules (type consistency, spatial constraints, prior history).

**Evidence Used:** Observable element properties used to ELIMINATE candidates, not to SELECT one. Each evidence category is an elimination filter: "candidates whose type is inconsistent with the intended interaction → eliminated"; "candidates outside the expected screen region → eliminated."

**Decision Boundary:** The candidate set starts as all elements matching the target description by text. Elimination rules are applied. If the set collapses to {1}, that candidate is the intended target. If the set remains {N > 1}, the system refuses. If the set becomes {}, all candidates were eliminated — the system signals "no viable target."

**What establishes sufficient identity?** Sufficient identity is established when exactly one candidate survives all elimination rules. The surviving candidate IS the intended target by process of elimination. No selection, no ranking, no "best match" — only elimination.

**Ambiguity Behavior:** The system NEVER selects from N > 1 candidates. It only acts when the candidate set is a singleton. This is the most conservative grounding approach — it refuses whenever elimination cannot reduce the set to one element.

**Action Relationship:** Dispatch requires candidate set = {1}. The system never acts on a hypothesis, never ranks candidates, never selects a "best" match. Action implies certainty-through-elimination.

**Post-Action Relationship:** Not applicable — GC-04 only acts when the candidate set is a singleton. Post-action verification is not part of the grounding decision (identity is already established by elimination). However, post-action verification may still be used for CP-02 (page-change verification) — it's just not part of CP-12 grounding.

**Failure Modes:**
- **False Positive Risk: VERY LOW.** The system only acts when one candidate remains. The risk is that an incorrect candidate survives elimination because the elimination rules failed to filter it out (e.g., spatial constraint too loose, type label wrong).
- **False Negative / Refusal Risk: VERY HIGH.** The system refuses whenever N > 1 candidates survive elimination. NC-05 (Wi‑Fi vs Wi‑Fi Calling) will cause refusal because both survive text matching AND both survive type consistency AND both may survive spatial constraints. The capability achieves safety at the cost of refusing many resolvable cases.

**Safety Interaction:** GC-04's refusal is about identity, not safety. A singleton candidate may still be unsafe. Safety gating is a separate, subsequent check.

**CP-11 Dependency:** HIGH. Elimination rules depend on type labels and OCR text being reliable enough to eliminate wrong candidates. Wrong type labels may cause incorrect elimination (correct candidate eliminated, wrong one survives) or incorrect survival (wrong candidate not eliminated).

**Architecture-Invariant Compatibility:**
- Agent owns completion (I-10): ✓
- External world authoritative: ✓ — elimination based on observed properties
- Plan ≠ reality (I-5): ✓

**What It Does NOT Solve:** Any case where N > 1 candidates survive all applicable elimination rules. NC-05 is the canonical example — Wi‑Fi and Wi‑Fi Calling survive text matching, type consistency, and spatial constraints. GC-04 refuses, which is correct behavior (it doesn't guess) but does not complete the task. GC-04 is a safety guarantee, not a completeness guarantee.

**Pass Oracle:** Target "Wi‑Fi." Candidates: "Wi‑Fi" (menu_item), "Wi‑Fi Calling" (menu_item), "Flash notifications" (text, contains "Wi‑Fi"? no — "notifications" doesn't contain "Wi‑Fi"). Wait — this is a different example. Let me use: Target "Wi‑Fi" on a screen with "Wi‑Fi" menu_item, "Wi‑Fi Calling" menu_item, and "Wi‑Fi Direct" text. Type-consistency elimination removes "Wi‑Fi Direct" (text type, inconsistent with navigation). "Wi‑Fi" and "Wi‑Fi Calling" both survive → set = {2}. GC-04 refuses. Behavior is correct per ER-28 but does not complete the task.

**Fail Oracle:** Same scenario. Spatial constraint eliminates "Wi‑Fi Calling" (it's at the wrong screen position). Set = {"Wi‑Fi"} → singleton. GC-04 acts. But the spatial constraint was wrong — "Wi‑Fi" is actually at a different position on this device/OS version. The correct candidate was eliminated; the wrong one survived. GC-04 acts on wrong target. The fail oracle is: incorrect elimination due to unreliable elimination rules.

---

### GC-05 — Staged Grounding Decision (Composed)

**Candidate ID:** GC-05

**Title:** Staged Target Grounding: Pre-Action Candidate Distinction → Bounded Hypothesis with Explicit Confidence → Fresh Post-Action Verification → Ambiguity Refusal

**Behavioral Thesis:** Target identity is established through sequential stages, each gated. Stage 1: apply elimination rules to the candidate set. If one candidate remains → identity established, dispatch. Stage 2: if N > 1 candidates remain, form a bounded hypothesis for the best-supported candidate with explicit confidence and stated limitations. Stage 3: if hypothesis confidence exceeds threshold AND the action type is safe for hypothesis dispatch, act on the hypothesis. Stage 4: observe fresh world state. If consistent with expected transition → hypothesis CONFIRMED, identity established. If inconsistent → hypothesis REJECTED. If ambiguous → identity UNCONFIRMED, refuse further action. Stage 5: if no candidate has sufficient pre-action evidence AND hypothesis dispatch is not permitted (unsafe action or below confidence threshold) → refuse.

**RM-10 ER Coverage:**

| ER | Coverage | Explanation |
|---|---|---|
| ER-25 (evidence beyond text) | **FULL** | Elimination uses multiple evidence categories; hypothesis formation uses multiple evidence categories |
| ER-26 (type-interaction consistency) | **FULL** | Applied at both elimination stage and hypothesis formation stage |
| ER-27 (post-selection verification) | **FULL** | Core Stage 4 — fresh observation confirms or rejects hypothesis |
| ER-28 (ambiguity refusal) | **FULL** | Stage 5 — explicit refusal when grounding cannot be established through any stage |

**Required Inputs:** Target description, observable element inventory, intended interaction type, expected world transition, elimination rules, confidence threshold, action safety classification.

**Evidence Used:** All categories from GC-01, GC-02, GC-03, and GC-04, applied at the appropriate stage.

**Decision Boundary:** The grounding decision proceeds through stages. Each stage has an explicit gate. The decision is complete when identity is established at any stage OR when all stages are exhausted and identity cannot be established (→ refuse).

**Stage flow:**

```
Candidate set = {elements matching target description}

Stage 1: ELIMINATION
  Apply elimination rules (type consistency, spatial constraints, prior history)
  If |set| == 1 → IDENTITY ESTABLISHED, dispatch
  If |set| == 0 → NO VIABLE TARGET, signal failure
  If |set| > 1  → proceed to Stage 2

Stage 2: HYPOTHESIS FORMATION
  Rank remaining candidates by distinguishing evidence
  Form bounded hypothesis for best-supported candidate
  Record confidence and stated limitations
  If confidence < threshold → proceed to Stage 5 (refuse)
  If confidence ≥ threshold → proceed to Stage 3

Stage 3: ACTION SAFETY GATE
  If action type is safe for hypothesis dispatch → dispatch, proceed to Stage 4
  If action type is NOT safe → proceed to Stage 5 (refuse)

Stage 4: POST-ACTION VERIFICATION
  Observe fresh world state
  If consistent with expected transition → IDENTITY CONFIRMED
  If inconsistent → REJECT hypothesis, return to Stage 2 with remaining candidates
  If ambiguous → IDENTITY UNCONFIRMED, refuse further action

Stage 5: REFUSAL
  Signal "grounding incomplete: N candidates, insufficient distinguishing evidence"
  Do not dispatch
```

**What establishes sufficient identity?** Identity is established at EXACTLY TWO points: Stage 1 (exactly one candidate survives elimination) OR Stage 4 (hypothesis confirmed by post-action fresh observation). At all other points, identity is either unconfirmed or refused.

**Ambiguity Behavior:** Ambiguity is not an error — it's an expected state. The system explicitly tracks ambiguity through the candidate set size. At Stage 1, N > 1 is ambiguity. At Stage 2, confidence below threshold is ambiguity. At Stage 4, post-action observation inconsistent with expectation is a wrong-hypothesis detection. The system refuses at Stage 5 only when all previous stages have been exhausted without establishing identity.

**Action Relationship:** Two distinct dispatch paths:
1. Stage 1 dispatch: "I KNOW this is the target" — candidate set collapsed to {1}
2. Stage 3 dispatch: "I BELIEVE this is the target, and here's why, and here's what would prove me wrong" — bounded hypothesis

The system records WHICH path authorized the dispatch.

**Post-Action Relationship:** Post-action verification is required for Stage 3 dispatches. For Stage 1 dispatches, post-action verification is not required for grounding (identity was already established by elimination), but may still be performed for CP-02 (page-change verification).

**Failure Modes:**
- **False Positive Risk: LOW.** Stage 1 dispatch: risk is incorrect elimination (same as GC-04). Stage 3 dispatch: risk is hypothesis confirmed by false-matching post-action observation. The overall risk is lower than any single-stage approach because each stage gates the next.
- **False Negative / Refusal Risk: MODERATE.** Refusal occurs when: (a) elimination leaves N > 1 AND (b) hypothesis confidence is below threshold OR action is unsafe for hypothesis dispatch. This is more permissive than GC-04 (acts on hypothesis when safe) but more conservative than GC-02 (refuses when unsafe or low confidence).

**Safety Interaction:** Stage 3 explicitly gates hypothesis dispatch on action safety. This is the key composition point — the grounding capability queries the safety policy before dispatching on a hypothesis.

**CP-11 Dependency:** MODERATE. Stage 1 elimination depends on type labels being reliable for elimination. Stage 2 hypothesis confidence incorporates type label reliability. Stage 4 post-action verification can detect wrong-target selections even when type labels were wrong (navigation to unexpected page). The staged approach degrades gracefully — CP-11 failures shift more decisions from Stage 1 to Stage 3, or from Stage 3 to Stage 5 (refusal).

**Architecture-Invariant Compatibility:**
- Agent owns completion (I-10): ✓
- External world authoritative: ✓ — all stages use observed world evidence
- Plan ≠ reality (I-5): ✓ — expected transition from plan, actual from world
- Observation ≠ truth (I-4): ✓ — hypothesis can be wrong
- Recovery must be verified (I-9): ✓ — wrong-target recovery is a recovery event
- Completion requires Goal Evidence (I-10): ✓ — grounding is pre-completion

**What It Does NOT Solve:** Universal grounding. GC-05 provides a bounded, staged approach that establishes identity when possible and refuses when not. It does not guarantee that every target can be grounded — some targets may be ungroundable with the available evidence. It does not solve CP-11 (perception reliability). It does not define the safety policy (it queries it).

**Pass Oracle:** Any of:
- Stage 1 pass: candidate set collapses to {1} → correct dispatch
- Stage 4 pass: hypothesis dispatched, post-action confirms → identity established

**Fail Oracle:** Any of:
- Stage 1 fail: wrong candidate survives elimination → wrong dispatch
- Stage 4 fail: hypothesis confirmed by false-matching post-action → identity incorrectly established
- Stage 5 fail: system refuses when grounding WAS possible (false negative — safer than false positive)

---

## Negative-Control Matrix

| NC | GC-01 Pre-Action | GC-02 Expected-Effect | GC-03 Hypothesis-Verify | GC-04 Elimination | GC-05 Staged |
|---|---|---|---|---|---|
| **NC-01** Same type, similar text (Wi‑Fi / Wi‑Fi Calling) | Depends on distinguishing property existing. If spatial context distinguishes → passes. If not → refuses. | Selects best text match, acts, verifies post-action. If Wi‑Fi selected → correct. If Wi‑Fi Calling selected → post-action catches it. | Forms hypothesis with confidence. If both indistinguishable → confidence below threshold → refuses. If one slightly better → acts, verifies post-action. | Both survive elimination → set = {2} → refuses. Never acts. | Stage 1: both survive → N=2. Stage 2: forms hypothesis. Stage 3: if safe, dispatches. Stage 4: verifies. |
| **NC-02** Substring collision (Network_1 / Network_10) | Text matching must be identity-match, not substring. If exact match → only Network_1 matches "Network_1". Passes. | Same as GC-01 for pre-action. Post-action catches wrong selection. | Hypothesis formation must use identity match, not substring. Same as GC-01. | Elimination: Network_10 eliminated by non-matching text (substring ≠ identity). Set = {1} → passes. | Stage 1: identity-match elimination eliminates Network_10. Set = {1} → passes at Stage 1. |
| **NC-03** Correctly typed wrong target | Distinguishing property may not exist if both are same type. Refuses or wrong selection. | Acts on hypothesis, post-action verifies. Wrong target detected post-action. | Hypothesis + post-action verification. Wrong target detected. | Both survive type elimination → set = {2} → refuses. | Stage 1: N=2. Stage 2-4: hypothesis → verify → confirm or reject. |
| **NC-04** Coordinate proximity | Coordinates are one distinguishing property among others. Not sole identity proof. | Coordinates used for candidate ranking, not identity. Post-action is the identity proof. | Coordinates contribute to hypothesis confidence, not identity. | Coordinates used for elimination, not selection. | Coordinates used at elimination and hypothesis stages. Never sole identity proof. |
| **NC-05** Insufficient evidence | **REFUSES.** Correct behavior. Cannot distinguish → does not act. | **ACTS.** Selects best match, dispatches. Risky for unsafe actions. | **REFUSES** if confidence below threshold. **ACTS** if confidence ≥ threshold and action safe. | **REFUSES.** Set = {N>1} → never acts. | **REFUSES** at Stage 5 if no stage establishes identity. **ACTS** at Stage 3 if hypothesis dispatch safe. |
| **NC-06** Expected effect disagreement | Not applicable — GC-01 doesn't act when uncertain. | **DETECTS.** Post-action observation contradicts → selection rejected. May retry other candidate. | **DETECTS.** Fresh observation contradicts → hypothesis rejected. Records rejection reason. | Not applicable. | **DETECTS** at Stage 4. Hypothesis rejected. Returns to Stage 2 if candidates remain. |

---

## RM-10 ER Coverage Matrix

| Candidate | ER-25 Evidence Beyond Text | ER-26 Type-Interaction Consistency | ER-27 Post-Selection Verification | ER-28 Ambiguity Refusal |
|---|---|---|---|---|
| **GC-01** Pre-Action Discriminative | FULL | FULL | NONE | FULL |
| **GC-02** Expected-Effect Identity | FULL | FULL | FULL | PARTIAL |
| **GC-03** Hypothesis-Verify | FULL | FULL | FULL | FULL |
| **GC-04** Elimination Refusal | FULL | FULL | NONE | FULL |
| **GC-05** Staged (Composed) | FULL | FULL | FULL | FULL |

**Only GC-03 and GC-05 satisfy all four ERs at FULL coverage.**

---

## Candidate Rejections

### GC-01 — REJECT_INSUFFICIENT

**Reason:** Does not satisfy ER-27 (post-selection verification). GC-01 refuses when candidates cannot be distinguished pre-action — this correctly satisfies ER-28 (ambiguity refusal) but leaves the system unable to handle cases where the only distinguishing property is the navigation target (NC-05). These cases are common in GUI navigation. GC-01 would refuse on many legitimate tasks. The capability is safe but incomplete.

### GC-02 — REJECT_SAFETY_CONFLICT

**Reason:** Does not satisfy ER-28 (ambiguity refusal) at FULL coverage. GC-02 acts on a hypothesis for ALL actions, including irreversible ones. The "tap first, verify later" approach is not universally admissible. While GC-02 can be composed with safety gating externally, the capability itself does not gate hypothesis dispatch on action safety. This is a semantic gap — GC-02 assumes all actions are safe for hypothesis dispatch, which is not true in the external world.

### GC-04 — REJECT_INSUFFICIENT

**Reason:** Does not satisfy ER-27 (post-selection verification). GC-04 only acts when the candidate set is a singleton — it never reaches the "where identity remains materially uncertain" state that ER-27 addresses. While GC-04's refusal behavior is correct (ER-28), its refusal rate is prohibitive for GUI navigation tasks where text-ambiguous candidates are common. GC-04 would refuse on the "Wi‑Fi" / "Wi‑Fi Calling" case, which is a routine navigation task. The capability is safe but too restrictive.

---

## Minimality Analysis

### GC-03 vs GC-05 — Is the Composition Minimal?

GC-03 (Hypothesis-Verify) satisfies all four ERs at FULL coverage. GC-05 (Staged) adds Stage 1 elimination as a distinct pre-hypothesis step. The question: does Stage 1 elimination add semantic value beyond what GC-03 already provides?

**Argument for keeping Stage 1 (GC-05 over GC-03):**

Stage 1 elimination and Stage 2 hypothesis formation are semantically distinct:
- **Elimination** says: "This candidate CANNOT be the target because [property inconsistent with intended interaction]." It reduces the candidate set by removing impossible candidates.
- **Hypothesis formation** says: "Among the remaining candidates, this one is the MOST LIKELY target because [distinguishing evidence]." It ranks possible candidates.

These are different operations with different evidence requirements:
- Elimination requires a property that is NECESSARY for the intended target (if missing → cannot be the target).
- Hypothesis formation requires a property that is DIFFERENTIATING among candidates (if present in one but not others → more likely).

Merging them into a single "hypothesis formation with confidence" step loses the elimination semantics: "this candidate is IMPOSSIBLE" is stronger than "this candidate has LOW confidence." A candidate eliminated by type inconsistency is categorically wrong; a candidate with low hypothesis confidence might still be correct.

**Argument for removing Stage 1 (GC-03 is sufficient):**

GC-03's hypothesis formation can incorporate elimination-style reasoning through confidence scoring. A candidate eliminated by type inconsistency would receive confidence = 0, which is below any reasonable threshold. The distinction between "confidence = 0 (eliminated)" and "confidence = 0.1 (very unlikely)" is not semantically meaningful for the grounding decision — both result in the candidate not being selected.

**Minimality verdict:** The Stage 1/Stage 2 distinction in GC-05 is conceptually valid but **not semantically required** for satisfying RM-10. GC-03 achieves equivalent behavior through hypothesis confidence scoring — eliminated candidates have confidence = 0. The additional stage in GC-05 adds conceptual clarity but not semantic necessity. **GC-03 is the minimal capability that satisfies all four ERs.**

### GC-05 Action Safety Gate — Is It Part of CP-12 or External?

GC-05's Stage 3 (action safety gate for hypothesis dispatch) explicitly queries the safety policy. This is a COMPOSITION with an external capability, not a new semantic element within CP-12. GC-03 achieves the same composition by stating "the action must be safe for hypothesis dispatch" as a precondition without specifying the mechanism.

The action safety gate is **required for correctness** (you cannot hypothesis-dispatch irreversible actions) but it is **not part of the CP-12 capability** — it is a dependency on the existing safety authorization capability. Both GC-03 and GC-05 require it; GC-05 makes the composition explicit.

---

## Composition Analysis

**Should pre-action disambiguation, ambiguity refusal, and post-action verification be three separate capabilities or phases of one capability?**

**Decision: Phases of one indivisible grounding decision.**

Rationale:
- They share the same decision target: "is this element the intended target?"
- They operate on the same evidence: the candidate set
- They are sequentially dependent: post-action verification only applies when pre-action evidence was insufficient; refusal only applies when both pre-action and post-action are insufficient
- Separating them would create three capabilities that each make partial decisions about the same semantic question — this fragments authority and creates coordination gaps

The three phases are stages of ONE decision, not three decisions. The capability is "target grounding." Its internal structure has phases; its external interface is a single decision: "identity established → dispatch" or "identity not established → refuse."

---

## Ownership / Authority Compatibility

All candidates are compatible with existing ownership/authority:

| Invariant | Impact |
|---|---|
| Agent owns completion (I-10) | Target grounding is pre-completion action selection; does not affect completion authority |
| External world authoritative | All evidence sources are world observations; no plan-derived identity |
| Plan ≠ reality (I-5) | Target description from plan is a search key; identity from world observation |
| Observation ≠ truth (I-4) | Hypothesis can be wrong; fresh observation corrects |
| Container owns local page state | Grounding operates within Container's current observation |
| Traversal owns step execution | Grounding is part of step execution (target selection before dispatch) |
| Recovery must be verified (I-9) | Wrong-target selection is a recoverable event |

**No ARCHITECTURE_GATE_REQUIRED for any candidate.** All can operate within existing authority boundaries.

---

## Recommended Capability Candidate

**GC-03 — Hypothesis-with-Fresh-Verification**

### Rationale

1. **Satisfies all four RM-10 ERs at FULL coverage.** GC-03 is the only single-stage candidate that does so. GC-05 also satisfies all four but is not minimal — its Stage 1/Stage 2 distinction adds conceptual clarity without semantic necessity.

2. **Minimum semantic purchase.** GC-03 adds exactly two concepts to the current Runtime: (a) a bounded hypothesis with explicit confidence and stated limitations, and (b) fresh post-action observation used to confirm or reject the hypothesis. These are the minimum concepts needed to bridge the gap between "best text match → tap → hope" and "grounded identity evidence."

3. **Honest about uncertainty.** GC-03 does not pretend to have certainty when evidence is insufficient. It records WHY a candidate was selected (hypothesis confidence, supporting evidence) and WHAT would prove it wrong (expected post-action world state). This is the foundation for trustworthy grounding.

4. **Degrades gracefully with CP-11 failures.** When type labels are unreliable, hypothesis confidence is lower → more refusals or more post-action rejections. The capability does not break when perception is imperfect — it becomes more conservative.

5. **Composes with safety authorization.** GC-03 explicitly requires that hypothesis dispatch be gated on action safety. This is a dependency, not a deficiency — the capability acknowledges its boundary.

6. **All negative controls addressed.** NC-01 through NC-06 are handled through hypothesis confidence, post-action verification, and refusal paths.

### Why Not GC-05?

GC-05 is semantically equivalent to GC-03 plus an explicit elimination stage. The elimination stage adds conceptual clarity (distinguishing "impossible" from "unlikely") but is not semantically required — GC-03 achieves equivalent behavior through confidence = 0 for eliminated candidates. GC-05 is the "explained" version of GC-03; GC-03 is the "minimal" version.

For the architecture challenge (next phase), GC-05's explicit staging may be more useful as a specification — it separates concerns that may map to different Runtime mechanisms. But for the capability SEMANTICS, GC-03 is sufficient and minimal.

**Recommendation: Carry GC-03 forward. Optionally reference GC-05's staging as an implementation guidance note (non-normative).**

---

## Remaining Semantic Uncertainty

**NONE** — at the capability semantics level.

GC-03 fully defines:
- What evidence is required (pre-action observable properties + post-action fresh world state)
- When identity is sufficiently supported (hypothesis confidence ≥ threshold + post-action confirmation OR candidate set collapsed to {1})
- How ambiguity is handled (refusal when confidence < threshold or post-action ambiguous)
- How post-action evidence revises grounding (confirms, rejects, or leaves unconfirmed)

The remaining uncertainties are IMPLEMENTATION questions, not semantic questions:
- What specific confidence threshold? (implementation parameter)
- What specific observable properties beyond text/type/coordinates? (implementation design)
- How is "expected world transition" specified? (interface design, not capability semantics)

These are deferred to the architecture challenge.

---

## Architecture Gate Needed

**NO** — for the capability semantics.

GC-03 can be expressed within existing ownership/authority boundaries. The Architecture Gate may be needed later when translating GC-03's behavioral semantics into Runtime mechanisms, but the capability SEMANTICS do not require ownership/authority changes.

---

## Recommendation

**ONE_MINIMAL_CAPABILITY_CANDIDATE_READY_FOR_ARCHITECTURE_CHALLENGE**

**Selected candidate:** GC-03 — Hypothesis-with-Fresh-Verification

**Summary:** The system forms a bounded hypothesis about which candidate is the intended target, with explicit confidence and stated limitations. The hypothesis is tested by acting on the candidate (gated by action safety) and observing fresh world evidence. The hypothesis is CONFIRMED when evidence is consistent, REJECTED when contradictory, and left UNCONFIRMED when ambiguous. The system refuses to act when hypothesis confidence is below threshold, when the action is unsafe for hypothesis dispatch, or when post-action evidence is ambiguous.

**This is the minimum behavioral capability that satisfies RM-10 ER-25..ER-28 at FULL coverage.**

## Explicit Non-Actions

- No implementation — behavioral semantics only
- No architecture commitment — no component design
- No Runtime modification
- No model/provider selection
- No algorithm specification
- No threshold values (implementation parameters)

## Next Task

**CP12_ARCHITECTURE_CHALLENGE** — translate GC-03 behavioral semantics into architecture constraints, Scenario Contracts, and verifiable acceptance criteria. Then Candidate generation (Phase D proper) for HOW to implement GC-03 within Runtime architecture.

Recommended only. Do NOT execute.

## Repository Changes

`docs/decisions/cp12-capability-candidate-generation-result.md` — created. No other files modified.

STOP.
