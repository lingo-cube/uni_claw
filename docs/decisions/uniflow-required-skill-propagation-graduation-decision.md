# UniFlow Required Skill Propagation — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_UNIFLOW_REQUIRED_SKILL_PROPAGATION` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-uniflow-required-skill-propagation/`
> Authority: Runtime Architecture Contract I-1..I-14, Architecture v1, and the UniFlow workflow / WorkItem / repository-Skill contracts this change relies on remain the governing baselines; Skills remain `Authority: NONE` and cannot expand WorkItem scope, permissions, contracts, or lifecycle authority; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** UniFlow Worker/WorkItem chain — Leader-selected debugging Skills enter the verifiable WorkItem / ModuleContext path so Codex and DSH Workers follow the project debugging method before deep code-chain analysis (per proposal.md Why).

This receipt claims only that:

1. WorkItem carries an ordered, duplicate-free, backward-compatible `required_skills` name array, emitted explicitly for every newly built WorkItem, with an omitted field interpreted as an empty list and stored historical records unmutated (proposal.md What Changes; design.md D1/D2);
2. names resolve uniquely from the repository-owned `.ai/skills/<name>/SKILL.md` with frontmatter `name` match, and malformed, missing, duplicate, unreadable, frontmatter-mismatched, or non-Skill entries fail closed before Worker execution — caller-supplied absolute paths, traversal paths, and `.agents`/`.dsh` adapter paths are never Skill truth sources (design.md D3; specs/uniflow-required-skill-propagation/spec.md);
3. Codex Worker adapters and the DSH ModuleContext/Host payload receive the same ordered canonical Skill paths, complete Skill bodies, and fail-closed loading directive, with Skill content participating in the context digest (design.md D4/D5);
4. delayed DSH dispatch records persist the complete validated Worker payload; missing, empty, reordered, path-mismatched, or digest-mismatched Skill payloads are rejected as `REQUIRED_SKILL_UNAVAILABLE` before Host spawn (design.md D4/D5; tasks.md 2.2/2.3; spec scenario "DSH rejects incomplete Skill payload before spawn");
5. Bug routing requires `evidence-driven-debugging` for Bug/failure investigations and additionally `runtime-behavior-debugging` for Runtime, FSM, Traversal, Recovery, asynchronous, real-device, flaky, or nondeterministic behavior — an execution method, not architecture or modification authority (spec; design.md Risks/Trade-offs);
6. the Leader performs a bounded Reality Preflight before semantic attribution, architecture judgment, or deep code traversal, and marks missing UI evidence unknown rather than inventing visible state (design.md D5.1; spec);
7. the affected Skills (`evidence-driven-debugging`, `runtime-behavior-debugging`, `uniagent-evolution-loop`) gain UI-first, falsifiable interaction-hypothesis guidance that does not convert coordinates, fixed click sequences, incidental labels, timing, or one observed path into Runtime authority (design.md D6; spec).

No claim is made for: automatic architecture decision, code-fix selection, Worker fanout, MCP, plugin, or any new Runtime protocol; a semantic classifier that guesses Bug type from arbitrary Chinese text; migration rewrite of historical WorkItems or DSH receipts; any change to Runtime, Perception, Strategy Contract, GoalEvidence, SourceIdentity, product behavior, or lifecycle authority; or proof that a model actually followed a Skill instruction (the repository proves adapter delivery only — per evidence/checkpoint.md Host execution boundary).

## 2. Validation evidence

- tasks.md records all 13 task checklist items complete ([x]) across sections 1–5 (Portable WorkItem Contract, Worker Adapters, UI-first Debugging Method, UniFlow Contract and Documentation, Verification) and points to `evidence/checkpoint.md` as the verification record.
- `python3 -m unittest tests/AgentWorkflow/test_agent_profile_validator.py` — PASS, 58 tests (evidence/checkpoint.md, Passing evidence).
- `python3 -m unittest tests/AgentWorkflow/test_codex_skill_propagation.py tests/AgentWorkflow/test_codex_skill_discovery.py` — PASS, 6 tests (evidence/checkpoint.md).
- `python3 -m unittest tests/AgentWorkflow/test_skill_semantics.py` — PASS, 3 tests (evidence/checkpoint.md).
- Focused DSH propagation test `DshProfileAdapterTests.test_31_required_skills_enter_manifest_and_envelope` — PASS, 1 test (evidence/checkpoint.md).
- Follow-up DSH consumer, CLI persistence, Leader Reality Preflight, shared validator, Codex propagation/discovery, and Skill semantic suite — PASS, 93 tests total (evidence/checkpoint.md).
- Skill Creator `quick_validate.py` — PASS for all three changed Skill bodies (evidence/checkpoint.md).
- `python3 tools/agent_profile_validator.py validate` — `AGENT_WORKFLOW_VALIDATION_PASS` (evidence/checkpoint.md).
- `bash scripts/check-consistency.sh` — PASS, C1–C13; active membership and snapshot counts both resolve to 22 (evidence/checkpoint.md).
- `openspec validate uniflow-required-skill-propagation --strict` — PASS (evidence/checkpoint.md).
- `git diff --check` — PASS (evidence/checkpoint.md).
- The change's files record no `dotnet build` / `dotnet test` run; the change modifies no runtime build artifacts (proposal.md Impact lists engineering-governance schemas, validators, adapters, Worker instructions, Skill bodies, tests, documentation), so verification evidence is limited to the Python/DSH test and validation runs above.
- Boundary confirmation: implementation evidence records that the change modified only engineering-governance schemas, validators, adapters, Worker instructions, Skill bodies, tests, documentation, and its OpenSpec bundle — not Runtime, Perception, Strategy Contract, GoalEvidence, SourceIdentity, product behavior, or lifecycle authority (evidence/checkpoint.md, Boundary confirmation).

## 3. Scenario receipts and falsifiers

The change's files contain explicit falsifier / negative-proof records:

| Falsifier | Result |
|---|---|
| Full AgentWorkflow regression suite is green in-repo | **Not supported (pre-existing external blocker)**: the complete discovery run was executed from a temporary working directory, ran 140 tests, and was stopped by the same pre-existing trust gate — 10 failures and 19 errors downstream of Profile Source revision drift, reproduced directly by `python3 tools/dsh_profile_adapter.py validate` (`FAIL: source revision drift: pinned e2d8dd44214632f50777992d58fb4fe318ad45f0 != current e6c6f4b5eb927d05338128f86058d391cc23a3ba`). The propagation implementation itself is covered by the passing shared-validator, Codex, Skill semantic, and focused DSH tests; pin refresh is outside this change and remains a Human Gate (evidence/checkpoint.md, Full-regression divergence and Human Gate). |
| Caller-controlled paths / `.agents` / `.dsh` adapters become Skill truth sources | **Not falsified**: resolution is exactly `.ai/skills/<name>/SKILL.md` with frontmatter `name` match; missing, duplicated, malformed, unreadable, or mismatched entries fail before dispatch (design.md D3; spec requirement "Required Skills resolve from trusted repository sources"). |
| DSH spawns with an incomplete or reordered Skill payload | **Not falsified**: DSH rejects missing, empty, reordered, path-mismatched, or digest-mismatched Skill documents as `REQUIRED_SKILL_UNAVAILABLE` before Host spawn (evidence/checkpoint.md Implemented; spec scenario "DSH rejects incomplete Skill payload before spawn"). |
| A Skill change under a reused Worker context silently serves stale context | **Not falsified**: Skill paths and bytes participate in `RuleDigest`, invalidating the ProfileContextKey (design.md Risks/Trade-offs). |
| Skill rename breaks active WorkItems silently | **Not falsified**: resolution fails closed with the missing name; rename must update the WorkItem before dispatch (design.md Risks/Trade-offs). |
| UI-first guidance degrades into a click script / Runtime authority | **Not falsified**: Skill text and tests preserve the explicit no-coordinate / no-fixed-sequence / Runtime-authority boundary (design.md D6 and Risks/Trade-offs). |
| Adapting Skill context delivery is treated as proof a model followed the instruction | **Not falsified (boundary explicit)**: this is delivery evidence only; model self-report remains insufficient, and actual DSH execution still requires the existing Host receipt and session integration evidence (evidence/checkpoint.md, Host execution boundary). |
| The change alters protected product areas | **Not falsified**: boundary confirmation records no changes to Runtime, Perception, Strategy Contract, GoalEvidence, SourceIdentity, product behavior, or lifecycle authority (evidence/checkpoint.md, Boundary confirmation). |

Rejection/negative requirements are also defined in specs/uniflow-required-skill-propagation/spec.md: "Caller-supplied absolute paths, traversal paths, and `.agents` adapter paths MUST NOT become Skill truth sources"; "A Skill remains `Authority: NONE` and MUST NOT expand WorkItem scope, permissions, contracts, or lifecycle authority"; Bug-routing selection "MUST NOT be inferred as architecture or modification authority"; the Reality Preflight working view "MUST guide evidence entry and Owner routing without becoming a Fact, contract, Runtime belief, fixed interaction script, or modification authority" and "MUST mark [UI evidence] unknown rather than inventing visible state" when unavailable; and the UI-first Skills "MUST NOT convert coordinates, fixed click sequences, incidental labels, timing, or one observed UI path into Runtime authority or scenario knowledge."

## 4. Deferred scope

- Automatic architecture decision, code-fix selection, Worker fanout, MCP, plugin, or new Runtime protocol (design.md Non-Goals).
- Semantic classifier that guesses Bug type from arbitrary Chinese text (design.md Non-Goals).
- Migration rewrite of historical WorkItems or DSH receipts (design.md Non-Goals).
- DSH Profile Source revision pin refresh (`.dsh/profile-adapter/profile-source.yaml`): refreshing the trust pin is outside this change and remains a Human Gate for the DSH Profile Source owner (evidence/checkpoint.md, tasks.md Verification).
- End-to-end DSH Host execution evidence (Host receipt + session integration) that a model actually followed a Skill instruction — the repository proves adapter delivery only (evidence/checkpoint.md, Host execution boundary).

## 5. Final conclusion

**GRADUATED.** The bounded claim — portable ordered `required_skills` propagation from the Leader WorkItem through the shared validator into Codex and DSH Worker contexts with fail-closed resolution, plus the bounded Reality Preflight and UI-first Skill guidance — is supported by the recorded passing validator/Codex/DSH/Skill test runs, `git diff --check`, consistency checks, and strict OpenSpec validation, and by the explicit boundary confirmation in evidence/checkpoint.md. Archival of the change under `openspec/changes/archive/2026-08-30-uniflow-required-skill-propagation/` is performed on 2026-08-30 as a separate lifecycle operation in this batch.
