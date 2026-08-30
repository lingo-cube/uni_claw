# Universal AI Coder Protocol Migration — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_UNIVERSAL_AI_CODER_PROTOCOL_MIGRATION` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-universal-ai-coder-protocol-migration/`
> Authority: AGENTS.md (single project instruction entrypoint) and the portable `.ai/` protocol core (design.md D1) remain the governing baselines; Skills retain `Authority: NONE` and Host directories carry no project authority; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** Codex, DSH, and other AI Coder hosts consuming one unified WorkItem / Profile / Skill contract with Claude project configuration cleaned up, per proposal.md Why (the `.claude/`-anchored generic Skills, OpenSpec playbook, C# MCP guide, and consistency checks, consumed through a DSH skill adapter pointing back at `.claude/`, mixed Claude Host conventions into the shared protocol).

This receipt claims only that:

1. `.ai/` is fixed as the only portable source for AI Coder protocol, Profile, Workflow, Schema, Skill bodies, and shared tooling guidance, and root `AGENTS.md` remains the single project instruction entrypoint (proposal.md What Changes; specs/universal-ai-coder-protocol/spec.md, Requirement "Portable protocol has one canonical source");
2. `.agents/skills/` is fixed as the generic Skill discovery layer and `.dsh/skills/` exists only as an equivalent relative-link Host adapter — both resolving to the same `.ai/skills/<name>/SKILL.md` bundles, with no copied Skill bodies (proposal.md What Changes; spec Requirement "Skill body and discovery adapter are separated");
3. UniFlow required Skills resolve only from `.ai/skills`, with missing, malformed, duplicated, unreadable, or frontmatter-mismatched Skills failing before Worker action, and `.agents`/`.dsh`/`.codex`/`.claude`/caller-supplied paths/historical artifacts not becoming canonical Skill sources (spec Requirement "UniFlow resolves required Skills only from portable core"; MODIFIED Requirement "Required Skills resolve from trusted repository sources");
4. Claude project configuration is retired safely: a verified timestamped rollback archive exists before deletion, `.claude/` is absent after migration, and root `CLAUDE.md` remains only a stateless `AGENTS.md` compatibility pointer (spec Requirement "Claude project configuration is retired safely");
5. current execution entrypoints, active OpenSpec artifacts, Validator code, setup/consistency scripts, current guides, and current-state projections do not resolve or direct users to `.claude/`, while historical Decision/Archive records retain their original references as history only (spec Requirement "Current sources do not depend on Claude paths");
6. migrated Skills and the C# MCP guide retain their original method, safety boundary, and tests, use platform-neutral interaction language, and declare `Authority: NONE` without gaining modification authority over Runtime, architecture, lifecycle, scope, or ownership (design.md D4; spec Requirement "Skill migration preserves method authority boundary").

No claim is made for: new Runtime, Agent, model-routing, or lifecycle authority; migrating Claude Agent legacy code paths, test numbers, permission allowlists, or Hook logic into `.ai`; rewriting historical Decision / Archive / evidence Claude references; or claiming any Host has actually executed a Skill — real Host execution still requires the existing trusted Host receipt (design.md Non-Goals; evidence/checkpoint.md DSH Production Pin Boundary).

## 2. Validation evidence

- tasks.md records all 15 tasks complete (Sections 1.1–5.3): rollback archive creation and boundary recording; Skill/tooling/adapter migration; protocol and Host-adapter updates with Validator and guard changes; `.claude/` deletion after dependency migration; and verification/evidence recording.
- evidence/checkpoint.md (Result): `.ai/` is the only portable protocol, Profile, Workflow, Skill-body, and shared tooling source; the canonical workflow is `.ai/workflows/uniflow-coding-workflow.md` with the old Codex-named file as a no-semantics compatibility pointer.
- evidence/checkpoint.md (Result): all 18 project Skills have byte-identical discovery adapters in both `.agents/skills/` and `.dsh/skills/`, using only `../../.ai/skills/<name>` relative links.
- evidence/checkpoint.md (Result): 5 reusable Skills and the C# MCP guide moved out of `.claude`; the Skills no longer depend on Claude slash commands or Claude-only interaction tools; `tools/agent_profile_validator.py` resolves required Skills only from `.ai/skills` while Codex and DSH still receive the same ordered canonical bodies.
- evidence/checkpoint.md (Result): the 22 tracked `.claude` entries were removed; root `CLAUDE.md` remains only as a stateless pointer to `AGENTS.md`; bootstrap configuration no longer installs Claude Code, generates a Claude settings file, or extracts Claude-only credentials.
- evidence/checkpoint.md (Passing Evidence): Skill Creator `quick_validate.py` **PASS** for `openspec-propose`, `openspec-apply-change`, `openspec-explore`, `openspec-archive-change`, and `perception-model-intelligence`.
- evidence/checkpoint.md (Passing Evidence): focused AgentWorkflow regression **PASS, 99 tests**, covering portable-core structure, Validator, Codex discovery/propagation, Leader preflight, Skill semantics, focused DSH payload validation, isolated current-revision Host seam, and CLI dispatch/receipt tests.
- evidence/checkpoint.md (Passing Evidence): `python3 tools/agent_profile_validator.py validate` → `AGENT_WORKFLOW_VALIDATION_PASS`; `bash scripts/setup-dsh-skills.sh` **PASS and idempotent**; `bash scripts/check-consistency.sh` **PASS, C1–C14** with OpenSpec active membership and current projections both resolving to 22.
- evidence/checkpoint.md (Passing Evidence): strict validation **PASS** for `universal-ai-coder-protocol-migration` and `uniflow-required-skill-propagation`; `bash -n` and `shellcheck` **PASS** for changed setup, consistency, sync, bootstrap, and secrets scripts.
- evidence/checkpoint.md (Passing Evidence): current-source `.claude/` dependency scan **PASS** (C14 + focused tests); `.claude` absence, rollback archive listing, and `git diff --check` **PASS**.
- evidence/checkpoint.md (Rollback Evidence): archive `/tmp/uniclaw-ai-coder-migration-backup-20260829-012102.tar.gz`, SHA-256 `5a780b9f416ff5e8533008e1f19e2a4e1a23574ed285780a7e026cd01039c2a8`, verified with `tar -tzf` before implementation and again at the final static gate; Git remains an additional tracked-file recovery path.
- The change's files record no `dotnet build` / `dotnet test` run on the Runtime solution and claim no Runtime modification (proposal.md Impact "不修改 Runtime、Perception、Architecture Contract…"; evidence/checkpoint.md Scope Confirmation) — verification evidence is limited to the recorded AgentWorkflow regression, Validator, script, consistency, and scan results above.

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (no evidence/ falsifier file and no design.md falsifier section); rejection/negative requirements are defined in specs/universal-ai-coder-protocol/spec.md:

- "Host-specific directories MUST NOT redefine project authority, scope, ownership, permissions, contract, or lifecycle semantics." (Requirement "Portable protocol has one canonical source")
- "Adapter directories MUST NOT contain copied Skill bodies." with scenario "Adapter body or external link is introduced": WHEN an adapter is a normal directory, an absolute link, a dangling link, or resolves outside `.ai/skills`, THEN consistency validation fails closed. (Requirement "Skill body and discovery adapter are separated")
- "`.agents`, `.dsh`, `.codex`, `.claude`, caller-supplied paths, and historical artifacts MUST NOT become canonical Skill sources." with scenario "Claude-local Skill is attempted": WHEN a required Skill exists only in a Host-specific or historical path, THEN dispatch is rejected as required Skill unavailable. (Requirement "UniFlow resolves required Skills only from portable core")
- "After migration, `.claude/` MUST NOT exist." and root `CLAUDE.md` "MUST NOT contain project protocol, Skill, routing, permission, Hook, MCP, or workflow truth." (Requirement "Claude project configuration is retired safely")
- "Historical Decision and Archive records MAY retain their original references but MUST NOT be loaded as current protocol sources." (Requirement "Current sources do not depend on Claude paths")
- "A migrated Skill MUST NOT gain permission to modify Runtime, architecture, lifecycle, scope, or ownership beyond the invoking task." (Requirement "Skill migration preserves method authority boundary")
- "Caller-supplied paths and Host discovery adapter paths MUST NOT become Skill truth sources." (MODIFIED Requirement "Required Skills resolve from trusted repository sources")

Recorded guard results (evidence/checkpoint.md):

| Guard | Result |
|---|---|
| Current-source `.claude/` dependency scan (C14 + focused tests) | **Not falsified — PASS** |
| `.claude` absence, rollback archive listing, `git diff --check` | **Not falsified — PASS** |
| UniFlow required-Skill resolution restricted to `.ai/skills` (Validator + Focused AgentWorkflow regression, 99 tests) | **Not falsified — PASS** |
| Any real Host/model actually executing a Skill after migration | **Not claimed** — "This migration proves canonical payload delivery and adapter identity, not that a real Host/model followed the Skill" (evidence/checkpoint.md DSH Production Pin Boundary) |
| DSH production Profile Source trust gate (pinned revision == current revision) | **Known FAIL, explicitly deferred**: `source revision drift: pinned e2d8dd44214632f50777992d58fb4fe318ad45f0 != current e6c6f4b5eb927d05338128f86058d391cc23a3ba`; pin refresh belongs to the DSH Profile Source owner after the final committed revision is known (evidence/checkpoint.md) |

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- New Runtime, Agent, model routing, or lifecycle authority (design.md Non-Goals; proposal.md Impact).
- Migrating Claude Agent legacy code paths, test numbers, permission allowlists, or Hook logic into `.ai` (design.md Non-Goals D4).
- Rewriting historical Decision / Archive / evidence Claude references (design.md Non-Goals / D6).
- Claiming actual Host execution of the migrated protocol/Skills — that remains gated by the existing trusted Host receipt (design.md Non-Goals; evidence/checkpoint.md DSH Production Pin Boundary).
- Refreshing the DSH production Profile Source revision pin (evidence/checkpoint.md — owner action after the final committed revision is known).
- Architecture Decision / archive transition authority beyond this receipt, and main-spec sync beyond this batch's capability sync (evidence/checkpoint.md Documentation Sync: `DEFER_TO_ARCHIVE` items — resolved here as the separate archive/finalization operation of 2026-08-30).

## 5. Final conclusion

**GRADUATED.** The single portable `.ai/` protocol core, the relative-link Skill discovery adapters, the `.ai/skills`-only UniFlow resolution, the safe retirement of `.claude/`, and the zero-current-dependency posture for Claude paths are human-authorized, evidence-verified, and bounded by the spec requirements cited above; deferred scope remains unauthorized for separate gates. Archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.