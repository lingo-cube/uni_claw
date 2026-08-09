# SC-P3-CAND-005 — Evidence-Validated Progress Resume After Agent Recovery

> Phase 3 | Reopened Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`
> Approved Production Model Delta: exactly one immutable `PlanStep.BranchEffectEvidenceEvaluator: Func<Observation, bool?>?` field
> Model Types: `+0` | Fields: `+1` | Enums: `+0` | Interfaces: `+0` | Components: `+0` | New Mutable-State Owners: `+0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/recovery-progress-resume/spec.md`

## Goal

Prove that verified world-position Recovery does not automatically validate retained Agent-owned branch progress: Agent must evaluate the branch's durable external-effect criterion against fresh recovered-world evidence, preserve only revalidated progress, reject contradicted or unresolved progress, and avoid blindly redispatching completed work.

## Given

- Runtime is Running in bounded parent semantic Container P with approved siblings A and B.
- Fresh P inventory evidence established A and B through frozen SC-P3-CAND-004 behavior.
- A's approved branch-entry `PlanStep` carries a deterministic, side-effect-free Observation-only criterion for A's required external effect.
- A was locally proven complete before returning to P; `BranchProgressEvidence` records A's historical completion Observation sequence while B remains incomplete.
- An external Launcher/desktop drift occurs while P is the suspended Container.
- Frozen SC-P2-001 Recovery restores and verifies P exactly once and supplies a fresh post-Recovery Observation beyond `Trap.Observed`.
- Agent owns Plan, branch progress, recovered-world validity interpretation, resume/escalation, GoalEvidence, and final RunState.

## Positive

```text
A historically complete
→ Launcher drift
→ verified Recovery directly restores P
→ fresh recovered-world Observation evaluates A criterion to true
→ Agent revalidates A with the fresh sequence
→ Agent does not replay A-entry, A-work, or A-return
→ execution continues with B
→ B requires its own proof
→ only valid A+B evidence may support subtree completion
→ only satisfied GoalEvidence may complete the Run
```

## Contradicted

```text
A historically complete
→ verified Recovery restores P
→ fresh Observation evaluates A criterion to false
→ A historical completion cannot contribute
→ historical Trace/journal remain provenance only
→ no fabricated subtree or Goal completion
→ no blind redispatch of A
```

## Unresolved

```text
A historically complete
→ verified Recovery restores P
→ A criterion is absent or evaluates fresh Observation to null
→ A validity remains unresolved
→ retained evidence contributes nothing
→ no blind redispatch of A
→ explicit Agent-level non-completion/escalation
```

## Required Assertions

1. The branch criterion is associated with A's approved branch-entry step and is never treated as proof before evaluation.
2. A's historical completion sequence is at or before `Trap.Observed` and cannot independently contribute after Recovery.
3. The revalidation Observation is fresh, postdates the drift boundary, and is obtained only after `RecoveryResult.Verified`.
4. Criterion `true` refreshes A's completion evidence and permits continuation with B.
5. Criterion `false` excludes A from current completion while preserving historical Trace/journal provenance.
6. Criterion `null` or absence produces explicit unresolved non-completion/escalation.
7. Correct parent identity, fresh inventory, Container continuity, and Recovery verification alone never validate A.
8. A's completed external-effect action appears exactly once; no branch-entry/work/return prefix is replayed after successful revalidation.
9. B remains independently pending until its own local proof exists.
10. `RunState.Completed` appears only after Agent consumes satisfied GoalEvidence.
11. Recovery never owns or mutates branch progress and does not decide validity.
12. Equal inputs replay to equal criterion outcomes, progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and RunState.

## Ownership and Authority

- Agent owns the immutable Plan criterion, `BranchProgressEvidence`, recovered-world validity interpretation, resume/escalation, GoalEvidence evaluation, and final RunState.
- Recovery owns only restoration actions, fresh observation, and verification of its injected position criterion.
- Container owns only page-local identity and progress.
- Traversal owns deterministic local execution and journal evidence only.
- Environment reports external Observation and dispatch outcomes only.

## No-Blind-Replay Boundary

Avoiding replay is limited to A's already-completed evidence-backed prefix when verified Recovery directly restores the suspended bounded parent P and A is freshly revalidated. Contradicted or unresolved evidence produces explicit non-completion/escalation rather than automatic repair. No action-idempotence taxonomy, checkpoint, ResumeToken, navigation graph/stack, or generic Recovery planner is implied.

## Explicitly Deferred

- Persistent validity state, EvidenceState/RecoveryState enum, recovery epoch, freshness field, or second criterion field.
- New model type, interface, component, or mutable-state owner.
- Generic evidence/predicate framework, EffectRegistry, action-idempotence taxonomy, checkpoint/snapshot/progress manager, Recovery planner, navigation graph/stack, TraversalContext, FSM, or workflow engine.
- More than one parent scope or Recovery cycle.
- Autonomous discovered-candidate safety, SC-S0-CAPSTONE-001 implementation, Runtime refactor, Harness changes, real-device/Vision work, or Phase completion.
