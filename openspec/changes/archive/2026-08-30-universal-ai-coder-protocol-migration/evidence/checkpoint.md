# Universal AI Coder Protocol Migration Checkpoint

DocumentType: `IMPLEMENTATION_EVIDENCE`
Authority: `NONE`
RecordedAt: `2026-08-29`
CheckpointState: `IMPLEMENTATION_COMPLETE_DSH_PIN_REVIEW_PENDING`

## Result

- `.ai/` is the only portable protocol, Profile, Workflow, Skill-body, and shared
  tooling source.
- The canonical workflow is `.ai/workflows/uniflow-coding-workflow.md`; the old
  Codex-named file is a no-semantics compatibility pointer for existing evidence.
- All 18 project Skills have byte-identical discovery adapters in both
  `.agents/skills/` and `.dsh/skills/`, using only
  `../../.ai/skills/<name>` relative links.
- Five reusable Skills and the C# MCP guide moved out of `.claude`. The Skills
  no longer depend on Claude slash commands or Claude-only interaction tools.
- `tools/agent_profile_validator.py` resolves required Skills only from
  `.ai/skills`; Codex and DSH still receive the same ordered canonical bodies.
- The 22 tracked `.claude` entries were removed. Root `CLAUDE.md` remains only
  as a stateless pointer to `AGENTS.md`.
- Bootstrap configuration no longer installs Claude Code, generates a Claude
  settings file, or extracts Claude-only credentials.

## Rollback Evidence

- Archive: `/tmp/uniclaw-ai-coder-migration-backup-20260829-012102.tar.gz`
- SHA-256: `5a780b9f416ff5e8533008e1f19e2a4e1a23574ed285780a7e026cd01039c2a8`
- `tar -tzf` passed before implementation and again at the final static gate.
- Git remains an additional tracked-file recovery path. Recovery must be scoped
  to migration files so unrelated dirty-worktree changes are not overwritten.

## Passing Evidence

- Skill Creator `quick_validate.py`: PASS for `openspec-propose`,
  `openspec-apply-change`, `openspec-explore`, `openspec-archive-change`, and
  `perception-model-intelligence`.
- Focused AgentWorkflow regression: PASS, 99 tests. This includes portable-core
  structure, Validator, Codex discovery/propagation, Leader preflight, Skill
  semantics, focused DSH payload validation, isolated current-revision Host seam,
  and CLI dispatch/receipt tests.
- `python3 tools/agent_profile_validator.py validate`:
  `AGENT_WORKFLOW_VALIDATION_PASS`.
- `bash scripts/setup-dsh-skills.sh`: PASS and idempotent.
- `bash scripts/check-consistency.sh`: PASS, C1-C14; OpenSpec active membership
  and current projections both resolve to 22.
- Strict validation: PASS for `universal-ai-coder-protocol-migration` and
  `uniflow-required-skill-propagation`.
- `bash -n`: PASS for changed setup, consistency, sync, bootstrap, and secrets scripts.
- `shellcheck`: PASS for changed setup, sync, bootstrap, and secrets scripts.
- Current-source `.claude/` dependency scan: PASS via C14 and focused tests.
- `.claude` absence, rollback archive listing, and `git diff --check`: PASS.

## DSH Production Pin Boundary

The repository production Profile Source still fails closed at its independent
trust gate:

```text
FAIL: source revision drift: pinned e2d8dd44214632f50777992d58fb4fe318ad45f0 != current e6c6f4b5eb927d05338128f86058d391cc23a3ba
```

Focused DSH tests pass with an in-memory config pinned to the current checkout;
the tracked production pin was not refreshed. Reviewing and updating that pin
belongs to the DSH Profile Source owner after the final committed revision is
known. This migration proves canonical payload delivery and adapter identity,
not that a real Host/model followed the Skill; actual execution still requires
the existing trusted Host receipt.

## Documentation Sync

- Canonical Source / Contract: `UPDATE` — AGENTS, `.ai` Workflow/Profile,
  Task/Result/Skill contracts, Validator, Host adapters, and current guides.
- Relevant Runtime Layer / Pattern Docs: `NO_CHANGE` — no RuntimeAgent boundary
  or product behavior changed.
- Current Projections: `UPDATE`, `Authority: NONE` — current-gates/latest counts
  match current OpenSpec membership.
- Decision / Archive Receipt: `DEFER_TO_ARCHIVE` — no new Architecture Decision
  or archive transition is authorized here.
- Main Spec Sync: `DEFER_TO_ARCHIVE` — the active change remains the source until
  a separate archive operation.

## Scope Confirmation

This change modified engineering-governance protocol, Skills, adapters, scripts,
tests, guides, bootstrap configuration, and its OpenSpec artifacts only. It did
not modify Runtime, Perception, Architecture Contract, product protocol,
lifecycle authority, or provider availability. Unrelated pre-existing dirty
worktree changes were preserved.
