# Post-S0 Reality-Grounded Usability Roadmap

> Status: Canonical post-S0 planning baseline | Date: 2026-08-09
> Scope: Transition from Architecture Discovery to Reality-Grounded Usability Development. Preserves S0 architecture/semantic discipline while progressing toward a genuinely usable autonomous GUI Agent.
> Authority boundary: this roadmap freezes phase order, goals, gates, exit criteria, authority boundaries, and the evidence/usability maturity distinction. It does NOT freeze Reality Model count, grounding/Planner architecture, graph/stack/frontier structures, perception implementation, model/provider selection, U2/U3 internals, or future Candidates. It does not replace the Architecture Contract, approved OpenSpec SHALL, frozen capability closeouts, task authorization, or existing Human Gates.

## 1. Development Thesis

UniClaw graduates from **Architecture Discovery** to **Reality-Grounded Usability Development** along two independent axes.

```text
EVIDENCE MATURITY          PRODUCT USABILITY MATURITY
S0 deterministic synthetic U0 structured task, controlled world
S1 recorded-reality replay U1 one bounded NL GUI task end-to-end (emulator)
S2 production-shaped percep U2 open-world Settings traversal from intent
S3 live emulator           U3 multiple task families + variation/disturbance
Future: real-device         Future: reliable autonomous GUI Agent
```

**Freezed principle:** S1 → S2 → S3 is NOT a mandatory serial product roadmap. Evidence maturity is **pulled by usability blockers**. A capability may stay at S0 evidence while usability progresses, and an evidence upgrade happens only when a concrete usability blocker requires it.

## 2. Development Strategy

```text
Canonical Pressure
+ Accepted Reality Model
+ Minimum Usable Vertical Slice
→ development
```

- **Canonical Pressure** defines what semantic truth the system must respect (14-CP unified portfolio).
- **Reality Model** defines what real-world structure, ambiguity, disturbance and failure the system must survive (Phase B corpus — implementation-independent).
- **Usability Slice** defines what end-to-end product behavior must actually work (Phase E — U1 slice).

Operating loop (frozen):

```text
usable target
→ execute
→ identify highest-value blocker
→ challenge that blocker
→ buy minimum necessary capability
→ validate against relevant Reality Models
→ execute again
```

**Do NOT return to open-ended architecture mining.** All further work is blocker-pulled and tied to a canonical pressure + accepted Reality Model + usability blocker.

## 3. Evidence Maturity Track

| Level | Name | Required evidence | Authority |
|---|---|---|---|
| `S0` | Deterministic synthetic world | Synthetic deterministic external-world evidence: positive, negative/disturbance, replay proof | Achieved — `S0_GRADUATED` (2026-08-09) |
| `S1` | Recorded-reality replay | Recorded legacy/emulator/real-world evidence replaying the same reality pressure | Requires HUMAN `PROJECT_LEADER_S1_AUTHORIZATION` |
| `S2` | Production-shaped perception/grounding | Offline integration with production-shaped Observation parsing, grounding, semantic evidence; device I/O controlled | Requires HUMAN `PROJECT_LEADER_S2_AUTHORIZATION` (analogous) |
| `S3` | Live emulator | Same high-value capability executed against a live emulator | Requires HUMAN `PROJECT_LEADER_S3_AUTHORIZATION` (analogous) |
| Future | Real-device reliability | Real-device execution | Deferred; no design frozen |

S1/S2/S3 are not mandatory serial milestones. Each is entered only when a usability blocker (Phase F) demonstrates the evidence gap is the binding constraint.

## 4. Usability Maturity Track

| Level | Name | Definition | Status |
|---|---|---|---|
| `U0` | Structured task, controlled world | Structured task against controlled Runtime world — the 13 frozen S0 scenarios + Capstone | ACHIEVED (`S0_GRADUATED`) |
| `U1` | One bounded NL GUI task end-to-end | "确保 WiFi 已开启" works end-to-end on Android Emulator via the full chain (NL → intent → goal → execution → discovery → grounding → action → verification → recovery → GoalEvidence → honest completion) | NOT_STARTED (Phase E) |
| `U2` | Open-world Settings traversal | "遍历 Settings 中深度 <= N 的所有安全配置项" from high-level task intent; type-level traversal spec + runtime-discovered inventory + boundaries + branch progress + honest completion + visual grounding + recovery | NOT_STARTED (Phase H) |
| `U3` | Multi-family variation + disturbance | Multiple GUI task families with UI variation, observation noise, alternate routes, ambiguity, popups, external drift, timeout, action uncertainty, longer horizon, recovery complexity | NOT_STARTED (Phase H; implementation architecture NOT defined now) |
| Future | Reliable autonomous GUI Agent | — | Not planned at this roadmap horizon |

## 5. Phase Registry

### PHASE_A_CURRENT_SEMANTIC_CLOSEOUT — **COMPLETE**

**Name:** `PHASE_A_CURRENT_SEMANTIC_CLOSEOUT`

**Scope:** Close remaining bounded semantic/spec work already identified.

**Item:** CP-06 — Goal satisfaction without execution (`GoalExpression != GoalState`).

**Repository truth (2026-08-09):** **FULLY_CLOSED.** The original SPECIFICATION_GAP (empty plan + initially-satisfied Goal → `Failed("Plan 步数耗尽")`) is fixed and subsequently generalized: the `Agent.cs` pre-loop GoalEvidence evaluation is now unconditional (plan-length-independent, per `HUMAN_AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL`). Both branches proven:

- **Empty plan + initially satisfied:** 0 Plan-step dispatches, Completed from seq=2 evidence (Assertion6/Assertion7).
- **Non-empty plan + initially satisfied:** 0 Plan-step dispatches, Completed from seq=2 evidence (Assertion8).
- **Negative controls:** initially unsatisfied + empty → Failed (Assertion7); unsatisfied + non-empty → normal execution (Assertion9).

Classification: `SEMANTIC_CORRECTION_WITHIN_EXISTING_CP06`. Full suite 415/415 PASS; architecture guards 8/8; build 0 warnings 0 errors; deterministic replay 22/22. Production semantic delta = 0 (no new types, fields, enums, or authority). Ownership/Authority Delta = NONE. Plan-length-independent initial GoalEvidence authority is proven — Plan existence does not create an obligation to act.

Records: `docs/decisions/cp-06-spec-reconciliation-result.md`, `docs/decisions/cp-06-nonempty-initial-goal-repair-result.md`, `docs/decisions/cp-06-initial-goal-semantic-gate.md`.

**Entry Conditions:** `S0_GRADUATED`; CP-06 classified `SPECIFICATION_GAP` in Step 6; `HUMAN_AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL` (2026-08-09).

**Primary Deliverable:** CP-06 FULLY_CLOSED with non-vacuous executable proof (both empty and non-empty branches).

**Exit Criteria:**
- CP-06 FULLY_CLOSED;
- initial GoalEvidence behavior proven non-vacuously for all Plan lengths;
- zero unnecessary Plan-step dispatch when the initial world already satisfies Goal (empty-plan: Assertion6; non-empty: Assertion8);
- full tests pass (415/415);
- architecture guards pass (8/8);
- semantic delta accepted (SEMANTIC_CORRECTION_WITHIN_EXISTING_CP06);
- ownership unchanged; authority unchanged.

**Human Gate:** `HUMAN_AUTHORIZE_PLAN_LENGTH_INDEPENDENT_INITIAL_GOAL` — APPROVED (2026-08-09).

**Dependencies:** Unified 14-CP portfolio; Step 6 challenge classification; CP-06 reconciliation; CP-06 Semantic Gate.

**Explicit Non-Goals:**
- No new Goal model; no new completion authority; no new Planner; no new mutable state; no ownership change; no authority change; no architecture change; no Intent → Goal synthesis.
- No new types, fields, enums, or authority.

### PHASE_B_REALITY_MODEL_FOUNDATION — **READY**

**Name:** `PHASE_B_REALITY_MODEL_FOUNDATION`

**Purpose:** Convert the already-mined legacy evidence corpus into a small, reliable, implementation-independent corpus of Reality Models.

**B1 — `REALITY_MODEL_ADMISSION_CONTRACT`:** Define World Fact / Reality Inference / Reality Model, provenance requirements, evidence strength, minimization, deduplication, independent validation, admission authority.

**B2 — `LEGACY_REALITY_MODEL_EXTRACTION`:** Extract from the already-filtered evidence corpus ONLY. No new broad mining.

**B3 — `INDEPENDENT_REALITY_MODEL_VALIDATION`:** Extractor must not approve its own models.

**B4 — `REALITY_MODEL_ADMISSION`:** Outcomes: `ACCEPT_NEW_MODEL` | `MERGE_EXISTING_MODEL` | `ADD_VARIANT` | `ADD_EVIDENCE` | `DEFER` | `REJECT_LEGACY_MECHANISM`.

**Frozen principle:** Reality != legacy interpretation; Reality != Runtime belief; Reality != AI assertion. Reality Models are evidence-supported minimal models of world facts, observations and transitions.

**Entry Conditions:** Evidence corpus mined and filtered (Steps 1–6 + visual + traversal supplements); no open blocking semantic item.

**Primary Deliverable:** Accepted Reality Model corpus + frozen Admission Contract.

**Exit Criteria:**
- Admission Contract frozen;
- accepted Reality Model corpus exists;
- each accepted model has provenance;
- facts and inference separated;
- legacy implementation mechanisms excluded from normative model;
- corpus minimized and deduplicated;
- independent validation complete.

**Human Gate:** `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` — required because admission rules set evidence authority (a gated change). Extraction, deduplication, and candidate validation under frozen rules require no approval.

**Dependencies:** NONE beyond the frozen evidence corpus (Phase A not required).

**Explicit Non-Goals:** No new broad legacy mining; no Runtime changes; no new semantics; no Candidates; no evidence authority change without gate.

### PHASE_C_PRESSURE_REALITY_MATRIX — **NOT_STARTED**

**Name:** `PHASE_C_PRESSURE_REALITY_MATRIX`

**Purpose:** Map the 14 canonical pressures against accepted Reality Models: Canonical Pressure × Reality Model × Evidence Maturity.

**Classifications:** `PROVEN` | `REPLAY_NEEDED` | `PRODUCTION_PERCEPTION_NEEDED` | `LIVE_EMULATOR_NEEDED` | `SEMANTIC_CHALLENGE_NEEDED` | `NOT_APPLICABLE`.

**Ranking criteria:** 1. false-success severity; 2. usability relevance; 3. safety/recovery relevance; 4. evidence quality; 5. dependency.

**Entry Conditions:** Accepted Reality Model corpus exists (Phase B complete).

**Primary Deliverable:** `PRESSURE_REALITY_MODEL_MATRIX_READY` — full matrix with ranked combinations.

**Exit Criteria:** `PRESSURE_REALITY_MODEL_MATRIX_READY`.

**Human Gate:** NONE (analysis only; a `SEMANTIC_CHALLENGE_NEEDED` classification routes the challenge itself through the Semantic Gate path, it does not bypass it).

**Dependencies:** Phase B.

**Explicit Non-Goals:** No capability purchases; no classification overrides a frozen closeout; matrix is a planning input only.

### PHASE_D_CP12_TARGET_GROUNDING_CHALLENGE — **NOT_STARTED**

**Name:** `PHASE_D_CP12_TARGET_GROUNDING_CHALLENGE`

**Canonical pressure:** CP-12 — Target Grounding. **Primary distinction:** Coordinate/Text Match != Semantic Target Identity.

**Core question:** When multiple observed candidates plausibly match the task, can the current Runtime represent and verify that the semantically correct target was selected?

**Evidence inputs:** accepted Reality Models covering: same/overlapping text; substring collision (`"Network_1" ⊆ "Network_10"`); multiple navigable candidates; wrong element type; search-box misclassification; coordinate drift (golden tolerance 0.08–0.1); observation-source disagreement.

**Allowed classifications:** `ALREADY_COVERED` | `SPECIFICATION_GAP` | `BEHAVIOR_GAP` | `COMPOSITION_GAP` | `S2_EVIDENCE_GAP` | `SEMANTIC_MODEL_GAP` | `ARCHITECTURE_PRESSURE`.

**Entry Conditions:** Accepted Reality Models covering grounding cases (Phase B); Phase C matrix (optional context).

**Primary Deliverable:** CP-12 formally classified with a bounded next action.

**Exit Criteria:** CP-12 classification recorded; next action bounded. No `GroundingEngine` pre-design.

**Human Gate:** Conditional — a semantic/ownership/authority-affecting classification (e.g., `SPECIFICATION_GAP` requiring new semantics) requires a Semantic Gate; an `S2_EVIDENCE_GAP` classification requires `PROJECT_LEADER_S2_AUTHORIZATION` when pursued.

**Dependencies:** Phase B (accepted Reality Models); optionally Phase C.

**Explicit Non-Goals:** No GroundingEngine design; no perception implementation; no new Candidates.

### PHASE_E_MINIMUM_USABLE_AGENT_SLICE — **NOT_STARTED**

**Name:** `PHASE_E_MINIMUM_USABLE_AGENT_SLICE`

**Target task (U1):** User intent **"确保 WiFi 已开启"** on Android Emulator.

**Slice definition:** `MINIMUM_USABLE_AGENT_SLICE_001` — must eventually exercise the complete chain:

```text
Natural Language → Intent interpretation → Goal / scope / constraints
→ execution representation → startup / environment verification
→ fresh observation → page understanding → open-world work discovery
→ target grounding → action → fresh observation → result verification
→ recovery when required → GoalEvidence → honest completion
```

**Required behaviors:**
1. Natural-language input accepted.
2. Goal reflects desired world state: WiFi == ON.
3. Execution must not require a hard-coded full future route.
4. Runtime may discover concrete work from fresh observations.
5. Correct WiFi target must be grounded.
6. If WiFi is already ON: zero unnecessary mutation; complete from evidence.
7. If WiFi is OFF: use desired-effect action; fresh observation; verify ON.
8. Completion only from GoalEvidence.
9. One bounded disturbance eventually supported: popup OR launcher drift.

**Entry Conditions:** CP-12 classified (Phase D) so the grounding requirement of behavior 5 is known and bounded; Android Emulator available for the execution leg; Phase F may pull S1/S2/S3 evidence per blockers.

**Primary Deliverable:** `MINIMUM_USABLE_AGENT_SLICE_001` executed end-to-end on emulator.

**Exit Criteria:** U1 PASS — behaviors 1–9 demonstrated; zero unnecessary mutation when already ON; completion only from GoalEvidence; one bounded disturbance handled; honest failure path (Plan exhaustion ≠ completion).

**Human Gate:** `HUMAN_AUTHORIZE_MINIMUM_USABLE_SLICE_001` — approves the bounded NL→Goal interpretation for the slice (a new product interpretation of Intent/Goal) and the emulator integration leg.

**Dependencies:** Phase D (grounding requirement bounded); Phase F (blocker-driven evidence pulls).

**Explicit Non-Goals:** Hard-coded full UI route; fake world success; Plan exhaustion as completion; text-only target identity; action dispatch as proof of world change; replay-specific production shortcuts; generic Planner.

### PHASE_F_BLOCKER_DRIVEN_MATURITY — **NOT_STARTED**

**Name:** `PHASE_F_BLOCKER_DRIVEN_MATURITY`

**Purpose:** Once U1 execution begins, do NOT require completion of entire S1/S2/S3 before moving forward. Use:

```text
U1 blocker → choose minimum evidence/capability advancement required
```

Examples (mapping, not a queue):
- Recorded-world behavioral uncertainty → bounded S1 replay.
- Perception/grounding uncertainty → bounded S2 production-shaped evidence.
- Real environment/action/latency issue → bounded S3 emulator integration.
- Intent/Goal representation deficiency → semantic challenge.

**Entry Conditions:** U1 execution begins (Phase E).

**Primary Deliverable:** Per-blocker evidence/capability advancement decisions, each justified by (Canonical Pressure + Reality Model + Usability Blocker).

**Exit Criteria:** Ongoing discipline, not a terminal gate. Every advancement tied to its pressure/model/blocker; no evidence work without one.

**Human Gate:** Evidence-maturity advancement beyond S0 requires the corresponding existing HUMAN authorization (`PROJECT_LEADER_S1_AUTHORIZATION`, S2/S3 analogues). Semantic purchases route through the Semantic Gate.

**Dependencies:** Phase E start.

**Explicit Non-Goals:** No evidence work without a concrete pressure or usability reason; no wholesale S1/S2/S3 completion requirement before U1.

### PHASE_G_INTENT_GOAL_PLAN — **NOT_STARTED**

**Name:** `PHASE_G_INTENT_GOAL_PLAN`

**Primary canonical pressure:** CP-14 (`TaskIntent != ExecutionMethod`).

**Frozen evidence-backed finding:** Both are legitimate task classes and are NOT interchangeable:
- A. CLOSED-WORLD CONCRETE PLAN (stable world, known route — TE-02).
- B. OPEN-WORLD TYPE-LEVEL TRAVERSAL SPECIFICATION (world partially unknown/variable — TE-01/TE-03/TE-08).

For open-world traversal, pre-execution knowledge may include only: task scope; candidate category/type; safety constraints; depth bound; completion requirement — while concrete elements/pages/routes/work inventory are discovered from fresh reality.

**Do NOT reduce planning to:** Natural Language → fixed step list.

**Investigate:** the actual product transformation `User Intent → Goal → scope/constraints → execution representation → reality-grounded concrete work`; different tasks may select different execution representations.

**Entry Conditions:** U1 exercises the chain (Phase E), producing evidence of the intent→execution gap; open-world discovery evidence (E behaviors 3–5).

**Primary Deliverable:** ONE bounded Intent→Goal/execution-specification path proven for U1/U2.

**Exit Criteria:** one bounded path proven; CP-14 finding preserved (both classes legitimate, not interchangeable).

**Human Gate:** `HUMAN_AUTHORIZE_INTENT_GOAL_PATH` — new major product interpretation of Intent/Goal.

**Dependencies:** Phase E; optionally Phase H (U2 path).

**Explicit Non-Goals:** No generic Planner pre-design; no NL→fixed step list reduction; no freezing of execution representation choices.

### PHASE_H_USABILITY_EXPANSION — **NOT_STARTED**

**Name:** `PHASE_H_USABILITY_EXPANSION`

**U2 — Open-world Settings traversal:** Example class: "遍历 Settings 中深度 <= N 的所有安全配置项". Must preserve: type-level traversal specification; runtime-discovered concrete inventory; depth/scope/safety boundaries; branch progress; revisit preservation; honest completion; visual grounding; recovery.

**U3 — Task families:** increasing UI variation, observation noise, alternate routes, ambiguity, popups, external drift, timeout, action uncertainty, longer horizon, recovery complexity. U3 implementation architecture NOT defined now.

**Entry Conditions:** U1 succeeds (Phase E); U2 additionally requires Phase G execution representation.

**Primary Deliverable:** U2 PASS; U3 scoped (definition only).

**Exit Criteria:** U2 honest-completion traversal proven; U3 task-family list scoped without implementation architecture.

**Human Gate:** None new beyond existing gates (Semantic Gate for any semantic purchase; S2/S3 human authorizations for evidence legs).

**Dependencies:** Phase E; Phase G (for U2's execution representation).

**Explicit Non-Goals:** U3 implementation architecture not defined now; no freezing of U2/U3 internals; no new Candidates pre-authorized.

## 6. Current Critical Path

Exact next dependency sequence (ordered; none authorized by this roadmap):

```text
1. REALITY_MODEL_ADMISSION_CONTRACT      (B1)   ← NEXT AUTHORIZED TASK (recommendation only)
2. LEGACY_REALITY_MODEL_EXTRACTION       (B2)
3. INDEPENDENT_REALITY_MODEL_VALIDATION  (B3)
4. REALITY_MODEL_ADMISSION               (B4)
5. PRESSURE × REALITY MODEL MATRIX       (C)
6. CP_12_TARGET_GROUNDING_CHALLENGE      (D)
7. MINIMUM_USABLE_AGENT_SLICE_001        (E / U1)
8. BLOCKER_DRIVEN_MATURITY               (F — concurrent from U1 execution)
9. INTENT_GOAL_PLAN                      (G)
10. USABILITY_EXPANSION (U2 → U3)        (H)
```

Phase A is complete; it contributes no blocking dependency to this path. Phases B–D are the semantic/evidence foundation; the first usability execution leg (E) is the product milestone.

## 7. Human Gates

Approval required for:
- new Reality Admission rule that changes evidence authority (`HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT`);
- new semantic distinction (Semantic Gate);
- ownership change;
- authority change;
- architecture invariant change;
- new major product interpretation of Intent/Goal (`HUMAN_AUTHORIZE_MINIMUM_USABLE_SLICE_001`, `HUMAN_AUTHORIZE_INTENT_GOAL_PATH`);
- unresolved evidence contradiction;
- S1/S2/S3 evidence-maturity advancement (`PROJECT_LEADER_S1_AUTHORIZATION`, S2/S3 analogues).

Approval NOT required for:
- evidence attachment;
- replay test;
- deterministic validation;
- Reality Model candidate extraction;
- deduplication under frozen rules;
- bounded behavior repair with zero semantic delta.

Existing gates remain authoritative: Semantic Gate, Architecture Gate, OpenSpec approval, task authorization, `STOP_AT_S1_AUTHORIZATION` boundary from `s0-roadmap-coverage.md`.

## 8. Explicitly Deferred Design Decisions

NOT frozen — pulled by evidence when reached:
- exact number of Reality Models;
- grounding architecture (no pre-designed GroundingEngine — Phase D);
- Planner architecture (no generic Planner — Phase G);
- Graph/Stack/Frontier structures;
- perception implementation (YOLO/OCR/screenshot pipeline);
- model/provider selection;
- U2/U3 internal implementation;
- future Candidates.

## 9. Architecture Governance

Existing architecture invariants remain authoritative. At minimum preserved:
- External world authoritative.
- Observation != truth (I-4).
- Plan != reality (I-5).
- Memory != current truth.
- Fingerprint != identity.
- Action dispatch result != world result.
- One mutable state → one owner (I-2).
- One decision → one authority (I-3).
- Agent → Container → Traversal → Environment dependency direction.
- Recovery must be verified (I-9).
- Completion requires Goal Evidence (I-10).

A Roadmap phase may challenge architecture only if a concrete pressure AND an accepted Reality Model prove existing ownership/authority incapable of representing the required reality. No speculative architecture expansion.

## 10. Next Authority Boundary

```text
STOP_AT_REALITY_MODEL_ADMISSION_CONTRACT
```

Reason: `S0_GRADUATED` is established; CP-06 is FULLY_CLOSED (Phase A COMPLETE, plan-length-independent initial GoalEvidence proven — both empty and non-empty branches, 2026-08-09); the evidence corpus is fully mined and filtered. The next authority is HUMAN adoption of the Reality Model Admission Contract (`HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT`, Phase B1). No Reality Model admission, S1/S2/S3 work, new semantics, new Candidates, or U1 execution is authorized by this roadmap.
