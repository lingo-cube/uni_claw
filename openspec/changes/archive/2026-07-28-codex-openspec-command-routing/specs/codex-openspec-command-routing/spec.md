## ADDED Requirements

### Requirement: Codex OpenSpec lifecycle triggers
The project guidance SHALL define natural-language Codex triggers for the OpenSpec lifecycle actions `propose`, `apply`, `explore`, and `archive`.

#### Scenario: Codex receives an OpenSpec lifecycle request
- **WHEN** a user asks Codex to run `openspec propose`, `openspec apply`, `openspec explore`, or `openspec archive`
- **THEN** Codex maps the request to the corresponding OpenSpec lifecycle action described in `AGENTS.md`

### Requirement: Codex playbook reuse
The project guidance SHALL map each Codex OpenSpec lifecycle action to the matching `.claude/skills/openspec-*` playbook when detailed workflow instructions are needed.

#### Scenario: Codex needs lifecycle workflow details
- **WHEN** Codex needs detailed steps for an OpenSpec lifecycle action
- **THEN** Codex reads the mapped `.claude/skills/openspec-*/SKILL.md` file before executing that action

### Requirement: Shared artifact authority
The project guidance SHALL state that OpenSpec artifacts under `openspec/changes/` remain the source of truth for active change progress, regardless of whether Claude Code or Codex performs the work.

#### Scenario: Codex executes an OpenSpec apply request
- **WHEN** Codex applies an active OpenSpec change
- **THEN** Codex reads the change artifacts under `openspec/changes/<change>/` and updates `tasks.md` as tasks are completed

### Requirement: Claude command boundary
The project guidance SHALL clarify that Claude slash commands remain Claude-specific and are not native Codex commands.

#### Scenario: A user expects Claude slash commands in Codex
- **WHEN** a user references `/opsx:*` or `/openspec-*` while using Codex
- **THEN** Codex treats the request as an OpenSpec natural-language request rather than a native slash command invocation
