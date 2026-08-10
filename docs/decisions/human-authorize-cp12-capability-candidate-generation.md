# HUMAN_AUTHORIZE_CP12_CAPABILITY_CANDIDATE_GENERATION

> Generated: 2026-08-09
> Role: Project Leader / Human Authority
> Gate: `HUMAN_AUTHORIZE_CP12_CAPABILITY_CANDIDATE_GENERATION`
> Contract: `docs/system/reality-model-admission-contract.md` §19

---

## Decision

**AUTHORIZE_CP12_CAPABILITY_CANDIDATE_GENERATION**

---

## Evidence Reviewed

| Artifact | Status |
|---|---|
| CP-12 Challenge Result (Phase D) | 5/5 cases GAP — Runtime cannot establish "selected target == intended target" |
| RM-10 Reality Model | ACCEPTED (B4, 2026-08-09) — 5 WFs, 4 RIs, 4 ERs, all gates PASS |
| RM-10 Independent Validation (B3) | CONDITIONAL_PASS → conditions resolved in B4 |
| CP-12 Semantic Gate Preparation | CAPABILITY_GAP confirmed — all 4 ERs GAP against current Runtime |
| Reality Model Admission Contract | Frozen v1.0, adopted `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` |
| Architecture Invariants | 14 invariants preserved; no CP-12 candidate may violate them |

## Rationale

1. **RM-10 is an accepted Reality Model.** CP-12 is not hypothetical — it is modeled with 5 World Facts (2 DIRECT from E3 evidence), 4 Reality Inferences, and 4 Expected Requirements. The World Facts are directly observed in committed evidence (VE-07, VE-06 at E3).

2. **The capability gap is demonstrated, not speculated.** The Phase D challenge found all 5 challenge cases GAP. All 4 RM-10 ERs are GAP against the current Runtime. The gap is specific: a missing verification step between candidate discovery and action dispatch.

3. **CP-12 is the critical-path blocker for U1.** The U1 usability slice requires reliable target grounding at 4 steps. Without this capability, U1 cannot proceed.

4. **Candidate generation ≠ implementation.** This authorization allows exploring WHAT approaches could satisfy RM-10's ERs. It does not authorize building any of them. Architecture commitment requires a subsequent gate.

5. **The capability boundary is well-defined.** RM-10's ER-25..ER-28 define WHAT the system must be able to do. The non-requirements explicitly exclude implementation design.

---

## Authorized Scope

The next phase (`CP12_CAPABILITY_CANDIDATE_GENERATION_RESULT`) is authorized to:

### Permitted

1. **Generate Capability Candidates** — one or more candidate approaches to satisfy RM-10 ER-25..ER-28
2. **Evaluate candidates against RM-10** — each candidate must demonstrate which ERs it satisfies and how
3. **Identify failure modes** — each candidate must state what world conditions would cause it to fail
4. **Assess trade-offs** — compare candidates on correctness, complexity, perception-dependence, and evidence requirements
5. **Validate against Architecture Invariants** — each candidate must be checked against all 14 invariants
6. **Produce rejection reasons** — candidates that are rejected must have explicit reasons

### Candidate Format

Each candidate must be expressed as a **behavioral capability description,** not an implementation design. For example:

- ✓ "The system compares the element's screen region against the expected region for the target type"
- ✗ "Use a CNN classifier trained on element bounding box features"

Candidates describe WHAT the system would do. Implementation describes HOW.

### Acceptance Criteria

The output (`CP12_CAPABILITY_CANDIDATE_GENERATION_RESULT`) must contain:

- **Candidate list** with behavioral descriptions
- **ER coverage matrix** — which ERs each candidate satisfies
- **Failure mode analysis** — what world conditions break each candidate
- **Trade-off comparison** — correctness vs complexity vs perception-dependence
- **Invariant compatibility** — explicit check against each architecture invariant
- **Rejection reasons** — for any candidate not carried forward

---

## Explicitly NOT Authorized

- GroundingEngine implementation
- Matcher implementation
- Vision model selection
- LLM / VLM selection
- Runtime code modification
- Architecture commitment (no component design)
- New Reality Model creation
- New CP registration
- S1 / S2 / S3 execution
- U1 implementation

---

## Next Phase

**CP12_CAPABILITY_CANDIDATE_GENERATION**

## Repository Changes

`docs/decisions/human-authorize-cp12-capability-candidate-generation.md` — created (this authorization). No other files modified.

STOP.
