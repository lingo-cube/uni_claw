# universal-ai-coder-protocol Specification

## Purpose

让 Codex、DSH 与其他采用通用文件协议的 AI Coder 共享一个项目协议与 Skill
真相源，同时把 Host 兼容配置限制为不产生语义的薄 adapter。

## Requirements

### Requirement: Portable protocol has one canonical source

The repository MUST maintain portable AI Coder protocol, Profile, Workflow, Schema,
Skill bodies, and shared tooling guidance under `.ai/`. Root `AGENTS.md` MUST remain
the single project instruction entrypoint. Host-specific directories MUST NOT redefine
project authority, scope, ownership, permissions, contract, or lifecycle semantics.

#### Scenario: A new AI Coder enters the repository

- **WHEN** an AI Coder supports `AGENTS.md` and `.agents/skills`
- **THEN** it can consume the project rules and Skills without reading a Claude-specific source

### Requirement: Skill body and discovery adapter are separated

Every project Skill body MUST exist only at `.ai/skills/<name>/SKILL.md`. Every
`.agents/skills/<name>` entry MUST be a project-internal relative symbolic link to
that canonical bundle. A `.dsh/skills/<name>` entry MAY exist only as an equivalent
DSH Host adapter and MUST resolve to the same `.ai/skills` bundle. Adapter directories
MUST NOT contain copied Skill bodies.

#### Scenario: Codex and DSH discover the same Skill

- **WHEN** both Hosts discover a named project Skill
- **THEN** their adapter paths resolve to the same `.ai/skills/<name>/SKILL.md` bytes

#### Scenario: Adapter body or external link is introduced

- **WHEN** an adapter is a normal directory, an absolute link, a dangling link, or resolves outside `.ai/skills`
- **THEN** consistency validation fails closed

### Requirement: UniFlow resolves required Skills only from portable core

The UniFlow Validator MUST resolve `required_skills` only from `.ai/skills`. Missing,
malformed, duplicated, unreadable, or frontmatter-mismatched Skills MUST fail before
Worker action. `.agents`, `.dsh`, `.codex`, `.claude`, caller-supplied paths, and
historical artifacts MUST NOT become canonical Skill sources.

#### Scenario: Canonical Skill resolves

- **WHEN** a WorkItem names a valid unique Skill under `.ai/skills`
- **THEN** Codex and DSH Worker payloads contain that canonical body and repository-relative path

#### Scenario: Claude-local Skill is attempted

- **WHEN** a required Skill exists only in a Host-specific or historical path
- **THEN** dispatch is rejected as required Skill unavailable

### Requirement: Claude project configuration is retired safely

Before deleting `.claude/`, the migration MUST create and verify a timestamped rollback
archive. After migration, `.claude/` MUST NOT exist. Root `CLAUDE.md` MAY remain only
as a stateless compatibility adapter that points to `AGENTS.md` and MUST NOT contain
project protocol, Skill, routing, permission, Hook, MCP, or workflow truth.

#### Scenario: Migration completes

- **WHEN** all current references and adapters have moved to the portable core
- **THEN** `.claude/` is absent and the rollback archive can be listed successfully

### Requirement: Current sources do not depend on Claude paths

Current execution entrypoints, active OpenSpec artifacts, Validator code, setup and
consistency scripts, current guides, and current-state projections MUST NOT resolve
or direct users to `.claude/`. Historical Decision and Archive records MAY retain
their original references but MUST NOT be loaded as current protocol sources.

#### Scenario: Current reference scan runs

- **WHEN** mechanical validation scans the defined current-source set
- **THEN** it finds no `.claude/` dependency outside the explicitly minimal `CLAUDE.md` compatibility statement

### Requirement: Skill migration preserves method authority boundary

Migrated Skills MUST retain their original task method, safety boundary, and tests,
use platform-neutral interaction language, and declare `Authority: NONE`. A migrated
Skill MUST NOT gain permission to modify Runtime, architecture, lifecycle, scope, or
ownership beyond the invoking task.

#### Scenario: OpenSpec Skill is used by different Hosts

- **WHEN** Codex, DSH, or another AI Coder follows the Skill
- **THEN** the same OpenSpec lifecycle and fail/stop behavior applies without requiring a Claude tool name or slash command

### Requirement: Required Skills resolve from trusted repository sources

The WorkItem validator SHALL accept only valid Skill names and SHALL resolve each
name uniquely from the repository-owned `.ai/skills` root. Missing, duplicated,
malformed, unreadable, or frontmatter-mismatched entries MUST fail before Worker
execution. Caller-supplied paths and Host discovery adapter paths MUST NOT become
Skill truth sources.

#### Scenario: Valid project Skill resolves after migration

- **WHEN** `required_skills` names a readable `.ai/skills/<name>/SKILL.md`
- **THEN** ModuleContext contains that canonical repository-relative path
