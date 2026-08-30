# Required Skill Propagation Checkpoint Evidence

DocumentType: `IMPLEMENTATION_EVIDENCE`
Authority: `NONE`
RecordedAt: `2026-08-29`
CheckpointState: `IMPLEMENTATION_COMPLETE_DSH_PIN_HUMAN_GATE`

## Implemented

- `WorkItem.required_skills` is an ordered, duplicate-free, backward-compatible
  Skill-name array; newly built WorkItems emit it explicitly.
- The shared validator resolves names from `.ai/skills` only and rejects
  malformed, missing, duplicate, unreadable, or
  frontmatter-mismatched Skills, includes the full Skill bodies in the context
  digest, and emits ordered canonical Skill documents in ModuleContext.
- Codex Worker adapters and the DSH ModuleContext/Host payload receive the same
  ordered Skill context. DSH rejects missing, empty, reordered, path-mismatched,
  or digest-mismatched Skill documents before Host spawn.
- In-process DSH Host spawn receives the complete `worker_payload`; delayed CLI
  dispatch records persist the same payload for session-side spawn instead of
  preserving only Skill names or a context digest.
- The shared coding-leader Profile and UniFlow workflow require a bounded Reality
  Preflight before semantic attribution, architecture judgment, or deep code
  traversal. Missing UI evidence is marked unknown rather than invented.
- Bug routing selects `evidence-driven-debugging`; Runtime/FSM/Traversal/Recovery,
  asynchronous, device, flaky, or nondeterministic work additionally selects
  `runtime-behavior-debugging`.
- The debugging and UniAgent evolution Skills now begin from the user-visible
  goal, current UI, shortest plausible human interaction path, expected visible
  transition, and First Divergence Point. They prohibit unbounded call-chain
  following and prohibit coordinates or fixed click scripts as Runtime knowledge.

## Passing evidence

- `python3 -m unittest tests/AgentWorkflow/test_agent_profile_validator.py` —
  PASS, 58 tests.
- `python3 -m unittest tests/AgentWorkflow/test_codex_skill_propagation.py tests/AgentWorkflow/test_codex_skill_discovery.py`
  — PASS, 6 tests.
- `python3 -m unittest tests/AgentWorkflow/test_skill_semantics.py` — PASS,
  3 tests.
- Focused DSH propagation test
  `DshProfileAdapterTests.test_31_required_skills_enter_manifest_and_envelope` —
  PASS, 1 test.
- Follow-up DSH consumer, CLI persistence, Leader Reality Preflight, shared
  validator, Codex propagation/discovery, and Skill semantic suite — PASS,
  93 tests total.
- Skill Creator `quick_validate.py` — PASS for all three changed Skill bodies.
- `python3 tools/agent_profile_validator.py validate` —
  `AGENT_WORKFLOW_VALIDATION_PASS`.
- `bash scripts/check-consistency.sh` — PASS, C1-C13; active membership and
  snapshot counts both resolve to 22.
- `openspec validate uniflow-required-skill-propagation --strict` — PASS.
- `git diff --check` — PASS.

## Full-regression divergence and Human Gate

The complete AgentWorkflow discovery run was executed from a temporary working
directory to avoid intentionally writing DSH state into the repository. It ran
140 tests and was stopped by the same pre-existing trust gate: 10 failures and
19 errors are downstream of Profile Source revision drift.

`python3 tools/dsh_profile_adapter.py validate` reproduces the First Divergence
Point directly:

```text
FAIL: source revision drift: pinned e2d8dd44214632f50777992d58fb4fe318ad45f0 != current e6c6f4b5eb927d05338128f86058d391cc23a3ba
```

Refreshing `.dsh/profile-adapter/profile-source.yaml` changes the DSH trust
baseline and is not authorized by this change. Human Gate: the DSH Profile Source
owner must review the intervening revision and explicitly authorize a new pin.
The propagation implementation itself is covered by the passing shared-validator,
Codex, Skill semantic, and focused DSH tests above.

The follow-up tests use an isolated temporary DSH state directory and an in-memory
copy of the config pinned to the current checkout revision. They do not rewrite
`.dsh/profile-adapter/profile-source.yaml` or append repository state events.

## Host execution boundary

The repository now proves that the adapter resolves canonical Skill bodies,
validates their order/content/digest, delivers them to the in-process Host seam,
and persists them for delayed session-side spawn. This is delivery evidence, not
proof that a model followed the instruction. Model self-report remains insufficient;
actual DSH execution still requires the existing Host receipt and session integration
evidence. The production Profile Source pin remains unchanged until its owner reviews
and authorizes the final committed revision.

## Boundary confirmation

This change modified only engineering-governance schemas, validators, adapters,
Worker instructions, Skill bodies, tests, documentation, and its OpenSpec bundle.
It did not modify Runtime, Perception, Strategy Contract, GoalEvidence,
SourceIdentity, product behavior, or lifecycle authority. The dirty worktree
contains unrelated in-flight changes in some protected areas; they were neither
used as authorization nor edited by this change.

Knowledge System disposition:

- Canonical WorkItem/UniFlow/Skill contracts: `UPDATE`.
- DSH consumer documentation and examples: `UPDATE`.
- Original OpenSpec membership projection: `UPDATE` from 20 to 21 when this
  change was introduced; the current repository resolves to 22 because of an
  independent active change.
- Architecture source: `NO_CHANGE`.
- Runtime source: `NO_CHANGE`.
- Decision/archive artifacts: `NO_CHANGE`; archive is not authorized.
