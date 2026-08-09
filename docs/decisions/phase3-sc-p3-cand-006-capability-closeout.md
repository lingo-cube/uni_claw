# SC-P3-CAND-006 Capability Closeout

> Status: Frozen Capability | Date: 2026-08-09
> Scope: SC-P3-CAND-006 only — this is not a Phase 3 freeze or Capstone authorization.
> Authority: acceptance receipt for `openspec/changes/phase3-bounded-candidate-safety/`; it does not replace the approved Scenario, Spec, Architecture Contract, prior frozen capability closeouts, or S0 roadmap gates.

## Capability

**Bounded Safety Classification of Newly Discovered Settings Candidates**

Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`

## Proven Behavior

```text
one fresh active-Container Observation exposes S / D / T / U
→ Agent evaluates the Goal-owned bounded criterion in stable order
→ false / null: append explicit pre-dispatch Trace evidence and never dispatch
→ first true: nominate at most one transient existing Tap step
→ Traversal performs normal Select → Execute → Observe → Verify
→ only fresh satisfied GoalEvidence may complete the Run
```

The accepted slice proves:

- Observation membership, semantic authorization, execution, required work, and Goal completion remain distinct.
- The evaluator consumes only candidates contained in one supplied fresh Observation and returns deterministic `true` / `false` / `null` evidence with non-empty reasons.
- Agent is the sole semantic authorization authority; Traversal receives only the first authorized candidate and retains only mechanical execution authority.
- Destructive navigation-like D, state-changing T, and unresolved U produce explicit Trace evidence with candidate text/index and source sequence while `Action` and `ActionId` remain absent.
- D/T/U never enter Traversal and produce zero matching Environment actions.
- If no candidate is authorized, Agent returns an explicit existing failure/non-completion result with zero candidate dispatch and no fabricated GoalEvidence.
- An authorized candidate remains subject to normal Traversal rejection and does not imply dispatch success, world effect, required-work membership, or completion.
- A successful local Tap without satisfied GoalEvidence ends in explicit non-completion rather than fabricated success.
- Rejected/unresolved candidates are not added to Agent branch-progress inventory merely because they were visible.
- An absent evaluator preserves the existing fixed-Plan path.
- Equal inputs replay to equal authorization evidence, Trace, journal, ActionHistory, Observations, GoalEvidence, and RunState.

## Production Delta

- Model types: exactly +1 immutable `CandidateAuthorizationEvidence` value.
- Fields: exactly +3 immutable fields total — `Authorized`, `Reason`, and optional `Goal.CandidateAuthorizationEvaluator`.
- Enums: +0.
- Interfaces: +0.
- Components: +0.
- New mutable-state owners: +0.
- Behavior: one opt-in bounded Agent pre-dispatch control-flow branch only.

## Ownership and Authority

- Ownership delta: **NONE**.
- Authority delta: **NONE**.
- Agent remains the sole owner of Goal intent, candidate authorization, candidate nomination, denial Trace evidence, GoalEvidence consumption, and final RunState.
- Container retains page-local Observation/candidate/local-progress ownership and gains no authorization role.
- Traversal retains deterministic local selection, dispatch, fresh observation, verification, and journal ownership; it does not evaluate the Goal criterion.
- Environment reports external Observation and dispatch outcomes only.
- Recovery remains unchanged and receives no candidate-safety authority.

## Frozen Boundary

| Criterion outcome | Frozen meaning |
|---|---|
| `true` | Candidate is eligible for at most the first bounded existing Tap path; authorization is not execution or completion truth. |
| `false` | Positively rejected; explicit pre-dispatch Trace evidence; zero candidate dispatch. |
| `null` | Evidence is insufficient and grants no authorization; explicit unresolved Trace evidence; zero candidate dispatch. |
| evaluator absent | No discovered-candidate behavior is activated; existing fixed-Plan execution remains unchanged. |

## Explicitly Not Purchased

- SafetyManager, RiskEngine, SafetyEngine, PolicyEngine, RuleEngine, SafeActionExecutor, authorization manager, or mutable safety owner;
- RiskLevel, Confidence, policy hash, coordinates, Fingerprint, Vision/VLM judgement, or AI safety framework;
- persistent authorization cache/history, policy registry/parser, or new audit/Trace/journal/Trap surface;
- generalized candidate discovery, multi-page route construction, dynamic planner, navigation graph/stack, or universal action interception;
- new DeviceAction variants, Recovery semantics, Capstone implementation, Harness change, Runtime refactor, S1/S2/S3 work, or Phase completion.

## Structural Pressure

Agent now contains one additional bounded pre-loop decision branch. The branch stays inside existing semantic authority and the exact approved production budget, so this is non-blocking structural pressure and does not authorize extraction or refactor.

## Acceptance Receipt

- OpenSpec: strict validation passed; artifacts complete.
- Tasks: 4/4 complete.
- Independent validation: PASS.
- Build: 0 warnings, 0 errors.
- Tests: 270/270 passed.
- SC-P3-CAND-006 fixture/behavior/formal tests: 14/14 passed.
- Formal Scenario tests: 4/4 passed.
- Candidate authorization value tests: 6/6 passed.
- Architecture-filtered tests: 9/9 passed; core `ArchitectureGuardTests` remains 8 facts.
- Consistency checks: C1–C9 ALL PASS.
- Production delta: exactly one approved immutable type, three immutable fields, and one existing Agent control-flow adjustment.
- Ownership delta: NONE.
- Authority delta: NONE.
- Semantic drift: NONE.

## State

```text
SC_P3_CAND_006_FROZEN_CAPABILITY
```

This state does **not** mean `PHASE_3_FROZEN`, `S0_BASELINE_READY`, `S0_GRADUATED`, or `PHASE_COMPLETE`. Legacy baseline classification, Capstone authorization/execution, OpenSpec archive, and any new Scenario require separate authority.
