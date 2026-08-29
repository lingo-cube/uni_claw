## 1. Portable WorkItem Contract

- [x] 1.1 Add backward-compatible `required_skills` schema, trusted resolver, builder emission, context digest integration, and CLI WorkItem context input.
- [x] 1.2 Add validator tests for ordered propagation, legacy omission, invalid names, duplicates, missing Skills, ambiguous roots, and context-key invalidation.

## 2. Worker Adapters

- [x] 2.1 Update all Codex Worker adapters to fully load resolved required Skills before action and fail closed without granting Skill authority.
- [x] 2.2 Propagate resolved Skill context through the DSH ModuleContext/dispatch payload and update focused DSH tests and usage documentation.
- [x] 2.3 Persist the complete validated Worker payload for delayed DSH dispatch, reject incomplete Skill payload before spawn, and verify ordered Skill bodies reach the Host seam.

## 3. UI-first Debugging Method

- [x] 3.1 Update `evidence-driven-debugging`, `runtime-behavior-debugging`, and `uniagent-evolution-loop` with the human-visible UI goal/current-state/shortest-path hypothesis and no-microcontrol boundary.
- [x] 3.2 Add focused semantic tests and run `quick_validate.py` for all three Skill bodies.
- [x] 3.3 Add the bounded Leader Reality Preflight to the shared RoleProfile and UniFlow workflow, with focused behavioral contract tests.

## 4. UniFlow Contract and Documentation

- [x] 4.1 Update the UniFlow workflow, Task Contract, examples, and repository Skill documentation with Bug routing and `required_skills` precedence/authority rules.
- [x] 4.2 Complete Knowledge System documentation sync or record explicit `NO_CHANGE` decisions for architecture and runtime sources.

## 5. Verification

- [x] 5.1 Run AgentWorkflow focused/full regression, profile and DSH adapter validators, consistency checks, strict OpenSpec validation, and `git diff --check`.
- [x] 5.2 Record implementation evidence, confirm no Runtime/Perception/Strategy Contract/GoalEvidence/SourceIdentity changes, and stop at the completed checkpoint.
- [x] 5.3 Re-run focused gates for the DSH consumer and Leader preflight increment, preserve the known full-suite pin blocker, and amend checkpoint evidence with the exact Host/pin boundary.

Verification evidence: [`evidence/checkpoint.md`](evidence/checkpoint.md). The
original change-specific gates passed. Follow-up review found and closed two gaps:
delayed DSH dispatch now persists the complete Worker payload, and the shared Leader
profile now requires a bounded Reality Preflight. The DSH adapter validator remains
fail-closed on the pre-existing Profile Source revision pin drift; refreshing that
trust pin is outside this change and remains a Human Gate.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| engineering-governance | `openspec/changes/uniflow-required-skill-propagation/design.md` |
