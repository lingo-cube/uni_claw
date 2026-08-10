# U2_MINIMUM_USABLE_AGENT_SLICE_RESULT

> Date: 2026-08-10
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Scenario: `SC-U2-MUS-001`
> Status: `VALIDATED`

## Capability

**Bounded Open-World Settings Traversal from a Resolved Type-Level Intent**

```text
resolved OPEN_WORLD_TYPE_LEVEL envelope
→ bounded upstream execution seam
→ runtime-discovered branch inventory
→ independently authorized child execution
→ fresh verified parent continuation
→ retained sibling progress
→ no unresolved bounded in-scope work
→ existing fresh GoalEvidence
→ Agent Completed
```

The slice proves dynamic A/B discovery without a concrete future route,
exactly four Tap actions, verified child-to-parent returns, A progress retained
while B remains pending, navigation-only/depth boundaries, and completion only
after Agent derives `VerifiedBoundedTraversalCompletion` and consumes satisfied
existing fresh `GoalEvidence`.

## Runtime Delta

- Added exactly one production file:
  `src/UniClaw.Runtime/Planning/IntentSemanticEnvelopeExecution.cs`.
- Modified only `src/UniClaw.Runtime/Agent/Agent.cs` for U2 production behavior.
- Added one public static execution seam and one internal bounded Agent path.
- Parent continuation is method-local
  `Stack<(RuntimeContainer Parent, string ChildIdentity)>`; semantic depth is
  derived from stack count.
- `Goal.cs` delta: NONE.
- New Goal values, Model types, enums, interfaces, engines, managers, mutable
  fields, and state owners: 0.
- Existing `Agent.RunAsync(Goal, Plan, ...)`: unchanged.

## Formal Scenario Evidence

The promoted L2 family consists of:

- `tests/UniClaw.Runtime.Tests/Scenario/Fakes/U2OpenWorldSettingsFixture.cs`;
- `tests/UniClaw.Runtime.Tests/Scenario/U2OpenWorldExecutionTests.cs`;
- `tests/UniClaw.Runtime.Tests/Scenario/U2OpenWorldSettingsFormalScenarioTests.cs`.

It proves:

- positive dynamic A/B traversal, exactly four Tap actions, fresh final root
  evidence, retained sibling progress, and deterministic completion;
- unresolved inventory performs zero discovered-branch dispatch and no final
  Goal evaluation;
- A complete while B pending does not complete;
- ambiguous or rejected parent return performs no return dispatch;
- wrong-parent and stale post-action evidence record no child completion;
- unsatisfied final fresh GoalEvidence fails after mechanically complete
  traversal;
- dangerous and beyond-depth candidates receive zero dispatch;
- equal inputs replay equal actions, Observations, journal, Trace, progress,
  GoalEvidence, reason, and RunState.

Evidence Asset:

```text
Classification: NEW_VARIANT
Level: L2_SHORT_CHAIN_INTEGRATION
Source: SC-U2-MUS-001 production Planning → Agent → Container → Traversal → Environment path
Oracle: positive + negative + cutoff + fresh-evidence completion + deterministic replay
Promotion: PROMOTED
```

## Validation

- Build: PASS, 0 warnings, 0 errors.
- U2 targeted tests: 18/18 PASS.
- Architecture Guards: 9/9 PASS.
- CP-04/07/12/14 and Phase 1–3 focused regression: 15/15 PASS.
- Full suite: 484/484 PASS.
- Consistency: C1–C10 ALL PASS.
- U2 strict OpenSpec validation: PASS.
- All OpenSpec changes strict validation: 14/14 PASS.
- Static whitespace/scope audit: PASS.
- Independent read-only validation: PASS.

## Architecture / Ownership / Authority

- Architecture invariants: UNCHANGED.
- Ownership: UNCHANGED.
- Decision authority: UNCHANGED.
- Dependency direction: UNCHANGED.
- Safety semantics: UNCHANGED.
- State-machine pressure: NONE.

Planning validates and destructures only. Agent owns inventory acceptance,
semantic depth, method-local parent association, branch progress,
`GoalEvidence` consumption, and final RunState. Container, Traversal,
Environment, and Recovery retain their frozen responsibilities.

## Remaining Capability Gap

U2 proves the bounded deterministic navigation-only L2 slice. It does not prove
viewport-expanded discovery, state-changing open-world work, real emulator or
device evidence, arbitrary navigation/back semantics, generic planning,
Recovery/Popup composition inside this path, or U3 task-family behavior.

## Recommended Next Capability

`PROJECT_LEADER_U3_TASK_FAMILY_SCOPING` — definition and prioritization only,
using increasing UI variation, observation noise, alternate routes, ambiguity,
Popup, external drift, timeout/action uncertainty, and longer-horizon Recovery
pressure. Do not define U3 implementation architecture at this step.
