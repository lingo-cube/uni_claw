# Slow Semantic Buyer Discovery

> Date: 2026-08-19
> Role: Project Leader / Buyer Discovery
> Base: `PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_GRADUATED`
> Scope: Discovery analysis only — no implementation, no LLM, no Runtime capability addition
> Result: `PROJECT_LEADER_SLOW_SEMANTIC_BUYER_DISCOVERY_RESULT`
> Decision: **SLOW_SEMANTIC_NOT_JUSTIFIED**

## 1. Fast Semantic covered scope

Fast Semantic Container Identity Recovery is graduated and real-world validated:

- Scrolled Container Identity Drift (DeveloperOptions title-offscreen) → Fast
  Semantic candidate → Runtime Validation recovery.
- Text Resolver success path is unchanged.
- Vector miss / low confidence / wrong container → fail-close preserved.
- False recovery rate = 0.

Fast Semantic currently covers:

- **Container Identity Recovery** only.

## 2. Current unresolved problems

From the real-world failure distribution corpus (24 runs) and later validation:

| Class | Count | Status after Fast Semantic |
|---|---|---|
| K. BELIEF_CONTRADICTED (ASU page-unresolved) | 6/24 | Root cause = Scrolled Container Identity Drift; addressed by Fast Semantic Container Identity + Runtime Validation |
| F. BINDING_UNRESOLVED | 2/24 | Intermittent cold-start perception/landing variance; local/deterministic, no semantic gap |
| G. ACTION_GROUNDING_FAILURE | 2/24 | Nav tap landed but transition proof failed; local verification, no semantic gap |
| C. SCENARIO_SETUP_DEFECT | 1/24 | Device/launch preparation defect |
| A/B local closure | 13/24 | Already satisfied / L0 completed |

No TRUE_PLANNING_GAP was observed (0/24). No evidence that ambiguous element meaning,
relation understanding, or long-context semantics is currently blocking Runtime.

## 3. Failure distribution analysis

- The only previously dominant failure mode (ASU SemanticContradiction) was a
  **bounded container-identity defect**, not a deep-semantics gap.
- After Fast Semantic Container Identity, that failure mode has a validated recovery
  path and a fail-close fallback.
- Remaining failures are **intermittent local mechanisms** (binding / nav transition /
  setup), not ambiguous semantic understanding.
- L1/L2 buyer pressure remains low/none. No natural Slow Semantic trigger has been
  observed in the existing corpus.

## 4. Candidate buyers

| Candidate | Description | Exists in current evidence? |
|---|---|---|
| A. Container Identity | Already covered by Fast Semantic | No — not a Slow buyer |
| B. Element Meaning | e.g. "Enable" / "Activate" / "Turn on" | No confirmed runtime blocker; local binding/vision rules handle current scope |
| C. Relation Understanding | element → region / container / context | No confirmed runtime blocker; Container/local structure covers current scope |
| D. Ambiguous Evidence | multiple plausible page/element candidates | No current dominant failure; local disambiguation and fail-close are present |
| E. Long Context Semantic | cross-page history / multi-step context | No confirmed buyer; container history + Runtime belief cover current scope |

## 5. Local-first exclusion process

Every candidate is evaluated: can it be solved with Vision Evidence, Binding, Runtime
Rule, Container History, or Fast Semantic?

| Candidate | Local mechanism? | Verdict |
|---|---|---|
| A. Container Identity | Yes — Fast Semantic Container Identity | NO_SLOW_SEMANTIC_REQUIRED |
| B. Element Meaning | Current scope uses Vision evidence + Binding + Runtime rules; no failure demands deeper meaning | NO_SLOW_SEMANTIC_REQUIRED |
| C. Relation Understanding | Current scope uses Container structure + observation continuity; no failure demands semantic relation model | NO_SLOW_SEMANTIC_REQUIRED |
| D. Ambiguous Evidence | Current scope fail-closes or uses deterministic disambiguation; no failure demands LLM ranking | NO_SLOW_SEMANTIC_REQUIRED |
| E. Long Context Semantic | Current scope uses Container History + Existing Belief Context; no failure demands cross-page LLM reasoning | NO_SLOW_SEMANTIC_REQUIRED |

No candidate reached `SLOW_SEMANTIC_CANDIDATE` because no local mechanism was shown
insufficient by current repository truth.

## 6. Do we need Slow Semantic?

**No clear buyer is confirmed.**

- Fast Semantic + Runtime Evidence Fusion + Runtime Validation already cover the
  validated real-world failure mode (Scrolled Container Identity Drift).
- Remaining real-world failures are local, intermittent, and deterministic in nature.
- No evidence shows that LLM / vector-long-context / slow semantic reasoning would
  close a current Runtime gap without violating fail-closed safety.
- The architecture remains frozen: Semantic is an Evidence Provider; Runtime is the
  only Belief / Action authority.

## 7. Future possible Slow Semantic boundary

If a future buyer appears, Slow Semantic must remain:

- **Async Semantic Checkpoint Evidence** only.
- An additional evidence provider, not a Decision Maker / Planner / Action Generator /
  Fact Producer / Agent Replacement.
- Inputs limited to Observation / Perception Evidence / Existing Belief Context.
- Outputs limited to SemanticEvidence (candidate ranking, ambiguity explanation,
  semantic relation evidence).
- Not L1 Assistance, not model consultation, not Agent Loop, not Planner.

This boundary is **reserved**, not purchased.

## 8. Decision

```text
PROJECT_LEADER_SLOW_SEMANTIC_BUYER_DISCOVERY_RESULT
Decision: SLOW_SEMANTIC_NOT_JUSTIFIED
NEXT_GATE: Keep current Semantic architecture frozen
```

No implementation is started. No Agent / Runtime / Resolver / Vision / Belief /
L1 / DSH changes were made.