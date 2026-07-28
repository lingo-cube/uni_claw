## Why

Claude Code already has `/opsx:*` and `/openspec-*` command entry points, but Codex does not execute those slash commands directly. The project needs a repo-level, durable mapping so Codex can consistently run the same OpenSpec lifecycle from natural-language prompts.

## What Changes

- Add shared Codex trigger rules to `AGENTS.md` for `openspec propose/apply/explore/archive`.
- Define which existing `.claude/skills/openspec-*` playbook Codex should read for each lifecycle action.
- Clarify that Claude slash commands remain Claude-only while OpenSpec artifacts remain shared.
- Keep the change docs-only; no C# production behavior changes.

## Capabilities

### New Capabilities
- `codex-openspec-command-routing`: Defines how Codex recognizes and executes OpenSpec lifecycle requests in this repository.

### Modified Capabilities
- None.

## Impact

- `AGENTS.md`: Adds Codex-specific OpenSpec trigger rules under the shared project protocol.
- `CLAUDE.md`: No behavioral change; remains a Claude Code adapter pointing to `AGENTS.md`.
- `.claude/skills/openspec-*`: Reused as playbooks by reference; no duplication or migration in this change.
- Runtime code and tests: No impact.
