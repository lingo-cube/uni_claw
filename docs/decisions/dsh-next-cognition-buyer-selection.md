# Decision: Next DSH Cognition Buyer — Selection

> Gate: `PROJECT_LEADER_SELECT_NEXT_DSH_COGNITION_BUYER`
> Mode: `POST_SHADOW_BUYER_SELECTION_AND_ARCHITECTURE_PRESSURE_AUDIT`
> Date: 2026-08-15
> Current maturity: `DSH_SHADOW_COGNITION_INTEGRATED` (frozen, not reopened)
> Decision: **NO_IMMEDIATE_COGNITION_EXPANSION**

## Audit summary (per-candidate)

### A. dsh-advisory-cognition — NO BUYER
- The Agent is a fully deterministic run controller: Plan-driven bind/traverse/navigate
  loop, injected evidence evaluator (SC-P1-003), final failure authority (SC-P1-004),
  recovery decisions in Agent with a single-shot deterministic mechanism (HG-2: no
  retry strategy), no FSM (I-7), no hardcoded scenario strings. There is no code path
  that consumes a semantic proposal and no ingestion seam: the wire contract is the
  frozen 8 read-only methods and `src/UniClaw.Runtime` contains zero proposal /
  suggestion / advisory concepts.
- The system's real, current gap is physical evidence, not interpretation:
  `RAW_CONTROL_CANDIDATE_GENERATION_GAP` — YOLO on Android 15/API 35 does not classify
  controls, so `perception_type` is empty and Binding/StateBeliefReducer cannot
  consume toggle evidence. The active fix is raw-pixel candidate detection with
  **zero LLM/VLM** (RPER-12). Cognitive interpretation cannot repair a raw-pixel
  detection gap.
- Advisory would require the **first-ever DSH→Kernel ingestion path** (new wire method
  + Runtime ingestion contract) — a Runtime change and a new authority surface, both
  unjustified without a concrete Kernel decision buyer.
- C-class semantic events (DecisionProposed / DecisionAccepted / ActionAuthorized /
  RecoveryVerified): ZERO exist in Runtime, ZERO consumers exist → NO new runtime event
  until a real consumer exists. Input-proposal contract ≠ observability event.

### B. dsh-shadow-durability-extension — NO BUYER
- No product/human buyer anywhere in docs or changes for historical Shadow analyses,
  audit history, cross-session review, or long-term cognition comparison. The pinned
  DSH falsification stands (no sanctioned safe live custom-event persistence seam for
  out-of-repo events); no explicit DSH modification authorization exists.
- Defer. Do NOT revisit unsafe PersistenceCoordinator dual-write.

### C. dsh-shadow-human-ui-consumption — NO BUYER
- No operator workflow exists (no dashboard, no frontend, no human-operator surface in
  this repo). Command-only Shadow inspection (classification, facts vs hypotheses,
  uncertainties, recommendations, model-call info) is not blocking any real workflow.
- Defer. Do not build UI because Shadow exists.

### D. NO_IMMEDIATE_COGNITION_EXPANSION — SELECTED
- Cross-stream priority: `perception-actionable-toggle-evidence` (11/45, buyer LIVE
  PHYSICAL SEMANTIC ACTIONABILITY) and `perception-actionable-toggle-evidence-reality-repair`
  (3/25, root cause RAW_CONTROL_CANDIDATE_GENERATION_GAP) are ACTIVE and incomplete;
  `semantic-run-popup-obstruction-integration` (0/24) is the newest deterministic work.
  Physical semantic reality proof has higher system value than any cognition expansion.
- DSH cognition must not outrun Kernel/reality integration maturity.

## Frozen conclusions

- Shadow V1 semantics unchanged: HUMAN_REQUEST_ONLY / EPHEMERAL_PROCESS_LOCAL /
  COGNITIVE_INFERENCE / KernelConsumesShadowOutput=NO / ShadowToKernelMutationPath=NONE.
- No new OpenSpec change. No production change. No Runtime change. No new emitters.
- Advisory/Durability/UI remain FORBIDDEN until a real buyer exists.

## Next action

FREEZE_DSH_COGNITION_AND_RETURN_TO_HIGHEST_PRIORITY_RUNTIME_BUYER
(the physical semantic reality stream: perception toggle evidence + reality repair).
