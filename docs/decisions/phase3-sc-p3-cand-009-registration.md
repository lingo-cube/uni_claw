# SC-P3-CAND-009 Candidate Registration — Recovery Revalidation for Freshly Discovered Branch Progress

> Status: `REGISTERED_CANDIDATE` | Date: 2026-08-09
> Scope: candidate registration only — no Semantic Gate, OpenSpec purchase, implementation task, Runtime change, Capstone authorization, Phase freeze, or completion claim.
> Origin: `S0_BASELINE_READY_CAPSTONE_AUTHORIZATION_REVIEW_RESULT` → `EXTRACT_BOUNDED_CANDIDATE`.

## Candidate Identity

- ID: `SC-P3-CAND-009`
- Working title: **Evidence-Validated Resume for Freshly Discovered Non-Plan Branch Progress After Agent Recovery**
- Phase: Phase 3 candidate
- Current semantic status: `UNREVIEWED`
- Current lifecycle state: `SEMANTIC_GATE_PENDING`

## Repository Evidence

1. Frozen SC-P3-CAND-008 requires the concrete discovered route to be absent from the initial immutable Plan and proves bounded fresh-evidence branch discovery through transient existing Tap mechanics.
2. Frozen SC-P3-CAND-005 carries each post-Recovery branch-effect criterion on the approved branch-entry `PlanStep` and production revalidation finds that criterion by matching the retained branch identity against `Plan.Steps`.
3. A branch discovered under SC-P3-CAND-008 therefore has no guaranteed initial `PlanStep` on which the frozen SC-P3-CAND-005 criterion can exist.
4. SC-S0-CAPSTONE-001 schedules external Launcher/desktop drift while requiring already-proven traversal progress to be neither fabricated nor silently discarded after fresh Recovery verification and reconciliation.

Primary sources:

- `openspec/changes/phase3-bounded-cross-page-discovery/scenarios/SC-P3-CAND-008-bounded-cross-page-discovery.md`
- `openspec/changes/phase3-bounded-cross-page-discovery/specs/bounded-cross-page-discovery/spec.md`
- `openspec/changes/phase3-recovery-progress-resume/scenarios/SC-P3-CAND-005-recovery-progress-resume.md`
- `openspec/changes/phase3-recovery-progress-resume/specs/recovery-progress-resume/spec.md`
- `src/UniClaw.Runtime/Agent/Agent.cs`
- `docs/system/scenarios/06-s0-capstone-settings-traversal.md`

## Reality Distinction

```text
branch A is discovered from fresh accepted evidence
+
A is absent from the initial concrete Plan
+
A obtains evidence-backed completion under parent P
+
external drift triggers one verified Agent Recovery to P

!=

the recovered world has freshly revalidated A's external effect
```

Historical discovered-branch completion, verified world-position Recovery, correct parent identity, and a refreshed branch inventory do not individually prove that A's required external effect still holds. At the same time, the frozen SC-P3-CAND-005 revalidation criterion cannot be assumed to exist on an initial `PlanStep` for a branch that was intentionally absent from that Plan.

## Bounded Scenario Pressure

The Semantic Gate must evaluate one bounded parent scope with:

- parent P and freshly discovered required branch A;
- A absent from the initial Plan's concrete targets;
- independently authorized A executed through existing Tap mechanics;
- evidence-backed A completion retained by Agent-owned branch progress;
- exactly one Agent-scope external drift and one verified Recovery to P;
- one fresh recovered-world Observation capable of producing a positive, contradicted, or unresolved A-effect result;
- one remaining required sibling B used only to test honest resume/non-resume behavior;
- deterministic replay for equal RunId, Goal inputs, external-world inputs, disturbance schedule, and action sequence.

## Observable Branches for Gate Review

These are candidate pressures, not yet approved normative SHALL:

### Positive evidence

Fresh recovered-world evidence supports A's retained effect. The Runtime may preserve A as contributing progress and continue unresolved required work without blindly redispatching A.

### Contradicted evidence

Fresh recovered-world evidence contradicts A's retained effect. Historical provenance remains observable, but A cannot contribute to current completion and the Runtime must not fabricate repair or success.

### Unresolved evidence

Fresh recovered-world evidence cannot determine whether A's effect remains valid. A cannot contribute to completion, and the Runtime must not silently replay A or reinterpret Recovery verification as branch success.

## Existing Frozen Coverage

- SC-P2-001/003: Agent Recovery position restoration, fresh Observation, verification, and failure honesty.
- SC-P3-CAND-004: Agent-owned bounded sibling inventory/completion evidence.
- SC-P3-CAND-005: post-Recovery true/false/null effect revalidation for branch entries represented in Plan.
- SC-P3-CAND-006: independent authorization of freshly observed candidates.
- SC-P3-CAND-008: required-branch discovery when concrete targets are absent from the initial Plan.

None of these frozen slices may be reinterpreted or expanded by this registration.

## Questions Reserved for Semantic Gate

The Semantic Gate must determine, without presupposing a solution:

1. whether this pressure is genuinely independent or can be expressed by an admissible composition of existing frozen semantics;
2. what immutable semantic evidence, if any, must associate a freshly discovered branch with its post-Recovery external-effect criterion;
3. whether an existing production surface can carry that meaning without changing its frozen semantics;
4. whether any production type or field purchase is required and, if so, the smallest exact budget;
5. whether the bounded positive/contradicted/unresolved branches preserve Agent ownership and sole decision authority;
6. whether the pressure can be expressed without route/frontier/graph/tree/stack state, a new mutable owner, or new Recovery authority.

## Ownership and Authority Baseline

- Environment: external Observation, disturbance, dispatch outcome, and deterministic world transition only.
- Recovery: restore, observe, and verify mechanics only.
- Traversal: deterministic local Execute → Observe → Verify and journal evidence only.
- Container: semantic-page continuity and page-local evidence/progress only.
- Agent: discovered-branch interpretation, cross-Container progress, recovered-world validity interpretation, resume/escalation, GoalEvidence, and final RunState.

Registration proposes:

- Ownership delta: `NONE`
- Authority delta: `NONE`

These are constraints for Gate review, not an approval finding.

## Explicitly Not Authorized

- any production model type, field, enum, interface, component, or mutable state;
- reinterpretation of `PlanStep.BranchEffectEvidenceEvaluator`, `BranchInventoryEvidence`, `BranchProgressEvidence`, `CandidateAuthorizationEvidence`, or `GoalEvidence`;
- generic effect registry, predicate framework, dynamic planner, retry framework, or uncertainty framework;
- navigation graph/tree/stack, frontier, persistent route/depth state, checkpoint, ResumeToken, or progress manager;
- Recovery ownership of branch progress or Recovery dependency on Container/Traversal;
- Runtime code, tests, Fake infrastructure, OpenSpec artifacts, implementation tasks, or coder dispatch;
- Capstone implementation, `READY_FOR_S0_RUN`, `S0_BASELINE_READY`, `S0_GRADUATED`, Phase 3 freeze, or Phase completion;
- legacy-corpus classification or roadmap reconciliation.

## Registration Result

```text
SC_P3_CAND_009_REGISTERED
```

Next required authority:

```text
PROJECT_LEADER_SEMANTIC_GATE_SC_P3_CAND_009
```

STOP after registration.
